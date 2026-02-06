using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class SettlementBatchGrainTests
{
    private readonly TestClusterFixture _fixture;

    public SettlementBatchGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // Given: a new settlement batch for today's business date at a site
    // When: the batch is opened
    // Then: the batch is in Open status with a BATCH- prefixed number and the correct business date
    [Fact]
    public async Task OpenAsync_ShouldCreateBatch()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISettlementBatchGrain>(
            GrainKeys.Batch(orgId, siteId, batchId));

        // Act
        var result = await grain.OpenAsync(new OpenBatchCommand(orgId, siteId, businessDate, userId));

        // Assert
        result.BatchId.Should().Be(batchId);
        result.BatchNumber.Should().StartWith("BATCH-");
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(SettlementBatchStatus.Open);
        state.BusinessDate.Should().Be(businessDate);
    }

    // Given: an open settlement batch and an initiated credit card payment of $100
    // When: the payment is added to the batch
    // Then: the batch contains one payment with a total amount of $100
    [Fact]
    public async Task AddPaymentAsync_ShouldAddPaymentToBatch()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISettlementBatchGrain>(
            GrainKeys.Batch(orgId, siteId, batchId));
        await grain.OpenAsync(new OpenBatchCommand(orgId, siteId, DateOnly.FromDateTime(DateTime.UtcNow), userId));

        // Create a payment first
        var paymentGrain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(
            GrainKeys.Payment(orgId, siteId, paymentId));
        await paymentGrain.InitiateAsync(new InitiatePaymentCommand(
            orgId, siteId, Guid.NewGuid(), PaymentMethod.CreditCard, 100m, userId));

        // Act
        await grain.AddPaymentAsync(new AddPaymentToBatchCommand(
            paymentId, 100m, PaymentMethod.CreditCard, "ref123"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Payments.Should().HaveCount(1);
        state.TotalAmount.Should().Be(100m);
        state.PaymentCount.Should().Be(1);
    }

    // Given: an open settlement batch
    // When: the batch is closed by a user
    // Then: the batch status transitions to Closed
    [Fact]
    public async Task CloseAsync_ShouldCloseBatch()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISettlementBatchGrain>(
            GrainKeys.Batch(orgId, siteId, batchId));
        await grain.OpenAsync(new OpenBatchCommand(orgId, siteId, DateOnly.FromDateTime(DateTime.UtcNow), userId));

        // Act
        var result = await grain.CloseAsync(new CloseBatchCommand(userId));

        // Assert
        result.BatchId.Should().Be(batchId);
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(SettlementBatchStatus.Closed);
    }

    // Given: a closed settlement batch containing a $100 credit card payment
    // When: the batch is settled with $2.50 in processing fees
    // Then: the settled amount is $100, processing fees are $2.50, net amount is $97.50, and status is Settled
    [Fact]
    public async Task RecordSettlementAsync_ShouldSettleBatch()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISettlementBatchGrain>(
            GrainKeys.Batch(orgId, siteId, batchId));
        await grain.OpenAsync(new OpenBatchCommand(orgId, siteId, DateOnly.FromDateTime(DateTime.UtcNow), userId));

        // Create and add a payment
        var paymentId = Guid.NewGuid();
        var paymentGrain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(
            GrainKeys.Payment(orgId, siteId, paymentId));
        await paymentGrain.InitiateAsync(new InitiatePaymentCommand(
            orgId, siteId, Guid.NewGuid(), PaymentMethod.CreditCard, 100m, userId));
        await grain.AddPaymentAsync(new AddPaymentToBatchCommand(paymentId, 100m, PaymentMethod.CreditCard, "ref123"));

        await grain.CloseAsync(new CloseBatchCommand(userId));

        // Act
        var result = await grain.RecordSettlementAsync(new SettleBatchCommand("SETTLE-123", 2.50m));

        // Assert
        result.SettledAmount.Should().Be(100m);
        result.ProcessingFees.Should().Be(2.50m);
        result.NetAmount.Should().Be(97.50m);
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(SettlementBatchStatus.Settled);
    }

    // Given: an open settlement batch with a $100 credit card payment and a $50 cash payment
    // When: the totals by payment method are retrieved
    // Then: two method groups are returned with the correct amounts for credit card and cash
    [Fact]
    public async Task GetTotalsByMethodAsync_ShouldGroupByPaymentMethod()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISettlementBatchGrain>(
            GrainKeys.Batch(orgId, siteId, batchId));
        await grain.OpenAsync(new OpenBatchCommand(orgId, siteId, DateOnly.FromDateTime(DateTime.UtcNow), userId));

        // Add multiple payments of different types
        var paymentId1 = Guid.NewGuid();
        var paymentGrain1 = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(
            GrainKeys.Payment(orgId, siteId, paymentId1));
        await paymentGrain1.InitiateAsync(new InitiatePaymentCommand(
            orgId, siteId, Guid.NewGuid(), PaymentMethod.CreditCard, 100m, userId));
        await grain.AddPaymentAsync(new AddPaymentToBatchCommand(paymentId1, 100m, PaymentMethod.CreditCard));

        var paymentId2 = Guid.NewGuid();
        var paymentGrain2 = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(
            GrainKeys.Payment(orgId, siteId, paymentId2));
        await paymentGrain2.InitiateAsync(new InitiatePaymentCommand(
            orgId, siteId, Guid.NewGuid(), PaymentMethod.Cash, 50m, userId));
        await grain.AddPaymentAsync(new AddPaymentToBatchCommand(paymentId2, 50m, PaymentMethod.Cash));

        // Act
        var totals = await grain.GetTotalsByMethodAsync();

        // Assert
        totals.Should().HaveCount(2);
        totals.Should().Contain(t => t.Method == PaymentMethod.CreditCard && t.TotalAmount == 100m);
        totals.Should().Contain(t => t.Method == PaymentMethod.Cash && t.TotalAmount == 50m);
    }

    // Given: a settlement batch that has been closed
    // When: the batch is reopened with a reason
    // Then: the batch status transitions back to Open
    [Fact]
    public async Task ReopenAsync_ShouldReopenClosedBatch()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISettlementBatchGrain>(
            GrainKeys.Batch(orgId, siteId, batchId));
        await grain.OpenAsync(new OpenBatchCommand(orgId, siteId, DateOnly.FromDateTime(DateTime.UtcNow), userId));
        await grain.CloseAsync(new CloseBatchCommand(userId));

        // Act
        await grain.ReopenAsync(userId, "Need to add more payments");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(SettlementBatchStatus.Open);
    }

    // Given: a closed settlement batch ready for settlement
    // When: the settlement fails with a GATEWAY_ERROR
    // Then: the batch status becomes Failed with the error code recorded and the settlement attempt count incremented
    [Fact]
    public async Task RecordSettlementFailureAsync_ShouldRecordFailure()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISettlementBatchGrain>(
            GrainKeys.Batch(orgId, siteId, batchId));
        await grain.OpenAsync(new OpenBatchCommand(orgId, siteId, DateOnly.FromDateTime(DateTime.UtcNow), userId));
        await grain.CloseAsync(new CloseBatchCommand(userId));

        // Act
        await grain.RecordSettlementFailureAsync("GATEWAY_ERROR", "Settlement gateway unavailable");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(SettlementBatchStatus.Failed);
        state.LastErrorCode.Should().Be("GATEWAY_ERROR");
        state.SettlementAttempts.Should().Be(1);
    }
}
