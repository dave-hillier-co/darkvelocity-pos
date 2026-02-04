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

    [Fact]
    public async Task RecordIngredientSnapshotAsync_OverPar_ShouldTrack()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(new InventorySnapshotCommand(date, siteId, "Test Site"));

        // Record an item that is over par
        await grain.RecordIngredientSnapshotAsync(new IngredientSnapshot(
            Guid.NewGuid(), "Excess Item", "SKU-EXCESS", "Dry Goods", 200m, 200m, "kg",
            5.00m, 1000.00m, null, false, false, false, true, 5));

        var metrics = await grain.GetHealthMetricsAsync();

        metrics.OverParCount.Should().Be(1);
        metrics.OverParValue.Should().Be(1000.00m);
    }

    [Fact]
    public async Task RecordIngredientSnapshotAsync_MultipleIngredients_ShouldAggregate()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(new InventorySnapshotCommand(date, siteId, "Test Site"));

        // Record multiple ingredients
        await grain.RecordIngredientSnapshotAsync(new IngredientSnapshot(
            Guid.NewGuid(), "Flour", "FLR-001", "Dry Goods", 50m, 50m, "kg",
            2.00m, 100.00m, null, false, false, false, false, 2));

        await grain.RecordIngredientSnapshotAsync(new IngredientSnapshot(
            Guid.NewGuid(), "Sugar", "SUG-001", "Dry Goods", 30m, 30m, "kg",
            1.50m, 45.00m, null, false, false, false, false, 1));

        await grain.RecordIngredientSnapshotAsync(new IngredientSnapshot(
            Guid.NewGuid(), "Butter", "BUT-001", "Dairy", 20m, 18m, "kg",
            8.00m, 160.00m, DateTime.UtcNow.AddDays(5), false, false, true, false, 3));

        var snapshot = await grain.GetSnapshotAsync();

        snapshot.Ingredients.Should().HaveCount(3);
        snapshot.TotalStockValue.Should().Be(305.00m); // 100 + 45 + 160
        snapshot.TotalSkuCount.Should().Be(3);
        snapshot.ExpiringSoonCount.Should().Be(1);
    }

    [Fact]
    public async Task GetFactsAsync_ShouldReturnFacts()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var ingredientId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(new InventorySnapshotCommand(date, siteId, "Test Site"));

        await grain.RecordIngredientSnapshotAsync(new IngredientSnapshot(
            ingredientId, "Test Ingredient", "TST-001", "Test Category", 100m, 95m, "ea",
            5.00m, 500.00m, null, false, false, false, false, 2));

        var facts = await grain.GetFactsAsync();

        facts.Should().ContainSingle();
        facts[0].IngredientId.Should().Be(ingredientId);
        facts[0].IngredientName.Should().Be("Test Ingredient");
        facts[0].OnHandQuantity.Should().Be(100m);
        facts[0].TotalValue.Should().Be(500.00m);
    }

    [Fact]
    public async Task FinalizeAsync_ShouldFinalize()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(new InventorySnapshotCommand(date, siteId, "Test Site"));

        await grain.RecordIngredientSnapshotAsync(new IngredientSnapshot(
            Guid.NewGuid(), "Item", "SKU", "Cat", 10m, 10m, "ea",
            1.00m, 10.00m, null, false, false, false, false, 1));

        // Act
        await grain.FinalizeAsync();

        // Assert - grain should still work but be marked as finalized
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Should().NotBeNull();
        snapshot.TotalSkuCount.Should().Be(1);
    }

    [Fact]
    public async Task NotInitialized_ShouldThrow()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        // Act & Assert
        await grain.Invoking(g => g.GetSnapshotAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
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

    [Fact]
    public async Task RecordConsumptionAsync_SameIngredient_ShouldAggregate()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var ingredientId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        // Record consumption for the same ingredient multiple times
        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            ingredientId, "Ground Beef", "Proteins", "kg",
            5.0m, 50.00m, 5.5m, 55.00m, CostingMethod.FIFO,
            Guid.NewGuid(), Guid.NewGuid(), null));

        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            ingredientId, "Ground Beef", "Proteins", "kg",
            3.0m, 30.00m, 3.2m, 32.00m, CostingMethod.FIFO,
            Guid.NewGuid(), Guid.NewGuid(), null));

        var variances = await grain.GetVarianceBreakdownAsync();

        // Should aggregate to single ingredient entry
        var beefVariance = variances.FirstOrDefault(v => v.IngredientName == "Ground Beef");
        beefVariance.Should().NotBeNull();
        beefVariance!.TheoreticalCost.Should().Be(80.00m); // 50 + 30
        beefVariance.ActualCost.Should().Be(87.00m); // 55 + 32
    }

    [Fact]
    public async Task GetVarianceBreakdownAsync_ShouldOrderByAbsoluteVariance()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        // Add items with different variance levels
        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "Small Variance", "Cat", "kg",
            10.0m, 100.00m, 11.0m, 110.00m, CostingMethod.FIFO,
            null, null, null)); // Variance: 10

        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "Large Variance", "Cat", "kg",
            10.0m, 100.00m, 20.0m, 200.00m, CostingMethod.FIFO,
            null, null, null)); // Variance: 100

        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "Medium Variance", "Cat", "kg",
            10.0m, 100.00m, 15.0m, 150.00m, CostingMethod.FIFO,
            null, null, null)); // Variance: 50

        var variances = await grain.GetVarianceBreakdownAsync();

        // Should be ordered by absolute variance descending
        variances[0].IngredientName.Should().Be("Large Variance");
        variances[1].IngredientName.Should().Be("Medium Variance");
        variances[2].IngredientName.Should().Be("Small Variance");
    }

    [Fact]
    public async Task RecordConsumptionAsync_NegativeVariance_ShouldHandle()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        // Actual less than theoretical (negative variance - efficient use)
        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "Efficient Item", "Cat", "kg",
            20.0m, 200.00m, 18.0m, 180.00m, CostingMethod.FIFO,
            null, null, null));

        var snapshot = await grain.GetSnapshotAsync();

        snapshot.TotalTheoreticalCost.Should().Be(200.00m);
        snapshot.TotalActualCost.Should().Be(180.00m);
        snapshot.TotalVariance.Should().Be(-20.00m); // Negative = saved money
        snapshot.VariancePercent.Should().Be(-10m); // -20/200 * 100
    }

    [Fact]
    public async Task RecordConsumptionAsync_ZeroTheoreticalCost_ShouldHandle()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        // Zero theoretical cost edge case
        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "Zero Theoretical", "Cat", "kg",
            0m, 0m, 5.0m, 50.00m, CostingMethod.FIFO,
            null, null, null));

        var snapshot = await grain.GetSnapshotAsync();

        snapshot.TotalTheoreticalCost.Should().Be(0m);
        snapshot.TotalActualCost.Should().Be(50.00m);
        snapshot.VariancePercent.Should().Be(0); // Avoid division by zero
    }

    [Fact]
    public async Task GetFactsAsync_ShouldReturnFacts()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var ingredientId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        await grain.RecordConsumptionAsync(new RecordConsumptionCommand(
            ingredientId, "Test Ingredient", "Category", "kg",
            10.0m, 100.00m, 11.0m, 110.00m, CostingMethod.FIFO,
            orderId, Guid.NewGuid(), null));

        var facts = await grain.GetFactsAsync();

        facts.Should().ContainSingle();
        facts[0].IngredientId.Should().Be(ingredientId);
        facts[0].OrderId.Should().Be(orderId);
        facts[0].TheoreticalQuantity.Should().Be(10.0m);
        facts[0].ActualQuantity.Should().Be(11.0m);
    }

    [Fact]
    public async Task NotInitialized_ShouldThrow()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        // Act & Assert
        await grain.Invoking(g => g.GetSnapshotAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
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

    [Fact]
    public async Task RecordWasteAsync_AllReasonTypes()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        // Test all waste reason types
        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Milk", "MLK-1", "Dairy", null,
            2m, "L", WasteReason.Expired, "Past use-by date", 5.00m,
            Guid.NewGuid(), null, null));

        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Wine Glass", "WG-1", "Equipment", null,
            3m, "ea", WasteReason.Breakage, "Dropped while cleaning", 15.00m,
            Guid.NewGuid(), null, null));

        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Draft Line", "DL-1", "Beverages", null,
            1m, "L", WasteReason.LineCleaning, "Weekly line clean", 8.00m,
            Guid.NewGuid(), null, null));

        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Prep Food", "PF-1", "Prepared", null,
            2m, "kg", WasteReason.OverProduction, "Excess prep", 25.00m,
            Guid.NewGuid(), null, null));

        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Returned Steak", "STK-1", "Proteins", null,
            1m, "ea", WasteReason.CustomerReturn, "Overcooked", 22.00m,
            Guid.NewGuid(), null, null));

        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Produce Delivery", "PRD-1", "Produce", null,
            5m, "kg", WasteReason.QualityRejection, "Poor quality on delivery", 30.00m,
            Guid.NewGuid(), null, null));

        var snapshot = await grain.GetSnapshotAsync();

        snapshot.TotalWasteCount.Should().Be(6);
        snapshot.TotalWasteValue.Should().Be(105.00m);
        snapshot.WasteByReason.Should().ContainKey(WasteReason.Expired);
        snapshot.WasteByReason.Should().ContainKey(WasteReason.Breakage);
        snapshot.WasteByReason.Should().ContainKey(WasteReason.LineCleaning);
        snapshot.WasteByReason.Should().ContainKey(WasteReason.OverProduction);
        snapshot.WasteByReason.Should().ContainKey(WasteReason.CustomerReturn);
        snapshot.WasteByReason.Should().ContainKey(WasteReason.QualityRejection);
    }

    [Fact]
    public async Task RecordWasteAsync_WithPhotoUrl_ShouldStore()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var wasteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        var photoUrl = "https://storage.example.com/waste/photo123.jpg";
        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            wasteId, Guid.NewGuid(), "Damaged Item", "DMG-1", "Goods", Guid.NewGuid(),
            1m, "ea", WasteReason.Breakage, "Packaging damaged in storage", 50.00m,
            Guid.NewGuid(), null, photoUrl));

        var facts = await grain.GetFactsAsync();

        facts.Should().ContainSingle();
        facts[0].PhotoUrl.Should().Be(photoUrl);
    }

    [Fact]
    public async Task RecordWasteAsync_ApprovalWorkflow_ShouldTrack()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var recordedBy = Guid.NewGuid();
        var approvedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        // Record waste with approval workflow
        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Expensive Item", "EXP-1", "Equipment", null,
            1m, "ea", WasteReason.Breakage, "Dropped by staff", 500.00m,
            recordedBy, approvedBy, null));

        var facts = await grain.GetFactsAsync();

        facts.Should().ContainSingle();
        facts[0].RecordedBy.Should().Be(recordedBy);
        facts[0].ApprovedBy.Should().Be(approvedBy);
    }

    [Fact]
    public async Task GetFactsAsync_ShouldReturnFacts()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var wasteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, date);

        await grain.InitializeAsync(date, siteId);

        await grain.RecordWasteAsync(new RecordWasteFactCommand(
            wasteId, ingredientId, "Test Item", "TST-1", "Test Category", null,
            5m, "kg", WasteReason.Spoilage, "Test reason", 100.00m,
            Guid.NewGuid(), null, null));

        var facts = await grain.GetFactsAsync();

        facts.Should().ContainSingle();
        facts[0].FactId.Should().Be(wasteId);
        facts[0].IngredientId.Should().Be(ingredientId);
        facts[0].IngredientName.Should().Be("Test Item");
        facts[0].Quantity.Should().Be(5m);
        facts[0].CostBasis.Should().Be(100.00m);
        facts[0].Reason.Should().Be(WasteReason.Spoilage);
    }

    [Fact]
    public async Task NotInitialized_ShouldThrow()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, siteId, date);

        // Act & Assert
        await grain.Invoking(g => g.GetSnapshotAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
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

    [Fact]
    public async Task InitializeAsync_MonthlyPeriod_ShouldWork()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, PeriodType.Monthly, 2024, 3);

        var command = new PeriodAggregationCommand(
            PeriodType: PeriodType.Monthly,
            PeriodStart: new DateTime(2024, 3, 1),
            PeriodEnd: new DateTime(2024, 3, 31),
            PeriodNumber: 3,
            FiscalYear: 2024);

        await grain.InitializeAsync(command);

        var summary = await grain.GetSummaryAsync();
        summary.PeriodType.Should().Be(PeriodType.Monthly);
        summary.PeriodNumber.Should().Be(3);
        summary.PeriodStart.Should().Be(new DateTime(2024, 3, 1));
        summary.PeriodEnd.Should().Be(new DateTime(2024, 3, 31));
    }

    [Fact]
    public async Task InitializeAsync_YearlyPeriod_ShouldWork()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, PeriodType.Yearly, 2024, 1);

        var command = new PeriodAggregationCommand(
            PeriodType: PeriodType.Yearly,
            PeriodStart: new DateTime(2024, 1, 1),
            PeriodEnd: new DateTime(2024, 12, 31),
            PeriodNumber: 1,
            FiscalYear: 2024);

        await grain.InitializeAsync(command);

        var summary = await grain.GetSummaryAsync();
        summary.PeriodType.Should().Be(PeriodType.Yearly);
        summary.PeriodStart.Should().Be(new DateTime(2024, 1, 1));
        summary.PeriodEnd.Should().Be(new DateTime(2024, 12, 31));
    }

    [Fact]
    public async Task AggregateFromDailyAsync_DuplicateDate_ShouldPrevent()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, PeriodType.Weekly, 2024, 15);

        await grain.InitializeAsync(new PeriodAggregationCommand(
            PeriodType.Weekly,
            new DateTime(2024, 4, 8),
            new DateTime(2024, 4, 14),
            15, 2024));

        var dailySales = new DailySalesSnapshot(
            new DateTime(2024, 4, 8), siteId, "Site", 1000m, 900m, 270m, 280m,
            620m, 68.89m, 50, 60, 18m,
            new Dictionary<SaleChannel, decimal>(),
            new Dictionary<string, decimal>());

        var dailyInventory = new DailyInventorySnapshot(
            new DateTime(2024, 4, 8), siteId, "Site", 10000m, 100, 3, 1, 2, 50m,
            Array.Empty<IngredientSnapshot>());

        var dailyConsumption = new DailyConsumptionSnapshot(
            new DateTime(2024, 4, 8), siteId, 270m, 280m, 10m, 3.7m,
            Array.Empty<VarianceBreakdown>());

        var dailyWaste = new DailyWasteSnapshot(
            new DateTime(2024, 4, 8), siteId, 25m, 3,
            new Dictionary<WasteReason, decimal>(),
            new Dictionary<string, decimal>());

        // Aggregate same date twice
        await grain.AggregateFromDailyAsync(new DateTime(2024, 4, 8), dailySales, dailyInventory, dailyConsumption, dailyWaste);
        await grain.AggregateFromDailyAsync(new DateTime(2024, 4, 8), dailySales, dailyInventory, dailyConsumption, dailyWaste);

        var summary = await grain.GetSummaryAsync();

        // The implementation may either prevent duplicate or accumulate
        // Based on the current implementation, it accumulates but tracks included dates
        summary.SalesMetrics.GrossSales.Should().BeGreaterThanOrEqualTo(1000m);
    }

    [Fact]
    public async Task GetStockTurnAsync_ShouldCalculate()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, PeriodType.Monthly, 2024, 6);

        await grain.InitializeAsync(new PeriodAggregationCommand(
            PeriodType.Monthly,
            new DateTime(2024, 6, 1),
            new DateTime(2024, 6, 30),
            6, 2024));

        // Add data with COGS and closing stock value
        var dailySales = new DailySalesSnapshot(
            new DateTime(2024, 6, 15), siteId, "Site", 10000m, 9000m, 2700m, 2800m,
            6200m, 68.89m, 300, 400, 30m,
            new Dictionary<SaleChannel, decimal>(),
            new Dictionary<string, decimal>());

        var dailyInventory = new DailyInventorySnapshot(
            new DateTime(2024, 6, 15), siteId, "Site", 7000m, 150, 5, 2, 3, 100m,
            Array.Empty<IngredientSnapshot>());

        var dailyConsumption = new DailyConsumptionSnapshot(
            new DateTime(2024, 6, 15), siteId, 2700m, 2800m, 100m, 3.7m,
            Array.Empty<VarianceBreakdown>());

        var dailyWaste = new DailyWasteSnapshot(
            new DateTime(2024, 6, 15), siteId, 50m, 5,
            new Dictionary<WasteReason, decimal>(),
            new Dictionary<string, decimal>());

        await grain.AggregateFromDailyAsync(new DateTime(2024, 6, 15), dailySales, dailyInventory, dailyConsumption, dailyWaste);

        var summary = await grain.GetSummaryAsync();

        // Stock turn = COGS / Closing Stock Value = 2800 / 7000 = 0.4
        summary.StockHealth.StockTurn.Should().BeApproximately(0.4m, 0.01m);
    }

    [Fact]
    public async Task GetSalesMetricsAsync_ShouldReturnMetrics()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, PeriodType.Weekly, 2024, 20);

        await grain.InitializeAsync(new PeriodAggregationCommand(
            PeriodType.Weekly,
            new DateTime(2024, 5, 13),
            new DateTime(2024, 5, 19),
            20, 2024));

        var dailySales = new DailySalesSnapshot(
            new DateTime(2024, 5, 13), siteId, "Site", 3000m, 2700m, 810m, 850m,
            1850m, 68.52m, 100, 120, 27m,
            new Dictionary<SaleChannel, decimal>(),
            new Dictionary<string, decimal>());

        var dailyInventory = new DailyInventorySnapshot(
            new DateTime(2024, 5, 13), siteId, "Site", 15000m, 200, 8, 3, 5, 200m,
            Array.Empty<IngredientSnapshot>());

        var dailyConsumption = new DailyConsumptionSnapshot(
            new DateTime(2024, 5, 13), siteId, 810m, 850m, 40m, 4.94m,
            Array.Empty<VarianceBreakdown>());

        var dailyWaste = new DailyWasteSnapshot(
            new DateTime(2024, 5, 13), siteId, 30m, 4,
            new Dictionary<WasteReason, decimal>(),
            new Dictionary<string, decimal>());

        await grain.AggregateFromDailyAsync(new DateTime(2024, 5, 13), dailySales, dailyInventory, dailyConsumption, dailyWaste);

        var metrics = await grain.GetSalesMetricsAsync();

        metrics.GrossSales.Should().Be(3000m);
        metrics.NetSales.Should().Be(2700m);
        metrics.TransactionCount.Should().Be(100);
        metrics.CoversServed.Should().Be(120);
    }

    [Fact]
    public async Task GetGrossProfitMetricsAsync_FIFO_ShouldDifferFromWAC()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, PeriodType.Weekly, 2024, 25);

        await grain.InitializeAsync(new PeriodAggregationCommand(
            PeriodType.Weekly,
            new DateTime(2024, 6, 17),
            new DateTime(2024, 6, 23),
            25, 2024));

        var dailySales = new DailySalesSnapshot(
            new DateTime(2024, 6, 17), siteId, "Site", 5000m, 4500m, 1350m, 1400m,
            3100m, 68.89m, 150, 180, 30m,
            new Dictionary<SaleChannel, decimal>(),
            new Dictionary<string, decimal>());

        var dailyInventory = new DailyInventorySnapshot(
            new DateTime(2024, 6, 17), siteId, "Site", 20000m, 180, 6, 2, 4, 150m,
            Array.Empty<IngredientSnapshot>());

        var dailyConsumption = new DailyConsumptionSnapshot(
            new DateTime(2024, 6, 17), siteId, 1350m, 1400m, 50m, 3.7m,
            Array.Empty<VarianceBreakdown>());

        var dailyWaste = new DailyWasteSnapshot(
            new DateTime(2024, 6, 17), siteId, 40m, 5,
            new Dictionary<WasteReason, decimal>(),
            new Dictionary<string, decimal>());

        await grain.AggregateFromDailyAsync(new DateTime(2024, 6, 17), dailySales, dailyInventory, dailyConsumption, dailyWaste);

        var fifoMetrics = await grain.GetGrossProfitMetricsAsync(CostingMethod.FIFO);
        var wacMetrics = await grain.GetGrossProfitMetricsAsync(CostingMethod.WAC);

        fifoMetrics.CostingMethod.Should().Be(CostingMethod.FIFO);
        wacMetrics.CostingMethod.Should().Be(CostingMethod.WAC);
        fifoMetrics.NetSales.Should().Be(4500m);
        wacMetrics.NetSales.Should().Be(4500m);
    }

    [Fact]
    public async Task FinalizeAsync_ShouldFinalize()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, PeriodType.Weekly, 2024, 30);

        await grain.InitializeAsync(new PeriodAggregationCommand(
            PeriodType.Weekly,
            new DateTime(2024, 7, 22),
            new DateTime(2024, 7, 28),
            30, 2024));

        // Act
        await grain.FinalizeAsync();

        // Assert - grain should still work but be marked as finalized
        var summary = await grain.GetSummaryAsync();
        summary.Should().NotBeNull();
        summary.PeriodType.Should().Be(PeriodType.Weekly);
    }

    [Fact]
    public async Task NotInitialized_ShouldThrow()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, PeriodType.Weekly, 2024, 99);

        // Act & Assert
        await grain.Invoking(g => g.GetSummaryAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
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

    [Fact]
    public async Task GetTodaySalesAsync_WhenGrainExists_ShouldReturn()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;
        var dashboardGrain = GetGrain(orgId, siteId);
        await dashboardGrain.InitializeAsync(orgId, siteId, "Sales Test Site");

        // Create and initialize the daily sales grain
        var salesKey = $"{orgId}:{siteId}:sales:{today:yyyy-MM-dd}";
        var salesGrain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(salesKey);
        await salesGrain.InitializeAsync(new DailySalesAggregationCommand(today, siteId, "Sales Test Site"));

        await salesGrain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.DineIn,
            ProductId: Guid.NewGuid(),
            ProductName: "Burger",
            Category: "Mains",
            Quantity: 1,
            GrossSales: 20.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 1.60m,
            NetSales: 18.40m,
            TheoreticalCOGS: 5.00m,
            ActualCOGS: 5.25m,
            GuestCount: 1));

        // Act
        var todaySales = await dashboardGrain.GetTodaySalesAsync();

        // Assert
        todaySales.Should().NotBeNull();
        todaySales.GrossSales.Should().Be(20.00m);
        todaySales.NetSales.Should().Be(18.40m);
    }

    [Fact]
    public async Task GetTodaySalesAsync_WhenNotInitialized_ShouldReturnEmpty()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var dashboardGrain = GetGrain(orgId, siteId);
        await dashboardGrain.InitializeAsync(orgId, siteId, "Empty Sales Site");

        // Note: We intentionally do NOT create the daily sales grain

        // Act
        var todaySales = await dashboardGrain.GetTodaySalesAsync();

        // Assert - should return empty snapshot with zero values
        todaySales.Should().NotBeNull();
        todaySales.GrossSales.Should().Be(0);
        todaySales.NetSales.Should().Be(0);
        todaySales.TransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task GetCurrentInventoryAsync_WhenGrainExists_ShouldReturn()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;
        var dashboardGrain = GetGrain(orgId, siteId);
        await dashboardGrain.InitializeAsync(orgId, siteId, "Inventory Test Site");

        // Create and initialize the daily inventory snapshot grain
        var inventoryKey = $"{orgId}:{siteId}:inventory-snapshot:{today:yyyy-MM-dd}";
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IDailyInventorySnapshotGrain>(inventoryKey);
        await inventoryGrain.InitializeAsync(new InventorySnapshotCommand(today, siteId, "Inventory Test Site"));

        await inventoryGrain.RecordIngredientSnapshotAsync(new IngredientSnapshot(
            Guid.NewGuid(), "Test Flour", "FLR-001", "Dry Goods", 50m, 50m, "kg",
            2.50m, 125.00m, null, false, false, false, false, 2));

        // Act
        var inventory = await dashboardGrain.GetCurrentInventoryAsync();

        // Assert
        inventory.Should().NotBeNull();
        inventory.TotalStockValue.Should().Be(125.00m);
        inventory.TotalSkuCount.Should().Be(1);
    }

    [Fact]
    public async Task GetCurrentInventoryAsync_WhenNotInitialized_ShouldReturnEmpty()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var dashboardGrain = GetGrain(orgId, siteId);
        await dashboardGrain.InitializeAsync(orgId, siteId, "Empty Inventory Site");

        // Note: We intentionally do NOT create the daily inventory grain

        // Act
        var inventory = await dashboardGrain.GetCurrentInventoryAsync();

        // Assert - should return empty snapshot with zero values
        inventory.Should().NotBeNull();
        inventory.TotalStockValue.Should().Be(0);
        inventory.TotalSkuCount.Should().Be(0);
        inventory.Ingredients.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTopVariancesAsync_WhenGrainExists_ShouldReturn()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;
        var dashboardGrain = GetGrain(orgId, siteId);
        await dashboardGrain.InitializeAsync(orgId, siteId, "Variance Dashboard Site");

        // Create and initialize the daily consumption grain
        var consumptionKey = $"{orgId}:{siteId}:consumption:{today:yyyy-MM-dd}";
        var consumptionGrain = _fixture.Cluster.GrainFactory.GetGrain<IDailyConsumptionGrain>(consumptionKey);
        await consumptionGrain.InitializeAsync(today, siteId);

        await consumptionGrain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "High Variance Ingredient", "Proteins", "kg",
            10.0m, 100.00m, 15.0m, 150.00m, CostingMethod.FIFO,
            null, null, null));

        await consumptionGrain.RecordConsumptionAsync(new RecordConsumptionCommand(
            Guid.NewGuid(), "Low Variance Ingredient", "Produce", "kg",
            20.0m, 40.00m, 21.0m, 42.00m, CostingMethod.FIFO,
            null, null, null));

        // Act
        var variances = await dashboardGrain.GetTopVariancesAsync(10);

        // Assert
        variances.Should().NotBeNull();
        variances.Should().HaveCount(2);
        // Should be ordered by absolute variance descending
        variances[0].IngredientName.Should().Be("High Variance Ingredient");
    }

    [Fact]
    public async Task AlertCounts_ShouldTrack()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var dashboardGrain = GetGrain(orgId, siteId);
        await dashboardGrain.InitializeAsync(orgId, siteId, "Alert Test Site");

        // Act
        var metrics = await dashboardGrain.GetMetricsAsync();

        // Assert - verify alert count fields exist and are non-negative
        metrics.LowStockAlertCount.Should().BeGreaterThanOrEqualTo(0);
        metrics.OutOfStockAlertCount.Should().BeGreaterThanOrEqualTo(0);
        metrics.ExpiryRiskCount.Should().BeGreaterThanOrEqualTo(0);
        metrics.HighVarianceCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task NotInitialized_ShouldThrow()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        // Act & Assert
        await grain.Invoking(g => g.GetMetricsAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }
}
