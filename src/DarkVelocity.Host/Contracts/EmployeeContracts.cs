using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

public record CreateEmployeeRequest(
    Guid UserId,
    Guid DefaultSiteId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    EmploymentType EmploymentType = EmploymentType.FullTime,
    DateOnly? HireDate = null);

public record UpdateEmployeeRequest(
    string? FirstName = null,
    string? LastName = null,
    string? Email = null,
    decimal? HourlyRate = null,
    decimal? SalaryAmount = null,
    string? PayFrequency = null);

public record ClockInRequest(Guid SiteId, Guid? ShiftId = null);
public record ClockOutRequest(string? Notes = null);
public record AssignRoleRequest(Guid RoleId, string RoleName, string Department, bool IsPrimary = false, decimal? HourlyRateOverride = null);
