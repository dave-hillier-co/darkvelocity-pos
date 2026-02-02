namespace DarkVelocity.Host.Events;

// ============================================================================
// Purchase Document Enums
// ============================================================================

/// <summary>
/// Type of purchase document being processed.
/// </summary>
public enum PurchaseDocumentType
{
    /// <summary>Formal supplier invoice (typically Net 30, creates payable)</summary>
    Invoice,
    /// <summary>Retail receipt (already paid at point of purchase)</summary>
    Receipt
}

/// <summary>
/// Source of the purchase document.
/// </summary>
public enum DocumentSource
{
    /// <summary>Forwarded email with attachment</summary>
    Email,
    /// <summary>Manual file upload via back-office</summary>
    Upload,
    /// <summary>Mobile photo capture</summary>
    Photo,
    /// <summary>Direct integration / webhook</summary>
    Api
}

/// <summary>
/// Processing status of the purchase document.
/// </summary>
public enum PurchaseDocumentStatus
{
    /// <summary>Document received, awaiting processing</summary>
    Received,
    /// <summary>OCR/extraction in progress</summary>
    Processing,
    /// <summary>Extraction complete, awaiting review</summary>
    Extracted,
    /// <summary>Extraction failed, needs manual intervention</summary>
    Failed,
    /// <summary>User confirmed data is correct</summary>
    Confirmed,
    /// <summary>Document rejected/discarded</summary>
    Rejected,
    /// <summary>Archived after downstream processing</summary>
    Archived
}

/// <summary>
/// Source of item-to-SKU mapping.
/// </summary>
public enum MappingSource
{
    /// <summary>Automatically matched from previous mappings</summary>
    Auto,
    /// <summary>User manually selected the mapping</summary>
    Manual,
    /// <summary>System suggested, user accepted</summary>
    Suggested,
    /// <summary>Bulk categorization (all items marked as category)</summary>
    Bulk
}

// ============================================================================
// Purchase Document Lifecycle Events
// ============================================================================

/// <summary>
/// A purchase document (invoice or receipt) was received from an external source.
/// This is an observed fact - the document already exists.
/// </summary>
public sealed record PurchaseDocumentReceived : DomainEvent
{
    public override string EventType => "purchase-document.received";
    public override string AggregateType => "PurchaseDocument";
    public override Guid AggregateId => DocumentId;

    public required Guid DocumentId { get; init; }
    public required PurchaseDocumentType DocumentType { get; init; }
    public required DocumentSource Source { get; init; }
    public required string OriginalFilename { get; init; }
    public required string StorageUrl { get; init; }
    public required string ContentType { get; init; }
    public required long FileSizeBytes { get; init; }
    public string? EmailFrom { get; init; }
    public string? EmailSubject { get; init; }
    /// <summary>True for receipts (already paid), false for invoices by default</summary>
    public bool IsPaid { get; init; }
}

/// <summary>
/// OCR/extraction processing started on a document.
/// </summary>
public sealed record PurchaseDocumentProcessingStarted : DomainEvent
{
    public override string EventType => "purchase-document.processing.started";
    public override string AggregateType => "PurchaseDocument";
    public override Guid AggregateId => DocumentId;

    public required Guid DocumentId { get; init; }
    public required string ProcessorType { get; init; }
}

/// <summary>
/// OCR/extraction completed successfully.
/// </summary>
public sealed record PurchaseDocumentExtracted : DomainEvent
{
    public override string EventType => "purchase-document.extracted";
    public override string AggregateType => "PurchaseDocument";
    public override Guid AggregateId => DocumentId;

    public required Guid DocumentId { get; init; }
    public required ExtractedDocumentData Data { get; init; }
    public required decimal ExtractionConfidence { get; init; }
    public required string ProcessorVersion { get; init; }
}

/// <summary>
/// Extraction failed - needs manual intervention.
/// </summary>
public sealed record PurchaseDocumentExtractionFailed : DomainEvent
{
    public override string EventType => "purchase-document.extraction.failed";
    public override string AggregateType => "PurchaseDocument";
    public override Guid AggregateId => DocumentId;

    public required Guid DocumentId { get; init; }
    public required string FailureReason { get; init; }
    public string? ProcessorError { get; init; }
}

/// <summary>
/// A line item was mapped to an internal SKU.
/// </summary>
public sealed record PurchaseDocumentLineMapped : DomainEvent
{
    public override string EventType => "purchase-document.line.mapped";
    public override string AggregateType => "PurchaseDocument";
    public override Guid AggregateId => DocumentId;

    public required Guid DocumentId { get; init; }
    public required int LineIndex { get; init; }
    public required Guid IngredientId { get; init; }
    public required string VendorDescription { get; init; }
    public required MappingSource Source { get; init; }
    public required decimal Confidence { get; init; }
}

/// <summary>
/// A line item could not be mapped - flagged for manual review.
/// </summary>
public sealed record PurchaseDocumentLineUnmapped : DomainEvent
{
    public override string EventType => "purchase-document.line.unmapped";
    public override string AggregateType => "PurchaseDocument";
    public override Guid AggregateId => DocumentId;

    public required Guid DocumentId { get; init; }
    public required int LineIndex { get; init; }
    public required string VendorDescription { get; init; }
    public IReadOnlyList<SuggestedMapping>? Suggestions { get; init; }
}

/// <summary>
/// User confirmed the document data is correct.
/// </summary>
public sealed record PurchaseDocumentConfirmed : DomainEvent
{
    public override string EventType => "purchase-document.confirmed";
    public override string AggregateType => "PurchaseDocument";
    public override Guid AggregateId => DocumentId;

    public required Guid DocumentId { get; init; }
    public required Guid ConfirmedBy { get; init; }
    public required ConfirmedDocumentData Data { get; init; }
}

/// <summary>
/// Document was rejected/discarded.
/// </summary>
public sealed record PurchaseDocumentRejected : DomainEvent
{
    public override string EventType => "purchase-document.rejected";
    public override string AggregateType => "PurchaseDocument";
    public override Guid AggregateId => DocumentId;

    public required Guid DocumentId { get; init; }
    public required Guid RejectedBy { get; init; }
    public required string Reason { get; init; }
}

// ============================================================================
// Supporting Records
// ============================================================================

/// <summary>
/// Data extracted from OCR processing.
/// </summary>
[GenerateSerializer]
public sealed record ExtractedDocumentData
{
    [Id(0)] public PurchaseDocumentType DetectedType { get; init; }

    // Vendor info (supplier for invoices, merchant for receipts)
    [Id(1)] public string? VendorName { get; init; }
    [Id(2)] public string? VendorAddress { get; init; }
    [Id(3)] public string? VendorPhone { get; init; }

    // Invoice-specific
    [Id(4)] public string? InvoiceNumber { get; init; }
    [Id(5)] public string? PurchaseOrderNumber { get; init; }
    [Id(6)] public DateOnly? DueDate { get; init; }
    [Id(7)] public string? PaymentTerms { get; init; }

    // Receipt-specific
    [Id(8)] public TimeOnly? TransactionTime { get; init; }
    [Id(9)] public string? PaymentMethod { get; init; }
    [Id(10)] public string? CardLastFour { get; init; }

    // Common fields
    [Id(11)] public DateOnly? DocumentDate { get; init; }
    [Id(12)] public required IReadOnlyList<ExtractedLineItem> Lines { get; init; }
    [Id(13)] public decimal? Subtotal { get; init; }
    [Id(14)] public decimal? Tax { get; init; }
    [Id(15)] public decimal? Tip { get; init; }
    [Id(16)] public decimal? DeliveryFee { get; init; }
    [Id(17)] public decimal? Total { get; init; }
    [Id(18)] public string? Currency { get; init; }
}

/// <summary>
/// A line item extracted from the document.
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
    [Id(6)] public decimal Confidence { get; init; }
}

/// <summary>
/// A suggested SKU mapping for an unmatched item.
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
/// Confirmed document data after user review.
/// </summary>
[GenerateSerializer]
public sealed record ConfirmedDocumentData
{
    [Id(0)] public Guid? VendorId { get; init; }
    [Id(1)] public required string VendorName { get; init; }
    [Id(2)] public required DateOnly DocumentDate { get; init; }
    [Id(3)] public string? InvoiceNumber { get; init; }
    [Id(4)] public required IReadOnlyList<ConfirmedLineItem> Lines { get; init; }
    [Id(5)] public required decimal Total { get; init; }
    [Id(6)] public decimal Tax { get; init; }
    [Id(7)] public required string Currency { get; init; }
    [Id(8)] public bool IsPaid { get; init; }
    [Id(9)] public DateOnly? DueDate { get; init; }
}

/// <summary>
/// A confirmed line item with SKU mapping.
/// </summary>
[GenerateSerializer]
public sealed record ConfirmedLineItem
{
    [Id(0)] public required string Description { get; init; }
    [Id(1)] public required decimal Quantity { get; init; }
    [Id(2)] public required string Unit { get; init; }
    [Id(3)] public required decimal UnitPrice { get; init; }
    [Id(4)] public required decimal TotalPrice { get; init; }
    [Id(5)] public Guid? IngredientId { get; init; }
    [Id(6)] public string? IngredientSku { get; init; }
    [Id(7)] public MappingSource? MappingSource { get; init; }
}
