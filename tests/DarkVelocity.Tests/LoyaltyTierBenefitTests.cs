using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for LoyaltyProgramGrain tier benefits configuration and application.
/// Verifies that tier benefits are properly configured and filtered by tier level.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class LoyaltyTierBenefitTests
{
    private readonly TestClusterFixture _fixture;

    public LoyaltyTierBenefitTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ILoyaltyProgramGrain> CreateProgramAsync(Guid orgId, Guid programId, string name = "Test Program")
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ILoyaltyProgramGrain>(GrainKeys.LoyaltyProgram(orgId, programId));
        await grain.CreateAsync(new CreateLoyaltyProgramCommand(orgId, name, "Test program description"));
        return grain;
    }

    // ==================== TIER BENEFIT CONFIGURATION TESTS ====================

    [Fact]
    public async Task AddTierAsync_WithMultipleBenefits_ShouldStoreAllBenefits()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        var benefits = new List<TierBenefit>
        {
            new() { Name = "Points Multiplier", Description = "2x points on all purchases", Type = BenefitType.PointsMultiplier, Value = 2.0m },
            new() { Name = "Free Delivery", Description = "Free delivery on all orders", Type = BenefitType.FreeDelivery },
            new() { Name = "Priority Booking", Description = "Priority reservations", Type = BenefitType.PriorityBooking },
            new() { Name = "Birthday Reward", Description = "Special birthday treat", Type = BenefitType.BirthdayReward },
            new() { Name = "Exclusive Access", Description = "Access to VIP events", Type = BenefitType.ExclusiveAccess }
        };

        // Act
        var result = await grain.AddTierAsync(new AddTierCommand(
            "VIP",
            5,
            5000,
            benefits,
            EarningMultiplier: 2.5m,
            MaintenancePoints: 1000,
            GracePeriodDays: 30,
            Color: "#FFD700"));

        // Assert
        var tier = await grain.GetTierByLevelAsync(5);
        tier.Should().NotBeNull();
        tier!.Benefits.Should().HaveCount(5);
        tier.Benefits.Should().Contain(b => b.Type == BenefitType.PointsMultiplier);
        tier.Benefits.Should().Contain(b => b.Type == BenefitType.FreeDelivery);
        tier.Benefits.Should().Contain(b => b.Type == BenefitType.PriorityBooking);
        tier.Benefits.Should().Contain(b => b.Type == BenefitType.BirthdayReward);
        tier.Benefits.Should().Contain(b => b.Type == BenefitType.ExclusiveAccess);
    }

    [Fact]
    public async Task AddTierAsync_WithNullBenefits_ShouldHaveEmptyBenefitsList()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act
        await grain.AddTierAsync(new AddTierCommand("Basic", 1, 0, Benefits: null));

        // Assert
        var tier = await grain.GetTierByLevelAsync(1);
        tier.Should().NotBeNull();
        tier!.Benefits.Should().NotBeNull();
        tier.Benefits.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateTierAsync_AddingBenefits_ShouldReplaceBenefits()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        var initialBenefits = new List<TierBenefit>
        {
            new() { Name = "Original Benefit", Type = BenefitType.PointsMultiplier }
        };

        var tier = await grain.AddTierAsync(new AddTierCommand("Silver", 2, 500, initialBenefits));

        // Act - Update with new benefits
        var newBenefits = new List<TierBenefit>
        {
            new() { Name = "New Benefit 1", Type = BenefitType.FreeDelivery },
            new() { Name = "New Benefit 2", Type = BenefitType.PriorityBooking },
            new() { Name = "New Benefit 3", Type = BenefitType.PercentDiscount, Value = 10m }
        };

        await grain.UpdateTierAsync(tier.TierId, pointsRequired: null, benefits: newBenefits);

        // Assert
        var updatedTier = await grain.GetTierByLevelAsync(2);
        updatedTier!.Benefits.Should().HaveCount(3);
        updatedTier.Benefits.Should().NotContain(b => b.Name == "Original Benefit");
        updatedTier.Benefits.Should().Contain(b => b.Name == "New Benefit 1");
    }

    [Fact]
    public async Task UpdateTierAsync_ClearingBenefits_ShouldSetEmptyList()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        var initialBenefits = new List<TierBenefit>
        {
            new() { Name = "Benefit 1", Type = BenefitType.FreeDelivery },
            new() { Name = "Benefit 2", Type = BenefitType.PointsMultiplier }
        };

        var tier = await grain.AddTierAsync(new AddTierCommand("Gold", 3, 1000, initialBenefits));

        // Act - Clear benefits
        await grain.UpdateTierAsync(tier.TierId, pointsRequired: null, benefits: new List<TierBenefit>());

        // Assert
        var updatedTier = await grain.GetTierByLevelAsync(3);
        updatedTier!.Benefits.Should().BeEmpty();
    }

    // ==================== TIER-BASED REWARD FILTERING TESTS ====================

    [Fact]
    public async Task GetAvailableRewardsAsync_ShouldFilterByMinimumTierLevel()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Add rewards with different tier requirements
        await grain.AddRewardAsync(new AddRewardCommand("Basic Reward", "For all tiers", RewardType.PercentDiscount, 100, DiscountValue: 5, MinimumTierLevel: null));
        await grain.AddRewardAsync(new AddRewardCommand("Bronze+ Reward", "Bronze and above", RewardType.PercentDiscount, 150, DiscountValue: 10, MinimumTierLevel: 1));
        await grain.AddRewardAsync(new AddRewardCommand("Silver+ Reward", "Silver and above", RewardType.PercentDiscount, 200, DiscountValue: 15, MinimumTierLevel: 2));
        await grain.AddRewardAsync(new AddRewardCommand("Gold+ Reward", "Gold and above", RewardType.PercentDiscount, 300, DiscountValue: 20, MinimumTierLevel: 3));
        await grain.AddRewardAsync(new AddRewardCommand("Platinum Only", "Platinum exclusive", RewardType.FreeItem, 500, MinimumTierLevel: 4));

        // Act & Assert - Bronze (level 1)
        var bronzeRewards = await grain.GetAvailableRewardsAsync(1);
        bronzeRewards.Should().HaveCount(2); // Basic + Bronze+
        bronzeRewards.Should().Contain(r => r.Name == "Basic Reward");
        bronzeRewards.Should().Contain(r => r.Name == "Bronze+ Reward");

        // Act & Assert - Silver (level 2)
        var silverRewards = await grain.GetAvailableRewardsAsync(2);
        silverRewards.Should().HaveCount(3); // Basic + Bronze+ + Silver+

        // Act & Assert - Gold (level 3)
        var goldRewards = await grain.GetAvailableRewardsAsync(3);
        goldRewards.Should().HaveCount(4); // All except Platinum

        // Act & Assert - Platinum (level 4)
        var platinumRewards = await grain.GetAvailableRewardsAsync(4);
        platinumRewards.Should().HaveCount(5); // All rewards
    }

    [Fact]
    public async Task GetAvailableRewardsAsync_WithNoMinimumTier_ShouldBeAvailableToAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        await grain.AddRewardAsync(new AddRewardCommand("Universal Reward", "For everyone", RewardType.PercentDiscount, 50, DiscountValue: 5, MinimumTierLevel: null));

        // Act
        var level0Rewards = await grain.GetAvailableRewardsAsync(0);
        var level1Rewards = await grain.GetAvailableRewardsAsync(1);
        var level10Rewards = await grain.GetAvailableRewardsAsync(10);

        // Assert - Should be available at all levels
        level0Rewards.Should().HaveCount(1);
        level1Rewards.Should().HaveCount(1);
        level10Rewards.Should().HaveCount(1);
    }

    // ==================== TIER EARNING MULTIPLIER TESTS ====================

    [Fact]
    public async Task CalculatePointsAsync_WithDifferentTierMultipliers_ShouldApplyCorrectMultiplier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        await grain.AddEarningRuleAsync(new AddEarningRuleCommand("Base", EarningType.PerDollar, PointsPerDollar: 1));
        await grain.AddTierAsync(new AddTierCommand("Bronze", 1, 0, EarningMultiplier: 1.0m));
        await grain.AddTierAsync(new AddTierCommand("Silver", 2, 500, EarningMultiplier: 1.5m));
        await grain.AddTierAsync(new AddTierCommand("Gold", 3, 1500, EarningMultiplier: 2.0m));
        await grain.AddTierAsync(new AddTierCommand("Platinum", 4, 5000, EarningMultiplier: 3.0m));
        await grain.ActivateAsync();

        // Act & Assert
        var bronzeResult = await grain.CalculatePointsAsync(100m, 1, Guid.NewGuid(), DateTime.UtcNow);
        bronzeResult.TotalPoints.Should().Be(100); // 100 * 1.0

        var silverResult = await grain.CalculatePointsAsync(100m, 2, Guid.NewGuid(), DateTime.UtcNow);
        silverResult.TotalPoints.Should().Be(150); // 100 * 1.5

        var goldResult = await grain.CalculatePointsAsync(100m, 3, Guid.NewGuid(), DateTime.UtcNow);
        goldResult.TotalPoints.Should().Be(200); // 100 * 2.0

        var platinumResult = await grain.CalculatePointsAsync(100m, 4, Guid.NewGuid(), DateTime.UtcNow);
        platinumResult.TotalPoints.Should().Be(300); // 100 * 3.0
    }

    [Fact]
    public async Task CalculatePointsAsync_WithNonExistentTier_ShouldUseDefaultMultiplier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        await grain.AddEarningRuleAsync(new AddEarningRuleCommand("Base", EarningType.PerDollar, PointsPerDollar: 10));
        await grain.AddTierAsync(new AddTierCommand("Bronze", 1, 0, EarningMultiplier: 1.0m));
        await grain.ActivateAsync();

        // Act - Use a tier level that doesn't exist
        var result = await grain.CalculatePointsAsync(100m, 99, Guid.NewGuid(), DateTime.UtcNow);

        // Assert - Should use default multiplier of 1
        result.Multiplier.Should().Be(1m);
        result.TotalPoints.Should().Be(1000); // 100 * 10 * 1
    }

    // ==================== TIER MAINTENANCE POINTS TESTS ====================

    [Fact]
    public async Task AddTierAsync_WithMaintenancePoints_ShouldStore()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act
        await grain.AddTierAsync(new AddTierCommand(
            "Gold",
            3,
            PointsRequired: 1000,
            MaintenancePoints: 500,
            GracePeriodDays: 30));

        // Assert
        var tier = await grain.GetTierByLevelAsync(3);
        tier.Should().NotBeNull();
        tier!.MaintenancePoints.Should().Be(500);
        tier.GracePeriodDays.Should().Be(30);
    }

    [Fact]
    public async Task AddTierAsync_WithoutMaintenancePoints_ShouldBeNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act
        await grain.AddTierAsync(new AddTierCommand("Bronze", 1, 0));

        // Assert
        var tier = await grain.GetTierByLevelAsync(1);
        tier.Should().NotBeNull();
        tier!.MaintenancePoints.Should().BeNull();
        tier.GracePeriodDays.Should().BeNull();
    }

    // ==================== TIER ORDERING TESTS ====================

    [Fact]
    public async Task GetTiersAsync_ShouldReturnOrderedByLevel()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Add tiers in non-sequential order
        await grain.AddTierAsync(new AddTierCommand("Gold", 3, 1500));
        await grain.AddTierAsync(new AddTierCommand("Bronze", 1, 0));
        await grain.AddTierAsync(new AddTierCommand("Platinum", 4, 5000));
        await grain.AddTierAsync(new AddTierCommand("Silver", 2, 500));

        // Act
        var tiers = await grain.GetTiersAsync();

        // Assert - Should be ordered by level
        tiers.Should().HaveCount(4);
        tiers[0].Level.Should().Be(1);
        tiers[0].Name.Should().Be("Bronze");
        tiers[1].Level.Should().Be(2);
        tiers[1].Name.Should().Be("Silver");
        tiers[2].Level.Should().Be(3);
        tiers[2].Name.Should().Be("Gold");
        tiers[3].Level.Should().Be(4);
        tiers[3].Name.Should().Be("Platinum");
    }

    // ==================== POINTS EXPIRY CONFIGURATION TESTS ====================

    [Fact]
    public async Task ConfigurePointsExpiryAsync_ShouldSetAllFields()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act
        await grain.ConfigurePointsExpiryAsync(new ConfigurePointsExpiryCommand(
            Enabled: true,
            ExpiryMonths: 24,
            WarningDays: 60));

        // Assert
        var state = await grain.GetStateAsync();
        state.PointsExpiry.Should().NotBeNull();
        state.PointsExpiry!.Enabled.Should().BeTrue();
        state.PointsExpiry.ExpiryMonths.Should().Be(24);
        state.PointsExpiry.WarningDays.Should().Be(60);
    }

    [Fact]
    public async Task ConfigurePointsExpiryAsync_Disabled_ShouldSetEnabled()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // First enable
        await grain.ConfigurePointsExpiryAsync(new ConfigurePointsExpiryCommand(true, 12, 30));

        // Act - Disable
        await grain.ConfigurePointsExpiryAsync(new ConfigurePointsExpiryCommand(false, 12, 30));

        // Assert
        var state = await grain.GetStateAsync();
        state.PointsExpiry!.Enabled.Should().BeFalse();
    }

    // ==================== REWARD LIMIT TESTS ====================

    [Fact]
    public async Task AddRewardAsync_WithLimits_ShouldStoreAllLimitFields()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act
        var result = await grain.AddRewardAsync(new AddRewardCommand(
            "Limited Reward",
            "Can only redeem 3 times per month",
            RewardType.PercentDiscount,
            100,
            DiscountValue: 10,
            LimitPerCustomer: 3,
            LimitPeriod: LimitPeriod.Month,
            ValidDays: 14));

        // Assert
        var state = await grain.GetStateAsync();
        var reward = state.Rewards.First(r => r.Id == result.RewardId);
        reward.LimitPerCustomer.Should().Be(3);
        reward.LimitPeriod.Should().Be(LimitPeriod.Month);
        reward.ValidDays.Should().Be(14);
    }

    [Fact]
    public async Task AddRewardAsync_LifetimeLimit_ShouldStore()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act
        var result = await grain.AddRewardAsync(new AddRewardCommand(
            "One Time Reward",
            "Can only redeem once ever",
            RewardType.FreeItem,
            500,
            LimitPerCustomer: 1,
            LimitPeriod: LimitPeriod.Lifetime));

        // Assert
        var state = await grain.GetStateAsync();
        var reward = state.Rewards.First(r => r.Id == result.RewardId);
        reward.LimitPerCustomer.Should().Be(1);
        reward.LimitPeriod.Should().Be(LimitPeriod.Lifetime);
    }
}
