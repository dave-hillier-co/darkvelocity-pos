using Orleans;

namespace DarkVelocity.Host.Events;

// ============================================================================
// Email Ingestion Events
// ============================================================================

/// <summary>
/// An email was received at a site inbox.
/// </summary>
[GenerateSerializer]
public sealed record EmailReceived : DomainEvent
{
    public override string EventType => "email.received";
    public override string AggregateType => "EmailInbox";
    public override Guid AggregateId => SiteId;

    [Id(100)] public required string MessageId { get; init; }
    [Id(101)] public required string From { get; init; }
    [Id(102)] public string? FromName { get; init; }
    [Id(103)] public required string To { get; init; }
    [Id(104)] public required string Subject { get; init; }
    [Id(105)] public required DateTime SentAt { get; init; }
    [Id(106)] public required int AttachmentCount { get; init; }
    [Id(107)] public required long TotalAttachmentSize { get; init; }
}

/// <summary>
/// An email was processed and documents were created.
/// </summary>
[GenerateSerializer]
public sealed record EmailProcessed : DomainEvent
{
    public override string EventType => "email.processed";
    public override string AggregateType => "EmailInbox";
    public override Guid AggregateId => SiteId;

    [Id(100)] public required string MessageId { get; init; }
    [Id(101)] public required int DocumentsCreated { get; init; }
    [Id(102)] public required IReadOnlyList<Guid> DocumentIds { get; init; }
    [Id(103)] public required int AttachmentsSkipped { get; init; }
    [Id(104)] public string? SkipReason { get; init; }
}

/// <summary>
/// Email processing failed.
/// </summary>
[GenerateSerializer]
public sealed record EmailProcessingFailed : DomainEvent
{
    public override string EventType => "email.processing.failed";
    public override string AggregateType => "EmailInbox";
    public override Guid AggregateId => SiteId;

    [Id(100)] public required string MessageId { get; init; }
    [Id(101)] public required string FailureReason { get; init; }
    [Id(102)] public string? ErrorDetails { get; init; }
}

/// <summary>
/// Email was rejected (spam, invalid sender, etc.).
/// </summary>
[GenerateSerializer]
public sealed record EmailRejected : DomainEvent
{
    public override string EventType => "email.rejected";
    public override string AggregateType => "EmailInbox";
    public override Guid AggregateId => SiteId;

    [Id(100)] public required string MessageId { get; init; }
    [Id(101)] public required string From { get; init; }
    [Id(102)] public required string Subject { get; init; }
    [Id(103)] public required EmailRejectionReason Reason { get; init; }
    [Id(104)] public string? ReasonDetails { get; init; }
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
