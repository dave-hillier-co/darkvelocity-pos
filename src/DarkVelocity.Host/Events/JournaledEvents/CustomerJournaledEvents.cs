namespace DarkVelocity.Host.Events.JournaledEvents;

/// <summary>
/// Base interface for all Customer journaled events used in event sourcing.
/// </summary>
public interface ICustomerJournaledEvent
{
    Guid CustomerId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record CustomerCreatedJournaledEvent : ICustomerJournaledEvent
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
public sealed record CustomerProfileUpdatedJournaledEvent : ICustomerJournaledEvent
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
public sealed record CustomerLoyaltyEnrolledJournaledEvent : ICustomerJournaledEvent
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
public sealed record CustomerPointsEarnedJournaledEvent : ICustomerJournaledEvent
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
public sealed record CustomerPointsRedeemedJournaledEvent : ICustomerJournaledEvent
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
public sealed record CustomerPointsAdjustedJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public int Adjustment { get; init; }
    [Id(2)] public int NewBalance { get; init; }
    [Id(3)] public string Reason { get; init; } = "";
    [Id(4)] public Guid AdjustedBy { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerPointsExpiredJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public int ExpiredPoints { get; init; }
    [Id(2)] public int NewBalance { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerTierChangedJournaledEvent : ICustomerJournaledEvent
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
public sealed record CustomerRewardIssuedJournaledEvent : ICustomerJournaledEvent
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
public sealed record CustomerRewardRedeemedJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid RewardId { get; init; }
    [Id(2)] public Guid OrderId { get; init; }
    [Id(3)] public decimal? RedeemedValue { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerVisitRecordedJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public Guid? OrderId { get; init; }
    [Id(3)] public decimal SpendAmount { get; init; }
    [Id(4)] public int VisitNumber { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerTagAddedJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Tag { get; init; } = "";
    [Id(2)] public Guid? AddedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerTagRemovedJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Tag { get; init; } = "";
    [Id(2)] public Guid? RemovedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerDeactivatedJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Reason { get; init; } = "";
    [Id(2)] public Guid? DeactivatedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerReactivatedJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid? ReactivatedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerMergedJournaledEvent : ICustomerJournaledEvent
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
public sealed record CustomerReferralCodeGeneratedJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string ReferralCode { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerReferralCompletedJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; } // The referrer
    [Id(1)] public Guid ReferredCustomerId { get; init; }
    [Id(2)] public int? BonusPointsAwarded { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerNoteAddedJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid NoteId { get; init; }
    [Id(2)] public string Content { get; init; } = "";
    [Id(3)] public Guid CreatedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerPreferencesUpdatedJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public List<string>? DietaryRestrictions { get; init; }
    [Id(2)] public List<string>? Allergens { get; init; }
    [Id(3)] public string? SeatingPreference { get; init; }
    [Id(4)] public string? Notes { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerDietaryRestrictionAddedJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Restriction { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerDietaryRestrictionRemovedJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Restriction { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerAllergenAddedJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Allergen { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerAllergenRemovedJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Allergen { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerSeatingPreferenceSetJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Preference { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerRewardsExpiredJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public List<Guid> ExpiredRewardIds { get; init; } = [];
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerReferredBySetJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid ReferrerId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerAnonymizedJournaledEvent : ICustomerJournaledEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
}
