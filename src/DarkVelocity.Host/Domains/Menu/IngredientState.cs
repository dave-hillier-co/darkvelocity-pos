namespace DarkVelocity.Host.State;

// ============================================================================
// Common Allergen Definitions
// ============================================================================

/// <summary>
/// Standard allergens recognized across ingredients and recipes.
/// Based on EU FIC 14 major allergens plus common additions.
/// </summary>
public static class StandardAllergens
{
    public const string Gluten = "gluten";
    public const string Dairy = "dairy";
    public const string Eggs = "eggs";
    public const string Fish = "fish";
    public const string Shellfish = "shellfish";
    public const string TreeNuts = "tree-nuts";
    public const string Peanuts = "peanuts";
    public const string Soy = "soy";
    public const string Sesame = "sesame";
    public const string Celery = "celery";
    public const string Mustard = "mustard";
    public const string Lupin = "lupin";
    public const string Molluscs = "molluscs";
    public const string Sulphites = "sulphites";

    /// <summary>
    /// All standard allergen identifiers.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Gluten, Dairy, Eggs, Fish, Shellfish, TreeNuts, Peanuts,
        Soy, Sesame, Celery, Mustard, Lupin, Molluscs, Sulphites
    };

    /// <summary>
    /// Human-readable names for allergens.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        [Gluten] = "Gluten",
        [Dairy] = "Dairy",
        [Eggs] = "Eggs",
        [Fish] = "Fish",
        [Shellfish] = "Shellfish",
        [TreeNuts] = "Tree Nuts",
        [Peanuts] = "Peanuts",
        [Soy] = "Soy",
        [Sesame] = "Sesame",
        [Celery] = "Celery",
        [Mustard] = "Mustard",
        [Lupin] = "Lupin",
        [Molluscs] = "Molluscs",
        [Sulphites] = "Sulphites"
    };
}

/// <summary>
/// Allergen declaration type - whether an item contains or may contain an allergen.
/// </summary>
public enum AllergenDeclarationType
{
    /// <summary>
    /// The item definitely contains this allergen.
    /// </summary>
    Contains,

    /// <summary>
    /// The item may contain traces due to cross-contamination (e.g., shared equipment).
    /// </summary>
    MayContain
}

/// <summary>
/// An allergen declaration with its type.
/// </summary>
[GenerateSerializer]
public sealed class AllergenDeclaration
{
    [Id(0)] public string Allergen { get; set; } = string.Empty;
    [Id(1)] public AllergenDeclarationType DeclarationType { get; set; } = AllergenDeclarationType.Contains;
    [Id(2)] public string? Notes { get; set; }

    public AllergenDeclaration() { }

    public AllergenDeclaration(string allergen, AllergenDeclarationType type = AllergenDeclarationType.Contains, string? notes = null)
    {
        Allergen = allergen;
        DeclarationType = type;
        Notes = notes;
    }
}

// ============================================================================
// Nutritional Information at Ingredient Level
// ============================================================================

/// <summary>
/// Nutritional data for an ingredient (per 100g/100ml standard).
/// </summary>
[GenerateSerializer]
public sealed class IngredientNutrition
{
    [Id(0)] public decimal? CaloriesPer100g { get; set; }
    [Id(1)] public decimal? ProteinPer100g { get; set; }
    [Id(2)] public decimal? CarbohydratesPer100g { get; set; }
    [Id(3)] public decimal? FatPer100g { get; set; }
    [Id(4)] public decimal? SaturatedFatPer100g { get; set; }
    [Id(5)] public decimal? FiberPer100g { get; set; }
    [Id(6)] public decimal? SugarPer100g { get; set; }
    [Id(7)] public decimal? SodiumPer100g { get; set; }

    /// <summary>
    /// Whether values are per 100g (solid) or 100ml (liquid).
    /// </summary>
    [Id(8)] public bool IsPerMilliliter { get; set; }
}

// ============================================================================
// Ingredient Master State
// ============================================================================

/// <summary>
/// Supplier association for an ingredient.
/// </summary>
[GenerateSerializer]
public sealed class IngredientSupplierLink
{
    [Id(0)] public Guid SupplierId { get; set; }
    [Id(1)] public string SupplierName { get; set; } = string.Empty;
    [Id(2)] public string? SupplierSku { get; set; }
    [Id(3)] public decimal? SupplierPrice { get; set; }
    [Id(4)] public string? SupplierUnit { get; set; }
    [Id(5)] public decimal? ConversionToBaseUnit { get; set; }
    [Id(6)] public bool IsPreferred { get; set; }
    [Id(7)] public DateTimeOffset? LastPriceUpdate { get; set; }
}

/// <summary>
/// Unit of measure for an ingredient.
/// </summary>
[GenerateSerializer]
public sealed class IngredientUnitOfMeasure
{
    /// <summary>
    /// The base unit for this ingredient (e.g., "g", "ml", "each").
    /// </summary>
    [Id(0)] public string BaseUnit { get; set; } = "g";

    /// <summary>
    /// Conversion factors to base unit (e.g., {"kg": 1000, "oz": 28.35}).
    /// </summary>
    [Id(1)] public Dictionary<string, decimal> Conversions { get; set; } = new();

    /// <summary>
    /// Converts a quantity from one unit to the base unit.
    /// </summary>
    public decimal ConvertToBase(decimal quantity, string fromUnit)
    {
        if (fromUnit == BaseUnit) return quantity;
        if (Conversions.TryGetValue(fromUnit, out var factor))
            return quantity * factor;
        throw new ArgumentException($"Unknown unit: {fromUnit}");
    }
}

/// <summary>
/// Ingredient master data state.
/// Key: "{orgId}:ingredient:{ingredientId}"
/// </summary>
[GenerateSerializer]
public sealed class IngredientState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid IngredientId { get; set; }
    [Id(2)] public bool IsCreated { get; set; }

    // Basic info
    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public string? Description { get; set; }
    [Id(5)] public string? Sku { get; set; }
    [Id(6)] public string? Barcode { get; set; }

    // Unit of measure
    [Id(7)] public IngredientUnitOfMeasure UnitOfMeasure { get; set; } = new();

    // Costing
    [Id(8)] public decimal DefaultCostPerUnit { get; set; }
    [Id(9)] public string CostUnit { get; set; } = "g";
    [Id(10)] public DateTimeOffset? LastCostUpdate { get; set; }
    [Id(11)] public List<IngredientCostHistory> CostHistory { get; set; } = [];

    // Allergens
    [Id(12)] public List<AllergenDeclaration> Allergens { get; set; } = [];

    // Suppliers
    [Id(13)] public List<IngredientSupplierLink> Suppliers { get; set; } = [];

    // Nutrition
    [Id(14)] public IngredientNutrition? Nutrition { get; set; }

    // Categorization
    [Id(15)] public string? Category { get; set; }
    [Id(16)] public List<string> Tags { get; set; } = [];

    // Sub-recipe support: if this ingredient is produced by a recipe
    [Id(17)] public string? ProducedByRecipeId { get; set; }
    [Id(18)] public bool IsSubRecipeOutput { get; set; }

    // Lifecycle
    [Id(19)] public bool IsArchived { get; set; }
    [Id(20)] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Id(21)] public DateTimeOffset? ArchivedAt { get; set; }

    // Product linkage
    [Id(22)] public Guid? ProductId { get; set; }

    // Audit
    [Id(23)] public List<AuditEntry> AuditLog { get; set; } = [];
}

/// <summary>
/// Historical cost entry for an ingredient.
/// </summary>
[GenerateSerializer]
public sealed class IngredientCostHistory
{
    [Id(0)] public decimal CostPerUnit { get; set; }
    [Id(1)] public string Unit { get; set; } = string.Empty;
    [Id(2)] public DateTimeOffset EffectiveDate { get; set; }
    [Id(3)] public Guid? SupplierId { get; set; }
    [Id(4)] public string? Source { get; set; }
    [Id(5)] public Guid? UpdatedBy { get; set; }
}

// ============================================================================
// Ingredient Registry State
// ============================================================================

/// <summary>
/// Registry entry for an ingredient.
/// </summary>
[GenerateSerializer]
public sealed class IngredientRegistryEntry
{
    [Id(0)] public Guid IngredientId { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public string? Sku { get; set; }
    [Id(3)] public string? Category { get; set; }
    [Id(4)] public decimal DefaultCostPerUnit { get; set; }
    [Id(5)] public string BaseUnit { get; set; } = "g";
    [Id(6)] public List<string> AllergenTags { get; set; } = [];
    [Id(7)] public bool IsSubRecipeOutput { get; set; }
    [Id(8)] public bool IsArchived { get; set; }
    [Id(9)] public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Registry state for ingredients.
/// Key: "{orgId}:ingredientregistry"
/// </summary>
[GenerateSerializer]
public sealed class IngredientRegistryState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public bool IsCreated { get; set; }

    [Id(2)] public Dictionary<Guid, IngredientRegistryEntry> Ingredients { get; set; } = [];
}
