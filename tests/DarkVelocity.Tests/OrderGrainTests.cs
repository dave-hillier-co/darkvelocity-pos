using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class OrderGrainTests
{
    private readonly TestClusterFixture _fixture;

    public OrderGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IOrderGrain GetOrderGrain(Guid orgId, Guid siteId, Guid orderId)
        => _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));

    [Fact]
    public async Task CreateAsync_ShouldCreateOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        var command = new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn, Guid.NewGuid(), "T1", null, 2);

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Id.Should().Be(orderId);
        result.OrderNumber.Should().StartWith("ORD-");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetStateAsync_ShouldReturnState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn, tableId, "T5", null, 4));

        // Act
        var state = await grain.GetStateAsync();

        // Assert
        state.Id.Should().Be(orderId);
        state.OrganizationId.Should().Be(orgId);
        state.SiteId.Should().Be(siteId);
        state.Type.Should().Be(OrderType.DineIn);
        state.TableId.Should().Be(tableId);
        state.TableNumber.Should().Be("T5");
        state.GuestCount.Should().Be(4);
        state.Status.Should().Be(OrderStatus.Open);
    }

    [Fact]
    public async Task AddLineAsync_ShouldAddLine()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Act
        var result = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 2, 12.99m));

        // Assert
        result.LineTotal.Should().Be(25.98m);
        var lines = await grain.GetLinesAsync();
        lines.Should().HaveCount(1);
        lines[0].Name.Should().Be("Burger");
        lines[0].Quantity.Should().Be(2);
    }

    [Fact]
    public async Task AddLineAsync_WithModifiers_ShouldIncludeModifierCosts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        var modifiers = new List<OrderLineModifier>
        {
            new() { ModifierId = Guid.NewGuid(), Name = "Extra Cheese", Price = 1.50m, Quantity = 1 },
            new() { ModifierId = Guid.NewGuid(), Name = "Bacon", Price = 2.00m, Quantity = 1 }
        };

        // Act
        var result = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 12.99m, null, modifiers));

        // Assert
        result.LineTotal.Should().Be(16.49m); // 12.99 + 1.50 + 2.00
    }

    [Fact]
    public async Task UpdateLineAsync_ShouldUpdateQuantity()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var addResult = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 12.99m));

        // Act
        await grain.UpdateLineAsync(new UpdateLineCommand(addResult.LineId, Quantity: 3));

        // Assert
        var lines = await grain.GetLinesAsync();
        lines[0].Quantity.Should().Be(3);
        lines[0].LineTotal.Should().Be(38.97m);
    }

    [Fact]
    public async Task VoidLineAsync_ShouldVoidLine()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var addResult = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 12.99m));

        // Act
        await grain.VoidLineAsync(new VoidLineCommand(addResult.LineId, userId, "Customer changed mind"));

        // Assert
        var lines = await grain.GetLinesAsync();
        lines[0].Status.Should().Be(OrderLineStatus.Voided);
        lines[0].VoidReason.Should().Be("Customer changed mind");

        var totals = await grain.GetTotalsAsync();
        totals.Subtotal.Should().Be(0);
    }

    [Fact]
    public async Task RemoveLineAsync_ShouldRemoveLine()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var addResult = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 12.99m));

        // Act
        await grain.RemoveLineAsync(addResult.LineId);

        // Assert
        var lines = await grain.GetLinesAsync();
        lines.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_ShouldMarkLinesAsSent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 12.99m));

        // Act
        await grain.SendAsync(userId);

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(OrderStatus.Sent);
        state.SentAt.Should().NotBeNull();
        state.Lines[0].Status.Should().Be(OrderLineStatus.Sent);
    }

    [Fact]
    public async Task ApplyDiscountAsync_WithPercentage_ShouldCalculateDiscount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 100m));

        // Act
        await grain.ApplyDiscountAsync(new ApplyDiscountCommand("10% Off", DiscountType.Percentage, 10m, userId));

        // Assert
        var totals = await grain.GetTotalsAsync();
        totals.DiscountTotal.Should().Be(10m);
    }

    [Fact]
    public async Task ApplyDiscountAsync_WithFixedAmount_ShouldApplyDiscount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 100m));

        // Act
        await grain.ApplyDiscountAsync(new ApplyDiscountCommand("$5 Off", DiscountType.FixedAmount, 5m, userId));

        // Assert
        var totals = await grain.GetTotalsAsync();
        totals.DiscountTotal.Should().Be(5m);
    }

    [Fact]
    public async Task RecordPaymentAsync_ShouldUpdatePaidAmount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 100m));

        var totals = await grain.GetTotalsAsync();

        // Act
        await grain.RecordPaymentAsync(paymentId, totals.GrandTotal, 10m, "Cash");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(OrderStatus.Paid);
        state.PaidAmount.Should().Be(totals.GrandTotal);
        state.TipTotal.Should().Be(10m);
        state.BalanceDue.Should().Be(0);
    }

    [Fact]
    public async Task RecordPaymentAsync_PartialPayment_ShouldSetPartiallyPaidStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 100m));

        // Act
        await grain.RecordPaymentAsync(Guid.NewGuid(), 50m, 0m, "Cash");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(OrderStatus.PartiallyPaid);
        state.PaidAmount.Should().Be(50m);
        state.BalanceDue.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CloseAsync_WithBalancePaid_ShouldCloseOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 100m));
        var totals = await grain.GetTotalsAsync();
        await grain.RecordPaymentAsync(Guid.NewGuid(), totals.GrandTotal, 0m, "Cash");

        // Act
        await grain.CloseAsync(userId);

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(OrderStatus.Closed);
        state.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CloseAsync_WithOutstandingBalance_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 100m));

        // Act
        var act = () => grain.CloseAsync(userId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot close order with outstanding balance");
    }

    [Fact]
    public async Task VoidAsync_ShouldVoidOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 100m));

        // Act
        await grain.VoidAsync(new VoidOrderCommand(userId, "Customer left"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(OrderStatus.Voided);
        state.VoidReason.Should().Be("Customer left");
    }

    [Fact]
    public async Task TransferTableAsync_ShouldUpdateTable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var newTableId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn, Guid.NewGuid(), "T1"));

        // Act
        await grain.TransferTableAsync(newTableId, "T10", userId);

        // Assert
        var state = await grain.GetStateAsync();
        state.TableId.Should().Be(newTableId);
        state.TableNumber.Should().Be("T10");
    }

    [Fact]
    public async Task AssignServerAsync_ShouldAssignServer()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Act
        await grain.AssignServerAsync(serverId, "John Smith");

        // Assert
        var state = await grain.GetStateAsync();
        state.ServerId.Should().Be(serverId);
        state.ServerName.Should().Be("John Smith");
    }

    [Fact]
    public async Task AssignCustomerAsync_ShouldAssignCustomer()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Act
        await grain.AssignCustomerAsync(customerId, "Jane Doe");

        // Assert
        var state = await grain.GetStateAsync();
        state.CustomerId.Should().Be(customerId);
        state.CustomerName.Should().Be("Jane Doe");
    }

    // Bill Splitting Tests

    [Fact]
    public async Task SplitByItemsAsync_ShouldCreateNewOrderWithMovedItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn, Guid.NewGuid(), "T1"));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 15.00m));
        var line2 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Fries", 1, 5.00m));
        var line3 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Soda", 1, 3.00m));

        // Act
        var result = await grain.SplitByItemsAsync(new SplitByItemsCommand(
            new List<Guid> { line2.LineId, line3.LineId },
            userId,
            1));

        // Assert
        result.NewOrderId.Should().NotBeEmpty();
        result.NewOrderNumber.Should().EndWith("-S");
        result.LinesMoved.Should().Be(2);

        // Verify original order has only the burger
        var originalState = await grain.GetStateAsync();
        originalState.Lines.Should().HaveCount(1);
        originalState.Lines[0].Name.Should().Be("Burger");
        originalState.ChildOrders.Should().HaveCount(1);
        originalState.ChildOrders[0].OrderId.Should().Be(result.NewOrderId);

        // Verify new order has fries and soda
        var newOrderGrain = GetOrderGrain(orgId, siteId, result.NewOrderId);
        var newOrderState = await newOrderGrain.GetStateAsync();
        newOrderState.Lines.Should().HaveCount(2);
        newOrderState.ParentOrderId.Should().Be(orderId);
        newOrderState.TableNumber.Should().Be("T1");
    }

    [Fact]
    public async Task SplitByItemsAsync_ShouldFailWhenNoLinesSpecified()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 15.00m));

        // Act
        var act = () => grain.SplitByItemsAsync(new SplitByItemsCommand(new List<Guid>(), userId));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*At least one line must be specified*");
    }

    [Fact]
    public async Task SplitByItemsAsync_ShouldFailWhenAllLinesSelected()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 15.00m));

        // Act
        var act = () => grain.SplitByItemsAsync(new SplitByItemsCommand(
            new List<Guid> { line1.LineId },
            userId));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*at least one line must remain*");
    }

    [Fact]
    public async Task SplitByItemsAsync_ShouldFailForClosedOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 15.00m));
        var line2 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Fries", 1, 5.00m));
        var totals = await grain.GetTotalsAsync();
        await grain.RecordPaymentAsync(Guid.NewGuid(), totals.GrandTotal, 0m, "Cash");
        await grain.CloseAsync(userId);

        // Act
        var act = () => grain.SplitByItemsAsync(new SplitByItemsCommand(
            new List<Guid> { line1.LineId },
            userId));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    [Fact]
    public async Task SplitByItemsAsync_BothOrdersShouldBeIndependentlyPayable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 100.00m));
        var line2 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Fries", 1, 50.00m));

        var splitResult = await grain.SplitByItemsAsync(new SplitByItemsCommand(
            new List<Guid> { line2.LineId },
            userId));

        // Act - Pay original order
        var originalTotals = await grain.GetTotalsAsync();
        await grain.RecordPaymentAsync(Guid.NewGuid(), originalTotals.GrandTotal, 0m, "Cash");
        await grain.CloseAsync(userId);

        // Act - Pay new order
        var newOrderGrain = GetOrderGrain(orgId, siteId, splitResult.NewOrderId);
        var newTotals = await newOrderGrain.GetTotalsAsync();
        await newOrderGrain.RecordPaymentAsync(Guid.NewGuid(), newTotals.GrandTotal, 0m, "Card");
        await newOrderGrain.CloseAsync(userId);

        // Assert - Both orders should be closed
        var originalState = await grain.GetStateAsync();
        var newState = await newOrderGrain.GetStateAsync();

        originalState.Status.Should().Be(OrderStatus.Closed);
        newState.Status.Should().Be(OrderStatus.Closed);
    }

    [Fact]
    public async Task CalculateSplitByPeopleAsync_ShouldCalculateEqualShares()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Meal", 1, 100.00m));

        // Act
        var result = await grain.CalculateSplitByPeopleAsync(4);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Shares.Should().HaveCount(4);
        result.Shares.Sum(s => s.Total).Should().Be(result.BalanceDue);
        result.Shares.All(s => s.Label!.StartsWith("Guest")).Should().BeTrue();
    }

    [Fact]
    public async Task CalculateSplitByPeopleAsync_ShouldHandleRemainderCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        // Create amount that doesn't divide evenly by 3 (after tax)
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Meal", 1, 100.00m));

        var totals = await grain.GetTotalsAsync();

        // Act
        var result = await grain.CalculateSplitByPeopleAsync(3);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Shares.Should().HaveCount(3);
        // Sum should equal balance due exactly
        result.Shares.Sum(s => s.Total).Should().Be(result.BalanceDue);
    }

    [Fact]
    public async Task CalculateSplitByPeopleAsync_ShouldFailWithZeroPeople()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Meal", 1, 100.00m));

        // Act
        var act = () => grain.CalculateSplitByPeopleAsync(0);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public async Task CalculateSplitByPeopleAsync_ShouldReturnInvalidWhenFullyPaid()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Meal", 1, 100.00m));
        var totals = await grain.GetTotalsAsync();
        await grain.RecordPaymentAsync(Guid.NewGuid(), totals.GrandTotal, 0m, "Cash");

        // Act
        var result = await grain.CalculateSplitByPeopleAsync(2);

        // Assert
        result.IsValid.Should().BeFalse();
        result.BalanceDue.Should().Be(0);
        result.Shares.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculateSplitByAmountsAsync_ShouldValidateAmountsMatchBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Meal", 1, 100.00m));
        var totals = await grain.GetTotalsAsync();

        // Act - Amounts that match balance
        var result = await grain.CalculateSplitByAmountsAsync(new List<decimal>
        {
            totals.BalanceDue / 2,
            totals.BalanceDue / 2
        });

        // Assert
        result.IsValid.Should().BeTrue();
        result.Shares.Should().HaveCount(2);
        result.Shares.Sum(s => s.Total).Should().BeApproximately(totals.BalanceDue, 0.01m);
    }

    [Fact]
    public async Task CalculateSplitByAmountsAsync_ShouldReturnInvalidWhenAmountsDontMatch()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Meal", 1, 100.00m));

        // Act - Amounts that don't match balance
        var result = await grain.CalculateSplitByAmountsAsync(new List<decimal> { 30.00m, 30.00m });

        // Assert
        result.IsValid.Should().BeFalse();
        result.Shares.Should().HaveCount(2);
    }

    [Fact]
    public async Task CalculateSplitByAmountsAsync_ShouldFailWithNegativeAmounts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Meal", 1, 100.00m));

        // Act
        var act = () => grain.CalculateSplitByAmountsAsync(new List<decimal> { -10.00m, 50.00m });

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public async Task CalculateSplitByAmountsAsync_ShouldFailWithEmptyList()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Meal", 1, 100.00m));

        // Act
        var act = () => grain.CalculateSplitByAmountsAsync(new List<decimal>());

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*At least one amount*");
    }

    // Per-Item Tax Rate Tests

    [Fact]
    public async Task AddLineAsync_WithDifferentTaxRates_ShouldCalculateCorrectTax()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Act - Add items with different tax rates (e.g., food vs alcohol)
        // Food item at 10% tax
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 20.00m, TaxRate: 10));
        // Alcohol item at 20% tax
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Beer", 2, 5.00m, TaxRate: 20));
        // Non-taxable item (e.g., takeout in some jurisdictions)
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Gift Card", 1, 25.00m, TaxRate: 0));

        // Assert
        var state = await grain.GetStateAsync();
        state.Subtotal.Should().Be(55.00m); // 20 + 10 + 25

        // Tax breakdown:
        // Burger: 20 * 0.10 = 2.00
        // Beer: 10 * 0.20 = 2.00
        // Gift Card: 25 * 0.00 = 0.00
        // Total tax: 4.00
        state.TaxTotal.Should().Be(4.00m);
        state.GrandTotal.Should().Be(59.00m); // 55 + 4

        // Verify individual line tax amounts
        state.Lines[0].TaxRate.Should().Be(10);
        state.Lines[0].TaxAmount.Should().Be(2.00m);
        state.Lines[1].TaxRate.Should().Be(20);
        state.Lines[1].TaxAmount.Should().Be(2.00m);
        state.Lines[2].TaxRate.Should().Be(0);
        state.Lines[2].TaxAmount.Should().Be(0.00m);
    }

    [Fact]
    public async Task AddLineAsync_TakeoutWithReducedTax_ShouldCalculateCorrectTax()
    {
        // Arrange - Simulates takeout scenario where tax rate might be lower
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.TakeOut));

        // Act - Add items with takeout tax rate (e.g., 7.5% instead of 10%)
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 100.00m, TaxRate: 7.5m));

        // Assert
        var state = await grain.GetStateAsync();
        state.Subtotal.Should().Be(100.00m);
        state.TaxTotal.Should().Be(7.50m); // 100 * 0.075
        state.GrandTotal.Should().Be(107.50m);
    }

    [Fact]
    public async Task UpdateLineAsync_ShouldRecalculateTaxAmount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var addResult = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 10.00m, TaxRate: 10));

        // Act - Update quantity from 1 to 3
        await grain.UpdateLineAsync(new UpdateLineCommand(addResult.LineId, Quantity: 3));

        // Assert
        var state = await grain.GetStateAsync();
        state.Subtotal.Should().Be(30.00m); // 10 * 3
        state.Lines[0].TaxAmount.Should().Be(3.00m); // 30 * 0.10
        state.TaxTotal.Should().Be(3.00m);
        state.GrandTotal.Should().Be(33.00m);
    }

    // Bundle/Combo Meal Tests

    [Fact]
    public async Task AddLineAsync_WithBundle_ShouldStoreBundleComponents()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        var bundleComponents = new List<OrderLineBundleComponent>
        {
            new() { SlotId = "main", SlotName = "Main", ItemDocumentId = "burger-doc", ItemName = "Cheeseburger", Quantity = 1 },
            new() { SlotId = "side", SlotName = "Side", ItemDocumentId = "fries-doc", ItemName = "Large Fries", Quantity = 1 },
            new() { SlotId = "drink", SlotName = "Drink", ItemDocumentId = "coke-doc", ItemName = "Large Coke", Quantity = 1 }
        };

        // Act
        var result = await grain.AddLineAsync(new AddLineCommand(
            Guid.NewGuid(),
            "Combo Meal",
            1,
            9.99m,
            IsBundle: true,
            BundleComponents: bundleComponents));

        // Assert
        var lines = await grain.GetLinesAsync();
        lines.Should().HaveCount(1);
        lines[0].IsBundle.Should().BeTrue();
        lines[0].BundleComponents.Should().HaveCount(3);
        lines[0].BundleComponents[0].SlotName.Should().Be("Main");
        lines[0].BundleComponents[0].ItemName.Should().Be("Cheeseburger");
        lines[0].BundleComponents[1].SlotName.Should().Be("Side");
        lines[0].BundleComponents[2].SlotName.Should().Be("Drink");
    }

    [Fact]
    public async Task AddLineAsync_WithBundleUpgrade_ShouldIncludePriceAdjustment()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        var bundleComponents = new List<OrderLineBundleComponent>
        {
            new() { SlotId = "main", SlotName = "Main", ItemDocumentId = "burger-doc", ItemName = "Cheeseburger", Quantity = 1, PriceAdjustment = 0 },
            new() { SlotId = "side", SlotName = "Side", ItemDocumentId = "onion-rings-doc", ItemName = "Onion Rings", Quantity = 1, PriceAdjustment = 1.50m }, // Upgrade from fries
            new() { SlotId = "drink", SlotName = "Drink", ItemDocumentId = "shake-doc", ItemName = "Milkshake", Quantity = 1, PriceAdjustment = 2.00m } // Upgrade from soda
        };

        // Act
        var result = await grain.AddLineAsync(new AddLineCommand(
            Guid.NewGuid(),
            "Combo Meal",
            1,
            9.99m,
            IsBundle: true,
            BundleComponents: bundleComponents));

        // Assert
        // Line total should be base price + upgrades: 9.99 + 1.50 + 2.00 = 13.49
        result.LineTotal.Should().Be(13.49m);

        var lines = await grain.GetLinesAsync();
        lines[0].LineTotal.Should().Be(13.49m);
    }

    [Fact]
    public async Task AddLineAsync_WithBundleAndTax_ShouldCalculateTaxOnTotalIncludingUpgrades()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        var bundleComponents = new List<OrderLineBundleComponent>
        {
            new() { SlotId = "main", SlotName = "Main", ItemDocumentId = "burger-doc", ItemName = "Burger", Quantity = 1 },
            new() { SlotId = "side", SlotName = "Side", ItemDocumentId = "fries-doc", ItemName = "Large Fries", Quantity = 1, PriceAdjustment = 1.00m }
        };

        // Act
        await grain.AddLineAsync(new AddLineCommand(
            Guid.NewGuid(),
            "Combo Meal",
            1,
            10.00m,
            TaxRate: 10,
            IsBundle: true,
            BundleComponents: bundleComponents));

        // Assert
        var state = await grain.GetStateAsync();
        // Line total: 10.00 + 1.00 = 11.00
        // Tax: 11.00 * 0.10 = 1.10
        state.Subtotal.Should().Be(11.00m);
        state.TaxTotal.Should().Be(1.10m);
        state.GrandTotal.Should().Be(12.10m);
    }

    [Fact]
    public async Task AddLineAsync_WithBundleQuantityGreaterThanOne_ShouldMultiplyCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        var bundleComponents = new List<OrderLineBundleComponent>
        {
            new() { SlotId = "main", SlotName = "Main", ItemDocumentId = "burger-doc", ItemName = "Burger", Quantity = 1 },
            new() { SlotId = "side", SlotName = "Side", ItemDocumentId = "fries-doc", ItemName = "Fries", Quantity = 1, PriceAdjustment = 0.50m }
        };

        // Act - Order 2 combo meals
        var result = await grain.AddLineAsync(new AddLineCommand(
            Guid.NewGuid(),
            "Combo Meal",
            2,
            10.00m,
            IsBundle: true,
            BundleComponents: bundleComponents));

        // Assert
        // Base: 10.00 * 2 = 20.00
        // Upgrade: 0.50 * 1 = 0.50 (component quantity is 1, line quantity is 2)
        // Total: 20.50
        result.LineTotal.Should().Be(20.50m);
    }

    [Fact]
    public async Task AddLineAsync_WithBundleComponentModifiers_ShouldStoreModifiers()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        var componentModifiers = new List<OrderLineModifier>
        {
            new() { ModifierId = Guid.NewGuid(), Name = "No Pickles", Price = 0, Quantity = 1 },
            new() { ModifierId = Guid.NewGuid(), Name = "Extra Cheese", Price = 0.75m, Quantity = 1 }
        };

        var bundleComponents = new List<OrderLineBundleComponent>
        {
            new()
            {
                SlotId = "main",
                SlotName = "Main",
                ItemDocumentId = "burger-doc",
                ItemName = "Cheeseburger",
                Quantity = 1,
                Modifiers = componentModifiers
            },
            new() { SlotId = "side", SlotName = "Side", ItemDocumentId = "fries-doc", ItemName = "Fries", Quantity = 1 }
        };

        // Act
        await grain.AddLineAsync(new AddLineCommand(
            Guid.NewGuid(),
            "Combo Meal",
            1,
            9.99m,
            IsBundle: true,
            BundleComponents: bundleComponents));

        // Assert
        var lines = await grain.GetLinesAsync();
        lines[0].BundleComponents[0].Modifiers.Should().HaveCount(2);
        lines[0].BundleComponents[0].Modifiers[0].Name.Should().Be("No Pickles");
        lines[0].BundleComponents[0].Modifiers[1].Name.Should().Be("Extra Cheese");
    }

    [Fact]
    public async Task AddLineAsync_NonBundle_ShouldHaveEmptyBundleComponents()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Act - Add a regular (non-bundle) item
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 12.99m));

        // Assert
        var lines = await grain.GetLinesAsync();
        lines[0].IsBundle.Should().BeFalse();
        lines[0].BundleComponents.Should().BeEmpty();
    }

    // Hold/Fire Workflow Tests

    [Fact]
    public async Task HoldItemsAsync_ShouldMarkItemsAsHeld()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Appetizer", 1, 10.00m));
        var line2 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Main Course", 1, 25.00m));

        // Act
        await grain.HoldItemsAsync(new HoldItemsCommand(
            new List<Guid> { line2.LineId },
            userId,
            "Wait for appetizer"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Lines[0].IsHeld.Should().BeFalse();
        state.Lines[1].IsHeld.Should().BeTrue();
        state.Lines[1].HoldReason.Should().Be("Wait for appetizer");
        state.Lines[1].HeldBy.Should().Be(userId);
        state.Lines[1].HeldAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ReleaseItemsAsync_ShouldUnholdItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Main Course", 1, 25.00m));
        await grain.HoldItemsAsync(new HoldItemsCommand(new List<Guid> { line1.LineId }, userId, "Wait"));

        // Act
        await grain.ReleaseItemsAsync(new ReleaseItemsCommand(new List<Guid> { line1.LineId }, userId));

        // Assert
        var state = await grain.GetStateAsync();
        state.Lines[0].IsHeld.Should().BeFalse();
        state.Lines[0].HoldReason.Should().BeNull();
        state.Lines[0].HeldAt.Should().BeNull();
    }

    [Fact]
    public async Task SetItemCourseAsync_ShouldSetCourseNumber()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Soup", 1, 8.00m));
        var line2 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Steak", 1, 35.00m));

        // Act
        await grain.SetItemCourseAsync(new SetItemCourseCommand(
            new List<Guid> { line1.LineId },
            1,
            userId));
        await grain.SetItemCourseAsync(new SetItemCourseCommand(
            new List<Guid> { line2.LineId },
            2,
            userId));

        // Assert
        var state = await grain.GetStateAsync();
        state.Lines[0].CourseNumber.Should().Be(1);
        state.Lines[1].CourseNumber.Should().Be(2);
    }

    [Fact]
    public async Task FireItemsAsync_ShouldMarkItemsAsSent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Appetizer", 1, 10.00m));
        await grain.HoldItemsAsync(new HoldItemsCommand(new List<Guid> { line1.LineId }, userId, "Wait"));

        // Act
        var result = await grain.FireItemsAsync(new FireItemsCommand(
            new List<Guid> { line1.LineId },
            userId));

        // Assert
        result.FiredCount.Should().Be(1);
        result.FiredLineIds.Should().Contain(line1.LineId);

        var state = await grain.GetStateAsync();
        state.Lines[0].IsHeld.Should().BeFalse();
        state.Lines[0].FiredAt.Should().NotBeNull();
        state.Lines[0].SentAt.Should().NotBeNull();
        state.Lines[0].Status.Should().Be(OrderLineStatus.Sent);
        state.Status.Should().Be(OrderStatus.Sent);
    }

    [Fact]
    public async Task FireCourseAsync_ShouldFireAllItemsInCourse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Soup", 1, 8.00m));
        var line2 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Salad", 1, 10.00m));
        var line3 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Steak", 1, 35.00m));

        // Set courses
        await grain.SetItemCourseAsync(new SetItemCourseCommand(
            new List<Guid> { line1.LineId, line2.LineId },
            1,
            userId));
        await grain.SetItemCourseAsync(new SetItemCourseCommand(
            new List<Guid> { line3.LineId },
            2,
            userId));

        // Act - Fire course 1
        var result = await grain.FireCourseAsync(new FireCourseCommand(1, userId));

        // Assert
        result.FiredCount.Should().Be(2);
        result.FiredLineIds.Should().Contain(line1.LineId);
        result.FiredLineIds.Should().Contain(line2.LineId);

        var state = await grain.GetStateAsync();
        state.Lines[0].Status.Should().Be(OrderLineStatus.Sent);
        state.Lines[1].Status.Should().Be(OrderLineStatus.Sent);
        state.Lines[2].Status.Should().Be(OrderLineStatus.Pending); // Course 2 not fired
    }

    [Fact]
    public async Task FireAllAsync_ShouldFireAllPendingItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Appetizer", 1, 10.00m));
        var line2 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Main", 1, 25.00m));
        var line3 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Dessert", 1, 8.00m));

        // Hold some items
        await grain.HoldItemsAsync(new HoldItemsCommand(
            new List<Guid> { line2.LineId, line3.LineId },
            userId,
            "Wait"));

        // Act
        var result = await grain.FireAllAsync(userId);

        // Assert
        result.FiredCount.Should().Be(3);
        result.FiredLineIds.Should().HaveCount(3);

        var state = await grain.GetStateAsync();
        state.Lines.Should().AllSatisfy(l =>
        {
            l.Status.Should().Be(OrderLineStatus.Sent);
            l.IsHeld.Should().BeFalse();
            l.FiredAt.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task GetHoldSummaryAsync_ShouldReturnHeldItemsCounts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Appetizer", 1, 10.00m));
        var line2 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Main", 1, 25.00m));
        var line3 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Dessert", 1, 8.00m));

        // Set courses and hold
        await grain.SetItemCourseAsync(new SetItemCourseCommand(
            new List<Guid> { line1.LineId },
            1,
            userId));
        await grain.SetItemCourseAsync(new SetItemCourseCommand(
            new List<Guid> { line2.LineId, line3.LineId },
            2,
            userId));
        await grain.HoldItemsAsync(new HoldItemsCommand(
            new List<Guid> { line2.LineId, line3.LineId },
            userId));

        // Act
        var summary = await grain.GetHoldSummaryAsync();

        // Assert
        summary.TotalHeldCount.Should().Be(2);
        summary.HeldByCourseCounts.Should().ContainKey(2);
        summary.HeldByCourseCounts[2].Should().Be(2);
        summary.HeldLineIds.Should().Contain(line2.LineId);
        summary.HeldLineIds.Should().Contain(line3.LineId);
    }

    [Fact]
    public async Task GetCourseSummaryAsync_ShouldReturnItemCountsByCourse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Soup", 1, 8.00m));
        var line2 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Salad", 1, 10.00m));
        var line3 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Steak", 1, 35.00m));
        var line4 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Dessert", 1, 12.00m));

        // Set courses
        await grain.SetItemCourseAsync(new SetItemCourseCommand(
            new List<Guid> { line1.LineId, line2.LineId },
            1,
            userId));
        await grain.SetItemCourseAsync(new SetItemCourseCommand(
            new List<Guid> { line3.LineId },
            2,
            userId));
        await grain.SetItemCourseAsync(new SetItemCourseCommand(
            new List<Guid> { line4.LineId },
            3,
            userId));

        // Act
        var courses = await grain.GetCourseSummaryAsync();

        // Assert
        courses.Should().HaveCount(3);
        courses[1].Should().Be(2); // 2 items in course 1
        courses[2].Should().Be(1); // 1 item in course 2
        courses[3].Should().Be(1); // 1 item in course 3
    }

    [Fact]
    public async Task HoldItemsAsync_ShouldFailForClosedOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 10.00m));
        var totals = await grain.GetTotalsAsync();
        await grain.RecordPaymentAsync(Guid.NewGuid(), totals.GrandTotal, 0m, "Cash");
        await grain.CloseAsync(userId);

        // Act
        var act = () => grain.HoldItemsAsync(new HoldItemsCommand(
            new List<Guid> { line1.LineId },
            userId));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    [Fact]
    public async Task FireItemsAsync_ShouldFailWhenNoValidItemsToFire()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 10.00m));
        await grain.SendAsync(userId); // Send the item first

        // Act
        var act = () => grain.FireItemsAsync(new FireItemsCommand(
            new List<Guid> { line1.LineId },
            userId));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No valid items to fire*");
    }

    // ============================================================================
    // Service Charges Tests
    // ============================================================================

    [Fact]
    public async Task AddServiceChargeAsync_ShouldCalculateOnSubtotal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Steak", 1, 100.00m));

        // Act - Add 15% service charge
        await grain.AddServiceChargeAsync("Service Charge", 15m, isTaxable: false);

        // Assert
        var state = await grain.GetStateAsync();
        state.Subtotal.Should().Be(100.00m);
        state.ServiceChargeTotal.Should().Be(15.00m);
        state.ServiceCharges.Should().HaveCount(1);
        state.ServiceCharges[0].Name.Should().Be("Service Charge");
        state.ServiceCharges[0].Rate.Should().Be(15m);
        state.ServiceCharges[0].Amount.Should().Be(15.00m);
    }

    [Fact]
    public async Task AddServiceChargeAsync_Taxable_ShouldIncludeInTax()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Steak", 1, 100.00m, TaxRate: 10m));

        // Act - Add 20% taxable service charge
        await grain.AddServiceChargeAsync("Gratuity", 20m, isTaxable: true);

        // Assert
        var state = await grain.GetStateAsync();
        state.Subtotal.Should().Be(100.00m);
        state.ServiceChargeTotal.Should().Be(20.00m);
        state.TaxTotal.Should().Be(12.00m);
        state.GrandTotal.Should().Be(132.00m);
    }

    [Fact]
    public async Task MultipleServiceCharges_ShouldAccumulate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Dinner", 1, 100.00m));

        // Act
        await grain.AddServiceChargeAsync("Service Charge", 10m, isTaxable: false);
        await grain.AddServiceChargeAsync("Large Party Fee", 5m, isTaxable: false);

        // Assert
        var state = await grain.GetStateAsync();
        state.ServiceCharges.Should().HaveCount(2);
        state.ServiceChargeTotal.Should().Be(15.00m);
    }

    // ============================================================================
    // Discount Removal Tests
    // ============================================================================

    [Fact]
    public async Task RemoveDiscountAsync_ShouldRecalculateTotals()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 100m));
        await grain.ApplyDiscountAsync(new ApplyDiscountCommand("$20 Off", DiscountType.FixedAmount, 20m, userId));

        var stateBeforeRemoval = await grain.GetStateAsync();
        var discountId = stateBeforeRemoval.Discounts[0].Id;

        // Act
        await grain.RemoveDiscountAsync(discountId);

        // Assert
        var state = await grain.GetStateAsync();
        state.Discounts.Should().BeEmpty();
        state.DiscountTotal.Should().Be(0m);
        state.GrandTotal.Should().Be(100m);
    }

    // ============================================================================
    // Payment Removal Tests
    // ============================================================================

    [Fact]
    public async Task RemovePaymentAsync_ShouldUpdateStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 100m));
        var totals = await grain.GetTotalsAsync();
        await grain.RecordPaymentAsync(paymentId, totals.GrandTotal, 0m, "Cash");

        var stateBeforeRemoval = await grain.GetStateAsync();
        stateBeforeRemoval.Status.Should().Be(OrderStatus.Paid);

        // Act
        await grain.RemovePaymentAsync(paymentId);

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(OrderStatus.Open);
        state.PaidAmount.Should().Be(0m);
        state.BalanceDue.Should().Be(totals.GrandTotal);
        state.Payments.Should().BeEmpty();
    }

    // ============================================================================
    // Order Reopening Tests
    // ============================================================================

    [Fact]
    public async Task ReopenAsync_ClosedOrder_ShouldRevertToOpen()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 100m));
        var totals = await grain.GetTotalsAsync();
        await grain.RecordPaymentAsync(Guid.NewGuid(), totals.GrandTotal, 0m, "Cash");
        await grain.CloseAsync(userId);

        var closedState = await grain.GetStateAsync();
        closedState.Status.Should().Be(OrderStatus.Closed);

        // Act
        await grain.ReopenAsync(userId, "Need to add more items");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(OrderStatus.Open);
    }

    [Fact]
    public async Task ReopenAsync_OpenOrder_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 100m));

        // Act
        var act = () => grain.ReopenAsync(userId, "Invalid reopen");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only reopen closed or voided*");
    }

    // ============================================================================
    // State Transition Guards Tests
    // ============================================================================

    [Fact]
    public async Task VoidAsync_ClosedOrder_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 100m));
        var totals = await grain.GetTotalsAsync();
        await grain.RecordPaymentAsync(Guid.NewGuid(), totals.GrandTotal, 0m, "Cash");
        await grain.CloseAsync(userId);

        // Act
        var act = () => grain.VoidAsync(new VoidOrderCommand(userId, "Try to void closed"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    [Fact]
    public async Task CloseAsync_ClosedOrder_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 100m));
        var totals = await grain.GetTotalsAsync();
        await grain.RecordPaymentAsync(Guid.NewGuid(), totals.GrandTotal, 0m, "Cash");
        await grain.CloseAsync(userId);

        // Act
        var act = () => grain.CloseAsync(userId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ============================================================================
    // Validation Tests
    // ============================================================================

    [Fact]
    public async Task AddLineAsync_WithNegativeQuantity_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Act
        var act = () => grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", -1, 10m));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Quantity must be greater than zero*");
    }

    // ============================================================================
    // Payment Edge Cases Tests
    // ============================================================================

    [Fact]
    public async Task RecordPaymentAsync_Overpayment_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Coffee", 1, 5.00m));

        // Act - Pay more than owed
        await grain.RecordPaymentAsync(Guid.NewGuid(), 10.00m, 0m, "Cash");

        // Assert
        var state = await grain.GetStateAsync();
        state.PaidAmount.Should().Be(10.00m);
        state.BalanceDue.Should().BeLessThan(0);
        state.Status.Should().Be(OrderStatus.Paid);
    }

    // ============================================================================
    // Kitchen Ticket Tests
    // ============================================================================

    private IKitchenTicketGrain GetKitchenTicketGrain(Guid orgId, Guid siteId, Guid ticketId)
        => _fixture.Cluster.GrainFactory.GetGrain<IKitchenTicketGrain>(GrainKeys.KitchenOrder(orgId, siteId, ticketId));

    [Fact]
    public async Task KitchenTicket_ReceiveAsync_ShouldMarkReceived()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetKitchenTicketGrain(orgId, siteId, ticketId);

        await grain.CreateAsync(new CreateKitchenTicketCommand(
            orgId, siteId, orderId, "ORD-001", OrderType.DineIn, "T1", 2, "John"));

        var stateBefore = await grain.GetStateAsync();
        stateBefore.ReceivedAt.Should().BeNull();

        // Act
        await grain.ReceiveAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.ReceivedAt.Should().NotBeNull();
        state.ReceivedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task KitchenTicket_VoidAlreadyVoided_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetKitchenTicketGrain(orgId, siteId, ticketId);

        await grain.CreateAsync(new CreateKitchenTicketCommand(
            orgId, siteId, orderId, "ORD-001", OrderType.DineIn, "T1", 2, "John"));

        await grain.VoidAsync("First void");

        var voidedState = await grain.GetStateAsync();
        voidedState.Status.Should().Be(TicketStatus.Voided);

        // Act - Try to void again
        var act = () => grain.VoidAsync("Second void");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot void*");
    }
}
