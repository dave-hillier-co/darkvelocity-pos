using DarkVelocity.Orleans.Abstractions.Costing;
using DarkVelocity.Orleans.Abstractions.Projections;
using DarkVelocity.Shared.Contracts.Events;

namespace DarkVelocity.Orleans.Abstractions.State;

// ============================================================================
// Daily Sales Aggregation State
// ============================================================================

[GenerateSerializer]
public sealed class DailySalesState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public string SiteName { get; set; } = string.Empty;
    [Id(3)] public DateTime BusinessDate { get; set; }
    [Id(4)] public bool IsFinalized { get; set; }

    // Aggregated totals
    [Id(5)] public decimal GrossSales { get; set; }
    [Id(6)] public decimal Discounts { get; set; }
    [Id(7)] public decimal Voids { get; set; }
    [Id(8)] public decimal Comps { get; set; }
    [Id(9)] public decimal Tax { get; set; }
    [Id(10)] public decimal NetSales { get; set; }
    [Id(11)] public decimal TheoreticalCOGS { get; set; }
    [Id(12)] public decimal ActualCOGS { get; set; }
    [Id(13)] public int TransactionCount { get; set; }
    [Id(14)] public int GuestCount { get; set; }

    // Breakdown by channel
    [Id(15)] public Dictionary<SaleChannel, decimal> SalesByChannel { get; set; } = [];

    // Breakdown by category
    [Id(16)] public Dictionary<string, decimal> SalesByCategory { get; set; } = [];

    // Individual facts (for detailed queries)
    [Id(17)] public List<SalesFact> Facts { get; set; } = [];

    [Id(18)] public int Version { get; set; }
}

// ============================================================================
// Daily Inventory Snapshot State
// ============================================================================

[GenerateSerializer]
public sealed class DailyInventorySnapshotState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public string SiteName { get; set; } = string.Empty;
    [Id(3)] public DateTime BusinessDate { get; set; }
    [Id(4)] public bool IsFinalized { get; set; }

    // Aggregated totals
    [Id(5)] public decimal TotalStockValue { get; set; }
    [Id(6)] public int TotalSkuCount { get; set; }
    [Id(7)] public int ActiveBatchCount { get; set; }
    [Id(8)] public int LowStockCount { get; set; }
    [Id(9)] public int OutOfStockCount { get; set; }
    [Id(10)] public int ExpiringSoonCount { get; set; }
    [Id(11)] public decimal ExpiringSoonValue { get; set; }
    [Id(12)] public int OverParCount { get; set; }
    [Id(13)] public decimal OverParValue { get; set; }

    // Individual snapshots
    [Id(14)] public List<IngredientSnapshotState> Ingredients { get; set; } = [];

    // Individual facts
    [Id(15)] public List<InventoryFact> Facts { get; set; } = [];

    [Id(16)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class IngredientSnapshotState
{
    [Id(0)] public Guid IngredientId { get; set; }
    [Id(1)] public string IngredientName { get; set; } = string.Empty;
    [Id(2)] public string Sku { get; set; } = string.Empty;
    [Id(3)] public string Category { get; set; } = string.Empty;
    [Id(4)] public decimal OnHandQuantity { get; set; }
    [Id(5)] public decimal AvailableQuantity { get; set; }
    [Id(6)] public string Unit { get; set; } = string.Empty;
    [Id(7)] public decimal WeightedAverageCost { get; set; }
    [Id(8)] public decimal TotalValue { get; set; }
    [Id(9)] public DateTime? EarliestExpiry { get; set; }
    [Id(10)] public bool IsLowStock { get; set; }
    [Id(11)] public bool IsOutOfStock { get; set; }
    [Id(12)] public bool IsExpiringSoon { get; set; }
    [Id(13)] public bool IsOverPar { get; set; }
    [Id(14)] public int ActiveBatchCount { get; set; }
}

// ============================================================================
// Daily Consumption State
// ============================================================================

[GenerateSerializer]
public sealed class DailyConsumptionState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public DateTime BusinessDate { get; set; }

    // Aggregated totals
    [Id(3)] public decimal TotalTheoreticalCost { get; set; }
    [Id(4)] public decimal TotalActualCost { get; set; }

    // Breakdown by ingredient
    [Id(5)] public Dictionary<Guid, ConsumptionAggregation> ByIngredient { get; set; } = [];

    // Individual facts
    [Id(6)] public List<ConsumptionFact> Facts { get; set; } = [];

    [Id(7)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class ConsumptionAggregation
{
    [Id(0)] public Guid IngredientId { get; set; }
    [Id(1)] public string IngredientName { get; set; } = string.Empty;
    [Id(2)] public string Category { get; set; } = string.Empty;
    [Id(3)] public decimal TheoreticalQuantity { get; set; }
    [Id(4)] public decimal TheoreticalCost { get; set; }
    [Id(5)] public decimal ActualQuantity { get; set; }
    [Id(6)] public decimal ActualCost { get; set; }
}

// ============================================================================
// Daily Waste State
// ============================================================================

[GenerateSerializer]
public sealed class DailyWasteState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public DateTime BusinessDate { get; set; }

    // Aggregated totals
    [Id(3)] public decimal TotalWasteValue { get; set; }
    [Id(4)] public int TotalWasteCount { get; set; }

    // Breakdown by reason
    [Id(5)] public Dictionary<WasteReason, decimal> ByReason { get; set; } = [];

    // Breakdown by category
    [Id(6)] public Dictionary<string, decimal> ByCategory { get; set; } = [];

    // Individual facts
    [Id(7)] public List<WasteFact> Facts { get; set; } = [];

    [Id(8)] public int Version { get; set; }
}

// ============================================================================
// Period Aggregation State
// ============================================================================

[GenerateSerializer]
public sealed class PeriodAggregationState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public PeriodType PeriodType { get; set; }
    [Id(3)] public DateTime PeriodStart { get; set; }
    [Id(4)] public DateTime PeriodEnd { get; set; }
    [Id(5)] public int PeriodNumber { get; set; }
    [Id(6)] public int FiscalYear { get; set; }
    [Id(7)] public bool IsFinalized { get; set; }

    // Sales metrics
    [Id(8)] public decimal GrossSales { get; set; }
    [Id(9)] public decimal Discounts { get; set; }
    [Id(10)] public decimal Voids { get; set; }
    [Id(11)] public decimal Comps { get; set; }
    [Id(12)] public decimal Tax { get; set; }
    [Id(13)] public decimal NetSales { get; set; }
    [Id(14)] public int TransactionCount { get; set; }
    [Id(15)] public int CoversServed { get; set; }

    // COGS (FIFO)
    [Id(16)] public decimal FifoActualCOGS { get; set; }
    [Id(17)] public decimal FifoTheoreticalCOGS { get; set; }

    // COGS (WAC)
    [Id(18)] public decimal WacActualCOGS { get; set; }
    [Id(19)] public decimal WacTheoreticalCOGS { get; set; }

    // Stock metrics (period-end)
    [Id(20)] public decimal ClosingStockValue { get; set; }
    [Id(21)] public int LowStockCount { get; set; }
    [Id(22)] public int OutOfStockCount { get; set; }

    // Waste
    [Id(23)] public decimal TotalWasteValue { get; set; }

    // Days included
    [Id(24)] public List<DateTime> IncludedDates { get; set; } = [];

    [Id(25)] public int Version { get; set; }
}

// ============================================================================
// Site Dashboard State
// ============================================================================

[GenerateSerializer]
public sealed class SiteDashboardState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public string SiteName { get; set; } = string.Empty;
    [Id(3)] public DateTime LastRefreshed { get; set; }

    // Today's snapshot references
    [Id(4)] public DateTime CurrentBusinessDate { get; set; }

    // Cached comparisons
    [Id(5)] public decimal LastWeekSameDayNetSales { get; set; }
    [Id(6)] public decimal LastYearSameDayNetSales { get; set; }
    [Id(7)] public decimal BudgetGrossProfitPercent { get; set; }

    // Alert counts
    [Id(8)] public int LowStockAlertCount { get; set; }
    [Id(9)] public int OutOfStockAlertCount { get; set; }
    [Id(10)] public int ExpiryRiskCount { get; set; }
    [Id(11)] public int HighVarianceCount { get; set; }
    [Id(12)] public decimal OutstandingPOValue { get; set; }

    // Cached top variances
    [Id(13)] public List<VarianceBreakdownState> TopVariances { get; set; } = [];

    // Cached low stock items
    [Id(14)] public List<IngredientSnapshotState> LowStockItems { get; set; } = [];

    [Id(15)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class VarianceBreakdownState
{
    [Id(0)] public Guid IngredientId { get; set; }
    [Id(1)] public string IngredientName { get; set; } = string.Empty;
    [Id(2)] public string Category { get; set; } = string.Empty;
    [Id(3)] public decimal TheoreticalUsage { get; set; }
    [Id(4)] public decimal ActualUsage { get; set; }
    [Id(5)] public decimal UsageVariance { get; set; }
    [Id(6)] public decimal TheoreticalCost { get; set; }
    [Id(7)] public decimal ActualCost { get; set; }
    [Id(8)] public decimal CostVariance { get; set; }
    [Id(9)] public VarianceReason? LikelyReason { get; set; }
}

// ============================================================================
// Enums
// ============================================================================

[GenerateSerializer]
public enum PeriodType
{
    Daily,
    Weekly,
    FourWeek,
    Monthly,
    Yearly
}
