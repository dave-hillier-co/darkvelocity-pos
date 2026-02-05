using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;
using Orleans.TestingHost;
using Xunit;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ProfitabilityDashboardGrainTests
{
    private readonly TestCluster _cluster;

    public ProfitabilityDashboardGrainTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    private IProfitabilityDashboardGrain GetDashboardGrain(Guid orgId, Guid siteId)
    {
        var key = GrainKeys.ProfitabilityDashboard(orgId, siteId);
        return _cluster.GrainFactory.GetGrain<IProfitabilityDashboardGrain>(key);
    }

    [Fact]
    public async Task InitializeAsync_ShouldInitializeDashboard()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetDashboardGrain(orgId, siteId);

        // Act
        await grain.InitializeAsync(new InitializeProfitabilityDashboardCommand(
            orgId, siteId, "Test Site"));

        // Assert - should not throw when getting dashboard
        var dashboard = await grain.GetDashboardAsync(new DateRange(
            DateTime.UtcNow.AddMonths(-1),
            DateTime.UtcNow));

        dashboard.Should().NotBeNull();
        dashboard.OrgId.Should().Be(orgId);
        dashboard.SiteId.Should().Be(siteId);
    }

    [Fact]
    public async Task RecordItemCostDataAsync_ShouldRecordItemData()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var grain = GetDashboardGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeProfitabilityDashboardCommand(
            orgId, siteId, "Test Site"));

        // Act
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            itemId,
            "Grilled Salmon",
            "Main Course",
            SellingPrice: 25.99m,
            TheoreticalCost: 8.50m,
            ActualCost: 9.00m,
            UnitsSold: 50,
            TotalRevenue: 1299.50m,
            RecordedDate: DateTime.UtcNow));

        // Assert
        var item = await grain.GetItemProfitabilityAsync(itemId);
        item.Should().NotBeNull();
        item!.ItemName.Should().Be("Grilled Salmon");
        item.Category.Should().Be("Main Course");
        item.UnitsSold.Should().Be(50);
    }

    [Fact]
    public async Task RecordItemCostDataAsync_ShouldAccumulateSales()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var grain = GetDashboardGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeProfitabilityDashboardCommand(
            orgId, siteId, "Test Site"));

        // Act - record sales twice
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            itemId,
            "Burger",
            "Food",
            SellingPrice: 15.00m,
            TheoreticalCost: 5.00m,
            ActualCost: 5.50m,
            UnitsSold: 100,
            TotalRevenue: 1500.00m,
            RecordedDate: DateTime.UtcNow));

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            itemId,
            "Burger",
            "Food",
            SellingPrice: 15.00m,
            TheoreticalCost: 5.00m,
            ActualCost: 5.50m,
            UnitsSold: 50,
            TotalRevenue: 750.00m,
            RecordedDate: DateTime.UtcNow));

        // Assert
        var item = await grain.GetItemProfitabilityAsync(itemId);
        item.Should().NotBeNull();
        item!.UnitsSold.Should().Be(150);
        item.TotalRevenue.Should().Be(2250.00m);
    }

    [Fact]
    public async Task RecordDailyCostSummaryAsync_ShouldRecordTrend()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetDashboardGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeProfitabilityDashboardCommand(
            orgId, siteId, "Test Site"));

        var today = DateTime.UtcNow.Date;

        // Act
        await grain.RecordDailyCostSummaryAsync(new RecordDailyCostSummaryCommand(
            today,
            FoodCostPercent: 28.5m,
            BeverageCostPercent: 22.0m,
            TotalCost: 5000.00m,
            TotalRevenue: 18000.00m));

        // Assert
        var trends = await grain.GetCostTrendsAsync(new DateRange(
            today.AddDays(-1),
            today.AddDays(1)));

        trends.Should().HaveCount(1);
        trends[0].FoodCostPercent.Should().Be(28.5m);
        trends[0].BeverageCostPercent.Should().Be(22.0m);
    }

    [Fact]
    public async Task GetDashboardAsync_ShouldCalculateMetrics()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetDashboardGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeProfitabilityDashboardCommand(
            orgId, siteId, "Test Site"));

        var today = DateTime.UtcNow;

        // Add some item data
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(),
            "High Margin Item",
            "Food",
            SellingPrice: 20.00m,
            TheoreticalCost: 5.00m,
            ActualCost: 5.50m,
            UnitsSold: 100,
            TotalRevenue: 2000.00m,
            RecordedDate: today));

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(),
            "Low Margin Item",
            "Food",
            SellingPrice: 10.00m,
            TheoreticalCost: 7.00m,
            ActualCost: 7.50m,
            UnitsSold: 50,
            TotalRevenue: 500.00m,
            RecordedDate: today));

        // Act
        var dashboard = await grain.GetDashboardAsync(new DateRange(
            today.AddDays(-1),
            today.AddDays(1)));

        // Assert
        dashboard.TotalRevenue.Should().Be(2500.00m);
        // Total cost = 100 * 5.50 + 50 * 7.50 = 550 + 375 = 925
        dashboard.TotalCost.Should().Be(925.00m);
        // Gross profit = 2500 - 925 = 1575
        dashboard.GrossProfit.Should().Be(1575.00m);
    }

    [Fact]
    public async Task GetCategoryBreakdownAsync_ShouldGroupByCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetDashboardGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeProfitabilityDashboardCommand(
            orgId, siteId, "Test Site"));

        var today = DateTime.UtcNow;

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(),
            "Burger",
            "Food",
            SellingPrice: 15.00m,
            TheoreticalCost: 5.00m,
            ActualCost: 5.50m,
            UnitsSold: 100,
            TotalRevenue: 1500.00m,
            RecordedDate: today));

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(),
            "Beer",
            "Beverage",
            SellingPrice: 6.00m,
            TheoreticalCost: 1.50m,
            ActualCost: 1.75m,
            UnitsSold: 200,
            TotalRevenue: 1200.00m,
            RecordedDate: today));

        // Act
        var breakdown = await grain.GetCategoryBreakdownAsync();

        // Assert
        breakdown.Should().HaveCount(2);
        breakdown.Should().Contain(c => c.Category == "Food");
        breakdown.Should().Contain(c => c.Category == "Beverage");
    }

    [Fact]
    public async Task GetTopMarginItemsAsync_ShouldReturnHighestMarginItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetDashboardGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeProfitabilityDashboardCommand(
            orgId, siteId, "Test Site"));

        var today = DateTime.UtcNow;

        // High margin item: (20 - 5) / 20 = 75%
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(),
            "High Margin Item",
            "Food",
            SellingPrice: 20.00m,
            TheoreticalCost: 5.00m,
            ActualCost: 5.00m,
            UnitsSold: 10,
            TotalRevenue: 200.00m,
            RecordedDate: today));

        // Low margin item: (10 - 8) / 10 = 20%
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(),
            "Low Margin Item",
            "Food",
            SellingPrice: 10.00m,
            TheoreticalCost: 8.00m,
            ActualCost: 8.00m,
            UnitsSold: 10,
            TotalRevenue: 100.00m,
            RecordedDate: today));

        // Act
        var topItems = await grain.GetTopMarginItemsAsync(10);

        // Assert
        topItems.Should().HaveCount(2);
        topItems[0].ItemName.Should().Be("High Margin Item");
        topItems[0].ContributionMarginPercent.Should().Be(75m);
    }

    [Fact]
    public async Task GetBottomMarginItemsAsync_ShouldReturnLowestMarginItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetDashboardGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeProfitabilityDashboardCommand(
            orgId, siteId, "Test Site"));

        var today = DateTime.UtcNow;

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(),
            "High Margin Item",
            "Food",
            SellingPrice: 20.00m,
            TheoreticalCost: 5.00m,
            ActualCost: 5.00m,
            UnitsSold: 10,
            TotalRevenue: 200.00m,
            RecordedDate: today));

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(),
            "Low Margin Item",
            "Food",
            SellingPrice: 10.00m,
            TheoreticalCost: 8.00m,
            ActualCost: 8.00m,
            UnitsSold: 10,
            TotalRevenue: 100.00m,
            RecordedDate: today));

        // Act
        var bottomItems = await grain.GetBottomMarginItemsAsync(10);

        // Assert
        bottomItems.Should().HaveCount(2);
        bottomItems[0].ItemName.Should().Be("Low Margin Item");
        bottomItems[0].ContributionMarginPercent.Should().Be(20m);
    }

    [Fact]
    public async Task GetTopVarianceItemsAsync_ShouldReturnHighestVarianceItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetDashboardGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeProfitabilityDashboardCommand(
            orgId, siteId, "Test Site"));

        var today = DateTime.UtcNow;

        // High variance: actual 6.00 vs theoretical 5.00 = 20% variance
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(),
            "High Variance Item",
            "Food",
            SellingPrice: 20.00m,
            TheoreticalCost: 5.00m,
            ActualCost: 6.00m,
            UnitsSold: 100,
            TotalRevenue: 2000.00m,
            RecordedDate: today));

        // Low variance: actual 5.10 vs theoretical 5.00 = 2% variance
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(),
            "Low Variance Item",
            "Food",
            SellingPrice: 20.00m,
            TheoreticalCost: 5.00m,
            ActualCost: 5.10m,
            UnitsSold: 100,
            TotalRevenue: 2000.00m,
            RecordedDate: today));

        // Act
        var varianceItems = await grain.GetTopVarianceItemsAsync(10);

        // Assert
        varianceItems.Should().HaveCount(2);
        // High variance should be first (sorted by total variance)
        varianceItems[0].ItemName.Should().Be("High Variance Item");
        varianceItems[0].VariancePercent.Should().Be(20m);
    }

    [Fact]
    public async Task GetItemProfitabilityAsync_NonExistentItem_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetDashboardGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeProfitabilityDashboardCommand(
            orgId, siteId, "Test Site"));

        // Act
        var item = await grain.GetItemProfitabilityAsync(Guid.NewGuid());

        // Assert
        item.Should().BeNull();
    }

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllData()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var grain = GetDashboardGrain(orgId, siteId);

        await grain.InitializeAsync(new InitializeProfitabilityDashboardCommand(
            orgId, siteId, "Test Site"));

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            itemId,
            "Test Item",
            "Food",
            SellingPrice: 10.00m,
            TheoreticalCost: 3.00m,
            ActualCost: 3.50m,
            UnitsSold: 100,
            TotalRevenue: 1000.00m,
            RecordedDate: DateTime.UtcNow));

        // Act
        await grain.ClearAsync();

        // Assert
        var item = await grain.GetItemProfitabilityAsync(itemId);
        item.Should().BeNull();

        var breakdown = await grain.GetCategoryBreakdownAsync();
        breakdown.Should().BeEmpty();
    }

    [Fact]
    public async Task Operations_OnUninitializedGrain_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetDashboardGrain(orgId, siteId);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
                Guid.NewGuid(),
                "Test Item",
                "Food",
                SellingPrice: 10.00m,
                TheoreticalCost: 3.00m,
                ActualCost: 3.50m,
                UnitsSold: 100,
                TotalRevenue: 1000.00m,
                RecordedDate: DateTime.UtcNow)));
    }
}
