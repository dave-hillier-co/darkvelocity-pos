using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Labor.Api.Dtos;

/// <summary>
/// Full employee details response.
/// </summary>
public class EmployeeDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid LocationId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public DateOnly HireDate { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public string EmploymentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid DefaultLocationId { get; set; }
    public List<Guid> AllowedLocationIds { get; set; } = new();
    public Guid DefaultRoleId { get; set; }
    public string? DefaultRoleName { get; set; }
    public decimal? HourlyRate { get; set; }
    public decimal? SalaryAmount { get; set; }
    public string PayFrequency { get; set; } = string.Empty;
    public decimal OvertimeRate { get; set; }
    public int? MaxHoursPerWeek { get; set; }
    public int? MinHoursPerWeek { get; set; }
    public List<EmployeeRoleDto> Roles { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Summary employee for list views.
/// </summary>
public class EmployeeSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string Email { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? DefaultRoleName { get; set; }
    public Guid DefaultLocationId { get; set; }
}

/// <summary>
/// Employee role assignment.
/// </summary>
public class EmployeeRoleDto : HalResource
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal? HourlyRateOverride { get; set; }
    public decimal? EffectiveHourlyRate { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime? CertifiedAt { get; set; }
}

/// <summary>
/// Request to create a new employee.
/// </summary>
public record CreateEmployeeRequest(
    Guid UserId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    DateOnly HireDate,
    Guid DefaultRoleId,
    string? Phone = null,
    DateOnly? DateOfBirth = null,
    string EmploymentType = "fulltime",
    Guid? DefaultLocationId = null,
    List<Guid>? AllowedLocationIds = null,
    decimal? HourlyRate = null,
    decimal? SalaryAmount = null,
    string PayFrequency = "biweekly",
    decimal OvertimeRate = 1.5m,
    int? MaxHoursPerWeek = null,
    int? MinHoursPerWeek = null);

/// <summary>
/// Request to update an employee.
/// </summary>
public record UpdateEmployeeRequest(
    string? FirstName = null,
    string? LastName = null,
    string? Email = null,
    string? Phone = null,
    DateOnly? DateOfBirth = null,
    string? EmploymentType = null,
    string? Status = null,
    Guid? DefaultLocationId = null,
    List<Guid>? AllowedLocationIds = null,
    Guid? DefaultRoleId = null,
    decimal? HourlyRate = null,
    decimal? SalaryAmount = null,
    string? PayFrequency = null,
    decimal? OvertimeRate = null,
    int? MaxHoursPerWeek = null,
    int? MinHoursPerWeek = null);

/// <summary>
/// Request to terminate an employee.
/// </summary>
public record TerminateEmployeeRequest(
    DateOnly TerminationDate,
    string? Reason = null);

/// <summary>
/// Request to assign a role to an employee.
/// </summary>
public record AssignRoleRequest(
    Guid RoleId,
    decimal? HourlyRateOverride = null,
    bool IsPrimary = false);
