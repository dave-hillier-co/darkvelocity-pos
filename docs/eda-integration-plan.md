# Event-Driven Architecture Integration Plan

## Overview

This plan outlines the staged approach to integrating all DarkVelocity POS services through Event-Driven Architecture (EDA). Each stage is designed to be **independently workable** with clear dependencies noted.

## Current State

| Service | Events Defined | Publishing | Consuming | Status |
|---------|---------------|------------|-----------|--------|
| Auth | 10 | Yes | Yes | **Complete** |
| Labor | 12 | Yes | Yes | **Complete** |
| Orders | 5 | No | No | Pending |
| Inventory | 5 | No | No | Pending |
| Payments | 0 | No | No | Pending |
| Accounting | 0 | No | No | Pending |
| GiftCards | 10 | No | No | Pending |
| Customers | 8 | No | No | Pending |
| Procurement | 6 | No | No | Pending |
| Fiscalisation | 6 | No | No | Pending |
| Costing | 0 | No | No | Pending |
| Menu | 0 | No | No | Pending |
| Booking | 0 | No | No | Pending |
| Reporting | 0 | No | No | Pending |

---

## Stage 1: Orders Service Event Publishing

**Priority:** Critical
**Dependencies:** None (foundational)
**Estimated Scope:** Orders service only

### Objective
Enable the Orders service to publish events for order lifecycle, which other services will consume.

### Tasks

- [ ] **1.1** Add event bus registration to Orders Program.cs
  ```csharp
  builder.Services.AddInMemoryEventBus();
  builder.Services.AddEventHandlersFromAssembly(typeof(Program).Assembly);
  ```

- [ ] **1.2** Update OrdersController to inject IEventBus

- [ ] **1.3** Publish `OrderCreated` event when order is created
  - Include: OrderId, LocationId, UserId, OrderNumber, OrderType

- [ ] **1.4** Publish `OrderLineAdded` event when line items added
  - Include: OrderId, LineId, MenuItemId, ItemName, Quantity, UnitPrice, LineTotal

- [ ] **1.5** Publish `OrderCompleted` event when order is completed
  - Include: OrderId, LocationId, OrderNumber, GrandTotal, full Lines array

- [ ] **1.6** Publish `OrderVoided` event when order is voided
  - Include: OrderId, UserId, Reason

### Files to Modify
- `src/Services/Orders/Orders.Api/Program.cs`
- `src/Services/Orders/Orders.Api/Controllers/OrdersController.cs`

### Events (already defined in Shared.Contracts)
- `OrderCreated`
- `OrderLineAdded`
- `OrderLineRemoved`
- `OrderCompleted`
- `OrderVoided`

### Verification
- Create an order and verify event is logged
- Complete an order and verify OrderCompleted contains all line items

---

## Stage 2: Inventory Stock Consumption

**Priority:** Critical
**Dependencies:** Stage 1 (Orders publishing)
**Estimated Scope:** Inventory service

### Objective
Automatically consume inventory when orders are completed, maintaining accurate stock levels.

### Tasks

- [ ] **2.1** Add event bus registration to Inventory Program.cs

- [ ] **2.2** Create `OrderCompletedHandler` in Inventory service
  - For each OrderLine, look up MenuItem recipe
  - Calculate ingredient quantities based on recipe
  - Create StockConsumption records
  - Update StockBatch quantities (FIFO consumption)

- [ ] **2.3** Publish `StockConsumedForSale` event after consumption
  - Include: OrderId, LocationId, list of Consumptions, TotalCOGS

- [ ] **2.4** Publish `StockBatchExhausted` when batch runs out

- [ ] **2.5** Check reorder levels and publish alerts if needed

### Files to Modify
- `src/Services/Inventory/Inventory.Api/Program.cs`
- Create: `src/Services/Inventory/Inventory.Api/EventHandlers/OrderEventHandlers.cs`

### New Event Handlers
```csharp
public class OrderCompletedHandler : IEventHandler<OrderCompleted>
{
    // Consume stock for each order line
    // Publish StockConsumedForSale
}
```

### Cross-Service Data Access
- May need to call Menu service API to get MenuItem → RecipeId mapping
- May need to call Costing service API to get Recipe → Ingredients

### Verification
- Complete an order with menu items that have recipes
- Verify StockConsumption records created
- Verify StockBatch quantities reduced

---

## Stage 3: Accounting Journal Entries

**Priority:** Critical
**Dependencies:** Stage 1 (Orders), Stage 2 (Inventory for COGS)
**Estimated Scope:** Accounting service

### Objective
Automatically create journal entries for sales revenue and cost of goods sold.

### Tasks

- [ ] **3.1** Add event bus registration to Accounting Program.cs

- [ ] **3.2** Define `PaymentEvents` contracts (currently missing)
  - `PaymentCompleted`
  - `PaymentRefunded`
  - `PaymentVoided`

- [ ] **3.3** Create `OrderCompletedHandler` in Accounting
  - Create revenue journal entry:
    - Debit: Accounts Receivable / Cash
    - Credit: Sales Revenue

- [ ] **3.4** Create `StockConsumedForSaleHandler` in Accounting
  - Create COGS journal entry:
    - Debit: Cost of Goods Sold
    - Credit: Inventory Asset

- [ ] **3.5** Create `PaymentCompletedHandler` in Accounting
  - Settle receivables:
    - Debit: Cash
    - Credit: Accounts Receivable

### Files to Modify
- `src/Services/Accounting/Accounting.Api/Program.cs`
- Create: `src/Services/Accounting/Accounting.Api/EventHandlers/SalesEventHandlers.cs`
- Create: `src/Shared/Shared.Contracts/Events/PaymentEvents.cs`

### Journal Entry Mappings
| Event | Debit Account | Credit Account |
|-------|--------------|----------------|
| OrderCompleted | 1200-Receivables | 4000-Revenue |
| StockConsumedForSale | 5100-COGS | 1300-Inventory |
| PaymentCompleted | 1000-Cash | 1200-Receivables |
| PaymentRefunded | 4000-Revenue | 1000-Cash |

### Verification
- Complete an order and verify journal entry created
- Verify trial balance remains balanced

---

## Stage 4: Payments Service Integration

**Priority:** High
**Dependencies:** Stage 3 (for Accounting handlers)
**Estimated Scope:** Payments service

### Objective
Enable Payments service to publish events and optionally consume order events.

### Tasks

- [ ] **4.1** Define `PaymentEvents` contracts
  ```csharp
  public sealed record PaymentCreated(...) : IntegrationEvent;
  public sealed record PaymentCompleted(...) : IntegrationEvent;
  public sealed record PaymentRefunded(...) : IntegrationEvent;
  public sealed record PaymentVoided(...) : IntegrationEvent;
  ```

- [ ] **4.2** Add event bus registration to Payments Program.cs

- [ ] **4.3** Update PaymentsController to publish events
  - `PaymentCreated` when payment initiated
  - `PaymentCompleted` when payment succeeds
  - `PaymentRefunded` on refund
  - `PaymentVoided` on void

- [ ] **4.4** (Optional) Handle `OrderCompleted` to auto-initiate payment processing

### Files to Modify
- Create: `src/Shared/Shared.Contracts/Events/PaymentEvents.cs`
- `src/Services/Payments/Payments.Api/Program.cs`
- `src/Services/Payments/Payments.Api/Controllers/PaymentsController.cs`

### Verification
- Process a payment and verify PaymentCompleted published
- Verify Accounting receives event and creates journal entry

---

## Stage 5: Fiscalisation Compliance

**Priority:** High (regulatory requirement)
**Dependencies:** Stage 1 (Orders), Stage 4 (Payments)
**Estimated Scope:** Fiscalisation service

### Objective
Automatically register fiscal transactions when orders/payments complete.

### Tasks

- [ ] **5.1** Add event bus registration to Fiscalisation Program.cs

- [ ] **5.2** Create `OrderCompletedHandler` in Fiscalisation
  - Create FiscalTransaction record
  - Sign transaction with fiscal device
  - Publish `TransactionSigned` event

- [ ] **5.3** Create `PaymentCompletedHandler` in Fiscalisation
  - Update fiscal record with payment info

- [ ] **5.4** Handle device failures gracefully
  - Publish `TransactionSigningFailed` event
  - Queue for retry

### Files to Modify
- `src/Services/Fiscalisation/Fiscalisation.Api/Program.cs`
- Create: `src/Services/Fiscalisation/Fiscalisation.Api/EventHandlers/TransactionEventHandlers.cs`

### Events (already defined)
- `TransactionSigned`
- `TransactionSigningFailed`
- `FiscalDeviceHealthChanged`

### Verification
- Complete order in country requiring fiscalisation
- Verify FiscalTransaction created with signature

---

## Stage 6: GiftCards Integration

**Priority:** Medium
**Dependencies:** Stage 1 (Orders), Stage 4 (Payments)
**Estimated Scope:** GiftCards service, Accounting updates

### Objective
Enable gift card lifecycle events and integrate with accounting for liability tracking.

### Tasks

- [ ] **6.1** Add event bus registration to GiftCards Program.cs

- [ ] **6.2** Update GiftCardsController to publish events
  - Events already defined but not published
  - `GiftCardIssued`, `GiftCardActivated`, `GiftCardRedeemed`, etc.

- [ ] **6.3** Create handlers in Accounting for gift card events
  - `GiftCardIssued` → Debit: Cash, Credit: GiftCardLiability
  - `GiftCardRedeemed` → Debit: GiftCardLiability, Credit: Revenue

- [ ] **6.4** (Optional) Handle `OrderCompleted` to check for gift card payments

### Files to Modify
- `src/Services/GiftCards/GiftCards.Api/Program.cs`
- `src/Services/GiftCards/GiftCards.Api/Controllers/GiftCardsController.cs`
- Add: `src/Services/Accounting/Accounting.Api/EventHandlers/GiftCardEventHandlers.cs`

### Journal Entry Mappings
| Event | Debit Account | Credit Account |
|-------|--------------|----------------|
| GiftCardIssued | 1000-Cash | 2500-GiftCardLiability |
| GiftCardRedeemed | 2500-GiftCardLiability | 4000-Revenue |
| GiftCardExpired | 2500-GiftCardLiability | 4900-OtherIncome |

### Verification
- Issue gift card, verify liability journal entry
- Redeem on order, verify liability reversed

---

## Stage 7: Customer Loyalty Integration

**Priority:** Medium
**Dependencies:** Stage 1 (Orders)
**Estimated Scope:** Customers service

### Objective
Award loyalty points when orders complete, enable points redemption.

### Tasks

- [ ] **7.1** Add event bus registration to Customers Program.cs

- [ ] **7.2** Create `OrderCompletedHandler` in Customers
  - Look up customer by order association (if any)
  - Calculate points based on order total
  - Create PointsTransaction record
  - Publish `PointsEarned` event

- [ ] **7.3** Publish customer lifecycle events (already defined)
  - `CustomerCreated`, `CustomerUpdated`
  - `CustomerEnrolledInLoyalty`
  - `PointsEarned`, `PointsRedeemed`
  - `TierChanged`, `RewardIssued`

- [ ] **7.4** (Future) Link customers to orders
  - Orders may need CustomerId field
  - Or separate CustomerOrder linking entity

### Files to Modify
- `src/Services/Customers/Customers.Api/Program.cs`
- Create: `src/Services/Customers/Customers.Api/EventHandlers/OrderEventHandlers.cs`

### Events (already defined)
- All CustomerEvents.cs events ready to publish

### Verification
- Complete order with identified customer
- Verify points awarded and PointsEarned event published

---

## Stage 8: Procurement → Inventory Supply Chain

**Priority:** Medium
**Dependencies:** None (independent track)
**Estimated Scope:** Procurement, Inventory services

### Objective
Automatically create inventory stock batches when deliveries are received.

### Tasks

- [ ] **8.1** Add event bus registration to Procurement Program.cs

- [ ] **8.2** Publish procurement events (already defined)
  - `PurchaseOrderCreated`, `PurchaseOrderSubmitted`
  - `DeliveryReceived`, `DeliveryLineReceived`
  - `DeliveryAccepted`, `DeliveryRejected`

- [ ] **8.3** Create `DeliveryAcceptedHandler` in Inventory
  - For each delivery line, create StockBatch
  - Set quantity, unit cost, expiry date
  - Publish `StockBatchCreated`

- [ ] **8.4** Create handlers in Accounting for procurement
  - `PurchaseOrderSubmitted` → Create payable
  - `DeliveryAccepted` → Debit: Inventory, Credit: Payables

### Files to Modify
- `src/Services/Procurement/Procurement.Api/Program.cs`
- `src/Services/Procurement/Procurement.Api/Controllers/*.cs`
- Create: `src/Services/Inventory/Inventory.Api/EventHandlers/ProcurementEventHandlers.cs`
- Add: `src/Services/Accounting/Accounting.Api/EventHandlers/ProcurementEventHandlers.cs`

### Events (already defined)
- All ProcurementEvents.cs events ready to publish

### Verification
- Create and submit PO
- Receive delivery
- Verify StockBatch created in Inventory
- Verify journal entries in Accounting

---

## Stage 9: Costing and Menu Price Updates

**Priority:** Low
**Dependencies:** Stage 8 (Procurement/Inventory)
**Estimated Scope:** Costing, Menu services

### Objective
Automatically recalculate recipe costs when ingredient prices change.

### Tasks

- [ ] **9.1** Add event bus registration to Costing Program.cs

- [ ] **9.2** Create `StockBatchCreatedHandler` in Costing
  - Check if ingredient price changed
  - Recalculate affected recipe costs
  - Publish `RecipeCostRecalculated`

- [ ] **9.3** Add event bus registration to Menu Program.cs

- [ ] **9.4** Create `RecipeCostRecalculatedHandler` in Menu
  - Update MenuItem unit cost
  - (Optional) Publish `MenuItemCostUpdated` for reporting

### Files to Modify
- `src/Services/Costing/Costing.Api/Program.cs`
- Create: `src/Services/Costing/Costing.Api/EventHandlers/InventoryEventHandlers.cs`
- `src/Services/Menu/Menu.Api/Program.cs`
- Create: `src/Services/Menu/Menu.Api/EventHandlers/CostingEventHandlers.cs`

### New Events to Define
```csharp
public sealed record RecipeCostRecalculated(
    Guid RecipeId,
    Guid MenuItemId,
    decimal NewCost,
    decimal PreviousCost
) : IntegrationEvent;

public sealed record MenuItemCostUpdated(
    Guid MenuItemId,
    Guid LocationId,
    decimal NewUnitCost,
    decimal PreviousUnitCost
) : IntegrationEvent;
```

### Verification
- Receive delivery with different unit cost
- Verify recipe costs recalculated
- Verify menu item costs updated

---

## Stage 10: Booking → Orders Integration

**Priority:** Low
**Dependencies:** Stage 1 (Orders)
**Estimated Scope:** Booking service

### Objective
Link reservations to orders when guests are seated.

### Tasks

- [ ] **10.1** Add event bus registration to Booking Program.cs

- [ ] **10.2** Create `OrderCreatedHandler` in Booking
  - If order has table assignment, find matching booking
  - Update Booking.OrderId

- [ ] **10.3** Publish booking lifecycle events
  - `BookingCreated`, `BookingConfirmed`
  - `BookingCancelled`, `BookingNoShow`
  - `GuestSeated`, `GuestDeparted`

### Files to Modify
- `src/Services/Booking/Booking.Api/Program.cs`
- Create: `src/Services/Booking/Booking.Api/EventHandlers/OrderEventHandlers.cs`

### New Events to Define
```csharp
public sealed record BookingCreated(...) : IntegrationEvent;
public sealed record GuestSeated(
    Guid BookingId,
    Guid LocationId,
    Guid? OrderId,
    DateTime SeatedAt
) : IntegrationEvent;
```

### Verification
- Create booking for table
- Create order for same table
- Verify booking linked to order

---

## Stage 11: Reporting Event Aggregation

**Priority:** Low
**Dependencies:** All previous stages
**Estimated Scope:** Reporting service (consume-only)

### Objective
Build real-time reporting by consuming events from all services.

### Tasks

- [ ] **11.1** Add event bus registration to Reporting Program.cs

- [ ] **11.2** Create handlers for sales aggregation
  - `OrderCompletedHandler` → Update daily sales summary
  - `PaymentCompletedHandler` → Update payment method breakdown

- [ ] **11.3** Create handlers for inventory tracking
  - `StockConsumedForSaleHandler` → Track COGS by category
  - `StockBatchCreatedHandler` → Track receiving by supplier

- [ ] **11.4** Create handlers for customer metrics
  - `PointsEarnedHandler` → Track loyalty engagement
  - `CustomerCreatedHandler` → Track acquisition

### Files to Modify
- `src/Services/Reporting/Reporting.Api/Program.cs`
- Create: `src/Services/Reporting/Reporting.Api/EventHandlers/*.cs`

### Handlers (consume-only, no publishing)
```csharp
public class SalesAggregationHandler :
    IEventHandler<OrderCompleted>,
    IEventHandler<PaymentCompleted>,
    IEventHandler<OrderVoided>
{
    // Aggregate into DailySalesSummary, CategorySales, etc.
}
```

### Verification
- Complete orders throughout day
- Verify real-time dashboards update

---

## Stage 12: Production Kafka Migration

**Priority:** Required for Production
**Dependencies:** All stages functional with InMemoryEventBus
**Estimated Scope:** Infrastructure, all services

### Objective
Replace InMemoryEventBus with Kafka for reliable cross-service messaging.

### Tasks

- [ ] **12.1** Set up Kafka cluster (or managed service)

- [ ] **12.2** Implement `KafkaEventBus` in Shared.Infrastructure
  - Producer configuration
  - Consumer group management
  - Topic naming convention

- [ ] **12.3** Implement transactional outbox pattern
  - OutboxEvent entity already exists
  - OutboxProcessor background service exists
  - Wire to Kafka producer

- [ ] **12.4** Add idempotency handling
  - Track processed EventIds
  - Skip duplicate events

- [ ] **12.5** Configure dead-letter queues
  - Handle poison messages
  - Alert on DLQ growth

- [ ] **12.6** Update all services to use Kafka configuration
  - Environment-based switching (InMemory for dev, Kafka for prod)

### Files to Modify
- Create: `src/Shared/Shared.Infrastructure/Events/KafkaEventBus.cs`
- Update: `src/Shared/Shared.Infrastructure/Events/EventBusExtensions.cs`
- All service Program.cs files (configuration)

### Infrastructure
- Kafka brokers (3+ for production)
- Schema registry (optional, for Avro)
- Monitoring (Kafka UI, metrics)

### Verification
- Deploy to staging with Kafka
- Verify cross-service events delivered
- Test failure scenarios (service down, network partition)

---

## Dependency Graph

```
Stage 1: Orders Publishing
    ↓
    ├── Stage 2: Inventory Consumption
    │       ↓
    │       └── Stage 9: Costing Updates
    │               ↓
    │               └── Menu Updates
    │
    ├── Stage 3: Accounting (needs Stage 2 for COGS)
    │
    ├── Stage 4: Payments Publishing
    │       ↓
    │       └── Stage 5: Fiscalisation
    │
    ├── Stage 6: GiftCards
    │
    ├── Stage 7: Customer Loyalty
    │
    └── Stage 10: Booking

Stage 8: Procurement → Inventory (Independent Track)
    ↓
    └── Stage 9: Costing

Stage 11: Reporting (After all event producers ready)

Stage 12: Kafka (After all stages verified with InMemory)
```

---

## Quick Reference: Event Contract Locations

| Domain | File | Events |
|--------|------|--------|
| Auth/User | `AuthEvents.cs`, `UserEvents.cs` | 10 events |
| Employee | `EmployeeEvents.cs` | 12 events |
| Order | `OrderEvents.cs` | 5 events |
| Inventory | `InventoryEvents.cs` | 5 events |
| Procurement | `ProcurementEvents.cs` | 6 events |
| GiftCard | `GiftCardEvents.cs` | 10 events |
| Customer | `CustomerEvents.cs` | 8 events |
| Fiscalisation | `FiscalisationEvents.cs` | 6 events |
| Payment | **To Create** | ~4 events |
| Booking | **To Create** | ~5 events |
| Costing | **To Create** | ~2 events |
| Menu | **To Create** | ~2 events |

---

## Implementation Pattern Reference

Use Auth↔Labor integration as the reference implementation:

**Publishing Events (see UsersController.cs:130-140):**
```csharp
await _eventBus.PublishAsync(new UserCreated(
    UserId: user.Id,
    TenantId: location.TenantId,
    // ... other properties
));
```

**Handling Events (see EmployeeEventHandlers.cs):**
```csharp
public class EmployeeTerminatedHandler : IEventHandler<EmployeeTerminated>
{
    public async Task HandleAsync(EmployeeTerminated @event, CancellationToken ct)
    {
        // Handle the event
    }
}
```

**Registering in Program.cs:**
```csharp
builder.Services.AddInMemoryEventBus();
builder.Services.AddEventHandlersFromAssembly(typeof(Program).Assembly);
```
