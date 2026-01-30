namespace DarkVelocity.Orleans.Abstractions.State;

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

public record OrderLine
{
    public Guid Id { get; init; }
    public Guid MenuItemId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Sku { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public string? Notes { get; init; }
    public List<OrderLineModifier> Modifiers { get; init; } = [];
    public Guid? SentBy { get; init; }
    public DateTime? SentAt { get; init; }
    public OrderLineStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid? VoidedBy { get; init; }
    public DateTime? VoidedAt { get; init; }
    public string? VoidReason { get; init; }
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

public record OrderLineModifier
{
    public Guid ModifierId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Quantity { get; init; }
}

public record OrderDiscount
{
    public Guid Id { get; init; }
    public Guid? DiscountId { get; init; }
    public string Name { get; init; } = string.Empty;
    public DiscountType Type { get; init; }
    public decimal Value { get; init; }
    public decimal Amount { get; init; }
    public Guid AppliedBy { get; init; }
    public DateTime AppliedAt { get; init; }
    public string? Reason { get; init; }
    public Guid? ApprovedBy { get; init; }
}

public enum DiscountType
{
    Percentage,
    FixedAmount,
    Voucher,
    Loyalty
}

public record ServiceCharge
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Rate { get; init; }
    public decimal Amount { get; init; }
    public bool IsTaxable { get; init; }
}

public record OrderPaymentSummary
{
    public Guid PaymentId { get; init; }
    public decimal Amount { get; init; }
    public decimal TipAmount { get; init; }
    public string Method { get; init; } = string.Empty;
    public DateTime PaidAt { get; init; }
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

    [Id(37)] public int Version { get; set; }
}
