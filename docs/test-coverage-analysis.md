# Domain Test Coverage Analysis

**Generated:** 2026-02-04
**Branch:** claude/domain-test-coverage-agents-opmHg

## Executive Summary

This document provides a comprehensive analysis of test coverage across all 17 domains in the DarkVelocity POS system. The analysis identifies existing coverage, gaps, and recommended test cases to add.

**Overall Assessment:**
- **Total Domains:** 17
- **Total Grains:** ~100
- **Grains with 0% Coverage:** 15+ (critical)
- **Estimated Current Coverage:** ~55%
- **Estimated Tests to Add:** ~700-800

---

## Coverage Overview by Domain

| Domain | Grains | Tests | Coverage | Status |
|--------|--------|-------|----------|--------|
| Orders | 4 | 87 | ~80% | Good |
| Payments | 10 | ~60 | ~70% | Good |
| Customers | 3 | 59 | ~55% | Partial |
| Inventory | 2 | ~50 | ~60% | Partial |
| Menu | 13 | ~110 | ~60% | Partial |
| Bookings | 5 | ~45 | ~70% | Good |
| Staff | 9 | 19 | **~15%** | **Critical** |
| Finance | 4 | ~45 | ~50% | Partial |
| Channels | 7 | ~45 | ~70% | Good |
| Organization | 10 | ~75 | ~60% | Partial |
| Devices | 9 | 48 | **~44%** | **Critical** |
| Reporting | 6 | 27 | ~71% | Good |
| Fiscal | 4 | 32 | ~40% | Needs Work |
| Procurement | 4 | 42 | ~70% | Good |
| Workflow | 3 | 31 | ~50% | Partial |
| Costing | 4 | 13 | ~38% | Needs Work |
| Alerts | 1 | 9 | ~40% | Needs Work |

---

## Critical Gaps (0% Coverage)

### 1. Staff Domain - Multiple Grains Untested

The Staff domain has the lowest coverage at ~15%. The following grains have **zero tests**:

- `RoleGrain` - Role creation, updates, certifications
- `ScheduleGrain` - Weekly schedule management, shift CRUD, publishing/locking
- `TimeEntryGrain` - Time clock, breaks, overtime calculation, approvals
- `TipPoolGrain` - Tip distribution (equal, by hours, by points)
- `PayrollPeriodGrain` - Payroll calculation, approval workflow

**Impact:** Core labor management features are untested.

### 2. Organization Domain - OAuth Flow Untested

All OAuth-related grains have **zero tests**:

- `OAuthStateGrain` - CSRF protection, PKCE parameters
- `AuthorizationCodeGrain` - Authorization code flow, PKCE validation
- `ExternalIdentityGrain` - OAuth identity linking
- `OAuthLookupGrain` - Identity resolution
- `EmailLookupGrain` - Global email to user mapping

**Impact:** Security-critical authentication flow is completely untested.

### 3. Devices Domain - Authentication Untested

All device authentication grains have **zero tests**:

- `DeviceAuthGrain` - OAuth device flow, code generation
- `SessionGrain` - JWT token generation, refresh token rotation
- `DeviceGrain` - Device lifecycle management
- `UserLookupGrain` - PIN-based user lookup
- `RefreshTokenLookupGrain` - Token mapping

**Impact:** Device onboarding and session management untested.

### 4. Finance Domain - Ledger Untested

- `LedgerGrain` - 0 tests (double-entry bookkeeping core)
- `ExpenseIndexGrain` - 0 tests (expense query operations)

**Impact:** Financial accounting core is untested.

### 5. Workflow Domain - WorkflowGrain Untested

- `WorkflowGrain` - 0 tests (generic approval workflow state machine)

**Impact:** Cross-domain approval workflows untested.

### 6. Bookings Domain - Missing Implementation

- `BookingCalendarGrain` - Tests exist but **grain not implemented**

**Impact:** Tests will fail; implementation needed.

---

## Domain-by-Domain Analysis

### 1. Orders Domain (~80% coverage)

**Grains:** OrderGrain, KitchenTicketGrain, KitchenStationGrain, LineItemsGrain

**Well Covered:**
- Order lifecycle (create, close, void)
- Line item management
- Discounts, modifiers, tax rates
- Payment recording
- Bill splitting
- Kitchen ticket routing

**Missing Tests:**
- `AddServiceChargeAsync` - not tested at all
- `RemoveDiscountAsync` - not tested
- `RemovePaymentAsync` - not tested
- `ReopenAsync` - not tested
- State transition guards (void closed order, close closed order)
- Service charges + discount interaction
- Complex split payment scenarios

**Recommended:** ~60-80 additional tests

---

### 2. Payments Domain (~70% coverage)

**Grains:** PaymentGrain, GiftCardGrain, PaymentIntentGrain, PaymentMethodGrain, MockProcessorGrain, MerchantGrain, TerminalGrain, RefundGrain, WebhookEndpointGrain, CashDrawerGrain

**Well Covered:**
- Basic payment completion (cash, card)
- Refunds (full and partial)
- Gift card lifecycle
- Payment intent basics

**Missing Tests:**
- Card authorization flow (Initiated → Authorizing → Authorized → Captured)
- 3DS authentication flow (`HandleNextActionAsync`)
- `RecordAuthorizationAsync`, `RecordDeclineAsync`, `CaptureAsync`
- Cash drawer `CountAsync`
- State transition validations
- Merchant API key management
- Test cards (expired, CVC fail, processing error)

**Recommended:** ~50-70 additional tests

---

### 3. Customers Domain (~55% coverage)

**Grains:** CustomerGrain, CustomerSpendProjectionGrain, LoyaltyProgramGrain

**Well Covered:**
- Customer creation and profile updates
- Loyalty enrollment, points earning/redemption
- Visit tracking
- Preferences management

**Missing Tests:**
- Error handling (earn points without enrollment, expired reward redemption)
- `AdjustPointsAsync` - not tested
- `ExpirePointsAsync` - not tested
- Period resets (YTD, MTD)
- Transaction history limits (100/50)
- Program lifecycle (deactivation)
- Stream event publishing

**Recommended:** ~40-50 additional tests

---

### 4. Inventory Domain (~60% coverage)

**Grains:** InventoryGrain, VendorItemMappingGrain

**Well Covered:**
- FIFO consumption logic
- Batch receipts and WAC calculation
- Waste recording
- Stock adjustments
- Transfer operations

**Missing Tests:**
- Alert stream publishing verification
- Ledger integration testing
- Stock reservation system (field exists, unused)
- Physical count workflow (`RecordPhysicalCountAsync`)
- Movement history pruning
- Fuzzy matching confidence thresholds

**Recommended:** ~35-40 additional tests

---

### 5. Menu Domain (~60% coverage)

**Grains:** MenuItemGrain, MenuCategoryGrain, MenuDefinitionGrain, AccountingGroupGrain, MenuEngineeringGrain, RecipeDocumentGrain, RecipeCategoryDocumentGrain, RecipeRegistryGrain, MenuItemDocumentGrain, MenuCategoryDocumentGrain, ModifierBlockGrain, ContentTagGrain, SiteMenuOverridesGrain

**Well Covered:**
- Basic CRUD operations
- Menu definition screens/buttons
- Recipe cost calculations
- Site menu overrides

**Missing Tests:**
- Draft/publish workflows for CMS grains
- Version management and history
- `RemoveTranslationAsync`
- Scheduling features
- Category hiding/unhiding
- Event sourcing replay

**Grains with lowest coverage:**
- MenuCategoryDocumentGrain (~20%)
- ModifierBlockGrain (~25%)
- RecipeCategoryDocumentGrain (~30%)

**Recommended:** ~80-100 additional tests

---

### 6. Bookings Domain (~70% coverage)

**Grains:** BookingGrain, TableGrain, FloorPlanGrain, BookingSettingsGrain, WaitlistGrain

**Well Covered:**
- Booking lifecycle
- Table management
- Guest arrival/seating flow
- Deposit handling

**Missing Tests:**
- State transition validation
- Deposit edge cases (waive, forfeit validation)
- Table combination with IsCombinable=false
- `GetChannelsForLocationAsync` (ChannelRegistryGrain)
- **BookingCalendarGrain implementation missing**

**Recommended:** ~30 tests + implement BookingCalendarGrain

---

### 7. Staff Domain (~15% coverage) **CRITICAL**

**Grains:** EmployeeGrain, RoleGrain, ScheduleGrain, TimeEntryGrain, TipPoolGrain, PayrollPeriodGrain, EmployeeAvailabilityGrain, ShiftSwapGrain, TimeOffGrain

**Tested:** EmployeeGrain (basic), EmployeeAvailabilityGrain, ShiftSwapGrain, TimeOffGrain (partially)

**Completely Untested:**
- `RoleGrain` - role CRUD, certifications
- `ScheduleGrain` - schedule lifecycle, shift management, publishing
- `TimeEntryGrain` - clock in/out, breaks, overtime, approvals
- `TipPoolGrain` - all distribution methods
- `PayrollPeriodGrain` - payroll workflow

**Recommended:** ~100+ additional tests

---

### 8. Finance Domain (~50% coverage)

**Grains:** AccountGrain, ExpenseGrain, LedgerGrain, ExpenseIndexGrain

**Well Covered:**
- Account CRUD
- Double-entry mechanics
- Period closing
- Expense lifecycle

**Completely Untested:**
- `LedgerGrain` - all functionality (0 tests)
- `ExpenseIndexGrain` - all query operations (0 tests)

**Missing Tests:**
- Reconciliation
- Hierarchical accounts
- Multi-currency
- Recurring expenses

**Recommended:** ~100 additional tests (LedgerGrain: 20, ExpenseIndexGrain: 15, others: 65)

---

### 9. Channels Domain (~70% coverage)

**Grains:** ChannelGrain, StatusMappingGrain, ChannelRegistryGrain, DeliveryPlatformGrain, ExternalOrderGrain, MenuSyncGrain, PlatformPayoutGrain

**Well Covered:**
- Channel CRUD
- Status mapping
- External order lifecycle
- Menu sync basics

**Missing Tests:**
- Daily counter reset
- `DisconnectAsync`
- `UpdateCourierAsync`
- `GetChannelsForLocationAsync`
- State transition validation
- Complex order data (modifiers, discounts)

**Recommended:** ~35 additional tests

---

### 10. Organization Domain (~60% coverage)

**Grains:** OrganizationGrain, SiteGrain, UserGrain, UserGroupGrain, IndexGrain, EmailLookupGrain, OAuthLookupGrain, OAuthStateGrain, ExternalIdentityGrain, AuthorizationCodeGrain

**Well Covered:**
- Organization/Site/User CRUD
- Index operations (excellent)

**Completely Untested:**
- `EmailLookupGrain` - 0 tests
- `OAuthLookupGrain` - 0 tests
- `OAuthStateGrain` - 0 tests (CSRF protection)
- `ExternalIdentityGrain` - 0 tests
- `AuthorizationCodeGrain` - 0 tests (PKCE validation)

**Recommended:** ~50 additional tests (OAuth focus)

---

### 11. Devices Domain (~44% coverage) **CRITICAL**

**Grains:** DeviceAuthGrain, DeviceGrain, SessionGrain, UserLookupGrain, RefreshTokenLookupGrain, PosDeviceGrain, PrinterGrain, CashDrawerHardwareGrain, DeviceStatusGrain

**Well Covered:**
- Hardware grains (POS, Printer, CashDrawer)
- DeviceStatusGrain

**Completely Untested:**
- `DeviceAuthGrain` - 0 tests (OAuth device flow)
- `DeviceGrain` - 0 tests (device lifecycle)
- `SessionGrain` - 0 tests (JWT generation)
- `UserLookupGrain` - 0 tests (PIN lookup)
- `RefreshTokenLookupGrain` - 0 tests

**Recommended:** ~120 additional tests (60+ for auth, 60 for edge cases)

---

### 12. Reporting Domain (~71% coverage)

**Grains:** DailySalesGrain, DailyInventorySnapshotGrain, DailyConsumptionGrain, DailyWasteGrain, PeriodAggregationGrain, SiteDashboardGrain

**Well Covered:**
- Basic aggregation
- Sales by channel
- Inventory snapshots

**Missing Tests:**
- Stream-based aggregation (`RecordSaleFromStreamCommand`)
- `RecordVoidAsync`
- Grain-to-grain calls in SiteDashboardGrain
- Period type coverage (monthly, yearly)
- Duplicate date protection

**Recommended:** ~20-30 additional tests

---

### 13. Fiscal Domain (~40% coverage)

**Grains:** FiscalDeviceGrain, FiscalTransactionGrain, FiscalJournalGrain, TaxRateGrain

**Well Covered:**
- Device registration
- Transaction signing
- Journal logging
- Tax rate basics

**Missing Tests:**
- All transaction types (Cancellation untested)
- German process types (AVTransfer, AVBestellung, AVSonstiger)
- Error handling
- Certificate expiry edge cases
- All journal event types
- Multi-device scenarios

**Recommended:** ~60 additional tests

---

### 14. Procurement Domain (~70% coverage)

**Grains:** SupplierGrain, PurchaseOrderGrain, DeliveryGrain, PurchaseDocumentGrain

**Well Covered:**
- Supplier CRUD
- PO lifecycle
- Delivery workflow

**Missing Tests:**
- Status transition validation
- `UnmapLineAsync` (completely untested)
- `SetLineSuggestionsAsync` (completely untested)
- Three-way match integration
- All discrepancy types

**Recommended:** ~30-40 additional tests

---

### 15. Workflow Domain (~50% coverage)

**Grains:** EmailInboxGrain, WebhookSubscriptionGrain, WorkflowGrain

**Well Covered:**
- Email processing
- Webhook delivery

**Completely Untested:**
- `WorkflowGrain` - 0 tests (approval state machine)

**Missing Tests:**
- Auto-processing path
- Stream event publishing

**Recommended:** ~25 tests for WorkflowGrain + 15 for others

---

### 16. Costing Domain (~38% coverage)

**Grains:** RecipeGrain, IngredientPriceGrain, CostAlertGrain, CostingSettingsGrain

**Well Covered:**
- Recipe cost calculation
- Ingredient price tracking

**Missing Tests:**
- Update operations across all grains
- Delete/soft delete operations
- Threshold checks
- Pack size calculations
- All alert types and actions

**Recommended:** ~40-50 additional tests

---

### 17. Alerts Domain (~40% coverage)

**Grains:** AlertGrain

**Well Covered:**
- Basic alert lifecycle
- Alert queries

**Missing Tests:**
- Error handling (operations on uninitialized)
- Snooze expiration behavior
- Stream event publishing
- All alert types (15 types, only 2 tested)
- State transition validation

**Recommended:** ~25 additional tests

---

## Common Gap Patterns

### 1. Error Handling & Validation (~90% of domains)
Most grains lack tests for:
- Operations on uninitialized grains (should throw)
- Double-creation prevention
- Invalid state transitions
- Not-found scenarios

### 2. Stream Event Publishing (~80% of domains)
Events are published but never verified in tests.

### 3. Edge Cases & Boundaries (~75% of domains)
- Zero/negative values
- Exactly-at-threshold conditions
- List/history trimming limits

### 4. State Transition Guards (~70% of domains)
Invalid status transitions not tested.

### 5. Integration Tests (~85% of domains)
Cross-grain workflows and end-to-end scenarios missing.

---

## Recommended Action Plan

### Phase 1: Critical Security (Highest Priority)
1. **Devices Domain Auth Grains** - DeviceAuthGrain, SessionGrain, DeviceGrain
2. **Organization OAuth Grains** - OAuthStateGrain, AuthorizationCodeGrain
3. **Finance LedgerGrain** - Core accounting

### Phase 2: Core Business Logic
1. **Staff Domain** - All untested grains
2. **Workflow WorkflowGrain** - Approval workflows
3. **Payments Card Auth Flow** - 3DS, authorization lifecycle

### Phase 3: Business Features
1. **Menu CMS Workflows** - Draft/publish, versioning
2. **Orders Advanced** - Service charges, reopening
3. **Bookings** - BookingCalendarGrain implementation

### Phase 4: Edge Cases & Robustness
1. Add error handling tests across all domains
2. Add stream event verification
3. Add boundary condition tests

---

## Appendix: Test Files by Domain

| Domain | Test Files |
|--------|-----------|
| Orders | OrderGrainTests.cs, OrdersGatewayGrainTests.cs, KitchenGrainTests.cs, KitchenTicketRoutingTests.cs, LineItemsGrainTests.cs, OrderInventoryIntegrationTests.cs |
| Payments | PaymentGrainTests.cs, PaymentIntentGrainTests.cs, PaymentMethodGrainTests.cs, GiftCardGrainTests.cs, MockProcessorGrainTests.cs, PaymentGatewayGrainTests.cs, PaymentGiftCardDecouplingTests.cs, PaymentSplitScenarioTests.cs, RefundErrorScenarioTests.cs |
| Customers | CustomerGrainTests.cs, CustomerSpendProjectionGrainTests.cs, LoyaltyProgramGrainTests.cs |
| Inventory | InventoryGrainTests.cs, VendorItemMappingGrainTests.cs, StockInventoryBddTests.cs |
| Menu | MenuGrainTests.cs, MenuEngineeringGrainTests.cs, RecipeCmsGrainTests.cs, MenuCmsGrainTests.cs, MenuItemAvailabilityTests.cs |
| Bookings | BookingGrainTests.cs, BookingExtendedGrainTests.cs, BookingAvailabilityTests.cs, TableGrainTests.cs |
| Staff | EmployeeGrainTests.cs, LaborExtendedGrainTests.cs |
| Finance | AccountGrainTests.cs, AccountingStreamTests.cs, ExpenseGrainTests.cs |
| Channels | ChannelGrainTests.cs |
| Organization | OrganizationGrainTests.cs, SiteGrainTests.cs, UserGrainTests.cs, IndexGrainTests.cs |
| Devices | HardwareGrainTests.cs |
| Reporting | DailySalesGrainTests.cs, ReportingGrainTests.cs |
| Fiscal | FiscalisationGrainTests.cs |
| Procurement | ProcurementGrainTests.cs, PurchaseDocumentGrainTests.cs |
| Workflow | EmailInboxGrainTests.cs, WebhookGrainTests.cs |
| Costing | CostingGrainTests.cs, CostingPolicyTests.cs |
| Alerts | AlertGrainTests.cs |
