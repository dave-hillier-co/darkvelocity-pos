using DarkVelocity.Fiscalisation.Api.Entities;
using DarkVelocity.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Fiscalisation.Api.Data;

public class FiscalisationDbContext : BaseDbContext
{
    public FiscalisationDbContext(DbContextOptions<FiscalisationDbContext> options) : base(options)
    {
    }

    public DbSet<FiscalDevice> FiscalDevices => Set<FiscalDevice>();
    public DbSet<FiscalTransaction> FiscalTransactions => Set<FiscalTransaction>();
    public DbSet<FiscalExport> FiscalExports => Set<FiscalExport>();
    public DbSet<FiscalJournal> FiscalJournals => Set<FiscalJournal>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FiscalDevice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DeviceType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SerialNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PublicKey).HasMaxLength(2000);
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.ApiEndpoint).HasMaxLength(500);
            entity.Property(e => e.ApiCredentialsEncrypted).HasMaxLength(2000);
            entity.Property(e => e.ClientId).HasMaxLength(100);

            entity.HasIndex(e => e.LocationId);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.LocationId, e.Status });
            entity.HasIndex(e => e.SerialNumber).IsUnique();
        });

        modelBuilder.Entity<FiscalTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TransactionType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.ProcessType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.SourceType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.GrossAmount).HasPrecision(12, 4);
            entity.Property(e => e.NetAmounts).HasMaxLength(2000);
            entity.Property(e => e.TaxAmounts).HasMaxLength(2000);
            entity.Property(e => e.PaymentTypes).HasMaxLength(2000);
            entity.Property(e => e.Signature).HasMaxLength(1000);
            entity.Property(e => e.CertificateSerial).HasMaxLength(100);
            entity.Property(e => e.QrCodeData).HasMaxLength(2000);
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);

            entity.HasIndex(e => e.LocationId);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.FiscalDeviceId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.SourceType, e.SourceId });
            entity.HasIndex(e => new { e.FiscalDeviceId, e.TransactionNumber }).IsUnique();

            entity.HasOne(e => e.FiscalDevice)
                .WithMany(d => d.Transactions)
                .HasForeignKey(e => e.FiscalDeviceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FiscalExport>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExportType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.FileUrl).HasMaxLength(1000);
            entity.Property(e => e.FileSha256).HasMaxLength(64);
            entity.Property(e => e.AuditReference).HasMaxLength(100);

            entity.HasIndex(e => e.LocationId);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.LocationId, e.StartDate, e.EndDate });
        });

        modelBuilder.Entity<FiscalJournal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.Severity).HasMaxLength(20).IsRequired();

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.LocationId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => new { e.TenantId, e.Timestamp });
        });

        modelBuilder.Entity<TaxRate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CountryCode).HasMaxLength(2).IsRequired();
            entity.Property(e => e.Rate).HasPrecision(5, 4);
            entity.Property(e => e.FiscalCode).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(200);

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.CountryCode });
            entity.HasIndex(e => new { e.TenantId, e.FiscalCode }).IsUnique();
        });
    }
}
