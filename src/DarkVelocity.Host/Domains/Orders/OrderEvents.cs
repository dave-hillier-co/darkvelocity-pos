using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Order events used in event sourcing.
/// These events are the source of truth for OrderGrain state.
/// </summary>
public interface IOrderEvent
{
    Guid OrderId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record OrderCreated : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid SiteId { get; init; }
    [Id(3)] public string OrderNumber { get; init; } = "";
    [Id(4)] public OrderType Type { get; init; }
    [Id(5)] public Guid? TableId { get; init; }
    [Id(6)] public string? TableNumber { get; init; }
    [Id(7)] public Guid? CustomerId { get; init; }
    [Id(8)] public int GuestCount { get; init; }
    [Id(9)] public Guid CreatedBy { get; init; }
    [Id(10)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OrderLineAdded : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public Guid MenuItemId { get; init; }
    [Id(3)] public string Name { get; init; } = "";
    [Id(4)] public int Quantity { get; init; }
    [Id(5)] public decimal UnitPrice { get; init; }
    [Id(6)] public decimal LineTotal { get; init; }
    [Id(7)] public string? Notes { get; init; }
    [Id(8)] public List<OrderLineModifier> Modifiers { get; init; } = [];
    [Id(9)] public DateTime OccurredAt { get; init; }
    /// <summary>
    /// Tax rate as a percentage (e.g., 10.0 for 10% tax).
    /// </summary>
    [Id(10)] public decimal TaxRate { get; init; }
    /// <summary>
    /// Calculated tax amount for this line.
    /// </summary>
    [Id(11)] public decimal TaxAmount { get; init; }

    /// <summary>
    /// Whether this line is a bundle/combo item.
    /// </summary>
    [Id(12)] public bool IsBundle { get; init; }

    /// <summary>
    /// Selected components for bundle items.
    /// </summary>
    [Id(13)] public List<OrderLineBundleComponent> BundleComponents { get; init; } = [];

    /// <summary>
    /// Optional seat number for seat-based ordering.
    /// </summary>
    [Id(14)] public int? Seat { get; init; }
}

[GenerateSerializer]
public sealed record OrderLineUpdated : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public int? Quantity { get; init; }
    [Id(3)] public string? Notes { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OrderLineVoided : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public Guid VoidedBy { get; init; }
    [Id(3)] public string Reason { get; init; } = "";
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OrderLineRemoved : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OrderSent : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid SentBy { get; init; }
    [Id(2)] public List<Guid> SentLineIds { get; init; } = [];
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OrderDiscountApplied : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid DiscountInstanceId { get; init; }
    [Id(2)] public Guid? DiscountId { get; init; }
    [Id(3)] public string Name { get; init; } = "";
    [Id(4)] public DiscountType Type { get; init; }
    [Id(5)] public decimal Value { get; init; }
    [Id(6)] public decimal Amount { get; init; }
    [Id(7)] public Guid AppliedBy { get; init; }
    [Id(8)] public string? Reason { get; init; }
    [Id(9)] public Guid? ApprovedBy { get; init; }
    [Id(10)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OrderDiscountRemoved : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid DiscountInstanceId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OrderServiceChargeAdded : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid ServiceChargeId { get; init; }
    [Id(2)] public string Name { get; init; } = "";
    [Id(3)] public decimal Rate { get; init; }
    [Id(4)] public decimal Amount { get; init; }
    [Id(5)] public bool IsTaxable { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OrderCustomerAssigned : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid CustomerId { get; init; }
    [Id(2)] public string? CustomerName { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OrderServerAssigned : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid ServerId { get; init; }
    [Id(2)] public string ServerName { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OrderTableTransferred : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid NewTableId { get; init; }
    [Id(2)] public string NewTableNumber { get; init; } = "";
    [Id(3)] public Guid TransferredBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OrderPaymentRecorded : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid PaymentId { get; init; }
    [Id(2)] public decimal Amount { get; init; }
    [Id(3)] public decimal TipAmount { get; init; }
    [Id(4)] public string Method { get; init; } = "";
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OrderPaymentRemoved : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid PaymentId { get; init; }
    [Id(2)] public decimal Amount { get; init; }
    [Id(3)] public decimal TipAmount { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OrderClosed : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid ClosedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OrderVoided : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid VoidedBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OrderReopened : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid ReopenedBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event when an order is split by moving items to a new order.
/// Recorded on the source order.
/// </summary>
[GenerateSerializer]
public sealed record OrderSplitByItems : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid NewOrderId { get; init; }
    [Id(2)] public string NewOrderNumber { get; init; } = "";
    [Id(3)] public List<Guid> MovedLineIds { get; init; } = [];
    [Id(4)] public Guid SplitBy { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event when an order is created from splitting another order.
/// Recorded on the new child order.
/// </summary>
[GenerateSerializer]
public sealed record OrderCreatedFromSplit : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid SiteId { get; init; }
    [Id(3)] public string OrderNumber { get; init; } = "";
    [Id(4)] public OrderType Type { get; init; }
    [Id(5)] public Guid ParentOrderId { get; init; }
    [Id(6)] public string ParentOrderNumber { get; init; } = "";
    [Id(7)] public Guid? TableId { get; init; }
    [Id(8)] public string? TableNumber { get; init; }
    [Id(9)] public Guid? CustomerId { get; init; }
    [Id(10)] public int GuestCount { get; init; }
    [Id(11)] public Guid CreatedBy { get; init; }
    [Id(12)] public List<OrderLine> Lines { get; init; } = [];
    [Id(13)] public DateTime OccurredAt { get; init; }
}

#region Hold/Fire Events

/// <summary>
/// Event when items are put on hold, preventing them from being sent to the kitchen.
/// </summary>
[GenerateSerializer]
public sealed record OrderItemsHeld : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public List<Guid> LineIds { get; init; } = [];
    [Id(2)] public Guid HeldBy { get; init; }
    [Id(3)] public string? Reason { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event when items are released from hold (unhold).
/// </summary>
[GenerateSerializer]
public sealed record OrderItemsReleased : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public List<Guid> LineIds { get; init; } = [];
    [Id(2)] public Guid ReleasedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event when a course number is assigned to items.
/// </summary>
[GenerateSerializer]
public sealed record OrderItemsCourseSet : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public List<Guid> LineIds { get; init; } = [];
    [Id(2)] public int CourseNumber { get; init; }
    [Id(3)] public Guid SetBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event when items are fired to the kitchen (explicitly sent, bypassing hold).
/// </summary>
[GenerateSerializer]
public sealed record OrderItemsFired : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public List<Guid> LineIds { get; init; } = [];
    [Id(2)] public Guid FiredBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event when all items in a specific course are fired to the kitchen.
/// </summary>
[GenerateSerializer]
public sealed record OrderCourseFired : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public int CourseNumber { get; init; }
    [Id(2)] public List<Guid> FiredLineIds { get; init; } = [];
    [Id(3)] public Guid FiredBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event when all held items are fired at once.
/// </summary>
[GenerateSerializer]
public sealed record OrderAllItemsFired : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public List<Guid> FiredLineIds { get; init; } = [];
    [Id(2)] public Guid FiredBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

#endregion

#region Seat Assignment Events

/// <summary>
/// Event when a seat is assigned to a line item.
/// </summary>
[GenerateSerializer]
public sealed record SeatAssigned : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public int SeatNumber { get; init; }
    [Id(3)] public Guid AssignedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

#endregion

#region Line-Level Discount Events

/// <summary>
/// Event when a discount is applied to a specific line item.
/// </summary>
[GenerateSerializer]
public sealed record LineDiscountApplied : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public DiscountType DiscountType { get; init; }
    [Id(3)] public decimal Value { get; init; }
    [Id(4)] public decimal Amount { get; init; }
    [Id(5)] public string? Reason { get; init; }
    [Id(6)] public Guid AppliedBy { get; init; }
    [Id(7)] public Guid? ApprovedBy { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event when a line-level discount is removed.
/// </summary>
[GenerateSerializer]
public sealed record LineDiscountRemoved : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public Guid RemovedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

#endregion

#region Price Override Events

/// <summary>
/// Event when a price is overridden on a line item.
/// </summary>
[GenerateSerializer]
public sealed record PriceOverridden : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public decimal OriginalPrice { get; init; }
    [Id(3)] public decimal NewPrice { get; init; }
    [Id(4)] public string Reason { get; init; } = "";
    [Id(5)] public Guid OverriddenBy { get; init; }
    [Id(6)] public Guid? ApprovedBy { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

#endregion

#region Order Merge Events

/// <summary>
/// Event when another order is merged into this order.
/// Recorded on the target (surviving) order.
/// </summary>
[GenerateSerializer]
public sealed record OrderMerged : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid SourceOrderId { get; init; }
    [Id(2)] public string SourceOrderNumber { get; init; } = "";
    [Id(3)] public List<OrderLine> MergedLines { get; init; } = [];
    [Id(4)] public List<OrderDiscount> MergedDiscounts { get; init; } = [];
    [Id(5)] public List<OrderPaymentSummary> MergedPayments { get; init; } = [];
    [Id(6)] public Guid MergedBy { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event when an order is closed because it was merged into another order.
/// Recorded on the source (merged-away) order.
/// </summary>
[GenerateSerializer]
public sealed record OrderMergedAway : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid TargetOrderId { get; init; }
    [Id(2)] public string TargetOrderNumber { get; init; } = "";
    [Id(3)] public Guid MergedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

#endregion
