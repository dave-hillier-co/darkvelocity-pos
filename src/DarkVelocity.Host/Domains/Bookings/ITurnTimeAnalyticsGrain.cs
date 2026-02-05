namespace DarkVelocity.Host.Grains;

// ============================================================================
// Turn Time Analytics Types
// ============================================================================

[GenerateSerializer]
public record TurnTimeRecord
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public Guid? TableId { get; init; }
    [Id(2)] public int PartySize { get; init; }
    [Id(3)] public DateTime SeatedAt { get; init; }
    [Id(4)] public DateTime DepartedAt { get; init; }
    [Id(5)] public TimeSpan Duration { get; init; }
    [Id(6)] public DayOfWeek DayOfWeek { get; init; }
    [Id(7)] public TimeOnly TimeOfDay { get; init; }
    [Id(8)] public decimal? CheckTotal { get; init; }
}

[GenerateSerializer]
public record TurnTimeStats
{
    [Id(0)] public TimeSpan AverageTurnTime { get; init; }
    [Id(1)] public TimeSpan MedianTurnTime { get; init; }
    [Id(2)] public TimeSpan MinTurnTime { get; init; }
    [Id(3)] public TimeSpan MaxTurnTime { get; init; }
    [Id(4)] public int SampleCount { get; init; }
    [Id(5)] public TimeSpan StandardDeviation { get; init; }
}

[GenerateSerializer]
public record TurnTimeByPartySizeStats
{
    [Id(0)] public int PartySize { get; init; }
    [Id(1)] public TurnTimeStats Stats { get; init; } = new();
}

[GenerateSerializer]
public record TurnTimeByDayStats
{
    [Id(0)] public DayOfWeek DayOfWeek { get; init; }
    [Id(1)] public TurnTimeStats Stats { get; init; } = new();
}

[GenerateSerializer]
public record TurnTimeByTimeOfDayStats
{
    [Id(0)] public string Period { get; init; } = string.Empty; // "Lunch", "Dinner", etc.
    [Id(1)] public TimeOnly StartTime { get; init; }
    [Id(2)] public TimeOnly EndTime { get; init; }
    [Id(3)] public TurnTimeStats Stats { get; init; } = new();
}

[GenerateSerializer]
public record RecordTurnTimeCommand(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] Guid? TableId,
    [property: Id(2)] int PartySize,
    [property: Id(3)] DateTime SeatedAt,
    [property: Id(4)] DateTime DepartedAt,
    [property: Id(5)] decimal? CheckTotal = null);

[GenerateSerializer]
public record LongRunningTableAlert
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public Guid? TableId { get; init; }
    [Id(2)] public string? TableNumber { get; init; }
    [Id(3)] public int PartySize { get; init; }
    [Id(4)] public DateTime SeatedAt { get; init; }
    [Id(5)] public TimeSpan CurrentDuration { get; init; }
    [Id(6)] public TimeSpan ExpectedDuration { get; init; }
    [Id(7)] public TimeSpan OverdueBy { get; init; }
}

// ============================================================================
// Turn Time Analytics Grain Interface
// ============================================================================

/// <summary>
/// Grain for tracking and analyzing table turn times.
/// Key: "{orgId}:{siteId}:turntime"
/// </summary>
public interface ITurnTimeAnalyticsGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid organizationId, Guid siteId);

    /// <summary>
    /// Records a completed turn time.
    /// </summary>
    Task RecordTurnTimeAsync(RecordTurnTimeCommand command);

    /// <summary>
    /// Gets overall turn time statistics.
    /// </summary>
    Task<TurnTimeStats> GetOverallStatsAsync();

    /// <summary>
    /// Gets turn time statistics by party size.
    /// </summary>
    Task<IReadOnlyList<TurnTimeByPartySizeStats>> GetStatsByPartySizeAsync();

    /// <summary>
    /// Gets turn time statistics by day of week.
    /// </summary>
    Task<IReadOnlyList<TurnTimeByDayStats>> GetStatsByDayAsync();

    /// <summary>
    /// Gets turn time statistics by time of day (meal period).
    /// </summary>
    Task<IReadOnlyList<TurnTimeByTimeOfDayStats>> GetStatsByTimeOfDayAsync();

    /// <summary>
    /// Gets the estimated turn time for a party size at a given time.
    /// </summary>
    Task<TimeSpan> GetEstimatedTurnTimeAsync(int partySize, DayOfWeek dayOfWeek, TimeOnly timeOfDay);

    /// <summary>
    /// Gets recent turn time records.
    /// </summary>
    Task<IReadOnlyList<TurnTimeRecord>> GetRecentRecordsAsync(int limit = 100);

    /// <summary>
    /// Gets tables that are running longer than expected.
    /// </summary>
    Task<IReadOnlyList<LongRunningTableAlert>> GetLongRunningTablesAsync(TimeSpan threshold);

    /// <summary>
    /// Registers a current seating for tracking (called when guest is seated).
    /// </summary>
    Task RegisterSeatingAsync(Guid bookingId, Guid? tableId, string? tableNumber, int partySize, DateTime seatedAt);

    /// <summary>
    /// Removes a seating tracking (called when guest departs or booking is cancelled).
    /// </summary>
    Task UnregisterSeatingAsync(Guid bookingId);

    /// <summary>
    /// Gets all currently seated bookings with their elapsed time.
    /// </summary>
    Task<IReadOnlyList<ActiveSeating>> GetActiveSeatingsAsync();

    Task<bool> ExistsAsync();
}

[GenerateSerializer]
public record ActiveSeating
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public Guid? TableId { get; init; }
    [Id(2)] public string? TableNumber { get; init; }
    [Id(3)] public int PartySize { get; init; }
    [Id(4)] public DateTime SeatedAt { get; init; }
    [Id(5)] public TimeSpan ElapsedTime { get; init; }
    [Id(6)] public TimeSpan ExpectedDuration { get; init; }
    [Id(7)] public bool IsOverdue { get; init; }
}
