namespace DarkVelocity.Host.Grains;

// ============================================================================
// Offline Sync Types
// ============================================================================

public enum OfflineOperationType
{
    CreateOrder,
    UpdateOrder,
    AddOrderLine,
    ApplyPayment,
    CloseOrder,
    VoidOrder,
    CreateBooking,
    UpdateBooking,
    TimeEntry,
    InventoryAdjustment
}

public enum SyncOperationStatus
{
    Queued,
    Syncing,
    Synced,
    Conflicted,
    Failed
}

public enum ConflictResolutionStrategy
{
    ServerWins,
    ClientWins,
    Manual
}

// ============================================================================
// Offline Sync Commands
// ============================================================================

[GenerateSerializer]
public record QueueOfflineOperationCommand(
    [property: Id(0)] OfflineOperationType OperationType,
    [property: Id(1)] string EntityType,
    [property: Id(2)] Guid EntityId,
    [property: Id(3)] string PayloadJson,
    [property: Id(4)] DateTime ClientTimestamp,
    [property: Id(5)] long ClientSequence,
    [property: Id(6)] Guid? UserId = null,
    [property: Id(7)] string? IdempotencyKey = null);

[GenerateSerializer]
public record SyncOperationCommand(
    [property: Id(0)] Guid OperationId,
    [property: Id(1)] int? ExpectedVersion = null);

[GenerateSerializer]
public record ResolveConflictCommand(
    [property: Id(0)] Guid OperationId,
    [property: Id(1)] ConflictResolutionStrategy Strategy,
    [property: Id(2)] string? ResolvedPayloadJson = null,
    [property: Id(3)] Guid? ResolvedBy = null);

// ============================================================================
// Offline Sync Snapshots
// ============================================================================

[GenerateSerializer]
public record OfflineOperationSnapshot(
    [property: Id(0)] Guid OperationId,
    [property: Id(1)] Guid DeviceId,
    [property: Id(2)] OfflineOperationType OperationType,
    [property: Id(3)] string EntityType,
    [property: Id(4)] Guid EntityId,
    [property: Id(5)] SyncOperationStatus Status,
    [property: Id(6)] string PayloadJson,
    [property: Id(7)] DateTime ClientTimestamp,
    [property: Id(8)] long ClientSequence,
    [property: Id(9)] DateTime QueuedAt,
    [property: Id(10)] DateTime? SyncedAt,
    [property: Id(11)] DateTime? ConflictedAt,
    [property: Id(12)] string? ConflictReason,
    [property: Id(13)] string? ServerPayloadJson,
    [property: Id(14)] int RetryCount,
    [property: Id(15)] string? LastError,
    [property: Id(16)] string? IdempotencyKey);

[GenerateSerializer]
public record SyncQueueSummary(
    [property: Id(0)] int QueuedCount,
    [property: Id(1)] int SyncingCount,
    [property: Id(2)] int SyncedCount,
    [property: Id(3)] int ConflictedCount,
    [property: Id(4)] int FailedCount,
    [property: Id(5)] DateTime? OldestQueuedAt,
    [property: Id(6)] DateTime? LastSyncedAt,
    [property: Id(7)] IReadOnlyList<OfflineOperationSnapshot> PendingOperations,
    [property: Id(8)] IReadOnlyList<OfflineOperationSnapshot> ConflictedOperations);

[GenerateSerializer]
public record SyncResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] int SyncedCount,
    [property: Id(2)] int FailedCount,
    [property: Id(3)] int ConflictedCount,
    [property: Id(4)] IReadOnlyList<OfflineOperationSnapshot> Results);

// ============================================================================
// Offline Sync Queue Grain Interface
// ============================================================================

/// <summary>
/// Grain for managing offline operations queue for a device.
/// Key: "{orgId}:device:{deviceId}:syncqueue"
/// </summary>
public interface IOfflineSyncQueueGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initializes the sync queue for a device.
    /// </summary>
    Task InitializeAsync(Guid deviceId);

    /// <summary>
    /// Queues an offline operation.
    /// </summary>
    Task<OfflineOperationSnapshot> QueueOperationAsync(QueueOfflineOperationCommand command);

    /// <summary>
    /// Gets all queued (pending) operations in order.
    /// </summary>
    Task<IReadOnlyList<OfflineOperationSnapshot>> GetQueuedOperationsAsync();

    /// <summary>
    /// Gets operations that have conflicts.
    /// </summary>
    Task<IReadOnlyList<OfflineOperationSnapshot>> GetConflictedOperationsAsync();

    /// <summary>
    /// Processes the entire queue (syncs all pending operations).
    /// </summary>
    Task<SyncResult> ProcessQueueAsync();

    /// <summary>
    /// Syncs a single operation.
    /// </summary>
    Task<OfflineOperationSnapshot> SyncOperationAsync(SyncOperationCommand command);

    /// <summary>
    /// Marks an operation as synced.
    /// </summary>
    Task<OfflineOperationSnapshot> MarkSyncedAsync(Guid operationId, string? serverResponse = null);

    /// <summary>
    /// Marks an operation as conflicted.
    /// </summary>
    Task<OfflineOperationSnapshot> MarkConflictedAsync(Guid operationId, string reason, string? serverPayloadJson = null);

    /// <summary>
    /// Marks an operation as failed.
    /// </summary>
    Task<OfflineOperationSnapshot> MarkFailedAsync(Guid operationId, string errorMessage);

    /// <summary>
    /// Resolves a conflict.
    /// </summary>
    Task<OfflineOperationSnapshot> ResolveConflictAsync(ResolveConflictCommand command);

    /// <summary>
    /// Gets queue summary.
    /// </summary>
    Task<SyncQueueSummary> GetSummaryAsync();

    /// <summary>
    /// Clears synced operations from history.
    /// </summary>
    Task ClearSyncedOperationsAsync();

    /// <summary>
    /// Checks if the queue has pending operations.
    /// </summary>
    Task<bool> HasPendingOperationsAsync();

    /// <summary>
    /// Gets the last sync timestamp.
    /// </summary>
    Task<DateTime?> GetLastSyncTimeAsync();
}
