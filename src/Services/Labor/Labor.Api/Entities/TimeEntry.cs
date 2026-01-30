using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Labor.Api.Entities;

/// <summary>
/// Represents a clock in/out record for time tracking.
/// </summary>
public class TimeEntry : BaseEntity, ILocationScoped
{
    /// <summary>
    /// The tenant this entry belongs to.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Reference to the employee.
    /// </summary>
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// The location where the employee clocked in.
    /// </summary>
    public Guid LocationId { get; set; }

    /// <summary>
    /// Reference to the scheduled shift (if any).
    /// </summary>
    public Guid? ShiftId { get; set; }

    /// <summary>
    /// The role worked during this time entry.
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Time when the employee clocked in.
    /// </summary>
    public DateTime ClockInAt { get; set; }

    /// <summary>
    /// Time when the employee clocked out (null if still clocked in).
    /// </summary>
    public DateTime? ClockOutAt { get; set; }

    /// <summary>
    /// Method used to clock in: pin, qr, biometric, manager.
    /// </summary>
    public string ClockInMethod { get; set; } = "pin";

    /// <summary>
    /// Method used to clock out: pin, qr, biometric, manager, auto.
    /// </summary>
    public string? ClockOutMethod { get; set; }

    /// <summary>
    /// Total break minutes (sum of all breaks).
    /// </summary>
    public int BreakMinutes { get; set; }

    /// <summary>
    /// Actual hours worked (calculated).
    /// </summary>
    public decimal ActualHours { get; set; }

    /// <summary>
    /// Regular (non-overtime) hours.
    /// </summary>
    public decimal RegularHours { get; set; }

    /// <summary>
    /// Overtime hours.
    /// </summary>
    public decimal OvertimeHours { get; set; }

    /// <summary>
    /// Hourly rate applied.
    /// </summary>
    public decimal HourlyRate { get; set; }

    /// <summary>
    /// Overtime rate multiplier.
    /// </summary>
    public decimal OvertimeRate { get; set; }

    /// <summary>
    /// Calculated gross pay for this entry.
    /// </summary>
    public decimal GrossPay { get; set; }

    /// <summary>
    /// Status: active, completed, adjusted, disputed.
    /// </summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// User who adjusted this entry (if adjusted).
    /// </summary>
    public Guid? AdjustedByUserId { get; set; }

    /// <summary>
    /// Reason for adjustment.
    /// </summary>
    public string? AdjustmentReason { get; set; }

    /// <summary>
    /// User who approved this entry.
    /// </summary>
    public Guid? ApprovedByUserId { get; set; }

    /// <summary>
    /// When this entry was approved.
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Notes about this time entry.
    /// </summary>
    public string? Notes { get; set; }

    // Navigation properties
    public Employee? Employee { get; set; }
    public Shift? Shift { get; set; }
    public Role? Role { get; set; }
    public ICollection<Break> Breaks { get; set; } = new List<Break>();
}
