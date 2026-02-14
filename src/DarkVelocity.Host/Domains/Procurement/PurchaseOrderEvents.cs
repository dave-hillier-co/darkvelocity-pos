namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Purchase Order events used in event sourcing.
/// </summary>
public interface IPurchaseOrderEvent
{
    Guid PurchaseOrderId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record PurchaseOrderDrafted : IPurchaseOrderEvent
{
    [Id(0)] public Guid PurchaseOrderId { get; init; }
    [Id(1)] public Guid OrgId { get; init; }
    [Id(2)] public string OrderNumber { get; init; } = "";
    [Id(3)] public Guid SupplierId { get; init; }
    [Id(4)] public string SupplierName { get; init; } = "";
    [Id(5)] public Guid LocationId { get; init; }
    [Id(6)] public Guid? CreatedByUserId { get; init; }
    [Id(7)] public DateTime ExpectedDeliveryDate { get; init; }
    [Id(8)] public string? Notes { get; init; }
    [Id(9)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseOrderLineAdded : IPurchaseOrderEvent
{
    [Id(0)] public Guid PurchaseOrderId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public Guid SkuId { get; init; }
    [Id(3)] public string SkuCode { get; init; } = "";
    [Id(4)] public decimal QuantityOrdered { get; init; }
    [Id(5)] public decimal UnitPrice { get; init; }
    [Id(6)] public string? Notes { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
    [Id(8)] public string ProductName { get; init; } = "";
}

[GenerateSerializer]
public sealed record PurchaseOrderLineUpdated : IPurchaseOrderEvent
{
    [Id(0)] public Guid PurchaseOrderId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public decimal? QuantityOrdered { get; init; }
    [Id(3)] public decimal? UnitPrice { get; init; }
    [Id(4)] public string? Notes { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseOrderLineRemoved : IPurchaseOrderEvent
{
    [Id(0)] public Guid PurchaseOrderId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseOrderSubmittedEvent : IPurchaseOrderEvent
{
    [Id(0)] public Guid PurchaseOrderId { get; init; }
    [Id(1)] public Guid SubmittedByUserId { get; init; }
    [Id(2)] public decimal OrderTotal { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseOrderLineReceived : IPurchaseOrderEvent
{
    [Id(0)] public Guid PurchaseOrderId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public decimal QuantityReceived { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseOrderFullyReceived : IPurchaseOrderEvent
{
    [Id(0)] public Guid PurchaseOrderId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseOrderCancelledEvent : IPurchaseOrderEvent
{
    [Id(0)] public Guid PurchaseOrderId { get; init; }
    [Id(1)] public string Reason { get; init; } = "";
    [Id(2)] public Guid CancelledByUserId { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}
