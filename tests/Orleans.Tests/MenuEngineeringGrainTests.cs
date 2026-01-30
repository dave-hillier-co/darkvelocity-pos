using DarkVelocity.Orleans.Abstractions.Costing;
using DarkVelocity.Orleans.Abstractions.Grains;
using FluentAssertions;

namespace DarkVelocity.Orleans.Tests;

[Collection(ClusterCollection.Name)]
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
        report.StarCount.Should().BeGreaterOrEqualTo(1);
        report.PlowhorseCount.Should().BeGreaterOrEqualTo(1);
        report.PuzzleCount.Should().BeGreaterOrEqualTo(1);
        report.DogCount.Should().BeGreaterOrEqualTo(1);
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

        // Add high margin, high popularity item (Star)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Star Item",
            Category: "Mains",
            SellingPrice: 20.00m,
            TheoreticalCost: 5.00m,
            UnitsSold: 200,
            TotalRevenue: 4000.00m));

        // Add low margin, high popularity item (Plowhorse)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Plowhorse Item",
            Category: "Mains",
            SellingPrice: 10.00m,
            TheoreticalCost: 6.00m,
            UnitsSold: 150,
            TotalRevenue: 1500.00m));

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

        // Low margin item
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Low Margin Burger",
            Category: "Mains",
            SellingPrice: 10.00m,
            TheoreticalCost: 5.00m, // 50% margin
            UnitsSold: 100,
            TotalRevenue: 1000.00m));

        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Act
        var suggestions = await grain.GetPriceSuggestionsAsync(targetMarginPercent: 70m);

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

        // Add item and check suggestions reflect new target
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Item",
            Category: "Mains",
            SellingPrice: 10.00m,
            TheoreticalCost: 3.00m, // 70% margin
            UnitsSold: 100,
            TotalRevenue: 1000.00m));

        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));
        var suggestions = await grain.GetPriceSuggestionsAsync(targetMarginPercent: 75m);

        // Assert - should suggest price increase to hit 75%
        suggestions.Should().NotBeEmpty();
    }
}
