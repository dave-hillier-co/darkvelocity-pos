using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
public class StatusMappingGrainTests
{
    private readonly TestClusterFixture _fixture;

    public StatusMappingGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IStatusMappingGrain GetStatusMappingGrain(Guid orgId, DeliveryPlatformType platformType)
        => _fixture.Cluster.GrainFactory.GetGrain<IStatusMappingGrain>(GrainKeys.StatusMapping(orgId, platformType));

    [Fact]
    public async Task ConfigureAsync_ShouldCreateMappings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetStatusMappingGrain(orgId, DeliveryPlatformType.Deliverect);

        var mappings = new List<StatusMappingEntry>
        {
            new("10", "Received", InternalOrderStatus.Received, false, null),
            new("20", "Accepted", InternalOrderStatus.Accepted, true, "PrintKot"),
            new("50", "Preparing", InternalOrderStatus.Preparing, false, null),
            new("60", "Ready", InternalOrderStatus.Ready, true, "NotifyCourier")
        };

        var command = new ConfigureStatusMappingCommand(DeliveryPlatformType.Deliverect, mappings);

        // Act
        var result = await grain.ConfigureAsync(command);

        // Assert
        result.PlatformType.Should().Be(DeliveryPlatformType.Deliverect);
        result.Mappings.Should().HaveCount(4);
        result.ConfiguredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetInternalStatusAsync_ShouldReturnMappedStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetStatusMappingGrain(orgId, DeliveryPlatformType.UberEats);

        var mappings = new List<StatusMappingEntry>
        {
            new("accepted", "Accepted", InternalOrderStatus.Accepted, true, "PrintKot"),
            new("picked_up", "Picked Up", InternalOrderStatus.PickedUp, false, null)
        };

        await grain.ConfigureAsync(new ConfigureStatusMappingCommand(DeliveryPlatformType.UberEats, mappings));

        // Act
        var result = await grain.GetInternalStatusAsync("accepted");

        // Assert
        result.Should().Be(InternalOrderStatus.Accepted);
    }

    [Fact]
    public async Task GetInternalStatusAsync_ShouldReturnNullForUnknownStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetStatusMappingGrain(orgId, DeliveryPlatformType.Deliveroo);

        var mappings = new List<StatusMappingEntry>
        {
            new("ACCEPTED", "Accepted", InternalOrderStatus.Accepted, false, null)
        };

        await grain.ConfigureAsync(new ConfigureStatusMappingCommand(DeliveryPlatformType.Deliveroo, mappings));

        // Act
        var result = await grain.GetInternalStatusAsync("UNKNOWN_STATUS");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetExternalStatusAsync_ShouldReturnExternalCode()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetStatusMappingGrain(orgId, DeliveryPlatformType.JustEat);

        var mappings = new List<StatusMappingEntry>
        {
            new("CONFIRMED", "Confirmed", InternalOrderStatus.Accepted, true, "PrintKot"),
            new("READY_FOR_PICKUP", "Ready", InternalOrderStatus.Ready, false, null)
        };

        await grain.ConfigureAsync(new ConfigureStatusMappingCommand(DeliveryPlatformType.JustEat, mappings));

        // Act
        var result = await grain.GetExternalStatusAsync(InternalOrderStatus.Ready);

        // Assert
        result.Should().Be("READY_FOR_PICKUP");
    }

    [Fact]
    public async Task AddMappingAsync_ShouldAddNewMapping()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetStatusMappingGrain(orgId, DeliveryPlatformType.DoorDash);

        var initialMappings = new List<StatusMappingEntry>
        {
            new("confirmed", "Confirmed", InternalOrderStatus.Accepted, false, null)
        };

        await grain.ConfigureAsync(new ConfigureStatusMappingCommand(DeliveryPlatformType.DoorDash, initialMappings));

        // Act
        await grain.AddMappingAsync(new StatusMappingEntry("ready", "Ready", InternalOrderStatus.Ready, true, "NotifyCourier"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Mappings.Should().HaveCount(2);
        snapshot.Mappings.Should().Contain(m => m.ExternalStatusCode == "ready");
    }

    [Fact]
    public async Task AddMappingAsync_ShouldUpdateExistingMapping()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetStatusMappingGrain(orgId, DeliveryPlatformType.Wolt);

        var initialMappings = new List<StatusMappingEntry>
        {
            new("accepted", "Accepted", InternalOrderStatus.Accepted, false, null)
        };

        await grain.ConfigureAsync(new ConfigureStatusMappingCommand(DeliveryPlatformType.Wolt, initialMappings));

        // Act - update the same status code with different mapping
        await grain.AddMappingAsync(new StatusMappingEntry("accepted", "Accepted", InternalOrderStatus.Accepted, true, "PrintKot"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Mappings.Should().HaveCount(1);
        var mapping = snapshot.Mappings.First(m => m.ExternalStatusCode == "accepted");
        mapping.TriggersPosAction.Should().BeTrue();
        mapping.PosActionType.Should().Be("PrintKot");
    }

    [Fact]
    public async Task RemoveMappingAsync_ShouldRemoveMapping()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetStatusMappingGrain(orgId, DeliveryPlatformType.GrubHub);

        var initialMappings = new List<StatusMappingEntry>
        {
            new("confirmed", "Confirmed", InternalOrderStatus.Accepted, false, null),
            new("ready", "Ready", InternalOrderStatus.Ready, false, null)
        };

        await grain.ConfigureAsync(new ConfigureStatusMappingCommand(DeliveryPlatformType.GrubHub, initialMappings));

        // Act
        await grain.RemoveMappingAsync("confirmed");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Mappings.Should().HaveCount(1);
        snapshot.Mappings.Should().NotContain(m => m.ExternalStatusCode == "confirmed");
    }

    [Fact]
    public async Task RecordUsageAsync_ShouldUpdateLastUsedAt()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetStatusMappingGrain(orgId, DeliveryPlatformType.Postmates);

        var mappings = new List<StatusMappingEntry>
        {
            new("accepted", "Accepted", InternalOrderStatus.Accepted, false, null)
        };

        await grain.ConfigureAsync(new ConfigureStatusMappingCommand(DeliveryPlatformType.Postmates, mappings));

        // Act
        await grain.RecordUsageAsync("accepted");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LastUsedAt.Should().NotBeNull();
        snapshot.LastUsedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}

[Collection(ClusterCollection.Name)]
public class ChannelGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ChannelGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IChannelGrain GetChannelGrain(Guid orgId, Guid channelId)
        => _fixture.Cluster.GrainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));

    [Fact]
    public async Task ConnectAsync_ShouldCreateChannel()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        var command = new ConnectChannelCommand(
            PlatformType: DeliveryPlatformType.Deliverect,
            IntegrationType: IntegrationType.Aggregator,
            Name: "Deliverect Production",
            ApiCredentialsEncrypted: "encrypted-api-key",
            WebhookSecret: "webhook-secret",
            ExternalChannelId: "ext-123",
            Settings: null);

        // Act
        var result = await grain.ConnectAsync(command);

        // Assert
        result.ChannelId.Should().Be(channelId);
        result.PlatformType.Should().Be(DeliveryPlatformType.Deliverect);
        result.IntegrationType.Should().Be(IntegrationType.Aggregator);
        result.Name.Should().Be("Deliverect Production");
        result.Status.Should().Be(ChannelStatus.Active);
        result.ExternalChannelId.Should().Be("ext-123");
        result.ConnectedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ConnectAsync_ShouldThrowIfAlreadyConnected()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        var command = new ConnectChannelCommand(
            DeliveryPlatformType.UberEats,
            IntegrationType.Direct,
            "UberEats",
            null, null, null, null);

        await grain.ConnectAsync(command);

        // Act & Assert
        var action = () => grain.ConnectAsync(command);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Channel already connected");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateChannel()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.Deliveroo,
            IntegrationType.Direct,
            "Deliveroo Test",
            null, null, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdateChannelCommand(
            Name: "Deliveroo Production",
            Status: null,
            ApiCredentialsEncrypted: "new-api-key",
            WebhookSecret: null,
            Settings: null));

        // Assert
        result.Name.Should().Be("Deliveroo Production");
    }

    [Fact]
    public async Task PauseAsync_ShouldSetPausedStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.JustEat,
            IntegrationType.Direct,
            "JustEat",
            null, null, null, null));

        // Act
        await grain.PauseAsync("Kitchen overwhelmed");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(ChannelStatus.Paused);
    }

    [Fact]
    public async Task ResumeAsync_ShouldSetActiveStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.DoorDash,
            IntegrationType.Direct,
            "DoorDash",
            null, null, null, null));

        await grain.PauseAsync();

        // Act
        await grain.ResumeAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(ChannelStatus.Active);
    }

    [Fact]
    public async Task AddLocationMappingAsync_ShouldAddLocation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.LocalWebsite,
            IntegrationType.Internal,
            "Website Orders",
            null, null, null, null));

        var mapping = new ChannelLocationMapping(
            LocationId: locationId,
            ExternalStoreId: "store-001",
            IsActive: true,
            MenuId: "menu-v1",
            OperatingHoursOverride: null);

        // Act
        await grain.AddLocationMappingAsync(mapping);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Locations.Should().HaveCount(1);
        snapshot.Locations[0].LocationId.Should().Be(locationId);
        snapshot.Locations[0].ExternalStoreId.Should().Be("store-001");
    }

    [Fact]
    public async Task RecordOrderAsync_ShouldIncrementCounters()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.Kiosk,
            IntegrationType.Internal,
            "Self-Service Kiosk",
            null, null, null, null));

        // Act
        await grain.RecordOrderAsync(25.50m);
        await grain.RecordOrderAsync(18.75m);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalOrdersToday.Should().Be(2);
        snapshot.TotalRevenueToday.Should().Be(44.25m);
        snapshot.LastOrderAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordErrorAsync_ShouldSetErrorStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.Wolt,
            IntegrationType.Direct,
            "Wolt",
            null, null, null, null));

        // Act
        await grain.RecordErrorAsync("API authentication failed");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(ChannelStatus.Error);
        snapshot.LastErrorMessage.Should().Be("API authentication failed");
    }

    [Fact]
    public async Task IsAcceptingOrdersAsync_ShouldReturnTrueWhenActive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.PhoneOrder,
            IntegrationType.Internal,
            "Phone Orders",
            null, null, null, null));

        // Act
        var result = await grain.IsAcceptingOrdersAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAcceptingOrdersAsync_ShouldReturnFalseWhenPaused()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.GrubHub,
            IntegrationType.Direct,
            "GrubHub",
            null, null, null, null));

        await grain.PauseAsync();

        // Act
        var result = await grain.IsAcceptingOrdersAsync();

        // Assert
        result.Should().BeFalse();
    }
}

[Collection(ClusterCollection.Name)]
public class ChannelRegistryGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ChannelRegistryGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IChannelRegistryGrain GetRegistryGrain(Guid orgId)
        => _fixture.Cluster.GrainFactory.GetGrain<IChannelRegistryGrain>(GrainKeys.ChannelRegistry(orgId));

    [Fact]
    public async Task RegisterChannelAsync_ShouldAddChannel()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetRegistryGrain(orgId);

        // Act
        await grain.RegisterChannelAsync(channelId, DeliveryPlatformType.Deliverect, IntegrationType.Aggregator, "Deliverect");

        // Assert
        var channels = await grain.GetAllChannelsAsync();
        channels.Should().HaveCount(1);
        channels[0].ChannelId.Should().Be(channelId);
        channels[0].PlatformType.Should().Be(DeliveryPlatformType.Deliverect);
        channels[0].IntegrationType.Should().Be(IntegrationType.Aggregator);
    }

    [Fact]
    public async Task GetChannelsByTypeAsync_ShouldFilterByIntegrationType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetRegistryGrain(orgId);

        await grain.RegisterChannelAsync(Guid.NewGuid(), DeliveryPlatformType.UberEats, IntegrationType.Direct, "UberEats");
        await grain.RegisterChannelAsync(Guid.NewGuid(), DeliveryPlatformType.Deliverect, IntegrationType.Aggregator, "Deliverect");
        await grain.RegisterChannelAsync(Guid.NewGuid(), DeliveryPlatformType.LocalWebsite, IntegrationType.Internal, "Website");
        await grain.RegisterChannelAsync(Guid.NewGuid(), DeliveryPlatformType.Deliveroo, IntegrationType.Direct, "Deliveroo");

        // Act
        var directChannels = await grain.GetChannelsByTypeAsync(IntegrationType.Direct);

        // Assert
        directChannels.Should().HaveCount(2);
        directChannels.Should().AllSatisfy(c => c.IntegrationType.Should().Be(IntegrationType.Direct));
    }

    [Fact]
    public async Task GetChannelsByPlatformAsync_ShouldFilterByPlatformType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetRegistryGrain(orgId);

        await grain.RegisterChannelAsync(Guid.NewGuid(), DeliveryPlatformType.Deliverect, IntegrationType.Aggregator, "Deliverect Channel 1");
        await grain.RegisterChannelAsync(Guid.NewGuid(), DeliveryPlatformType.Deliverect, IntegrationType.Aggregator, "Deliverect Channel 2");
        await grain.RegisterChannelAsync(Guid.NewGuid(), DeliveryPlatformType.UberEats, IntegrationType.Direct, "UberEats");

        // Act
        var deliverectChannels = await grain.GetChannelsByPlatformAsync(DeliveryPlatformType.Deliverect);

        // Assert
        deliverectChannels.Should().HaveCount(2);
        deliverectChannels.Should().AllSatisfy(c => c.PlatformType.Should().Be(DeliveryPlatformType.Deliverect));
    }

    [Fact]
    public async Task UpdateChannelStatusAsync_ShouldUpdateStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetRegistryGrain(orgId);

        await grain.RegisterChannelAsync(channelId, DeliveryPlatformType.JustEat, IntegrationType.Direct, "JustEat");

        // Act
        await grain.UpdateChannelStatusAsync(channelId, ChannelStatus.Paused);

        // Assert
        var channels = await grain.GetAllChannelsAsync();
        channels.Should().ContainSingle(c => c.ChannelId == channelId && c.Status == ChannelStatus.Paused);
    }

    [Fact]
    public async Task UnregisterChannelAsync_ShouldRemoveChannel()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetRegistryGrain(orgId);

        await grain.RegisterChannelAsync(channelId, DeliveryPlatformType.Wolt, IntegrationType.Direct, "Wolt");

        // Act
        await grain.UnregisterChannelAsync(channelId);

        // Assert
        var channels = await grain.GetAllChannelsAsync();
        channels.Should().BeEmpty();
    }
}
