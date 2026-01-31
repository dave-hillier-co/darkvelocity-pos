using DarkVelocity.Orleans.Abstractions.Grains;

namespace DarkVelocity.Orleans.Abstractions.State;

// ============================================================================
// Employee State
// ============================================================================

[GenerateSerializer]
public sealed class EmployeeState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid EmployeeId { get; set; }
    [Id(2)] public Guid UserId { get; set; }
    [Id(3)] public Guid LocationId { get; set; }
    [Id(4)] public string EmployeeNumber { get; set; } = string.Empty;
    [Id(5)] public string FirstName { get; set; } = string.Empty;
    [Id(6)] public string LastName { get; set; } = string.Empty;
    [Id(7)] public string Email { get; set; } = string.Empty;
    [Id(8)] public string Phone { get; set; } = string.Empty;
    [Id(9)] public DateTime? DateOfBirth { get; set; }
    [Id(10)] public DateTime HireDate { get; set; }
    [Id(11)] public DateTime? TerminationDate { get; set; }
    [Id(12)] public EmploymentStatus Status { get; set; }
    [Id(13)] public EmploymentType EmploymentType { get; set; }
    [Id(14)] public decimal HourlyRate { get; set; }
    [Id(15)] public decimal? SalaryAmount { get; set; }
    [Id(16)] public decimal OvertimeRate { get; set; }
    [Id(17)] public int MaxHoursPerWeek { get; set; }
    [Id(18)] public int MinHoursPerWeek { get; set; }
    [Id(19)] public Guid DefaultRoleId { get; set; }
    [Id(20)] public List<EmployeeRoleState> Roles { get; set; } = [];
    [Id(21)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class EmployeeRoleState
{
    [Id(0)] public Guid RoleId { get; set; }
    [Id(1)] public string RoleName { get; set; } = string.Empty;
    [Id(2)] public decimal? HourlyRateOverride { get; set; }
    [Id(3)] public bool IsPrimary { get; set; }
    [Id(4)] public DateTime? CertifiedAt { get; set; }
}

// ============================================================================
// Role State
// ============================================================================

[GenerateSerializer]
public sealed class RoleState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid RoleId { get; set; }
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public Department Department { get; set; }
    [Id(4)] public decimal DefaultHourlyRate { get; set; }
    [Id(5)] public string Color { get; set; } = string.Empty;
    [Id(6)] public int SortOrder { get; set; }
    [Id(7)] public List<string> RequiredCertifications { get; set; } = [];
    [Id(8)] public bool IsActive { get; set; } = true;
    [Id(9)] public int Version { get; set; }
}

// ============================================================================
// Schedule State
// ============================================================================

[GenerateSerializer]
public sealed class ScheduleState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid ScheduleId { get; set; }
    [Id(2)] public Guid LocationId { get; set; }
    [Id(3)] public DateTime WeekStartDate { get; set; }
    [Id(4)] public ScheduleStatus Status { get; set; }
    [Id(5)] public DateTime? PublishedAt { get; set; }
    [Id(6)] public Guid? PublishedByUserId { get; set; }
    [Id(7)] public decimal TotalScheduledHours { get; set; }
    [Id(8)] public decimal TotalLaborCost { get; set; }
    [Id(9)] public List<ShiftState> Shifts { get; set; } = [];
    [Id(10)] public string? Notes { get; set; }
    [Id(11)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class ShiftState
{
    [Id(0)] public Guid ShiftId { get; set; }
    [Id(1)] public Guid EmployeeId { get; set; }
    [Id(2)] public Guid RoleId { get; set; }
    [Id(3)] public DateTime Date { get; set; }
    [Id(4)] public TimeSpan StartTime { get; set; }
    [Id(5)] public TimeSpan EndTime { get; set; }
    [Id(6)] public int BreakMinutes { get; set; }
    [Id(7)] public decimal ScheduledHours { get; set; }
    [Id(8)] public decimal HourlyRate { get; set; }
    [Id(9)] public decimal LaborCost { get; set; }
    [Id(10)] public ShiftStatus Status { get; set; }
    [Id(11)] public bool IsOvertime { get; set; }
    [Id(12)] public string? Notes { get; set; }
}

// ============================================================================
// Time Entry State
// ============================================================================

[GenerateSerializer]
public sealed class TimeEntryState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid TimeEntryId { get; set; }
    [Id(2)] public Guid EmployeeId { get; set; }
    [Id(3)] public Guid LocationId { get; set; }
    [Id(4)] public Guid RoleId { get; set; }
    [Id(5)] public Guid? ShiftId { get; set; }
    [Id(6)] public DateTime ClockInAt { get; set; }
    [Id(7)] public DateTime? ClockOutAt { get; set; }
    [Id(8)] public ClockMethod ClockInMethod { get; set; }
    [Id(9)] public ClockMethod? ClockOutMethod { get; set; }
    [Id(10)] public int BreakMinutes { get; set; }
    [Id(11)] public decimal? ActualHours { get; set; }
    [Id(12)] public decimal? RegularHours { get; set; }
    [Id(13)] public decimal? OvertimeHours { get; set; }
    [Id(14)] public decimal HourlyRate { get; set; }
    [Id(15)] public decimal OvertimeRate { get; set; }
    [Id(16)] public decimal? GrossPay { get; set; }
    [Id(17)] public TimeEntryStatus Status { get; set; }
    [Id(18)] public Guid? AdjustedByUserId { get; set; }
    [Id(19)] public string? AdjustmentReason { get; set; }
    [Id(20)] public Guid? ApprovedByUserId { get; set; }
    [Id(21)] public DateTime? ApprovedAt { get; set; }
    [Id(22)] public string? Notes { get; set; }
    [Id(23)] public List<BreakState> Breaks { get; set; } = [];
    [Id(24)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class BreakState
{
    [Id(0)] public TimeSpan BreakStart { get; set; }
    [Id(1)] public TimeSpan? BreakEnd { get; set; }
    [Id(2)] public bool IsPaid { get; set; }
}

// ============================================================================
// Tip Pool State
// ============================================================================

[GenerateSerializer]
public sealed class TipPoolState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid TipPoolId { get; set; }
    [Id(2)] public Guid LocationId { get; set; }
    [Id(3)] public DateTime BusinessDate { get; set; }
    [Id(4)] public string Name { get; set; } = string.Empty;
    [Id(5)] public TipPoolMethod Method { get; set; }
    [Id(6)] public List<Guid> EligibleRoleIds { get; set; } = [];
    [Id(7)] public decimal TotalTips { get; set; }
    [Id(8)] public bool IsDistributed { get; set; }
    [Id(9)] public DateTime? DistributedAt { get; set; }
    [Id(10)] public Guid? DistributedByUserId { get; set; }
    [Id(11)] public List<TipParticipantState> Participants { get; set; } = [];
    [Id(12)] public List<TipDistributionState> Distributions { get; set; } = [];
    [Id(13)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class TipParticipantState
{
    [Id(0)] public Guid EmployeeId { get; set; }
    [Id(1)] public string EmployeeName { get; set; } = string.Empty;
    [Id(2)] public Guid RoleId { get; set; }
    [Id(3)] public decimal HoursWorked { get; set; }
    [Id(4)] public decimal Points { get; set; }
}

[GenerateSerializer]
public sealed class TipDistributionState
{
    [Id(0)] public Guid EmployeeId { get; set; }
    [Id(1)] public string EmployeeName { get; set; } = string.Empty;
    [Id(2)] public Guid RoleId { get; set; }
    [Id(3)] public decimal HoursWorked { get; set; }
    [Id(4)] public decimal Points { get; set; }
    [Id(5)] public decimal TipAmount { get; set; }
}

// ============================================================================
// Payroll Period State
// ============================================================================

[GenerateSerializer]
public sealed class PayrollPeriodState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid PayrollPeriodId { get; set; }
    [Id(2)] public Guid LocationId { get; set; }
    [Id(3)] public DateTime PeriodStart { get; set; }
    [Id(4)] public DateTime PeriodEnd { get; set; }
    [Id(5)] public PayrollStatus Status { get; set; }
    [Id(6)] public decimal TotalRegularHours { get; set; }
    [Id(7)] public decimal TotalOvertimeHours { get; set; }
    [Id(8)] public decimal TotalRegularPay { get; set; }
    [Id(9)] public decimal TotalOvertimePay { get; set; }
    [Id(10)] public decimal TotalTips { get; set; }
    [Id(11)] public decimal TotalGrossPay { get; set; }
    [Id(12)] public decimal TotalDeductions { get; set; }
    [Id(13)] public decimal TotalNetPay { get; set; }
    [Id(14)] public List<PayrollEntryState> Entries { get; set; } = [];
    [Id(15)] public Guid? ApprovedByUserId { get; set; }
    [Id(16)] public DateTime? ApprovedAt { get; set; }
    [Id(17)] public DateTime? ProcessedAt { get; set; }
    [Id(18)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class PayrollEntryState
{
    [Id(0)] public Guid EmployeeId { get; set; }
    [Id(1)] public string EmployeeName { get; set; } = string.Empty;
    [Id(2)] public decimal RegularHours { get; set; }
    [Id(3)] public decimal OvertimeHours { get; set; }
    [Id(4)] public decimal RegularPay { get; set; }
    [Id(5)] public decimal OvertimePay { get; set; }
    [Id(6)] public decimal TipsReceived { get; set; }
    [Id(7)] public decimal GrossPay { get; set; }
    [Id(8)] public decimal Deductions { get; set; }
    [Id(9)] public decimal NetPay { get; set; }
}
