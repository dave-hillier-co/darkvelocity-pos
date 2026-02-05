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

// ============================================================================
// Break Commands and Results
// ============================================================================

[GenerateSerializer]
public record StartBreakCommand(
    [property: Id(0)] string BreakType,
    [property: Id(1)] bool IsPaid = false);

[GenerateSerializer]
public record StartBreakResult(
    [property: Id(0)] Guid BreakId,
    [property: Id(1)] DateTime StartTime,
    [property: Id(2)] string BreakType,
    [property: Id(3)] bool IsPaid);

[GenerateSerializer]
public record EndBreakResult(
    [property: Id(0)] Guid BreakId,
    [property: Id(1)] DateTime EndTime,
    [property: Id(2)] decimal DurationMinutes);

// ============================================================================
// Certification Commands and Results
// ============================================================================

[GenerateSerializer]
public record AddCertificationCommand(
    [property: Id(0)] string CertificationType,
    [property: Id(1)] string CertificationName,
    [property: Id(2)] DateOnly IssuedDate,
    [property: Id(3)] DateOnly ExpirationDate,
    [property: Id(4)] string? CertificationNumber = null,
    [property: Id(5)] string? IssuingAuthority = null);

[GenerateSerializer]
public record UpdateCertificationCommand(
    [property: Id(0)] Guid CertificationId,
    [property: Id(1)] DateOnly? NewExpirationDate = null,
    [property: Id(2)] string? NewCertificationNumber = null);

[GenerateSerializer]
public record CertificationSnapshot(
    [property: Id(0)] Guid Id,
    [property: Id(1)] string CertificationType,
    [property: Id(2)] string CertificationName,
    [property: Id(3)] string? CertificationNumber,
    [property: Id(4)] DateOnly IssuedDate,
    [property: Id(5)] DateOnly ExpirationDate,
    [property: Id(6)] string? IssuingAuthority,
    [property: Id(7)] string Status,
    [property: Id(8)] int DaysUntilExpiration);

[GenerateSerializer]
public record CertificationComplianceResult(
    [property: Id(0)] bool IsCompliant,
    [property: Id(1)] IReadOnlyList<string> MissingCertifications,
    [property: Id(2)] IReadOnlyList<CertificationSnapshot> ExpiredCertifications,
    [property: Id(3)] IReadOnlyList<CertificationSnapshot> ExpiringSoonCertifications);

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

    // ============================================================================
    // Break Tracking
    // ============================================================================

    /// <summary>
    /// Starts a break for the currently clocked-in employee.
    /// </summary>
    Task<StartBreakResult> StartBreakAsync(StartBreakCommand command);

    /// <summary>
    /// Ends the current break.
    /// </summary>
    Task<EndBreakResult> EndBreakAsync();

    /// <summary>
    /// Checks if the employee is currently on break.
    /// </summary>
    Task<bool> IsOnBreakAsync();

    /// <summary>
    /// Gets break summary for the current time entry.
    /// </summary>
    Task<BreakSummary> GetBreakSummaryAsync();

    // ============================================================================
    // Certification Tracking
    // ============================================================================

    /// <summary>
    /// Adds a certification to the employee.
    /// </summary>
    Task<CertificationSnapshot> AddCertificationAsync(AddCertificationCommand command);

    /// <summary>
    /// Updates a certification.
    /// </summary>
    Task<CertificationSnapshot> UpdateCertificationAsync(UpdateCertificationCommand command);

    /// <summary>
    /// Removes a certification.
    /// </summary>
    Task RemoveCertificationAsync(Guid certificationId, string reason);

    /// <summary>
    /// Gets all certifications for the employee.
    /// </summary>
    Task<IReadOnlyList<CertificationSnapshot>> GetCertificationsAsync();

    /// <summary>
    /// Checks if the employee has valid certifications for a role.
    /// </summary>
    Task<CertificationComplianceResult> CheckCertificationComplianceAsync(IReadOnlyList<string> requiredCertifications);

    /// <summary>
    /// Checks certifications and raises alerts for expiring ones.
    /// </summary>
    Task<IReadOnlyList<CertificationSnapshot>> CheckCertificationExpirationsAsync(int warningDays = 30, int criticalDays = 7);

    /// <summary>
    /// Sets the jurisdiction code for the employee (for labor law compliance).
    /// </summary>
    Task SetJurisdictionAsync(string jurisdictionCode);
}

[GenerateSerializer]
public record BreakSummary(
    [property: Id(0)] decimal TotalPaidBreakMinutes,
    [property: Id(1)] decimal TotalUnpaidBreakMinutes,
    [property: Id(2)] decimal NetWorkedHours,
    [property: Id(3)] int BreakCount,
    [property: Id(4)] bool IsCurrentlyOnBreak);
