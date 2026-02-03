namespace DarkVelocity.Host.State;

public enum ProgramStatus
{
    Draft,
    Active,
    Paused,
    Archived
}

public enum EarningType
{
    PerDollar,
    PerVisit,
    BonusDay,
    BirthdayBonus,
    SignupBonus
}

[GenerateSerializer]
public record EarningRule
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public string Name { get; init; } = string.Empty;
    [Id(2)] public EarningType Type { get; init; }
    [Id(3)] public decimal? PointsPerDollar { get; init; }
    [Id(4)] public int? PointsPerVisit { get; init; }
    [Id(5)] public decimal? BonusMultiplier { get; init; }
    [Id(6)] public List<DayOfWeek>? ApplicableDays { get; init; }
    [Id(7)] public TimeOnly? StartTime { get; init; }
    [Id(8)] public TimeOnly? EndTime { get; init; }
    [Id(9)] public List<Guid>? ApplicableSites { get; init; }
    [Id(10)] public decimal? MinimumSpend { get; init; }
    [Id(11)] public bool IsActive { get; init; }
}

public enum BenefitType
{
    PointsMultiplier,
    PercentDiscount,
    FreeItem,
    PriorityBooking,
    FreeDelivery,
    ExclusiveAccess,
    BirthdayReward,
    Custom
}

[GenerateSerializer]
public record TierBenefit
{
    [Id(0)] public string Name { get; init; } = string.Empty;
    [Id(1)] public string Description { get; init; } = string.Empty;
    [Id(2)] public BenefitType Type { get; init; }
    [Id(3)] public decimal? Value { get; init; }
}

[GenerateSerializer]
public record LoyaltyTier
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public string Name { get; init; } = string.Empty;
    [Id(2)] public int Level { get; init; }
    [Id(3)] public int PointsRequired { get; init; }
    [Id(4)] public List<TierBenefit> Benefits { get; init; } = [];
    [Id(5)] public decimal EarningMultiplier { get; init; } = 1m;
    [Id(6)] public int? MaintenancePoints { get; init; }
    [Id(7)] public int? GracePeriodDays { get; init; }
    [Id(8)] public string Color { get; init; } = "#808080";
}

public enum RewardType
{
    PercentDiscount,
    FixedDiscount,
    FreeItem,
    BuyOneGetOne,
    FreeUpgrade,
    FreeDelivery,
    Custom
}

public enum LimitPeriod
{
    Day,
    Week,
    Month,
    Year,
    Lifetime
}

[GenerateSerializer]
public record RewardDefinition
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public string Name { get; init; } = string.Empty;
    [Id(2)] public string Description { get; init; } = string.Empty;
    [Id(3)] public RewardType Type { get; init; }
    [Id(4)] public int PointsCost { get; init; }
    [Id(5)] public decimal? DiscountValue { get; init; }
    [Id(6)] public DiscountType? DiscountType { get; init; }
    [Id(7)] public Guid? FreeItemId { get; init; }
    [Id(8)] public int? MinimumTierLevel { get; init; }
    [Id(9)] public int? LimitPerCustomer { get; init; }
    [Id(10)] public LimitPeriod? LimitPeriod { get; init; }
    [Id(11)] public int? ValidDays { get; init; }
    [Id(12)] public string? ImageUrl { get; init; }
    [Id(13)] public bool IsActive { get; init; }
}

[GenerateSerializer]
public record PointsExpiryConfig
{
    [Id(0)] public bool Enabled { get; init; }
    [Id(1)] public int ExpiryMonths { get; init; } = 12;
    [Id(2)] public int WarningDays { get; init; } = 30;
}

[GenerateSerializer]
public record ReferralConfig
{
    [Id(0)] public bool Enabled { get; init; }
    [Id(1)] public int ReferrerPoints { get; init; }
    [Id(2)] public int RefereePoints { get; init; }
    [Id(3)] public decimal? MinimumQualifyingSpend { get; init; }
    [Id(4)] public int? CodeValidityDays { get; init; }
}

[GenerateSerializer]
public sealed class LoyaltyProgramState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public string? Description { get; set; }
    [Id(4)] public ProgramStatus Status { get; set; } = ProgramStatus.Draft;

    [Id(5)] public List<EarningRule> EarningRules { get; set; } = [];
    [Id(6)] public List<LoyaltyTier> Tiers { get; set; } = [];
    [Id(7)] public List<RewardDefinition> Rewards { get; set; } = [];

    [Id(8)] public PointsExpiryConfig? PointsExpiry { get; set; }
    [Id(9)] public ReferralConfig? ReferralProgram { get; set; }
    [Id(10)] public string? TermsAndConditions { get; set; }

    [Id(11)] public int TotalEnrollments { get; set; }
    [Id(12)] public int ActiveMembers { get; set; }
    [Id(13)] public long TotalPointsIssued { get; set; }
    [Id(14)] public long TotalPointsRedeemed { get; set; }

    [Id(15)] public DateTime CreatedAt { get; set; }
    [Id(16)] public DateTime? UpdatedAt { get; set; }
    [Id(17)] public DateTime? ActivatedAt { get; set; }

    [Id(18)] public int Version { get; set; }
}
