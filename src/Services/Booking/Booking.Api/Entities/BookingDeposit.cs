using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Booking.Api.Entities;

/// <summary>
/// Represents a deposit payment for a booking
/// </summary>
public class BookingDeposit : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public Guid BookingId { get; set; }

    /// <summary>
    /// Deposit amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency code (ISO 4217)
    /// </summary>
    public string CurrencyCode { get; set; } = "GBP";

    /// <summary>
    /// Current status of the deposit
    /// </summary>
    public string Status { get; set; } = "pending"; // pending, paid, refunded, forfeited, applied

    /// <summary>
    /// Payment method used
    /// </summary>
    public string? PaymentMethod { get; set; } // card, bank_transfer, cash, voucher

    // Payment processing details
    /// <summary>
    /// Stripe PaymentIntent ID if paid via Stripe
    /// </summary>
    public string? StripePaymentIntentId { get; set; }

    /// <summary>
    /// Card brand if paid by card
    /// </summary>
    public string? CardBrand { get; set; }

    /// <summary>
    /// Last four digits of card
    /// </summary>
    public string? CardLastFour { get; set; }

    /// <summary>
    /// External payment reference
    /// </summary>
    public string? PaymentReference { get; set; }

    // Timestamps
    public DateTime? PaidAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public DateTime? ForfeitedAt { get; set; }
    public DateTime? AppliedAt { get; set; }

    /// <summary>
    /// Refund amount if partially refunded
    /// </summary>
    public decimal? RefundAmount { get; set; }

    /// <summary>
    /// Reason for refund or forfeiture
    /// </summary>
    public string? RefundReason { get; set; }

    /// <summary>
    /// User who processed the refund
    /// </summary>
    public Guid? RefundedByUserId { get; set; }

    /// <summary>
    /// Order ID if deposit was applied to bill
    /// </summary>
    public Guid? AppliedToOrderId { get; set; }

    /// <summary>
    /// Notes about this deposit
    /// </summary>
    public string? Notes { get; set; }

    // Navigation property
    public Booking? Booking { get; set; }
}
