using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using Orleans.TestingHost;
using Xunit;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for advanced reporting grains: Daypart Analysis, Labor Report, Product Mix, Payment Reconciliation.
/// </summary>
[Collection(ClusterCollection.Name)]
public class AdvancedReportingGrainTests
{
    private readonly TestCluster _cluster;

    public AdvancedReportingGrainTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    // ============================================================================
    // Daypart Analysis Grain Tests
    // ============================================================================

    [Fact]
    public async Task DaypartAnalysisGrain_RecordHourlySale_AggregatesCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var key = GrainKeys.DaypartAnalysis(orgId, siteId, date);
        var grain = _cluster.GrainFactory.GetGrain<IDaypartAnalysisGrain>(key);

        await grain.InitializeAsync(DateTime.Today, siteId);

        // Act - Record sales for multiple hours
        await grain.RecordHourlySaleAsync(new RecordHourlySaleCommand(
            Hour: 11,
            NetSales: 500.00m,
            TransactionCount: 20,
            GuestCount: 35,
            TheoreticalCOGS: 150.00m));

        await grain.RecordHourlySaleAsync(new RecordHourlySaleCommand(
            Hour: 12,
            NetSales: 800.00m,
            TransactionCount: 30,
            GuestCount: 55,
            TheoreticalCOGS: 240.00m));

        await grain.RecordHourlySaleAsync(new RecordHourlySaleCommand(
            Hour: 18,
            NetSales: 1200.00m,
            TransactionCount: 40,
            GuestCount: 80,
            TheoreticalCOGS: 360.00m));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();

        Assert.Equal(3, snapshot.HourlyPerformances.Count);
        Assert.Equal(12, snapshot.PeakHour); // Lunch hour should have highest sales among lunch hours
        Assert.Equal(DayPart.Dinner, snapshot.PeakDaypart); // Dinner should be peak daypart

        // Check hourly data
        var hour12 = snapshot.HourlyPerformances.First(h => h.Hour == 12);
        Assert.Equal(800.00m, hour12.NetSales);
        Assert.Equal(30, hour12.TransactionCount);
        Assert.Equal(55, hour12.GuestCount);
    }

    [Fact]
    public async Task DaypartAnalysisGrain_RecordHourlyLabor_CalculatesSPLH()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var key = GrainKeys.DaypartAnalysis(orgId, siteId, date);
        var grain = _cluster.GrainFactory.GetGrain<IDaypartAnalysisGrain>(key);

        await grain.InitializeAsync(DateTime.Today, siteId);

        // Act - Record sales and labor
        await grain.RecordHourlySaleAsync(new RecordHourlySaleCommand(
            Hour: 12,
            NetSales: 1000.00m,
            TransactionCount: 40,
            GuestCount: 60,
            TheoreticalCOGS: 300.00m));

        await grain.RecordHourlyLaborAsync(new RecordHourlyLaborCommand(
            Hour: 12,
            LaborHours: 8.0m,
            LaborCost: 120.00m));

        // Assert
        var hourlyPerformance = await grain.GetHourlyPerformanceAsync();
        var hour12 = hourlyPerformance.First(h => h.Hour == 12);

        Assert.Equal(1000.00m, hour12.NetSales);
        Assert.Equal(8.0m, hour12.LaborHours);
        Assert.Equal(125.00m, hour12.SalesPerLaborHour); // 1000 / 8 = 125
    }

    [Fact]
    public async Task DaypartAnalysisGrain_GetDaypartPerformance_CorrectlyGroupsHours()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var key = GrainKeys.DaypartAnalysis(orgId, siteId, date);
        var grain = _cluster.GrainFactory.GetGrain<IDaypartAnalysisGrain>(key);

        await grain.InitializeAsync(DateTime.Today, siteId);

        // Act - Record sales for lunch hours (11-15)
        await grain.RecordHourlySaleAsync(new RecordHourlySaleCommand(Hour: 11, NetSales: 300.00m, TransactionCount: 10, GuestCount: 15, TheoreticalCOGS: 90.00m));
        await grain.RecordHourlySaleAsync(new RecordHourlySaleCommand(Hour: 12, NetSales: 500.00m, TransactionCount: 20, GuestCount: 30, TheoreticalCOGS: 150.00m));
        await grain.RecordHourlySaleAsync(new RecordHourlySaleCommand(Hour: 13, NetSales: 400.00m, TransactionCount: 15, GuestCount: 25, TheoreticalCOGS: 120.00m));

        // Assert
        var lunchPerformance = await grain.GetDaypartPerformanceAsync(DayPart.Lunch);

        Assert.Equal(1200.00m, lunchPerformance.NetSales); // 300 + 500 + 400
        Assert.Equal(45, lunchPerformance.TransactionCount); // 10 + 20 + 15
        Assert.Equal(70, lunchPerformance.GuestCount); // 15 + 30 + 25
    }

    // ============================================================================
    // Labor Report Grain Tests
    // ============================================================================

    [Fact]
    public async Task LaborReportGrain_RecordLaborEntry_CalculatesLaborCostPercent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.Today);
        var key = GrainKeys.LaborReport(orgId, siteId, periodStart);
        var grain = _cluster.GrainFactory.GetGrain<ILaborReportGrain>(key);

        await grain.InitializeAsync(DateTime.Today, DateTime.Today.AddDays(6), siteId);

        // Act - Record labor entries
        await grain.RecordLaborEntryAsync(new RecordLaborEntryCommand(
            EmployeeId: Guid.NewGuid(),
            Department: Department.FrontOfHouse,
            RegularHours: 40.0m,
            OvertimeHours: 5.0m,
            RegularRate: 15.00m,
            OvertimeRate: 22.50m,
            Daypart: DayPart.Lunch));

        await grain.RecordLaborEntryAsync(new RecordLaborEntryCommand(
            EmployeeId: Guid.NewGuid(),
            Department: Department.BackOfHouse,
            RegularHours: 35.0m,
            OvertimeHours: 0.0m,
            RegularRate: 18.00m,
            OvertimeRate: 27.00m,
            Daypart: DayPart.Dinner));

        await grain.RecordSalesAsync(5000.00m, 0);

        // Assert
        var laborCostPercent = await grain.GetLaborCostPercentAsync();
        var snapshot = await grain.GetSnapshotAsync();

        // Labor cost = (40 * 15) + (5 * 22.50) + (35 * 18) = 600 + 112.50 + 630 = 1342.50
        // Labor cost % = 1342.50 / 5000 * 100 = 26.85%
        Assert.Equal(1342.50m, snapshot.TotalLaborCost);
        Assert.Equal(26.85m, Math.Round(laborCostPercent, 2));
    }

    [Fact]
    public async Task LaborReportGrain_TrackOvertimeHours_IdentifiesOvertimeAlerts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.Today);
        var key = GrainKeys.LaborReport(orgId, siteId, periodStart);
        var grain = _cluster.GrainFactory.GetGrain<ILaborReportGrain>(key);

        await grain.InitializeAsync(DateTime.Today, DateTime.Today.AddDays(6), siteId);

        // Act - Record employees with overtime
        var emp1 = Guid.NewGuid();
        var emp2 = Guid.NewGuid();

        await grain.RecordLaborEntryAsync(new RecordLaborEntryCommand(
            EmployeeId: emp1,
            Department: Department.FrontOfHouse,
            RegularHours: 40.0m,
            OvertimeHours: 10.0m,
            RegularRate: 15.00m,
            OvertimeRate: 22.50m,
            Daypart: null));

        await grain.RecordLaborEntryAsync(new RecordLaborEntryCommand(
            EmployeeId: emp2,
            Department: Department.BackOfHouse,
            RegularHours: 40.0m,
            OvertimeHours: 0.0m,
            RegularRate: 18.00m,
            OvertimeRate: 27.00m,
            Daypart: null));

        // Assert
        var overtimeAlerts = await grain.GetOvertimeAlertsAsync();

        Assert.Single(overtimeAlerts);
        Assert.Equal(emp1, overtimeAlerts[0].EmployeeId);
        Assert.Equal(10.0m, overtimeAlerts[0].OvertimeHours);
        Assert.Equal(225.00m, overtimeAlerts[0].OvertimeCost); // 10 * 22.50
        Assert.Equal(50.0m, overtimeAlerts[0].WeeklyHoursTotal); // 40 + 10
    }

    [Fact]
    public async Task LaborReportGrain_CalculatesSalesPerLaborHour()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.Today);
        var key = GrainKeys.LaborReport(orgId, siteId, periodStart);
        var grain = _cluster.GrainFactory.GetGrain<ILaborReportGrain>(key);

        await grain.InitializeAsync(DateTime.Today, DateTime.Today.AddDays(6), siteId);

        // Act
        await grain.RecordLaborEntryAsync(new RecordLaborEntryCommand(
            EmployeeId: Guid.NewGuid(),
            Department: Department.FrontOfHouse,
            RegularHours: 80.0m,
            OvertimeHours: 0.0m,
            RegularRate: 15.00m,
            OvertimeRate: 22.50m,
            Daypart: null));

        await grain.RecordSalesAsync(4000.00m, 0);

        // Assert
        var splh = await grain.GetSalesPerLaborHourAsync();

        Assert.Equal(50.00m, splh); // 4000 / 80 = 50
    }

    // ============================================================================
    // Product Mix Grain Tests
    // ============================================================================

    [Fact]
    public async Task ProductMixGrain_RecordProductSale_AggregatesCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var key = GrainKeys.ProductMix(orgId, siteId, date);
        var grain = _cluster.GrainFactory.GetGrain<IProductMixGrain>(key);

        await grain.InitializeAsync(DateTime.Today, siteId);

        var burgerId = Guid.NewGuid();
        var friesId = Guid.NewGuid();

        // Act - Record product sales
        await grain.RecordProductSaleAsync(new RecordProductSaleCommand(
            ProductId: burgerId,
            ProductName: "Classic Burger",
            Category: "Entrees",
            Quantity: 50,
            NetSales: 500.00m,
            COGS: 150.00m,
            Modifiers: []));

        await grain.RecordProductSaleAsync(new RecordProductSaleCommand(
            ProductId: friesId,
            ProductName: "French Fries",
            Category: "Sides",
            Quantity: 75,
            NetSales: 225.00m,
            COGS: 45.00m,
            Modifiers: []));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();

        Assert.Equal(2, snapshot.Products.Count);
        Assert.Equal(725.00m, snapshot.Products.Sum(p => p.NetSales)); // 500 + 225

        var burger = snapshot.Products.First(p => p.ProductId == burgerId);
        Assert.Equal(50, burger.QuantitySold);
        Assert.Equal(500.00m, burger.NetSales);
        Assert.Equal(350.00m, burger.GrossProfit); // 500 - 150
        Assert.Equal(70.00m, burger.GrossProfitPercent); // 350 / 500 * 100
    }

    [Fact]
    public async Task ProductMixGrain_RecordModifiers_TracksModifierPerformance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var key = GrainKeys.ProductMix(orgId, siteId, date);
        var grain = _cluster.GrainFactory.GetGrain<IProductMixGrain>(key);

        await grain.InitializeAsync(DateTime.Today, siteId);

        var burgerId = Guid.NewGuid();
        var cheeseModId = Guid.NewGuid();
        var baconModId = Guid.NewGuid();

        // Act - Record product sales with modifiers
        await grain.RecordProductSaleAsync(new RecordProductSaleCommand(
            ProductId: burgerId,
            ProductName: "Classic Burger",
            Category: "Entrees",
            Quantity: 1,
            NetSales: 12.00m,
            COGS: 4.00m,
            Modifiers: [
                new ModifierSale(cheeseModId, "Add Cheese", 1.50m),
                new ModifierSale(baconModId, "Add Bacon", 2.00m)
            ]));

        await grain.RecordProductSaleAsync(new RecordProductSaleCommand(
            ProductId: burgerId,
            ProductName: "Classic Burger",
            Category: "Entrees",
            Quantity: 1,
            NetSales: 12.00m,
            COGS: 4.00m,
            Modifiers: [
                new ModifierSale(cheeseModId, "Add Cheese", 1.50m)
            ]));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();

        Assert.Equal(2, snapshot.Modifiers.Count);

        var cheeseModifier = snapshot.Modifiers.First(m => m.ModifierId == cheeseModId);
        Assert.Equal(2, cheeseModifier.TimesApplied);
        Assert.Equal(3.00m, cheeseModifier.TotalRevenue); // 1.50 * 2

        var baconModifier = snapshot.Modifiers.First(m => m.ModifierId == baconModId);
        Assert.Equal(1, baconModifier.TimesApplied);
        Assert.Equal(2.00m, baconModifier.TotalRevenue);
    }

    [Fact]
    public async Task ProductMixGrain_RecordVoidsAndComps_TracksVoidCompAnalysis()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var key = GrainKeys.ProductMix(orgId, siteId, date);
        var grain = _cluster.GrainFactory.GetGrain<IProductMixGrain>(key);

        await grain.InitializeAsync(DateTime.Today, siteId);

        var productId = Guid.NewGuid();

        // Act - Record sales, voids, and comps
        await grain.RecordProductSaleAsync(new RecordProductSaleCommand(
            ProductId: productId,
            ProductName: "Test Item",
            Category: "Test",
            Quantity: 100,
            NetSales: 1000.00m,
            COGS: 300.00m,
            Modifiers: []));

        await grain.RecordVoidAsync(new RecordVoidCommand(
            ProductId: productId,
            Reason: "Customer Request",
            Amount: 50.00m));

        await grain.RecordVoidAsync(new RecordVoidCommand(
            ProductId: productId,
            Reason: "Kitchen Error",
            Amount: 30.00m));

        await grain.RecordCompAsync(new RecordCompCommand(
            ProductId: productId,
            Reason: "Service Recovery",
            Amount: 25.00m));

        // Assert
        var analysis = await grain.GetVoidCompAnalysisAsync();

        Assert.Equal(2, analysis.TotalVoids);
        Assert.Equal(80.00m, analysis.TotalVoidAmount); // 50 + 30
        Assert.Equal(1, analysis.TotalComps);
        Assert.Equal(25.00m, analysis.TotalCompAmount);

        // Gross sales = Net + Voids + Comps = 1000 + 80 + 25 = 1105
        // Void % = 80 / 1105 * 100 = ~7.24%
        Assert.True(analysis.VoidPercent > 7 && analysis.VoidPercent < 8);

        Assert.Equal(2, analysis.VoidsByReason.Count);
        var customerVoids = analysis.VoidsByReason.First(v => v.Reason == "Customer Request");
        Assert.Equal(50.00m, customerVoids.Amount);
    }

    [Fact]
    public async Task ProductMixGrain_GetTopProducts_ReturnsSortedCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var key = GrainKeys.ProductMix(orgId, siteId, date);
        var grain = _cluster.GrainFactory.GetGrain<IProductMixGrain>(key);

        await grain.InitializeAsync(DateTime.Today, siteId);

        // Act - Record products with different metrics
        await grain.RecordProductSaleAsync(new RecordProductSaleCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "High Revenue",
            Category: "Test",
            Quantity: 10,
            NetSales: 500.00m, // Highest revenue
            COGS: 400.00m,
            Modifiers: []));

        await grain.RecordProductSaleAsync(new RecordProductSaleCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "High Quantity",
            Category: "Test",
            Quantity: 100, // Highest quantity
            NetSales: 200.00m,
            COGS: 50.00m,
            Modifiers: []));

        await grain.RecordProductSaleAsync(new RecordProductSaleCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "High Profit",
            Category: "Test",
            Quantity: 20,
            NetSales: 300.00m,
            COGS: 50.00m, // Highest profit (250)
            Modifiers: []));

        // Assert
        var topByRevenue = await grain.GetTopProductsAsync(2, "revenue");
        Assert.Equal("High Revenue", topByRevenue[0].ProductName);
        Assert.Equal("High Profit", topByRevenue[1].ProductName);

        var topByQuantity = await grain.GetTopProductsAsync(2, "quantity");
        Assert.Equal("High Quantity", topByQuantity[0].ProductName);

        var topByProfit = await grain.GetTopProductsAsync(2, "profit");
        Assert.Equal("High Profit", topByProfit[0].ProductName);
    }

    // ============================================================================
    // Payment Reconciliation Grain Tests
    // ============================================================================

    [Fact]
    public async Task PaymentReconciliationGrain_RecordPayments_AggregatesCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var key = GrainKeys.PaymentReconciliation(orgId, siteId, date);
        var grain = _cluster.GrainFactory.GetGrain<IPaymentReconciliationGrain>(key);

        await grain.InitializeAsync(DateTime.Today, siteId);

        // Act - Record payments
        await grain.RecordPosPaymentAsync(new RecordPosPaymentCommand(
            PaymentMethod: "Cash",
            Amount: 500.00m,
            ProcessorName: null,
            TransactionId: null));

        await grain.RecordPosPaymentAsync(new RecordPosPaymentCommand(
            PaymentMethod: "Credit Card",
            Amount: 1500.00m,
            ProcessorName: "Stripe",
            TransactionId: "ch_123"));

        await grain.RecordPosPaymentAsync(new RecordPosPaymentCommand(
            PaymentMethod: "Debit Card",
            Amount: 300.00m,
            ProcessorName: "Stripe",
            TransactionId: "ch_456"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();

        Assert.Equal(500.00m, snapshot.PosTotalCash);
        Assert.Equal(1800.00m, snapshot.PosTotalCard); // 1500 + 300
        Assert.Equal(2300.00m, snapshot.PosGrandTotal);
    }

    [Fact]
    public async Task PaymentReconciliationGrain_ReconcilePayments_DetectsDiscrepancies()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var key = GrainKeys.PaymentReconciliation(orgId, siteId, date);
        var grain = _cluster.GrainFactory.GetGrain<IPaymentReconciliationGrain>(key);

        await grain.InitializeAsync(DateTime.Today, siteId);

        // Record POS payments
        await grain.RecordPosPaymentAsync(new RecordPosPaymentCommand(
            PaymentMethod: "Cash",
            Amount: 500.00m,
            ProcessorName: null,
            TransactionId: null));

        await grain.RecordPosPaymentAsync(new RecordPosPaymentCommand(
            PaymentMethod: "Credit Card",
            Amount: 1500.00m,
            ProcessorName: "Stripe",
            TransactionId: "ch_123"));

        // Record cash count (short by $10)
        await grain.RecordCashCountAsync(new RecordCashCountCommand(
            CashCounted: 490.00m,
            CountedBy: Guid.NewGuid()));

        // Record processor settlement (matches card total)
        await grain.RecordProcessorSettlementAsync(new RecordProcessorSettlementCommand(
            ProcessorName: "Stripe",
            BatchId: "batch_123",
            GrossAmount: 1500.00m,
            Fees: 45.00m,
            NetAmount: 1455.00m,
            TransactionCount: 1,
            SettlementDate: DateTime.Today));

        // Act
        await grain.ReconcileAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();

        Assert.Equal(-10.00m, snapshot.CashVariance); // Cash short
        Assert.Equal(0.00m, snapshot.CardVariance); // Card matches

        // Should have one exception for cash variance
        Assert.Single(snapshot.Exceptions);
        Assert.Equal("CashVariance", snapshot.Exceptions[0].ExceptionType);
        Assert.Equal(PaymentReconciliationStatus.Discrepancy, snapshot.Status);
    }

    [Fact]
    public async Task PaymentReconciliationGrain_ResolveException_UpdatesStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var key = GrainKeys.PaymentReconciliation(orgId, siteId, date);
        var grain = _cluster.GrainFactory.GetGrain<IPaymentReconciliationGrain>(key);

        await grain.InitializeAsync(DateTime.Today, siteId);

        await grain.RecordPosPaymentAsync(new RecordPosPaymentCommand(
            PaymentMethod: "Cash",
            Amount: 500.00m,
            ProcessorName: null,
            TransactionId: null));

        await grain.RecordCashCountAsync(new RecordCashCountCommand(
            CashCounted: 480.00m,
            CountedBy: Guid.NewGuid()));

        await grain.ReconcileAsync();

        var exceptions = await grain.GetExceptionsAsync();
        var exceptionId = exceptions[0].ExceptionId;

        // Act
        await grain.ResolveExceptionAsync(new ResolveExceptionCommand(
            ExceptionId: exceptionId,
            Resolution: "Verified with camera footage - cashier error",
            ResolvedBy: Guid.NewGuid()));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();

        Assert.Equal(PaymentReconciliationStatus.Matched, snapshot.Status);
        Assert.Equal(PaymentReconciliationStatus.Resolved, snapshot.Exceptions[0].Status);
        Assert.Equal("Verified with camera footage - cashier error", snapshot.Exceptions[0].Resolution);
    }

    [Fact]
    public async Task PaymentReconciliationGrain_Finalize_SetsReconciliationData()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var key = GrainKeys.PaymentReconciliation(orgId, siteId, date);
        var grain = _cluster.GrainFactory.GetGrain<IPaymentReconciliationGrain>(key);

        await grain.InitializeAsync(DateTime.Today, siteId);

        await grain.RecordPosPaymentAsync(new RecordPosPaymentCommand(
            PaymentMethod: "Cash",
            Amount: 500.00m,
            ProcessorName: null,
            TransactionId: null));

        await grain.RecordCashCountAsync(new RecordCashCountCommand(
            CashCounted: 500.00m,
            CountedBy: Guid.NewGuid()));

        var managerId = Guid.NewGuid();

        // Act
        await grain.FinalizeAsync(managerId);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();

        Assert.NotNull(snapshot.ReconciledAt);
        Assert.Equal(managerId, snapshot.ReconciledBy);
    }

    [Fact]
    public async Task PaymentReconciliationGrain_GetTotalVariance_CalculatesCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var key = GrainKeys.PaymentReconciliation(orgId, siteId, date);
        var grain = _cluster.GrainFactory.GetGrain<IPaymentReconciliationGrain>(key);

        await grain.InitializeAsync(DateTime.Today, siteId);

        // Record POS payments
        await grain.RecordPosPaymentAsync(new RecordPosPaymentCommand(
            PaymentMethod: "Cash",
            Amount: 500.00m,
            ProcessorName: null,
            TransactionId: null));

        await grain.RecordPosPaymentAsync(new RecordPosPaymentCommand(
            PaymentMethod: "Credit Card",
            Amount: 1000.00m,
            ProcessorName: "Stripe",
            TransactionId: "ch_123"));

        // Cash over by $5
        await grain.RecordCashCountAsync(new RecordCashCountCommand(
            CashCounted: 505.00m,
            CountedBy: Guid.NewGuid()));

        // Processor settled $10 less
        await grain.RecordProcessorSettlementAsync(new RecordProcessorSettlementCommand(
            ProcessorName: "Stripe",
            BatchId: "batch_123",
            GrossAmount: 990.00m,
            Fees: 30.00m,
            NetAmount: 960.00m,
            TransactionCount: 1,
            SettlementDate: DateTime.Today));

        // Act
        var totalVariance = await grain.GetTotalVarianceAsync();

        // Assert
        // Cash variance: 505 - 500 = +5
        // Card variance: 1000 - 990 = +10
        // Total: +15
        Assert.Equal(15.00m, totalVariance);
    }

    // ============================================================================
    // Category Performance Tests
    // ============================================================================

    [Fact]
    public async Task ProductMixGrain_GetCategoryPerformance_GroupsCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var key = GrainKeys.ProductMix(orgId, siteId, date);
        var grain = _cluster.GrainFactory.GetGrain<IProductMixGrain>(key);

        await grain.InitializeAsync(DateTime.Today, siteId);

        // Act - Record products in different categories
        await grain.RecordProductSaleAsync(new RecordProductSaleCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Burger",
            Category: "Entrees",
            Quantity: 50,
            NetSales: 500.00m,
            COGS: 150.00m,
            Modifiers: []));

        await grain.RecordProductSaleAsync(new RecordProductSaleCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Steak",
            Category: "Entrees",
            Quantity: 25,
            NetSales: 625.00m,
            COGS: 250.00m,
            Modifiers: []));

        await grain.RecordProductSaleAsync(new RecordProductSaleCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Fries",
            Category: "Sides",
            Quantity: 100,
            NetSales: 300.00m,
            COGS: 60.00m,
            Modifiers: []));

        // Assert
        var categories = await grain.GetCategoryPerformanceAsync();

        Assert.Equal(2, categories.Count);

        var entrees = categories.First(c => c.Category == "Entrees");
        Assert.Equal(2, entrees.ItemCount);
        Assert.Equal(75, entrees.QuantitySold); // 50 + 25
        Assert.Equal(1125.00m, entrees.NetSales); // 500 + 625
        Assert.Equal(725.00m, entrees.GrossProfit); // 1125 - 400

        var sides = categories.First(c => c.Category == "Sides");
        Assert.Equal(1, sides.ItemCount);
        Assert.Equal(100, sides.QuantitySold);
        Assert.Equal(300.00m, sides.NetSales);
    }
}
