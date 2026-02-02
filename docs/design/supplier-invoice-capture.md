# Purchase Document Capture - Requirements & Architecture

## Problem Statement

Restaurants need visibility into their costs to manage profitability. Purchase documents arrive through multiple channels in unstructured formats:

- **Supplier invoices**: Formal invoices from food distributors, beverage companies (Sysco, US Foods, local suppliers)
- **Retail receipts**: Grocery store purchases (Costco, Restaurant Depot, local supermarkets) - common for smaller operations
- **Ad-hoc purchases**: Staff buying supplies with petty cash

Manual data entry is time-consuming and error-prone.

**Goal**: Automatically capture, process, and categorize all purchase documents to provide accurate cost tracking and inventory reconciliation.

---

## Document Types: Invoices vs Receipts

The system handles both formal supplier invoices and retail receipts through a unified pipeline with type-specific processing.

| Aspect | Supplier Invoice | Retail Receipt |
|--------|------------------|----------------|
| **Source** | Email, supplier portal, delivery | Photo at checkout, email receipt |
| **Format** | Formal PDF, structured layout | Thermal print, abbreviated text |
| **Item descriptions** | "Chicken Breast 5kg Case" | "CHKN BRST 2.3LB" |
| **Payment status** | Net 30, creates payable | Already paid |
| **Frequency** | Weekly/periodic deliveries | Ad-hoc, possibly daily |
| **OCR model** | Azure "Invoice" model | Azure "Receipt" model |
| **Vendor** | Known supplier with account | Store name from header |
| **Typical use** | Established supplier relationships | Grocery runs, small top-ups |

**Key insight**: The core pipeline is identical - capture document → extract data → map to SKUs → track costs. The differences are:

1. **OCR model selection** - system auto-detects or user specifies document type
2. **Payment workflow** - invoices create accounts payable; receipts are marked as paid
3. **Vendor handling** - invoices link to supplier records; receipts use store name as vendor
4. **Description quality** - receipts need more aggressive fuzzy matching ("ORG EGGS 18CT" → eggs)

---

## Functional Requirements

### 1. Document Ingestion

**Sources**:
- **Email forwarding**: Restaurant forwards invoice emails to a dedicated inbox (e.g., `invoices-{siteId}@darkvelocity.io`)
- **Image upload**: Staff photographs paper invoices/receipts via mobile app (primary method for receipts)
- **File upload**: PDF/image upload through back-office
- **Future**: Direct supplier integrations (EDI, API)

**Supported Formats**:
- PDF (text-based and scanned)
- Images (JPEG, PNG, HEIC) - especially important for receipt photos
- Email bodies with embedded tables

### 2. Document Processing

**Extraction Goals**:
- Supplier identification (name, address, account number)
- Invoice metadata (number, date, due date, PO reference)
- Line items with: description, quantity, unit, unit price, total
- Totals: subtotal, tax, delivery fees, discounts, grand total
- Payment terms

**Processing States**:
```
Received → Processing → Extracted → (Review) → Confirmed → Archived
                ↓
            Failed (manual intervention required)
```

### 3. SKU Mapping

**Challenge**: Supplier item descriptions don't match internal inventory SKUs.
- "CHICKEN BREAST 5KG" (supplier) → "chicken-breast-raw" (internal SKU)

**Solution**:
- Maintain supplier-specific item mappings
- Auto-suggest matches based on previous mappings and fuzzy matching
- Highlight unmapped items for manual review
- Learn from confirmations to improve future matching

### 4. Cost Integration

Once confirmed, invoice data flows to:
- **Inventory**: Update stock levels and weighted average costs
- **Accounts Payable**: Create payable records
- **Analytics**: Cost tracking, price variance alerts

### 5. Other Expenses (Extension)

Non-inventory costs need tracking too:
- Rent, utilities, insurance
- Equipment maintenance
- Marketing expenses
- Bank fees, credit card processing fees

These are simpler: just categorize and record, no SKU mapping needed.

---

## Domain Model

### Aggregates

#### 1. SupplierInvoice (Grain)
The core aggregate representing a captured invoice document.

```
Key: "{orgId}:{siteId}:supplier-invoice:{invoiceId}"
```

**State**:
- Document metadata (source, original filename, storage URL)
- Processing status and history
- Extracted data (supplier, lines, totals)
- Mapping status per line
- Confirmation audit trail

#### 2. SupplierItemMapping (Grain)
Maps supplier-specific item descriptions to internal SKUs.

```
Key: "{orgId}:supplier-mapping:{supplierId}"
```

**State**:
- Mapping rules: supplier item description → internal SKU
- Confidence scores from ML/usage
- Manual override flags

#### 3. Expense (Grain)
Represents a non-inventory expense.

```
Key: "{orgId}:{siteId}:expense:{expenseId}"
```

**State**:
- Category, description, amount
- Date, payment method
- Supporting document reference
- Allocation (which cost center/site)

#### 4. SupplierInvoiceIndex (Grain)
Per-site index for querying invoices.

```
Key: "{orgId}:{siteId}:supplier-invoice-index"
```

---

## Events (Past Tense, Following Codebase Conventions)

### Invoice Lifecycle Events

```csharp
// Enums for document classification
public enum PurchaseDocumentType
{
    Invoice,    // Formal supplier invoice (Net 30, creates payable)
    Receipt     // Retail receipt (already paid)
}

public enum DocumentSource
{
    Email,      // Forwarded email with attachment
    Upload,     // Manual file upload via back-office
    Photo,      // Mobile photo capture
    Api         // Direct integration / webhook
}

// Document received from any source
public sealed record PurchaseDocumentReceived : DomainEvent
{
    public override string EventType => "purchase-document.received";

    public required Guid DocumentId { get; init; }
    public required PurchaseDocumentType DocumentType { get; init; }
    public required DocumentSource Source { get; init; }
    public required string OriginalFilename { get; init; }
    public required string StorageUrl { get; init; }
    public required string ContentType { get; init; }
    public required long FileSizeBytes { get; init; }
    public string? EmailFrom { get; init; }
    public string? EmailSubject { get; init; }
    public bool IsPaid { get; init; }  // True for receipts, false for invoices by default
}

// OCR/extraction completed
public sealed record SupplierInvoiceExtracted : DomainEvent
{
    public override string EventType => "supplier-invoice.extracted";

    public required Guid InvoiceId { get; init; }
    public required ExtractedInvoiceData Data { get; init; }
    public required decimal ExtractionConfidence { get; init; }
    public required string ProcessorVersion { get; init; }
}

// Extraction failed - needs manual intervention
public sealed record SupplierInvoiceExtractionFailed : DomainEvent
{
    public override string EventType => "supplier-invoice.extraction.failed";

    public required Guid InvoiceId { get; init; }
    public required string FailureReason { get; init; }
    public required string? ProcessorError { get; init; }
}

// Line item mapped to internal SKU
public sealed record SupplierInvoiceLineMapped : DomainEvent
{
    public override string EventType => "supplier-invoice.line.mapped";

    public required Guid InvoiceId { get; init; }
    public required int LineIndex { get; init; }
    public required Guid IngredientId { get; init; }
    public required string SupplierDescription { get; init; }
    public required MappingSource Source { get; init; }  // Auto, Manual, Suggested
    public required decimal Confidence { get; init; }
}

// Line item flagged as unmapped
public sealed record SupplierInvoiceLineUnmapped : DomainEvent
{
    public override string EventType => "supplier-invoice.line.unmapped";

    public required Guid InvoiceId { get; init; }
    public required int LineIndex { get; init; }
    public required string SupplierDescription { get; init; }
    public IReadOnlyList<SuggestedMapping>? Suggestions { get; init; }
}

// User confirmed invoice data is correct
public sealed record SupplierInvoiceConfirmed : DomainEvent
{
    public override string EventType => "supplier-invoice.confirmed";

    public required Guid InvoiceId { get; init; }
    public required Guid ConfirmedBy { get; init; }
    public required ConfirmedInvoiceData Data { get; init; }
}

// Invoice rejected/discarded
public sealed record SupplierInvoiceRejected : DomainEvent
{
    public override string EventType => "supplier-invoice.rejected";

    public required Guid InvoiceId { get; init; }
    public required Guid RejectedBy { get; init; }
    public required string Reason { get; init; }
}
```

### Expense Events

```csharp
public sealed record ExpenseRecorded : DomainEvent
{
    public override string EventType => "expense.recorded";

    public required Guid ExpenseId { get; init; }
    public required ExpenseCategory Category { get; init; }
    public required string Description { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateOnly ExpenseDate { get; init; }
    public Guid? DocumentId { get; init; }  // Link to uploaded receipt
    public Guid? VendorId { get; init; }
}

public enum ExpenseCategory
{
    Rent,
    Utilities,
    Insurance,
    Equipment,
    Maintenance,
    Marketing,
    Supplies,       // Non-food supplies (cleaning, paper goods)
    Professional,   // Accounting, legal
    BankFees,
    CreditCardFees,
    Licenses,
    Other
}
```

---

## Processing Pipeline Architecture

### Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         INGESTION LAYER                                  │
├─────────────────┬─────────────────┬─────────────────┬───────────────────┤
│  Email Poller   │  Upload API     │  Mobile Photo   │  Supplier EDI     │
│  (Azure Logic   │  (POST /docs)   │  (via POS app)  │  (webhooks)       │
│   App / AWS     │                 │                 │                   │
│   SES)          │                 │                 │                   │
└────────┬────────┴────────┬────────┴────────┬────────┴─────────┬─────────┘
         │                 │                 │                  │
         ▼                 ▼                 ▼                  ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                      DOCUMENT STORAGE (Blob)                            │
│                   S3 / Azure Blob / GCS                                 │
│              Organized: /{orgId}/{siteId}/invoices/{year}/{month}/      │
└─────────────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                      ORLEANS GRAIN LAYER                                 │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │  SupplierInvoiceGrain                                            │   │
│  │  - Receives document reference                                    │   │
│  │  - Triggers processing                                            │   │
│  │  - Manages state machine                                          │   │
│  │  - Coordinates mapping                                            │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                              │                                           │
│                              ▼                                           │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │  InvoiceProcessorGrain (Stateless Worker)                        │   │
│  │  - Calls OCR service (Azure Document Intelligence / Textract)    │   │
│  │  - Parses extracted text into structured data                    │   │
│  │  - Returns extracted invoice data                                │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                              │                                           │
│                              ▼                                           │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │  SupplierItemMappingGrain                                        │   │
│  │  - Maintains supplier → SKU mappings                             │   │
│  │  - Auto-maps known items                                         │   │
│  │  - Suggests matches for unknown items                            │   │
│  │  - Learns from user confirmations                                │   │
│  └──────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    DOWNSTREAM INTEGRATION (via Events)                   │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────────────┐     │
│  │  Inventory     │  │  Accounts      │  │  Analytics/Reporting   │     │
│  │  (stock +      │  │  Payable       │  │  (cost trends,         │     │
│  │   costs)       │  │  (payment      │  │   price variance)      │     │
│  │                │  │   tracking)    │  │                        │     │
│  └────────────────┘  └────────────────┘  └────────────────────────┘     │
└─────────────────────────────────────────────────────────────────────────┘
```

### Grain Interfaces

```csharp
public interface ISupplierInvoiceGrain : IGrainWithStringKey
{
    /// <summary>
    /// Records a new invoice document received from an external source.
    /// Follows the "Received" pattern for external data.
    /// </summary>
    Task<SupplierInvoiceSnapshot> ReceiveDocumentAsync(ReceiveInvoiceDocumentCommand command);

    /// <summary>
    /// Triggers OCR/extraction processing.
    /// </summary>
    Task RequestProcessingAsync();

    /// <summary>
    /// Called by processor when extraction completes.
    /// </summary>
    Task ApplyExtractionResultAsync(ExtractionResult result);

    /// <summary>
    /// Map a specific line item to an internal SKU.
    /// </summary>
    Task MapLineAsync(int lineIndex, Guid ingredientId, MappingSource source);

    /// <summary>
    /// Confirm the invoice data is correct and ready for downstream processing.
    /// </summary>
    Task<SupplierInvoiceSnapshot> ConfirmAsync(ConfirmInvoiceCommand command);

    /// <summary>
    /// Reject/discard the invoice.
    /// </summary>
    Task RejectAsync(RejectInvoiceCommand command);

    Task<SupplierInvoiceSnapshot> GetSnapshotAsync();
}

public interface ISupplierItemMappingGrain : IGrainWithStringKey
{
    /// <summary>
    /// Get the SKU mapping for a supplier item description.
    /// Returns null if no mapping exists.
    /// </summary>
    Task<ItemMappingResult?> GetMappingAsync(string supplierItemDescription);

    /// <summary>
    /// Get suggested mappings based on fuzzy matching.
    /// </summary>
    Task<IReadOnlyList<SuggestedMapping>> GetSuggestionsAsync(string supplierItemDescription);

    /// <summary>
    /// Record a confirmed mapping (learns from user actions).
    /// </summary>
    Task LearnMappingAsync(LearnMappingCommand command);

    /// <summary>
    /// Manually set or override a mapping.
    /// </summary>
    Task SetMappingAsync(SetMappingCommand command);
}

[StatelessWorker]
public interface IInvoiceProcessorGrain : IGrainWithStringKey
{
    /// <summary>
    /// Process a document and extract invoice data.
    /// </summary>
    Task<ExtractionResult> ProcessAsync(ProcessInvoiceCommand command);
}

public interface IExpenseGrain : IGrainWithStringKey
{
    Task<ExpenseSnapshot> RecordAsync(RecordExpenseCommand command);
    Task<ExpenseSnapshot> UpdateAsync(UpdateExpenseCommand command);
    Task DeleteAsync(Guid deletedBy, string reason);
    Task<ExpenseSnapshot> GetSnapshotAsync();
}
```

---

## SKU Mapping Strategy

### Mapping Sources (Priority Order)

1. **Exact Match**: Supplier item code matches a previous mapping exactly
2. **Learned Match**: Description similarity to confirmed mappings (>90% confidence)
3. **Fuzzy Suggestion**: Approximate matches for user review (50-90% confidence)
4. **Unmapped**: No match found, requires manual mapping

### Mapping Data Structure

```csharp
[GenerateSerializer]
public sealed class SupplierItemMappingState
{
    [Id(0)] public Guid SupplierId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }

    // Exact mappings: supplier item code/description → internal SKU
    [Id(2)] public Dictionary<string, ItemMapping> ExactMappings { get; set; } = [];

    // Learned patterns for fuzzy matching
    [Id(3)] public List<LearnedPattern> LearnedPatterns { get; set; } = [];

    [Id(4)] public int Version { get; set; }
}

[GenerateSerializer]
public record ItemMapping
{
    [Id(0)] public required Guid IngredientId { get; init; }
    [Id(1)] public required string IngredientName { get; init; }
    [Id(2)] public required string IngredientSku { get; init; }
    [Id(3)] public required DateTime CreatedAt { get; init; }
    [Id(4)] public required Guid CreatedBy { get; init; }
    [Id(5)] public required int UsageCount { get; init; }
    [Id(6)] public decimal? ExpectedUnitPrice { get; init; }  // For price variance alerts
}
```

### Learning Algorithm

When a user confirms a mapping:
1. Store exact match for that supplier item description
2. Extract tokens (normalized words) from description
3. Associate token patterns with the target SKU
4. Weight by frequency of confirmation

Future matching uses token overlap scoring against learned patterns.

---

## API Endpoints

```
--- Purchase Documents (invoices and receipts) ---

POST   /api/orgs/{orgId}/sites/{siteId}/purchases
       - Upload new purchase document (invoice or receipt)
       - Body: multipart/form-data with file + metadata
       - Query param: ?type=invoice|receipt (optional, auto-detected if omitted)

GET    /api/orgs/{orgId}/sites/{siteId}/purchases
       - List documents with filtering (type, status, date range, vendor)
       - Query params: ?type=invoice|receipt&status=pending&from=2024-01-01

GET    /api/orgs/{orgId}/sites/{siteId}/purchases/{id}
       - Get document details including extracted data

POST   /api/orgs/{orgId}/sites/{siteId}/purchases/{id}/process
       - Trigger (re)processing

PATCH  /api/orgs/{orgId}/sites/{siteId}/purchases/{id}/lines/{idx}
       - Update line item mapping
       - Body: { "ingredientId": "...", "source": "manual" }

POST   /api/orgs/{orgId}/sites/{siteId}/purchases/{id}/confirm
       - Confirm document for downstream processing

DELETE /api/orgs/{orgId}/sites/{siteId}/purchases/{id}
       - Reject/delete document

--- Expenses (non-inventory costs) ---

POST   /api/orgs/{orgId}/sites/{siteId}/expenses
GET    /api/orgs/{orgId}/sites/{siteId}/expenses
GET    /api/orgs/{orgId}/sites/{siteId}/expenses/{id}
PATCH  /api/orgs/{orgId}/sites/{siteId}/expenses/{id}
DELETE /api/orgs/{orgId}/sites/{siteId}/expenses/{id}

--- Vendor Item Mappings ---

GET    /api/orgs/{orgId}/vendors/{vendorId}/item-mappings
       - List all mappings for a vendor (supplier or store)

POST   /api/orgs/{orgId}/vendors/{vendorId}/item-mappings
       - Manually create/update mapping

GET    /api/orgs/{orgId}/vendors/{vendorId}/item-mappings/suggest
       - Get suggested mappings for a description
       - Query param: ?description=ORG%20LG%20EGGS
```

---

## OCR Service Integration

### Recommended: Azure AI Document Intelligence

Azure provides separate pre-built models optimized for each document type:

| Model | Use Case | Key Fields Extracted |
|-------|----------|---------------------|
| **prebuilt-invoice** | Supplier invoices | Vendor, invoice #, PO #, line items, due date, payment terms |
| **prebuilt-receipt** | Retail receipts | Merchant, items, subtotal, tax, tip, total, payment method |

Both models:
- Return confidence scores per field
- Handle rotated/skewed images
- Support multiple languages

### Alternative: AWS Textract

- `AnalyzeExpense` API handles both invoices and receipts
- Similar structured output with confidence scores

### Abstraction Layer

```csharp
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
    string? PaymentMethod,        // Cash, Credit, Debit
    string? LastFourDigits,       // Card last 4 if present
    decimal OverallConfidence,
    IReadOnlyList<ExtractionWarning> Warnings);

// Unified result that can hold either type
public record DocumentExtractionResult(
    PurchaseDocumentType DetectedType,
    InvoiceExtractionResult? Invoice,
    ReceiptExtractionResult? Receipt);
```

### Receipt-Specific Challenges

Receipts present unique OCR challenges:

1. **Abbreviated text**: "ORG LG EGGS" → need expansion/normalization
2. **No item codes**: Just descriptions, unlike supplier invoices with SKUs
3. **Faded thermal print**: Lower quality images
4. **Store-specific formats**: Each retailer has different layouts

**Mitigation strategies**:
- Build store-specific parsing rules for common retailers (Costco, Walmart, etc.)
- More aggressive fuzzy matching with lower confidence thresholds
- Allow bulk "this is all groceries" categorization for small receipts

---

## Email Ingestion Options

### Option A: Dedicated Mailbox Polling

- Create `invoices@yourdomain.com` per organization
- Azure Logic App / AWS SES + Lambda polls and forwards to API
- Simple setup, works with any email provider

### Option B: Email Forwarding Rules

- Restaurants set up auto-forward from their existing inbox
- We provide unique forwarding address per site
- Lower friction for onboarding

### Option C: IMAP Integration

- Restaurant provides email credentials (OAuth preferred)
- We scan inbox for invoice-like attachments
- More complex, privacy concerns

**Recommendation**: Start with Option A (dedicated mailbox), add Option B later.

---

## Data Model Summary

```
┌─────────────────────────────────────────────────────────────┐
│  PurchaseDocument (unified for invoices and receipts)       │
├─────────────────────────────────────────────────────────────┤
│  - DocumentId (PK)                                          │
│  - OrganizationId, SiteId                                   │
│  - DocumentType (Invoice | Receipt)                         │
│  - Status (Received, Processing, Extracted, Confirmed...)   │
│  - Source (Email, Upload, Photo, Api)                       │
│  - DocumentUrl (blob storage)                               │
│  - OriginalFilename, ContentType, FileSizeBytes             │
│                                                             │
│  // Vendor info (supplier for invoices, merchant for receipts)
│  - VendorId (nullable - links to Supplier if matched)       │
│  - VendorName, VendorAddress                                │
│                                                             │
│  // Invoice-specific (null for receipts)                    │
│  - InvoiceNumber, PurchaseOrderNumber                       │
│  - DueDate, PaymentTerms                                    │
│                                                             │
│  // Receipt-specific (null for invoices)                    │
│  - TransactionTime                                          │
│  - PaymentMethod, CardLastFour                              │
│                                                             │
│  // Common fields                                           │
│  - DocumentDate                                             │
│  - Lines[] { description, qty, unit, unitPrice, total,      │
│              mappedIngredientId, mappingConfidence,         │
│              mappingSource }                                │
│  - Subtotal, Tax, Tip (receipts), DeliveryFee, Total        │
│  - Currency                                                 │
│  - IsPaid (always true for receipts)                        │
│  - ExtractionConfidence                                     │
│  - ConfirmedAt, ConfirmedBy                                 │
│  - CreatedAt, UpdatedAt, Version                            │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  VendorItemMapping                                          │
│  (works for both suppliers and retail stores)               │
├─────────────────────────────────────────────────────────────┤
│  - VendorId (supplier or store identifier)                  │
│  - VendorType (Supplier | RetailStore)                      │
│  - ItemDescription (key - normalized)                       │
│  - IngredientId                                             │
│  - IngredientSku                                            │
│  - Confidence                                               │
│  - UsageCount                                               │
│  - LastUsedAt                                               │
│  - ExpectedUnitPrice (for variance detection)               │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  Expense (non-inventory costs)                              │
├─────────────────────────────────────────────────────────────┤
│  - ExpenseId (PK)                                           │
│  - OrganizationId, SiteId                                   │
│  - Category (Rent, Utilities, Insurance, etc.)              │
│  - Description                                              │
│  - Amount, Currency                                         │
│  - ExpenseDate                                              │
│  - VendorId (optional)                                      │
│  - DocumentUrl (receipt/supporting doc)                     │
│  - PaymentMethod                                            │
│  - CreatedAt, CreatedBy                                     │
└─────────────────────────────────────────────────────────────┘
```

---

## Implementation Phases

### Phase 1: Core Document Capture (MVP)
- Unified upload API for invoices and receipts
- Azure Document Intelligence integration (both models)
- Auto-detect document type or accept hint
- Basic extraction and display in back-office
- Manual line-item mapping UI
- Confirmation workflow
- Events emitted for downstream systems
- Mobile-friendly photo capture for receipts

### Phase 2: Email Ingestion
- Dedicated mailbox setup per organization
- Email parsing (extract PDF/image attachments)
- Auto-detect invoice vs receipt from content
- Automatic processing trigger

### Phase 3: Smart Mapping
- Vendor-specific mapping persistence (suppliers and stores)
- Auto-mapping for previously confirmed items
- Fuzzy matching suggestions with confidence scores
- Learning algorithm improves from confirmations
- Common retailer recognition (Costco, Walmart item formats)

### Phase 4: Expense Tracking
- General expense recording (rent, utilities, etc.)
- Category management
- Receipt/document upload for supporting docs
- Basic expense reporting

### Phase 5: Advanced Features
- Price variance alerts (supplier raised prices)
- Vendor comparison (same item, different prices)
- Purchase order matching for invoices
- Delivery reconciliation
- Cost trend analytics and dashboards
- Budget tracking and alerts

---

## User Workflows

### Workflow 1: Chef photographs Costco receipt

```
1. Chef buys supplies at Costco
2. Opens POS app → "Add Purchase" → takes photo of receipt
3. System uploads, runs receipt OCR
4. Extracts: "COSTCO WHOLESALE", items like "ORG EGGS 24CT", totals
5. System auto-maps known items (eggs → eggs-organic)
6. Flags unknown items: "KS PARCHMENT PPR" → suggests "parchment-paper" or "new item"
7. Chef confirms or adjusts mappings
8. Inventory updated, cost recorded
```

### Workflow 2: Owner forwards supplier invoice email

```
1. Owner receives Sysco invoice PDF via email
2. Forwards to invoices@myrestaurant.darkvelocity.io
3. System extracts attachment, runs invoice OCR
4. Extracts: invoice #, line items with product codes, due date
5. Most items auto-map (Sysco codes previously learned)
6. Creates accounts payable entry (due in 30 days)
7. Owner reviews in back-office, confirms
8. Inventory updated, payable tracked
```

### Workflow 3: Manager uploads utility bill

```
1. Manager receives gas bill
2. Back-office → Expenses → "Add Expense"
3. Selects category "Utilities", enters amount, uploads PDF
4. No SKU mapping needed - just recorded as expense
5. Shows up in P&L reports under utilities
```

### Workflow 4: Quick grocery run (bulk categorization)

```
1. Staff makes emergency grocery run for small items
2. Takes photo of receipt (15 items, $47 total)
3. System extracts but many items are generic ("PRODUCE", "DAIRY")
4. User chooses "Bulk categorize" → marks all as "Food supplies"
5. Total cost recorded without line-item detail
6. Good enough for small purchases, maintains cost visibility
```

---

## Open Questions

1. **Supplier identification**: How do we identify/create supplier records from invoice extraction? Match by name? Tax ID?

2. **Multi-currency**: How do we handle invoices in different currencies? Convert at confirmation time?

3. **Duplicate detection**: How do we prevent the same invoice from being entered twice?

4. **Approval workflow**: Do invoices need manager approval before confirmation?

5. **Integration depth**: Should confirmed invoices automatically create inventory receipts, or just emit events for separate processing?

---

## Security Considerations

- Invoice documents may contain sensitive supplier/financial data
- Blob storage should be private (signed URLs for access)
- User must have site-level permissions to upload/view invoices
- Audit trail for all confirmations and changes
- Consider PII in supplier contact information
