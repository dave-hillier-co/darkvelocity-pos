using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

public record CreateOrderRequest(
    Guid CreatedBy,
    OrderType Type,
    Guid? TableId = null,
    string? TableNumber = null,
    Guid? CustomerId = null,
    int GuestCount = 1);

public record AddLineRequest(
    Guid MenuItemId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    string? Notes = null,
    List<OrderLineModifier>? Modifiers = null);

public record SendOrderRequest(Guid SentBy);
public record CloseOrderRequest(Guid ClosedBy);
public record VoidOrderRequest(Guid VoidedBy, string Reason);

public record ApplyDiscountRequest(
    string Name,
    DiscountType Type,
    decimal Value,
    Guid AppliedBy,
    Guid? DiscountId = null,
    string? Reason = null,
    Guid? ApprovedBy = null);

// Bill Splitting Requests
public record SplitByItemsRequest(
    List<Guid> LineIds,
    Guid SplitBy,
    int? GuestCount = null);

public record SplitByAmountsRequest(
    List<decimal> Amounts);

// Bill Splitting Responses
public record SplitByItemsResponse(
    Guid NewOrderId,
    string NewOrderNumber,
    int LinesMoved,
    decimal NewOrderTotal,
    decimal RemainingOrderTotal);

public record SplitPaymentResponse(
    decimal TotalAmount,
    decimal BalanceDue,
    List<SplitShareResponse> Shares,
    bool IsValid);

public record SplitShareResponse(
    int ShareNumber,
    decimal Amount,
    decimal Tax,
    decimal Total,
    string? Label);
