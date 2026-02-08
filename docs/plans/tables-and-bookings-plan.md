# Tables & Bookings -- Implementation Plan

> **Date:** February 2026
> **Domain Completeness:** ~80-85% (was documented as 60%)
> **Grains:** 11 production grains + 1 stream subscriber

---

## Current State

The domain is substantially more complete than previously documented. Here's what actually exists:

### Production-Ready Grains

| Grain | What It Does | Status |
|-------|-------------|--------|
| **BookingGrain** | Full lifecycle with 13 event types, deposits, no-show, order linking | Done |
| **TableGrain** | CRUD, status management, occupancy, table combinations, cleaning flow | Done |
| **FloorPlanGrain** | Sections, table layout, canvas dimensions, background images | Done |
| **BookingSettingsGrain** | Operating hours, slot intervals, party size limits, deposit rules, blocked dates | Done |
| **BookingCalendarGrain** | Daily calendar view, hourly slots, cover counts, table allocations, availability | Done |
| **WaitlistGrain** | FIFO queue, quoted waits, notify/seat/remove, average wait tracking | Done |
| **TableAssignmentOptimizerGrain** | Scored recommendations, server workload balancing, table combinations | Done |
| **TurnTimeAnalyticsGrain** | 10k-record rolling window, stats by party size/day/time, active seating monitoring | Done |
| **BookingAccountingSubscriber** | Deposit journal entries (paid/applied/refunded/forfeited) | Done |

### Partially Complete Grains

| Grain | What's Done | What's Missing |
|-------|------------|----------------|
| **EnhancedWaitlistGrain** | Priority for returning customers, SMS notifications, turn time data, table suitability matching | Needs integration testing, notification delivery depends on System domain |
| **BookingNotificationSchedulerGrain** | Scheduling logic, Orleans reminders registered, notification history tracking | `ReceiveReminder()` callback is stubbed -- notifications are scheduled but never actually sent |
| **NoShowDetectionGrain** | Registration, grace period settings, history tracking | `ReceiveReminder()` callback is stubbed -- registered bookings are never actually checked |

### What's Not There At All

| Feature | Notes |
|---------|-------|
| Public-facing booking widget | No online booking flow for guests |
| Google/social booking integration | No Agentic or Reserve with Google support |
| SMS/email confirmation delivery | Scheduler exists but NotificationGrain uses stub services |
| Deposit payment link generation | Deposits recorded but no payment link sent to guest |
| Automated deposit-to-order application | Event defined but never triggered |
| Real-time floor plan updates via SignalR | State supports it, no push to POS clients |
| Customer preference learning | No "prefers window seat" or "usually orders wine" from history |
| Multi-venue booking | Can't book across sites in one flow |

---

## Plan

### Phase 1: Wire Up What's Already Built (1-2 weeks)

The biggest bang-for-buck is connecting the grains that are 90% done but have stubbed reminder callbacks.

#### 1.1 NoShowDetectionGrain -- Complete the Reminder Callback
**Effort:** 2-3 days

The grain registers Orleans reminders at `bookingTime + gracePeriod` but the `ReceiveReminder()` method doesn't do anything. Wire it to:

1. Check if booking status is still `Confirmed` (guest hasn't arrived)
2. Call `BookingGrain.MarkNoShowAsync()` if `AutoMarkNoShow` is enabled
3. Call `BookingGrain.ForfeitDepositAsync()` if `ForfeitDepositOnNoShow` is enabled
4. Publish a no-show event for downstream consumers (customer history, reporting)

Tests:
- Booking past grace period with no arrival -> marked no-show
- Booking past grace period but guest arrived -> not marked
- No-show with deposit -> deposit forfeited
- No-show without deposit -> just marked
- AutoMarkNoShow disabled -> only records check, doesn't mark

#### 1.2 BookingNotificationSchedulerGrain -- Complete the Reminder Callback
**Effort:** 3-4 days

The grain schedules reminders for confirmation, 24h, 2h, and follow-up notifications but `ReceiveReminder()` doesn't dispatch them. Wire it to:

1. Look up the scheduled notification by reminder name
2. Call `NotificationGrain.SendAsync()` with the appropriate template and channel
3. Mark the notification as sent (or record error)
4. For follow-up notifications, include a link to leave a review or rebook

This depends on the System domain's NotificationGrain -- which exists and is functional but uses stub email/SMS services. For Phase 1, use the stubs (logs instead of sends). Real delivery comes in Phase 3.

Tests:
- Confirmed booking -> confirmation notification dispatched
- 24h before booking -> reminder dispatched
- Notification for cancelled booking -> cancelled, not sent
- Notification failure -> error recorded, not retried (for now)

#### 1.3 Deposit-to-Order Application
**Effort:** 1-2 days

`BookingDepositAppliedToOrderEvent` is defined but nothing triggers it. When a booking is linked to an order via `LinkToOrderAsync()`, automatically apply the deposit as a credit on the order's payment.

Wire:
1. `BookingGrain.LinkToOrderAsync()` checks if deposit is paid
2. If so, publish `BookingDepositAppliedToOrderEvent` to the booking-events stream
3. BookingAccountingSubscriber already handles this event (debits deposit liability, credits sales)
4. PaymentGrain on the order should receive the deposit amount as a pre-payment

Tests:
- Link order to booking with paid deposit -> deposit applied
- Link order to booking with no deposit -> no-op
- Link order to booking with waived deposit -> no-op

#### 1.4 Real-Time Floor Plan via SignalR
**Effort:** 2-3 days

Table status changes (seat, clear, dirty, clean, block) should push updates to connected POS devices so the floor plan view stays current without polling.

Wire:
1. After `TableGrain.SeatAsync()`, `ClearAsync()`, `MarkDirtyAsync()`, `MarkCleanAsync()` -- push a `TableStatusChanged` message via SignalR hub
2. Create a `FloorPlanHub` that POS clients subscribe to by site
3. Message payload: `{ tableId, status, occupancy, serverName }`

Tests:
- Table seated -> SignalR message received by connected clients
- Table cleared -> status update pushed
- Multiple clients on same site -> all receive update

---

### Phase 2: Competitive Parity Features (2-3 weeks)

#### 2.1 Online Booking Widget / Public API
**Effort:** 5-7 days

No competitor survives without online booking. Guests need a public-facing flow. This doesn't require a full frontend build -- expose a public API that a whitelabel widget or third-party booking page can consume.

Endpoints (unauthenticated, rate-limited):
```
GET  /api/public/sites/{siteSlug}/availability?date=&partySize=
POST /api/public/sites/{siteSlug}/bookings
GET  /api/public/bookings/{confirmationCode}
POST /api/public/bookings/{confirmationCode}/cancel
```

The grains already support all of this -- BookingSettingsGrain.GetAvailabilityAsync(), BookingGrain.RequestAsync(), etc. The work is:
1. Create public endpoints with site slug lookup (no auth required)
2. Add rate limiting (per IP, per site)
3. Add CAPTCHA or similar bot protection
4. Return availability in a format suitable for calendar rendering
5. Send confirmation email/SMS after booking (via notification scheduler)
6. Generate a manage-my-booking link with confirmation code

Tests:
- Public availability check returns slots
- Public booking request creates booking and sends confirmation
- Confirmation code lookup returns booking status
- Rate limiting prevents abuse
- Blocked dates return no availability

#### 2.2 Customer Preference Learning
**Effort:** 3-4 days

OpenTable and Fresha track guest preferences from history. DarkVelocity has `CustomerVisitHistoryGrain` in the Customers domain but the Booking domain doesn't use it.

Wire:
1. On `BookingGrain.RecordDepartureAsync()`, record visit details to `CustomerVisitHistoryGrain` (table preferences, party size patterns, occasion)
2. On `BookingGrain.RequestAsync()`, fetch customer history if `customerId` provided
3. `TableAssignmentOptimizerGrain.GetRecommendationsAsync()` should factor in customer's table preferences (previously assigned tables, section preferences)
4. Store preference signals: "sat at window 3 of last 5 visits" -> suggest window tables

Tests:
- Customer with history of window tables -> window table scored higher
- New customer -> no preference applied
- Customer with allergy tag -> tag visible in booking details

#### 2.3 Waitlist-to-Booking Promotion with Notification
**Effort:** 2-3 days

`EnhancedWaitlistGrain.PromoteToBookingAsync()` exists but isn't connected end-to-end. Complete the flow:

1. When a table becomes available, call `FindNextSuitableEntryAsync()` to find a match
2. Create a booking via `BookingGrain.RequestAsync()` and auto-confirm
3. Send "your table is ready" notification via the notification scheduler
4. If guest doesn't respond within `NotificationResponseTimeout` (10 min default), expire and try next entry

Tests:
- Table clears -> next suitable waitlist entry promoted
- Guest notified -> accepts within timeout -> seated
- Guest doesn't respond -> expired, next entry tried
- No suitable entry -> no action

---

### Phase 3: Differentiation (2-3 weeks)

#### 3.1 Real Notification Delivery
**Effort:** 5-7 days

This is a System domain dependency, not a Bookings task, but bookings are the most visible consumer. Replace stub notification services with real ones:

1. Email via SendGrid or AWS SES
2. SMS via Twilio or MessageBird
3. Push via Firebase Cloud Messaging

Booking-specific templates:
- Confirmation: "Your booking at {venue} for {partySize} on {date} at {time} is confirmed. Ref: {code}"
- Reminder (24h): "Reminder: your booking tomorrow at {venue}..."
- Reminder (2h): "Your table at {venue} is in 2 hours..."
- Table ready (waitlist): "Your table is ready at {venue}! Please check in within {timeout} minutes."
- Follow-up: "Thanks for visiting {venue}! We'd love your feedback."
- No-show deposit forfeit: "Your booking was marked as a no-show. Your deposit of {amount} has been forfeited per our cancellation policy."

#### 3.2 Deposit Payment Links
**Effort:** 3-4 days

When `BookingGrain.RequireDepositAsync()` fires, send the guest a payment link. This requires:

1. Create a `PaymentIntentGrain` for the deposit amount
2. Generate a hosted payment page URL (Stripe Checkout Session or equivalent)
3. Include the payment link in the deposit-required notification
4. On successful payment webhook, call `BookingGrain.RecordDepositPaymentAsync()`
5. On payment expiry, optionally cancel the booking

This bridges the Bookings and Payment Processors domains.

#### 3.3 Google Reserve Integration
**Effort:** 5-7 days

Google Reserve with Google lets guests book directly from Google Search/Maps. This is how OpenTable generates 1.7B diners/year. The integration:

1. Implement the Google Reserve API (Maps Booking API) as an inbound channel
2. Map Google availability requests to `BookingSettingsGrain.GetAvailabilityAsync()`
3. Map Google booking requests to `BookingGrain.RequestAsync()`
4. Map Google cancellation requests to `BookingGrain.CancelAsync()`
5. Push status updates back to Google on confirmation/cancellation

This could also be modeled as an External Channel (like Deliverect) with a Google Reserve adapter.

#### 3.4 Experiential Dining / Events
**Effort:** 5-7 days

Toast and OpenTable support "experiences" -- chef's tables, tasting menus, themed nights. These are bookable events with:

1. Fixed capacity (e.g., 12 seats for chef's table)
2. Fixed menu (pre-selected or prix fixe)
3. Different pricing than standard dining
4. Time-bound (specific dates/times, not recurring)
5. Often require full pre-payment, not just deposit

This could be a new `ExperienceGrain` or an extension of `BookingGrain` with an `ExperienceId` link:

```
ExperienceGrain
├── Name, Description, Images
├── Capacity (min/max guests)
├── Schedule (specific dates and times)
├── Menu (linked MenuDefinition or inline)
├── Pricing (per person, flat rate, or tiered)
├── PaymentPolicy (full prepayment, deposit, none)
├── Status (Draft, Published, SoldOut, Completed, Cancelled)
└── BookingIds (linked bookings)
```

---

### Phase 4: Advanced Analytics & Intelligence (1-2 weeks)

#### 4.1 Predictive Availability
**Effort:** 3-5 days

Use `TurnTimeAnalyticsGrain` data to predict when currently-occupied tables will free up:

1. For each occupied table, estimate remaining time based on historical turn time for that party size, day, and time of day
2. Show "estimated available at {time}" in the availability response
3. Allow overbooking within confidence intervals (e.g., "90% chance table 5 frees up by 8:15pm")
4. Factor weather, events, and day-of-week patterns into predictions

#### 4.2 No-Show Risk Scoring
**Effort:** 2-3 days

Use `NoShowDetectionGrain` history + customer data to score no-show risk:

1. Customer's personal no-show rate
2. Day-of-week patterns (Friday/Saturday higher no-show)
3. Party size (larger parties more likely to no-show)
4. Booking lead time (far-in-advance bookings more likely to cancel)
5. Deposit status (deposits dramatically reduce no-shows)

Use the risk score to:
- Suggest deposits for high-risk bookings
- Adjust overbooking strategy
- Prioritize confirmation calls for high-risk bookings

#### 4.3 Revenue Optimization
**Effort:** 3-4 days

Combine turn time analytics, cover counts, and order data to optimize seating:

1. Revenue-per-seat-hour metric: which tables/times generate the most revenue?
2. Duration management: suggest shorter booking durations for high-demand slots
3. Party size optimization: recommend which party sizes to prioritize for which slots
4. Pace alerts: notify managers when the evening is filling faster/slower than forecast

---

## Summary

| Phase | Effort | Impact | Dependency |
|-------|--------|--------|------------|
| **Phase 1: Wire up existing grains** | 1-2 weeks | High -- turns 80% into 90%+ | None |
| **Phase 2: Competitive parity** | 2-3 weeks | High -- online booking is table stakes | Phase 1 (notifications) |
| **Phase 3: Differentiation** | 2-3 weeks | Medium-High -- real notifications, Google, experiences | System domain (notification services), Payment Processors |
| **Phase 4: Analytics & intelligence** | 1-2 weeks | Medium -- data-driven operations | Phase 1 (turn time data accumulation) |

**Phase 1 is the priority.** The grains are built but disconnected. Wiring up the reminder callbacks, deposit application, and SignalR push completes the operational flow without writing new domain logic.
