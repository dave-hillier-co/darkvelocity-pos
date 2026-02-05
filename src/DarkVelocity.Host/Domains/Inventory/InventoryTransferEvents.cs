using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Inventory Transfer events used in event sourcing.
/// </summary>
public interface IInventoryTransferEvent
{
    Guid TransferId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record TransferRequested : IInventoryTransferEvent
{
    [Id(0)] public Guid TransferId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid SourceSiteId { get; init; }
    [Id(3)] public Guid DestinationSiteId { get; init; }
    [Id(4)] public string TransferNumber { get; init; } = "";
    [Id(5)] public Guid RequestedBy { get; init; }
    [Id(6)] public DateTime? RequestedDeliveryDate { get; init; }
    [Id(7)] public string? Notes { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record TransferLineAdded : IInventoryTransferEvent
{
    [Id(0)] public Guid TransferId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public Guid IngredientId { get; init; }
    [Id(3)] public string IngredientName { get; init; } = "";
    [Id(4)] public decimal Quantity { get; init; }
    [Id(5)] public string Unit { get; init; } = "";
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record TransferApproved : IInventoryTransferEvent
{
    [Id(0)] public Guid TransferId { get; init; }
    [Id(1)] public Guid ApprovedBy { get; init; }
    [Id(2)] public string? Notes { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record TransferRejected : IInventoryTransferEvent
{
    [Id(0)] public Guid TransferId { get; init; }
    [Id(1)] public Guid RejectedBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record TransferShipped : IInventoryTransferEvent
{
    [Id(0)] public Guid TransferId { get; init; }
    [Id(1)] public Guid ShippedBy { get; init; }
    [Id(2)] public DateTime? EstimatedArrival { get; init; }
    [Id(3)] public string? TrackingNumber { get; init; }
    [Id(4)] public string? Carrier { get; init; }
    [Id(5)] public string? Notes { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record TransferLineShipped : IInventoryTransferEvent
{
    [Id(0)] public Guid TransferId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public Guid IngredientId { get; init; }
    [Id(3)] public decimal ShippedQuantity { get; init; }
    [Id(4)] public decimal UnitCost { get; init; }
    [Id(5)] public decimal ShippedValue { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record TransferItemReceived : IInventoryTransferEvent
{
    [Id(0)] public Guid TransferId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public Guid IngredientId { get; init; }
    [Id(3)] public decimal ReceivedQuantity { get; init; }
    [Id(4)] public decimal ShippedQuantity { get; init; }
    [Id(5)] public decimal Variance { get; init; }
    [Id(6)] public decimal VarianceValue { get; init; }
    [Id(7)] public Guid ReceivedBy { get; init; }
    [Id(8)] public string? Condition { get; init; }
    [Id(9)] public string? Notes { get; init; }
    [Id(10)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record TransferReceived : IInventoryTransferEvent
{
    [Id(0)] public Guid TransferId { get; init; }
    [Id(1)] public Guid ReceivedBy { get; init; }
    [Id(2)] public decimal TotalShippedValue { get; init; }
    [Id(3)] public decimal TotalReceivedValue { get; init; }
    [Id(4)] public decimal TotalVarianceValue { get; init; }
    [Id(5)] public int LinesWithVariance { get; init; }
    [Id(6)] public string? Notes { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record TransferCancelled : IInventoryTransferEvent
{
    [Id(0)] public Guid TransferId { get; init; }
    [Id(1)] public Guid CancelledBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public bool StockReturnedToSource { get; init; }
    [Id(4)] public TransferStatus PreviousStatus { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}
