using DarkVelocity.Host.Events;

namespace DarkVelocity.Host.State;

/// <summary>
/// State for a site's email inbox for receiving invoices/receipts.
/// </summary>
[GenerateSerializer]
public sealed class EmailInboxState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }

    /// <summary>The inbox email address (e.g., invoices-{siteId}@darkvelocity.io)</summary>
    [Id(2)] public string InboxAddress { get; set; } = string.Empty;

    /// <summary>Whether the inbox is active and accepting emails</summary>
    [Id(3)] public bool IsActive { get; set; } = true;

    /// <summary>Allowed sender domains (empty = allow all)</summary>
    [Id(4)] public List<string> AllowedSenderDomains { get; set; } = [];

    /// <summary>Allowed sender email addresses (empty = allow all)</summary>
    [Id(5)] public List<string> AllowedSenderEmails { get; set; } = [];

    /// <summary>Maximum attachment size in bytes (default 25MB)</summary>
    [Id(6)] public long MaxAttachmentSizeBytes { get; set; } = 25 * 1024 * 1024;

    /// <summary>Maximum total email size in bytes (default 50MB)</summary>
    [Id(7)] public long MaxEmailSizeBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>Default document type when auto-detection fails</summary>
    [Id(8)] public PurchaseDocumentType DefaultDocumentType { get; set; } = PurchaseDocumentType.Invoice;

    /// <summary>Whether to auto-process documents after receiving</summary>
    [Id(9)] public bool AutoProcess { get; set; } = true;

    // Statistics
    [Id(10)] public int TotalEmailsReceived { get; set; }
    [Id(11)] public int TotalEmailsProcessed { get; set; }
    [Id(12)] public int TotalEmailsRejected { get; set; }
    [Id(13)] public int TotalDocumentsCreated { get; set; }
    [Id(14)] public DateTime? LastEmailReceivedAt { get; set; }

    /// <summary>Recent processed message IDs for deduplication</summary>
    [Id(15)] public HashSet<string> RecentMessageIds { get; set; } = [];

    [Id(16)] public int Version { get; set; }
}

/// <summary>
/// Record of a processed email for audit trail.
/// </summary>
[GenerateSerializer]
public sealed record ProcessedEmailRecord
{
    [Id(0)] public required string MessageId { get; init; }
    [Id(1)] public required string From { get; init; }
    [Id(2)] public required string Subject { get; init; }
    [Id(3)] public required DateTime ReceivedAt { get; init; }
    [Id(4)] public required DateTime ProcessedAt { get; init; }
    [Id(5)] public required int DocumentsCreated { get; init; }
    [Id(6)] public required IReadOnlyList<Guid> DocumentIds { get; init; }
    [Id(7)] public EmailRejectionReason? RejectionReason { get; init; }
}
