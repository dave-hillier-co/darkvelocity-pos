using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

// ============================================================================
// Enhanced Booking Calendar State
// ============================================================================

[GenerateSerializer]
public record BookingReference
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public string ConfirmationCode { get; init; } = string.Empty;
    [Id(2)] public TimeOnly Time { get; init; }
    [Id(3)] public int PartySize { get; init; }
    [Id(4)] public string GuestName { get; init; } = string.Empty;
    [Id(5)] public BookingStatus Status { get; init; }
    [Id(6)] public Guid? TableId { get; init; }
    [Id(7)] public string? TableNumber { get; init; }
    [Id(8)] public TimeSpan? Duration { get; init; }
}

[GenerateSerializer]
public sealed class BookingCalendarState
{
    [Id(0)] public Guid SiteId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public DateOnly Date { get; set; }
    [Id(3)] public List<BookingReference> Bookings { get; set; } = [];
    [Id(4)] public int TotalCovers { get; set; }
    [Id(5)] public int Version { get; set; }
}

// ============================================================================
// Table Assignment Optimizer State
// ============================================================================

[GenerateSerializer]
public record OptimizableTable
{
    [Id(0)] public Guid TableId { get; init; }
    [Id(1)] public string TableNumber { get; init; } = string.Empty;
    [Id(2)] public int MinCapacity { get; init; }
    [Id(3)] public int MaxCapacity { get; init; }
    [Id(4)] public bool IsCombinable { get; init; }
    [Id(5)] public List<string> Tags { get; init; } = [];
    [Id(6)] public Guid? CurrentServerId { get; init; }
    [Id(7)] public bool IsOccupied { get; init; }
    [Id(8)] public int CurrentCovers { get; init; }
}

[GenerateSerializer]
public record ServerSectionRecord
{
    [Id(0)] public Guid ServerId { get; init; }
    [Id(1)] public string ServerName { get; init; } = string.Empty;
    [Id(2)] public List<Guid> TableIds { get; init; } = [];
    [Id(3)] public int MaxCovers { get; init; }
    [Id(4)] public int CurrentCovers { get; set; }
}

[GenerateSerializer]
public sealed class TableAssignmentOptimizerState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public List<OptimizableTable> Tables { get; set; } = [];
    [Id(3)] public List<ServerSectionRecord> ServerSections { get; set; } = [];
    [Id(4)] public int Version { get; set; }
}

// ============================================================================
// Turn Time Analytics State
// ============================================================================

[GenerateSerializer]
public record TurnTimeRecordData
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public Guid? TableId { get; init; }
    [Id(2)] public int PartySize { get; init; }
    [Id(3)] public DateTime SeatedAt { get; init; }
    [Id(4)] public DateTime DepartedAt { get; init; }
    [Id(5)] public long DurationTicks { get; init; }
    [Id(6)] public DayOfWeek DayOfWeek { get; init; }
    [Id(7)] public TimeOnly TimeOfDay { get; init; }
    [Id(8)] public decimal? CheckTotal { get; init; }
}

[GenerateSerializer]
public record ActiveSeatingRecord
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public Guid? TableId { get; init; }
    [Id(2)] public string? TableNumber { get; init; }
    [Id(3)] public int PartySize { get; init; }
    [Id(4)] public DateTime SeatedAt { get; init; }
}

[GenerateSerializer]
public sealed class TurnTimeAnalyticsState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public List<TurnTimeRecordData> Records { get; set; } = [];
    [Id(3)] public List<ActiveSeatingRecord> ActiveSeatings { get; set; } = [];
    [Id(4)] public int MaxRecords { get; set; } = 10000;
    [Id(5)] public int Version { get; set; }
}

// ============================================================================
// Booking Notification Scheduler State
// ============================================================================

[GenerateSerializer]
public record ScheduledNotificationRecord
{
    [Id(0)] public Guid NotificationId { get; init; }
    [Id(1)] public Guid BookingId { get; init; }
    [Id(2)] public BookingNotificationType Type { get; init; }
    [Id(3)] public DateTime ScheduledFor { get; init; }
    [Id(4)] public bool IsSent { get; set; }
    [Id(5)] public DateTime? SentAt { get; set; }
    [Id(6)] public string Recipient { get; init; } = string.Empty;
    [Id(7)] public string Channel { get; init; } = string.Empty;
    [Id(8)] public string? ErrorMessage { get; set; }
    [Id(9)] public string? GuestName { get; init; }
    [Id(10)] public DateTime? BookingTime { get; init; }
    [Id(11)] public string? ConfirmationCode { get; init; }
    [Id(12)] public int? PartySize { get; init; }
    [Id(13)] public string? SiteName { get; init; }
}

[GenerateSerializer]
public sealed class BookingNotificationSchedulerState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public List<ScheduledNotificationRecord> Notifications { get; set; } = [];
    [Id(3)] public BookingNotificationSettings Settings { get; set; } = new();
    [Id(4)] public int Version { get; set; }
}

// ============================================================================
// No-Show Detection State
// ============================================================================

[GenerateSerializer]
public record NoShowCheckRecord
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public DateTime BookingTime { get; init; }
    [Id(2)] public string? GuestName { get; init; }
    [Id(3)] public Guid? CustomerId { get; init; }
    [Id(4)] public bool HasDeposit { get; init; }
    [Id(5)] public DateTime RegisteredAt { get; init; }
}

[GenerateSerializer]
public record NoShowHistoryRecord
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public bool IsNoShow { get; init; }
    [Id(2)] public DateTime BookingTime { get; init; }
    [Id(3)] public DateTime CheckedAt { get; init; }
    [Id(4)] public TimeSpan GracePeriod { get; init; }
    [Id(5)] public string? GuestName { get; init; }
    [Id(6)] public Guid? CustomerId { get; init; }
}

[GenerateSerializer]
public sealed class NoShowDetectionState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public List<NoShowCheckRecord> PendingChecks { get; set; } = [];
    [Id(3)] public List<NoShowHistoryRecord> History { get; set; } = [];
    [Id(4)] public NoShowSettings Settings { get; set; } = new();
    [Id(5)] public int MaxHistoryRecords { get; set; } = 1000;
    [Id(6)] public int Version { get; set; }
}

// ============================================================================
// Enhanced Waitlist State
// ============================================================================

[GenerateSerializer]
public record WaitlistTurnTimeData
{
    [Id(0)] public int PartySize { get; init; }
    [Id(1)] public long AverageTurnTimeTicks { get; init; }
    [Id(2)] public int SampleCount { get; init; }
}

[GenerateSerializer]
public record WaitlistNotificationRecord
{
    [Id(0)] public Guid EntryId { get; init; }
    [Id(1)] public Guid NotificationId { get; init; }
    [Id(2)] public string Type { get; init; } = string.Empty;
    [Id(3)] public DateTime SentAt { get; init; }
    [Id(4)] public string Channel { get; init; } = string.Empty;
    [Id(5)] public bool Success { get; init; }
    [Id(6)] public string? ErrorMessage { get; init; }
}

[GenerateSerializer]
public sealed class EnhancedWaitlistState
{
    [Id(0)] public Guid SiteId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public DateOnly Date { get; set; }
    [Id(3)] public List<WaitlistEntry> Entries { get; set; } = [];
    [Id(4)] public int CurrentPosition { get; set; }
    [Id(5)] public TimeSpan AverageWait { get; set; }
    [Id(6)] public List<WaitlistTurnTimeData> TurnTimeData { get; set; } = [];
    [Id(7)] public List<WaitlistNotificationRecord> NotificationHistory { get; set; } = [];
    [Id(8)] public WaitlistSettings Settings { get; set; } = new();
    [Id(9)] public int Version { get; set; }
}
