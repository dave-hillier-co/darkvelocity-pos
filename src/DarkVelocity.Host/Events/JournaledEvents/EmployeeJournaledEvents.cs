using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Events.JournaledEvents;

/// <summary>
/// Base interface for all Employee journaled events used in event sourcing.
/// </summary>
public interface IEmployeeJournaledEvent
{
    Guid EmployeeId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record EmployeeCreatedJournaledEvent : IEmployeeJournaledEvent
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
public sealed record EmployeeProfileUpdatedJournaledEvent : IEmployeeJournaledEvent
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
public sealed record EmployeeStatusChangedJournaledEvent : IEmployeeJournaledEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public EmployeeStatus OldStatus { get; init; }
    [Id(2)] public EmployeeStatus NewStatus { get; init; }
    [Id(3)] public string? Reason { get; init; }
    [Id(4)] public Guid? ChangedBy { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeRoleAssignedJournaledEvent : IEmployeeJournaledEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public string RoleName { get; init; } = "";
    [Id(2)] public Guid? SiteId { get; init; }
    [Id(3)] public Guid AssignedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeRoleRevokedJournaledEvent : IEmployeeJournaledEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public string RoleName { get; init; } = "";
    [Id(2)] public Guid? SiteId { get; init; }
    [Id(3)] public Guid RevokedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeSiteAssignedJournaledEvent : IEmployeeJournaledEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public Guid AssignedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeSiteRemovedJournaledEvent : IEmployeeJournaledEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public Guid RemovedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeClockedInJournaledEvent : IEmployeeJournaledEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public Guid? ShiftId { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeClockedOutJournaledEvent : IEmployeeJournaledEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public decimal TotalHours { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeBreakStartedJournaledEvent : IEmployeeJournaledEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public string BreakType { get; init; } = ""; // paid, unpaid, meal
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeBreakEndedJournaledEvent : IEmployeeJournaledEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public decimal BreakDurationMinutes { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeePayRateChangedJournaledEvent : IEmployeeJournaledEvent
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
public sealed record EmployeeTerminatedJournaledEvent : IEmployeeJournaledEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public DateOnly TerminationDate { get; init; }
    [Id(2)] public string? Reason { get; init; }
    [Id(3)] public Guid TerminatedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record EmployeeRehiredJournaledEvent : IEmployeeJournaledEvent
{
    [Id(0)] public Guid EmployeeId { get; init; }
    [Id(1)] public DateOnly RehireDate { get; init; }
    [Id(2)] public Guid DefaultSiteId { get; init; }
    [Id(3)] public Guid RehiredBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}
