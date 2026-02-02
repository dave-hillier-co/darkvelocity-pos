using DarkVelocity.Host.Events;
using DarkVelocity.Host.Services;
using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Commands
// ============================================================================

/// <summary>
/// Command to initialize an email inbox for a site.
/// </summary>
[GenerateSerializer]
public record InitializeEmailInboxCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string InboxAddress,
    [property: Id(3)] PurchaseDocumentType DefaultDocumentType = PurchaseDocumentType.Invoice,
    [property: Id(4)] bool AutoProcess = true);

/// <summary>
/// Command to process an incoming email.
/// </summary>
[GenerateSerializer]
public record ProcessIncomingEmailCommand(
    [property: Id(0)] ParsedEmail Email,
    [property: Id(1)] PurchaseDocumentType? DocumentTypeHint = null);

/// <summary>
/// Command to update inbox settings.
/// </summary>
[GenerateSerializer]
public record UpdateInboxSettingsCommand(
    [property: Id(0)] List<string>? AllowedSenderDomains = null,
    [property: Id(1)] List<string>? AllowedSenderEmails = null,
    [property: Id(2)] long? MaxAttachmentSizeBytes = null,
    [property: Id(3)] PurchaseDocumentType? DefaultDocumentType = null,
    [property: Id(4)] bool? AutoProcess = null,
    [property: Id(5)] bool? IsActive = null);

// ============================================================================
// Results
// ============================================================================

/// <summary>
/// Snapshot of inbox state.
/// </summary>
[GenerateSerializer]
public record EmailInboxSnapshot(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string InboxAddress,
    [property: Id(3)] bool IsActive,
    [property: Id(4)] PurchaseDocumentType DefaultDocumentType,
    [property: Id(5)] bool AutoProcess,
    [property: Id(6)] int TotalEmailsReceived,
    [property: Id(7)] int TotalDocumentsCreated,
    [property: Id(8)] DateTime? LastEmailReceivedAt);

/// <summary>
/// Result of processing an email.
/// </summary>
[GenerateSerializer]
public record EmailProcessingResultInternal(
    [property: Id(0)] bool Accepted,
    [property: Id(1)] string MessageId,
    [property: Id(2)] IReadOnlyList<Guid>? DocumentIds = null,
    [property: Id(3)] EmailRejectionReason? RejectionReason = null,
    [property: Id(4)] string? RejectionDetails = null);

// ============================================================================
// Grain Interface
// ============================================================================

/// <summary>
/// Grain representing a site's email inbox for receiving invoices/receipts.
/// Key: "{orgId}:{siteId}:email-inbox"
/// </summary>
public interface IEmailInboxGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initialize the inbox for a site.
    /// </summary>
    Task<EmailInboxSnapshot> InitializeAsync(InitializeEmailInboxCommand command);

    /// <summary>
    /// Process an incoming email, creating purchase documents for valid attachments.
    /// </summary>
    Task<EmailProcessingResultInternal> ProcessEmailAsync(ProcessIncomingEmailCommand command);

    /// <summary>
    /// Update inbox settings.
    /// </summary>
    Task<EmailInboxSnapshot> UpdateSettingsAsync(UpdateInboxSettingsCommand command);

    /// <summary>
    /// Activate the inbox.
    /// </summary>
    Task ActivateInboxAsync();

    /// <summary>
    /// Deactivate the inbox (stop accepting emails).
    /// </summary>
    Task DeactivateInboxAsync();

    /// <summary>
    /// Check if a message ID has already been processed (for deduplication).
    /// </summary>
    Task<bool> IsMessageProcessedAsync(string messageId);

    /// <summary>
    /// Get the inbox snapshot.
    /// </summary>
    Task<EmailInboxSnapshot> GetSnapshotAsync();

    /// <summary>
    /// Get the full state.
    /// </summary>
    Task<EmailInboxState> GetStateAsync();

    /// <summary>
    /// Check if inbox exists.
    /// </summary>
    Task<bool> ExistsAsync();
}
