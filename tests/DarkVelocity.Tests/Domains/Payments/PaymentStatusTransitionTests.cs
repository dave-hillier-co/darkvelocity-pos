using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests.Domains.Payments;

/// <summary>
/// Tests for payment status transitions, void vs refund logic,
/// and state machine edge cases.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PaymentStatusTransitionTests
{
    private readonly TestClusterFixture _fixture;

    public PaymentStatusTransitionTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // ============================================================================
    // Payment Status Transitions - Happy Path
    // ============================================================================

    [Fact]
    public async Task Payment_CashFlow_Initiated_To_Completed()
    {
        // Arrange
        var payment = await CreatePaymentAsync(PaymentMethod.Cash, 100m);

        // Assert initial state
        var initialState = await payment.GetStateAsync();
        initialState.Status.Should().Be(PaymentStatus.Initiated);

        // Act
        await payment.CompleteCashAsync(new CompleteCashPaymentCommand(100m));

        // Assert
        var finalState = await payment.GetStateAsync();
        finalState.Status.Should().Be(PaymentStatus.Completed);
        finalState.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Payment_CardFlow_Initiated_Authorizing_Authorized_Captured()
    {
        // Arrange
        var payment = await CreatePaymentAsync(PaymentMethod.CreditCard, 100m);

        // Assert initial state
        var state1 = await payment.GetStateAsync();
        state1.Status.Should().Be(PaymentStatus.Initiated);

        // Act - Request Authorization
        await payment.RequestAuthorizationAsync();

        var state2 = await payment.GetStateAsync();
        state2.Status.Should().Be(PaymentStatus.Authorizing);

        // Record Authorization
        await payment.RecordAuthorizationAsync("AUTH123", "GW456", new CardInfo
        {
            MaskedNumber = "****4242",
            Brand = "Visa",
            EntryMethod = "chip"
        });

        var state3 = await payment.GetStateAsync();
        state3.Status.Should().Be(PaymentStatus.Authorized);
        state3.AuthorizedAt.Should().NotBeNull();

        // Capture
        await payment.CaptureAsync();

        var state4 = await payment.GetStateAsync();
        state4.Status.Should().Be(PaymentStatus.Captured);
        state4.CapturedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Payment_CardFlow_Initiated_Authorizing_Declined()
    {
        // Arrange
        var payment = await CreatePaymentAsync(PaymentMethod.CreditCard, 100m);

        await payment.RequestAuthorizationAsync();

        // Act
        await payment.RecordDeclineAsync("INSUFFICIENT_FUNDS", "Card declined: insufficient funds");

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Declined);
    }

    // ============================================================================
    // Void vs Refund Logic
    // ============================================================================

    [Fact]
    public async Task Payment_Void_InitiatedPayment_ShouldSucceed()
    {
        // Arrange - Payment just initiated, not yet completed
        var payment = await CreatePaymentAsync(PaymentMethod.Cash, 100m);

        // Act
        await payment.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Customer changed mind"));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Voided);
        state.VoidedAt.Should().NotBeNull();
        state.VoidReason.Should().Be("Customer changed mind");
    }

    [Fact]
    public async Task Payment_Void_AuthorizedPayment_ShouldSucceed()
    {
        // Arrange - Card payment authorized but not captured
        var payment = await CreatePaymentAsync(PaymentMethod.CreditCard, 100m);

        await payment.RequestAuthorizationAsync();
        await payment.RecordAuthorizationAsync("AUTH123", "GW456", new CardInfo
        {
            MaskedNumber = "****4242",
            Brand = "Visa"
        });

        var stateBefore = await payment.GetStateAsync();
        stateBefore.Status.Should().Be(PaymentStatus.Authorized);

        // Act - Void before capture
        await payment.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Customer cancelled order"));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Voided);
    }

    [Fact]
    public async Task Payment_Void_CompletedPayment_ShouldSucceed()
    {
        // Arrange - Payment completed
        var payment = await CreateCompletedPaymentAsync(100m);

        // Act - Business logic allows voiding completed payments
        await payment.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Manager override"));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Voided);
    }

    [Fact]
    public async Task Payment_Void_AlreadyVoidedPayment_ShouldThrow()
    {
        // Arrange
        var payment = await CreatePaymentAsync(PaymentMethod.Cash, 100m);
        await payment.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "First void"));

        // Act
        var act = () => payment.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Second void"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot void*");
    }

    [Fact]
    public async Task Payment_Void_FullyRefundedPayment_ShouldThrow()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100m);
        await payment.RefundAsync(new RefundPaymentCommand(100m, "Full refund", Guid.NewGuid()));

        var stateBefore = await payment.GetStateAsync();
        stateBefore.Status.Should().Be(PaymentStatus.Refunded);

        // Act
        var act = () => payment.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Void after refund"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot void*");
    }

    [Fact]
    public async Task Payment_Refund_CompletedPayment_ShouldSucceed()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100m);

        // Act
        await payment.RefundAsync(new RefundPaymentCommand(100m, "Customer return", Guid.NewGuid()));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public async Task Payment_Refund_VoidedPayment_ShouldThrow()
    {
        // Arrange
        var payment = await CreatePaymentAsync(PaymentMethod.Cash, 100m);
        await payment.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Voided"));

        // Act
        var act = () => payment.RefundAsync(new RefundPaymentCommand(50m, "Refund", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only refund completed*");
    }

    [Fact]
    public async Task Payment_PartialRefund_ThenVoid_ShouldSucceed()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100m);
        await payment.RefundAsync(new RefundPaymentCommand(30m, "Partial return", Guid.NewGuid()));

        var stateAfterRefund = await payment.GetStateAsync();
        stateAfterRefund.Status.Should().Be(PaymentStatus.PartiallyRefunded);

        // Act - Void can still be done on partially refunded payments
        await payment.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Void rest"));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Voided);
    }

    // ============================================================================
    // Invalid Status Transitions
    // ============================================================================

    [Fact]
    public async Task Payment_RequestAuthorization_WhenNotInitiated_ShouldThrow()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100m);

        // Act
        var act = () => payment.RequestAuthorizationAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status*");
    }

    [Fact]
    public async Task Payment_RecordAuthorization_WhenNotAuthorizing_ShouldThrow()
    {
        // Arrange
        var payment = await CreatePaymentAsync(PaymentMethod.CreditCard, 100m);
        // Note: Not calling RequestAuthorizationAsync()

        // Act
        var act = () => payment.RecordAuthorizationAsync("AUTH", "REF", new CardInfo
        {
            MaskedNumber = "****4242",
            Brand = "Visa"
        });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status*");
    }

    [Fact]
    public async Task Payment_Capture_WhenNotAuthorized_ShouldThrow()
    {
        // Arrange
        var payment = await CreatePaymentAsync(PaymentMethod.CreditCard, 100m);

        // Act
        var act = () => payment.CaptureAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status*");
    }

    [Fact]
    public async Task Payment_RecordDecline_WhenNotAuthorizing_ShouldThrow()
    {
        // Arrange
        var payment = await CreatePaymentAsync(PaymentMethod.CreditCard, 100m);
        // Note: Not calling RequestAuthorizationAsync()

        // Act
        var act = () => payment.RecordDeclineAsync("DECLINED", "Card declined");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status*");
    }

    [Fact]
    public async Task Payment_CompleteCash_WhenNotInitiated_ShouldThrow()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100m);

        // Act
        var act = () => payment.CompleteCashAsync(new CompleteCashPaymentCommand(100m));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status*");
    }

    [Fact]
    public async Task Payment_CompleteGiftCard_WhenNotInitiated_ShouldThrow()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100m);

        // Act
        var act = () => payment.CompleteGiftCardAsync(new ProcessGiftCardPaymentCommand(
            Guid.NewGuid(), "GC-12345"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status*");
    }

    [Fact]
    public async Task Payment_AdjustTip_WhenNotCompleted_ShouldThrow()
    {
        // Arrange
        var payment = await CreatePaymentAsync(PaymentMethod.Cash, 100m);

        // Act
        var act = () => payment.AdjustTipAsync(new AdjustTipCommand(10m, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only adjust tip on completed*");
    }

    [Fact]
    public async Task Payment_Refund_WhenInitiated_ShouldThrow()
    {
        // Arrange
        var payment = await CreatePaymentAsync(PaymentMethod.Cash, 100m);

        // Act
        var act = () => payment.RefundAsync(new RefundPaymentCommand(50m, "Refund", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only refund completed*");
    }

    // ============================================================================
    // CompleteCard Status Transition Tests
    // ============================================================================

    [Fact]
    public async Task Payment_CompleteCard_FromInitiated_ShouldSucceed()
    {
        // Arrange
        var payment = await CreatePaymentAsync(PaymentMethod.CreditCard, 100m);

        // Act
        await payment.CompleteCardAsync(new ProcessCardPaymentCommand(
            "ref_123", "auth_456",
            new CardInfo { MaskedNumber = "****4242", Brand = "Visa" },
            "Stripe"));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public async Task Payment_CompleteCard_FromAuthorized_ShouldSucceed()
    {
        // Arrange
        var payment = await CreatePaymentAsync(PaymentMethod.CreditCard, 100m);

        await payment.RequestAuthorizationAsync();
        await payment.RecordAuthorizationAsync("AUTH123", "GW456", new CardInfo
        {
            MaskedNumber = "****4242",
            Brand = "Visa"
        });

        // Act
        await payment.CompleteCardAsync(new ProcessCardPaymentCommand(
            "ref_123", "auth_456",
            new CardInfo { MaskedNumber = "****4242", Brand = "Visa" },
            "Stripe"));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public async Task Payment_CompleteCard_FromAuthorizing_ShouldThrow()
    {
        // Arrange
        var payment = await CreatePaymentAsync(PaymentMethod.CreditCard, 100m);
        await payment.RequestAuthorizationAsync();

        // Act
        var act = () => payment.CompleteCardAsync(new ProcessCardPaymentCommand(
            "ref_123", "auth_456",
            new CardInfo { MaskedNumber = "****4242", Brand = "Visa" },
            "Stripe"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status*");
    }

    // ============================================================================
    // Refund After Status Change Tests
    // ============================================================================

    [Fact]
    public async Task Payment_Refund_PartiallyRefunded_ShouldSucceed()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100m);
        await payment.RefundAsync(new RefundPaymentCommand(30m, "First refund", Guid.NewGuid()));

        // Act
        await payment.RefundAsync(new RefundPaymentCommand(40m, "Second refund", Guid.NewGuid()));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.PartiallyRefunded);
        state.RefundedAmount.Should().Be(70m);
    }

    [Fact]
    public async Task Payment_Refund_CompletingRefund_ShouldChangeToRefundedStatus()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100m);
        await payment.RefundAsync(new RefundPaymentCommand(60m, "Partial", Guid.NewGuid()));

        // Act
        await payment.RefundAsync(new RefundPaymentCommand(40m, "Remainder", Guid.NewGuid()));

        // Assert
        var state = await payment.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Refunded);
        state.RefundedAmount.Should().Be(100m);
    }

    // ============================================================================
    // Exists and Status Query Tests
    // ============================================================================

    [Fact]
    public async Task Payment_ExistsAsync_WhenNotCreated_ShouldReturnFalse()
    {
        // Arrange
        var payment = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(
            GrainKeys.Payment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Act
        var exists = await payment.ExistsAsync();

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Payment_ExistsAsync_WhenCreated_ShouldReturnTrue()
    {
        // Arrange
        var payment = await CreatePaymentAsync(PaymentMethod.Cash, 100m);

        // Act
        var exists = await payment.ExistsAsync();

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Payment_GetStatusAsync_ShouldReturnCurrentStatus()
    {
        // Arrange
        var payment = await CreatePaymentAsync(PaymentMethod.Cash, 100m);

        // Act & Assert
        var status1 = await payment.GetStatusAsync();
        status1.Should().Be(PaymentStatus.Initiated);

        await payment.CompleteCashAsync(new CompleteCashPaymentCommand(100m));
        var status2 = await payment.GetStatusAsync();
        status2.Should().Be(PaymentStatus.Completed);

        await payment.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Test"));
        var status3 = await payment.GetStatusAsync();
        status3.Should().Be(PaymentStatus.Voided);
    }

    // ============================================================================
    // Idempotency Tests
    // ============================================================================

    [Fact]
    public async Task Payment_Initiate_Twice_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var payment = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(
            GrainKeys.Payment(orgId, siteId, paymentId));

        await payment.InitiateAsync(new InitiatePaymentCommand(
            orgId, siteId, orderId, PaymentMethod.Cash, 100m, Guid.NewGuid()));

        // Act
        var act = () => payment.InitiateAsync(new InitiatePaymentCommand(
            orgId, siteId, orderId, PaymentMethod.Cash, 100m, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    // ============================================================================
    // Batch Assignment Tests
    // ============================================================================

    [Fact]
    public async Task Payment_AssignToBatch_ShouldSetBatchId()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100m);
        var batchId = Guid.NewGuid();

        // Act
        await payment.AssignToBatchAsync(batchId);

        // Assert
        var state = await payment.GetStateAsync();
        state.BatchId.Should().Be(batchId);
    }

    [Fact]
    public async Task Payment_AssignToBatch_MultipleTimes_ShouldUpdateBatchId()
    {
        // Arrange
        var payment = await CreateCompletedPaymentAsync(100m);
        var batch1 = Guid.NewGuid();
        var batch2 = Guid.NewGuid();

        // Act
        await payment.AssignToBatchAsync(batch1);
        await payment.AssignToBatchAsync(batch2);

        // Assert
        var state = await payment.GetStateAsync();
        state.BatchId.Should().Be(batch2);
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

    private async Task<IPaymentGrain> CreatePaymentAsync(PaymentMethod method, decimal amount)
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(
            GrainKeys.Payment(orgId, siteId, paymentId));

        await grain.InitiateAsync(new InitiatePaymentCommand(
            orgId, siteId, orderId, method, amount, Guid.NewGuid()));

        return grain;
    }

    private async Task<IPaymentGrain> CreateCompletedPaymentAsync(decimal amount)
    {
        var grain = await CreatePaymentAsync(PaymentMethod.Cash, amount);
        await grain.CompleteCashAsync(new CompleteCashPaymentCommand(amount + 10m));
        return grain;
    }
}
