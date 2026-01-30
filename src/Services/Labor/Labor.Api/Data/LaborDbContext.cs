using DarkVelocity.Labor.Api.Entities;
using DarkVelocity.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Labor.Api.Data;

public class LaborDbContext : BaseDbContext
{
    public LaborDbContext(DbContextOptions<LaborDbContext> options) : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<EmployeeRole> EmployeeRoles => Set<EmployeeRole>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<Entities.Break> Breaks => Set<Entities.Break>();
    public DbSet<ShiftSwapRequest> ShiftSwapRequests => Set<ShiftSwapRequest>();
    public DbSet<TipPool> TipPools => Set<TipPool>();
    public DbSet<TipDistribution> TipDistributions => Set<TipDistribution>();
    public DbSet<TipPoolRule> TipPoolRules => Set<TipPoolRule>();
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<PayrollEntry> PayrollEntries => Set<PayrollEntry>();
    public DbSet<Availability> Availabilities => Set<Availability>();
    public DbSet<TimeOffRequest> TimeOffRequests => Set<TimeOffRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureEmployee(modelBuilder);
        ConfigureRole(modelBuilder);
        ConfigureEmployeeRole(modelBuilder);
        ConfigureSchedule(modelBuilder);
        ConfigureShift(modelBuilder);
        ConfigureTimeEntry(modelBuilder);
        ConfigureBreak(modelBuilder);
        ConfigureShiftSwapRequest(modelBuilder);
        ConfigureTipPool(modelBuilder);
        ConfigureTipDistribution(modelBuilder);
        ConfigureTipPoolRule(modelBuilder);
        ConfigurePayrollPeriod(modelBuilder);
        ConfigurePayrollEntry(modelBuilder);
        ConfigureAvailability(modelBuilder);
        ConfigureTimeOffRequest(modelBuilder);
    }

    private static void ConfigureEmployee(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EmployeeNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.EmploymentType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PayFrequency).HasMaxLength(50).IsRequired();
            entity.Property(e => e.TaxId).HasMaxLength(100);
            entity.Property(e => e.BankDetailsEncrypted).HasMaxLength(2000);
            entity.Property(e => e.EmergencyContactJson).HasColumnType("jsonb");

            entity.Property(e => e.HourlyRate).HasPrecision(12, 4);
            entity.Property(e => e.SalaryAmount).HasPrecision(12, 4);
            entity.Property(e => e.OvertimeRate).HasPrecision(5, 2);

            // Store allowed locations as JSONB array
            entity.Property(e => e.AllowedLocationIds).HasColumnType("jsonb");

            entity.HasIndex(e => new { e.TenantId, e.EmployeeNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.UserId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Status });
            entity.HasIndex(e => e.LocationId);

            entity.HasOne(e => e.DefaultRole)
                .WithMany()
                .HasForeignKey(e => e.DefaultRoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRole(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Department).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property(e => e.DefaultHourlyRate).HasPrecision(12, 4);

            // Store certifications as JSONB array
            entity.Property(e => e.RequiredCertifications).HasColumnType("jsonb");

            entity.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.IsActive });
        });
    }

    private static void ConfigureEmployeeRole(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmployeeRole>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.HourlyRateOverride).HasPrecision(12, 4);

            entity.HasIndex(e => new { e.EmployeeId, e.RoleId }).IsUnique();

            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.EmployeeRoles)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Role)
                .WithMany(r => r.EmployeeRoles)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSchedule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.MetadataJson).HasColumnType("jsonb");
            entity.Property(e => e.TotalScheduledHours).HasPrecision(10, 2);
            entity.Property(e => e.TotalLaborCost).HasPrecision(12, 4);

            entity.HasIndex(e => new { e.TenantId, e.LocationId, e.WeekStartDate }).IsUnique();
            entity.HasIndex(e => new { e.LocationId, e.Status });
        });
    }

    private static void ConfigureShift(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.MetadataJson).HasColumnType("jsonb");
            entity.Property(e => e.ScheduledHours).HasPrecision(10, 2);
            entity.Property(e => e.HourlyRate).HasPrecision(12, 4);
            entity.Property(e => e.LaborCost).HasPrecision(12, 4);

            entity.HasIndex(e => new { e.ScheduleId, e.EmployeeId, e.Date });
            entity.HasIndex(e => new { e.EmployeeId, e.Date });
            entity.HasIndex(e => e.Status);

            entity.HasOne(e => e.Schedule)
                .WithMany(s => s.Shifts)
                .HasForeignKey(e => e.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.Shifts)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Role)
                .WithMany(r => r.Shifts)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.SwapRequest)
                .WithMany()
                .HasForeignKey(e => e.SwapRequestId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureTimeEntry(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimeEntry>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ClockInMethod).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ClockOutMethod).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AdjustmentReason).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.Property(e => e.ActualHours).HasPrecision(10, 2);
            entity.Property(e => e.RegularHours).HasPrecision(10, 2);
            entity.Property(e => e.OvertimeHours).HasPrecision(10, 2);
            entity.Property(e => e.HourlyRate).HasPrecision(12, 4);
            entity.Property(e => e.OvertimeRate).HasPrecision(5, 2);
            entity.Property(e => e.GrossPay).HasPrecision(12, 4);

            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.ClockInAt });
            entity.HasIndex(e => new { e.LocationId, e.ClockInAt });
            entity.HasIndex(e => new { e.EmployeeId, e.Status });
            entity.HasIndex(e => e.ShiftId);

            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.TimeEntries)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Shift)
                .WithMany(s => s.TimeEntries)
                .HasForeignKey(e => e.ShiftId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Role)
                .WithMany(r => r.TimeEntries)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureBreak(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Entities.Break>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();

            entity.HasIndex(e => e.TimeEntryId);

            entity.HasOne(e => e.TimeEntry)
                .WithMany(te => te.Breaks)
                .HasForeignKey(e => e.TimeEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureShiftSwapRequest(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShiftSwapRequest>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasIndex(e => new { e.TenantId, e.Status });
            entity.HasIndex(e => new { e.RequestingEmployeeId, e.Status });
            entity.HasIndex(e => new { e.TargetEmployeeId, e.Status });

            entity.HasOne(e => e.RequestingEmployee)
                .WithMany()
                .HasForeignKey(e => e.RequestingEmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.TargetEmployee)
                .WithMany()
                .HasForeignKey(e => e.TargetEmployeeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.RequestingShift)
                .WithMany()
                .HasForeignKey(e => e.RequestingShiftId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.TargetShift)
                .WithMany()
                .HasForeignKey(e => e.TargetShiftId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureTipPool(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TipPool>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.DistributionMethod).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.TotalTips).HasPrecision(12, 4);

            entity.HasIndex(e => new { e.TenantId, e.LocationId, e.Date }).IsUnique();
            entity.HasIndex(e => new { e.LocationId, e.Status });
        });
    }

    private static void ConfigureTipDistribution(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TipDistribution>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.HoursWorked).HasPrecision(10, 2);
            entity.Property(e => e.TipShare).HasPrecision(12, 4);
            entity.Property(e => e.TipPercentage).HasPrecision(8, 4);
            entity.Property(e => e.DeclaredTips).HasPrecision(12, 4);

            entity.HasIndex(e => new { e.TipPoolId, e.EmployeeId }).IsUnique();
            entity.HasIndex(e => new { e.EmployeeId, e.Status });

            entity.HasOne(e => e.TipPool)
                .WithMany(tp => tp.Distributions)
                .HasForeignKey(e => e.TipPoolId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.TipDistributions)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Role)
                .WithMany(r => r.TipDistributions)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTipPoolRule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TipPoolRule>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PoolSharePercentage).HasPrecision(8, 4);
            entity.Property(e => e.DistributionWeight).HasPrecision(8, 4);
            entity.Property(e => e.MinimumHoursToQualify).HasPrecision(10, 2);

            entity.HasIndex(e => new { e.TenantId, e.LocationId, e.RoleId }).IsUnique();
            entity.HasIndex(e => new { e.LocationId, e.IsActive });

            entity.HasOne(e => e.Role)
                .WithMany(r => r.TipPoolRules)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePayrollPeriod(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PayrollPeriod>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ExportFormat).HasMaxLength(50);

            entity.Property(e => e.TotalRegularHours).HasPrecision(12, 2);
            entity.Property(e => e.TotalOvertimeHours).HasPrecision(12, 2);
            entity.Property(e => e.TotalGrossPay).HasPrecision(14, 4);
            entity.Property(e => e.TotalTips).HasPrecision(14, 4);

            entity.HasIndex(e => new { e.TenantId, e.PeriodStart, e.PeriodEnd }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Status });
        });
    }

    private static void ConfigurePayrollEntry(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PayrollEntry>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AdjustmentNotes).HasMaxLength(500);

            entity.Property(e => e.RegularHours).HasPrecision(10, 2);
            entity.Property(e => e.OvertimeHours).HasPrecision(10, 2);
            entity.Property(e => e.RegularPay).HasPrecision(12, 4);
            entity.Property(e => e.OvertimePay).HasPrecision(12, 4);
            entity.Property(e => e.TipIncome).HasPrecision(12, 4);
            entity.Property(e => e.GrossPay).HasPrecision(12, 4);
            entity.Property(e => e.Adjustments).HasPrecision(12, 4);

            entity.HasIndex(e => new { e.PayrollPeriodId, e.EmployeeId }).IsUnique();
            entity.HasIndex(e => new { e.EmployeeId, e.Status });

            entity.HasOne(e => e.PayrollPeriod)
                .WithMany(pp => pp.Entries)
                .HasForeignKey(e => e.PayrollPeriodId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.PayrollEntries)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureAvailability(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Availability>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasIndex(e => new { e.EmployeeId, e.DayOfWeek, e.EffectiveFrom });

            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.Availabilities)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureTimeOffRequest(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimeOffRequest>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.TotalDays).HasPrecision(5, 2);

            entity.HasIndex(e => new { e.EmployeeId, e.Status });
            entity.HasIndex(e => new { e.EmployeeId, e.StartDate, e.EndDate });

            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.TimeOffRequests)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
