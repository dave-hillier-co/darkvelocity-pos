# Tables & Bookings -- Implementation Plan

> **Date:** February 2026 (revised after codebase validation)
> **Domain Completeness:** ~85-90% grain logic done; gaps are in cross-grain wiring and availability intelligence
> **Grains:** 11 grains + 1 stream subscriber

---

## Current State (Validated)

### What's Actually Done

Every grain has a fully implemented `ReceiveReminder()` callback. The earlier assessment that these were stubbed was **wrong**. Here's the accurate picture:

| Grain | Lines | Tests | Status |
|-------|------:|:-----:|--------|
| **TableGrain** | 233 | 17 | Complete. CRUD, status machine, occupancy, combinations, tags, position. |
| **FloorPlanGrain** | 172 | 8 | Complete. Sections, table list, canvas dimensions, active/default. |
| **BookingSettingsGrain** | 128 | ~6 | Complete but availability is **naive** (see below). |
| **BookingGrain** | ~400 | ~20 | Complete. 13 events, full lifecycle, deposits, order linking. |
| **BookingCalendarGrain** | ~250 | ~8 | Complete. Day views, hourly slots, cover counts, table allocations. |
| **WaitlistGrain** | ~150 | ~4 | Complete. FIFO queue, wait estimates, notify/seat/remove. |
| **EnhancedWaitlistGrain** | ~300 | 0 | Complete. Returning customer priority, SMS, turn time data, suitability matching. |
| **TableAssignmentOptimizerGrain** | 408 | 0 | Complete. Scoring algorithm, server workload balancing, table combinations. |
| **TurnTimeAnalyticsGrain** | 345 | ~3 | Complete. 10k rolling window, stats by party/day/time, active monitoring. |
| **NoShowDetectionGrain** | 335 | ~4 | Complete. `ReceiveReminder()` calls `CheckNoShowAsync()` → `ProcessNoShowAsync()` → marks no-show, forfeits deposit, records history, sends notification. |
| **BookingNotificationSchedulerGrain** | 482 | ~2 | Complete. `ReceiveReminder()` dispatches via `SendNotificationAsync()` → checks booking not cancelled → sends via NotificationGrain (email/SMS/push). Templates for all 6 notification types. |
| **BookingAccountingSubscriber** | ~80 | 0 | Complete. Journal entries for deposit paid/applied/refunded/forfeited. |

### What's Actually Wrong

The gaps are **not** stubbed callbacks. They are:

#### 1. Availability is disconnected from actual bookings
`BookingSettingsGrain.GetAvailabilityAsync()` at `TableGrain.cs:457-483` generates time slots but **only checks party size and blocked dates**. It does not:
- Query `BookingCalendarGrain` for how many bookings already exist per slot
- Check actual table capacity
- Factor in turn times or existing occupancy
- Consider table suitability for the party size

This means every slot always shows as "available" if the date isn't blocked and party size is under the max. This is the single biggest functional gap.

#### 2. Table optimizer is never called
`TableAssignmentOptimizerGrain` has a full scoring algorithm (size match, VIP tags, seating preferences, server workload balancing, table combinations) but **nothing calls it**. Tables are registered manually. No endpoint exposes recommendations. No integration exists between booking confirmation and table assignment.

#### 3. Turn time analytics are never fed
`TurnTimeAnalyticsGrain` calculates stats by party size, day, and time of day, tracks active seatings, and flags long-running tables. But **no grain calls `RecordTurnTimeAsync()`** when a guest departs. The data never flows in.

#### 4. No tests for optimizer, enhanced waitlist, or subscriber
Three production grains and one subscriber have **zero test coverage**. These contain scoring algorithms and financial logic (deposit accounting) that need tests.

#### 5. Customer no-show history update is commented out
`NoShowDetectionGrain.ProcessNoShowAsync()` lines 321-327: the customer grain call is commented out with `// Would call customer grain to update no-show count`.

#### 6. Manager notification email is hardcoded
`NoShowDetectionGrain.ProcessNoShowAsync()` line 313: `To: "manager@restaurant.com"` -- should come from site settings.

#### 7. Sections aren't linked to tables
`FloorPlanGrain` tracks sections (name, color, sort order) and table IDs separately, but there's no association between which tables belong to which section.

#### 8. No composite floor plan view endpoint
The event storming spec defines `FloorPlanBookingView` (tables + current booking + next booking + upcoming arrivals + waitlist) but no endpoint assembles this view.

---

## Plan

### Phase 1: Fix Availability (the critical gap)

**Why this is #1:** Every other feature (public booking, optimizer, analytics) depends on availability returning real numbers.

#### 1.1 Table-aware availability calculation

Replace the naive `BookingSettingsGrain.GetAvailabilityAsync()` with a calculation that actually checks capacity.

**Approach:** The availability endpoint (`AvailabilityEndpoints.cs`) should orchestrate across grains rather than relying on BookingSettingsGrain alone:

1. Get settings from `BookingSettingsGrain` (operating hours, slot interval, party size limit, blocked dates)
2. Get existing bookings from `BookingCalendarGrain.GetBookingsAsync()` for the requested date
3. Get table capacity from `FloorPlanGrain.GetTableIdsAsync()` → fan-out to `ITableGrain.GetStateAsync()` for each table
4. For each time slot:
   - Count bookings that overlap (considering duration + turnover buffer)
   - Count available tables that fit the party size
   - Return `IsAvailable`, `AvailableCapacity`, and `AvailableTableIds`

The `BookingCalendarGrain` already has `GetAvailabilityAsync(int partySize)` which generates `AvailableTimeSlot` with `SuggestedTables` -- this logic should be used instead of the settings grain's naive version.

**Tests:**
- Slot with 0 bookings → available
- Slot at max capacity → unavailable
- Slot with overlapping bookings that span across slots → correct overlap detection
- Party of 6 with only 4-top tables → unavailable (or suggests combination)
- Blocked date → all slots unavailable

#### 1.2 Wire BookingCalendarGrain into booking lifecycle

Currently BookingCalendarGrain tracks bookings but **nothing adds them**. Wire:

1. `BookingGrain.RequestAsync()` → call `BookingCalendarGrain.AddBookingAsync()`
2. `BookingGrain.CancelAsync()` → call `BookingCalendarGrain.RemoveBookingAsync()`
3. `BookingGrain.ModifyAsync()` (time change) → call `BookingCalendarGrain.UpdateBookingAsync()`

**Tests:**
- Request booking → appears on calendar
- Cancel booking → removed from calendar, cover count decremented
- Modify booking time → calendar updated

---

### Phase 2: Connect the Optimizer and Analytics

These grains are fully built but isolated. Wire them into the operational flow.

#### 2.1 Auto-register tables with optimizer

When a table is created via `TableGrain.CreateAsync()`, auto-register with `TableAssignmentOptimizerGrain.RegisterTableAsync()`. When deleted, unregister.

**Approach:** The `TableEndpoints.cs` POST handler already calls `CreateAsync()` -- add a call to the optimizer after creation.

**Tests:**
- Create table → registered with optimizer
- Delete table → unregistered
- Update table capacity → optimizer record updated

#### 2.2 Expose optimizer via API

Add endpoints to use the optimizer:

```
GET  /api/orgs/{orgId}/sites/{siteId}/tables/recommendations?partySize=&bookingId=&preference=
POST /api/orgs/{orgId}/sites/{siteId}/tables/auto-assign
GET  /api/orgs/{orgId}/sites/{siteId}/tables/server-workloads
POST /api/orgs/{orgId}/sites/{siteId}/tables/server-sections
```

#### 2.3 Feed turn time analytics on departure

When `BookingGrain.RecordDepartureAsync()` fires:

1. Call `TurnTimeAnalyticsGrain.RecordTurnTimeAsync()` with seated-at/departed-at times
2. Call `TurnTimeAnalyticsGrain.UnregisterSeatingAsync()` to remove from active tracking

When `BookingGrain.SeatGuestAsync()` fires:

1. Call `TurnTimeAnalyticsGrain.RegisterSeatingAsync()` to start tracking

**Tests:**
- Guest departs → turn time recorded
- Turn time stats update after recording
- Active seatings list updates on seat/depart

#### 2.4 Expose analytics via API

```
GET /api/orgs/{orgId}/sites/{siteId}/analytics/turn-times
GET /api/orgs/{orgId}/sites/{siteId}/analytics/turn-times/by-party-size
GET /api/orgs/{orgId}/sites/{siteId}/analytics/turn-times/by-day
GET /api/orgs/{orgId}/sites/{siteId}/analytics/active-seatings
GET /api/orgs/{orgId}/sites/{siteId}/analytics/long-running?threshold=30m
```

---

### Phase 3: Cross-Grain Wiring and Missing Integrations

#### 3.1 Update optimizer on table status changes

When `TableGrain.SeatAsync()` fires → call `TableAssignmentOptimizerGrain.RecordTableUsageAsync()`
When `TableGrain.ClearAsync()` fires → call `TableAssignmentOptimizerGrain.ClearTableUsageAsync()`

This keeps the optimizer's occupancy view current for workload balancing.

#### 3.2 Uncomment customer no-show history

`NoShowDetectionGrain.ProcessNoShowAsync()` lines 321-327: uncomment and wire `ICustomerGrain.RecordNoShowAsync()`. Verify the customer grain has this method or add it.

#### 3.3 Fix hardcoded manager email

Replace `"manager@restaurant.com"` in `NoShowDetectionGrain` line 313 with a lookup from site settings or notification preferences.

#### 3.4 Link sections to tables in FloorPlanGrain

Add `SectionId` to `TableState` (or a mapping in `FloorPlanState`). When tables are assigned to sections, the optimizer can use section-based server assignments.

**State change:** Add `[Id(19)] public Guid? SectionId { get; set; }` to `TableState`.

#### 3.5 Composite floor plan view endpoint

Build the `FloorPlanBookingView` from the event storming spec:

```
GET /api/orgs/{orgId}/sites/{siteId}/floor-plans/{floorPlanId}/live
```

Assembles:
1. All tables from FloorPlanGrain with current status from TableGrain
2. Current booking for each occupied table from BookingGrain
3. Next upcoming booking per table from BookingCalendarGrain
4. Active waitlist entries from WaitlistGrain
5. Upcoming arrivals (next 30 min) from BookingCalendarGrain

This is the "host stand view" -- the single most important screen for front-of-house operations.

#### 3.6 SignalR push for floor plan updates

When table status changes, push to connected POS clients:
1. Create `FloorPlanHub` scoped by site
2. On `TableGrain.SeatAsync()`, `ClearAsync()`, `MarkDirtyAsync()`, `MarkCleanAsync()` → push `TableStatusChanged`
3. On `BookingGrain.RecordArrivalAsync()` → push `GuestArrived` (upcoming arrivals list changes)

---

### Phase 4: Test Coverage

Zero-test grains that contain real logic:

#### 4.1 TableAssignmentOptimizerGrain tests
- Perfect capacity match scores highest
- Larger tables penalized (wasted capacity)
- VIP tag matching boosts score
- Seating preference matching boosts score
- Server workload balancing prefers less busy servers
- 2-table combinations found for oversized parties
- AutoAssign returns top recommendation

#### 4.2 EnhancedWaitlistGrain tests
- Returning customer gets priority position boost
- FindNextSuitableEntry matches party to table capacity
- Expired entries auto-removed
- Wait estimates recalculate based on turn time data

#### 4.3 BookingAccountingSubscriber tests
- Deposit paid → journal entry debits cash, credits deposit liability
- Deposit applied to order → debits liability, credits revenue
- Deposit refunded → debits liability, credits cash
- Deposit forfeited → debits liability, credits other income

#### 4.4 TurnTimeAnalyticsGrain tests
- Stats calculation (average, median, stddev)
- Stats by party size grouping
- Rolling window trims at 10k records
- Long-running table detection
- Estimated turn time falls back gracefully (exact match → size range → party-size default)

---

### Phase 5: Public Booking and Integrations

Once availability is correct and the optimizer is wired, the public booking API becomes viable.

#### 5.1 Public booking endpoints
```
GET  /api/public/sites/{siteSlug}/availability?date=&partySize=
POST /api/public/sites/{siteSlug}/bookings
GET  /api/public/bookings/{confirmationCode}
POST /api/public/bookings/{confirmationCode}/cancel
```

#### 5.2 Deposit payment links
When deposit required → create PaymentIntent → generate Stripe Checkout URL → include in notification.

#### 5.3 Deposit-to-order application
When `BookingGrain.LinkToOrderAsync()` fires with a paid deposit → publish `BookingDepositAppliedToOrderEvent` → BookingAccountingSubscriber creates the journal entry.

---

## Summary

| Phase | What | Why | Effort |
|-------|------|-----|--------|
| **1. Fix availability** | Wire BookingCalendarGrain into availability; make availability table-aware | Nothing works if availability is wrong | 3-5 days |
| **2. Connect optimizer & analytics** | Auto-register tables, feed turn times, expose APIs | Grains are built but isolated -- zero value until wired | 4-6 days |
| **3. Cross-grain wiring** | Status sync, no-show customer history, sections, composite view, SignalR | Completes the operational flow for front-of-house | 5-7 days |
| **4. Test coverage** | Tests for optimizer, enhanced waitlist, subscriber, analytics | 4 production grains with zero tests | 3-4 days |
| **5. Public booking** | Public API, deposit payment links, deposit-to-order | Requires phases 1-2 to be correct | 5-7 days |

**Phase 1 is the priority.** Availability is the foundation. Everything else -- optimizer recommendations, public booking, waitlist estimates -- depends on availability returning real numbers. Today it returns "everything is available" regardless of how many bookings exist.
