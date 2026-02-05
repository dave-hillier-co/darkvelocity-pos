using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PaymentRetryTests
{
    private readonly TestClusterFixture _fixture;

    public PaymentRetryTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IPaymentGrain> CreateInitiatedPaymentAsync(Guid orgId, Guid siteId, Guid paymentId, decimal amount)
    {
        // Create an order first
        var orderId = Guid.NewGuid();
        var orderGrain = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
        await orderGrain.CreateAsync(new CreateOrderCommand(orgId, siteId, Guid.NewGuid(), OrderType.DineIn));
        await orderGrain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, amount));

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
        await grain.InitiateAsync(new InitiatePaymentCommand(orgId, siteId, orderId, PaymentMethod.CreditCard, amount, Guid.NewGuid()));

        return grain;
    }

    [Fact]
    public async Task ScheduleRetryAsync_ShouldScheduleRetry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = await CreateInitiatedPaymentAsync(orgId, siteId, paymentId, 100m);

        // Act
        await grain.ScheduleRetryAsync("Gateway timeout");

        // Assert
        var retryInfo = await grain.GetRetryInfoAsync();
        retryInfo.RetryCount.Should().Be(1);
        retryInfo.NextRetryAt.Should().NotBeNull();
        retryInfo.NextRetryAt.Should().BeAfter(DateTime.UtcNow);
        retryInfo.RetryExhausted.Should().BeFalse();
    }

    [Fact]
    public async Task ScheduleRetryAsync_WithCustomMaxRetries_ShouldUseCustomValue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = await CreateInitiatedPaymentAsync(orgId, siteId, paymentId, 100m);

        // Act
        await grain.ScheduleRetryAsync("Gateway timeout", maxRetries: 5);

        // Assert
        var retryInfo = await grain.GetRetryInfoAsync();
        retryInfo.MaxRetries.Should().Be(5);
    }

    [Fact]
    public async Task RecordRetryAttemptAsync_Success_ShouldClearRetryState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = await CreateInitiatedPaymentAsync(orgId, siteId, paymentId, 100m);
        await grain.ScheduleRetryAsync("Gateway timeout");

        // Act
        await grain.RecordRetryAttemptAsync(success: true);

        // Assert
        var retryInfo = await grain.GetRetryInfoAsync();
        retryInfo.NextRetryAt.Should().BeNull();
        retryInfo.LastErrorCode.Should().BeNull();

        var state = await grain.GetStateAsync();
        state.RetryHistory.Should().HaveCount(1);
        state.RetryHistory[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task RecordRetryAttemptAsync_Failure_ShouldRecordError()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = await CreateInitiatedPaymentAsync(orgId, siteId, paymentId, 100m);
        await grain.ScheduleRetryAsync("Gateway timeout");

        // Act
        await grain.RecordRetryAttemptAsync(success: false, errorCode: "TIMEOUT", errorMessage: "Connection timeout");

        // Assert
        var retryInfo = await grain.GetRetryInfoAsync();
        retryInfo.LastErrorCode.Should().Be("TIMEOUT");
        retryInfo.LastErrorMessage.Should().Be("Connection timeout");

        var state = await grain.GetStateAsync();
        state.RetryHistory.Should().HaveCount(1);
        state.RetryHistory[0].Success.Should().BeFalse();
    }

    [Fact]
    public async Task ScheduleRetryAsync_ExceedsMaxRetries_ShouldMarkExhausted()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = await CreateInitiatedPaymentAsync(orgId, siteId, paymentId, 100m);

        // Schedule 3 retries (default max)
        await grain.ScheduleRetryAsync("Failure 1");
        await grain.RecordRetryAttemptAsync(false, "ERROR", "Failed");
        await grain.ScheduleRetryAsync("Failure 2");
        await grain.RecordRetryAttemptAsync(false, "ERROR", "Failed");
        await grain.ScheduleRetryAsync("Failure 3");
        await grain.RecordRetryAttemptAsync(false, "ERROR", "Failed");

        // Act - Try to schedule beyond max
        await grain.ScheduleRetryAsync("Failure 4");

        // Assert
        var retryInfo = await grain.GetRetryInfoAsync();
        retryInfo.RetryExhausted.Should().BeTrue();
        retryInfo.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public async Task ScheduleRetryAsync_AlreadyExhausted_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = await CreateInitiatedPaymentAsync(orgId, siteId, paymentId, 100m);

        // Exhaust retries
        await grain.ScheduleRetryAsync("Failure 1", maxRetries: 1);
        await grain.RecordRetryAttemptAsync(false, "ERROR", "Failed");
        await grain.ScheduleRetryAsync("Failure 2"); // This exhausts it

        // Act
        var act = () => grain.ScheduleRetryAsync("Failure 3");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exhausted*");
    }

    [Fact]
    public async Task ShouldRetryAsync_NotScheduled_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = await CreateInitiatedPaymentAsync(orgId, siteId, paymentId, 100m);

        // Act
        var shouldRetry = await grain.ShouldRetryAsync();

        // Assert
        shouldRetry.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldRetryAsync_Exhausted_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = await CreateInitiatedPaymentAsync(orgId, siteId, paymentId, 100m);

        // Exhaust retries
        await grain.ScheduleRetryAsync("Failure", maxRetries: 1);
        await grain.RecordRetryAttemptAsync(false);
        await grain.ScheduleRetryAsync("Failure 2");

        // Act
        var shouldRetry = await grain.ShouldRetryAsync();

        // Assert
        shouldRetry.Should().BeFalse();
    }

    [Fact]
    public async Task GetRetryInfoAsync_ShouldReturnCompleteInfo()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = await CreateInitiatedPaymentAsync(orgId, siteId, paymentId, 100m);
        await grain.ScheduleRetryAsync("Gateway error", maxRetries: 5);

        // Act
        var retryInfo = await grain.GetRetryInfoAsync();

        // Assert
        retryInfo.RetryCount.Should().Be(1);
        retryInfo.MaxRetries.Should().Be(5);
        retryInfo.NextRetryAt.Should().NotBeNull();
        retryInfo.RetryExhausted.Should().BeFalse();
        retryInfo.LastErrorMessage.Should().Be("Gateway error");
    }

    [Fact]
    public async Task RetryHistory_ShouldTrackAllAttempts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = await CreateInitiatedPaymentAsync(orgId, siteId, paymentId, 100m);

        // Act - Multiple retry attempts
        await grain.ScheduleRetryAsync("First failure");
        await grain.RecordRetryAttemptAsync(false, "ERR1", "Error 1");
        await grain.ScheduleRetryAsync("Second failure");
        await grain.RecordRetryAttemptAsync(false, "ERR2", "Error 2");
        await grain.ScheduleRetryAsync("Third try");
        await grain.RecordRetryAttemptAsync(true);

        // Assert
        var state = await grain.GetStateAsync();
        state.RetryHistory.Should().HaveCount(3);
        state.RetryHistory[0].Success.Should().BeFalse();
        state.RetryHistory[0].ErrorCode.Should().Be("ERR1");
        state.RetryHistory[1].Success.Should().BeFalse();
        state.RetryHistory[1].ErrorCode.Should().Be("ERR2");
        state.RetryHistory[2].Success.Should().BeTrue();
    }
}
