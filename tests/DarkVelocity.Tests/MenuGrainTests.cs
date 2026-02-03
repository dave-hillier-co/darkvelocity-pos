using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MenuCategoryGrainTests
{
    private readonly TestClusterFixture _fixture;

    public MenuCategoryGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IMenuCategoryGrain GetGrain(Guid orgId, Guid categoryId)
    {
        var key = $"{orgId}:menucategory:{categoryId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IMenuCategoryGrain>(key);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, categoryId);

        // Act
        var result = await grain.CreateAsync(new CreateMenuCategoryCommand(
            LocationId: locationId,
            Name: "Starters",
            Description: "Appetizers and small plates",
            DisplayOrder: 1,
            Color: "#FF5733"));

        // Assert
        result.CategoryId.Should().Be(categoryId);
        result.Name.Should().Be("Starters");
        result.Description.Should().Be("Appetizers and small plates");
        result.DisplayOrder.Should().Be(1);
        result.Color.Should().Be("#FF5733");
        result.IsActive.Should().BeTrue();
        result.ItemCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = GetGrain(orgId, categoryId);
        await grain.CreateAsync(new CreateMenuCategoryCommand(
            LocationId: Guid.NewGuid(),
            Name: "Starters",
            Description: "Appetizers",
            DisplayOrder: 1,
            Color: "#FF5733"));

        // Act
        var result = await grain.UpdateAsync(new UpdateMenuCategoryCommand(
            Name: "Appetizers",
            Description: "Start your meal right",
            DisplayOrder: 2,
            Color: "#00FF00",
            IsActive: null));

        // Assert
        result.Name.Should().Be("Appetizers");
        result.Description.Should().Be("Start your meal right");
        result.DisplayOrder.Should().Be(2);
        result.Color.Should().Be("#00FF00");
    }

    [Fact]
    public async Task DeactivateAsync_ShouldDeactivateCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = GetGrain(orgId, categoryId);
        await grain.CreateAsync(new CreateMenuCategoryCommand(
            LocationId: Guid.NewGuid(),
            Name: "Seasonal",
            Description: null,
            DisplayOrder: 10,
            Color: null));

        // Act
        await grain.DeactivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task IncrementItemCountAsync_ShouldIncreaseCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = GetGrain(orgId, categoryId);
        await grain.CreateAsync(new CreateMenuCategoryCommand(
            LocationId: Guid.NewGuid(),
            Name: "Mains",
            Description: null,
            DisplayOrder: 2,
            Color: null));

        // Act
        await grain.IncrementItemCountAsync();
        await grain.IncrementItemCountAsync();
        await grain.IncrementItemCountAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemCount.Should().Be(3);
    }

    [Fact]
    public async Task DecrementItemCountAsync_ShouldDecreaseCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = GetGrain(orgId, categoryId);
        await grain.CreateAsync(new CreateMenuCategoryCommand(
            LocationId: Guid.NewGuid(),
            Name: "Desserts",
            Description: null,
            DisplayOrder: 5,
            Color: null));
        await grain.IncrementItemCountAsync();
        await grain.IncrementItemCountAsync();

        // Act
        await grain.DecrementItemCountAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemCount.Should().Be(1);
    }

    [Fact]
    public async Task DecrementItemCountAsync_AtZero_ShouldRemainZero()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = GetGrain(orgId, categoryId);
        await grain.CreateAsync(new CreateMenuCategoryCommand(
            LocationId: Guid.NewGuid(),
            Name: "Empty Category",
            Description: null,
            DisplayOrder: 99,
            Color: null));

        // Act
        await grain.DecrementItemCountAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemCount.Should().Be(0);
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MenuItemGrainTests
{
    private readonly TestClusterFixture _fixture;

    public MenuItemGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IMenuItemGrain GetGrain(Guid orgId, Guid itemId)
    {
        var key = $"{orgId}:menuitem:{itemId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IMenuItemGrain>(key);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateMenuItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = GetGrain(orgId, itemId);

        // Act
        var result = await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: locationId,
            CategoryId: categoryId,
            AccountingGroupId: null,
            RecipeId: null,
            Name: "Caesar Salad",
            Description: "Crisp romaine with house-made dressing",
            Price: 12.99m,
            ImageUrl: "https://example.com/caesar.jpg",
            Sku: "SAL-001",
            TrackInventory: true));

        // Assert
        result.MenuItemId.Should().Be(itemId);
        result.Name.Should().Be("Caesar Salad");
        result.Description.Should().Be("Crisp romaine with house-made dressing");
        result.Price.Should().Be(12.99m);
        result.Sku.Should().Be("SAL-001");
        result.IsActive.Should().BeTrue();
        result.TrackInventory.Should().BeTrue();
        result.Modifiers.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateMenuItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var grain = GetGrain(orgId, itemId);
        await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: null,
            RecipeId: null,
            Name: "House Burger",
            Description: "Our signature burger",
            Price: 14.99m,
            ImageUrl: null,
            Sku: "BUR-001",
            TrackInventory: false));

        // Act
        var result = await grain.UpdateAsync(new UpdateMenuItemCommand(
            CategoryId: null,
            AccountingGroupId: null,
            RecipeId: null,
            Name: "Classic Burger",
            Description: "Beef patty with all the fixings",
            Price: 15.99m,
            ImageUrl: null,
            Sku: null,
            IsActive: null,
            TrackInventory: true));

        // Assert
        result.Name.Should().Be("Classic Burger");
        result.Description.Should().Be("Beef patty with all the fixings");
        result.Price.Should().Be(15.99m);
        result.TrackInventory.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateAsync_ShouldDeactivateItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var grain = GetGrain(orgId, itemId);
        await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: null,
            RecipeId: null,
            Name: "Seasonal Special",
            Description: null,
            Price: 18.99m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        // Act
        await grain.DeactivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetPriceAsync_ShouldReturnPrice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var grain = GetGrain(orgId, itemId);
        await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: null,
            RecipeId: null,
            Name: "Fish & Chips",
            Description: null,
            Price: 16.50m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        // Act
        var price = await grain.GetPriceAsync();

        // Assert
        price.Should().Be(16.50m);
    }

    [Fact]
    public async Task AddModifierAsync_ShouldAddModifierGroup()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var grain = GetGrain(orgId, itemId);
        await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: null,
            RecipeId: null,
            Name: "Coffee",
            Description: null,
            Price: 3.50m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        var modifier = new MenuItemModifier(
            ModifierId: Guid.NewGuid(),
            Name: "Size",
            PriceAdjustment: 0,
            IsRequired: true,
            MinSelections: 1,
            MaxSelections: 1,
            Options: new List<MenuItemModifierOption>
            {
                new(Guid.NewGuid(), "Small", 0m, true),
                new(Guid.NewGuid(), "Medium", 0.50m, false),
                new(Guid.NewGuid(), "Large", 1.00m, false)
            });

        // Act
        await grain.AddModifierAsync(modifier);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Modifiers.Should().HaveCount(1);
        snapshot.Modifiers[0].Name.Should().Be("Size");
        snapshot.Modifiers[0].IsRequired.Should().BeTrue();
        snapshot.Modifiers[0].Options.Should().HaveCount(3);
    }

    [Fact]
    public async Task AddModifierAsync_MultipleModifiers_ShouldAddAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var grain = GetGrain(orgId, itemId);
        await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: null,
            RecipeId: null,
            Name: "Pizza",
            Description: null,
            Price: 14.99m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: true));

        var sizeModifier = new MenuItemModifier(
            ModifierId: Guid.NewGuid(),
            Name: "Size",
            PriceAdjustment: 0,
            IsRequired: true,
            MinSelections: 1,
            MaxSelections: 1,
            Options: new List<MenuItemModifierOption>
            {
                new(Guid.NewGuid(), "Medium", 0m, true),
                new(Guid.NewGuid(), "Large", 4.00m, false)
            });

        var toppingsModifier = new MenuItemModifier(
            ModifierId: Guid.NewGuid(),
            Name: "Extra Toppings",
            PriceAdjustment: 0,
            IsRequired: false,
            MinSelections: 0,
            MaxSelections: 5,
            Options: new List<MenuItemModifierOption>
            {
                new(Guid.NewGuid(), "Pepperoni", 1.50m, false),
                new(Guid.NewGuid(), "Mushrooms", 1.00m, false),
                new(Guid.NewGuid(), "Olives", 1.00m, false),
                new(Guid.NewGuid(), "Extra Cheese", 2.00m, false)
            });

        // Act
        await grain.AddModifierAsync(sizeModifier);
        await grain.AddModifierAsync(toppingsModifier);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Modifiers.Should().HaveCount(2);
        snapshot.Modifiers.Should().Contain(m => m.Name == "Size" && m.IsRequired);
        snapshot.Modifiers.Should().Contain(m => m.Name == "Extra Toppings" && !m.IsRequired);
    }

    [Fact]
    public async Task AddModifierAsync_UpdateExisting_ShouldReplaceModifier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var grain = GetGrain(orgId, itemId);
        await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: null,
            RecipeId: null,
            Name: "Sandwich",
            Description: null,
            Price: 9.99m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        var originalModifier = new MenuItemModifier(
            ModifierId: modifierId,
            Name: "Bread",
            PriceAdjustment: 0,
            IsRequired: true,
            MinSelections: 1,
            MaxSelections: 1,
            Options: new List<MenuItemModifierOption>
            {
                new(Guid.NewGuid(), "White", 0m, true),
                new(Guid.NewGuid(), "Wheat", 0m, false)
            });

        await grain.AddModifierAsync(originalModifier);

        var updatedModifier = new MenuItemModifier(
            ModifierId: modifierId,
            Name: "Bread Type",
            PriceAdjustment: 0,
            IsRequired: true,
            MinSelections: 1,
            MaxSelections: 1,
            Options: new List<MenuItemModifierOption>
            {
                new(Guid.NewGuid(), "White", 0m, true),
                new(Guid.NewGuid(), "Wheat", 0m, false),
                new(Guid.NewGuid(), "Sourdough", 0.50m, false)
            });

        // Act
        await grain.AddModifierAsync(updatedModifier);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Modifiers.Should().HaveCount(1);
        snapshot.Modifiers[0].Name.Should().Be("Bread Type");
        snapshot.Modifiers[0].Options.Should().HaveCount(3);
    }

    [Fact]
    public async Task RemoveModifierAsync_ShouldRemoveModifier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var grain = GetGrain(orgId, itemId);
        await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: null,
            RecipeId: null,
            Name: "Steak",
            Description: null,
            Price: 29.99m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: true));

        await grain.AddModifierAsync(new MenuItemModifier(
            ModifierId: modifierId,
            Name: "Temperature",
            PriceAdjustment: 0,
            IsRequired: true,
            MinSelections: 1,
            MaxSelections: 1,
            Options: new List<MenuItemModifierOption>
            {
                new(Guid.NewGuid(), "Rare", 0m, false),
                new(Guid.NewGuid(), "Medium", 0m, true),
                new(Guid.NewGuid(), "Well Done", 0m, false)
            }));

        // Act
        await grain.RemoveModifierAsync(modifierId);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Modifiers.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateCostAsync_ShouldUpdateTheoreticalCost()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var grain = GetGrain(orgId, itemId);
        await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: null,
            RecipeId: Guid.NewGuid(),
            Name: "Pasta Carbonara",
            Description: null,
            Price: 18.99m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: true));

        // Act
        await grain.UpdateCostAsync(5.75m);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TheoreticalCost.Should().Be(5.75m);
        snapshot.CostPercent.Should().BeApproximately(30.28m, 0.01m);
    }

    [Fact]
    public async Task CreateAsync_WithRecipe_ShouldLinkRecipe()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var grain = GetGrain(orgId, itemId);

        // Act
        var result = await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: Guid.NewGuid(),
            RecipeId: recipeId,
            Name: "Chicken Parmesan",
            Description: null,
            Price: 22.99m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: true));

        // Assert
        result.RecipeId.Should().Be(recipeId);
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MenuDefinitionGrainTests
{
    private readonly TestClusterFixture _fixture;

    public MenuDefinitionGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IMenuDefinitionGrain GetGrain(Guid orgId, Guid menuId)
    {
        var key = $"{orgId}:menudef:{menuId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IMenuDefinitionGrain>(key);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateMenuDefinition()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, menuId);

        // Act
        var result = await grain.CreateAsync(new CreateMenuDefinitionCommand(
            LocationId: locationId,
            Name: "Main POS Menu",
            Description: "Primary menu for dine-in orders",
            IsDefault: true));

        // Assert
        result.MenuId.Should().Be(menuId);
        result.Name.Should().Be("Main POS Menu");
        result.Description.Should().Be("Primary menu for dine-in orders");
        result.IsDefault.Should().BeTrue();
        result.IsActive.Should().BeTrue();
        result.Screens.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateMenuDefinition()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var grain = GetGrain(orgId, menuId);
        await grain.CreateAsync(new CreateMenuDefinitionCommand(
            LocationId: Guid.NewGuid(),
            Name: "Bar Menu",
            Description: null,
            IsDefault: false));

        // Act
        var result = await grain.UpdateAsync(new UpdateMenuDefinitionCommand(
            Name: "Bar & Lounge Menu",
            Description: "For bar service area",
            IsDefault: null,
            IsActive: null));

        // Assert
        result.Name.Should().Be("Bar & Lounge Menu");
        result.Description.Should().Be("For bar service area");
    }

    [Fact]
    public async Task AddScreenAsync_ShouldAddScreen()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var screenId = Guid.NewGuid();
        var grain = GetGrain(orgId, menuId);
        await grain.CreateAsync(new CreateMenuDefinitionCommand(
            LocationId: Guid.NewGuid(),
            Name: "Quick Service Menu",
            Description: null,
            IsDefault: true));

        var screen = new MenuScreenDefinition(
            ScreenId: screenId,
            Name: "Main Screen",
            Position: 1,
            Color: "#FFFFFF",
            Rows: 4,
            Columns: 6,
            Buttons: new List<MenuButtonDefinition>());

        // Act
        await grain.AddScreenAsync(screen);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Screens.Should().HaveCount(1);
        snapshot.Screens[0].Name.Should().Be("Main Screen");
        snapshot.Screens[0].Rows.Should().Be(4);
        snapshot.Screens[0].Columns.Should().Be(6);
    }

    [Fact]
    public async Task AddScreenAsync_WithButtons_ShouldAddScreenWithButtons()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var screenId = Guid.NewGuid();
        var menuItemId = Guid.NewGuid();
        var grain = GetGrain(orgId, menuId);
        await grain.CreateAsync(new CreateMenuDefinitionCommand(
            LocationId: Guid.NewGuid(),
            Name: "Test Menu",
            Description: null,
            IsDefault: false));

        var screen = new MenuScreenDefinition(
            ScreenId: screenId,
            Name: "Drinks",
            Position: 1,
            Color: "#0000FF",
            Rows: 3,
            Columns: 4,
            Buttons: new List<MenuButtonDefinition>
            {
                new(Guid.NewGuid(), menuItemId, null, 0, 0, "Coffee", "#8B4513", "Item"),
                new(Guid.NewGuid(), Guid.NewGuid(), null, 0, 1, "Tea", "#228B22", "Item"),
                new(Guid.NewGuid(), Guid.NewGuid(), null, 0, 2, "Soda", "#FF0000", "Item")
            });

        // Act
        await grain.AddScreenAsync(screen);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Screens[0].Buttons.Should().HaveCount(3);
        snapshot.Screens[0].Buttons[0].Label.Should().Be("Coffee");
        snapshot.Screens[0].Buttons[0].MenuItemId.Should().Be(menuItemId);
    }

    [Fact]
    public async Task UpdateScreenAsync_ShouldUpdateScreen()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var screenId = Guid.NewGuid();
        var grain = GetGrain(orgId, menuId);
        await grain.CreateAsync(new CreateMenuDefinitionCommand(
            LocationId: Guid.NewGuid(),
            Name: "Test Menu",
            Description: null,
            IsDefault: false));
        await grain.AddScreenAsync(new MenuScreenDefinition(
            ScreenId: screenId,
            Name: "Screen 1",
            Position: 1,
            Color: "#FFFFFF",
            Rows: 3,
            Columns: 4,
            Buttons: new List<MenuButtonDefinition>()));

        // Act
        await grain.UpdateScreenAsync(screenId, "Food Screen", "#FFFACD", 5, 8);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Screens[0].Name.Should().Be("Food Screen");
        snapshot.Screens[0].Color.Should().Be("#FFFACD");
        snapshot.Screens[0].Rows.Should().Be(5);
        snapshot.Screens[0].Columns.Should().Be(8);
    }

    [Fact]
    public async Task RemoveScreenAsync_ShouldRemoveScreen()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var screenId1 = Guid.NewGuid();
        var screenId2 = Guid.NewGuid();
        var grain = GetGrain(orgId, menuId);
        await grain.CreateAsync(new CreateMenuDefinitionCommand(
            LocationId: Guid.NewGuid(),
            Name: "Test Menu",
            Description: null,
            IsDefault: false));
        await grain.AddScreenAsync(new MenuScreenDefinition(
            screenId1, "Screen 1", 1, null, 3, 4, new List<MenuButtonDefinition>()));
        await grain.AddScreenAsync(new MenuScreenDefinition(
            screenId2, "Screen 2", 2, null, 3, 4, new List<MenuButtonDefinition>()));

        // Act
        await grain.RemoveScreenAsync(screenId1);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Screens.Should().HaveCount(1);
        snapshot.Screens[0].Name.Should().Be("Screen 2");
    }

    [Fact]
    public async Task AddButtonAsync_ShouldAddButtonToScreen()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var screenId = Guid.NewGuid();
        var menuItemId = Guid.NewGuid();
        var grain = GetGrain(orgId, menuId);
        await grain.CreateAsync(new CreateMenuDefinitionCommand(
            LocationId: Guid.NewGuid(),
            Name: "Test Menu",
            Description: null,
            IsDefault: false));
        await grain.AddScreenAsync(new MenuScreenDefinition(
            screenId, "Main", 1, null, 4, 6, new List<MenuButtonDefinition>()));

        var button = new MenuButtonDefinition(
            ButtonId: Guid.NewGuid(),
            MenuItemId: menuItemId,
            SubScreenId: null,
            Row: 1,
            Column: 2,
            Label: "Burger",
            Color: "#FF6B35",
            ButtonType: "Item");

        // Act
        await grain.AddButtonAsync(screenId, button);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Screens[0].Buttons.Should().HaveCount(1);
        snapshot.Screens[0].Buttons[0].Label.Should().Be("Burger");
        snapshot.Screens[0].Buttons[0].Row.Should().Be(1);
        snapshot.Screens[0].Buttons[0].Column.Should().Be(2);
    }

    [Fact]
    public async Task AddButtonAsync_NavigationButton_ShouldLinkToSubScreen()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var mainScreenId = Guid.NewGuid();
        var subScreenId = Guid.NewGuid();
        var grain = GetGrain(orgId, menuId);
        await grain.CreateAsync(new CreateMenuDefinitionCommand(
            LocationId: Guid.NewGuid(),
            Name: "Test Menu",
            Description: null,
            IsDefault: false));
        await grain.AddScreenAsync(new MenuScreenDefinition(
            mainScreenId, "Main", 1, null, 4, 6, new List<MenuButtonDefinition>()));
        await grain.AddScreenAsync(new MenuScreenDefinition(
            subScreenId, "Drinks", 2, null, 3, 4, new List<MenuButtonDefinition>()));

        var navButton = new MenuButtonDefinition(
            ButtonId: Guid.NewGuid(),
            MenuItemId: null,
            SubScreenId: subScreenId,
            Row: 0,
            Column: 0,
            Label: "Drinks",
            Color: "#0066CC",
            ButtonType: "Navigation");

        // Act
        await grain.AddButtonAsync(mainScreenId, navButton);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        var mainScreen = snapshot.Screens.First(s => s.ScreenId == mainScreenId);
        mainScreen.Buttons[0].SubScreenId.Should().Be(subScreenId);
        mainScreen.Buttons[0].ButtonType.Should().Be("Navigation");
    }

    [Fact]
    public async Task RemoveButtonAsync_ShouldRemoveButton()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var screenId = Guid.NewGuid();
        var buttonId1 = Guid.NewGuid();
        var buttonId2 = Guid.NewGuid();
        var grain = GetGrain(orgId, menuId);
        await grain.CreateAsync(new CreateMenuDefinitionCommand(
            LocationId: Guid.NewGuid(),
            Name: "Test Menu",
            Description: null,
            IsDefault: false));
        await grain.AddScreenAsync(new MenuScreenDefinition(
            screenId, "Main", 1, null, 4, 6, new List<MenuButtonDefinition>()));
        await grain.AddButtonAsync(screenId, new MenuButtonDefinition(
            buttonId1, Guid.NewGuid(), null, 0, 0, "Item 1", null, "Item"));
        await grain.AddButtonAsync(screenId, new MenuButtonDefinition(
            buttonId2, Guid.NewGuid(), null, 0, 1, "Item 2", null, "Item"));

        // Act
        await grain.RemoveButtonAsync(screenId, buttonId1);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Screens[0].Buttons.Should().HaveCount(1);
        snapshot.Screens[0].Buttons[0].Label.Should().Be("Item 2");
    }

    [Fact]
    public async Task SetAsDefaultAsync_ShouldSetAsDefault()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var grain = GetGrain(orgId, menuId);
        await grain.CreateAsync(new CreateMenuDefinitionCommand(
            LocationId: Guid.NewGuid(),
            Name: "Secondary Menu",
            Description: null,
            IsDefault: false));

        // Act
        await grain.SetAsDefaultAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsDefault.Should().BeTrue();
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class AccountingGroupGrainTests
{
    private readonly TestClusterFixture _fixture;

    public AccountingGroupGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IAccountingGroupGrain GetGrain(Guid orgId, Guid groupId)
    {
        var key = $"{orgId}:accountinggroup:{groupId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(key);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateAccountingGroup()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, groupId);

        // Act
        var result = await grain.CreateAsync(new CreateAccountingGroupCommand(
            LocationId: locationId,
            Name: "Food Sales",
            Code: "4100",
            Description: "Revenue from food sales",
            RevenueAccountCode: "4100-001",
            CogsAccountCode: "5100-001"));

        // Assert
        result.AccountingGroupId.Should().Be(groupId);
        result.Name.Should().Be("Food Sales");
        result.Code.Should().Be("4100");
        result.Description.Should().Be("Revenue from food sales");
        result.RevenueAccountCode.Should().Be("4100-001");
        result.CogsAccountCode.Should().Be("5100-001");
        result.IsActive.Should().BeTrue();
        result.ItemCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateAccountingGroup()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var grain = GetGrain(orgId, groupId);
        await grain.CreateAsync(new CreateAccountingGroupCommand(
            LocationId: Guid.NewGuid(),
            Name: "Beverages",
            Code: "4200",
            Description: null,
            RevenueAccountCode: "4200-001",
            CogsAccountCode: "5200-001"));

        // Act
        var result = await grain.UpdateAsync(new UpdateAccountingGroupCommand(
            Name: "Beverage Sales",
            Code: null,
            Description: "All drink revenue",
            RevenueAccountCode: null,
            CogsAccountCode: null,
            IsActive: null));

        // Assert
        result.Name.Should().Be("Beverage Sales");
        result.Description.Should().Be("All drink revenue");
        result.Code.Should().Be("4200"); // Unchanged
    }

    [Fact]
    public async Task IncrementItemCountAsync_ShouldIncreaseCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var grain = GetGrain(orgId, groupId);
        await grain.CreateAsync(new CreateAccountingGroupCommand(
            LocationId: Guid.NewGuid(),
            Name: "Merchandise",
            Code: "4300",
            Description: null,
            RevenueAccountCode: null,
            CogsAccountCode: null));

        // Act
        await grain.IncrementItemCountAsync();
        await grain.IncrementItemCountAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemCount.Should().Be(2);
    }

    [Fact]
    public async Task DecrementItemCountAsync_ShouldDecreaseCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var grain = GetGrain(orgId, groupId);
        await grain.CreateAsync(new CreateAccountingGroupCommand(
            LocationId: Guid.NewGuid(),
            Name: "Alcohol",
            Code: "4400",
            Description: null,
            RevenueAccountCode: null,
            CogsAccountCode: null));
        await grain.IncrementItemCountAsync();
        await grain.IncrementItemCountAsync();
        await grain.IncrementItemCountAsync();

        // Act
        await grain.DecrementItemCountAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemCount.Should().Be(2);
    }

    [Fact]
    public async Task UpdateAsync_Deactivate_ShouldDeactivateGroup()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var grain = GetGrain(orgId, groupId);
        await grain.CreateAsync(new CreateAccountingGroupCommand(
            LocationId: Guid.NewGuid(),
            Name: "Discontinued",
            Code: "4900",
            Description: null,
            RevenueAccountCode: null,
            CogsAccountCode: null));

        // Act
        var result = await grain.UpdateAsync(new UpdateAccountingGroupCommand(
            Name: null,
            Code: null,
            Description: null,
            RevenueAccountCode: null,
            CogsAccountCode: null,
            IsActive: false));

        // Assert
        result.IsActive.Should().BeFalse();
    }
}
