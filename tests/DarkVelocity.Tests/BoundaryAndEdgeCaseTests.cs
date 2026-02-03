using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for boundary conditions and edge cases including zero amounts,
/// negative values, empty collections, maximum values, and null handling.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class BoundaryAndEdgeCaseTests
{
    private readonly TestClusterFixture _fixture;

    public BoundaryAndEdgeCaseTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    #region Order Grain Helpers

    private IOrderGrain GetOrderGrain(Guid orgId, Guid siteId, Guid orderId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(orgId, siteId, orderId));
    }

    private async Task<IOrderGrain> CreateOrderAsync(Guid orgId, Guid siteId, Guid orderId)
    {
        var grain = GetOrderGrain(orgId, siteId, orderId);
        await grain.CreateAsync(new CreateOrderCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            CreatedBy: Guid.NewGuid(),
            Type: OrderType.DineIn,
            GuestCount: 2));
        return grain;
    }

    #endregion

    #region Menu Grain Helpers

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

    #endregion

    #region Zero Amount Tests

    [Fact]
    public async Task AddLineAsync_ZeroQuantity_ShouldHandleGracefully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);

        // Act & Assert - zero quantity should be handled (either accepted or rejected)
        var act = async () => await order.AddLineAsync(new AddLineCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Zero Quantity Item",
            Quantity: 0,
            UnitPrice: 10.00m));

        // The system should either accept it with $0 total or throw
        // This test documents the current behavior
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task AddLineAsync_ZeroPrice_ShouldCreateZeroTotalLine()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);

        // Act
        var result = await order.AddLineAsync(new AddLineCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Complimentary Item",
            Quantity: 1,
            UnitPrice: 0m));

        // Assert
        result.LineTotal.Should().Be(0m);
    }

    [Fact]
    public async Task RecordPaymentAsync_ZeroAmount_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);
        await order.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 10.00m));
        await order.SendAsync(Guid.NewGuid());

        // Act & Assert
        var act = async () => await order.RecordPaymentAsync(
            Guid.NewGuid(), 0m, 0m, "Cash");

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task CreateMenuItem_ZeroPrice_ShouldBeAllowed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var grain = GetMenuItemGrain(orgId, itemId);

        // Act
        var result = await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: null,
            RecipeId: null,
            Name: "Free Sample",
            Description: null,
            Price: 0m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        // Assert
        result.Price.Should().Be(0m);
    }

    #endregion

    #region Negative Value Tests

    [Fact]
    public async Task AddLineAsync_NegativeQuantity_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);

        // Act & Assert
        var act = async () => await order.AddLineAsync(new AddLineCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Negative Quantity Item",
            Quantity: -1,
            UnitPrice: 10.00m));

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task AddLineAsync_NegativePrice_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);

        // Act & Assert
        var act = async () => await order.AddLineAsync(new AddLineCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Negative Price Item",
            Quantity: 1,
            UnitPrice: -10.00m));

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task RecordPaymentAsync_NegativeAmount_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);
        await order.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 10.00m));
        await order.SendAsync(Guid.NewGuid());

        // Act & Assert
        var act = async () => await order.RecordPaymentAsync(
            Guid.NewGuid(), -10.00m, 0m, "Cash");

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task RecordPaymentAsync_NegativeTip_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);
        await order.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 10.00m));
        await order.SendAsync(Guid.NewGuid());

        // Act & Assert
        var act = async () => await order.RecordPaymentAsync(
            Guid.NewGuid(), 10.00m, -2.00m, "Cash");

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task CreateMenuItem_NegativePrice_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var grain = GetMenuItemGrain(orgId, itemId);

        // Act & Assert
        var act = async () => await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: null,
            RecipeId: null,
            Name: "Negative Price Item",
            Description: null,
            Price: -10.00m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        await act.Should().ThrowAsync<Exception>();
    }

    #endregion

    #region Large Value Tests

    [Fact]
    public async Task AddLineAsync_LargeQuantity_ShouldCalculateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);

        // Act
        var result = await order.AddLineAsync(new AddLineCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Bulk Item",
            Quantity: 10000,
            UnitPrice: 1.00m));

        // Assert
        result.LineTotal.Should().Be(10000m);
    }

    [Fact]
    public async Task AddLineAsync_HighPrecisionPrice_ShouldRoundAppropriately()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);

        // Act
        var result = await order.AddLineAsync(new AddLineCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Precision Item",
            Quantity: 3,
            UnitPrice: 9.999m));

        // Assert - verify proper decimal handling
        result.LineTotal.Should().BeApproximately(29.997m, 0.001m);
    }

    [Fact]
    public async Task CreateOrder_LargeGuestCount_ShouldAccept()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        // Act - Large party order
        var result = await grain.CreateAsync(new CreateOrderCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            CreatedBy: Guid.NewGuid(),
            Type: OrderType.DineIn,
            GuestCount: 50));

        // Assert
        var state = await grain.GetStateAsync();
        state.GuestCount.Should().Be(50);
    }

    #endregion

    #region Empty Collection Tests

    [Fact]
    public async Task GetLinesAsync_EmptyOrder_ShouldReturnEmptyList()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);

        // Act
        var lines = await order.GetLinesAsync();

        // Assert
        lines.Should().BeEmpty();
    }

    [Fact]
    public async Task AddLineAsync_EmptyModifiersList_ShouldAccept()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);

        // Act
        var result = await order.AddLineAsync(new AddLineCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "No Modifiers Item",
            Quantity: 1,
            UnitPrice: 10.00m,
            Modifiers: new List<OrderLineModifier>()));

        // Assert
        result.LineId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddLineAsync_NullModifiersList_ShouldAccept()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);

        // Act
        var result = await order.AddLineAsync(new AddLineCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Null Modifiers Item",
            Quantity: 1,
            UnitPrice: 10.00m,
            Modifiers: null));

        // Assert
        result.LineId.Should().NotBeEmpty();
    }

    #endregion

    #region String Boundary Tests

    [Fact]
    public async Task AddLineAsync_EmptyName_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);

        // Act & Assert
        var act = async () => await order.AddLineAsync(new AddLineCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "",
            Quantity: 1,
            UnitPrice: 10.00m));

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task CreateMenuItem_EmptyName_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var grain = GetMenuItemGrain(orgId, itemId);

        // Act & Assert
        var act = async () => await grain.CreateAsync(new CreateMenuItemCommand(
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

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task AddLineAsync_VeryLongName_ShouldHandleGracefully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);
        var longName = new string('A', 1000);

        // Act
        var result = await order.AddLineAsync(new AddLineCommand(
            MenuItemId: Guid.NewGuid(),
            Name: longName,
            Quantity: 1,
            UnitPrice: 10.00m));

        // Assert - should either accept or truncate
        result.LineId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddLineAsync_SpecialCharactersInName_ShouldAccept()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);

        // Act
        var result = await order.AddLineAsync(new AddLineCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Café Latté (Special!) — $10",
            Quantity: 1,
            UnitPrice: 10.00m));

        // Assert
        result.LineId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddLineAsync_UnicodeCharactersInName_ShouldAccept()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);

        // Act
        var result = await order.AddLineAsync(new AddLineCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "お寿司 🍣 Sushi",
            Quantity: 1,
            UnitPrice: 15.00m));

        // Assert
        result.LineId.Should().NotBeEmpty();
    }

    #endregion

    #region Category Count Edge Cases

    [Fact]
    public async Task DecrementItemCountAsync_BelowZero_ShouldRemainAtZero()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = GetCategoryGrain(orgId, categoryId);
        await grain.CreateAsync(new CreateMenuCategoryCommand(
            LocationId: Guid.NewGuid(),
            Name: "Empty Category",
            Description: null,
            DisplayOrder: 1,
            Color: null));

        // Act - decrement multiple times when already at 0
        await grain.DecrementItemCountAsync();
        await grain.DecrementItemCountAsync();
        await grain.DecrementItemCountAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemCount.Should().Be(0);
    }

    [Fact]
    public async Task IncrementItemCountAsync_ManyTimes_ShouldAccumulate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = GetCategoryGrain(orgId, categoryId);
        await grain.CreateAsync(new CreateMenuCategoryCommand(
            LocationId: Guid.NewGuid(),
            Name: "Large Category",
            Description: null,
            DisplayOrder: 1,
            Color: null));

        // Act
        for (int i = 0; i < 100; i++)
        {
            await grain.IncrementItemCountAsync();
        }

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemCount.Should().Be(100);
    }

    #endregion

    #region Display Order Edge Cases

    [Fact]
    public async Task CreateCategory_NegativeDisplayOrder_ShouldAccept()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = GetCategoryGrain(orgId, categoryId);

        // Act
        var result = await grain.CreateAsync(new CreateMenuCategoryCommand(
            LocationId: Guid.NewGuid(),
            Name: "Negative Order Category",
            Description: null,
            DisplayOrder: -1,
            Color: null));

        // Assert
        result.DisplayOrder.Should().Be(-1);
    }

    [Fact]
    public async Task CreateCategory_ZeroDisplayOrder_ShouldAccept()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = GetCategoryGrain(orgId, categoryId);

        // Act
        var result = await grain.CreateAsync(new CreateMenuCategoryCommand(
            LocationId: Guid.NewGuid(),
            Name: "Zero Order Category",
            Description: null,
            DisplayOrder: 0,
            Color: null));

        // Assert
        result.DisplayOrder.Should().Be(0);
    }

    [Fact]
    public async Task CreateCategory_LargeDisplayOrder_ShouldAccept()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = GetCategoryGrain(orgId, categoryId);

        // Act
        var result = await grain.CreateAsync(new CreateMenuCategoryCommand(
            LocationId: Guid.NewGuid(),
            Name: "Large Order Category",
            Description: null,
            DisplayOrder: int.MaxValue,
            Color: null));

        // Assert
        result.DisplayOrder.Should().Be(int.MaxValue);
    }

    #endregion

    #region Existence Check Edge Cases

    [Fact]
    public async Task ExistsAsync_NonExistentOrder_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var nonExistentOrderId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, nonExistentOrderId);

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ExistingOrder_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = await CreateOrderAsync(orgId, siteId, orderId);

        // Act
        var exists = await order.ExistsAsync();

        // Assert
        exists.Should().BeTrue();
    }

    #endregion

    #region Guest Count Edge Cases

    [Fact]
    public async Task CreateOrder_ZeroGuestCount_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        // Act & Assert
        var act = async () => await grain.CreateAsync(new CreateOrderCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            CreatedBy: Guid.NewGuid(),
            Type: OrderType.DineIn,
            GuestCount: 0));

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task CreateOrder_NegativeGuestCount_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        // Act & Assert
        var act = async () => await grain.CreateAsync(new CreateOrderCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            CreatedBy: Guid.NewGuid(),
            Type: OrderType.DineIn,
            GuestCount: -1));

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task CreateOrder_DefaultGuestCount_ShouldBeOne()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        // Act - use default guest count
        await grain.CreateAsync(new CreateOrderCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            CreatedBy: Guid.NewGuid(),
            Type: OrderType.DineIn));

        // Assert
        var state = await grain.GetStateAsync();
        state.GuestCount.Should().Be(1);
    }

    #endregion

    #region Modifier Edge Cases

    [Fact]
    public async Task AddModifierAsync_EmptyOptions_ShouldReject()
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
            Name: "Test Item",
            Description: null,
            Price: 10.00m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        // Act & Assert
        var act = async () => await grain.AddModifierAsync(new MenuItemModifier(
            ModifierId: Guid.NewGuid(),
            Name: "Empty Options",
            PriceAdjustment: 0,
            IsRequired: false,
            MinSelections: 0,
            MaxSelections: 0,
            Options: new List<MenuItemModifierOption>()));

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task AddModifierAsync_MinGreaterThanMax_ShouldReject()
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
            Name: "Test Item",
            Description: null,
            Price: 10.00m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        // Act & Assert
        var act = async () => await grain.AddModifierAsync(new MenuItemModifier(
            ModifierId: Guid.NewGuid(),
            Name: "Invalid Selections",
            PriceAdjustment: 0,
            IsRequired: true,
            MinSelections: 5,
            MaxSelections: 2, // Min > Max is invalid
            Options: new List<MenuItemModifierOption>
            {
                new(Guid.NewGuid(), "Option 1", 0m, true)
            }));

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task RemoveModifierAsync_NonExistentModifier_ShouldHandleGracefully()
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
            Name: "Test Item",
            Description: null,
            Price: 10.00m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        // Act - attempt to remove non-existent modifier
        await grain.RemoveModifierAsync(Guid.NewGuid());

        // Assert - should not throw, just no-op
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Modifiers.Should().BeEmpty();
    }

    #endregion

    #region Update With Null Values

    [Fact]
    public async Task UpdateAsync_AllNullValues_ShouldNotChangeAnything()
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
            Name: "Original Name",
            Description: "Original Description",
            Price: 15.00m,
            ImageUrl: null,
            Sku: "SKU-001",
            TrackInventory: true));

        // Act - update with all nulls
        await grain.UpdateAsync(new UpdateMenuItemCommand(
            CategoryId: null,
            AccountingGroupId: null,
            RecipeId: null,
            Name: null,
            Description: null,
            Price: null,
            ImageUrl: null,
            Sku: null,
            IsActive: null,
            TrackInventory: null));

        // Assert - nothing should change
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Name.Should().Be("Original Name");
        snapshot.Description.Should().Be("Original Description");
        snapshot.Price.Should().Be(15.00m);
        snapshot.Sku.Should().Be("SKU-001");
        snapshot.TrackInventory.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateCategoryAsync_PartialUpdate_ShouldOnlyUpdateProvided()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = GetCategoryGrain(orgId, categoryId);
        await grain.CreateAsync(new CreateMenuCategoryCommand(
            LocationId: Guid.NewGuid(),
            Name: "Original",
            Description: "Original Desc",
            DisplayOrder: 5,
            Color: "#FF0000"));

        // Act - only update name
        await grain.UpdateAsync(new UpdateMenuCategoryCommand(
            Name: "Updated Name",
            Description: null,
            DisplayOrder: null,
            Color: null,
            IsActive: null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Name.Should().Be("Updated Name");
        snapshot.Description.Should().Be("Original Desc");
        snapshot.DisplayOrder.Should().Be(5);
        snapshot.Color.Should().Be("#FF0000");
    }

    #endregion
}
