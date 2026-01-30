namespace DarkVelocity.Shared.Contracts.Events;

/// <summary>
/// Published when a new employee record is created.
/// </summary>
public sealed record EmployeeCreated(
    Guid EmployeeId,
    Guid TenantId,
    Guid UserId,
    Guid LocationId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    string EmploymentType,
    Guid DefaultRoleId,
    string DefaultRoleName,
    DateOnly HireDate
) : IntegrationEvent
{
    public override string EventType => "labor.employee.created";
}

/// <summary>
/// Published when an employee's details are updated.
/// </summary>
public sealed record EmployeeUpdated(
    Guid EmployeeId,
    Guid TenantId,
    Guid UserId,
    List<string> ChangedFields
) : IntegrationEvent
{
    public override string EventType => "labor.employee.updated";
}

/// <summary>
/// Published when an employee is terminated.
/// </summary>
public sealed record EmployeeTerminated(
    Guid EmployeeId,
    Guid TenantId,
    Guid UserId,
    Guid LocationId,
    DateOnly TerminationDate,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "labor.employee.terminated";
}

/// <summary>
/// Published when an employee's status changes (e.g., active, onleave, terminated).
/// </summary>
public sealed record EmployeeStatusChanged(
    Guid EmployeeId,
    Guid TenantId,
    Guid UserId,
    string OldStatus,
    string NewStatus
) : IntegrationEvent
{
    public override string EventType => "labor.employee.status_changed";
}

/// <summary>
/// Published when an employee is assigned a new role.
/// </summary>
public sealed record EmployeeRoleAssigned(
    Guid EmployeeId,
    Guid TenantId,
    Guid UserId,
    Guid RoleId,
    string RoleName,
    string Department,
    bool IsPrimary,
    decimal? HourlyRateOverride
) : IntegrationEvent
{
    public override string EventType => "labor.employee.role_assigned";
}

/// <summary>
/// Published when an employee's role assignment is removed.
/// </summary>
public sealed record EmployeeRoleRemoved(
    Guid EmployeeId,
    Guid TenantId,
    Guid UserId,
    Guid RoleId,
    string RoleName
) : IntegrationEvent
{
    public override string EventType => "labor.employee.role_removed";
}

/// <summary>
/// Published when an employee's default role changes.
/// </summary>
public sealed record EmployeeDefaultRoleChanged(
    Guid EmployeeId,
    Guid TenantId,
    Guid UserId,
    Guid OldRoleId,
    string OldRoleName,
    Guid NewRoleId,
    string NewRoleName
) : IntegrationEvent
{
    public override string EventType => "labor.employee.default_role_changed";
}

/// <summary>
/// Published when an employee gains access to a location.
/// </summary>
public sealed record EmployeeLocationAdded(
    Guid EmployeeId,
    Guid TenantId,
    Guid UserId,
    Guid LocationId
) : IntegrationEvent
{
    public override string EventType => "labor.employee.location_added";
}

/// <summary>
/// Published when an employee loses access to a location.
/// </summary>
public sealed record EmployeeLocationRemoved(
    Guid EmployeeId,
    Guid TenantId,
    Guid UserId,
    Guid LocationId
) : IntegrationEvent
{
    public override string EventType => "labor.employee.location_removed";
}

/// <summary>
/// Published when an employee clocks in.
/// </summary>
public sealed record EmployeeClockedIn(
    Guid TimeEntryId,
    Guid EmployeeId,
    Guid TenantId,
    Guid UserId,
    Guid LocationId,
    Guid? ShiftId,
    DateTime ClockInTime
) : IntegrationEvent
{
    public override string EventType => "labor.time.clocked_in";
}

/// <summary>
/// Published when an employee clocks out.
/// </summary>
public sealed record EmployeeClockedOut(
    Guid TimeEntryId,
    Guid EmployeeId,
    Guid TenantId,
    Guid UserId,
    Guid LocationId,
    DateTime ClockOutTime,
    decimal TotalHours,
    decimal? RegularHours,
    decimal? OvertimeHours
) : IntegrationEvent
{
    public override string EventType => "labor.time.clocked_out";
}

/// <summary>
/// Published when an employee's compensation details change.
/// </summary>
public sealed record EmployeeCompensationChanged(
    Guid EmployeeId,
    Guid TenantId,
    Guid UserId,
    decimal? OldHourlyRate,
    decimal? NewHourlyRate,
    decimal? OldSalaryAmount,
    decimal? NewSalaryAmount,
    string? OldPayFrequency,
    string? NewPayFrequency
) : IntegrationEvent
{
    public override string EventType => "labor.employee.compensation_changed";
}
