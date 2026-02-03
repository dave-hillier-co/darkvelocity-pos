using DarkVelocity.Host.Costing;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MenuEngineeringGrainTests
{
    private readonly TestClusterFixture _fixture;

    public MenuEngineeringGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private string GetGrainKey(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:menu-engineering";

    [Fact]
    public async Task InitializeAsync_ShouldInitializeGrain()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        // Act
        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Main Street", 70m));

        // Assert - grain should be functional
        var classifications = await grain.GetClassificationCountsAsync();
        classifications.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordItemSalesAsync_ShouldRecordSales()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Main Street"));

        // Act
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: productId,
            ProductName: "Burger",
            Category: "Mains",
            SellingPrice: 15.00m,
            TheoreticalCost: 4.50m,
            UnitsSold: 100,
            TotalRevenue: 1500.00m));

        // Assert
        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));
        var item = await grain.GetItemAsync(productId);
        item.Should().NotBeNull();
        item!.UnitsSold.Should().Be(100);
        item.TotalRevenue.Should().Be(1500.00m);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldClassifyItemsCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Main Street"));

        // Star: High margin, high popularity
        var starId = Guid.NewGuid();
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: starId,
            ProductName: "Premium Burger",
            Category: "Mains",
            SellingPrice: 20.00m,
            TheoreticalCost: 5.00m, // 75% margin
            UnitsSold: 200, // High volume
            TotalRevenue: 4000.00m));

        // Plowhorse: Low margin, high popularity
        var plowhorseId = Guid.NewGuid();
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: plowhorseId,
            ProductName: "Basic Burger",
            Category: "Mains",
            SellingPrice: 10.00m,
            TheoreticalCost: 5.00m, // 50% margin
            UnitsSold: 180, // High volume
            TotalRevenue: 1800.00m));

        // Puzzle: High margin, low popularity
        var puzzleId = Guid.NewGuid();
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: puzzleId,
            ProductName: "Gourmet Salad",
            Category: "Mains",
            SellingPrice: 18.00m,
            TheoreticalCost: 4.00m, // 78% margin
            UnitsSold: 30, // Low volume
            TotalRevenue: 540.00m));

        // Dog: Low margin, low popularity
        var dogId = Guid.NewGuid();
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: dogId,
            ProductName: "Old Recipe",
            Category: "Mains",
            SellingPrice: 12.00m,
            TheoreticalCost: 7.00m, // 42% margin
            UnitsSold: 20, // Low volume
            TotalRevenue: 240.00m));

        // Act
        var report = await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        report.TotalMenuItems.Should().Be(4);
        report.StarCount.Should().BeGreaterThanOrEqualTo(1);
        report.PlowhorseCount.Should().BeGreaterThanOrEqualTo(1);
        report.PuzzleCount.Should().BeGreaterThanOrEqualTo(1);
        report.DogCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldCalculateContributionMargin()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Main Street"));

        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: productId,
            ProductName: "Burger",
            Category: "Mains",
            SellingPrice: 15.00m,
            TheoreticalCost: 4.50m, // 70% margin
            UnitsSold: 100,
            TotalRevenue: 1500.00m));

        // Act
        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));
        var item = await grain.GetItemAsync(productId);

        // Assert
        item.Should().NotBeNull();
        item!.ContributionMargin.Should().Be(10.50m);
        item.ContributionMarginPercent.Should().Be(70.00m);
        item.TotalContribution.Should().Be(1050.00m);
    }

    [Fact]
    public async Task GetItemsByClassAsync_ShouldReturnCorrectItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Main Street"));

        // Need 3+ items so classification works (relative to average)
        // Star: high popularity (200 > avg 133), high margin ($15 > avg $7.33)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Star Item",
            Category: "Mains",
            SellingPrice: 20.00m,
            TheoreticalCost: 5.00m, // $15 margin
            UnitsSold: 200,
            TotalRevenue: 4000.00m));

        // Plowhorse: high popularity (180 > avg 133), low margin ($4 < avg $7.33)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Plowhorse Item",
            Category: "Mains",
            SellingPrice: 10.00m,
            TheoreticalCost: 6.00m, // $4 margin
            UnitsSold: 180,
            TotalRevenue: 1800.00m));

        // Dog: low popularity (20 < avg 133), low margin ($3 < avg $7.33)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Dog Item",
            Category: "Mains",
            SellingPrice: 8.00m,
            TheoreticalCost: 5.00m, // $3 margin
            UnitsSold: 20,
            TotalRevenue: 160.00m));

        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Act
        var stars = await grain.GetItemsByClassAsync(MenuClass.Star);
        var plowhorses = await grain.GetItemsByClassAsync(MenuClass.Plowhorse);

        // Assert
        stars.Should().NotBeEmpty();
        plowhorses.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetCategoryAnalysisAsync_ShouldReturnCategoryBreakdown()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Main Street"));

        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Burger",
            Category: "Mains",
            SellingPrice: 15.00m,
            TheoreticalCost: 4.50m,
            UnitsSold: 100,
            TotalRevenue: 1500.00m));

        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Steak",
            Category: "Mains",
            SellingPrice: 30.00m,
            TheoreticalCost: 12.00m,
            UnitsSold: 50,
            TotalRevenue: 1500.00m));

        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Fries",
            Category: "Sides",
            SellingPrice: 5.00m,
            TheoreticalCost: 1.00m,
            UnitsSold: 200,
            TotalRevenue: 1000.00m));

        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Act
        var categories = await grain.GetCategoryAnalysisAsync();

        // Assert
        categories.Should().HaveCount(2);
        categories.Should().Contain(c => c.Category == "Mains");
        categories.Should().Contain(c => c.Category == "Sides");

        var mainsCategory = categories.First(c => c.Category == "Mains");
        mainsCategory.ItemCount.Should().Be(2);
        mainsCategory.TotalUnitsSold.Should().Be(150);
    }

    [Fact]
    public async Task GetPriceSuggestionsAsync_ShouldSuggestPriceIncreases()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Main Street"));

        // The grain only suggests if margin is 5+ points below target AND price change is <= 15%
        // For item with 62% margin and target 68%:
        //   - 62 < 68 - 5 = 62 < 63 is TRUE (triggers suggestion)
        //   - Price = $10, Cost = $3.80
        //   - Target price = $3.80 / 0.32 = $11.875 = 18.75% increase (exceeds 15%)
        //
        // For item with 62% margin and target 66%:
        //   - 62 < 66 - 5 = 62 < 61 is FALSE (doesn't trigger)
        //
        // The constraints are very tight. Let's use margin = 57% with target = 65%:
        //   - 57 < 65 - 5 = 57 < 60 is TRUE
        //   - Price = $10, Cost = $4.30
        //   - Target price = $4.30 / 0.35 = $12.29 = 22.9% (too high)
        //
        // With higher price and lower target gap:
        // Price = $20, Cost = $7 (65% margin), Target = 72%
        //   - 65 < 72 - 5 = 65 < 67 is TRUE
        //   - Target price = $7 / 0.28 = $25 = 25% increase (too high)
        //
        // The 15% max price change is very restrictive. Let's test with higher limit:
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Low Margin Burger",
            Category: "Mains",
            SellingPrice: 10.00m,
            TheoreticalCost: 4.00m, // 60% margin
            UnitsSold: 100,
            TotalRevenue: 1000.00m));

        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Act - use higher maxPriceChangePercent to allow larger price changes
        var suggestions = await grain.GetPriceSuggestionsAsync(targetMarginPercent: 70m, maxPriceChangePercent: 50m);

        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions.First().CurrentMargin.Should().BeLessThan(70m);
        suggestions.First().SuggestedPrice.Should().BeGreaterThan(10.00m);
    }

    [Fact]
    public async Task BulkRecordSalesAsync_ShouldRecordAllItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Main Street"));

        var commands = Enumerable.Range(1, 5).Select(i => new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: $"Item {i}",
            Category: "Mains",
            SellingPrice: 15.00m,
            TheoreticalCost: 5.00m,
            UnitsSold: 50 * i,
            TotalRevenue: 750.00m * i)).ToList();

        // Act
        await grain.BulkRecordSalesAsync(commands);
        var report = await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        report.TotalMenuItems.Should().Be(5);
    }

    [Fact]
    public async Task SetTargetMarginAsync_ShouldUpdateTarget()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Main Street", 70m));

        // Act
        await grain.SetTargetMarginAsync(75m);

        // Add item with 65% margin - below new 75% target (triggers at margin < 70%)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Item",
            Category: "Mains",
            SellingPrice: 10.00m,
            TheoreticalCost: 3.50m, // 65% margin
            UnitsSold: 100,
            TotalRevenue: 1000.00m));

        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));
        // Use higher maxPriceChangePercent to allow suggestions
        var suggestions = await grain.GetPriceSuggestionsAsync(targetMarginPercent: 75m, maxPriceChangePercent: 50m);

        // Assert - should suggest price increase to hit 75%
        suggestions.Should().NotBeEmpty();
    }
}
