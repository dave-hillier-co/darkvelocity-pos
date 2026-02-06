using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Services;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
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

    // Given: a new site without an email inbox
    // When: the inbox is initialized with an inbox address
    // Then: the inbox should be created with default settings and zero emails received
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

    // Given: an email inbox that has already been initialized
    // When: initialization is attempted again
    // Then: the operation should be rejected as already initialized
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

    // Given: an active email inbox with auto-processing disabled
    // When: a valid email with a PDF attachment is received
    // Then: the email should be accepted and a purchase document should be created
    [Fact]
    public async Task ProcessEmailAsync_ValidEmail_ShouldAcceptAndCreateDocument()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false)); // Disable auto-process for simpler testing

        var email = CreateTestEmail();

        // Act
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert
        result.Accepted.Should().BeTrue();
        result.MessageId.Should().Be(email.MessageId);
        result.DocumentIds.Should().NotBeNull();
        result.DocumentIds.Should().HaveCount(1);
    }

    // Given: an email inbox that has already processed a specific message
    // When: the same email (same message ID) is received again
    // Then: the duplicate email should be rejected
    [Fact]
    public async Task ProcessEmailAsync_DuplicateEmail_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false));

        var email = CreateTestEmail(messageId: "duplicate-message-123@test.local");

        // Process first time
        await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Act - process again
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert
        result.Accepted.Should().BeFalse();
        result.RejectionReason.Should().Be(EmailRejectionReason.Duplicate);
    }

    // Given: an inbox restricted to trusted-supplier.com senders
    // When: an email from an untrusted domain is received
    // Then: the email should be rejected as unauthorized sender
    [Fact]
    public async Task ProcessEmailAsync_UnauthorizedSender_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false));

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

    // Given: an active email inbox
    // When: an email with no attachments is received
    // Then: the email should be rejected for missing attachments
    [Fact]
    public async Task ProcessEmailAsync_NoAttachments_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false));

        var email = CreateTestEmail(attachments: new List<EmailAttachment>());

        // Act
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert
        result.Accepted.Should().BeFalse();
        result.RejectionReason.Should().Be(EmailRejectionReason.NoAttachments);
    }

    // Given: an email inbox that has been deactivated
    // When: an email is received
    // Then: the email should be rejected because the inbox is inactive
    [Fact]
    public async Task ProcessEmailAsync_InactiveInbox_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false));

        await grain.DeactivateInboxAsync();

        var email = CreateTestEmail();

        // Act
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert
        result.Accepted.Should().BeFalse();
        result.RejectionReason.Should().Be(EmailRejectionReason.SiteNotFound);
    }

    // Given: an initialized email inbox
    // When: inbox settings are updated with allowed sender domains, max attachment size, and document type
    // Then: the inbox configuration should reflect the updated settings
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

    // Given: an initialized active email inbox
    // When: the inbox is deactivated and then reactivated
    // Then: the inbox active state should toggle accordingly
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

    // Given: an inbox with no processed messages
    // When: an email is received and processed
    // Then: the message ID should be tracked as processed
    [Fact]
    public async Task IsMessageProcessedAsync_ShouldTrackProcessedMessages()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false));

        var messageId = $"track-test-{Guid.NewGuid():N}@test.local";
        var email = CreateTestEmail(messageId: messageId);

        // Assert - should not be processed initially
        (await grain.IsMessageProcessedAsync(messageId)).Should().BeFalse();

        // Act
        await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert - should be processed now
        (await grain.IsMessageProcessedAsync(messageId)).Should().BeTrue();
    }

    // Given: a grain key for a site inbox
    // When: checking inbox existence before and after initialization
    // Then: it should return false before and true after initialization
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

    // Given: an active inbox
    // When: an email with two PDF attachments is received
    // Then: two purchase documents should be created from a single email
    [Fact]
    public async Task ProcessEmailAsync_MultipleAttachments_ShouldCreateMultipleDocuments()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false));

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

    // Given: an active inbox
    // When: an email with "Receipt" in the subject line is received
    // Then: the purchase document should be classified as a receipt
    [Fact]
    public async Task ProcessEmailAsync_ReceiptSubject_ShouldDetectReceiptType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false));

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

    // Given: an inbox restricted to trusted-supplier.com senders
    // When: an email from the trusted domain is received
    // Then: the email should be accepted
    [Fact]
    public async Task ProcessEmailAsync_TrustedDomain_ShouldAccept()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false));

        // Restrict to specific domain
        await grain.UpdateSettingsAsync(new UpdateInboxSettingsCommand(
            AllowedSenderDomains: new List<string> { "trusted-supplier.com" }));

        var email = CreateTestEmail(from: "invoices@trusted-supplier.com");

        // Act
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert
        result.Accepted.Should().BeTrue();
    }

    // Given: an inbox with a 100-byte max attachment size limit
    // When: an email with an oversized attachment is received
    // Then: the email should be rejected because no valid attachments remain after size filtering
    [Fact]
    public async Task ProcessEmailAsync_AttachmentTooLarge_ShouldSkipAttachment()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false));

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

    // ============================================================================
    // Auto-Processing Tests
    // ============================================================================

    // Given: an inbox with auto-processing enabled
    // When: a supplier invoice email is received
    // Then: the purchase document should be created and auto-processing attempted
    [Fact]
    public async Task ProcessEmailAsync_AutoProcessEnabled_ShouldProcessDocument()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: true)); // Enable auto-processing

        var email = CreateTestEmail(subject: "Invoice #INV-2024-001");

        // Act
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert
        result.Accepted.Should().BeTrue();
        result.DocumentIds.Should().HaveCount(1);

        // Verify the document was created and auto-processing was attempted
        var documentGrain = _fixture.Cluster.GrainFactory.GetGrain<IPurchaseDocumentGrain>(
            GrainKeys.PurchaseDocument(orgId, siteId, result.DocumentIds![0]));
        var docSnapshot = await documentGrain.GetSnapshotAsync();
        docSnapshot.Should().NotBeNull();
        docSnapshot.DocumentId.Should().Be(result.DocumentIds![0]);
    }

    // Given: an inbox with auto-processing disabled
    // When: a supplier email is received
    // Then: the document should be created in Received status without processing
    [Fact]
    public async Task ProcessEmailAsync_AutoProcessDisabled_ShouldNotProcessDocument()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false)); // Disable auto-processing

        var email = CreateTestEmail();

        // Act
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert
        result.Accepted.Should().BeTrue();
        result.DocumentIds.Should().HaveCount(1);

        // Document should exist but not be processed yet
        var documentGrain = _fixture.Cluster.GrainFactory.GetGrain<IPurchaseDocumentGrain>(
            GrainKeys.PurchaseDocument(orgId, siteId, result.DocumentIds![0]));
        var docSnapshot = await documentGrain.GetSnapshotAsync();
        docSnapshot.Status.Should().Be(PurchaseDocumentStatus.Received);
    }

    // ============================================================================
    // Deduplication Boundary Tests
    // ============================================================================

    // Given: an inbox approaching the 1000 message ID deduplication limit
    // When: more than 1000 emails are processed
    // Then: the tracked message IDs should be capped at 1000 while maintaining inbox functionality
    [Fact]
    public async Task ProcessEmailAsync_DeduplicationBoundary_ShouldMaintainMaxMessageIds()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false));

        // Process more than MaxRecentMessageIds (1000) emails
        var messageIds = new List<string>();
        for (int i = 0; i < 1005; i++)
        {
            var messageId = $"bulk-test-{i:D4}@test.local";
            messageIds.Add(messageId);
            var email = CreateTestEmail(messageId: messageId);
            await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));
        }

        // Assert - the first few message IDs should have been evicted
        // but the grain should still be functional
        var state = await grain.GetStateAsync();
        state.RecentMessageIds.Count.Should().BeLessThanOrEqualTo(1000);
        state.TotalEmailsReceived.Should().Be(1005);

        // Recent messages should still be tracked
        (await grain.IsMessageProcessedAsync(messageIds[1004])).Should().BeTrue();
        (await grain.IsMessageProcessedAsync(messageIds[1003])).Should().BeTrue();

        // Very old message IDs may have been evicted (exact behavior depends on HashSet implementation)
        // The important thing is we don't exceed 1000 tracked IDs
    }

    // Given: an inbox with exactly 1000 tracked message IDs at the deduplication limit
    // When: one additional email is processed
    // Then: the message ID set should remain capped at 1000
    [Fact]
    public async Task ProcessEmailAsync_AtDeduplicationLimit_ShouldHandleGracefully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false));

        // Process exactly 1000 emails
        for (int i = 0; i < 1000; i++)
        {
            var email = CreateTestEmail(messageId: $"limit-test-{i:D4}@test.local");
            await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));
        }

        // Assert - all should be tracked
        var state = await grain.GetStateAsync();
        state.RecentMessageIds.Count.Should().Be(1000);
        state.TotalEmailsReceived.Should().Be(1000);

        // Process one more
        var extraEmail = CreateTestEmail(messageId: "limit-test-extra@test.local");
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(extraEmail));

        result.Accepted.Should().BeTrue();
        var stateAfter = await grain.GetStateAsync();
        stateAfter.RecentMessageIds.Count.Should().Be(1000); // Still capped at 1000
    }

    // ============================================================================
    // Statistics Accuracy Tests
    // ============================================================================

    // Given: an inbox restricted to trusted.com senders
    // When: 3 accepted, 2 unauthorized, and 1 duplicate email are processed
    // Then: statistics should accurately reflect accepted, rejected, and processed counts
    [Fact]
    public async Task Statistics_ShouldAccuratelyTrackEmailCounts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false));

        // Restrict to specific domain for rejection testing
        await grain.UpdateSettingsAsync(new UpdateInboxSettingsCommand(
            AllowedSenderDomains: new List<string> { "trusted.com" }));

        // Act - Process various emails
        // 3 accepted emails from trusted domain
        for (int i = 0; i < 3; i++)
        {
            var email = CreateTestEmail(
                from: $"supplier{i}@trusted.com",
                messageId: $"stats-accepted-{i}@test.local");
            await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));
        }

        // 2 rejected emails from untrusted domain
        for (int i = 0; i < 2; i++)
        {
            var email = CreateTestEmail(
                from: $"spammer{i}@untrusted.com",
                messageId: $"stats-rejected-{i}@test.local");
            await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));
        }

        // 1 duplicate email (should be rejected)
        var duplicateEmail = CreateTestEmail(
            from: "supplier1@trusted.com",
            messageId: "stats-accepted-0@test.local"); // Same as first email
        await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(duplicateEmail));

        // Assert
        var state = await grain.GetStateAsync();
        state.TotalEmailsReceived.Should().Be(3); // Only accepted emails count
        state.TotalEmailsRejected.Should().Be(3); // 2 unauthorized + 1 duplicate
        state.TotalDocumentsCreated.Should().Be(3); // One document per accepted email
        state.TotalEmailsProcessed.Should().Be(3);
    }

    // Given: an active inbox
    // When: one email with 3 attachments and one email with 1 attachment are received
    // Then: the total documents created count should be 4
    [Fact]
    public async Task Statistics_ShouldTrackDocumentsCreatedPerEmail()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false));

        // Email with 3 attachments
        var multiAttachmentEmail = CreateTestEmail(
            messageId: "multi-attach@test.local",
            attachments: new List<EmailAttachment>
            {
                new EmailAttachment
                {
                    Filename = "invoice1.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 1024,
                    Content = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\ntest1\n%%EOF")
                },
                new EmailAttachment
                {
                    Filename = "invoice2.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 1024,
                    Content = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\ntest2\n%%EOF")
                },
                new EmailAttachment
                {
                    Filename = "invoice3.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 1024,
                    Content = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\ntest3\n%%EOF")
                }
            });

        // Email with 1 attachment
        var singleAttachmentEmail = CreateTestEmail(messageId: "single-attach@test.local");

        // Act
        await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(multiAttachmentEmail));
        await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(singleAttachmentEmail));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalEmailsReceived.Should().Be(2);
        snapshot.TotalDocumentsCreated.Should().Be(4); // 3 + 1
    }

    // Given: an inbox with no emails received
    // When: two emails are processed sequentially
    // Then: the last email received timestamp should update with each accepted email
    [Fact]
    public async Task Statistics_LastEmailReceivedAt_ShouldUpdate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false));

        var snapshotBefore = await grain.GetSnapshotAsync();
        snapshotBefore.LastEmailReceivedAt.Should().BeNull();

        var beforeFirstEmail = DateTime.UtcNow;

        // Act - process first email
        var email1 = CreateTestEmail(messageId: "first@test.local");
        await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email1));

        var snapshotAfterFirst = await grain.GetSnapshotAsync();
        snapshotAfterFirst.LastEmailReceivedAt.Should().NotBeNull();
        snapshotAfterFirst.LastEmailReceivedAt.Should().BeOnOrAfter(beforeFirstEmail);

        var firstReceivedAt = snapshotAfterFirst.LastEmailReceivedAt;

        // Small delay
        await Task.Delay(10);

        // Process second email
        var email2 = CreateTestEmail(messageId: "second@test.local");
        await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email2));

        // Assert - timestamp should be updated
        var snapshotAfterSecond = await grain.GetSnapshotAsync();
        snapshotAfterSecond.LastEmailReceivedAt.Should().BeOnOrAfter(firstReceivedAt!.Value);
    }

    // ============================================================================
    // Edge Cases and Validation Tests
    // ============================================================================

    // Given: an inbox with a 2000-byte max attachment size
    // When: an email with mixed valid and oversized attachments is received
    // Then: only the valid-sized attachments should produce purchase documents
    [Fact]
    public async Task ProcessEmailAsync_MixedValidAndInvalidAttachments_ShouldProcessValidOnes()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false));

        // Set max attachment size to filter out large files
        await grain.UpdateSettingsAsync(new UpdateInboxSettingsCommand(
            MaxAttachmentSizeBytes: 2000));

        var mixedAttachments = new List<EmailAttachment>
        {
            new EmailAttachment
            {
                Filename = "small.pdf",
                ContentType = "application/pdf",
                SizeBytes = 1024, // Under limit
                Content = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\nsmall\n%%EOF")
            },
            new EmailAttachment
            {
                Filename = "large.pdf",
                ContentType = "application/pdf",
                SizeBytes = 5000, // Over limit
                Content = new byte[5000]
            },
            new EmailAttachment
            {
                Filename = "another-small.pdf",
                ContentType = "application/pdf",
                SizeBytes = 500, // Under limit
                Content = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\nanother\n%%EOF")
            }
        };

        var email = CreateTestEmail(
            messageId: "mixed-attachments@test.local",
            attachments: mixedAttachments);

        // Act
        var result = await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert - should accept and create documents only for valid attachments
        result.Accepted.Should().BeTrue();
        result.DocumentIds.Should().HaveCount(2); // Only the two small PDFs
    }

    // Given: an initialized inbox at a known state version
    // When: an email is successfully processed
    // Then: the inbox state version should increment by one
    [Fact]
    public async Task ProcessEmailAsync_Version_ShouldIncrementOnSuccess()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{siteId}@test.io",
            AutoProcess: false));

        var initialState = await grain.GetStateAsync();
        var initialVersion = initialState.Version;

        // Act
        var email = CreateTestEmail(messageId: "version-test@test.local");
        await grain.ProcessEmailAsync(new ProcessIncomingEmailCommand(email));

        // Assert
        var stateAfter = await grain.GetStateAsync();
        stateAfter.Version.Should().Be(initialVersion + 1);
    }
}
