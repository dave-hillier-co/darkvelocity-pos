namespace DarkVelocity.Host.Events;

// ============================================================================
// Menu Item Document Events
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
