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

    // Given: An ingredient with negative system stock of -30 units due to consumption exceeding recorded receipts
    // When: A physical count records 40 units actually on hand (unrecorded transfer arrived)
    // Then: A critical positive variance of +70 is calculated (40 minus -30)
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

    // Given: Olive oil inventory at -15 units due to consumption exceeding recorded receipts
    // When: A physical count records zero units on the shelf
    // Then: A positive variance of +15 is calculated (reconciling from -15 to 0)
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

    // Given: Salmon fillet inventory at -50 units from event catering consumption
    // When: A physical count records zero units (cannot physically count negative stock)
    // Then: A positive variance of +50 is calculated (reconciling from -50 to 0)
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

    // Given: An ingredient at -20 units with a physical count of 25, submitted for approval
    // When: The stock take is finalized with adjustments applied
    // Then: The inventory is adjusted from -20 to the counted quantity of 25
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

    // Given: Inventory at 80 units with a stock take counting 75, submitted for approval
    // When: The stock take is finalized without applying adjustments
    // Then: The inventory remains at 80 units, unchanged by the stock take
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

    // Given: An ingredient with 100 units on hand during a stock take
    // When: A physical count of 98 is recorded (2% shortage)
    // Then: The variance severity is classified as Low
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

    // Given: An ingredient with 100 high-value units ($50 each) during a stock take
    // When: A physical count of 70 is recorded (30% shortage)
    // Then: The variance severity is classified as Critical
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

    // Given: Three ingredients (flour, sugar, salt) with known stock levels in a single stock take
    // When: Physical counts are recorded showing -5 flour, +5 sugar, and 0 salt variance
    // Then: The variance report shows $10 negative and $15 positive variance across 2 items with discrepancies
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

    // Given: Three ingredients included in a stock take
    // When: Only two of the three ingredients are physically counted
    // Then: The variance report shows 3 total items but only 2 counted
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

    // Given: A stock take with an initial physical count of 90 recorded for an ingredient
    // When: A recount of 95 is recorded for the same ingredient
    // Then: The recount replaces the original, showing 95 counted with a variance of -5
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

    // Given: A stock take started in blind count mode for an ingredient with 100 units on hand
    // When: Line items are retrieved first without and then with theoretical quantities revealed
    // Then: Theoretical is hidden (0) when not revealed and shows the actual 100 when revealed
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

    // Given: A stock take that has been submitted for approval with counts already recorded
    // When: An attempt is made to record additional counts after submission
    // Then: The operation is rejected because the stock take is no longer in counting phase
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

    // Given: A stock take with counts recorded but not yet submitted for approval
    // When: An attempt is made to finalize the stock take directly
    // Then: The operation is rejected because the approval workflow step was skipped
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
