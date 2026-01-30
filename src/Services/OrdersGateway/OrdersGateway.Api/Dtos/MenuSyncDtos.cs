using DarkVelocity.OrdersGateway.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.OrdersGateway.Api.Dtos;

/// <summary>
/// Response DTO for a menu sync job.
/// </summary>
public class MenuSyncDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DeliveryPlatformId { get; set; }
    public Guid LocationId { get; set; }
    public MenuSyncStatus Status { get; set; }
    public int ItemsTotal { get; set; }
    public int ItemsSynced { get; set; }
    public int ItemsFailed { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<MenuSyncError>? Errors { get; set; }
    public MenuSyncTrigger TriggeredBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Error information for a failed menu item sync.
/// </summary>
public class MenuSyncError
{
    public Guid MenuItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Request to trigger a menu sync.
/// </summary>
public record TriggerMenuSyncRequest(
    Guid? LocationId = null,
    bool FullSync = false);

/// <summary>
/// Response DTO for a menu item mapping.
/// </summary>
public class MenuItemMappingDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DeliveryPlatformId { get; set; }
    public Guid InternalMenuItemId { get; set; }
    public string PlatformItemId { get; set; } = string.Empty;
    public string? PlatformCategoryId { get; set; }
    public decimal? PriceOverride { get; set; }
    public bool IsAvailable { get; set; }
    public Dictionary<string, string> ModifierMappings { get; set; } = new();
    public DateTime? LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request to update a menu item mapping.
/// </summary>
public record UpdateMenuItemMappingRequest(
    string? PlatformCategoryId = null,
    decimal? PriceOverride = null,
    bool? IsAvailable = null,
    Dictionary<string, string>? ModifierMappings = null);

/// <summary>
/// Request to bulk update menu item mappings.
/// </summary>
public record BulkUpdateMappingsRequest(
    List<BulkMappingUpdate> Updates);

/// <summary>
/// A single mapping update in a bulk operation.
/// </summary>
public record BulkMappingUpdate(
    Guid MappingId,
    decimal? PriceOverride = null,
    bool? IsAvailable = null);
