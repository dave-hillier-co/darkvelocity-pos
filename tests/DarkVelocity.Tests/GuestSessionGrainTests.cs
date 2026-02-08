using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class GuestSessionGrainTests
{
    private readonly TestClusterFixture _fixture;

    public GuestSessionGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IGuestSessionGrain GetGrain(Guid orgId, Guid siteId, Guid sessionId)
    {
        var key = GrainKeys.GuestSession(orgId, siteId, sessionId);
        return _fixture.Cluster.GrainFactory.GetGrain<IGuestSessionGrain>(key);
    }

    // Given: a valid ordering link for table 5
    // When: a guest session is started
    // Then: the session is active with an empty cart linked to the table
    [Fact]
    public async Task StartAsync_ShouldCreateActiveSessionWithEmptyCart()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, sessionId);

        // Act
        var result = await grain.StartAsync(new StartGuestSessionCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            LinkId: linkId,
            OrderingType: OrderingLinkType.TableQr,
            TableId: tableId,
            TableNumber: "5"));

        // Assert
        result.SessionId.Should().Be(sessionId);
        result.OrganizationId.Should().Be(orgId);
        result.SiteId.Should().Be(siteId);
        result.LinkId.Should().Be(linkId);
        result.Status.Should().Be(GuestSessionStatus.Active);
        result.CartItems.Should().BeEmpty();
        result.CartTotal.Should().Be(0);
        result.OrderId.Should().BeNull();
        result.OrderingType.Should().Be(OrderingLinkType.TableQr);
        result.TableId.Should().Be(tableId);
        result.TableNumber.Should().Be("5");
    }

    // Given: a kiosk ordering link
    // When: a guest session is started for kiosk ordering
    // Then: the session has kiosk type with no table
    [Fact]
    public async Task StartAsync_Kiosk_ShouldCreateSessionWithoutTable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, sessionId);

        // Act
        var result = await grain.StartAsync(new StartGuestSessionCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            LinkId: linkId,
            OrderingType: OrderingLinkType.Kiosk));

        // Assert
        result.OrderingType.Should().Be(OrderingLinkType.Kiosk);
        result.TableId.Should().BeNull();
        result.TableNumber.Should().BeNull();
        result.Status.Should().Be(GuestSessionStatus.Active);
    }

    // Given: an active guest session with an empty cart
    // When: a menu item is added to the cart
    // Then: the cart contains one item with correct price and the total is updated
    [Fact]
    public async Task AddToCartAsync_ShouldAddItemAndUpdateTotal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, sessionId);
        await grain.StartAsync(new StartGuestSessionCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            LinkId: Guid.NewGuid(),
            OrderingType: OrderingLinkType.TableQr));

        var menuItemId = Guid.NewGuid();

        // Act
        var result = await grain.AddToCartAsync(new AddToCartCommand(
            MenuItemId: menuItemId,
            Name: "Margherita Pizza",
            Quantity: 1,
            UnitPrice: 12.50m));

        // Assert
        result.CartItems.Should().HaveCount(1);
        result.CartItems[0].MenuItemId.Should().Be(menuItemId);
        result.CartItems[0].Name.Should().Be("Margherita Pizza");
        result.CartItems[0].Quantity.Should().Be(1);
        result.CartItems[0].UnitPrice.Should().Be(12.50m);
        result.CartTotal.Should().Be(12.50m);
    }

    // Given: an active guest session
    // When: two different items are added to the cart
    // Then: the cart contains two items and the total is the sum of both
    [Fact]
    public async Task AddToCartAsync_MultipleItems_ShouldAccumulateTotal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, sessionId);
        await grain.StartAsync(new StartGuestSessionCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            LinkId: Guid.NewGuid(),
            OrderingType: OrderingLinkType.Kiosk));

        // Act
        await grain.AddToCartAsync(new AddToCartCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Burger",
            Quantity: 2,
            UnitPrice: 9.99m));

        var result = await grain.AddToCartAsync(new AddToCartCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Fries",
            Quantity: 1,
            UnitPrice: 4.50m));

        // Assert
        result.CartItems.Should().HaveCount(2);
        result.CartTotal.Should().Be(2 * 9.99m + 4.50m);
    }

    // Given: a cart item with notes requesting no onions
    // When: the item is added with notes
    // Then: the notes are preserved on the cart item
    [Fact]
    public async Task AddToCartAsync_WithNotes_ShouldPreserveNotes()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, sessionId);
        await grain.StartAsync(new StartGuestSessionCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            LinkId: Guid.NewGuid(),
            OrderingType: OrderingLinkType.TableQr));

        // Act
        var result = await grain.AddToCartAsync(new AddToCartCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Caesar Salad",
            Quantity: 1,
            UnitPrice: 8.50m,
            Notes: "No croutons, dressing on the side"));

        // Assert
        result.CartItems[0].Notes.Should().Be("No croutons, dressing on the side");
    }

    // Given: a cart item with modifiers (extra cheese, bacon)
    // When: the item is added with modifiers
    // Then: the modifiers are stored and their prices are included in the total
    [Fact]
    public async Task AddToCartAsync_WithModifiers_ShouldIncludeModifierPricesInTotal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, sessionId);
        await grain.StartAsync(new StartGuestSessionCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            LinkId: Guid.NewGuid(),
            OrderingType: OrderingLinkType.Kiosk));

        var modifiers = new List<GuestCartModifier>
        {
            new(ModifierId: Guid.NewGuid(), Name: "Extra Cheese", PriceAdjustment: 1.50m),
            new(ModifierId: Guid.NewGuid(), Name: "Bacon", PriceAdjustment: 2.00m)
        };

        // Act
        var result = await grain.AddToCartAsync(new AddToCartCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Burger",
            Quantity: 1,
            UnitPrice: 10.00m,
            Modifiers: modifiers));

        // Assert
        result.CartItems[0].Modifiers.Should().HaveCount(2);
        // Total = 10.00 (base) + 1.50 (cheese) + 2.00 (bacon) = 13.50
        result.CartTotal.Should().Be(13.50m);
    }

    // Given: a cart with one item at quantity 1
    // When: the item quantity is updated to 3
    // Then: the quantity changes and the total is recalculated
    [Fact]
    public async Task UpdateCartItemAsync_ChangeQuantity_ShouldRecalculateTotal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, sessionId);
        await grain.StartAsync(new StartGuestSessionCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            LinkId: Guid.NewGuid(),
            OrderingType: OrderingLinkType.TableQr));

        var added = await grain.AddToCartAsync(new AddToCartCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Latte",
            Quantity: 1,
            UnitPrice: 4.50m));

        var cartItemId = added.CartItems[0].CartItemId;

        // Act
        var result = await grain.UpdateCartItemAsync(new UpdateCartItemCommand(
            CartItemId: cartItemId,
            Quantity: 3));

        // Assert
        result.CartItems[0].Quantity.Should().Be(3);
        result.CartTotal.Should().Be(3 * 4.50m);
    }

    // Given: a cart with one item
    // When: the item notes are updated
    // Then: the notes are changed
    [Fact]
    public async Task UpdateCartItemAsync_ChangeNotes_ShouldUpdateNotes()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, sessionId);
        await grain.StartAsync(new StartGuestSessionCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            LinkId: Guid.NewGuid(),
            OrderingType: OrderingLinkType.TableQr));

        var added = await grain.AddToCartAsync(new AddToCartCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Pasta",
            Quantity: 1,
            UnitPrice: 14.00m,
            Notes: "Gluten free"));

        var cartItemId = added.CartItems[0].CartItemId;

        // Act
        var result = await grain.UpdateCartItemAsync(new UpdateCartItemCommand(
            CartItemId: cartItemId,
            Notes: "Extra spicy"));

        // Assert
        result.CartItems[0].Notes.Should().Be("Extra spicy");
    }

    // Given: a cart with two items
    // When: one item is removed
    // Then: only one item remains and the total is recalculated
    [Fact]
    public async Task RemoveFromCartAsync_ShouldRemoveItemAndRecalculate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, sessionId);
        await grain.StartAsync(new StartGuestSessionCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            LinkId: Guid.NewGuid(),
            OrderingType: OrderingLinkType.Kiosk));

        var first = await grain.AddToCartAsync(new AddToCartCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Pizza",
            Quantity: 1,
            UnitPrice: 15.00m));

        await grain.AddToCartAsync(new AddToCartCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Salad",
            Quantity: 1,
            UnitPrice: 8.00m));

        var pizzaCartItemId = first.CartItems[0].CartItemId;

        // Act
        var result = await grain.RemoveFromCartAsync(pizzaCartItemId);

        // Assert
        result.CartItems.Should().HaveCount(1);
        result.CartItems[0].Name.Should().Be("Salad");
        result.CartTotal.Should().Be(8.00m);
    }

    // Given: a cart with items
    // When: removing a non-existent cart item ID
    // Then: an InvalidOperationException is thrown
    [Fact]
    public async Task RemoveFromCartAsync_NonExistentItem_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, sessionId);
        await grain.StartAsync(new StartGuestSessionCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            LinkId: Guid.NewGuid(),
            OrderingType: OrderingLinkType.TableQr));

        // Act & Assert
        var act = () => grain.RemoveFromCartAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // Given: a cart with items for table 5
    // When: the guest submits the order
    // Then: the session status changes to Submitted and an order ID is returned
    [Fact]
    public async Task SubmitOrderAsync_ShouldCreateOrderAndChangeStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, sessionId);
        await grain.StartAsync(new StartGuestSessionCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            LinkId: Guid.NewGuid(),
            OrderingType: OrderingLinkType.TableQr,
            TableId: Guid.NewGuid(),
            TableNumber: "5"));

        await grain.AddToCartAsync(new AddToCartCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Fish & Chips",
            Quantity: 1,
            UnitPrice: 16.50m));

        await grain.AddToCartAsync(new AddToCartCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Beer",
            Quantity: 2,
            UnitPrice: 5.50m));

        // Act
        var result = await grain.SubmitOrderAsync(new SubmitGuestOrderCommand(
            GuestName: "John",
            GuestPhone: "+441234567890"));

        // Assert
        result.OrderId.Should().NotBeEmpty();
        result.OrderNumber.Should().NotBeNullOrEmpty();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(GuestSessionStatus.Submitted);
        snapshot.OrderId.Should().Be(result.OrderId);
        snapshot.OrderNumber.Should().Be(result.OrderNumber);
        snapshot.GuestName.Should().Be("John");
        snapshot.GuestPhone.Should().Be("+441234567890");
    }

    // Given: a submitted order
    // When: the guest tries to add more items to the cart
    // Then: an InvalidOperationException is thrown
    [Fact]
    public async Task AddToCartAsync_AfterSubmit_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, sessionId);
        await grain.StartAsync(new StartGuestSessionCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            LinkId: Guid.NewGuid(),
            OrderingType: OrderingLinkType.Kiosk));

        await grain.AddToCartAsync(new AddToCartCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Coffee",
            Quantity: 1,
            UnitPrice: 3.50m));

        await grain.SubmitOrderAsync(new SubmitGuestOrderCommand());

        // Act & Assert
        var act = () => grain.AddToCartAsync(new AddToCartCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Cake",
            Quantity: 1,
            UnitPrice: 5.00m));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // Given: an empty cart
    // When: the guest tries to submit the order
    // Then: an InvalidOperationException is thrown because the cart is empty
    [Fact]
    public async Task SubmitOrderAsync_EmptyCart_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, sessionId);
        await grain.StartAsync(new StartGuestSessionCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            LinkId: Guid.NewGuid(),
            OrderingType: OrderingLinkType.TableQr));

        // Act & Assert
        var act = () => grain.SubmitOrderAsync(new SubmitGuestOrderCommand());
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // Given: an active session
    // When: getting the status
    // Then: it returns Active
    [Fact]
    public async Task GetStatusAsync_ShouldReturnCurrentStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, sessionId);
        await grain.StartAsync(new StartGuestSessionCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            LinkId: Guid.NewGuid(),
            OrderingType: OrderingLinkType.TakeOut));

        // Act
        var status = await grain.GetStatusAsync();

        // Assert
        status.Should().Be(GuestSessionStatus.Active);
    }

    // Given: a submitted kiosk order
    // When: checking the created order via the order grain
    // Then: the order exists with the correct type and items
    [Fact]
    public async Task SubmitOrderAsync_ShouldCreateRealOrderGrain()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, sessionId);
        await grain.StartAsync(new StartGuestSessionCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            LinkId: Guid.NewGuid(),
            OrderingType: OrderingLinkType.Kiosk));

        var menuItemId = Guid.NewGuid();
        await grain.AddToCartAsync(new AddToCartCommand(
            MenuItemId: menuItemId,
            Name: "Chicken Wrap",
            Quantity: 2,
            UnitPrice: 8.00m));

        // Act
        var result = await grain.SubmitOrderAsync(new SubmitGuestOrderCommand(
            GuestName: "Guest"));

        // Assert - verify the order grain was actually created
        var orderGrain = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(orgId, siteId, result.OrderId));
        var orderExists = await orderGrain.ExistsAsync();
        orderExists.Should().BeTrue();

        var orderState = await orderGrain.GetStateAsync();
        orderState.Lines.Should().HaveCount(1);
        orderState.Lines[0].Name.Should().Be("Chicken Wrap");
        orderState.Lines[0].Quantity.Should().Be(2);
        orderState.Lines[0].UnitPrice.Should().Be(8.00m);
    }
}
