using DarkVelocity.Orleans.Grains.Grains;

namespace DarkVelocity.Orleans.Grains.State;

public class RecipeState
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid MenuItemId { get; set; }
    public string MenuItemName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? Description { get; set; }
    public int PortionYield { get; set; } = 1;
    public string? PrepInstructions { get; set; }
    public decimal CurrentCostPerPortion { get; set; }
    public DateTime? CostCalculatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<RecipeIngredientState> Ingredients { get; set; } = new();
    public List<RecipeCostSnapshotState> CostSnapshots { get; set; } = new();
}

public class RecipeIngredientState
{
    public Guid Id { get; set; }
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal WastePercentage { get; set; }
    public decimal CurrentUnitCost { get; set; }
    public decimal CurrentLineCost { get; set; }
}

public class RecipeCostSnapshotState
{
    public Guid Id { get; set; }
    public DateTime SnapshotDate { get; set; }
    public decimal CostPerPortion { get; set; }
    public decimal? MenuPrice { get; set; }
    public decimal? MarginPercent { get; set; }
    public string? Notes { get; set; }
}

public class IngredientPriceState
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal PackSize { get; set; } = 1;
    public decimal PricePerUnit { get; set; }
    public Guid? PreferredSupplierId { get; set; }
    public string? PreferredSupplierName { get; set; }
    public decimal? PreviousPrice { get; set; }
    public DateTime? PriceChangedAt { get; set; }
    public decimal PriceChangePercent { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<PriceHistoryEntryState> PriceHistory { get; set; } = new();
}

public class PriceHistoryEntryState
{
    public DateTime Timestamp { get; set; }
    public decimal Price { get; set; }
    public decimal PricePerUnit { get; set; }
    public decimal ChangePercent { get; set; }
    public Guid? SupplierId { get; set; }
    public string? ChangeReason { get; set; }
}

public class CostAlertState
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public CostAlertType AlertType { get; set; }
    public Guid? RecipeId { get; set; }
    public string? RecipeName { get; set; }
    public Guid? IngredientId { get; set; }
    public string? IngredientName { get; set; }
    public Guid? MenuItemId { get; set; }
    public string? MenuItemName { get; set; }
    public decimal PreviousValue { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal? ThresholdValue { get; set; }
    public string? ImpactDescription { get; set; }
    public int AffectedRecipeCount { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedByUserId { get; set; }
    public string? Notes { get; set; }
    public CostAlertAction ActionTaken { get; set; } = CostAlertAction.None;
    public DateTime CreatedAt { get; set; }
}

public class CostingSettingsState
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LocationId { get; set; }
    public decimal TargetFoodCostPercent { get; set; } = 30;
    public decimal TargetBeverageCostPercent { get; set; } = 25;
    public decimal MinimumMarginPercent { get; set; } = 50;
    public decimal WarningMarginPercent { get; set; } = 60;
    public decimal PriceChangeAlertThreshold { get; set; } = 10;
    public decimal CostIncreaseAlertThreshold { get; set; } = 5;
    public bool AutoRecalculateCosts { get; set; } = true;
    public bool AutoCreateSnapshots { get; set; } = true;
    public int SnapshotFrequencyDays { get; set; } = 7;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
