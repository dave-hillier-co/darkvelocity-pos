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
}
