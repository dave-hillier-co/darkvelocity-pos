using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Offline Payment Queue events.
/// </summary>
public interface IOfflinePaymentQueueEvent
{
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record OfflinePaymentQueued : IOfflinePaymentQueueEvent
{
    [Id(0)] public Guid QueueEntryId { get; init; }
    [Id(1)] public Guid PaymentId { get; init; }
    [Id(2)] public Guid OrganizationId { get; init; }
    [Id(3)] public Guid SiteId { get; init; }
    [Id(4)] public Guid OrderId { get; init; }
    [Id(5)] public PaymentMethod Method { get; init; }
    [Id(6)] public decimal Amount { get; init; }
    [Id(7)] public string PaymentData { get; init; } = ""; // JSON serialized payment details
    [Id(8)] public string QueueReason { get; init; } = "";
    [Id(9)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OfflinePaymentRetryScheduled : IOfflinePaymentQueueEvent
{
    [Id(0)] public Guid QueueEntryId { get; init; }
    [Id(1)] public int AttemptNumber { get; init; }
    [Id(2)] public DateTime ScheduledFor { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OfflinePaymentRetryAttempted : IOfflinePaymentQueueEvent
{
    [Id(0)] public Guid QueueEntryId { get; init; }
    [Id(1)] public int AttemptNumber { get; init; }
    [Id(2)] public bool Success { get; init; }
    [Id(3)] public string? ErrorCode { get; init; }
    [Id(4)] public string? ErrorMessage { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OfflinePaymentProcessed : IOfflinePaymentQueueEvent
{
    [Id(0)] public Guid QueueEntryId { get; init; }
    [Id(1)] public Guid PaymentId { get; init; }
    [Id(2)] public string GatewayReference { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OfflinePaymentFailed : IOfflinePaymentQueueEvent
{
    [Id(0)] public Guid QueueEntryId { get; init; }
    [Id(1)] public Guid PaymentId { get; init; }
    [Id(2)] public string FinalErrorCode { get; init; } = "";
    [Id(3)] public string FinalErrorMessage { get; init; } = "";
    [Id(4)] public int TotalAttempts { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OfflinePaymentCancelled : IOfflinePaymentQueueEvent
{
    [Id(0)] public Guid QueueEntryId { get; init; }
    [Id(1)] public Guid CancelledBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}
