# Test Coverage Documentation

This document provides a comprehensive inventory of all tests in the DarkVelocity POS codebase, organized by domain and test type.

## Test Infrastructure

### Fixtures

| Fixture | File | Purpose |
|---------|------|---------|
| `TestClusterFixture` | `TestClusterFixture.cs` | Orleans test cluster with in-memory grain storage, memory streams, and payment gateway services |
| `ApiTestFixture` | `ApiTestFixture.cs` | WebApplicationFactory for HTTP endpoint integration tests |

### Collections

| Collection | Fixture | Usage |
|------------|---------|-------|
| `ClusterCollection` | `TestClusterFixture` | All Orleans grain tests |
| `ApiCollection` | `ApiTestFixture` | All HTTP API integration tests |

### Testing Libraries

- **xUnit** - Test framework
- **FluentAssertions** - BDD-style assertions
- **Orleans.TestingHost** - Orleans grain testing infrastructure

---

## Grain Tests

### Accounting & Financial

#### AccountGrainTests (56 tests)

Tests the general ledger account functionality for financial tracking.

| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesAccount` | Creates asset, expense, revenue, and liability accounts |
| `PostDebitAsync_PostsDebitEntry` | Posts debit entries with amounts and descriptions |
| `PostCreditAsync_PostsCreditEntry` | Posts credit entries with amounts and descriptions |
| `AdjustBalanceAsync_AdjustsBalance` | Adjusts account balances with audit trail |
| `ReverseEntryAsync_ReversesEntry` | Reverses previous journal entries |
| `GetBalanceAsync_ReturnsCurrentBalance` | Retrieves current account balance |
| `GetSummaryAsync_ReturnsSummary` | Returns account summary with transaction counts |
| `GetRecentEntriesAsync_ReturnsEntries` | Retrieves recent journal entries |
| `ClosePeriodAsync_ClosesPeriod` | Closes accounting period |
| `ActivateAsync/DeactivateAsync_ChangesStatus` | Account lifecycle management |
| `UpdateAsync_UpdatesProperties` | Updates account metadata |

#### AccountingStreamTests (6 tests)

Tests event streaming for accounting-related events.

| Test | Scenario |
|------|----------|
| `GiftCardActivatedEvent_Published` | Publishes event when gift card is activated |
| `GiftCardRedeemedEvent_Published` | Publishes event when gift card is redeemed |
| `GiftCardExpiredEvent_Published` | Publishes event when gift card expires |
| `CustomerSpendRecordedEvent_Published` | Publishes event when customer spend is recorded |
| `CustomerTierChangedEvent_Published` | Publishes event when customer tier changes |
| `LoyaltyPointsRedeemedEvent_Published` | Publishes event when loyalty points are redeemed |

#### DailySalesGrainTests (8 tests)

Tests daily sales aggregation and metrics.

| Test | Scenario |
|------|----------|
| `InitializeAsync_CreatesSalesRecord` | Initializes daily sales record for a site |
| `RecordSaleAsync_RecordsSale` | Records individual sales transactions |
| `RecordSaleAsync_MultiChannel` | Tracks sales by channel (DineIn, TakeOut, Delivery) |
| `RecordSaleAsync_WithDiscounts` | Handles discounts and comps |
| `GetMetricsAsync_ReturnsMetrics` | Returns daily sales metrics |
| `GetGrossProfitMetricsAsync_CalculatesProfit` | Calculates gross profit with COGS tracking |
| `GetFactsAsync_ReturnsFactData` | Returns fact data for reporting |
| `FinalizeAsync_FinalizesDay` | Finalizes daily sales period |

---

### Booking & Reservations

#### BookingGrainTests (40+ tests)

Tests restaurant booking and reservation management.

| Test | Scenario |
|------|----------|
| `RequestAsync_CreatesBooking` | Creates booking request with guest info |
| `ConfirmAsync_ConfirmsBooking` | Confirms pending booking with time slot |
| `ModifyAsync_ModifiesBooking` | Updates booking details (time, party size) |
| `CancelAsync_CancelsBooking` | Cancels booking with reason tracking |
| `AssignTableAsync_AssignsTable` | Assigns table to confirmed booking |
| `RecordArrivalAsync_RecordsArrival` | Records guest arrival (check-in) |
| `SeatGuestAsync_SeatsGuest` | Seats guest at assigned table |
| `RecordDepartureAsync_RecordsDeparture` | Records guest departure |
| `RecordNoShowAsync_RecordsNoShow` | Marks booking as no-show |
| `RequireDepositAsync_SetsDeposit` | Sets deposit requirement |
| `RecordDepositAsync_RecordsPayment` | Records deposit payment |

#### WaitlistGrainTests

Tests waitlist management for walk-in guests.

| Test | Scenario |
|------|----------|
| `CreateEntryAsync_AddsToWaitlist` | Adds guest to waitlist with estimated wait |
| `GetPositionAsync_ReturnsPosition` | Returns current position in queue |
| `NotifyAsync_SendsNotification` | Sends table-ready notification |
| `SeatAsync_SeatsFromWaitlist` | Seats guest from waitlist |
| `RemoveAsync_RemovesFromWaitlist` | Removes guest who left |
| `ConvertToBookingAsync_CreatesBooking` | Converts waitlist entry to booking |

#### BookingSettingsGrainTests (12 tests)

Tests booking configuration and availability.

| Test | Scenario |
|------|----------|
| `InitializeAsync_SetsDefaults` | Initializes default booking settings |
| `UpdateSettingsAsync_UpdatesSettings` | Updates booking configuration |
| `GetAvailabilityAsync_ReturnsSlots` | Returns available time slots |
| `BlockDateAsync_BlocksDate` | Blocks specific dates from booking |
| `GetSlotIntervalAsync_ReturnsInterval` | Returns booking slot intervals |
| `CalculateDepositAsync_CalculatesDeposit` | Calculates deposit based on party size |
| `ValidateBookingWindowAsync_ValidatesWindow` | Validates advance booking window |

#### TableGrainTests (5 tests)

Tests table management.

| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesTable` | Creates table with number and capacity |
| `ChangeStatusAsync_ChangesStatus` | Changes table status (Available, Occupied, Reserved) |
| `GetAvailabilityAsync_ReturnsAvailability` | Returns table availability |
| `UpdatePositionAsync_UpdatesPosition` | Updates table floor plan position |
| `UpdateCapacityAsync_UpdatesCapacity` | Updates table seating capacity |

#### FloorPlanGrainTests (5 tests)

Tests floor plan management.

| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesFloorPlan` | Creates floor plan with name and dimensions |
| `GetTableCountAsync_ReturnsCount` | Returns table count for floor |
| `UpdatePropertiesAsync_UpdatesProperties` | Updates floor plan properties |
| `SetDimensionsAsync_SetsDimensions` | Sets grid dimensions |
| `ActivateAsync/DeactivateAsync_ChangesStatus` | Floor plan lifecycle |

---

### Customer Management

#### CustomerGrainTests (59 tests)

Comprehensive customer profile and loyalty management.

**Creation & Profile**
| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesCustomer` | Creates customer with contact info |
| `UpdateAsync_UpdatesProfile` | Updates customer profile |
| `ChangeStatusAsync_ChangesStatus` | Changes customer status |

**Loyalty**
| Test | Scenario |
|------|----------|
| `EnrollInLoyaltyAsync_Enrolls` | Enrolls customer in loyalty program |
| `EarnPointsAsync_EarnsPoints` | Awards loyalty points |
| `RedeemPointsAsync_RedeemsPoints` | Redeems loyalty points |
| `IssueRewardAsync_IssuesReward` | Issues loyalty reward |
| `RedeemRewardAsync_RedeemsReward` | Redeems loyalty reward |
| `PromoteTierAsync_PromotesTier` | Promotes customer to higher tier |

**Visit History (6 tests)**
| Test | Scenario |
|------|----------|
| `RecordVisitAsync_RecordsVisit` | Records customer visit |
| `GetVisitHistoryAsync_ReturnsHistory` | Retrieves visit history |
| `GetVisitHistoryAsync_FiltersBySite` | Filters visits by site |
| `GetLastVisitAsync_ReturnsLastVisit` | Returns most recent visit |
| `GetVisitHistoryAsync_LimitsResults` | Limits result count |
| `RecordVisitAsync_TracksDetails` | Tracks detailed visit data |

**Preferences (12 tests)**
| Test | Scenario |
|------|----------|
| `AddDietaryRestrictionAsync_Adds` | Adds dietary restriction |
| `RemoveDietaryRestrictionAsync_Removes` | Removes dietary restriction |
| `AddAllergenAsync_Adds` | Adds allergen |
| `RemoveAllergenAsync_Removes` | Removes allergen |
| `SetSeatingPreferenceAsync_Sets` | Sets seating preference |
| `UpdatePreferencesAsync_Updates` | Updates multiple preferences |

**Tags & Notes**
| Test | Scenario |
|------|----------|
| `AddTagAsync_AddsTag` | Adds customer tag |
| `RemoveTagAsync_RemovesTag` | Removes customer tag |
| `AddTagAsync_PreventsDuplicates` | Prevents duplicate tags |
| `AddNoteAsync_AddsNote` | Adds note with staff tracking |

**Referrals & Merge**
| Test | Scenario |
|------|----------|
| `SetReferralCodeAsync_SetsCode` | Sets referral code |
| `TrackReferrerAsync_TracksReferrer` | Tracks who referred customer |
| `IncrementReferralCountAsync_Increments` | Increments referral count |
| `MergeAsync_MergesCustomers` | Merges duplicate customer records |
| `AnonymizeAsync_AnonymizesData` | GDPR anonymization |

#### CustomerSpendProjectionGrainTests (11 tests)

Tests customer spending tracking and tier management.

| Test | Scenario |
|------|----------|
| `InitializeAsync_SetsDefaultTiers` | Initializes Bronze, Silver, Gold tiers |
| `RecordSpendAsync_AccumulatesSpend` | Accumulates customer spend |
| `RecordSpendAsync_PromotesTier` | Promotes tier on spend threshold |
| `RedeemPointsAsync_RedeemsPoints` | Redeems accumulated points |
| `ReverseSpendAsync_ReversesSpend` | Reverses spend with potential demotion |
| `ConfigureTiersAsync_ConfiguresTiers` | Configures tier thresholds and multipliers |
| `GetSnapshotAsync_ReturnsSnapshot` | Returns current spend snapshot |
| `HasSufficientPointsAsync_ChecksBalance` | Checks if sufficient points for redemption |
| `RecordSpendAsync_AppliesMultiplier` | Applies tier earning multiplier |

---

### Inventory & Costing

#### InventoryGrainTests

Tests inventory tracking with batch management.

| Test | Scenario |
|------|----------|
| `InitializeAsync_CreatesInventory` | Initializes inventory item |
| `ReceiveBatchAsync_AddsBatch` | Receives inventory batch with cost |
| `ConsumeAsync_ConsumesWithFIFO` | Consumes inventory using FIFO |
| `GetLevelInfoAsync_ReturnsLevels` | Returns current stock levels |
| `GetActiveBatchesAsync_ReturnsBatches` | Returns active batches |
| `CalculateWACAsync_CalculatesWeightedAverage` | Calculates weighted average cost |

#### CostingGrainTests (10 tests)

**RecipeGrain Tests**
| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesRecipe` | Creates recipe with name and yield |
| `AddIngredientAsync_AddsIngredient` | Adds ingredient with quantity |
| `CalculateCostAsync_CalculatesCost` | Calculates recipe cost from ingredients |
| `GetCostSnapshotAsync_ReturnsSnapshot` | Returns cost snapshot |
| `GetCostHistoryAsync_ReturnsHistory` | Returns cost history over time |

**IngredientPriceGrain Tests**
| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesPrice` | Creates ingredient price record |
| `UpdatePriceAsync_UpdatesPrice` | Updates ingredient price |
| `GetPriceHistoryAsync_ReturnsHistory` | Returns price history |
| `GetPriceChangePercentAsync_ReturnsPercent` | Calculates price change percentage |

**CostAlertGrain Tests**
| Test | Scenario |
|------|----------|
| `CreateAlertAsync_CreatesAlert` | Creates cost alert for thresholds |
| `AcknowledgeAsync_AcknowledgesAlert` | Acknowledges alert with action |

#### CostingPolicyTests (11 tests - Unit Tests)

Tests costing calculation policies.

| Test | Scenario |
|------|----------|
| `FifoCostingPolicy_ConsumesOldestFirst` | FIFO consumes oldest batches first |
| `FifoCostingPolicy_HandlesExhaustedBatches` | Handles exhausted batches |
| `FifoCostingPolicy_InsufficientStock` | Handles insufficient stock |
| `LifoCostingPolicy_ConsumesNewestFirst` | LIFO consumes newest batches first |
| `WeightedAverageCostingPolicy_UsesWeightedAverage` | Calculates weighted average cost |
| `StandardCostingPolicy_UsesStandardCost` | Uses configured standard cost |
| `CostingPolicyFactory_CreatesCorrectPolicy` | Factory creates correct policy type |

#### AlertGrainTests (9 tests)

Tests inventory alert management.

| Test | Scenario |
|------|----------|
| `InitializeAsync_SetsRules` | Initializes alert rules |
| `CreateAlertAsync_CreatesAlert` | Creates LowStock, OutOfStock, ExpiryRisk alerts |
| `GetActiveAlertsAsync_ReturnsActive` | Returns active alerts |
| `AcknowledgeAsync_AcknowledgesAlert` | Acknowledges alert |
| `ResolveAsync_ResolvesAlert` | Resolves alert |
| `SnoozeAsync_SnoozesAlert` | Snoozes alert temporarily |
| `DismissAsync_DismissesAlert` | Dismisses alert |
| `UpdateRuleAsync_UpdatesRule` | Updates alert rule configuration |

---

### Reporting & Analytics

#### ReportingGrainTests

**DailyInventorySnapshotGrain (5 tests)**
| Test | Scenario |
|------|----------|
| `InitializeAsync_CreatesSnapshot` | Creates daily inventory snapshot |
| `RecordIngredientSnapshotAsync_Records` | Records ingredient snapshot |
| `RecordIngredientSnapshotAsync_TracksLowStock` | Tracks low stock count |
| `RecordIngredientSnapshotAsync_TracksExpiringSoon` | Tracks expiring items |
| `GetHealthMetricsAsync_ReturnsMetrics` | Returns stock health metrics |

**DailyConsumptionGrain (4 tests)**
| Test | Scenario |
|------|----------|
| `InitializeAsync_CreatesRecord` | Creates consumption record |
| `RecordConsumptionAsync_RecordsTheoretical` | Records theoretical vs actual consumption |
| `RecordConsumptionAsync_Aggregates` | Aggregates multiple consumptions |
| `GetVarianceBreakdownAsync_ReturnsVariances` | Returns top variances |

**DailyWasteGrain (4 tests)**
| Test | Scenario |
|------|----------|
| `InitializeAsync_CreatesRecord` | Creates waste record |
| `RecordWasteAsync_RecordsSpoilage` | Records spoilage waste |
| `RecordWasteAsync_AggregatesByReason` | Aggregates by reason (Spoilage, Breakage) |
| `RecordWasteAsync_AggregatesByCategory` | Aggregates by category |

**PeriodAggregationGrain (3 tests)**
| Test | Scenario |
|------|----------|
| `InitializeAsync_CreatesWeeklyPeriod` | Creates weekly aggregation |
| `InitializeAsync_CreatesFourWeekPeriod` | Creates 4-week period |
| `AggregateFromDailyAsync_Aggregates` | Aggregates daily data |

**SiteDashboardGrain (4 tests)**
| Test | Scenario |
|------|----------|
| `InitializeAsync_CreatesDashboard` | Creates site dashboard |
| `GetMetricsAsync_ReturnsMetrics` | Returns dashboard metrics |
| `RefreshAsync_RefreshesData` | Refreshes dashboard data |
| `GetTopVariancesAsync_ReturnsVariances` | Returns top variance items |

---

### Order & Payment

#### OrderGrainTests

Tests order management.

| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesOrder` | Creates order with type and guest count |
| `GetStateAsync_ReturnsState` | Returns order state |
| `AddLineAsync_AddsLine` | Adds line item with modifiers |
| `GetLinesAsync_ReturnsLines` | Returns order lines |
| `UpdateLineAsync_UpdatesLine` | Updates line quantity/modifiers |
| `RemoveLineAsync_RemovesLine` | Removes line item |
| `AssignTableAsync_AssignsTable` | Assigns table to order |

#### PaymentGrainTests

Tests payment processing.

| Test | Scenario |
|------|----------|
| `InitiateAsync_InitiatesPayment` | Initiates payment with method and amount |
| `CompleteCashAsync_CompletesCashPayment` | Completes cash payment with change |
| `CompleteCardAsync_CompletesCardPayment` | Completes card payment with gateway info |
| `ProcessCardPaymentCommand_ProcessesCard` | Processes card through gateway |
| `VoidAsync_VoidsPayment` | Voids pending payment |
| `RefundAsync_RefundsPayment` | Refunds completed payment |

#### GiftCardGrainTests

Tests gift card management.

| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesGiftCard` | Creates physical or digital card |
| `ActivateAsync_ActivatesCard` | Activates card with initial balance |
| `RedeemAsync_RedeemsValue` | Redeems card value |
| `GetBalanceAsync_ReturnsBalance` | Returns current balance |
| `ExpireAsync_ExpiresCard` | Expires gift card |

---

### Menu Management

#### MenuGrainTests

**MenuCategoryGrain Tests**
| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesCategory` | Creates menu category |
| `UpdateAsync_UpdatesCategory` | Updates category properties |
| `DeactivateAsync_DeactivatesCategory` | Deactivates category |
| `ActivateAsync_ActivatesCategory` | Reactivates category |
| `GetItemCountAsync_ReturnsCount` | Returns item count |

**MenuItemGrain Tests**
| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesItem` | Creates menu item with pricing |
| `UpdateAsync_UpdatesItem` | Updates item properties |
| `AddModifierGroupAsync_AddsModifiers` | Adds modifier groups |
| `SetAvailabilityAsync_SetsAvailability` | Sets item availability |

---

### Employee & Labor

#### EmployeeGrainTests (9 tests)

Tests employee management.

| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesEmployee` | Creates employee record |
| `UpdateAsync_UpdatesEmployee` | Updates employee info |
| `ClockInAsync_ClocksIn` | Records clock-in time |
| `ClockOutAsync_ClocksOut` | Records clock-out time |
| `AssignRoleAsync_AssignsRole` | Assigns role with override rates |
| `TerminateAsync_TerminatesEmployee` | Terminates employment |
| `GrantSiteAccessAsync_GrantsAccess` | Grants site access |
| `RevokeSiteAccessAsync_RevokesAccess` | Revokes site access |
| `SyncFromUserAsync_SyncsUser` | Syncs from user grain |

#### LaborExtendedGrainTests (11 tests)

**EmployeeAvailabilityGrain (3 tests)**
| Test | Scenario |
|------|----------|
| `InitializeAsync_CreatesEmptyAvailability` | Initializes empty availability |
| `SetAvailabilityAsync_AddsEntry` | Sets availability for day/time |
| `IsAvailableOnAsync_ReturnsCorrectValue` | Checks availability |

**ShiftSwapGrain (3 tests)**
| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesRequest` | Creates swap/drop/pickup request |
| `ApproveAsync_ApprovesRequest` | Approves shift swap |
| `CancelAsync_CancelsRequest` | Cancels pending request |

**TimeOffGrain (5 tests)**
| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesRequest` | Creates vacation, sick, personal time off |
| `CreateAsync_UnpaidLeave_MarksUnpaid` | Marks unpaid leave correctly |
| `ApproveAsync_ApprovesRequest` | Approves time off |
| `RejectAsync_RejectsRequest` | Rejects time off with reason |
| `CalculateTotalDays_CalculatesCorrectly` | Calculates total days |

---

### Procurement

#### ProcurementGrainTests

**SupplierGrain (12 tests)**
| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesSupplier` | Creates supplier with contact info |
| `UpdateAsync_UpdatesSupplier` | Updates supplier details |
| `AddIngredientAsync_AddsToCatalog` | Adds ingredient to supplier catalog |
| `AddIngredientAsync_MultipleIngredients` | Adds multiple ingredients |
| `AddIngredientAsync_UpdatesExisting` | Updates existing ingredient |
| `RemoveIngredientAsync_Removes` | Removes ingredient from catalog |
| `UpdateIngredientPriceAsync_UpdatesPrice` | Updates ingredient price |
| `GetIngredientPriceAsync_ReturnsPrice` | Gets ingredient price |
| `RecordPurchaseAsync_UpdatesMetrics` | Updates YTD purchases and delivery metrics |
| `UpdateAsync_Deactivate_Deactivates` | Deactivates supplier |

**PurchaseOrderGrain (14 tests)**
| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesDraft` | Creates draft purchase order |
| `AddLineAsync_AddsLine` | Adds line item |
| `AddLineAsync_MultipleLines_CalculatesTotal` | Calculates order total |
| `UpdateLineAsync_UpdatesLine` | Updates line quantity/price |
| `RemoveLineAsync_RemovesLine` | Removes line item |
| `SubmitAsync_SubmitsOrder` | Submits order to supplier |
| `ReceiveLineAsync_PartialReceive` | Handles partial receipt |
| `ReceiveLineAsync_FullReceive` | Marks fully received |
| `ReceiveLineAsync_IncrementalReceive` | Accumulates incremental receipts |
| `CancelAsync_CancelsOrder` | Cancels order with reason |
| `GetTotalAsync_ReturnsTotal` | Returns order total |

**DeliveryGrain (12 tests)**
| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesDelivery` | Creates delivery record |
| `CreateAsync_WithoutPO_AllowsDirect` | Allows direct delivery without PO |
| `AddLineAsync_AddsLine` | Adds delivery line |
| `AddLineAsync_MultipleLines_AccumulatesTotal` | Accumulates total value |
| `RecordDiscrepancyAsync_ShortDelivery` | Records short delivery |
| `RecordDiscrepancyAsync_DamagedGoods` | Records damaged goods |
| `RecordDiscrepancyAsync_MultipleDiscrepancies` | Records multiple issues |
| `AcceptAsync_AcceptsDelivery` | Accepts delivery |
| `AcceptAsync_WithDiscrepancies_StillAccepts` | Accepts with noted discrepancies |
| `RejectAsync_RejectsDelivery` | Rejects delivery with reason |
| `AddLineAsync_WithBatchAndExpiry_TracksBatch` | Tracks batch and expiry |

---

### Loyalty Program

#### LoyaltyProgramGrainTests (28 tests)

Tests loyalty program configuration and management.

**Program Lifecycle**
| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesProgram` | Creates loyalty program |
| `UpdateAsync_UpdatesProgram` | Updates program details |
| `ActivateAsync_ActivatesProgram` | Activates program |
| `ActivateAsync_WithoutRules_Throws` | Requires earning rules |
| `ActivateAsync_WithoutTiers_Throws` | Requires at least one tier |
| `PauseAsync_PausesProgram` | Pauses active program |

**Earning Rules**
| Test | Scenario |
|------|----------|
| `AddEarningRuleAsync_AddsRule` | Adds points-per-dollar rule |
| `AddEarningRuleAsync_BonusDay` | Adds bonus day multiplier |
| `UpdateEarningRuleAsync_DeactivatesRule` | Deactivates earning rule |

**Tiers**
| Test | Scenario |
|------|----------|
| `AddTierAsync_AddsTier` | Adds tier with benefits |
| `AddTierAsync_DuplicateLevel_Throws` | Prevents duplicate tier levels |
| `GetNextTierAsync_ReturnsNextTier` | Returns next tier for progression |
| `GetNextTierAsync_AtMaxTier_ReturnsNull` | Returns null at max tier |

**Rewards**
| Test | Scenario |
|------|----------|
| `AddRewardAsync_AddsReward` | Adds redemption reward |
| `GetAvailableRewardsAsync_FiltersByTier` | Filters rewards by tier |

**Points Calculation**
| Test | Scenario |
|------|----------|
| `CalculatePointsAsync_CalculatesBasePoints` | Calculates base points |
| `CalculatePointsAsync_WithTierMultiplier` | Applies tier multiplier |
| `CalculatePointsAsync_WithBonusDay` | Applies bonus day multiplier |

**Program Configuration**
| Test | Scenario |
|------|----------|
| `ConfigurePointsExpiryAsync_SetsExpiry` | Configures points expiration |
| `ConfigureReferralProgramAsync_SetsReferral` | Configures referral program |
| `SetTermsAndConditionsAsync_SetsTerms` | Sets terms and conditions |

**Metrics**
| Test | Scenario |
|------|----------|
| `IncrementEnrollmentsAsync_IncrementsCounters` | Tracks enrollments |
| `RecordPointsIssuedAsync_TracksPoints` | Tracks issued points |
| `RecordPointsRedeemedAsync_TracksRedemptions` | Tracks redemptions |

---

### Organization & Site

#### OrganizationGrainTests

| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesOrganization` | Creates organization with slug |
| `CreateAsync_DuplicateSlug_Throws` | Prevents duplicate slugs |
| `GetStateAsync_ReturnsState` | Returns organization state |

#### SiteGrainTests

| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesSite` | Creates site with address |
| `RegisterWithOrgAsync_Registers` | Registers site with organization |
| `UpdateAsync_UpdatesSite` | Updates site properties |

#### UserGrainTests

| Test | Scenario |
|------|----------|
| `CreateAsync_CreatesUser` | Creates user with type |
| `GetStateAsync_ReturnsState` | Returns user state |
| `UpdateAsync_UpdatesUser` | Updates user properties |

---

### Services

#### CardValidationServiceTests (15 tests)

Tests credit card validation utilities.

| Test | Scenario |
|------|----------|
| `ValidateCardNumber_ValidCard_ReturnsTrue` | Validates card with Luhn algorithm |
| `ValidateCardNumber_InvalidCard_ReturnsFalse` | Rejects invalid card numbers |
| `ValidateCardNumber_FormattedNumber_Validates` | Handles spaces and dashes |
| `GetCardBrand_Visa_ReturnsVisa` | Detects Visa cards |
| `GetCardBrand_Mastercard_ReturnsMastercard` | Detects Mastercard |
| `GetCardBrand_Amex_ReturnsAmex` | Detects American Express |
| `GetCardBrand_Discover_ReturnsDiscover` | Detects Discover |
| `GenerateFingerprint_GeneratesHash` | Generates card fingerprint |
| `MaskCardNumber_MasksMiddle` | Masks card number (****1234) |
| `GetLast4_ReturnsLastFour` | Extracts last 4 digits |
| `ValidateExpiry_FutureDate_ReturnsTrue` | Validates future expiry |
| `ValidateExpiry_PastDate_ReturnsFalse` | Rejects expired cards |
| `ValidateCvc_ValidCvc_ReturnsTrue` | Validates CVC format |
| `ValidateCvc_InvalidCvc_ReturnsFalse` | Rejects invalid CVC |
| `DetectFundingType_DetectsType` | Detects credit vs debit |

---

## API Integration Tests

All API tests use HAL+JSON responses with `_links` and `_embedded` properties.

### HealthApiTests (1 test)

| Test | Scenario |
|------|----------|
| `GetHealth_ReturnsOk` | GET /health returns 200 with "healthy" status |

### OrganizationApiTests

| Test | Scenario |
|------|----------|
| `CreateOrganization_ReturnsCreated` | POST /api/orgs returns 201 |
| `GetOrganization_ReturnsOk` | GET /api/orgs/{id} returns 200 |

### SiteApiTests

| Test | Scenario |
|------|----------|
| `CreateSite_ReturnsCreated` | POST /api/orgs/{orgId}/sites returns 201 |
| `GetSite_ReturnsOk` | GET /api/orgs/{orgId}/sites/{id} returns 200 |

### OrderApiTests

| Test | Scenario |
|------|----------|
| `CreateOrder_ReturnsCreated` | POST /api/orgs/{orgId}/sites/{siteId}/orders returns 201 |
| `GetOrder_ReturnsOk` | GET /api/orgs/{orgId}/sites/{siteId}/orders/{id} returns 200 with HAL links |

### MenuApiTests

| Test | Scenario |
|------|----------|
| `CreateCategory_ReturnsCreated` | POST returns 201 with HAL response |
| `GetCategory_ReturnsOk` | GET returns 200 with HAL links |

### BookingApiTests (8 tests)

| Test | Scenario |
|------|----------|
| `RequestBooking_ReturnsCreated` | Creates booking with confirmation code |
| `GetBooking_WhenExists_ReturnsOk` | Returns booking with HAL links |
| `GetBooking_WhenNotExists_ReturnsNotFound` | Returns 404 with error |
| `ConfirmBooking_ReturnsOk` | Confirms pending booking |
| `CancelBooking_ReturnsOk` | Cancels booking |
| `CheckInGuest_ReturnsOk` | Records guest arrival |
| `SeatGuest_ReturnsOk` | Seats guest at table |

### PaymentApiTests (8 tests)

| Test | Scenario |
|------|----------|
| `InitiatePayment_ReturnsCreated` | Creates payment with complete links |
| `GetPayment_WhenExists_ReturnsOk` | Returns payment with HAL links |
| `GetPayment_WhenNotExists_ReturnsNotFound` | Returns 404 with error |
| `CompleteCashPayment_ReturnsOk` | Completes cash payment |
| `CompleteCardPayment_ReturnsOk` | Completes card payment |
| `VoidPayment_ReturnsOk` | Voids pending payment |
| `RefundPayment_ReturnsOk` | Refunds completed payment |

### CustomerApiTests (9 tests)

| Test | Scenario |
|------|----------|
| `CreateCustomer_ReturnsCreated` | Creates customer with HAL links |
| `GetCustomer_WhenExists_ReturnsOk` | Returns customer with loyalty/rewards links |
| `GetCustomer_WhenNotExists_ReturnsNotFound` | Returns 404 with error |
| `UpdateCustomer_ReturnsOk` | Updates customer with PATCH |
| `EnrollInLoyalty_ReturnsOk` | Enrolls in loyalty program |
| `EarnPoints_ReturnsOk` | Earns loyalty points |
| `RedeemPoints_ReturnsOk` | Redeems loyalty points |
| `GetRewards_ReturnsHalCollection` | Returns rewards as HAL collection |

### EmployeeApiTests

| Test | Scenario |
|------|----------|
| `CreateEmployee_ReturnsCreated` | Creates employee |
| `GetEmployee_ReturnsOk` | Returns employee |

### InventoryApiTests

| Test | Scenario |
|------|----------|
| `CreateInventoryItem_ReturnsCreated` | Creates inventory item |
| `GetInventoryItem_ReturnsOk` | Returns inventory item |

---

## Integration & Edge Case Tests

### StateTransitionValidationTests (45 tests)

Tests invalid state transitions across domain grains to prevent invalid workflow paths.

**Booking State Transitions**
| Test | Scenario |
|------|----------|
| `ModifyAsync_AfterArrival_ShouldFail` | Cannot modify after guest arrival |
| `CancelAsync_AfterSeated_ShouldFail` | Cannot cancel after guest is seated |
| `ConfirmAsync_AlreadyConfirmed_ShouldFail` | Cannot confirm twice |
| `SeatAsync_WithoutArrival_ShouldFail` | Cannot seat without check-in |
| `RecordDepartureAsync_WithoutSeating_ShouldFail` | Cannot depart without seating |
| `RecordNoShowAsync_AfterArrival_ShouldFail` | Cannot no-show arrived guest |
| `AssignTableAsync_AfterCancellation_ShouldFail` | Cannot assign to cancelled booking |

**Order State Transitions**
| Test | Scenario |
|------|----------|
| `VoidAsync_OnClosedOrder_ShouldFail` | Cannot void closed order |
| `CloseAsync_WithUnpaidBalance_ShouldFail` | Cannot close with balance due |
| `SendAsync_EmptyOrder_ShouldFail` | Cannot send order without items |
| `AddLineAsync_AfterClosed_ShouldFail` | Cannot add items to closed order |
| `RemoveLineAsync_AfterSent_ShouldFail` | Cannot remove sent items |
| `RecordPaymentAsync_OnVoidedOrder_ShouldFail` | Cannot pay voided order |

**Payment State Transitions**
| Test | Scenario |
|------|----------|
| `RefundAsync_OnPendingPayment_ShouldFail` | Cannot refund pending payment |
| `VoidAsync_OnCompletedPayment_ShouldFail` | Cannot void completed payment |
| `CompleteAsync_AlreadyCompleted_ShouldFail` | Cannot complete twice |
| `RefundAsync_PartialAfterFull_ShouldFail` | Cannot partial refund after full refund |

### OrderInventoryIntegrationTests (23 tests)

Tests cross-grain integration between Order and Inventory grains.

| Test | Scenario |
|------|----------|
| `ConsumeForOrderAsync_ShouldDeductInventory` | Order deducts inventory on kitchen send |
| `ConsumeForOrderAsync_InsufficientStock_ShouldFail` | Rejects when insufficient stock |
| `VoidOrderAsync_ShouldReverseInventory` | Voided order returns inventory |
| `ConsumeAsync_FIFO_ShouldConsumeOldestFirst` | FIFO batch consumption |
| `ConsumeAsync_MultipleBatches_ShouldTrackCorrectly` | Multi-batch consumption tracking |
| `VoidLineAsync_PartialOrder_ShouldPartiallyReverse` | Partial void reverses partial inventory |

### PaymentSplitScenarioTests (18 tests)

Tests multiple payment methods per order and split payment scenarios.

| Test | Scenario |
|------|----------|
| `SplitPayment_CashAndCard_ShouldAllowBoth` | Mixed cash and card payments |
| `SplitPayment_ThreeWays_ShouldTrackAll` | Three-way split tracking |
| `PartialPayment_ShouldTrackBalance` | Partial payment with balance |
| `PartialPayment_Overpay_ShouldHandleChange` | Overpayment handling |
| `SplitPayment_Tips_ShouldAccumulateCorrectly` | Split tip accumulation |
| `SplitPayment_VoidOne_ShouldUpdateBalance` | Void one payment, balance updates |

### RefundErrorScenarioTests (23 tests)

Tests refund validation and error scenarios.

| Test | Scenario |
|------|----------|
| `RefundAsync_ExceedsAmount_ShouldReject` | Cannot refund more than paid |
| `RefundAsync_AfterFullRefund_ShouldReject` | Cannot refund twice |
| `PartialRefund_ThenFull_ShouldCalculateRemaining` | Partial + full refund math |
| `RefundAsync_OnVoidedPayment_ShouldReject` | Cannot refund voided payment |
| `RefundAsync_ZeroAmount_ShouldReject` | Cannot refund zero |
| `RefundAsync_NegativeAmount_ShouldReject` | Cannot refund negative |

### MenuItemAvailabilityTests (30 tests)

Tests menu item availability, snoozing, and variation management.

**Snooze Tests**
| Test | Scenario |
|------|----------|
| `SetSnoozedAsync_ShouldSnoozeItem` | Snoozes item for delivery |
| `SetSnoozedAsync_WithDuration_ShouldSetSnoozedUntil` | Time-limited snooze |
| `SetSnoozedAsync_Unsnooze_ShouldClearSnooze` | Clears snooze state |

**Deactivation Tests**
| Test | Scenario |
|------|----------|
| `DeactivateAsync_ShouldMakeItemUnavailable` | Deactivates item |
| `UpdateAsync_Reactivate_ShouldMakeItemAvailable` | Reactivates item |

**Variation Tests**
| Test | Scenario |
|------|----------|
| `AddVariationAsync_ShouldAddVariation` | Adds size/variation |
| `UpdateVariationAsync_Deactivate_ShouldMakeUnavailable` | Deactivates variation |
| `AddVariationAsync_VariablePricing_ShouldAllowNullPrice` | Variable pricing support |

**Product Tag & Tax Tests**
| Test | Scenario |
|------|----------|
| `AddProductTagAsync_ShouldAddTag` | Adds allergen/dietary tags |
| `UpdateTaxRatesAsync_ShouldSetContextualRates` | Contextual tax rates |

### KitchenTicketRoutingTests (35 tests)

Tests kitchen ticket routing, station management, and priority handling.

**Multi-Station Routing**
| Test | Scenario |
|------|----------|
| `AddItemAsync_WithDifferentStations_ShouldTrackAllStations` | Multi-station tracking |
| `AddItemAsync_SameStationMultipleTimes_ShouldNotDuplicateStation` | No duplicate station IDs |

**Station Management**
| Test | Scenario |
|------|----------|
| `ReceiveTicketAsync_ShouldAddTicketToStation` | Station receives ticket |
| `CompleteTicketAsync_ShouldRemoveFromStation` | Completion removes ticket |
| `PauseAsync_WithActiveTickets_ShouldKeepTickets` | Paused station keeps tickets |
| `CloseAsync_WithActiveTickets_ShouldClearTickets` | Closed station clears tickets |

**Priority & Rush**
| Test | Scenario |
|------|----------|
| `MarkRushAsync_ShouldSetRushPriority` | Rush priority setting |
| `MarkVipAsync_ShouldSetVipPriority` | VIP priority setting |
| `FireAllAsync_ShouldSetFireAllAndAllDayPriority` | Fire all items |

**Course Management**
| Test | Scenario |
|------|----------|
| `AddItemAsync_WithCourseNumber_ShouldTrackCourse` | Course number tracking |

### BoundaryAndEdgeCaseTests (35 tests)

Tests boundary conditions, zero/negative values, and edge cases.

**Zero Amount Tests**
| Test | Scenario |
|------|----------|
| `AddLineAsync_ZeroQuantity_ShouldHandleGracefully` | Zero quantity handling |
| `AddLineAsync_ZeroPrice_ShouldCreateZeroTotalLine` | Complimentary items |
| `RecordPaymentAsync_ZeroAmount_ShouldReject` | Zero payment rejected |

**Negative Value Tests**
| Test | Scenario |
|------|----------|
| `AddLineAsync_NegativeQuantity_ShouldReject` | Negative quantity rejected |
| `AddLineAsync_NegativePrice_ShouldReject` | Negative price rejected |
| `RecordPaymentAsync_NegativeAmount_ShouldReject` | Negative payment rejected |

**Large Value Tests**
| Test | Scenario |
|------|----------|
| `AddLineAsync_LargeQuantity_ShouldCalculateCorrectly` | Large quantity math |
| `AddLineAsync_HighPrecisionPrice_ShouldRoundAppropriately` | Decimal precision |

**Empty Collection Tests**
| Test | Scenario |
|------|----------|
| `GetLinesAsync_EmptyOrder_ShouldReturnEmptyList` | Empty order lines |
| `AddLineAsync_EmptyModifiersList_ShouldAccept` | Empty modifiers accepted |

**String Boundary Tests**
| Test | Scenario |
|------|----------|
| `AddLineAsync_EmptyName_ShouldReject` | Empty name rejected |
| `AddLineAsync_UnicodeCharactersInName_ShouldAccept` | Unicode support |

### ConcurrentOperationTests (18 tests)

Tests for concurrent operations and race condition handling using Orleans' single-writer guarantees.

**Order Concurrency**
| Test | Scenario |
|------|----------|
| `Order_ConcurrentLineAdds_ShouldSerializeCorrectly` | Concurrent line additions |
| `Order_ConcurrentPayments_ShouldTrackCorrectBalance` | Concurrent payment recording |
| `Order_ConcurrentUpdateAndVoid_ShouldHandleCorrectly` | Update vs void race condition |

**Inventory Concurrency**
| Test | Scenario |
|------|----------|
| `Inventory_ConcurrentConsumptions_ShouldMaintainCorrectLevel` | Concurrent stock deductions |
| `Inventory_ConcurrentReceiveAndConsume_ShouldTrackBatchesCorrectly` | Mixed receive/consume operations |

**Customer Concurrency**
| Test | Scenario |
|------|----------|
| `Customer_ConcurrentPointsEarning_ShouldAccumulateCorrectly` | Concurrent loyalty points |
| `Customer_ConcurrentTagOperations_ShouldNotDuplicateTags` | Tag deduplication |

**Multi-Grain Concurrency**
| Test | Scenario |
|------|----------|
| `MultiGrain_ConcurrentOrdersToSameTable_ShouldAllSucceed` | Multiple orders same table |
| `MultiGrain_RapidFireOperations_ShouldMaintainConsistency` | 50 rapid-fire operations |

### AuditTrailVerificationTests (14 tests)

Tests for event stream verification and audit trail consistency.

**Gift Card Events**
| Test | Scenario |
|------|----------|
| `GiftCard_Lifecycle_ShouldEmitAllEvents` | Activate, redeem, expire events |
| `GiftCard_RedeemEvent_ShouldHaveCorrectBalanceTracking` | Balance tracking in events |

**Customer Spend Events**
| Test | Scenario |
|------|----------|
| `CustomerSpend_RecordSpend_ShouldEmitSpendAndPointsEvents` | Spend and points earned events |
| `CustomerSpend_TierPromotion_ShouldEmitTierChangedEvent` | Tier change event |
| `CustomerSpend_PointsRedemption_ShouldEmitRedemptionEvent` | Points redemption event |
| `CustomerSpend_ReverseSpend_ShouldEmitReversalEvent` | Spend reversal event |

**Event Metadata**
| Test | Scenario |
|------|----------|
| `Events_ShouldHaveTimestamps` | Timestamp verification |
| `Events_ShouldHaveOrganizationContext` | Org context in events |
| `Events_ShouldBeOrderedChronologically` | Chronological ordering |

### PerformanceBoundaryTests (20 tests)

Tests for large collection handling and batch operation limits.

**Large Order Tests**
| Test | Scenario |
|------|----------|
| `Order_ManyLines_ShouldHandleCorrectly` | 100 line items |
| `Order_ManyModifiersPerLine_ShouldCalculateCorrectly` | 20 modifiers per item |
| `Order_ManyPayments_ShouldTrackAll` | 10 split payments |

**Large Inventory Tests**
| Test | Scenario |
|------|----------|
| `Inventory_ManyBatches_ShouldTrackAllWithFIFO` | 30 batches tracking |
| `Inventory_ConsumeThroughMultipleBatches_ShouldUseFIFO` | FIFO across batches |

**Large Kitchen Tests**
| Test | Scenario |
|------|----------|
| `KitchenTicket_ManyItems_ShouldTrackAll` | 50 items per ticket |
| `KitchenStation_ManyActiveTickets_ShouldTrackAll` | 30 concurrent tickets |

**Large Menu Tests**
| Test | Scenario |
|------|----------|
| `MenuItem_ManyVariations_ShouldHandleAll` | 15 size variations |
| `MenuItem_ManyModifierGroups_ShouldHandleAll` | 10 modifier groups, 50 options |
| `MenuDefinition_ManyScreensAndButtons_ShouldHandleAll` | 10 screens, 240 buttons |

**Bulk Operations**
| Test | Scenario |
|------|----------|
| `MenuEngineering_BulkRecordSales_ShouldHandleLargeVolume` | 50 product sales batch |

---

## Stream Event Tests

#### StreamEventTests

| Test | Scenario |
|------|----------|
| `UserCreatedEvent_Published` | Publishes event on user creation |
| `UserStatusChangedEvent_Published` | Publishes event on status change |

---

## Summary Statistics

| Category | Count |
|----------|-------|
| **Total Test Files** | 58 |
| **Grain Test Files** | 46+ |
| **API Test Files** | 10 |
| **Service Test Files** | 2 |
| **Unit Test Files** | 1 |
| **Total Test Methods** | 650+ |

### Coverage by Domain

| Domain | Test Count | Key Grains |
|--------|------------|------------|
| Accounting | 62+ | Account, DailySales, Streams |
| Booking | 50+ | Booking, Waitlist, Table, FloorPlan, Settings |
| Customer | 70+ | Customer, CustomerSpendProjection |
| Inventory | 30+ | Inventory, Alert, DailySnapshot |
| Costing | 21+ | Recipe, IngredientPrice, CostAlert, Policies |
| Menu | 10+ | MenuCategory, MenuItem |
| Order | 10+ | Order, OrderLine |
| Payment | 15+ | Payment, GiftCard |
| Loyalty | 28+ | LoyaltyProgram |
| Employee | 20+ | Employee, Availability, ShiftSwap, TimeOff |
| Procurement | 38+ | Supplier, PurchaseOrder, Delivery |
| Reporting | 20+ | Consumption, Waste, Period, Dashboard |
| Organization | 10+ | Organization, Site, User |

---

## Test Patterns & Conventions

### Naming Convention
```
MethodName_Condition_ExpectedOutcome
```

Example: `CreateAsync_DuplicateSlug_Throws`

### Arrangement Pattern (AAA)
```csharp
// Arrange
var grain = GetGrain(orgId, entityId);
await grain.CreateAsync(command);

// Act
var result = await grain.SomeOperationAsync();

// Assert
result.Property.Should().Be(expectedValue);
```

### Collection Usage
- All grain tests: `[Collection(ClusterCollection.Name)]`
- All API tests: `[Collection(ApiCollection.Name)]`

### Helper Methods
- `GetGrain(orgId, entityId)` - Gets typed grain from factory
- `CreateOrgAndSiteAsync()` - Creates test org and site
- `CreateFullProgramAsync()` - Creates program with tiers and rules

### Assertions
Uses FluentAssertions for BDD-style readable assertions:
```csharp
result.Should().NotBeNull();
result.Status.Should().Be(ExpectedStatus.Active);
items.Should().HaveCount(3);
```

---

## Coverage Gap Analysis

This section identifies logical coverage gaps and areas for improvement.

### Risk Assessment Summary

| Area | Coverage | Risk Level |
|------|----------|------------|
| Happy Path Workflows | Excellent | Low |
| Validation & Error Cases | Poor | **High** |
| State Transition Validation | Partial | **High** |
| Concurrent Operations | None | **High** |
| Cross-Grain Integration | Minimal | **High** |
| Boundary Cases | Sparse | Medium |
| Refunds/Reversals | Partial | **High** |

---

### Booking Domain Gaps

**Missing State Transition Tests:**
- Modifying booking after confirmation
- Cancelling a completed booking
- Confirming an already-confirmed booking
- Seating without first recording arrival

**Missing Availability & Conflict Tests:**
- Table availability checks with overlapping bookings
- Double-booking prevention
- Party size vs table capacity validation
- Table combination for large parties

**Missing Boundary Tests:**
- Zero or negative guest counts (error handling)
- Booking with party size exceeding max capacity
- Past date booking attempts
- Maximum advance booking enforcement

**Missing Integration Tests:**
- Booking confirmation → Table availability impact
- Booking → Automatic reminder triggering

---

### Order Domain Gaps

**Missing State Transition Tests:**
- Modifying order after sent to kitchen
- Closing order without full payment
- Sending an empty order
- Void after order completion/closure

**Missing Line Item Tests:**
- Voiding line after kitchen completion
- Modifying quantity/price after line is sent
- Line state validation (voiding already-voided line)

**Missing Discount Tests:**
- Applying discount greater than order total
- Multiple discount stacking rules
- Discount removal/adjustment
- Discount application after payment

**Missing Split Order Tests:**
- Split bill scenarios (multiple payment methods)
- Split order to different tables
- Unequal splits

**Missing Integration Tests:**
- Order send → Kitchen ticket creation
- Order send → Inventory deduction
- Void order → Cancel kitchen tickets
- Insufficient inventory rejection

---

### Payment Domain Gaps

**Missing Error Handling Tests:**
- Card declined during capture
- Timeout/network failure during payment
- CVV validation failure
- Address mismatch (AVS failure)

**Missing Refund Scenario Tests:**
- Refund exceeding payment amount
- Refund with prior partial refund
- Refund processor failure (retry logic)
- Refund reversal/chargeback

**Missing Split Payment Tests:**
- Multiple payment methods on one order
- Partial card + cash split
- Unequal split scenarios

**Missing Edge Case Tests:**
- Zero amount payments
- Negative amounts (error case)
- Overpayment handling
- Rounding errors (cash vs digital)

**Missing Integration Tests:**
- Payment → Order status update verification
- Payment → Accounting ledger entry
- Payment → Loyalty points application

---

### Inventory Domain Gaps

**Missing Consumption Error Tests:**
- Consume with zero quantity
- Consume negative amount
- Boundary cases on batch exhaustion

**Missing Expiration Tests:**
- Batch expiry date enforcement
- Partial batch expiration
- Expiration date updates
- Grace periods for use

**Missing Transfer Tests:**
- Chain transfers (A → B → C)
- Transfer approval workflows
- Transfer failures and rollback
- Transfer reconciliation

**Missing Integration Tests:**
- Order placed → Inventory deduction on kitchen send
- Menu item unavailable when inventory depleted
- Forecast-based ordering

---

### Kitchen Domain Gaps

**Missing State Transition Tests:**
- Item recall (reopen completed item)
- Item modification after start
- Item void after completion
- Invalid state transitions

**Missing Routing Tests:**
- Items for different stations on same ticket
- Ticket splitting across stations
- Item routing to wrong station (error handling)

**Missing Queue Tests:**
- Queue depth tracking
- Backing up alerts
- Peak time management
- Queue timeout/escalation

**Missing Allergen Tests:**
- Allergen tagging and validation
- Cross-contamination prevention
- Allergen warning on ticket

---

### Menu Domain Gaps

**Missing Time-Based Tests:**
- Lunch vs dinner menu switching
- Time-restricted items (breakfast only)
- Blackout periods
- Seasonal item dates

**Missing Availability Tests:**
- Sold out items (soft unavailability)
- Item deactivation impact on existing orders
- Inventory-based availability

**Missing Modifier Validation Tests:**
- Required modifier enforcement
- Min/max selection validation
- Incompatible modifier combinations

**Missing Pricing Tests:**
- Price tiers (happy hour, customer type)
- Price change impact on active orders

---

### Cross-Cutting Gaps

**Concurrency Testing:**
- No tests for simultaneous operations on same entity
- No race condition testing
- No deadlock scenarios

**Input Validation:**
- Limited negative/boundary value testing
- Business rule violation message verification

**Audit/History:**
- No audit trail testing
- No event sourcing validation
- No state change tracking verification

---

## Recommended Priority Improvements

### Priority 1 (High Risk) - COMPLETED

1. **~~Add state transition validation tests~~** ✓ (45 tests)
   - StateTransitionValidationTests.cs
   - Covers Booking, Order, and Payment state machines

2. **~~Add order ↔ inventory integration tests~~** ✓ (23 tests)
   - OrderInventoryIntegrationTests.cs
   - FIFO consumption, stock levels, void reversal

3. **~~Add payment split scenarios~~** ✓ (18 tests)
   - PaymentSplitScenarioTests.cs
   - Multi-method payments, partial payments, tips

4. **~~Add refund error scenarios~~** ✓ (23 tests)
   - RefundErrorScenarioTests.cs
   - Amount validation, status validation, partial refunds

### Priority 2 (Medium Risk) - COMPLETED

5. **~~Add menu item availability tests~~** ✓ (30 tests)
   - MenuItemAvailabilityTests.cs
   - Snooze, deactivation, variations, tax rates, product tags

6. **~~Add kitchen ticket routing tests~~** ✓ (35 tests)
   - KitchenTicketRoutingTests.cs
   - Multi-station routing, station management, priorities, courses

7. **~~Add negative/boundary case tests~~** ✓ (35 tests)
   - BoundaryAndEdgeCaseTests.cs
   - Zero/negative values, empty collections, string boundaries

### Priority 3 (Lower Risk) - COMPLETED

8. **~~Add concurrent operation tests~~** ✓ (18 tests)
   - ConcurrentOperationTests.cs
   - Race conditions, simultaneous modifications, grain serialization

9. **~~Add audit trail verification~~** ✓ (14 tests)
   - AuditTrailVerificationTests.cs
   - Event stream verification, metadata validation, chronological ordering

10. **~~Add performance boundary tests~~** ✓ (20 tests)
    - PerformanceBoundaryTests.cs
    - Large collections, many batches, bulk operations
