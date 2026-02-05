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
public record InventoryIngredientSnapshot(
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
    [property: Id(9)] IReadOnlyList<InventoryIngredientSnapshot> Ingredients);

/// <summary>
/// Grain for daily inventory snapshot at site level.
/// Key: "{orgId}:{siteId}:inventory-snapshot:{date:yyyy-MM-dd}"
/// </summary>
public interface IDailyInventorySnapshotGrain : IGrainWithStringKey
{
    Task InitializeAsync(InventorySnapshotCommand command);
    Task RecordIngredientSnapshotAsync(InventoryIngredientSnapshot snapshot);
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
    [property: Id(15)] IReadOnlyList<InventoryIngredientSnapshot> LowStockItems);

/// <summary>
/// Grain for site dashboard (GM view).
/// Key: "{orgId}:{siteId}:dashboard"
/// </summary>
public interface ISiteDashboardGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid orgId, Guid siteId, string siteName);
    Task RefreshAsync();
    Task<DashboardMetrics> GetMetricsAsync();
    Task<ExtendedDashboardMetrics> GetExtendedMetricsAsync();
    Task<DailySalesSnapshot> GetTodaySalesAsync();
    Task<DailyInventorySnapshot> GetCurrentInventoryAsync();
    Task<IReadOnlyList<VarianceBreakdown>> GetTopVariancesAsync(int count);
    Task<HourlySalesBreakdown> GetHourlySalesAsync();
    Task<IReadOnlyList<TopSellingItem>> GetTopSellingItemsAsync(int count);
    Task<PaymentMethodBreakdown> GetPaymentBreakdownAsync();
}

// ============================================================================
// Extended Dashboard Types
// ============================================================================

[GenerateSerializer]
public record ExtendedDashboardMetrics(
    // Today's performance
    [property: Id(0)] decimal TodayNetSales,
    [property: Id(1)] decimal YesterdayNetSales,
    [property: Id(2)] decimal LastWeekSameDayNetSales,
    [property: Id(3)] decimal TodayVsYesterdayPercent,
    [property: Id(4)] decimal TodayVsLastWeekPercent,

    // Average ticket and covers
    [property: Id(5)] decimal AverageTicketSize,
    [property: Id(6)] int GuestCount,
    [property: Id(7)] int TransactionCount,
    [property: Id(8)] decimal RevenuePerCover,

    // Gross profit
    [property: Id(9)] decimal TodayGrossProfitPercent,
    [property: Id(10)] decimal TheoreticalCOGS,
    [property: Id(11)] decimal ActualCOGS,

    // Week-to-date
    [property: Id(12)] decimal WtdNetSales,
    [property: Id(13)] decimal WtdGrossProfitPercent,
    [property: Id(14)] int WtdTransactionCount,

    // Period-to-date
    [property: Id(15)] decimal PtdNetSales,
    [property: Id(16)] decimal PtdGrossProfitPercent,

    // Timestamps
    [property: Id(17)] DateTime LastRefreshed,
    [property: Id(18)] DateTime CurrentBusinessDate);

[GenerateSerializer]
public record HourlySalesBreakdown(
    [property: Id(0)] DateTime Date,
    [property: Id(1)] IReadOnlyList<HourlySalesEntry> HourlySales,
    [property: Id(2)] IReadOnlyList<DaypartSalesEntry> DaypartSales);

[GenerateSerializer]
public record HourlySalesEntry(
    [property: Id(0)] int Hour,
    [property: Id(1)] decimal NetSales,
    [property: Id(2)] int TransactionCount,
    [property: Id(3)] int GuestCount,
    [property: Id(4)] decimal AverageTicket);

[GenerateSerializer]
public record DaypartSalesEntry(
    [property: Id(0)] DayPart Daypart,
    [property: Id(1)] decimal NetSales,
    [property: Id(2)] decimal PercentOfTotal,
    [property: Id(3)] int TransactionCount,
    [property: Id(4)] int GuestCount,
    [property: Id(5)] decimal AverageTicket);

[GenerateSerializer]
public record TopSellingItem(
    [property: Id(0)] Guid ProductId,
    [property: Id(1)] string ProductName,
    [property: Id(2)] string Category,
    [property: Id(3)] int QuantitySold,
    [property: Id(4)] decimal NetSales,
    [property: Id(5)] decimal GrossProfit,
    [property: Id(6)] decimal GrossProfitPercent);

[GenerateSerializer]
public record PaymentMethodBreakdown(
    [property: Id(0)] DateTime Date,
    [property: Id(1)] IReadOnlyList<PaymentMethodEntry> Payments,
    [property: Id(2)] decimal TotalCollected);

[GenerateSerializer]
public record PaymentMethodEntry(
    [property: Id(0)] string PaymentMethod,
    [property: Id(1)] decimal Amount,
    [property: Id(2)] decimal PercentOfTotal,
    [property: Id(3)] int TransactionCount);

// ============================================================================
// Daypart Analysis Grain
// ============================================================================

[GenerateSerializer]
public record DaypartDefinition(
    [property: Id(0)] DayPart Daypart,
    [property: Id(1)] TimeSpan StartTime,
    [property: Id(2)] TimeSpan EndTime,
    [property: Id(3)] string DisplayName);

[GenerateSerializer]
public record DaypartAnalysisSnapshot(
    [property: Id(0)] DateTime BusinessDate,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] IReadOnlyList<DaypartPerformance> DaypartPerformances,
    [property: Id(3)] IReadOnlyList<HourlyPerformance> HourlyPerformances,
    [property: Id(4)] DayPart PeakDaypart,
    [property: Id(5)] int PeakHour);

[GenerateSerializer]
public record DaypartPerformance(
    [property: Id(0)] DayPart Daypart,
    [property: Id(1)] decimal NetSales,
    [property: Id(2)] decimal PercentOfDailySales,
    [property: Id(3)] int TransactionCount,
    [property: Id(4)] int GuestCount,
    [property: Id(5)] decimal AverageTicket,
    [property: Id(6)] decimal LaborCost,
    [property: Id(7)] decimal SalesPerLaborHour,
    [property: Id(8)] decimal ComparisonVsLastWeek);

[GenerateSerializer]
public record HourlyPerformance(
    [property: Id(0)] int Hour,
    [property: Id(1)] decimal NetSales,
    [property: Id(2)] int TransactionCount,
    [property: Id(3)] int GuestCount,
    [property: Id(4)] decimal LaborHours,
    [property: Id(5)] decimal SalesPerLaborHour);

[GenerateSerializer]
public record RecordHourlySaleCommand(
    [property: Id(0)] int Hour,
    [property: Id(1)] decimal NetSales,
    [property: Id(2)] int TransactionCount,
    [property: Id(3)] int GuestCount,
    [property: Id(4)] decimal TheoreticalCOGS);

[GenerateSerializer]
public record RecordHourlyLaborCommand(
    [property: Id(0)] int Hour,
    [property: Id(1)] decimal LaborHours,
    [property: Id(2)] decimal LaborCost);

/// <summary>
/// Grain for daypart analysis at site level.
/// Key: "{orgId}:{siteId}:daypart:{date:yyyy-MM-dd}"
/// </summary>
public interface IDaypartAnalysisGrain : IGrainWithStringKey
{
    Task InitializeAsync(DateTime businessDate, Guid siteId);
    Task RecordHourlySaleAsync(RecordHourlySaleCommand command);
    Task RecordHourlyLaborAsync(RecordHourlyLaborCommand command);
    Task<DaypartAnalysisSnapshot> GetSnapshotAsync();
    Task<DaypartPerformance> GetDaypartPerformanceAsync(DayPart daypart);
    Task<IReadOnlyList<HourlyPerformance>> GetHourlyPerformanceAsync();
    Task SetDaypartDefinitionsAsync(IReadOnlyList<DaypartDefinition> definitions);
    Task FinalizeAsync();
}

// ============================================================================
// Labor Report Grain
// ============================================================================

[GenerateSerializer]
public record LaborReportSnapshot(
    [property: Id(0)] DateTime PeriodStart,
    [property: Id(1)] DateTime PeriodEnd,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] decimal TotalLaborCost,
    [property: Id(4)] decimal TotalSales,
    [property: Id(5)] decimal LaborCostPercent,
    [property: Id(6)] decimal TotalLaborHours,
    [property: Id(7)] decimal SalesPerLaborHour,
    [property: Id(8)] decimal ScheduledHours,
    [property: Id(9)] decimal ActualHours,
    [property: Id(10)] decimal ScheduleVarianceHours,
    [property: Id(11)] decimal ScheduleVariancePercent,
    [property: Id(12)] decimal OvertimeHours,
    [property: Id(13)] decimal OvertimeCost,
    [property: Id(14)] decimal OvertimePercent,
    [property: Id(15)] IReadOnlyList<DepartmentLaborMetrics> ByDepartment,
    [property: Id(16)] IReadOnlyList<DaypartLaborMetrics> ByDaypart);

[GenerateSerializer]
public record DepartmentLaborMetrics(
    [property: Id(0)] Department Department,
    [property: Id(1)] decimal LaborCost,
    [property: Id(2)] decimal LaborHours,
    [property: Id(3)] decimal OvertimeHours,
    [property: Id(4)] decimal LaborCostPercent,
    [property: Id(5)] int EmployeeCount);

[GenerateSerializer]
public record DaypartLaborMetrics(
    [property: Id(0)] DayPart Daypart,
    [property: Id(1)] decimal LaborCost,
    [property: Id(2)] decimal LaborHours,
    [property: Id(3)] decimal Sales,
    [property: Id(4)] decimal SalesPerLaborHour,
    [property: Id(5)] decimal LaborCostPercent);

[GenerateSerializer]
public record RecordLaborEntryCommand(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] Department Department,
    [property: Id(2)] decimal RegularHours,
    [property: Id(3)] decimal OvertimeHours,
    [property: Id(4)] decimal RegularRate,
    [property: Id(5)] decimal OvertimeRate,
    [property: Id(6)] DayPart? Daypart);

/// <summary>
/// Grain for labor reporting at site level.
/// Key: "{orgId}:{siteId}:labor:{periodStart:yyyy-MM-dd}"
/// </summary>
public interface ILaborReportGrain : IGrainWithStringKey
{
    Task InitializeAsync(DateTime periodStart, DateTime periodEnd, Guid siteId);
    Task RecordLaborEntryAsync(RecordLaborEntryCommand command);
    Task RecordScheduledHoursAsync(decimal scheduledHours, decimal scheduledCost);
    Task RecordSalesAsync(decimal sales, decimal salesByDaypart);
    Task<LaborReportSnapshot> GetSnapshotAsync();
    Task<decimal> GetLaborCostPercentAsync();
    Task<decimal> GetSalesPerLaborHourAsync();
    Task<IReadOnlyList<OvertimeAlert>> GetOvertimeAlertsAsync();
    Task FinalizeAsync();
}

[GenerateSerializer]
public record OvertimeAlert(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] string EmployeeName,
    [property: Id(2)] decimal OvertimeHours,
    [property: Id(3)] decimal OvertimeCost,
    [property: Id(4)] decimal WeeklyHoursTotal);

// ============================================================================
// Product Mix Grain
// ============================================================================

[GenerateSerializer]
public record ProductMixSnapshot(
    [property: Id(0)] DateTime PeriodStart,
    [property: Id(1)] DateTime PeriodEnd,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] IReadOnlyList<ProductPerformance> Products,
    [property: Id(4)] IReadOnlyList<CategoryPerformance> Categories,
    [property: Id(5)] IReadOnlyList<ModifierPerformance> Modifiers,
    [property: Id(6)] VoidCompAnalysis VoidCompAnalysis);

[GenerateSerializer]
public record ProductPerformance(
    [property: Id(0)] Guid ProductId,
    [property: Id(1)] string ProductName,
    [property: Id(2)] string Category,
    [property: Id(3)] int QuantitySold,
    [property: Id(4)] decimal NetSales,
    [property: Id(5)] decimal PercentOfSales,
    [property: Id(6)] decimal GrossProfit,
    [property: Id(7)] decimal GrossProfitPercent,
    [property: Id(8)] decimal SalesVelocity, // Units per hour
    [property: Id(9)] int RankByQuantity,
    [property: Id(10)] int RankByRevenue,
    [property: Id(11)] int RankByProfit);

[GenerateSerializer]
public record CategoryPerformance(
    [property: Id(0)] string Category,
    [property: Id(1)] int ItemCount,
    [property: Id(2)] int QuantitySold,
    [property: Id(3)] decimal NetSales,
    [property: Id(4)] decimal PercentOfSales,
    [property: Id(5)] decimal GrossProfit,
    [property: Id(6)] decimal GrossProfitPercent,
    [property: Id(7)] decimal AverageItemPrice);

[GenerateSerializer]
public record ModifierPerformance(
    [property: Id(0)] Guid ModifierId,
    [property: Id(1)] string ModifierName,
    [property: Id(2)] int TimesApplied,
    [property: Id(3)] decimal TotalRevenue,
    [property: Id(4)] decimal AveragePerApplication,
    [property: Id(5)] decimal AttachmentRate); // % of applicable items with this modifier

[GenerateSerializer]
public record VoidCompAnalysis(
    [property: Id(0)] int TotalVoids,
    [property: Id(1)] decimal TotalVoidAmount,
    [property: Id(2)] decimal VoidPercent,
    [property: Id(3)] int TotalComps,
    [property: Id(4)] decimal TotalCompAmount,
    [property: Id(5)] decimal CompPercent,
    [property: Id(6)] IReadOnlyList<VoidReasonBreakdown> VoidsByReason,
    [property: Id(7)] IReadOnlyList<CompReasonBreakdown> CompsByReason);

[GenerateSerializer]
public record VoidReasonBreakdown(
    [property: Id(0)] string Reason,
    [property: Id(1)] int Count,
    [property: Id(2)] decimal Amount);

[GenerateSerializer]
public record CompReasonBreakdown(
    [property: Id(0)] string Reason,
    [property: Id(1)] int Count,
    [property: Id(2)] decimal Amount);

[GenerateSerializer]
public record RecordProductSaleCommand(
    [property: Id(0)] Guid ProductId,
    [property: Id(1)] string ProductName,
    [property: Id(2)] string Category,
    [property: Id(3)] int Quantity,
    [property: Id(4)] decimal NetSales,
    [property: Id(5)] decimal COGS,
    [property: Id(6)] IReadOnlyList<ModifierSale> Modifiers);

[GenerateSerializer]
public record ModifierSale(
    [property: Id(0)] Guid ModifierId,
    [property: Id(1)] string ModifierName,
    [property: Id(2)] decimal Price);

[GenerateSerializer]
public record RecordVoidCommand(
    [property: Id(0)] Guid ProductId,
    [property: Id(1)] string Reason,
    [property: Id(2)] decimal Amount);

[GenerateSerializer]
public record RecordCompCommand(
    [property: Id(0)] Guid ProductId,
    [property: Id(1)] string Reason,
    [property: Id(2)] decimal Amount);

/// <summary>
/// Grain for product mix analysis at site level.
/// Key: "{orgId}:{siteId}:productmix:{date:yyyy-MM-dd}"
/// </summary>
public interface IProductMixGrain : IGrainWithStringKey
{
    Task InitializeAsync(DateTime businessDate, Guid siteId);
    Task RecordProductSaleAsync(RecordProductSaleCommand command);
    Task RecordVoidAsync(RecordVoidCommand command);
    Task RecordCompAsync(RecordCompCommand command);
    Task SetOperatingHoursAsync(decimal operatingHours);
    Task<ProductMixSnapshot> GetSnapshotAsync();
    Task<IReadOnlyList<ProductPerformance>> GetTopProductsAsync(int count, string sortBy);
    Task<IReadOnlyList<CategoryPerformance>> GetCategoryPerformanceAsync();
    Task<VoidCompAnalysis> GetVoidCompAnalysisAsync();
    Task FinalizeAsync();
}

// ============================================================================
// Payment Reconciliation Grain
// ============================================================================

public enum PaymentReconciliationStatus
{
    Pending,
    Matched,
    Discrepancy,
    Resolved,
    Exception
}

[GenerateSerializer]
public record PaymentReconciliationSnapshot(
    [property: Id(0)] DateTime BusinessDate,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] PaymentReconciliationStatus Status,
    [property: Id(3)] decimal PosTotalCash,
    [property: Id(4)] decimal PosTotalCard,
    [property: Id(5)] decimal PosTotalOther,
    [property: Id(6)] decimal PosGrandTotal,
    [property: Id(7)] decimal ProcessorTotalSettled,
    [property: Id(8)] decimal ProcessorFees,
    [property: Id(9)] decimal ProcessorNetSettlement,
    [property: Id(10)] decimal CashExpected,
    [property: Id(11)] decimal CashActual,
    [property: Id(12)] decimal CashVariance,
    [property: Id(13)] decimal CardVariance,
    [property: Id(14)] IReadOnlyList<ProcessorSettlement> ProcessorSettlements,
    [property: Id(15)] IReadOnlyList<ReconciliationException> Exceptions,
    [property: Id(16)] DateTime? ReconciledAt,
    [property: Id(17)] Guid? ReconciledBy);

[GenerateSerializer]
public record ProcessorSettlement(
    [property: Id(0)] string ProcessorName,
    [property: Id(1)] string BatchId,
    [property: Id(2)] decimal GrossAmount,
    [property: Id(3)] decimal Fees,
    [property: Id(4)] decimal NetAmount,
    [property: Id(5)] int TransactionCount,
    [property: Id(6)] DateTime SettlementDate,
    [property: Id(7)] PaymentReconciliationStatus Status);

[GenerateSerializer]
public record ReconciliationException(
    [property: Id(0)] Guid ExceptionId,
    [property: Id(1)] string ExceptionType,
    [property: Id(2)] string Description,
    [property: Id(3)] decimal Amount,
    [property: Id(4)] string? TransactionReference,
    [property: Id(5)] PaymentReconciliationStatus Status,
    [property: Id(6)] string? Resolution,
    [property: Id(7)] DateTime? ResolvedAt,
    [property: Id(8)] Guid? ResolvedBy);

[GenerateSerializer]
public record RecordPosPaymentCommand(
    [property: Id(0)] string PaymentMethod,
    [property: Id(1)] decimal Amount,
    [property: Id(2)] string? ProcessorName,
    [property: Id(3)] string? TransactionId);

[GenerateSerializer]
public record RecordProcessorSettlementCommand(
    [property: Id(0)] string ProcessorName,
    [property: Id(1)] string BatchId,
    [property: Id(2)] decimal GrossAmount,
    [property: Id(3)] decimal Fees,
    [property: Id(4)] decimal NetAmount,
    [property: Id(5)] int TransactionCount,
    [property: Id(6)] DateTime SettlementDate);

[GenerateSerializer]
public record RecordCashCountCommand(
    [property: Id(0)] decimal CashCounted,
    [property: Id(1)] Guid CountedBy);

[GenerateSerializer]
public record ResolveExceptionCommand(
    [property: Id(0)] Guid ExceptionId,
    [property: Id(1)] string Resolution,
    [property: Id(2)] Guid ResolvedBy);

/// <summary>
/// Grain for payment reconciliation at site level.
/// Key: "{orgId}:{siteId}:reconciliation:{date:yyyy-MM-dd}"
/// </summary>
public interface IPaymentReconciliationGrain : IGrainWithStringKey
{
    Task InitializeAsync(DateTime businessDate, Guid siteId);
    Task RecordPosPaymentAsync(RecordPosPaymentCommand command);
    Task RecordProcessorSettlementAsync(RecordProcessorSettlementCommand command);
    Task RecordCashCountAsync(RecordCashCountCommand command);
    Task<PaymentReconciliationSnapshot> GetSnapshotAsync();
    Task ReconcileAsync();
    Task ResolveExceptionAsync(ResolveExceptionCommand command);
    Task<IReadOnlyList<ReconciliationException>> GetExceptionsAsync();
    Task<decimal> GetTotalVarianceAsync();
    Task FinalizeAsync(Guid reconciledBy);
}
