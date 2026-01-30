using DarkVelocity.Orleans.Abstractions.Costing;
using DarkVelocity.Shared.Contracts.Events;

namespace DarkVelocity.Orleans.Abstractions.Projections;

// ============================================================================
// Sales Fact Projection
// ============================================================================

/// <summary>
/// Sales fact at the date × location × channel × product grain.
/// </summary>
[GenerateSerializer]
public sealed record SalesFact
{
    [Id(0)] public required Guid FactId { get; init; }
    [Id(1)] public required DateTime Date { get; init; }
    [Id(2)] public required Guid OrgId { get; init; }
    [Id(3)] public required Guid SiteId { get; init; }
    [Id(4)] public required string SiteName { get; init; }
    [Id(5)] public required SaleChannel Channel { get; init; }
    [Id(6)] public required Guid ProductId { get; init; }
    [Id(7)] public required string ProductName { get; init; }
    [Id(8)] public required string Category { get; init; }

    // Quantities
    [Id(9)] public required int Quantity { get; init; }
    [Id(10)] public required int TransactionCount { get; init; }

    // Revenue
    [Id(11)] public required decimal GrossSales { get; init; }
    [Id(12)] public required decimal Discounts { get; init; }
    [Id(13)] public required decimal Voids { get; init; }
    [Id(14)] public required decimal Comps { get; init; }
    [Id(15)] public required decimal Tax { get; init; }
    [Id(16)] public required decimal NetSales { get; init; }

    // Cost
    [Id(17)] public required decimal TheoreticalCOGS { get; init; }
    [Id(18)] public decimal? ActualCOGS { get; init; }
    [Id(19)] public decimal? COGSVariance { get; init; }

    // Derived
    [Id(20)] public decimal GrossProfit => NetSales - (ActualCOGS ?? TheoreticalCOGS);
    [Id(21)] public decimal GrossProfitPercent => NetSales > 0 ? GrossProfit / NetSales * 100 : 0;

    // Period tracking
    [Id(22)] public required int WeekNumber { get; init; }
    [Id(23)] public required int PeriodNumber { get; init; } // 4-week period (1-13)
    [Id(24)] public required int FiscalYear { get; init; }
}

// ============================================================================
// Inventory Fact Projection (Daily Snapshot)
// ============================================================================

/// <summary>
/// Inventory fact at the date × location × sku × batch grain.
/// End-of-day snapshot for stock valuation.
/// </summary>
[GenerateSerializer]
public sealed record InventoryFact
{
    [Id(0)] public required Guid FactId { get; init; }
    [Id(1)] public required DateTime Date { get; init; }
    [Id(2)] public required Guid OrgId { get; init; }
    [Id(3)] public required Guid SiteId { get; init; }
    [Id(4)] public required string SiteName { get; init; }
    [Id(5)] public required Guid IngredientId { get; init; }
    [Id(6)] public required string IngredientName { get; init; }
    [Id(7)] public required string Sku { get; init; }
    [Id(8)] public required string Category { get; init; }
    [Id(9)] public Guid? BatchId { get; init; }
    [Id(10)] public string? BatchNumber { get; init; }

    // Quantities
    [Id(11)] public required decimal OnHandQuantity { get; init; }
    [Id(12)] public required decimal ReservedQuantity { get; init; }
    [Id(13)] public required decimal AvailableQuantity { get; init; }
    [Id(14)] public required string Unit { get; init; }

    // Valuation
    [Id(15)] public required decimal UnitCost { get; init; }
    [Id(16)] public required decimal TotalValue { get; init; }
    [Id(17)] public required CostingMethod CostingMethod { get; init; }

    // Batch info
    [Id(18)] public DateTime? ExpiryDate { get; init; }
    [Id(19)] public required FreezeState FreezeState { get; init; }
    [Id(20)] public DateTime? ReceivedDate { get; init; }

    // Stock health metrics
    [Id(21)] public int? DaysOnHand { get; init; }
    [Id(22)] public int? DaysUntilExpiry { get; init; }
    [Id(23)] public required bool IsLowStock { get; init; }
    [Id(24)] public required bool IsOutOfStock { get; init; }
    [Id(25)] public required bool IsExpiringSoon { get; init; } // < 7 days
    [Id(26)] public required bool IsOverPar { get; init; }

    // Par level tracking
    [Id(27)] public required decimal ReorderPoint { get; init; }
    [Id(28)] public required decimal ParLevel { get; init; }
    [Id(29)] public required decimal MaxLevel { get; init; }
}

public enum FreezeState
{
    Fresh,
    Frozen,
    Defrosted
}

// ============================================================================
// Consumption Fact Projection
// ============================================================================

/// <summary>
/// Consumption fact for theoretical vs actual COGS analysis.
/// </summary>
[GenerateSerializer]
public sealed record ConsumptionFact
{
    [Id(0)] public required Guid FactId { get; init; }
    [Id(1)] public required DateTime Date { get; init; }
    [Id(2)] public required Guid OrgId { get; init; }
    [Id(3)] public required Guid SiteId { get; init; }
    [Id(4)] public required Guid IngredientId { get; init; }
    [Id(5)] public required string IngredientName { get; init; }
    [Id(6)] public required string Category { get; init; }
    [Id(7)] public required string Unit { get; init; }

    // Theoretical (from recipes × sales)
    [Id(8)] public required decimal TheoreticalQuantity { get; init; }
    [Id(9)] public required decimal TheoreticalCost { get; init; }

    // Actual (from inventory movements)
    [Id(10)] public required decimal ActualQuantity { get; init; }
    [Id(11)] public required decimal ActualCost { get; init; }

    // Variance
    [Id(12)] public decimal VarianceQuantity => ActualQuantity - TheoreticalQuantity;
    [Id(13)] public decimal VarianceCost => ActualCost - TheoreticalCost;
    [Id(14)] public decimal VariancePercent => TheoreticalCost > 0 ? VarianceCost / TheoreticalCost * 100 : 0;

    // Costing
    [Id(15)] public required CostingMethod CostingMethod { get; init; }
    [Id(16)] public Guid? BatchId { get; init; }

    // Reference
    [Id(17)] public Guid? OrderId { get; init; }
    [Id(18)] public Guid? MenuItemId { get; init; }
    [Id(19)] public Guid? RecipeVersionId { get; init; }
}

// ============================================================================
// Waste Fact Projection
// ============================================================================

/// <summary>
/// Waste fact for tracking waste by reason, location, and product.
/// </summary>
[GenerateSerializer]
public sealed record WasteFact
{
    [Id(0)] public required Guid FactId { get; init; }
    [Id(1)] public required DateTime Date { get; init; }
    [Id(2)] public required Guid OrgId { get; init; }
    [Id(3)] public required Guid SiteId { get; init; }
    [Id(4)] public required string SiteName { get; init; }
    [Id(5)] public required Guid IngredientId { get; init; }
    [Id(6)] public required string IngredientName { get; init; }
    [Id(7)] public required string Sku { get; init; }
    [Id(8)] public required string Category { get; init; }
    [Id(9)] public Guid? BatchId { get; init; }

    // Waste details
    [Id(10)] public required decimal Quantity { get; init; }
    [Id(11)] public required string Unit { get; init; }
    [Id(12)] public required WasteReason Reason { get; init; }
    [Id(13)] public required string ReasonDetails { get; init; }
    [Id(14)] public required decimal CostBasis { get; init; }
    [Id(15)] public string? PhotoUrl { get; init; }

    // Rates (calculated vs purchases or sales)
    [Id(16)] public decimal? WasteRateVsPurchases { get; init; }
    [Id(17)] public decimal? WasteRateVsSales { get; init; }

    // Approval
    [Id(18)] public required Guid RecordedBy { get; init; }
    [Id(19)] public Guid? ApprovedBy { get; init; }
}

// ============================================================================
// Purchase Fact Projection
// ============================================================================

/// <summary>
/// Purchase fact for tracking purchasing patterns and supplier performance.
/// </summary>
[GenerateSerializer]
public sealed record PurchaseFact
{
    [Id(0)] public required Guid FactId { get; init; }
    [Id(1)] public required DateTime Date { get; init; }
    [Id(2)] public required Guid OrgId { get; init; }
    [Id(3)] public required Guid SiteId { get; init; }
    [Id(4)] public required string SiteName { get; init; }
    [Id(5)] public required Guid SupplierId { get; init; }
    [Id(6)] public required string SupplierName { get; init; }
    [Id(7)] public required Guid IngredientId { get; init; }
    [Id(8)] public required string IngredientName { get; init; }
    [Id(9)] public required string Sku { get; init; }
    [Id(10)] public required string Category { get; init; }

    // Quantities
    [Id(11)] public required decimal Quantity { get; init; }
    [Id(12)] public required string Unit { get; init; }
    [Id(13)] public required decimal UnitCost { get; init; }
    [Id(14)] public required decimal TotalCost { get; init; }

    // Delivery tracking
    [Id(15)] public required Guid? DeliveryId { get; init; }
    [Id(16)] public required Guid? PurchaseOrderId { get; init; }
    [Id(17)] public int? LeadTimeDays { get; init; }
    [Id(18)] public required bool OnTime { get; init; }

    // Price tracking
    [Id(19)] public decimal? PreviousUnitCost { get; init; }
    [Id(20)] public decimal? PriceVariance { get; init; }
    [Id(21)] public decimal? PriceVariancePercent { get; init; }
}

// ============================================================================
// Aggregated Metrics (derived from facts)
// ============================================================================

/// <summary>
/// Aggregated sales metrics for a period.
/// </summary>
[GenerateSerializer]
public sealed record SalesMetrics
{
    [Id(0)] public required Guid OrgId { get; init; }
    [Id(1)] public required Guid SiteId { get; init; }
    [Id(2)] public required DateTime PeriodStart { get; init; }
    [Id(3)] public required DateTime PeriodEnd { get; init; }
    [Id(4)] public required string PeriodType { get; init; } // Daily, Weekly, Period, Monthly

    // Revenue
    [Id(5)] public required decimal GrossSales { get; init; }
    [Id(6)] public required decimal Discounts { get; init; }
    [Id(7)] public required decimal Voids { get; init; }
    [Id(8)] public required decimal Comps { get; init; }
    [Id(9)] public required decimal Tax { get; init; }
    [Id(10)] public required decimal NetSales { get; init; }

    // Transactions
    [Id(11)] public required int TransactionCount { get; init; }
    [Id(12)] public decimal AverageTicketValue => TransactionCount > 0 ? NetSales / TransactionCount : 0;

    // Covers
    [Id(13)] public required int CoversServed { get; init; }
    [Id(14)] public decimal RevenuePerCover => CoversServed > 0 ? NetSales / CoversServed : 0;
}

/// <summary>
/// Gross profit metrics with theoretical vs actual COGS.
/// </summary>
[GenerateSerializer]
public sealed record GrossProfitMetrics
{
    [Id(0)] public required Guid OrgId { get; init; }
    [Id(1)] public required Guid SiteId { get; init; }
    [Id(2)] public required DateTime PeriodStart { get; init; }
    [Id(3)] public required DateTime PeriodEnd { get; init; }
    [Id(4)] public required string PeriodType { get; init; }

    // Net Sales
    [Id(5)] public required decimal NetSales { get; init; }

    // Actual COGS (from inventory movements)
    [Id(6)] public required decimal ActualCOGS { get; init; }
    [Id(7)] public decimal ActualCOGSPercent => NetSales > 0 ? ActualCOGS / NetSales * 100 : 0;
    [Id(8)] public decimal ActualGrossProfit => NetSales - ActualCOGS;
    [Id(9)] public decimal ActualGrossProfitPercent => NetSales > 0 ? ActualGrossProfit / NetSales * 100 : 0;

    // Theoretical COGS (from recipes)
    [Id(10)] public required decimal TheoreticalCOGS { get; init; }
    [Id(11)] public decimal TheoreticalCOGSPercent => NetSales > 0 ? TheoreticalCOGS / NetSales * 100 : 0;
    [Id(12)] public decimal TheoreticalGrossProfit => NetSales - TheoreticalCOGS;
    [Id(13)] public decimal TheoreticalGrossProfitPercent => NetSales > 0 ? TheoreticalGrossProfit / NetSales * 100 : 0;

    // Variance
    [Id(14)] public decimal Variance => ActualCOGS - TheoreticalCOGS;
    [Id(15)] public decimal VariancePercent => TheoreticalCOGS > 0 ? Variance / TheoreticalCOGS * 100 : 0;

    // Costing method used
    [Id(16)] public required CostingMethod CostingMethod { get; init; }
}

/// <summary>
/// Variance breakdown by ingredient.
/// </summary>
[GenerateSerializer]
public sealed record VarianceBreakdown
{
    [Id(0)] public required Guid IngredientId { get; init; }
    [Id(1)] public required string IngredientName { get; init; }
    [Id(2)] public required string Category { get; init; }

    [Id(3)] public required decimal TheoreticalUsage { get; init; }
    [Id(4)] public required decimal ActualUsage { get; init; }
    [Id(5)] public decimal UsageVariance => ActualUsage - TheoreticalUsage;

    [Id(6)] public required decimal TheoreticalCost { get; init; }
    [Id(7)] public required decimal ActualCost { get; init; }
    [Id(8)] public decimal CostVariance => ActualCost - TheoreticalCost;

    [Id(9)] public VarianceReason? LikelyReason { get; init; }
}

public enum VarianceReason
{
    OverPour,
    Waste,
    Theft,
    PortionControl,
    RecipeNotFollowed,
    CountError,
    PriceChange,
    Unknown
}

/// <summary>
/// Stock health metrics.
/// </summary>
[GenerateSerializer]
public sealed record StockHealthMetrics
{
    [Id(0)] public required Guid OrgId { get; init; }
    [Id(1)] public required Guid SiteId { get; init; }
    [Id(2)] public required DateTime AsOfDate { get; init; }

    // Valuation
    [Id(3)] public required decimal TotalStockValue { get; init; }
    [Id(4)] public required int TotalSkuCount { get; init; }
    [Id(5)] public required int ActiveBatchCount { get; init; }

    // Turn metrics
    [Id(6)] public required decimal StockTurn { get; init; } // COGS / Avg stock value
    [Id(7)] public required decimal AverageDaysOnHand { get; init; }

    // Health indicators
    [Id(8)] public required int LowStockCount { get; init; }
    [Id(9)] public required int OutOfStockCount { get; init; }
    [Id(10)] public required int ExpiringSoonCount { get; init; } // < 7 days
    [Id(11)] public required decimal ExpiringSoonValue { get; init; }
    [Id(12)] public required int OverParCount { get; init; }
    [Id(13)] public required decimal OverParValue { get; init; }

    // Par compliance
    [Id(14)] public required int ItemsAtPar { get; init; }
    [Id(15)] public required int TotalTrackedItems { get; init; }
    [Id(16)] public decimal ParCompliancePercent => TotalTrackedItems > 0 ? (decimal)ItemsAtPar / TotalTrackedItems * 100 : 0;

    // Aged stock
    [Id(17)] public required decimal AgedStockValue { get; init; } // Batches > 30 days
    [Id(18)] public decimal AgedStockPercent => TotalStockValue > 0 ? AgedStockValue / TotalStockValue * 100 : 0;
}
