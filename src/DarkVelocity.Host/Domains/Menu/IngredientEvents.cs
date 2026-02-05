using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Events;

// ============================================================================
// Ingredient Domain Event Interfaces
// ============================================================================

/// <summary>
/// Base interface for Ingredient domain events (for JournaledGrain event sourcing).
/// </summary>
public interface IIngredientEvent
{
    Guid IngredientId { get; }
    DateTimeOffset OccurredAt { get; }
}

// ============================================================================
// Ingredient Domain Events (for JournaledGrain)
// ============================================================================

/// <summary>
/// Domain event: An ingredient was created.
/// </summary>
[GenerateSerializer]
public sealed record IngredientCreated(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] DateTimeOffset OccurredAt,
    [property: Id(3)] string Name,
    [property: Id(4)] string? Description,
    [property: Id(5)] string? Sku,
    [property: Id(6)] string BaseUnit,
    [property: Id(7)] decimal DefaultCostPerUnit,
    [property: Id(8)] string CostUnit,
    [property: Id(9)] List<AllergenDeclarationData>? Allergens,
    [property: Id(10)] IngredientNutritionData? Nutrition,
    [property: Id(11)] string? Category,
    [property: Id(12)] List<string>? Tags,
    [property: Id(13)] Guid? CreatedBy
) : IIngredientEvent;

/// <summary>
/// Data transfer object for allergen declarations in events.
/// </summary>
[GenerateSerializer]
public sealed record AllergenDeclarationData(
    [property: Id(0)] string Allergen,
    [property: Id(1)] AllergenDeclarationType DeclarationType,
    [property: Id(2)] string? Notes
);

/// <summary>
/// Data transfer object for ingredient nutrition in events.
/// </summary>
[GenerateSerializer]
public sealed record IngredientNutritionData(
    [property: Id(0)] decimal? CaloriesPer100g,
    [property: Id(1)] decimal? ProteinPer100g,
    [property: Id(2)] decimal? CarbohydratesPer100g,
    [property: Id(3)] decimal? FatPer100g,
    [property: Id(4)] decimal? SaturatedFatPer100g,
    [property: Id(5)] decimal? FiberPer100g,
    [property: Id(6)] decimal? SugarPer100g,
    [property: Id(7)] decimal? SodiumPer100g,
    [property: Id(8)] bool IsPerMilliliter
);

/// <summary>
/// Domain event: An ingredient was updated.
/// </summary>
[GenerateSerializer]
public sealed record IngredientUpdated(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string? Name,
    [property: Id(3)] string? Description,
    [property: Id(4)] string? Sku,
    [property: Id(5)] string? Category,
    [property: Id(6)] List<string>? Tags,
    [property: Id(7)] Guid? UpdatedBy
) : IIngredientEvent;

/// <summary>
/// Domain event: Ingredient cost was updated.
/// </summary>
[GenerateSerializer]
public sealed record IngredientCostUpdated(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] decimal PreviousCost,
    [property: Id(3)] decimal NewCost,
    [property: Id(4)] string CostUnit,
    [property: Id(5)] Guid? SupplierId,
    [property: Id(6)] string? Source,
    [property: Id(7)] Guid? UpdatedBy
) : IIngredientEvent;

/// <summary>
/// Domain event: Ingredient allergens were updated.
/// </summary>
[GenerateSerializer]
public sealed record IngredientAllergensUpdated(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] List<AllergenDeclarationData> Allergens,
    [property: Id(3)] Guid? UpdatedBy
) : IIngredientEvent;

/// <summary>
/// Domain event: Ingredient nutrition was updated.
/// </summary>
[GenerateSerializer]
public sealed record IngredientNutritionUpdated(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] IngredientNutritionData? Nutrition,
    [property: Id(3)] Guid? UpdatedBy
) : IIngredientEvent;

/// <summary>
/// Domain event: A supplier was linked to an ingredient.
/// </summary>
[GenerateSerializer]
public sealed record IngredientSupplierLinked(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid SupplierId,
    [property: Id(3)] string SupplierName,
    [property: Id(4)] string? SupplierSku,
    [property: Id(5)] decimal? SupplierPrice,
    [property: Id(6)] string? SupplierUnit,
    [property: Id(7)] decimal? ConversionToBaseUnit,
    [property: Id(8)] bool IsPreferred
) : IIngredientEvent;

/// <summary>
/// Domain event: A supplier was unlinked from an ingredient.
/// </summary>
[GenerateSerializer]
public sealed record IngredientSupplierUnlinked(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid SupplierId
) : IIngredientEvent;

/// <summary>
/// Domain event: Ingredient unit conversions were updated.
/// </summary>
[GenerateSerializer]
public sealed record IngredientUnitConversionsUpdated(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string BaseUnit,
    [property: Id(3)] Dictionary<string, decimal> Conversions
) : IIngredientEvent;

/// <summary>
/// Domain event: Ingredient was linked to a sub-recipe that produces it.
/// </summary>
[GenerateSerializer]
public sealed record IngredientLinkedToSubRecipe(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string RecipeDocumentId
) : IIngredientEvent;

/// <summary>
/// Domain event: Ingredient was unlinked from its sub-recipe.
/// </summary>
[GenerateSerializer]
public sealed record IngredientUnlinkedFromSubRecipe(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string RecipeDocumentId
) : IIngredientEvent;

/// <summary>
/// Domain event: Ingredient was archived.
/// </summary>
[GenerateSerializer]
public sealed record IngredientArchived(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid? ArchivedBy,
    [property: Id(3)] string? Reason
) : IIngredientEvent;

/// <summary>
/// Domain event: Ingredient was restored from archive.
/// </summary>
[GenerateSerializer]
public sealed record IngredientRestored(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid? RestoredBy
) : IIngredientEvent;

// ============================================================================
// Integration Events (for external notifications, NOT event sourcing)
// ============================================================================

/// <summary>
/// A new ingredient was created.
/// </summary>
public sealed record IngredientCreatedIntegration(
    Guid IngredientId,
    Guid OrgId,
    string Name,
    string? Sku,
    decimal DefaultCostPerUnit,
    string BaseUnit,
    IReadOnlyList<string> AllergenTags
) : IntegrationEvent
{
    public override string EventType => "ingredient.created";
}

/// <summary>
/// Ingredient cost was updated.
/// </summary>
public sealed record IngredientCostUpdatedIntegration(
    Guid IngredientId,
    Guid OrgId,
    decimal PreviousCost,
    decimal NewCost,
    string CostUnit,
    decimal CostChange,
    decimal CostChangePercent
) : IntegrationEvent
{
    public override string EventType => "ingredient.cost.updated";
}

/// <summary>
/// Ingredient allergens were updated.
/// </summary>
public sealed record IngredientAllergensUpdatedIntegration(
    Guid IngredientId,
    Guid OrgId,
    IReadOnlyList<string> ContainsAllergens,
    IReadOnlyList<string> MayContainAllergens
) : IntegrationEvent
{
    public override string EventType => "ingredient.allergens.updated";
}
