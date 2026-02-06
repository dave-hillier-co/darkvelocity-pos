using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests.Grains.Menu;

/// <summary>
/// Extended tests for Menu domain covering high-priority gaps:
/// - MenuDefinition versioning and screen management
/// - ModifierBlock selection rules validation
/// - SiteMenuOverrides cascade logic
/// - Availability window edge cases
/// - Draft/published state transitions
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MenuDomainExtendedTests
{
    private readonly TestClusterFixture _fixture;

    public MenuDomainExtendedTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    #region Grain Factory Helpers

    private IMenuDefinitionGrain GetMenuDefinitionGrain(Guid orgId, Guid menuId)
    {
        var key = $"{orgId}:menudef:{menuId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IMenuDefinitionGrain>(key);
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

    private IModifierBlockGrain GetModifierBlockGrain(Guid orgId, string blockId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IModifierBlockGrain>(
            GrainKeys.ModifierBlock(orgId, blockId));
    }

    private ISiteMenuOverridesGrain GetOverridesGrain(Guid orgId, Guid siteId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<ISiteMenuOverridesGrain>(
            GrainKeys.SiteMenuOverrides(orgId, siteId));
    }

    private IMenuItemGrain GetMenuItemGrain(Guid orgId, Guid itemId)
    {
        var key = $"{orgId}:menuitem:{itemId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IMenuItemGrain>(key);
    }

    #endregion

    // ============================================================================
    // MenuDefinition Versioning and Screen Management Tests
    // ============================================================================

    // Given: a POS menu definition with a main screen and two sub-screens (Drinks, Desserts)
    // When: navigation buttons linking to the sub-screens are added to the main screen
    // Then: the menu has 3 screens, and the main screen has 2 navigation buttons pointing to the sub-screens
    [Fact]
    public async Task MenuDefinition_MultipleScreensWithNavigation_ShouldManageScreenHierarchy()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var grain = GetMenuDefinitionGrain(orgId, menuId);

        await grain.CreateAsync(new CreateMenuDefinitionCommand(
            LocationId: Guid.NewGuid(),
            Name: "Full Service Menu",
            Description: "Complete restaurant menu with sub-screens",
            IsDefault: true));

        // Create main screen
        var mainScreenId = Guid.NewGuid();
        await grain.AddScreenAsync(new MenuScreenDefinition(
            ScreenId: mainScreenId,
            Name: "Main Screen",
            Position: 1,
            Color: "#FFFFFF",
            Rows: 5,
            Columns: 8,
            Buttons: new List<MenuButtonDefinition>()));

        // Create sub-screens
        var drinksScreenId = Guid.NewGuid();
        var dessertsScreenId = Guid.NewGuid();

        await grain.AddScreenAsync(new MenuScreenDefinition(
            drinksScreenId, "Drinks", 2, "#0066CC", 4, 6, new List<MenuButtonDefinition>()));
        await grain.AddScreenAsync(new MenuScreenDefinition(
            dessertsScreenId, "Desserts", 3, "#FF6B35", 3, 4, new List<MenuButtonDefinition>()));

        // Act - Add navigation buttons to main screen
        await grain.AddButtonAsync(mainScreenId, new MenuButtonDefinition(
            ButtonId: Guid.NewGuid(),
            MenuItemId: null,
            SubScreenId: drinksScreenId,
            Row: 0,
            Column: 0,
            Label: "Drinks Menu",
            Color: "#0066CC",
            ButtonType: "Navigation"));

        await grain.AddButtonAsync(mainScreenId, new MenuButtonDefinition(
            ButtonId: Guid.NewGuid(),
            MenuItemId: null,
            SubScreenId: dessertsScreenId,
            Row: 0,
            Column: 1,
            Label: "Desserts Menu",
            Color: "#FF6B35",
            ButtonType: "Navigation"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Screens.Should().HaveCount(3);

        var mainScreen = snapshot.Screens.First(s => s.ScreenId == mainScreenId);
        mainScreen.Buttons.Should().HaveCount(2);
        mainScreen.Buttons.Should().AllSatisfy(b => b.ButtonType.Should().Be("Navigation"));
        mainScreen.Buttons.Should().Contain(b => b.SubScreenId == drinksScreenId);
        mainScreen.Buttons.Should().Contain(b => b.SubScreenId == dessertsScreenId);
    }

    // Given: a menu definition with a screen named "Original Screen" (3x4 grid, black)
    // When: the screen is updated to "Updated Screen" with a red color and 5x8 grid
    // Then: the screen reflects the new name, color, and dimensions
    [Fact]
    public async Task MenuDefinition_UpdateScreenProperties_ShouldModifyNameAndDimensions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var grain = GetMenuDefinitionGrain(orgId, menuId);

        await grain.CreateAsync(new CreateMenuDefinitionCommand(
            LocationId: Guid.NewGuid(),
            Name: "Update Test Menu",
            Description: null,
            IsDefault: false));

        var screenId = Guid.NewGuid();
        await grain.AddScreenAsync(new MenuScreenDefinition(
            screenId, "Original Screen", 1, "#000000", 3, 4, new List<MenuButtonDefinition>()));

        // Act - Update screen properties
        await grain.UpdateScreenAsync(screenId, "Updated Screen", "#FF0000", 5, 8);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        var screen = snapshot.Screens.First(s => s.ScreenId == screenId);
        screen.Name.Should().Be("Updated Screen");
        screen.Color.Should().Be("#FF0000");
        screen.Rows.Should().Be(5);
        screen.Columns.Should().Be(8);
    }

    // Given: three screens added out of position order (C=3, A=1, B=2) to a menu definition
    // When: the screens are retrieved and sorted by position
    // Then: the screens appear in correct order: Screen A, Screen B, Screen C
    [Fact]
    public async Task MenuDefinition_ScreensAddedInSequence_ShouldMaintainPositionOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var grain = GetMenuDefinitionGrain(orgId, menuId);

        await grain.CreateAsync(new CreateMenuDefinitionCommand(
            LocationId: Guid.NewGuid(),
            Name: "Position Test Menu",
            Description: null,
            IsDefault: false));

        var screen1Id = Guid.NewGuid();
        var screen2Id = Guid.NewGuid();
        var screen3Id = Guid.NewGuid();

        // Add screens with specific positions
        await grain.AddScreenAsync(new MenuScreenDefinition(
            screen3Id, "Screen C", 3, null, 3, 4, new List<MenuButtonDefinition>()));
        await grain.AddScreenAsync(new MenuScreenDefinition(
            screen1Id, "Screen A", 1, null, 3, 4, new List<MenuButtonDefinition>()));
        await grain.AddScreenAsync(new MenuScreenDefinition(
            screen2Id, "Screen B", 2, null, 3, 4, new List<MenuButtonDefinition>()));

        // Assert - Screens should be retrievable ordered by position
        var snapshot = await grain.GetSnapshotAsync();
        var orderedScreens = snapshot.Screens.OrderBy(s => s.Position).ToList();
        orderedScreens[0].Name.Should().Be("Screen A");
        orderedScreens[1].Name.Should().Be("Screen B");
        orderedScreens[2].Name.Should().Be("Screen C");
    }

    // Given: a 4x6 grid screen on a POS menu definition
    // When: five item buttons are placed at specific grid positions (corners and center)
    // Then: each button retains its assigned row and column position in the grid
    [Fact]
    public async Task MenuDefinition_ButtonGridLayout_ShouldRespectRowColumnPositions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var grain = GetMenuDefinitionGrain(orgId, menuId);

        await grain.CreateAsync(new CreateMenuDefinitionCommand(
            LocationId: Guid.NewGuid(),
            Name: "Grid Layout Menu",
            Description: null,
            IsDefault: false));

        var screenId = Guid.NewGuid();
        await grain.AddScreenAsync(new MenuScreenDefinition(
            screenId, "Grid Screen", 1, null, 4, 6, new List<MenuButtonDefinition>()));

        // Act - Add buttons in specific grid positions
        var positions = new[]
        {
            (Row: 0, Col: 0, Label: "Top-Left"),
            (Row: 0, Col: 5, Label: "Top-Right"),
            (Row: 3, Col: 0, Label: "Bottom-Left"),
            (Row: 3, Col: 5, Label: "Bottom-Right"),
            (Row: 1, Col: 2, Label: "Middle"),
        };

        foreach (var pos in positions)
        {
            await grain.AddButtonAsync(screenId, new MenuButtonDefinition(
                ButtonId: Guid.NewGuid(),
                MenuItemId: Guid.NewGuid(),
                SubScreenId: null,
                Row: pos.Row,
                Column: pos.Col,
                Label: pos.Label,
                Color: "#333333",
                ButtonType: "Item"));
        }

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        var screen = snapshot.Screens.First();
        screen.Buttons.Should().HaveCount(5);

        screen.Buttons.Should().Contain(b => b.Row == 0 && b.Column == 0 && b.Label == "Top-Left");
        screen.Buttons.Should().Contain(b => b.Row == 0 && b.Column == 5 && b.Label == "Top-Right");
        screen.Buttons.Should().Contain(b => b.Row == 3 && b.Column == 0 && b.Label == "Bottom-Left");
        screen.Buttons.Should().Contain(b => b.Row == 3 && b.Column == 5 && b.Label == "Bottom-Right");
        screen.Buttons.Should().Contain(b => b.Row == 1 && b.Column == 2 && b.Label == "Middle");
    }

    // Given: a POS menu button placed at position (0,0) on a screen
    // When: the button is removed and re-added at position (2,3)
    // Then: the button's new position is (2,3)
    [Fact]
    public async Task MenuDefinition_UpdateButtonPosition_ShouldMoveButton()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var grain = GetMenuDefinitionGrain(orgId, menuId);

        await grain.CreateAsync(new CreateMenuDefinitionCommand(
            LocationId: Guid.NewGuid(),
            Name: "Button Move Menu",
            Description: null,
            IsDefault: false));

        var screenId = Guid.NewGuid();
        var buttonId = Guid.NewGuid();

        await grain.AddScreenAsync(new MenuScreenDefinition(
            screenId, "Test Screen", 1, null, 4, 6, new List<MenuButtonDefinition>()));

        await grain.AddButtonAsync(screenId, new MenuButtonDefinition(
            buttonId, Guid.NewGuid(), null, 0, 0, "Movable Button", "#FF0000", "Item"));

        // Act - Remove and re-add button at new position (simulating move)
        await grain.RemoveButtonAsync(screenId, buttonId);
        await grain.AddButtonAsync(screenId, new MenuButtonDefinition(
            buttonId, Guid.NewGuid(), null, 2, 3, "Movable Button", "#FF0000", "Item"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        var button = snapshot.Screens.First().Buttons.First(b => b.ButtonId == buttonId);
        button.Row.Should().Be(2);
        button.Column.Should().Be(3);
    }

    // ============================================================================
    // ModifierBlock Selection Rules Validation Tests
    // ============================================================================

    // Given: a new modifier block for steak temperature with ChooseOne selection rule
    // When: the block is created with 5 options (Rare through Well Done), min=1, max=1, required
    // Then: the published block enforces single selection with exactly one default option
    [Fact]
    public async Task ModifierBlock_ChooseOne_ShouldEnforceMinMaxOfOne()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetModifierBlockGrain(orgId, blockId);

        // Act
        var result = await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "Temperature",
            SelectionRule: ModifierSelectionRule.ChooseOne,
            MinSelections: 1,
            MaxSelections: 1,
            IsRequired: true,
            Options:
            [
                new CreateModifierOptionCommand("Rare", 0m, false, 1),
                new CreateModifierOptionCommand("Medium Rare", 0m, false, 2),
                new CreateModifierOptionCommand("Medium", 0m, true, 3),
                new CreateModifierOptionCommand("Medium Well", 0m, false, 4),
                new CreateModifierOptionCommand("Well Done", 0m, false, 5)
            ],
            PublishImmediately: true));

        // Assert
        result.Published.Should().NotBeNull();
        result.Published!.SelectionRule.Should().Be(ModifierSelectionRule.ChooseOne);
        result.Published.MinSelections.Should().Be(1);
        result.Published.MaxSelections.Should().Be(1);
        result.Published.IsRequired.Should().BeTrue();
        result.Published.Options.Should().HaveCount(5);
        result.Published.Options.Count(o => o.IsDefault).Should().Be(1);
    }

    // Given: a new modifier block for extra toppings with ChooseMany selection rule
    // When: the block is created with 6 priced options, min=0, max=10, not required
    // Then: the published block allows multiple optional selections with individual price adjustments
    [Fact]
    public async Task ModifierBlock_ChooseMany_ShouldAllowMultipleSelections()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetModifierBlockGrain(orgId, blockId);

        // Act
        var result = await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "Extra Toppings",
            SelectionRule: ModifierSelectionRule.ChooseMany,
            MinSelections: 0,
            MaxSelections: 10,
            IsRequired: false,
            Options:
            [
                new CreateModifierOptionCommand("Extra Cheese", 2.00m, false, 1),
                new CreateModifierOptionCommand("Bacon", 3.00m, false, 2),
                new CreateModifierOptionCommand("Avocado", 2.50m, false, 3),
                new CreateModifierOptionCommand("Jalapenos", 1.00m, false, 4),
                new CreateModifierOptionCommand("Mushrooms", 1.50m, false, 5),
                new CreateModifierOptionCommand("Onions", 1.00m, false, 6)
            ],
            PublishImmediately: true));

        // Assert
        result.Published.Should().NotBeNull();
        result.Published!.SelectionRule.Should().Be(ModifierSelectionRule.ChooseMany);
        result.Published.MinSelections.Should().Be(0);
        result.Published.MaxSelections.Should().Be(10);
        result.Published.IsRequired.Should().BeFalse();
        result.Published.Options.Should().HaveCount(6);
    }

    // Given: a "Choose Your Sides" modifier block requiring at least 2 selections (max 3)
    // When: the block is created with 5 side options and 2 defaults pre-selected
    // Then: the published block enforces min=2, max=3 with 2 default options to meet the minimum
    [Fact]
    public async Task ModifierBlock_RequiredWithMinimum_ShouldEnforceMinSelections()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetModifierBlockGrain(orgId, blockId);

        // Act - At least 2 sides required
        var result = await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "Choose Your Sides",
            SelectionRule: ModifierSelectionRule.ChooseMany,
            MinSelections: 2,
            MaxSelections: 3,
            IsRequired: true,
            Options:
            [
                new CreateModifierOptionCommand("Fries", 0m, true, 1),
                new CreateModifierOptionCommand("Coleslaw", 0m, true, 2),
                new CreateModifierOptionCommand("Mac & Cheese", 0m, false, 3),
                new CreateModifierOptionCommand("Side Salad", 0m, false, 4),
                new CreateModifierOptionCommand("Onion Rings", 1.00m, false, 5)
            ],
            PublishImmediately: true));

        // Assert
        result.Published.Should().NotBeNull();
        result.Published!.MinSelections.Should().Be(2);
        result.Published.MaxSelections.Should().Be(3);
        result.Published.IsRequired.Should().BeTrue();
        // Default options should be set to help meet minimum
        result.Published.Options.Count(o => o.IsDefault).Should().Be(2);
    }

    // Given: a "Size Options" modifier block with price adjustments (-$2 Small, $0 Regular, +$1.50 Large, +$3 XL)
    // When: the block is created and published
    // Then: each option stores its correct price adjustment including negative discounts
    [Fact]
    public async Task ModifierBlock_OptionsWithPriceAdjustments_ShouldStoreCorrectPrices()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetModifierBlockGrain(orgId, blockId);

        // Act
        var result = await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "Size Options",
            SelectionRule: ModifierSelectionRule.ChooseOne,
            MinSelections: 1,
            MaxSelections: 1,
            IsRequired: true,
            Options:
            [
                new CreateModifierOptionCommand("Small", -2.00m, false, 1),
                new CreateModifierOptionCommand("Regular", 0m, true, 2),
                new CreateModifierOptionCommand("Large", 1.50m, false, 3),
                new CreateModifierOptionCommand("Extra Large", 3.00m, false, 4)
            ],
            PublishImmediately: true));

        // Assert
        var options = result.Published!.Options;
        options.First(o => o.Name == "Small").PriceAdjustment.Should().Be(-2.00m);
        options.First(o => o.Name == "Regular").PriceAdjustment.Should().Be(0m);
        options.First(o => o.Name == "Large").PriceAdjustment.Should().Be(1.50m);
        options.First(o => o.Name == "Extra Large").PriceAdjustment.Should().Be(3.00m);
    }

    // Given: a published modifier block with 2 options (A, B)
    // When: a draft is created adding a third option (C) at version 2
    // Then: the draft has 3 options at version 2, while the published version still has 2 options
    [Fact]
    public async Task ModifierBlock_UpdateDraftWithNewOptions_ShouldPreserveVersionHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetModifierBlockGrain(orgId, blockId);

        await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "Original Options",
            SelectionRule: ModifierSelectionRule.ChooseOne,
            MinSelections: 1,
            MaxSelections: 1,
            IsRequired: true,
            Options:
            [
                new CreateModifierOptionCommand("Option A", 0m, true, 1),
                new CreateModifierOptionCommand("Option B", 0m, false, 2)
            ],
            PublishImmediately: true));

        // Act - Create draft with additional options
        var draft = await grain.CreateDraftAsync(new CreateModifierBlockDraftCommand(
            Name: "Updated Options",
            ChangeNote: "Adding Option C",
            Options:
            [
                new CreateModifierOptionCommand("Option A", 0m, true, 1),
                new CreateModifierOptionCommand("Option B", 0m, false, 2),
                new CreateModifierOptionCommand("Option C", 1.00m, false, 3)
            ]));

        // Assert - Draft has 3 options
        draft.Options.Should().HaveCount(3);
        draft.VersionNumber.Should().Be(2);

        // Published still has 2 options
        var published = await grain.GetPublishedAsync();
        published!.Options.Should().HaveCount(2);
    }

    // ============================================================================
    // SiteMenuOverrides Cascade Logic Tests
    // ============================================================================

    // Given: site-level price overrides set for three different menu items
    // When: the price override for each item (and a non-overridden item) is queried
    // Then: overridden items return their site-specific prices; non-overridden items return null
    [Fact]
    public async Task SiteMenuOverrides_MultiplePriceOverrides_ShouldReturnCorrectPrices()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetOverridesGrain(orgId, siteId);

        // Act - Set multiple price overrides
        await grain.SetPriceOverrideAsync(new SetSitePriceOverrideCommand(
            ItemDocumentId: "item-1",
            Price: 9.99m,
            Reason: "Local pricing for item 1"));

        await grain.SetPriceOverrideAsync(new SetSitePriceOverrideCommand(
            ItemDocumentId: "item-2",
            Price: 14.99m,
            Reason: "Local pricing for item 2"));

        await grain.SetPriceOverrideAsync(new SetSitePriceOverrideCommand(
            ItemDocumentId: "item-3",
            Price: 19.99m,
            Reason: "Local pricing for item 3"));

        // Assert
        var price1 = await grain.GetPriceOverrideAsync("item-1");
        var price2 = await grain.GetPriceOverrideAsync("item-2");
        var price3 = await grain.GetPriceOverrideAsync("item-3");
        var price4 = await grain.GetPriceOverrideAsync("item-4"); // No override

        price1.Should().Be(9.99m);
        price2.Should().Be(14.99m);
        price3.Should().Be(19.99m);
        price4.Should().BeNull();
    }

    // Given: two items hidden and two items snoozed (one timed, one indefinite) at the site level
    // When: the site override snapshot is inspected
    // Then: hidden and snoozed items are tracked independently with correct states
    [Fact]
    public async Task SiteMenuOverrides_HiddenAndSnoozedItems_ShouldTrackBothStates()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetOverridesGrain(orgId, siteId);

        // Act
        await grain.HideItemAsync("hidden-item-1");
        await grain.HideItemAsync("hidden-item-2");
        await grain.SnoozeItemAsync("snoozed-item-1", DateTimeOffset.UtcNow.AddHours(2));
        await grain.SnoozeItemAsync("snoozed-item-2", until: null); // Indefinite snooze

        // Assert
        var snapshot = await grain.GetSnapshotAsync();

        snapshot.HiddenItemIds.Should().Contain("hidden-item-1");
        snapshot.HiddenItemIds.Should().Contain("hidden-item-2");
        snapshot.SnoozedItems.Keys.Should().Contain("snoozed-item-1");
        snapshot.SnoozedItems.Keys.Should().Contain("snoozed-item-2");

        // Verify snooze states
        var isSnoozed1 = await grain.IsItemSnoozedAsync("snoozed-item-1");
        var isSnoozed2 = await grain.IsItemSnoozedAsync("snoozed-item-2");
        isSnoozed1.Should().BeTrue();
        isSnoozed2.Should().BeTrue();
    }

    // Given: a site price override of $10.00 for an item
    // When: the price override is updated to $12.99
    // Then: only the latest price override is stored (no duplicate entries)
    [Fact]
    public async Task SiteMenuOverrides_UpdatePriceOverride_ShouldReplaceExisting()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetOverridesGrain(orgId, siteId);

        await grain.SetPriceOverrideAsync(new SetSitePriceOverrideCommand(
            ItemDocumentId: "item-1",
            Price: 10.00m,
            Reason: "Original price"));

        // Act - Update with new price
        await grain.SetPriceOverrideAsync(new SetSitePriceOverrideCommand(
            ItemDocumentId: "item-1",
            Price: 12.99m,
            Reason: "Updated price"));

        // Assert
        var price = await grain.GetPriceOverrideAsync("item-1");
        price.Should().Be(12.99m);

        // Should only have one override for the item
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PriceOverrides.Count(p => p.ItemDocumentId == "item-1").Should().Be(1);
    }

    // Given: a site with no hidden categories
    // When: "seasonal-category" and "lunch-only-category" are hidden at the site level
    // Then: both category IDs appear in the hidden categories list
    [Fact]
    public async Task SiteMenuOverrides_HiddenCategory_ShouldTrackCategoryVisibility()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetOverridesGrain(orgId, siteId);

        // Act
        await grain.HideCategoryAsync("seasonal-category");
        await grain.HideCategoryAsync("lunch-only-category");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.HiddenCategoryIds.Should().HaveCount(2);
        snapshot.HiddenCategoryIds.Should().Contain("seasonal-category");
        snapshot.HiddenCategoryIds.Should().Contain("lunch-only-category");
    }

    // Given: two categories hidden at the site level
    // When: one category is unhidden
    // Then: only the still-hidden category remains in the hidden list
    [Fact]
    public async Task SiteMenuOverrides_UnhideCategory_ShouldRemoveFromHiddenList()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetOverridesGrain(orgId, siteId);

        await grain.HideCategoryAsync("category-1");
        await grain.HideCategoryAsync("category-2");

        // Act
        await grain.UnhideCategoryAsync("category-1");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.HiddenCategoryIds.Should().NotContain("category-1");
        snapshot.HiddenCategoryIds.Should().Contain("category-2");
    }

    // ============================================================================
    // Availability Window Edge Cases Tests
    // ============================================================================

    // Given: a site needing a late night menu
    // When: an availability window is created from 10 PM to 2 AM on Friday and Saturday
    // Then: the window stores the overnight time span crossing midnight with the correct days
    [Fact]
    public async Task AvailabilityWindow_OvernightWindow_ShouldSpanMidnight()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetOverridesGrain(orgId, siteId);

        // Act - Create late night menu (10 PM to 2 AM)
        var window = await grain.AddAvailabilityWindowAsync(new AddAvailabilityWindowCommand(
            Name: "Late Night Menu",
            StartTime: new TimeOnly(22, 0),  // 10 PM
            EndTime: new TimeOnly(2, 0),     // 2 AM next day
            DaysOfWeek: new List<DayOfWeek>
            {
                DayOfWeek.Friday,
                DayOfWeek.Saturday
            },
            ItemDocumentIds: new List<string> { "late-night-burger", "late-night-fries" }));

        // Assert
        window.StartTime.Should().Be(new TimeOnly(22, 0));
        window.EndTime.Should().Be(new TimeOnly(2, 0));
        window.Name.Should().Be("Late Night Menu");
        window.IsActive.Should().BeTrue();
    }

    // Given: an "eggs-benedict" item that should be available during breakfast and brunch
    // When: two overlapping availability windows are created for the same item
    // Then: both windows are tracked independently for the item
    [Fact]
    public async Task AvailabilityWindow_MultipleWindowsSameItem_ShouldTrackAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetOverridesGrain(orgId, siteId);

        // Act - Item available during breakfast and dinner
        await grain.AddAvailabilityWindowAsync(new AddAvailabilityWindowCommand(
            Name: "Breakfast Window",
            StartTime: new TimeOnly(6, 0),
            EndTime: new TimeOnly(11, 0),
            DaysOfWeek: Enum.GetValues<DayOfWeek>().ToList(),
            ItemDocumentIds: new List<string> { "eggs-benedict" }));

        await grain.AddAvailabilityWindowAsync(new AddAvailabilityWindowCommand(
            Name: "Brunch Window",
            StartTime: new TimeOnly(10, 0),
            EndTime: new TimeOnly(14, 0),
            DaysOfWeek: new List<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday },
            ItemDocumentIds: new List<string> { "eggs-benedict" }));

        // Assert
        var windows = await grain.GetAvailabilityWindowsAsync();
        windows.Should().HaveCount(2);
        windows.Should().Contain(w => w.Name == "Breakfast Window");
        windows.Should().Contain(w => w.Name == "Brunch Window");
    }

    // Given: a happy hour promotion running every day of the week
    // When: an availability window is created for all 7 days (4 PM - 7 PM) with 2 items
    // Then: the window includes all 7 days and both item document IDs
    [Fact]
    public async Task AvailabilityWindow_AllWeekWindow_ShouldIncludeAllDays()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetOverridesGrain(orgId, siteId);

        // Act - Happy hour every day
        var window = await grain.AddAvailabilityWindowAsync(new AddAvailabilityWindowCommand(
            Name: "Happy Hour",
            StartTime: new TimeOnly(16, 0),
            EndTime: new TimeOnly(19, 0),
            DaysOfWeek: new List<DayOfWeek>
            {
                DayOfWeek.Sunday,
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
                DayOfWeek.Saturday
            },
            ItemDocumentIds: new List<string> { "half-price-apps", "discount-drinks" }));

        // Assert
        window.DaysOfWeek.Should().HaveCount(7);
        window.ItemDocumentIds.Should().HaveCount(2);
    }

    // Given: two availability windows configured for a site
    // When: the first window is removed
    // Then: only the second window remains
    [Fact]
    public async Task AvailabilityWindow_RemoveWindow_ShouldDeleteCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetOverridesGrain(orgId, siteId);

        var window1 = await grain.AddAvailabilityWindowAsync(new AddAvailabilityWindowCommand(
            Name: "Window 1",
            StartTime: new TimeOnly(9, 0),
            EndTime: new TimeOnly(12, 0),
            DaysOfWeek: new List<DayOfWeek> { DayOfWeek.Monday }));

        var window2 = await grain.AddAvailabilityWindowAsync(new AddAvailabilityWindowCommand(
            Name: "Window 2",
            StartTime: new TimeOnly(14, 0),
            EndTime: new TimeOnly(17, 0),
            DaysOfWeek: new List<DayOfWeek> { DayOfWeek.Tuesday }));

        // Act
        await grain.RemoveAvailabilityWindowAsync(window1.WindowId);

        // Assert
        var windows = await grain.GetAvailabilityWindowsAsync();
        windows.Should().ContainSingle();
        windows[0].Name.Should().Be("Window 2");
    }

    // Given: a weekday lunch specials window
    // When: the window is created with both specific item IDs and a category ID
    // Then: the window tracks both individual items and the category for availability filtering
    [Fact]
    public async Task AvailabilityWindow_CategoryAndItemsCombined_ShouldTrackBoth()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetOverridesGrain(orgId, siteId);

        // Act - Window with both category and specific items
        var window = await grain.AddAvailabilityWindowAsync(new AddAvailabilityWindowCommand(
            Name: "Lunch Specials",
            StartTime: new TimeOnly(11, 0),
            EndTime: new TimeOnly(15, 0),
            DaysOfWeek: new List<DayOfWeek>
            {
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            },
            ItemDocumentIds: new List<string> { "special-item-1", "special-item-2" },
            CategoryDocumentIds: new List<string> { "lunch-category" }));

        // Assert
        window.ItemDocumentIds.Should().HaveCount(2);
        window.CategoryDocumentIds.Should().ContainSingle();
        window.CategoryDocumentIds.Should().Contain("lunch-category");
    }

    // ============================================================================
    // MenuItem Variation Pricing Tests
    // ============================================================================

    // Given: a "Draft Beer" menu item with inventory tracking enabled
    // When: three variations (Half Pint $4, Pint $6, Pitcher $18) are added with different SKUs
    // Then: all three variations are stored with their distinct prices and display order
    [Fact]
    public async Task MenuItem_MultipleVariationsWithDifferentPrices_ShouldTrackAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = GetMenuItemGrain(orgId, itemId);

        await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: categoryId,
            AccountingGroupId: null,
            RecipeId: null,
            Name: "Draft Beer",
            Description: "Local craft beers on tap",
            Price: 6.00m, // Base price
            ImageUrl: null,
            Sku: "BEER-DRAFT",
            TrackInventory: true));

        // Act - Add variations with different prices
        var halfPint = await grain.AddVariationAsync(new CreateMenuItemVariationCommand(
            Name: "Half Pint",
            PricingType: PricingType.Fixed,
            Price: 4.00m,
            Sku: "BEER-DRAFT-HP",
            DisplayOrder: 1));

        var pint = await grain.AddVariationAsync(new CreateMenuItemVariationCommand(
            Name: "Pint",
            PricingType: PricingType.Fixed,
            Price: 6.00m,
            Sku: "BEER-DRAFT-PT",
            DisplayOrder: 2));

        var pitcher = await grain.AddVariationAsync(new CreateMenuItemVariationCommand(
            Name: "Pitcher",
            PricingType: PricingType.Fixed,
            Price: 18.00m,
            Sku: "BEER-DRAFT-PI",
            DisplayOrder: 3));

        // Assert
        var variations = await grain.GetVariationsAsync();
        variations.Should().HaveCount(3);
        variations.Should().Contain(v => v.Name == "Half Pint" && v.Price == 4.00m);
        variations.Should().Contain(v => v.Name == "Pint" && v.Price == 6.00m);
        variations.Should().Contain(v => v.Name == "Pitcher" && v.Price == 18.00m);
    }

    // Given: an "Open Food Item" where the price is entered at time of sale
    // When: a "Market Price" variation is added with Variable pricing type and no fixed price
    // Then: the variation stores PricingType.Variable with a null price
    [Fact]
    public async Task MenuItem_VariationWithVariablePricing_ShouldAllowOpenPrice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var grain = GetMenuItemGrain(orgId, itemId);

        await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: null,
            RecipeId: null,
            Name: "Open Food Item",
            Description: "Price entered at time of sale",
            Price: 0m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        // Act - Add variable price variation
        var openPrice = await grain.AddVariationAsync(new CreateMenuItemVariationCommand(
            Name: "Market Price",
            PricingType: PricingType.Variable,
            Price: null, // No fixed price
            Sku: null,
            DisplayOrder: 1));

        // Assert
        openPrice.PricingType.Should().Be(PricingType.Variable);
        openPrice.Price.Should().BeNull();
    }

    // Given: a "Coffee" item with Small, Medium, and Large variations
    // When: the Medium variation is deactivated
    // Then: Medium is marked inactive while Small and Large remain active (2 active variations)
    [Fact]
    public async Task MenuItem_DeactivateVariation_ShouldMarkAsInactive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var grain = GetMenuItemGrain(orgId, itemId);

        await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: null,
            RecipeId: null,
            Name: "Coffee",
            Description: null,
            Price: 3.00m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        var small = await grain.AddVariationAsync(new CreateMenuItemVariationCommand(
            Name: "Small", PricingType: PricingType.Fixed, Price: 2.50m, Sku: null, DisplayOrder: 1));
        var medium = await grain.AddVariationAsync(new CreateMenuItemVariationCommand(
            Name: "Medium", PricingType: PricingType.Fixed, Price: 3.00m, Sku: null, DisplayOrder: 2));
        var large = await grain.AddVariationAsync(new CreateMenuItemVariationCommand(
            Name: "Large", PricingType: PricingType.Fixed, Price: 3.50m, Sku: null, DisplayOrder: 3));

        // Act - Deactivate medium size
        await grain.UpdateVariationAsync(medium.VariationId, new UpdateMenuItemVariationCommand(
            IsActive: false));

        // Assert
        var variations = await grain.GetVariationsAsync();
        var mediumVariation = variations.First(v => v.Name == "Medium");
        mediumVariation.IsActive.Should().BeFalse();

        var activeVariations = variations.Where(v => v.IsActive).ToList();
        activeVariations.Should().HaveCount(2);
    }

    // ============================================================================
    // Draft/Published State Transition Tests
    // ============================================================================

    // Given: a menu item document created as a draft (not published immediately)
    // When: the draft is published, a new draft is created with updated content, and then published again
    // Then: the document transitions through draft->v1->draft->v2 with correct version numbers at each step
    [Fact]
    public async Task MenuItemDocument_FullPublishWorkflow_ShouldTransitionCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetItemDocumentGrain(orgId, documentId);

        // Act & Assert - Create as draft
        var created = await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "New Item",
            Price: 10.00m,
            PublishImmediately: false));

        created.PublishedVersion.Should().BeNull();
        created.DraftVersion.Should().Be(1);
        created.Published.Should().BeNull();
        created.Draft.Should().NotBeNull();

        // Act - Publish the draft
        await grain.PublishDraftAsync(note: "Initial publication");

        var afterPublish = await grain.GetSnapshotAsync();
        afterPublish.PublishedVersion.Should().Be(1);
        afterPublish.DraftVersion.Should().BeNull();
        afterPublish.Published.Should().NotBeNull();

        // Act - Create new draft
        await grain.CreateDraftAsync(new CreateMenuItemDraftCommand(
            Name: "Updated Item",
            Price: 12.00m,
            ChangeNote: "Price increase"));

        var withDraft = await grain.GetSnapshotAsync();
        withDraft.PublishedVersion.Should().Be(1);
        withDraft.DraftVersion.Should().Be(2);
        withDraft.Published!.Name.Should().Be("New Item");
        withDraft.Draft!.Name.Should().Be("Updated Item");

        // Act - Publish new draft
        await grain.PublishDraftAsync();

        var finalState = await grain.GetSnapshotAsync();
        finalState.PublishedVersion.Should().Be(2);
        finalState.DraftVersion.Should().BeNull();
        finalState.Published!.Name.Should().Be("Updated Item");
        finalState.Published.Price.Should().Be(12.00m);
    }

    // Given: a published menu item "Original" at $10.00 with an unpublished draft "Bad Draft" at $999.99
    // When: the draft is discarded
    // Then: the draft is removed and the published version remains unchanged at "Original" $10.00
    [Fact]
    public async Task MenuItemDocument_DiscardDraft_ShouldRevertToPreviousPublished()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetItemDocumentGrain(orgId, documentId);

        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Original",
            Price: 10.00m,
            PublishImmediately: true));

        await grain.CreateDraftAsync(new CreateMenuItemDraftCommand(
            Name: "Bad Draft",
            Price: 999.99m,
            ChangeNote: "This will be discarded"));

        // Act
        await grain.DiscardDraftAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.DraftVersion.Should().BeNull();
        snapshot.Draft.Should().BeNull();
        snapshot.PublishedVersion.Should().Be(1);
        snapshot.Published!.Name.Should().Be("Original");
        snapshot.Published.Price.Should().Be(10.00m);
        snapshot.TotalVersions.Should().Be(1); // Draft was removed
    }

    // Given: a menu item document with 3 published versions (Original $10, Updated $15, Final $20)
    // When: the document is reverted to version 1 due to a customer complaint
    // Then: a new version 4 is created with version 1's content, preserving full version history
    [Fact]
    public async Task MenuItemDocument_RevertToOlderVersion_ShouldCreateNewVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetItemDocumentGrain(orgId, documentId);

        // Create version history
        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Version 1 - Original",
            Price: 10.00m,
            PublishImmediately: true));

        await grain.CreateDraftAsync(new CreateMenuItemDraftCommand(
            Name: "Version 2 - Updated",
            Price: 15.00m));
        await grain.PublishDraftAsync();

        await grain.CreateDraftAsync(new CreateMenuItemDraftCommand(
            Name: "Version 3 - Final",
            Price: 20.00m));
        await grain.PublishDraftAsync();

        // Act - Revert to version 1
        await grain.RevertToVersionAsync(1, reason: "Customer complaint, reverting to original");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PublishedVersion.Should().Be(4); // New version created from revert
        snapshot.Published!.Name.Should().Be("Version 1 - Original");
        snapshot.Published.Price.Should().Be(10.00m);
        snapshot.TotalVersions.Should().Be(4);

        // Version history should show all versions
        var history = await grain.GetVersionHistoryAsync();
        history.Should().HaveCount(4);
    }

    // Given: a published category with items A, B, C in their original order
    // When: the items are reordered to C, A, B
    // Then: the published category reflects the new item display order
    [Fact]
    public async Task MenuCategoryDocument_DraftWithItemReordering_ShouldPreserveOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetCategoryDocumentGrain(orgId, documentId);

        await grain.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Test Category",
            DisplayOrder: 1,
            PublishImmediately: true));

        // Add items
        await grain.AddItemAsync("item-a");
        await grain.AddItemAsync("item-b");
        await grain.AddItemAsync("item-c");

        // Act - Reorder items
        await grain.ReorderItemsAsync(new List<string> { "item-c", "item-a", "item-b" });

        // Assert
        var published = await grain.GetPublishedAsync();
        published!.ItemDocumentIds[0].Should().Be("item-c");
        published.ItemDocumentIds[1].Should().Be("item-a");
        published.ItemDocumentIds[2].Should().Be("item-b");
    }

    // Given: a published menu item with version 2 containing holiday pricing
    // When: version 2 is scheduled to activate 7 days in the future as "Holiday Sale"
    // Then: a schedule entry is created with the activation date, version, and name
    [Fact]
    public async Task MenuItemDocument_ScheduledPublish_ShouldCreateScheduleEntry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetItemDocumentGrain(orgId, documentId);

        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Current Item",
            Price: 10.00m,
            PublishImmediately: true));

        await grain.CreateDraftAsync(new CreateMenuItemDraftCommand(
            Name: "Future Item",
            Price: 8.00m,
            ChangeNote: "Holiday pricing"));
        await grain.PublishDraftAsync();

        var futureDate = DateTimeOffset.UtcNow.AddDays(7);

        // Act - Schedule version 2 to activate in the future
        var schedule = await grain.ScheduleChangeAsync(
            version: 2,
            activateAt: futureDate,
            name: "Holiday Sale");

        // Assert
        schedule.VersionToActivate.Should().Be(2);
        schedule.ActivateAt.Should().Be(futureDate);
        schedule.Name.Should().Be("Holiday Sale");
        schedule.IsActive.Should().BeTrue();

        var schedules = await grain.GetSchedulesAsync();
        schedules.Should().ContainSingle();
    }

    // Given: a menu item with a scheduled version activation
    // When: the schedule is cancelled
    // Then: the schedule entry is removed and no pending schedules remain
    [Fact]
    public async Task MenuItemDocument_CancelSchedule_ShouldRemoveScheduleEntry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetItemDocumentGrain(orgId, documentId);

        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Item",
            Price: 10.00m,
            PublishImmediately: true));

        var schedule = await grain.ScheduleChangeAsync(
            version: 1,
            activateAt: DateTimeOffset.UtcNow.AddDays(1),
            name: "To Cancel");

        // Act
        await grain.CancelScheduleAsync(schedule.ScheduleId);

        // Assert
        var schedules = await grain.GetSchedulesAsync();
        schedules.Should().BeEmpty();
    }

    // Given: a published modifier block with no pending draft
    // When: an attempt is made to publish a draft
    // Then: an InvalidOperationException is thrown indicating there is no draft to publish
    [Fact]
    public async Task ModifierBlock_PublishDraftWithoutDraft_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetModifierBlockGrain(orgId, blockId);

        await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "No Draft Block",
            SelectionRule: ModifierSelectionRule.ChooseOne,
            Options:
            [
                new CreateModifierOptionCommand("Option", 0m, true, 1)
            ],
            PublishImmediately: true));

        // Act & Assert
        var action = () => grain.PublishDraftAsync();
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No draft to publish*");
    }

    // Given: a published menu item
    // When: the item is archived as "Discontinued product" and then restored
    // Then: the item transitions from archived=true back to archived=false
    [Fact]
    public async Task MenuItemDocument_ArchiveAndRestore_ShouldToggleArchiveState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetItemDocumentGrain(orgId, documentId);

        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Archivable Item",
            Price: 10.00m,
            PublishImmediately: true));

        // Act - Archive
        await grain.ArchiveAsync(reason: "Discontinued product");

        var afterArchive = await grain.GetSnapshotAsync();
        afterArchive.IsArchived.Should().BeTrue();

        // Act - Restore
        await grain.RestoreAsync();

        var afterRestore = await grain.GetSnapshotAsync();
        afterRestore.IsArchived.Should().BeFalse();
    }
}
