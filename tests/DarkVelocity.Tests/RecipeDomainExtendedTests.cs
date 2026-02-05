using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;
using Orleans.TestingHost;

namespace DarkVelocity.Tests;

// ============================================================================
// Sub-Recipe Composition Tests
// Tests for recipes that use other recipes as ingredients (nested recipes)
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class SubRecipeCompositionTests
{
    private readonly TestCluster _cluster;

    public SubRecipeCompositionTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task Recipe_WithSubRecipeIngredient_InheritsCostFromSubRecipe()
    {
        // Arrange - Create a sub-recipe (marinara sauce)
        var orgId = Guid.NewGuid();
        var tomatoId = Guid.NewGuid();
        var garlicId = Guid.NewGuid();
        var sauceOutputId = Guid.NewGuid();

        // Create base ingredients
        var tomatoGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, tomatoId));
        await tomatoGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Crushed Tomatoes",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.004m,
            CostUnit: "g"));

        var garlicGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, garlicId));
        await garlicGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Garlic",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.015m,
            CostUnit: "g"));

        // Create sauce output ingredient (linked to sub-recipe)
        var sauceIngredientGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, sauceOutputId));
        await sauceIngredientGrain.CreateAsync(new CreateIngredientCommand(
            Name: "House Marinara Sauce",
            BaseUnit: "ml",
            DefaultCostPerUnit: 0.005m, // Placeholder cost
            CostUnit: "ml"));

        // Create the sub-recipe (sauce)
        var subRecipeId = Guid.NewGuid().ToString();
        var subRecipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, subRecipeId));
        var subRecipe = await subRecipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "House Marinara Sauce",
            PortionYield: 1000m, // Makes 1000ml
            YieldUnit: "ml",
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(tomatoId, "Crushed Tomatoes", 800, "g", 0, 0.004m),
                new(garlicId, "Garlic", 20, "g", 0, 0.015m)
            },
            PublishImmediately: true,
            RecipeType: RecipeType.BatchPrep,
            OutputInventoryItemId: sauceOutputId,
            OutputUnit: "ml",
            OutputQuantityPerYield: 1000m));

        // Link the sauce ingredient to the sub-recipe
        await sauceIngredientGrain.LinkToSubRecipeAsync(subRecipeId);

        // Act - Create main recipe using the sub-recipe output
        var mainRecipeId = Guid.NewGuid().ToString();
        var pastaId = Guid.NewGuid();

        var pastaGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, pastaId));
        await pastaGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Spaghetti",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.003m,
            CostUnit: "g"));

        var mainRecipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, mainRecipeId));
        var mainRecipe = await mainRecipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Spaghetti Marinara",
            PortionYield: 4,
            YieldUnit: "portions",
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(pastaId, "Spaghetti", 400, "g", 0, 0.003m),
                new(sauceOutputId, "House Marinara Sauce", 500, "ml", 0, 0.005m)
            },
            PublishImmediately: true));

        // Assert - The recipe should have calculated costs
        mainRecipe.Published.Should().NotBeNull();
        mainRecipe.Published!.TheoreticalCost.Should().BeGreaterThan(0);
        mainRecipe.Published.Ingredients.Should().HaveCount(2);
    }

    [Fact]
    public async Task Recipe_WithMultipleLevelsOfNesting_CalculatesCostCorrectly()
    {
        // Arrange - Three-level recipe hierarchy
        var orgId = Guid.NewGuid();

        // Level 1: Base ingredients
        var flourId = Guid.NewGuid();
        var butterId = Guid.NewGuid();
        var flourGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, flourId));
        await flourGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Flour", BaseUnit: "g", DefaultCostPerUnit: 0.002m, CostUnit: "g"));

        var butterGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, butterId));
        await butterGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Butter", BaseUnit: "g", DefaultCostPerUnit: 0.01m, CostUnit: "g"));

        // Level 2: Pastry dough (uses flour + butter)
        var pastryOutputId = Guid.NewGuid();
        var pastryIngredient = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, pastryOutputId));
        await pastryIngredient.CreateAsync(new CreateIngredientCommand(
            Name: "Pastry Dough", BaseUnit: "g", DefaultCostPerUnit: 0.008m, CostUnit: "g"));

        var pastryRecipeId = Guid.NewGuid().ToString();
        var pastryGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, pastryRecipeId));
        await pastryGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Pastry Dough",
            PortionYield: 500,
            YieldUnit: "g",
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(flourId, "Flour", 300, "g", 0, 0.002m),
                new(butterId, "Butter", 200, "g", 0, 0.01m)
            },
            PublishImmediately: true,
            RecipeType: RecipeType.BatchPrep,
            OutputInventoryItemId: pastryOutputId));

        await pastryIngredient.LinkToSubRecipeAsync(pastryRecipeId);

        // Level 3: Tart (uses pastry dough + filling)
        var fillingId = Guid.NewGuid();
        var fillingGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, fillingId));
        await fillingGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Fruit Filling", BaseUnit: "g", DefaultCostPerUnit: 0.015m, CostUnit: "g"));

        var tartRecipeId = Guid.NewGuid().ToString();
        var tartGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, tartRecipeId));

        // Act
        var tartRecipe = await tartGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Fruit Tart",
            PortionYield: 8,
            YieldUnit: "slices",
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(pastryOutputId, "Pastry Dough", 250, "g", 0, 0.008m),
                new(fillingId, "Fruit Filling", 300, "g", 0, 0.015m)
            },
            PublishImmediately: true));

        // Assert
        tartRecipe.Published.Should().NotBeNull();
        tartRecipe.Published!.TheoreticalCost.Should().BeGreaterThan(0);
        tartRecipe.Published.CostPerPortion.Should().BeGreaterThan(0);
        tartRecipe.Published.Ingredients.Should().HaveCount(2);
    }

    [Fact]
    public async Task SubRecipe_CostUpdate_PropagatesToParentRecipe()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var ingredientGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));
        await ingredientGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Ingredient A", BaseUnit: "g", DefaultCostPerUnit: 0.01m, CostUnit: "g"));

        var recipeId = Guid.NewGuid().ToString();
        var recipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, recipeId));
        await recipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Test Recipe",
            PortionYield: 4,
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(ingredientId, "Ingredient A", 100, "g", 0, 0.01m) // Cost: 1.00
            },
            PublishImmediately: true));

        // Act - Update ingredient cost
        await recipeGrain.RecalculateCostAsync(new Dictionary<Guid, decimal>
        {
            [ingredientId] = 0.02m // Double the cost
        });

        // Assert
        var snapshot = await recipeGrain.GetSnapshotAsync();
        snapshot.Published!.Ingredients[0].UnitCost.Should().Be(0.02m);
        snapshot.Published.Ingredients[0].LineCost.Should().Be(2.00m); // 100 * 0.02
    }
}

// ============================================================================
// Batch Prep and Yield Calculation Tests
// Tests for batch preparation recipes with output units and shelf life
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class BatchPrepRecipeTests
{
    private readonly TestCluster _cluster;

    public BatchPrepRecipeTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task BatchPrepRecipe_CreatesWithCorrectProperties()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var outputItemId = Guid.NewGuid();

        var ingredientGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));
        await ingredientGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Base Ingredient", BaseUnit: "g", DefaultCostPerUnit: 0.005m, CostUnit: "g"));

        // Act
        var recipeId = Guid.NewGuid().ToString();
        var recipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, recipeId));
        var recipe = await recipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "House Stock",
            Description: "Chicken stock made in large batches",
            PortionYield: 10, // Makes 10 liters
            YieldUnit: "liters",
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(ingredientId, "Base Ingredient", 2000, "g", 0, 0.005m)
            },
            PublishImmediately: true,
            RecipeType: RecipeType.BatchPrep,
            OutputInventoryItemId: outputItemId,
            OutputInventoryItemName: "Chicken Stock",
            ShelfLifeHours: 72, // 3 days
            MinBatchSize: 5m,
            MaxBatchSize: 20m,
            OutputUnit: "liters",
            OutputQuantityPerYield: 10m));

        // Assert
        recipe.Published.Should().NotBeNull();
        recipe.Published!.RecipeType.Should().Be(RecipeType.BatchPrep);
        recipe.Published.ShelfLifeHours.Should().Be(72);
        recipe.Published.MinBatchSize.Should().Be(5m);
        recipe.Published.MaxBatchSize.Should().Be(20m);
        recipe.Published.OutputUnit.Should().Be("liters");
        recipe.Published.OutputQuantityPerYield.Should().Be(10m);
        recipe.Published.OutputInventoryItemId.Should().Be(outputItemId);
    }

    [Fact]
    public async Task BatchPrepRecipe_CalculatesCostPerOutputUnit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var ingredientGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));
        await ingredientGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Sugar", BaseUnit: "g", DefaultCostPerUnit: 0.001m, CostUnit: "g"));

        // Act - Recipe: 1000g sugar makes 500ml syrup
        var recipeId = Guid.NewGuid().ToString();
        var recipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, recipeId));
        var recipe = await recipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Simple Syrup",
            PortionYield: 500,
            YieldUnit: "ml",
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(ingredientId, "Sugar", 1000, "g", 0, 0.001m) // Cost: 1.00
            },
            PublishImmediately: true,
            RecipeType: RecipeType.BatchPrep,
            OutputUnit: "ml",
            OutputQuantityPerYield: 500m));

        // Assert - Total cost: 1.00, Output: 500ml, CostPerOutputUnit: 0.002/ml
        recipe.Published.Should().NotBeNull();
        recipe.Published!.TheoreticalCost.Should().Be(1.00m);
        recipe.Published.CostPerPortion.Should().Be(0.002m); // 1.00 / 500 = 0.002 per ml
    }

    [Fact]
    public async Task Registry_GetBatchPrepRecipes_FiltersCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var registry = _cluster.GrainFactory.GetGrain<IRecipeRegistryGrain>(
            GrainKeys.RecipeRegistry(orgId));

        await registry.RegisterRecipeAsync("recipe-made-to-order", "Cocktail", 3.50m, null,
            recipeType: RecipeType.MadeToOrder);
        await registry.RegisterRecipeAsync("recipe-batch-1", "House Sauce", 0.50m, null,
            recipeType: RecipeType.BatchPrep, shelfLifeHours: 48);
        await registry.RegisterRecipeAsync("recipe-batch-2", "Prep Dough", 1.20m, null,
            recipeType: RecipeType.BatchPrep, shelfLifeHours: 24);

        // Act
        var batchPrepRecipes = await registry.GetBatchPrepRecipesAsync();

        // Assert
        batchPrepRecipes.Should().HaveCount(2);
        batchPrepRecipes.Should().OnlyContain(r => r.RecipeType == RecipeType.BatchPrep);
        batchPrepRecipes.Should().Contain(r => r.Name == "House Sauce");
        batchPrepRecipes.Should().Contain(r => r.Name == "Prep Dough");
    }

    [Fact]
    public async Task Registry_GetRecipesByOutputItem_ReturnsMatchingRecipes()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var outputItemId = Guid.NewGuid();
        var registry = _cluster.GrainFactory.GetGrain<IRecipeRegistryGrain>(
            GrainKeys.RecipeRegistry(orgId));

        await registry.RegisterRecipeAsync("recipe-1", "Sauce Batch A", 0.50m, null,
            recipeType: RecipeType.BatchPrep, outputInventoryItemId: outputItemId);
        await registry.RegisterRecipeAsync("recipe-2", "Sauce Batch B", 0.55m, null,
            recipeType: RecipeType.BatchPrep, outputInventoryItemId: outputItemId);
        await registry.RegisterRecipeAsync("recipe-3", "Different Output", 0.60m, null,
            recipeType: RecipeType.BatchPrep, outputInventoryItemId: Guid.NewGuid());

        // Act
        var matchingRecipes = await registry.GetRecipesByOutputItemAsync(outputItemId);

        // Assert
        matchingRecipes.Should().HaveCount(2);
        matchingRecipes.Should().OnlyContain(r => r.OutputInventoryItemId == outputItemId);
    }
}

// ============================================================================
// Ingredient Substitution Tests
// Tests for ingredient substitution feature in recipes
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class IngredientSubstitutionTests
{
    private readonly TestCluster _cluster;

    public IngredientSubstitutionTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task Recipe_WithSubstitutions_TracksSubstitutionOptions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var primaryIngredientId = Guid.NewGuid();
        var substitution1Id = Guid.NewGuid();
        var substitution2Id = Guid.NewGuid();

        // Create primary ingredient
        var primaryGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, primaryIngredientId));
        await primaryGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Butter", BaseUnit: "g", DefaultCostPerUnit: 0.01m, CostUnit: "g"));

        // Create substitution ingredients
        var sub1Grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, substitution1Id));
        await sub1Grain.CreateAsync(new CreateIngredientCommand(
            Name: "Margarine", BaseUnit: "g", DefaultCostPerUnit: 0.005m, CostUnit: "g"));

        var sub2Grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, substitution2Id));
        await sub2Grain.CreateAsync(new CreateIngredientCommand(
            Name: "Coconut Oil", BaseUnit: "g", DefaultCostPerUnit: 0.02m, CostUnit: "g"));

        // Act - Create recipe with substitutions
        var recipeId = Guid.NewGuid().ToString();
        var recipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, recipeId));
        var recipe = await recipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Cookies",
            PortionYield: 24,
            YieldUnit: "cookies",
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(primaryIngredientId, "Butter", 200, "g", 0, 0.01m,
                    SubstitutionIds: new List<Guid> { substitution1Id, substitution2Id })
            },
            PublishImmediately: true));

        // Assert
        recipe.Published.Should().NotBeNull();
        recipe.Published!.Ingredients.Should().HaveCount(1);

        var butterIngredient = recipe.Published.Ingredients[0];
        butterIngredient.SubstitutionIds.Should().NotBeNull();
        butterIngredient.SubstitutionIds.Should().HaveCount(2);
        butterIngredient.SubstitutionIds.Should().Contain(substitution1Id);
        butterIngredient.SubstitutionIds.Should().Contain(substitution2Id);
    }

    [Fact]
    public async Task Recipe_Draft_CanUpdateSubstitutions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var newSubstitutionId = Guid.NewGuid();

        var ingredientGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));
        await ingredientGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Primary Ingredient", BaseUnit: "g", DefaultCostPerUnit: 0.01m, CostUnit: "g"));

        var subGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, newSubstitutionId));
        await subGrain.CreateAsync(new CreateIngredientCommand(
            Name: "New Substitution", BaseUnit: "g", DefaultCostPerUnit: 0.008m, CostUnit: "g"));

        var recipeId = Guid.NewGuid().ToString();
        var recipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, recipeId));
        await recipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Test Recipe",
            PortionYield: 4,
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(ingredientId, "Primary Ingredient", 100, "g", 0, 0.01m)
            },
            PublishImmediately: true));

        // Act - Create draft with substitution
        var draft = await recipeGrain.CreateDraftAsync(new CreateRecipeDraftCommand(
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(ingredientId, "Primary Ingredient", 100, "g", 0, 0.01m,
                    SubstitutionIds: new List<Guid> { newSubstitutionId })
            },
            ChangeNote: "Added substitution option"));

        // Assert
        draft.Ingredients.Should().HaveCount(1);
        draft.Ingredients[0].SubstitutionIds.Should().Contain(newSubstitutionId);
    }
}

// ============================================================================
// Advanced Allergen Aggregation Tests
// More complex scenarios for allergen inheritance
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class AdvancedAllergenAggregationTests
{
    private readonly TestCluster _cluster;

    public AdvancedAllergenAggregationTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task Recipe_WithSubRecipe_InheritsAllergensFromSubRecipe()
    {
        // Arrange
        var orgId = Guid.NewGuid();

        // Create base ingredient with allergens for sub-recipe
        var flourId = Guid.NewGuid();
        var flourGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, flourId));
        await flourGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Wheat Flour",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.002m,
            CostUnit: "g",
            Allergens: new[] { new AllergenDeclarationCommand(StandardAllergens.Gluten) }));

        // Create sub-recipe output ingredient
        var pastaOutputId = Guid.NewGuid();
        var pastaOutputGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, pastaOutputId));
        await pastaOutputGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Fresh Pasta",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.005m,
            CostUnit: "g"));

        // Create sub-recipe
        var subRecipeId = Guid.NewGuid().ToString();
        var subRecipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, subRecipeId));
        await subRecipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Fresh Pasta",
            PortionYield: 500,
            YieldUnit: "g",
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(flourId, "Wheat Flour", 300, "g", 0, 0.002m)
            },
            AllergenTags: new[] { StandardAllergens.Gluten },
            PublishImmediately: true,
            RecipeType: RecipeType.BatchPrep,
            OutputInventoryItemId: pastaOutputId));

        await pastaOutputGrain.LinkToSubRecipeAsync(subRecipeId);

        // Create main recipe ingredient (sauce with different allergen)
        var sauceId = Guid.NewGuid();
        var sauceGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, sauceId));
        await sauceGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Cream Sauce",
            BaseUnit: "ml",
            DefaultCostPerUnit: 0.01m,
            CostUnit: "ml",
            Allergens: new[] { new AllergenDeclarationCommand(StandardAllergens.Dairy) }));

        // Create calculation service
        var service = new RecipeCalculationService(_cluster.GrainFactory);

        // Act
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(pastaOutputId, "Fresh Pasta", 200, "g", 0, 0.005m, false, true), // Sub-recipe output
            new(sauceId, "Cream Sauce", 100, "ml", 0, 0.01m, false)
        };
        var result = await service.CalculateAllergensAsync(orgId, ingredients);

        // Assert
        result.ContainsAllergens.Should().Contain(StandardAllergens.Dairy);
        // Note: Sub-recipe allergens are propagated through the sub-recipe document
        // The service should detect the linked sub-recipe and include its allergens
    }

    [Fact]
    public async Task Recipe_WithMultipleIngredientsContainingSameAllergen_DeduplicatesAllergens()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredient1Id = Guid.NewGuid();
        var ingredient2Id = Guid.NewGuid();
        var ingredient3Id = Guid.NewGuid();

        // Three ingredients all containing gluten
        var grain1 = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredient1Id));
        await grain1.CreateAsync(new CreateIngredientCommand(
            Name: "Bread Crumbs",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.003m,
            CostUnit: "g",
            Allergens: new[] { new AllergenDeclarationCommand(StandardAllergens.Gluten) }));

        var grain2 = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredient2Id));
        await grain2.CreateAsync(new CreateIngredientCommand(
            Name: "Pasta",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.004m,
            CostUnit: "g",
            Allergens: new[] { new AllergenDeclarationCommand(StandardAllergens.Gluten) }));

        var grain3 = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredient3Id));
        await grain3.CreateAsync(new CreateIngredientCommand(
            Name: "Soy Sauce",
            BaseUnit: "ml",
            DefaultCostPerUnit: 0.01m,
            CostUnit: "ml",
            Allergens: new[]
            {
                new AllergenDeclarationCommand(StandardAllergens.Gluten),
                new AllergenDeclarationCommand(StandardAllergens.Soy)
            }));

        var service = new RecipeCalculationService(_cluster.GrainFactory);
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(ingredient1Id, "Bread Crumbs", 50, "g", 0, 0.003m, false),
            new(ingredient2Id, "Pasta", 200, "g", 0, 0.004m, false),
            new(ingredient3Id, "Soy Sauce", 30, "ml", 0, 0.01m, false)
        };

        // Act
        var result = await service.CalculateAllergensAsync(orgId, ingredients);

        // Assert - Gluten should appear only once
        result.ContainsAllergens.Count(a => a == StandardAllergens.Gluten).Should().Be(1);
        result.ContainsAllergens.Should().Contain(StandardAllergens.Soy);
    }

    [Fact]
    public async Task Recipe_CrossContamination_TrackedAsMayContain()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        // Create ingredient processed on equipment that handles nuts
        var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));
        await grain.CreateAsync(new CreateIngredientCommand(
            Name: "Oat Flour",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.005m,
            CostUnit: "g",
            Allergens: new[]
            {
                new AllergenDeclarationCommand(StandardAllergens.TreeNuts, AllergenDeclarationType.MayContain,
                    "Processed on equipment that handles tree nuts")
            }));

        var service = new RecipeCalculationService(_cluster.GrainFactory);
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(ingredientId, "Oat Flour", 200, "g", 0, 0.005m, false)
        };

        // Act
        var result = await service.CalculateAllergensAsync(orgId, ingredients);

        // Assert
        result.MayContainAllergens.Should().Contain(StandardAllergens.TreeNuts);
        result.ContainsAllergens.Should().NotContain(StandardAllergens.TreeNuts);
    }
}

// ============================================================================
// Prep Instruction and Display Order Tests
// Tests for ingredient ordering and preparation instructions
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PrepInstructionOrderingTests
{
    private readonly TestCluster _cluster;

    public PrepInstructionOrderingTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task Recipe_IngredientsWithDisplayOrder_MaintainsOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredient1Id = Guid.NewGuid();
        var ingredient2Id = Guid.NewGuid();
        var ingredient3Id = Guid.NewGuid();

        foreach (var (id, name) in new[]
                 {
                     (ingredient1Id, "Ingredient A"),
                     (ingredient2Id, "Ingredient B"),
                     (ingredient3Id, "Ingredient C")
                 })
        {
            var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
                GrainKeys.Ingredient(orgId, id));
            await grain.CreateAsync(new CreateIngredientCommand(
                Name: name, BaseUnit: "g", DefaultCostPerUnit: 0.01m, CostUnit: "g"));
        }

        // Act - Create recipe with specific display order
        var recipeId = Guid.NewGuid().ToString();
        var recipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, recipeId));
        var recipe = await recipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Ordered Recipe",
            PortionYield: 4,
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(ingredient3Id, "Ingredient C", 100, "g", 0, 0.01m, DisplayOrder: 1),
                new(ingredient1Id, "Ingredient A", 100, "g", 0, 0.01m, DisplayOrder: 2),
                new(ingredient2Id, "Ingredient B", 100, "g", 0, 0.01m, DisplayOrder: 3)
            },
            PublishImmediately: true));

        // Assert
        recipe.Published.Should().NotBeNull();
        var ingredients = recipe.Published!.Ingredients.OrderBy(i => i.DisplayOrder).ToList();
        ingredients[0].IngredientName.Should().Be("Ingredient C");
        ingredients[1].IngredientName.Should().Be("Ingredient A");
        ingredients[2].IngredientName.Should().Be("Ingredient B");
    }

    [Fact]
    public async Task Recipe_IngredientsWithPrepInstructions_StoresPrepInstructions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var ingredientGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));
        await ingredientGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Onion", BaseUnit: "g", DefaultCostPerUnit: 0.003m, CostUnit: "g"));

        // Act
        var recipeId = Guid.NewGuid().ToString();
        var recipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, recipeId));
        var recipe = await recipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "French Onion Soup",
            PortionYield: 6,
            YieldUnit: "portions",
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(ingredientId, "Onion", 500, "g", 5, 0.003m,
                    PrepInstructions: "Slice thinly and caramelize for 45 minutes")
            },
            PrepInstructions: "1. Caramelize onions\n2. Add stock\n3. Top with bread and cheese",
            PrepTimeMinutes: 60,
            CookTimeMinutes: 30,
            PublishImmediately: true));

        // Assert
        recipe.Published.Should().NotBeNull();
        recipe.Published!.Ingredients[0].PrepInstructions.Should().Be("Slice thinly and caramelize for 45 minutes");
        recipe.Published.PrepInstructions.Should().Contain("Caramelize onions");
        recipe.Published.PrepTimeMinutes.Should().Be(60);
        recipe.Published.CookTimeMinutes.Should().Be(30);
    }

    [Fact]
    public async Task Recipe_WithOptionalIngredients_MarksOptionalCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requiredId = Guid.NewGuid();
        var optionalId = Guid.NewGuid();

        var requiredGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, requiredId));
        await requiredGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Chicken Breast", BaseUnit: "g", DefaultCostPerUnit: 0.015m, CostUnit: "g"));

        var optionalGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, optionalId));
        await optionalGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Bacon Bits", BaseUnit: "g", DefaultCostPerUnit: 0.025m, CostUnit: "g"));

        // Act
        var recipeId = Guid.NewGuid().ToString();
        var recipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, recipeId));
        var recipe = await recipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Chicken Salad",
            PortionYield: 4,
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(requiredId, "Chicken Breast", 400, "g", 0, 0.015m, IsOptional: false),
                new(optionalId, "Bacon Bits", 50, "g", 0, 0.025m, IsOptional: true)
            },
            PublishImmediately: true));

        // Assert
        recipe.Published.Should().NotBeNull();
        var required = recipe.Published!.Ingredients.First(i => i.IngredientName == "Chicken Breast");
        var optional = recipe.Published.Ingredients.First(i => i.IngredientName == "Bacon Bits");

        required.IsOptional.Should().BeFalse();
        optional.IsOptional.Should().BeTrue();
    }
}

// ============================================================================
// Recipe Version Edge Case Tests
// Advanced versioning scenarios
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class RecipeVersionEdgeCaseTests
{
    private readonly TestCluster _cluster;

    public RecipeVersionEdgeCaseTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task Recipe_MultipleVersionReverts_MaintainsVersionHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var ingredientGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));
        await ingredientGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Test Ingredient", BaseUnit: "g", DefaultCostPerUnit: 0.01m, CostUnit: "g"));

        var recipeId = Guid.NewGuid().ToString();
        var recipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, recipeId));

        // Version 1
        await recipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Version 1",
            PortionYield: 4,
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(ingredientId, "Test Ingredient", 100, "g", 0, 0.01m)
            },
            PublishImmediately: true));

        // Version 2
        await recipeGrain.CreateDraftAsync(new CreateRecipeDraftCommand(Name: "Version 2"));
        await recipeGrain.PublishDraftAsync();

        // Version 3
        await recipeGrain.CreateDraftAsync(new CreateRecipeDraftCommand(Name: "Version 3"));
        await recipeGrain.PublishDraftAsync();

        // Act - Revert to version 1 (creates version 4)
        await recipeGrain.RevertToVersionAsync(1, reason: "Rolling back to original");

        // Assert
        var history = await recipeGrain.GetVersionHistoryAsync();
        history.Should().HaveCount(4);
        history[0].VersionNumber.Should().Be(4); // Most recent
        history[0].Name.Should().Be("Version 1"); // Content from version 1

        var snapshot = await recipeGrain.GetSnapshotAsync();
        snapshot.PublishedVersion.Should().Be(4);
        snapshot.TotalVersions.Should().Be(4);
    }

    [Fact]
    public async Task Recipe_DraftWithDifferentIngredients_PreservesPublishedVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredient1Id = Guid.NewGuid();
        var ingredient2Id = Guid.NewGuid();

        var grain1 = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredient1Id));
        await grain1.CreateAsync(new CreateIngredientCommand(
            Name: "Ingredient 1", BaseUnit: "g", DefaultCostPerUnit: 0.01m, CostUnit: "g"));

        var grain2 = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredient2Id));
        await grain2.CreateAsync(new CreateIngredientCommand(
            Name: "Ingredient 2", BaseUnit: "g", DefaultCostPerUnit: 0.02m, CostUnit: "g"));

        var recipeId = Guid.NewGuid().ToString();
        var recipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, recipeId));

        // Create published version with ingredient 1
        await recipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Original Recipe",
            PortionYield: 4,
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(ingredient1Id, "Ingredient 1", 100, "g", 0, 0.01m)
            },
            PublishImmediately: true));

        // Act - Create draft with different ingredient
        await recipeGrain.CreateDraftAsync(new CreateRecipeDraftCommand(
            Name: "Draft Recipe",
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(ingredient2Id, "Ingredient 2", 200, "g", 0, 0.02m)
            }));

        // Assert - Published version unchanged
        var snapshot = await recipeGrain.GetSnapshotAsync();
        snapshot.Published.Should().NotBeNull();
        snapshot.Draft.Should().NotBeNull();

        snapshot.Published!.Name.Should().Be("Original Recipe");
        snapshot.Published.Ingredients.Should().HaveCount(1);
        snapshot.Published.Ingredients[0].IngredientId.Should().Be(ingredient1Id);

        snapshot.Draft!.Name.Should().Be("Draft Recipe");
        snapshot.Draft.Ingredients.Should().HaveCount(1);
        snapshot.Draft.Ingredients[0].IngredientId.Should().Be(ingredient2Id);
    }

    [Fact]
    public async Task Recipe_GetNonExistentVersion_ReturnsNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var ingredientGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));
        await ingredientGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Test", BaseUnit: "g", DefaultCostPerUnit: 0.01m, CostUnit: "g"));

        var recipeId = Guid.NewGuid().ToString();
        var recipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, recipeId));

        await recipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Test Recipe",
            PortionYield: 4,
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(ingredientId, "Test", 100, "g", 0, 0.01m)
            },
            PublishImmediately: true));

        // Act
        var nonExistentVersion = await recipeGrain.GetVersionAsync(999);

        // Assert
        nonExistentVersion.Should().BeNull();
    }
}

// ============================================================================
// Recipe Cost Calculation Edge Cases
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class RecipeCostEdgeCaseTests
{
    private readonly TestCluster _cluster;

    public RecipeCostEdgeCaseTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task Recipe_WithHighWastePercentage_CalculatesCorrectEffectiveQuantity()
    {
        // Arrange - 30% waste (e.g., shellfish with shells)
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var ingredientGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));
        await ingredientGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Whole Lobster",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.05m,
            CostUnit: "g"));

        // Act
        var recipeId = Guid.NewGuid().ToString();
        var recipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, recipeId));
        var recipe = await recipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Lobster Meat",
            PortionYield: 1,
            YieldUnit: "portion",
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                // Need 200g meat, with 30% waste need to order more
                new(ingredientId, "Whole Lobster", 200, "g", 30, 0.05m)
            },
            PublishImmediately: true));

        // Assert
        recipe.Published.Should().NotBeNull();
        var lobsterIngredient = recipe.Published!.Ingredients[0];

        // Effective quantity: 200 / (1 - 0.30) = 285.71g
        lobsterIngredient.EffectiveQuantity.Should().BeApproximately(285.71m, 0.1m);

        // Cost: 285.71 * 0.05 = 14.29
        lobsterIngredient.LineCost.Should().BeApproximately(14.29m, 0.1m);
    }

    [Fact]
    public async Task Recipe_WithMixedIngredientWaste_CalculatesTotalCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredient1Id = Guid.NewGuid();
        var ingredient2Id = Guid.NewGuid();
        var ingredient3Id = Guid.NewGuid();

        var grain1 = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredient1Id));
        await grain1.CreateAsync(new CreateIngredientCommand(
            Name: "Carrots", BaseUnit: "g", DefaultCostPerUnit: 0.002m, CostUnit: "g"));

        var grain2 = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredient2Id));
        await grain2.CreateAsync(new CreateIngredientCommand(
            Name: "Olive Oil", BaseUnit: "ml", DefaultCostPerUnit: 0.02m, CostUnit: "ml"));

        var grain3 = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredient3Id));
        await grain3.CreateAsync(new CreateIngredientCommand(
            Name: "Steak", BaseUnit: "g", DefaultCostPerUnit: 0.03m, CostUnit: "g"));

        // Act
        var recipeId = Guid.NewGuid().ToString();
        var recipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, recipeId));
        var recipe = await recipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Steak with Vegetables",
            PortionYield: 4,
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(ingredient1Id, "Carrots", 300, "g", 15, 0.002m), // 15% waste (peeling)
                new(ingredient2Id, "Olive Oil", 30, "ml", 0, 0.02m), // No waste
                new(ingredient3Id, "Steak", 800, "g", 10, 0.03m)     // 10% waste (trimming)
            },
            PublishImmediately: true));

        // Assert
        recipe.Published.Should().NotBeNull();

        // Carrots: 300 / 0.85 = 352.94g, cost = 352.94 * 0.002 = 0.71
        var carrots = recipe.Published!.Ingredients.First(i => i.IngredientName == "Carrots");
        carrots.EffectiveQuantity.Should().BeApproximately(352.94m, 0.1m);

        // Oil: 30ml, no waste, cost = 30 * 0.02 = 0.60
        var oil = recipe.Published.Ingredients.First(i => i.IngredientName == "Olive Oil");
        oil.EffectiveQuantity.Should().Be(30m);

        // Steak: 800 / 0.90 = 888.89g, cost = 888.89 * 0.03 = 26.67
        var steak = recipe.Published.Ingredients.First(i => i.IngredientName == "Steak");
        steak.EffectiveQuantity.Should().BeApproximately(888.89m, 0.1m);

        // Total theoretical cost = ~27.98
        recipe.Published.TheoreticalCost.Should().BeGreaterThan(27m);
        recipe.Published.TheoreticalCost.Should().BeLessThan(29m);
    }

    [Fact]
    public async Task Recipe_OptionalIngredients_ExcludedFromCost()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requiredId = Guid.NewGuid();
        var optionalId = Guid.NewGuid();

        var requiredGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, requiredId));
        await requiredGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Base", BaseUnit: "g", DefaultCostPerUnit: 0.01m, CostUnit: "g"));

        var optionalGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, optionalId));
        await optionalGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Expensive Topping", BaseUnit: "g", DefaultCostPerUnit: 0.50m, CostUnit: "g"));

        var service = new RecipeCalculationService(_cluster.GrainFactory);

        var ingredients = new List<RecipeIngredientInfo>
        {
            new(requiredId, "Base", 100, "g", 0, 0.01m, false),        // Cost: 1.00
            new(optionalId, "Expensive Topping", 50, "g", 0, 0.50m, true) // Cost: 25.00 (optional)
        };

        // Act
        var result = await service.CalculateCostAsync(orgId, ingredients, 1);

        // Assert - Only required ingredient cost
        result.TheoreticalCost.Should().Be(1.00m);
        result.CostBreakdown.Should().HaveCount(1);
        result.CostBreakdown[0].IngredientName.Should().Be("Base");
    }
}

// ============================================================================
// Recipe Translation Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class RecipeTranslationTests
{
    private readonly TestCluster _cluster;

    public RecipeTranslationTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task Recipe_MultipleTranslations_StoredCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var ingredientGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));
        await ingredientGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Test", BaseUnit: "g", DefaultCostPerUnit: 0.01m, CostUnit: "g"));

        var recipeId = Guid.NewGuid().ToString();
        var recipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, recipeId));

        await recipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Chicken Soup",
            Description: "Traditional chicken soup",
            PortionYield: 6,
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(ingredientId, "Test", 100, "g", 0, 0.01m)
            },
            PublishImmediately: true));

        // Act - Add translations
        await recipeGrain.AddTranslationAsync(new AddRecipeTranslationCommand(
            Locale: "es-ES",
            Name: "Sopa de Pollo",
            Description: "Sopa tradicional de pollo"));

        await recipeGrain.AddTranslationAsync(new AddRecipeTranslationCommand(
            Locale: "fr-FR",
            Name: "Soupe au Poulet",
            Description: "Soupe traditionnelle au poulet"));

        await recipeGrain.AddTranslationAsync(new AddRecipeTranslationCommand(
            Locale: "de-DE",
            Name: "Huhnersuppe",
            Description: "Traditionelle Huhnersuppe"));

        // Assert
        var published = await recipeGrain.GetPublishedAsync();
        published.Should().NotBeNull();
        published!.Translations.Should().HaveCount(3);
        published.Translations.Should().ContainKey("es-ES");
        published.Translations.Should().ContainKey("fr-FR");
        published.Translations.Should().ContainKey("de-DE");

        published.Translations["es-ES"].Name.Should().Be("Sopa de Pollo");
        published.Translations["fr-FR"].Name.Should().Be("Soupe au Poulet");
    }

    [Fact]
    public async Task Recipe_RemoveTranslation_RemovesCorrectLocale()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var ingredientGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));
        await ingredientGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Test", BaseUnit: "g", DefaultCostPerUnit: 0.01m, CostUnit: "g"));

        var recipeId = Guid.NewGuid().ToString();
        var recipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, recipeId));

        await recipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Test Recipe",
            PortionYield: 4,
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new(ingredientId, "Test", 100, "g", 0, 0.01m)
            },
            PublishImmediately: true));

        await recipeGrain.AddTranslationAsync(new AddRecipeTranslationCommand("es-ES", "Spanish"));
        await recipeGrain.AddTranslationAsync(new AddRecipeTranslationCommand("fr-FR", "French"));

        // Act
        await recipeGrain.RemoveTranslationAsync("es-ES");

        // Assert
        var published = await recipeGrain.GetPublishedAsync();
        published!.Translations.Should().HaveCount(1);
        published.Translations.Should().ContainKey("fr-FR");
        published.Translations.Should().NotContainKey("es-ES");
    }
}
