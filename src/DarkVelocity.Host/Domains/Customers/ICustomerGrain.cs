using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record CreateCustomerCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] string FirstName,
    [property: Id(2)] string LastName,
    [property: Id(3)] string? Email = null,
    [property: Id(4)] string? Phone = null,
    [property: Id(5)] CustomerSource Source = CustomerSource.Direct);

[GenerateSerializer]
public record UpdateCustomerCommand(
    [property: Id(0)] string? FirstName = null,
    [property: Id(1)] string? LastName = null,
    [property: Id(2)] string? Email = null,
    [property: Id(3)] string? Phone = null,
    [property: Id(4)] DateOnly? DateOfBirth = null,
    [property: Id(5)] CustomerPreferences? Preferences = null);

[GenerateSerializer]
public record EnrollLoyaltyCommand(
    [property: Id(0)] Guid ProgramId,
    [property: Id(1)] string MemberNumber,
    [property: Id(2)] Guid InitialTierId,
    [property: Id(3)] string TierName);

[GenerateSerializer]
public record EarnPointsCommand(
    [property: Id(0)] int Points,
    [property: Id(1)] string Reason,
    [property: Id(2)] Guid? OrderId = null,
    [property: Id(3)] Guid? SiteId = null,
    [property: Id(4)] decimal? SpendAmount = null);

[GenerateSerializer]
public record RedeemPointsCommand(
    [property: Id(0)] int Points,
    [property: Id(1)] Guid OrderId,
    [property: Id(2)] string Reason,
    [property: Id(3)] decimal? DiscountValue = null,
    [property: Id(4)] string? RewardType = null);

[GenerateSerializer]
public record AdjustPointsCommand(
    [property: Id(0)] int Points,
    [property: Id(1)] string Reason,
    [property: Id(2)] Guid AdjustedBy);

[GenerateSerializer]
public record IssueRewardCommand(
    [property: Id(0)] Guid RewardDefinitionId,
    [property: Id(1)] string RewardName,
    [property: Id(2)] int PointsCost,
    [property: Id(3)] DateTime? ExpiresAt = null);

[GenerateSerializer]
public record RedeemRewardCommand(
    [property: Id(0)] Guid RewardId,
    [property: Id(1)] Guid OrderId,
    [property: Id(2)] Guid SiteId);

[GenerateSerializer]
public record RecordVisitCommand(
    [property: Id(0)] Guid SiteId,
    [property: Id(1)] Guid? OrderId,
    [property: Id(2)] decimal SpendAmount,
    [property: Id(3)] string? SiteName = null,
    [property: Id(4)] Guid? BookingId = null,
    [property: Id(5)] int PartySize = 1,
    [property: Id(6)] int PointsEarned = 0,
    [property: Id(7)] string? Notes = null);

[GenerateSerializer]
public record UpdatePreferencesCommand(
    [property: Id(0)] List<string>? DietaryRestrictions = null,
    [property: Id(1)] List<string>? Allergens = null,
    [property: Id(2)] string? SeatingPreference = null,
    [property: Id(3)] string? Notes = null);

// ==================== Consent Commands ====================

[GenerateSerializer]
public record UpdateConsentCommand(
    [property: Id(0)] bool? MarketingEmail = null,
    [property: Id(1)] bool? Sms = null,
    [property: Id(2)] bool? DataRetention = null,
    [property: Id(3)] bool? Profiling = null,
    [property: Id(4)] string? ConsentVersion = null,
    [property: Id(5)] string? IpAddress = null,
    [property: Id(6)] string? UserAgent = null);

// ==================== VIP Commands ====================

[GenerateSerializer]
public record GrantVipStatusCommand(
    [property: Id(0)] string Reason,
    [property: Id(1)] Guid? GrantedBy = null);

[GenerateSerializer]
public record RevokeVipStatusCommand(
    [property: Id(0)] string? Reason = null,
    [property: Id(1)] Guid? RevokedBy = null);

// ==================== Birthday Commands ====================

[GenerateSerializer]
public record SetBirthdayCommand(
    [property: Id(0)] DateOnly Birthday);

[GenerateSerializer]
public record IssueBirthdayRewardCommand(
    [property: Id(0)] string RewardName,
    [property: Id(1)] int? ValidDays = 30);

// ==================== Enhanced Referral Commands ====================

[GenerateSerializer]
public record GenerateReferralCodeCommand(
    [property: Id(0)] string? Prefix = null);

[GenerateSerializer]
public record CompleteReferralCommand(
    [property: Id(0)] Guid ReferredCustomerId,
    [property: Id(1)] int PointsToAward);

// ==================== Segmentation Thresholds ====================

[GenerateSerializer]
public record SegmentationThresholds(
    [property: Id(0)] int RecencyDaysExcellent = 7,
    [property: Id(1)] int RecencyDaysGood = 30,
    [property: Id(2)] int RecencyDaysFair = 60,
    [property: Id(3)] int RecencyDaysPoor = 90,
    [property: Id(4)] int FrequencyCountExcellent = 10,
    [property: Id(5)] int FrequencyCountGood = 5,
    [property: Id(6)] int FrequencyCountFair = 3,
    [property: Id(7)] int FrequencyCountPoor = 1,
    [property: Id(8)] decimal MonetaryValueExcellent = 1000m,
    [property: Id(9)] decimal MonetaryValueGood = 500m,
    [property: Id(10)] decimal MonetaryValueFair = 200m,
    [property: Id(11)] decimal MonetaryValuePoor = 50m);

// ==================== VIP Thresholds ====================

[GenerateSerializer]
public record VipThresholds(
    [property: Id(0)] decimal? MinimumSpend = 1000m,
    [property: Id(1)] int? MinimumVisits = 10,
    [property: Id(2)] bool RequireBoth = false);

[GenerateSerializer]
public record CustomerCreatedResult([property: Id(0)] Guid Id, [property: Id(1)] string DisplayName, [property: Id(2)] DateTime CreatedAt);
[GenerateSerializer]
public record PointsResult([property: Id(0)] int NewBalance, [property: Id(1)] int LifetimePoints);
[GenerateSerializer]
public record RewardResult([property: Id(0)] Guid RewardId, [property: Id(1)] DateTime? ExpiresAt = null);
[GenerateSerializer]
public record ReferralResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] int PointsAwarded,
    [property: Id(2)] int TotalReferrals,
    [property: Id(3)] bool CapReached = false);

public interface ICustomerGrain : IGrainWithStringKey
{
    Task<CustomerCreatedResult> CreateAsync(CreateCustomerCommand command);
    Task<CustomerState> GetStateAsync();
    Task UpdateAsync(UpdateCustomerCommand command);

    // Tags & Notes
    Task AddTagAsync(string tag);
    Task RemoveTagAsync(string tag);
    Task AddNoteAsync(string content, Guid createdBy);

    // Loyalty
    Task EnrollInLoyaltyAsync(EnrollLoyaltyCommand command);
    Task<PointsResult> EarnPointsAsync(EarnPointsCommand command);
    Task<PointsResult> RedeemPointsAsync(RedeemPointsCommand command);
    Task<PointsResult> AdjustPointsAsync(AdjustPointsCommand command);
    Task ExpirePointsAsync(int points, DateTime expiryDate);
    Task PromoteTierAsync(Guid newTierId, string tierName, int pointsToNextTier);
    Task DemoteTierAsync(Guid newTierId, string tierName, int pointsToNextTier);

    // Rewards
    Task<RewardResult> IssueRewardAsync(IssueRewardCommand command);
    Task RedeemRewardAsync(RedeemRewardCommand command);
    Task ExpireRewardsAsync();

    // Visits
    Task RecordVisitAsync(RecordVisitCommand command);

    // Referrals
    Task SetReferralCodeAsync(string code);
    Task SetReferredByAsync(Guid referrerId);
    Task IncrementReferralCountAsync();

    // Customer merge
    Task MergeFromAsync(Guid sourceCustomerId);

    // GDPR & Privacy
    Task DeleteAsync();
    Task AnonymizeAsync();
    Task<bool> RequestDataDeletionAsync(string? requestedBy = null, string? reason = null);

    // GDPR Consent Management
    Task UpdateConsentAsync(UpdateConsentCommand command);
    Task<ConsentStatus> GetConsentStatusAsync();
    Task<IReadOnlyList<ConsentChange>> GetConsentHistoryAsync();

    // RFM Segmentation
    Task<RfmScore> CalculateRfmScoreAsync(SegmentationThresholds? thresholds = null);
    Task<CustomerSegment> GetSegmentAsync();
    Task<IReadOnlyList<SegmentChange>> GetSegmentHistoryAsync();
    Task RecalculateSegmentAsync(SegmentationThresholds? thresholds = null);

    // VIP Detection
    Task<bool> IsVipAsync();
    Task GrantVipStatusAsync(GrantVipStatusCommand command);
    Task RevokeVipStatusAsync(RevokeVipStatusCommand command);
    Task CheckAndUpdateVipStatusAsync(VipThresholds? thresholds = null);
    Task<VipStatus> GetVipStatusAsync();

    // Birthday Rewards
    Task SetBirthdayAsync(SetBirthdayCommand command);
    Task<RewardResult> IssueBirthdayRewardAsync(IssueBirthdayRewardCommand command);
    Task<bool> HasBirthdayRewardThisYearAsync();
    Task<BirthdayRewardStatus?> GetCurrentBirthdayRewardAsync();

    // Enhanced Referral Tracking
    Task<string> GenerateReferralCodeAsync(GenerateReferralCodeCommand? command = null);
    Task<ReferralResult> CompleteReferralAsync(CompleteReferralCommand command);
    Task<ReferralStatus> GetReferralStatusAsync();
    Task<bool> HasReachedReferralCapAsync();

    // Visit History
    Task<IReadOnlyList<CustomerVisitRecord>> GetVisitHistoryAsync(int limit = 50);
    Task<IReadOnlyList<CustomerVisitRecord>> GetVisitsBySiteAsync(Guid siteId, int limit = 20);
    Task<CustomerVisitRecord?> GetLastVisitAsync();

    // Preferences
    Task UpdatePreferencesAsync(UpdatePreferencesCommand command);
    Task AddDietaryRestrictionAsync(string restriction);
    Task RemoveDietaryRestrictionAsync(string restriction);
    Task AddAllergenAsync(string allergen);
    Task RemoveAllergenAsync(string allergen);
    Task SetSeatingPreferenceAsync(string preference);

    // Queries
    // No-show tracking
    Task RecordNoShowAsync(DateTime bookingTime, Guid? bookingId = null);

    Task<bool> ExistsAsync();
    Task<bool> IsLoyaltyMemberAsync();
    Task<int> GetPointsBalanceAsync();
    Task<IReadOnlyList<CustomerReward>> GetAvailableRewardsAsync();
}
