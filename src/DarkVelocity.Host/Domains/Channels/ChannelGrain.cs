using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Status Mapping Grain Implementation
// ============================================================================

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class StatusMappingGrain : JournaledGrain<StatusMappingState, IStatusMappingEvent>, IStatusMappingGrain
{
    protected override void TransitionState(StatusMappingState state, IStatusMappingEvent @event)
    {
        switch (@event)
        {
            case StatusMappingConfigured e:
                state.OrgId = e.OrgId;
                state.MappingId = e.MappingId;
                state.PlatformType = e.PlatformType;
                state.Mappings = e.Mappings.Select(m => new StatusMappingEntryState
                {
                    ExternalStatusCode = m.ExternalStatusCode,
                    ExternalStatusName = m.ExternalStatusName,
                    InternalStatus = m.InternalStatus,
                    TriggersPosAction = m.TriggersPosAction,
                    PosActionType = m.PosActionType
                }).ToList();
                state.ConfiguredAt = e.OccurredAt.UtcDateTime;
                break;

            case StatusMappingEntryAdded e:
                state.Mappings.RemoveAll(m => m.ExternalStatusCode == e.ExternalStatusCode);
                state.Mappings.Add(new StatusMappingEntryState
                {
                    ExternalStatusCode = e.ExternalStatusCode,
                    ExternalStatusName = e.ExternalStatusName,
                    InternalStatus = e.InternalStatus,
                    TriggersPosAction = e.TriggersPosAction,
                    PosActionType = e.PosActionType
                });
                break;

            case StatusMappingEntryRemoved e:
                state.Mappings.RemoveAll(m => m.ExternalStatusCode == e.ExternalStatusCode);
                break;

            case StatusMappingUsageRecorded e:
                state.LastUsedAt = e.OccurredAt.UtcDateTime;
                break;
        }
    }

    public async Task<StatusMappingSnapshot> ConfigureAsync(ConfigureStatusMappingCommand command)
    {
        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var mappingId = Guid.NewGuid();

        RaiseEvent(new StatusMappingConfigured(
            MappingId: mappingId,
            OrgId: orgId,
            PlatformType: command.PlatformType,
            Mappings: command.Mappings.Select(m => new StatusMappingEntryData(
                ExternalStatusCode: m.ExternalStatusCode,
                ExternalStatusName: m.ExternalStatusName,
                InternalStatus: m.InternalStatus,
                TriggersPosAction: m.TriggersPosAction,
                PosActionType: m.PosActionType
            )).ToList(),
            OccurredAt: DateTimeOffset.UtcNow
        ));
        await ConfirmEvents();

        return CreateSnapshot();
    }

    public async Task AddMappingAsync(StatusMappingEntry mapping)
    {
        EnsureExists();

        RaiseEvent(new StatusMappingEntryAdded(
            MappingId: State.MappingId,
            ExternalStatusCode: mapping.ExternalStatusCode,
            ExternalStatusName: mapping.ExternalStatusName,
            InternalStatus: mapping.InternalStatus,
            TriggersPosAction: mapping.TriggersPosAction,
            PosActionType: mapping.PosActionType,
            OccurredAt: DateTimeOffset.UtcNow
        ));
        await ConfirmEvents();
    }

    public async Task RemoveMappingAsync(string externalStatusCode)
    {
        EnsureExists();

        var exists = State.Mappings.Any(m => m.ExternalStatusCode == externalStatusCode);
        if (exists)
        {
            RaiseEvent(new StatusMappingEntryRemoved(
                MappingId: State.MappingId,
                ExternalStatusCode: externalStatusCode,
                OccurredAt: DateTimeOffset.UtcNow
            ));
            await ConfirmEvents();
        }
    }

    public Task<InternalOrderStatus?> GetInternalStatusAsync(string externalStatusCode)
    {
        var mapping = State.Mappings.FirstOrDefault(m => m.ExternalStatusCode == externalStatusCode);
        return Task.FromResult(mapping != null ? (InternalOrderStatus?)mapping.InternalStatus : null);
    }

    public Task<string?> GetExternalStatusAsync(InternalOrderStatus internalStatus)
    {
        var mapping = State.Mappings.FirstOrDefault(m => m.InternalStatus == internalStatus);
        return Task.FromResult(mapping?.ExternalStatusCode);
    }

    public Task<StatusMappingSnapshot> GetSnapshotAsync()
    {
        return Task.FromResult(CreateSnapshot());
    }

    public async Task RecordUsageAsync(string externalStatusCode)
    {
        RaiseEvent(new StatusMappingUsageRecorded(
            MappingId: State.MappingId,
            ExternalStatusCode: externalStatusCode,
            OccurredAt: DateTimeOffset.UtcNow
        ));
        await ConfirmEvents();
    }

    public Task<int> GetVersionAsync() => Task.FromResult(Version);

    private StatusMappingSnapshot CreateSnapshot() => new(
        MappingId: State.MappingId,
        PlatformType: State.PlatformType,
        Mappings: State.Mappings.Select(m => new StatusMappingEntry(
            ExternalStatusCode: m.ExternalStatusCode,
            ExternalStatusName: m.ExternalStatusName,
            InternalStatus: m.InternalStatus,
            TriggersPosAction: m.TriggersPosAction,
            PosActionType: m.PosActionType
        )).ToList(),
        ConfiguredAt: State.ConfiguredAt,
        LastUsedAt: State.LastUsedAt
    );

    private void EnsureExists()
    {
        if (State.MappingId == Guid.Empty)
            throw new InvalidOperationException("Status mapping not configured");
    }
}

// ============================================================================
// Channel Grain Implementation
// ============================================================================

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class ChannelGrain : JournaledGrain<ChannelState, IChannelEvent>, IChannelGrain
{
    protected override void TransitionState(ChannelState state, IChannelEvent @event)
    {
        switch (@event)
        {
            case ChannelConnected e:
                state.OrgId = e.OrgId;
                state.ChannelId = e.ChannelId;
                state.PlatformType = e.PlatformType;
                state.IntegrationType = e.IntegrationType;
                state.Name = e.Name;
                state.Status = ChannelStatus.Active;
                state.ApiCredentialsEncrypted = e.ApiCredentialsEncrypted;
                state.WebhookSecret = e.WebhookSecret;
                state.ExternalChannelId = e.ExternalChannelId;
                state.Settings = e.Settings;
                state.ConnectedAt = e.OccurredAt.UtcDateTime;
                state.TodayDate = e.OccurredAt.UtcDateTime.Date;
                break;

            case ChannelUpdated e:
                if (e.Name != null) state.Name = e.Name;
                if (e.ApiCredentialsEncrypted != null) state.ApiCredentialsEncrypted = e.ApiCredentialsEncrypted;
                if (e.WebhookSecret != null) state.WebhookSecret = e.WebhookSecret;
                if (e.Settings != null) state.Settings = e.Settings;
                break;

            case ChannelDisconnected:
                state.Status = ChannelStatus.Disconnected;
                break;

            case ChannelPaused e:
                state.Status = ChannelStatus.Paused;
                state.PauseReason = e.Reason;
                break;

            case ChannelResumed:
                state.Status = ChannelStatus.Active;
                state.PauseReason = null;
                break;

            case ChannelLocationMappingAdded e:
                state.Locations.RemoveAll(l => l.LocationId == e.LocationId);
                state.Locations.Add(new ChannelLocationState
                {
                    LocationId = e.LocationId,
                    ExternalStoreId = e.ExternalStoreId,
                    IsActive = e.IsActive,
                    MenuId = e.MenuId,
                    OperatingHoursOverride = e.OperatingHoursOverride
                });
                break;

            case ChannelLocationMappingRemoved e:
                state.Locations.RemoveAll(l => l.LocationId == e.LocationId);
                break;

            case ChannelOrderRecorded e:
                state.TotalOrdersToday++;
                state.TotalRevenueToday += e.Amount;
                state.LastOrderAt = e.OccurredAt.UtcDateTime;
                break;

            case ChannelSyncRecorded e:
                state.LastSyncAt = e.OccurredAt.UtcDateTime;
                break;

            case ChannelHeartbeatRecorded e:
                state.LastHeartbeatAt = e.OccurredAt.UtcDateTime;
                break;

            case ChannelErrorRecorded e:
                state.LastErrorMessage = e.ErrorMessage;
                state.Status = ChannelStatus.Error;
                break;

            case ChannelDailyCountersReset e:
                state.TodayDate = e.NewDate.ToDateTime(TimeOnly.MinValue);
                state.TotalOrdersToday = 0;
                state.TotalRevenueToday = 0;
                break;
        }
    }

    public async Task<ChannelSnapshot> ConnectAsync(ConnectChannelCommand command)
    {
        if (State.ChannelId != Guid.Empty)
            throw new InvalidOperationException("Channel already connected");

        var (orgId, _, channelId) = GrainKeys.ParseOrgEntity(this.GetPrimaryKeyString());

        RaiseEvent(new ChannelConnected(
            ChannelId: channelId,
            OrgId: orgId,
            Name: command.Name,
            PlatformType: command.PlatformType,
            IntegrationType: command.IntegrationType,
            ExternalChannelId: command.ExternalChannelId,
            ApiCredentialsEncrypted: command.ApiCredentialsEncrypted,
            WebhookSecret: command.WebhookSecret,
            Settings: command.Settings,
            OccurredAt: DateTimeOffset.UtcNow
        ));
        await ConfirmEvents();

        return CreateSnapshot();
    }

    public async Task<ChannelSnapshot> UpdateAsync(UpdateChannelCommand command)
    {
        EnsureExists();

        RaiseEvent(new ChannelUpdated(
            ChannelId: State.ChannelId,
            Name: command.Name,
            ApiCredentialsEncrypted: command.ApiCredentialsEncrypted,
            WebhookSecret: command.WebhookSecret,
            Settings: command.Settings,
            OccurredAt: DateTimeOffset.UtcNow
        ));
        await ConfirmEvents();

        return CreateSnapshot();
    }

    public async Task DisconnectAsync()
    {
        EnsureExists();

        RaiseEvent(new ChannelDisconnected(
            ChannelId: State.ChannelId,
            OccurredAt: DateTimeOffset.UtcNow
        ));
        await ConfirmEvents();
    }

    public async Task PauseAsync(string? reason = null)
    {
        EnsureExists();

        RaiseEvent(new ChannelPaused(
            ChannelId: State.ChannelId,
            Reason: reason,
            OccurredAt: DateTimeOffset.UtcNow
        ));
        await ConfirmEvents();
    }

    public async Task ResumeAsync()
    {
        EnsureExists();

        RaiseEvent(new ChannelResumed(
            ChannelId: State.ChannelId,
            OccurredAt: DateTimeOffset.UtcNow
        ));
        await ConfirmEvents();
    }

    public async Task AddLocationMappingAsync(ChannelLocationMapping mapping)
    {
        EnsureExists();

        RaiseEvent(new ChannelLocationMappingAdded(
            ChannelId: State.ChannelId,
            LocationId: mapping.LocationId,
            ExternalStoreId: mapping.ExternalStoreId,
            IsActive: mapping.IsActive,
            MenuId: mapping.MenuId,
            OperatingHoursOverride: mapping.OperatingHoursOverride,
            OccurredAt: DateTimeOffset.UtcNow
        ));
        await ConfirmEvents();
    }

    public async Task RemoveLocationMappingAsync(Guid locationId)
    {
        EnsureExists();

        var exists = State.Locations.Any(l => l.LocationId == locationId);
        if (exists)
        {
            RaiseEvent(new ChannelLocationMappingRemoved(
                ChannelId: State.ChannelId,
                LocationId: locationId,
                OccurredAt: DateTimeOffset.UtcNow
            ));
            await ConfirmEvents();
        }
    }

    public Task<ChannelSnapshot> GetSnapshotAsync()
    {
        return Task.FromResult(CreateSnapshot());
    }

    public async Task RecordOrderAsync(decimal orderTotal)
    {
        EnsureExists();
        await ResetDailyCountersIfNeededAsync();

        RaiseEvent(new ChannelOrderRecorded(
            ChannelId: State.ChannelId,
            Amount: orderTotal,
            OccurredAt: DateTimeOffset.UtcNow
        ));
        await ConfirmEvents();
    }

    public async Task RecordSyncAsync()
    {
        EnsureExists();

        RaiseEvent(new ChannelSyncRecorded(
            ChannelId: State.ChannelId,
            OccurredAt: DateTimeOffset.UtcNow
        ));
        await ConfirmEvents();
    }

    public async Task RecordHeartbeatAsync()
    {
        EnsureExists();

        RaiseEvent(new ChannelHeartbeatRecorded(
            ChannelId: State.ChannelId,
            OccurredAt: DateTimeOffset.UtcNow
        ));
        await ConfirmEvents();
    }

    public async Task RecordErrorAsync(string errorMessage)
    {
        EnsureExists();

        RaiseEvent(new ChannelErrorRecorded(
            ChannelId: State.ChannelId,
            ErrorMessage: errorMessage,
            OccurredAt: DateTimeOffset.UtcNow
        ));
        await ConfirmEvents();
    }

    public Task<bool> IsAcceptingOrdersAsync()
    {
        return Task.FromResult(State.ChannelId != Guid.Empty && State.Status == ChannelStatus.Active);
    }

    public Task<int> GetVersionAsync() => Task.FromResult(Version);

    private ChannelSnapshot CreateSnapshot() => new(
        ChannelId: State.ChannelId,
        PlatformType: State.PlatformType,
        IntegrationType: State.IntegrationType,
        Name: State.Name,
        Status: State.Status,
        ExternalChannelId: State.ExternalChannelId,
        ConnectedAt: State.ConnectedAt,
        LastSyncAt: State.LastSyncAt,
        LastOrderAt: State.LastOrderAt,
        LastHeartbeatAt: State.LastHeartbeatAt,
        Locations: State.Locations.Select(l => new ChannelLocationMapping(
            LocationId: l.LocationId,
            ExternalStoreId: l.ExternalStoreId,
            IsActive: l.IsActive,
            MenuId: l.MenuId,
            OperatingHoursOverride: l.OperatingHoursOverride
        )).ToList(),
        TotalOrdersToday: State.TotalOrdersToday,
        TotalRevenueToday: State.TotalRevenueToday,
        LastErrorMessage: State.LastErrorMessage
    );

    private void EnsureExists()
    {
        if (State.ChannelId == Guid.Empty)
            throw new InvalidOperationException("Channel not connected");
    }

    private async Task ResetDailyCountersIfNeededAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (State.TodayDate != today.ToDateTime(TimeOnly.MinValue))
        {
            RaiseEvent(new ChannelDailyCountersReset(
                ChannelId: State.ChannelId,
                NewDate: today,
                OccurredAt: DateTimeOffset.UtcNow
            ));
            await ConfirmEvents();
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
