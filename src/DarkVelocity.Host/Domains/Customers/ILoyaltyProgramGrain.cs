using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record CreateLoyaltyProgramCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] string Name,
    [property: Id(2)] string? Description = null);

[GenerateSerializer]
public record AddEarningRuleCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] EarningType Type,
    [property: Id(2)] decimal? PointsPerDollar = null,
    [property: Id(3)] int? PointsPerVisit = null,
    [property: Id(4)] decimal? BonusMultiplier = null,
    [property: Id(5)] List<DayOfWeek>? ApplicableDays = null,
    [property: Id(6)] decimal? MinimumSpend = null);

[GenerateSerializer]
public record AddTierCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] int Level,
    [property: Id(2)] int PointsRequired,
    [property: Id(3)] List<TierBenefit>? Benefits = null,
    [property: Id(4)] decimal EarningMultiplier = 1m,
    [property: Id(5)] int? MaintenancePoints = null,
    [property: Id(6)] int? GracePeriodDays = null,
    [property: Id(7)] string Color = "#808080");

[GenerateSerializer]
public record AddRewardCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] string Description,
    [property: Id(2)] RewardType Type,
    [property: Id(3)] int PointsCost,
    [property: Id(4)] decimal? DiscountValue = null,
    [property: Id(5)] DiscountType? DiscountType = null,
    [property: Id(6)] Guid? FreeItemId = null,
    [property: Id(7)] int? MinimumTierLevel = null,
    [property: Id(8)] int? LimitPerCustomer = null,
    [property: Id(9)] LimitPeriod? LimitPeriod = null,
    [property: Id(10)] int? ValidDays = null);

[GenerateSerializer]
public record ConfigurePointsExpiryCommand(
    [property: Id(0)] bool Enabled,
    [property: Id(1)] int ExpiryMonths = 12,
    [property: Id(2)] int WarningDays = 30);

[GenerateSerializer]
public record ConfigureReferralCommand(
    [property: Id(0)] bool Enabled,
    [property: Id(1)] int ReferrerPoints,
    [property: Id(2)] int RefereePoints,
    [property: Id(3)] decimal? MinimumQualifyingSpend = null);

[GenerateSerializer]
public record LoyaltyProgramCreatedResult([property: Id(0)] Guid Id, [property: Id(1)] string Name, [property: Id(2)] DateTime CreatedAt);
[GenerateSerializer]
public record EarningRuleResult([property: Id(0)] Guid RuleId);
[GenerateSerializer]
public record TierResult([property: Id(0)] Guid TierId);
[GenerateSerializer]
public record RewardDefinitionResult([property: Id(0)] Guid RewardId);

[GenerateSerializer]
public record PointsCalculation(
    [property: Id(0)] int BasePoints,
    [property: Id(1)] decimal Multiplier,
    [property: Id(2)] int TotalPoints,
    [property: Id(3)] Guid? AppliedRuleId);

public interface ILoyaltyProgramGrain : IGrainWithStringKey
{
    Task<LoyaltyProgramCreatedResult> CreateAsync(CreateLoyaltyProgramCommand command);
    Task<LoyaltyProgramState> GetStateAsync();

    // Program lifecycle
    Task UpdateAsync(string? name, string? description);
    Task ActivateAsync();
    Task PauseAsync();
    Task DeactivateAsync();

    // Earning rules
    Task<EarningRuleResult> AddEarningRuleAsync(AddEarningRuleCommand command);
    Task UpdateEarningRuleAsync(Guid ruleId, bool isActive);
    Task RemoveEarningRuleAsync(Guid ruleId);

    // Tiers
    Task<TierResult> AddTierAsync(AddTierCommand command);
    Task UpdateTierAsync(Guid tierId, int? pointsRequired, List<TierBenefit>? benefits);
    Task RemoveTierAsync(Guid tierId);
    Task<LoyaltyTier?> GetTierByLevelAsync(int level);
    Task<LoyaltyTier?> GetNextTierAsync(int currentLevel);

    // Rewards
    Task<RewardDefinitionResult> AddRewardAsync(AddRewardCommand command);
    Task UpdateRewardAsync(Guid rewardId, int? pointsCost, bool? isActive);
    Task RemoveRewardAsync(Guid rewardId);
    Task<IReadOnlyList<RewardDefinition>> GetAvailableRewardsAsync(int tierLevel);

    // Configuration
    Task ConfigurePointsExpiryAsync(ConfigurePointsExpiryCommand command);
    Task ConfigureReferralProgramAsync(ConfigureReferralCommand command);
    Task SetTermsAndConditionsAsync(string terms);

    // Points calculation
    Task<PointsCalculation> CalculatePointsAsync(decimal spendAmount, int customerTierLevel, Guid siteId, DateTime timestamp);

    // Statistics
    Task IncrementEnrollmentsAsync();
    Task DecrementActiveMembersAsync();
    Task RecordPointsIssuedAsync(int points);
    Task RecordPointsRedeemedAsync(int points);

    // Queries
    Task<bool> ExistsAsync();
    Task<bool> IsActiveAsync();
    Task<IReadOnlyList<EarningRule>> GetEarningRulesAsync();
    Task<IReadOnlyList<LoyaltyTier>> GetTiersAsync();
}
