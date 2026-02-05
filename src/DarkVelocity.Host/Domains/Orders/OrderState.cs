namespace DarkVelocity.Host.State;

public enum OrderStatus
{
    Open,
    Sent,
    PartiallyPaid,
    Paid,
    Closed,
    Voided
}

public enum OrderType
{
    DineIn,
    TakeOut,
    Delivery,
    DriveThru,
    Online,
    Tab
}

[GenerateSerializer]
public record OrderLine
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public Guid MenuItemId { get; init; }
    [Id(2)] public string Name { get; init; } = string.Empty;
    [Id(3)] public string? Sku { get; init; }
    [Id(4)] public int Quantity { get; init; }
    [Id(5)] public decimal UnitPrice { get; init; }
    [Id(6)] public decimal LineTotal { get; init; }
    [Id(7)] public decimal DiscountAmount { get; init; }
    [Id(8)] public decimal TaxAmount { get; init; }
    [Id(9)] public string? Notes { get; init; }
    [Id(10)] public List<OrderLineModifier> Modifiers { get; init; } = [];
    [Id(11)] public Guid? SentBy { get; init; }
    [Id(12)] public DateTime? SentAt { get; init; }
    [Id(13)] public OrderLineStatus Status { get; init; }
    [Id(14)] public DateTime CreatedAt { get; init; }
    [Id(15)] public Guid? VoidedBy { get; init; }
    [Id(16)] public DateTime? VoidedAt { get; init; }
    [Id(17)] public string? VoidReason { get; init; }
    /// <summary>
    /// Tax rate as a percentage (e.g., 10.0 for 10% tax).
    /// Varies by order type (dine-in, takeout, delivery) and item category.
    /// </summary>
    [Id(18)] public decimal TaxRate { get; init; }

    /// <summary>
    /// Whether this line is a bundle/combo item.
    /// </summary>
    [Id(19)] public bool IsBundle { get; init; }

    /// <summary>
    /// Selected components for bundle items (e.g., side, drink choices).
    /// Empty for non-bundle items.
    /// </summary>
    [Id(20)] public List<OrderLineBundleComponent> BundleComponents { get; init; } = [];

    // Hold/Fire workflow fields

    /// <summary>
    /// Whether this item is on hold (not to be sent to kitchen yet).
    /// </summary>
    [Id(21)] public bool IsHeld { get; init; }

    /// <summary>
    /// When the item was placed on hold.
    /// </summary>
    [Id(22)] public DateTime? HeldAt { get; init; }

    /// <summary>
    /// Who placed the item on hold.
    /// </summary>
    [Id(23)] public Guid? HeldBy { get; init; }

    /// <summary>
    /// Reason for holding the item (e.g., "Wait for guest", "Course timing").
    /// </summary>
    [Id(24)] public string? HoldReason { get; init; }

    /// <summary>
    /// Course number for coursed dining (1 = appetizer, 2 = main, 3 = dessert, etc.).
    /// Default is 1.
    /// </summary>
    [Id(25)] public int CourseNumber { get; init; } = 1;

    /// <summary>
    /// When the item was explicitly fired to the kitchen.
    /// </summary>
    [Id(26)] public DateTime? FiredAt { get; init; }

    /// <summary>
    /// Who fired the item to the kitchen.
    /// </summary>
    [Id(27)] public Guid? FiredBy { get; init; }

    /// <summary>
    /// Seat number for seat-based ordering (e.g., seat 1, seat 2).
    /// Null if not assigned to a specific seat.
    /// </summary>
    [Id(28)] public int? Seat { get; init; }

    /// <summary>
    /// Line-level discount amount (separate from order-level discounts).
    /// </summary>
    [Id(29)] public decimal LineDiscountAmount { get; init; }

    /// <summary>
    /// Reason for line-level discount.
    /// </summary>
    [Id(30)] public string? LineDiscountReason { get; init; }

    /// <summary>
    /// Type of line-level discount applied.
    /// </summary>
    [Id(31)] public DiscountType? LineDiscountType { get; init; }

    /// <summary>
    /// Who approved the line-level discount (manager approval).
    /// </summary>
    [Id(32)] public Guid? LineDiscountApprovedBy { get; init; }

    /// <summary>
    /// Original price before any override.
    /// </summary>
    [Id(33)] public decimal? OriginalPrice { get; init; }

    /// <summary>
    /// Reason for price override.
    /// </summary>
    [Id(34)] public string? PriceOverrideReason { get; init; }

    /// <summary>
    /// Who approved the price override (manager approval).
    /// </summary>
    [Id(35)] public Guid? PriceOverrideApprovedBy { get; init; }
}

public enum OrderLineStatus
{
    Pending,
    Sent,
    Preparing,
    Ready,
    Served,
    Voided
}

[GenerateSerializer]
public record OrderLineModifier
{
    [Id(0)] public Guid ModifierId { get; init; }
    [Id(1)] public string Name { get; init; } = string.Empty;
    [Id(2)] public decimal Price { get; init; }
    [Id(3)] public int Quantity { get; init; }
}

/// <summary>
/// A selected component within a bundle order line.
/// Tracks which item was chosen for a bundle slot.
/// </summary>
[GenerateSerializer]
public record OrderLineBundleComponent
{
    /// <summary>The slot ID from the bundle definition.</summary>
    [Id(0)] public string SlotId { get; init; } = string.Empty;

    /// <summary>The slot name (e.g., "Choose your side").</summary>
    [Id(1)] public string SlotName { get; init; } = string.Empty;

    /// <summary>The selected menu item document ID.</summary>
    [Id(2)] public string ItemDocumentId { get; init; } = string.Empty;

    /// <summary>The selected item name.</summary>
    [Id(3)] public string ItemName { get; init; } = string.Empty;

    /// <summary>Quantity of this component selected.</summary>
    [Id(4)] public int Quantity { get; init; } = 1;

    /// <summary>Price adjustment for this selection (e.g., upgrade fee).</summary>
    [Id(5)] public decimal PriceAdjustment { get; init; }

    /// <summary>Modifiers applied to this specific component.</summary>
    [Id(6)] public List<OrderLineModifier> Modifiers { get; init; } = [];
}

[GenerateSerializer]
public record OrderDiscount
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public Guid? DiscountId { get; init; }
    [Id(2)] public string Name { get; init; } = string.Empty;
    [Id(3)] public DiscountType Type { get; init; }
    [Id(4)] public decimal Value { get; init; }
    [Id(5)] public decimal Amount { get; init; }
    [Id(6)] public Guid AppliedBy { get; init; }
    [Id(7)] public DateTime AppliedAt { get; init; }
    [Id(8)] public string? Reason { get; init; }
    [Id(9)] public Guid? ApprovedBy { get; init; }
}

public enum DiscountType
{
    Percentage,
    FixedAmount,
    Voucher,
    Loyalty,
    PriceOverride,
    QuantityBreak,
    Comp,
    Employee,
    Promotion
}

[GenerateSerializer]
public record ServiceCharge
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public string Name { get; init; } = string.Empty;
    [Id(2)] public decimal Rate { get; init; }
    [Id(3)] public decimal Amount { get; init; }
    [Id(4)] public bool IsTaxable { get; init; }
}

[GenerateSerializer]
public record OrderPaymentSummary
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public decimal Amount { get; init; }
    [Id(2)] public decimal TipAmount { get; init; }
    [Id(3)] public string Method { get; init; } = string.Empty;
    [Id(4)] public DateTime PaidAt { get; init; }
}

/// <summary>
/// Reference to a child order created from a bill split.
/// </summary>
[GenerateSerializer]
public record SplitOrderReference
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public string OrderNumber { get; init; } = string.Empty;
    [Id(2)] public List<Guid> LineIds { get; init; } = [];
    [Id(3)] public DateTime SplitAt { get; init; }
}

/// <summary>
/// Represents a calculated split share for payment splitting.
/// </summary>
[GenerateSerializer]
public record SplitShare
{
    [Id(0)] public int ShareNumber { get; init; }
    [Id(1)] public decimal Amount { get; init; }
    [Id(2)] public decimal Tax { get; init; }
    [Id(3)] public decimal Total { get; init; }
    [Id(4)] public string? Label { get; init; }
}

[GenerateSerializer]
public sealed class OrderState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public string OrderNumber { get; set; } = string.Empty;
    [Id(4)] public OrderStatus Status { get; set; } = OrderStatus.Open;
    [Id(5)] public OrderType Type { get; set; }
    [Id(6)] public Guid? TableId { get; set; }
    [Id(7)] public string? TableNumber { get; set; }
    [Id(8)] public Guid? ServerId { get; set; }
    [Id(9)] public string? ServerName { get; set; }
    [Id(10)] public Guid? CustomerId { get; set; }
    [Id(11)] public string? CustomerName { get; set; }
    [Id(12)] public int GuestCount { get; set; } = 1;

    [Id(13)] public List<OrderLine> Lines { get; set; } = [];
    [Id(14)] public List<OrderDiscount> Discounts { get; set; } = [];
    [Id(15)] public List<ServiceCharge> ServiceCharges { get; set; } = [];
    [Id(16)] public List<OrderPaymentSummary> Payments { get; set; } = [];

    [Id(17)] public decimal Subtotal { get; set; }
    [Id(18)] public decimal DiscountTotal { get; set; }
    [Id(19)] public decimal ServiceChargeTotal { get; set; }
    [Id(20)] public decimal TaxTotal { get; set; }
    [Id(21)] public decimal GrandTotal { get; set; }
    [Id(22)] public decimal PaidAmount { get; set; }
    [Id(23)] public decimal BalanceDue { get; set; }
    [Id(24)] public decimal TipTotal { get; set; }

    [Id(25)] public string? Notes { get; set; }
    [Id(26)] public Guid? BookingId { get; set; }
    [Id(27)] public string? ExternalOrderId { get; set; }
    [Id(28)] public string? ExternalSource { get; set; }

    // Bill splitting tracking
    [Id(37)] public Guid? ParentOrderId { get; set; }
    [Id(38)] public string? ParentOrderNumber { get; set; }
    [Id(39)] public List<SplitOrderReference> ChildOrders { get; set; } = [];

    [Id(29)] public Guid CreatedBy { get; set; }
    [Id(30)] public DateTime CreatedAt { get; set; }
    [Id(31)] public DateTime? UpdatedAt { get; set; }
    [Id(32)] public DateTime? SentAt { get; set; }
    [Id(33)] public DateTime? ClosedAt { get; set; }
    [Id(34)] public Guid? VoidedBy { get; set; }
    [Id(35)] public DateTime? VoidedAt { get; set; }
    [Id(36)] public string? VoidReason { get; set; }

    // Version property removed - JournaledGrain provides this.Version automatically

    /// <summary>
    /// Recalculates all totals based on current lines, discounts, and service charges.
    /// Tax is calculated per line item based on each item's tax rate.
    /// Line-level discounts are included in the discount total.
    /// </summary>
    public void RecalculateTotals()
    {
        var activeLines = Lines.Where(l => l.Status != OrderLineStatus.Voided).ToList();
        Subtotal = activeLines.Sum(l => l.LineTotal);

        // Include both order-level and line-level discounts
        var orderLevelDiscounts = Discounts.Sum(d => d.Amount);
        var lineLevelDiscounts = activeLines.Sum(l => l.LineDiscountAmount);
        DiscountTotal = orderLevelDiscounts + lineLevelDiscounts;

        ServiceChargeTotal = ServiceCharges.Sum(s => s.Amount);

        // Sum tax from each line item (each has its own rate)
        var lineTax = activeLines.Sum(l => l.TaxAmount);

        // Calculate tax on taxable service charges using weighted average rate
        decimal serviceChargeTax = 0;
        var taxableServiceCharges = ServiceCharges.Where(s => s.IsTaxable).Sum(s => s.Amount);
        if (taxableServiceCharges > 0 && Subtotal > 0)
        {
            // Use weighted average tax rate from line items for service charge tax
            var weightedTaxRate = activeLines.Sum(l => l.LineTotal * l.TaxRate) / Subtotal;
            serviceChargeTax = taxableServiceCharges * (weightedTaxRate / 100m);
        }

        TaxTotal = lineTax + serviceChargeTax;

        GrandTotal = Subtotal - DiscountTotal + ServiceChargeTotal + TaxTotal;
        BalanceDue = GrandTotal - PaidAmount;
    }
}
