using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Stock Take events used in event sourcing.
/// </summary>
public interface IStockTakeEvent
{
    Guid StockTakeId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record StockTakeStarted : IStockTakeEvent
{
    [Id(0)] public Guid StockTakeId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid SiteId { get; init; }
    [Id(3)] public string Name { get; init; } = "";
    [Id(4)] public bool BlindCount { get; init; }
    [Id(5)] public string? Category { get; init; }
    [Id(6)] public List<Guid> IngredientIds { get; init; } = [];
    [Id(7)] public Guid StartedBy { get; init; }
    [Id(8)] public string? Notes { get; init; }
    [Id(9)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record StockTakeLineItemAdded : IStockTakeEvent
{
    [Id(0)] public Guid StockTakeId { get; init; }
    [Id(1)] public Guid IngredientId { get; init; }
    [Id(2)] public string IngredientName { get; init; } = "";
    [Id(3)] public string Sku { get; init; } = "";
    [Id(4)] public string Unit { get; init; } = "";
    [Id(5)] public string Category { get; init; } = "";
    [Id(6)] public decimal TheoreticalQuantity { get; init; }
    [Id(7)] public decimal UnitCost { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CountRecorded : IStockTakeEvent
{
    [Id(0)] public Guid StockTakeId { get; init; }
    [Id(1)] public Guid IngredientId { get; init; }
    [Id(2)] public decimal CountedQuantity { get; init; }
    [Id(3)] public decimal TheoreticalQuantity { get; init; }
    [Id(4)] public decimal Variance { get; init; }
    [Id(5)] public decimal VariancePercentage { get; init; }
    [Id(6)] public decimal VarianceValue { get; init; }
    [Id(7)] public decimal UnitCost { get; init; }
    [Id(8)] public VarianceSeverity Severity { get; init; }
    [Id(9)] public Guid CountedBy { get; init; }
    [Id(10)] public string? BatchNumber { get; init; }
    [Id(11)] public string? Location { get; init; }
    [Id(12)] public string? Notes { get; init; }
    [Id(13)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record StockTakeSubmittedForApproval : IStockTakeEvent
{
    [Id(0)] public Guid StockTakeId { get; init; }
    [Id(1)] public Guid SubmittedBy { get; init; }
    [Id(2)] public int TotalItems { get; init; }
    [Id(3)] public int ItemsCounted { get; init; }
    [Id(4)] public decimal TotalVarianceValue { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record StockTakeFinalized : IStockTakeEvent
{
    [Id(0)] public Guid StockTakeId { get; init; }
    [Id(1)] public Guid FinalizedBy { get; init; }
    [Id(2)] public bool AdjustmentsApplied { get; init; }
    [Id(3)] public decimal TotalVarianceValue { get; init; }
    [Id(4)] public decimal TotalPositiveVariance { get; init; }
    [Id(5)] public decimal TotalNegativeVariance { get; init; }
    [Id(6)] public int ItemsAdjusted { get; init; }
    [Id(7)] public string? ApprovalNotes { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record StockTakeCancelled : IStockTakeEvent
{
    [Id(0)] public Guid StockTakeId { get; init; }
    [Id(1)] public Guid CancelledBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record VarianceCalculated : IStockTakeEvent
{
    [Id(0)] public Guid StockTakeId { get; init; }
    [Id(1)] public Guid IngredientId { get; init; }
    [Id(2)] public decimal TheoreticalQuantity { get; init; }
    [Id(3)] public decimal CountedQuantity { get; init; }
    [Id(4)] public decimal Variance { get; init; }
    [Id(5)] public decimal VariancePercentage { get; init; }
    [Id(6)] public decimal VarianceValue { get; init; }
    [Id(7)] public VarianceSeverity Severity { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
}
