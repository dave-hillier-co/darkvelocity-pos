namespace DarkVelocity.Host.Events;

// ============================================================================
// Domain Event Interfaces for Event Sourcing
// ============================================================================

/// <summary>
/// Base interface for Menu Item Document domain events (for JournaledGrain event sourcing).
/// </summary>
public interface IMenuItemDocumentEvent
{
    string DocumentId { get; }
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Base interface for Menu Category Document domain events.
/// </summary>
public interface IMenuCategoryDocumentEvent
{
    string DocumentId { get; }
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Base interface for Modifier Block domain events.
/// </summary>
public interface IModifierBlockEvent
{
    string BlockId { get; }
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Base interface for Content Tag domain events.
/// </summary>
public interface IContentTagEvent
{
    string TagId { get; }
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Base interface for Site Menu Overrides domain events.
/// </summary>
public interface ISiteMenuOverridesEvent
{
    Guid SiteId { get; }
    DateTimeOffset OccurredAt { get; }
}

// ============================================================================
// Menu Item Document Domain Events (for JournaledGrain)
// ============================================================================

/// <summary>
/// Domain event: A menu item document was initialized.
/// </summary>
[GenerateSerializer]
public sealed record MenuItemDocumentInitialized(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] DateTimeOffset OccurredAt,
    [property: Id(3)] string Name,
    [property: Id(4)] decimal Price,
    [property: Id(5)] string? Description,
    [property: Id(6)] Guid? CategoryId,
    [property: Id(7)] Guid? AccountingGroupId,
    [property: Id(8)] Guid? RecipeId,
    [property: Id(9)] string? ImageUrl,
    [property: Id(10)] string? Sku,
    [property: Id(11)] bool TrackInventory,
    [property: Id(12)] string Locale,
    [property: Id(13)] Guid? CreatedBy,
    [property: Id(14)] bool PublishImmediately
) : IMenuItemDocumentEvent;

/// <summary>
/// Domain event: A draft version was created.
/// </summary>
[GenerateSerializer]
public sealed record MenuItemDraftVersionCreated(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int VersionNumber,
    [property: Id(3)] Guid? CreatedBy,
    [property: Id(4)] string? ChangeNote,
    [property: Id(5)] string? Name,
    [property: Id(6)] decimal? Price,
    [property: Id(7)] string? Description,
    [property: Id(8)] string? ImageUrl,
    [property: Id(9)] Guid? CategoryId,
    [property: Id(10)] Guid? AccountingGroupId,
    [property: Id(11)] Guid? RecipeId,
    [property: Id(12)] string? Sku,
    [property: Id(13)] bool? TrackInventory,
    [property: Id(14)] List<string>? ModifierBlockIds,
    [property: Id(15)] List<string>? TagIds
) : IMenuItemDocumentEvent;

/// <summary>
/// Domain event: A draft was published.
/// </summary>
[GenerateSerializer]
public sealed record MenuItemDraftWasPublished(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int PublishedVersion,
    [property: Id(3)] int? PreviousPublishedVersion,
    [property: Id(4)] Guid? PublishedBy,
    [property: Id(5)] string? Note
) : IMenuItemDocumentEvent;

/// <summary>
/// Domain event: A draft was discarded.
/// </summary>
[GenerateSerializer]
public sealed record MenuItemDraftDiscarded(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int DiscardedVersion
) : IMenuItemDocumentEvent;

/// <summary>
/// Domain event: Reverted to a previous version.
/// </summary>
[GenerateSerializer]
public sealed record MenuItemRevertedToVersion(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int FromVersion,
    [property: Id(3)] int ToVersion,
    [property: Id(4)] int NewVersionNumber,
    [property: Id(5)] Guid? RevertedBy,
    [property: Id(6)] string? Reason
) : IMenuItemDocumentEvent;

/// <summary>
/// Domain event: A translation was added.
/// </summary>
[GenerateSerializer]
public sealed record MenuItemTranslationAdded(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string Locale,
    [property: Id(3)] string Name,
    [property: Id(4)] string? Description,
    [property: Id(5)] string? KitchenName
) : IMenuItemDocumentEvent;

/// <summary>
/// Domain event: A translation was removed.
/// </summary>
[GenerateSerializer]
public sealed record MenuItemTranslationRemoved(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string Locale
) : IMenuItemDocumentEvent;

/// <summary>
/// Domain event: A change was scheduled.
/// </summary>
[GenerateSerializer]
public sealed record MenuItemChangeWasScheduled(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string ScheduleId,
    [property: Id(3)] int VersionToActivate,
    [property: Id(4)] DateTimeOffset ActivateAt,
    [property: Id(5)] DateTimeOffset? DeactivateAt,
    [property: Id(6)] string? Name
) : IMenuItemDocumentEvent;

/// <summary>
/// Domain event: A schedule was cancelled.
/// </summary>
[GenerateSerializer]
public sealed record MenuItemScheduleWasCancelled(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string ScheduleId
) : IMenuItemDocumentEvent;

/// <summary>
/// Domain event: Document was archived.
/// </summary>
[GenerateSerializer]
public sealed record MenuItemDocumentWasArchived(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid? ArchivedBy,
    [property: Id(3)] string? Reason
) : IMenuItemDocumentEvent;

/// <summary>
/// Domain event: Document was restored from archive.
/// </summary>
[GenerateSerializer]
public sealed record MenuItemDocumentWasRestored(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid? RestoredBy
) : IMenuItemDocumentEvent;

// ============================================================================
// Menu Category Document Domain Events (for JournaledGrain)
// ============================================================================

/// <summary>
/// Domain event: A menu category document was initialized.
/// </summary>
[GenerateSerializer]
public sealed record MenuCategoryDocumentInitialized(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] DateTimeOffset OccurredAt,
    [property: Id(3)] string Name,
    [property: Id(4)] int DisplayOrder,
    [property: Id(5)] string? Description,
    [property: Id(6)] string? Color,
    [property: Id(7)] string? IconUrl,
    [property: Id(8)] string Locale,
    [property: Id(9)] Guid? CreatedBy,
    [property: Id(10)] bool PublishImmediately
) : IMenuCategoryDocumentEvent;

/// <summary>
/// Domain event: A category draft version was created.
/// </summary>
[GenerateSerializer]
public sealed record MenuCategoryDraftVersionCreated(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int VersionNumber,
    [property: Id(3)] Guid? CreatedBy,
    [property: Id(4)] string? ChangeNote,
    [property: Id(5)] string? Name,
    [property: Id(6)] int? DisplayOrder,
    [property: Id(7)] string? Description,
    [property: Id(8)] string? Color,
    [property: Id(9)] string? IconUrl,
    [property: Id(10)] List<string>? ItemDocumentIds
) : IMenuCategoryDocumentEvent;

/// <summary>
/// Domain event: A category draft was published.
/// </summary>
[GenerateSerializer]
public sealed record MenuCategoryDraftWasPublished(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int PublishedVersion,
    [property: Id(3)] int? PreviousPublishedVersion,
    [property: Id(4)] Guid? PublishedBy,
    [property: Id(5)] string? Note
) : IMenuCategoryDocumentEvent;

/// <summary>
/// Domain event: A category draft was discarded.
/// </summary>
[GenerateSerializer]
public sealed record MenuCategoryDraftDiscarded(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int DiscardedVersion
) : IMenuCategoryDocumentEvent;

/// <summary>
/// Domain event: Category reverted to a previous version.
/// </summary>
[GenerateSerializer]
public sealed record MenuCategoryRevertedToVersion(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int FromVersion,
    [property: Id(3)] int ToVersion,
    [property: Id(4)] int NewVersionNumber,
    [property: Id(5)] Guid? RevertedBy,
    [property: Id(6)] string? Reason
) : IMenuCategoryDocumentEvent;

/// <summary>
/// Domain event: An item was added to the category.
/// </summary>
[GenerateSerializer]
public sealed record MenuCategoryItemAdded(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string ItemDocumentId
) : IMenuCategoryDocumentEvent;

/// <summary>
/// Domain event: An item was removed from the category.
/// </summary>
[GenerateSerializer]
public sealed record MenuCategoryItemRemoved(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string ItemDocumentId
) : IMenuCategoryDocumentEvent;

/// <summary>
/// Domain event: Items were reordered in the category.
/// </summary>
[GenerateSerializer]
public sealed record MenuCategoryItemsReordered(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] List<string> ItemDocumentIds
) : IMenuCategoryDocumentEvent;

/// <summary>
/// Domain event: A category change was scheduled.
/// </summary>
[GenerateSerializer]
public sealed record MenuCategoryChangeWasScheduled(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string ScheduleId,
    [property: Id(3)] int VersionToActivate,
    [property: Id(4)] DateTimeOffset ActivateAt,
    [property: Id(5)] DateTimeOffset? DeactivateAt,
    [property: Id(6)] string? Name
) : IMenuCategoryDocumentEvent;

/// <summary>
/// Domain event: A category schedule was cancelled.
/// </summary>
[GenerateSerializer]
public sealed record MenuCategoryScheduleWasCancelled(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string ScheduleId
) : IMenuCategoryDocumentEvent;

/// <summary>
/// Domain event: Category was archived.
/// </summary>
[GenerateSerializer]
public sealed record MenuCategoryDocumentWasArchived(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid? ArchivedBy,
    [property: Id(3)] string? Reason
) : IMenuCategoryDocumentEvent;

/// <summary>
/// Domain event: Category was restored from archive.
/// </summary>
[GenerateSerializer]
public sealed record MenuCategoryDocumentWasRestored(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid? RestoredBy
) : IMenuCategoryDocumentEvent;

// ============================================================================
// Modifier Block Domain Events (for JournaledGrain)
// ============================================================================

/// <summary>
/// Domain event: A modifier block was initialized.
/// </summary>
[GenerateSerializer]
public sealed record ModifierBlockInitialized(
    [property: Id(0)] string BlockId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] DateTimeOffset OccurredAt,
    [property: Id(3)] string Name,
    [property: Id(4)] State.ModifierSelectionRule SelectionRule,
    [property: Id(5)] int MinSelections,
    [property: Id(6)] int MaxSelections,
    [property: Id(7)] bool IsRequired,
    [property: Id(8)] List<ModifierOptionData>? Options,
    [property: Id(9)] Guid? CreatedBy,
    [property: Id(10)] bool PublishImmediately
) : IModifierBlockEvent;

/// <summary>
/// Data transfer object for modifier options in events.
/// </summary>
[GenerateSerializer]
public sealed record ModifierOptionData(
    [property: Id(0)] string OptionId,
    [property: Id(1)] string Name,
    [property: Id(2)] decimal PriceAdjustment,
    [property: Id(3)] bool IsDefault,
    [property: Id(4)] int DisplayOrder,
    [property: Id(5)] decimal? ServingSize,
    [property: Id(6)] string? ServingUnit,
    [property: Id(7)] Guid? InventoryItemId
);

/// <summary>
/// Domain event: A modifier block draft was created.
/// </summary>
[GenerateSerializer]
public sealed record ModifierBlockDraftVersionCreated(
    [property: Id(0)] string BlockId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int VersionNumber,
    [property: Id(3)] Guid? CreatedBy,
    [property: Id(4)] string? ChangeNote,
    [property: Id(5)] string? Name,
    [property: Id(6)] State.ModifierSelectionRule? SelectionRule,
    [property: Id(7)] int? MinSelections,
    [property: Id(8)] int? MaxSelections,
    [property: Id(9)] bool? IsRequired,
    [property: Id(10)] List<ModifierOptionData>? Options
) : IModifierBlockEvent;

/// <summary>
/// Domain event: A modifier block draft was published.
/// </summary>
[GenerateSerializer]
public sealed record ModifierBlockDraftWasPublished(
    [property: Id(0)] string BlockId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int PublishedVersion,
    [property: Id(3)] int? PreviousPublishedVersion,
    [property: Id(4)] Guid? PublishedBy,
    [property: Id(5)] string? Note
) : IModifierBlockEvent;

/// <summary>
/// Domain event: A modifier block draft was discarded.
/// </summary>
[GenerateSerializer]
public sealed record ModifierBlockDraftDiscarded(
    [property: Id(0)] string BlockId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int DiscardedVersion
) : IModifierBlockEvent;

/// <summary>
/// Domain event: Modifier block usage was registered.
/// </summary>
[GenerateSerializer]
public sealed record ModifierBlockUsageRegistered(
    [property: Id(0)] string BlockId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string ItemDocumentId
) : IModifierBlockEvent;

/// <summary>
/// Domain event: Modifier block usage was unregistered.
/// </summary>
[GenerateSerializer]
public sealed record ModifierBlockUsageUnregistered(
    [property: Id(0)] string BlockId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string ItemDocumentId
) : IModifierBlockEvent;

/// <summary>
/// Domain event: Modifier block was archived.
/// </summary>
[GenerateSerializer]
public sealed record ModifierBlockWasArchived(
    [property: Id(0)] string BlockId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid? ArchivedBy,
    [property: Id(3)] string? Reason
) : IModifierBlockEvent;

/// <summary>
/// Domain event: Modifier block was restored from archive.
/// </summary>
[GenerateSerializer]
public sealed record ModifierBlockWasRestored(
    [property: Id(0)] string BlockId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid? RestoredBy
) : IModifierBlockEvent;

// ============================================================================
// Integration Events (for external notifications, NOT event sourcing)
// ============================================================================

// ============================================================================
// Menu Item Document Integration Events
// ============================================================================

/// <summary>
/// A new menu item document was created.
/// </summary>
public sealed record MenuItemDocumentCreated(
    string DocumentId,
    Guid OrgId,
    int Version,
    string Name,
    decimal Price,
    Guid? CategoryId
) : IntegrationEvent
{
    public override string EventType => "menu.item.document.created";
}

/// <summary>
/// A new draft version was created for a menu item document.
/// </summary>
public sealed record MenuItemDraftCreated(
    string DocumentId,
    Guid OrgId,
    int Version,
    Guid? CreatedBy,
    string? ChangeNote
) : IntegrationEvent
{
    public override string EventType => "menu.item.draft.created";
}

/// <summary>
/// A menu item draft was published to become the live version.
/// </summary>
public sealed record MenuItemDraftPublished(
    string DocumentId,
    Guid OrgId,
    int Version,
    int? PreviousPublishedVersion,
    Guid? PublishedBy
) : IntegrationEvent
{
    public override string EventType => "menu.item.draft.published";
}

/// <summary>
/// A menu item was reverted to a previous version.
/// </summary>
public sealed record MenuItemVersionReverted(
    string DocumentId,
    Guid OrgId,
    int FromVersion,
    int ToVersion,
    Guid? RevertedBy,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "menu.item.version.reverted";
}

/// <summary>
/// A scheduled change was created for a menu item.
/// </summary>
public sealed record MenuItemChangeScheduled(
    string DocumentId,
    Guid OrgId,
    string ScheduleId,
    int Version,
    DateTimeOffset ActivateAt,
    DateTimeOffset? DeactivateAt
) : IntegrationEvent
{
    public override string EventType => "menu.item.change.scheduled";
}

/// <summary>
/// A scheduled change was cancelled for a menu item.
/// </summary>
public sealed record MenuItemScheduleCancelled(
    string DocumentId,
    Guid OrgId,
    string ScheduleId,
    Guid? CancelledBy
) : IntegrationEvent
{
    public override string EventType => "menu.item.schedule.cancelled";
}

/// <summary>
/// A menu item document was archived.
/// </summary>
public sealed record MenuItemDocumentArchived(
    string DocumentId,
    Guid OrgId,
    Guid? ArchivedBy,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "menu.item.document.archived";
}

/// <summary>
/// A menu item document was restored from archive.
/// </summary>
public sealed record MenuItemDocumentRestored(
    string DocumentId,
    Guid OrgId,
    Guid? RestoredBy
) : IntegrationEvent
{
    public override string EventType => "menu.item.document.restored";
}

// ============================================================================
// Menu Category Document Events
// ============================================================================

/// <summary>
/// A new menu category document was created.
/// </summary>
public sealed record MenuCategoryDocumentCreated(
    string DocumentId,
    Guid OrgId,
    int Version,
    string Name,
    int DisplayOrder
) : IntegrationEvent
{
    public override string EventType => "menu.category.document.created";
}

/// <summary>
/// A new draft version was created for a menu category document.
/// </summary>
public sealed record MenuCategoryDraftCreated(
    string DocumentId,
    Guid OrgId,
    int Version,
    Guid? CreatedBy,
    string? ChangeNote
) : IntegrationEvent
{
    public override string EventType => "menu.category.draft.created";
}

/// <summary>
/// A menu category draft was published to become the live version.
/// </summary>
public sealed record MenuCategoryDraftPublished(
    string DocumentId,
    Guid OrgId,
    int Version,
    int? PreviousPublishedVersion,
    Guid? PublishedBy
) : IntegrationEvent
{
    public override string EventType => "menu.category.draft.published";
}

/// <summary>
/// A menu category document was archived.
/// </summary>
public sealed record MenuCategoryDocumentArchived(
    string DocumentId,
    Guid OrgId,
    Guid? ArchivedBy,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "menu.category.document.archived";
}

// ============================================================================
// Modifier Block Events
// ============================================================================

/// <summary>
/// A new modifier block was created.
/// </summary>
public sealed record ModifierBlockCreated(
    string BlockId,
    Guid OrgId,
    int Version,
    string Name
) : IntegrationEvent
{
    public override string EventType => "menu.modifier.block.created";
}

/// <summary>
/// A new draft version was created for a modifier block.
/// </summary>
public sealed record ModifierBlockDraftCreated(
    string BlockId,
    Guid OrgId,
    int Version,
    Guid? CreatedBy,
    string? ChangeNote
) : IntegrationEvent
{
    public override string EventType => "menu.modifier.draft.created";
}

/// <summary>
/// A modifier block draft was published.
/// </summary>
public sealed record ModifierBlockDraftPublished(
    string BlockId,
    Guid OrgId,
    int Version,
    int? PreviousPublishedVersion,
    Guid? PublishedBy
) : IntegrationEvent
{
    public override string EventType => "menu.modifier.draft.published";
}

/// <summary>
/// A modifier block was archived.
/// </summary>
public sealed record ModifierBlockArchived(
    string BlockId,
    Guid OrgId,
    Guid? ArchivedBy,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "menu.modifier.block.archived";
}

// ============================================================================
// Content Tag Events
// ============================================================================

/// <summary>
/// A new content tag was created.
/// </summary>
public sealed record ContentTagCreated(
    string TagId,
    Guid OrgId,
    string Name,
    State.TagCategory Category
) : IntegrationEvent
{
    public override string EventType => "menu.tag.created";
}

/// <summary>
/// A content tag was updated.
/// </summary>
public sealed record ContentTagUpdated(
    string TagId,
    Guid OrgId,
    string Name
) : IntegrationEvent
{
    public override string EventType => "menu.tag.updated";
}

/// <summary>
/// A content tag was archived.
/// </summary>
public sealed record ContentTagArchived(
    string TagId,
    Guid OrgId,
    Guid? ArchivedBy
) : IntegrationEvent
{
    public override string EventType => "menu.tag.archived";
}

// ============================================================================
// Site Menu Override Events
// ============================================================================

/// <summary>
/// A site price override was applied.
/// </summary>
public sealed record SitePriceOverrideApplied(
    Guid OrgId,
    Guid SiteId,
    string ItemDocumentId,
    decimal Price,
    decimal? PreviousPrice,
    Guid? AppliedBy
) : IntegrationEvent
{
    public override string EventType => "menu.site.price.override.applied";
}

/// <summary>
/// A site price override was removed.
/// </summary>
public sealed record SitePriceOverrideRemoved(
    Guid OrgId,
    Guid SiteId,
    string ItemDocumentId,
    Guid? RemovedBy
) : IntegrationEvent
{
    public override string EventType => "menu.site.price.override.removed";
}

/// <summary>
/// An item was hidden at a site.
/// </summary>
public sealed record SiteItemHidden(
    Guid OrgId,
    Guid SiteId,
    string ItemDocumentId,
    Guid? HiddenBy
) : IntegrationEvent
{
    public override string EventType => "menu.site.item.hidden";
}

/// <summary>
/// An item was unhidden at a site.
/// </summary>
public sealed record SiteItemUnhidden(
    Guid OrgId,
    Guid SiteId,
    string ItemDocumentId,
    Guid? UnhiddenBy
) : IntegrationEvent
{
    public override string EventType => "menu.site.item.unhidden";
}

/// <summary>
/// A category was hidden at a site.
/// </summary>
public sealed record SiteCategoryHidden(
    Guid OrgId,
    Guid SiteId,
    string CategoryDocumentId,
    Guid? HiddenBy
) : IntegrationEvent
{
    public override string EventType => "menu.site.category.hidden";
}

/// <summary>
/// A category was unhidden at a site.
/// </summary>
public sealed record SiteCategoryUnhidden(
    Guid OrgId,
    Guid SiteId,
    string CategoryDocumentId,
    Guid? UnhiddenBy
) : IntegrationEvent
{
    public override string EventType => "menu.site.category.unhidden";
}

/// <summary>
/// An item was snoozed (temporarily unavailable) at a site.
/// </summary>
public sealed record SiteItemSnoozed(
    Guid OrgId,
    Guid SiteId,
    string ItemDocumentId,
    DateTimeOffset? SnoozedUntil,
    Guid? SnoozedBy,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "menu.site.item.snoozed";
}

/// <summary>
/// An item was unsnoozed at a site.
/// </summary>
public sealed record SiteItemUnsnoozed(
    Guid OrgId,
    Guid SiteId,
    string ItemDocumentId,
    Guid? UnsnoozedBy
) : IntegrationEvent
{
    public override string EventType => "menu.site.item.unsnoozed";
}

/// <summary>
/// An availability window was added at a site.
/// </summary>
public sealed record AvailabilityWindowAdded(
    Guid OrgId,
    Guid SiteId,
    string WindowId,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime
) : IntegrationEvent
{
    public override string EventType => "menu.site.availability.added";
}

/// <summary>
/// An availability window was removed at a site.
/// </summary>
public sealed record AvailabilityWindowRemoved(
    Guid OrgId,
    Guid SiteId,
    string WindowId,
    Guid? RemovedBy
) : IntegrationEvent
{
    public override string EventType => "menu.site.availability.removed";
}

// ============================================================================
// Menu Resolution Events
// ============================================================================

/// <summary>
/// The effective menu was resolved for a site.
/// Used for caching invalidation signals.
/// </summary>
public sealed record EffectiveMenuResolved(
    Guid OrgId,
    Guid SiteId,
    string Channel,
    string ETag,
    int CategoryCount,
    int ItemCount
) : IntegrationEvent
{
    public override string EventType => "menu.effective.resolved";
}

/// <summary>
/// The menu cache was invalidated for a site.
/// </summary>
public sealed record MenuCacheInvalidated(
    Guid OrgId,
    Guid? SiteId,
    string Reason
) : IntegrationEvent
{
    public override string EventType => "menu.cache.invalidated";
}
