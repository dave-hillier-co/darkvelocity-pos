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

    // Given: a new customer spend projection grain
    // When: the projection is initialized for a customer
    // Then: the state is set with Bronze tier, zero spend, zero points, and default multiplier
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

    // Given: an initialized customer spend projection at Bronze tier
    // When: a $100 transaction is recorded
    // Then: 100 points are earned, lifetime spend is $100, and the tier remains Bronze
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

    // Given: a customer at Bronze tier with $400 in recorded spend
    // When: a $150 transaction pushes lifetime spend to $550 (crossing the $500 Silver threshold)
    // Then: the customer is promoted to Silver tier with a 1.25x earning multiplier
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

    // Given: a customer already promoted to Silver tier (1.25x multiplier)
    // When: a $100 transaction is recorded
    // Then: 125 points are earned (100 * 1.0 * 1.25 Silver multiplier)
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

    // Given: a customer with 500 available loyalty points from spending
    // When: 100 points are redeemed for a discount
    // Then: the points balance is reduced to 400 and a $1.00 discount value is returned
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

    // Given: a customer with zero available loyalty points
    // When: a 100-point redemption is attempted
    // Then: the redemption is rejected due to insufficient points
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

    // Given: a customer with $200 in recorded spend and 200 earned points
    // When: the full $200 order is refunded
    // Then: lifetime spend and available points both return to zero
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

    // Given: a customer at Silver tier with $600 in recorded spend
    // When: a $400 refund reduces lifetime spend to $200 (below the $500 Silver threshold)
    // Then: the customer is demoted back to Bronze tier
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

    // Given: a customer at Silver tier with $750 in lifetime spend
    // When: the spend projection snapshot is retrieved
    // Then: the snapshot shows Silver tier, 1.25x multiplier, and $750 remaining to Gold tier
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

    // Given: a customer with 200 earned loyalty points from spending
    // When: point sufficiency is checked for various amounts
    // Then: checks pass for 100 and 200 points but fail for 201 points
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

    // Given: a customer at Bronze tier with $100 in spend under default tier thresholds
    // When: custom tiers are configured with a VIP threshold at $50
    // Then: the customer is automatically re-evaluated and promoted to VIP tier
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

    // Given: a newly initialized customer spend projection
    // When: the first transaction is recorded
    // Then: the current year, month, YTD spend, and MTD spend are all set correctly
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

    // Given: an initialized customer spend projection
    // When: two transactions ($100 and $150) are recorded in the same period
    // Then: YTD, MTD, and lifetime spend all accumulate to $250
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

    // Given: an initialized customer spend projection
    // When: 105 transactions are recorded
    // Then: only the 100 most recent are retained but lifetime transaction count reflects all 105
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

    // Given: a customer with $200 in recorded spend and 200 points
    // When: a $50 refund is issued against a non-existent order ID
    // Then: lifetime spend is reduced but points remain unchanged since no original transaction was found
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

    // Given: an initialized customer spend projection
    // When: a zero-spend transaction is recorded (e.g., fully discounted order)
    // Then: no points are earned but the transaction is still counted
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

    // Given: an initialized customer spend projection with no transactions
    // When: two transactions are recorded sequentially
    // Then: the first transaction timestamp is set once and not overwritten by subsequent transactions
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

    // Given: an initialized customer spend projection
    // When: two transactions are recorded sequentially
    // Then: the state version increments by one with each recorded transaction
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

    // Given: a customer with 500 earned loyalty points from spending
    // When: 200 points are redeemed for a percentage discount
    // Then: the redemption details (order, points, discount value, reward type) are tracked
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

    // Given: a customer with 1000 earned loyalty points from spending
    // When: three separate redemptions are made for different reward types
    // Then: all redemptions are tracked and the most recent appears first in history
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
        await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(Guid.NewGuid(), 100, "Discount"));
        await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(Guid.NewGuid(), 150, "FreeItem"));
        await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(Guid.NewGuid(), 200, "Upgrade"));

        var state = await grain.GetStateAsync();
        Assert.Equal(450, state.TotalPointsRedeemed);
        Assert.Equal(3, state.RecentRedemptions.Count);

        // Most recent redemption should be first
        Assert.Equal("Upgrade", state.RecentRedemptions[0].RewardType);
    }
}
