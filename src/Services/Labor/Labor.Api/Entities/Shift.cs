using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Labor.Api.Entities;

/// <summary>
/// Represents a scheduled shift for an employee.
/// </summary>
public class Shift : BaseEntity
{
    /// <summary>
    /// Reference to the schedule this shift belongs to.
    /// </summary>
    public Guid ScheduleId { get; set; }

    /// <summary>
    /// Reference to the employee assigned to this shift.
    /// </summary>
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Reference to the role the employee will work.
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Date of the shift.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Shift start time.
    /// </summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>
    /// Shift end time.
    /// </summary>
    public TimeOnly EndTime { get; set; }

    /// <summary>
    /// Scheduled break duration in minutes.
    /// </summary>
    public int BreakMinutes { get; set; }

    /// <summary>
    /// Calculated scheduled hours (EndTime - StartTime - BreakMinutes).
    /// </summary>
    public decimal ScheduledHours { get; set; }

    /// <summary>
    /// Hourly rate snapshot at time of scheduling.
    /// </summary>
    public decimal HourlyRate { get; set; }

    /// <summary>
    /// Calculated labor cost for this shift.
    /// </summary>
    public decimal LaborCost { get; set; }

    /// <summary>
    /// Status: scheduled, confirmed, started, completed, noshow, cancelled.
    /// </summary>
    public string Status { get; set; } = "scheduled";

    /// <summary>
    /// Notes about this shift.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Whether this shift counts as overtime.
    /// </summary>
    public bool IsOvertime { get; set; }

    /// <summary>
    /// Reference to swap request if this shift resulted from a swap.
    /// </summary>
    public Guid? SwapRequestId { get; set; }

    /// <summary>
    /// Additional metadata as JSON.
    /// </summary>
    public string? MetadataJson { get; set; }

    // Navigation properties
    public Schedule? Schedule { get; set; }
    public Employee? Employee { get; set; }
    public Role? Role { get; set; }
    public ShiftSwapRequest? SwapRequest { get; set; }
    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
}
