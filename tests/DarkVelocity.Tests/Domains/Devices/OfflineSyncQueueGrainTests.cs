using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests.Domains.Devices;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class OfflineSyncQueueGrainTests
{
    private readonly TestClusterFixture _fixture;

    public OfflineSyncQueueGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IOfflineSyncQueueGrain GetGrain(Guid orgId, Guid deviceId)
    {
        var key = $"{orgId}:device:{deviceId}:syncqueue";
        return _fixture.Cluster.GrainFactory.GetGrain<IOfflineSyncQueueGrain>(key);
    }

    // Given: A new offline sync queue for a device
    // When: The queue is initialized
    // Then: The queue starts with zero queued and zero synced operations
    [Fact]
    public async Task InitializeAsync_ShouldInitializeQueue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        // Act
        await grain.InitializeAsync(deviceId);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.QueuedCount.Should().Be(0);
        summary.SyncedCount.Should().Be(0);
    }

    // Given: An initialized offline sync queue for a POS device
    // When: An order creation operation is queued while the device is offline
    // Then: The operation is stored with queued status and correct metadata
    [Fact]
    public async Task QueueOperationAsync_ShouldQueueOperation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        // Act
        var result = await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OperationType: OfflineOperationType.CreateOrder,
            EntityType: "Order",
            EntityId: orderId,
            PayloadJson: "{\"items\": [{\"name\": \"Burger\", \"quantity\": 1}]}",
            ClientTimestamp: DateTime.UtcNow,
            ClientSequence: 1,
            UserId: Guid.NewGuid(),
            IdempotencyKey: "order-001"));

        // Assert
        result.Status.Should().Be(SyncOperationStatus.Queued);
        result.OperationType.Should().Be(OfflineOperationType.CreateOrder);
        result.EntityType.Should().Be("Order");
        result.EntityId.Should().Be(orderId);
        result.IdempotencyKey.Should().Be("order-001");
    }

    // Given: An operation already queued with a specific idempotency key
    // When: A duplicate operation with the same idempotency key is queued
    // Then: The original operation is returned instead of creating a duplicate
    [Fact]
    public async Task QueueOperationAsync_WithDuplicateIdempotencyKey_ShouldReturnExisting()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        var idempotencyKey = $"test-{Guid.NewGuid()}";
        var firstOperation = await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OperationType: OfflineOperationType.CreateOrder,
            EntityType: "Order",
            EntityId: Guid.NewGuid(),
            PayloadJson: "{}",
            ClientTimestamp: DateTime.UtcNow,
            ClientSequence: 1,
            IdempotencyKey: idempotencyKey));

        // Act
        var secondOperation = await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OperationType: OfflineOperationType.CreateOrder,
            EntityType: "Order",
            EntityId: Guid.NewGuid(),
            PayloadJson: "{\"different\": true}",
            ClientTimestamp: DateTime.UtcNow,
            ClientSequence: 2,
            IdempotencyKey: idempotencyKey));

        // Assert - should return the first operation
        secondOperation.OperationId.Should().Be(firstOperation.OperationId);
    }

    // Given: Three operations queued with out-of-order client sequence numbers
    // When: The queued operations are retrieved
    // Then: The operations are returned ordered by client sequence number
    [Fact]
    public async Task GetQueuedOperationsAsync_ShouldReturnQueuedOperationsInOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, ClientSequence: 3));
        await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, ClientSequence: 1));
        await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, ClientSequence: 2));

        // Act
        var queued = await grain.GetQueuedOperationsAsync();

        // Assert - should be ordered by client sequence
        queued.Should().HaveCount(3);
        queued[0].ClientSequence.Should().Be(1);
        queued[1].ClientSequence.Should().Be(2);
        queued[2].ClientSequence.Should().Be(3);
    }

    // Given: A device with two queued offline operations (order and payment)
    // When: The sync queue is processed upon reconnection
    // Then: All operations are synced successfully with no failures or conflicts
    [Fact]
    public async Task ProcessQueueAsync_ShouldSyncAllOperations()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 1));
        await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.ApplyPayment, "Payment", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 2));

        // Act
        var result = await grain.ProcessQueueAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.SyncedCount.Should().Be(2);
        result.FailedCount.Should().Be(0);
        result.ConflictedCount.Should().Be(0);
    }

    // Given: A queued offline operation
    // When: The operation is marked as synced with a server acknowledgment
    // Then: The operation status transitions to synced with a timestamp
    [Fact]
    public async Task MarkSyncedAsync_ShouldTransitionToSynced()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        var op = await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 1));

        // Act
        var result = await grain.MarkSyncedAsync(op.OperationId, "Server ACK");

        // Assert
        result.Status.Should().Be(SyncOperationStatus.Synced);
        result.SyncedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: A queued offline order update operation
    // When: The sync detects the order was modified by another user on the server
    // Then: The operation is marked as conflicted with the reason and server state
    [Fact]
    public async Task MarkConflictedAsync_ShouldTransitionToConflicted()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        var op = await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.UpdateOrder, "Order", Guid.NewGuid(),
            "{\"status\": \"completed\"}", DateTime.UtcNow, 1));

        // Act
        var result = await grain.MarkConflictedAsync(
            op.OperationId,
            "Order was modified by another user",
            "{\"status\": \"voided\"}");

        // Assert
        result.Status.Should().Be(SyncOperationStatus.Conflicted);
        result.ConflictReason.Should().Be("Order was modified by another user");
        result.ServerPayloadJson.Should().Be("{\"status\": \"voided\"}");
    }

    // Given: A queued offline operation
    // When: The sync fails due to a network timeout
    // Then: The operation is marked as failed with error details and retry count incremented
    [Fact]
    public async Task MarkFailedAsync_ShouldTransitionToFailed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        var op = await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 1));

        // Act
        var result = await grain.MarkFailedAsync(op.OperationId, "Network timeout");

        // Assert
        result.Status.Should().Be(SyncOperationStatus.Failed);
        result.LastError.Should().Be("Network timeout");
        result.RetryCount.Should().Be(1);
    }

    // Given: Two operations where one is conflicted and one is synced
    // When: Conflicted operations are queried
    // Then: Only the conflicted operation is returned
    [Fact]
    public async Task GetConflictedOperationsAsync_ShouldReturnOnlyConflicted()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        var op1 = await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 1));
        var op2 = await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.UpdateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 2));

        await grain.MarkConflictedAsync(op1.OperationId, "Conflict");
        await grain.MarkSyncedAsync(op2.OperationId);

        // Act
        var conflicts = await grain.GetConflictedOperationsAsync();

        // Assert
        conflicts.Should().HaveCount(1);
        conflicts[0].OperationId.Should().Be(op1.OperationId);
    }

    // Given: A conflicted offline operation
    // When: The conflict is resolved with server-wins strategy
    // Then: The operation is marked as synced, accepting the server version
    [Fact]
    public async Task ResolveConflictAsync_WithServerWins_ShouldMarkAsSynced()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        var op = await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.UpdateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 1));
        await grain.MarkConflictedAsync(op.OperationId, "Conflict");

        // Act
        var result = await grain.ResolveConflictAsync(new ResolveConflictCommand(
            OperationId: op.OperationId,
            Strategy: ConflictResolutionStrategy.ServerWins));

        // Assert
        result.Status.Should().Be(SyncOperationStatus.Synced);
    }

    // Given: A conflicted offline operation
    // When: The conflict is resolved with client-wins strategy
    // Then: The operation is requeued for sync with the client's version
    [Fact]
    public async Task ResolveConflictAsync_WithClientWins_ShouldRequeueForSync()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        var op = await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.UpdateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 1));
        await grain.MarkConflictedAsync(op.OperationId, "Conflict");

        // Act
        var result = await grain.ResolveConflictAsync(new ResolveConflictCommand(
            OperationId: op.OperationId,
            Strategy: ConflictResolutionStrategy.ClientWins));

        // Assert
        result.Status.Should().Be(SyncOperationStatus.Queued);
        result.ConflictReason.Should().BeNull();
    }

    // Given: A conflicted operation with differing client and server amounts
    // When: The conflict is resolved manually with a merged payload
    // Then: The operation is requeued with the manually resolved payload
    [Fact]
    public async Task ResolveConflictAsync_WithManual_ShouldUseResolvedPayload()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        var op = await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.UpdateOrder, "Order", Guid.NewGuid(),
            "{\"amount\": 10}", DateTime.UtcNow, 1));
        await grain.MarkConflictedAsync(op.OperationId, "Conflict", "{\"amount\": 20}");

        // Act
        var result = await grain.ResolveConflictAsync(new ResolveConflictCommand(
            OperationId: op.OperationId,
            Strategy: ConflictResolutionStrategy.Manual,
            ResolvedPayloadJson: "{\"amount\": 15}",
            ResolvedBy: Guid.NewGuid()));

        // Assert
        result.Status.Should().Be(SyncOperationStatus.Queued);
        result.PayloadJson.Should().Be("{\"amount\": 15}");
    }

    // Given: An initialized sync queue with no operations
    // When: Pending operations status is checked
    // Then: No pending operations exist
    [Fact]
    public async Task HasPendingOperationsAsync_WhenNoOperations_ShouldReturnFalse()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.InitializeAsync(Guid.NewGuid());

        // Act
        var hasPending = await grain.HasPendingOperationsAsync();

        // Assert
        hasPending.Should().BeFalse();
    }

    // Given: A sync queue with one queued operation
    // When: Pending operations status is checked
    // Then: Pending operations are detected
    [Fact]
    public async Task HasPendingOperationsAsync_WhenHasQueued_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 1));

        // Act
        var hasPending = await grain.HasPendingOperationsAsync();

        // Assert
        hasPending.Should().BeTrue();
    }

    // Given: A sync queue with a synced operation
    // When: Synced operations are cleared from the queue
    // Then: The synced operation count resets to zero
    [Fact]
    public async Task ClearSyncedOperationsAsync_ShouldRemoveSyncedOperations()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        var op = await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 1));
        await grain.MarkSyncedAsync(op.OperationId);

        // Act
        await grain.ClearSyncedOperationsAsync();

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.SyncedCount.Should().Be(0);
    }

    // Given: An initialized sync queue that has never synced
    // When: The last sync time is queried
    // Then: No sync time is available
    [Fact]
    public async Task GetLastSyncTimeAsync_WhenNeverSynced_ShouldReturnNull()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.InitializeAsync(Guid.NewGuid());

        // Act
        var lastSync = await grain.GetLastSyncTimeAsync();

        // Assert
        lastSync.Should().BeNull();
    }

    // Given: A sync queue with a recently synced operation
    // When: The last sync time is queried
    // Then: The timestamp of the most recent sync is returned
    [Fact]
    public async Task GetLastSyncTimeAsync_AfterSync_ShouldReturnTime()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        var op = await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 1));
        await grain.MarkSyncedAsync(op.OperationId);

        // Act
        var lastSync = await grain.GetLastSyncTimeAsync();

        // Assert
        lastSync.Should().NotBeNull();
        lastSync.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: A sync queue with operations in queued, synced, and conflicted states
    // When: The queue summary is requested
    // Then: The summary reports correct counts for each operation status
    [Fact]
    public async Task GetSummaryAsync_ShouldReturnCorrectCounts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        var op1 = await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 1));
        var op2 = await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.UpdateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 2));
        var op3 = await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.ApplyPayment, "Payment", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 3));

        await grain.MarkSyncedAsync(op1.OperationId);
        await grain.MarkConflictedAsync(op2.OperationId, "Conflict");
        // op3 remains queued

        // Act
        var summary = await grain.GetSummaryAsync();

        // Assert
        summary.QueuedCount.Should().Be(1);
        summary.SyncedCount.Should().Be(1);
        summary.ConflictedCount.Should().Be(1);
        summary.PendingOperations.Should().HaveCount(1);
        summary.ConflictedOperations.Should().HaveCount(1);
    }
}
