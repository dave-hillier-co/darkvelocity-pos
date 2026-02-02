using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.Host.Services;

/// <summary>
/// Stub implementation of email ingestion for development.
/// In production, replace with SendGridEmailIngestionService, SesEmailIngestionService, etc.
/// </summary>
public partial class StubEmailIngestionService : IEmailIngestionService
{
    private readonly ILogger<StubEmailIngestionService> _logger;

    // Pattern: invoices-{orgId}-{siteId}@domain.com or invoices-{siteId}@domain.com
    [GeneratedRegex(@"^(?<type>invoices|receipts|expenses)-(?:(?<orgId>[a-f0-9-]+)-)?(?<siteId>[a-f0-9-]+)@", RegexOptions.IgnoreCase)]
    private static partial Regex InboxAddressPattern();

    public StubEmailIngestionService(ILogger<StubEmailIngestionService> logger)
    {
        _logger = logger;
    }

    public Task<ParsedEmail> ParseEmailAsync(
        Stream emailContent,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parsing email from stream, content-type: {ContentType}", contentType);

        // For webhook payloads (JSON format from SendGrid, Mailgun, etc.)
        if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return ParseJsonWebhookAsync(emailContent, cancellationToken);
        }

        // For raw MIME emails
        using var reader = new StreamReader(emailContent);
        var content = reader.ReadToEnd();
        return ParseMimeEmailAsync(content, cancellationToken);
    }

    public Task<ParsedEmail> ParseMimeEmailAsync(
        string mimeContent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Using stub MIME parser - returning mock email data");

        // In a real implementation, use MimeKit or similar library
        var email = new ParsedEmail
        {
            MessageId = $"stub-{Guid.NewGuid():N}@darkvelocity.local",
            From = "supplier@example.com",
            FromName = "Acme Supplies",
            To = "invoices-test@darkvelocity.io",
            Subject = "Invoice #INV-2024-001",
            TextBody = "Please find attached invoice for your recent order.",
            SentAt = DateTime.UtcNow.AddMinutes(-5),
            ReceivedAt = DateTime.UtcNow,
            Attachments = new List<EmailAttachment>
            {
                new EmailAttachment
                {
                    Filename = "invoice-2024-001.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 52480,
                    Content = GenerateMockPdfContent()
                }
            }
        };

        return Task.FromResult(email);
    }

    public SiteEmailInfo? ParseInboxAddress(string emailAddress)
    {
        if (string.IsNullOrEmpty(emailAddress))
            return null;

        var match = InboxAddressPattern().Match(emailAddress);
        if (!match.Success)
            return null;

        var inboxType = match.Groups["type"].Value.ToLowerInvariant();
        var siteIdStr = match.Groups["siteId"].Value;
        var orgIdStr = match.Groups["orgId"].Value;

        if (!Guid.TryParse(siteIdStr, out var siteId))
            return null;

        // If org ID is not in the address, we'll need to look it up from siteId
        var orgId = Guid.TryParse(orgIdStr, out var parsedOrgId)
            ? parsedOrgId
            : Guid.Empty; // Will be resolved later from site lookup

        return new SiteEmailInfo(orgId, siteId, inboxType);
    }

    private async Task<ParsedEmail> ParseJsonWebhookAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        // Parse generic webhook format
        // Real implementations would handle SendGrid, Mailgun, AWS SES formats
        var doc = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        var attachments = new List<EmailAttachment>();

        if (root.TryGetProperty("attachments", out var attachmentsElement))
        {
            foreach (var att in attachmentsElement.EnumerateArray())
            {
                var filename = att.GetProperty("filename").GetString() ?? "attachment";
                var contentType = att.GetProperty("content_type").GetString() ?? "application/octet-stream";
                var contentBase64 = att.GetProperty("content").GetString() ?? "";

                var contentBytes = Convert.FromBase64String(contentBase64);

                attachments.Add(new EmailAttachment
                {
                    Filename = filename,
                    ContentType = contentType,
                    SizeBytes = contentBytes.Length,
                    Content = contentBytes
                });
            }
        }

        return new ParsedEmail
        {
            MessageId = root.TryGetProperty("message_id", out var mid) ? mid.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString(),
            From = root.TryGetProperty("from", out var from) ? from.GetString() ?? "" : "",
            FromName = root.TryGetProperty("from_name", out var fromName) ? fromName.GetString() : null,
            To = root.TryGetProperty("to", out var to) ? to.GetString() ?? "" : "",
            Subject = root.TryGetProperty("subject", out var subject) ? subject.GetString() ?? "" : "",
            TextBody = root.TryGetProperty("text", out var text) ? text.GetString() : null,
            HtmlBody = root.TryGetProperty("html", out var html) ? html.GetString() : null,
            SentAt = root.TryGetProperty("timestamp", out var ts) ? DateTimeOffset.FromUnixTimeSeconds(ts.GetInt64()).UtcDateTime : DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow,
            Attachments = attachments
        };
    }

    private static byte[] GenerateMockPdfContent()
    {
        // Minimal PDF header for testing
        var pdfContent = "%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF";
        return System.Text.Encoding.ASCII.GetBytes(pdfContent);
    }
}
