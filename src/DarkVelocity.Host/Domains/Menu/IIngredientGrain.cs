using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Ingredient Grain Commands & Snapshots
// ============================================================================

/// <summary>
/// Command to create a new ingredient.
/// </summary>
[GenerateSerializer]
public record CreateIngredientCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] string? Description = null,
    [property: Id(2)] string? Sku = null,
    [property: Id(3)] string BaseUnit = "g",
    [property: Id(4)] decimal DefaultCostPerUnit = 0,
    [property: Id(5)] string CostUnit = "g",
    [property: Id(6)] IReadOnlyList<AllergenDeclarationCommand>? Allergens = null,
    [property: Id(7)] IngredientNutritionCommand? Nutrition = null,
    [property: Id(8)] string? Category = null,
    [property: Id(9)] IReadOnlyList<string>? Tags = null,
    [property: Id(10)] Guid? CreatedBy = null);

/// <summary>
/// Command for allergen declaration.
/// </summary>
[GenerateSerializer]
public record AllergenDeclarationCommand(
    [property: Id(0)] string Allergen,
    [property: Id(1)] AllergenDeclarationType DeclarationType = AllergenDeclarationType.Contains,
    [property: Id(2)] string? Notes = null);

/// <summary>
/// Command for ingredient nutrition data.
/// </summary>
[GenerateSerializer]
public record IngredientNutritionCommand(
    [property: Id(0)] decimal? CaloriesPer100g = null,
    [property: Id(1)] decimal? ProteinPer100g = null,
    [property: Id(2)] decimal? CarbohydratesPer100g = null,
    [property: Id(3)] decimal? FatPer100g = null,
    [property: Id(4)] decimal? SaturatedFatPer100g = null,
    [property: Id(5)] decimal? FiberPer100g = null,
    [property: Id(6)] decimal? SugarPer100g = null,
    [property: Id(7)] decimal? SodiumPer100g = null,
    [property: Id(8)] bool IsPerMilliliter = false);

/// <summary>
/// Command to update an ingredient.
/// </summary>
[GenerateSerializer]
public record UpdateIngredientCommand(
    [property: Id(0)] string? Name = null,
    [property: Id(1)] string? Description = null,
    [property: Id(2)] string? Sku = null,
    [property: Id(3)] string? Category = null,
    [property: Id(4)] IReadOnlyList<string>? Tags = null,
    [property: Id(5)] Guid? UpdatedBy = null);

/// <summary>
/// Command to update ingredient cost.
/// </summary>
[GenerateSerializer]
public record UpdateIngredientCostCommand(
    [property: Id(0)] decimal NewCost,
    [property: Id(1)] string? CostUnit = null,
    [property: Id(2)] Guid? SupplierId = null,
    [property: Id(3)] string? Source = null,
    [property: Id(4)] Guid? UpdatedBy = null);

/// <summary>
/// Command to link a supplier to an ingredient.
/// </summary>
[GenerateSerializer]
public record LinkSupplierCommand(
    [property: Id(0)] Guid SupplierId,
    [property: Id(1)] string SupplierName,
    [property: Id(2)] string? SupplierSku = null,
    [property: Id(3)] decimal? SupplierPrice = null,
    [property: Id(4)] string? SupplierUnit = null,
    [property: Id(5)] decimal? ConversionToBaseUnit = null,
    [property: Id(6)] bool IsPreferred = false);

/// <summary>
/// Command to update unit conversions.
/// </summary>
[GenerateSerializer]
public record UpdateUnitConversionsCommand(
    [property: Id(0)] string BaseUnit,
    [property: Id(1)] IReadOnlyDictionary<string, decimal> Conversions);

/// <summary>
/// Snapshot of an ingredient.
/// </summary>
[GenerateSerializer]
public record IngredientSnapshot(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] string Name,
    [property: Id(3)] string? Description,
    [property: Id(4)] string? Sku,
    [property: Id(5)] string BaseUnit,
    [property: Id(6)] decimal DefaultCostPerUnit,
    [property: Id(7)] string CostUnit,
    [property: Id(8)] DateTimeOffset? LastCostUpdate,
    [property: Id(9)] IReadOnlyList<AllergenDeclarationSnapshot> Allergens,
    [property: Id(10)] IngredientNutritionSnapshot? Nutrition,
    [property: Id(11)] IReadOnlyList<IngredientSupplierSnapshot> Suppliers,
    [property: Id(12)] IReadOnlyDictionary<string, decimal> UnitConversions,
    [property: Id(13)] string? Category,
    [property: Id(14)] IReadOnlyList<string> Tags,
    [property: Id(15)] string? ProducedByRecipeId,
    [property: Id(16)] bool IsSubRecipeOutput,
    [property: Id(17)] bool IsArchived,
    [property: Id(18)] DateTimeOffset CreatedAt,
    [property: Id(19)] Guid? ProductId);

/// <summary>
/// Snapshot of an allergen declaration.
/// </summary>
[GenerateSerializer]
public record AllergenDeclarationSnapshot(
    [property: Id(0)] string Allergen,
    [property: Id(1)] AllergenDeclarationType DeclarationType,
    [property: Id(2)] string? Notes);

/// <summary>
/// Snapshot of ingredient nutrition.
/// </summary>
[GenerateSerializer]
public record IngredientNutritionSnapshot(
    [property: Id(0)] decimal? CaloriesPer100g,
    [property: Id(1)] decimal? ProteinPer100g,
    [property: Id(2)] decimal? CarbohydratesPer100g,
    [property: Id(3)] decimal? FatPer100g,
    [property: Id(4)] decimal? SaturatedFatPer100g,
    [property: Id(5)] decimal? FiberPer100g,
    [property: Id(6)] decimal? SugarPer100g,
    [property: Id(7)] decimal? SodiumPer100g,
    [property: Id(8)] bool IsPerMilliliter);

/// <summary>
/// Snapshot of an ingredient supplier link.
/// </summary>
[GenerateSerializer]
public record IngredientSupplierSnapshot(
    [property: Id(0)] Guid SupplierId,
    [property: Id(1)] string SupplierName,
    [property: Id(2)] string? SupplierSku,
    [property: Id(3)] decimal? SupplierPrice,
    [property: Id(4)] string? SupplierUnit,
    [property: Id(5)] decimal? ConversionToBaseUnit,
    [property: Id(6)] bool IsPreferred,
    [property: Id(7)] DateTimeOffset? LastPriceUpdate);

/// <summary>
/// Summary of an ingredient for listing.
/// </summary>
[GenerateSerializer]
public record IngredientSummary(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] string Name,
    [property: Id(2)] string? Sku,
    [property: Id(3)] string? Category,
    [property: Id(4)] decimal DefaultCostPerUnit,
    [property: Id(5)] string BaseUnit,
    [property: Id(6)] IReadOnlyList<string> AllergenTags,
    [property: Id(7)] bool IsSubRecipeOutput,
    [property: Id(8)] bool IsArchived,
    [property: Id(9)] DateTimeOffset LastModified);

/// <summary>
/// Grain for ingredient master data management.
/// Key: "{orgId}:ingredient:{ingredientId}"
/// </summary>
public interface IIngredientGrain : IGrainWithStringKey
{
    // Lifecycle
    Task<IngredientSnapshot> CreateAsync(CreateIngredientCommand command);
    Task<bool> ExistsAsync();
    Task<IngredientSnapshot> GetSnapshotAsync();

    // Updates
    Task<IngredientSnapshot> UpdateAsync(UpdateIngredientCommand command);
    Task UpdateCostAsync(UpdateIngredientCostCommand command);
    Task UpdateAllergensAsync(IReadOnlyList<AllergenDeclarationCommand> allergens, Guid? updatedBy = null);
    Task UpdateNutritionAsync(IngredientNutritionCommand? nutrition, Guid? updatedBy = null);
    Task UpdateUnitConversionsAsync(UpdateUnitConversionsCommand command);

    // Supplier management
    Task LinkSupplierAsync(LinkSupplierCommand command);
    Task UnlinkSupplierAsync(Guid supplierId);
    Task<IReadOnlyList<IngredientSupplierSnapshot>> GetSuppliersAsync();

    // Product linkage
    Task LinkToProductAsync(Guid productId);
    Task UnlinkFromProductAsync();

    // Sub-recipe support
    Task LinkToSubRecipeAsync(string recipeDocumentId);
    Task UnlinkFromSubRecipeAsync();

    // Allergen queries
    Task<IReadOnlyList<AllergenDeclarationSnapshot>> GetAllergensAsync();
    Task<bool> ContainsAllergenAsync(string allergen);

    // Nutrition queries
    Task<IngredientNutritionSnapshot?> GetNutritionAsync();

    // Cost queries
    Task<decimal> GetCostInUnitAsync(string unit);
    Task<IReadOnlyList<IngredientCostHistorySnapshot>> GetCostHistoryAsync(int take = 20);

    // Archive
    Task ArchiveAsync(Guid? archivedBy = null, string? reason = null);
    Task RestoreAsync(Guid? restoredBy = null);

    // Event history
    Task<IReadOnlyList<IIngredientEvent>> GetEventHistoryAsync(int fromVersion = 0, int maxCount = 100);
}

/// <summary>
/// Snapshot of ingredient cost history.
/// </summary>
[GenerateSerializer]
public record IngredientCostHistorySnapshot(
    [property: Id(0)] decimal CostPerUnit,
    [property: Id(1)] string Unit,
    [property: Id(2)] DateTimeOffset EffectiveDate,
    [property: Id(3)] Guid? SupplierId,
    [property: Id(4)] string? Source);

/// <summary>
/// Grain for maintaining a registry of ingredients.
/// Key: "{orgId}:ingredientregistry"
/// </summary>
public interface IIngredientRegistryGrain : IGrainWithStringKey
{
    Task RegisterIngredientAsync(IngredientSummary summary);
    Task UpdateIngredientAsync(IngredientSummary summary);
    Task UnregisterIngredientAsync(Guid ingredientId);

    Task<IReadOnlyList<IngredientSummary>> GetIngredientsAsync(
        string? category = null,
        bool includeArchived = false,
        bool? isSubRecipeOutput = null);

    Task<IReadOnlyList<IngredientSummary>> SearchIngredientsAsync(string query, int take = 20);

    Task<IReadOnlyList<IngredientSummary>> GetIngredientsByAllergenAsync(string allergen);

    Task<IReadOnlyList<IngredientSummary>> GetSubRecipeOutputsAsync();
}
