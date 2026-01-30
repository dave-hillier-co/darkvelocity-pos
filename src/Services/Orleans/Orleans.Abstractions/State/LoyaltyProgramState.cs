namespace DarkVelocity.Orleans.Abstractions.State;

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

public record EarningRule
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public EarningType Type { get; init; }
    public decimal? PointsPerDollar { get; init; }
    public int? PointsPerVisit { get; init; }
    public decimal? BonusMultiplier { get; init; }
    public List<DayOfWeek>? ApplicableDays { get; init; }
    public TimeOnly? StartTime { get; init; }
    public TimeOnly? EndTime { get; init; }
    public List<Guid>? ApplicableSites { get; init; }
    public decimal? MinimumSpend { get; init; }
    public bool IsActive { get; init; }
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

public record TierBenefit
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public BenefitType Type { get; init; }
    public decimal? Value { get; init; }
}

public record LoyaltyTier
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Level { get; init; }
    public int PointsRequired { get; init; }
    public List<TierBenefit> Benefits { get; init; } = [];
    public decimal EarningMultiplier { get; init; } = 1m;
    public int? MaintenancePoints { get; init; }
    public int? GracePeriodDays { get; init; }
    public string Color { get; init; } = "#808080";
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

public record RewardDefinition
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public RewardType Type { get; init; }
    public int PointsCost { get; init; }
    public decimal? DiscountValue { get; init; }
    public DiscountType? DiscountType { get; init; }
    public Guid? FreeItemId { get; init; }
    public int? MinimumTierLevel { get; init; }
    public int? LimitPerCustomer { get; init; }
    public LimitPeriod? LimitPeriod { get; init; }
    public int? ValidDays { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsActive { get; init; }
}

public record PointsExpiryConfig
{
    public bool Enabled { get; init; }
    public int ExpiryMonths { get; init; } = 12;
    public int WarningDays { get; init; } = 30;
}

public record ReferralConfig
{
    public bool Enabled { get; init; }
    public int ReferrerPoints { get; init; }
    public int RefereePoints { get; init; }
    public decimal? MinimumQualifyingSpend { get; init; }
    public int? CodeValidityDays { get; init; }
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
