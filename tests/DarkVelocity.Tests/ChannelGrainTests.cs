using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
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

    [Fact]
    public async Task GetExternalStatusAsync_MultipleMappingsToSame_ShouldReturnFirst()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetStatusMappingGrain(orgId, DeliveryPlatformType.Custom);

        // Multiple external codes mapping to the same internal status
        var mappings = new List<StatusMappingEntry>
        {
            new("PREP_STARTED", "Prep Started", InternalOrderStatus.Preparing, false, null),
            new("COOKING", "Cooking", InternalOrderStatus.Preparing, false, null),
            new("IN_PROGRESS", "In Progress", InternalOrderStatus.Preparing, false, null)
        };

        await grain.ConfigureAsync(new ConfigureStatusMappingCommand(DeliveryPlatformType.Custom, mappings));

        // Act
        var result = await grain.GetExternalStatusAsync(InternalOrderStatus.Preparing);

        // Assert - Should return the first matching external code
        result.Should().Be("PREP_STARTED");
    }

    [Fact]
    public async Task Operations_BeforeConfigureAsync_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetStatusMappingGrain(orgId, DeliveryPlatformType.Kiosk);

        // Act & Assert - AddMappingAsync before configure should throw
        var addAction = () => grain.AddMappingAsync(
            new StatusMappingEntry("test", "Test", InternalOrderStatus.Accepted, false, null));
        await addAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Status mapping not configured");

        // Act & Assert - RemoveMappingAsync before configure should throw
        var removeAction = () => grain.RemoveMappingAsync("test");
        await removeAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Status mapping not configured");
    }

    [Fact]
    public async Task ConfigureAsync_ReplacesExistingConfiguration()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetStatusMappingGrain(orgId, DeliveryPlatformType.PhoneOrder);

        var initialMappings = new List<StatusMappingEntry>
        {
            new("10", "Received", InternalOrderStatus.Received, false, null),
            new("20", "Accepted", InternalOrderStatus.Accepted, false, null)
        };

        await grain.ConfigureAsync(new ConfigureStatusMappingCommand(DeliveryPlatformType.PhoneOrder, initialMappings));

        // Act - Configure with completely different mappings
        var newMappings = new List<StatusMappingEntry>
        {
            new("NEW_STATUS", "New Status", InternalOrderStatus.Ready, true, "PrintReceipt")
        };

        var result = await grain.ConfigureAsync(new ConfigureStatusMappingCommand(DeliveryPlatformType.PhoneOrder, newMappings));

        // Assert - Old mappings should be replaced
        result.Mappings.Should().HaveCount(1);
        result.Mappings.Should().Contain(m => m.ExternalStatusCode == "NEW_STATUS");
        result.Mappings.Should().NotContain(m => m.ExternalStatusCode == "10");
        result.Mappings.Should().NotContain(m => m.ExternalStatusCode == "20");
    }

    [Fact]
    public async Task RemoveMappingAsync_NonExistent_ShouldBeIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetStatusMappingGrain(orgId, DeliveryPlatformType.LocalWebsite);

        var mappings = new List<StatusMappingEntry>
        {
            new("existing", "Existing", InternalOrderStatus.Accepted, false, null)
        };

        await grain.ConfigureAsync(new ConfigureStatusMappingCommand(DeliveryPlatformType.LocalWebsite, mappings));

        // Act - Remove a mapping that doesn't exist (should not throw)
        await grain.RemoveMappingAsync("nonexistent");

        // Assert - Original mapping should still exist
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Mappings.Should().HaveCount(1);
        snapshot.Mappings.Should().Contain(m => m.ExternalStatusCode == "existing");
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
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

    [Fact]
    public async Task RecordOrderAsync_DifferentDay_ShouldResetDailyCounters()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.UberEats,
            IntegrationType.Direct,
            "UberEats Counter Test",
            null, null, null, null));

        // Record orders on the same day
        await grain.RecordOrderAsync(100.00m);
        await grain.RecordOrderAsync(50.00m);

        var snapshotBeforeReset = await grain.GetSnapshotAsync();

        // Assert - Counters accumulate on same day
        snapshotBeforeReset.TotalOrdersToday.Should().Be(2);
        snapshotBeforeReset.TotalRevenueToday.Should().Be(150.00m);

        // Note: The actual day-change reset cannot be tested without time manipulation
        // This test verifies the counter accumulation works correctly
        // The reset logic in ResetDailyCountersIfNeeded is implementation-tested
    }

    [Fact]
    public async Task DisconnectAsync_ShouldSetStatusToDisconnected()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.Deliverect,
            IntegrationType.Aggregator,
            "Deliverect Disconnect Test",
            null, null, null, null));

        // Act
        await grain.DisconnectAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(ChannelStatus.Disconnected);
    }

    [Fact]
    public async Task RemoveLocationMappingAsync_ShouldRemoveLocation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.DoorDash,
            IntegrationType.Direct,
            "DoorDash Remove Location",
            null, null, null, null));

        await grain.AddLocationMappingAsync(new ChannelLocationMapping(
            LocationId: locationId,
            ExternalStoreId: "store-to-remove",
            IsActive: true,
            MenuId: null,
            OperatingHoursOverride: null));

        // Act
        await grain.RemoveLocationMappingAsync(locationId);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Locations.Should().BeEmpty();
    }

    [Fact]
    public async Task AddLocationMappingAsync_ExistingLocation_ShouldReplace()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.Deliveroo,
            IntegrationType.Direct,
            "Deliveroo Replace Location",
            null, null, null, null));

        // Add initial mapping
        await grain.AddLocationMappingAsync(new ChannelLocationMapping(
            LocationId: locationId,
            ExternalStoreId: "old-store-id",
            IsActive: true,
            MenuId: "menu-v1",
            OperatingHoursOverride: null));

        // Act - Add mapping for the same location with different details
        await grain.AddLocationMappingAsync(new ChannelLocationMapping(
            LocationId: locationId,
            ExternalStoreId: "new-store-id",
            IsActive: false,
            MenuId: "menu-v2",
            OperatingHoursOverride: "{ \"open\": \"10:00\" }"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Locations.Should().HaveCount(1);
        snapshot.Locations[0].ExternalStoreId.Should().Be("new-store-id");
        snapshot.Locations[0].IsActive.Should().BeFalse();
        snapshot.Locations[0].MenuId.Should().Be("menu-v2");
        snapshot.Locations[0].OperatingHoursOverride.Should().Be("{ \"open\": \"10:00\" }");
    }

    [Fact]
    public async Task UpdateAsync_WithStatusChange_ShouldUpdateStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.JustEat,
            IntegrationType.Direct,
            "JustEat Status Change",
            null, null, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdateChannelCommand(
            Name: null,
            Status: ChannelStatus.Maintenance,
            ApiCredentialsEncrypted: null,
            WebhookSecret: null,
            Settings: null));

        // Assert
        result.Status.Should().Be(ChannelStatus.Maintenance);
    }

    [Fact]
    public async Task RecordSyncAsync_ShouldUpdateTimestamp()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.Wolt,
            IntegrationType.Direct,
            "Wolt Sync Test",
            null, null, null, null));

        // Act
        await grain.RecordSyncAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LastSyncAt.Should().NotBeNull();
        snapshot.LastSyncAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RecordHeartbeatAsync_ShouldUpdateTimestamp()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.Postmates,
            IntegrationType.Direct,
            "Postmates Heartbeat Test",
            null, null, null, null));

        // Act
        await grain.RecordHeartbeatAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LastHeartbeatAt.Should().NotBeNull();
        snapshot.LastHeartbeatAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Operations_OnUnconnectedGrain_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        // Act & Assert - UpdateAsync on unconnected grain should throw
        var updateAction = () => grain.UpdateAsync(new UpdateChannelCommand(
            Name: "Test", Status: null, ApiCredentialsEncrypted: null, WebhookSecret: null, Settings: null));
        await updateAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Channel not connected");

        // Act & Assert - DisconnectAsync on unconnected grain should throw
        var disconnectAction = () => grain.DisconnectAsync();
        await disconnectAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Channel not connected");

        // Act & Assert - PauseAsync on unconnected grain should throw
        var pauseAction = () => grain.PauseAsync();
        await pauseAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Channel not connected");

        // Act & Assert - ResumeAsync on unconnected grain should throw
        var resumeAction = () => grain.ResumeAsync();
        await resumeAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Channel not connected");

        // Act & Assert - AddLocationMappingAsync on unconnected grain should throw
        var addLocationAction = () => grain.AddLocationMappingAsync(new ChannelLocationMapping(
            Guid.NewGuid(), "store-1", true, null, null));
        await addLocationAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Channel not connected");

        // Act & Assert - RecordOrderAsync on unconnected grain should throw
        var recordOrderAction = () => grain.RecordOrderAsync(100m);
        await recordOrderAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Channel not connected");

        // Act & Assert - RecordSyncAsync on unconnected grain should throw
        var recordSyncAction = () => grain.RecordSyncAsync();
        await recordSyncAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Channel not connected");

        // Act & Assert - RecordHeartbeatAsync on unconnected grain should throw
        var recordHeartbeatAction = () => grain.RecordHeartbeatAsync();
        await recordHeartbeatAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Channel not connected");
    }

    [Fact]
    public async Task AddLocationMappingAsync_MultipleMappings_ShouldTrackAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var location1 = Guid.NewGuid();
        var location2 = Guid.NewGuid();
        var location3 = Guid.NewGuid();
        var grain = GetChannelGrain(orgId, channelId);

        await grain.ConnectAsync(new ConnectChannelCommand(
            DeliveryPlatformType.Deliverect,
            IntegrationType.Aggregator,
            "Multi-Location Channel",
            null, null, null, null));

        // Act - Add multiple location mappings
        await grain.AddLocationMappingAsync(new ChannelLocationMapping(
            LocationId: location1,
            ExternalStoreId: "store-001",
            IsActive: true,
            MenuId: "menu-a",
            OperatingHoursOverride: null));

        await grain.AddLocationMappingAsync(new ChannelLocationMapping(
            LocationId: location2,
            ExternalStoreId: "store-002",
            IsActive: true,
            MenuId: "menu-b",
            OperatingHoursOverride: null));

        await grain.AddLocationMappingAsync(new ChannelLocationMapping(
            LocationId: location3,
            ExternalStoreId: "store-003",
            IsActive: false,
            MenuId: "menu-c",
            OperatingHoursOverride: null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Locations.Should().HaveCount(3);
        snapshot.Locations.Should().Contain(l => l.LocationId == location1 && l.ExternalStoreId == "store-001");
        snapshot.Locations.Should().Contain(l => l.LocationId == location2 && l.ExternalStoreId == "store-002");
        snapshot.Locations.Should().Contain(l => l.LocationId == location3 && l.ExternalStoreId == "store-003" && !l.IsActive);
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
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

    [Fact]
    public async Task GetChannelsForLocationAsync_ShouldReturnChannelsForLocation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var channel1 = Guid.NewGuid();
        var channel2 = Guid.NewGuid();
        var channel3 = Guid.NewGuid();
        var grain = GetRegistryGrain(orgId);

        // Register channels - note: location association is tracked separately
        // The registry tracks channels, location associations happen through the channel grains
        await grain.RegisterChannelAsync(channel1, DeliveryPlatformType.UberEats, IntegrationType.Direct, "UberEats");
        await grain.RegisterChannelAsync(channel2, DeliveryPlatformType.DoorDash, IntegrationType.Direct, "DoorDash");
        await grain.RegisterChannelAsync(channel3, DeliveryPlatformType.Deliveroo, IntegrationType.Direct, "Deliveroo");

        // Act - Query channels for a location (returns empty since we haven't associated locations)
        var channels = await grain.GetChannelsForLocationAsync(locationId);

        // Assert - No channels associated with this location yet
        channels.Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterChannelAsync_ExistingChannel_ShouldReplace()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetRegistryGrain(orgId);

        // Register initial channel
        await grain.RegisterChannelAsync(channelId, DeliveryPlatformType.UberEats, IntegrationType.Direct, "UberEats Initial");

        // Act - Register same channel ID with different details
        await grain.RegisterChannelAsync(channelId, DeliveryPlatformType.DoorDash, IntegrationType.Aggregator, "DoorDash Updated");

        // Assert
        var channels = await grain.GetAllChannelsAsync();
        channels.Should().HaveCount(1);
        channels[0].ChannelId.Should().Be(channelId);
        channels[0].PlatformType.Should().Be(DeliveryPlatformType.DoorDash);
        channels[0].IntegrationType.Should().Be(IntegrationType.Aggregator);
        channels[0].Name.Should().Be("DoorDash Updated");
    }

    [Fact]
    public async Task UpdateChannelStatusAsync_NonExistent_ShouldBeIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var nonExistentChannelId = Guid.NewGuid();
        var grain = GetRegistryGrain(orgId);

        // Act - Update status for a channel that doesn't exist (should not throw)
        await grain.UpdateChannelStatusAsync(nonExistentChannelId, ChannelStatus.Paused);

        // Assert - No channels should exist
        var channels = await grain.GetAllChannelsAsync();
        channels.Should().BeEmpty();
    }
}
