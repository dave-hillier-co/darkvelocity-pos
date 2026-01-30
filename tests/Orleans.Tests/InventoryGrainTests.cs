using DarkVelocity.Orleans.Abstractions;
using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.State;
using FluentAssertions;

namespace DarkVelocity.Orleans.Tests;

[Collection(ClusterCollection.Name)]
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
}
