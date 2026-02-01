namespace DarkVelocity.Host.Search;

/// <summary>
/// Search-optimized projection of an order.
/// Denormalized for fast filtering and full-text search.
/// </summary>
[GenerateSerializer]
public sealed record OrderSearchDocument
{
    [Id(0)] public required Guid Id { get; init; }
    [Id(1)] public required Guid OrgId { get; init; }
    [Id(2)] public required Guid SiteId { get; init; }
    [Id(3)] public required string OrderNumber { get; init; }

    // Searchable text fields
    [Id(4)] public string? CustomerName { get; init; }
    [Id(5)] public string? ServerName { get; init; }
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
    [Id(14)] public required int GuestCount { get; init; }
}

/// <summary>
/// Search-optimized projection of a customer.
/// Designed for CRM fuzzy search and autocomplete.
/// </summary>
[GenerateSerializer]
public sealed record CustomerSearchDocument
{
    [Id(0)] public required Guid Id { get; init; }
    [Id(1)] public required Guid OrgId { get; init; }

    // Searchable text fields
    [Id(2)] public required string DisplayName { get; init; }
    [Id(3)] public string? FirstName { get; init; }
    [Id(4)] public string? LastName { get; init; }
    [Id(5)] public string? Email { get; init; }
    [Id(6)] public string? Phone { get; init; }

    // Filterable fields
    [Id(7)] public required string Status { get; init; }
    [Id(8)] public string? LoyaltyTier { get; init; }
    [Id(9)] public required decimal LifetimeSpend { get; init; }
    [Id(10)] public required int VisitCount { get; init; }
    [Id(11)] public DateTime? LastVisitAt { get; init; }
    [Id(12)] public required DateTime CreatedAt { get; init; }
    [Id(13)] public required string Segment { get; init; }

    // Tags for filtering
    [Id(14)] public required List<string> Tags { get; init; }
}

/// <summary>
/// Search-optimized projection of a payment.
/// Denormalized for transaction lookups and reporting.
/// </summary>
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
    [Id(7)] public string? GatewayReference { get; init; }

    // Filterable fields
    [Id(8)] public required string Method { get; init; }
    [Id(9)] public required string Status { get; init; }
    [Id(10)] public required decimal Amount { get; init; }
    [Id(11)] public required decimal TipAmount { get; init; }
    [Id(12)] public required DateTime CreatedAt { get; init; }
    [Id(13)] public DateTime? CompletedAt { get; init; }
}

/// <summary>
/// Generic search result container with pagination metadata.
/// </summary>
public sealed record SearchResult<T>
{
    public required List<T> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Skip { get; init; }
    public required int Take { get; init; }

    public bool HasMore => Skip + Items.Count < TotalCount;
}

/// <summary>
/// Query parameters for order search.
/// </summary>
public sealed record OrderSearchQuery
{
    public string? Text { get; init; }
    public Guid? SiteId { get; init; }
    public string? Status { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public decimal? MinTotal { get; init; }
    public decimal? MaxTotal { get; init; }
    public int Skip { get; init; } = 0;
    public int Take { get; init; } = 20;
    public string SortBy { get; init; } = "CreatedAt";
    public bool Descending { get; init; } = true;
}

/// <summary>
/// Query parameters for customer search.
/// </summary>
public sealed record CustomerSearchQuery
{
    public string? Text { get; init; }
    public string? Status { get; init; }
    public string? LoyaltyTier { get; init; }
    public string? Segment { get; init; }
    public decimal? MinSpend { get; init; }
    public decimal? MaxSpend { get; init; }
    public List<string>? Tags { get; init; }
    public int Skip { get; init; } = 0;
    public int Take { get; init; } = 20;
    public string SortBy { get; init; } = "DisplayName";
    public bool Descending { get; init; } = false;
}

/// <summary>
/// Query parameters for payment search.
/// </summary>
public sealed record PaymentSearchQuery
{
    public string? Text { get; init; }
    public Guid? SiteId { get; init; }
    public Guid? OrderId { get; init; }
    public string? Method { get; init; }
    public string? Status { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public int Skip { get; init; } = 0;
    public int Take { get; init; } = 20;
    public string SortBy { get; init; } = "CreatedAt";
    public bool Descending { get; init; } = true;
}
