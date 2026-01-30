using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Costing;

/// <summary>
/// Represents the result of a cost calculation.
/// </summary>
public record ConsumptionCost
{
    /// <summary>
    /// Ingredient that was costed.
    /// </summary>
    public required Guid IngredientId { get; init; }

    /// <summary>
    /// Quantity costed.
    /// </summary>
    public required decimal Quantity { get; init; }

    /// <summary>
    /// Unit of measure.
    /// </summary>
    public required string Unit { get; init; }

    /// <summary>
    /// Calculated unit cost.
    /// </summary>
    public required decimal UnitCost { get; init; }

    /// <summary>
    /// Total cost (Quantity * UnitCost).
    /// </summary>
    public required decimal TotalCost { get; init; }

    /// <summary>
    /// The costing policy used.
    /// </summary>
    public required CostingMethod Method { get; init; }

    /// <summary>
    /// Breakdown by batch (for FIFO).
    /// </summary>
    public IReadOnlyList<BatchCostBreakdown>? BatchBreakdown { get; init; }

    /// <summary>
    /// As-of date for the cost calculation.
    /// </summary>
    public required DateTime AsOfDate { get; init; }
}

/// <summary>
/// Cost breakdown by batch.
/// </summary>
public record BatchCostBreakdown
{
    public required Guid BatchId { get; init; }
    public required string BatchNumber { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitCost { get; init; }
    public required decimal TotalCost { get; init; }
    public required DateTime ReceivedDate { get; init; }
    public DateTime? ExpiryDate { get; init; }
}

/// <summary>
/// Supported costing methods.
/// </summary>
public enum CostingMethod
{
    /// <summary>
    /// First-In-First-Out: Uses actual batch costs, consuming oldest batches first.
    /// Best for: Weekly ops GP, food safety compliance, batch traceability.
    /// </summary>
    FIFO,

    /// <summary>
    /// Weighted Average Cost: Running average cost across all inventory.
    /// Best for: Monthly management GP, smoothing cost fluctuations.
    /// </summary>
    WAC,

    /// <summary>
    /// Standard Cost: Latest supplier price from catalog.
    /// Best for: Menu engineering, pricing decisions, budget planning.
    /// </summary>
    Standard,

    /// <summary>
    /// Last-In-First-Out: Uses most recent batch costs.
    /// Best for: Specific scenarios where recent costs are more relevant.
    /// </summary>
    LIFO
}

/// <summary>
/// Interface for costing policy implementations.
/// </summary>
public interface ICostingPolicy
{
    /// <summary>
    /// The name of this costing policy.
    /// </summary>
    string PolicyName { get; }

    /// <summary>
    /// The costing method this policy implements.
    /// </summary>
    CostingMethod Method { get; }

    /// <summary>
    /// Calculate the cost for consuming a quantity of an ingredient.
    /// </summary>
    /// <param name="batches">Available batches ordered appropriately for the policy.</param>
    /// <param name="quantity">Quantity to cost.</param>
    /// <param name="unit">Unit of measure.</param>
    /// <param name="asOfDate">Date for the cost calculation.</param>
    /// <returns>Cost calculation result.</returns>
    ConsumptionCost CalculateCost(
        IReadOnlyList<StockBatch> batches,
        decimal quantity,
        string unit,
        DateTime asOfDate);

    /// <summary>
    /// Calculate the cost for consuming a quantity using cached cost data.
    /// </summary>
    ConsumptionCost CalculateCost(
        Guid ingredientId,
        decimal quantity,
        string unit,
        decimal weightedAverageCost,
        DateTime asOfDate);
}

/// <summary>
/// Recipe ingredient with quantity for costing.
/// </summary>
public record RecipeIngredientCost
{
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required decimal WastePercentage { get; init; }
    public required decimal EffectiveQuantity { get; init; }
    public required decimal Cost { get; init; }
    public required CostingMethod Method { get; init; }
}

/// <summary>
/// Result of costing a full recipe.
/// </summary>
public record RecipeCostResult
{
    public required Guid RecipeId { get; init; }
    public required Guid? RecipeVersionId { get; init; }
    public required string RecipeName { get; init; }
    public required IReadOnlyList<RecipeIngredientCost> Ingredients { get; init; }
    public required decimal TotalCost { get; init; }
    public required decimal PortionYield { get; init; }
    public required decimal CostPerPortion { get; init; }
    public required CostingMethod Method { get; init; }
    public required DateTime AsOfDate { get; init; }
}

/// <summary>
/// Configuration for costing policy per use case.
/// </summary>
public record CostingConfiguration
{
    /// <summary>
    /// Default costing method for the organization.
    /// </summary>
    public CostingMethod DefaultMethod { get; init; } = CostingMethod.FIFO;

    /// <summary>
    /// Method for weekly operations GP.
    /// </summary>
    public CostingMethod WeeklyOpsMethod { get; init; } = CostingMethod.FIFO;

    /// <summary>
    /// Method for monthly management GP.
    /// </summary>
    public CostingMethod MonthlyManagementMethod { get; init; } = CostingMethod.WAC;

    /// <summary>
    /// Method for menu engineering/pricing.
    /// </summary>
    public CostingMethod MenuEngineeringMethod { get; init; } = CostingMethod.Standard;

    /// <summary>
    /// Include waste percentage in cost calculations.
    /// </summary>
    public bool IncludeWastePercentage { get; init; } = true;
}
