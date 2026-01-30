using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Grains;

public record CreateLoyaltyProgramCommand(
    Guid OrganizationId,
    string Name,
    string? Description = null);

public record AddEarningRuleCommand(
    string Name,
    EarningType Type,
    decimal? PointsPerDollar = null,
    int? PointsPerVisit = null,
    decimal? BonusMultiplier = null,
    List<DayOfWeek>? ApplicableDays = null,
    decimal? MinimumSpend = null);

public record AddTierCommand(
    string Name,
    int Level,
    int PointsRequired,
    List<TierBenefit>? Benefits = null,
    decimal EarningMultiplier = 1m,
    int? MaintenancePoints = null,
    int? GracePeriodDays = null,
    string Color = "#808080");

public record AddRewardCommand(
    string Name,
    string Description,
    RewardType Type,
    int PointsCost,
    decimal? DiscountValue = null,
    DiscountType? DiscountType = null,
    Guid? FreeItemId = null,
    int? MinimumTierLevel = null,
    int? LimitPerCustomer = null,
    LimitPeriod? LimitPeriod = null,
    int? ValidDays = null);

public record ConfigurePointsExpiryCommand(
    bool Enabled,
    int ExpiryMonths = 12,
    int WarningDays = 30);

public record ConfigureReferralCommand(
    bool Enabled,
    int ReferrerPoints,
    int RefereePoints,
    decimal? MinimumQualifyingSpend = null);

public record LoyaltyProgramCreatedResult(Guid Id, string Name, DateTime CreatedAt);
public record EarningRuleResult(Guid RuleId);
public record TierResult(Guid TierId);
public record RewardDefinitionResult(Guid RewardId);

public record PointsCalculation(
    int BasePoints,
    decimal Multiplier,
    int TotalPoints,
    Guid? AppliedRuleId);

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
