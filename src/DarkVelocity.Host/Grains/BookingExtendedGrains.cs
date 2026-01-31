using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Table Grain Implementation
// ============================================================================

public class TableGrain : Grain, ITableGrain
{
    private readonly IPersistentState<TableState> _state;

    public TableGrain(
        [PersistentState("table", "OrleansStorage")]
        IPersistentState<TableState> state)
    {
        _state = state;
    }

    public async Task<TableSnapshot> CreateAsync(CreateTableCommand command)
    {
        if (_state.State.TableId != Guid.Empty)
            throw new InvalidOperationException("Table already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var siteId = Guid.Parse(parts[1]);
        var tableId = Guid.Parse(parts[3]);

        _state.State = new TableState
        {
            OrgId = orgId,
            SiteId = siteId,
            TableId = tableId,
            FloorPlanId = command.FloorPlanId,
            TableNumber = command.TableNumber,
            Name = command.Name,
            MinCapacity = command.MinCapacity,
            MaxCapacity = command.MaxCapacity,
            Shape = command.Shape,
            PositionX = command.PositionX,
            PositionY = command.PositionY,
            Width = command.Width,
            Height = command.Height,
            Rotation = command.Rotation,
            Status = TableStatus.Available,
            IsCombinationAllowed = command.IsCombinationAllowed,
            IsActive = true,
            AssignmentPriority = command.AssignmentPriority,
            Notes = command.Notes,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<TableSnapshot> UpdateAsync(UpdateTableCommand command)
    {
        EnsureExists();

        if (command.TableNumber != null) _state.State.TableNumber = command.TableNumber;
        if (command.Name != null) _state.State.Name = command.Name;
        if (command.MinCapacity.HasValue) _state.State.MinCapacity = command.MinCapacity.Value;
        if (command.MaxCapacity.HasValue) _state.State.MaxCapacity = command.MaxCapacity.Value;
        if (command.Shape != null) _state.State.Shape = command.Shape;
        if (command.PositionX.HasValue) _state.State.PositionX = command.PositionX.Value;
        if (command.PositionY.HasValue) _state.State.PositionY = command.PositionY.Value;
        if (command.Width.HasValue) _state.State.Width = command.Width.Value;
        if (command.Height.HasValue) _state.State.Height = command.Height.Value;
        if (command.Rotation.HasValue) _state.State.Rotation = command.Rotation.Value;
        if (command.Status.HasValue) _state.State.Status = command.Status.Value;
        if (command.IsCombinationAllowed.HasValue) _state.State.IsCombinationAllowed = command.IsCombinationAllowed.Value;
        if (command.IsActive.HasValue) _state.State.IsActive = command.IsActive.Value;
        if (command.AssignmentPriority.HasValue) _state.State.AssignmentPriority = command.AssignmentPriority.Value;
        if (command.Notes != null) _state.State.Notes = command.Notes;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<TableSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.TableId != Guid.Empty);
    }

    public async Task DeleteAsync()
    {
        EnsureExists();
        _state.State.IsActive = false;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task<TableSnapshot> SetStatusAsync(TableStatus status)
    {
        EnsureExists();
        _state.State.Status = status;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<TableStatus> GetStatusAsync()
    {
        return Task.FromResult(_state.State.Status);
    }

    public Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(_state.State.IsActive && _state.State.Status == TableStatus.Available);
    }

    public async Task UpdatePositionAsync(int positionX, int positionY, int? rotation)
    {
        EnsureExists();
        _state.State.PositionX = positionX;
        _state.State.PositionY = positionY;
        if (rotation.HasValue) _state.State.Rotation = rotation.Value;
        _state.State.UpdatedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    private void EnsureExists()
    {
        if (_state.State.TableId == Guid.Empty)
            throw new InvalidOperationException("Table not found");
    }

    private TableSnapshot CreateSnapshot()
    {
        return new TableSnapshot(
            _state.State.TableId,
            _state.State.SiteId, // LocationId == SiteId
            _state.State.FloorPlanId,
            _state.State.FloorPlanName,
            _state.State.TableNumber,
            _state.State.Name,
            _state.State.MinCapacity,
            _state.State.MaxCapacity,
            _state.State.Shape,
            _state.State.PositionX,
            _state.State.PositionY,
            _state.State.Width,
            _state.State.Height,
            _state.State.Rotation,
            _state.State.Status,
            _state.State.IsCombinationAllowed,
            _state.State.IsActive,
            _state.State.AssignmentPriority,
            _state.State.Notes,
            _state.State.CreatedAt);
    }
}

// ============================================================================
// Floor Plan Grain Implementation
// ============================================================================

public class FloorPlanGrain : Grain, IFloorPlanGrain
{
    private readonly IPersistentState<FloorPlanState> _state;

    public FloorPlanGrain(
        [PersistentState("floorPlan", "OrleansStorage")]
        IPersistentState<FloorPlanState> state)
    {
        _state = state;
    }

    public async Task<FloorPlanSnapshot> CreateAsync(CreateFloorPlanCommand command)
    {
        if (_state.State.FloorPlanId != Guid.Empty)
            throw new InvalidOperationException("Floor plan already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var siteId = Guid.Parse(parts[1]);
        var floorPlanId = Guid.Parse(parts[3]);

        _state.State = new FloorPlanState
        {
            OrgId = orgId,
            SiteId = siteId,
            FloorPlanId = floorPlanId,
            Name = command.Name,
            Description = command.Description,
            GridWidth = command.GridWidth,
            GridHeight = command.GridHeight,
            BackgroundImageUrl = command.BackgroundImageUrl,
            SortOrder = command.SortOrder,
            IsActive = true,
            DefaultTurnTimeMinutes = command.DefaultTurnTimeMinutes,
            TableCount = 0,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<FloorPlanSnapshot> UpdateAsync(UpdateFloorPlanCommand command)
    {
        EnsureExists();

        if (command.Name != null) _state.State.Name = command.Name;
        if (command.Description != null) _state.State.Description = command.Description;
        if (command.GridWidth.HasValue) _state.State.GridWidth = command.GridWidth.Value;
        if (command.GridHeight.HasValue) _state.State.GridHeight = command.GridHeight.Value;
        if (command.BackgroundImageUrl != null) _state.State.BackgroundImageUrl = command.BackgroundImageUrl;
        if (command.SortOrder.HasValue) _state.State.SortOrder = command.SortOrder.Value;
        if (command.IsActive.HasValue) _state.State.IsActive = command.IsActive.Value;
        if (command.DefaultTurnTimeMinutes.HasValue) _state.State.DefaultTurnTimeMinutes = command.DefaultTurnTimeMinutes.Value;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<FloorPlanSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.FloorPlanId != Guid.Empty);
    }

    public async Task DeleteAsync()
    {
        EnsureExists();
        _state.State.IsActive = false;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task IncrementTableCountAsync()
    {
        EnsureExists();
        _state.State.TableCount++;
        await _state.WriteStateAsync();
    }

    public async Task DecrementTableCountAsync()
    {
        EnsureExists();
        if (_state.State.TableCount > 0)
        {
            _state.State.TableCount--;
            await _state.WriteStateAsync();
        }
    }

    public Task<int> GetTableCountAsync()
    {
        return Task.FromResult(_state.State.TableCount);
    }

    private void EnsureExists()
    {
        if (_state.State.FloorPlanId == Guid.Empty)
            throw new InvalidOperationException("Floor plan not found");
    }

    private FloorPlanSnapshot CreateSnapshot()
    {
        return new FloorPlanSnapshot(
            _state.State.FloorPlanId,
            _state.State.SiteId, // LocationId == SiteId
            _state.State.Name,
            _state.State.Description,
            _state.State.GridWidth,
            _state.State.GridHeight,
            _state.State.BackgroundImageUrl,
            _state.State.SortOrder,
            _state.State.IsActive,
            _state.State.DefaultTurnTimeMinutes,
            _state.State.TableCount,
            _state.State.CreatedAt);
    }
}

// ============================================================================
// Booking Settings Grain Implementation
// ============================================================================

public class BookingSettingsGrain : Grain, IBookingSettingsGrain
{
    private readonly IPersistentState<BookingSettingsState> _state;

    public BookingSettingsGrain(
        [PersistentState("bookingSettings", "OrleansStorage")]
        IPersistentState<BookingSettingsState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(Guid locationId)
    {
        if (_state.State.LocationId != Guid.Empty)
            return; // Already initialized

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State = new BookingSettingsState
        {
            OrgId = orgId,
            LocationId = locationId,
            DefaultBookingDurationMinutes = 90,
            MinAdvanceBookingMinutes = 30,
            MaxAdvanceBookingDays = 60,
            AllowOnlineBookings = true,
            RequireDeposit = false,
            DepositAmount = 0,
            DepositPercentage = 0,
            CancellationDeadlineMinutes = 120,
            CancellationFeeAmount = 0,
            CancellationFeePercentage = 0,
            AllowWaitlist = true,
            MaxWaitlistSize = 20,
            TurnTimeMinutes = 15,
            BufferTimeMinutes = 5,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public Task<BookingSettingsSnapshot> GetSettingsAsync()
    {
        EnsureExists();
        return Task.FromResult(CreateSnapshot());
    }

    public async Task<BookingSettingsSnapshot> UpdateAsync(UpdateBookingSettingsCommand command)
    {
        EnsureExists();

        if (command.DefaultBookingDurationMinutes.HasValue)
            _state.State.DefaultBookingDurationMinutes = command.DefaultBookingDurationMinutes.Value;
        if (command.MinAdvanceBookingMinutes.HasValue)
            _state.State.MinAdvanceBookingMinutes = command.MinAdvanceBookingMinutes.Value;
        if (command.MaxAdvanceBookingDays.HasValue)
            _state.State.MaxAdvanceBookingDays = command.MaxAdvanceBookingDays.Value;
        if (command.AllowOnlineBookings.HasValue)
            _state.State.AllowOnlineBookings = command.AllowOnlineBookings.Value;
        if (command.RequireDeposit.HasValue)
            _state.State.RequireDeposit = command.RequireDeposit.Value;
        if (command.DepositAmount.HasValue)
            _state.State.DepositAmount = command.DepositAmount.Value;
        if (command.DepositPercentage.HasValue)
            _state.State.DepositPercentage = command.DepositPercentage.Value;
        if (command.CancellationDeadlineMinutes.HasValue)
            _state.State.CancellationDeadlineMinutes = command.CancellationDeadlineMinutes.Value;
        if (command.CancellationFeeAmount.HasValue)
            _state.State.CancellationFeeAmount = command.CancellationFeeAmount.Value;
        if (command.CancellationFeePercentage.HasValue)
            _state.State.CancellationFeePercentage = command.CancellationFeePercentage.Value;
        if (command.AllowWaitlist.HasValue)
            _state.State.AllowWaitlist = command.AllowWaitlist.Value;
        if (command.MaxWaitlistSize.HasValue)
            _state.State.MaxWaitlistSize = command.MaxWaitlistSize.Value;
        if (command.FirstServiceStart.HasValue)
            _state.State.FirstServiceStart = command.FirstServiceStart;
        if (command.FirstServiceEnd.HasValue)
            _state.State.FirstServiceEnd = command.FirstServiceEnd;
        if (command.SecondServiceStart.HasValue)
            _state.State.SecondServiceStart = command.SecondServiceStart;
        if (command.SecondServiceEnd.HasValue)
            _state.State.SecondServiceEnd = command.SecondServiceEnd;
        if (command.TurnTimeMinutes.HasValue)
            _state.State.TurnTimeMinutes = command.TurnTimeMinutes.Value;
        if (command.BufferTimeMinutes.HasValue)
            _state.State.BufferTimeMinutes = command.BufferTimeMinutes.Value;
        if (command.ConfirmationMessageTemplate != null)
            _state.State.ConfirmationMessageTemplate = command.ConfirmationMessageTemplate;
        if (command.ReminderMessageTemplate != null)
            _state.State.ReminderMessageTemplate = command.ReminderMessageTemplate;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.LocationId != Guid.Empty);
    }

    public Task<bool> CanAcceptOnlineBookingAsync()
    {
        return Task.FromResult(_state.State.AllowOnlineBookings);
    }

    public Task<decimal> CalculateDepositAsync(decimal totalAmount)
    {
        if (!_state.State.RequireDeposit)
            return Task.FromResult(0m);

        if (_state.State.DepositAmount > 0)
            return Task.FromResult(_state.State.DepositAmount);

        if (_state.State.DepositPercentage > 0)
            return Task.FromResult(totalAmount * (_state.State.DepositPercentage / 100));

        return Task.FromResult(0m);
    }

    public Task<decimal> CalculateCancellationFeeAsync(decimal totalAmount, DateTime bookingTime)
    {
        var deadline = bookingTime.AddMinutes(-_state.State.CancellationDeadlineMinutes);
        if (DateTime.UtcNow < deadline)
            return Task.FromResult(0m);

        if (_state.State.CancellationFeeAmount > 0)
            return Task.FromResult(_state.State.CancellationFeeAmount);

        if (_state.State.CancellationFeePercentage > 0)
            return Task.FromResult(totalAmount * (_state.State.CancellationFeePercentage / 100));

        return Task.FromResult(0m);
    }

    public Task<bool> IsWithinBookingWindowAsync(DateTime requestedTime)
    {
        var now = DateTime.UtcNow;
        var minTime = now.AddMinutes(_state.State.MinAdvanceBookingMinutes);
        var maxTime = now.AddDays(_state.State.MaxAdvanceBookingDays);

        var isWithinWindow = requestedTime >= minTime && requestedTime <= maxTime;
        return Task.FromResult(isWithinWindow);
    }

    private void EnsureExists()
    {
        if (_state.State.LocationId == Guid.Empty)
            throw new InvalidOperationException("Booking settings not found - call InitializeAsync first");
    }

    private BookingSettingsSnapshot CreateSnapshot()
    {
        return new BookingSettingsSnapshot(
            _state.State.LocationId,
            _state.State.DefaultBookingDurationMinutes,
            _state.State.MinAdvanceBookingMinutes,
            _state.State.MaxAdvanceBookingDays,
            _state.State.AllowOnlineBookings,
            _state.State.RequireDeposit,
            _state.State.DepositAmount,
            _state.State.DepositPercentage,
            _state.State.CancellationDeadlineMinutes,
            _state.State.CancellationFeeAmount,
            _state.State.CancellationFeePercentage,
            _state.State.AllowWaitlist,
            _state.State.MaxWaitlistSize,
            _state.State.FirstServiceStart,
            _state.State.FirstServiceEnd,
            _state.State.SecondServiceStart,
            _state.State.SecondServiceEnd,
            _state.State.TurnTimeMinutes,
            _state.State.BufferTimeMinutes,
            _state.State.ConfirmationMessageTemplate,
            _state.State.ReminderMessageTemplate);
    }
}
