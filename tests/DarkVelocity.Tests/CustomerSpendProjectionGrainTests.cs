using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.TestingHost;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class CustomerSpendProjectionGrainTests
{
    private readonly TestCluster _cluster;

    public CustomerSpendProjectionGrainTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task InitializeAsync_SetsInitialState()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        var state = await grain.GetStateAsync();

        Assert.Equal(customerId, state.CustomerId);
        Assert.Equal(orgId, state.OrganizationId);
        Assert.Equal("Bronze", state.CurrentTier);
        Assert.Equal(1.0m, state.CurrentTierMultiplier);
        Assert.Equal(0m, state.LifetimeSpend);
        Assert.Equal(0, state.AvailablePoints);
    }

    [Fact]
    public async Task RecordSpendAsync_AccumulatesSpend()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        var result = await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: orderId,
            SiteId: siteId,
            NetSpend: 100m,
            GrossSpend: 108m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 3,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        Assert.Equal(100, result.PointsEarned); // 100 * 1.0 * 1.0 = 100
        Assert.Equal(100, result.TotalPoints);
        Assert.Equal("Bronze", result.CurrentTier);
        Assert.False(result.TierChanged);
        Assert.Null(result.NewTier); // No tier change

        var state = await grain.GetStateAsync();
        Assert.Equal(100m, state.LifetimeSpend);
        Assert.Equal(1, state.LifetimeTransactions);
    }

    [Fact]
    public async Task RecordSpendAsync_TriggerstierPromotion()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // Record spend just under Silver threshold
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 400m,
            GrossSpend: 432m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 10,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        var state1 = await grain.GetStateAsync();
        Assert.Equal("Bronze", state1.CurrentTier);

        // Record spend that crosses Silver threshold (500)
        var result = await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 150m,
            GrossSpend: 162m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 5,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        Assert.True(result.TierChanged);
        Assert.Equal("Silver", result.NewTier);

        var state2 = await grain.GetStateAsync();
        Assert.Equal("Silver", state2.CurrentTier);
        Assert.Equal(1.25m, state2.CurrentTierMultiplier);
        Assert.Equal(550m, state2.LifetimeSpend);
    }

    [Fact]
    public async Task RecordSpendAsync_EarnsPointsWithMultiplier()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // First, get to Silver tier
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 600m,
            GrossSpend: 648m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 15,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        var state = await grain.GetStateAsync();
        Assert.Equal("Silver", state.CurrentTier);

        // Record another spend - should earn at 1.25x multiplier
        var result = await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 100m,
            GrossSpend: 108m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 3,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        // 100 * 1.0 * 1.25 = 125 points
        Assert.Equal(125, result.PointsEarned);
    }

    [Fact]
    public async Task RedeemPointsAsync_DeductsPoints()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // Earn some points
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 500m,
            GrossSpend: 540m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 10,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        var pointsBefore = await grain.GetAvailablePointsAsync();
        Assert.Equal(500, pointsBefore);

        // Redeem points
        var result = await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(
            Points: 100,
            OrderId: orderId,
            RewardType: "Discount"));

        Assert.Equal(1.00m, result.DiscountValue); // 100 points = $1.00
        Assert.Equal(400, result.RemainingPoints);

        var pointsAfter = await grain.GetAvailablePointsAsync();
        Assert.Equal(400, pointsAfter);
    }

    [Fact]
    public async Task RedeemPointsAsync_ThrowsOnInsufficientPoints()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // Try to redeem points without having any
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            grain.RedeemPointsAsync(new RedeemSpendPointsCommand(
                Points: 100,
                OrderId: orderId,
                RewardType: "Discount")));
    }

    [Fact]
    public async Task ReverseSpendAsync_ReducesSpendAndPoints()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // Record spend
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: orderId,
            SiteId: siteId,
            NetSpend: 200m,
            GrossSpend: 216m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 5,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        var stateBefore = await grain.GetStateAsync();
        Assert.Equal(200m, stateBefore.LifetimeSpend);
        Assert.Equal(200, stateBefore.AvailablePoints);

        // Reverse the spend
        await grain.ReverseSpendAsync(new ReverseSpendCommand(
            OrderId: orderId,
            Amount: 200m,
            Reason: "Order refund"));

        var stateAfter = await grain.GetStateAsync();
        Assert.Equal(0m, stateAfter.LifetimeSpend);
        Assert.Equal(0, stateAfter.AvailablePoints);
    }

    [Fact]
    public async Task ReverseSpendAsync_CanCauseTierDemotion()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // Get to Silver tier
        var orderId1 = Guid.NewGuid();
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: orderId1,
            SiteId: siteId,
            NetSpend: 600m,
            GrossSpend: 648m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 15,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        var state1 = await grain.GetStateAsync();
        Assert.Equal("Silver", state1.CurrentTier);

        // Reverse a large portion - should demote to Bronze
        await grain.ReverseSpendAsync(new ReverseSpendCommand(
            OrderId: orderId1,
            Amount: 400m,
            Reason: "Partial refund"));

        var state2 = await grain.GetStateAsync();
        Assert.Equal("Bronze", state2.CurrentTier);
        Assert.Equal(200m, state2.LifetimeSpend);
    }

    [Fact]
    public async Task GetSnapshotAsync_ReturnsCorrectData()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 750m,
            GrossSpend: 810m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 20,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        var snapshot = await grain.GetSnapshotAsync();

        Assert.Equal(customerId, snapshot.CustomerId);
        Assert.Equal(750m, snapshot.LifetimeSpend);
        Assert.Equal("Silver", snapshot.CurrentTier);
        Assert.Equal(1.25m, snapshot.TierMultiplier);
        Assert.Equal(750m, snapshot.SpendToNextTier); // 1500 - 750 = 750 to Gold
        Assert.Equal("Gold", snapshot.NextTier);
        Assert.Equal(1, snapshot.LifetimeTransactions);
        Assert.NotNull(snapshot.LastTransactionAt);
    }

    [Fact]
    public async Task HasSufficientPointsAsync_ReturnsCorrectly()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // No points yet
        Assert.False(await grain.HasSufficientPointsAsync(100));

        // Earn some points
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 200m,
            GrossSpend: 216m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 5,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        Assert.True(await grain.HasSufficientPointsAsync(100));
        Assert.True(await grain.HasSufficientPointsAsync(200));
        Assert.False(await grain.HasSufficientPointsAsync(201));
    }

    [Fact]
    public async Task ConfigureTiersAsync_AppliesCustomTiers()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // Record spend that would be Bronze with default tiers
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 100m,
            GrossSpend: 108m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 3,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        var state1 = await grain.GetStateAsync();
        Assert.Equal("Bronze", state1.CurrentTier);

        // Configure custom tiers with lower thresholds
        await grain.ConfigureTiersAsync(
        [
            new SpendTier { Name = "Starter", MinSpend = 0, MaxSpend = 50, PointsMultiplier = 1.0m, PointsPerDollar = 1.0m },
            new SpendTier { Name = "VIP", MinSpend = 50, MaxSpend = decimal.MaxValue, PointsMultiplier = 2.0m, PointsPerDollar = 1.0m }
        ]);

        // Should now be VIP tier since spend ($100) > threshold ($50)
        var state2 = await grain.GetStateAsync();
        Assert.Equal("VIP", state2.CurrentTier);
        Assert.Equal(2.0m, state2.CurrentTierMultiplier);
    }
}
