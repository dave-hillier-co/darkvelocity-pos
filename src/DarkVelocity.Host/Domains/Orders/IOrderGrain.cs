using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record CreateOrderCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid CreatedBy,
    [property: Id(3)] OrderType Type,
    [property: Id(4)] Guid? TableId = null,
    [property: Id(5)] string? TableNumber = null,
    [property: Id(6)] Guid? CustomerId = null,
    [property: Id(7)] int GuestCount = 1);

[GenerateSerializer]
public record AddLineCommand(
    [property: Id(0)] Guid MenuItemId,
    [property: Id(1)] string Name,
    [property: Id(2)] int Quantity,
    [property: Id(3)] decimal UnitPrice,
    [property: Id(4)] string? Notes = null,
    [property: Id(5)] List<OrderLineModifier>? Modifiers = null);

[GenerateSerializer]
public record UpdateLineCommand(
    [property: Id(0)] Guid LineId,
    [property: Id(1)] int? Quantity = null,
    [property: Id(2)] string? Notes = null);

[GenerateSerializer]
public record ApplyDiscountCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] DiscountType Type,
    [property: Id(2)] decimal Value,
    [property: Id(3)] Guid AppliedBy,
    [property: Id(4)] Guid? DiscountId = null,
    [property: Id(5)] string? Reason = null,
    [property: Id(6)] Guid? ApprovedBy = null);

[GenerateSerializer]
public record VoidOrderCommand([property: Id(0)] Guid VoidedBy, [property: Id(1)] string Reason);
[GenerateSerializer]
public record VoidLineCommand([property: Id(0)] Guid LineId, [property: Id(1)] Guid VoidedBy, [property: Id(2)] string Reason);

[GenerateSerializer]
public record OrderCreatedResult([property: Id(0)] Guid Id, [property: Id(1)] string OrderNumber, [property: Id(2)] DateTime CreatedAt);
[GenerateSerializer]
public record AddLineResult([property: Id(0)] Guid LineId, [property: Id(1)] decimal LineTotal, [property: Id(2)] decimal GrandTotal);
[GenerateSerializer]
public record OrderTotals(
    [property: Id(0)] decimal Subtotal,
    [property: Id(1)] decimal DiscountTotal,
    [property: Id(2)] decimal ServiceChargeTotal,
    [property: Id(3)] decimal TaxTotal,
    [property: Id(4)] decimal GrandTotal,
    [property: Id(5)] decimal PaidAmount,
    [property: Id(6)] decimal BalanceDue);

public interface IOrderGrain : IGrainWithStringKey
{
    Task<OrderCreatedResult> CreateAsync(CreateOrderCommand command);
    Task<OrderState> GetStateAsync();

    // Line management
    Task<AddLineResult> AddLineAsync(AddLineCommand command);
    Task UpdateLineAsync(UpdateLineCommand command);
    Task VoidLineAsync(VoidLineCommand command);
    Task RemoveLineAsync(Guid lineId);

    // Order operations
    Task SendAsync(Guid sentBy);
    Task<OrderTotals> RecalculateTotalsAsync();
    Task ApplyDiscountAsync(ApplyDiscountCommand command);
    Task RemoveDiscountAsync(Guid discountId);
    Task AddServiceChargeAsync(string name, decimal rate, bool isTaxable);

    // Customer
    Task AssignCustomerAsync(Guid customerId, string? customerName);
    Task AssignServerAsync(Guid serverId, string serverName);
    Task TransferTableAsync(Guid newTableId, string newTableNumber, Guid transferredBy);

    // Payment recording
    Task RecordPaymentAsync(Guid paymentId, decimal amount, decimal tipAmount, string method);
    Task RemovePaymentAsync(Guid paymentId);

    // Completion
    Task CloseAsync(Guid closedBy);
    Task VoidAsync(VoidOrderCommand command);
    Task ReopenAsync(Guid reopenedBy, string reason);

    // Queries
    Task<bool> ExistsAsync();
    Task<OrderStatus> GetStatusAsync();
    Task<OrderTotals> GetTotalsAsync();
    Task<IReadOnlyList<OrderLine>> GetLinesAsync();

    // Clone
    /// <summary>
    /// Clone this order to create a new order with the same line items.
    /// Similar to Square's POST /v2/orders/clone
    /// </summary>
    Task<CloneOrderResult> CloneAsync(CloneOrderCommand command);

    // Bill Splitting
    /// <summary>
    /// Split specific line items into a new order that can be paid independently.
    /// </summary>
    Task<SplitByItemsResult> SplitByItemsAsync(SplitByItemsCommand command);

    /// <summary>
    /// Initialize this order as a child order created from a split.
    /// Called on the new order grain to set up parent relationship and lines.
    /// </summary>
    Task<OrderCreatedResult> CreateFromSplitAsync(CreateFromSplitCommand command);

    /// <summary>
    /// Calculate equal payment splits for a given number of people.
    /// Returns the share amounts without modifying the order.
    /// </summary>
    Task<SplitPaymentResult> CalculateSplitByPeopleAsync(int numberOfPeople);

    /// <summary>
    /// Calculate custom payment splits by specified amounts.
    /// Validates that amounts sum to the balance due.
    /// Returns the share details without modifying the order.
    /// </summary>
    Task<SplitPaymentResult> CalculateSplitByAmountsAsync(List<decimal> amounts);
}

[GenerateSerializer]
public record CloneOrderCommand(
    [property: Id(0)] Guid CreatedBy,
    [property: Id(1)] OrderType? NewType = null,
    [property: Id(2)] Guid? NewTableId = null,
    [property: Id(3)] string? NewTableNumber = null,
    [property: Id(4)] bool IncludeDiscounts = false,
    [property: Id(5)] bool IncludeServiceCharges = false);

[GenerateSerializer]
public record CloneOrderResult(
    [property: Id(0)] Guid NewOrderId,
    [property: Id(1)] string NewOrderNumber,
    [property: Id(2)] int LinesCloned);

// Bill Splitting Commands and Results
[GenerateSerializer]
public record SplitByItemsCommand(
    [property: Id(0)] List<Guid> LineIds,
    [property: Id(1)] Guid SplitBy,
    [property: Id(2)] int? GuestCount = null);

[GenerateSerializer]
public record SplitByItemsResult(
    [property: Id(0)] Guid NewOrderId,
    [property: Id(1)] string NewOrderNumber,
    [property: Id(2)] int LinesMoved,
    [property: Id(3)] decimal NewOrderTotal,
    [property: Id(4)] decimal RemainingOrderTotal);

[GenerateSerializer]
public record CreateFromSplitCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid ParentOrderId,
    [property: Id(3)] string ParentOrderNumber,
    [property: Id(4)] OrderType Type,
    [property: Id(5)] List<OrderLine> Lines,
    [property: Id(6)] Guid CreatedBy,
    [property: Id(7)] Guid? TableId = null,
    [property: Id(8)] string? TableNumber = null,
    [property: Id(9)] Guid? CustomerId = null,
    [property: Id(10)] int GuestCount = 1);

[GenerateSerializer]
public record SplitPaymentResult(
    [property: Id(0)] decimal TotalAmount,
    [property: Id(1)] decimal BalanceDue,
    [property: Id(2)] List<SplitShare> Shares,
    [property: Id(3)] bool IsValid);
