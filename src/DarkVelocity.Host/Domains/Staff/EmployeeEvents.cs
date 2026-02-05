using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Employee events used in event sourcing.
/// </summary>
public interface IEmployeeEvent
{
    Guid EmployeeId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record EmployeeCreated : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid UserId { get; init; }
    [Id(3)] public Guid DefaultSiteId { get; init; }
    [Id(4)] public string EmployeeNumber { get; init; } = "";
    [Id(5)] public string FirstName { get; init; } = "";
    [Id(6)] public string LastName { get; init; } = "";
    [Id(7)] public string Email { get; init; } = "";
    [Id(8)] public string? Phone { get; init; }
    [Id(9)] public EmploymentType EmploymentType { get; init; }
    [Id(10)] public DateOnly HireDate { get; init; }
    [Id(11)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeProfileUpdated : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public string? FirstName { get; init; }
    [Id(2)] public string? LastName { get; init; }
    [Id(3)] public string? Phone { get; init; }
    [Id(4)] public string? Email { get; init; }
    [Id(5)] public Guid UpdatedBy { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeStatusChanged : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public EmployeeStatus OldStatus { get; init; }
    [Id(2)] public EmployeeStatus NewStatus { get; init; }
    [Id(3)] public string? Reason { get; init; }
    [Id(4)] public Guid? ChangedBy { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeRoleAssigned : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public string RoleName { get; init; } = "";
    [Id(2)] public Guid? SiteId { get; init; }
    [Id(3)] public Guid AssignedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeRoleRevoked : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public string RoleName { get; init; } = "";
    [Id(2)] public Guid? SiteId { get; init; }
    [Id(3)] public Guid RevokedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeSiteAssigned : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public Guid AssignedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeSiteRemoved : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public Guid RemovedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeClockedIn : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public Guid? ShiftId { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeClockedOut : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public decimal TotalHours { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeBreakStarted : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public string BreakType { get; init; } = ""; // paid, unpaid, meal
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeBreakEnded : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public decimal BreakDurationMinutes { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeePayRateChanged : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public decimal OldRate { get; init; }
    [Id(2)] public decimal NewRate { get; init; }
    [Id(3)] public string RateType { get; init; } = ""; // hourly, salary
    [Id(4)] public DateOnly EffectiveDate { get; init; }
    [Id(5)] public Guid ChangedBy { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeTerminated : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public DateOnly TerminationDate { get; init; }
    [Id(2)] public string? Reason { get; init; }
    [Id(3)] public Guid TerminatedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeRehired : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public DateOnly RehireDate { get; init; }
    [Id(2)] public Guid DefaultSiteId { get; init; }
    [Id(3)] public Guid RehiredBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

// ============================================================================
// Break Tracking Events
// ============================================================================

[GenerateSerializer]
public sealed record BreakStarted : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public Guid TimeEntryId { get; init; }
    [Id(2)] public Guid BreakId { get; init; }
    [Id(3)] public string BreakType { get; init; } = ""; // meal, rest, paid, unpaid
    [Id(4)] public bool IsPaid { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BreakEnded : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public Guid TimeEntryId { get; init; }
    [Id(2)] public Guid BreakId { get; init; }
    [Id(3)] public decimal DurationMinutes { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BreakMissedAlertRaised : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public Guid TimeEntryId { get; init; }
    [Id(2)] public string AlertType { get; init; } = ""; // missed_meal, missed_rest, late_break
    [Id(3)] public string Description { get; init; } = "";
    [Id(4)] public string JurisdictionCode { get; init; } = "";
    [Id(5)] public int RequiredBreakMinutes { get; init; }
    [Id(6)] public decimal HoursWorkedWithoutBreak { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

// ============================================================================
// Certification Tracking Events
// ============================================================================

[GenerateSerializer]
public sealed record CertificationAdded : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public Guid CertificationId { get; init; }
    [Id(2)] public string CertificationType { get; init; } = ""; // food_handler, alcohol_service, servsafe, tips
    [Id(3)] public string CertificationName { get; init; } = "";
    [Id(4)] public string? CertificationNumber { get; init; }
    [Id(5)] public DateOnly IssuedDate { get; init; }
    [Id(6)] public DateOnly ExpirationDate { get; init; }
    [Id(7)] public string? IssuingAuthority { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CertificationUpdated : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public Guid CertificationId { get; init; }
    [Id(2)] public DateOnly? NewExpirationDate { get; init; }
    [Id(3)] public string? NewCertificationNumber { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CertificationRemoved : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public Guid CertificationId { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CertificationExpirationAlertRaised : IEmployeeEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public Guid CertificationId { get; init; }
    [Id(2)] public string CertificationType { get; init; } = "";
    [Id(3)] public string CertificationName { get; init; } = "";
    [Id(4)] public DateOnly ExpirationDate { get; init; }
    [Id(5)] public int DaysUntilExpiration { get; init; }
    [Id(6)] public string AlertLevel { get; init; } = ""; // warning, critical, expired
    [Id(7)] public DateTime OccurredAt { get; init; }
}
