# Clover POS API vs DarkVelocity POS API Comparison

This document compares the Clover POS REST API with the DarkVelocity POS API to identify feature parity, gaps, and architectural differences.

## Executive Summary

| Aspect | Clover | DarkVelocity |
|--------|--------|--------------|
| **API Style** | Traditional REST | Minimal APIs with Orleans actors |
| **Authentication** | OAuth 2.0 + API tokens | OAuth 2.0 + Device Flow (RFC 8628) + PIN |
| **Multi-tenancy** | Merchant-centric | Organization → Site hierarchy |
| **Real-time** | Webhooks | SignalR |
| **Format** | JSON | HAL+JSON (planned) |

---

## API Categories Comparison

### 1. Merchants / Organizations

| Feature | Clover | DarkVelocity | Gap |
|---------|--------|--------------|-----|
| Get merchant/org info | `GET /v3/merchants/{mId}` | `GET /api/locations/organizations/{orgId}` | Disabled |
| Update merchant | `POST /v3/merchants/{mId}` | Not implemented | **Missing** |
| Get address | `GET /v3/merchants/{mId}/address` | Part of org state | - |
| Get payment gateway config | `GET /v3/merchants/{mId}/gateway` | Not exposed | **Missing** |
| Manage properties | `GET/POST /v3/merchants/{mId}/properties` | Not implemented | **Missing** |
| Default service charge | `GET /v3/merchants/{mId}/default_service_charge` | Not implemented | **Missing** |
| Tip suggestions | `GET/POST /v3/merchants/{mId}/tip_suggestions` | Not implemented | **Missing** |
| Order types | `GET/POST/DELETE /v3/merchants/{mId}/order_types` | OrderType enum only | **Missing CRUD** |
| Manage roles | `GET/POST/DELETE /v3/merchants/{mId}/roles` | Not implemented | **Missing** |
| Manage tenders | `GET/POST/PUT/DELETE /v3/merchants/{mId}/tenders` | PaymentMethod enum only | **Missing CRUD** |
| Opening hours | `GET/POST/PUT/DELETE /v3/merchants/{mId}/opening_hours` | Not implemented | **Missing** |
| Get devices | `GET /v3/merchants/{mId}/devices` | `GET /api/devices/{orgId}/{deviceId}` | Partial |

### 2. Sites (Clover doesn't have multi-site)

| Feature | Clover | DarkVelocity | Notes |
|---------|--------|--------------|-------|
| Site management | N/A | `POST/GET /api/locations/sites` | DarkVelocity advantage |
| Site-level settings | Part of merchant | Per-site timezone, currency, tax | DarkVelocity more flexible |
| Sales periods | N/A | Planned | Daily business period tracking |

### 3. Customers

| Feature | Clover | DarkVelocity | Gap |
|---------|--------|--------------|-----|
| List customers | `GET /v3/merchants/{mId}/customers` | `GET /api/customers/{orgId}/{customerId}` | Disabled |
| Create customer | `POST /v3/merchants/{mId}/customers` | `POST /api/customers/` | Disabled |
| Update customer | `POST /v3/merchants/{mId}/customers/{cId}` | Not implemented | **Missing** |
| Delete customer | `DELETE /v3/merchants/{mId}/customers/{cId}` | Not implemented | **Missing** |
| Customer addresses | `POST/DELETE` endpoints | Not implemented | **Missing** |
| Customer phone/email | `POST/DELETE` endpoints | Not implemented | **Missing** |
| Customer cards | `POST/PUT/DELETE` | Not implemented | **Missing** |
| Customer metadata | `POST` | Not implemented | **Missing** |
| Loyalty programs | Via third-party apps | `POST /api/customers/loyalty` | Disabled |

### 4. Employees / Staff

| Feature | Clover | DarkVelocity | Gap |
|---------|--------|--------------|-----|
| List employees | `GET /v3/merchants/{mId}/employees` | `GET /api/labor/employees/{orgId}/{employeeId}` | Disabled |
| Create employee | `POST /v3/merchants/{mId}/employees` | `POST /api/labor/employees` | Disabled |
| Update employee | `POST /v3/merchants/{mId}/employees/{eId}` | Not implemented | **Missing** |
| Delete employee | `DELETE /v3/merchants/{mId}/employees/{eId}` | Not implemented | **Missing** |
| Manage shifts | `GET/POST/PUT/DELETE` | `POST /api/labor/shifts` | Disabled |
| Get employee orders | `GET /v3/merchants/{mId}/employees/{eId}/orders` | Not implemented | **Missing** |
| Clock in/out | Via shifts API | `POST /api/labor/employees/{id}/clock-in|out` | Disabled |
| Time-off requests | Not built-in | `POST /api/labor/time-off` | Disabled |

### 5. Inventory / Menu

| Feature | Clover | DarkVelocity | Gap |
|---------|--------|--------------|-----|
| List items | `GET /v3/merchants/{mId}/items` | Via menu grains | Disabled |
| Create item | `POST /v3/merchants/{mId}/items` | `POST /api/menu/items` | Disabled |
| Update item | `PUT /v3/merchants/{mId}/items/{iId}` | `PUT /api/menu/items/{orgId}/{id}/price` | Disabled |
| Delete item | `DELETE /v3/merchants/{mId}/items/{iId}` | Not implemented | **Missing** |
| Bulk create | `POST /v3/merchants/{mId}/bulk_items` | Not implemented | **Missing** |
| Bulk update | `PUT /v3/merchants/{mId}/bulk_items` | Not implemented | **Missing** |
| Stock management | `GET/POST/DELETE /v3/merchants/{mId}/item_stocks` | `POST /api/inventory/items/{id}/adjust` | Disabled |
| Item groups | `GET/POST/PUT/DELETE` | Not implemented | **Missing** |
| Tags | `GET/POST/DELETE` | Not implemented | **Missing** |
| Tax rates | `GET/POST/PUT/DELETE` | Via site settings | **Missing CRUD** |
| Categories | `GET/POST/DELETE` | `POST /api/menu/categories` | Disabled |
| Modifier groups | `GET/POST/DELETE` | `POST /api/menu/modifiers` | Disabled |
| Modifiers | `GET/POST/DELETE` | Part of modifier groups | Partial |
| Attributes/Options | `GET/POST/PUT/DELETE` | Not implemented | **Missing** |
| Discounts | `GET/POST/PUT/DELETE` | Via order line items | **Missing CRUD** |

### 6. Orders

| Feature | Clover | DarkVelocity | Gap |
|---------|--------|--------------|-----|
| Create atomic order | `POST /v3/merchants/{mId}/atomic_order/orders` | `POST /api/orders/` | Disabled |
| Checkout atomic | `POST /v3/merchants/{mId}/atomic_order/checkouts` | Via submit + payment | Different approach |
| List orders | `GET /v3/merchants/{mId}/orders` | Not implemented | **Missing** |
| Get order | `GET /v3/merchants/{mId}/orders/{oId}` | `GET /api/orders/{orgId}/{orderId}` | Disabled |
| Update order | `POST /v3/merchants/{mId}/orders/{oId}` | Not implemented | **Missing** |
| Delete order | `DELETE /v3/merchants/{mId}/orders/{oId}` | Via cancel | Different |
| Add line items | `POST /v3/merchants/{mId}/orders/{oId}/line_items` | `POST /api/orders/{id}/items` | Disabled |
| Remove line items | `DELETE .../line_items/{liId}` | Not implemented | **Missing** |
| Add discounts | `POST .../discounts` | Via line items | Different |
| Apply modifications | `POST .../modifications` | Not implemented | **Missing** |
| Service charges | `POST/DELETE` | Not implemented | **Missing** |
| Submit order | N/A | `POST /api/orders/{id}/submit` | DarkVelocity has workflow |
| Complete order | N/A | `POST /api/orders/{id}/complete` | DarkVelocity has workflow |
| Cancel order | N/A | `POST /api/orders/{id}/cancel` | - |
| Void operations | `POST/DELETE` | Not implemented | **Missing** |

### 7. Payments

| Feature | Clover | DarkVelocity | Gap |
|---------|--------|--------------|-----|
| Get payments | `GET /v3/merchants/{mId}/payments` | `GET /api/payments/{orgId}/{paymentId}` | Disabled |
| Create payment | `POST /v3/merchants/{mId}/orders/{oId}/payments` | `POST /api/payments/` | Disabled |
| Update payment | `POST /v3/merchants/{mId}/payments/{pId}` | Not implemented | **Missing** |
| Process payment | N/A | `POST /api/payments/{id}/process` | Disabled |
| Authorizations | `GET/POST/PUT/DELETE` | Not implemented | **Missing** |
| Refunds | `GET` view only | `POST /api/payments/refunds` | Disabled |
| Tip adjust | `POST /v3/merchants/{mId}/payments/{pId}/tip` | Not implemented | **Missing** |
| Void payment | Via Card Present API | Not implemented | **Missing** |

### 8. Cash Management

| Feature | Clover | DarkVelocity | Gap |
|---------|--------|--------------|-----|
| Get cash events | `GET /v3/merchants/{mId}/cash_events` | Via CashDrawerGrain | Disabled |
| Employee cash events | `GET .../employees/{eId}/cash_events` | Not implemented | **Missing** |
| Device cash events | `GET .../devices/{dId}/cash_events` | Not implemented | **Missing** |
| Open/close drawer | Via Card Present API | Via hardware grain | - |

### 9. Devices / Hardware

| Feature | Clover | DarkVelocity | Gap |
|---------|--------|--------------|-----|
| List devices | `GET /v3/merchants/{mId}/devices` | Via device grain | Different |
| Get device | Part of list | `GET /api/devices/{orgId}/{deviceId}` | Active |
| Register device | Via setup flow | `POST /api/hardware/devices` | Disabled |
| Device heartbeat | Not exposed | `POST /api/devices/{id}/heartbeat` | Active |
| Suspend device | Not exposed | `POST /api/devices/{id}/suspend` | Active |
| Revoke device | Not exposed | `POST /api/devices/{id}/revoke` | Active |
| Ping device | `GET/POST` | Not implemented | **Missing** |
| Device status | `GET/PUT` | Via heartbeat/state | Different |
| Cancel operations | `POST` | Not implemented | **Missing** |
| Custom activity | `POST` | Not implemented | **Missing** |
| Display messages | `POST` | Not implemented | **Missing** |
| Display order | `POST` | Not implemented | **Missing** |
| Read confirmations | `POST` | Not implemented | **Missing** |
| Read input | `POST` | Not implemented | **Missing** |
| Read signature | `POST` | Not implemented | **Missing** |
| Read tip | `POST` | Not implemented | **Missing** |
| Cash drawer control | `POST/GET` | Via hardware grain | Disabled |

### 10. Printing

| Feature | Clover | DarkVelocity | Gap |
|---------|--------|--------------|-----|
| Submit print request | `POST /v3/merchants/{mId}/print_event` | Via printer grain | Disabled |
| Get print event | `GET /v3/merchants/{mId}/print_event/{id}` | Not implemented | **Missing** |
| Receipt delivery | Via Card Present API | Not implemented | **Missing** |

### 11. Authentication & Authorization

| Feature | Clover | DarkVelocity | Notes |
|---------|--------|--------------|-------|
| OAuth 2.0 | Yes (v2/OAuth) | Yes | - |
| API tokens | Merchant-specific | JWT | Different approach |
| Device flow | Not supported | RFC 8628 | **DarkVelocity advantage** |
| PIN login | Not via API | `POST /api/auth/pin` | **DarkVelocity advantage** |
| Token refresh | Yes | `POST /api/auth/refresh` | - |
| Role-based access | Via roles API | Planned (SpiceDB) | - |

### 12. Ecommerce / Online Orders

| Feature | Clover | DarkVelocity | Gap |
|---------|--------|--------------|-----|
| Create charge | `POST /v1/charges` | Not implemented | **Missing** |
| Capture charge | `POST /v1/charges/{id}/capture` | Not implemented | **Missing** |
| Card-on-file customer | `POST /v1/customers` | Not implemented | **Missing** |
| Refunds | `POST/GET /v1/refunds` | Via payments API | Different |
| Online orders | `GET/POST /v1/orders` | Via orders API | - |
| Pay for order | `POST /v1/orders/{id}/pay` | Via payments API | Different |

### 13. Tokenization

| Feature | Clover | DarkVelocity | Gap |
|---------|--------|--------------|-----|
| Create card token | `POST /v1/tokens` | Not implemented | **Missing** |
| Apple Pay token | Yes | Not implemented | **Missing** |
| ACH token | Yes | Not implemented | **Missing** |
| Gift card token | Yes | Not implemented | **Missing** |

### 14. Recurring Payments / Subscriptions

| Feature | Clover | DarkVelocity | Gap |
|---------|--------|--------------|-----|
| Plans management | `GET/POST/PUT/DELETE /v1/plans` | Not implemented | **Missing** |
| Subscriptions | `GET/POST/PUT/DELETE /v1/subscriptions` | Not implemented | **Missing** |

### 15. Gift Cards

| Feature | Clover | DarkVelocity | Gap |
|---------|--------|--------------|-----|
| Balance inquiry | `POST /v1/gift_cards/balance` | Via gift card grain | Disabled |
| Reload | `POST /v1/gift_cards/reload` | Not implemented | **Missing** |
| Cashout | `POST /v1/gift_cards/cashout` | Not implemented | **Missing** |
| Activation | `POST /v1/gift_cards/activate` | Not implemented | **Missing** |
| Create gift card | N/A | `POST /api/giftcards/` | Disabled |
| Redeem | N/A | `POST /api/giftcards/{id}/redeem` | Disabled |

### 16. Notifications

| Feature | Clover | DarkVelocity | Gap |
|---------|--------|--------------|-----|
| App notifications | `POST /v3/merchants/{mId}/notifications` | Not implemented | **Missing** |
| Webhooks | Yes | SignalR instead | Different approach |

### 17. Reporting

| Feature | Clover | DarkVelocity | Gap |
|---------|--------|--------------|-----|
| Export data | Via merchant data | Not implemented | **Missing** |
| Daily sales report | Not built-in (apps) | `GET /api/reports/sales/{orgId}/{siteId}/{date}` | Disabled |
| Generate report | Not built-in | `POST /api/reports/.../generate` | Disabled |

---

## Backend Capabilities: Grain Interface Comparison

This section compares Clover API capabilities against DarkVelocity's **actual backend implementation** via Orleans grain interfaces. Many features exist in grains but aren't yet exposed via REST API.

### Legend
- ✅ **Grain Ready** - Backend fully implemented, needs API exposure
- ⚠️ **Partial** - Some methods implemented
- ❌ **Not Implemented** - No grain support

### 1. Merchants / Organizations

| Clover Feature | DarkVelocity Grain | Status | Grain Methods |
|----------------|-------------------|--------|---------------|
| Get/Update merchant | `IOrganizationGrain` | ✅ | `CreateAsync`, `UpdateAsync`, `GetStateAsync` |
| Manage properties | `IOrganizationGrain` | ✅ | Part of state |
| Default service charge | `IOrderGrain` | ✅ | `AddServiceChargeAsync` |
| Tip suggestions | - | ❌ | Not implemented |
| Order types | `IOrderGrain` | ✅ | OrderType enum in CreateOrderCommand |
| Manage roles | `IRoleGrain` | ✅ | `CreateAsync`, `UpdateAsync`, `GetSnapshotAsync` |
| Manage tenders | `IPaymentGrain` | ✅ | PaymentMethod enum |
| Opening hours | - | ❌ | Not implemented |

### 2. Sites (DarkVelocity Advantage)

| Feature | Grain | Status | Methods |
|---------|-------|--------|---------|
| Site CRUD | `ISiteGrain` | ✅ | `CreateAsync`, `UpdateAsync`, `GetStateAsync` |
| Open/Close site | `ISiteGrain` | ✅ | `OpenAsync`, `CloseAsync`, `CloseTemporarilyAsync` |
| Floor plans | `IFloorPlanGrain` | ✅ | `CreateAsync`, `UpdateAsync`, `DeleteAsync` |
| Kitchen stations | `IKitchenStationGrain` | ✅ | `OpenAsync`, `CloseAsync`, per-site |
| Active menu | `ISiteGrain` | ✅ | `SetActiveMenuAsync` |

### 3. Customers

| Clover Feature | DarkVelocity Grain | Status | Grain Methods |
|----------------|-------------------|--------|---------------|
| Create customer | `ICustomerGrain` | ✅ | `CreateAsync` |
| Update customer | `ICustomerGrain` | ✅ | `UpdateAsync` |
| Delete customer | `ICustomerGrain` | ✅ | `DeleteAsync`, `AnonymizeAsync` (GDPR) |
| Customer metadata | `ICustomerGrain` | ✅ | `AddTagAsync`, `AddNoteAsync` |
| Customer cards | - | ❌ | Not implemented |
| Loyalty programs | `ILoyaltyProgramGrain` | ✅ | Full program management |
| Points | `ICustomerGrain` | ✅ | `EarnPointsAsync`, `RedeemPointsAsync`, `AdjustPointsAsync` |
| Rewards | `ICustomerGrain` | ✅ | `IssueRewardAsync`, `RedeemRewardAsync` |
| Referrals | `ICustomerGrain` | ✅ | `SetReferralCodeAsync`, `IncrementReferralCountAsync` |
| Spend tracking | `ICustomerSpendProjectionGrain` | ✅ | `RecordSpendAsync`, `GetSnapshotAsync` |

### 4. Employees / Labor

| Clover Feature | DarkVelocity Grain | Status | Grain Methods |
|----------------|-------------------|--------|---------------|
| Employee CRUD | `IEmployeeGrain` | ✅ | `CreateAsync`, `UpdateAsync`, `GetStateAsync` |
| Activate/Deactivate | `IEmployeeGrain` | ✅ | `ActivateAsync`, `DeactivateAsync`, `TerminateAsync` |
| Role assignment | `IEmployeeGrain` | ✅ | `AssignRoleAsync`, `RemoveRoleAsync` |
| Site access | `IEmployeeGrain` | ✅ | `GrantSiteAccessAsync`, `RevokeSiteAccessAsync` |
| Shifts | `IScheduleGrain` | ✅ | `AddShiftAsync`, `UpdateShiftAsync`, `RemoveShiftAsync` |
| Clock in/out | `IEmployeeGrain`, `ITimeEntryGrain` | ✅ | `ClockInAsync`, `ClockOutAsync` |
| Time entries | `ITimeEntryGrain` | ✅ | `AdjustAsync`, `ApproveAsync`, `AddBreakAsync` |
| Availability | `IEmployeeAvailabilityGrain` | ✅ | `SetAvailabilityAsync`, `IsAvailableOnAsync` |
| Shift swaps | `IShiftSwapGrain` | ✅ | `CreateAsync`, `ApproveAsync`, `RejectAsync` |
| Time-off requests | `ITimeOffGrain` | ✅ | `CreateAsync`, `ApproveAsync`, `RejectAsync` |
| Tip pools | `ITipPoolGrain` | ✅ | `CreateAsync`, `AddTipsAsync`, `DistributeAsync` |
| Payroll | `IPayrollPeriodGrain` | ✅ | `CreateAsync`, `CalculateAsync`, `ProcessAsync` |

**DarkVelocity Labor Management: Far exceeds Clover's basic employee API**

### 5. Inventory / Menu

| Clover Feature | DarkVelocity Grain | Status | Grain Methods |
|----------------|-------------------|--------|---------------|
| Items CRUD | `IMenuItemGrain` | ✅ | `CreateAsync`, `UpdateAsync`, `DeactivateAsync` |
| Item price | `IMenuItemGrain` | ✅ | `GetPriceAsync`, `UpdateCostAsync` |
| Modifiers | `IMenuItemGrain` | ✅ | `AddModifierAsync`, `RemoveModifierAsync` |
| Categories | `IMenuCategoryGrain` | ✅ | `CreateAsync`, `UpdateAsync`, `DeactivateAsync` |
| Stock management | `IInventoryGrain` | ✅ | Full FIFO batch management |
| Stock levels | `IInventoryGrain` | ✅ | `GetLevelInfoAsync`, `GetStockLevelAsync` |
| Receive stock | `IInventoryGrain` | ✅ | `ReceiveBatchAsync`, `ReceiveTransferAsync` |
| Consume stock | `IInventoryGrain` | ✅ | `ConsumeAsync`, `ConsumeForOrderAsync` |
| Waste tracking | `IInventoryGrain` | ✅ | `RecordWasteAsync` |
| Physical counts | `IInventoryGrain` | ✅ | `RecordPhysicalCountAsync` |
| Transfers | `IInventoryGrain` | ✅ | `TransferOutAsync` |
| Par levels | `IInventoryGrain` | ✅ | `SetParLevelAsync`, `SetReorderPointAsync` |
| Batch expiry | `IInventoryGrain` | ✅ | `WriteOffExpiredBatchesAsync` |
| Tax rates | `ITaxRateGrain` | ✅ | `CreateAsync`, `DeactivateAsync` |
| Bulk operations | - | ❌ | Not implemented |
| Tags | - | ❌ | Not implemented |
| Accounting groups | `IAccountingGroupGrain` | ✅ | `CreateAsync`, `UpdateAsync` |

**DarkVelocity Inventory: FIFO batch tracking with expiry far exceeds Clover**

### 6. Menu Definitions (DarkVelocity Advantage)

| Feature | Grain | Status | Methods |
|---------|-------|--------|---------|
| Menu definitions | `IMenuDefinitionGrain` | ✅ | `CreateAsync`, `UpdateAsync`, `SetAsDefaultAsync` |
| POS screens | `IMenuDefinitionGrain` | ✅ | `AddScreenAsync`, `UpdateScreenAsync`, `RemoveScreenAsync` |
| POS buttons | `IMenuDefinitionGrain` | ✅ | `AddButtonAsync`, `RemoveButtonAsync` |

### 7. Orders

| Clover Feature | DarkVelocity Grain | Status | Grain Methods |
|----------------|-------------------|--------|---------------|
| Create order | `IOrderGrain` | ✅ | `CreateAsync` |
| Get order | `IOrderGrain` | ✅ | `GetStateAsync`, `GetLinesAsync`, `GetTotalsAsync` |
| Add line items | `IOrderGrain` | ✅ | `AddLineAsync` |
| Update line items | `IOrderGrain` | ✅ | `UpdateLineAsync` |
| Void line items | `IOrderGrain` | ✅ | `VoidLineAsync` |
| Remove line items | `IOrderGrain` | ✅ | `RemoveLineAsync` |
| Discounts | `IOrderGrain` | ✅ | `ApplyDiscountAsync`, `RemoveDiscountAsync` |
| Service charges | `IOrderGrain` | ✅ | `AddServiceChargeAsync` |
| Assign customer | `IOrderGrain` | ✅ | `AssignCustomerAsync` |
| Assign server | `IOrderGrain` | ✅ | `AssignServerAsync` |
| Table transfer | `IOrderGrain` | ✅ | `TransferTableAsync` |
| Send to kitchen | `IOrderGrain` | ✅ | `SendAsync` |
| Record payment | `IOrderGrain` | ✅ | `RecordPaymentAsync`, `RemovePaymentAsync` |
| Close order | `IOrderGrain` | ✅ | `CloseAsync` |
| Void order | `IOrderGrain` | ✅ | `VoidAsync` |
| Reopen order | `IOrderGrain` | ✅ | `ReopenAsync` |
| Recalculate totals | `IOrderGrain` | ✅ | `RecalculateTotalsAsync` |

**Order grain is feature-complete - just needs API exposure**

### 8. Payments

| Clover Feature | DarkVelocity Grain | Status | Grain Methods |
|----------------|-------------------|--------|---------------|
| Initiate payment | `IPaymentGrain` | ✅ | `InitiateAsync` |
| Cash payment | `IPaymentGrain` | ✅ | `CompleteCashAsync` |
| Card payment | `IPaymentGrain` | ✅ | `CompleteCardAsync` |
| Gift card payment | `IPaymentGrain` | ✅ | `CompleteGiftCardAsync` |
| Authorization | `IPaymentGrain` | ✅ | `RequestAuthorizationAsync`, `RecordAuthorizationAsync` |
| Capture | `IPaymentGrain` | ✅ | `CaptureAsync` |
| Decline handling | `IPaymentGrain` | ✅ | `RecordDeclineAsync` |
| Full refund | `IPaymentGrain` | ✅ | `RefundAsync` |
| Partial refund | `IPaymentGrain` | ✅ | `PartialRefundAsync` |
| Void payment | `IPaymentGrain` | ✅ | `VoidAsync` |
| Tip adjustment | `IPaymentGrain` | ✅ | `AdjustTipAsync` |
| Batch assignment | `IPaymentGrain` | ✅ | `AssignToBatchAsync` |
| Refund tracking | `IRefundGrain` | ✅ | `CreateAsync`, `ProcessAsync`, `FailAsync` |

**Payment grain is feature-complete including tip adjustment**

### 9. Cash Management

| Clover Feature | DarkVelocity Grain | Status | Grain Methods |
|----------------|-------------------|--------|---------------|
| Open drawer | `ICashDrawerGrain` | ✅ | `OpenAsync` |
| Close drawer | `ICashDrawerGrain` | ✅ | `CloseAsync` |
| Cash in | `ICashDrawerGrain` | ✅ | `RecordCashInAsync` |
| Cash out | `ICashDrawerGrain` | ✅ | `RecordCashOutAsync` |
| Cash drop | `ICashDrawerGrain` | ✅ | `RecordDropAsync` |
| No-sale open | `ICashDrawerGrain` | ✅ | `OpenNoSaleAsync` |
| Count drawer | `ICashDrawerGrain` | ✅ | `CountAsync` |
| Expected balance | `ICashDrawerGrain` | ✅ | `GetExpectedBalanceAsync` |

### 10. Kitchen Display (DarkVelocity Advantage)

| Feature | Grain | Status | Methods |
|---------|-------|--------|---------|
| Create ticket | `IKitchenTicketGrain` | ✅ | `CreateAsync` |
| Add items | `IKitchenTicketGrain` | ✅ | `AddItemAsync` |
| Start/Complete items | `IKitchenTicketGrain` | ✅ | `StartItemAsync`, `CompleteItemAsync` |
| Void items | `IKitchenTicketGrain` | ✅ | `VoidItemAsync` |
| Ticket workflow | `IKitchenTicketGrain` | ✅ | `ReceiveAsync`, `StartAsync`, `BumpAsync`, `VoidAsync` |
| Rush/VIP priority | `IKitchenTicketGrain` | ✅ | `SetPriorityAsync`, `MarkRushAsync`, `MarkVipAsync` |
| Fire all | `IKitchenTicketGrain` | ✅ | `FireAllAsync` |
| Timings | `IKitchenTicketGrain` | ✅ | `GetTimingsAsync` |
| Station management | `IKitchenStationGrain` | ✅ | `OpenAsync`, `CloseAsync`, `PauseAsync`, `ResumeAsync` |
| Item routing | `IKitchenStationGrain` | ✅ | `AssignItemsAsync`, `ReceiveTicketAsync` |
| Station printer/display | `IKitchenStationGrain` | ✅ | `SetPrinterAsync`, `SetDisplayAsync` |

**Clover has no native KDS - relies on third-party apps**

### 11. Hardware

| Clover Feature | DarkVelocity Grain | Status | Grain Methods |
|----------------|-------------------|--------|---------------|
| POS devices | `IPosDeviceGrain` | ✅ | `RegisterAsync`, `UpdateAsync`, `DeactivateAsync` |
| Device heartbeat | `IPosDeviceGrain` | ✅ | `RecordHeartbeatAsync` |
| Device online status | `IPosDeviceGrain` | ✅ | `IsOnlineAsync`, `SetOfflineAsync` |
| Printers | `IPrinterGrain` | ✅ | `RegisterAsync`, `UpdateAsync`, `RecordPrintAsync` |
| Cash drawers | `ICashDrawerHardwareGrain` | ✅ | `RegisterAsync`, `UpdateAsync`, `GetKickCommandAsync` |
| Device status overview | `IDeviceStatusGrain` | ✅ | `GetSummaryAsync`, `UpdateDeviceStatusAsync` |
| Ping device | - | ❌ | Not implemented |
| Display messages | - | ❌ | Not implemented |
| Read signature | - | ❌ | Not implemented |

### 12. Gift Cards

| Clover Feature | DarkVelocity Grain | Status | Grain Methods |
|----------------|-------------------|--------|---------------|
| Create gift card | `IGiftCardGrain` | ✅ | `CreateAsync` |
| Activate | `IGiftCardGrain` | ✅ | `ActivateAsync` |
| Balance inquiry | `IGiftCardGrain` | ✅ | `GetBalanceInfoAsync`, `HasSufficientBalanceAsync` |
| Redeem | `IGiftCardGrain` | ✅ | `RedeemAsync` |
| Reload | `IGiftCardGrain` | ✅ | `ReloadAsync` |
| Refund to card | `IGiftCardGrain` | ✅ | `RefundToCardAsync` |
| Adjust balance | `IGiftCardGrain` | ✅ | `AdjustBalanceAsync` |
| PIN validation | `IGiftCardGrain` | ✅ | `ValidatePinAsync` |
| Expire | `IGiftCardGrain` | ✅ | `ExpireAsync` |
| Cancel | `IGiftCardGrain` | ✅ | `CancelAsync` |
| Void transaction | `IGiftCardGrain` | ✅ | `VoidTransactionAsync` |
| Transaction history | `IGiftCardGrain` | ✅ | `GetTransactionsAsync` |
| Set recipient | `IGiftCardGrain` | ✅ | `SetRecipientAsync` |

**Gift card grain exceeds Clover's gift card API**

### 13. Booking / Reservations (DarkVelocity Advantage)

| Feature | Grain | Status | Methods |
|---------|-------|--------|---------|
| Request booking | `IBookingGrain` | ✅ | `RequestAsync` |
| Confirm booking | `IBookingGrain` | ✅ | `ConfirmAsync` |
| Modify booking | `IBookingGrain` | ✅ | `ModifyAsync` |
| Cancel booking | `IBookingGrain` | ✅ | `CancelAsync` |
| Table assignment | `IBookingGrain` | ✅ | `AssignTableAsync`, `ClearTableAssignmentAsync` |
| Guest arrival | `IBookingGrain` | ✅ | `RecordArrivalAsync`, `SeatGuestAsync` |
| Guest departure | `IBookingGrain` | ✅ | `RecordDepartureAsync` |
| No-show handling | `IBookingGrain` | ✅ | `MarkNoShowAsync` |
| Deposits | `IBookingGrain` | ✅ | `RequireDepositAsync`, `RecordDepositPaymentAsync`, `RefundDepositAsync` |
| Special requests | `IBookingGrain` | ✅ | `AddSpecialRequestAsync`, `AddNoteAsync` |
| Link to order | `IBookingGrain` | ✅ | `LinkToOrderAsync` |
| Table management | `ITableGrain` | ✅ | Full table CRUD with status |
| Floor plans | `IFloorPlanGrain` | ✅ | Floor plan management |
| Waitlist | `IWaitlistGrain` | ✅ | `AddEntryAsync`, `SeatEntryAsync`, wait time estimation |
| Booking settings | `IBookingSettingsGrain` | ✅ | Deposit rules, booking windows |

**Clover has no native booking - relies on third-party apps**

### 14. Procurement (DarkVelocity Advantage)

| Feature | Grain | Status | Methods |
|---------|-------|--------|---------|
| Suppliers | `ISupplierGrain` | ✅ | `CreateAsync`, `UpdateAsync` |
| Supplier ingredients | `ISupplierGrain` | ✅ | `AddIngredientAsync`, `UpdateIngredientPriceAsync` |
| Purchase orders | `IPurchaseOrderGrain` | ✅ | `CreateAsync`, `SubmitAsync` |
| PO lines | `IPurchaseOrderGrain` | ✅ | `AddLineAsync`, `UpdateLineAsync`, `RemoveLineAsync` |
| Receive PO | `IPurchaseOrderGrain` | ✅ | `ReceiveLineAsync` |
| Cancel PO | `IPurchaseOrderGrain` | ✅ | `CancelAsync` |
| Deliveries | `IDeliveryGrain` | ✅ | `CreateAsync`, `AcceptAsync`, `RejectAsync` |
| Discrepancies | `IDeliveryGrain` | ✅ | `RecordDiscrepancyAsync` |

**Clover has no procurement capabilities**

### 15. Costing & Menu Engineering (DarkVelocity Advantage)

| Feature | Grain | Status | Methods |
|---------|-------|--------|---------|
| Recipes | `IRecipeGrain` | ✅ | `CreateAsync`, `UpdateAsync`, `DeleteAsync` |
| Recipe ingredients | `IRecipeGrain` | ✅ | `AddIngredientAsync`, `UpdateIngredientAsync` |
| Cost calculation | `IRecipeGrain` | ✅ | `CalculateCostAsync` |
| Cost snapshots | `IRecipeGrain` | ✅ | `CreateCostSnapshotAsync`, `GetCostHistoryAsync` |
| Ingredient prices | `IIngredientPriceGrain` | ✅ | `CreateAsync`, `UpdatePriceAsync` |
| Price history | `IIngredientPriceGrain` | ✅ | `GetPriceHistoryAsync` |
| Cost alerts | `ICostAlertGrain` | ✅ | `CreateAsync`, `AcknowledgeAsync` |
| Costing settings | `ICostingSettingsGrain` | ✅ | Thresholds for alerts |
| Menu engineering | `IMenuEngineeringGrain` | ✅ | `AnalyzeAsync`, Star/Plowhorse/Puzzle/Dog |
| Price optimization | `IMenuEngineeringGrain` | ✅ | `GetPriceSuggestionsAsync` |

**Clover has no recipe costing or menu engineering**

### 16. Reporting & Analytics (DarkVelocity Advantage)

| Feature | Grain | Status | Methods |
|---------|-------|--------|---------|
| Daily sales | `IDailySalesGrain` | ✅ | `RecordSaleAsync`, `GetMetricsAsync` |
| Gross profit | `IDailySalesGrain` | ✅ | `GetGrossProfitMetricsAsync` |
| Inventory snapshots | `IDailyInventorySnapshotGrain` | ✅ | Daily stock levels |
| Consumption tracking | `IDailyConsumptionGrain` | ✅ | `RecordConsumptionAsync`, variance analysis |
| Waste tracking | `IDailyWasteGrain` | ✅ | `RecordWasteAsync` |
| Period aggregation | `IPeriodAggregationGrain` | ✅ | Weekly/monthly/yearly rollups |
| Site dashboard | `ISiteDashboardGrain` | ✅ | `GetMetricsAsync`, `GetTopVariancesAsync` |

**DarkVelocity has built-in analytics; Clover relies on apps/exports**

### 17. Delivery Platform Integration (DarkVelocity Advantage)

| Feature | Grain | Status | Methods |
|---------|-------|--------|---------|
| Platform connection | `IDeliveryPlatformGrain` | ✅ | `ConnectAsync`, `DisconnectAsync` |
| Pause/Resume | `IDeliveryPlatformGrain` | ✅ | `PauseAsync`, `ResumeAsync` |
| Location mapping | `IDeliveryPlatformGrain` | ✅ | `AddLocationMappingAsync` |
| External orders | `IExternalOrderGrain` | ✅ | `CreateAsync`, `AcceptAsync`, `RejectAsync` |
| Order status | `IExternalOrderGrain` | ✅ | `SetPreparingAsync`, `SetReadyAsync`, etc. |
| Menu sync | `IMenuSyncGrain` | ✅ | `StartAsync`, `RecordItemSyncedAsync` |
| Payouts | `IPlatformPayoutGrain` | ✅ | `CreateAsync`, `CompleteAsync` |

**Native integration with Uber Eats, DoorDash, Deliveroo, etc.**

### 18. Fiscalization & Compliance (DarkVelocity Advantage)

| Feature | Grain | Status | Methods |
|---------|-------|--------|---------|
| Fiscal devices | `IFiscalDeviceGrain` | ✅ | `RegisterAsync`, Swissbit/Fiskaly/Epson |
| Transaction signing | `IFiscalTransactionGrain` | ✅ | `CreateAsync`, `SignAsync` |
| QR codes | `IFiscalTransactionGrain` | ✅ | `GetQrCodeDataAsync` |
| Fiscal journal | `IFiscalJournalGrain` | ✅ | Audit logging |
| Tax rates | `ITaxRateGrain` | ✅ | Multi-jurisdiction support |

**European fiscalization compliance built-in**

### 19. Accounting (DarkVelocity Advantage)

| Feature | Grain | Status | Methods |
|---------|-------|--------|---------|
| Account management | `IAccountGrain` | ✅ | Double-entry accounting |
| Debits/Credits | `IAccountGrain` | ✅ | `PostDebitAsync`, `PostCreditAsync` |
| Journal entries | `IAccountGrain` | ✅ | `GetRecentEntriesAsync`, `GetEntriesInRangeAsync` |
| Balance queries | `IAccountGrain` | ✅ | `GetBalanceAsync`, `GetBalanceAtAsync` |
| Period closing | `IAccountGrain` | ✅ | `ClosePeriodAsync` |
| Entry reversal | `IAccountGrain` | ✅ | `ReverseEntryAsync` |

**Full double-entry accounting system**

### 20. Alerts & Notifications (DarkVelocity Advantage)

| Feature | Grain | Status | Methods |
|---------|-------|--------|---------|
| Alert creation | `IAlertGrain` | ✅ | `CreateAlertAsync` |
| Alert management | `IAlertGrain` | ✅ | `AcknowledgeAsync`, `ResolveAsync`, `SnoozeAsync` |
| Alert rules | `IAlertGrain` | ✅ | `EvaluateRulesAsync`, `UpdateRuleAsync` |
| Notification channels | `INotificationGrain` | ✅ | Slack, Email, Push, Webhooks |
| Multi-channel | `INotificationGrain` | ✅ | `SendAsync`, `AddChannelAsync` |

---

## Grain Coverage Summary

| Category | Clover Endpoints | DarkVelocity Grains | Backend Ready? |
|----------|-----------------|---------------------|----------------|
| Organizations/Sites | 12 | 2 grains, 10+ methods | ✅ Yes |
| Customers/Loyalty | 8 | 3 grains, 50+ methods | ✅ Yes |
| Employees/Labor | 6 | 8 grains, 80+ methods | ✅ Yes |
| Menu/Inventory | 20 | 6 grains, 60+ methods | ✅ Yes |
| Orders | 15 | 1 grain, 25+ methods | ✅ Yes |
| Payments | 10 | 4 grains, 40+ methods | ✅ Yes |
| Hardware | 15 | 5 grains, 30+ methods | ⚠️ Partial |
| Kitchen Display | 0 | 2 grains, 30+ methods | ✅ Yes |
| Booking | 0 | 5 grains, 60+ methods | ✅ Yes |
| Procurement | 0 | 3 grains, 30+ methods | ✅ Yes |
| Costing | 0 | 5 grains, 40+ methods | ✅ Yes |
| Reporting | 0 | 6 grains, 40+ methods | ✅ Yes |
| Delivery Platforms | 0 | 4 grains, 30+ methods | ✅ Yes |
| Fiscalization | 0 | 4 grains, 20+ methods | ✅ Yes |
| Accounting | 0 | 1 grain, 20+ methods | ✅ Yes |
| Alerts | 1 | 2 grains, 20+ methods | ✅ Yes |

**Total: 48 grains with 500+ methods ready for API exposure**

---

## Missing from DarkVelocity Grains (Clover Has)

This section identifies capabilities Clover provides that are **not implemented at the grain level** in DarkVelocity.

### 1. Menu Items - Missing Fields

| Clover Field | Purpose | DarkVelocity Gap |
|--------------|---------|------------------|
| `priceType` | FIXED, PER_UNIT, VARIABLE pricing | Only fixed `Price` decimal |
| `unitName` | Unit for per-unit pricing ("lb", "oz", "hr") | ❌ Not implemented |
| `alternateName` | Alternative display name | ❌ Not implemented |
| `code` | Internal item code (separate from SKU) | ❌ Not implemented (only `Sku`) |
| `colorCode` | Hex color for item display | ❌ Items don't have color (categories do) |
| `priceWithoutVat` | Base price for VAT merchants | ❌ Not implemented |
| `autoManage` | Auto-unavailable when stock=0 | ❌ Not implemented |
| `available` | Temporary unavailability flag | ❌ Only `IsActive` (permanent) |
| `isRevenue` | Counts toward revenue | ❌ Not implemented |
| `defaultTaxRates` | Tax rate associations | ❌ Not on item (site-level only) |
| `cost` | Item cost on item itself | Uses `TheoreticalCost` from recipes instead |

**DarkVelocity has:** `MenuItemId`, `CategoryId`, `Name`, `Description`, `Price`, `ImageUrl`, `Sku`, `IsActive`, `TrackInventory`, `TheoreticalCost`, `Modifiers`

### 2. Item Variants / Attributes

| Clover Feature | Purpose | DarkVelocity Gap |
|----------------|---------|------------------|
| Item Groups | Parent item for variants | ❌ Not implemented |
| Attributes | Variant dimensions (Size, Color) | ❌ Not implemented |
| Options | Attribute values (Small, Medium, Large) | ❌ Not implemented |
| Variant SKUs | Auto-generated variant SKUs | ❌ Not implemented |

**Impact:** Cannot create "T-Shirt" with Size (S/M/L) and Color (Red/Blue) variants that auto-generate 6 items.

### 3. Customer - Missing Fields

| Clover Field | Purpose | DarkVelocity Gap |
|--------------|---------|------------------|
| `marketingAllowed` | General marketing consent | Has `EmailOptIn`/`SmsOptIn` but no general flag |
| `cards` | Stored payment methods | ❌ No card-on-file on customer |
| Multiple emails | Array of email addresses | ❌ Single `Email` only |
| Multiple phones | Array of phone numbers | ❌ Single `Phone` only |
| `metadata.businessName` | B2B customer company | ❌ Not implemented |
| `customerSince` | Customer creation date | Has `CreatedAt` ✓ |

**DarkVelocity has better:** Loyalty integration, rewards, referrals, dietary preferences, allergens, customer segmentation, visit stats

### 4. Payment - Missing Capabilities

| Clover Feature | Purpose | DarkVelocity Gap |
|----------------|---------|------------------|
| Signature capture | Store customer signature | ❌ Not implemented |
| Card-on-file | Tokenized cards per customer | ❌ Not implemented |
| Increment auth | Increase pre-auth amount | ❌ Not implemented |
| Device tip read | Read tip from terminal | ❌ Not implemented |
| Tender types | Custom payment methods CRUD | ❌ Enum only |

**DarkVelocity has:** `AdjustTipAsync` for tip after payment, but no device interaction for tip entry

### 5. Merchant/Organization - Missing Features

| Clover Feature | Purpose | DarkVelocity Gap |
|----------------|---------|------------------|
| Opening hours | Business hours per day | ❌ Not implemented |
| Tip suggestions | Suggested tip percentages | ❌ Not implemented |
| Properties | Key-value merchant settings | ❌ Not implemented |
| Gateway config | Payment gateway settings | ❌ Not exposed |
| Order types CRUD | Custom order types | ❌ Enum only (`DineIn`, `Takeout`, etc.) |
| Tender types CRUD | Custom payment methods | ❌ Enum only |

### 6. Device Interactions - Missing

| Clover Feature | Purpose | DarkVelocity Gap |
|----------------|---------|------------------|
| `POST /device/ping` | Check device online | ❌ Not implemented |
| `POST /device/message` | Display message on device | ❌ Not implemented |
| `POST /device/signature` | Capture signature | ❌ Not implemented |
| `POST /device/tip` | Prompt for tip amount | ❌ Not implemented |
| `POST /device/confirmation` | Yes/No prompt | ❌ Not implemented |
| `POST /device/input` | Custom text input | ❌ Not implemented |
| `POST /device/custom_activity` | Launch custom app | ❌ Not implemented |
| `POST /device/cancel` | Cancel current operation | ❌ Not implemented |
| `POST /device/reset` | Reset device state | ❌ Not implemented |

**Note:** These are Clover-specific terminal interactions. DarkVelocity uses standard payment gateways, so some don't apply.

### 7. Printing - Missing

| Clover Feature | Purpose | DarkVelocity Gap |
|----------------|---------|------------------|
| Print event status | Get status by print ID | ❌ Not implemented |
| Receipt delivery | Track receipt sent | ❌ Not implemented |

### 8. Notifications - Missing

| Clover Feature | Purpose | DarkVelocity Gap |
|----------------|---------|------------------|
| App notifications | Push to specific device | ❌ Not implemented (has SignalR broadcast) |

### 9. Tokenization - Missing

| Clover Feature | Purpose | DarkVelocity Gap |
|----------------|---------|------------------|
| Card tokenization | `POST /v1/tokens` | ❌ Not implemented |
| Apple Pay tokens | Mobile wallet tokens | ❌ Not implemented |
| Google Pay tokens | Mobile wallet tokens | ❌ Not implemented |
| ACH tokens | Bank account tokens | ❌ Not implemented |

**Note:** Tokenization is typically handled by payment gateway (Stripe, Adyen, etc.), not the POS system.

### 10. Recurring/Subscriptions - Missing

| Clover Feature | Purpose | DarkVelocity Gap |
|----------------|---------|------------------|
| Plans | Subscription plan definitions | ❌ Not implemented |
| Subscriptions | Customer subscription management | ❌ Not implemented |
| Recurring billing | Automated charge schedules | ❌ Not implemented |

### 11. App Billing - N/A

| Clover Feature | Purpose | DarkVelocity Gap |
|----------------|---------|------------------|
| Metered events | Usage-based app billing | N/A - Not an app marketplace |
| Billing info | App subscription status | N/A - Not an app marketplace |

---

## Gap Analysis Summary

### Critical Gaps (Affects core POS workflows)

| Gap | Impact | Effort |
|-----|--------|--------|
| Item variants/attributes | Can't sell products with options (clothing, etc.) | High |
| Price types (PER_UNIT, VARIABLE) | Can't do weigh scale or open-price items | Medium |
| Item availability flag | Can't temporarily 86 items without deactivating | Low |
| Opening hours | No business hour validation | Low |

### Important Gaps (Common use cases)

| Gap | Impact | Effort |
|-----|--------|--------|
| Card-on-file / tokenization | No stored payment methods | Medium-High |
| Tip suggestions configuration | No configurable tip presets | Low |
| Signature capture | No signature on payments | Medium |
| Multiple customer emails/phones | Limited customer contact info | Low |

### Minor Gaps (Edge cases / Clover-specific)

| Gap | Impact | Effort |
|-----|--------|--------|
| Device display/input | Clover hardware specific | N/A |
| App billing | Marketplace specific | N/A |
| Recurring payments | B2B/subscription use case | Medium |

---

## Payload Field Comparison

### MenuItem: Clover vs DarkVelocity

```
Clover                          DarkVelocity
─────────────────────────────── ───────────────────────────────
id                              MenuItemId ✓
name                            Name ✓
alternateName                   ❌ Missing
price                           Price ✓
priceType                       ❌ Missing (fixed only)
unitName                        ❌ Missing
cost                            TheoreticalCost (from recipe)
sku                             Sku ✓
code                            ❌ Missing
hidden                          ❌ Missing
available                       ❌ Missing
isRevenue                       ❌ Missing
colorCode                       ❌ Missing (category has Color)
priceWithoutVat                 ❌ Missing
defaultTaxRates                 ❌ Missing
autoManage                      ❌ Missing
modifiedTime                    ❌ Missing (has Version)
itemGroup                       ❌ Missing
categories                      CategoryId ✓
modifierGroups                  Modifiers ✓
tags                            ❌ Missing
─                               ImageUrl ✓ (DV extra)
─                               Description ✓ (DV extra)
─                               TrackInventory ✓ (DV extra)
─                               RecipeId ✓ (DV extra)
─                               AccountingGroupId ✓ (DV extra)
```

### Customer: Clover vs DarkVelocity

```
Clover                          DarkVelocity
─────────────────────────────── ───────────────────────────────
id                              Id ✓
firstName                       FirstName ✓
lastName                        LastName ✓
emailAddresses[]                Contact.Email (single) ⚠️
phoneNumbers[]                  Contact.Phone (single) ⚠️
addresses[]                     Contact.Address ✓
cards[]                         ❌ Missing
marketingAllowed                EmailOptIn + SmsOptIn (separate) ⚠️
metadata.businessName           ❌ Missing
customerSince                   CreatedAt ✓
─                               DisplayName ✓ (DV extra)
─                               DateOfBirth ✓ (DV extra)
─                               Anniversary ✓ (DV extra)
─                               Gender ✓ (DV extra)
─                               AvatarUrl ✓ (DV extra)
─                               Preferences ✓ (DV extra)
─                               Tags ✓ (DV extra)
─                               Source ✓ (DV extra)
─                               ExternalIds ✓ (DV extra)
─                               Loyalty ✓ (DV extra - built-in)
─                               Rewards ✓ (DV extra)
─                               Stats ✓ (DV extra)
─                               Notes ✓ (DV extra)
─                               ReferralCode ✓ (DV extra)
```

### Payment: Clover vs DarkVelocity

```
Clover                          DarkVelocity
─────────────────────────────── ───────────────────────────────
id                              Id ✓
order.id                        OrderId ✓
amount                          Amount ✓
tipAmount                       TipAmount ✓
tender.label                    Method (enum) ⚠️
result                          Status ✓
employee.id                     CashierId ✓
cardTransaction.authCode        AuthorizationCode ✓
cardTransaction.last4           CardInfo.MaskedNumber ✓
cardTransaction.cardType        CardInfo.Brand ✓
cardTransaction.entryType       CardInfo.EntryMethod ✓
signature                       ❌ Missing
externalPaymentId               GatewayReference ✓
─                               TotalAmount ✓ (DV extra)
─                               Currency ✓ (DV extra)
─                               CustomerId ✓ (DV extra)
─                               DrawerId ✓ (DV extra)
─                               AvsResult ✓ (DV extra)
─                               CvvResult ✓ (DV extra)
─                               AmountTendered ✓ (DV extra - cash)
─                               ChangeGiven ✓ (DV extra - cash)
─                               GiftCardId ✓ (DV extra)
─                               LoyaltyPointsRedeemed ✓ (DV extra)
─                               Refunds[] ✓ (DV extra)
─                               BatchId ✓ (DV extra)
```

---

## Features DarkVelocity Has That Clover Lacks

| Feature | Description |
|---------|-------------|
| **Multi-site hierarchy** | Organization → Site model for franchise/chain management |
| **Device authorization flow** | RFC 8628 Device Flow for secure device onboarding |
| **PIN-based staff login** | Quick staff authentication on shared devices |
| **Kitchen Display System** | First-class KDS support with station selection |
| **Sales periods** | Daily business period tracking and reconciliation |
| **Recipe costing** | Cost calculation for menu items |
| **Procurement** | Purchase order and supplier management |
| **Booking/Reservations** | Table reservation system |
| **Event sourcing** | Full audit trail via Orleans grain events |
| **Offline support** | PWA with sql.js for offline operation |

---

## Priority Gaps to Address

### High Priority (Core POS functionality)

1. **Order Management APIs** - Enable disabled endpoints
2. **Payment Processing APIs** - Enable disabled endpoints
3. **Menu/Inventory APIs** - Enable disabled endpoints
4. **Customer APIs** - Enable disabled endpoints

### Medium Priority (Operational features)

5. **Employee Management** - Enable labor APIs
6. **Reporting** - Enable sales report APIs
7. **Tip Management** - Add tip adjustment to payments
8. **Discount Management** - CRUD for discounts

### Lower Priority (Advanced features)

9. **Ecommerce/Tokenization** - Card-on-file, online ordering
10. **Recurring Payments** - Subscriptions and plans
11. **Notifications** - Push notifications to devices
12. **Device Display Control** - On-device prompts and messages

---

## Architectural Differences

| Aspect | Clover | DarkVelocity |
|--------|--------|--------------|
| **State Management** | Traditional database | Orleans virtual actors with event sourcing |
| **Scalability** | Horizontal via load balancer | Orleans silo clustering with grain activation |
| **Real-time** | Webhooks (pull-based) | SignalR (push-based) |
| **Offline** | Limited | Full PWA with local database |
| **Multi-tenancy** | Single merchant per app | Organization hierarchy |
| **Authorization** | Role-based | Relationship-based (SpiceDB planned) |

---

## Recommendations

### Short-term (Enable existing code)

1. Review and enable disabled API endpoints in `Program.cs`
2. Ensure grain interfaces match API contracts
3. Add OpenAPI/Swagger documentation

### Medium-term (Feature parity)

1. Implement missing CRUD operations for core resources
2. Add tip adjustment to payment workflow
3. Implement bulk operations for inventory
4. Add discount management endpoints

### Long-term (Differentiation)

1. Leverage event sourcing for advanced reporting/analytics
2. Build on offline-first PWA capabilities
3. Expand KDS and kitchen workflow features
4. Implement relationship-based authorization with SpiceDB

---

## Sources

- [Clover API Reference](https://docs.clover.com/dev/reference/api-reference-overview)
- [Clover REST API Tutorials](https://docs.clover.com/dev/docs/clover-rest-api-index)
- DarkVelocity POS codebase analysis (`src/DarkVelocity.Host/Program.cs`)
