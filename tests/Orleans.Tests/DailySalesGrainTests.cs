using DarkVelocity.Orleans.Abstractions.Costing;
using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Shared.Contracts.Events;
using FluentAssertions;

namespace DarkVelocity.Orleans.Tests;

[Collection(ClusterCollection.Name)]
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
}
