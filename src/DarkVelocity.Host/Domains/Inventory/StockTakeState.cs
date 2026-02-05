using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

[GenerateSerializer]
public sealed class StockTakeState
{
    [Id(0)] public Guid StockTakeId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public StockTakeStatus Status { get; set; } = StockTakeStatus.Draft;
    [Id(5)] public bool BlindCount { get; set; }

    /// <summary>
    /// Optional category filter - if set, only items in this category are included.
    /// </summary>
    [Id(6)] public string? Category { get; set; }

    /// <summary>
    /// Optional list of specific ingredients to count.
    /// </summary>
    [Id(7)] public List<Guid> IngredientIds { get; set; } = [];

    /// <summary>
    /// Line items with frozen theoretical values and counts.
    /// </summary>
    [Id(8)] public List<StockTakeLineItemState> LineItems { get; set; } = [];

    [Id(9)] public DateTime StartedAt { get; set; }
    [Id(10)] public Guid StartedBy { get; set; }
    [Id(11)] public DateTime? SubmittedAt { get; set; }
    [Id(12)] public Guid? SubmittedBy { get; set; }
    [Id(13)] public DateTime? FinalizedAt { get; set; }
    [Id(14)] public Guid? FinalizedBy { get; set; }
    [Id(15)] public DateTime? CancelledAt { get; set; }
    [Id(16)] public Guid? CancelledBy { get; set; }
    [Id(17)] public string? CancellationReason { get; set; }
    [Id(18)] public string? Notes { get; set; }
    [Id(19)] public string? ApprovalNotes { get; set; }

    /// <summary>
    /// Whether adjustments were applied when finalized.
    /// </summary>
    [Id(20)] public bool AdjustmentsApplied { get; set; }

    /// <summary>
    /// Summary statistics.
    /// </summary>
    [Id(21)] public decimal TotalVarianceValue { get; set; }
    [Id(22)] public decimal TotalPositiveVariance { get; set; }
    [Id(23)] public decimal TotalNegativeVariance { get; set; }
    [Id(24)] public int ItemsWithVariance { get; set; }
}

[GenerateSerializer]
public sealed class StockTakeLineItemState
{
    [Id(0)] public Guid IngredientId { get; set; }
    [Id(1)] public string IngredientName { get; set; } = string.Empty;
    [Id(2)] public string Sku { get; set; } = string.Empty;
    [Id(3)] public string Unit { get; set; } = string.Empty;
    [Id(4)] public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Theoretical quantity frozen at stock take start.
    /// </summary>
    [Id(5)] public decimal TheoreticalQuantity { get; set; }

    /// <summary>
    /// Unit cost at time of stock take.
    /// </summary>
    [Id(6)] public decimal UnitCost { get; set; }

    /// <summary>
    /// Counted quantity (null if not yet counted).
    /// </summary>
    [Id(7)] public decimal? CountedQuantity { get; set; }

    /// <summary>
    /// Calculated variance (counted - theoretical).
    /// </summary>
    [Id(8)] public decimal Variance { get; set; }

    /// <summary>
    /// Variance as percentage of theoretical.
    /// </summary>
    [Id(9)] public decimal VariancePercentage { get; set; }

    /// <summary>
    /// Variance value (variance * unit cost).
    /// </summary>
    [Id(10)] public decimal VarianceValue { get; set; }

    [Id(11)] public Guid? CountedBy { get; set; }
    [Id(12)] public DateTime? CountedAt { get; set; }
    [Id(13)] public string? Location { get; set; }
    [Id(14)] public string? Notes { get; set; }
    [Id(15)] public string? BatchNumber { get; set; }
    [Id(16)] public VarianceSeverity Severity { get; set; }
}
