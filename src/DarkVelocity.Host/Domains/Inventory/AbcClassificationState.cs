using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

[GenerateSerializer]
public sealed class AbcClassificationState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }

    /// <summary>
    /// Classification settings.
    /// </summary>
    [Id(2)] public AbcClassificationSettings Settings { get; set; } = new();

    /// <summary>
    /// Registered ingredients.
    /// </summary>
    [Id(3)] public Dictionary<Guid, AbcIngredientData> Ingredients { get; set; } = new();

    /// <summary>
    /// Reorder policies by classification.
    /// </summary>
    [Id(4)] public Dictionary<AbcClass, AbcReorderPolicy> ReorderPolicies { get; set; } = new();

    /// <summary>
    /// Manual classification overrides.
    /// </summary>
    [Id(5)] public Dictionary<Guid, AbcClassificationOverride> Overrides { get; set; } = new();

    /// <summary>
    /// Last classification run timestamp.
    /// </summary>
    [Id(6)] public DateTime? LastClassifiedAt { get; set; }

    /// <summary>
    /// Cached report summary.
    /// </summary>
    [Id(7)] public AbcReportCache? CachedReport { get; set; }
}

[GenerateSerializer]
public sealed class AbcIngredientData
{
    [Id(0)] public Guid IngredientId { get; set; }
    [Id(1)] public string IngredientName { get; set; } = string.Empty;
    [Id(2)] public string Sku { get; set; } = string.Empty;
    [Id(3)] public string Category { get; set; } = string.Empty;
    [Id(4)] public DateTime RegisteredAt { get; set; }

    // Classification data
    [Id(5)] public AbcClass Classification { get; set; } = AbcClass.Unclassified;
    [Id(6)] public AbcClass? PreviousClassification { get; set; }
    [Id(7)] public decimal AnnualConsumptionValue { get; set; }
    [Id(8)] public decimal CurrentValue { get; set; }
    [Id(9)] public decimal Velocity { get; set; }
    [Id(10)] public decimal CumulativePercentage { get; set; }
    [Id(11)] public int Rank { get; set; }
    [Id(12)] public DateTime? ClassifiedAt { get; set; }
}

[GenerateSerializer]
public sealed class AbcClassificationOverride
{
    [Id(0)] public Guid IngredientId { get; set; }
    [Id(1)] public AbcClass OverrideClassification { get; set; }
    [Id(2)] public string Reason { get; set; } = string.Empty;
    [Id(3)] public DateTime OverriddenAt { get; set; }
    [Id(4)] public Guid OverriddenBy { get; set; }
}

[GenerateSerializer]
public sealed class AbcReportCache
{
    [Id(0)] public DateTime GeneratedAt { get; set; }
    [Id(1)] public int ClassACount { get; set; }
    [Id(2)] public decimal ClassAValue { get; set; }
    [Id(3)] public int ClassBCount { get; set; }
    [Id(4)] public decimal ClassBValue { get; set; }
    [Id(5)] public int ClassCCount { get; set; }
    [Id(6)] public decimal ClassCValue { get; set; }
    [Id(7)] public int TotalItems { get; set; }
    [Id(8)] public decimal TotalValue { get; set; }
}
