# Event Storming Documentation

This folder contains event storming documentation for the DarkVelocity POS system. Each document captures the domain model, aggregates, commands, events, and policies for a specific bounded context.

## Domain Documents

| # | Domain | Description |
|---|--------|-------------|
| [01](./01-organization-site-management.md) | Organization & Site Management | Multi-tenant structure, organizations, sites, venues |
| [02](./02-identity-access-management.md) | Identity & Access Management | Users, roles, permissions, authentication |
| [03](./03-order-management.md) | Order Management | Orders, items, modifiers, discounts, tabs |
| [04](./04-payment-processing.md) | Payment Processing | Payments, refunds, tips, split payments |
| [05](./05-inventory-management.md) | Inventory Management | Stock levels, consumption, adjustments, transfers |
| [06](./06-procurement.md) | Procurement | Purchase orders, vendors, receiving, invoices |
| [07](./07-booking-reservations.md) | Booking & Reservations | Table reservations, waitlists, party management |
| [08](./08-customer-loyalty.md) | Customer & Loyalty | Customer profiles, loyalty programs, points |
| [09](./09-gift-card.md) | Gift Cards | Card issuance, redemption, balance management |
| [10](./10-kitchen-operations.md) | Kitchen Operations | Kitchen tickets, display systems, routing |
| [11](./11-accounting.md) | Accounting | Journal entries, general ledger, financial reporting |
| [12](./12-labor-scheduling.md) | Labor & Scheduling | Employees, timecards, schedules, tip pools |

## Event Architecture

DarkVelocity uses a dual-event pattern:

### Event Sourcing Events

Orleans JournaledGrains use event sourcing for state persistence. Events implement marker interfaces per aggregate:

- `IOrderEvent` - Order aggregate events
- `ICustomerEvent` - Customer aggregate events
- `IInventoryEvent` - Inventory aggregate events
- `IPaymentEvent` - Payment aggregate events
- etc.

These events are stored in Azure Table Storage and replayed to rebuild grain state.

### Domain Events (Kafka)

Domain events inherit from `DomainEvent` base class and are published to Kafka for:

- Cross-grain communication
- External system integration
- Analytics and reporting
- Audit trails

## Event Naming Conventions

All events use **past tense** to describe facts that have occurred:

- `OrderCreated` (not `CreateOrder`)
- `PaymentCaptured` (not `CapturePayment`)
- `StockConsumed` (not `ConsumeStock`)
- `EmployeeClockedIn` (not `ClockInEmployee`)

### External System Events

For data received from external systems (Deliverect, UberEats, payment processors), use the `Received` suffix:

- `ExternalOrderReceived` (not `ExternalOrderCreated`)
- `PayoutReceived` (not `PayoutCreated`)
- `WebhookReceived` (not `WebhookProcessed`)

See [CLAUDE.md](/CLAUDE.md) for complete coding conventions.

## Document Structure

Each domain document follows a consistent structure:

1. **Overview** - Domain context and business purpose
2. **Actors** - Users and systems that interact with the domain
3. **Aggregates** - Domain entities with identity and lifecycle
4. **State Machines** - Valid state transitions for aggregates
5. **Commands** - Actions that can be performed
6. **Domain Events** - Facts that result from commands
7. **Event Details** - C# record definitions for events
8. **Policies** - Business rules triggered by events
9. **Read Models** - Query projections for the domain
10. **Bounded Context Relationships** - Integration with other domains
11. **Business Rules** - Invariants and constraints
12. **Event Type Registry** - Summary table of all events

## Implementation

Events are implemented in `src/DarkVelocity.Host/Events/` with separate files per domain:

- `OrderEvents.cs`
- `PaymentEvents.cs`
- `InventoryEvents.cs`
- `CustomerEvents.cs`
- etc.

Each file contains both the event sourcing records and the domain event classes for that bounded context.
