using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Services;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class EmailIngestionEndpoints
{
    public static WebApplication MapEmailIngestionEndpoints(this WebApplication app)
    {
        // Webhook endpoint for receiving emails (called by email service)
        var webhookGroup = app.MapGroup("/api/webhooks/email").WithTags("EmailWebhooks");

        // Generic inbound email webhook (SendGrid, Mailgun, AWS SES format)
        webhookGroup.MapPost("/inbound", async (
            HttpRequest request,
            [FromServices] IEmailIngestionService emailService,
            [FromServices] IGrainFactory grainFactory,
            [FromServices] ILogger<Program> logger) =>
        {
            try
            {
                // Parse the email
                var email = await emailService.ParseEmailAsync(
                    request.Body,
                    request.ContentType ?? "application/json");

                // Extract site info from recipient address
                var siteInfo = emailService.ParseInboxAddress(email.To);
                if (siteInfo == null)
                {
                    logger.LogWarning("Invalid inbox address: {To}", email.To);
                    return Results.BadRequest(new { error = "invalid_inbox", message = "Could not parse inbox address" });
                }

                // If org ID wasn't in address, we need to look it up
                // For now, require it in the address format: invoices-{orgId}-{siteId}@domain.com
                if (siteInfo.OrganizationId == Guid.Empty)
                {
                    return Results.BadRequest(new { error = "missing_org", message = "Organization ID required in inbox address" });
                }

                // Get the inbox grain
                var inboxGrain = grainFactory.GetGrain<IEmailInboxGrain>(
                    GrainKeys.EmailInbox(siteInfo.OrganizationId, siteInfo.SiteId));

                if (!await inboxGrain.ExistsAsync())
                {
                    logger.LogWarning("Inbox not found for site: {SiteId}", siteInfo.SiteId);
                    return Results.NotFound(new { error = "inbox_not_found" });
                }

                // Determine document type hint from inbox type
                PurchaseDocumentType? typeHint = siteInfo.InboxType switch
                {
                    "invoices" => PurchaseDocumentType.Invoice,
                    "receipts" => PurchaseDocumentType.Receipt,
                    _ => null
                };

                // Process the email
                var result = await inboxGrain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email, typeHint));

                if (result.Accepted)
                {
                    return Results.Ok(new
                    {
                        accepted = true,
                        messageId = result.MessageId,
                        documentsCreated = result.DocumentIds?.Count ?? 0,
                        documentIds = result.DocumentIds
                    });
                }
                else
                {
                    return Results.Ok(new
                    {
                        accepted = false,
                        messageId = result.MessageId,
                        rejectionReason = result.RejectionReason?.ToString(),
                        rejectionDetails = result.RejectionDetails
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing inbound email");
                return Results.Problem("Error processing email");
            }
        });

        // Site-specific inbox management
        var inboxGroup = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/email-inbox").WithTags("EmailInbox");

        // Initialize inbox for a site
        inboxGroup.MapPost("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] InitializeInboxRequest request,
            IGrainFactory grainFactory) =>
        {
            var inboxGrain = grainFactory.GetGrain<IEmailInboxGrain>(GrainKeys.EmailInbox(orgId, siteId));

            if (await inboxGrain.ExistsAsync())
                return Results.Conflict(Hal.Error("already_exists", "Inbox already initialized"));

            var inboxAddress = request.InboxAddress ?? $"invoices-{orgId}-{siteId}@darkvelocity.io";

            var snapshot = await inboxGrain.InitializeAsync(new InitializeEmailInboxCommand(
                orgId,
                siteId,
                inboxAddress,
                request.DefaultDocumentType ?? PurchaseDocumentType.Invoice,
                request.AutoProcess ?? true));

            return Results.Created(
                $"/api/orgs/{orgId}/sites/{siteId}/email-inbox",
                Hal.Resource(snapshot, BuildInboxLinks(orgId, siteId)));
        });

        // Get inbox status
        inboxGroup.MapGet("/", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var inboxGrain = grainFactory.GetGrain<IEmailInboxGrain>(GrainKeys.EmailInbox(orgId, siteId));

            if (!await inboxGrain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Inbox not found"));

            var snapshot = await inboxGrain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildInboxLinks(orgId, siteId)));
        });

        // Update inbox settings
        inboxGroup.MapPatch("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] UpdateInboxRequest request,
            IGrainFactory grainFactory) =>
        {
            var inboxGrain = grainFactory.GetGrain<IEmailInboxGrain>(GrainKeys.EmailInbox(orgId, siteId));

            if (!await inboxGrain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Inbox not found"));

            var snapshot = await inboxGrain.UpdateSettingsAsync(new UpdateInboxSettingsCommand(
                request.AllowedSenderDomains,
                request.AllowedSenderEmails,
                request.MaxAttachmentSizeBytes,
                request.DefaultDocumentType,
                request.AutoProcess,
                request.IsActive));

            return Results.Ok(Hal.Resource(snapshot, BuildInboxLinks(orgId, siteId)));
        });

        // Activate inbox
        inboxGroup.MapPost("/activate", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var inboxGrain = grainFactory.GetGrain<IEmailInboxGrain>(GrainKeys.EmailInbox(orgId, siteId));

            if (!await inboxGrain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Inbox not found"));

            await inboxGrain.ActivateInboxAsync();
            var snapshot = await inboxGrain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildInboxLinks(orgId, siteId)));
        });

        // Deactivate inbox
        inboxGroup.MapPost("/deactivate", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var inboxGrain = grainFactory.GetGrain<IEmailInboxGrain>(GrainKeys.EmailInbox(orgId, siteId));

            if (!await inboxGrain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Inbox not found"));

            await inboxGrain.DeactivateInboxAsync();
            var snapshot = await inboxGrain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildInboxLinks(orgId, siteId)));
        });

        // Test endpoint: Simulate receiving an email (for development)
        inboxGroup.MapPost("/test", async (
            Guid orgId,
            Guid siteId,
            [FromBody] TestEmailRequest request,
            IGrainFactory grainFactory,
            IEmailIngestionService emailService) =>
        {
            var inboxGrain = grainFactory.GetGrain<IEmailInboxGrain>(GrainKeys.EmailInbox(orgId, siteId));

            if (!await inboxGrain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Inbox not found"));

            // Create a mock email
            var mockEmail = new ParsedEmail
            {
                MessageId = $"test-{Guid.NewGuid():N}@darkvelocity.local",
                From = request.From ?? "test-supplier@example.com",
                FromName = request.FromName ?? "Test Supplier",
                To = $"invoices-{orgId}-{siteId}@darkvelocity.io",
                Subject = request.Subject ?? "Test Invoice #TEST-001",
                TextBody = request.Body ?? "This is a test email with an attached invoice.",
                SentAt = DateTime.UtcNow,
                ReceivedAt = DateTime.UtcNow,
                Attachments = new List<EmailAttachment>
                {
                    new EmailAttachment
                    {
                        Filename = request.AttachmentFilename ?? "test-invoice.pdf",
                        ContentType = "application/pdf",
                        SizeBytes = 1024,
                        Content = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\ntest\n%%EOF")
                    }
                }
            };

            var result = await inboxGrain.ProcessEmailAsync(new ProcessIncomingEmailCommand(
                mockEmail,
                request.DocumentType));

            return Results.Ok(new
            {
                accepted = result.Accepted,
                messageId = result.MessageId,
                documentsCreated = result.DocumentIds?.Count ?? 0,
                documentIds = result.DocumentIds,
                rejectionReason = result.RejectionReason?.ToString(),
                rejectionDetails = result.RejectionDetails
            });
        });

        return app;
    }

    private static Dictionary<string, object> BuildInboxLinks(Guid orgId, Guid siteId)
    {
        var basePath = $"/api/orgs/{orgId}/sites/{siteId}/email-inbox";
        return new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["activate"] = new { href = $"{basePath}/activate" },
            ["deactivate"] = new { href = $"{basePath}/deactivate" },
            ["test"] = new { href = $"{basePath}/test" },
            ["purchases"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/purchases" }
        };
    }
}
