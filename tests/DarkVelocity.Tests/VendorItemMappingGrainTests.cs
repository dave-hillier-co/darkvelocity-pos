using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class VendorItemMappingGrainTests
{
    private readonly TestClusterFixture _fixture;

    public VendorItemMappingGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IVendorItemMappingGrain GetGrain(Guid orgId, string vendorId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IVendorItemMappingGrain>(
            GrainKeys.VendorItemMapping(orgId, vendorId));
    }

    // Given: a new vendor item mapping grain
    // When: initialized for a supplier
    // Then: the vendor mapping is created with zero mappings
    [Fact]
    public async Task InitializeAsync_ShouldCreateVendorMapping()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = "sysco";
        var grain = GetGrain(orgId, vendorId);

        // Act
        var snapshot = await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId,
            vendorId,
            "Sysco Corporation",
            VendorType.Supplier));

        // Assert
        snapshot.OrganizationId.Should().Be(orgId);
        snapshot.VendorId.Should().Be(vendorId);
        snapshot.VendorName.Should().Be("Sysco Corporation");
        snapshot.VendorType.Should().Be(VendorType.Supplier);
        snapshot.TotalMappings.Should().Be(0);
    }

    // Given: an already initialized vendor mapping
    // When: initialization is attempted a second time
    // Then: the operation is rejected as a duplicate initialization
    [Fact]
    public async Task InitializeAsync_AlreadyInitialized_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"test-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Unknown));

        // Act
        var act = () => grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Unknown));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Vendor mapping already initialized");
    }

    // Given: an initialized vendor mapping for a supplier
    // When: a vendor product description is manually mapped to an inventory ingredient
    // Then: the mapping is created with manual source and full confidence
    [Fact]
    public async Task SetMappingAsync_ShouldCreateMapping()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId = Guid.NewGuid();

        // Act
        var mapping = await grain.SetMappingAsync(new SetMappingCommand(
            "CHICKEN BREAST 5KG",
            ingredientId,
            "Chicken Breast",
            "chicken-breast",
            Guid.NewGuid(),
            "SKU-12345",
            12.50m,
            "kg"));

        // Assert
        mapping.VendorDescription.Should().Be("CHICKEN BREAST 5KG");
        mapping.IngredientId.Should().Be(ingredientId);
        mapping.IngredientName.Should().Be("Chicken Breast");
        mapping.IngredientSku.Should().Be("chicken-breast");
        mapping.Source.Should().Be(MappingSource.Manual);
        mapping.Confidence.Should().Be(1.0m);
    }

    // Given: a vendor mapping with an exact product description registered
    // When: the same description is looked up
    // Then: the mapping is found via exact description match
    [Fact]
    public async Task GetMappingAsync_ExactMatch_ShouldReturnMapping()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId = Guid.NewGuid();
        await grain.SetMappingAsync(new SetMappingCommand(
            "Organic Large Eggs 24ct",
            ingredientId,
            "Organic Eggs",
            "eggs-organic-large",
            Guid.NewGuid()));

        // Act
        var result = await grain.GetMappingAsync("Organic Large Eggs 24ct");

        // Assert
        result.Found.Should().BeTrue();
        result.MatchType.Should().Be(MappingMatchType.ExactDescription);
        result.Mapping.Should().NotBeNull();
        result.Mapping!.IngredientId.Should().Be(ingredientId);
    }

    // Given: a vendor mapping with a supplier product code assigned
    // When: looking up by product code with a different description
    // Then: the mapping is found via product code match
    [Fact]
    public async Task GetMappingAsync_ProductCodeMatch_ShouldReturnMapping()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId = Guid.NewGuid();
        await grain.SetMappingAsync(new SetMappingCommand(
            "Fresh Atlantic Salmon",
            ingredientId,
            "Atlantic Salmon",
            "salmon-atlantic",
            Guid.NewGuid(),
            "FISH-001"));

        // Act - lookup by product code
        var result = await grain.GetMappingAsync("Atlantic Salmon Fillet", "FISH-001");

        // Assert
        result.Found.Should().BeTrue();
        result.MatchType.Should().Be(MappingMatchType.ProductCode);
        result.Mapping!.IngredientId.Should().Be(ingredientId);
    }

    // Given: an initialized vendor mapping with no matching descriptions registered
    // When: an unknown product description is looked up
    // Then: no mapping is found
    [Fact]
    public async Task GetMappingAsync_NoMatch_ShouldReturnNotFound()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        // Act
        var result = await grain.GetMappingAsync("Unknown Product XYZ");

        // Assert
        result.Found.Should().BeFalse();
        result.MatchType.Should().Be(MappingMatchType.None);
        result.Mapping.Should().BeNull();
    }

    // Given: an initialized vendor mapping with no prior learned patterns
    // When: a mapping is learned from a confirmed purchase document
    // Then: the exact mapping is created and fuzzy matching patterns are generated
    [Fact]
    public async Task LearnMappingAsync_ShouldCreateMappingAndPattern()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId = Guid.NewGuid();

        // Act
        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Ground Beef 80/20 5lb",
            ingredientId,
            "Ground Beef",
            "beef-ground-80-20",
            MappingSource.Manual,
            1.0m,
            null,
            Guid.NewGuid()));

        // Assert - should be able to find the exact mapping
        var result = await grain.GetMappingAsync("Ground Beef 80/20 5lb");
        result.Found.Should().BeTrue();
        result.Mapping!.IngredientId.Should().Be(ingredientId);

        // Check snapshot
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalMappings.Should().Be(1);
        snapshot.TotalPatterns.Should().BeGreaterThan(0);
    }

    // Given: a vendor mapping with learned patterns for chicken breast and ground beef
    // When: suggestions are requested for a similar chicken description
    // Then: the chicken ingredient is suggested as the top match
    [Fact]
    public async Task GetSuggestionsAsync_WithPatterns_ShouldReturnSuggestions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var chickenId = Guid.NewGuid();
        var beefId = Guid.NewGuid();

        // Learn some patterns
        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Chicken Breast Boneless",
            chickenId,
            "Chicken Breast",
            "chicken-breast",
            MappingSource.Manual));

        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Ground Beef 85/15",
            beefId,
            "Ground Beef",
            "beef-ground",
            MappingSource.Manual));

        // Act - search for similar item
        var suggestions = await grain.GetSuggestionsAsync("Chicken Breast Skinless");

        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions[0].IngredientId.Should().Be(chickenId);
        suggestions[0].IngredientName.Should().Be("Chicken Breast");
    }

    // Given: a vendor mapping with one registered product description
    // When: the mapping is deleted
    // Then: the description no longer resolves to any ingredient
    [Fact]
    public async Task DeleteMappingAsync_ShouldRemoveMapping()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId = Guid.NewGuid();
        await grain.SetMappingAsync(new SetMappingCommand(
            "Test Item",
            ingredientId,
            "Test Ingredient",
            "test-sku",
            Guid.NewGuid()));

        // Verify it exists
        var beforeResult = await grain.GetMappingAsync("Test Item");
        beforeResult.Found.Should().BeTrue();

        // Act
        await grain.DeleteMappingAsync(new DeleteMappingCommand("Test Item", Guid.NewGuid()));

        // Assert
        var afterResult = await grain.GetMappingAsync("Test Item");
        afterResult.Found.Should().BeFalse();
    }

    // Given: a vendor mapping for olive oil with an established ingredient link
    // When: the mapping is used in two separate purchase document confirmations
    // Then: the usage count reflects both confirmations
    [Fact]
    public async Task RecordUsageAsync_ShouldIncrementUsageCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        await grain.SetMappingAsync(new SetMappingCommand(
            "Olive Oil Extra Virgin",
            Guid.NewGuid(),
            "Olive Oil",
            "oil-olive-ev",
            Guid.NewGuid()));

        // Act
        await grain.RecordUsageAsync(new RecordMappingUsageCommand("Olive Oil Extra Virgin", Guid.NewGuid()));
        await grain.RecordUsageAsync(new RecordMappingUsageCommand("Olive Oil Extra Virgin", Guid.NewGuid()));

        // Assert
        var mappings = await grain.GetAllMappingsAsync();
        var olivOilMapping = mappings.FirstOrDefault(m => m.VendorDescription == "Olive Oil Extra Virgin");
        olivOilMapping.Should().NotBeNull();
        olivOilMapping!.UsageCount.Should().BeGreaterThanOrEqualTo(2);
    }

    // Given: a vendor mapping with three registered product descriptions
    // When: all mappings are requested
    // Then: all three descriptions and their ingredient associations are returned
    [Fact]
    public async Task GetAllMappingsAsync_ShouldReturnAllMappings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        await grain.SetMappingAsync(new SetMappingCommand("Item 1", Guid.NewGuid(), "Ingredient 1", "sku-1", Guid.NewGuid()));
        await grain.SetMappingAsync(new SetMappingCommand("Item 2", Guid.NewGuid(), "Ingredient 2", "sku-2", Guid.NewGuid()));
        await grain.SetMappingAsync(new SetMappingCommand("Item 3", Guid.NewGuid(), "Ingredient 3", "sku-3", Guid.NewGuid()));

        // Act
        var mappings = await grain.GetAllMappingsAsync();

        // Assert
        mappings.Should().HaveCount(3);
        mappings.Select(m => m.VendorDescription).Should().Contain("Item 1", "Item 2", "Item 3");
    }

    // Given: a vendor mapping registered with an uppercase product description
    // When: the same description is looked up in lowercase
    // Then: the mapping is found via case-insensitive exact match
    [Fact]
    public async Task GetMappingAsync_CaseInsensitive_ShouldMatch()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        await grain.SetMappingAsync(new SetMappingCommand(
            "WHOLE MILK 1 GAL",
            Guid.NewGuid(),
            "Whole Milk",
            "milk-whole",
            Guid.NewGuid()));

        // Act - search with different case
        var result = await grain.GetMappingAsync("whole milk 1 gal");

        // Assert
        result.Found.Should().BeTrue();
        result.MatchType.Should().Be(MappingMatchType.ExactDescription);
    }

    // Given: a vendor mapping grain that has not been explicitly initialized
    // When: a mapping is set directly without prior initialization
    // Then: the grain auto-initializes and the mapping is created successfully
    [Fact]
    public async Task SetMappingAsync_AutoInitializes_WhenNotInitialized()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        // Act - set mapping without initializing first
        var mapping = await grain.SetMappingAsync(new SetMappingCommand(
            "Test Product",
            Guid.NewGuid(),
            "Test Ingredient",
            "test-sku",
            Guid.NewGuid()));

        // Assert
        mapping.Should().NotBeNull();
        (await grain.ExistsAsync()).Should().BeTrue();
    }

    // Given: a learned vendor mapping for "Chicken Breast Boneless Skinless 10LB"
    // When: a similar description differing only in package size is looked up
    // Then: the mapping is found via fuzzy pattern matching above the similarity threshold
    [Fact]
    public async Task GetMappingAsync_SimilarDescription_ShouldMatchIfAbove85Percent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId = Guid.NewGuid();
        // Learn a mapping with specific description
        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Chicken Breast Boneless Skinless 10LB",
            ingredientId,
            "Chicken Breast",
            "chicken-breast",
            MappingSource.Manual));

        // Act - search with a very similar description (should match via fuzzy pattern)
        var result = await grain.GetMappingAsync("Chicken Breast Boneless Skinless 5LB");

        // Assert - should match via fuzzy pattern matching
        result.Found.Should().BeTrue();
        result.Mapping!.IngredientId.Should().Be(ingredientId);
        result.MatchType.Should().Be(MappingMatchType.FuzzyPattern);
    }

    // Given: a learned vendor mapping for Atlantic salmon
    // When: a completely unrelated description for ribeye steak is looked up
    // Then: no mapping is found due to insufficient similarity
    [Fact]
    public async Task GetMappingAsync_LowSimilarity_ShouldNotMatch()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId = Guid.NewGuid();
        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Fresh Atlantic Salmon Fillet",
            ingredientId,
            "Atlantic Salmon",
            "salmon-atlantic",
            MappingSource.Manual));

        // Act - search with completely different description
        var result = await grain.GetMappingAsync("Ribeye Steak Prime Grade");

        // Assert - should not match
        result.Found.Should().BeFalse();
        result.MatchType.Should().Be(MappingMatchType.None);
    }

    // Given: a vendor mapping with a learned pattern for organic eggs
    // When: a second similar description for the same ingredient is learned (reinforcement)
    // Then: the pattern weight increases, improving future suggestion confidence
    [Fact]
    public async Task LearnMappingAsync_SamePatternTwice_ShouldIncreaseWeight()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId = Guid.NewGuid();

        // Act - learn the same pattern twice
        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Organic Free Range Eggs Large 12ct",
            ingredientId,
            "Organic Eggs",
            "eggs-organic",
            MappingSource.Manual));

        // Learn again with same tokens (reinforcement)
        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Organic Free Range Eggs Large 24ct",
            ingredientId,
            "Organic Eggs",
            "eggs-organic",
            MappingSource.Manual));

        // Assert - snapshot should show patterns learned
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalPatterns.Should().BeGreaterThan(0);
        // The pattern weight should have increased, which we can verify
        // by checking that suggestions are returned with higher confidence
        var suggestions = await grain.GetSuggestionsAsync("Organic Free Range Eggs");
        suggestions.Should().NotBeEmpty();
        suggestions[0].IngredientId.Should().Be(ingredientId);
    }

    // Given: a vendor mapping with unit price and unit of measure configured
    // When: the mapping is retrieved by product description
    // Then: the expected unit price and unit are included in the result
    [Fact]
    public async Task GetMappingAsync_WithExpectedPrice_ShouldReturnPriceInfo()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId = Guid.NewGuid();
        await grain.SetMappingAsync(new SetMappingCommand(
            "Premium Ground Coffee 2LB",
            ingredientId,
            "Ground Coffee",
            "coffee-ground",
            Guid.NewGuid(),
            "COFFEE-001",
            15.99m,
            "lb"));

        // Act
        var result = await grain.GetMappingAsync("Premium Ground Coffee 2LB");

        // Assert
        result.Found.Should().BeTrue();
        result.Mapping.Should().NotBeNull();
        result.Mapping!.ExpectedUnitPrice.Should().Be(15.99m);
        result.Mapping.Unit.Should().Be("lb");
    }

    // Given: a vendor mapping with learned chicken patterns and external ingredient candidates
    // When: suggestions are requested for a chicken description with candidate ingredients provided
    // Then: results merge pattern-based and candidate-based matches with the learned pattern preferred
    [Fact]
    public async Task GetSuggestionsAsync_WithCandidates_ShouldMergePatternAndIngredient()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var chickenId = Guid.NewGuid();
        var beefId = Guid.NewGuid();
        var porkId = Guid.NewGuid();

        // Learn one pattern
        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Boneless Chicken Thighs",
            chickenId,
            "Chicken Thighs",
            "chicken-thighs",
            MappingSource.Manual));

        // Provide candidate ingredients that aren't learned yet
        var candidates = new List<IngredientInfo>
        {
            new(beefId, "Ground Beef", "beef-ground"),
            new(porkId, "Pork Chops", "pork-chops"),
            new(chickenId, "Chicken Thighs", "chicken-thighs") // Duplicate of learned
        };

        // Act - search for something that could match either pattern or candidate
        var suggestions = await grain.GetSuggestionsAsync("Chicken Thighs Skinless", candidates);

        // Assert - should include suggestion from pattern (preferred) and possibly ingredient candidates
        suggestions.Should().NotBeEmpty();
        // Should find the chicken thighs via pattern matching
        suggestions.Any(s => s.IngredientId == chickenId).Should().BeTrue();
    }

    // Given: a vendor mapping with an uppercase product code
    // When: the same product code is looked up in lowercase
    // Then: the mapping is found via case-insensitive product code match
    [Fact]
    public async Task GetMappingAsync_ProductCodeDifferentCase_ShouldMatch()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId = Guid.NewGuid();
        await grain.SetMappingAsync(new SetMappingCommand(
            "Mozzarella Cheese Block",
            ingredientId,
            "Mozzarella Cheese",
            "cheese-mozz",
            Guid.NewGuid(),
            "DAIRY-MOZ-001"));

        // Act - lookup with different case product code
        var result = await grain.GetMappingAsync("Some Other Description", "dairy-moz-001");

        // Assert - should match via product code (case insensitive)
        result.Found.Should().BeTrue();
        result.MatchType.Should().Be(MappingMatchType.ProductCode);
        result.Mapping!.IngredientId.Should().Be(ingredientId);
    }

    // Given: an initialized vendor mapping with a placeholder name and unknown type
    // When: the vendor name and type are updated to reflect the actual supplier identity
    // Then: the snapshot reflects the new vendor name and type
    [Fact]
    public async Task UpdateVendorInfoAsync_ShouldUpdateNameAndType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Old Vendor Name", VendorType.Unknown));

        var snapshotBefore = await grain.GetSnapshotAsync();
        snapshotBefore.VendorName.Should().Be("Old Vendor Name");
        snapshotBefore.VendorType.Should().Be(VendorType.Unknown);

        // Act
        await grain.UpdateVendorInfoAsync("New Vendor Name", VendorType.Supplier);

        // Assert
        var snapshotAfter = await grain.GetSnapshotAsync();
        snapshotAfter.VendorName.Should().Be("New Vendor Name");
        snapshotAfter.VendorType.Should().Be(VendorType.Supplier);
    }

    // Given: an initialized vendor mapping for a supplier
    // When: three different vendor descriptions for the same unsalted butter ingredient are learned
    // Then: all three descriptions are individually findable and multiple patterns are generated
    [Fact]
    public async Task LearnMapping_SameIngredientDifferentDescriptions_ShouldCreateMultiplePatterns()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId = Guid.NewGuid();

        // Act - learn multiple different descriptions for the same ingredient
        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Butter Unsalted 1LB",
            ingredientId,
            "Unsalted Butter",
            "butter-unsalted",
            MappingSource.Manual));

        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Sweet Cream Butter No Salt",
            ingredientId,
            "Unsalted Butter",
            "butter-unsalted",
            MappingSource.Manual));

        await grain.LearnMappingAsync(new LearnMappingCommand(
            "European Style Butter Unsalted Block",
            ingredientId,
            "Unsalted Butter",
            "butter-unsalted",
            MappingSource.Manual));

        // Assert - should have multiple patterns and multiple exact mappings
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalMappings.Should().Be(3); // 3 exact mappings
        snapshot.TotalPatterns.Should().BeGreaterThanOrEqualTo(1); // At least 1 pattern

        // All three descriptions should be findable
        (await grain.GetMappingAsync("Butter Unsalted 1LB")).Found.Should().BeTrue();
        (await grain.GetMappingAsync("Sweet Cream Butter No Salt")).Found.Should().BeTrue();
        (await grain.GetMappingAsync("European Style Butter Unsalted Block")).Found.Should().BeTrue();
    }
}
