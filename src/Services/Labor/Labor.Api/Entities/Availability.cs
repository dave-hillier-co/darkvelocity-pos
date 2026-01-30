using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Labor.Api.Entities;

/// <summary>
/// Represents an employee's recurring availability for scheduling.
/// </summary>
public class Availability : BaseEntity
{
    /// <summary>
    /// Reference to the employee.
    /// </summary>
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Day of week (0 = Sunday, 1 = Monday, ... 6 = Saturday).
    /// </summary>
    public int DayOfWeek { get; set; }

    /// <summary>
    /// Available start time (null = all day if IsAvailable = true).
    /// </summary>
    public TimeOnly? StartTime { get; set; }

    /// <summary>
    /// Available end time (null = all day if IsAvailable = true).
    /// </summary>
    public TimeOnly? EndTime { get; set; }

    /// <summary>
    /// Whether the employee is available on this day.
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Whether this is a preferred work day.
    /// </summary>
    public bool IsPreferred { get; set; }

    /// <summary>
    /// Date from which this availability is effective.
    /// </summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>
    /// Date until which this availability is effective (null = indefinite).
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>
    /// Notes about availability (e.g., "Available for closing shifts only").
    /// </summary>
    public string? Notes { get; set; }

    // Navigation properties
    public Employee? Employee { get; set; }
}
