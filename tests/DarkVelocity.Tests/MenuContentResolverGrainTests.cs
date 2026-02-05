using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MenuContentResolverGrainTests
{
    private readonly TestClusterFixture _fixture;

    public MenuContentResolverGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IMenuContentResolverGrain GetResolverGrain(Guid orgId, Guid siteId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IMenuContentResolverGrain>(
            GrainKeys.MenuContentResolver(orgId, siteId));
    }

    private IMenuRegistryGrain GetRegistryGrain(Guid orgId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IMenuRegistryGrain>(
            GrainKeys.MenuRegistry(orgId));
    }

    private IMenuItemDocumentGrain GetItemDocumentGrain(Guid orgId, string documentId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IMenuItemDocumentGrain>(
            GrainKeys.MenuItemDocument(orgId, documentId));
    }

    private IMenuCategoryDocumentGrain GetCategoryDocumentGrain(Guid orgId, string documentId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IMenuCategoryDocumentGrain>(
            GrainKeys.MenuCategoryDocument(orgId, documentId));
    }

    private ISiteMenuOverridesGrain GetOverridesGrain(Guid orgId, Guid siteId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<ISiteMenuOverridesGrain>(
            GrainKeys.SiteMenuOverrides(orgId, siteId));
    }

    private IModifierBlockGrain GetModifierBlockGrain(Guid orgId, string blockId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IModifierBlockGrain>(
            GrainKeys.ModifierBlock(orgId, blockId));
    }

    private IContentTagGrain GetContentTagGrain(Guid orgId, string tagId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IContentTagGrain>(
            GrainKeys.ContentTag(orgId, tagId));
    }

    // ============================================================================
    // Basic Content Resolution Tests
    // ============================================================================

    [Fact]
    public async Task ResolveAsync_EmptyMenu_ShouldReturnEmptyResult()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var resolver = GetResolverGrain(orgId, siteId);

        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.OrgId.Should().Be(orgId);
        result.SiteId.Should().Be(siteId);
        result.Categories.Should().BeEmpty();
        result.Items.Should().BeEmpty();
        result.Channel.Should().Be("pos");
        result.Locale.Should().Be("en-US");
        result.ETag.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WithPublishedCategory_ShouldReturnCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var categoryDocId = Guid.NewGuid().ToString();

        var categoryGrain = GetCategoryDocumentGrain(orgId, categoryDocId);
        await categoryGrain.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Appetizers",
            DisplayOrder: 1,
            Color: "#FF5733",
            PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterCategoryAsync(categoryDocId, "Appetizers", 1, "#FF5733");

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Categories.Should().HaveCount(1);
        result.Categories[0].DocumentId.Should().Be(categoryDocId);
        result.Categories[0].Name.Should().Be("Appetizers");
        result.Categories[0].Color.Should().Be("#FF5733");
        result.Categories[0].DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task ResolveAsync_WithPublishedItem_ShouldReturnItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemDocId = Guid.NewGuid().ToString();

        var itemGrain = GetItemDocumentGrain(orgId, itemDocId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Caesar Salad",
            Price: 12.99m,
            Description: "Fresh romaine with house dressing",
            PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemDocId, "Caesar Salad", 12.99m, null);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].DocumentId.Should().Be(itemDocId);
        result.Items[0].Name.Should().Be("Caesar Salad");
        result.Items[0].Price.Should().Be(12.99m);
        result.Items[0].Description.Should().Be("Fresh romaine with house dressing");
        result.Items[0].IsAvailable.Should().BeTrue();
        result.Items[0].IsSnoozed.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_WithMultipleItems_ShouldReturnAllItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemDocId1 = Guid.NewGuid().ToString();
        var itemDocId2 = Guid.NewGuid().ToString();
        var itemDocId3 = Guid.NewGuid().ToString();

        var item1 = GetItemDocumentGrain(orgId, itemDocId1);
        await item1.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Burger", Price: 15.99m, PublishImmediately: true));

        var item2 = GetItemDocumentGrain(orgId, itemDocId2);
        await item2.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Pizza", Price: 18.99m, PublishImmediately: true));

        var item3 = GetItemDocumentGrain(orgId, itemDocId3);
        await item3.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Pasta", Price: 14.99m, PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemDocId1, "Burger", 15.99m, null);
        await registryGrain.RegisterItemAsync(itemDocId2, "Pizza", 18.99m, null);
        await registryGrain.RegisterItemAsync(itemDocId3, "Pasta", 14.99m, null);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(3);
        result.Items.Select(i => i.Name).Should().Contain(new[] { "Burger", "Pizza", "Pasta" });
    }

    // ============================================================================
    // Site Override Tests - Price Overrides
    // ============================================================================

    [Fact]
    public async Task ResolveAsync_WithPriceOverride_ShouldApplyOverriddenPrice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemDocId = Guid.NewGuid().ToString();

        var itemGrain = GetItemDocumentGrain(orgId, itemDocId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Premium Steak",
            Price: 45.00m,
            PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemDocId, "Premium Steak", 45.00m, null);

        // Set a site-specific price override
        var overridesGrain = GetOverridesGrain(orgId, siteId);
        await overridesGrain.SetPriceOverrideAsync(new SetSitePriceOverrideCommand(
            ItemDocumentId: itemDocId,
            Price: 39.99m,
            Reason: "Location discount"));

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Premium Steak");
        result.Items[0].Price.Should().Be(39.99m); // Overridden price
    }

    [Fact]
    public async Task ResolveAsync_WithExpiredPriceOverride_ShouldUseOriginalPrice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemDocId = Guid.NewGuid().ToString();

        var itemGrain = GetItemDocumentGrain(orgId, itemDocId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Happy Hour Cocktail",
            Price: 12.00m,
            PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemDocId, "Happy Hour Cocktail", 12.00m, null);

        // Set expired price override
        var overridesGrain = GetOverridesGrain(orgId, siteId);
        await overridesGrain.SetPriceOverrideAsync(new SetSitePriceOverrideCommand(
            ItemDocumentId: itemDocId,
            Price: 8.00m,
            EffectiveFrom: DateTimeOffset.UtcNow.AddHours(-5),
            EffectiveUntil: DateTimeOffset.UtcNow.AddHours(-1),
            Reason: "Happy hour (expired)"));

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Price.Should().Be(12.00m); // Original price since override expired
    }

    [Fact]
    public async Task ResolveAsync_WithFuturePriceOverride_ShouldUseOriginalPrice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemDocId = Guid.NewGuid().ToString();

        var itemGrain = GetItemDocumentGrain(orgId, itemDocId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Seasonal Special",
            Price: 25.00m,
            PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemDocId, "Seasonal Special", 25.00m, null);

        // Set future price override
        var overridesGrain = GetOverridesGrain(orgId, siteId);
        await overridesGrain.SetPriceOverrideAsync(new SetSitePriceOverrideCommand(
            ItemDocumentId: itemDocId,
            Price: 19.99m,
            EffectiveFrom: DateTimeOffset.UtcNow.AddDays(7),
            Reason: "Future promotion"));

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Price.Should().Be(25.00m); // Original price since override not yet active
    }

    // ============================================================================
    // Site Override Tests - Hidden Items/Categories
    // ============================================================================

    [Fact]
    public async Task ResolveAsync_WithHiddenItem_ShouldExcludeItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var visibleItemId = Guid.NewGuid().ToString();
        var hiddenItemId = Guid.NewGuid().ToString();

        var visibleItem = GetItemDocumentGrain(orgId, visibleItemId);
        await visibleItem.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Visible Item", Price: 10.00m, PublishImmediately: true));

        var hiddenItem = GetItemDocumentGrain(orgId, hiddenItemId);
        await hiddenItem.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Hidden Item", Price: 15.00m, PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(visibleItemId, "Visible Item", 10.00m, null);
        await registryGrain.RegisterItemAsync(hiddenItemId, "Hidden Item", 15.00m, null);

        // Hide one item
        var overridesGrain = GetOverridesGrain(orgId, siteId);
        await overridesGrain.HideItemAsync(hiddenItemId);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US",
            IncludeHidden = false
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Visible Item");
    }

    [Fact]
    public async Task ResolveAsync_WithHiddenItem_IncludeHiddenTrue_ShouldIncludeItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var hiddenItemId = Guid.NewGuid().ToString();

        var hiddenItem = GetItemDocumentGrain(orgId, hiddenItemId);
        await hiddenItem.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Hidden Item", Price: 15.00m, PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(hiddenItemId, "Hidden Item", 15.00m, null);

        var overridesGrain = GetOverridesGrain(orgId, siteId);
        await overridesGrain.HideItemAsync(hiddenItemId);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US",
            IncludeHidden = true
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Hidden Item");
    }

    [Fact]
    public async Task ResolveAsync_WithHiddenCategory_ShouldExcludeCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var visibleCategoryId = Guid.NewGuid().ToString();
        var hiddenCategoryId = Guid.NewGuid().ToString();

        var visibleCategory = GetCategoryDocumentGrain(orgId, visibleCategoryId);
        await visibleCategory.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Visible Category", DisplayOrder: 1, PublishImmediately: true));

        var hiddenCategory = GetCategoryDocumentGrain(orgId, hiddenCategoryId);
        await hiddenCategory.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Hidden Category", DisplayOrder: 2, PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterCategoryAsync(visibleCategoryId, "Visible Category", 1, null);
        await registryGrain.RegisterCategoryAsync(hiddenCategoryId, "Hidden Category", 2, null);

        var overridesGrain = GetOverridesGrain(orgId, siteId);
        await overridesGrain.HideCategoryAsync(hiddenCategoryId);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US",
            IncludeHidden = false
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Categories.Should().HaveCount(1);
        result.Categories[0].Name.Should().Be("Visible Category");
    }

    // ============================================================================
    // Snoozing Tests (86'd Items)
    // ============================================================================

    [Fact]
    public async Task ResolveAsync_WithSnoozedItem_ShouldExcludeItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var snoozedItemId = Guid.NewGuid().ToString();

        var snoozedItem = GetItemDocumentGrain(orgId, snoozedItemId);
        await snoozedItem.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "86'd Special", Price: 22.00m, PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(snoozedItemId, "86'd Special", 22.00m, null);

        var overridesGrain = GetOverridesGrain(orgId, siteId);
        await overridesGrain.SnoozeItemAsync(
            snoozedItemId,
            until: DateTimeOffset.UtcNow.AddHours(2),
            reason: "Out of stock");

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US",
            IncludeSnoozed = false
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WithSnoozedItem_IncludeSnoozedTrue_ShouldIncludeItemWithFlag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var snoozedItemId = Guid.NewGuid().ToString();
        var snoozeUntil = DateTimeOffset.UtcNow.AddHours(2);

        var snoozedItem = GetItemDocumentGrain(orgId, snoozedItemId);
        await snoozedItem.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "86'd Special", Price: 22.00m, PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(snoozedItemId, "86'd Special", 22.00m, null);

        var overridesGrain = GetOverridesGrain(orgId, siteId);
        await overridesGrain.SnoozeItemAsync(snoozedItemId, until: snoozeUntil, reason: "Out of stock");

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US",
            IncludeSnoozed = true
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("86'd Special");
        result.Items[0].IsSnoozed.Should().BeTrue();
        result.Items[0].IsAvailable.Should().BeFalse();
        result.Items[0].SnoozedUntil.Should().BeCloseTo(snoozeUntil, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ResolveAsync_WithExpiredSnooze_ShouldIncludeItemAsAvailable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();

        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Previously Snoozed", Price: 15.00m, PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemId, "Previously Snoozed", 15.00m, null);

        // Snooze with a time in the past (expired)
        var overridesGrain = GetOverridesGrain(orgId, siteId);
        await overridesGrain.SnoozeItemAsync(
            itemId,
            until: DateTimeOffset.UtcNow.AddHours(-1),
            reason: "Was out of stock");

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US",
            IncludeSnoozed = false
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Previously Snoozed");
        result.Items[0].IsSnoozed.Should().BeFalse();
        result.Items[0].IsAvailable.Should().BeTrue();
    }

    // ============================================================================
    // Availability Window Tests
    // ============================================================================

    [Fact]
    public async Task ResolveAsync_ItemWithinAvailabilityWindow_ShouldIncludeItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        var currentDay = now.DayOfWeek;
        var currentTime = TimeOnly.FromDateTime(now.DateTime);

        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Available Item", Price: 10.00m, PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemId, "Available Item", 10.00m, null);

        // Create availability window that includes current time
        var overridesGrain = GetOverridesGrain(orgId, siteId);
        await overridesGrain.AddAvailabilityWindowAsync(new AddAvailabilityWindowCommand(
            Name: "Current Window",
            StartTime: currentTime.AddHours(-1),
            EndTime: currentTime.AddHours(1),
            DaysOfWeek: new List<DayOfWeek> { currentDay },
            ItemDocumentIds: new List<string> { itemId }));

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = now,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Available Item");
    }

    [Fact]
    public async Task ResolveAsync_ItemOutsideAvailabilityWindow_ShouldExcludeItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        var currentDay = now.DayOfWeek;
        var currentTime = TimeOnly.FromDateTime(now.DateTime);

        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Unavailable Item", Price: 10.00m, PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemId, "Unavailable Item", 10.00m, null);

        // Create availability window that does NOT include current time
        var overridesGrain = GetOverridesGrain(orgId, siteId);
        await overridesGrain.AddAvailabilityWindowAsync(new AddAvailabilityWindowCommand(
            Name: "Future Window",
            StartTime: currentTime.AddHours(3),
            EndTime: currentTime.AddHours(5),
            DaysOfWeek: new List<DayOfWeek> { currentDay },
            ItemDocumentIds: new List<string> { itemId }));

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = now,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_ItemWithNoAvailabilityWindow_ShouldAlwaysBeAvailable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();

        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Always Available", Price: 10.00m, PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemId, "Always Available", 10.00m, null);

        // No availability window set - item should always be available

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Always Available");
    }

    [Fact]
    public async Task ResolveAsync_ItemOnWrongDayOfWeek_ShouldExcludeItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        var currentDay = now.DayOfWeek;
        var currentTime = TimeOnly.FromDateTime(now.DateTime);

        // Get a day that's not today
        var differentDay = currentDay == DayOfWeek.Monday ? DayOfWeek.Tuesday : DayOfWeek.Monday;

        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Wrong Day Item", Price: 10.00m, PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemId, "Wrong Day Item", 10.00m, null);

        // Create availability window for a different day
        var overridesGrain = GetOverridesGrain(orgId, siteId);
        await overridesGrain.AddAvailabilityWindowAsync(new AddAvailabilityWindowCommand(
            Name: "Different Day Window",
            StartTime: new TimeOnly(0, 0),
            EndTime: new TimeOnly(23, 59),
            DaysOfWeek: new List<DayOfWeek> { differentDay },
            ItemDocumentIds: new List<string> { itemId }));

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = now,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().BeEmpty();
    }

    // ============================================================================
    // Localization Tests
    // ============================================================================

    [Fact]
    public async Task ResolveAsync_WithTranslation_ShouldReturnLocalizedName()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();

        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Chicken",
            Price: 15.00m,
            Description: "Grilled chicken",
            Locale: "en-US",
            PublishImmediately: true));

        await itemGrain.AddTranslationAsync(new AddMenuItemTranslationCommand(
            Locale: "es-ES",
            Name: "Pollo",
            Description: "Pollo a la parrilla"));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemId, "Chicken", 15.00m, null);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "es-ES"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Pollo");
        result.Items[0].Description.Should().Be("Pollo a la parrilla");
    }

    [Fact]
    public async Task ResolveAsync_WithMissingTranslation_ShouldFallbackToEnUS()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();

        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Beef Steak",
            Price: 30.00m,
            Locale: "en-US",
            PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemId, "Beef Steak", 30.00m, null);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "de-DE" // German - not available
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Beef Steak"); // Fallback to en-US
    }

    // ============================================================================
    // Modifier Block Tests
    // ============================================================================

    [Fact]
    public async Task ResolveAsync_WithModifierBlock_ShouldIncludeResolvedModifiers()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();
        var blockId = Guid.NewGuid().ToString();

        // Create modifier block
        var modifierGrain = GetModifierBlockGrain(orgId, blockId);
        await modifierGrain.CreateAsync(new CreateModifierBlockCommand(
            Name: "Size",
            SelectionRule: ModifierSelectionRule.ChooseOne,
            MinSelections: 1,
            MaxSelections: 1,
            IsRequired: true,
            Options: new List<CreateModifierOptionCommand>
            {
                new("Small", 0m, true, 1),
                new("Medium", 1.00m, false, 2),
                new("Large", 2.00m, false, 3)
            },
            PublishImmediately: true));

        // Create item with modifier block
        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Coffee",
            Price: 4.00m,
            PublishImmediately: true));
        await itemGrain.CreateDraftAsync(new CreateMenuItemDraftCommand(
            ModifierBlockIds: new List<string> { blockId }));
        await itemGrain.PublishDraftAsync();

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemId, "Coffee", 4.00m, null);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Modifiers.Should().HaveCount(1);
        result.Items[0].Modifiers[0].Name.Should().Be("Size");
        result.Items[0].Modifiers[0].IsRequired.Should().BeTrue();
        result.Items[0].Modifiers[0].SelectionRule.Should().Be(ModifierSelectionRule.ChooseOne);
        result.Items[0].Modifiers[0].Options.Should().HaveCount(3);
        result.Items[0].Modifiers[0].Options[0].Name.Should().Be("Small");
        result.Items[0].Modifiers[0].Options[0].IsDefault.Should().BeTrue();
    }

    // ============================================================================
    // Content Tag Tests
    // ============================================================================

    [Fact]
    public async Task ResolveAsync_WithTags_ShouldIncludeResolvedTags()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();
        var tagId1 = Guid.NewGuid().ToString();
        var tagId2 = Guid.NewGuid().ToString();

        // Create tags
        var tag1Grain = GetContentTagGrain(orgId, tagId1);
        await tag1Grain.CreateAsync(new CreateContentTagCommand(
            Name: "Gluten Free",
            Category: TagCategory.Dietary,
            BadgeColor: "#4CAF50"));

        var tag2Grain = GetContentTagGrain(orgId, tagId2);
        await tag2Grain.CreateAsync(new CreateContentTagCommand(
            Name: "Contains Nuts",
            Category: TagCategory.Allergen,
            BadgeColor: "#FF5722"));

        // Create item with tags
        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Almond Salad",
            Price: 12.00m,
            TagIds: new List<string> { tagId1, tagId2 },
            PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemId, "Almond Salad", 12.00m, null);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Tags.Should().HaveCount(2);
        result.Items[0].Tags.Should().Contain(t => t.Name == "Gluten Free" && t.Category == TagCategory.Dietary);
        result.Items[0].Tags.Should().Contain(t => t.Name == "Contains Nuts" && t.Category == TagCategory.Allergen);
    }

    [Fact]
    public async Task ResolveAsync_WithDeactivatedTag_ShouldExcludeTag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();
        var activeTagId = Guid.NewGuid().ToString();
        var inactiveTagId = Guid.NewGuid().ToString();

        // Create active tag
        var activeTagGrain = GetContentTagGrain(orgId, activeTagId);
        await activeTagGrain.CreateAsync(new CreateContentTagCommand(
            Name: "Vegan",
            Category: TagCategory.Dietary));

        // Create and deactivate tag
        var inactiveTagGrain = GetContentTagGrain(orgId, inactiveTagId);
        await inactiveTagGrain.CreateAsync(new CreateContentTagCommand(
            Name: "Old Promo",
            Category: TagCategory.Promotional));
        await inactiveTagGrain.DeactivateAsync();

        // Create item with both tags
        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Vegan Bowl",
            Price: 14.00m,
            TagIds: new List<string> { activeTagId, inactiveTagId },
            PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemId, "Vegan Bowl", 14.00m, null);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Tags.Should().HaveCount(1);
        result.Items[0].Tags[0].Name.Should().Be("Vegan");
    }

    // ============================================================================
    // Draft/Published Version Tests
    // ============================================================================

    [Fact]
    public async Task ResolveAsync_IncludeDraftFalse_ShouldOnlyReturnPublished()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();

        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Published Item",
            Price: 10.00m,
            PublishImmediately: true));

        // Create a draft with different data
        await itemGrain.CreateDraftAsync(new CreateMenuItemDraftCommand(
            Name: "Draft Version",
            Price: 12.00m));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemId, "Published Item", 10.00m, null);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US",
            IncludeDraft = false
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Published Item");
        result.Items[0].Price.Should().Be(10.00m);
    }

    [Fact]
    public async Task ResolveAsync_IncludeDraftTrue_ShouldReturnDraftWhenAvailable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();

        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Published Item",
            Price: 10.00m,
            PublishImmediately: true));

        await itemGrain.CreateDraftAsync(new CreateMenuItemDraftCommand(
            Name: "Draft Version",
            Price: 12.00m));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemId, "Published Item", 10.00m, null);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US",
            IncludeDraft = true
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Draft Version");
        result.Items[0].Price.Should().Be(12.00m);
    }

    // ============================================================================
    // Cache Tests
    // ============================================================================

    [Fact]
    public async Task InvalidateCacheAsync_ShouldClearCache()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();

        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Original Name",
            Price: 10.00m,
            PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemId, "Original Name", 10.00m, null);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // First resolve to populate cache
        var result1 = await resolver.ResolveAsync(context);
        result1.Items.Should().HaveCount(1);
        result1.Items[0].Name.Should().Be("Original Name");

        // Update item and registry
        await itemGrain.CreateDraftAsync(new CreateMenuItemDraftCommand(Name: "Updated Name"));
        await itemGrain.PublishDraftAsync();
        await registryGrain.UpdateItemAsync(itemId, "Updated Name", 10.00m, null, false, false);

        // Invalidate cache
        await resolver.InvalidateCacheAsync();

        // Act - resolve again
        var result2 = await resolver.ResolveAsync(context);

        // Assert
        result2.Items.Should().HaveCount(1);
        result2.Items[0].Name.Should().Be("Updated Name");
    }

    // ============================================================================
    // Preview Tests
    // ============================================================================

    [Fact]
    public async Task PreviewAsync_ShouldApplyPreviewOptions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var draftItemId = Guid.NewGuid().ToString();
        var hiddenItemId = Guid.NewGuid().ToString();
        var snoozedItemId = Guid.NewGuid().ToString();

        // Create draft-only item
        var draftItem = GetItemDocumentGrain(orgId, draftItemId);
        await draftItem.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Draft Only",
            Price: 5.00m,
            PublishImmediately: false));

        // Create hidden item
        var hiddenItem = GetItemDocumentGrain(orgId, hiddenItemId);
        await hiddenItem.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Hidden Item",
            Price: 10.00m,
            PublishImmediately: true));

        // Create snoozed item
        var snoozedItem = GetItemDocumentGrain(orgId, snoozedItemId);
        await snoozedItem.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Snoozed Item",
            Price: 15.00m,
            PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(draftItemId, "Draft Only", 5.00m, null);
        await registryGrain.RegisterItemAsync(hiddenItemId, "Hidden Item", 10.00m, null);
        await registryGrain.RegisterItemAsync(snoozedItemId, "Snoozed Item", 15.00m, null);

        var overridesGrain = GetOverridesGrain(orgId, siteId);
        await overridesGrain.HideItemAsync(hiddenItemId);
        await overridesGrain.SnoozeItemAsync(snoozedItemId, DateTimeOffset.UtcNow.AddHours(2));

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        var previewOptions = new MenuPreviewOptions(
            ShowDraft: true,
            ShowHidden: true,
            ShowSnoozed: true);

        // Act
        var result = await resolver.PreviewAsync(context, previewOptions);

        // Assert
        result.Items.Should().HaveCount(3);
        result.Items.Should().Contain(i => i.Name == "Draft Only");
        result.Items.Should().Contain(i => i.Name == "Hidden Item");
        result.Items.Should().Contain(i => i.Name == "Snoozed Item");
    }

    // ============================================================================
    // ResolveItemAsync Tests
    // ============================================================================

    [Fact]
    public async Task ResolveItemAsync_ExistingItem_ShouldReturnItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();

        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Single Item",
            Price: 20.00m,
            Description: "A specific item",
            PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemId, "Single Item", 20.00m, null);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveItemAsync(itemId, context);

        // Assert
        result.Should().NotBeNull();
        result!.DocumentId.Should().Be(itemId);
        result.Name.Should().Be("Single Item");
        result.Price.Should().Be(20.00m);
        result.Description.Should().Be("A specific item");
    }

    [Fact]
    public async Task ResolveItemAsync_NonExistingItem_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var nonExistentItemId = Guid.NewGuid().ToString();

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveItemAsync(nonExistentItemId, context);

        // Assert
        result.Should().BeNull();
    }

    // ============================================================================
    // WouldBeActiveAsync Tests
    // ============================================================================

    [Fact]
    public async Task WouldBeActiveAsync_CurrentVersion_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();

        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Versioned Item",
            Price: 10.00m,
            PublishImmediately: true));

        var resolver = GetResolverGrain(orgId, siteId);

        // Act
        var result = await resolver.WouldBeActiveAsync(itemId, 1, DateTimeOffset.UtcNow);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task WouldBeActiveAsync_WrongVersion_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();

        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Versioned Item",
            Price: 10.00m,
            PublishImmediately: true));

        var resolver = GetResolverGrain(orgId, siteId);

        // Act - check for version 999 which doesn't exist
        var result = await resolver.WouldBeActiveAsync(itemId, 999, DateTimeOffset.UtcNow);

        // Assert
        result.Should().BeFalse();
    }

    // ============================================================================
    // Edge Cases
    // ============================================================================

    [Fact]
    public async Task ResolveAsync_WithArchivedItem_ShouldNotIncludeItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var activeItemId = Guid.NewGuid().ToString();
        var archivedItemId = Guid.NewGuid().ToString();

        var activeItem = GetItemDocumentGrain(orgId, activeItemId);
        await activeItem.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Active Item",
            Price: 10.00m,
            PublishImmediately: true));

        var archivedItem = GetItemDocumentGrain(orgId, archivedItemId);
        await archivedItem.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Archived Item",
            Price: 15.00m,
            PublishImmediately: true));
        await archivedItem.ArchiveAsync(reason: "Discontinued");

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(activeItemId, "Active Item", 10.00m, null);
        // Note: Archived items should be marked in registry
        await registryGrain.UpdateItemAsync(archivedItemId, "Archived Item", 15.00m, null, false, true);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Active Item");
    }

    [Fact]
    public async Task ResolveAsync_WithCategoryAndItems_ShouldResolveCategoryName()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var categoryId = Guid.NewGuid().ToString();
        var itemId = Guid.NewGuid().ToString();

        // Create category
        var categoryGrain = GetCategoryDocumentGrain(orgId, categoryId);
        await categoryGrain.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Main Courses",
            DisplayOrder: 1,
            PublishImmediately: true));

        // Create item in category
        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Steak Dinner",
            Price: 35.00m,
            CategoryId: Guid.Parse(categoryId),
            PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterCategoryAsync(categoryId, "Main Courses", 1, null);
        await registryGrain.RegisterItemAsync(itemId, "Steak Dinner", 35.00m, categoryId);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Categories.Should().HaveCount(1);
        result.Categories[0].Name.Should().Be("Main Courses");

        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Steak Dinner");
        result.Items[0].CategoryId.Should().Be(categoryId);
        result.Items[0].CategoryName.Should().Be("Main Courses");
    }

    [Fact]
    public async Task ResolveAsync_MultipleCategoriesOrdered_ShouldReturnInDisplayOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var cat1Id = Guid.NewGuid().ToString();
        var cat2Id = Guid.NewGuid().ToString();
        var cat3Id = Guid.NewGuid().ToString();

        var cat1 = GetCategoryDocumentGrain(orgId, cat1Id);
        await cat1.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Desserts",
            DisplayOrder: 3,
            PublishImmediately: true));

        var cat2 = GetCategoryDocumentGrain(orgId, cat2Id);
        await cat2.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Appetizers",
            DisplayOrder: 1,
            PublishImmediately: true));

        var cat3 = GetCategoryDocumentGrain(orgId, cat3Id);
        await cat3.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Main Courses",
            DisplayOrder: 2,
            PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterCategoryAsync(cat1Id, "Desserts", 3, null);
        await registryGrain.RegisterCategoryAsync(cat2Id, "Appetizers", 1, null);
        await registryGrain.RegisterCategoryAsync(cat3Id, "Main Courses", 2, null);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Categories.Should().HaveCount(3);
        result.Categories[0].Name.Should().Be("Appetizers");
        result.Categories[1].Name.Should().Be("Main Courses");
        result.Categories[2].Name.Should().Be("Desserts");
    }

    [Fact]
    public async Task ResolveAsync_ShouldComputeETag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid().ToString();

        var itemGrain = GetItemDocumentGrain(orgId, itemId);
        await itemGrain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "ETag Test Item",
            Price: 10.00m,
            PublishImmediately: true));

        var registryGrain = GetRegistryGrain(orgId);
        await registryGrain.RegisterItemAsync(itemId, "ETag Test Item", 10.00m, null);

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.ETag.Should().NotBeNullOrEmpty();
        result.ETag.Length.Should().Be(16); // Base64-encoded SHA256 truncated to 16 chars
    }

    [Fact]
    public async Task ResolveAsync_ShouldSetCacheUntil()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.CacheUntil.Should().NotBeNull();
        result.CacheUntil.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ResolveAsync_ChannelAndLocaleStoredInResult()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "online",
            Locale = "fr-FR"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.Channel.Should().Be("online");
        result.Locale.Should().Be("fr-FR");
    }

    [Fact]
    public async Task ResolveAsync_ResolvedAtTimestamp_ShouldBeSetCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        var resolver = GetResolverGrain(orgId, siteId);
        var context = new MenuResolveContext
        {
            OrgId = orgId,
            SiteId = siteId,
            AsOf = DateTimeOffset.UtcNow,
            Channel = "pos",
            Locale = "en-US"
        };

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        var after = DateTimeOffset.UtcNow;
        result.ResolvedAt.Should().BeOnOrAfter(before);
        result.ResolvedAt.Should().BeOnOrBefore(after);
    }
}
