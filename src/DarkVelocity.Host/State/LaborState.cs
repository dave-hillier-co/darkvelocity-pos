using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

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

// ============================================================================
// Employee Availability State
// ============================================================================

[GenerateSerializer]
public sealed class EmployeeAvailabilityState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid EmployeeId { get; set; }
    [Id(2)] public List<AvailabilityEntryState> Availabilities { get; set; } = [];
    [Id(3)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class AvailabilityEntryState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public int DayOfWeek { get; set; }
    [Id(2)] public TimeSpan? StartTime { get; set; }
    [Id(3)] public TimeSpan? EndTime { get; set; }
    [Id(4)] public bool IsAvailable { get; set; }
    [Id(5)] public bool IsPreferred { get; set; }
    [Id(6)] public DateOnly EffectiveFrom { get; set; }
    [Id(7)] public DateOnly? EffectiveTo { get; set; }
    [Id(8)] public string? Notes { get; set; }
}

// ============================================================================
// Shift Swap Request State
// ============================================================================

[GenerateSerializer]
public sealed class ShiftSwapState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SwapRequestId { get; set; }
    [Id(2)] public Guid RequestingEmployeeId { get; set; }
    [Id(3)] public string RequestingEmployeeName { get; set; } = string.Empty;
    [Id(4)] public Guid RequestingShiftId { get; set; }
    [Id(5)] public Guid? TargetEmployeeId { get; set; }
    [Id(6)] public string? TargetEmployeeName { get; set; }
    [Id(7)] public Guid? TargetShiftId { get; set; }
    [Id(8)] public ShiftSwapType Type { get; set; }
    [Id(9)] public ShiftSwapStatus Status { get; set; }
    [Id(10)] public DateTime RequestedAt { get; set; }
    [Id(11)] public DateTime? RespondedAt { get; set; }
    [Id(12)] public Guid? ManagerApprovedByUserId { get; set; }
    [Id(13)] public string? Reason { get; set; }
    [Id(14)] public string? Notes { get; set; }
    [Id(15)] public int Version { get; set; }
}

// ============================================================================
// Time Off Request State
// ============================================================================

[GenerateSerializer]
public sealed class TimeOffState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid TimeOffRequestId { get; set; }
    [Id(2)] public Guid EmployeeId { get; set; }
    [Id(3)] public string EmployeeName { get; set; } = string.Empty;
    [Id(4)] public TimeOffType Type { get; set; }
    [Id(5)] public DateOnly StartDate { get; set; }
    [Id(6)] public DateOnly EndDate { get; set; }
    [Id(7)] public int TotalDays { get; set; }
    [Id(8)] public bool IsPaid { get; set; }
    [Id(9)] public TimeOffStatus Status { get; set; }
    [Id(10)] public DateTime RequestedAt { get; set; }
    [Id(11)] public Guid? ReviewedByUserId { get; set; }
    [Id(12)] public DateTime? ReviewedAt { get; set; }
    [Id(13)] public string? Reason { get; set; }
    [Id(14)] public string? Notes { get; set; }
    [Id(15)] public int Version { get; set; }
}
