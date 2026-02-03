using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class IndexGrainTests
{
    private readonly TestClusterFixture _fixture;

    public IndexGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IIndexGrain<OrderSummary> GetIndexGrain(Guid orgId, string indexType, string scope)
    {
        var key = GrainKeys.Index(orgId, indexType, scope);
        return _fixture.Cluster.GrainFactory.GetGrain<IIndexGrain<OrderSummary>>(key);
    }

    private IIndexGrain<OrderSummary> GetIndexGrain(Guid orgId, string indexType, Guid siteId)
    {
        var key = GrainKeys.Index(orgId, indexType, siteId);
        return _fixture.Cluster.GrainFactory.GetGrain<IIndexGrain<OrderSummary>>(key);
    }

    private static OrderSummary CreateOrderSummary(
        Guid orderId,
        OrderStatus status = OrderStatus.Open,
        decimal total = 100m,
        DateTime? createdAt = null,
        string? customerName = null)
    {
        return new OrderSummary(
            OrderId: orderId,
            OrderNumber: $"ORD-{orderId.ToString()[..8]}",
            SiteId: Guid.NewGuid(),
            Status: status,
            Type: OrderType.DineIn,
            CustomerId: customerName != null ? Guid.NewGuid() : null,
            CustomerName: customerName,
            ServerId: null,
            ServerName: null,
            TableNumber: null,
            ItemCount: 1,
            Subtotal: total,
            GrandTotal: total,
            PaidAmount: 0m,
            BalanceDue: total,
            CreatedAt: createdAt ?? DateTime.UtcNow,
            ClosedAt: null);
    }

    [Fact]
    public async Task RegisterAsync_ShouldAddEntryToIndex()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        var summary = CreateOrderSummary(orderId, OrderStatus.Open, 99.99m, customerName: "John Doe");

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

        var initialSummary = CreateOrderSummary(orderId, OrderStatus.Open, 99.99m);
        await index.RegisterAsync(orderId, initialSummary);

        // Act
        var updatedSummary = CreateOrderSummary(orderId, OrderStatus.Closed, 99.99m);
        await index.RegisterAsync(orderId, updatedSummary);

        // Assert
        var count = await index.GetCountAsync();
        count.Should().Be(1);

        var retrieved = await index.GetByIdAsync(orderId);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(OrderStatus.Closed);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateExistingEntry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        var initialSummary = CreateOrderSummary(orderId, OrderStatus.Open, 50m);
        await index.RegisterAsync(orderId, initialSummary);

        // Act
        var updatedSummary = CreateOrderSummary(orderId, OrderStatus.Paid, 50m);
        await index.UpdateAsync(orderId, updatedSummary);

        // Assert
        var retrieved = await index.GetByIdAsync(orderId);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveEntryFromIndex()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        var summary = CreateOrderSummary(orderId, OrderStatus.Open, 75m);
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
    public async Task GetAllAsync_ShouldAllowClientSideFiltering()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        var now = DateTime.UtcNow;
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        var id4 = Guid.NewGuid();
        await index.RegisterAsync(id1, CreateOrderSummary(id1, OrderStatus.Open, 100m, now));
        await index.RegisterAsync(id2, CreateOrderSummary(id2, OrderStatus.Closed, 200m, now));
        await index.RegisterAsync(id3, CreateOrderSummary(id3, OrderStatus.Open, 150m, now));
        await index.RegisterAsync(id4, CreateOrderSummary(id4, OrderStatus.Paid, 75m, now));

        // Act - filter on client side
        var all = await index.GetAllAsync();
        var openOrders = all.Where(e => e.Summary.Status == OrderStatus.Open).ToList();

        // Assert
        openOrders.Should().HaveCount(2);
        openOrders.Should().AllSatisfy(o => o.Summary.Status.Should().Be(OrderStatus.Open));
    }

    [Fact]
    public async Task GetAllAsync_ShouldAllowClientSideFilteringByTotal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        var id4 = Guid.NewGuid();
        await index.RegisterAsync(id1, CreateOrderSummary(id1, OrderStatus.Open, 50m));
        await index.RegisterAsync(id2, CreateOrderSummary(id2, OrderStatus.Open, 150m));
        await index.RegisterAsync(id3, CreateOrderSummary(id3, OrderStatus.Open, 250m));
        await index.RegisterAsync(id4, CreateOrderSummary(id4, OrderStatus.Open, 75m));

        // Act - filter on client side
        var all = await index.GetAllAsync();
        var highValueOrders = all.Where(e => e.Summary.GrandTotal >= 100m).ToList();

        // Assert
        highValueOrders.Should().HaveCount(2);
        highValueOrders.Should().AllSatisfy(o => o.Summary.GrandTotal.Should().BeGreaterThanOrEqualTo(100m));
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

        await index.RegisterAsync(orderId1, CreateOrderSummary(orderId1, OrderStatus.Open, 10m, DateTime.UtcNow.AddHours(-2)));
        await Task.Delay(10); // Ensure different timestamps
        await index.RegisterAsync(orderId2, CreateOrderSummary(orderId2, OrderStatus.Sent, 20m, DateTime.UtcNow.AddHours(-1)));
        await Task.Delay(10);
        await index.RegisterAsync(orderId3, CreateOrderSummary(orderId3, OrderStatus.Paid, 30m, DateTime.UtcNow));

        // Act
        var recent = await index.GetRecentAsync(3);

        // Assert
        recent.Should().HaveCount(3);
        recent[0].Status.Should().Be(OrderStatus.Paid); // Most recent first
        recent[2].Status.Should().Be(OrderStatus.Open); // Oldest last
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
            var id = Guid.NewGuid();
            await index.RegisterAsync(id, CreateOrderSummary(id, OrderStatus.Open, i * 5m));
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

        await index.RegisterAsync(orderId1, CreateOrderSummary(orderId1, OrderStatus.Open, 100m));
        await index.RegisterAsync(orderId2, CreateOrderSummary(orderId2, OrderStatus.Closed, 200m));

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

        var summary = CreateOrderSummary(orderId, OrderStatus.Open, 99.99m, customerName: "Jane Doe");
        await index.RegisterAsync(orderId, summary);

        // Act
        var retrieved = await index.GetByIdAsync(orderId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.OrderId.Should().Be(orderId);
        retrieved.Status.Should().Be(OrderStatus.Open);
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

        await index.RegisterAsync(orderId, CreateOrderSummary(orderId, OrderStatus.Open, 50m));

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

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        await index.RegisterAsync(id1, CreateOrderSummary(id1, OrderStatus.Open, 100m));
        await index.RegisterAsync(id2, CreateOrderSummary(id2, OrderStatus.Closed, 200m));

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
        await index1.RegisterAsync(orderId1, CreateOrderSummary(orderId1, OrderStatus.Open, 100m));
        await index2.RegisterAsync(orderId2, CreateOrderSummary(orderId2, OrderStatus.Open, 200m));

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
        var expenseIndex = _fixture.Cluster.GrainFactory.GetGrain<IIndexGrain<OrderSummary>>(
            GrainKeys.Index(orgId, "expenses", 2024, 1)); // January 2024

        var expenseId = Guid.NewGuid();

        // Act
        await expenseIndex.RegisterAsync(expenseId, CreateOrderSummary(expenseId, OrderStatus.Closed, 500m));

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
    public async Task GetAllAsync_ComplexClientSideFiltering_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var index = GetIndexGrain(orgId, "orders", siteId);

        var now = DateTime.UtcNow;
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        var id4 = Guid.NewGuid();
        await index.RegisterAsync(id1, CreateOrderSummary(id1, OrderStatus.Open, 50m, now, "Alice"));
        await index.RegisterAsync(id2, CreateOrderSummary(id2, OrderStatus.Open, 150m, now, "Bob"));
        await index.RegisterAsync(id3, CreateOrderSummary(id3, OrderStatus.Closed, 200m, now, "Charlie"));
        await index.RegisterAsync(id4, CreateOrderSummary(id4, OrderStatus.Open, 80m, now, "Diana"));

        // Act - Open orders with total >= 100, filtered on client side
        var all = await index.GetAllAsync();
        var results = all
            .Where(e => e.Summary.Status == OrderStatus.Open && e.Summary.GrandTotal >= 100m)
            .Select(e => e.Summary)
            .ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].CustomerName.Should().Be("Bob");
    }
}
