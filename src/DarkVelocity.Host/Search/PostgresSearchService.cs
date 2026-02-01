using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Host.Search;

/// <summary>
/// PostgreSQL full-text search implementation.
/// Uses tsvector/tsquery for text search with GIN indexes.
/// </summary>
public class PostgresSearchService : ISearchService, ISearchIndexer
{
    private readonly IDbContextFactory<SearchDbContext> _dbFactory;
    private readonly ILogger<PostgresSearchService> _logger;

    public PostgresSearchService(
        IDbContextFactory<SearchDbContext> dbFactory,
        ILogger<PostgresSearchService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    #region ISearchService Implementation

    public async Task<SearchResult<OrderSearchDocument>> SearchOrdersAsync(
        Guid orgId,
        OrderSearchQuery query,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var baseQuery = db.Orders.Where(o => o.OrgId == orgId);

        // Apply filters
        if (query.SiteId.HasValue)
            baseQuery = baseQuery.Where(o => o.SiteId == query.SiteId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status))
            baseQuery = baseQuery.Where(o => o.Status == query.Status);

        if (query.FromDate.HasValue)
            baseQuery = baseQuery.Where(o => o.CreatedAt >= query.FromDate.Value.ToDateTime(TimeOnly.MinValue));

        if (query.ToDate.HasValue)
            baseQuery = baseQuery.Where(o => o.CreatedAt < query.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));

        if (query.MinTotal.HasValue)
            baseQuery = baseQuery.Where(o => o.GrandTotal >= query.MinTotal.Value);

        if (query.MaxTotal.HasValue)
            baseQuery = baseQuery.Where(o => o.GrandTotal <= query.MaxTotal.Value);

        // Apply full-text search
        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var searchQuery = BuildTsQuery(query.Text);
            baseQuery = baseQuery.Where(o =>
                o.SearchVector != null &&
                o.SearchVector.Matches(EF.Functions.PlainToTsQuery("english", searchQuery)));
        }

        // Get total count
        var totalCount = await baseQuery.CountAsync(ct);

        // Apply sorting
        baseQuery = query.SortBy?.ToLowerInvariant() switch
        {
            "ordernumber" => query.Descending
                ? baseQuery.OrderByDescending(o => o.OrderNumber)
                : baseQuery.OrderBy(o => o.OrderNumber),
            "grandtotal" => query.Descending
                ? baseQuery.OrderByDescending(o => o.GrandTotal)
                : baseQuery.OrderBy(o => o.GrandTotal),
            "status" => query.Descending
                ? baseQuery.OrderByDescending(o => o.Status)
                : baseQuery.OrderBy(o => o.Status),
            _ => query.Descending
                ? baseQuery.OrderByDescending(o => o.CreatedAt)
                : baseQuery.OrderBy(o => o.CreatedAt)
        };

        // Apply pagination and project to documents
        var items = await baseQuery
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(o => new OrderSearchDocument
            {
                Id = o.Id,
                OrgId = o.OrgId,
                SiteId = o.SiteId,
                OrderNumber = o.OrderNumber,
                CustomerName = o.CustomerName,
                ServerName = o.ServerName,
                TableNumber = o.TableNumber,
                Notes = o.Notes,
                Status = o.Status,
                OrderType = o.OrderType,
                GrandTotal = o.GrandTotal,
                CreatedAt = o.CreatedAt,
                ClosedAt = o.ClosedAt,
                ItemCount = o.ItemCount,
                GuestCount = o.GuestCount
            })
            .ToListAsync(ct);

        return new SearchResult<OrderSearchDocument>
        {
            Items = items,
            TotalCount = totalCount,
            Skip = query.Skip,
            Take = query.Take
        };
    }

    public async Task<SearchResult<CustomerSearchDocument>> SearchCustomersAsync(
        Guid orgId,
        CustomerSearchQuery query,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var baseQuery = db.Customers.Where(c => c.OrgId == orgId);

        // Apply filters
        if (!string.IsNullOrWhiteSpace(query.Status))
            baseQuery = baseQuery.Where(c => c.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.LoyaltyTier))
            baseQuery = baseQuery.Where(c => c.LoyaltyTier == query.LoyaltyTier);

        if (!string.IsNullOrWhiteSpace(query.Segment))
            baseQuery = baseQuery.Where(c => c.Segment == query.Segment);

        if (query.MinSpend.HasValue)
            baseQuery = baseQuery.Where(c => c.LifetimeSpend >= query.MinSpend.Value);

        if (query.MaxSpend.HasValue)
            baseQuery = baseQuery.Where(c => c.LifetimeSpend <= query.MaxSpend.Value);

        if (query.Tags is { Count: > 0 })
            baseQuery = baseQuery.Where(c => c.Tags.Any(t => query.Tags.Contains(t)));

        // Apply full-text search
        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var searchQuery = BuildTsQuery(query.Text);
            baseQuery = baseQuery.Where(c =>
                c.SearchVector != null &&
                c.SearchVector.Matches(EF.Functions.PlainToTsQuery("english", searchQuery)));
        }

        // Get total count
        var totalCount = await baseQuery.CountAsync(ct);

        // Apply sorting
        baseQuery = query.SortBy?.ToLowerInvariant() switch
        {
            "lifetimespend" => query.Descending
                ? baseQuery.OrderByDescending(c => c.LifetimeSpend)
                : baseQuery.OrderBy(c => c.LifetimeSpend),
            "visitcount" => query.Descending
                ? baseQuery.OrderByDescending(c => c.VisitCount)
                : baseQuery.OrderBy(c => c.VisitCount),
            "lastvisitat" => query.Descending
                ? baseQuery.OrderByDescending(c => c.LastVisitAt)
                : baseQuery.OrderBy(c => c.LastVisitAt),
            "createdat" => query.Descending
                ? baseQuery.OrderByDescending(c => c.CreatedAt)
                : baseQuery.OrderBy(c => c.CreatedAt),
            _ => query.Descending
                ? baseQuery.OrderByDescending(c => c.DisplayName)
                : baseQuery.OrderBy(c => c.DisplayName)
        };

        // Apply pagination and project to documents
        var items = await baseQuery
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(c => new CustomerSearchDocument
            {
                Id = c.Id,
                OrgId = c.OrgId,
                DisplayName = c.DisplayName,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Email = c.Email,
                Phone = c.Phone,
                Status = c.Status,
                LoyaltyTier = c.LoyaltyTier,
                LifetimeSpend = c.LifetimeSpend,
                VisitCount = c.VisitCount,
                LastVisitAt = c.LastVisitAt,
                CreatedAt = c.CreatedAt,
                Segment = c.Segment,
                Tags = c.Tags
            })
            .ToListAsync(ct);

        return new SearchResult<CustomerSearchDocument>
        {
            Items = items,
            TotalCount = totalCount,
            Skip = query.Skip,
            Take = query.Take
        };
    }

    public async Task<SearchResult<PaymentSearchDocument>> SearchPaymentsAsync(
        Guid orgId,
        PaymentSearchQuery query,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var baseQuery = db.Payments.Where(p => p.OrgId == orgId);

        // Apply filters
        if (query.SiteId.HasValue)
            baseQuery = baseQuery.Where(p => p.SiteId == query.SiteId.Value);

        if (query.OrderId.HasValue)
            baseQuery = baseQuery.Where(p => p.OrderId == query.OrderId.Value);

        if (!string.IsNullOrWhiteSpace(query.Method))
            baseQuery = baseQuery.Where(p => p.Method == query.Method);

        if (!string.IsNullOrWhiteSpace(query.Status))
            baseQuery = baseQuery.Where(p => p.Status == query.Status);

        if (query.FromDate.HasValue)
            baseQuery = baseQuery.Where(p => p.CreatedAt >= query.FromDate.Value.ToDateTime(TimeOnly.MinValue));

        if (query.ToDate.HasValue)
            baseQuery = baseQuery.Where(p => p.CreatedAt < query.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));

        if (query.MinAmount.HasValue)
            baseQuery = baseQuery.Where(p => p.Amount >= query.MinAmount.Value);

        if (query.MaxAmount.HasValue)
            baseQuery = baseQuery.Where(p => p.Amount <= query.MaxAmount.Value);

        // Apply full-text search
        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var searchQuery = BuildTsQuery(query.Text);
            baseQuery = baseQuery.Where(p =>
                p.SearchVector != null &&
                p.SearchVector.Matches(EF.Functions.PlainToTsQuery("english", searchQuery)));
        }

        // Get total count
        var totalCount = await baseQuery.CountAsync(ct);

        // Apply sorting
        baseQuery = query.SortBy?.ToLowerInvariant() switch
        {
            "amount" => query.Descending
                ? baseQuery.OrderByDescending(p => p.Amount)
                : baseQuery.OrderBy(p => p.Amount),
            "method" => query.Descending
                ? baseQuery.OrderByDescending(p => p.Method)
                : baseQuery.OrderBy(p => p.Method),
            "status" => query.Descending
                ? baseQuery.OrderByDescending(p => p.Status)
                : baseQuery.OrderBy(p => p.Status),
            _ => query.Descending
                ? baseQuery.OrderByDescending(p => p.CreatedAt)
                : baseQuery.OrderBy(p => p.CreatedAt)
        };

        // Apply pagination and project to documents
        var items = await baseQuery
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(p => new PaymentSearchDocument
            {
                Id = p.Id,
                OrgId = p.OrgId,
                SiteId = p.SiteId,
                OrderId = p.OrderId,
                OrderNumber = p.OrderNumber,
                CustomerName = p.CustomerName,
                CardLastFour = p.CardLastFour,
                GatewayReference = p.GatewayReference,
                Method = p.Method,
                Status = p.Status,
                Amount = p.Amount,
                TipAmount = p.TipAmount,
                CreatedAt = p.CreatedAt,
                CompletedAt = p.CompletedAt
            })
            .ToListAsync(ct);

        return new SearchResult<PaymentSearchDocument>
        {
            Items = items,
            TotalCount = totalCount,
            Skip = query.Skip,
            Take = query.Take
        };
    }

    #endregion

    #region ISearchIndexer Implementation

    public async Task IndexOrderAsync(OrderSearchDocument document, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.Orders.FindAsync([document.Id], ct);
        if (entity == null)
        {
            entity = new OrderSearchEntity { Id = document.Id };
            db.Orders.Add(entity);
        }

        entity.OrgId = document.OrgId;
        entity.SiteId = document.SiteId;
        entity.OrderNumber = document.OrderNumber;
        entity.CustomerName = document.CustomerName;
        entity.ServerName = document.ServerName;
        entity.TableNumber = document.TableNumber;
        entity.Notes = document.Notes;
        entity.Status = document.Status;
        entity.OrderType = document.OrderType;
        entity.GrandTotal = document.GrandTotal;
        entity.CreatedAt = document.CreatedAt;
        entity.ClosedAt = document.ClosedAt;
        entity.ItemCount = document.ItemCount;
        entity.GuestCount = document.GuestCount;

        // Update search vector using raw SQL for proper tsvector generation
        await db.SaveChangesAsync(ct);
        await UpdateOrderSearchVectorAsync(db, document.Id, ct);
    }

    public async Task IndexCustomerAsync(CustomerSearchDocument document, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.Customers.FindAsync([document.Id], ct);
        if (entity == null)
        {
            entity = new CustomerSearchEntity { Id = document.Id };
            db.Customers.Add(entity);
        }

        entity.OrgId = document.OrgId;
        entity.DisplayName = document.DisplayName;
        entity.FirstName = document.FirstName;
        entity.LastName = document.LastName;
        entity.Email = document.Email;
        entity.Phone = document.Phone;
        entity.Status = document.Status;
        entity.LoyaltyTier = document.LoyaltyTier;
        entity.LifetimeSpend = document.LifetimeSpend;
        entity.VisitCount = document.VisitCount;
        entity.LastVisitAt = document.LastVisitAt;
        entity.CreatedAt = document.CreatedAt;
        entity.Segment = document.Segment;
        entity.Tags = document.Tags;

        await db.SaveChangesAsync(ct);
        await UpdateCustomerSearchVectorAsync(db, document.Id, ct);
    }

    public async Task IndexPaymentAsync(PaymentSearchDocument document, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.Payments.FindAsync([document.Id], ct);
        if (entity == null)
        {
            entity = new PaymentSearchEntity { Id = document.Id };
            db.Payments.Add(entity);
        }

        entity.OrgId = document.OrgId;
        entity.SiteId = document.SiteId;
        entity.OrderId = document.OrderId;
        entity.OrderNumber = document.OrderNumber;
        entity.CustomerName = document.CustomerName;
        entity.CardLastFour = document.CardLastFour;
        entity.GatewayReference = document.GatewayReference;
        entity.Method = document.Method;
        entity.Status = document.Status;
        entity.Amount = document.Amount;
        entity.TipAmount = document.TipAmount;
        entity.CreatedAt = document.CreatedAt;
        entity.CompletedAt = document.CompletedAt;

        await db.SaveChangesAsync(ct);
        await UpdatePaymentSearchVectorAsync(db, document.Id, ct);
    }

    public async Task UpdateOrderStatusAsync(Guid orderId, string status, DateTime? closedAt, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Orders
            .Where(o => o.Id == orderId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, status)
                .SetProperty(o => o.ClosedAt, closedAt), ct);
    }

    public async Task UpdatePaymentStatusAsync(Guid paymentId, string status, DateTime? completedAt, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Payments
            .Where(p => p.Id == paymentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, status)
                .SetProperty(p => p.CompletedAt, completedAt), ct);
    }

    public async Task UpdateCustomerStatsAsync(Guid customerId, decimal lifetimeSpend, int visitCount, DateTime? lastVisitAt, string segment, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Customers
            .Where(c => c.Id == customerId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.LifetimeSpend, lifetimeSpend)
                .SetProperty(c => c.VisitCount, visitCount)
                .SetProperty(c => c.LastVisitAt, lastVisitAt)
                .SetProperty(c => c.Segment, segment), ct);
    }

    public async Task DeleteOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Orders.Where(o => o.Id == orderId).ExecuteDeleteAsync(ct);
    }

    public async Task DeleteCustomerAsync(Guid customerId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Customers.Where(c => c.Id == customerId).ExecuteDeleteAsync(ct);
    }

    public async Task DeletePaymentAsync(Guid paymentId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Payments.Where(p => p.Id == paymentId).ExecuteDeleteAsync(ct);
    }

    #endregion

    #region Private Helpers

    private static string BuildTsQuery(string text)
    {
        // Simple preprocessing: split into words and join with &
        // For production, consider more sophisticated query parsing
        return string.Join(" & ", text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static async Task UpdateOrderSearchVectorAsync(SearchDbContext db, Guid orderId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE order_search SET search_vector =
                setweight(to_tsvector('english', coalesce(order_number, '')), 'A') ||
                setweight(to_tsvector('english', coalesce(customer_name, '')), 'B') ||
                setweight(to_tsvector('english', coalesce(server_name, '')), 'C') ||
                setweight(to_tsvector('english', coalesce(notes, '')), 'D')
            WHERE id = {orderId}", ct);
    }

    private static async Task UpdateCustomerSearchVectorAsync(SearchDbContext db, Guid customerId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE customer_search SET search_vector =
                setweight(to_tsvector('english', coalesce(display_name, '')), 'A') ||
                setweight(to_tsvector('english', coalesce(first_name, '')), 'A') ||
                setweight(to_tsvector('english', coalesce(last_name, '')), 'A') ||
                setweight(to_tsvector('english', coalesce(email, '')), 'B') ||
                setweight(to_tsvector('english', coalesce(phone, '')), 'B')
            WHERE id = {customerId}", ct);
    }

    private static async Task UpdatePaymentSearchVectorAsync(SearchDbContext db, Guid paymentId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE payment_search SET search_vector =
                setweight(to_tsvector('english', coalesce(order_number, '')), 'A') ||
                setweight(to_tsvector('english', coalesce(customer_name, '')), 'B') ||
                setweight(to_tsvector('english', coalesce(card_last_four, '')), 'B') ||
                setweight(to_tsvector('english', coalesce(gateway_reference, '')), 'B')
            WHERE id = {paymentId}", ct);
    }

    #endregion
}
