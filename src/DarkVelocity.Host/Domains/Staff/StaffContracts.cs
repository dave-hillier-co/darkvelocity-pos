using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.Contracts;

// ============================================================================
// Role Requests
// ============================================================================

public record CreateRoleRequest(
    string Name,
    Department Department,
    decimal DefaultHourlyRate,
    string Color,
    int SortOrder = 0,
    IReadOnlyList<string>? RequiredCertifications = null);

public record UpdateRoleRequest(
    string? Name = null,
    Department? Department = null,
    decimal? DefaultHourlyRate = null,
    string? Color = null,
    int? SortOrder = null,
    bool? IsActive = null);

// ============================================================================
// Schedule Requests
// ============================================================================

public record CreateScheduleRequest(
    DateOnly WeekStartDate,
    string? Notes = null);

public record PublishScheduleRequest(
    Guid PublishedByUserId);

public record AddShiftRequest(
    Guid EmployeeId,
    Guid RoleId,
    DateOnly Date,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int BreakMinutes = 0,
    decimal HourlyRate = 0,
    string? Notes = null);

public record UpdateShiftRequest(
    TimeSpan? StartTime = null,
    TimeSpan? EndTime = null,
    int? BreakMinutes = null,
    Guid? EmployeeId = null,
    Guid? RoleId = null,
    string? Notes = null);

// ============================================================================
// Time Entry Requests
// ============================================================================

public record CreateTimeEntryRequest(
    Guid EmployeeId,
    Guid SiteId,
    Guid RoleId,
    Guid? ShiftId = null,
    ClockMethod Method = ClockMethod.Pin,
    string? Notes = null);

public record ClockOutTimeEntryRequest(
    ClockMethod Method = ClockMethod.Pin,
    string? Notes = null);

public record AddBreakRequest(
    TimeSpan BreakStart,
    TimeSpan? BreakEnd = null,
    bool IsPaid = false);

public record AdjustTimeEntryRequest(
    Guid AdjustedByUserId,
    DateTime? ClockInAt = null,
    DateTime? ClockOutAt = null,
    int? BreakMinutes = null,
    string Reason = "");

public record ApproveTimeEntryRequest(
    Guid ApprovedByUserId);

// ============================================================================
// Availability Requests
// ============================================================================

public record SetAvailabilityRequest(
    int DayOfWeek,
    TimeSpan? StartTime = null,
    TimeSpan? EndTime = null,
    bool IsAvailable = true,
    bool IsPreferred = false,
    DateOnly? EffectiveFrom = null,
    DateOnly? EffectiveTo = null,
    string? Notes = null);

public record SetWeekAvailabilityRequest(
    IReadOnlyList<SetAvailabilityRequest> Availabilities);

// ============================================================================
// Time Off Requests
// ============================================================================

public record CreateTimeOffRequest(
    Guid EmployeeId,
    TimeOffType Type,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Reason = null);

public record RespondToTimeOffRequest(
    Guid ReviewedByUserId,
    string? Notes = null);

// ============================================================================
// Shift Swap Requests
// ============================================================================

public record CreateShiftSwapRequest(
    Guid RequestingEmployeeId,
    Guid RequestingShiftId,
    Guid? TargetEmployeeId = null,
    Guid? TargetShiftId = null,
    ShiftSwapType Type = ShiftSwapType.Swap,
    string? Reason = null);

public record RespondToShiftSwapRequest(
    Guid RespondingUserId,
    string? Notes = null);

// ============================================================================
// Tip Pool Requests
// ============================================================================

public record CreateTipPoolRequest(
    DateOnly BusinessDate,
    string Name,
    TipPoolMethod Method = TipPoolMethod.ByHoursWorked,
    IReadOnlyList<Guid>? EligibleRoleIds = null);

public record AddTipsRequest(
    decimal Amount,
    string Source);

public record AddParticipantRequest(
    Guid EmployeeId,
    decimal HoursWorked,
    decimal Points = 0);

public record DistributeTipsRequest(
    Guid DistributedByUserId);

// ============================================================================
// Payroll Requests
// ============================================================================

public record CreatePayrollPeriodRequest(
    DateOnly PeriodStart,
    DateOnly PeriodEnd);

public record ApprovePayrollRequest(
    Guid ApprovedByUserId);
