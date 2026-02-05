using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Stock take (inventory count) status.
/// </summary>
public enum StockTakeStatus
{
    /// <summary>Initial state when stock take is created but not started.</summary>
    Draft,
    /// <summary>Stock take is in progress - theoretical values are frozen.</summary>
    InProgress,
    /// <summary>All counts have been recorded, waiting for approval.</summary>
    PendingApproval,
    /// <summary>Stock take has been approved and inventory adjusted.</summary>
    Finalized,
    /// <summary>Stock take was cancelled.</summary>
    Cancelled
}

/// <summary>
/// Variance severity classification.
/// </summary>
public enum VarianceSeverity
{
    None,
    Low,      // < 2% variance
    Medium,   // 2-5% variance
    High,     // 5-10% variance
    Critical  // > 10% variance
}

[GenerateSerializer]
public record StartStockTakeCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string Name,
    [property: Id(3)] Guid StartedBy,
    [property: Id(4)] bool BlindCount = false,
    [property: Id(5)] string? Category = null,
    [property: Id(6)] List<Guid>? IngredientIds = null,
    [property: Id(7)] string? Notes = null);

[GenerateSerializer]
public record RecordCountCommand(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] decimal CountedQuantity,
    [property: Id(2)] Guid CountedBy,
    [property: Id(3)] string? BatchNumber = null,
    [property: Id(4)] string? Location = null,
    [property: Id(5)] string? Notes = null);

[GenerateSerializer]
public record FinalizeStockTakeCommand(
    [property: Id(0)] Guid ApprovedBy,
    [property: Id(1)] bool ApplyAdjustments = true,
    [property: Id(2)] string? ApprovalNotes = null);

[GenerateSerializer]
public record StockTakeLineItem
{
    [Id(0)] public Guid IngredientId { get; init; }
    [Id(1)] public string IngredientName { get; init; } = string.Empty;
    [Id(2)] public string Sku { get; init; } = string.Empty;
    [Id(3)] public string Unit { get; init; } = string.Empty;
    [Id(4)] public string Category { get; init; } = string.Empty;
    [Id(5)] public decimal TheoreticalQuantity { get; init; }
    [Id(6)] public decimal? CountedQuantity { get; init; }
    [Id(7)] public decimal Variance { get; init; }
    [Id(8)] public decimal VariancePercentage { get; init; }
    [Id(9)] public decimal VarianceValue { get; init; }
    [Id(10)] public decimal UnitCost { get; init; }
    [Id(11)] public Guid? CountedBy { get; init; }
    [Id(12)] public DateTime? CountedAt { get; init; }
    [Id(13)] public VarianceSeverity Severity { get; init; }
    [Id(14)] public string? Location { get; init; }
    [Id(15)] public string? Notes { get; init; }
}

[GenerateSerializer]
public record StockTakeVarianceReport
{
    [Id(0)] public Guid StockTakeId { get; init; }
    [Id(1)] public int TotalItems { get; init; }
    [Id(2)] public int ItemsCounted { get; init; }
    [Id(3)] public int ItemsWithVariance { get; init; }
    [Id(4)] public decimal TotalVarianceValue { get; init; }
    [Id(5)] public decimal TotalPositiveVariance { get; init; }
    [Id(6)] public decimal TotalNegativeVariance { get; init; }
    [Id(7)] public int CriticalVarianceCount { get; init; }
    [Id(8)] public int HighVarianceCount { get; init; }
    [Id(9)] public int MediumVarianceCount { get; init; }
    [Id(10)] public int LowVarianceCount { get; init; }
    [Id(11)] public Dictionary<string, decimal> VarianceByCategory { get; init; } = new();
    [Id(12)] public List<StockTakeLineItem> HighVarianceItems { get; init; } = [];
    [Id(13)] public decimal AccuracyPercentage { get; init; }
}

[GenerateSerializer]
public record StockTakeSummary
{
    [Id(0)] public Guid StockTakeId { get; init; }
    [Id(1)] public string Name { get; init; } = string.Empty;
    [Id(2)] public StockTakeStatus Status { get; init; }
    [Id(3)] public DateTime StartedAt { get; init; }
    [Id(4)] public DateTime? FinalizedAt { get; init; }
    [Id(5)] public int TotalItems { get; init; }
    [Id(6)] public int ItemsCounted { get; init; }
    [Id(7)] public decimal TotalVarianceValue { get; init; }
    [Id(8)] public bool BlindCount { get; init; }
}

/// <summary>
/// Grain for managing stock take (inventory count) workflow.
/// Freezes theoretical inventory values, supports blind count mode,
/// tracks variances, and applies adjustments when finalized.
/// </summary>
public interface IStockTakeGrain : IGrainWithStringKey
{
    /// <summary>
    /// Starts a new stock take, freezing theoretical inventory values.
    /// </summary>
    Task StartAsync(StartStockTakeCommand command);

    /// <summary>
    /// Gets the current stock take state.
    /// </summary>
    Task<StockTakeState> GetStateAsync();

    /// <summary>
    /// Gets a summary of the stock take.
    /// </summary>
    Task<StockTakeSummary> GetSummaryAsync();

    /// <summary>
    /// Records a count for an ingredient.
    /// </summary>
    Task RecordCountAsync(RecordCountCommand command);

    /// <summary>
    /// Records multiple counts at once.
    /// </summary>
    Task RecordCountsAsync(IEnumerable<RecordCountCommand> commands);

    /// <summary>
    /// Gets the line items for the stock take.
    /// In blind count mode, theoretical quantities are hidden until finalized.
    /// </summary>
    Task<IReadOnlyList<StockTakeLineItem>> GetLineItemsAsync(bool includeTheoretical = true);

    /// <summary>
    /// Gets pending (uncounted) items.
    /// </summary>
    Task<IReadOnlyList<StockTakeLineItem>> GetPendingItemsAsync();

    /// <summary>
    /// Submits the stock take for approval.
    /// </summary>
    Task SubmitForApprovalAsync(Guid submittedBy);

    /// <summary>
    /// Finalizes the stock take and optionally applies adjustments.
    /// </summary>
    Task FinalizeAsync(FinalizeStockTakeCommand command);

    /// <summary>
    /// Cancels the stock take.
    /// </summary>
    Task CancelAsync(Guid cancelledBy, string reason);

    /// <summary>
    /// Generates a variance report.
    /// </summary>
    Task<StockTakeVarianceReport> GetVarianceReportAsync();

    /// <summary>
    /// Gets high variance items (above threshold).
    /// </summary>
    Task<IReadOnlyList<StockTakeLineItem>> GetHighVarianceItemsAsync(decimal thresholdPercentage = 5);

    /// <summary>
    /// Checks if the stock take exists.
    /// </summary>
    Task<bool> ExistsAsync();
}
