using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain for tracking and analyzing table turn times.
/// </summary>
public class TurnTimeAnalyticsGrain : Grain, ITurnTimeAnalyticsGrain
{
    private readonly IPersistentState<TurnTimeAnalyticsState> _state;

    public TurnTimeAnalyticsGrain(
        [PersistentState("turntime", "OrleansStorage")]
        IPersistentState<TurnTimeAnalyticsState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(Guid organizationId, Guid siteId)
    {
        if (_state.State.SiteId != Guid.Empty)
            return;

        _state.State = new TurnTimeAnalyticsState
        {
            OrganizationId = organizationId,
            SiteId = siteId,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task RecordTurnTimeAsync(RecordTurnTimeCommand command)
    {
        EnsureExists();

        var duration = command.DepartedAt - command.SeatedAt;
        if (duration.TotalMinutes < 5) return; // Ignore very short durations

        var record = new TurnTimeRecordData
        {
            BookingId = command.BookingId,
            TableId = command.TableId,
            PartySize = command.PartySize,
            SeatedAt = command.SeatedAt,
            DepartedAt = command.DepartedAt,
            DurationTicks = duration.Ticks,
            DayOfWeek = command.SeatedAt.DayOfWeek,
            TimeOfDay = TimeOnly.FromDateTime(command.SeatedAt),
            CheckTotal = command.CheckTotal
        };

        _state.State.Records.Insert(0, record);

        // Trim old records
        while (_state.State.Records.Count > _state.State.MaxRecords)
        {
            _state.State.Records.RemoveAt(_state.State.Records.Count - 1);
        }

        // Remove from active seatings
        _state.State.ActiveSeatings.RemoveAll(s => s.BookingId == command.BookingId);

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<TurnTimeStats> GetOverallStatsAsync()
    {
        EnsureExists();

        if (_state.State.Records.Count == 0)
            return Task.FromResult(GetDefaultStats());

        var durations = _state.State.Records.Select(r => TimeSpan.FromTicks(r.DurationTicks)).ToList();
        return Task.FromResult(CalculateStats(durations));
    }

    public Task<IReadOnlyList<TurnTimeByPartySizeStats>> GetStatsByPartySizeAsync()
    {
        EnsureExists();

        var stats = _state.State.Records
            .GroupBy(r => r.PartySize)
            .Select(g => new TurnTimeByPartySizeStats
            {
                PartySize = g.Key,
                Stats = CalculateStats(g.Select(r => TimeSpan.FromTicks(r.DurationTicks)).ToList())
            })
            .OrderBy(s => s.PartySize)
            .ToList();

        return Task.FromResult<IReadOnlyList<TurnTimeByPartySizeStats>>(stats);
    }

    public Task<IReadOnlyList<TurnTimeByDayStats>> GetStatsByDayAsync()
    {
        EnsureExists();

        var stats = _state.State.Records
            .GroupBy(r => r.DayOfWeek)
            .Select(g => new TurnTimeByDayStats
            {
                DayOfWeek = g.Key,
                Stats = CalculateStats(g.Select(r => TimeSpan.FromTicks(r.DurationTicks)).ToList())
            })
            .OrderBy(s => s.DayOfWeek)
            .ToList();

        return Task.FromResult<IReadOnlyList<TurnTimeByDayStats>>(stats);
    }

    public Task<IReadOnlyList<TurnTimeByTimeOfDayStats>> GetStatsByTimeOfDayAsync()
    {
        EnsureExists();

        var periods = new[]
        {
            ("Lunch", new TimeOnly(11, 0), new TimeOnly(14, 30)),
            ("Afternoon", new TimeOnly(14, 30), new TimeOnly(17, 30)),
            ("Dinner", new TimeOnly(17, 30), new TimeOnly(22, 0))
        };

        var stats = periods.Select(p =>
        {
            var periodRecords = _state.State.Records
                .Where(r => r.TimeOfDay >= p.Item2 && r.TimeOfDay < p.Item3)
                .ToList();

            return new TurnTimeByTimeOfDayStats
            {
                Period = p.Item1,
                StartTime = p.Item2,
                EndTime = p.Item3,
                Stats = periodRecords.Count > 0
                    ? CalculateStats(periodRecords.Select(r => TimeSpan.FromTicks(r.DurationTicks)).ToList())
                    : GetDefaultStats()
            };
        }).ToList();

        return Task.FromResult<IReadOnlyList<TurnTimeByTimeOfDayStats>>(stats);
    }

    public Task<TimeSpan> GetEstimatedTurnTimeAsync(int partySize, DayOfWeek dayOfWeek, TimeOnly timeOfDay)
    {
        EnsureExists();

        // Find matching records with same party size, day, and similar time
        var matchingRecords = _state.State.Records
            .Where(r =>
                r.PartySize == partySize ||
                Math.Abs(r.PartySize - partySize) <= 2)
            .Where(r => r.DayOfWeek == dayOfWeek)
            .Where(r =>
            {
                var hourDiff = Math.Abs(r.TimeOfDay.Hour - timeOfDay.Hour);
                return hourDiff <= 2;
            })
            .ToList();

        if (matchingRecords.Count == 0)
        {
            // Fall back to party size only
            matchingRecords = _state.State.Records
                .Where(r => r.PartySize == partySize || Math.Abs(r.PartySize - partySize) <= 2)
                .ToList();
        }

        if (matchingRecords.Count == 0)
        {
            // Default based on party size
            return Task.FromResult(TimeSpan.FromMinutes(60 + partySize * 10));
        }

        var averageTicks = (long)matchingRecords.Average(r => r.DurationTicks);
        return Task.FromResult(TimeSpan.FromTicks(averageTicks));
    }

    public Task<IReadOnlyList<TurnTimeRecord>> GetRecentRecordsAsync(int limit = 100)
    {
        EnsureExists();

        var records = _state.State.Records
            .Take(limit)
            .Select(r => new TurnTimeRecord
            {
                BookingId = r.BookingId,
                TableId = r.TableId,
                PartySize = r.PartySize,
                SeatedAt = r.SeatedAt,
                DepartedAt = r.DepartedAt,
                Duration = TimeSpan.FromTicks(r.DurationTicks),
                DayOfWeek = r.DayOfWeek,
                TimeOfDay = r.TimeOfDay,
                CheckTotal = r.CheckTotal
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<TurnTimeRecord>>(records);
    }

    public async Task<IReadOnlyList<LongRunningTableAlert>> GetLongRunningTablesAsync(TimeSpan threshold)
    {
        EnsureExists();

        var now = DateTime.UtcNow;
        var alerts = new List<LongRunningTableAlert>();

        foreach (var seating in _state.State.ActiveSeatings)
        {
            var elapsed = now - seating.SeatedAt;
            var expected = await GetEstimatedTurnTimeAsync(
                seating.PartySize,
                seating.SeatedAt.DayOfWeek,
                TimeOnly.FromDateTime(seating.SeatedAt));

            if (elapsed > expected + threshold)
            {
                alerts.Add(new LongRunningTableAlert
                {
                    BookingId = seating.BookingId,
                    TableId = seating.TableId,
                    TableNumber = seating.TableNumber,
                    PartySize = seating.PartySize,
                    SeatedAt = seating.SeatedAt,
                    CurrentDuration = elapsed,
                    ExpectedDuration = expected,
                    OverdueBy = elapsed - expected
                });
            }
        }

        return alerts.OrderByDescending(a => a.OverdueBy).ToList();
    }

    public async Task RegisterSeatingAsync(Guid bookingId, Guid? tableId, string? tableNumber, int partySize, DateTime seatedAt)
    {
        EnsureExists();

        // Remove if already exists
        _state.State.ActiveSeatings.RemoveAll(s => s.BookingId == bookingId);

        _state.State.ActiveSeatings.Add(new ActiveSeatingRecord
        {
            BookingId = bookingId,
            TableId = tableId,
            TableNumber = tableNumber,
            PartySize = partySize,
            SeatedAt = seatedAt
        });

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task UnregisterSeatingAsync(Guid bookingId)
    {
        EnsureExists();

        var removed = _state.State.ActiveSeatings.RemoveAll(s => s.BookingId == bookingId);
        if (removed > 0)
        {
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task<IReadOnlyList<ActiveSeating>> GetActiveSeatingsAsync()
    {
        EnsureExists();

        var now = DateTime.UtcNow;
        var seatings = new List<ActiveSeating>();

        foreach (var seating in _state.State.ActiveSeatings)
        {
            var elapsed = now - seating.SeatedAt;
            var expected = await GetEstimatedTurnTimeAsync(
                seating.PartySize,
                seating.SeatedAt.DayOfWeek,
                TimeOnly.FromDateTime(seating.SeatedAt));

            seatings.Add(new ActiveSeating
            {
                BookingId = seating.BookingId,
                TableId = seating.TableId,
                TableNumber = seating.TableNumber,
                PartySize = seating.PartySize,
                SeatedAt = seating.SeatedAt,
                ElapsedTime = elapsed,
                ExpectedDuration = expected,
                IsOverdue = elapsed > expected
            });
        }

        return seatings.OrderByDescending(s => s.ElapsedTime).ToList();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.SiteId != Guid.Empty);

    private static TurnTimeStats CalculateStats(List<TimeSpan> durations)
    {
        if (durations.Count == 0)
            return GetDefaultStats();

        var sortedDurations = durations.OrderBy(d => d).ToList();
        var average = TimeSpan.FromTicks((long)durations.Average(d => d.Ticks));
        var median = sortedDurations[sortedDurations.Count / 2];
        var min = sortedDurations.First();
        var max = sortedDurations.Last();

        // Calculate standard deviation
        var avgTicks = average.Ticks;
        var variance = durations.Average(d => Math.Pow(d.Ticks - avgTicks, 2));
        var stdDev = TimeSpan.FromTicks((long)Math.Sqrt(variance));

        return new TurnTimeStats
        {
            AverageTurnTime = average,
            MedianTurnTime = median,
            MinTurnTime = min,
            MaxTurnTime = max,
            SampleCount = durations.Count,
            StandardDeviation = stdDev
        };
    }

    private static TurnTimeStats GetDefaultStats() => new()
    {
        AverageTurnTime = TimeSpan.FromMinutes(90),
        MedianTurnTime = TimeSpan.FromMinutes(90),
        MinTurnTime = TimeSpan.FromMinutes(60),
        MaxTurnTime = TimeSpan.FromMinutes(120),
        SampleCount = 0,
        StandardDeviation = TimeSpan.Zero
    };

    private void EnsureExists()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Turn time analytics not initialized");
    }
}
