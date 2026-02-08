namespace DarkVelocity.Host.Events;

// ============================================================================
// Menu Sync Domain Events (for JournaledGrain event sourcing)
// ============================================================================

/// <summary>
/// Base interface for Menu Sync domain events (for JournaledGrain event sourcing).
/// </summary>
public interface IMenuSyncEvent
{
    Guid MenuSyncId { get; }
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Domain event: A menu sync was started.
/// </summary>
[GenerateSerializer]
public sealed record MenuSyncStarted(
    [property: Id(0)] Guid MenuSyncId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] Guid DeliveryPlatformId,
    [property: Id(3)] Guid? LocationId,
    [property: Id(4)] DateTimeOffset OccurredAt
) : IMenuSyncEvent;

/// <summary>
/// Domain event: A menu item was successfully synced to the platform.
/// </summary>
[GenerateSerializer]
public sealed record MenuSyncItemSynced(
    [property: Id(0)] Guid MenuSyncId,
    [property: Id(1)] Guid InternalMenuItemId,
    [property: Id(2)] string? PlatformItemId,
    [property: Id(3)] string? PlatformCategoryId,
    [property: Id(4)] decimal? PriceOverride,
    [property: Id(5)] bool IsAvailable,
    [property: Id(6)] DateTimeOffset OccurredAt
) : IMenuSyncEvent;

/// <summary>
/// Domain event: A menu item failed to sync to the platform.
/// </summary>
[GenerateSerializer]
public sealed record MenuSyncItemFailed(
    [property: Id(0)] Guid MenuSyncId,
    [property: Id(1)] Guid MenuItemId,
    [property: Id(2)] string Error,
    [property: Id(3)] DateTimeOffset OccurredAt
) : IMenuSyncEvent;

/// <summary>
/// Domain event: The menu sync completed successfully.
/// </summary>
[GenerateSerializer]
public sealed record MenuSyncCompleted(
    [property: Id(0)] Guid MenuSyncId,
    [property: Id(1)] DateTimeOffset OccurredAt
) : IMenuSyncEvent;

/// <summary>
/// Domain event: The menu sync failed.
/// </summary>
[GenerateSerializer]
public sealed record MenuSyncFailed(
    [property: Id(0)] Guid MenuSyncId,
    [property: Id(1)] string Error,
    [property: Id(2)] DateTimeOffset OccurredAt
) : IMenuSyncEvent;
