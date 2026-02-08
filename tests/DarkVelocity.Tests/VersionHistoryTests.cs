using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

// ============================================================================
// ModifierBlock Version History Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ModifierBlockVersionHistoryTests
{
    private readonly TestClusterFixture _fixture;

    public ModifierBlockVersionHistoryTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IModifierBlockGrain GetGrain(Guid orgId, string blockId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IModifierBlockGrain>(
            GrainKeys.ModifierBlock(orgId, blockId));
    }

    // Given: a modifier block with 3 published versions (Size options evolving)
    // When: the version history is retrieved
    // Then: all 3 versions are returned in reverse chronological order
    [Fact]
    public async Task GetVersionHistoryAsync_ShouldReturnAllVersions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, blockId);
        await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "Size V1",
            SelectionRule: ModifierSelectionRule.ChooseOne,
            Options: [new CreateModifierOptionCommand("Small", 0m)],
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateModifierBlockDraftCommand(Name: "Size V2"));
        await grain.PublishDraftAsync();
        await grain.CreateDraftAsync(new CreateModifierBlockDraftCommand(Name: "Size V3"));
        await grain.PublishDraftAsync();

        // Act
        var history = await grain.GetVersionHistoryAsync();

        // Assert
        history.Should().HaveCount(3);
        history[0].VersionNumber.Should().Be(3);
        history[0].Name.Should().Be("Size V3");
        history[1].VersionNumber.Should().Be(2);
        history[1].Name.Should().Be("Size V2");
        history[2].VersionNumber.Should().Be(1);
        history[2].Name.Should().Be("Size V1");
    }

    // Given: a modifier block with version 1 ("Size") and version 2 ("Portion")
    // When: the block is reverted to version 1
    // Then: a new version 3 is created with version 1's content and the block name is "Size"
    [Fact]
    public async Task RevertToVersionAsync_ShouldRevertToOlderVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, blockId);
        await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "Size",
            SelectionRule: ModifierSelectionRule.ChooseOne,
            MinSelections: 1,
            MaxSelections: 1,
            IsRequired: true,
            Options: [new CreateModifierOptionCommand("Small", 0m), new CreateModifierOptionCommand("Large", 1.00m)],
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateModifierBlockDraftCommand(
            Name: "Portion",
            SelectionRule: ModifierSelectionRule.ChooseMany));
        await grain.PublishDraftAsync();

        // Act
        await grain.RevertToVersionAsync(1, reason: "Reverting to original size options");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PublishedVersion.Should().Be(3);
        snapshot.Published!.Name.Should().Be("Size");
        snapshot.Published.SelectionRule.Should().Be(ModifierSelectionRule.ChooseOne);
        snapshot.Published.IsRequired.Should().BeTrue();
        snapshot.Published.Options.Should().HaveCount(2);
        snapshot.TotalVersions.Should().Be(3);
    }

    // Given: a modifier block with version 1
    // When: a revert to non-existent version 99 is attempted
    // Then: an InvalidOperationException is thrown
    [Fact]
    public async Task RevertToVersionAsync_ShouldThrowForNonExistentVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, blockId);
        await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "Test",
            PublishImmediately: true));

        // Act & Assert
        var act = () => grain.RevertToVersionAsync(99);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // Given: a modifier block with 5 published versions
    // When: the version history is retrieved with skip=1, take=2
    // Then: only versions 4 and 3 are returned (skipping the latest, taking 2)
    [Fact]
    public async Task GetVersionHistoryAsync_ShouldSupportPagination()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, blockId);
        await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "V1",
            PublishImmediately: true));
        for (int i = 2; i <= 5; i++)
        {
            await grain.CreateDraftAsync(new CreateModifierBlockDraftCommand(Name: $"V{i}"));
            await grain.PublishDraftAsync();
        }

        // Act
        var history = await grain.GetVersionHistoryAsync(skip: 1, take: 2);

        // Assert
        history.Should().HaveCount(2);
        history[0].VersionNumber.Should().Be(4);
        history[1].VersionNumber.Should().Be(3);
    }
}

// ============================================================================
// FloorPlan Version History Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class FloorPlanVersionHistoryTests
{
    private readonly TestClusterFixture _fixture;

    public FloorPlanVersionHistoryTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IFloorPlanGrain GetGrain(Guid orgId, Guid siteId, Guid floorPlanId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IFloorPlanGrain>(
            GrainKeys.FloorPlan(orgId, siteId, floorPlanId));
    }

    // Given: no existing floor plan
    // When: a "Main Floor" floor plan is created
    // Then: the floor plan exists with version 1
    [Fact]
    public async Task CreateAsync_ShouldCreateFloorPlanWithVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, floorPlanId);

        // Act
        await grain.CreateAsync(new CreateFloorPlanCommand(orgId, siteId, "Main Floor", false, 1000, 800));

        // Assert
        var version = await grain.GetVersionAsync();
        version.Should().BeGreaterThan(0);
        var state = await grain.GetStateAsync();
        state.Name.Should().Be("Main Floor");
    }

    // Given: an existing floor plan
    // When: the floor plan is updated twice (name and dimensions)
    // Then: the version is incremented for each change
    [Fact]
    public async Task UpdateAsync_ShouldIncrementVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, floorPlanId);
        await grain.CreateAsync(new CreateFloorPlanCommand(orgId, siteId, "Floor", false, 500, 500));
        var v1 = await grain.GetVersionAsync();

        // Act
        await grain.UpdateAsync(new UpdateFloorPlanCommand(Name: "Updated Floor"));
        var v2 = await grain.GetVersionAsync();

        await grain.UpdateAsync(new UpdateFloorPlanCommand(Width: 800));
        var v3 = await grain.GetVersionAsync();

        // Assert
        v2.Should().BeGreaterThan(v1);
        v3.Should().BeGreaterThan(v2);
    }

    // Given: a floor plan with sections
    // When: a section is added
    // Then: the version is incremented and the section exists
    [Fact]
    public async Task AddSectionAsync_ShouldIncrementVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, floorPlanId);
        await grain.CreateAsync(new CreateFloorPlanCommand(orgId, siteId, "Floor", false, 500, 500));
        var vBefore = await grain.GetVersionAsync();

        // Act
        await grain.AddSectionAsync("Patio", "#00FF00");
        var vAfter = await grain.GetVersionAsync();

        // Assert
        vAfter.Should().BeGreaterThan(vBefore);
        var state = await grain.GetStateAsync();
        state.Sections.Should().HaveCount(1);
        state.Sections[0].Name.Should().Be("Patio");
    }
}

// ============================================================================
// StatusMapping Version History Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class StatusMappingVersionHistoryTests
{
    private readonly TestClusterFixture _fixture;

    public StatusMappingVersionHistoryTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IStatusMappingGrain GetGrain(Guid orgId, DeliveryPlatformType platformType)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IStatusMappingGrain>(
            GrainKeys.StatusMapping(orgId, platformType));
    }

    // Given: a status mapping configured with 2 entries
    // When: a third mapping entry is added
    // Then: the version increments and all 3 mappings are present
    [Fact]
    public async Task AddMappingAsync_ShouldIncrementVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, DeliveryPlatformType.UberEats);
        await grain.ConfigureAsync(new ConfigureStatusMappingCommand(
            DeliveryPlatformType.UberEats,
            new List<StatusMappingEntry>
            {
                new("received", "Received", InternalOrderStatus.Received, false, null),
                new("accepted", "Accepted", InternalOrderStatus.Accepted, true, "PrintKot")
            }));
        var v1 = await grain.GetVersionAsync();

        // Act
        await grain.AddMappingAsync(new StatusMappingEntry("ready", "Ready", InternalOrderStatus.Ready, true, "NotifyCourier"));
        var v2 = await grain.GetVersionAsync();

        // Assert
        v2.Should().BeGreaterThan(v1);
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Mappings.Should().HaveCount(3);
    }
}

// ============================================================================
// Channel Version History Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ChannelVersionHistoryTests
{
    private readonly TestClusterFixture _fixture;

    public ChannelVersionHistoryTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IChannelGrain GetGrain(Guid orgId, Guid channelId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IChannelGrain>(
            GrainKeys.Channel(orgId, channelId));
    }

    // Given: no existing channel
    // When: a channel is connected
    // Then: the channel has version > 0
    [Fact]
    public async Task ConnectAsync_ShouldCreateChannelWithVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetGrain(orgId, channelId);

        // Act
        await grain.ConnectAsync(new ConnectChannelCommand(
            Name: "UberEats London",
            PlatformType: DeliveryPlatformType.UberEats,
            IntegrationType: IntegrationType.Api,
            ExternalChannelId: "uber-123",
            ApiCredentials: "encrypted-key",
            WebhookSecret: "webhook-secret"));

        // Assert
        var version = await grain.GetVersionAsync();
        version.Should().BeGreaterThan(0);
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Name.Should().Be("UberEats London");
    }

    // Given: a connected channel
    // When: the channel is updated and then paused
    // Then: the version increments for each operation
    [Fact]
    public async Task UpdateAndPause_ShouldIncrementVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetGrain(orgId, channelId);
        await grain.ConnectAsync(new ConnectChannelCommand(
            Name: "Deliveroo",
            PlatformType: DeliveryPlatformType.Deliveroo,
            IntegrationType: IntegrationType.Api,
            ExternalChannelId: "del-456"));
        var v1 = await grain.GetVersionAsync();

        // Act
        await grain.UpdateAsync(new UpdateChannelCommand(Name: "Deliveroo Updated"));
        var v2 = await grain.GetVersionAsync();

        await grain.PauseAsync("Maintenance window");
        var v3 = await grain.GetVersionAsync();

        // Assert
        v2.Should().BeGreaterThan(v1);
        v3.Should().BeGreaterThan(v2);
    }
}

// ============================================================================
// Supplier Version History Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class SupplierVersionHistoryTests
{
    private readonly TestClusterFixture _fixture;

    public SupplierVersionHistoryTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private ISupplierGrain GetGrain(Guid orgId, Guid supplierId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<ISupplierGrain>(
            GrainKeys.Supplier(orgId, supplierId));
    }

    // Given: no existing supplier
    // When: a supplier is created
    // Then: the supplier has version > 0
    [Fact]
    public async Task CreateAsync_ShouldCreateSupplierWithVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);

        // Act
        await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP001",
            Name: "Fresh Farms",
            PaymentTermsDays: 30,
            LeadTimeDays: 2));

        // Assert
        var version = await grain.GetVersionAsync();
        version.Should().BeGreaterThan(0);
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Name.Should().Be("Fresh Farms");
    }

    // Given: a supplier with no ingredients
    // When: an ingredient is added and its price is updated
    // Then: the version increments for each operation and the new price is reflected
    [Fact]
    public async Task AddIngredientAndUpdatePrice_ShouldIncrementVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);
        await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP002",
            Name: "Meat Co",
            PaymentTermsDays: 14,
            LeadTimeDays: 1));
        var v1 = await grain.GetVersionAsync();

        // Act
        await grain.AddIngredientAsync(new SupplierIngredientInput(
            ingredientId, "Chicken Breast", null, null, 8.50m, "kg", null, null));
        var v2 = await grain.GetVersionAsync();

        await grain.UpdateIngredientPriceAsync(ingredientId, 9.00m);
        var v3 = await grain.GetVersionAsync();

        // Assert
        v2.Should().BeGreaterThan(v1);
        v3.Should().BeGreaterThan(v2);
        var price = await grain.GetIngredientPriceAsync(ingredientId);
        price.Should().Be(9.00m);
    }
}

// ============================================================================
// BookingSettings Version History Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class BookingSettingsVersionHistoryTests
{
    private readonly TestClusterFixture _fixture;

    public BookingSettingsVersionHistoryTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IBookingSettingsGrain GetGrain(Guid orgId, Guid siteId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));
    }

    // Given: uninitialized booking settings
    // When: settings are initialized and then updated
    // Then: the version increments for each change
    [Fact]
    public async Task UpdateAsync_ShouldIncrementVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);
        await grain.InitializeAsync(orgId, siteId);
        var v1 = await grain.GetVersionAsync();

        // Act
        await grain.UpdateAsync(new UpdateBookingSettingsCommand(MaxPartySizeOnline: 12));
        var v2 = await grain.GetVersionAsync();

        await grain.BlockDateAsync(new DateOnly(2026, 12, 25));
        var v3 = await grain.GetVersionAsync();

        // Assert
        v2.Should().BeGreaterThan(v1);
        v3.Should().BeGreaterThan(v2);
    }
}

// ============================================================================
// CostingSettings Version History Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class CostingSettingsVersionHistoryTests
{
    private readonly TestClusterFixture _fixture;

    public CostingSettingsVersionHistoryTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private ICostingSettingsGrain GetGrain(Guid orgId, Guid locationId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));
    }

    // Given: uninitialized costing settings
    // When: settings are initialized and then updated
    // Then: the version increments for each change and thresholds are updated
    [Fact]
    public async Task UpdateAsync_ShouldIncrementVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        var v1 = await grain.GetVersionAsync();

        // Act
        await grain.UpdateAsync(new UpdateCostingSettingsCommand(
            TargetFoodCostPercent: 25m,
            MinimumMarginPercent: 55m));
        var v2 = await grain.GetVersionAsync();

        // Assert
        v2.Should().BeGreaterThan(v1);
        var settings = await grain.GetSettingsAsync();
        settings.TargetFoodCostPercent.Should().Be(25m);
        settings.MinimumMarginPercent.Should().Be(55m);
    }
}

// ============================================================================
// MenuSync Version History Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MenuSyncVersionHistoryTests
{
    private readonly TestClusterFixture _fixture;

    public MenuSyncVersionHistoryTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IMenuSyncGrain GetGrain(Guid orgId, Guid syncId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IMenuSyncGrain>(
            GrainKeys.MenuSync(orgId, syncId));
    }

    // Given: no existing menu sync
    // When: a sync is started and items are synced
    // Then: the version increments for each operation
    [Fact]
    public async Task SyncLifecycle_ShouldIncrementVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var grain = GetGrain(orgId, syncId);

        // Act
        await grain.StartAsync(new StartMenuSyncCommand(
            DeliveryPlatformId: Guid.NewGuid(),
            LocationId: Guid.NewGuid()));
        var v1 = await grain.GetVersionAsync();

        await grain.RecordItemSyncedAsync(new MenuItemMappingRecord(
            InternalMenuItemId: Guid.NewGuid(),
            PlatformItemId: "platform-item-1"));
        var v2 = await grain.GetVersionAsync();

        await grain.CompleteAsync();
        var v3 = await grain.GetVersionAsync();

        // Assert
        v1.Should().BeGreaterThan(0);
        v2.Should().BeGreaterThan(v1);
        v3.Should().BeGreaterThan(v2);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(MenuSyncStatus.Completed);
        snapshot.ItemsSynced.Should().Be(1);
    }
}
