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
    Lost,
    PotentialLoyal,
    Hibernating
}

/// <summary>
/// RFM (Recency, Frequency, Monetary) scores for customer segmentation.
/// Each score ranges from 1-5, with 5 being the best.
/// </summary>
[GenerateSerializer]
public record RfmScore
{
    [Id(0)] public int RecencyScore { get; init; } = 1;
    [Id(1)] public int FrequencyScore { get; init; } = 1;
    [Id(2)] public int MonetaryScore { get; init; } = 1;
    [Id(3)] public DateTime CalculatedAt { get; init; }
    [Id(4)] public int DaysSinceLastVisit { get; init; }
    [Id(5)] public int VisitCount { get; init; }
    [Id(6)] public decimal TotalSpend { get; init; }

    public int CombinedScore => RecencyScore + FrequencyScore + MonetaryScore;
}

/// <summary>
/// Tracks a segment change for historical analysis.
/// </summary>
[GenerateSerializer]
public record SegmentChange
{
    [Id(0)] public CustomerSegment PreviousSegment { get; init; }
    [Id(1)] public CustomerSegment NewSegment { get; init; }
    [Id(2)] public RfmScore? RfmScore { get; init; }
    [Id(3)] public DateTime ChangedAt { get; init; }
    [Id(4)] public string? Reason { get; init; }
}

/// <summary>
/// GDPR consent tracking with timestamps and audit trail.
/// </summary>
[GenerateSerializer]
public record ConsentStatus
{
    [Id(0)] public bool MarketingEmail { get; init; }
    [Id(1)] public DateTime? MarketingEmailConsentedAt { get; init; }
    [Id(2)] public bool Sms { get; init; }
    [Id(3)] public DateTime? SmsConsentedAt { get; init; }
    [Id(4)] public bool DataRetention { get; init; }
    [Id(5)] public DateTime? DataRetentionConsentedAt { get; init; }
    [Id(6)] public bool Profiling { get; init; }
    [Id(7)] public DateTime? ProfilingConsentedAt { get; init; }
    [Id(8)] public string? ConsentVersion { get; init; }
    [Id(9)] public DateTime LastUpdated { get; init; }
}

/// <summary>
/// Tracks a consent change for audit purposes.
/// </summary>
[GenerateSerializer]
public record ConsentChange
{
    [Id(0)] public string ConsentType { get; init; } = "";
    [Id(1)] public bool PreviousValue { get; init; }
    [Id(2)] public bool NewValue { get; init; }
    [Id(3)] public DateTime ChangedAt { get; init; }
    [Id(4)] public string? IpAddress { get; init; }
    [Id(5)] public string? UserAgent { get; init; }
}

/// <summary>
/// VIP status and criteria tracking.
/// </summary>
[GenerateSerializer]
public record VipStatus
{
    [Id(0)] public bool IsVip { get; init; }
    [Id(1)] public DateTime? VipSince { get; init; }
    [Id(2)] public string? VipReason { get; init; }
    [Id(3)] public decimal? SpendAtVipGrant { get; init; }
    [Id(4)] public int? VisitsAtVipGrant { get; init; }
    [Id(5)] public bool ManuallyAssigned { get; init; }
}

/// <summary>
/// Birthday reward tracking.
/// </summary>
[GenerateSerializer]
public record BirthdayRewardStatus
{
    [Id(0)] public Guid? CurrentRewardId { get; init; }
    [Id(1)] public DateTime? IssuedAt { get; init; }
    [Id(2)] public DateTime? RedeemedAt { get; init; }
    [Id(3)] public int Year { get; init; }
}

/// <summary>
/// Referral program tracking with rewards.
/// </summary>
[GenerateSerializer]
public record ReferralStatus
{
    [Id(0)] public string? ReferralCode { get; init; }
    [Id(1)] public Guid? ReferredBy { get; init; }
    [Id(2)] public int SuccessfulReferrals { get; init; }
    [Id(3)] public int TotalPointsEarnedFromReferrals { get; init; }
    [Id(4)] public int ReferralRewardsCap { get; init; } = 10;
    [Id(5)] public List<Guid> ReferredCustomers { get; init; } = [];
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

    // Referral (legacy fields - use Referral property for new code)
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

    // RFM Segmentation
    [Id(28)] public RfmScore? RfmScore { get; set; }
    [Id(29)] public List<SegmentChange> SegmentHistory { get; set; } = [];

    // GDPR Consent Management
    [Id(30)] public ConsentStatus Consent { get; set; } = new();
    [Id(31)] public List<ConsentChange> ConsentHistory { get; set; } = [];

    // VIP Status
    [Id(32)] public VipStatus VipStatus { get; set; } = new();

    // Birthday Rewards
    [Id(33)] public BirthdayRewardStatus? CurrentBirthdayReward { get; set; }
    [Id(34)] public List<BirthdayRewardStatus> BirthdayRewardHistory { get; set; } = [];

    // Enhanced Referral
    [Id(35)] public ReferralStatus Referral { get; set; } = new();

    // Anonymization tracking
    [Id(36)] public bool IsAnonymized { get; set; }
    [Id(37)] public DateTime? AnonymizedAt { get; set; }
    [Id(38)] public string? AnonymizedHash { get; set; }

    // No-show tracking
    [Id(39)] public int NoShowCount { get; set; }
    [Id(40)] public DateTime? LastNoShowAt { get; set; }
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
