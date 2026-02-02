using DarkVelocity.Host.Extensions;
using DarkVelocity.Host.State;

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

public class FloorPlanGrain : Grain, IFloorPlanGrain
{
    private readonly IPersistentState<FloorPlanState> _state;

    public FloorPlanGrain(
        [PersistentState("floorplan", "OrleansStorage")]
        IPersistentState<FloorPlanState> state)
    {
        _state = state;
    }

    public async Task<FloorPlanCreatedResult> CreateAsync(CreateFloorPlanCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Floor plan already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, _, floorPlanId) = GrainKeys.ParseSiteEntity(key);

        _state.State = new FloorPlanState
        {
            Id = floorPlanId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            Name = command.Name,
            IsDefault = command.IsDefault,
            IsActive = true,
            Width = command.Width,
            Height = command.Height,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        return new FloorPlanCreatedResult(floorPlanId, command.Name, _state.State.CreatedAt);
    }

    public Task<FloorPlanState> GetStateAsync() => Task.FromResult(_state.State);

    public async Task UpdateAsync(UpdateFloorPlanCommand command)
    {
        EnsureExists();

        if (command.Name != null) _state.State.Name = command.Name;
        if (command.Width.HasValue) _state.State.Width = command.Width.Value;
        if (command.Height.HasValue) _state.State.Height = command.Height.Value;
        if (command.BackgroundImageUrl != null) _state.State.BackgroundImageUrl = command.BackgroundImageUrl;
        if (command.IsActive.HasValue) _state.State.IsActive = command.IsActive.Value;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task DeleteAsync()
    {
        EnsureExists();
        await _state.ClearStateAsync();
    }

    public async Task AddTableAsync(Guid tableId)
    {
        EnsureExists();
        if (!_state.State.TableIds.Contains(tableId))
        {
            _state.State.TableIds.Add(tableId);
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveTableAsync(Guid tableId)
    {
        EnsureExists();
        if (_state.State.TableIds.Remove(tableId))
        {
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<IReadOnlyList<Guid>> GetTableIdsAsync() =>
        Task.FromResult<IReadOnlyList<Guid>>(_state.State.TableIds);

    public async Task AddSectionAsync(string name, string? color = null)
    {
        EnsureExists();
        var section = new FloorPlanSection
        {
            Id = Guid.NewGuid(),
            Name = name,
            Color = color,
            SortOrder = _state.State.Sections.Count
        };
        _state.State.Sections.Add(section);
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RemoveSectionAsync(Guid sectionId)
    {
        EnsureExists();
        var section = _state.State.Sections.FirstOrDefault(s => s.Id == sectionId);
        if (section != null)
        {
            _state.State.Sections.Remove(section);
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task UpdateSectionAsync(Guid sectionId, string? name = null, string? color = null, int? sortOrder = null)
    {
        EnsureExists();
        var index = _state.State.Sections.FindIndex(s => s.Id == sectionId);
        if (index >= 0)
        {
            var existing = _state.State.Sections[index];
            _state.State.Sections[index] = existing with
            {
                Name = name ?? existing.Name,
                Color = color ?? existing.Color,
                SortOrder = sortOrder ?? existing.SortOrder
            };
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task SetDefaultAsync()
    {
        EnsureExists();
        _state.State.IsDefault = true;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task ActivateAsync()
    {
        EnsureExists();
        _state.State.IsActive = true;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        EnsureExists();
        _state.State.IsActive = false;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.Id != Guid.Empty);

    public Task<bool> IsActiveAsync() => Task.FromResult(_state.State.IsActive);

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Floor plan does not exist");
    }
}

public class BookingSettingsGrain : Grain, IBookingSettingsGrain
{
    private readonly IPersistentState<BookingSettingsState> _state;
    private readonly IGrainFactory _grainFactory;

    public BookingSettingsGrain(
        [PersistentState("bookingsettings", "OrleansStorage")]
        IPersistentState<BookingSettingsState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public async Task InitializeAsync(Guid organizationId, Guid siteId)
    {
        if (_state.State.SiteId != Guid.Empty)
            return; // Already initialized

        _state.State = new BookingSettingsState
        {
            OrganizationId = organizationId,
            SiteId = siteId,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public Task<BookingSettingsState> GetStateAsync() => Task.FromResult(_state.State);

    public async Task UpdateAsync(UpdateBookingSettingsCommand command)
    {
        EnsureExists();

        if (command.DefaultOpenTime.HasValue) _state.State.DefaultOpenTime = command.DefaultOpenTime.Value;
        if (command.DefaultCloseTime.HasValue) _state.State.DefaultCloseTime = command.DefaultCloseTime.Value;
        if (command.DefaultDuration.HasValue) _state.State.DefaultDuration = command.DefaultDuration.Value;
        if (command.SlotInterval.HasValue) _state.State.SlotInterval = command.SlotInterval.Value;
        if (command.MaxPartySizeOnline.HasValue) _state.State.MaxPartySizeOnline = command.MaxPartySizeOnline.Value;
        if (command.MaxBookingsPerSlot.HasValue) _state.State.MaxBookingsPerSlot = command.MaxBookingsPerSlot.Value;
        if (command.AdvanceBookingDays.HasValue) _state.State.AdvanceBookingDays = command.AdvanceBookingDays.Value;
        if (command.RequireDeposit.HasValue) _state.State.RequireDeposit = command.RequireDeposit.Value;
        if (command.DepositAmount.HasValue) _state.State.DepositAmount = command.DepositAmount.Value;

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<AvailabilitySlot>> GetAvailabilityAsync(GetAvailabilityQuery query)
    {
        EnsureExists();

        var slots = new List<AvailabilitySlot>();
        var currentTime = _state.State.DefaultOpenTime;
        var closeTime = _state.State.DefaultCloseTime;

        // Generate time slots
        while (currentTime < closeTime)
        {
            // Check if party size exceeds online max
            var isAvailable = query.PartySize <= _state.State.MaxPartySizeOnline &&
                              !_state.State.BlockedDates.Contains(query.Date);

            slots.Add(new AvailabilitySlot
            {
                Time = currentTime,
                IsAvailable = isAvailable,
                AvailableCapacity = isAvailable ? _state.State.MaxBookingsPerSlot : 0,
                AvailableTableIds = []
            });

            currentTime = currentTime.Add(_state.State.SlotInterval);
        }

        return Task.FromResult<IReadOnlyList<AvailabilitySlot>>(slots);
    }

    public Task<bool> IsSlotAvailableAsync(DateOnly date, TimeOnly time, int partySize)
    {
        EnsureExists();

        if (_state.State.BlockedDates.Contains(date))
            return Task.FromResult(false);

        if (partySize > _state.State.MaxPartySizeOnline)
            return Task.FromResult(false);

        if (time < _state.State.DefaultOpenTime || time >= _state.State.DefaultCloseTime)
            return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public async Task BlockDateAsync(DateOnly date)
    {
        EnsureExists();
        if (!_state.State.BlockedDates.Contains(date))
        {
            _state.State.BlockedDates.Add(date);
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task UnblockDateAsync(DateOnly date)
    {
        EnsureExists();
        if (_state.State.BlockedDates.Remove(date))
        {
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<bool> IsDateBlockedAsync(DateOnly date)
    {
        return Task.FromResult(_state.State.BlockedDates.Contains(date));
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.SiteId != Guid.Empty);

    private void EnsureExists()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Booking settings not initialized");
    }
}
