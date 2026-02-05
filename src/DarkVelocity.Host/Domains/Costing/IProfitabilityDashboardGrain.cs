namespace DarkVelocity.Host.Grains;

// ============================================================================
// Date Range for Queries
// ============================================================================

[GenerateSerializer]
public record DateRange(
    [property: Id(0)] DateTime StartDate,
    [property: Id(1)] DateTime EndDate);

// ============================================================================
// Profitability Dashboard Data Types
// ============================================================================

/// <summary>
/// Cost breakdown by category.
/// </summary>
[GenerateSerializer]
public record CategoryCostBreakdown(
    [property: Id(0)] string Category,
    [property: Id(1)] decimal TotalCost,
    [property: Id(2)] decimal TotalRevenue,
    [property: Id(3)] decimal Contribution,
    [property: Id(4)] decimal CostPercent,
    [property: Id(5)] decimal ContributionMarginPercent,
    [property: Id(6)] int ItemCount,
    [property: Id(7)] int UnitsSold);

/// <summary>
/// Item profitability summary.
/// </summary>
[GenerateSerializer]
public record ItemProfitability(
    [property: Id(0)] Guid ItemId,
    [property: Id(1)] string ItemName,
    [property: Id(2)] string Category,
    [property: Id(3)] decimal SellingPrice,
    [property: Id(4)] decimal TheoreticalCost,
    [property: Id(5)] decimal ActualCost,
    [property: Id(6)] decimal ContributionMargin,
    [property: Id(7)] decimal ContributionMarginPercent,
    [property: Id(8)] decimal Variance,
    [property: Id(9)] decimal VariancePercent,
    [property: Id(10)] int UnitsSold,
    [property: Id(11)] decimal TotalRevenue,
    [property: Id(12)] decimal TotalContribution);

/// <summary>
/// Cost trend data point.
/// </summary>
[GenerateSerializer]
public record CostTrendPoint(
    [property: Id(0)] DateTime Date,
    [property: Id(1)] decimal FoodCostPercent,
    [property: Id(2)] decimal BeverageCostPercent,
    [property: Id(3)] decimal OverallCostPercent,
    [property: Id(4)] decimal TotalCost,
    [property: Id(5)] decimal TotalRevenue);

/// <summary>
/// Theoretical vs actual cost variance.
/// </summary>
[GenerateSerializer]
public record CostVariance(
    [property: Id(0)] Guid ItemId,
    [property: Id(1)] string ItemName,
    [property: Id(2)] string Category,
    [property: Id(3)] decimal TheoreticalCost,
    [property: Id(4)] decimal ActualCost,
    [property: Id(5)] decimal VarianceAmount,
    [property: Id(6)] decimal VariancePercent,
    [property: Id(7)] int UnitsSold,
    [property: Id(8)] decimal TotalVariance);

/// <summary>
/// Complete profitability dashboard.
/// </summary>
[GenerateSerializer]
public record ProfitabilityDashboard(
    [property: Id(0)] Guid OrgId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] DateTime PeriodStart,
    [property: Id(3)] DateTime PeriodEnd,

    // Overall metrics
    [property: Id(4)] decimal TotalRevenue,
    [property: Id(5)] decimal TotalCost,
    [property: Id(6)] decimal GrossProfit,
    [property: Id(7)] decimal GrossProfitMarginPercent,
    [property: Id(8)] decimal FoodCostPercent,
    [property: Id(9)] decimal BeverageCostPercent,
    [property: Id(10)] decimal OverallCostPercent,

    // Breakdowns
    [property: Id(11)] IReadOnlyList<CategoryCostBreakdown> CategoryBreakdown,
    [property: Id(12)] IReadOnlyList<ItemProfitability> TopMarginItems,
    [property: Id(13)] IReadOnlyList<ItemProfitability> BottomMarginItems,

    // Trends
    [property: Id(14)] IReadOnlyList<CostTrendPoint> CostTrends,

    // Variance analysis
    [property: Id(15)] decimal TotalTheoreticalCost,
    [property: Id(16)] decimal TotalActualCost,
    [property: Id(17)] decimal TotalVariance,
    [property: Id(18)] decimal TotalVariancePercent,
    [property: Id(19)] IReadOnlyList<CostVariance> TopVarianceItems);

// ============================================================================
// Commands
// ============================================================================

[GenerateSerializer]
public record InitializeProfitabilityDashboardCommand(
    [property: Id(0)] Guid OrgId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string SiteName);

[GenerateSerializer]
public record RecordItemCostDataCommand(
    [property: Id(0)] Guid ItemId,
    [property: Id(1)] string ItemName,
    [property: Id(2)] string Category,
    [property: Id(3)] decimal SellingPrice,
    [property: Id(4)] decimal TheoreticalCost,
    [property: Id(5)] decimal ActualCost,
    [property: Id(6)] int UnitsSold,
    [property: Id(7)] decimal TotalRevenue,
    [property: Id(8)] DateTime RecordedDate);

[GenerateSerializer]
public record RecordDailyCostSummaryCommand(
    [property: Id(0)] DateTime Date,
    [property: Id(1)] decimal FoodCostPercent,
    [property: Id(2)] decimal BeverageCostPercent,
    [property: Id(3)] decimal TotalCost,
    [property: Id(4)] decimal TotalRevenue);

// ============================================================================
// Profitability Dashboard Grain Interface
// ============================================================================

/// <summary>
/// Grain that aggregates profitability data for a site.
/// Provides food cost percentages, margin analysis, and variance tracking.
/// Key: "{orgId}:{siteId}:profitability"
/// </summary>
public interface IProfitabilityDashboardGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initializes the dashboard grain for a site.
    /// </summary>
    Task InitializeAsync(InitializeProfitabilityDashboardCommand command);

    /// <summary>
    /// Records cost data for a menu item.
    /// </summary>
    Task RecordItemCostDataAsync(RecordItemCostDataCommand command);

    /// <summary>
    /// Records a daily cost summary for trends.
    /// </summary>
    Task RecordDailyCostSummaryAsync(RecordDailyCostSummaryCommand command);

    /// <summary>
    /// Gets the complete profitability dashboard for a date range.
    /// </summary>
    Task<ProfitabilityDashboard> GetDashboardAsync(DateRange range);

    /// <summary>
    /// Gets cost breakdown by category.
    /// </summary>
    Task<IReadOnlyList<CategoryCostBreakdown>> GetCategoryBreakdownAsync();

    /// <summary>
    /// Gets profitability analysis for a specific item.
    /// </summary>
    Task<ItemProfitability?> GetItemProfitabilityAsync(Guid itemId);

    /// <summary>
    /// Gets cost trends over time.
    /// </summary>
    Task<IReadOnlyList<CostTrendPoint>> GetCostTrendsAsync(DateRange range);

    /// <summary>
    /// Gets items with the highest variance between theoretical and actual cost.
    /// </summary>
    Task<IReadOnlyList<CostVariance>> GetTopVarianceItemsAsync(int count = 10);

    /// <summary>
    /// Gets items with the highest contribution margin.
    /// </summary>
    Task<IReadOnlyList<ItemProfitability>> GetTopMarginItemsAsync(int count = 10);

    /// <summary>
    /// Gets items with the lowest contribution margin.
    /// </summary>
    Task<IReadOnlyList<ItemProfitability>> GetBottomMarginItemsAsync(int count = 10);

    /// <summary>
    /// Clears all recorded data (for testing/reset).
    /// </summary>
    Task ClearAsync();
}
