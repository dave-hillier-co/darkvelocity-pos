# Search Architecture Design

This document outlines the architecture for adding search capabilities to DarkVelocity POS, covering Orders, Customers, and Payments.

## Overview

Search in an event-sourced system requires **projections**—read models built by subscribing to event streams and materializing data optimized for query patterns. The existing `CustomerSpendProjectionGrain` demonstrates this pattern.

## Architecture Options

### Option 1: PostgreSQL Full-Text Search (Recommended Starting Point)

**Approach**: Project events to PostgreSQL tables with GIN indexes for full-text search.

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   Order Grain   │────▶│  Orleans Stream  │────▶│ Search Projector│
│  Payment Grain  │     │   (per org)      │     │    (Grain)      │
│ Customer Grain  │     └──────────────────┘     └────────┬────────┘
└─────────────────┘                                       │
                                                          ▼
                                                ┌─────────────────┐
                                                │   PostgreSQL    │
                                                │ (search tables) │
                                                └─────────────────┘
```

**Pros:**
- No new infrastructure (already using PostgreSQL)
- ACID guarantees, strong consistency
- Good enough for most POS search needs
- Simple to implement and maintain

**Cons:**
- Limited fuzzy matching compared to dedicated search engines
- Scaling requires read replicas
- Complex relevance tuning

### Option 2: Dedicated Search Engine (Meilisearch/Typesense)

**Approach**: Use a lightweight, modern search engine optimized for instant search.

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   Order Grain   │────▶│  Orleans Stream  │────▶│ Search Projector│
│  Payment Grain  │     │   (per org)      │     │    (Grain)      │
│ Customer Grain  │     └──────────────────┘     └────────┬────────┘
└─────────────────┘                                       │
                                                          ▼
                                                ┌─────────────────┐
                                                │   Meilisearch   │
                                                │  (per tenant)   │
                                                └─────────────────┘
```

**Pros:**
- Instant search with typo tolerance
- Faceted filtering out of the box
- Excellent relevance ranking
- Easy to operate (single binary)

**Cons:**
- Additional infrastructure
- Eventually consistent
- Additional sync complexity

### Option 3: Hybrid (Recommended for Production)

PostgreSQL for transactional queries + Search engine for full-text. Start with Option 1, migrate to Option 3 when needed.

## Recommended Implementation

### Phase 1: PostgreSQL Search Tables

#### Search Document Models

```csharp
// Search-optimized projection of an order
[GenerateSerializer]
public sealed record OrderSearchDocument
{
    [Id(0)] public required Guid Id { get; init; }
    [Id(1)] public required Guid OrgId { get; init; }
    [Id(2)] public required Guid SiteId { get; init; }
    [Id(3)] public required string OrderNumber { get; init; }

    // Searchable text fields
    [Id(4)] public required string CustomerName { get; init; }
    [Id(5)] public required string ServerName { get; init; }
    [Id(6)] public string? TableNumber { get; init; }
    [Id(7)] public string? Notes { get; init; }

    // Filterable fields
    [Id(8)] public required string Status { get; init; }
    [Id(9)] public required string OrderType { get; init; }
    [Id(10)] public required decimal GrandTotal { get; init; }
    [Id(11)] public required DateTime CreatedAt { get; init; }
    [Id(12)] public DateTime? ClosedAt { get; init; }

    // Denormalized for display
    [Id(13)] public required int ItemCount { get; init; }
    [Id(14)] public required string SiteName { get; init; }

    // For PostgreSQL FTS
    [Id(15)] public required string SearchVector { get; init; }
}

// Search-optimized projection of a customer
[GenerateSerializer]
public sealed record CustomerSearchDocument
{
    [Id(0)] public required Guid Id { get; init; }
    [Id(1)] public required Guid OrgId { get; init; }

    // Searchable text fields
    [Id(2)] public required string DisplayName { get; init; }
    [Id(3)] public string? Email { get; init; }
    [Id(4)] public string? Phone { get; init; }
    [Id(5)] public string? Notes { get; init; }

    // Filterable fields
    [Id(6)] public required string Status { get; init; }
    [Id(7)] public string? LoyaltyTier { get; init; }
    [Id(8)] public required decimal LifetimeSpend { get; init; }
    [Id(9)] public required int VisitCount { get; init; }
    [Id(10)] public DateTime? LastVisitAt { get; init; }
    [Id(11)] public required DateTime CreatedAt { get; init; }

    // Tags for filtering
    [Id(12)] public required List<string> Tags { get; init; }

    // For PostgreSQL FTS
    [Id(13)] public required string SearchVector { get; init; }
}

// Search-optimized projection of a payment
[GenerateSerializer]
public sealed record PaymentSearchDocument
{
    [Id(0)] public required Guid Id { get; init; }
    [Id(1)] public required Guid OrgId { get; init; }
    [Id(2)] public required Guid SiteId { get; init; }
    [Id(3)] public required Guid OrderId { get; init; }

    // Searchable text fields
    [Id(4)] public required string OrderNumber { get; init; }
    [Id(5)] public string? CustomerName { get; init; }
    [Id(6)] public string? CardLastFour { get; init; }
    [Id(7)] public string? ReferenceNumber { get; init; }

    // Filterable fields
    [Id(8)] public required string Method { get; init; }
    [Id(9)] public required string Status { get; init; }
    [Id(10)] public required decimal Amount { get; init; }
    [Id(11)] public required DateTime CreatedAt { get; init; }
    [Id(12)] public DateTime? CompletedAt { get; init; }

    // For PostgreSQL FTS
    [Id(13)] public required string SearchVector { get; init; }
}
```

#### PostgreSQL Schema

```sql
-- Orders search table
CREATE TABLE order_search (
    id UUID PRIMARY KEY,
    org_id UUID NOT NULL,
    site_id UUID NOT NULL,
    order_number TEXT NOT NULL,
    customer_name TEXT,
    server_name TEXT,
    table_number TEXT,
    notes TEXT,
    status TEXT NOT NULL,
    order_type TEXT NOT NULL,
    grand_total DECIMAL(18,2) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    closed_at TIMESTAMPTZ,
    item_count INT NOT NULL,
    site_name TEXT NOT NULL,

    -- Full-text search vector
    search_vector TSVECTOR GENERATED ALWAYS AS (
        setweight(to_tsvector('english', coalesce(order_number, '')), 'A') ||
        setweight(to_tsvector('english', coalesce(customer_name, '')), 'B') ||
        setweight(to_tsvector('english', coalesce(server_name, '')), 'C') ||
        setweight(to_tsvector('english', coalesce(notes, '')), 'D')
    ) STORED
);

CREATE INDEX idx_order_search_org ON order_search(org_id);
CREATE INDEX idx_order_search_site ON order_search(org_id, site_id);
CREATE INDEX idx_order_search_status ON order_search(org_id, status);
CREATE INDEX idx_order_search_created ON order_search(org_id, created_at DESC);
CREATE INDEX idx_order_search_fts ON order_search USING GIN(search_vector);

-- Customers search table
CREATE TABLE customer_search (
    id UUID PRIMARY KEY,
    org_id UUID NOT NULL,
    display_name TEXT NOT NULL,
    email TEXT,
    phone TEXT,
    notes TEXT,
    status TEXT NOT NULL,
    loyalty_tier TEXT,
    lifetime_spend DECIMAL(18,2) NOT NULL,
    visit_count INT NOT NULL,
    last_visit_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL,
    tags TEXT[] NOT NULL DEFAULT '{}',

    search_vector TSVECTOR GENERATED ALWAYS AS (
        setweight(to_tsvector('english', coalesce(display_name, '')), 'A') ||
        setweight(to_tsvector('english', coalesce(email, '')), 'B') ||
        setweight(to_tsvector('english', coalesce(phone, '')), 'B') ||
        setweight(to_tsvector('english', coalesce(notes, '')), 'D')
    ) STORED
);

CREATE INDEX idx_customer_search_org ON customer_search(org_id);
CREATE INDEX idx_customer_search_tier ON customer_search(org_id, loyalty_tier);
CREATE INDEX idx_customer_search_spend ON customer_search(org_id, lifetime_spend DESC);
CREATE INDEX idx_customer_search_fts ON customer_search USING GIN(search_vector);
CREATE INDEX idx_customer_search_tags ON customer_search USING GIN(tags);

-- Payments search table
CREATE TABLE payment_search (
    id UUID PRIMARY KEY,
    org_id UUID NOT NULL,
    site_id UUID NOT NULL,
    order_id UUID NOT NULL,
    order_number TEXT NOT NULL,
    customer_name TEXT,
    card_last_four TEXT,
    reference_number TEXT,
    method TEXT NOT NULL,
    status TEXT NOT NULL,
    amount DECIMAL(18,2) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    completed_at TIMESTAMPTZ,

    search_vector TSVECTOR GENERATED ALWAYS AS (
        setweight(to_tsvector('english', coalesce(order_number, '')), 'A') ||
        setweight(to_tsvector('english', coalesce(customer_name, '')), 'B') ||
        setweight(to_tsvector('english', coalesce(card_last_four, '')), 'B') ||
        setweight(to_tsvector('english', coalesce(reference_number, '')), 'B')
    ) STORED
);

CREATE INDEX idx_payment_search_org ON payment_search(org_id);
CREATE INDEX idx_payment_search_site ON payment_search(org_id, site_id);
CREATE INDEX idx_payment_search_order ON payment_search(order_id);
CREATE INDEX idx_payment_search_method ON payment_search(org_id, method);
CREATE INDEX idx_payment_search_created ON payment_search(org_id, created_at DESC);
CREATE INDEX idx_payment_search_fts ON payment_search USING GIN(search_vector);
```

### Search Projection Grain

A single grain per organization subscribes to all relevant streams and projects to search tables:

```csharp
public interface ISearchProjectionGrain : IGrainWithStringKey
{
    Task InitializeAsync();
    Task RebuildAsync(); // Full rebuild from event store
}

public class SearchProjectionGrain : Grain, ISearchProjectionGrain
{
    private readonly IDbContextFactory<SearchDbContext> _dbFactory;
    private StreamSubscriptionHandle<IStreamEvent>? _orderSubscription;
    private StreamSubscriptionHandle<IStreamEvent>? _paymentSubscription;
    private StreamSubscriptionHandle<IStreamEvent>? _customerSubscription;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        var orgId = this.GetPrimaryKeyString(); // org:{orgId}
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);

        // Subscribe to order events
        var orderStream = streamProvider.GetStream<IStreamEvent>(
            StreamId.Create(StreamConstants.OrderStreamNamespace, orgId));
        _orderSubscription = await orderStream.SubscribeAsync(OnOrderEventAsync);

        // Subscribe to payment events
        var paymentStream = streamProvider.GetStream<IStreamEvent>(
            StreamId.Create(StreamConstants.PaymentStreamNamespace, orgId));
        _paymentSubscription = await paymentStream.SubscribeAsync(OnPaymentEventAsync);

        // Subscribe to customer events
        var customerStream = streamProvider.GetStream<IStreamEvent>(
            StreamId.Create(StreamConstants.CustomerStreamNamespace, orgId));
        _customerSubscription = await customerStream.SubscribeAsync(OnCustomerEventAsync);
    }

    private async Task OnOrderEventAsync(IStreamEvent @event, StreamSequenceToken? token)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        switch (@event)
        {
            case OrderCreatedEvent created:
                await db.OrderSearch.AddAsync(MapToSearchDocument(created));
                break;
            case OrderCompletedEvent completed:
                await UpdateOrderStatus(db, completed.OrderId, "Completed", completed);
                break;
            case OrderVoidedEvent voided:
                await UpdateOrderStatus(db, voided.OrderId, "Voided", voided);
                break;
            // ... other events
        }

        await db.SaveChangesAsync();
    }

    // Similar handlers for payments and customers...
}
```

### Search Query API

```csharp
public interface ISearchService
{
    Task<SearchResult<OrderSearchDocument>> SearchOrdersAsync(
        Guid orgId,
        OrderSearchQuery query,
        CancellationToken ct = default);

    Task<SearchResult<CustomerSearchDocument>> SearchCustomersAsync(
        Guid orgId,
        CustomerSearchQuery query,
        CancellationToken ct = default);

    Task<SearchResult<PaymentSearchDocument>> SearchPaymentsAsync(
        Guid orgId,
        PaymentSearchQuery query,
        CancellationToken ct = default);
}

public record OrderSearchQuery
{
    public string? Text { get; init; }           // Full-text search
    public Guid? SiteId { get; init; }           // Filter by site
    public string? Status { get; init; }         // Filter by status
    public DateOnly? FromDate { get; init; }     // Date range
    public DateOnly? ToDate { get; init; }
    public decimal? MinTotal { get; init; }      // Amount range
    public decimal? MaxTotal { get; init; }
    public int Skip { get; init; } = 0;
    public int Take { get; init; } = 20;
    public string SortBy { get; init; } = "created_at";
    public bool Descending { get; init; } = true;
}

public record SearchResult<T>
{
    public required List<T> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Skip { get; init; }
    public required int Take { get; init; }
}
```

### API Endpoints

```csharp
// GET /api/orgs/{orgId}/search/orders?q=smith&status=open&from=2024-01-01
// GET /api/orgs/{orgId}/search/customers?q=john&tier=gold
// GET /api/orgs/{orgId}/search/payments?method=card&from=2024-01-01
```

## Phase 2: Dedicated Search Engine (Future)

When PostgreSQL FTS becomes insufficient:

### Meilisearch Integration

```csharp
public interface ISearchIndexer
{
    Task IndexOrderAsync(OrderSearchDocument doc, CancellationToken ct = default);
    Task IndexCustomerAsync(CustomerSearchDocument doc, CancellationToken ct = default);
    Task IndexPaymentAsync(PaymentSearchDocument doc, CancellationToken ct = default);
    Task DeleteOrderAsync(Guid orgId, Guid orderId, CancellationToken ct = default);
    // ...
}

public class MeilisearchIndexer : ISearchIndexer
{
    private readonly MeilisearchClient _client;

    // Index per tenant for isolation
    private string GetIndexName(Guid orgId, string type)
        => $"{orgId}_{type}";

    public async Task IndexOrderAsync(OrderSearchDocument doc, CancellationToken ct)
    {
        var index = _client.Index(GetIndexName(doc.OrgId, "orders"));
        await index.AddDocumentsAsync(new[] { doc }, cancellationToken: ct);
    }
}
```

### Configuration

```json
{
  "Search": {
    "Provider": "PostgreSQL", // or "Meilisearch"
    "Meilisearch": {
      "Url": "http://localhost:7700",
      "ApiKey": "masterKey"
    }
  }
}
```

## Event Flow Summary

```
┌─────────────┐    Event    ┌─────────────────┐    Upsert    ┌────────────┐
│ OrderGrain  │────────────▶│ SearchProjection│─────────────▶│ PostgreSQL │
│ completes   │             │ Grain           │              │ or Search  │
│ order       │             │ (per org)       │              │ Engine     │
└─────────────┘             └─────────────────┘              └────────────┘
       │                                                            │
       │                                                            │
       ▼                                                            ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                           Search API                                     │
│  GET /api/orgs/{orgId}/search/orders?q=smith&status=completed           │
└─────────────────────────────────────────────────────────────────────────┘
```

## Key Design Decisions

### 1. One Projection Grain Per Organization

- Simplifies stream subscription management
- Provides natural tenant isolation
- Can be sharded later (e.g., by site) if needed

### 2. Separate Search Tables (Not Views)

- Allows independent schema evolution
- Enables search-specific indexes
- Decouples read model from write model

### 3. Upsert Pattern

Events are idempotent—replaying events produces the same search state:

```csharp
await db.OrderSearch
    .Upsert(doc)
    .On(o => o.Id)
    .WhenMatched(o => new OrderSearchDocument {
        Status = doc.Status,
        ClosedAt = doc.ClosedAt,
        // ... only update changed fields
    })
    .RunAsync();
```

### 4. Eventual Consistency

Search results may lag a few hundred milliseconds behind writes. This is acceptable for search use cases. For real-time data, query the grain directly.

### 5. Rebuild Capability

The projection grain can rebuild its state from the event store:

```csharp
public async Task RebuildAsync()
{
    // Truncate search tables for this org
    // Replay all events from event store
    // This enables schema migrations and bug fixes
}
```

## Testing Strategy

```csharp
[Fact]
public async Task OrderSearch_FindsByCustomerName()
{
    // Arrange
    var order = await CreateOrderWithCustomer("John Smith");
    await Task.Delay(500); // Allow projection to process

    // Act
    var results = await _searchService.SearchOrdersAsync(
        _orgId,
        new OrderSearchQuery { Text = "smith" });

    // Assert
    Assert.Single(results.Items);
    Assert.Equal(order.Id, results.Items[0].Id);
}
```

## Migration Path

1. **Deploy search tables** with EF migrations
2. **Deploy SearchProjectionGrain** subscribing to streams
3. **Run initial backfill** from existing grain states
4. **Enable search API** endpoints
5. **Monitor and optimize** based on query patterns
6. **Evaluate Meilisearch** when FTS limitations emerge

## Open Questions

1. **Backfill strategy**: Query all grains or replay from event store?
2. **Retention**: How long to keep search documents for voided/old orders?
3. **Permissions**: Integrate with SpiceDB for row-level access control?
4. **Analytics**: Should search queries be logged for analysis?
