using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;
using Orleans.TestingHost;
using Xunit;

namespace DarkVelocity.Tests.Grains.Costing;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ProfitabilityDashboardGrainTests
{
    private readonly TestCluster _cluster;

    public ProfitabilityDashboardGrainTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    // ============================================================================
    // Initialization Tests
    // ============================================================================

    // Given: a new profitability dashboard grain with no prior state
    // When: the dashboard is initialized with organization and site identifiers
    // Then: the dashboard correctly reports the organization and site IDs
    [Fact]
    public async Task InitializeAsync_ShouldSetSiteIdAndOrgId()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IProfitabilityDashboardGrain>(
            GrainKeys.ProfitabilityDashboard(orgId, siteId));

        var command = new InitializeProfitabilityDashboardCommand(
            orgId,
            siteId,
            "Test Restaurant");

        // Act
        await grain.InitializeAsync(command);

        // Assert - verify by getting dashboard (which requires initialization)
        var range = new DateRange(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        var dashboard = await grain.GetDashboardAsync(range);

        dashboard.OrgId.Should().Be(orgId);
        dashboard.SiteId.Should().Be(siteId);
    }

    // Given: an initialized dashboard with existing item cost data
    // When: initialization is called again with a different site name
    // Then: existing data is preserved and the dashboard remains functional
    [Fact]
    public async Task InitializeAsync_AlreadyInitialized_ShouldBeIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IProfitabilityDashboardGrain>(
            GrainKeys.ProfitabilityDashboard(orgId, siteId));

        await grain.InitializeAsync(new InitializeProfitabilityDashboardCommand(
            orgId, siteId, "Original Site Name"));

        // Add some data
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Test Item", "Food", 10.00m, 3.00m, 3.50m, 5, 50.00m, DateTime.UtcNow));

        // Act - try to re-initialize
        await grain.InitializeAsync(new InitializeProfitabilityDashboardCommand(
            orgId, siteId, "New Site Name"));

        // Assert - data should still be there
        var breakdown = await grain.GetCategoryBreakdownAsync();
        breakdown.Should().HaveCount(1);
        breakdown[0].Category.Should().Be("Food");
    }

    // ============================================================================
    // Operations On Uninitialized Grain Tests
    // ============================================================================

    // Given: a profitability dashboard grain that has never been initialized
    // When: the dashboard is requested
    // Then: an InvalidOperationException is thrown indicating the dashboard is not initialized
    [Fact]
    public async Task GetDashboardAsync_NotInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IProfitabilityDashboardGrain>(
            GrainKeys.ProfitabilityDashboard(orgId, siteId));

        var range = new DateRange(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        // Act
        var act = () => grain.GetDashboardAsync(range);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Profitability dashboard not initialized");
    }

    // Given: a profitability dashboard grain that has never been initialized
    // When: item cost data recording is attempted
    // Then: an InvalidOperationException is thrown indicating the dashboard is not initialized
    [Fact]
    public async Task RecordItemCostDataAsync_NotInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IProfitabilityDashboardGrain>(
            GrainKeys.ProfitabilityDashboard(orgId, siteId));

        var command = new RecordItemCostDataCommand(
            Guid.NewGuid(), "Test Item", "Food", 10.00m, 3.00m, 3.50m, 5, 50.00m, DateTime.UtcNow);

        // Act
        var act = () => grain.RecordItemCostDataAsync(command);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Profitability dashboard not initialized");
    }

    // Given: a profitability dashboard grain that has never been initialized
    // When: a daily cost summary recording is attempted
    // Then: an InvalidOperationException is thrown indicating the dashboard is not initialized
    [Fact]
    public async Task RecordDailyCostSummaryAsync_NotInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IProfitabilityDashboardGrain>(
            GrainKeys.ProfitabilityDashboard(orgId, siteId));

        var command = new RecordDailyCostSummaryCommand(
            DateTime.UtcNow, 30m, 25m, 1000m, 3000m);

        // Act
        var act = () => grain.RecordDailyCostSummaryAsync(command);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Profitability dashboard not initialized");
    }

    // ============================================================================
    // RecordItemCostData Tests
    // ============================================================================

    // Given: an initialized profitability dashboard with no existing items
    // When: cost data for a new grilled salmon item is recorded
    // Then: the item's name, category, pricing, costs, units, and revenue are persisted
    [Fact]
    public async Task RecordItemCostDataAsync_NewItem_ShouldAddItem()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var itemId = Guid.NewGuid();
        var recordedDate = DateTime.UtcNow;

        var command = new RecordItemCostDataCommand(
            itemId,
            "Grilled Salmon",
            "Food - Entree",
            25.00m,
            7.50m,
            8.00m,
            10,
            250.00m,
            recordedDate);

        // Act
        await grain.RecordItemCostDataAsync(command);

        // Assert
        var item = await grain.GetItemProfitabilityAsync(itemId);
        item.Should().NotBeNull();
        item!.ItemName.Should().Be("Grilled Salmon");
        item.Category.Should().Be("Food - Entree");
        item.SellingPrice.Should().Be(25.00m);
        item.TheoreticalCost.Should().Be(7.50m);
        item.ActualCost.Should().Be(8.00m);
        item.UnitsSold.Should().Be(10);
        item.TotalRevenue.Should().Be(250.00m);
    }

    // Given: a grilled salmon item already recorded with 10 units sold
    // When: additional sales of 5 units are recorded for the same item at an updated price
    // Then: units and revenue accumulate while pricing and cost reflect the latest values
    [Fact]
    public async Task RecordItemCostDataAsync_ExistingItem_ShouldUpdateAndAccumulateUnits()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var itemId = Guid.NewGuid();
        var recordedDate = DateTime.UtcNow;

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            itemId, "Grilled Salmon", "Food - Entree",
            25.00m, 7.50m, 8.00m, 10, 250.00m, recordedDate));

        // Act - record more sales of the same item
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            itemId, "Grilled Salmon", "Food - Entree",
            26.00m, 7.75m, 8.25m, 5, 130.00m, recordedDate));

        // Assert
        var item = await grain.GetItemProfitabilityAsync(itemId);
        item.Should().NotBeNull();
        item!.SellingPrice.Should().Be(26.00m); // Updated to new price
        item.TheoreticalCost.Should().Be(7.75m); // Updated
        item.ActualCost.Should().Be(8.25m); // Updated
        item.UnitsSold.Should().Be(15); // Accumulated: 10 + 5
        item.TotalRevenue.Should().Be(380.00m); // Accumulated: 250 + 130
    }

    // Given: an initialized profitability dashboard
    // When: more than 5000 item records are added
    // Then: the oldest items are trimmed to keep the total at or below the 5000 record limit
    [Fact]
    public async Task RecordItemCostDataAsync_ExceedsMaxRecords_ShouldTrimOldestItems()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var baseDate = DateTime.UtcNow;

        // Add 5001 items (exceeds max of 5000)
        for (int i = 0; i < 5001; i++)
        {
            await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
                Guid.NewGuid(),
                $"Item {i}",
                "Food",
                10.00m,
                3.00m,
                3.50m,
                1,
                10.00m,
                baseDate.AddMinutes(i)));
        }

        // Act
        var breakdown = await grain.GetCategoryBreakdownAsync();

        // Assert - should have trimmed to max records
        breakdown.Should().HaveCount(1);
        breakdown[0].ItemCount.Should().BeLessOrEqualTo(5000);
    }

    // ============================================================================
    // RecordDailyCostSummary Tests
    // ============================================================================

    // Given: an initialized profitability dashboard with no daily summaries
    // When: a daily cost summary is recorded with food/beverage cost percentages and totals
    // Then: the summary is retrievable in cost trends for that date
    [Fact]
    public async Task RecordDailyCostSummaryAsync_NewDay_ShouldAddSummary()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var date = DateTime.UtcNow.Date;

        var command = new RecordDailyCostSummaryCommand(
            date,
            28.5m,
            22.0m,
            1500.00m,
            5000.00m);

        // Act
        await grain.RecordDailyCostSummaryAsync(command);

        // Assert
        var range = new DateRange(date.AddDays(-1), date.AddDays(1));
        var trends = await grain.GetCostTrendsAsync(range);
        trends.Should().HaveCount(1);
        trends[0].Date.Should().Be(date);
        trends[0].FoodCostPercent.Should().Be(28.5m);
        trends[0].BeverageCostPercent.Should().Be(22.0m);
        trends[0].TotalCost.Should().Be(1500.00m);
        trends[0].TotalRevenue.Should().Be(5000.00m);
    }

    // Given: a daily cost summary already recorded for today
    // When: a new summary is recorded for the same date with updated figures
    // Then: the previous summary is replaced with the updated values
    [Fact]
    public async Task RecordDailyCostSummaryAsync_SameDay_ShouldReplaceSummary()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var date = DateTime.UtcNow.Date;

        await grain.RecordDailyCostSummaryAsync(new RecordDailyCostSummaryCommand(
            date, 28.5m, 22.0m, 1500.00m, 5000.00m));

        // Act - update the same day
        await grain.RecordDailyCostSummaryAsync(new RecordDailyCostSummaryCommand(
            date, 30.0m, 24.0m, 1800.00m, 6000.00m));

        // Assert
        var range = new DateRange(date.AddDays(-1), date.AddDays(1));
        var trends = await grain.GetCostTrendsAsync(range);
        trends.Should().HaveCount(1);
        trends[0].FoodCostPercent.Should().Be(30.0m);
        trends[0].BeverageCostPercent.Should().Be(24.0m);
        trends[0].TotalCost.Should().Be(1800.00m);
        trends[0].TotalRevenue.Should().Be(6000.00m);
    }

    // Given: an initialized profitability dashboard
    // When: 400 days of daily cost summaries are recorded
    // Then: the oldest summaries are trimmed to keep the total at or below 365 days
    [Fact]
    public async Task RecordDailyCostSummaryAsync_Over365Days_ShouldTrim()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var startDate = DateTime.UtcNow.Date.AddDays(-400);

        // Add 400 days of data
        for (int i = 0; i < 400; i++)
        {
            await grain.RecordDailyCostSummaryAsync(new RecordDailyCostSummaryCommand(
                startDate.AddDays(i), 30m, 25m, 1000m, 3000m));
        }

        // Act
        var range = new DateRange(startDate, startDate.AddDays(400));
        var trends = await grain.GetCostTrendsAsync(range);

        // Assert - should have trimmed to max 365 points
        trends.Count.Should().BeLessOrEqualTo(365);
    }

    // ============================================================================
    // GetDashboard Tests
    // ============================================================================

    // Given: a food item and a beverage item with known costs, prices, and sales volumes
    // When: the profitability dashboard is retrieved
    // Then: total revenue, cost, gross profit, margin percentage, and category cost percentages are correctly calculated
    [Fact]
    public async Task GetDashboardAsync_WithItems_ShouldCalculateCorrectMetrics()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        // Add food item: Selling: 20, Actual Cost: 6, Units: 10, Revenue: 200
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Burger", "Food - Main",
            20.00m, 5.00m, 6.00m, 10, 200.00m, recordedDate));

        // Add beverage item: Selling: 8, Actual Cost: 2, Units: 15, Revenue: 120
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Craft Beer", "Beverage - Beer",
            8.00m, 1.50m, 2.00m, 15, 120.00m, recordedDate));

        // Act
        var range = new DateRange(recordedDate.AddDays(-1), recordedDate.AddDays(1));
        var dashboard = await grain.GetDashboardAsync(range);

        // Assert
        // Total Revenue = 200 + 120 = 320
        dashboard.TotalRevenue.Should().Be(320.00m);

        // Total Cost = (6 * 10) + (2 * 15) = 60 + 30 = 90
        dashboard.TotalCost.Should().Be(90.00m);

        // Gross Profit = 320 - 90 = 230
        dashboard.GrossProfit.Should().Be(230.00m);

        // Gross Profit Margin = 230 / 320 * 100 = 71.875%
        dashboard.GrossProfitMarginPercent.Should().BeApproximately(71.875m, 0.01m);

        // Overall Cost Percent = 90 / 320 * 100 = 28.125%
        dashboard.OverallCostPercent.Should().BeApproximately(28.125m, 0.01m);

        // Food Cost Percent = 60 / 200 * 100 = 30%
        dashboard.FoodCostPercent.Should().Be(30.00m);

        // Beverage Cost Percent = 30 / 120 * 100 = 25%
        dashboard.BeverageCostPercent.Should().Be(25.00m);
    }

    // Given: an item with theoretical cost of $5 and actual cost of $6 per unit
    // When: the profitability dashboard is retrieved
    // Then: the variance ($10 total) and variance percentage (20%) are correctly calculated
    [Fact]
    public async Task GetDashboardAsync_WithVariance_ShouldCalculateTheoreticalVsActual()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        // Item with cost variance: Theoretical: 5, Actual: 6, Units: 10
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Test Item", "Food",
            20.00m, 5.00m, 6.00m, 10, 200.00m, recordedDate));

        // Act
        var range = new DateRange(recordedDate.AddDays(-1), recordedDate.AddDays(1));
        var dashboard = await grain.GetDashboardAsync(range);

        // Assert
        // Total Theoretical Cost = 5 * 10 = 50
        dashboard.TotalTheoreticalCost.Should().Be(50.00m);

        // Total Actual Cost = 6 * 10 = 60
        dashboard.TotalActualCost.Should().Be(60.00m);

        // Total Variance = 60 - 50 = 10
        dashboard.TotalVariance.Should().Be(10.00m);

        // Variance Percent = 10 / 50 * 100 = 20%
        dashboard.TotalVariancePercent.Should().Be(20.00m);
    }

    // Given: an initialized profitability dashboard with no recorded items
    // When: the dashboard is retrieved
    // Then: all financial metrics (revenue, cost, profit, margins, variance) return zero
    [Fact]
    public async Task GetDashboardAsync_NoItems_ShouldReturnZeroMetrics()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var range = new DateRange(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        // Act
        var dashboard = await grain.GetDashboardAsync(range);

        // Assert
        dashboard.TotalRevenue.Should().Be(0m);
        dashboard.TotalCost.Should().Be(0m);
        dashboard.GrossProfit.Should().Be(0m);
        dashboard.GrossProfitMarginPercent.Should().Be(0m);
        dashboard.OverallCostPercent.Should().Be(0m);
        dashboard.FoodCostPercent.Should().Be(0m);
        dashboard.BeverageCostPercent.Should().Be(0m);
        dashboard.TotalVariance.Should().Be(0m);
        dashboard.TotalVariancePercent.Should().Be(0m);
    }

    // Given: items recorded on different dates (two days ago and yesterday)
    // When: the dashboard is queried for yesterday's date range only
    // Then: only the item recorded yesterday is included in the metrics
    [Fact]
    public async Task GetDashboardAsync_FiltersByDateRange()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Old Item", "Food", 10.00m, 3.00m, 3.50m, 5, 50.00m, twoDaysAgo));

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Recent Item", "Food", 15.00m, 4.00m, 4.50m, 10, 150.00m, yesterday));

        // Act - query only for yesterday
        var range = new DateRange(yesterday, today);
        var dashboard = await grain.GetDashboardAsync(range);

        // Assert - should only include "Recent Item"
        dashboard.TotalRevenue.Should().Be(150.00m);
        dashboard.TotalCost.Should().Be(45.00m); // 4.50 * 10
    }

    // ============================================================================
    // Category Classification Tests
    // ============================================================================

    // Given: items categorized under various food-related names (Appetizer, Entree, Dessert, etc.)
    // When: the profitability dashboard is retrieved
    // Then: all items are classified as food with the correct combined food cost percentage
    [Fact]
    public async Task GetDashboardAsync_CorrectlyClassifiesFoodCategories()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        // Add various food categories
        var foodCategories = new[] { "Food", "Appetizer", "Entree", "Main Course", "Side Dish", "Dessert", "Salad", "Soup" };
        foreach (var category in foodCategories)
        {
            await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
                Guid.NewGuid(), $"Test {category}", category,
                10.00m, 3.00m, 3.00m, 1, 10.00m, recordedDate));
        }

        // Act
        var range = new DateRange(recordedDate.AddDays(-1), recordedDate.AddDays(1));
        var dashboard = await grain.GetDashboardAsync(range);

        // Assert - all items should be classified as food
        // Total Food Revenue = 80, Total Food Cost = 24, Food Cost % = 30%
        dashboard.FoodCostPercent.Should().Be(30.00m);
        dashboard.BeverageCostPercent.Should().Be(0m); // No beverages
    }

    // Given: items categorized under various beverage-related names (Beer, Wine, Cocktails, Coffee, etc.)
    // When: the profitability dashboard is retrieved
    // Then: all items are classified as beverages with the correct combined beverage cost percentage
    [Fact]
    public async Task GetDashboardAsync_CorrectlyClassifiesBeverageCategories()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        // Add various beverage categories
        var beverageCategories = new[] { "Beverage", "Drinks", "Beer", "Wine", "Cocktails", "Spirits", "Coffee", "Tea", "Soda", "Juice" };
        foreach (var category in beverageCategories)
        {
            await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
                Guid.NewGuid(), $"Test {category}", category,
                8.00m, 2.00m, 2.00m, 1, 8.00m, recordedDate));
        }

        // Act
        var range = new DateRange(recordedDate.AddDays(-1), recordedDate.AddDays(1));
        var dashboard = await grain.GetDashboardAsync(range);

        // Assert - all items should be classified as beverage
        // Total Beverage Revenue = 80, Total Beverage Cost = 20, Beverage Cost % = 25%
        dashboard.BeverageCostPercent.Should().Be(25.00m);
        dashboard.FoodCostPercent.Should().Be(0m); // No food
    }

    // ============================================================================
    // GetCategoryBreakdown Tests
    // ============================================================================

    // Given: two mains items and one drinks item recorded with known costs and sales
    // When: the category breakdown is retrieved
    // Then: items are grouped by category with correct item counts, revenue, cost, and margin percentages
    [Fact]
    public async Task GetCategoryBreakdownAsync_ShouldGroupByCategory()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Burger", "Mains", 15.00m, 4.00m, 4.50m, 10, 150.00m, recordedDate));

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Steak", "Mains", 30.00m, 10.00m, 11.00m, 5, 150.00m, recordedDate));

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Beer", "Drinks", 6.00m, 1.50m, 1.50m, 20, 120.00m, recordedDate));

        // Act
        var breakdown = await grain.GetCategoryBreakdownAsync();

        // Assert
        breakdown.Should().HaveCount(2);

        var mains = breakdown.First(c => c.Category == "Mains");
        mains.ItemCount.Should().Be(2);
        mains.UnitsSold.Should().Be(15);
        mains.TotalRevenue.Should().Be(300.00m);
        // Total Cost = (4.50 * 10) + (11.00 * 5) = 45 + 55 = 100
        mains.TotalCost.Should().Be(100.00m);
        mains.Contribution.Should().Be(200.00m); // 300 - 100
        mains.CostPercent.Should().BeApproximately(33.33m, 0.1m); // 100 / 300 * 100
        mains.ContributionMarginPercent.Should().BeApproximately(66.67m, 0.1m); // 200 / 300 * 100

        var drinks = breakdown.First(c => c.Category == "Drinks");
        drinks.ItemCount.Should().Be(1);
        drinks.UnitsSold.Should().Be(20);
        drinks.TotalRevenue.Should().Be(120.00m);
        drinks.TotalCost.Should().Be(30.00m); // 1.50 * 20
    }

    // Given: items in three categories with distinct revenue totals
    // When: the category breakdown is retrieved
    // Then: categories are ordered by total revenue descending
    [Fact]
    public async Task GetCategoryBreakdownAsync_OrderedByRevenue()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Item1", "Low Revenue", 5.00m, 1.00m, 1.00m, 2, 10.00m, recordedDate));

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Item2", "High Revenue", 50.00m, 10.00m, 10.00m, 10, 500.00m, recordedDate));

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Item3", "Medium Revenue", 20.00m, 5.00m, 5.00m, 5, 100.00m, recordedDate));

        // Act
        var breakdown = await grain.GetCategoryBreakdownAsync();

        // Assert - should be ordered by TotalRevenue descending
        breakdown.Should().HaveCount(3);
        breakdown[0].Category.Should().Be("High Revenue");
        breakdown[1].Category.Should().Be("Medium Revenue");
        breakdown[2].Category.Should().Be("Low Revenue");
    }

    // ============================================================================
    // GetItemProfitability Tests
    // ============================================================================

    // Given: a premium steak item with known theoretical cost ($12), actual cost ($14), and 20 units sold
    // When: item profitability is retrieved
    // Then: contribution margin, margin percentage, variance, and total contribution are correctly calculated
    [Fact]
    public async Task GetItemProfitabilityAsync_ExistingItem_ShouldReturnCalculatedProfitability()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var itemId = Guid.NewGuid();
        var recordedDate = DateTime.UtcNow;

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            itemId, "Premium Steak", "Mains",
            45.00m, 12.00m, 14.00m, 20, 900.00m, recordedDate));

        // Act
        var profitability = await grain.GetItemProfitabilityAsync(itemId);

        // Assert
        profitability.Should().NotBeNull();
        profitability!.ItemId.Should().Be(itemId);
        profitability.ItemName.Should().Be("Premium Steak");
        profitability.Category.Should().Be("Mains");
        profitability.SellingPrice.Should().Be(45.00m);
        profitability.TheoreticalCost.Should().Be(12.00m);
        profitability.ActualCost.Should().Be(14.00m);

        // Contribution Margin = 45 - 14 = 31
        profitability.ContributionMargin.Should().Be(31.00m);

        // Contribution Margin % = 31 / 45 * 100 = 68.89%
        profitability.ContributionMarginPercent.Should().BeApproximately(68.89m, 0.1m);

        // Variance = 14 - 12 = 2
        profitability.Variance.Should().Be(2.00m);

        // Variance % = 2 / 12 * 100 = 16.67%
        profitability.VariancePercent.Should().BeApproximately(16.67m, 0.1m);

        profitability.UnitsSold.Should().Be(20);
        profitability.TotalRevenue.Should().Be(900.00m);

        // Total Contribution = 31 * 20 = 620
        profitability.TotalContribution.Should().Be(620.00m);
    }

    // Given: an initialized profitability dashboard with no matching item
    // When: profitability is requested for a non-existent item ID
    // Then: null is returned
    [Fact]
    public async Task GetItemProfitabilityAsync_NonExistingItem_ShouldReturnNull()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var nonExistentId = Guid.NewGuid();

        // Act
        var profitability = await grain.GetItemProfitabilityAsync(nonExistentId);

        // Assert
        profitability.Should().BeNull();
    }

    // Given: a free sample item with zero selling price
    // When: item profitability is retrieved
    // Then: the contribution margin percentage is zero without division errors
    [Fact]
    public async Task GetItemProfitabilityAsync_ZeroSellingPrice_ShouldHandleGracefully()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var itemId = Guid.NewGuid();

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            itemId, "Free Sample", "Samples",
            0m, 1.00m, 1.00m, 5, 0m, DateTime.UtcNow));

        // Act
        var profitability = await grain.GetItemProfitabilityAsync(itemId);

        // Assert
        profitability.Should().NotBeNull();
        profitability!.ContributionMarginPercent.Should().Be(0m);
    }

    // Given: a digital item with zero theoretical and actual cost
    // When: item profitability is retrieved
    // Then: the variance percentage is zero without division errors
    [Fact]
    public async Task GetItemProfitabilityAsync_ZeroTheoreticalCost_ShouldHandleGracefully()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var itemId = Guid.NewGuid();

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            itemId, "No Cost Item", "Digital",
            10.00m, 0m, 0m, 5, 50.00m, DateTime.UtcNow));

        // Act
        var profitability = await grain.GetItemProfitabilityAsync(itemId);

        // Assert
        profitability.Should().NotBeNull();
        profitability!.VariancePercent.Should().Be(0m);
    }

    // ============================================================================
    // GetCostTrends Tests
    // ============================================================================

    // Given: daily cost summaries recorded out of chronological order
    // When: cost trends are retrieved
    // Then: the trends are returned ordered by date ascending
    [Fact]
    public async Task GetCostTrendsAsync_MultipleDays_ShouldReturnOrderedByDate()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var today = DateTime.UtcNow.Date;

        // Add trends out of order
        await grain.RecordDailyCostSummaryAsync(new RecordDailyCostSummaryCommand(
            today.AddDays(-2), 28m, 22m, 800m, 2500m));
        await grain.RecordDailyCostSummaryAsync(new RecordDailyCostSummaryCommand(
            today, 32m, 26m, 1000m, 3000m));
        await grain.RecordDailyCostSummaryAsync(new RecordDailyCostSummaryCommand(
            today.AddDays(-1), 30m, 24m, 900m, 2800m));

        // Act
        var range = new DateRange(today.AddDays(-3), today.AddDays(1));
        var trends = await grain.GetCostTrendsAsync(range);

        // Assert - should be ordered by date ascending
        trends.Should().HaveCount(3);
        trends[0].Date.Should().Be(today.AddDays(-2));
        trends[1].Date.Should().Be(today.AddDays(-1));
        trends[2].Date.Should().Be(today);
    }

    // Given: daily cost summaries spanning five days
    // When: cost trends are queried for a three-day window
    // Then: only summaries within the requested date range are returned
    [Fact]
    public async Task GetCostTrendsAsync_FiltersByDateRange()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var today = DateTime.UtcNow.Date;

        await grain.RecordDailyCostSummaryAsync(new RecordDailyCostSummaryCommand(
            today.AddDays(-5), 28m, 22m, 800m, 2500m));
        await grain.RecordDailyCostSummaryAsync(new RecordDailyCostSummaryCommand(
            today.AddDays(-2), 30m, 24m, 900m, 2800m));
        await grain.RecordDailyCostSummaryAsync(new RecordDailyCostSummaryCommand(
            today, 32m, 26m, 1000m, 3000m));

        // Act - query only recent days
        var range = new DateRange(today.AddDays(-3), today.AddDays(1));
        var trends = await grain.GetCostTrendsAsync(range);

        // Assert - should only include trends within the range
        trends.Should().HaveCount(2);
        trends.Should().NotContain(t => t.Date == today.AddDays(-5));
    }

    // Given: a daily cost summary with $900 cost and $3000 revenue
    // When: cost trends are retrieved
    // Then: the overall cost percentage is calculated as 30%
    [Fact]
    public async Task GetCostTrendsAsync_CalculatesOverallCostPercent()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var today = DateTime.UtcNow.Date;

        await grain.RecordDailyCostSummaryAsync(new RecordDailyCostSummaryCommand(
            today, 28m, 22m, 900m, 3000m)); // Overall: 900/3000 = 30%

        // Act
        var range = new DateRange(today.AddDays(-1), today.AddDays(1));
        var trends = await grain.GetCostTrendsAsync(range);

        // Assert
        trends.Should().HaveCount(1);
        trends[0].OverallCostPercent.Should().Be(30m);
    }

    // Given: a daily cost summary recorded for a zero-revenue day
    // When: cost trends are retrieved
    // Then: the overall cost percentage is zero without division errors
    [Fact]
    public async Task GetCostTrendsAsync_ZeroRevenue_ShouldHandleGracefully()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var today = DateTime.UtcNow.Date;

        await grain.RecordDailyCostSummaryAsync(new RecordDailyCostSummaryCommand(
            today, 0m, 0m, 0m, 0m)); // Zero revenue day

        // Act
        var range = new DateRange(today.AddDays(-1), today.AddDays(1));
        var trends = await grain.GetCostTrendsAsync(range);

        // Assert
        trends.Should().HaveCount(1);
        trends[0].OverallCostPercent.Should().Be(0m); // Should not divide by zero
    }

    // ============================================================================
    // GetTopVarianceItems Tests
    // ============================================================================

    // Given: three items with small ($5), large ($10), and medium ($8) total cost variances
    // When: top variance items are retrieved
    // Then: items are ordered by total absolute variance descending
    [Fact]
    public async Task GetTopVarianceItemsAsync_ShouldReturnOrderedByAbsoluteVariance()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        // Small variance: (3.5 - 3) * 10 = 5
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Small Variance", "Food",
            10.00m, 3.00m, 3.50m, 10, 100.00m, recordedDate));

        // Large variance: (12 - 10) * 5 = 10
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Large Variance", "Food",
            30.00m, 10.00m, 12.00m, 5, 150.00m, recordedDate));

        // Medium variance: (5 - 4) * 8 = 8
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Medium Variance", "Food",
            15.00m, 4.00m, 5.00m, 8, 120.00m, recordedDate));

        // Act
        var varianceItems = await grain.GetTopVarianceItemsAsync(10);

        // Assert - should be ordered by absolute total variance descending
        varianceItems.Should().HaveCount(3);
        varianceItems[0].ItemName.Should().Be("Large Variance"); // Total: 10
        varianceItems[1].ItemName.Should().Be("Medium Variance"); // Total: 8
        varianceItems[2].ItemName.Should().Be("Small Variance"); // Total: 5
    }

    // Given: twenty items with varying cost variances
    // When: top 5 variance items are requested
    // Then: exactly five items are returned
    [Fact]
    public async Task GetTopVarianceItemsAsync_LimitedCount_ShouldReturnRequestedCount()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
                Guid.NewGuid(), $"Item {i}", "Food",
                10.00m, 3.00m, (3.00m + i * 0.1m), 5, 50.00m, recordedDate));
        }

        // Act
        var varianceItems = await grain.GetTopVarianceItemsAsync(5);

        // Assert
        varianceItems.Should().HaveCount(5);
    }

    // Given: one item with sales and one item with zero sales, both having cost variance
    // When: top variance items are retrieved
    // Then: only the item with actual sales is included
    [Fact]
    public async Task GetTopVarianceItemsAsync_ItemsWithZeroSales_ShouldBeExcluded()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        // Item with sales
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Has Sales", "Food",
            10.00m, 3.00m, 4.00m, 5, 50.00m, recordedDate));

        // Item with zero sales
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "No Sales", "Food",
            10.00m, 3.00m, 4.00m, 0, 0m, recordedDate));

        // Act
        var varianceItems = await grain.GetTopVarianceItemsAsync(10);

        // Assert
        varianceItems.Should().HaveCount(1);
        varianceItems[0].ItemName.Should().Be("Has Sales");
    }

    // Given: an item with theoretical cost $5, actual cost $6.50, and 10 units sold
    // When: top variance items are retrieved
    // Then: per-unit variance ($1.50), variance percent (30%), and total variance ($15) are correctly calculated
    [Fact]
    public async Task GetTopVarianceItemsAsync_ShouldCalculateCorrectVarianceMetrics()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Test Item", "Food",
            20.00m, 5.00m, 6.50m, 10, 200.00m, recordedDate));

        // Act
        var varianceItems = await grain.GetTopVarianceItemsAsync(10);

        // Assert
        varianceItems.Should().HaveCount(1);
        var item = varianceItems[0];
        item.TheoreticalCost.Should().Be(5.00m);
        item.ActualCost.Should().Be(6.50m);
        item.VarianceAmount.Should().Be(1.50m); // 6.50 - 5.00
        item.VariancePercent.Should().Be(30m); // 1.50 / 5.00 * 100
        item.UnitsSold.Should().Be(10);
        item.TotalVariance.Should().Be(15.00m); // 1.50 * 10
    }

    // ============================================================================
    // GetTopMarginItems Tests
    // ============================================================================

    // Given: three items with contribution margins of 30%, 60%, and 80%
    // When: top margin items are retrieved
    // Then: items are ordered by contribution margin percentage descending
    [Fact]
    public async Task GetTopMarginItemsAsync_ShouldReturnOrderedByMarginPercent()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        // Low margin: (10 - 7) / 10 = 30%
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Low Margin", "Food",
            10.00m, 6.00m, 7.00m, 5, 50.00m, recordedDate));

        // High margin: (10 - 2) / 10 = 80%
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "High Margin", "Food",
            10.00m, 1.50m, 2.00m, 5, 50.00m, recordedDate));

        // Medium margin: (10 - 4) / 10 = 60%
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Medium Margin", "Food",
            10.00m, 3.50m, 4.00m, 5, 50.00m, recordedDate));

        // Act
        var topMarginItems = await grain.GetTopMarginItemsAsync(10);

        // Assert - should be ordered by contribution margin percent descending
        topMarginItems.Should().HaveCount(3);
        topMarginItems[0].ItemName.Should().Be("High Margin");
        topMarginItems[1].ItemName.Should().Be("Medium Margin");
        topMarginItems[2].ItemName.Should().Be("Low Margin");
    }

    // Given: one item with sales and one item with zero sales
    // When: top margin items are retrieved
    // Then: only the item with actual sales is included
    [Fact]
    public async Task GetTopMarginItemsAsync_ItemsWithZeroSales_ShouldBeExcluded()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Has Sales", "Food",
            10.00m, 3.00m, 4.00m, 5, 50.00m, recordedDate));

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "No Sales", "Food",
            10.00m, 3.00m, 4.00m, 0, 0m, recordedDate));

        // Act
        var topMarginItems = await grain.GetTopMarginItemsAsync(10);

        // Assert
        topMarginItems.Should().HaveCount(1);
        topMarginItems[0].ItemName.Should().Be("Has Sales");
    }

    // Given: twenty items with varying margin percentages
    // When: top 5 margin items are requested
    // Then: exactly five items are returned
    [Fact]
    public async Task GetTopMarginItemsAsync_LimitedCount_ShouldReturnRequestedCount()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
                Guid.NewGuid(), $"Item {i}", "Food",
                (10.00m + i), 3.00m, 3.00m, 5, 50.00m, recordedDate));
        }

        // Act
        var topMarginItems = await grain.GetTopMarginItemsAsync(5);

        // Assert
        topMarginItems.Should().HaveCount(5);
    }

    // ============================================================================
    // GetBottomMarginItems Tests
    // ============================================================================

    // Given: three items with contribution margins of 30%, 60%, and 80%
    // When: bottom margin items are retrieved
    // Then: items are ordered by contribution margin percentage ascending (lowest first)
    [Fact]
    public async Task GetBottomMarginItemsAsync_ShouldReturnOrderedByMarginPercentAscending()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        // Low margin: (10 - 7) / 10 = 30%
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Low Margin", "Food",
            10.00m, 6.00m, 7.00m, 5, 50.00m, recordedDate));

        // High margin: (10 - 2) / 10 = 80%
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "High Margin", "Food",
            10.00m, 1.50m, 2.00m, 5, 50.00m, recordedDate));

        // Medium margin: (10 - 4) / 10 = 60%
        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Medium Margin", "Food",
            10.00m, 3.50m, 4.00m, 5, 50.00m, recordedDate));

        // Act
        var bottomMarginItems = await grain.GetBottomMarginItemsAsync(10);

        // Assert - should be ordered by contribution margin percent ascending
        bottomMarginItems.Should().HaveCount(3);
        bottomMarginItems[0].ItemName.Should().Be("Low Margin");
        bottomMarginItems[1].ItemName.Should().Be("Medium Margin");
        bottomMarginItems[2].ItemName.Should().Be("High Margin");
    }

    // Given: one item with sales and one item with zero sales
    // When: bottom margin items are retrieved
    // Then: only the item with actual sales is included
    [Fact]
    public async Task GetBottomMarginItemsAsync_ItemsWithZeroSales_ShouldBeExcluded()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Has Sales", "Food",
            10.00m, 3.00m, 4.00m, 5, 50.00m, recordedDate));

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "No Sales", "Food",
            10.00m, 3.00m, 4.00m, 0, 0m, recordedDate));

        // Act
        var bottomMarginItems = await grain.GetBottomMarginItemsAsync(10);

        // Assert
        bottomMarginItems.Should().HaveCount(1);
        bottomMarginItems[0].ItemName.Should().Be("Has Sales");
    }

    // Given: twenty items recorded on the profitability dashboard
    // When: bottom margin items are requested with default count
    // Then: exactly ten items are returned
    [Fact]
    public async Task GetBottomMarginItemsAsync_DefaultCount_ShouldBeTen()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
                Guid.NewGuid(), $"Item {i}", "Food",
                10.00m, 3.00m, 3.00m, 5, 50.00m, recordedDate));
        }

        // Act - use default count
        var bottomMarginItems = await grain.GetBottomMarginItemsAsync();

        // Assert
        bottomMarginItems.Should().HaveCount(10);
    }

    // ============================================================================
    // Clear Tests
    // ============================================================================

    // Given: a profitability dashboard with recorded item data and daily cost summaries
    // When: the dashboard data is cleared
    // Then: category breakdown and cost trends are both empty
    [Fact]
    public async Task ClearAsync_ShouldRemoveAllData()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Test Item", "Food",
            10.00m, 3.00m, 3.50m, 5, 50.00m, recordedDate));

        await grain.RecordDailyCostSummaryAsync(new RecordDailyCostSummaryCommand(
            recordedDate, 30m, 25m, 1000m, 3000m));

        // Act
        await grain.ClearAsync();

        // Assert
        var breakdown = await grain.GetCategoryBreakdownAsync();
        breakdown.Should().BeEmpty();

        var range = new DateRange(recordedDate.AddDays(-1), recordedDate.AddDays(1));
        var trends = await grain.GetCostTrendsAsync(range);
        trends.Should().BeEmpty();
    }

    // Given: an initialized profitability dashboard with recorded data
    // When: the dashboard data is cleared
    // Then: the dashboard remains initialized and operational without throwing errors
    [Fact]
    public async Task ClearAsync_ShouldPreserveInitialization()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IProfitabilityDashboardGrain>(
            GrainKeys.ProfitabilityDashboard(orgId, siteId));

        await grain.InitializeAsync(new InitializeProfitabilityDashboardCommand(
            orgId, siteId, "Test Site"));

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Test Item", "Food",
            10.00m, 3.00m, 3.50m, 5, 50.00m, DateTime.UtcNow));

        // Act
        await grain.ClearAsync();

        // Assert - should still be able to get dashboard (initialization preserved)
        var range = new DateRange(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        var act = async () => await grain.GetDashboardAsync(range);
        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    // ============================================================================
    // Dashboard Breakdown Lists Tests
    // ============================================================================

    // Given: fifteen menu items recorded on the profitability dashboard
    // When: the dashboard is retrieved
    // Then: the result includes top margin items, bottom margin items, and variance items lists
    [Fact]
    public async Task GetDashboardAsync_ShouldIncludeTopAndBottomMarginItemsInResult()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        for (int i = 1; i <= 15; i++)
        {
            await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
                Guid.NewGuid(), $"Item {i}", "Food",
                (10.00m + i * 2), 3.00m, 3.00m, 5, 50.00m, recordedDate));
        }

        // Act
        var range = new DateRange(recordedDate.AddDays(-1), recordedDate.AddDays(1));
        var dashboard = await grain.GetDashboardAsync(range);

        // Assert
        dashboard.TopMarginItems.Should().HaveCount(10);
        dashboard.BottomMarginItems.Should().HaveCount(10);
        dashboard.TopVarianceItems.Should().NotBeNull();
    }

    // Given: items in Mains and Drinks categories recorded on the dashboard
    // When: the dashboard is retrieved
    // Then: the category breakdown contains both categories
    [Fact]
    public async Task GetDashboardAsync_ShouldIncludeCategoryBreakdown()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var recordedDate = DateTime.UtcNow;

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Burger", "Mains",
            15.00m, 4.00m, 4.50m, 10, 150.00m, recordedDate));

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Beer", "Drinks",
            6.00m, 1.50m, 1.50m, 20, 120.00m, recordedDate));

        // Act
        var range = new DateRange(recordedDate.AddDays(-1), recordedDate.AddDays(1));
        var dashboard = await grain.GetDashboardAsync(range);

        // Assert
        dashboard.CategoryBreakdown.Should().HaveCount(2);
        dashboard.CategoryBreakdown.Should().Contain(c => c.Category == "Mains");
        dashboard.CategoryBreakdown.Should().Contain(c => c.Category == "Drinks");
    }

    // Given: three days of daily cost summaries recorded on the dashboard
    // When: the dashboard is retrieved for a date range covering all three days
    // Then: the cost trends contain all three daily summaries
    [Fact]
    public async Task GetDashboardAsync_ShouldIncludeCostTrends()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var today = DateTime.UtcNow.Date;

        await grain.RecordDailyCostSummaryAsync(new RecordDailyCostSummaryCommand(
            today.AddDays(-2), 28m, 22m, 800m, 2500m));
        await grain.RecordDailyCostSummaryAsync(new RecordDailyCostSummaryCommand(
            today.AddDays(-1), 30m, 24m, 900m, 2800m));
        await grain.RecordDailyCostSummaryAsync(new RecordDailyCostSummaryCommand(
            today, 32m, 26m, 1000m, 3000m));

        // Act
        var range = new DateRange(today.AddDays(-3), today.AddDays(1));
        var dashboard = await grain.GetDashboardAsync(range);

        // Assert
        dashboard.CostTrends.Should().HaveCount(3);
    }

    // ============================================================================
    // Edge Case Tests
    // ============================================================================

    // Given: an item where actual cost ($4) is less than theoretical cost ($5)
    // When: the item's profitability is retrieved
    // Then: the variance is negative (-$1) indicating favorable cost performance
    [Fact]
    public async Task RecordItemCostDataAsync_NegativeVariance_ShouldHandleCorrectly()
    {
        // Arrange - actual cost less than theoretical (favorable variance)
        var grain = await CreateInitializedGrain();
        var itemId = Guid.NewGuid();

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            itemId, "Efficient Item", "Food",
            10.00m, 5.00m, 4.00m, 10, 100.00m, DateTime.UtcNow)); // Actual < Theoretical

        // Act
        var profitability = await grain.GetItemProfitabilityAsync(itemId);

        // Assert
        profitability.Should().NotBeNull();
        profitability!.Variance.Should().Be(-1.00m); // 4 - 5 = -1 (favorable)
        profitability.VariancePercent.Should().Be(-20m); // -1 / 5 * 100
    }

    // Given: item data recorded two years ago
    // When: the dashboard is queried for the last 30 days
    // Then: all metrics return zero and breakdown lists are empty
    [Fact]
    public async Task GetDashboardAsync_DateRangeWithNoMatchingItems_ShouldReturnEmpty()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var oldDate = DateTime.UtcNow.AddYears(-2);

        await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
            Guid.NewGuid(), "Old Item", "Food",
            10.00m, 3.00m, 3.50m, 5, 50.00m, oldDate));

        // Act - query for recent date range
        var range = new DateRange(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        var dashboard = await grain.GetDashboardAsync(range);

        // Assert
        dashboard.TotalRevenue.Should().Be(0m);
        dashboard.TotalCost.Should().Be(0m);
        dashboard.CategoryBreakdown.Should().BeEmpty();
        dashboard.TopMarginItems.Should().BeEmpty();
        dashboard.BottomMarginItems.Should().BeEmpty();
    }

    // Given: the same menu item recorded five separate times with 2 units each
    // When: the item's profitability is retrieved
    // Then: units accumulate to 10 and revenue accumulates to the combined total
    [Fact]
    public async Task MultipleRecordsForSameItem_ShouldAccumulateCorrectly()
    {
        // Arrange
        var grain = await CreateInitializedGrain();
        var itemId = Guid.NewGuid();
        var baseDate = DateTime.UtcNow;

        // Add multiple records for the same item
        for (int i = 0; i < 5; i++)
        {
            await grain.RecordItemCostDataAsync(new RecordItemCostDataCommand(
                itemId, "Popular Item", "Food",
                10.00m, 3.00m, 3.50m, 2, 20.00m, baseDate.AddMinutes(i)));
        }

        // Act
        var profitability = await grain.GetItemProfitabilityAsync(itemId);

        // Assert - units and revenue should accumulate
        profitability.Should().NotBeNull();
        profitability!.UnitsSold.Should().Be(10); // 2 * 5
        profitability.TotalRevenue.Should().Be(100.00m); // 20 * 5
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

    private async Task<IProfitabilityDashboardGrain> CreateInitializedGrain()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IProfitabilityDashboardGrain>(
            GrainKeys.ProfitabilityDashboard(orgId, siteId));

        await grain.InitializeAsync(new InitializeProfitabilityDashboardCommand(
            orgId, siteId, "Test Restaurant"));

        return grain;
    }
}
