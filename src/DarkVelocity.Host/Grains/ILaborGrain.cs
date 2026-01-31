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

public record CreateRoleCommand(
    string Name,
    Department Department,
    decimal DefaultHourlyRate,
    string Color,
    int SortOrder,
    IReadOnlyList<string> RequiredCertifications);

public record UpdateRoleCommand(
    string? Name,
    Department? Department,
    decimal? DefaultHourlyRate,
    string? Color,
    int? SortOrder,
    bool? IsActive);

public record RoleSnapshot(
    Guid RoleId,
    string Name,
    Department Department,
    decimal DefaultHourlyRate,
    string Color,
    int SortOrder,
    IReadOnlyList<string> RequiredCertifications,
    bool IsActive);

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

public record CreateScheduleCommand(
    Guid LocationId,
    DateTime WeekStartDate,
    string? Notes);

public record PublishScheduleCommand(
    Guid PublishedByUserId);

public record AddShiftCommand(
    Guid ShiftId,
    Guid EmployeeId,
    Guid RoleId,
    DateTime Date,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int BreakMinutes,
    decimal HourlyRate,
    string? Notes);

public record UpdateShiftCommand(
    Guid ShiftId,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    int? BreakMinutes,
    Guid? EmployeeId,
    Guid? RoleId,
    string? Notes);

public record ShiftSnapshot(
    Guid ShiftId,
    Guid EmployeeId,
    Guid RoleId,
    DateTime Date,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int BreakMinutes,
    decimal ScheduledHours,
    decimal HourlyRate,
    decimal LaborCost,
    ShiftStatus Status,
    bool IsOvertime,
    string? Notes);

public enum ShiftStatus
{
    Scheduled,
    Confirmed,
    Started,
    Completed,
    NoShow,
    Cancelled
}

public record ScheduleSnapshot(
    Guid ScheduleId,
    Guid LocationId,
    DateTime WeekStartDate,
    ScheduleStatus Status,
    DateTime? PublishedAt,
    Guid? PublishedByUserId,
    decimal TotalScheduledHours,
    decimal TotalLaborCost,
    IReadOnlyList<ShiftSnapshot> Shifts,
    string? Notes);

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

public record TimeEntryClockInCommand(
    Guid EmployeeId,
    Guid LocationId,
    Guid RoleId,
    Guid? ShiftId,
    ClockMethod Method,
    string? Notes);

public record TimeEntryClockOutCommand(
    ClockMethod Method,
    string? Notes);

public record AddBreakCommand(
    TimeSpan BreakStart,
    TimeSpan? BreakEnd,
    bool IsPaid);

public record AdjustTimeEntryCommand(
    Guid AdjustedByUserId,
    DateTime? ClockInAt,
    DateTime? ClockOutAt,
    int? BreakMinutes,
    string Reason);

public record ApproveTimeEntryCommand(
    Guid ApprovedByUserId);

public record TimeEntrySnapshot(
    Guid TimeEntryId,
    Guid EmployeeId,
    Guid LocationId,
    Guid RoleId,
    Guid? ShiftId,
    DateTime ClockInAt,
    DateTime? ClockOutAt,
    ClockMethod ClockInMethod,
    ClockMethod? ClockOutMethod,
    int BreakMinutes,
    decimal? ActualHours,
    decimal? RegularHours,
    decimal? OvertimeHours,
    decimal HourlyRate,
    decimal OvertimeRate,
    decimal? GrossPay,
    TimeEntryStatus Status,
    Guid? AdjustedByUserId,
    string? AdjustmentReason,
    Guid? ApprovedByUserId,
    DateTime? ApprovedAt,
    string? Notes);

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

public record CreateTipPoolCommand(
    Guid LocationId,
    DateTime BusinessDate,
    string Name,
    TipPoolMethod Method,
    IReadOnlyList<Guid> EligibleRoleIds);

public record AddTipsCommand(
    decimal Amount,
    string Source);

public record DistributeTipsCommand(
    Guid DistributedByUserId);

public record TipDistribution(
    Guid EmployeeId,
    string EmployeeName,
    Guid RoleId,
    decimal HoursWorked,
    decimal Points,
    decimal TipAmount);

public record TipPoolSnapshot(
    Guid TipPoolId,
    Guid LocationId,
    DateTime BusinessDate,
    string Name,
    TipPoolMethod Method,
    decimal TotalTips,
    bool IsDistributed,
    DateTime? DistributedAt,
    Guid? DistributedByUserId,
    IReadOnlyList<TipDistribution> Distributions);

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

public record CreatePayrollPeriodCommand(
    Guid LocationId,
    DateTime PeriodStart,
    DateTime PeriodEnd);

public record PayrollEntrySnapshot(
    Guid EmployeeId,
    string EmployeeName,
    decimal RegularHours,
    decimal OvertimeHours,
    decimal RegularPay,
    decimal OvertimePay,
    decimal TipsReceived,
    decimal GrossPay,
    decimal Deductions,
    decimal NetPay);

public record PayrollPeriodSnapshot(
    Guid PayrollPeriodId,
    Guid LocationId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    PayrollStatus Status,
    decimal TotalRegularHours,
    decimal TotalOvertimeHours,
    decimal TotalRegularPay,
    decimal TotalOvertimePay,
    decimal TotalTips,
    decimal TotalGrossPay,
    decimal TotalDeductions,
    decimal TotalNetPay,
    IReadOnlyList<PayrollEntrySnapshot> Entries);

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

public record SetAvailabilityCommand(
    int DayOfWeek,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    bool IsAvailable,
    bool IsPreferred,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    string? Notes);

public record AvailabilityEntrySnapshot(
    Guid Id,
    int DayOfWeek,
    string DayOfWeekName,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    bool IsAvailable,
    bool IsPreferred,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string? Notes);

public record EmployeeAvailabilitySnapshot(
    Guid EmployeeId,
    IReadOnlyList<AvailabilityEntrySnapshot> Availabilities);

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

public record CreateShiftSwapCommand(
    Guid RequestingEmployeeId,
    Guid RequestingShiftId,
    Guid? TargetEmployeeId,
    Guid? TargetShiftId,
    ShiftSwapType Type,
    string? Reason);

public record RespondToShiftSwapCommand(
    Guid RespondingUserId,
    string? Notes);

public record ShiftSwapSnapshot(
    Guid SwapRequestId,
    Guid RequestingEmployeeId,
    string RequestingEmployeeName,
    Guid RequestingShiftId,
    Guid? TargetEmployeeId,
    string? TargetEmployeeName,
    Guid? TargetShiftId,
    ShiftSwapType Type,
    ShiftSwapStatus Status,
    DateTime RequestedAt,
    DateTime? RespondedAt,
    Guid? ManagerApprovedByUserId,
    string? Reason,
    string? Notes);

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

public record CreateTimeOffCommand(
    Guid EmployeeId,
    TimeOffType Type,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Reason);

public record RespondToTimeOffCommand(
    Guid ReviewedByUserId,
    string? Notes);

public record TimeOffBalanceSnapshot(
    TimeOffType Type,
    decimal Accrued,
    decimal Used,
    decimal Pending,
    decimal Available);

public record TimeOffSnapshot(
    Guid TimeOffRequestId,
    Guid EmployeeId,
    string EmployeeName,
    TimeOffType Type,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalDays,
    bool IsPaid,
    TimeOffStatus Status,
    DateTime RequestedAt,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt,
    string? Reason,
    string? Notes);

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
