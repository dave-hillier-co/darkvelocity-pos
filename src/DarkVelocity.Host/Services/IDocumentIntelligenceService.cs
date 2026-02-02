using DarkVelocity.Host.Events;

namespace DarkVelocity.Host.Services;

/// <summary>
/// Service for extracting structured data from purchase documents using OCR/AI.
/// </summary>
public interface IDocumentIntelligenceService
{
    /// <summary>
    /// Extract data from an invoice document.
    /// </summary>
    Task<InvoiceExtractionResult> ExtractInvoiceAsync(
        Stream document,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract data from a receipt document.
    /// </summary>
    Task<ReceiptExtractionResult> ExtractReceiptAsync(
        Stream document,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-detect document type and extract accordingly.
    /// </summary>
    Task<DocumentExtractionResult> ExtractAsync(
        Stream document,
        string contentType,
        PurchaseDocumentType? typeHint = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of invoice extraction.
/// </summary>
public record InvoiceExtractionResult(
    VendorInfo? Vendor,
    string? InvoiceNumber,
    string? PurchaseOrderNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    string? PaymentTerms,
    IReadOnlyList<ExtractedLineItem> LineItems,
    MonetaryAmount? Subtotal,
    MonetaryAmount? Tax,
    MonetaryAmount? Total,
    decimal OverallConfidence,
    IReadOnlyList<ExtractionWarning> Warnings);

/// <summary>
/// Result of receipt extraction.
/// </summary>
public record ReceiptExtractionResult(
    string? MerchantName,
    string? MerchantAddress,
    string? MerchantPhone,
    DateOnly? TransactionDate,
    TimeOnly? TransactionTime,
    IReadOnlyList<ExtractedLineItem> LineItems,
    MonetaryAmount? Subtotal,
    MonetaryAmount? Tax,
    MonetaryAmount? Tip,
    MonetaryAmount? Total,
    string? PaymentMethod,
    string? LastFourDigits,
    decimal OverallConfidence,
    IReadOnlyList<ExtractionWarning> Warnings);

/// <summary>
/// Unified result that can hold either document type.
/// </summary>
public record DocumentExtractionResult(
    PurchaseDocumentType DetectedType,
    InvoiceExtractionResult? Invoice,
    ReceiptExtractionResult? Receipt)
{
    /// <summary>
    /// Convert to the common ExtractedDocumentData format.
    /// </summary>
    public ExtractedDocumentData ToExtractedDocumentData()
    {
        if (Invoice != null)
        {
            return new ExtractedDocumentData
            {
                DetectedType = PurchaseDocumentType.Invoice,
                VendorName = Invoice.Vendor?.Name,
                VendorAddress = Invoice.Vendor?.Address,
                VendorPhone = Invoice.Vendor?.Phone,
                InvoiceNumber = Invoice.InvoiceNumber,
                PurchaseOrderNumber = Invoice.PurchaseOrderNumber,
                DocumentDate = Invoice.InvoiceDate,
                DueDate = Invoice.DueDate,
                PaymentTerms = Invoice.PaymentTerms,
                Lines = Invoice.LineItems,
                Subtotal = Invoice.Subtotal?.Amount,
                Tax = Invoice.Tax?.Amount,
                Total = Invoice.Total?.Amount,
                Currency = Invoice.Total?.Currency ?? "USD"
            };
        }

        if (Receipt != null)
        {
            return new ExtractedDocumentData
            {
                DetectedType = PurchaseDocumentType.Receipt,
                VendorName = Receipt.MerchantName,
                VendorAddress = Receipt.MerchantAddress,
                VendorPhone = Receipt.MerchantPhone,
                DocumentDate = Receipt.TransactionDate,
                TransactionTime = Receipt.TransactionTime,
                PaymentMethod = Receipt.PaymentMethod,
                CardLastFour = Receipt.LastFourDigits,
                Lines = Receipt.LineItems,
                Subtotal = Receipt.Subtotal?.Amount,
                Tax = Receipt.Tax?.Amount,
                Tip = Receipt.Tip?.Amount,
                Total = Receipt.Total?.Amount,
                Currency = Receipt.Total?.Currency ?? "USD"
            };
        }

        throw new InvalidOperationException("Either Invoice or Receipt must be set");
    }

    public decimal OverallConfidence => Invoice?.OverallConfidence ?? Receipt?.OverallConfidence ?? 0;
}

/// <summary>
/// Vendor information extracted from document.
/// </summary>
public record VendorInfo(
    string? Name,
    string? Address,
    string? Phone,
    string? TaxId);

/// <summary>
/// Monetary amount with currency.
/// </summary>
public record MonetaryAmount(
    decimal Amount,
    string Currency);

/// <summary>
/// Warning from extraction process.
/// </summary>
public record ExtractionWarning(
    string Code,
    string Message,
    string? Field = null);
