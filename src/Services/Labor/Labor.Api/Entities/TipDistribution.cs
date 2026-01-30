using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Labor.Api.Entities;

/// <summary>
/// Represents the tip share allocated to an individual employee from a tip pool.
/// </summary>
public class TipDistribution : BaseEntity
{
    /// <summary>
    /// Reference to the tip pool.
    /// </summary>
    public Guid TipPoolId { get; set; }

    /// <summary>
    /// Reference to the employee receiving tips.
    /// </summary>
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// The role worked during this tip period.
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Hours worked during the tip pool period.
    /// </summary>
    public decimal HoursWorked { get; set; }

    /// <summary>
    /// Points earned (for point-based distribution systems).
    /// </summary>
    public int? PointsEarned { get; set; }

    /// <summary>
    /// Calculated tip share amount.
    /// </summary>
    public decimal TipShare { get; set; }

    /// <summary>
    /// Percentage of total pool received.
    /// </summary>
    public decimal TipPercentage { get; set; }

    /// <summary>
    /// Tips declared by employee (if applicable).
    /// </summary>
    public decimal? DeclaredTips { get; set; }

    /// <summary>
    /// Status: calculated, approved, disputed, paid.
    /// </summary>
    public string Status { get; set; } = "calculated";

    /// <summary>
    /// When the tips were paid out.
    /// </summary>
    public DateTime? PaidAt { get; set; }

    // Navigation properties
    public TipPool? TipPool { get; set; }
    public Employee? Employee { get; set; }
    public Role? Role { get; set; }
}
