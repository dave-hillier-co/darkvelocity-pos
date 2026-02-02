# Supplier Invoice Capture - Requirements & Architecture

## Problem Statement

Restaurants need visibility into their costs to manage profitability. Supplier invoices arrive through multiple channels (email, paper, supplier portals) in unstructured formats. Manual data entry is time-consuming and error-prone.

**Goal**: Automatically capture, process, and categorize supplier invoices to provide accurate cost tracking and inventory reconciliation.

---

## Functional Requirements

### 1. Document Ingestion

**Sources**:
- **Email forwarding**: Restaurant forwards invoice emails to a dedicated inbox (e.g., `invoices-{siteId}@darkvelocity.io`)
- **Image upload**: Staff photographs paper invoices via mobile app
- **File upload**: PDF/image upload through back-office
- **Future**: Direct supplier integrations (EDI, API)

**Supported Formats**:
- PDF (text-based and scanned)
- Images (JPEG, PNG, HEIC)
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
// Document received from any source
public sealed record SupplierInvoiceDocumentReceived : DomainEvent
{
    public override string EventType => "supplier-invoice.document.received";

    public required Guid InvoiceId { get; init; }
    public required DocumentSource Source { get; init; }  // Email, Upload, Photo
    public required string OriginalFilename { get; init; }
    public required string StorageUrl { get; init; }
    public required string ContentType { get; init; }
    public required long FileSizeBytes { get; init; }
    public string? EmailFrom { get; init; }
    public string? EmailSubject { get; init; }
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
POST   /api/orgs/{orgId}/sites/{siteId}/supplier-invoices
       - Upload new invoice document
       - Body: multipart/form-data with file + metadata

GET    /api/orgs/{orgId}/sites/{siteId}/supplier-invoices
       - List invoices with filtering (status, date range, supplier)

GET    /api/orgs/{orgId}/sites/{siteId}/supplier-invoices/{id}
       - Get invoice details including extracted data

POST   /api/orgs/{orgId}/sites/{siteId}/supplier-invoices/{id}/process
       - Trigger (re)processing

PATCH  /api/orgs/{orgId}/sites/{siteId}/supplier-invoices/{id}/lines/{idx}
       - Update line item mapping

POST   /api/orgs/{orgId}/sites/{siteId}/supplier-invoices/{id}/confirm
       - Confirm invoice for downstream processing

DELETE /api/orgs/{orgId}/sites/{siteId}/supplier-invoices/{id}
       - Reject/delete invoice

--- Expenses ---

POST   /api/orgs/{orgId}/sites/{siteId}/expenses
GET    /api/orgs/{orgId}/sites/{siteId}/expenses
GET    /api/orgs/{orgId}/sites/{siteId}/expenses/{id}
PATCH  /api/orgs/{orgId}/sites/{siteId}/expenses/{id}
DELETE /api/orgs/{orgId}/sites/{siteId}/expenses/{id}

--- Mappings ---

GET    /api/orgs/{orgId}/suppliers/{supplierId}/item-mappings
POST   /api/orgs/{orgId}/suppliers/{supplierId}/item-mappings
       - Manually create/update mapping
```

---

## OCR Service Integration

### Recommended: Azure AI Document Intelligence

- Pre-built "Invoice" model handles common invoice formats
- Returns structured fields: vendor, invoice number, line items, totals
- Confidence scores per field
- Handles rotated/skewed images

### Alternative: AWS Textract

- AnalyzeExpense API for invoices/receipts
- Similar structured output

### Abstraction Layer

```csharp
public interface IDocumentIntelligenceService
{
    Task<InvoiceExtractionResult> ExtractInvoiceAsync(
        Stream document,
        string contentType,
        CancellationToken cancellationToken = default);
}

public record InvoiceExtractionResult(
    VendorInfo? Vendor,
    string? InvoiceNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    IReadOnlyList<ExtractedLineItem> LineItems,
    MonetaryAmount? Subtotal,
    MonetaryAmount? Tax,
    MonetaryAmount? Total,
    decimal OverallConfidence,
    IReadOnlyList<ExtractionWarning> Warnings);
```

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
│  SupplierInvoice                                            │
├─────────────────────────────────────────────────────────────┤
│  - InvoiceId (PK)                                           │
│  - OrganizationId, SiteId                                   │
│  - Status (Received, Processing, Extracted, Confirmed...)   │
│  - Source (Email, Upload, Photo)                            │
│  - DocumentUrl                                              │
│  - SupplierId (nullable until identified)                   │
│  - SupplierName, SupplierAddress                            │
│  - InvoiceNumber, InvoiceDate, DueDate                      │
│  - Lines[] { description, qty, unit, unitPrice, total,      │
│              mappedIngredientId, mappingConfidence }        │
│  - Subtotal, Tax, DeliveryFee, Total                        │
│  - Currency                                                  │
│  - ExtractionConfidence                                     │
│  - ConfirmedAt, ConfirmedBy                                 │
│  - CreatedAt, UpdatedAt, Version                            │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  SupplierItemMapping                                        │
├─────────────────────────────────────────────────────────────┤
│  - SupplierId                                               │
│  - SupplierItemDescription (key)                            │
│  - IngredientId                                             │
│  - IngredientSku                                            │
│  - Confidence                                               │
│  - UsageCount                                               │
│  - LastUsedAt                                               │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  Expense                                                    │
├─────────────────────────────────────────────────────────────┤
│  - ExpenseId (PK)                                           │
│  - OrganizationId, SiteId                                   │
│  - Category (enum)                                          │
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

### Phase 1: Core Invoice Capture (MVP)
- Document upload API (PDF, images)
- Azure Document Intelligence integration
- Basic extraction and display
- Manual line-item mapping
- Confirmation workflow
- Event emission for downstream

### Phase 2: Email Ingestion
- Dedicated mailbox setup
- Email parsing (extract attachments)
- Automatic processing trigger

### Phase 3: Smart Mapping
- Supplier-specific mapping persistence
- Auto-mapping for known items
- Fuzzy matching suggestions
- Learning from confirmations

### Phase 4: Expense Tracking
- General expense recording
- Category management
- Receipt upload
- Basic reporting

### Phase 5: Advanced Features
- Price variance alerts
- Supplier comparison
- Purchase order matching
- Delivery reconciliation
- Cost trend analytics

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
