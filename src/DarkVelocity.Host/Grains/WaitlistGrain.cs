using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

public class WaitlistGrain : Grain, IWaitlistGrain
{
    private readonly IPersistentState<WaitlistState> _state;

    public WaitlistGrain(
        [PersistentState("waitlist", "OrleansStorage")]
        IPersistentState<WaitlistState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(Guid organizationId, Guid siteId, DateOnly date)
    {
        if (_state.State.SiteId != Guid.Empty)
            return; // Already initialized

        _state.State = new WaitlistState
        {
            OrganizationId = organizationId,
            SiteId = siteId,
            Date = date,
            AverageWait = TimeSpan.FromMinutes(15),
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public Task<WaitlistState> GetStateAsync() => Task.FromResult(_state.State);

    public async Task<WaitlistEntryResult> AddEntryAsync(AddToWaitlistCommand command)
    {
        EnsureExists();

        var entryId = Guid.NewGuid();
        _state.State.CurrentPosition++;
        var position = _state.State.CurrentPosition;

        var entry = new WaitlistEntry
        {
            Id = entryId,
            Position = position,
            Guest = command.Guest,
            PartySize = command.PartySize,
            CheckedInAt = DateTime.UtcNow,
            QuotedWait = command.QuotedWait,
            Status = WaitlistStatus.Waiting,
            TablePreferences = command.TablePreferences,
            NotificationMethod = command.NotificationMethod
        };

        _state.State.Entries.Add(entry);
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new WaitlistEntryResult(entryId, position, command.QuotedWait);
    }

    public async Task UpdatePositionAsync(Guid entryId, int newPosition)
    {
        EnsureExists();

        var index = _state.State.Entries.FindIndex(e => e.Id == entryId);
        if (index < 0)
            throw new InvalidOperationException("Entry not found");

        var entry = _state.State.Entries[index];
        _state.State.Entries[index] = entry with { Position = newPosition };
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task NotifyEntryAsync(Guid entryId)
    {
        EnsureExists();

        var index = _state.State.Entries.FindIndex(e => e.Id == entryId);
        if (index < 0)
            throw new InvalidOperationException("Entry not found");

        var entry = _state.State.Entries[index];
        if (entry.Status != WaitlistStatus.Waiting)
            throw new InvalidOperationException($"Cannot notify entry with status {entry.Status}");

        _state.State.Entries[index] = entry with
        {
            Status = WaitlistStatus.Notified,
            NotifiedAt = DateTime.UtcNow
        };
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task SeatEntryAsync(Guid entryId, Guid tableId)
    {
        EnsureExists();

        var index = _state.State.Entries.FindIndex(e => e.Id == entryId);
        if (index < 0)
            throw new InvalidOperationException("Entry not found");

        var entry = _state.State.Entries[index];
        if (entry.Status != WaitlistStatus.Waiting && entry.Status != WaitlistStatus.Notified)
            throw new InvalidOperationException($"Cannot seat entry with status {entry.Status}");

        var seatedAt = DateTime.UtcNow;
        _state.State.Entries[index] = entry with
        {
            Status = WaitlistStatus.Seated,
            SeatedAt = seatedAt
        };

        // Update average wait time
        var actualWait = seatedAt - entry.CheckedInAt;
        var totalSeated = _state.State.Entries.Count(e => e.Status == WaitlistStatus.Seated);
        _state.State.AverageWait = TimeSpan.FromTicks(
            (_state.State.AverageWait.Ticks * (totalSeated - 1) + actualWait.Ticks) / totalSeated);

        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RemoveEntryAsync(Guid entryId, string reason)
    {
        EnsureExists();

        var index = _state.State.Entries.FindIndex(e => e.Id == entryId);
        if (index < 0)
            throw new InvalidOperationException("Entry not found");

        var entry = _state.State.Entries[index];
        _state.State.Entries[index] = entry with
        {
            Status = WaitlistStatus.Left,
            LeftAt = DateTime.UtcNow
        };
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task<Guid?> ConvertToBookingAsync(Guid entryId, DateTime bookingTime)
    {
        EnsureExists();

        var index = _state.State.Entries.FindIndex(e => e.Id == entryId);
        if (index < 0)
            throw new InvalidOperationException("Entry not found");

        var entry = _state.State.Entries[index];
        var bookingId = Guid.NewGuid();

        _state.State.Entries[index] = entry with
        {
            ConvertedToBookingId = bookingId
        };
        _state.State.Version++;

        await _state.WriteStateAsync();

        return bookingId;
    }

    public Task<int> GetWaitingCountAsync()
    {
        var count = _state.State.Entries.Count(e => e.Status == WaitlistStatus.Waiting);
        return Task.FromResult(count);
    }

    public Task<TimeSpan> GetEstimatedWaitAsync(int partySize)
    {
        EnsureExists();

        var waitingAhead = _state.State.Entries.Count(e => e.Status == WaitlistStatus.Waiting);
        var estimatedWait = TimeSpan.FromTicks(_state.State.AverageWait.Ticks * (waitingAhead + 1));

        // Adjust for party size (larger parties may wait longer)
        if (partySize > 4)
        {
            estimatedWait = TimeSpan.FromTicks((long)(estimatedWait.Ticks * 1.5));
        }

        return Task.FromResult(estimatedWait);
    }

    public Task<IReadOnlyList<WaitlistEntry>> GetEntriesAsync() =>
        Task.FromResult<IReadOnlyList<WaitlistEntry>>(_state.State.Entries);

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.SiteId != Guid.Empty);

    private void EnsureExists()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Waitlist not initialized");
    }
}
