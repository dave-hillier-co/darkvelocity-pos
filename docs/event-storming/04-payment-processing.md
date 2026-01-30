# Event Storming: Payment Processing Domain

## Overview

The Payment Processing domain handles all monetary transactions within the DarkVelocity POS system. This includes cash handling, card payments (credit/debit), gift cards, loyalty point redemption, split payments, refunds, and cash drawer management. The domain ensures financial accuracy, integrates with external payment gateways, and maintains proper audit trails.

---

## Domain Purpose

- **Payment Acceptance**: Process multiple payment methods (cash, card, gift card, loyalty)
- **Transaction Security**: Ensure PCI compliance and secure card handling
- **Cash Management**: Track cash drawers, drops, and reconciliation
- **Refund Processing**: Handle returns and adjustments
- **Split Payments**: Support multiple payment methods per order
- **Settlement**: Batch and settle card transactions

---

## Actors

| Actor | Description | Typical Actions |
|-------|-------------|-----------------|
| **Cashier** | Staff processing payments | Accept cash, process cards |
| **Server** | Wait staff | Collect payments, record tips |
| **Manager** | Supervising operations | Approve voids, process refunds, reconcile |
| **Customer** | Person paying | Present payment, sign receipts |
| **Payment Gateway** | External processor | Authorize, capture, settle |
| **Terminal** | Payment hardware | Read cards, process EMV |
| **System** | Automated processes | Batch settlements, alerts |

---

## Aggregates

### Payment Aggregate

Represents a single payment transaction.

```
Payment
├── Id: Guid
├── OrgId: Guid
├── SiteId: Guid
├── OrderId: Guid
├── Method: PaymentMethod
├── Status: PaymentStatus
├── Amount: decimal
├── TipAmount: decimal
├── TotalAmount: decimal
├── CashierId: Guid
├── CustomerId?: Guid
├── DrawerId?: Guid
├── GatewayReference?: string
├── AuthorizationCode?: string
├── CardInfo?: CardInfo
├── GiftCardId?: Guid
├── LoyaltyRedemption?: LoyaltyRedemptionInfo
├── ChangeAmount?: decimal
├── CreatedAt: DateTime
├── CompletedAt?: DateTime
├── VoidedAt?: DateTime
└── RefundedAmount: decimal
```

**Invariants:**
- TotalAmount = Amount + TipAmount
- Cannot void after settlement batch closed
- Refunded amount cannot exceed original amount
- Card payments require gateway reference

### CashDrawer Aggregate

Represents a physical cash drawer and its state.

```
CashDrawer
├── Id: Guid
├── SiteId: Guid
├── DeviceId: Guid
├── Name: string
├── Status: DrawerStatus
├── CurrentUserId?: Guid
├── OpenedAt?: DateTime
├── ExpectedBalance: decimal
├── ActualBalance?: decimal
├── OpeningFloat: decimal
├── CashIn: decimal
├── CashOut: decimal
├── CashDrops: List<CashDrop>
├── LastCountedAt?: DateTime
└── Transactions: List<DrawerTransaction>
```

**Invariants:**
- Only one user can have drawer open at a time
- ExpectedBalance = OpeningFloat + CashIn - CashOut - Drops
- Cannot process cash when drawer closed
- Must count before closing

### Batch Aggregate

Represents a settlement batch for card transactions.

```
Batch
├── Id: Guid
├── SiteId: Guid
├── Status: BatchStatus
├── OpenedAt: DateTime
├── ClosedAt?: DateTime
├── TransactionCount: int
├── TotalAmount: decimal
├── TipAmount: decimal
├── RefundAmount: decimal
├── NetAmount: decimal
├── Payments: List<BatchPayment>
└── SettlementReference?: string
```

---

## Payment State Machine

```
┌─────────────┐
│   Created   │
└──────┬──────┘
       │
       │ Initiate
       ▼
┌─────────────┐
│  Initiated  │
└──────┬──────┘
       │
       ├─────────────────┬────────────────────┐
       │ Cash            │ Card               │ Gift Card
       ▼                 ▼                    ▼
┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│   Cash      │   │ Authorizing │   │  Checking   │
│  Received   │   └──────┬──────┘   │  Balance    │
└──────┬──────┘          │          └──────┬──────┘
       │                 │                 │
       │          ┌──────┴──────┐          │
       │          │             │          │
       │          ▼             ▼          │
       │   ┌───────────┐ ┌───────────┐     │
       │   │ Authorized│ │ Declined  │     │
       │   └─────┬─────┘ └───────────┘     │
       │         │                         │
       │         │ Capture                 │
       │         ▼                         │
       │   ┌───────────┐                   │
       │   │ Captured  │                   │
       │   └─────┬─────┘                   │
       │         │                         │
       └────┬────┴─────────────────────────┘
            │
            ▼
     ┌─────────────┐
     │  Completed  │
     └──────┬──────┘
            │
            ├──────────────┬───────────────┐
            │              │               │
            ▼              ▼               ▼
     ┌───────────┐  ┌───────────┐  ┌─────────────┐
     │  Voided   │  │ Partially │  │   Refunded  │
     └───────────┘  │ Refunded  │  └─────────────┘
                    └───────────┘
```

---

## Commands

### Payment Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `InitiatePayment` | Start payment process | Order exists, has balance | Cashier |
| `CompleteCashPayment` | Finalize cash transaction | Cash received | Cashier |
| `RequestCardAuthorization` | Send to gateway | Card details valid | Terminal |
| `ConfirmCardAuthorization` | Record auth response | Auth received | Gateway |
| `CaptureCardPayment` | Capture authorized amount | Auth valid | System |
| `DeclineCardPayment` | Record declined | Gateway declined | Gateway |
| `ProcessGiftCardPayment` | Use gift card balance | Card valid, sufficient balance | Cashier |
| `ProcessLoyaltyRedemption` | Redeem loyalty points | Customer has points | Cashier |
| `VoidPayment` | Cancel payment | Not settled, manager approval | Manager |
| `IssueRefund` | Return money | Original payment exists | Manager |
| `PartialRefund` | Return partial amount | Amount <= remaining | Manager |
| `AddTip` | Add gratuity post-payment | Payment completed | Server |
| `AdjustTip` | Modify tip amount | Within adjustment window | Server |

### Cash Drawer Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `OpenDrawer` | Start drawer session | Drawer available | Cashier |
| `CloseDrawer` | End drawer session | Drawer counted | Cashier |
| `RecordCashIn` | Cash payment received | Drawer open | Cashier |
| `RecordCashOut` | Cash given out | Drawer open, manager for payouts | Cashier |
| `OpenDrawerNoSale` | Open without transaction | Manager or configured permission | Cashier |
| `RecordCashDrop` | Move cash to safe | Drawer open | Cashier |
| `CountDrawer` | Record physical count | Drawer open | Cashier |
| `ReconcileDrawer` | Compare expected vs actual | Count recorded | Manager |
| `OverrideDiscrepancy` | Accept difference | Manager approval | Manager |

### Batch Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `OpenBatch` | Start new batch | No open batch | System |
| `AddToBatch` | Include payment | Batch open, payment captured | System |
| `CloseBatch` | Close and submit | Batch has transactions | Manager/System |
| `SettleBatch` | Record settlement result | Batch submitted | Gateway |
| `VoidBatchPayment` | Remove from batch | Batch not settled | Manager |

---

## Domain Events

### Payment Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `PaymentInitiated` | Payment started | PaymentId, OrderId, Method, Amount | InitiatePayment |
| `CashPaymentReceived` | Cash tendered | PaymentId, Amount, Tendered, Change | CompleteCashPayment |
| `CardAuthorizationRequested` | Sent to gateway | PaymentId, MaskedCard, Amount | RequestCardAuthorization |
| `CardPaymentAuthorized` | Auth approved | PaymentId, AuthCode, MaskedCard | ConfirmCardAuthorization |
| `CardPaymentCaptured` | Amount captured | PaymentId, Amount, CaptureRef | CaptureCardPayment |
| `CardPaymentDeclined` | Auth denied | PaymentId, DeclineCode, Reason | DeclineCardPayment |
| `GiftCardPaymentApplied` | Gift card used | PaymentId, CardId, Amount, RemainingBalance | ProcessGiftCardPayment |
| `LoyaltyPointsRedeemed` | Points used | PaymentId, CustomerId, Points, Value | ProcessLoyaltyRedemption |
| `PaymentCompleted` | Transaction finished | PaymentId, TotalAmount, Method | Various completion paths |
| `PaymentVoided` | Payment cancelled | PaymentId, Reason, VoidedBy | VoidPayment |
| `RefundIssued` | Money returned | PaymentId, RefundId, Amount, Method | IssueRefund |
| `PartialRefundIssued` | Partial return | PaymentId, RefundId, Amount, Remaining | PartialRefund |
| `TipAdded` | Gratuity recorded | PaymentId, TipAmount, ServerId | AddTip |
| `TipAdjusted` | Tip modified | PaymentId, OldAmount, NewAmount | AdjustTip |

### Cash Drawer Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `DrawerOpened` | Session started | DrawerId, UserId, OpeningFloat | OpenDrawer |
| `DrawerClosed` | Session ended | DrawerId, ExpectedBalance, ActualBalance, Variance | CloseDrawer |
| `CashReceived` | Cash payment in | DrawerId, PaymentId, Amount | RecordCashIn |
| `CashPaidOut` | Cash given out | DrawerId, Amount, Reason, ApprovedBy | RecordCashOut |
| `DrawerOpenedNoSale` | Opened without tx | DrawerId, UserId, Reason | OpenDrawerNoSale |
| `CashDropRecorded` | Cash to safe | DrawerId, Amount, DropId | RecordCashDrop |
| `DrawerCounted` | Physical count | DrawerId, CountedBy, Amount, ExpectedAmount | CountDrawer |
| `DrawerReconciled` | Variance resolved | DrawerId, Variance, Resolution, ReconciledBy | ReconcileDrawer |
| `DiscrepancyOverridden` | Accepted difference | DrawerId, Variance, OverriddenBy, Reason | OverrideDiscrepancy |

### Batch Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `BatchOpened` | New batch started | BatchId, SiteId, OpenedAt | OpenBatch |
| `PaymentAddedToBatch` | Payment included | BatchId, PaymentId, Amount | AddToBatch |
| `BatchClosed` | Batch submitted | BatchId, TransactionCount, TotalAmount | CloseBatch |
| `BatchSettled` | Settlement confirmed | BatchId, SettlementRef, NetAmount | SettleBatch |
| `BatchSettlementFailed` | Settlement error | BatchId, ErrorCode, ErrorMessage | Gateway |
| `BatchPaymentVoided` | Removed from batch | BatchId, PaymentId, Reason | VoidBatchPayment |

---

## Event Details

### CashPaymentReceived

```csharp
public record CashPaymentReceived : DomainEvent
{
    public override string EventType => "payments.cash.received";

    public required Guid PaymentId { get; init; }
    public required Guid OrderId { get; init; }
    public required Guid SiteId { get; init; }
    public required Guid CashierId { get; init; }
    public required Guid DrawerId { get; init; }
    public required decimal Amount { get; init; }
    public required decimal AmountTendered { get; init; }
    public required decimal ChangeGiven { get; init; }
    public decimal TipAmount { get; init; }
    public required string Currency { get; init; }
    public required DateTime ReceivedAt { get; init; }
}
```

### CardPaymentAuthorized

```csharp
public record CardPaymentAuthorized : DomainEvent
{
    public override string EventType => "payments.card.authorized";

    public required Guid PaymentId { get; init; }
    public required Guid OrderId { get; init; }
    public required Guid SiteId { get; init; }
    public required string AuthorizationCode { get; init; }
    public required string GatewayReference { get; init; }
    public required decimal AuthorizedAmount { get; init; }
    public required CardInfo CardInfo { get; init; }
    public required string GatewayName { get; init; }
    public required DateTime AuthorizedAt { get; init; }
    public string? AvsResult { get; init; }
    public string? CvvResult { get; init; }
}

public record CardInfo
{
    public string MaskedNumber { get; init; } // e.g., "****4242"
    public string Brand { get; init; } // Visa, Mastercard, etc.
    public string? ExpiryMonth { get; init; }
    public string? ExpiryYear { get; init; }
    public string EntryMethod { get; init; } // chip, swipe, contactless, keyed
    public string? CardholderName { get; init; }
}
```

### RefundIssued

```csharp
public record RefundIssued : DomainEvent
{
    public override string EventType => "payments.refund.issued";

    public required Guid RefundId { get; init; }
    public required Guid OriginalPaymentId { get; init; }
    public required Guid OrderId { get; init; }
    public required Guid SiteId { get; init; }
    public required PaymentMethod Method { get; init; }
    public required decimal Amount { get; init; }
    public required string Reason { get; init; }
    public required Guid IssuedBy { get; init; }
    public required DateTime IssuedAt { get; init; }
    public string? GatewayReference { get; init; }
    public Guid? GiftCardId { get; init; } // If refunded to gift card
    public bool IsOriginalMethodRefund { get; init; }
}
```

### DrawerClosed

```csharp
public record DrawerClosed : DomainEvent
{
    public override string EventType => "payments.drawer.closed";

    public required Guid DrawerId { get; init; }
    public required Guid SiteId { get; init; }
    public required Guid ClosedBy { get; init; }
    public required decimal OpeningFloat { get; init; }
    public required decimal CashSales { get; init; }
    public required decimal CashRefunds { get; init; }
    public required decimal CashPayouts { get; init; }
    public required decimal CashDrops { get; init; }
    public required decimal ExpectedBalance { get; init; }
    public required decimal ActualBalance { get; init; }
    public required decimal Variance { get; init; }
    public required int TransactionCount { get; init; }
    public required DateTime OpenedAt { get; init; }
    public required DateTime ClosedAt { get; init; }
    public required TimeSpan Duration { get; init; }
    public IReadOnlyList<DenominationCount>? Denominations { get; init; }
}

public record DenominationCount
{
    public string Denomination { get; init; }
    public int Count { get; init; }
    public decimal Total { get; init; }
}
```

---

## Policies (Event Reactions)

### When PaymentCompleted

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Update Order Balance | Reduce order balance due | Orders |
| Add to Batch | Include in settlement batch | Batch |
| Print Receipt | Generate customer receipt | Devices |
| Update Daily Totals | Increment reporting | Reporting |
| Track Server Tips | Record tip for distribution | Labor |

### When CardPaymentAuthorized

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Schedule Capture | Auto-capture after configurable delay | System |
| Monitor for Tip Adjust | Start tip adjustment window | Timer |
| Log for PCI Compliance | Record masked data | Audit |

### When RefundIssued

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Update Order | Record refund on order | Orders |
| Reverse Inventory | Optionally restore stock | Inventory |
| Reverse Loyalty Points | Deduct previously earned | Customer |
| Post Accounting | Record refund journal entry | Accounting |
| Add to Batch | Include in settlement | Batch |

### When DrawerClosed

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Generate Drawer Report | Create reconciliation report | Reporting |
| Alert on Variance | Notify if over/short | Notifications |
| End User Session | Optional auto-logout | Identity |
| Update Daily Cash Report | Add to site totals | Reporting |

### When BatchSettled

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Post Deposit | Record bank deposit | Accounting |
| Update Payment Status | Mark payments settled | Payments |
| Generate Settlement Report | Create batch report | Reporting |
| Archive Batch | Move to history | Archival |

### When GiftCardPaymentApplied

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Deduct Balance | Reduce gift card balance | Gift Card |
| Check Zero Balance | Mark depleted if zero | Gift Card |
| Track Usage | Record redemption | Gift Card |

---

## Read Models / Projections

### PaymentSummary

```csharp
public record PaymentSummary
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public PaymentMethod Method { get; init; }
    public PaymentStatus Status { get; init; }
    public decimal Amount { get; init; }
    public decimal TipAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public string? LastFour { get; init; }
    public string? CardBrand { get; init; }
    public DateTime ProcessedAt { get; init; }
    public string CashierName { get; init; }
}
```

### DrawerStatusView

```csharp
public record DrawerStatusView
{
    public Guid DrawerId { get; init; }
    public string Name { get; init; }
    public DrawerStatus Status { get; init; }
    public string? CurrentUserName { get; init; }
    public DateTime? OpenedAt { get; init; }
    public decimal? ExpectedBalance { get; init; }
    public int TransactionCount { get; init; }
    public TimeSpan? Duration { get; init; }
}
```

### DrawerReportView

```csharp
public record DrawerReportView
{
    public Guid DrawerId { get; init; }
    public string DrawerName { get; init; }
    public string UserName { get; init; }

    // Session Info
    public DateTime OpenedAt { get; init; }
    public DateTime ClosedAt { get; init; }
    public TimeSpan Duration { get; init; }

    // Cash Summary
    public decimal OpeningFloat { get; init; }
    public decimal CashSales { get; init; }
    public decimal CashRefunds { get; init; }
    public decimal CashPayouts { get; init; }
    public decimal CashDrops { get; init; }
    public decimal ExpectedBalance { get; init; }
    public decimal ActualBalance { get; init; }
    public decimal Variance { get; init; }

    // Transactions
    public int SalesCount { get; init; }
    public int RefundCount { get; init; }
    public int PayoutCount { get; init; }
    public int NoSaleCount { get; init; }

    // Denominations
    public IReadOnlyList<DenominationCount> Denominations { get; init; }
}
```

### BatchSummaryView

```csharp
public record BatchSummaryView
{
    public Guid BatchId { get; init; }
    public BatchStatus Status { get; init; }
    public DateTime OpenedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
    public int TransactionCount { get; init; }
    public decimal GrossAmount { get; init; }
    public decimal TipAmount { get; init; }
    public decimal RefundAmount { get; init; }
    public decimal NetAmount { get; init; }
    public IReadOnlyDictionary<string, BatchCardBreakdown> ByCardBrand { get; init; }
}

public record BatchCardBreakdown
{
    public int Count { get; init; }
    public decimal Amount { get; init; }
    public decimal TipAmount { get; init; }
}
```

### SitePaymentDashboard

```csharp
public record SitePaymentDashboard
{
    public Guid SiteId { get; init; }
    public DateTime AsOf { get; init; }

    // Today's Summary
    public decimal TodayTotal { get; init; }
    public int TodayTransactions { get; init; }

    // By Method
    public decimal CashTotal { get; init; }
    public decimal CardTotal { get; init; }
    public decimal GiftCardTotal { get; init; }
    public decimal LoyaltyTotal { get; init; }

    // Tips
    public decimal TotalTips { get; init; }
    public decimal AverageTipPercent { get; init; }

    // Refunds/Voids
    public decimal RefundTotal { get; init; }
    public int RefundCount { get; init; }
    public decimal VoidTotal { get; init; }
    public int VoidCount { get; init; }

    // Drawer Status
    public IReadOnlyList<DrawerStatusView> Drawers { get; init; }

    // Current Batch
    public BatchSummaryView? CurrentBatch { get; init; }
}
```

### PaymentHistoryView

```csharp
public record PaymentHistoryView
{
    public Guid PaymentId { get; init; }
    public IReadOnlyList<PaymentHistoryEntry> Timeline { get; init; }
}

public record PaymentHistoryEntry
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
| Orders | Published Language | Payment completion, balance updates |
| Accounting | Published Language | Revenue, deposits, fees |
| Reporting | Published Language | Sales data, payment breakdown |
| Labor | Published Language | Tips for distribution |

### Downstream Contexts (This domain consumes from)

| Context | Relationship | Data Consumed |
|---------|--------------|---------------|
| Orders | Customer/Supplier | Order total, balance due |
| Customer | Customer/Supplier | Customer info for receipts |
| Gift Card | Customer/Supplier | Card balance, validation |
| Loyalty | Customer/Supplier | Points balance, redemption rules |
| External Gateway | Conformist | Authorization, capture, settlement |

### Anti-Corruption Layer

```csharp
// Gateway response translation
public class GatewayResponseTranslator
{
    public CardAuthResult TranslateStripeResponse(StripeCharge charge)
    {
        return new CardAuthResult
        {
            Success = charge.Status == "succeeded",
            AuthorizationCode = charge.Id,
            GatewayReference = charge.PaymentIntent,
            DeclineCode = charge.FailureCode,
            DeclineReason = charge.FailureMessage,
            CardInfo = new CardInfo
            {
                MaskedNumber = $"****{charge.PaymentMethodDetails.Card.Last4}",
                Brand = charge.PaymentMethodDetails.Card.Brand,
                ExpiryMonth = charge.PaymentMethodDetails.Card.ExpMonth.ToString(),
                ExpiryYear = charge.PaymentMethodDetails.Card.ExpYear.ToString()
            }
        };
    }

    public CardAuthResult TranslateSquareResponse(SquarePayment payment)
    {
        return new CardAuthResult
        {
            Success = payment.Status == "COMPLETED",
            AuthorizationCode = payment.Id,
            // ... translate other fields
        };
    }
}
```

---

## Process Flows

### Card Payment Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│  Terminal   │   │ PaymentGrain│   │   Gateway   │   │ OrderGrain  │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ Card Presented  │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ PaymentInitiated│                 │
       │                 │────┐            │                 │
       │                 │<───┘            │                 │
       │                 │                 │                 │
       │                 │ AuthRequest     │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │                 │ Process         │
       │                 │                 │────┐            │
       │                 │                 │<───┘            │
       │                 │                 │                 │
       │                 │ AuthResponse    │                 │
       │                 │<────────────────│                 │
       │                 │                 │                 │
       │                 │ CardAuthorized  │                 │
       │                 │────┐            │                 │
       │                 │<───┘            │                 │
       │                 │                 │                 │
       │                 │ Capture         │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ CaptureConfirm  │                 │
       │                 │<────────────────│                 │
       │                 │                 │                 │
       │                 │ ApplyPayment    │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
       │   Approved      │                 │                 │
       │<────────────────│                 │                 │
       │                 │                 │                 │
```

### Cash Payment Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│   Cashier   │   │ PaymentGrain│   │ DrawerGrain │   │ OrderGrain  │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ Cash Received   │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ RecordCashIn    │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ DrawerUpdated   │                 │
       │                 │<────────────────│                 │
       │                 │                 │                 │
       │                 │ CashReceived    │                 │
       │                 │────┐            │                 │
       │                 │<───┘            │                 │
       │                 │                 │                 │
       │                 │ ApplyPayment    │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
       │   Change Due    │                 │                 │
       │<────────────────│                 │                 │
       │                 │                 │                 │
       │ Open Drawer     │                 │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
```

### Refund Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│   Manager   │   │ PaymentGrain│   │   Gateway   │   │ OrderGrain  │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ IssueRefund     │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ Validate        │                 │
       │                 │────┐            │                 │
       │                 │<───┘            │                 │
       │                 │                 │                 │
       │                 │ RefundRequest   │ (if card)       │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ RefundConfirm   │                 │
       │                 │<────────────────│                 │
       │                 │                 │                 │
       │                 │ RefundIssued    │                 │
       │                 │────┐            │                 │
       │                 │<───┘            │                 │
       │                 │                 │                 │
       │                 │ RecordRefund    │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
       │   Complete      │                 │                 │
       │<────────────────│                 │                 │
       │                 │                 │                 │
```

---

## Business Rules

### Payment Rules

1. **Minimum Payment**: Payments must be at least $0.01
2. **Maximum Cash**: Cash transactions over configurable limit require manager approval
3. **Card Minimum**: Optional minimum for card payments
4. **Tip Limits**: Tips over configured % of payment require confirmation
5. **Tip Adjustment Window**: Tips can be adjusted until batch close (usually 24-48 hours)

### Refund Rules

1. **Same-Method Preference**: Refunds should go to original payment method when possible
2. **Manager Approval**: All refunds require manager authorization
3. **Time Limit**: Refunds only allowed within configured window (e.g., 90 days)
4. **Partial Allowed**: Multiple partial refunds up to original amount
5. **Receipt Required**: Configuration for whether receipt is needed

### Cash Drawer Rules

1. **Single User**: Only one user can have a drawer open at a time
2. **Opening Float**: Must record starting cash amount
3. **Count Required**: Must count drawer before closing
4. **Variance Threshold**: Alert when over/short exceeds threshold
5. **Drop Threshold**: Suggest drops when balance exceeds limit

### Batch Rules

1. **Daily Close**: Batches typically close once per day
2. **Deadline**: Batch must close by configured time to avoid fee penalties
3. **Minimum**: Some processors require minimum transaction count
4. **Holiday Handling**: Handle batches across holidays/weekends

---

## Security Considerations

### PCI Compliance

| Requirement | Implementation |
|-------------|----------------|
| **No PAN Storage** | Never store full card numbers |
| **Tokenization** | Use gateway tokens for recurring |
| **Encryption** | TLS for all gateway communication |
| **Masking** | Display only last 4 digits |
| **Access Control** | Limit who can view payment details |
| **Audit Trail** | Log all payment operations |

### Fraud Prevention

| Control | Description |
|---------|-------------|
| **AVS** | Address verification for keyed entries |
| **CVV** | Require security code |
| **Velocity Checks** | Alert on unusual patterns |
| **Amount Limits** | Configurable transaction limits |
| **Manager Approval** | Required for high-value transactions |

### Cash Security

| Control | Description |
|---------|-------------|
| **Drop Alerts** | Prompt when cash exceeds threshold |
| **Blind Counts** | Counter cannot see expected amount |
| **Variance Tracking** | Monitor patterns by employee |
| **No Sale Logging** | Track all drawer opens |
| **Video Integration** | Link transactions to camera footage |

---

## Event Type Registry

```csharp
public static class PaymentEventTypes
{
    // Payment Lifecycle
    public const string PaymentInitiated = "payments.payment.initiated";
    public const string PaymentCompleted = "payments.payment.completed";
    public const string PaymentVoided = "payments.payment.voided";

    // Cash
    public const string CashPaymentReceived = "payments.cash.received";

    // Card
    public const string CardAuthorizationRequested = "payments.card.auth_requested";
    public const string CardPaymentAuthorized = "payments.card.authorized";
    public const string CardPaymentCaptured = "payments.card.captured";
    public const string CardPaymentDeclined = "payments.card.declined";

    // Alternative Methods
    public const string GiftCardPaymentApplied = "payments.giftcard.applied";
    public const string LoyaltyPointsRedeemed = "payments.loyalty.redeemed";

    // Refunds
    public const string RefundIssued = "payments.refund.issued";
    public const string PartialRefundIssued = "payments.refund.partial_issued";

    // Tips
    public const string TipAdded = "payments.tip.added";
    public const string TipAdjusted = "payments.tip.adjusted";

    // Cash Drawer
    public const string DrawerOpened = "payments.drawer.opened";
    public const string DrawerClosed = "payments.drawer.closed";
    public const string CashReceived = "payments.drawer.cash_received";
    public const string CashPaidOut = "payments.drawer.cash_paid_out";
    public const string DrawerOpenedNoSale = "payments.drawer.no_sale";
    public const string CashDropRecorded = "payments.drawer.drop";
    public const string DrawerCounted = "payments.drawer.counted";
    public const string DrawerReconciled = "payments.drawer.reconciled";
    public const string DiscrepancyOverridden = "payments.drawer.discrepancy_override";

    // Batch
    public const string BatchOpened = "payments.batch.opened";
    public const string PaymentAddedToBatch = "payments.batch.payment_added";
    public const string BatchClosed = "payments.batch.closed";
    public const string BatchSettled = "payments.batch.settled";
    public const string BatchSettlementFailed = "payments.batch.settlement_failed";
    public const string BatchPaymentVoided = "payments.batch.payment_voided";
}
```

---

## Hotspots & Risks

### High Complexity Areas

| Area | Complexity | Mitigation |
|------|------------|------------|
| **Gateway Integration** | Multiple processors | Abstract gateway interface |
| **Split Payments** | Partial amounts, multiple methods | Clear allocation rules |
| **Tip Adjustments** | After-the-fact changes | Audit trail, time limits |
| **Batch Reconciliation** | Matching with gateway | Daily reconciliation process |

### Performance Considerations

| Concern | Impact | Strategy |
|---------|--------|----------|
| **Gateway Latency** | User-facing delay | Async with timeout |
| **Batch Processing** | Settlement time | Off-peak scheduling |
| **Receipt Generation** | Printing delay | Async print queue |

### Failure Scenarios

| Scenario | Impact | Handling |
|----------|--------|----------|
| **Gateway Timeout** | Payment in limbo | Idempotent retry, void stale |
| **Network Failure** | Offline operation | Store-and-forward |
| **Drawer Mismatch** | Accounting variance | Manager resolution workflow |
| **Batch Failure** | Settlement delay | Alert, retry mechanism |
