namespace DarkVelocity.Shared.Contracts.Events;

// ============================================================================
// Sale Item Events
// ============================================================================

/// <summary>
/// Sale item added to check with full financial breakdown.
/// </summary>
public sealed record SaleItemAdded : DomainEvent
{
    public override string EventType => "sales.item.added";
    public override string AggregateType => "Check";
    public override Guid AggregateId => CheckId;

    public required Guid CheckId { get; init; }
    public required Guid ItemId { get; init; }
    public required Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public required string Category { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal LineTotal { get; init; }
    public required IReadOnlyList<SaleModifier> Modifiers { get; init; }
    public required IReadOnlyList<SaleDiscount> Discounts { get; init; }
    public required decimal ModifierTotal { get; init; }
    public required decimal DiscountTotal { get; init; }
    public required decimal NetAmount { get; init; }
    public required SaleChannel Channel { get; init; }
    public required DayPart DayPart { get; init; }
    public Guid? RecipeVersionId { get; init; }
    public Guid? ServerId { get; init; }
    public string? ServerName { get; init; }
    public int? SeatNumber { get; init; }
    public string? Notes { get; init; }
}

public sealed record SaleModifier
{
    public required Guid ModifierId { get; init; }
    public required string Name { get; init; }
    public required decimal Price { get; init; }
    public required int Quantity { get; init; }
    public required decimal Total { get; init; }
}

public sealed record SaleDiscount
{
    public required Guid DiscountId { get; init; }
    public required string Name { get; init; }
    public required DiscountType Type { get; init; }
    public required decimal Value { get; init; }
    public required decimal Amount { get; init; }
    public string? Reason { get; init; }
    public Guid? ApprovedBy { get; init; }
}

public enum DiscountType
{
    Percentage,
    FixedAmount,
    PriceOverride,
    QuantityBreak,
    Comp,
    Employee,
    Loyalty,
    Promotion
}

public enum SaleChannel
{
    DineIn,
    TakeOut,
    Delivery,
    DriveThru,
    Catering,
    Online,
    ThirdParty,
    RoomService
}

public enum DayPart
{
    Breakfast,
    Lunch,
    Afternoon,
    Dinner,
    LateNight
}

/// <summary>
/// Sale item voided from check.
/// </summary>
public sealed record SaleItemVoided : DomainEvent
{
    public override string EventType => "sales.item.voided";
    public override string AggregateType => "Check";
    public override Guid AggregateId => CheckId;

    public required Guid CheckId { get; init; }
    public required Guid ItemId { get; init; }
    public required Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public required int Quantity { get; init; }
    public required decimal RefundAmount { get; init; }
    public required VoidReason Reason { get; init; }
    public required string ReasonDetails { get; init; }
    public required Guid VoidedBy { get; init; }
    public required Guid ApprovedBy { get; init; }
    public required bool InventoryReturned { get; init; }
}

public enum VoidReason
{
    CustomerRequest,
    IncorrectOrder,
    QualityIssue,
    PrepError,
    SystemError,
    Duplicate,
    Other
}

/// <summary>
/// Sale item comped (complimentary).
/// </summary>
public sealed record SaleItemComped : DomainEvent
{
    public override string EventType => "sales.item.comped";
    public override string AggregateType => "Check";
    public override Guid AggregateId => CheckId;

    public required Guid CheckId { get; init; }
    public required Guid ItemId { get; init; }
    public required Guid ProductId { get; init; }
    public required decimal OriginalAmount { get; init; }
    public required decimal CompAmount { get; init; }
    public required CompReason Reason { get; init; }
    public required string ReasonDetails { get; init; }
    public required Guid CompedBy { get; init; }
    public required Guid ApprovedBy { get; init; }
}

public enum CompReason
{
    ServiceRecovery,
    QualityIssue,
    VIP,
    Manager,
    Employee,
    Marketing,
    WaitTime,
    Birthday,
    Other
}

// ============================================================================
// Sale Finalization Events
// ============================================================================

/// <summary>
/// Sale/Check finalized with full financial breakdown.
/// </summary>
public sealed record SaleFinalized : DomainEvent
{
    public override string EventType => "sales.check.finalized";
    public override string AggregateType => "Check";
    public override Guid AggregateId => CheckId;

    public required Guid CheckId { get; init; }
    public required string CheckNumber { get; init; }
    public required SaleChannel Channel { get; init; }

    // Amounts
    public required decimal Subtotal { get; init; }
    public required decimal DiscountTotal { get; init; }
    public required decimal TaxTotal { get; init; }
    public required decimal ServiceChargeTotal { get; init; }
    public required decimal GratuityTotal { get; init; }
    public required decimal GrandTotal { get; init; }

    // Tender breakdown
    public required IReadOnlyList<SaleTender> Tenders { get; init; }
    public required decimal TotalTendered { get; init; }
    public required decimal ChangeGiven { get; init; }

    // Staff
    public required Guid ServerId { get; init; }
    public required string ServerName { get; init; }
    public Guid? TableId { get; init; }
    public string? TableName { get; init; }

    // Covers/Guest info
    public required int GuestCount { get; init; }
    public required int ItemCount { get; init; }

    // Timing
    public required DateTime OpenedAt { get; init; }
    public required DateTime ClosedAt { get; init; }
    public required int DurationMinutes { get; init; }

    // Items summary
    public required IReadOnlyList<SaleLineItem> Lines { get; init; }

    // COGS data
    public required decimal TheoreticalCOGS { get; init; }
    public decimal? ActualCOGS { get; init; }
}

public sealed record SaleTender
{
    public required Guid TenderId { get; init; }
    public required TenderType Type { get; init; }
    public required decimal Amount { get; init; }
    public required decimal Tip { get; init; }
    public string? Reference { get; init; }
    public string? CardLastFour { get; init; }
    public string? CardBrand { get; init; }
}

public enum TenderType
{
    Cash,
    CreditCard,
    DebitCard,
    GiftCard,
    AccountCharge,
    MobilePayment,
    RoomCharge,
    Voucher,
    LoyaltyPoints,
    External
}

public sealed record SaleLineItem
{
    public required Guid LineId { get; init; }
    public required Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public required string Category { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal GrossAmount { get; init; }
    public required decimal Discounts { get; init; }
    public required decimal Tax { get; init; }
    public required decimal NetAmount { get; init; }
    public required decimal TheoreticalCost { get; init; }
    public Guid? RecipeVersionId { get; init; }
    public bool IsVoided { get; init; }
    public bool IsComped { get; init; }
}

/// <summary>
/// Sale reopened after being finalized.
/// </summary>
public sealed record SaleReopened : DomainEvent
{
    public override string EventType => "sales.check.reopened";
    public override string AggregateType => "Check";
    public override Guid AggregateId => CheckId;

    public required Guid CheckId { get; init; }
    public required string Reason { get; init; }
    public required Guid ReopenedBy { get; init; }
    public required Guid ApprovedBy { get; init; }
}

/// <summary>
/// Sale fully voided.
/// </summary>
public sealed record SaleVoided : DomainEvent
{
    public override string EventType => "sales.check.voided";
    public override string AggregateType => "Check";
    public override Guid AggregateId => CheckId;

    public required Guid CheckId { get; init; }
    public required string CheckNumber { get; init; }
    public required decimal VoidedAmount { get; init; }
    public required VoidReason Reason { get; init; }
    public required string ReasonDetails { get; init; }
    public required Guid VoidedBy { get; init; }
    public required Guid ApprovedBy { get; init; }
    public required bool RefundProcessed { get; init; }
    public required bool InventoryReturned { get; init; }
}

/// <summary>
/// Refund processed on a sale.
/// </summary>
public sealed record SaleRefunded : DomainEvent
{
    public override string EventType => "sales.check.refunded";
    public override string AggregateType => "Check";
    public override Guid AggregateId => CheckId;

    public required Guid CheckId { get; init; }
    public required Guid RefundId { get; init; }
    public required decimal RefundAmount { get; init; }
    public required IReadOnlyList<RefundLineItem> Lines { get; init; }
    public required RefundReason Reason { get; init; }
    public required string ReasonDetails { get; init; }
    public required TenderType RefundMethod { get; init; }
    public string? RefundReference { get; init; }
    public required Guid RefundedBy { get; init; }
    public required Guid ApprovedBy { get; init; }
}

public sealed record RefundLineItem
{
    public required Guid OriginalLineId { get; init; }
    public required Guid ProductId { get; init; }
    public required int Quantity { get; init; }
    public required decimal Amount { get; init; }
}

public enum RefundReason
{
    CustomerDissatisfaction,
    QualityIssue,
    IncorrectOrder,
    Overcharge,
    CancelledOrder,
    Other
}

// ============================================================================
// Revenue Events (for daily reconciliation)
// ============================================================================

/// <summary>
/// Daily revenue recorded for a site.
/// </summary>
public sealed record DailyRevenueRecorded : DomainEvent
{
    public override string EventType => "sales.revenue.daily_recorded";
    public override string AggregateType => "Site";
    public override Guid AggregateId => SiteId;

    public required DateTime BusinessDate { get; init; }
    public required decimal GrossSales { get; init; }
    public required decimal Discounts { get; init; }
    public required decimal Voids { get; init; }
    public required decimal Comps { get; init; }
    public required decimal NetSales { get; init; }
    public required decimal Tax { get; init; }
    public required decimal ServiceCharges { get; init; }
    public required decimal Gratuities { get; init; }
    public required decimal TotalRevenue { get; init; }
    public required int TransactionCount { get; init; }
    public required int GuestCount { get; init; }
    public required decimal AverageTicket { get; init; }
    public required decimal RevenuePerCover { get; init; }
    public required IReadOnlyList<RevenueBySaleChannel> ByChannel { get; init; }
    public required IReadOnlyList<RevenueByDayPart> ByDayPart { get; init; }
}

public sealed record RevenueBySaleChannel
{
    public required SaleChannel Channel { get; init; }
    public required decimal NetSales { get; init; }
    public required int TransactionCount { get; init; }
}

public sealed record RevenueByDayPart
{
    public required DayPart DayPart { get; init; }
    public required decimal NetSales { get; init; }
    public required int TransactionCount { get; init; }
}
