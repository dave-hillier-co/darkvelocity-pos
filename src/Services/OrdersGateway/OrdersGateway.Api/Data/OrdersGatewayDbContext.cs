using DarkVelocity.OrdersGateway.Api.Entities;
using DarkVelocity.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.OrdersGateway.Api.Data;

public class OrdersGatewayDbContext : BaseDbContext
{
    public OrdersGatewayDbContext(DbContextOptions<OrdersGatewayDbContext> options) : base(options)
    {
    }

    public DbSet<DeliveryPlatform> DeliveryPlatforms => Set<DeliveryPlatform>();
    public DbSet<PlatformLocation> PlatformLocations => Set<PlatformLocation>();
    public DbSet<ExternalOrder> ExternalOrders => Set<ExternalOrder>();
    public DbSet<MenuSync> MenuSyncs => Set<MenuSync>();
    public DbSet<MenuItemMapping> MenuItemMappings => Set<MenuItemMapping>();
    public DbSet<PlatformPayout> PlatformPayouts => Set<PlatformPayout>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // DeliveryPlatform configuration
        modelBuilder.Entity<DeliveryPlatform>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlatformType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.MerchantId).HasMaxLength(100);
            entity.Property(e => e.Settings).HasColumnType("jsonb");

            entity.HasIndex(e => new { e.TenantId, e.PlatformType });
            entity.HasIndex(e => e.Status);
        });

        // PlatformLocation configuration
        modelBuilder.Entity<PlatformLocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlatformStoreId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.OperatingHoursOverride).HasColumnType("jsonb");

            entity.HasIndex(e => new { e.DeliveryPlatformId, e.LocationId }).IsUnique();
            entity.HasIndex(e => new { e.DeliveryPlatformId, e.PlatformStoreId }).IsUnique();

            entity.HasOne(e => e.DeliveryPlatform)
                .WithMany(p => p.PlatformLocations)
                .HasForeignKey(e => e.DeliveryPlatformId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ExternalOrder configuration
        modelBuilder.Entity<ExternalOrder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlatformOrderId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PlatformOrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.OrderType).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Customer).HasColumnType("jsonb");
            entity.Property(e => e.Items).HasColumnType("jsonb");
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Metadata).HasColumnType("jsonb");

            // Monetary precision
            entity.Property(e => e.Subtotal).HasPrecision(12, 4);
            entity.Property(e => e.DeliveryFee).HasPrecision(12, 4);
            entity.Property(e => e.ServiceFee).HasPrecision(12, 4);
            entity.Property(e => e.Tax).HasPrecision(12, 4);
            entity.Property(e => e.Tip).HasPrecision(12, 4);
            entity.Property(e => e.Total).HasPrecision(12, 4);

            // Indexes for common queries
            entity.HasIndex(e => new { e.DeliveryPlatformId, e.PlatformOrderId }).IsUnique();
            entity.HasIndex(e => new { e.LocationId, e.Status });
            entity.HasIndex(e => new { e.LocationId, e.PlacedAt });
            entity.HasIndex(e => e.InternalOrderId);

            entity.HasOne(e => e.DeliveryPlatform)
                .WithMany(p => p.ExternalOrders)
                .HasForeignKey(e => e.DeliveryPlatformId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // MenuSync configuration
        modelBuilder.Entity<MenuSync>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.TriggeredBy).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ErrorLog).HasColumnType("jsonb");

            entity.HasIndex(e => new { e.DeliveryPlatformId, e.LocationId, e.StartedAt });

            entity.HasOne(e => e.DeliveryPlatform)
                .WithMany(p => p.MenuSyncs)
                .HasForeignKey(e => e.DeliveryPlatformId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // MenuItemMapping configuration
        modelBuilder.Entity<MenuItemMapping>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlatformItemId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PlatformCategoryId).HasMaxLength(100);
            entity.Property(e => e.PriceOverride).HasPrecision(12, 4);
            entity.Property(e => e.ModifierMappings).HasColumnType("jsonb");

            entity.HasIndex(e => new { e.DeliveryPlatformId, e.InternalMenuItemId }).IsUnique();
            entity.HasIndex(e => new { e.DeliveryPlatformId, e.PlatformItemId }).IsUnique();

            entity.HasOne(e => e.DeliveryPlatform)
                .WithMany(p => p.MenuItemMappings)
                .HasForeignKey(e => e.DeliveryPlatformId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PlatformPayout configuration
        modelBuilder.Entity<PlatformPayout>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PayoutReference).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.OrderIds).HasColumnType("jsonb");

            // Monetary precision
            entity.Property(e => e.GrossAmount).HasPrecision(12, 4);
            entity.Property(e => e.Commissions).HasPrecision(12, 4);
            entity.Property(e => e.Fees).HasPrecision(12, 4);
            entity.Property(e => e.Adjustments).HasPrecision(12, 4);
            entity.Property(e => e.NetAmount).HasPrecision(12, 4);

            entity.HasIndex(e => new { e.DeliveryPlatformId, e.PayoutReference }).IsUnique();
            entity.HasIndex(e => new { e.LocationId, e.PeriodStart, e.PeriodEnd });
            entity.HasIndex(e => e.Status);

            entity.HasOne(e => e.DeliveryPlatform)
                .WithMany(p => p.Payouts)
                .HasForeignKey(e => e.DeliveryPlatformId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
