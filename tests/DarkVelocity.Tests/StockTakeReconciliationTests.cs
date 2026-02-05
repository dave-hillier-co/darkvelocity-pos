using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Stock take reconciliation tests covering:
/// - Negative stock scenarios during stock take
/// - Large variance handling
/// - Multi-ingredient stock takes
/// - Stock take with ongoing operations
/// - Audit trail and approval flows
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class StockTakeReconciliationTests
{
    private readonly TestClusterFixture _fixture;

    public StockTakeReconciliationTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IInventoryGrain> CreateInventoryAsync(
        Guid orgId, Guid siteId, Guid ingredientId,
        string name = "Test Ingredient")
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));
        await grain.InitializeAsync(new InitializeInventoryCommand(
            orgId, siteId, ingredientId, name,
            $"SKU-{ingredientId.ToString()[..8]}", "units", "General", 10, 50));
        return grain;
    }

    // ============================================================================
    // Negative Stock Reconciliation Tests
    // ============================================================================

    [Fact]
    public async Task StockTake_SystemShowsNegative_PhysicalCountPositive_ShouldReconcile()
    {
        // Arrange - System went negative due to unrecorded transfers
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredientId, "Ground Beef");
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));

        // Create negative stock scenario
        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 50, 5.00m));
        await inventoryGrain.ConsumeAsync(new ConsumeStockCommand(80, "Weekend rush")); // Goes to -30

        var levelBefore = await inventoryGrain.GetLevelInfoAsync();
        levelBefore.QuantityOnHand.Should().Be(-30);

        // Start stock take
        var stockTake = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(
            GrainKeys.StockTake(orgId, siteId, stockTakeId));

        // Act
        await stockTake.StartAsync(new StartStockTakeCommand(
            orgId, siteId, "Emergency Count", userId,
            BlindCount: false,
            IngredientIds: [ingredientId]));

        // Physical count finds 40 units (unrecorded transfer came in)
        await stockTake.RecordCountAsync(new RecordCountCommand(ingredientId, 40, userId));

        // Assert
        var lineItems = await stockTake.GetLineItemsAsync();
        lineItems.Should().HaveCount(1);
        lineItems[0].TheoreticalQuantity.Should().Be(-30);
        lineItems[0].CountedQuantity.Should().Be(40);
        lineItems[0].Variance.Should().Be(70); // 40 - (-30) = 70 over
        lineItems[0].Severity.Should().Be(VarianceSeverity.Critical); // Large positive variance
    }

    [Fact]
    public async Task StockTake_SystemShowsNegative_PhysicalCountZero_ShouldReconcile()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredientId, "Olive Oil");
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));

        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 20, 15.00m));
        await inventoryGrain.ConsumeAsync(new ConsumeStockCommand(35, "High usage")); // Goes to -15

        var stockTake = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(
            GrainKeys.StockTake(orgId, siteId, stockTakeId));

        // Act
        await stockTake.StartAsync(new StartStockTakeCommand(
            orgId, siteId, "Count", userId, IngredientIds: [ingredientId]));
        await stockTake.RecordCountAsync(new RecordCountCommand(ingredientId, 0, userId));

        // Assert - reconciling to 0 from -15 is a positive variance
        var lineItems = await stockTake.GetLineItemsAsync();
        lineItems[0].Variance.Should().Be(15); // 0 - (-15) = 15
    }

    [Fact]
    public async Task StockTake_SystemShowsNegative_PhysicalCountStillNegative_ShouldReconcile()
    {
        // Arrange - Rare but possible: counted less than expected negative
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredientId, "Salmon Fillet");
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));

        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 30, 25.00m));
        await inventoryGrain.ConsumeAsync(new ConsumeStockCommand(80, "Event catering")); // -50

        var stockTake = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(
            GrainKeys.StockTake(orgId, siteId, stockTakeId));

        // Act - Physical count finds we're actually at -10 (transfers came in)
        await stockTake.StartAsync(new StartStockTakeCommand(
            orgId, siteId, "Count", userId, IngredientIds: [ingredientId]));

        // In a real scenario, you can't count negative. This would be entered as 0,
        // but let's test the theoretical count recording mechanism.
        // Record as 0 since you can't physically have negative items
        await stockTake.RecordCountAsync(new RecordCountCommand(ingredientId, 0, userId));

        // Assert
        var lineItems = await stockTake.GetLineItemsAsync();
        lineItems[0].TheoreticalQuantity.Should().Be(-50);
        lineItems[0].CountedQuantity.Should().Be(0);
        lineItems[0].Variance.Should().Be(50); // Positive variance (found more than expected)
    }

    // ============================================================================
    // Finalization with Adjustments Tests
    // ============================================================================

    [Fact]
    public async Task FinalizeAsync_WithNegativeTheoreticalAndPositiveCount_ShouldAdjustCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredientId);
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));

        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 40, 5.00m));
        await inventoryGrain.ConsumeAsync(new ConsumeStockCommand(60, "Production")); // -20

        var stockTake = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(
            GrainKeys.StockTake(orgId, siteId, stockTakeId));

        await stockTake.StartAsync(new StartStockTakeCommand(
            orgId, siteId, "End of Day", userId, IngredientIds: [ingredientId]));
        await stockTake.RecordCountAsync(new RecordCountCommand(ingredientId, 25, userId));
        await stockTake.SubmitForApprovalAsync(userId);

        // Act
        await stockTake.FinalizeAsync(new FinalizeStockTakeCommand(userId, ApplyAdjustments: true));

        // Assert
        var inventoryState = await inventoryGrain.GetStateAsync();
        inventoryState.QuantityOnHand.Should().Be(25);
    }

    [Fact]
    public async Task FinalizeAsync_WithoutApplyingAdjustments_ShouldNotChangeInventory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredientId);
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));

        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));
        await inventoryGrain.ConsumeAsync(new ConsumeStockCommand(20, "Production")); // 80

        var stockTake = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(
            GrainKeys.StockTake(orgId, siteId, stockTakeId));

        await stockTake.StartAsync(new StartStockTakeCommand(
            orgId, siteId, "Audit", userId, IngredientIds: [ingredientId]));
        await stockTake.RecordCountAsync(new RecordCountCommand(ingredientId, 75, userId));
        await stockTake.SubmitForApprovalAsync(userId);

        // Act - finalize WITHOUT applying adjustments
        await stockTake.FinalizeAsync(new FinalizeStockTakeCommand(userId, ApplyAdjustments: false));

        // Assert - inventory should remain unchanged
        var inventoryState = await inventoryGrain.GetStateAsync();
        inventoryState.QuantityOnHand.Should().Be(80); // Not adjusted to 75
    }

    // ============================================================================
    // Variance Severity Tests
    // ============================================================================

    [Fact]
    public async Task VarianceSeverity_SmallVariance_ShouldBeLow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredientId);
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));

        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        var stockTake = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(
            GrainKeys.StockTake(orgId, siteId, stockTakeId));

        // Act - 2% variance
        await stockTake.StartAsync(new StartStockTakeCommand(
            orgId, siteId, "Count", userId, IngredientIds: [ingredientId]));
        await stockTake.RecordCountAsync(new RecordCountCommand(ingredientId, 98, userId));

        // Assert
        var lineItems = await stockTake.GetLineItemsAsync();
        lineItems[0].Variance.Should().Be(-2);
        lineItems[0].Severity.Should().Be(VarianceSeverity.Low);
    }

    [Fact]
    public async Task VarianceSeverity_LargeVariance_ShouldBeCritical()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredientId);
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));

        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 50.00m));

        var stockTake = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(
            GrainKeys.StockTake(orgId, siteId, stockTakeId));

        // Act - 30% variance (large shortage)
        await stockTake.StartAsync(new StartStockTakeCommand(
            orgId, siteId, "Count", userId, IngredientIds: [ingredientId]));
        await stockTake.RecordCountAsync(new RecordCountCommand(ingredientId, 70, userId));

        // Assert
        var lineItems = await stockTake.GetLineItemsAsync();
        lineItems[0].Variance.Should().Be(-30);
        lineItems[0].Severity.Should().Be(VarianceSeverity.Critical);
    }

    // ============================================================================
    // Multi-Ingredient Stock Take Tests
    // ============================================================================

    [Fact]
    public async Task MultiIngredient_MixedVariances_ShouldTrackAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var ingredient1 = Guid.NewGuid();
        var ingredient2 = Guid.NewGuid();
        var ingredient3 = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredient1, "Flour");
        await CreateInventoryAsync(orgId, siteId, ingredient2, "Sugar");
        await CreateInventoryAsync(orgId, siteId, ingredient3, "Salt");

        var inv1 = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredient1));
        var inv2 = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredient2));
        var inv3 = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredient3));

        await inv1.ReceiveBatchAsync(new ReceiveBatchCommand("F1", 100, 2.00m));
        await inv2.ReceiveBatchAsync(new ReceiveBatchCommand("S1", 50, 3.00m));
        await inv3.ReceiveBatchAsync(new ReceiveBatchCommand("A1", 200, 0.50m));

        var stockTake = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(
            GrainKeys.StockTake(orgId, siteId, stockTakeId));

        // Act
        await stockTake.StartAsync(new StartStockTakeCommand(
            orgId, siteId, "Full Count", userId,
            IngredientIds: [ingredient1, ingredient2, ingredient3]));

        await stockTake.RecordCountAsync(new RecordCountCommand(ingredient1, 95, userId)); // -5
        await stockTake.RecordCountAsync(new RecordCountCommand(ingredient2, 55, userId)); // +5
        await stockTake.RecordCountAsync(new RecordCountCommand(ingredient3, 200, userId)); // 0

        // Assert
        var lineItems = await stockTake.GetLineItemsAsync();
        lineItems.Should().HaveCount(3);

        var report = await stockTake.GetVarianceReportAsync();
        report.TotalItems.Should().Be(3);
        report.ItemsCounted.Should().Be(3);
        report.ItemsWithVariance.Should().Be(2); // Flour and Sugar have variance

        // Flour: -5 * $2 = -$10
        // Sugar: +5 * $3 = +$15
        report.TotalNegativeVariance.Should().Be(10);
        report.TotalPositiveVariance.Should().Be(15);
    }

    [Fact]
    public async Task MultiIngredient_PartialCounts_ShouldTrackProgress()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var ingredient1 = Guid.NewGuid();
        var ingredient2 = Guid.NewGuid();
        var ingredient3 = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredient1, "Item1");
        await CreateInventoryAsync(orgId, siteId, ingredient2, "Item2");
        await CreateInventoryAsync(orgId, siteId, ingredient3, "Item3");

        var inv1 = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredient1));
        var inv2 = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredient2));
        var inv3 = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredient3));

        await inv1.ReceiveBatchAsync(new ReceiveBatchCommand("B1", 100, 5.00m));
        await inv2.ReceiveBatchAsync(new ReceiveBatchCommand("B2", 50, 10.00m));
        await inv3.ReceiveBatchAsync(new ReceiveBatchCommand("B3", 75, 8.00m));

        var stockTake = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(
            GrainKeys.StockTake(orgId, siteId, stockTakeId));

        await stockTake.StartAsync(new StartStockTakeCommand(
            orgId, siteId, "Count", userId,
            IngredientIds: [ingredient1, ingredient2, ingredient3]));

        // Act - only count 2 of 3 items
        await stockTake.RecordCountAsync(new RecordCountCommand(ingredient1, 100, userId));
        await stockTake.RecordCountAsync(new RecordCountCommand(ingredient2, 48, userId));

        // Assert
        var report = await stockTake.GetVarianceReportAsync();
        report.TotalItems.Should().Be(3);
        report.ItemsCounted.Should().Be(2);
    }

    // ============================================================================
    // Recount Tests
    // ============================================================================

    [Fact]
    public async Task Recount_ShouldOverridePreviousCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredientId);
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));

        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        var stockTake = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(
            GrainKeys.StockTake(orgId, siteId, stockTakeId));

        await stockTake.StartAsync(new StartStockTakeCommand(
            orgId, siteId, "Count", userId, IngredientIds: [ingredientId]));

        // First count
        await stockTake.RecordCountAsync(new RecordCountCommand(ingredientId, 90, userId));

        var lineItemsAfterFirst = await stockTake.GetLineItemsAsync();
        lineItemsAfterFirst[0].CountedQuantity.Should().Be(90);

        // Act - Recount
        await stockTake.RecordCountAsync(new RecordCountCommand(ingredientId, 95, userId));

        // Assert
        var lineItemsAfterRecount = await stockTake.GetLineItemsAsync();
        lineItemsAfterRecount.Should().HaveCount(1);
        lineItemsAfterRecount[0].CountedQuantity.Should().Be(95);
        lineItemsAfterRecount[0].Variance.Should().Be(-5);
    }

    // ============================================================================
    // Blind Count Tests
    // ============================================================================

    [Fact]
    public async Task BlindCount_ShouldHideTheoreticalUntilRevealed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredientId);
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));

        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        var stockTake = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(
            GrainKeys.StockTake(orgId, siteId, stockTakeId));

        // Act
        await stockTake.StartAsync(new StartStockTakeCommand(
            orgId, siteId, "Blind Audit", userId,
            BlindCount: true,
            IngredientIds: [ingredientId]));

        // Get items without revealing theoretical
        var blindItems = await stockTake.GetLineItemsAsync(includeTheoretical: false);
        blindItems[0].TheoreticalQuantity.Should().Be(0); // Hidden

        // Get items with theoretical revealed
        var revealedItems = await stockTake.GetLineItemsAsync(includeTheoretical: true);
        revealedItems[0].TheoreticalQuantity.Should().Be(100); // Revealed
    }

    // ============================================================================
    // Workflow State Tests
    // ============================================================================

    [Fact]
    public async Task Workflow_CannotRecordCountOnSubmittedStockTake()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredientId);
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));

        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        var stockTake = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(
            GrainKeys.StockTake(orgId, siteId, stockTakeId));

        await stockTake.StartAsync(new StartStockTakeCommand(
            orgId, siteId, "Count", userId, IngredientIds: [ingredientId]));
        await stockTake.RecordCountAsync(new RecordCountCommand(ingredientId, 95, userId));
        await stockTake.SubmitForApprovalAsync(userId);

        // Act & Assert
        var act = () => stockTake.RecordCountAsync(new RecordCountCommand(ingredientId, 90, userId));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Workflow_CannotFinalizeWithoutApproval()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stockTakeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredientId);
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));

        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        var stockTake = _fixture.Cluster.GrainFactory.GetGrain<IStockTakeGrain>(
            GrainKeys.StockTake(orgId, siteId, stockTakeId));

        await stockTake.StartAsync(new StartStockTakeCommand(
            orgId, siteId, "Count", userId, IngredientIds: [ingredientId]));
        await stockTake.RecordCountAsync(new RecordCountCommand(ingredientId, 95, userId));

        // Act - try to finalize without submitting for approval
        var act = () => stockTake.FinalizeAsync(new FinalizeStockTakeCommand(userId, ApplyAdjustments: true));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
