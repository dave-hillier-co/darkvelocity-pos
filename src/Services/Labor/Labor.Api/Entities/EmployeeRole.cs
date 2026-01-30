using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Labor.Api.Entities;

/// <summary>
/// Many-to-many relationship between Employee and Role with custom rates.
/// </summary>
public class EmployeeRole : BaseEntity
{
    /// <summary>
    /// Reference to the employee.
    /// </summary>
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Reference to the role.
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Override hourly rate for this employee in this role (null = use role default).
    /// </summary>
    public decimal? HourlyRateOverride { get; set; }

    /// <summary>
    /// Whether this is the employee's primary role.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Date when the employee was certified for this role.
    /// </summary>
    public DateTime? CertifiedAt { get; set; }

    // Navigation properties
    public Employee? Employee { get; set; }
    public Role? Role { get; set; }
}
