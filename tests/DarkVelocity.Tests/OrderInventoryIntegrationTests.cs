using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Integration tests for Order and Inventory grain interactions.
/// Tests the flow of inventory consumption when orders are processed.
/// </summary>
[Collection(ClusterCollection.Name)]
public class OrderInventoryIntegrationTests
{
    private readonly TestClusterFixture _fixture;

    public OrderInventoryIntegrationTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // ============================================================================
    // Inventory Consumption Tests
    // ============================================================================

    [Fact]
    public async Task Inventory_ConsumeForOrder_ShouldDeductStock()
    {
        // Arrange
        var (inventory, _) = await CreateInventoryWithStockAsync(100m);
        var orderId = Guid.NewGuid();

        // Act
        var result = await inventory.ConsumeForOrderAsync(orderId, 10m, Guid.NewGuid());

        // Assert
        result.QuantityConsumed.Should().Be(10m);
        result.TotalCost.Should().BeGreaterThan(0);

        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(90m);
    }

    [Fact]
    public async Task Inventory_ConsumeForOrder_WithInsufficientStock_ShouldThrow()
    {
        // Arrange
        var (inventory, _) = await CreateInventoryWithStockAsync(5m);
        var orderId = Guid.NewGuid();

        // Act
        var act = () => inventory.ConsumeForOrderAsync(orderId, 10m, Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient stock*");
    }

    [Fact]
    public async Task Inventory_ConsumeForOrder_WithExactStock_ShouldSucceed()
    {
        // Arrange
        var (inventory, _) = await CreateInventoryWithStockAsync(25m);
        var orderId = Guid.NewGuid();

        // Act
        var result = await inventory.ConsumeForOrderAsync(orderId, 25m, Guid.NewGuid());

        // Assert
        result.QuantityConsumed.Should().Be(25m);

        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(0m);
        levelInfo.Level.Should().Be(StockLevel.OutOfStock);
    }

    [Fact]
    public async Task Inventory_ConsumeForOrder_ShouldTrackOrderId()
    {
        // Arrange
        var (inventory, _) = await CreateInventoryWithStockAsync(100m);
        var orderId = Guid.NewGuid();

        // Act
        await inventory.ConsumeForOrderAsync(orderId, 10m, Guid.NewGuid());

        // Assert
        var state = await inventory.GetStateAsync();
        state.Movements.Should().Contain(m => m.OrderId == orderId);
    }

    [Fact]
    public async Task Inventory_ConsumeForOrder_MultipleTimes_ShouldAccumulateDeductions()
    {
        // Arrange
        var (inventory, _) = await CreateInventoryWithStockAsync(100m);

        // Act
        await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 10m, Guid.NewGuid());
        await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 20m, Guid.NewGuid());
        await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 15m, Guid.NewGuid());

        // Assert
        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(55m); // 100 - 10 - 20 - 15
    }

    // ============================================================================
    // Stock Level Alert Tests
    // ============================================================================

    [Fact]
    public async Task Inventory_ConsumeForOrder_BelowReorderPoint_ShouldTriggerLowStock()
    {
        // Arrange
        var (inventory, _) = await CreateInventoryWithStockAsync(50m, reorderPoint: 20m);

        // Act
        await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 35m, Guid.NewGuid());

        // Assert
        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(15m);
        levelInfo.Level.Should().Be(StockLevel.Low);
    }

    [Fact]
    public async Task Inventory_HasSufficientStock_WhenSufficient_ShouldReturnTrue()
    {
        // Arrange
        var (inventory, _) = await CreateInventoryWithStockAsync(100m);

        // Act
        var hasSufficient = await inventory.HasSufficientStockAsync(50m);

        // Assert
        hasSufficient.Should().BeTrue();
    }

    [Fact]
    public async Task Inventory_HasSufficientStock_WhenInsufficient_ShouldReturnFalse()
    {
        // Arrange
        var (inventory, _) = await CreateInventoryWithStockAsync(10m);

        // Act
        var hasSufficient = await inventory.HasSufficientStockAsync(50m);

        // Assert
        hasSufficient.Should().BeFalse();
    }

    // ============================================================================
    // FIFO Consumption Tests
    // ============================================================================

    [Fact]
    public async Task Inventory_ConsumeForOrder_ShouldUseFifo()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        // Add batches with different costs (FIFO should use oldest first)
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand(
            "BATCH-001", 20m, 1.00m)); // Oldest - cost $1.00
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand(
            "BATCH-002", 20m, 2.00m)); // Newer - cost $2.00
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand(
            "BATCH-003", 20m, 3.00m)); // Newest - cost $3.00

        // Act - consume 25 units (should take all 20 from first batch + 5 from second)
        var result = await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 25m, Guid.NewGuid());

        // Assert
        result.QuantityConsumed.Should().Be(25m);
        result.BatchBreakdown.Should().HaveCount(2);

        // First batch should be fully consumed
        var firstBatchConsumption = result.BatchBreakdown.First(b => b.BatchNumber == "BATCH-001");
        firstBatchConsumption.Quantity.Should().Be(20m);
        firstBatchConsumption.UnitCost.Should().Be(1.00m);

        // Second batch should have 5 consumed
        var secondBatchConsumption = result.BatchBreakdown.First(b => b.BatchNumber == "BATCH-002");
        secondBatchConsumption.Quantity.Should().Be(5m);
        secondBatchConsumption.UnitCost.Should().Be(2.00m);

        // Total cost: (20 * 1.00) + (5 * 2.00) = 30
        result.TotalCost.Should().Be(30m);
    }

    [Fact]
    public async Task Inventory_ConsumeForOrder_ShouldExhaustBatchesCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH-A", 10m, 5.00m));
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH-B", 10m, 6.00m));

        // Act - consume exactly 10 (should exhaust first batch)
        await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 10m, Guid.NewGuid());

        // Get active batches
        var activeBatches = await inventory.GetActiveBatchesAsync();

        // Assert - only second batch should remain
        activeBatches.Should().HaveCount(1);
        activeBatches[0].BatchNumber.Should().Be("BATCH-B");
        activeBatches[0].RemainingQuantity.Should().Be(10m);
    }

    // ============================================================================
    // Reversal Tests
    // ============================================================================

    [Fact]
    public async Task Inventory_ReverseConsumption_ShouldRestoreStock()
    {
        // Arrange
        var (inventory, _) = await CreateInventoryWithStockAsync(100m);
        var orderId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();

        await inventory.ConsumeForOrderAsync(orderId, 20m, performedBy);

        var levelAfterConsumption = await inventory.GetLevelInfoAsync();
        levelAfterConsumption.QuantityAvailable.Should().Be(80m);

        // Get the movement ID
        var state = await inventory.GetStateAsync();
        var consumptionMovement = state.Movements.First(m => m.OrderId == orderId);

        // Act
        await inventory.ReverseConsumptionAsync(consumptionMovement.MovementId, "Order voided", performedBy);

        // Assert
        var levelAfterReversal = await inventory.GetLevelInfoAsync();
        levelAfterReversal.QuantityAvailable.Should().Be(100m);
    }

    // ============================================================================
    // Order Close Integration Tests
    // ============================================================================

    [Fact]
    public async Task Order_WithInventoryItems_ShouldConsumeOnSend()
    {
        // This test verifies the conceptual flow:
        // 1. Create order with line items
        // 2. Each line item should map to menu items with recipes
        // 3. When order is sent, inventory should be consumed

        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        // Create inventory for an ingredient
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId, "Ground Beef", "lb");
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BEEF-001", 50m, 5.00m));

        // Create recipe that uses 0.5 lb of ground beef
        var recipeId = Guid.NewGuid();
        var recipe = _fixture.Cluster.GrainFactory.GetGrain<IRecipeGrain>(
            $"{orgId}:recipe:{recipeId}");

        await recipe.CreateAsync(new CreateRecipeCommand(
            orgId, "Hamburger", RecipeCategory.MainCourse, 1m, "serving"));

        await recipe.AddIngredientAsync(new RecipeIngredientCommand(
            ingredientId, "Ground Beef", 0.5m, "lb", 2.50m, true, null));

        // Create order
        var orderId = Guid.NewGuid();
        var order = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(orgId, siteId, orderId));

        await order.CreateAsync(new CreateOrderCommand(orgId, siteId, Guid.NewGuid(), OrderType.DineIn, GuestCount: 1));

        var menuItemId = Guid.NewGuid();
        await order.AddLineAsync(new AddLineCommand(
            menuItemId, "Hamburger", 2, 12.00m)); // 2 hamburgers

        // Act - Simulate inventory consumption for order
        // In real flow, this happens via OrderEventSubscriber when order is sent
        // Here we directly test the inventory grain behavior

        // 2 hamburgers * 0.5 lb each = 1 lb
        var consumptionResult = await inventory.ConsumeForOrderAsync(orderId, 1m, Guid.NewGuid());

        // Assert
        consumptionResult.QuantityConsumed.Should().Be(1m);

        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(49m);
    }

    [Fact]
    public async Task Order_Void_ShouldAllowInventoryReversal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH-001", 100m, 2.00m));

        var orderId = Guid.NewGuid();
        var order = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(orgId, siteId, orderId));

        await order.CreateAsync(new CreateOrderCommand(orgId, siteId, Guid.NewGuid(), OrderType.DineIn, GuestCount: 1));
        await order.AddLineAsync(new AddLineCommand(
            Guid.NewGuid(), "Burger", 1, 10.00m));

        // Consume inventory for order
        await inventory.ConsumeForOrderAsync(orderId, 5m, Guid.NewGuid());

        var levelAfterOrder = await inventory.GetLevelInfoAsync();
        levelAfterOrder.QuantityAvailable.Should().Be(95m);

        // Act - Void the order
        await order.VoidAsync(new VoidOrderCommand(Guid.NewGuid(), "Customer cancelled"));

        // Get movement for reversal
        var state = await inventory.GetStateAsync();
        var movement = state.Movements.First(m => m.OrderId == orderId);

        // Reverse the inventory consumption (in real flow, this happens via event handler)
        await inventory.ReverseConsumptionAsync(movement.MovementId, "Order voided", Guid.NewGuid());

        // Assert
        var levelAfterVoid = await inventory.GetLevelInfoAsync();
        levelAfterVoid.QuantityAvailable.Should().Be(100m);

        var orderState = await order.GetStateAsync();
        orderState.Status.Should().Be(OrderStatus.Voided);
    }

    // ============================================================================
    // Waste Recording Tests
    // ============================================================================

    [Fact]
    public async Task Inventory_RecordWaste_ShouldDeductStock()
    {
        // Arrange
        var (inventory, _) = await CreateInventoryWithStockAsync(50m);

        // Act
        await inventory.RecordWasteAsync(new RecordWasteCommand(
            5m, "Spoiled", "Spoilage", Guid.NewGuid()));

        // Assert
        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(45m);
    }

    [Fact]
    public async Task Inventory_RecordWaste_WithInsufficientStock_ShouldThrow()
    {
        // Arrange
        var (inventory, _) = await CreateInventoryWithStockAsync(5m);

        // Act
        var act = () => inventory.RecordWasteAsync(new RecordWasteCommand(
            10m, "Spoiled", "Spoilage", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient*");
    }

    // ============================================================================
    // Adjustment Tests
    // ============================================================================

    [Fact]
    public async Task Inventory_AdjustQuantity_ShouldUpdateStock()
    {
        // Arrange
        var (inventory, _) = await CreateInventoryWithStockAsync(50m);

        // Act - adjust down
        await inventory.AdjustQuantityAsync(new AdjustQuantityCommand(
            40m, "Physical count variance", Guid.NewGuid()));

        // Assert
        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(40m);
    }

    [Fact]
    public async Task Inventory_AdjustQuantity_CanIncreaseStock()
    {
        // Arrange
        var (inventory, _) = await CreateInventoryWithStockAsync(50m);

        // Act - adjust up (found extra stock during physical count)
        await inventory.AdjustQuantityAsync(new AdjustQuantityCommand(
            60m, "Found extra during count", Guid.NewGuid()));

        // Assert
        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityOnHand.Should().Be(60m);
    }

    // ============================================================================
    // Transfer Tests
    // ============================================================================

    [Fact]
    public async Task Inventory_TransferOut_ShouldDeductFromSource()
    {
        // Arrange
        var (sourceInventory, _) = await CreateInventoryWithStockAsync(100m);
        var destinationSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();

        // Act
        await sourceInventory.TransferOutAsync(new TransferOutCommand(
            25m, destinationSiteId, transferId, Guid.NewGuid()));

        // Assert
        var levelInfo = await sourceInventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(75m);
    }

    [Fact]
    public async Task Inventory_TransferOut_WithInsufficientStock_ShouldThrow()
    {
        // Arrange
        var (sourceInventory, _) = await CreateInventoryWithStockAsync(10m);

        // Act
        var act = () => sourceInventory.TransferOutAsync(new TransferOutCommand(
            50m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient*");
    }

    [Fact]
    public async Task Inventory_ReceiveTransfer_ShouldAddToDestination()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var destinationSiteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();

        var destinationInventory = await CreateInventoryAsync(orgId, destinationSiteId, ingredientId);

        // Act
        await destinationInventory.ReceiveTransferAsync(new ReceiveTransferCommand(
            25m, 2.50m, sourceSiteId, transferId, "TRANSFER-001"));

        // Assert
        var levelInfo = await destinationInventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(25m);
    }

    // ============================================================================
    // Expiry Tests
    // ============================================================================

    [Fact]
    public async Task Inventory_WriteOffExpiredBatches_ShouldRemoveExpiredStock()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        // Add expired batch
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand(
            "EXPIRED-001", 20m, 5.00m, DateTime.UtcNow.AddDays(-1))); // Already expired

        // Add valid batch
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand(
            "VALID-001", 30m, 5.00m, DateTime.UtcNow.AddDays(30))); // Expires in 30 days

        var levelBefore = await inventory.GetLevelInfoAsync();
        levelBefore.QuantityAvailable.Should().Be(50m);

        // Act
        await inventory.WriteOffExpiredBatchesAsync(Guid.NewGuid());

        // Assert
        var levelAfter = await inventory.GetLevelInfoAsync();
        levelAfter.QuantityAvailable.Should().Be(30m);

        var activeBatches = await inventory.GetActiveBatchesAsync();
        activeBatches.Should().HaveCount(1);
        activeBatches[0].BatchNumber.Should().Be("VALID-001");
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

    private async Task<IInventoryGrain> CreateInventoryAsync(
        Guid orgId, Guid siteId, Guid ingredientId,
        string name = "Test Ingredient", string unit = "kg")
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));

        await grain.InitializeAsync(new InitializeInventoryCommand(
            orgId, siteId, ingredientId, name, $"SKU-{ingredientId.ToString()[..8]}", unit, "Test Category"));

        return grain;
    }

    private async Task<(IInventoryGrain Grain, Guid IngredientId)> CreateInventoryWithStockAsync(
        decimal quantity, decimal reorderPoint = 10m, decimal parLevel = 50m)
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));

        await grain.InitializeAsync(new InitializeInventoryCommand(
            orgId, siteId, ingredientId, "Test Ingredient", "SKU-001", "kg", "Test Category",
            reorderPoint, parLevel));

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand(
            "INITIAL-BATCH", quantity, 2.50m));

        return (grain, ingredientId);
    }
}
