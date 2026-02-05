using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class StockTakeGrainTests
{
    private readonly TestClusterFixture _fixture;

    public StockTakeGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IInventoryGrain> CreateInventoryAsync(Guid orgId, Guid siteId, Guid ingredientId, string name = "Test Ingredient")
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
        await grain.InitializeAsync(new InitializeInventoryCommand(orgId, siteId, ingredientId, name, $"SKU-{ingredientId.ToString()[..8]}", "units", "General", 10, 50));
        return grain;
    }

    [Fact]
    public async Task StartAsync_ShouldInitializeStockTake()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var ingredientId = Guid.NewGuid();
        await CreateInventoryAsync(orgId, siteId, ingredientId, "Ground Beef");
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(GrainKeys.StockTake(orgId, siteId, stockTakeId));

        // Act
        await grain.StartAsync(new StartStockTakeCommand(
            orgId, siteId, "Monthly Count", userId,
            BlindCount: false,
            IngredientIds: [ingredientId]));

        // Assert
        var state = await grain.GetStateAsync();
        state.Name.Should().Be("Monthly Count");
        state.Status.Should().Be(StockTakeStatus.InProgress);
        state.BlindCount.Should().BeFalse();
        state.StartedBy.Should().Be(userId);
        state.LineItems.Should().HaveCount(1);
        state.LineItems[0].TheoreticalQuantity.Should().Be(100);
    }

    [Fact]
    public async Task RecordCountAsync_ShouldCalculateVariance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var ingredientId = Guid.NewGuid();
        await CreateInventoryAsync(orgId, siteId, ingredientId, "Ground Beef");
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(GrainKeys.StockTake(orgId, siteId, stockTakeId));
        await grain.StartAsync(new StartStockTakeCommand(orgId, siteId, "Monthly Count", userId, IngredientIds: [ingredientId]));

        // Act
        await grain.RecordCountAsync(new RecordCountCommand(ingredientId, 95, userId));

        // Assert
        var lineItems = await grain.GetLineItemsAsync();
        lineItems.Should().HaveCount(1);
        lineItems[0].CountedQuantity.Should().Be(95);
        lineItems[0].Variance.Should().Be(-5); // 95 - 100 = -5
        lineItems[0].VarianceValue.Should().Be(-25); // -5 * 5.00 = -25
        lineItems[0].Severity.Should().Be(VarianceSeverity.Medium); // 5% variance
    }

    [Fact]
    public async Task BlindCount_ShouldHideTheoreticalQuantities()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var ingredientId = Guid.NewGuid();
        await CreateInventoryAsync(orgId, siteId, ingredientId);
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(GrainKeys.StockTake(orgId, siteId, stockTakeId));
        await grain.StartAsync(new StartStockTakeCommand(
            orgId, siteId, "Blind Count", userId,
            BlindCount: true,
            IngredientIds: [ingredientId]));

        // Act
        var lineItems = await grain.GetLineItemsAsync(includeTheoretical: false);

        // Assert
        lineItems.Should().HaveCount(1);
        lineItems[0].TheoreticalQuantity.Should().Be(0); // Hidden in blind mode
    }

    [Fact]
    public async Task FinalizeAsync_ShouldApplyAdjustments()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var ingredientId = Guid.NewGuid();
        await CreateInventoryAsync(orgId, siteId, ingredientId);
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(GrainKeys.StockTake(orgId, siteId, stockTakeId));
        await grain.StartAsync(new StartStockTakeCommand(orgId, siteId, "Monthly Count", userId, IngredientIds: [ingredientId]));
        await grain.RecordCountAsync(new RecordCountCommand(ingredientId, 85, userId));
        await grain.SubmitForApprovalAsync(userId);

        // Act
        await grain.FinalizeAsync(new FinalizeStockTakeCommand(userId, ApplyAdjustments: true));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(StockTakeStatus.Finalized);
        state.AdjustmentsApplied.Should().BeTrue();

        // Verify inventory was adjusted
        var inventoryState = await inventoryGrain.GetStateAsync();
        inventoryState.QuantityOnHand.Should().Be(85);
    }

    [Fact]
    public async Task GetVarianceReportAsync_ShouldReturnCorrectStatistics()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var ingredient1 = Guid.NewGuid();
        var ingredient2 = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredient1, "Item 1");
        await CreateInventoryAsync(orgId, siteId, ingredient2, "Item 2");

        var inv1 = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredient1));
        var inv2 = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredient2));
        await inv1.ReceiveBatchAsync(new ReceiveBatchCommand("B1", 100, 10.00m));
        await inv2.ReceiveBatchAsync(new ReceiveBatchCommand("B2", 50, 20.00m));

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(GrainKeys.StockTake(orgId, siteId, stockTakeId));
        await grain.StartAsync(new StartStockTakeCommand(orgId, siteId, "Count", userId, IngredientIds: [ingredient1, ingredient2]));
        await grain.RecordCountAsync(new RecordCountCommand(ingredient1, 90, userId)); // -10 variance, -100 value
        await grain.RecordCountAsync(new RecordCountCommand(ingredient2, 55, userId)); // +5 variance, +100 value

        // Act
        var report = await grain.GetVarianceReportAsync();

        // Assert
        report.TotalItems.Should().Be(2);
        report.ItemsCounted.Should().Be(2);
        report.ItemsWithVariance.Should().Be(2);
        report.TotalPositiveVariance.Should().Be(100); // +5 * 20
        report.TotalNegativeVariance.Should().Be(100); // -10 * 10
    }

    [Fact]
    public async Task CancelAsync_ShouldPreventFurtherOperations()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var ingredientId = Guid.NewGuid();
        await CreateInventoryAsync(orgId, siteId, ingredientId);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(GrainKeys.StockTake(orgId, siteId, stockTakeId));
        await grain.StartAsync(new StartStockTakeCommand(orgId, siteId, "Count", userId, IngredientIds: [ingredientId]));

        // Act
        await grain.CancelAsync(userId, "Testing cancellation");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(StockTakeStatus.Cancelled);
        state.CancellationReason.Should().Be("Testing cancellation");

        // Should not be able to record counts on cancelled stock take
        var act = () => grain.RecordCountAsync(new RecordCountCommand(ingredientId, 50, userId));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
