using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.OrdersGateway.Api.Entities;

/// <summary>
/// Tracks payouts received from delivery platforms for reconciliation.
/// </summary>
public class PlatformPayout : BaseEntity, ILocationScoped
{
    public Guid TenantId { get; set; }
    public Guid DeliveryPlatformId { get; set; }
    public Guid LocationId { get; set; }

    /// <summary>
    /// Platform's reference number for this payout.
    /// </summary>
    public string PayoutReference { get; set; } = string.Empty;

    /// <summary>
    /// Start date of the payout period.
    /// </summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>
    /// End date of the payout period.
    /// </summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>
    /// Gross amount before deductions.
    /// </summary>
    public decimal GrossAmount { get; set; }

    /// <summary>
    /// Platform commissions deducted.
    /// </summary>
    public decimal Commissions { get; set; }

    /// <summary>
    /// Other fees deducted.
    /// </summary>
    public decimal Fees { get; set; }

    /// <summary>
    /// Adjustments (refunds, corrections, etc.).
    /// </summary>
    public decimal Adjustments { get; set; }

    /// <summary>
    /// Net amount received.
    /// </summary>
    public decimal NetAmount { get; set; }

    /// <summary>
    /// Currency code (ISO 4217).
    /// </summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>
    /// Current reconciliation status.
    /// </summary>
    public PayoutStatus Status { get; set; } = PayoutStatus.Pending;

    /// <summary>
    /// When the payout was received/processed.
    /// </summary>
    public DateTime ReceivedAt { get; set; }

    /// <summary>
    /// List of order IDs included in this payout as JSON array.
    /// </summary>
    public string OrderIds { get; set; } = "[]";

    // Navigation property
    public DeliveryPlatform DeliveryPlatform { get; set; } = null!;
}

public enum PayoutStatus
{
    Pending,
    Reconciled,
    Disputed
}
