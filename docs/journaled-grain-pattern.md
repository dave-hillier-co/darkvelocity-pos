# Journaled Grain Pattern

This document describes the migration from manual version tracking to Orleans `JournaledGrain<TState, TEvent>` for grains requiring full audit trails and event sourcing capabilities.

## Background

The codebase previously used a manual version pattern across all grains:

```csharp
// Old pattern - manual version tracking
public class OrderGrain : Grain, IOrderGrain
{
    private readonly IPersistentState<OrderState> _state;

    public async Task AddLineAsync(AddLineCommand command)
    {
        // ... business logic ...
        _state.State.Version++;
        await _state.WriteStateAsync();

        // Events published to streams (fire-and-forget, not for replay)
        await GetOrderStream().OnNextAsync(new OrderLineAddedEvent(...));
    }
}
```

This pattern has limitations:
- Events are notifications, not the source of truth
- Cannot rebuild state from events
- No built-in concurrency protection
- Manual `Version++` boilerplate in 500+ locations

## Journaled Grain Benefits

Orleans `JournaledGrain<TState, TEvent>` provides true event sourcing:

| Feature | Manual Version | JournaledGrain |
|---------|---------------|----------------|
| Version tracking | Manual `Version++` | Built-in `this.Version` |
| State persistence | Snapshot (current state) | Event log + snapshots |
| Rebuild state | Not possible | Replay from events |
| Audit trail | External streams (lossy) | Events are source of truth |
| Concurrency | Manual checks | Optimistic with ETags |
| Time travel | Not possible | Query state at any version |

## Grain Categorization

### Journaled Grains (Full Event Sourcing)

These grains handle financial transactions or critical business events requiring complete audit trails:

| Grain | Domain | Rationale |
|-------|--------|-----------|
| **OrderGrain** | Sales | Revenue accuracy, line item history, discount tracking |
| **PaymentGrain** | Payments | Reconciliation, chargebacks, refund tracking |
| **InventoryGrain** | Inventory | Food cost, shrinkage, stock movement history |
| **AccountGrain** | Accounting | Financial compliance, double-entry ledger |
| **GiftCardGrain** | Gift Cards | Liability tracking, balance history |
| **BookingGrain** | Reservations | Deposit disputes, cancellation history |
| **CustomerGrain** | Customers | Loyalty points, reward history, disputes |
| **ExpenseGrain** | Expenses | Expense approvals, audit trail |
| **PurchaseDocumentGrain** | Procurement | PO/invoice history, vendor reconciliation |
| **EmployeeGrain** | HR | Staff records, compliance |
| **LaborGrains** | Payroll | Time tracking, payroll audits |

### Versioned Grains (Snapshot Persistence)

These grains store configuration or reference data where full history is less critical:

| Grain | Domain | Rationale |
|-------|--------|-----------|
| MenuGrains | Menu | Configuration data, infrequent changes |
| TableGrain | Floor | Layout changes, operational state |
| HardwareGrains | Devices | Device configuration |
| SiteGrain | Sites | Location settings |
| UserGrain | Users | Account configuration |
| ReportingGrains | Analytics | Derived/aggregated data |
| IndexGrain | Search | Query projections |

## Implementation Pattern

### JournaledGrain Base

```csharp
public class OrderGrain : JournaledGrain<OrderState, IOrderEvent>, IOrderGrain
{
    public OrderGrain(ILogConsistencyProtocolServices services)
        : base(services) { }

    // Events define state transitions
    protected override void TransitionState(OrderState state, IOrderEvent @event)
    {
        switch (@event)
        {
            case OrderCreatedEvent e:
                state.Id = e.OrderId;
                state.SiteId = e.SiteId;
                state.Status = OrderStatus.Open;
                state.CreatedAt = e.OccurredAt;
                break;

            case OrderLineAddedEvent e:
                state.Lines.Add(new OrderLine
                {
                    Id = e.LineId,
                    MenuItemId = e.MenuItemId,
                    Quantity = e.Quantity,
                    UnitPrice = e.UnitPrice
                });
                state.RecalculateTotals();
                break;

            case OrderSettledEvent e:
                state.Status = OrderStatus.Settled;
                state.SettledAt = e.OccurredAt;
                break;
        }

        state.UpdatedAt = @event.OccurredAt;
    }

    // Commands raise events
    public async Task<OrderCreatedResult> CreateAsync(CreateOrderCommand command)
    {
        if (State.Id != Guid.Empty)
            throw new InvalidOperationException("Order already exists");

        RaiseEvent(new OrderCreatedEvent
        {
            OrderId = command.OrderId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            ServerId = command.ServerId,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();

        return new OrderCreatedResult(State.Id, Version);
    }

    public async Task<AddLineResult> AddLineAsync(AddLineCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        var lineId = Guid.NewGuid();

        RaiseEvent(new OrderLineAddedEvent
        {
            OrderId = State.Id,
            LineId = lineId,
            MenuItemId = command.MenuItemId,
            Name = command.Name,
            Quantity = command.Quantity,
            UnitPrice = command.UnitPrice,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();

        return new AddLineResult(lineId, State.GrandTotal, Version);
    }

    // Version is automatic
    public Task<int> GetVersionAsync() => Task.FromResult(Version);

    // Can query historical state
    public async Task<OrderState> GetStateAtVersionAsync(int version)
    {
        return await RetrieveConfirmedEvents(0, version)
            .AggregateAsync(new OrderState(), (state, e) =>
            {
                TransitionState(state, e);
                return state;
            });
    }
}
```

### Event Definitions

Events must be serializable and immutable:

```csharp
public interface IOrderEvent
{
    Guid OrderId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record OrderCreatedEvent : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid SiteId { get; init; }
    [Id(3)] public Guid ServerId { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record OrderLineAddedEvent : IOrderEvent
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public Guid MenuItemId { get; init; }
    [Id(3)] public string Name { get; init; } = "";
    [Id(4)] public int Quantity { get; init; }
    [Id(5)] public decimal UnitPrice { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}
```

### State Class

State classes should be mutable for `TransitionState`:

```csharp
[GenerateSerializer]
public class OrderState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public OrderStatus Status { get; set; }
    [Id(4)] public List<OrderLine> Lines { get; set; } = [];
    [Id(5)] public decimal GrandTotal { get; set; }
    [Id(6)] public DateTime CreatedAt { get; set; }
    [Id(7)] public DateTime UpdatedAt { get; set; }

    // No Version property needed - JournaledGrain provides this.Version

    public void RecalculateTotals()
    {
        GrandTotal = Lines.Sum(l => l.Quantity * l.UnitPrice);
    }
}
```

## Configuration

### Log Consistency Provider

Configure the log consistency provider in `Program.cs`:

```csharp
builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder
        .AddLogStorageBasedLogConsistencyProvider("LogStorage")
        .AddMemoryGrainStorage("LogStorage"); // Use PostgreSQL in production
});
```

For production with PostgreSQL:

```csharp
siloBuilder
    .AddLogStorageBasedLogConsistencyProvider("LogStorage")
    .AddAdoNetGrainStorage("LogStorage", options =>
    {
        options.Invariant = "Npgsql";
        options.ConnectionString = connectionString;
    });
```

### Grain Registration

Journaled grains use the `[LogConsistencyProvider]` attribute:

```csharp
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class OrderGrain : JournaledGrain<OrderState, IOrderEvent>, IOrderGrain
{
    // ...
}
```

## Migration Strategy

1. **Create event interfaces** for each journaled grain domain
2. **Define event records** with proper serialization attributes
3. **Convert grain class** to inherit from `JournaledGrain<TState, TEvent>`
4. **Implement TransitionState** to apply events to state
5. **Replace state mutations** with `RaiseEvent()` + `ConfirmEvents()`
6. **Remove manual Version property** from state classes
7. **Update tests** to verify event replay behavior

### Migration Checklist

**Critical - Financial Transactions:**
- [x] OrderGrain - Converted to JournaledGrain with full event sourcing
- [ ] PaymentGrain - Events defined in PaymentJournaledEvents.cs
- [ ] InventoryGrain - Events defined in InventoryJournaledEvents.cs
- [ ] AccountGrain - Events defined in AccountJournaledEvents.cs
- [ ] GiftCardGrain - Events defined in GiftCardJournaledEvents.cs

**High Priority - Business Operations:**
- [ ] BookingGrain - Events defined in BookingJournaledEvents.cs
- [ ] CustomerGrain - Events defined in CustomerJournaledEvents.cs
- [ ] ExpenseGrain - Events defined in ExpenseJournaledEvents.cs
- [ ] PurchaseDocumentGrain - Events defined in PurchaseDocumentJournaledEvents.cs
- [ ] EmployeeGrain - Events defined in EmployeeJournaledEvents.cs
- [ ] LaborGrains (ScheduleGrain, TimecardGrain, TipPoolGrain)

### Event Files Created

All journaled event interfaces and records are defined in:
`src/DarkVelocity.Host/Events/JournaledEvents/`

| File | Grain | Events |
|------|-------|--------|
| OrderJournaledEvents.cs | OrderGrain | 16 events |
| PaymentJournaledEvents.cs | PaymentGrain | 12 events |
| InventoryJournaledEvents.cs | InventoryGrain | 10 events |
| GiftCardJournaledEvents.cs | GiftCardGrain | 10 events |
| BookingJournaledEvents.cs | BookingGrain | 17 events |
| CustomerJournaledEvents.cs | CustomerGrain | 18 events |
| AccountJournaledEvents.cs | AccountGrain | 7 events |
| ExpenseJournaledEvents.cs | ExpenseGrain | 8 events |
| EmployeeJournaledEvents.cs | EmployeeGrain | 15 events |
| PurchaseDocumentJournaledEvents.cs | PurchaseDocumentGrain | 13 events |

## Testing

Test both command execution and event replay:

```csharp
[Fact]
public async Task Order_CanReplayEvents()
{
    // Arrange
    var grain = await Cluster.CreateGrainAsync<IOrderGrain>(grainId);
    await grain.CreateAsync(createCommand);
    await grain.AddLineAsync(lineCommand);

    // Act - deactivate and reactivate grain
    await Cluster.DeactivateGrainAsync(grain);
    grain = await Cluster.GetGrainAsync<IOrderGrain>(grainId);

    // Assert - state rebuilt from events
    var state = await grain.GetStateAsync();
    Assert.Single(state.Lines);
    Assert.Equal(2, await grain.GetVersionAsync());
}
```

## References

- [Orleans Event Sourcing](https://learn.microsoft.com/en-us/dotnet/orleans/grains/event-sourcing/)
- [JournaledGrain Documentation](https://learn.microsoft.com/en-us/dotnet/orleans/grains/event-sourcing/journaledgrain)
- [Log-Consistency Providers](https://learn.microsoft.com/en-us/dotnet/orleans/grains/event-sourcing/log-consistency-providers)
