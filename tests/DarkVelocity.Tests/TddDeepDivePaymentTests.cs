using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TddDeepDivePaymentTests
{
    private readonly TestClusterFixture _fixture;

    public TddDeepDivePaymentTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IOrderGrain> CreateOrderWithLineAsync(Guid orgId, Guid siteId, Guid orderId, decimal amount)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, Guid.NewGuid(), OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, amount));
        return grain;
    }

    private async Task<IPaymentGrain> CreateInitiatedPaymentAsync(
        Guid orgId, Guid siteId, Guid orderId, Guid paymentId, decimal amount, PaymentMethod method = PaymentMethod.Cash)
    {
        await CreateOrderWithLineAsync(orgId, siteId, orderId, amount);
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
        await grain.InitiateAsync(new InitiatePaymentCommand(orgId, siteId, orderId, method, amount, Guid.NewGuid()));
        return grain;
    }

    private async Task<IPaymentGrain> CreateCompletedCashPaymentAsync(
        Guid orgId, Guid siteId, Guid orderId, Guid paymentId, decimal amount)
    {
        var grain = await CreateInitiatedPaymentAsync(orgId, siteId, orderId, paymentId, amount, PaymentMethod.Cash);
        await grain.CompleteCashAsync(new CompleteCashPaymentCommand(amount));
        return grain;
    }

    // =========================================================================
    // Void from Non-Standard States
    // =========================================================================

    // Given: a credit card payment that was initiated, authorization was requested, then declined
    // When: a void is called on the declined payment
    // Then: the void succeeds because only Voided and Refunded states block voiding
    [Fact]
    public async Task VoidPayment_FromDeclinedState_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = await CreateInitiatedPaymentAsync(orgId, siteId, orderId, paymentId, 100m, PaymentMethod.CreditCard);
        await grain.RequestAuthorizationAsync();
        await grain.RecordDeclineAsync("insufficient_funds", "Card declined");

        var stateBeforeVoid = await grain.GetStateAsync();
        stateBeforeVoid.Status.Should().Be(PaymentStatus.Declined);

        // Act
        await grain.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Cleanup after decline"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Voided);
        state.VoidReason.Should().Be("Cleanup after decline");
    }

    // Given: a credit card payment in Authorizing state (authorization requested but not yet responded)
    // When: a void is called on the authorizing payment
    // Then: the void succeeds because only Voided and Refunded states block voiding
    [Fact]
    public async Task VoidPayment_FromAuthorizingState_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = await CreateInitiatedPaymentAsync(orgId, siteId, orderId, paymentId, 75m, PaymentMethod.CreditCard);
        await grain.RequestAuthorizationAsync();

        var stateBeforeVoid = await grain.GetStateAsync();
        stateBeforeVoid.Status.Should().Be(PaymentStatus.Authorizing);

        // Act
        await grain.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Timeout waiting for authorization"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Voided);
        state.VoidReason.Should().Be("Timeout waiting for authorization");
    }

    // Given: a payment that has already been voided
    // When: a void is called again on the same payment
    // Then: an InvalidOperationException is thrown because voided payments cannot be voided again
    [Fact]
    public async Task VoidPayment_FromVoidedState_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = await CreateInitiatedPaymentAsync(orgId, siteId, orderId, paymentId, 50m);
        await grain.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "First void"));

        var stateBeforeSecondVoid = await grain.GetStateAsync();
        stateBeforeSecondVoid.Status.Should().Be(PaymentStatus.Voided);

        // Act
        var act = () => grain.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Second void attempt"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot void payment*");
    }

    // =========================================================================
    // Refund Edge Cases
    // =========================================================================

    // Given: a completed cash payment of $50
    // When: a full refund of $50 is issued
    // Then: the status becomes Refunded and RefundedAmount equals TotalAmount
    [Fact]
    public async Task RefundFullAmount_StatusBecomesRefunded()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        var grain = await CreateCompletedCashPaymentAsync(orgId, siteId, orderId, paymentId, 50m);

        // Act
        var result = await grain.RefundAsync(new RefundPaymentCommand(50m, "Full refund", managerId));

        // Assert
        result.RefundedAmount.Should().Be(50m);
        result.RemainingBalance.Should().Be(0m);

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Refunded);
        state.RefundedAmount.Should().Be(state.TotalAmount);
    }

    // Given: a completed cash payment of $100
    // When: three partial refunds of $33.33, $33.33, and $33.34 are issued
    // Then: the status becomes Refunded because 33.33 + 33.33 + 33.34 = 100.00 (testing decimal precision)
    [Fact]
    public async Task RefundMultiplePartial_SumEqualsTotal_StatusBecomesRefunded()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        var grain = await CreateCompletedCashPaymentAsync(orgId, siteId, orderId, paymentId, 100m);

        // Act - Three partial refunds that sum to exactly 100.00
        var result1 = await grain.RefundAsync(new RefundPaymentCommand(33.33m, "Partial refund 1", managerId));
        result1.RemainingBalance.Should().Be(66.67m);

        var state1 = await grain.GetStateAsync();
        state1.Status.Should().Be(PaymentStatus.PartiallyRefunded);

        var result2 = await grain.RefundAsync(new RefundPaymentCommand(33.33m, "Partial refund 2", managerId));
        result2.RemainingBalance.Should().Be(33.34m);

        var state2 = await grain.GetStateAsync();
        state2.Status.Should().Be(PaymentStatus.PartiallyRefunded);

        var result3 = await grain.RefundAsync(new RefundPaymentCommand(33.34m, "Partial refund 3", managerId));

        // Assert
        result3.RefundedAmount.Should().Be(100m);
        result3.RemainingBalance.Should().Be(0m);

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Refunded);
        state.Refunds.Should().HaveCount(3);
    }

    // Given: a completed cash payment of $50
    // When: a refund of $51 is attempted (exceeds the total amount)
    // Then: an InvalidOperationException is thrown with "Refund amount exceeds available balance"
    [Fact]
    public async Task RefundExceedingBalance_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        var grain = await CreateCompletedCashPaymentAsync(orgId, siteId, orderId, paymentId, 50m);

        // Act
        var act = () => grain.RefundAsync(new RefundPaymentCommand(51m, "Over-refund attempt", managerId));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Refund amount exceeds available balance*");
    }

    // Given: a completed cash payment of $100 with a $60 partial refund already issued
    // When: a second refund of $50 is attempted (60 + 50 = 110 > 100)
    // Then: an InvalidOperationException is thrown because the cumulative refund exceeds the total
    [Fact]
    public async Task RefundAfterPartialRefund_ExceedsRemainingBalance_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        var grain = await CreateCompletedCashPaymentAsync(orgId, siteId, orderId, paymentId, 100m);

        // First partial refund of $60 succeeds
        var result = await grain.RefundAsync(new RefundPaymentCommand(60m, "Partial refund", managerId));
        result.RemainingBalance.Should().Be(40m);

        var stateAfterPartial = await grain.GetStateAsync();
        stateAfterPartial.Status.Should().Be(PaymentStatus.PartiallyRefunded);
        stateAfterPartial.RefundedAmount.Should().Be(60m);

        // Act - Second refund of $50 exceeds remaining balance of $40
        var act = () => grain.RefundAsync(new RefundPaymentCommand(50m, "Excess refund attempt", managerId));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Refund amount exceeds available balance*");
    }

    // =========================================================================
    // Cash Payment Edge Cases
    // =========================================================================

    // Given: a cash payment of $50
    // When: the amount tendered is $40 (less than the total)
    // Then: change given is -$10 (negative) because the code computes
    //       changeGiven = amountTendered - totalAmount without validating tendered >= total
    [Fact]
    public async Task CashPayment_TenderedLessThanTotal_NegativeChange()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = await CreateInitiatedPaymentAsync(orgId, siteId, orderId, paymentId, 50m);

        // Act - Tender $40 for a $50 payment (underpayment)
        var result = await grain.CompleteCashAsync(new CompleteCashPaymentCommand(40m));

        // Assert - The grain does not validate that tendered >= total,
        // so it computes negative change: 40 - 50 = -10
        result.TotalAmount.Should().Be(50m);
        result.ChangeGiven.Should().Be(-10m);

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Completed);
        state.AmountTendered.Should().Be(40m);
        state.ChangeGiven.Should().Be(-10m);
    }
}
