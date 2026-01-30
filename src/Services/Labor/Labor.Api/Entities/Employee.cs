using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Labor.Api.Entities;

/// <summary>
/// Represents an employee in the labor management system.
/// Links to Auth service user for authentication/authorization.
/// </summary>
public class Employee : BaseEntity, ILocationScoped
{
    /// <summary>
    /// The tenant this employee belongs to.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Reference to the Auth service user.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Primary location for this employee (required by ILocationScoped).
    /// </summary>
    public Guid LocationId { get; set; }

    /// <summary>
    /// Human-readable employee number (e.g., "EMP-001").
    /// </summary>
    public string EmployeeNumber { get; set; } = string.Empty;

    /// <summary>
    /// Employee's first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Employee's last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Employee's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Employee's phone number.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Employee's date of birth.
    /// </summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Date the employee was hired.
    /// </summary>
    public DateOnly HireDate { get; set; }

    /// <summary>
    /// Date the employee was terminated (null if still employed).
    /// </summary>
    public DateOnly? TerminationDate { get; set; }

    /// <summary>
    /// Type of employment: fulltime, parttime, casual, contractor.
    /// </summary>
    public string EmploymentType { get; set; } = "fulltime";

    /// <summary>
    /// Current status: active, onleave, terminated.
    /// </summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// Default location ID for this employee.
    /// </summary>
    public Guid DefaultLocationId { get; set; }

    /// <summary>
    /// List of location IDs this employee can work at.
    /// </summary>
    public List<Guid> AllowedLocationIds { get; set; } = new();

    /// <summary>
    /// Default role ID for this employee.
    /// </summary>
    public Guid DefaultRoleId { get; set; }

    /// <summary>
    /// Hourly rate for hourly employees.
    /// </summary>
    public decimal? HourlyRate { get; set; }

    /// <summary>
    /// Salary amount for salaried employees.
    /// </summary>
    public decimal? SalaryAmount { get; set; }

    /// <summary>
    /// Pay frequency: weekly, biweekly, monthly.
    /// </summary>
    public string PayFrequency { get; set; } = "biweekly";

    /// <summary>
    /// Overtime rate multiplier (e.g., 1.5 for time-and-a-half).
    /// </summary>
    public decimal OvertimeRate { get; set; } = 1.5m;

    /// <summary>
    /// Maximum hours per week before overtime kicks in.
    /// </summary>
    public int? MaxHoursPerWeek { get; set; }

    /// <summary>
    /// Minimum guaranteed hours per week.
    /// </summary>
    public int? MinHoursPerWeek { get; set; }

    /// <summary>
    /// Tax identification number for payroll.
    /// </summary>
    public string? TaxId { get; set; }

    /// <summary>
    /// Encrypted bank details for direct deposit.
    /// </summary>
    public string? BankDetailsEncrypted { get; set; }

    /// <summary>
    /// Emergency contact information as JSON.
    /// </summary>
    public string? EmergencyContactJson { get; set; }

    // Navigation properties
    public Role? DefaultRole { get; set; }
    public ICollection<EmployeeRole> EmployeeRoles { get; set; } = new List<EmployeeRole>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
    public ICollection<Availability> Availabilities { get; set; } = new List<Availability>();
    public ICollection<TimeOffRequest> TimeOffRequests { get; set; } = new List<TimeOffRequest>();
    public ICollection<TipDistribution> TipDistributions { get; set; } = new List<TipDistribution>();
    public ICollection<PayrollEntry> PayrollEntries { get; set; } = new List<PayrollEntry>();
}
