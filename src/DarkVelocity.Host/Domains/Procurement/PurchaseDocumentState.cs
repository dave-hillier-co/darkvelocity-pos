using DarkVelocity.Host.Events;

namespace DarkVelocity.Host.State;

/// <summary>
/// State for a purchase document (invoice or receipt).
/// </summary>
[GenerateSerializer]
public sealed class PurchaseDocumentState
{
    [Id(0)] public Guid DocumentId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }

    // Document classification
    [Id(3)] public PurchaseDocumentType DocumentType { get; set; }
    [Id(4)] public PurchaseDocumentStatus Status { get; set; }
    [Id(5)] public DocumentSource Source { get; set; }

    // Original document
    [Id(6)] public string StorageUrl { get; set; } = string.Empty;
    [Id(7)] public string OriginalFilename { get; set; } = string.Empty;
    [Id(8)] public string ContentType { get; set; } = string.Empty;
    [Id(9)] public long FileSizeBytes { get; set; }

    // Email source metadata
    [Id(10)] public string? EmailFrom { get; set; }
    [Id(11)] public string? EmailSubject { get; set; }

    // Vendor info (supplier for invoices, merchant for receipts)
    [Id(12)] public Guid? VendorId { get; set; }
    [Id(13)] public string? VendorName { get; set; }
    [Id(14)] public string? VendorAddress { get; set; }
    [Id(15)] public string? VendorPhone { get; set; }

    // Invoice-specific fields
    [Id(16)] public string? InvoiceNumber { get; set; }
    [Id(17)] public string? PurchaseOrderNumber { get; set; }
    [Id(18)] public DateOnly? DueDate { get; set; }
    [Id(19)] public string? PaymentTerms { get; set; }

    // Receipt-specific fields
    [Id(20)] public TimeOnly? TransactionTime { get; set; }
    [Id(21)] public string? PaymentMethod { get; set; }
    [Id(22)] public string? CardLastFour { get; set; }

    // Common extracted data
    [Id(23)] public DateOnly? DocumentDate { get; set; }
    [Id(24)] public List<PurchaseDocumentLine> Lines { get; set; } = [];
    [Id(25)] public decimal? Subtotal { get; set; }
    [Id(26)] public decimal? Tax { get; set; }
    [Id(27)] public decimal? Tip { get; set; }
    [Id(28)] public decimal? DeliveryFee { get; set; }
    [Id(29)] public decimal? Total { get; set; }
    [Id(30)] public string Currency { get; set; } = "USD";

    // Payment status
    [Id(31)] public bool IsPaid { get; set; }

    // Processing metadata
    [Id(32)] public decimal ExtractionConfidence { get; set; }
    [Id(33)] public string? ProcessorVersion { get; set; }
    [Id(34)] public string? ProcessingError { get; set; }

    // Audit
    [Id(35)] public DateTime CreatedAt { get; set; }
    [Id(36)] public DateTime? ProcessedAt { get; set; }
    [Id(37)] public DateTime? ConfirmedAt { get; set; }
    [Id(38)] public Guid? ConfirmedBy { get; set; }
    [Id(39)] public DateTime? RejectedAt { get; set; }
    [Id(40)] public Guid? RejectedBy { get; set; }
    [Id(41)] public string? RejectionReason { get; set; }
}

/// <summary>
/// A line item on a purchase document.
/// </summary>
[GenerateSerializer]
public sealed record PurchaseDocumentLine
{
    [Id(0)] public int LineIndex { get; init; }
    [Id(1)] public required string Description { get; init; }
    [Id(2)] public decimal? Quantity { get; init; }
    [Id(3)] public string? Unit { get; init; }
    [Id(4)] public decimal? UnitPrice { get; init; }
    [Id(5)] public decimal? TotalPrice { get; init; }
    [Id(6)] public string? ProductCode { get; init; }
    [Id(7)] public decimal ExtractionConfidence { get; init; }

    // SKU mapping
    [Id(8)] public Guid? MappedIngredientId { get; init; }
    [Id(9)] public string? MappedIngredientSku { get; init; }
    [Id(10)] public string? MappedIngredientName { get; init; }
    [Id(11)] public MappingSource? MappingSource { get; init; }
    [Id(12)] public decimal MappingConfidence { get; init; }

    // Suggestions for unmapped items
    [Id(13)] public List<SuggestedMapping>? Suggestions { get; init; }
}
