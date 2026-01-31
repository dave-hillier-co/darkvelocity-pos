namespace DarkVelocity.Host.Events;

public sealed record OrderCreated(
    Guid OrderId,
    Guid LocationId,
    Guid UserId,
    string OrderNumber,
    string OrderType,
    Guid? TableId = null
) : IntegrationEvent
{
    public override string EventType => "orders.order.created";
}

public sealed record OrderLineAdded(
    Guid OrderId,
    Guid LineId,
    Guid MenuItemId,
    string ItemName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal
) : IntegrationEvent
{
    public override string EventType => "orders.line.added";
}

public sealed record OrderLineRemoved(
    Guid OrderId,
    Guid LineId
) : IntegrationEvent
{
    public override string EventType => "orders.line.removed";
}

public sealed record OrderCompleted(
    Guid OrderId,
    Guid LocationId,
    string OrderNumber,
    decimal GrandTotal,
    List<OrderLineSnapshot> Lines
) : IntegrationEvent
{
    public override string EventType => "orders.order.completed";
}

public sealed record OrderLineSnapshot(
    Guid LineId,
    Guid MenuItemId,
    string ItemName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal
);

public sealed record OrderVoided(
    Guid OrderId,
    Guid UserId,
    string Reason
) : IntegrationEvent
{
    public override string EventType => "orders.order.voided";
}
