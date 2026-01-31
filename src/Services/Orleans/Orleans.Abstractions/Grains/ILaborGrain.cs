namespace DarkVelocity.Orleans.Abstractions.Grains;

// ============================================================================
// Employee Grain
// ============================================================================

public enum EmploymentStatus
{
    Active,
    OnLeave,
    Terminated
}

public enum EmploymentType
{
    FullTime,
    PartTime,
    Casual,
    Contractor
}

public enum Department
{
    FrontOfHouse,
    BackOfHouse,
    Management
}

public record CreateEmployeeCommand(
    Guid UserId,
    Guid LocationId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    DateTime? DateOfBirth,
    DateTime HireDate,
    EmploymentType EmploymentType,
    decimal HourlyRate,
    decimal? SalaryAmount,
    decimal OvertimeRate,
    int MaxHoursPerWeek,
    int MinHoursPerWeek,
    Guid DefaultRoleId);

public record UpdateEmployeeCommand(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    EmploymentStatus? Status,
    EmploymentType? EmploymentType,
    decimal? HourlyRate,
    decimal? SalaryAmount,
    decimal? OvertimeRate,
    int? MaxHoursPerWeek,
    int? MinHoursPerWeek);

public record TerminateEmployeeCommand(
    DateTime TerminationDate,
    string Reason);

public record AssignRoleCommand(
    Guid RoleId,
    decimal? HourlyRateOverride,
    bool IsPrimary);

public record EmployeeSnapshot(
    Guid EmployeeId,
    Guid UserId,
    Guid LocationId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    DateTime? DateOfBirth,
    DateTime HireDate,
    DateTime? TerminationDate,
    EmploymentStatus Status,
    EmploymentType EmploymentType,
    decimal HourlyRate,
    decimal? SalaryAmount,
    decimal OvertimeRate,
    int MaxHoursPerWeek,
    int MinHoursPerWeek,
    Guid DefaultRoleId,
    IReadOnlyList<EmployeeRoleAssignment> Roles);

public record EmployeeRoleAssignment(
    Guid RoleId,
    string RoleName,
    decimal? HourlyRateOverride,
    bool IsPrimary,
    DateTime? CertifiedAt);

/// <summary>
/// Grain for employee management.
/// Key: "{orgId}:employee:{employeeId}"
/// </summary>
public interface IEmployeeGrain : IGrainWithStringKey
{
    Task<EmployeeSnapshot> CreateAsync(CreateEmployeeCommand command);
    Task<EmployeeSnapshot> UpdateAsync(UpdateEmployeeCommand command);
    Task TerminateAsync(TerminateEmployeeCommand command);
    Task AssignRoleAsync(AssignRoleCommand command);
    Task RemoveRoleAsync(Guid roleId);
    Task<EmployeeSnapshot> GetSnapshotAsync();
    Task<bool> IsActiveAsync();
    Task<decimal> GetEffectiveHourlyRateAsync(Guid roleId);
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

public record ClockInCommand(
    Guid EmployeeId,
    Guid LocationId,
    Guid RoleId,
    Guid? ShiftId,
    ClockMethod Method,
    string? Notes);

public record ClockOutCommand(
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
    Task<TimeEntrySnapshot> ClockInAsync(ClockInCommand command);
    Task<TimeEntrySnapshot> ClockOutAsync(ClockOutCommand command);
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
