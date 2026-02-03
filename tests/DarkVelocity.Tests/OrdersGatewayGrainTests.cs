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

    private IDeliveryPlatformGrain GetGrain(Guid orgId, Guid platformId)
    {
        var key = $"{orgId}:deliveryplatform:{platformId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IDeliveryPlatformGrain>(key);
    }

    [Fact]
    public async Task ConnectAsync_WithUberEats_CreatesPlatformConnection()
    {
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetGrain(orgId, platformId);

        var command = new ConnectDeliveryPlatformCommand(
            PlatformType: DeliveryPlatformType.UberEats,
            IntegrationType: IntegrationType.Direct,
            Name: "UberEats Main",
            ApiCredentialsEncrypted: "encrypted-api-key",
            WebhookSecret: "webhook-secret",
            MerchantId: "uber-merchant-123",
            Settings: null);

        var snapshot = await grain.ConnectAsync(command);

        snapshot.DeliveryPlatformId.Should().Be(platformId);
        snapshot.PlatformType.Should().Be(DeliveryPlatformType.UberEats);
        snapshot.Name.Should().Be("UberEats Main");
        snapshot.Status.Should().Be(DeliveryPlatformStatus.Active);
        snapshot.MerchantId.Should().Be("uber-merchant-123");
        snapshot.ConnectedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ConnectAsync_WithDoorDash_CreatesPlatformConnection()
    {
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetGrain(orgId, platformId);

        var command = new ConnectDeliveryPlatformCommand(
            PlatformType: DeliveryPlatformType.DoorDash,
            IntegrationType: IntegrationType.Direct,
            Name: "DoorDash Connection",
            ApiCredentialsEncrypted: "doordash-key",
            WebhookSecret: "doordash-secret",
            MerchantId: "dd-merchant-456",
            Settings: null);

        var snapshot = await grain.ConnectAsync(command);

        snapshot.PlatformType.Should().Be(DeliveryPlatformType.DoorDash);
        snapshot.Status.Should().Be(DeliveryPlatformStatus.Active);
    }

    [Fact]
    public async Task UpdateAsync_ChangesStatus_UpdatesPlatform()
    {
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            PlatformType: DeliveryPlatformType.Deliveroo,
            IntegrationType: IntegrationType.Direct,
            Name: "Deliveroo",
            ApiCredentialsEncrypted: "key",
            WebhookSecret: null,
            MerchantId: null,
            Settings: null));

        var updateCommand = new UpdateDeliveryPlatformCommand(
            Name: "Deliveroo Updated",
            Status: null,
            ApiCredentialsEncrypted: "new-key",
            WebhookSecret: "new-secret",
            Settings: null);

        var snapshot = await grain.UpdateAsync(updateCommand);

        snapshot.Name.Should().Be("Deliveroo Updated");
    }

    [Fact]
    public async Task PauseAsync_SetsStatusToPaused()
    {
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            PlatformType: DeliveryPlatformType.JustEat,
            IntegrationType: IntegrationType.Direct,
            Name: "Just Eat",
            ApiCredentialsEncrypted: null,
            WebhookSecret: null,
            MerchantId: null,
            Settings: null));

        await grain.PauseAsync();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(DeliveryPlatformStatus.Paused);
    }

    [Fact]
    public async Task ResumeAsync_AfterPause_SetsStatusToActive()
    {
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            PlatformType: DeliveryPlatformType.Wolt,
            IntegrationType: IntegrationType.Direct,
            Name: "Wolt",
            ApiCredentialsEncrypted: null,
            WebhookSecret: null,
            MerchantId: null,
            Settings: null));

        await grain.PauseAsync();
        await grain.ResumeAsync();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(DeliveryPlatformStatus.Active);
    }

    [Fact]
    public async Task AddLocationMappingAsync_AddsLocationToPlatform()
    {
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            PlatformType: DeliveryPlatformType.GrubHub,
            IntegrationType: IntegrationType.Direct,
            Name: "GrubHub",
            ApiCredentialsEncrypted: null,
            WebhookSecret: null,
            MerchantId: null,
            Settings: null));

        var mapping = new PlatformLocationMapping(
            LocationId: locationId,
            PlatformStoreId: "grubhub-store-123",
            IsActive: true,
            OperatingHoursOverride: null);

        await grain.AddLocationMappingAsync(mapping);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Locations.Should().ContainSingle(l => l.LocationId == locationId);
        snapshot.Locations[0].PlatformStoreId.Should().Be("grubhub-store-123");
    }

    [Fact]
    public async Task RemoveLocationMappingAsync_RemovesLocationFromPlatform()
    {
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            PlatformType: DeliveryPlatformType.Custom,
            IntegrationType: IntegrationType.Direct,
            Name: "Custom Platform",
            ApiCredentialsEncrypted: null,
            WebhookSecret: null,
            MerchantId: null,
            Settings: null));

        await grain.AddLocationMappingAsync(new PlatformLocationMapping(
            LocationId: locationId,
            PlatformStoreId: "store-1",
            IsActive: true,
            OperatingHoursOverride: null));

        await grain.RemoveLocationMappingAsync(locationId);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Locations.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordOrderAsync_IncrementsDailyMetrics()
    {
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            PlatformType: DeliveryPlatformType.UberEats,
            IntegrationType: IntegrationType.Direct,
            Name: "UberEats",
            ApiCredentialsEncrypted: null,
            WebhookSecret: null,
            MerchantId: null,
            Settings: null));

        await grain.RecordOrderAsync(45.50m);
        await grain.RecordOrderAsync(32.00m);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalOrdersToday.Should().Be(2);
        snapshot.TotalRevenueToday.Should().Be(77.50m);
        snapshot.LastOrderAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordSyncAsync_UpdatesLastSyncAt()
    {
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            PlatformType: DeliveryPlatformType.DoorDash,
            IntegrationType: IntegrationType.Direct,
            Name: "DoorDash",
            ApiCredentialsEncrypted: null,
            WebhookSecret: null,
            MerchantId: null,
            Settings: null));

        await grain.RecordSyncAsync();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LastSyncAt.Should().NotBeNull();
        snapshot.LastSyncAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DisconnectAsync_DisconnectsPlatform()
    {
        var orgId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetGrain(orgId, platformId);

        await grain.ConnectAsync(new ConnectDeliveryPlatformCommand(
            PlatformType: DeliveryPlatformType.Postmates,
            IntegrationType: IntegrationType.Direct,
            Name: "Postmates",
            ApiCredentialsEncrypted: null,
            WebhookSecret: null,
            MerchantId: null,
            Settings: null));

        await grain.DisconnectAsync();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(DeliveryPlatformStatus.Disconnected);
    }
}

// ============================================================================
// External Order Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ExternalOrderGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ExternalOrderGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IExternalOrderGrain GetGrain(Guid orgId, Guid externalOrderId)
    {
        var key = $"{orgId}:externalorder:{externalOrderId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IExternalOrderGrain>(key);
    }

    private ExternalOrderReceived CreateTestOrderReceived(Guid locationId, Guid platformId)
    {
        return new ExternalOrderReceived(
            LocationId: locationId,
            DeliveryPlatformId: platformId,
            PlatformOrderId: "uber-order-12345",
            PlatformOrderNumber: "UE-12345",
            ChannelDisplayId: null,
            OrderType: ExternalOrderType.Delivery,
            PlacedAt: DateTime.UtcNow,
            ScheduledPickupAt: null,
            ScheduledDeliveryAt: null,
            IsAsapDelivery: true,
            Customer: new ExternalOrderCustomer(
                Name: "John Doe",
                Phone: "+1234567890",
                Email: "john@example.com",
                DeliveryAddress: new DeliveryAddress("123 Main St", "12345", "City", "US", null)),
            Courier: null,
            Items: new[]
            {
                new ExternalOrderItem(
                    PlatformItemId: "item-1",
                    InternalMenuItemId: Guid.NewGuid(),
                    Name: "Burger",
                    Quantity: 2,
                    UnitPrice: 12.99m,
                    TotalPrice: 25.98m,
                    SpecialInstructions: "No onions",
                    Modifiers: null)
            },
            Subtotal: 25.98m,
            DeliveryFee: 4.99m,
            ServiceFee: 2.50m,
            Tax: 2.60m,
            Tip: 5.00m,
            Total: 41.07m,
            Currency: "USD",
            Discounts: null,
            Packaging: null,
            SpecialInstructions: null,
            PlatformRawPayload: null);
    }

    [Fact]
    public async Task CreateAsync_WithDeliveryOrder_CreatesExternalOrder()
    {
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetGrain(orgId, externalOrderId);

        var order = CreateTestOrderReceived(locationId, platformId);
        var snapshot = await grain.ReceiveAsync(order);

        snapshot.ExternalOrderId.Should().Be(externalOrderId);
        snapshot.LocationId.Should().Be(locationId);
        snapshot.DeliveryPlatformId.Should().Be(platformId);
        snapshot.PlatformOrderNumber.Should().Be("UE-12345");
        snapshot.Status.Should().Be(ExternalOrderStatus.Pending);
        snapshot.OrderType.Should().Be(ExternalOrderType.Delivery);
        snapshot.Customer.Name.Should().Be("John Doe");
        snapshot.Total.Should().Be(41.07m);
    }

    [Fact]
    public async Task CreateAsync_WithPickupOrder_CreatesExternalOrder()
    {
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var grain = GetGrain(orgId, externalOrderId);

        var order = new ExternalOrderReceived(
            LocationId: Guid.NewGuid(),
            DeliveryPlatformId: Guid.NewGuid(),
            PlatformOrderId: "dd-pickup-001",
            PlatformOrderNumber: "DD-001",
            ChannelDisplayId: null,
            OrderType: ExternalOrderType.Pickup,
            PlacedAt: DateTime.UtcNow,
            ScheduledPickupAt: null,
            ScheduledDeliveryAt: null,
            IsAsapDelivery: true,
            Customer: new ExternalOrderCustomer(Name: "Jane Smith", Phone: null, Email: null, DeliveryAddress: null),
            Courier: null,
            Items: Array.Empty<ExternalOrderItem>(),
            Subtotal: 15.00m,
            DeliveryFee: 0m,
            ServiceFee: 1.00m,
            Tax: 1.20m,
            Tip: 0m,
            Total: 17.20m,
            Currency: "USD",
            Discounts: null,
            Packaging: null,
            SpecialInstructions: null,
            PlatformRawPayload: null);

        var snapshot = await grain.ReceiveAsync(order);

        snapshot.OrderType.Should().Be(ExternalOrderType.Pickup);
        snapshot.DeliveryFee.Should().Be(0m);
    }

    [Fact]
    public async Task AcceptAsync_SetsStatusToAccepted()
    {
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var grain = GetGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestOrderReceived(Guid.NewGuid(), Guid.NewGuid()));

        var estimatedPickup = DateTime.UtcNow.AddMinutes(30);
        var snapshot = await grain.AcceptAsync(estimatedPickup);

        snapshot.Status.Should().Be(ExternalOrderStatus.Accepted);
        snapshot.AcceptedAt.Should().NotBeNull();
        snapshot.EstimatedPickupAt.Should().Be(estimatedPickup);
    }

    [Fact]
    public async Task RejectAsync_SetsStatusToRejected()
    {
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var grain = GetGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestOrderReceived(Guid.NewGuid(), Guid.NewGuid()));

        var snapshot = await grain.RejectAsync("Store too busy");

        snapshot.Status.Should().Be(ExternalOrderStatus.Rejected);
        snapshot.ErrorMessage.Should().Be("Store too busy");
    }

    [Fact]
    public async Task SetPreparingAsync_SetsStatusToPreparing()
    {
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var grain = GetGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestOrderReceived(Guid.NewGuid(), Guid.NewGuid()));
        await grain.AcceptAsync(null);

        await grain.SetPreparingAsync();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(ExternalOrderStatus.Preparing);
    }

    [Fact]
    public async Task SetReadyAsync_SetsStatusToReady()
    {
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var grain = GetGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestOrderReceived(Guid.NewGuid(), Guid.NewGuid()));
        await grain.AcceptAsync(null);
        await grain.SetPreparingAsync();

        await grain.SetReadyAsync();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(ExternalOrderStatus.Ready);
    }

    [Fact]
    public async Task SetPickedUpAsync_SetsStatusToPickedUp()
    {
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var grain = GetGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestOrderReceived(Guid.NewGuid(), Guid.NewGuid()));
        await grain.AcceptAsync(null);
        await grain.SetPreparingAsync();
        await grain.SetReadyAsync();

        await grain.SetPickedUpAsync();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(ExternalOrderStatus.PickedUp);
        snapshot.ActualPickupAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SetDeliveredAsync_SetsStatusToDelivered()
    {
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var grain = GetGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestOrderReceived(Guid.NewGuid(), Guid.NewGuid()));
        await grain.AcceptAsync(null);
        await grain.SetPickedUpAsync();

        await grain.SetDeliveredAsync();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(ExternalOrderStatus.Delivered);
    }

    [Fact]
    public async Task CancelAsync_SetsStatusToCancelled()
    {
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var grain = GetGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestOrderReceived(Guid.NewGuid(), Guid.NewGuid()));

        await grain.CancelAsync("Customer requested cancellation");

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(ExternalOrderStatus.Cancelled);
        snapshot.ErrorMessage.Should().Be("Customer requested cancellation");
    }

    [Fact]
    public async Task LinkInternalOrderAsync_LinksToInternalOrder()
    {
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var internalOrderId = Guid.NewGuid();
        var grain = GetGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestOrderReceived(Guid.NewGuid(), Guid.NewGuid()));

        await grain.LinkInternalOrderAsync(internalOrderId);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.InternalOrderId.Should().Be(internalOrderId);
    }

    [Fact]
    public async Task MarkFailedAsync_SetsStatusToFailed()
    {
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var grain = GetGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestOrderReceived(Guid.NewGuid(), Guid.NewGuid()));

        await grain.MarkFailedAsync("API timeout");

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(ExternalOrderStatus.Failed);
        snapshot.ErrorMessage.Should().Be("API timeout");
    }

    [Fact]
    public async Task IncrementRetryAsync_IncrementsRetryCount()
    {
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var grain = GetGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestOrderReceived(Guid.NewGuid(), Guid.NewGuid()));

        await grain.IncrementRetryAsync();
        await grain.IncrementRetryAsync();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.RetryCount.Should().Be(2);
    }
}

// ============================================================================
// Menu Sync Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MenuSyncGrainTests
{
    private readonly TestClusterFixture _fixture;

    public MenuSyncGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IMenuSyncGrain GetGrain(Guid orgId, Guid syncId)
    {
        var key = $"{orgId}:menusync:{syncId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IMenuSyncGrain>(key);
    }

    [Fact]
    public async Task StartAsync_CreatesMenuSync()
    {
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var grain = GetGrain(orgId, syncId);

        var command = new StartMenuSyncCommand(
            DeliveryPlatformId: platformId,
            LocationId: null);

        var snapshot = await grain.StartAsync(command);

        snapshot.MenuSyncId.Should().Be(syncId);
        snapshot.DeliveryPlatformId.Should().Be(platformId);
        snapshot.Status.Should().Be(MenuSyncStatus.InProgress);
        snapshot.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StartAsync_WithLocation_CreatesScopedSync()
    {
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, syncId);

        var command = new StartMenuSyncCommand(
            DeliveryPlatformId: platformId,
            LocationId: locationId);

        var snapshot = await grain.StartAsync(command);

        snapshot.LocationId.Should().Be(locationId);
    }

    [Fact]
    public async Task RecordItemSyncedAsync_IncrementsItemsSynced()
    {
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var grain = GetGrain(orgId, syncId);

        await grain.StartAsync(new StartMenuSyncCommand(Guid.NewGuid(), null));

        var mapping1 = new MenuItemMappingRecord(
            InternalMenuItemId: Guid.NewGuid(),
            PlatformItemId: "platform-item-1",
            PlatformCategoryId: "cat-1",
            PriceOverride: null,
            IsAvailable: true);

        var mapping2 = new MenuItemMappingRecord(
            InternalMenuItemId: Guid.NewGuid(),
            PlatformItemId: "platform-item-2",
            PlatformCategoryId: "cat-1",
            PriceOverride: 15.99m,
            IsAvailable: true);

        await grain.RecordItemSyncedAsync(mapping1);
        await grain.RecordItemSyncedAsync(mapping2);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemsSynced.Should().Be(2);
    }

    [Fact]
    public async Task RecordItemFailedAsync_IncrementsItemsFailed()
    {
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var grain = GetGrain(orgId, syncId);

        await grain.StartAsync(new StartMenuSyncCommand(Guid.NewGuid(), null));

        var menuItemId = Guid.NewGuid();
        await grain.RecordItemFailedAsync(menuItemId, "Invalid price format");

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemsFailed.Should().Be(1);
        snapshot.Errors.Should().ContainMatch("*Invalid price format*");
    }

    [Fact]
    public async Task CompleteAsync_SetsStatusToCompleted()
    {
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var grain = GetGrain(orgId, syncId);

        await grain.StartAsync(new StartMenuSyncCommand(Guid.NewGuid(), null));
        await grain.RecordItemSyncedAsync(new MenuItemMappingRecord(
            Guid.NewGuid(), "item-1", null, null, true));

        await grain.CompleteAsync();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(MenuSyncStatus.Completed);
        snapshot.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task FailAsync_SetsStatusToFailed()
    {
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var grain = GetGrain(orgId, syncId);

        await grain.StartAsync(new StartMenuSyncCommand(Guid.NewGuid(), null));

        await grain.FailAsync("Platform API unavailable");

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(MenuSyncStatus.Failed);
        snapshot.Errors.Should().ContainMatch("*Platform API unavailable*");
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

    private IPlatformPayoutGrain GetGrain(Guid orgId, Guid payoutId)
    {
        var key = $"{orgId}:payout:{payoutId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IPlatformPayoutGrain>(key);
    }

    [Fact]
    public async Task ReceiveAsync_CreatesPayoutRecord()
    {
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, payoutId);

        var payout = new PayoutReceived(
            DeliveryPlatformId: platformId,
            LocationId: locationId,
            PeriodStart: DateTime.UtcNow.AddDays(-7),
            PeriodEnd: DateTime.UtcNow,
            GrossAmount: 5000.00m,
            PlatformFees: 750.00m,
            NetAmount: 4250.00m,
            Currency: "USD",
            PayoutReference: "PAYOUT-2024-001");

        var snapshot = await grain.ReceiveAsync(payout);

        snapshot.PayoutId.Should().Be(payoutId);
        snapshot.DeliveryPlatformId.Should().Be(platformId);
        snapshot.GrossAmount.Should().Be(5000.00m);
        snapshot.PlatformFees.Should().Be(750.00m);
        snapshot.NetAmount.Should().Be(4250.00m);
        snapshot.Status.Should().Be(PayoutStatus.Pending);
    }

    [Fact]
    public async Task SetProcessingAsync_SetsStatusToProcessing()
    {
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetGrain(orgId, payoutId);

        await grain.ReceiveAsync(new PayoutReceived(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            1000m, 150m, 850m, "USD", null));

        await grain.SetProcessingAsync();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PayoutStatus.Processing);
    }

    [Fact]
    public async Task CompleteAsync_SetsStatusToCompleted()
    {
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetGrain(orgId, payoutId);

        await grain.ReceiveAsync(new PayoutReceived(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            2000m, 300m, 1700m, "EUR", "REF-123"));

        await grain.SetProcessingAsync();
        var processedAt = DateTime.UtcNow;
        await grain.CompleteAsync(processedAt);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PayoutStatus.Completed);
        snapshot.ProcessedAt.Should().Be(processedAt);
    }

    [Fact]
    public async Task FailAsync_SetsStatusToFailed()
    {
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetGrain(orgId, payoutId);

        await grain.ReceiveAsync(new PayoutReceived(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            500m, 75m, 425m, "GBP", null));

        await grain.SetProcessingAsync();
        await grain.FailAsync("Bank account verification failed");

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PayoutStatus.Failed);
    }
}
