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
[Trait("Category", "Integration")]
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

    // Given: An inventory item with 100 units of stock
    // When: 10 units are consumed for an order
    // Then: Available stock is reduced to 90 units with a non-zero cost recorded
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

    // Given: An inventory item with only 5 units of stock
    // When: 10 units are consumed for an order exceeding available stock
    // Then: Stock goes negative to -5, flagging a discrepancy for reconciliation
    [Fact]
    public async Task Inventory_ConsumeForOrder_WithInsufficientStock_ShouldAllowNegative()
    {
        // Arrange - Per design: "Negative stock is the default"
        var (inventory, _) = await CreateInventoryWithStockAsync(5m);
        var orderId = Guid.NewGuid();

        // Act - Consume more than available (5 - 10 = -5)
        var result = await inventory.ConsumeForOrderAsync(orderId, 10m, Guid.NewGuid());

        // Assert - Stock goes negative, flagging a discrepancy for reconciliation
        result.QuantityConsumed.Should().Be(10m);
        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityOnHand.Should().Be(-5m);
    }

    // Given: An inventory item with exactly 25 units of stock
    // When: All 25 units are consumed for an order
    // Then: Available stock reaches zero and level is marked as out of stock
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

    // Given: An inventory item with 100 units of stock
    // When: Stock is consumed for a specific order
    // Then: The inventory movement records the order ID as a reference
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
        state.RecentMovements.Should().Contain(m => m.ReferenceId == orderId);
    }

    // Given: An inventory item with 100 units of stock
    // When: Three separate orders consume 10, 20, and 15 units respectively
    // Then: Available stock is reduced to 55 units reflecting all deductions
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

    // Given: An inventory item with 50 units and a reorder point of 20
    // When: 35 units are consumed dropping stock below the reorder point
    // Then: Stock level is flagged as low to trigger replenishment
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

    // Given: An inventory item with 100 units of stock
    // When: A stock sufficiency check is performed for 50 units
    // Then: The check confirms sufficient stock is available
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

    // Given: An inventory item with only 10 units of stock
    // When: A stock sufficiency check is performed for 50 units
    // Then: The check reports insufficient stock
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

    // Given: Three inventory batches received at different costs ($1, $2, $3 per unit)
    // When: 25 units are consumed for an order
    // Then: FIFO costing draws all 20 from the oldest batch and 5 from the next
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

    // Given: Two inventory batches of 10 units each at different costs
    // When: Exactly 10 units are consumed exhausting the first batch
    // Then: Only the second batch remains active with its full quantity
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
        activeBatches[0].Quantity.Should().Be(10m);
    }

    // ============================================================================
    // Reversal Tests
    // ============================================================================

    // Given: An inventory item with stock reduced by a 20-unit order consumption
    // When: The consumption movement is reversed
    // Then: Stock is fully restored to its original level
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
        var consumptionMovement = state.RecentMovements.First(m => m.ReferenceId == orderId);

        // Act
        await inventory.ReverseConsumptionAsync(consumptionMovement.Id, "Order voided", performedBy);

        // Assert
        var levelAfterReversal = await inventory.GetLevelInfoAsync();
        levelAfterReversal.QuantityAvailable.Should().Be(100m);
    }

    // ============================================================================
    // Order Close Integration Tests
    // ============================================================================

    // Given: A recipe-linked inventory item (ground beef) and a dine-in order with 2 hamburgers
    // When: Inventory is consumed based on the recipe quantities for the order
    // Then: Stock is reduced by the total recipe requirement (1 lb for 2 burgers)
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
        var menuItemId = Guid.NewGuid();
        var recipe = _fixture.Cluster.GrainFactory.GetGrain<IRecipeGrain>(
            $"{orgId}:recipe:{recipeId}");

        await recipe.CreateAsync(new CreateRecipeCommand(
            menuItemId, "Hamburger", "BURG-001", null, null, "Hamburger with ground beef", 1, null));

        await recipe.AddIngredientAsync(new RecipeIngredientCommand(
            ingredientId, "Ground Beef", 0.5m, "lb", 0m, 2.50m));

        // Create order
        var orderId = Guid.NewGuid();
        var order = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(orgId, siteId, orderId));

        await order.CreateAsync(new CreateOrderCommand(orgId, siteId, Guid.NewGuid(), OrderType.DineIn, GuestCount: 1));

        await order.AddLineAsync(new AddLineCommand(
            menuItemId, "Hamburger", 2, 12.00m)); // 2 hamburgers (uses menuItemId from recipe)

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

    // Given: A dine-in order with inventory consumed and the order subsequently voided
    // When: The inventory consumption is reversed after the void
    // Then: Stock is fully restored to its pre-order level
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
        var movement = state.RecentMovements.First(m => m.ReferenceId == orderId);

        // Reverse the inventory consumption (in real flow, this happens via event handler)
        await inventory.ReverseConsumptionAsync(movement.Id, "Order voided", Guid.NewGuid());

        // Assert
        var levelAfterVoid = await inventory.GetLevelInfoAsync();
        levelAfterVoid.QuantityAvailable.Should().Be(100m);

        var orderState = await order.GetStateAsync();
        orderState.Status.Should().Be(OrderStatus.Voided);
    }

    // Given: An inventory item with two separate consumptions for the same order
    // When: All consumption movements for the order are reversed
    // Then: Both movements are reversed and stock is fully restored
    [Fact]
    public async Task Inventory_ReverseOrderConsumption_ShouldRestoreAllMovementsForOrder()
    {
        // Arrange
        var (inventory, _) = await CreateInventoryWithStockAsync(100m);
        var orderId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();

        // Consume multiple times for the same order
        await inventory.ConsumeForOrderAsync(orderId, 10m, performedBy);
        await inventory.ConsumeForOrderAsync(orderId, 15m, performedBy);

        var levelAfterConsumption = await inventory.GetLevelInfoAsync();
        levelAfterConsumption.QuantityAvailable.Should().Be(75m); // 100 - 10 - 15

        // Act
        var reversedCount = await inventory.ReverseOrderConsumptionAsync(orderId, "Order voided", performedBy);

        // Assert
        reversedCount.Should().Be(2);

        var levelAfterReversal = await inventory.GetLevelInfoAsync();
        levelAfterReversal.QuantityAvailable.Should().Be(100m);
    }

    // Given: An inventory item with no consumption movements for the specified order
    // When: A reversal is attempted for the non-existent order
    // Then: Zero movements are reversed and stock remains unchanged
    [Fact]
    public async Task Inventory_ReverseOrderConsumption_WhenNoMovements_ShouldReturnZero()
    {
        // Arrange
        var (inventory, _) = await CreateInventoryWithStockAsync(100m);
        var nonExistentOrderId = Guid.NewGuid();

        // Act
        var reversedCount = await inventory.ReverseOrderConsumptionAsync(
            nonExistentOrderId, "Order voided", Guid.NewGuid());

        // Assert
        reversedCount.Should().Be(0);

        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(100m);
    }

    // Given: A dine-in order with line items
    // When: The order is voided with the ReverseInventory flag set to true
    // Then: The order is voided and the void reason is recorded for inventory reversal
    [Fact]
    public async Task Order_VoidWithReverseInventory_ShouldPassFlagInEvent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var orderId = Guid.NewGuid();
        var order = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(orgId, siteId, orderId));

        await order.CreateAsync(new CreateOrderCommand(orgId, siteId, Guid.NewGuid(), OrderType.DineIn, GuestCount: 1));
        await order.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 2, 10.00m));

        // Act - Void with ReverseInventory=true
        await order.VoidAsync(new VoidOrderCommand(Guid.NewGuid(), "Customer cancelled", ReverseInventory: true));

        // Assert
        var orderState = await order.GetStateAsync();
        orderState.Status.Should().Be(OrderStatus.Voided);
        orderState.VoidReason.Should().Be("Customer cancelled");
    }

    // Given: A dine-in order with inventory consumed (e.g., dropped food)
    // When: The order is voided without the ReverseInventory flag
    // Then: Inventory remains deducted because the stock was physically used
    [Fact]
    public async Task Order_VoidWithoutReverseInventory_ShouldNotReverseStock()
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
        await order.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 10.00m));

        // Simulate inventory consumption (as would happen when order completes)
        await inventory.ConsumeForOrderAsync(orderId, 5m, Guid.NewGuid());

        var levelAfterOrder = await inventory.GetLevelInfoAsync();
        levelAfterOrder.QuantityAvailable.Should().Be(95m);

        // Act - Void WITHOUT ReverseInventory (default is false)
        await order.VoidAsync(new VoidOrderCommand(Guid.NewGuid(), "Dropped food"));

        // Assert - inventory should NOT be reversed
        var levelAfterVoid = await inventory.GetLevelInfoAsync();
        levelAfterVoid.QuantityAvailable.Should().Be(95m); // Still deducted

        var orderState = await order.GetStateAsync();
        orderState.Status.Should().Be(OrderStatus.Voided);
    }

    // ============================================================================
    // Waste Recording Tests
    // ============================================================================

    // Given: An inventory item with 50 units of stock
    // When: 5 units are recorded as waste due to spoilage
    // Then: Available stock is reduced to 45 units
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

    // Given: An inventory item with only 5 units of stock
    // When: A waste of 10 units is recorded exceeding available stock
    // Then: The waste recording is rejected due to insufficient stock
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

    // Given: An inventory item with 50 units of stock
    // When: A physical count adjustment sets the quantity to 40 units
    // Then: Available stock is corrected to 40 units
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

    // Given: An inventory item with 50 units of stock
    // When: A physical count finds extra stock and adjusts quantity to 60 units
    // Then: On-hand quantity is increased to 60 units
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

    // Given: A source site inventory item with 100 units of stock
    // When: 25 units are transferred out to another site
    // Then: Source site stock is reduced to 75 units
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

    // Given: A source site inventory item with only 10 units of stock
    // When: A transfer of 50 units is attempted exceeding available stock
    // Then: The transfer is rejected due to insufficient stock at the source
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

    // Given: An empty destination site inventory item
    // When: A transfer of 25 units is received from another site
    // Then: Destination stock is increased to 25 units
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

    // Given: An inventory item with one expired batch and one valid batch
    // When: Expired batches are written off
    // Then: Only the valid batch remains and stock reflects the non-expired quantity
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
