using DarkVelocity.Host.Events;
using DarkVelocity.Host.Services;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain representing a site's email inbox for receiving invoices/receipts.
/// </summary>
public class EmailInboxGrain : Grain, IEmailInboxGrain
{
    private readonly IPersistentState<EmailInboxState> _state;
    private readonly IGrainFactory _grainFactory;
    private readonly IDocumentIntelligenceService _documentService;
    private readonly ILogger<EmailInboxGrain> _logger;
    private IAsyncStream<IStreamEvent>? _emailStream;

    // Maximum message IDs to keep for deduplication
    private const int MaxRecentMessageIds = 1000;

    public EmailInboxGrain(
        [PersistentState("email-inbox", "OrleansStorage")]
        IPersistentState<EmailInboxState> state,
        IGrainFactory grainFactory,
        IDocumentIntelligenceService documentService,
        ILogger<EmailInboxGrain> logger)
    {
        _state = state;
        _grainFactory = grainFactory;
        _documentService = documentService;
        _logger = logger;
    }

    private IAsyncStream<IStreamEvent> GetEmailStream()
    {
        if (_emailStream == null && _state.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create("email-inbox-events", _state.State.OrganizationId.ToString());
            _emailStream = streamProvider.GetStream<IStreamEvent>(streamId);
        }
        return _emailStream!;
    }

    public async Task<EmailInboxSnapshot> InitializeAsync(InitializeEmailInboxCommand command)
    {
        if (_state.State.SiteId != Guid.Empty)
            throw new InvalidOperationException("Inbox already initialized");

        _state.State = new EmailInboxState
        {
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            InboxAddress = command.InboxAddress,
            DefaultDocumentType = command.DefaultDocumentType,
            AutoProcess = command.AutoProcess,
            IsActive = true,
            Version = 1
        };

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Email inbox initialized for site {SiteId}: {InboxAddress}",
            command.SiteId,
            command.InboxAddress);

        return ToSnapshot();
    }

    public async Task<EmailProcessingResultInternal> ProcessEmailAsync(ProcessIncomingEmailCommand command)
    {
        EnsureExists();

        var email = command.Email;

        // Check if inbox is active
        if (!_state.State.IsActive)
        {
            return RejectEmail(email.MessageId, EmailRejectionReason.SiteNotFound, "Inbox is inactive");
        }

        // Check for duplicate
        if (_state.State.RecentMessageIds.Contains(email.MessageId))
        {
            _logger.LogWarning("Duplicate email detected: {MessageId}", email.MessageId);
            return RejectEmail(email.MessageId, EmailRejectionReason.Duplicate, "Email already processed");
        }

        // Validate sender
        var senderValidation = ValidateSender(email.From);
        if (!senderValidation.isValid)
        {
            return RejectEmail(email.MessageId, EmailRejectionReason.UnauthorizedSender, senderValidation.reason);
        }

        // Check attachments
        var documentAttachments = email.Attachments
            .Where(a => a.IsDocument)
            .Where(a => a.SizeBytes <= _state.State.MaxAttachmentSizeBytes)
            .ToList();

        if (documentAttachments.Count == 0)
        {
            return RejectEmail(email.MessageId, EmailRejectionReason.NoAttachments,
                email.Attachments.Count > 0 ? "No valid document attachments" : "No attachments");
        }

        // Update stats
        _state.State.TotalEmailsReceived++;
        _state.State.LastEmailReceivedAt = DateTime.UtcNow;

        // Track message ID for deduplication
        _state.State.RecentMessageIds.Add(email.MessageId);
        if (_state.State.RecentMessageIds.Count > MaxRecentMessageIds)
        {
            // Remove oldest (HashSet doesn't maintain order, but good enough for dedup)
            var oldest = _state.State.RecentMessageIds.First();
            _state.State.RecentMessageIds.Remove(oldest);
        }

        // Process each attachment
        var createdDocumentIds = new List<Guid>();
        var skippedCount = 0;

        foreach (var attachment in documentAttachments)
        {
            try
            {
                var documentId = await CreateDocumentFromAttachmentAsync(
                    email, attachment, command.DocumentTypeHint);
                createdDocumentIds.Add(documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create document from attachment: {Filename}", attachment.Filename);
                skippedCount++;
            }
        }

        _state.State.TotalEmailsProcessed++;
        _state.State.TotalDocumentsCreated += createdDocumentIds.Count;
        _state.State.Version++;

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Processed email {MessageId}: created {Count} documents, skipped {Skipped}",
            email.MessageId,
            createdDocumentIds.Count,
            skippedCount);

        return new EmailProcessingResultInternal(
            Accepted: true,
            MessageId: email.MessageId,
            DocumentIds: createdDocumentIds);
    }

    private async Task<Guid> CreateDocumentFromAttachmentAsync(
        ParsedEmail email,
        EmailAttachment attachment,
        PurchaseDocumentType? typeHint)
    {
        var documentId = Guid.NewGuid();
        var documentType = typeHint ?? DetectDocumentType(email, attachment);

        // Store attachment to blob storage
        // TODO: In production, upload to S3/Azure Blob and get URL
        var storageUrl = $"/storage/email-attachments/{_state.State.OrganizationId}/{_state.State.SiteId}/{documentId}/{attachment.Filename}";

        // Create the purchase document grain
        var documentGrain = _grainFactory.GetGrain<IPurchaseDocumentGrain>(
            GrainKeys.PurchaseDocument(_state.State.OrganizationId, _state.State.SiteId, documentId));

        await documentGrain.ReceiveAsync(new ReceivePurchaseDocumentCommand(
            _state.State.OrganizationId,
            _state.State.SiteId,
            documentId,
            documentType,
            DocumentSource.Email,
            storageUrl,
            attachment.Filename,
            attachment.ContentType,
            attachment.SizeBytes,
            email.From,
            email.Subject));

        // Auto-process if enabled
        if (_state.State.AutoProcess)
        {
            try
            {
                await documentGrain.RequestProcessingAsync();

                using var stream = new MemoryStream(attachment.Content);
                var extractionResult = await _documentService.ExtractAsync(
                    stream, attachment.ContentType, documentType);

                await documentGrain.ApplyExtractionResultAsync(new ApplyExtractionResultCommand(
                    extractionResult.ToExtractedDocumentData(),
                    extractionResult.OverallConfidence,
                    "email-auto-process"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto-processing failed for document {DocumentId}", documentId);
                await documentGrain.MarkExtractionFailedAsync(new MarkExtractionFailedCommand(
                    "Auto-processing failed", ex.Message));
            }
        }

        return documentId;
    }

    private PurchaseDocumentType DetectDocumentType(ParsedEmail email, EmailAttachment attachment)
    {
        // Simple heuristics for document type detection
        var subjectLower = email.Subject.ToLowerInvariant();
        var filenameLower = attachment.Filename.ToLowerInvariant();

        // Check for receipt indicators
        if (subjectLower.Contains("receipt") ||
            filenameLower.Contains("receipt") ||
            subjectLower.Contains("purchase confirmation") ||
            subjectLower.Contains("order confirmation"))
        {
            return PurchaseDocumentType.Receipt;
        }

        // Check for invoice indicators
        if (subjectLower.Contains("invoice") ||
            filenameLower.Contains("invoice") ||
            subjectLower.Contains("bill") ||
            subjectLower.Contains("statement"))
        {
            return PurchaseDocumentType.Invoice;
        }

        // Default to configured type
        return _state.State.DefaultDocumentType;
    }

    private (bool isValid, string? reason) ValidateSender(string senderEmail)
    {
        // If no restrictions configured, allow all
        if (_state.State.AllowedSenderEmails.Count == 0 &&
            _state.State.AllowedSenderDomains.Count == 0)
        {
            return (true, null);
        }

        // Check specific email addresses
        if (_state.State.AllowedSenderEmails.Contains(senderEmail, StringComparer.OrdinalIgnoreCase))
        {
            return (true, null);
        }

        // Check domains
        var atIndex = senderEmail.IndexOf('@');
        if (atIndex > 0)
        {
            var domain = senderEmail[(atIndex + 1)..].ToLowerInvariant();
            if (_state.State.AllowedSenderDomains.Any(d => d.Equals(domain, StringComparison.OrdinalIgnoreCase)))
            {
                return (true, null);
            }
        }

        return (false, $"Sender {senderEmail} not in allowed list");
    }

    private EmailProcessingResultInternal RejectEmail(string messageId, EmailRejectionReason reason, string? details)
    {
        _state.State.TotalEmailsRejected++;

        _logger.LogWarning(
            "Email rejected: {MessageId}, reason: {Reason}, details: {Details}",
            messageId, reason, details);

        return new EmailProcessingResultInternal(
            Accepted: false,
            MessageId: messageId,
            RejectionReason: reason,
            RejectionDetails: details);
    }

    public async Task<EmailInboxSnapshot> UpdateSettingsAsync(UpdateInboxSettingsCommand command)
    {
        EnsureExists();

        if (command.AllowedSenderDomains != null)
            _state.State.AllowedSenderDomains = command.AllowedSenderDomains;
        if (command.AllowedSenderEmails != null)
            _state.State.AllowedSenderEmails = command.AllowedSenderEmails;
        if (command.MaxAttachmentSizeBytes.HasValue)
            _state.State.MaxAttachmentSizeBytes = command.MaxAttachmentSizeBytes.Value;
        if (command.DefaultDocumentType.HasValue)
            _state.State.DefaultDocumentType = command.DefaultDocumentType.Value;
        if (command.AutoProcess.HasValue)
            _state.State.AutoProcess = command.AutoProcess.Value;
        if (command.IsActive.HasValue)
            _state.State.IsActive = command.IsActive.Value;

        _state.State.Version++;
        await _state.WriteStateAsync();

        return ToSnapshot();
    }

    public async Task ActivateInboxAsync()
    {
        EnsureExists();
        _state.State.IsActive = true;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateInboxAsync()
    {
        EnsureExists();
        _state.State.IsActive = false;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<bool> IsMessageProcessedAsync(string messageId)
    {
        return Task.FromResult(_state.State.RecentMessageIds.Contains(messageId));
    }

    public Task<EmailInboxSnapshot> GetSnapshotAsync()
    {
        return Task.FromResult(ToSnapshot());
    }

    public Task<EmailInboxState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.SiteId != Guid.Empty);
    }

    private void EnsureExists()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Inbox not initialized");
    }

    private EmailInboxSnapshot ToSnapshot()
    {
        return new EmailInboxSnapshot(
            _state.State.OrganizationId,
            _state.State.SiteId,
            _state.State.InboxAddress,
            _state.State.IsActive,
            _state.State.DefaultDocumentType,
            _state.State.AutoProcess,
            _state.State.TotalEmailsReceived,
            _state.State.TotalDocumentsCreated,
            _state.State.LastEmailReceivedAt);
    }
}
