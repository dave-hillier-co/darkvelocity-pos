using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Costing;

/// <summary>
/// FIFO (First-In-First-Out) costing policy.
/// Consumes from oldest batches first, using actual batch costs.
/// </summary>
public class FifoCostingPolicy : ICostingPolicy
{
    public string PolicyName => "First-In-First-Out";
    public CostingMethod Method => CostingMethod.FIFO;

    public ConsumptionCost CalculateCost(
        IReadOnlyList<StockBatch> batches,
        decimal quantity,
        string unit,
        DateTime asOfDate)
    {
        var remaining = quantity;
        var breakdown = new List<BatchCostBreakdown>();
        var totalCost = 0m;

        // Order by received date (oldest first)
        var orderedBatches = batches
            .Where(b => b.Status == BatchStatus.Active && b.Quantity > 0)
            .OrderBy(b => b.ReceivedDate)
            .ToList();

        foreach (var batch in orderedBatches)
        {
            if (remaining <= 0) break;

            var consumeQty = Math.Min(remaining, batch.Quantity);
            var batchCost = consumeQty * batch.UnitCost;

            breakdown.Add(new BatchCostBreakdown
            {
                BatchId = batch.Id,
                BatchNumber = batch.BatchNumber,
                Quantity = consumeQty,
                UnitCost = batch.UnitCost,
                TotalCost = batchCost,
                ReceivedDate = batch.ReceivedDate,
                ExpiryDate = batch.ExpiryDate
            });

            totalCost += batchCost;
            remaining -= consumeQty;
        }

        var actualQuantity = quantity - remaining;
        var unitCost = actualQuantity > 0 ? totalCost / actualQuantity : 0;

        return new ConsumptionCost
        {
            IngredientId = batches.FirstOrDefault()?.Id ?? Guid.Empty,
            Quantity = actualQuantity,
            Unit = unit,
            UnitCost = unitCost,
            TotalCost = totalCost,
            Method = CostingMethod.FIFO,
            BatchBreakdown = breakdown,
            AsOfDate = asOfDate
        };
    }

    public ConsumptionCost CalculateCost(
        Guid ingredientId,
        decimal quantity,
        string unit,
        decimal weightedAverageCost,
        DateTime asOfDate)
    {
        // Fallback when batch data not available - use WAC as approximation
        return new ConsumptionCost
        {
            IngredientId = ingredientId,
            Quantity = quantity,
            Unit = unit,
            UnitCost = weightedAverageCost,
            TotalCost = quantity * weightedAverageCost,
            Method = CostingMethod.FIFO,
            AsOfDate = asOfDate
        };
    }
}

/// <summary>
/// LIFO (Last-In-First-Out) costing policy.
/// Consumes from newest batches first.
/// </summary>
public class LifoCostingPolicy : ICostingPolicy
{
    public string PolicyName => "Last-In-First-Out";
    public CostingMethod Method => CostingMethod.LIFO;

    public ConsumptionCost CalculateCost(
        IReadOnlyList<StockBatch> batches,
        decimal quantity,
        string unit,
        DateTime asOfDate)
    {
        var remaining = quantity;
        var breakdown = new List<BatchCostBreakdown>();
        var totalCost = 0m;

        // Order by received date descending (newest first)
        var orderedBatches = batches
            .Where(b => b.Status == BatchStatus.Active && b.Quantity > 0)
            .OrderByDescending(b => b.ReceivedDate)
            .ToList();

        foreach (var batch in orderedBatches)
        {
            if (remaining <= 0) break;

            var consumeQty = Math.Min(remaining, batch.Quantity);
            var batchCost = consumeQty * batch.UnitCost;

            breakdown.Add(new BatchCostBreakdown
            {
                BatchId = batch.Id,
                BatchNumber = batch.BatchNumber,
                Quantity = consumeQty,
                UnitCost = batch.UnitCost,
                TotalCost = batchCost,
                ReceivedDate = batch.ReceivedDate,
                ExpiryDate = batch.ExpiryDate
            });

            totalCost += batchCost;
            remaining -= consumeQty;
        }

        var actualQuantity = quantity - remaining;
        var unitCost = actualQuantity > 0 ? totalCost / actualQuantity : 0;

        return new ConsumptionCost
        {
            IngredientId = batches.FirstOrDefault()?.Id ?? Guid.Empty,
            Quantity = actualQuantity,
            Unit = unit,
            UnitCost = unitCost,
            TotalCost = totalCost,
            Method = CostingMethod.LIFO,
            BatchBreakdown = breakdown,
            AsOfDate = asOfDate
        };
    }

    public ConsumptionCost CalculateCost(
        Guid ingredientId,
        decimal quantity,
        string unit,
        decimal weightedAverageCost,
        DateTime asOfDate)
    {
        return new ConsumptionCost
        {
            IngredientId = ingredientId,
            Quantity = quantity,
            Unit = unit,
            UnitCost = weightedAverageCost,
            TotalCost = quantity * weightedAverageCost,
            Method = CostingMethod.LIFO,
            AsOfDate = asOfDate
        };
    }
}

/// <summary>
/// Weighted Average Cost (WAC) costing policy.
/// Uses the running weighted average across all inventory.
/// </summary>
public class WeightedAverageCostingPolicy : ICostingPolicy
{
    public string PolicyName => "Weighted Average Cost";
    public CostingMethod Method => CostingMethod.WAC;

    public ConsumptionCost CalculateCost(
        IReadOnlyList<StockBatch> batches,
        decimal quantity,
        string unit,
        DateTime asOfDate)
    {
        var activeBatches = batches
            .Where(b => b.Status == BatchStatus.Active && b.Quantity > 0)
            .ToList();

        var totalQuantity = activeBatches.Sum(b => b.Quantity);
        var totalValue = activeBatches.Sum(b => b.Quantity * b.UnitCost);
        var wac = totalQuantity > 0 ? totalValue / totalQuantity : 0;

        var actualQuantity = Math.Min(quantity, totalQuantity);
        var totalCost = actualQuantity * wac;

        return new ConsumptionCost
        {
            IngredientId = batches.FirstOrDefault()?.Id ?? Guid.Empty,
            Quantity = actualQuantity,
            Unit = unit,
            UnitCost = wac,
            TotalCost = totalCost,
            Method = CostingMethod.WAC,
            AsOfDate = asOfDate
        };
    }

    public ConsumptionCost CalculateCost(
        Guid ingredientId,
        decimal quantity,
        string unit,
        decimal weightedAverageCost,
        DateTime asOfDate)
    {
        return new ConsumptionCost
        {
            IngredientId = ingredientId,
            Quantity = quantity,
            Unit = unit,
            UnitCost = weightedAverageCost,
            TotalCost = quantity * weightedAverageCost,
            Method = CostingMethod.WAC,
            AsOfDate = asOfDate
        };
    }

    /// <summary>
    /// Calculate the new WAC after receiving a batch.
    /// </summary>
    public static decimal CalculateNewWAC(
        decimal existingQuantity,
        decimal existingWAC,
        decimal newQuantity,
        decimal newUnitCost)
    {
        var existingValue = existingQuantity * existingWAC;
        var newValue = newQuantity * newUnitCost;
        var totalQuantity = existingQuantity + newQuantity;

        return totalQuantity > 0 ? (existingValue + newValue) / totalQuantity : 0;
    }
}

/// <summary>
/// Standard Cost costing policy.
/// Uses the latest supplier price from catalog.
/// </summary>
public class StandardCostingPolicy : ICostingPolicy
{
    private readonly Func<Guid, decimal>? _standardCostLookup;

    public StandardCostingPolicy(Func<Guid, decimal>? standardCostLookup = null)
    {
        _standardCostLookup = standardCostLookup;
    }

    public string PolicyName => "Standard Cost";
    public CostingMethod Method => CostingMethod.Standard;

    public ConsumptionCost CalculateCost(
        IReadOnlyList<StockBatch> batches,
        decimal quantity,
        string unit,
        DateTime asOfDate)
    {
        // For standard cost, use the most recent batch cost as the standard
        // In production, this would be looked up from a catalog
        var mostRecentBatch = batches
            .Where(b => b.Status == BatchStatus.Active)
            .OrderByDescending(b => b.ReceivedDate)
            .FirstOrDefault();

        var standardCost = mostRecentBatch?.UnitCost ?? 0;

        if (_standardCostLookup != null && batches.Count > 0)
        {
            var ingredientId = batches.First().Id;
            standardCost = _standardCostLookup(ingredientId);
        }

        return new ConsumptionCost
        {
            IngredientId = batches.FirstOrDefault()?.Id ?? Guid.Empty,
            Quantity = quantity,
            Unit = unit,
            UnitCost = standardCost,
            TotalCost = quantity * standardCost,
            Method = CostingMethod.Standard,
            AsOfDate = asOfDate
        };
    }

    public ConsumptionCost CalculateCost(
        Guid ingredientId,
        decimal quantity,
        string unit,
        decimal weightedAverageCost,
        DateTime asOfDate)
    {
        var standardCost = _standardCostLookup?.Invoke(ingredientId) ?? weightedAverageCost;

        return new ConsumptionCost
        {
            IngredientId = ingredientId,
            Quantity = quantity,
            Unit = unit,
            UnitCost = standardCost,
            TotalCost = quantity * standardCost,
            Method = CostingMethod.Standard,
            AsOfDate = asOfDate
        };
    }
}

/// <summary>
/// Factory for creating costing policy instances.
/// </summary>
public static class CostingPolicyFactory
{
    public static ICostingPolicy Create(CostingMethod method, Func<Guid, decimal>? standardCostLookup = null)
    {
        return method switch
        {
            CostingMethod.FIFO => new FifoCostingPolicy(),
            CostingMethod.LIFO => new LifoCostingPolicy(),
            CostingMethod.WAC => new WeightedAverageCostingPolicy(),
            CostingMethod.Standard => new StandardCostingPolicy(standardCostLookup),
            _ => throw new ArgumentException($"Unknown costing method: {method}")
        };
    }
}
