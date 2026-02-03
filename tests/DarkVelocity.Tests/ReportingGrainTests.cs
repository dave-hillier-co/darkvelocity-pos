using DarkVelocity.Host.Costing;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Projections;
using FluentAssertions;

namespace DarkVelocity.Tests;

// ============================================================================
// Daily Inventory Snapshot Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DailyInventorySnapshotGrainTests
{
    private readonly TestClusterFixture _fixture;

    public DailyInventorySnapshotGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDailyInventorySnapshotGrain GetGrain(Guid orgId, Guid siteId, DateTime date)
    {
        var key = $"{orgId}:{siteId}:inventory-snapshot:{date:yyyy-MM-dd}";
        return _fixture.Cluster.GrainFactory.GetGrain<IDailyInventorySnapshotGrain>(key);
    }

    [Fact]
    public async Task InitializeAsync_CreatesInventorySnapshot()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        var command = new InventorySnapshotCommand(
            BusinessDate: date,
            SiteId: siteId,
            SiteName: "Warehouse A");

        await grain.InitializeAsync(command);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Date.Should().Be(date);
        snapshot.SiteId.Should().Be(siteId);
        snapshot.SiteName.Should().Be("Warehouse A");
    }

    [Fact]
    public async Task RecordIngredientSnapshotAsync_RecordsIngredient()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(new InventorySnapshotCommand(date, siteId, "Test Site"));

        var ingredientSnapshot = new IngredientSnapshot(
            IngredientId: Guid.NewGuid(),
            IngredientName: "Flour",
            Sku: "FLR-001",
            Category: "Dry Goods",
            OnHandQuantity: 50m,
            AvailableQuantity: 45m,
            Unit: "kg",
            WeightedAverageCost: 2.50m,
            TotalValue: 125.00m,
            EarliestExpiry: DateTime.UtcNow.AddDays(30),
            IsLowStock: false,
            IsOutOfStock: false,
            IsExpiringSoon: false,
            IsOverPar: false,
            ActiveBatchCount: 2);

        await grain.RecordIngredientSnapshotAsync(ingredientSnapshot);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Ingredients.Should().ContainSingle();
        snapshot.Ingredients[0].IngredientName.Should().Be("Flour");
        snapshot.TotalStockValue.Should().Be(125.00m);
        snapshot.TotalSkuCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordIngredientSnapshotAsync_TracksLowStockCount()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(new InventorySnapshotCommand(date, siteId, "Test Site"));

        await grain.RecordIngredientSnapshotAsync(new IngredientSnapshot(
            Guid.NewGuid(), "Item 1", "SKU-1", "Cat", 100m, 100m, "ea",
            1.00m, 100.00m, null, false, false, false, false, 1));

        await grain.RecordIngredientSnapshotAsync(new IngredientSnapshot(
            Guid.NewGuid(), "Item 2", "SKU-2", "Cat", 5m, 5m, "ea",
            2.00m, 10.00m, null, true, false, false, false, 1));

        await grain.RecordIngredientSnapshotAsync(new IngredientSnapshot(
            Guid.NewGuid(), "Item 3", "SKU-3", "Cat", 0m, 0m, "ea",
            3.00m, 0m, null, false, true, false, false, 0));

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LowStockCount.Should().Be(1);
        snapshot.OutOfStockCount.Should().Be(1);
        snapshot.TotalSkuCount.Should().Be(3);
    }

    [Fact]
    public async Task RecordIngredientSnapshotAsync_TracksExpiringSoon()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(new InventorySnapshotCommand(date, siteId, "Test Site"));

        await grain.RecordIngredientSnapshotAsync(new IngredientSnapshot(
            Guid.NewGuid(), "Fresh Milk", "MLK-001", "Dairy", 20m, 20m, "L",
            1.50m, 30.00m, DateTime.UtcNow.AddDays(3), false, false, true, false, 1));

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ExpiringSoonCount.Should().Be(1);
        snapshot.ExpiringSoonValue.Should().Be(30.00m);
    }

    [Fact]
    public async Task GetHealthMetricsAsync_ReturnsStockHealthMetrics()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(new InventorySnapshotCommand(date, siteId, "Test Site"));

        await grain.RecordIngredientSnapshotAsync(new IngredientSnapshot(
            Guid.NewGuid(), "Item A", "SKU-A", "Cat", 50m, 50m, "ea",
            10.00m, 500.00m, null, false, false, false, false, 2));

        var metrics = await grain.GetHealthMetricsAsync();

        metrics.TotalStockValue.Should().Be(500.00m);
        metrics.TotalSkuCount.Should().Be(1);
        metrics.ActiveBatchCount.Should().BeGreaterThanOrEqualTo(0);
    }
}

// ============================================================================
// Daily Consumption Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DailyConsumptionGrainTests
{
    private readonly TestClusterFixture _fixture;

    public DailyConsumptionGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDailyConsumptionGrain GetGrain(Guid orgId, Guid siteId, DateTime date)
    {
        var key = $"{orgId}:{siteId}:consumption:{date:yyyy-MM-dd}";
        return _fixture.Cluster.GrainFactory.GetGrain<IDailyConsumptionGrain>(key);
    }

    [Fact]
    public async Task InitializeAsync_CreatesConsumptionRecord()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Date.Should().Be(date);
        snapshot.SiteId.Should().Be(siteId);
    }

    [Fact]
    public async Task RecordConsumptionAsync_RecordsTheoreticalAndActual()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        var command = new RecordConsumptionCommand(
            IngredientId: Guid.NewGuid(),
            IngredientName: "Ground Beef",
            Category: "Proteins",
            Unit: "kg",
            TheoreticalQuantity: 10.0m,
            TheoreticalCost: 150.00m,
            ActualQuantity: 11.5m,
            ActualCost: 172.50m,
            CostingMethod: CostingMethod.FIFO,
            OrderId: Guid.NewGuid(),
            MenuItemId: Guid.NewGuid(),
            RecipeVersionId: null);

        await grain.RecordConsumptionAsync(command);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalTheoreticalCost.Should().Be(150.00m);
        snapshot.TotalActualCost.Should().Be(172.50m);
        snapshot.TotalVariance.Should().Be(22.50m);
    }

    [Fact]
    public async Task RecordConsumptionAsync_AggregatesMultipleConsumptions()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "Ingredient 1", "Cat", "kg",
            5.0m, 50.00m, 5.5m, 55.00m, CostingMethod.FIFO,
            null, null, null));

        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "Ingredient 2", "Cat", "L",
            10.0m, 30.00m, 9.0m, 27.00m, CostingMethod.WAC,
            null, null, null));

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalTheoreticalCost.Should().Be(80.00m);
        snapshot.TotalActualCost.Should().Be(82.00m);
    }

    [Fact]
    public async Task GetVarianceBreakdownAsync_ReturnsTopVariances()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "High Variance Item", "Cat", "kg",
            10.0m, 100.00m, 15.0m, 150.00m, CostingMethod.FIFO,
            null, null, null));

        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "Low Variance Item", "Cat", "kg",
            20.0m, 200.00m, 21.0m, 210.00m, CostingMethod.FIFO,
            null, null, null));

        var variances = await grain.GetVarianceBreakdownAsync();

        variances.Should().NotBeEmpty();
        variances.Should().Contain(v => v.IngredientName == "High Variance Item");
    }
}

// ============================================================================
// Daily Waste Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DailyWasteGrainTests
{
    private readonly TestClusterFixture _fixture;

    public DailyWasteGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDailyWasteGrain GetGrain(Guid orgId, Guid siteId, DateTime date)
    {
        var key = $"{orgId}:{siteId}:waste:{date:yyyy-MM-dd}";
        return _fixture.Cluster.GrainFactory.GetGrain<IDailyWasteGrain>(key);
    }

    [Fact]
    public async Task InitializeAsync_CreatesWasteRecord()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Date.Should().Be(date);
        snapshot.SiteId.Should().Be(siteId);
    }

    [Fact]
    public async Task RecordWasteAsync_RecordsSpoilage()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        var command = new RecordWasteFactCommand(
            WasteId: Guid.NewGuid(),
            IngredientId: Guid.NewGuid(),
            IngredientName: "Lettuce",
            Sku: "LET-001",
            Category: "Produce",
            BatchId: Guid.NewGuid(),
            Quantity: 5m,
            Unit: "head",
            Reason: WasteReason.Spoilage,
            ReasonDetails: "Wilted due to refrigeration failure",
            CostBasis: 12.50m,
            RecordedBy: Guid.NewGuid(),
            ApprovedBy: Guid.NewGuid(),
            PhotoUrl: null);

        await grain.RecordWasteAsync(command);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalWasteValue.Should().Be(12.50m);
        snapshot.TotalWasteCount.Should().Be(1);
        snapshot.WasteByReason.Should().ContainKey(WasteReason.Spoilage);
    }

    [Fact]
    public async Task RecordWasteAsync_AggregatesWasteByReason()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Item 1", "SKU-1", "Cat", null,
            2m, "ea", WasteReason.Spoilage, "Spoiled", 10.00m,
            Guid.NewGuid(), null, null));

        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Item 2", "SKU-2", "Cat", null,
            1m, "ea", WasteReason.Breakage, "Dropped", 25.00m,
            Guid.NewGuid(), null, null));

        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Item 3", "SKU-3", "Cat", null,
            3m, "ea", WasteReason.Spoilage, "Expired", 15.00m,
            Guid.NewGuid(), null, null));

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalWasteValue.Should().Be(50.00m);
        snapshot.TotalWasteCount.Should().Be(3);
        snapshot.WasteByReason[WasteReason.Spoilage].Should().Be(25.00m);
        snapshot.WasteByReason[WasteReason.Breakage].Should().Be(25.00m);
    }

    [Fact]
    public async Task RecordWasteAsync_AggregatesWasteByCategory()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Tomatoes", "TOM-1", "Produce", null,
            5m, "kg", WasteReason.Spoilage, "Overripe", 20.00m,
            Guid.NewGuid(), null, null));

        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Chicken", "CHK-1", "Proteins", null,
            2m, "kg", WasteReason.Expired, "Past date", 30.00m,
            Guid.NewGuid(), null, null));

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.WasteByCategory.Should().ContainKey("Produce");
        snapshot.WasteByCategory.Should().ContainKey("Proteins");
        snapshot.WasteByCategory["Produce"].Should().Be(20.00m);
        snapshot.WasteByCategory["Proteins"].Should().Be(30.00m);
    }
}

// ============================================================================
// Period Aggregation Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PeriodAggregationGrainTests
{
    private readonly TestClusterFixture _fixture;

    public PeriodAggregationGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IPeriodAggregationGrain GetGrain(Guid orgId, Guid siteId, PeriodType periodType, int year, int periodNumber)
    {
        var key = $"{orgId}:{siteId}:period:{periodType}:{year}:{periodNumber}";
        return _fixture.Cluster.GrainFactory.GetGrain<IPeriodAggregationGrain>(key);
    }

    [Fact]
    public async Task InitializeAsync_CreatesWeeklyPeriod()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, PeriodType.Weekly, 2024, 5);

        var command = new PeriodAggregationCommand(
            PeriodType: PeriodType.Weekly,
            PeriodStart: new DateTime(2024, 1, 29),
            PeriodEnd: new DateTime(2024, 2, 4),
            PeriodNumber: 5,
            FiscalYear: 2024);

        await grain.InitializeAsync(command);

        var summary = await grain.GetSummaryAsync();
        summary.PeriodType.Should().Be(PeriodType.Weekly);
        summary.PeriodNumber.Should().Be(5);
    }

    [Fact]
    public async Task InitializeAsync_CreatesFourWeekPeriod()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, PeriodType.FourWeek, 2024, 3);

        var command = new PeriodAggregationCommand(
            PeriodType: PeriodType.FourWeek,
            PeriodStart: new DateTime(2024, 2, 26),
            PeriodEnd: new DateTime(2024, 3, 24),
            PeriodNumber: 3,
            FiscalYear: 2024);

        await grain.InitializeAsync(command);

        var summary = await grain.GetSummaryAsync();
        summary.PeriodType.Should().Be(PeriodType.FourWeek);
        summary.PeriodNumber.Should().Be(3);
    }

    [Fact]
    public async Task AggregateFromDailyAsync_AggregatesDailyData()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, PeriodType.Weekly, 2024, 10);

        await grain.InitializeAsync(new PeriodAggregationCommand(
            PeriodType.Weekly,
            new DateTime(2024, 3, 4),
            new DateTime(2024, 3, 10),
            10, 2024));

        var dailySales = new DailySalesSnapshot(
            Date: new DateTime(2024, 3, 4),
            SiteId: siteId,
            SiteName: "Test Site",
            GrossSales: 5000m,
            NetSales: 4500m,
            TheoreticalCOGS: 1350m,
            ActualCOGS: 1400m,
            GrossProfit: 3100m,
            GrossProfitPercent: 68.89m,
            TransactionCount: 150,
            GuestCount: 200,
            AverageTicket: 30m,
            SalesByChannel: new Dictionary<SaleChannel, decimal>(),
            SalesByCategory: new Dictionary<string, decimal>());

        var dailyInventory = new DailyInventorySnapshot(
            Date: new DateTime(2024, 3, 4),
            SiteId: siteId,
            SiteName: "Test Site",
            TotalStockValue: 25000m,
            TotalSkuCount: 150,
            LowStockCount: 5,
            OutOfStockCount: 2,
            ExpiringSoonCount: 3,
            ExpiringSoonValue: 150m,
            Ingredients: Array.Empty<IngredientSnapshot>());

        var dailyConsumption = new DailyConsumptionSnapshot(
            Date: new DateTime(2024, 3, 4),
            SiteId: siteId,
            TotalTheoreticalCost: 1350m,
            TotalActualCost: 1400m,
            TotalVariance: 50m,
            VariancePercent: 3.7m,
            TopVariances: Array.Empty<VarianceBreakdown>());

        var dailyWaste = new DailyWasteSnapshot(
            Date: new DateTime(2024, 3, 4),
            SiteId: siteId,
            TotalWasteValue: 75m,
            TotalWasteCount: 8,
            WasteByReason: new Dictionary<WasteReason, decimal>(),
            WasteByCategory: new Dictionary<string, decimal>());

        await grain.AggregateFromDailyAsync(
            new DateTime(2024, 3, 4),
            dailySales,
            dailyInventory,
            dailyConsumption,
            dailyWaste);

        var summary = await grain.GetSummaryAsync();
        summary.SalesMetrics.GrossSales.Should().Be(5000m);
        summary.TotalWasteValue.Should().Be(75m);
    }
}

// ============================================================================
// Site Dashboard Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class SiteDashboardGrainTests
{
    private readonly TestClusterFixture _fixture;

    public SiteDashboardGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private ISiteDashboardGrain GetGrain(Guid orgId, Guid siteId)
    {
        var key = $"{orgId}:{siteId}:dashboard";
        return _fixture.Cluster.GrainFactory.GetGrain<ISiteDashboardGrain>(key);
    }

    [Fact]
    public async Task InitializeAsync_CreatesDashboard()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(orgId, siteId, "Downtown Location");

        var metrics = await grain.GetMetricsAsync();
        metrics.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMetricsAsync_ReturnsDashboardMetrics()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(orgId, siteId, "Test Location");

        var metrics = await grain.GetMetricsAsync();

        metrics.TodayNetSales.Should().BeGreaterThanOrEqualTo(0);
        metrics.LowStockAlertCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task RefreshAsync_UpdatesDashboardData()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(orgId, siteId, "Refresh Test Site");

        await grain.RefreshAsync();

        var metrics = await grain.GetMetricsAsync();
        metrics.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTopVariancesAsync_ReturnsVarianceList()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.InitializeAsync(orgId, siteId, "Variance Test Site");

        var variances = await grain.GetTopVariancesAsync(5);

        variances.Should().NotBeNull();
        variances.Count.Should().BeLessThanOrEqualTo(5);
    }
}
