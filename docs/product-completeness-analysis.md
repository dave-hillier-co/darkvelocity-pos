# DarkVelocity POS - Product Completeness Analysis

> **Analysis Date:** February 2026 (Updated after rebase)
> **Analyzed By:** Claude Code Domain Analysis Agents
> **Purpose:** Identify gaps and improvements needed for production readiness

## Executive Summary

This document provides a comprehensive analysis of all 17 grain domains in the DarkVelocity POS system, evaluating product completeness and identifying specific actionable improvements needed for a production-ready hospitality platform.

### Recent Improvements (Since Last Analysis)

- **Bundle/Combo Support** - Full meal deal and bundle support added to Menu and Orders
- **Order Holds & Fires** - Complete kitchen workflow for course-based firing
- **Fiskaly Integration** - Configuration-driven integration for Germany, Austria, Italy
- **Comprehensive Test Coverage** - ~18,600 lines of tests added across all domains
- **Internal TSE** - Software-based TSE with HMAC-SHA256 signatures

### Overall Assessment

| Category | Count | Status |
|----------|-------|--------|
| Domains Analyzed | 17 | Complete |
| Production Ready | 2 | Menu (82%), Orders (78%) |
| Near Complete (70%+) | 4 | Payments (75%), Organization (70%), Customers (65%) |
| Partial (50-70%) | 5 | Inventory (60%), Tables (60%), Reporting (60%), Recipes (55%), Fiscal (50%) |
| Significant Gaps (<50%) | 6 | Devices (45%), Finance (40%), External Channels (40%), System (35%), Payment Processors (30%), Costing (50%) |

### Critical Blockers (P0)

1. **Devices Domain** - No offline mode or printing system
2. **External Channels Domain** - No webhook handlers or HTTP clients for delivery platforms
3. **Payment Processors Domain** - No actual Stripe/Adyen implementation
4. **System Domain** - Notifications don't send, webhooks don't deliver
5. **Costing Domain** - No REST API endpoints

---

## Domain Analysis Details

### 1. Orders Domain

**Completeness: 78%** *(improved from 70%)*

#### Current Implementation
- Full order lifecycle with event sourcing
- Order types: DineIn, TakeOut, Delivery, DriveThru, Online, Tab
- Per-item tax rates for multiple jurisdictions
- Order-level discounts with approval tracking
- Bill splitting by items, people, and amounts
- Table and server transfers
- Kitchen ticket integration via Orleans streams
- **NEW:** Bundle/combo support with flexible slot selection rules
- **NEW:** Hold/Fire workflow for course-based kitchen management
- **NEW:** Course numbering and course firing

#### Remaining Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Seat Assignment | High | 3-5 days | Cannot assign items to seats for seat-based splitting |
| Line-Level Discounts | High | 1 week | Only order-level discounts exist |
| Price Overrides | High | 3-5 days | PriceOverride exists but not exposed in AddLineCommand |
| Order Merging | Medium | 3-5 days | Can split but cannot merge orders (combining tables) |
| Course Timing Coordination | Medium | 1 week | Courses fire but no delay/timing between courses |
| Kitchen Ticket Void Sync | Medium | 3-5 days | Voiding lines doesn't void kitchen ticket items |
| Seat-Based Payment Split | Medium | 1 week | Cannot split payment by seat |
| Order History API | Low | 3-5 days | No timeline view endpoint |

#### Recommended Actions

```
Phase 1 (Essential - 2 weeks):
├── Add Seat property to OrderLine
├── Implement line-level discount methods
├── Expose PriceOverride in AddLineCommand
└── Sync kitchen ticket voids

Phase 2 (Enhanced - 2 weeks):
├── Implement MergeFromOrderAsync()
├── Add course timing coordination
├── Implement seat-based payment splitting
└── Create order history/timeline API
```

**Total Effort to 90%:** 4-6 weeks

#### Key Files
- `/src/DarkVelocity.Host/Domains/Orders/OrderGrain.cs`
- `/src/DarkVelocity.Host/Domains/Orders/OrderState.cs`
- `/src/DarkVelocity.Host/Domains/Orders/KitchenGrain.cs`

---

### 2. Payments Domain

**Completeness: 75%**

#### Current Implementation
- Full payment lifecycle with event sourcing
- Split and partial payments
- Card authorization flow
- Cash drawer management with variance tracking
- Gift card lifecycle (activation, redemption, reload)
- Refund support (full and partial)
- Payment batch tracking structure

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Settlement Batch | Critical | 40-50h | Interface exists but no implementation |
| House Accounts | High | 30-35h | Customer AR for tabs and corporate accounts |
| Loyalty Integration | High | 25-30h | PaymentMethod.LoyaltyPoints exists but no processing |
| Offline Queue | Critical | 35-40h | No store-and-forward for network failures |
| Chargeback Handling | Medium | 30-35h | No dispute workflow |
| Payment Reconciliation | Medium | 15-20h | No settlement vs system records comparison |
| Surcharges | Low | 10-15h | No card processing fee handling |

#### Recommended Actions

```
Phase 1 (Critical):
├── Implement ISettlementBatchGrain
├── Create OfflinePaymentQueueGrain
├── Add payment failure retry logic
└── Implement chargeback workflow

Phase 2 (High Value):
├── Create CustomerAccountGrain for house accounts
├── Integrate LoyaltyProgramGrain with payments
├── Add surcharge calculation
└── Implement payment reconciliation reports
```

**Total Effort to 90%:** 4-5 weeks

#### Key Files
- `/src/DarkVelocity.Host/Domains/Payments/PaymentGrain.cs`
- `/src/DarkVelocity.Host/Domains/Payments/GiftCardGrain.cs`
- `/src/DarkVelocity.Host/Domains/Shared/IBatchGrains.cs`

---

### 3. Payment Processors Domain

**Completeness: 30%**

#### Current Implementation
- Merchant and Terminal grain interfaces
- MockProcessorGrain for testing (comprehensive)
- Payment Intent flow (Stripe-compatible)
- Webhook endpoint structure
- Basic auth/capture/refund/void flow
- 3DS handling via NextAction pattern

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Stripe Implementation | Critical | 14-21 days | Interface only, no actual SDK integration |
| Adyen Implementation | Critical | 16-21 days | Interface only, no actual SDK integration |
| Offline Payments | Critical | 10-14 days | No store-forward queue |
| Retry Logic & Idempotency | Critical | 5-7 days | No idempotency keys, no exponential backoff |
| Terminal Pairing | High | 6-8 days | Basic registration only |
| PCI Compliance | High | 8-10 days | Card data stored in state, no tokenization |
| Settlement Reporting | High | 10-12 days | No batch management |

#### Recommended Actions

```
Phase 1 (Critical - 6 weeks):
├── Implement StripeProcessorGrain with SDK
├── Implement AdyenProcessorGrain with SDK
├── Create OfflinePaymentQueueGrain
├── Add idempotency key management
└── Implement retry with exponential backoff

Phase 2 (Production - 4 weeks):
├── Add terminal pairing workflow
├── Implement settlement batch grain
├── Create DisputeGrain for chargebacks
├── Add processor routing/failover
└── Implement PCI-compliant tokenization
```

**Total Effort to 90%:** 10-14 weeks

#### Key Files to Create
- `/src/DarkVelocity.Host/Domains/Payments/StripeProcessorGrain.cs`
- `/src/DarkVelocity.Host/Domains/Payments/AdyenProcessorGrain.cs`
- `/src/DarkVelocity.Host/Domains/Payments/OfflinePaymentQueueGrain.cs`
- `/src/DarkVelocity.Host/Domains/Payments/DisputeGrain.cs`

---

### 4. Customers Domain

**Completeness: 65%**

#### Current Implementation
- Customer profile management with event sourcing
- Loyalty enrollment and points with tier-based multipliers
- Rewards issuance and redemption
- Visit history tracking (last 50)
- Preferences and dietary restrictions
- Referral code generation
- GDPR compliance (anonymize/delete)
- Points expiration configuration

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Points Expiration Enforcement | P0 | 14 pts | Config exists but never enforced |
| Segmentation Automation | P0 | 16 pts | Set to "New" on creation, never updated |
| GDPR Consent Granularity | P0 | 12 pts | Only boolean opt-ins, no timestamps |
| Birthday Rewards | P1 | 11 pts | Defined but never triggered |
| Referral Completion | P1 | 16 pts | Codes generated but no qualification tracking |
| Customer Journey Automation | P1 | 16 pts | No lifecycle stage management |
| VIP Detection | P2 | 12 pts | No automated high-value customer flagging |
| Redemption Limits | P2 | 11 pts | Config exists but not checked |

#### Recommended Actions

```
Phase 1 (Compliance - 2 sprints):
├── Implement GDPR consent granularity
├── Complete data privacy/anonymization
└── Create automated points expiration job

Phase 2 (Core Marketing - 2 sprints):
├── Implement segmentation automation (RFM)
├── Create birthday reward background job
└── Add customer journey automation

Phase 3 (Growth - 2 sprints):
├── Complete referral program tracking
├── Implement VIP detection
└── Add marketing automation integration
```

**Total Effort to 90%:** 4-6 weeks (~167 story points)

#### Key Files
- `/src/DarkVelocity.Host/Domains/Customers/CustomerGrain.cs`
- `/src/DarkVelocity.Host/Domains/Customers/LoyaltyProgramGrain.cs`
- `/src/DarkVelocity.Host/Domains/Customers/CustomerSpendProjectionGrain.cs`

---

### 5. Inventory Domain

**Completeness: 60%**

#### Current Implementation
- FIFO batch/lot tracking with expiry
- Weighted average cost calculation
- Stock movements (receipt, consumption, waste, transfer, adjustment)
- Reorder points and par levels
- Negative stock support (matches philosophy)
- Supplier and purchase order management
- AI-powered invoice/receipt processing

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Stock Take Workflow | High | 26 days | Basic count only, no blind count or approval |
| Transfer Lifecycle Grain | High | 39 days | Basic in/out, no transit tracking |
| Variance Reporting | High | 44 days | Tracks but no analysis or alerts |
| Stock Optimization | Medium | 50 days | No ABC analysis or dynamic reorder points |
| Expiry Monitoring Job | High | 36 days | No background job for expiry alerts |

#### Recommended Actions

```
Phase 1 (Core - 6-8 weeks):
├── Create StockTakeGrain with blind count workflow
├── Add variance analysis and alerting
├── Implement expiry monitoring background job
└── Create transfer lifecycle grain

Phase 2 (Advanced - 4-6 weeks):
├── Implement ABC classification
├── Add automatic reorder suggestions
├── Create multi-location inventory view
└── Build three-way match (PO-Delivery-Invoice)
```

**Total Effort to 90%:** 6-8 weeks (~260 days across phases)

#### Key Files
- `/src/DarkVelocity.Host/Domains/Inventory/InventoryGrain.cs`
- `/src/DarkVelocity.Host/Domains/Procurement/ProcurementGrains.cs`
- `/src/DarkVelocity.Host/Domains/Procurement/PurchaseDocumentGrain.cs`

---

### 6. Menu Domain

**Completeness: 82%** *(improved from 80%)*

#### Current Implementation
- Event-sourced CMS with draft/publish workflow
- Multi-language localization
- Nutrition and allergen data
- 86'd items with snooze duration
- Daypart availability windows
- Site-specific overrides
- Channel visibility (POS, online, delivery)
- Reusable modifier blocks
- **NEW:** Bundle/combo support with flexible selection rules (Fixed, ChooseOne, ChooseMany, ChooseRange)
- **NEW:** Per-slot price adjustments for upsells/downgrades
- **NEW:** Nested menu items as bundle options

#### Remaining Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Auto-86 from Inventory | High | 3-5 days | Manual snooze only, no inventory integration |
| Upsell Prompts | Medium | 3-5 days | No cross-sell or upsell suggestions |
| Price Tiers | Medium | 1 week | No VIP/staff/loyalty pricing |
| Menu Engineering Integration | Medium | 3-5 days | Grain exists but not integrated in resolution |
| Modifier Availability Windows | Medium | 3-5 days | Only items have daypart availability |

#### Recommended Actions

```
Phase 1 (Essential - 1 week):
├── Integrate inventory for auto-snoozing
└── Connect menu engineering data to resolution

Phase 2 (Enhanced - 1.5 weeks):
├── Add UpsellPrompt support
├── Implement PriceTier for customer segments
├── Add modifier availability windows
└── Enhance media support (gallery, video)
```

**Total Effort to 95%:** 2.5-3 weeks

#### Key Files
- `/src/DarkVelocity.Host/Domains/Menu/MenuCmsState.cs`
- `/src/DarkVelocity.Host/Domains/Menu/MenuResolverGrains.cs`
- `/src/DarkVelocity.Host/Domains/Menu/MenuEngineeringGrain.cs`

---

### 7. Recipes Domain

**Completeness: 55%**

#### Current Implementation
- Recipe CMS with 3-level versioning (published, draft, current)
- Localization support
- Ingredient management with waste percentage
- Batch prep configuration
- Menu item linkage
- Recipe registry for indexing
- Cost calculation with snapshots

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Missing Ingredient Grain | High | 2-3 weeks | Ingredients are just Guids with string names |
| Domain Fragmentation | High | 1-2 weeks | RecipeCMS and Costing domains disconnected |
| Sub-Recipes | High | 1-2 weeks | Cannot use recipes as ingredients |
| Allergen Inheritance | High | 1 week | Manual tagging, not computed from ingredients |
| Recipe Scaling | Medium | 1 week | Cannot adjust for different batch sizes |
| Nutritional Calculation | Medium | 1 week | Not calculated from ingredients |
| Production Tracking | Medium | 1-2 weeks | Events defined but no grain |

#### Recommended Actions

```
Phase 1 (Critical - 3 weeks):
├── Create IIngredientGrain for master data
├── Implement allergen inheritance from ingredients
├── Consolidate recipe costing integration
└── Add recipe validation rules

Phase 2 (Enhanced - 3 weeks):
├── Add sub-recipe/component support
├── Implement recipe scaling
├── Add nutritional calculation
├── Create batch production tracking grain
```

**Total Effort to 85%:** 8-12 weeks

#### Key Files
- `/src/DarkVelocity.Host/Domains/Menu/RecipeCmsGrains.cs`
- `/src/DarkVelocity.Host/Domains/Costing/CostingGrains.cs`

---

### 8. Costing Domain

**Completeness: 50%**

#### Current Implementation
- Recipe cost calculation
- Ingredient price tracking with history (max 100 entries)
- Cost alerts with acknowledgment
- Costing settings with thresholds
- Menu engineering (BCG matrix)
- FIFO/LIFO/WAC costing policies
- Recipe snapshots (max 52 weekly)

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| NO API ENDPOINTS | Critical | 40h | Zero REST API exposure |
| Alert Index | High | 20h | Cannot query alerts by type/status |
| Cost Trending & Variance | High | 30h | No variance analysis |
| Profitability Dashboard | High | 25h | No aggregated profitability view |
| Menu Engineering Integration | Medium | 10h | Uses theoretical cost, not actual recipe costs |
| Automatic Alert Generation | Medium | 15h | Manual alerts only |
| Accounting Group | Medium | 35h | Listed in docs but not implemented |

#### Recommended Actions

```
Phase 1 (MVP - 2 weeks):
├── Create CostingEndpoints.cs (all grains)
├── Create CostAlertIndexGrain
└── Implement profitability dashboard grain

Phase 2 (Analytics - 3 weeks):
├── Add cost trending grain
├── Implement variance analysis
├── Connect menu engineering to real recipe costs
└── Add automatic alert generation
```

**Total Effort to 90%:** 6-8 weeks (~245 hours)

#### Key Files
- `/src/DarkVelocity.Host/Domains/Costing/CostingGrains.cs`
- `/src/DarkVelocity.Host/Domains/Menu/MenuEngineeringGrain.cs`

#### Files to Create
- `/src/DarkVelocity.Host/Domains/Costing/CostingEndpoints.cs`
- `/src/DarkVelocity.Host/Domains/Costing/CostAlertIndexGrain.cs`

---

### 9. Tables & Bookings Domain

**Completeness: 60%**

#### Current Implementation
- Booking lifecycle with event sourcing
- Table status management
- Floor plan support
- Waitlist with quoted times
- Booking settings per site
- Deposit lifecycle (required → paid → applied/forfeited/refunded)
- Booking accounting subscriber for journal entries

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| BookingCalendarGrain | Critical | 40-60h | Referenced but NOT implemented |
| Advanced Availability | High | 30-40h | No actual table matching logic |
| Notifications | High | 20-25h | No confirmation or reminder sending |
| No-Show Detection | High | 25-35h | Manual marking only |
| Turn Time Tracking | Medium | 20-30h | No actual turn time calculation |
| Table Optimization | Medium | 35-45h | No smart table assignment |

#### Recommended Actions

```
Phase 1 (Critical - 2 weeks):
├── Implement BookingCalendarGrain
├── Create advanced availability calculation
└── Add no-show auto-detection via Orleans reminders

Phase 2 (Enhancement - 3 weeks):
├── Implement NotificationSchedulerGrain
├── Add turn time tracking and analytics
├── Create TableAssignmentOptimizerGrain
└── Integrate deposit payment links
```

**Total Effort to 90%:** 6-8 weeks (~285-395 hours)

#### Key Files
- `/src/DarkVelocity.Host/Domains/Bookings/BookingGrain.cs`
- `/src/DarkVelocity.Host/Domains/Bookings/TableGrain.cs`

---

### 10. Staff Domain

**Completeness: 55%**

#### Current Implementation
- Employee lifecycle management
- Clock in/out with method tracking
- Role and permission management
- Schedule management with labor cost
- Time entry tracking
- Tip pool distribution
- Payroll period aggregation
- Availability and time off requests
- Shift swap workflow
- **NEW:** Comprehensive test coverage

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Overtime Calculation | High | 21 pts | Only >8 hours/day, no weekly or jurisdiction rules |
| Labor Compliance | High | 20 pts | No break validation, no child labor rules |
| Certifications | High | 18 pts | Role requires but not tracked |
| Payroll Export | High | 21 pts | Stub only, no ADP/Gusto/QB export |
| Tax Calculation | High | 18 pts | No federal/state/local withholding |
| Break Tracking | High | 16 pts | Events defined but not implemented |
| Performance Metrics | Medium | 21 pts | No tracking at all |

#### Recommended Actions

```
Phase 1 (Critical - 4 weeks):
├── Implement jurisdiction-aware overtime rules
├── Create LaborLawComplianceGrain
├── Implement break tracking in EmployeeGrain
├── Create payroll export service (ADP, Gusto, CSV)
└── Add tax calculation service

Phase 2 (Enhanced - 4 weeks):
├── Create certification tracking grain
├── Implement performance metrics
├── Add paid leave accrual
├── Create direct deposit management
```

**Total Effort to 90%:** 8-10 weeks (~250 story points)

#### Key Files
- `/src/DarkVelocity.Host/Domains/Staff/EmployeeGrain.cs`
- `/src/DarkVelocity.Host/Domains/Staff/LaborGrains.cs`

---

### 11. Finance Domain

**Completeness: 40%**

#### Current Implementation
- Account grain with double-entry
- Journal entries per account
- Expense grain with approval workflow
- Expense index for queries
- Ledger grain (utility for balances)
- Tax rate management
- Booking and GiftCard accounting subscribers

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Chart of Accounts | P0 | 1-2 weeks | No centralized account registry |
| Journal Entry Grain | P0 | 1-2 weeks | No proper multi-line balanced entries |
| Accounting Periods | P1 | 1 week | Minimal period management |
| Financial Reports | P1 | 2-3 weeks | No P&L, balance sheet, cash flow |
| Expense-to-GL | P1 | 1 week | Expenses don't post to GL |
| AP/AR Grains | P2 | 3-4 weeks | No accounts payable/receivable |
| External Integration | P3 | 6-8 weeks | No QuickBooks/Xero sync |

#### Recommended Actions

```
Phase 1 (Foundation - 4 weeks):
├── Create ChartOfAccountsGrain
├── Create JournalEntryGrain (balanced double-entry)
├── Create AccountingPeriodGrain
├── Add Account REST API endpoints
└── Create expense-to-GL subscriber

Phase 2 (Automation - 3 weeks):
├── Create financial reports (Trial Balance, P&L, Balance Sheet)
├── Add sales/COGS posting subscribers
└── Implement period closing automation

Phase 3 (Advanced - 5 weeks):
├── Create AccountsPayable/ReceivableGrain
├── Implement bank reconciliation
└── Build QuickBooks/Xero integration
```

**Total Effort to 90%:** 12-15 weeks

#### Key Files
- `/src/DarkVelocity.Host/Domains/Finance/AccountGrain.cs`
- `/src/DarkVelocity.Host/Domains/Finance/ExpenseGrain.cs`

---

### 12. External Channels Domain

**Completeness: 40%**

#### Current Implementation
- Channel grain with connection management
- Channel registry per organization
- Status mapping between platforms
- Delivery platform credentials
- External order grain with full data model
- Menu sync tracking
- Platform payout tracking
- Daily metrics tracking
- Comprehensive API endpoints for channels

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Webhook Handlers | Critical | 2-3 weeks | No inbound webhook endpoints |
| HTTP Clients | Critical | 2-3 weeks | No outbound API calls to platforms |
| Platform Adapters | Critical | 3-4 weeks | No Deliverect/UberEats/DoorDash adapters |
| External Order API | High | 3-5 days | Grain exists but no endpoints |
| OAuth2 M2M | High | 1 week | No client credentials grant |
| Menu Push | High | 2 weeks | No menu sync to platforms |
| Payout Reconciliation | Medium | 2-3 weeks | No matching against orders |

#### Recommended Actions

```
Phase 1 (Foundation - 2 weeks):
├── Create API endpoints for ExternalOrder grains
├── Build webhook receiver infrastructure
└── Create platform adapter architecture

Phase 2 (Platform Integration - 2 weeks):
├── Implement Deliverect adapter + webhook handler
├── Create menu push implementation
└── Add OAuth2 client credentials flow

Phase 3 (Operations - 2 weeks):
├── Build payout reconciliation
├── Add commission tracking
└── Implement channel health monitoring
```

**Total Effort to 90%:** 6-7 weeks (~28 days)

#### Key Files
- `/src/DarkVelocity.Host/Domains/Channels/ChannelGrain.cs`
- `/src/DarkVelocity.Host/Domains/Orders/OrdersGatewayGrains.cs`

---

### 13. Devices Domain

**Completeness: 45%**

#### Current Implementation
- Device authorization (OAuth 2.0 device flow)
- Device registration and lifecycle
- Session management with JWT
- POS device management
- Printer configuration
- Cash drawer hardware
- Device status aggregation
- Kitchen display system grains

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Print Job Management | Critical | 8-12 pts | No job queue, no retry, no status tracking |
| Offline Mode & Sync | Critical | 10-15 pts | No local storage or sync queue |
| Device Health Monitoring | Critical | 8-10 pts | Only LastSeenAt, no metrics |
| Device Provisioning | High | 5-8 pts | No setup wizard |
| Terminal Pairing | High | 8-10 pts | Minimal implementation |
| Multi-Device Sync | Medium | 8-12 pts | No order hand-off or shared state |
| ESC/POS Library | Low | 8-10 pts | Only basic cash drawer kick |

#### Recommended Actions

```
Phase 1 (Critical - 4 weeks):
├── Create PrintJobGrain with queue and retry
├── Implement IndexedDB schema for offline
├── Create OfflineQueueGrain for sync
└── Add device health monitoring

Phase 2 (Production - 4 weeks):
├── Build device provisioning wizard
├── Implement ESC/POS command builder
├── Create receipt template engine
└── Add multi-device synchronization
```

**Total Effort to 90%:** 6 sprints (~75-110 points)

#### Key Files
- `/src/DarkVelocity.Host/Domains/Devices/DeviceGrain.cs`
- `/src/DarkVelocity.Host/Domains/Devices/DeviceAuthGrain.cs`
- `/apps/pos/` - PWA application

---

### 14. Reporting Domain

**Completeness: 60%**

#### Current Implementation
- Daily sales aggregation
- Daily inventory snapshots
- Daily consumption tracking
- Daily waste tracking
- Period aggregation (weekly/monthly)
- Site dashboard composition
- Comprehensive API endpoints
- Stream subscribers for orders and inventory

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Hourly/Daypart Analysis | High | 3-4 days | No time-based breakdown |
| Labor Reports | Critical | 3-4 weeks | Not implemented at all |
| Payment Reconciliation | High | 2-3 weeks | No tender breakdown |
| Dashboard Metrics | High | 5-7 days | WTD/PTD are placeholders (hardcoded 0) |
| Data Export | High | 3 weeks | No CSV/Excel/PDF |
| Product Mix | Medium | 2 weeks | Partial only |
| Comparative Reports | Medium | 2 weeks | No YoY, MoM trending |

#### Recommended Actions

```
Phase 1 (Foundation - 4 weeks):
├── Complete dashboard metrics calculations
├── Add hourly/daypart analysis
├── Implement data export (CSV/Excel)
└── Add payment method breakdown

Phase 2 (Core - 4 weeks):
├── Create labor reporting grains
├── Implement payment reconciliation
├── Add product mix analysis
└── Build comp/void breakdown

Phase 3 (Advanced - 4 weeks):
├── Create comparative/trending reports
├── Implement tax by jurisdiction
├── Add inventory turnover calculations
```

**Total Effort to 90%:** 12-15 weeks

#### Key Files
- `/src/DarkVelocity.Host/Domains/Reporting/ReportingGrains.cs`
- `/src/DarkVelocity.Host/Domains/Reporting/ReportingEndpoints.cs`

---

### 15. Fiscal Domain

**Completeness: 50%** *(improved from 35%)*

#### Current Implementation
- Fiscal device grain with counters
- Fiscal transaction lifecycle
- Fiscal journal (append-only)
- Tax rate management
- Multi-device type support (Swissbit, Fiskaly, Epson, Diebold)
- Comprehensive test coverage
- **NEW:** Fiskaly cloud integration with Germany, Austria, Italy support
- **NEW:** Internal TSE provider with HMAC-SHA256 signatures
- **NEW:** Configuration-driven integration (FiskalyConfigGrain)
- **NEW:** TSE domain events for external TSE mapping

#### Remaining Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| NO API ENDPOINTS | Critical | 2-3 days | Zero REST API |
| NO DSFinV-K EXPORT | Critical | 1-2 weeks | Compliance requirement |
| No Order Integration | Critical | 2-3 days | Operates in isolation |
| External TSE Adapters | High | 2-3 weeks | Swissbit USB/Cloud not implemented |
| Device Lifecycle | High | 1-2 weeks | Basic registration only |
| Italy RT Implementation | High | 1 week | Stubbed only |
| France/Poland | Medium | 4 weeks | No NF 525 or JPK/KSeF |

#### Recommended Actions

```
Phase 1 (MVP Germany - 3 weeks):
├── Create FiscalEndpoints.cs
├── Implement order→fiscal integration
├── Create DSFinV-K export
└── Add device lifecycle management

Phase 2 (Production - 3 weeks):
├── Implement external TSE adapters (Swissbit)
├── Complete Italy RT implementation
├── Add background jobs (Z-report, cert expiry)
└── Create error recovery/resilience

Phase 3 (Multi-Country - 4 weeks):
├── Implement France (NF 525)
├── Implement Poland (JPK/KSeF)
```

**Total Effort to 90%:** 10-14 weeks

#### Key Files
- `/src/DarkVelocity.Host/Domains/Fiscal/FiscalisationGrains.cs`
- `/src/DarkVelocity.Host/Domains/Fiscal/FiskalyIntegration.cs`

---

### 16. Organization Domain

**Completeness: 70%**

#### Current Implementation
- Organization CRUD with status management
- Site management with timezone/currency
- User authentication (PIN, OAuth)
- User groups
- SpiceDB authorization schema
- Email and OAuth lookup grains

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| No Event Sourcing | P0 | 40h | Simple state, no audit trail |
| Subscription Plans | P0 | 56h | No tier limits or feature flags |
| Cancellation Flow | P0 | 28h | No proper offboarding |
| Ownership Transfer | P0 | 24h | Documented but not implemented |
| Slug Uniqueness | P1 | 16h | Not enforced |
| Branding | P2 | 32h | No logo, colors, theme |
| UserGroup Endpoints | P1 | 20h | Grain exists but no API |

#### Recommended Actions

```
Phase 1 (Foundation - 4 weeks):
├── Convert to JournaledGrain pattern
├── Implement cancellation flow
├── Add ownership transfer
└── Create subscription plan management

Phase 2 (Completeness - 3 weeks):
├── Create SlugLookupGrain for uniqueness
├── Add UserGroup API endpoints
├── Implement branding settings
└── Add multi-site aggregation
```

**Total Effort to 90%:** 4-5 weeks (~352 hours)

#### Key Files
- `/src/DarkVelocity.Host/Domains/Organization/OrganizationGrain.cs`
- `/src/DarkVelocity.Host/Domains/Organization/SiteGrain.cs`
- `/src/DarkVelocity.Host/Domains/Organization/UserGrain.cs`

---

### 17. System Domain

**Completeness: 35%**

#### Current Implementation
- Alert grain with rule engine structure
- Alert types and severity levels
- Email inbox with attachment processing
- Workflow grain (generic state machine)
- Webhook subscription management
- Good test coverage

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| NotificationGrain | Critical | 80-120h | INTERFACE ONLY - no implementation |
| Webhook Delivery | Critical | 60-80h | Returns mock success, no HTTP calls |
| Alert Evaluation | Critical | 40-60h | EvaluateRulesAsync() is a stub |
| Background Jobs | High | 40-60h | No scheduled processing |
| API Endpoints | High | 30-40h | No alert/workflow endpoints |
| Blob Storage | Medium | 35-50h | Email attachments not persisted |
| Circuit Breaker | Medium | 20-30h | Webhooks fail permanently |

#### Recommended Actions

```
Phase 1 (Critical - 4 weeks):
├── Implement NotificationGrain (email, SMS, Slack, push)
├── Implement AlertGrain.EvaluateRulesAsync()
├── Create HttpWebhookDeliveryService
└── Create background job scheduler

Phase 2 (Reliability - 3 weeks):
├── Add webhook retry queue with exponential backoff
├── Implement circuit breaker pattern
├── Create alert/workflow API endpoints
└── Add audit logging

Phase 3 (Operations - 2 weeks):
├── Add blob storage for attachments
├── Create system health monitoring
├── Implement webhook analytics
```

**Total Effort to 90%:** 16-17 weeks (~630-700 hours)

#### Key Files
- `/src/DarkVelocity.Host/Domains/Alerts/AlertGrain.cs`
- `/src/DarkVelocity.Host/Domains/Workflow/WebhookGrain.cs`
- `/src/DarkVelocity.Host/Domains/Workflow/EmailInboxGrain.cs`

---

## Implementation Roadmap

### Phase 1: Production Blockers (Weeks 1-8)

| Week | Focus | Deliverables |
|------|-------|--------------|
| 1-2 | Payments | Settlement batch, offline queue |
| 2-3 | Payment Processors | Stripe/Adyen implementation |
| 3-4 | Devices | Offline mode, print job management |
| 4-5 | External Channels | Webhook handlers, Deliverect adapter |
| 5-6 | System | Notification delivery, webhook delivery |
| 6-7 | Costing | API endpoints, alert index |
| 7-8 | Fiscal | API endpoints, DSFinV-K export |

### Phase 2: Core Completeness (Weeks 9-16)

| Week | Focus | Deliverables |
|------|-------|--------------|
| 9-10 | Finance | Chart of accounts, journal entries |
| 10-11 | Reporting | Labor reports, dashboard metrics |
| 11-12 | Staff | Overtime, break tracking, payroll export |
| 12-13 | Orders | Seat assignment, line discounts |
| 13-14 | Tables & Bookings | BookingCalendar, notifications |
| 14-15 | Customers | Segmentation, birthday rewards |
| 15-16 | Inventory | Stock take workflow, variance reporting |

### Phase 3: Market Expansion (Weeks 17-24)

| Week | Focus | Deliverables |
|------|-------|--------------|
| 17-18 | External Channels | UberEats, DoorDash adapters |
| 18-20 | Fiscal | France (NF 525), Poland (KSeF) |
| 20-22 | Finance | QuickBooks, Xero integration |
| 22-24 | Advanced Features | Analytics, forecasting, multi-currency |

---

## Estimated Total Effort

| Domain | Current | Effort to 90% |
|--------|---------|---------------|
| Payment Processors | 30% | 10-14 weeks |
| System | 35% | 16-17 weeks |
| Finance | 40% | 12-15 weeks |
| External Channels | 40% | 6-7 weeks |
| Devices | 45% | 6 sprints |
| Costing | 50% | 6-8 weeks |
| Fiscal | 50% | 10-14 weeks |
| Recipes | 55% | 8-12 weeks |
| Staff | 55% | 8-10 weeks |
| Reporting | 60% | 12-15 weeks |
| Tables & Bookings | 60% | 6-8 weeks |
| Inventory | 60% | 6-8 weeks |
| Customers | 65% | 4-6 weeks |
| Organization | 70% | 4-5 weeks |
| Payments | 75% | 4-5 weeks |
| Orders | 78% | 4-6 weeks |
| Menu | 82% | 3-4 weeks |

**Total Estimated Effort:** 110-150 developer-weeks for full production readiness

---

## Architecture Strengths

The codebase demonstrates strong architectural patterns:

1. **Orleans Virtual Actors** - Proper grain key conventions, single-writer principle
2. **Event Sourcing** - JournaledGrain pattern used consistently where appropriate
3. **HAL+JSON APIs** - Consistent REST API design with hypermedia
4. **Composition over Inheritance** - Grains collaborate via grain-to-grain calls
5. **Stream-Based Integration** - Orleans streams for cross-domain events
6. **Multi-Tenancy** - Proper org/site key prefixing throughout
7. **Test Coverage** - Comprehensive test patterns (~18,600 lines)
8. **Configuration-Driven Integration** - Fiskaly integration shows good patterns

## Architecture Recommendations

1. **Standardize Event Sourcing** - Convert remaining simple-state grains to JournaledGrain
2. **Add Background Services** - Many features need scheduled processing
3. **Implement Circuit Breakers** - For all external integrations
4. **Add Observability** - OpenTelemetry for tracing and metrics
5. **Create Index Grains** - For queryable aggregations
6. **Build Subscriber Pattern** - More Orleans stream subscribers for integration

---

## Appendix: File Index

### Core Domain Files

```
src/DarkVelocity.Host/Domains/
├── Alerts/
│   ├── AlertGrain.cs
│   ├── AlertState.cs
│   └── IAlertGrain.cs
├── Bookings/
│   ├── BookingGrain.cs
│   ├── BookingState.cs
│   └── TableGrain.cs
├── Channels/
│   ├── ChannelGrain.cs
│   ├── ChannelEndpoints.cs
│   └── StatusMappingGrain.cs
├── Costing/
│   └── CostingGrains.cs
├── Customers/
│   ├── CustomerGrain.cs
│   └── LoyaltyProgramGrain.cs
├── Devices/
│   ├── DeviceAuthGrain.cs
│   └── DeviceGrain.cs
├── Finance/
│   ├── AccountGrain.cs
│   └── ExpenseGrain.cs
├── Fiscal/
│   ├── FiscalisationGrains.cs
│   └── FiskalyIntegration.cs
├── Inventory/
│   └── InventoryGrain.cs
├── Menu/
│   ├── MenuCmsGrains.cs
│   └── MenuResolverGrains.cs
├── Orders/
│   ├── OrderGrain.cs
│   └── KitchenGrain.cs
├── Organization/
│   ├── OrganizationGrain.cs
│   ├── SiteGrain.cs
│   └── UserGrain.cs
├── Payments/
│   ├── PaymentGrain.cs
│   └── GiftCardGrain.cs
├── Procurement/
│   └── ProcurementGrains.cs
├── Reporting/
│   └── ReportingGrains.cs
├── Staff/
│   ├── EmployeeGrain.cs
│   └── LaborGrains.cs
└── Workflow/
    ├── WebhookGrain.cs
    ├── WorkflowGrain.cs
    └── EmailInboxGrain.cs
```

### Test Files

```
tests/DarkVelocity.Tests/
├── AlertGrainTests.cs
├── BookingGrainTests.cs
├── ChannelGrainTests.cs
├── CustomerGrainTests.cs
├── DeviceAuthGrainTests.cs
├── EmailInboxGrainTests.cs
├── ExpenseGrainTests.cs
├── FiscalDeviceGrainTests.cs
├── GiftCardGrainTests.cs
├── InventoryGrainTests.cs
├── OrderGrainTests.cs
├── PaymentGrainTests.cs
├── RecipeCmsGrainTests.cs
├── StaffGrainTests.cs
├── WebhookGrainTests.cs
└── WorkflowGrainTests.cs
```

---

*This document was updated after rebasing to include recent improvements: bundle/combo support, order holds/fires, Fiskaly integration, and comprehensive test coverage.*
