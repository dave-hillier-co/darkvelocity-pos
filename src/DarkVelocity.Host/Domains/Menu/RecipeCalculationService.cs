using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Recipe Calculation and Validation Service
// ============================================================================

/// <summary>
/// Service for recipe calculations including allergen inheritance, scaling,
/// nutritional calculation, and validation.
/// </summary>
public interface IRecipeCalculationService
{
    /// <summary>
    /// Calculates the aggregated allergens from all ingredients (including sub-recipes).
    /// </summary>
    Task<RecipeAllergenResult> CalculateAllergensAsync(
        Guid orgId,
        IReadOnlyList<RecipeIngredientInfo> ingredients);

    /// <summary>
    /// Scales a recipe by a given factor.
    /// </summary>
    ScaledRecipeResult ScaleRecipe(
        IReadOnlyList<RecipeIngredientInfo> ingredients,
        decimal currentYield,
        decimal targetYield,
        RoundingStrategy rounding = RoundingStrategy.None);

    /// <summary>
    /// Calculates nutritional information from ingredients.
    /// </summary>
    Task<RecipeNutritionResult> CalculateNutritionAsync(
        Guid orgId,
        IReadOnlyList<RecipeIngredientInfo> ingredients,
        decimal portionYield);

    /// <summary>
    /// Validates a recipe for completeness.
    /// </summary>
    Task<RecipeValidationResult> ValidateRecipeAsync(
        Guid orgId,
        RecipeValidationInput recipe);

    /// <summary>
    /// Calculates the full cost including sub-recipe costs.
    /// </summary>
    Task<RecipeCostResult> CalculateCostAsync(
        Guid orgId,
        IReadOnlyList<RecipeIngredientInfo> ingredients,
        decimal portionYield);
}

/// <summary>
/// Input for ingredient in calculations.
/// </summary>
public record RecipeIngredientInfo(
    Guid IngredientId,
    string IngredientName,
    decimal Quantity,
    string Unit,
    decimal WastePercentage,
    decimal UnitCost,
    bool IsOptional,
    bool IsSubRecipeOutput = false);

/// <summary>
/// Input for recipe validation.
/// </summary>
public record RecipeValidationInput(
    string Name,
    decimal PortionYield,
    string? YieldUnit,
    IReadOnlyList<RecipeIngredientInfo>? Ingredients,
    IReadOnlyList<string>? AllergenTags);

/// <summary>
/// Result of allergen calculation.
/// </summary>
public record RecipeAllergenResult(
    IReadOnlyList<AllergenDeclarationSnapshot> Allergens,
    IReadOnlyList<string> ContainsAllergens,
    IReadOnlyList<string> MayContainAllergens,
    IReadOnlyDictionary<Guid, IReadOnlyList<string>> AllergensByIngredient);

/// <summary>
/// Scaling rounding strategy.
/// </summary>
public enum RoundingStrategy
{
    /// <summary>No rounding applied.</summary>
    None,
    /// <summary>Round to nearest practical quantity (0.25 for most, 1 for whole items).</summary>
    Practical,
    /// <summary>Always round up for ordering purposes.</summary>
    RoundUp,
    /// <summary>Always round to whole numbers.</summary>
    WholeNumbers
}

/// <summary>
/// Result of recipe scaling.
/// </summary>
public record ScaledRecipeResult(
    decimal ScaleFactor,
    decimal NewYield,
    IReadOnlyList<ScaledIngredient> ScaledIngredients);

/// <summary>
/// A scaled ingredient.
/// </summary>
public record ScaledIngredient(
    Guid IngredientId,
    string IngredientName,
    decimal OriginalQuantity,
    decimal ScaledQuantity,
    decimal RoundedQuantity,
    string Unit,
    decimal ScaledCost);

/// <summary>
/// Result of nutritional calculation.
/// </summary>
public record RecipeNutritionResult(
    decimal? CaloriesPerServing,
    decimal? ProteinPerServing,
    decimal? CarbohydratesPerServing,
    decimal? FatPerServing,
    decimal? SaturatedFatPerServing,
    decimal? FiberPerServing,
    decimal? SugarPerServing,
    decimal? SodiumPerServing,
    bool IsComplete,
    IReadOnlyList<Guid> MissingNutritionIngredients);

/// <summary>
/// Result of recipe cost calculation.
/// </summary>
public record RecipeCostResult(
    decimal TheoreticalCost,
    decimal CostPerPortion,
    IReadOnlyList<IngredientCostBreakdown> CostBreakdown,
    IReadOnlyList<SubRecipeCostInfo> SubRecipeCosts);

/// <summary>
/// Cost breakdown for an ingredient.
/// </summary>
public record IngredientCostBreakdown(
    Guid IngredientId,
    string IngredientName,
    decimal Quantity,
    decimal EffectiveQuantity,
    decimal UnitCost,
    decimal LineCost,
    decimal CostPercentage);

/// <summary>
/// Sub-recipe cost information.
/// </summary>
public record SubRecipeCostInfo(
    Guid IngredientId,
    string RecipeDocumentId,
    decimal CostPerUnit,
    decimal QuantityUsed,
    decimal TotalCost);

/// <summary>
/// Result of recipe validation.
/// </summary>
public record RecipeValidationResult(
    bool IsValid,
    IReadOnlyList<RecipeValidationError> Errors,
    IReadOnlyList<RecipeValidationWarning> Warnings);

/// <summary>
/// Validation error.
/// </summary>
public record RecipeValidationError(
    string Code,
    string Message,
    string? Field);

/// <summary>
/// Validation warning.
/// </summary>
public record RecipeValidationWarning(
    string Code,
    string Message,
    string? Field);

/// <summary>
/// Implementation of recipe calculation service.
/// </summary>
public class RecipeCalculationService : IRecipeCalculationService
{
    private readonly IGrainFactory _grainFactory;

    public RecipeCalculationService(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async Task<RecipeAllergenResult> CalculateAllergensAsync(
        Guid orgId,
        IReadOnlyList<RecipeIngredientInfo> ingredients)
    {
        var allergensByIngredient = new Dictionary<Guid, IReadOnlyList<string>>();
        var containsAllergens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mayContainAllergens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allAllergens = new List<AllergenDeclarationSnapshot>();

        foreach (var ingredient in ingredients.Where(i => !i.IsOptional))
        {
            var ingredientGrain = _grainFactory.GetGrain<IIngredientGrain>(
                GrainKeys.Ingredient(orgId, ingredient.IngredientId));

            if (!await ingredientGrain.ExistsAsync())
                continue;

            var ingredientAllergens = await ingredientGrain.GetAllergensAsync();
            var ingredientAllergenTags = new List<string>();

            foreach (var allergen in ingredientAllergens)
            {
                ingredientAllergenTags.Add(allergen.Allergen);

                // Check if we already have this allergen with a different declaration type
                var existingAllergen = allAllergens.FirstOrDefault(a =>
                    a.Allergen.Equals(allergen.Allergen, StringComparison.OrdinalIgnoreCase));

                if (existingAllergen == null)
                {
                    allAllergens.Add(allergen);

                    if (allergen.DeclarationType == AllergenDeclarationType.Contains)
                        containsAllergens.Add(allergen.Allergen);
                    else
                        mayContainAllergens.Add(allergen.Allergen);
                }
                else if (allergen.DeclarationType == AllergenDeclarationType.Contains &&
                         existingAllergen.DeclarationType == AllergenDeclarationType.MayContain)
                {
                    // Upgrade from "may contain" to "contains"
                    allAllergens.Remove(existingAllergen);
                    allAllergens.Add(allergen);
                    mayContainAllergens.Remove(allergen.Allergen);
                    containsAllergens.Add(allergen.Allergen);
                }
            }

            if (ingredientAllergenTags.Count > 0)
                allergensByIngredient[ingredient.IngredientId] = ingredientAllergenTags;

            // If this ingredient is produced by a sub-recipe, get allergens from that too
            var snapshot = await ingredientGrain.GetSnapshotAsync();
            if (snapshot.IsSubRecipeOutput && !string.IsNullOrEmpty(snapshot.ProducedByRecipeId))
            {
                var subRecipeGrain = _grainFactory.GetGrain<IRecipeDocumentGrain>(
                    GrainKeys.RecipeDocument(orgId, snapshot.ProducedByRecipeId));

                if (await subRecipeGrain.ExistsAsync())
                {
                    var subRecipeSnapshot = await subRecipeGrain.GetSnapshotAsync();
                    var published = subRecipeSnapshot.Published;
                    if (published != null)
                    {
                        foreach (var allergenTag in published.AllergenTags)
                        {
                            if (!containsAllergens.Contains(allergenTag) &&
                                !mayContainAllergens.Contains(allergenTag))
                            {
                                containsAllergens.Add(allergenTag);
                                allAllergens.Add(new AllergenDeclarationSnapshot(
                                    allergenTag,
                                    AllergenDeclarationType.Contains,
                                    $"From sub-recipe: {published.Name}"));
                            }
                        }
                    }
                }
            }
        }

        // Remove any "may contain" that are already in "contains"
        foreach (var contains in containsAllergens)
            mayContainAllergens.Remove(contains);

        return new RecipeAllergenResult(
            Allergens: allAllergens,
            ContainsAllergens: containsAllergens.ToList(),
            MayContainAllergens: mayContainAllergens.ToList(),
            AllergensByIngredient: allergensByIngredient);
    }

    public ScaledRecipeResult ScaleRecipe(
        IReadOnlyList<RecipeIngredientInfo> ingredients,
        decimal currentYield,
        decimal targetYield,
        RoundingStrategy rounding = RoundingStrategy.None)
    {
        if (currentYield <= 0)
            throw new ArgumentException("Current yield must be positive", nameof(currentYield));

        if (targetYield <= 0)
            throw new ArgumentException("Target yield must be positive", nameof(targetYield));

        var scaleFactor = targetYield / currentYield;
        var scaledIngredients = new List<ScaledIngredient>();

        foreach (var ingredient in ingredients)
        {
            var scaledQuantity = ingredient.Quantity * scaleFactor;
            var roundedQuantity = ApplyRounding(scaledQuantity, ingredient.Unit, rounding);
            var scaledCost = roundedQuantity * ingredient.UnitCost;

            // Apply waste percentage
            if (ingredient.WastePercentage > 0)
            {
                var effectiveRounded = roundedQuantity / (1 - ingredient.WastePercentage / 100);
                scaledCost = effectiveRounded * ingredient.UnitCost;
            }

            scaledIngredients.Add(new ScaledIngredient(
                IngredientId: ingredient.IngredientId,
                IngredientName: ingredient.IngredientName,
                OriginalQuantity: ingredient.Quantity,
                ScaledQuantity: scaledQuantity,
                RoundedQuantity: roundedQuantity,
                Unit: ingredient.Unit,
                ScaledCost: scaledCost));
        }

        return new ScaledRecipeResult(
            ScaleFactor: scaleFactor,
            NewYield: targetYield,
            ScaledIngredients: scaledIngredients);
    }

    private static decimal ApplyRounding(decimal value, string unit, RoundingStrategy strategy)
    {
        return strategy switch
        {
            RoundingStrategy.None => value,
            RoundingStrategy.Practical => ApplyPracticalRounding(value, unit),
            RoundingStrategy.RoundUp => Math.Ceiling(value),
            RoundingStrategy.WholeNumbers => Math.Round(value, 0),
            _ => value
        };
    }

    private static decimal ApplyPracticalRounding(decimal value, string unit)
    {
        // For "each" units, round to whole numbers
        if (unit.Equals("each", StringComparison.OrdinalIgnoreCase) ||
            unit.Equals("piece", StringComparison.OrdinalIgnoreCase) ||
            unit.Equals("pcs", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Ceiling(value);
        }

        // For liquids, round to nearest 5ml or 0.25 oz
        if (unit.Equals("ml", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Round(value / 5) * 5;
        }

        if (unit.Equals("fl oz", StringComparison.OrdinalIgnoreCase) ||
            unit.Equals("floz", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Round(value * 4) / 4; // Round to nearest 0.25
        }

        // For weight in grams, round to nearest 5g
        if (unit.Equals("g", StringComparison.OrdinalIgnoreCase))
        {
            return value < 10 ? Math.Round(value, 1) : Math.Round(value / 5) * 5;
        }

        // For larger weights, round to 2 decimal places
        if (unit.Equals("kg", StringComparison.OrdinalIgnoreCase) ||
            unit.Equals("lb", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Round(value, 2);
        }

        // Default: round to 2 decimal places
        return Math.Round(value, 2);
    }

    public async Task<RecipeNutritionResult> CalculateNutritionAsync(
        Guid orgId,
        IReadOnlyList<RecipeIngredientInfo> ingredients,
        decimal portionYield)
    {
        if (portionYield <= 0)
            portionYield = 1;

        decimal? totalCalories = 0;
        decimal? totalProtein = 0;
        decimal? totalCarbs = 0;
        decimal? totalFat = 0;
        decimal? totalSaturatedFat = 0;
        decimal? totalFiber = 0;
        decimal? totalSugar = 0;
        decimal? totalSodium = 0;
        var missingNutrition = new List<Guid>();
        var hasAnyNutrition = false;

        foreach (var ingredient in ingredients.Where(i => !i.IsOptional))
        {
            var ingredientGrain = _grainFactory.GetGrain<IIngredientGrain>(
                GrainKeys.Ingredient(orgId, ingredient.IngredientId));

            if (!await ingredientGrain.ExistsAsync())
            {
                missingNutrition.Add(ingredient.IngredientId);
                continue;
            }

            var nutrition = await ingredientGrain.GetNutritionAsync();
            if (nutrition == null)
            {
                missingNutrition.Add(ingredient.IngredientId);
                continue;
            }

            hasAnyNutrition = true;

            // Calculate effective quantity after waste
            var effectiveQty = ingredient.WastePercentage > 0
                ? ingredient.Quantity / (1 - ingredient.WastePercentage / 100)
                : ingredient.Quantity;

            // Convert quantity to 100g/100ml equivalent
            var quantityFactor = effectiveQty / 100m;

            // Add nutritional values
            if (nutrition.CaloriesPer100g.HasValue)
                totalCalories += nutrition.CaloriesPer100g.Value * quantityFactor;
            else
                totalCalories = null;

            if (nutrition.ProteinPer100g.HasValue && totalProtein.HasValue)
                totalProtein += nutrition.ProteinPer100g.Value * quantityFactor;
            else
                totalProtein = null;

            if (nutrition.CarbohydratesPer100g.HasValue && totalCarbs.HasValue)
                totalCarbs += nutrition.CarbohydratesPer100g.Value * quantityFactor;
            else
                totalCarbs = null;

            if (nutrition.FatPer100g.HasValue && totalFat.HasValue)
                totalFat += nutrition.FatPer100g.Value * quantityFactor;
            else
                totalFat = null;

            if (nutrition.SaturatedFatPer100g.HasValue && totalSaturatedFat.HasValue)
                totalSaturatedFat += nutrition.SaturatedFatPer100g.Value * quantityFactor;
            else
                totalSaturatedFat = null;

            if (nutrition.FiberPer100g.HasValue && totalFiber.HasValue)
                totalFiber += nutrition.FiberPer100g.Value * quantityFactor;
            else
                totalFiber = null;

            if (nutrition.SugarPer100g.HasValue && totalSugar.HasValue)
                totalSugar += nutrition.SugarPer100g.Value * quantityFactor;
            else
                totalSugar = null;

            if (nutrition.SodiumPer100g.HasValue && totalSodium.HasValue)
                totalSodium += nutrition.SodiumPer100g.Value * quantityFactor;
            else
                totalSodium = null;
        }

        // Calculate per-serving values
        return new RecipeNutritionResult(
            CaloriesPerServing: totalCalories.HasValue ? Math.Round(totalCalories.Value / portionYield, 1) : null,
            ProteinPerServing: totalProtein.HasValue ? Math.Round(totalProtein.Value / portionYield, 1) : null,
            CarbohydratesPerServing: totalCarbs.HasValue ? Math.Round(totalCarbs.Value / portionYield, 1) : null,
            FatPerServing: totalFat.HasValue ? Math.Round(totalFat.Value / portionYield, 1) : null,
            SaturatedFatPerServing: totalSaturatedFat.HasValue ? Math.Round(totalSaturatedFat.Value / portionYield, 1) : null,
            FiberPerServing: totalFiber.HasValue ? Math.Round(totalFiber.Value / portionYield, 1) : null,
            SugarPerServing: totalSugar.HasValue ? Math.Round(totalSugar.Value / portionYield, 1) : null,
            SodiumPerServing: totalSodium.HasValue ? Math.Round(totalSodium.Value / portionYield, 1) : null,
            IsComplete: hasAnyNutrition && missingNutrition.Count == 0,
            MissingNutritionIngredients: missingNutrition);
    }

    public async Task<RecipeValidationResult> ValidateRecipeAsync(
        Guid orgId,
        RecipeValidationInput recipe)
    {
        var errors = new List<RecipeValidationError>();
        var warnings = new List<RecipeValidationWarning>();

        // Validate name
        if (string.IsNullOrWhiteSpace(recipe.Name))
        {
            errors.Add(new RecipeValidationError(
                Code: "RECIPE_NAME_REQUIRED",
                Message: "Recipe name is required",
                Field: "Name"));
        }
        else if (recipe.Name.Length > 200)
        {
            errors.Add(new RecipeValidationError(
                Code: "RECIPE_NAME_TOO_LONG",
                Message: "Recipe name must be 200 characters or less",
                Field: "Name"));
        }

        // Validate yield
        if (recipe.PortionYield <= 0)
        {
            errors.Add(new RecipeValidationError(
                Code: "RECIPE_YIELD_REQUIRED",
                Message: "Recipe yield must be greater than zero",
                Field: "PortionYield"));
        }

        if (string.IsNullOrWhiteSpace(recipe.YieldUnit))
        {
            warnings.Add(new RecipeValidationWarning(
                Code: "RECIPE_YIELD_UNIT_MISSING",
                Message: "Yield unit is recommended for clarity",
                Field: "YieldUnit"));
        }

        // Validate ingredients
        if (recipe.Ingredients == null || recipe.Ingredients.Count == 0)
        {
            errors.Add(new RecipeValidationError(
                Code: "RECIPE_INGREDIENTS_REQUIRED",
                Message: "At least one ingredient is required",
                Field: "Ingredients"));
        }
        else
        {
            // Check that all ingredients exist
            var missingIngredients = new List<Guid>();
            var zeroQuantityIngredients = new List<Guid>();

            foreach (var ingredient in recipe.Ingredients)
            {
                // Check ingredient exists
                var ingredientGrain = _grainFactory.GetGrain<IIngredientGrain>(
                    GrainKeys.Ingredient(orgId, ingredient.IngredientId));

                if (!await ingredientGrain.ExistsAsync())
                {
                    missingIngredients.Add(ingredient.IngredientId);
                }

                // Check quantity
                if (ingredient.Quantity <= 0 && !ingredient.IsOptional)
                {
                    zeroQuantityIngredients.Add(ingredient.IngredientId);
                }
            }

            if (missingIngredients.Count > 0)
            {
                errors.Add(new RecipeValidationError(
                    Code: "RECIPE_INGREDIENTS_NOT_FOUND",
                    Message: $"The following ingredients do not exist: {string.Join(", ", missingIngredients)}",
                    Field: "Ingredients"));
            }

            if (zeroQuantityIngredients.Count > 0)
            {
                errors.Add(new RecipeValidationError(
                    Code: "RECIPE_INGREDIENT_ZERO_QUANTITY",
                    Message: $"Required ingredients must have quantity > 0: {string.Join(", ", zeroQuantityIngredients)}",
                    Field: "Ingredients"));
            }

            // Check for zero costs
            var zeroCostIngredients = recipe.Ingredients
                .Where(i => !i.IsOptional && i.UnitCost <= 0)
                .Select(i => i.IngredientId)
                .ToList();

            if (zeroCostIngredients.Count > 0)
            {
                warnings.Add(new RecipeValidationWarning(
                    Code: "RECIPE_INGREDIENT_ZERO_COST",
                    Message: $"Ingredients with zero cost may affect costing accuracy: {string.Join(", ", zeroCostIngredients)}",
                    Field: "Ingredients"));
            }

            // Check allergens are calculated
            if (recipe.AllergenTags == null || recipe.AllergenTags.Count == 0)
            {
                // Calculate expected allergens
                var calculatedAllergens = await CalculateAllergensAsync(orgId, recipe.Ingredients);
                if (calculatedAllergens.ContainsAllergens.Count > 0 ||
                    calculatedAllergens.MayContainAllergens.Count > 0)
                {
                    warnings.Add(new RecipeValidationWarning(
                        Code: "RECIPE_ALLERGENS_NOT_SET",
                        Message: $"Allergens detected from ingredients but not declared: Contains: [{string.Join(", ", calculatedAllergens.ContainsAllergens)}], May contain: [{string.Join(", ", calculatedAllergens.MayContainAllergens)}]",
                        Field: "AllergenTags"));
                }
            }
        }

        return new RecipeValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors,
            Warnings: warnings);
    }

    public async Task<RecipeCostResult> CalculateCostAsync(
        Guid orgId,
        IReadOnlyList<RecipeIngredientInfo> ingredients,
        decimal portionYield)
    {
        if (portionYield <= 0)
            portionYield = 1;

        var costBreakdown = new List<IngredientCostBreakdown>();
        var subRecipeCosts = new List<SubRecipeCostInfo>();
        decimal totalCost = 0;

        foreach (var ingredient in ingredients.Where(i => !i.IsOptional))
        {
            // Calculate effective quantity after waste
            var effectiveQty = ingredient.WastePercentage > 0
                ? ingredient.Quantity / (1 - ingredient.WastePercentage / 100)
                : ingredient.Quantity;

            var lineCost = effectiveQty * ingredient.UnitCost;

            // Check if this is a sub-recipe output
            if (ingredient.IsSubRecipeOutput)
            {
                var ingredientGrain = _grainFactory.GetGrain<IIngredientGrain>(
                    GrainKeys.Ingredient(orgId, ingredient.IngredientId));

                if (await ingredientGrain.ExistsAsync())
                {
                    var snapshot = await ingredientGrain.GetSnapshotAsync();
                    if (!string.IsNullOrEmpty(snapshot.ProducedByRecipeId))
                    {
                        // Get cost from sub-recipe
                        var subRecipeGrain = _grainFactory.GetGrain<IRecipeDocumentGrain>(
                            GrainKeys.RecipeDocument(orgId, snapshot.ProducedByRecipeId));

                        if (await subRecipeGrain.ExistsAsync())
                        {
                            var subRecipeSnapshot = await subRecipeGrain.GetSnapshotAsync();
                            if (subRecipeSnapshot.Published != null)
                            {
                                var subRecipeCostPerUnit = subRecipeSnapshot.Published.CostPerPortion;
                                lineCost = effectiveQty * subRecipeCostPerUnit;

                                subRecipeCosts.Add(new SubRecipeCostInfo(
                                    IngredientId: ingredient.IngredientId,
                                    RecipeDocumentId: snapshot.ProducedByRecipeId,
                                    CostPerUnit: subRecipeCostPerUnit,
                                    QuantityUsed: effectiveQty,
                                    TotalCost: lineCost));
                            }
                        }
                    }
                }
            }

            totalCost += lineCost;

            costBreakdown.Add(new IngredientCostBreakdown(
                IngredientId: ingredient.IngredientId,
                IngredientName: ingredient.IngredientName,
                Quantity: ingredient.Quantity,
                EffectiveQuantity: effectiveQty,
                UnitCost: ingredient.UnitCost,
                LineCost: lineCost,
                CostPercentage: 0)); // Will be calculated after
        }

        // Calculate cost percentages
        if (totalCost > 0)
        {
            costBreakdown = costBreakdown
                .Select(c => c with { CostPercentage = Math.Round(c.LineCost / totalCost * 100, 2) })
                .ToList();
        }

        return new RecipeCostResult(
            TheoreticalCost: totalCost,
            CostPerPortion: totalCost / portionYield,
            CostBreakdown: costBreakdown,
            SubRecipeCosts: subRecipeCosts);
    }
}
