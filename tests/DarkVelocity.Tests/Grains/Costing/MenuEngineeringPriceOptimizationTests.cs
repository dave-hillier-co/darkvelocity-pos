using DarkVelocity.Host.Costing;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests.Grains.Costing;

/// <summary>
/// Tests for MenuEngineering price optimization suggestions and target margin features.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MenuEngineeringPriceOptimizationTests
{
    private readonly TestClusterFixture _fixture;

    public MenuEngineeringPriceOptimizationTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private string GetGrainKey(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:menu-engineering";

    // ============================================================================
    // Price Suggestion Tests
    // ============================================================================

    [Fact]
    public async Task GetPriceSuggestionsAsync_ItemsBelowTargetMargin_ShouldSuggestPriceIncrease()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var lowMarginItemId = Guid.NewGuid();

        // Item with 50% margin (cost $5, price $10) when target is 70%
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: lowMarginItemId,
            ProductName: "Low Margin Burger",
            Category: "Mains",
            SellingPrice: 10.00m,
            TheoreticalCost: 5.00m, // 50% margin
            UnitsSold: 100,
            TotalRevenue: 1000.00m));

        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Act
        var suggestions = await grain.GetPriceSuggestionsAsync(
            targetMarginPercent: 70m,
            maxPriceChangePercent: 50m);

        // Assert
        suggestions.Should().NotBeEmpty();
        var suggestion = suggestions.First(s => s.ProductId == lowMarginItemId);
        suggestion.CurrentMargin.Should().Be(50m);
        suggestion.TargetMargin.Should().Be(70m);
        suggestion.SuggestedPrice.Should().BeGreaterThan(suggestion.CurrentPrice);
        suggestion.SuggestionType.Should().Be(PriceSuggestionType.IncreaseToTargetMargin);
    }

    [Fact]
    public async Task GetPriceSuggestionsAsync_ItemsAtOrAboveTargetMargin_ShouldNotSuggestChange()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var highMarginItemId = Guid.NewGuid();

        // Item with 75% margin (cost $5, price $20) when target is 70%
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: highMarginItemId,
            ProductName: "High Margin Steak",
            Category: "Mains",
            SellingPrice: 20.00m,
            TheoreticalCost: 5.00m, // 75% margin
            UnitsSold: 50,
            TotalRevenue: 1000.00m));

        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Act
        var suggestions = await grain.GetPriceSuggestionsAsync(
            targetMarginPercent: 70m,
            maxPriceChangePercent: 20m);

        // Assert
        suggestions.Should().NotContain(s => s.ProductId == highMarginItemId);
    }

    [Fact]
    public async Task GetPriceSuggestionsAsync_ExceedingMaxPriceChange_ShouldNotSuggest()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var lowMarginItemId = Guid.NewGuid();

        // Item with very low margin requiring large price increase
        // Cost $8, Price $10, Margin 20%, Target 70%
        // To reach 70% margin: Price = 8 / (1 - 0.7) = 26.67
        // Price change = (26.67 - 10) / 10 * 100 = 166.7% - exceeds max
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: lowMarginItemId,
            ProductName: "Very Low Margin Item",
            Category: "Mains",
            SellingPrice: 10.00m,
            TheoreticalCost: 8.00m, // 20% margin
            UnitsSold: 50,
            TotalRevenue: 500.00m));

        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Act
        var suggestions = await grain.GetPriceSuggestionsAsync(
            targetMarginPercent: 70m,
            maxPriceChangePercent: 15m); // Very restrictive max change

        // Assert - should not suggest because price change would exceed 15%
        suggestions.Should().NotContain(s => s.ProductId == lowMarginItemId);
    }

    [Fact]
    public async Task GetPriceSuggestionsAsync_ShouldOrderByMarginGap()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        // Item 1: 60% margin (10% gap from 70% target)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Small Gap Item",
            Category: "Mains",
            SellingPrice: 25.00m,
            TheoreticalCost: 10.00m, // 60% margin
            UnitsSold: 50,
            TotalRevenue: 1250.00m));

        // Item 2: 50% margin (20% gap from 70% target)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Medium Gap Item",
            Category: "Mains",
            SellingPrice: 20.00m,
            TheoreticalCost: 10.00m, // 50% margin
            UnitsSold: 50,
            TotalRevenue: 1000.00m));

        // Item 3: 40% margin (30% gap from 70% target)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Large Gap Item",
            Category: "Mains",
            SellingPrice: 16.67m,
            TheoreticalCost: 10.00m, // ~40% margin
            UnitsSold: 50,
            TotalRevenue: 833.50m));

        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Act
        var suggestions = await grain.GetPriceSuggestionsAsync(
            targetMarginPercent: 70m,
            maxPriceChangePercent: 100m);

        // Assert - should be ordered by margin gap (largest gap first)
        suggestions.Should().HaveCount(3);
        suggestions[0].ProductName.Should().Be("Large Gap Item");
        suggestions[1].ProductName.Should().Be("Medium Gap Item");
        suggestions[2].ProductName.Should().Be("Small Gap Item");
    }

    // ============================================================================
    // Target Margin Setting Tests
    // ============================================================================

    [Fact]
    public async Task SetTargetMarginAsync_ShouldUpdateDefaultTargetMargin()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant", 65m));

        // Act
        await grain.SetTargetMarginAsync(72m);

        // Test by getting suggestions - they should use new target
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Test Item",
            Category: "Mains",
            SellingPrice: 20.00m,
            TheoreticalCost: 8.00m, // 60% margin
            UnitsSold: 50,
            TotalRevenue: 1000.00m));

        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        var suggestions = await grain.GetPriceSuggestionsAsync(72m);

        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions.First().TargetMargin.Should().Be(72m);
    }

    [Fact]
    public async Task SetCategoryTargetMarginAsync_ShouldSetCategorySpecificTarget()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant", 65m));

        // Act
        await grain.SetCategoryTargetMarginAsync("Beverages", 75m);
        await grain.SetCategoryTargetMarginAsync("Food", 60m);

        // Assert - verify by updating and checking the margin settings persist
        // The grain should now have category-specific targets
        // We can verify this by adding items and checking analysis

        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Cocktail",
            Category: "Beverages",
            SellingPrice: 12.00m,
            TheoreticalCost: 3.00m, // 75% margin
            UnitsSold: 100,
            TotalRevenue: 1200.00m));

        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        var analysis = await grain.GetCategoryAnalysisAsync();
        analysis.Should().Contain(c => c.Category == "Beverages");
    }

    // ============================================================================
    // Menu Mix and Contribution Analysis Tests
    // ============================================================================

    [Fact]
    public async Task AnalyzeAsync_ShouldCalculateMenuMixCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();

        // Item 1: 60 units sold out of 100 total = 60% menu mix
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: item1Id,
            ProductName: "Popular Item",
            Category: "Mains",
            SellingPrice: 20.00m,
            TheoreticalCost: 8.00m,
            UnitsSold: 60,
            TotalRevenue: 1200.00m));

        // Item 2: 40 units sold out of 100 total = 40% menu mix
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: item2Id,
            ProductName: "Less Popular Item",
            Category: "Mains",
            SellingPrice: 15.00m,
            TheoreticalCost: 5.00m,
            UnitsSold: 40,
            TotalRevenue: 600.00m));

        // Act
        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        var item1 = await grain.GetItemAsync(item1Id);
        var item2 = await grain.GetItemAsync(item2Id);

        item1!.MenuMix.Should().Be(60m);
        item2!.MenuMix.Should().Be(40m);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldCalculateRevenueMixCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();

        // Item 1: $800 out of $1000 total = 80% revenue mix
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: item1Id,
            ProductName: "High Revenue Item",
            Category: "Mains",
            SellingPrice: 40.00m,
            TheoreticalCost: 15.00m,
            UnitsSold: 20,
            TotalRevenue: 800.00m));

        // Item 2: $200 out of $1000 total = 20% revenue mix
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: item2Id,
            ProductName: "Low Revenue Item",
            Category: "Mains",
            SellingPrice: 10.00m,
            TheoreticalCost: 4.00m,
            UnitsSold: 20,
            TotalRevenue: 200.00m));

        // Act
        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        var item1 = await grain.GetItemAsync(item1Id);
        var item2 = await grain.GetItemAsync(item2Id);

        item1!.RevenueMix.Should().Be(80m);
        item2!.RevenueMix.Should().Be(20m);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldCalculateContributionMixCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();

        // Item 1: Margin $25, 20 units = $500 contribution (83.33%)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: item1Id,
            ProductName: "High Contribution Item",
            Category: "Mains",
            SellingPrice: 40.00m,
            TheoreticalCost: 15.00m, // $25 margin
            UnitsSold: 20,
            TotalRevenue: 800.00m));

        // Item 2: Margin $5, 20 units = $100 contribution (16.67%)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: item2Id,
            ProductName: "Low Contribution Item",
            Category: "Mains",
            SellingPrice: 10.00m,
            TheoreticalCost: 5.00m, // $5 margin
            UnitsSold: 20,
            TotalRevenue: 200.00m));

        // Act
        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        var item1 = await grain.GetItemAsync(item1Id);
        var item2 = await grain.GetItemAsync(item2Id);

        // Total contribution = $500 + $100 = $600
        item1!.ContributionMix.Should().BeApproximately(83.33m, 0.5m); // 500/600 * 100
        item2!.ContributionMix.Should().BeApproximately(16.67m, 0.5m); // 100/600 * 100
    }

    // ============================================================================
    // Report Summary Tests
    // ============================================================================

    [Fact]
    public async Task AnalyzeAsync_ReportShouldIncludeCorrectTotals()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        // Add multiple items
        for (int i = 0; i < 10; i++)
        {
            await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
                ProductId: Guid.NewGuid(),
                ProductName: $"Item {i}",
                Category: "Mains",
                SellingPrice: 15.00m + i,
                TheoreticalCost: 5.00m,
                UnitsSold: 10 * (i + 1),
                TotalRevenue: (15.00m + i) * 10 * (i + 1)));
        }

        // Act
        var report = await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        report.TotalMenuItems.Should().Be(10);
        report.TotalItemsSold.Should().Be(550); // Sum of 10, 20, 30, ..., 100
        report.TotalRevenue.Should().BeGreaterThan(0);
        report.TotalCost.Should().BeGreaterThan(0);
        report.TotalContribution.Should().Be(report.TotalRevenue - report.TotalCost);
        report.OverallMarginPercent.Should().BeGreaterThan(0);

        // Classification counts should total to menu items
        (report.StarCount + report.PlowhorseCount + report.PuzzleCount + report.DogCount)
            .Should().Be(report.TotalMenuItems);
    }

    [Fact]
    public async Task AnalyzeAsync_ReportShouldIncludeTopContributors()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var topContributorId = Guid.NewGuid();

        // Add a clear top contributor
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: topContributorId,
            ProductName: "Top Contributor",
            Category: "Mains",
            SellingPrice: 50.00m,
            TheoreticalCost: 15.00m, // $35 margin
            UnitsSold: 100, // $3500 total contribution
            TotalRevenue: 5000.00m));

        // Add some other items with lower contribution
        for (int i = 0; i < 5; i++)
        {
            await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
                ProductId: Guid.NewGuid(),
                ProductName: $"Regular Item {i}",
                Category: "Mains",
                SellingPrice: 15.00m,
                TheoreticalCost: 8.00m, // $7 margin
                UnitsSold: 20, // $140 total contribution
                TotalRevenue: 300.00m));
        }

        // Act
        var report = await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        report.TopContributors.Should().NotBeEmpty();
        report.TopContributors[0].ProductId.Should().Be(topContributorId);
        report.TopContributors[0].TotalContribution.Should().Be(3500.00m);
    }

    // ============================================================================
    // Bulk Recording Tests
    // ============================================================================

    [Fact]
    public async Task BulkRecordSalesAsync_ShouldRecordMultipleItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var commands = new List<RecordItemSalesCommand>();
        for (int i = 0; i < 20; i++)
        {
            commands.Add(new RecordItemSalesCommand(
                ProductId: Guid.NewGuid(),
                ProductName: $"Bulk Item {i}",
                Category: i % 2 == 0 ? "Mains" : "Drinks",
                SellingPrice: 10.00m + i,
                TheoreticalCost: 4.00m,
                UnitsSold: 10,
                TotalRevenue: (10.00m + i) * 10));
        }

        // Act
        await grain.BulkRecordSalesAsync(commands);
        var report = await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        report.TotalMenuItems.Should().Be(20);
        report.Categories.Should().HaveCount(2);
    }

    // ============================================================================
    // Category Filter Tests
    // ============================================================================

    [Fact]
    public async Task GetItemAnalysisAsync_WithCategoryFilter_ShouldReturnOnlyCategoryItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        // Add items in different categories
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Burger",
            Category: "Mains",
            SellingPrice: 15.00m,
            TheoreticalCost: 6.00m,
            UnitsSold: 50,
            TotalRevenue: 750.00m));

        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Beer",
            Category: "Drinks",
            SellingPrice: 8.00m,
            TheoreticalCost: 2.00m,
            UnitsSold: 100,
            TotalRevenue: 800.00m));

        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Steak",
            Category: "Mains",
            SellingPrice: 35.00m,
            TheoreticalCost: 12.00m,
            UnitsSold: 30,
            TotalRevenue: 1050.00m));

        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Act
        var mainsOnly = await grain.GetItemAnalysisAsync("Mains");
        var drinksOnly = await grain.GetItemAnalysisAsync("Drinks");
        var allItems = await grain.GetItemAnalysisAsync();

        // Assert
        mainsOnly.Should().HaveCount(2);
        mainsOnly.All(i => i.Category == "Mains").Should().BeTrue();

        drinksOnly.Should().HaveCount(1);
        drinksOnly.All(i => i.Category == "Drinks").Should().BeTrue();

        allItems.Should().HaveCount(3);
    }
}
