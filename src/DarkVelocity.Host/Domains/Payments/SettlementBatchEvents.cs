namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Settlement Batch events used in event sourcing.
/// </summary>
public interface ISettlementBatchEvent
{
    Guid BatchId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record BatchOpened : ISettlementBatchEvent
{
    [Id(0)] public Guid BatchId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid SiteId { get; init; }
    [Id(3)] public DateOnly BusinessDate { get; init; }
    [Id(4)] public string BatchNumber { get; init; } = "";
    [Id(5)] public Guid OpenedBy { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentAddedToBatch : ISettlementBatchEvent
{
    [Id(0)] public Guid BatchId { get; init; }
    [Id(1)] public Guid PaymentId { get; init; }
    [Id(2)] public decimal Amount { get; init; }
    [Id(3)] public State.PaymentMethod Method { get; init; }
    [Id(4)] public string? GatewayReference { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentRemovedFromBatch : ISettlementBatchEvent
{
    [Id(0)] public Guid BatchId { get; init; }
    [Id(1)] public Guid PaymentId { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BatchClosed : ISettlementBatchEvent
{
    [Id(0)] public Guid BatchId { get; init; }
    [Id(1)] public Guid ClosedBy { get; init; }
    [Id(2)] public decimal TotalAmount { get; init; }
    [Id(3)] public int PaymentCount { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BatchSettled : ISettlementBatchEvent
{
    [Id(0)] public Guid BatchId { get; init; }
    [Id(1)] public string SettlementReference { get; init; } = "";
    [Id(2)] public decimal SettledAmount { get; init; }
    [Id(3)] public decimal ProcessingFees { get; init; }
    [Id(4)] public decimal NetAmount { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BatchSettlementFailed : ISettlementBatchEvent
{
    [Id(0)] public Guid BatchId { get; init; }
    [Id(1)] public string ErrorCode { get; init; } = "";
    [Id(2)] public string ErrorMessage { get; init; } = "";
    [Id(3)] public int RetryCount { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BatchReopened : ISettlementBatchEvent
{
    [Id(0)] public Guid BatchId { get; init; }
    [Id(1)] public Guid ReopenedBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}
