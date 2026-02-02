using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

// ============================================================================
// Menu Item Document API Contracts
// ============================================================================

/// <summary>
/// Request to create a new menu item document.
/// </summary>
public record CreateMenuItemDocumentRequest(
    string Name,
    decimal Price,
    string? Description = null,
    Guid? CategoryId = null,
    Guid? AccountingGroupId = null,
    Guid? RecipeId = null,
    string? ImageUrl = null,
    string? Sku = null,
    bool TrackInventory = false,
    string Locale = "en-US",
    bool PublishImmediately = false);

/// <summary>
/// Request to create a draft of a menu item document.
/// </summary>
public record CreateMenuItemDraftRequest(
    string? Name = null,
    decimal? Price = null,
    string? Description = null,
    Guid? CategoryId = null,
    Guid? AccountingGroupId = null,
    Guid? RecipeId = null,
    string? ImageUrl = null,
    string? Sku = null,
    bool? TrackInventory = null,
    IReadOnlyList<string>? ModifierBlockIds = null,
    IReadOnlyList<string>? TagIds = null,
    string? ChangeNote = null);

/// <summary>
/// Request to add a localized translation.
/// </summary>
public record AddTranslationRequest(
    string Locale,
    string Name,
    string? Description = null,
    string? KitchenName = null);

/// <summary>
/// Request to publish a draft.
/// </summary>
public record PublishDraftRequest(
    string? Note = null);

/// <summary>
/// Request to revert to a previous version.
/// </summary>
public record RevertVersionRequest(
    int Version,
    string? Reason = null);

/// <summary>
/// Request to schedule a change.
/// </summary>
public record ScheduleChangeRequest(
    int Version,
    DateTimeOffset ActivateAt,
    DateTimeOffset? DeactivateAt = null,
    string? Name = null);

/// <summary>
/// Request to archive a document.
/// </summary>
public record ArchiveDocumentRequest(
    string? Reason = null);

// ============================================================================
// Menu Category Document API Contracts
// ============================================================================

/// <summary>
/// Request to create a new menu category document.
/// </summary>
public record CreateMenuCategoryDocumentRequest(
    string Name,
    int DisplayOrder = 0,
    string? Description = null,
    string? Color = null,
    string? IconUrl = null,
    string Locale = "en-US",
    bool PublishImmediately = false);

/// <summary>
/// Request to create a draft of a menu category document.
/// </summary>
public record CreateMenuCategoryDraftRequest(
    string? Name = null,
    int? DisplayOrder = null,
    string? Description = null,
    string? Color = null,
    string? IconUrl = null,
    IReadOnlyList<string>? ItemDocumentIds = null,
    string? ChangeNote = null);

/// <summary>
/// Request to reorder items in a category.
/// </summary>
public record ReorderItemsRequest(
    IReadOnlyList<string> ItemDocumentIds);

// ============================================================================
// Modifier Block API Contracts
// ============================================================================

/// <summary>
/// Request to create a modifier option.
/// </summary>
public record CreateModifierOptionRequest(
    string Name,
    decimal PriceAdjustment = 0,
    bool IsDefault = false,
    int DisplayOrder = 0,
    decimal? ServingSize = null,
    string? ServingUnit = null,
    Guid? InventoryItemId = null);

/// <summary>
/// Request to create a modifier block.
/// </summary>
public record CreateModifierBlockRequest(
    string Name,
    ModifierSelectionRule SelectionRule = ModifierSelectionRule.ChooseOne,
    int MinSelections = 0,
    int MaxSelections = 1,
    bool IsRequired = false,
    IReadOnlyList<CreateModifierOptionRequest>? Options = null,
    bool PublishImmediately = false);

/// <summary>
/// Request to create a draft of a modifier block.
/// </summary>
public record CreateModifierBlockDraftRequest(
    string? Name = null,
    ModifierSelectionRule? SelectionRule = null,
    int? MinSelections = null,
    int? MaxSelections = null,
    bool? IsRequired = null,
    IReadOnlyList<CreateModifierOptionRequest>? Options = null,
    string? ChangeNote = null);

// ============================================================================
// Content Tag API Contracts
// ============================================================================

/// <summary>
/// Request to create a content tag.
/// </summary>
public record CreateContentTagRequest(
    string Name,
    TagCategory Category,
    string? IconUrl = null,
    string? BadgeColor = null,
    int DisplayOrder = 0,
    int? ExternalTagId = null,
    string? ExternalPlatform = null);

/// <summary>
/// Request to update a content tag.
/// </summary>
public record UpdateContentTagRequest(
    string? Name = null,
    string? IconUrl = null,
    string? BadgeColor = null,
    int? DisplayOrder = null,
    bool? IsActive = null);

// ============================================================================
// Site Menu Overrides API Contracts
// ============================================================================

/// <summary>
/// Request to set a site price override.
/// </summary>
public record SetSitePriceOverrideRequest(
    string ItemDocumentId,
    decimal Price,
    DateTimeOffset? EffectiveFrom = null,
    DateTimeOffset? EffectiveUntil = null,
    string? Reason = null);

/// <summary>
/// Request to add an availability window.
/// </summary>
public record AddAvailabilityWindowRequest(
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<DayOfWeek> DaysOfWeek,
    IReadOnlyList<string>? ItemDocumentIds = null,
    IReadOnlyList<string>? CategoryDocumentIds = null);

/// <summary>
/// Request to snooze an item.
/// </summary>
public record SnoozeItemRequest(
    string ItemDocumentId,
    DateTimeOffset? Until = null,
    string? Reason = null);

/// <summary>
/// Request to hide/unhide items or categories.
/// </summary>
public record SetVisibilityRequest(
    string DocumentId,
    bool IsHidden);

// ============================================================================
// Menu Resolution API Contracts
// ============================================================================

/// <summary>
/// Request to resolve the effective menu.
/// </summary>
public record ResolveMenuRequest(
    string Channel = "pos",
    string Locale = "en-US",
    DateTimeOffset? AsOf = null,
    bool IncludeDraft = false,
    bool IncludeHidden = false,
    bool IncludeSnoozed = false);

// ============================================================================
// API Response Types
// ============================================================================

/// <summary>
/// Response containing a menu item document.
/// </summary>
public record MenuItemDocumentResponse(
    string DocumentId,
    Guid OrgId,
    int CurrentVersion,
    int? PublishedVersion,
    int? DraftVersion,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    MenuItemVersionResponse? Published,
    MenuItemVersionResponse? Draft,
    IReadOnlyList<ScheduledChangeResponse> Schedules,
    int TotalVersions);

/// <summary>
/// Response containing a menu item version.
/// </summary>
public record MenuItemVersionResponse(
    int VersionNumber,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    string? ChangeNote,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    Guid? CategoryId,
    Guid? AccountingGroupId,
    Guid? RecipeId,
    string? Sku,
    bool TrackInventory,
    IReadOnlyList<string> ModifierBlockIds,
    IReadOnlyList<string> TagIds);

/// <summary>
/// Response containing a scheduled change.
/// </summary>
public record ScheduledChangeResponse(
    string ScheduleId,
    int Version,
    DateTimeOffset ActivateAt,
    DateTimeOffset? DeactivateAt,
    string? Name,
    bool IsActive);

/// <summary>
/// Response containing a menu category document.
/// </summary>
public record MenuCategoryDocumentResponse(
    string DocumentId,
    Guid OrgId,
    int CurrentVersion,
    int? PublishedVersion,
    int? DraftVersion,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    MenuCategoryVersionResponse? Published,
    MenuCategoryVersionResponse? Draft,
    IReadOnlyList<ScheduledChangeResponse> Schedules,
    int TotalVersions);

/// <summary>
/// Response containing a menu category version.
/// </summary>
public record MenuCategoryVersionResponse(
    int VersionNumber,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    string? ChangeNote,
    string Name,
    string? Description,
    string? Color,
    string? IconUrl,
    int DisplayOrder,
    IReadOnlyList<string> ItemDocumentIds);

/// <summary>
/// Response containing a modifier block.
/// </summary>
public record ModifierBlockResponse(
    string BlockId,
    Guid OrgId,
    int CurrentVersion,
    int? PublishedVersion,
    int? DraftVersion,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    ModifierBlockVersionResponse? Published,
    ModifierBlockVersionResponse? Draft,
    int TotalVersions,
    IReadOnlyList<string> UsedByItemIds);

/// <summary>
/// Response containing a modifier block version.
/// </summary>
public record ModifierBlockVersionResponse(
    int VersionNumber,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    string? ChangeNote,
    string Name,
    ModifierSelectionRule SelectionRule,
    int MinSelections,
    int MaxSelections,
    bool IsRequired,
    IReadOnlyList<ModifierOptionResponse> Options);

/// <summary>
/// Response containing a modifier option.
/// </summary>
public record ModifierOptionResponse(
    string OptionId,
    string Name,
    decimal PriceAdjustment,
    bool IsDefault,
    int DisplayOrder,
    bool IsActive);

/// <summary>
/// Response containing a content tag.
/// </summary>
public record ContentTagResponse(
    string TagId,
    Guid OrgId,
    string Name,
    TagCategory Category,
    string? IconUrl,
    string? BadgeColor,
    int DisplayOrder,
    bool IsActive,
    int? ExternalTagId,
    string? ExternalPlatform);

/// <summary>
/// Response containing site menu overrides.
/// </summary>
public record SiteMenuOverridesResponse(
    Guid OrgId,
    Guid SiteId,
    IReadOnlyList<SitePriceOverrideResponse> PriceOverrides,
    IReadOnlyList<string> HiddenItemIds,
    IReadOnlyList<string> HiddenCategoryIds,
    IReadOnlyList<string> LocalItemIds,
    IReadOnlyList<string> LocalCategoryIds,
    IReadOnlyList<AvailabilityWindowResponse> AvailabilityWindows,
    IReadOnlyDictionary<string, DateTimeOffset?> SnoozedItems);

/// <summary>
/// Response containing a site price override.
/// </summary>
public record SitePriceOverrideResponse(
    string ItemDocumentId,
    decimal Price,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveUntil,
    string? Reason);

/// <summary>
/// Response containing an availability window.
/// </summary>
public record AvailabilityWindowResponse(
    string WindowId,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<DayOfWeek> DaysOfWeek,
    IReadOnlyList<string> ItemDocumentIds,
    IReadOnlyList<string> CategoryDocumentIds,
    bool IsActive);

/// <summary>
/// Response containing the effective menu for a site.
/// </summary>
public record EffectiveMenuResponse(
    Guid OrgId,
    Guid SiteId,
    DateTimeOffset ResolvedAt,
    string Channel,
    string Locale,
    IReadOnlyList<ResolvedMenuCategoryResponse> Categories,
    IReadOnlyList<ResolvedMenuItemResponse> Items,
    string ETag);

/// <summary>
/// Response containing a resolved menu category.
/// </summary>
public record ResolvedMenuCategoryResponse(
    string DocumentId,
    int Version,
    string Name,
    string? Description,
    string? Color,
    string? IconUrl,
    int DisplayOrder,
    int ItemCount);

/// <summary>
/// Response containing a resolved menu item.
/// </summary>
public record ResolvedMenuItemResponse(
    string DocumentId,
    int Version,
    string Name,
    string? Description,
    string? KitchenName,
    decimal Price,
    string? ImageUrl,
    string? CategoryId,
    string? CategoryName,
    IReadOnlyList<ResolvedModifierBlockResponse> Modifiers,
    IReadOnlyList<ResolvedContentTagResponse> Tags,
    bool IsSnoozed,
    DateTimeOffset? SnoozedUntil,
    bool IsAvailable,
    string? Sku,
    int DisplayOrder);

/// <summary>
/// Response containing a resolved modifier block.
/// </summary>
public record ResolvedModifierBlockResponse(
    string BlockId,
    string Name,
    ModifierSelectionRule SelectionRule,
    int MinSelections,
    int MaxSelections,
    bool IsRequired,
    IReadOnlyList<ResolvedModifierOptionResponse> Options);

/// <summary>
/// Response containing a resolved modifier option.
/// </summary>
public record ResolvedModifierOptionResponse(
    string OptionId,
    string Name,
    decimal PriceAdjustment,
    bool IsDefault,
    int DisplayOrder);

/// <summary>
/// Response containing a resolved content tag.
/// </summary>
public record ResolvedContentTagResponse(
    string TagId,
    string Name,
    TagCategory Category,
    string? IconUrl,
    string? BadgeColor);

/// <summary>
/// Response containing a list summary for items.
/// </summary>
public record MenuItemSummaryResponse(
    string DocumentId,
    string Name,
    decimal Price,
    string? CategoryId,
    bool HasDraft,
    bool IsArchived,
    int PublishedVersion,
    DateTimeOffset LastModified);

/// <summary>
/// Response containing a list summary for categories.
/// </summary>
public record MenuCategorySummaryResponse(
    string DocumentId,
    string Name,
    int DisplayOrder,
    string? Color,
    bool HasDraft,
    bool IsArchived,
    int ItemCount,
    DateTimeOffset LastModified);
