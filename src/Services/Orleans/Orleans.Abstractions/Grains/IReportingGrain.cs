using DarkVelocity.Orleans.Abstractions.Costing;
using DarkVelocity.Orleans.Abstractions.Projections;
using DarkVelocity.Shared.Contracts.Events;

namespace DarkVelocity.Orleans.Abstractions.Grains;

// ============================================================================
// Daily Sales Aggregation Grain (per site per day)
// ============================================================================

public record DailySalesAggregationCommand(
    DateTime BusinessDate,
    Guid SiteId,
    string SiteName);

public record RecordSaleCommand(
    Guid CheckId,
    SaleChannel Channel,
    Guid ProductId,
    string ProductName,
    string Category,
    int Quantity,
    decimal GrossSales,
    decimal Discounts,
    decimal Voids,
    decimal Comps,
    decimal Tax,
    decimal NetSales,
    decimal TheoreticalCOGS,
    decimal? ActualCOGS,
    int GuestCount);

public record DailySalesSnapshot(
    DateTime Date,
    Guid SiteId,
    string SiteName,
    decimal GrossSales,
    decimal NetSales,
    decimal TheoreticalCOGS,
    decimal ActualCOGS,
    decimal GrossProfit,
    decimal GrossProfitPercent,
    int TransactionCount,
    int GuestCount,
    decimal AverageTicket,
    IReadOnlyDictionary<SaleChannel, decimal> SalesByChannel,
    IReadOnlyDictionary<string, decimal> SalesByCategory);

/// <summary>
/// Grain for daily sales aggregation at site level.
/// Key: "{orgId}:{siteId}:sales:{date:yyyy-MM-dd}"
/// </summary>
public interface IDailySalesGrain : IGrainWithStringKey
{
    Task InitializeAsync(DailySalesAggregationCommand command);
    Task RecordSaleAsync(RecordSaleCommand command);
    Task<DailySalesSnapshot> GetSnapshotAsync();
    Task<SalesMetrics> GetMetricsAsync();
    Task<GrossProfitMetrics> GetGrossProfitMetricsAsync(CostingMethod method);
    Task<IReadOnlyList<SalesFact>> GetFactsAsync();
    Task FinalizeAsync();
}

// ============================================================================
// Inventory Snapshot Grain (per site per day)
// ============================================================================

public record InventorySnapshotCommand(
    DateTime BusinessDate,
    Guid SiteId,
    string SiteName);

public record IngredientSnapshot(
    Guid IngredientId,
    string IngredientName,
    string Sku,
    string Category,
    decimal OnHandQuantity,
    decimal AvailableQuantity,
    string Unit,
    decimal WeightedAverageCost,
    decimal TotalValue,
    DateTime? EarliestExpiry,
    bool IsLowStock,
    bool IsOutOfStock,
    bool IsExpiringSoon,
    bool IsOverPar,
    int ActiveBatchCount);

public record DailyInventorySnapshot(
    DateTime Date,
    Guid SiteId,
    string SiteName,
    decimal TotalStockValue,
    int TotalSkuCount,
    int LowStockCount,
    int OutOfStockCount,
    int ExpiringSoonCount,
    decimal ExpiringSoonValue,
    IReadOnlyList<IngredientSnapshot> Ingredients);

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

public record RecordConsumptionCommand(
    Guid IngredientId,
    string IngredientName,
    string Category,
    string Unit,
    decimal TheoreticalQuantity,
    decimal TheoreticalCost,
    decimal ActualQuantity,
    decimal ActualCost,
    CostingMethod CostingMethod,
    Guid? OrderId,
    Guid? MenuItemId,
    Guid? RecipeVersionId);

public record DailyConsumptionSnapshot(
    DateTime Date,
    Guid SiteId,
    decimal TotalTheoreticalCost,
    decimal TotalActualCost,
    decimal TotalVariance,
    decimal VariancePercent,
    IReadOnlyList<VarianceBreakdown> TopVariances);

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

public record RecordWasteFactCommand(
    Guid WasteId,
    Guid IngredientId,
    string IngredientName,
    string Sku,
    string Category,
    Guid? BatchId,
    decimal Quantity,
    string Unit,
    WasteReason Reason,
    string ReasonDetails,
    decimal CostBasis,
    Guid RecordedBy,
    Guid? ApprovedBy,
    string? PhotoUrl);

public record DailyWasteSnapshot(
    DateTime Date,
    Guid SiteId,
    decimal TotalWasteValue,
    int TotalWasteCount,
    IReadOnlyDictionary<WasteReason, decimal> WasteByReason,
    IReadOnlyDictionary<string, decimal> WasteByCategory);

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

public record PeriodAggregationCommand(
    PeriodType PeriodType,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    int PeriodNumber, // Week 1-52, Period 1-13
    int FiscalYear);

public record PeriodSummary(
    PeriodType PeriodType,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    int PeriodNumber,
    SalesMetrics SalesMetrics,
    GrossProfitMetrics FifoGrossProfit,
    GrossProfitMetrics WacGrossProfit,
    StockHealthMetrics StockHealth,
    decimal TotalWasteValue);

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

public record DashboardMetrics(
    // Today
    decimal TodayNetSales,
    decimal TodayNetSalesVsLastWeek,
    decimal TodayNetSalesVsLastYear,
    decimal TodayGrossProfitPercent,
    decimal TodayGrossProfitVsBudget,

    // Week to Date
    decimal WtdNetSales,
    decimal WtdGrossProfitPercent,

    // Period to Date
    decimal PtdNetSales,
    decimal PtdGrossProfitPercent,

    // Alerts
    int LowStockAlertCount,
    int OutOfStockAlertCount,
    int ExpiryRiskCount,
    int HighVarianceCount,
    decimal OutstandingPOValue,

    // Top issues
    IReadOnlyList<VarianceBreakdown> TopVariances,
    IReadOnlyList<IngredientSnapshot> LowStockItems);

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
