using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

public class SiteGrain : Grain, ISiteGrain
{
    private readonly IPersistentState<SiteState> _state;
    private readonly IGrainFactory _grainFactory;

    public SiteGrain(
        [PersistentState("site", "OrleansStorage")]
        IPersistentState<SiteState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public async Task<SiteCreatedResult> CreateAsync(CreateSiteCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Site already exists");

        var key = this.GetPrimaryKeyString();
        var (orgId, siteId) = ParseKey(key);

        _state.State = new SiteState
        {
            Id = siteId,
            OrganizationId = orgId,
            Name = command.Name,
            Code = command.Code,
            Address = command.Address,
            Timezone = command.Timezone,
            Currency = command.Currency,
            Status = SiteStatus.Open,
            Settings = new SiteSettings(),
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();

        // Register with organization
        var orgGrain = _grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
        await orgGrain.AddSiteAsync(siteId);

        return new SiteCreatedResult(siteId, command.Code, _state.State.CreatedAt);
    }

    public async Task<SiteUpdatedResult> UpdateAsync(UpdateSiteCommand command)
    {
        EnsureExists();

        if (command.Name != null)
            _state.State.Name = command.Name;

        if (command.Address != null)
            _state.State.Address = command.Address;

        if (command.OperatingHours != null)
            _state.State.OperatingHours = command.OperatingHours;

        if (command.Settings != null)
            _state.State.Settings = command.Settings;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new SiteUpdatedResult(_state.State.Version, _state.State.UpdatedAt.Value);
    }

    public Task<SiteState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task OpenAsync()
    {
        EnsureExists();

        _state.State.Status = SiteStatus.Open;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task CloseAsync()
    {
        EnsureExists();

        _state.State.Status = SiteStatus.Closed;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task CloseTemporarilyAsync(string reason)
    {
        EnsureExists();

        _state.State.Status = SiteStatus.TemporarilyClosed;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task SetActiveMenuAsync(Guid menuId)
    {
        EnsureExists();

        _state.State.Settings = _state.State.Settings with { ActiveMenuId = menuId };
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task AddFloorAsync(Guid floorId)
    {
        EnsureExists();

        if (!_state.State.FloorIds.Contains(floorId))
        {
            _state.State.FloorIds.Add(floorId);
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveFloorAsync(Guid floorId)
    {
        EnsureExists();

        if (_state.State.FloorIds.Remove(floorId))
        {
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task AddStationAsync(Guid stationId)
    {
        EnsureExists();

        if (!_state.State.StationIds.Contains(stationId))
        {
            _state.State.StationIds.Add(stationId);
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveStationAsync(Guid stationId)
    {
        EnsureExists();

        if (_state.State.StationIds.Remove(stationId))
        {
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.Id != Guid.Empty);
    }

    public Task<bool> IsOpenAsync()
    {
        return Task.FromResult(_state.State.Status == SiteStatus.Open);
    }

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Site does not exist");
    }

    private static (Guid OrgId, Guid SiteId) ParseKey(string key)
    {
        var parts = key.Split(':');
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid site key format: {key}");
        return (Guid.Parse(parts[0]), Guid.Parse(parts[1]));
    }
}
