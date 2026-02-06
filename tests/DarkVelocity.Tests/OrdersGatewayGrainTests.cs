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

    // Given: An organization without a connected UberEats delivery platform
    // When: The UberEats platform is connected with API credentials and merchant ID
    // Then: The platform is active with correct type, name, and merchant ID recorded
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

    // Given: An organization without a connected DoorDash delivery platform
    // When: The DoorDash platform is connected with API credentials
    // Then: The platform is active with the DoorDash platform type
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

    // Given: An active Deliveroo delivery platform connection
    // When: The platform name and API credentials are updated
    // Then: The platform reflects the updated name
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

    // Given: An active Just Eat delivery platform connection
    // When: The platform is paused
    // Then: The platform status changes to paused, halting order intake
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

    // Given: A paused Wolt delivery platform connection
    // When: The platform is resumed
    // Then: The platform status returns to active, resuming order intake
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

    // Given: An active GrubHub delivery platform connection
    // When: A site location is mapped to a platform store ID
    // Then: The platform tracks the location mapping with the external store identifier
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

    // Given: A custom delivery platform with one mapped location
    // When: The location mapping is removed
    // Then: The platform has no remaining location mappings
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

    // Given: An active UberEats delivery platform connection
    // When: Two delivery orders are recorded with revenue totals
    // Then: Daily order count and revenue metrics are accumulated correctly
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

    // Given: An active DoorDash delivery platform connection
    // When: A menu sync is recorded
    // Then: The last sync timestamp is updated to the current time
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

    // Given: An active Postmates delivery platform connection
    // When: The platform is disconnected
    // Then: The platform status changes to disconnected
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
