using Orleans;

namespace DarkVelocity.Host.Events;

/// <summary>
/// Base class for domain events with enhanced metadata for event sourcing.
/// Supports event versioning, late-arrival handling, and full auditability.
/// </summary>
[GenerateSerializer]
public abstract record DomainEvent : IIntegrationEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    [Id(0)] public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// When the business event occurred (business time).
    /// Used for projections and reporting.
    /// </summary>
    [Id(1)] public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the event was emitted to the event bus (system time).
    /// Used for debugging and late-arrival detection.
    /// </summary>
    [Id(2)] public DateTime EmittedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The source system/service that generated this event.
    /// </summary>
    [Id(3)] public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Schema version for backward compatibility.
    /// </summary>
    [Id(4)] public int Version { get; init; } = 1;

    /// <summary>
    /// Organization ID for multi-tenant partitioning.
    /// </summary>
    [Id(5)] public Guid OrgId { get; init; }

    /// <summary>
    /// Site/Location ID for site-level events.
    /// </summary>
    [Id(6)] public Guid SiteId { get; init; }

    /// <summary>
    /// Correlation ID for tracing related events across services.
    /// </summary>
    [Id(7)] public Guid? CorrelationId { get; init; }

    /// <summary>
    /// Causation ID - the event that caused this event.
    /// </summary>
    [Id(8)] public Guid? CausationId { get; init; }

    /// <summary>
    /// User who triggered this event.
    /// </summary>
    [Id(9)] public Guid? UserId { get; init; }

    /// <summary>
    /// Unique event type identifier (e.g., "inventory.batch.created").
    /// </summary>
    public abstract string EventType { get; }

    /// <summary>
    /// Aggregate ID this event belongs to.
    /// </summary>
    [Id(10)] public virtual Guid AggregateId { get; init; }

    /// <summary>
    /// Aggregate type (e.g., "Inventory", "Order").
    /// </summary>
    public virtual string AggregateType => string.Empty;

    /// <summary>
    /// Sequence number within the aggregate stream.
    /// </summary>
    [Id(11)] public long SequenceNumber { get; init; }
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
