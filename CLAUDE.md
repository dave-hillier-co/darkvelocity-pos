# CLAUDE.md

This file provides guidance for Claude Code when working with the DarkVelocity POS codebase.

## Project Overview

DarkVelocity POS is a full-featured Point of Sale system built with .NET 10 and React. The backend uses Microsoft Orleans virtual actor framework for distributed state management with event sourcing patterns.

## Tech Stack

### Backend
- **.NET 10** with Microsoft Orleans (virtual actors)
- **Entity Framework Core** for persistence
- **PostgreSQL** for production database
- **Kafka** for messaging/streaming
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

## Key Files

- `DarkVelocity.slnx` - Solution file
- `docker/docker-compose.yml` - Local PostgreSQL, Kafka, Zookeeper
- `docs/orleans-actor-architecture.md` - Detailed architecture docs
- `architecture.md` - Frontend/backend design decisions
- `spec.md` - Functional specification

## Infrastructure Ports

| Service | Port |
|---------|------|
| PostgreSQL | 5432 |
| Kafka | 9092 |
| Kafka UI | 8080 |
| Zookeeper | 2181 |

Database credentials (dev): `darkvelocity` / `darkvelocity_dev`

## Multi-Tenancy

- Organization is the tenant boundary
- Sites are physical venues within an organization
- All grain keys include organization ID prefix
- SpiceDB planned for relationship-based authorization

## Domain Concepts

- **Organization**: Top-level tenant (billing entity)
- **Site**: Physical venue with timezone, currency, tax jurisdiction
- **Sales Period**: Daily business period for a site
- **Order**: Captures items, modifiers, discounts, payments
- **Kitchen Ticket**: Order items sent to kitchen display/printer
