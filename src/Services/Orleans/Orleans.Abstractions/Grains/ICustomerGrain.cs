using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Grains;

public record CreateCustomerCommand(
    Guid OrganizationId,
    string FirstName,
    string LastName,
    string? Email = null,
    string? Phone = null,
    CustomerSource Source = CustomerSource.Direct);

public record UpdateCustomerCommand(
    string? FirstName = null,
    string? LastName = null,
    string? Email = null,
    string? Phone = null,
    DateOnly? DateOfBirth = null,
    CustomerPreferences? Preferences = null);

public record EnrollLoyaltyCommand(
    Guid ProgramId,
    string MemberNumber,
    Guid InitialTierId,
    string TierName);

public record EarnPointsCommand(
    int Points,
    string Reason,
    Guid? OrderId = null,
    Guid? SiteId = null,
    decimal? SpendAmount = null);

public record RedeemPointsCommand(
    int Points,
    Guid OrderId,
    string Reason);

public record AdjustPointsCommand(
    int Points,
    string Reason,
    Guid AdjustedBy);

public record IssueRewardCommand(
    Guid RewardDefinitionId,
    string RewardName,
    int PointsCost,
    DateTime ExpiresAt);

public record RedeemRewardCommand(
    Guid RewardId,
    Guid OrderId,
    Guid SiteId);

public record RecordVisitCommand(
    Guid SiteId,
    Guid? OrderId,
    decimal SpendAmount);

public record CustomerCreatedResult(Guid Id, string DisplayName, DateTime CreatedAt);
public record PointsResult(int NewBalance, int LifetimePoints);
public record RewardResult(Guid RewardId, DateTime ExpiresAt);

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

    // Queries
    Task<bool> ExistsAsync();
    Task<bool> IsLoyaltyMemberAsync();
    Task<int> GetPointsBalanceAsync();
    Task<IReadOnlyList<CustomerReward>> GetAvailableRewardsAsync();
}
