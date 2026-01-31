using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Grains;

public record CreateEmployeeCommand(
    Guid OrganizationId,
    Guid UserId,
    Guid DefaultSiteId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    EmploymentType EmploymentType = EmploymentType.FullTime,
    DateOnly? HireDate = null);

public record UpdateEmployeeCommand(
    string? FirstName = null,
    string? LastName = null,
    string? Email = null,
    decimal? HourlyRate = null,
    decimal? SalaryAmount = null,
    string? PayFrequency = null);

public record AssignRoleCommand(
    Guid RoleId,
    string RoleName,
    string Department,
    bool IsPrimary = false,
    decimal? HourlyRateOverride = null);

public record ClockInCommand(
    Guid SiteId,
    Guid? ShiftId = null);

public record ClockOutCommand(
    string? Notes = null);

public record EmployeeCreatedResult(Guid Id, string EmployeeNumber, DateTime CreatedAt);
public record EmployeeUpdatedResult(int Version, DateTime UpdatedAt);
public record ClockInResult(Guid TimeEntryId, DateTime ClockInTime);
public record ClockOutResult(Guid TimeEntryId, DateTime ClockOutTime, decimal TotalHours);

public interface IEmployeeGrain : IGrainWithStringKey
{
    /// <summary>
    /// Creates a new employee record.
    /// </summary>
    Task<EmployeeCreatedResult> CreateAsync(CreateEmployeeCommand command);

    /// <summary>
    /// Updates employee information.
    /// </summary>
    Task<EmployeeUpdatedResult> UpdateAsync(UpdateEmployeeCommand command);

    /// <summary>
    /// Gets the current employee state.
    /// </summary>
    Task<EmployeeState> GetStateAsync();

    /// <summary>
    /// Assigns a role to the employee.
    /// </summary>
    Task AssignRoleAsync(AssignRoleCommand command);

    /// <summary>
    /// Removes a role from the employee.
    /// </summary>
    Task RemoveRoleAsync(Guid roleId);

    /// <summary>
    /// Grants site access to the employee.
    /// </summary>
    Task GrantSiteAccessAsync(Guid siteId);

    /// <summary>
    /// Revokes site access from the employee.
    /// </summary>
    Task RevokeSiteAccessAsync(Guid siteId);

    /// <summary>
    /// Sets the employee status to active.
    /// </summary>
    Task ActivateAsync();

    /// <summary>
    /// Sets the employee status to inactive.
    /// </summary>
    Task DeactivateAsync();

    /// <summary>
    /// Sets the employee on leave.
    /// </summary>
    Task SetOnLeaveAsync();

    /// <summary>
    /// Terminates the employee.
    /// </summary>
    Task TerminateAsync(DateOnly terminationDate, string? reason = null);

    /// <summary>
    /// Clocks the employee in.
    /// </summary>
    Task<ClockInResult> ClockInAsync(ClockInCommand command);

    /// <summary>
    /// Clocks the employee out.
    /// </summary>
    Task<ClockOutResult> ClockOutAsync(ClockOutCommand command);

    /// <summary>
    /// Checks if the employee is currently clocked in.
    /// </summary>
    Task<bool> IsClockedInAsync();

    /// <summary>
    /// Syncs employee state from a user update event.
    /// Called internally when UserGrain publishes updates.
    /// </summary>
    Task SyncFromUserAsync(string? firstName, string? lastName, UserStatus userStatus);

    /// <summary>
    /// Checks if the employee exists.
    /// </summary>
    Task<bool> ExistsAsync();
}
