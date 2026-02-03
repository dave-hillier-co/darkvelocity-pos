using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record CreateEmployeeCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] Guid DefaultSiteId,
    [property: Id(3)] string EmployeeNumber,
    [property: Id(4)] string FirstName,
    [property: Id(5)] string LastName,
    [property: Id(6)] string Email,
    [property: Id(7)] EmploymentType EmploymentType = EmploymentType.FullTime,
    [property: Id(8)] DateOnly? HireDate = null);

[GenerateSerializer]
public record UpdateEmployeeCommand(
    [property: Id(0)] string? FirstName = null,
    [property: Id(1)] string? LastName = null,
    [property: Id(2)] string? Email = null,
    [property: Id(3)] decimal? HourlyRate = null,
    [property: Id(4)] decimal? SalaryAmount = null,
    [property: Id(5)] string? PayFrequency = null);

[GenerateSerializer]
public record AssignRoleCommand(
    [property: Id(0)] Guid RoleId,
    [property: Id(1)] string RoleName,
    [property: Id(2)] string Department,
    [property: Id(3)] bool IsPrimary = false,
    [property: Id(4)] decimal? HourlyRateOverride = null);

[GenerateSerializer]
public record ClockInCommand(
    [property: Id(0)] Guid SiteId,
    [property: Id(1)] Guid? ShiftId = null);

[GenerateSerializer]
public record ClockOutCommand(
    [property: Id(0)] string? Notes = null);

[GenerateSerializer]
public record EmployeeCreatedResult([property: Id(0)] Guid Id, [property: Id(1)] string EmployeeNumber, [property: Id(2)] DateTime CreatedAt);
[GenerateSerializer]
public record EmployeeUpdatedResult([property: Id(0)] int Version, [property: Id(1)] DateTime UpdatedAt);
[GenerateSerializer]
public record ClockInResult([property: Id(0)] Guid TimeEntryId, [property: Id(1)] DateTime ClockInTime);
[GenerateSerializer]
public record ClockOutResult([property: Id(0)] Guid TimeEntryId, [property: Id(1)] DateTime ClockOutTime, [property: Id(2)] decimal TotalHours);

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
