using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// BDD-style tests for negative stock behavior.
/// Service doesn't stop for inventory discrepancies - the system tracks reality.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "BDD")]
public class StockInventoryBddTests
{
    private readonly TestClusterFixture _fixture;

    public StockInventoryBddTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // ============================================================================
    // Feature: Negative Stock
    // As an operations manager
    // I want the system to allow sales beyond recorded stock
    // Because reality doesn't stop for inventory discrepancies
    // ============================================================================

    [Fact]
    public async Task Given_Stock_When_SalesExceedRecorded_Then_StockGoesNegative()
    {
        // Given: 5 portions recorded in system
        var inventory = await CreateInventory("Chef Special", "portions");
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH-001", 5m, 15.00m));

        // When: Kitchen sells 8 (had unrecorded stock from transfer)
        await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 8m, Guid.NewGuid());

        // Then: System shows -3, flagging discrepancy
        var level = await inventory.GetLevelInfoAsync();
        level.QuantityAvailable.Should().Be(-3m);
        level.Level.Should().Be(StockLevel.OutOfStock);
    }

    [Fact]
    public async Task Given_NegativeStock_When_DeliveryArrives_Then_StockBecomesPositive()
    {
        // Given: Stock at -5 from overselling
        var inventory = await CreateInventory("Ribeye Steak", "portions");
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH-001", 20m, 18.00m));
        await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 25m, Guid.NewGuid());

        var negative = await inventory.GetLevelInfoAsync();
        negative.QuantityAvailable.Should().Be(-5m);

        // When: New delivery of 30 arrives
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH-002", 30m, 19.00m));

        // Then: Stock is 25 (deficit covered + new stock)
        var positive = await inventory.GetLevelInfoAsync();
        positive.QuantityAvailable.Should().Be(25m);
        positive.Level.Should().Be(StockLevel.Normal);
    }

    [Fact]
    public async Task Given_NegativeStock_When_StockTake_Then_AdjustsToActualCount()
    {
        // Given: System shows -20 after busy weekend
        var inventory = await CreateInventory("Burger Patties", "pieces");
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH-001", 100m, 1.50m));
        await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 120m, Guid.NewGuid());

        var negative = await inventory.GetLevelInfoAsync();
        negative.QuantityAvailable.Should().Be(-20m);

        // When: Physical count finds 10 remaining (unrecorded transfer of 30 happened)
        await inventory.AdjustQuantityAsync(
            new AdjustQuantityCommand(10m, "Stock take: variance of 30 from unrecorded transfer", Guid.NewGuid()));

        // Then: Stock corrected to actual
        var adjusted = await inventory.GetLevelInfoAsync();
        adjusted.QuantityOnHand.Should().Be(10m);
    }

    // ============================================================================
    // Helper
    // ============================================================================

    private async Task<IInventoryGrain> CreateInventory(string name, string unit)
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, ingredientId));

        await grain.InitializeAsync(new InitializeInventoryCommand(
            orgId, siteId, ingredientId, name,
            $"SKU-{ingredientId.ToString()[..8]}", unit, "Test Category"));

        return grain;
    }
}
