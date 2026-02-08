using DarkVelocity.Host.Events;
using DarkVelocity.Host.Extensions;
using DarkVelocity.Host.State;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

public class TableGrain : Grain, ITableGrain
{
    private readonly IPersistentState<TableState> _state;

    public TableGrain(
        [PersistentState("table", "OrleansStorage")]
        IPersistentState<TableState> state)
    {
        _state = state;
    }

    public async Task<TableCreatedResult> CreateAsync(CreateTableCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Table already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, _, tableId) = GrainKeys.ParseSiteEntity(key);

        _state.State = new TableState
        {
            Id = tableId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            Number = command.Number,
            Name = command.Name,
            MinCapacity = command.MinCapacity,
            MaxCapacity = command.MaxCapacity,
            Shape = command.Shape,
            FloorPlanId = command.FloorPlanId,
            Status = TableStatus.Available,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        return new TableCreatedResult(tableId, command.Number, _state.State.CreatedAt);
    }

    public Task<TableState> GetStateAsync() => Task.FromResult(_state.State);

    public async Task UpdateAsync(UpdateTableCommand command)
    {
        EnsureExists();

        if (command.Number != null) _state.State.Number = command.Number;
        if (command.Name != null) _state.State.Name = command.Name;
        if (command.MinCapacity.HasValue) _state.State.MinCapacity = command.MinCapacity.Value;
        if (command.MaxCapacity.HasValue) _state.State.MaxCapacity = command.MaxCapacity.Value;
        if (command.Shape.HasValue) _state.State.Shape = command.Shape.Value;
        if (command.Position != null) _state.State.Position = command.Position;
        if (command.IsCombinable.HasValue) _state.State.IsCombinable = command.IsCombinable.Value;
        if (command.SortOrder.HasValue) _state.State.SortOrder = command.SortOrder.Value;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task DeleteAsync()
    {
        EnsureExists();
        await _state.ClearStateAsync();
    }

    public async Task SetStatusAsync(TableStatus status)
    {
        EnsureExists();
        _state.State.Status = status;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SeatAsync(SeatTableCommand command)
    {
        EnsureExists();
        if (_state.State.Status != TableStatus.Available && _state.State.Status != TableStatus.Reserved)
            throw new InvalidOperationException($"Cannot seat at table with status {_state.State.Status}");

        _state.State.Status = TableStatus.Occupied;
        _state.State.CurrentOccupancy = new TableOccupancy
        {
            BookingId = command.BookingId,
            OrderId = command.OrderId,
            GuestName = command.GuestName,
            GuestCount = command.GuestCount,
            SeatedAt = DateTime.UtcNow,
            ServerId = command.ServerId
        };
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task ClearAsync()
    {
        EnsureExists();
        _state.State.Status = TableStatus.Dirty;
        _state.State.CurrentOccupancy = null;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task MarkDirtyAsync()
    {
        EnsureExists();
        _state.State.Status = TableStatus.Dirty;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task MarkCleanAsync()
    {
        EnsureExists();
        if (_state.State.Status != TableStatus.Dirty)
            throw new InvalidOperationException("Table is not dirty");

        _state.State.Status = TableStatus.Available;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task BlockAsync(string? reason = null)
    {
        EnsureExists();
        _state.State.Status = TableStatus.Blocked;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task UnblockAsync()
    {
        EnsureExists();
        if (_state.State.Status != TableStatus.Blocked)
            throw new InvalidOperationException("Table is not blocked");

        _state.State.Status = TableStatus.Available;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task CombineWithAsync(Guid otherTableId)
    {
        EnsureExists();
        if (!_state.State.IsCombinable)
            throw new InvalidOperationException("Table is not combinable");

        if (!_state.State.CombinedWith.Contains(otherTableId))
        {
            _state.State.CombinedWith.Add(otherTableId);
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task UncombineAsync()
    {
        EnsureExists();
        _state.State.CombinedWith.Clear();
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task AddTagAsync(string tag)
    {
        EnsureExists();
        if (_state.State.Tags.TryAddTag(tag))
        {
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveTagAsync(string tag)
    {
        EnsureExists();
        if (_state.State.Tags.Remove(tag))
        {
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task SetPositionAsync(TablePosition position)
    {
        EnsureExists();
        _state.State.Position = position;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetFloorPlanAsync(Guid floorPlanId)
    {
        EnsureExists();
        _state.State.FloorPlanId = floorPlanId;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.Id != Guid.Empty);

    Task<TableStatus> ITableGrain.GetStatusAsync() => Task.FromResult(_state.State.Status);

    public Task<bool> IsAvailableAsync() =>
        Task.FromResult(_state.State.Status == TableStatus.Available);

    public Task<TableOccupancy?> GetOccupancyAsync() => Task.FromResult(_state.State.CurrentOccupancy);

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Table does not exist");
    }
}

/// <summary>
/// Event-sourced grain for floor plan management with full version history.
/// All state changes are recorded as events and can be replayed for audit trail.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class FloorPlanGrain : JournaledGrain<FloorPlanState, IFloorPlanEvent>, IFloorPlanGrain
{
    /// <summary>
    /// Applies domain events to mutate state. This is the core of event sourcing.
    /// </summary>
    protected override void TransitionState(FloorPlanState state, IFloorPlanEvent @event)
    {
        switch (@event)
        {
            case FloorPlanCreated e:
                state.Id = e.FloorPlanId;
                state.OrganizationId = e.OrgId;
                state.SiteId = e.SiteId;
                state.Name = e.Name;
                state.IsDefault = e.IsDefault;
                state.IsActive = true;
                state.Width = e.Width;
                state.Height = e.Height;
                state.CreatedAt = e.OccurredAt.UtcDateTime;
                state.Version = 1;
                break;

            case FloorPlanUpdated e:
                if (e.Name != null) state.Name = e.Name;
                if (e.Width.HasValue) state.Width = e.Width.Value;
                if (e.Height.HasValue) state.Height = e.Height.Value;
                if (e.BackgroundImageUrl != null) state.BackgroundImageUrl = e.BackgroundImageUrl;
                if (e.IsActive.HasValue) state.IsActive = e.IsActive.Value;
                state.UpdatedAt = e.OccurredAt.UtcDateTime;
                state.Version++;
                break;

            case FloorPlanTableAdded e:
                if (!state.TableIds.Contains(e.TableId))
                    state.TableIds.Add(e.TableId);
                state.UpdatedAt = e.OccurredAt.UtcDateTime;
                state.Version++;
                break;

            case FloorPlanTableRemoved e:
                state.TableIds.Remove(e.TableId);
                state.UpdatedAt = e.OccurredAt.UtcDateTime;
                state.Version++;
                break;

            case FloorPlanSectionAdded e:
                state.Sections.Add(new FloorPlanSection
                {
                    Id = e.SectionId,
                    Name = e.Name,
                    Color = e.Color,
                    SortOrder = e.SortOrder
                });
                state.UpdatedAt = e.OccurredAt.UtcDateTime;
                state.Version++;
                break;

            case FloorPlanSectionRemoved e:
                var sectionToRemove = state.Sections.FirstOrDefault(s => s.Id == e.SectionId);
                if (sectionToRemove != null)
                    state.Sections.Remove(sectionToRemove);
                state.UpdatedAt = e.OccurredAt.UtcDateTime;
                state.Version++;
                break;

            case FloorPlanSectionUpdated e:
                var index = state.Sections.FindIndex(s => s.Id == e.SectionId);
                if (index >= 0)
                {
                    var existing = state.Sections[index];
                    state.Sections[index] = existing with
                    {
                        Name = e.Name ?? existing.Name,
                        Color = e.Color ?? existing.Color,
                        SortOrder = e.SortOrder ?? existing.SortOrder
                    };
                }
                state.UpdatedAt = e.OccurredAt.UtcDateTime;
                state.Version++;
                break;

            case FloorPlanDefaultSet e:
                state.IsDefault = true;
                state.UpdatedAt = e.OccurredAt.UtcDateTime;
                state.Version++;
                break;

            case FloorPlanActivated e:
                state.IsActive = true;
                state.UpdatedAt = e.OccurredAt.UtcDateTime;
                state.Version++;
                break;

            case FloorPlanDeactivated e:
                state.IsActive = false;
                state.UpdatedAt = e.OccurredAt.UtcDateTime;
                state.Version++;
                break;
        }
    }

    public async Task<FloorPlanCreatedResult> CreateAsync(CreateFloorPlanCommand command)
    {
        if (State.Id != Guid.Empty)
            throw new InvalidOperationException("Floor plan already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, _, floorPlanId) = GrainKeys.ParseSiteEntity(key);

        RaiseEvent(new FloorPlanCreated(
            FloorPlanId: floorPlanId,
            OccurredAt: DateTimeOffset.UtcNow,
            OrgId: command.OrganizationId,
            SiteId: command.SiteId,
            Name: command.Name,
            IsDefault: command.IsDefault,
            Width: command.Width,
            Height: command.Height
        ));

        await ConfirmEvents();
        return new FloorPlanCreatedResult(floorPlanId, command.Name, State.CreatedAt);
    }

    public Task<FloorPlanState> GetStateAsync() => Task.FromResult(State);

    public async Task UpdateAsync(UpdateFloorPlanCommand command)
    {
        EnsureExists();

        RaiseEvent(new FloorPlanUpdated(
            FloorPlanId: State.Id,
            OccurredAt: DateTimeOffset.UtcNow,
            Name: command.Name,
            Width: command.Width,
            Height: command.Height,
            BackgroundImageUrl: command.BackgroundImageUrl,
            IsActive: command.IsActive
        ));

        await ConfirmEvents();
    }

    public async Task DeleteAsync()
    {
        EnsureExists();

        RaiseEvent(new FloorPlanDeactivated(
            FloorPlanId: State.Id,
            OccurredAt: DateTimeOffset.UtcNow
        ));

        await ConfirmEvents();
    }

    public async Task AddTableAsync(Guid tableId)
    {
        EnsureExists();
        if (!State.TableIds.Contains(tableId))
        {
            RaiseEvent(new FloorPlanTableAdded(
                FloorPlanId: State.Id,
                OccurredAt: DateTimeOffset.UtcNow,
                TableId: tableId
            ));

            await ConfirmEvents();
        }
    }

    public async Task RemoveTableAsync(Guid tableId)
    {
        EnsureExists();
        if (State.TableIds.Contains(tableId))
        {
            RaiseEvent(new FloorPlanTableRemoved(
                FloorPlanId: State.Id,
                OccurredAt: DateTimeOffset.UtcNow,
                TableId: tableId
            ));

            await ConfirmEvents();
        }
    }

    public Task<IReadOnlyList<Guid>> GetTableIdsAsync() =>
        Task.FromResult<IReadOnlyList<Guid>>(State.TableIds);

    public async Task AddSectionAsync(string name, string? color = null)
    {
        EnsureExists();
        var sectionId = Guid.NewGuid();

        RaiseEvent(new FloorPlanSectionAdded(
            FloorPlanId: State.Id,
            OccurredAt: DateTimeOffset.UtcNow,
            SectionId: sectionId,
            Name: name,
            Color: color,
            SortOrder: State.Sections.Count
        ));

        await ConfirmEvents();
    }

    public async Task RemoveSectionAsync(Guid sectionId)
    {
        EnsureExists();
        var section = State.Sections.FirstOrDefault(s => s.Id == sectionId);
        if (section != null)
        {
            RaiseEvent(new FloorPlanSectionRemoved(
                FloorPlanId: State.Id,
                OccurredAt: DateTimeOffset.UtcNow,
                SectionId: sectionId
            ));

            await ConfirmEvents();
        }
    }

    public async Task UpdateSectionAsync(Guid sectionId, string? name = null, string? color = null, int? sortOrder = null)
    {
        EnsureExists();
        var sectionIndex = State.Sections.FindIndex(s => s.Id == sectionId);
        if (sectionIndex >= 0)
        {
            RaiseEvent(new FloorPlanSectionUpdated(
                FloorPlanId: State.Id,
                OccurredAt: DateTimeOffset.UtcNow,
                SectionId: sectionId,
                Name: name,
                Color: color,
                SortOrder: sortOrder
            ));

            await ConfirmEvents();
        }
    }

    public async Task SetDefaultAsync()
    {
        EnsureExists();

        RaiseEvent(new FloorPlanDefaultSet(
            FloorPlanId: State.Id,
            OccurredAt: DateTimeOffset.UtcNow
        ));

        await ConfirmEvents();
    }

    public async Task ActivateAsync()
    {
        EnsureExists();

        RaiseEvent(new FloorPlanActivated(
            FloorPlanId: State.Id,
            OccurredAt: DateTimeOffset.UtcNow
        ));

        await ConfirmEvents();
    }

    public async Task DeactivateAsync()
    {
        EnsureExists();

        RaiseEvent(new FloorPlanDeactivated(
            FloorPlanId: State.Id,
            OccurredAt: DateTimeOffset.UtcNow
        ));

        await ConfirmEvents();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.Id != Guid.Empty);

    public Task<bool> IsActiveAsync() => Task.FromResult(State.IsActive);

    public Task<int> GetVersionAsync() => Task.FromResult(Version);

    private void EnsureExists()
    {
        if (State.Id == Guid.Empty)
            throw new InvalidOperationException("Floor plan does not exist");
    }
}

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class BookingSettingsGrain : JournaledGrain<BookingSettingsState, IBookingSettingsEvent>, IBookingSettingsGrain
{
    protected override void TransitionState(BookingSettingsState state, IBookingSettingsEvent @event)
    {
        switch (@event)
        {
            case BookingSettingsInitialized e:
                state.SiteId = e.SiteId;
                state.OrganizationId = e.OrganizationId;
                break;

            case BookingSettingsUpdated e:
                if (e.DefaultOpenTime.HasValue) state.DefaultOpenTime = e.DefaultOpenTime.Value;
                if (e.DefaultCloseTime.HasValue) state.DefaultCloseTime = e.DefaultCloseTime.Value;
                if (e.DefaultDuration.HasValue) state.DefaultDuration = e.DefaultDuration.Value;
                if (e.SlotInterval.HasValue) state.SlotInterval = e.SlotInterval.Value;
                if (e.MaxPartySizeOnline.HasValue) state.MaxPartySizeOnline = e.MaxPartySizeOnline.Value;
                if (e.MaxBookingsPerSlot.HasValue) state.MaxBookingsPerSlot = e.MaxBookingsPerSlot.Value;
                if (e.AdvanceBookingDays.HasValue) state.AdvanceBookingDays = e.AdvanceBookingDays.Value;
                if (e.RequireDeposit.HasValue) state.RequireDeposit = e.RequireDeposit.Value;
                if (e.DepositAmount.HasValue) state.DepositAmount = e.DepositAmount.Value;
                break;

            case BookingDateBlocked e:
                if (!state.BlockedDates.Contains(e.Date))
                    state.BlockedDates.Add(e.Date);
                break;

            case BookingDateUnblocked e:
                state.BlockedDates.Remove(e.Date);
                break;
        }
    }

    public async Task InitializeAsync(Guid organizationId, Guid siteId)
    {
        if (State.SiteId != Guid.Empty)
            return; // Already initialized

        RaiseEvent(new BookingSettingsInitialized
        {
            SiteId = siteId,
            OrganizationId = organizationId,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<BookingSettingsState> GetStateAsync() => Task.FromResult(State);

    public async Task UpdateAsync(UpdateBookingSettingsCommand command)
    {
        EnsureExists();

        RaiseEvent(new BookingSettingsUpdated
        {
            SiteId = State.SiteId,
            DefaultOpenTime = command.DefaultOpenTime,
            DefaultCloseTime = command.DefaultCloseTime,
            DefaultDuration = command.DefaultDuration,
            SlotInterval = command.SlotInterval,
            MaxPartySizeOnline = command.MaxPartySizeOnline,
            MaxBookingsPerSlot = command.MaxBookingsPerSlot,
            AdvanceBookingDays = command.AdvanceBookingDays,
            RequireDeposit = command.RequireDeposit,
            DepositAmount = command.DepositAmount,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<IReadOnlyList<AvailabilitySlot>> GetAvailabilityAsync(GetAvailabilityQuery query)
    {
        EnsureExists();

        var slots = new List<AvailabilitySlot>();
        var currentTime = State.DefaultOpenTime;
        var closeTime = State.DefaultCloseTime;

        // Generate time slots
        while (currentTime < closeTime)
        {
            // Check if party size exceeds online max
            var isAvailable = query.PartySize <= State.MaxPartySizeOnline &&
                              !State.BlockedDates.Contains(query.Date);

            slots.Add(new AvailabilitySlot
            {
                Time = currentTime,
                IsAvailable = isAvailable,
                AvailableCapacity = isAvailable ? State.MaxBookingsPerSlot : 0,
                AvailableTableIds = []
            });

            currentTime = currentTime.Add(State.SlotInterval);
        }

        return Task.FromResult<IReadOnlyList<AvailabilitySlot>>(slots);
    }

    public Task<bool> IsSlotAvailableAsync(DateOnly date, TimeOnly time, int partySize)
    {
        EnsureExists();

        if (State.BlockedDates.Contains(date))
            return Task.FromResult(false);

        if (partySize > State.MaxPartySizeOnline)
            return Task.FromResult(false);

        if (time < State.DefaultOpenTime || time >= State.DefaultCloseTime)
            return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public async Task BlockDateAsync(DateOnly date)
    {
        EnsureExists();
        if (!State.BlockedDates.Contains(date))
        {
            RaiseEvent(new BookingDateBlocked
            {
                SiteId = State.SiteId,
                Date = date,
                OccurredAt = DateTimeOffset.UtcNow
            });
            await ConfirmEvents();
        }
    }

    public async Task UnblockDateAsync(DateOnly date)
    {
        EnsureExists();
        if (State.BlockedDates.Contains(date))
        {
            RaiseEvent(new BookingDateUnblocked
            {
                SiteId = State.SiteId,
                Date = date,
                OccurredAt = DateTimeOffset.UtcNow
            });
            await ConfirmEvents();
        }
    }

    public Task<bool> IsDateBlockedAsync(DateOnly date)
    {
        return Task.FromResult(State.BlockedDates.Contains(date));
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.SiteId != Guid.Empty);

    public Task<int> GetVersionAsync() => Task.FromResult(Version);

    private void EnsureExists()
    {
        if (State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Booking settings not initialized");
    }
}
