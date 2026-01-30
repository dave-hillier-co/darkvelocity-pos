# DarkVelocity POS

A full-featured Point of Sale system inspired by Lightspeed Restaurant K-Series, built with .NET 10 and React.

## Architecture

**Backend**: Service-Oriented Architecture with 18 discrete .NET 10 Web APIs deployed on Kubernetes (no API gateway).

### Core Services

| Service | Purpose | Database |
|---------|---------|----------|
| Auth | User authentication, PIN/QR login, permissions | auth_db |
| Location | Multi-location management, settings, operating hours | location_db |
| Menu | Items, categories, menus, accounting groups | menu_db |
| Orders | Order lifecycle, sales periods | orders_db |
| Payments | Cash/card payments, Stripe integration | payments_db |
| Hardware | Printers, cash drawers, POS devices | hardware_db |
| Inventory | Ingredients, stock batches, FIFO tracking | inventory_db |
| Procurement | Suppliers, purchase orders, deliveries | procurement_db |
| Costing | Recipes, cost calculations, margin analysis | costing_db |
| Reporting | Sales reports, COGS, margin projections | reporting_db |
| Booking | Table reservations, availability, deposits | booking_db |
| PaymentGateway | External payment processor integration | paymentgateway_db |

### Planned Services (See [Additional Features Plan](docs/ADDITIONAL_FEATURES_PLAN.md))

| Service | Purpose | Database |
|---------|---------|----------|
| GiftCards | Gift card lifecycle, programs, liability tracking | giftcards_db |
| Fiscalisation | KassenSichV (TSE), European tax compliance, DSFinV-K | fiscalisation_db |
| Accounting | Journal entries, P&L, reconciliation, DATEV export | accounting_db |
| OrdersGateway | Uber Eats, DoorDash, Deliveroo, Just Eat integration | ordersgateway_db |
| Customers | CRM, loyalty programs, points, rewards, referrals | customers_db |
| Labor | Scheduling, time tracking, tip distribution, payroll | labor_db |

**Frontend**: React SPAs (planned)
- POS Application - Offline-first PWA for tablets
- Back Office - Configuration and reporting SPA

## Key Features

- **Multi-tenant SaaS** with schema-per-tenant isolation
- **Multi-location support** with timezone and currency handling
- **Recipe-based costing** with waste percentage calculations
- **FIFO inventory tracking** for accurate COGS
- **Margin analysis** and cost alerts
- **Full procurement workflow** - suppliers, POs, deliveries
- **Gift cards** - programs, designs, balance tracking, liability reporting
- **European fiscalisation** - KassenSichV (Germany), TSE integration, DSFinV-K export
- **Integrated accounting** - journal entries, P&L, reconciliation, DATEV export
- **Delivery platform integrations** - Uber Eats, DoorDash, Deliveroo, Just Eat, Wolt, and more
- **Customer loyalty** - points, tiers, rewards, referrals
- **Labor management** - scheduling, time tracking, tip pooling, payroll export
- **HAL+JSON hypermedia** API responses
- **Kubernetes-native** - Ingress routing, Istio service mesh, no API gateway

## Project Structure

```
lightspeed-clone/
├── src/
│   ├── Shared/
│   │   ├── Shared.Contracts/      # DTOs, Events, HAL helpers
│   │   └── Shared.Infrastructure/ # Base entities, common patterns
│   └── Services/
│       ├── Auth/Auth.Api/
│       ├── Location/Location.Api/
│       ├── Menu/Menu.Api/
│       ├── Orders/Orders.Api/
│       ├── Payments/Payments.Api/
│       ├── Hardware/Hardware.Api/
│       ├── Inventory/Inventory.Api/
│       ├── Procurement/Procurement.Api/
│       ├── Costing/Costing.Api/
│       └── Reporting/Reporting.Api/
├── tests/
│   ├── Tests.Shared/
│   ├── Auth.Tests/
│   ├── Location.Tests/
│   ├── Menu.Tests/
│   ├── Orders.Tests/
│   ├── Payments.Tests/
│   ├── Hardware.Tests/
│   ├── Inventory.Tests/
│   ├── Procurement.Tests/
│   ├── Costing.Tests/
│   └── Reporting.Tests/
├── apps/
│   ├── pos/                       # React PWA (planned)
│   └── backoffice/                # React SPA (planned)
└── docker/
    └── docker-compose.yml         # Local infrastructure
```

## Prerequisites

- Docker & Docker Compose
- .NET 10 SDK
- Node.js 20+ (for frontend apps)

## Getting Started

### Quick Setup (Recommended)

The fastest way to get started is using the setup script:

```bash
# Full setup: checks prerequisites, starts Docker, builds backend, installs frontend deps
./scripts/setup.sh

# Check prerequisites only
./scripts/setup.sh --check-only

# Skip specific steps
./scripts/setup.sh --skip-frontend
./scripts/setup.sh --skip-docker

# Show current environment status
./scripts/setup.sh --status
```

### Manual Setup

If you prefer to set things up manually:

```bash
# 1. Start Docker infrastructure
cd docker && docker compose up -d && cd ..

# 2. Build .NET projects
dotnet build

# 3. Install frontend dependencies
cd apps/pos && npm install && cd ../..
cd apps/backoffice && npm install && cd ../..
cd apps/kds && npm install && cd ../..
```

### Run Tests

```bash
# Run all tests
dotnet test

# Run specific service tests
dotnet test tests/Costing.Tests

# Run tests with coverage
./scripts/setup.sh --run-tests
```

### Running Services

```bash
# Run all backend services
./scripts/run-services.sh start

# Run specific services
./scripts/run-services.sh start Menu Orders

# Check service status
./scripts/run-services.sh status

# Stop all services
./scripts/run-services.sh stop

# View service logs
./scripts/run-services.sh logs Menu
```

Or run a single service manually:

```bash
cd src/Services/Menu/Menu.Api
dotnet run
```

### Running Frontend Apps

```bash
# POS App (PWA)
cd apps/pos && npm run dev

# Back Office
cd apps/backoffice && npm run dev

# Kitchen Display System
cd apps/kds && npm run dev
```

### Teardown

```bash
# Stop Docker containers (preserves data)
./scripts/teardown.sh

# Stop and remove volumes (deletes all data)
./scripts/teardown.sh --remove-volumes

# Remove everything including images
./scripts/teardown.sh --remove-all
```

### Service Ports

| Service | Port | | Service | Port |
|---------|------|-|---------|------|
| Auth | 5000 | | Inventory | 5006 |
| Location | 5001 | | Procurement | 5007 |
| Menu | 5002 | | Costing | 5008 |
| Orders | 5003 | | Reporting | 5009 |
| Payments | 5004 | | Booking | 5010 |
| Hardware | 5005 | | PaymentGateway | 5011 |
| GiftCards | 5012 | | Fiscalisation | 5013 |
| Accounting | 5014 | | OrdersGateway | 5015 |
| Customers | 5016 | | Labor | 5017 |

### Infrastructure Ports

| Service | Port | URL |
|---------|------|-----|
| PostgreSQL | 5432 | - |
| Kafka | 9092 | - |
| Zookeeper | 2181 | - |
| Kafka UI | 8080 | http://localhost:8080 |

Database credentials (development): `darkvelocity` / `darkvelocity_dev`

## Test Coverage

| Service | Tests |
|---------|-------|
| Auth | 13 |
| Menu | 31 |
| Orders | 19 |
| Inventory | 37 |
| Procurement | 42 |
| Payments | 35 |
| Hardware | 40 |
| Reporting | 81 |
| Location | 53 |
| Costing | 102 |
| **Total** | **453** |

## API Examples

### Create a Recipe

```bash
curl -X POST http://localhost:5000/api/recipes \
  -H "Content-Type: application/json" \
  -d '{
    "menuItemId": "uuid",
    "menuItemName": "Classic Burger",
    "code": "RCP-BURGER-001",
    "portionYield": 1
  }'
```

### Add Ingredient to Recipe

```bash
curl -X POST http://localhost:5000/api/recipes/{recipeId}/ingredients \
  -H "Content-Type: application/json" \
  -d '{
    "ingredientId": "uuid",
    "ingredientName": "Ground Beef",
    "quantity": 0.15,
    "unitOfMeasure": "kg",
    "wastePercentage": 5
  }'
```

### Calculate Recipe Cost

```bash
curl http://localhost:5000/api/recipes/{recipeId}/cost?menuPrice=12.00
```

## Technology Stack

- **.NET 10** - Backend APIs
- **Entity Framework Core 10** - ORM
- **PostgreSQL** - Production database
- **SQLite** - Development/testing
- **xUnit** - Testing framework
- **FluentAssertions** - Test assertions
- **React** - Frontend (planned)
- **sql.js** - Offline SQLite for PWA (planned)

## License

MIT
