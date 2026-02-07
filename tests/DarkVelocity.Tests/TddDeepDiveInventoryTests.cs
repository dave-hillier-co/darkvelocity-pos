using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TddDeepDiveInventoryTests
{
    private readonly TestClusterFixture _fixture;

    public TddDeepDiveInventoryTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IInventoryGrain> CreateInventoryAsync(
        Guid orgId, Guid siteId, Guid ingredientId,
        string name = "Tomatoes", string sku = "SKU-001", string unit = "kg",
        string category = "Produce", decimal reorderPoint = 5, decimal parLevel = 50)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));
        await grain.InitializeAsync(new InitializeInventoryCommand(
            orgId, siteId, ingredientId, name, sku, unit, category, reorderPoint, parLevel));
        return grain;
    }

    // Given: inventory with 10 units at $5/unit (total batch value = $50)
    // When: 15 units are consumed, driving stock to -5
    // Then: WAC becomes 0 because QuantityOnHand < 0 and RecalculateQuantitiesAndCost
    //       sets WAC = 0 when QuantityOnHand <= 0. This means subsequent cost calculations
    //       using WAC (e.g., transfer out cost, adjustment cost) will be wrong.
    [Fact]
    public async Task WeightedAverageCost_NegativeStock_BecomesZero()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH-001", Quantity: 10m, UnitCost: 5.00m));

        // Act - consume 15 units from 10 available, pushing stock to -5
        await grain.ConsumeAsync(new ConsumeStockCommand(Quantity: 15m, "Over-consumption test"));

        // Assert
        var level = await grain.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(-5m);

        // WAC drops to 0 when stock is negative because the code uses:
        //   state.WeightedAverageCost = state.QuantityOnHand > 0 ? totalValue / state.QuantityOnHand : 0;
        // This is an edge case: the $5/unit cost information is effectively lost.
        level.WeightedAverageCost.Should().Be(0m);
    }

    // Given: inventory with 5 units in a single batch
    // When: 8 units are consumed
    // Then: QuantityOnHand = -3 (negative stock is the default per design philosophy),
    //       the batch is fully exhausted, and the 3-unit excess is tracked as UnbatchedDeficit
    [Fact]
    public async Task ConsumeMoreThanAvailable_StockGoesNegative()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH-001", Quantity: 5m, UnitCost: 4.00m));

        // Act - consume 8 from 5 available
        var result = await grain.ConsumeAsync(new ConsumeStockCommand(Quantity: 8m, "Kitchen consumption"));

        // Assert - stock goes negative
        var level = await grain.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(-3m);

        // FIFO breakdown should only cover the 5 available units from the batch
        result.BatchBreakdown.Should().HaveCount(1);
        result.BatchBreakdown[0].Quantity.Should().Be(5m);
        result.BatchBreakdown[0].BatchNumber.Should().Be("BATCH-001");

        // The batch should be fully exhausted
        var activeBatches = await grain.GetActiveBatchesAsync();
        activeBatches.Should().BeEmpty();

        // Verify unbatched deficit is tracked in state
        var state = await grain.GetStateAsync();
        state.UnbatchedDeficit.Should().Be(3m);
    }

    // Given: inventory at -10 (unbatched deficit of 10, original batch exhausted)
    // When: a transfer of exactly 10 units is received
    // Then: all transferred quantity is absorbed by the deficit (no new batch created),
    //       QuantityOnHand returns to 0. The code at InventoryGrain.cs:396 does
    //       `State.Batches.Last().Id` which returns the WRONG batch ID because no new
    //       transfer batch was created — it returns the old exhausted batch's ID instead.
    [Fact]
    public async Task ReceiveTransfer_FullDeficitAbsorption_BatchIdRetrieval()
    {
        // Arrange - create inventory and drive it to -10
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("ORIGINAL-BATCH", Quantity: 10m, UnitCost: 3.00m));
        await grain.ConsumeAsync(new ConsumeStockCommand(Quantity: 20m, "Over-consumption"));

        // Verify we are at -10 with unbatched deficit of 10
        var stateBefore = await grain.GetStateAsync();
        stateBefore.QuantityOnHand.Should().Be(-10m);
        stateBefore.UnbatchedDeficit.Should().Be(10m);

        // Remember the last batch ID before transfer (the exhausted original batch)
        var lastBatchIdBeforeTransfer = stateBefore.Batches.Last().Id;

        // Act - receive transfer of exactly 10 units (matches deficit exactly)
        var sourceSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var result = await grain.ReceiveTransferAsync(
            new ReceiveTransferCommand(Quantity: 10m, UnitCost: 5.00m, sourceSiteId, transferId));

        // Assert - deficit should be fully absorbed, QuantityOnHand back to 0
        var stateAfter = await grain.GetStateAsync();
        stateAfter.UnbatchedDeficit.Should().Be(0m);
        stateAfter.QuantityOnHand.Should().Be(0m);

        // BUG: No new transfer batch was created because all quantity went to deficit reduction.
        // The code `State.Batches.Last().Id` returns the old exhausted batch, not a transfer batch.
        // Verify no XFER batch exists:
        var activeBatches = await grain.GetActiveBatchesAsync();
        activeBatches.Where(b => b.BatchNumber.StartsWith("XFER-")).Should().BeEmpty();

        // The returned BatchId is actually the old exhausted batch's ID — this is the bug.
        result.BatchId.Should().Be(lastBatchIdBeforeTransfer,
            "because State.Batches.Last().Id returns the old exhausted batch when no new transfer batch is created");
    }

    // Given: inventory with batch A ($2/unit, received first) and batch B ($5/unit, received second)
    // When: a partial consumption happens
    // Then: batch A is consumed first (FIFO order), batch B remains untouched
    [Fact]
    public async Task MultipleConsumptions_FIFOOrder_OldestBatchFirst()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        // Batch A: 10 units at $2/unit (received first)
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH-A", Quantity: 10m, UnitCost: 2.00m));
        // Batch B: 10 units at $5/unit (received second)
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH-B", Quantity: 10m, UnitCost: 5.00m));

        // Act - consume 7 units (less than batch A's quantity)
        var result = await grain.ConsumeAsync(new ConsumeStockCommand(Quantity: 7m, "Kitchen consumption"));

        // Assert - only batch A should be consumed (FIFO)
        result.BatchBreakdown.Should().HaveCount(1);
        result.BatchBreakdown[0].BatchNumber.Should().Be("BATCH-A");
        result.BatchBreakdown[0].Quantity.Should().Be(7m);
        result.BatchBreakdown[0].UnitCost.Should().Be(2.00m);
        result.TotalCost.Should().Be(14.00m); // 7 * $2

        // Verify batch A has 3 remaining and batch B is untouched
        var batches = await grain.GetActiveBatchesAsync();
        batches.Should().HaveCount(2);

        var batchA = batches.First(b => b.BatchNumber == "BATCH-A");
        batchA.Quantity.Should().Be(3m);

        var batchB = batches.First(b => b.BatchNumber == "BATCH-B");
        batchB.Quantity.Should().Be(10m);
    }

    // Given: inventory with 10 units in one batch
    // When: 15 units are consumed
    // Then: the batch is fully exhausted (quantity 0, status Exhausted),
    //       and 5 units are tracked as UnbatchedDeficit for negative stock support
    [Fact]
    public async Task ConsumeBeyondAllBatches_UnbatchedDeficitTracking()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH-001", Quantity: 10m, UnitCost: 6.00m));

        // Act - consume 15 from 10 available
        await grain.ConsumeAsync(new ConsumeStockCommand(Quantity: 15m, "Excess consumption"));

        // Assert
        var state = await grain.GetStateAsync();

        // The batch should be exhausted with 0 quantity
        var exhaustedBatch = state.Batches.FirstOrDefault(b => b.BatchNumber == "BATCH-001");
        exhaustedBatch.Should().NotBeNull();
        exhaustedBatch!.Quantity.Should().Be(0m);
        exhaustedBatch.Status.Should().Be(BatchStatus.Exhausted);

        // 5 units should be tracked as unbatched deficit
        state.UnbatchedDeficit.Should().Be(5m);

        // QuantityOnHand should be -5 (0 from active batches minus 5 deficit)
        state.QuantityOnHand.Should().Be(-5m);

        // No active batches remain
        var activeBatches = await grain.GetActiveBatchesAsync();
        activeBatches.Should().BeEmpty();
    }

    // Given: inventory at -5 (unbatched deficit of 5, no active batches)
    // When: 8 units are received at $3/unit
    // Then: deficit is reduced from 5 to 0, only 3 units are added as a new batch,
    //       and QuantityOnHand = 3
    [Fact]
    public async Task ReceiveAfterNegativeStock_DeficitReducedFirst()
    {
        // Arrange - drive inventory to -5
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("INITIAL", Quantity: 5m, UnitCost: 2.00m));
        await grain.ConsumeAsync(new ConsumeStockCommand(Quantity: 10m, "Over-consume to create deficit"));

        // Verify we are at -5 with unbatched deficit of 5
        var stateBefore = await grain.GetStateAsync();
        stateBefore.QuantityOnHand.Should().Be(-5m);
        stateBefore.UnbatchedDeficit.Should().Be(5m);

        // Act - receive 8 units at $3/unit
        var result = await grain.ReceiveBatchAsync(new ReceiveBatchCommand("RECOVERY-BATCH", Quantity: 8m, UnitCost: 3.00m));

        // Assert
        var stateAfter = await grain.GetStateAsync();

        // Deficit should be fully cleared
        stateAfter.UnbatchedDeficit.Should().Be(0m);

        // QuantityOnHand should be 3 (8 received - 5 absorbed by deficit)
        stateAfter.QuantityOnHand.Should().Be(3m);
        result.NewQuantityOnHand.Should().Be(3m);

        // The new batch should only have 3 units (8 - 5 deficit absorption)
        var activeBatches = await grain.GetActiveBatchesAsync();
        activeBatches.Should().HaveCount(1);
        activeBatches[0].BatchNumber.Should().Be("RECOVERY-BATCH");
        activeBatches[0].Quantity.Should().Be(3m);
    }

    // Given: inventory with reorderPoint=10 and parLevel=50
    // When: quantity available is exactly 10 (at the reorder point boundary)
    // Then: StockLevel should be Low because UpdateStockLevel uses `<=` comparison:
    //       `else if (state.QuantityAvailable <= state.ReorderPoint)`
    [Fact]
    public async Task StockLevel_AtExactReorderPoint_IsLow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId,
            reorderPoint: 10, parLevel: 50);

        // Add exactly 10 units (the reorder point)
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH-001", Quantity: 10m, UnitCost: 4.00m));

        // Act
        var stockLevel = await grain.GetStockLevelAsync();

        // Assert - at exactly reorder point, StockLevel should be Low (uses <= comparison)
        stockLevel.Should().Be(StockLevel.Low);

        // Also verify through state
        var state = await grain.GetStateAsync();
        state.QuantityAvailable.Should().Be(10m);
        state.ReorderPoint.Should().Be(10m);
        state.StockLevel.Should().Be(StockLevel.Low);
    }

    // Given: inventory with stock
    // When: all stock is consumed to exactly 0
    // Then: StockLevel = OutOfStock because UpdateStockLevel checks `<= 0`
    [Fact]
    public async Task StockLevel_ZeroQuantity_IsOutOfStock()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH-001", Quantity: 20m, UnitCost: 3.00m));

        // Verify we start in a normal state
        var levelBefore = await grain.GetStockLevelAsync();
        levelBefore.Should().Be(StockLevel.Normal);

        // Act - consume exactly all stock
        await grain.ConsumeAsync(new ConsumeStockCommand(Quantity: 20m, "Full depletion"));

        // Assert
        var level = await grain.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(0m);
        level.Level.Should().Be(StockLevel.OutOfStock);

        var stockLevel = await grain.GetStockLevelAsync();
        stockLevel.Should().Be(StockLevel.OutOfStock);
    }
}
