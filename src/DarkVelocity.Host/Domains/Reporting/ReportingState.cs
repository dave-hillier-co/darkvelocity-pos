using DarkVelocity.Host.Costing;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Projections;
using DarkVelocity.Host.Events;

namespace DarkVelocity.Host.State;

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
// Extended Dashboard State
// ============================================================================

[GenerateSerializer]
public sealed class ExtendedDashboardState
{
    // Hourly sales tracking
    [Id(0)] public Dictionary<int, HourlySalesData> HourlySales { get; set; } = [];

    // Top selling items
    [Id(1)] public Dictionary<Guid, TopItemData> TopItems { get; set; } = [];

    // Payment method breakdown
    [Id(2)] public Dictionary<string, PaymentMethodData> PaymentMethods { get; set; } = [];
}

[GenerateSerializer]
public sealed class HourlySalesData
{
    [Id(0)] public int Hour { get; set; }
    [Id(1)] public decimal NetSales { get; set; }
    [Id(2)] public int TransactionCount { get; set; }
    [Id(3)] public int GuestCount { get; set; }
}

[GenerateSerializer]
public sealed class TopItemData
{
    [Id(0)] public Guid ProductId { get; set; }
    [Id(1)] public string ProductName { get; set; } = string.Empty;
    [Id(2)] public string Category { get; set; } = string.Empty;
    [Id(3)] public int QuantitySold { get; set; }
    [Id(4)] public decimal NetSales { get; set; }
    [Id(5)] public decimal COGS { get; set; }
}

[GenerateSerializer]
public sealed class PaymentMethodData
{
    [Id(0)] public string PaymentMethod { get; set; } = string.Empty;
    [Id(1)] public decimal Amount { get; set; }
    [Id(2)] public int TransactionCount { get; set; }
}

// ============================================================================
// Daypart Analysis State
// ============================================================================

[GenerateSerializer]
public sealed class DaypartAnalysisState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public DateTime BusinessDate { get; set; }
    [Id(3)] public bool IsFinalized { get; set; }

    // Hourly data
    [Id(4)] public Dictionary<int, HourlyData> HourlyData { get; set; } = [];

    // Daypart definitions (customizable)
    [Id(5)] public List<DaypartDefinitionState> DaypartDefinitions { get; set; } = GetDefaultDaypartDefinitions();

    [Id(6)] public int Version { get; set; }

    private static List<DaypartDefinitionState> GetDefaultDaypartDefinitions() =>
    [
        new() { Daypart = Events.DayPart.Breakfast, StartTime = TimeSpan.FromHours(6), EndTime = TimeSpan.FromHours(11), DisplayName = "Breakfast" },
        new() { Daypart = Events.DayPart.Lunch, StartTime = TimeSpan.FromHours(11), EndTime = TimeSpan.FromHours(15), DisplayName = "Lunch" },
        new() { Daypart = Events.DayPart.Afternoon, StartTime = TimeSpan.FromHours(15), EndTime = TimeSpan.FromHours(17), DisplayName = "Afternoon" },
        new() { Daypart = Events.DayPart.Dinner, StartTime = TimeSpan.FromHours(17), EndTime = TimeSpan.FromHours(22), DisplayName = "Dinner" },
        new() { Daypart = Events.DayPart.LateNight, StartTime = TimeSpan.FromHours(22), EndTime = TimeSpan.FromHours(6), DisplayName = "Late Night" }
    ];
}

[GenerateSerializer]
public sealed class HourlyData
{
    [Id(0)] public int Hour { get; set; }
    [Id(1)] public decimal NetSales { get; set; }
    [Id(2)] public int TransactionCount { get; set; }
    [Id(3)] public int GuestCount { get; set; }
    [Id(4)] public decimal TheoreticalCOGS { get; set; }
    [Id(5)] public decimal LaborHours { get; set; }
    [Id(6)] public decimal LaborCost { get; set; }
}

[GenerateSerializer]
public sealed class DaypartDefinitionState
{
    [Id(0)] public Events.DayPart Daypart { get; set; }
    [Id(1)] public TimeSpan StartTime { get; set; }
    [Id(2)] public TimeSpan EndTime { get; set; }
    [Id(3)] public string DisplayName { get; set; } = string.Empty;
}

// ============================================================================
// Labor Report State
// ============================================================================

[GenerateSerializer]
public sealed class LaborReportState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public DateTime PeriodStart { get; set; }
    [Id(3)] public DateTime PeriodEnd { get; set; }
    [Id(4)] public bool IsFinalized { get; set; }

    // Scheduled vs Actual
    [Id(5)] public decimal ScheduledHours { get; set; }
    [Id(6)] public decimal ScheduledCost { get; set; }

    // Sales data
    [Id(7)] public decimal TotalSales { get; set; }

    // Labor entries by employee
    [Id(8)] public Dictionary<Guid, LaborEntryData> LaborEntries { get; set; } = [];

    // Department aggregations
    [Id(9)] public Dictionary<Grains.Department, DepartmentLaborData> ByDepartment { get; set; } = [];

    // Daypart aggregations
    [Id(10)] public Dictionary<Events.DayPart, DaypartLaborData> ByDaypart { get; set; } = [];

    [Id(11)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class LaborEntryData
{
    [Id(0)] public Guid EmployeeId { get; set; }
    [Id(1)] public string EmployeeName { get; set; } = string.Empty;
    [Id(2)] public Grains.Department Department { get; set; }
    [Id(3)] public decimal RegularHours { get; set; }
    [Id(4)] public decimal OvertimeHours { get; set; }
    [Id(5)] public decimal RegularCost { get; set; }
    [Id(6)] public decimal OvertimeCost { get; set; }
}

[GenerateSerializer]
public sealed class DepartmentLaborData
{
    [Id(0)] public Grains.Department Department { get; set; }
    [Id(1)] public decimal LaborCost { get; set; }
    [Id(2)] public decimal LaborHours { get; set; }
    [Id(3)] public decimal OvertimeHours { get; set; }
    [Id(4)] public HashSet<Guid> Employees { get; set; } = [];
}

[GenerateSerializer]
public sealed class DaypartLaborData
{
    [Id(0)] public Events.DayPart Daypart { get; set; }
    [Id(1)] public decimal LaborCost { get; set; }
    [Id(2)] public decimal LaborHours { get; set; }
    [Id(3)] public decimal Sales { get; set; }
}

// ============================================================================
// Product Mix State
// ============================================================================

[GenerateSerializer]
public sealed class ProductMixState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public DateTime BusinessDate { get; set; }
    [Id(3)] public bool IsFinalized { get; set; }

    // Operating hours for velocity calculation
    [Id(4)] public decimal OperatingHours { get; set; }
    [Id(5)] public decimal TotalSales { get; set; }

    // Product data
    [Id(6)] public Dictionary<Guid, ProductSalesData> Products { get; set; } = [];

    // Modifier data
    [Id(7)] public Dictionary<Guid, ModifierSalesData> Modifiers { get; set; } = [];

    // Void/Comp tracking
    [Id(8)] public List<VoidEntry> Voids { get; set; } = [];
    [Id(9)] public List<CompEntry> Comps { get; set; } = [];

    [Id(10)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class ProductSalesData
{
    [Id(0)] public Guid ProductId { get; set; }
    [Id(1)] public string ProductName { get; set; } = string.Empty;
    [Id(2)] public string Category { get; set; } = string.Empty;
    [Id(3)] public int QuantitySold { get; set; }
    [Id(4)] public decimal NetSales { get; set; }
    [Id(5)] public decimal COGS { get; set; }
}

[GenerateSerializer]
public sealed class ModifierSalesData
{
    [Id(0)] public Guid ModifierId { get; set; }
    [Id(1)] public string ModifierName { get; set; } = string.Empty;
    [Id(2)] public int TimesApplied { get; set; }
    [Id(3)] public decimal TotalRevenue { get; set; }
    [Id(4)] public int ApplicableItemCount { get; set; }
}

[GenerateSerializer]
public sealed class VoidEntry
{
    [Id(0)] public Guid ProductId { get; set; }
    [Id(1)] public string Reason { get; set; } = string.Empty;
    [Id(2)] public decimal Amount { get; set; }
}

[GenerateSerializer]
public sealed class CompEntry
{
    [Id(0)] public Guid ProductId { get; set; }
    [Id(1)] public string Reason { get; set; } = string.Empty;
    [Id(2)] public decimal Amount { get; set; }
}

// ============================================================================
// Payment Reconciliation State
// ============================================================================

[GenerateSerializer]
public sealed class PaymentReconciliationState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public DateTime BusinessDate { get; set; }
    [Id(3)] public Grains.PaymentReconciliationStatus Status { get; set; }

    // POS totals by payment method
    [Id(4)] public Dictionary<string, PosPaymentData> PosPayments { get; set; } = [];

    // Processor settlements
    [Id(5)] public List<ProcessorSettlementData> ProcessorSettlements { get; set; } = [];

    // Cash management
    [Id(6)] public decimal CashExpected { get; set; }
    [Id(7)] public decimal CashActual { get; set; }
    [Id(8)] public Guid? CashCountedBy { get; set; }

    // Exceptions
    [Id(9)] public List<ReconciliationExceptionData> Exceptions { get; set; } = [];

    // Finalization
    [Id(10)] public DateTime? ReconciledAt { get; set; }
    [Id(11)] public Guid? ReconciledBy { get; set; }

    [Id(12)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class PosPaymentData
{
    [Id(0)] public string PaymentMethod { get; set; } = string.Empty;
    [Id(1)] public decimal Amount { get; set; }
    [Id(2)] public int TransactionCount { get; set; }
    [Id(3)] public string? ProcessorName { get; set; }
}

[GenerateSerializer]
public sealed class ProcessorSettlementData
{
    [Id(0)] public string ProcessorName { get; set; } = string.Empty;
    [Id(1)] public string BatchId { get; set; } = string.Empty;
    [Id(2)] public decimal GrossAmount { get; set; }
    [Id(3)] public decimal Fees { get; set; }
    [Id(4)] public decimal NetAmount { get; set; }
    [Id(5)] public int TransactionCount { get; set; }
    [Id(6)] public DateTime SettlementDate { get; set; }
    [Id(7)] public Grains.PaymentReconciliationStatus Status { get; set; }
}

[GenerateSerializer]
public sealed class ReconciliationExceptionData
{
    [Id(0)] public Guid ExceptionId { get; set; }
    [Id(1)] public string ExceptionType { get; set; } = string.Empty;
    [Id(2)] public string Description { get; set; } = string.Empty;
    [Id(3)] public decimal Amount { get; set; }
    [Id(4)] public string? TransactionReference { get; set; }
    [Id(5)] public Grains.PaymentReconciliationStatus Status { get; set; }
    [Id(6)] public string? Resolution { get; set; }
    [Id(7)] public DateTime? ResolvedAt { get; set; }
    [Id(8)] public Guid? ResolvedBy { get; set; }
}

// ============================================================================
// Enums
// ============================================================================

// Note: PeriodType enum is defined in Grains/IReportingGrain.cs
