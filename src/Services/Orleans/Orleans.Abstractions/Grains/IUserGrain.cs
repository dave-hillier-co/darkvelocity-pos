using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Grains;

public record CreateUserCommand(
    Guid OrganizationId,
    string Email,
    string DisplayName,
    UserType Type = UserType.Employee,
    string? FirstName = null,
    string? LastName = null);

public record UpdateUserCommand(
    string? DisplayName = null,
    string? FirstName = null,
    string? LastName = null,
    UserPreferences? Preferences = null);

public record UserCreatedResult(Guid Id, string Email, DateTime CreatedAt);
public record UserUpdatedResult(int Version, DateTime UpdatedAt);
public record AuthResult(bool Success, string? Error = null);

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
    Task RecordLoginAsync();
    Task<bool> ExistsAsync();
}

public record CreateUserGroupCommand(
    Guid OrganizationId,
    string Name,
    string? Description = null);

public record UserGroupCreatedResult(Guid Id, DateTime CreatedAt);

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
