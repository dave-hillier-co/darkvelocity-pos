using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.TestingHost;

namespace DarkVelocity.Tests;

/// <summary>
/// Advanced tests for CustomerSpendProjectionGrain covering:
/// - Multi-tier transitions (upgrade and downgrade paths)
/// - Edge cases around tier boundaries
/// - Points calculation accuracy at tier transitions
/// - Historical spend tracking across year/month boundaries
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class CustomerSpendProjectionAdvancedTests
{
    private readonly TestCluster _cluster;

    public CustomerSpendProjectionAdvancedTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    private async Task<ICustomerSpendProjectionGrain> CreateAndInitializeProjectionAsync(Guid orgId, Guid customerId)
    {
        var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));
        await grain.InitializeAsync(orgId, customerId);
        return grain;
    }

    // ==================== TIER BOUNDARY TRANSITION TESTS ====================

    [Fact]
    public async Task RecordSpendAsync_AtExactTierBoundary_ShouldPromote()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        // Act - Spend exactly $500 (Silver threshold)
        var result = await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 500m, 540m, 0m, 40m, 10, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Assert
        Assert.True(result.TierChanged);
        Assert.Equal("Silver", result.NewTier);
        Assert.Equal("Silver", result.CurrentTier);
    }

    [Fact]
    public async Task RecordSpendAsync_JustBelowTierBoundary_ShouldNotPromote()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        // Act - Spend $499.99 (just below Silver threshold)
        var result = await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 499.99m, 540m, 40.01m, 40m, 10, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Assert
        Assert.False(result.TierChanged);
        Assert.Equal("Bronze", result.CurrentTier);
    }

    [Fact]
    public async Task RecordSpendAsync_CrossingMultipleTiers_ShouldEndAtCorrectTier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        // Act - Spend $2000 in one transaction (skips Bronze and Silver, lands in Gold)
        var result = await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 2000m, 2160m, 0m, 160m, 50, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Assert - Should be Gold (1500-5000)
        Assert.True(result.TierChanged);
        Assert.Equal("Gold", result.CurrentTier);
    }

    [Fact]
    public async Task RecordSpendAsync_ReachingMaxTier_ShouldHaveNoNextTier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        // Act - Spend $5500 (reaches Platinum, the max tier at $5000+)
        await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 5500m, 5940m, 0m, 440m, 100, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        Assert.Equal("Platinum", snapshot.CurrentTier);
        Assert.Null(snapshot.NextTier);
        Assert.Equal(0, snapshot.SpendToNextTier);
    }

    // ==================== TIER DEMOTION TESTS ====================

    [Fact]
    public async Task ReverseSpendAsync_DemotingMultipleTiers_ShouldEndAtCorrectTier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        // Get to Gold tier
        await grain.RecordSpendAsync(new RecordSpendCommand(
            orderId, siteId, 2000m, 2160m, 0m, 160m, 50, DateOnly.FromDateTime(DateTime.UtcNow)));

        var stateBefore = await grain.GetStateAsync();
        Assert.Equal("Gold", stateBefore.CurrentTier);

        // Act - Reverse $1800 (should drop from Gold back to Bronze)
        await grain.ReverseSpendAsync(new ReverseSpendCommand(orderId, 1800m, "Large refund"));

        // Assert
        var stateAfter = await grain.GetStateAsync();
        Assert.Equal("Bronze", stateAfter.CurrentTier); // 200 spend, Bronze is 0-500
        Assert.Equal(200m, stateAfter.LifetimeSpend);
    }

    [Fact]
    public async Task ReverseSpendAsync_ToExactlyAtLowerTierMin_ShouldBeAtLowerTier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId1 = Guid.NewGuid();
        var orderId2 = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        // Get to Silver tier with $600 spend
        await grain.RecordSpendAsync(new RecordSpendCommand(
            orderId1, siteId, 500m, 540m, 0m, 40m, 10, DateOnly.FromDateTime(DateTime.UtcNow)));
        await grain.RecordSpendAsync(new RecordSpendCommand(
            orderId2, siteId, 100m, 108m, 0m, 8m, 5, DateOnly.FromDateTime(DateTime.UtcNow)));

        var stateBefore = await grain.GetStateAsync();
        Assert.Equal("Silver", stateBefore.CurrentTier);

        // Act - Reverse $101 (brings spend to $499, just below Silver threshold of $500)
        await grain.ReverseSpendAsync(new ReverseSpendCommand(orderId2, 101m, "Partial refund"));

        // Assert
        var stateAfter = await grain.GetStateAsync();
        Assert.Equal("Bronze", stateAfter.CurrentTier);
        Assert.Equal(499m, stateAfter.LifetimeSpend);
    }

    // ==================== POINTS CALCULATION AT TIER TRANSITIONS ====================

    [Fact]
    public async Task RecordSpendAsync_FirstTransactionAtNewTier_ShouldUseNewTierMultiplier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        // First get to Silver tier
        await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 600m, 648m, 0m, 48m, 15, DateOnly.FromDateTime(DateTime.UtcNow)));

        var silverState = await grain.GetStateAsync();
        Assert.Equal("Silver", silverState.CurrentTier);
        Assert.Equal(1.25m, silverState.CurrentTierMultiplier);

        // Act - Record another transaction (should use Silver multiplier)
        var result = await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 100m, 108m, 0m, 8m, 3, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Assert - Points should be 100 * 1.0 * 1.25 = 125
        Assert.Equal(125, result.PointsEarned);
    }

    [Fact]
    public async Task RecordSpendAsync_TransactionThatPromotes_ShouldUseMultiplierAtTimeOfTransaction()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        // Get to just under Silver
        await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 400m, 432m, 0m, 32m, 10, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Act - This transaction will promote to Silver, but points are calculated at Bronze rate first
        var result = await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 150m, 162m, 0m, 12m, 5, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Assert - Points calculated at Bronze rate (1.0) for this transaction
        // Note: The tier promotion happens after points are calculated, so we use the Bronze multiplier
        Assert.Equal(150, result.PointsEarned); // 150 * 1.0 * 1.0
        Assert.True(result.TierChanged);
        Assert.Equal("Silver", result.NewTier);
    }

    // ==================== COMPLEX SPEND/REVERSE SCENARIOS ====================

    [Fact]
    public async Task SpendAndReverse_MultipleCycles_ShouldMaintainAccuracy()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        // Act - Cycle 1: Spend then reverse
        var order1 = Guid.NewGuid();
        await grain.RecordSpendAsync(new RecordSpendCommand(
            order1, siteId, 300m, 324m, 0m, 24m, 10, DateOnly.FromDateTime(DateTime.UtcNow)));
        await grain.ReverseSpendAsync(new ReverseSpendCommand(order1, 100m, "Partial"));

        // Cycle 2: More spend
        var order2 = Guid.NewGuid();
        await grain.RecordSpendAsync(new RecordSpendCommand(
            order2, siteId, 400m, 432m, 0m, 32m, 10, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Cycle 3: Reverse previous
        await grain.ReverseSpendAsync(new ReverseSpendCommand(order2, 200m, "Partial"));

        // Final spend
        await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 150m, 162m, 0m, 12m, 5, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Assert
        var state = await grain.GetStateAsync();
        // 300 - 100 + 400 - 200 + 150 = 550
        Assert.Equal(550m, state.LifetimeSpend);
        Assert.Equal("Silver", state.CurrentTier); // 550 >= 500
    }

    [Fact]
    public async Task ReverseSpendAsync_MoreThanLifetimeSpend_ShouldNotGoNegative()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        await grain.RecordSpendAsync(new RecordSpendCommand(
            orderId, siteId, 100m, 108m, 0m, 8m, 5, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Act - Try to reverse more than we have
        await grain.ReverseSpendAsync(new ReverseSpendCommand(Guid.NewGuid(), 500m, "Over-refund"));

        // Assert
        var state = await grain.GetStateAsync();
        Assert.Equal(0m, state.LifetimeSpend); // Should not go negative
        Assert.Equal("Bronze", state.CurrentTier);
    }

    // ==================== POINTS REDEMPTION EDGE CASES ====================

    [Fact]
    public async Task RedeemPointsAsync_ExactBalance_ShouldZeroOut()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 200m, 216m, 0m, 16m, 5, DateOnly.FromDateTime(DateTime.UtcNow)));

        var pointsBefore = await grain.GetAvailablePointsAsync();
        Assert.Equal(200, pointsBefore);

        // Act - Redeem exactly all points
        var result = await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(Guid.NewGuid(), 200, "Full redemption"));

        // Assert
        Assert.Equal(0, result.RemainingPoints);
        var pointsAfter = await grain.GetAvailablePointsAsync();
        Assert.Equal(0, pointsAfter);
    }

    [Fact]
    public async Task RedeemPointsAsync_MultipleSmallRedemptions_ShouldTrackCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 500m, 540m, 0m, 40m, 10, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Act - Multiple small redemptions
        await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(Guid.NewGuid(), 50, "Redemption 1"));
        await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(Guid.NewGuid(), 75, "Redemption 2"));
        await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(Guid.NewGuid(), 25, "Redemption 3"));
        var lastResult = await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(Guid.NewGuid(), 100, "Redemption 4"));

        // Assert
        Assert.Equal(250, lastResult.RemainingPoints); // 500 - 50 - 75 - 25 - 100 = 250
        var state = await grain.GetStateAsync();
        Assert.Equal(250, state.TotalPointsRedeemed);
    }

    [Fact]
    public async Task RedeemPointsAsync_AfterPointsExpiredFromReversal_ShouldReflectCorrectBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        // Earn points
        await grain.RecordSpendAsync(new RecordSpendCommand(
            orderId, siteId, 300m, 324m, 0m, 24m, 10, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Reverse some spend (which claws back points)
        await grain.ReverseSpendAsync(new ReverseSpendCommand(orderId, 100m, "Partial refund"));

        // Act - Try to redeem
        var hasSufficient = await grain.HasSufficientPointsAsync(100);
        var points = await grain.GetAvailablePointsAsync();

        // Assert
        // After reversal of 100, the points associated with that order are clawed back
        // Original order earned 300 points, after reversal we should have 0 (points clawed back entirely)
        Assert.Equal(0, points); // Points clawed back when order is reversed
        Assert.False(hasSufficient);
    }

    // ==================== CUSTOM TIER CONFIGURATION TESTS ====================

    [Fact]
    public async Task ConfigureTiersAsync_NewTiersCausePromotion_ShouldUpdateTier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        // Record $300 spend (Bronze with default tiers)
        await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 300m, 324m, 0m, 24m, 10, DateOnly.FromDateTime(DateTime.UtcNow)));

        var stateBefore = await grain.GetStateAsync();
        Assert.Equal("Bronze", stateBefore.CurrentTier);

        // Act - Configure new tiers with lower thresholds
        await grain.ConfigureTiersAsync([
            new SpendTier { Name = "Starter", MinSpend = 0, MaxSpend = 100, PointsMultiplier = 1.0m, PointsPerDollar = 1.0m },
            new SpendTier { Name = "Regular", MinSpend = 100, MaxSpend = 250, PointsMultiplier = 1.2m, PointsPerDollar = 1.0m },
            new SpendTier { Name = "VIP", MinSpend = 250, MaxSpend = decimal.MaxValue, PointsMultiplier = 2.0m, PointsPerDollar = 1.0m }
        ]);

        // Assert - Should now be VIP since $300 >= $250
        var stateAfter = await grain.GetStateAsync();
        Assert.Equal("VIP", stateAfter.CurrentTier);
        Assert.Equal(2.0m, stateAfter.CurrentTierMultiplier);
    }

    [Fact]
    public async Task ConfigureTiersAsync_NewTiersCauseDemotion_ShouldUpdateTier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        // Record $600 spend (Silver with default tiers)
        await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 600m, 648m, 0m, 48m, 15, DateOnly.FromDateTime(DateTime.UtcNow)));

        var stateBefore = await grain.GetStateAsync();
        Assert.Equal("Silver", stateBefore.CurrentTier);

        // Act - Configure new tiers with higher thresholds
        await grain.ConfigureTiersAsync([
            new SpendTier { Name = "Basic", MinSpend = 0, MaxSpend = 1000, PointsMultiplier = 1.0m, PointsPerDollar = 1.0m },
            new SpendTier { Name = "Premium", MinSpend = 1000, MaxSpend = decimal.MaxValue, PointsMultiplier = 1.5m, PointsPerDollar = 1.0m }
        ]);

        // Assert - Should now be Basic since $600 < $1000
        var stateAfter = await grain.GetStateAsync();
        Assert.Equal("Basic", stateAfter.CurrentTier);
        Assert.Equal(1.0m, stateAfter.CurrentTierMultiplier);
    }

    // ==================== SNAPSHOT ACCURACY TESTS ====================

    [Fact]
    public async Task GetSnapshotAsync_AfterMultipleOperations_ShouldBeAccurate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        // Multiple operations
        await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 1000m, 1080m, 0m, 80m, 20, DateOnly.FromDateTime(DateTime.UtcNow)));
        await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 400m, 432m, 0m, 32m, 10, DateOnly.FromDateTime(DateTime.UtcNow)));
        await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(Guid.NewGuid(), 300, "Redemption"));
        await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 200m, 216m, 0m, 16m, 5, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        Assert.Equal(customerId, snapshot.CustomerId);
        Assert.Equal(1600m, snapshot.LifetimeSpend); // 1000 + 400 + 200
        Assert.Equal("Gold", snapshot.CurrentTier); // 1600 >= 1500
        Assert.Equal(1.5m, snapshot.TierMultiplier);
        Assert.Equal(3, snapshot.LifetimeTransactions);
        Assert.NotNull(snapshot.LastTransactionAt);

        // Points: 1000*1.0 + 400*1.25 (Silver multiplier after 1000) + 200*1.5 (Gold after 1400) = 1000 + 500 + 300 - 300 redemption
        // Actually the calculation happens during RecordSpend, need to trace through
        // After first spend (1000): 1000 points, promoted to Silver
        // After second spend (400): 400 * 1.25 = 500 points, total 1500, promoted to Gold
        // Redeemed 300
        // After third spend (200): 200 * 1.5 = 300 points
        // Total available: 1000 + 500 - 300 + 300 = 1500
        Assert.Equal(1500, snapshot.AvailablePoints);
    }

    [Fact]
    public async Task GetSnapshotAsync_SpendToNextTier_ShouldBeAccurate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAndInitializeProjectionAsync(orgId, customerId);

        // Spend $1250 (Gold tier at 1500, so 250 to go)
        await grain.RecordSpendAsync(new RecordSpendCommand(
            Guid.NewGuid(), siteId, 1250m, 1350m, 0m, 100m, 30, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        Assert.Equal("Silver", snapshot.CurrentTier);
        Assert.Equal("Gold", snapshot.NextTier);
        Assert.Equal(250m, snapshot.SpendToNextTier); // 1500 - 1250
    }
}
