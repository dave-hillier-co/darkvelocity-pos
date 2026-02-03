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
    /// </summary>
    public void RecalculateTotals()
    {
        var activeLines = Lines.Where(l => l.Status != OrderLineStatus.Voided);
        Subtotal = activeLines.Sum(l => l.LineTotal);
        DiscountTotal = Discounts.Sum(d => d.Amount);
        ServiceChargeTotal = ServiceCharges.Sum(s => s.Amount);

        // Calculate tax (simplified - 10% tax rate)
        var taxableAmount = Subtotal - DiscountTotal;
        taxableAmount += ServiceCharges.Where(s => s.IsTaxable).Sum(s => s.Amount);
        TaxTotal = taxableAmount * 0.10m;

        GrandTotal = Subtotal - DiscountTotal + ServiceChargeTotal + TaxTotal;
        BalanceDue = GrandTotal - PaidAmount;
    }
}
