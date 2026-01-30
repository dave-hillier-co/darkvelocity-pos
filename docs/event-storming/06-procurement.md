# Event Storming: Procurement Domain

## Overview

The Procurement domain handles the complete purchasing lifecycle from supplier management through purchase order creation, delivery receipt, invoice matching, and supplier performance tracking. This domain ensures cost control, timely replenishment, and strong supplier relationships.

---

## Domain Purpose

- **Supplier Management**: Maintain supplier catalog with contracts, contacts, and performance metrics
- **Purchase Order Management**: Create, approve, and track purchase orders
- **Delivery Processing**: Receive, inspect, and reconcile deliveries
- **Invoice Matching**: Three-way match (PO, delivery, invoice)
- **Cost Control**: Track purchasing costs and identify savings opportunities
- **Compliance**: Ensure proper approvals and documentation

---

## Actors

| Actor | Description | Typical Actions |
|-------|-------------|-----------------|
| **Procurement Manager** | Oversees purchasing | Manage suppliers, approve POs |
| **Site Manager** | Venue responsibility | Create POs, receive deliveries |
| **Receiving Staff** | Handles deliveries | Inspect deliveries, record receipts |
| **Finance/AP** | Accounts payable | Match invoices, process payments |
| **Supplier** | External vendor | Confirm orders, deliver goods |
| **System** | Automated processes | Suggest reorders, alert on issues |
| **Quality Inspector** | Quality control | Inspect deliveries, flag issues |

---

## Aggregates

### Supplier Aggregate

Represents a vendor who provides goods to the organization.

```
Supplier
├── Id: Guid
├── OrgId: Guid
├── Name: string
├── Code: string
├── Status: SupplierStatus
├── Type: SupplierType
├── Contacts: List<Contact>
├── Addresses: List<Address>
├── PaymentTerms: PaymentTerms
├── TaxInfo: TaxInfo
├── Categories: List<SupplierCategory>
├── LeadTime: int (days)
├── MinimumOrderValue?: decimal
├── PreferredDeliveryDays: List<DayOfWeek>
├── Rating: decimal
├── Certifications: List<Certification>
├── Contract?: ContractInfo
├── AssignedSites: List<Guid>
├── Notes: string
└── Metadata: Dictionary<string, string>
```

**Invariants:**
- Code must be unique within organization
- Must have at least one contact
- Must have valid payment terms

### PurchaseOrder Aggregate

Represents a purchase order to a supplier.

```
PurchaseOrder
├── Id: Guid
├── OrgId: Guid
├── SiteId: Guid
├── PONumber: string
├── SupplierId: Guid
├── Status: POStatus
├── Lines: List<POLine>
├── Subtotal: decimal
├── TaxTotal: decimal
├── Total: decimal
├── RequestedDeliveryDate?: DateTime
├── ConfirmedDeliveryDate?: DateTime
├── ShippingAddress: Address
├── Notes?: string
├── CreatedBy: Guid
├── CreatedAt: DateTime
├── SubmittedAt?: DateTime
├── ApprovedBy?: Guid
├── ApprovedAt?: DateTime
├── CancelledAt?: DateTime
├── Deliveries: List<DeliveryReference>
└── InvoiceReference?: InvoiceReference
```

**Invariants:**
- Total = Sum(Lines.Total) + TaxTotal
- Cannot modify after approved (except cancel)
- Must have at least one line
- Delivery date must be in future when creating

### POLine Entity

```
POLine
├── Id: Guid
├── IngredientId: Guid
├── IngredientName: string
├── Quantity: decimal
├── Unit: string
├── UnitPrice: decimal
├── TaxRate: decimal
├── TaxAmount: decimal
├── Total: decimal
├── ReceivedQuantity: decimal
├── RemainingQuantity: decimal
├── Status: POLineStatus
└── Notes?: string
```

### Delivery Aggregate

Represents a physical delivery received.

```
Delivery
├── Id: Guid
├── OrgId: Guid
├── SiteId: Guid
├── DeliveryNumber: string
├── PurchaseOrderId?: Guid
├── SupplierId: Guid
├── Status: DeliveryStatus
├── Lines: List<DeliveryLine>
├── ReceivedBy: Guid
├── ReceivedAt: DateTime
├── InspectedBy?: Guid
├── InspectedAt?: DateTime
├── SupplierDeliveryNote?: string
├── Temperature?: decimal
├── QualityNotes?: string
├── Photos: List<PhotoReference>
├── Discrepancies: List<Discrepancy>
├── AcceptedAt?: DateTime
├── AcceptedBy?: Guid
└── Notes?: string
```

### DeliveryLine Entity

```
DeliveryLine
├── Id: Guid
├── POLineId?: Guid
├── IngredientId: Guid
├── IngredientName: string
├── ExpectedQuantity?: decimal
├── ReceivedQuantity: decimal
├── AcceptedQuantity: decimal
├── RejectedQuantity: decimal
├── Unit: string
├── UnitCost: decimal
├── TotalCost: decimal
├── BatchNumber?: string
├── ExpiryDate?: DateTime
├── InspectionResult: InspectionResult
├── RejectionReason?: string
└── StockBatchId?: Guid
```

### Invoice Aggregate

```
Invoice
├── Id: Guid
├── OrgId: Guid
├── InvoiceNumber: string
├── SupplierId: Guid
├── PurchaseOrderId?: Guid
├── DeliveryId?: Guid
├── Status: InvoiceStatus
├── InvoiceDate: DateTime
├── DueDate: DateTime
├── Lines: List<InvoiceLine>
├── Subtotal: decimal
├── TaxTotal: decimal
├── Total: decimal
├── PaidAmount: decimal
├── BalanceDue: decimal
├── PaymentStatus: PaymentStatus
├── MatchStatus: MatchStatus
├── MatchDiscrepancies: List<MatchDiscrepancy>
├── ApprovedBy?: Guid
├── ApprovedAt?: DateTime
└── PaidAt?: DateTime
```

---

## PO State Machine

```
┌─────────────┐
│   Draft     │
└──────┬──────┘
       │
       │ Submit
       ▼
┌─────────────┐
│  Submitted  │
└──────┬──────┘
       │
       ├────────────────────┬────────────────────┐
       │ Below threshold    │ Above threshold    │
       ▼                    ▼                    │
┌─────────────┐     ┌─────────────┐              │
│ Auto-Approved│     │  Pending    │              │
│             │     │  Approval   │              │
└──────┬──────┘     └──────┬──────┘              │
       │                   │                     │
       │                   ├────────┐            │
       │                   │ Approve│ Reject     │
       │                   ▼        ▼            │
       │            ┌─────────┐  ┌─────────┐     │
       └───────────>│ Approved │  │ Rejected │     │
                    └────┬────┘  └─────────┘     │
                         │                       │
                         │ Send to Supplier      │
                         ▼                       │
                  ┌─────────────┐                │
                  │    Sent     │                │
                  └──────┬──────┘                │
                         │                       │
                         │ Supplier Confirms     │
                         ▼                       │
                  ┌─────────────┐                │
                  │  Confirmed  │                │
                  └──────┬──────┘                │
                         │                       │
                         │ Delivery              │
                         ▼                       │
                  ┌─────────────┐                │
                  │ Partially   │─┐              │
                  │ Received    │ │ More         │
                  └──────┬──────┘ │ Deliveries   │
                         │        │              │
                         │<───────┘              │
                         │                       │
                         │ Full Receipt          │
                         ▼                       │
                  ┌─────────────┐                │
                  │  Received   │                │
                  └──────┬──────┘                │
                         │                       │
                         │ Invoice Matched       │
                         ▼                       │
                  ┌─────────────┐                │
                  │   Closed    │                │
                  └─────────────┘                │
                                                 │
From any state except Closed:                    │
                         │ Cancel                │
                         ▼                       │
                  ┌─────────────┐<───────────────┘
                  │  Cancelled  │
                  └─────────────┘
```

---

## Commands

### Supplier Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `CreateSupplier` | Add new supplier | Code unique | Procurement Manager |
| `UpdateSupplier` | Modify supplier details | Supplier exists | Procurement Manager |
| `DeactivateSupplier` | Stop ordering from supplier | No open POs | Procurement Manager |
| `ReactivateSupplier` | Resume supplier relationship | Supplier inactive | Procurement Manager |
| `AddSupplierContact` | Add contact person | Supplier exists | Procurement Manager |
| `UpdatePaymentTerms` | Change payment terms | Supplier exists | Finance |
| `AssignSupplierToSite` | Allow site to order | Supplier and site exist | Procurement Manager |
| `RemoveSupplierFromSite` | Disallow ordering | Assignment exists | Procurement Manager |
| `RateSupplier` | Record performance rating | Delivery received | Site Manager |

### Purchase Order Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `CreatePurchaseOrder` | Draft new PO | Supplier active | Site Manager |
| `AddPOLine` | Add item to PO | PO in draft | Site Manager |
| `UpdatePOLine` | Modify line details | PO in draft | Site Manager |
| `RemovePOLine` | Delete line | PO in draft, >1 lines | Site Manager |
| `SubmitPurchaseOrder` | Submit for approval | Valid PO | Site Manager |
| `ApprovePurchaseOrder` | Approve PO | PO pending approval | Procurement Manager |
| `RejectPurchaseOrder` | Reject PO | PO pending approval | Procurement Manager |
| `SendToSupplier` | Transmit PO | PO approved | System/Manager |
| `RecordSupplierConfirmation` | Mark confirmed | PO sent | Site Manager |
| `UpdateDeliveryDate` | Change expected date | PO confirmed | Site Manager |
| `CancelPurchaseOrder` | Cancel PO | Not fully received | Procurement Manager |

### Delivery Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `CreateDelivery` | Record delivery arrival | Supplier exists | Receiving Staff |
| `AddDeliveryLine` | Add received item | Delivery open | Receiving Staff |
| `UpdateDeliveryLine` | Modify received qty | Delivery open | Receiving Staff |
| `RecordInspection` | Record quality inspection | Line exists | Quality Inspector |
| `RecordDiscrepancy` | Note issues | Delivery exists | Receiving Staff |
| `AcceptDelivery` | Approve delivery | Inspection complete | Site Manager |
| `RejectDelivery` | Refuse delivery | Inspection failed | Site Manager |
| `LinkToPurchaseOrder` | Connect to PO | Both exist | System/Staff |
| `CreateInventoryBatches` | Move to inventory | Delivery accepted | System |

### Invoice Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `CreateInvoice` | Record supplier invoice | Supplier exists | Finance |
| `AddInvoiceLine` | Add line item | Invoice open | Finance |
| `MatchToDelivery` | Link to delivery | Both exist | Finance |
| `MatchToPurchaseOrder` | Link to PO | Both exist | Finance |
| `PerformThreeWayMatch` | Compare PO/Delivery/Invoice | All linked | System |
| `ApproveInvoice` | Approve for payment | Match acceptable | Finance Manager |
| `DisputeInvoice` | Raise discrepancy | Match issues | Finance |
| `RecordPayment` | Mark as paid | Invoice approved | Finance |

---

## Domain Events

### Supplier Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `SupplierCreated` | New supplier added | SupplierId, Name, Code, PaymentTerms | CreateSupplier |
| `SupplierUpdated` | Details changed | SupplierId, ChangedFields | UpdateSupplier |
| `SupplierDeactivated` | Supplier disabled | SupplierId, Reason | DeactivateSupplier |
| `SupplierReactivated` | Supplier re-enabled | SupplierId | ReactivateSupplier |
| `SupplierContactAdded` | Contact added | SupplierId, Contact | AddSupplierContact |
| `PaymentTermsUpdated` | Terms changed | SupplierId, OldTerms, NewTerms | UpdatePaymentTerms |
| `SupplierAssignedToSite` | Site can order | SupplierId, SiteId | AssignSupplierToSite |
| `SupplierRemovedFromSite` | Site cannot order | SupplierId, SiteId | RemoveSupplierFromSite |
| `SupplierRated` | Performance recorded | SupplierId, Rating, DeliveryId | RateSupplier |

### Purchase Order Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `PODrafted` | New PO created | POId, PONumber, SupplierId, SiteId | CreatePurchaseOrder |
| `POLineAdded` | Line added | POId, LineId, Ingredient, Qty, Price | AddPOLine |
| `POLineUpdated` | Line modified | POId, LineId, OldValues, NewValues | UpdatePOLine |
| `POLineRemoved` | Line deleted | POId, LineId | RemovePOLine |
| `POSubmitted` | Submitted for approval | POId, Total, SubmittedBy | SubmitPurchaseOrder |
| `POAutoApproved` | Below threshold auto-approved | POId, Total | SubmitPurchaseOrder |
| `POApproved` | Manually approved | POId, ApprovedBy, ApprovedAt | ApprovePurchaseOrder |
| `PORejected` | Rejected | POId, RejectedBy, Reason | RejectPurchaseOrder |
| `POSentToSupplier` | Transmitted | POId, SentAt, Method | SendToSupplier |
| `POConfirmed` | Supplier confirmed | POId, ConfirmedDate, SupplierRef | RecordSupplierConfirmation |
| `PODeliveryDateUpdated` | Expected date changed | POId, OldDate, NewDate | UpdateDeliveryDate |
| `POPartiallyReceived` | Some items received | POId, DeliveryId, ReceivedLines | Delivery acceptance |
| `POFullyReceived` | All items received | POId, FinalDeliveryId | Delivery acceptance |
| `POCancelled` | PO cancelled | POId, Reason, CancelledBy | CancelPurchaseOrder |
| `POClosed` | PO completed | POId, TotalCost, DeliveryCount | Invoice matched |

### Delivery Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `DeliveryReceived` | Delivery arrived | DeliveryId, SupplierId, POId, ReceivedBy | CreateDelivery |
| `DeliveryLineRecorded` | Item recorded | DeliveryId, LineId, Ingredient, Qty | AddDeliveryLine |
| `DeliveryInspected` | Quality check done | DeliveryId, LineId, Result, Inspector | RecordInspection |
| `DeliveryDiscrepancyRecorded` | Issue noted | DeliveryId, Type, Description, Photos | RecordDiscrepancy |
| `DeliveryAccepted` | Approved | DeliveryId, AcceptedBy, TotalValue | AcceptDelivery |
| `DeliveryRejected` | Refused | DeliveryId, Reason, RejectedBy | RejectDelivery |
| `DeliveryLinkedToPO` | Connected to PO | DeliveryId, POId | LinkToPurchaseOrder |
| `InventoryBatchesCreated` | Stock created | DeliveryId, BatchIds | CreateInventoryBatches |

### Invoice Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `InvoiceReceived` | Invoice recorded | InvoiceId, InvoiceNumber, SupplierId, Total | CreateInvoice |
| `InvoiceMatchedToDelivery` | Linked to delivery | InvoiceId, DeliveryId | MatchToDelivery |
| `InvoiceMatchedToPO` | Linked to PO | InvoiceId, POId | MatchToPurchaseOrder |
| `ThreeWayMatchCompleted` | Match performed | InvoiceId, POId, DeliveryId, MatchResult | PerformThreeWayMatch |
| `InvoiceMatchDiscrepancyDetected` | Match issue found | InvoiceId, DiscrepancyType, Details | PerformThreeWayMatch |
| `InvoiceApproved` | Approved for payment | InvoiceId, ApprovedBy | ApproveInvoice |
| `InvoiceDisputed` | Discrepancy raised | InvoiceId, DisputeReason, Details | DisputeInvoice |
| `InvoicePaid` | Payment recorded | InvoiceId, PaidAmount, PaymentRef | RecordPayment |

---

## Event Details

### PODrafted

```csharp
public record PODrafted : DomainEvent
{
    public override string EventType => "procurement.po.drafted";

    public required Guid PurchaseOrderId { get; init; }
    public required Guid SiteId { get; init; }
    public required string PONumber { get; init; }
    public required Guid SupplierId { get; init; }
    public required string SupplierName { get; init; }
    public DateTime? RequestedDeliveryDate { get; init; }
    public required Guid CreatedBy { get; init; }
    public required DateTime CreatedAt { get; init; }
    public string? Notes { get; init; }
}
```

### POApproved

```csharp
public record POApproved : DomainEvent
{
    public override string EventType => "procurement.po.approved";

    public required Guid PurchaseOrderId { get; init; }
    public required string PONumber { get; init; }
    public required Guid SupplierId { get; init; }
    public required decimal Total { get; init; }
    public required int LineCount { get; init; }
    public required Guid ApprovedBy { get; init; }
    public required DateTime ApprovedAt { get; init; }
    public string? ApprovalNotes { get; init; }
    public required bool WasAutoApproved { get; init; }
}
```

### DeliveryReceived

```csharp
public record DeliveryReceived : DomainEvent
{
    public override string EventType => "procurement.delivery.received";

    public required Guid DeliveryId { get; init; }
    public required Guid SiteId { get; init; }
    public required string DeliveryNumber { get; init; }
    public required Guid SupplierId { get; init; }
    public required string SupplierName { get; init; }
    public Guid? PurchaseOrderId { get; init; }
    public string? PONumber { get; init; }
    public required int LineCount { get; init; }
    public string? SupplierDeliveryNote { get; init; }
    public decimal? Temperature { get; init; }
    public required Guid ReceivedBy { get; init; }
    public required DateTime ReceivedAt { get; init; }
}
```

### ThreeWayMatchCompleted

```csharp
public record ThreeWayMatchCompleted : DomainEvent
{
    public override string EventType => "procurement.invoice.three_way_match";

    public required Guid InvoiceId { get; init; }
    public required string InvoiceNumber { get; init; }
    public required Guid PurchaseOrderId { get; init; }
    public required string PONumber { get; init; }
    public required Guid DeliveryId { get; init; }
    public required MatchResult Result { get; init; }
    public required decimal POTotal { get; init; }
    public required decimal DeliveryTotal { get; init; }
    public required decimal InvoiceTotal { get; init; }
    public IReadOnlyList<MatchDiscrepancy>? Discrepancies { get; init; }
    public required DateTime MatchedAt { get; init; }
}

public enum MatchResult
{
    ExactMatch,
    WithinTolerance,
    QuantityMismatch,
    PriceMismatch,
    MissingItems,
    ExtraItems,
    MultipleIssues
}

public record MatchDiscrepancy
{
    public Guid? IngredientId { get; init; }
    public string IngredientName { get; init; }
    public DiscrepancyType Type { get; init; }
    public decimal? ExpectedValue { get; init; }
    public decimal? ActualValue { get; init; }
    public decimal? Variance { get; init; }
}
```

---

## Policies (Event Reactions)

### When POApproved

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Send to Supplier | Auto-transmit if configured | Integration |
| Notify Supplier | Email/EDI notification | Notifications |
| Update Budget | Reserve budget amount | Accounting |
| Create Calendar Event | Add expected delivery | Scheduling |

### When DeliveryAccepted

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Create Inventory Batches | Move to stock | Inventory |
| Update PO Status | Mark lines as received | Purchase Orders |
| Calculate Costs | Update WAC | Inventory |
| Rate Supplier | Prompt for rating | Suppliers |
| Alert for Invoice | Prompt AP for invoice | Finance |

### When ThreeWayMatchCompleted (Success)

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Auto-Approve Invoice | If within tolerance | Invoices |
| Schedule Payment | Based on payment terms | Finance |
| Close PO | If fully received and matched | Purchase Orders |
| Update Supplier Metrics | Record accuracy | Suppliers |

### When ThreeWayMatchCompleted (Discrepancy)

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Alert AP | Notify of discrepancy | Notifications |
| Hold Payment | Block auto-payment | Finance |
| Log for Supplier Review | Track for performance | Suppliers |
| Create Dispute Task | Action item for resolution | Tasks |

### When InvoicePaid

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Post AP Journal | Debit AP, credit bank | Accounting |
| Update Supplier Balance | Track payables | Suppliers |
| Close Invoice | Mark as paid | Invoices |
| Archive Documents | Move to archive | Documents |

---

## Read Models / Projections

### SupplierCatalogView

```csharp
public record SupplierCatalogView
{
    public Guid SupplierId { get; init; }
    public string Name { get; init; }
    public string Code { get; init; }
    public SupplierStatus Status { get; init; }
    public string PrimaryContact { get; init; }
    public string Email { get; init; }
    public string Phone { get; init; }
    public decimal Rating { get; init; }
    public int OpenPOCount { get; init; }
    public decimal OpenPOValue { get; init; }
    public decimal BalanceDue { get; init; }
    public IReadOnlyList<string> Categories { get; init; }
    public int LeadTimeDays { get; init; }
    public DateTime? LastOrderDate { get; init; }
    public DateTime? LastDeliveryDate { get; init; }
}
```

### PurchaseOrderView

```csharp
public record PurchaseOrderView
{
    public Guid Id { get; init; }
    public string PONumber { get; init; }
    public POStatus Status { get; init; }
    public string SupplierName { get; init; }
    public string SiteName { get; init; }
    public int LineCount { get; init; }
    public decimal Subtotal { get; init; }
    public decimal TaxTotal { get; init; }
    public decimal Total { get; init; }
    public decimal ReceivedValue { get; init; }
    public decimal ReceivedPercent { get; init; }
    public DateTime? RequestedDeliveryDate { get; init; }
    public DateTime? ConfirmedDeliveryDate { get; init; }
    public string CreatedByName { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? ApprovedByName { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public IReadOnlyList<POLineView> Lines { get; init; }
    public IReadOnlyList<DeliverySummary> Deliveries { get; init; }
}
```

### DeliveryInspectionView

```csharp
public record DeliveryInspectionView
{
    public Guid DeliveryId { get; init; }
    public string DeliveryNumber { get; init; }
    public string SupplierName { get; init; }
    public string? PONumber { get; init; }
    public DeliveryStatus Status { get; init; }
    public IReadOnlyList<DeliveryLineInspection> Lines { get; init; }
    public IReadOnlyList<DiscrepancyInfo> Discrepancies { get; init; }
    public IReadOnlyList<string> PhotoUrls { get; init; }
    public decimal? Temperature { get; init; }
    public string? QualityNotes { get; init; }
    public DateTime ReceivedAt { get; init; }
    public string ReceivedByName { get; init; }
}

public record DeliveryLineInspection
{
    public Guid LineId { get; init; }
    public string IngredientName { get; init; }
    public decimal? ExpectedQty { get; init; }
    public decimal ReceivedQty { get; init; }
    public decimal AcceptedQty { get; init; }
    public decimal RejectedQty { get; init; }
    public string Unit { get; init; }
    public InspectionResult Result { get; init; }
    public string? RejectionReason { get; init; }
    public string? BatchNumber { get; init; }
    public DateTime? ExpiryDate { get; init; }
}
```

### SupplierPerformanceReport

```csharp
public record SupplierPerformanceReport
{
    public Guid SupplierId { get; init; }
    public string SupplierName { get; init; }
    public DateRange Period { get; init; }

    // Order Metrics
    public int TotalOrders { get; init; }
    public decimal TotalOrderValue { get; init; }
    public decimal AverageOrderValue { get; init; }

    // Delivery Metrics
    public int TotalDeliveries { get; init; }
    public int OnTimeDeliveries { get; init; }
    public decimal OnTimeDeliveryRate { get; init; }
    public decimal AverageLeadTimeDays { get; init; }

    // Quality Metrics
    public decimal QualityAcceptanceRate { get; init; }
    public int RejectedDeliveries { get; init; }
    public decimal RejectionRate { get; init; }
    public IReadOnlyList<QualityIssue> TopQualityIssues { get; init; }

    // Pricing Metrics
    public decimal AveragePriceVariance { get; init; }
    public IReadOnlyList<PriceVarianceItem> SignificantPriceChanges { get; init; }

    // Rating
    public decimal AverageRating { get; init; }
    public IReadOnlyList<RatingHistoryEntry> RatingHistory { get; init; }
}
```

### OutstandingPOsView

```csharp
public record OutstandingPOsView
{
    public Guid SiteId { get; init; }
    public IReadOnlyList<OutstandingPO> PendingApproval { get; init; }
    public IReadOnlyList<OutstandingPO> AwaitingDelivery { get; init; }
    public IReadOnlyList<OutstandingPO> PartiallyReceived { get; init; }
    public IReadOnlyList<OutstandingPO> OverdueDelivery { get; init; }
    public decimal TotalOutstandingValue { get; init; }
}

public record OutstandingPO
{
    public Guid POId { get; init; }
    public string PONumber { get; init; }
    public POStatus Status { get; init; }
    public string SupplierName { get; init; }
    public decimal Total { get; init; }
    public decimal ReceivedValue { get; init; }
    public decimal OutstandingValue { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }
    public int DaysOverdue { get; init; }
}
```

### APAgingReport

```csharp
public record APAgingReport
{
    public Guid OrgId { get; init; }
    public DateTime AsOfDate { get; init; }
    public IReadOnlyList<SupplierAging> BySupplier { get; init; }
    public AgingBuckets TotalAging { get; init; }
}

public record SupplierAging
{
    public Guid SupplierId { get; init; }
    public string SupplierName { get; init; }
    public AgingBuckets Buckets { get; init; }
    public decimal TotalDue { get; init; }
}

public record AgingBuckets
{
    public decimal Current { get; init; }      // Not yet due
    public decimal Days1To30 { get; init; }
    public decimal Days31To60 { get; init; }
    public decimal Days61To90 { get; init; }
    public decimal Over90Days { get; init; }
    public decimal Total { get; init; }
}
```

---

## Bounded Context Relationships

### Upstream Contexts (This domain provides data to)

| Context | Relationship | Data Provided |
|---------|--------------|---------------|
| Inventory | Published Language | Receipt data, batch info, costs |
| Accounting | Published Language | AP postings, expense accruals |
| Reporting | Published Language | Spend analytics, supplier metrics |

### Downstream Contexts (This domain consumes from)

| Context | Relationship | Data Consumed |
|---------|--------------|---------------|
| Inventory | Customer/Supplier | Stock levels for reordering |
| Site | Customer/Supplier | Site info for delivery |
| Ingredient Catalog | Customer/Supplier | Item master data |

---

## Process Flows

### Purchase Order Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│    Site     │   │   POGrain   │   │  Approver   │   │  Supplier   │
│   Manager   │   │             │   │             │   │             │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ CreatePO        │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │ AddLines (×N)   │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │ Submit          │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ If > threshold  │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ Approve         │                 │
       │                 │<────────────────│                 │
       │                 │                 │                 │
       │                 │ Send            │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
       │                 │                 │    Confirm      │
       │                 │<────────────────────────────────│
       │                 │                 │                 │
       │ PO Confirmed    │                 │                 │
       │<────────────────│                 │                 │
       │                 │                 │                 │
```

### Delivery Receipt Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│  Receiving  │   │  Delivery   │   │  Inventory  │   │   POGrain   │
│    Staff    │   │   Grain     │   │   Grains    │   │             │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ Create Delivery │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │ Record Lines    │                 │                 │
       │ (count items)   │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │ Inspect         │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │ Accept          │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ Create Batches  │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ Update PO       │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
       │                 │ POPartiallyReceived /            │
       │                 │ POFullyReceived │                 │
       │                 │<────────────────────────────────│
       │                 │                 │                 │
       │   Complete      │                 │                 │
       │<────────────────│                 │                 │
       │                 │                 │                 │
```

### Three-Way Match Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│   Finance   │   │  Invoice    │   │   POGrain   │   │  Delivery   │
│             │   │   Grain     │   │             │   │   Grain     │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ Enter Invoice   │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │ Match to PO     │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ Get PO Data     │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ Get Delivery    │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
       │                 │ Compare All 3   │                 │
       │                 │────┐            │                 │
       │                 │<───┘            │                 │
       │                 │                 │                 │
       │ Match Result    │                 │                 │
       │<────────────────│                 │                 │
       │                 │                 │                 │
       │ (if match OK)   │                 │                 │
       │ Approve         │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ Close PO        │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
```

---

## Business Rules

### Purchase Order Rules

1. **Minimum Order**: Some suppliers have minimum order values
2. **Approval Thresholds**: POs above threshold require approval
3. **Lead Time**: Requested delivery must be >= supplier lead time
4. **Budget Check**: Optional budget validation before approval
5. **Duplicate Prevention**: Warn on duplicate items or recent orders

### Delivery Rules

1. **PO Matching**: Deliveries should reference a PO when possible
2. **Quantity Tolerance**: Configurable over/under receive tolerance
3. **Quality Inspection**: Certain categories require inspection
4. **Temperature Check**: Cold items require temperature recording
5. **Expiry Requirements**: Minimum days-to-expiry for acceptance

### Invoice Matching Rules

1. **Three-Way Match**: Compare PO qty/price, delivery qty, invoice amount
2. **Tolerance Levels**: Small variances auto-approved
3. **Price Variance**: Flag if unit price differs from PO
4. **Quantity Variance**: Flag if invoiced qty differs from received
5. **Payment Terms**: Honor supplier payment terms for scheduling

---

## Event Type Registry

```csharp
public static class ProcurementEventTypes
{
    // Supplier
    public const string SupplierCreated = "procurement.supplier.created";
    public const string SupplierUpdated = "procurement.supplier.updated";
    public const string SupplierDeactivated = "procurement.supplier.deactivated";
    public const string SupplierReactivated = "procurement.supplier.reactivated";
    public const string SupplierContactAdded = "procurement.supplier.contact_added";
    public const string PaymentTermsUpdated = "procurement.supplier.payment_terms_updated";
    public const string SupplierAssignedToSite = "procurement.supplier.site_assigned";
    public const string SupplierRemovedFromSite = "procurement.supplier.site_removed";
    public const string SupplierRated = "procurement.supplier.rated";

    // Purchase Orders
    public const string PODrafted = "procurement.po.drafted";
    public const string POLineAdded = "procurement.po.line_added";
    public const string POLineUpdated = "procurement.po.line_updated";
    public const string POLineRemoved = "procurement.po.line_removed";
    public const string POSubmitted = "procurement.po.submitted";
    public const string POAutoApproved = "procurement.po.auto_approved";
    public const string POApproved = "procurement.po.approved";
    public const string PORejected = "procurement.po.rejected";
    public const string POSentToSupplier = "procurement.po.sent";
    public const string POConfirmed = "procurement.po.confirmed";
    public const string PODeliveryDateUpdated = "procurement.po.delivery_date_updated";
    public const string POPartiallyReceived = "procurement.po.partially_received";
    public const string POFullyReceived = "procurement.po.fully_received";
    public const string POCancelled = "procurement.po.cancelled";
    public const string POClosed = "procurement.po.closed";

    // Deliveries
    public const string DeliveryReceived = "procurement.delivery.received";
    public const string DeliveryLineRecorded = "procurement.delivery.line_recorded";
    public const string DeliveryInspected = "procurement.delivery.inspected";
    public const string DeliveryDiscrepancyRecorded = "procurement.delivery.discrepancy_recorded";
    public const string DeliveryAccepted = "procurement.delivery.accepted";
    public const string DeliveryRejected = "procurement.delivery.rejected";
    public const string DeliveryLinkedToPO = "procurement.delivery.linked_to_po";
    public const string InventoryBatchesCreated = "procurement.delivery.batches_created";

    // Invoices
    public const string InvoiceReceived = "procurement.invoice.received";
    public const string InvoiceMatchedToDelivery = "procurement.invoice.matched_to_delivery";
    public const string InvoiceMatchedToPO = "procurement.invoice.matched_to_po";
    public const string ThreeWayMatchCompleted = "procurement.invoice.three_way_match";
    public const string InvoiceMatchDiscrepancyDetected = "procurement.invoice.match_discrepancy_detected";
    public const string InvoiceApproved = "procurement.invoice.approved";
    public const string InvoiceDisputed = "procurement.invoice.disputed";
    public const string InvoicePaid = "procurement.invoice.paid";
}
```

---

## Hotspots & Risks

### High Complexity Areas

| Area | Complexity | Mitigation |
|------|------------|------------|
| **Three-Way Match** | Multiple documents, variances | Clear tolerance rules |
| **Partial Receipts** | Tracking across deliveries | Line-level status tracking |
| **Price Changes** | PO vs invoice pricing | Alert and approval workflow |
| **Multi-Site Suppliers** | Different terms per site | Site-level supplier config |

### Performance Considerations

| Concern | Impact | Strategy |
|---------|--------|----------|
| **PO Search** | Many open POs | Index by status, date |
| **Supplier Lookup** | Frequent queries | Cache active suppliers |
| **Match Calculation** | Complex comparison | Pre-compute on document entry |

### Audit Requirements

| Requirement | Implementation |
|-------------|----------------|
| **Approval Trail** | Log all approvals with timestamp |
| **Price Changes** | Track all price modifications |
| **Delivery Photos** | Retain for configurable period |
| **Match History** | Archive match results |
