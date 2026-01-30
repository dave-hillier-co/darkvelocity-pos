using DarkVelocity.Orleans.Abstractions;
using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.State;
using Orleans.Runtime;
using System.Security.Cryptography;
using System.Text;

namespace DarkVelocity.Orleans.Grains;

public class UserGrain : Grain, IUserGrain
{
    private readonly IPersistentState<UserState> _state;

    public UserGrain(
        [PersistentState("user", "OrleansStorage")]
        IPersistentState<UserState> state)
    {
        _state = state;
    }

    public async Task<UserCreatedResult> CreateAsync(CreateUserCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("User already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, userId) = GrainKeys.ParseOrgEntity(key);

        _state.State = new UserState
        {
            Id = userId,
            OrganizationId = command.OrganizationId,
            Email = command.Email,
            DisplayName = command.DisplayName,
            FirstName = command.FirstName,
            LastName = command.LastName,
            Type = command.Type,
            Status = UserStatus.Active,
            Preferences = new UserPreferences(),
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();

        return new UserCreatedResult(userId, command.Email, _state.State.CreatedAt);
    }

    public async Task<UserUpdatedResult> UpdateAsync(UpdateUserCommand command)
    {
        EnsureExists();

        if (command.DisplayName != null)
            _state.State.DisplayName = command.DisplayName;

        if (command.FirstName != null)
            _state.State.FirstName = command.FirstName;

        if (command.LastName != null)
            _state.State.LastName = command.LastName;

        if (command.Preferences != null)
            _state.State.Preferences = command.Preferences;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new UserUpdatedResult(_state.State.Version, _state.State.UpdatedAt.Value);
    }

    public Task<UserState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task SetPinAsync(string pin)
    {
        EnsureExists();

        _state.State.PinHash = HashPin(pin);
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<AuthResult> VerifyPinAsync(string pin)
    {
        EnsureExists();

        if (_state.State.Status == UserStatus.Locked)
            return Task.FromResult(new AuthResult(false, "User account is locked"));

        if (string.IsNullOrEmpty(_state.State.PinHash))
            return Task.FromResult(new AuthResult(false, "PIN not set"));

        var hashedInput = HashPin(pin);
        if (hashedInput == _state.State.PinHash)
        {
            _state.State.FailedLoginAttempts = 0;
            return Task.FromResult(new AuthResult(true));
        }

        _state.State.FailedLoginAttempts++;
        return Task.FromResult(new AuthResult(false, "Invalid PIN"));
    }

    public async Task GrantSiteAccessAsync(Guid siteId)
    {
        EnsureExists();

        if (!_state.State.SiteAccess.Contains(siteId))
        {
            _state.State.SiteAccess.Add(siteId);
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task RevokeSiteAccessAsync(Guid siteId)
    {
        EnsureExists();

        if (_state.State.SiteAccess.Remove(siteId))
        {
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<bool> HasSiteAccessAsync(Guid siteId)
    {
        return Task.FromResult(_state.State.SiteAccess.Contains(siteId));
    }

    public async Task AddToGroupAsync(Guid groupId)
    {
        EnsureExists();

        if (!_state.State.UserGroupIds.Contains(groupId))
        {
            _state.State.UserGroupIds.Add(groupId);
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveFromGroupAsync(Guid groupId)
    {
        EnsureExists();

        if (_state.State.UserGroupIds.Remove(groupId))
        {
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task ActivateAsync()
    {
        EnsureExists();

        _state.State.Status = UserStatus.Active;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        EnsureExists();

        _state.State.Status = UserStatus.Inactive;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task LockAsync(string reason)
    {
        EnsureExists();

        _state.State.Status = UserStatus.Locked;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task UnlockAsync()
    {
        EnsureExists();

        _state.State.Status = UserStatus.Active;
        _state.State.FailedLoginAttempts = 0;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RecordLoginAsync()
    {
        EnsureExists();

        _state.State.LastLoginAt = DateTime.UtcNow;
        _state.State.FailedLoginAttempts = 0;

        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.Id != Guid.Empty);
    }

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("User does not exist");
    }

    private static string HashPin(string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
        return Convert.ToBase64String(bytes);
    }
}

public class UserGroupGrain : Grain, IUserGroupGrain
{
    private readonly IPersistentState<UserGroupState> _state;

    public UserGroupGrain(
        [PersistentState("usergroup", "OrleansStorage")]
        IPersistentState<UserGroupState> state)
    {
        _state = state;
    }

    public async Task<UserGroupCreatedResult> CreateAsync(CreateUserGroupCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("User group already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, groupId) = GrainKeys.ParseOrgEntity(key);

        _state.State = new UserGroupState
        {
            Id = groupId,
            OrganizationId = command.OrganizationId,
            Name = command.Name,
            Description = command.Description,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();

        return new UserGroupCreatedResult(groupId, _state.State.CreatedAt);
    }

    public Task<UserGroupState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task UpdateAsync(string? name, string? description)
    {
        EnsureExists();

        if (name != null)
            _state.State.Name = name;

        if (description != null)
            _state.State.Description = description;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task AddMemberAsync(Guid userId)
    {
        EnsureExists();

        if (!_state.State.MemberIds.Contains(userId))
        {
            _state.State.MemberIds.Add(userId);
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveMemberAsync(Guid userId)
    {
        EnsureExists();

        if (_state.State.MemberIds.Remove(userId))
        {
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<IReadOnlyList<Guid>> GetMembersAsync()
    {
        return Task.FromResult<IReadOnlyList<Guid>>(_state.State.MemberIds);
    }

    public Task<bool> HasMemberAsync(Guid userId)
    {
        return Task.FromResult(_state.State.MemberIds.Contains(userId));
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.Id != Guid.Empty);
    }

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("User group does not exist");
    }
}
