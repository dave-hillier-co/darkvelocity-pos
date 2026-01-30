namespace DarkVelocity.Shared.Contracts.Events;

/// <summary>
/// Base class for domain events with enhanced metadata for event sourcing.
/// Supports event versioning, late-arrival handling, and full auditability.
/// </summary>
public abstract record DomainEvent : IIntegrationEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// When the business event occurred (business time).
    /// Used for projections and reporting.
    /// </summary>
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the event was emitted to the event bus (system time).
    /// Used for debugging and late-arrival detection.
    /// </summary>
    public DateTime EmittedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The source system/service that generated this event.
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Schema version for backward compatibility.
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Organization ID for multi-tenant partitioning.
    /// </summary>
    public Guid OrgId { get; init; }

    /// <summary>
    /// Site/Location ID for site-level events.
    /// </summary>
    public Guid SiteId { get; init; }

    /// <summary>
    /// Correlation ID for tracing related events across services.
    /// </summary>
    public Guid? CorrelationId { get; init; }

    /// <summary>
    /// Causation ID - the event that caused this event.
    /// </summary>
    public Guid? CausationId { get; init; }

    /// <summary>
    /// User who triggered this event.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Unique event type identifier (e.g., "inventory.batch.created").
    /// </summary>
    public abstract string EventType { get; }

    /// <summary>
    /// Aggregate ID this event belongs to.
    /// </summary>
    public virtual Guid AggregateId { get; init; }

    /// <summary>
    /// Aggregate type (e.g., "Inventory", "Order").
    /// </summary>
    public virtual string AggregateType => string.Empty;

    /// <summary>
    /// Sequence number within the aggregate stream.
    /// </summary>
    public long SequenceNumber { get; init; }
}

/// <summary>
/// Marker interface for idempotent events that can be safely replayed.
/// </summary>
public interface IIdempotentEvent
{
    /// <summary>
    /// Idempotency key for deduplication.
    /// </summary>
    string IdempotencyKey { get; }
}
