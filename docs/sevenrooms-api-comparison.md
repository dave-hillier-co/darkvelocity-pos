# SevenRooms API vs DarkVelocity POS API Comparison

## Executive Summary

This document compares the SevenRooms API (a leading restaurant reservation and guest management platform) with the DarkVelocity POS API to identify feature gaps, architectural differences, and potential integration opportunities.

## SevenRooms API Overview

SevenRooms is a cloud-based reservation and guest management platform for restaurants, hotels, and hospitality venues. Their API ecosystem includes:

- **Reservations API** - Core booking and reservation management
- **Concierge API** - For booking channel partners
- **Webhook API** - Real-time event notifications
- **Public Widget API** - Guest-facing availability queries

### Base URL & Versioning
```
https://api.sevenrooms.com/2_2/
```

### Authentication
- Token-based authentication via `/auth` endpoint
- Requires `client_id` and `client_secret`
- `venue_group_id` for multi-venue operations

---

## API Endpoint Comparison

### 1. Reservations / Bookings

| Feature | SevenRooms | DarkVelocity |
|---------|------------|--------------|
| **Create Reservation** | Via Concierge API (partners only) | `POST /api/orgs/{orgId}/sites/{siteId}/bookings` |
| **Get Reservation** | `GET /2_2/reservations?updated_since={date}` | `GET /api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}` |
| **List Reservations** | `GET /2_2/reservations/export?updated_since={date}` | ❌ Not implemented |
| **Confirm Booking** | Via status update | `POST /api/.../bookings/{bookingId}/confirm` |
| **Cancel Booking** | Via status update | `POST /api/.../bookings/{bookingId}/cancel` |
| **Check-in Guest** | Auto via POS integration | `POST /api/.../bookings/{bookingId}/checkin` |
| **Seat Guest** | Status: 'assigned' → 'seated' | `POST /api/.../bookings/{bookingId}/seat` |
| **Availability Query** | `GET /api-yoa/availability/widget/range` | ❌ Not implemented |

**Gap Analysis:**
- ✅ DarkVelocity has explicit workflow states (confirm, cancel, checkin, seat)
- ❌ DarkVelocity lacks: availability queries, bulk export, waitlist management

### 2. Guest / Customer Management

| Feature | SevenRooms | DarkVelocity |
|---------|------------|--------------|
| **Create Customer** | Auto-created from reservations | `POST /api/orgs/{orgId}/customers` |
| **Get Customer** | `GET /2_2/clients/{client_id}` | `GET /api/orgs/{orgId}/customers/{customerId}` |
| **Update Customer** | Via profile enrichment | `PATCH /api/orgs/{orgId}/customers/{customerId}` |
| **Guest Tags** | ✅ Supported | ❌ Not implemented |
| **Guest Preferences** | ✅ Comprehensive profiles | ❌ Not implemented |
| **Visit History** | ✅ Aggregated automatically | ❌ Not implemented |
| **Loyalty Program** | Via integrations | ✅ Native (`/loyalty/enroll`, `/earn`, `/redeem`) |
| **Rewards** | Via integrations | ✅ Native (`/rewards`) |

**Gap Analysis:**
- ✅ DarkVelocity has native loyalty/rewards (SevenRooms relies on integrations)
- ❌ DarkVelocity lacks: guest tags, preferences, visit history aggregation

### 3. Orders & Payments

| Feature | SevenRooms | DarkVelocity |
|---------|------------|--------------|
| **Create Order** | Via POS integration only | `POST /api/.../orders` |
| **Get Order** | Via POS sync | `GET /api/.../orders/{orderId}` |
| **Order Lines** | Synced from POS | `POST/GET/DELETE /api/.../orders/{orderId}/lines` |
| **Send to Kitchen** | POS-driven | `POST /api/.../orders/{orderId}/send` |
| **Order Totals** | Reflected in check | `GET /api/.../orders/{orderId}/totals` |
| **Apply Discount** | POS handles | `POST /api/.../orders/{orderId}/discounts` |
| **Close/Void Order** | POS finalizes | `POST /api/.../orders/{orderId}/close|void` |
| **Payments** | Via payment processors | Full payment lifecycle API |
| **Cash Payment** | N/A | `POST /api/.../payments/{id}/complete-cash` |
| **Card Payment** | Stripe/Adyen integration | `POST /api/.../payments/{id}/complete-card` |
| **Refunds** | Via processor | `POST /api/.../payments/{id}/refund` |

**Gap Analysis:**
- ✅ DarkVelocity has comprehensive native order/payment APIs
- SevenRooms focuses on reservation-to-order linking, not order creation

### 4. Menu Management

| Feature | SevenRooms | DarkVelocity |
|---------|------------|--------------|
| **Menu Categories** | ❌ Not in API | `POST/GET /api/.../menu/categories` |
| **Menu Items** | ❌ Not in API | `POST/GET/PATCH /api/.../menu/items` |

**Gap Analysis:**
- ✅ DarkVelocity has native menu management
- SevenRooms delegates menu to integrated POS systems

### 5. Venue / Site Management

| Feature | SevenRooms | DarkVelocity |
|---------|------------|--------------|
| **Multi-Venue** | `venue_group_id` parameter | Organization → Sites hierarchy |
| **Create Site** | Admin only | `POST /api/orgs/{orgId}/sites` |
| **List Sites** | Via admin | `GET /api/orgs/{orgId}/sites` |
| **Open/Close Site** | N/A | `POST /api/.../sites/{siteId}/open|close` |
| **Timezone/Currency** | Per-venue config | Per-site configuration |

**Gap Analysis:**
- ✅ Both support multi-venue/multi-site operations
- ✅ DarkVelocity has explicit open/close operations for sales periods

### 6. Table Management

| Feature | SevenRooms | DarkVelocity |
|---------|------------|--------------|
| **Table Assignment** | ✅ Core feature | ❌ Not implemented |
| **Table Status** | ✅ Real-time | ❌ Not implemented |
| **Floor Plan** | ✅ Visual management | ❌ Not implemented |
| **Seating Optimization** | ✅ AI-powered | ❌ Not implemented |

**Gap Analysis:**
- ❌ DarkVelocity lacks table management (significant gap for full-service restaurants)

### 7. Waitlist Management

| Feature | SevenRooms | DarkVelocity |
|---------|------------|--------------|
| **Join Waitlist** | ✅ Virtual waitlist | ❌ Not implemented |
| **Queue Position** | ✅ Real-time updates | ❌ Not implemented |
| **Wait Time Estimates** | ✅ AI-powered | ❌ Not implemented |
| **SMS Notifications** | ✅ Twilio integration | ❌ Not implemented |

**Gap Analysis:**
- ❌ DarkVelocity lacks waitlist features entirely

### 8. Inventory Management

| Feature | SevenRooms | DarkVelocity |
|---------|------------|--------------|
| **Track Inventory** | ❌ Not in scope | ✅ Full inventory API |
| **Receive Stock** | ❌ | `POST /api/.../inventory/{id}/receive` |
| **Consume Stock** | ❌ | `POST /api/.../inventory/{id}/consume` |
| **Adjust Quantity** | ❌ | `POST /api/.../inventory/{id}/adjust` |
| **Check Levels** | ❌ | `GET /api/.../inventory/{id}/level` |

**Gap Analysis:**
- ✅ DarkVelocity has comprehensive inventory management
- SevenRooms focuses on guest experience, not inventory

### 9. Employee Management

| Feature | SevenRooms | DarkVelocity |
|---------|------------|--------------|
| **Employee CRUD** | Admin only | `POST/GET/PATCH /api/.../employees` |
| **Clock In/Out** | ❌ | `POST /api/.../employees/{id}/clock-in|out` |
| **Role Assignment** | Admin config | `POST/DELETE /api/.../employees/{id}/roles` |

**Gap Analysis:**
- ✅ DarkVelocity has time tracking and role management
- SevenRooms handles staff through admin interface only

### 10. Webhooks & Real-Time Events

| Feature | SevenRooms | DarkVelocity |
|---------|------------|--------------|
| **Webhook Events** | ✅ Reservation status changes | ❌ Not implemented |
| **Real-Time Updates** | Via webhook subscriptions | SignalR (different pattern) |
| **Event Types** | Status change, party size, table assignment, cancellation | N/A |

**Gap Analysis:**
- ❌ DarkVelocity lacks webhook outbound notifications
- ✅ DarkVelocity uses SignalR for real-time (push vs. webhook pattern)

---

## Architectural Differences

| Aspect | SevenRooms | DarkVelocity |
|--------|------------|--------------|
| **Primary Focus** | Guest experience & reservations | Full POS operations |
| **Multi-Tenancy** | Venue Groups | Organizations → Sites |
| **State Management** | External (relies on POS) | Orleans virtual actors with event sourcing |
| **API Format** | Standard REST JSON | HAL+JSON with hypermedia links |
| **Authentication** | OAuth2 (client credentials) | OAuth2 + Device flow + PIN auth |
| **Offline Support** | N/A (cloud-only) | PWA with sql.js |

---

## Feature Gap Summary

### DarkVelocity Has (SevenRooms Lacks):
1. ✅ Native order creation and management
2. ✅ Full payment processing lifecycle
3. ✅ Menu management APIs
4. ✅ Inventory tracking
5. ✅ Employee time tracking
6. ✅ Native loyalty/rewards system
7. ✅ Offline-first PWA support
8. ✅ HAL+JSON hypermedia navigation
9. ✅ Device authorization flow (RFC 8628)

### SevenRooms Has (DarkVelocity Lacks):
1. ❌ Availability queries for bookings
2. ❌ Table management (floor plans, assignments)
3. ❌ Waitlist management
4. ❌ Guest tags and preferences
5. ❌ Visit history aggregation
6. ❌ Webhook event subscriptions
7. ❌ Reservation list/export endpoints
8. ❌ SMS/communication integration
9. ❌ Third-party booking channel integrations (Google, TripAdvisor, etc.)

---

## Integration Recommendations

### For DarkVelocity to Match SevenRooms Capabilities:

1. **Add Availability API**
   ```
   GET /api/orgs/{orgId}/sites/{siteId}/availability
   ?date={date}&party_size={size}&time_slot={time}
   ```

2. **Add Table Management**
   ```
   POST/GET /api/.../tables
   POST /api/.../tables/{tableId}/assign
   POST /api/.../tables/{tableId}/release
   GET /api/.../floor-plan
   ```

3. **Add Waitlist**
   ```
   POST /api/.../waitlist
   GET /api/.../waitlist/{entryId}
   POST /api/.../waitlist/{entryId}/notify
   DELETE /api/.../waitlist/{entryId}
   ```

4. **Add Webhook Subscriptions**
   ```
   POST /api/webhooks/subscriptions
   GET /api/webhooks/subscriptions
   DELETE /api/webhooks/subscriptions/{id}
   ```

5. **Enhance Customer Profiles**
   - Add guest tags
   - Add preferences
   - Add visit history aggregation

6. **Add Booking List Endpoint**
   ```
   GET /api/orgs/{orgId}/sites/{siteId}/bookings
   ?date={date}&status={status}&updated_since={timestamp}
   ```

---

## Conclusion

DarkVelocity POS and SevenRooms serve complementary purposes:

- **SevenRooms** excels at guest acquisition, reservation management, and front-of-house experience
- **DarkVelocity** excels at operational POS functions, payments, inventory, and back-of-house operations

For a complete restaurant management solution, these systems would typically integrate via:
1. SevenRooms webhook → DarkVelocity booking creation
2. DarkVelocity order status → SevenRooms reservation status update
3. Shared customer/guest profile synchronization

---

## Sources

- [SevenRooms Restaurant API and Integrations](https://sevenrooms.com/platform/integrations-apis/)
- [SevenRooms API Tracker](https://apitracker.io/a/sevenrooms)
- [SevenRooms API Credentials Guide](https://academy.airship.co.uk/en/articles/12277047-how-to-get-api-credentials-from-sevenrooms)
- [SevenRooms Reservations & Waitlist](https://sevenrooms.com/platform/reservations-waitlist/)
- [SevenRooms Integration - RedCat](https://www.redcatht.com/helpcentre/seven-rooms-integration)
- [GitHub - SevenRooms Booking Checker](https://github.com/jasonpraful/sevenrooms)
