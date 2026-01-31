using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

public class KitchenTicketGrain : Grain, IKitchenTicketGrain
{
    private readonly IPersistentState<KitchenTicketState> _state;
    private int _ticketCounter;

    public KitchenTicketGrain(
        [PersistentState("kitchenticket", "OrleansStorage")]
        IPersistentState<KitchenTicketState> state)
    {
        _state = state;
    }

    public async Task<KitchenTicketCreatedResult> CreateAsync(CreateKitchenTicketCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Kitchen ticket already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, _, ticketId) = GrainKeys.ParseSiteEntity(key);

        var ticketNumber = GenerateTicketNumber();

        _state.State = new KitchenTicketState
        {
            Id = ticketId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            OrderId = command.OrderId,
            OrderNumber = command.OrderNumber,
            TicketNumber = ticketNumber,
            Status = TicketStatus.New,
            Priority = command.Priority,
            OrderType = command.OrderType,
            TableNumber = command.TableNumber,
            GuestCount = command.GuestCount,
            ServerName = command.ServerName,
            Notes = command.Notes,
            CourseNumber = command.CourseNumber,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();

        return new KitchenTicketCreatedResult(ticketId, ticketNumber, _state.State.CreatedAt);
    }

    public Task<KitchenTicketState> GetStateAsync() => Task.FromResult(_state.State);

    public async Task AddItemAsync(AddTicketItemCommand command)
    {
        EnsureExists();

        var item = new TicketItem
        {
            Id = Guid.NewGuid(),
            OrderLineId = command.OrderLineId,
            MenuItemId = command.MenuItemId,
            Name = command.Name,
            Quantity = command.Quantity,
            Status = TicketItemStatus.Pending,
            Modifiers = command.Modifiers ?? [],
            SpecialInstructions = command.SpecialInstructions,
            StationId = command.StationId,
            StationName = command.StationName,
            CourseNumber = command.CourseNumber ?? _state.State.CourseNumber
        };

        _state.State.Items.Add(item);

        if (command.StationId != null && !_state.State.AssignedStationIds.Contains(command.StationId.Value))
            _state.State.AssignedStationIds.Add(command.StationId.Value);

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task StartItemAsync(StartItemCommand command)
    {
        EnsureExists();

        var index = _state.State.Items.FindIndex(i => i.Id == command.ItemId);
        if (index < 0)
            throw new InvalidOperationException("Item not found");

        var item = _state.State.Items[index];
        if (item.Status != TicketItemStatus.Pending)
            throw new InvalidOperationException($"Item cannot be started: {item.Status}");

        _state.State.Items[index] = item with
        {
            Status = TicketItemStatus.Preparing,
            StartedAt = DateTime.UtcNow,
            PreparedBy = command.PreparedBy
        };

        // If this is the first item being started, start the ticket
        if (_state.State.Status == TicketStatus.New)
        {
            _state.State.Status = TicketStatus.InProgress;
            _state.State.StartedAt = DateTime.UtcNow;
            _state.State.WaitTime = DateTime.UtcNow - _state.State.CreatedAt;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task CompleteItemAsync(CompleteItemCommand command)
    {
        EnsureExists();

        var index = _state.State.Items.FindIndex(i => i.Id == command.ItemId);
        if (index < 0)
            throw new InvalidOperationException("Item not found");

        var item = _state.State.Items[index];
        if (item.Status is not (TicketItemStatus.Pending or TicketItemStatus.Preparing))
            throw new InvalidOperationException($"Item cannot be completed: {item.Status}");

        _state.State.Items[index] = item with
        {
            Status = TicketItemStatus.Ready,
            CompletedAt = DateTime.UtcNow,
            PreparedBy = command.PreparedBy
        };

        // Check if all items are ready
        if (_state.State.Items.All(i => i.Status is TicketItemStatus.Ready or TicketItemStatus.Voided))
        {
            _state.State.Status = TicketStatus.Ready;
            _state.State.CompletedAt = DateTime.UtcNow;
            _state.State.PrepTime = _state.State.CompletedAt - _state.State.StartedAt;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task VoidItemAsync(VoidItemCommand command)
    {
        EnsureExists();

        var index = _state.State.Items.FindIndex(i => i.Id == command.ItemId);
        if (index < 0)
            throw new InvalidOperationException("Item not found");

        var item = _state.State.Items[index];
        _state.State.Items[index] = item with { Status = TicketItemStatus.Voided };

        // Check if all remaining items are done
        var activeItems = _state.State.Items.Where(i => i.Status != TicketItemStatus.Voided);
        if (activeItems.All(i => i.Status == TicketItemStatus.Ready))
        {
            _state.State.Status = TicketStatus.Ready;
            _state.State.CompletedAt = DateTime.UtcNow;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task ReceiveAsync()
    {
        EnsureExists();

        if (_state.State.ReceivedAt != null)
            throw new InvalidOperationException("Ticket already received");

        _state.State.ReceivedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task StartAsync()
    {
        EnsureExists();

        if (_state.State.Status != TicketStatus.New)
            throw new InvalidOperationException($"Cannot start ticket: {_state.State.Status}");

        _state.State.Status = TicketStatus.InProgress;
        _state.State.StartedAt = DateTime.UtcNow;
        _state.State.WaitTime = _state.State.StartedAt - _state.State.CreatedAt;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task BumpAsync(Guid bumpedBy)
    {
        EnsureExists();

        if (_state.State.Status is not (TicketStatus.Ready or TicketStatus.InProgress))
            throw new InvalidOperationException($"Cannot bump ticket: {_state.State.Status}");

        _state.State.Status = TicketStatus.Served;
        _state.State.BumpedAt = DateTime.UtcNow;
        _state.State.BumpedBy = bumpedBy;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task VoidAsync(string reason)
    {
        EnsureExists();

        if (_state.State.Status is TicketStatus.Served or TicketStatus.Voided)
            throw new InvalidOperationException($"Cannot void ticket: {_state.State.Status}");

        _state.State.Status = TicketStatus.Voided;
        _state.State.Notes = string.IsNullOrEmpty(_state.State.Notes)
            ? $"VOID: {reason}"
            : $"{_state.State.Notes}\nVOID: {reason}";
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task SetPriorityAsync(TicketPriority priority)
    {
        EnsureExists();

        _state.State.Priority = priority;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task MarkRushAsync() => SetPriorityAsync(TicketPriority.Rush);
    public Task MarkVipAsync() => SetPriorityAsync(TicketPriority.VIP);

    public async Task FireAllAsync()
    {
        EnsureExists();

        _state.State.IsFireAll = true;
        _state.State.Priority = TicketPriority.AllDay;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.Id != Guid.Empty);
    public Task<TicketStatus> GetStatusAsync() => Task.FromResult(_state.State.Status);

    public Task<TicketTimings> GetTimingsAsync()
        => Task.FromResult(new TicketTimings(_state.State.WaitTime, _state.State.PrepTime, _state.State.CompletedAt));

    public Task<IReadOnlyList<TicketItem>> GetPendingItemsAsync()
        => Task.FromResult<IReadOnlyList<TicketItem>>(
            _state.State.Items.Where(i => i.Status is TicketItemStatus.Pending or TicketItemStatus.Preparing).ToList());

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Kitchen ticket does not exist");
    }

    private string GenerateTicketNumber()
    {
        _ticketCounter++;
        return $"KOT-{_state.State.CreatedAt:HHmmss}-{_ticketCounter:D3}";
    }
}

public class KitchenStationGrain : Grain, IKitchenStationGrain
{
    private readonly IPersistentState<KitchenStationState> _state;

    public KitchenStationGrain(
        [PersistentState("kitchenstation", "OrleansStorage")]
        IPersistentState<KitchenStationState> state)
    {
        _state = state;
    }

    public async Task OpenAsync(OpenStationCommand command)
    {
        if (_state.State.Id != Guid.Empty && _state.State.Status == StationStatus.Open)
            throw new InvalidOperationException("Station is already open");

        var key = this.GetPrimaryKeyString();
        var (_, _, _, stationId) = GrainKeys.ParseSiteEntity(key);

        if (_state.State.Id == Guid.Empty)
        {
            _state.State.Id = stationId;
            _state.State.OrganizationId = command.OrganizationId;
            _state.State.SiteId = command.SiteId;
            _state.State.Name = command.Name;
            _state.State.Type = command.Type;
            _state.State.DisplayOrder = command.DisplayOrder;
        }

        _state.State.Status = StationStatus.Open;
        _state.State.OpenedAt = DateTime.UtcNow;
        _state.State.ClosedAt = null;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<KitchenStationState> GetStateAsync() => Task.FromResult(_state.State);

    public async Task AssignItemsAsync(AssignItemsToStationCommand command)
    {
        EnsureExists();

        if (command.MenuItemCategories != null)
        {
            foreach (var cat in command.MenuItemCategories)
            {
                if (!_state.State.AssignedMenuItemCategories.Contains(cat))
                    _state.State.AssignedMenuItemCategories.Add(cat);
            }
        }

        if (command.MenuItemIds != null)
        {
            foreach (var id in command.MenuItemIds)
            {
                if (!_state.State.AssignedMenuItemIds.Contains(id))
                    _state.State.AssignedMenuItemIds.Add(id);
            }
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetPrinterAsync(Guid printerId)
    {
        EnsureExists();

        _state.State.PrinterId = printerId;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task SetDisplayAsync(Guid displayId)
    {
        EnsureExists();

        _state.State.DisplayId = displayId;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task ReceiveTicketAsync(Guid ticketId)
    {
        EnsureExists();
        EnsureOpen();

        if (!_state.State.CurrentTicketIds.Contains(ticketId))
        {
            _state.State.CurrentTicketIds.Add(ticketId);
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task CompleteTicketAsync(Guid ticketId)
    {
        EnsureExists();

        if (_state.State.CurrentTicketIds.Remove(ticketId))
        {
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveTicketAsync(Guid ticketId)
    {
        await CompleteTicketAsync(ticketId);
    }

    public async Task PauseAsync()
    {
        EnsureExists();

        if (_state.State.Status != StationStatus.Open)
            throw new InvalidOperationException($"Cannot pause station: {_state.State.Status}");

        _state.State.Status = StationStatus.Paused;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task ResumeAsync()
    {
        EnsureExists();

        if (_state.State.Status != StationStatus.Paused)
            throw new InvalidOperationException($"Cannot resume station: {_state.State.Status}");

        _state.State.Status = StationStatus.Open;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task CloseAsync(Guid closedBy)
    {
        EnsureExists();

        if (_state.State.Status == StationStatus.Closed)
            throw new InvalidOperationException("Station is already closed");

        _state.State.Status = StationStatus.Closed;
        _state.State.ClosedAt = DateTime.UtcNow;
        _state.State.CurrentTicketIds.Clear();
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<bool> IsOpenAsync() => Task.FromResult(_state.State.Status == StationStatus.Open);
    public Task<int> GetActiveItemCountAsync() => Task.FromResult(_state.State.ActiveItemCount);
    public Task<IReadOnlyList<Guid>> GetCurrentTicketIdsAsync() => Task.FromResult<IReadOnlyList<Guid>>(_state.State.CurrentTicketIds);
    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.Id != Guid.Empty);

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Station does not exist");
    }

    private void EnsureOpen()
    {
        if (_state.State.Status != StationStatus.Open)
            throw new InvalidOperationException($"Station is not open: {_state.State.Status}");
    }
}
