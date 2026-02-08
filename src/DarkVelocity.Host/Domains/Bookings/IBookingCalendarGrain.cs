using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Booking Calendar Commands and Queries
// ============================================================================

[GenerateSerializer]
public record AddBookingToCalendarCommand(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] string ConfirmationCode,
    [property: Id(2)] TimeOnly Time,
    [property: Id(3)] int PartySize,
    [property: Id(4)] string GuestName,
    [property: Id(5)] BookingStatus Status,
    [property: Id(6)] TimeSpan? Duration = null);

[GenerateSerializer]
public record UpdateBookingInCalendarCommand(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] BookingStatus? Status = null,
    [property: Id(2)] TimeOnly? Time = null,
    [property: Id(3)] int? PartySize = null,
    [property: Id(4)] Guid? TableId = null,
    [property: Id(5)] string? TableNumber = null);

[GenerateSerializer]
public record CalendarDayView
{
    [Id(0)] public DateOnly Date { get; init; }
    [Id(1)] public IReadOnlyList<CalendarBookingSlot> Slots { get; init; } = [];
    [Id(2)] public int TotalBookings { get; init; }
    [Id(3)] public int TotalCovers { get; init; }
    [Id(4)] public int ConfirmedBookings { get; init; }
    [Id(5)] public int SeatedBookings { get; init; }
    [Id(6)] public int NoShowCount { get; init; }
}

[GenerateSerializer]
public record CalendarBookingSlot
{
    [Id(0)] public TimeOnly StartTime { get; init; }
    [Id(1)] public TimeOnly EndTime { get; init; }
    [Id(2)] public int BookingCount { get; init; }
    [Id(3)] public int CoverCount { get; init; }
    [Id(4)] public IReadOnlyList<BookingReference> Bookings { get; init; } = [];
}

[GenerateSerializer]
public record CalendarWeekView
{
    [Id(0)] public DateOnly StartDate { get; init; }
    [Id(1)] public DateOnly EndDate { get; init; }
    [Id(2)] public IReadOnlyList<CalendarDaySummary> Days { get; init; } = [];
    [Id(3)] public int TotalBookings { get; init; }
    [Id(4)] public int TotalCovers { get; init; }
}

[GenerateSerializer]
public record CalendarDaySummary
{
    [Id(0)] public DateOnly Date { get; init; }
    [Id(1)] public int BookingCount { get; init; }
    [Id(2)] public int CoverCount { get; init; }
    [Id(3)] public int AvailableSlots { get; init; }
    [Id(4)] public bool IsClosed { get; init; }
}

[GenerateSerializer]
public record AvailabilityResult
{
    [Id(0)] public DateOnly Date { get; init; }
    [Id(1)] public int PartySize { get; init; }
    [Id(2)] public IReadOnlyList<AvailableTimeSlot> AvailableSlots { get; init; } = [];
    [Id(3)] public string? Reason { get; init; }
}

[GenerateSerializer]
public record AvailableTimeSlot
{
    [Id(0)] public TimeOnly Time { get; init; }
    [Id(1)] public bool IsAvailable { get; init; }
    [Id(2)] public int AvailableCapacity { get; init; }
    [Id(3)] public IReadOnlyList<TableSuggestion> SuggestedTables { get; init; } = [];
    [Id(4)] public TimeSpan EstimatedDuration { get; init; }
}

[GenerateSerializer]
public record TableSuggestion
{
    [Id(0)] public Guid TableId { get; init; }
    [Id(1)] public string TableNumber { get; init; } = string.Empty;
    [Id(2)] public int Capacity { get; init; }
    [Id(3)] public bool IsCombination { get; init; }
    [Id(4)] public IReadOnlyList<Guid>? CombinedTableIds { get; init; }
    [Id(5)] public int Score { get; init; }
}

[GenerateSerializer]
public record GetCalendarAvailabilityQuery(
    [property: Id(0)] DateOnly Date,
    [property: Id(1)] int PartySize,
    [property: Id(2)] TimeOnly? PreferredTime = null,
    [property: Id(3)] TimeSpan? RequestedDuration = null);

// ============================================================================
// Enhanced Booking Calendar Grain Interface
// ============================================================================

/// <summary>
/// Enhanced booking calendar grain with day/week views and availability calculation.
/// Key: "{orgId}:{siteId}:calendar:{date:yyyy-MM-dd}"
/// </summary>
public interface IBookingCalendarGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid organizationId, Guid siteId, DateOnly date);
    Task<BookingCalendarState> GetStateAsync();

    // Booking management
    Task AddBookingAsync(AddBookingToCalendarCommand command);
    Task UpdateBookingAsync(UpdateBookingInCalendarCommand command);
    Task RemoveBookingAsync(Guid bookingId);

    // Queries
    Task<IReadOnlyList<BookingReference>> GetBookingsAsync(BookingStatus? status = null);
    Task<IReadOnlyList<BookingReference>> GetBookingsByTimeRangeAsync(TimeOnly start, TimeOnly end);
    Task<int> GetCoverCountAsync();
    Task<int> GetBookingCountAsync(BookingStatus? status = null);

    // Calendar views
    Task<CalendarDayView> GetDayViewAsync(TimeSpan? slotDuration = null);
    Task<IReadOnlyList<AvailableTimeSlot>> GetAvailabilityAsync(GetCalendarAvailabilityQuery query);

    // Resource view
    Task<IReadOnlyList<TableAllocation>> GetTableAllocationsAsync();
    Task SetTableAllocationAsync(Guid bookingId, Guid tableId, string tableNumber);

    Task<bool> ExistsAsync();
}

[GenerateSerializer]
public record TableAllocation
{
    [Id(0)] public Guid TableId { get; init; }
    [Id(1)] public string TableNumber { get; init; } = string.Empty;
    [Id(2)] public IReadOnlyList<BookingReference> Bookings { get; init; } = [];
}

// ============================================================================
// Week Calendar Grain Interface
// ============================================================================

/// <summary>
/// Grain for weekly booking calendar views.
/// Key: "{orgId}:{siteId}:weekcalendar:{startDate:yyyy-MM-dd}"
/// </summary>
public interface IWeekCalendarGrain : IGrainWithStringKey
{
    Task<CalendarWeekView> GetWeekViewAsync(Guid orgId, Guid siteId, DateOnly startDate);
}
