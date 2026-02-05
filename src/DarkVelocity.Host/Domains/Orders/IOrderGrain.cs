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
    [property: Id(5)] List<OrderLineModifier>? Modifiers = null,
    /// <summary>
    /// Tax rate as a percentage (e.g., 10.0 for 10% tax).
    /// Should be set based on order type and menu item's contextual tax rates.
    /// Defaults to 0 (no tax) if not specified.
    /// </summary>
    [property: Id(6)] decimal TaxRate = 0,
    /// <summary>
    /// Whether this line is a bundle/combo item.
    /// </summary>
    [property: Id(7)] bool IsBundle = false,
    /// <summary>
    /// Selected components for bundle items (e.g., chosen side, drink).
    /// Required for bundle items; ignored for regular items.
    /// </summary>
    [property: Id(8)] List<OrderLineBundleComponent>? BundleComponents = null,
    /// <summary>
    /// Optional seat number for seat-based ordering.
    /// </summary>
    [property: Id(9)] int? Seat = null);

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
public record VoidOrderCommand(
    [property: Id(0)] Guid VoidedBy,
    [property: Id(1)] string Reason,
    [property: Id(2)] bool ReverseInventory = false);
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

    // Hold/Fire workflow

    /// <summary>
    /// Place specific items on hold, preventing them from being sent to the kitchen.
    /// Held items must be explicitly fired before they are sent.
    /// </summary>
    Task HoldItemsAsync(HoldItemsCommand command);

    /// <summary>
    /// Release items from hold, returning them to normal pending status.
    /// Released items can be sent to kitchen via SendAsync.
    /// </summary>
    Task ReleaseItemsAsync(ReleaseItemsCommand command);

    /// <summary>
    /// Set the course number for specific items.
    /// Course numbers are used for coursed dining workflows.
    /// </summary>
    Task SetItemCourseAsync(SetItemCourseCommand command);

    /// <summary>
    /// Fire specific items to the kitchen immediately.
    /// Items are removed from hold (if held) and sent to kitchen.
    /// </summary>
    Task<FireResult> FireItemsAsync(FireItemsCommand command);

    /// <summary>
    /// Fire all items in a specific course to the kitchen.
    /// </summary>
    Task<FireResult> FireCourseAsync(FireCourseCommand command);

    /// <summary>
    /// Fire all held items to the kitchen at once.
    /// </summary>
    Task<FireResult> FireAllAsync(Guid firedBy);

    /// <summary>
    /// Get summary of items currently on hold.
    /// </summary>
    Task<HoldSummary> GetHoldSummaryAsync();

    /// <summary>
    /// Get all held line items.
    /// </summary>
    Task<IReadOnlyList<OrderLine>> GetHeldItemsAsync();

    /// <summary>
    /// Get distinct course numbers for this order with item counts.
    /// </summary>
    Task<Dictionary<int, int>> GetCourseSummaryAsync();

    // Seat Assignment

    /// <summary>
    /// Assign a seat number to a specific line item.
    /// </summary>
    Task AssignSeatAsync(AssignSeatCommand command);

    // Line-Level Discounts

    /// <summary>
    /// Apply a discount to a specific line item.
    /// </summary>
    Task ApplyLineDiscountAsync(ApplyLineDiscountCommand command);

    /// <summary>
    /// Remove a line-level discount from a specific line item.
    /// </summary>
    Task RemoveLineDiscountAsync(Guid lineId, Guid removedBy);

    // Price Overrides

    /// <summary>
    /// Override the price of a specific line item.
    /// </summary>
    Task OverridePriceAsync(OverridePriceCommand command);

    // Order Merging

    /// <summary>
    /// Merge another order into this order.
    /// Transfers all lines, payments, and discounts from the source order.
    /// The source order will be closed as merged.
    /// </summary>
    Task<MergeOrderResult> MergeFromOrderAsync(MergeFromOrderCommand command);

    /// <summary>
    /// Mark this order as merged into another order.
    /// Called on the source order when being merged.
    /// </summary>
    Task MarkAsMergedAsync(Guid targetOrderId, string targetOrderNumber, Guid mergedBy);
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

#region Hold/Fire Commands and Results

/// <summary>
/// Command to place specific items on hold.
/// </summary>
[GenerateSerializer]
public record HoldItemsCommand(
    [property: Id(0)] List<Guid> LineIds,
    [property: Id(1)] Guid HeldBy,
    [property: Id(2)] string? Reason = null);

/// <summary>
/// Command to release items from hold.
/// </summary>
[GenerateSerializer]
public record ReleaseItemsCommand(
    [property: Id(0)] List<Guid> LineIds,
    [property: Id(1)] Guid ReleasedBy);

/// <summary>
/// Command to set the course number for items.
/// </summary>
[GenerateSerializer]
public record SetItemCourseCommand(
    [property: Id(0)] List<Guid> LineIds,
    [property: Id(1)] int CourseNumber,
    [property: Id(2)] Guid SetBy);

/// <summary>
/// Command to fire specific items to the kitchen.
/// </summary>
[GenerateSerializer]
public record FireItemsCommand(
    [property: Id(0)] List<Guid> LineIds,
    [property: Id(1)] Guid FiredBy);

/// <summary>
/// Command to fire all items in a specific course.
/// </summary>
[GenerateSerializer]
public record FireCourseCommand(
    [property: Id(0)] int CourseNumber,
    [property: Id(1)] Guid FiredBy);

/// <summary>
/// Result of a fire operation.
/// </summary>
[GenerateSerializer]
public record FireResult(
    [property: Id(0)] int FiredCount,
    [property: Id(1)] List<Guid> FiredLineIds,
    [property: Id(2)] DateTime FiredAt);

/// <summary>
/// Summary of held items by course.
/// </summary>
[GenerateSerializer]
public record HoldSummary(
    [property: Id(0)] int TotalHeldCount,
    [property: Id(1)] Dictionary<int, int> HeldByCourseCounts,
    [property: Id(2)] List<Guid> HeldLineIds);

#endregion

#region Seat Assignment Commands

/// <summary>
/// Command to assign a seat to a line item.
/// </summary>
[GenerateSerializer]
public record AssignSeatCommand(
    [property: Id(0)] Guid LineId,
    [property: Id(1)] int SeatNumber,
    [property: Id(2)] Guid AssignedBy);

#endregion

#region Line Discount Commands and Results

/// <summary>
/// Command to apply a discount to a specific line item.
/// </summary>
[GenerateSerializer]
public record ApplyLineDiscountCommand(
    [property: Id(0)] Guid LineId,
    [property: Id(1)] DiscountType DiscountType,
    [property: Id(2)] decimal Value,
    [property: Id(3)] Guid AppliedBy,
    [property: Id(4)] string? Reason = null,
    [property: Id(5)] Guid? ApprovedBy = null);

#endregion

#region Price Override Commands

/// <summary>
/// Command to override the price of a line item.
/// </summary>
[GenerateSerializer]
public record OverridePriceCommand(
    [property: Id(0)] Guid LineId,
    [property: Id(1)] decimal NewPrice,
    [property: Id(2)] string Reason,
    [property: Id(3)] Guid OverriddenBy,
    [property: Id(4)] Guid? ApprovedBy = null);

#endregion

#region Order Merge Commands and Results

/// <summary>
/// Command to merge another order into this order.
/// </summary>
[GenerateSerializer]
public record MergeFromOrderCommand(
    [property: Id(0)] Guid SourceOrderId,
    [property: Id(1)] Guid MergedBy);

/// <summary>
/// Result of merging orders.
/// </summary>
[GenerateSerializer]
public record MergeOrderResult(
    [property: Id(0)] int LinesMerged,
    [property: Id(1)] int PaymentsMerged,
    [property: Id(2)] int DiscountsMerged,
    [property: Id(3)] decimal NewGrandTotal);

#endregion
