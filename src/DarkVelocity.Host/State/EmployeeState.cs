namespace DarkVelocity.Host.State;

public enum EmploymentType
{
    FullTime,
    PartTime,
    Contractor,
    Seasonal,
    Temporary
}

public enum EmployeeStatus
{
    Active,
    Inactive,
    OnLeave,
    Terminated
}

[GenerateSerializer]
public sealed class EmployeeRoleAssignment
{
    [Id(0)] public Guid RoleId { get; set; }
    [Id(1)] public string RoleName { get; set; } = string.Empty;
    [Id(2)] public string Department { get; set; } = string.Empty;
    [Id(3)] public bool IsPrimary { get; set; }
    [Id(4)] public decimal? HourlyRateOverride { get; set; }
    [Id(5)] public DateTime AssignedAt { get; set; }
}

[GenerateSerializer]
public sealed class TimeEntry
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public Guid? ShiftId { get; set; }
    [Id(3)] public DateTime ClockIn { get; set; }
    [Id(4)] public DateTime? ClockOut { get; set; }
    [Id(5)] public decimal? TotalHours { get; set; }
    [Id(6)] public string? Notes { get; set; }
}

[GenerateSerializer]
public sealed class EmployeeState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid UserId { get; set; }
    [Id(3)] public string EmployeeNumber { get; set; } = string.Empty;
    [Id(4)] public string FirstName { get; set; } = string.Empty;
    [Id(5)] public string LastName { get; set; } = string.Empty;
    [Id(6)] public string Email { get; set; } = string.Empty;
    [Id(7)] public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
    [Id(8)] public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    [Id(9)] public DateOnly HireDate { get; set; }
    [Id(10)] public DateOnly? TerminationDate { get; set; }
    [Id(11)] public string? TerminationReason { get; set; }
    [Id(12)] public Guid DefaultSiteId { get; set; }
    [Id(13)] public List<Guid> AllowedSiteIds { get; set; } = [];
    [Id(14)] public List<EmployeeRoleAssignment> RoleAssignments { get; set; } = [];
    [Id(15)] public decimal? HourlyRate { get; set; }
    [Id(16)] public decimal? SalaryAmount { get; set; }
    [Id(17)] public string? PayFrequency { get; set; }
    [Id(18)] public TimeEntry? CurrentTimeEntry { get; set; }
    [Id(19)] public List<TimeEntry> RecentTimeEntries { get; set; } = [];
    [Id(20)] public DateTime CreatedAt { get; set; }
    [Id(21)] public DateTime? UpdatedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public EmployeeRoleAssignment? PrimaryRole =>
        RoleAssignments.FirstOrDefault(r => r.IsPrimary) ?? RoleAssignments.FirstOrDefault();

    public bool IsClockedIn => CurrentTimeEntry != null;
}
