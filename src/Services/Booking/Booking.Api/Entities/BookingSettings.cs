using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Booking.Api.Entities;

/// <summary>
/// Location-level booking configuration settings
/// </summary>
public class BookingSettings : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }

    // Booking Window
    /// <summary>
    /// How many days in advance online bookings can be made
    /// </summary>
    public int BookingWindowDays { get; set; } = 30;

    /// <summary>
    /// Minimum hours notice required for online bookings
    /// </summary>
    public int MinAdvanceHours { get; set; } = 2;

    /// <summary>
    /// Maximum hours in advance that bookings can be made
    /// </summary>
    public int? MaxAdvanceHours { get; set; }

    // Party Size
    /// <summary>
    /// Minimum party size for online bookings
    /// </summary>
    public int MinPartySize { get; set; } = 1;

    /// <summary>
    /// Maximum party size for online bookings (larger requires calling)
    /// </summary>
    public int MaxOnlinePartySize { get; set; } = 8;

    /// <summary>
    /// Absolute maximum party size (including phone bookings)
    /// </summary>
    public int MaxPartySize { get; set; } = 20;

    // Duration
    /// <summary>
    /// Default booking duration in minutes
    /// </summary>
    public int DefaultDurationMinutes { get; set; } = 90;

    /// <summary>
    /// Minimum booking duration in minutes
    /// </summary>
    public int MinDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum booking duration in minutes
    /// </summary>
    public int MaxDurationMinutes { get; set; } = 180;

    // Buffer times
    /// <summary>
    /// Buffer time in minutes between bookings for the same table
    /// </summary>
    public int TableTurnBufferMinutes { get; set; } = 15;

    /// <summary>
    /// Grace period in minutes before marking as no-show
    /// </summary>
    public int NoShowGracePeriodMinutes { get; set; } = 15;

    // Confirmation
    /// <summary>
    /// Whether to require confirmation for bookings
    /// </summary>
    public bool RequireConfirmation { get; set; } = true;

    /// <summary>
    /// Hours before booking to send confirmation reminder
    /// </summary>
    public int ConfirmationReminderHours { get; set; } = 24;

    /// <summary>
    /// Whether to auto-confirm online bookings
    /// </summary>
    public bool AutoConfirmOnlineBookings { get; set; } = true;

    // Online booking settings
    /// <summary>
    /// Whether online booking is enabled
    /// </summary>
    public bool OnlineBookingEnabled { get; set; } = true;

    /// <summary>
    /// Whether to show available tables in online booking
    /// </summary>
    public bool ShowAvailableTables { get; set; }

    /// <summary>
    /// Whether guests can select specific tables online
    /// </summary>
    public bool AllowTableSelection { get; set; }

    /// <summary>
    /// Whether to require guest phone number
    /// </summary>
    public bool RequirePhone { get; set; } = true;

    /// <summary>
    /// Whether to require guest email
    /// </summary>
    public bool RequireEmail { get; set; } = true;

    // Cancellation
    /// <summary>
    /// Hours before booking that free cancellation is allowed
    /// </summary>
    public int FreeCancellationHours { get; set; } = 24;

    /// <summary>
    /// Whether guests can cancel their own bookings online
    /// </summary>
    public bool AllowOnlineCancellation { get; set; } = true;

    // Waitlist
    /// <summary>
    /// Whether waitlist is enabled
    /// </summary>
    public bool WaitlistEnabled { get; set; } = true;

    /// <summary>
    /// Maximum waitlist size
    /// </summary>
    public int MaxWaitlistSize { get; set; } = 20;

    /// <summary>
    /// Minutes a waitlist offer is valid before expiring
    /// </summary>
    public int WaitlistOfferExpiryMinutes { get; set; } = 15;

    // Display
    /// <summary>
    /// Timezone for displaying times (IANA timezone)
    /// </summary>
    public string Timezone { get; set; } = "Europe/London";

    /// <summary>
    /// Custom booking terms and conditions
    /// </summary>
    public string? TermsAndConditions { get; set; }

    /// <summary>
    /// Custom confirmation message
    /// </summary>
    public string? ConfirmationMessage { get; set; }

    /// <summary>
    /// Custom cancellation policy text
    /// </summary>
    public string? CancellationPolicyText { get; set; }
}
