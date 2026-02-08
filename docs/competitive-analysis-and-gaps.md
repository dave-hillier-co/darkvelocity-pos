# DarkVelocity - Competitive Analysis & Functional Gap Review

> **Date:** February 2026
> **Scope:** User story review, competitive landscape, functional gaps, improvement recommendations
> **Note:** DarkVelocity is positioned as a multi-vertical operations platform, not limited to hospitality

---

## 1. Executive Summary

DarkVelocity's architecture already spans a remarkably broad surface area -- 17 grain domains covering orders, payments, inventory, menu, recipes, costing, tables/bookings, staff, finance, external channels, devices, reporting, fiscal compliance, and more. This covers capabilities that competitors typically split across 3-5 separate products.

However, competitive analysis across 25+ platforms reveals **three categories of gaps**:

1. **Execution gaps** -- Features where grain logic is complete but external integrations are stubbed (e.g., Stripe/Adyen SDK, email/SMS services, delivery platform HTTP clients)
2. **Parity gaps** -- Features that competitors consider table stakes but are absent from DarkVelocity's user stories entirely
3. **Differentiation opportunities** -- Emerging capabilities (AI, marketplace, multi-vertical) where DarkVelocity could leapfrog competitors

**Note on `product-completeness-analysis.md`:** That document is outdated. Codebase validation (Feb 2026) shows most domains are 10-30 percentage points more complete than documented. The common pattern: grain logic, state machines, and endpoints are done; what's missing is real external service integrations and background job triggers. See Section 1.1 below for revised ratings.

This document focuses on categories 2 and 3.

### 1.1 Revised Domain Completeness (Validated Against Codebase)

| Domain | Old Rating | Actual | Key Finding |
|--------|:---------:|:------:|-------------|
| Orders | 78% | **~90%** | Seat assignment, line discounts, price overrides, order merging, course firing all implemented |
| Payments | 75% | **~90%** | Settlement batch, house accounts, offline queue, chargeback grain all implemented |
| Menu | 82% | **~85%** | Modifier availability windows added; auto-86, upsells, price tiers still missing |
| Inventory | 60% | **~85%** | StockTake with blind counts, 6-state transfer lifecycle, variance reporting, ABC classification |
| Staff | 55% | **~80%** | Multi-jurisdiction overtime, break compliance, payroll export (5 formats), tax calculation |
| Devices | 45% | **~75-80%** | Print queue with retry, offline sync with conflict resolution, device health monitoring, RFC 8628 auth |
| Customers | 65% | **~70%** | RFM segmentation, birthday rewards, points expiration methods exist but lack background job triggers |
| System | 35% | **~65-70%** | NotificationGrain, AlertGrain rule evaluation, webhook delivery, scheduled jobs all real -- but notification services are stubs |
| Payment Processors | 30% | **~55-60%** | Full grain logic with idempotency, circuit breakers, retry -- but use StubStripeClient/StubAdyenClient |
| Costing | 50% | ~50% | Unchanged |
| Fiscal | 50% | ~50% | Unchanged |
| Recipes | 55% | ~55% | Unchanged |
| External Channels | 40% | ~40% | Deliverect production-ready; UberEats/DoorDash/Deliveroo still stubs |
| Finance | 40% | ~38% | ChartOfAccounts and periods done; JournalEntry never posts to AccountGrain; reports return zeros |
| Reporting | 60% | ~60% | Unchanged |
| Tables & Bookings | 60% | **~85-90%** | 11 grains fully implemented incl. reminder callbacks; gaps are cross-grain wiring (availability ignores calendar, optimizer never called, analytics never fed) |
| Organization | 70% | ~70% | Unchanged |

**Average: ~65-70%** (up from ~55-60% in the outdated doc). Remaining work is primarily external service wiring, not domain logic.

---

## 2. Competitors Reviewed

### Hospitality-Focused
| Platform | Strengths | Revenue Model |
|----------|-----------|---------------|
| **Toast** | All-in-one restaurant platform, catering/events, embedded lending | Hardware + payments + SaaS |
| **Square for Restaurants** | Free tier, AI voice ordering, AI demand forecasting | Payments + SaaS tiers |
| **Lightspeed Restaurant** | AI business intelligence (Lightspeed AI), kitchen pacing (Tempo) | SaaS + payments |
| **TouchBistro** | Hybrid offline architecture, modular add-on pricing | SaaS + payments |
| **Revel Systems** | Digital menu boards, self-service kiosks, delivery management | SaaS + payments |
| **NCR Aloha** | Enterprise chain management, zero-upfront pricing | SaaS + payments |
| **Oracle MICROS Simphony** | Hotel PMS integration, multi-currency/language, global fiscal | License + SaaS |
| **Rezku** | 3-day PCI-compliant offline, ingredient-level liquor control | SaaS tiers |

### Retail & Commerce
| Platform | Strengths | Revenue Model |
|----------|-----------|---------------|
| **Shopify POS** | Unified commerce, single customer/inventory brain, extensions | SaaS + payments |
| **Clover** | Hardware variety, multi-vertical customization, app marketplace | Payments + SaaS |
| **Lightspeed Retail** | 8M+ preloaded catalogs, variants/bundles/serials, vendor mgmt | SaaS + payments |

### Enterprise / Multi-Vertical ERP
| Platform | Strengths | Revenue Model |
|----------|-----------|---------------|
| **Oracle NetSuite** | Full ERP with POS, GL/AR/AP, multi-subsidiary, AI forecasting | SaaS (high-end) |
| **SAP Business One** | Manufacturing/distribution/retail, batch/serial tracking, MRP | License + SaaS |
| **Odoo** | Open-source, multi-vertical POS modes, 90+ integrated apps | Freemium + SaaS |

### Service/Booking Platforms
| Platform | Strengths | Revenue Model |
|----------|-----------|---------------|
| **Mindbody** | 3.7M-user marketplace, class + appointment scheduling | SaaS + marketplace |
| **Vagaro** | Broadest vertical coverage (beauty to chiro), memberships | SaaS (low entry) |
| **Fresha** | Marketplace-first, commission on acquisition only, Google AI | Commission + payments |

### Specialized
| Platform | Strengths | Revenue Model |
|----------|-----------|---------------|
| **OpenTable** | 1.7B diners/year network, table optimization, benchmarking | Per-cover + SaaS |
| **7shifts** | Best-in-class restaurant scheduling, facial recognition clock-in | SaaS tiers |
| **MarketMan** | AI recipe creation, real-time cost updates, vendor payments | SaaS |
| **Otter** | Delivery aggregation, virtual brands, auto-accept | SaaS + per-order |
| **Deliverect** | 500+ integrations, AI agent library, fleet management | SaaS |
| **Erply** | Retail + wholesale, WMS, low-code App Maker | SaaS |

---

## 3. User Story Review -- What's Well Covered

The 16 user story files cover these domains with good depth:

| Domain | File | Story Count | Assessment |
|--------|------|-------------|------------|
| Order Management | `01-order-management.md` | High | Comprehensive lifecycle, splits, discounts |
| Kitchen Operations | `02-kitchen-operations.md` | High | Tickets, stations, priority, coursing |
| Payment Processing | `03-payment-processing.md` | High | Cash, card, refund, tips, drawer |
| Inventory | `04-inventory-management.md` | High | FIFO, waste, transfers, negative stock |
| Bookings & Tables | `05-bookings-and-tables.md` | High | Reservations, deposits, floor plans |
| Customer & Loyalty | `06-customer-loyalty.md` | Medium | Profiles, loyalty, visit history |
| Staff & Labor | `07-staff-and-labor.md` | Medium | Scheduling, time tracking, payroll |
| Finance | `08-finance-and-accounting.md` | Medium | Accounts, journal entries, expenses |
| Organization | `09-organization-and-sites.md` | Medium | Multi-tenant, site config |
| Menu Management | `10-menu-management.md` | High | CMS, modifiers, bundles, dayparts |
| External Channels | `11-external-channels.md` | Medium | Delivery platforms, menu sync |
| Procurement | `12-procurement.md` | Medium | POs, vendors, receiving |
| Devices | `13-devices-and-hardware.md` | Medium | Registration, printing, displays |
| Reporting | `14-reporting.md` | Medium | Daily sales, inventory snapshots |
| Workflows | `15-workflow-and-webhooks.md` | Low | Webhook management, automation |
| Fiscal | `16-fiscal-compliance.md` | Medium | Device integration, audit trails |

---

## 4. Functional Gaps -- Missing From User Stories Entirely

These capabilities are absent from DarkVelocity's user stories and event storming but are present in multiple competitors. They are organized by strategic importance.

### 4.1 HIGH PRIORITY -- Competitive Table Stakes

#### 4.1.1 Online Ordering & Ecommerce
**Gap:** No user stories for first-party online ordering (web or mobile). The platform only handles orders that arrive via POS or external delivery channels.

**Competitor baseline:**
- Toast: Commission-free online ordering
- Square: Integrated online ordering with QR menus
- Shopify: Full ecommerce integration with POS
- Odoo: QR code ordering from tables

**Recommendation:** Add a first-party ordering domain covering:
- Web-based ordering storefront (whitelabel)
- QR code table ordering (order & pay from phone)
- Pickup scheduling
- Customer-facing order tracking
- Integration with menu and inventory for real-time availability

**Why it matters:** Every major competitor now includes this. Without it, operators must use third-party solutions (with commissions) for any direct digital ordering.

---

#### 4.1.2 Self-Service Kiosks
**Gap:** No user stories for customer-facing self-service kiosks.

**Competitor baseline:**
- Toast: Self-order kiosks
- Square: Picture-based kiosk categories
- Revel: Kiosks with idle-screen video
- Clover: Kiosk hardware option
- Odoo: Self-service mode

**Recommendation:** Add kiosk stories covering:
- Customer-facing menu browsing and ordering
- Payment at kiosk (card, mobile wallet)
- Upsell/cross-sell prompts
- Language selection
- Accessibility compliance
- Idle screen with promotional content

**Why it matters:** Kiosks reduce labor costs by 30-40% for QSR operations and are becoming standard in casual dining.

---

#### 4.1.3 Data Export & External Accounting Integration
**Gap:** While `ADDITIONAL_FEATURES_PLAN.md` describes DATEV/Xero/QuickBooks export, the user stories in `08-finance-and-accounting.md` don't include any export or integration scenarios.

**Competitor baseline:**
- Every platform integrates with QuickBooks, Xero, or both
- NetSuite/SAP have native GL
- Odoo has built-in accounting
- MarketMan pays vendors directly from platform

**Recommendation:** Add user stories for:
- Export to external accounting (QuickBooks, Xero, DATEV, Sage)
- Scheduled automatic export
- Bank reconciliation
- Vendor payment initiation from platform

**Why it matters:** Operators who can't get data into their accounting system will churn. This is a hard requirement for any business.

---

#### 4.1.4 Auto-86 (Inventory-Driven Menu Availability)
**Gap:** The `product-completeness-analysis.md` identifies this as a gap in the Menu domain, but there are no user stories covering the cross-domain workflow.

**Competitor baseline:**
- Square Auto 86: Real-time cross-channel stock sync
- MarketMan: Real-time recipe costing tied to stock levels
- Lightspeed: Built-in inventory with auto-updates

**Recommendation:** Add cross-domain user stories:
- When inventory for a menu item's recipe drops below threshold, auto-snooze the item
- Propagate 86'd status to all channels (POS, online ordering, delivery platforms)
- Alert staff when items are low
- Auto-restore when stock is replenished

---

#### 4.1.5 Customer-Facing Displays
**Gap:** No stories for secondary/customer-facing screens during checkout.

**Competitor baseline:**
- Clover Station Duo: Customer-facing display
- Revel: Customer-facing display with video
- Vagaro: Dual-screen POS with transparent checkout
- Fresha: Integrated POS in appointment view

**Recommendation:** Add stories for:
- Order review display during checkout
- Tip prompt on customer screen
- Promotional content between transactions
- Regulatory price display compliance (required in some jurisdictions)

---

### 4.2 MEDIUM PRIORITY -- Differentiators That Are Becoming Standard

#### 4.2.1 AI-Powered Business Intelligence
**Gap:** No AI or analytics intelligence layer in any user stories.

**Competitor baseline:**
- Lightspeed AI (Jan 2026): Natural language business questions
- Square: AI demand forecasting for labor + inventory
- MarketMan: AI-powered ordering recommendations
- NetSuite Next: Conversational AI for ERP
- 7shifts: AI labor cost optimization

**Recommendation:** Add an intelligence domain:
- Natural language querying of business data ("What were my top 5 items last week?")
- Demand forecasting (predict busy periods from historical data, weather, events)
- Automated inventory reorder suggestions
- Labor scheduling optimization based on forecasted demand
- Menu engineering recommendations (price adjustments, item retirement)
- Anomaly detection (unusual voids, discounts, stock variances)

**Why it matters:** AI is the primary differentiator in the 2026 POS market. Over 50% of modern POS platforms now include predictive analytics. This is where DarkVelocity's rich event-sourced data model becomes a significant advantage -- competitors with simpler data models can't match the depth of insight possible from event streams.

---

#### 4.2.2 Catering & Events Management
**Gap:** No domain or user stories for catering, banquets, or events.

**Competitor baseline:**
- Toast: Full catering module (BEOs, lead management, contracts, online catering ordering)
- Oracle Simphony: Banquet/event management tied to hotel PMS
- OpenTable: Experiential dining (chef's tables, themed nights)

**Recommendation:** Add a Catering/Events domain:
- Event/banquet booking with customizable packages
- Banquet Event Orders (BEOs) with prep lists
- Catering menu management (separate from dine-in menu)
- Deposit and payment scheduling
- Event coordination with kitchen (prep timelines)
- Post-event invoicing

**Why it matters:** Catering is 10-30% of revenue for full-service restaurants. It's a high-margin channel that most POS platforms ignore.

---

#### 4.2.3 Memberships & Subscriptions
**Gap:** No user stories for recurring membership or subscription models.

**Competitor baseline:**
- Vagaro: Memberships and subscriptions management
- Mindbody: Class passes, memberships, auto-billing
- Fresha: Membership management for salons/spas
- Shopify: Subscription products

**Recommendation:** Add membership/subscription stories:
- Recurring billing for memberships (gym, club, wine club, coffee subscription)
- Membership tiers with benefit unlocks
- Auto-billing with failed payment retry
- Membership-based pricing (member vs non-member)
- Usage tracking (visits, classes attended)
- Pause/cancel/upgrade flows

**Why it matters:** If DarkVelocity serves beyond hospitality (fitness, wellness, clubs), memberships are core functionality. Even restaurants use subscription models (coffee subscriptions, wine clubs, meal plans).

---

#### 4.2.4 Appointment/Service Scheduling
**Gap:** The booking system is reservation-focused (tables). No appointment-based scheduling for services.

**Competitor baseline:**
- Vagaro: Class-based + appointment-based scheduling
- Mindbody: Multi-practitioner scheduling with resource management
- Fresha: Appointment booking integrated with POS
- Square Appointments: Service booking for any business

**Recommendation:** Generalize the booking domain or add a parallel scheduling domain:
- Appointment booking (1:1 with a practitioner)
- Class/group booking (1:many with capacity)
- Resource scheduling (rooms, equipment)
- Service duration management
- Practitioner availability and preferences
- Online booking widget
- Buffer time between appointments
- Waitlist for fully booked slots

**Why it matters:** This is the single biggest feature gap if DarkVelocity targets service businesses (salons, fitness, wellness, professional services). The existing Booking domain could be extended, but the mental model is different from table reservations.

---

#### 4.2.5 Marketing & Communications
**Gap:** No email marketing, SMS campaigns, or promotional automation in any user stories.

**Competitor baseline:**
- Toast: Built-in email marketing
- Square: 2-click email campaigns
- Vagaro: Email and SMS marketing
- Lightspeed: Loyalty-driven automated marketing
- OpenTable: Guest email campaigns

**Recommendation:** Add a marketing/communications domain:
- Email campaign creation and sending
- SMS campaigns
- Triggered automation (welcome series, birthday, lapsed customer win-back)
- Promotional offers creation and distribution
- Push notifications for loyalty members
- Campaign performance tracking (open rates, redemption)

---

#### 4.2.6 Digital Menu Boards
**Gap:** No user stories for digital display management.

**Competitor baseline:**
- Revel: Digital menu board sync (menu changes auto-update boards)
- Lightspeed: Digital menu displays
- Many QSR operations use digital boards as standard

**Recommendation:** Add stories for:
- Menu content pushed to display devices
- Daypart-aware display switching
- 86'd item automatic removal from displays
- Promotional content scheduling
- Multi-zone layouts (menu + promotions + queue status)

---

### 4.3 LOWER PRIORITY -- Forward-Looking Opportunities

#### 4.3.1 Marketplace / Demand Generation
**Gap:** No consumer-facing discovery or marketplace functionality.

**Competitor model:**
- OpenTable: 1.7B seated diners/year marketplace
- Mindbody: 3.7M monthly active users
- Fresha: Marketplace-first model (commission on new client acquisition only)

**Consideration:** Building a consumer marketplace is a large product initiative beyond core POS/operations. However, the architecture should not preclude it. The customer domain should support:
- Public-facing business profiles
- Searchable service/menu catalogs
- Online booking/ordering entry points
- Review/rating collection

---

#### 4.3.2 Embedded Financial Services
**Gap:** No lending, cash advance, or banking features.

**Competitor model:**
- Toast Capital: Loans based on sales data
- Square: Banking, loans, instant deposits
- Shopify Capital: Merchant cash advances
- Lightspeed Capital: Cash advances
- Mindbody Capital: Cash advances

**Consideration:** Embedded finance requires a fintech partner or license. However, the data foundation (sales history, payment processing volume) is already present in DarkVelocity's grain domains. This could be a future revenue stream through partnerships.

---

#### 4.3.3 Hotel PMS Integration
**Gap:** No property management system integration for hotels.

**Competitor baseline:**
- Oracle Simphony + OPERA: Room folio posting, shared guest profiles, multi-outlet F&B
- Lightspeed: Hotel PMS integrations

**Recommendation:** If targeting hotels, add stories for:
- Room charge posting (restaurant check to hotel folio)
- Guest profile sharing (preferences, allergies visible across outlets)
- Multi-outlet F&B management within a property
- Room service ordering integration
- Group/conference F&B coordination

---

#### 4.3.4 Low-Code Extensibility / Plugin Architecture
**Gap:** No extensibility or plugin model in user stories.

**Competitor baseline:**
- Shopify: POS UI Extensions via Polaris components
- Erply: Automat App Maker (low-code custom POS apps)
- Clover: App marketplace
- Odoo: Modular app architecture
- NetSuite: SuiteCloud platform

**Consideration:** A plugin/extension model allows vertical-specific customization without bloating the core platform. This is how a single platform serves multiple verticals without becoming unwieldy. The Orleans grain architecture is well-suited to extensibility (custom grains per vertical).

---

#### 4.3.5 AI Voice Ordering
**Gap:** No voice-based ordering in any user stories.

**Competitor baseline:**
- Square: AI-powered voice ordering (answers phones, sends orders to POS)
- Fast-casual chains deploying drive-through AI

**Consideration:** This is an emerging capability, not yet table stakes. But given the 80% annual restaurant turnover rate, automating phone orders addresses a real labor problem.

---

#### 4.3.6 Kitchen Pacing Intelligence
**Gap:** Kitchen operations stories cover ticket routing and priority but not predictive pacing.

**Competitor baseline:**
- Lightspeed Tempo: ML-driven kitchen pacing optimization
- Smart KDS systems that sequence orders for simultaneous course completion

**Recommendation:** Enhance kitchen stories with:
- Predicted preparation times per item
- Automatic course sequencing for simultaneous table completion
- Rush period prediction and alerts
- Station workload balancing
- Cooking time learning from historical data

---

## 5. Multi-Vertical Readiness Assessment

Since DarkVelocity is not limited to hospitality, here's how the current feature set maps to adjacent verticals:

| Capability | Hospitality | Retail | Services | Fitness/Wellness |
|-----------|:-----------:|:------:|:--------:|:----------------:|
| Point of Sale | Yes | Partial | Partial | Partial |
| Menu/Catalog Management | Yes | Needs variants/SKUs | N/A | Class catalog |
| Inventory | Yes (recipe-based) | Needs retail features | Minimal | Product inventory |
| Reservations/Booking | Yes (tables) | No | Needs appointments | Needs classes |
| Kitchen Display | Yes | N/A | N/A | N/A |
| Staff Scheduling | Yes | Yes | Yes | Yes |
| Customer/Loyalty | Yes | Yes | Yes | Yes |
| Payments | Yes | Yes | Yes | Yes |
| Reporting | Yes | Partial | Partial | Partial |
| Online Ordering | No | No | No | No |
| Memberships | No | No | No | Critical gap |
| Appointments | No | No | Critical gap | Critical gap |
| Ecommerce | No | Critical gap | No | No |

### What's Needed for Each Vertical

**Retail readiness requires:**
- Product variants (size, color, material)
- SKU/barcode management
- Serial number tracking
- Bundle/kit assembly
- Purchase ordering with retail-specific workflows
- Ecommerce channel (Shopify/WooCommerce integration or native)
- Returns/exchanges workflow (distinct from refunds)
- Layaway/special orders

**Service business readiness requires:**
- Appointment scheduling (1:1 and group)
- Practitioner/resource management
- Service duration and pricing
- Memberships and packages
- Client intake forms
- Before/after photos (salon, medical spa)
- Commission-based pay for practitioners

**Fitness/wellness readiness requires:**
- Class scheduling with capacity
- Membership management with billing
- Check-in tracking
- Class pass / punch card
- Waitlist for classes
- Virtual class streaming integration
- Facility/equipment booking

---

## 6. Architectural Strengths for Multi-Vertical Expansion

DarkVelocity's architecture has several properties that make multi-vertical expansion feasible:

1. **Orleans grain model** -- New verticals can be added as new grain domains without modifying existing ones
2. **Event sourcing** -- Rich event streams enable AI/analytics across any vertical
3. **Multi-tenant by design** -- Org/Site hierarchy maps to any business structure
4. **Composition over inheritance** -- Grains collaborate through interfaces, enabling vertical-specific grains to compose with shared ones (payments, customers, staff)
5. **HAL+JSON API** -- Hypermedia-driven APIs can evolve without breaking clients

### Recommended Architecture Changes for Multi-Vertical

1. **Generalize "Menu" to "Catalog"** -- The menu domain is restaurant-specific. A broader catalog concept (items, variants, pricing rules, availability) serves retail, services, and hospitality
2. **Generalize "Booking" to "Scheduling"** -- Tables, appointments, classes, and equipment bookings share core scheduling logic but differ in UX and rules
3. **Add "Business Type" to Organization** -- Allow org-level configuration to determine which domains are active (restaurant gets KDS; salon gets appointments; retail gets barcode scanning)
4. **Create vertical-specific grain packages** -- Keep the core small; vertical behaviors live in optional grain assemblies

---

## 7. Industry Trends DarkVelocity Should Track

| Trend | Competitive Pressure | DarkVelocity Position |
|-------|---------------------|----------------------|
| **AI as the intelligence layer** | High -- 50%+ of platforms now have AI | No AI features. Event-sourced data is a strong foundation for this. |
| **Unified commerce (online + offline)** | High -- table stakes for retail, emerging for hospitality | No online ordering or ecommerce |
| **Offline-first resilience** | High -- POS downtime costs ~$9K/min for enterprise | PWA with sql.js is a good start; needs PCI-compliant offline payments |
| **Delivery as infrastructure** | Medium -- white-label delivery (Uber Direct) becoming a plug-in | External channel grains exist but no implementation |
| **Embedded financial services** | Medium -- becoming expected by operators | Not present; not urgent but worth planning for |
| **Low-code extensibility** | Medium -- critical for multi-vertical without bloat | Not present; Orleans architecture supports it |
| **Marketplace demand generation** | Low-Medium -- powerful lock-in but hard to build | Not present; long-term consideration |
| **Subscription/membership models** | Medium-High for multi-vertical | Not present; critical if expanding beyond hospitality |

---

## 8. Prioritized Recommendations

### Tier 1 -- Do Now (required for competitive parity)
1. **Online ordering / QR ordering** -- Every competitor has this; operators expect it
2. **Auto-86 cross-channel** -- Connect inventory to menu availability across all channels
3. **Accounting export** -- QuickBooks/Xero integration is a hard requirement
4. **Customer-facing display** -- Standard hardware configuration, regulatory requirement in some markets
5. **Self-service kiosks** -- Table stakes for QSR and increasingly casual dining

### Tier 2 -- Do Next (competitive differentiation)
6. **AI business intelligence** -- Leverage event-sourced data for natural language analytics and forecasting
7. **Kitchen pacing intelligence** -- Predictive cooking sequencing, rush prediction
8. **Catering & events** -- High-margin revenue channel, few competitors do it well
9. **Marketing automation** -- Email/SMS campaigns, triggered communications
10. **Digital menu boards** -- Sync menu changes to display hardware

### Tier 3 -- Plan For (multi-vertical expansion)
11. **Appointment/service scheduling** -- Generalize booking for services vertical
12. **Memberships & subscriptions** -- Required for fitness, clubs, subscription commerce
13. **Product variants & retail inventory** -- SKUs, barcodes, serials for retail
14. **Low-code extensibility** -- Plugin architecture for vertical-specific customization
15. **Hotel PMS integration** -- Room charging, guest profiles for hotel F&B

### Tier 4 -- Monitor (emerging opportunities)
16. **AI voice ordering** -- Automated phone ordering
17. **Consumer marketplace** -- Demand generation for merchants
18. **Embedded financial services** -- Lending, cash advances
19. **Virtual brand management** -- Multiple brands from one kitchen/location

---

## 9. Competitive Positioning Summary

### Where DarkVelocity is Strong
- **Breadth of unified platform** -- Covers more domains in a single codebase than any single competitor
- **Event sourcing architecture** -- Richer data model than competitors, enabling better analytics
- **Multi-tenant from the start** -- Not bolted on; designed in
- **Fiscal compliance** -- Fiskaly integration puts it ahead of US-only competitors for European markets
- **Recipe and costing** -- Deeper than most POS platforms (closer to MarketMan depth)

### Where DarkVelocity is Weak
- **No online ordering** -- Every competitor has this; it's a glaring omission
- **No AI features** -- The market has moved to AI as standard; DarkVelocity has none
- **External integrations are stubbed** -- Grain logic is solid (~65-70% avg) but Stripe/Adyen use stub SDK clients, notification services log instead of sending, delivery platform adapters beyond Deliverect are skeletons
- **No consumer-facing anything** -- No online ordering, no kiosks, no customer apps
- **Finance posting loop is broken** -- JournalEntryGrain validates entries but never posts to AccountGrain; financial reports return zeros

### Net Assessment
DarkVelocity has the broadest architectural ambition of any platform reviewed. The grain-based domain model is well-designed and covers more ground than Toast, Square, or Lightspeed. Domain logic is more complete than previously documented (~65-70% average, not 55-60%). The remaining execution work is primarily wiring up real external services (payment SDKs, notification providers, delivery platform APIs) and adding background job triggers -- not building new domain logic. The most impactful new investment would be online ordering + AI analytics, which would close the two largest competitive gaps simultaneously.

---

*This analysis is based on publicly available information about competitor products as of February 2026.*
