using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record CreateUserCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] string Email,
    [property: Id(2)] string DisplayName,
    [property: Id(3)] UserType Type = UserType.Employee,
    [property: Id(4)] string? FirstName = null,
    [property: Id(5)] string? LastName = null);

[GenerateSerializer]
public record UpdateUserCommand(
    [property: Id(0)] string? DisplayName = null,
    [property: Id(1)] string? FirstName = null,
    [property: Id(2)] string? LastName = null,
    [property: Id(3)] UserPreferences? Preferences = null);

[GenerateSerializer]
public record UserCreatedResult([property: Id(0)] Guid Id, [property: Id(1)] string Email, [property: Id(2)] DateTime CreatedAt);
[GenerateSerializer]
public record UserUpdatedResult([property: Id(0)] int Version, [property: Id(1)] DateTime UpdatedAt);
[GenerateSerializer]
public record AuthResult([property: Id(0)] bool Success, [property: Id(1)] string? Error = null);

public interface IUserGrain : IGrainWithStringKey
{
    Task<UserCreatedResult> CreateAsync(CreateUserCommand command);
    Task<UserUpdatedResult> UpdateAsync(UpdateUserCommand command);
    Task<UserState> GetStateAsync();
    Task SetPinAsync(string pin);
    Task<AuthResult> VerifyPinAsync(string pin);
    Task GrantSiteAccessAsync(Guid siteId);
    Task RevokeSiteAccessAsync(Guid siteId);
    Task<bool> HasSiteAccessAsync(Guid siteId);
    Task AddToGroupAsync(Guid groupId);
    Task RemoveFromGroupAsync(Guid groupId);
    Task ActivateAsync();
    Task DeactivateAsync();
    Task LockAsync(string reason);
    Task UnlockAsync();
    Task RecordLoginAsync(string? oauthProvider = null, string? oauthEmail = null);
    Task<bool> ExistsAsync();

    /// <summary>
    /// Links an external OAuth identity to this user.
    /// </summary>
    Task LinkExternalIdentityAsync(string provider, string externalId, string? email);

    /// <summary>
    /// Unlinks an external OAuth identity from this user.
    /// </summary>
    Task UnlinkExternalIdentityAsync(string provider);

    /// <summary>
    /// Gets all linked external identities for this user.
    /// </summary>
    Task<Dictionary<string, string>> GetExternalIdsAsync();

    /// <summary>
    /// Converts UserType to role strings for JWT claims.
    /// </summary>
    Task<IReadOnlyList<string>> GetRolesAsync();
}

[GenerateSerializer]
public record CreateUserGroupCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] string Name,
    [property: Id(2)] string? Description = null);

[GenerateSerializer]
public record UserGroupCreatedResult([property: Id(0)] Guid Id, [property: Id(1)] DateTime CreatedAt);

public interface IUserGroupGrain : IGrainWithStringKey
{
    Task<UserGroupCreatedResult> CreateAsync(CreateUserGroupCommand command);
    Task<UserGroupState> GetStateAsync();
    Task UpdateAsync(string? name, string? description);
    Task AddMemberAsync(Guid userId);
    Task RemoveMemberAsync(Guid userId);
    Task<IReadOnlyList<Guid>> GetMembersAsync();
    Task<bool> HasMemberAsync(Guid userId);
    Task<bool> ExistsAsync();
}
