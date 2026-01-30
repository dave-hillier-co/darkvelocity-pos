using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.GiftCards.Api.Entities;

/// <summary>
/// Defines a gift card program with rules for card issuance, redemption, and expiration
/// </summary>
public class GiftCardProgram : BaseEntity
{
    /// <summary>
    /// Tenant that owns this program
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Display name for the program (e.g., "Standard Gift Cards", "Holiday Promotion")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the program
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Prefix for card numbers generated under this program (e.g., "GC", "PROMO")
    /// </summary>
    public string CardNumberPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Number of months until cards expire (null = no expiration)
    /// </summary>
    public int? DefaultExpiryMonths { get; set; }

    /// <summary>
    /// Minimum amount that can be loaded onto a card
    /// </summary>
    public decimal MinimumLoadAmount { get; set; } = 5.00m;

    /// <summary>
    /// Maximum amount that can be loaded in a single transaction
    /// </summary>
    public decimal MaximumLoadAmount { get; set; } = 500.00m;

    /// <summary>
    /// Maximum balance a card can hold
    /// </summary>
    public decimal MaximumBalance { get; set; } = 1000.00m;

    /// <summary>
    /// Whether cards can be reloaded after initial activation
    /// </summary>
    public bool AllowReload { get; set; } = true;

    /// <summary>
    /// Whether partial redemption is allowed (vs. full balance only)
    /// </summary>
    public bool AllowPartialRedemption { get; set; } = true;

    /// <summary>
    /// Whether a PIN is required for redemption
    /// </summary>
    public bool RequirePin { get; set; } = false;

    /// <summary>
    /// Whether this program is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// ISO 4217 currency code (e.g., "EUR", "GBP", "USD")
    /// </summary>
    public string CurrencyCode { get; set; } = "EUR";

    // Navigation properties
    public ICollection<GiftCard> GiftCards { get; set; } = new List<GiftCard>();
    public ICollection<GiftCardDesign> Designs { get; set; } = new List<GiftCardDesign>();
}
