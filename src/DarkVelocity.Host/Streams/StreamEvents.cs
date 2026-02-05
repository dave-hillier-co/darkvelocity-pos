using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Streams;

/// <summary>
/// Base interface for all stream events.
/// </summary>
public interface IStreamEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    Guid OrganizationId { get; }
}

/// <summary>
/// Base record for stream events with common metadata.
/// </summary>
[GenerateSerializer]
public abstract record StreamEvent : IStreamEvent
{
    [Id(0)] public Guid EventId { get; init; } = Guid.NewGuid();
    [Id(1)] public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    [Id(2)] public Guid OrganizationId { get; init; }
}

#region User Stream Events

/// <summary>
/// Published when a user is created. Allows other grains (e.g., EmployeeGrain) to react.
/// </summary>
[GenerateSerializer]
public sealed record UserCreatedEvent(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] string Email,
    [property: Id(2)] string DisplayName,
    [property: Id(3)] string? FirstName,
    [property: Id(4)] string? LastName,
    [property: Id(5)] UserType Type
) : StreamEvent;

/// <summary>
/// Published when a user's profile is updated.
/// </summary>
[GenerateSerializer]
public sealed record UserUpdatedEvent(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] string? DisplayName,
    [property: Id(2)] string? FirstName,
    [property: Id(3)] string? LastName,
    [property: Id(4)] List<string> ChangedFields
) : StreamEvent;

/// <summary>
/// Published when a user's status changes (active, inactive, locked).
/// </summary>
[GenerateSerializer]
public sealed record UserStatusChangedEvent(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] UserStatus OldStatus,
    [property: Id(2)] UserStatus NewStatus,
    [property: Id(3)] string? Reason
) : StreamEvent;

/// <summary>
/// Published when a user gains site access.
/// </summary>
[GenerateSerializer]
public sealed record UserSiteAccessGrantedEvent(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] Guid SiteId
) : StreamEvent;

/// <summary>
/// Published when a user loses site access.
/// </summary>
[GenerateSerializer]
public sealed record UserSiteAccessRevokedEvent(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] Guid SiteId
) : StreamEvent;

/// <summary>
/// Published when an external OAuth identity is linked to a user.
/// </summary>
[GenerateSerializer]
public sealed record ExternalIdentityLinkedEvent(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] string Provider,
    [property: Id(2)] string ExternalId,
    [property: Id(3)] string? Email
) : StreamEvent;

/// <summary>
/// Published when an external OAuth identity is unlinked from a user.
/// </summary>
[GenerateSerializer]
public sealed record ExternalIdentityUnlinkedEvent(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] string Provider,
    [property: Id(2)] string ExternalId
) : StreamEvent;

/// <summary>
/// Published when a user logs in via OAuth.
/// </summary>
[GenerateSerializer]
public sealed record UserOAuthLoginEvent(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] string Provider,
    [property: Id(2)] string? Email
) : StreamEvent;

#endregion

#region Employee Stream Events

/// <summary>
/// Published when an employee is created.
/// </summary>
[GenerateSerializer]
public sealed record EmployeeCreatedEvent(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] Guid DefaultSiteId,
    [property: Id(3)] string EmployeeNumber,
    [property: Id(4)] string FirstName,
    [property: Id(5)] string LastName,
    [property: Id(6)] string Email,
    [property: Id(7)] EmploymentType EmploymentType,
    [property: Id(8)] DateOnly HireDate
) : StreamEvent;

/// <summary>
/// Published when an employee's details are updated.
/// </summary>
[GenerateSerializer]
public sealed record EmployeeUpdatedEvent(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] List<string> ChangedFields
) : StreamEvent;

/// <summary>
/// Published when an employee's status changes.
/// </summary>
[GenerateSerializer]
public sealed record EmployeeStatusChangedEvent(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] EmployeeStatus OldStatus,
    [property: Id(3)] EmployeeStatus NewStatus
) : StreamEvent;

/// <summary>
/// Published when an employee is terminated.
/// </summary>
[GenerateSerializer]
public sealed record EmployeeTerminatedEvent(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] DateOnly TerminationDate,
    [property: Id(3)] string? Reason
) : StreamEvent;

/// <summary>
/// Published when an employee clocks in.
/// </summary>
[GenerateSerializer]
public sealed record EmployeeClockedInEvent(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] DateTime ClockInTime,
    [property: Id(4)] Guid? ShiftId
) : StreamEvent;

/// <summary>
/// Published when an employee clocks out.
/// </summary>
[GenerateSerializer]
public sealed record EmployeeClockedOutEvent(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] DateTime ClockOutTime,
    [property: Id(4)] decimal TotalHours
) : StreamEvent;

#endregion

#region Order Stream Events

/// <summary>
/// Published when an order is created.
/// </summary>
[GenerateSerializer]
public sealed record OrderCreatedEvent(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string OrderNumber,
    [property: Id(3)] Guid? ServerId
) : StreamEvent;

/// <summary>
/// Published when a line is added to an order.
/// </summary>
[GenerateSerializer]
public sealed record OrderLineAddedEvent(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid LineId,
    [property: Id(3)] Guid ProductId,
    [property: Id(4)] string ProductName,
    [property: Id(5)] int Quantity,
    [property: Id(6)] decimal UnitPrice,
    [property: Id(7)] decimal LineTotal
) : StreamEvent;

/// <summary>
/// Published when an order is finalized/completed.
/// This is the single source of truth for order completion.
/// Subscribers: Loyalty (points), Inventory (consumption), Reporting (aggregation).
/// </summary>
[GenerateSerializer]
public sealed record OrderCompletedEvent(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string OrderNumber,
    [property: Id(3)] decimal Subtotal,
    [property: Id(4)] decimal Tax,
    [property: Id(5)] decimal Total,
    [property: Id(6)] decimal DiscountAmount,
    [property: Id(7)] List<OrderLineSnapshot> Lines,
    [property: Id(8)] Guid? ServerId,
    [property: Id(9)] string? ServerName,
    [property: Id(10)] Guid? CustomerId = null,
    [property: Id(11)] string? CustomerName = null,
    [property: Id(12)] int GuestCount = 1,
    [property: Id(13)] string Channel = "DineIn",
    [property: Id(14)] DateOnly? BusinessDate = null
) : StreamEvent;

/// <summary>
/// Published when order items are sent to the kitchen.
/// Subscriber: Kitchen (ticket creation).
/// </summary>
[GenerateSerializer]
public sealed record OrderSentToKitchenEvent(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string OrderNumber,
    [property: Id(3)] string OrderType,
    [property: Id(4)] string? TableNumber,
    [property: Id(5)] int? GuestCount,
    [property: Id(6)] Guid? ServerId,
    [property: Id(7)] string? ServerName,
    [property: Id(8)] List<KitchenLineItem> Lines,
    [property: Id(9)] string? Notes = null
) : StreamEvent;

/// <summary>
/// Line item sent to kitchen with preparation details.
/// </summary>
[GenerateSerializer]
public sealed record KitchenLineItem(
    [property: Id(0)] Guid LineId,
    [property: Id(1)] Guid MenuItemId,
    [property: Id(2)] string Name,
    [property: Id(3)] int Quantity,
    [property: Id(4)] List<string>? Modifiers = null,
    [property: Id(5)] string? SpecialInstructions = null,
    [property: Id(6)] Guid? StationId = null,
    [property: Id(7)] int CourseNumber = 1
);

/// <summary>
/// Published when specific items are fired (explicitly sent) to the kitchen.
/// This is used for hold/fire workflow where items are held and then explicitly fired.
/// Subscriber: Kitchen (ticket creation or item addition).
/// </summary>
[GenerateSerializer]
public sealed record OrderItemsFiredToKitchenEvent(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string OrderNumber,
    [property: Id(3)] string OrderType,
    [property: Id(4)] string? TableNumber,
    [property: Id(5)] int? GuestCount,
    [property: Id(6)] Guid? ServerId,
    [property: Id(7)] string? ServerName,
    [property: Id(8)] Guid FiredBy,
    [property: Id(9)] List<KitchenLineItem> Lines,
    [property: Id(10)] int? CourseNumber = null,
    [property: Id(11)] bool IsFireAll = false
) : StreamEvent;

/// <summary>
/// Published when an order is voided.
/// Subscribers: Sales (aggregation), Kitchen (ticket cancellation), Inventory (optional reversal).
/// </summary>
[GenerateSerializer]
public sealed record OrderVoidedEvent(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string OrderNumber,
    [property: Id(3)] decimal VoidedAmount,
    [property: Id(4)] string Reason,
    [property: Id(5)] Guid VoidedByUserId,
    [property: Id(6)] DateOnly? BusinessDate = null,
    [property: Id(7)] Guid? CustomerId = null,
    [property: Id(8)] bool ReverseInventory = false,
    [property: Id(9)] IReadOnlyList<OrderLineSnapshot>? Lines = null
) : StreamEvent;

/// <summary>
/// Snapshot of an order line for stream events.
/// </summary>
[GenerateSerializer]
public sealed record OrderLineSnapshot(
    [property: Id(0)] Guid LineId,
    [property: Id(1)] Guid ProductId,
    [property: Id(2)] string ProductName,
    [property: Id(3)] int Quantity,
    [property: Id(4)] decimal UnitPrice,
    [property: Id(5)] decimal LineTotal,
    [property: Id(6)] Guid? RecipeId
);

/// <summary>
/// Published when a line item is voided on an order.
/// Subscriber: Kitchen (ticket item void notification).
/// </summary>
[GenerateSerializer]
public sealed record KitchenItemVoidedEvent(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string OrderNumber,
    [property: Id(3)] Guid LineId,
    [property: Id(4)] Guid MenuItemId,
    [property: Id(5)] string ItemName,
    [property: Id(6)] int Quantity,
    [property: Id(7)] string VoidReason,
    [property: Id(8)] Guid VoidedBy
) : StreamEvent;

/// <summary>
/// Published when orders are merged.
/// Subscriber: Kitchen (ticket consolidation notification).
/// </summary>
[GenerateSerializer]
public sealed record OrdersMergedEvent(
    [property: Id(0)] Guid TargetOrderId,
    [property: Id(1)] Guid SourceOrderId,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] string TargetOrderNumber,
    [property: Id(4)] string SourceOrderNumber,
    [property: Id(5)] int LinesMerged,
    [property: Id(6)] Guid MergedBy
) : StreamEvent;

#endregion

#region Inventory Stream Events

/// <summary>
/// Published when stock is consumed (e.g., from order completion).
/// </summary>
[GenerateSerializer]
public sealed record StockConsumedEvent(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] decimal QuantityConsumed,
    [property: Id(4)] string Unit,
    [property: Id(5)] decimal CostOfGoodsConsumed,
    [property: Id(6)] decimal QuantityRemaining,
    [property: Id(7)] Guid? OrderId,
    [property: Id(8)] string Reason
) : StreamEvent;

/// <summary>
/// Published when stock is received into inventory.
/// </summary>
[GenerateSerializer]
public sealed record StockReceivedEvent(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] decimal QuantityReceived,
    [property: Id(4)] string Unit,
    [property: Id(5)] decimal UnitCost,
    [property: Id(6)] decimal TotalQuantityOnHand,
    [property: Id(7)] string? BatchNumber,
    [property: Id(8)] DateOnly? ExpiryDate,
    [property: Id(9)] Guid? SupplierId,
    [property: Id(10)] Guid? DeliveryId
) : StreamEvent;

/// <summary>
/// Published when stock quantity fell below the reorder point threshold.
/// </summary>
[GenerateSerializer]
public sealed record ReorderPointBreachedEvent(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] decimal QuantityOnHand,
    [property: Id(4)] decimal ReorderPoint,
    [property: Id(5)] decimal ParLevel,
    [property: Id(6)] decimal QuantityToOrder
) : StreamEvent;

/// <summary>
/// Published when stock was completely depleted.
/// </summary>
[GenerateSerializer]
public sealed record StockDepletedEvent(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] DateTime DepletedAt,
    [property: Id(4)] Guid? LastConsumingOrderId
) : StreamEvent;

/// <summary>
/// Published when stock was adjusted (physical count variance).
/// </summary>
[GenerateSerializer]
public sealed record StockAdjustedEvent(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] decimal PreviousQuantity,
    [property: Id(4)] decimal NewQuantity,
    [property: Id(5)] decimal Variance,
    [property: Id(6)] string Reason,
    [property: Id(7)] Guid AdjustedBy
) : StreamEvent;

/// <summary>
/// Published when stock was transferred between sites.
/// </summary>
[GenerateSerializer]
public sealed record StockTransferredEvent(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] Guid SourceSiteId,
    [property: Id(2)] Guid DestinationSiteId,
    [property: Id(3)] string IngredientName,
    [property: Id(4)] decimal QuantityTransferred,
    [property: Id(5)] string Unit,
    [property: Id(6)] decimal UnitCost,
    [property: Id(7)] Guid TransferId,
    [property: Id(8)] Guid TransferredBy
) : StreamEvent;

/// <summary>
/// Published when stock was written off (waste, spoilage, expiry).
/// </summary>
[GenerateSerializer]
public sealed record StockWrittenOffEvent(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] decimal QuantityWrittenOff,
    [property: Id(4)] decimal CostWrittenOff,
    [property: Id(5)] string WriteOffCategory,
    [property: Id(6)] string Reason,
    [property: Id(7)] Guid RecordedBy
) : StreamEvent;

/// <summary>
/// Published when a stock take is finalized.
/// </summary>
[GenerateSerializer]
public sealed record StockTakeFinalizedEvent(
    [property: Id(0)] Guid StockTakeId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string StockTakeName,
    [property: Id(3)] decimal TotalVarianceValue,
    [property: Id(4)] int ItemsAdjusted,
    [property: Id(5)] bool AdjustmentsApplied
) : StreamEvent;

/// <summary>
/// Published when an inventory transfer status changes.
/// </summary>
[GenerateSerializer]
public sealed record InventoryTransferStatusChangedEvent(
    [property: Id(0)] Guid TransferId,
    [property: Id(1)] Guid SourceSiteId,
    [property: Id(2)] Guid DestinationSiteId,
    [property: Id(3)] string Status,
    [property: Id(4)] string? Notes
) : StreamEvent;

/// <summary>
/// Published when inventory items are expiring soon.
/// </summary>
[GenerateSerializer]
public sealed record ExpiryAlertEvent(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] DateTime ExpiryDate,
    [property: Id(4)] int DaysUntilExpiry,
    [property: Id(5)] decimal QuantityAtRisk,
    [property: Id(6)] decimal ValueAtRisk
) : StreamEvent;

/// <summary>
/// Published when a reorder suggestion is generated.
/// </summary>
[GenerateSerializer]
public sealed record ReorderSuggestionGeneratedEvent(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] decimal CurrentQuantity,
    [property: Id(4)] decimal SuggestedQuantity,
    [property: Id(5)] decimal EstimatedCost,
    [property: Id(6)] Guid? PreferredSupplierId
) : StreamEvent;

#endregion

#region Sales Stream Events

/// <summary>
/// Published when a sale is recorded for aggregation.
/// </summary>
[GenerateSerializer]
public sealed record SaleRecordedEvent(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] DateOnly BusinessDate,
    [property: Id(3)] decimal GrossSales,
    [property: Id(4)] decimal DiscountAmount,
    [property: Id(5)] decimal NetSales,
    [property: Id(6)] decimal Tax,
    [property: Id(7)] decimal TheoreticalCOGS,
    [property: Id(8)] int ItemCount,
    [property: Id(9)] int GuestCount,
    [property: Id(10)] string Channel
) : StreamEvent;

/// <summary>
/// Published when a void is recorded for aggregation.
/// </summary>
[GenerateSerializer]
public sealed record VoidRecordedEvent(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] DateOnly BusinessDate,
    [property: Id(3)] decimal VoidAmount,
    [property: Id(4)] string Reason
) : StreamEvent;

#endregion

#region Alert Stream Events

/// <summary>
/// Generic alert event that can trigger notifications.
/// </summary>
[GenerateSerializer]
public sealed record AlertTriggeredEvent(
    [property: Id(0)] Guid AlertId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string AlertType,
    [property: Id(3)] string Severity,
    [property: Id(4)] string Title,
    [property: Id(5)] string Message,
    [property: Id(6)] Dictionary<string, string> Metadata
) : StreamEvent;

#endregion

#region Booking Stream Events

/// <summary>
/// Published when a booking was created.
/// </summary>
[GenerateSerializer]
public sealed record BookingCreatedEvent(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid? CustomerId,
    [property: Id(3)] string CustomerName,
    [property: Id(4)] DateTime BookingDateTime,
    [property: Id(5)] int PartySize,
    [property: Id(6)] decimal? DepositRequired,
    [property: Id(7)] DateTime? DepositDueBy,
    [property: Id(8)] Guid CreatedBy
) : StreamEvent;

/// <summary>
/// Published when a booking deposit is required.
/// </summary>
[GenerateSerializer]
public sealed record BookingDepositRequiredEvent(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid? CustomerId,
    [property: Id(3)] decimal AmountRequired,
    [property: Id(4)] DateTime RequiredBy
) : StreamEvent;

/// <summary>
/// Published when a booking deposit was paid.
/// Triggers: Debit Cash, Credit Deposits Payable liability.
/// </summary>
[GenerateSerializer]
public sealed record BookingDepositPaidEvent(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid? CustomerId,
    [property: Id(3)] decimal AmountPaid,
    [property: Id(4)] string PaymentMethod,
    [property: Id(5)] string? PaymentReference,
    [property: Id(6)] decimal TotalDepositPaid
) : StreamEvent;

/// <summary>
/// Published when a booking deposit was refunded.
/// Triggers: Debit Deposits Payable, Credit Cash.
/// </summary>
[GenerateSerializer]
public sealed record BookingDepositRefundedEvent(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid? CustomerId,
    [property: Id(3)] decimal AmountRefunded,
    [property: Id(4)] string Reason,
    [property: Id(5)] Guid RefundedBy
) : StreamEvent;

/// <summary>
/// Published when a booking deposit was forfeited (no-show, late cancellation).
/// Triggers: Debit Deposits Payable, Credit Other Income.
/// </summary>
[GenerateSerializer]
public sealed record BookingDepositForfeitedEvent(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid? CustomerId,
    [property: Id(3)] decimal AmountForfeited,
    [property: Id(4)] string ForfeitureReason,
    [property: Id(5)] Guid ProcessedBy
) : StreamEvent;

/// <summary>
/// Published when a booking deposit was applied to the final bill.
/// Triggers: Debit Deposits Payable, Credit AR/Sales.
/// </summary>
[GenerateSerializer]
public sealed record BookingDepositAppliedToOrderEvent(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid OrderId,
    [property: Id(3)] Guid? CustomerId,
    [property: Id(4)] decimal AmountApplied,
    [property: Id(5)] decimal RemainingDeposit
) : StreamEvent;

/// <summary>
/// Published when a booking was confirmed.
/// </summary>
[GenerateSerializer]
public sealed record BookingConfirmedEvent(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid? CustomerId,
    [property: Id(3)] DateTime BookingDateTime,
    [property: Id(4)] string? ConfirmationMethod
) : StreamEvent;

/// <summary>
/// Published when a booking was cancelled.
/// </summary>
[GenerateSerializer]
public sealed record BookingCancelledEvent(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid? CustomerId,
    [property: Id(3)] string CancellationReason,
    [property: Id(4)] Guid? CancelledBy,
    [property: Id(5)] decimal? DepositToRefund,
    [property: Id(6)] decimal? DepositToForfeit
) : StreamEvent;

/// <summary>
/// Published when a booking party was seated.
/// </summary>
[GenerateSerializer]
public sealed record BookingSeatedEvent(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid? CustomerId,
    [property: Id(3)] string? TableNumber,
    [property: Id(4)] int ActualPartySize,
    [property: Id(5)] DateTime SeatedAt,
    [property: Id(6)] Guid? OrderId
) : StreamEvent;

/// <summary>
/// Published when a booking was marked as no-show.
/// </summary>
[GenerateSerializer]
public sealed record BookingNoShowEvent(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid? CustomerId,
    [property: Id(3)] DateTime OriginalBookingTime,
    [property: Id(4)] decimal? DepositToForfeit,
    [property: Id(5)] Guid MarkedBy
) : StreamEvent;

#endregion

#region Gift Card Stream Events

/// <summary>
/// Published when a gift card is activated (sold).
/// Triggers: Debit Cash, Credit Gift Card Liability.
/// </summary>
[GenerateSerializer]
public sealed record GiftCardActivatedEvent(
    [property: Id(0)] Guid CardId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string CardNumber,
    [property: Id(3)] decimal Amount,
    [property: Id(4)] Guid? PurchasedByCustomerId,
    [property: Id(5)] Guid? OrderId
) : StreamEvent;

/// <summary>
/// Published when a gift card is reloaded.
/// Triggers: Debit Cash, Credit Gift Card Liability.
/// </summary>
[GenerateSerializer]
public sealed record GiftCardReloadedEvent(
    [property: Id(0)] Guid CardId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string CardNumber,
    [property: Id(3)] decimal Amount,
    [property: Id(4)] decimal NewBalance,
    [property: Id(5)] Guid? OrderId
) : StreamEvent;

/// <summary>
/// Published when a gift card is redeemed.
/// Triggers: Debit Gift Card Liability, Credit Sales Revenue.
/// </summary>
[GenerateSerializer]
public sealed record GiftCardRedeemedEvent(
    [property: Id(0)] Guid CardId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string CardNumber,
    [property: Id(3)] decimal Amount,
    [property: Id(4)] decimal RemainingBalance,
    [property: Id(5)] Guid OrderId,
    [property: Id(6)] Guid? CustomerId
) : StreamEvent;

/// <summary>
/// Published when a gift card expires with remaining balance.
/// Triggers: Debit Gift Card Liability, Credit Breakage Income.
/// </summary>
[GenerateSerializer]
public sealed record GiftCardExpiredEvent(
    [property: Id(0)] Guid CardId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string CardNumber,
    [property: Id(3)] decimal ExpiredBalance
) : StreamEvent;

/// <summary>
/// Published when a refund is applied to a gift card.
/// Triggers: Debit Refunds Expense, Credit Gift Card Liability.
/// </summary>
[GenerateSerializer]
public sealed record GiftCardRefundAppliedEvent(
    [property: Id(0)] Guid CardId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string CardNumber,
    [property: Id(3)] decimal Amount,
    [property: Id(4)] decimal NewBalance,
    [property: Id(5)] Guid OriginalOrderId,
    [property: Id(6)] string? Reason
) : StreamEvent;

#endregion

#region Customer Spend Stream Events

/// <summary>
/// Published when a customer completes a purchase.
/// Used for loyalty projection - cumulative spend tracking.
/// </summary>
[GenerateSerializer]
public sealed record CustomerSpendRecordedEvent(
    [property: Id(0)] Guid CustomerId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid OrderId,
    [property: Id(3)] decimal NetSpend,
    [property: Id(4)] decimal GrossSpend,
    [property: Id(5)] decimal DiscountAmount,
    [property: Id(6)] decimal TaxAmount,
    [property: Id(7)] int ItemCount,
    [property: Id(8)] DateOnly TransactionDate
) : StreamEvent;

/// <summary>
/// Published when a customer's spend is reversed (void/refund).
/// </summary>
[GenerateSerializer]
public sealed record CustomerSpendReversedEvent(
    [property: Id(0)] Guid CustomerId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid OrderId,
    [property: Id(3)] decimal ReversedAmount,
    [property: Id(4)] string Reason
) : StreamEvent;

/// <summary>
/// Published when a customer's loyalty tier changes based on spend.
/// </summary>
[GenerateSerializer]
public sealed record CustomerTierChangedEvent(
    [property: Id(0)] Guid CustomerId,
    [property: Id(1)] string OldTier,
    [property: Id(2)] string NewTier,
    [property: Id(3)] decimal CumulativeSpend,
    [property: Id(4)] decimal SpendToNextTier
) : StreamEvent;

/// <summary>
/// Published when loyalty points are calculated from spend.
/// Points = NetSpend × PointsPerDollar × TierMultiplier
/// </summary>
[GenerateSerializer]
public sealed record LoyaltyPointsEarnedEvent(
    [property: Id(0)] Guid CustomerId,
    [property: Id(1)] Guid OrderId,
    [property: Id(2)] decimal SpendAmount,
    [property: Id(3)] int PointsEarned,
    [property: Id(4)] int TotalPoints,
    [property: Id(5)] string CurrentTier,
    [property: Id(6)] decimal TierMultiplier
) : StreamEvent;

/// <summary>
/// Published when loyalty points are redeemed.
/// Triggers: Debit Loyalty Liability, Credit Discount Applied.
/// </summary>
[GenerateSerializer]
public sealed record LoyaltyPointsRedeemedEvent(
    [property: Id(0)] Guid CustomerId,
    [property: Id(1)] Guid OrderId,
    [property: Id(2)] int PointsRedeemed,
    [property: Id(3)] decimal DiscountValue,
    [property: Id(4)] int RemainingPoints,
    [property: Id(5)] string RewardType
) : StreamEvent;

#endregion

#region Accounting Stream Events

/// <summary>
/// Published when a journal entry is created.
/// </summary>
[GenerateSerializer]
public sealed record JournalEntryCreatedEvent(
    [property: Id(0)] Guid EntryId,
    [property: Id(1)] Guid AccountId,
    [property: Id(2)] string AccountCode,
    [property: Id(3)] string AccountName,
    [property: Id(4)] bool IsDebit,
    [property: Id(5)] decimal Amount,
    [property: Id(6)] decimal BalanceAfter,
    [property: Id(7)] string Description,
    [property: Id(8)] string? ReferenceType,
    [property: Id(9)] Guid? ReferenceId,
    [property: Id(10)] Guid PerformedBy
) : StreamEvent;

#endregion

#region Payment Stream Events

/// <summary>
/// Published when a payment is initiated.
/// </summary>
[GenerateSerializer]
public sealed record PaymentInitiatedEvent(
    [property: Id(0)] Guid PaymentId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid OrderId,
    [property: Id(3)] decimal Amount,
    [property: Id(4)] string Method,
    [property: Id(5)] Guid? CustomerId,
    [property: Id(6)] Guid CashierId
) : StreamEvent;

/// <summary>
/// Published when a payment is completed (cash, card, or gift card).
/// Triggers: Debit Cash/AR, Credit Sales Revenue.
/// For gift card payments, triggers redemption on the gift card.
/// </summary>
[GenerateSerializer]
public sealed record PaymentCompletedEvent(
    [property: Id(0)] Guid PaymentId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid OrderId,
    [property: Id(3)] decimal Amount,
    [property: Id(4)] decimal TipAmount,
    [property: Id(5)] decimal TotalAmount,
    [property: Id(6)] string Method,
    [property: Id(7)] Guid? CustomerId,
    [property: Id(8)] Guid CashierId,
    [property: Id(9)] Guid? DrawerId,
    [property: Id(10)] string? GatewayReference,
    [property: Id(11)] string? CardLastFour,
    [property: Id(12)] Guid? GiftCardId = null
) : StreamEvent;

/// <summary>
/// Published when a payment is refunded.
/// Triggers: Debit Refund Expense, Credit Cash/AR.
/// For gift card payments, triggers refund credit on the gift card.
/// </summary>
[GenerateSerializer]
public sealed record PaymentRefundedEvent(
    [property: Id(0)] Guid PaymentId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid OrderId,
    [property: Id(3)] Guid RefundId,
    [property: Id(4)] decimal RefundAmount,
    [property: Id(5)] decimal TotalRefundedAmount,
    [property: Id(6)] string Method,
    [property: Id(7)] string? Reason,
    [property: Id(8)] Guid IssuedBy,
    [property: Id(9)] Guid? GiftCardId = null
) : StreamEvent;

/// <summary>
/// Published when a payment is voided.
/// Reverses the original accounting entries.
/// </summary>
[GenerateSerializer]
public sealed record PaymentVoidedEvent(
    [property: Id(0)] Guid PaymentId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid OrderId,
    [property: Id(3)] decimal VoidedAmount,
    [property: Id(4)] string Method,
    [property: Id(5)] string? Reason,
    [property: Id(6)] Guid VoidedBy
) : StreamEvent;

#endregion

#region Customer Lifecycle Stream Events

/// <summary>
/// Published when a customer account was created.
/// </summary>
[GenerateSerializer]
public sealed record CustomerCreatedEvent(
    [property: Id(0)] Guid CustomerId,
    [property: Id(1)] string DisplayName,
    [property: Id(2)] string? Email,
    [property: Id(3)] string? Phone,
    [property: Id(4)] string Source,
    [property: Id(5)] Guid? ReferredByCustomerId
) : StreamEvent;

/// <summary>
/// Published when a customer profile was updated.
/// </summary>
[GenerateSerializer]
public sealed record CustomerProfileUpdatedEvent(
    [property: Id(0)] Guid CustomerId,
    [property: Id(1)] string DisplayName,
    [property: Id(2)] List<string> ChangedFields,
    [property: Id(3)] Guid? UpdatedBy
) : StreamEvent;

/// <summary>
/// Published when a customer enrolled in a loyalty program.
/// </summary>
[GenerateSerializer]
public sealed record CustomerEnrolledInLoyaltyEvent(
    [property: Id(0)] Guid CustomerId,
    [property: Id(1)] Guid ProgramId,
    [property: Id(2)] string MemberNumber,
    [property: Id(3)] Guid InitialTierId,
    [property: Id(4)] string TierName,
    [property: Id(5)] int InitialPointsBalance
) : StreamEvent;

/// <summary>
/// Published when a tag was added to a customer.
/// </summary>
[GenerateSerializer]
public sealed record CustomerTagAddedEvent(
    [property: Id(0)] Guid CustomerId,
    [property: Id(1)] string Tag,
    [property: Id(2)] Guid? AddedBy
) : StreamEvent;

/// <summary>
/// Published when a tag was removed from a customer.
/// </summary>
[GenerateSerializer]
public sealed record CustomerTagRemovedEvent(
    [property: Id(0)] Guid CustomerId,
    [property: Id(1)] string Tag,
    [property: Id(2)] Guid? RemovedBy
) : StreamEvent;

/// <summary>
/// Published when a customer visited a site.
/// </summary>
[GenerateSerializer]
public sealed record CustomerVisitedEvent(
    [property: Id(0)] Guid CustomerId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid? OrderId,
    [property: Id(3)] decimal SpendAmount,
    [property: Id(4)] int VisitNumber,
    [property: Id(5)] decimal LifetimeSpend,
    [property: Id(6)] int DaysSinceLastVisit
) : StreamEvent;

/// <summary>
/// Published when a customer's segment classification changed.
/// </summary>
[GenerateSerializer]
public sealed record CustomerSegmentChangedEvent(
    [property: Id(0)] Guid CustomerId,
    [property: Id(1)] string PreviousSegment,
    [property: Id(2)] string NewSegment,
    [property: Id(3)] decimal LifetimeSpend,
    [property: Id(4)] int TotalVisits
) : StreamEvent;

/// <summary>
/// Published when a customer account was deactivated.
/// </summary>
[GenerateSerializer]
public sealed record CustomerDeactivatedEvent(
    [property: Id(0)] Guid CustomerId,
    [property: Id(1)] string Reason,
    [property: Id(2)] Guid? DeactivatedBy
) : StreamEvent;

/// <summary>
/// Published when a customer account was merged into another.
/// </summary>
[GenerateSerializer]
public sealed record CustomerMergedEvent(
    [property: Id(0)] Guid SourceCustomerId,
    [property: Id(1)] Guid TargetCustomerId,
    [property: Id(2)] decimal CombinedLifetimeSpend,
    [property: Id(3)] int CombinedVisits,
    [property: Id(4)] Guid MergedBy
) : StreamEvent;

#endregion

#region Device Authentication Stream Events

/// <summary>
/// Published when a device authorization code is requested.
/// </summary>
[GenerateSerializer]
public sealed record DeviceCodeRequestedEvent(
    [property: Id(0)] string UserCode,
    [property: Id(1)] string ClientId,
    [property: Id(2)] string? IpAddress,
    [property: Id(3)] DateTime ExpiresAt
) : StreamEvent;

/// <summary>
/// Published when a device is authorized.
/// </summary>
[GenerateSerializer]
public sealed record DeviceAuthorizedEvent(
    [property: Id(0)] Guid DeviceId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string DeviceName,
    [property: Id(3)] string AppType,
    [property: Id(4)] Guid AuthorizedBy
) : StreamEvent;

/// <summary>
/// Published when a device authorization is denied.
/// </summary>
[GenerateSerializer]
public sealed record DeviceAuthorizationDeniedEvent(
    [property: Id(0)] string UserCode,
    [property: Id(1)] string Reason,
    [property: Id(2)] Guid? DeniedBy
) : StreamEvent;

/// <summary>
/// Published when a device is revoked.
/// </summary>
[GenerateSerializer]
public sealed record DeviceRevokedEvent(
    [property: Id(0)] Guid DeviceId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string Reason,
    [property: Id(3)] Guid RevokedBy
) : StreamEvent;

/// <summary>
/// Published when a user session starts on a device (PIN login).
/// </summary>
[GenerateSerializer]
public sealed record DeviceSessionStartedEvent(
    [property: Id(0)] Guid DeviceId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid UserId,
    [property: Id(3)] string AuthMethod
) : StreamEvent;

/// <summary>
/// Published when a user session ends on a device (logout/switch user).
/// </summary>
[GenerateSerializer]
public sealed record DeviceSessionEndedEvent(
    [property: Id(0)] Guid DeviceId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid UserId,
    [property: Id(3)] string? Reason
) : StreamEvent;

#endregion

#region Notification Stream Events

/// <summary>
/// Published when a notification is queued for sending.
/// </summary>
[GenerateSerializer]
public sealed record NotificationQueuedEvent(
    [property: Id(0)] Guid NotificationId,
    [property: Id(1)] string NotificationType,
    [property: Id(2)] string Recipient,
    [property: Id(3)] string Subject,
    [property: Id(4)] Guid? TriggeredByAlertId
) : StreamEvent;

/// <summary>
/// Published when a notification was sent successfully.
/// </summary>
[GenerateSerializer]
public sealed record NotificationSentEvent(
    [property: Id(0)] Guid NotificationId,
    [property: Id(1)] string NotificationType,
    [property: Id(2)] string Recipient,
    [property: Id(3)] string? ExternalMessageId
) : StreamEvent;

/// <summary>
/// Published when a notification failed to send.
/// </summary>
[GenerateSerializer]
public sealed record NotificationFailedEvent(
    [property: Id(0)] Guid NotificationId,
    [property: Id(1)] string NotificationType,
    [property: Id(2)] string Recipient,
    [property: Id(3)] string ErrorMessage,
    [property: Id(4)] string? ErrorCode
) : StreamEvent;

/// <summary>
/// Published when a failed notification is being retried.
/// </summary>
[GenerateSerializer]
public sealed record NotificationRetriedEvent(
    [property: Id(0)] Guid NotificationId,
    [property: Id(1)] string NotificationType,
    [property: Id(2)] string Recipient,
    [property: Id(3)] int RetryAttempt
) : StreamEvent;

#endregion

#region Webhook Stream Events

/// <summary>
/// Published when a webhook delivery is attempted.
/// </summary>
[GenerateSerializer]
public sealed record WebhookDeliveryAttemptedEvent(
    [property: Id(0)] Guid WebhookId,
    [property: Id(1)] Guid DeliveryId,
    [property: Id(2)] string EventType,
    [property: Id(3)] string Url,
    [property: Id(4)] int AttemptNumber
) : StreamEvent;

/// <summary>
/// Published when a webhook delivery succeeded.
/// </summary>
[GenerateSerializer]
public sealed record WebhookDeliverySucceededEvent(
    [property: Id(0)] Guid WebhookId,
    [property: Id(1)] Guid DeliveryId,
    [property: Id(2)] string EventType,
    [property: Id(3)] int StatusCode,
    [property: Id(4)] int ResponseTimeMs
) : StreamEvent;

/// <summary>
/// Published when a webhook delivery failed.
/// </summary>
[GenerateSerializer]
public sealed record WebhookDeliveryFailedEvent(
    [property: Id(0)] Guid WebhookId,
    [property: Id(1)] Guid DeliveryId,
    [property: Id(2)] string EventType,
    [property: Id(3)] int? StatusCode,
    [property: Id(4)] string ErrorMessage,
    [property: Id(5)] int AttemptNumber,
    [property: Id(6)] bool WillRetry
) : StreamEvent;

/// <summary>
/// Published when a webhook endpoint is disabled due to too many failures.
/// </summary>
[GenerateSerializer]
public sealed record WebhookEndpointDisabledEvent(
    [property: Id(0)] Guid WebhookId,
    [property: Id(1)] string Url,
    [property: Id(2)] int ConsecutiveFailures,
    [property: Id(3)] string Reason
) : StreamEvent;

#endregion

#region Scheduled Job Stream Events

/// <summary>
/// Published when a scheduled job is scheduled.
/// </summary>
[GenerateSerializer]
public sealed record JobScheduledEvent(
    [property: Id(0)] Guid JobId,
    [property: Id(1)] string JobName,
    [property: Id(2)] string Schedule,
    [property: Id(3)] DateTime NextRunAt
) : StreamEvent;

/// <summary>
/// Published when a scheduled job starts execution.
/// </summary>
[GenerateSerializer]
public sealed record JobStartedEvent(
    [property: Id(0)] Guid JobId,
    [property: Id(1)] string JobName,
    [property: Id(2)] Guid ExecutionId
) : StreamEvent;

/// <summary>
/// Published when a scheduled job completes execution.
/// </summary>
[GenerateSerializer]
public sealed record JobCompletedEvent(
    [property: Id(0)] Guid JobId,
    [property: Id(1)] string JobName,
    [property: Id(2)] Guid ExecutionId,
    [property: Id(3)] bool Success,
    [property: Id(4)] string? ErrorMessage,
    [property: Id(5)] int DurationMs
) : StreamEvent;

/// <summary>
/// Published when a scheduled job is cancelled.
/// </summary>
[GenerateSerializer]
public sealed record JobCancelledEvent(
    [property: Id(0)] Guid JobId,
    [property: Id(1)] string JobName,
    [property: Id(2)] Guid? CancelledBy,
    [property: Id(3)] string? Reason
) : StreamEvent;

#endregion
