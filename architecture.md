# Lightspeed K-Series Clone - Architecture

This document defines the technical architecture for implementing a Lightspeed Restaurant K-Series clone. It complements the functional specification at `~/repos/lightspeed-k-series-spec/spec.md`.

---

## Technology Stack

### Frontend

- **Framework**: React 18+ with TypeScript
- **State Management**: React Context + useReducer (event-driven, past-tense actions)
- **Routing**: React Router v6
- **HTTP Client**: Fetch API with custom hooks
- **Styling**: Pico.css (classless) + inline styles for structural layout
- **Build Tool**: Vite
- **Testing**: Vitest + React Testing Library

### Backend (Service-Oriented Architecture)

- **Framework**: .NET 10 (LTS)
- **Architecture**: SOA with discrete services communicating via HTTP/messaging
- **ORM**: Entity Framework Core
- **Database**: PostgreSQL
- **Real-time**: SignalR (kitchen display, order updates)
- **Messaging**: Kafka (inter-service communication)
- **Authentication**: ASP.NET Core Identity + JWT
- **API Format**: HAL+JSON (Hypertext Application Language)
- **API Documentation**: OpenAPI/Swagger
- **Testing**: xUnit + TestContainers

### Infrastructure

- **Containerization**: Docker
- **Orchestration**: Kubernetes
- **CI/CD**: GitHub Actions

---

## System Architecture

Aligned with K-Series architecture from spec:

```
┌─────────────────────────────────────────────────────────────────┐
│                       BACK OFFICE                                │
│               (React SPA - Web Browser)                          │
├─────────────────────────────────────────────────────────────────┤
│  Configuration │  Menu Management │  Reporting │  Hardware      │
│  └─ Settings   │  └─ Items        │  └─ Sales  │  └─ Printers   │
│  └─ Users      │  └─ Menus        │  └─ Cash   │  └─ Terminals  │
│  └─ Locations  │  └─ Accounting   │            │  └─ Drawers    │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ HTTPS REST + SignalR
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                         API LAYER                                │
│                    ASP.NET Core Web API                          │
├─────────────────────────────────────────────────────────────────┤
│  Auth │ Orders │ Menu │ Users │ Hardware │ Payments │ Reports   │
└─────────────────────────────────────────────────────────────────┘
                              │
          ┌─────────────────────┴─────────────────────┐
          ▼                                         ▼
┌─────────────────┐                       ┌─────────────────┐
│   PostgreSQL    │                       │  Blob Storage   │
│   (Primary DB)  │                       │ (Images/Docs)   │
└─────────────────┘                       └─────────────────┘
                              │
                              │ Cloud Sync / SignalR
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      POS APPLICATION                             │
│            (React SPA - iPad Browser / PWA)                      │
├─────────────────────────────────────────────────────────────────┤
│  Home Screen  │  Register   │  Floor Plan │  Settings           │
│  └─ Clock     │  └─ Menu    │  └─ Tables  │  └─ Cash Drawer     │
│  └─ Login     │  └─ Cart    │  └─ Orders  │  └─ Reports         │
│  └─ About     │  └─ Payment │             │                     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ LAN / Bluetooth / USB
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        HARDWARE                                  │
├─────────┬─────────┬─────────┬─────────┬─────────┬───────────────┤
│ Receipt │ Kitchen │  Cash   │ Payment │ Barcode │ Kitchen       │
│ Printer │ Printer │ Drawer  │Terminal │ Scanner │ Display       │
└─────────┴─────────┴─────────┴─────────┴─────────┴───────────────┘
```

---

## Backend Structure (SOA)

Each service is independently deployable with its own database schema. Services communicate via HTTP for synchronous calls and Kafka for async events.

### Service Layout Pattern

Each service follows a consistent structure:

```text
{ServiceName}/
├── {ServiceName}.Api/
│   ├── Controllers/          # HTTP endpoints
│   ├── Hubs/                 # SignalR hubs (if real-time needed)
│   ├── Consumers/            # Message consumers
│   ├── Publishers/           # Message publishers
│   └── Program.cs
├── {ServiceName}.Domain/     # Entities, value objects, domain logic
├── {ServiceName}.Infrastructure/  # EF DbContext, repositories
└── {ServiceName}.Tests/

Shared/
├── Shared.Contracts/         # DTOs, Events, Interfaces shared across services
└── Shared.Infrastructure/    # Common EF configs, base classes
```

### Service Communication Pattern

```text
┌──────────┐     HTTP      ┌──────────┐
│ Service A│──────────────→│ Service B│  (synchronous queries)
└──────────┘               └──────────┘
     │
     │ publish: DomainEvent
     ▼
┌──────────────────────────────────────────────────────────────────┐
│                          Kafka                                 │
└──────────────────────────────────────────────────────────────────┘
     │                     │                      │
     ▼                     ▼                      ▼
┌──────────┐         ┌──────────┐          ┌──────────┐
│ Service C│         │ Real-time│          │ Service D│
│          │         │ (SignalR)│          │          │
│ consumes │         │          │          │ consumes │
│ & reacts │         │          │          │ for      │
│          │         │          │          │ reporting│
└──────────┘         └──────────┘          └──────────┘
```

**Communication guidelines:**

- Use HTTP for synchronous queries where response is needed immediately
- Use Kafka events for fire-and-forget notifications
- SignalR pushes events to connected clients (browsers, displays)
- Events should be past-tense (e.g., `OrderCompleted`, `PaymentReceived`)

---

## Frontend Structure

### Separate Applications

Two independent React applications - not a monorepo. Different concerns, different deployment, different pace of change.

| App | Purpose | Target Device |
| --- | ------- | ------------- |
| Back Office | Configuration, reporting, management | Desktop browser |
| POS | Point of sale operations | Tablet (PWA) |

### App Structure Pattern

```text
src/
├── pages/                # Route-based page components
├── components/           # App-specific components
├── contexts/             # React contexts
├── reducers/             # useReducer state management (if needed)
├── api/                  # API client, typed fetch wrappers
├── App.tsx
└── main.tsx
```

**Notes:**

- No `index.ts` barrel files
- Pages organised by route/feature
- Contexts for cross-cutting concerns (auth, current session)
- Reducers for complex local state
- Duplication between apps is acceptable - they have different needs

---

## Styling Approach

### Pico.css + Inline Styles

Use semantic HTML elements that Pico.css styles automatically. Add inline styles only for structural layout.

```tsx
// Good: Semantic HTML, Pico handles styling
function OrderSummary({ order }: { order: Order }) {
  return (
    <article>
      <header>
        <h3>Table {order.tableNumber}</h3>
        <small>{order.covers} covers</small>
      </header>

      <table>
        <thead>
          <tr>
            <th>Item</th>
            <th>Qty</th>
            <th>Price</th>
          </tr>
        </thead>
        <tbody>
          {order.lines.map(line => (
            <tr key={line.id}>
              <td>{line.itemName}</td>
              <td>{line.quantity}</td>
              <td>{formatCurrency(line.lineTotal)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <footer style={{ display: 'flex', justifyContent: 'space-between' }}>
        <strong>Total</strong>
        <strong>{formatCurrency(order.grandTotal)}</strong>
      </footer>
    </article>
  );
}

// Good: Grid layout via inline style, semantic elements
function MenuGrid({ items }: { items: MenuItem[] }) {
  return (
    <section style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '0.5rem' }}>
      {items.map(item => (
        <button key={item.id} onClick={() => addItem(item)}>
          {item.name}
          <small>{formatCurrency(item.price)}</small>
        </button>
      ))}
    </section>
  );
}
```

### When to Use Classes

Classes are acceptable for:

- Taxonomy/state indicators (e.g., `data-status="fired"`)
- Pico.css utility classes (e.g., `container`, `grid`)
- Component-specific overrides when inline would be repetitive

```tsx
// Acceptable: data attribute for state
<tr data-status={line.courseStatus}>

// Acceptable: Pico container class
<main className="container">
```

---

## Event-Driven Reducer Pattern

Use past-tense action names to describe what happened (events), not imperative commands:

```typescript
// Pattern: past-tense action types describe events
type Action =
  | { type: 'ITEM_ADDED'; payload: { item: Item; quantity: number } }
  | { type: 'ITEM_REMOVED'; payload: { itemId: string } }
  | { type: 'QUANTITY_CHANGED'; payload: { itemId: string; quantity: number } }
  | { type: 'STATUS_CHANGED'; payload: { status: Status } }
  | { type: 'FORM_SUBMITTED' }
  | { type: 'FORM_CLEARED' };

// Reducer handles state transitions
function reducer(state: State, action: Action): State {
  switch (action.type) {
    case 'ITEM_ADDED':
      return { ...state, items: [...state.items, createItem(action.payload)] };

    case 'STATUS_CHANGED':
      return { ...state, status: action.payload.status };

    case 'FORM_CLEARED':
      return initialState;

    default:
      return state;
  }
}
```

**Naming convention:**

- `ITEM_ADDED` not `ADD_ITEM`
- `USER_LOGGED_IN` not `LOGIN_USER`
- `FORM_SUBMITTED` not `SUBMIT_FORM`

Actions describe facts (events that occurred), not commands (requests to do something). This makes the action log a readable history and aligns frontend state changes with backend domain events.

---

## API Format (HAL+JSON)

All responses use `application/hal+json` media type with hypermedia links.

### Single Resource Response

```json
{
  "id": "resource-123",
  "name": "Example Resource",
  "status": "active",
  "_links": {
    "self": { "href": "/api/resources/resource-123" },
    "related": { "href": "/api/resources/resource-123/related" },
    "action": { "href": "/api/resources/resource-123/do-something" }
  },
  "_embedded": {
    "children": [
      {
        "id": "child-1",
        "_links": {
          "self": { "href": "/api/children/child-1" }
        }
      }
    ]
  }
}
```

### Collection Response

```json
{
  "_links": {
    "self": { "href": "/api/resources?page=1" },
    "next": { "href": "/api/resources?page=2" },
    "find": { "href": "/api/resources{?status,filter}", "templated": true }
  },
  "_embedded": {
    "resources": [
      { "id": "resource-123", "_links": { "self": { "href": "/api/resources/resource-123" } } }
    ]
  },
  "page": 1,
  "pageSize": 20,
  "totalItems": 45
}
```

### API Design Principles

- **Resource-oriented**: URLs represent resources, not actions
- **Hypermedia-driven**: Clients discover actions via `_links`
- **Consistent verbs**: GET (read), POST (create/action), PUT (update), DELETE (remove)
- **Nested resources**: `/api/parent/{id}/children` for relationships
- **Action endpoints**: POST to `/api/resources/{id}/verb` for state transitions

---

## Real-time Communication

SignalR hubs push updates to connected clients.

### Pattern

```typescript
// Server broadcasts events
await hubContext.Clients.Group(locationId).SendAsync("EntityUpdated", payload);

// Client subscribes and dispatches to reducer
connection.on('EntityUpdated', (payload: EntityPayload) => {
  dispatch({ type: 'ENTITY_UPDATED', payload });
});
```

**Use cases:**

- Status changes that multiple devices need to see
- New items arriving (e.g., kitchen displays)
- Sync state across multiple browser tabs/devices

---

## Hardware Integration

### Receipt Printing
ESC/POS protocol for thermal printers:
- LAN printers via HTTP/raw TCP
- Browser printing via Web Serial API (Bluetooth/USB)

### Kitchen Display
- SignalR-connected web display
- Replaces/supplements kitchen printers
- Touch interface for order management

### Payment Terminals
- Integration varies by provider
- Typically REST API or SDK
- Support standalone mode for resilience

---

## Multi-Location Design

When data spans multiple locations, define clear sharing scopes:

| Scope        | Description                     | Example                              |
| ------------ | ------------------------------- | ------------------------------------ |
| Global       | Shared across all locations     | User groups, accounting categories   |
| Shared       | Available to multiple locations | Products, suppliers                  |
| Local        | Specific to one location        | Stock levels, hardware config        |
| Configurable | Owner decides sharing           | Can be promoted from local to shared |

**Location context:**

- Include location ID in URLs: `/api/locations/{locationId}/resource`
- Back office UI includes location switcher
- Services filter data by location where applicable

---

## Key Design Decisions

### 1. PWA for POS

React PWA instead of native app:

- Cross-platform (iPad, Android tablets, desktop)
- Easier updates (no app store)
- Offline capable with service workers
- Access to Web APIs (printing, camera)

### 2. Service-Oriented Architecture

Discrete services with clear boundaries:

- Each service owns its data and schema
- HTTP for synchronous queries, Kafka for async events
- Independent scaling and deployment
- Shared contracts project for DTOs and events

### 3. HAL+JSON API Format

Hypermedia-driven REST:

- Self-describing responses with `_links`
- Discoverability without hardcoded URLs
- Embedded resources reduce round trips

### 4. Event-Driven Reducers (Frontend)

React Context + useReducer with past-tense actions:

- Clearer intent (what happened, not what to do)
- Easier debugging and logging
- Mirrors domain events from backend

### 5. Classless CSS (Pico.css)

Semantic HTML with minimal classes:

- Pico.css styles HTML elements automatically
- Inline styles for structural layout (flexbox, grid)
- Classes for taxonomy/state, not styling

### 6. SignalR for Real-time

WebSocket-based push:

- Instant updates to connected clients
- Multi-device sync
- Built-in reconnection handling

### 7. Soft Deletes

For entities that cannot be truly deleted:

- Use IsActive/IsDeleted flags
- Maintain audit trail
- Preserve historical relationships
