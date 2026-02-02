using DarkVelocity.Host.Events;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.Host.Services;

/// <summary>
/// Stub implementation of document intelligence service for development.
/// Replace with AzureDocumentIntelligenceService or AwsTextractService in production.
/// </summary>
public class StubDocumentIntelligenceService : IDocumentIntelligenceService
{
    private readonly ILogger<StubDocumentIntelligenceService> _logger;

    public StubDocumentIntelligenceService(ILogger<StubDocumentIntelligenceService> logger)
    {
        _logger = logger;
    }

    public Task<InvoiceExtractionResult> ExtractInvoiceAsync(
        Stream document,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Using stub document intelligence - returning mock invoice data");

        var result = new InvoiceExtractionResult(
            Vendor: new VendorInfo("Acme Foods Inc.", "123 Supplier Way, Foodtown, CA 90210", "555-123-4567", null),
            InvoiceNumber: $"INV-{DateTime.UtcNow:yyyyMMdd}-001",
            PurchaseOrderNumber: null,
            InvoiceDate: DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            PaymentTerms: "Net 30",
            LineItems: new List<ExtractedLineItem>
            {
                new ExtractedLineItem
                {
                    Description = "Chicken Breast 5kg Case",
                    Quantity = 2,
                    Unit = "case",
                    UnitPrice = 45.00m,
                    TotalPrice = 90.00m,
                    ProductCode = "CHKN-BRT-5KG",
                    Confidence = 0.95m
                },
                new ExtractedLineItem
                {
                    Description = "Organic Mixed Greens 1kg",
                    Quantity = 5,
                    Unit = "kg",
                    UnitPrice = 12.50m,
                    TotalPrice = 62.50m,
                    ProductCode = "ORG-GREENS-1KG",
                    Confidence = 0.92m
                }
            },
            Subtotal: new MonetaryAmount(152.50m, "USD"),
            Tax: new MonetaryAmount(12.20m, "USD"),
            Total: new MonetaryAmount(164.70m, "USD"),
            OverallConfidence: 0.90m,
            Warnings: new List<ExtractionWarning>
            {
                new ExtractionWarning("STUB", "This is mock data from stub service")
            });

        return Task.FromResult(result);
    }

    public Task<ReceiptExtractionResult> ExtractReceiptAsync(
        Stream document,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Using stub document intelligence - returning mock receipt data");

        var result = new ReceiptExtractionResult(
            MerchantName: "COSTCO WHOLESALE",
            MerchantAddress: "456 Retail Blvd, Shopville, CA 90211",
            MerchantPhone: "555-987-6543",
            TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow),
            TransactionTime: TimeOnly.FromDateTime(DateTime.UtcNow),
            LineItems: new List<ExtractedLineItem>
            {
                new ExtractedLineItem
                {
                    Description = "ORG EGGS 24CT",
                    Quantity = 1,
                    Unit = "ea",
                    UnitPrice = 8.99m,
                    TotalPrice = 8.99m,
                    ProductCode = null,
                    Confidence = 0.88m
                },
                new ExtractedLineItem
                {
                    Description = "KS OLIVE OIL 2L",
                    Quantity = 1,
                    Unit = "ea",
                    UnitPrice = 15.99m,
                    TotalPrice = 15.99m,
                    ProductCode = null,
                    Confidence = 0.85m
                },
                new ExtractedLineItem
                {
                    Description = "FLOUR AP 25LB",
                    Quantity = 1,
                    Unit = "ea",
                    UnitPrice = 9.49m,
                    TotalPrice = 9.49m,
                    ProductCode = null,
                    Confidence = 0.82m
                }
            },
            Subtotal: new MonetaryAmount(34.47m, "USD"),
            Tax: new MonetaryAmount(2.76m, "USD"),
            Tip: null,
            Total: new MonetaryAmount(37.23m, "USD"),
            PaymentMethod: "VISA",
            LastFourDigits: "4242",
            OverallConfidence: 0.85m,
            Warnings: new List<ExtractionWarning>
            {
                new ExtractionWarning("STUB", "This is mock data from stub service")
            });

        return Task.FromResult(result);
    }

    public async Task<DocumentExtractionResult> ExtractAsync(
        Stream document,
        string contentType,
        PurchaseDocumentType? typeHint = null,
        CancellationToken cancellationToken = default)
    {
        // Use type hint if provided, otherwise default to invoice
        var documentType = typeHint ?? PurchaseDocumentType.Invoice;

        if (documentType == PurchaseDocumentType.Receipt)
        {
            var receipt = await ExtractReceiptAsync(document, contentType, cancellationToken);
            return new DocumentExtractionResult(PurchaseDocumentType.Receipt, null, receipt);
        }
        else
        {
            var invoice = await ExtractInvoiceAsync(document, contentType, cancellationToken);
            return new DocumentExtractionResult(PurchaseDocumentType.Invoice, invoice, null);
        }
    }
}
