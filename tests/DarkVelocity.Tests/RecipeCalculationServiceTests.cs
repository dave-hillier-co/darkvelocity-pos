using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.TestingHost;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for recipe scaling calculations.
/// </summary>
public class RecipeScalingTests
{
    [Fact]
    public void ScaleRecipe_DoublesIngredientQuantities()
    {
        // Arrange
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(Guid.NewGuid(), "Flour", 500m, "g", 0, 0.002m, false),
            new(Guid.NewGuid(), "Sugar", 100m, "g", 0, 0.003m, false),
            new(Guid.NewGuid(), "Eggs", 3m, "each", 0, 0.30m, false)
        };

        var grainFactory = new Mock<IGrainFactory>();
        var service = new RecipeCalculationService(grainFactory.Object);

        // Act
        var result = service.ScaleRecipe(ingredients, 12m, 24m);

        // Assert
        Assert.Equal(2m, result.ScaleFactor);
        Assert.Equal(24m, result.NewYield);

        var flour = result.ScaledIngredients.First(i => i.IngredientName == "Flour");
        Assert.Equal(1000m, flour.ScaledQuantity);

        var eggs = result.ScaledIngredients.First(i => i.IngredientName == "Eggs");
        Assert.Equal(6m, eggs.ScaledQuantity);
    }

    [Fact]
    public void ScaleRecipe_HalvesIngredientQuantities()
    {
        // Arrange
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(Guid.NewGuid(), "Water", 1000m, "ml", 0, 0.001m, false),
            new(Guid.NewGuid(), "Salt", 20m, "g", 0, 0.002m, false)
        };

        var grainFactory = new Mock<IGrainFactory>();
        var service = new RecipeCalculationService(grainFactory.Object);

        // Act
        var result = service.ScaleRecipe(ingredients, 4m, 2m);

        // Assert
        Assert.Equal(0.5m, result.ScaleFactor);

        var water = result.ScaledIngredients.First(i => i.IngredientName == "Water");
        Assert.Equal(500m, water.ScaledQuantity);

        var salt = result.ScaledIngredients.First(i => i.IngredientName == "Salt");
        Assert.Equal(10m, salt.ScaledQuantity);
    }

    [Fact]
    public void ScaleRecipe_WithPracticalRounding_RoundsToUsefulQuantities()
    {
        // Arrange
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(Guid.NewGuid(), "Eggs", 2m, "each", 0, 0.30m, false),
            new(Guid.NewGuid(), "Milk", 200m, "ml", 0, 0.003m, false),
            new(Guid.NewGuid(), "Flour", 150m, "g", 0, 0.002m, false)
        };

        var grainFactory = new Mock<IGrainFactory>();
        var service = new RecipeCalculationService(grainFactory.Object);

        // Act - scale by 1.5x
        var result = service.ScaleRecipe(ingredients, 6m, 9m, RoundingStrategy.Practical);

        // Assert
        var eggs = result.ScaledIngredients.First(i => i.IngredientName == "Eggs");
        Assert.Equal(3m, eggs.RoundedQuantity); // Eggs round up to whole numbers

        var milk = result.ScaledIngredients.First(i => i.IngredientName == "Milk");
        Assert.Equal(300m, milk.RoundedQuantity); // 300ml rounds to nearest 5

        var flour = result.ScaledIngredients.First(i => i.IngredientName == "Flour");
        Assert.Equal(225m, flour.RoundedQuantity); // 225g rounds to nearest 5
    }

    [Fact]
    public void ScaleRecipe_WithRoundUp_AlwaysRoundsUp()
    {
        // Arrange
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(Guid.NewGuid(), "Lemons", 2m, "each", 0, 0.50m, false)
        };

        var grainFactory = new Mock<IGrainFactory>();
        var service = new RecipeCalculationService(grainFactory.Object);

        // Act - scale by 1.5x
        var result = service.ScaleRecipe(ingredients, 4m, 6m, RoundingStrategy.RoundUp);

        // Assert
        var lemons = result.ScaledIngredients.First(i => i.IngredientName == "Lemons");
        Assert.Equal(3m, lemons.RoundedQuantity); // 3 rounded up
    }

    [Fact]
    public void ScaleRecipe_CalculatesScaledCosts()
    {
        // Arrange
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(Guid.NewGuid(), "Chicken Breast", 500m, "g", 5m, 0.012m, false), // 5% waste
            new(Guid.NewGuid(), "Olive Oil", 30m, "ml", 0, 0.02m, false)
        };

        var grainFactory = new Mock<IGrainFactory>();
        var service = new RecipeCalculationService(grainFactory.Object);

        // Act - double the recipe
        var result = service.ScaleRecipe(ingredients, 4m, 8m);

        // Assert
        var chicken = result.ScaledIngredients.First(i => i.IngredientName == "Chicken Breast");
        Assert.Equal(1000m, chicken.ScaledQuantity); // 500 * 2 = 1000g
        // With 5% waste: effective = 1000 / (1 - 0.05) = 1052.63g
        // Cost = 1052.63 * 0.012 = 12.63
        Assert.True(chicken.ScaledCost > 12m && chicken.ScaledCost < 13m);
    }

    [Fact]
    public void ScaleRecipe_ThrowsOnZeroCurrentYield()
    {
        // Arrange
        var ingredients = new List<RecipeIngredientInfo>();
        var grainFactory = new Mock<IGrainFactory>();
        var service = new RecipeCalculationService(grainFactory.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            service.ScaleRecipe(ingredients, 0m, 10m));
    }

    [Fact]
    public void ScaleRecipe_ThrowsOnZeroTargetYield()
    {
        // Arrange
        var ingredients = new List<RecipeIngredientInfo>();
        var grainFactory = new Mock<IGrainFactory>();
        var service = new RecipeCalculationService(grainFactory.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            service.ScaleRecipe(ingredients, 10m, 0m));
    }
}

/// <summary>
/// Tests for allergen inheritance calculations.
/// </summary>
[Collection(ClusterCollection.Name)]
public class AllergenInheritanceTests
{
    private readonly TestCluster _cluster;

    public AllergenInheritanceTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task CalculateAllergens_InheritsFromIngredients()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var flourId = Guid.NewGuid();
        var eggId = Guid.NewGuid();
        var milkId = Guid.NewGuid();

        // Create ingredients with allergens
        var flourGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, flourId));
        await flourGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Flour",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.002m,
            CostUnit: "g",
            Allergens: [new AllergenDeclarationCommand(StandardAllergens.Gluten)]));

        var eggGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, eggId));
        await eggGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Eggs",
            BaseUnit: "each",
            DefaultCostPerUnit: 0.30m,
            CostUnit: "each",
            Allergens: [new AllergenDeclarationCommand(StandardAllergens.Eggs)]));

        var milkGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, milkId));
        await milkGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Milk",
            BaseUnit: "ml",
            DefaultCostPerUnit: 0.002m,
            CostUnit: "ml",
            Allergens: [
                new AllergenDeclarationCommand(StandardAllergens.Dairy),
                new AllergenDeclarationCommand(StandardAllergens.Soy, AllergenDeclarationType.MayContain)
            ]));

        var ingredients = new List<RecipeIngredientInfo>
        {
            new(flourId, "Flour", 500m, "g", 0, 0.002m, false),
            new(eggId, "Eggs", 2m, "each", 0, 0.30m, false),
            new(milkId, "Milk", 250m, "ml", 0, 0.002m, false)
        };

        var service = new RecipeCalculationService(_cluster.GrainFactory);

        // Act
        var result = await service.CalculateAllergensAsync(orgId, ingredients);

        // Assert
        Assert.Contains(StandardAllergens.Gluten, result.ContainsAllergens);
        Assert.Contains(StandardAllergens.Eggs, result.ContainsAllergens);
        Assert.Contains(StandardAllergens.Dairy, result.ContainsAllergens);
        Assert.Contains(StandardAllergens.Soy, result.MayContainAllergens);
        Assert.DoesNotContain(StandardAllergens.Soy, result.ContainsAllergens); // Should be in MayContain only
    }

    [Fact]
    public async Task CalculateAllergens_ExcludesOptionalIngredients()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var flourId = Guid.NewGuid();
        var nutsId = Guid.NewGuid();

        var flourGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, flourId));
        await flourGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Flour",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.002m,
            CostUnit: "g",
            Allergens: [new AllergenDeclarationCommand(StandardAllergens.Gluten)]));

        var nutsGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, nutsId));
        await nutsGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Walnuts",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.02m,
            CostUnit: "g",
            Allergens: [new AllergenDeclarationCommand(StandardAllergens.TreeNuts)]));

        var ingredients = new List<RecipeIngredientInfo>
        {
            new(flourId, "Flour", 500m, "g", 0, 0.002m, false),
            new(nutsId, "Walnuts", 50m, "g", 0, 0.02m, true) // Optional
        };

        var service = new RecipeCalculationService(_cluster.GrainFactory);

        // Act
        var result = await service.CalculateAllergensAsync(orgId, ingredients);

        // Assert
        Assert.Contains(StandardAllergens.Gluten, result.ContainsAllergens);
        Assert.DoesNotContain(StandardAllergens.TreeNuts, result.ContainsAllergens);
    }

    [Fact]
    public async Task CalculateAllergens_UpgradesMayContainToContains()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredient1Id = Guid.NewGuid();
        var ingredient2Id = Guid.NewGuid();

        // First ingredient has "may contain" gluten
        var grain1 = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredient1Id));
        await grain1.CreateAsync(new CreateIngredientCommand(
            Name: "Oats (may contain gluten)",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.005m,
            CostUnit: "g",
            Allergens: [new AllergenDeclarationCommand(StandardAllergens.Gluten, AllergenDeclarationType.MayContain)]));

        // Second ingredient definitively contains gluten
        var grain2 = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredient2Id));
        await grain2.CreateAsync(new CreateIngredientCommand(
            Name: "Wheat Flour",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.002m,
            CostUnit: "g",
            Allergens: [new AllergenDeclarationCommand(StandardAllergens.Gluten, AllergenDeclarationType.Contains)]));

        var ingredients = new List<RecipeIngredientInfo>
        {
            new(ingredient1Id, "Oats", 200m, "g", 0, 0.005m, false),
            new(ingredient2Id, "Wheat Flour", 300m, "g", 0, 0.002m, false)
        };

        var service = new RecipeCalculationService(_cluster.GrainFactory);

        // Act
        var result = await service.CalculateAllergensAsync(orgId, ingredients);

        // Assert
        Assert.Contains(StandardAllergens.Gluten, result.ContainsAllergens);
        Assert.DoesNotContain(StandardAllergens.Gluten, result.MayContainAllergens); // Should be upgraded to Contains
    }
}

/// <summary>
/// Tests for nutritional calculation.
/// </summary>
[Collection(ClusterCollection.Name)]
public class NutritionalCalculationTests
{
    private readonly TestCluster _cluster;

    public NutritionalCalculationTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task CalculateNutrition_SumsIngredientNutrition()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var flourId = Guid.NewGuid();
        var sugarId = Guid.NewGuid();

        var flourGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, flourId));
        await flourGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Flour",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.002m,
            CostUnit: "g",
            Nutrition: new IngredientNutritionCommand(
                CaloriesPer100g: 364m,
                ProteinPer100g: 10m,
                CarbohydratesPer100g: 76m,
                FatPer100g: 1m)));

        var sugarGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, sugarId));
        await sugarGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Sugar",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.001m,
            CostUnit: "g",
            Nutrition: new IngredientNutritionCommand(
                CaloriesPer100g: 387m,
                ProteinPer100g: 0m,
                CarbohydratesPer100g: 100m,
                FatPer100g: 0m)));

        // Recipe: 200g flour + 100g sugar = 4 servings
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(flourId, "Flour", 200m, "g", 0, 0.002m, false),
            new(sugarId, "Sugar", 100m, "g", 0, 0.001m, false)
        };

        var service = new RecipeCalculationService(_cluster.GrainFactory);

        // Act
        var result = await service.CalculateNutritionAsync(orgId, ingredients, 4m);

        // Assert
        // Total calories: (200/100 * 364) + (100/100 * 387) = 728 + 387 = 1115
        // Per serving (4): 278.75 calories
        Assert.NotNull(result.CaloriesPerServing);
        Assert.True(result.CaloriesPerServing > 275m && result.CaloriesPerServing < 280m);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task CalculateNutrition_ReportsIncompleteWhenMissingData()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        // Create ingredient without nutrition data
        var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));
        await grain.CreateAsync(new CreateIngredientCommand(
            Name: "Mystery Ingredient",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.01m,
            CostUnit: "g")); // No nutrition

        var ingredients = new List<RecipeIngredientInfo>
        {
            new(ingredientId, "Mystery Ingredient", 100m, "g", 0, 0.01m, false)
        };

        var service = new RecipeCalculationService(_cluster.GrainFactory);

        // Act
        var result = await service.CalculateNutritionAsync(orgId, ingredients, 1m);

        // Assert
        Assert.False(result.IsComplete);
        Assert.Contains(ingredientId, result.MissingNutritionIngredients);
    }

    [Fact]
    public async Task CalculateNutrition_ExcludesOptionalIngredients()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var baseIngredientId = Guid.NewGuid();
        var optionalId = Guid.NewGuid();

        var baseGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, baseIngredientId));
        await baseGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Base",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.01m,
            CostUnit: "g",
            Nutrition: new IngredientNutritionCommand(CaloriesPer100g: 100m)));

        var optionalGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, optionalId));
        await optionalGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Optional Topping",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.02m,
            CostUnit: "g",
            Nutrition: new IngredientNutritionCommand(CaloriesPer100g: 500m)));

        var ingredients = new List<RecipeIngredientInfo>
        {
            new(baseIngredientId, "Base", 100m, "g", 0, 0.01m, false),
            new(optionalId, "Optional Topping", 50m, "g", 0, 0.02m, true) // Optional
        };

        var service = new RecipeCalculationService(_cluster.GrainFactory);

        // Act
        var result = await service.CalculateNutritionAsync(orgId, ingredients, 1m);

        // Assert
        // Only base ingredient: 100g at 100 cal/100g = 100 cal
        Assert.Equal(100m, result.CaloriesPerServing);
    }
}

/// <summary>
/// Tests for recipe validation.
/// </summary>
[Collection(ClusterCollection.Name)]
public class RecipeValidationTests
{
    private readonly TestCluster _cluster;

    public RecipeValidationTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task ValidateRecipe_PassesValidRecipe()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));
        await grain.CreateAsync(new CreateIngredientCommand(
            Name: "Valid Ingredient",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.01m,
            CostUnit: "g"));

        var service = new RecipeCalculationService(_cluster.GrainFactory);

        var recipeInput = new RecipeValidationInput(
            Name: "Test Recipe",
            PortionYield: 4m,
            YieldUnit: "portions",
            Ingredients: [new RecipeIngredientInfo(ingredientId, "Valid Ingredient", 100m, "g", 0, 0.01m, false)],
            AllergenTags: []);

        // Act
        var result = await service.ValidateRecipeAsync(orgId, recipeInput);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateRecipe_FailsOnMissingName()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var service = new RecipeCalculationService(_cluster.GrainFactory);

        var recipeInput = new RecipeValidationInput(
            Name: "",
            PortionYield: 4m,
            YieldUnit: "portions",
            Ingredients: null,
            AllergenTags: null);

        // Act
        var result = await service.ValidateRecipeAsync(orgId, recipeInput);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "RECIPE_NAME_REQUIRED");
    }

    [Fact]
    public async Task ValidateRecipe_FailsOnZeroYield()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var service = new RecipeCalculationService(_cluster.GrainFactory);

        var recipeInput = new RecipeValidationInput(
            Name: "Test Recipe",
            PortionYield: 0m,
            YieldUnit: "portions",
            Ingredients: null,
            AllergenTags: null);

        // Act
        var result = await service.ValidateRecipeAsync(orgId, recipeInput);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "RECIPE_YIELD_REQUIRED");
    }

    [Fact]
    public async Task ValidateRecipe_FailsOnNoIngredients()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var service = new RecipeCalculationService(_cluster.GrainFactory);

        var recipeInput = new RecipeValidationInput(
            Name: "Test Recipe",
            PortionYield: 4m,
            YieldUnit: "portions",
            Ingredients: [],
            AllergenTags: null);

        // Act
        var result = await service.ValidateRecipeAsync(orgId, recipeInput);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "RECIPE_INGREDIENTS_REQUIRED");
    }

    [Fact]
    public async Task ValidateRecipe_FailsOnNonExistentIngredient()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var nonExistentId = Guid.NewGuid();
        var service = new RecipeCalculationService(_cluster.GrainFactory);

        var recipeInput = new RecipeValidationInput(
            Name: "Test Recipe",
            PortionYield: 4m,
            YieldUnit: "portions",
            Ingredients: [new RecipeIngredientInfo(nonExistentId, "Non-existent", 100m, "g", 0, 0.01m, false)],
            AllergenTags: null);

        // Act
        var result = await service.ValidateRecipeAsync(orgId, recipeInput);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "RECIPE_INGREDIENTS_NOT_FOUND");
    }

    [Fact]
    public async Task ValidateRecipe_WarnsOnZeroCostIngredients()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));
        await grain.CreateAsync(new CreateIngredientCommand(
            Name: "Free Ingredient",
            BaseUnit: "g",
            DefaultCostPerUnit: 0m,
            CostUnit: "g"));

        var service = new RecipeCalculationService(_cluster.GrainFactory);

        var recipeInput = new RecipeValidationInput(
            Name: "Test Recipe",
            PortionYield: 4m,
            YieldUnit: "portions",
            Ingredients: [new RecipeIngredientInfo(ingredientId, "Free Ingredient", 100m, "g", 0, 0m, false)],
            AllergenTags: []);

        // Act
        var result = await service.ValidateRecipeAsync(orgId, recipeInput);

        // Assert
        Assert.True(result.IsValid); // Valid but with warnings
        Assert.Contains(result.Warnings, w => w.Code == "RECIPE_INGREDIENT_ZERO_COST");
    }

    [Fact]
    public async Task ValidateRecipe_WarnsOnMissingYieldUnit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));
        await grain.CreateAsync(new CreateIngredientCommand(
            Name: "Ingredient",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.01m,
            CostUnit: "g"));

        var service = new RecipeCalculationService(_cluster.GrainFactory);

        var recipeInput = new RecipeValidationInput(
            Name: "Test Recipe",
            PortionYield: 4m,
            YieldUnit: null, // Missing yield unit
            Ingredients: [new RecipeIngredientInfo(ingredientId, "Ingredient", 100m, "g", 0, 0.01m, false)],
            AllergenTags: []);

        // Act
        var result = await service.ValidateRecipeAsync(orgId, recipeInput);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "RECIPE_YIELD_UNIT_MISSING");
    }
}

/// <summary>
/// Tests for sub-recipe costing.
/// </summary>
[Collection(ClusterCollection.Name)]
public class SubRecipeCostingTests
{
    private readonly TestCluster _cluster;

    public SubRecipeCostingTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task CalculateCost_IncludesSubRecipeCosts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sauceIngredientId = Guid.NewGuid();
        var pastaId = Guid.NewGuid();

        // Create a sub-recipe output ingredient (house sauce)
        var sauceGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, sauceIngredientId));
        await sauceGrain.CreateAsync(new CreateIngredientCommand(
            Name: "House Marinara",
            BaseUnit: "ml",
            DefaultCostPerUnit: 0.008m, // Will be overridden by sub-recipe cost
            CostUnit: "ml"));

        // Mark it as a sub-recipe output
        await sauceGrain.LinkToSubRecipeAsync("marinara-recipe");

        // Create a regular ingredient
        var pastaGrain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, pastaId));
        await pastaGrain.CreateAsync(new CreateIngredientCommand(
            Name: "Spaghetti",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.003m,
            CostUnit: "g"));

        // Create the sub-recipe that produces the sauce
        var subRecipeGrain = _cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, "marinara-recipe"));
        await subRecipeGrain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "House Marinara Sauce",
            PortionYield: 1000m, // Makes 1000ml
            YieldUnit: "ml",
            PublishImmediately: true));

        var ingredients = new List<RecipeIngredientInfo>
        {
            new(pastaId, "Spaghetti", 200m, "g", 0, 0.003m, false),
            new(sauceIngredientId, "House Marinara", 150m, "ml", 0, 0.008m, false, true) // Sub-recipe output
        };

        var service = new RecipeCalculationService(_cluster.GrainFactory);

        // Act
        var result = await service.CalculateCostAsync(orgId, ingredients, 1m);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TheoreticalCost > 0);
        Assert.Equal(2, result.CostBreakdown.Count);
    }

    [Fact]
    public async Task CalculateCost_CalculatesCostPercentages()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredient1Id = Guid.NewGuid();
        var ingredient2Id = Guid.NewGuid();

        var grain1 = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredient1Id));
        await grain1.CreateAsync(new CreateIngredientCommand(
            Name: "Expensive Item",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.10m,
            CostUnit: "g"));

        var grain2 = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredient2Id));
        await grain2.CreateAsync(new CreateIngredientCommand(
            Name: "Cheap Item",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.01m,
            CostUnit: "g"));

        var ingredients = new List<RecipeIngredientInfo>
        {
            new(ingredient1Id, "Expensive Item", 100m, "g", 0, 0.10m, false), // Cost: 10.00
            new(ingredient2Id, "Cheap Item", 100m, "g", 0, 0.01m, false)      // Cost: 1.00
        };

        var service = new RecipeCalculationService(_cluster.GrainFactory);

        // Act
        var result = await service.CalculateCostAsync(orgId, ingredients, 1m);

        // Assert
        // Total cost: 11.00
        // Expensive: 10.00 / 11.00 = 90.91%
        // Cheap: 1.00 / 11.00 = 9.09%
        var expensive = result.CostBreakdown.First(c => c.IngredientName == "Expensive Item");
        Assert.True(expensive.CostPercentage > 90m && expensive.CostPercentage < 92m);

        var cheap = result.CostBreakdown.First(c => c.IngredientName == "Cheap Item");
        Assert.True(cheap.CostPercentage > 8m && cheap.CostPercentage < 10m);
    }

    [Fact]
    public async Task CalculateCost_AppliesWastePercentage()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<IIngredientGrain>(
            GrainKeys.Ingredient(orgId, ingredientId));
        await grain.CreateAsync(new CreateIngredientCommand(
            Name: "Trimmed Meat",
            BaseUnit: "g",
            DefaultCostPerUnit: 0.02m,
            CostUnit: "g"));

        // 500g needed, 10% waste means we need ~555g effective
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(ingredientId, "Trimmed Meat", 500m, "g", 10m, 0.02m, false)
        };

        var service = new RecipeCalculationService(_cluster.GrainFactory);

        // Act
        var result = await service.CalculateCostAsync(orgId, ingredients, 1m);

        // Assert
        var meat = result.CostBreakdown.First();
        // Effective quantity: 500 / (1 - 0.10) = 555.56g
        Assert.True(meat.EffectiveQuantity > 555m && meat.EffectiveQuantity < 556m);
        // Cost: 555.56 * 0.02 = 11.11
        Assert.True(meat.LineCost > 11m && meat.LineCost < 11.2m);
    }
}

/// <summary>
/// Simple mock for IGrainFactory when testing without Orleans cluster.
/// </summary>
public class Mock<T> where T : class
{
    public T Object { get; }

    public Mock()
    {
        // For tests that don't need real grain factory
        Object = default!;
    }
}
