namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Customer events used in event sourcing.
/// </summary>
public interface ICustomerEvent
{
    Guid CustomerId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record CustomerCreated : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public string DisplayName { get; init; } = "";
    [Id(3)] public string? Email { get; init; }
    [Id(4)] public string? Phone { get; init; }
    [Id(5)] public string? FirstName { get; init; }
    [Id(6)] public string? LastName { get; init; }
    [Id(7)] public string Source { get; init; } = "";
    [Id(8)] public Guid? ReferredByCustomerId { get; init; }
    [Id(9)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerProfileUpdated : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string? DisplayName { get; init; }
    [Id(2)] public string? Email { get; init; }
    [Id(3)] public string? Phone { get; init; }
    [Id(4)] public string? FirstName { get; init; }
    [Id(5)] public string? LastName { get; init; }
    [Id(6)] public Guid? UpdatedBy { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerLoyaltyEnrolled : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid ProgramId { get; init; }
    [Id(2)] public string MemberNumber { get; init; } = "";
    [Id(3)] public Guid InitialTierId { get; init; }
    [Id(4)] public string TierName { get; init; } = "";
    [Id(5)] public int InitialPointsBalance { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerPointsEarned : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public int Points { get; init; }
    [Id(2)] public int NewBalance { get; init; }
    [Id(3)] public Guid? OrderId { get; init; }
    [Id(4)] public decimal? SpendAmount { get; init; }
    [Id(5)] public string Reason { get; init; } = "";
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerPointsRedeemed : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public int Points { get; init; }
    [Id(2)] public int NewBalance { get; init; }
    [Id(3)] public Guid OrderId { get; init; }
    [Id(4)] public decimal DiscountValue { get; init; }
    [Id(5)] public string RewardType { get; init; } = "";
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerPointsAdjusted : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public int Adjustment { get; init; }
    [Id(2)] public int NewBalance { get; init; }
    [Id(3)] public string Reason { get; init; } = "";
    [Id(4)] public Guid AdjustedBy { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerPointsExpired : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public int ExpiredPoints { get; init; }
    [Id(2)] public int NewBalance { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerTierChanged : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid OldTierId { get; init; }
    [Id(2)] public string OldTierName { get; init; } = "";
    [Id(3)] public Guid NewTierId { get; init; }
    [Id(4)] public string NewTierName { get; init; } = "";
    [Id(5)] public decimal CumulativeSpend { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerRewardIssued : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid RewardId { get; init; }
    [Id(2)] public string RewardType { get; init; } = "";
    [Id(3)] public string RewardName { get; init; } = "";
    [Id(4)] public decimal? Value { get; init; }
    [Id(5)] public DateOnly? ExpiryDate { get; init; }
    [Id(6)] public string? Reason { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerRewardRedeemed : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid RewardId { get; init; }
    [Id(2)] public Guid OrderId { get; init; }
    [Id(3)] public decimal? RedeemedValue { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerVisitRecorded : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public Guid? OrderId { get; init; }
    [Id(3)] public decimal SpendAmount { get; init; }
    [Id(4)] public int VisitNumber { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerTagAdded : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Tag { get; init; } = "";
    [Id(2)] public Guid? AddedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerTagRemoved : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Tag { get; init; } = "";
    [Id(2)] public Guid? RemovedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerDeactivated : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Reason { get; init; } = "";
    [Id(2)] public Guid? DeactivatedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerReactivated : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid? ReactivatedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerMerged : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; } // This is the target (surviving) customer
    [Id(1)] public Guid SourceCustomerId { get; init; }
    [Id(2)] public decimal CombinedLifetimeSpend { get; init; }
    [Id(3)] public int CombinedTotalPoints { get; init; }
    [Id(4)] public int CombinedVisits { get; init; }
    [Id(5)] public Guid MergedBy { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerReferralCodeGenerated : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string ReferralCode { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerReferralCompleted : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; } // The referrer
    [Id(1)] public Guid ReferredCustomerId { get; init; }
    [Id(2)] public int? BonusPointsAwarded { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerNoteAdded : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid NoteId { get; init; }
    [Id(2)] public string Content { get; init; } = "";
    [Id(3)] public Guid CreatedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerPreferencesUpdated : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public List<string>? DietaryRestrictions { get; init; }
    [Id(2)] public List<string>? Allergens { get; init; }
    [Id(3)] public string? SeatingPreference { get; init; }
    [Id(4)] public string? Notes { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerDietaryRestrictionAdded : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Restriction { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerDietaryRestrictionRemoved : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Restriction { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerAllergenAdded : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Allergen { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerAllergenRemoved : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Allergen { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerSeatingPreferenceSet : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Preference { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerRewardsExpired : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public List<Guid> ExpiredRewardIds { get; init; } = [];
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerReferredBySet : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid ReferrerId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerAnonymized : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
    [Id(2)] public string? AnonymizedHash { get; init; }
    [Id(3)] public bool RetainAggregateStats { get; init; }
}

// ==================== RFM Segmentation Events ====================

[GenerateSerializer]
public sealed record CustomerRfmScoreCalculated : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public int RecencyScore { get; init; }
    [Id(2)] public int FrequencyScore { get; init; }
    [Id(3)] public int MonetaryScore { get; init; }
    [Id(4)] public int DaysSinceLastVisit { get; init; }
    [Id(5)] public int VisitCount { get; init; }
    [Id(6)] public decimal TotalSpend { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerSegmentChanged : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string PreviousSegment { get; init; } = "";
    [Id(2)] public string NewSegment { get; init; } = "";
    [Id(3)] public int? RecencyScore { get; init; }
    [Id(4)] public int? FrequencyScore { get; init; }
    [Id(5)] public int? MonetaryScore { get; init; }
    [Id(6)] public string? Reason { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

// ==================== GDPR Consent Events ====================

[GenerateSerializer]
public sealed record CustomerConsentUpdated : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string ConsentType { get; init; } = "";
    [Id(2)] public bool PreviousValue { get; init; }
    [Id(3)] public bool NewValue { get; init; }
    [Id(4)] public string? ConsentVersion { get; init; }
    [Id(5)] public string? IpAddress { get; init; }
    [Id(6)] public string? UserAgent { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerDataDeletionRequested : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string? RequestedBy { get; init; }
    [Id(2)] public string? Reason { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

// ==================== VIP Events ====================

[GenerateSerializer]
public sealed record CustomerVipStatusGranted : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Reason { get; init; } = "";
    [Id(2)] public decimal? SpendAtGrant { get; init; }
    [Id(3)] public int? VisitsAtGrant { get; init; }
    [Id(4)] public bool ManuallyAssigned { get; init; }
    [Id(5)] public Guid? GrantedBy { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerVipStatusRevoked : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string? Reason { get; init; }
    [Id(2)] public Guid? RevokedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

// ==================== Birthday Reward Events ====================

[GenerateSerializer]
public sealed record CustomerBirthdaySet : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public DateOnly Birthday { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerBirthdayRewardIssued : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid RewardId { get; init; }
    [Id(2)] public string RewardName { get; init; } = "";
    [Id(3)] public int Year { get; init; }
    [Id(4)] public DateOnly? ExpiresAt { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerBirthdayRewardRedeemed : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid RewardId { get; init; }
    [Id(2)] public Guid OrderId { get; init; }
    [Id(3)] public int Year { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

// ==================== Enhanced Referral Events ====================

[GenerateSerializer]
public sealed record CustomerReferralRewardAwarded : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid ReferredCustomerId { get; init; }
    [Id(2)] public int PointsAwarded { get; init; }
    [Id(3)] public int TotalReferrals { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerReferralCapReached : ICustomerEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public int TotalReferrals { get; init; }
    [Id(2)] public int TotalPointsEarned { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}
