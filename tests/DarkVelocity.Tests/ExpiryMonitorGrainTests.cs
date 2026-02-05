using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ExpiryMonitorGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ExpiryMonitorGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IInventoryGrain> CreateInventoryAsync(Guid orgId, Guid siteId, Guid ingredientId, string name = "Test Ingredient")
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
        await grain.InitializeAsync(new InitializeInventoryCommand(orgId, siteId, ingredientId, name, $"SKU-{ingredientId.ToString()[..8]}", "units", "General", 10, 50));
        return grain;
    }

    [Fact]
    public async Task InitializeAsync_ShouldSetupMonitor()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IExpiryMonitorGrain>(GrainKeys.ExpiryMonitor(orgId, siteId));

        // Act
        await grain.InitializeAsync(orgId, siteId);

        // Assert
        (await grain.ExistsAsync()).Should().BeTrue();
        var settings = await grain.GetSettingsAsync();
        settings.WarningDays.Should().Be(30);
        settings.CriticalDays.Should().Be(7);
    }

    [Fact]
    public async Task ScanForExpiringItemsAsync_ShouldDetectExpiringBatches()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredientId, "Milk");
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 50, 3.00m, DateTime.UtcNow.AddDays(5))); // Critical - expires in 5 days

        var monitorGrain = _fixture.Cluster.GrainFactory.GetGrain<IExpiryMonitorGrain>(GrainKeys.ExpiryMonitor(orgId, siteId));
        await monitorGrain.InitializeAsync(orgId, siteId);
        await monitorGrain.RegisterIngredientAsync(ingredientId, "Milk", "SKU-MILK", "Dairy");

        // Act
        var report = await monitorGrain.ScanForExpiringItemsAsync();

        // Assert
        report.CriticalCount.Should().Be(1);
        report.CriticalItems.Should().HaveCount(1);
        report.CriticalItems[0].IngredientName.Should().Be("Milk");
        report.CriticalItems[0].Urgency.Should().Be(ExpiryUrgency.Critical);
        report.CriticalItems[0].DaysUntilExpiry.Should().BeLessThanOrEqualTo(7);
    }

    [Fact]
    public async Task ScanForExpiringItemsAsync_ShouldDetectExpiredBatches()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredientId, "Yogurt");
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 30, 2.50m, DateTime.UtcNow.AddDays(-1))); // Expired yesterday

        var monitorGrain = _fixture.Cluster.GrainFactory.GetGrain<IExpiryMonitorGrain>(GrainKeys.ExpiryMonitor(orgId, siteId));
        await monitorGrain.InitializeAsync(orgId, siteId);
        await monitorGrain.RegisterIngredientAsync(ingredientId, "Yogurt", "SKU-YOG", "Dairy");

        // Act
        var report = await monitorGrain.ScanForExpiringItemsAsync();

        // Assert
        report.ExpiredCount.Should().Be(1);
        report.ExpiredItems.Should().HaveCount(1);
        report.ExpiredItems[0].Urgency.Should().Be(ExpiryUrgency.Expired);
        report.TotalExpiredValue.Should().Be(75); // 30 * 2.50
    }

    [Fact]
    public async Task WriteOffExpiredBatchesAsync_ShouldRemoveExpiredStock()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredientId, "Cheese");
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("EXPIRED", 20, 5.00m, DateTime.UtcNow.AddDays(-2)));
        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("VALID", 30, 5.00m, DateTime.UtcNow.AddDays(30)));

        var monitorGrain = _fixture.Cluster.GrainFactory.GetGrain<IExpiryMonitorGrain>(GrainKeys.ExpiryMonitor(orgId, siteId));
        await monitorGrain.InitializeAsync(orgId, siteId);
        await monitorGrain.RegisterIngredientAsync(ingredientId, "Cheese", "SKU-CHE", "Dairy");

        // Act
        var writeOffs = await monitorGrain.WriteOffExpiredBatchesAsync(userId);

        // Assert
        writeOffs.Should().HaveCount(1);
        writeOffs[0].Quantity.Should().Be(20);
        writeOffs[0].TotalCost.Should().Be(100); // 20 * 5.00

        // Verify inventory was updated
        var level = await inventoryGrain.GetLevelInfoAsync();
        level.QuantityOnHand.Should().Be(30); // Only valid batch remains
    }

    [Fact]
    public async Task GetValueAtRiskByUrgencyAsync_ShouldCategorizeCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryAsync(orgId, siteId, ingredientId, "Produce");
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH1", 10, 10.00m, DateTime.UtcNow.AddDays(5)));  // Critical
        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH2", 20, 10.00m, DateTime.UtcNow.AddDays(10))); // Urgent
        await inventoryGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH3", 30, 10.00m, DateTime.UtcNow.AddDays(20))); // Warning

        var monitorGrain = _fixture.Cluster.GrainFactory.GetGrain<IExpiryMonitorGrain>(GrainKeys.ExpiryMonitor(orgId, siteId));
        await monitorGrain.InitializeAsync(orgId, siteId);
        await monitorGrain.RegisterIngredientAsync(ingredientId, "Produce", "SKU-PRO", "Fresh");

        // Act
        var valueByUrgency = await monitorGrain.GetValueAtRiskByUrgencyAsync();

        // Assert
        valueByUrgency[ExpiryUrgency.Critical].Should().Be(100); // 10 * 10
        valueByUrgency[ExpiryUrgency.Urgent].Should().Be(200);   // 20 * 10
        valueByUrgency[ExpiryUrgency.Warning].Should().Be(300);  // 30 * 10
    }
}
