using DarkVelocity.GiftCards.Api.Entities;
using DarkVelocity.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.GiftCards.Api.Data;

public class GiftCardsDbContext : BaseDbContext
{
    public GiftCardsDbContext(DbContextOptions<GiftCardsDbContext> options) : base(options)
    {
    }

    public DbSet<GiftCard> GiftCards => Set<GiftCard>();
    public DbSet<GiftCardTransaction> GiftCardTransactions => Set<GiftCardTransaction>();
    public DbSet<GiftCardProgram> GiftCardPrograms => Set<GiftCardProgram>();
    public DbSet<GiftCardDesign> GiftCardDesigns => Set<GiftCardDesign>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureGiftCardProgram(modelBuilder);
        ConfigureGiftCardDesign(modelBuilder);
        ConfigureGiftCard(modelBuilder);
        ConfigureGiftCardTransaction(modelBuilder);
    }

    private static void ConfigureGiftCardProgram(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GiftCardProgram>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            entity.Property(e => e.CardNumberPrefix)
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(e => e.CurrencyCode)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(e => e.MinimumLoadAmount)
                .HasPrecision(12, 4);

            entity.Property(e => e.MaximumLoadAmount)
                .HasPrecision(12, 4);

            entity.Property(e => e.MaximumBalance)
                .HasPrecision(12, 4);

            // Indexes
            entity.HasIndex(e => new { e.TenantId, e.Name })
                .IsUnique();

            entity.HasIndex(e => new { e.TenantId, e.IsActive });

            entity.HasIndex(e => e.CardNumberPrefix);
        });
    }

    private static void ConfigureGiftCardDesign(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GiftCardDesign>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500);

            entity.Property(e => e.ThumbnailUrl)
                .HasMaxLength(500);

            // Indexes
            entity.HasIndex(e => new { e.ProgramId, e.Name })
                .IsUnique();

            entity.HasIndex(e => new { e.ProgramId, e.IsActive, e.SortOrder });

            entity.HasIndex(e => new { e.ProgramId, e.IsDefault });

            // Relationships
            entity.HasOne(e => e.Program)
                .WithMany(p => p.Designs)
                .HasForeignKey(e => e.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureGiftCard(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GiftCard>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CardNumber)
                .HasMaxLength(25)
                .IsRequired();

            entity.Property(e => e.PinHash)
                .HasMaxLength(100);

            entity.Property(e => e.CurrencyCode)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(e => e.CardType)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.InitialBalance)
                .HasPrecision(12, 4);

            entity.Property(e => e.CurrentBalance)
                .HasPrecision(12, 4);

            entity.Property(e => e.RecipientName)
                .HasMaxLength(200);

            entity.Property(e => e.RecipientEmail)
                .HasMaxLength(200);

            entity.Property(e => e.GiftMessage)
                .HasMaxLength(1000);

            entity.Property(e => e.PurchaserName)
                .HasMaxLength(200);

            entity.Property(e => e.PurchaserEmail)
                .HasMaxLength(200);

            entity.Property(e => e.Notes)
                .HasMaxLength(1000);

            entity.Property(e => e.ExternalReference)
                .HasMaxLength(100);

            entity.Property(e => e.SuspensionReason)
                .HasMaxLength(500);

            // Indexes
            entity.HasIndex(e => e.CardNumber)
                .IsUnique();

            entity.HasIndex(e => new { e.TenantId, e.Status });

            entity.HasIndex(e => new { e.LocationId, e.Status });

            entity.HasIndex(e => new { e.TenantId, e.ProgramId, e.Status });

            entity.HasIndex(e => new { e.TenantId, e.ExpiryDate });

            entity.HasIndex(e => new { e.TenantId, e.IssuedAt });

            entity.HasIndex(e => e.RecipientEmail);

            entity.HasIndex(e => e.PurchaserEmail);

            entity.HasIndex(e => e.ExternalReference);

            // Relationships
            entity.HasOne(e => e.Program)
                .WithMany(p => p.GiftCards)
                .HasForeignKey(e => e.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Design)
                .WithMany(d => d.GiftCards)
                .HasForeignKey(e => e.DesignId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureGiftCardTransaction(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GiftCardTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TransactionType)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(e => e.Amount)
                .HasPrecision(12, 4);

            entity.Property(e => e.BalanceBefore)
                .HasPrecision(12, 4);

            entity.Property(e => e.BalanceAfter)
                .HasPrecision(12, 4);

            entity.Property(e => e.Reason)
                .HasMaxLength(500);

            entity.Property(e => e.ExternalReference)
                .HasMaxLength(100);

            entity.Property(e => e.TransactionReference)
                .HasMaxLength(50);

            entity.Property(e => e.Notes)
                .HasMaxLength(1000);

            // Indexes
            entity.HasIndex(e => new { e.GiftCardId, e.ProcessedAt });

            entity.HasIndex(e => new { e.LocationId, e.ProcessedAt });

            entity.HasIndex(e => new { e.LocationId, e.TransactionType, e.ProcessedAt });

            entity.HasIndex(e => e.OrderId);

            entity.HasIndex(e => e.PaymentId);

            entity.HasIndex(e => e.TransactionReference);

            entity.HasIndex(e => e.ExternalReference);

            // Relationships
            entity.HasOne(e => e.GiftCard)
                .WithMany(g => g.Transactions)
                .HasForeignKey(e => e.GiftCardId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
