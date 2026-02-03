using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for payment split scenarios - multiple payment methods per order,
/// partial payments, and split bill handling.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PaymentSplitScenarioTests
{
    private readonly TestClusterFixture _fixture;

    public PaymentSplitScenarioTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // ============================================================================
    // Multiple Payment Methods on Single Order
    // ============================================================================

    [Fact]
    public async Task Order_MultiplePayments_CashAndCard_ShouldTrackBoth()
    {
        // Arrange
        var order = await CreateOrderWithLinesAsync(100.00m);
        var cashPaymentId = Guid.NewGuid();
        var cardPaymentId = Guid.NewGuid();

        // Act - Pay $60 cash, $40 card
        await order.RecordPaymentAsync(cashPaymentId, 60.00m, 0, "Cash");
        await order.RecordPaymentAsync(cardPaymentId, 40.00m, 5.00m, "CreditCard");

        // Assert
        var state = await order.GetStateAsync();
        state.PaidAmount.Should().Be(100.00m);
        state.TipTotal.Should().Be(5.00m);
        state.BalanceDue.Should().BeApproximately(0, 0.02m); // Small tolerance for tax rounding
        state.Status.Should().Be(OrderStatus.Paid);
        state.Payments.Should().HaveCount(2);
    }

    [Fact]
    public async Task Order_MultiplePayments_ThreeWaySplit_ShouldTrackAll()
    {
        // Arrange
        var order = await CreateOrderWithLinesAsync(150.00m);

        // Act - Three-way split: $50 each
        await order.RecordPaymentAsync(Guid.NewGuid(), 50.00m, 0, "Cash");
        await order.RecordPaymentAsync(Guid.NewGuid(), 50.00m, 5.00m, "CreditCard");
        await order.RecordPaymentAsync(Guid.NewGuid(), 50.00m, 5.00m, "DebitCard");

        // Assert
        var state = await order.GetStateAsync();
        state.PaidAmount.Should().Be(150.00m);
        state.TipTotal.Should().Be(10.00m);
        state.BalanceDue.Should().BeApproximately(0, 0.02m);
        state.Status.Should().Be(OrderStatus.Paid);
        state.Payments.Should().HaveCount(3);
    }

    [Fact]
    public async Task Order_MultiplePayments_WithGiftCard_ShouldCombine()
    {
        // Arrange
        var order = await CreateOrderWithLinesAsync(75.00m);

        // Act - $25 gift card + $50 cash
        await order.RecordPaymentAsync(Guid.NewGuid(), 25.00m, 0, "GiftCard");
        await order.RecordPaymentAsync(Guid.NewGuid(), 50.00m, 3.00m, "Cash");

        // Assert
        var state = await order.GetStateAsync();
        state.PaidAmount.Should().Be(75.00m);
        state.TipTotal.Should().Be(3.00m);
        state.BalanceDue.Should().BeApproximately(0, 0.02m);
        state.Status.Should().Be(OrderStatus.Paid);
    }

    // ============================================================================
    // Partial Payment Scenarios
    // ============================================================================

    [Fact]
    public async Task Order_PartialPayment_ShouldSetPartiallyPaidStatus()
    {
        // Arrange
        var order = await CreateOrderWithLinesAsync(100.00m);

        // Act
        await order.RecordPaymentAsync(Guid.NewGuid(), 40.00m, 0, "Cash");

        // Assert
        var state = await order.GetStateAsync();
        state.PaidAmount.Should().Be(40.00m);
        state.BalanceDue.Should().BeApproximately(60.00m, 0.02m);
        state.Status.Should().Be(OrderStatus.PartiallyPaid);
    }

    [Fact]
    public async Task Order_PartialPayment_ThenFullPayment_ShouldCompletePaid()
    {
        // Arrange
        var order = await CreateOrderWithLinesAsync(100.00m);

        // Act - First partial payment
        await order.RecordPaymentAsync(Guid.NewGuid(), 30.00m, 0, "Cash");

        var stateAfterPartial = await order.GetStateAsync();
        stateAfterPartial.Status.Should().Be(OrderStatus.PartiallyPaid);
        stateAfterPartial.BalanceDue.Should().BeApproximately(70.00m, 0.02m);

        // Complete payment
        await order.RecordPaymentAsync(Guid.NewGuid(), 70.00m, 5.00m, "CreditCard");

        // Assert
        var state = await order.GetStateAsync();
        state.PaidAmount.Should().Be(100.00m);
        state.BalanceDue.Should().BeApproximately(0, 0.02m);
        state.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task Order_MultiplePartialPayments_ShouldAccumulate()
    {
        // Arrange
        var order = await CreateOrderWithLinesAsync(200.00m);

        // Act - Multiple small payments
        await order.RecordPaymentAsync(Guid.NewGuid(), 50.00m, 0, "Cash");
        await order.RecordPaymentAsync(Guid.NewGuid(), 50.00m, 0, "Cash");
        await order.RecordPaymentAsync(Guid.NewGuid(), 50.00m, 0, "CreditCard");

        var stateIntermediate = await order.GetStateAsync();
        stateIntermediate.Status.Should().Be(OrderStatus.PartiallyPaid);
        stateIntermediate.BalanceDue.Should().BeApproximately(50.00m, 0.02m);

        // Final payment
        await order.RecordPaymentAsync(Guid.NewGuid(), 50.00m, 10.00m, "CreditCard");

        // Assert
        var state = await order.GetStateAsync();
        state.PaidAmount.Should().Be(200.00m);
        state.TipTotal.Should().Be(10.00m);
        state.Status.Should().Be(OrderStatus.Paid);
        state.Payments.Should().HaveCount(4);
    }

    // ============================================================================
    // Overpayment Scenarios
    // ============================================================================

    [Fact]
    public async Task Order_Overpayment_ShouldSetPaidStatus()
    {
        // Arrange
        var order = await CreateOrderWithLinesAsync(50.00m);

        // Act - Customer pays more than owed (e.g., rounds up)
        await order.RecordPaymentAsync(Guid.NewGuid(), 60.00m, 0, "Cash");

        // Assert
        var state = await order.GetStateAsync();
        state.PaidAmount.Should().Be(60.00m);
        state.BalanceDue.Should().BeApproximately(-10.00m, 0.02m); // Negative balance = overpayment
        state.Status.Should().Be(OrderStatus.Paid);
    }

    // ============================================================================
    // Payment Removal Scenarios
    // ============================================================================

    [Fact]
    public async Task Order_RemovePayment_ShouldUpdateBalance()
    {
        // Arrange
        var order = await CreateOrderWithLinesAsync(100.00m);
        var paymentId = Guid.NewGuid();

        await order.RecordPaymentAsync(paymentId, 100.00m, 5.00m, "CreditCard");

        var stateAfterPayment = await order.GetStateAsync();
        stateAfterPayment.Status.Should().Be(OrderStatus.Paid);

        // Act
        await order.RemovePaymentAsync(paymentId);

        // Assert
        var state = await order.GetStateAsync();
        state.PaidAmount.Should().Be(0);
        state.TipTotal.Should().Be(0);
        state.BalanceDue.Should().BeApproximately(100.00m, 0.02m);
        state.Status.Should().Be(OrderStatus.Open);
    }

    [Fact]
    public async Task Order_RemoveOneOfMultiplePayments_ShouldRevertToPartiallyPaid()
    {
        // Arrange
        var order = await CreateOrderWithLinesAsync(100.00m);
        var payment1Id = Guid.NewGuid();
        var payment2Id = Guid.NewGuid();

        await order.RecordPaymentAsync(payment1Id, 60.00m, 0, "Cash");
        await order.RecordPaymentAsync(payment2Id, 40.00m, 0, "CreditCard");

        var statePaid = await order.GetStateAsync();
        statePaid.Status.Should().Be(OrderStatus.Paid);

        // Act - Remove the card payment
        await order.RemovePaymentAsync(payment2Id);

        // Assert
        var state = await order.GetStateAsync();
        state.PaidAmount.Should().Be(60.00m);
        state.BalanceDue.Should().BeApproximately(40.00m, 0.02m);
        state.Status.Should().Be(OrderStatus.PartiallyPaid);
        state.Payments.Should().HaveCount(1);
    }

    // ============================================================================
    // Split Bill with Payment Grains
    // ============================================================================

    [Fact]
    public async Task SplitBill_TwoPaymentGrains_ForSameOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var order = await CreateOrderWithLinesAsync(orgId, siteId, 80.00m);
        var orderState = await order.GetStateAsync();

        // Create two separate payment grains for the split
        var payment1 = await CreatePaymentGrainAsync(orgId, siteId, orderState.Id, 40.00m);
        var payment2 = await CreatePaymentGrainAsync(orgId, siteId, orderState.Id, 40.00m);

        // Act - Complete both payments
        await payment1.CompleteCashAsync(new CompleteCashPaymentCommand(50.00m, 5.00m));
        await payment2.CompleteCardAsync(new ProcessCardPaymentCommand(
            "ref_123", "auth_456", new CardInfo { MaskedNumber = "****1234", Brand = "Visa", ExpiryMonth = "12", ExpiryYear = "2025" }, "Stripe", 5.00m));

        // Record payments on order
        var payment1State = await payment1.GetStateAsync();
        var payment2State = await payment2.GetStateAsync();

        await order.RecordPaymentAsync(payment1State.Id, 40.00m, 5.00m, "Cash");
        await order.RecordPaymentAsync(payment2State.Id, 40.00m, 5.00m, "CreditCard");

        // Assert
        var finalOrderState = await order.GetStateAsync();
        finalOrderState.PaidAmount.Should().Be(80.00m);
        finalOrderState.TipTotal.Should().Be(10.00m);
        finalOrderState.Status.Should().Be(OrderStatus.Paid);

        payment1State.Status.Should().Be(PaymentStatus.Completed);
        payment2State.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public async Task SplitBill_UnequalSplit_ShouldWorkCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var order = await CreateOrderWithLinesAsync(orgId, siteId, 75.00m);

        // Act - Unequal split: one person pays more
        await order.RecordPaymentAsync(Guid.NewGuid(), 50.00m, 5.00m, "CreditCard");
        await order.RecordPaymentAsync(Guid.NewGuid(), 25.00m, 3.00m, "Cash");

        // Assert
        var state = await order.GetStateAsync();
        state.PaidAmount.Should().Be(75.00m);
        state.TipTotal.Should().Be(8.00m);
        state.Status.Should().Be(OrderStatus.Paid);
    }

    // ============================================================================
    // Close Order with Split Payment
    // ============================================================================

    [Fact]
    public async Task Order_Close_AfterSplitPayment_ShouldSucceed()
    {
        // Arrange
        var order = await CreateSentOrderWithLinesAsync(100.00m);

        // Split payment
        await order.RecordPaymentAsync(Guid.NewGuid(), 50.00m, 0, "Cash");
        await order.RecordPaymentAsync(Guid.NewGuid(), 50.00m, 5.00m, "CreditCard");

        // Act
        await order.CloseAsync(Guid.NewGuid());

        // Assert
        var state = await order.GetStateAsync();
        state.Status.Should().Be(OrderStatus.Closed);
    }

    [Fact]
    public async Task Order_Close_WithPartialPayment_ShouldFail()
    {
        // Arrange
        var order = await CreateSentOrderWithLinesAsync(100.00m);

        // Only partial payment
        await order.RecordPaymentAsync(Guid.NewGuid(), 50.00m, 0, "Cash");

        // Act
        var act = () => order.CloseAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*outstanding balance*");
    }

    // ============================================================================
    // Tips with Split Payments
    // ============================================================================

    [Fact]
    public async Task Order_SplitPayment_TipsShouldAccumulate()
    {
        // Arrange
        var order = await CreateOrderWithLinesAsync(120.00m);

        // Act - Each person adds their own tip
        await order.RecordPaymentAsync(Guid.NewGuid(), 40.00m, 8.00m, "Cash");      // 20% tip
        await order.RecordPaymentAsync(Guid.NewGuid(), 40.00m, 6.00m, "CreditCard"); // 15% tip
        await order.RecordPaymentAsync(Guid.NewGuid(), 40.00m, 10.00m, "DebitCard"); // 25% tip

        // Assert
        var state = await order.GetStateAsync();
        state.PaidAmount.Should().Be(120.00m);
        state.TipTotal.Should().Be(24.00m); // 8 + 6 + 10
        state.Status.Should().Be(OrderStatus.Paid);
    }

    // ============================================================================
    // Edge Cases
    // ============================================================================

    [Fact]
    public async Task Order_ZeroAmountPayment_ShouldBeRecorded()
    {
        // Scenario: Using a 100% discount voucher
        var order = await CreateOrderWithLinesAsync(50.00m);

        // Apply 100% discount
        await order.ApplyDiscountAsync(new ApplyDiscountCommand(
            "100% Off Voucher", DiscountType.Percentage, 100m, Guid.NewGuid()));

        var stateAfterDiscount = await order.GetStateAsync();
        stateAfterDiscount.GrandTotal.Should().Be(0);

        // Record zero payment to complete
        await order.RecordPaymentAsync(Guid.NewGuid(), 0, 0, "Voucher");

        // Assert
        var state = await order.GetStateAsync();
        state.PaidAmount.Should().Be(0);
        state.BalanceDue.Should().BeApproximately(0, 0.02m);
        state.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task Order_ManySmallPayments_ShouldAllBeTracked()
    {
        // Arrange
        var order = await CreateOrderWithLinesAsync(100.00m);

        // Act - 10 small payments of $10 each
        for (int i = 0; i < 10; i++)
        {
            await order.RecordPaymentAsync(Guid.NewGuid(), 10.00m, 1.00m, "Cash");
        }

        // Assert
        var state = await order.GetStateAsync();
        state.PaidAmount.Should().Be(100.00m);
        state.TipTotal.Should().Be(10.00m);
        state.Payments.Should().HaveCount(10);
        state.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task Order_RemoveNonExistentPayment_ShouldNotFail()
    {
        // Arrange
        var order = await CreateOrderWithLinesAsync(50.00m);
        await order.RecordPaymentAsync(Guid.NewGuid(), 50.00m, 0, "Cash");

        // Act - Try to remove a payment that doesn't exist
        await order.RemovePaymentAsync(Guid.NewGuid());

        // Assert - Should not throw, just no-op
        var state = await order.GetStateAsync();
        state.PaidAmount.Should().Be(50.00m);
        state.Payments.Should().HaveCount(1);
    }

    // ============================================================================
    // Payment Method Tracking
    // ============================================================================

    [Fact]
    public async Task Order_Payments_ShouldTrackMethod()
    {
        // Arrange
        var order = await CreateOrderWithLinesAsync(100.00m);

        // Act
        await order.RecordPaymentAsync(Guid.NewGuid(), 50.00m, 0, "Cash");
        await order.RecordPaymentAsync(Guid.NewGuid(), 30.00m, 0, "CreditCard");
        await order.RecordPaymentAsync(Guid.NewGuid(), 20.00m, 0, "GiftCard");

        // Assert
        var state = await order.GetStateAsync();
        state.Payments.Should().Contain(p => p.Method == "Cash");
        state.Payments.Should().Contain(p => p.Method == "CreditCard");
        state.Payments.Should().Contain(p => p.Method == "GiftCard");
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

    private async Task<IOrderGrain> CreateOrderWithLinesAsync(decimal totalAmount)
    {
        return await CreateOrderWithLinesAsync(Guid.NewGuid(), Guid.NewGuid(), totalAmount);
    }

    private async Task<IOrderGrain> CreateOrderWithLinesAsync(Guid orgId, Guid siteId, decimal grandTotal)
    {
        var orderId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(orgId, siteId, orderId));

        await grain.CreateAsync(new CreateOrderCommand(
            orgId, siteId, Guid.NewGuid(), OrderType.DineIn, GuestCount: 2));

        // Calculate pre-tax line item amount to achieve desired GrandTotal
        // GrandTotal = LineTotal * 1.10 (10% tax rate), so LineTotal = GrandTotal / 1.10
        // Use truncation to avoid exceeding target (e.g., 90.90 instead of 90.91)
        var preTaxAmount = Math.Truncate(grandTotal / 1.10m * 100) / 100;
        await grain.AddLineAsync(new AddLineCommand(
            Guid.NewGuid(), "Test Item", 1, preTaxAmount));

        return grain;
    }

    private async Task<IOrderGrain> CreateSentOrderWithLinesAsync(decimal totalAmount)
    {
        var order = await CreateOrderWithLinesAsync(totalAmount);
        await order.SendAsync(Guid.NewGuid());
        return order;
    }

    private async Task<IPaymentGrain> CreatePaymentGrainAsync(Guid orgId, Guid siteId, Guid orderId, decimal amount)
    {
        var paymentId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(
            GrainKeys.Payment(orgId, siteId, paymentId));

        await grain.InitiateAsync(new InitiatePaymentCommand(
            orgId, siteId, orderId, PaymentMethod.Cash, amount, Guid.NewGuid()));

        return grain;
    }
}
