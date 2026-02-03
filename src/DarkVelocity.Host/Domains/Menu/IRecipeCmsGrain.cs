using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Recipe Document Grain Commands & Snapshots
// ============================================================================

/// <summary>
/// Command to create a new recipe document.
/// </summary>
[GenerateSerializer]
public record CreateRecipeDocumentCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] string? Description = null,
    [property: Id(2)] decimal PortionYield = 1,
    [property: Id(3)] string YieldUnit = "portion",
    [property: Id(4)] IReadOnlyList<CreateRecipeIngredientCommand>? Ingredients = null,
    [property: Id(5)] IReadOnlyList<string>? AllergenTags = null,
    [property: Id(6)] IReadOnlyList<string>? DietaryTags = null,
    [property: Id(7)] string? PrepInstructions = null,
    [property: Id(8)] int PrepTimeMinutes = 0,
    [property: Id(9)] int CookTimeMinutes = 0,
    [property: Id(10)] string? ImageUrl = null,
    [property: Id(11)] Guid? CategoryId = null,
    [property: Id(12)] string Locale = "en-US",
    [property: Id(13)] Guid? CreatedBy = null,
    [property: Id(14)] bool PublishImmediately = false);

/// <summary>
/// Command to create a recipe ingredient.
/// </summary>
[GenerateSerializer]
public record CreateRecipeIngredientCommand(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] string IngredientName,
    [property: Id(2)] decimal Quantity,
    [property: Id(3)] string Unit,
    [property: Id(4)] decimal WastePercentage = 0,
    [property: Id(5)] decimal UnitCost = 0,
    [property: Id(6)] string? PrepInstructions = null,
    [property: Id(7)] bool IsOptional = false,
    [property: Id(8)] int DisplayOrder = 0,
    [property: Id(9)] IReadOnlyList<Guid>? SubstitutionIds = null);

/// <summary>
/// Command to create a draft version of a recipe document.
/// </summary>
[GenerateSerializer]
public record CreateRecipeDraftCommand(
    [property: Id(0)] string? Name = null,
    [property: Id(1)] string? Description = null,
    [property: Id(2)] decimal? PortionYield = null,
    [property: Id(3)] string? YieldUnit = null,
    [property: Id(4)] IReadOnlyList<CreateRecipeIngredientCommand>? Ingredients = null,
    [property: Id(5)] IReadOnlyList<string>? AllergenTags = null,
    [property: Id(6)] IReadOnlyList<string>? DietaryTags = null,
    [property: Id(7)] string? PrepInstructions = null,
    [property: Id(8)] int? PrepTimeMinutes = null,
    [property: Id(9)] int? CookTimeMinutes = null,
    [property: Id(10)] string? ImageUrl = null,
    [property: Id(11)] Guid? CategoryId = null,
    [property: Id(12)] string? ChangeNote = null,
    [property: Id(13)] Guid? CreatedBy = null);

/// <summary>
/// Command to add a localized translation to a recipe.
/// </summary>
[GenerateSerializer]
public record AddRecipeTranslationCommand(
    [property: Id(0)] string Locale,
    [property: Id(1)] string Name,
    [property: Id(2)] string? Description = null,
    [property: Id(3)] string? PrepInstructions = null);

/// <summary>
/// Snapshot of a recipe ingredient.
/// </summary>
[GenerateSerializer]
public record RecipeIngredientSnapshot(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] string IngredientName,
    [property: Id(2)] decimal Quantity,
    [property: Id(3)] string Unit,
    [property: Id(4)] decimal WastePercentage,
    [property: Id(5)] decimal EffectiveQuantity,
    [property: Id(6)] decimal UnitCost,
    [property: Id(7)] decimal LineCost,
    [property: Id(8)] string? PrepInstructions,
    [property: Id(9)] bool IsOptional,
    [property: Id(10)] int DisplayOrder,
    [property: Id(11)] IReadOnlyList<Guid>? SubstitutionIds);

/// <summary>
/// Snapshot of a recipe document version.
/// </summary>
[GenerateSerializer]
public record RecipeVersionSnapshot(
    [property: Id(0)] int VersionNumber,
    [property: Id(1)] DateTimeOffset CreatedAt,
    [property: Id(2)] Guid? CreatedBy,
    [property: Id(3)] string? ChangeNote,
    [property: Id(4)] string Name,
    [property: Id(5)] string? Description,
    [property: Id(6)] decimal PortionYield,
    [property: Id(7)] string YieldUnit,
    [property: Id(8)] IReadOnlyList<RecipeIngredientSnapshot> Ingredients,
    [property: Id(9)] IReadOnlyList<string> AllergenTags,
    [property: Id(10)] IReadOnlyList<string> DietaryTags,
    [property: Id(11)] string? PrepInstructions,
    [property: Id(12)] int PrepTimeMinutes,
    [property: Id(13)] int CookTimeMinutes,
    [property: Id(14)] string? ImageUrl,
    [property: Id(15)] Guid? CategoryId,
    [property: Id(16)] decimal TheoreticalCost,
    [property: Id(17)] decimal CostPerPortion,
    [property: Id(18)] IReadOnlyDictionary<string, LocalizedStrings> Translations);

/// <summary>
/// Snapshot of a recipe document with full metadata.
/// </summary>
[GenerateSerializer]
public record RecipeDocumentSnapshot(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] int CurrentVersion,
    [property: Id(3)] int? PublishedVersion,
    [property: Id(4)] int? DraftVersion,
    [property: Id(5)] bool IsArchived,
    [property: Id(6)] DateTimeOffset CreatedAt,
    [property: Id(7)] RecipeVersionSnapshot? Published,
    [property: Id(8)] RecipeVersionSnapshot? Draft,
    [property: Id(9)] IReadOnlyList<ScheduledChange> Schedules,
    [property: Id(10)] int TotalVersions,
    [property: Id(11)] IReadOnlyList<string> LinkedMenuItemIds);

/// <summary>
/// Grain for recipe document management with versioning and workflow.
/// Key: "{orgId}:recipedoc:{documentId}"
/// </summary>
public interface IRecipeDocumentGrain : IGrainWithStringKey
{
    // Lifecycle
    Task<RecipeDocumentSnapshot> CreateAsync(CreateRecipeDocumentCommand command);
    Task<bool> ExistsAsync();

    // Versioning
    Task<RecipeVersionSnapshot> CreateDraftAsync(CreateRecipeDraftCommand command);
    Task<RecipeVersionSnapshot?> GetVersionAsync(int version);
    Task<RecipeVersionSnapshot?> GetPublishedAsync();
    Task<RecipeVersionSnapshot?> GetDraftAsync();
    Task<IReadOnlyList<RecipeVersionSnapshot>> GetVersionHistoryAsync(int skip = 0, int take = 20);
    Task PublishDraftAsync(Guid? publishedBy = null, string? note = null);
    Task DiscardDraftAsync();
    Task RevertToVersionAsync(int version, Guid? revertedBy = null, string? reason = null);

    // Localization
    Task AddTranslationAsync(AddRecipeTranslationCommand command);
    Task RemoveTranslationAsync(string locale);

    // Scheduling
    Task<ScheduledChange> ScheduleChangeAsync(int version, DateTimeOffset activateAt, DateTimeOffset? deactivateAt = null, string? name = null);
    Task CancelScheduleAsync(string scheduleId);
    Task<IReadOnlyList<ScheduledChange>> GetSchedulesAsync();

    // Archive
    Task ArchiveAsync(Guid? archivedBy = null, string? reason = null);
    Task RestoreAsync(Guid? restoredBy = null);

    // Cost recalculation
    Task RecalculateCostAsync(IReadOnlyDictionary<Guid, decimal>? ingredientPrices = null);

    // Menu item linkage
    Task LinkMenuItemAsync(string menuItemDocumentId);
    Task UnlinkMenuItemAsync(string menuItemDocumentId);
    Task<IReadOnlyList<string>> GetLinkedMenuItemsAsync();

    // Full snapshot
    Task<RecipeDocumentSnapshot> GetSnapshotAsync();

    // Preview at a specific time
    Task<RecipeVersionSnapshot?> PreviewAtAsync(DateTimeOffset when);

    // Event history (for audit trail via event sourcing)
    Task<IReadOnlyList<IRecipeDocumentEvent>> GetEventHistoryAsync(int fromVersion = 0, int maxCount = 100);
}

// ============================================================================
// Recipe Category Document Grain Commands & Snapshots
// ============================================================================

/// <summary>
/// Command to create a new recipe category document.
/// </summary>
[GenerateSerializer]
public record CreateRecipeCategoryDocumentCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] int DisplayOrder = 0,
    [property: Id(2)] string? Description = null,
    [property: Id(3)] string? Color = null,
    [property: Id(4)] string? IconUrl = null,
    [property: Id(5)] string Locale = "en-US",
    [property: Id(6)] Guid? CreatedBy = null,
    [property: Id(7)] bool PublishImmediately = false);

/// <summary>
/// Command to create a draft version of a recipe category document.
/// </summary>
[GenerateSerializer]
public record CreateRecipeCategoryDraftCommand(
    [property: Id(0)] string? Name = null,
    [property: Id(1)] int? DisplayOrder = null,
    [property: Id(2)] string? Description = null,
    [property: Id(3)] string? Color = null,
    [property: Id(4)] string? IconUrl = null,
    [property: Id(5)] IReadOnlyList<string>? RecipeDocumentIds = null,
    [property: Id(6)] string? ChangeNote = null,
    [property: Id(7)] Guid? CreatedBy = null);

/// <summary>
/// Snapshot of a recipe category document version.
/// </summary>
[GenerateSerializer]
public record RecipeCategoryVersionSnapshot(
    [property: Id(0)] int VersionNumber,
    [property: Id(1)] DateTimeOffset CreatedAt,
    [property: Id(2)] Guid? CreatedBy,
    [property: Id(3)] string? ChangeNote,
    [property: Id(4)] string Name,
    [property: Id(5)] string? Description,
    [property: Id(6)] string? Color,
    [property: Id(7)] string? IconUrl,
    [property: Id(8)] int DisplayOrder,
    [property: Id(9)] IReadOnlyList<string> RecipeDocumentIds,
    [property: Id(10)] IReadOnlyDictionary<string, LocalizedStrings> Translations);

/// <summary>
/// Snapshot of a recipe category document with full metadata.
/// </summary>
[GenerateSerializer]
public record RecipeCategoryDocumentSnapshot(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] int CurrentVersion,
    [property: Id(3)] int? PublishedVersion,
    [property: Id(4)] int? DraftVersion,
    [property: Id(5)] bool IsArchived,
    [property: Id(6)] DateTimeOffset CreatedAt,
    [property: Id(7)] RecipeCategoryVersionSnapshot? Published,
    [property: Id(8)] RecipeCategoryVersionSnapshot? Draft,
    [property: Id(9)] IReadOnlyList<ScheduledChange> Schedules,
    [property: Id(10)] int TotalVersions);

/// <summary>
/// Grain for recipe category document management with versioning and workflow.
/// Key: "{orgId}:recipecategorydoc:{documentId}"
/// </summary>
public interface IRecipeCategoryDocumentGrain : IGrainWithStringKey
{
    // Lifecycle
    Task<RecipeCategoryDocumentSnapshot> CreateAsync(CreateRecipeCategoryDocumentCommand command);
    Task<bool> ExistsAsync();

    // Versioning
    Task<RecipeCategoryVersionSnapshot> CreateDraftAsync(CreateRecipeCategoryDraftCommand command);
    Task<RecipeCategoryVersionSnapshot?> GetVersionAsync(int version);
    Task<RecipeCategoryVersionSnapshot?> GetPublishedAsync();
    Task<RecipeCategoryVersionSnapshot?> GetDraftAsync();
    Task<IReadOnlyList<RecipeCategoryVersionSnapshot>> GetVersionHistoryAsync(int skip = 0, int take = 20);
    Task PublishDraftAsync(Guid? publishedBy = null, string? note = null);
    Task DiscardDraftAsync();
    Task RevertToVersionAsync(int version, Guid? revertedBy = null, string? reason = null);

    // Recipe management
    Task AddRecipeAsync(string recipeDocumentId);
    Task RemoveRecipeAsync(string recipeDocumentId);
    Task ReorderRecipesAsync(IReadOnlyList<string> recipeDocumentIds);

    // Scheduling
    Task<ScheduledChange> ScheduleChangeAsync(int version, DateTimeOffset activateAt, DateTimeOffset? deactivateAt = null, string? name = null);
    Task CancelScheduleAsync(string scheduleId);

    // Archive
    Task ArchiveAsync(Guid? archivedBy = null, string? reason = null);
    Task RestoreAsync(Guid? restoredBy = null);

    // Full snapshot
    Task<RecipeCategoryDocumentSnapshot> GetSnapshotAsync();

    // Event history (for audit trail via event sourcing)
    Task<IReadOnlyList<IRecipeCategoryDocumentEvent>> GetEventHistoryAsync(int fromVersion = 0, int maxCount = 100);
}

// ============================================================================
// Recipe Registry Grain (for listing documents)
// ============================================================================

/// <summary>
/// Summary of a recipe document for listing.
/// </summary>
[GenerateSerializer]
public record RecipeDocumentSummary(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] string Name,
    [property: Id(2)] decimal CostPerPortion,
    [property: Id(3)] string? CategoryId,
    [property: Id(4)] bool HasDraft,
    [property: Id(5)] bool IsArchived,
    [property: Id(6)] int PublishedVersion,
    [property: Id(7)] DateTimeOffset LastModified,
    [property: Id(8)] int LinkedMenuItemCount);

/// <summary>
/// Summary of a recipe category document for listing.
/// </summary>
[GenerateSerializer]
public record RecipeCategoryDocumentSummary(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] string Name,
    [property: Id(2)] int DisplayOrder,
    [property: Id(3)] string? Color,
    [property: Id(4)] bool HasDraft,
    [property: Id(5)] bool IsArchived,
    [property: Id(6)] int RecipeCount,
    [property: Id(7)] DateTimeOffset LastModified);

/// <summary>
/// Grain for maintaining a registry of recipe documents.
/// Key: "{orgId}:reciperegistry"
/// </summary>
public interface IRecipeRegistryGrain : IGrainWithStringKey
{
    // Recipes
    Task RegisterRecipeAsync(string documentId, string name, decimal costPerPortion, string? categoryId, int linkedMenuItemCount = 0);
    Task UpdateRecipeAsync(string documentId, string name, decimal costPerPortion, string? categoryId, bool hasDraft, bool isArchived, int linkedMenuItemCount);
    Task UnregisterRecipeAsync(string documentId);
    Task<IReadOnlyList<RecipeDocumentSummary>> GetRecipesAsync(string? categoryId = null, bool includeArchived = false);

    // Categories
    Task RegisterCategoryAsync(string documentId, string name, int displayOrder, string? color);
    Task UpdateCategoryAsync(string documentId, string name, int displayOrder, string? color, bool hasDraft, bool isArchived, int recipeCount);
    Task UnregisterCategoryAsync(string documentId);
    Task<IReadOnlyList<RecipeCategoryDocumentSummary>> GetCategoriesAsync(bool includeArchived = false);

    // Search
    Task<IReadOnlyList<RecipeDocumentSummary>> SearchRecipesAsync(string query, int take = 20);
}
