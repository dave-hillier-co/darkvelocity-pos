# Event Storming: Order Management Domain

## Overview

The Order Management domain is the core transactional domain of the DarkVelocity POS system. It handles the complete lifecycle of customer orders from creation through settlement, including item management, modifications, discounts, splits, transfers, and coordination with kitchen and payment systems.

---

## Domain Purpose

- **Order Lifecycle**: Manage orders from opening to settlement or void
- **Item Management**: Add, modify, remove, and void order items with modifiers
- **Pricing & Discounts**: Apply prices, taxes, and various discount types
- **Order Coordination**: Send to kitchen, track preparation, manage timing
- **Financial Accuracy**: Maintain accurate totals for payment processing
- **Operational Flexibility**: Support splits, merges, transfers, and reopening

---

## Actors

| Actor | Description | Typical Actions |
|-------|-------------|-----------------|
| **Server** | Wait staff serving tables | Open orders, add items, send to kitchen |
| **Bartender** | Bar staff | Open bar tabs, add drinks, quick checkout |
| **Cashier** | Front counter/register | Quick orders, payment processing |
| **Manager** | Supervising staff | Void items/orders, apply discounts, resolve issues |
| **Kitchen System** | KDS/Kitchen printer | Receive tickets, mark complete |
| **Customer** | Self-service scenarios | Order via kiosk/app |
| **System** | Automated processes | Auto-fire courses, timeout warnings |

---

## Aggregates

### Order Aggregate

The primary aggregate representing a customer transaction.

```
Order
├── Id: Guid
├── OrgId: Guid
├── SiteId: Guid
├── OrderNumber: string (display number)
├── Type: OrderType
├── Status: OrderStatus
├── ServerId: Guid
├── TableId?: Guid
├── CustomerId?: Guid
├── BookingId?: Guid
├── GuestCount: int
├── Lines: List<OrderLine>
├── Discounts: List<OrderDiscount>
├── Payments: List<PaymentSummary>
├── Taxes: List<TaxBreakdown>
├── Subtotal: decimal
├── DiscountTotal: decimal
├── TaxTotal: decimal
├── ServiceCharge?: decimal
├── Total: decimal
├── PaidAmount: decimal
├── BalanceDue: decimal
├── Tips: List<TipRecord>
├── Notes?: string
├── OpenedAt: DateTime
├── SentAt?: DateTime
├── SettledAt?: DateTime
├── VoidedAt?: DateTime
└── Metadata: OrderMetadata
```

**Invariants:**
- Total = Subtotal - DiscountTotal + TaxTotal + ServiceCharge
- BalanceDue = Total - PaidAmount
- Cannot add items to settled or voided orders
- Cannot settle with balance due > 0 (unless manager override)
- Cannot void settled orders (must refund)
- GuestCount >= 1

### OrderLine Entity

Individual item within an order.

```
OrderLine
├── Id: Guid
├── MenuItemId: Guid
├── MenuItemName: string
├── Quantity: int
├── UnitPrice: decimal
├── Modifiers: List<ModifierSelection>
├── ModifierTotal: decimal
├── Discounts: List<LineDiscount>
├── DiscountTotal: decimal
├── TaxRate: decimal
├── TaxAmount: decimal
├── LineTotal: decimal
├── Status: OrderLineStatus
├── CourseNumber?: int
├── Seat?: int
├── Notes?: string
├── RecipeId?: Guid
├── AddedBy: Guid
├── AddedAt: DateTime
├── SentAt?: DateTime
├── VoidedAt?: DateTime
├── VoidedBy?: Guid
└── VoidReason?: string
```

**Invariants:**
- Quantity >= 1 (or voided)
- LineTotal = (UnitPrice * Quantity) + ModifierTotal - DiscountTotal + TaxAmount
- Cannot modify after sent to kitchen (without void/remake)
- Voided items don't contribute to order total

### ModifierSelection Value Object

```
ModifierSelection
├── ModifierId: Guid
├── ModifierName: string
├── GroupId: Guid
├── GroupName: string
├── PriceAdjustment: decimal
├── Quantity: int
└── IsDefault: bool
```

### OrderDiscount Entity

```
OrderDiscount
├── Id: Guid
├── Type: DiscountType
├── Name: string
├── Amount: decimal
├── IsPercentage: bool
├── AppliedTo: DiscountScope (Order, Line, Category)
├── CouponCode?: string
├── PromotionId?: Guid
├── AppliedBy: Guid
├── AppliedAt: DateTime
├── Reason?: string
└── RequiredApproval: bool
```

---

## Order State Machine

```
                                    ┌─────────────────┐
                                    │                 │
                                    ▼                 │
┌─────────┐    OpenOrder    ┌─────────────┐    AddLine/UpdateLine
│  None   │ ───────────────>│    Open     │ ◄─────────┘
└─────────┘                 └──────┬──────┘
                                   │
                                   │ SendToKitchen
                                   ▼
                            ┌─────────────┐
                            │    Sent     │ ◄───┐
                            └──────┬──────┘     │
                                   │            │ AddLine (new course)
                    ┌──────────────┼────────────┘
                    │              │
                    │              │ ApplyPayment (full)
                    │              ▼
                    │       ┌─────────────┐
     ApplyPayment   │       │   Settled   │
     (partial)      │       └─────────────┘
                    │
                    ▼
             ┌─────────────┐
             │  Partial    │
             │   Paid      │
             └──────┬──────┘
                    │
                    │ ApplyPayment (remaining)
                    ▼
             ┌─────────────┐
             │   Settled   │
             └─────────────┘


From any state (except Settled):
                    │
                    │ VoidOrder
                    ▼
             ┌─────────────┐
             │   Voided    │
             └─────────────┘

From Settled:
                    │
                    │ ReopenOrder (manager)
                    ▼
             ┌─────────────┐
             │  Reopened   │ ──────> (back to Open/Sent state)
             └─────────────┘
```

---

## Commands

### Order Lifecycle Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `OpenOrder` | Create new order | Site open, user has access | Server, Cashier |
| `CloseOrder` | Close without payment (comp) | Manager approval | Manager |
| `VoidOrder` | Cancel entire order | Not settled, manager approval | Manager |
| `ReopenOrder` | Reopen settled order | Within time limit, manager | Manager |
| `SettleOrder` | Complete and close | Balance = 0 or override | Server, Cashier |

### Line Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `AddLine` | Add item to order | Order open/sent | Server |
| `UpdateLine` | Modify quantity/modifiers | Line not sent | Server |
| `VoidLine` | Cancel item | Manager approval if sent | Server, Manager |
| `SplitLine` | Divide item across seats/checks | Line exists | Server |
| `TransferLine` | Move to another order | Both orders editable | Server |
| `SetLineCourse` | Assign to course | Line exists | Server |
| `SetLineSeat` | Assign to seat | Line exists | Server |
| `AddLineNote` | Add special instructions | Line exists | Server |

### Discount Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `ApplyOrderDiscount` | Order-level discount | Order not settled | Manager |
| `ApplyLineDiscount` | Line-level discount | Line exists | Manager |
| `ApplyCoupon` | Redeem coupon code | Valid coupon | Server |
| `ApplyLoyaltyDiscount` | Use loyalty points | Customer attached | Server |
| `RemoveDiscount` | Remove applied discount | Discount exists | Manager |

### Kitchen Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `SendToKitchen` | Fire order to kitchen | Has unsent items | Server |
| `FireCourse` | Send specific course | Course has items | Server |
| `HoldOrder` | Delay kitchen firing | Order not sent | Server |
| `RushOrder` | Prioritize in kitchen | Order sent | Server, Manager |
| `RecallFromKitchen` | Pull back sent items | Manager approval | Manager |

### Transfer Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `TransferToTable` | Move order to table | Order exists, table available | Server |
| `TransferToServer` | Reassign ownership | Order exists, target valid | Manager |
| `MergeOrders` | Combine two orders | Both open, same table | Server |
| `SplitOrder` | Divide into multiple | Order has items | Server |
| `SplitBySeats` | Create order per seat | Seats assigned | Server |

### Payment Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `ApplyPayment` | Record payment | Order not settled | Cashier |
| `RecordTip` | Add gratuity | Order exists | Server |
| `AdjustTotal` | Manager price override | Manager approval | Manager |

---

## Domain Events

### Order Lifecycle Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `OrderOpened` | New order created | OrderId, SiteId, ServerId, TableId, Type, GuestCount | OpenOrder |
| `OrderSettled` | Order completed | OrderId, Total, PaymentMethod, SettledAt | SettleOrder |
| `OrderVoided` | Order cancelled | OrderId, Reason, VoidedBy, WasSent, AffectedInventory | VoidOrder |
| `OrderReopened` | Settled order reopened | OrderId, ReopenedBy, Reason | ReopenOrder |
| `OrderClosed` | Comped/closed no payment | OrderId, Reason, ClosedBy | CloseOrder |

### Line Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `OrderLineAdded` | Item added | OrderId, LineId, MenuItem, Qty, Price, Modifiers | AddLine |
| `OrderLineUpdated` | Item modified | OrderId, LineId, OldValues, NewValues | UpdateLine |
| `OrderLineVoided` | Item cancelled | OrderId, LineId, Reason, WasSent, VoidedBy | VoidLine |
| `OrderLineSplit` | Item divided | OrderId, LineId, NewLineIds, Distribution | SplitLine |
| `OrderLineTransferred` | Item moved | SourceOrderId, TargetOrderId, LineId | TransferLine |
| `LineCourseSet` | Course assigned | OrderId, LineId, CourseNumber | SetLineCourse |
| `LineSeatSet` | Seat assigned | OrderId, LineId, SeatNumber | SetLineSeat |
| `LineNoteAdded` | Instructions added | OrderId, LineId, Note | AddLineNote |

### Discount Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `OrderDiscountApplied` | Order discount added | OrderId, DiscountId, Type, Amount, AppliedBy | ApplyOrderDiscount |
| `LineDiscountApplied` | Line discount added | OrderId, LineId, DiscountId, Type, Amount | ApplyLineDiscount |
| `CouponRedeemed` | Coupon used | OrderId, CouponCode, DiscountAmount | ApplyCoupon |
| `LoyaltyDiscountApplied` | Points redeemed | OrderId, CustomerId, PointsUsed, DiscountAmount | ApplyLoyaltyDiscount |
| `DiscountRemoved` | Discount revoked | OrderId, DiscountId, RemovedBy, Reason | RemoveDiscount |

### Kitchen Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `OrderSentToKitchen` | Order fired | OrderId, LineIds, SentAt, Stations | SendToKitchen |
| `CourseFired` | Course sent | OrderId, CourseNumber, LineIds, FiredAt | FireCourse |
| `OrderHeld` | Delayed firing | OrderId, HoldUntil, HeldBy | HoldOrder |
| `OrderRushed` | Priority elevated | OrderId, RushedBy | RushOrder |
| `OrderRecalledFromKitchen` | Items pulled back | OrderId, LineIds, RecalledBy, Reason | RecallFromKitchen |

### Transfer Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `OrderTransferredToTable` | Table changed | OrderId, OldTableId, NewTableId | TransferToTable |
| `OrderTransferredToServer` | Server changed | OrderId, OldServerId, NewServerId | TransferToServer |
| `OrdersMerged` | Orders combined | SourceOrderId, TargetOrderId, MergedLineIds | MergeOrders |
| `OrderSplit` | Order divided | SourceOrderId, NewOrderIds, LineDistribution | SplitOrder |
| `OrderSplitBySeats` | Per-seat orders | SourceOrderId, SeatOrderMap | SplitBySeats |

### Payment Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `PaymentApplied` | Payment recorded | OrderId, PaymentId, Method, Amount | ApplyPayment |
| `TipRecorded` | Gratuity added | OrderId, TipAmount, TipType, ServerId | RecordTip |
| `TotalAdjusted` | Price override | OrderId, OldTotal, NewTotal, AdjustedBy, Reason | AdjustTotal |

---

## Event Details

### OrderOpened

```csharp
public record OrderOpened : DomainEvent
{
    public override string EventType => "orders.order.opened";

    public required Guid OrderId { get; init; }
    public required Guid SiteId { get; init; }
    public required Guid ServerId { get; init; }
    public required string OrderNumber { get; init; }
    public required OrderType Type { get; init; }
    public Guid? TableId { get; init; }
    public string? TableNumber { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? BookingId { get; init; }
    public required int GuestCount { get; init; }
    public string? Notes { get; init; }
    public required DateTime OpenedAt { get; init; }
}

public enum OrderType
{
    DineIn,
    TakeOut,
    Delivery,
    BarTab,
    QuickSale,
    Catering,
    RoomService
}
```

### OrderLineAdded

```csharp
public record OrderLineAdded : DomainEvent
{
    public override string EventType => "orders.order.line_added";

    public required Guid OrderId { get; init; }
    public required Guid LineId { get; init; }
    public required Guid MenuItemId { get; init; }
    public required string MenuItemName { get; init; }
    public required string MenuItemSku { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal TaxRate { get; init; }
    public IReadOnlyList<ModifierSelection> Modifiers { get; init; } = [];
    public required decimal ModifierTotal { get; init; }
    public required decimal LineTotal { get; init; }
    public required decimal TaxAmount { get; init; }
    public Guid? RecipeId { get; init; }
    public int? CourseNumber { get; init; }
    public int? Seat { get; init; }
    public string? Notes { get; init; }
    public required Guid AddedBy { get; init; }
    public required DateTime AddedAt { get; init; }
}
```

### OrderSentToKitchen

```csharp
public record OrderSentToKitchen : DomainEvent
{
    public override string EventType => "orders.order.sent_to_kitchen";

    public required Guid OrderId { get; init; }
    public required Guid SiteId { get; init; }
    public required IReadOnlyList<KitchenTicketInfo> Tickets { get; init; }
    public required DateTime SentAt { get; init; }
    public required Guid SentBy { get; init; }
}

public record KitchenTicketInfo
{
    public Guid TicketId { get; init; }
    public string StationId { get; init; }
    public IReadOnlyList<Guid> LineIds { get; init; }
    public int Priority { get; init; }
}
```

### OrderSettled

```csharp
public record OrderSettled : DomainEvent
{
    public override string EventType => "orders.order.settled";

    public required Guid OrderId { get; init; }
    public required Guid SiteId { get; init; }
    public required Guid ServerId { get; init; }
    public Guid? CustomerId { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal DiscountTotal { get; init; }
    public required decimal TaxTotal { get; init; }
    public decimal? ServiceCharge { get; init; }
    public required decimal Total { get; init; }
    public required decimal TipTotal { get; init; }
    public required IReadOnlyList<PaymentSummary> Payments { get; init; }
    public required IReadOnlyList<TaxBreakdown> TaxBreakdown { get; init; }
    public required int ItemCount { get; init; }
    public required int GuestCount { get; init; }
    public required TimeSpan Duration { get; init; }
    public required DateTime SettledAt { get; init; }
}
```

### OrderVoided

```csharp
public record OrderVoided : DomainEvent
{
    public override string EventType => "orders.order.voided";

    public required Guid OrderId { get; init; }
    public required Guid SiteId { get; init; }
    public required string Reason { get; init; }
    public required Guid VoidedBy { get; init; }
    public required bool WasSent { get; init; }
    public required decimal VoidedAmount { get; init; }
    public IReadOnlyList<VoidedLineInfo> VoidedLines { get; init; } = [];
    public IReadOnlyList<AffectedInventoryItem> AffectedInventory { get; init; } = [];
    public required DateTime VoidedAt { get; init; }
}

public record VoidedLineInfo
{
    public Guid LineId { get; init; }
    public Guid MenuItemId { get; init; }
    public int Quantity { get; init; }
    public decimal Amount { get; init; }
}

public record AffectedInventoryItem
{
    public Guid IngredientId { get; init; }
    public decimal Quantity { get; init; }
    public bool ShouldRestore { get; init; }
}
```

---

## Policies (Event Reactions)

### When OrderOpened

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Occupy Table | Mark table as occupied | Site/Tables |
| Create SpiceDB Relations | Link order to server/site | Authorization |
| Initialize Kitchen Context | Prepare for tickets | Kitchen |
| Link to Booking | Connect if booking provided | Booking |

### When OrderLineAdded

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Update Order Totals | Recalculate subtotal/tax/total | Order (internal) |
| Check Stock Availability | Warn if low stock | Inventory |
| Track Item Popularity | Record for analytics | Reporting |

### When OrderSentToKitchen

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Create Kitchen Tickets | Generate tickets per station | Kitchen |
| Start Prep Timer | Track preparation time | Kitchen |
| Print Kitchen Tickets | Send to printers | Devices |
| Reserve Inventory | Soft-reserve ingredients | Inventory |

### When OrderSettled

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Consume Inventory | Deduct ingredients (FIFO) | Inventory |
| Award Loyalty Points | Credit customer points | Customer/Loyalty |
| Post Accounting Entry | Record revenue | Accounting |
| Clear Table | Mark table available | Site/Tables |
| Update Daily Totals | Increment reporting | Reporting |
| Process Tip Distribution | Calculate tip pools | Labor |

### When OrderVoided

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Void Kitchen Tickets | Cancel preparation | Kitchen |
| Restore Inventory | Return reserved stock | Inventory |
| Clear Table | Mark table available | Site/Tables |
| Record Void Reason | Log for analysis | Reporting |
| Notify Manager | Alert of void | Notifications |

### When OrderTransferredToServer

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Update SpiceDB | Change server relationship | Authorization |
| Notify New Server | Alert of transfer | Notifications |
| Update Table Assignment | If server has tables | Site/Tables |

---

## Read Models / Projections

### OrderSummary

```csharp
public record OrderSummary
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; }
    public OrderType Type { get; init; }
    public OrderStatus Status { get; init; }
    public string ServerName { get; init; }
    public string? TableNumber { get; init; }
    public int GuestCount { get; init; }
    public int ItemCount { get; init; }
    public decimal Total { get; init; }
    public decimal BalanceDue { get; init; }
    public TimeSpan Age { get; init; }
    public DateTime OpenedAt { get; init; }
    public bool IsSent { get; init; }
}
```

### OrderDetailView

```csharp
public record OrderDetailView
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; }
    public OrderType Type { get; init; }
    public OrderStatus Status { get; init; }

    // Participants
    public UserInfo Server { get; init; }
    public TableInfo? Table { get; init; }
    public CustomerInfo? Customer { get; init; }
    public BookingInfo? Booking { get; init; }
    public int GuestCount { get; init; }

    // Lines grouped by course/seat
    public IReadOnlyList<OrderLineView> Lines { get; init; }
    public IReadOnlyDictionary<int, IReadOnlyList<OrderLineView>> ByCourse { get; init; }
    public IReadOnlyDictionary<int, IReadOnlyList<OrderLineView>> BySeat { get; init; }

    // Financials
    public decimal Subtotal { get; init; }
    public IReadOnlyList<DiscountView> Discounts { get; init; }
    public decimal DiscountTotal { get; init; }
    public IReadOnlyList<TaxBreakdown> Taxes { get; init; }
    public decimal TaxTotal { get; init; }
    public decimal? ServiceCharge { get; init; }
    public decimal Total { get; init; }
    public IReadOnlyList<PaymentView> Payments { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal BalanceDue { get; init; }
    public IReadOnlyList<TipView> Tips { get; init; }

    // Timing
    public DateTime OpenedAt { get; init; }
    public DateTime? SentAt { get; init; }
    public DateTime? SettledAt { get; init; }
    public TimeSpan Duration { get; init; }

    // Notes
    public string? Notes { get; init; }
}
```

### ServerOrdersView

```csharp
public record ServerOrdersView
{
    public Guid ServerId { get; init; }
    public string ServerName { get; init; }
    public IReadOnlyList<OrderSummary> OpenOrders { get; init; }
    public int TotalOpenOrders { get; init; }
    public decimal TotalOpenValue { get; init; }
    public IReadOnlyList<OrderSummary> RecentlyClosed { get; init; }
    public decimal TodaySales { get; init; }
    public decimal TodayTips { get; init; }
}
```

### TableOrderView

```csharp
public record TableOrderView
{
    public Guid TableId { get; init; }
    public string TableNumber { get; init; }
    public TableStatus Status { get; init; }
    public OrderSummary? CurrentOrder { get; init; }
    public UserInfo? AssignedServer { get; init; }
    public int Capacity { get; init; }
    public int? CurrentGuests { get; init; }
    public DateTime? SeatedAt { get; init; }
    public TimeSpan? SeatedDuration { get; init; }
}
```

### SiteOrdersView

```csharp
public record SiteOrdersView
{
    public Guid SiteId { get; init; }
    public IReadOnlyList<OrderSummary> OpenOrders { get; init; }
    public int TotalOpenOrders { get; init; }
    public decimal TotalOpenValue { get; init; }
    public IReadOnlyList<OrderSummary> KitchenPending { get; init; }
    public IReadOnlyDictionary<string, int> OrdersByType { get; init; }
    public IReadOnlyDictionary<string, int> OrdersByServer { get; init; }
}
```

### OrderHistoryView

```csharp
public record OrderHistoryView
{
    public Guid OrderId { get; init; }
    public IReadOnlyList<OrderHistoryEntry> Timeline { get; init; }
}

public record OrderHistoryEntry
{
    public DateTime Timestamp { get; init; }
    public string EventType { get; init; }
    public string Description { get; init; }
    public Guid? ActorId { get; init; }
    public string? ActorName { get; init; }
    public IDictionary<string, object>? Details { get; init; }
}
```

---

## Bounded Context Relationships

### Upstream Contexts (This domain provides data to)

| Context | Relationship | Data Provided |
|---------|--------------|---------------|
| Kitchen | Published Language | Order items, modifiers, timing |
| Payments | Published Language | Order total, balance due |
| Inventory | Published Language | Items consumed |
| Accounting | Published Language | Revenue, tax breakdown |
| Reporting | Published Language | Order metrics |
| Customer/Loyalty | Published Language | Purchase amount for points |

### Downstream Contexts (This domain consumes from)

| Context | Relationship | Data Consumed |
|---------|--------------|---------------|
| Menu | Customer/Supplier | Item info, prices, modifiers |
| Site | Customer/Supplier | Table info, operating status |
| Customer | Customer/Supplier | Customer info, loyalty status |
| Booking | Customer/Supplier | Reservation info |
| Promotions | Customer/Supplier | Active promotions, coupon validation |

---

## Process Flows

### Standard Order Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│   Server    │   │ OrderGrain  │   │   Kitchen   │   │  Payment    │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ OpenOrder       │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │ AddLine (×N)    │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │ SendToKitchen   │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ CreateTickets   │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │                 │ ItemsComplete   │
       │                 │<────────────────│                 │
       │                 │                 │                 │
       │                         ... Dining ...              │
       │                 │                 │                 │
       │ ApplyPayment    │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ ProcessPayment  │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
       │                 │ PaymentComplete │                 │
       │                 │<────────────────────────────────│
       │                 │                 │                 │
       │ SettleOrder     │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │──── Post-settlement policies ────│
       │                 │                 │                 │
```

### Split Check Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│   Server    │   │ OrderGrain  │   │ New Orders  │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │
       │ SplitBySeats    │                 │
       │────────────────>│                 │
       │                 │                 │
       │                 │ OrderSplit      │
       │                 │────────────────>│
       │                 │                 │
       │                 │ Create Order A  │
       │                 │────────────────>│
       │                 │                 │
       │                 │ Create Order B  │
       │                 │────────────────>│
       │                 │                 │
       │ New Order IDs   │                 │
       │<────────────────│                 │
       │                 │                 │
       │ Each order settles independently  │
       │                 │                 │
```

### Order Void Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│   Manager   │   │ OrderGrain  │   │   Kitchen   │   │  Inventory  │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ VoidOrder       │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ VoidTickets     │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ RestoreStock    │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
       │                 │ OrderVoided     │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
       │   Void Complete │                 │                 │
       │<────────────────│                 │                 │
       │                 │                 │                 │
```

---

## Business Rules

### Order Rules

1. **Order Number Format**: `{SiteCode}-{Date}-{Sequence}` e.g., "DT01-20240115-0042"
2. **Table Exclusivity**: Only one open order per table (unless split)
3. **Site Open Required**: Cannot open orders when site is closed
4. **Guest Count**: Must be at least 1, max 99
5. **Order Timeout**: Warning after configurable period (default 90 min)

### Line Rules

1. **Quantity Limits**: 1-99 per line
2. **Required Modifiers**: Must select from required modifier groups
3. **Modifier Limits**: Cannot exceed max selections per group
4. **Price Lock**: Price captured at add time (not affected by menu updates)
5. **Send Lock**: Lines cannot be modified after sent to kitchen

### Discount Rules

1. **Stacking**: Configure whether discounts can stack
2. **Maximum Discount**: Cannot exceed configured % of subtotal
3. **Manager Approval**: Required for discounts above threshold
4. **Coupon Single Use**: Track coupon redemption
5. **Loyalty Requirements**: Customer must be attached

### Void Rules

1. **Pre-Send Voids**: Server can void unsent items
2. **Post-Send Voids**: Require manager approval
3. **Settled Order Voids**: Not allowed (use refund)
4. **Reason Required**: All voids must have reason
5. **Inventory Restore**: Configurable per-site

### Settlement Rules

1. **Balance Zero**: Order must have zero balance (or manager override)
2. **All Items Sent**: Warn if unsent items exist
3. **Tip Recording**: Optional tip capture before settlement
4. **Receipt Generation**: Auto-generate if configured

---

## Hotspots & Risks

### High Complexity Areas

| Area | Complexity | Mitigation |
|------|------------|------------|
| **Tax Calculation** | Multiple jurisdictions, inclusive/exclusive | Tax calculation service, clear configuration |
| **Discount Stacking** | Complex precedence rules | Well-defined discount hierarchy |
| **Split/Merge** | State synchronization | Event-driven coordination |
| **Modifier Pricing** | Complex pricing rules | Price calculation encapsulation |

### Performance Considerations

| Concern | Impact | Strategy |
|---------|--------|----------|
| **Order Retrieval** | Frequent reads during service | Cache active orders in memory |
| **Total Calculation** | Every line change | Incremental recalculation |
| **Kitchen Updates** | Real-time requirements | Orleans streams + SignalR |
| **Order Search** | Manager queries | Separate search projection grain |

### Concurrency Concerns

| Scenario | Risk | Mitigation |
|----------|------|------------|
| **Multiple Servers** | Conflicting edits | Single-writer via grain |
| **Split in Progress** | Inconsistent state | Saga pattern with compensation |
| **Kitchen Completion** | Race with void | Event ordering guarantees |

---

## Event Type Registry

```csharp
public static class OrderEventTypes
{
    // Order Lifecycle
    public const string OrderOpened = "orders.order.opened";
    public const string OrderSettled = "orders.order.settled";
    public const string OrderVoided = "orders.order.voided";
    public const string OrderReopened = "orders.order.reopened";
    public const string OrderClosed = "orders.order.closed";

    // Lines
    public const string OrderLineAdded = "orders.line.added";
    public const string OrderLineUpdated = "orders.line.updated";
    public const string OrderLineVoided = "orders.line.voided";
    public const string OrderLineSplit = "orders.line.split";
    public const string OrderLineTransferred = "orders.line.transferred";
    public const string LineCourseSet = "orders.line.course_set";
    public const string LineSeatSet = "orders.line.seat_set";
    public const string LineNoteAdded = "orders.line.note_added";

    // Discounts
    public const string OrderDiscountApplied = "orders.discount.order_applied";
    public const string LineDiscountApplied = "orders.discount.line_applied";
    public const string CouponRedeemed = "orders.discount.coupon_redeemed";
    public const string LoyaltyDiscountApplied = "orders.discount.loyalty_applied";
    public const string DiscountRemoved = "orders.discount.removed";

    // Kitchen
    public const string OrderSentToKitchen = "orders.kitchen.sent";
    public const string CourseFired = "orders.kitchen.course_fired";
    public const string OrderHeld = "orders.kitchen.held";
    public const string OrderRushed = "orders.kitchen.rushed";
    public const string OrderRecalledFromKitchen = "orders.kitchen.recalled";

    // Transfers
    public const string OrderTransferredToTable = "orders.transfer.to_table";
    public const string OrderTransferredToServer = "orders.transfer.to_server";
    public const string OrdersMerged = "orders.transfer.merged";
    public const string OrderSplit = "orders.transfer.split";
    public const string OrderSplitBySeats = "orders.transfer.split_by_seats";

    // Payments
    public const string PaymentApplied = "orders.payment.applied";
    public const string TipRecorded = "orders.payment.tip_recorded";
    public const string TotalAdjusted = "orders.payment.total_adjusted";
}
```

---

## Integration Points

### Menu Integration

```csharp
// When adding a line, fetch current menu item details
public async Task<OrderLineAddedEvent> AddLineAsync(AddOrderLineCommand cmd)
{
    var menuGrain = _grainFactory.GetGrain<IActiveMenuGrain>(
        GrainKeys.Site(State.OrgId, State.SiteId));

    var menuItem = await menuGrain.GetItemAsync(cmd.MenuItemId);

    if (menuItem == null)
        throw new MenuItemNotFoundException(cmd.MenuItemId);

    if (!menuItem.IsAvailable)
        throw new MenuItemUnavailableException(cmd.MenuItemId);

    // Validate modifiers
    ValidateModifiers(cmd.Modifiers, menuItem.ModifierGroups);

    // Calculate line total
    var lineTotal = CalculateLineTotal(cmd, menuItem);

    // Raise event
    return await RaiseEventAsync(new OrderLineAdded { /* ... */ });
}
```

### Inventory Reservation

```csharp
// On send to kitchen - soft reserve
public async Task OnOrderSentToKitchen(OrderSentToKitchen @event)
{
    foreach (var ticket in @event.Tickets)
    {
        foreach (var lineId in ticket.LineIds)
        {
            var line = GetLine(lineId);
            if (line.RecipeId.HasValue)
            {
                await ReserveIngredients(line.RecipeId.Value, line.Quantity);
            }
        }
    }
}

// On settlement - consume
public async Task OnOrderSettled(OrderSettled @event)
{
    foreach (var line in State.Lines.Where(l => l.RecipeId.HasValue))
    {
        await ConsumeIngredients(line.RecipeId.Value, line.Quantity);
    }
}
```

### Payment Coordination

```csharp
public async Task<PaymentAppliedEvent> ApplyPaymentAsync(ApplyPaymentCommand cmd)
{
    // Validate amount
    if (cmd.Amount > State.BalanceDue && cmd.Amount - State.BalanceDue > 0.01m)
        throw new OverpaymentException(State.BalanceDue, cmd.Amount);

    // Create payment grain
    var paymentId = Guid.NewGuid();
    var paymentGrain = _grainFactory.GetGrain<IPaymentGrain>(
        GrainKeys.OrgEntity(State.OrgId, "payment", paymentId));

    // Initiate payment
    await paymentGrain.InitiateAsync(new InitiatePaymentCommand(
        State.OrgId,
        State.Id,
        State.SiteId,
        cmd.Method,
        cmd.Amount,
        cmd.TipAmount));

    // Record on order
    return await RaiseEventAsync(new PaymentApplied
    {
        OrgId = State.OrgId,
        OrderId = State.Id,
        PaymentId = paymentId,
        Method = cmd.Method,
        Amount = cmd.Amount,
        TipAmount = cmd.TipAmount ?? 0
    });
}
```
