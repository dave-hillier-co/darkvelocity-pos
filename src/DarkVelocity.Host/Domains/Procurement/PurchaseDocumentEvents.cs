namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all PurchaseDocument events used in event sourcing.
/// </summary>
public interface IPurchaseDocumentEvent
{
    Guid DocumentId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentCreated : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid SiteId { get; init; }
    [Id(3)] public string DocumentType { get; init; } = ""; // Invoice, Receipt, PurchaseOrder
    [Id(4)] public string DocumentNumber { get; init; } = "";
    [Id(5)] public Guid VendorId { get; init; }
    [Id(6)] public string VendorName { get; init; } = "";
    [Id(7)] public DateOnly DocumentDate { get; init; }
    [Id(8)] public string? Source { get; init; }
    [Id(9)] public Guid CreatedBy { get; init; }
    [Id(10)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineAdded : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public string Description { get; init; } = "";
    [Id(3)] public decimal Quantity { get; init; }
    [Id(4)] public string Unit { get; init; } = "";
    [Id(5)] public decimal UnitPrice { get; init; }
    [Id(6)] public decimal LineTotal { get; init; }
    [Id(7)] public Guid? IngredientId { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineUpdated : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public decimal? Quantity { get; init; }
    [Id(3)] public decimal? UnitPrice { get; init; }
    [Id(4)] public decimal? LineTotal { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineRemoved : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentTotalsUpdated : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public decimal Subtotal { get; init; }
    [Id(2)] public decimal TaxAmount { get; init; }
    [Id(3)] public decimal ShippingAmount { get; init; }
    [Id(4)] public decimal TotalAmount { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentSubmitted : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid SubmittedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentApproved : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid ApprovedBy { get; init; }
    [Id(2)] public string? Notes { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentRejected : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid RejectedBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentReceived : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid ReceivedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentPartiallyReceived : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public List<LineReceiptInfo> LinesReceived { get; init; } = [];
    [Id(2)] public Guid ReceivedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record LineReceiptInfo
{
    [Id(0)] public Guid LineId { get; init; }
    [Id(1)] public decimal QuantityReceived { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentPaid : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public decimal AmountPaid { get; init; }
    [Id(2)] public string PaymentMethod { get; init; } = "";
    [Id(3)] public string? PaymentReference { get; init; }
    [Id(4)] public Guid PaidBy { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentCancelled : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid CancelledBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLinkedToDelivery : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid DeliveryId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentProcessingRequested : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentExtractionApplied : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public string? VendorName { get; init; }
    [Id(2)] public DateOnly? DocumentDate { get; init; }
    [Id(3)] public decimal? Total { get; init; }
    [Id(4)] public int LineCount { get; init; }
    [Id(5)] public decimal Confidence { get; init; }
    [Id(6)] public string? ProcessorVersion { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentExtractionFailed : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public string Reason { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineMapped : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public int LineIndex { get; init; }
    [Id(2)] public Guid IngredientId { get; init; }
    [Id(3)] public string? IngredientSku { get; init; }
    [Id(4)] public string? IngredientName { get; init; }
    [Id(5)] public string MappingSource { get; init; } = "";
    [Id(6)] public decimal Confidence { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineUnmapped : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public int LineIndex { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineModified : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public int LineIndex { get; init; }
    [Id(2)] public string? Description { get; init; }
    [Id(3)] public decimal? Quantity { get; init; }
    [Id(4)] public string? Unit { get; init; }
    [Id(5)] public decimal? UnitPrice { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentConfirmed : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid ConfirmedBy { get; init; }
    [Id(2)] public Guid? VendorId { get; init; }
    [Id(3)] public string? VendorName { get; init; }
    [Id(4)] public DateOnly? DocumentDate { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

// ============================================================================
// Enums
// ============================================================================

/// <summary>
/// Type of purchase document.
/// </summary>
public enum PurchaseDocumentType
{
    /// <summary>Supplier invoice for goods received</summary>
    Invoice,
    /// <summary>Receipt from retail purchase</summary>
    Receipt,
    /// <summary>Purchase order (before goods received)</summary>
    PurchaseOrder,
    /// <summary>Credit note from supplier</summary>
    CreditNote,
    /// <summary>Unknown document type</summary>
    Unknown
}

/// <summary>
/// Status of a purchase document in the processing workflow.
/// </summary>
public enum PurchaseDocumentStatus
{
    /// <summary>Document received, awaiting processing</summary>
    Received,
    /// <summary>OCR/extraction in progress</summary>
    Processing,
    /// <summary>Extraction complete, data extracted</summary>
    Extracted,
    /// <summary>Extraction complete, awaiting review</summary>
    PendingReview,
    /// <summary>Document confirmed and ready for downstream processing</summary>
    Confirmed,
    /// <summary>Document rejected/discarded</summary>
    Rejected,
    /// <summary>Processing failed</summary>
    Failed
}

/// <summary>
/// Source/origin of the document.
/// </summary>
public enum DocumentSource
{
    /// <summary>Uploaded via API or UI</summary>
    Upload,
    /// <summary>Received via email</summary>
    Email,
    /// <summary>Photo captured via mobile app</summary>
    Photo,
    /// <summary>Received from integrated supplier system</summary>
    SupplierIntegration,
    /// <summary>Manually entered</summary>
    Manual,
    /// <summary>Unknown source</summary>
    Unknown
}

/// <summary>
/// Source of an SKU mapping.
/// </summary>
public enum MappingSource
{
    /// <summary>Manually mapped by a user</summary>
    Manual,
    /// <summary>Automatically learned from confirmed document</summary>
    Learned,
    /// <summary>Auto-matched by fuzzy pattern</summary>
    Auto,
    /// <summary>Matched by exact product code</summary>
    ProductCode,
    /// <summary>Matched by fuzzy pattern</summary>
    FuzzyMatch,
    /// <summary>Suggested by AI/ML</summary>
    AiSuggestion,
    /// <summary>Imported from external system</summary>
    Import
}

// ============================================================================
// Supporting Types
// ============================================================================

/// <summary>
/// A suggested mapping for an unmapped line item.
/// </summary>
[GenerateSerializer]
public sealed record SuggestedMapping
{
    [Id(0)] public required Guid IngredientId { get; init; }
    [Id(1)] public required string IngredientName { get; init; }
    [Id(2)] public required string Sku { get; init; }
    [Id(3)] public required decimal Confidence { get; init; }
    [Id(4)] public string? MatchReason { get; init; }
}

/// <summary>
/// Extracted data from a purchase document (output from OCR/AI processing).
/// </summary>
[GenerateSerializer]
public sealed class ExtractedDocumentData
{
    [Id(0)] public PurchaseDocumentType DetectedType { get; init; }
    [Id(1)] public string? VendorName { get; init; }
    [Id(2)] public string? VendorAddress { get; init; }
    [Id(3)] public string? VendorPhone { get; init; }
    [Id(4)] public string? InvoiceNumber { get; init; }
    [Id(5)] public string? PurchaseOrderNumber { get; init; }
    [Id(6)] public DateOnly? DocumentDate { get; init; }
    [Id(7)] public DateOnly? DueDate { get; init; }
    [Id(8)] public string? PaymentTerms { get; init; }
    [Id(9)] public TimeOnly? TransactionTime { get; init; }
    [Id(10)] public string? PaymentMethod { get; init; }
    [Id(11)] public string? CardLastFour { get; init; }
    [Id(12)] public IReadOnlyList<ExtractedLineItem> Lines { get; init; } = [];
    [Id(13)] public decimal? Subtotal { get; init; }
    [Id(14)] public decimal? Tax { get; init; }
    [Id(15)] public decimal? Tip { get; init; }
    [Id(16)] public decimal? DeliveryFee { get; init; }
    [Id(17)] public decimal? Total { get; init; }
    [Id(18)] public string Currency { get; init; } = "USD";
}

/// <summary>
/// A line item extracted from a purchase document.
/// </summary>
[GenerateSerializer]
public sealed record ExtractedLineItem
{
    [Id(0)] public required string Description { get; init; }
    [Id(1)] public decimal? Quantity { get; init; }
    [Id(2)] public string? Unit { get; init; }
    [Id(3)] public decimal? UnitPrice { get; init; }
    [Id(4)] public decimal? TotalPrice { get; init; }
    [Id(5)] public string? ProductCode { get; init; }
    [Id(6)] public decimal Confidence { get; init; } = 1.0m;
}

/// <summary>
/// Data from a confirmed purchase document, ready for downstream processing.
/// </summary>
[GenerateSerializer]
public sealed record ConfirmedDocumentData
{
    [Id(0)] public Guid? VendorId { get; init; }
    [Id(1)] public string? VendorName { get; init; }
    [Id(2)] public DateOnly? DocumentDate { get; init; }
    [Id(3)] public string? InvoiceNumber { get; init; }
    [Id(4)] public IReadOnlyList<ConfirmedLineItem> Lines { get; init; } = [];
    [Id(5)] public decimal Total { get; init; }
    [Id(6)] public decimal Tax { get; init; }
    [Id(7)] public string Currency { get; init; } = "USD";
    [Id(8)] public bool IsPaid { get; init; }
    [Id(9)] public DateOnly? DueDate { get; init; }
}

/// <summary>
/// A confirmed line item with mapped ingredient.
/// </summary>
[GenerateSerializer]
public sealed record ConfirmedLineItem
{
    [Id(0)] public int LineIndex { get; init; }
    [Id(1)] public required string Description { get; init; }
    [Id(2)] public decimal? Quantity { get; init; }
    [Id(3)] public string? Unit { get; init; }
    [Id(4)] public decimal? UnitPrice { get; init; }
    [Id(5)] public decimal? TotalPrice { get; init; }
    [Id(6)] public Guid? IngredientId { get; init; }
    [Id(7)] public string? IngredientSku { get; init; }
    [Id(8)] public MappingSource? MappingSource { get; init; }
}
