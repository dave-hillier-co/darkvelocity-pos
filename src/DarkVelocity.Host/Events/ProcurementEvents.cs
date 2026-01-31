namespace DarkVelocity.Host.Events;

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
