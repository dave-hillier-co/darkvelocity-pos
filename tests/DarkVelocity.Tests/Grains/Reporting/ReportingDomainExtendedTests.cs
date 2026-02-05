using DarkVelocity.Host;
using DarkVelocity.Host.Costing;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Projections;
using FluentAssertions;

namespace DarkVelocity.Tests.Grains.Reporting;

/// <summary>
/// Extended tests for the Reporting domain covering:
/// - Cross-grain data aggregation
/// - Extended metrics calculation
/// - Period rollup accuracy
/// - Dashboard real-time updates
/// - Snapshot timing
/// - Consumption vs sales reconciliation
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ReportingDomainExtendedTests
{
    private readonly TestClusterFixture _fixture;

    public ReportingDomainExtendedTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // ============================================================================
    // Cross-Grain Data Aggregation Tests
    // ============================================================================

    [Fact]
    public async Task SiteDashboard_GetTodaySales_AggregatesFromDailySalesGrain()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        // Initialize the daily sales grain first
        var salesKey = $"{orgId}:{siteId}:sales:{today:yyyy-MM-dd}";
        var salesGrain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(salesKey);
        await salesGrain.InitializeAsync(new DailySalesAggregationCommand(today, siteId, "Test Site"));

        // Record multiple sales transactions
        await salesGrain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.DineIn,
            ProductId: Guid.NewGuid(),
            ProductName: "Burger",
            Category: "Mains",
            Quantity: 2,
            GrossSales: 30.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 2.40m,
            NetSales: 27.60m,
            TheoreticalCOGS: 8.00m,
            ActualCOGS: 8.50m,
            GuestCount: 2));

        await salesGrain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.TakeOut,
            ProductId: Guid.NewGuid(),
            ProductName: "Pizza",
            Category: "Mains",
            Quantity: 1,
            GrossSales: 20.00m,
            Discounts: 2.00m,
            Voids: 0,
            Comps: 0,
            Tax: 1.44m,
            NetSales: 16.56m,
            TheoreticalCOGS: 5.00m,
            ActualCOGS: 5.25m,
            GuestCount: 1));

        // Initialize the dashboard grain
        var dashboardKey = $"{orgId}:{siteId}:dashboard";
        var dashboardGrain = _fixture.Cluster.GrainFactory.GetGrain<ISiteDashboardGrain>(dashboardKey);
        await dashboardGrain.InitializeAsync(orgId, siteId, "Test Site");

        // Act
        var todaySales = await dashboardGrain.GetTodaySalesAsync();

        // Assert
        todaySales.GrossSales.Should().Be(50.00m);
        todaySales.NetSales.Should().Be(44.16m);
        todaySales.TransactionCount.Should().Be(2);
        todaySales.GuestCount.Should().Be(3);
        todaySales.SalesByChannel.Should().ContainKey(SaleChannel.DineIn);
        todaySales.SalesByChannel.Should().ContainKey(SaleChannel.TakeOut);
    }

    [Fact]
    public async Task SiteDashboard_GetCurrentInventory_AggregatesFromDailyInventorySnapshotGrain()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        // Initialize the daily inventory snapshot grain
        var inventoryKey = $"{orgId}:{siteId}:inventory-snapshot:{today:yyyy-MM-dd}";
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IDailyInventorySnapshotGrain>(inventoryKey);
        await inventoryGrain.InitializeAsync(new InventorySnapshotCommand(today, siteId, "Test Site"));

        // Record multiple ingredient snapshots
        await inventoryGrain.RecordIngredientSnapshotAsync(new InventoryIngredientSnapshot(
            Guid.NewGuid(), "Flour", "FLR-001", "Dry Goods", 100m, 100m, "kg",
            2.00m, 200.00m, null, false, false, false, false, 3));

        await inventoryGrain.RecordIngredientSnapshotAsync(new InventoryIngredientSnapshot(
            Guid.NewGuid(), "Sugar", "SUG-001", "Dry Goods", 50m, 50m, "kg",
            1.50m, 75.00m, null, false, false, false, false, 2));

        await inventoryGrain.RecordIngredientSnapshotAsync(new InventoryIngredientSnapshot(
            Guid.NewGuid(), "Milk", "MLK-001", "Dairy", 20m, 20m, "L",
            1.00m, 20.00m, DateTime.UtcNow.AddDays(3), true, false, true, false, 1));

        // Initialize the dashboard grain
        var dashboardKey = $"{orgId}:{siteId}:dashboard";
        var dashboardGrain = _fixture.Cluster.GrainFactory.GetGrain<ISiteDashboardGrain>(dashboardKey);
        await dashboardGrain.InitializeAsync(orgId, siteId, "Test Site");

        // Act
        var inventory = await dashboardGrain.GetCurrentInventoryAsync();

        // Assert
        inventory.TotalStockValue.Should().Be(295.00m);
        inventory.TotalSkuCount.Should().Be(3);
        inventory.LowStockCount.Should().Be(1);
        inventory.ExpiringSoonCount.Should().Be(1);
        inventory.Ingredients.Should().HaveCount(3);
    }

    [Fact]
    public async Task SiteDashboard_GetTopVariances_AggregatesFromDailyConsumptionGrain()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        // Initialize the daily consumption grain
        var consumptionKey = $"{orgId}:{siteId}:consumption:{today:yyyy-MM-dd}";
        var consumptionGrain = _fixture.Cluster.GrainFactory.GetGrain<IDailyConsumptionGrain>(consumptionKey);
        await consumptionGrain.InitializeAsync(today, siteId);

        // Record consumption with varying variances
        await consumptionGrain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "High Variance Ingredient", "Proteins", "kg",
            10.0m, 100.00m, 15.0m, 150.00m, CostingMethod.FIFO,
            null, null, null)); // 50% variance

        await consumptionGrain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "Low Variance Ingredient", "Produce", "kg",
            20.0m, 40.00m, 21.0m, 42.00m, CostingMethod.FIFO,
            null, null, null)); // 5% variance

        await consumptionGrain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "Medium Variance Ingredient", "Dairy", "L",
            15.0m, 30.00m, 18.0m, 36.00m, CostingMethod.FIFO,
            null, null, null)); // 20% variance

        // Initialize the dashboard grain
        var dashboardKey = $"{orgId}:{siteId}:dashboard";
        var dashboardGrain = _fixture.Cluster.GrainFactory.GetGrain<ISiteDashboardGrain>(dashboardKey);
        await dashboardGrain.InitializeAsync(orgId, siteId, "Test Site");

        // Act
        var topVariances = await dashboardGrain.GetTopVariancesAsync(3);

        // Assert
        topVariances.Should().HaveCount(3);
        // Should be ordered by absolute variance descending
        topVariances[0].IngredientName.Should().Be("High Variance Ingredient");
        topVariances[0].CostVariance.Should().Be(50.00m);
    }

    // ============================================================================
    // Extended Metrics Calculation Tests
    // ============================================================================

    [Fact]
    public async Task SiteDashboard_GetExtendedMetrics_CalculatesTodayMetricsCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        // Initialize today's sales grain
        var todaySalesKey = $"{orgId}:{siteId}:sales:{today:yyyy-MM-dd}";
        var todaySalesGrain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(todaySalesKey);
        await todaySalesGrain.InitializeAsync(new DailySalesAggregationCommand(today, siteId, "Test Site"));

        // Record today's sales
        await todaySalesGrain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.DineIn,
            ProductId: Guid.NewGuid(),
            ProductName: "Steak",
            Category: "Mains",
            Quantity: 1,
            GrossSales: 50.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 4.00m,
            NetSales: 46.00m,
            TheoreticalCOGS: 15.00m,
            ActualCOGS: 16.00m,
            GuestCount: 2));

        // Initialize the dashboard grain
        var dashboardKey = $"{orgId}:{siteId}:dashboard";
        var dashboardGrain = _fixture.Cluster.GrainFactory.GetGrain<ISiteDashboardGrain>(dashboardKey);
        await dashboardGrain.InitializeAsync(orgId, siteId, "Test Site");

        // Act
        var extendedMetrics = await dashboardGrain.GetExtendedMetricsAsync();

        // Assert
        extendedMetrics.TodayNetSales.Should().Be(46.00m);
        extendedMetrics.TransactionCount.Should().Be(1);
        extendedMetrics.GuestCount.Should().Be(2);
        extendedMetrics.AverageTicketSize.Should().Be(46.00m); // 46 / 1
        extendedMetrics.RevenuePerCover.Should().Be(23.00m); // 46 / 2
        extendedMetrics.TheoreticalCOGS.Should().Be(15.00m);
        extendedMetrics.ActualCOGS.Should().Be(16.00m);

        // Gross profit percent: (46 - 16) / 46 * 100 = 65.22%
        extendedMetrics.TodayGrossProfitPercent.Should().BeApproximately(65.22m, 0.01m);
    }

    [Fact]
    public async Task SiteDashboard_GetExtendedMetrics_CalculatesComparisonPercentages()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        // Initialize yesterday's sales grain
        var yesterdaySalesKey = $"{orgId}:{siteId}:sales:{yesterday:yyyy-MM-dd}";
        var yesterdaySalesGrain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(yesterdaySalesKey);
        await yesterdaySalesGrain.InitializeAsync(new DailySalesAggregationCommand(yesterday, siteId, "Test Site"));

        await yesterdaySalesGrain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.DineIn,
            ProductId: Guid.NewGuid(),
            ProductName: "Burger",
            Category: "Mains",
            Quantity: 1,
            GrossSales: 100.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 8.00m,
            NetSales: 92.00m,
            TheoreticalCOGS: 30.00m,
            ActualCOGS: 32.00m,
            GuestCount: 4));

        // Initialize today's sales grain
        var todaySalesKey = $"{orgId}:{siteId}:sales:{today:yyyy-MM-dd}";
        var todaySalesGrain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(todaySalesKey);
        await todaySalesGrain.InitializeAsync(new DailySalesAggregationCommand(today, siteId, "Test Site"));

        await todaySalesGrain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.DineIn,
            ProductId: Guid.NewGuid(),
            ProductName: "Steak",
            Category: "Mains",
            Quantity: 1,
            GrossSales: 115.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 9.20m,
            NetSales: 105.80m,
            TheoreticalCOGS: 35.00m,
            ActualCOGS: 37.00m,
            GuestCount: 5));

        // Initialize the dashboard grain
        var dashboardKey = $"{orgId}:{siteId}:dashboard";
        var dashboardGrain = _fixture.Cluster.GrainFactory.GetGrain<ISiteDashboardGrain>(dashboardKey);
        await dashboardGrain.InitializeAsync(orgId, siteId, "Test Site");

        // Act
        var extendedMetrics = await dashboardGrain.GetExtendedMetricsAsync();

        // Assert
        extendedMetrics.TodayNetSales.Should().Be(105.80m);
        extendedMetrics.YesterdayNetSales.Should().Be(92.00m);

        // Today vs Yesterday: (105.80 - 92.00) / 92.00 * 100 = 15%
        extendedMetrics.TodayVsYesterdayPercent.Should().BeApproximately(15m, 0.1m);
    }

    // ============================================================================
    // Period Rollup Accuracy Tests
    // ============================================================================

    [Fact]
    public async Task PeriodAggregation_AggregatesMultipleDaysCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = new DateTime(2024, 3, 4);
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPeriodAggregationGrain>(
            $"{orgId}:{siteId}:period:{PeriodType.Weekly}:2024:10");

        await grain.InitializeAsync(new PeriodAggregationCommand(
            PeriodType.Weekly,
            weekStart,
            weekStart.AddDays(6),
            10, 2024));

        // Aggregate 7 days of data
        for (int day = 0; day < 7; day++)
        {
            var date = weekStart.AddDays(day);
            var dailySales = new DailySalesSnapshot(
                Date: date,
                SiteId: siteId,
                SiteName: "Test Site",
                GrossSales: 1000m + (day * 100), // Increasing daily
                NetSales: 900m + (day * 90),
                TheoreticalCOGS: 270m + (day * 27),
                ActualCOGS: 280m + (day * 28),
                GrossProfit: 620m + (day * 62),
                GrossProfitPercent: 68.89m,
                TransactionCount: 30 + day,
                GuestCount: 40 + day,
                AverageTicket: 30m,
                SalesByChannel: new Dictionary<SaleChannel, decimal>(),
                SalesByCategory: new Dictionary<string, decimal>());

            var dailyInventory = new DailyInventorySnapshot(
                Date: date,
                SiteId: siteId,
                SiteName: "Test Site",
                TotalStockValue: 25000m,
                TotalSkuCount: 150,
                LowStockCount: 5,
                OutOfStockCount: 2,
                ExpiringSoonCount: 3,
                ExpiringSoonValue: 150m,
                Ingredients: Array.Empty<InventoryIngredientSnapshot>());

            var dailyConsumption = new DailyConsumptionSnapshot(
                Date: date,
                SiteId: siteId,
                TotalTheoreticalCost: 270m + (day * 27),
                TotalActualCost: 280m + (day * 28),
                TotalVariance: 10m + day,
                VariancePercent: 3.7m,
                TopVariances: Array.Empty<VarianceBreakdown>());

            var dailyWaste = new DailyWasteSnapshot(
                Date: date,
                SiteId: siteId,
                TotalWasteValue: 20m + (day * 2),
                TotalWasteCount: 3 + day,
                WasteByReason: new Dictionary<WasteReason, decimal>(),
                WasteByCategory: new Dictionary<string, decimal>());

            await grain.AggregateFromDailyAsync(date, dailySales, dailyInventory, dailyConsumption, dailyWaste);
        }

        // Act
        var summary = await grain.GetSummaryAsync();

        // Assert
        // Gross sales: 1000 + 1100 + 1200 + 1300 + 1400 + 1500 + 1600 = 9100
        summary.SalesMetrics.GrossSales.Should().Be(9100m);

        // Net sales: 900 + 990 + 1080 + 1170 + 1260 + 1350 + 1440 = 8190
        summary.SalesMetrics.NetSales.Should().Be(8190m);

        // Transactions: 30 + 31 + 32 + 33 + 34 + 35 + 36 = 231
        summary.SalesMetrics.TransactionCount.Should().Be(231);

        // Waste: 20 + 22 + 24 + 26 + 28 + 30 + 32 = 182
        summary.TotalWasteValue.Should().Be(182m);
    }

    [Fact]
    public async Task PeriodAggregation_CalculatesStockTurnCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPeriodAggregationGrain>(
            $"{orgId}:{siteId}:period:{PeriodType.Monthly}:2024:6");

        await grain.InitializeAsync(new PeriodAggregationCommand(
            PeriodType.Monthly,
            new DateTime(2024, 6, 1),
            new DateTime(2024, 6, 30),
            6, 2024));

        var dailySales = new DailySalesSnapshot(
            Date: new DateTime(2024, 6, 15),
            SiteId: siteId,
            SiteName: "Test Site",
            GrossSales: 10000m,
            NetSales: 9000m,
            TheoreticalCOGS: 2700m,
            ActualCOGS: 3000m, // COGS for stock turn
            GrossProfit: 6000m,
            GrossProfitPercent: 66.67m,
            TransactionCount: 300,
            GuestCount: 400,
            AverageTicket: 30m,
            SalesByChannel: new Dictionary<SaleChannel, decimal>(),
            SalesByCategory: new Dictionary<string, decimal>());

        var dailyInventory = new DailyInventorySnapshot(
            Date: new DateTime(2024, 6, 15),
            SiteId: siteId,
            SiteName: "Test Site",
            TotalStockValue: 10000m, // Closing stock value for turn calculation
            TotalSkuCount: 150,
            LowStockCount: 5,
            OutOfStockCount: 2,
            ExpiringSoonCount: 3,
            ExpiringSoonValue: 150m,
            Ingredients: Array.Empty<InventoryIngredientSnapshot>());

        var dailyConsumption = new DailyConsumptionSnapshot(
            new DateTime(2024, 6, 15), siteId, 2700m, 3000m, 300m, 11.1m,
            Array.Empty<VarianceBreakdown>());

        var dailyWaste = new DailyWasteSnapshot(
            new DateTime(2024, 6, 15), siteId, 100m, 10,
            new Dictionary<WasteReason, decimal>(),
            new Dictionary<string, decimal>());

        await grain.AggregateFromDailyAsync(new DateTime(2024, 6, 15), dailySales, dailyInventory, dailyConsumption, dailyWaste);

        // Act
        var summary = await grain.GetSummaryAsync();

        // Assert
        // Stock turn = COGS / Closing Stock = 3000 / 10000 = 0.3
        summary.StockHealth.StockTurn.Should().BeApproximately(0.3m, 0.01m);
    }

    [Fact]
    public async Task PeriodAggregation_DifferentCostingMethodsReturnCorrectGP()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPeriodAggregationGrain>(
            $"{orgId}:{siteId}:period:{PeriodType.Weekly}:2024:25");

        await grain.InitializeAsync(new PeriodAggregationCommand(
            PeriodType.Weekly,
            new DateTime(2024, 6, 17),
            new DateTime(2024, 6, 23),
            25, 2024));

        var dailySales = new DailySalesSnapshot(
            Date: new DateTime(2024, 6, 17),
            SiteId: siteId,
            SiteName: "Test Site",
            GrossSales: 5000m,
            NetSales: 4500m,
            TheoreticalCOGS: 1350m,
            ActualCOGS: 1400m,
            GrossProfit: 3100m,
            GrossProfitPercent: 68.89m,
            TransactionCount: 150,
            GuestCount: 180,
            AverageTicket: 30m,
            SalesByChannel: new Dictionary<SaleChannel, decimal>(),
            SalesByCategory: new Dictionary<string, decimal>());

        var dailyInventory = new DailyInventorySnapshot(
            new DateTime(2024, 6, 17), siteId, "Test Site", 20000m, 180, 6, 2, 4, 150m,
            Array.Empty<InventoryIngredientSnapshot>());

        var dailyConsumption = new DailyConsumptionSnapshot(
            new DateTime(2024, 6, 17), siteId, 1350m, 1400m, 50m, 3.7m,
            Array.Empty<VarianceBreakdown>());

        var dailyWaste = new DailyWasteSnapshot(
            new DateTime(2024, 6, 17), siteId, 40m, 5,
            new Dictionary<WasteReason, decimal>(),
            new Dictionary<string, decimal>());

        await grain.AggregateFromDailyAsync(new DateTime(2024, 6, 17), dailySales, dailyInventory, dailyConsumption, dailyWaste);

        // Act
        var fifoMetrics = await grain.GetGrossProfitMetricsAsync(CostingMethod.FIFO);
        var wacMetrics = await grain.GetGrossProfitMetricsAsync(CostingMethod.WAC);

        // Assert
        fifoMetrics.CostingMethod.Should().Be(CostingMethod.FIFO);
        wacMetrics.CostingMethod.Should().Be(CostingMethod.WAC);
        fifoMetrics.NetSales.Should().Be(4500m);
        wacMetrics.NetSales.Should().Be(4500m);

        // GP = NetSales - ActualCOGS = 4500 - 1400 = 3100
        fifoMetrics.ActualGrossProfit.Should().Be(3100m);
        wacMetrics.ActualGrossProfit.Should().Be(3100m);

        // GP% = 3100 / 4500 * 100 = 68.89%
        fifoMetrics.ActualGrossProfitPercent.Should().BeApproximately(68.89m, 0.01m);
    }

    // ============================================================================
    // Consumption vs Sales Reconciliation Tests
    // ============================================================================

    [Fact]
    public async Task DailyConsumption_VarianceCalculation_CorrectForMultipleIngredients()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailyConsumptionGrain>(
            $"{orgId}:{siteId}:consumption:{date:yyyy-MM-dd}");

        await grain.InitializeAsync(date, siteId);

        // Positive variance (over-usage)
        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "Ground Beef", "Proteins", "kg",
            10.0m, 150.00m, 12.0m, 180.00m, CostingMethod.FIFO,
            Guid.NewGuid(), Guid.NewGuid(), null));

        // Negative variance (efficient usage)
        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "Tomatoes", "Produce", "kg",
            5.0m, 25.00m, 4.5m, 22.50m, CostingMethod.FIFO,
            Guid.NewGuid(), Guid.NewGuid(), null));

        // Zero variance
        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "Cheese", "Dairy", "kg",
            3.0m, 45.00m, 3.0m, 45.00m, CostingMethod.FIFO,
            Guid.NewGuid(), Guid.NewGuid(), null));

        // Act
        var snapshot = await grain.GetSnapshotAsync();
        var variances = await grain.GetVarianceBreakdownAsync();

        // Assert
        snapshot.TotalTheoreticalCost.Should().Be(220.00m); // 150 + 25 + 45
        snapshot.TotalActualCost.Should().Be(247.50m); // 180 + 22.50 + 45
        snapshot.TotalVariance.Should().Be(27.50m); // 247.50 - 220

        // Variance percent: 27.50 / 220 * 100 = 12.5%
        snapshot.VariancePercent.Should().BeApproximately(12.5m, 0.1m);

        variances.Should().HaveCount(3);
        // First should be Ground Beef (highest absolute variance)
        variances[0].IngredientName.Should().Be("Ground Beef");
        variances[0].CostVariance.Should().Be(30.00m);
    }

    [Fact]
    public async Task DailyConsumption_SameIngredientMultipleEntries_AggregatesCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var ingredientId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailyConsumptionGrain>(
            $"{orgId}:{siteId}:consumption:{date:yyyy-MM-dd}");

        await grain.InitializeAsync(date, siteId);

        // Same ingredient used in multiple orders
        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            ingredientId, "Ground Beef", "Proteins", "kg",
            2.0m, 30.00m, 2.2m, 33.00m, CostingMethod.FIFO,
            Guid.NewGuid(), Guid.NewGuid(), null));

        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            ingredientId, "Ground Beef", "Proteins", "kg",
            3.0m, 45.00m, 3.5m, 52.50m, CostingMethod.FIFO,
            Guid.NewGuid(), Guid.NewGuid(), null));

        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            ingredientId, "Ground Beef", "Proteins", "kg",
            1.5m, 22.50m, 1.4m, 21.00m, CostingMethod.FIFO,
            Guid.NewGuid(), Guid.NewGuid(), null));

        // Act
        var variances = await grain.GetVarianceBreakdownAsync();

        // Assert
        // Should be aggregated into a single entry
        var beefVariance = variances.Single(v => v.IngredientName == "Ground Beef");
        beefVariance.TheoreticalUsage.Should().Be(6.5m); // 2 + 3 + 1.5
        beefVariance.ActualUsage.Should().Be(7.1m); // 2.2 + 3.5 + 1.4
        beefVariance.TheoreticalCost.Should().Be(97.50m); // 30 + 45 + 22.50
        beefVariance.ActualCost.Should().Be(106.50m); // 33 + 52.50 + 21
        beefVariance.CostVariance.Should().Be(9.00m);
    }

    // ============================================================================
    // Dashboard Real-Time Updates Tests
    // ============================================================================

    [Fact]
    public async Task SiteDashboard_GetHourlySales_IntegratesWithDaypartAnalysis()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        // Initialize daypart analysis grain
        var daypartKey = GrainKeys.DaypartAnalysis(orgId, siteId, DateOnly.FromDateTime(today));
        var daypartGrain = _fixture.Cluster.GrainFactory.GetGrain<IDaypartAnalysisGrain>(daypartKey);
        await daypartGrain.InitializeAsync(today, siteId);

        // Record hourly sales
        await daypartGrain.RecordHourlySaleAsync(new RecordHourlySaleCommand(
            Hour: 12,
            NetSales: 500.00m,
            TransactionCount: 20,
            GuestCount: 30,
            TheoreticalCOGS: 150.00m));

        await daypartGrain.RecordHourlySaleAsync(new RecordHourlySaleCommand(
            Hour: 13,
            NetSales: 450.00m,
            TransactionCount: 18,
            GuestCount: 25,
            TheoreticalCOGS: 135.00m));

        await daypartGrain.RecordHourlySaleAsync(new RecordHourlySaleCommand(
            Hour: 19,
            NetSales: 800.00m,
            TransactionCount: 35,
            GuestCount: 50,
            TheoreticalCOGS: 240.00m));

        // Initialize dashboard grain
        var dashboardKey = $"{orgId}:{siteId}:dashboard";
        var dashboardGrain = _fixture.Cluster.GrainFactory.GetGrain<ISiteDashboardGrain>(dashboardKey);
        await dashboardGrain.InitializeAsync(orgId, siteId, "Test Site");

        // Act
        var hourlySales = await dashboardGrain.GetHourlySalesAsync();

        // Assert
        hourlySales.HourlySales.Should().HaveCount(3);
        hourlySales.HourlySales.Should().Contain(h => h.Hour == 12 && h.NetSales == 500.00m);
        hourlySales.HourlySales.Should().Contain(h => h.Hour == 19 && h.NetSales == 800.00m);
    }

    [Fact]
    public async Task SiteDashboard_GetTopSellingItems_IntegratesWithProductMix()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        // Initialize product mix grain
        var productMixKey = GrainKeys.ProductMix(orgId, siteId, DateOnly.FromDateTime(today));
        var productMixGrain = _fixture.Cluster.GrainFactory.GetGrain<IProductMixGrain>(productMixKey);
        await productMixGrain.InitializeAsync(today, siteId);

        // Record product sales
        await productMixGrain.RecordProductSaleAsync(new RecordProductSaleCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Signature Burger",
            Category: "Mains",
            Quantity: 50,
            NetSales: 750.00m,
            COGS: 225.00m,
            Modifiers: []));

        await productMixGrain.RecordProductSaleAsync(new RecordProductSaleCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Classic Fries",
            Category: "Sides",
            Quantity: 100,
            NetSales: 400.00m,
            COGS: 80.00m,
            Modifiers: []));

        await productMixGrain.RecordProductSaleAsync(new RecordProductSaleCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Craft Soda",
            Category: "Beverages",
            Quantity: 75,
            NetSales: 225.00m,
            COGS: 37.50m,
            Modifiers: []));

        // Initialize dashboard grain
        var dashboardKey = $"{orgId}:{siteId}:dashboard";
        var dashboardGrain = _fixture.Cluster.GrainFactory.GetGrain<ISiteDashboardGrain>(dashboardKey);
        await dashboardGrain.InitializeAsync(orgId, siteId, "Test Site");

        // Act
        var topItems = await dashboardGrain.GetTopSellingItemsAsync(3);

        // Assert
        topItems.Should().HaveCount(3);
        // By revenue, Signature Burger should be first
        topItems[0].ProductName.Should().Be("Signature Burger");
        topItems[0].NetSales.Should().Be(750.00m);
        topItems[0].GrossProfit.Should().Be(525.00m);
        topItems[0].GrossProfitPercent.Should().Be(70.00m);
    }

    [Fact]
    public async Task SiteDashboard_GetPaymentBreakdown_IntegratesWithReconciliation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        // Initialize payment reconciliation grain
        var reconciliationKey = GrainKeys.PaymentReconciliation(orgId, siteId, DateOnly.FromDateTime(today));
        var reconciliationGrain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentReconciliationGrain>(reconciliationKey);
        await reconciliationGrain.InitializeAsync(today, siteId);

        // Record payments
        await reconciliationGrain.RecordPosPaymentAsync(new RecordPosPaymentCommand(
            PaymentMethod: "Cash",
            Amount: 500.00m,
            ProcessorName: null,
            TransactionId: null));

        await reconciliationGrain.RecordPosPaymentAsync(new RecordPosPaymentCommand(
            PaymentMethod: "Credit Card",
            Amount: 1200.00m,
            ProcessorName: "Stripe",
            TransactionId: "ch_123"));

        await reconciliationGrain.RecordPosPaymentAsync(new RecordPosPaymentCommand(
            PaymentMethod: "Debit Card",
            Amount: 300.00m,
            ProcessorName: "Stripe",
            TransactionId: "ch_456"));

        // Initialize dashboard grain
        var dashboardKey = $"{orgId}:{siteId}:dashboard";
        var dashboardGrain = _fixture.Cluster.GrainFactory.GetGrain<ISiteDashboardGrain>(dashboardKey);
        await dashboardGrain.InitializeAsync(orgId, siteId, "Test Site");

        // Act
        var paymentBreakdown = await dashboardGrain.GetPaymentBreakdownAsync();

        // Assert
        paymentBreakdown.TotalCollected.Should().Be(2000.00m);
        paymentBreakdown.Payments.Should().HaveCount(2); // Cash and Card

        var cashPayment = paymentBreakdown.Payments.First(p => p.PaymentMethod == "Cash");
        cashPayment.Amount.Should().Be(500.00m);
        cashPayment.PercentOfTotal.Should().Be(25.00m);

        var cardPayment = paymentBreakdown.Payments.First(p => p.PaymentMethod == "Card");
        cardPayment.Amount.Should().Be(1500.00m);
        cardPayment.PercentOfTotal.Should().Be(75.00m);
    }

    // ============================================================================
    // Waste Categorization Extended Tests
    // ============================================================================

    [Fact]
    public async Task DailyWaste_CategoryBreakdown_AggregatesCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailyWasteGrain>(
            $"{orgId}:{siteId}:waste:{date:yyyy-MM-dd}");

        await grain.InitializeAsync(date, siteId);

        // Record waste across categories
        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Lettuce", "LET-1", "Produce", null,
            2m, "kg", WasteReason.Spoilage, "Wilted", 10.00m,
            Guid.NewGuid(), null, null));

        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Tomatoes", "TOM-1", "Produce", null,
            3m, "kg", WasteReason.Spoilage, "Overripe", 15.00m,
            Guid.NewGuid(), null, null));

        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Chicken", "CHK-1", "Proteins", null,
            1m, "kg", WasteReason.Expired, "Past date", 20.00m,
            Guid.NewGuid(), null, null));

        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Wine Glass", "WG-1", "Equipment", null,
            2m, "ea", WasteReason.Breakage, "Dropped", 30.00m,
            Guid.NewGuid(), null, null));

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.TotalWasteValue.Should().Be(75.00m);
        snapshot.TotalWasteCount.Should().Be(4);

        snapshot.WasteByCategory.Should().HaveCount(3);
        snapshot.WasteByCategory["Produce"].Should().Be(25.00m); // 10 + 15
        snapshot.WasteByCategory["Proteins"].Should().Be(20.00m);
        snapshot.WasteByCategory["Equipment"].Should().Be(30.00m);

        snapshot.WasteByReason.Should().HaveCount(3);
        snapshot.WasteByReason[WasteReason.Spoilage].Should().Be(25.00m);
        snapshot.WasteByReason[WasteReason.Expired].Should().Be(20.00m);
        snapshot.WasteByReason[WasteReason.Breakage].Should().Be(30.00m);
    }

    // ============================================================================
    // Date Boundary and Timing Tests
    // ============================================================================

    [Fact]
    public async Task DailySales_SeparateGrainsPerDay()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var day1 = DateTime.UtcNow.Date;
        var day2 = day1.AddDays(1);

        // Initialize grains for two different days
        var day1Key = $"{orgId}:{siteId}:sales:{day1:yyyy-MM-dd}";
        var day1Grain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(day1Key);
        await day1Grain.InitializeAsync(new DailySalesAggregationCommand(day1, siteId, "Test Site"));

        var day2Key = $"{orgId}:{siteId}:sales:{day2:yyyy-MM-dd}";
        var day2Grain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(day2Key);
        await day2Grain.InitializeAsync(new DailySalesAggregationCommand(day2, siteId, "Test Site"));

        // Record sales on each day
        await day1Grain.RecordSaleAsync(new RecordSaleCommand(
            Guid.NewGuid(), SaleChannel.DineIn, Guid.NewGuid(), "Product", "Category",
            1, 100.00m, 0, 0, 0, 8.00m, 92.00m, 30.00m, 32.00m, 1));

        await day2Grain.RecordSaleAsync(new RecordSaleCommand(
            Guid.NewGuid(), SaleChannel.DineIn, Guid.NewGuid(), "Product", "Category",
            1, 200.00m, 0, 0, 0, 16.00m, 184.00m, 60.00m, 64.00m, 2));

        // Act
        var day1Snapshot = await day1Grain.GetSnapshotAsync();
        var day2Snapshot = await day2Grain.GetSnapshotAsync();

        // Assert
        day1Snapshot.GrossSales.Should().Be(100.00m);
        day1Snapshot.Date.Should().Be(day1);

        day2Snapshot.GrossSales.Should().Be(200.00m);
        day2Snapshot.Date.Should().Be(day2);
    }

    [Fact]
    public async Task PeriodAggregation_FourWeekPeriod_HasCorrectDateRange()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var periodStart = new DateTime(2024, 2, 26);
        var periodEnd = new DateTime(2024, 3, 24);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPeriodAggregationGrain>(
            $"{orgId}:{siteId}:period:{PeriodType.FourWeek}:2024:3");

        await grain.InitializeAsync(new PeriodAggregationCommand(
            PeriodType: PeriodType.FourWeek,
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            PeriodNumber: 3,
            FiscalYear: 2024));

        // Act
        var summary = await grain.GetSummaryAsync();

        // Assert
        summary.PeriodType.Should().Be(PeriodType.FourWeek);
        summary.PeriodNumber.Should().Be(3);
        summary.PeriodStart.Should().Be(periodStart);
        summary.PeriodEnd.Should().Be(periodEnd);

        // 4-week period should span 28 days
        (summary.PeriodEnd - summary.PeriodStart).Days.Should().Be(27); // Inclusive
    }
}
