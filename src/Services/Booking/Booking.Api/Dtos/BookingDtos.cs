using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Booking.Api.Dtos;

public class BookingDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public string BookingReference { get; set; } = string.Empty;

    // Table assignment
    public Guid? TableId { get; set; }
    public string? TableNumber { get; set; }
    public Guid? TableCombinationId { get; set; }
    public string? TableCombinationName { get; set; }

    // Guest information
    public string GuestName { get; set; } = string.Empty;
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public int PartySize { get; set; }
    public string? SpecialRequests { get; set; }
    public string? InternalNotes { get; set; }

    // Timing
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int DurationMinutes { get; set; }

    // Status
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? ExternalReference { get; set; }

    // Confirmation
    public bool IsConfirmed { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmationMethod { get; set; }

    // Timestamps
    public DateTime? ArrivedAt { get; set; }
    public DateTime? SeatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? MarkedNoShowAt { get; set; }

    // POS integration
    public Guid? OrderId { get; set; }

    // Additional info
    public bool IsVip { get; set; }
    public string? Tags { get; set; }
    public string? Occasion { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Deposits
    public List<BookingDepositDto> Deposits { get; set; } = new();
    public decimal TotalDepositAmount { get; set; }
    public decimal PaidDepositAmount { get; set; }
}

public class BookingDepositDto : HalResource
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
    public string? CardBrand { get; set; }
    public string? CardLastFour { get; set; }
    public string? PaymentReference { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public decimal? RefundAmount { get; set; }
    public string? RefundReason { get; set; }
    public Guid? AppliedToOrderId { get; set; }
    public DateTime? AppliedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BookingSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public int PartySize { get; set; }
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? TableNumber { get; set; }
    public bool IsVip { get; set; }
    public bool HasDeposit { get; set; }
}

// Request DTOs

public record CreateBookingRequest(
    string GuestName,
    int PartySize,
    DateOnly BookingDate,
    TimeOnly StartTime,
    int? DurationMinutes = null,
    Guid? TableId = null,
    Guid? TableCombinationId = null,
    string? GuestEmail = null,
    string? GuestPhone = null,
    string? SpecialRequests = null,
    string? InternalNotes = null,
    string Source = "phone",
    string? ExternalReference = null,
    bool IsVip = false,
    string? Tags = null,
    string? Occasion = null,
    Guid? CreatedByUserId = null);

public record UpdateBookingRequest(
    string? GuestName = null,
    int? PartySize = null,
    DateOnly? BookingDate = null,
    TimeOnly? StartTime = null,
    int? DurationMinutes = null,
    Guid? TableId = null,
    Guid? TableCombinationId = null,
    string? GuestEmail = null,
    string? GuestPhone = null,
    string? SpecialRequests = null,
    string? InternalNotes = null,
    bool? IsVip = null,
    string? Tags = null,
    string? Occasion = null);

public record ConfirmBookingRequest(
    string? ConfirmationMethod = null);

public record SeatBookingRequest(
    Guid? TableId = null,
    Guid? TableCombinationId = null);

public record LinkOrderRequest(
    Guid OrderId);

public record CancelBookingRequest(
    string? Reason = null,
    Guid? CancelledByUserId = null);

public record MarkNoShowRequest(
    Guid? MarkedByUserId = null);

public record CompleteBookingRequest();

// Deposit requests

public record CreateDepositRequest(
    decimal Amount,
    string CurrencyCode = "GBP",
    string? Notes = null);

public record RecordDepositPaymentRequest(
    string PaymentMethod,
    string? StripePaymentIntentId = null,
    string? CardBrand = null,
    string? CardLastFour = null,
    string? PaymentReference = null);

public record RefundDepositRequest(
    decimal? Amount = null,
    string? Reason = null,
    Guid? RefundedByUserId = null);

public record ApplyDepositToOrderRequest(
    Guid OrderId);

public record ForfeitDepositRequest(
    string? Reason = null);
