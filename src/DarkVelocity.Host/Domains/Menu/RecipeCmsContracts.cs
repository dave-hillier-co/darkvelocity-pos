using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

// ============================================================================
// Recipe Document API Contracts
// ============================================================================

/// <summary>
/// Request to create a new recipe document.
/// </summary>
public record CreateRecipeDocumentRequest(
    string Name,
    string? Description = null,
    decimal PortionYield = 1,
    string YieldUnit = "portion",
    IReadOnlyList<RecipeIngredientRequest>? Ingredients = null,
    IReadOnlyList<string>? AllergenTags = null,
    IReadOnlyList<string>? DietaryTags = null,
    string? PrepInstructions = null,
    int PrepTimeMinutes = 0,
    int CookTimeMinutes = 0,
    string? ImageUrl = null,
    Guid? CategoryId = null,
    string Locale = "en-US",
    bool PublishImmediately = false);

/// <summary>
/// Request for a recipe ingredient.
/// </summary>
public record RecipeIngredientRequest(
    Guid IngredientId,
    string IngredientName,
    decimal Quantity,
    string Unit,
    decimal WastePercentage = 0,
    decimal UnitCost = 0,
    string? PrepInstructions = null,
    bool IsOptional = false,
    int DisplayOrder = 0,
    IReadOnlyList<Guid>? SubstitutionIds = null);

/// <summary>
/// Request to create a draft of a recipe document.
/// </summary>
public record CreateRecipeDraftRequest(
    string? Name = null,
    string? Description = null,
    decimal? PortionYield = null,
    string? YieldUnit = null,
    IReadOnlyList<RecipeIngredientRequest>? Ingredients = null,
    IReadOnlyList<string>? AllergenTags = null,
    IReadOnlyList<string>? DietaryTags = null,
    string? PrepInstructions = null,
    int? PrepTimeMinutes = null,
    int? CookTimeMinutes = null,
    string? ImageUrl = null,
    Guid? CategoryId = null,
    string? ChangeNote = null);

/// <summary>
/// Request to add a localized translation to a recipe.
/// </summary>
public record AddRecipeTranslationRequest(
    string Locale,
    string Name,
    string? Description = null,
    string? PrepInstructions = null);

/// <summary>
/// Request to recalculate recipe costs.
/// </summary>
public record RecalculateCostRequest(
    IReadOnlyDictionary<Guid, decimal>? IngredientPrices = null);

/// <summary>
/// Request to link a recipe to a menu item.
/// </summary>
public record LinkRecipeToMenuItemRequest(
    string MenuItemDocumentId);

// ============================================================================
// Recipe Category Document API Contracts
// ============================================================================

/// <summary>
/// Request to create a new recipe category document.
/// </summary>
public record CreateRecipeCategoryDocumentRequest(
    string Name,
    int DisplayOrder = 0,
    string? Description = null,
    string? Color = null,
    string? IconUrl = null,
    string Locale = "en-US",
    bool PublishImmediately = false);

/// <summary>
/// Request to create a draft of a recipe category document.
/// </summary>
public record CreateRecipeCategoryDraftRequest(
    string? Name = null,
    int? DisplayOrder = null,
    string? Description = null,
    string? Color = null,
    string? IconUrl = null,
    IReadOnlyList<string>? RecipeDocumentIds = null,
    string? ChangeNote = null);

/// <summary>
/// Request to reorder recipes in a category.
/// </summary>
public record ReorderRecipesRequest(
    IReadOnlyList<string> RecipeDocumentIds);

// ============================================================================
// API Response Types
// ============================================================================

/// <summary>
/// Response containing a recipe ingredient.
/// </summary>
public record RecipeIngredientResponse(
    Guid IngredientId,
    string IngredientName,
    decimal Quantity,
    string Unit,
    decimal WastePercentage,
    decimal EffectiveQuantity,
    decimal UnitCost,
    decimal LineCost,
    string? PrepInstructions,
    bool IsOptional,
    int DisplayOrder,
    IReadOnlyList<Guid>? SubstitutionIds);

/// <summary>
/// Response containing a recipe document.
/// </summary>
public record RecipeDocumentResponse(
    string DocumentId,
    Guid OrgId,
    int CurrentVersion,
    int? PublishedVersion,
    int? DraftVersion,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    RecipeVersionResponse? Published,
    RecipeVersionResponse? Draft,
    IReadOnlyList<ScheduledChangeResponse> Schedules,
    int TotalVersions,
    IReadOnlyList<string> LinkedMenuItemIds);

/// <summary>
/// Response containing a recipe version.
/// </summary>
public record RecipeVersionResponse(
    int VersionNumber,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    string? ChangeNote,
    string Name,
    string? Description,
    decimal PortionYield,
    string YieldUnit,
    IReadOnlyList<RecipeIngredientResponse> Ingredients,
    IReadOnlyList<string> AllergenTags,
    IReadOnlyList<string> DietaryTags,
    string? PrepInstructions,
    int PrepTimeMinutes,
    int CookTimeMinutes,
    string? ImageUrl,
    Guid? CategoryId,
    decimal TheoreticalCost,
    decimal CostPerPortion);

/// <summary>
/// Response containing a recipe category document.
/// </summary>
public record RecipeCategoryDocumentResponse(
    string DocumentId,
    Guid OrgId,
    int CurrentVersion,
    int? PublishedVersion,
    int? DraftVersion,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    RecipeCategoryVersionResponse? Published,
    RecipeCategoryVersionResponse? Draft,
    IReadOnlyList<ScheduledChangeResponse> Schedules,
    int TotalVersions);

/// <summary>
/// Response containing a recipe category version.
/// </summary>
public record RecipeCategoryVersionResponse(
    int VersionNumber,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    string? ChangeNote,
    string Name,
    string? Description,
    string? Color,
    string? IconUrl,
    int DisplayOrder,
    IReadOnlyList<string> RecipeDocumentIds);

/// <summary>
/// Response containing a list summary for recipes.
/// </summary>
public record RecipeSummaryResponse(
    string DocumentId,
    string Name,
    decimal CostPerPortion,
    string? CategoryId,
    bool HasDraft,
    bool IsArchived,
    int PublishedVersion,
    DateTimeOffset LastModified,
    int LinkedMenuItemCount);

/// <summary>
/// Response containing a list summary for recipe categories.
/// </summary>
public record RecipeCategorySummaryResponse(
    string DocumentId,
    string Name,
    int DisplayOrder,
    string? Color,
    bool HasDraft,
    bool IsArchived,
    int RecipeCount,
    DateTimeOffset LastModified);

/// <summary>
/// Response containing cost recalculation result.
/// </summary>
public record CostRecalculationResponse(
    string DocumentId,
    int Version,
    decimal PreviousCost,
    decimal NewCost,
    decimal CostChange,
    decimal CostChangePercent);
