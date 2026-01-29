using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Booking.Api.Entities;

/// <summary>
/// Represents an entry on the waitlist for walk-ins or when no tables are available
/// </summary>
public class WaitlistEntry : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }

    /// <summary>
    /// Guest name
    /// </summary>
    public string GuestName { get; set; } = string.Empty;

    /// <summary>
    /// Guest phone number for notifications
    /// </summary>
    public string? GuestPhone { get; set; }

    /// <summary>
    /// Guest email (optional)
    /// </summary>
    public string? GuestEmail { get; set; }

    /// <summary>
    /// Party size
    /// </summary>
    public int PartySize { get; set; }

    /// <summary>
    /// Requested date (today for walk-ins, future for waitlist bookings)
    /// </summary>
    public DateOnly RequestedDate { get; set; }

    /// <summary>
    /// Preferred start time (null = any available)
    /// </summary>
    public TimeOnly? PreferredTime { get; set; }

    /// <summary>
    /// End of acceptable time window
    /// </summary>
    public TimeOnly? LatestAcceptableTime { get; set; }

    /// <summary>
    /// Current status
    /// </summary>
    public string Status { get; set; } = "waiting"; // waiting, notified, confirmed, seated, expired, cancelled

    /// <summary>
    /// Position in the queue
    /// </summary>
    public int QueuePosition { get; set; }

    /// <summary>
    /// Estimated wait time in minutes (calculated)
    /// </summary>
    public int? EstimatedWaitMinutes { get; set; }

    /// <summary>
    /// Time when guest was added to waitlist
    /// </summary>
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Time when offer was sent
    /// </summary>
    public DateTime? NotifiedAt { get; set; }

    /// <summary>
    /// Time when offer expires
    /// </summary>
    public DateTime? OfferExpiresAt { get; set; }

    /// <summary>
    /// Time when guest confirmed the offer
    /// </summary>
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>
    /// Time when guest was seated
    /// </summary>
    public DateTime? SeatedAt { get; set; }

    /// <summary>
    /// Time when entry expired or was cancelled
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Table offered (when status = notified)
    /// </summary>
    public Guid? OfferedTableId { get; set; }

    /// <summary>
    /// Booking created when waitlist entry is converted
    /// </summary>
    public Guid? ConvertedToBookingId { get; set; }

    /// <summary>
    /// Number of times guest has been notified
    /// </summary>
    public int NotificationCount { get; set; }

    /// <summary>
    /// Notes about this waitlist entry
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Source of the waitlist entry
    /// </summary>
    public string Source { get; set; } = "walk_in"; // walk_in, phone, online

    /// <summary>
    /// Preferred floor plan (null = any)
    /// </summary>
    public Guid? PreferredFloorPlanId { get; set; }

    /// <summary>
    /// Whether guest prefers indoor/outdoor (null = no preference)
    /// </summary>
    public string? SeatingPreference { get; set; } // indoor, outdoor, bar, any
}
