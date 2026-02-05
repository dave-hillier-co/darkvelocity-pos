using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Advanced inventory tests covering edge cases for:
/// - Reserved quantity tracking
/// - Ledger consistency
/// - Complex negative stock scenarios
/// - Multi-batch consumption patterns
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class InventoryAdvancedTests
{
    private readonly TestClusterFixture _fixture;

    public InventoryAdvancedTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IInventoryGrain> CreateInventoryAsync(
        Guid orgId, Guid siteId, Guid ingredientId,
        string name = "Test Ingredient",
        decimal reorderPoint = 10m,
        decimal parLevel = 50m)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));
        await grain.InitializeAsync(new InitializeInventoryCommand(
            orgId, siteId, ingredientId, name,
            $"SKU-{ingredientId.ToString()[..8]}", "units", "General",
            reorderPoint, parLevel));
        return grain;
    }

    // ============================================================================
    // Ledger Consistency Tests
    // ============================================================================

    [Fact]
    public async Task LedgerConsistency_MultipleOperations_ShouldMaintainAccuracy()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        // Act - perform many operations
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH002", 50, 6.00m));
        await inventory.ConsumeAsync(new ConsumeStockCommand(30, "Production"));
        await inventory.ConsumeAsync(new ConsumeStockCommand(20, "Production"));
        await inventory.RecordWasteAsync(new RecordWasteCommand(5, "Spoiled", "Spoilage", Guid.NewGuid()));
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH003", 25, 5.50m));
        await inventory.ConsumeAsync(new ConsumeStockCommand(40, "Production"));

        // Assert - verify final state consistency
        var level = await inventory.GetLevelInfoAsync();
        // 100 + 50 - 30 - 20 - 5 + 25 - 40 = 80
        level.QuantityOnHand.Should().Be(80);

        // Verify HasSufficientStock reflects ledger state
        (await inventory.HasSufficientStockAsync(80)).Should().BeTrue();
        (await inventory.HasSufficientStockAsync(81)).Should().BeFalse();
    }

    [Fact]
    public async Task LedgerConsistency_AfterReversal_ShouldMatchInventory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);
        var orderId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));
        await inventory.ConsumeForOrderAsync(orderId, 30, performedBy);

        var levelBefore = await inventory.GetLevelInfoAsync();
        levelBefore.QuantityOnHand.Should().Be(70);

        // Act - reverse the consumption
        var reversedCount = await inventory.ReverseOrderConsumptionAsync(orderId, "Order voided", performedBy);

        // Assert
        reversedCount.Should().Be(1);
        var levelAfter = await inventory.GetLevelInfoAsync();
        levelAfter.QuantityOnHand.Should().Be(100);

        // Ledger should also reflect the reversal
        (await inventory.HasSufficientStockAsync(100)).Should().BeTrue();
        (await inventory.HasSufficientStockAsync(101)).Should().BeFalse();
    }

    [Fact]
    public async Task LedgerConsistency_NegativeStock_LedgerAllowsNegative()
    {
        // Arrange - Per design: "Negative stock is the default"
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 50, 5.00m));

        // Act - consume more than available
        await inventory.ConsumeAsync(new ConsumeStockCommand(80, "High demand"));

        // Assert - stock should be negative
        var level = await inventory.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(-30);

        // HasSufficientStock should return false for any positive quantity
        (await inventory.HasSufficientStockAsync(1)).Should().BeFalse();
    }

    [Fact]
    public async Task LedgerConsistency_PhysicalCount_ShouldAdjustBothInventoryAndLedger()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);
        var countedBy = Guid.NewGuid();

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));
        await inventory.ConsumeAsync(new ConsumeStockCommand(20, "Production"));

        // System shows 80, but physical count finds 75
        var levelBefore = await inventory.GetLevelInfoAsync();
        levelBefore.QuantityOnHand.Should().Be(80);

        // Act
        await inventory.RecordPhysicalCountAsync(75, countedBy);

        // Assert
        var levelAfter = await inventory.GetLevelInfoAsync();
        levelAfter.QuantityOnHand.Should().Be(75);

        // Ledger should also be adjusted
        (await inventory.HasSufficientStockAsync(75)).Should().BeTrue();
        (await inventory.HasSufficientStockAsync(76)).Should().BeFalse();
    }

    // ============================================================================
    // Negative Stock Edge Cases
    // ============================================================================

    [Fact]
    public async Task NegativeStock_DeepNegative_ThenReceivePartial_ShouldRemainNegative()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 20, 5.00m));
        // Go deeply negative
        await inventory.ConsumeAsync(new ConsumeStockCommand(100, "Oversell"));

        var levelNegative = await inventory.GetLevelInfoAsync();
        levelNegative.QuantityOnHand.Should().Be(-80);

        // Act - receive partial replenishment
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH002", 50, 6.00m));

        // Assert - still negative
        var levelAfter = await inventory.GetLevelInfoAsync();
        levelAfter.QuantityOnHand.Should().Be(-30);
        levelAfter.Level.Should().Be(StockLevel.OutOfStock);
    }

    [Fact]
    public async Task NegativeStock_MultipleOversells_ThenReconcile()
    {
        // Arrange - Simulates busy weekend with unrecorded transfers
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId, "Burger Patties");

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 1.50m));

        // Multiple orders consume beyond recorded stock
        await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 40, Guid.NewGuid());
        await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 35, Guid.NewGuid());
        await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 45, Guid.NewGuid()); // Goes negative

        var levelNegative = await inventory.GetLevelInfoAsync();
        levelNegative.QuantityOnHand.Should().Be(-20);

        // Act - Physical count finds unrecorded transfer brought in 30
        await inventory.AdjustQuantityAsync(new AdjustQuantityCommand(
            10, "Physical count - found unrecorded transfer", Guid.NewGuid()));

        // Assert
        var levelAfter = await inventory.GetLevelInfoAsync();
        levelAfter.QuantityOnHand.Should().Be(10);
        levelAfter.Level.Should().Be(StockLevel.Low); // Below reorder point
    }

    [Fact]
    public async Task NegativeStock_ConsumeWhenAlreadyNegative_ShouldGoMoreNegative()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 10, 5.00m));
        await inventory.ConsumeAsync(new ConsumeStockCommand(20, "First oversell")); // -10

        var levelFirst = await inventory.GetLevelInfoAsync();
        levelFirst.QuantityOnHand.Should().Be(-10);

        // Act - consume again while already negative
        await inventory.ConsumeAsync(new ConsumeStockCommand(15, "Second oversell"));

        // Assert
        var levelSecond = await inventory.GetLevelInfoAsync();
        levelSecond.QuantityOnHand.Should().Be(-25);
    }

    [Fact]
    public async Task NegativeStock_WAC_ShouldEstimateCostFromLastKnownWAC()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 50, 10.00m));

        // Act - consume beyond available
        var result = await inventory.ConsumeAsync(new ConsumeStockCommand(80, "High demand"));

        // Assert
        result.QuantityConsumed.Should().Be(80);
        // 50 units at $10 from batch + 30 units estimated at WAC ($10)
        result.TotalCost.Should().Be(800); // 80 * 10

        var level = await inventory.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(-30);
    }

    // ============================================================================
    // FIFO Consumption Edge Cases
    // ============================================================================

    [Fact]
    public async Task FIFO_ExhaustMultipleBatches_ShouldConsumeInOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 30, 4.00m));
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH002", 20, 5.00m));
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH003", 25, 6.00m));
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH004", 15, 7.00m));

        // Act - consume exactly 50 (depletes BATCH001 and BATCH002)
        var result = await inventory.ConsumeAsync(new ConsumeStockCommand(50, "Production"));

        // Assert
        result.BatchBreakdown.Should().HaveCount(2);
        result.BatchBreakdown[0].BatchNumber.Should().Be("BATCH001");
        result.BatchBreakdown[0].Quantity.Should().Be(30);
        result.BatchBreakdown[1].BatchNumber.Should().Be("BATCH002");
        result.BatchBreakdown[1].Quantity.Should().Be(20);

        // Total cost: 30*4 + 20*5 = 120 + 100 = 220
        result.TotalCost.Should().Be(220);

        var batches = await inventory.GetActiveBatchesAsync();
        batches.Should().HaveCount(2); // BATCH003 and BATCH004 remain
        batches[0].BatchNumber.Should().Be("BATCH003");
        batches[1].BatchNumber.Should().Be("BATCH004");
    }

    [Fact]
    public async Task FIFO_PartialBatchConsumption_ShouldLeaveRemainder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        // Act - consume partially
        await inventory.ConsumeAsync(new ConsumeStockCommand(35, "Production"));

        // Assert
        var batches = await inventory.GetActiveBatchesAsync();
        batches.Should().HaveCount(1);
        batches[0].Quantity.Should().Be(65);
    }

    [Fact]
    public async Task FIFO_SmallBatches_ShouldExhaustManyBatches()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        // Add 10 small batches
        for (int i = 1; i <= 10; i++)
        {
            await inventory.ReceiveBatchAsync(new ReceiveBatchCommand($"BATCH{i:D3}", 5, i * 1.00m));
        }

        // Act - consume enough to exhaust 5 batches
        var result = await inventory.ConsumeAsync(new ConsumeStockCommand(25, "Production"));

        // Assert
        result.BatchBreakdown.Should().HaveCount(5);
        result.QuantityConsumed.Should().Be(25);

        var batches = await inventory.GetActiveBatchesAsync();
        batches.Should().HaveCount(5); // 5 remaining
    }

    // ============================================================================
    // Reserved Quantity Tests
    // ============================================================================

    [Fact]
    public async Task QuantityAvailable_ShouldReflectOnHandMinusReserved()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        // Assert - initially available equals on hand (no reservations)
        var level = await inventory.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(100);
        level.QuantityAvailable.Should().Be(100);
    }

    [Fact]
    public async Task QuantityAvailable_AfterConsumption_ShouldDecrease()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        // Act
        await inventory.ConsumeAsync(new ConsumeStockCommand(30, "Production"));

        // Assert
        var level = await inventory.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(70);
        level.QuantityAvailable.Should().Be(70);
    }

    // ============================================================================
    // WAC (Weighted Average Cost) Edge Cases
    // ============================================================================

    [Fact]
    public async Task WAC_MultipleBatches_ShouldCalculateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        // Act
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 10.00m)); // $1000
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH002", 50, 8.00m));  // $400

        // Assert - WAC = $1400 / 150 = $9.33
        var level = await inventory.GetLevelInfoAsync();
        level.WeightedAverageCost.Should().BeApproximately(9.33m, 0.01m);
    }

    [Fact]
    public async Task WAC_AfterConsumption_ShouldRecalculateFromRemainingBatches()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 10.00m));
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH002", 50, 8.00m));

        // Act - consume all of BATCH001
        await inventory.ConsumeAsync(new ConsumeStockCommand(100, "Production"));

        // Assert - only BATCH002 remains, so WAC = $8
        var level = await inventory.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(50);
        level.WeightedAverageCost.Should().Be(8.00m);
    }

    [Fact]
    public async Task WAC_AllStockDepleted_ShouldBeZero()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 50, 10.00m));
        await inventory.ConsumeAsync(new ConsumeStockCommand(50, "Deplete"));

        // Assert
        var level = await inventory.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(0);
        level.WeightedAverageCost.Should().Be(0);
    }

    // ============================================================================
    // Movement History Tests
    // ============================================================================

    [Fact]
    public async Task MovementHistory_ShouldTrackAllOperations()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);
        var orderId = Guid.NewGuid();

        // Act
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));
        await inventory.ConsumeForOrderAsync(orderId, 20, Guid.NewGuid());
        await inventory.RecordWasteAsync(new RecordWasteCommand(5, "Spoiled", "Spoilage", Guid.NewGuid()));

        // Assert
        var state = await inventory.GetStateAsync();
        state.RecentMovements.Should().HaveCount(3);

        var receipt = state.RecentMovements.First(m => m.Type == MovementType.Receipt);
        receipt.Quantity.Should().Be(100);

        var consumption = state.RecentMovements.First(m => m.Type == MovementType.Consumption);
        consumption.Quantity.Should().Be(-20);
        consumption.ReferenceId.Should().Be(orderId);

        var waste = state.RecentMovements.First(m => m.Type == MovementType.Waste);
        waste.Quantity.Should().Be(-5);
    }

    [Fact]
    public async Task MovementHistory_Over100_ShouldPruneOldest()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        // Act - create 110 movements
        for (int i = 0; i < 55; i++)
        {
            await inventory.ReceiveBatchAsync(new ReceiveBatchCommand($"BATCH{i:D3}", 10, 1.00m));
            await inventory.ConsumeAsync(new ConsumeStockCommand(1, $"Consumption {i}"));
        }

        // Assert - should be pruned to 100
        var state = await inventory.GetStateAsync();
        state.RecentMovements.Count.Should().BeLessThanOrEqualTo(100);
    }

    // ============================================================================
    // Batch Expiry Tests
    // ============================================================================

    [Fact]
    public async Task Expiry_MixedBatches_ShouldWriteOffOnlyExpired()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("EXPIRED1", 20, 5.00m, DateTime.UtcNow.AddDays(-10)));
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("VALID1", 30, 5.00m, DateTime.UtcNow.AddDays(30)));
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("EXPIRED2", 15, 5.00m, DateTime.UtcNow.AddDays(-5)));
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("VALID2", 25, 5.00m, DateTime.UtcNow.AddDays(60)));

        var levelBefore = await inventory.GetLevelInfoAsync();
        levelBefore.QuantityOnHand.Should().Be(90);

        // Act
        await inventory.WriteOffExpiredBatchesAsync(Guid.NewGuid());

        // Assert
        var levelAfter = await inventory.GetLevelInfoAsync();
        levelAfter.QuantityOnHand.Should().Be(55); // Only valid batches remain

        var batches = await inventory.GetActiveBatchesAsync();
        batches.Should().HaveCount(2);
        batches.Select(b => b.BatchNumber).Should().Contain("VALID1", "VALID2");
    }

    [Fact]
    public async Task Expiry_AllExpired_ShouldWriteOffAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("EXPIRED1", 50, 5.00m, DateTime.UtcNow.AddDays(-1)));
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("EXPIRED2", 30, 6.00m, DateTime.UtcNow.AddDays(-2)));

        // Act
        await inventory.WriteOffExpiredBatchesAsync(Guid.NewGuid());

        // Assert
        var level = await inventory.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(0);
        level.Level.Should().Be(StockLevel.OutOfStock);
    }

    [Fact]
    public async Task Expiry_NoneExpired_ShouldNotWriteOffAnything()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("VALID1", 50, 5.00m, DateTime.UtcNow.AddDays(30)));
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("VALID2", 30, 6.00m, DateTime.UtcNow.AddDays(60)));

        // Act
        await inventory.WriteOffExpiredBatchesAsync(Guid.NewGuid());

        // Assert
        var level = await inventory.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(80);
    }

    [Fact]
    public async Task Expiry_EarliestExpiry_ShouldReflectClosestExpiryDate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var inventory = await CreateInventoryAsync(orgId, siteId, ingredientId);

        var closestExpiry = DateTime.UtcNow.AddDays(7);
        var middleExpiry = DateTime.UtcNow.AddDays(14);
        var farthestExpiry = DateTime.UtcNow.AddDays(30);

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH1", 20, 5.00m, farthestExpiry));
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH2", 30, 5.00m, closestExpiry));
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH3", 25, 5.00m, middleExpiry));

        // Assert
        var level = await inventory.GetLevelInfoAsync();
        level.EarliestExpiry.Should().NotBeNull();
        level.EarliestExpiry.Should().BeCloseTo(closestExpiry, TimeSpan.FromSeconds(1));
    }
}
