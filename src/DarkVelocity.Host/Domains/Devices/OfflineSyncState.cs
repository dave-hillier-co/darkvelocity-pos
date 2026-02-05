using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

/// <summary>
/// State for an offline sync operation.
/// </summary>
[GenerateSerializer]
public sealed class OfflineSyncOperationState
{
    [Id(0)] public Guid OperationId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid DeviceId { get; set; }
    [Id(3)] public OfflineOperationType OperationType { get; set; }
    [Id(4)] public string EntityType { get; set; } = string.Empty;
    [Id(5)] public Guid EntityId { get; set; }
    [Id(6)] public SyncOperationStatus Status { get; set; } = SyncOperationStatus.Queued;
    [Id(7)] public string PayloadJson { get; set; } = string.Empty;
    [Id(8)] public DateTime ClientTimestamp { get; set; }
    [Id(9)] public long ClientSequence { get; set; }
    [Id(10)] public Guid? UserId { get; set; }
    [Id(11)] public string? IdempotencyKey { get; set; }
    [Id(12)] public DateTime QueuedAt { get; set; }
    [Id(13)] public DateTime? SyncStartedAt { get; set; }
    [Id(14)] public DateTime? SyncedAt { get; set; }
    [Id(15)] public DateTime? ConflictedAt { get; set; }
    [Id(16)] public DateTime? FailedAt { get; set; }
    [Id(17)] public string? ConflictReason { get; set; }
    [Id(18)] public string? ServerPayloadJson { get; set; }
    [Id(19)] public string? ServerResponse { get; set; }
    [Id(20)] public int RetryCount { get; set; }
    [Id(21)] public string? LastError { get; set; }
    [Id(22)] public ConflictResolutionStrategy? ResolutionStrategy { get; set; }
    [Id(23)] public string? ResolvedPayloadJson { get; set; }
    [Id(24)] public Guid? ResolvedBy { get; set; }
    [Id(25)] public DateTime? ResolvedAt { get; set; }
}

/// <summary>
/// State for the offline sync queue grain.
/// </summary>
[GenerateSerializer]
public sealed class OfflineSyncQueueState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public Guid DeviceId { get; set; }
    [Id(2)] public List<OfflineSyncOperationState> Operations { get; set; } = [];
    [Id(3)] public DateTime? LastSyncTime { get; set; }
    [Id(4)] public long LastClientSequence { get; set; }
    [Id(5)] public int MaxHistorySize { get; set; } = 500;
    [Id(6)] public bool Initialized { get; set; }
}
