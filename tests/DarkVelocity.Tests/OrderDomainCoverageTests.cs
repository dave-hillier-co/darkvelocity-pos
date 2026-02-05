using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Comprehensive tests for the Orders domain covering:
/// - Event sourcing validation (event replay, state reconstruction)
/// - Hold/Fire state machine edge cases
/// - Order reopening scenarios
/// - Discount stacking rules
/// - Tax calculation edge cases
/// - Invalid state transitions
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class OrderDomainCoverageTests
{
    private readonly TestClusterFixture _fixture;

    public OrderDomainCoverageTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IOrderGrain GetOrderGrain(Guid orgId, Guid siteId, Guid orderId)
        => _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));

    // ============================================================================
    // Event Sourcing Validation Tests
    // ============================================================================

    #region Event Sourcing - State Consistency

    [Fact]
    public async Task State_ShouldBeConsistent_AfterMultipleOperations()
    {
        // Arrange - Complex sequence of operations
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        // Act - Perform sequence
        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 2, 15.00m, TaxRate: 10m));
        var line2 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Beer", 1, 8.00m, TaxRate: 20m));
        await grain.UpdateLineAsync(new UpdateLineCommand(line1.LineId, Quantity: 1));
        await grain.VoidLineAsync(new VoidLineCommand(line2.LineId, userId, "Customer changed mind"));
        await grain.ApplyDiscountAsync(new ApplyDiscountCommand("10% off", DiscountType.Percentage, 10m, userId));

        // Assert - State should reflect all operations correctly
        var state = await grain.GetStateAsync();
        state.Lines.Should().HaveCount(2);
        state.Lines[0].Quantity.Should().Be(1); // Updated
        state.Lines[0].LineTotal.Should().Be(15.00m);
        state.Lines[1].Status.Should().Be(OrderLineStatus.Voided);
        state.Subtotal.Should().Be(15.00m); // Only non-voided line
        state.DiscountTotal.Should().Be(1.50m); // 10% of 15
        state.TaxTotal.Should().Be(1.50m); // 10% of 15 (voided line not taxed)
        state.GrandTotal.Should().Be(15.00m); // 15 - 1.50 + 1.50
    }

    [Fact]
    public async Task State_ShouldMaintainIntegrity_AfterPaymentOperations()
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

        // Act - Multiple partial payments
        await grain.RecordPaymentAsync(Guid.NewGuid(), 30m, 0m, "Cash");
        await grain.RecordPaymentAsync(Guid.NewGuid(), 30m, 5m, "Card");
        await grain.RecordPaymentAsync(Guid.NewGuid(), 40m, 10m, "Card");

        // Assert
        var state = await grain.GetStateAsync();
        state.Payments.Should().HaveCount(3);
        state.PaidAmount.Should().Be(100m);
        state.TipTotal.Should().Be(15m);
        state.BalanceDue.Should().Be(0m);
        state.Status.Should().Be(OrderStatus.Paid);
    }

    #endregion

    // ============================================================================
    // Hold/Fire State Machine Edge Cases
    // ============================================================================

    #region Hold/Fire - Edge Cases

    [Fact]
    public async Task HoldItemsAsync_WithEmptyLineIds_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 10.00m));

        // Act
        var act = () => grain.HoldItemsAsync(new HoldItemsCommand(
            new List<Guid>(), // Empty list
            userId,
            "Test"));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*At least one line*");
    }

    [Fact]
    public async Task HoldItemsAsync_WithNonExistentLineId_ShouldNotHoldAnything()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 10.00m));

        // Act
        var act = () => grain.HoldItemsAsync(new HoldItemsCommand(
            new List<Guid> { Guid.NewGuid() }, // Non-existent ID
            userId,
            "Test"));

        // Assert - Should throw because no valid items to hold
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No valid pending items to hold*");
    }

    [Fact]
    public async Task HoldItemsAsync_AlreadySentItem_ShouldNotBeHeld()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Appetizer", 1, 10.00m));
        var line2 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Main", 1, 20.00m));

        // Send appetizer
        await grain.FireItemsAsync(new FireItemsCommand(new List<Guid> { line1.LineId }, userId));

        // Act - Try to hold the sent item
        var act = () => grain.HoldItemsAsync(new HoldItemsCommand(
            new List<Guid> { line1.LineId },
            userId,
            "Test"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No valid pending items to hold*");
    }

    [Fact]
    public async Task ReleaseItemsAsync_WithNonHeldItem_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 10.00m));
        // Item is NOT held

        // Act - Try to release non-held item
        var act = () => grain.ReleaseItemsAsync(new ReleaseItemsCommand(
            new List<Guid> { line1.LineId },
            userId));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No valid held items to release*");
    }

    [Fact]
    public async Task SetItemCourseAsync_WithInvalidCourseNumber_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 10.00m));

        // Act - Try to set course 0
        var act = () => grain.SetItemCourseAsync(new SetItemCourseCommand(
            new List<Guid> { line1.LineId },
            0, // Invalid course number
            userId));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Course number must be at least 1*");
    }

    [Fact]
    public async Task FireCourseAsync_WithNoItemsInCourse_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 10.00m));

        // Set item to course 1
        await grain.SetItemCourseAsync(new SetItemCourseCommand(
            new List<Guid> { line1.LineId }, 1, userId));

        // Act - Try to fire course 2 (no items)
        var act = () => grain.FireCourseAsync(new FireCourseCommand(2, userId));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No pending items in course 2*");
    }

    [Fact]
    public async Task FireAllAsync_WithNoPendingItems_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 10.00m));

        // Fire all items first
        await grain.FireAllAsync(userId);

        // Act - Try to fire again
        var act = () => grain.FireAllAsync(userId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No pending items to fire*");
    }

    [Fact]
    public async Task HoldItems_ThenVoidLine_ShouldRemoveFromHoldSummary()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item1", 1, 10.00m));
        var line2 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item2", 1, 20.00m));

        // Hold both items
        await grain.HoldItemsAsync(new HoldItemsCommand(
            new List<Guid> { line1.LineId, line2.LineId }, userId, "Wait"));

        var summaryBefore = await grain.GetHoldSummaryAsync();
        summaryBefore.TotalHeldCount.Should().Be(2);

        // Act - Void one item
        await grain.VoidLineAsync(new VoidLineCommand(line1.LineId, userId, "Cancelled"));

        // Assert - Hold summary should still show 1 held item
        var summaryAfter = await grain.GetHoldSummaryAsync();
        summaryAfter.TotalHeldCount.Should().Be(1);
        summaryAfter.HeldLineIds.Should().Contain(line2.LineId);
        summaryAfter.HeldLineIds.Should().NotContain(line1.LineId);
    }

    #endregion

    // ============================================================================
    // Order Reopening Scenarios
    // ============================================================================

    #region Order Reopening

    [Fact]
    public async Task ReopenAsync_ClosedOrder_ShouldReopenSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 50.00m));
        var totals = await grain.GetTotalsAsync();
        await grain.RecordPaymentAsync(Guid.NewGuid(), totals.GrandTotal, 0m, "Cash");
        await grain.CloseAsync(userId);

        var closedState = await grain.GetStateAsync();
        closedState.Status.Should().Be(OrderStatus.Closed);
        closedState.ClosedAt.Should().NotBeNull();

        // Act
        await grain.ReopenAsync(userId, "Customer wants to add more items");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(OrderStatus.Open);
        state.ClosedAt.Should().BeNull();
    }

    [Fact]
    public async Task ReopenAsync_VoidedOrder_ShouldReopenSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 50.00m));
        await grain.VoidAsync(new VoidOrderCommand(userId, "Mistake"));

        var voidedState = await grain.GetStateAsync();
        voidedState.Status.Should().Be(OrderStatus.Voided);
        voidedState.VoidedAt.Should().NotBeNull();
        voidedState.VoidReason.Should().Be("Mistake");

        // Act
        await grain.ReopenAsync(userId, "Void was a mistake");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(OrderStatus.Open);
        state.VoidedAt.Should().BeNull();
        state.VoidReason.Should().BeNull();
    }

    [Fact]
    public async Task ReopenedOrder_ShouldAcceptNewLines()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item1", 1, 30.00m));
        var totals = await grain.GetTotalsAsync();
        await grain.RecordPaymentAsync(Guid.NewGuid(), totals.GrandTotal, 0m, "Cash");
        await grain.CloseAsync(userId);
        await grain.ReopenAsync(userId, "Add more items");

        // Act
        var line2 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item2", 1, 20.00m));

        // Assert
        var state = await grain.GetStateAsync();
        state.Lines.Should().HaveCount(2);
        state.Subtotal.Should().Be(50.00m);
        state.BalanceDue.Should().Be(20.00m); // Already paid 30, added 20 more
    }

    [Fact]
    public async Task ReopenAsync_FromPartiallyPaid_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 100.00m));
        await grain.RecordPaymentAsync(Guid.NewGuid(), 50m, 0m, "Cash"); // Partial payment

        // Act
        var act = () => grain.ReopenAsync(userId, "Test");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only reopen closed or voided*");
    }

    #endregion

    // ============================================================================
    // Discount Stacking Rules
    // ============================================================================

    #region Discount Stacking

    [Fact]
    public async Task MultiplePercentageDiscounts_ShouldStack()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 100.00m));

        // Act - Apply two percentage discounts
        await grain.ApplyDiscountAsync(new ApplyDiscountCommand("10% off", DiscountType.Percentage, 10m, userId));
        await grain.ApplyDiscountAsync(new ApplyDiscountCommand("5% off", DiscountType.Percentage, 5m, userId));

        // Assert - Both discounts calculated on subtotal
        var state = await grain.GetStateAsync();
        state.Discounts.Should().HaveCount(2);
        state.DiscountTotal.Should().Be(15.00m); // 10% + 5% of 100
    }

    [Fact]
    public async Task PercentageAndFixedDiscounts_ShouldStack()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 100.00m));

        // Act
        await grain.ApplyDiscountAsync(new ApplyDiscountCommand("10% off", DiscountType.Percentage, 10m, userId));
        await grain.ApplyDiscountAsync(new ApplyDiscountCommand("$5 off", DiscountType.FixedAmount, 5m, userId));

        // Assert
        var state = await grain.GetStateAsync();
        state.Discounts.Should().HaveCount(2);
        state.DiscountTotal.Should().Be(15.00m); // 10 + 5
    }

    [Fact]
    public async Task LineDiscountAndOrderDiscount_ShouldBothApply()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line1 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item1", 1, 60.00m));
        var line2 = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item2", 1, 40.00m));

        // Act - Apply line-level discount to Item1 and order-level discount
        await grain.ApplyLineDiscountAsync(new ApplyLineDiscountCommand(
            line1.LineId, DiscountType.FixedAmount, 10m, userId, "Employee discount"));
        await grain.ApplyDiscountAsync(new ApplyDiscountCommand("10% order", DiscountType.Percentage, 10m, userId));

        // Assert
        var state = await grain.GetStateAsync();
        // Line discount: $10 on Item1
        // Order discount: 10% of $100 (subtotal) = $10
        // Total discounts: $20
        state.Lines[0].LineDiscountAmount.Should().Be(10.00m);
        state.Discounts.Should().HaveCount(1);
        state.DiscountTotal.Should().Be(20.00m);
    }

    [Fact]
    public async Task LinePercentageDiscount_ShouldCalculateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 80.00m));

        // Act - Apply 25% line discount
        await grain.ApplyLineDiscountAsync(new ApplyLineDiscountCommand(
            line.LineId, DiscountType.Percentage, 25m, userId, "Manager comp"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Lines[0].LineDiscountAmount.Should().Be(20.00m); // 25% of 80
        state.DiscountTotal.Should().Be(20.00m);
        state.GrandTotal.Should().Be(60.00m);
    }

    [Fact]
    public async Task RemoveLineDiscountAsync_ShouldRecalculateTotals()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 100.00m));
        await grain.ApplyLineDiscountAsync(new ApplyLineDiscountCommand(
            line.LineId, DiscountType.FixedAmount, 20m, userId, "Test"));

        var discountedState = await grain.GetStateAsync();
        discountedState.DiscountTotal.Should().Be(20.00m);

        // Act
        await grain.RemoveLineDiscountAsync(line.LineId, userId);

        // Assert
        var state = await grain.GetStateAsync();
        state.Lines[0].LineDiscountAmount.Should().Be(0m);
        state.DiscountTotal.Should().Be(0m);
        state.GrandTotal.Should().Be(100.00m);
    }

    [Fact]
    public async Task ApplyLineDiscountAsync_OnVoidedLine_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 100.00m));
        await grain.VoidLineAsync(new VoidLineCommand(line.LineId, userId, "Wrong item"));

        // Act
        var act = () => grain.ApplyLineDiscountAsync(new ApplyLineDiscountCommand(
            line.LineId, DiscountType.FixedAmount, 10m, userId, "Test"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*voided item*");
    }

    [Fact]
    public async Task LineDiscount_ShouldNotExceedLineTotal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 50.00m));

        // Act - Try to apply discount greater than line total
        await grain.ApplyLineDiscountAsync(new ApplyLineDiscountCommand(
            line.LineId, DiscountType.FixedAmount, 100m, userId, "Big discount"));

        // Assert - Discount should be capped at line total
        var state = await grain.GetStateAsync();
        state.Lines[0].LineDiscountAmount.Should().Be(50.00m);
        state.DiscountTotal.Should().Be(50.00m);
        state.GrandTotal.Should().Be(0.00m);
    }

    #endregion

    // ============================================================================
    // Tax Calculation Edge Cases
    // ============================================================================

    #region Tax Edge Cases

    [Fact]
    public async Task TaxCalculation_WithZeroRatedItems_ShouldNotChargeZeroTax()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Add zero-rated item (like gift card or some groceries)
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Gift Card", 1, 50.00m, TaxRate: 0m));

        // Assert
        var state = await grain.GetStateAsync();
        state.Subtotal.Should().Be(50.00m);
        state.TaxTotal.Should().Be(0m);
        state.GrandTotal.Should().Be(50.00m);
    }

    [Fact]
    public async Task TaxCalculation_MixedRatesAfterVoid_ShouldRecalculate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Add items with different tax rates
        var food = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 20.00m, TaxRate: 10m)); // Tax: 2.00
        var alcohol = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Beer", 1, 10.00m, TaxRate: 20m)); // Tax: 2.00

        var stateBeforeVoid = await grain.GetStateAsync();
        stateBeforeVoid.TaxTotal.Should().Be(4.00m);

        // Act - Void the higher-taxed item
        await grain.VoidLineAsync(new VoidLineCommand(alcohol.LineId, userId, "Customer changed mind"));

        // Assert - Tax should be recalculated
        var state = await grain.GetStateAsync();
        state.Subtotal.Should().Be(20.00m);
        state.TaxTotal.Should().Be(2.00m); // Only burger tax
        state.GrandTotal.Should().Be(22.00m);
    }

    [Fact]
    public async Task TaxCalculation_WithServiceChargeOnMixedRates_ShouldUseWeightedAverage()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Add items with different tax rates
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Food", 1, 60.00m, TaxRate: 10m)); // Tax: 6.00
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Alcohol", 1, 40.00m, TaxRate: 20m)); // Tax: 8.00

        // Act - Add taxable service charge
        await grain.AddServiceChargeAsync("Gratuity", 10m, isTaxable: true);

        // Assert
        var state = await grain.GetStateAsync();
        // Weighted tax rate: (60*10 + 40*20) / 100 = (600+800)/100 = 14%
        // Service charge: $10
        // Tax on service charge: $10 * 14% = $1.40
        // Total tax: 6.00 + 8.00 + 1.40 = 15.40
        state.ServiceChargeTotal.Should().Be(10.00m);
        state.TaxTotal.Should().Be(15.40m);
    }

    [Fact]
    public async Task TaxCalculation_AfterQuantityUpdate_ShouldRecalculate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 25.00m, TaxRate: 8m));

        var initialState = await grain.GetStateAsync();
        initialState.TaxTotal.Should().Be(2.00m); // 8% of 25

        // Act - Increase quantity
        await grain.UpdateLineAsync(new UpdateLineCommand(line.LineId, Quantity: 4));

        // Assert
        var state = await grain.GetStateAsync();
        state.Subtotal.Should().Be(100.00m);
        state.TaxTotal.Should().Be(8.00m); // 8% of 100
        state.GrandTotal.Should().Be(108.00m);
    }

    [Fact]
    public async Task TaxCalculation_WithModifiers_ShouldIncludeModifierInTaxBase()
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
            new() { ModifierId = Guid.NewGuid(), Name = "Extra Cheese", Price = 2.00m, Quantity = 1 },
            new() { ModifierId = Guid.NewGuid(), Name = "Bacon", Price = 3.00m, Quantity = 1 }
        };

        // Act
        await grain.AddLineAsync(new AddLineCommand(
            Guid.NewGuid(), "Burger", 1, 15.00m,
            TaxRate: 10m, Modifiers: modifiers));

        // Assert
        var state = await grain.GetStateAsync();
        // Line total: 15 + 2 + 3 = 20
        // Tax: 20 * 10% = 2
        state.Subtotal.Should().Be(20.00m);
        state.TaxTotal.Should().Be(2.00m);
        state.GrandTotal.Should().Be(22.00m);
    }

    #endregion

    // ============================================================================
    // Price Override Tests
    // ============================================================================

    #region Price Override

    [Fact]
    public async Task OverridePriceAsync_ShouldUpdateLineAndRecalculate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 2, 50.00m, TaxRate: 10m));

        // Act
        await grain.OverridePriceAsync(new OverridePriceCommand(
            line.LineId, 30.00m, "Manager discount", userId));

        // Assert
        var state = await grain.GetStateAsync();
        state.Lines[0].UnitPrice.Should().Be(30.00m);
        state.Lines[0].OriginalPrice.Should().Be(50.00m);
        state.Lines[0].PriceOverrideReason.Should().Be("Manager discount");
        state.Subtotal.Should().Be(60.00m); // 30 * 2
        state.TaxTotal.Should().Be(6.00m); // 10% of 60
    }

    [Fact]
    public async Task OverridePriceAsync_WithoutReason_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 50.00m));

        // Act
        var act = () => grain.OverridePriceAsync(new OverridePriceCommand(
            line.LineId, 30.00m, "", userId)); // Empty reason

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Reason is required*");
    }

    [Fact]
    public async Task OverridePriceAsync_WithNegativePrice_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 50.00m));

        // Act
        var act = () => grain.OverridePriceAsync(new OverridePriceCommand(
            line.LineId, -10.00m, "Test", userId));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public async Task OverridePriceAsync_ToZero_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 50.00m));

        // Act - Comp the item (price override to $0)
        await grain.OverridePriceAsync(new OverridePriceCommand(
            line.LineId, 0.00m, "Full comp - manager", userId));

        // Assert
        var state = await grain.GetStateAsync();
        state.Lines[0].UnitPrice.Should().Be(0m);
        state.GrandTotal.Should().Be(0m);
    }

    #endregion

    // ============================================================================
    // Seat Assignment Tests
    // ============================================================================

    #region Seat Assignment

    [Fact]
    public async Task AssignSeatAsync_ShouldSetSeatNumber()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 15.00m));

        // Act
        await grain.AssignSeatAsync(new AssignSeatCommand(line.LineId, 3, userId));

        // Assert
        var state = await grain.GetStateAsync();
        state.Lines[0].Seat.Should().Be(3);
    }

    [Fact]
    public async Task AssignSeatAsync_WithInvalidSeatNumber_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 15.00m));

        // Act
        var act = () => grain.AssignSeatAsync(new AssignSeatCommand(line.LineId, 0, userId));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Seat number must be at least 1*");
    }

    [Fact]
    public async Task AssignSeatAsync_OnVoidedLine_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        var line = await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 15.00m));
        await grain.VoidLineAsync(new VoidLineCommand(line.LineId, userId, "Cancelled"));

        // Act
        var act = () => grain.AssignSeatAsync(new AssignSeatCommand(line.LineId, 1, userId));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*voided item*");
    }

    [Fact]
    public async Task AssignSeatAsync_WithSeatOnLineCreation_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Act - Add line with seat assignment
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 15.00m, Seat: 2));

        // Assert
        var state = await grain.GetStateAsync();
        state.Lines[0].Seat.Should().Be(2);
    }

    #endregion

    // ============================================================================
    // Order Merge Tests
    // ============================================================================

    #region Order Merge

    [Fact]
    public async Task MergeFromOrderAsync_ShouldMoveAllLinesAndPayments()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var targetOrderId = Guid.NewGuid();
        var sourceOrderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var targetGrain = GetOrderGrain(orgId, siteId, targetOrderId);
        var sourceGrain = GetOrderGrain(orgId, siteId, sourceOrderId);

        // Create target order with one item
        await targetGrain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await targetGrain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Target Item", 1, 50.00m));

        // Create source order with items and partial payment
        await sourceGrain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await sourceGrain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Source Item 1", 1, 30.00m));
        await sourceGrain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Source Item 2", 1, 20.00m));
        await sourceGrain.RecordPaymentAsync(Guid.NewGuid(), 25.00m, 0m, "Cash");

        // Act
        var result = await targetGrain.MergeFromOrderAsync(new MergeFromOrderCommand(sourceOrderId, userId));

        // Assert
        result.LinesMerged.Should().Be(2);
        result.PaymentsMerged.Should().Be(1);

        var targetState = await targetGrain.GetStateAsync();
        targetState.Lines.Should().HaveCount(3);
        targetState.Subtotal.Should().Be(100.00m);
        targetState.PaidAmount.Should().Be(25.00m);
        targetState.BalanceDue.Should().Be(75.00m);

        var sourceState = await sourceGrain.GetStateAsync();
        sourceState.Status.Should().Be(OrderStatus.Closed);
    }

    [Fact]
    public async Task MergeFromOrderAsync_FromClosedOrder_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var targetOrderId = Guid.NewGuid();
        var sourceOrderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var targetGrain = GetOrderGrain(orgId, siteId, targetOrderId);
        var sourceGrain = GetOrderGrain(orgId, siteId, sourceOrderId);

        await targetGrain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await targetGrain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 50.00m));

        await sourceGrain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await sourceGrain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 30.00m));
        var totals = await sourceGrain.GetTotalsAsync();
        await sourceGrain.RecordPaymentAsync(Guid.NewGuid(), totals.GrandTotal, 0m, "Cash");
        await sourceGrain.CloseAsync(userId);

        // Act
        var act = () => targetGrain.MergeFromOrderAsync(new MergeFromOrderCommand(sourceOrderId, userId));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    [Fact]
    public async Task MergeFromOrderAsync_WhenTargetClosed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var targetOrderId = Guid.NewGuid();
        var sourceOrderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var targetGrain = GetOrderGrain(orgId, siteId, targetOrderId);
        var sourceGrain = GetOrderGrain(orgId, siteId, sourceOrderId);

        await targetGrain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await targetGrain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 50.00m));
        var totals = await targetGrain.GetTotalsAsync();
        await targetGrain.RecordPaymentAsync(Guid.NewGuid(), totals.GrandTotal, 0m, "Cash");
        await targetGrain.CloseAsync(userId);

        await sourceGrain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await sourceGrain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 30.00m));

        // Act
        var act = () => targetGrain.MergeFromOrderAsync(new MergeFromOrderCommand(sourceOrderId, userId));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    #endregion

    // ============================================================================
    // Payment Edge Cases
    // ============================================================================

    #region Payment Edge Cases

    [Fact]
    public async Task RecordPaymentAsync_NegativeAmount_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 50.00m));

        // Act
        var act = () => grain.RecordPaymentAsync(Guid.NewGuid(), -10m, 0m, "Cash");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public async Task RecordPaymentAsync_NegativeTip_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 50.00m));

        // Act
        var act = () => grain.RecordPaymentAsync(Guid.NewGuid(), 50m, -5m, "Cash");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Tip amount cannot be negative*");
    }

    [Fact]
    public async Task RecordPaymentAsync_ZeroWithOutstandingBalance_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 50.00m));

        // Act
        var act = () => grain.RecordPaymentAsync(Guid.NewGuid(), 0m, 0m, "Cash");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*must be greater than zero when balance is due*");
    }

    [Fact]
    public async Task RemovePaymentAsync_ShouldRecalculateBalanceAndStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 100.00m));
        await grain.RecordPaymentAsync(paymentId, 100m, 10m, "Cash");

        var paidState = await grain.GetStateAsync();
        paidState.Status.Should().Be(OrderStatus.Paid);

        // Act
        await grain.RemovePaymentAsync(paymentId);

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(OrderStatus.Open);
        state.PaidAmount.Should().Be(0m);
        state.TipTotal.Should().Be(0m);
        state.BalanceDue.Should().Be(100m);
    }

    #endregion

    // ============================================================================
    // Validation Edge Cases
    // ============================================================================

    #region Validation Edge Cases

    [Fact]
    public async Task CreateAsync_WithZeroGuestCount_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        // Act
        var act = () => grain.CreateAsync(new CreateOrderCommand(
            orgId, siteId, userId, OrderType.DineIn, GuestCount: 0));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Guest count must be greater than zero*");
    }

    [Fact]
    public async Task CreateAsync_AlreadyExists_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Act
        var act = () => grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.TakeOut));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task AddLineAsync_WithEmptyName_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Act
        var act = () => grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "", 1, 10.00m));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Name cannot be empty*");
    }

    [Fact]
    public async Task AddLineAsync_WithNegativePrice_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Act
        var act = () => grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, -10.00m));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public async Task AddLineAsync_WithZeroQuantity_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Act
        var act = () => grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 0, 10.00m));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Quantity must be greater than zero*");
    }

    [Fact]
    public async Task AddLineAsync_WithNegativeTaxRate_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Act
        var act = () => grain.AddLineAsync(new AddLineCommand(
            Guid.NewGuid(), "Item", 1, 10.00m, TaxRate: -5m));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Tax rate cannot be negative*");
    }

    [Fact]
    public async Task UpdateLineAsync_NonExistentLine_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Act
        var act = () => grain.UpdateLineAsync(new UpdateLineCommand(Guid.NewGuid(), Quantity: 5));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Line not found*");
    }

    [Fact]
    public async Task VoidLineAsync_NonExistentLine_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Act
        var act = () => grain.VoidLineAsync(new VoidLineCommand(Guid.NewGuid(), userId, "Test"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Line not found*");
    }

    [Fact]
    public async Task RemoveLineAsync_NonExistentLine_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Act
        var act = () => grain.RemoveLineAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Line not found*");
    }

    #endregion
}
