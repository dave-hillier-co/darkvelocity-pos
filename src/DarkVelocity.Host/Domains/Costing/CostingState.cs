using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

[GenerateSerializer]
public class RecipeState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid MenuItemId { get; set; }
    [Id(3)] public string MenuItemName { get; set; } = string.Empty;
    [Id(4)] public string Code { get; set; } = string.Empty;
    [Id(5)] public Guid? CategoryId { get; set; }
    [Id(6)] public string? CategoryName { get; set; }
    [Id(7)] public string? Description { get; set; }
    [Id(8)] public int PortionYield { get; set; } = 1;
    [Id(9)] public string? PrepInstructions { get; set; }
    [Id(10)] public decimal CurrentCostPerPortion { get; set; }
    [Id(11)] public DateTime? CostCalculatedAt { get; set; }
    [Id(12)] public bool IsActive { get; set; } = true;
    [Id(13)] public DateTime CreatedAt { get; set; }
    [Id(14)] public DateTime? UpdatedAt { get; set; }
    [Id(15)] public List<CostingRecipeIngredientState> Ingredients { get; set; } = new();
    [Id(16)] public List<RecipeCostSnapshotState> CostSnapshots { get; set; } = new();
}

[GenerateSerializer]
public class CostingRecipeIngredientState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid IngredientId { get; set; }
    [Id(2)] public string IngredientName { get; set; } = string.Empty;
    [Id(3)] public decimal Quantity { get; set; }
    [Id(4)] public string UnitOfMeasure { get; set; } = string.Empty;
    [Id(5)] public decimal WastePercentage { get; set; }
    [Id(6)] public decimal CurrentUnitCost { get; set; }
    [Id(7)] public decimal CurrentLineCost { get; set; }
}

[GenerateSerializer]
public class RecipeCostSnapshotState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public DateTime SnapshotDate { get; set; }
    [Id(2)] public decimal CostPerPortion { get; set; }
    [Id(3)] public decimal? MenuPrice { get; set; }
    [Id(4)] public decimal? MarginPercent { get; set; }
    [Id(5)] public string? Notes { get; set; }
}

[GenerateSerializer]
public class IngredientPriceState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid IngredientId { get; set; }
    [Id(3)] public string IngredientName { get; set; } = string.Empty;
    [Id(4)] public decimal CurrentPrice { get; set; }
    [Id(5)] public string UnitOfMeasure { get; set; } = string.Empty;
    [Id(6)] public decimal PackSize { get; set; } = 1;
    [Id(7)] public decimal PricePerUnit { get; set; }
    [Id(8)] public Guid? PreferredSupplierId { get; set; }
    [Id(9)] public string? PreferredSupplierName { get; set; }
    [Id(10)] public decimal? PreviousPrice { get; set; }
    [Id(11)] public DateTime? PriceChangedAt { get; set; }
    [Id(12)] public decimal PriceChangePercent { get; set; }
    [Id(13)] public bool IsActive { get; set; } = true;
    [Id(14)] public DateTime CreatedAt { get; set; }
    [Id(15)] public DateTime? UpdatedAt { get; set; }
    [Id(16)] public List<PriceHistoryEntryState> PriceHistory { get; set; } = new();
}

[GenerateSerializer]
public class PriceHistoryEntryState
{
    [Id(0)] public DateTime Timestamp { get; set; }
    [Id(1)] public decimal Price { get; set; }
    [Id(2)] public decimal PricePerUnit { get; set; }
    [Id(3)] public decimal ChangePercent { get; set; }
    [Id(4)] public Guid? SupplierId { get; set; }
    [Id(5)] public string? ChangeReason { get; set; }
}

[GenerateSerializer]
public class CostAlertState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public CostAlertType AlertType { get; set; }
    [Id(3)] public Guid? RecipeId { get; set; }
    [Id(4)] public string? RecipeName { get; set; }
    [Id(5)] public Guid? IngredientId { get; set; }
    [Id(6)] public string? IngredientName { get; set; }
    [Id(7)] public Guid? MenuItemId { get; set; }
    [Id(8)] public string? MenuItemName { get; set; }
    [Id(9)] public decimal PreviousValue { get; set; }
    [Id(10)] public decimal CurrentValue { get; set; }
    [Id(11)] public decimal ChangePercent { get; set; }
    [Id(12)] public decimal? ThresholdValue { get; set; }
    [Id(13)] public string? ImpactDescription { get; set; }
    [Id(14)] public int AffectedRecipeCount { get; set; }
    [Id(15)] public bool IsAcknowledged { get; set; }
    [Id(16)] public DateTime? AcknowledgedAt { get; set; }
    [Id(17)] public Guid? AcknowledgedByUserId { get; set; }
    [Id(18)] public string? Notes { get; set; }
    [Id(19)] public CostAlertAction ActionTaken { get; set; } = CostAlertAction.None;
    [Id(20)] public DateTime CreatedAt { get; set; }
}

[GenerateSerializer]
public class CostingSettingsState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid LocationId { get; set; }
    [Id(3)] public decimal TargetFoodCostPercent { get; set; } = 30;
    [Id(4)] public decimal TargetBeverageCostPercent { get; set; } = 25;
    [Id(5)] public decimal MinimumMarginPercent { get; set; } = 50;
    [Id(6)] public decimal WarningMarginPercent { get; set; } = 60;
    [Id(7)] public decimal PriceChangeAlertThreshold { get; set; } = 10;
    [Id(8)] public decimal CostIncreaseAlertThreshold { get; set; } = 5;
    [Id(9)] public bool AutoRecalculateCosts { get; set; } = true;
    [Id(10)] public bool AutoCreateSnapshots { get; set; } = true;
    [Id(11)] public int SnapshotFrequencyDays { get; set; } = 7;
    [Id(12)] public DateTime CreatedAt { get; set; }
    [Id(13)] public DateTime? UpdatedAt { get; set; }
}
