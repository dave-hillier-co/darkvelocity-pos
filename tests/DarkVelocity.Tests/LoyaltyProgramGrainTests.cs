using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class LoyaltyProgramGrainTests
{
    private readonly TestClusterFixture _fixture;

    public LoyaltyProgramGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ILoyaltyProgramGrain> CreateProgramAsync(Guid orgId, Guid programId, string name = "Rewards Program")
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ILoyaltyProgramGrain>(GrainKeys.LoyaltyProgram(orgId, programId));
        await grain.CreateAsync(new CreateLoyaltyProgramCommand(orgId, name, "Earn points with every purchase"));
        return grain;
    }

    private async Task<ILoyaltyProgramGrain> CreateFullProgramAsync(Guid orgId, Guid programId)
    {
        var grain = await CreateProgramAsync(orgId, programId);

        // Add earning rule
        await grain.AddEarningRuleAsync(new AddEarningRuleCommand(
            "Base Earning",
            EarningType.PerDollar,
            PointsPerDollar: 10));

        // Add tiers
        await grain.AddTierAsync(new AddTierCommand("Bronze", 1, 0, null, 1m));
        await grain.AddTierAsync(new AddTierCommand("Silver", 2, 500, null, 1.5m));
        await grain.AddTierAsync(new AddTierCommand("Gold", 3, 1000, null, 2m));

        return grain;
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateProgram()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ILoyaltyProgramGrain>(GrainKeys.LoyaltyProgram(orgId, programId));

        // Act
        var result = await grain.CreateAsync(new CreateLoyaltyProgramCommand(
            orgId,
            "VIP Rewards",
            "Exclusive rewards for our best customers"));

        // Assert
        result.Id.Should().Be(programId);
        result.Name.Should().Be("VIP Rewards");

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(ProgramStatus.Draft);
        state.Description.Should().Be("Exclusive rewards for our best customers");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateProgram()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act
        await grain.UpdateAsync("Updated Name", "Updated description");

        // Assert
        var state = await grain.GetStateAsync();
        state.Name.Should().Be("Updated Name");
        state.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task AddEarningRuleAsync_ShouldAddRule()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act
        var result = await grain.AddEarningRuleAsync(new AddEarningRuleCommand(
            "Points per Dollar",
            EarningType.PerDollar,
            PointsPerDollar: 10,
            MinimumSpend: 5m));

        // Assert
        result.RuleId.Should().NotBeEmpty();

        var rules = await grain.GetEarningRulesAsync();
        rules.Should().HaveCount(1);
        rules[0].Name.Should().Be("Points per Dollar");
        rules[0].PointsPerDollar.Should().Be(10);
        rules[0].MinimumSpend.Should().Be(5m);
    }

    [Fact]
    public async Task AddEarningRuleAsync_BonusDay_ShouldAddBonusRule()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act
        var result = await grain.AddEarningRuleAsync(new AddEarningRuleCommand(
            "Double Points Tuesday",
            EarningType.BonusDay,
            BonusMultiplier: 2m,
            ApplicableDays: [DayOfWeek.Tuesday]));

        // Assert
        var state = await grain.GetStateAsync();
        var rule = state.EarningRules.First(r => r.Id == result.RuleId);
        rule.Type.Should().Be(EarningType.BonusDay);
        rule.BonusMultiplier.Should().Be(2m);
        rule.ApplicableDays.Should().Contain(DayOfWeek.Tuesday);
    }

    [Fact]
    public async Task UpdateEarningRuleAsync_ShouldDeactivateRule()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);
        var result = await grain.AddEarningRuleAsync(new AddEarningRuleCommand("Test", EarningType.PerDollar, PointsPerDollar: 5));

        // Act
        await grain.UpdateEarningRuleAsync(result.RuleId, isActive: false);

        // Assert
        var rules = await grain.GetEarningRulesAsync();
        rules.Should().BeEmpty(); // Active rules only
    }

    [Fact]
    public async Task AddTierAsync_ShouldAddTier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        var benefits = new List<TierBenefit>
        {
            new() { Name = "Free Shipping", Description = "Free shipping on orders over $50", Type = BenefitType.FreeDelivery },
            new() { Name = "Birthday Reward", Description = "Special birthday discount", Type = BenefitType.BirthdayReward }
        };

        // Act
        var result = await grain.AddTierAsync(new AddTierCommand(
            "Gold",
            3,
            1000,
            benefits,
            EarningMultiplier: 2m,
            MaintenancePoints: 500,
            GracePeriodDays: 30,
            Color: "#FFD700"));

        // Assert
        result.TierId.Should().NotBeEmpty();

        var tiers = await grain.GetTiersAsync();
        tiers.Should().HaveCount(1);
        tiers[0].Name.Should().Be("Gold");
        tiers[0].PointsRequired.Should().Be(1000);
        tiers[0].EarningMultiplier.Should().Be(2m);
        tiers[0].Benefits.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddTierAsync_DuplicateLevel_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);
        await grain.AddTierAsync(new AddTierCommand("Bronze", 1, 0));

        // Act
        var act = () => grain.AddTierAsync(new AddTierCommand("Another Bronze", 1, 100));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*level 1 already exists*");
    }

    [Fact]
    public async Task GetNextTierAsync_ShouldReturnNextTier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateFullProgramAsync(orgId, programId);

        // Act
        var nextTier = await grain.GetNextTierAsync(1);

        // Assert
        nextTier.Should().NotBeNull();
        nextTier!.Name.Should().Be("Silver");
        nextTier.Level.Should().Be(2);
    }

    [Fact]
    public async Task GetNextTierAsync_AtMaxTier_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateFullProgramAsync(orgId, programId);

        // Act
        var nextTier = await grain.GetNextTierAsync(3);

        // Assert
        nextTier.Should().BeNull();
    }

    [Fact]
    public async Task AddRewardAsync_ShouldAddReward()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act
        var result = await grain.AddRewardAsync(new AddRewardCommand(
            "Free Coffee",
            "Get a free coffee on us!",
            RewardType.FreeItem,
            100,
            FreeItemId: Guid.NewGuid(),
            MinimumTierLevel: 2,
            LimitPerCustomer: 5,
            LimitPeriod: LimitPeriod.Month,
            ValidDays: 30));

        // Assert
        result.RewardId.Should().NotBeEmpty();

        var state = await grain.GetStateAsync();
        state.Rewards.Should().HaveCount(1);
        state.Rewards[0].Name.Should().Be("Free Coffee");
        state.Rewards[0].PointsCost.Should().Be(100);
        state.Rewards[0].Type.Should().Be(RewardType.FreeItem);
    }

    [Fact]
    public async Task GetAvailableRewardsAsync_ShouldFilterByTier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        await grain.AddRewardAsync(new AddRewardCommand("Basic Reward", "For all", RewardType.PercentDiscount, 50, DiscountValue: 10));
        await grain.AddRewardAsync(new AddRewardCommand("Silver Reward", "Silver+ only", RewardType.PercentDiscount, 100, DiscountValue: 15, MinimumTierLevel: 2));
        await grain.AddRewardAsync(new AddRewardCommand("Gold Reward", "Gold only", RewardType.PercentDiscount, 150, DiscountValue: 20, MinimumTierLevel: 3));

        // Act
        var bronzeRewards = await grain.GetAvailableRewardsAsync(1);
        var silverRewards = await grain.GetAvailableRewardsAsync(2);
        var goldRewards = await grain.GetAvailableRewardsAsync(3);

        // Assert
        bronzeRewards.Should().HaveCount(1);
        bronzeRewards[0].Name.Should().Be("Basic Reward");

        silverRewards.Should().HaveCount(2);
        goldRewards.Should().HaveCount(3);
    }

    [Fact]
    public async Task ActivateAsync_ShouldActivateProgram()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateFullProgramAsync(orgId, programId);

        // Act
        await grain.ActivateAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(ProgramStatus.Active);
        state.ActivatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ActivateAsync_WithoutRules_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);
        await grain.AddTierAsync(new AddTierCommand("Bronze", 1, 0));

        // Act
        var act = () => grain.ActivateAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*at least one earning rule*");
    }

    [Fact]
    public async Task ActivateAsync_WithoutTiers_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);
        await grain.AddEarningRuleAsync(new AddEarningRuleCommand("Base", EarningType.PerDollar, PointsPerDollar: 10));

        // Act
        var act = () => grain.ActivateAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*at least one tier*");
    }

    [Fact]
    public async Task PauseAsync_ShouldPauseProgram()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateFullProgramAsync(orgId, programId);
        await grain.ActivateAsync();

        // Act
        await grain.PauseAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(ProgramStatus.Paused);
    }

    [Fact]
    public async Task ConfigurePointsExpiryAsync_ShouldSetExpiry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act
        await grain.ConfigurePointsExpiryAsync(new ConfigurePointsExpiryCommand(true, 18, 45));

        // Assert
        var state = await grain.GetStateAsync();
        state.PointsExpiry.Should().NotBeNull();
        state.PointsExpiry!.Enabled.Should().BeTrue();
        state.PointsExpiry.ExpiryMonths.Should().Be(18);
        state.PointsExpiry.WarningDays.Should().Be(45);
    }

    [Fact]
    public async Task ConfigureReferralProgramAsync_ShouldSetReferral()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act
        await grain.ConfigureReferralProgramAsync(new ConfigureReferralCommand(true, 100, 50, 25m));

        // Assert
        var state = await grain.GetStateAsync();
        state.ReferralProgram.Should().NotBeNull();
        state.ReferralProgram!.Enabled.Should().BeTrue();
        state.ReferralProgram.ReferrerPoints.Should().Be(100);
        state.ReferralProgram.RefereePoints.Should().Be(50);
        state.ReferralProgram.MinimumQualifyingSpend.Should().Be(25m);
    }

    [Fact]
    public async Task CalculatePointsAsync_ShouldCalculateBasePoints()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateFullProgramAsync(orgId, programId);
        await grain.ActivateAsync();

        // Act
        var result = await grain.CalculatePointsAsync(50m, 1, Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        result.BasePoints.Should().Be(500); // 50 * 10
        result.Multiplier.Should().Be(1m); // Bronze tier
        result.TotalPoints.Should().Be(500);
    }

    [Fact]
    public async Task CalculatePointsAsync_WithTierMultiplier_ShouldApplyMultiplier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateFullProgramAsync(orgId, programId);
        await grain.ActivateAsync();

        // Act
        var result = await grain.CalculatePointsAsync(50m, 3, Guid.NewGuid(), DateTime.UtcNow); // Gold tier (2x)

        // Assert
        result.BasePoints.Should().Be(500);
        result.Multiplier.Should().Be(2m);
        result.TotalPoints.Should().Be(1000);
    }

    [Fact]
    public async Task CalculatePointsAsync_WithBonusDay_ShouldApplyBonus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateFullProgramAsync(orgId, programId);

        // Find a Tuesday
        var tuesday = DateTime.UtcNow;
        while (tuesday.DayOfWeek != DayOfWeek.Tuesday)
            tuesday = tuesday.AddDays(1);

        await grain.AddEarningRuleAsync(new AddEarningRuleCommand(
            "Double Points Tuesday",
            EarningType.BonusDay,
            BonusMultiplier: 2m,
            ApplicableDays: [DayOfWeek.Tuesday]));

        await grain.ActivateAsync();

        // Act
        var result = await grain.CalculatePointsAsync(50m, 2, Guid.NewGuid(), tuesday); // Silver tier (1.5x) + bonus day (2x)

        // Assert
        result.BasePoints.Should().Be(500);
        result.Multiplier.Should().Be(3m); // 1.5 * 2
        result.TotalPoints.Should().Be(1500);
    }

    [Fact]
    public async Task IncrementEnrollmentsAsync_ShouldIncrementCounters()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act
        await grain.IncrementEnrollmentsAsync();
        await grain.IncrementEnrollmentsAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.TotalEnrollments.Should().Be(2);
        state.ActiveMembers.Should().Be(2);
    }

    [Fact]
    public async Task RecordPointsIssuedAsync_ShouldTrackPoints()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act
        await grain.RecordPointsIssuedAsync(500);
        await grain.RecordPointsIssuedAsync(300);

        // Assert
        var state = await grain.GetStateAsync();
        state.TotalPointsIssued.Should().Be(800);
    }

    [Fact]
    public async Task RecordPointsRedeemedAsync_ShouldTrackRedemptions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act
        await grain.RecordPointsRedeemedAsync(200);
        await grain.RecordPointsRedeemedAsync(100);

        // Assert
        var state = await grain.GetStateAsync();
        state.TotalPointsRedeemed.Should().Be(300);
    }

    [Fact]
    public async Task SetTermsAndConditionsAsync_ShouldSetTerms()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act
        await grain.SetTermsAndConditionsAsync("Points expire after 12 months of inactivity...");

        // Assert
        var state = await grain.GetStateAsync();
        state.TermsAndConditions.Should().StartWith("Points expire");
    }

    // ==================== Program Lifecycle Tests ====================

    [Fact]
    public async Task DeactivateAsync_ShouldArchiveProgram()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateFullProgramAsync(orgId, programId);
        await grain.ActivateAsync();

        // Act
        await grain.DeactivateAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(ProgramStatus.Archived);
    }

    [Fact]
    public async Task ActivateAsync_Archived_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateFullProgramAsync(orgId, programId);
        await grain.ActivateAsync();
        await grain.DeactivateAsync(); // Archive the program

        // Act
        var act = () => grain.ActivateAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*archived*");
    }

    [Fact]
    public async Task PauseAsync_NotActive_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId); // Status is Draft

        // Act
        var act = () => grain.PauseAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot pause*");
    }

    [Fact]
    public async Task IsActiveAsync_ShouldReturnCorrectStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateFullProgramAsync(orgId, programId);

        // Assert - Draft
        var isActiveDraft = await grain.IsActiveAsync();
        isActiveDraft.Should().BeFalse();

        // Act - Activate
        await grain.ActivateAsync();

        // Assert - Active
        var isActiveActive = await grain.IsActiveAsync();
        isActiveActive.Should().BeTrue();

        // Act - Pause
        await grain.PauseAsync();

        // Assert - Paused
        var isActivePaused = await grain.IsActiveAsync();
        isActivePaused.Should().BeFalse();
    }

    // ==================== Earning Rule Tests ====================

    [Fact]
    public async Task RemoveEarningRuleAsync_ShouldRemove()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        var rule1 = await grain.AddEarningRuleAsync(new AddEarningRuleCommand(
            "Rule 1", EarningType.PerDollar, PointsPerDollar: 5));
        var rule2 = await grain.AddEarningRuleAsync(new AddEarningRuleCommand(
            "Rule 2", EarningType.PerDollar, PointsPerDollar: 10));

        // Act
        await grain.RemoveEarningRuleAsync(rule1.RuleId);

        // Assert
        var rules = await grain.GetEarningRulesAsync();
        rules.Should().HaveCount(1);
        rules[0].Name.Should().Be("Rule 2");
    }

    // ==================== Points Calculation Tests ====================

    [Fact]
    public async Task CalculatePointsAsync_BelowMinimumSpend_ShouldReturn0()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Add earning rule with minimum spend
        await grain.AddEarningRuleAsync(new AddEarningRuleCommand(
            "Min Spend Rule",
            EarningType.PerDollar,
            PointsPerDollar: 10,
            MinimumSpend: 50m));

        await grain.AddTierAsync(new AddTierCommand("Bronze", 1, 0));
        await grain.ActivateAsync();

        // Act - Spend below minimum
        var result = await grain.CalculatePointsAsync(25m, 1, Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        result.BasePoints.Should().Be(0);
        result.TotalPoints.Should().Be(0);
    }

    [Fact]
    public async Task CalculatePointsAsync_NoActiveRules_ShouldReturn0()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Add rule then deactivate it
        var rule = await grain.AddEarningRuleAsync(new AddEarningRuleCommand(
            "Test Rule", EarningType.PerDollar, PointsPerDollar: 10));
        await grain.UpdateEarningRuleAsync(rule.RuleId, isActive: false);

        await grain.AddTierAsync(new AddTierCommand("Bronze", 1, 0));

        // Act - No active rules
        var result = await grain.CalculatePointsAsync(100m, 1, Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        result.BasePoints.Should().Be(0);
        result.TotalPoints.Should().Be(0);
        result.RuleId.Should().BeNull();
    }

    // ==================== Rewards Tests ====================

    [Fact]
    public async Task GetAvailableRewardsAsync_ShouldExcludeInactive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Add rewards
        var reward1 = await grain.AddRewardAsync(new AddRewardCommand(
            "Active Reward", "Active", RewardType.PercentDiscount, 100, DiscountValue: 10));
        var reward2 = await grain.AddRewardAsync(new AddRewardCommand(
            "To Be Deactivated", "Will deactivate", RewardType.PercentDiscount, 150, DiscountValue: 15));
        await grain.AddRewardAsync(new AddRewardCommand(
            "Another Active", "Also active", RewardType.PercentDiscount, 200, DiscountValue: 20));

        // Deactivate second reward
        await grain.UpdateRewardAsync(reward2.RewardId, isActive: false);

        // Act
        var availableRewards = await grain.GetAvailableRewardsAsync(1);

        // Assert
        availableRewards.Should().HaveCount(2);
        availableRewards.Should().NotContain(r => r.Name == "To Be Deactivated");
        availableRewards.Should().Contain(r => r.Name == "Active Reward");
        availableRewards.Should().Contain(r => r.Name == "Another Active");
    }

    // ==================== Tier Management Tests ====================

    [Fact]
    public async Task UpdateTierAsync_ShouldUpdateTier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        var tier = await grain.AddTierAsync(new AddTierCommand(
            "Silver",
            2,
            500,
            new List<TierBenefit> { new() { Name = "Original Benefit", Type = BenefitType.PointsMultiplier } },
            EarningMultiplier: 1.5m));

        // Act
        var newBenefits = new List<TierBenefit>
        {
            new() { Name = "New Benefit 1", Type = BenefitType.FreeDelivery },
            new() { Name = "New Benefit 2", Type = BenefitType.BirthdayReward }
        };
        await grain.UpdateTierAsync(tier.TierId, pointsRequired: 750, benefits: newBenefits);

        // Assert
        var updatedTier = await grain.GetTierByLevelAsync(2);
        updatedTier.Should().NotBeNull();
        updatedTier!.PointsRequired.Should().Be(750);
        updatedTier.Benefits.Should().HaveCount(2);
        updatedTier.Benefits.Should().Contain(b => b.Name == "New Benefit 1");
    }

    [Fact]
    public async Task RemoveTierAsync_ShouldRemoveTier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        await grain.AddTierAsync(new AddTierCommand("Bronze", 1, 0));
        var silverTier = await grain.AddTierAsync(new AddTierCommand("Silver", 2, 500));
        await grain.AddTierAsync(new AddTierCommand("Gold", 3, 1000));

        // Act
        await grain.RemoveTierAsync(silverTier.TierId);

        // Assert
        var tiers = await grain.GetTiersAsync();
        tiers.Should().HaveCount(2);
        tiers.Should().NotContain(t => t.Name == "Silver");
        tiers.Should().Contain(t => t.Name == "Bronze");
        tiers.Should().Contain(t => t.Name == "Gold");
    }

    // ==================== Reward Validation Tests ====================

    [Fact]
    public async Task AddRewardAsync_ZeroPointsCost_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act - Zero points cost is valid (e.g., free reward for tier benefits)
        var result = await grain.AddRewardAsync(new AddRewardCommand(
            "Free Welcome Gift",
            "Complimentary gift for new members",
            RewardType.FreeItem,
            0, // Zero points cost
            FreeItemId: Guid.NewGuid()));

        // Assert
        result.RewardId.Should().NotBeEmpty();

        var state = await grain.GetStateAsync();
        state.Rewards.Should().HaveCount(1);
        state.Rewards[0].PointsCost.Should().Be(0);
    }

    // ==================== Remove Reward Tests ====================

    [Fact]
    public async Task RemoveRewardAsync_ShouldRemoveReward()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        var reward1 = await grain.AddRewardAsync(new AddRewardCommand(
            "Reward 1", "First", RewardType.PercentDiscount, 100, DiscountValue: 10));
        await grain.AddRewardAsync(new AddRewardCommand(
            "Reward 2", "Second", RewardType.PercentDiscount, 150, DiscountValue: 15));

        // Act
        await grain.RemoveRewardAsync(reward1.RewardId);

        // Assert
        var state = await grain.GetStateAsync();
        state.Rewards.Should().HaveCount(1);
        state.Rewards[0].Name.Should().Be("Reward 2");
    }

    // ==================== Decrement Active Members Tests ====================

    [Fact]
    public async Task DecrementActiveMembersAsync_ShouldDecrement()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Add some members
        await grain.IncrementEnrollmentsAsync();
        await grain.IncrementEnrollmentsAsync();
        await grain.IncrementEnrollmentsAsync();

        var stateBefore = await grain.GetStateAsync();
        stateBefore.ActiveMembers.Should().Be(3);

        // Act
        await grain.DecrementActiveMembersAsync();

        // Assert
        var stateAfter = await grain.GetStateAsync();
        stateAfter.ActiveMembers.Should().Be(2);
        stateAfter.TotalEnrollments.Should().Be(3); // Total unchanged
    }

    [Fact]
    public async Task DecrementActiveMembersAsync_AtZero_ShouldNotGoNegative()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        // Act - Try to decrement when at zero
        await grain.DecrementActiveMembersAsync();

        // Assert - Should stay at 0
        var state = await grain.GetStateAsync();
        state.ActiveMembers.Should().Be(0);
    }

    // ==================== Update Reward Tests ====================

    [Fact]
    public async Task UpdateRewardAsync_ShouldUpdatePointsCost()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateProgramAsync(orgId, programId);

        var reward = await grain.AddRewardAsync(new AddRewardCommand(
            "Adjustable Reward",
            "Points cost will be updated",
            RewardType.PercentDiscount,
            100,
            DiscountValue: 10));

        // Act
        await grain.UpdateRewardAsync(reward.RewardId, pointsCost: 150);

        // Assert
        var state = await grain.GetStateAsync();
        var updatedReward = state.Rewards.First(r => r.Id == reward.RewardId);
        updatedReward.PointsCost.Should().Be(150);
    }

    // ==================== Resume Program Tests ====================

    [Fact]
    public async Task ActivateAsync_FromPaused_ShouldReactivate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var grain = await CreateFullProgramAsync(orgId, programId);
        await grain.ActivateAsync();
        await grain.PauseAsync();

        // Verify paused
        var pausedState = await grain.GetStateAsync();
        pausedState.Status.Should().Be(ProgramStatus.Paused);

        // Act
        await grain.ActivateAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(ProgramStatus.Active);
    }
}
