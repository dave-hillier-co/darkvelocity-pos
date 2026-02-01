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
