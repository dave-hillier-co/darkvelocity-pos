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
}
