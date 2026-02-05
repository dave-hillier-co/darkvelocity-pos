namespace DarkVelocity.Host.Grains;

/// <summary>
/// ABC inventory classification based on value/velocity.
/// </summary>
public enum AbcClass
{
    /// <summary>High value/velocity items requiring tight control (top ~20% by value).</summary>
    A,
    /// <summary>Medium value/velocity items with standard control (middle ~30% by value).</summary>
    B,
    /// <summary>Low value/velocity items with minimal control (bottom ~50% by value).</summary>
    C,
    /// <summary>Not yet classified.</summary>
    Unclassified
}

/// <summary>
/// Classification method used for ABC analysis.
/// </summary>
public enum ClassificationMethod
{
    /// <summary>Based on annual consumption value (quantity * unit cost).</summary>
    AnnualConsumptionValue,
    /// <summary>Based on usage velocity (consumption frequency).</summary>
    Velocity,
    /// <summary>Based on current inventory value.</summary>
    CurrentValue,
    /// <summary>Combined method using multiple factors.</summary>
    Combined
}

[GenerateSerializer]
public record AbcClassificationSettings(
    [property: Id(0)] decimal ClassAThreshold = 80,  // Top X% of cumulative value
    [property: Id(1)] decimal ClassBThreshold = 95,  // Next Y% up to this threshold
    [property: Id(2)] ClassificationMethod Method = ClassificationMethod.AnnualConsumptionValue,
    [property: Id(3)] int AnalysisPeriodDays = 365,
    [property: Id(4)] bool AutoReclassify = true,
    [property: Id(5)] int ReclassifyIntervalDays = 30);

[GenerateSerializer]
public record ClassifiedItem
{
    [Id(0)] public Guid IngredientId { get; init; }
    [Id(1)] public string IngredientName { get; init; } = string.Empty;
    [Id(2)] public string Sku { get; init; } = string.Empty;
    [Id(3)] public string Category { get; init; } = string.Empty;
    [Id(4)] public AbcClass Classification { get; init; }
    [Id(5)] public decimal AnnualConsumptionValue { get; init; }
    [Id(6)] public decimal CurrentValue { get; init; }
    [Id(7)] public decimal Velocity { get; init; } // Units consumed per day
    [Id(8)] public decimal CumulativePercentage { get; init; }
    [Id(9)] public int Rank { get; init; }
    [Id(10)] public DateTime ClassifiedAt { get; init; }
    [Id(11)] public AbcClass? PreviousClassification { get; init; }
}

[GenerateSerializer]
public record AbcClassificationReport
{
    [Id(0)] public DateTime GeneratedAt { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public ClassificationMethod Method { get; init; }
    [Id(3)] public int AnalysisPeriodDays { get; init; }

    // Class A summary
    [Id(4)] public int ClassACount { get; init; }
    [Id(5)] public decimal ClassAValue { get; init; }
    [Id(6)] public decimal ClassAPercentage { get; init; }

    // Class B summary
    [Id(7)] public int ClassBCount { get; init; }
    [Id(8)] public decimal ClassBValue { get; init; }
    [Id(9)] public decimal ClassBPercentage { get; init; }

    // Class C summary
    [Id(10)] public int ClassCCount { get; init; }
    [Id(11)] public decimal ClassCValue { get; init; }
    [Id(12)] public decimal ClassCPercentage { get; init; }

    // Totals
    [Id(13)] public int TotalItems { get; init; }
    [Id(14)] public decimal TotalValue { get; init; }

    // Detailed lists
    [Id(15)] public List<ClassifiedItem> ClassAItems { get; init; } = [];
    [Id(16)] public List<ClassifiedItem> ClassBItems { get; init; } = [];
    [Id(17)] public List<ClassifiedItem> ClassCItems { get; init; } = [];

    // Items that changed classification
    [Id(18)] public List<ClassifiedItem> ReclassifiedItems { get; init; } = [];
}

[GenerateSerializer]
public record AbcReorderPolicy
{
    [Id(0)] public AbcClass Classification { get; init; }
    [Id(1)] public decimal SafetyStockDays { get; init; }
    [Id(2)] public decimal ReviewFrequencyDays { get; init; }
    [Id(3)] public decimal OrderFrequencyDays { get; init; }
    [Id(4)] public bool RequiresApproval { get; init; }
    [Id(5)] public decimal MaxOrderValueWithoutApproval { get; init; }
}

/// <summary>
/// Grain for ABC inventory classification and analysis.
/// One per site.
/// </summary>
public interface IAbcClassificationGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initializes the ABC classification grain for a site.
    /// </summary>
    Task InitializeAsync(Guid organizationId, Guid siteId);

    /// <summary>
    /// Configures classification settings.
    /// </summary>
    Task ConfigureAsync(AbcClassificationSettings settings);

    /// <summary>
    /// Gets current settings.
    /// </summary>
    Task<AbcClassificationSettings> GetSettingsAsync();

    /// <summary>
    /// Registers an ingredient for classification.
    /// </summary>
    Task RegisterIngredientAsync(Guid ingredientId, string ingredientName, string sku, string category);

    /// <summary>
    /// Removes an ingredient from classification.
    /// </summary>
    Task UnregisterIngredientAsync(Guid ingredientId);

    /// <summary>
    /// Runs the ABC classification analysis on all registered ingredients.
    /// </summary>
    Task<AbcClassificationReport> ClassifyAsync();

    /// <summary>
    /// Gets the classification for a specific ingredient.
    /// </summary>
    Task<ClassifiedItem?> GetClassificationAsync(Guid ingredientId);

    /// <summary>
    /// Gets all items in a specific class.
    /// </summary>
    Task<IReadOnlyList<ClassifiedItem>> GetItemsByClassAsync(AbcClass classification);

    /// <summary>
    /// Gets the full classification report.
    /// </summary>
    Task<AbcClassificationReport> GetReportAsync();

    /// <summary>
    /// Sets the reorder policy for a classification.
    /// </summary>
    Task SetReorderPolicyAsync(AbcReorderPolicy policy);

    /// <summary>
    /// Gets the reorder policy for a classification.
    /// </summary>
    Task<AbcReorderPolicy?> GetReorderPolicyAsync(AbcClass classification);

    /// <summary>
    /// Gets all reorder policies.
    /// </summary>
    Task<IReadOnlyList<AbcReorderPolicy>> GetAllReorderPoliciesAsync();

    /// <summary>
    /// Gets items that have been reclassified since last analysis.
    /// </summary>
    Task<IReadOnlyList<ClassifiedItem>> GetReclassifiedItemsAsync();

    /// <summary>
    /// Manually overrides the classification for an ingredient.
    /// </summary>
    Task OverrideClassificationAsync(Guid ingredientId, AbcClass classification, string reason);

    /// <summary>
    /// Clears the manual override for an ingredient.
    /// </summary>
    Task ClearOverrideAsync(Guid ingredientId);

    /// <summary>
    /// Checks if the grain is initialized.
    /// </summary>
    Task<bool> ExistsAsync();
}
