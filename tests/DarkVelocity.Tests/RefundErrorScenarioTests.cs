using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for refund error scenarios - validation errors, processor failures,
/// edge cases, and refund reversal handling.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class RefundErrorScenarioTests
{
    private readonly TestClusterFixture _fixture;

    public RefundErrorScenarioTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // ============================================================================
    // Refund Amount Validation
    // ============================================================================

    // Given: a completed $100 cash payment
    // When: a $150 refund is attempted, exceeding the original payment amount
    // Then: the refund is rejected because it exceeds the available balance
    [Fact]
    public async Task Payment_Refund_ExceedsTotalAmount_ShouldThrow()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100.00m);

        // Act
        var act = () => payment.RefundAsync(new RefundPaymentCommand(150.00m, "Test refund", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds available balance*");
    }

    // Given: a completed $100 payment that has already been partially refunded $60
    // When: a $50 refund is attempted against the remaining $40 balance
    // Then: the refund is rejected because it exceeds the remaining refundable amount
    [Fact]
    public async Task Payment_Refund_ExceedsRemainingAfterPartialRefund_ShouldThrow()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100.00m);

        // First refund $60
        await payment.RefundAsync(new RefundPaymentCommand(60.00m, "Partial refund", Guid.NewGuid()));

        // Act - try to refund another $50 (only $40 remaining)
        var act = () => payment.RefundAsync(new RefundPaymentCommand(50.00m, "Another refund", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds available balance*");
    }

    // Given: a completed $100 payment
    // When: a zero-amount refund is attempted
    // Then: the refund is rejected as invalid
    [Fact]
    public async Task Payment_Refund_ZeroAmount_ShouldThrow()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100.00m);

        // Act
        var act = () => payment.RefundAsync(new RefundPaymentCommand(0m, "Zero refund", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // Given: a completed $100 payment
    // When: a negative-amount refund is attempted
    // Then: the refund is rejected as invalid
    [Fact]
    public async Task Payment_Refund_NegativeAmount_ShouldThrow()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100.00m);

        // Act
        var act = () => payment.RefundAsync(new RefundPaymentCommand(-10.00m, "Negative refund", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ============================================================================
    // Refund Status Validation
    // ============================================================================

    // Given: a payment that has been initiated but not yet completed
    // When: a refund is attempted against the pending payment
    // Then: the refund is rejected because only completed payments can be refunded
    [Fact]
    public async Task Payment_Refund_WhenPending_ShouldThrow()
    {
        // Arrange
        var payment = await CreateInitiatedPaymentAsync(100.00m);

        // Act
        var act = () => payment.RefundAsync(new RefundPaymentCommand(50.00m, "Test refund", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only refund completed payments*");
    }

    // Given: a payment that has been voided
    // When: a refund is attempted against the voided payment
    // Then: the refund is rejected because only completed payments can be refunded
    [Fact]
    public async Task Payment_Refund_WhenVoided_ShouldThrow()
    {
        // Arrange
        var payment = await CreateInitiatedPaymentAsync(100.00m);
        await payment.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Test void"));

        // Act
        var act = () => payment.RefundAsync(new RefundPaymentCommand(50.00m, "Test refund", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only refund completed payments*");
    }

    // Given: a completed $100 payment that has already been fully refunded
    // When: an additional $10 refund is attempted
    // Then: the refund is rejected because the payment is already in refunded status
    [Fact]
    public async Task Payment_Refund_WhenAlreadyFullyRefunded_ShouldThrow()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100.00m);
        await payment.RefundAsync(new RefundPaymentCommand(100.00m, "Full refund", Guid.NewGuid()));

        // Verify status changed to Refunded
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Refunded);

        // Act - try to refund again
        var act = () => payment.RefundAsync(new RefundPaymentCommand(10.00m, "Another refund", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only refund completed payments*");
    }

    // ============================================================================
    // Partial Refund Scenarios
    // ============================================================================

    // Given: a completed $100 payment
    // When: a $30 partial refund is issued
    // Then: the payment status changes to partially refunded with $30 tracked
    [Fact]
    public async Task Payment_PartialRefund_ShouldSetPartiallyRefundedStatus()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100.00m);

        // Act
        await payment.RefundAsync(new RefundPaymentCommand(30.00m, "Partial refund", Guid.NewGuid()));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.PartiallyRefunded);
        state.RefundedAmount.Should().Be(30.00m);
    }

    // Given: a completed $100 payment
    // When: three partial refunds of $20, $30, and $25 are issued sequentially
    // Then: the refunded amount accumulates to $75 with partially refunded status
    [Fact]
    public async Task Payment_MultiplePartialRefunds_ShouldAccumulate()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100.00m);

        // Act - Multiple small refunds
        await payment.RefundAsync(new RefundPaymentCommand(20.00m, "First refund", Guid.NewGuid()));
        await payment.RefundAsync(new RefundPaymentCommand(30.00m, "Second refund", Guid.NewGuid()));
        await payment.RefundAsync(new RefundPaymentCommand(25.00m, "Third refund", Guid.NewGuid()));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.PartiallyRefunded);
        state.RefundedAmount.Should().Be(75.00m);
    }

    // Given: a completed $100 payment
    // When: a $60 partial refund is followed by a $40 refund for the remainder
    // Then: the payment status changes to fully refunded with $100 total refunded
    [Fact]
    public async Task Payment_PartialRefunds_ThenFullRefund_ShouldChangeToRefundedStatus()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100.00m);

        // Act - Partial then remaining
        await payment.RefundAsync(new RefundPaymentCommand(60.00m, "Partial refund", Guid.NewGuid()));
        await payment.RefundAsync(new RefundPaymentCommand(40.00m, "Remaining refund", Guid.NewGuid()));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Refunded);
        state.RefundedAmount.Should().Be(100.00m);
    }

    // Given: a completed $100 payment with a $70 partial refund already applied
    // When: a refund for exactly the remaining $30 is issued
    // Then: the payment becomes fully refunded with $100 total refunded
    [Fact]
    public async Task Payment_PartialRefund_ExactRemainingAmount_ShouldSucceed()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100.00m);
        await payment.RefundAsync(new RefundPaymentCommand(70.00m, "First refund", Guid.NewGuid()));

        // Act - Refund exact remaining amount
        await payment.RefundAsync(new RefundPaymentCommand(30.00m, "Exact remaining", Guid.NewGuid()));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Refunded);
        state.RefundedAmount.Should().Be(100.00m);
    }

    // ============================================================================
    // Void After Refund Scenarios
    // ============================================================================

    // Given: a completed $100 payment that has been partially refunded $30
    // When: the payment is voided
    // Then: the payment status changes to voided
    [Fact]
    public async Task Payment_Void_WhenPartiallyRefunded_ShouldSucceed()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100.00m);
        await payment.RefundAsync(new RefundPaymentCommand(30.00m, "Partial refund", Guid.NewGuid()));

        // Act
        await payment.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Void after partial refund"));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Voided);
    }

    // Given: a completed $100 payment that has been fully refunded
    // When: a void is attempted on the fully refunded payment
    // Then: the void is rejected because the payment is already refunded
    [Fact]
    public async Task Payment_Void_WhenFullyRefunded_ShouldThrow()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100.00m);
        await payment.RefundAsync(new RefundPaymentCommand(100.00m, "Full refund", Guid.NewGuid()));

        // Act
        var act = () => payment.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Test void"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot void payment with status*");
    }

    // ============================================================================
    // Tip Adjustment After Refund
    // ============================================================================

    // Given: a completed $100 payment that has been partially refunded $30
    // When: a tip adjustment is attempted on the partially refunded payment
    // Then: the tip adjustment is rejected because tips can only be adjusted on completed payments
    [Fact]
    public async Task Payment_AdjustTip_WhenPartiallyRefunded_ShouldThrow()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100.00m);
        await payment.RefundAsync(new RefundPaymentCommand(30.00m, "Partial refund", Guid.NewGuid()));

        // Act
        var act = () => payment.AdjustTipAsync(new AdjustTipCommand(10.00m, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only adjust tip on completed payments*");
    }

    // ============================================================================
    // Refund Tracking
    // ============================================================================

    // Given: a completed $100 payment
    // When: a $40 refund is issued with a reason and issuer recorded
    // Then: the refund details including amount, reason, issuer, and timestamp are tracked
    [Fact]
    public async Task Payment_Refund_ShouldTrackRefundDetails()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100.00m);
        var issuedBy = Guid.NewGuid();
        var reason = "Customer returned item";

        // Act
        await payment.RefundAsync(new RefundPaymentCommand(40.00m, reason, issuedBy));

        // Assert
        var state = await payment.GetStateAsync();
        state.RefundedAmount.Should().Be(40.00m);
        state.Refunds.Should().HaveCount(1);
        state.Refunds[0].Amount.Should().Be(40.00m);
        state.Refunds[0].Reason.Should().Be(reason);
        state.Refunds[0].IssuedBy.Should().Be(issuedBy);
        state.Refunds[0].IssuedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: a completed $100 payment
    // When: three separate refunds are issued for returned items ($25, $15, $10)
    // Then: all three refund records are tracked with a cumulative $50 refunded total
    [Fact]
    public async Task Payment_MultipleRefunds_ShouldTrackAllDetails()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100.00m);

        // Act
        await payment.RefundAsync(new RefundPaymentCommand(25.00m, "First item returned", Guid.NewGuid()));
        await payment.RefundAsync(new RefundPaymentCommand(15.00m, "Second item returned", Guid.NewGuid()));
        await payment.RefundAsync(new RefundPaymentCommand(10.00m, "Third item returned", Guid.NewGuid()));

        // Assert
        var state = await payment.GetStateAsync();
        state.RefundedAmount.Should().Be(50.00m);
        state.Refunds.Should().HaveCount(3);
        state.Refunds.Sum(r => r.Amount).Should().Be(50.00m);
    }

    // ============================================================================
    // Edge Cases
    // ============================================================================

    // Given: a completed $100 payment
    // When: a 1-cent refund is issued as a rounding correction
    // Then: the payment is partially refunded with $0.01 tracked
    [Fact]
    public async Task Payment_Refund_SmallAmount_ShouldSucceed()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100.00m);

        // Act - refund 1 cent
        await payment.RefundAsync(new RefundPaymentCommand(0.01m, "Rounding correction", Guid.NewGuid()));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.PartiallyRefunded);
        state.RefundedAmount.Should().Be(0.01m);
    }

    // Given: a completed $100 payment
    // When: ten separate $5 refunds are issued against the payment
    // Then: all ten refund records are tracked with a $50 cumulative total
    [Fact]
    public async Task Payment_Refund_LargeNumberOfRefunds_ShouldAllBeTracked()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100.00m);

        // Act - 10 small refunds
        for (int i = 0; i < 10; i++)
        {
            await payment.RefundAsync(new RefundPaymentCommand(5.00m, $"Refund {i + 1}", Guid.NewGuid()));
        }

        // Assert
        var state = await payment.GetStateAsync();
        state.RefundedAmount.Should().Be(50.00m);
        state.Refunds.Should().HaveCount(10);
        state.Status.Should().Be(PaymentStatus.PartiallyRefunded);
    }

    // ============================================================================
    // Cash vs Card Refund Scenarios
    // ============================================================================

    // Given: a completed $50 cash payment
    // When: a $25 refund is issued
    // Then: the cash payment is partially refunded with $25 tracked
    [Fact]
    public async Task CashPayment_Refund_ShouldWork()
    {
        // Arrange
        var payment = await CreateCompletedCashPaymentAsync(50.00m);

        // Act
        await payment.RefundAsync(new RefundPaymentCommand(25.00m, "Cash refund", Guid.NewGuid()));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.PartiallyRefunded);
        state.RefundedAmount.Should().Be(25.00m);
    }

    // Given: a completed $75 credit card payment
    // When: a $30 refund is issued
    // Then: the card payment is partially refunded with $30 tracked
    [Fact]
    public async Task CardPayment_Refund_ShouldWork()
    {
        // Arrange
        var payment = await CreateCompletedCardPaymentAsync(75.00m);

        // Act
        await payment.RefundAsync(new RefundPaymentCommand(30.00m, "Card refund", Guid.NewGuid()));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.PartiallyRefunded);
        state.RefundedAmount.Should().Be(30.00m);
    }

    // ============================================================================
    // Gift Card Refund Scenarios
    // ============================================================================

    // Given: a completed $100 gift card payment
    // When: a $40 refund is issued against the gift card payment
    // Then: the payment is partially refunded with $40 tracked
    [Fact]
    public async Task GiftCardPayment_Refund_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var payment = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(
            GrainKeys.Payment(orgId, siteId, paymentId));

        await payment.InitiateAsync(new InitiatePaymentCommand(
            orgId, siteId, orderId, PaymentMethod.GiftCard, 100.00m, Guid.NewGuid()));

        var giftCardId = Guid.NewGuid();
        await payment.CompleteGiftCardAsync(new ProcessGiftCardPaymentCommand(
            giftCardId, "GC-12345"));

        // Act
        await payment.RefundAsync(new RefundPaymentCommand(40.00m, "Gift card refund", Guid.NewGuid()));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.PartiallyRefunded);
        state.RefundedAmount.Should().Be(40.00m);
    }

    // ============================================================================
    // Refund with Tips
    // ============================================================================

    // Given: a completed $100 cash payment with a $15 tip
    // When: a $30 refund is issued for a returned item
    // Then: the tip remains unchanged at $15 while the refund is tracked separately
    [Fact]
    public async Task Payment_Refund_ShouldNotAffectTip()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var payment = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(
            GrainKeys.Payment(orgId, siteId, paymentId));

        await payment.InitiateAsync(new InitiatePaymentCommand(
            orgId, siteId, orderId, PaymentMethod.Cash, 100.00m, Guid.NewGuid()));

        await payment.CompleteCashAsync(new CompleteCashPaymentCommand(120.00m, 15.00m)); // $15 tip

        var stateBeforeRefund = await payment.GetStateAsync();
        stateBeforeRefund.TipAmount.Should().Be(15.00m);

        // Act - refund base amount only
        await payment.RefundAsync(new RefundPaymentCommand(30.00m, "Item refund", Guid.NewGuid()));

        // Assert - tip should remain unchanged
        var state = await payment.GetStateAsync();
        state.TipAmount.Should().Be(15.00m);
        state.RefundedAmount.Should().Be(30.00m);
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

    private async Task<IPaymentGrain> CreateInitiatedPaymentAsync(decimal amount)
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(
            GrainKeys.Payment(orgId, siteId, paymentId));

        await grain.InitiateAsync(new InitiatePaymentCommand(
            orgId, siteId, orderId, PaymentMethod.Cash, amount, Guid.NewGuid()));

        return grain;
    }

    private async Task<IPaymentGrain> CreateCompletedPaymentAsync(decimal amount)
    {
        return await CreateCompletedCashPaymentAsync(amount);
    }

    private async Task<IPaymentGrain> CreateCompletedCashPaymentAsync(decimal amount)
    {
        var grain = await CreateInitiatedPaymentAsync(amount);
        await grain.CompleteCashAsync(new CompleteCashPaymentCommand(amount + 10m, 0)); // Tendered more
        return grain;
    }

    private async Task<IPaymentGrain> CreateCompletedCardPaymentAsync(decimal amount)
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(
            GrainKeys.Payment(orgId, siteId, paymentId));

        await grain.InitiateAsync(new InitiatePaymentCommand(
            orgId, siteId, orderId, PaymentMethod.CreditCard, amount, Guid.NewGuid()));

        await grain.CompleteCardAsync(new ProcessCardPaymentCommand(
            "ref_12345",
            "auth_67890",
            new CardInfo { MaskedNumber = "****1234", Brand = "Visa", EntryMethod = "chip" },
            "Stripe",
            0));

        return grain;
    }
}
