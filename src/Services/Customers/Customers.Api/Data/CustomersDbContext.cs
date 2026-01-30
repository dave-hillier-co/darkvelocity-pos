using DarkVelocity.Customers.Api.Entities;
using DarkVelocity.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Customers.Api.Data;

public class CustomersDbContext : BaseDbContext
{
    public CustomersDbContext(DbContextOptions<CustomersDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<LoyaltyProgram> LoyaltyPrograms => Set<LoyaltyProgram>();
    public DbSet<LoyaltyTier> LoyaltyTiers => Set<LoyaltyTier>();
    public DbSet<CustomerLoyalty> CustomerLoyalties => Set<CustomerLoyalty>();
    public DbSet<PointsTransaction> PointsTransactions => Set<PointsTransaction>();
    public DbSet<Reward> Rewards => Set<Reward>();
    public DbSet<CustomerReward> CustomerRewards => Set<CustomerReward>();
    public DbSet<Referral> Referrals => Set<Referral>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Customer configuration
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExternalId).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Gender).HasMaxLength(20);
            entity.Property(e => e.PreferredLanguage).HasMaxLength(10);
            entity.Property(e => e.Source).HasMaxLength(30);
            entity.Property(e => e.TotalSpend).HasPrecision(12, 4);
            entity.Property(e => e.AverageOrderValue).HasPrecision(12, 4);

            entity.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Phone });
            entity.HasIndex(e => new { e.TenantId, e.ExternalId });

            // Soft delete filter
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // CustomerAddress configuration
        modelBuilder.Entity<CustomerAddress>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).HasMaxLength(30);
            entity.Property(e => e.Street).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Street2).HasMaxLength(255);
            entity.Property(e => e.City).HasMaxLength(100).IsRequired();
            entity.Property(e => e.State).HasMaxLength(100);
            entity.Property(e => e.PostalCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Country).HasMaxLength(100).IsRequired();
            entity.Property(e => e.DeliveryInstructions).HasMaxLength(500);

            entity.HasOne(e => e.Customer)
                .WithMany(c => c.Addresses)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // LoyaltyProgram configuration
        modelBuilder.Entity<LoyaltyProgram>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.PointsPerCurrencyUnit).HasPrecision(10, 4);
            entity.Property(e => e.PointsValueInCurrency).HasPrecision(10, 6);

            entity.HasIndex(e => e.TenantId);
        });

        // LoyaltyTier configuration
        modelBuilder.Entity<LoyaltyTier>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PointsMultiplier).HasPrecision(5, 2);
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property(e => e.IconUrl).HasMaxLength(500);
            entity.Property(e => e.BirthdayReward).HasMaxLength(200);

            entity.HasOne(e => e.Program)
                .WithMany(p => p.Tiers)
                .HasForeignKey(e => e.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ProgramId, e.SortOrder });
        });

        // CustomerLoyalty configuration
        modelBuilder.Entity<CustomerLoyalty>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Customer)
                .WithMany(c => c.LoyaltyMemberships)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Program)
                .WithMany(p => p.Members)
                .HasForeignKey(e => e.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CurrentTier)
                .WithMany(t => t.Members)
                .HasForeignKey(e => e.CurrentTierId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.CustomerId, e.ProgramId }).IsUnique();
        });

        // PointsTransaction configuration
        modelBuilder.Entity<PointsTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TransactionType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);

            entity.HasOne(e => e.CustomerLoyalty)
                .WithMany(l => l.Transactions)
                .HasForeignKey(e => e.CustomerLoyaltyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.CustomerLoyaltyId);
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.ProcessedAt);
        });

        // Reward configuration
        modelBuilder.Entity<Reward>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Type).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Value).HasPrecision(12, 4);
            entity.Property(e => e.DiscountPercentage).HasPrecision(5, 2);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);

            entity.HasOne(e => e.Program)
                .WithMany(p => p.Rewards)
                .HasForeignKey(e => e.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.TenantId, e.IsActive });
            entity.HasIndex(e => e.ProgramId);
        });

        // CustomerReward configuration
        modelBuilder.Entity<CustomerReward>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();

            entity.HasOne(e => e.Customer)
                .WithMany(c => c.Rewards)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Reward)
                .WithMany(r => r.CustomerRewards)
                .HasForeignKey(e => e.RewardId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => new { e.CustomerId, e.Status });
        });

        // Referral configuration
        modelBuilder.Entity<Referral>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReferralCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();

            entity.HasOne(e => e.ReferrerCustomer)
                .WithMany()
                .HasForeignKey(e => e.ReferrerCustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ReferredCustomer)
                .WithMany()
                .HasForeignKey(e => e.ReferredCustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.ReferralCode).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Status });
        });
    }
}
