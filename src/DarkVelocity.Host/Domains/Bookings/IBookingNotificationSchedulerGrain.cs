namespace DarkVelocity.Host.Grains;

// ============================================================================
// Booking Notification Types
// ============================================================================

public enum BookingNotificationType
{
    Confirmation,
    Reminder24h,
    Reminder2h,
    TableReady,
    FollowUp,
    NoShowWarning,
    DepositReminder
}

[GenerateSerializer]
public record ScheduledBookingNotification
{
    [Id(0)] public Guid NotificationId { get; init; }
    [Id(1)] public Guid BookingId { get; init; }
    [Id(2)] public BookingNotificationType Type { get; init; }
    [Id(3)] public DateTime ScheduledFor { get; init; }
    [Id(4)] public bool IsSent { get; init; }
    [Id(5)] public DateTime? SentAt { get; init; }
    [Id(6)] public string? Recipient { get; init; }
    [Id(7)] public string? Channel { get; init; } // "email", "sms", "push"
    [Id(8)] public string? ErrorMessage { get; init; }
}

[GenerateSerializer]
public record ScheduleBookingNotificationCommand(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] BookingNotificationType Type,
    [property: Id(2)] DateTime ScheduledFor,
    [property: Id(3)] string Recipient,
    [property: Id(4)] string Channel,
    [property: Id(5)] string? GuestName = null,
    [property: Id(6)] DateTime? BookingTime = null,
    [property: Id(7)] string? ConfirmationCode = null,
    [property: Id(8)] int? PartySize = null,
    [property: Id(9)] string? SiteName = null);

[GenerateSerializer]
public record BookingNotificationSettings
{
    [Id(0)] public bool SendConfirmation { get; init; } = true;
    [Id(1)] public bool Send24hReminder { get; init; } = true;
    [Id(2)] public bool Send2hReminder { get; init; } = true;
    [Id(3)] public bool SendFollowUp { get; init; } = true;
    [Id(4)] public TimeSpan FollowUpDelay { get; init; } = TimeSpan.FromHours(24);
    [Id(5)] public string DefaultChannel { get; init; } = "email";
    [Id(6)] public string? EmailTemplate { get; init; }
    [Id(7)] public string? SmsTemplate { get; init; }
}

[GenerateSerializer]
public record UpdateBookingNotificationSettingsCommand(
    [property: Id(0)] bool? SendConfirmation = null,
    [property: Id(1)] bool? Send24hReminder = null,
    [property: Id(2)] bool? Send2hReminder = null,
    [property: Id(3)] bool? SendFollowUp = null,
    [property: Id(4)] TimeSpan? FollowUpDelay = null,
    [property: Id(5)] string? DefaultChannel = null);

// ============================================================================
// Booking Notification Scheduler Grain Interface
// ============================================================================

/// <summary>
/// Grain for scheduling booking notifications (confirmations, reminders, follow-ups).
/// Uses Orleans reminders for scheduling.
/// Key: "{orgId}:{siteId}:bookingnotifications"
/// </summary>
public interface IBookingNotificationSchedulerGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid organizationId, Guid siteId);

    /// <summary>
    /// Schedules all standard notifications for a new booking.
    /// </summary>
    Task ScheduleBookingNotificationsAsync(
        Guid bookingId,
        string guestName,
        string recipient,
        string channel,
        DateTime bookingTime,
        string confirmationCode,
        int partySize,
        string? siteName = null);

    /// <summary>
    /// Schedules a specific notification.
    /// </summary>
    Task<ScheduledBookingNotification> ScheduleNotificationAsync(ScheduleBookingNotificationCommand command);

    /// <summary>
    /// Cancels all pending notifications for a booking.
    /// </summary>
    Task CancelBookingNotificationsAsync(Guid bookingId);

    /// <summary>
    /// Cancels a specific notification.
    /// </summary>
    Task CancelNotificationAsync(Guid notificationId);

    /// <summary>
    /// Gets pending notifications for a booking.
    /// </summary>
    Task<IReadOnlyList<ScheduledBookingNotification>> GetPendingNotificationsAsync(Guid bookingId);

    /// <summary>
    /// Gets all pending notifications for the site.
    /// </summary>
    Task<IReadOnlyList<ScheduledBookingNotification>> GetAllPendingNotificationsAsync();

    /// <summary>
    /// Gets notification history for a booking.
    /// </summary>
    Task<IReadOnlyList<ScheduledBookingNotification>> GetNotificationHistoryAsync(Guid bookingId);

    /// <summary>
    /// Sends a notification immediately (bypasses scheduling).
    /// </summary>
    Task SendImmediateNotificationAsync(ScheduleBookingNotificationCommand command);

    /// <summary>
    /// Gets notification settings.
    /// </summary>
    Task<BookingNotificationSettings> GetSettingsAsync();

    /// <summary>
    /// Updates notification settings.
    /// </summary>
    Task UpdateSettingsAsync(UpdateBookingNotificationSettingsCommand command);

    /// <summary>
    /// Triggers follow-up notification for a completed booking.
    /// </summary>
    Task ScheduleFollowUpAsync(Guid bookingId, string recipient, string channel, string? guestName = null);

    Task<bool> ExistsAsync();
}

// ============================================================================
// No-Show Detection Grain Interface
// ============================================================================

[GenerateSerializer]
public record NoShowCheckResult
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
public record NoShowSettings
{
    [Id(0)] public TimeSpan GracePeriod { get; init; } = TimeSpan.FromMinutes(15);
    [Id(1)] public bool AutoMarkNoShow { get; init; } = true;
    [Id(2)] public bool NotifyOnNoShow { get; init; } = true;
    [Id(3)] public bool ForfeitDepositOnNoShow { get; init; } = true;
    [Id(4)] public bool UpdateCustomerHistory { get; init; } = true;
}

[GenerateSerializer]
public record UpdateNoShowSettingsCommand(
    [property: Id(0)] TimeSpan? GracePeriod = null,
    [property: Id(1)] bool? AutoMarkNoShow = null,
    [property: Id(2)] bool? NotifyOnNoShow = null,
    [property: Id(3)] bool? ForfeitDepositOnNoShow = null,
    [property: Id(4)] bool? UpdateCustomerHistory = null);

[GenerateSerializer]
public record RegisterNoShowCheckCommand(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] DateTime BookingTime,
    [property: Id(2)] string? GuestName = null,
    [property: Id(3)] Guid? CustomerId = null,
    [property: Id(4)] bool HasDeposit = false);

/// <summary>
/// Grain for no-show detection and handling.
/// Uses Orleans reminders to check for no-shows at booking time + grace period.
/// Key: "{orgId}:{siteId}:noshowdetection"
/// </summary>
public interface INoShowDetectionGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid organizationId, Guid siteId);

    /// <summary>
    /// Registers a booking for no-show detection (sets up reminder).
    /// </summary>
    Task RegisterBookingAsync(RegisterNoShowCheckCommand command);

    /// <summary>
    /// Unregisters a booking from no-show detection (booking was seated or cancelled).
    /// </summary>
    Task UnregisterBookingAsync(Guid bookingId);

    /// <summary>
    /// Manually checks if a booking is a no-show.
    /// </summary>
    Task<NoShowCheckResult> CheckNoShowAsync(Guid bookingId);

    /// <summary>
    /// Gets pending no-show checks (bookings being monitored).
    /// </summary>
    Task<IReadOnlyList<RegisterNoShowCheckCommand>> GetPendingChecksAsync();

    /// <summary>
    /// Gets no-show settings.
    /// </summary>
    Task<NoShowSettings> GetSettingsAsync();

    /// <summary>
    /// Updates no-show settings.
    /// </summary>
    Task UpdateSettingsAsync(UpdateNoShowSettingsCommand command);

    /// <summary>
    /// Gets no-show history for the site.
    /// </summary>
    Task<IReadOnlyList<NoShowCheckResult>> GetNoShowHistoryAsync(DateOnly? from = null, DateOnly? to = null, int limit = 100);

    Task<bool> ExistsAsync();
}
