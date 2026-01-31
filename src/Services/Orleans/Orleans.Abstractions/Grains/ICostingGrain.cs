namespace DarkVelocity.Orleans.Abstractions.Grains;

// ============================================================================
// Recipe Grain
// ============================================================================

public record CreateRecipeCommand(
    Guid MenuItemId,
    string MenuItemName,
    string Code,
    Guid? CategoryId,
    string? CategoryName,
    string? Description,
    int PortionYield,
    string? PrepInstructions);

public record UpdateRecipeCommand(
    string? MenuItemName,
    string? Code,
    Guid? CategoryId,
    string? CategoryName,
    string? Description,
    int? PortionYield,
    string? PrepInstructions,
    bool? IsActive);

public record RecipeIngredientCommand(
    Guid IngredientId,
    string IngredientName,
    decimal Quantity,
    string UnitOfMeasure,
    decimal WastePercentage,
    decimal CurrentUnitCost);

public record RecipeIngredientSnapshot(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    decimal Quantity,
    string UnitOfMeasure,
    decimal WastePercentage,
    decimal EffectiveQuantity,
    decimal CurrentUnitCost,
    decimal CurrentLineCost,
    decimal CostPercentOfTotal);

public record RecipeCostCalculation(
    Guid RecipeId,
    string RecipeName,
    decimal TotalIngredientCost,
    decimal CostPerPortion,
    int PortionYield,
    decimal? MenuPrice,
    decimal? CostPercentage,
    decimal? GrossMarginPercent,
    IReadOnlyList<RecipeIngredientSnapshot> IngredientCosts);

public record RecipeSnapshot(
    Guid RecipeId,
    Guid MenuItemId,
    string MenuItemName,
    string Code,
    Guid? CategoryId,
    string? CategoryName,
    string? Description,
    int PortionYield,
    string? PrepInstructions,
    decimal CurrentCostPerPortion,
    DateTime? CostCalculatedAt,
    bool IsActive,
    IReadOnlyList<RecipeIngredientSnapshot> Ingredients);

public record RecipeCostSnapshotEntry(
    Guid SnapshotId,
    DateTime SnapshotDate,
    decimal CostPerPortion,
    decimal? MenuPrice,
    decimal? MarginPercent,
    string? Notes);

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
    Task<IReadOnlyList<RecipeIngredientSnapshot>> GetIngredientsAsync();

    // Cost calculation
    Task<RecipeCostCalculation> CalculateCostAsync(decimal? menuPrice = null);
    Task<RecipeSnapshot> RecalculateFromPricesAsync(IReadOnlyDictionary<Guid, decimal> ingredientPrices);
    Task<RecipeCostSnapshotEntry> CreateCostSnapshotAsync(decimal? menuPrice, string? notes = null);
    Task<IReadOnlyList<RecipeCostSnapshotEntry>> GetCostHistoryAsync(int count = 10);
}

// ============================================================================
// Ingredient Price Grain
// ============================================================================

public record CreateIngredientPriceCommand(
    Guid IngredientId,
    string IngredientName,
    decimal CurrentPrice,
    string UnitOfMeasure,
    decimal PackSize,
    Guid? PreferredSupplierId,
    string? PreferredSupplierName);

public record UpdateIngredientPriceCommand(
    decimal? CurrentPrice,
    decimal? PackSize,
    Guid? PreferredSupplierId,
    string? PreferredSupplierName,
    bool? IsActive);

public record IngredientPriceSnapshot(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    decimal CurrentPrice,
    string UnitOfMeasure,
    decimal PackSize,
    decimal PricePerUnit,
    Guid? PreferredSupplierId,
    string? PreferredSupplierName,
    decimal? PreviousPrice,
    DateTime? PriceChangedAt,
    decimal PriceChangePercent,
    bool IsActive);

public record PriceHistoryEntry(
    DateTime Timestamp,
    decimal Price,
    decimal PricePerUnit,
    decimal ChangePercent,
    Guid? SupplierId,
    string? ChangeReason);

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

public record CreateCostAlertCommand(
    CostAlertType AlertType,
    Guid? RecipeId,
    string? RecipeName,
    Guid? IngredientId,
    string? IngredientName,
    Guid? MenuItemId,
    string? MenuItemName,
    decimal PreviousValue,
    decimal CurrentValue,
    decimal? ThresholdValue,
    string? ImpactDescription,
    int AffectedRecipeCount);

public record AcknowledgeCostAlertCommand(
    Guid AcknowledgedByUserId,
    string? Notes,
    CostAlertAction ActionTaken);

public record CostAlertSnapshot(
    Guid AlertId,
    CostAlertType AlertType,
    Guid? RecipeId,
    string? RecipeName,
    Guid? IngredientId,
    string? IngredientName,
    Guid? MenuItemId,
    string? MenuItemName,
    decimal PreviousValue,
    decimal CurrentValue,
    decimal ChangePercent,
    decimal? ThresholdValue,
    string? ImpactDescription,
    int AffectedRecipeCount,
    bool IsAcknowledged,
    DateTime? AcknowledgedAt,
    Guid? AcknowledgedByUserId,
    string? Notes,
    CostAlertAction ActionTaken,
    DateTime CreatedAt);

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

public record UpdateCostingSettingsCommand(
    decimal? TargetFoodCostPercent,
    decimal? TargetBeverageCostPercent,
    decimal? MinimumMarginPercent,
    decimal? WarningMarginPercent,
    decimal? PriceChangeAlertThreshold,
    decimal? CostIncreaseAlertThreshold,
    bool? AutoRecalculateCosts,
    bool? AutoCreateSnapshots,
    int? SnapshotFrequencyDays);

public record CostingSettingsSnapshot(
    Guid LocationId,
    decimal TargetFoodCostPercent,
    decimal TargetBeverageCostPercent,
    decimal MinimumMarginPercent,
    decimal WarningMarginPercent,
    decimal PriceChangeAlertThreshold,
    decimal CostIncreaseAlertThreshold,
    bool AutoRecalculateCosts,
    bool AutoCreateSnapshots,
    int SnapshotFrequencyDays);

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
