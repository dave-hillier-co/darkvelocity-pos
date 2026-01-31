namespace DarkVelocity.Shared.Infrastructure.Events;

public sealed record PurchaseOrderCreated(
    Guid PurchaseOrderId,
    Guid SupplierId,
    Guid LocationId,
    string OrderNumber
) : IntegrationEvent
{
    public override string EventType => "procurement.po.created";
}

public sealed record PurchaseOrderSubmitted(
    Guid PurchaseOrderId,
    decimal OrderTotal,
    DateTime ExpectedDeliveryDate
) : IntegrationEvent
{
    public override string EventType => "procurement.po.submitted";
}

public sealed record DeliveryReceived(
    Guid DeliveryId,
    Guid? PurchaseOrderId,
    Guid SupplierId,
    Guid LocationId,
    string DeliveryNumber
) : IntegrationEvent
{
    public override string EventType => "procurement.delivery.received";
}

public sealed record DeliveryLineReceived(
    Guid DeliveryId,
    Guid DeliveryLineId,
    Guid IngredientId,
    decimal QuantityReceived,
    decimal UnitCost,
    DateTime? ExpiryDate
) : IntegrationEvent
{
    public override string EventType => "procurement.delivery.line_received";
}

public sealed record DeliveryAccepted(
    Guid DeliveryId,
    decimal TotalValue,
    bool HasDiscrepancies
) : IntegrationEvent
{
    public override string EventType => "procurement.delivery.accepted";
}

public sealed record DeliveryRejected(
    Guid DeliveryId,
    string Reason
) : IntegrationEvent
{
    public override string EventType => "procurement.delivery.rejected";
}
