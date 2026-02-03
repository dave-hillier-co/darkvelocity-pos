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

[GenerateSerializer]
public record CustomerCreatedResult([property: Id(0)] Guid Id, [property: Id(1)] string DisplayName, [property: Id(2)] DateTime CreatedAt);
[GenerateSerializer]
public record PointsResult([property: Id(0)] int NewBalance, [property: Id(1)] int LifetimePoints);
[GenerateSerializer]
public record RewardResult([property: Id(0)] Guid RewardId, [property: Id(1)] DateTime? ExpiresAt = null);

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

    // GDPR
    Task DeleteAsync();
    Task AnonymizeAsync();

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
    Task<bool> ExistsAsync();
    Task<bool> IsLoyaltyMemberAsync();
    Task<int> GetPointsBalanceAsync();
    Task<IReadOnlyList<CustomerReward>> GetAvailableRewardsAsync();
}
