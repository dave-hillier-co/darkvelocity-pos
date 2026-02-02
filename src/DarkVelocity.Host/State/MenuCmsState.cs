using System.Collections.Immutable;

namespace DarkVelocity.Host.State;

// ============================================================================
// CMS Document Models - Versioned Content with Draft/Published Workflow
// ============================================================================

/// <summary>
/// Localized content strings for multi-language support.
/// </summary>
[GenerateSerializer]
public sealed class LocalizedStrings
{
    [Id(0)] public string Name { get; set; } = string.Empty;
    [Id(1)] public string? Description { get; set; }
    [Id(2)] public string? KitchenName { get; set; }
}

/// <summary>
/// Localized content container with default locale and translations.
/// </summary>
[GenerateSerializer]
public sealed class LocalizedContent
{
    [Id(0)] public string DefaultLocale { get; set; } = "en-US";
    [Id(1)] public Dictionary<string, LocalizedStrings> Translations { get; set; } = new()
    {
        ["en-US"] = new LocalizedStrings()
    };

    public LocalizedStrings GetStrings(string? locale = null)
    {
        locale ??= DefaultLocale;
        return Translations.TryGetValue(locale, out var strings)
            ? strings
            : Translations[DefaultLocale];
    }
}

/// <summary>
/// Pricing information for a menu item version.
/// </summary>
[GenerateSerializer]
public sealed class PricingInfo
{
    [Id(0)] public decimal BasePrice { get; set; }
    [Id(1)] public decimal? CostPrice { get; set; }
    [Id(2)] public string Currency { get; set; } = "GBP";
}

/// <summary>
/// Media information (images, etc.) for menu content.
/// </summary>
[GenerateSerializer]
public sealed class MediaInfo
{
    [Id(0)] public string? PrimaryImageUrl { get; set; }
    [Id(1)] public string? ThumbnailUrl { get; set; }
    [Id(2)] public List<string> AdditionalImageUrls { get; set; } = [];
}

/// <summary>
/// Audit entry for tracking changes to documents.
/// </summary>
[GenerateSerializer]
public sealed class AuditEntry
{
    [Id(0)] public Guid AuditId { get; set; } = Guid.NewGuid();
    [Id(1)] public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    [Id(2)] public string Action { get; set; } = string.Empty;
    [Id(3)] public Guid? UserId { get; set; }
    [Id(4)] public string? UserName { get; set; }
    [Id(5)] public string? Note { get; set; }
    [Id(6)] public int? VersionNumber { get; set; }
}

/// <summary>
/// Schedule recurrence pattern for scheduled changes.
/// </summary>
[GenerateSerializer]
public sealed class ScheduleRecurrence
{
    [Id(0)] public RecurrenceType Type { get; set; } = RecurrenceType.None;
    [Id(1)] public List<DayOfWeek> DaysOfWeek { get; set; } = [];
    [Id(2)] public TimeOnly? StartTime { get; set; }
    [Id(3)] public TimeOnly? EndTime { get; set; }
}

public enum RecurrenceType
{
    None,
    Daily,
    Weekly,
    Custom
}

/// <summary>
/// Scheduled change for time-based content activation.
/// </summary>
[GenerateSerializer]
public sealed class ScheduledChange
{
    [Id(0)] public string ScheduleId { get; set; } = Guid.NewGuid().ToString();
    [Id(1)] public int VersionToActivate { get; set; }
    [Id(2)] public DateTimeOffset ActivateAt { get; set; }
    [Id(3)] public DateTimeOffset? DeactivateAt { get; set; }
    [Id(4)] public ScheduleRecurrence? Recurrence { get; set; }
    [Id(5)] public string? Name { get; set; }
    [Id(6)] public bool IsActive { get; set; } = true;
}

// ============================================================================
// Menu Item Document State
// ============================================================================

/// <summary>
/// A single version of a menu item document.
/// </summary>
[GenerateSerializer]
public sealed class MenuItemVersionState
{
    [Id(0)] public int VersionNumber { get; set; }
    [Id(1)] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Id(2)] public Guid? CreatedBy { get; set; }
    [Id(3)] public string? ChangeNote { get; set; }

    // Content
    [Id(4)] public LocalizedContent Content { get; set; } = new();
    [Id(5)] public PricingInfo Pricing { get; set; } = new();
    [Id(6)] public MediaInfo? Media { get; set; }

    // References
    [Id(7)] public Guid? CategoryId { get; set; }
    [Id(8)] public Guid? AccountingGroupId { get; set; }
    [Id(9)] public Guid? RecipeId { get; set; }
    [Id(10)] public List<string> ModifierBlockIds { get; set; } = [];
    [Id(11)] public List<string> TagIds { get; set; } = [];

    // Metadata
    [Id(12)] public string? Sku { get; set; }
    [Id(13)] public bool TrackInventory { get; set; }
    [Id(14)] public int DisplayOrder { get; set; }
}

/// <summary>
/// Menu item as a versioned document with draft/published workflow.
/// Key: "{orgId}:menuitemdoc:{documentId}"
/// </summary>
[GenerateSerializer]
public sealed class MenuItemDocumentState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public string DocumentId { get; set; } = string.Empty;
    [Id(2)] public bool IsCreated { get; set; }

    // Version management
    [Id(3)] public int CurrentVersion { get; set; }
    [Id(4)] public int? PublishedVersion { get; set; }
    [Id(5)] public int? DraftVersion { get; set; }
    [Id(6)] public List<MenuItemVersionState> Versions { get; set; } = [];

    // Scheduling
    [Id(7)] public List<ScheduledChange> Schedules { get; set; } = [];

    // Audit
    [Id(8)] public List<AuditEntry> AuditLog { get; set; } = [];

    // Lifecycle
    [Id(9)] public bool IsArchived { get; set; }
    [Id(10)] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Id(11)] public DateTimeOffset? ArchivedAt { get; set; }
}

// ============================================================================
// Menu Category Document State
// ============================================================================

/// <summary>
/// A single version of a menu category document.
/// </summary>
[GenerateSerializer]
public sealed class MenuCategoryVersionState
{
    [Id(0)] public int VersionNumber { get; set; }
    [Id(1)] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Id(2)] public Guid? CreatedBy { get; set; }
    [Id(3)] public string? ChangeNote { get; set; }

    // Content
    [Id(4)] public LocalizedContent Content { get; set; } = new();
    [Id(5)] public string? Color { get; set; }
    [Id(6)] public string? IconUrl { get; set; }
    [Id(7)] public int DisplayOrder { get; set; }

    // Items in this category (ordered)
    [Id(8)] public List<string> ItemDocumentIds { get; set; } = [];
}

/// <summary>
/// Menu category as a versioned document with draft/published workflow.
/// Key: "{orgId}:menucategorydoc:{documentId}"
/// </summary>
[GenerateSerializer]
public sealed class MenuCategoryDocumentState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public string DocumentId { get; set; } = string.Empty;
    [Id(2)] public bool IsCreated { get; set; }

    // Version management
    [Id(3)] public int CurrentVersion { get; set; }
    [Id(4)] public int? PublishedVersion { get; set; }
    [Id(5)] public int? DraftVersion { get; set; }
    [Id(6)] public List<MenuCategoryVersionState> Versions { get; set; } = [];

    // Scheduling
    [Id(7)] public List<ScheduledChange> Schedules { get; set; } = [];

    // Audit
    [Id(8)] public List<AuditEntry> AuditLog { get; set; } = [];

    // Lifecycle
    [Id(9)] public bool IsArchived { get; set; }
    [Id(10)] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

// ============================================================================
// Modifier Block State (Reusable Content Blocks)
// ============================================================================

public enum ModifierSelectionRule
{
    ChooseOne,      // Radio buttons - must choose exactly one
    ChooseMany,     // Checkboxes - any number
    ChooseRange,    // Must choose between min and max
    ChooseExactly   // Must choose exactly N
}

/// <summary>
/// A single modifier option within a block.
/// </summary>
[GenerateSerializer]
public sealed class ModifierOptionState
{
    [Id(0)] public string OptionId { get; set; } = Guid.NewGuid().ToString();
    [Id(1)] public LocalizedContent Content { get; set; } = new();
    [Id(2)] public decimal PriceAdjustment { get; set; }
    [Id(3)] public bool IsDefault { get; set; }
    [Id(4)] public int DisplayOrder { get; set; }
    [Id(5)] public bool IsActive { get; set; } = true;

    // Inventory linkage
    [Id(6)] public decimal? ServingSize { get; set; }
    [Id(7)] public string? ServingUnit { get; set; }
    [Id(8)] public Guid? InventoryItemId { get; set; }
}

/// <summary>
/// A version of a modifier block.
/// </summary>
[GenerateSerializer]
public sealed class ModifierBlockVersionState
{
    [Id(0)] public int VersionNumber { get; set; }
    [Id(1)] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Id(2)] public Guid? CreatedBy { get; set; }
    [Id(3)] public string? ChangeNote { get; set; }

    // Content
    [Id(4)] public LocalizedContent Content { get; set; } = new();
    [Id(5)] public ModifierSelectionRule SelectionRule { get; set; } = ModifierSelectionRule.ChooseOne;
    [Id(6)] public int MinSelections { get; set; }
    [Id(7)] public int MaxSelections { get; set; } = 1;
    [Id(8)] public bool IsRequired { get; set; }

    // Options
    [Id(9)] public List<ModifierOptionState> Options { get; set; } = [];
}

/// <summary>
/// Reusable modifier block (e.g., "Size", "Temperature", "Toppings").
/// Key: "{orgId}:modifierblock:{blockId}"
/// </summary>
[GenerateSerializer]
public sealed class ModifierBlockState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public string BlockId { get; set; } = string.Empty;
    [Id(2)] public bool IsCreated { get; set; }

    // Version management
    [Id(3)] public int CurrentVersion { get; set; }
    [Id(4)] public int? PublishedVersion { get; set; }
    [Id(5)] public int? DraftVersion { get; set; }
    [Id(6)] public List<ModifierBlockVersionState> Versions { get; set; } = [];

    // Audit
    [Id(7)] public List<AuditEntry> AuditLog { get; set; } = [];

    // Lifecycle
    [Id(8)] public bool IsArchived { get; set; }
    [Id(9)] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Usage tracking
    [Id(10)] public List<string> UsedByItemIds { get; set; } = [];
}

// ============================================================================
// Content Tag State
// ============================================================================

public enum TagCategory
{
    Allergen,
    Dietary,
    Promotional,
    Custom
}

/// <summary>
/// Content tag for categorizing items (allergens, dietary info, promotions).
/// Key: "{orgId}:contenttag:{tagId}"
/// </summary>
[GenerateSerializer]
public sealed class ContentTagState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public string TagId { get; set; } = string.Empty;
    [Id(2)] public bool IsCreated { get; set; }

    [Id(3)] public TagCategory Category { get; set; }
    [Id(4)] public LocalizedContent Content { get; set; } = new();
    [Id(5)] public string? IconUrl { get; set; }
    [Id(6)] public string? BadgeColor { get; set; }
    [Id(7)] public int DisplayOrder { get; set; }
    [Id(8)] public bool IsActive { get; set; } = true;

    // Standard tag IDs for platform integration (e.g., Deliverect)
    [Id(9)] public int? ExternalTagId { get; set; }
    [Id(10)] public string? ExternalPlatform { get; set; }
}

// ============================================================================
// Site Menu Overrides State
// ============================================================================

/// <summary>
/// Price override for a specific item at a site.
/// </summary>
[GenerateSerializer]
public sealed class SitePriceOverride
{
    [Id(0)] public string ItemDocumentId { get; set; } = string.Empty;
    [Id(1)] public decimal Price { get; set; }
    [Id(2)] public DateTimeOffset? EffectiveFrom { get; set; }
    [Id(3)] public DateTimeOffset? EffectiveUntil { get; set; }
    [Id(4)] public string? Reason { get; set; }
}

/// <summary>
/// Availability window for time-based menu availability.
/// </summary>
[GenerateSerializer]
public sealed class AvailabilityWindow
{
    [Id(0)] public string WindowId { get; set; } = Guid.NewGuid().ToString();
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public List<DayOfWeek> DaysOfWeek { get; set; } = [];
    [Id(3)] public TimeOnly StartTime { get; set; }
    [Id(4)] public TimeOnly EndTime { get; set; }
    [Id(5)] public List<string> ItemDocumentIds { get; set; } = [];
    [Id(6)] public List<string> CategoryDocumentIds { get; set; } = [];
    [Id(7)] public bool IsActive { get; set; } = true;
}

/// <summary>
/// Channel-specific visibility settings.
/// </summary>
[GenerateSerializer]
public sealed class ChannelVisibility
{
    [Id(0)] public string Channel { get; set; } = string.Empty; // "pos", "online", "delivery"
    [Id(1)] public HashSet<string> HiddenItemIds { get; set; } = [];
    [Id(2)] public HashSet<string> HiddenCategoryIds { get; set; } = [];
    [Id(3)] public Dictionary<string, decimal> PriceOverrides { get; set; } = [];
}

/// <summary>
/// Site-level menu overrides for inheritance model.
/// Key: "{orgId}:{siteId}:menuoverrides"
/// </summary>
[GenerateSerializer]
public sealed class SiteMenuOverridesState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public bool IsCreated { get; set; }

    // Price overrides
    [Id(3)] public List<SitePriceOverride> PriceOverrides { get; set; } = [];

    // Hidden items/categories (not shown at this site)
    [Id(4)] public HashSet<string> HiddenItemIds { get; set; } = [];
    [Id(5)] public HashSet<string> HiddenCategoryIds { get; set; } = [];

    // Site-specific items (only at this site)
    [Id(6)] public List<string> LocalItemIds { get; set; } = [];
    [Id(7)] public List<string> LocalCategoryIds { get; set; } = [];

    // Availability windows
    [Id(8)] public List<AvailabilityWindow> AvailabilityWindows { get; set; } = [];

    // Channel-specific overrides
    [Id(9)] public List<ChannelVisibility> ChannelOverrides { get; set; } = [];

    // Real-time overrides (86'd items)
    [Id(10)] public Dictionary<string, DateTimeOffset?> SnoozedItems { get; set; } = [];

    // Audit
    [Id(11)] public List<AuditEntry> AuditLog { get; set; } = [];
}

// ============================================================================
// Menu Content Resolution
// ============================================================================

/// <summary>
/// Context for resolving the effective menu.
/// </summary>
[GenerateSerializer]
public sealed class MenuResolveContext
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public DateTimeOffset AsOf { get; set; } = DateTimeOffset.UtcNow;
    [Id(3)] public string Channel { get; set; } = "pos";
    [Id(4)] public string Locale { get; set; } = "en-US";
    [Id(5)] public bool IncludeDraft { get; set; }
    [Id(6)] public bool IncludeHidden { get; set; }
    [Id(7)] public bool IncludeSnoozed { get; set; }
}

/// <summary>
/// A resolved menu item ready for consumption.
/// </summary>
[GenerateSerializer]
public sealed class ResolvedMenuItem
{
    [Id(0)] public string DocumentId { get; set; } = string.Empty;
    [Id(1)] public int Version { get; set; }
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public string? Description { get; set; }
    [Id(4)] public string? KitchenName { get; set; }
    [Id(5)] public decimal Price { get; set; }
    [Id(6)] public string? ImageUrl { get; set; }
    [Id(7)] public string? CategoryId { get; set; }
    [Id(8)] public string? CategoryName { get; set; }
    [Id(9)] public List<ResolvedModifierBlock> Modifiers { get; set; } = [];
    [Id(10)] public List<ResolvedContentTag> Tags { get; set; } = [];
    [Id(11)] public bool IsSnoozed { get; set; }
    [Id(12)] public DateTimeOffset? SnoozedUntil { get; set; }
    [Id(13)] public bool IsAvailable { get; set; } = true;
    [Id(14)] public string? Sku { get; set; }
    [Id(15)] public int DisplayOrder { get; set; }
}

/// <summary>
/// A resolved modifier block ready for consumption.
/// </summary>
[GenerateSerializer]
public sealed class ResolvedModifierBlock
{
    [Id(0)] public string BlockId { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public ModifierSelectionRule SelectionRule { get; set; }
    [Id(3)] public int MinSelections { get; set; }
    [Id(4)] public int MaxSelections { get; set; }
    [Id(5)] public bool IsRequired { get; set; }
    [Id(6)] public List<ResolvedModifierOption> Options { get; set; } = [];
}

/// <summary>
/// A resolved modifier option ready for consumption.
/// </summary>
[GenerateSerializer]
public sealed class ResolvedModifierOption
{
    [Id(0)] public string OptionId { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public decimal PriceAdjustment { get; set; }
    [Id(3)] public bool IsDefault { get; set; }
    [Id(4)] public int DisplayOrder { get; set; }
}

/// <summary>
/// A resolved content tag ready for consumption.
/// </summary>
[GenerateSerializer]
public sealed class ResolvedContentTag
{
    [Id(0)] public string TagId { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public TagCategory Category { get; set; }
    [Id(3)] public string? IconUrl { get; set; }
    [Id(4)] public string? BadgeColor { get; set; }
}

/// <summary>
/// A resolved menu category ready for consumption.
/// </summary>
[GenerateSerializer]
public sealed class ResolvedMenuCategory
{
    [Id(0)] public string DocumentId { get; set; } = string.Empty;
    [Id(1)] public int Version { get; set; }
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public string? Description { get; set; }
    [Id(4)] public string? Color { get; set; }
    [Id(5)] public string? IconUrl { get; set; }
    [Id(6)] public int DisplayOrder { get; set; }
    [Id(7)] public int ItemCount { get; set; }
}

/// <summary>
/// The complete resolved effective menu for a site.
/// </summary>
[GenerateSerializer]
public sealed class EffectiveMenuState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public DateTimeOffset ResolvedAt { get; set; } = DateTimeOffset.UtcNow;
    [Id(3)] public string Channel { get; set; } = "pos";
    [Id(4)] public string Locale { get; set; } = "en-US";

    [Id(5)] public List<ResolvedMenuCategory> Categories { get; set; } = [];
    [Id(6)] public List<ResolvedMenuItem> Items { get; set; } = [];

    // Cache control
    [Id(7)] public string ETag { get; set; } = string.Empty;
    [Id(8)] public DateTimeOffset? CacheUntil { get; set; }
}
