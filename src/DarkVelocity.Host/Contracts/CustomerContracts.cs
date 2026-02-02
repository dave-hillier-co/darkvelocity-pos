using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

public record CreateCustomerRequest(
    string FirstName,
    string LastName,
    string? Email = null,
    string? Phone = null,
    CustomerSource Source = CustomerSource.Direct);

public record UpdateCustomerRequest(
    string? FirstName = null,
    string? LastName = null,
    string? Email = null,
    string? Phone = null,
    DateOnly? DateOfBirth = null,
    CustomerPreferences? Preferences = null);

public record EnrollLoyaltyRequest(Guid ProgramId, string MemberNumber, Guid InitialTierId, string TierName);
public record EarnPointsRequest(int Points, string Reason, Guid? OrderId = null, Guid? SiteId = null, decimal? SpendAmount = null);
public record RedeemPointsRequest(int Points, Guid OrderId, string Reason);
