namespace DarkVelocity.Orleans.Abstractions.State;

public enum CustomerStatus
{
    Active,
    Inactive,
    Blocked
}

public enum CustomerSource
{
    Direct,
    Website,
    Mobile,
    Import,
    Referral,
    ThirdParty
}

public enum CustomerSegment
{
    New,
    Regular,
    Loyal,
    Champion,
    AtRisk,
    Lapsed,
    Lost
}

public record ContactInfo
{
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? PhoneType { get; init; }
    public Address? Address { get; init; }
    public bool EmailOptIn { get; init; }
    public bool SmsOptIn { get; init; }
}

public record CustomerPreferences
{
    public List<Guid> FavoriteItemIds { get; init; } = [];
    public List<string> DietaryRestrictions { get; init; } = [];
    public List<string> Allergens { get; init; } = [];
    public string? SeatingPreference { get; init; }
    public string? Notes { get; init; }
}

public record CustomerNote
{
    public Guid Id { get; init; }
    public string Content { get; init; } = string.Empty;
    public Guid CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record LoyaltyStatus
{
    public DateTime EnrolledAt { get; init; }
    public Guid ProgramId { get; init; }
    public string MemberNumber { get; init; } = string.Empty;
    public Guid TierId { get; init; }
    public string TierName { get; init; } = string.Empty;
    public int PointsBalance { get; init; }
    public int LifetimePoints { get; init; }
    public int YtdPoints { get; init; }
    public int PointsToNextTier { get; init; }
    public DateTime? TierExpiresAt { get; init; }
    public int PointsExpiring { get; init; }
    public DateTime? PointsExpiringAt { get; init; }
}

public record CustomerStats
{
    public int TotalVisits { get; init; }
    public decimal TotalSpend { get; init; }
    public decimal AverageCheck { get; init; }
    public Guid? LastVisitSiteId { get; init; }
    public Guid? FavoriteSiteId { get; init; }
    public int DaysSinceLastVisit { get; init; }
    public CustomerSegment Segment { get; init; }
}

public record CustomerReward
{
    public Guid Id { get; init; }
    public Guid RewardDefinitionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public RewardStatus Status { get; init; }
    public int PointsSpent { get; init; }
    public DateTime IssuedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime? RedeemedAt { get; init; }
    public Guid? RedemptionOrderId { get; init; }
    public Guid? RedemptionSiteId { get; init; }
}

public enum RewardStatus
{
    Available,
    Redeemed,
    Expired,
    Cancelled
}

[GenerateSerializer]
public sealed class CustomerState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    // Profile
    [Id(3)] public string FirstName { get; set; } = string.Empty;
    [Id(4)] public string LastName { get; set; } = string.Empty;
    [Id(5)] public string DisplayName { get; set; } = string.Empty;
    [Id(6)] public DateOnly? DateOfBirth { get; set; }
    [Id(7)] public DateOnly? Anniversary { get; set; }
    [Id(8)] public string? Gender { get; set; }
    [Id(9)] public string? AvatarUrl { get; set; }

    [Id(10)] public ContactInfo Contact { get; set; } = new();
    [Id(11)] public CustomerPreferences Preferences { get; set; } = new();
    [Id(12)] public List<string> Tags { get; set; } = [];
    [Id(13)] public CustomerSource Source { get; set; }
    [Id(14)] public Dictionary<string, string> ExternalIds { get; set; } = [];

    // Loyalty
    [Id(15)] public LoyaltyStatus? Loyalty { get; set; }
    [Id(16)] public List<CustomerReward> Rewards { get; set; } = [];

    // Stats
    [Id(17)] public CustomerStats Stats { get; set; } = new();
    [Id(18)] public List<CustomerNote> Notes { get; set; } = [];

    // Referral
    [Id(19)] public string? ReferralCode { get; set; }
    [Id(20)] public Guid? ReferredBy { get; set; }
    [Id(21)] public int SuccessfulReferrals { get; set; }

    // Timestamps
    [Id(22)] public DateTime CreatedAt { get; set; }
    [Id(23)] public DateTime? UpdatedAt { get; set; }
    [Id(24)] public DateTime? LastVisitAt { get; set; }
    [Id(25)] public List<Guid> MergedFrom { get; set; } = [];

    [Id(26)] public int Version { get; set; }
}
