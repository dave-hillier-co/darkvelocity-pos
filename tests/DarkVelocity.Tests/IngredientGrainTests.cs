using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.TestingHost;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for the Ingredient grain and related functionality.
/// </summary>
[Collection(ClusterCollection.Name)]
public class IngredientGrainTests
{
    private readonly TestCluster _cluster;

    public IngredientGrainTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    // Given: A new ingredient definition for all-purpose flour with SKU, base unit, and cost
    // When: The ingredient is created in the catalog
    // Then: The ingredient is registered with the correct properties and is not archived
    [Fact]
    public async Task CreateIngredient_WithBasicInfo_CreatesSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));

        // Act
        var snapshot = await grain.CreateAsync(new CreateIngredientCommand(
            Name: "Flour, All-Purpose",
            Description: "Standard white flour for baking",
            Sku: "FLOUR-001",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.002m,
            CostUnit: "g",
            Category: "Dry Goods"));

        // Assert
        Assert.Equal(ingredientId, snapshot.IngredientId);
        Assert.Equal(orgId, snapshot.OrgId);
        Assert.Equal("Flour, All-Purpose", snapshot.Name);
        Assert.Equal("g", snapshot.BaseUnit);
        Assert.Equal(0.002m, snapshot.DefaultCostPerUnit);
        Assert.False(snapshot.IsArchived);
    }

    // Given: A new wheat bread ingredient with gluten (contains) and soy (may contain) allergen declarations
    // When: The ingredient is created with allergen information
    // Then: Both allergens are recorded with their respective declaration types and notes
    [Fact]
    public async Task CreateIngredient_WithAllergens_TracksAllergensProperly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));

        var allergens = new[]
        {
            new AllergenDeclarationCommand(StandardAllergens.Gluten, AllergenDeclarationType.Contains),
            new AllergenDeclarationCommand(StandardAllergens.Soy, AllergenDeclarationType.MayContain, "May be processed on shared equipment")
        };

        // Act
        var snapshot = await grain.CreateAsync(new CreateIngredientCommand(
            Name: "Wheat Bread",
            BaseUnit: "each",
            DefaultCostPerUnit: 1.50m,
            CostUnit: "each",
            Allergens: allergens));

        // Assert
        Assert.Equal(2, snapshot.Allergens.Count);

        var glutenAllergen = snapshot.Allergens.First(a => a.Allergen == StandardAllergens.Gluten);
        Assert.Equal(AllergenDeclarationType.Contains, glutenAllergen.DeclarationType);

        var soyAllergen = snapshot.Allergens.First(a => a.Allergen == StandardAllergens.Soy);
        Assert.Equal(AllergenDeclarationType.MayContain, soyAllergen.DeclarationType);
        Assert.Contains("shared equipment", soyAllergen.Notes);
    }

    // Given: A new flour ingredient with nutritional data (calories, protein, carbohydrates, fat, fiber per 100g)
    // When: The ingredient is created with its nutritional profile
    // Then: The nutritional information is stored and retrievable per 100g
    [Fact]
    public async Task CreateIngredient_WithNutrition_TracksNutritionData()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));

        var nutrition = new IngredientNutritionCommand(
            CaloriesPer100g: 364m,
            ProteinPer100g: 10m,
            CarbohydratesPer100g: 76m,
            FatPer100g: 1m,
            FiberPer100g: 3m);

        // Act
        var snapshot = await grain.CreateAsync(new CreateIngredientCommand(
            Name: "All-Purpose Flour",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.002m,
            CostUnit: "g",
            Nutrition: nutrition));

        // Assert
        Assert.NotNull(snapshot.Nutrition);
        Assert.Equal(364m, snapshot.Nutrition.CaloriesPer100g);
        Assert.Equal(10m, snapshot.Nutrition.ProteinPer100g);
        Assert.Equal(76m, snapshot.Nutrition.CarbohydratesPer100g);
    }

    // Given: An existing olive oil ingredient at $0.01 per ml
    // When: The cost is updated to $0.015 per ml due to new supplier pricing
    // Then: The current cost reflects the update and cost history records both the initial and updated entries
    [Fact]
    public async Task UpdateIngredientCost_UpdatesCostAndTracksHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));

        await grain.CreateAsync(new CreateIngredientCommand(
            Name: "Olive Oil",
            BaseUnit: "ml",
            DefaultCostPerUnit: 0.01m,
            CostUnit: "ml"));

        // Act
        await grain.UpdateCostAsync(new UpdateIngredientCostCommand(
            NewCost: 0.015m,
            Source: "New supplier pricing"));

        var snapshot = await grain.GetSnapshotAsync();
        var history = await grain.GetCostHistoryAsync();

        // Assert
        Assert.Equal(0.015m, snapshot.DefaultCostPerUnit);
        Assert.Equal(2, history.Count); // Initial + update
        Assert.Equal(0.015m, history[0].CostPerUnit); // Most recent first
        Assert.Equal("New supplier pricing", history[0].Source);
    }

    // Given: Soy sauce with a single soy allergen declaration
    // When: The allergen list is updated to include both soy and gluten
    // Then: The allergen list is fully replaced with the two new declarations
    [Fact]
    public async Task UpdateAllergens_ReplacesAllAllergens()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));

        await grain.CreateAsync(new CreateIngredientCommand(
            Name: "Soy Sauce",
            BaseUnit: "ml",
            DefaultCostPerUnit: 0.01m,
            CostUnit: "ml",
            Allergens: [new AllergenDeclarationCommand(StandardAllergens.Soy)]));

        // Act
        await grain.UpdateAllergensAsync([
            new AllergenDeclarationCommand(StandardAllergens.Soy, AllergenDeclarationType.Contains),
            new AllergenDeclarationCommand(StandardAllergens.Gluten, AllergenDeclarationType.Contains)
        ]);

        var allergens = await grain.GetAllergensAsync();

        // Assert
        Assert.Equal(2, allergens.Count);
        Assert.Contains(allergens, a => a.Allergen == StandardAllergens.Soy);
        Assert.Contains(allergens, a => a.Allergen == StandardAllergens.Gluten);
    }

    // Given: A tomatoes ingredient without any supplier associations
    // When: A supplier is linked with SKU, price per case, and base unit conversion factor
    // Then: The supplier is associated as the preferred vendor with the correct pricing and unit mapping
    [Fact]
    public async Task LinkSupplier_AssociatesSupplierWithIngredient()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));

        await grain.CreateAsync(new CreateIngredientCommand(
            Name: "Tomatoes",
            BaseUnit: "kg",
            DefaultCostPerUnit: 2.50m,
            CostUnit: "kg"));

        // Act
        await grain.LinkSupplierAsync(new LinkSupplierCommand(
            SupplierId: supplierId,
            SupplierName: "Farm Fresh Produce",
            SupplierSku: "TOM-VINE-001",
            SupplierPrice: 12.00m,
            SupplierUnit: "case",
            ConversionToBaseUnit: 5.0m, // 5kg per case
            IsPreferred: true));

        var suppliers = await grain.GetSuppliersAsync();

        // Assert
        Assert.Single(suppliers);
        Assert.Equal(supplierId, suppliers[0].SupplierId);
        Assert.Equal("Farm Fresh Produce", suppliers[0].SupplierName);
        Assert.True(suppliers[0].IsPreferred);
    }

    // Given: A house marinara sauce ingredient in the catalog
    // When: The ingredient is linked to a sub-recipe as its production output
    // Then: The ingredient is marked as a sub-recipe output with the producing recipe identified
    [Fact]
    public async Task LinkToSubRecipe_MarksAsSubRecipeOutput()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));

        await grain.CreateAsync(new CreateIngredientCommand(
            Name: "House Marinara Sauce",
            BaseUnit: "ml",
            DefaultCostPerUnit: 0.005m,
            CostUnit: "ml"));

        // Act
        await grain.LinkToSubRecipeAsync("marinara-sauce-recipe");

        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        Assert.True(snapshot.IsSubRecipeOutput);
        Assert.Equal("marinara-sauce-recipe", snapshot.ProducedByRecipeId);
    }

    // Given: An active ingredient in the catalog
    // When: The ingredient is archived because the supplier discontinued it
    // Then: The ingredient is marked as archived
    [Fact]
    public async Task ArchiveIngredient_SetsArchivedStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));

        await grain.CreateAsync(new CreateIngredientCommand(
            Name: "Discontinued Ingredient",
            BaseUnit: "each",
            DefaultCostPerUnit: 1.0m,
            CostUnit: "each"));

        // Act
        await grain.ArchiveAsync(reason: "Product discontinued by supplier");

        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        Assert.True(snapshot.IsArchived);
    }

    // Given: A previously archived seasonal ingredient
    // When: The ingredient is restored to active status
    // Then: The ingredient's archived flag is removed, making it available again
    [Fact]
    public async Task RestoreIngredient_RemovesArchivedStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));

        await grain.CreateAsync(new CreateIngredientCommand(
            Name: "Seasonal Ingredient",
            BaseUnit: "kg",
            DefaultCostPerUnit: 5.0m,
            CostUnit: "kg"));

        await grain.ArchiveAsync();

        // Act
        await grain.RestoreAsync();

        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        Assert.False(snapshot.IsArchived);
    }

    // Given: A peanut butter ingredient with a peanuts allergen declaration
    // When: Allergen checks are performed for peanuts and dairy
    // Then: The peanut check returns true and the dairy check returns false
    [Fact]
    public async Task ContainsAllergen_ReturnsCorrectResult()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));

        await grain.CreateAsync(new CreateIngredientCommand(
            Name: "Peanut Butter",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.01m,
            CostUnit: "g",
            Allergens: [new AllergenDeclarationCommand(StandardAllergens.Peanuts)]));

        // Act & Assert
        Assert.True(await grain.ContainsAllergenAsync(StandardAllergens.Peanuts));
        Assert.False(await grain.ContainsAllergenAsync(StandardAllergens.Dairy));
    }
}

/// <summary>
/// Tests for the IngredientRegistry grain.
/// </summary>
[Collection(ClusterCollection.Name)]
public class IngredientRegistryGrainTests
{
    private readonly TestCluster _cluster;

    public IngredientRegistryGrainTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    // Given: An empty ingredient registry for an organization
    // When: An ingredient summary is registered in the catalog
    // Then: The ingredient appears in the registry listing
    [Fact]
    public async Task RegisterIngredient_AddsToRegistry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var registry = _cluster.GrainFactory.GetGrain<IIngredientRegistryGrain>(
            GrainKeys.IngredientRegistry(orgId));

        var summary = new IngredientSummary(
            IngredientId: Guid.NewGuid(),
            Name: "Test Ingredient",
            Sku: "TEST-001",
            Category: "Test Category",
            DefaultCostPerUnit: 1.0m,
            BaseUnit: "each",
            AllergenTags: [StandardAllergens.Gluten],
            IsSubRecipeOutput: false,
            IsArchived: false,
            LastModified: DateTimeOffset.UtcNow);

        // Act
        await registry.RegisterIngredientAsync(summary);
        var ingredients = await registry.GetIngredientsAsync();

        // Assert
        Assert.Contains(ingredients, i => i.Name == "Test Ingredient");
    }

    // Given: A registry containing tomato paste, tomato sauce, and olive oil
    // When: A search for "tomato" is performed
    // Then: Both tomato ingredients are returned while olive oil is excluded
    [Fact]
    public async Task SearchIngredients_FindsByName()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var registry = _cluster.GrainFactory.GetGrain<IIngredientRegistryGrain>(
            GrainKeys.IngredientRegistry(orgId));

        await registry.RegisterIngredientAsync(new IngredientSummary(
            Guid.NewGuid(), "Tomato Paste", "TOM-PASTE-001", "Canned Goods",
            0.5m, "g", [], false, false, DateTimeOffset.UtcNow));

        await registry.RegisterIngredientAsync(new IngredientSummary(
            Guid.NewGuid(), "Tomato Sauce", "TOM-SAUCE-001", "Canned Goods",
            0.3m, "ml", [], false, false, DateTimeOffset.UtcNow));

        await registry.RegisterIngredientAsync(new IngredientSummary(
            Guid.NewGuid(), "Olive Oil", "OIL-001", "Oils",
            0.02m, "ml", [], false, false, DateTimeOffset.UtcNow));

        // Act
        var results = await registry.SearchIngredientsAsync("tomato");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains("Tomato", r.Name, StringComparison.OrdinalIgnoreCase));
    }

    // Given: A registry with wheat flour (gluten), almond flour (tree nuts), and rice flour (no allergens)
    // When: Filtering ingredients by gluten allergen
    // Then: Only wheat flour is returned
    [Fact]
    public async Task GetIngredientsByAllergen_FiltersCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var registry = _cluster.GrainFactory.GetGrain<IIngredientRegistryGrain>(
            GrainKeys.IngredientRegistry(orgId));

        await registry.RegisterIngredientAsync(new IngredientSummary(
            Guid.NewGuid(), "Wheat Flour", null, "Dry Goods",
            0.002m, "g", [StandardAllergens.Gluten], false, false, DateTimeOffset.UtcNow));

        await registry.RegisterIngredientAsync(new IngredientSummary(
            Guid.NewGuid(), "Almond Flour", null, "Dry Goods",
            0.02m, "g", [StandardAllergens.TreeNuts], false, false, DateTimeOffset.UtcNow));

        await registry.RegisterIngredientAsync(new IngredientSummary(
            Guid.NewGuid(), "Rice Flour", null, "Dry Goods",
            0.003m, "g", [], false, false, DateTimeOffset.UtcNow));

        // Act
        var glutenIngredients = await registry.GetIngredientsByAllergenAsync(StandardAllergens.Gluten);

        // Assert
        Assert.Single(glutenIngredients);
        Assert.Equal("Wheat Flour", glutenIngredients[0].Name);
    }

    // Given: A registry with house sauce (sub-recipe output) and raw tomatoes (regular ingredient)
    // When: Querying for sub-recipe output ingredients
    // Then: Only the house sauce is returned
    [Fact]
    public async Task GetSubRecipeOutputs_ReturnsOnlySubRecipeOutputs()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var registry = _cluster.GrainFactory.GetGrain<IIngredientRegistryGrain>(
            GrainKeys.IngredientRegistry(orgId));

        await registry.RegisterIngredientAsync(new IngredientSummary(
            Guid.NewGuid(), "House Sauce", null, "Sauces",
            0.005m, "ml", [], true, false, DateTimeOffset.UtcNow)); // Sub-recipe output

        await registry.RegisterIngredientAsync(new IngredientSummary(
            Guid.NewGuid(), "Raw Tomatoes", null, "Produce",
            0.003m, "g", [], false, false, DateTimeOffset.UtcNow)); // Regular ingredient

        // Act
        var subRecipeOutputs = await registry.GetSubRecipeOutputsAsync();

        // Assert
        Assert.Single(subRecipeOutputs);
        Assert.Equal("House Sauce", subRecipeOutputs[0].Name);
    }
}
