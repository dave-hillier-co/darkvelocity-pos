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
