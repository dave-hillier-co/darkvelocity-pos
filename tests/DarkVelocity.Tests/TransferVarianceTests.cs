using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Transfer variance calculation tests covering:
/// - Variance calculation during receiving
/// - Partial transfer scenarios
/// - Over-delivery handling
/// - Multi-item transfer variances
/// - Cost reconciliation with variance
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TransferVarianceTests
{
    private readonly TestClusterFixture _fixture;

    public TransferVarianceTests(TestClusterFixture fixture)
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

    private IInventoryTransferGrain GetTransferGrain(Guid orgId, Guid transferId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IInventoryTransferGrain>(
            GrainKeys.InventoryTransfer(orgId, transferId));
    }

    // ============================================================================
    // Basic Variance Calculation Tests
    // ============================================================================

    // Given: A shipped inter-site transfer of 50 units of ground beef
    // When: The destination site receives only 45 units (5 damaged in transit)
    // Then: A negative variance of -5 is recorded against the shipped quantity
    [Fact]
    public async Task ReceiveWithVariance_ShortDelivery_ShouldCalculateNegativeVariance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Create source inventory
        var sourceInventory = await CreateInventoryAsync(orgId, sourceSiteId, ingredientId, "Ground Beef");
        await sourceInventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 10.00m));

        // Create destination inventory
        var destInventory = await CreateInventoryAsync(orgId, destSiteId, ingredientId, "Ground Beef");

        var transfer = GetTransferGrain(orgId, transferId);

        // Request transfer of 50 units
        await transfer.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId, $"TRN-{transferId.ToString()[..8]}", userId,
            Lines: [new TransferLineRequest(ingredientId, "Ground Beef", 50, "lb")]));

        await transfer.ApproveAsync(new ApproveTransferCommand(userId, "Approved for weekend"));
        await transfer.ShipAsync(new ShipTransferCommand(userId));

        // Act - receive only 45 (5 units short)
        await transfer.ReceiveItemAsync(new ReceiveTransferItemCommand(
            ingredientId,
            ReceivedQuantity: 45,
            ReceivedBy: userId,
            Condition: "Good",
            Notes: "5 units damaged in transit"));

        // Assert
        var variances = await transfer.GetVariancesAsync();
        variances.Should().HaveCount(1);
        variances[0].ShippedQuantity.Should().Be(50);
        variances[0].ReceivedQuantity.Should().Be(45);
        variances[0].Variance.Should().Be(-5);
    }

    // Given: A shipped inter-site transfer of 60 units of chicken wings
    // When: The destination site receives 65 units (extra units included)
    // Then: A positive variance of +5 is recorded against the shipped quantity
    [Fact]
    public async Task ReceiveWithVariance_OverDelivery_ShouldCalculatePositiveVariance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var sourceInventory = await CreateInventoryAsync(orgId, sourceSiteId, ingredientId, "Chicken Wings");
        await sourceInventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 200, 5.00m));

        var destInventory = await CreateInventoryAsync(orgId, destSiteId, ingredientId, "Chicken Wings");

        var transfer = GetTransferGrain(orgId, transferId);

        await transfer.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId, $"TRN-{transferId.ToString()[..8]}", userId,
            Lines: [new TransferLineRequest(ingredientId, "Chicken Wings", 60, "lb")]));

        await transfer.ApproveAsync(new ApproveTransferCommand(userId));
        await transfer.ShipAsync(new ShipTransferCommand(userId));

        // Act - receive 65 (5 more than expected)
        await transfer.ReceiveItemAsync(new ReceiveTransferItemCommand(
            ingredientId, 65, userId, "Good", "Extra units included"));

        // Assert
        var variances = await transfer.GetVariancesAsync();
        variances[0].ReceivedQuantity.Should().Be(65);
        variances[0].Variance.Should().Be(5);
    }

    // Given: A shipped inter-site transfer of 30 units
    // When: The destination site receives exactly 30 units
    // Then: Zero variance is recorded for the transfer line
    [Fact]
    public async Task ReceiveWithVariance_ExactQuantity_ShouldHaveZeroVariance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var sourceInventory = await CreateInventoryAsync(orgId, sourceSiteId, ingredientId);
        await sourceInventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 8.00m));

        var destInventory = await CreateInventoryAsync(orgId, destSiteId, ingredientId);

        var transfer = GetTransferGrain(orgId, transferId);

        await transfer.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId, $"TRN-{transferId.ToString()[..8]}", userId,
            Lines: [new TransferLineRequest(ingredientId, "Test Item", 30, "units")]));

        await transfer.ApproveAsync(new ApproveTransferCommand(userId));
        await transfer.ShipAsync(new ShipTransferCommand(userId));

        // Act
        await transfer.ReceiveItemAsync(new ReceiveTransferItemCommand(ingredientId, 30, userId));

        // Assert
        var variances = await transfer.GetVariancesAsync();
        variances[0].Variance.Should().Be(0);
    }

    // ============================================================================
    // Multi-Item Transfer Variance Tests
    // ============================================================================

    // Given: A shipped transfer containing three items (A: 30, B: 20, C: 25 units)
    // When: Items are received with mixed results (A: 28 short, B: 20 exact, C: 27 over)
    // Then: Variances are tracked per item: A at -2, B at 0, C at +2
    [Fact]
    public async Task MultiItemTransfer_MixedVariances_ShouldTrackEachItemSeparately()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var ingredient1 = Guid.NewGuid();
        var ingredient2 = Guid.NewGuid();
        var ingredient3 = Guid.NewGuid();

        var source1 = await CreateInventoryAsync(orgId, sourceSiteId, ingredient1, "Item A");
        var source2 = await CreateInventoryAsync(orgId, sourceSiteId, ingredient2, "Item B");
        var source3 = await CreateInventoryAsync(orgId, sourceSiteId, ingredient3, "Item C");

        await source1.ReceiveBatchAsync(new ReceiveBatchCommand("A1", 100, 5.00m));
        await source2.ReceiveBatchAsync(new ReceiveBatchCommand("B1", 100, 10.00m));
        await source3.ReceiveBatchAsync(new ReceiveBatchCommand("C1", 100, 15.00m));

        await CreateInventoryAsync(orgId, destSiteId, ingredient1, "Item A");
        await CreateInventoryAsync(orgId, destSiteId, ingredient2, "Item B");
        await CreateInventoryAsync(orgId, destSiteId, ingredient3, "Item C");

        var transfer = GetTransferGrain(orgId, transferId);

        await transfer.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId, $"TRN-{transferId.ToString()[..8]}", userId,
            Lines:
            [
                new TransferLineRequest(ingredient1, "Item A", 30, "units"),
                new TransferLineRequest(ingredient2, "Item B", 20, "units"),
                new TransferLineRequest(ingredient3, "Item C", 25, "units")
            ]));

        await transfer.ApproveAsync(new ApproveTransferCommand(userId));
        await transfer.ShipAsync(new ShipTransferCommand(userId));

        // Act - receive with different variances
        await transfer.ReceiveItemAsync(new ReceiveTransferItemCommand(ingredient1, 28, userId, "Good", "Minor shortage"));
        await transfer.ReceiveItemAsync(new ReceiveTransferItemCommand(ingredient2, 20, userId)); // Exact
        await transfer.ReceiveItemAsync(new ReceiveTransferItemCommand(ingredient3, 27, userId, "Good", "Bonus units"));

        // Assert
        var variances = await transfer.GetVariancesAsync();
        variances.Should().HaveCount(3);

        var itemA = variances.First(v => v.IngredientId == ingredient1);
        itemA.Variance.Should().Be(-2);

        var itemB = variances.First(v => v.IngredientId == ingredient2);
        itemB.Variance.Should().Be(0);

        var itemC = variances.First(v => v.IngredientId == ingredient3);
        itemC.Variance.Should().Be(2);
    }

    // ============================================================================
    // Transfer Completion with Variance Tests
    // ============================================================================

    // Given: A shipped two-item transfer with all items received (some with variance)
    // When: The transfer receipt is finalized
    // Then: The transfer status transitions to Received with a receipt timestamp
    [Fact]
    public async Task CompleteTransfer_AllItemsReceived_ShouldTransitionToReceived()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var ingredient1 = Guid.NewGuid();
        var ingredient2 = Guid.NewGuid();

        var source1 = await CreateInventoryAsync(orgId, sourceSiteId, ingredient1, "Item 1");
        var source2 = await CreateInventoryAsync(orgId, sourceSiteId, ingredient2, "Item 2");

        await source1.ReceiveBatchAsync(new ReceiveBatchCommand("B1", 100, 5.00m));
        await source2.ReceiveBatchAsync(new ReceiveBatchCommand("B2", 100, 10.00m));

        await CreateInventoryAsync(orgId, destSiteId, ingredient1, "Item 1");
        await CreateInventoryAsync(orgId, destSiteId, ingredient2, "Item 2");

        var transfer = GetTransferGrain(orgId, transferId);

        await transfer.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId, $"TRN-{transferId.ToString()[..8]}", userId,
            Lines:
            [
                new TransferLineRequest(ingredient1, "Item 1", 25, "units"),
                new TransferLineRequest(ingredient2, "Item 2", 30, "units")
            ]));

        await transfer.ApproveAsync(new ApproveTransferCommand(userId));
        await transfer.ShipAsync(new ShipTransferCommand(userId));

        // Act
        await transfer.ReceiveItemAsync(new ReceiveTransferItemCommand(ingredient1, 25, userId));
        await transfer.ReceiveItemAsync(new ReceiveTransferItemCommand(ingredient2, 28, userId, "Good", "2 short"));

        await transfer.FinalizeReceiptAsync(new FinalizeTransferReceiptCommand(userId));

        // Assert
        var state = await transfer.GetStateAsync();
        state.Status.Should().Be(TransferStatus.Received);
        state.ReceivedAt.Should().NotBeNull();
    }

    // Given: A shipped transfer with an expensive item ($50/unit) and a cheap item ($5/unit)
    // When: Both items are received 2 units short
    // Then: Variance values reflect unit cost: -$100 for the expensive item and -$10 for the cheap item
    [Fact]
    public async Task CompleteTransfer_WithVariances_ShouldCalculateTotalVarianceValue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var ingredient1 = Guid.NewGuid();
        var ingredient2 = Guid.NewGuid();

        var source1 = await CreateInventoryAsync(orgId, sourceSiteId, ingredient1, "Expensive Item");
        var source2 = await CreateInventoryAsync(orgId, sourceSiteId, ingredient2, "Cheap Item");

        await source1.ReceiveBatchAsync(new ReceiveBatchCommand("E1", 100, 50.00m));
        await source2.ReceiveBatchAsync(new ReceiveBatchCommand("C1", 100, 5.00m));

        await CreateInventoryAsync(orgId, destSiteId, ingredient1, "Expensive Item");
        await CreateInventoryAsync(orgId, destSiteId, ingredient2, "Cheap Item");

        var transfer = GetTransferGrain(orgId, transferId);

        await transfer.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId, $"TRN-{transferId.ToString()[..8]}", userId,
            Lines:
            [
                new TransferLineRequest(ingredient1, "Expensive Item", 10, "units"),
                new TransferLineRequest(ingredient2, "Cheap Item", 20, "units")
            ]));

        await transfer.ApproveAsync(new ApproveTransferCommand(userId));
        await transfer.ShipAsync(new ShipTransferCommand(userId));

        // Act - receive with variances
        await transfer.ReceiveItemAsync(new ReceiveTransferItemCommand(ingredient1, 8, userId, "Damaged", "2 units damaged"));
        await transfer.ReceiveItemAsync(new ReceiveTransferItemCommand(ingredient2, 18, userId, "Good", "2 units missing"));

        // Assert
        var variances = await transfer.GetVariancesAsync();

        // Expensive: -2 at $50 = -$100
        // Cheap: -2 at $5 = -$10
        var expensiveItem = variances.First(v => v.IngredientId == ingredient1);
        expensiveItem.Variance.Should().Be(-2);
        expensiveItem.VarianceValue.Should().Be(-100); // -2 * 50

        var cheapItem = variances.First(v => v.IngredientId == ingredient2);
        cheapItem.Variance.Should().Be(-2);
        cheapItem.VarianceValue.Should().Be(-10); // -2 * 5
    }

    // ============================================================================
    // Variance Percentage Tests
    // ============================================================================

    // Given: A shipped transfer of 100 units between two sites
    // When: 90 units are received at the destination (10% shortage)
    // Then: The variance percentage is calculated as -10%
    [Fact]
    public async Task Variance_ShouldCalculatePercentageCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var sourceInventory = await CreateInventoryAsync(orgId, sourceSiteId, ingredientId, "Test Item");
        await sourceInventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 200, 10.00m));

        await CreateInventoryAsync(orgId, destSiteId, ingredientId, "Test Item");

        var transfer = GetTransferGrain(orgId, transferId);

        await transfer.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId, $"TRN-{transferId.ToString()[..8]}", userId,
            Lines: [new TransferLineRequest(ingredientId, "Test Item", 100, "units")]));

        await transfer.ApproveAsync(new ApproveTransferCommand(userId));
        await transfer.ShipAsync(new ShipTransferCommand(userId));

        // Act - receive 90 (10% shortage)
        await transfer.ReceiveItemAsync(new ReceiveTransferItemCommand(ingredientId, 90, userId));

        // Assert
        var variances = await transfer.GetVariancesAsync();
        variances[0].Variance.Should().Be(-10);
        variances[0].VariancePercentage.Should().BeApproximately(-10, 0.01m); // -10%
    }

    // ============================================================================
    // Edge Cases
    // ============================================================================

    // Given: A shipped bulk transfer of 50,000 units at $0.10 each
    // When: 49,500 units are received (500 units short in transit)
    // Then: The variance of -500 is correctly calculated for the large quantity
    [Fact]
    public async Task Variance_LargeQuantity_ShouldCalculateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var sourceInventory = await CreateInventoryAsync(orgId, sourceSiteId, ingredientId, "Bulk Item");
        await sourceInventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100000, 0.10m));

        var destInventory = await CreateInventoryAsync(orgId, destSiteId, ingredientId, "Bulk Item");

        var transfer = GetTransferGrain(orgId, transferId);

        await transfer.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId, $"TRN-{transferId.ToString()[..8]}", userId,
            Lines: [new TransferLineRequest(ingredientId, "Bulk Item", 50000, "units")]));

        await transfer.ApproveAsync(new ApproveTransferCommand(userId));
        await transfer.ShipAsync(new ShipTransferCommand(userId));

        // Act - receive with 1% variance (500 units)
        await transfer.ReceiveItemAsync(new ReceiveTransferItemCommand(ingredientId, 49500, userId, "Good", "500 units short"));

        // Assert
        var variances = await transfer.GetVariancesAsync();
        variances[0].Variance.Should().Be(-500);
    }

    // Given: A shipped transfer that deducted 25 units from source inventory (100 to 75)
    // When: The transfer is cancelled with stock return to source enabled
    // Then: Source inventory is restored to 100 units and the transfer is marked Cancelled
    [Fact]
    public async Task Transfer_CancelAfterShipped_ShouldReturnStockToSource()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var sourceInventory = await CreateInventoryAsync(orgId, sourceSiteId, ingredientId);
        await sourceInventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        var transfer = GetTransferGrain(orgId, transferId);

        await transfer.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId, $"TRN-{transferId.ToString()[..8]}", userId,
            Lines: [new TransferLineRequest(ingredientId, "Test Item", 25, "units")]));

        await transfer.ApproveAsync(new ApproveTransferCommand(userId));
        await transfer.ShipAsync(new ShipTransferCommand(userId));

        // Verify stock was deducted
        var levelAfterShip = await sourceInventory.GetLevelInfoAsync();
        levelAfterShip.QuantityOnHand.Should().Be(75);

        // Act
        await transfer.CancelAsync(new CancelTransferCommand(userId, "Transfer cancelled", ReturnStockToSource: true));

        // Assert
        var state = await transfer.GetStateAsync();
        state.Status.Should().Be(TransferStatus.Cancelled);
        state.StockReturnedToSource.Should().BeTrue();

        // Verify stock was returned
        var levelAfterCancel = await sourceInventory.GetLevelInfoAsync();
        levelAfterCancel.QuantityOnHand.Should().Be(100); // Back to original
    }

    // Given: A shipped transfer of 30 units with 28 received (2 unit shortage)
    // When: The transfer summary is retrieved
    // Then: The summary reports a total variance of -2
    [Fact]
    public async Task GetSummary_ShouldIncludeTotalVariance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var sourceInventory = await CreateInventoryAsync(orgId, sourceSiteId, ingredientId);
        await sourceInventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        await CreateInventoryAsync(orgId, destSiteId, ingredientId);

        var transfer = GetTransferGrain(orgId, transferId);

        await transfer.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId, $"TRN-{transferId.ToString()[..8]}", userId,
            Lines: [new TransferLineRequest(ingredientId, "Test", 30, "units")]));

        await transfer.ApproveAsync(new ApproveTransferCommand(userId));
        await transfer.ShipAsync(new ShipTransferCommand(userId));
        await transfer.ReceiveItemAsync(new ReceiveTransferItemCommand(ingredientId, 28, userId));

        // Act
        var summary = await transfer.GetSummaryAsync();

        // Assert
        summary.Status.Should().NotBe(TransferStatus.Requested);
        summary.TotalVariance.Should().Be(-2);
    }
}
