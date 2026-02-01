using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Host.Search;

/// <summary>
/// EF Core DbContext for search projections.
/// Uses PostgreSQL with full-text search indexes.
/// </summary>
public class SearchDbContext : DbContext
{
    public SearchDbContext(DbContextOptions<SearchDbContext> options) : base(options)
    {
    }

    public DbSet<OrderSearchEntity> Orders => Set<OrderSearchEntity>();
    public DbSet<CustomerSearchEntity> Customers => Set<CustomerSearchEntity>();
    public DbSet<PaymentSearchEntity> Payments => Set<PaymentSearchEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureOrderSearch(modelBuilder);
        ConfigureCustomerSearch(modelBuilder);
        ConfigurePaymentSearch(modelBuilder);
    }

    private static void ConfigureOrderSearch(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderSearchEntity>(entity =>
        {
            entity.ToTable("order_search");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrgId).HasColumnName("org_id").IsRequired();
            entity.Property(e => e.SiteId).HasColumnName("site_id").IsRequired();
            entity.Property(e => e.OrderNumber).HasColumnName("order_number").HasMaxLength(50).IsRequired();
            entity.Property(e => e.CustomerName).HasColumnName("customer_name").HasMaxLength(200);
            entity.Property(e => e.ServerName).HasColumnName("server_name").HasMaxLength(200);
            entity.Property(e => e.TableNumber).HasColumnName("table_number").HasMaxLength(50);
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
            entity.Property(e => e.OrderType).HasColumnName("order_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.GrandTotal).HasColumnName("grand_total").HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at");
            entity.Property(e => e.ItemCount).HasColumnName("item_count").IsRequired();
            entity.Property(e => e.GuestCount).HasColumnName("guest_count").IsRequired();
            entity.Property(e => e.SearchVector).HasColumnName("search_vector").HasColumnType("tsvector");

            // Indexes for common query patterns
            entity.HasIndex(e => e.OrgId).HasDatabaseName("idx_order_search_org");
            entity.HasIndex(e => new { e.OrgId, e.SiteId }).HasDatabaseName("idx_order_search_site");
            entity.HasIndex(e => new { e.OrgId, e.Status }).HasDatabaseName("idx_order_search_status");
            entity.HasIndex(e => new { e.OrgId, e.CreatedAt }).HasDatabaseName("idx_order_search_created").IsDescending(false, true);

            // Full-text search index
            entity.HasIndex(e => e.SearchVector)
                .HasDatabaseName("idx_order_search_fts")
                .HasMethod("GIN");
        });
    }

    private static void ConfigureCustomerSearch(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerSearchEntity>(entity =>
        {
            entity.ToTable("customer_search");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrgId).HasColumnName("org_id").IsRequired();
            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.FirstName).HasColumnName("first_name").HasMaxLength(100);
            entity.Property(e => e.LastName).HasColumnName("last_name").HasMaxLength(100);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(50);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
            entity.Property(e => e.LoyaltyTier).HasColumnName("loyalty_tier").HasMaxLength(50);
            entity.Property(e => e.LifetimeSpend).HasColumnName("lifetime_spend").HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.VisitCount).HasColumnName("visit_count").IsRequired();
            entity.Property(e => e.LastVisitAt).HasColumnName("last_visit_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(e => e.Segment).HasColumnName("segment").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Tags).HasColumnName("tags").HasColumnType("text[]");
            entity.Property(e => e.SearchVector).HasColumnName("search_vector").HasColumnType("tsvector");

            // Indexes
            entity.HasIndex(e => e.OrgId).HasDatabaseName("idx_customer_search_org");
            entity.HasIndex(e => new { e.OrgId, e.LoyaltyTier }).HasDatabaseName("idx_customer_search_tier");
            entity.HasIndex(e => new { e.OrgId, e.LifetimeSpend }).HasDatabaseName("idx_customer_search_spend").IsDescending(false, true);
            entity.HasIndex(e => new { e.OrgId, e.Segment }).HasDatabaseName("idx_customer_search_segment");

            // Full-text search index
            entity.HasIndex(e => e.SearchVector)
                .HasDatabaseName("idx_customer_search_fts")
                .HasMethod("GIN");

            // Tags GIN index for array contains queries
            entity.HasIndex(e => e.Tags)
                .HasDatabaseName("idx_customer_search_tags")
                .HasMethod("GIN");
        });
    }

    private static void ConfigurePaymentSearch(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentSearchEntity>(entity =>
        {
            entity.ToTable("payment_search");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrgId).HasColumnName("org_id").IsRequired();
            entity.Property(e => e.SiteId).HasColumnName("site_id").IsRequired();
            entity.Property(e => e.OrderId).HasColumnName("order_id").IsRequired();
            entity.Property(e => e.OrderNumber).HasColumnName("order_number").HasMaxLength(50).IsRequired();
            entity.Property(e => e.CustomerName).HasColumnName("customer_name").HasMaxLength(200);
            entity.Property(e => e.CardLastFour).HasColumnName("card_last_four").HasMaxLength(4);
            entity.Property(e => e.GatewayReference).HasColumnName("gateway_reference").HasMaxLength(100);
            entity.Property(e => e.Method).HasColumnName("method").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.TipAmount).HasColumnName("tip_amount").HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.SearchVector).HasColumnName("search_vector").HasColumnType("tsvector");

            // Indexes
            entity.HasIndex(e => e.OrgId).HasDatabaseName("idx_payment_search_org");
            entity.HasIndex(e => new { e.OrgId, e.SiteId }).HasDatabaseName("idx_payment_search_site");
            entity.HasIndex(e => e.OrderId).HasDatabaseName("idx_payment_search_order");
            entity.HasIndex(e => new { e.OrgId, e.Method }).HasDatabaseName("idx_payment_search_method");
            entity.HasIndex(e => new { e.OrgId, e.CreatedAt }).HasDatabaseName("idx_payment_search_created").IsDescending(false, true);

            // Full-text search index
            entity.HasIndex(e => e.SearchVector)
                .HasDatabaseName("idx_payment_search_fts")
                .HasMethod("GIN");
        });
    }
}

/// <summary>
/// EF Core entity for order search table.
/// </summary>
public class OrderSearchEntity
{
    public Guid Id { get; set; }
    public Guid OrgId { get; set; }
    public Guid SiteId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? ServerName { get; set; }
    public string? TableNumber { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int ItemCount { get; set; }
    public int GuestCount { get; set; }
    public NpgsqlTypes.NpgsqlTsVector? SearchVector { get; set; }
}

/// <summary>
/// EF Core entity for customer search table.
/// </summary>
public class CustomerSearchEntity
{
    public Guid Id { get; set; }
    public Guid OrgId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? LoyaltyTier { get; set; }
    public decimal LifetimeSpend { get; set; }
    public int VisitCount { get; set; }
    public DateTime? LastVisitAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Segment { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public NpgsqlTypes.NpgsqlTsVector? SearchVector { get; set; }
}

/// <summary>
/// EF Core entity for payment search table.
/// </summary>
public class PaymentSearchEntity
{
    public Guid Id { get; set; }
    public Guid OrgId { get; set; }
    public Guid SiteId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? CardLastFour { get; set; }
    public string? GatewayReference { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal TipAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public NpgsqlTypes.NpgsqlTsVector? SearchVector { get; set; }
}
