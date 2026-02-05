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

    // Given: no existing menu category
    // When: a new "Starters" category is created with a display order, color, and description
    // Then: the category is active with the correct name, description, display order, color, and zero items
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

    // Given: an existing "Starters" menu category
    // When: the category name, description, display order, and color are updated
    // Then: the category reflects the new name "Appetizers", updated description, reordered position, and new color
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

    // Given: an active "Seasonal" menu category
    // When: the category is deactivated
    // Then: the category is marked as inactive
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

    // Given: a "Mains" category with zero items
    // When: three menu items are added to the category
    // Then: the category item count is 3
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

    // Given: a "Desserts" category with 2 items
    // When: one item is removed from the category
    // Then: the category item count decreases to 1
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

    // Given: an empty category with zero items
    // When: item count is decremented below zero
    // Then: the item count remains at zero and does not go negative
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

    // Given: no existing menu item
    // When: a "Caesar Salad" is created at $12.99 with inventory tracking and a SKU
    // Then: the item is active with correct name, price, SKU, inventory tracking enabled, and no modifiers
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

    // Given: an existing "House Burger" menu item at $14.99 without inventory tracking
    // When: the item name, description, price, and inventory tracking are updated
    // Then: the item reflects the new name "Classic Burger", updated price of $15.99, and inventory tracking enabled
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

    // Given: an active "Seasonal Special" menu item
    // When: the item is deactivated
    // Then: the item is marked as inactive
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

    // Given: a "Fish & Chips" menu item priced at $16.50
    // When: the item price is queried
    // Then: the returned price is $16.50
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

    // Given: a "Coffee" menu item with no modifiers
    // When: a required "Size" modifier with Small, Medium, and Large options is added
    // Then: the item has one modifier group with 3 options and the modifier is marked as required
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

    // Given: a "Pizza" menu item with no modifiers
    // When: a required "Size" modifier and an optional "Extra Toppings" modifier are both added
    // Then: the item has two modifier groups, one required and one optional
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

    // Given: a "Sandwich" menu item with a "Bread" modifier offering White and Wheat options
    // When: the same modifier is re-added with a new name "Bread Type" and an additional Sourdough option
    // Then: the modifier is replaced in-place with the updated name and 3 options
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

    // Given: a "Steak" menu item with a required "Temperature" modifier (Rare, Medium, Well Done)
    // When: the temperature modifier is removed
    // Then: the item has no modifiers
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

    // Given: a "Pasta Carbonara" priced at $18.99 with a linked recipe
    // When: the theoretical food cost is updated to $5.75
    // Then: the item shows a cost of $5.75 and a cost percentage of approximately 30.28%
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

    // Given: no existing menu item
    // When: a "Chicken Parmesan" is created with a linked recipe
    // Then: the item stores the recipe reference for cost tracking
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

    // Given: no existing menu item
    // When: a menu item is created with a negative price of -$5.00
    // Then: the operation is rejected with a validation error about negative prices
    [Fact]
    public async Task CreateAsync_WithNegativePrice_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var grain = GetGrain(orgId, itemId);

        // Act
        var action = () => grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: null,
            RecipeId: null,
            Name: "Invalid Item",
            Description: null,
            Price: -5.00m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        // Assert
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*negative*");
    }

    // Given: no existing menu item
    // When: a menu item is created with an empty name
    // Then: the operation is rejected with a validation error about empty names
    [Fact]
    public async Task CreateAsync_WithEmptyName_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var grain = GetGrain(orgId, itemId);

        // Act
        var action = () => grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: null,
            RecipeId: null,
            Name: "",
            Description: null,
            Price: 10.00m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        // Assert
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*empty*");
    }

    // Given: an existing menu item
    // When: a modifier group with zero options is added
    // Then: the operation is rejected because a modifier must have at least one option
    [Fact]
    public async Task AddModifierAsync_WithNoOptions_ShouldThrowException()
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
            Name: "Test Item",
            Description: null,
            Price: 10.00m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        var modifier = new MenuItemModifier(
            ModifierId: Guid.NewGuid(),
            Name: "Empty Modifier",
            PriceAdjustment: 0,
            IsRequired: false,
            MinSelections: 0,
            MaxSelections: 1,
            Options: new List<MenuItemModifierOption>());

        // Act
        var action = () => grain.AddModifierAsync(modifier);

        // Assert
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*at least one option*");
    }

    // Given: an existing menu item
    // When: a modifier is added with minimum selections (5) exceeding maximum selections (2)
    // Then: the operation is rejected because min selections cannot exceed max selections
    [Fact]
    public async Task AddModifierAsync_MinGreaterThanMax_ShouldThrowException()
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
            Name: "Test Item",
            Description: null,
            Price: 10.00m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        var modifier = new MenuItemModifier(
            ModifierId: Guid.NewGuid(),
            Name: "Invalid Modifier",
            PriceAdjustment: 0,
            IsRequired: true,
            MinSelections: 5,
            MaxSelections: 2,
            Options: new List<MenuItemModifierOption>
            {
                new(Guid.NewGuid(), "Option 1", 0m, false),
                new(Guid.NewGuid(), "Option 2", 0m, false)
            });

        // Act
        var action = () => grain.AddModifierAsync(modifier);

        // Assert
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*MinSelections*MaxSelections*");
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

    // Given: no existing POS menu definition
    // When: a new "Main POS Menu" is created as the default menu for dine-in orders
    // Then: the menu is active, set as default, and has no screens
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

    // Given: an existing "Bar Menu" definition
    // When: the menu name and description are updated
    // Then: the menu reflects the new name "Bar & Lounge Menu" and description
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

    // Given: a "Quick Service Menu" definition with no screens
    // When: a "Main Screen" with a 4x6 grid layout is added
    // Then: the menu has one screen with the correct name, rows, and columns
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

    // Given: a menu definition with no screens
    // When: a "Drinks" screen is added with 3 pre-configured item buttons (Coffee, Tea, Soda)
    // Then: the screen contains all 3 buttons with correct labels and linked menu item IDs
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

    // Given: a menu with a 3x4 screen named "Screen 1"
    // When: the screen name, color, and grid dimensions are updated to 5x8
    // Then: the screen reflects the new name "Food Screen", updated color, and expanded grid layout
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

    // Given: a menu with two screens ("Screen 1" and "Screen 2")
    // When: the first screen is removed
    // Then: only "Screen 2" remains in the menu
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

    // Given: a menu screen with a 4x6 grid and no buttons
    // When: a "Burger" item button is placed at row 1, column 2
    // Then: the screen has one button at the specified grid position with the correct label
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

    // Given: a menu with a main screen and a "Drinks" sub-screen
    // When: a navigation button linking to the drinks sub-screen is added to the main screen
    // Then: the button is typed as "Navigation" and references the drinks sub-screen ID
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

    // Given: a menu screen with two item buttons ("Item 1" and "Item 2")
    // When: the first button is removed
    // Then: only the "Item 2" button remains on the screen
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
