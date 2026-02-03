namespace DarkVelocity.Host.Grains;

// Note: IEmployeeGrain and related types are defined in IEmployeeGrain.cs

public enum Department
{
    FrontOfHouse,
    BackOfHouse,
    Management
}

// ============================================================================
// Role Grain
// ============================================================================

[GenerateSerializer]
public record CreateRoleCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] Department Department,
    [property: Id(2)] decimal DefaultHourlyRate,
    [property: Id(3)] string Color,
    [property: Id(4)] int SortOrder,
    [property: Id(5)] IReadOnlyList<string> RequiredCertifications);

[GenerateSerializer]
public record UpdateRoleCommand(
    [property: Id(0)] string? Name,
    [property: Id(1)] Department? Department,
    [property: Id(2)] decimal? DefaultHourlyRate,
    [property: Id(3)] string? Color,
    [property: Id(4)] int? SortOrder,
    [property: Id(5)] bool? IsActive);

[GenerateSerializer]
public record RoleSnapshot(
    [property: Id(0)] Guid RoleId,
    [property: Id(1)] string Name,
    [property: Id(2)] Department Department,
    [property: Id(3)] decimal DefaultHourlyRate,
    [property: Id(4)] string Color,
    [property: Id(5)] int SortOrder,
    [property: Id(6)] IReadOnlyList<string> RequiredCertifications,
    [property: Id(7)] bool IsActive);

/// <summary>
/// Grain for role management.
/// Key: "{orgId}:role:{roleId}"
/// </summary>
public interface IRoleGrain : IGrainWithStringKey
{
    Task<RoleSnapshot> CreateAsync(CreateRoleCommand command);
    Task<RoleSnapshot> UpdateAsync(UpdateRoleCommand command);
    Task<RoleSnapshot> GetSnapshotAsync();
}

// ============================================================================
// Schedule Grain
// ============================================================================

public enum ScheduleStatus
{
    Draft,
    Published,
    Locked
}

[GenerateSerializer]
public record CreateScheduleCommand(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] DateTime WeekStartDate,
    [property: Id(2)] string? Notes);

[GenerateSerializer]
public record PublishScheduleCommand(
    [property: Id(0)] Guid PublishedByUserId);

[GenerateSerializer]
public record AddShiftCommand(
    [property: Id(0)] Guid ShiftId,
    [property: Id(1)] Guid EmployeeId,
    [property: Id(2)] Guid RoleId,
    [property: Id(3)] DateTime Date,
    [property: Id(4)] TimeSpan StartTime,
    [property: Id(5)] TimeSpan EndTime,
    [property: Id(6)] int BreakMinutes,
    [property: Id(7)] decimal HourlyRate,
    [property: Id(8)] string? Notes);

[GenerateSerializer]
public record UpdateShiftCommand(
    [property: Id(0)] Guid ShiftId,
    [property: Id(1)] TimeSpan? StartTime,
    [property: Id(2)] TimeSpan? EndTime,
    [property: Id(3)] int? BreakMinutes,
    [property: Id(4)] Guid? EmployeeId,
    [property: Id(5)] Guid? RoleId,
    [property: Id(6)] string? Notes);

[GenerateSerializer]
public record ShiftSnapshot(
    [property: Id(0)] Guid ShiftId,
    [property: Id(1)] Guid EmployeeId,
    [property: Id(2)] Guid RoleId,
    [property: Id(3)] DateTime Date,
    [property: Id(4)] TimeSpan StartTime,
    [property: Id(5)] TimeSpan EndTime,
    [property: Id(6)] int BreakMinutes,
    [property: Id(7)] decimal ScheduledHours,
    [property: Id(8)] decimal HourlyRate,
    [property: Id(9)] decimal LaborCost,
    [property: Id(10)] ShiftStatus Status,
    [property: Id(11)] bool IsOvertime,
    [property: Id(12)] string? Notes);

public enum ShiftStatus
{
    Scheduled,
    Confirmed,
    Started,
    Completed,
    NoShow,
    Cancelled
}

[GenerateSerializer]
public record ScheduleSnapshot(
    [property: Id(0)] Guid ScheduleId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] DateTime WeekStartDate,
    [property: Id(3)] ScheduleStatus Status,
    [property: Id(4)] DateTime? PublishedAt,
    [property: Id(5)] Guid? PublishedByUserId,
    [property: Id(6)] decimal TotalScheduledHours,
    [property: Id(7)] decimal TotalLaborCost,
    [property: Id(8)] IReadOnlyList<ShiftSnapshot> Shifts,
    [property: Id(9)] string? Notes);

/// <summary>
/// Grain for schedule management.
/// Key: "{orgId}:{locationId}:schedule:{weekStartDate:yyyy-MM-dd}"
/// </summary>
public interface IScheduleGrain : IGrainWithStringKey
{
    Task<ScheduleSnapshot> CreateAsync(CreateScheduleCommand command);
    Task<ScheduleSnapshot> PublishAsync(PublishScheduleCommand command);
    Task LockAsync();
    Task AddShiftAsync(AddShiftCommand command);
    Task UpdateShiftAsync(UpdateShiftCommand command);
    Task RemoveShiftAsync(Guid shiftId);
    Task<ScheduleSnapshot> GetSnapshotAsync();
    Task<IReadOnlyList<ShiftSnapshot>> GetShiftsForEmployeeAsync(Guid employeeId);
    Task<IReadOnlyList<ShiftSnapshot>> GetShiftsForDateAsync(DateTime date);
    Task<decimal> GetTotalLaborCostAsync();
}

// ============================================================================
// Time Entry Grain
// ============================================================================

public enum ClockMethod
{
    Pin,
    Qr,
    Biometric,
    Manager,
    Auto
}

public enum TimeEntryStatus
{
    Active,
    Completed,
    Adjusted,
    Disputed
}

[GenerateSerializer]
public record TimeEntryClockInCommand(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] Guid RoleId,
    [property: Id(3)] Guid? ShiftId,
    [property: Id(4)] ClockMethod Method,
    [property: Id(5)] string? Notes);

[GenerateSerializer]
public record TimeEntryClockOutCommand(
    [property: Id(0)] ClockMethod Method,
    [property: Id(1)] string? Notes);

[GenerateSerializer]
public record AddBreakCommand(
    [property: Id(0)] TimeSpan BreakStart,
    [property: Id(1)] TimeSpan? BreakEnd,
    [property: Id(2)] bool IsPaid);

[GenerateSerializer]
public record AdjustTimeEntryCommand(
    [property: Id(0)] Guid AdjustedByUserId,
    [property: Id(1)] DateTime? ClockInAt,
    [property: Id(2)] DateTime? ClockOutAt,
    [property: Id(3)] int? BreakMinutes,
    [property: Id(4)] string Reason);

[GenerateSerializer]
public record ApproveTimeEntryCommand(
    [property: Id(0)] Guid ApprovedByUserId);

[GenerateSerializer]
public record TimeEntrySnapshot(
    [property: Id(0)] Guid TimeEntryId,
    [property: Id(1)] Guid EmployeeId,
    [property: Id(2)] Guid LocationId,
    [property: Id(3)] Guid RoleId,
    [property: Id(4)] Guid? ShiftId,
    [property: Id(5)] DateTime ClockInAt,
    [property: Id(6)] DateTime? ClockOutAt,
    [property: Id(7)] ClockMethod ClockInMethod,
    [property: Id(8)] ClockMethod? ClockOutMethod,
    [property: Id(9)] int BreakMinutes,
    [property: Id(10)] decimal? ActualHours,
    [property: Id(11)] decimal? RegularHours,
    [property: Id(12)] decimal? OvertimeHours,
    [property: Id(13)] decimal HourlyRate,
    [property: Id(14)] decimal OvertimeRate,
    [property: Id(15)] decimal? GrossPay,
    [property: Id(16)] TimeEntryStatus Status,
    [property: Id(17)] Guid? AdjustedByUserId,
    [property: Id(18)] string? AdjustmentReason,
    [property: Id(19)] Guid? ApprovedByUserId,
    [property: Id(20)] DateTime? ApprovedAt,
    [property: Id(21)] string? Notes);

/// <summary>
/// Grain for time entry management.
/// Key: "{orgId}:timeentry:{timeEntryId}"
/// </summary>
public interface ITimeEntryGrain : IGrainWithStringKey
{
    Task<TimeEntrySnapshot> ClockInAsync(TimeEntryClockInCommand command);
    Task<TimeEntrySnapshot> ClockOutAsync(TimeEntryClockOutCommand command);
    Task AddBreakAsync(AddBreakCommand command);
    Task<TimeEntrySnapshot> AdjustAsync(AdjustTimeEntryCommand command);
    Task<TimeEntrySnapshot> ApproveAsync(ApproveTimeEntryCommand command);
    Task<TimeEntrySnapshot> GetSnapshotAsync();
    Task<bool> IsActiveAsync();
}

// ============================================================================
// Tip Pool Grain
// ============================================================================

public enum TipPoolMethod
{
    Equal,
    ByHoursWorked,
    ByPoints
}

[GenerateSerializer]
public record CreateTipPoolCommand(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] DateTime BusinessDate,
    [property: Id(2)] string Name,
    [property: Id(3)] TipPoolMethod Method,
    [property: Id(4)] IReadOnlyList<Guid> EligibleRoleIds);

[GenerateSerializer]
public record AddTipsCommand(
    [property: Id(0)] decimal Amount,
    [property: Id(1)] string Source);

[GenerateSerializer]
public record DistributeTipsCommand(
    [property: Id(0)] Guid DistributedByUserId);

[GenerateSerializer]
public record TipDistribution(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] string EmployeeName,
    [property: Id(2)] Guid RoleId,
    [property: Id(3)] decimal HoursWorked,
    [property: Id(4)] decimal Points,
    [property: Id(5)] decimal TipAmount);

[GenerateSerializer]
public record TipPoolSnapshot(
    [property: Id(0)] Guid TipPoolId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] DateTime BusinessDate,
    [property: Id(3)] string Name,
    [property: Id(4)] TipPoolMethod Method,
    [property: Id(5)] decimal TotalTips,
    [property: Id(6)] bool IsDistributed,
    [property: Id(7)] DateTime? DistributedAt,
    [property: Id(8)] Guid? DistributedByUserId,
    [property: Id(9)] IReadOnlyList<TipDistribution> Distributions);

/// <summary>
/// Grain for tip pool management.
/// Key: "{orgId}:{locationId}:tippool:{date:yyyy-MM-dd}:{poolName}"
/// </summary>
public interface ITipPoolGrain : IGrainWithStringKey
{
    Task<TipPoolSnapshot> CreateAsync(CreateTipPoolCommand command);
    Task AddTipsAsync(AddTipsCommand command);
    Task<TipPoolSnapshot> DistributeAsync(DistributeTipsCommand command);
    Task<TipPoolSnapshot> GetSnapshotAsync();
    Task AddParticipantAsync(Guid employeeId, decimal hoursWorked, decimal points);
}

// ============================================================================
// Payroll Period Grain
// ============================================================================

public enum PayrollStatus
{
    Open,
    Calculating,
    PendingApproval,
    Approved,
    Processed
}

[GenerateSerializer]
public record CreatePayrollPeriodCommand(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] DateTime PeriodStart,
    [property: Id(2)] DateTime PeriodEnd);

[GenerateSerializer]
public record PayrollEntrySnapshot(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] string EmployeeName,
    [property: Id(2)] decimal RegularHours,
    [property: Id(3)] decimal OvertimeHours,
    [property: Id(4)] decimal RegularPay,
    [property: Id(5)] decimal OvertimePay,
    [property: Id(6)] decimal TipsReceived,
    [property: Id(7)] decimal GrossPay,
    [property: Id(8)] decimal Deductions,
    [property: Id(9)] decimal NetPay);

[GenerateSerializer]
public record PayrollPeriodSnapshot(
    [property: Id(0)] Guid PayrollPeriodId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] DateTime PeriodStart,
    [property: Id(3)] DateTime PeriodEnd,
    [property: Id(4)] PayrollStatus Status,
    [property: Id(5)] decimal TotalRegularHours,
    [property: Id(6)] decimal TotalOvertimeHours,
    [property: Id(7)] decimal TotalRegularPay,
    [property: Id(8)] decimal TotalOvertimePay,
    [property: Id(9)] decimal TotalTips,
    [property: Id(10)] decimal TotalGrossPay,
    [property: Id(11)] decimal TotalDeductions,
    [property: Id(12)] decimal TotalNetPay,
    [property: Id(13)] IReadOnlyList<PayrollEntrySnapshot> Entries);

/// <summary>
/// Grain for payroll period management.
/// Key: "{orgId}:{locationId}:payroll:{periodStart:yyyy-MM-dd}"
/// </summary>
public interface IPayrollPeriodGrain : IGrainWithStringKey
{
    Task<PayrollPeriodSnapshot> CreateAsync(CreatePayrollPeriodCommand command);
    Task CalculateAsync();
    Task ApproveAsync(Guid approvedByUserId);
    Task ProcessAsync();
    Task<PayrollPeriodSnapshot> GetSnapshotAsync();
    Task<PayrollEntrySnapshot> GetEmployeePayrollAsync(Guid employeeId);
}

// ============================================================================
// Employee Availability Grain
// ============================================================================

[GenerateSerializer]
public record SetAvailabilityCommand(
    [property: Id(0)] int DayOfWeek,
    [property: Id(1)] TimeSpan? StartTime,
    [property: Id(2)] TimeSpan? EndTime,
    [property: Id(3)] bool IsAvailable,
    [property: Id(4)] bool IsPreferred,
    [property: Id(5)] DateOnly? EffectiveFrom,
    [property: Id(6)] DateOnly? EffectiveTo,
    [property: Id(7)] string? Notes);

[GenerateSerializer]
public record AvailabilityEntrySnapshot(
    [property: Id(0)] Guid Id,
    [property: Id(1)] int DayOfWeek,
    [property: Id(2)] string DayOfWeekName,
    [property: Id(3)] TimeSpan? StartTime,
    [property: Id(4)] TimeSpan? EndTime,
    [property: Id(5)] bool IsAvailable,
    [property: Id(6)] bool IsPreferred,
    [property: Id(7)] DateOnly EffectiveFrom,
    [property: Id(8)] DateOnly? EffectiveTo,
    [property: Id(9)] string? Notes);

[GenerateSerializer]
public record EmployeeAvailabilitySnapshot(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] IReadOnlyList<AvailabilityEntrySnapshot> Availabilities);

/// <summary>
/// Grain for employee availability management.
/// Key: "{orgId}:availability:{employeeId}"
/// </summary>
public interface IEmployeeAvailabilityGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid employeeId);
    Task<EmployeeAvailabilitySnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();
    Task<AvailabilityEntrySnapshot> SetAvailabilityAsync(SetAvailabilityCommand command);
    Task UpdateAvailabilityAsync(Guid availabilityId, SetAvailabilityCommand command);
    Task RemoveAvailabilityAsync(Guid availabilityId);
    Task SetWeekAvailabilityAsync(IReadOnlyList<SetAvailabilityCommand> availabilities);
    Task<IReadOnlyList<AvailabilityEntrySnapshot>> GetCurrentAvailabilityAsync();
    Task<bool> IsAvailableOnAsync(int dayOfWeek, TimeSpan time);
}

// ============================================================================
// Shift Swap Request Grain
// ============================================================================

public enum ShiftSwapType
{
    Swap,
    Drop,
    Pickup
}

public enum ShiftSwapStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

[GenerateSerializer]
public record CreateShiftSwapCommand(
    [property: Id(0)] Guid RequestingEmployeeId,
    [property: Id(1)] Guid RequestingShiftId,
    [property: Id(2)] Guid? TargetEmployeeId,
    [property: Id(3)] Guid? TargetShiftId,
    [property: Id(4)] ShiftSwapType Type,
    [property: Id(5)] string? Reason);

[GenerateSerializer]
public record RespondToShiftSwapCommand(
    [property: Id(0)] Guid RespondingUserId,
    [property: Id(1)] string? Notes);

[GenerateSerializer]
public record ShiftSwapSnapshot(
    [property: Id(0)] Guid SwapRequestId,
    [property: Id(1)] Guid RequestingEmployeeId,
    [property: Id(2)] string RequestingEmployeeName,
    [property: Id(3)] Guid RequestingShiftId,
    [property: Id(4)] Guid? TargetEmployeeId,
    [property: Id(5)] string? TargetEmployeeName,
    [property: Id(6)] Guid? TargetShiftId,
    [property: Id(7)] ShiftSwapType Type,
    [property: Id(8)] ShiftSwapStatus Status,
    [property: Id(9)] DateTime RequestedAt,
    [property: Id(10)] DateTime? RespondedAt,
    [property: Id(11)] Guid? ManagerApprovedByUserId,
    [property: Id(12)] string? Reason,
    [property: Id(13)] string? Notes);

/// <summary>
/// Grain for shift swap request management.
/// Key: "{orgId}:shiftswap:{requestId}"
/// </summary>
public interface IShiftSwapGrain : IGrainWithStringKey
{
    Task<ShiftSwapSnapshot> CreateAsync(CreateShiftSwapCommand command);
    Task<ShiftSwapSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();
    Task<ShiftSwapSnapshot> ApproveAsync(RespondToShiftSwapCommand command);
    Task<ShiftSwapSnapshot> RejectAsync(RespondToShiftSwapCommand command);
    Task<ShiftSwapSnapshot> CancelAsync();
    Task<ShiftSwapStatus> GetStatusAsync();
}

// ============================================================================
// Time Off Request Grain
// ============================================================================

public enum TimeOffType
{
    Vacation,
    Sick,
    Personal,
    Unpaid,
    Bereavement,
    JuryDuty,
    Other
}

public enum TimeOffStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

[GenerateSerializer]
public record CreateTimeOffCommand(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] TimeOffType Type,
    [property: Id(2)] DateOnly StartDate,
    [property: Id(3)] DateOnly EndDate,
    [property: Id(4)] string? Reason);

[GenerateSerializer]
public record RespondToTimeOffCommand(
    [property: Id(0)] Guid ReviewedByUserId,
    [property: Id(1)] string? Notes);

[GenerateSerializer]
public record TimeOffBalanceSnapshot(
    [property: Id(0)] TimeOffType Type,
    [property: Id(1)] decimal Accrued,
    [property: Id(2)] decimal Used,
    [property: Id(3)] decimal Pending,
    [property: Id(4)] decimal Available);

[GenerateSerializer]
public record TimeOffSnapshot(
    [property: Id(0)] Guid TimeOffRequestId,
    [property: Id(1)] Guid EmployeeId,
    [property: Id(2)] string EmployeeName,
    [property: Id(3)] TimeOffType Type,
    [property: Id(4)] DateOnly StartDate,
    [property: Id(5)] DateOnly EndDate,
    [property: Id(6)] int TotalDays,
    [property: Id(7)] bool IsPaid,
    [property: Id(8)] TimeOffStatus Status,
    [property: Id(9)] DateTime RequestedAt,
    [property: Id(10)] Guid? ReviewedByUserId,
    [property: Id(11)] DateTime? ReviewedAt,
    [property: Id(12)] string? Reason,
    [property: Id(13)] string? Notes);

/// <summary>
/// Grain for time off request management.
/// Key: "{orgId}:timeoff:{requestId}"
/// </summary>
public interface ITimeOffGrain : IGrainWithStringKey
{
    Task<TimeOffSnapshot> CreateAsync(CreateTimeOffCommand command);
    Task<TimeOffSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();
    Task<TimeOffSnapshot> ApproveAsync(RespondToTimeOffCommand command);
    Task<TimeOffSnapshot> RejectAsync(RespondToTimeOffCommand command);
    Task<TimeOffSnapshot> CancelAsync();
    Task<TimeOffStatus> GetStatusAsync();
}
