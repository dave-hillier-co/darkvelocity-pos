# Event Storming: Accounting Domain

## Overview

The Accounting domain manages the financial record-keeping for the DarkVelocity POS system, including journal entries, general ledger, revenue recognition, expense tracking, cost of goods sold (COGS), and period management. This domain ensures accurate financial reporting and compliance.

---

## Domain Purpose

- **General Ledger**: Maintain chart of accounts and account balances
- **Journal Entries**: Record double-entry transactions
- **Revenue Recognition**: Track sales, taxes, and discounts
- **COGS Tracking**: Calculate and record cost of goods sold
- **Period Management**: Handle accounting periods and closing
- **Reconciliation**: Support bank and cash reconciliation
- **Reporting**: Generate financial statements and reports

---

## Actors

| Actor | Description | Typical Actions |
|-------|-------------|-----------------|
| **System** | Automated postings | Post sales, COGS, payments |
| **Finance Manager** | Accounting oversight | Review entries, close periods |
| **Controller** | Senior finance | Approve adjustments, review statements |
| **Auditor** | External/internal audit | Review entries, verify compliance |

---

## Aggregates

### AccountingPeriod Aggregate

Represents a fiscal period (typically monthly).

```
AccountingPeriod
├── Id: Guid
├── OrgId: Guid
├── PeriodType: PeriodType
├── StartDate: DateOnly
├── EndDate: DateOnly
├── Status: PeriodStatus
├── OpenedAt: DateTime
├── ClosedAt?: DateTime
├── ClosedBy?: Guid
├── TrialBalance?: TrialBalance
├── PreviousPeriodId?: Guid
└── Adjustments: List<PeriodAdjustment>
```

**Invariants:**
- Cannot post to closed periods
- Periods must be sequential
- Only one open period per type at a time

### GeneralLedger Aggregate (per Period)

Manages the ledger for a specific period.

```
GeneralLedger
├── PeriodId: Guid
├── OrgId: Guid
├── Accounts: List<LedgerAccount>
├── JournalEntries: List<JournalEntry>
├── TotalDebits: decimal
├── TotalCredits: decimal
├── IsBalanced: bool
└── LastEntryAt: DateTime
```

### LedgerAccount Entity

```
LedgerAccount
├── Id: Guid
├── AccountNumber: string
├── Name: string
├── Type: AccountType
├── Category: AccountCategory
├── ParentAccountId?: Guid
├── NormalBalance: DebitCredit
├── OpeningBalance: decimal
├── CurrentBalance: decimal
├── IsActive: bool
├── IsSystem: bool
└── CostCenterId?: Guid
```

### JournalEntry Entity

```
JournalEntry
├── Id: Guid
├── PeriodId: Guid
├── EntryNumber: string
├── Type: JournalEntryType
├── Date: DateOnly
├── Description: string
├── Lines: List<JournalLine>
├── TotalDebits: decimal
├── TotalCredits: decimal
├── SourceType?: string
├── SourceId?: Guid
├── Status: EntryStatus
├── CreatedAt: DateTime
├── CreatedBy: Guid
├── ApprovedAt?: DateTime
├── ApprovedBy?: Guid
├── ReversedEntryId?: Guid
└── Tags: List<string>
```

### JournalLine Value Object

```
JournalLine
├── AccountId: Guid
├── AccountNumber: string
├── AccountName: string
├── DebitAmount: decimal
├── CreditAmount: decimal
├── Memo?: string
├── CostCenterId?: Guid
└── Dimensions: Dictionary<string, string>
```

### ChartOfAccounts Aggregate

Master list of accounts for an organization.

```
ChartOfAccounts
├── OrgId: Guid
├── Accounts: List<AccountDefinition>
├── Categories: List<AccountCategory>
├── CostCenters: List<CostCenter>
└── DefaultAccounts: DefaultAccountMapping
```

---

## Period State Machine

```
┌─────────────┐
│   Future    │
└──────┬──────┘
       │
       │ Period start date reached
       ▼
┌─────────────┐
│    Open     │
└──────┬──────┘
       │
       │ Initiate close
       ▼
┌─────────────┐
│  Closing    │ (No new transactions)
└──────┬──────┘
       │
       │ Adjustments complete
       ▼
┌─────────────┐
│   Closed    │
└──────┬──────┘
       │
       │ If reopen needed
       ▼
┌─────────────┐
│  Reopened   │───────> Open
└─────────────┘
```

---

## Commands

### Period Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `OpenPeriod` | Start new period | Previous closed | System |
| `InitiateClose` | Begin closing process | Period open | Finance Manager |
| `PostAdjustingEntry` | Period-end adjustments | Period closing | Finance Manager |
| `ClosePeriod` | Finalize period | All adjustments complete | Controller |
| `ReopenPeriod` | Reopen closed period | Period closed, approval | Controller |
| `LockPeriod` | Permanently lock | Period closed | Controller |

### Journal Entry Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `PostJournalEntry` | Create entry | Period open, balanced | System, Finance |
| `ReverseEntry` | Negate entry | Entry exists | Finance Manager |
| `VoidEntry` | Cancel entry | Entry posted | Finance Manager |
| `ApproveEntry` | Approve manual entry | Entry pending | Controller |
| `RejectEntry` | Reject manual entry | Entry pending | Controller |
| `RecurringEntry` | Post recurring entry | Template exists | System |

### Account Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `CreateAccount` | Add account | Valid number | Finance Manager |
| `UpdateAccount` | Modify account | Account exists | Finance Manager |
| `DeactivateAccount` | Disable account | Zero balance | Finance Manager |
| `ReactivateAccount` | Re-enable account | Account inactive | Finance Manager |
| `CreateCostCenter` | Add cost center | Unique code | Finance Manager |

### Auto-Posting Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `PostSaleEntry` | Record sale revenue | Order settled | System |
| `PostPaymentEntry` | Record payment | Payment complete | System |
| `PostCOGSEntry` | Record COGS | Sale with recipes | System |
| `PostPurchaseEntry` | Record purchase | Invoice approved | System |
| `PostPayrollEntry` | Record payroll | Payroll processed | System |

---

## Domain Events

### Period Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `PeriodOpened` | Period started | PeriodId, StartDate, EndDate | OpenPeriod |
| `PeriodCloseInitiated` | Closing began | PeriodId, InitiatedBy | InitiateClose |
| `PeriodClosed` | Period finalized | PeriodId, TrialBalance, ClosedBy | ClosePeriod |
| `PeriodReopened` | Period reopened | PeriodId, ReopenedBy, Reason | ReopenPeriod |
| `PeriodLocked` | Permanently locked | PeriodId, LockedBy | LockPeriod |

### Journal Entry Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `JournalEntryPosted` | Entry created | EntryId, Lines, Totals, Source | PostJournalEntry |
| `JournalEntryReversed` | Entry negated | EntryId, ReversalEntryId, Reason | ReverseEntry |
| `JournalEntryVoided` | Entry cancelled | EntryId, VoidedBy, Reason | VoidEntry |
| `JournalEntryApproved` | Entry approved | EntryId, ApprovedBy | ApproveEntry |
| `JournalEntryRejected` | Entry rejected | EntryId, RejectedBy, Reason | RejectEntry |
| `RecurringEntryPosted` | Recurring posted | EntryId, TemplateId | RecurringEntry |

### Account Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `AccountCreated` | Account added | AccountId, Number, Name, Type | CreateAccount |
| `AccountUpdated` | Account modified | AccountId, Changes | UpdateAccount |
| `AccountDeactivated` | Account disabled | AccountId, DeactivatedBy | DeactivateAccount |
| `AccountReactivated` | Account enabled | AccountId, ReactivatedBy | ReactivateAccount |
| `CostCenterCreated` | Cost center added | CostCenterId, Code, Name | CreateCostCenter |

### Financial Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `SaleRecognized` | Revenue recorded | OrderId, Revenue, Tax, Discounts | PostSaleEntry |
| `PaymentRecorded` | Payment posted | PaymentId, Amount, Method | PostPaymentEntry |
| `COGSRecorded` | Cost recorded | OrderId, COGS, Items | PostCOGSEntry |
| `PurchaseRecorded` | Expense posted | InvoiceId, Amount, Supplier | PostPurchaseEntry |
| `PayrollRecorded` | Labor cost posted | PayrollId, Wages, Taxes | PostPayrollEntry |

---

## Event Details

### JournalEntryPosted

```csharp
public record JournalEntryPosted : DomainEvent
{
    public override string EventType => "accounting.journal.posted";

    public required Guid EntryId { get; init; }
    public required Guid PeriodId { get; init; }
    public required string EntryNumber { get; init; }
    public required JournalEntryType Type { get; init; }
    public required DateOnly Date { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<JournalLineInfo> Lines { get; init; }
    public required decimal TotalDebits { get; init; }
    public required decimal TotalCredits { get; init; }
    public string? SourceType { get; init; }
    public Guid? SourceId { get; init; }
    public required Guid PostedBy { get; init; }
    public required DateTime PostedAt { get; init; }
}

public record JournalLineInfo
{
    public Guid AccountId { get; init; }
    public string AccountNumber { get; init; }
    public string AccountName { get; init; }
    public decimal DebitAmount { get; init; }
    public decimal CreditAmount { get; init; }
    public string? Memo { get; init; }
    public string? CostCenterCode { get; init; }
}

public enum JournalEntryType
{
    Standard,
    Adjusting,
    Closing,
    Reversing,
    Recurring,
    System
}
```

### SaleRecognized

```csharp
public record SaleRecognized : DomainEvent
{
    public override string EventType => "accounting.revenue.sale_recognized";

    public required Guid EntryId { get; init; }
    public required Guid OrderId { get; init; }
    public required Guid SiteId { get; init; }
    public required DateOnly Date { get; init; }

    // Revenue Breakdown
    public required decimal GrossRevenue { get; init; }
    public required decimal Discounts { get; init; }
    public required decimal NetRevenue { get; init; }

    // Tax Breakdown
    public required IReadOnlyList<TaxLine> Taxes { get; init; }
    public required decimal TotalTax { get; init; }

    // Categories
    public required IReadOnlyList<RevenueCategoryBreakdown> ByCategory { get; init; }

    public required DateTime RecordedAt { get; init; }
}

public record TaxLine
{
    public string TaxName { get; init; }
    public decimal TaxRate { get; init; }
    public decimal TaxableAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public Guid TaxAccountId { get; init; }
}

public record RevenueCategoryBreakdown
{
    public string Category { get; init; }
    public decimal Amount { get; init; }
    public Guid RevenueAccountId { get; init; }
}
```

### COGSRecorded

```csharp
public record COGSRecorded : DomainEvent
{
    public override string EventType => "accounting.cogs.recorded";

    public required Guid EntryId { get; init; }
    public required Guid OrderId { get; init; }
    public required Guid SiteId { get; init; }
    public required DateOnly Date { get; init; }
    public required decimal TotalCOGS { get; init; }
    public required IReadOnlyList<COGSItem> Items { get; init; }
    public required DateTime RecordedAt { get; init; }
}

public record COGSItem
{
    public Guid IngredientId { get; init; }
    public string IngredientName { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public decimal TotalCost { get; init; }
    public Guid COGSAccountId { get; init; }
    public Guid InventoryAccountId { get; init; }
}
```

### PeriodClosed

```csharp
public record PeriodClosed : DomainEvent
{
    public override string EventType => "accounting.period.closed";

    public required Guid PeriodId { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required TrialBalanceSummary TrialBalance { get; init; }
    public required decimal NetIncome { get; init; }
    public required int JournalEntryCount { get; init; }
    public required Guid ClosedBy { get; init; }
    public required DateTime ClosedAt { get; init; }
}

public record TrialBalanceSummary
{
    public decimal TotalDebits { get; init; }
    public decimal TotalCredits { get; init; }
    public bool IsBalanced { get; init; }
    public IReadOnlyList<AccountBalance> Accounts { get; init; }
}

public record AccountBalance
{
    public Guid AccountId { get; init; }
    public string AccountNumber { get; init; }
    public string AccountName { get; init; }
    public AccountType Type { get; init; }
    public decimal DebitBalance { get; init; }
    public decimal CreditBalance { get; init; }
}
```

---

## Policies (Event Reactions)

### When OrderSettled (from Orders domain)

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Post Revenue Entry | Record sales revenue | Accounting |
| Post Tax Entry | Record tax liability | Accounting |
| Post Discount Entry | Record discount expense | Accounting |
| Post COGS Entry | Record cost of goods | Accounting |

### When PaymentCompleted (from Payments domain)

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Post Cash Entry | Debit cash, credit AR | Accounting |
| Post Card Entry | Debit merchant receivable | Accounting |
| Record Tip Liability | Credit tip payable | Accounting |

### When InvoicePaid (from Procurement domain)

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Post AP Entry | Debit AP, credit bank | Accounting |
| Clear Accrual | Reverse accrued expense | Accounting |

### When PeriodCloseInitiated

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Generate Trial Balance | Calculate balances | Accounting |
| Identify Open Items | List unreconciled | Reconciliation |
| Notify Finance | Alert period closing | Notifications |

### When PeriodClosed

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Open Next Period | Create next period | Accounting |
| Generate Financials | Create P&L, Balance Sheet | Reporting |
| Archive Entries | Move to archive | Archival |

---

## Read Models / Projections

### TrialBalanceView

```csharp
public record TrialBalanceView
{
    public Guid PeriodId { get; init; }
    public DateOnly AsOfDate { get; init; }
    public IReadOnlyList<TrialBalanceAccount> Accounts { get; init; }
    public decimal TotalDebits { get; init; }
    public decimal TotalCredits { get; init; }
    public bool IsBalanced { get; init; }
}

public record TrialBalanceAccount
{
    public string AccountNumber { get; init; }
    public string AccountName { get; init; }
    public AccountType Type { get; init; }
    public decimal DebitBalance { get; init; }
    public decimal CreditBalance { get; init; }
}
```

### IncomeStatementView

```csharp
public record IncomeStatementView
{
    public Guid OrgId { get; init; }
    public DateRange Period { get; init; }

    // Revenue
    public decimal GrossRevenue { get; init; }
    public decimal Discounts { get; init; }
    public decimal NetRevenue { get; init; }
    public IReadOnlyList<CategoryAmount> RevenueByCategory { get; init; }

    // Cost of Goods Sold
    public decimal COGS { get; init; }
    public IReadOnlyList<CategoryAmount> COGSByCategory { get; init; }

    // Gross Profit
    public decimal GrossProfit { get; init; }
    public decimal GrossProfitMargin { get; init; }

    // Operating Expenses
    public decimal TotalOperatingExpenses { get; init; }
    public IReadOnlyList<ExpenseCategory> OperatingExpenses { get; init; }

    // Operating Income
    public decimal OperatingIncome { get; init; }

    // Other Income/Expense
    public decimal OtherIncome { get; init; }
    public decimal OtherExpense { get; init; }

    // Net Income
    public decimal NetIncome { get; init; }
    public decimal NetMargin { get; init; }
}
```

### BalanceSheetView

```csharp
public record BalanceSheetView
{
    public Guid OrgId { get; init; }
    public DateOnly AsOfDate { get; init; }

    // Assets
    public AssetSection Assets { get; init; }
    public decimal TotalAssets { get; init; }

    // Liabilities
    public LiabilitySection Liabilities { get; init; }
    public decimal TotalLiabilities { get; init; }

    // Equity
    public EquitySection Equity { get; init; }
    public decimal TotalEquity { get; init; }

    // Balance Check
    public decimal TotalLiabilitiesAndEquity { get; init; }
    public bool IsBalanced { get; init; }
}

public record AssetSection
{
    public IReadOnlyList<AccountAmount> CurrentAssets { get; init; }
    public decimal TotalCurrentAssets { get; init; }
    public IReadOnlyList<AccountAmount> FixedAssets { get; init; }
    public decimal TotalFixedAssets { get; init; }
}
```

### JournalEntryDetailView

```csharp
public record JournalEntryDetailView
{
    public Guid EntryId { get; init; }
    public string EntryNumber { get; init; }
    public JournalEntryType Type { get; init; }
    public EntryStatus Status { get; init; }
    public DateOnly Date { get; init; }
    public string Description { get; init; }
    public IReadOnlyList<JournalLineView> Lines { get; init; }
    public decimal TotalDebits { get; init; }
    public decimal TotalCredits { get; init; }
    public string? SourceType { get; init; }
    public string? SourceReference { get; init; }
    public string CreatedByName { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? ApprovedByName { get; init; }
    public DateTime? ApprovedAt { get; init; }
}
```

### CostCenterProfitability

```csharp
public record CostCenterProfitability
{
    public string CostCenterCode { get; init; }
    public string CostCenterName { get; init; }
    public DateRange Period { get; init; }
    public decimal Revenue { get; init; }
    public decimal DirectCosts { get; init; }
    public decimal AllocatedCosts { get; init; }
    public decimal TotalCosts { get; init; }
    public decimal Profit { get; init; }
    public decimal ProfitMargin { get; init; }
}
```

---

## Standard Account Structure

```
1xxx - Assets
  1000 - Cash
  1010 - Cash on Hand
  1020 - Bank Account
  1100 - Accounts Receivable
  1200 - Inventory
  1300 - Prepaid Expenses
  1500 - Fixed Assets
  1510 - Equipment
  1520 - Accumulated Depreciation

2xxx - Liabilities
  2000 - Accounts Payable
  2100 - Sales Tax Payable
  2200 - Payroll Liabilities
  2210 - Wages Payable
  2220 - Tax Withholdings
  2300 - Gift Card Liability
  2400 - Deferred Revenue

3xxx - Equity
  3000 - Retained Earnings
  3100 - Current Year Earnings

4xxx - Revenue
  4000 - Sales Revenue
  4010 - Food Sales
  4020 - Beverage Sales
  4030 - Merchandise Sales
  4100 - Discounts
  4200 - Service Charges
  4900 - Other Income

5xxx - Cost of Goods Sold
  5000 - COGS - Food
  5100 - COGS - Beverage
  5200 - COGS - Merchandise

6xxx - Operating Expenses
  6000 - Payroll Expense
  6100 - Rent Expense
  6200 - Utilities
  6300 - Marketing
  6400 - Supplies
  6500 - Repairs & Maintenance
  6600 - Insurance
  6700 - Depreciation
  6800 - Professional Fees
  6900 - Other Operating Expenses

7xxx - Other Income/Expense
  7000 - Interest Income
  7100 - Interest Expense
  7200 - Bank Fees
```

---

## Event Type Registry

```csharp
public static class AccountingEventTypes
{
    // Period Lifecycle
    public const string PeriodOpened = "accounting.period.opened";
    public const string PeriodCloseInitiated = "accounting.period.close_initiated";
    public const string PeriodClosed = "accounting.period.closed";
    public const string PeriodReopened = "accounting.period.reopened";
    public const string PeriodLocked = "accounting.period.locked";

    // Journal Entries
    public const string JournalEntryPosted = "accounting.journal.posted";
    public const string JournalEntryReversed = "accounting.journal.reversed";
    public const string JournalEntryVoided = "accounting.journal.voided";
    public const string JournalEntryApproved = "accounting.journal.approved";
    public const string JournalEntryRejected = "accounting.journal.rejected";
    public const string RecurringEntryPosted = "accounting.journal.recurring_posted";

    // Accounts
    public const string AccountCreated = "accounting.account.created";
    public const string AccountUpdated = "accounting.account.updated";
    public const string AccountDeactivated = "accounting.account.deactivated";
    public const string AccountReactivated = "accounting.account.reactivated";
    public const string CostCenterCreated = "accounting.cost_center.created";

    // Financial Recognition
    public const string SaleRecognized = "accounting.revenue.sale_recognized";
    public const string PaymentRecorded = "accounting.revenue.payment_recorded";
    public const string COGSRecorded = "accounting.cogs.recorded";
    public const string PurchaseRecorded = "accounting.expense.purchase_recorded";
    public const string PayrollRecorded = "accounting.expense.payroll_recorded";
}
```
