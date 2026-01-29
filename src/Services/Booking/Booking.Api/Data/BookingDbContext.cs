using DarkVelocity.Booking.Api.Entities;
using DarkVelocity.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Booking.Api.Data;

public class BookingDbContext : BaseDbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }

    public DbSet<FloorPlan> FloorPlans => Set<FloorPlan>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<TableCombination> TableCombinations => Set<TableCombination>();
    public DbSet<TableCombinationTable> TableCombinationTables => Set<TableCombinationTable>();
    public DbSet<Entities.Booking> Bookings => Set<Entities.Booking>();
    public DbSet<BookingDeposit> BookingDeposits => Set<BookingDeposit>();
    public DbSet<DepositPolicy> DepositPolicies => Set<DepositPolicy>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<DateOverride> DateOverrides => Set<DateOverride>();
    public DbSet<BookingSettings> BookingSettings => Set<BookingSettings>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureFloorPlan(modelBuilder);
        ConfigureTable(modelBuilder);
        ConfigureTableCombination(modelBuilder);
        ConfigureBooking(modelBuilder);
        ConfigureBookingDeposit(modelBuilder);
        ConfigureDepositPolicy(modelBuilder);
        ConfigureTimeSlot(modelBuilder);
        ConfigureDateOverride(modelBuilder);
        ConfigureBookingSettings(modelBuilder);
        ConfigureWaitlistEntry(modelBuilder);
    }

    private static void ConfigureFloorPlan(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FloorPlan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.BackgroundImageUrl).HasMaxLength(500);

            entity.HasIndex(e => new { e.LocationId, e.Name }).IsUnique();
            entity.HasIndex(e => new { e.LocationId, e.IsActive });
        });
    }

    private static void ConfigureTable(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Table>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TableNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Shape).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasIndex(e => new { e.LocationId, e.TableNumber }).IsUnique();
            entity.HasIndex(e => new { e.LocationId, e.Status });
            entity.HasIndex(e => new { e.FloorPlanId, e.IsActive });

            entity.HasOne(e => e.FloorPlan)
                .WithMany(f => f.Tables)
                .HasForeignKey(e => e.FloorPlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureTableCombination(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TableCombination>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasIndex(e => new { e.LocationId, e.Name }).IsUnique();
            entity.HasIndex(e => new { e.LocationId, e.IsActive });

            entity.HasOne(e => e.FloorPlan)
                .WithMany()
                .HasForeignKey(e => e.FloorPlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TableCombinationTable>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.TableCombinationId, e.TableId }).IsUnique();

            entity.HasOne(e => e.TableCombination)
                .WithMany(c => c.Tables)
                .HasForeignKey(e => e.TableCombinationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Table)
                .WithMany(t => t.TableCombinations)
                .HasForeignKey(e => e.TableId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureBooking(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Entities.Booking>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BookingReference).HasMaxLength(50).IsRequired();
            entity.Property(e => e.GuestName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.GuestEmail).HasMaxLength(200);
            entity.Property(e => e.GuestPhone).HasMaxLength(50);
            entity.Property(e => e.SpecialRequests).HasMaxLength(1000);
            entity.Property(e => e.InternalNotes).HasMaxLength(1000);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Source).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ExternalReference).HasMaxLength(100);
            entity.Property(e => e.ConfirmationMethod).HasMaxLength(20);
            entity.Property(e => e.CancellationReason).HasMaxLength(500);
            entity.Property(e => e.Tags).HasMaxLength(500);
            entity.Property(e => e.Occasion).HasMaxLength(100);

            entity.HasIndex(e => new { e.LocationId, e.BookingReference }).IsUnique();
            entity.HasIndex(e => new { e.LocationId, e.BookingDate, e.Status });
            entity.HasIndex(e => new { e.LocationId, e.Status });
            entity.HasIndex(e => new { e.TableId, e.BookingDate, e.StartTime });
            entity.HasIndex(e => new { e.TableCombinationId, e.BookingDate, e.StartTime });
            entity.HasIndex(e => e.GuestEmail);
            entity.HasIndex(e => e.GuestPhone);

            entity.HasOne(e => e.Table)
                .WithMany(t => t.Bookings)
                .HasForeignKey(e => e.TableId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.TableCombination)
                .WithMany(c => c.Bookings)
                .HasForeignKey(e => e.TableCombinationId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureBookingDeposit(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BookingDeposit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(12, 4);
            entity.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.PaymentMethod).HasMaxLength(30);
            entity.Property(e => e.StripePaymentIntentId).HasMaxLength(100);
            entity.Property(e => e.CardBrand).HasMaxLength(20);
            entity.Property(e => e.CardLastFour).HasMaxLength(4);
            entity.Property(e => e.PaymentReference).HasMaxLength(100);
            entity.Property(e => e.RefundAmount).HasPrecision(12, 4);
            entity.Property(e => e.RefundReason).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasIndex(e => new { e.LocationId, e.Status });
            entity.HasIndex(e => e.BookingId);
            entity.HasIndex(e => e.StripePaymentIntentId);

            entity.HasOne(e => e.Booking)
                .WithMany(b => b.Deposits)
                .HasForeignKey(e => e.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureDepositPolicy(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DepositPolicy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.DepositType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.AmountPerPerson).HasPrecision(12, 4);
            entity.Property(e => e.FlatAmount).HasPrecision(12, 4);
            entity.Property(e => e.PercentageRate).HasPrecision(5, 4);
            entity.Property(e => e.MinimumAmount).HasPrecision(12, 4);
            entity.Property(e => e.MaximumAmount).HasPrecision(12, 4);
            entity.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.RefundPercentage).HasPrecision(5, 4);
            entity.Property(e => e.ApplicableDays).HasMaxLength(100);

            entity.HasIndex(e => new { e.LocationId, e.Name }).IsUnique();
            entity.HasIndex(e => new { e.LocationId, e.IsActive, e.Priority });
        });
    }

    private static void ConfigureTimeSlot(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimeSlot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(50);

            entity.HasIndex(e => new { e.LocationId, e.DayOfWeek, e.IsActive });
            entity.HasIndex(e => new { e.LocationId, e.FloorPlanId, e.DayOfWeek });
        });
    }

    private static void ConfigureDateOverride(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DateOverride>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OverrideType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasIndex(e => new { e.LocationId, e.Date }).IsUnique();
        });
    }

    private static void ConfigureBookingSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BookingSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Timezone).HasMaxLength(50).IsRequired();
            entity.Property(e => e.TermsAndConditions).HasMaxLength(5000);
            entity.Property(e => e.ConfirmationMessage).HasMaxLength(1000);
            entity.Property(e => e.CancellationPolicyText).HasMaxLength(2000);

            entity.HasIndex(e => e.LocationId).IsUnique();
        });
    }

    private static void ConfigureWaitlistEntry(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WaitlistEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GuestName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.GuestPhone).HasMaxLength(50);
            entity.Property(e => e.GuestEmail).HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Source).HasMaxLength(20).IsRequired();
            entity.Property(e => e.SeatingPreference).HasMaxLength(20);

            entity.HasIndex(e => new { e.LocationId, e.RequestedDate, e.Status });
            entity.HasIndex(e => new { e.LocationId, e.Status, e.QueuePosition });
            entity.HasIndex(e => e.GuestPhone);
        });
    }
}
