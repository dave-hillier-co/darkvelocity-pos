namespace DarkVelocity.Host.Events;

// ============================================================================
// Purchase Order Events
// ============================================================================

/// <summary>
/// Purchase order created.
/// </summary>
public sealed record PurchaseOrderCreated : DomainEvent
{
    public override string EventType => "procurement.order.created";
    public override string AggregateType => "PurchaseOrder";
    public override Guid AggregateId => PurchaseOrderId;

    public required Guid PurchaseOrderId { get; init; }
    public required string OrderNumber { get; init; }
    public required Guid SupplierId { get; init; }
    public required string SupplierName { get; init; }
    public required IReadOnlyList<PurchaseOrderLine> Lines { get; init; }
    public required decimal TotalAmount { get; init; }
    public required DateTime ExpectedDeliveryDate { get; init; }
    public required Guid CreatedBy { get; init; }
    public string? Notes { get; init; }
}

public sealed record PurchaseOrderLine
{
    public required Guid LineId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required string Sku { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal LineTotal { get; init; }
}

/// <summary>
/// Purchase order submitted to supplier.
/// </summary>
public sealed record PurchaseOrderSubmitted : DomainEvent
{
    public override string EventType => "procurement.order.submitted";
    public override string AggregateType => "PurchaseOrder";
    public override Guid AggregateId => PurchaseOrderId;

    public required Guid PurchaseOrderId { get; init; }
    public required Guid SupplierId { get; init; }
    public required DateTime SubmittedAt { get; init; }
    public required Guid SubmittedBy { get; init; }
    public string? SubmissionReference { get; init; }
}

/// <summary>
/// Purchase order acknowledged by supplier.
/// </summary>
public sealed record PurchaseOrderAcknowledged : DomainEvent
{
    public override string EventType => "procurement.order.acknowledged";
    public override string AggregateType => "PurchaseOrder";
    public override Guid AggregateId => PurchaseOrderId;

    public required Guid PurchaseOrderId { get; init; }
    public required Guid SupplierId { get; init; }
    public required DateTime AcknowledgedAt { get; init; }
    public DateTime? ConfirmedDeliveryDate { get; init; }
    public string? SupplierReference { get; init; }
}

/// <summary>
/// Purchase order cancelled.
/// </summary>
public sealed record PurchaseOrderCancelled : DomainEvent
{
    public override string EventType => "procurement.order.cancelled";
    public override string AggregateType => "PurchaseOrder";
    public override Guid AggregateId => PurchaseOrderId;

    public required Guid PurchaseOrderId { get; init; }
    public required string Reason { get; init; }
    public required Guid CancelledBy { get; init; }
}

// ============================================================================
// Invoice Events
// ============================================================================

/// <summary>
/// Invoice received from supplier.
/// </summary>
public sealed record InvoiceReceived : DomainEvent
{
    public override string EventType => "procurement.invoice.received";
    public override string AggregateType => "Invoice";
    public override Guid AggregateId => InvoiceId;

    public required Guid InvoiceId { get; init; }
    public required string InvoiceNumber { get; init; }
    public required Guid SupplierId { get; init; }
    public required string SupplierName { get; init; }
    public Guid? PurchaseOrderId { get; init; }
    public Guid? DeliveryId { get; init; }
    public required DateTime InvoiceDate { get; init; }
    public required DateTime DueDate { get; init; }
    public required IReadOnlyList<InvoiceLine> Lines { get; init; }
    public required decimal SubTotal { get; init; }
    public required decimal TaxTotal { get; init; }
    public required decimal GrandTotal { get; init; }
    public required Guid RecordedBy { get; init; }
}

public sealed record InvoiceLine
{
    public required Guid LineId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal LineTotal { get; init; }
    public Guid? PurchaseOrderLineId { get; init; }
    public Guid? DeliveryLineId { get; init; }
}

/// <summary>
/// Invoice approved for payment.
/// </summary>
public sealed record InvoiceApproved : DomainEvent
{
    public override string EventType => "procurement.invoice.approved";
    public override string AggregateType => "Invoice";
    public override Guid AggregateId => InvoiceId;

    public required Guid InvoiceId { get; init; }
    public required Guid ApprovedBy { get; init; }
    public required DateTime ApprovedAt { get; init; }
}

/// <summary>
/// Invoice disputed.
/// </summary>
public sealed record InvoiceDisputed : DomainEvent
{
    public override string EventType => "procurement.invoice.disputed";
    public override string AggregateType => "Invoice";
    public override Guid AggregateId => InvoiceId;

    public required Guid InvoiceId { get; init; }
    public required string DisputeReason { get; init; }
    public required IReadOnlyList<DisputedLine> DisputedLines { get; init; }
    public required decimal DisputedAmount { get; init; }
    public required Guid DisputedBy { get; init; }
}

public sealed record DisputedLine
{
    public required Guid LineId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string Reason { get; init; }
    public required decimal ExpectedAmount { get; init; }
    public required decimal InvoicedAmount { get; init; }
    public required decimal Variance { get; init; }
}

// ============================================================================
// Three-Way Match Events
// ============================================================================

/// <summary>
/// Three-way match performed (PO, Delivery, Invoice).
/// </summary>
public sealed record ThreeWayMatchPerformed : DomainEvent
{
    public override string EventType => "procurement.match.performed";
    public override string AggregateType => "Match";
    public override Guid AggregateId => MatchId;

    public required Guid MatchId { get; init; }
    public required Guid InvoiceId { get; init; }
    public required Guid PurchaseOrderId { get; init; }
    public required Guid DeliveryId { get; init; }
    public required MatchStatus Status { get; init; }

    // Quantity comparisons
    public required decimal POQuantity { get; init; }
    public required decimal DeliveryQuantity { get; init; }
    public required decimal InvoiceQuantity { get; init; }

    // Amount comparisons
    public required decimal POAmount { get; init; }
    public required decimal DeliveryAmount { get; init; }
    public required decimal InvoiceAmount { get; init; }

    public required IReadOnlyList<MatchDiscrepancy>? Discrepancies { get; init; }
    public required Guid PerformedBy { get; init; }
}

public enum MatchStatus
{
    ExactMatch,
    WithinTolerance,
    QuantityMismatch,
    PriceMismatch,
    RequiresReview,
    Disputed
}

public sealed record MatchDiscrepancy
{
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required DiscrepancyType Type { get; init; }
    public required decimal ExpectedValue { get; init; }
    public required decimal ActualValue { get; init; }
    public required decimal Variance { get; init; }
    public required decimal VariancePercent { get; init; }
}

public enum DiscrepancyType
{
    QuantityShort,
    QuantityOver,
    PriceHigher,
    PriceLower,
    MissingItem,
    ExtraItem
}

/// <summary>
/// Match discrepancy resolved.
/// </summary>
public sealed record MatchDiscrepancyResolved : DomainEvent
{
    public override string EventType => "procurement.match.discrepancy_resolved";
    public override string AggregateType => "Match";
    public override Guid AggregateId => MatchId;

    public required Guid MatchId { get; init; }
    public required Guid DiscrepancyId { get; init; }
    public required ResolutionType Resolution { get; init; }
    public required string Notes { get; init; }
    public decimal? CreditAmount { get; init; }
    public required Guid ResolvedBy { get; init; }
}

public enum ResolutionType
{
    CreditReceived,
    QuantityAdjusted,
    PriceAccepted,
    ReturnArranged,
    WrittenOff,
    Disputed
}

// ============================================================================
// Supplier Performance Events
// ============================================================================

/// <summary>
/// Delivery performance recorded for supplier.
/// </summary>
public sealed record DeliveryPerformanceRecorded : DomainEvent
{
    public override string EventType => "procurement.supplier.delivery_performance";
    public override string AggregateType => "Supplier";
    public override Guid AggregateId => SupplierId;

    public required Guid SupplierId { get; init; }
    public required string SupplierName { get; init; }
    public required Guid DeliveryId { get; init; }
    public required DateTime ExpectedDate { get; init; }
    public required DateTime ActualDate { get; init; }
    public required int VarianceDays { get; init; }
    public required bool OnTime { get; init; }
    public required decimal FillRate { get; init; }
    public required decimal QualityAcceptanceRate { get; init; }
}

/// <summary>
/// Price change detected for supplier item.
/// </summary>
public sealed record SupplierPriceChanged : DomainEvent
{
    public override string EventType => "procurement.supplier.price_changed";
    public override string AggregateType => "Supplier";
    public override Guid AggregateId => SupplierId;

    public required Guid SupplierId { get; init; }
    public required string SupplierName { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal PreviousPrice { get; init; }
    public required decimal NewPrice { get; init; }
    public required decimal PriceChange { get; init; }
    public required decimal PriceChangePercent { get; init; }
    public required DateTime EffectiveDate { get; init; }
    public Guid? InvoiceId { get; init; }
}

/// <summary>
/// Supplier rating updated.
/// </summary>
public sealed record SupplierRatingUpdated : DomainEvent
{
    public override string EventType => "procurement.supplier.rating_updated";
    public override string AggregateType => "Supplier";
    public override Guid AggregateId => SupplierId;

    public required Guid SupplierId { get; init; }
    public required string SupplierName { get; init; }
    public required decimal PreviousRating { get; init; }
    public required decimal NewRating { get; init; }
    public required SupplierMetrics Metrics { get; init; }
    public required DateTime PeriodStart { get; init; }
    public required DateTime PeriodEnd { get; init; }
}

public sealed record SupplierMetrics
{
    /// <summary>
    /// On-time deliveries / Total deliveries.
    /// </summary>
    public required decimal OnTimeDeliveryPercent { get; init; }

    /// <summary>
    /// Exact matches / Total invoices.
    /// </summary>
    public required decimal InvoiceMatchRate { get; init; }

    /// <summary>
    /// Accepted qty / Delivered qty.
    /// </summary>
    public required decimal QualityAcceptanceRate { get; init; }

    /// <summary>
    /// Average actual lead time vs quoted.
    /// </summary>
    public required decimal LeadTimeVarianceDays { get; init; }

    /// <summary>
    /// Average fill rate (delivered vs ordered).
    /// </summary>
    public required decimal FillRate { get; init; }

    /// <summary>
    /// Number of orders in the period.
    /// </summary>
    public required int OrderCount { get; init; }

    /// <summary>
    /// Total spend in the period.
    /// </summary>
    public required decimal TotalSpend { get; init; }
}

/// <summary>
/// Supplier issue recorded.
/// </summary>
public sealed record SupplierIssueRecorded : DomainEvent
{
    public override string EventType => "procurement.supplier.issue_recorded";
    public override string AggregateType => "Supplier";
    public override Guid AggregateId => SupplierId;

    public required Guid IssueId { get; init; }
    public required Guid SupplierId { get; init; }
    public required string SupplierName { get; init; }
    public required SupplierIssueType IssueType { get; init; }
    public required string Description { get; init; }
    public required IssueSeverity Severity { get; init; }
    public Guid? DeliveryId { get; init; }
    public Guid? InvoiceId { get; init; }
    public required Guid RecordedBy { get; init; }
}

public enum SupplierIssueType
{
    LateDelivery,
    ShortDelivery,
    QualityIssue,
    PriceDispute,
    CommunicationIssue,
    DocumentationError,
    Other
}

public enum IssueSeverity
{
    Low,
    Medium,
    High,
    Critical
}
