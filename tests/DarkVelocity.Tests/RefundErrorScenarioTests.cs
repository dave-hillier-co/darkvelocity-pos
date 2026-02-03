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
