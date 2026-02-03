using DarkVelocity.Host.Costing;
using DarkVelocity.Host.Projections;
using DarkVelocity.Host.Events;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Daily Sales Aggregation Grain (per site per day)
// ============================================================================

[GenerateSerializer]
public record DailySalesAggregationCommand(
    [property: Id(0)] DateTime BusinessDate,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string SiteName);

[GenerateSerializer]
public record RecordSaleCommand(
    [property: Id(0)] Guid CheckId,
    [property: Id(1)] SaleChannel Channel,
    [property: Id(2)] Guid ProductId,
    [property: Id(3)] string ProductName,
    [property: Id(4)] string Category,
    [property: Id(5)] int Quantity,
    [property: Id(6)] decimal GrossSales,
    [property: Id(7)] decimal Discounts,
    [property: Id(8)] decimal Voids,
    [property: Id(9)] decimal Comps,
    [property: Id(10)] decimal Tax,
    [property: Id(11)] decimal NetSales,
    [property: Id(12)] decimal TheoreticalCOGS,
    [property: Id(13)] decimal? ActualCOGS,
    [property: Id(14)] int GuestCount);

[GenerateSerializer]
public record DailySalesSnapshot(
    [property: Id(0)] DateTime Date,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string SiteName,
    [property: Id(3)] decimal GrossSales,
    [property: Id(4)] decimal NetSales,
    [property: Id(5)] decimal TheoreticalCOGS,
    [property: Id(6)] decimal ActualCOGS,
    [property: Id(7)] decimal GrossProfit,
    [property: Id(8)] decimal GrossProfitPercent,
    [property: Id(9)] int TransactionCount,
    [property: Id(10)] int GuestCount,
    [property: Id(11)] decimal AverageTicket,
    [property: Id(12)] IReadOnlyDictionary<SaleChannel, decimal> SalesByChannel,
    [property: Id(13)] IReadOnlyDictionary<string, decimal> SalesByCategory);

/// <summary>
/// Simplified sale command for stream-based aggregation.
/// </summary>
[GenerateSerializer]
public record RecordSaleFromStreamCommand(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] decimal GrossSales,
    [property: Id(2)] decimal Discounts,
    [property: Id(3)] decimal Tax,
    [property: Id(4)] int GuestCount,
    [property: Id(5)] int ItemCount,
    [property: Id(6)] string Channel,
    [property: Id(7)] decimal TheoreticalCOGS);

/// <summary>
/// Grain for daily sales aggregation at site level.
/// Key: "{orgId}:{siteId}:sales:{date:yyyy-MM-dd}"
/// </summary>
public interface IDailySalesGrain : IGrainWithStringKey
{
    Task InitializeAsync(DailySalesAggregationCommand command);
    Task RecordSaleAsync(RecordSaleCommand command);
    Task RecordSaleAsync(RecordSaleFromStreamCommand command);
    Task RecordVoidAsync(Guid orderId, decimal voidAmount, string reason);
    Task<DailySalesSnapshot> GetSnapshotAsync();
    Task<SalesMetrics> GetMetricsAsync();
    Task<GrossProfitMetrics> GetGrossProfitMetricsAsync(CostingMethod method);
    Task<IReadOnlyList<SalesFact>> GetFactsAsync();
    Task FinalizeAsync();
}

// ============================================================================
// Inventory Snapshot Grain (per site per day)
// ============================================================================

[GenerateSerializer]
public record InventorySnapshotCommand(
    [property: Id(0)] DateTime BusinessDate,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string SiteName);

[GenerateSerializer]
public record IngredientSnapshot(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] string IngredientName,
    [property: Id(2)] string Sku,
    [property: Id(3)] string Category,
    [property: Id(4)] decimal OnHandQuantity,
    [property: Id(5)] decimal AvailableQuantity,
    [property: Id(6)] string Unit,
    [property: Id(7)] decimal WeightedAverageCost,
    [property: Id(8)] decimal TotalValue,
    [property: Id(9)] DateTime? EarliestExpiry,
    [property: Id(10)] bool IsLowStock,
    [property: Id(11)] bool IsOutOfStock,
    [property: Id(12)] bool IsExpiringSoon,
    [property: Id(13)] bool IsOverPar,
    [property: Id(14)] int ActiveBatchCount);

[GenerateSerializer]
public record DailyInventorySnapshot(
    [property: Id(0)] DateTime Date,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string SiteName,
    [property: Id(3)] decimal TotalStockValue,
    [property: Id(4)] int TotalSkuCount,
    [property: Id(5)] int LowStockCount,
    [property: Id(6)] int OutOfStockCount,
    [property: Id(7)] int ExpiringSoonCount,
    [property: Id(8)] decimal ExpiringSoonValue,
    [property: Id(9)] IReadOnlyList<IngredientSnapshot> Ingredients);

/// <summary>
/// Grain for daily inventory snapshot at site level.
/// Key: "{orgId}:{siteId}:inventory-snapshot:{date:yyyy-MM-dd}"
/// </summary>
public interface IDailyInventorySnapshotGrain : IGrainWithStringKey
{
    Task InitializeAsync(InventorySnapshotCommand command);
    Task RecordIngredientSnapshotAsync(IngredientSnapshot snapshot);
    Task<DailyInventorySnapshot> GetSnapshotAsync();
    Task<StockHealthMetrics> GetHealthMetricsAsync();
    Task<IReadOnlyList<InventoryFact>> GetFactsAsync();
    Task FinalizeAsync();
}

// ============================================================================
// Consumption Tracking Grain (per site per day)
// ============================================================================

[GenerateSerializer]
public record RecordConsumptionCommand(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] string IngredientName,
    [property: Id(2)] string Category,
    [property: Id(3)] string Unit,
    [property: Id(4)] decimal TheoreticalQuantity,
    [property: Id(5)] decimal TheoreticalCost,
    [property: Id(6)] decimal ActualQuantity,
    [property: Id(7)] decimal ActualCost,
    [property: Id(8)] CostingMethod CostingMethod,
    [property: Id(9)] Guid? OrderId,
    [property: Id(10)] Guid? MenuItemId,
    [property: Id(11)] Guid? RecipeVersionId);

[GenerateSerializer]
public record DailyConsumptionSnapshot(
    [property: Id(0)] DateTime Date,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] decimal TotalTheoreticalCost,
    [property: Id(3)] decimal TotalActualCost,
    [property: Id(4)] decimal TotalVariance,
    [property: Id(5)] decimal VariancePercent,
    [property: Id(6)] IReadOnlyList<VarianceBreakdown> TopVariances);

/// <summary>
/// Grain for daily consumption tracking at site level.
/// Key: "{orgId}:{siteId}:consumption:{date:yyyy-MM-dd}"
/// </summary>
public interface IDailyConsumptionGrain : IGrainWithStringKey
{
    Task InitializeAsync(DateTime businessDate, Guid siteId);
    Task RecordConsumptionAsync(RecordConsumptionCommand command);
    Task<DailyConsumptionSnapshot> GetSnapshotAsync();
    Task<IReadOnlyList<ConsumptionFact>> GetFactsAsync();
    Task<IReadOnlyList<VarianceBreakdown>> GetVarianceBreakdownAsync();
}

// ============================================================================
// Waste Tracking Grain (per site per day)
// ============================================================================

[GenerateSerializer]
public record RecordWasteFactCommand(
    [property: Id(0)] Guid WasteId,
    [property: Id(1)] Guid IngredientId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] string Sku,
    [property: Id(4)] string Category,
    [property: Id(5)] Guid? BatchId,
    [property: Id(6)] decimal Quantity,
    [property: Id(7)] string Unit,
    [property: Id(8)] WasteReason Reason,
    [property: Id(9)] string ReasonDetails,
    [property: Id(10)] decimal CostBasis,
    [property: Id(11)] Guid RecordedBy,
    [property: Id(12)] Guid? ApprovedBy,
    [property: Id(13)] string? PhotoUrl);

[GenerateSerializer]
public record DailyWasteSnapshot(
    [property: Id(0)] DateTime Date,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] decimal TotalWasteValue,
    [property: Id(3)] int TotalWasteCount,
    [property: Id(4)] IReadOnlyDictionary<WasteReason, decimal> WasteByReason,
    [property: Id(5)] IReadOnlyDictionary<string, decimal> WasteByCategory);

/// <summary>
/// Grain for daily waste tracking at site level.
/// Key: "{orgId}:{siteId}:waste:{date:yyyy-MM-dd}"
/// </summary>
public interface IDailyWasteGrain : IGrainWithStringKey
{
    Task InitializeAsync(DateTime businessDate, Guid siteId);
    Task RecordWasteAsync(RecordWasteFactCommand command);
    Task<DailyWasteSnapshot> GetSnapshotAsync();
    Task<IReadOnlyList<WasteFact>> GetFactsAsync();
}

// ============================================================================
// Period Aggregation Grain (Weekly/Monthly)
// ============================================================================

public enum PeriodType
{
    Daily,
    Weekly,      // Mon-Sun
    FourWeek,    // 4-week period (1-13)
    Monthly,
    Yearly
}

[GenerateSerializer]
public record PeriodAggregationCommand(
    [property: Id(0)] PeriodType PeriodType,
    [property: Id(1)] DateTime PeriodStart,
    [property: Id(2)] DateTime PeriodEnd,
    [property: Id(3)] int PeriodNumber, // Week 1-52, Period 1-13
    [property: Id(4)] int FiscalYear);

[GenerateSerializer]
public record PeriodSummary(
    [property: Id(0)] PeriodType PeriodType,
    [property: Id(1)] DateTime PeriodStart,
    [property: Id(2)] DateTime PeriodEnd,
    [property: Id(3)] int PeriodNumber,
    [property: Id(4)] SalesMetrics SalesMetrics,
    [property: Id(5)] GrossProfitMetrics FifoGrossProfit,
    [property: Id(6)] GrossProfitMetrics WacGrossProfit,
    [property: Id(7)] StockHealthMetrics StockHealth,
    [property: Id(8)] decimal TotalWasteValue);

/// <summary>
/// Grain for period aggregation (weekly, monthly).
/// Key: "{orgId}:{siteId}:period:{periodType}:{year}:{periodNumber}"
/// </summary>
public interface IPeriodAggregationGrain : IGrainWithStringKey
{
    Task InitializeAsync(PeriodAggregationCommand command);
    Task AggregateFromDailyAsync(DateTime date, DailySalesSnapshot sales, DailyInventorySnapshot inventory, DailyConsumptionSnapshot consumption, DailyWasteSnapshot waste);
    Task<PeriodSummary> GetSummaryAsync();
    Task<SalesMetrics> GetSalesMetricsAsync();
    Task<GrossProfitMetrics> GetGrossProfitMetricsAsync(CostingMethod method);
    Task FinalizeAsync();
}

// ============================================================================
// Site Dashboard Grain
// ============================================================================

[GenerateSerializer]
public record DashboardMetrics(
    // Today
    [property: Id(0)] decimal TodayNetSales,
    [property: Id(1)] decimal TodayNetSalesVsLastWeek,
    [property: Id(2)] decimal TodayNetSalesVsLastYear,
    [property: Id(3)] decimal TodayGrossProfitPercent,
    [property: Id(4)] decimal TodayGrossProfitVsBudget,

    // Week to Date
    [property: Id(5)] decimal WtdNetSales,
    [property: Id(6)] decimal WtdGrossProfitPercent,

    // Period to Date
    [property: Id(7)] decimal PtdNetSales,
    [property: Id(8)] decimal PtdGrossProfitPercent,

    // Alerts
    [property: Id(9)] int LowStockAlertCount,
    [property: Id(10)] int OutOfStockAlertCount,
    [property: Id(11)] int ExpiryRiskCount,
    [property: Id(12)] int HighVarianceCount,
    [property: Id(13)] decimal OutstandingPOValue,

    // Top issues
    [property: Id(14)] IReadOnlyList<VarianceBreakdown> TopVariances,
    [property: Id(15)] IReadOnlyList<IngredientSnapshot> LowStockItems);

/// <summary>
/// Grain for site dashboard (GM view).
/// Key: "{orgId}:{siteId}:dashboard"
/// </summary>
public interface ISiteDashboardGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid orgId, Guid siteId, string siteName);
    Task RefreshAsync();
    Task<DashboardMetrics> GetMetricsAsync();
    Task<DailySalesSnapshot> GetTodaySalesAsync();
    Task<DailyInventorySnapshot> GetCurrentInventoryAsync();
    Task<IReadOnlyList<VarianceBreakdown>> GetTopVariancesAsync(int count);
}
