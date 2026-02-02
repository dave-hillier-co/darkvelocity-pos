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

## Email Ingestion (Implemented)

### Architecture Overview

Email ingestion uses a webhook-based approach where external email services (SendGrid, Mailgun, AWS SES) forward parsed emails to our API endpoint.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                       EMAIL FLOW                                         │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   Supplier/Store Email                                                   │
│          │                                                               │
│          ▼                                                               │
│   ┌──────────────────┐                                                  │
│   │  Email Provider   │  (Gmail, Outlook, etc.)                         │
│   └────────┬─────────┘                                                  │
│            │ Forward to:                                                 │
│            │ invoices-{orgId}-{siteId}@darkvelocity.io                  │
│            ▼                                                             │
│   ┌──────────────────┐                                                  │
│   │  Email Gateway    │  (SendGrid, Mailgun, AWS SES)                   │
│   │  Inbound Parse    │                                                  │
│   └────────┬─────────┘                                                  │
│            │ Webhook POST                                                │
│            ▼                                                             │
│   ┌──────────────────┐                                                  │
│   │  /api/webhooks/   │                                                  │
│   │  email/inbound    │                                                  │
│   └────────┬─────────┘                                                  │
│            │                                                             │
│            ▼                                                             │
│   ┌──────────────────┐     ┌──────────────────┐                         │
│   │ EmailInboxGrain   │ ──▶│ PurchaseDocument │                         │
│   │ (per-site inbox)  │     │     Grain        │                         │
│   └──────────────────┘     └──────────────────┘                         │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Inbox Address Format

Each site gets a unique inbox address in the format:
- `invoices-{orgId}-{siteId}@darkvelocity.io` - for supplier invoices
- `receipts-{orgId}-{siteId}@darkvelocity.io` - for retail receipts

The address parsing extracts:
- Organization ID (for multi-tenant isolation)
- Site ID (for per-location processing)
- Inbox type (invoices/receipts) for document type hint

### Components

#### IEmailIngestionService
```csharp
public interface IEmailIngestionService
{
    Task<ParsedEmail> ParseEmailAsync(Stream emailContent, string contentType, CancellationToken cancellationToken = default);
    Task<ParsedEmail> ParseMimeEmailAsync(string mimeContent, CancellationToken cancellationToken = default);
    SiteEmailInfo? ParseInboxAddress(string emailAddress);
}
```

Handles parsing webhook payloads from various email providers (JSON format from SendGrid/Mailgun) and extracting site info from inbox addresses.

#### EmailInboxGrain
```csharp
public interface IEmailInboxGrain : IGrainWithStringKey
{
    Task<EmailInboxSnapshot> InitializeAsync(InitializeEmailInboxCommand command);
    Task<EmailProcessingResultInternal> ProcessEmailAsync(ProcessIncomingEmailCommand command);
    Task<EmailInboxSnapshot> UpdateSettingsAsync(UpdateInboxSettingsCommand command);
    Task ActivateInboxAsync();
    Task DeactivateInboxAsync();
    Task<bool> IsMessageProcessedAsync(string messageId);
    Task<EmailInboxSnapshot> GetSnapshotAsync();
}
```

One grain per site, responsible for:
- Validating incoming emails (sender whitelist, attachment checks)
- Deduplication (tracks recent message IDs)
- Creating PurchaseDocument grains for each valid attachment
- Auto-processing if enabled (triggers OCR extraction)
- Statistics tracking (emails received, documents created)

#### Validation Rules

1. **Active inbox check**: Inactive inboxes reject all emails
2. **Duplicate detection**: Message IDs tracked to prevent reprocessing
3. **Sender validation**: Optional whitelist by domain or email address
4. **Attachment validation**:
   - Must have at least one document attachment (PDF, image, spreadsheet)
   - Size limits enforced (default 25MB per attachment)
   - Non-document attachments (signatures, logos) filtered out

#### Document Type Detection

Auto-detects document type from email metadata:
- Subject contains "receipt" or "order confirmation" → Receipt
- Subject contains "invoice" or "bill" → Invoice
- Filename contains type hints
- Falls back to inbox's default document type

### API Endpoints

```
POST /api/webhooks/email/inbound
    - Webhook endpoint for email providers
    - Parses email, extracts site, processes attachments

POST /api/orgs/{orgId}/sites/{siteId}/email-inbox
    - Initialize inbox for a site
    - Body: { inboxAddress?, defaultDocumentType?, autoProcess? }

GET /api/orgs/{orgId}/sites/{siteId}/email-inbox
    - Get inbox status and statistics

PATCH /api/orgs/{orgId}/sites/{siteId}/email-inbox
    - Update inbox settings (allowed senders, max sizes, etc.)

POST /api/orgs/{orgId}/sites/{siteId}/email-inbox/activate
    - Activate the inbox

POST /api/orgs/{orgId}/sites/{siteId}/email-inbox/deactivate
    - Deactivate the inbox (stops accepting emails)

POST /api/orgs/{orgId}/sites/{siteId}/email-inbox/test
    - Send a test email (development only)
```

### Events

```csharp
EmailReceived     // Email accepted and logged
EmailProcessed    // Documents successfully created
EmailProcessingFailed  // Error during processing
EmailRejected     // Email rejected (unauthorized sender, no attachments, etc.)
```

### Configuration Options

| Setting | Default | Description |
|---------|---------|-------------|
| `IsActive` | true | Whether inbox accepts emails |
| `AllowedSenderDomains` | [] | Whitelist of sender domains (empty = allow all) |
| `AllowedSenderEmails` | [] | Whitelist of specific addresses |
| `MaxAttachmentSizeBytes` | 25MB | Maximum size per attachment |
| `DefaultDocumentType` | Invoice | Type when auto-detection fails |
| `AutoProcess` | true | Automatically trigger OCR on receipt |

### Email Provider Setup

#### SendGrid Inbound Parse

1. Configure MX records to point to SendGrid
2. Set up Inbound Parse webhook to `POST /api/webhooks/email/inbound`
3. Enable "Post the raw, full MIME message" for complete data

#### AWS SES

1. Configure SES to receive email for your domain
2. Set up SNS notification → Lambda → API call
3. Or use SES Actions to invoke Lambda directly

#### Mailgun

1. Configure route to forward to webhook URL
2. Mailgun posts multipart/form-data with parsed email

### Legacy Options (Not Implemented)

For reference, other approaches considered but not currently implemented:

#### Option A: Dedicated Mailbox Polling
- Azure Logic App / AWS SES + Lambda polls mailbox
- Simple but adds latency

#### Option B: IMAP Integration
- Restaurant provides email credentials (OAuth)
- We scan inbox for invoice-like attachments
- Complex, privacy concerns, not recommended

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

### Phase 1: Core Document Capture (MVP) ✅ COMPLETE
- Unified upload API for invoices and receipts
- Azure Document Intelligence integration (both models)
- Auto-detect document type or accept hint
- Basic extraction and display in back-office
- Manual line-item mapping UI
- Confirmation workflow
- Events emitted for downstream systems
- Mobile-friendly photo capture for receipts

**Implemented files:**
- `Events/PurchaseDocumentEvents.cs` - Domain events
- `State/PurchaseDocumentState.cs` - Grain state
- `Grains/IPurchaseDocumentGrain.cs` - Grain interface
- `Grains/PurchaseDocumentGrain.cs` - Grain implementation
- `Services/IDocumentIntelligenceService.cs` - OCR abstraction
- `Services/StubDocumentIntelligenceService.cs` - Development stub
- `Endpoints/PurchaseDocumentEndpoints.cs` - API endpoints
- `Contracts/PurchaseDocumentContracts.cs` - Request DTOs

### Phase 2: Email Ingestion ✅ COMPLETE
- Per-site inbox addresses (`invoices-{orgId}-{siteId}@darkvelocity.io`)
- Webhook endpoint for email provider integration
- Email parsing (extract PDF/image attachments)
- Sender validation (domain/email whitelist)
- Auto-detect invoice vs receipt from content
- Automatic processing trigger (configurable)
- Deduplication via message ID tracking

**Implemented files:**
- `Services/IEmailIngestionService.cs` - Email parsing interface
- `Services/StubEmailIngestionService.cs` - Development stub
- `Events/EmailIngestionEvents.cs` - Email domain events
- `State/EmailInboxState.cs` - Inbox grain state
- `Grains/IEmailInboxGrain.cs` - Inbox grain interface
- `Grains/EmailInboxGrain.cs` - Inbox grain implementation
- `Endpoints/EmailIngestionEndpoints.cs` - Webhook and management API
- `Contracts/EmailIngestionContracts.cs` - Request DTOs

### Phase 3: Smart Mapping ✅ COMPLETE
- Vendor-specific mapping persistence (suppliers and stores)
- Auto-mapping for previously confirmed items
- Fuzzy matching suggestions with confidence scores
- Learning algorithm improves from confirmations
- Common retailer recognition (Costco, Walmart item formats)
- Receipt abbreviation expansion (CHKN → CHICKEN, ORG → ORGANIC, etc.)

**Implemented files:**
- `State/VendorItemMappingState.cs` - Grain state with exact/pattern mappings
- `Events/VendorItemMappingEvents.cs` - Mapping domain events
- `Grains/IVendorItemMappingGrain.cs` - Grain interface with commands/results
- `Grains/VendorItemMappingGrain.cs` - Grain implementation
- `Services/IFuzzyMatchingService.cs` - Fuzzy matching with Levenshtein distance
- `Endpoints/VendorMappingEndpoints.cs` - Mapping management API
- `Contracts/VendorMappingContracts.cs` - Request DTOs

**Key Features:**
- Exact description matching (case-insensitive, normalized)
- Product code matching for supplier SKUs
- Fuzzy pattern matching using token overlap
- Auto-learning from confirmed purchase documents
- Receipt abbreviation expansion dictionary
- Vendor normalization (Costco variants → "costco")

**Auto-mapping Flow:**
```
1. Document extraction completes
2. PurchaseDocumentGrain calls AttemptAutoMappingAsync()
3. For each line item:
   a. Look up exact match by description or product code
   b. If found → auto-map with high confidence
   c. If not found → get fuzzy suggestions from patterns
4. On confirmation, learn new mappings from manual/suggested items
```

### Phase 4: Expense Tracking ✅ COMPLETE
- General expense recording (rent, utilities, etc.)
- Full expense lifecycle (Pending → Approved → Paid)
- Category management (16 built-in categories)
- Receipt/document upload for supporting docs
- Basic expense reporting by category
- Recurring expense support
- Index grain for querying and aggregation

**Implemented files:**
- `State/ExpenseState.cs` - Grain state with full expense data model
- `Events/ExpenseEvents.cs` - Domain events for expense lifecycle
- `Grains/IExpenseGrain.cs` - Grain interface with commands/results
- `Grains/ExpenseGrain.cs` - Grain implementation + ExpenseIndexGrain
- `Endpoints/ExpenseEndpoints.cs` - REST API endpoints
- `Contracts/ExpenseContracts.cs` - Request DTOs

**Key Features:**
- **16 expense categories**: Rent, Utilities, Insurance, Equipment, Maintenance, Marketing, Supplies, Professional, BankFees, CreditCardFees, Licenses, Travel, Subscriptions, Taxes, Payroll, Other
- **Payment methods**: Cash, Check, CreditCard, DebitCard, BankTransfer, Other
- **Status workflow**: Pending → Approved/Rejected → Paid/Voided
- **Approval tracking**: Who approved, when, with optional notes
- **Document attachment**: Link receipt/invoice files to expenses
- **Recurring expenses**: Pattern support for monthly/weekly/annual expenses
- **Tax tracking**: Tax amounts and tax-deductible flags
- **Tag support**: Free-form tags for filtering/grouping
- **Index grain**: Efficient querying without loading individual expense grains

**Expense Workflow:**
```
1. Record expense (status: Pending)
2. Review and approve/reject
   - Approved → can be marked as paid
   - Rejected → terminal state with reason
3. Mark as paid (status: Paid) with payment details
4. Can void at any state with reason
```

**API Endpoints:**
```
POST   /api/orgs/{orgId}/sites/{siteId}/expenses          - Create expense
GET    /api/orgs/{orgId}/sites/{siteId}/expenses          - List with filtering
GET    /api/orgs/{orgId}/sites/{siteId}/expenses/{id}     - Get expense
PATCH  /api/orgs/{orgId}/sites/{siteId}/expenses/{id}     - Update expense
DELETE /api/orgs/{orgId}/sites/{siteId}/expenses/{id}     - Void expense
POST   /api/orgs/{orgId}/sites/{siteId}/expenses/{id}/approve   - Approve
POST   /api/orgs/{orgId}/sites/{siteId}/expenses/{id}/reject    - Reject
POST   /api/orgs/{orgId}/sites/{siteId}/expenses/{id}/pay       - Mark paid
POST   /api/orgs/{orgId}/sites/{siteId}/expenses/{id}/document  - Attach doc
GET    /api/orgs/{orgId}/sites/{siteId}/expenses/summary/by-category - Category totals
```

### Phase 5: Advanced Features (DEFERRED)
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

## LLM-Enhanced Processing (Future Enhancement)

Large Language Models can significantly improve both OCR accuracy and item mapping by understanding context rather than relying purely on pattern matching. This section describes an architecture for incorporating LLMs with appropriate fallbacks.

### Client-Side vs Server-Side Processing

A key architectural decision is **where** LLM processing occurs. We recommend a **client-side-first approach** for document extraction, with server-side processing for item mapping.

#### Benefits of Client-Side LLM (Document Extraction)

| Benefit | Description |
|---------|-------------|
| **Privacy** | Document images never leave the device. Sensitive supplier pricing and financial data stays local. Only structured JSON is transmitted to server. |
| **Cost Model (BYOK)** | Restaurant provides their own OpenAI/Anthropic API key. Platform doesn't bear LLM costs. Heavy users pay more, light users pay less - natural scaling. |
| **Regulatory Compliance** | Some jurisdictions have data residency requirements. Client-side processing means no document transmission across borders. |
| **Real-time Feedback** | When photographing a receipt at POS, immediate quality feedback ("image is blurry, retake") without server round-trip. |
| **Reduced Server Load** | Extraction is CPU/GPU intensive. Offloading to client reduces infrastructure costs. |

#### What Stays Server-Side

- **SKU/ingredient mapping** - Requires access to organization's inventory catalog
- **Workflow orchestration** - Approval, payment tracking, status management
- **Learning from confirmations** - Pattern learning needs centralized state
- **Analytics and reporting** - Aggregate data across documents
- **Fallback processing** - For clients without LLM configured

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│  CLIENT (POS Tablet / Back-office Browser)                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   1. User captures document (photo/upload)                               │
│          │                                                               │
│          ▼                                                               │
│   ┌──────────────────┐                                                  │
│   │  Quality Check    │◄─── Local image analysis                        │
│   │  (blur, lighting) │     Immediate feedback to user                  │
│   └────────┬─────────┘                                                  │
│            │                                                             │
│            ▼  LLM configured?                                            │
│   ┌──────────────────┐     ┌──────────────────┐                         │
│   │  YES: Client LLM  │     │  NO: Upload raw   │                        │
│   │  Vision Extract   │     │  to server        │                        │
│   │  (org's API key)  │     │  (fallback mode)  │                        │
│   └────────┬─────────┘     └────────┬─────────┘                         │
│            │                         │                                   │
│            ▼                         │                                   │
│   ┌──────────────────┐               │                                   │
│   │  Structured JSON  │               │                                   │
│   │  (no raw image)   │               │                                   │
│   └────────┬─────────┘               │                                   │
│            │                         │                                   │
└────────────┼─────────────────────────┼───────────────────────────────────┘
             │                         │
             ▼                         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  SERVER (Orleans)                                                        │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   ┌──────────────────┐     ┌──────────────────┐                         │
│   │  Receive JSON     │     │  Fallback OCR     │◄─── Azure Document    │
│   │  (pre-extracted)  │     │  (raw document)   │     Intelligence      │
│   └────────┬─────────┘     └────────┬─────────┘                         │
│            │                         │                                   │
│            └────────────┬────────────┘                                   │
│                         ▼                                                │
│            ┌──────────────────┐                                          │
│            │  PurchaseDocument │                                          │
│            │  Grain            │                                          │
│            └────────┬─────────┘                                          │
│                     │                                                    │
│                     ▼                                                    │
│            ┌──────────────────┐                                          │
│            │  Item Mapping     │◄─── Server-side (needs ingredient      │
│            │  (exact → fuzzy   │     catalog access)                     │
│            │   → LLM semantic) │                                         │
│            └──────────────────┘                                          │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Client-Side LLM Configuration

Organizations configure their LLM provider in organization settings. The client apps read this configuration and use it for document extraction.

```typescript
// apps/pos/src/services/llm-config.ts

interface LlmConfig {
  provider: 'openai' | 'anthropic' | 'azure-openai' | 'none';
  apiKey: string;           // Encrypted, stored in org settings
  model: string;            // e.g., 'gpt-4o', 'claude-sonnet-4-20250514'
  visionModel?: string;     // e.g., 'gpt-4o' (if different from default)
  maxTokens: number;        // Cost control
  enabled: boolean;         // Feature flag
}

interface ExtractionConfig {
  enableClientExtraction: boolean;   // Use client-side LLM
  enableQualityCheck: boolean;       // Pre-flight image quality
  fallbackToServer: boolean;         // If client extraction fails
  confidenceThreshold: number;       // Below this → flag for review
}
```

**API Key Security:**
- Keys stored encrypted in organization settings (server-side)
- Fetched on app initialization, held in memory only
- Never logged or transmitted except to LLM provider
- Can be rotated without app update

### Client-Side Extraction Flow

```typescript
// apps/pos/src/services/document-extraction.ts

interface ExtractedDocument {
  vendorName?: string;
  documentNumber?: string;
  documentDate?: string;
  lineItems: ExtractedLineItem[];
  subtotal?: number;
  tax?: number;
  total?: number;
  confidence: number;
  extractedAt: string;
  extractionMethod: 'client-llm' | 'server-ocr' | 'server-llm';
}

async function extractDocument(
  imageData: Blob,
  config: LlmConfig,
  documentType: 'invoice' | 'receipt'
): Promise<ExtractedDocument> {

  // 1. Quality check (local, no API call)
  const quality = await checkImageQuality(imageData);
  if (quality.score < 0.5) {
    throw new QualityError('Image too blurry or dark. Please retake.');
  }

  // 2. Client-side LLM extraction if configured
  if (config.enabled && config.apiKey) {
    const prompt = buildExtractionPrompt(documentType);
    const result = await callLlmVision(config, imageData, prompt);

    if (result.confidence >= config.confidenceThreshold) {
      return {
        ...result,
        extractionMethod: 'client-llm'
      };
    }
  }

  // 3. Fallback: upload to server for processing
  return await uploadForServerExtraction(imageData, documentType);
}
```

### LLM Extraction Prompt

```
You are extracting data from a {document_type} image for a restaurant's
cost tracking system.

Extract the following information in JSON format:
{
  "vendor_name": "Store or supplier name",
  "document_number": "Invoice/receipt number if visible",
  "document_date": "YYYY-MM-DD format",
  "line_items": [
    {
      "description": "Item description (expand abbreviations if obvious)",
      "product_code": "SKU/product code if visible",
      "quantity": 1.0,
      "unit": "each/lb/kg/case/etc",
      "unit_price": 0.00,
      "total": 0.00
    }
  ],
  "subtotal": 0.00,
  "tax": 0.00,
  "tip": 0.00,
  "total": 0.00,
  "payment_method": "cash/credit/debit if visible",
  "confidence": 0.0-1.0
}

Guidelines:
- Expand common abbreviations: CHKN=Chicken, ORG=Organic, LG=Large, etc.
- If a value is unclear, omit it rather than guess
- Set confidence based on image quality and extraction certainty
- For receipts, items may be abbreviated - do your best to interpret

Return ONLY valid JSON, no explanation.
```

### Server-Side Item Mapping

Item mapping remains server-side because it requires access to the organization's ingredient catalog. The server can optionally use LLM for semantic matching when fuzzy matching confidence is low.

```csharp
public class ItemMappingService : IItemMappingService
{
    private readonly IFuzzyMatchingService _fuzzyMatcher;
    private readonly ILlmMappingService? _llmMapper;  // Optional

    public async Task<MappingResult> MapItemAsync(
        string vendorDescription,
        string vendorId,
        IReadOnlyList<IngredientInfo> ingredients,
        MappingOptions options)
    {
        // 1. Exact match (instant, free)
        var exact = await GetExactMatchAsync(vendorDescription, vendorId);
        if (exact != null) return exact;

        // 2. Fuzzy match (fast, free)
        var fuzzy = _fuzzyMatcher.FindBestMatch(vendorDescription, ingredients);
        if (fuzzy?.Confidence >= options.MinFuzzyConfidence)
            return fuzzy;

        // 3. LLM semantic match (if configured, costs money)
        if (_llmMapper != null && options.AllowLlmFallback)
        {
            var candidates = _fuzzyMatcher.FindTopMatches(vendorDescription, ingredients, 10);
            var llmResult = await _llmMapper.SelectBestMatchAsync(vendorDescription, candidates);
            if (llmResult?.Confidence >= options.MinLlmConfidence)
                return llmResult;
        }

        // 4. Return best fuzzy match or null (needs manual mapping)
        return fuzzy;
    }
}
```

### Cost and Latency Summary

| Stage | Location | Cost | Latency | Notes |
|-------|----------|------|---------|-------|
| Image quality check | Client | Free | <100ms | Local analysis |
| LLM vision extraction | Client | $0.01-0.10/doc | 3-10s | Org pays via BYOK |
| Server OCR fallback | Server | $0.01/page | 2-5s | Platform cost |
| Exact mapping | Server | Free | <10ms | Database lookup |
| Fuzzy mapping | Server | Free | <50ms | In-memory |
| LLM semantic mapping | Server | $0.01-0.03/call | 1-3s | Platform cost (optional) |

### Configuration Tiers

| Tier | Client Extraction | Server Mapping | Cost Bearer |
|------|-------------------|----------------|-------------|
| **Free** | None (upload raw) | Exact + Fuzzy only | Platform (OCR) |
| **Standard** | None (upload raw) | Exact + Fuzzy + LLM | Platform |
| **BYOK** | Client LLM (org key) | Exact + Fuzzy + LLM | Organization (extraction) + Platform (mapping) |
| **Enterprise** | Client LLM (org key) | Full LLM (org key passthrough) | Organization (all LLM) |

### Fallback Strategy

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         EXTRACTION FALLBACK                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  1. Client LLM configured?                                               │
│     ├─► YES: Extract on client                                          │
│     │        └─► Success? Send JSON to server                           │
│     │        └─► Failure? Fall through to step 2                        │
│     └─► NO: Fall through to step 2                                      │
│                                                                          │
│  2. Upload raw document to server                                        │
│     └─► Server runs Azure Document Intelligence                         │
│         └─► Low confidence? Server LLM fallback (if enabled)            │
│             └─► Still low? Flag for manual review                       │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│                         MAPPING FALLBACK (Server)                        │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  1. Exact match from learned mappings ──► Found? Done                   │
│                                                                          │
│  2. Fuzzy token matching ──► Confidence ≥60%? Done                      │
│                                                                          │
│  3. LLM semantic matching ──► Confidence ≥80%? Done                     │
│                                                                          │
│  4. Flag for manual mapping                                              │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Privacy Considerations

With client-side extraction:

1. **Document images stay on device** - Only structured JSON transmitted
2. **LLM calls go directly to provider** - Platform never sees raw documents
3. **Audit trail preserved** - Server logs what was submitted, not raw images
4. **Optional raw storage** - User can opt-in to store originals for audit

```typescript
// When submitting extracted document
interface SubmitDocumentRequest {
  extractedData: ExtractedDocument;       // Always sent
  rawDocument?: Blob;                      // Optional, user consent required
  storeRawDocument: boolean;               // Explicit opt-in
  extractionMethod: 'client-llm' | 'server';
}
```

### Implementation Phases

**Phase A: Server-side baseline**
- Azure Document Intelligence integration (current stub)
- Confidence tracking and logging
- Manual review workflow for low-confidence extractions

**Phase B: Client-side extraction**
- LLM configuration in organization settings
- Client-side extraction service in POS/back-office apps
- Quality check before extraction
- Fallback to server when needed

**Phase C: Enhanced mapping**
- Server-side LLM for semantic matching
- Optional BYOK passthrough for enterprise tier
- A/B testing to measure accuracy improvement

---

## Security Considerations

- Invoice documents may contain sensitive supplier/financial data
- Blob storage should be private (signed URLs for access)
- User must have site-level permissions to upload/view invoices
- Audit trail for all confirmations and changes
- Consider PII in supplier contact information
