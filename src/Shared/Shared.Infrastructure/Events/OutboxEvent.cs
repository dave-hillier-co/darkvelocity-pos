using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Shared.Infrastructure.Events;

/// <summary>
/// Entity for storing events in an outbox table for reliable event delivery.
/// Implements the transactional outbox pattern.
/// </summary>
public class OutboxEvent : BaseEntity
{
    /// <summary>
    /// The type of event (e.g., "auth.user.created").
    /// </summary>
    public required string EventType { get; set; }

    /// <summary>
    /// The serialized event payload as JSON.
    /// </summary>
    public required string Payload { get; set; }

    /// <summary>
    /// The CLR type name of the event for deserialization.
    /// </summary>
    public required string EventClrType { get; set; }

    /// <summary>
    /// When the event occurred in the source system.
    /// </summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// When the event was processed/published (null if not yet processed).
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Number of times processing has been attempted.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Error message from the last failed processing attempt.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Correlation ID for tracing related events.
    /// </summary>
    public Guid? CorrelationId { get; set; }

    /// <summary>
    /// The aggregate root ID this event relates to.
    /// </summary>
    public Guid? AggregateId { get; set; }

    /// <summary>
    /// The type of aggregate (e.g., "User", "Employee").
    /// </summary>
    public string? AggregateType { get; set; }
}
