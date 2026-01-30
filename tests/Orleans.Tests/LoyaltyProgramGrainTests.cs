using DarkVelocity.Orleans.Abstractions;
using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.State;
using FluentAssertions;

namespace DarkVelocity.Orleans.Tests;

[Collection(ClusterCollection.Name)]
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
}
