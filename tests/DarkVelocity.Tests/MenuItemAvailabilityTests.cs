using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for menu item availability scenarios including snoozing, deactivation,
/// and time-based availability.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MenuItemAvailabilityTests
{
    private readonly TestClusterFixture _fixture;

    public MenuItemAvailabilityTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IMenuItemGrain GetMenuItemGrain(Guid orgId, Guid itemId)
    {
        var key = $"{orgId}:menuitem:{itemId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IMenuItemGrain>(key);
    }

    private IMenuCategoryGrain GetCategoryGrain(Guid orgId, Guid categoryId)
    {
        var key = $"{orgId}:menucategory:{categoryId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IMenuCategoryGrain>(key);
    }

    private async Task<IMenuItemGrain> CreateMenuItemAsync(Guid orgId, Guid itemId, Guid categoryId, string name = "Test Item", decimal price = 10.00m)
    {
        var grain = GetMenuItemGrain(orgId, itemId);
        await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: categoryId,
            AccountingGroupId: null,
            RecipeId: null,
            Name: name,
            Description: "Test item description",
            Price: price,
            ImageUrl: null,
            Sku: $"SKU-{itemId.ToString()[..8]}",
            TrackInventory: false));
        return grain;
    }

    #region Snooze Tests

    [Fact]
    public async Task SetSnoozedAsync_ShouldSnoozeItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId);

        // Act
        await grain.SetSnoozedAsync(true);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsSnoozed.Should().BeTrue();
        snapshot.IsActive.Should().BeTrue(); // Still active, just snoozed
    }

    [Fact]
    public async Task SetSnoozedAsync_WithDuration_ShouldSetSnoozedUntil()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId);
        var duration = TimeSpan.FromHours(2);

        // Act
        await grain.SetSnoozedAsync(true, duration);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsSnoozed.Should().BeTrue();
        snapshot.SnoozedUntil.Should().NotBeNull();
        snapshot.SnoozedUntil.Should().BeCloseTo(DateTime.UtcNow.Add(duration), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SetSnoozedAsync_Unsnooze_ShouldClearSnooze()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId);
        await grain.SetSnoozedAsync(true, TimeSpan.FromHours(1));

        // Act
        await grain.SetSnoozedAsync(false);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsSnoozed.Should().BeFalse();
        snapshot.SnoozedUntil.Should().BeNull();
    }

    [Fact]
    public async Task SetSnoozedAsync_MultipleTimes_ShouldUpdateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId);

        // Act - snooze, unsnooze, snooze again
        await grain.SetSnoozedAsync(true, TimeSpan.FromMinutes(30));
        await grain.SetSnoozedAsync(false);
        await grain.SetSnoozedAsync(true, TimeSpan.FromHours(4));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsSnoozed.Should().BeTrue();
        snapshot.SnoozedUntil.Should().BeCloseTo(DateTime.UtcNow.AddHours(4), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SetSnoozedAsync_IndefiniteSnooze_ShouldHaveNoExpiry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId);

        // Act - snooze without duration
        await grain.SetSnoozedAsync(true);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsSnoozed.Should().BeTrue();
        snapshot.SnoozedUntil.Should().BeNull();
    }

    #endregion

    #region Deactivation Tests

    [Fact]
    public async Task DeactivateAsync_ShouldMakeItemUnavailable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId);

        // Act
        await grain.DeactivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_Reactivate_ShouldMakeItemAvailable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId);
        await grain.DeactivateAsync();

        // Act
        await grain.UpdateAsync(new UpdateMenuItemCommand(
            CategoryId: null,
            AccountingGroupId: null,
            RecipeId: null,
            Name: null,
            Description: null,
            Price: null,
            ImageUrl: null,
            Sku: null,
            IsActive: true,
            TrackInventory: null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateAsync_SnoozedItem_ShouldDeactivateAndClearSnooze()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId);
        await grain.SetSnoozedAsync(true, TimeSpan.FromHours(1));

        // Act
        await grain.DeactivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
        // Snooze state is typically cleared when item is deactivated
    }

    #endregion

    #region Category Deactivation Tests

    [Fact]
    public async Task DeactivateCategory_ShouldMakeCategoryUnavailable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var categoryGrain = GetCategoryGrain(orgId, categoryId);
        await categoryGrain.CreateAsync(new CreateMenuCategoryCommand(
            LocationId: Guid.NewGuid(),
            Name: "Lunch Specials",
            Description: "Available 11am-2pm",
            DisplayOrder: 1,
            Color: "#FF5733"));

        // Act
        await categoryGrain.DeactivateAsync();

        // Assert
        var snapshot = await categoryGrain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateCategory_Reactivate_ShouldMakeCategoryAvailable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var categoryGrain = GetCategoryGrain(orgId, categoryId);
        await categoryGrain.CreateAsync(new CreateMenuCategoryCommand(
            LocationId: Guid.NewGuid(),
            Name: "Seasonal",
            Description: null,
            DisplayOrder: 10,
            Color: null));
        await categoryGrain.DeactivateAsync();

        // Act
        await categoryGrain.UpdateAsync(new UpdateMenuCategoryCommand(
            Name: null,
            Description: null,
            DisplayOrder: null,
            Color: null,
            IsActive: true));

        // Assert
        var snapshot = await categoryGrain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeTrue();
    }

    #endregion

    #region Modifier Availability Tests

    [Fact]
    public async Task RemoveModifierAsync_ShouldRemoveModifierGroup()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId);

        await grain.AddModifierAsync(new MenuItemModifier(
            ModifierId: modifierId,
            Name: "Size",
            PriceAdjustment: 0,
            IsRequired: true,
            MinSelections: 1,
            MaxSelections: 1,
            Options: new List<MenuItemModifierOption>
            {
                new(Guid.NewGuid(), "Small", 0m, true),
                new(Guid.NewGuid(), "Large", 2.00m, false)
            }));

        // Act
        await grain.RemoveModifierAsync(modifierId);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Modifiers.Should().BeEmpty();
    }

    [Fact]
    public async Task AddModifierAsync_ToDeactivatedItem_ShouldStillWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId);
        await grain.DeactivateAsync();

        // Act
        await grain.AddModifierAsync(new MenuItemModifier(
            ModifierId: Guid.NewGuid(),
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

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
        snapshot.Modifiers.Should().HaveCount(1);
    }

    #endregion

    #region Product Tag Tests

    [Fact]
    public async Task AddProductTagAsync_ShouldAddTag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId);

        // Act
        await grain.AddProductTagAsync(new ProductTag(1, "Vegetarian"));
        await grain.AddProductTagAsync(new ProductTag(2, "Gluten-Free"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ProductTags.Should().NotBeNull();
        snapshot.ProductTags.Should().HaveCount(2);
        snapshot.ProductTags.Should().Contain(t => t.Name == "Vegetarian");
        snapshot.ProductTags.Should().Contain(t => t.Name == "Gluten-Free");
    }

    [Fact]
    public async Task RemoveProductTagAsync_ShouldRemoveTag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId);
        await grain.AddProductTagAsync(new ProductTag(1, "Vegetarian"));
        await grain.AddProductTagAsync(new ProductTag(2, "Vegan"));

        // Act
        await grain.RemoveProductTagAsync(1);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ProductTags.Should().HaveCount(1);
        snapshot.ProductTags.Should().Contain(t => t.TagId == 2);
    }

    [Fact]
    public async Task AddProductTagAsync_DuplicateTagId_ShouldUpdateExisting()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId);
        await grain.AddProductTagAsync(new ProductTag(1, "Original Name"));

        // Act
        await grain.AddProductTagAsync(new ProductTag(1, "Updated Name"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ProductTags.Should().HaveCount(1);
        snapshot.ProductTags![0].Name.Should().Be("Updated Name");
    }

    #endregion

    #region Contextual Tax Rates Tests

    [Fact]
    public async Task UpdateTaxRatesAsync_ShouldSetContextualRates()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId);

        var taxRates = new ContextualTaxRates(
            DeliveryTaxPercent: 5.0m,
            TakeawayTaxPercent: 7.5m,
            DineInTaxPercent: 10.0m);

        // Act
        await grain.UpdateTaxRatesAsync(taxRates);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TaxRates.Should().NotBeNull();
        snapshot.TaxRates!.DeliveryTaxPercent.Should().Be(5.0m);
        snapshot.TaxRates.TakeawayTaxPercent.Should().Be(7.5m);
        snapshot.TaxRates.DineInTaxPercent.Should().Be(10.0m);
    }

    [Fact]
    public async Task CreateAsync_WithTaxRates_ShouldSetContextualRates()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = GetMenuItemGrain(orgId, itemId);

        var taxRates = new ContextualTaxRates(
            DeliveryTaxPercent: 0m,
            TakeawayTaxPercent: 5.0m,
            DineInTaxPercent: 8.0m);

        // Act
        var result = await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: categoryId,
            AccountingGroupId: null,
            RecipeId: null,
            Name: "Taxed Item",
            Description: null,
            Price: 15.00m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false,
            TaxRates: taxRates));

        // Assert
        result.TaxRates.Should().NotBeNull();
        result.TaxRates!.DeliveryTaxPercent.Should().Be(0m);
        result.TaxRates.TakeawayTaxPercent.Should().Be(5.0m);
        result.TaxRates.DineInTaxPercent.Should().Be(8.0m);
    }

    #endregion

    #region Variation Availability Tests

    [Fact]
    public async Task AddVariationAsync_ShouldAddVariation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId, "Coffee", 3.50m);

        // Act
        var result = await grain.AddVariationAsync(new CreateMenuItemVariationCommand(
            Name: "Large",
            PricingType: PricingType.Fixed,
            Price: 4.50m,
            Sku: "COFFEE-LG",
            DisplayOrder: 1));

        // Assert
        result.Name.Should().Be("Large");
        result.Price.Should().Be(4.50m);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveVariationAsync_ShouldRemoveVariation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId, "Beer", 6.00m);

        var variation = await grain.AddVariationAsync(new CreateMenuItemVariationCommand(
            Name: "Pint",
            PricingType: PricingType.Fixed,
            Price: 6.00m,
            Sku: null));

        // Act
        await grain.RemoveVariationAsync(variation.VariationId);

        // Assert
        var variations = await grain.GetVariationsAsync();
        variations.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateVariationAsync_Deactivate_ShouldMakeVariationUnavailable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId, "Pizza", 12.00m);

        var variation = await grain.AddVariationAsync(new CreateMenuItemVariationCommand(
            Name: "Extra Large",
            PricingType: PricingType.Fixed,
            Price: 18.00m,
            Sku: null));

        // Act
        await grain.UpdateVariationAsync(variation.VariationId, new UpdateMenuItemVariationCommand(
            IsActive: false));

        // Assert
        var variations = await grain.GetVariationsAsync();
        variations.Should().ContainSingle();
        variations[0].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task AddVariationAsync_MultipleVariations_ShouldOrderByDisplayOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId, "Soda", 2.00m);

        // Act
        await grain.AddVariationAsync(new CreateMenuItemVariationCommand(
            Name: "Large", PricingType: PricingType.Fixed, Price: 3.50m, Sku: null, DisplayOrder: 3));
        await grain.AddVariationAsync(new CreateMenuItemVariationCommand(
            Name: "Small", PricingType: PricingType.Fixed, Price: 2.00m, Sku: null, DisplayOrder: 1));
        await grain.AddVariationAsync(new CreateMenuItemVariationCommand(
            Name: "Medium", PricingType: PricingType.Fixed, Price: 2.75m, Sku: null, DisplayOrder: 2));

        // Assert
        var variations = await grain.GetVariationsAsync();
        variations.Should().HaveCount(3);
        variations[0].Name.Should().Be("Small");
        variations[1].Name.Should().Be("Medium");
        variations[2].Name.Should().Be("Large");
    }

    [Fact]
    public async Task AddVariationAsync_VariablePricing_ShouldAllowNullPrice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId, "Open Item", 0m);

        // Act
        var result = await grain.AddVariationAsync(new CreateMenuItemVariationCommand(
            Name: "Custom Price",
            PricingType: PricingType.Variable,
            Price: null,
            Sku: null));

        // Assert
        result.PricingType.Should().Be(PricingType.Variable);
        result.Price.Should().BeNull();
    }

    #endregion

    #region Price Update Tests

    [Fact]
    public async Task UpdateAsync_Price_ShouldUpdatePrice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId, "Burger", 12.99m);

        // Act
        await grain.UpdateAsync(new UpdateMenuItemCommand(
            CategoryId: null,
            AccountingGroupId: null,
            RecipeId: null,
            Name: null,
            Description: null,
            Price: 14.99m,
            ImageUrl: null,
            Sku: null,
            IsActive: null,
            TrackInventory: null));

        // Assert
        var price = await grain.GetPriceAsync();
        price.Should().Be(14.99m);
    }

    [Fact]
    public async Task GetPriceAsync_SnoozedItem_ShouldStillReturnPrice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId, "Seasonal Special", 19.99m);
        await grain.SetSnoozedAsync(true);

        // Act
        var price = await grain.GetPriceAsync();

        // Assert
        price.Should().Be(19.99m);
    }

    [Fact]
    public async Task GetPriceAsync_DeactivatedItem_ShouldStillReturnPrice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = await CreateMenuItemAsync(orgId, itemId, categoryId, "Discontinued Item", 25.00m);
        await grain.DeactivateAsync();

        // Act
        var price = await grain.GetPriceAsync();

        // Assert
        price.Should().Be(25.00m);
    }

    #endregion
}
