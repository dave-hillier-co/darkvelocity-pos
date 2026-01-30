using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.OrdersGateway.Api.Entities;

/// <summary>
/// Maps internal menu items to platform-specific item IDs.
/// </summary>
public class MenuItemMapping : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid DeliveryPlatformId { get; set; }

    /// <summary>
    /// Internal DarkVelocity menu item ID.
    /// </summary>
    public Guid InternalMenuItemId { get; set; }

    /// <summary>
    /// The platform's item ID.
    /// </summary>
    public string PlatformItemId { get; set; } = string.Empty;

    /// <summary>
    /// The platform's category ID for this item.
    /// </summary>
    public string? PlatformCategoryId { get; set; }

    /// <summary>
    /// Optional price override for this platform (can be different from internal price).
    /// </summary>
    public decimal? PriceOverride { get; set; }

    /// <summary>
    /// Whether this item is currently available on the platform.
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Modifier mappings as JSON (maps internal modifiers to platform modifiers).
    /// </summary>
    public string ModifierMappings { get; set; } = "{}";

    /// <summary>
    /// When this mapping was last synced to the platform.
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }

    // Navigation property
    public DeliveryPlatform DeliveryPlatform { get; set; } = null!;
}
