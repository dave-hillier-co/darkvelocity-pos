using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Vendor mapping conflict tests covering:
/// - Duplicate mapping handling
/// - Conflicting ingredient mappings
/// - Mapping priority resolution
/// - Pattern collision scenarios
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class VendorMappingConflictTests
{
    private readonly TestClusterFixture _fixture;

    public VendorMappingConflictTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IVendorItemMappingGrain GetGrain(Guid orgId, string vendorId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IVendorItemMappingGrain>(
            GrainKeys.VendorItemMapping(orgId, vendorId));
    }

    // ============================================================================
    // Duplicate Description Handling Tests
    // ============================================================================

    [Fact]
    public async Task SetMapping_SameDescription_ShouldOverwriteExisting()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId1 = Guid.NewGuid();
        var ingredientId2 = Guid.NewGuid();

        // First mapping
        await grain.SetMappingAsync(new SetMappingCommand(
            "Chicken Breast 5LB",
            ingredientId1,
            "Chicken Breast",
            "chicken-breast",
            Guid.NewGuid(),
            "CHKN-001",
            10.00m,
            "lb"));

        // Act - same description, different ingredient
        await grain.SetMappingAsync(new SetMappingCommand(
            "Chicken Breast 5LB",
            ingredientId2,
            "Chicken Breast Premium",
            "chicken-breast-premium",
            Guid.NewGuid(),
            "CHKN-002",
            12.00m,
            "lb"));

        // Assert - should overwrite with new ingredient
        var result = await grain.GetMappingAsync("Chicken Breast 5LB");
        result.Found.Should().BeTrue();
        result.Mapping!.IngredientId.Should().Be(ingredientId2);
        result.Mapping.IngredientName.Should().Be("Chicken Breast Premium");
        result.Mapping.ExpectedUnitPrice.Should().Be(12.00m);

        // Check total mappings - should still be 1
        var allMappings = await grain.GetAllMappingsAsync();
        allMappings.Should().HaveCount(1);
    }

    [Fact]
    public async Task SetMapping_SameIngredientDifferentDescriptions_ShouldCreateMultipleMappings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId = Guid.NewGuid();

        // Act - same ingredient, different vendor descriptions
        await grain.SetMappingAsync(new SetMappingCommand(
            "GROUND BEEF 80/20",
            ingredientId,
            "Ground Beef",
            "beef-ground",
            Guid.NewGuid()));

        await grain.SetMappingAsync(new SetMappingCommand(
            "GB 80-20 5LB",
            ingredientId,
            "Ground Beef",
            "beef-ground",
            Guid.NewGuid()));

        await grain.SetMappingAsync(new SetMappingCommand(
            "Beef Ground 80% Lean",
            ingredientId,
            "Ground Beef",
            "beef-ground",
            Guid.NewGuid()));

        // Assert - all three descriptions should map to the same ingredient
        var allMappings = await grain.GetAllMappingsAsync();
        allMappings.Should().HaveCount(3);
        allMappings.All(m => m.IngredientId == ingredientId).Should().BeTrue();

        // All three lookups should work
        (await grain.GetMappingAsync("GROUND BEEF 80/20")).Found.Should().BeTrue();
        (await grain.GetMappingAsync("GB 80-20 5LB")).Found.Should().BeTrue();
        (await grain.GetMappingAsync("Beef Ground 80% Lean")).Found.Should().BeTrue();
    }

    // ============================================================================
    // Product Code Conflict Tests
    // ============================================================================

    [Fact]
    public async Task ProductCodeLookup_SameCode_DifferentDescriptions_ShouldReturnByCode()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId = Guid.NewGuid();

        // Create mapping with product code
        await grain.SetMappingAsync(new SetMappingCommand(
            "Original Olive Oil Description",
            ingredientId,
            "Olive Oil",
            "oil-olive",
            Guid.NewGuid(),
            "OIL-OLV-001"));

        // Act - lookup by product code with different description
        var result = await grain.GetMappingAsync("Some Completely Different Description", "OIL-OLV-001");

        // Assert - should find by product code
        result.Found.Should().BeTrue();
        result.MatchType.Should().Be(MappingMatchType.ProductCode);
        result.Mapping!.IngredientId.Should().Be(ingredientId);
    }

    [Fact]
    public async Task ProductCodeLookup_CodeNotFound_ShouldFallbackToDescription()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId = Guid.NewGuid();

        // Create mapping without product code
        await grain.SetMappingAsync(new SetMappingCommand(
            "Fresh Basil Leaves",
            ingredientId,
            "Fresh Basil",
            "basil-fresh",
            Guid.NewGuid()));

        // Act - lookup with product code that doesn't exist
        var result = await grain.GetMappingAsync("Fresh Basil Leaves", "NONEXISTENT-CODE");

        // Assert - should fall back to exact description match
        result.Found.Should().BeTrue();
        result.MatchType.Should().Be(MappingMatchType.ExactDescription);
    }

    // ============================================================================
    // Pattern Collision Tests
    // ============================================================================

    [Fact]
    public async Task LearnMapping_SimilarPatterns_ShouldHandleGracefully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var chickenBreastId = Guid.NewGuid();
        var chickenThighId = Guid.NewGuid();

        // Act - learn similar patterns for different ingredients
        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Chicken Breast Boneless Skinless",
            chickenBreastId,
            "Chicken Breast",
            "chicken-breast",
            MappingSource.Manual));

        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Chicken Thigh Boneless Skinless",
            chickenThighId,
            "Chicken Thigh",
            "chicken-thigh",
            MappingSource.Manual));

        // Assert - both mappings should be distinct
        var breastResult = await grain.GetMappingAsync("Chicken Breast Boneless Skinless");
        breastResult.Mapping!.IngredientId.Should().Be(chickenBreastId);

        var thighResult = await grain.GetMappingAsync("Chicken Thigh Boneless Skinless");
        thighResult.Mapping!.IngredientId.Should().Be(chickenThighId);
    }

    [Fact]
    public async Task Suggestions_OverlappingPatterns_ShouldReturnMostRelevant()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var beefPattyId = Guid.NewGuid();
        var beefGroundId = Guid.NewGuid();
        var beefStripId = Guid.NewGuid();

        // Learn overlapping beef patterns
        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Beef Patty 4oz Frozen",
            beefPattyId,
            "Beef Patty",
            "beef-patty",
            MappingSource.Manual));

        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Ground Beef 80/20 Fresh",
            beefGroundId,
            "Ground Beef",
            "beef-ground",
            MappingSource.Manual));

        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Beef Strip Steak Prime",
            beefStripId,
            "Beef Strip",
            "beef-strip",
            MappingSource.Manual));

        // Act - search for something that matches multiple patterns
        var suggestions = await grain.GetSuggestionsAsync("Beef Patty Fresh 6oz");

        // Assert - should include beef patty as highest match
        suggestions.Should().NotBeEmpty();
        suggestions[0].IngredientId.Should().Be(beefPattyId);
    }

    // ============================================================================
    // Mapping Update/Delete Conflict Tests
    // ============================================================================

    [Fact]
    public async Task DeleteMapping_ShouldNotAffectOtherMappings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId = Guid.NewGuid();

        await grain.SetMappingAsync(new SetMappingCommand("Description A", ingredientId, "Ingredient", "sku-1", Guid.NewGuid()));
        await grain.SetMappingAsync(new SetMappingCommand("Description B", ingredientId, "Ingredient", "sku-1", Guid.NewGuid()));
        await grain.SetMappingAsync(new SetMappingCommand("Description C", ingredientId, "Ingredient", "sku-1", Guid.NewGuid()));

        // Act - delete one mapping
        await grain.DeleteMappingAsync(new DeleteMappingCommand("Description B", Guid.NewGuid()));

        // Assert - other mappings should still exist
        (await grain.GetMappingAsync("Description A")).Found.Should().BeTrue();
        (await grain.GetMappingAsync("Description B")).Found.Should().BeFalse();
        (await grain.GetMappingAsync("Description C")).Found.Should().BeTrue();

        var allMappings = await grain.GetAllMappingsAsync();
        allMappings.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteMapping_NonExistent_ShouldNotThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        await grain.SetMappingAsync(new SetMappingCommand("Existing Item", Guid.NewGuid(), "Ingredient", "sku", Guid.NewGuid()));

        // Act - delete non-existent mapping
        var act = async () => await grain.DeleteMappingAsync(new DeleteMappingCommand("Non-existent Item", Guid.NewGuid()));

        // Assert - should not throw
        await act.Should().NotThrowAsync();
    }

    // ============================================================================
    // Source Priority Tests
    // ============================================================================

    [Fact]
    public async Task LearnMapping_ManualSource_ShouldHaveHighConfidence()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        // Act
        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Premium Olive Oil 1L",
            Guid.NewGuid(),
            "Olive Oil Premium",
            "oil-olive-premium",
            MappingSource.Manual,
            1.0m));

        // Assert
        var result = await grain.GetMappingAsync("Premium Olive Oil 1L");
        result.Mapping!.Source.Should().Be(MappingSource.Manual);
        result.Mapping.Confidence.Should().Be(1.0m);
    }

    [Fact]
    public async Task LearnMapping_AutoSource_ShouldHaveLowerConfidence()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        // Act
        await grain.LearnMappingAsync(new LearnMappingCommand(
            "Unknown Product XYZ",
            Guid.NewGuid(),
            "Inferred Product",
            "inferred-sku",
            MappingSource.Inferred,
            0.75m));

        // Assert
        var result = await grain.GetMappingAsync("Unknown Product XYZ");
        result.Mapping!.Source.Should().Be(MappingSource.Inferred);
        result.Mapping.Confidence.Should().Be(0.75m);
    }

    // ============================================================================
    // Multi-Vendor Tests
    // ============================================================================

    [Fact]
    public async Task DifferentVendors_SameMappings_ShouldBeIsolated()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendor1 = $"sysco-{Guid.NewGuid():N}";
        var vendor2 = $"usfoods-{Guid.NewGuid():N}";

        var grain1 = GetGrain(orgId, vendor1);
        var grain2 = GetGrain(orgId, vendor2);

        await grain1.InitializeAsync(new InitializeVendorMappingCommand(orgId, vendor1, "Sysco", VendorType.Supplier));
        await grain2.InitializeAsync(new InitializeVendorMappingCommand(orgId, vendor2, "US Foods", VendorType.Supplier));

        var ingredient1 = Guid.NewGuid();
        var ingredient2 = Guid.NewGuid();

        // Act - same description, different ingredients per vendor
        await grain1.SetMappingAsync(new SetMappingCommand(
            "Chicken Breast",
            ingredient1,
            "Sysco Chicken Breast",
            "sysco-chicken",
            Guid.NewGuid()));

        await grain2.SetMappingAsync(new SetMappingCommand(
            "Chicken Breast",
            ingredient2,
            "USF Chicken Breast",
            "usf-chicken",
            Guid.NewGuid()));

        // Assert - each vendor has its own mapping
        var result1 = await grain1.GetMappingAsync("Chicken Breast");
        result1.Mapping!.IngredientId.Should().Be(ingredient1);

        var result2 = await grain2.GetMappingAsync("Chicken Breast");
        result2.Mapping!.IngredientId.Should().Be(ingredient2);
    }

    // ============================================================================
    // Case Sensitivity Tests
    // ============================================================================

    [Fact]
    public async Task Mapping_CaseInsensitive_ShouldMatchDifferentCases()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        await grain.SetMappingAsync(new SetMappingCommand(
            "FRESH ATLANTIC SALMON",
            Guid.NewGuid(),
            "Atlantic Salmon",
            "salmon-atlantic",
            Guid.NewGuid()));

        // Act - search with different cases
        var upperResult = await grain.GetMappingAsync("FRESH ATLANTIC SALMON");
        var lowerResult = await grain.GetMappingAsync("fresh atlantic salmon");
        var mixedResult = await grain.GetMappingAsync("Fresh Atlantic Salmon");
        var weirdResult = await grain.GetMappingAsync("fReSh AtLaNtIc SaLmOn");

        // Assert - all should match
        upperResult.Found.Should().BeTrue();
        lowerResult.Found.Should().BeTrue();
        mixedResult.Found.Should().BeTrue();
        weirdResult.Found.Should().BeTrue();
    }

    // ============================================================================
    // Whitespace and Special Character Tests
    // ============================================================================

    [Fact]
    public async Task Mapping_WhitespaceVariations_ShouldHandleGracefully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        await grain.SetMappingAsync(new SetMappingCommand(
            "Ground Beef 80/20",
            Guid.NewGuid(),
            "Ground Beef",
            "beef-ground",
            Guid.NewGuid()));

        // Act - extra whitespace should be normalized
        var result = await grain.GetMappingAsync("Ground Beef 80/20");

        // Assert
        result.Found.Should().BeTrue();
    }

    [Fact]
    public async Task Mapping_SpecialCharacters_ShouldHandle()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var vendorId = $"vendor-{Guid.NewGuid():N}";
        var grain = GetGrain(orgId, vendorId);

        await grain.InitializeAsync(new InitializeVendorMappingCommand(
            orgId, vendorId, "Test Vendor", VendorType.Supplier));

        var ingredientId = Guid.NewGuid();

        // Act - descriptions with special characters
        await grain.SetMappingAsync(new SetMappingCommand(
            "Ham & Cheese (Pre-Made)",
            ingredientId,
            "Ham and Cheese",
            "ham-cheese",
            Guid.NewGuid()));

        // Assert
        var result = await grain.GetMappingAsync("Ham & Cheese (Pre-Made)");
        result.Found.Should().BeTrue();
        result.Mapping!.IngredientId.Should().Be(ingredientId);
    }
}
