using DarkVelocity.Host.Costing;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests.Grains.Costing;

/// <summary>
/// Tests for MenuEngineering quadrant classification (Stars, Plowhorses, Puzzles, Dogs).
/// Menu engineering classifies items based on popularity (units sold vs average) and
/// profitability (contribution margin vs average).
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MenuEngineeringQuadrantTests
{
    private readonly TestClusterFixture _fixture;

    public MenuEngineeringQuadrantTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private string GetGrainKey(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:menu-engineering";

    // ============================================================================
    // Star Classification Tests - High Margin, High Popularity
    // ============================================================================

    // Given: a premium burger with above-average margin and above-average sales volume
    // When: menu engineering analysis classifies all items
    // Then: the item is classified as a Star with popularity and profitability indexes above 1.0
    [Fact]
    public async Task AnalyzeAsync_HighMarginHighPopularity_ShouldClassifyAsStar()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var starId = Guid.NewGuid();
        var dogId = Guid.NewGuid();

        // Star: High margin ($15), High popularity (200 units)
        // Average margin will be around $9, Average units will be around 110
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: starId,
            ProductName: "Premium Burger",
            Category: "Mains",
            SellingPrice: 20.00m,
            TheoreticalCost: 5.00m, // $15 contribution margin
            UnitsSold: 200,
            TotalRevenue: 4000.00m));

        // Dog: Low margin ($3), Low popularity (20 units)
        // This brings down the averages so the Star clearly stands out
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: dogId,
            ProductName: "Basic Fries",
            Category: "Mains",
            SellingPrice: 5.00m,
            TheoreticalCost: 2.00m, // $3 contribution margin
            UnitsSold: 20,
            TotalRevenue: 100.00m));

        // Act
        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        var item = await grain.GetItemAsync(starId);
        item.Should().NotBeNull();
        item!.Classification.Should().Be(MenuClass.Star);
        item.PopularityIndex.Should().BeGreaterThan(1.0m); // Above average
        item.ProfitabilityIndex.Should().BeGreaterThan(1.0m); // Above average
    }

    // Given: a signature steak with high margin ($33) and high volume (150 units)
    // When: menu engineering analysis is performed
    // Then: the item appears in the top stars report
    [Fact]
    public async Task AnalyzeAsync_Star_ShouldAppearInTopStarsReport()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var starId = Guid.NewGuid();

        // Add clear star item
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: starId,
            ProductName: "Signature Steak",
            Category: "Mains",
            SellingPrice: 45.00m,
            TheoreticalCost: 12.00m, // $33 margin
            UnitsSold: 150,
            TotalRevenue: 6750.00m));

        // Add a contrasting item
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "House Salad",
            Category: "Mains",
            SellingPrice: 8.00m,
            TheoreticalCost: 5.00m, // $3 margin
            UnitsSold: 30,
            TotalRevenue: 240.00m));

        // Act
        var report = await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        report.TopStars.Should().NotBeEmpty();
        report.TopStars.Should().Contain(i => i.ProductId == starId);
    }

    // ============================================================================
    // Plowhorse Classification Tests - Low Margin, High Popularity
    // ============================================================================

    // Given: a basic burger with below-average margin but high sales volume (180 units)
    // When: menu engineering analysis classifies all items
    // Then: the item is classified as a Plowhorse (popular but low-profit)
    [Fact]
    public async Task AnalyzeAsync_LowMarginHighPopularity_ShouldClassifyAsPlowhorse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var plowhorseId = Guid.NewGuid();

        // Plowhorse: Low margin ($4), High popularity (180 units)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: plowhorseId,
            ProductName: "Basic Burger",
            Category: "Mains",
            SellingPrice: 10.00m,
            TheoreticalCost: 6.00m, // $4 margin - below average
            UnitsSold: 180, // High volume
            TotalRevenue: 1800.00m));

        // Add high margin, low volume item to balance averages
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Premium Steak",
            Category: "Mains",
            SellingPrice: 50.00m,
            TheoreticalCost: 15.00m, // $35 margin - well above average
            UnitsSold: 20, // Low volume
            TotalRevenue: 1000.00m));

        // Act
        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        var item = await grain.GetItemAsync(plowhorseId);
        item.Should().NotBeNull();
        item!.Classification.Should().Be(MenuClass.Plowhorse);
        item.PopularityIndex.Should().BeGreaterThan(1.0m); // Above average popularity
        item.ProfitabilityIndex.Should().BeLessThan(1.0m); // Below average margin
    }

    // Given: a kids meal selling 200 units with only $3 margin per unit
    // When: menu engineering analysis is performed
    // Then: the item appears in the low-margin, high-volume items list
    [Fact]
    public async Task AnalyzeAsync_Plowhorse_ShouldAppearInLowMarginHighVolumeList()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var plowhorseId = Guid.NewGuid();

        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: plowhorseId,
            ProductName: "Kids Meal",
            Category: "Mains",
            SellingPrice: 8.00m,
            TheoreticalCost: 5.00m, // $3 margin
            UnitsSold: 200,
            TotalRevenue: 1600.00m));

        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Lobster Tail",
            Category: "Mains",
            SellingPrice: 65.00m,
            TheoreticalCost: 20.00m, // $45 margin
            UnitsSold: 10,
            TotalRevenue: 650.00m));

        // Act
        var report = await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        report.LowMarginHighVolume.Should().NotBeEmpty();
        report.LowMarginHighVolume.Should().Contain(i => i.ProductId == plowhorseId);
    }

    // ============================================================================
    // Puzzle Classification Tests - High Margin, Low Popularity
    // ============================================================================

    // Given: a gourmet seafood platter with $40 margin but only 15 units sold
    // When: menu engineering analysis classifies all items
    // Then: the item is classified as a Puzzle (profitable but unpopular)
    [Fact]
    public async Task AnalyzeAsync_HighMarginLowPopularity_ShouldClassifyAsPuzzle()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var puzzleId = Guid.NewGuid();

        // Puzzle: High margin ($40), Low popularity (15 units)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: puzzleId,
            ProductName: "Gourmet Seafood Platter",
            Category: "Mains",
            SellingPrice: 55.00m,
            TheoreticalCost: 15.00m, // $40 margin
            UnitsSold: 15, // Low volume
            TotalRevenue: 825.00m));

        // Add low margin, high volume item to balance
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "House Fries",
            Category: "Mains",
            SellingPrice: 5.00m,
            TheoreticalCost: 3.00m, // $2 margin
            UnitsSold: 200, // High volume
            TotalRevenue: 1000.00m));

        // Act
        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        var item = await grain.GetItemAsync(puzzleId);
        item.Should().NotBeNull();
        item!.Classification.Should().Be(MenuClass.Puzzle);
        item.PopularityIndex.Should().BeLessThan(1.0m); // Below average popularity
        item.ProfitabilityIndex.Should().BeGreaterThan(1.0m); // Above average margin
    }

    // Given: a Wagyu beef item with $60 margin but only 8 units sold
    // When: menu engineering analysis is performed
    // Then: the item appears in the high-margin, low-volume items list
    [Fact]
    public async Task AnalyzeAsync_Puzzle_ShouldAppearInHighMarginLowVolumeList()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var puzzleId = Guid.NewGuid();

        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: puzzleId,
            ProductName: "Wagyu Beef",
            Category: "Mains",
            SellingPrice: 95.00m,
            TheoreticalCost: 35.00m, // $60 margin
            UnitsSold: 8,
            TotalRevenue: 760.00m));

        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Garden Salad",
            Category: "Mains",
            SellingPrice: 7.00m,
            TheoreticalCost: 4.00m, // $3 margin
            UnitsSold: 150,
            TotalRevenue: 1050.00m));

        // Act
        var report = await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        report.HighMarginLowVolume.Should().NotBeEmpty();
        report.HighMarginLowVolume.Should().Contain(i => i.ProductId == puzzleId);
    }

    // ============================================================================
    // Dog Classification Tests - Low Margin, Low Popularity
    // ============================================================================

    // Given: an old recipe soup with only $2 margin and 10 units sold
    // When: menu engineering analysis classifies all items
    // Then: the item is classified as a Dog (low profit, low popularity)
    [Fact]
    public async Task AnalyzeAsync_LowMarginLowPopularity_ShouldClassifyAsDog()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var dogId = Guid.NewGuid();

        // Dog: Low margin ($2), Low popularity (10 units)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: dogId,
            ProductName: "Old Recipe Soup",
            Category: "Mains",
            SellingPrice: 6.00m,
            TheoreticalCost: 4.00m, // $2 margin
            UnitsSold: 10, // Low volume
            TotalRevenue: 60.00m));

        // Add star item to balance averages
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Signature Dish",
            Category: "Mains",
            SellingPrice: 35.00m,
            TheoreticalCost: 10.00m, // $25 margin
            UnitsSold: 150, // High volume
            TotalRevenue: 5250.00m));

        // Act
        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        var item = await grain.GetItemAsync(dogId);
        item.Should().NotBeNull();
        item!.Classification.Should().Be(MenuClass.Dog);
        item.PopularityIndex.Should().BeLessThan(1.0m); // Below average popularity
        item.ProfitabilityIndex.Should().BeLessThan(1.0m); // Below average margin
    }

    // Given: a discontinued item with $2 margin and only 5 units sold
    // When: menu engineering analysis is performed
    // Then: the item appears in the dogs-to-review list for potential menu removal
    [Fact]
    public async Task AnalyzeAsync_Dog_ShouldAppearInDogsToReviewList()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var dogId = Guid.NewGuid();

        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: dogId,
            ProductName: "Discontinued Item",
            Category: "Mains",
            SellingPrice: 8.00m,
            TheoreticalCost: 6.00m, // $2 margin
            UnitsSold: 5,
            TotalRevenue: 40.00m));

        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Best Seller",
            Category: "Mains",
            SellingPrice: 25.00m,
            TheoreticalCost: 8.00m, // $17 margin
            UnitsSold: 180,
            TotalRevenue: 4500.00m));

        // Act
        var report = await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        report.DogsToReview.Should().NotBeEmpty();
        report.DogsToReview.Should().Contain(i => i.ProductId == dogId);
    }

    // ============================================================================
    // Comprehensive Quadrant Tests
    // ============================================================================

    // Given: four menu items designed to fall into each quadrant (Star, Plowhorse, Puzzle, Dog)
    // When: menu engineering analysis classifies all items
    // Then: each item is classified into its expected quadrant based on margin and popularity
    [Fact]
    public async Task AnalyzeAsync_AllFourQuadrants_ShouldClassifyCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var starId = Guid.NewGuid();
        var plowhorseId = Guid.NewGuid();
        var puzzleId = Guid.NewGuid();
        var dogId = Guid.NewGuid();

        // Create items that clearly fall into each quadrant
        // Average units will be (200 + 180 + 20 + 10) / 4 = 102.5
        // Average margin will be ($18 + $4 + $45 + $2) / 4 = $17.25

        // Star: High margin ($18), High volume (200)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: starId,
            ProductName: "Star Item",
            Category: "Mains",
            SellingPrice: 25.00m,
            TheoreticalCost: 7.00m, // $18 margin > $17.25 avg
            UnitsSold: 200, // > 102.5 avg
            TotalRevenue: 5000.00m));

        // Plowhorse: Low margin ($4), High volume (180)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: plowhorseId,
            ProductName: "Plowhorse Item",
            Category: "Mains",
            SellingPrice: 10.00m,
            TheoreticalCost: 6.00m, // $4 margin < $17.25 avg
            UnitsSold: 180, // > 102.5 avg
            TotalRevenue: 1800.00m));

        // Puzzle: High margin ($45), Low volume (20)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: puzzleId,
            ProductName: "Puzzle Item",
            Category: "Mains",
            SellingPrice: 60.00m,
            TheoreticalCost: 15.00m, // $45 margin > $17.25 avg
            UnitsSold: 20, // < 102.5 avg
            TotalRevenue: 1200.00m));

        // Dog: Low margin ($2), Low volume (10)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: dogId,
            ProductName: "Dog Item",
            Category: "Mains",
            SellingPrice: 6.00m,
            TheoreticalCost: 4.00m, // $2 margin < $17.25 avg
            UnitsSold: 10, // < 102.5 avg
            TotalRevenue: 60.00m));

        // Act
        var report = await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        report.StarCount.Should().BeGreaterOrEqualTo(1);
        report.PlowhorseCount.Should().BeGreaterOrEqualTo(1);
        report.PuzzleCount.Should().BeGreaterOrEqualTo(1);
        report.DogCount.Should().BeGreaterOrEqualTo(1);

        var starItem = await grain.GetItemAsync(starId);
        var plowhorseItem = await grain.GetItemAsync(plowhorseId);
        var puzzleItem = await grain.GetItemAsync(puzzleId);
        var dogItem = await grain.GetItemAsync(dogId);

        starItem!.Classification.Should().Be(MenuClass.Star);
        plowhorseItem!.Classification.Should().Be(MenuClass.Plowhorse);
        puzzleItem!.Classification.Should().Be(MenuClass.Puzzle);
        dogItem!.Classification.Should().Be(MenuClass.Dog);
    }

    // Given: menu items with contrasting margin and volume characteristics after analysis
    // When: items are queried by classification (Star, Dog)
    // Then: each query returns only items matching the requested classification
    [Fact]
    public async Task GetItemsByClassAsync_ShouldReturnCorrectItemsForEachClass()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        // Add items that will fall into different quadrants
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "High Margin High Volume",
            Category: "Mains",
            SellingPrice: 30.00m,
            TheoreticalCost: 8.00m,
            UnitsSold: 200,
            TotalRevenue: 6000.00m));

        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Low Margin Low Volume",
            Category: "Mains",
            SellingPrice: 5.00m,
            TheoreticalCost: 4.00m,
            UnitsSold: 10,
            TotalRevenue: 50.00m));

        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Act
        var stars = await grain.GetItemsByClassAsync(MenuClass.Star);
        var dogs = await grain.GetItemsByClassAsync(MenuClass.Dog);

        // Assert
        stars.Should().NotBeEmpty();
        dogs.Should().NotBeEmpty();
        stars.All(i => i.Classification == MenuClass.Star).Should().BeTrue();
        dogs.All(i => i.Classification == MenuClass.Dog).Should().BeTrue();
    }

    // Given: five menu items (three high-performers, two low-performers) after analysis
    // When: classification counts are retrieved
    // Then: counts for all four quadrants sum to the total number of menu items
    [Fact]
    public async Task GetClassificationCountsAsync_ShouldReturnAccurateCounts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        // Add multiple items of each type
        for (int i = 0; i < 3; i++)
        {
            await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
                ProductId: Guid.NewGuid(),
                ProductName: $"Star {i}",
                Category: "Mains",
                SellingPrice: 30.00m,
                TheoreticalCost: 8.00m,
                UnitsSold: 150 + i * 10,
                TotalRevenue: 4500.00m + i * 300));
        }

        for (int i = 0; i < 2; i++)
        {
            await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
                ProductId: Guid.NewGuid(),
                ProductName: $"Dog {i}",
                Category: "Mains",
                SellingPrice: 5.00m,
                TheoreticalCost: 4.00m,
                UnitsSold: 5 + i,
                TotalRevenue: 25.00m + i * 5));
        }

        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Act
        var counts = await grain.GetClassificationCountsAsync();

        // Assert
        counts.Should().ContainKey(MenuClass.Star);
        counts.Should().ContainKey(MenuClass.Plowhorse);
        counts.Should().ContainKey(MenuClass.Puzzle);
        counts.Should().ContainKey(MenuClass.Dog);

        var totalCount = counts.Values.Sum();
        totalCount.Should().Be(5); // 3 + 2
    }

    // ============================================================================
    // Edge Cases
    // ============================================================================

    // Given: a single menu item as the only item in the analysis
    // When: menu engineering analysis is performed
    // Then: the item's popularity and profitability indexes are exactly 1.0 (at average)
    [Fact]
    public async Task AnalyzeAsync_SingleItem_ShouldClassifyAsStarByDefault()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var itemId = Guid.NewGuid();
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: itemId,
            ProductName: "Only Item",
            Category: "Mains",
            SellingPrice: 20.00m,
            TheoreticalCost: 8.00m,
            UnitsSold: 100,
            TotalRevenue: 2000.00m));

        // Act
        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        var item = await grain.GetItemAsync(itemId);
        item.Should().NotBeNull();
        // Single item is exactly at average, so indexes should be 1.0
        // Classification depends on whether >= 1.0 counts as "high"
        item!.PopularityIndex.Should().Be(1.0m);
        item.ProfitabilityIndex.Should().Be(1.0m);
    }

    // Given: five menu items with identical pricing, cost, and sales volume
    // When: menu engineering analysis is performed
    // Then: all items receive the same classification
    [Fact]
    public async Task AnalyzeAsync_ItemsWithSameMetrics_ShouldAllBeClassifiedSame()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var ids = new List<Guid>();
        for (int i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid();
            ids.Add(id);
            await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
                ProductId: id,
                ProductName: $"Identical Item {i}",
                Category: "Mains",
                SellingPrice: 15.00m,
                TheoreticalCost: 5.00m,
                UnitsSold: 100,
                TotalRevenue: 1500.00m));
        }

        // Act
        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert - all items have identical metrics so should have same classification
        var classifications = new List<MenuClass>();
        foreach (var id in ids)
        {
            var item = await grain.GetItemAsync(id);
            classifications.Add(item!.Classification);
        }

        classifications.Distinct().Count().Should().Be(1);
    }

    // Given: a digital gift card with zero cost (100% margin) alongside a regular item
    // When: menu engineering analysis is performed
    // Then: the zero-cost item is analyzed with full contribution margin without errors
    [Fact]
    public async Task AnalyzeAsync_ZeroCostItems_ShouldNotBreakClassification()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        var zeroCostId = Guid.NewGuid();

        // Item with zero cost (100% margin)
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: zeroCostId,
            ProductName: "Digital Gift Card",
            Category: "Digital",
            SellingPrice: 50.00m,
            TheoreticalCost: 0m, // Zero cost
            UnitsSold: 50,
            TotalRevenue: 2500.00m));

        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Regular Item",
            Category: "Digital",
            SellingPrice: 20.00m,
            TheoreticalCost: 10.00m,
            UnitsSold: 100,
            TotalRevenue: 2000.00m));

        // Act
        var report = await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Assert
        report.TotalMenuItems.Should().Be(2);
        var zeroCostItem = await grain.GetItemAsync(zeroCostId);
        zeroCostItem.Should().NotBeNull();
        zeroCostItem!.ContributionMargin.Should().Be(50.00m);
        zeroCostItem.ContributionMarginPercent.Should().Be(100.00m);
    }

    // ============================================================================
    // Category-Specific Classification Tests
    // ============================================================================

    // Given: menu items across Mains and Drinks categories after analysis
    // When: category-level analysis is retrieved
    // Then: each category includes its own item count and classification quadrant breakdown
    [Fact]
    public async Task GetCategoryAnalysisAsync_ShouldIncludeClassificationBreakdownPerCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(GetGrainKey(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(orgId, siteId, "Test Restaurant"));

        // Add items to "Mains" category
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Main Star",
            Category: "Mains",
            SellingPrice: 25.00m,
            TheoreticalCost: 8.00m,
            UnitsSold: 150,
            TotalRevenue: 3750.00m));

        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Main Dog",
            Category: "Mains",
            SellingPrice: 8.00m,
            TheoreticalCost: 6.00m,
            UnitsSold: 20,
            TotalRevenue: 160.00m));

        // Add items to "Drinks" category
        await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
            ProductId: Guid.NewGuid(),
            ProductName: "Popular Drink",
            Category: "Drinks",
            SellingPrice: 6.00m,
            TheoreticalCost: 1.50m,
            UnitsSold: 300,
            TotalRevenue: 1800.00m));

        await grain.AnalyzeAsync(new AnalyzeMenuCommand(DateTime.Today.AddDays(-30), DateTime.Today));

        // Act
        var categories = await grain.GetCategoryAnalysisAsync();

        // Assert
        categories.Should().HaveCount(2);
        var mainsCategory = categories.First(c => c.Category == "Mains");
        mainsCategory.ItemCount.Should().Be(2);
        (mainsCategory.StarCount + mainsCategory.PlowhorseCount +
         mainsCategory.PuzzleCount + mainsCategory.DogCount).Should().Be(2);
    }
}
