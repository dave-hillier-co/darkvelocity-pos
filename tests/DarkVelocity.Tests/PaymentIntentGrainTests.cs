using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PaymentIntentGrainTests
{
    private readonly TestClusterFixture _fixture;

    public PaymentIntentGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IPaymentIntentGrain GetPaymentIntentGrain(Guid accountId, Guid paymentIntentId)
        => _fixture.Cluster.GrainFactory.GetGrain<IPaymentIntentGrain>($"{accountId}:pi:{paymentIntentId}");

    [Fact]
    public async Task CreateAsync_ShouldCreatePaymentIntent()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        // Act
        var result = await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            1000,
            "usd",
            Description: "Test payment"));

        // Assert
        result.Id.Should().Be(paymentIntentId);
        result.AccountId.Should().Be(accountId);
        result.Amount.Should().Be(1000);
        result.Currency.Should().Be("usd");
        result.Status.Should().Be(PaymentIntentStatus.RequiresPaymentMethod);
        result.ClientSecret.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateAsync_WithPaymentMethod_ShouldSetStatusToRequiresConfirmation()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        // Act
        var result = await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            2000,
            "usd",
            PaymentMethodId: "pm_card_4242"));

        // Assert
        result.Status.Should().Be(PaymentIntentStatus.RequiresConfirmation);
        result.PaymentMethodId.Should().Be("pm_card_4242");
    }

    [Fact]
    public async Task ConfirmAsync_WithSuccessfulCard_ShouldSucceed()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            3000,
            "usd",
            PaymentMethodId: "pm_card_4242"));

        // Act
        var result = await grain.ConfirmAsync(new ConfirmPaymentIntentCommand());

        // Assert
        result.Status.Should().Be(PaymentIntentStatus.Succeeded);
        result.AmountReceived.Should().Be(3000);
    }

    [Fact]
    public async Task ConfirmAsync_WithDeclinedCard_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            3000,
            "usd",
            PaymentMethodId: "pm_card_0002")); // Declined card last4

        // Act
        var result = await grain.ConfirmAsync(new ConfirmPaymentIntentCommand());

        // Assert
        result.Status.Should().Be(PaymentIntentStatus.RequiresPaymentMethod);
        result.LastPaymentError.Should().Contain("declined");
    }

    [Fact]
    public async Task ConfirmAsync_WithManualCapture_ShouldSetStatusToRequiresCapture()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            5000,
            "usd",
            CaptureMethod: CaptureMethod.Manual,
            PaymentMethodId: "pm_card_4242"));

        // Act
        var result = await grain.ConfirmAsync(new ConfirmPaymentIntentCommand());

        // Assert
        result.Status.Should().Be(PaymentIntentStatus.RequiresCapture);
        result.AmountCapturable.Should().Be(5000);
    }

    [Fact]
    public async Task CaptureAsync_ShouldCaptureFullAmount()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            5000,
            "usd",
            CaptureMethod: CaptureMethod.Manual,
            PaymentMethodId: "pm_card_4242"));

        await grain.ConfirmAsync(new ConfirmPaymentIntentCommand());

        // Act
        var result = await grain.CaptureAsync();

        // Assert
        result.Status.Should().Be(PaymentIntentStatus.Succeeded);
        result.AmountReceived.Should().Be(5000);
        result.AmountCapturable.Should().Be(0);
    }

    [Fact]
    public async Task CaptureAsync_WithPartialAmount_ShouldCapturePartialAmount()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            5000,
            "usd",
            CaptureMethod: CaptureMethod.Manual,
            PaymentMethodId: "pm_card_4242"));

        await grain.ConfirmAsync(new ConfirmPaymentIntentCommand());

        // Act
        var result = await grain.CaptureAsync(3000);

        // Assert
        result.Status.Should().Be(PaymentIntentStatus.Succeeded);
        result.AmountReceived.Should().Be(3000);
    }

    [Fact]
    public async Task CancelAsync_ShouldCancelPaymentIntent()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            1000,
            "usd"));

        // Act
        var result = await grain.CancelAsync("requested_by_customer");

        // Assert
        result.Status.Should().Be(PaymentIntentStatus.Canceled);
        result.CanceledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelAsync_WhenSucceeded_ShouldThrow()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            1000,
            "usd",
            PaymentMethodId: "pm_card_4242"));
        await grain.ConfirmAsync(new ConfirmPaymentIntentCommand());

        // Act
        var act = () => grain.CancelAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot cancel*succeeded*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateAmount()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            1000,
            "usd"));

        // Act
        var result = await grain.UpdateAsync(new UpdatePaymentIntentCommand(Amount: 2000));

        // Assert
        result.Amount.Should().Be(2000);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateMetadata()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            1000,
            "usd"));

        // Act
        var result = await grain.UpdateAsync(new UpdatePaymentIntentCommand(
            Metadata: new Dictionary<string, string> { ["order_id"] = "12345" }));

        // Assert
        result.Metadata.Should().ContainKey("order_id");
        result.Metadata!["order_id"].Should().Be("12345");
    }

    [Fact]
    public async Task AttachPaymentMethodAsync_ShouldAttachAndUpdateStatus()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            1000,
            "usd"));

        // Act
        var result = await grain.AttachPaymentMethodAsync("pm_card_4242");

        // Assert
        result.PaymentMethodId.Should().Be("pm_card_4242");
        result.Status.Should().Be(PaymentIntentStatus.RequiresConfirmation);
    }

    [Fact]
    public async Task ExistsAsync_WhenNotCreated_ShouldReturnFalse()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenCreated_ShouldReturnTrue()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(accountId, 1000, "usd"));

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmAsync_WithInsufficientFundsCard_ShouldFail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            3000,
            "usd",
            PaymentMethodId: "pm_card_9995")); // Insufficient funds card last4

        // Act
        var result = await grain.ConfirmAsync(new ConfirmPaymentIntentCommand());

        // Assert
        result.Status.Should().Be(PaymentIntentStatus.RequiresPaymentMethod);
        result.LastPaymentError.Should().Contain("insufficient_funds");
    }

    // =========================================================================
    // 3DS Flow Tests
    // =========================================================================

    [Fact]
    public async Task PaymentIntent_3dsFlow_ShouldCompleteAfterAuthentication()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            5000,
            "usd",
            PaymentMethodId: "pm_card_3155")); // 3DS required card

        // Act - Confirm should trigger 3DS
        var confirmResult = await grain.ConfirmAsync(new ConfirmPaymentIntentCommand());

        // Assert - Should require action
        confirmResult.Status.Should().Be(PaymentIntentStatus.RequiresAction);
        confirmResult.NextAction.Should().NotBeNull();
        confirmResult.NextAction!.Type.Should().Be("redirect_to_url");
    }

    [Fact]
    public async Task PaymentIntent_HandleNextAction_ShouldProcessAuthentication()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            5000,
            "usd",
            PaymentMethodId: "pm_card_3155")); // 3DS required card

        await grain.ConfirmAsync(new ConfirmPaymentIntentCommand());

        // Act - Handle the 3DS action
        var result = await grain.HandleNextActionAsync("{\"authenticated\":true}");

        // Assert
        result.NextAction.Should().BeNull();
        result.Status.Should().Be(PaymentIntentStatus.Processing);
    }

    [Fact]
    public async Task PaymentIntent_RecordAuthorizationAsync_DirectTest()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            2500,
            "usd",
            CaptureMethod: CaptureMethod.Manual));

        // Act - Directly record authorization (simulating callback from processor)
        await grain.RecordAuthorizationAsync("txn_test123", "AUTH456");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PaymentIntentStatus.RequiresCapture);
        snapshot.ProcessorTransactionId.Should().Be("txn_test123");
        snapshot.AmountCapturable.Should().Be(2500);
    }

    [Fact]
    public async Task PaymentIntent_RecordAuthorizationAsync_AutoCapture()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            1500,
            "usd",
            CaptureMethod: CaptureMethod.Automatic));

        // Act - Directly record authorization with automatic capture
        await grain.RecordAuthorizationAsync("txn_auto456", "AUTH789");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PaymentIntentStatus.Succeeded);
        snapshot.ProcessorTransactionId.Should().Be("txn_auto456");
        snapshot.AmountReceived.Should().Be(1500);
        snapshot.SucceededAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PaymentIntent_RecordDeclineAsync_DirectTest()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            3000,
            "usd"));

        // Act - Directly record decline (simulating callback from processor)
        await grain.RecordDeclineAsync("card_declined", "Your card was declined");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PaymentIntentStatus.RequiresPaymentMethod);
        snapshot.LastPaymentError.Should().Contain("card_declined");
        snapshot.LastPaymentError.Should().Contain("Your card was declined");
    }

    [Fact]
    public async Task PaymentIntent_UpdateWhileProcessing_ShouldThrow()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            1000,
            "usd",
            PaymentMethodId: "pm_card_4242"));

        // Complete the payment
        await grain.ConfirmAsync(new ConfirmPaymentIntentCommand());

        // Act - Try to update after succeeded
        var act = () => grain.UpdateAsync(new UpdatePaymentIntentCommand(Amount: 2000));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot update*succeeded*");
    }

    [Fact]
    public async Task PaymentIntent_RecordCaptureAsync_DirectTest()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var grain = GetPaymentIntentGrain(accountId, paymentIntentId);

        await grain.CreateAsync(new CreatePaymentIntentCommand(
            accountId,
            4000,
            "usd",
            CaptureMethod: CaptureMethod.Manual));

        await grain.RecordAuthorizationAsync("txn_cap_test", "AUTH_CAP");

        // Act - Directly record capture (simulating callback from processor)
        await grain.RecordCaptureAsync("txn_cap_test", 4000);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PaymentIntentStatus.Succeeded);
        snapshot.AmountReceived.Should().Be(4000);
        snapshot.AmountCapturable.Should().Be(0);
        snapshot.SucceededAt.Should().NotBeNull();
    }
}
