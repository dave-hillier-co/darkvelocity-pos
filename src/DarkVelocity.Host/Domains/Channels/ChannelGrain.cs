using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Status Mapping Grain Implementation
// ============================================================================

public class StatusMappingGrain : Grain, IStatusMappingGrain
{
    private readonly IPersistentState<StatusMappingState> _state;

    public StatusMappingGrain(
        [PersistentState("statusmapping", "OrleansStorage")]
        IPersistentState<StatusMappingState> state)
    {
        _state = state;
    }

    public async Task<StatusMappingSnapshot> ConfigureAsync(ConfigureStatusMappingCommand command)
    {
        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State = new StatusMappingState
        {
            OrgId = orgId,
            MappingId = Guid.NewGuid(),
            PlatformType = command.PlatformType,
            Mappings = command.Mappings.Select(m => new StatusMappingEntryState
            {
                ExternalStatusCode = m.ExternalStatusCode,
                ExternalStatusName = m.ExternalStatusName,
                InternalStatus = m.InternalStatus,
                TriggersPosAction = m.TriggersPosAction,
                PosActionType = m.PosActionType
            }).ToList(),
            ConfiguredAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task AddMappingAsync(StatusMappingEntry mapping)
    {
        EnsureExists();

        // Remove existing mapping for this status code if present
        _state.State.Mappings.RemoveAll(m => m.ExternalStatusCode == mapping.ExternalStatusCode);

        _state.State.Mappings.Add(new StatusMappingEntryState
        {
            ExternalStatusCode = mapping.ExternalStatusCode,
            ExternalStatusName = mapping.ExternalStatusName,
            InternalStatus = mapping.InternalStatus,
            TriggersPosAction = mapping.TriggersPosAction,
            PosActionType = mapping.PosActionType
        });

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RemoveMappingAsync(string externalStatusCode)
    {
        EnsureExists();

        var removed = _state.State.Mappings.RemoveAll(m => m.ExternalStatusCode == externalStatusCode);
        if (removed > 0)
        {
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<InternalOrderStatus?> GetInternalStatusAsync(string externalStatusCode)
    {
        var mapping = _state.State.Mappings.FirstOrDefault(m => m.ExternalStatusCode == externalStatusCode);
        return Task.FromResult(mapping != null ? (InternalOrderStatus?)mapping.InternalStatus : null);
    }

    public Task<string?> GetExternalStatusAsync(InternalOrderStatus internalStatus)
    {
        var mapping = _state.State.Mappings.FirstOrDefault(m => m.InternalStatus == internalStatus);
        return Task.FromResult(mapping?.ExternalStatusCode);
    }

    public Task<StatusMappingSnapshot> GetSnapshotAsync()
    {
        return Task.FromResult(CreateSnapshot());
    }

    public async Task RecordUsageAsync(string externalStatusCode)
    {
        _state.State.LastUsedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    private StatusMappingSnapshot CreateSnapshot() => new(
        MappingId: _state.State.MappingId,
        PlatformType: _state.State.PlatformType,
        Mappings: _state.State.Mappings.Select(m => new StatusMappingEntry(
            ExternalStatusCode: m.ExternalStatusCode,
            ExternalStatusName: m.ExternalStatusName,
            InternalStatus: m.InternalStatus,
            TriggersPosAction: m.TriggersPosAction,
            PosActionType: m.PosActionType
        )).ToList(),
        ConfiguredAt: _state.State.ConfiguredAt,
        LastUsedAt: _state.State.LastUsedAt
    );

    private void EnsureExists()
    {
        if (_state.State.MappingId == Guid.Empty)
            throw new InvalidOperationException("Status mapping not configured");
    }
}

// ============================================================================
// Channel Grain Implementation
// ============================================================================

public class ChannelGrain : Grain, IChannelGrain
{
    private readonly IPersistentState<ChannelState> _state;

    public ChannelGrain(
        [PersistentState("channel", "OrleansStorage")]
        IPersistentState<ChannelState> state)
    {
        _state = state;
    }

    public async Task<ChannelSnapshot> ConnectAsync(ConnectChannelCommand command)
    {
        if (_state.State.ChannelId != Guid.Empty)
            throw new InvalidOperationException("Channel already connected");

        var (orgId, _, channelId) = GrainKeys.ParseOrgEntity(this.GetPrimaryKeyString());

        _state.State = new ChannelState
        {
            OrgId = orgId,
            ChannelId = channelId,
            PlatformType = command.PlatformType,
            IntegrationType = command.IntegrationType,
            Name = command.Name,
            Status = ChannelStatus.Active,
            ApiCredentialsEncrypted = command.ApiCredentialsEncrypted,
            WebhookSecret = command.WebhookSecret,
            ExternalChannelId = command.ExternalChannelId,
            Settings = command.Settings,
            ConnectedAt = DateTime.UtcNow,
            TodayDate = DateTime.UtcNow.Date,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<ChannelSnapshot> UpdateAsync(UpdateChannelCommand command)
    {
        EnsureExists();

        if (command.Name != null) _state.State.Name = command.Name;
        if (command.Status != null) _state.State.Status = command.Status.Value;
        if (command.ApiCredentialsEncrypted != null) _state.State.ApiCredentialsEncrypted = command.ApiCredentialsEncrypted;
        if (command.WebhookSecret != null) _state.State.WebhookSecret = command.WebhookSecret;
        if (command.Settings != null) _state.State.Settings = command.Settings;

        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task DisconnectAsync()
    {
        EnsureExists();
        _state.State.Status = ChannelStatus.Disconnected;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task PauseAsync(string? reason = null)
    {
        EnsureExists();
        _state.State.Status = ChannelStatus.Paused;
        _state.State.PauseReason = reason;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task ResumeAsync()
    {
        EnsureExists();
        _state.State.Status = ChannelStatus.Active;
        _state.State.PauseReason = null;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task AddLocationMappingAsync(ChannelLocationMapping mapping)
    {
        EnsureExists();

        // Remove existing mapping for this location if present
        _state.State.Locations.RemoveAll(l => l.LocationId == mapping.LocationId);

        _state.State.Locations.Add(new ChannelLocationState
        {
            LocationId = mapping.LocationId,
            ExternalStoreId = mapping.ExternalStoreId,
            IsActive = mapping.IsActive,
            MenuId = mapping.MenuId,
            OperatingHoursOverride = mapping.OperatingHoursOverride
        });

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RemoveLocationMappingAsync(Guid locationId)
    {
        EnsureExists();

        var removed = _state.State.Locations.RemoveAll(l => l.LocationId == locationId);
        if (removed > 0)
        {
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<ChannelSnapshot> GetSnapshotAsync()
    {
        return Task.FromResult(CreateSnapshot());
    }

    public async Task RecordOrderAsync(decimal orderTotal)
    {
        EnsureExists();
        ResetDailyCountersIfNeeded();

        _state.State.TotalOrdersToday++;
        _state.State.TotalRevenueToday += orderTotal;
        _state.State.LastOrderAt = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task RecordSyncAsync()
    {
        EnsureExists();
        _state.State.LastSyncAt = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordHeartbeatAsync()
    {
        EnsureExists();
        _state.State.LastHeartbeatAt = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordErrorAsync(string errorMessage)
    {
        EnsureExists();
        _state.State.LastErrorMessage = errorMessage;
        _state.State.Status = ChannelStatus.Error;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<bool> IsAcceptingOrdersAsync()
    {
        return Task.FromResult(_state.State.ChannelId != Guid.Empty && _state.State.Status == ChannelStatus.Active);
    }

    private ChannelSnapshot CreateSnapshot() => new(
        ChannelId: _state.State.ChannelId,
        PlatformType: _state.State.PlatformType,
        IntegrationType: _state.State.IntegrationType,
        Name: _state.State.Name,
        Status: _state.State.Status,
        ExternalChannelId: _state.State.ExternalChannelId,
        ConnectedAt: _state.State.ConnectedAt,
        LastSyncAt: _state.State.LastSyncAt,
        LastOrderAt: _state.State.LastOrderAt,
        LastHeartbeatAt: _state.State.LastHeartbeatAt,
        Locations: _state.State.Locations.Select(l => new ChannelLocationMapping(
            LocationId: l.LocationId,
            ExternalStoreId: l.ExternalStoreId,
            IsActive: l.IsActive,
            MenuId: l.MenuId,
            OperatingHoursOverride: l.OperatingHoursOverride
        )).ToList(),
        TotalOrdersToday: _state.State.TotalOrdersToday,
        TotalRevenueToday: _state.State.TotalRevenueToday,
        LastErrorMessage: _state.State.LastErrorMessage
    );

    private void EnsureExists()
    {
        if (_state.State.ChannelId == Guid.Empty)
            throw new InvalidOperationException("Channel not connected");
    }

    private void ResetDailyCountersIfNeeded()
    {
        var today = DateTime.UtcNow.Date;
        if (_state.State.TodayDate != today)
        {
            _state.State.TodayDate = today;
            _state.State.TotalOrdersToday = 0;
            _state.State.TotalRevenueToday = 0;
        }
    }
}

// ============================================================================
// Channel Registry Grain Implementation
// ============================================================================

public class ChannelRegistryGrain : Grain, IChannelRegistryGrain
{
    private readonly IPersistentState<ChannelRegistryState> _state;

    public ChannelRegistryGrain(
        [PersistentState("channelregistry", "OrleansStorage")]
        IPersistentState<ChannelRegistryState> state)
    {
        _state = state;
    }

    public async Task RegisterChannelAsync(Guid channelId, DeliveryPlatformType platformType, IntegrationType integrationType, string name)
    {
        // Remove existing if re-registering
        _state.State.Channels.RemoveAll(c => c.ChannelId == channelId);

        _state.State.Channels.Add(new RegisteredChannelState
        {
            ChannelId = channelId,
            PlatformType = platformType,
            IntegrationType = integrationType,
            Name = name,
            Status = ChannelStatus.Active
        });

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task UnregisterChannelAsync(Guid channelId)
    {
        var removed = _state.State.Channels.RemoveAll(c => c.ChannelId == channelId);
        if (removed > 0)
        {
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task UpdateChannelStatusAsync(Guid channelId, ChannelStatus status)
    {
        var channel = _state.State.Channels.FirstOrDefault(c => c.ChannelId == channelId);
        if (channel != null)
        {
            channel.Status = status;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<IReadOnlyList<ChannelSummary>> GetAllChannelsAsync()
    {
        return Task.FromResult<IReadOnlyList<ChannelSummary>>(
            _state.State.Channels.Select(CreateSummary).ToList());
    }

    public Task<IReadOnlyList<ChannelSummary>> GetChannelsByTypeAsync(IntegrationType integrationType)
    {
        return Task.FromResult<IReadOnlyList<ChannelSummary>>(
            _state.State.Channels
                .Where(c => c.IntegrationType == integrationType)
                .Select(CreateSummary)
                .ToList());
    }

    public Task<IReadOnlyList<ChannelSummary>> GetChannelsByPlatformAsync(DeliveryPlatformType platformType)
    {
        return Task.FromResult<IReadOnlyList<ChannelSummary>>(
            _state.State.Channels
                .Where(c => c.PlatformType == platformType)
                .Select(CreateSummary)
                .ToList());
    }

    public Task<IReadOnlyList<ChannelSummary>> GetChannelsForLocationAsync(Guid locationId)
    {
        return Task.FromResult<IReadOnlyList<ChannelSummary>>(
            _state.State.Channels
                .Where(c => c.LocationIds.Contains(locationId))
                .Select(CreateSummary)
                .ToList());
    }

    private static ChannelSummary CreateSummary(RegisteredChannelState c) => new(
        ChannelId: c.ChannelId,
        PlatformType: c.PlatformType,
        IntegrationType: c.IntegrationType,
        Name: c.Name,
        Status: c.Status,
        LocationCount: c.LocationIds.Count,
        LastOrderAt: c.LastOrderAt
    );
}
