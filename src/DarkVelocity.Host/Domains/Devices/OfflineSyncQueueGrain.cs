using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain for managing offline operations queue for a device.
/// Key: "{orgId}:device:{deviceId}:syncqueue"
/// </summary>
public class OfflineSyncQueueGrain : Grain, IOfflineSyncQueueGrain
{
    private readonly IPersistentState<OfflineSyncQueueState> _state;

    public OfflineSyncQueueGrain(
        [PersistentState("offlineSyncQueue", "OrleansStorage")]
        IPersistentState<OfflineSyncQueueState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(Guid deviceId)
    {
        if (_state.State.Initialized)
            return;

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State.OrganizationId = orgId;
        _state.State.DeviceId = deviceId;
        _state.State.Initialized = true;

        await _state.WriteStateAsync();
    }

    public async Task<OfflineOperationSnapshot> QueueOperationAsync(QueueOfflineOperationCommand command)
    {
        EnsureInitialized();

        // Check for duplicate idempotency key
        if (!string.IsNullOrEmpty(command.IdempotencyKey))
        {
            var existing = _state.State.Operations
                .FirstOrDefault(o => o.IdempotencyKey == command.IdempotencyKey);

            if (existing != null)
            {
                return CreateSnapshot(existing);
            }
        }

        var operationId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var operation = new OfflineSyncOperationState
        {
            OperationId = operationId,
            OrganizationId = _state.State.OrganizationId,
            DeviceId = _state.State.DeviceId,
            OperationType = command.OperationType,
            EntityType = command.EntityType,
            EntityId = command.EntityId,
            PayloadJson = command.PayloadJson,
            ClientTimestamp = command.ClientTimestamp,
            ClientSequence = command.ClientSequence,
            UserId = command.UserId,
            IdempotencyKey = command.IdempotencyKey,
            Status = SyncOperationStatus.Queued,
            QueuedAt = now
        };

        _state.State.Operations.Add(operation);
        _state.State.LastClientSequence = Math.Max(_state.State.LastClientSequence, command.ClientSequence);

        await _state.WriteStateAsync();

        return CreateSnapshot(operation);
    }

    public Task<IReadOnlyList<OfflineOperationSnapshot>> GetQueuedOperationsAsync()
    {
        EnsureInitialized();

        var queued = _state.State.Operations
            .Where(o => o.Status == SyncOperationStatus.Queued)
            .OrderBy(o => o.ClientSequence)
            .ThenBy(o => o.QueuedAt)
            .Select(CreateSnapshot)
            .ToList();

        return Task.FromResult<IReadOnlyList<OfflineOperationSnapshot>>(queued);
    }

    public Task<IReadOnlyList<OfflineOperationSnapshot>> GetConflictedOperationsAsync()
    {
        EnsureInitialized();

        var conflicted = _state.State.Operations
            .Where(o => o.Status == SyncOperationStatus.Conflicted)
            .OrderBy(o => o.ConflictedAt)
            .Select(CreateSnapshot)
            .ToList();

        return Task.FromResult<IReadOnlyList<OfflineOperationSnapshot>>(conflicted);
    }

    public async Task<SyncResult> ProcessQueueAsync()
    {
        EnsureInitialized();

        var queuedOperations = _state.State.Operations
            .Where(o => o.Status == SyncOperationStatus.Queued)
            .OrderBy(o => o.ClientSequence)
            .ThenBy(o => o.QueuedAt)
            .ToList();

        var syncedCount = 0;
        var failedCount = 0;
        var conflictedCount = 0;
        var results = new List<OfflineOperationSnapshot>();

        foreach (var operation in queuedOperations)
        {
            try
            {
                // Mark as syncing
                operation.Status = SyncOperationStatus.Syncing;
                operation.SyncStartedAt = DateTime.UtcNow;

                // Attempt to sync - in real implementation this would call the appropriate grain
                var syncSuccess = await AttemptSyncAsync(operation);

                if (syncSuccess)
                {
                    operation.Status = SyncOperationStatus.Synced;
                    operation.SyncedAt = DateTime.UtcNow;
                    _state.State.LastSyncTime = DateTime.UtcNow;
                    syncedCount++;
                }

                results.Add(CreateSnapshot(operation));
            }
            catch (ConflictException ex)
            {
                operation.Status = SyncOperationStatus.Conflicted;
                operation.ConflictedAt = DateTime.UtcNow;
                operation.ConflictReason = ex.Message;
                operation.ServerPayloadJson = ex.ServerPayload;
                conflictedCount++;
                results.Add(CreateSnapshot(operation));
            }
            catch (Exception ex)
            {
                operation.Status = SyncOperationStatus.Failed;
                operation.FailedAt = DateTime.UtcNow;
                operation.LastError = ex.Message;
                operation.RetryCount++;
                failedCount++;
                results.Add(CreateSnapshot(operation));
            }
        }

        await _state.WriteStateAsync();

        return new SyncResult(
            Success: failedCount == 0 && conflictedCount == 0,
            SyncedCount: syncedCount,
            FailedCount: failedCount,
            ConflictedCount: conflictedCount,
            Results: results);
    }

    public async Task<OfflineOperationSnapshot> SyncOperationAsync(SyncOperationCommand command)
    {
        EnsureInitialized();

        var operation = _state.State.Operations.FirstOrDefault(o => o.OperationId == command.OperationId);
        if (operation == null)
            throw new InvalidOperationException("Operation not found");

        if (operation.Status != SyncOperationStatus.Queued && operation.Status != SyncOperationStatus.Failed)
            throw new InvalidOperationException($"Cannot sync operation in status {operation.Status}");

        operation.Status = SyncOperationStatus.Syncing;
        operation.SyncStartedAt = DateTime.UtcNow;

        await _state.WriteStateAsync();

        return CreateSnapshot(operation);
    }

    public async Task<OfflineOperationSnapshot> MarkSyncedAsync(Guid operationId, string? serverResponse = null)
    {
        EnsureInitialized();

        var operation = _state.State.Operations.FirstOrDefault(o => o.OperationId == operationId);
        if (operation == null)
            throw new InvalidOperationException("Operation not found");

        operation.Status = SyncOperationStatus.Synced;
        operation.SyncedAt = DateTime.UtcNow;
        operation.ServerResponse = serverResponse;
        _state.State.LastSyncTime = DateTime.UtcNow;

        await _state.WriteStateAsync();

        return CreateSnapshot(operation);
    }

    public async Task<OfflineOperationSnapshot> MarkConflictedAsync(Guid operationId, string reason, string? serverPayloadJson = null)
    {
        EnsureInitialized();

        var operation = _state.State.Operations.FirstOrDefault(o => o.OperationId == operationId);
        if (operation == null)
            throw new InvalidOperationException("Operation not found");

        operation.Status = SyncOperationStatus.Conflicted;
        operation.ConflictedAt = DateTime.UtcNow;
        operation.ConflictReason = reason;
        operation.ServerPayloadJson = serverPayloadJson;

        await _state.WriteStateAsync();

        return CreateSnapshot(operation);
    }

    public async Task<OfflineOperationSnapshot> MarkFailedAsync(Guid operationId, string errorMessage)
    {
        EnsureInitialized();

        var operation = _state.State.Operations.FirstOrDefault(o => o.OperationId == operationId);
        if (operation == null)
            throw new InvalidOperationException("Operation not found");

        operation.Status = SyncOperationStatus.Failed;
        operation.FailedAt = DateTime.UtcNow;
        operation.LastError = errorMessage;
        operation.RetryCount++;

        await _state.WriteStateAsync();

        return CreateSnapshot(operation);
    }

    public async Task<OfflineOperationSnapshot> ResolveConflictAsync(ResolveConflictCommand command)
    {
        EnsureInitialized();

        var operation = _state.State.Operations.FirstOrDefault(o => o.OperationId == command.OperationId);
        if (operation == null)
            throw new InvalidOperationException("Operation not found");

        if (operation.Status != SyncOperationStatus.Conflicted)
            throw new InvalidOperationException("Operation is not in conflicted state");

        operation.ResolutionStrategy = command.Strategy;
        operation.ResolvedBy = command.ResolvedBy;
        operation.ResolvedAt = DateTime.UtcNow;

        switch (command.Strategy)
        {
            case ConflictResolutionStrategy.ServerWins:
                // Accept server version - mark as synced
                operation.Status = SyncOperationStatus.Synced;
                operation.SyncedAt = DateTime.UtcNow;
                break;

            case ConflictResolutionStrategy.ClientWins:
                // Use client version - re-queue for sync
                operation.Status = SyncOperationStatus.Queued;
                operation.ConflictReason = null;
                operation.ServerPayloadJson = null;
                break;

            case ConflictResolutionStrategy.Manual:
                // Use manually resolved payload
                if (string.IsNullOrEmpty(command.ResolvedPayloadJson))
                    throw new ArgumentException("Resolved payload required for manual resolution");

                operation.ResolvedPayloadJson = command.ResolvedPayloadJson;
                operation.PayloadJson = command.ResolvedPayloadJson;
                operation.Status = SyncOperationStatus.Queued;
                operation.ConflictReason = null;
                operation.ServerPayloadJson = null;
                break;
        }

        await _state.WriteStateAsync();

        return CreateSnapshot(operation);
    }

    public Task<SyncQueueSummary> GetSummaryAsync()
    {
        EnsureInitialized();

        var operations = _state.State.Operations;

        var queuedCount = operations.Count(o => o.Status == SyncOperationStatus.Queued);
        var syncingCount = operations.Count(o => o.Status == SyncOperationStatus.Syncing);
        var syncedCount = operations.Count(o => o.Status == SyncOperationStatus.Synced);
        var conflictedCount = operations.Count(o => o.Status == SyncOperationStatus.Conflicted);
        var failedCount = operations.Count(o => o.Status == SyncOperationStatus.Failed);

        var pendingOperations = operations
            .Where(o => o.Status == SyncOperationStatus.Queued || o.Status == SyncOperationStatus.Failed)
            .OrderBy(o => o.ClientSequence)
            .Select(CreateSnapshot)
            .ToList();

        var conflictedOperations = operations
            .Where(o => o.Status == SyncOperationStatus.Conflicted)
            .OrderBy(o => o.ConflictedAt)
            .Select(CreateSnapshot)
            .ToList();

        var oldestQueuedAt = operations
            .Where(o => o.Status == SyncOperationStatus.Queued)
            .OrderBy(o => o.QueuedAt)
            .FirstOrDefault()?.QueuedAt;

        return Task.FromResult(new SyncQueueSummary(
            QueuedCount: queuedCount,
            SyncingCount: syncingCount,
            SyncedCount: syncedCount,
            ConflictedCount: conflictedCount,
            FailedCount: failedCount,
            OldestQueuedAt: oldestQueuedAt,
            LastSyncedAt: _state.State.LastSyncTime,
            PendingOperations: pendingOperations,
            ConflictedOperations: conflictedOperations));
    }

    public async Task ClearSyncedOperationsAsync()
    {
        EnsureInitialized();

        var syncedOps = _state.State.Operations
            .Where(o => o.Status == SyncOperationStatus.Synced)
            .ToList();

        foreach (var op in syncedOps)
        {
            _state.State.Operations.Remove(op);
        }

        await _state.WriteStateAsync();
    }

    public Task<bool> HasPendingOperationsAsync()
    {
        EnsureInitialized();

        var hasPending = _state.State.Operations
            .Any(o => o.Status == SyncOperationStatus.Queued ||
                     o.Status == SyncOperationStatus.Syncing ||
                     o.Status == SyncOperationStatus.Failed);

        return Task.FromResult(hasPending);
    }

    public Task<DateTime?> GetLastSyncTimeAsync()
    {
        EnsureInitialized();
        return Task.FromResult(_state.State.LastSyncTime);
    }

    private async Task<bool> AttemptSyncAsync(OfflineSyncOperationState operation)
    {
        // In a real implementation, this would route to the appropriate grain
        // based on operation type and execute the operation.
        // For now, we simulate success.

        // Example of how it might work:
        // switch (operation.OperationType)
        // {
        //     case OfflineOperationType.CreateOrder:
        //         var orderGrain = GrainFactory.GetGrain<IOrderGrain>(...);
        //         await orderGrain.CreateAsync(...);
        //         break;
        //     case OfflineOperationType.ApplyPayment:
        //         var paymentGrain = GrainFactory.GetGrain<IPaymentGrain>(...);
        //         await paymentGrain.ProcessAsync(...);
        //         break;
        // }

        await Task.CompletedTask;
        return true;
    }

    private static OfflineOperationSnapshot CreateSnapshot(OfflineSyncOperationState operation)
    {
        return new OfflineOperationSnapshot(
            OperationId: operation.OperationId,
            DeviceId: operation.DeviceId,
            OperationType: operation.OperationType,
            EntityType: operation.EntityType,
            EntityId: operation.EntityId,
            Status: operation.Status,
            PayloadJson: operation.PayloadJson,
            ClientTimestamp: operation.ClientTimestamp,
            ClientSequence: operation.ClientSequence,
            QueuedAt: operation.QueuedAt,
            SyncedAt: operation.SyncedAt,
            ConflictedAt: operation.ConflictedAt,
            ConflictReason: operation.ConflictReason,
            ServerPayloadJson: operation.ServerPayloadJson,
            RetryCount: operation.RetryCount,
            LastError: operation.LastError,
            IdempotencyKey: operation.IdempotencyKey);
    }

    private void EnsureInitialized()
    {
        if (!_state.State.Initialized)
            throw new InvalidOperationException("Offline sync queue not initialized");
    }
}

/// <summary>
/// Exception thrown when a sync conflict occurs.
/// </summary>
public class ConflictException : Exception
{
    public string? ServerPayload { get; }

    public ConflictException(string message, string? serverPayload = null)
        : base(message)
    {
        ServerPayload = serverPayload;
    }
}
