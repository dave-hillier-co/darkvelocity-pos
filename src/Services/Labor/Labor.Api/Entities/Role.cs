using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Labor.Api.Entities;

/// <summary>
/// Represents a job position/role (e.g., Server, Bartender, Line Cook, Manager).
/// </summary>
public class Role : BaseEntity
{
    /// <summary>
    /// The tenant this role belongs to.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Name of the role (e.g., "Server", "Bartender", "Line Cook").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Department: foh (front of house), boh (back of house), management.
    /// </summary>
    public string Department { get; set; } = "foh";

    /// <summary>
    /// Default hourly rate for this role.
    /// </summary>
    public decimal? DefaultHourlyRate { get; set; }

    /// <summary>
    /// Color for schedule display (hex color code).
    /// </summary>
    public string Color { get; set; } = "#3B82F6";

    /// <summary>
    /// Sort order for display purposes.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Required certifications for this role (e.g., "Food Handler", "Alcohol Service").
    /// </summary>
    public List<string> RequiredCertifications { get; set; } = new();

    /// <summary>
    /// Whether this role is active and can be assigned.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<EmployeeRole> EmployeeRoles { get; set; } = new List<EmployeeRole>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
    public ICollection<TipPoolRule> TipPoolRules { get; set; } = new List<TipPoolRule>();
    public ICollection<TipDistribution> TipDistributions { get; set; } = new List<TipDistribution>();
}
