using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Services;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
public class EmailInboxGrainTests
{
    private readonly TestClusterFixture _fixture;

    public EmailInboxGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IEmailInboxGrain GetGrain(Guid orgId, Guid siteId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IEmailInboxGrain>(
            GrainKeys.EmailInbox(orgId, siteId));
    }

    private static ParsedEmail CreateTestEmail(
        string from = "supplier@example.com",
        string subject = "Invoice #TEST-001",
        string? messageId = null,
        List<EmailAttachment>? attachments = null)
    {
        return new ParsedEmail
        {
            MessageId = messageId ?? $"test-{Guid.NewGuid():N}@test.local",
            From = from,
            FromName = "Test Supplier",
            To = "invoices-test@darkvelocity.io",
            Subject = subject,
            TextBody = "Please find attached invoice.",
            SentAt = DateTime.UtcNow.AddMinutes(-5),
            ReceivedAt = DateTime.UtcNow,
            Attachments = attachments ?? new List<EmailAttachment>
            {
                new EmailAttachment
                {
                    Filename = "invoice.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 1024,
                    Content = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\ntest\n%%EOF")
                }
            }
        };
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateInbox()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        // Act
        var snapshot = await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId,
            siteId,
            $"invoices-{orgId}-{siteId}@darkvelocity.io"));

        // Assert
        snapshot.OrganizationId.Should().Be(orgId);
        snapshot.SiteId.Should().Be(siteId);
        snapshot.IsActive.Should().BeTrue();
        snapshot.DefaultDocumentType.Should().Be(PurchaseDocumentType.Invoice);
        snapshot.AutoProcess.Should().BeTrue();
        snapshot.TotalEmailsReceived.Should().Be(0);
    }

    [Fact]
    public async Task InitializeAsync_AlreadyInitialized_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io"));

        // Act
        var act = () => grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Inbox already initialized");
    }

    [Fact]
    public async Task ProcessEmailAsync_ValidEmail_ShouldAcceptAndCreateDocument()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            autoProcess: false)); // Disable auto-process for simpler testing

        var email = CreateTestEmail();

        // Act
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert
        result.Accepted.Should().BeTrue();
        result.MessageId.Should().Be(email.MessageId);
        result.DocumentIds.Should().NotBeNull();
        result.DocumentIds.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessEmailAsync_DuplicateEmail_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            autoProcess: false));

        var email = CreateTestEmail(messageId: "duplicate-message-123@test.local");

        // Process first time
        await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Act - process again
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert
        result.Accepted.Should().BeFalse();
        result.RejectionReason.Should().Be(EmailRejectionReason.Duplicate);
    }

    [Fact]
    public async Task ProcessEmailAsync_UnauthorizedSender_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            autoProcess: false));

        // Restrict to specific domain
        await grain.UpdateSettingsAsync(new UpdateInboxSettingsCommand(
            AllowedSenderDomains: new List<string> { "trusted-supplier.com" }));

        var email = CreateTestEmail(from: "untrusted@malicious.com");

        // Act
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert
        result.Accepted.Should().BeFalse();
        result.RejectionReason.Should().Be(EmailRejectionReason.UnauthorizedSender);
    }

    [Fact]
    public async Task ProcessEmailAsync_NoAttachments_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            autoProcess: false));

        var email = CreateTestEmail(attachments: new List<EmailAttachment>());

        // Act
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert
        result.Accepted.Should().BeFalse();
        result.RejectionReason.Should().Be(EmailRejectionReason.NoAttachments);
    }

    [Fact]
    public async Task ProcessEmailAsync_InactiveInbox_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            autoProcess: false));

        await grain.DeactivateInboxAsync();

        var email = CreateTestEmail();

        // Act
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert
        result.Accepted.Should().BeFalse();
        result.RejectionReason.Should().Be(EmailRejectionReason.SiteNotFound);
    }

    [Fact]
    public async Task UpdateSettingsAsync_ShouldUpdateConfiguration()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io"));

        // Act
        var snapshot = await grain.UpdateSettingsAsync(new UpdateInboxSettingsCommand(
            AllowedSenderDomains: new List<string> { "supplier1.com", "supplier2.com" },
            MaxAttachmentSizeBytes: 10 * 1024 * 1024, // 10MB
            DefaultDocumentType: PurchaseDocumentType.Receipt,
            AutoProcess: false));

        // Assert
        snapshot.DefaultDocumentType.Should().Be(PurchaseDocumentType.Receipt);
        snapshot.AutoProcess.Should().BeFalse();

        var state = await grain.GetStateAsync();
        state.AllowedSenderDomains.Should().Contain("supplier1.com");
        state.AllowedSenderDomains.Should().Contain("supplier2.com");
        state.MaxAttachmentSizeBytes.Should().Be(10 * 1024 * 1024);
    }

    [Fact]
    public async Task ActivateDeactivateAsync_ShouldToggleState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io"));

        // Act & Assert - should be active initially
        (await grain.GetSnapshotAsync()).IsActive.Should().BeTrue();

        // Deactivate
        await grain.DeactivateInboxAsync();
        (await grain.GetSnapshotAsync()).IsActive.Should().BeFalse();

        // Activate again
        await grain.ActivateInboxAsync();
        (await grain.GetSnapshotAsync()).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task IsMessageProcessedAsync_ShouldTrackProcessedMessages()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            autoProcess: false));

        var messageId = $"track-test-{Guid.NewGuid():N}@test.local";
        var email = CreateTestEmail(messageId: messageId);

        // Assert - should not be processed initially
        (await grain.IsMessageProcessedAsync(messageId)).Should().BeFalse();

        // Act
        await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert - should be processed now
        (await grain.IsMessageProcessedAsync(messageId)).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnCorrectValue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        // Assert - should not exist initially
        (await grain.ExistsAsync()).Should().BeFalse();

        // Act
        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io"));

        // Assert - should exist after initialization
        (await grain.ExistsAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessEmailAsync_MultipleAttachments_ShouldCreateMultipleDocuments()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            autoProcess: false));

        var attachments = new List<EmailAttachment>
        {
            new EmailAttachment
            {
                Filename = "invoice-page1.pdf",
                ContentType = "application/pdf",
                SizeBytes = 1024,
                Content = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\npage1\n%%EOF")
            },
            new EmailAttachment
            {
                Filename = "invoice-page2.pdf",
                ContentType = "application/pdf",
                SizeBytes = 1024,
                Content = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\npage2\n%%EOF")
            }
        };

        var email = CreateTestEmail(attachments: attachments);

        // Act
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert
        result.Accepted.Should().BeTrue();
        result.DocumentIds.Should().HaveCount(2);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalEmailsReceived.Should().Be(1);
        snapshot.TotalDocumentsCreated.Should().Be(2);
    }

    [Fact]
    public async Task ProcessEmailAsync_ReceiptSubject_ShouldDetectReceiptType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            autoProcess: false));

        var email = CreateTestEmail(subject: "Your Costco Receipt #12345");

        // Act
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert
        result.Accepted.Should().BeTrue();

        // Verify the document was created with Receipt type
        var documentGrain = _fixture.Cluster.GrainFactory.GetGrain<IPurchaseDocumentGrain>(
            GrainKeys.PurchaseDocument(orgId, siteId, result.DocumentIds![0]));
        var docSnapshot = await documentGrain.GetSnapshotAsync();
        docSnapshot.DocumentType.Should().Be(PurchaseDocumentType.Receipt);
    }

    [Fact]
    public async Task ProcessEmailAsync_TrustedDomain_ShouldAccept()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            autoProcess: false));

        // Restrict to specific domain
        await grain.UpdateSettingsAsync(new UpdateInboxSettingsCommand(
            AllowedSenderDomains: new List<string> { "trusted-supplier.com" }));

        var email = CreateTestEmail(from: "invoices@trusted-supplier.com");

        // Act
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert
        result.Accepted.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessEmailAsync_AttachmentTooLarge_ShouldSkipAttachment()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            autoProcess: false));

        // Set a very small max size
        await grain.UpdateSettingsAsync(new UpdateInboxSettingsCommand(
            MaxAttachmentSizeBytes: 100)); // 100 bytes max

        var attachments = new List<EmailAttachment>
        {
            new EmailAttachment
            {
                Filename = "large-file.pdf",
                ContentType = "application/pdf",
                SizeBytes = 1000, // Larger than max
                Content = new byte[1000]
            }
        };

        var email = CreateTestEmail(attachments: attachments);

        // Act
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert - should reject because no valid attachments after filtering
        result.Accepted.Should().BeFalse();
        result.RejectionReason.Should().Be(EmailRejectionReason.NoAttachments);
    }
}
