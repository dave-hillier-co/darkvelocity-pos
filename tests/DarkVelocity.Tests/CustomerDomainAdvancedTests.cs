using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Advanced tests for Customer domain covering:
/// - Loyalty tier transitions (upgrade/downgrade)
/// - Point expiration handling edge cases
/// - Referral program caps
/// - Customer merge scenarios
/// - Birthday reward triggers and history
/// - Historical spend tracking
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class CustomerDomainAdvancedTests
{
    private readonly TestClusterFixture _fixture;

    public CustomerDomainAdvancedTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ICustomerGrain> CreateCustomerAsync(Guid orgId, Guid customerId, string firstName = "John", string lastName = "Doe")
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
        await grain.CreateAsync(new CreateCustomerCommand(orgId, firstName, lastName, $"{firstName.ToLower()}@example.com", "+1234567890"));
        return grain;
    }

    private async Task<ICustomerGrain> CreateLoyaltyCustomerAsync(Guid orgId, Guid customerId, string tierName = "Bronze")
    {
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), $"MEM{customerId.ToString()[..8]}", Guid.NewGuid(), tierName));
        return grain;
    }

    // ==================== LOYALTY TIER TRANSITION TESTS ====================

    [Fact]
    public async Task DemoteTierAsync_ShouldDowngradeTier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateLoyaltyCustomerAsync(orgId, customerId, "Gold");

        var silverTierId = Guid.NewGuid();

        // Act - Demote from Gold to Silver
        await grain.DemoteTierAsync(silverTierId, "Silver", 1000);

        // Assert
        var state = await grain.GetStateAsync();
        state.Loyalty!.TierId.Should().Be(silverTierId);
        state.Loyalty.TierName.Should().Be("Silver");
    }

    [Fact]
    public async Task TierTransition_MultipleUpgrades_ShouldTrackAllChanges()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateLoyaltyCustomerAsync(orgId, customerId, "Bronze");

        // Act - Upgrade through tiers
        await grain.PromoteTierAsync(Guid.NewGuid(), "Silver", 500);
        await grain.PromoteTierAsync(Guid.NewGuid(), "Gold", 250);
        await grain.PromoteTierAsync(Guid.NewGuid(), "Platinum", 0);

        // Assert
        var state = await grain.GetStateAsync();
        state.Loyalty!.TierName.Should().Be("Platinum");
    }

    [Fact]
    public async Task TierTransition_UpgradeThenDowngrade_ShouldReflectCurrentTier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateLoyaltyCustomerAsync(orgId, customerId, "Bronze");
        var siteId = Guid.NewGuid();

        // Earn points to justify tier
        await grain.EarnPointsAsync(new EarnPointsCommand(1000, "Initial purchase"));

        // Upgrade to Gold
        var goldTierId = Guid.NewGuid();
        await grain.PromoteTierAsync(goldTierId, "Gold", 0);

        var stateAfterUpgrade = await grain.GetStateAsync();
        stateAfterUpgrade.Loyalty!.TierName.Should().Be("Gold");

        // Act - Demote back to Silver
        var silverTierId = Guid.NewGuid();
        await grain.DemoteTierAsync(silverTierId, "Silver", 500);

        // Assert
        var state = await grain.GetStateAsync();
        state.Loyalty!.TierName.Should().Be("Silver");
        state.Loyalty.TierId.Should().Be(silverTierId);
    }

    [Fact]
    public async Task TierTransition_WithoutLoyaltyEnrollment_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        // Note: Customer is NOT enrolled in loyalty

        // Act
        var act = () => grain.PromoteTierAsync(Guid.NewGuid(), "Silver", 500);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not enrolled in loyalty*");
    }

    // ==================== POINT EXPIRATION EDGE CASE TESTS ====================

    [Fact]
    public async Task ExpirePointsAsync_MoreThanBalance_ShouldNotGoNegative()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateLoyaltyCustomerAsync(orgId, customerId);
        await grain.EarnPointsAsync(new EarnPointsCommand(50, "Purchase"));

        // Act - Try to expire more points than available
        await grain.ExpirePointsAsync(100, DateTime.UtcNow);

        // Assert - Balance should be 0, not negative
        var balance = await grain.GetPointsBalanceAsync();
        balance.Should().Be(0);
    }

    [Fact]
    public async Task ExpirePointsAsync_ExactBalance_ShouldZeroOut()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateLoyaltyCustomerAsync(orgId, customerId);
        await grain.EarnPointsAsync(new EarnPointsCommand(100, "Purchase"));

        // Act
        await grain.ExpirePointsAsync(100, DateTime.UtcNow);

        // Assert
        var balance = await grain.GetPointsBalanceAsync();
        balance.Should().Be(0);
    }

    [Fact]
    public async Task ExpirePointsAsync_PartialExpiration_ShouldReduceBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateLoyaltyCustomerAsync(orgId, customerId);
        await grain.EarnPointsAsync(new EarnPointsCommand(500, "Purchase"));

        // Act - Expire 200 of 500 points
        await grain.ExpirePointsAsync(200, DateTime.UtcNow);

        // Assert
        var balance = await grain.GetPointsBalanceAsync();
        balance.Should().Be(300);
    }

    [Fact]
    public async Task ExpirePointsAsync_MultipleExpirations_ShouldAccumulate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateLoyaltyCustomerAsync(orgId, customerId);
        await grain.EarnPointsAsync(new EarnPointsCommand(1000, "Purchase"));

        // Act - Multiple expirations
        await grain.ExpirePointsAsync(100, DateTime.UtcNow);
        await grain.ExpirePointsAsync(150, DateTime.UtcNow);
        await grain.ExpirePointsAsync(250, DateTime.UtcNow);

        // Assert
        var balance = await grain.GetPointsBalanceAsync();
        balance.Should().Be(500); // 1000 - 100 - 150 - 250 = 500
    }

    [Fact]
    public async Task ExpirePointsAsync_ZeroPoints_ShouldNotChangeBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateLoyaltyCustomerAsync(orgId, customerId);
        await grain.EarnPointsAsync(new EarnPointsCommand(100, "Purchase"));

        // Act
        await grain.ExpirePointsAsync(0, DateTime.UtcNow);

        // Assert
        var balance = await grain.GetPointsBalanceAsync();
        balance.Should().Be(100);
    }

    [Fact]
    public async Task ExpirePointsAsync_PreservesLifetimePoints()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateLoyaltyCustomerAsync(orgId, customerId);
        await grain.EarnPointsAsync(new EarnPointsCommand(500, "Purchase"));

        // Act
        await grain.ExpirePointsAsync(200, DateTime.UtcNow);

        // Assert - Lifetime should remain unchanged
        var state = await grain.GetStateAsync();
        state.Loyalty!.PointsBalance.Should().Be(300);
        state.Loyalty.LifetimePoints.Should().Be(500); // Lifetime should not decrease
    }

    // ==================== REFERRAL PROGRAM CAP TESTS ====================

    [Fact]
    public async Task CompleteReferralAsync_ExactlyAtCap_ShouldMarkCapReached()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.GenerateReferralCodeAsync();
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));

        // Complete exactly cap number of referrals (default cap is 10)
        for (int i = 0; i < 9; i++)
        {
            var result = await grain.CompleteReferralAsync(new CompleteReferralCommand(Guid.NewGuid(), 100));
            result.CapReached.Should().BeFalse();
        }

        // Act - Complete the 10th (cap) referral
        var finalResult = await grain.CompleteReferralAsync(new CompleteReferralCommand(Guid.NewGuid(), 100));

        // Assert
        finalResult.Success.Should().BeTrue();
        finalResult.TotalReferrals.Should().Be(10);
        finalResult.CapReached.Should().BeTrue();
        // The 10th referral still earns points
        finalResult.PointsAwarded.Should().Be(100);
    }

    [Fact]
    public async Task CompleteReferralAsync_PastCap_ShouldNotAwardPoints()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.GenerateReferralCodeAsync();
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));

        // Complete cap number of referrals
        for (int i = 0; i < 10; i++)
        {
            await grain.CompleteReferralAsync(new CompleteReferralCommand(Guid.NewGuid(), 100));
        }

        // Act - Try to complete an 11th referral
        var result = await grain.CompleteReferralAsync(new CompleteReferralCommand(Guid.NewGuid(), 100));

        // Assert
        result.Success.Should().BeFalse();
        result.PointsAwarded.Should().Be(0);
        result.CapReached.Should().BeTrue();
    }

    [Fact]
    public async Task HasReachedReferralCapAsync_BeforeCap_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.GenerateReferralCodeAsync();
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));

        // Complete 5 referrals (half of cap)
        for (int i = 0; i < 5; i++)
        {
            await grain.CompleteReferralAsync(new CompleteReferralCommand(Guid.NewGuid(), 100));
        }

        // Act
        var hasReachedCap = await grain.HasReachedReferralCapAsync();

        // Assert
        hasReachedCap.Should().BeFalse();
    }

    [Fact]
    public async Task ReferralStatus_ShouldTrackAllReferredCustomers()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.GenerateReferralCodeAsync();
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));

        var referredIds = new List<Guid>();
        for (int i = 0; i < 5; i++)
        {
            var referredId = Guid.NewGuid();
            referredIds.Add(referredId);
            await grain.CompleteReferralAsync(new CompleteReferralCommand(referredId, 100));
        }

        // Act
        var status = await grain.GetReferralStatusAsync();

        // Assert
        status.SuccessfulReferrals.Should().Be(5);
        status.TotalPointsEarnedFromReferrals.Should().Be(500);
        status.ReferredCustomers.Should().HaveCount(5);
        foreach (var referredId in referredIds)
        {
            status.ReferredCustomers.Should().Contain(referredId);
        }
    }

    [Fact]
    public async Task CompleteReferralAsync_WithoutLoyaltyEnrollment_ShouldNotAwardPointsButTrackReferral()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.GenerateReferralCodeAsync();
        // Note: NOT enrolled in loyalty

        // Act
        var result = await grain.CompleteReferralAsync(new CompleteReferralCommand(Guid.NewGuid(), 100));

        // Assert
        result.Success.Should().BeTrue();
        result.TotalReferrals.Should().Be(1);
        // Points are tracked in referral status but not added to loyalty (not enrolled)
        var status = await grain.GetReferralStatusAsync();
        status.TotalPointsEarnedFromReferrals.Should().Be(100);
    }

    // ==================== CUSTOMER MERGE TESTS ====================

    [Fact]
    public async Task MergeFromAsync_MultipleMerges_ShouldTrackAllSources()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var primaryCustomerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, primaryCustomerId);

        var sourceIds = new List<Guid>();
        for (int i = 0; i < 5; i++)
        {
            sourceIds.Add(Guid.NewGuid());
        }

        // Act - Merge multiple customers
        foreach (var sourceId in sourceIds)
        {
            await grain.MergeFromAsync(sourceId);
        }

        // Assert
        var state = await grain.GetStateAsync();
        state.MergedFrom.Should().HaveCount(5);
        foreach (var sourceId in sourceIds)
        {
            state.MergedFrom.Should().Contain(sourceId);
        }
    }

    [Fact]
    public async Task MergeFromAsync_SameCustomerTwice_ShouldAddBothEntries()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var sourceCustomerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act - Merge same customer twice (shouldn't happen in practice, but test the behavior)
        await grain.MergeFromAsync(sourceCustomerId);
        await grain.MergeFromAsync(sourceCustomerId);

        // Assert - Both entries are tracked (deduplication is caller's responsibility)
        var state = await grain.GetStateAsync();
        state.MergedFrom.Should().HaveCount(2);
        state.MergedFrom.Should().AllBeEquivalentTo(sourceCustomerId);
    }

    [Fact]
    public async Task MergeFromAsync_WithLoyaltyEnrolled_ShouldPreservePoints()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var sourceCustomerId = Guid.NewGuid();
        var grain = await CreateLoyaltyCustomerAsync(orgId, customerId);
        await grain.EarnPointsAsync(new EarnPointsCommand(500, "Initial purchase"));

        // Act
        await grain.MergeFromAsync(sourceCustomerId);

        // Assert - Points should remain unchanged after merge tracking
        var state = await grain.GetStateAsync();
        state.Loyalty!.PointsBalance.Should().Be(500);
        state.MergedFrom.Should().Contain(sourceCustomerId);
    }

    [Fact]
    public async Task MergeFromAsync_WithVisitHistory_ShouldPreserveHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Record some visits
        await grain.RecordVisitAsync(new RecordVisitCommand(siteId, Guid.NewGuid(), 100m, SiteName: "Site 1"));
        await grain.RecordVisitAsync(new RecordVisitCommand(siteId, Guid.NewGuid(), 200m, SiteName: "Site 2"));

        // Act
        await grain.MergeFromAsync(Guid.NewGuid());

        // Assert
        var history = await grain.GetVisitHistoryAsync();
        history.Should().HaveCount(2);
    }

    // ==================== BIRTHDAY REWARD HISTORY AND REDEMPTION TESTS ====================

    [Fact]
    public async Task BirthdayReward_Redemption_ShouldUpdateHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.SetBirthdayAsync(new SetBirthdayCommand(new DateOnly(1990, 6, 15)));

        var reward = await grain.IssueBirthdayRewardAsync(new IssueBirthdayRewardCommand("Birthday Treat", 30));

        // Act - Redeem the birthday reward
        await grain.RedeemRewardAsync(new RedeemRewardCommand(reward.RewardId, Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        var state = await grain.GetStateAsync();
        state.CurrentBirthdayReward.Should().NotBeNull();
        state.CurrentBirthdayReward!.RedeemedAt.Should().NotBeNull();

        // The birthday reward history should be updated
        state.BirthdayRewardHistory.Should().HaveCount(1);
        state.BirthdayRewardHistory[0].RedeemedAt.Should().NotBeNull();
        state.BirthdayRewardHistory[0].Year.Should().Be(DateTime.UtcNow.Year);
    }

    [Fact]
    public async Task BirthdayReward_CustomValidDays_ShouldSetCorrectExpiry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.SetBirthdayAsync(new SetBirthdayCommand(new DateOnly(1990, 6, 15)));

        // Act - Issue with 60 day validity
        var result = await grain.IssueBirthdayRewardAsync(new IssueBirthdayRewardCommand("Birthday Treat", 60));

        // Assert
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddDays(59));
        result.ExpiresAt.Should().BeBefore(DateTime.UtcNow.AddDays(61));
    }

    [Fact]
    public async Task BirthdayReward_DefaultValidDays_ShouldBe30Days()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.SetBirthdayAsync(new SetBirthdayCommand(new DateOnly(1990, 6, 15)));

        // Act - Issue with default validity (null = 30 days)
        var result = await grain.IssueBirthdayRewardAsync(new IssueBirthdayRewardCommand("Birthday Treat", null));

        // Assert
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddDays(29));
        result.ExpiresAt.Should().BeBefore(DateTime.UtcNow.AddDays(31));
    }

    [Fact]
    public async Task BirthdayReward_ShouldAppearInAvailableRewards()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.SetBirthdayAsync(new SetBirthdayCommand(new DateOnly(1990, 6, 15)));

        // Act
        var result = await grain.IssueBirthdayRewardAsync(new IssueBirthdayRewardCommand("Free Birthday Cake", 30));

        // Assert
        var rewards = await grain.GetAvailableRewardsAsync();
        rewards.Should().HaveCount(1);
        rewards[0].Id.Should().Be(result.RewardId);
        rewards[0].Name.Should().Be("Free Birthday Cake");
    }

    [Fact]
    public async Task BirthdayReward_AfterRedemption_ShouldNotAppearInAvailableRewards()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.SetBirthdayAsync(new SetBirthdayCommand(new DateOnly(1990, 6, 15)));
        var reward = await grain.IssueBirthdayRewardAsync(new IssueBirthdayRewardCommand("Birthday Treat", 30));

        // Act
        await grain.RedeemRewardAsync(new RedeemRewardCommand(reward.RewardId, Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        var rewards = await grain.GetAvailableRewardsAsync();
        rewards.Should().BeEmpty();
    }

    [Fact]
    public async Task SetBirthdayAsync_ShouldOverwriteExistingBirthday()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.SetBirthdayAsync(new SetBirthdayCommand(new DateOnly(1990, 6, 15)));

        // Act
        await grain.SetBirthdayAsync(new SetBirthdayCommand(new DateOnly(1985, 12, 25)));

        // Assert
        var state = await grain.GetStateAsync();
        state.DateOfBirth.Should().Be(new DateOnly(1985, 12, 25));
    }

    // ==================== SPEND PROJECTION HISTORICAL TRACKING TESTS ====================

    [Fact]
    public async Task RecordSpendAsync_WithDiscount_ShouldTrackCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));
        await grain.InitializeAsync(orgId, customerId);

        // Act - Record spend with discount
        var result = await grain.RecordSpendAsync(new RecordSpendCommand(
            OrderId: Guid.NewGuid(),
            SiteId: siteId,
            NetSpend: 80m, // After discount
            GrossSpend: 100m, // Before discount
            DiscountAmount: 20m,
            TaxAmount: 8m,
            ItemCount: 5,
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

        // Assert - Points should be based on net spend
        result.PointsEarned.Should().Be(80); // 80 * 1.0 * 1.0 = 80

        var state = await grain.GetStateAsync();
        state.LifetimeSpend.Should().Be(80m);
    }

    [Fact]
    public async Task RecordSpendAsync_MultipleTransactionsInDay_ShouldAccumulateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));
        await grain.InitializeAsync(orgId, customerId);

        // Act - Record multiple transactions
        await grain.RecordSpendAsync(new RecordSpendCommand(Guid.NewGuid(), siteId, 50m, 54m, 0m, 4m, 2, DateOnly.FromDateTime(DateTime.UtcNow)));
        await grain.RecordSpendAsync(new RecordSpendCommand(Guid.NewGuid(), siteId, 75m, 81m, 0m, 6m, 3, DateOnly.FromDateTime(DateTime.UtcNow)));
        await grain.RecordSpendAsync(new RecordSpendCommand(Guid.NewGuid(), siteId, 25m, 27m, 0m, 2m, 1, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Assert
        var state = await grain.GetStateAsync();
        state.LifetimeSpend.Should().Be(150m);
        state.LifetimeTransactions.Should().Be(3);
        state.YearToDateSpend.Should().Be(150m);
        state.MonthToDateSpend.Should().Be(150m);
    }

    [Fact]
    public async Task GetSnapshotAsync_ShouldProvideAccurateSpendToNextTier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));
        await grain.InitializeAsync(orgId, customerId);

        // Record $300 spend (Bronze tier, Silver at $500)
        await grain.RecordSpendAsync(new RecordSpendCommand(Guid.NewGuid(), siteId, 300m, 324m, 0m, 24m, 10, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.CurrentTier.Should().Be("Bronze");
        snapshot.NextTier.Should().Be("Silver");
        snapshot.SpendToNextTier.Should().Be(200m); // 500 - 300 = 200
    }

    [Fact]
    public async Task GetSnapshotAsync_AtMaxTier_ShouldHaveNoNextTier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));
        await grain.InitializeAsync(orgId, customerId);

        // Record $6000 spend (should be Platinum - the max tier at $5000+)
        await grain.RecordSpendAsync(new RecordSpendCommand(Guid.NewGuid(), siteId, 6000m, 6480m, 0m, 480m, 100, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.CurrentTier.Should().Be("Platinum");
        snapshot.NextTier.Should().BeNull();
        snapshot.SpendToNextTier.Should().Be(0);
    }

    [Fact]
    public async Task ReverseSpendAsync_PartialRefund_ShouldAdjustCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));
        await grain.InitializeAsync(orgId, customerId);

        await grain.RecordSpendAsync(new RecordSpendCommand(orderId, siteId, 200m, 216m, 0m, 16m, 5, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Act - Partial refund
        await grain.ReverseSpendAsync(new ReverseSpendCommand(orderId, 50m, "Partial refund"));

        // Assert
        var state = await grain.GetStateAsync();
        state.LifetimeSpend.Should().Be(150m);
        state.AvailablePoints.Should().Be(0); // Points clawed back
    }

    [Fact]
    public async Task RedemptionHistory_ShouldLimitTo50()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));
        await grain.InitializeAsync(orgId, customerId);

        // Earn lots of points
        await grain.RecordSpendAsync(new RecordSpendCommand(Guid.NewGuid(), siteId, 10000m, 10800m, 0m, 800m, 100, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Act - Make 60 redemptions
        for (int i = 0; i < 60; i++)
        {
            await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(Guid.NewGuid(), 10, "Discount"));
        }

        // Assert
        var state = await grain.GetStateAsync();
        state.RecentRedemptions.Should().HaveCount(50);
        state.TotalPointsRedeemed.Should().Be(600); // All 60 redemptions counted
    }

    // ==================== POINTS BALANCE TRACKING TESTS ====================

    [Fact]
    public async Task EarnPointsAsync_WithSpendAmount_ShouldUpdateTotalSpend()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateLoyaltyCustomerAsync(orgId, customerId);

        // Act - Earn points with spend tracking
        await grain.EarnPointsAsync(new EarnPointsCommand(100, "Purchase", null, null, 50m));
        await grain.EarnPointsAsync(new EarnPointsCommand(200, "Purchase", null, null, 100m));

        // Assert
        var state = await grain.GetStateAsync();
        state.Stats.TotalSpend.Should().Be(150m);
        state.Loyalty!.PointsBalance.Should().Be(300);
        state.Loyalty.LifetimePoints.Should().Be(300);
    }

    [Fact]
    public async Task PointsTracking_YtdPoints_ShouldAccumulate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateLoyaltyCustomerAsync(orgId, customerId);

        // Act
        await grain.EarnPointsAsync(new EarnPointsCommand(100, "Purchase 1"));
        await grain.EarnPointsAsync(new EarnPointsCommand(200, "Purchase 2"));
        await grain.EarnPointsAsync(new EarnPointsCommand(150, "Purchase 3"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Loyalty!.YtdPoints.Should().Be(450);
    }

    // ==================== REWARD EXPIRATION BATCH TESTS ====================

    [Fact]
    public async Task ExpireRewardsAsync_MixedExpiry_ShouldOnlyExpireExpiredOnes()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateLoyaltyCustomerAsync(orgId, customerId);
        await grain.EarnPointsAsync(new EarnPointsCommand(500, "Purchase"));

        // Issue rewards with different expiry dates
        await grain.IssueRewardAsync(new IssueRewardCommand(Guid.NewGuid(), "Expired 1", 0, DateTime.UtcNow.AddDays(-5)));
        await grain.IssueRewardAsync(new IssueRewardCommand(Guid.NewGuid(), "Valid 1", 0, DateTime.UtcNow.AddDays(30)));
        await grain.IssueRewardAsync(new IssueRewardCommand(Guid.NewGuid(), "Expired 2", 0, DateTime.UtcNow.AddDays(-1)));
        await grain.IssueRewardAsync(new IssueRewardCommand(Guid.NewGuid(), "Valid 2", 0, DateTime.UtcNow.AddDays(60)));
        await grain.IssueRewardAsync(new IssueRewardCommand(Guid.NewGuid(), "Expired 3", 0, DateTime.UtcNow.AddDays(-10)));

        // Act
        await grain.ExpireRewardsAsync();

        // Assert
        var rewards = await grain.GetAvailableRewardsAsync();
        rewards.Should().HaveCount(2);
        rewards.Should().Contain(r => r.Name == "Valid 1");
        rewards.Should().Contain(r => r.Name == "Valid 2");
    }

    [Fact]
    public async Task ExpireRewardsAsync_NoExpiredRewards_ShouldNotChange()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateLoyaltyCustomerAsync(orgId, customerId);
        await grain.EarnPointsAsync(new EarnPointsCommand(500, "Purchase"));

        await grain.IssueRewardAsync(new IssueRewardCommand(Guid.NewGuid(), "Valid 1", 0, DateTime.UtcNow.AddDays(30)));
        await grain.IssueRewardAsync(new IssueRewardCommand(Guid.NewGuid(), "Valid 2", 0, DateTime.UtcNow.AddDays(60)));

        // Act
        await grain.ExpireRewardsAsync();

        // Assert
        var rewards = await grain.GetAvailableRewardsAsync();
        rewards.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExpireRewardsAsync_AllExpired_ShouldExpireAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateLoyaltyCustomerAsync(orgId, customerId);
        await grain.EarnPointsAsync(new EarnPointsCommand(500, "Purchase"));

        await grain.IssueRewardAsync(new IssueRewardCommand(Guid.NewGuid(), "Expired 1", 0, DateTime.UtcNow.AddDays(-1)));
        await grain.IssueRewardAsync(new IssueRewardCommand(Guid.NewGuid(), "Expired 2", 0, DateTime.UtcNow.AddDays(-2)));
        await grain.IssueRewardAsync(new IssueRewardCommand(Guid.NewGuid(), "Expired 3", 0, DateTime.UtcNow.AddDays(-3)));

        // Act
        await grain.ExpireRewardsAsync();

        // Assert
        var rewards = await grain.GetAvailableRewardsAsync();
        rewards.Should().BeEmpty();
    }
}
