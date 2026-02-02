using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Test summary type for IndexGrain tests.
/// </summary>
[GenerateSerializer]
public sealed record TestOrderSummary(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] string Status,
    [property: Id(2)] decimal Total,
    [property: Id(3)] DateTime CreatedAt,
    [property: Id(4)] string? CustomerName = null);

[Collection(ClusterCollection.Name)]
public class IndexGrainTests
{
    private readonly TestClusterFixture _fixture;

    public IndexGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IIndexGrain<TestOrderSummary> GetIndexGrain(Guid orgId, string indexType, string scope)
    {
        var key = GrainKeys.Index(orgId, indexType, scope);
        return _fixture.Cluster.GrainFactory.GetGrain<IIndexGrain<TestOrderSummary>>(key);
    }

    private IIndexGrain<TestOrderSummary> GetIndexGrain(Guid orgId, string indexType, Guid siteId)
    {
        var key = GrainKeys.Index(orgId, indexType, siteId);
        return _fixture.Cluster.GrainFactory.GetGrain<IIndexGrain<TestOrderSummary>>(key);
    }

    [Fact]
    public async Task RegisterAsync_ShouldAddEntryToIndex()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        var summary = new TestOrderSummary(orderId, "Open", 99.99m, DateTime.UtcNow, "John Doe");

        // Act
        await index.RegisterAsync(orderId, summary);

        // Assert
        var count = await index.GetCountAsync();
        count.Should().Be(1);

        var exists = await index.ExistsAsync(orderId);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_ShouldUpdateExistingEntry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        var initialSummary = new TestOrderSummary(orderId, "Open", 99.99m, DateTime.UtcNow);
        await index.RegisterAsync(orderId, initialSummary);

        // Act
        var updatedSummary = new TestOrderSummary(orderId, "Closed", 99.99m, DateTime.UtcNow);
        await index.RegisterAsync(orderId, updatedSummary);

        // Assert
        var count = await index.GetCountAsync();
        count.Should().Be(1);

        var retrieved = await index.GetByIdAsync(orderId);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be("Closed");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateExistingEntry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        var initialSummary = new TestOrderSummary(orderId, "Open", 50m, DateTime.UtcNow);
        await index.RegisterAsync(orderId, initialSummary);

        // Act
        var updatedSummary = new TestOrderSummary(orderId, "Paid", 50m, DateTime.UtcNow);
        await index.UpdateAsync(orderId, updatedSummary);

        // Assert
        var retrieved = await index.GetByIdAsync(orderId);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be("Paid");
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveEntryFromIndex()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        var summary = new TestOrderSummary(orderId, "Open", 75m, DateTime.UtcNow);
        await index.RegisterAsync(orderId, summary);

        // Act
        await index.RemoveAsync(orderId);

        // Assert
        var count = await index.GetCountAsync();
        count.Should().Be(0);

        var exists = await index.ExistsAsync(orderId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAsync_NonExistentEntry_ShouldBeNoOp()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        // Act & Assert - should not throw
        await index.RemoveAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterByPredicate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        var now = DateTime.UtcNow;
        await index.RegisterAsync(Guid.NewGuid(), new TestOrderSummary(Guid.NewGuid(), "Open", 100m, now));
        await index.RegisterAsync(Guid.NewGuid(), new TestOrderSummary(Guid.NewGuid(), "Closed", 200m, now));
        await index.RegisterAsync(Guid.NewGuid(), new TestOrderSummary(Guid.NewGuid(), "Open", 150m, now));
        await index.RegisterAsync(Guid.NewGuid(), new TestOrderSummary(Guid.NewGuid(), "Paid", 75m, now));

        // Act
        var openOrders = await index.QueryAsync(s => s.Status == "Open");

        // Assert
        openOrders.Should().HaveCount(2);
        openOrders.Should().AllSatisfy(o => o.Status.Should().Be("Open"));
    }

    [Fact]
    public async Task QueryAsync_WithLimit_ShouldRespectLimit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        for (int i = 0; i < 10; i++)
        {
            await index.RegisterAsync(Guid.NewGuid(), new TestOrderSummary(Guid.NewGuid(), "Open", i * 10m, DateTime.UtcNow));
        }

        // Act
        var limited = await index.QueryAsync(_ => true, limit: 5);

        // Assert
        limited.Should().HaveCount(5);
    }

    [Fact]
    public async Task QueryAsync_ByTotalRange_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        await index.RegisterAsync(Guid.NewGuid(), new TestOrderSummary(Guid.NewGuid(), "Open", 50m, DateTime.UtcNow));
        await index.RegisterAsync(Guid.NewGuid(), new TestOrderSummary(Guid.NewGuid(), "Open", 150m, DateTime.UtcNow));
        await index.RegisterAsync(Guid.NewGuid(), new TestOrderSummary(Guid.NewGuid(), "Open", 250m, DateTime.UtcNow));
        await index.RegisterAsync(Guid.NewGuid(), new TestOrderSummary(Guid.NewGuid(), "Open", 75m, DateTime.UtcNow));

        // Act
        var highValueOrders = await index.QueryAsync(s => s.Total >= 100m);

        // Assert
        highValueOrders.Should().HaveCount(2);
        highValueOrders.Should().AllSatisfy(o => o.Total.Should().BeGreaterOrEqualTo(100m));
    }

    [Fact]
    public async Task GetRecentAsync_ShouldReturnMostRecentFirst()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        var orderId1 = Guid.NewGuid();
        var orderId2 = Guid.NewGuid();
        var orderId3 = Guid.NewGuid();

        await index.RegisterAsync(orderId1, new TestOrderSummary(orderId1, "First", 10m, DateTime.UtcNow.AddHours(-2)));
        await Task.Delay(10); // Ensure different timestamps
        await index.RegisterAsync(orderId2, new TestOrderSummary(orderId2, "Second", 20m, DateTime.UtcNow.AddHours(-1)));
        await Task.Delay(10);
        await index.RegisterAsync(orderId3, new TestOrderSummary(orderId3, "Third", 30m, DateTime.UtcNow));

        // Act
        var recent = await index.GetRecentAsync(3);

        // Assert
        recent.Should().HaveCount(3);
        recent[0].Status.Should().Be("Third"); // Most recent first
        recent[2].Status.Should().Be("First"); // Oldest last
    }

    [Fact]
    public async Task GetRecentAsync_ShouldRespectLimit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        for (int i = 0; i < 20; i++)
        {
            await index.RegisterAsync(Guid.NewGuid(), new TestOrderSummary(Guid.NewGuid(), $"Order{i}", i * 5m, DateTime.UtcNow));
        }

        // Act
        var recent = await index.GetRecentAsync(5);

        // Assert
        recent.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllEntries()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        var orderId1 = Guid.NewGuid();
        var orderId2 = Guid.NewGuid();

        await index.RegisterAsync(orderId1, new TestOrderSummary(orderId1, "Open", 100m, DateTime.UtcNow));
        await index.RegisterAsync(orderId2, new TestOrderSummary(orderId2, "Closed", 200m, DateTime.UtcNow));

        // Act
        var all = await index.GetAllAsync();

        // Assert
        all.Should().HaveCount(2);
        all.Select(e => e.EntityId).Should().Contain(orderId1);
        all.Select(e => e.EntityId).Should().Contain(orderId2);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingEntry_ShouldReturnSummary()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        var summary = new TestOrderSummary(orderId, "Open", 99.99m, DateTime.UtcNow, "Jane Doe");
        await index.RegisterAsync(orderId, summary);

        // Act
        var retrieved = await index.GetByIdAsync(orderId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.OrderId.Should().Be(orderId);
        retrieved.Status.Should().Be("Open");
        retrieved.CustomerName.Should().Be("Jane Doe");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentEntry_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        // Act
        var retrieved = await index.GetByIdAsync(Guid.NewGuid());

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_ExistingEntry_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        await index.RegisterAsync(orderId, new TestOrderSummary(orderId, "Open", 50m, DateTime.UtcNow));

        // Act
        var exists = await index.ExistsAsync(orderId);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistentEntry_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        // Act
        var exists = await index.ExistsAsync(Guid.NewGuid());

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllEntries()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        await index.RegisterAsync(Guid.NewGuid(), new TestOrderSummary(Guid.NewGuid(), "Open", 100m, DateTime.UtcNow));
        await index.RegisterAsync(Guid.NewGuid(), new TestOrderSummary(Guid.NewGuid(), "Closed", 200m, DateTime.UtcNow));

        var countBefore = await index.GetCountAsync();
        countBefore.Should().Be(2);

        // Act
        await index.ClearAsync();

        // Assert
        var countAfter = await index.GetCountAsync();
        countAfter.Should().Be(0);
    }

    [Fact]
    public async Task DifferentScopes_ShouldMaintainSeparateIndexes()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId1 = Guid.NewGuid();
        var siteId2 = Guid.NewGuid();

        var index1 = GetIndexGrain(orgId, "orders", siteId1);
        var index2 = GetIndexGrain(orgId, "orders", siteId2);

        var orderId1 = Guid.NewGuid();
        var orderId2 = Guid.NewGuid();

        // Act
        await index1.RegisterAsync(orderId1, new TestOrderSummary(orderId1, "Open", 100m, DateTime.UtcNow));
        await index2.RegisterAsync(orderId2, new TestOrderSummary(orderId2, "Open", 200m, DateTime.UtcNow));

        // Assert
        var count1 = await index1.GetCountAsync();
        var count2 = await index2.GetCountAsync();

        count1.Should().Be(1);
        count2.Should().Be(1);

        var exists1InIndex1 = await index1.ExistsAsync(orderId1);
        var exists1InIndex2 = await index2.ExistsAsync(orderId1);

        exists1InIndex1.Should().BeTrue();
        exists1InIndex2.Should().BeFalse();
    }

    [Fact]
    public async Task MonthlyScope_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var expenseIndex = _fixture.Cluster.GrainFactory.GetGrain<IIndexGrain<TestOrderSummary>>(
            GrainKeys.Index(orgId, "expenses", 2024, 1)); // January 2024

        var expenseId = Guid.NewGuid();

        // Act
        await expenseIndex.RegisterAsync(expenseId, new TestOrderSummary(expenseId, "Approved", 500m, DateTime.UtcNow));

        // Assert
        var count = await expenseIndex.GetCountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public void GrainKeysIndex_ShouldGenerateCorrectFormat()
    {
        // Arrange
        var orgId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        var siteId = Guid.Parse("abcdefab-abcd-abcd-abcd-abcdefabcdef");

        // Act & Assert
        var siteKey = GrainKeys.Index(orgId, "orders", siteId);
        siteKey.Should().Be("org:12345678-1234-1234-1234-123456789012:index:orders:abcdefab-abcd-abcd-abcd-abcdefabcdef");

        var monthKey = GrainKeys.Index(orgId, "expenses", 2024, 1);
        monthKey.Should().Be("org:12345678-1234-1234-1234-123456789012:index:expenses:2024-01");

        var stringKey = GrainKeys.Index(orgId, "custom", "my-scope");
        stringKey.Should().Be("org:12345678-1234-1234-1234-123456789012:index:custom:my-scope");
    }

    [Fact]
    public void GrainKeysParseIndex_ShouldParseCorrectly()
    {
        // Arrange
        var orgId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        var key = "org:12345678-1234-1234-1234-123456789012:index:orders:site-456";

        // Act
        var (parsedOrgId, indexType, scope) = GrainKeys.ParseIndex(key);

        // Assert
        parsedOrgId.Should().Be(orgId);
        indexType.Should().Be("orders");
        scope.Should().Be("site-456");
    }

    [Fact]
    public void GrainKeysParseIndex_WithColonsInScope_ShouldPreserveScope()
    {
        // Arrange
        var key = "org:12345678-1234-1234-1234-123456789012:index:custom:scope:with:colons";

        // Act
        var (_, _, scope) = GrainKeys.ParseIndex(key);

        // Assert
        scope.Should().Be("scope:with:colons");
    }

    [Fact]
    public void GrainKeysParseIndex_InvalidFormat_ShouldThrow()
    {
        // Arrange
        var invalidKey = "invalid:key:format";

        // Act
        var act = () => GrainKeys.ParseIndex(invalidKey);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task ComplexQuery_MultipleConditions_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        var now = DateTime.UtcNow;
        await index.RegisterAsync(Guid.NewGuid(), new TestOrderSummary(Guid.NewGuid(), "Open", 50m, now, "Alice"));
        await index.RegisterAsync(Guid.NewGuid(), new TestOrderSummary(Guid.NewGuid(), "Open", 150m, now, "Bob"));
        await index.RegisterAsync(Guid.NewGuid(), new TestOrderSummary(Guid.NewGuid(), "Closed", 200m, now, "Charlie"));
        await index.RegisterAsync(Guid.NewGuid(), new TestOrderSummary(Guid.NewGuid(), "Open", 80m, now, "Diana"));

        // Act - Open orders with total >= 100
        var results = await index.QueryAsync(s => s.Status == "Open" && s.Total >= 100m);

        // Assert
        results.Should().HaveCount(1);
        results[0].CustomerName.Should().Be("Bob");
    }
}
