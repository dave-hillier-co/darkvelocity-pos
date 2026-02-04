using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MockProcessorGrainTests
{
    private readonly TestClusterFixture _fixture;

    public MockProcessorGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IMockProcessorGrain GetProcessorGrain(Guid accountId, Guid paymentIntentId)
        => _fixture.Cluster.GrainFactory.GetGrain<IMockProcessorGrain>($"{accountId}:mock:{paymentIntentId}");

    [Fact]
    public async Task AuthorizeAsync_WithSuccessfulCard_ShouldSucceed()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            1000,
            "usd",
            "pm_card_4242", // Success card
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.TransactionId.Should().NotBeNullOrEmpty();
        result.AuthorizationCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AuthorizeAsync_WithDeclinedCard_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            1000,
            "usd",
            "pm_card_0002", // Declined card
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.DeclineCode.Should().Be("card_declined");
        result.DeclineMessage.Should().Contain("declined");
    }

    [Fact]
    public async Task AuthorizeAsync_WithInsufficientFundsCard_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            1000,
            "usd",
            "pm_card_9995", // Insufficient funds
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.DeclineCode.Should().Be("insufficient_funds");
    }

    [Fact]
    public async Task AuthorizeAsync_With3dsCard_ShouldRequireAction()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            1000,
            "usd",
            "pm_card_3155", // 3DS required card
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.RequiredAction.Should().NotBeNull();
        result.RequiredAction!.Type.Should().Be("redirect_to_url");
    }

    [Fact]
    public async Task CaptureAsync_ShouldCaptureAuthorizedPayment()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_4242",
            CaptureAutomatically: false, // Manual capture
            StatementDescriptor: null,
            Metadata: null));

        // Act
        var result = await grain.CaptureAsync(authResult.TransactionId!, 5000);

        // Assert
        result.Success.Should().BeTrue();
        result.CapturedAmount.Should().Be(5000);
    }

    [Fact]
    public async Task CaptureAsync_WithInvalidTransaction_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        // Act
        var result = await grain.CaptureAsync("invalid_transaction_id", 1000);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_transaction");
    }

    [Fact]
    public async Task RefundAsync_ShouldRefundCapturedPayment()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_4242",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        var result = await grain.RefundAsync(authResult.TransactionId!, 2500, "customer_request");

        // Assert
        result.Success.Should().BeTrue();
        result.RefundedAmount.Should().Be(2500);
    }

    [Fact]
    public async Task RefundAsync_FullAmount_ShouldSetStatusToRefunded()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_4242",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        await grain.RefundAsync(authResult.TransactionId!, 5000, "customer_request");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be("refunded");
    }

    [Fact]
    public async Task VoidAsync_ShouldVoidAuthorizedPayment()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_4242",
            CaptureAutomatically: false,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        var result = await grain.VoidAsync(authResult.TransactionId!, "requested_by_customer");

        // Assert
        result.Success.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.Status.Should().Be("voided");
    }

    [Fact]
    public async Task ConfigureNextResponseAsync_ShouldOverrideNextResponse()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        await grain.ConfigureNextResponseAsync(false, "card_declined");

        // Act
        var result = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            1000,
            "usd",
            "pm_card_4242", // Would normally succeed
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Assert
        result.Success.Should().BeFalse();
        result.DeclineCode.Should().Be("card_declined");
    }

    [Fact]
    public async Task GetStateAsync_ShouldReturnCurrentState()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            3000,
            "usd",
            "pm_card_4242",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        var state = await grain.GetStateAsync();

        // Assert
        state.ProcessorName.Should().Be("mock");
        state.PaymentIntentId.Should().Be(paymentIntentId);
        state.Status.Should().Be("captured");
        state.CapturedAmount.Should().Be(3000);
    }

    // =========================================================================
    // Additional Test Card Scenarios
    // =========================================================================

    [Fact]
    public async Task Authorize_WithExpiredCard_ShouldDecline()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            2000,
            "usd",
            "pm_card_0069", // Expired card (4000000000000069)
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.DeclineCode.Should().Be("expired_card");
        result.DeclineMessage.Should().Contain("expired");
    }

    [Fact]
    public async Task Authorize_WithCvcFailCard_ShouldDecline()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            1500,
            "usd",
            "pm_card_0127", // CVC fail card (4000000000000127)
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.DeclineCode.Should().Be("incorrect_cvc");
        result.DeclineMessage.Should().Contain("security code");
    }

    [Fact]
    public async Task Authorize_WithProcessingErrorCard_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var request = new ProcessorAuthRequest(
            paymentIntentId,
            3000,
            "usd",
            "pm_card_0119", // Processing error card (4000000000000119)
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null);

        // Act
        var result = await grain.AuthorizeAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.DeclineCode.Should().Be("processing_error");
        result.DeclineMessage.Should().Contain("error");
    }

    [Fact]
    public async Task HandleWebhook_3dsCompletion_ShouldUpdateStatus()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        // First trigger 3DS requirement
        await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_3155", // 3DS required card
            CaptureAutomatically: false,
            StatementDescriptor: null,
            Metadata: null));

        var stateBeforeWebhook = await grain.GetStateAsync();
        stateBeforeWebhook.Status.Should().Be("requires_action");

        // Act - Simulate 3DS completion webhook
        await grain.HandleWebhookAsync("next_action_completed", "{\"authenticated\":true}");

        // Assert
        var stateAfterWebhook = await grain.GetStateAsync();
        stateAfterWebhook.Status.Should().Be("authorized");
        stateAfterWebhook.TransactionId.Should().NotBeNullOrEmpty();
        stateAfterWebhook.AuthorizationCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SimulateDispute_ShouldChangeStatus()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            10000,
            "usd",
            "pm_card_4242",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Act
        await grain.SimulateDisputeAsync(10000, "fraudulent");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be("disputed");
        state.Events.Should().Contain(e => e.Type == "dispute_created");
    }

    [Fact]
    public async Task PartialCapture_LessThanAuthorized_ShouldSucceed()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        // Authorize for $100
        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            10000,
            "usd",
            "pm_card_4242",
            CaptureAutomatically: false, // Manual capture
            StatementDescriptor: null,
            Metadata: null));

        authResult.Success.Should().BeTrue();

        // Act - Capture only $75
        var captureResult = await grain.CaptureAsync(authResult.TransactionId!, 7500);

        // Assert
        captureResult.Success.Should().BeTrue();
        captureResult.CapturedAmount.Should().Be(7500);

        var state = await grain.GetStateAsync();
        state.Status.Should().Be("captured");
        state.CapturedAmount.Should().Be(7500);
    }

    [Fact]
    public async Task Capture_ExceedsAuthorizedAmount_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        // Authorize for $50
        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_4242",
            CaptureAutomatically: false,
            StatementDescriptor: null,
            Metadata: null));

        // Act - Try to capture more than authorized
        var captureResult = await grain.CaptureAsync(authResult.TransactionId!, 7500);

        // Assert
        captureResult.Success.Should().BeFalse();
        captureResult.ErrorCode.Should().Be("amount_too_large");
    }

    [Fact]
    public async Task Refund_ExceedsAvailableBalance_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_4242",
            CaptureAutomatically: true,
            StatementDescriptor: null,
            Metadata: null));

        // Refund part
        await grain.RefundAsync(authResult.TransactionId!, 3000, "partial_refund");

        // Act - Try to refund more than remaining balance
        var refundResult = await grain.RefundAsync(authResult.TransactionId!, 3000, "over_refund");

        // Assert
        refundResult.Success.Should().BeFalse();
        refundResult.ErrorCode.Should().Be("amount_too_large");
    }

    [Fact]
    public async Task Void_AlreadyCaptured_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetProcessorGrain(accountId, paymentIntentId);

        var authResult = await grain.AuthorizeAsync(new ProcessorAuthRequest(
            paymentIntentId,
            5000,
            "usd",
            "pm_card_4242",
            CaptureAutomatically: true, // Automatically captured
            StatementDescriptor: null,
            Metadata: null));

        // Act - Try to void a captured transaction
        var voidResult = await grain.VoidAsync(authResult.TransactionId!, "cancel_request");

        // Assert
        voidResult.Success.Should().BeFalse();
        voidResult.ErrorCode.Should().Be("invalid_state");
    }
}
