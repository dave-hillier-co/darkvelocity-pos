using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.OrdersGateway.Api.Entities;

/// <summary>
/// Maps a delivery platform to a specific location (many-to-many relationship).
/// </summary>
public class PlatformLocation : BaseEntity, ILocationScoped
{
    public Guid DeliveryPlatformId { get; set; }
    public Guid LocationId { get; set; }

    /// <summary>
    /// The platform's identifier for this specific store/location.
    /// </summary>
    public string PlatformStoreId { get; set; } = string.Empty;

    /// <summary>
    /// Whether this location is active on the platform.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional reference to which menu configuration to sync to this platform.
    /// </summary>
    public Guid? MenuMappingId { get; set; }

    /// <summary>
    /// Operating hours override as JSON (if different from standard location hours).
    /// </summary>
    public string? OperatingHoursOverride { get; set; }

    // Navigation property
    public DeliveryPlatform DeliveryPlatform { get; set; } = null!;
}
