using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Delivery events used in event sourcing.
/// </summary>
public interface IDeliveryEvent
{
    Guid DeliveryId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record DeliveryCreated : IDeliveryEvent
{
    [Id(0)] public Guid DeliveryId { get; init; }
    [Id(1)] public Guid OrgId { get; init; }
    [Id(2)] public string DeliveryNumber { get; init; } = "";
    [Id(3)] public Guid SupplierId { get; init; }
    [Id(4)] public string SupplierName { get; init; } = "";
    [Id(5)] public Guid? PurchaseOrderId { get; init; }
    [Id(6)] public Guid LocationId { get; init; }
    [Id(7)] public Guid? ReceivedByUserId { get; init; }
    [Id(8)] public string? SupplierInvoiceNumber { get; init; }
    [Id(9)] public string? Notes { get; init; }
    [Id(10)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record DeliveryLineAdded : IDeliveryEvent
{
    [Id(0)] public Guid DeliveryId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public Guid SkuId { get; init; }
    [Id(3)] public string SkuCode { get; init; } = "";
    [Id(4)] public Guid? PurchaseOrderLineId { get; init; }
    [Id(5)] public decimal QuantityReceived { get; init; }
    [Id(6)] public decimal UnitCost { get; init; }
    [Id(7)] public string? BatchNumber { get; init; }
    [Id(8)] public DateTime? ExpiryDate { get; init; }
    [Id(9)] public string? Notes { get; init; }
    [Id(10)] public DateTime OccurredAt { get; init; }
    [Id(11)] public string ProductName { get; init; } = "";
}

[GenerateSerializer]
public sealed record DeliveryDiscrepancyRecorded : IDeliveryEvent
{
    [Id(0)] public Guid DeliveryId { get; init; }
    [Id(1)] public Guid DiscrepancyId { get; init; }
    [Id(2)] public Guid LineId { get; init; }
    [Id(3)] public Grains.DiscrepancyType Type { get; init; }
    [Id(4)] public decimal ExpectedQuantity { get; init; }
    [Id(5)] public decimal ActualQuantity { get; init; }
    [Id(6)] public string? Notes { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record DeliveryAcceptedEvent : IDeliveryEvent
{
    [Id(0)] public Guid DeliveryId { get; init; }
    [Id(1)] public Guid AcceptedByUserId { get; init; }
    [Id(2)] public decimal TotalValue { get; init; }
    [Id(3)] public bool HasDiscrepancies { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record DeliveryRejectedEvent : IDeliveryEvent
{
    [Id(0)] public Guid DeliveryId { get; init; }
    [Id(1)] public string Reason { get; init; } = "";
    [Id(2)] public Guid RejectedByUserId { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}
