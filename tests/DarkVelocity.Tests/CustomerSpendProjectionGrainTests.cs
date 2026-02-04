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

    // ==================== Year/Month Rollover Tests ====================

    [Fact]
    public async Task RecordSpendAsync_InitialState_ShouldSetCurrentYearAndMonth()
    {
        // This test verifies that the grain properly tracks year/month for YTD/MTD calculations
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // Record some spend
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 100m,
            GrossSpend: 108m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 3,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        var state = await grain.GetStateAsync();
        Assert.Equal(DateTime.UtcNow.Year, state.CurrentYear);
        Assert.Equal(DateTime.UtcNow.Month, state.CurrentMonth);
        Assert.Equal(100m, state.YearToDateSpend);
        Assert.Equal(100m, state.MonthToDateSpend);
    }

    [Fact]
    public async Task RecordSpendAsync_MultipleInSamePeriod_ShouldAccumulateYtdMtd()
    {
        // Verifies that multiple spends in the same year/month accumulate properly
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // Record first spend
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 100m,
            GrossSpend: 108m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 3,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        // Record second spend
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 150m,
            GrossSpend: 162m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 5,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        var state = await grain.GetStateAsync();
        Assert.Equal(250m, state.YearToDateSpend);
        Assert.Equal(250m, state.MonthToDateSpend);
        Assert.Equal(250m, state.LifetimeSpend);
    }

    // ==================== Recent Transactions Tests ====================

    [Fact]
    public async Task RecordSpendAsync_RecentTransactions_ShouldLimitTo100()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // Record 105 transactions
        for (int i = 0; i < 105; i++)
        {
            await grain.RecordSpendAsync(new RecordSpendCommand(
                OrderId: Guid.NewGuid(),
                SiteId: siteId,
                NetSpend: 10m,
                GrossSpend: 10.80m,
                DiscountAmount: 0m,
                TaxAmount: 0m,
                ItemCount: 1,
                TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));
        }

        var state = await grain.GetStateAsync();

        // Should be limited to 100 recent transactions
        Assert.Equal(100, state.RecentTransactions.Count);
        // But lifetime should reflect all 105 transactions
        Assert.Equal(105, state.LifetimeTransactions);
    }

    // ==================== Reverse Spend Tests ====================

    [Fact]
    public async Task ReverseSpendAsync_NonExistentOrder_ShouldHandleGracefully()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // Record some initial spend
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
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

        // Try to reverse a non-existent order
        await grain.ReverseSpendAsync(new ReverseSpendCommand(
            OrderId: Guid.NewGuid(), // Non-existent order
            Amount: 50m,
            Reason: "Non-existent order refund"));

        // Should reduce spend but not crash
        var stateAfter = await grain.GetStateAsync();
        Assert.Equal(150m, stateAfter.LifetimeSpend);
        // Points should remain unchanged since no original transaction found
        Assert.Equal(200, stateAfter.AvailablePoints);
    }

    // ==================== Zero Spend Tests ====================

    [Fact]
    public async Task RecordSpendAsync_ZeroSpend_ShouldHandle()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // Record zero spend (e.g., free item, full discount)
        var result = await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 0m,
            GrossSpend: 0m,
            DiscountAmount: 100m, // Full discount
            TaxAmount: 0m,
            ItemCount: 1,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        Assert.Equal(0, result.PointsEarned);
        Assert.Equal(0, result.TotalPoints);

        var state = await grain.GetStateAsync();
        Assert.Equal(0m, state.LifetimeSpend);
        Assert.Equal(1, state.LifetimeTransactions); // Still counts as transaction
    }

    // ==================== First Transaction Tests ====================

    [Fact]
    public async Task RecordSpendAsync_ShouldTrackFirstTransactionAt()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        var beforeFirstTransaction = DateTime.UtcNow;

        // Record first spend
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 100m,
            GrossSpend: 108m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 3,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        var stateAfterFirst = await grain.GetStateAsync();
        Assert.NotNull(stateAfterFirst.FirstTransactionAt);
        Assert.True(stateAfterFirst.FirstTransactionAt >= beforeFirstTransaction);

        var firstTransactionTime = stateAfterFirst.FirstTransactionAt;

        // Record second spend
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 50m,
            GrossSpend: 54m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 2,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        var stateAfterSecond = await grain.GetStateAsync();
        // FirstTransactionAt should NOT change on subsequent transactions
        Assert.Equal(firstTransactionTime, stateAfterSecond.FirstTransactionAt);
    }

    // ==================== Version Tests ====================

    [Fact]
    public async Task RecordSpendAsync_ShouldUpdateVersion()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        var stateInitial = await grain.GetStateAsync();
        var initialVersion = stateInitial.Version;

        // Record first spend
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 100m,
            GrossSpend: 108m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 3,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        var stateAfterFirst = await grain.GetStateAsync();
        Assert.Equal(initialVersion + 1, stateAfterFirst.Version);

        // Record second spend
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 50m,
            GrossSpend: 54m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 2,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        var stateAfterSecond = await grain.GetStateAsync();
        Assert.Equal(initialVersion + 2, stateAfterSecond.Version);
    }

    // ==================== Redemption Details Tests ====================

    [Fact]
    public async Task RedeemPointsAsync_ShouldTrackRedemptionDetails()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var redemptionOrderId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // Earn some points first
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 500m,
            GrossSpend: 540m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 10,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        // Redeem points
        var result = await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(
            Points: 200,
            OrderId: redemptionOrderId,
            RewardType: "PercentageDiscount"));

        Assert.Equal(2.00m, result.DiscountValue); // 200 points = $2.00
        Assert.Equal(300, result.RemainingPoints);

        // Verify redemption is tracked
        var state = await grain.GetStateAsync();
        Assert.Equal(200, state.TotalPointsRedeemed);
        Assert.Single(state.RecentRedemptions);

        var redemption = state.RecentRedemptions[0];
        Assert.Equal(redemptionOrderId, redemption.OrderId);
        Assert.Equal(200, redemption.PointsRedeemed);
        Assert.Equal(2.00m, redemption.DiscountValue);
        Assert.Equal("PercentageDiscount", redemption.RewardType);
    }

    [Fact]
    public async Task RedeemPointsAsync_MultipleRedemptions_ShouldTrackAll()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // Earn points
        await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 1000m,
            GrossSpend: 1080m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            ItemCount: 20,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        // Multiple redemptions
        await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(100, Guid.NewGuid(), "Discount"));
        await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(150, Guid.NewGuid(), "FreeItem"));
        await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(200, Guid.NewGuid(), "Upgrade"));

        var state = await grain.GetStateAsync();
        Assert.Equal(450, state.TotalPointsRedeemed);
        Assert.Equal(3, state.RecentRedemptions.Count);

        // Most recent redemption should be first
        Assert.Equal("Upgrade", state.RecentRedemptions[0].RewardType);
    }
}
