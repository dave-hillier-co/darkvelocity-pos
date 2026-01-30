using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.OrdersGateway.Api.Entities;

/// <summary>
/// Represents a third-party delivery platform integration (e.g., Uber Eats, DoorDash).
/// </summary>
public class DeliveryPlatform : BaseAuditableEntity
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// The type of platform (e.g., UberEats, DoorDash, Deliveroo, JustEat, Wolt, etc.)
    /// </summary>
    public string PlatformType { get; set; } = string.Empty;

    /// <summary>
    /// Display name for this platform connection.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the platform connection.
    /// </summary>
    public PlatformStatus Status { get; set; } = PlatformStatus.Disconnected;

    /// <summary>
    /// Encrypted API credentials for the platform.
    /// </summary>
    public string? ApiCredentialsEncrypted { get; set; }

    /// <summary>
    /// Secret for verifying webhook signatures from the platform.
    /// </summary>
    public string? WebhookSecret { get; set; }

    /// <summary>
    /// The platform's identifier for this merchant.
    /// </summary>
    public string? MerchantId { get; set; }

    /// <summary>
    /// Platform-specific settings as JSON.
    /// </summary>
    public string Settings { get; set; } = "{}";

    /// <summary>
    /// When the platform was first connected.
    /// </summary>
    public DateTime? ConnectedAt { get; set; }

    /// <summary>
    /// When the last menu/settings sync occurred.
    /// </summary>
    public DateTime? LastSyncAt { get; set; }

    /// <summary>
    /// When the last order was received from this platform.
    /// </summary>
    public DateTime? LastOrderAt { get; set; }

    // Navigation properties
    public ICollection<PlatformLocation> PlatformLocations { get; set; } = new List<PlatformLocation>();
    public ICollection<ExternalOrder> ExternalOrders { get; set; } = new List<ExternalOrder>();
    public ICollection<MenuSync> MenuSyncs { get; set; } = new List<MenuSync>();
    public ICollection<MenuItemMapping> MenuItemMappings { get; set; } = new List<MenuItemMapping>();
    public ICollection<PlatformPayout> Payouts { get; set; } = new List<PlatformPayout>();
}

public enum PlatformStatus
{
    Active,
    Paused,
    Disconnected,
    Error
}
