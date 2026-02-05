using DarkVelocity.Host.Grains;
using DarkVelocity.Host.PaymentProcessors;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Integration tests for Payment Processors covering advanced scenarios:
/// - 3D Secure authentication flows
/// - Webhook signature validation
/// - Processor failover scenarios
/// - Idempotency key handling
/// - Timeout handling
/// - Circuit breaker integration with grains
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PaymentProcessorIntegrationTests : IDisposable
{
    private readonly TestClusterFixture _fixture;
    private readonly List<string> _circuitKeysToClean = [];

    public PaymentProcessorIntegrationTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    public void Dispose()
    {
        // Clean up any circuit breaker state
        foreach (var key in _circuitKeysToClean)
        {
            PaymentProcessorRetryHelper.ResetCircuit(key);
        }
    }

    private IMockProcessorGrain GetMockProcessorGrain(Guid accountId, Guid paymentIntentId)
        => _fixture.Cluster.GrainFactory.GetGrain<IMockProcessorGrain>($"{accountId}:mock:{paymentIntentId}");

    private IStripeProcessorGrain GetStripeProcessorGrain(Guid accountId, Guid paymentIntentId)
        => _fixture.Cluster.GrainFactory.GetGrain<IStripeProcessorGrain>($"{accountId}:stripe:{paymentIntentId}");

    private IAdyenProcessorGrain GetAdyenProcessorGrain(Guid accountId, Guid paymentIntentId)
        => _fixture.Cluster.GrainFactory.GetGrain<IAdyenProcessorGrain>($"{accountId}:adyen:{paymentIntentId}");

    // =========================================================================
    // 3D Secure Authentication Flow Tests
    // =========================================================================

    [Fact]
    public async Task ThreeDSecure_MockProcessor_RequiresAction_ThenCompletes()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetMockProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_3155", // 3DS required card
            CaptureAutomatically: false,
            StatementDescriptor: null,
            Metadata: null);

        // Act - Initial authorization triggers 3DS
        var authResult = await grain.AuthorizeAsync(request);

        // Assert - Should require 3DS action
        authResult.Success.Should().BeFalse("3DS should be required");
        authResult.RequiredAction.Should().NotBeNull();
        authResult.RequiredAction!.Type.Should().Be("redirect_to_url");
        authResult.RequiredAction.RedirectUrl.Should().Contain("3ds");

        var stateAfter3dsRequired = await grain.GetStateAsync();
        stateAfter3dsRequired.Status.Should().Be("requires_action");

        // Act - Simulate 3DS completion via webhook
        await grain.HandleWebhookAsync("next_action_completed", "{\"authenticated\":true}");

        // Assert - Payment should now be authorized
        var stateAfterCompletion = await grain.GetStateAsync();
        stateAfterCompletion.Status.Should().Be("authorized");
        stateAfterCompletion.TransactionId.Should().NotBeNullOrEmpty();
        stateAfterCompletion.AuthorizationCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ThreeDSecure_StripeProcessor_WebhookCompletesPayment()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetStripeProcessorGrain(accountId, paymentIntentId);

        // First authorize (stub simulates success, but we test webhook handling)
        var request = new ProcessorAuthRequest(
            paymentIntentId,
            10000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        await grain.AuthorizeAsync(request);

        // Act - Simulate payment_intent.succeeded webhook
        await grain.HandleStripeWebhookAsync(
            "payment_intent.succeeded",
            "evt_test_3ds_complete",
            "{\"id\":\"pi_test\",\"status\":\"succeeded\"}");

        // Assert
        var state = await grain.GetStateAsync();
        state.Events.Should().Contain(e => e.EventType == "payment_intent.succeeded");
    }

    [Fact]
    public async Task ThreeDSecure_AdyenProcessor_NotificationUpdatesState()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetAdyenProcessorGrain(accountId, paymentIntentId);

        // First authorize
        await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "EUR",
            "scheme_visa",
            CaptureAutomatically: false,
            StatementDescriptor: null,
            Metadata: null));

        // Act - Simulate Adyen notification
        await grain.HandleAdyenNotificationAsync(
            "AUTHORISATION",
            "8535516083855839",
            "{\"success\":true,\"amount\":{\"value\":5000,\"currency\":\"EUR\"}}");

        // Assert
        var state = await grain.GetStateAsync();
        state.Events.Should().Contain(e => e.EventType.Contains("notification"));
    }

    // =========================================================================
    // Webhook Signature Validation Tests (Stub Behavior)
    // =========================================================================

    [Fact]
    public async Task Webhook_StripeProcessor_RecordsEventDetails()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetStripeProcessorGrain(accountId, paymentIntentId);

        // Initialize grain with payment
        await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            2500,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act - Process webhook
        var eventId = "evt_unique_webhook_123";
        await grain.HandleStripeWebhookAsync(
            "charge.succeeded",
            eventId,
            "{\"object\":\"charge\",\"amount\":2500}");

        // Assert
        var state = await grain.GetStateAsync();
        state.Events.Should().Contain(e => e.EventType == "charge.succeeded");
        state.Events.Should().Contain(e => e.ExternalEventId != null && e.ExternalEventId.Contains(eventId));
    }

    [Fact]
    public async Task Webhook_AdyenProcessor_ChargebackUpdatesStatus()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetAdyenProcessorGrain(accountId, paymentIntentId);

        await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            8000,
            "EUR",
            "scheme_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        await grain.HandleWebhookAsync("CHARGEBACK", "{\"reason\":\"fraud_suspected\"}");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be("disputed");
    }

    [Fact]
    public async Task Webhook_MockProcessor_DisputeCreation()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetMockProcessorGrain(accountId, paymentIntentId);

        await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            15000,
            "usd",
            "pm_card_4242",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        await grain.SimulateDisputeAsync(15000, "fraudulent");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be("disputed");
        state.Events.Should().Contain(e => e.EventType == "dispute_created");
    }

    // =========================================================================
    // Idempotency Key Handling Tests
    // =========================================================================

    [Fact]
    public async Task Idempotency_StripeProcessor_TracksRetryCount()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetStripeProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act - First authorization
        var result1 = await grain.AuthorizeAsync(request);

        // Assert
        var state = await grain.GetStateAsync();
        state.RetryCount.Should().Be(1);
        result1.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Idempotency_AdyenProcessor_TracksRetryCount()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetAdyenProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            7500,
            "EUR",
            "scheme_mastercard",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        await grain.AuthorizeAsync(request);

        // Assert
        var state = await grain.GetStateAsync();
        state.RetryCount.Should().Be(1);
    }

    [Fact]
    public async Task Idempotency_MultipleCaptures_TracksCorrectAmount()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetStripeProcessorGrain(accountId, paymentIntentId);

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

        var state = await grain.GetStateAsync();
        state.CapturedAmount.Should().Be(7500);
        state.AuthorizedAmount.Should().Be(10000);
    }

    // =========================================================================
    // Timeout Handling Tests (using MockProcessor delay feature)
    // =========================================================================

    [Fact]
    public async Task Timeout_MockProcessor_WithDelay_ShouldComplete()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetMockProcessorGrain(accountId, paymentIntentId);

        // Configure a small delay
        await grain.ConfigureNextResponseAsync(true, null, 100);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            2500,
            "usd",
            "pm_card_4242",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await grain.AuthorizeAsync(request);
        stopwatch.Stop();

        // Assert
        result.Success.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(100);
    }

    [Fact]
    public async Task Timeout_MockProcessor_ConfiguredFailure_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetMockProcessorGrain(accountId, paymentIntentId);

        // Configure timeout failure
        await grain.ConfigureNextResponseAsync(false, "timeout", 50);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            3000,
            "usd",
            "pm_card_4242",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.DeclineCode.Should().Be("timeout");
    }

    // =========================================================================
    // Processor Failover Scenario Tests
    // =========================================================================

    [Fact]
    public async Task Failover_StripeProcessor_CircuitOpenReturnsUnavailable()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var circuitKey = $"stripe:{accountId}";
        _circuitKeysToClean.Add(circuitKey);

        // Force circuit open by recording many failures
        for (int i = 0; i < 5; i++)
        {
            PaymentProcessorRetryHelper.RecordFailure(circuitKey);
        }

        var grain = GetStripeProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.DeclineCode.Should().Be("circuit_open");
        result.DeclineMessage.Should().Contain("temporarily unavailable");
    }

    [Fact]
    public async Task Failover_AdyenProcessor_CircuitOpenReturnsUnavailable()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var circuitKey = $"adyen:{accountId}";
        _circuitKeysToClean.Add(circuitKey);

        // Force circuit open
        for (int i = 0; i < 5; i++)
        {
            PaymentProcessorRetryHelper.RecordFailure(circuitKey);
        }

        var grain = GetAdyenProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            3000,
            "EUR",
            "scheme_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.DeclineCode.Should().Be("circuit_open");
    }

    [Fact]
    public async Task Failover_SuccessAfterCircuitRecovery()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var circuitKey = $"stripe:{accountId}";
        _circuitKeysToClean.Add(circuitKey);

        // Force circuit open then reset to simulate recovery
        for (int i = 0; i < 5; i++)
        {
            PaymentProcessorRetryHelper.RecordFailure(circuitKey);
        }

        // Simulate circuit recovery
        PaymentProcessorRetryHelper.ResetCircuit(circuitKey);

        var grain = GetStripeProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeTrue("should succeed after circuit recovery");
    }

    [Fact]
    public async Task Failover_CaptureBlockedByOpenCircuit()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var circuitKey = $"stripe:{accountId}";
        _circuitKeysToClean.Add(circuitKey);

        var grain = GetStripeProcessorGrain(accountId, paymentIntentId);

        // First authorize successfully
        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            8000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: false,
            StatementDescriptor: null,
            Metadata: null));

        authResult.Success.Should().BeTrue();

        // Now force circuit open
        for (int i = 0; i < 5; i++)
        {
            PaymentProcessorRetryHelper.RecordFailure(circuitKey);
        }

        // Act
        var captureResult = await grain.CaptureAsync(authResult.TransactionId!, 8000);

        // Assert
        captureResult.Success.Should().BeFalse();
        captureResult.ErrorCode.Should().Be("circuit_open");
    }

    // =========================================================================
    // Error Classification in Grain Context Tests
    // =========================================================================

    [Theory]
    [InlineData("pm_card_0002", "card_declined")]
    [InlineData("pm_card_9995", "insufficient_funds")]
    [InlineData("pm_card_0069", "expired_card")]
    [InlineData("pm_card_0127", "incorrect_cvc")]
    [InlineData("pm_card_0119", "processing_error")]
    public async Task ErrorClassification_MockProcessor_ReturnsCorrectDeclineCode(string cardToken, string expectedDeclineCode)
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetMockProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            2500,
            "usd",
            cardToken,
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.DeclineCode.Should().Be(expectedDeclineCode);
        result.DeclineMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ErrorClassification_ConfiguredFailure_TracksInState()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetMockProcessorGrain(accountId, paymentIntentId);

        await grain.ConfigureNextResponseAsync(false, "rate_limit");

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            4000,
            "usd",
            "pm_card_4242",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        await grain.AuthorizeAsync(request);

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be("failed");
        state.LastError.Should().NotBeNullOrEmpty();
    }

    // =========================================================================
    // State Management Tests
    // =========================================================================

    [Fact]
    public async Task State_StripeProcessor_TracksFullLifecycle()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetStripeProcessorGrain(accountId, paymentIntentId);

        // Act - Authorize
        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            10000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: false,
            StatementDescriptor: "Test Merchant",
            Metadata: new Dictionary<string, string> { ["order_id"] = "ord_123" }));

        // Assert - Authorized state
        var stateAfterAuth = await grain.GetStateAsync();
        stateAfterAuth.Status.Should().Be("authorized");
        stateAfterAuth.AuthorizedAmount.Should().Be(10000);
        stateAfterAuth.CapturedAmount.Should().Be(0);

        // Act - Capture
        await grain.CaptureAsync(authResult.TransactionId!, 10000);

        // Assert - Captured state
        var stateAfterCapture = await grain.GetStateAsync();
        stateAfterCapture.Status.Should().Be("captured");
        stateAfterCapture.CapturedAmount.Should().Be(10000);

        // Act - Partial refund
        await grain.RefundAsync(authResult.TransactionId!, 3000, "customer_request");

        // Assert - Partially refunded
        var stateAfterRefund = await grain.GetStateAsync();
        stateAfterRefund.RefundedAmount.Should().Be(3000);
        stateAfterRefund.Status.Should().Be("captured"); // Still captured (partial refund)

        // Act - Full refund of remaining
        await grain.RefundAsync(authResult.TransactionId!, 7000, "customer_request");

        // Assert - Fully refunded
        var finalState = await grain.GetStateAsync();
        finalState.Status.Should().Be("refunded");
        finalState.RefundedAmount.Should().Be(10000);
    }

    [Fact]
    public async Task State_AdyenProcessor_TracksEvents()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetAdyenProcessorGrain(accountId, paymentIntentId);

        // Act - Multiple operations
        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "EUR",
            "scheme_visa",
            CaptureAutomatically: false,
            StatementDescriptor: null,
            Metadata: null));

        await grain.CaptureAsync(authResult.TransactionId!, 5000);
        await grain.RefundAsync(authResult.TransactionId!, 2000, "partial_refund");

        // Assert
        var state = await grain.GetStateAsync();
        state.Events.Should().HaveCountGreaterThanOrEqualTo(3);
        state.Events.Should().Contain(e => e.EventType.Contains("authorized") || e.EventType.Contains("captured"));
    }

    // =========================================================================
    // Adyen Split Payment Tests
    // =========================================================================

    [Fact]
    public async Task SplitPayment_Adyen_ProcessesMultipleSplits()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetAdyenProcessorGrain(accountId, paymentIntentId);

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
            new("platform_account", 9000, "Default", "platform_revenue"),
            new("merchant_account", 1000, "Commission", "merchant_commission")
        };

        // Act
        var result = await grain.AuthorizeWithSplitAsync(request, splits);

        // Assert
        result.Success.Should().BeTrue();
        result.TransactionId.Should().NotBeNullOrEmpty();

        var state = await grain.GetStateAsync();
        state.Status.Should().Be("captured");
        state.Events.Should().Contain(e => e.EventType.Contains("split"));
    }

    // =========================================================================
    // Stripe Connect Tests
    // =========================================================================

    [Fact]
    public async Task Connect_Stripe_AuthorizeOnBehalfOf()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetStripeProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            8000,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: "Platform Payment",
            Metadata: null);

        // Act
        var result = await grain.AuthorizeOnBehalfOfAsync(
            request,
            connectedAccountId: "acct_connected_merchant",
            applicationFee: 400); // 5% fee

        // Assert
        result.Success.Should().BeTrue();
        result.TransactionId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Connect_Stripe_SetupIntent()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetStripeProcessorGrain(accountId, paymentIntentId);

        // Initialize grain first
        await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            100,
            "usd",
            "pm_card_visa",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        var clientSecret = await grain.CreateSetupIntentAsync("cus_test_customer_123");

        // Assert
        clientSecret.Should().NotBeNullOrEmpty();
        clientSecret.Should().Contain("_secret_");
    }

    // =========================================================================
    // Multiple Refund Tests
    // =========================================================================

    [Fact]
    public async Task MultipleRefunds_TracksTotalCorrectly()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetMockProcessorGrain(accountId, paymentIntentId);

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            10000, // $100
            "usd",
            "pm_card_4242",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act - Multiple partial refunds
        var refund1 = await grain.RefundAsync(authResult.TransactionId!, 2000, "partial_1");
        var refund2 = await grain.RefundAsync(authResult.TransactionId!, 3000, "partial_2");
        var refund3 = await grain.RefundAsync(authResult.TransactionId!, 2500, "partial_3");

        // Assert
        refund1.Success.Should().BeTrue();
        refund2.Success.Should().BeTrue();
        refund3.Success.Should().BeTrue();

        var state = await grain.GetStateAsync();
        state.RefundedAmount.Should().Be(7500);
        state.Status.Should().Be("captured"); // Not fully refunded yet

        // Act - Final refund
        var refund4 = await grain.RefundAsync(authResult.TransactionId!, 2500, "final");

        // Assert
        refund4.Success.Should().BeTrue();
        var finalState = await grain.GetStateAsync();
        finalState.RefundedAmount.Should().Be(10000);
        finalState.Status.Should().Be("refunded");
    }

    [Fact]
    public async Task MultipleRefunds_ExceedsBalance_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetMockProcessorGrain(accountId, paymentIntentId);

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_4242",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // First refund
        await grain.RefundAsync(authResult.TransactionId!, 3000, "partial");

        // Act - Try to refund more than remaining
        var result = await grain.RefundAsync(authResult.TransactionId!, 3000, "over_refund");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("amount_too_large");
    }
}
