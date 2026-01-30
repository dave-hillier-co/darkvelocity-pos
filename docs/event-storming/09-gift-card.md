# Event Storming: Gift Card Domain

## Overview

The Gift Card domain manages the complete lifecycle of gift cards from issuance through redemption and expiration. This includes physical and digital cards, balance management, reloading, transfers, and integration with payments and customer accounts.

---

## Domain Purpose

- **Card Issuance**: Create and activate physical and digital gift cards
- **Balance Management**: Track and maintain card balances
- **Redemption**: Process gift card payments
- **Reloading**: Add value to existing cards
- **Fraud Prevention**: Monitor and prevent fraudulent usage
- **Reporting**: Track liability and redemption patterns

---

## Actors

| Actor | Description | Typical Actions |
|-------|-------------|-----------------|
| **Cashier** | Staff issuing/redeeming | Sell cards, process redemptions |
| **Customer** | Card holder | Purchase, check balance, redeem |
| **Manager** | Oversight | Adjust balances, investigate fraud |
| **System** | Automated processes | Expire cards, send notifications |
| **Recipient** | Gift recipient | Receive and use card |

---

## Aggregates

### GiftCard Aggregate

Represents a gift card and its lifecycle.

```
GiftCard
├── Id: Guid
├── OrgId: Guid
├── CardNumber: string
├── Pin?: string (hashed)
├── Type: GiftCardType
├── Status: GiftCardStatus
├── InitialBalance: decimal
├── CurrentBalance: decimal
├── Currency: string
├── IssuedAt: DateTime
├── ActivatedAt?: DateTime
├── ExpiresAt?: DateTime
├── LastUsedAt?: DateTime
├── PurchaserId?: Guid
├── RecipientInfo?: RecipientInfo
├── LinkedCustomerId?: Guid
├── IssuingSiteId: Guid
├── Source: CardSource
├── Transactions: List<CardTransaction>
├── SuspendedAt?: DateTime
├── SuspensionReason?: string
└── Metadata: Dictionary<string, string>
```

**Invariants:**
- CurrentBalance >= 0
- CurrentBalance <= InitialBalance + TotalReloads
- Cannot redeem if suspended or expired
- Card number must be unique within organization

### CardTransaction Entity

```
CardTransaction
├── Id: Guid
├── Type: TransactionType
├── Amount: decimal
├── BalanceAfter: decimal
├── Timestamp: DateTime
├── SiteId: Guid
├── OrderId?: Guid
├── PerformedBy: Guid
├── Reference?: string
└── Notes?: string
```

### RecipientInfo Value Object

```
RecipientInfo
├── Name?: string
├── Email?: string
├── Phone?: string
├── Message?: string
└── DeliveryDate?: DateTime
```

### GiftCardProgram Aggregate

Configuration for gift card offerings.

```
GiftCardProgram
├── Id: Guid
├── OrgId: Guid
├── Name: string
├── Status: ProgramStatus
├── CardDesigns: List<CardDesign>
├── Denominations: List<decimal>
├── AllowCustomAmount: bool
├── MinAmount: decimal
├── MaxAmount: decimal
├── ExpiryPolicy: ExpiryPolicy
├── ReloadPolicy: ReloadPolicy
├── FeeStructure?: FeeStructure
└── Terms: string
```

---

## Gift Card State Machine

```
┌─────────────┐
│   Created   │ (Physical: pre-printed)
└──────┬──────┘
       │
       │ Activate (Sell)
       ▼
┌─────────────┐
│   Active    │◄─────────────┐
└──────┬──────┘              │
       │                     │
       ├─────────┬───────────┼───────────┐
       │         │           │           │
       │ Redeem  │ Reload    │ Transfer  │ Suspend
       │         │           │           │
       ▼         ▼           │           ▼
┌──────────┐  ┌──────────┐   │    ┌─────────────┐
│ (reduced │  │ (balance │   │    │  Suspended  │
│  balance)│  │  added)  │───┘    └──────┬──────┘
└──────────┘  └──────────┘               │
       │                                 │ Resume
       │ Balance = 0                     │
       ▼                                 ▼
┌─────────────┐                   ┌─────────────┐
│  Depleted   │                   │   Active    │
└─────────────┘                   └─────────────┘

From Active or Depleted (after expiry date):
       │
       │ Expire
       ▼
┌─────────────┐
│   Expired   │
└─────────────┘
```

---

## Commands

### Issuance Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `IssueGiftCard` | Create and activate | Amount within limits | Cashier |
| `IssueDigitalCard` | Create for email/SMS | Valid recipient | Customer, Cashier |
| `ActivatePhysicalCard` | Activate pre-printed | Card exists, inactive | Cashier |
| `BatchCreateCards` | Create inventory | Manager approval | Manager |
| `CustomizeCard` | Set design/message | Card exists | Customer |

### Balance Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `RedeemGiftCard` | Use for payment | Card active, balance > 0 | Cashier |
| `ReloadGiftCard` | Add value | Card active, within limits | Cashier |
| `AdjustBalance` | Manual correction | Manager approval | Manager |
| `RefundToCard` | Return to card | Card active | Cashier |
| `CheckBalance` | Query balance | Card exists | Anyone |

### Management Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `TransferCard` | Change ownership | Card active | Customer |
| `LinkToCustomer` | Connect to profile | Card active | Customer |
| `SuspendCard` | Freeze card | Card active | Manager |
| `ResumeCard` | Unfreeze card | Card suspended | Manager |
| `ExpireCard` | Force expiration | Card exists | System |
| `ReplaceCard` | Issue replacement | Card lost/stolen | Manager |
| `MergeCards` | Combine balances | Both cards active | Manager |

---

## Domain Events

### Issuance Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `GiftCardIssued` | Card created and activated | CardId, CardNumber, Amount, Type | IssueGiftCard |
| `DigitalCardIssued` | Digital card created | CardId, RecipientEmail, Amount | IssueDigitalCard |
| `PhysicalCardActivated` | Pre-printed activated | CardId, CardNumber, Amount | ActivatePhysicalCard |
| `CardBatchCreated` | Batch of cards created | BatchId, Count, TotalValue | BatchCreateCards |
| `CardCustomized` | Design/message set | CardId, Design, Message | CustomizeCard |

### Balance Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `GiftCardRedeemed` | Balance used | CardId, Amount, OrderId, NewBalance | RedeemGiftCard |
| `GiftCardReloaded` | Value added | CardId, Amount, NewBalance | ReloadGiftCard |
| `BalanceAdjusted` | Manual change | CardId, OldBalance, NewBalance, Reason | AdjustBalance |
| `RefundAppliedToCard` | Refund credited | CardId, Amount, OrderId, NewBalance | RefundToCard |
| `GiftCardDepleted` | Balance = 0 | CardId, TotalRedeemed, History | Automatic |
| `BalanceChecked` | Balance inquiry | CardId, Balance, CheckedAt | CheckBalance |

### Lifecycle Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `GiftCardTransferred` | Ownership changed | CardId, OldOwner, NewOwner | TransferCard |
| `CardLinkedToCustomer` | Connected to profile | CardId, CustomerId | LinkToCustomer |
| `GiftCardSuspended` | Card frozen | CardId, Reason, SuspendedBy | SuspendCard |
| `GiftCardResumed` | Card unfrozen | CardId, ResumedBy | ResumeCard |
| `GiftCardExpired` | Card expired | CardId, RemainingBalance, ExpiredAt | ExpireCard |
| `GiftCardReplaced` | Replacement issued | OldCardId, NewCardId, Balance | ReplaceCard |
| `CardsMerged` | Balances combined | SourceCardIds, TargetCardId, TotalBalance | MergeCards |

### Fraud Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `SuspiciousActivityDetected` | Unusual pattern | CardId, ActivityType, Details | System |
| `InvalidPinAttempted` | Wrong PIN entered | CardId, AttemptCount, Location | CheckBalance/Redeem |
| `CardReportedLost` | Customer report | CardId, ReportedBy | Customer service |
| `CardReportedStolen` | Theft report | CardId, ReportedBy | Customer service |

---

## Event Details

### GiftCardIssued

```csharp
public record GiftCardIssued : DomainEvent
{
    public override string EventType => "giftcards.card.issued";

    public required Guid CardId { get; init; }
    public required string CardNumber { get; init; }
    public required GiftCardType Type { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required Guid IssuingSiteId { get; init; }
    public required Guid IssuedBy { get; init; }
    public required DateTime IssuedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public Guid? PurchaserId { get; init; }
    public RecipientInfo? Recipient { get; init; }
    public string? CardDesignId { get; init; }
    public decimal? PurchaseAmount { get; init; } // May differ if promotional
    public Guid? PaymentId { get; init; }
    public CardSource Source { get; init; }
}

public enum GiftCardType
{
    Physical,
    Digital,
    Virtual
}

public enum CardSource
{
    Purchase,
    Promotional,
    Reward,
    Replacement,
    Refund,
    Corporate
}
```

### GiftCardRedeemed

```csharp
public record GiftCardRedeemed : DomainEvent
{
    public override string EventType => "giftcards.card.redeemed";

    public required Guid CardId { get; init; }
    public required string CardNumber { get; init; }
    public required decimal Amount { get; init; }
    public required decimal PreviousBalance { get; init; }
    public required decimal NewBalance { get; init; }
    public required Guid OrderId { get; init; }
    public required Guid SiteId { get; init; }
    public required Guid RedeemedBy { get; init; }
    public required DateTime RedeemedAt { get; init; }
    public bool IsFullRedemption { get; init; }
    public bool IsDepleted { get; init; }
}
```

### GiftCardReloaded

```csharp
public record GiftCardReloaded : DomainEvent
{
    public override string EventType => "giftcards.card.reloaded";

    public required Guid CardId { get; init; }
    public required string CardNumber { get; init; }
    public required decimal Amount { get; init; }
    public required decimal PreviousBalance { get; init; }
    public required decimal NewBalance { get; init; }
    public required Guid SiteId { get; init; }
    public required Guid ReloadedBy { get; init; }
    public required DateTime ReloadedAt { get; init; }
    public Guid? PaymentId { get; init; }
    public Guid? CustomerId { get; init; }
}
```

### GiftCardExpired

```csharp
public record GiftCardExpired : DomainEvent
{
    public override string EventType => "giftcards.card.expired";

    public required Guid CardId { get; init; }
    public required string CardNumber { get; init; }
    public required decimal RemainingBalance { get; init; }
    public required DateTime OriginalExpiryDate { get; init; }
    public required DateTime ExpiredAt { get; init; }
    public required decimal TotalIssued { get; init; }
    public required decimal TotalRedeemed { get; init; }
    public bool WasNotified { get; init; }
    public Guid? LinkedCustomerId { get; init; }
}
```

---

## Policies (Event Reactions)

### When GiftCardIssued

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Send Digital Card | Email/SMS with card details | Notifications |
| Record Liability | Post deferred revenue | Accounting |
| Register Payment | Link to payment transaction | Payments |
| Send Purchase Receipt | Receipt to purchaser | Notifications |

### When GiftCardRedeemed

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Apply to Order | Credit order payment | Orders |
| Recognize Revenue | Convert from deferred | Accounting |
| Update Customer History | Track usage | Customer |
| Check Depletion | Send depleted notice if zero | Notifications |

### When GiftCardExpired

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Recognize Breakage | Convert remaining to revenue | Accounting |
| Notify Customer | Expiration notice | Notifications |
| Update Liability | Remove from balance sheet | Accounting |

### When SuspiciousActivityDetected

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Auto-Suspend | Freeze card if threshold met | Gift Card |
| Alert Security | Notify fraud team | Notifications |
| Log for Review | Record for investigation | Audit |

---

## Read Models / Projections

### GiftCardView

```csharp
public record GiftCardView
{
    public Guid Id { get; init; }
    public string CardNumber { get; init; }
    public string MaskedNumber { get; init; } // ****1234
    public GiftCardType Type { get; init; }
    public GiftCardStatus Status { get; init; }
    public decimal InitialBalance { get; init; }
    public decimal CurrentBalance { get; init; }
    public string Currency { get; init; }
    public DateTime IssuedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public int? DaysUntilExpiry { get; init; }
    public DateTime? LastUsedAt { get; init; }
    public string? IssuingSiteName { get; init; }
    public string? LinkedCustomerName { get; init; }
    public string? CardDesignUrl { get; init; }
}
```

### GiftCardTransactionHistory

```csharp
public record GiftCardTransactionHistory
{
    public Guid CardId { get; init; }
    public string CardNumber { get; init; }
    public decimal CurrentBalance { get; init; }
    public IReadOnlyList<TransactionView> Transactions { get; init; }
}

public record TransactionView
{
    public Guid TransactionId { get; init; }
    public DateTime Timestamp { get; init; }
    public TransactionType Type { get; init; }
    public decimal Amount { get; init; }
    public decimal BalanceAfter { get; init; }
    public string SiteName { get; init; }
    public string? OrderNumber { get; init; }
    public string PerformedByName { get; init; }
    public string? Notes { get; init; }
}
```

### GiftCardLiabilityReport

```csharp
public record GiftCardLiabilityReport
{
    public Guid OrgId { get; init; }
    public DateTime AsOfDate { get; init; }

    // Outstanding Liability
    public decimal TotalOutstandingBalance { get; init; }
    public int ActiveCardCount { get; init; }
    public decimal AverageBalance { get; init; }

    // By Status
    public decimal ActiveCardsBalance { get; init; }
    public decimal SuspendedCardsBalance { get; init; }
    public decimal ExpiringSoonBalance { get; init; }

    // Period Activity
    public decimal IssuedThisPeriod { get; init; }
    public decimal RedeemedThisPeriod { get; init; }
    public decimal ExpiredThisPeriod { get; init; }
    public decimal NetChange { get; init; }

    // Aging
    public IReadOnlyList<AgingBucket> BalanceAging { get; init; }

    // Breakage Estimate
    public decimal EstimatedBreakage { get; init; }
    public decimal BreakageRate { get; init; }
}

public record AgingBucket
{
    public string Range { get; init; } // "0-30 days", "31-60 days", etc.
    public int CardCount { get; init; }
    public decimal Balance { get; init; }
    public decimal Percentage { get; init; }
}
```

### GiftCardSearchResult

```csharp
public record GiftCardSearchResult
{
    public Guid Id { get; init; }
    public string CardNumber { get; init; }
    public GiftCardStatus Status { get; init; }
    public decimal Balance { get; init; }
    public DateTime IssuedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? CustomerName { get; init; }
    public DateTime? LastUsedAt { get; init; }
}
```

---

## Bounded Context Relationships

### Upstream Contexts (This domain provides data to)

| Context | Relationship | Data Provided |
|---------|--------------|---------------|
| Payments | Published Language | Payment method, balance |
| Accounting | Published Language | Liability, revenue recognition |
| Reporting | Published Language | Card metrics |

### Downstream Contexts (This domain consumes from)

| Context | Relationship | Data Consumed |
|---------|--------------|---------------|
| Payments | Customer/Supplier | Payment completion |
| Orders | Customer/Supplier | Order for redemption |
| Customer | Customer/Supplier | Customer linking |

---

## Business Rules

### Issuance Rules

1. **Amount Limits**: Min $5, Max $500 (configurable)
2. **Denominations**: Pre-set amounts or custom within range
3. **PIN Protection**: Optional PIN for balance checks
4. **Activation Required**: Physical cards must be activated

### Redemption Rules

1. **Partial Redemption**: Use any amount up to balance
2. **No Cash Back**: Cannot convert to cash
3. **Split Tender**: Can combine with other payment methods
4. **Balance Display**: Show remaining after redemption

### Expiration Rules

1. **Expiry Period**: Configurable (typically 12-24 months)
2. **Expiry Notification**: 30, 7, 1 day warnings
3. **No Extension**: Expired cards cannot be reactivated
4. **Breakage Revenue**: Expired balances recognized as revenue

### Fraud Prevention Rules

1. **Velocity Limits**: Max redemptions per time period
2. **Location Mismatch**: Alert on unusual locations
3. **PIN Lockout**: Lock after 3 failed PIN attempts
4. **High Balance Alert**: Extra verification for large balances

---

## Event Type Registry

```csharp
public static class GiftCardEventTypes
{
    // Issuance
    public const string GiftCardIssued = "giftcards.card.issued";
    public const string DigitalCardIssued = "giftcards.card.digital_issued";
    public const string PhysicalCardActivated = "giftcards.card.physical_activated";
    public const string CardBatchCreated = "giftcards.card.batch_created";
    public const string CardCustomized = "giftcards.card.customized";

    // Balance
    public const string GiftCardRedeemed = "giftcards.card.redeemed";
    public const string GiftCardReloaded = "giftcards.card.reloaded";
    public const string BalanceAdjusted = "giftcards.card.balance_adjusted";
    public const string RefundAppliedToCard = "giftcards.card.refund_applied";
    public const string GiftCardDepleted = "giftcards.card.depleted";
    public const string BalanceChecked = "giftcards.card.balance_checked";

    // Lifecycle
    public const string GiftCardTransferred = "giftcards.card.transferred";
    public const string CardLinkedToCustomer = "giftcards.card.linked_to_customer";
    public const string GiftCardSuspended = "giftcards.card.suspended";
    public const string GiftCardResumed = "giftcards.card.resumed";
    public const string GiftCardExpired = "giftcards.card.expired";
    public const string GiftCardReplaced = "giftcards.card.replaced";
    public const string CardsMerged = "giftcards.card.merged";

    // Fraud
    public const string SuspiciousActivityDetected = "giftcards.fraud.suspicious_activity";
    public const string InvalidPinAttempted = "giftcards.fraud.invalid_pin_attempted";
    public const string CardReportedLost = "giftcards.fraud.reported_lost";
    public const string CardReportedStolen = "giftcards.fraud.reported_stolen";
}
```
