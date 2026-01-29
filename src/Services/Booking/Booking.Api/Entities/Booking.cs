using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Booking.Api.Entities;

/// <summary>
/// Represents a table reservation/booking
/// </summary>
public class Booking : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }

    /// <summary>
    /// Unique booking reference for customer communication (e.g., "BK-2024-ABC123")
    /// </summary>
    public string BookingReference { get; set; } = string.Empty;

    /// <summary>
    /// Table assigned to this booking (null if using a combination)
    /// </summary>
    public Guid? TableId { get; set; }

    /// <summary>
    /// Table combination assigned to this booking (for larger parties)
    /// </summary>
    public Guid? TableCombinationId { get; set; }

    // Guest Information
    public string GuestName { get; set; } = string.Empty;
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public int PartySize { get; set; }

    /// <summary>
    /// Special requests or notes from the guest
    /// </summary>
    public string? SpecialRequests { get; set; }

    /// <summary>
    /// Internal notes visible only to staff
    /// </summary>
    public string? InternalNotes { get; set; }

    // Timing
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    /// <summary>
    /// Expected duration in minutes
    /// </summary>
    public int DurationMinutes { get; set; }

    // Status tracking
    /// <summary>
    /// Current booking status
    /// </summary>
    public string Status { get; set; } = "pending"; // pending, confirmed, seated, completed, cancelled, no_show

    /// <summary>
    /// Source of the booking
    /// </summary>
    public string Source { get; set; } = "phone"; // phone, web, walk_in, third_party, staff

    /// <summary>
    /// Third-party booking reference (e.g., OpenTable, Resy)
    /// </summary>
    public string? ExternalReference { get; set; }

    // Confirmation tracking
    public bool IsConfirmed { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmationMethod { get; set; } // email, sms, phone

    // Arrival tracking
    public DateTime? ArrivedAt { get; set; }
    public DateTime? SeatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Cancellation tracking
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }

    // No-show tracking
    public DateTime? MarkedNoShowAt { get; set; }
    public Guid? MarkedNoShowByUserId { get; set; }

    /// <summary>
    /// Linked POS order ID when guest is seated and ordering
    /// </summary>
    public Guid? OrderId { get; set; }

    /// <summary>
    /// User who created the booking
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// VIP flag for special treatment
    /// </summary>
    public bool IsVip { get; set; }

    /// <summary>
    /// Tags for categorization (comma-separated)
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Occasion if specified (birthday, anniversary, business, etc.)
    /// </summary>
    public string? Occasion { get; set; }

    // Navigation properties
    public Table? Table { get; set; }
    public TableCombination? TableCombination { get; set; }
    public ICollection<BookingDeposit> Deposits { get; set; } = new List<BookingDeposit>();
}
