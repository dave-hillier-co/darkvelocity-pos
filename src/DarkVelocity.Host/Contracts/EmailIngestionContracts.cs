using DarkVelocity.Host.Events;

namespace DarkVelocity.Host.Contracts;

/// <summary>
/// Request to initialize an email inbox.
/// </summary>
public record InitializeInboxRequest(
    string? InboxAddress = null,
    PurchaseDocumentType? DefaultDocumentType = null,
    bool? AutoProcess = null);

/// <summary>
/// Request to update inbox settings.
/// </summary>
public record UpdateInboxRequest(
    List<string>? AllowedSenderDomains = null,
    List<string>? AllowedSenderEmails = null,
    long? MaxAttachmentSizeBytes = null,
    PurchaseDocumentType? DefaultDocumentType = null,
    bool? AutoProcess = null,
    bool? IsActive = null);

/// <summary>
/// Request to send a test email (for development).
/// </summary>
public record TestEmailRequest(
    string? From = null,
    string? FromName = null,
    string? Subject = null,
    string? Body = null,
    string? AttachmentFilename = null,
    PurchaseDocumentType? DocumentType = null);
