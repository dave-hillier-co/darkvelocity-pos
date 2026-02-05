namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for offline sync events.
/// </summary>
public interface IOfflineSyncEvent
{
    Guid OperationId { get; }
    DateTime OccurredAt { get; }
}

/// <summary>
/// Event raised when an operation is queued for offline sync.
/// </summary>
[GenerateSerializer]
public record OperationQueued : IOfflineSyncEvent
{
    [Id(0)] public Guid OperationId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid DeviceId { get; init; }
    [Id(3)] public DarkVelocity.Host.Grains.OfflineOperationType OperationType { get; init; }
    [Id(4)] public string EntityType { get; init; } = string.Empty;
    [Id(5)] public Guid EntityId { get; init; }
    [Id(6)] public string PayloadJson { get; init; } = string.Empty;
    [Id(7)] public DateTime ClientTimestamp { get; init; }
    [Id(8)] public long ClientSequence { get; init; }
    [Id(9)] public Guid? UserId { get; init; }
    [Id(10)] public string? IdempotencyKey { get; init; }
    [Id(11)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event raised when an operation starts syncing.
/// </summary>
[GenerateSerializer]
public record OperationSyncStarted : IOfflineSyncEvent
{
    [Id(0)] public Guid OperationId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event raised when an operation is successfully synced.
/// </summary>
[GenerateSerializer]
public record OperationSynced : IOfflineSyncEvent
{
    [Id(0)] public Guid OperationId { get; init; }
    [Id(1)] public string? ServerResponse { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event raised when an operation has a conflict.
/// </summary>
[GenerateSerializer]
public record OperationConflicted : IOfflineSyncEvent
{
    [Id(0)] public Guid OperationId { get; init; }
    [Id(1)] public string ConflictReason { get; init; } = string.Empty;
    [Id(2)] public string? ServerPayloadJson { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event raised when an operation fails to sync.
/// </summary>
[GenerateSerializer]
public record OperationSyncFailed : IOfflineSyncEvent
{
    [Id(0)] public Guid OperationId { get; init; }
    [Id(1)] public string ErrorMessage { get; init; } = string.Empty;
    [Id(2)] public int RetryCount { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event raised when a conflict is resolved.
/// </summary>
[GenerateSerializer]
public record OperationConflictResolved : IOfflineSyncEvent
{
    [Id(0)] public Guid OperationId { get; init; }
    [Id(1)] public DarkVelocity.Host.Grains.ConflictResolutionStrategy Strategy { get; init; }
    [Id(2)] public string? ResolvedPayloadJson { get; init; }
    [Id(3)] public Guid? ResolvedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event raised when synced operations are cleared.
/// </summary>
[GenerateSerializer]
public record QueueCleared : IOfflineSyncEvent
{
    [Id(0)] public Guid OperationId { get; init; }
    [Id(1)] public int ClearedCount { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}
