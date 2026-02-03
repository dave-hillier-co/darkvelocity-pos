using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

public record CreateUserRequest(
    string Email,
    string DisplayName,
    UserType Type = UserType.Employee,
    string? FirstName = null,
    string? LastName = null);

public record UpdateUserRequest(
    string? DisplayName = null,
    string? FirstName = null,
    string? LastName = null,
    UserPreferences? Preferences = null);

public record SetPinRequest(string Pin);

public record GrantSiteAccessRequest(Guid SiteId);

public record AddToGroupRequest(Guid GroupId);

public record LinkExternalIdentityRequest(
    string Provider,
    string ExternalId,
    string? Email = null);

public record UserResponse(
    Guid Id,
    Guid OrganizationId,
    string Email,
    string DisplayName,
    string? FirstName,
    string? LastName,
    UserStatus Status,
    UserType Type,
    List<Guid> SiteAccess,
    List<Guid> UserGroupIds,
    Dictionary<string, string> ExternalIds,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastLoginAt);

public record UserSummary(
    Guid Id,
    string Email,
    string DisplayName,
    UserStatus Status,
    UserType Type);

public record CreateUserGroupRequest(
    string Name,
    string? Description = null);

public record UpdateUserGroupRequest(
    string? Name = null,
    string? Description = null);

public record UserGroupResponse(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    List<Guid> MemberIds,
    bool IsSystemGroup,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Response for multi-org OAuth login when user exists in multiple organizations.
/// </summary>
public record PendingOAuthResponse(
    string PendingToken,
    string Email,
    string? Name,
    List<OrganizationOption> Organizations);

public record OrganizationOption(
    Guid OrganizationId,
    string OrganizationName);

/// <summary>
/// Request to complete OAuth login after selecting an organization.
/// </summary>
public record SelectOrganizationRequest(
    string PendingToken,
    Guid OrganizationId);
