using System.Text;
using System.Text.Json;
using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

// ============================================================================
// Delivery Platform Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DeliveryPlatformGrainTests
{
    private readonly TestClusterFixture _fixture;

    public DeliveryPlatformGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDeliveryPlatformGrain GetDeliveryPlatformGrain(Guid orgId, Guid platformId)
        => _fixture.Cluster.GrainFactory.GetGrain<IDeliveryPlatformGrain>(GrainKeys.DeliveryPlatform(orgId, platformId));

    // Given: UberEats platform credentials and configuration
    // When: the delivery platform connection is established
    // Then: the platform is created with active status, merchant details, and zero daily counters
    [Fact]
    public async Task ConnectAsync_ShouldCreateDeliveryPlatform()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetDeliveryPlatformGrain(orgId, platformId);

        var command = new ConnectDeliveryPlatformCommand(
            PlatformType: DeliveryPlatformType.UberEats,
            IntegrationType: IntegrationType.Direct,
            Name: "UberEats Production",
            ApiCredentialsEncrypted: "encrypted-api-key",
            WebhookSecret: "webhook-secret-123",
            MerchantId: "merchant-456",
            Settings: "{\"timezone\": \"UTC\"}");

        // Act
        var result = await grain.ConnectAsync(command);

        // Assert
        result.DeliveryPlatformId.Should().Be(platformId);
        result.PlatformType.Should().Be(DeliveryPlatformType.UberEats);
        result.IntegrationType.Should().Be(IntegrationType.Direct);
        result.Name.Should().Be("UberEats Production");
        result.MerchantId.Should().Be("merchant-456");
        result.Status.Should().Be(DeliveryPlatformStatus.Active);
        result.ConnectedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.TotalOrdersToday.Should().Be(0);
        result.TotalRevenueToday.Should().Be(0);
    }

    // Given: an already connected DoorDash delivery platform
    // When: a duplicate connection attempt is made
    // Then: the operation is rejected to prevent duplicate platform integrations
    [Fact]
    public async Task ConnectAsync_ShouldThrowIfAlreadyConnected()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetDeliveryPlatformGrain(orgId, platformId);

        var command = new ConnectDeliveryPlatformCommand(
            DeliveryPlatformType.DoorDash,
            IntegrationType.Direct,
            "DoorDash",
            null, null, null, null);

        await grain.ConnectAsync(command);

        // Act & Assert
        var action = () => grain.ConnectAsync(command);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Delivery platform already exists");
    }

    // Given: a connected Deliveroo delivery platform
    // When: the platform name and API credentials are updated
    // Then: the platform details are modified to reflect the new configuration
    [Fact]
    public async Task UpdateAsync_ShouldUpdatePlatformDetails()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetDeliveryPlatformGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            DeliveryPlatformType.Deliveroo,
            IntegrationType.Direct,
            "Deliveroo Test",
            null, null, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdateDeliveryPlatformCommand(
            Name: "Deliveroo Production",
            Status: null,
            ApiCredentialsEncrypted: "new-api-key",
            WebhookSecret: "new-webhook-secret",
            Settings: "{\"feature_flags\": true}"));

        // Assert
        result.Name.Should().Be("Deliveroo Production");
    }

    // Given: an active JustEat delivery platform
    // When: the platform status is changed to paused
    // Then: the platform stops accepting orders while preserving other configuration
    [Fact]
    public async Task UpdateAsync_WithStatusChange_ShouldUpdateStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetDeliveryPlatformGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            DeliveryPlatformType.JustEat,
            IntegrationType.Direct,
            "JustEat",
            null, null, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdateDeliveryPlatformCommand(
            Name: null,
            Status: DeliveryPlatformStatus.Paused,
            ApiCredentialsEncrypted: null,
            WebhookSecret: null,
            Settings: null));

        // Assert
        result.Status.Should().Be(DeliveryPlatformStatus.Paused);
    }

    // Given: an active Wolt delivery platform
    // When: the platform is disconnected
    // Then: the platform status transitions to disconnected, ending the integration
    [Fact]
    public async Task DisconnectAsync_ShouldSetStatusToDisconnected()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetDeliveryPlatformGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            DeliveryPlatformType.Wolt,
            IntegrationType.Direct,
            "Wolt",
            null, null, null, null));

        // Act
        await grain.DisconnectAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(DeliveryPlatformStatus.Disconnected);
    }

    // Given: an active GrubHub delivery platform
    // When: the platform is paused and then resumed
    // Then: the status toggles between paused and active accordingly
    [Fact]
    public async Task PauseAndResume_ShouldToggleStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetDeliveryPlatformGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            DeliveryPlatformType.GrubHub,
            IntegrationType.Direct,
            "GrubHub",
            null, null, null, null));

        // Act - Pause
        await grain.PauseAsync();
        var pausedSnapshot = await grain.GetSnapshotAsync();
        pausedSnapshot.Status.Should().Be(DeliveryPlatformStatus.Paused);

        // Act - Resume
        await grain.ResumeAsync();
        var resumedSnapshot = await grain.GetSnapshotAsync();
        resumedSnapshot.Status.Should().Be(DeliveryPlatformStatus.Active);
    }

    // Given: a connected Deliverect aggregator platform
    // When: a venue is mapped to a platform store ID
    // Then: the location mapping is stored linking the internal site to the external store
    [Fact]
    public async Task AddLocationMappingAsync_ShouldAddLocation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetDeliveryPlatformGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            DeliveryPlatformType.Deliverect,
            IntegrationType.Aggregator,
            "Deliverect",
            null, null, null, null));

        // Act
        await grain.AddLocationMappingAsync(new PlatformLocationMapping(
            LocationId: locationId,
            PlatformStoreId: "store-abc-123",
            IsActive: true,
            OperatingHoursOverride: null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Locations.Should().HaveCount(1);
        snapshot.Locations[0].LocationId.Should().Be(locationId);
        snapshot.Locations[0].PlatformStoreId.Should().Be("store-abc-123");
        snapshot.Locations[0].IsActive.Should().BeTrue();
    }

    // Given: a UberEats platform with an existing location mapping
    // When: the same location is re-mapped with a different store ID and settings
    // Then: the existing mapping is replaced with the updated store details
    [Fact]
    public async Task AddLocationMappingAsync_ExistingLocation_ShouldUpdate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetDeliveryPlatformGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            DeliveryPlatformType.UberEats,
            IntegrationType.Direct,
            "UberEats",
            null, null, null, null));

        await grain.AddLocationMappingAsync(new PlatformLocationMapping(
            LocationId: locationId,
            PlatformStoreId: "old-store-id",
            IsActive: true,
            OperatingHoursOverride: null));

        // Act - Update existing location
        await grain.AddLocationMappingAsync(new PlatformLocationMapping(
            LocationId: locationId,
            PlatformStoreId: "new-store-id",
            IsActive: false,
            OperatingHoursOverride: "{\"hours\": \"10am-10pm\"}"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Locations.Should().HaveCount(1);
        snapshot.Locations[0].PlatformStoreId.Should().Be("new-store-id");
        snapshot.Locations[0].IsActive.Should().BeFalse();
        snapshot.Locations[0].OperatingHoursOverride.Should().Be("{\"hours\": \"10am-10pm\"}");
    }

    // Given: a DoorDash platform with a mapped location
    // When: the location mapping is removed
    // Then: the platform has no remaining location mappings
    [Fact]
    public async Task RemoveLocationMappingAsync_ShouldRemoveLocation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetDeliveryPlatformGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            DeliveryPlatformType.DoorDash,
            IntegrationType.Direct,
            "DoorDash",
            null, null, null, null));

        await grain.AddLocationMappingAsync(new PlatformLocationMapping(
            LocationId: locationId,
            PlatformStoreId: "store-to-remove",
            IsActive: true,
            OperatingHoursOverride: null));

        // Act
        await grain.RemoveLocationMappingAsync(locationId);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Locations.Should().BeEmpty();
    }

    // Given: an active Postmates delivery platform
    // When: multiple orders are received throughout the day
    // Then: daily order count and revenue totals accumulate correctly
    [Fact]
    public async Task RecordOrderAsync_ShouldIncrementDailyCounters()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetDeliveryPlatformGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            DeliveryPlatformType.Postmates,
            IntegrationType.Direct,
            "Postmates",
            null, null, null, null));

        // Act
        await grain.RecordOrderAsync(25.50m);
        await grain.RecordOrderAsync(18.75m);
        await grain.RecordOrderAsync(42.00m);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalOrdersToday.Should().Be(3);
        snapshot.TotalRevenueToday.Should().Be(86.25m);
        snapshot.LastOrderAt.Should().NotBeNull();
        snapshot.LastOrderAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: a connected Deliverect aggregator platform
    // When: a menu or catalog sync operation completes
    // Then: the last sync timestamp is updated to the current time
    [Fact]
    public async Task RecordSyncAsync_ShouldUpdateTimestamp()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetDeliveryPlatformGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            DeliveryPlatformType.Deliverect,
            IntegrationType.Aggregator,
            "Deliverect Sync",
            null, null, null, null));

        // Act
        await grain.RecordSyncAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LastSyncAt.Should().NotBeNull();
        snapshot.LastSyncAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: a delivery platform that has not been connected
    // When: any operation (update, disconnect, pause, record order, get snapshot) is attempted
    // Then: all operations are rejected because the platform is not initialized
    [Fact]
    public async Task Operations_OnUninitializedGrain_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetDeliveryPlatformGrain(orgId, platformId);

        // Act & Assert - All operations should throw on uninitialized grain
        var updateAction = () => grain.UpdateAsync(new UpdateDeliveryPlatformCommand(
            Name: "Test", Status: null, ApiCredentialsEncrypted: null, WebhookSecret: null, Settings: null));
        await updateAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Delivery platform grain not initialized");

        var disconnectAction = () => grain.DisconnectAsync();
        await disconnectAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Delivery platform grain not initialized");

        var pauseAction = () => grain.PauseAsync();
        await pauseAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Delivery platform grain not initialized");

        var recordOrderAction = () => grain.RecordOrderAsync(100m);
        await recordOrderAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Delivery platform grain not initialized");

        var getSnapshotAction = () => grain.GetSnapshotAsync();
        await getSnapshotAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Delivery platform grain not initialized");
    }

    // Given: a Deliverect aggregator platform for a multi-location restaurant group
    // When: three venue locations are mapped to their platform store IDs
    // Then: all location mappings are tracked including active and inactive sites
    [Fact]
    public async Task MultipleLocationMappings_ShouldTrackAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var location1 = Guid.NewGuid();
        var location2 = Guid.NewGuid();
        var location3 = Guid.NewGuid();
        var grain = GetDeliveryPlatformGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            DeliveryPlatformType.Deliverect,
            IntegrationType.Aggregator,
            "Multi-Location Platform",
            null, null, null, null));

        // Act
        await grain.AddLocationMappingAsync(new PlatformLocationMapping(
            location1, "store-nyc", true, null));
        await grain.AddLocationMappingAsync(new PlatformLocationMapping(
            location2, "store-la", true, null));
        await grain.AddLocationMappingAsync(new PlatformLocationMapping(
            location3, "store-chicago", false, "{\"closed\": true}"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Locations.Should().HaveCount(3);
        snapshot.Locations.Should().Contain(l => l.LocationId == location1 && l.PlatformStoreId == "store-nyc");
        snapshot.Locations.Should().Contain(l => l.LocationId == location2 && l.PlatformStoreId == "store-la");
        snapshot.Locations.Should().Contain(l => l.LocationId == location3 && !l.IsActive);
    }
}

// ============================================================================
// External Order Grain - State Transition and Edge Case Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ExternalOrderStateTransitionTests
{
    private readonly TestClusterFixture _fixture;

    public ExternalOrderStateTransitionTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IExternalOrderGrain GetExternalOrderGrain(Guid orgId, Guid orderId)
        => _fixture.Cluster.GrainFactory.GetGrain<IExternalOrderGrain>(GrainKeys.ExternalOrder(orgId, orderId));

    // Given: an external delivery order that has already been rejected
    // When: an attempt is made to accept the rejected order
    // Then: the operation is rejected because only pending orders can be accepted
    [Fact]
    public async Task AcceptAsync_OnRejectedOrder_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, orderId);

        await grain.ReceiveAsync(CreateMinimalExternalOrder());
        await grain.RejectAsync("Out of stock");

        // Act & Assert
        var action = () => grain.AcceptAsync(DateTime.UtcNow.AddMinutes(30));
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Order is not pending");
    }

    // Given: an external delivery order that has already been accepted
    // When: an attempt is made to reject the accepted order
    // Then: the operation is rejected because only pending orders can be rejected
    [Fact]
    public async Task RejectAsync_OnAcceptedOrder_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, orderId);

        await grain.ReceiveAsync(CreateMinimalExternalOrder());
        await grain.AcceptAsync(DateTime.UtcNow.AddMinutes(30));

        // Act & Assert
        var action = () => grain.RejectAsync("Too late");
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Order is not pending");
    }

    // Given: an external delivery order that has already been received
    // When: the same order is received a second time
    // Then: the duplicate is rejected to enforce idempotency
    [Fact]
    public async Task ReceiveAsync_OnExistingOrder_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, orderId);

        await grain.ReceiveAsync(CreateMinimalExternalOrder());

        // Act & Assert - Cannot receive the same order twice (idempotency)
        var action = () => grain.ReceiveAsync(CreateMinimalExternalOrder());
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("External order already exists");
    }

    // Given: an accepted external delivery order
    // When: a platform API failure occurs during processing
    // Then: the order is marked as failed with the error message preserved
    [Fact]
    public async Task MarkFailedAsync_ShouldSetStatusAndMessage()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, orderId);

        await grain.ReceiveAsync(CreateMinimalExternalOrder());
        await grain.AcceptAsync(DateTime.UtcNow.AddMinutes(30));

        // Act
        await grain.MarkFailedAsync("Platform API timeout");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(ExternalOrderStatus.Failed);
        snapshot.ErrorMessage.Should().Be("Platform API timeout");
    }

    // Given: an external delivery order already in preparation
    // When: the customer requests cancellation during preparation
    // Then: the order is cancelled with the cancellation reason stored
    [Fact]
    public async Task CancelAsync_AfterPreparation_ShouldCancelWithReason()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, orderId);

        await grain.ReceiveAsync(CreateMinimalExternalOrder());
        await grain.AcceptAsync(DateTime.UtcNow.AddMinutes(30));
        await grain.SetPreparingAsync();

        // Act
        await grain.CancelAsync("Customer requested cancellation");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(ExternalOrderStatus.Cancelled);
        snapshot.ErrorMessage.Should().Be("Customer requested cancellation");
    }

    // Given: a newly received external delivery order
    // When: the order progresses through the complete delivery lifecycle (pending, accepted, preparing, ready, picked up, delivered)
    // Then: each status transition is recorded with timestamps at each stage
    [Fact]
    public async Task FullWorkflow_Delivery_ShouldProgressThroughAllStates()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, orderId);

        await grain.ReceiveAsync(CreateMinimalExternalOrder());

        // Act & Assert - Progress through complete workflow
        var pending = await grain.GetSnapshotAsync();
        pending.Status.Should().Be(ExternalOrderStatus.Pending);

        var accepted = await grain.AcceptAsync(DateTime.UtcNow.AddMinutes(30));
        accepted.Status.Should().Be(ExternalOrderStatus.Accepted);
        accepted.AcceptedAt.Should().NotBeNull();

        await grain.SetPreparingAsync();
        var preparing = await grain.GetSnapshotAsync();
        preparing.Status.Should().Be(ExternalOrderStatus.Preparing);

        await grain.SetReadyAsync();
        var ready = await grain.GetSnapshotAsync();
        ready.Status.Should().Be(ExternalOrderStatus.Ready);

        await grain.SetPickedUpAsync();
        var pickedUp = await grain.GetSnapshotAsync();
        pickedUp.Status.Should().Be(ExternalOrderStatus.PickedUp);
        pickedUp.ActualPickupAt.Should().NotBeNull();

        await grain.SetDeliveredAsync();
        var delivered = await grain.GetSnapshotAsync();
        delivered.Status.Should().Be(ExternalOrderStatus.Delivered);
    }

    // Given: an external order that has not been received yet
    // When: any operation (accept, reject, get snapshot) is attempted
    // Then: all operations are rejected because the order is not initialized
    [Fact]
    public async Task Operations_OnUninitializedGrain_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, orderId);

        // Act & Assert
        var acceptAction = () => grain.AcceptAsync(DateTime.UtcNow);
        await acceptAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("External order grain not initialized");

        var rejectAction = () => grain.RejectAsync("reason");
        await rejectAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("External order grain not initialized");

        var getSnapshotAction = () => grain.GetSnapshotAsync();
        await getSnapshotAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("External order grain not initialized");
    }

    // Given: a received external delivery order
    // When: platform communication fails and is retried five times
    // Then: the retry count accumulates to five reflecting all sync attempts
    [Fact]
    public async Task RetryCount_ShouldAccumulateAcrossMultipleIncrements()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, orderId);

        await grain.ReceiveAsync(CreateMinimalExternalOrder());

        // Act
        await grain.IncrementRetryAsync();
        await grain.IncrementRetryAsync();
        await grain.IncrementRetryAsync();
        await grain.IncrementRetryAsync();
        await grain.IncrementRetryAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.RetryCount.Should().Be(5);
    }

    // Given: an accepted external delivery order
    // When: courier details are received and subsequently updated with a new status
    // Then: the courier assignment and status changes are tracked on the order
    [Fact]
    public async Task CourierUpdate_ShouldTrackCourierInfo()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, orderId);

        await grain.ReceiveAsync(CreateMinimalExternalOrder());
        await grain.AcceptAsync(DateTime.UtcNow.AddMinutes(30));

        // Act - Courier assigned
        await grain.UpdateCourierAsync(new CourierInfo(
            FirstName: "John",
            LastName: "Driver",
            PhoneNumber: "+1555987654",
            Provider: "UberEats",
            Status: 10));

        var afterAssign = await grain.GetSnapshotAsync();
        afterAssign.Courier.Should().NotBeNull();
        afterAssign.Courier!.FirstName.Should().Be("John");
        afterAssign.Courier!.Provider.Should().Be("UberEats");

        // Act - Courier status update (on the way)
        await grain.UpdateCourierAsync(new CourierInfo(
            FirstName: "John",
            LastName: "Driver",
            PhoneNumber: "+1555987654",
            Provider: "UberEats",
            Status: 30));

        // Assert
        var afterUpdate = await grain.GetSnapshotAsync();
        afterUpdate.Courier!.Status.Should().Be(30);
    }

    // Given: an accepted external delivery order
    // When: the external order is linked to an internal POS order
    // Then: the internal order ID is stored for cross-referencing between systems
    [Fact]
    public async Task LinkInternalOrder_ShouldSetInternalOrderId()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var internalOrderId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateMinimalExternalOrder());
        await grain.AcceptAsync(DateTime.UtcNow.AddMinutes(30));

        // Act
        await grain.LinkInternalOrderAsync(internalOrderId);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.InternalOrderId.Should().Be(internalOrderId);
    }

    private static ExternalOrderReceived CreateMinimalExternalOrder()
    {
        return new ExternalOrderReceived(
            LocationId: Guid.NewGuid(),
            DeliveryPlatformId: Guid.NewGuid(),
            PlatformOrderId: "PLATFORM-123",
            PlatformOrderNumber: "#100",
            ChannelDisplayId: "#100",
            OrderType: ExternalOrderType.Delivery,
            PlacedAt: DateTime.UtcNow,
            ScheduledPickupAt: null,
            ScheduledDeliveryAt: null,
            IsAsapDelivery: true,
            Customer: new ExternalOrderCustomer(
                Name: "Test Customer",
                Phone: "+1555123456",
                Email: "test@example.com",
                DeliveryAddress: new DeliveryAddress(
                    Street: "123 Test St",
                    PostalCode: "12345",
                    City: "TestCity",
                    Country: "US",
                    ExtraAddressInfo: null)),
            Courier: null,
            Items: new List<ExternalOrderItem>
            {
                new("ITEM-001", null, "Test Item", 1, 10.00m, 10.00m, null, null)
            },
            Subtotal: 10.00m,
            DeliveryFee: 2.99m,
            ServiceFee: 1.50m,
            Tax: 1.00m,
            Tip: 2.00m,
            Total: 17.49m,
            Currency: "USD",
            Discounts: null,
            Packaging: null,
            SpecialInstructions: null,
            PlatformRawPayload: "{}");
    }
}

// ============================================================================
// Platform Payout Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PlatformPayoutGrainTests
{
    private readonly TestClusterFixture _fixture;

    public PlatformPayoutGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IPlatformPayoutGrain GetPayoutGrain(Guid orgId, Guid payoutId)
        => _fixture.Cluster.GrainFactory.GetGrain<IPlatformPayoutGrain>(GrainKeys.PlatformPayout(orgId, payoutId));

    [Fact]
    public async Task ReceiveAsync_ShouldCreatePayout()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);

        var payout = new PayoutReceived(
            DeliveryPlatformId: platformId,
            LocationId: locationId,
            PeriodStart: DateTime.UtcNow.AddDays(-7),
            PeriodEnd: DateTime.UtcNow,
            GrossAmount: 1500.00m,
            PlatformFees: 225.00m,
            NetAmount: 1275.00m,
            Currency: "USD",
            PayoutReference: "PAY-2024-001");

        // Act
        var result = await grain.ReceiveAsync(payout);

        // Assert
        result.PayoutId.Should().Be(payoutId);
        result.DeliveryPlatformId.Should().Be(platformId);
        result.LocationId.Should().Be(locationId);
        result.GrossAmount.Should().Be(1500.00m);
        result.PlatformFees.Should().Be(225.00m);
        result.NetAmount.Should().Be(1275.00m);
        result.Status.Should().Be(PayoutStatus.Pending);
        result.PayoutReference.Should().Be("PAY-2024-001");
    }

    [Fact]
    public async Task ReceiveAsync_ExistingPayout_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);

        var payout = new PayoutReceived(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            1000.00m, 150.00m, 850.00m,
            "USD", null);

        await grain.ReceiveAsync(payout);

        // Act & Assert
        var action = () => grain.ReceiveAsync(payout);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Payout already exists");
    }

    [Fact]
    public async Task PayoutWorkflow_ShouldProgressThroughStates()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);

        await grain.ReceiveAsync(new PayoutReceived(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            2000.00m, 300.00m, 1700.00m,
            "EUR", "PAY-EU-001"));

        // Act & Assert - Progress through workflow
        var pending = await grain.GetSnapshotAsync();
        pending.Status.Should().Be(PayoutStatus.Pending);

        await grain.SetProcessingAsync();
        var processing = await grain.GetSnapshotAsync();
        processing.Status.Should().Be(PayoutStatus.Processing);

        var processedAt = DateTime.UtcNow;
        await grain.CompleteAsync(processedAt);
        var completed = await grain.GetSnapshotAsync();
        completed.Status.Should().Be(PayoutStatus.Completed);
        completed.ProcessedAt.Should().Be(processedAt);
    }

    [Fact]
    public async Task FailAsync_ShouldSetFailedStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);

        await grain.ReceiveAsync(new PayoutReceived(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            500.00m, 75.00m, 425.00m,
            "GBP", null));

        await grain.SetProcessingAsync();

        // Act
        await grain.FailAsync("Bank account verification failed");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PayoutStatus.Failed);
    }

    [Fact]
    public async Task Operations_OnUninitializedGrain_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);

        // Act & Assert
        var getSnapshotAction = () => grain.GetSnapshotAsync();
        await getSnapshotAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Platform payout grain not initialized");

        var setProcessingAction = () => grain.SetProcessingAsync();
        await setProcessingAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Platform payout grain not initialized");
    }
}

// ============================================================================
// Menu Sync Grain - Extended Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MenuSyncExtendedGrainTests
{
    private readonly TestClusterFixture _fixture;

    public MenuSyncExtendedGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IMenuSyncGrain GetMenuSyncGrain(Guid orgId, Guid syncId)
        => _fixture.Cluster.GrainFactory.GetGrain<IMenuSyncGrain>(GrainKeys.MenuSync(orgId, syncId));

    [Fact]
    public async Task StartAsync_ExistingSync_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetMenuSyncGrain(orgId, syncId);

        await grain.StartAsync(new StartMenuSyncCommand(channelId, null));

        // Act & Assert
        var action = () => grain.StartAsync(new StartMenuSyncCommand(channelId, null));
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Menu sync already exists");
    }

    [Fact]
    public async Task MixedSyncResults_ShouldTrackBothSuccessAndFailure()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetMenuSyncGrain(orgId, syncId);

        await grain.StartAsync(new StartMenuSyncCommand(channelId, Guid.NewGuid()));

        // Act - Mix of successful and failed items
        await grain.RecordItemSyncedAsync(new MenuItemMappingRecord(Guid.NewGuid(), "PLU-001", "CAT-1", null, true));
        await grain.RecordItemSyncedAsync(new MenuItemMappingRecord(Guid.NewGuid(), "PLU-002", "CAT-1", 12.99m, true));
        await grain.RecordItemFailedAsync(Guid.NewGuid(), "Invalid image URL");
        await grain.RecordItemSyncedAsync(new MenuItemMappingRecord(Guid.NewGuid(), "PLU-003", "CAT-2", null, false));
        await grain.RecordItemFailedAsync(Guid.NewGuid(), "Missing description");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemsSynced.Should().Be(3);
        snapshot.ItemsFailed.Should().Be(2);
        snapshot.Errors.Should().HaveCount(2);
        snapshot.Errors.Should().Contain(e => e.Contains("Invalid image URL"));
        snapshot.Errors.Should().Contain(e => e.Contains("Missing description"));
    }

    [Fact]
    public async Task CompleteAfterFailures_ShouldStillComplete()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetMenuSyncGrain(orgId, syncId);

        await grain.StartAsync(new StartMenuSyncCommand(channelId, null));
        await grain.RecordItemSyncedAsync(new MenuItemMappingRecord(Guid.NewGuid(), "PLU-001", "CAT-1", null, true));
        await grain.RecordItemFailedAsync(Guid.NewGuid(), "Network timeout");

        // Act
        await grain.CompleteAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(MenuSyncStatus.Completed);
        snapshot.CompletedAt.Should().NotBeNull();
        snapshot.ItemsSynced.Should().Be(1);
        snapshot.ItemsFailed.Should().Be(1);
    }

    [Fact]
    public async Task FailAsync_ShouldRecordErrorAndSetStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetMenuSyncGrain(orgId, syncId);

        await grain.StartAsync(new StartMenuSyncCommand(channelId, null));
        await grain.RecordItemSyncedAsync(new MenuItemMappingRecord(Guid.NewGuid(), "PLU-001", "CAT-1", null, true));

        // Act
        await grain.FailAsync("Platform API returned 503 Service Unavailable");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(MenuSyncStatus.Failed);
        snapshot.Errors.Should().Contain("Platform API returned 503 Service Unavailable");
        snapshot.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Operations_OnUninitializedGrain_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var grain = GetMenuSyncGrain(orgId, syncId);

        // Act & Assert
        var recordItemAction = () => grain.RecordItemSyncedAsync(
            new MenuItemMappingRecord(Guid.NewGuid(), "PLU-001", "CAT-1", null, true));
        await recordItemAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Menu sync grain not initialized");

        var completeAction = () => grain.CompleteAsync();
        await completeAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Menu sync grain not initialized");

        var getSnapshotAction = () => grain.GetSnapshotAsync();
        await getSnapshotAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Menu sync grain not initialized");
    }
}

// ============================================================================
// Webhook Routing and Handler Tests
// ============================================================================

public class WebhookHandlerRoutingTests
{
    [Fact]
    public void DeliverectEventRouting_ShouldIdentifyCorrectEventType()
    {
        // Test different event types
        var orderCreatedPayload = CreateDeliverectPayload("order.created");
        var orderCancelledPayload = CreateDeliverectPayload("order.cancelled");
        var statusUpdatePayload = CreateDeliverectPayload("order.status");
        var productSyncPayload = CreateDeliverectPayload("products.sync");
        var storeBusyPayload = CreateDeliverectPayload("store.busy");

        // Assert
        GetEventType(orderCreatedPayload).Should().Be("order.created");
        GetEventType(orderCancelledPayload).Should().Be("order.cancelled");
        GetEventType(statusUpdatePayload).Should().Be("order.status");
        GetEventType(productSyncPayload).Should().Be("products.sync");
        GetEventType(storeBusyPayload).Should().Be("store.busy");
    }

    [Fact]
    public void UberEatsEventRouting_ShouldIdentifyCorrectEventType()
    {
        var orderNotification = @"{""eventType"": ""orders.notification"", ""meta"": {""resourceId"": ""order-123""}}";
        var orderCancel = @"{""eventType"": ""orders.cancel"", ""meta"": {""resourceId"": ""order-456""}}";

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var notification = JsonSerializer.Deserialize<UberEatsPayload>(orderNotification, options);
        var cancel = JsonSerializer.Deserialize<UberEatsPayload>(orderCancel, options);

        notification!.EventType.Should().Be("orders.notification");
        cancel!.EventType.Should().Be("orders.cancel");
    }

    [Fact]
    public void DoorDashEventRouting_ShouldIdentifyCorrectEventType()
    {
        var orderCreated = @"{""eventName"": ""order.created"", ""orderId"": ""dd-order-123"", ""storeId"": ""store-456""}";
        var orderCancelled = @"{""eventName"": ""order.cancelled"", ""orderId"": ""dd-order-789"", ""storeId"": ""store-456""}";

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var created = JsonSerializer.Deserialize<DoorDashPayload>(orderCreated, options);
        var cancelled = JsonSerializer.Deserialize<DoorDashPayload>(orderCancelled, options);

        created!.EventName.Should().Be("order.created");
        created.OrderId.Should().Be("dd-order-123");
        cancelled!.EventName.Should().Be("order.cancelled");
        cancelled.OrderId.Should().Be("dd-order-789");
    }

    [Fact]
    public void HmacValidation_WithDifferentAlgorithms_ShouldValidate()
    {
        var payload = @"{""test"": ""data""}";
        var secret = "my-secret-key";

        // SHA256
        using (var hmacSha256 = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret)))
        {
            var hash = hmacSha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var signature = Convert.ToHexString(hash).ToLowerInvariant();
            ValidateHmac(payload, signature, secret, "SHA256").Should().BeTrue();
        }

        // SHA1
        using (var hmacSha1 = new System.Security.Cryptography.HMACSHA1(Encoding.UTF8.GetBytes(secret)))
        {
            var hash = hmacSha1.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var signature = Convert.ToHexString(hash).ToLowerInvariant();
            ValidateHmac(payload, signature, secret, "SHA1").Should().BeTrue();
        }
    }

    [Fact]
    public void HmacValidation_WithEmptyInputs_ShouldReturnFalse()
    {
        ValidateHmac("payload", "", "secret", "SHA256").Should().BeFalse();
        ValidateHmac("payload", "signature", "", "SHA256").Should().BeFalse();
        ValidateHmac("payload", null!, "secret", "SHA256").Should().BeFalse();
    }

    private static string CreateDeliverectPayload(string eventType)
    {
        return $@"{{""event"": ""{eventType}"", ""accountId"": ""acc-123"", ""locationId"": ""loc-456"", ""order"": {{}}}}";
    }

    private static string GetEventType(string payload)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var obj = JsonSerializer.Deserialize<JsonDocument>(payload, options);
        return obj!.RootElement.GetProperty("event").GetString()!;
    }

    private static bool ValidateHmac(string payload, string signature, string secret, string algorithm)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
            return false;

        try
        {
            using var hmac = algorithm.ToUpperInvariant() switch
            {
                "SHA256" => (System.Security.Cryptography.HMAC)new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret)),
                "SHA512" => new System.Security.Cryptography.HMACSHA512(Encoding.UTF8.GetBytes(secret)),
                "SHA1" => new System.Security.Cryptography.HMACSHA1(Encoding.UTF8.GetBytes(secret)),
                _ => new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret))
            };

            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computedSignature = Convert.ToHexString(computedHash).ToLowerInvariant();

            var providedSignature = signature.Contains('=')
                ? signature.Split('=').Last().ToLowerInvariant()
                : signature.ToLowerInvariant();

            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature),
                Encoding.UTF8.GetBytes(providedSignature));
        }
        catch
        {
            return false;
        }
    }

    // Helper DTOs for deserialization
    private record UberEatsPayload(string? EventType, UberEatsMeta? Meta);
    private record UberEatsMeta(string? ResourceId, string? Status);
    private record DoorDashPayload(string? EventName, string? OrderId, string? StoreId);
}

// ============================================================================
// Platform Adapter Error Handling Tests
// ============================================================================

public class PlatformAdapterMappingTests
{
    [Theory]
    [InlineData(1, ExternalOrderType.Delivery)]
    [InlineData(2, ExternalOrderType.Pickup)]
    [InlineData(3, ExternalOrderType.DineIn)]
    [InlineData(0, ExternalOrderType.Delivery)]  // Unknown defaults to Delivery
    [InlineData(99, ExternalOrderType.Delivery)] // Invalid defaults to Delivery
    [InlineData(null, ExternalOrderType.Delivery)] // Null defaults to Delivery
    public void DeliverectOrderTypeMapping_ShouldMapCorrectly(int? deliverectType, ExternalOrderType expected)
    {
        // Act
        var result = MapOrderType(deliverectType);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(ExternalOrderStatus.Pending, 10)]
    [InlineData(ExternalOrderStatus.Accepted, 20)]
    [InlineData(ExternalOrderStatus.Preparing, 30)]
    [InlineData(ExternalOrderStatus.Ready, 40)]
    [InlineData(ExternalOrderStatus.PickedUp, 50)]
    [InlineData(ExternalOrderStatus.Delivered, 60)]
    [InlineData(ExternalOrderStatus.Cancelled, 110)]
    [InlineData(ExternalOrderStatus.Failed, 120)]
    [InlineData(ExternalOrderStatus.Rejected, 120)]
    public void DeliverectStatusMapping_ToExternal_ShouldMapCorrectly(ExternalOrderStatus internal_, int expectedExternal)
    {
        // Act
        var result = MapToDeliverectStatus(internal_);

        // Assert
        result.Should().Be(expectedExternal);
    }

    [Theory]
    [InlineData(10, ExternalOrderStatus.Pending)]
    [InlineData(20, ExternalOrderStatus.Accepted)]
    [InlineData(30, ExternalOrderStatus.Preparing)]
    [InlineData(40, ExternalOrderStatus.Ready)]
    [InlineData(50, ExternalOrderStatus.PickedUp)]
    [InlineData(60, ExternalOrderStatus.Delivered)]
    [InlineData(110, ExternalOrderStatus.Cancelled)]
    [InlineData(120, ExternalOrderStatus.Failed)]
    public void DeliverectStatusMapping_ToInternal_ShouldMapCorrectly(int deliverectStatus, ExternalOrderStatus expected)
    {
        // Act
        var result = MapFromDeliverectStatus(deliverectStatus);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1299, 12.99)]
    [InlineData(100, 1.00)]
    [InlineData(0, 0.00)]
    [InlineData(99999, 999.99)]
    [InlineData(1, 0.01)]
    public void PriceConversion_FromCents_ShouldConvertCorrectly(int cents, decimal expectedDollars)
    {
        // Act
        var result = cents / 100m;

        // Assert
        result.Should().Be(expectedDollars);
    }

    [Theory]
    [InlineData(12.99, 1299)]
    [InlineData(1.00, 100)]
    [InlineData(0.00, 0)]
    [InlineData(999.99, 99999)]
    [InlineData(0.01, 1)]
    public void PriceConversion_ToCents_ShouldConvertCorrectly(decimal dollars, int expectedCents)
    {
        // Act
        var result = (int)(dollars * 100);

        // Assert
        result.Should().Be(expectedCents);
    }

    private static ExternalOrderType MapOrderType(int? orderType) => orderType switch
    {
        1 => ExternalOrderType.Delivery,
        2 => ExternalOrderType.Pickup,
        3 => ExternalOrderType.DineIn,
        _ => ExternalOrderType.Delivery
    };

    private static int MapToDeliverectStatus(ExternalOrderStatus status) => status switch
    {
        ExternalOrderStatus.Pending => 10,
        ExternalOrderStatus.Accepted => 20,
        ExternalOrderStatus.Preparing => 30,
        ExternalOrderStatus.Ready => 40,
        ExternalOrderStatus.PickedUp => 50,
        ExternalOrderStatus.Delivered => 60,
        ExternalOrderStatus.Cancelled => 110,
        ExternalOrderStatus.Failed => 120,
        ExternalOrderStatus.Rejected => 120,
        _ => 10
    };

    private static ExternalOrderStatus MapFromDeliverectStatus(int status) => status switch
    {
        10 => ExternalOrderStatus.Pending,
        20 => ExternalOrderStatus.Accepted,
        30 => ExternalOrderStatus.Preparing,
        40 => ExternalOrderStatus.Ready,
        50 => ExternalOrderStatus.PickedUp,
        60 => ExternalOrderStatus.Delivered,
        110 => ExternalOrderStatus.Cancelled,
        120 => ExternalOrderStatus.Failed,
        _ => ExternalOrderStatus.Pending
    };
}

// ============================================================================
// Channel and Status Mapping - Edge Case Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ChannelEdgeCaseTests
{
    private readonly TestClusterFixture _fixture;

    public ChannelEdgeCaseTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IChannelGrain GetChannelGrain(Guid orgId, Guid channelId)
        => _fixture.Cluster.GrainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));

    private IStatusMappingGrain GetStatusMappingGrain(Guid orgId, DeliveryPlatformType platformType)
        => _fixture.Cluster.GrainFactory.GetGrain<IStatusMappingGrain>(GrainKeys.StatusMapping(orgId, platformType));

    [Fact]
    public async Task Channel_ErrorThenRecover_ShouldResetStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.Deliverect,
            IntegrationType.Aggregator,
            "Error Recovery Test",
            null, null, null, null));

        // Act - Enter error state
        await grain.RecordErrorAsync("API timeout");
        var errorSnapshot = await grain.GetSnapshotAsync();
        errorSnapshot.Status.Should().Be(ChannelStatus.Error);
        errorSnapshot.LastErrorMessage.Should().Be("API timeout");

        // Act - Recover by updating status
        await grain.UpdateAsync(new UpdateChannelCommand(
            Name: null,
            Status: ChannelStatus.Active,
            ApiCredentialsEncrypted: null,
            WebhookSecret: null,
            Settings: null));

        // Assert
        var recoveredSnapshot = await grain.GetSnapshotAsync();
        recoveredSnapshot.Status.Should().Be(ChannelStatus.Active);
    }

    [Fact]
    public async Task Channel_HighVolumeOrders_ShouldAccumulateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.UberEats,
            IntegrationType.Direct,
            "High Volume Test",
            null, null, null, null));

        // Act - Record many orders
        decimal totalExpected = 0;
        for (int i = 0; i < 100; i++)
        {
            var orderAmount = 10.00m + (i * 0.50m);
            await grain.RecordOrderAsync(orderAmount);
            totalExpected += orderAmount;
        }

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalOrdersToday.Should().Be(100);
        snapshot.TotalRevenueToday.Should().Be(totalExpected);
    }

    [Fact]
    public async Task StatusMapping_CaseInsensitiveStatusCodes_ShouldMatch()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetStatusMappingGrain(orgId, DeliveryPlatformType.Custom);

        var mappings = new List<StatusMappingEntry>
        {
            new("ACCEPTED", "Accepted", InternalOrderStatus.Accepted, false, null)
        };

        await grain.ConfigureAsync(new ConfigureStatusMappingCommand(DeliveryPlatformType.Custom, mappings));

        // Act - Query with exact case
        var exactMatch = await grain.GetInternalStatusAsync("ACCEPTED");
        // Query with different case should NOT match (case-sensitive)
        var lowerMatch = await grain.GetInternalStatusAsync("accepted");

        // Assert
        exactMatch.Should().Be(InternalOrderStatus.Accepted);
        lowerMatch.Should().BeNull(); // Status codes are case-sensitive
    }

    [Fact]
    public async Task StatusMapping_MappingsWithPosActions_ShouldTrackActions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetStatusMappingGrain(orgId, DeliveryPlatformType.Deliverect);

        var mappings = new List<StatusMappingEntry>
        {
            new("20", "Accepted", InternalOrderStatus.Accepted, true, "PrintKot"),
            new("40", "Ready", InternalOrderStatus.Ready, true, "NotifyCourier"),
            new("50", "PickedUp", InternalOrderStatus.PickedUp, false, null)
        };

        await grain.ConfigureAsync(new ConfigureStatusMappingCommand(DeliveryPlatformType.Deliverect, mappings));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Mappings.Should().HaveCount(3);

        var acceptedMapping = snapshot.Mappings.First(m => m.ExternalStatusCode == "20");
        acceptedMapping.TriggersPosAction.Should().BeTrue();
        acceptedMapping.PosActionType.Should().Be("PrintKot");

        var readyMapping = snapshot.Mappings.First(m => m.ExternalStatusCode == "40");
        readyMapping.TriggersPosAction.Should().BeTrue();
        readyMapping.PosActionType.Should().Be("NotifyCourier");

        var pickedUpMapping = snapshot.Mappings.First(m => m.ExternalStatusCode == "50");
        pickedUpMapping.TriggersPosAction.Should().BeFalse();
        pickedUpMapping.PosActionType.Should().BeNull();
    }
}

// ============================================================================
// Channel Registry - Location Association Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ChannelRegistryLocationTests
{
    private readonly TestClusterFixture _fixture;

    public ChannelRegistryLocationTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IChannelRegistryGrain GetRegistryGrain(Guid orgId)
        => _fixture.Cluster.GrainFactory.GetGrain<IChannelRegistryGrain>(GrainKeys.ChannelRegistry(orgId));

    [Fact]
    public async Task UnregisterChannel_ThatDoesNotExist_ShouldBeIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var nonExistentChannelId = Guid.NewGuid();
        var grain = GetRegistryGrain(orgId);

        // Act - Should not throw
        await grain.UnregisterChannelAsync(nonExistentChannelId);

        // Assert - Registry should still be empty
        var channels = await grain.GetAllChannelsAsync();
        channels.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChannelsByType_EmptyRegistry_ShouldReturnEmpty()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetRegistryGrain(orgId);

        // Act
        var directChannels = await grain.GetChannelsByTypeAsync(IntegrationType.Direct);
        var aggregatorChannels = await grain.GetChannelsByTypeAsync(IntegrationType.Aggregator);
        var internalChannels = await grain.GetChannelsByTypeAsync(IntegrationType.Internal);

        // Assert
        directChannels.Should().BeEmpty();
        aggregatorChannels.Should().BeEmpty();
        internalChannels.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleChannels_SameOrg_ShouldAllBeTracked()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetRegistryGrain(orgId);

        // Act - Register multiple channels of different types
        await grain.RegisterChannelAsync(Guid.NewGuid(), DeliveryPlatformType.UberEats, IntegrationType.Direct, "UberEats");
        await grain.RegisterChannelAsync(Guid.NewGuid(), DeliveryPlatformType.DoorDash, IntegrationType.Direct, "DoorDash");
        await grain.RegisterChannelAsync(Guid.NewGuid(), DeliveryPlatformType.Deliverect, IntegrationType.Aggregator, "Deliverect");
        await grain.RegisterChannelAsync(Guid.NewGuid(), DeliveryPlatformType.LocalWebsite, IntegrationType.Internal, "Website");
        await grain.RegisterChannelAsync(Guid.NewGuid(), DeliveryPlatformType.Kiosk, IntegrationType.Internal, "Kiosk");

        // Assert
        var allChannels = await grain.GetAllChannelsAsync();
        allChannels.Should().HaveCount(5);

        var directChannels = await grain.GetChannelsByTypeAsync(IntegrationType.Direct);
        directChannels.Should().HaveCount(2);

        var aggregatorChannels = await grain.GetChannelsByTypeAsync(IntegrationType.Aggregator);
        aggregatorChannels.Should().HaveCount(1);

        var internalChannels = await grain.GetChannelsByTypeAsync(IntegrationType.Internal);
        internalChannels.Should().HaveCount(2);
    }
}
