namespace DarkVelocity.Host.Grains;

// ============================================================================
// Recipe Grain
// ============================================================================

[GenerateSerializer]
public record CreateRecipeCommand(
    [property: Id(0)] Guid MenuItemId,
    [property: Id(1)] string MenuItemName,
    [property: Id(2)] string Code,
    [property: Id(3)] Guid? CategoryId,
    [property: Id(4)] string? CategoryName,
    [property: Id(5)] string? Description,
    [property: Id(6)] int PortionYield,
    [property: Id(7)] string? PrepInstructions);

[GenerateSerializer]
public record UpdateRecipeCommand(
    [property: Id(0)] string? MenuItemName,
    [property: Id(1)] string? Code,
    [property: Id(2)] Guid? CategoryId,
    [property: Id(3)] string? CategoryName,
    [property: Id(4)] string? Description,
    [property: Id(5)] int? PortionYield,
    [property: Id(6)] string? PrepInstructions,
    [property: Id(7)] bool? IsActive);

[GenerateSerializer]
public record RecipeIngredientCommand(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] string IngredientName,
    [property: Id(2)] decimal Quantity,
    [property: Id(3)] string UnitOfMeasure,
    [property: Id(4)] decimal WastePercentage,
    [property: Id(5)] decimal CurrentUnitCost);

[GenerateSerializer]
public record CostingRecipeIngredientSnapshot(
    [property: Id(0)] Guid Id,
    [property: Id(1)] Guid IngredientId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] decimal Quantity,
    [property: Id(4)] string UnitOfMeasure,
    [property: Id(5)] decimal WastePercentage,
    [property: Id(6)] decimal EffectiveQuantity,
    [property: Id(7)] decimal CurrentUnitCost,
    [property: Id(8)] decimal CurrentLineCost,
    [property: Id(9)] decimal CostPercentOfTotal);

[GenerateSerializer]
public record RecipeCostCalculation(
    [property: Id(0)] Guid RecipeId,
    [property: Id(1)] string RecipeName,
    [property: Id(2)] decimal TotalIngredientCost,
    [property: Id(3)] decimal CostPerPortion,
    [property: Id(4)] int PortionYield,
    [property: Id(5)] decimal? MenuPrice,
    [property: Id(6)] decimal? CostPercentage,
    [property: Id(7)] decimal? GrossMarginPercent,
    [property: Id(8)] IReadOnlyList<CostingRecipeIngredientSnapshot> IngredientCosts);

[GenerateSerializer]
public record RecipeSnapshot(
    [property: Id(0)] Guid RecipeId,
    [property: Id(1)] Guid MenuItemId,
    [property: Id(2)] string MenuItemName,
    [property: Id(3)] string Code,
    [property: Id(4)] Guid? CategoryId,
    [property: Id(5)] string? CategoryName,
    [property: Id(6)] string? Description,
    [property: Id(7)] int PortionYield,
    [property: Id(8)] string? PrepInstructions,
    [property: Id(9)] decimal CurrentCostPerPortion,
    [property: Id(10)] DateTime? CostCalculatedAt,
    [property: Id(11)] bool IsActive,
    [property: Id(12)] IReadOnlyList<CostingRecipeIngredientSnapshot> Ingredients);

[GenerateSerializer]
public record RecipeCostSnapshotEntry(
    [property: Id(0)] Guid SnapshotId,
    [property: Id(1)] DateTime SnapshotDate,
    [property: Id(2)] decimal CostPerPortion,
    [property: Id(3)] decimal? MenuPrice,
    [property: Id(4)] decimal? MarginPercent,
    [property: Id(5)] string? Notes);

/// <summary>
/// Grain for recipe management and cost calculation.
/// Key: "{orgId}:recipe:{recipeId}"
/// </summary>
public interface IRecipeGrain : IGrainWithStringKey
{
    Task<RecipeSnapshot> CreateAsync(CreateRecipeCommand command);
    Task<RecipeSnapshot> UpdateAsync(UpdateRecipeCommand command);
    Task<RecipeSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();
    Task DeleteAsync();

    // Ingredient management
    Task AddIngredientAsync(RecipeIngredientCommand command);
    Task UpdateIngredientAsync(Guid ingredientId, RecipeIngredientCommand command);
    Task RemoveIngredientAsync(Guid ingredientId);
    Task<IReadOnlyList<CostingRecipeIngredientSnapshot>> GetIngredientsAsync();

    // Cost calculation
    Task<RecipeCostCalculation> CalculateCostAsync(decimal? menuPrice = null);
    Task<RecipeSnapshot> RecalculateFromPricesAsync(IReadOnlyDictionary<Guid, decimal> ingredientPrices);
    Task<RecipeCostSnapshotEntry> CreateCostSnapshotAsync(decimal? menuPrice, string? notes = null);
    Task<IReadOnlyList<RecipeCostSnapshotEntry>> GetCostHistoryAsync(int count = 10);
}

// ============================================================================
// Ingredient Price Grain
// ============================================================================

[GenerateSerializer]
public record CreateIngredientPriceCommand(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] string IngredientName,
    [property: Id(2)] decimal CurrentPrice,
    [property: Id(3)] string UnitOfMeasure,
    [property: Id(4)] decimal PackSize,
    [property: Id(5)] Guid? PreferredSupplierId,
    [property: Id(6)] string? PreferredSupplierName);

[GenerateSerializer]
public record UpdateIngredientPriceCommand(
    [property: Id(0)] decimal? CurrentPrice,
    [property: Id(1)] decimal? PackSize,
    [property: Id(2)] Guid? PreferredSupplierId,
    [property: Id(3)] string? PreferredSupplierName,
    [property: Id(4)] bool? IsActive);

[GenerateSerializer]
public record IngredientPriceSnapshot(
    [property: Id(0)] Guid Id,
    [property: Id(1)] Guid IngredientId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] decimal CurrentPrice,
    [property: Id(4)] string UnitOfMeasure,
    [property: Id(5)] decimal PackSize,
    [property: Id(6)] decimal PricePerUnit,
    [property: Id(7)] Guid? PreferredSupplierId,
    [property: Id(8)] string? PreferredSupplierName,
    [property: Id(9)] decimal? PreviousPrice,
    [property: Id(10)] DateTime? PriceChangedAt,
    [property: Id(11)] decimal PriceChangePercent,
    [property: Id(12)] bool IsActive);

[GenerateSerializer]
public record PriceHistoryEntry(
    [property: Id(0)] DateTime Timestamp,
    [property: Id(1)] decimal Price,
    [property: Id(2)] decimal PricePerUnit,
    [property: Id(3)] decimal ChangePercent,
    [property: Id(4)] Guid? SupplierId,
    [property: Id(5)] string? ChangeReason);

/// <summary>
/// Grain for ingredient price management.
/// Key: "{orgId}:ingredientprice:{ingredientId}"
/// </summary>
public interface IIngredientPriceGrain : IGrainWithStringKey
{
    Task<IngredientPriceSnapshot> CreateAsync(CreateIngredientPriceCommand command);
    Task<IngredientPriceSnapshot> UpdateAsync(UpdateIngredientPriceCommand command);
    Task<IngredientPriceSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();
    Task DeleteAsync();

    // Price operations
    Task<IngredientPriceSnapshot> UpdatePriceAsync(decimal newPrice, string? changeReason = null);
    Task<decimal> GetPricePerUnitAsync();
    Task<IReadOnlyList<PriceHistoryEntry>> GetPriceHistoryAsync(int count = 20);
}

// ============================================================================
// Cost Alert Grain
// ============================================================================

public enum CostAlertType
{
    RecipeCostIncrease,
    IngredientPriceIncrease,
    MarginBelowThreshold,
    IngredientPriceDecrease
}

public enum CostAlertAction
{
    None,
    PriceAdjusted,
    MenuUpdated,
    Accepted,
    Ignored
}

[GenerateSerializer]
public record CreateCostAlertCommand(
    [property: Id(0)] CostAlertType AlertType,
    [property: Id(1)] Guid? RecipeId,
    [property: Id(2)] string? RecipeName,
    [property: Id(3)] Guid? IngredientId,
    [property: Id(4)] string? IngredientName,
    [property: Id(5)] Guid? MenuItemId,
    [property: Id(6)] string? MenuItemName,
    [property: Id(7)] decimal PreviousValue,
    [property: Id(8)] decimal CurrentValue,
    [property: Id(9)] decimal? ThresholdValue,
    [property: Id(10)] string? ImpactDescription,
    [property: Id(11)] int AffectedRecipeCount);

[GenerateSerializer]
public record AcknowledgeCostAlertCommand(
    [property: Id(0)] Guid AcknowledgedByUserId,
    [property: Id(1)] string? Notes,
    [property: Id(2)] CostAlertAction ActionTaken);

[GenerateSerializer]
public record CostAlertSnapshot(
    [property: Id(0)] Guid AlertId,
    [property: Id(1)] CostAlertType AlertType,
    [property: Id(2)] Guid? RecipeId,
    [property: Id(3)] string? RecipeName,
    [property: Id(4)] Guid? IngredientId,
    [property: Id(5)] string? IngredientName,
    [property: Id(6)] Guid? MenuItemId,
    [property: Id(7)] string? MenuItemName,
    [property: Id(8)] decimal PreviousValue,
    [property: Id(9)] decimal CurrentValue,
    [property: Id(10)] decimal ChangePercent,
    [property: Id(11)] decimal? ThresholdValue,
    [property: Id(12)] string? ImpactDescription,
    [property: Id(13)] int AffectedRecipeCount,
    [property: Id(14)] bool IsAcknowledged,
    [property: Id(15)] DateTime? AcknowledgedAt,
    [property: Id(16)] Guid? AcknowledgedByUserId,
    [property: Id(17)] string? Notes,
    [property: Id(18)] CostAlertAction ActionTaken,
    [property: Id(19)] DateTime CreatedAt);

/// <summary>
/// Grain for cost alert management.
/// Key: "{orgId}:costalert:{alertId}"
/// </summary>
public interface ICostAlertGrain : IGrainWithStringKey
{
    Task<CostAlertSnapshot> CreateAsync(CreateCostAlertCommand command);
    Task<CostAlertSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();

    Task<CostAlertSnapshot> AcknowledgeAsync(AcknowledgeCostAlertCommand command);
    Task<bool> IsAcknowledgedAsync();
}

// ============================================================================
// Costing Settings Grain
// ============================================================================

[GenerateSerializer]
public record UpdateCostingSettingsCommand(
    [property: Id(0)] decimal? TargetFoodCostPercent,
    [property: Id(1)] decimal? TargetBeverageCostPercent,
    [property: Id(2)] decimal? MinimumMarginPercent,
    [property: Id(3)] decimal? WarningMarginPercent,
    [property: Id(4)] decimal? PriceChangeAlertThreshold,
    [property: Id(5)] decimal? CostIncreaseAlertThreshold,
    [property: Id(6)] bool? AutoRecalculateCosts,
    [property: Id(7)] bool? AutoCreateSnapshots,
    [property: Id(8)] int? SnapshotFrequencyDays);

[GenerateSerializer]
public record CostingSettingsSnapshot(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] decimal TargetFoodCostPercent,
    [property: Id(2)] decimal TargetBeverageCostPercent,
    [property: Id(3)] decimal MinimumMarginPercent,
    [property: Id(4)] decimal WarningMarginPercent,
    [property: Id(5)] decimal PriceChangeAlertThreshold,
    [property: Id(6)] decimal CostIncreaseAlertThreshold,
    [property: Id(7)] bool AutoRecalculateCosts,
    [property: Id(8)] bool AutoCreateSnapshots,
    [property: Id(9)] int SnapshotFrequencyDays);

/// <summary>
/// Grain for costing settings management.
/// Key: "{orgId}:{locationId}:costingsettings"
/// </summary>
public interface ICostingSettingsGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid locationId);
    Task<CostingSettingsSnapshot> GetSettingsAsync();
    Task<CostingSettingsSnapshot> UpdateAsync(UpdateCostingSettingsCommand command);
    Task<bool> ExistsAsync();

    // Threshold checks
    Task<bool> ShouldAlertOnPriceChangeAsync(decimal changePercent);
    Task<bool> ShouldAlertOnCostIncreaseAsync(decimal changePercent);
    Task<bool> IsMarginBelowMinimumAsync(decimal marginPercent);
    Task<bool> IsMarginBelowWarningAsync(decimal marginPercent);
}
