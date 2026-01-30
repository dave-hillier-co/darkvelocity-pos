using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.State;
using Orleans.Runtime;

namespace DarkVelocity.Orleans.Grains;

public class OrganizationGrain : Grain, IOrganizationGrain
{
    private readonly IPersistentState<OrganizationState> _state;

    public OrganizationGrain(
        [PersistentState("organization", "OrleansStorage")]
        IPersistentState<OrganizationState> state)
    {
        _state = state;
    }

    public async Task<OrganizationCreatedResult> CreateAsync(CreateOrganizationCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Organization already exists");

        var orgId = Guid.Parse(this.GetPrimaryKeyString());

        _state.State = new OrganizationState
        {
            Id = orgId,
            Name = command.Name,
            Slug = command.Slug,
            Status = OrganizationStatus.Active,
            Settings = command.Settings ?? new OrganizationSettings(),
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();

        return new OrganizationCreatedResult(orgId, command.Slug, _state.State.CreatedAt);
    }

    public async Task<OrganizationUpdatedResult> UpdateAsync(UpdateOrganizationCommand command)
    {
        EnsureExists();

        if (command.Name != null)
            _state.State.Name = command.Name;

        if (command.Settings != null)
            _state.State.Settings = command.Settings;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new OrganizationUpdatedResult(_state.State.Version, _state.State.UpdatedAt.Value);
    }

    public Task<OrganizationState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task SuspendAsync(string reason)
    {
        EnsureExists();

        _state.State.Status = OrganizationStatus.Suspended;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task ReactivateAsync()
    {
        EnsureExists();

        if (_state.State.Status != OrganizationStatus.Suspended)
            throw new InvalidOperationException("Organization is not suspended");

        _state.State.Status = OrganizationStatus.Active;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task<Guid> AddSiteAsync(Guid siteId)
    {
        EnsureExists();

        if (!_state.State.SiteIds.Contains(siteId))
        {
            _state.State.SiteIds.Add(siteId);
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }

        return siteId;
    }

    public async Task RemoveSiteAsync(Guid siteId)
    {
        EnsureExists();

        if (_state.State.SiteIds.Remove(siteId))
        {
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<IReadOnlyList<Guid>> GetSiteIdsAsync()
    {
        return Task.FromResult<IReadOnlyList<Guid>>(_state.State.SiteIds);
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.Id != Guid.Empty);
    }

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Organization does not exist");
    }
}
