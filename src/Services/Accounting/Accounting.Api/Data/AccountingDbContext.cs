using DarkVelocity.Accounting.Api.Entities;
using DarkVelocity.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Accounting.Api.Data;

public class AccountingDbContext : BaseDbContext
{
    public AccountingDbContext(DbContextOptions<AccountingDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<Reconciliation> Reconciliations => Set<Reconciliation>();
    public DbSet<TaxLiability> TaxLiabilities => Set<TaxLiability>();
    public DbSet<GiftCardLiability> GiftCardLiabilities => Set<GiftCardLiability>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Account configuration
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.AccountCode }).IsUnique();
            entity.HasOne(e => e.ParentAccount)
                  .WithMany(e => e.ChildAccounts)
                  .HasForeignKey(e => e.ParentAccountId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // AccountingPeriod configuration
        modelBuilder.Entity<AccountingPeriod>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.LocationId, e.PeriodType, e.StartDate }).IsUnique();
        });

        // JournalEntry configuration
        modelBuilder.Entity<JournalEntry>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.LocationId, e.EntryNumber }).IsUnique();
            entity.HasIndex(e => new { e.SourceType, e.SourceId });
            entity.HasOne(e => e.ReversedByEntry)
                  .WithOne()
                  .HasForeignKey<JournalEntry>(e => e.ReversedByEntryId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReversesEntry)
                  .WithOne()
                  .HasForeignKey<JournalEntry>(e => e.ReversesEntryId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountingPeriod)
                  .WithMany(p => p.JournalEntries)
                  .HasForeignKey(e => e.AccountingPeriodId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // JournalEntryLine configuration
        modelBuilder.Entity<JournalEntryLine>(entity =>
        {
            entity.HasOne(e => e.JournalEntry)
                  .WithMany(j => j.Lines)
                  .HasForeignKey(e => e.JournalEntryId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CostCenter)
                  .WithMany()
                  .HasForeignKey(e => e.CostCenterId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // CostCenter configuration
        modelBuilder.Entity<CostCenter>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
        });

        // Reconciliation configuration
        modelBuilder.Entity<Reconciliation>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.LocationId, e.ReconciliationType, e.Date });
        });

        // TaxLiability configuration
        modelBuilder.Entity<TaxLiability>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Period, e.TaxCode }).IsUnique();
        });

        // GiftCardLiability configuration
        modelBuilder.Entity<GiftCardLiability>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.AsOfDate }).IsUnique();
        });
    }
}
