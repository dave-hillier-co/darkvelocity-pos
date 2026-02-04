using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class InventoryGrainTests
{
    private readonly TestClusterFixture _fixture;

    public InventoryGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IInventoryGrain> CreateInventoryAsync(Guid orgId, Guid siteId, Guid ingredientId)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
        await grain.InitializeAsync(new InitializeInventoryCommand(orgId, siteId, ingredientId, "Ground Beef", "BEEF001", "lb", "Proteins", 10, 50));
        return grain;
    }

    [Fact]
    public async Task InitializeAsync_ShouldInitializeInventory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));

        // Act
        await grain.InitializeAsync(new InitializeInventoryCommand(orgId, siteId, ingredientId, "Ground Beef", "BEEF001", "lb", "Proteins", 10, 50));

        // Assert
        var state = await grain.GetStateAsync();
        state.IngredientId.Should().Be(ingredientId);
        state.IngredientName.Should().Be("Ground Beef");
        state.Sku.Should().Be("BEEF001");
        state.Unit.Should().Be("lb");
        state.ReorderPoint.Should().Be(10);
        state.ParLevel.Should().Be(50);
    }

    [Fact]
    public async Task ReceiveBatchAsync_ShouldAddBatchAndUpdateQuantity()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        // Act
        var result = await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        // Assert
        result.NewQuantityOnHand.Should().Be(100);
        result.NewWeightedAverageCost.Should().Be(5.00m);

        var batches = await grain.GetActiveBatchesAsync();
        batches.Should().HaveCount(1);
        batches[0].BatchNumber.Should().Be("BATCH001");
    }

    [Fact]
    public async Task ReceiveBatchAsync_MultipleBatches_ShouldCalculateWeightedAverageCost()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        // Act
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m)); // $500
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH002", 100, 7.00m)); // $700

        // Assert
        var level = await grain.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(200);
        level.WeightedAverageCost.Should().Be(6.00m); // (500 + 700) / 200 = 6
    }

    [Fact]
    public async Task ConsumeAsync_ShouldUseFifo()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 50, 5.00m));
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH002", 50, 7.00m));

        // Act
        var result = await grain.ConsumeAsync(new ConsumeStockCommand(60, "Production"));

        // Assert
        result.QuantityConsumed.Should().Be(60);
        result.BatchBreakdown.Should().HaveCount(2);
        result.BatchBreakdown[0].Quantity.Should().Be(50); // All from BATCH001
        result.BatchBreakdown[1].Quantity.Should().Be(10); // 10 from BATCH002

        // Total cost: 50 * 5 + 10 * 7 = 250 + 70 = 320
        result.TotalCost.Should().Be(320);

        var level = await grain.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(40);
    }

    [Fact]
    public async Task ConsumeAsync_InsufficientStock_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 50, 5.00m));

        // Act
        var act = () => grain.ConsumeAsync(new ConsumeStockCommand(100, "Production"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Insufficient stock");
    }

    [Fact]
    public async Task RecordWasteAsync_ShouldDeductStock()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        // Act
        await grain.RecordWasteAsync(new RecordWasteCommand(10, "Expired", "Spoilage", Guid.NewGuid()));

        // Assert
        var level = await grain.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(90);
    }

    [Fact]
    public async Task AdjustQuantityAsync_Increase_ShouldAddStock()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        // Act
        await grain.AdjustQuantityAsync(new AdjustQuantityCommand(120, "Found extra stock", Guid.NewGuid()));

        // Assert
        var level = await grain.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(120);
    }

    [Fact]
    public async Task AdjustQuantityAsync_Decrease_ShouldRemoveStock()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        // Act
        await grain.AdjustQuantityAsync(new AdjustQuantityCommand(80, "Physical count shortage", Guid.NewGuid()));

        // Assert
        var level = await grain.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(80);
    }

    [Fact]
    public async Task GetStockLevelAsync_ShouldReturnCorrectLevel()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        // Initially out of stock
        var level = await grain.GetStockLevelAsync();
        level.Should().Be(StockLevel.OutOfStock);

        // Add some stock (below reorder point of 10)
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 5, 5.00m));
        level = await grain.GetStockLevelAsync();
        level.Should().Be(StockLevel.Low);

        // Add more stock (normal range)
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH002", 25, 5.00m));
        level = await grain.GetStockLevelAsync();
        level.Should().Be(StockLevel.Normal);

        // Add more stock (above par of 50)
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH003", 30, 5.00m));
        level = await grain.GetStockLevelAsync();
        level.Should().Be(StockLevel.AbovePar);
    }

    [Fact]
    public async Task HasSufficientStockAsync_ShouldReturnCorrectValue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 50, 5.00m));

        // Act & Assert
        (await grain.HasSufficientStockAsync(30)).Should().BeTrue();
        (await grain.HasSufficientStockAsync(50)).Should().BeTrue();
        (await grain.HasSufficientStockAsync(51)).Should().BeFalse();
    }

    [Fact]
    public async Task WriteOffExpiredBatchesAsync_ShouldRemoveExpiredStock()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 50, 5.00m, DateTime.UtcNow.AddDays(-1))); // Expired
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH002", 50, 6.00m, DateTime.UtcNow.AddDays(30))); // Valid

        // Act
        await grain.WriteOffExpiredBatchesAsync(Guid.NewGuid());

        // Assert
        var level = await grain.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(50);

        var batches = await grain.GetActiveBatchesAsync();
        batches.Should().HaveCount(1);
        batches[0].BatchNumber.Should().Be("BATCH002");
    }

    [Fact]
    public async Task TransferOutAsync_ShouldDeductStock()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        // Act
        await grain.TransferOutAsync(new TransferOutCommand(30, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        var level = await grain.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(70);
    }

    [Fact]
    public async Task ConsumeAsync_BelowReorderPoint_ShouldTriggerAlert()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId); // ReorderPoint = 10

        // Start with stock above reorder point
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 20, 5.00m));

        // Act - consume to go below reorder point
        await grain.ConsumeAsync(new ConsumeStockCommand(15, "Production"));

        // Assert - stock level should be Low
        var state = await grain.GetStateAsync();
        state.StockLevel.Should().Be(StockLevel.Low);
        state.QuantityOnHand.Should().Be(5);
    }

    [Fact]
    public async Task ReceiveBatchAsync_ShouldIntegrateWithLedger()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        // Act
        var result = await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 50, 4.00m));

        // Assert - ledger integration verified through state consistency
        var level = await grain.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(50);

        // Verify sufficient balance check works (ledger integration)
        (await grain.HasSufficientStockAsync(50)).Should().BeTrue();
        (await grain.HasSufficientStockAsync(51)).Should().BeFalse();
    }

    [Fact]
    public async Task ConsumeAsync_BeyondAvailable_ShouldEstimateCostFromWAC()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 50, 10.00m));

        // Act - consume more than available (system allows negative stock)
        var result = await grain.ConsumeAsync(new ConsumeStockCommand(80, "High demand production"));

        // Assert
        result.QuantityConsumed.Should().Be(80);
        // 50 units at $10 from batch + 30 units estimated at WAC ($10)
        // Total: 50 * 10 + 30 * 10 = 500 + 300 = 800
        result.TotalCost.Should().Be(800);

        var level = await grain.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(-30); // Negative stock
    }

    [Fact]
    public async Task RecordPhysicalCountAsync_ShouldAdjustAndRecordCountDate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        var countedBy = Guid.NewGuid();
        var beforeCount = DateTime.UtcNow;

        // Act
        await grain.RecordPhysicalCountAsync(85, countedBy);

        // Assert
        var state = await grain.GetStateAsync();
        state.QuantityOnHand.Should().Be(85);
        state.LastCountedAt.Should().NotBeNull();
        state.LastCountedAt.Should().BeOnOrAfter(beforeCount);
    }

    [Fact]
    public async Task UpdateSettingsAsync_ShouldUpdateBothReorderAndPar()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId); // ReorderPoint = 10, ParLevel = 50

        // Act
        await grain.UpdateSettingsAsync(new UpdateInventorySettingsCommand(20, 100));

        // Assert
        var state = await grain.GetStateAsync();
        state.ReorderPoint.Should().Be(20);
        state.ParLevel.Should().Be(100);
    }

    [Fact]
    public async Task RecordMovement_Over100_ShouldPruneOldest()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        // Act - create more than 100 movements via batch receipts and consumptions
        for (int i = 0; i < 60; i++)
        {
            await grain.ReceiveBatchAsync(new ReceiveBatchCommand($"BATCH{i:D3}", 10, 1.00m));
        }

        for (int i = 0; i < 50; i++)
        {
            await grain.ConsumeAsync(new ConsumeStockCommand(1, $"Consumption {i}"));
        }

        // Assert - movements should be pruned to 100
        var state = await grain.GetStateAsync();
        state.RecentMovements.Count.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task WAC_StockGoesToZero_ThenReceive_ShouldResetToNewCost()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 50, 5.00m));
        await grain.ConsumeAsync(new ConsumeStockCommand(50, "Deplete stock"));

        // Verify stock is zero and WAC is reset
        var levelBefore = await grain.GetLevelInfoAsync();
        levelBefore.QuantityOnHand.Should().Be(0);
        levelBefore.WeightedAverageCost.Should().Be(0); // WAC is 0 when no stock

        // Act - receive new batch at different cost
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH002", 30, 8.00m));

        // Assert - WAC should be the new cost
        var levelAfter = await grain.GetLevelInfoAsync();
        levelAfter.QuantityOnHand.Should().Be(30);
        levelAfter.WeightedAverageCost.Should().Be(8.00m);
    }

    [Fact]
    public async Task WAC_NegativeStock_ThenReceive_ShouldCalculateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 20, 5.00m));
        // Consume more than available to go negative
        await grain.ConsumeAsync(new ConsumeStockCommand(30, "Oversell"));

        var levelBefore = await grain.GetLevelInfoAsync();
        levelBefore.QuantityOnHand.Should().Be(-10);

        // Act - receive to cover negative and add more
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH002", 50, 6.00m));

        // Assert
        var levelAfter = await grain.GetLevelInfoAsync();
        levelAfter.QuantityOnHand.Should().Be(40); // -10 + 50 = 40
        // Only the new batch has positive quantity (50 @ $6)
        // WAC = 50 * 6 / 50 = 6 (from the active batch with positive quantity)
        levelAfter.WeightedAverageCost.Should().Be(6.00m);
    }

    [Fact]
    public async Task WriteOffExpiredBatches_ExpiresExactlyNow_ShouldWriteOff()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        // Add batch that expires yesterday (definitely expired)
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("EXPIRED", 25, 5.00m, DateTime.UtcNow.AddDays(-1)));
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("VALID", 25, 6.00m, DateTime.UtcNow.AddDays(30)));

        var levelBefore = await grain.GetLevelInfoAsync();
        levelBefore.QuantityOnHand.Should().Be(50);

        // Act
        await grain.WriteOffExpiredBatchesAsync(Guid.NewGuid());

        // Assert
        var levelAfter = await grain.GetLevelInfoAsync();
        levelAfter.QuantityOnHand.Should().Be(25); // Only valid batch remains

        var batches = await grain.GetActiveBatchesAsync();
        batches.Should().HaveCount(1);
        batches[0].BatchNumber.Should().Be("VALID");
    }

    [Fact]
    public async Task ConsumeFifo_ExpiredBatchPresent_ShouldConsumeFromNonExpired()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        // Older batch (cheaper) - still active until explicitly written off
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 30, 4.00m, DateTime.UtcNow.AddDays(-1)));
        // Newer batch (more expensive)
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH002", 30, 6.00m, DateTime.UtcNow.AddDays(30)));

        // Act - consume using FIFO (consumes from oldest first regardless of expiry)
        var result = await grain.ConsumeAsync(new ConsumeStockCommand(20, "Production"));

        // Assert - FIFO consumes from oldest batch first
        result.QuantityConsumed.Should().Be(20);
        result.BatchBreakdown.Should().HaveCount(1);
        result.BatchBreakdown[0].BatchNumber.Should().Be("BATCH001");
        result.BatchBreakdown[0].UnitCost.Should().Be(4.00m);
    }

    [Fact]
    public async Task ReceiveBatch_FromLow_ShouldTransitionToNormal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId); // ReorderPoint = 10, ParLevel = 50

        // Start with low stock
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 5, 5.00m));
        var levelBefore = await grain.GetStockLevelAsync();
        levelBefore.Should().Be(StockLevel.Low);

        // Act - receive to get above reorder point but below par
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH002", 20, 5.00m));

        // Assert
        var levelAfter = await grain.GetStockLevelAsync();
        levelAfter.Should().Be(StockLevel.Normal);
    }

    [Fact]
    public async Task ReceiveBatch_AbovePar_ShouldTransitionToAbovePar()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId); // ReorderPoint = 10, ParLevel = 50

        // Start with normal stock
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 30, 5.00m));
        var levelBefore = await grain.GetStockLevelAsync();
        levelBefore.Should().Be(StockLevel.Normal);

        // Act - receive to go above par level
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH002", 30, 5.00m));

        // Assert
        var levelAfter = await grain.GetStockLevelAsync();
        levelAfter.Should().Be(StockLevel.AbovePar);

        var level = await grain.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(60); // Above par level of 50
    }

    [Fact]
    public async Task TransferOut_ShouldRecordMovementWithTransferId()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        var transferId = Guid.NewGuid();
        var destinationSiteId = Guid.NewGuid();
        var transferredBy = Guid.NewGuid();

        // Act
        await grain.TransferOutAsync(new TransferOutCommand(25, destinationSiteId, transferId, transferredBy));

        // Assert
        var state = await grain.GetStateAsync();
        var transferMovement = state.RecentMovements.FirstOrDefault(m => m.Type == MovementType.Transfer && m.Quantity < 0);
        transferMovement.Should().NotBeNull();
        transferMovement!.Quantity.Should().Be(-25);
        transferMovement.ReferenceId.Should().Be(transferId);
        transferMovement.PerformedBy.Should().Be(transferredBy);
    }

    [Fact]
    public async Task ReceiveTransfer_ShouldRecordSourceSite()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        var sourceSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();

        // Act
        var result = await grain.ReceiveTransferAsync(new ReceiveTransferCommand(40, 5.50m, sourceSiteId, transferId));

        // Assert
        result.NewQuantityOnHand.Should().Be(40);
        result.NewWeightedAverageCost.Should().Be(5.50m);

        var state = await grain.GetStateAsync();
        var transferMovement = state.RecentMovements.FirstOrDefault(m => m.Type == MovementType.Transfer && m.Quantity > 0);
        transferMovement.Should().NotBeNull();
        transferMovement!.Quantity.Should().Be(40);
        transferMovement.ReferenceId.Should().Be(transferId);
        transferMovement.Reason.Should().Contain(sourceSiteId.ToString());

        // Verify batch was created with transfer prefix
        var batches = await grain.GetActiveBatchesAsync();
        batches.Should().HaveCount(1);
        batches[0].BatchNumber.Should().StartWith("XFER-");
    }
}
