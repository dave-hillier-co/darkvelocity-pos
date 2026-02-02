namespace DarkVelocity.Host.Events;

// ============================================================================
// Email Ingestion Events
// ============================================================================

/// <summary>
/// An email was received at a site inbox.
/// </summary>
public sealed record EmailReceived : DomainEvent
{
    public override string EventType => "email.received";
    public override string AggregateType => "EmailInbox";
    public override Guid AggregateId => SiteId;

    public required string MessageId { get; init; }
    public required string From { get; init; }
    public string? FromName { get; init; }
    public required string To { get; init; }
    public required string Subject { get; init; }
    public required DateTime SentAt { get; init; }
    public required int AttachmentCount { get; init; }
    public required long TotalAttachmentSize { get; init; }
}

/// <summary>
/// An email was processed and documents were created.
/// </summary>
public sealed record EmailProcessed : DomainEvent
{
    public override string EventType => "email.processed";
    public override string AggregateType => "EmailInbox";
    public override Guid AggregateId => SiteId;

    public required string MessageId { get; init; }
    public required int DocumentsCreated { get; init; }
    public required IReadOnlyList<Guid> DocumentIds { get; init; }
    public required int AttachmentsSkipped { get; init; }
    public string? SkipReason { get; init; }
}

/// <summary>
/// Email processing failed.
/// </summary>
public sealed record EmailProcessingFailed : DomainEvent
{
    public override string EventType => "email.processing.failed";
    public override string AggregateType => "EmailInbox";
    public override Guid AggregateId => SiteId;

    public required string MessageId { get; init; }
    public required string FailureReason { get; init; }
    public string? ErrorDetails { get; init; }
}

/// <summary>
/// Email was rejected (spam, invalid sender, etc.).
/// </summary>
public sealed record EmailRejected : DomainEvent
{
    public override string EventType => "email.rejected";
    public override string AggregateType => "EmailInbox";
    public override Guid AggregateId => SiteId;

    public required string MessageId { get; init; }
    public required string From { get; init; }
    public required string Subject { get; init; }
    public required EmailRejectionReason Reason { get; init; }
    public string? ReasonDetails { get; init; }
}

/// <summary>
/// Reasons an email may be rejected.
/// </summary>
public enum EmailRejectionReason
{
    /// <summary>Sender not in allowed list</summary>
    UnauthorizedSender,
    /// <summary>No valid attachments found</summary>
    NoAttachments,
    /// <summary>Attachments too large</summary>
    AttachmentsTooLarge,
    /// <summary>Duplicate email (already processed)</summary>
    Duplicate,
    /// <summary>Invalid inbox address</summary>
    InvalidInbox,
    /// <summary>Site not found or inactive</summary>
    SiteNotFound,
    /// <summary>Email appears to be spam</summary>
    Spam,
    /// <summary>Other rejection reason</summary>
    Other
}
