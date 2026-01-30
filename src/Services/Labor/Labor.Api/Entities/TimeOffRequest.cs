using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Labor.Api.Entities;

/// <summary>
/// Represents a request for time off (vacation, sick, personal, etc.).
/// </summary>
public class TimeOffRequest : BaseEntity
{
    /// <summary>
    /// Reference to the employee requesting time off.
    /// </summary>
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Type of time off: vacation, sick, personal, bereavement, other.
    /// </summary>
    public string Type { get; set; } = "vacation";

    /// <summary>
    /// First day of time off.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Last day of time off.
    /// </summary>
    public DateOnly EndDate { get; set; }

    /// <summary>
    /// Total days requested (can be decimal for half days).
    /// </summary>
    public decimal TotalDays { get; set; }

    /// <summary>
    /// Whether this time off is paid.
    /// </summary>
    public bool IsPaid { get; set; }

    /// <summary>
    /// Status: pending, approved, rejected, cancelled.
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// When the request was submitted.
    /// </summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who reviewed the request.
    /// </summary>
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>
    /// When the request was reviewed.
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Reason for the time off request.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Notes (e.g., manager comments on approval/rejection).
    /// </summary>
    public string? Notes { get; set; }

    // Navigation properties
    public Employee? Employee { get; set; }
}
