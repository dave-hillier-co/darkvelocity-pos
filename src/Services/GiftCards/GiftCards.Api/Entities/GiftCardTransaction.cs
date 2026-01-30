using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.GiftCards.Api.Entities;

/// <summary>
/// Records all transactions (activations, redemptions, reloads, adjustments) for a gift card
/// </summary>
public class GiftCardTransaction : BaseEntity, ILocationScoped
{
    /// <summary>
    /// Gift card this transaction belongs to
    /// </summary>
    public Guid GiftCardId { get; set; }

    /// <summary>
    /// Location where the transaction occurred
    /// </summary>
    public Guid LocationId { get; set; }

    /// <summary>
    /// Type of transaction
    /// </summary>
    public string TransactionType { get; set; } = string.Empty; // activation, redemption, reload, adjustment, refund, expiry

    /// <summary>
    /// Amount of the transaction (positive for credits, negative for debits)
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Balance before this transaction
    /// </summary>
    public decimal BalanceBefore { get; set; }

    /// <summary>
    /// Balance after this transaction
    /// </summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>
    /// Associated order ID (for redemptions and refunds)
    /// </summary>
    public Guid? OrderId { get; set; }

    /// <summary>
    /// Associated payment ID (for redemptions)
    /// </summary>
    public Guid? PaymentId { get; set; }

    /// <summary>
    /// User who processed the transaction
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Reason for the transaction (especially for adjustments)
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// When the transaction was processed
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// External reference for fiscal linkage
    /// </summary>
    public string? ExternalReference { get; set; }

    /// <summary>
    /// Additional notes
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Reference number for this transaction (for receipts)
    /// </summary>
    public string? TransactionReference { get; set; }

    // Navigation properties
    public GiftCard? GiftCard { get; set; }
}
