# DarkVelocity POS - Comprehensive Integration Test Plan

## Executive Summary

This document outlines a comprehensive integration test plan for the DarkVelocity POS system. The plan focuses on business/domain scenarios that span multiple services and complex workflows.

---

## Part 1: Existing Test Coverage Summary

### Current Test Statistics
- **Total Tests**: 453 tests across 37 test files
- **Test Framework**: xUnit with FluentAssertions
- **Test Pattern**: ASP.NET Core Integration Tests with WebApplicationFactory
- **Database**: In-memory SQLite for test isolation

### Existing Tests by Service

| Service | Test File | Test Count | Coverage Focus |
|---------|-----------|------------|----------------|
| **Auth** | AuthControllerTests.cs | 5 | Login, token validation |
| | UsersControllerTests.cs | 8 | User CRUD, access management |
| **Orders** | OrdersControllerTests.cs | 13 | Order lifecycle, line items |
| | SalesPeriodsControllerTests.cs | 6 | Cash period management |
| **Menu** | ItemsControllerTests.cs | 10 | Menu item CRUD, filtering |
| | CategoriesControllerTests.cs | 6 | Category management |
| | MenusControllerTests.cs | 9 | Menu layouts |
| | AccountingGroupsControllerTests.cs | 6 | Tax grouping |
| **Payments** | PaymentsControllerTests.cs | 17 | Cash/card payments, refunds |
| | PaymentMethodsControllerTests.cs | 9 | Payment type configuration |
| | ReceiptsControllerTests.cs | 9 | Receipt generation |
| **Inventory** | IngredientsControllerTests.cs | 9 | Ingredient CRUD |
| | RecipesControllerTests.cs | 12 | Recipe management |
| | StockControllerTests.cs | 10 | Stock levels, FIFO consumption |
| | WasteControllerTests.cs | 6 | Waste tracking |
| **Costing** | RecipesControllerTests.cs | 22 | Recipe costing calculations |
| | IngredientPricesControllerTests.cs | 19 | Price tracking |
| | RecipeSnapshotsControllerTests.cs | 15 | Historical cost snapshots |
| | RecipeIngredientsControllerTests.cs | 17 | Recipe composition |
| | CostAlertsControllerTests.cs | 18 | Cost alert management |
| | CostingSettingsControllerTests.cs | 11 | Configuration |
| **Procurement** | PurchaseOrdersControllerTests.cs | 14 | PO management |
| | DeliveriesControllerTests.cs | 16 | Receiving/delivery |
| | SuppliersControllerTests.cs | 12 | Vendor management |
| **Hardware** | PosDevicesControllerTests.cs | 16 | Terminal devices |
| | PrintersControllerTests.cs | 11 | Printer configuration |
| | CashDrawersControllerTests.cs | 13 | Cash drawer management |
| **Location** | LocationsControllerTests.cs | 22 | Store management |
| | LocationSettingsControllerTests.cs | 14 | Configuration |
| | OperatingHoursControllerTests.cs | 17 | Hours management |
| **Reporting** | DailySalesControllerTests.cs | 9 | Sales reports |
| | CategoryMarginsControllerTests.cs | 10 | Category profitability |
| | ItemMarginsControllerTests.cs | 12 | Item margins |
| | MarginAlertsControllerTests.cs | 11 | Alert management |
| | MarginThresholdsControllerTests.cs | 15 | Alert thresholds |
| | StockConsumptionsControllerTests.cs | 14 | Usage reports |
| | SupplierAnalysisControllerTests.cs | 10 | Vendor metrics |

### Current Test Coverage Gaps

1. **No Cross-Service Integration Tests**: Each service is tested in isolation
2. **Limited End-to-End Workflow Tests**: Missing full business workflow coverage
3. **No Event-Driven Tests**: Kafka event publishing/consuming not tested
4. **Limited Concurrency Tests**: Race conditions not covered
5. **No Performance Tests**: Load and stress testing absent
6. **Limited Edge Case Coverage**: Happy path focused

---

## Part 2: Business Domain Scenarios

### Domain Analysis

The DarkVelocity POS system covers the following key business domains:

```
┌─────────────────────────────────────────────────────────────────┐
│                    RESTAURANT POS DOMAIN                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐        │
│  │   SALES     │───▶│   ORDERS    │───▶│  PAYMENTS   │        │
│  │  PERIODS    │    │             │    │             │        │
│  └─────────────┘    └──────┬──────┘    └──────┬──────┘        │
│                            │                  │                │
│                            ▼                  ▼                │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐        │
│  │    MENU     │    │  INVENTORY  │◀───│  RECEIPTS   │        │
│  │   ITEMS     │    │   STOCK     │    │             │        │
│  └─────────────┘    └──────┬──────┘    └─────────────┘        │
│                            │                                   │
│                            ▼                                   │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐        │
│  │  COSTING    │◀───│ PROCUREMENT │◀───│  SUPPLIERS  │        │
│  │  & PRICING  │    │     POs     │    │             │        │
│  └─────────────┘    └─────────────┘    └─────────────┘        │
│                            │                                   │
│                            ▼                                   │
│                    ┌─────────────┐                             │
│                    │  REPORTING  │                             │
│                    │  & MARGINS  │                             │
│                    └─────────────┘                             │
└─────────────────────────────────────────────────────────────────┘
```

---

## Part 3: Comprehensive Integration Test Plan

### Category 1: Sales Period & Cash Management

#### Scenario 1.1: Opening a New Sales Period
**Business Context**: A cashier starts their shift and opens the cash register.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `OpenSalesPeriod_CreatesOpenPeriod` | Create sales period with opening cash amount | P1 |
| `OpenSalesPeriod_OnlyOneOpenPeriodPerLocation` | Cannot open second period when one is already open | P1 |
| `OpenSalesPeriod_RecordsOpeningUser` | Audits who opened the period | P2 |
| `OpenSalesPeriod_EnablesOrderCreation` | Orders can only be created during open period | P1 |

#### Scenario 1.2: Closing a Sales Period
**Business Context**: End of shift, cashier counts the drawer and closes out.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `CloseSalesPeriod_RecordsClosingAmount` | Closing cash amount recorded | P1 |
| `CloseSalesPeriod_CalculatesDiscrepancy` | Expected vs actual cash difference | P1 |
| `CloseSalesPeriod_IncludesAllOrdersInPeriod` | All orders in period are included in totals | P1 |
| `CloseSalesPeriod_CannotCloseWithOpenOrders` | Must complete/void all orders first | P1 |
| `CloseSalesPeriod_GeneratesSummaryReport` | Daily sales summary generated | P2 |

#### Scenario 1.3: Cash Drawer Operations
**Business Context**: No Sale (open drawer), cash drops, pay-ins.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `CashDrop_ReducesDrawerBalance` | Safe drops reduce expected cash | P1 |
| `PayIn_IncreasesDrawerBalance` | Petty cash additions tracked | P2 |
| `NoSale_LogsDrawerOpening` | Audit trail for drawer opens without sales | P2 |
| `CashDrop_RequiresOpenSalesPeriod` | Cannot drop cash without open period | P1 |

---

### Category 2: Complete Order Lifecycle

#### Scenario 2.1: Order Creation and Building
**Business Context**: Server takes customer order, adds items to the ticket.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `CreateOrder_RequiresOpenSalesPeriod` | Cannot create order without active period | P1 |
| `CreateOrder_AssignsSequentialOrderNumber` | Order numbers sequential per location/day | P1 |
| `AddLine_FromMenuItemWithPrice` | Item price pulled from menu catalog | P1 |
| `AddLine_WithModifiers` | Add-ons and substitutions tracked | P2 |
| `AddLine_AppliesItemDiscount` | Percentage or fixed discount on line | P1 |
| `AddLine_CalculatesTaxCorrectly` | Tax calculation based on accounting group | P1 |
| `UpdateLine_RecalculatesOrderTotals` | Quantity changes update subtotal/tax/total | P1 |
| `RemoveLine_RecalculatesOrderTotals` | Removing items updates totals | P1 |
| `AddLine_TrackedItem_ChecksInventory` | Warns if inventory tracked item low | P2 |

#### Scenario 2.2: Order Modifications
**Business Context**: Customer changes their order after placing it.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `ModifyOrder_OnlyOpenStatus` | Cannot modify sent/completed orders | P1 |
| `ModifyOrder_UpdatesModifiedTimestamp` | Audit trail for changes | P2 |
| `ModifyOrder_AllowsAddingItemsToSentOrder` | Can add more items after sending | P2 |
| `SplitOrder_DividesLinesBetweenOrders` | Split check functionality | P2 |
| `MergeOrders_CombinesLines` | Combine multiple orders | P2 |

#### Scenario 2.3: Order Workflow State Transitions
**Business Context**: Order moves through kitchen to completion.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `SendOrder_TransitionsToSent` | Order status changes to 'sent' | P1 |
| `SendOrder_RequiresAtLeastOneLine` | Cannot send empty order | P1 |
| `SendOrder_RecordsSentTimestamp` | SentAt timestamp set | P1 |
| `SendOrder_PublishesKitchenEvent` | Event emitted for KDS | P1 |
| `CompleteOrder_TransitionsToCompleted` | Order marked as completed | P1 |
| `CompleteOrder_RequiresFullPayment` | Cannot complete with balance due | P1 |
| `CompleteOrder_RecordsCompletionTimestamp` | CompletedAt timestamp set | P1 |
| `VoidOrder_TransitionsToVoided` | Order can be voided | P1 |
| `VoidOrder_RequiresReason` | Void reason must be provided | P1 |
| `VoidOrder_VoidsAssociatedPayments` | Related payments also voided | P1 |
| `VoidOrder_RevertsInventoryConsumption` | Stock returned for tracked items | P2 |

#### Scenario 2.4: Order Discounts
**Business Context**: Manager applies discounts, happy hour pricing.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `ApplyOrderDiscount_Percentage` | 10% off entire order | P1 |
| `ApplyOrderDiscount_FixedAmount` | $5 off order | P1 |
| `ApplyOrderDiscount_RequiresAuthorization` | Manager override for large discounts | P2 |
| `ApplyOrderDiscount_RecalculatesTaxAfterDiscount` | Tax on discounted amount | P1 |
| `RemoveDiscount_RestoresOriginalTotals` | Discount removal updates totals | P1 |
| `MultipleDiscounts_AppliedInOrder` | Stacking discount rules | P3 |

---

### Category 3: Payment Processing

#### Scenario 3.1: Cash Payment Flow
**Business Context**: Customer pays with cash.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `CashPayment_FullPayment_CompletesOrder` | Full payment marks order complete | P1 |
| `CashPayment_CalculatesCorrectChange` | Change = Received - Total | P1 |
| `CashPayment_PartialPayment_KeepsOrderOpen` | Partial payment leaves balance | P1 |
| `CashPayment_OverPayment_AsTip` | Overpayment tracked as tip | P2 |
| `CashPayment_UpdatesCashDrawerExpected` | Drawer balance increases | P1 |
| `CashPayment_GeneratesReceipt` | Receipt created automatically | P1 |

#### Scenario 3.2: Card Payment Flow
**Business Context**: Customer pays with credit/debit card.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `CardPayment_CreatesPendingPayment` | Awaits terminal confirmation | P1 |
| `CardPayment_CompleteWithTerminalResponse` | Payment completed after approval | P1 |
| `CardPayment_FailedAuthorization_MarksDeclined` | Card declined handling | P1 |
| `CardPayment_WithTip_AddsTipToTotal` | Tip entry on terminal | P1 |
| `CardPayment_RecordsCardBrandAndLastFour` | Card details for receipt | P1 |
| `CardPayment_DoesNotAffectCashDrawer` | Drawer balance unchanged | P1 |

#### Scenario 3.3: Split Payments
**Business Context**: Customer pays with multiple payment methods.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `SplitPayment_CashThenCard` | Part cash, part card | P1 |
| `SplitPayment_MultipleCards` | Split between two cards | P2 |
| `SplitPayment_CompletesOnFullPayment` | Order completes when fully paid | P1 |
| `SplitPayment_PartialRefund` | Refund one of multiple payments | P2 |
| `SplitPayment_EqualSplit` | Divide evenly among N payments | P2 |

#### Scenario 3.4: Refunds and Voids
**Business Context**: Customer returns item, order voided.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `RefundCashPayment_DeductFromDrawer` | Cash refund reduces drawer | P1 |
| `RefundCardPayment_InitiatesRefundWithProcessor` | Card refund to terminal | P1 |
| `RefundPartial_RefundsSpecificAmount` | Partial refund for returned item | P1 |
| `VoidPayment_OnlyBeforeSettlement` | Can only void same-day | P2 |
| `RefundPayment_RequiresReason` | Refund reason tracked | P1 |
| `RefundPayment_OnlyCompletedPayments` | Cannot refund pending/voided | P1 |

---

### Category 4: Menu & Pricing Management

#### Scenario 4.1: Menu Item Management
**Business Context**: Adding/modifying items on the menu.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `CreateMenuItem_WithRecipeLink` | Item linked to recipe for costing | P1 |
| `CreateMenuItem_WithAccountingGroup` | Tax rate from accounting group | P1 |
| `UpdateMenuItemPrice_AffectsNewOrdersOnly` | Existing orders keep old price | P1 |
| `DeactivateMenuItem_ExcludesFromPOS` | Deactivated items not sellable | P1 |
| `DeactivateMenuItem_KeepsHistoricalData` | Old orders retain item info | P1 |
| `MenuItem_UniqueSkuPerLocation` | SKU uniqueness enforced | P1 |

#### Scenario 4.2: Menu Structure
**Business Context**: Organizing menu for POS display.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `MenuWithScreens_DisplaysCorrectLayout` | Menu screen organization | P2 |
| `MenuButton_LinksToMenuItem` | Button press adds correct item | P1 |
| `MenuButton_LinksToCategory` | Category button shows items | P2 |
| `DefaultMenu_SelectedOnPOSLoad` | Default menu loads automatically | P2 |
| `ChangeDefaultMenu_UpdatesSingleLocation` | Only one default per location | P1 |

#### Scenario 4.3: Pricing Tiers and Happy Hour
**Business Context**: Time-based or customer-based pricing.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `HappyHourPricing_AppliesDuringTimeWindow` | Reduced prices 4-6pm | P3 |
| `CustomerTypePricing_StaffDiscount` | Employee pricing tier | P3 |
| `PromotionalPricing_LimitedTimeOffer` | BOGO, combo deals | P3 |

---

### Category 5: Inventory & Stock Management

#### Scenario 5.1: Stock Receipt (From Procurement)
**Business Context**: Receiving goods from suppliers.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `ReceiveDelivery_CreatesStockBatches` | New batches from PO receipt | P1 |
| `ReceiveDelivery_UpdatesIngredientPrices` | New cost from invoice | P1 |
| `ReceiveDelivery_WithShortage_RecordsDiscrepancy` | Less received than ordered | P1 |
| `ReceiveDelivery_WithExpiry_SetsExpirationDate` | Batch expiry tracking | P1 |
| `ReceiveDelivery_UpdatesStockLevel` | Running total increases | P1 |

#### Scenario 5.2: Stock Consumption (From Orders)
**Business Context**: Selling items reduces inventory.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `CompleteOrder_ConsumesRecipeIngredients` | Stock decremented on sale | P1 |
| `ConsumeStock_FIFO_OldestFirst` | First-in-first-out depletion | P1 |
| `ConsumeStock_AcrossMultipleBatches` | Consumption spans batches | P1 |
| `ConsumeStock_BelowMinimum_TriggersAlert` | Low stock warning | P1 |
| `ConsumeStock_TracksConsumptionRecord` | Usage history maintained | P1 |
| `VoidOrder_ReversesConsumption` | Stock returned on void | P2 |

#### Scenario 5.3: Waste & Loss Tracking
**Business Context**: Spoilage, spillage, theft tracking.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `RecordWaste_DeductsFromStock` | Stock reduced for waste | P1 |
| `RecordWaste_RequiresReason` | Waste reason categorized | P1 |
| `RecordWaste_FromSpecificBatch` | Batch-level waste tracking | P2 |
| `RecordWaste_AffectsCostReporting` | Waste cost in reports | P2 |
| `RecordWaste_ByIngredient` | Raw ingredient waste | P1 |
| `RecordWaste_ByPreparedItem` | Prepared food waste | P2 |

#### Scenario 5.4: Inventory Adjustments
**Business Context**: Physical counts, corrections.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `AdjustStock_IncreasesQuantity` | Count found more than expected | P1 |
| `AdjustStock_DecreasesQuantity` | Count found less than expected | P1 |
| `AdjustStock_RequiresReason` | Adjustment reason required | P1 |
| `AdjustStock_AuditTrail` | Who adjusted and when | P1 |
| `StockTake_LocksInventoryDuringCount` | Prevent sales during count | P3 |

#### Scenario 5.5: Low Stock & Reorder
**Business Context**: Automated reorder suggestions.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `LowStock_BelowMinimumLevel_TriggersAlert` | Alert when below threshold | P1 |
| `LowStock_IncludesInLowStockReport` | Dashboard visibility | P1 |
| `LowStock_SuggestsReorderQuantity` | PAR level recommendations | P2 |
| `OutOfStock_ExcludesFromOrderWarning` | Cannot sell 86'd items | P2 |

---

### Category 6: Costing & Margin Analysis

#### Scenario 6.1: Recipe Cost Calculation
**Business Context**: Understanding true cost of menu items.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `CalculateRecipeCost_SumsIngredientCosts` | Total ingredient cost | P1 |
| `CalculateRecipeCost_IncludesWastePercentage` | Effective quantity with waste | P1 |
| `CalculateRecipeCost_CostPerPortion` | Divide by yield | P1 |
| `CalculateRecipeCost_MarginCalculation` | Price - Cost / Price | P1 |
| `RecalculateCost_WhenIngredientPriceChanges` | Auto-recalc on price update | P2 |

#### Scenario 6.2: Cost Snapshots
**Business Context**: Historical cost tracking for analysis.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `CreateCostSnapshot_RecordsCurrentCost` | Point-in-time cost capture | P1 |
| `CostSnapshot_OnDeliveryReceipt` | Snapshot when prices change | P2 |
| `CostSnapshot_CompareOverTime` | Cost trend analysis | P2 |
| `CostSnapshot_PerIngredient` | Ingredient-level history | P2 |

#### Scenario 6.3: Margin Monitoring
**Business Context**: Profitability tracking and alerts.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `MarginBelowThreshold_TriggersAlert` | Alert on low margin | P1 |
| `UpdateMarginThreshold_PerCategory` | Different thresholds per category | P2 |
| `MarginReport_ByMenuItem` | Item-level margin report | P1 |
| `MarginReport_ByCategory` | Category aggregate margins | P1 |
| `MarginReport_ByTimeperiod` | Weekly/monthly margin trends | P2 |

---

### Category 7: Procurement Workflow

#### Scenario 7.1: Purchase Order Lifecycle
**Business Context**: Ordering supplies from vendors.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `CreatePO_FromReorderSuggestions` | Auto-generate PO from low stock | P2 |
| `CreatePO_WithSupplierPricing` | Prices from supplier catalog | P1 |
| `SubmitPO_TransitionsToPending` | PO status workflow | P1 |
| `ApprovePO_RequiresAuthorization` | Manager approval for POs | P2 |
| `CancelPO_OnlyBeforeReceipt` | Cannot cancel received PO | P1 |
| `ReceivePO_PartialReceipt` | Receive in multiple deliveries | P1 |
| `ReceivePO_ClosesOnFullReceipt` | PO completed when fully received | P1 |

#### Scenario 7.2: Delivery Receipt
**Business Context**: Receiving and verifying deliveries.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `ReceiveDelivery_MatchesToPO` | Link delivery to purchase order | P1 |
| `ReceiveDelivery_RecordVariances` | Quantity/price discrepancies | P1 |
| `ReceiveDelivery_UpdatesInventory` | Stock levels increase | P1 |
| `ReceiveDelivery_UpdatesIngredientCosts` | New average cost calculation | P1 |
| `ReceiveDelivery_WithInvoiceMatching` | Match invoice to PO | P2 |

#### Scenario 7.3: Supplier Management
**Business Context**: Vendor performance tracking.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `SupplierAnalysis_OnTimeDeliveryRate` | Delivery performance metric | P2 |
| `SupplierAnalysis_PriceVariance` | Price consistency tracking | P2 |
| `SupplierAnalysis_QualityScore` | Rejection/return tracking | P3 |
| `DeactivateSupplier_ExcludesFromPOs` | Inactive suppliers not selectable | P1 |

---

### Category 8: Reporting & Analytics

#### Scenario 8.1: Sales Reporting
**Business Context**: Daily/weekly/monthly sales reports.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `DailySalesReport_AggregatesAllOrders` | Sum of completed orders | P1 |
| `DailySalesReport_ByPaymentMethod` | Cash vs card breakdown | P1 |
| `DailySalesReport_ByEmployee` | Sales by server | P2 |
| `DailySalesReport_ByMenuItem` | Top selling items | P1 |
| `DailySalesReport_ByCategory` | Category-level sales | P1 |
| `DailySalesReport_ByHour` | Hourly sales distribution | P2 |
| `DailySalesReport_IncludesDiscounts` | Discount totals | P1 |
| `DailySalesReport_IncludesVoids` | Void count and value | P1 |

#### Scenario 8.2: Inventory Reports
**Business Context**: Stock visibility and movement.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `InventoryValueReport_CurrentStockValue` | Total inventory at cost | P1 |
| `InventoryMovementReport_InOutSummary` | Stock in vs out | P2 |
| `ExpiringStockReport_ItemsNearExpiry` | Expiration warnings | P2 |
| `StockConsumptionReport_ByIngredient` | Usage by ingredient | P1 |
| `StockConsumptionReport_ByRecipe` | Usage by menu item | P2 |

#### Scenario 8.3: Financial Reports
**Business Context**: P&L and margin analysis.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `GrossMarginReport_ByMenuItem` | Item profitability | P1 |
| `GrossMarginReport_ByCategory` | Category profitability | P1 |
| `FoodCostPercentage_OverTime` | Cost trends | P2 |
| `WasteReport_CostImpact` | Waste cost analysis | P2 |

---

### Category 9: Multi-Location Scenarios

#### Scenario 9.1: Location Isolation
**Business Context**: Data separation between locations.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `LocationScoped_OrdersIsolated` | Location A cannot see Location B orders | P1 |
| `LocationScoped_InventoryIsolated` | Separate stock per location | P1 |
| `LocationScoped_MenuCanVary` | Different menus/prices per location | P1 |
| `LocationScoped_UserAccessControl` | User can only access assigned locations | P1 |

#### Scenario 9.2: Multi-Location Reporting
**Business Context**: Corporate rollup views.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `CorporateReport_AggregateAllLocations` | Company-wide sales | P2 |
| `CorporateReport_CompareLocations` | Location benchmarking | P3 |
| `CorporateReport_ConsolidatedInventory` | Total company stock | P3 |

---

### Category 10: Error Handling & Edge Cases

#### Scenario 10.1: Concurrent Operations
**Business Context**: Multiple users operating simultaneously.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `ConcurrentOrderModification_OptimisticLocking` | Two servers editing same order | P2 |
| `ConcurrentPayment_PreventDoublePayment` | Prevent duplicate charges | P1 |
| `ConcurrentStockConsumption_ConsistentDepletion` | Two orders consuming same stock | P2 |
| `ConcurrentSalesPeriodClose_OnlyOneSucceeds` | Race condition prevention | P2 |

#### Scenario 10.2: System Failures
**Business Context**: Graceful degradation.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `PaymentTerminalOffline_QueuePayments` | Offline payment handling | P2 |
| `DatabaseConnectionLost_TransactionRollback` | Data consistency on failure | P1 |
| `PartialDeliveryReceipt_RollbackOnError` | Atomic delivery processing | P2 |

#### Scenario 10.3: Data Validation
**Business Context**: Input validation and business rules.

| Test Case | Description | Priority |
|-----------|-------------|----------|
| `NegativeQuantity_Rejected` | Cannot add negative qty | P1 |
| `ZeroPrice_Rejected` | Price must be positive | P1 |
| `FutureDate_RejectedForSalesPeriod` | Cannot open period for future | P1 |
| `InvalidTaxRate_Rejected` | Tax rate 0-100% | P1 |
| `ExcessiveDiscount_RequiresOverride` | >50% discount needs manager | P2 |

---

## Part 4: Implementation Plan

### Phase 1: Core Business Workflows (High Priority)
**Estimated Test Count: 85 tests**

1. **Order-to-Payment Flow** (25 tests)
   - Complete order lifecycle with payment
   - Split payments
   - Refunds and voids

2. **Inventory Consumption Flow** (20 tests)
   - Stock consumption on order completion
   - FIFO batch depletion
   - Low stock alerts

3. **Sales Period Management** (15 tests)
   - Open/close periods
   - Cash reconciliation
   - Order constraints

4. **Basic Reporting** (25 tests)
   - Daily sales aggregation
   - Payment method breakdown
   - Margin calculations

### Phase 2: Procurement & Costing (Medium Priority)
**Estimated Test Count: 60 tests**

1. **Purchase Order Flow** (20 tests)
   - PO creation to receipt
   - Partial deliveries
   - Price variance handling

2. **Cost Management** (20 tests)
   - Recipe cost calculation
   - Cost snapshots
   - Margin monitoring

3. **Supplier Integration** (20 tests)
   - Supplier analysis
   - Price history
   - Delivery performance

### Phase 3: Advanced Features (Lower Priority)
**Estimated Test Count: 45 tests**

1. **Multi-Location** (15 tests)
   - Location isolation
   - Corporate reporting

2. **Concurrency & Resilience** (15 tests)
   - Race condition handling
   - Error recovery

3. **Advanced Pricing** (15 tests)
   - Happy hour
   - Promotional pricing
   - Customer tiers

---

## Part 5: Test Infrastructure Requirements

### New Test Projects

```
tests/
├── Integration.Tests/              # New cross-service integration tests
│   ├── Fixtures/
│   │   ├── IntegrationTestFixture.cs
│   │   └── MultiServiceFixture.cs
│   ├── OrderToPayment/
│   │   └── OrderToPaymentFlowTests.cs
│   ├── InventoryConsumption/
│   │   └── StockConsumptionFlowTests.cs
│   ├── Procurement/
│   │   └── PurchaseOrderFlowTests.cs
│   └── Reporting/
│       └── SalesReportingFlowTests.cs
```

### Test Fixture Design

```csharp
public class IntegrationTestFixture : IAsyncLifetime
{
    // Multiple service clients
    public HttpClient OrdersClient { get; }
    public HttpClient PaymentsClient { get; }
    public HttpClient InventoryClient { get; }
    public HttpClient MenuClient { get; }

    // Shared test data
    public Guid TestLocationId { get; }
    public Guid TestUserId { get; }
    public Guid TestSalesPeriodId { get; }

    // Event bus for inter-service events
    public ITestEventBus EventBus { get; }
}
```

### Test Data Management

- Seed realistic test data representing a real restaurant
- Include multiple menu items with recipes
- Pre-existing stock batches with varied ages
- Configured suppliers and pricing

---

## Part 6: Priority Summary

### P1 - Critical (Must Have)
- Order lifecycle tests
- Payment processing tests
- Stock consumption tests
- Sales period management
- Basic reporting

### P2 - Important (Should Have)
- Procurement workflows
- Costing and margins
- Multi-location isolation
- Concurrency handling

### P3 - Nice to Have
- Advanced pricing tiers
- Corporate rollup reports
- Performance testing

---

## Appendix: Test Naming Convention

```
{Feature}_{Scenario}_{ExpectedOutcome}

Examples:
- CreateOrder_WithValidSalesPeriod_ReturnsCreated
- CompleteOrder_WithoutFullPayment_ReturnsBadRequest
- ConsumeStock_AcrossMultipleBatches_DepletesInFifoOrder
- ClosesSalesPeriod_WithCashDiscrepancy_RecordsVariance
```

---

*Document Version: 1.0*
*Created: 2026-01-29*
*Status: Draft for Review*
