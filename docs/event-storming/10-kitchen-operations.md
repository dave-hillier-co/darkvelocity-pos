# Event Storming: Kitchen Operations Domain

## Overview

The Kitchen Operations domain manages kitchen tickets, preparation workflow, station routing, expediting, and kitchen display systems (KDS). This domain ensures orders flow efficiently from POS to kitchen to plate, with proper timing and coordination.

---

## Domain Purpose

- **Ticket Management**: Route orders to appropriate kitchen stations
- **Preparation Tracking**: Monitor item preparation status
- **Timing Control**: Coordinate course firing and table synchronization
- **Expediting**: Manage the pass and item pickup
- **Performance Monitoring**: Track kitchen efficiency metrics
- **Communication**: Facilitate FOH/BOH coordination

---

## Actors

| Actor | Description | Typical Actions |
|-------|-------------|-----------------|
| **Server** | Front of house | Send orders, fire courses |
| **Line Cook** | Station cook | Start items, mark complete |
| **Expediter** | Pass coordinator | Bump tickets, coordinate timing |
| **Kitchen Manager** | Kitchen oversight | Monitor performance, handle issues |
| **KDS System** | Kitchen displays | Show tickets, track times |

---

## Aggregates

### KitchenTicket Aggregate

Represents a kitchen ticket for a station.

```
KitchenTicket
├── Id: Guid
├── OrgId: Guid
├── SiteId: Guid
├── OrderId: Guid
├── StationId: string
├── TicketNumber: string
├── Status: TicketStatus
├── Priority: int
├── Items: List<TicketItem>
├── TableNumber?: string
├── ServerName: string
├── GuestCount: int
├── CourseNumber?: int
├── SpecialInstructions?: string
├── CreatedAt: DateTime
├── StartedAt?: DateTime
├── CompletedAt?: DateTime
├── BumpedAt?: DateTime
├── RecalledAt?: DateTime
├── PrintedAt?: DateTime
└── Flags: List<TicketFlag>
```

**Invariants:**
- All items must belong to same station
- Cannot complete until all items complete
- Cannot bump until completed

### TicketItem Entity

```
TicketItem
├── Id: Guid
├── OrderLineId: Guid
├── MenuItemName: string
├── Quantity: int
├── Modifiers: List<string>
├── SpecialInstructions?: string
├── Seat?: int
├── Status: ItemStatus
├── StartedAt?: DateTime
├── CompletedAt?: DateTime
├── CompletedBy?: Guid
└── Flags: List<ItemFlag>
```

### Kitchen Aggregate (per Site)

Manages overall kitchen state and configuration.

```
Kitchen
├── SiteId: Guid
├── Stations: List<KitchenStation>
├── ActiveTickets: List<TicketReference>
├── CompletedToday: int
├── AverageTicketTime: TimeSpan
├── CurrentLoad: KitchenLoad
├── Settings: KitchenSettings
└── Status: KitchenStatus
```

### KitchenStation Value Object

```
KitchenStation
├── Id: string
├── Name: string
├── Type: StationType
├── Categories: List<string> (menu categories routed here)
├── PrinterIds: List<string>
├── DisplayId?: string
├── MaxConcurrentTickets: int
├── ActiveTicketCount: int
├── AverageItemTime: TimeSpan
└── IsActive: bool
```

---

## Ticket State Machine

```
┌─────────────┐
│   Created   │
└──────┬──────┘
       │
       │ Display on KDS
       ▼
┌─────────────┐
│   Pending   │
└──────┬──────┘
       │
       │ Cook starts working
       ▼
┌─────────────┐
│ In Progress │◄────────────────┐
└──────┬──────┘                 │
       │                        │
       │ All items complete     │ More items added
       ▼                        │
┌─────────────┐                 │
│  Completed  │─────────────────┘
└──────┬──────┘
       │
       │ Expediter bumps
       ▼
┌─────────────┐
│   Bumped    │
└─────────────┘

From Bumped:
       │
       │ Issue found
       ▼
┌─────────────┐
│  Recalled   │───────> In Progress
└─────────────┘

From any state:
       │
       │ Order voided
       ▼
┌─────────────┐
│   Voided    │
└─────────────┘
```

---

## Commands

### Ticket Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `CreateTicket` | Generate kitchen ticket | Order sent | Order System |
| `PrintTicket` | Send to printer | Ticket exists | System |
| `DisplayTicket` | Show on KDS | Ticket exists | System |
| `StartTicket` | Begin preparation | Ticket pending | Line Cook |
| `StartItem` | Begin item prep | Item pending | Line Cook |
| `CompleteItem` | Mark item done | Item in progress | Line Cook |
| `CompleteTicket` | All items done | All items complete | System/Cook |
| `BumpTicket` | Remove from display | Ticket complete | Expediter |
| `RecallTicket` | Return to display | Ticket bumped | Expediter |
| `VoidTicket` | Cancel ticket | Order voided | Manager |
| `PrioritizeTicket` | Increase priority | Ticket exists | Expediter |
| `HoldTicket` | Delay preparation | Ticket not started | Server |
| `ReleaseHold` | Resume preparation | Ticket held | Server |

### Coordination Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `FireCourse` | Start course items | Course exists | Server, Expediter |
| `HoldFire` | Delay course | Course not fired | Server |
| `RequestAllDay` | Get all-day count | Items pending | Line Cook |
| `MarkBehind` | Flag falling behind | Ticket exists | Expediter |
| `RequestAssistance` | Ask for help | Station busy | Line Cook |
| `AssignCookToTicket` | Assign responsibility | Ticket exists | Kitchen Manager |

### Kitchen Management Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `OpenStation` | Activate station | Station configured | Kitchen Manager |
| `CloseStation` | Deactivate station | No active tickets | Kitchen Manager |
| `SetStationCapacity` | Adjust max tickets | Station exists | Kitchen Manager |
| `ConfigureRouting` | Set item routing | Station exists | Kitchen Manager |
| `ResetAllDay` | Clear all-day counts | Service period | Kitchen Manager |

---

## Domain Events

### Ticket Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `KitchenTicketCreated` | Ticket generated | TicketId, OrderId, StationId, Items | CreateTicket |
| `TicketPrinted` | Sent to printer | TicketId, PrinterId, PrintedAt | PrintTicket |
| `TicketDisplayed` | Shown on KDS | TicketId, DisplayId | DisplayTicket |
| `TicketStarted` | Preparation began | TicketId, StartedBy, StartedAt | StartTicket |
| `TicketItemStarted` | Item prep began | TicketId, ItemId, StartedBy | StartItem |
| `TicketItemCompleted` | Item finished | TicketId, ItemId, CompletedBy, Duration | CompleteItem |
| `TicketCompleted` | All items done | TicketId, Duration, CompletedAt | CompleteTicket |
| `TicketBumped` | Removed from display | TicketId, BumpedBy, TotalTime | BumpTicket |
| `TicketRecalled` | Returned to display | TicketId, Reason, RecalledBy | RecallTicket |
| `TicketVoided` | Cancelled | TicketId, Reason, VoidedBy | VoidTicket |
| `TicketPrioritized` | Priority changed | TicketId, OldPriority, NewPriority | PrioritizeTicket |
| `TicketHeld` | Delayed | TicketId, HoldReason | HoldTicket |
| `TicketHoldReleased` | Resumed | TicketId, HeldDuration | ReleaseHold |

### Coordination Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `CourseFired` | Course started | OrderId, CourseNumber, TicketIds | FireCourse |
| `CourseHeld` | Course delayed | OrderId, CourseNumber, Reason | HoldFire |
| `AllDayRequested` | All-day count | StationId, ItemId, Count | RequestAllDay |
| `StationFellBehind` | Falling behind alert | StationId, TicketCount, OldestAge | MarkBehind |
| `AssistanceRequested` | Help needed | StationId, RequestedBy | RequestAssistance |
| `CookAssigned` | Cook assigned | TicketId, CookId | AssignCookToTicket |

### Kitchen Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `StationOpened` | Station activated | StationId, OpenedBy | OpenStation |
| `StationClosed` | Station deactivated | StationId, ClosedBy | CloseStation |
| `StationCapacityChanged` | Capacity adjusted | StationId, OldCap, NewCap | SetStationCapacity |
| `RoutingConfigured` | Routing updated | StationId, Categories | ConfigureRouting |
| `AllDayReset` | Counts cleared | SiteId, ResetBy | ResetAllDay |

---

## Event Details

### KitchenTicketCreated

```csharp
public record KitchenTicketCreated : DomainEvent
{
    public override string EventType => "kitchen.ticket.created";

    public required Guid TicketId { get; init; }
    public required Guid OrderId { get; init; }
    public required Guid SiteId { get; init; }
    public required string TicketNumber { get; init; }
    public required string StationId { get; init; }
    public required string StationName { get; init; }
    public required IReadOnlyList<TicketItemInfo> Items { get; init; }
    public required string? TableNumber { get; init; }
    public required string ServerName { get; init; }
    public required int GuestCount { get; init; }
    public int? CourseNumber { get; init; }
    public int Priority { get; init; }
    public string? SpecialInstructions { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public record TicketItemInfo
{
    public Guid ItemId { get; init; }
    public Guid OrderLineId { get; init; }
    public string Name { get; init; }
    public int Quantity { get; init; }
    public IReadOnlyList<string> Modifiers { get; init; }
    public string? SpecialInstructions { get; init; }
    public int? Seat { get; init; }
}
```

### TicketCompleted

```csharp
public record TicketCompleted : DomainEvent
{
    public override string EventType => "kitchen.ticket.completed";

    public required Guid TicketId { get; init; }
    public required Guid OrderId { get; init; }
    public required string StationId { get; init; }
    public required string? TableNumber { get; init; }
    public required DateTime StartedAt { get; init; }
    public required DateTime CompletedAt { get; init; }
    public required TimeSpan TotalDuration { get; init; }
    public required TimeSpan PrepDuration { get; init; } // Started to completed
    public required int ItemCount { get; init; }
    public required IReadOnlyList<ItemDuration> ItemDurations { get; init; }
}

public record ItemDuration
{
    public Guid ItemId { get; init; }
    public string Name { get; init; }
    public TimeSpan Duration { get; init; }
    public Guid? CompletedBy { get; init; }
}
```

### TicketBumped

```csharp
public record TicketBumped : DomainEvent
{
    public override string EventType => "kitchen.ticket.bumped";

    public required Guid TicketId { get; init; }
    public required Guid OrderId { get; init; }
    public required string StationId { get; init; }
    public required string? TableNumber { get; init; }
    public required Guid BumpedBy { get; init; }
    public required DateTime BumpedAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required TimeSpan TotalTicketTime { get; init; }
    public required bool WasRushed { get; init; }
    public required int ItemCount { get; init; }
}
```

---

## Policies (Event Reactions)

### When KitchenTicketCreated

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Route to Printer | Print kitchen ticket | Devices |
| Display on KDS | Show on station display | Display |
| Update Station Load | Increment active count | Kitchen |
| Start Timer | Begin tracking time | Metrics |

### When TicketItemCompleted

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Check Ticket Complete | Auto-complete if all done | Kitchen |
| Update All-Day | Decrement all-day count | Kitchen |
| Track Item Time | Record for metrics | Reporting |

### When TicketCompleted

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Alert Expo | Notify expediter | Notifications |
| Update Order Status | Mark items ready | Orders |
| Update Display | Show as ready | Display |
| Start Expo Timer | Track time to bump | Metrics |

### When TicketBumped

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Clear Display | Remove from KDS | Display |
| Update Station Load | Decrement count | Kitchen |
| Record Metrics | Log ticket time | Reporting |
| Notify Server | Alert for pickup | Notifications |

### When StationFellBehind

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Alert Manager | Send behind notice | Notifications |
| Highlight on Display | Visual indicator | Display |
| Log Performance | Record for analysis | Reporting |

---

## Read Models / Projections

### StationDisplayView

```csharp
public record StationDisplayView
{
    public string StationId { get; init; }
    public string StationName { get; init; }
    public StationStatus Status { get; init; }
    public IReadOnlyList<DisplayTicket> ActiveTickets { get; init; }
    public IReadOnlyList<DisplayTicket> CompletedTickets { get; init; }
    public int AllDayCount { get; init; }
    public IReadOnlyList<AllDayItem> AllDayItems { get; init; }
    public TimeSpan AverageTicketTime { get; init; }
    public int TicketsBehind { get; init; }
}

public record DisplayTicket
{
    public Guid TicketId { get; init; }
    public string TicketNumber { get; init; }
    public TicketStatus Status { get; init; }
    public string? TableNumber { get; init; }
    public int GuestCount { get; init; }
    public int? CourseNumber { get; init; }
    public IReadOnlyList<DisplayItem> Items { get; init; }
    public DateTime CreatedAt { get; init; }
    public TimeSpan Age { get; init; }
    public int Priority { get; init; }
    public bool IsRushed { get; init; }
    public bool IsBehind { get; init; }
    public string? SpecialInstructions { get; init; }
}

public record DisplayItem
{
    public Guid ItemId { get; init; }
    public string Name { get; init; }
    public int Quantity { get; init; }
    public ItemStatus Status { get; init; }
    public IReadOnlyList<string> Modifiers { get; init; }
    public string? SpecialInstructions { get; init; }
    public int? Seat { get; init; }
}
```

### ExpeditingView

```csharp
public record ExpeditingView
{
    public Guid SiteId { get; init; }
    public IReadOnlyList<ExpoTicket> ReadyForPickup { get; init; }
    public IReadOnlyList<ExpoTicket> InProgress { get; init; }
    public IReadOnlyList<OrderProgress> OrderProgress { get; init; }
    public int TotalPending { get; init; }
    public TimeSpan LongestWait { get; init; }
}

public record ExpoTicket
{
    public Guid TicketId { get; init; }
    public string TicketNumber { get; init; }
    public string StationName { get; init; }
    public string TableNumber { get; init; }
    public string ServerName { get; init; }
    public IReadOnlyList<string> Items { get; init; }
    public DateTime CompletedAt { get; init; }
    public TimeSpan WaitingTime { get; init; }
    public bool IsOverdue { get; init; }
}

public record OrderProgress
{
    public Guid OrderId { get; init; }
    public string TableNumber { get; init; }
    public int TotalItems { get; init; }
    public int CompletedItems { get; init; }
    public int InProgressItems { get; init; }
    public int PendingItems { get; init; }
    public decimal PercentComplete { get; init; }
    public TimeSpan EstimatedRemaining { get; init; }
}
```

### KitchenMetricsView

```csharp
public record KitchenMetricsView
{
    public Guid SiteId { get; init; }
    public DateTime Period { get; init; }

    // Current Status
    public int ActiveTickets { get; init; }
    public int CompletedTickets { get; init; }
    public int VoidedTickets { get; init; }

    // Timing Metrics
    public TimeSpan AverageTicketTime { get; init; }
    public TimeSpan AverageItemTime { get; init; }
    public TimeSpan LongestTicketTime { get; init; }
    public decimal OnTimePercentage { get; init; }

    // By Station
    public IReadOnlyList<StationMetrics> ByStation { get; init; }

    // By Hour
    public IReadOnlyList<HourlyMetrics> ByHour { get; init; }

    // Issues
    public int RecallCount { get; init; }
    public int BehindCount { get; init; }
}

public record StationMetrics
{
    public string StationId { get; init; }
    public string StationName { get; init; }
    public int TicketCount { get; init; }
    public int ItemCount { get; init; }
    public TimeSpan AverageTime { get; init; }
    public int BehindCount { get; init; }
}
```

---

## Bounded Context Relationships

### Upstream Contexts (This domain provides data to)

| Context | Relationship | Data Provided |
|---------|--------------|---------------|
| Orders | Published Language | Item ready status |
| Reporting | Published Language | Kitchen metrics |
| Notifications | Published Language | Alerts and status |

### Downstream Contexts (This domain consumes from)

| Context | Relationship | Data Consumed |
|---------|--------------|---------------|
| Orders | Customer/Supplier | Order items to prepare |
| Menu | Customer/Supplier | Item routing rules |
| Site | Customer/Supplier | Station configuration |

---

## Business Rules

### Routing Rules

1. **Category Routing**: Items route to stations by menu category
2. **Multi-Station Items**: Complex items may route to multiple stations
3. **Modifier Routing**: Some modifiers change routing
4. **Course Grouping**: Same course items on same ticket

### Timing Rules

1. **Target Times**: Configurable target time per item type
2. **Behind Threshold**: Alert when > 2x target time
3. **Rush Priority**: Rush tickets get highest priority
4. **Course Timing**: Hold later courses until earlier complete

### Display Rules

1. **FIFO Display**: Oldest tickets first
2. **Priority Override**: Rushed tickets at top
3. **Color Coding**: Green/Yellow/Red by age
4. **Auto-Bump Option**: Configurable auto-bump timer

---

## Event Type Registry

```csharp
public static class KitchenEventTypes
{
    // Ticket Lifecycle
    public const string KitchenTicketCreated = "kitchen.ticket.created";
    public const string TicketPrinted = "kitchen.ticket.printed";
    public const string TicketDisplayed = "kitchen.ticket.displayed";
    public const string TicketStarted = "kitchen.ticket.started";
    public const string TicketItemStarted = "kitchen.ticket.item_started";
    public const string TicketItemCompleted = "kitchen.ticket.item_completed";
    public const string TicketCompleted = "kitchen.ticket.completed";
    public const string TicketBumped = "kitchen.ticket.bumped";
    public const string TicketRecalled = "kitchen.ticket.recalled";
    public const string TicketVoided = "kitchen.ticket.voided";
    public const string TicketPrioritized = "kitchen.ticket.prioritized";
    public const string TicketHeld = "kitchen.ticket.held";
    public const string TicketHoldReleased = "kitchen.ticket.hold_released";

    // Coordination
    public const string CourseFired = "kitchen.coordination.course_fired";
    public const string CourseHeld = "kitchen.coordination.course_held";
    public const string AllDayRequested = "kitchen.coordination.all_day_requested";
    public const string StationFellBehind = "kitchen.coordination.station_fell_behind";
    public const string AssistanceRequested = "kitchen.coordination.assistance_requested";
    public const string CookAssigned = "kitchen.coordination.cook_assigned";

    // Station Management
    public const string StationOpened = "kitchen.station.opened";
    public const string StationClosed = "kitchen.station.closed";
    public const string StationCapacityChanged = "kitchen.station.capacity_changed";
    public const string RoutingConfigured = "kitchen.station.routing_configured";
    public const string AllDayReset = "kitchen.station.all_day_reset";
}
```
