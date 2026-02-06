using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class OfflinePaymentQueueGrainTests
{
    private readonly TestClusterFixture _fixture;

    public OfflinePaymentQueueGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // Given: an initialized offline payment queue for a site
    // When: a credit card payment of $100 is queued due to a gateway timeout
    // Then: a queue entry is created with a future retry time and the queue statistics show one pending payment
    [Fact]
    public async Task QueuePaymentAsync_ShouldQueuePayment()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOfflinePaymentQueueGrain>(
            GrainKeys.OfflinePaymentQueue(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act
        var result = await grain.QueuePaymentAsync(new QueuePaymentCommand(
            paymentId,
            Guid.NewGuid(),
            PaymentMethod.CreditCard,
            100m,
            "{}",
            "Gateway timeout"));

        // Assert
        result.QueueEntryId.Should().NotBeEmpty();
        result.NextRetryAt.Should().NotBeNull();

        var stats = await grain.GetStatisticsAsync();
        stats.PendingCount.Should().Be(1);
        stats.TotalQueued.Should().Be(1);
    }

    // Given: a queued offline payment that has been picked up for processing
    // When: the payment processing succeeds with a gateway reference
    // Then: the entry status becomes Processed with the gateway reference and the processed count increments
    [Fact]
    public async Task RecordSuccessAsync_ShouldMarkPaymentAsProcessed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOfflinePaymentQueueGrain>(
            GrainKeys.OfflinePaymentQueue(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var result = await grain.QueuePaymentAsync(new QueuePaymentCommand(
            paymentId,
            Guid.NewGuid(),
            PaymentMethod.CreditCard,
            100m,
            "{}",
            "Gateway timeout"));

        await grain.ProcessPaymentAsync(result.QueueEntryId);

        // Act
        await grain.RecordSuccessAsync(result.QueueEntryId, "gateway-ref-123");

        // Assert
        var entry = await grain.GetPaymentEntryAsync(result.QueueEntryId);
        entry.Should().NotBeNull();
        entry!.Status.Should().Be(OfflinePaymentStatus.Processed);
        entry.GatewayReference.Should().Be("gateway-ref-123");

        var stats = await grain.GetStatisticsAsync();
        stats.TotalProcessed.Should().Be(1);
    }

    // Given: a queued offline payment that has been picked up for processing
    // When: the payment processing fails with a TIMEOUT error
    // Then: the entry is re-queued for retry with an incremented attempt count and a future retry time
    [Fact]
    public async Task RecordFailureAsync_ShouldScheduleRetry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOfflinePaymentQueueGrain>(
            GrainKeys.OfflinePaymentQueue(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var result = await grain.QueuePaymentAsync(new QueuePaymentCommand(
            paymentId,
            Guid.NewGuid(),
            PaymentMethod.CreditCard,
            100m,
            "{}",
            "Gateway timeout"));

        await grain.ProcessPaymentAsync(result.QueueEntryId);

        // Act
        await grain.RecordFailureAsync(result.QueueEntryId, "TIMEOUT", "Connection timeout");

        // Assert
        var entry = await grain.GetPaymentEntryAsync(result.QueueEntryId);
        entry.Should().NotBeNull();
        entry!.Status.Should().Be(OfflinePaymentStatus.Queued); // Back to queued for retry
        entry.AttemptCount.Should().Be(1);
        entry.NextRetryAt.Should().NotBeNull();
        entry.LastErrorCode.Should().Be("TIMEOUT");
    }

    // Given: an offline payment queue configured for a maximum of 2 retries
    // When: the queued payment fails processing twice
    // Then: the entry status becomes Failed and the failed count increments in the queue statistics
    [Fact]
    public async Task RecordFailureAsync_AfterMaxRetries_ShouldMarkAsFailed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOfflinePaymentQueueGrain>(
            GrainKeys.OfflinePaymentQueue(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.ConfigureRetrySettingsAsync(2, 1, 1.0); // Only 2 retries

        var result = await grain.QueuePaymentAsync(new QueuePaymentCommand(
            paymentId,
            Guid.NewGuid(),
            PaymentMethod.CreditCard,
            100m,
            "{}",
            "Gateway timeout"));

        // First attempt
        await grain.ProcessPaymentAsync(result.QueueEntryId);
        await grain.RecordFailureAsync(result.QueueEntryId, "ERROR", "Failed");

        // Second attempt
        await grain.ProcessPaymentAsync(result.QueueEntryId);
        await grain.RecordFailureAsync(result.QueueEntryId, "ERROR", "Failed again");

        // Assert
        var entry = await grain.GetPaymentEntryAsync(result.QueueEntryId);
        entry.Should().NotBeNull();
        entry!.Status.Should().Be(OfflinePaymentStatus.Failed);

        var stats = await grain.GetStatisticsAsync();
        stats.TotalFailed.Should().Be(1);
    }

    // Given: a payment queued in the offline queue due to a gateway timeout
    // When: the queued payment is cancelled by a user with a reason
    // Then: the entry status becomes Cancelled
    [Fact]
    public async Task CancelPaymentAsync_ShouldCancelQueuedPayment()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOfflinePaymentQueueGrain>(
            GrainKeys.OfflinePaymentQueue(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var result = await grain.QueuePaymentAsync(new QueuePaymentCommand(
            paymentId,
            Guid.NewGuid(),
            PaymentMethod.CreditCard,
            100m,
            "{}",
            "Gateway timeout"));

        // Act
        await grain.CancelPaymentAsync(result.QueueEntryId, userId, "Customer cancelled");

        // Assert
        var entry = await grain.GetPaymentEntryAsync(result.QueueEntryId);
        entry.Should().NotBeNull();
        entry!.Status.Should().Be(OfflinePaymentStatus.Cancelled);
    }

    // Given: two queued offline payments, one of which has been successfully processed
    // When: the pending payments are retrieved
    // Then: only the unprocessed payment is returned
    [Fact]
    public async Task GetPendingPaymentsAsync_ShouldReturnPendingOnly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOfflinePaymentQueueGrain>(
            GrainKeys.OfflinePaymentQueue(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Queue two payments
        var result1 = await grain.QueuePaymentAsync(new QueuePaymentCommand(
            Guid.NewGuid(), Guid.NewGuid(), PaymentMethod.CreditCard, 100m, "{}", "Offline"));
        var result2 = await grain.QueuePaymentAsync(new QueuePaymentCommand(
            Guid.NewGuid(), Guid.NewGuid(), PaymentMethod.Cash, 50m, "{}", "Offline"));

        // Process and complete one
        await grain.ProcessPaymentAsync(result1.QueueEntryId);
        await grain.RecordSuccessAsync(result1.QueueEntryId, "ref-123");

        // Act
        var pending = await grain.GetPendingPaymentsAsync();

        // Assert
        pending.Should().HaveCount(1);
        pending[0].QueueEntryId.Should().Be(result2.QueueEntryId);
    }
}
