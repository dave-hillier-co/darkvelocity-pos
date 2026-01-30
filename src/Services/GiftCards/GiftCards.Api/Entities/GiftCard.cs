using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.GiftCards.Api.Entities;

/// <summary>
/// Represents a gift card with stored value
/// </summary>
public class GiftCard : BaseEntity, ILocationScoped
{
    /// <summary>
    /// Tenant that owns this card
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Location where the card was issued
    /// </summary>
    public Guid LocationId { get; set; }

    /// <summary>
    /// Program this card belongs to
    /// </summary>
    public Guid ProgramId { get; set; }

    /// <summary>
    /// Optional design for visual representation
    /// </summary>
    public Guid? DesignId { get; set; }

    /// <summary>
    /// Unique card number (16-19 digits, typically masked for display)
    /// </summary>
    public string CardNumber { get; set; } = string.Empty;

    /// <summary>
    /// Hashed PIN for redemption security (4-8 digits)
    /// </summary>
    public string? PinHash { get; set; }

    /// <summary>
    /// Initial balance when the card was activated
    /// </summary>
    public decimal InitialBalance { get; set; }

    /// <summary>
    /// Current available balance
    /// </summary>
    public decimal CurrentBalance { get; set; }

    /// <summary>
    /// ISO 4217 currency code
    /// </summary>
    public string CurrencyCode { get; set; } = "EUR";

    /// <summary>
    /// Current status of the card
    /// </summary>
    public string Status { get; set; } = "pending_activation"; // pending_activation, active, suspended, expired, depleted

    /// <summary>
    /// Type of card
    /// </summary>
    public string CardType { get; set; } = "physical"; // physical, digital, promotional

    /// <summary>
    /// When the card expires (null = no expiration)
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// When the card was issued/created
    /// </summary>
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the card was activated (null if not yet activated)
    /// </summary>
    public DateTime? ActivatedAt { get; set; }

    /// <summary>
    /// User who issued the card
    /// </summary>
    public Guid? IssuedByUserId { get; set; }

    /// <summary>
    /// User who activated the card
    /// </summary>
    public Guid? ActivatedByUserId { get; set; }

    /// <summary>
    /// When the card was last used for a transaction
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Optional recipient name for gifting
    /// </summary>
    public string? RecipientName { get; set; }

    /// <summary>
    /// Optional recipient email for digital cards
    /// </summary>
    public string? RecipientEmail { get; set; }

    /// <summary>
    /// Optional gift message
    /// </summary>
    public string? GiftMessage { get; set; }

    /// <summary>
    /// Optional purchaser/sender name
    /// </summary>
    public string? PurchaserName { get; set; }

    /// <summary>
    /// Optional purchaser email
    /// </summary>
    public string? PurchaserEmail { get; set; }

    /// <summary>
    /// Optional notes (internal use)
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// External reference for fiscal linkage
    /// </summary>
    public string? ExternalReference { get; set; }

    /// <summary>
    /// When the card was suspended (if applicable)
    /// </summary>
    public DateTime? SuspendedAt { get; set; }

    /// <summary>
    /// Reason for suspension
    /// </summary>
    public string? SuspensionReason { get; set; }

    /// <summary>
    /// User who suspended the card
    /// </summary>
    public Guid? SuspendedByUserId { get; set; }

    // Navigation properties
    public GiftCardProgram? Program { get; set; }
    public GiftCardDesign? Design { get; set; }
    public ICollection<GiftCardTransaction> Transactions { get; set; } = new List<GiftCardTransaction>();
}
