namespace DarkVelocity.Host.State;

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

[GenerateSerializer]
public record ContactInfo
{
    [Id(0)] public string? Email { get; init; }
    [Id(1)] public string? Phone { get; init; }
    [Id(2)] public string? PhoneType { get; init; }
    [Id(3)] public Address? Address { get; init; }
    [Id(4)] public bool EmailOptIn { get; init; }
    [Id(5)] public bool SmsOptIn { get; init; }
}

[GenerateSerializer]
public record CustomerPreferences
{
    [Id(0)] public List<Guid> FavoriteItemIds { get; init; } = [];
    [Id(1)] public List<string> DietaryRestrictions { get; init; } = [];
    [Id(2)] public List<string> Allergens { get; init; } = [];
    [Id(3)] public string? SeatingPreference { get; init; }
    [Id(4)] public string? Notes { get; init; }
}

[GenerateSerializer]
public record CustomerNote
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public string Content { get; init; } = string.Empty;
    [Id(2)] public Guid CreatedBy { get; init; }
    [Id(3)] public DateTime CreatedAt { get; init; }
}

[GenerateSerializer]
public record LoyaltyStatus
{
    [Id(0)] public DateTime EnrolledAt { get; init; }
    [Id(1)] public Guid ProgramId { get; init; }
    [Id(2)] public string MemberNumber { get; init; } = string.Empty;
    [Id(3)] public Guid TierId { get; init; }
    [Id(4)] public string TierName { get; init; } = string.Empty;
    [Id(5)] public int PointsBalance { get; init; }
    [Id(6)] public int LifetimePoints { get; init; }
    [Id(7)] public int YtdPoints { get; init; }
    [Id(8)] public int PointsToNextTier { get; init; }
    [Id(9)] public DateTime? TierExpiresAt { get; init; }
    [Id(10)] public int PointsExpiring { get; init; }
    [Id(11)] public DateTime? PointsExpiringAt { get; init; }
}

[GenerateSerializer]
public record CustomerStats
{
    [Id(0)] public int TotalVisits { get; init; }
    [Id(1)] public decimal TotalSpend { get; init; }
    [Id(2)] public decimal AverageCheck { get; init; }
    [Id(3)] public Guid? LastVisitSiteId { get; init; }
    [Id(4)] public Guid? FavoriteSiteId { get; init; }
    [Id(5)] public int DaysSinceLastVisit { get; init; }
    [Id(6)] public CustomerSegment Segment { get; init; }
}

[GenerateSerializer]
public record CustomerReward
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public Guid RewardDefinitionId { get; init; }
    [Id(2)] public string Name { get; init; } = string.Empty;
    [Id(3)] public RewardStatus Status { get; init; }
    [Id(4)] public int PointsSpent { get; init; }
    [Id(5)] public DateTime IssuedAt { get; init; }
    [Id(6)] public DateTime ExpiresAt { get; init; }
    [Id(7)] public DateTime? RedeemedAt { get; init; }
    [Id(8)] public Guid? RedemptionOrderId { get; init; }
    [Id(9)] public Guid? RedemptionSiteId { get; init; }
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

    // Visit History (recent visits, limited to last 50)
    [Id(27)] public List<CustomerVisitRecord> VisitHistory { get; set; } = [];

    // Timestamps
    [Id(22)] public DateTime CreatedAt { get; set; }
    [Id(23)] public DateTime? UpdatedAt { get; set; }
    [Id(24)] public DateTime? LastVisitAt { get; set; }
    [Id(25)] public List<Guid> MergedFrom { get; set; } = [];
}

[GenerateSerializer]
public record CustomerVisitRecord
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public string? SiteName { get; init; }
    [Id(3)] public DateTime VisitedAt { get; init; }
    [Id(4)] public Guid? OrderId { get; init; }
    [Id(5)] public Guid? BookingId { get; init; }
    [Id(6)] public decimal SpendAmount { get; init; }
    [Id(7)] public int PartySize { get; init; }
    [Id(8)] public int PointsEarned { get; init; }
    [Id(9)] public string? Notes { get; init; }
}
