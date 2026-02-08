using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Menu Item Document Grain Commands & Snapshots
// ============================================================================

/// <summary>
/// Command to create a new menu item document.
/// </summary>
[GenerateSerializer]
public record CreateMenuItemDocumentCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] decimal Price,
    [property: Id(2)] string? Description = null,
    [property: Id(3)] Guid? CategoryId = null,
    [property: Id(4)] Guid? AccountingGroupId = null,
    [property: Id(5)] Guid? RecipeId = null,
    [property: Id(6)] string? ImageUrl = null,
    [property: Id(7)] string? Sku = null,
    [property: Id(8)] bool TrackInventory = false,
    [property: Id(9)] string Locale = "en-US",
    [property: Id(10)] Guid? CreatedBy = null,
    [property: Id(11)] bool PublishImmediately = false,
    [property: Id(12)] NutritionInfoData? Nutrition = null,
    [property: Id(13)] IReadOnlyList<string>? TagIds = null);

/// <summary>
/// Command to create a draft version of a menu item document.
/// </summary>
[GenerateSerializer]
public record CreateMenuItemDraftCommand(
    [property: Id(0)] string? Name = null,
    [property: Id(1)] decimal? Price = null,
    [property: Id(2)] string? Description = null,
    [property: Id(3)] Guid? CategoryId = null,
    [property: Id(4)] Guid? AccountingGroupId = null,
    [property: Id(5)] Guid? RecipeId = null,
    [property: Id(6)] string? ImageUrl = null,
    [property: Id(7)] string? Sku = null,
    [property: Id(8)] bool? TrackInventory = null,
    [property: Id(9)] IReadOnlyList<string>? ModifierBlockIds = null,
    [property: Id(10)] IReadOnlyList<string>? TagIds = null,
    [property: Id(11)] string? ChangeNote = null,
    [property: Id(12)] Guid? CreatedBy = null,
    [property: Id(13)] NutritionInfoData? Nutrition = null);

/// <summary>
/// Command to add a localized translation to a menu item.
/// </summary>
[GenerateSerializer]
public record AddMenuItemTranslationCommand(
    [property: Id(0)] string Locale,
    [property: Id(1)] string Name,
    [property: Id(2)] string? Description = null,
    [property: Id(3)] string? KitchenName = null);

/// <summary>
/// Snapshot of a menu item document version.
/// </summary>
[GenerateSerializer]
public record MenuItemVersionSnapshot(
    [property: Id(0)] int VersionNumber,
    [property: Id(1)] DateTimeOffset CreatedAt,
    [property: Id(2)] Guid? CreatedBy,
    [property: Id(3)] string? ChangeNote,
    [property: Id(4)] string Name,
    [property: Id(5)] string? Description,
    [property: Id(6)] decimal Price,
    [property: Id(7)] string? ImageUrl,
    [property: Id(8)] Guid? CategoryId,
    [property: Id(9)] Guid? AccountingGroupId,
    [property: Id(10)] Guid? RecipeId,
    [property: Id(11)] string? Sku,
    [property: Id(12)] bool TrackInventory,
    [property: Id(13)] IReadOnlyList<string> ModifierBlockIds,
    [property: Id(14)] IReadOnlyList<string> TagIds,
    [property: Id(15)] IReadOnlyDictionary<string, LocalizedStrings> Translations,
    [property: Id(16)] NutritionInfoData? Nutrition = null);

/// <summary>
/// Snapshot of a menu item document with full metadata.
/// </summary>
[GenerateSerializer]
public record MenuItemDocumentSnapshot(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] int CurrentVersion,
    [property: Id(3)] int? PublishedVersion,
    [property: Id(4)] int? DraftVersion,
    [property: Id(5)] bool IsArchived,
    [property: Id(6)] DateTimeOffset CreatedAt,
    [property: Id(7)] MenuItemVersionSnapshot? Published,
    [property: Id(8)] MenuItemVersionSnapshot? Draft,
    [property: Id(9)] IReadOnlyList<ScheduledChange> Schedules,
    [property: Id(10)] int TotalVersions);

/// <summary>
/// Grain for menu item document management with versioning and workflow.
/// Key: "{orgId}:menuitemdoc:{documentId}"
/// </summary>
public interface IMenuItemDocumentGrain : IGrainWithStringKey
{
    // Lifecycle
    Task<MenuItemDocumentSnapshot> CreateAsync(CreateMenuItemDocumentCommand command);
    Task<bool> ExistsAsync();

    // Versioning
    Task<MenuItemVersionSnapshot> CreateDraftAsync(CreateMenuItemDraftCommand command);
    Task<MenuItemVersionSnapshot?> GetVersionAsync(int version);
    Task<MenuItemVersionSnapshot?> GetPublishedAsync();
    Task<MenuItemVersionSnapshot?> GetDraftAsync();
    Task<IReadOnlyList<MenuItemVersionSnapshot>> GetVersionHistoryAsync(int skip = 0, int take = 20);
    Task PublishDraftAsync(Guid? publishedBy = null, string? note = null);
    Task DiscardDraftAsync();
    Task RevertToVersionAsync(int version, Guid? revertedBy = null, string? reason = null);

    // Localization
    Task AddTranslationAsync(AddMenuItemTranslationCommand command);
    Task RemoveTranslationAsync(string locale);

    // Scheduling
    Task<ScheduledChange> ScheduleChangeAsync(int version, DateTimeOffset activateAt, DateTimeOffset? deactivateAt = null, string? name = null);
    Task CancelScheduleAsync(string scheduleId);
    Task<IReadOnlyList<ScheduledChange>> GetSchedulesAsync();

    // Archive
    Task ArchiveAsync(Guid? archivedBy = null, string? reason = null);
    Task RestoreAsync(Guid? restoredBy = null);

    // Full snapshot
    Task<MenuItemDocumentSnapshot> GetSnapshotAsync();

    // Preview at a specific time
    Task<MenuItemVersionSnapshot?> PreviewAtAsync(DateTimeOffset when);

    // Event history (for audit trail via event sourcing)
    Task<IReadOnlyList<IMenuItemDocumentEvent>> GetEventHistoryAsync(int fromVersion = 0, int maxCount = 100);
}

// ============================================================================
// Menu Category Document Grain Commands & Snapshots
// ============================================================================

/// <summary>
/// Command to create a new menu category document.
/// </summary>
[GenerateSerializer]
public record CreateMenuCategoryDocumentCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] int DisplayOrder = 0,
    [property: Id(2)] string? Description = null,
    [property: Id(3)] string? Color = null,
    [property: Id(4)] string? IconUrl = null,
    [property: Id(5)] string Locale = "en-US",
    [property: Id(6)] Guid? CreatedBy = null,
    [property: Id(7)] bool PublishImmediately = false);

/// <summary>
/// Command to create a draft version of a menu category document.
/// </summary>
[GenerateSerializer]
public record CreateMenuCategoryDraftCommand(
    [property: Id(0)] string? Name = null,
    [property: Id(1)] int? DisplayOrder = null,
    [property: Id(2)] string? Description = null,
    [property: Id(3)] string? Color = null,
    [property: Id(4)] string? IconUrl = null,
    [property: Id(5)] IReadOnlyList<string>? ItemDocumentIds = null,
    [property: Id(6)] string? ChangeNote = null,
    [property: Id(7)] Guid? CreatedBy = null);

/// <summary>
/// Snapshot of a menu category document version.
/// </summary>
[GenerateSerializer]
public record MenuCategoryVersionSnapshot(
    [property: Id(0)] int VersionNumber,
    [property: Id(1)] DateTimeOffset CreatedAt,
    [property: Id(2)] Guid? CreatedBy,
    [property: Id(3)] string? ChangeNote,
    [property: Id(4)] string Name,
    [property: Id(5)] string? Description,
    [property: Id(6)] string? Color,
    [property: Id(7)] string? IconUrl,
    [property: Id(8)] int DisplayOrder,
    [property: Id(9)] IReadOnlyList<string> ItemDocumentIds,
    [property: Id(10)] IReadOnlyDictionary<string, LocalizedStrings> Translations);

/// <summary>
/// Snapshot of a menu category document with full metadata.
/// </summary>
[GenerateSerializer]
public record MenuCategoryDocumentSnapshot(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] int CurrentVersion,
    [property: Id(3)] int? PublishedVersion,
    [property: Id(4)] int? DraftVersion,
    [property: Id(5)] bool IsArchived,
    [property: Id(6)] DateTimeOffset CreatedAt,
    [property: Id(7)] MenuCategoryVersionSnapshot? Published,
    [property: Id(8)] MenuCategoryVersionSnapshot? Draft,
    [property: Id(9)] IReadOnlyList<ScheduledChange> Schedules,
    [property: Id(10)] int TotalVersions);

/// <summary>
/// Grain for menu category document management with versioning and workflow.
/// Key: "{orgId}:menucategorydoc:{documentId}"
/// </summary>
public interface IMenuCategoryDocumentGrain : IGrainWithStringKey
{
    // Lifecycle
    Task<MenuCategoryDocumentSnapshot> CreateAsync(CreateMenuCategoryDocumentCommand command);
    Task<bool> ExistsAsync();

    // Versioning
    Task<MenuCategoryVersionSnapshot> CreateDraftAsync(CreateMenuCategoryDraftCommand command);
    Task<MenuCategoryVersionSnapshot?> GetVersionAsync(int version);
    Task<MenuCategoryVersionSnapshot?> GetPublishedAsync();
    Task<MenuCategoryVersionSnapshot?> GetDraftAsync();
    Task<IReadOnlyList<MenuCategoryVersionSnapshot>> GetVersionHistoryAsync(int skip = 0, int take = 20);
    Task PublishDraftAsync(Guid? publishedBy = null, string? note = null);
    Task DiscardDraftAsync();
    Task RevertToVersionAsync(int version, Guid? revertedBy = null, string? reason = null);

    // Item management
    Task AddItemAsync(string itemDocumentId);
    Task RemoveItemAsync(string itemDocumentId);
    Task ReorderItemsAsync(IReadOnlyList<string> itemDocumentIds);

    // Scheduling
    Task<ScheduledChange> ScheduleChangeAsync(int version, DateTimeOffset activateAt, DateTimeOffset? deactivateAt = null, string? name = null);
    Task CancelScheduleAsync(string scheduleId);

    // Archive
    Task ArchiveAsync(Guid? archivedBy = null, string? reason = null);
    Task RestoreAsync(Guid? restoredBy = null);

    // Full snapshot
    Task<MenuCategoryDocumentSnapshot> GetSnapshotAsync();

    // Event history (for audit trail via event sourcing)
    Task<IReadOnlyList<IMenuCategoryDocumentEvent>> GetEventHistoryAsync(int fromVersion = 0, int maxCount = 100);
}

// ============================================================================
// Modifier Block Grain Commands & Snapshots
// ============================================================================

/// <summary>
/// Command to create a modifier option.
/// </summary>
[GenerateSerializer]
public record CreateModifierOptionCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] decimal PriceAdjustment = 0,
    [property: Id(2)] bool IsDefault = false,
    [property: Id(3)] int DisplayOrder = 0,
    [property: Id(4)] decimal? ServingSize = null,
    [property: Id(5)] string? ServingUnit = null,
    [property: Id(6)] Guid? InventoryItemId = null);

/// <summary>
/// Command to create a new modifier block.
/// </summary>
[GenerateSerializer]
public record CreateModifierBlockCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] ModifierSelectionRule SelectionRule = ModifierSelectionRule.ChooseOne,
    [property: Id(2)] int MinSelections = 0,
    [property: Id(3)] int MaxSelections = 1,
    [property: Id(4)] bool IsRequired = false,
    [property: Id(5)] List<CreateModifierOptionCommand>? Options = null,
    [property: Id(6)] Guid? CreatedBy = null,
    [property: Id(7)] bool PublishImmediately = false);

/// <summary>
/// Command to create a draft version of a modifier block.
/// </summary>
[GenerateSerializer]
public record CreateModifierBlockDraftCommand(
    [property: Id(0)] string? Name = null,
    [property: Id(1)] ModifierSelectionRule? SelectionRule = null,
    [property: Id(2)] int? MinSelections = null,
    [property: Id(3)] int? MaxSelections = null,
    [property: Id(4)] bool? IsRequired = null,
    [property: Id(5)] List<CreateModifierOptionCommand>? Options = null,
    [property: Id(6)] string? ChangeNote = null,
    [property: Id(7)] Guid? CreatedBy = null);

/// <summary>
/// Snapshot of a modifier option.
/// </summary>
[GenerateSerializer]
public record ModifierOptionSnapshot(
    [property: Id(0)] string OptionId,
    [property: Id(1)] string Name,
    [property: Id(2)] decimal PriceAdjustment,
    [property: Id(3)] bool IsDefault,
    [property: Id(4)] int DisplayOrder,
    [property: Id(5)] bool IsActive,
    [property: Id(6)] decimal? ServingSize,
    [property: Id(7)] string? ServingUnit,
    [property: Id(8)] Guid? InventoryItemId);

/// <summary>
/// Snapshot of a modifier block version.
/// </summary>
[GenerateSerializer]
public record ModifierBlockVersionSnapshot(
    [property: Id(0)] int VersionNumber,
    [property: Id(1)] DateTimeOffset CreatedAt,
    [property: Id(2)] Guid? CreatedBy,
    [property: Id(3)] string? ChangeNote,
    [property: Id(4)] string Name,
    [property: Id(5)] ModifierSelectionRule SelectionRule,
    [property: Id(6)] int MinSelections,
    [property: Id(7)] int MaxSelections,
    [property: Id(8)] bool IsRequired,
    [property: Id(9)] IReadOnlyList<ModifierOptionSnapshot> Options);

/// <summary>
/// Snapshot of a modifier block with full metadata.
/// </summary>
[GenerateSerializer]
public record ModifierBlockSnapshot(
    [property: Id(0)] string BlockId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] int CurrentVersion,
    [property: Id(3)] int? PublishedVersion,
    [property: Id(4)] int? DraftVersion,
    [property: Id(5)] bool IsArchived,
    [property: Id(6)] DateTimeOffset CreatedAt,
    [property: Id(7)] ModifierBlockVersionSnapshot? Published,
    [property: Id(8)] ModifierBlockVersionSnapshot? Draft,
    [property: Id(9)] int TotalVersions,
    [property: Id(10)] IReadOnlyList<string> UsedByItemIds);

/// <summary>
/// Grain for reusable modifier block management.
/// Key: "{orgId}:modifierblock:{blockId}"
/// </summary>
public interface IModifierBlockGrain : IGrainWithStringKey
{
    // Lifecycle
    Task<ModifierBlockSnapshot> CreateAsync(CreateModifierBlockCommand command);
    Task<bool> ExistsAsync();

    // Versioning
    Task<ModifierBlockVersionSnapshot> CreateDraftAsync(CreateModifierBlockDraftCommand command);
    Task<ModifierBlockVersionSnapshot?> GetVersionAsync(int version);
    Task<ModifierBlockVersionSnapshot?> GetPublishedAsync();
    Task<ModifierBlockVersionSnapshot?> GetDraftAsync();
    Task<IReadOnlyList<ModifierBlockVersionSnapshot>> GetVersionHistoryAsync(int skip = 0, int take = 20);
    Task PublishDraftAsync(Guid? publishedBy = null, string? note = null);
    Task DiscardDraftAsync();
    Task RevertToVersionAsync(int version, Guid? revertedBy = null, string? reason = null);

    // Usage tracking
    Task RegisterUsageAsync(string itemDocumentId);
    Task UnregisterUsageAsync(string itemDocumentId);
    Task<IReadOnlyList<string>> GetUsageAsync();

    // Archive
    Task ArchiveAsync(Guid? archivedBy = null, string? reason = null);
    Task RestoreAsync(Guid? restoredBy = null);

    // Full snapshot
    Task<ModifierBlockSnapshot> GetSnapshotAsync();

    // Event history (for audit trail via event sourcing)
    Task<IReadOnlyList<IModifierBlockEvent>> GetEventHistoryAsync(int fromVersion = 0, int maxCount = 100);
}

// ============================================================================
// Content Tag Grain Commands & Snapshots
// ============================================================================

/// <summary>
/// Command to create a content tag.
/// </summary>
[GenerateSerializer]
public record CreateContentTagCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] TagCategory Category,
    [property: Id(2)] string? IconUrl = null,
    [property: Id(3)] string? BadgeColor = null,
    [property: Id(4)] int DisplayOrder = 0,
    [property: Id(5)] int? ExternalTagId = null,
    [property: Id(6)] string? ExternalPlatform = null);

/// <summary>
/// Command to update a content tag.
/// </summary>
[GenerateSerializer]
public record UpdateContentTagCommand(
    [property: Id(0)] string? Name = null,
    [property: Id(1)] string? IconUrl = null,
    [property: Id(2)] string? BadgeColor = null,
    [property: Id(3)] int? DisplayOrder = null,
    [property: Id(4)] bool? IsActive = null);

/// <summary>
/// Snapshot of a content tag.
/// </summary>
[GenerateSerializer]
public record ContentTagSnapshot(
    [property: Id(0)] string TagId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] string Name,
    [property: Id(3)] TagCategory Category,
    [property: Id(4)] string? IconUrl,
    [property: Id(5)] string? BadgeColor,
    [property: Id(6)] int DisplayOrder,
    [property: Id(7)] bool IsActive,
    [property: Id(8)] int? ExternalTagId,
    [property: Id(9)] string? ExternalPlatform);

/// <summary>
/// Grain for content tag management.
/// Key: "{orgId}:contenttag:{tagId}"
/// </summary>
public interface IContentTagGrain : IGrainWithStringKey
{
    Task<ContentTagSnapshot> CreateAsync(CreateContentTagCommand command);
    Task<ContentTagSnapshot> UpdateAsync(UpdateContentTagCommand command);
    Task<ContentTagSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();
    Task DeactivateAsync();
    Task ReactivateAsync();
}

// ============================================================================
// Site Menu Overrides Grain Commands & Snapshots
// ============================================================================

/// <summary>
/// Command to set a site price override.
/// </summary>
[GenerateSerializer]
public record SetSitePriceOverrideCommand(
    [property: Id(0)] string ItemDocumentId,
    [property: Id(1)] decimal Price,
    [property: Id(2)] DateTimeOffset? EffectiveFrom = null,
    [property: Id(3)] DateTimeOffset? EffectiveUntil = null,
    [property: Id(4)] string? Reason = null,
    [property: Id(5)] Guid? SetBy = null);

/// <summary>
/// Command to add an availability window.
/// </summary>
[GenerateSerializer]
public record AddAvailabilityWindowCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] TimeOnly StartTime,
    [property: Id(2)] TimeOnly EndTime,
    [property: Id(3)] List<DayOfWeek> DaysOfWeek,
    [property: Id(4)] List<string>? ItemDocumentIds = null,
    [property: Id(5)] List<string>? CategoryDocumentIds = null);

/// <summary>
/// Snapshot of site menu overrides.
/// </summary>
[GenerateSerializer]
public record SiteMenuOverridesSnapshot(
    [property: Id(0)] Guid OrgId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] IReadOnlyList<SitePriceOverride> PriceOverrides,
    [property: Id(3)] IReadOnlyList<string> HiddenItemIds,
    [property: Id(4)] IReadOnlyList<string> HiddenCategoryIds,
    [property: Id(5)] IReadOnlyList<string> LocalItemIds,
    [property: Id(6)] IReadOnlyList<string> LocalCategoryIds,
    [property: Id(7)] IReadOnlyList<AvailabilityWindow> AvailabilityWindows,
    [property: Id(8)] IReadOnlyDictionary<string, DateTimeOffset?> SnoozedItems);

/// <summary>
/// Grain for site-level menu overrides.
/// Key: "{orgId}:{siteId}:menuoverrides"
/// </summary>
public interface ISiteMenuOverridesGrain : IGrainWithStringKey
{
    // Price overrides
    Task SetPriceOverrideAsync(SetSitePriceOverrideCommand command);
    Task RemovePriceOverrideAsync(string itemDocumentId, Guid? removedBy = null);
    Task<decimal?> GetPriceOverrideAsync(string itemDocumentId);

    // Visibility
    Task HideItemAsync(string itemDocumentId, Guid? hiddenBy = null);
    Task UnhideItemAsync(string itemDocumentId, Guid? unhiddenBy = null);
    Task HideCategoryAsync(string categoryDocumentId, Guid? hiddenBy = null);
    Task UnhideCategoryAsync(string categoryDocumentId, Guid? unhiddenBy = null);

    // Local items (site-specific)
    Task AddLocalItemAsync(string itemDocumentId);
    Task RemoveLocalItemAsync(string itemDocumentId);
    Task AddLocalCategoryAsync(string categoryDocumentId);
    Task RemoveLocalCategoryAsync(string categoryDocumentId);

    // Availability windows
    Task<AvailabilityWindow> AddAvailabilityWindowAsync(AddAvailabilityWindowCommand command);
    Task UpdateAvailabilityWindowAsync(string windowId, AddAvailabilityWindowCommand command);
    Task RemoveAvailabilityWindowAsync(string windowId, Guid? removedBy = null);
    Task<IReadOnlyList<AvailabilityWindow>> GetAvailabilityWindowsAsync();

    // Snoozing (86'd items)
    Task SnoozeItemAsync(string itemDocumentId, DateTimeOffset? until = null, Guid? snoozedBy = null, string? reason = null);
    Task UnsnoozeItemAsync(string itemDocumentId, Guid? unsnoozedBy = null);
    Task<bool> IsItemSnoozedAsync(string itemDocumentId);

    // Full snapshot
    Task<SiteMenuOverridesSnapshot> GetSnapshotAsync();
    Task InitializeAsync();
}

// ============================================================================
// Menu Content Resolver Grain
// ============================================================================

/// <summary>
/// Preview options for menu resolution.
/// </summary>
[GenerateSerializer]
public record MenuPreviewOptions(
    [property: Id(0)] bool ShowDraft = false,
    [property: Id(1)] bool ShowHidden = false,
    [property: Id(2)] bool ShowSnoozed = false,
    [property: Id(3)] int? SpecificVersion = null);

/// <summary>
/// Grain for resolving effective menu content for a site.
/// Key: "{orgId}:{siteId}:menuresolver"
/// </summary>
public interface IMenuContentResolverGrain : IGrainWithStringKey
{
    /// <summary>
    /// Resolve the effective menu for consumption (POS, online ordering, etc.).
    /// </summary>
    Task<EffectiveMenuState> ResolveAsync(MenuResolveContext context);

    /// <summary>
    /// Preview the menu with specific options (for backoffice).
    /// </summary>
    Task<EffectiveMenuState> PreviewAsync(MenuResolveContext context, MenuPreviewOptions options);

    /// <summary>
    /// Get a single resolved item.
    /// </summary>
    Task<ResolvedMenuItem?> ResolveItemAsync(string itemDocumentId, MenuResolveContext context);

    /// <summary>
    /// Invalidate cached menu data.
    /// </summary>
    Task InvalidateCacheAsync();

    /// <summary>
    /// Check if a specific version would be active at a given time.
    /// </summary>
    Task<bool> WouldBeActiveAsync(string documentId, int version, DateTimeOffset when);
}

// ============================================================================
// Menu Registry Grain (for listing documents)
// ============================================================================

/// <summary>
/// Summary of a menu item document for listing.
/// </summary>
[GenerateSerializer]
public record MenuItemDocumentSummary(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] string Name,
    [property: Id(2)] decimal Price,
    [property: Id(3)] string? CategoryId,
    [property: Id(4)] bool HasDraft,
    [property: Id(5)] bool IsArchived,
    [property: Id(6)] int PublishedVersion,
    [property: Id(7)] DateTimeOffset LastModified);

/// <summary>
/// Summary of a menu category document for listing.
/// </summary>
[GenerateSerializer]
public record MenuCategoryDocumentSummary(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] string Name,
    [property: Id(2)] int DisplayOrder,
    [property: Id(3)] string? Color,
    [property: Id(4)] bool HasDraft,
    [property: Id(5)] bool IsArchived,
    [property: Id(6)] int ItemCount,
    [property: Id(7)] DateTimeOffset LastModified);

/// <summary>
/// Grain for maintaining a registry of menu documents.
/// Key: "{orgId}:menuregistry"
/// </summary>
public interface IMenuRegistryGrain : IGrainWithStringKey
{
    // Items
    Task RegisterItemAsync(string documentId, string name, decimal price, string? categoryId);
    Task UpdateItemAsync(string documentId, string name, decimal price, string? categoryId, bool hasDraft, bool isArchived);
    Task UnregisterItemAsync(string documentId);
    Task<IReadOnlyList<MenuItemDocumentSummary>> GetItemsAsync(string? categoryId = null, bool includeArchived = false);

    // Categories
    Task RegisterCategoryAsync(string documentId, string name, int displayOrder, string? color);
    Task UpdateCategoryAsync(string documentId, string name, int displayOrder, string? color, bool hasDraft, bool isArchived, int itemCount);
    Task UnregisterCategoryAsync(string documentId);
    Task<IReadOnlyList<MenuCategoryDocumentSummary>> GetCategoriesAsync(bool includeArchived = false);

    // Modifier blocks
    Task RegisterModifierBlockAsync(string blockId, string name);
    Task UnregisterModifierBlockAsync(string blockId);
    Task<IReadOnlyList<string>> GetModifierBlockIdsAsync();

    // Tags
    Task RegisterTagAsync(string tagId, string name, TagCategory category);
    Task UnregisterTagAsync(string tagId);
    Task<IReadOnlyList<string>> GetTagIdsAsync(TagCategory? category = null);
}
