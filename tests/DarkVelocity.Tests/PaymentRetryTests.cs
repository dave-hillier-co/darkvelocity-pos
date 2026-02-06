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

    // Given: an initiated credit card payment of $100
    // When: a retry is scheduled due to a gateway timeout
    // Then: the retry count is 1, a future retry time is set, and retries are not yet exhausted
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

    // Given: an initiated credit card payment of $100
    // When: a retry is scheduled with a custom maximum of 5 retries
    // Then: the retry info reflects the custom max retries value
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

    // Given: a payment with a scheduled retry after a gateway timeout
    // When: the retry attempt succeeds
    // Then: the retry schedule is cleared, the error is removed, and a successful attempt is recorded in history
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

    // Given: a payment with a scheduled retry after a gateway timeout
    // When: the retry attempt fails with a TIMEOUT error code
    // Then: the error code and message are recorded and the failed attempt is added to retry history
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

    // Given: a payment that has already failed 3 retry attempts (the default maximum)
    // When: a fourth retry is scheduled
    // Then: retries are marked as exhausted and no further retry time is scheduled
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

    // Given: a payment whose retries have already been exhausted
    // When: another retry is scheduled
    // Then: an InvalidOperationException is thrown indicating retries are already exhausted
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

    // Given: an initiated payment with no retries ever scheduled
    // When: the payment is checked for pending retries
    // Then: no retry is pending
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

    // Given: a payment whose retries have been exhausted (max retries of 1 exceeded)
    // When: the payment is checked for pending retries
    // Then: no retry is pending because retries are exhausted
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

    // Given: a payment with a retry scheduled due to a gateway error, configured for up to 5 retries
    // When: the retry info is retrieved
    // Then: the info contains the retry count, max retries, next retry time, and the original error message
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

    // Given: a payment that undergoes multiple retry cycles
    // When: two retries fail with different error codes and the third succeeds
    // Then: all three attempts are recorded in the retry history with their respective outcomes and error codes
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
