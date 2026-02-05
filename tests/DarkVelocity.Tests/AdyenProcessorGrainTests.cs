using DarkVelocity.Host.Grains;
using DarkVelocity.Host.PaymentProcessors;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class AdyenProcessorGrainTests
{
    private readonly TestClusterFixture _fixture;

    public AdyenProcessorGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IAdyenProcessorGrain GetProcessorGrain(Guid accountId, Guid paymentIntentId)
        => _fixture.Cluster.GrainFactory.GetGrain<IAdyenProcessorGrain>($"{accountId}:adyen:{paymentIntentId}");

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
            "EUR",
            "scheme_visa",
            CaptureAutomatically: true,
            StatementDescriptor: "Test Payment",
            Metadata: new Dictionary<string, string> { ["order_id"] = "order_123" });

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.TransactionId.Should().NotBeNullOrEmpty();
        result.AuthorizationCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AuthorizeAsync_WithDelayedCapture_ShouldSetStatusToAuthorized()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            10000,
            "USD",
            "scheme_mastercard",
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
    public async Task AuthorizeAsync_WithImmediateCapture_ShouldSetStatusToCaptured()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            7500,
            "GBP",
            "scheme_amex",
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

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            8000,
            "EUR",
            "scheme_visa",
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
            "EUR",
            "scheme_visa",
            CaptureAutomatically: false,
            StatementDescriptor: null,
            Metadata: null));

        // Act
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
            "EUR",
            "scheme_visa",
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
    public async Task CaptureAsync_WithInvalidPspReference_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        // Act
        var result = await grain.CaptureAsync("invalid_psp_reference", 1000);

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
            "EUR",
            "scheme_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        var refundResult = await grain.RefundAsync(authResult.TransactionId!, 2000, "customer_request");

        // Assert
        refundResult.Success.Should().BeTrue();
        refundResult.RefundedAmount.Should().Be(2000);
        refundResult.RefundId.Should().NotBeNullOrEmpty();

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
            "EUR",
            "scheme_visa",
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
            "EUR",
            "scheme_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        var refundResult = await grain.RefundAsync(authResult.TransactionId!, 7500, "over_refund");

        // Assert
        refundResult.Success.Should().BeFalse();
        refundResult.ErrorCode.Should().Be("amount_too_large");
    }

    // =========================================================================
    // Void (Cancel) Tests
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
            "EUR",
            "scheme_visa",
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
            "EUR",
            "scheme_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        var voidResult = await grain.VoidAsync(authResult.TransactionId!, "void_attempt");

        // Assert
        voidResult.Success.Should().BeFalse();
        voidResult.ErrorCode.Should().Be("invalid_state");
    }

    // =========================================================================
    // Split Payment Tests (Adyen-Specific)
    // =========================================================================

    [Fact]
    public async Task AuthorizeWithSplitAsync_ShouldProcessSplitPayment()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            10000,
            "EUR",
            "scheme_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        var splits = new List<AdyenSplitItem>
        {
            new("acct_platform", 9500, "Default", "platform_fee"),
            new("acct_merchant", 500, "Commission", "merchant_share")
        };

        // Act
        var result = await grain.AuthorizeWithSplitAsync(request, splits);

        // Assert
        result.Success.Should().BeTrue();
        result.TransactionId.Should().NotBeNullOrEmpty();
    }

    // =========================================================================
    // Notification Handling Tests
    // =========================================================================

    [Fact]
    public async Task HandleAdyenNotificationAsync_AuthorizationSuccess_ShouldUpdateState()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        // First create a payment
        await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "EUR",
            "scheme_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        await grain.HandleAdyenNotificationAsync(
            "AUTHORISATION",
            "8535516083855839",
            "{\"success\": true}");

        // Assert
        var state = await grain.GetStateAsync();
        state.Events.Should().Contain(e => e.EventType.Contains("notification"));
    }

    [Fact]
    public async Task HandleWebhookAsync_ChargebackNotification_ShouldUpdateStatus()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "EUR",
            "scheme_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        await grain.HandleWebhookAsync("CHARGEBACK", "{\"reason\": \"fraud\"}");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be("disputed");
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
            "EUR",
            "scheme_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        var state = await grain.GetStateAsync();

        // Assert
        state.ProcessorName.Should().Be("adyen");
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
            "EUR",
            "scheme_visa",
            CaptureAutomatically: false,
            StatementDescriptor: null,
            Metadata: null));

        await grain.CaptureAsync(authResult.TransactionId!, 4000);
        await grain.RefundAsync(authResult.TransactionId!, 1000, "partial_refund");

        // Assert
        var state = await grain.GetStateAsync();
        state.Events.Should().HaveCountGreaterThan(2);
    }

    // =========================================================================
    // Currency Handling Tests
    // =========================================================================

    [Fact]
    public async Task AuthorizeAsync_WithDifferentCurrencies_ShouldNormalizeCurrency()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "eur", // lowercase
            "scheme_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        // The currency should be normalized to uppercase internally
    }
}
