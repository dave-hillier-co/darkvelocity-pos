# CLAUDE.md

This file provides guidance for Claude Code when working with the DarkVelocity codebase.

## Project Overview

DarkVelocity is a hospitality operations platform built with .NET 10 and React. It unifies the systems that restaurants, bars, and hotels typically buy separately: point of sale, kitchen display, table management, reservations, inventory, procurement, customer loyalty, gift cards, staff scheduling, payroll, delivery platform integration, and back-office reporting.

The backend uses Microsoft Orleans virtual actor framework for distributed state management with event sourcing patterns.

## Tech Stack

### Backend
- **.NET 10** with Microsoft Orleans (virtual actors)
- **Azure Table Storage** for JournaledGrain event sourcing (Azurite for local dev)
- **Entity Framework Core** for persistence
- **PostgreSQL** for relational data
- **Kafka** for Orleans streaming (Azure Event Hubs with Kafka protocol in production)
- **SignalR** for real-time updates
- **xUnit** for testing

### Frontend
- **React 19** with TypeScript
- **Vite** for build tooling
- **Pico.css** for classless styling
- **React Router v7** for routing
- **sql.js** for offline PWA support

## Project Structure

```
darkvelocity-pos/
├── src/
│   └── DarkVelocity.Host/           # Orleans silo with grains, state, events, API
├── tests/
│   └── DarkVelocity.Tests/          # Orleans grain tests
├── apps/
│   ├── pos/                         # React PWA for tablets
│   └── backoffice/                  # React SPA for management
├── docker/
│   └── docker-compose.yml           # Local infrastructure
└── scripts/
    ├── setup.sh                     # Full project setup
    ├── run-services.sh              # Service management
    └── teardown.sh                  # Cleanup script
```

## Common Commands

### Build & Test
```bash
# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/DarkVelocity.Tests
```

### Local Development Setup
```bash
# Full setup (Docker + build + frontend deps)
./scripts/setup.sh

# Start infrastructure only
cd docker && docker compose up -d

# Check prerequisites
./scripts/setup.sh --check-only
```

### Frontend Apps
```bash
# POS app
cd apps/pos && npm install && npm run dev

# Back office
cd apps/backoffice && npm install && npm run dev
```

### Infrastructure
```bash
# Stop containers (preserve data)
./scripts/teardown.sh

# Stop and remove volumes
./scripts/teardown.sh --remove-volumes
```

## Architecture Patterns

### Orleans Grains
- Grains are domain aggregates with identity and lifecycle
- Event sourcing for state persistence
- Single-writer principle per aggregate
- Grain keys use tenant prefixes: `org:{orgId}:{type}:{entityId}`
- **Prefer composition over inheritance**: Extend grain functionality by calling other grains, not by subclassing
  - Grains collaborate through grain-to-grain calls (`GrainFactory.GetGrain<T>()`)
  - Extract reusable logic into separate grains or plain C# services
  - Avoid base grain classes that add domain behavior—use them only for cross-cutting infrastructure concerns
  - Example: An `OrderGrain` needing loyalty points calls `ILoyaltyGrain`, it doesn't inherit from a `LoyaltyAwareGrain`

### Event Naming
- Past tense for domain events: `OrderOpened`, `PaymentApplied`, `StockConsumed`
- Events describe facts that occurred, not commands

### External System Integration
- **Never use "Create" for external data**: You cannot command external systems into existence
- External orders, payouts, webhooks are *observed facts*, not commands
- Use `Received` suffix: `ExternalOrderReceived`, `PayoutReceived`, `WebhookReceived`
- Grain methods use `ReceiveAsync()` not `CreateAsync()` for external data
- Example: `IExternalOrderGrain.ReceiveAsync(ExternalOrderReceived order)`
- This applies to any data originating from third parties (Deliverect, UberEats, payment processors)

### Frontend State
- React Context + useReducer with past-tense actions
- Action types: `ITEM_ADDED`, `STATUS_CHANGED` (not `ADD_ITEM`, `CHANGE_STATUS`)

### API Format
- HAL+JSON with `_links` and `_embedded`
- Resource-oriented URLs: `/api/orgs/{orgId}/sites/{siteId}/orders`

## Code Conventions

### C# Style
- Records for DTOs and value objects
- Async/await throughout
- Use `required` keyword for mandatory properties
- Immutable state where possible

### TypeScript Style
- Functional components with hooks
- No barrel files (`index.ts`)
- Semantic HTML elements (Pico.css styles them automatically)
- Inline styles for layout only

### Testing
- xUnit for backend tests
- Arrange-Act-Assert pattern
- Test grain behavior through interfaces
- **Tests must provide new logical coverage**: Only write tests that exercise grain behavior, not arithmetic or conversions that happen in the calling layer
  - Unit conversions (pints to liters, oz to grams) happen before calling grains—don't test these in grain tests
  - Container math (cases + loose bottles) is caller responsibility—grains receive quantities in base units
  - If a test only exercises math you wrote in the test itself, it's not testing grain behavior
- BDD-style tests (Given-When-Then) are preferred for complex scenarios

### Inventory Philosophy
- **Negative stock is the default**: Service doesn't stop for inventory discrepancies—the system tracks reality
- Sales that exceed recorded stock go negative, flagging a discrepancy for reconciliation
- Physical counts (stock takes) correct the system to match actual inventory

## Key Files

- `DarkVelocity.slnx` - Solution file
- `docker/docker-compose.yml` - Local Azurite, PostgreSQL, Kafka, Zookeeper
- `docs/orleans-actor-architecture.md` - Detailed architecture docs
- `architecture.md` - Frontend/backend design decisions
- `spec.md` - Functional specification

## Infrastructure Ports

| Service | Port |
|---------|------|
| Azurite Blob | 10000 |
| Azurite Queue | 10001 |
| Azurite Table | 10002 |
| PostgreSQL | 5432 |
| Kafka | 9092 |
| Kafka UI | 8080 |
| Zookeeper | 2181 |
| SpiceDB gRPC | 50051 |
| SpiceDB HTTP | 8443 |
| SpiceDB Metrics | 9090 |

Database credentials (dev): `darkvelocity` / `darkvelocity_dev`

Azure Storage (dev): `UseDevelopmentStorage=true` (Azurite default connection string)

Kafka (dev): `localhost:9092` (Docker)

SpiceDB (dev): `localhost:50051` with preshared key `darkvelocity_dev_key`

## Multi-Tenancy

- Organization is the tenant boundary
- Sites are physical venues within an organization
- All grain keys include organization ID prefix
- SpiceDB for relationship-based authorization

## Domain Concepts

- **Organization**: Top-level tenant (billing entity)
- **Site**: Physical venue with timezone, currency, tax jurisdiction
- **Sales Period**: Daily business period for a site
- **Order**: Captures items, modifiers, discounts, payments
- **Kitchen Ticket**: Order items sent to kitchen display/printer

## Grain Domains

Orleans grains are organized by domain. Each grain is a stateful actor with identity.

| Domain | Grains |
|--------|--------|
| **Orders** | Order, LineItems, KitchenTicket, KitchenStation |
| **Payments** | Payment, PaymentIntent, PaymentMethod, CashDrawer, Refund, GiftCard |
| **Payment Processors** | Merchant, Terminal, MockProcessor, StripeProcessor, AdyenProcessor |
| **Customers** | Customer, CustomerSpendProjection, LoyaltyProgram |
| **Inventory** | Inventory, Supplier, PurchaseOrder, PurchaseDocument, Delivery, VendorItemMapping |
| **Menu** | MenuDefinition, MenuCategory, MenuItem, MenuItemVariation, ModifierBlock, ContentTag, SiteMenuOverrides, MenuContentResolver, MenuRegistry |
| **Recipes** | Recipe, RecipeDocument, RecipeCategoryDocument, RecipeRegistry, IngredientPrice |
| **Costing** | CostAlert, CostingSettings, AccountingGroup, MenuEngineering |
| **Tables & Bookings** | Table, FloorPlan, Booking, BookingSettings, BookingCalendar, Waitlist, CustomerVisitHistory |
| **Staff** | Employee, Role, Schedule, TimeEntry, TipPool, PayrollPeriod, EmployeeAvailability, ShiftSwap, TimeOff |
| **Finance** | Account, Ledger, Expense, ExpenseIndex, TaxRate |
| **External Channels** | Channel, ChannelRegistry, DeliveryPlatform, ExternalOrder, MenuSync, PlatformPayout, StatusMapping |
| **Devices** | PosDevice, Device, DeviceAuth, DeviceStatus, Session, Printer, CashDrawerHardware |
| **Reporting** | DailySales, DailyInventorySnapshot, DailyConsumption, DailyWaste, PeriodAggregation, SiteDashboard |
| **Fiscal** | FiscalDevice, FiscalTransaction, FiscalJournal |
| **Organization** | Organization, Site, User, UserGroup, UserLookup |
| **System** | Alert, Notification, EmailInbox, Workflow, WebhookEndpoint, WebhookSubscription |

## API Endpoint Groups

All endpoints follow the pattern `/api/orgs/{orgId}/...` for tenant-scoped resources.

| Group | Path | Purpose |
|-------|------|---------|
| **Auth** | `/api/auth`, `/api/oauth`, `/api/device` | PIN login, OAuth, device authentication |
| **Organizations** | `/api/orgs` | Tenant management |
| **Sites** | `/api/orgs/{orgId}/sites` | Venue configuration |
| **Orders** | `.../sites/{siteId}/orders` | Order lifecycle, line items, discounts |
| **Payments** | `.../sites/{siteId}/payments` | Cash, card, refunds, voids |
| **Tables** | `.../sites/{siteId}/tables` | Table management |
| **Menu** | `/api/orgs/{orgId}/menu` | Categories, items, modifiers |
| **Menu CMS** | `/api/orgs/{orgId}/menu/cms` | Menu content management |
| **Recipes** | `/api/orgs/{orgId}/recipes/cms` | Recipe management |
| **Inventory** | `.../sites/{siteId}/inventory` | Stock, receiving, consumption |
| **Purchases** | `.../sites/{siteId}/purchases` | Purchase orders |
| **Bookings** | `.../sites/{siteId}/bookings` | Reservations |
| **Availability** | `.../sites/{siteId}/availability` | Booking capacity |
| **Waitlist** | `.../sites/{siteId}/waitlist` | Guest waitlist |
| **Floor Plans** | `.../sites/{siteId}/floor-plans` | Floor layout |
| **Customers** | `/api/orgs/{orgId}/customers` | Customer profiles |
| **Employees** | `/api/orgs/{orgId}/employees` | Staff management |
| **Expenses** | `.../sites/{siteId}/expenses` | Expense tracking |
| **Channels** | `/api/orgs/{orgId}/channels` | Delivery platform integration |
| **Webhooks** | `/api/orgs/{orgId}/webhooks` | Webhook management |
| **Search** | `/api/orgs/{orgId}/search` | Cross-domain search |
| **Devices** | `/api/devices`, `/api/stations` | Device registration |
