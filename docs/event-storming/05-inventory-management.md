# Event Storming: Inventory Management Domain

## Overview

The Inventory Management domain tracks all physical stock at each site, manages stock movements, implements FIFO (First-In-First-Out) consumption, monitors stock levels, and calculates weighted average costs. This domain is critical for cost control, waste management, and ensuring menu items can be fulfilled.

---

## Domain Purpose

- **Stock Tracking**: Maintain accurate quantity-on-hand for all ingredients at each site
- **Batch Management**: Track individual batches with costs, expiry dates, and traceability
- **FIFO Consumption**: Ensure oldest stock is used first for accurate costing
- **Cost Calculation**: Compute weighted average costs for recipe costing
- **Level Monitoring**: Alert when stock falls below configurable thresholds
- **Movement Tracking**: Record all stock movements with full audit trail

---

## Actors

| Actor | Description | Typical Actions |
|-------|-------------|-----------------|
| **Receiving Staff** | Staff receiving deliveries | Record receipts, inspect quality |
| **Kitchen Staff** | Food preparation staff | Consume ingredients, record waste |
| **Inventory Manager** | Stock management responsibility | Adjust counts, manage transfers |
| **Site Manager** | Venue oversight | Approve adjustments, review reports |
| **Order System** | Automated consumption | Deduct ingredients on order settlement |
| **Procurement System** | Reordering automation | Trigger reorder points |
| **System** | Automated monitoring | Expiry alerts, low stock alerts |

---

## Aggregates

### Inventory Aggregate (per Ingredient per Site)

Tracks the stock position for a specific ingredient at a specific site.

```
Inventory
├── Id: Guid (composite: OrgId:SiteId:IngredientId)
├── OrgId: Guid
├── SiteId: Guid
├── IngredientId: Guid
├── IngredientName: string
├── Unit: string (base unit)
├── Batches: List<StockBatch>
├── QuantityOnHand: decimal
├── QuantityReserved: decimal
├── QuantityAvailable: decimal
├── ReorderPoint: decimal
├── ReorderQuantity: decimal
├── ParLevel: decimal
├── MaxLevel: decimal
├── WeightedAverageCost: decimal
├── LastReceivedAt?: DateTime
├── LastConsumedAt?: DateTime
├── LastCountedAt?: DateTime
└── StockLevel: StockLevel
```

**Invariants:**
- QuantityAvailable = QuantityOnHand - QuantityReserved
- QuantityOnHand = Sum of all batch quantities
- WeightedAverageCost is recalculated on receipt
- Cannot have negative quantity on hand

### StockBatch Entity

Represents a received batch of inventory with traceability.

```
StockBatch
├── Id: Guid
├── BatchNumber: string
├── ReceivedDate: DateTime
├── ExpiryDate?: DateTime
├── Quantity: decimal (current remaining)
├── OriginalQuantity: decimal
├── UnitCost: decimal
├── TotalCost: decimal
├── SupplierId?: Guid
├── DeliveryId?: Guid
├── PurchaseOrderLineId?: Guid
├── Status: BatchStatus
├── Location?: string (storage location)
└── Notes?: string
```

**Invariants:**
- Quantity cannot exceed OriginalQuantity
- TotalCost = UnitCost × OriginalQuantity
- Consumed batches (Quantity = 0) marked as Exhausted

### IngredientCatalog Aggregate

Master data for ingredients across the organization.

```
Ingredient
├── Id: Guid
├── OrgId: Guid
├── Name: string
├── Sku: string
├── Category: IngredientCategory
├── BaseUnit: string
├── ConversionUnits: List<UnitConversion>
├── TrackingMethod: TrackingMethod
├── Allergens: List<Allergen>
├── DietaryFlags: List<DietaryFlag>
├── ShelfLife?: int (days)
├── StorageRequirements?: string
├── PreferredSupplierId?: Guid
├── AlternateSuppliers: List<Guid>
├── IsActive: bool
└── Metadata: Dictionary<string, string>
```

### StockMovement Value Object

Records any movement of inventory.

```
StockMovement
├── Id: Guid
├── Timestamp: DateTime
├── Type: MovementType
├── Quantity: decimal
├── BatchId?: Guid
├── UnitCost: decimal
├── TotalCost: decimal
├── Reason: string
├── ReferenceType?: string
├── ReferenceId?: Guid
├── PerformedBy: Guid
└── Notes?: string
```

---

## Stock Level State Machine

```
                           Receive/Adjust Up
                         ┌──────────────────┐
                         │                  │
                         ▼                  │
┌──────────┐        ┌─────────┐        ┌─────────┐
│   Out    │───────>│  Low    │───────>│ Normal  │
│ Of Stock │        │  Stock  │        │         │
└──────────┘        └─────────┘        └────┬────┘
     ▲                   ▲                  │
     │                   │                  │
     │                   │                  │ Consume
     │    Consume        │    Consume       │
     │    to zero        │    below         ▼
     │                   │    reorder  ┌─────────┐
     │                   │             │ Above   │
     │                   └─────────────│  Par    │
     │                                 └─────────┘
     │                                      │
     │            Consume to zero           │
     └──────────────────────────────────────┘

Transitions:
- OutOfStock: QuantityAvailable = 0
- Low: QuantityAvailable <= ReorderPoint
- Normal: ReorderPoint < QuantityAvailable <= ParLevel
- AbovePar: QuantityAvailable > ParLevel
```

---

## Commands

### Receipt Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `ReceiveBatch` | Record new inventory arrival | Valid ingredient, site | Receiving Staff |
| `ReceiveFromDelivery` | Receive from purchase order | Delivery exists | Receiving Staff |
| `ReceiveTransfer` | Accept transfer from other site | Transfer in progress | Receiving Staff |
| `AdjustReceipt` | Correct receipt error | Receipt exists, not consumed | Inventory Manager |
| `RejectBatch` | Mark batch as rejected | Batch exists | Receiving Staff |

### Consumption Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `ConsumeForOrder` | Deduct for order settlement | Stock available | Order System |
| `ConsumeForWaste` | Record spoilage/waste | Stock exists | Kitchen Staff |
| `ConsumeForSample` | Record sampling | Stock exists | Kitchen Staff |
| `ConsumeForTransfer` | Remove for outgoing transfer | Stock exists | Inventory Manager |
| `ReverseConsumption` | Undo consumption | Previous consumption exists | Inventory Manager |

### Adjustment Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `AdjustQuantity` | Physical count adjustment | Inventory exists | Inventory Manager |
| `RecordPhysicalCount` | Full count entry | Inventory exists | Inventory Manager |
| `WriteOffExpired` | Remove expired stock | Expired batches exist | System/Manager |
| `MergeBatches` | Combine same batches | Same ingredient, same cost | Inventory Manager |

### Transfer Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `InitiateTransfer` | Start inter-site transfer | Stock available | Inventory Manager |
| `ConfirmTransferSent` | Mark as shipped | Transfer initiated | Source Site |
| `ConfirmTransferReceived` | Complete transfer | Transfer in transit | Receiving Site |
| `CancelTransfer` | Abort transfer | Transfer not received | Inventory Manager |

### Configuration Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `SetReorderPoint` | Configure alert threshold | Inventory exists | Site Manager |
| `SetParLevel` | Configure target level | Inventory exists | Site Manager |
| `SetMaxLevel` | Configure maximum | Inventory exists | Site Manager |
| `EnableIngredientAtSite` | Activate tracking | Ingredient in catalog | Site Manager |
| `DisableIngredientAtSite` | Stop tracking | Inventory exists | Site Manager |

---

## Domain Events

### Receipt Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `StockBatchReceived` | New inventory arrived | BatchId, IngredientId, Qty, Cost, ExpiryDate | ReceiveBatch |
| `DeliveryReceived` | Received from PO | DeliveryId, BatchIds, TotalQty, TotalCost | ReceiveFromDelivery |
| `TransferReceived` | Received from other site | TransferId, SourceSiteId, BatchId | ReceiveTransfer |
| `ReceiptAdjusted` | Corrected receipt | ReceiptId, OldQty, NewQty, Reason | AdjustReceipt |
| `BatchRejected` | Rejected on receipt | BatchId, Reason, SupplierId | RejectBatch |

### Consumption Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `StockConsumed` | Ingredients used | IngredientId, Qty, BatchIds, Reason, Cost | Various consumption |
| `StockConsumedForOrder` | Order consumption | OrderId, LineItems, TotalCost | ConsumeForOrder |
| `StockWasted` | Spoilage recorded | IngredientId, Qty, Reason, WasteCategory, Cost | ConsumeForWaste |
| `StockSampled` | Sample usage | IngredientId, Qty, Reason | ConsumeForSample |
| `StockTransferredOut` | Transfer outgoing | TransferId, DestinationSiteId, BatchId, Qty | ConsumeForTransfer |
| `ConsumptionReversed` | Undo consumption | OriginalEventId, Qty, Reason | ReverseConsumption |

### Batch Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `BatchExhausted` | Batch fully consumed | BatchId, TotalConsumed, Duration | Automatic on depletion |
| `BatchExpired` | Past expiry date | BatchId, ExpiryDate, RemainingQty | System check |
| `BatchWrittenOff` | Expired stock removed | BatchId, Qty, Cost, Reason | WriteOffExpired |
| `BatchesMerged` | Batches combined | SourceBatchIds, TargetBatchId | MergeBatches |

### Level Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `LowStockAlertTriggered` | Below reorder point | IngredientId, CurrentQty, ReorderPoint | Automatic |
| `OutOfStockAlertTriggered` | Zero available | IngredientId, LastConsumedAt | Automatic |
| `StockLevelNormalized` | Back above reorder | IngredientId, CurrentQty | After receipt |
| `ParLevelExceeded` | Above max desired | IngredientId, CurrentQty, ParLevel | After receipt |

### Adjustment Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `QuantityAdjusted` | Count correction | IngredientId, OldQty, NewQty, Variance, Reason | AdjustQuantity |
| `PhysicalCountRecorded` | Full count completed | IngredientId, CountedQty, SystemQty, Variance | RecordPhysicalCount |
| `CostAdjusted` | Cost correction | BatchId, OldCost, NewCost, Reason | System/Manual |
| `WeightedAverageCostRecalculated` | WAC updated | IngredientId, OldWAC, NewWAC | After receipt |

### Transfer Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `TransferInitiated` | Transfer started | TransferId, SourceSite, DestSite, Items | InitiateTransfer |
| `TransferSent` | Shipped to destination | TransferId, ShippedAt | ConfirmTransferSent |
| `TransferCompleted` | Received at destination | TransferId, ReceivedAt | ConfirmTransferReceived |
| `TransferCancelled` | Transfer aborted | TransferId, Reason | CancelTransfer |

---

## Event Details

### StockBatchReceived

```csharp
public record StockBatchReceived : DomainEvent
{
    public override string EventType => "inventory.stock.batch_received";

    public required Guid BatchId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required Guid SiteId { get; init; }
    public required string BatchNumber { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required decimal UnitCost { get; init; }
    public required decimal TotalCost { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public Guid? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public Guid? DeliveryId { get; init; }
    public Guid? PurchaseOrderId { get; init; }
    public string? Location { get; init; }
    public required Guid ReceivedBy { get; init; }
    public required DateTime ReceivedAt { get; init; }
    public string? Notes { get; init; }
}
```

### StockConsumedForOrder

```csharp
public record StockConsumedForOrder : DomainEvent
{
    public override string EventType => "inventory.stock.consumed_for_order";

    public required Guid OrderId { get; init; }
    public required Guid SiteId { get; init; }
    public required IReadOnlyList<ConsumedItem> Items { get; init; }
    public required decimal TotalCost { get; init; }
    public required DateTime ConsumedAt { get; init; }
}

public record ConsumedItem
{
    public Guid IngredientId { get; init; }
    public string IngredientName { get; init; }
    public decimal Quantity { get; init; }
    public string Unit { get; init; }
    public decimal UnitCost { get; init; }
    public decimal TotalCost { get; init; }
    public IReadOnlyList<BatchConsumption> BatchBreakdown { get; init; }
}

public record BatchConsumption
{
    public Guid BatchId { get; init; }
    public string BatchNumber { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public decimal TotalCost { get; init; }
}
```

### StockWasted

```csharp
public record StockWasted : DomainEvent
{
    public override string EventType => "inventory.stock.wasted";

    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required Guid SiteId { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required WasteCategory Category { get; init; }
    public required string Reason { get; init; }
    public required decimal EstimatedCost { get; init; }
    public required Guid RecordedBy { get; init; }
    public required DateTime RecordedAt { get; init; }
    public IReadOnlyList<BatchConsumption>? BatchBreakdown { get; init; }
}

public enum WasteCategory
{
    Spoilage,
    Expired,
    Damaged,
    OverProduction,
    CustomerReturn,
    QualityIssue,
    SpillageAccident,
    Other
}
```

### QuantityAdjusted

```csharp
public record QuantityAdjusted : DomainEvent
{
    public override string EventType => "inventory.stock.quantity_adjusted";

    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required Guid SiteId { get; init; }
    public required decimal OldQuantity { get; init; }
    public required decimal NewQuantity { get; init; }
    public required decimal Variance { get; init; }
    public required AdjustmentReason Reason { get; init; }
    public required string ReasonDetails { get; init; }
    public Guid? PhysicalCountId { get; init; }
    public required Guid AdjustedBy { get; init; }
    public required DateTime AdjustedAt { get; init; }
    public bool ManagerApproved { get; init; }
    public Guid? ApprovedBy { get; init; }
}

public enum AdjustmentReason
{
    PhysicalCount,
    CountCorrection,
    SystemError,
    ReceivingError,
    TheftSuspected,
    UnitConversionError,
    Other
}
```

---

## Policies (Event Reactions)

### When StockBatchReceived

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Recalculate WAC | Update weighted average cost | Inventory |
| Check Level | Clear low-stock alerts if applicable | Alerts |
| Update Recipe Costs | Recalculate recipe costs if ingredient | Menu/Recipes |
| Link to Delivery | Update delivery received status | Procurement |
| Log for Traceability | Record for food safety | Compliance |

### When StockConsumedForOrder

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Record COGS | Post cost of goods sold | Accounting |
| Update Daily Usage | Increment usage statistics | Reporting |
| Check Levels | Trigger alerts if below threshold | Alerts |
| Update Recipe Performance | Track actual vs theoretical | Analytics |

### When LowStockAlertTriggered

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Notify Managers | Send low stock alert | Notifications |
| Suggest Reorder | Create suggested PO line | Procurement |
| Update Dashboard | Show on management dashboard | Reporting |
| Check Auto-Order | Trigger automatic reorder if configured | Procurement |

### When BatchExpired

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Alert Staff | Notify of expired stock | Notifications |
| Suggest Writeoff | Prompt for disposal | Inventory |
| Flag for Audit | Mark for food safety review | Compliance |
| Update Availability | Exclude from available count | Menu |

### When TransferCompleted

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Credit Source | Update source site inventory | Inventory (Source) |
| Debit Destination | Update destination inventory | Inventory (Dest) |
| Record Movement | Log transfer for reporting | Reporting |
| Close Transfer | Mark transfer as completed | Procurement |

---

## Read Models / Projections

### InventoryLevelView

```csharp
public record InventoryLevelView
{
    public Guid IngredientId { get; init; }
    public string IngredientName { get; init; }
    public string Sku { get; init; }
    public string Category { get; init; }
    public decimal QuantityOnHand { get; init; }
    public decimal QuantityReserved { get; init; }
    public decimal QuantityAvailable { get; init; }
    public string Unit { get; init; }
    public StockLevel Level { get; init; }
    public decimal ReorderPoint { get; init; }
    public decimal ParLevel { get; init; }
    public decimal WeightedAverageCost { get; init; }
    public decimal TotalValue { get; init; }
    public int BatchCount { get; init; }
    public DateTime? EarliestExpiry { get; init; }
    public DateTime? LastReceivedAt { get; init; }
    public DateTime? LastConsumedAt { get; init; }
}
```

### BatchDetailView

```csharp
public record BatchDetailView
{
    public Guid BatchId { get; init; }
    public string BatchNumber { get; init; }
    public string IngredientName { get; init; }
    public BatchStatus Status { get; init; }
    public DateTime ReceivedDate { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public int DaysUntilExpiry { get; init; }
    public decimal OriginalQuantity { get; init; }
    public decimal RemainingQuantity { get; init; }
    public decimal ConsumedQuantity { get; init; }
    public decimal PercentRemaining { get; init; }
    public decimal UnitCost { get; init; }
    public decimal RemainingValue { get; init; }
    public string? SupplierName { get; init; }
    public string? PurchaseOrderNumber { get; init; }
    public string? Location { get; init; }
}
```

### SiteInventorySummary

```csharp
public record SiteInventorySummary
{
    public Guid SiteId { get; init; }
    public string SiteName { get; init; }
    public int TotalSkus { get; init; }
    public int LowStockCount { get; init; }
    public int OutOfStockCount { get; init; }
    public int ExpiringWithinWeek { get; init; }
    public int ExpiredCount { get; init; }
    public decimal TotalInventoryValue { get; init; }
    public IReadOnlyList<CategorySummary> ByCategory { get; init; }
    public IReadOnlyList<InventoryAlert> ActiveAlerts { get; init; }
}

public record CategorySummary
{
    public string Category { get; init; }
    public int SkuCount { get; init; }
    public decimal TotalValue { get; init; }
    public int LowStockCount { get; init; }
}
```

### StockMovementHistory

```csharp
public record StockMovementHistory
{
    public Guid IngredientId { get; init; }
    public string IngredientName { get; init; }
    public DateRange Period { get; init; }
    public decimal StartingQuantity { get; init; }
    public decimal EndingQuantity { get; init; }
    public decimal TotalReceived { get; init; }
    public decimal TotalConsumed { get; init; }
    public decimal TotalWasted { get; init; }
    public decimal TotalAdjusted { get; init; }
    public decimal TotalTransferredIn { get; init; }
    public decimal TotalTransferredOut { get; init; }
    public IReadOnlyList<MovementEntry> Movements { get; init; }
}

public record MovementEntry
{
    public DateTime Timestamp { get; init; }
    public MovementType Type { get; init; }
    public decimal Quantity { get; init; }
    public decimal RunningBalance { get; init; }
    public string Reference { get; init; }
    public string? PerformedByName { get; init; }
    public string? Notes { get; init; }
}
```

### WasteAnalysisView

```csharp
public record WasteAnalysisView
{
    public Guid SiteId { get; init; }
    public DateRange Period { get; init; }
    public decimal TotalWasteValue { get; init; }
    public decimal WastePercentage { get; init; }
    public IReadOnlyList<WasteByCategoryBreakdown> ByCategory { get; init; }
    public IReadOnlyList<WasteByIngredientBreakdown> TopWastedIngredients { get; init; }
    public IReadOnlyList<WasteByReasonBreakdown> ByReason { get; init; }
    public IReadOnlyList<DailyWasteTrend> DailyTrend { get; init; }
}
```

### ValuationReport

```csharp
public record ValuationReport
{
    public Guid SiteId { get; init; }
    public DateTime AsOfDate { get; init; }
    public decimal TotalValue { get; init; }
    public IReadOnlyList<IngredientValuation> Ingredients { get; init; }
    public IReadOnlyList<CategoryValuation> ByCategory { get; init; }
}

public record IngredientValuation
{
    public Guid IngredientId { get; init; }
    public string Name { get; init; }
    public string Category { get; init; }
    public decimal Quantity { get; init; }
    public string Unit { get; init; }
    public decimal AverageCost { get; init; }
    public decimal TotalValue { get; init; }
}
```

---

## Bounded Context Relationships

### Upstream Contexts (This domain provides data to)

| Context | Relationship | Data Provided |
|---------|--------------|---------------|
| Menu/Recipes | Published Language | Ingredient costs for recipe costing |
| Accounting | Published Language | COGS, inventory valuation |
| Reporting | Published Language | Stock levels, waste data |
| Procurement | Published Language | Stock levels for reordering |

### Downstream Contexts (This domain consumes from)

| Context | Relationship | Data Consumed |
|---------|--------------|---------------|
| Orders | Customer/Supplier | Order items for consumption |
| Recipes | Customer/Supplier | Recipe ingredients and quantities |
| Procurement | Customer/Supplier | Delivery receipt data |
| Site | Customer/Supplier | Site configuration |

---

## Process Flows

### FIFO Consumption Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│ OrderGrain  │   │  Inventory  │   │   Batches   │
│  (Settled)  │   │    Grain    │   │  (Ordered)  │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │
       │ ConsumeForOrder │                 │
       │────────────────>│                 │
       │                 │                 │
       │                 │ Get Batches FIFO│
       │                 │────────────────>│
       │                 │                 │
       │                 │ Oldest First    │
       │                 │<────────────────│
       │                 │                 │
       │                 │ Deduct from     │
       │                 │ Batch #1        │
       │                 │────────────────>│
       │                 │                 │
       │                 │ If exhausted,   │
       │                 │ next batch      │
       │                 │────────────────>│
       │                 │                 │
       │                 │ StockConsumed   │
       │                 │────┐            │
       │                 │<───┘            │
       │                 │                 │
       │ Consumption Info│                 │
       │ (COGS)          │                 │
       │<────────────────│                 │
       │                 │                 │
```

### Physical Count Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│  Inventory  │   │  Inventory  │   │  Manager    │
│   Counter   │   │    Grain    │   │  (Approve)  │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │
       │ StartCount      │                 │
       │────────────────>│                 │
       │                 │                 │
       │   Blind Count   │                 │
       │   (no system    │                 │
       │   values shown) │                 │
       │                 │                 │
       │ RecordCount     │                 │
       │────────────────>│                 │
       │                 │                 │
       │                 │ Calculate Var   │
       │                 │────┐            │
       │                 │<───┘            │
       │                 │                 │
       │ If variance     │                 │
       │ above threshold │                 │
       │                 │ Request Approval│
       │                 │────────────────>│
       │                 │                 │
       │                 │   Approve       │
       │                 │<────────────────│
       │                 │                 │
       │                 │ AdjustQuantity  │
       │                 │────┐            │
       │                 │<───┘            │
       │                 │                 │
       │  Count Complete │                 │
       │<────────────────│                 │
       │                 │                 │
```

### Inter-Site Transfer Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│   Source    │   │  Transfer   │   │ Destination │   │  Receiving  │
│   Manager   │   │   Grain     │   │  Inventory  │   │    Staff    │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ InitiateTransfer│                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ Reserve Stock   │                 │
       │                 │ at Source       │                 │
       │                 │────┐            │                 │
       │                 │<───┘            │                 │
       │                 │                 │                 │
       │                 │ TransferInit'd  │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
       │ ConfirmSent     │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ TransferSent    │                 │
       │                 │────┐            │                 │
       │                 │<───┘            │                 │
       │                 │                 │                 │
       │                 │       Physical Movement           │
       │                 │                 │                 │
       │                 │                 │ ConfirmReceived │
       │                 │                 │<────────────────│
       │                 │                 │                 │
       │                 │ Deduct Source   │                 │
       │                 │────┐            │                 │
       │                 │<───┘            │                 │
       │                 │                 │                 │
       │                 │ Credit Dest     │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ TransferComplete│                 │
       │                 │────┐            │                 │
       │                 │<───┘            │                 │
       │                 │                 │                 │
```

---

## Business Rules

### FIFO Rules

1. **Strict FIFO**: Oldest batches consumed first (by received date)
2. **Expiry Override**: Expired batches cannot be consumed (skip to next)
3. **Partial Consumption**: Can consume part of a batch
4. **Batch Tracking**: All consumption records which batches were used
5. **Cost Tracking**: Actual cost from consumed batches, not average

### Count Rules

1. **Blind Counting**: Counter cannot see expected values during count
2. **Variance Threshold**: Adjustments over threshold require manager approval
3. **Frequency Requirements**: Configurable minimum count frequency
4. **Category Cycles**: Different categories may have different count cycles
5. **Documentation**: All adjustments require reason codes

### Transfer Rules

1. **Same Organization**: Transfers only within same organization
2. **Manager Approval**: Required for transfers over threshold
3. **In-Transit Tracking**: Stock marked as reserved until received
4. **Timeout**: Unconfirmed transfers expire after configurable period
5. **Cost Preservation**: Transfer at original batch costs

### Expiry Rules

1. **Alert Windows**: Configurable alerts at 7/3/1 days before expiry
2. **Automatic Flagging**: Expired batches automatically marked
3. **No Sales**: Expired items excluded from available quantity
4. **Write-off Required**: Must explicitly write off expired stock

---

## Hotspots & Risks

### High Complexity Areas

| Area | Complexity | Mitigation |
|------|------------|------------|
| **FIFO Calculation** | Multiple batches per consumption | Batch iterator pattern |
| **WAC Calculation** | Accuracy across receipts | Recalculate on every receipt |
| **Unit Conversions** | Multiple units per ingredient | Conversion factor tables |
| **Negative Inventory** | Timing issues | Prevent at command level |

### Performance Considerations

| Concern | Impact | Strategy |
|---------|--------|----------|
| **High-Volume Consumption** | Many deductions per order | Batch updates |
| **Real-time Level Checks** | Frequent reads | Cache current levels |
| **Historical Queries** | Large movement history | Time-partitioned projections |

### Data Integrity

| Concern | Risk | Mitigation |
|---------|------|------------|
| **Count Discrepancy** | Theft, errors | Blind counts, variance tracking |
| **Batch Traceability** | Food safety recalls | Complete batch lineage |
| **Cost Accuracy** | Profit margin errors | Audit trail on all cost changes |

---

## Event Type Registry

```csharp
public static class InventoryEventTypes
{
    // Receipt
    public const string StockBatchReceived = "inventory.stock.batch_received";
    public const string DeliveryReceived = "inventory.stock.delivery_received";
    public const string TransferReceived = "inventory.stock.transfer_received";
    public const string ReceiptAdjusted = "inventory.stock.receipt_adjusted";
    public const string BatchRejected = "inventory.stock.batch_rejected";

    // Consumption
    public const string StockConsumed = "inventory.stock.consumed";
    public const string StockConsumedForOrder = "inventory.stock.consumed_for_order";
    public const string StockWasted = "inventory.stock.wasted";
    public const string StockSampled = "inventory.stock.sampled";
    public const string StockTransferredOut = "inventory.stock.transferred_out";
    public const string ConsumptionReversed = "inventory.stock.consumption_reversed";

    // Batches
    public const string BatchExhausted = "inventory.batch.exhausted";
    public const string BatchExpired = "inventory.batch.expired";
    public const string BatchWrittenOff = "inventory.batch.written_off";
    public const string BatchesMerged = "inventory.batch.merged";

    // Levels
    public const string LowStockAlertTriggered = "inventory.alert.low_stock";
    public const string OutOfStockAlertTriggered = "inventory.alert.out_of_stock";
    public const string StockLevelNormalized = "inventory.alert.level_normalized";
    public const string ParLevelExceeded = "inventory.alert.par_exceeded";

    // Adjustments
    public const string QuantityAdjusted = "inventory.stock.quantity_adjusted";
    public const string PhysicalCountRecorded = "inventory.stock.count_recorded";
    public const string CostAdjusted = "inventory.stock.cost_adjusted";
    public const string WeightedAverageCostRecalculated = "inventory.stock.wac_recalculated";

    // Transfers
    public const string TransferInitiated = "inventory.transfer.initiated";
    public const string TransferSent = "inventory.transfer.sent";
    public const string TransferCompleted = "inventory.transfer.completed";
    public const string TransferCancelled = "inventory.transfer.cancelled";

    // Configuration
    public const string ReorderPointSet = "inventory.config.reorder_point_set";
    public const string ParLevelSet = "inventory.config.par_level_set";
    public const string IngredientEnabledAtSite = "inventory.config.ingredient_enabled";
    public const string IngredientDisabledAtSite = "inventory.config.ingredient_disabled";
}
```

---

## Integration Points

### Recipe Integration

```csharp
// Calculate actual COGS for an order
public async Task<OrderCostResult> CalculateOrderCOGS(Guid orderId)
{
    var order = await _orderGrain.GetStateAsync();
    var totalCost = 0m;
    var consumptions = new List<ConsumedItem>();

    foreach (var line in order.Lines.Where(l => l.RecipeId.HasValue))
    {
        var recipe = await _recipeGrain.GetStateAsync(line.RecipeId.Value);

        foreach (var ingredient in recipe.Ingredients)
        {
            var inventoryGrain = _grainFactory.GetGrain<IInventoryGrain>(
                GrainKeys.SiteEntity(order.OrgId, order.SiteId, "inventory", ingredient.IngredientId));

            var consumption = await inventoryGrain.ConsumeAsync(new ConsumeStockCommand(
                orderId,
                ingredient.Quantity * line.Quantity,
                ConsumeReason.Sale));

            totalCost += consumption.TotalCost;
            consumptions.Add(consumption);
        }
    }

    return new OrderCostResult(orderId, totalCost, consumptions);
}
```

### Procurement Integration

```csharp
// Suggest purchase orders based on stock levels
public async Task<SuggestedPurchaseOrder> SuggestReorder(Guid siteId)
{
    var lowStockItems = await GetItemsBelowReorderPoint(siteId);
    var suggestions = new List<SuggestedPOLine>();

    foreach (var item in lowStockItems)
    {
        var ingredient = await _ingredientCatalog.GetAsync(item.IngredientId);
        var preferredSupplier = ingredient.PreferredSupplierId;
        var reorderQty = item.ParLevel - item.QuantityOnHand;

        suggestions.Add(new SuggestedPOLine
        {
            IngredientId = item.IngredientId,
            SuggestedQuantity = reorderQty,
            SupplierId = preferredSupplier,
            EstimatedCost = reorderQty * item.WeightedAverageCost
        });
    }

    return new SuggestedPurchaseOrder(siteId, suggestions);
}
```
