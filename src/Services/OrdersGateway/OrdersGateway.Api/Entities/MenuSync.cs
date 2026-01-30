using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.OrdersGateway.Api.Entities;

/// <summary>
/// Tracks menu synchronization jobs to delivery platforms.
/// </summary>
public class MenuSync : BaseEntity, ILocationScoped
{
    public Guid TenantId { get; set; }
    public Guid DeliveryPlatformId { get; set; }
    public Guid LocationId { get; set; }

    /// <summary>
    /// Current status of the sync operation.
    /// </summary>
    public MenuSyncStatus Status { get; set; } = MenuSyncStatus.Pending;

    /// <summary>
    /// Total number of menu items to sync.
    /// </summary>
    public int ItemsTotal { get; set; }

    /// <summary>
    /// Number of items successfully synced.
    /// </summary>
    public int ItemsSynced { get; set; }

    /// <summary>
    /// Number of items that failed to sync.
    /// </summary>
    public int ItemsFailed { get; set; }

    /// <summary>
    /// When the sync started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the sync completed (success or failure).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error log as JSON for failed items.
    /// </summary>
    public string? ErrorLog { get; set; }

    /// <summary>
    /// What triggered this sync.
    /// </summary>
    public MenuSyncTrigger TriggeredBy { get; set; } = MenuSyncTrigger.Manual;

    // Navigation property
    public DeliveryPlatform DeliveryPlatform { get; set; } = null!;
}

public enum MenuSyncStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

public enum MenuSyncTrigger
{
    Manual,
    Scheduled,
    MenuChange
}
