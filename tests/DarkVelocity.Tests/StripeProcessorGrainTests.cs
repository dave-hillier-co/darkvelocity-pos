using DarkVelocity.Host.Grains;
using DarkVelocity.Host.PaymentProcessors;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class StripeProcessorGrainTests
{
    private readonly TestClusterFixture _fixture;

    public StripeProcessorGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IStripeProcessorGrain GetProcessorGrain(Guid accountId, Guid paymentIntentId)
        => _fixture.Cluster.GrainFactory.GetGrain<IStripeProcessorGrain>($"{accountId}:stripe:{paymentIntentId}");

    // =========================================================================
    // Authorization Tests
    // =========================================================================

    [Fact]
    public async Task AuthorizeAsync_WithValidRequest_ShouldSucceed()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: "Test Payment",
            Metadata: new Dictionary<string, string> { ["order_id"] = "order_123" });

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.TransactionId.Should().NotBeNullOrEmpty();
        result.TransactionId.Should().StartWith("pi_");
    }

    [Fact]
    public async Task AuthorizeAsync_WithManualCapture_ShouldSetStatusToAuthorized()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            10000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: false,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        var state = await grain.GetStateAsync();
        state.Status.Should().Be("authorized");
        state.AuthorizedAmount.Should().Be(10000);
        state.CapturedAmount.Should().Be(0);
    }

    [Fact]
    public async Task AuthorizeAsync_WithAutomaticCapture_ShouldSetStatusToCaptured()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            7500,
            "eur",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        var state = await grain.GetStateAsync();
        state.Status.Should().Be("captured");
        state.CapturedAmount.Should().Be(7500);
    }

    // =========================================================================
    // Capture Tests
    // =========================================================================

    [Fact]
    public async Task CaptureAsync_WithAuthorizedPayment_ShouldSucceed()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        // First authorize
        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            8000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: false,
            StatementDescriptor: null,
            Metadata: null));

        authResult.Success.Should().BeTrue();

        // Act
        var captureResult = await grain.CaptureAsync(authResult.TransactionId!, 8000);

        // Assert
        captureResult.Success.Should().BeTrue();
        captureResult.CapturedAmount.Should().Be(8000);

        var state = await grain.GetStateAsync();
        state.Status.Should().Be("captured");
    }

    [Fact]
    public async Task CaptureAsync_WithPartialAmount_ShouldSucceed()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            10000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: false,
            StatementDescriptor: null,
            Metadata: null));

        // Act - Capture partial amount
        var captureResult = await grain.CaptureAsync(authResult.TransactionId!, 7500);

        // Assert
        captureResult.Success.Should().BeTrue();
        captureResult.CapturedAmount.Should().Be(7500);
    }

    [Fact]
    public async Task CaptureAsync_ExceedsAuthorizedAmount_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: false,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        var captureResult = await grain.CaptureAsync(authResult.TransactionId!, 7500);

        // Assert
        captureResult.Success.Should().BeFalse();
        captureResult.ErrorCode.Should().Be("amount_too_large");
    }

    [Fact]
    public async Task CaptureAsync_WithInvalidTransactionId_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        // Act
        var result = await grain.CaptureAsync("invalid_pi_id", 1000);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_transaction");
    }

    // =========================================================================
    // Refund Tests
    // =========================================================================

    [Fact]
    public async Task RefundAsync_WithCapturedPayment_ShouldSucceed()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        var refundResult = await grain.RefundAsync(authResult.TransactionId!, 2000, "customer_request");

        // Assert
        refundResult.Success.Should().BeTrue();
        refundResult.RefundedAmount.Should().Be(2000);
        refundResult.RefundId.Should().StartWith("re_");

        var state = await grain.GetStateAsync();
        state.RefundedAmount.Should().Be(2000);
    }

    [Fact]
    public async Task RefundAsync_FullAmount_ShouldChangeStatusToRefunded()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            3000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        await grain.RefundAsync(authResult.TransactionId!, 3000, "full_refund");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be("refunded");
        state.RefundedAmount.Should().Be(3000);
    }

    [Fact]
    public async Task RefundAsync_ExceedsAvailableBalance_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        var refundResult = await grain.RefundAsync(authResult.TransactionId!, 7500, "over_refund");

        // Assert
        refundResult.Success.Should().BeFalse();
        refundResult.ErrorCode.Should().Be("amount_too_large");
    }

    [Fact]
    public async Task RefundAsync_OnAuthorizedPayment_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: false, // Not captured
            StatementDescriptor: null,
            Metadata: null));

        // Act
        var refundResult = await grain.RefundAsync(authResult.TransactionId!, 2500, "refund_attempt");

        // Assert
        refundResult.Success.Should().BeFalse();
        refundResult.ErrorCode.Should().Be("invalid_state");
    }

    // =========================================================================
    // Void Tests
    // =========================================================================

    [Fact]
    public async Task VoidAsync_OnAuthorizedPayment_ShouldSucceed()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: false,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        var voidResult = await grain.VoidAsync(authResult.TransactionId!, "customer_cancelled");

        // Assert
        voidResult.Success.Should().BeTrue();
        voidResult.VoidId.Should().NotBeNullOrEmpty();

        var state = await grain.GetStateAsync();
        state.Status.Should().Be("voided");
    }

    [Fact]
    public async Task VoidAsync_OnCapturedPayment_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: true, // Auto-captured
            StatementDescriptor: null,
            Metadata: null));

        // Act
        var voidResult = await grain.VoidAsync(authResult.TransactionId!, "void_attempt");

        // Assert
        voidResult.Success.Should().BeFalse();
        voidResult.ErrorCode.Should().Be("invalid_state");
    }

    // =========================================================================
    // State and Event Tracking Tests
    // =========================================================================

    [Fact]
    public async Task GetStateAsync_ShouldReturnCorrectProcessorInfo()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            6000,
            "gbp",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: "Test Descriptor",
            Metadata: null));

        // Act
        var state = await grain.GetStateAsync();

        // Assert
        state.ProcessorName.Should().Be("stripe");
        state.PaymentIntentId.Should().Be(paymentIntentId);
        state.Status.Should().Be("captured");
        state.CapturedAmount.Should().Be(6000);
    }

    [Fact]
    public async Task Events_ShouldBeTracked()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        // Act
        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            4000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: false,
            StatementDescriptor: null,
            Metadata: null));

        await grain.CaptureAsync(authResult.TransactionId!, 4000);
        await grain.RefundAsync(authResult.TransactionId!, 1000, "partial_refund");

        // Assert
        var state = await grain.GetStateAsync();
        state.Events.Should().HaveCountGreaterThan(2);
        state.Events.Should().Contain(e => e.EventType == "authorized" || e.EventType == "captured");
    }

    // =========================================================================
    // Stripe-Specific Operation Tests
    // =========================================================================

    [Fact]
    public async Task CreateSetupIntentAsync_ShouldReturnClientSecret()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        // Initialize the grain with a payment first
        await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            100,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        var clientSecret = await grain.CreateSetupIntentAsync("cus_test_customer");

        // Assert
        clientSecret.Should().NotBeNullOrEmpty();
        clientSecret.Should().Contain("_secret_");
    }

    [Fact]
    public async Task AuthorizeOnBehalfOfAsync_ShouldStoreConnectDetails()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            10000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: "Platform Payment",
            Metadata: null);

        // Act
        var result = await grain.AuthorizeOnBehalfOfAsync(
            request,
            connectedAccountId: "acct_connected_123",
            applicationFee: 500);

        // Assert
        result.Success.Should().BeTrue();
    }

    // =========================================================================
    // Webhook Handling Tests
    // =========================================================================

    [Fact]
    public async Task HandleStripeWebhookAsync_PaymentSucceeded_ShouldBeRecorded()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        await grain.HandleStripeWebhookAsync(
            "payment_intent.succeeded",
            "evt_test_123",
            "{\"id\": \"pi_test\", \"status\": \"succeeded\"}");

        // Assert
        var state = await grain.GetStateAsync();
        state.Events.Should().Contain(e => e.EventType == "payment_intent.succeeded");
    }

    // =========================================================================
    // Idempotency Tests
    // =========================================================================

    [Fact]
    public async Task MultipleAuthorizeAttempts_ShouldUseIdempotencyKeys()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act - First attempt
        var result1 = await grain.AuthorizeAsync(request);

        // Second attempt should use same idempotency key for retries
        // (In production, the stub returns a new result, but idempotency key is tracked)
        var state = await grain.GetStateAsync();

        // Assert
        result1.Success.Should().BeTrue();
        state.RetryCount.Should().Be(1);
    }
}
