# DarkVelocity POS - Additional Features Plan

This document outlines the implementation plan for additional core features required to make DarkVelocity POS a complete, production-ready system for European markets with multi-tenant SaaS deployment.

---

## Table of Contents

1. [Gift Cards Service](#1-gift-cards-service)
2. [Fiscalisation Service](#2-fiscalisation-service)
3. [Accounting Service](#3-accounting-service)
4. [Orders Gateway Service](#4-orders-gateway-service)
5. [Customers & Loyalty Service](#5-customers--loyalty-service)
6. [Labor Management Service](#6-labor-management-service)
7. [Multi-Tenancy Architecture](#7-multi-tenancy-architecture)
8. [Kubernetes-Native Deployment](#8-kubernetes-native-deployment-no-api-gateway)
9. [Implementation Phases](#9-implementation-phases)

---

## 1. Gift Cards Service

A dedicated microservice for managing gift cards, vouchers, and stored value products.

### Service Overview

| Attribute | Value |
|-----------|-------|
| Service Name | GiftCards.Api |
| Port | 5012 |
| Database | giftcards_db |
| Dependencies | Payments, Orders, Auth |

### Core Entities

```
GiftCard
├── Id (Guid)
├── CardNumber (string, unique, 16-19 digits)
├── Pin (string, hashed, 4-8 digits)
├── InitialBalance (decimal)
├── CurrentBalance (decimal)
├── Currency (string, ISO 4217)
├── Status (Active, Suspended, Expired, Depleted)
├── CardType (Physical, Digital, Promotional)
├── ExpiryDate (DateTime?)
├── IssuedAt (DateTime)
├── IssuedByLocationId (Guid)
├── IssuedByUserId (Guid)
├── TenantId (Guid)
├── LastUsedAt (DateTime?)
└── Metadata (JSON - custom fields)

GiftCardTransaction
├── Id (Guid)
├── GiftCardId (Guid)
├── TransactionType (Activation, Redemption, Reload, Adjustment, Refund)
├── Amount (decimal)
├── BalanceBefore (decimal)
├── BalanceAfter (decimal)
├── OrderId (Guid?)
├── LocationId (Guid)
├── UserId (Guid)
├── Reason (string?)
├── ProcessedAt (DateTime)
└── ExternalReference (string?) - for fiscal linkage

GiftCardProgram
├── Id (Guid)
├── TenantId (Guid)
├── Name (string)
├── CardNumberPrefix (string)
├── DefaultExpiryMonths (int?)
├── MinimumLoadAmount (decimal)
├── MaximumLoadAmount (decimal)
├── MaximumBalance (decimal)
├── AllowReload (bool)
├── AllowPartialRedemption (bool)
├── RequirePin (bool)
└── IsActive (bool)

GiftCardDesign
├── Id (Guid)
├── ProgramId (Guid)
├── Name (string)
├── ImageUrl (string)
├── IsDefault (bool)
└── IsActive (bool)
```

### API Endpoints (HAL+JSON)

```
Gift Card Management
├── GET    /api/giftcards                           # List cards (with filters)
├── POST   /api/giftcards                           # Create/issue new card
├── GET    /api/giftcards/{id}                      # Get card details
├── GET    /api/giftcards/lookup?number={number}    # Lookup by card number
├── POST   /api/giftcards/{id}/activate             # Activate card
├── POST   /api/giftcards/{id}/suspend              # Suspend card
├── POST   /api/giftcards/{id}/resume               # Resume suspended card
├── POST   /api/giftcards/{id}/reload               # Add balance
├── POST   /api/giftcards/{id}/redeem               # Deduct balance (payment)
├── POST   /api/giftcards/{id}/refund               # Refund to card
├── POST   /api/giftcards/{id}/adjust               # Manual adjustment
├── GET    /api/giftcards/{id}/transactions         # Transaction history
└── GET    /api/giftcards/{id}/balance              # Quick balance check

Programs
├── GET    /api/giftcard-programs                   # List programs
├── POST   /api/giftcard-programs                   # Create program
├── GET    /api/giftcard-programs/{id}              # Get program
├── PUT    /api/giftcard-programs/{id}              # Update program
└── GET    /api/giftcard-programs/{id}/cards        # Cards in program

Designs
├── GET    /api/giftcard-programs/{id}/designs      # List designs
├── POST   /api/giftcard-programs/{id}/designs      # Create design
└── PUT    /api/giftcard-designs/{id}               # Update design

Reporting
├── GET    /api/giftcard-reports/liability          # Outstanding liability
├── GET    /api/giftcard-reports/activity           # Activity summary
└── GET    /api/giftcard-reports/expiring           # Cards expiring soon
```

### Integration Points

**With Payments Service:**
- Gift card as payment method
- Partial payment with gift card + other method
- Refunds to gift card

**With Orders Service:**
- Link redemption to order
- Purchase gift cards as line items

**With Fiscalisation Service:**
- All transactions require fiscal recording
- Gift card liability tracking for tax purposes

### Kafka Events

```
GiftCardIssued
├── CardId, CardNumber, InitialBalance, IssuedAt, LocationId

GiftCardActivated
├── CardId, ActivatedAt, LocationId, UserId

GiftCardRedeemed
├── CardId, Amount, OrderId, BalanceAfter, RedeemedAt

GiftCardReloaded
├── CardId, Amount, BalanceAfter, ReloadedAt

GiftCardExpired
├── CardId, ExpiredAt, FinalBalance

GiftCardBalanceAdjusted
├── CardId, Amount, Reason, AdjustedAt, UserId
```

---

## 2. Fiscalisation Service

A critical service for European tax compliance, specifically designed for German KassenSichV (TSE) requirements but extensible to other jurisdictions.

### Service Overview

| Attribute | Value |
|-----------|-------|
| Service Name | Fiscalisation.Api |
| Port | 5013 |
| Database | fiscalisation_db |
| Dependencies | Orders, Payments, GiftCards, Auth |

### Regulatory Requirements

#### KassenSichV (Germany)
- Technical Security Equipment (TSE) integration
- Tamper-proof transaction logging
- Digital signatures for all transactions
- DSFinV-K export format
- QR code on receipts for verification

#### Other European Requirements (Future)
- Austria: RKSV (Registrierkassensicherheitsverordnung)
- France: NF 525 certification
- Italy: RT (Registratore Telematico)
- Poland: Centralne Repozytorium Kas

### Core Entities

```
FiscalDevice (TSE)
├── Id (Guid)
├── TenantId (Guid)
├── LocationId (Guid)
├── DeviceType (SwissbitCloud, SwissbitUSB, FiskalyCloud, Epson, Diebold)
├── SerialNumber (string)
├── PublicKey (string)
├── CertificateExpiryDate (DateTime)
├── Status (Active, Inactive, Failed, CertificateExpiring)
├── ApiEndpoint (string?) - for cloud TSE
├── ApiCredentialsEncrypted (string?)
├── LastSyncAt (DateTime?)
├── TransactionCounter (long)
└── SignatureCounter (long)

FiscalTransaction
├── Id (Guid)
├── FiscalDeviceId (Guid)
├── TransactionNumber (long) - sequential per device
├── TransactionType (Receipt, TrainingReceipt, Void, Cancellation)
├── ProcessType (Kassenbeleg, AVTransfer, AVBestellung, AVSonstiger)
├── StartTime (DateTime)
├── EndTime (DateTime)
├── SourceType (Order, Payment, GiftCard, CashDrawer)
├── SourceId (Guid)
├── GrossAmount (decimal)
├── NetAmounts (JSON) - per tax rate
├── TaxAmounts (JSON) - per tax rate
├── PaymentTypes (JSON) - breakdown
├── Signature (string)
├── SignatureCounter (long)
├── CertificateSerial (string)
├── QRCodeData (string)
├── TseResponseRaw (string) - full TSE response
├── Status (Pending, Signed, Failed, Retrying)
├── ErrorMessage (string?)
├── RetryCount (int)
└── ExportedAt (DateTime?)

FiscalExport (DSFinV-K)
├── Id (Guid)
├── TenantId (Guid)
├── LocationId (Guid)
├── ExportType (Daily, Monthly, OnDemand, AuditRequest)
├── StartDate (Date)
├── EndDate (Date)
├── Status (Generating, Completed, Failed)
├── FileUrl (string)
├── FileSha256 (string)
├── TransactionCount (int)
├── GeneratedAt (DateTime)
├── RequestedByUserId (Guid)
└── AuditReference (string?) - for tax authority requests

FiscalJournal (Audit Log)
├── Id (Guid)
├── TenantId (Guid)
├── Timestamp (DateTime)
├── EventType (TransactionSigned, DeviceRegistered, ExportGenerated, Error)
├── DeviceId (Guid?)
├── TransactionId (Guid?)
├── Details (JSON)
├── IpAddress (string)
└── UserId (Guid?)

TaxRate
├── Id (Guid)
├── TenantId (Guid)
├── CountryCode (string)
├── Rate (decimal)
├── FiscalCode (string) - e.g., "A" for 19%, "B" for 7% in Germany
├── Description (string)
├── EffectiveFrom (Date)
├── EffectiveTo (Date?)
└── IsActive (bool)
```

### API Endpoints

```
Fiscal Devices (TSE)
├── GET    /api/fiscal-devices                      # List devices
├── POST   /api/fiscal-devices                      # Register new TSE
├── GET    /api/fiscal-devices/{id}                 # Get device info
├── POST   /api/fiscal-devices/{id}/initialize      # Initialize TSE
├── POST   /api/fiscal-devices/{id}/self-test       # Run self-test
├── GET    /api/fiscal-devices/{id}/status          # Health status
└── POST   /api/fiscal-devices/{id}/decommission    # Decommission device

Transactions
├── POST   /api/fiscal-transactions                 # Create & sign transaction
├── GET    /api/fiscal-transactions/{id}            # Get transaction
├── GET    /api/fiscal-transactions/{id}/qr         # Get QR code data
├── POST   /api/fiscal-transactions/{id}/void       # Void transaction
└── GET    /api/locations/{locId}/fiscal-transactions  # By location

Exports (DSFinV-K)
├── POST   /api/fiscal-exports                      # Generate export
├── GET    /api/fiscal-exports/{id}                 # Get export status
├── GET    /api/fiscal-exports/{id}/download        # Download export file
└── GET    /api/fiscal-exports                      # List exports

Tax Rates
├── GET    /api/tax-rates                           # List tax rates
├── POST   /api/tax-rates                           # Create tax rate
└── PUT    /api/tax-rates/{id}                      # Update tax rate

Journal (Audit)
├── GET    /api/fiscal-journal                      # Query audit log
└── GET    /api/fiscal-journal/export               # Export audit log
```

### TSE Integration Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Fiscalisation Service                         │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────┐ │
│  │  TSE Adapter    │    │  TSE Adapter    │    │ TSE Adapter │ │
│  │  (Swissbit)     │    │  (Fiskaly)      │    │ (Epson)     │ │
│  └────────┬────────┘    └────────┬────────┘    └──────┬──────┘ │
└───────────┼─────────────────────┼────────────────────┼──────────┘
            │                     │                    │
            ▼                     ▼                    ▼
    ┌───────────────┐    ┌───────────────────┐   ┌───────────────┐
    │ Swissbit      │    │ Fiskaly Cloud     │   │ Epson TSE     │
    │ Cloud/USB     │    │ (German TSE       │   │ Hardware      │
    │               │    │  as-a-Service)    │   │               │
    └───────────────┘    └───────────────────┘   └───────────────┘
```

### Receipt QR Code Format (KassenSichV)

```
V0;[TSE-Seriennummer];[Signaturzähler];[Start-Zeit];[Ende-Zeit];
[Prozess-Typ];[Prozess-Daten];[Brutto-Umsätze];[Signatur-Base64]
```

### DSFinV-K Export Structure

```
DSFinV-K Export (ZIP)
├── single_file/
│   ├── cashpointclosing.csv     # Z-Abschlüsse
│   ├── transactions.csv          # All transactions
│   ├── transactions_tse.csv      # TSE data per transaction
│   ├── payment.csv               # Payment details
│   ├── lines.csv                 # Line items
│   ├── lines_vat.csv             # VAT breakdown
│   └── cash_per_currency.csv     # Cash movements
├── index.xml                      # Index file
├── processing_protocols/          # Processing protocols
└── other_obligatory_data/         # Additional required data
```

### Kafka Events

```
TransactionSigned
├── TransactionId, DeviceId, Signature, SignatureCounter, Timestamp

TransactionSigningFailed
├── TransactionId, DeviceId, ErrorCode, ErrorMessage, WillRetry

FiscalDeviceHealthChanged
├── DeviceId, OldStatus, NewStatus, Reason

ExportGenerated
├── ExportId, LocationId, StartDate, EndDate, TransactionCount
```

---

## 3. Accounting Service

A core service that bridges the gap between operational data (tabs, orders, payments) and merchant accounting systems. Acts as the single source of truth for financial records.

### Service Overview

| Attribute | Value |
|-----------|-------|
| Service Name | Accounting.Api |
| Port | 5014 |
| Database | accounting_db |
| Dependencies | Orders, Payments, GiftCards, Fiscalisation, Inventory |

### Core Entities

```
AccountingPeriod
├── Id (Guid)
├── TenantId (Guid)
├── LocationId (Guid)
├── PeriodType (Daily, Weekly, Monthly)
├── StartDate (Date)
├── EndDate (Date)
├── Status (Open, Closing, Closed, Locked)
├── ClosedAt (DateTime?)
├── ClosedByUserId (Guid?)
└── Notes (string?)

JournalEntry
├── Id (Guid)
├── TenantId (Guid)
├── LocationId (Guid)
├── EntryNumber (string) - sequential
├── EntryDate (Date)
├── PostedAt (DateTime)
├── SourceType (Order, Payment, CashMovement, Adjustment, GiftCard)
├── SourceId (Guid)
├── Description (string)
├── TotalDebit (decimal)
├── TotalCredit (decimal)
├── Currency (string)
├── Status (Pending, Posted, Reversed)
├── ReversedByEntryId (Guid?)
├── FiscalTransactionId (Guid?)
└── Lines → JournalEntryLine[]

JournalEntryLine
├── Id (Guid)
├── JournalEntryId (Guid)
├── AccountCode (string)
├── AccountName (string)
├── DebitAmount (decimal)
├── CreditAmount (decimal)
├── TaxCode (string?)
├── TaxAmount (decimal?)
├── CostCenterId (Guid?)
└── Description (string?)

Account (Chart of Accounts)
├── Id (Guid)
├── TenantId (Guid)
├── AccountCode (string)
├── Name (string)
├── AccountType (Asset, Liability, Equity, Revenue, Expense)
├── SubType (string) - e.g., "Cash", "Receivables", "Sales"
├── ParentAccountId (Guid?)
├── IsSystemAccount (bool) - auto-created, cannot delete
├── IsActive (bool)
├── TaxCode (string?)
└── ExternalReference (string?) - for ERP mapping

CostCenter
├── Id (Guid)
├── TenantId (Guid)
├── Code (string)
├── Name (string)
├── LocationId (Guid?)
└── IsActive (bool)

Reconciliation
├── Id (Guid)
├── TenantId (Guid)
├── LocationId (Guid)
├── ReconciliationType (CashDrawer, BankDeposit, CardSettlement)
├── Date (Date)
├── ExpectedAmount (decimal)
├── ActualAmount (decimal)
├── Variance (decimal)
├── Status (Pending, Matched, Variance, Investigated, Resolved)
├── ResolvedAt (DateTime?)
├── ResolvedByUserId (Guid?)
├── ResolutionNotes (string?)
└── RelatedEntryIds (Guid[])

TaxLiability
├── Id (Guid)
├── TenantId (Guid)
├── Period (string) - e.g., "2026-Q1"
├── TaxCode (string)
├── TaxRate (decimal)
├── TaxableAmount (decimal)
├── TaxAmount (decimal)
├── Status (Calculated, Filed, Paid)
└── FiledAt (DateTime?)

GiftCardLiability
├── Id (Guid)
├── TenantId (Guid)
├── AsOfDate (Date)
├── TotalOutstandingCards (int)
├── TotalLiability (decimal)
├── BreakdownByProgram (JSON)
├── BreakdownByAge (JSON) - 0-30, 30-60, 60-90, 90+ days
└── CalculatedAt (DateTime)
```

### Automatic Journal Entry Generation

The service automatically creates journal entries from operational events:

```
Order Completed → Journal Entry
├── DR: Accounts Receivable (or Cash/Card)    $12.00
├── CR: Sales Revenue                         $10.09
└── CR: VAT Payable                           $ 1.91

Payment Received (Cash) → Journal Entry
├── DR: Cash in Drawer                        $12.00
└── CR: Accounts Receivable                   $12.00

Gift Card Issued → Journal Entry
├── DR: Cash/Card                             $50.00
└── CR: Gift Card Liability                   $50.00

Gift Card Redeemed → Journal Entry
├── DR: Gift Card Liability                   $25.00
└── CR: Sales Revenue                         $21.01
└── CR: VAT Payable                           $ 3.99

Inventory Received → Journal Entry
├── DR: Inventory Asset                       $500.00
└── CR: Accounts Payable                      $500.00

Cost of Goods Sold → Journal Entry
├── DR: COGS Expense                          $4.50
└── CR: Inventory Asset                       $4.50
```

### API Endpoints

```
Chart of Accounts
├── GET    /api/accounts                            # List accounts
├── POST   /api/accounts                            # Create account
├── GET    /api/accounts/{id}                       # Get account
├── PUT    /api/accounts/{id}                       # Update account
├── GET    /api/accounts/tree                       # Hierarchical view
└── POST   /api/accounts/import                     # Import from template

Journal Entries
├── GET    /api/journal-entries                     # List entries (filtered)
├── POST   /api/journal-entries                     # Manual entry
├── GET    /api/journal-entries/{id}                # Get entry with lines
├── POST   /api/journal-entries/{id}/reverse        # Reverse entry
└── GET    /api/journal-entries/by-source/{type}/{id}  # Find by source

Accounting Periods
├── GET    /api/accounting-periods                  # List periods
├── POST   /api/accounting-periods                  # Open new period
├── GET    /api/accounting-periods/{id}             # Get period
├── POST   /api/accounting-periods/{id}/close       # Close period
├── POST   /api/accounting-periods/{id}/lock        # Lock (prevent changes)
└── GET    /api/accounting-periods/current          # Get current period

Reconciliation
├── GET    /api/reconciliations                     # List reconciliations
├── POST   /api/reconciliations                     # Start reconciliation
├── PUT    /api/reconciliations/{id}                # Update amounts
├── POST   /api/reconciliations/{id}/resolve        # Resolve variance
└── GET    /api/reconciliations/pending             # Pending items

Financial Reports
├── GET    /api/reports/trial-balance               # Trial balance
├── GET    /api/reports/profit-loss                 # P&L statement
├── GET    /api/reports/balance-sheet               # Balance sheet
├── GET    /api/reports/vat-summary                 # VAT summary
├── GET    /api/reports/tax-liability               # Tax liability report
├── GET    /api/reports/gift-card-liability         # Gift card liability
└── GET    /api/reports/daily-summary               # Daily financial summary

Exports
├── POST   /api/exports/datev                       # DATEV export (Germany)
├── POST   /api/exports/csv                         # Generic CSV export
└── POST   /api/exports/sage                        # Sage export
└── POST   /api/exports/xero                        # Xero export
└── POST   /api/exports/quickbooks                  # QuickBooks export
```

### Integration with External Accounting Systems

```
┌─────────────────────────────────────────────────────────────────┐
│                    Accounting Service                            │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐             │
│  │ DATEV       │  │ Xero        │  │ QuickBooks  │  ...        │
│  │ Exporter    │  │ Sync        │  │ Sync        │             │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘             │
└─────────┼────────────────┼───────────────┼──────────────────────┘
          │                │               │
          ▼                ▼               ▼
   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
   │ DATEV       │  │ Xero Cloud  │  │ QuickBooks  │
   │ Unternehmen │  │ Accounting  │  │ Online      │
   │ Online      │  │             │  │             │
   └─────────────┘  └─────────────┘  └─────────────┘
```

### Kafka Events Consumed

```
OrderCompleted          → Generate sales journal entry
PaymentReceived         → Generate payment journal entry
GiftCardIssued          → Generate liability entry
GiftCardRedeemed        → Generate redemption entry
DeliveryReceived        → Generate inventory/payable entry
InventoryConsumed       → Generate COGS entry
CashDrawerClosed        → Generate cash reconciliation
TransactionSigned       → Link fiscal transaction to entry
```

### Kafka Events Published

```
JournalEntryPosted
├── EntryId, EntryNumber, TotalDebit, TotalCredit, SourceType, SourceId

AccountingPeriodClosed
├── PeriodId, LocationId, StartDate, EndDate, TotalRevenue, TotalExpenses

ReconciliationVarianceDetected
├── ReconciliationId, Type, ExpectedAmount, ActualAmount, Variance
```

---

## 4. Orders Gateway Service

A unified integration hub for receiving orders from third-party delivery platforms and online ordering systems.

### Service Overview

| Attribute | Value |
|-----------|-------|
| Service Name | OrdersGateway.Api |
| Port | 5015 |
| Database | ordersgateway_db |
| Dependencies | Orders, Menu, Location, Fiscalisation |

### Supported Platforms

| Platform | Region | Integration Type |
|----------|--------|------------------|
| Uber Eats | Global | REST API + Webhooks |
| DoorDash | US, Canada, Australia | REST API + Webhooks |
| Deliveroo | UK, EU, Middle East | REST API + Webhooks |
| Just Eat / Takeaway.com | UK, EU | REST API + Webhooks |
| Grubhub | US | REST API + Webhooks |
| Wolt | EU, Middle East | REST API + Webhooks |
| Glovo | EU, LATAM | REST API + Webhooks |
| Rappi | LATAM | REST API + Webhooks |
| Lieferando | Germany, Austria | REST API + Webhooks |
| Foodpanda | Asia, EU | REST API + Webhooks |
| Custom/White-label | Any | Generic webhook interface |

### Core Entities

```
DeliveryPlatform
├── Id (Guid)
├── TenantId (Guid)
├── PlatformType (UberEats, DoorDash, Deliveroo, JustEat, etc.)
├── Name (string) - display name
├── Status (Active, Paused, Disconnected)
├── ApiCredentialsEncrypted (string)
├── WebhookSecret (string)
├── MerchantId (string) - platform's ID for this merchant
├── Settings (JSON)
│   ├── AutoAcceptOrders (bool)
│   ├── DefaultPrepTime (int minutes)
│   ├── BusyModePrepTime (int minutes)
│   └── AutoSyncMenu (bool)
├── ConnectedAt (DateTime)
├── LastSyncAt (DateTime?)
└── LastOrderAt (DateTime?)

PlatformLocation (many-to-many: Platform ↔ Location)
├── Id (Guid)
├── DeliveryPlatformId (Guid)
├── LocationId (Guid)
├── PlatformStoreId (string) - platform's ID for this store
├── IsActive (bool)
├── MenuMappingId (Guid?) - which menu to sync
└── OperatingHoursOverride (JSON?)

ExternalOrder
├── Id (Guid)
├── TenantId (Guid)
├── LocationId (Guid)
├── DeliveryPlatformId (Guid)
├── PlatformOrderId (string) - ID from the platform
├── PlatformOrderNumber (string) - display number
├── InternalOrderId (Guid?) - linked DarkVelocity order
├── Status (Pending, Accepted, Preparing, Ready, PickedUp, Delivered, Cancelled, Failed)
├── OrderType (Delivery, Pickup)
├── PlacedAt (DateTime)
├── AcceptedAt (DateTime?)
├── EstimatedPickupAt (DateTime)
├── ActualPickupAt (DateTime?)
├── Customer (JSON)
│   ├── Name (string)
│   ├── Phone (string, masked)
│   └── DeliveryAddress (for delivery orders)
├── Items (JSON) - original platform items
├── Subtotal (decimal)
├── DeliveryFee (decimal)
├── ServiceFee (decimal)
├── Tax (decimal)
├── Tip (decimal)
├── Total (decimal)
├── Currency (string)
├── SpecialInstructions (string?)
├── PlatformRawPayload (string) - full webhook payload
├── ErrorMessage (string?)
├── RetryCount (int)
└── Metadata (JSON)

MenuSync
├── Id (Guid)
├── TenantId (Guid)
├── DeliveryPlatformId (Guid)
├── LocationId (Guid)
├── Status (Pending, InProgress, Completed, Failed)
├── ItemsTotal (int)
├── ItemsSynced (int)
├── ItemsFailed (int)
├── StartedAt (DateTime)
├── CompletedAt (DateTime?)
├── ErrorLog (JSON)
└── TriggeredBy (Manual, Scheduled, MenuChange)

MenuItemMapping
├── Id (Guid)
├── TenantId (Guid)
├── DeliveryPlatformId (Guid)
├── InternalMenuItemId (Guid)
├── PlatformItemId (string)
├── PlatformCategoryId (string)
├── PriceOverride (decimal?) - different price for platform
├── IsAvailable (bool)
├── ModifierMappings (JSON)
└── LastSyncedAt (DateTime)

PlatformPayout
├── Id (Guid)
├── TenantId (Guid)
├── DeliveryPlatformId (Guid)
├── LocationId (Guid)
├── PayoutReference (string)
├── PeriodStart (Date)
├── PeriodEnd (Date)
├── GrossAmount (decimal)
├── Commissions (decimal)
├── Fees (decimal)
├── Adjustments (decimal)
├── NetAmount (decimal)
├── Currency (string)
├── Status (Pending, Reconciled, Disputed)
├── ReceivedAt (DateTime)
└── OrderIds (Guid[])
```

### Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     Third-Party Delivery Platforms                       │
├─────────────────────────────────────────────────────────────────────────┤
│  Uber Eats  │  DoorDash  │  Deliveroo  │  Just Eat  │  Wolt  │  ...    │
└──────┬──────┴─────┬──────┴──────┬──────┴─────┬──────┴───┬────┴─────────┘
       │            │             │            │          │
       │  Webhooks (order events) │            │          │
       ▼            ▼             ▼            ▼          ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        Orders Gateway Service                            │
├─────────────────────────────────────────────────────────────────────────┤
│  ┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐        │
│  │ Uber Eats  │  │ DoorDash   │  │ Deliveroo  │  │ Just Eat   │  ...   │
│  │  Adapter   │  │  Adapter   │  │  Adapter   │  │  Adapter   │        │
│  └─────┬──────┘  └─────┬──────┘  └─────┬──────┘  └─────┬──────┘        │
│        └───────────────┴───────────────┴───────────────┘                │
│                                │                                         │
│                    ┌───────────▼───────────┐                            │
│                    │   Order Normalizer    │                            │
│                    │  (Common Order Model) │                            │
│                    └───────────┬───────────┘                            │
│                                │                                         │
│        ┌───────────────────────┼───────────────────────┐                │
│        ▼                       ▼                       ▼                │
│  ┌───────────┐          ┌───────────┐          ┌───────────┐           │
│  │ Auto-     │          │ Menu      │          │ Payout    │           │
│  │ Accept    │          │ Sync      │          │ Reconcile │           │
│  │ Engine    │          │ Engine    │          │ Engine    │           │
│  └─────┬─────┘          └─────┬─────┘          └───────────┘           │
└────────┼──────────────────────┼─────────────────────────────────────────┘
         │                      │
         ▼                      ▼
┌─────────────────┐    ┌─────────────────┐
│  Orders Service │    │   Menu Service  │
│ (Internal POS)  │    │                 │
└────────┬────────┘    └─────────────────┘
         │
         ▼
┌─────────────────┐    ┌─────────────────┐
│ Fiscalisation   │    │   Accounting    │
│    Service      │    │    Service      │
└─────────────────┘    └─────────────────┘
```

### API Endpoints

```
Platform Management
├── GET    /api/delivery-platforms                    # List connected platforms
├── POST   /api/delivery-platforms                    # Connect new platform
├── GET    /api/delivery-platforms/{id}               # Get platform details
├── PUT    /api/delivery-platforms/{id}               # Update settings
├── POST   /api/delivery-platforms/{id}/pause         # Pause receiving orders
├── POST   /api/delivery-platforms/{id}/resume        # Resume receiving orders
├── POST   /api/delivery-platforms/{id}/disconnect    # Disconnect platform
├── GET    /api/delivery-platforms/{id}/status        # Connection health
└── POST   /api/delivery-platforms/{id}/test          # Test connection

Platform-Location Mapping
├── GET    /api/delivery-platforms/{id}/locations     # List location mappings
├── POST   /api/delivery-platforms/{id}/locations     # Map location
├── PUT    /api/platform-locations/{id}               # Update mapping
└── DELETE /api/platform-locations/{id}               # Remove mapping

External Orders
├── GET    /api/external-orders                       # List orders (filtered)
├── GET    /api/external-orders/{id}                  # Get order details
├── POST   /api/external-orders/{id}/accept           # Accept order
├── POST   /api/external-orders/{id}/reject           # Reject order
├── POST   /api/external-orders/{id}/ready            # Mark ready for pickup
├── POST   /api/external-orders/{id}/cancel           # Cancel order
├── POST   /api/external-orders/{id}/adjust-time      # Update prep time
├── GET    /api/external-orders/{id}/tracking         # Get delivery tracking
└── GET    /api/locations/{locId}/external-orders     # Orders by location

Webhooks (Inbound from Platforms)
├── POST   /webhooks/ubereats                         # Uber Eats events
├── POST   /webhooks/doordash                         # DoorDash events
├── POST   /webhooks/deliveroo                        # Deliveroo events
├── POST   /webhooks/justeat                          # Just Eat events
├── POST   /webhooks/wolt                             # Wolt events
├── POST   /webhooks/generic                          # Generic/custom platforms
└── GET    /webhooks/{platform}/verify                # Webhook verification

Menu Sync
├── POST   /api/delivery-platforms/{id}/menu-sync     # Trigger menu sync
├── GET    /api/menu-syncs/{id}                       # Get sync status
├── GET    /api/delivery-platforms/{id}/menu-syncs    # Sync history
├── GET    /api/menu-mappings                         # List all mappings
├── PUT    /api/menu-mappings/{id}                    # Update item mapping
└── POST   /api/menu-mappings/bulk                    # Bulk update mappings

Payouts & Reconciliation
├── GET    /api/platform-payouts                      # List payouts
├── GET    /api/platform-payouts/{id}                 # Payout details
├── POST   /api/platform-payouts/{id}/reconcile       # Mark reconciled
└── GET    /api/platform-payouts/pending              # Pending reconciliation

Analytics
├── GET    /api/delivery-analytics/summary            # Platform performance
├── GET    /api/delivery-analytics/by-platform        # Breakdown by platform
├── GET    /api/delivery-analytics/revenue            # Revenue by platform
└── GET    /api/delivery-analytics/prep-times         # Prep time metrics
```

### Order Flow

```
1. Platform sends webhook
   └── POST /webhooks/ubereats { order_id: "UE-12345", items: [...] }

2. Adapter normalizes order
   └── Convert Uber Eats format → Common ExternalOrder model

3. Order stored & event published
   └── ExternalOrderReceived event to Kafka

4. Auto-accept (if enabled)
   ├── Check location is open
   ├── Check menu items available
   ├── Calculate prep time
   └── Call platform API to accept

5. Create internal order
   ├── Map external items → internal menu items
   ├── Create Order in Orders service
   └── Link ExternalOrder.InternalOrderId

6. Kitchen receives order
   ├── Order appears on KDS
   └── SignalR push to POS devices

7. Order marked ready
   ├── Staff marks ready in POS
   ├── Orders Gateway notifies platform
   └── Driver dispatched

8. Order picked up
   ├── Platform confirms pickup
   └── ExternalOrder status → PickedUp

9. Fiscalisation
   └── Transaction signed when order paid/completed
```

### Platform Adapter Interface

```csharp
public interface IDeliveryPlatformAdapter
{
    string PlatformType { get; }

    // Connection
    Task<ConnectionResult> ConnectAsync(PlatformCredentials credentials);
    Task<bool> TestConnectionAsync();
    Task DisconnectAsync();

    // Orders
    Task<ExternalOrder> ParseWebhookAsync(string payload, string signature);
    Task AcceptOrderAsync(string platformOrderId, int prepTimeMinutes);
    Task RejectOrderAsync(string platformOrderId, string reason);
    Task MarkReadyAsync(string platformOrderId);
    Task CancelOrderAsync(string platformOrderId, string reason);
    Task UpdatePrepTimeAsync(string platformOrderId, int newPrepTimeMinutes);

    // Menu
    Task<MenuSyncResult> SyncMenuAsync(IEnumerable<MenuItemDto> items);
    Task UpdateItemAvailabilityAsync(string platformItemId, bool available);
    Task UpdateItemPriceAsync(string platformItemId, decimal price);

    // Store
    Task SetStoreStatusAsync(bool isOpen);
    Task SetBusyModeAsync(bool isBusy, int? additionalPrepMinutes);
}
```

### Kafka Events

```
ExternalOrderReceived
├── ExternalOrderId, PlatformType, PlatformOrderId, LocationId, Total

ExternalOrderAccepted
├── ExternalOrderId, InternalOrderId, EstimatedPickupAt

ExternalOrderRejected
├── ExternalOrderId, Reason

ExternalOrderReady
├── ExternalOrderId, InternalOrderId, ReadyAt

ExternalOrderPickedUp
├── ExternalOrderId, PickedUpAt, DriverName

ExternalOrderCancelled
├── ExternalOrderId, CancelledBy (Platform/Merchant/Customer), Reason

MenuSyncCompleted
├── PlatformId, LocationId, ItemsSynced, ItemsFailed

PlatformConnectionChanged
├── PlatformId, OldStatus, NewStatus, Reason
```

### Error Handling & Resilience

**Webhook Processing:**
- Idempotent processing using PlatformOrderId
- Store raw payload for debugging
- Retry failed order creation with exponential backoff
- Dead-letter queue for permanently failed orders

**Platform API Calls:**
- Circuit breaker per platform
- Retry with exponential backoff
- Fallback to queue if platform unavailable
- Alert on repeated failures

**Menu Sync:**
- Incremental sync (only changed items)
- Rollback on partial failure
- Scheduled re-sync (nightly)

---

## 5. Customers & Loyalty Service

A comprehensive CRM and loyalty program service for customer relationship management, rewards, and retention.

### Service Overview

| Attribute | Value |
|-----------|-------|
| Service Name | Customers.Api |
| Port | 5016 |
| Database | customers_db |
| Dependencies | Orders, Payments, Auth, GiftCards |

### Core Entities

```
Customer
├── Id (Guid)
├── TenantId (Guid)
├── ExternalId (string?) - for POS integrations
├── Email (string, unique per tenant)
├── Phone (string?)
├── FirstName (string)
├── LastName (string)
├── DateOfBirth (Date?)
├── Gender (string?)
├── PreferredLanguage (string)
├── MarketingOptIn (bool)
├── SmsOptIn (bool)
├── Tags (string[]) - VIP, Regular, New, etc.
├── Notes (string?)
├── Source (POS, Online, Import, Reservation)
├── DefaultLocationId (Guid?) - home location
├── CreatedAt (DateTime)
├── LastVisitAt (DateTime?)
├── TotalVisits (int)
├── TotalSpend (decimal)
├── AverageOrderValue (decimal)
└── Metadata (JSON)

CustomerAddress
├── Id (Guid)
├── CustomerId (Guid)
├── Label (Home, Work, Other)
├── Street (string)
├── City (string)
├── PostalCode (string)
├── Country (string)
├── IsDefault (bool)
└── DeliveryInstructions (string?)

LoyaltyProgram
├── Id (Guid)
├── TenantId (Guid)
├── Name (string)
├── Type (Points, Stamps, Tiered, Cashback)
├── Status (Active, Paused, Ended)
├── PointsPerCurrencyUnit (decimal) - e.g., 1 point per €1
├── PointsValueInCurrency (decimal) - e.g., 100 points = €1
├── MinimumRedemption (int) - minimum points to redeem
├── PointsExpireAfterDays (int?)
├── WelcomeBonus (int?) - points on signup
├── BirthdayBonus (int?)
├── ReferralBonus (int?)
├── TermsAndConditions (string)
├── StartDate (Date)
├── EndDate (Date?)
└── Settings (JSON)

LoyaltyTier
├── Id (Guid)
├── ProgramId (Guid)
├── Name (string) - Bronze, Silver, Gold, Platinum
├── MinimumPoints (int) - threshold to reach tier
├── PointsMultiplier (decimal) - e.g., 1.5x for Gold
├── Benefits (JSON)
│   ├── FreeDelivery (bool)
│   ├── PriorityBooking (bool)
│   ├── ExclusiveOffers (bool)
│   └── BirthdayReward (string)
├── Color (string) - for UI display
├── IconUrl (string?)
└── SortOrder (int)

CustomerLoyalty
├── Id (Guid)
├── CustomerId (Guid)
├── ProgramId (Guid)
├── CurrentPoints (int)
├── LifetimePoints (int)
├── CurrentTierId (Guid?)
├── TierQualifyingPoints (int) - points counting toward next tier
├── TierExpiresAt (DateTime?)
├── EnrolledAt (DateTime)
└── LastActivityAt (DateTime)

PointsTransaction
├── Id (Guid)
├── CustomerLoyaltyId (Guid)
├── TransactionType (Earn, Redeem, Expire, Adjust, Bonus, Referral)
├── Points (int) - positive for earn, negative for redeem
├── BalanceBefore (int)
├── BalanceAfter (int)
├── OrderId (Guid?)
├── Description (string)
├── ExpiresAt (DateTime?)
├── ProcessedAt (DateTime)
├── ProcessedByUserId (Guid?)
└── Metadata (JSON)

Reward
├── Id (Guid)
├── TenantId (Guid)
├── ProgramId (Guid)
├── Name (string)
├── Description (string)
├── Type (Discount, FreeItem, Voucher, Experience)
├── PointsCost (int)
├── Value (decimal?) - monetary value if applicable
├── MenuItemId (Guid?) - for free item rewards
├── DiscountPercentage (decimal?)
├── MaxRedemptionsPerCustomer (int?)
├── TotalAvailable (int?)
├── TotalRedeemed (int)
├── ValidFrom (DateTime)
├── ValidUntil (DateTime?)
├── TermsAndConditions (string?)
├── ImageUrl (string?)
└── IsActive (bool)

CustomerReward
├── Id (Guid)
├── CustomerId (Guid)
├── RewardId (Guid)
├── Code (string) - unique redemption code
├── Status (Available, Redeemed, Expired, Cancelled)
├── IssuedAt (DateTime)
├── ExpiresAt (DateTime?)
├── RedeemedAt (DateTime?)
├── RedeemedOrderId (Guid?)
└── RedeemedLocationId (Guid?)

Referral
├── Id (Guid)
├── TenantId (Guid)
├── ReferrerCustomerId (Guid)
├── ReferredCustomerId (Guid?)
├── ReferralCode (string)
├── Status (Pending, Completed, Expired)
├── ReferrerBonus (int) - points awarded to referrer
├── RefereeBonus (int) - points awarded to new customer
├── CreatedAt (DateTime)
├── CompletedAt (DateTime?)
└── FirstOrderId (Guid?)
```

### API Endpoints

```
Customer Management
├── GET    /api/customers                             # List customers (search, filter)
├── POST   /api/customers                             # Create customer
├── GET    /api/customers/{id}                        # Get customer details
├── PUT    /api/customers/{id}                        # Update customer
├── DELETE /api/customers/{id}                        # Delete (soft) customer
├── GET    /api/customers/{id}/orders                 # Customer order history
├── GET    /api/customers/{id}/visits                 # Visit history by location
├── POST   /api/customers/lookup                      # Find by email/phone
├── POST   /api/customers/merge                       # Merge duplicate profiles
└── POST   /api/customers/import                      # Bulk import

Customer Addresses
├── GET    /api/customers/{id}/addresses              # List addresses
├── POST   /api/customers/{id}/addresses              # Add address
├── PUT    /api/customer-addresses/{id}               # Update address
└── DELETE /api/customer-addresses/{id}               # Remove address

Loyalty Programs
├── GET    /api/loyalty-programs                      # List programs
├── POST   /api/loyalty-programs                      # Create program
├── GET    /api/loyalty-programs/{id}                 # Get program details
├── PUT    /api/loyalty-programs/{id}                 # Update program
├── POST   /api/loyalty-programs/{id}/pause           # Pause program
├── POST   /api/loyalty-programs/{id}/resume          # Resume program
├── GET    /api/loyalty-programs/{id}/tiers           # List tiers
├── POST   /api/loyalty-programs/{id}/tiers           # Create tier
└── GET    /api/loyalty-programs/{id}/analytics       # Program analytics

Customer Loyalty
├── GET    /api/customers/{id}/loyalty                # Get loyalty status
├── POST   /api/customers/{id}/loyalty/enroll         # Enroll in program
├── GET    /api/customers/{id}/loyalty/points         # Points balance
├── GET    /api/customers/{id}/loyalty/transactions   # Points history
├── POST   /api/customers/{id}/loyalty/earn           # Earn points (manual)
├── POST   /api/customers/{id}/loyalty/redeem         # Redeem points
├── POST   /api/customers/{id}/loyalty/adjust         # Adjust points (admin)
└── GET    /api/customers/{id}/loyalty/rewards        # Available rewards

Rewards Catalog
├── GET    /api/rewards                               # List rewards
├── POST   /api/rewards                               # Create reward
├── GET    /api/rewards/{id}                          # Get reward
├── PUT    /api/rewards/{id}                          # Update reward
├── GET    /api/customers/{id}/rewards                # Customer's earned rewards
└── POST   /api/customer-rewards/{id}/redeem          # Redeem a reward

Referrals
├── GET    /api/customers/{id}/referral-code          # Get/generate referral code
├── POST   /api/referrals/validate                    # Validate referral code
├── GET    /api/customers/{id}/referrals              # List successful referrals
└── GET    /api/referral-analytics                    # Referral program stats

Segments & Tags
├── GET    /api/customer-segments                     # List segments
├── POST   /api/customer-segments                     # Create segment (rule-based)
├── GET    /api/customer-segments/{id}/customers      # Customers in segment
└── POST   /api/customers/{id}/tags                   # Add/remove tags
```

### Integration Points

**With Orders Service:**
- Attach customer to order
- Auto-earn points on order completion
- Apply customer discounts

**With Payments Service:**
- Redeem points as payment method
- Track spend for tier qualification

**With GiftCards Service:**
- Issue gift cards as rewards
- Link gift cards to customer profile

**With Booking Service:**
- Customer history for reservations
- VIP recognition and preferences

### Kafka Events

```
CustomerCreated
├── CustomerId, Email, Source, CreatedAt

CustomerUpdated
├── CustomerId, ChangedFields

PointsEarned
├── CustomerId, ProgramId, Points, OrderId, NewBalance

PointsRedeemed
├── CustomerId, ProgramId, Points, RewardId, NewBalance

TierChanged
├── CustomerId, ProgramId, OldTier, NewTier, Reason

RewardIssued
├── CustomerId, RewardId, Code, ExpiresAt

RewardRedeemed
├── CustomerId, RewardId, OrderId, LocationId

ReferralCompleted
├── ReferrerId, RefereeId, BonusPoints
```

---

## 6. Labor Management Service

A comprehensive workforce management service for scheduling, time tracking, tip distribution, and labor cost optimization.

### Service Overview

| Attribute | Value |
|-----------|-------|
| Service Name | Labor.Api |
| Port | 5017 |
| Database | labor_db |
| Dependencies | Auth, Location, Orders, Payments, Reporting |

### Core Entities

```
Employee
├── Id (Guid)
├── TenantId (Guid)
├── UserId (Guid) - link to Auth service user
├── EmployeeNumber (string)
├── FirstName (string)
├── LastName (string)
├── Email (string)
├── Phone (string)
├── DateOfBirth (Date?)
├── HireDate (Date)
├── TerminationDate (Date?)
├── EmploymentType (FullTime, PartTime, Casual, Contractor)
├── Status (Active, OnLeave, Terminated)
├── DefaultLocationId (Guid)
├── AllowedLocationIds (Guid[])
├── DefaultRoleId (Guid)
├── HourlyRate (decimal?)
├── SalaryAmount (decimal?)
├── PayFrequency (Weekly, Biweekly, Monthly)
├── OvertimeRate (decimal) - multiplier, e.g., 1.5
├── MaxHoursPerWeek (int?)
├── MinHoursPerWeek (int?)
├── TaxId (string?) - for payroll
├── BankDetails (JSON, encrypted)
└── EmergencyContact (JSON)

Role (Position)
├── Id (Guid)
├── TenantId (Guid)
├── Name (string) - Server, Bartender, Line Cook, Manager
├── Department (FOH, BOH, Management)
├── DefaultHourlyRate (decimal?)
├── Color (string) - for schedule display
├── SortOrder (int)
├── RequiredCertifications (string[])
└── IsActive (bool)

EmployeeRole (many-to-many with custom rates)
├── Id (Guid)
├── EmployeeId (Guid)
├── RoleId (Guid)
├── HourlyRateOverride (decimal?)
├── IsPrimary (bool)
└── CertifiedAt (DateTime?)

Schedule
├── Id (Guid)
├── TenantId (Guid)
├── LocationId (Guid)
├── WeekStartDate (Date) - always Monday
├── Status (Draft, Published, Locked)
├── PublishedAt (DateTime?)
├── PublishedByUserId (Guid?)
├── TotalScheduledHours (decimal)
├── TotalLaborCost (decimal)
├── Notes (string?)
└── Metadata (JSON)

Shift
├── Id (Guid)
├── ScheduleId (Guid)
├── EmployeeId (Guid)
├── RoleId (Guid)
├── Date (Date)
├── StartTime (TimeOnly)
├── EndTime (TimeOnly)
├── BreakMinutes (int)
├── ScheduledHours (decimal) - calculated
├── HourlyRate (decimal) - snapshot
├── LaborCost (decimal) - calculated
├── Status (Scheduled, Confirmed, Started, Completed, NoShow, Cancelled)
├── Notes (string?)
├── IsOvertime (bool)
├── SwapRequestId (Guid?) - if result of swap
└── Metadata (JSON)

TimeEntry (Clock in/out records)
├── Id (Guid)
├── TenantId (Guid)
├── EmployeeId (Guid)
├── LocationId (Guid)
├── ShiftId (Guid?) - linked scheduled shift
├── RoleId (Guid)
├── ClockInAt (DateTime)
├── ClockOutAt (DateTime?)
├── ClockInMethod (PIN, QR, Biometric, Manager)
├── ClockOutMethod (PIN, QR, Biometric, Manager, Auto)
├── BreakMinutes (int)
├── ActualHours (decimal) - calculated
├── RegularHours (decimal)
├── OvertimeHours (decimal)
├── HourlyRate (decimal)
├── OvertimeRate (decimal)
├── GrossPay (decimal) - calculated
├── Status (Active, Completed, Adjusted, Disputed)
├── AdjustedByUserId (Guid?)
├── AdjustmentReason (string?)
├── ApprovedByUserId (Guid?)
├── ApprovedAt (DateTime?)
└── Notes (string?)

Break
├── Id (Guid)
├── TimeEntryId (Guid)
├── StartAt (DateTime)
├── EndAt (DateTime?)
├── Type (Paid, Unpaid, Meal)
├── DurationMinutes (int) - calculated or manual
└── AutoDeducted (bool)

ShiftSwapRequest
├── Id (Guid)
├── TenantId (Guid)
├── RequestingEmployeeId (Guid)
├── RequestingShiftId (Guid)
├── TargetEmployeeId (Guid?)
├── TargetShiftId (Guid?) - for swap, null for drop
├── Type (Swap, Drop, Pickup)
├── Status (Pending, Approved, Rejected, Cancelled)
├── RequestedAt (DateTime)
├── RespondedAt (DateTime?)
├── ManagerApprovalRequired (bool)
├── ManagerApprovedByUserId (Guid?)
├── Reason (string?)
└── Notes (string?)

TipPool
├── Id (Guid)
├── TenantId (Guid)
├── LocationId (Guid)
├── Date (Date)
├── SalesPeriodId (Guid?)
├── TotalTips (decimal)
├── DistributionMethod (Equal, Hours, Points, Custom)
├── Status (Pending, Calculated, Distributed, Locked)
├── CalculatedAt (DateTime?)
├── DistributedAt (DateTime?)
├── DistributedByUserId (Guid?)
└── Notes (string?)

TipDistribution
├── Id (Guid)
├── TipPoolId (Guid)
├── EmployeeId (Guid)
├── RoleId (Guid)
├── HoursWorked (decimal)
├── PointsEarned (int?) - for point-based systems
├── TipShare (decimal)
├── TipPercentage (decimal) - of pool
├── DeclaredTips (decimal?) - if employee declares
├── Status (Calculated, Approved, Disputed, Paid)
└── PaidAt (DateTime?)

TipPoolRule
├── Id (Guid)
├── TenantId (Guid)
├── LocationId (Guid)
├── RoleId (Guid)
├── PoolSharePercentage (decimal) - % of role's tips to pool
├── DistributionWeight (decimal) - for weighted distribution
├── MinimumHoursToQualify (decimal?)
└── IsActive (bool)

PayrollPeriod
├── Id (Guid)
├── TenantId (Guid)
├── PeriodStart (Date)
├── PeriodEnd (Date)
├── Status (Open, Processing, Approved, Exported, Paid)
├── TotalRegularHours (decimal)
├── TotalOvertimeHours (decimal)
├── TotalGrossPay (decimal)
├── TotalTips (decimal)
├── ProcessedAt (DateTime?)
├── ApprovedByUserId (Guid?)
├── ExportedAt (DateTime?)
└── ExportFormat (string?) - ADP, Gusto, Paychex, etc.

PayrollEntry
├── Id (Guid)
├── PayrollPeriodId (Guid)
├── EmployeeId (Guid)
├── RegularHours (decimal)
├── OvertimeHours (decimal)
├── RegularPay (decimal)
├── OvertimePay (decimal)
├── TipIncome (decimal)
├── GrossPay (decimal)
├── Adjustments (decimal)
├── AdjustmentNotes (string?)
└── Status (Pending, Approved, Disputed)

Availability
├── Id (Guid)
├── EmployeeId (Guid)
├── DayOfWeek (int) - 0-6
├── StartTime (TimeOnly?)
├── EndTime (TimeOnly?)
├── IsAvailable (bool)
├── IsPreferred (bool)
├── EffectiveFrom (Date)
├── EffectiveTo (Date?)
└── Notes (string?)

TimeOffRequest
├── Id (Guid)
├── EmployeeId (Guid)
├── Type (Vacation, Sick, Personal, Bereavement, Other)
├── StartDate (Date)
├── EndDate (Date)
├── TotalDays (decimal)
├── IsPaid (bool)
├── Status (Pending, Approved, Rejected, Cancelled)
├── RequestedAt (DateTime)
├── ReviewedByUserId (Guid?)
├── ReviewedAt (DateTime?)
├── Reason (string?)
└── Notes (string?)
```

### API Endpoints

```
Employee Management
├── GET    /api/employees                              # List employees
├── POST   /api/employees                              # Create employee
├── GET    /api/employees/{id}                         # Get employee
├── PUT    /api/employees/{id}                         # Update employee
├── POST   /api/employees/{id}/terminate               # Terminate employee
├── GET    /api/employees/{id}/roles                   # Employee roles
├── POST   /api/employees/{id}/roles                   # Assign role
├── GET    /api/employees/{id}/availability            # Get availability
└── PUT    /api/employees/{id}/availability            # Update availability

Roles
├── GET    /api/roles                                  # List roles
├── POST   /api/roles                                  # Create role
├── PUT    /api/roles/{id}                             # Update role
└── GET    /api/roles/{id}/employees                   # Employees in role

Scheduling
├── GET    /api/schedules                              # List schedules
├── POST   /api/schedules                              # Create schedule (week)
├── GET    /api/schedules/{id}                         # Get schedule
├── POST   /api/schedules/{id}/publish                 # Publish schedule
├── POST   /api/schedules/{id}/copy                    # Copy to new week
├── GET    /api/schedules/{id}/shifts                  # List shifts
├── POST   /api/schedules/{id}/shifts                  # Create shift
├── PUT    /api/shifts/{id}                            # Update shift
├── DELETE /api/shifts/{id}                            # Delete shift
├── POST   /api/schedules/{id}/auto-generate           # Auto-generate shifts
├── GET    /api/schedules/{id}/labor-forecast          # Labor cost forecast
└── GET    /api/schedules/{id}/coverage                # Coverage analysis

Shift Swaps
├── POST   /api/shift-swap-requests                    # Request swap/drop
├── GET    /api/shift-swap-requests                    # List requests
├── POST   /api/shift-swap-requests/{id}/approve       # Approve request
├── POST   /api/shift-swap-requests/{id}/reject        # Reject request
└── GET    /api/employees/{id}/swap-requests           # Employee's requests

Time Tracking
├── POST   /api/time-entries/clock-in                  # Clock in
├── POST   /api/time-entries/clock-out                 # Clock out
├── GET    /api/time-entries                           # List entries (date range)
├── GET    /api/time-entries/{id}                      # Get entry
├── PUT    /api/time-entries/{id}                      # Adjust entry (manager)
├── POST   /api/time-entries/{id}/approve              # Approve entry
├── GET    /api/employees/{id}/time-entries            # Employee's entries
├── GET    /api/employees/{id}/current-shift           # Current clock status
├── POST   /api/time-entries/{id}/breaks               # Add break
└── PUT    /api/breaks/{id}                            # Update break

Tips
├── GET    /api/tip-pools                              # List tip pools
├── POST   /api/tip-pools                              # Create tip pool
├── GET    /api/tip-pools/{id}                         # Get pool details
├── POST   /api/tip-pools/{id}/calculate               # Calculate distribution
├── POST   /api/tip-pools/{id}/distribute              # Finalize distribution
├── GET    /api/tip-pools/{id}/distributions           # List distributions
├── PUT    /api/tip-distributions/{id}                 # Adjust distribution
├── GET    /api/tip-pool-rules                         # List rules
├── POST   /api/tip-pool-rules                         # Create rule
└── GET    /api/employees/{id}/tips                    # Employee tip history

Payroll
├── GET    /api/payroll-periods                        # List periods
├── POST   /api/payroll-periods                        # Create period
├── GET    /api/payroll-periods/{id}                   # Get period
├── POST   /api/payroll-periods/{id}/process           # Process payroll
├── POST   /api/payroll-periods/{id}/approve           # Approve payroll
├── POST   /api/payroll-periods/{id}/export            # Export to payroll provider
├── GET    /api/payroll-periods/{id}/entries           # List entries
├── PUT    /api/payroll-entries/{id}                   # Adjust entry
└── GET    /api/employees/{id}/payroll                 # Employee payroll history

Time Off
├── GET    /api/time-off-requests                      # List requests
├── POST   /api/time-off-requests                      # Submit request
├── GET    /api/time-off-requests/{id}                 # Get request
├── POST   /api/time-off-requests/{id}/approve         # Approve request
├── POST   /api/time-off-requests/{id}/reject          # Reject request
├── GET    /api/employees/{id}/time-off                # Employee's requests
└── GET    /api/employees/{id}/time-off-balance        # Remaining PTO

Labor Analytics
├── GET    /api/labor-analytics/costs                  # Labor cost report
├── GET    /api/labor-analytics/hours                  # Hours summary
├── GET    /api/labor-analytics/overtime               # Overtime report
├── GET    /api/labor-analytics/labor-vs-sales         # Labor % of sales
└── GET    /api/labor-analytics/schedule-adherence     # Actual vs scheduled
```

### Payroll Integration Exports

| Provider | Format | Region |
|----------|--------|--------|
| ADP | CSV/API | US, Global |
| Gusto | API | US |
| Paychex | CSV | US |
| DATEV LODAS | ASCII | Germany |
| Sage | CSV | UK, EU |
| Xero Payroll | API | AU, NZ, UK |
| Generic | CSV/JSON | Any |

### Kafka Events

```
EmployeeClockIn
├── EmployeeId, LocationId, ShiftId, ClockInAt, Method

EmployeeClockOut
├── EmployeeId, TimeEntryId, ClockOutAt, TotalHours

ShiftPublished
├── ScheduleId, EmployeeId, ShiftId, Date, StartTime, EndTime

ShiftSwapRequested
├── RequestId, RequestingEmployeeId, TargetEmployeeId, ShiftIds

ShiftSwapApproved
├── RequestId, ApprovedByUserId

TimeEntryAdjusted
├── TimeEntryId, AdjustedByUserId, OldHours, NewHours, Reason

TipsDistributed
├── TipPoolId, LocationId, Date, TotalAmount, EmployeeCount

PayrollProcessed
├── PayrollPeriodId, TotalEmployees, TotalGrossPay

OvertimeAlert
├── EmployeeId, CurrentHours, ThresholdHours, WeekOf
```

### Labor Cost Calculations

```
Regular Pay = Regular Hours × Hourly Rate
Overtime Pay = Overtime Hours × Hourly Rate × Overtime Multiplier
Gross Pay = Regular Pay + Overtime Pay + Tips
Labor Cost % = Total Labor Cost ÷ Total Sales × 100

Overtime Rules (configurable per location/jurisdiction):
- US: > 40 hours/week at 1.5x
- California: > 8 hours/day at 1.5x, > 12 hours/day at 2x
- EU: Varies by country
```

---

## 7. Multi-Tenancy Architecture

Transform the existing multi-location design into a true multi-tenant SaaS architecture with proper isolation.

### Tenant Hierarchy

```
Platform (DarkVelocity)
└── Tenant (Restaurant Group/Company)
    ├── Organization Settings
    ├── Subscription/Billing
    ├── Users (with tenant-wide roles)
    └── Locations
        ├── Location Settings
        ├── Local Users (with location roles)
        ├── Hardware
        └── Operational Data
```

### Isolation Strategy: Schema-per-Tenant

For regulatory compliance (especially fiscal data), use PostgreSQL schemas:

```
Database: darkvelocity_prod
├── Schema: public              # Shared platform data (tenants, subscriptions)
├── Schema: tenant_a1b2c3d4     # Tenant A's data
├── Schema: tenant_e5f6g7h8     # Tenant B's data
└── Schema: tenant_...          # More tenants
```

**Advantages:**
- Strong data isolation (required for fiscal compliance)
- Easy tenant backup/restore
- Tenant-specific migrations possible
- Clear audit trail per tenant

### Tenant Entity

```
Tenant
├── Id (Guid)
├── Code (string) - URL-safe identifier
├── Name (string)
├── LegalName (string)
├── TaxId (string)
├── Country (string, ISO 3166)
├── Currency (string, ISO 4217)
├── Timezone (string)
├── Status (Trial, Active, Suspended, Cancelled)
├── SubscriptionTier (Starter, Professional, Enterprise)
├── Features (JSON) - enabled features
├── Settings (JSON) - tenant-wide config
├── SchemaName (string) - PostgreSQL schema
├── CreatedAt (DateTime)
├── TrialEndsAt (DateTime?)
└── BillingEmail (string)

TenantSubscription
├── Id (Guid)
├── TenantId (Guid)
├── PlanId (string)
├── Status (Active, PastDue, Cancelled)
├── CurrentPeriodStart (DateTime)
├── CurrentPeriodEnd (DateTime)
├── Locations (int) - included locations
├── Users (int) - included users
├── StripeSubscriptionId (string?)
└── StripeCustomerId (string?)
```

### Request Context & Routing

Every request includes tenant context derived from:

1. **Subdomain**: `tenant-code.darkvelocity.io`
2. **Header**: `X-Tenant-ID` (for API clients)
3. **JWT Claim**: `tenant_id` (from auth token)

```csharp
public class TenantContext
{
    public Guid TenantId { get; init; }
    public string TenantCode { get; init; }
    public string SchemaName { get; init; }
    public Guid? LocationId { get; init; }  // If location-scoped request
    public TenantFeatures Features { get; init; }
}

// Middleware sets this per-request
public class TenantMiddleware
{
    public async Task InvokeAsync(HttpContext context, ITenantResolver resolver)
    {
        var tenant = await resolver.ResolveAsync(context);
        context.Items["TenantContext"] = tenant;

        // Set EF Core schema for this request
        var dbContext = context.RequestServices.GetRequiredService<AppDbContext>();
        dbContext.SetSchema(tenant.SchemaName);

        await _next(context);
    }
}
```

### Database Context Configuration

```csharp
public class TenantDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;
    private string _schema = "public";

    public void SetSchema(string schema)
    {
        _schema = schema;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // All entities go to tenant schema
        modelBuilder.HasDefaultSchema(_schema);

        // Apply global query filters for tenant isolation
        modelBuilder.Entity<Location>()
            .HasQueryFilter(l => l.TenantId == _tenantContext.TenantId);
        // ... repeat for all tenant-scoped entities
    }
}
```

### Cross-Tenant Operations (Admin Only)

Platform-level operations for system administrators:

```
Platform Admin API (Internal)
├── GET    /admin/tenants                    # List all tenants
├── POST   /admin/tenants                    # Create tenant (provisions schema)
├── GET    /admin/tenants/{id}               # Get tenant details
├── POST   /admin/tenants/{id}/suspend       # Suspend tenant
├── POST   /admin/tenants/{id}/provision     # Re-provision schema
├── DELETE /admin/tenants/{id}               # Delete tenant (archive data first)
├── GET    /admin/tenants/{id}/metrics       # Usage metrics
└── POST   /admin/tenants/{id}/migrate       # Run pending migrations
```

### Tenant Provisioning Workflow

```
1. Tenant Signs Up
   └── Create Tenant record (public schema)

2. Schema Provisioning
   ├── CREATE SCHEMA tenant_{code}
   ├── Run all EF migrations against new schema
   └── Seed default data (accounts, tax rates, etc.)

3. Initial Setup
   ├── Create admin user
   ├── Create first location
   └── Configure fiscal device (if required)

4. Ready for Use
   └── Tenant accesses via subdomain
```

### Multi-Tenant Event Bus

Kafka topics include tenant context:

```
Topic: orders.completed
Message:
{
  "eventId": "...",
  "tenantId": "abc123",
  "locationId": "...",
  "orderId": "...",
  "completedAt": "..."
}
```

Consumers filter by tenant when needed:

```csharp
[Consumer("orders.completed")]
public async Task HandleOrderCompleted(OrderCompletedEvent evt)
{
    using var scope = _scopeFactory.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();
    db.SetSchema($"tenant_{evt.TenantCode}");

    // Process event with correct tenant context
    await GenerateJournalEntry(db, evt);
}
```

---

## 8. Kubernetes-Native Deployment (No API Gateway)

Remove the API Gateway in favor of Kubernetes-native service mesh and ingress.

### Why Remove API Gateway?

1. **Kubernetes provides routing** - Ingress controllers handle path-based routing
2. **Service mesh for cross-cutting** - Istio/Linkerd handle auth, rate limiting, observability
3. **Simpler architecture** - One less component to maintain
4. **Direct service access** - Lower latency, easier debugging

### Architecture Without Gateway

```
┌─────────────────────────────────────────────────────────────────┐
│                         Internet                                 │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Kubernetes Ingress                            │
│                (NGINX Ingress Controller)                        │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ Rules:                                                       ││
│  │  /api/auth/*       → auth-service:5000                      ││
│  │  /api/menu/*       → menu-service:5002                      ││
│  │  /api/orders/*     → orders-service:5003                    ││
│  │  /api/payments/*   → payments-service:5004                  ││
│  │  /api/giftcards/*  → giftcards-service:5012                 ││
│  │  /api/fiscal/*     → fiscalisation-service:5013             ││
│  │  /api/accounting/* → accounting-service:5014                ││
│  │  /*                → frontend-service                        ││
│  └─────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│               Kubernetes Service Mesh (Istio)                    │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ Sidecar Proxies (Envoy) for each pod:                     │  │
│  │  • mTLS between services                                  │  │
│  │  • JWT validation (tenant context)                        │  │
│  │  • Rate limiting per tenant                               │  │
│  │  • Circuit breaking                                       │  │
│  │  • Distributed tracing                                    │  │
│  │  • Metrics collection                                     │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│  auth-service   │  │  menu-service   │  │ orders-service  │
│     (Pod)       │  │     (Pod)       │  │     (Pod)       │
│  ┌───────────┐  │  │  ┌───────────┐  │  │  ┌───────────┐  │
│  │  Envoy    │  │  │  │  Envoy    │  │  │  │  Envoy    │  │
│  │  Sidecar  │  │  │  │  Sidecar  │  │  │  │  Sidecar  │  │
│  └───────────┘  │  │  └───────────┘  │  │  └───────────┘  │
│  ┌───────────┐  │  │  ┌───────────┐  │  │  ┌───────────┐  │
│  │  .NET App │  │  │  │  .NET App │  │  │  │  .NET App │  │
│  └───────────┘  │  │  └───────────┘  │  │  └───────────┘  │
└─────────────────┘  └─────────────────┘  └─────────────────┘
```

### Kubernetes Manifests Structure

```
k8s/
├── base/
│   ├── namespace.yaml
│   ├── configmap.yaml              # Shared config
│   ├── secrets.yaml                # (reference to external secrets)
│   └── services/
│       ├── auth/
│       │   ├── deployment.yaml
│       │   ├── service.yaml
│       │   └── hpa.yaml            # Horizontal Pod Autoscaler
│       ├── menu/
│       ├── orders/
│       ├── payments/
│       ├── giftcards/
│       ├── fiscalisation/
│       ├── accounting/
│       └── ...
├── overlays/
│   ├── development/
│   │   ├── kustomization.yaml
│   │   └── patches/
│   ├── staging/
│   │   ├── kustomization.yaml
│   │   └── patches/
│   └── production/
│       ├── kustomization.yaml
│       ├── patches/
│       └── ingress.yaml
└── istio/
    ├── gateway.yaml
    ├── virtual-services.yaml
    ├── destination-rules.yaml
    ├── authorization-policies.yaml
    └── peer-authentication.yaml
```

### Sample Service Deployment

```yaml
# k8s/base/services/accounting/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: accounting-service
  labels:
    app: accounting
    version: v1
spec:
  replicas: 2
  selector:
    matchLabels:
      app: accounting
  template:
    metadata:
      labels:
        app: accounting
        version: v1
      annotations:
        sidecar.istio.io/inject: "true"
    spec:
      containers:
      - name: accounting
        image: darkvelocity/accounting-service:latest
        ports:
        - containerPort: 5014
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: db-credentials
              key: connection-string
        resources:
          requests:
            memory: "256Mi"
            cpu: "100m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 5014
          initialDelaySeconds: 10
          periodSeconds: 5
        livenessProbe:
          httpGet:
            path: /health/live
            port: 5014
          initialDelaySeconds: 15
          periodSeconds: 10
```

### Ingress Configuration

```yaml
# k8s/overlays/production/ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: darkvelocity-ingress
  annotations:
    kubernetes.io/ingress.class: nginx
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/proxy-body-size: "10m"
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  tls:
  - hosts:
    - "*.darkvelocity.io"
    secretName: darkvelocity-tls
  rules:
  - host: "*.darkvelocity.io"
    http:
      paths:
      - path: /api/auth
        pathType: Prefix
        backend:
          service:
            name: auth-service
            port:
              number: 5000
      - path: /api/locations
        pathType: Prefix
        backend:
          service:
            name: location-service
            port:
              number: 5001
      - path: /api/menu
        pathType: Prefix
        backend:
          service:
            name: menu-service
            port:
              number: 5002
      - path: /api/orders
        pathType: Prefix
        backend:
          service:
            name: orders-service
            port:
              number: 5003
      - path: /api/payments
        pathType: Prefix
        backend:
          service:
            name: payments-service
            port:
              number: 5004
      - path: /api/hardware
        pathType: Prefix
        backend:
          service:
            name: hardware-service
            port:
              number: 5005
      - path: /api/inventory
        pathType: Prefix
        backend:
          service:
            name: inventory-service
            port:
              number: 5006
      - path: /api/procurement
        pathType: Prefix
        backend:
          service:
            name: procurement-service
            port:
              number: 5007
      - path: /api/costing
        pathType: Prefix
        backend:
          service:
            name: costing-service
            port:
              number: 5008
      - path: /api/reports
        pathType: Prefix
        backend:
          service:
            name: reporting-service
            port:
              number: 5009
      - path: /api/bookings
        pathType: Prefix
        backend:
          service:
            name: booking-service
            port:
              number: 5010
      - path: /api/payment-gateway
        pathType: Prefix
        backend:
          service:
            name: payment-gateway-service
            port:
              number: 5011
      - path: /api/giftcards
        pathType: Prefix
        backend:
          service:
            name: giftcards-service
            port:
              number: 5012
      - path: /api/fiscal
        pathType: Prefix
        backend:
          service:
            name: fiscalisation-service
            port:
              number: 5013
      - path: /api/accounting
        pathType: Prefix
        backend:
          service:
            name: accounting-service
            port:
              number: 5014
      - path: /
        pathType: Prefix
        backend:
          service:
            name: frontend-service
            port:
              number: 80
```

### Istio Authorization (JWT Validation)

```yaml
# k8s/istio/authorization-policies.yaml
apiVersion: security.istio.io/v1beta1
kind: AuthorizationPolicy
metadata:
  name: require-jwt
  namespace: darkvelocity
spec:
  selector:
    matchLabels:
      app.kubernetes.io/part-of: darkvelocity
  action: ALLOW
  rules:
  - from:
    - source:
        requestPrincipals: ["*"]
    when:
    - key: request.auth.claims[tenant_id]
      values: ["*"]
---
apiVersion: security.istio.io/v1beta1
kind: RequestAuthentication
metadata:
  name: jwt-auth
  namespace: darkvelocity
spec:
  selector:
    matchLabels:
      app.kubernetes.io/part-of: darkvelocity
  jwtRules:
  - issuer: "https://auth.darkvelocity.io"
    jwksUri: "https://auth.darkvelocity.io/.well-known/jwks.json"
    forwardOriginalToken: true
```

### Rate Limiting per Tenant

```yaml
# k8s/istio/envoy-filter-ratelimit.yaml
apiVersion: networking.istio.io/v1alpha3
kind: EnvoyFilter
metadata:
  name: tenant-rate-limit
  namespace: darkvelocity
spec:
  workloadSelector:
    labels:
      app.kubernetes.io/part-of: darkvelocity
  configPatches:
  - applyTo: HTTP_FILTER
    match:
      context: SIDECAR_INBOUND
    patch:
      operation: INSERT_BEFORE
      value:
        name: envoy.filters.http.local_ratelimit
        typed_config:
          "@type": type.googleapis.com/udpa.type.v1.TypedStruct
          type_url: type.googleapis.com/envoy.extensions.filters.http.local_ratelimit.v3.LocalRateLimit
          value:
            stat_prefix: http_local_rate_limiter
            token_bucket:
              max_tokens: 1000
              tokens_per_fill: 100
              fill_interval: 1s
            filter_enabled:
              runtime_key: local_rate_limit_enabled
              default_value:
                numerator: 100
                denominator: HUNDRED
```

### Removing API Gateway Project

1. Delete `src/Gateway/` directory
2. Remove from solution file
3. Update README to remove gateway references
4. Update docker-compose to remove gateway service
5. Update service-to-service calls to use Kubernetes DNS:
   - Old: `http://gateway:8000/api/menu/...`
   - New: `http://menu-service.darkvelocity.svc.cluster.local:5002/api/...`

---

## 9. Implementation Phases

### Phase 1: Foundation (Weeks 1-2)

**Multi-Tenancy Core**
- [ ] Design Tenant entity and provisioning
- [ ] Implement schema-per-tenant in EF Core
- [ ] Create TenantMiddleware and context resolution
- [ ] Add tenant filters to all existing entities
- [ ] Update all services with tenant awareness

**Kubernetes Setup**
- [ ] Create base Kubernetes manifests
- [ ] Set up Ingress controller
- [ ] Configure Istio service mesh
- [ ] Implement health check endpoints
- [ ] Create Helm charts or Kustomize overlays
- [ ] Remove API Gateway project

### Phase 2: Gift Cards (Weeks 3-4)

- [ ] Create GiftCards.Api service
- [ ] Implement card lifecycle (issue, activate, redeem)
- [ ] Add gift card programs and designs
- [ ] Integrate with Payments service
- [ ] Add gift card as payment method in Orders
- [ ] Implement balance and liability reporting
- [ ] Write integration tests

### Phase 3: Fiscalisation (Weeks 5-7)

- [ ] Create Fiscalisation.Api service
- [ ] Implement TSE adapter interface
- [ ] Integrate Fiskaly Cloud TSE (primary)
- [ ] Implement transaction signing flow
- [ ] Add QR code generation for receipts
- [ ] Implement DSFinV-K export
- [ ] Create fiscal journal and audit log
- [ ] Write integration tests with mock TSE

### Phase 4: Accounting (Weeks 8-10)

- [ ] Create Accounting.Api service
- [ ] Implement Chart of Accounts
- [ ] Create journal entry automation
- [ ] Add accounting period management
- [ ] Implement reconciliation workflows
- [ ] Create financial reports (P&L, Balance Sheet)
- [ ] Add DATEV export (Germany)
- [ ] Write integration tests

### Phase 5: Orders Gateway (Weeks 11-13)

- [ ] Create OrdersGateway.Api service
- [ ] Implement platform adapter interface
- [ ] Build Uber Eats adapter
- [ ] Build DoorDash adapter
- [ ] Build Deliveroo adapter
- [ ] Build Just Eat adapter
- [ ] Implement webhook receivers with signature verification
- [ ] Create order normalization pipeline
- [ ] Add auto-accept engine with availability checks
- [ ] Implement menu sync to platforms
- [ ] Add payout reconciliation
- [ ] Write integration tests with mock platforms

### Phase 6: Integration & Polish (Weeks 14-16)

- [ ] End-to-end testing across all services
- [ ] Performance testing and optimization
- [ ] Security audit and penetration testing
- [ ] Documentation update
- [ ] Production deployment procedures
- [ ] Monitoring and alerting setup

---

## Service Port Summary (Updated)

| Service | Port | Description |
|---------|------|-------------|
| Auth | 5000 | Authentication, users, permissions |
| Location | 5001 | Multi-location management |
| Menu | 5002 | Items, categories, menus |
| Orders | 5003 | Order lifecycle, sales periods |
| Payments | 5004 | Payment processing |
| Hardware | 5005 | POS devices, printers, drawers |
| Inventory | 5006 | Stock tracking, FIFO |
| Procurement | 5007 | Suppliers, POs, deliveries |
| Costing | 5008 | Recipes, cost calculations |
| Reporting | 5009 | Sales and margin reports |
| Booking | 5010 | Table reservations |
| PaymentGateway | 5011 | External payment processors |
| **GiftCards** | **5012** | **Gift card management** |
| **Fiscalisation** | **5013** | **TSE/KassenSichV compliance** |
| **Accounting** | **5014** | **Financial records, journals** |
| **OrdersGateway** | **5015** | **Uber Eats, DoorDash, Deliveroo, etc.** |
| **Customers** | **5016** | **CRM, loyalty programs, rewards** |
| **Labor** | **5017** | **Scheduling, timesheets, tip distribution** |

---

## Dependencies Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     Third-Party Delivery Platforms                       │
│            (Uber Eats, DoorDash, Deliveroo, Just Eat, etc.)             │
└────────────────────────────────┬────────────────────────────────────────┘
                                 │ Webhooks
                                 ▼
                        ┌─────────────────┐
                        │ Orders Gateway  │
                        │    Service      │
                        └────────┬────────┘
                                 │
                           ┌──────────────┐
                           │    Auth      │
                           │   Service    │
                           └──────┬───────┘
                                  │ (JWT tokens)
                                  ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                          All Services                                     │
└──────────────────────────────────────────────────────────────────────────┘
                                  │
          ┌───────────────────────┼───────────────────────┐
          ▼                       ▼                       ▼
┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐
│    Orders       │      │   GiftCards     │      │   Inventory     │
│    Service      │      │   Service       │      │   Service       │
└────────┬────────┘      └────────┬────────┘      └────────┬────────┘
         │                        │                        │
         │   ┌────────────────────┼────────────────────────┘
         │   │                    │
         ▼   ▼                    ▼
┌─────────────────┐      ┌─────────────────┐
│   Payments      │      │ Fiscalisation   │
│   Service       │──────│   Service       │
└────────┬────────┘      └────────┬────────┘
         │                        │
         │                        │
         ▼                        ▼
┌─────────────────────────────────────────┐
│           Accounting Service             │
│  (Central financial record keeping)      │
└─────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────┐
│           External Systems               │
│  (DATEV, Xero, QuickBooks, etc.)         │
└─────────────────────────────────────────┘
```

---

*Document Version: 1.0*
*Last Updated: January 2026*
*Author: DarkVelocity Engineering*
