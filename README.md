# DarkVelocity

A complete restaurant and bar management solution built with .NET 10, Microsoft Orleans, and React.

DarkVelocity provides everything hospitality businesses need to run their operations: point of sale, kitchen display systems, table and floor management, reservations and bookings, inventory and procurement, customer loyalty, gift cards, staff scheduling, and back-office reporting.

## Tech Stack

- **Backend**: .NET 10 with Orleans (virtual actors), PostgreSQL, Kafka, SignalR
- **Frontend**: React 19 + TypeScript, Vite, Pico.css

## Quick Start

```bash
# Setup (Docker + build + frontend deps)
./scripts/setup.sh

# Run tests
dotnet test

# Start frontend apps
cd apps/pos && npm run dev
cd apps/backoffice && npm run dev
```

## Infrastructure

```bash
cd docker && docker compose up -d
```

| Service    | Port |
|------------|------|
| PostgreSQL | 5432 |
| Kafka      | 9092 |
| Kafka UI   | 8080 |

Dev credentials: `darkvelocity` / `darkvelocity_dev`

## Project Structure

```
darkvelocity-pos/
├── src/DarkVelocity.Host/    # Orleans silo (grains, state, events, API)
├── tests/DarkVelocity.Tests/ # Grain tests
├── apps/pos/                 # React PWA for tablets
├── apps/backoffice/          # React SPA for management
├── docker/                   # Local infrastructure
└── scripts/                  # Setup and management scripts
```

## Documentation

See `CLAUDE.md` for architecture details and development guidelines.

## License

MIT
