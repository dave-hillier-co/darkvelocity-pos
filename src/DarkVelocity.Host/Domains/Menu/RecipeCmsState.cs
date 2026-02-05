namespace DarkVelocity.Host.State;

// ============================================================================
// Recipe Document State
// ============================================================================

/// <summary>
/// Defines how a recipe is produced and consumed.
/// </summary>
public enum RecipeType
{
    /// <summary>
    /// Made at sale time. Ingredients are consumed when the item is sold.
    /// Example: a cocktail mixed when ordered, a sandwich made to order.
    /// </summary>
    MadeToOrder,

    /// <summary>
    /// Made in advance as batch prep. Consumes ingredients during production
    /// and creates stocked inventory with a shelf life that is consumed when sold.
    /// Example: a house sauce prepped daily, pre-portioned proteins, soup stock.
    /// </summary>
    BatchPrep
}

/// <summary>
/// A recipe ingredient within a version.
/// </summary>
[GenerateSerializer]
public sealed class RecipeIngredientState
{
    [Id(0)] public Guid IngredientId { get; set; }
    [Id(1)] public string IngredientName { get; set; } = string.Empty;
    [Id(2)] public decimal Quantity { get; set; }
    [Id(3)] public string Unit { get; set; } = string.Empty;
    [Id(4)] public decimal WastePercentage { get; set; }
    [Id(5)] public decimal UnitCost { get; set; }
    [Id(6)] public string? PrepInstructions { get; set; }
    [Id(7)] public bool IsOptional { get; set; }
    [Id(8)] public int DisplayOrder { get; set; }
    [Id(9)] public List<Guid> SubstitutionIds { get; set; } = [];

    /// <summary>
    /// If this ingredient is produced by a sub-recipe, the document ID of that recipe.
    /// Used for cost rollup and allergen cascade.
    /// </summary>
    [Id(10)] public string? SubRecipeDocumentId { get; set; }

    /// <summary>
    /// Whether this ingredient is output from a sub-recipe.
    /// </summary>
    [Id(11)] public bool IsSubRecipeOutput { get; set; }

    /// <summary>
    /// Effective quantity after waste adjustment: Quantity / (1 - WastePercentage/100)
    /// </summary>
    public decimal EffectiveQuantity => WastePercentage > 0
        ? Quantity / (1 - WastePercentage / 100)
        : Quantity;

    /// <summary>
    /// Line cost: EffectiveQuantity * UnitCost
    /// </summary>
    public decimal LineCost => EffectiveQuantity * UnitCost;
}

/// <summary>
/// Enhanced allergen declaration with "contains" vs "may contain" distinction.
/// </summary>
[GenerateSerializer]
public sealed class RecipeAllergenDeclaration
{
    [Id(0)] public string Allergen { get; set; } = string.Empty;
    [Id(1)] public AllergenDeclarationType DeclarationType { get; set; } = AllergenDeclarationType.Contains;
    [Id(2)] public string? Source { get; set; }
    [Id(3)] public Guid? SourceIngredientId { get; set; }
}

/// <summary>
/// Nutritional information override for a recipe.
/// When set, these values override calculated values.
/// </summary>
[GenerateSerializer]
public sealed class RecipeNutritionOverride
{
    [Id(0)] public decimal? CaloriesPerServing { get; set; }
    [Id(1)] public decimal? ProteinPerServing { get; set; }
    [Id(2)] public decimal? CarbohydratesPerServing { get; set; }
    [Id(3)] public decimal? FatPerServing { get; set; }
    [Id(4)] public decimal? SaturatedFatPerServing { get; set; }
    [Id(5)] public decimal? FiberPerServing { get; set; }
    [Id(6)] public decimal? SugarPerServing { get; set; }
    [Id(7)] public decimal? SodiumPerServing { get; set; }
    [Id(8)] public string? ServingSize { get; set; }
}

/// <summary>
/// A single version of a recipe document.
/// </summary>
[GenerateSerializer]
public sealed class RecipeVersionState
{
    [Id(0)] public int VersionNumber { get; set; }
    [Id(1)] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Id(2)] public Guid? CreatedBy { get; set; }
    [Id(3)] public string? ChangeNote { get; set; }

    // Content
    [Id(4)] public LocalizedContent Content { get; set; } = new();
    [Id(5)] public MediaInfo? Media { get; set; }

    // Recipe details
    [Id(6)] public decimal PortionYield { get; set; } = 1;
    [Id(7)] public string YieldUnit { get; set; } = "portion";
    [Id(8)] public List<RecipeIngredientState> Ingredients { get; set; } = [];
    [Id(9)] public List<string> AllergenTags { get; set; } = [];
    [Id(10)] public List<string> DietaryTags { get; set; } = [];
    [Id(11)] public string? PrepInstructions { get; set; }
    [Id(12)] public int PrepTimeMinutes { get; set; }
    [Id(13)] public int CookTimeMinutes { get; set; }

    // References
    [Id(14)] public Guid? CategoryId { get; set; }

    // Recipe type and batch prep configuration
    [Id(15)] public RecipeType RecipeType { get; set; } = RecipeType.MadeToOrder;

    /// <summary>
    /// For BatchPrep recipes: the inventory item ID that this recipe produces.
    /// When a batch is produced, stock of this item is created.
    /// </summary>
    [Id(16)] public Guid? OutputInventoryItemId { get; set; }

    /// <summary>
    /// For BatchPrep recipes: the name of the output item (for display).
    /// </summary>
    [Id(17)] public string? OutputInventoryItemName { get; set; }

    /// <summary>
    /// For BatchPrep recipes: how long the produced item lasts in hours.
    /// Used to calculate expiry date when production batches are created.
    /// </summary>
    [Id(18)] public int? ShelfLifeHours { get; set; }

    /// <summary>
    /// For BatchPrep recipes: minimum batch size for production (optional).
    /// </summary>
    [Id(19)] public decimal? MinBatchSize { get; set; }

    /// <summary>
    /// For BatchPrep recipes: maximum batch size for production (optional).
    /// </summary>
    [Id(20)] public decimal? MaxBatchSize { get; set; }

    /// <summary>
    /// For BatchPrep recipes: the unit for the output item (may differ from yield unit).
    /// Example: recipe yields "2 liters" but output is stocked in "portions" (8 portions per 2L).
    /// </summary>
    [Id(21)] public string? OutputUnit { get; set; }

    /// <summary>
    /// For BatchPrep recipes: how many output units are created per recipe yield.
    /// Example: 1 batch yields 2L, which equals 8 portions, so OutputQuantityPerYield = 8.
    /// </summary>
    [Id(22)] public decimal? OutputQuantityPerYield { get; set; }

    /// <summary>
    /// Enhanced allergen declarations with "contains" vs "may contain" distinction.
    /// This replaces the simple AllergenTags list for more precise allergen tracking.
    /// </summary>
    [Id(23)] public List<RecipeAllergenDeclaration> AllergenDeclarations { get; set; } = [];

    /// <summary>
    /// Nutritional information override. When set, these values override calculated values.
    /// </summary>
    [Id(24)] public RecipeNutritionOverride? NutritionOverride { get; set; }

    /// <summary>
    /// Whether this recipe is used as a sub-recipe/component in other recipes.
    /// </summary>
    [Id(25)] public bool IsSubRecipe { get; set; }

    /// <summary>
    /// Parent recipes that use this as a sub-recipe.
    /// </summary>
    [Id(26)] public List<string> UsedByRecipeIds { get; set; } = [];

    /// <summary>
    /// Theoretical cost: sum of all ingredient line costs.
    /// </summary>
    public decimal TheoreticalCost => Ingredients.Sum(i => i.LineCost);

    /// <summary>
    /// Cost per portion: TheoreticalCost / PortionYield
    /// </summary>
    public decimal CostPerPortion => PortionYield > 0 ? TheoreticalCost / PortionYield : 0;

    /// <summary>
    /// For BatchPrep recipes: cost per output unit.
    /// </summary>
    public decimal? CostPerOutputUnit => OutputQuantityPerYield.HasValue && OutputQuantityPerYield > 0
        ? TheoreticalCost / OutputQuantityPerYield.Value
        : null;

    /// <summary>
    /// Gets all "contains" allergens from declarations plus legacy allergen tags.
    /// </summary>
    public IReadOnlyList<string> GetContainsAllergens()
    {
        var contains = AllergenDeclarations
            .Where(a => a.DeclarationType == AllergenDeclarationType.Contains)
            .Select(a => a.Allergen)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Include legacy allergen tags
        foreach (var tag in AllergenTags)
            contains.Add(tag);

        return contains.ToList();
    }

    /// <summary>
    /// Gets all "may contain" allergens from declarations.
    /// </summary>
    public IReadOnlyList<string> GetMayContainAllergens()
    {
        return AllergenDeclarations
            .Where(a => a.DeclarationType == AllergenDeclarationType.MayContain)
            .Select(a => a.Allergen)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

/// <summary>
/// Recipe as a versioned document with draft/published workflow.
/// Key: "{orgId}:recipedoc:{documentId}"
/// </summary>
[GenerateSerializer]
public sealed class RecipeDocumentState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public string DocumentId { get; set; } = string.Empty;
    [Id(2)] public bool IsCreated { get; set; }

    // Version management
    [Id(3)] public int CurrentVersion { get; set; }
    [Id(4)] public int? PublishedVersion { get; set; }
    [Id(5)] public int? DraftVersion { get; set; }
    [Id(6)] public List<RecipeVersionState> Versions { get; set; } = [];

    // Scheduling
    [Id(7)] public List<ScheduledChange> Schedules { get; set; } = [];

    // Audit
    [Id(8)] public List<AuditEntry> AuditLog { get; set; } = [];

    // Lifecycle
    [Id(9)] public bool IsArchived { get; set; }
    [Id(10)] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Id(11)] public DateTimeOffset? ArchivedAt { get; set; }

    // Menu item linkage (recipes linked to menu items)
    [Id(12)] public List<string> LinkedMenuItemIds { get; set; } = [];
}

// ============================================================================
// Recipe Category Document State
// ============================================================================

/// <summary>
/// A single version of a recipe category document.
/// </summary>
[GenerateSerializer]
public sealed class RecipeCategoryVersionState
{
    [Id(0)] public int VersionNumber { get; set; }
    [Id(1)] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Id(2)] public Guid? CreatedBy { get; set; }
    [Id(3)] public string? ChangeNote { get; set; }

    // Content
    [Id(4)] public LocalizedContent Content { get; set; } = new();
    [Id(5)] public string? Color { get; set; }
    [Id(6)] public string? IconUrl { get; set; }
    [Id(7)] public int DisplayOrder { get; set; }

    // Recipes in this category (ordered)
    [Id(8)] public List<string> RecipeDocumentIds { get; set; } = [];
}

/// <summary>
/// Recipe category as a versioned document with draft/published workflow.
/// Key: "{orgId}:recipecategorydoc:{documentId}"
/// </summary>
[GenerateSerializer]
public sealed class RecipeCategoryDocumentState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public string DocumentId { get; set; } = string.Empty;
    [Id(2)] public bool IsCreated { get; set; }

    // Version management
    [Id(3)] public int CurrentVersion { get; set; }
    [Id(4)] public int? PublishedVersion { get; set; }
    [Id(5)] public int? DraftVersion { get; set; }
    [Id(6)] public List<RecipeCategoryVersionState> Versions { get; set; } = [];

    // Scheduling
    [Id(7)] public List<ScheduledChange> Schedules { get; set; } = [];

    // Audit
    [Id(8)] public List<AuditEntry> AuditLog { get; set; } = [];

    // Lifecycle
    [Id(9)] public bool IsArchived { get; set; }
    [Id(10)] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

// ============================================================================
// Recipe Registry State
// ============================================================================

/// <summary>
/// Registry state for recipe documents.
/// Key: "{orgId}:reciperegistry"
/// </summary>
[GenerateSerializer]
public sealed class RecipeRegistryState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public bool IsCreated { get; set; }

    // Recipe index
    [Id(2)] public Dictionary<string, RecipeRegistryEntry> Recipes { get; set; } = [];

    // Category index
    [Id(3)] public Dictionary<string, RecipeCategoryRegistryEntry> Categories { get; set; } = [];
}

/// <summary>
/// Registry entry for a recipe document.
/// </summary>
[GenerateSerializer]
public sealed class RecipeRegistryEntry
{
    [Id(0)] public string DocumentId { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public decimal CostPerPortion { get; set; }
    [Id(3)] public string? CategoryId { get; set; }
    [Id(4)] public bool HasDraft { get; set; }
    [Id(5)] public bool IsArchived { get; set; }
    [Id(6)] public int PublishedVersion { get; set; }
    [Id(7)] public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;
    [Id(8)] public int LinkedMenuItemCount { get; set; }
    [Id(9)] public RecipeType RecipeType { get; set; } = RecipeType.MadeToOrder;
    [Id(10)] public Guid? OutputInventoryItemId { get; set; }
    [Id(11)] public int? ShelfLifeHours { get; set; }
}

/// <summary>
/// Registry entry for a recipe category document.
/// </summary>
[GenerateSerializer]
public sealed class RecipeCategoryRegistryEntry
{
    [Id(0)] public string DocumentId { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public int DisplayOrder { get; set; }
    [Id(3)] public string? Color { get; set; }
    [Id(4)] public bool HasDraft { get; set; }
    [Id(5)] public bool IsArchived { get; set; }
    [Id(6)] public int RecipeCount { get; set; }
    [Id(7)] public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;
}
