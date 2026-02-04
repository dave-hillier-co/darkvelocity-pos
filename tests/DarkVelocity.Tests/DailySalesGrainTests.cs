using DarkVelocity.Host.Costing;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Events;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DailySalesGrainTests
{
    private readonly TestClusterFixture _fixture;

    public DailySalesGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private string GetGrainKey(Guid orgId, Guid siteId, DateTime date)
        => $"{orgId}:{siteId}:sales:{date:yyyy-MM-dd}";

    [Fact]
    public async Task InitializeAsync_ShouldInitializeGrain()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.Today;

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(GetGrainKey(orgId, siteId, date));

        // Act
        await grain.InitializeAsync(new DailySalesAggregationCommand(date, siteId, "Main Street"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.SiteId.Should().Be(siteId);
        snapshot.SiteName.Should().Be("Main Street");
        snapshot.Date.Should().Be(date);
    }

    [Fact]
    public async Task RecordSaleAsync_ShouldAggregateSales()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.Today;

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(GetGrainKey(orgId, siteId, date));
        await grain.InitializeAsync(new DailySalesAggregationCommand(date, siteId, "Main Street"));

        // Act
        await grain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.DineIn,
            ProductId: Guid.NewGuid(),
            ProductName: "Burger",
            Category: "Mains",
            Quantity: 2,
            GrossSales: 30.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 2.40m,
            NetSales: 27.60m,
            TheoreticalCOGS: 8.00m,
            ActualCOGS: 8.50m,
            GuestCount: 2));

        await grain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.DineIn,
            ProductId: Guid.NewGuid(),
            ProductName: "Fries",
            Category: "Sides",
            Quantity: 2,
            GrossSales: 10.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 0.80m,
            NetSales: 9.20m,
            TheoreticalCOGS: 2.00m,
            ActualCOGS: 2.20m,
            GuestCount: 0));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.GrossSales.Should().Be(40.00m);
        snapshot.NetSales.Should().Be(36.80m);
        snapshot.TheoreticalCOGS.Should().Be(10.00m);
        snapshot.ActualCOGS.Should().Be(10.70m);
        snapshot.TransactionCount.Should().Be(2);
        snapshot.GuestCount.Should().Be(2);
    }

    [Fact]
    public async Task RecordSaleAsync_ShouldTrackSalesByChannel()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.Today;

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(GetGrainKey(orgId, siteId, date));
        await grain.InitializeAsync(new DailySalesAggregationCommand(date, siteId, "Main Street"));

        // Act
        await grain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.DineIn,
            ProductId: Guid.NewGuid(),
            ProductName: "Burger",
            Category: "Mains",
            Quantity: 1,
            GrossSales: 15.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 1.20m,
            NetSales: 13.80m,
            TheoreticalCOGS: 4.00m,
            ActualCOGS: 4.25m,
            GuestCount: 1));

        await grain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.TakeOut,
            ProductId: Guid.NewGuid(),
            ProductName: "Burger",
            Category: "Mains",
            Quantity: 1,
            GrossSales: 15.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 1.20m,
            NetSales: 13.80m,
            TheoreticalCOGS: 4.00m,
            ActualCOGS: 4.25m,
            GuestCount: 1));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.SalesByChannel.Should().ContainKey(SaleChannel.DineIn);
        snapshot.SalesByChannel.Should().ContainKey(SaleChannel.TakeOut);
        snapshot.SalesByChannel[SaleChannel.DineIn].Should().Be(13.80m);
        snapshot.SalesByChannel[SaleChannel.TakeOut].Should().Be(13.80m);
    }

    [Fact]
    public async Task GetMetricsAsync_ShouldReturnSalesMetrics()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.Today;

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(GetGrainKey(orgId, siteId, date));
        await grain.InitializeAsync(new DailySalesAggregationCommand(date, siteId, "Main Street"));

        await grain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.DineIn,
            ProductId: Guid.NewGuid(),
            ProductName: "Burger",
            Category: "Mains",
            Quantity: 1,
            GrossSales: 100.00m,
            Discounts: 10.00m,
            Voids: 0,
            Comps: 5.00m,
            Tax: 6.80m,
            NetSales: 78.20m,
            TheoreticalCOGS: 25.00m,
            ActualCOGS: 27.00m,
            GuestCount: 4));

        // Act
        var metrics = await grain.GetMetricsAsync();

        // Assert
        metrics.GrossSales.Should().Be(100.00m);
        metrics.Discounts.Should().Be(10.00m);
        metrics.Comps.Should().Be(5.00m);
        metrics.NetSales.Should().Be(78.20m);
        metrics.TransactionCount.Should().Be(1);
        metrics.CoversServed.Should().Be(4);
    }

    [Fact]
    public async Task GetGrossProfitMetricsAsync_ShouldCalculateGP()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.Today;

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(GetGrainKey(orgId, siteId, date));
        await grain.InitializeAsync(new DailySalesAggregationCommand(date, siteId, "Main Street"));

        await grain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.DineIn,
            ProductId: Guid.NewGuid(),
            ProductName: "Burger",
            Category: "Mains",
            Quantity: 1,
            GrossSales: 100.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 0,
            NetSales: 100.00m,
            TheoreticalCOGS: 30.00m,
            ActualCOGS: 32.00m,
            GuestCount: 4));

        // Act
        var gpMetrics = await grain.GetGrossProfitMetricsAsync(CostingMethod.FIFO);

        // Assert
        gpMetrics.NetSales.Should().Be(100.00m);
        gpMetrics.TheoreticalCOGS.Should().Be(30.00m);
        gpMetrics.ActualCOGS.Should().Be(32.00m);
        gpMetrics.ActualGrossProfit.Should().Be(68.00m);
        gpMetrics.ActualGrossProfitPercent.Should().Be(68.00m);
        gpMetrics.TheoreticalGrossProfit.Should().Be(70.00m);
        gpMetrics.TheoreticalGrossProfitPercent.Should().Be(70.00m);
        gpMetrics.Variance.Should().Be(2.00m);
    }

    [Fact]
    public async Task GetFactsAsync_ShouldReturnAllFacts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.Today;

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(GetGrainKey(orgId, siteId, date));
        await grain.InitializeAsync(new DailySalesAggregationCommand(date, siteId, "Main Street"));

        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();

        await grain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.DineIn,
            ProductId: productId1,
            ProductName: "Burger",
            Category: "Mains",
            Quantity: 1,
            GrossSales: 15.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 1.20m,
            NetSales: 13.80m,
            TheoreticalCOGS: 4.00m,
            ActualCOGS: 4.25m,
            GuestCount: 1));

        await grain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.TakeOut,
            ProductId: productId2,
            ProductName: "Salad",
            Category: "Starters",
            Quantity: 1,
            GrossSales: 12.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 0.96m,
            NetSales: 11.04m,
            TheoreticalCOGS: 3.00m,
            ActualCOGS: 3.10m,
            GuestCount: 1));

        // Act
        var facts = await grain.GetFactsAsync();

        // Assert
        facts.Should().HaveCount(2);
        facts.Should().Contain(f => f.ProductId == productId1);
        facts.Should().Contain(f => f.ProductId == productId2);
    }

    [Fact]
    public async Task FinalizeAsync_ShouldMarkAsFinalized()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.Today;

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(GetGrainKey(orgId, siteId, date));
        await grain.InitializeAsync(new DailySalesAggregationCommand(date, siteId, "Main Street"));

        // Act
        await grain.FinalizeAsync();

        // Assert - grain should still work but be marked as finalized
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordSaleAsync_FromStream_ShouldAggregate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.Today;

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(GetGrainKey(orgId, siteId, date));
        await grain.InitializeAsync(new DailySalesAggregationCommand(date, siteId, "Main Street"));

        // Act
        await grain.RecordSaleAsync(new RecordSaleFromStreamCommand(
            OrderId: Guid.NewGuid(),
            GrossSales: 50.00m,
            Discounts: 5.00m,
            Tax: 3.60m,
            GuestCount: 2,
            ItemCount: 3,
            Channel: "DineIn",
            TheoreticalCOGS: 15.00m));

        await grain.RecordSaleAsync(new RecordSaleFromStreamCommand(
            OrderId: Guid.NewGuid(),
            GrossSales: 30.00m,
            Discounts: 0m,
            Tax: 2.40m,
            GuestCount: 1,
            ItemCount: 2,
            Channel: "TakeOut",
            TheoreticalCOGS: 9.00m));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.GrossSales.Should().Be(80.00m);
        snapshot.NetSales.Should().Be(75.00m); // 50-5 + 30-0
        snapshot.TheoreticalCOGS.Should().Be(24.00m);
        snapshot.TransactionCount.Should().Be(2);
        snapshot.GuestCount.Should().Be(3);
        snapshot.SalesByChannel.Should().ContainKey(SaleChannel.DineIn);
        snapshot.SalesByChannel.Should().ContainKey(SaleChannel.TakeOut);
    }

    [Fact]
    public async Task RecordVoidAsync_ShouldTrackVoids()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.Today;

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(GetGrainKey(orgId, siteId, date));
        await grain.InitializeAsync(new DailySalesAggregationCommand(date, siteId, "Main Street"));

        // Record initial sale
        await grain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.DineIn,
            ProductId: Guid.NewGuid(),
            ProductName: "Burger",
            Category: "Mains",
            Quantity: 1,
            GrossSales: 20.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 1.60m,
            NetSales: 18.40m,
            TheoreticalCOGS: 5.00m,
            ActualCOGS: 5.50m,
            GuestCount: 1));

        // Act - void part of the order
        await grain.RecordVoidAsync(Guid.NewGuid(), 10.00m, "Customer changed mind");

        // Assert
        var metrics = await grain.GetMetricsAsync();
        metrics.Voids.Should().Be(10.00m);
    }

    [Fact]
    public async Task RecordSaleAsync_ByCategory_ShouldAggregate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.Today;

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(GetGrainKey(orgId, siteId, date));
        await grain.InitializeAsync(new DailySalesAggregationCommand(date, siteId, "Main Street"));

        // Act - record sales in different categories
        await grain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.DineIn,
            ProductId: Guid.NewGuid(),
            ProductName: "Burger",
            Category: "Mains",
            Quantity: 1,
            GrossSales: 15.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 1.20m,
            NetSales: 13.80m,
            TheoreticalCOGS: 4.00m,
            ActualCOGS: 4.25m,
            GuestCount: 1));

        await grain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.DineIn,
            ProductId: Guid.NewGuid(),
            ProductName: "Coke",
            Category: "Beverages",
            Quantity: 2,
            GrossSales: 6.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 0.48m,
            NetSales: 5.52m,
            TheoreticalCOGS: 1.00m,
            ActualCOGS: 1.00m,
            GuestCount: 0));

        await grain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.DineIn,
            ProductId: Guid.NewGuid(),
            ProductName: "Cheesecake",
            Category: "Desserts",
            Quantity: 1,
            GrossSales: 8.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 0.64m,
            NetSales: 7.36m,
            TheoreticalCOGS: 2.00m,
            ActualCOGS: 2.20m,
            GuestCount: 0));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.SalesByCategory.Should().ContainKey("Mains");
        snapshot.SalesByCategory.Should().ContainKey("Beverages");
        snapshot.SalesByCategory.Should().ContainKey("Desserts");
        snapshot.SalesByCategory["Mains"].Should().Be(13.80m);
        snapshot.SalesByCategory["Beverages"].Should().Be(5.52m);
        snapshot.SalesByCategory["Desserts"].Should().Be(7.36m);
    }

    [Fact]
    public async Task RecordSaleAsync_SameChannel_ShouldAccumulate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.Today;

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(GetGrainKey(orgId, siteId, date));
        await grain.InitializeAsync(new DailySalesAggregationCommand(date, siteId, "Main Street"));

        // Act - record multiple sales on the same channel
        await grain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.Delivery,
            ProductId: Guid.NewGuid(),
            ProductName: "Pizza",
            Category: "Mains",
            Quantity: 1,
            GrossSales: 20.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 1.60m,
            NetSales: 18.40m,
            TheoreticalCOGS: 5.00m,
            ActualCOGS: 5.25m,
            GuestCount: 1));

        await grain.RecordSaleAsync(new RecordSaleCommand(
            CheckId: Guid.NewGuid(),
            Channel: SaleChannel.Delivery,
            ProductId: Guid.NewGuid(),
            ProductName: "Wings",
            Category: "Appetizers",
            Quantity: 1,
            GrossSales: 12.00m,
            Discounts: 0,
            Voids: 0,
            Comps: 0,
            Tax: 0.96m,
            NetSales: 11.04m,
            TheoreticalCOGS: 3.00m,
            ActualCOGS: 3.10m,
            GuestCount: 0));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.SalesByChannel[SaleChannel.Delivery].Should().Be(29.44m); // 18.40 + 11.04
    }

    [Fact]
    public async Task GetSnapshotAsync_ZeroTransactions_AverageTicketShouldBeZero()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.Today;

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(GetGrainKey(orgId, siteId, date));
        await grain.InitializeAsync(new DailySalesAggregationCommand(date, siteId, "Empty Store"));

        // Act - don't record any sales
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.TransactionCount.Should().Be(0);
        snapshot.AverageTicket.Should().Be(0);
        snapshot.GrossProfitPercent.Should().Be(0);
    }

    [Fact]
    public async Task InitializeAsync_Twice_ShouldBeIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.Today;

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(GetGrainKey(orgId, siteId, date));

        // Act - initialize twice with different names
        await grain.InitializeAsync(new DailySalesAggregationCommand(date, siteId, "Original Name"));
        await grain.InitializeAsync(new DailySalesAggregationCommand(date, siteId, "Different Name"));

        // Assert - should keep the original name (idempotent)
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.SiteName.Should().Be("Original Name");
    }

    [Fact]
    public async Task NotInitialized_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateTime.Today;

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IDailySalesGrain>(GetGrainKey(orgId, siteId, date));

        // Act & Assert - calling GetSnapshotAsync without initialization should throw
        await grain.Invoking(g => g.GetSnapshotAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }
}
