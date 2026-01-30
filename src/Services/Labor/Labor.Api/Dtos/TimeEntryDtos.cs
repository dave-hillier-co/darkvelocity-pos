using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Labor.Api.Dtos;

/// <summary>
/// Full time entry details response.
/// </summary>
public class TimeEntryDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public Guid? ShiftId { get; set; }
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public DateTime ClockInAt { get; set; }
    public DateTime? ClockOutAt { get; set; }
    public string ClockInMethod { get; set; } = string.Empty;
    public string? ClockOutMethod { get; set; }
    public int BreakMinutes { get; set; }
    public decimal ActualHours { get; set; }
    public decimal RegularHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal OvertimeRate { get; set; }
    public decimal GrossPay { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? AdjustedByUserId { get; set; }
    public string? AdjustmentReason { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Notes { get; set; }
    public List<BreakDto> Breaks { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Summary time entry for list views.
/// </summary>
public class TimeEntrySummaryDto : HalResource
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public DateTime ClockInAt { get; set; }
    public DateTime? ClockOutAt { get; set; }
    public decimal ActualHours { get; set; }
    public decimal GrossPay { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsClockedIn => ClockOutAt == null;
}

/// <summary>
/// Break details.
/// </summary>
public class BreakDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TimeEntryId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public string Type { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public bool AutoDeducted { get; set; }
    public bool IsOnBreak => EndAt == null;
}

/// <summary>
/// Request to clock in.
/// </summary>
public record ClockInRequest(
    Guid EmployeeId,
    Guid RoleId,
    string Method = "pin",
    Guid? ShiftId = null,
    string? Notes = null);

/// <summary>
/// Request to clock out.
/// </summary>
public record ClockOutRequest(
    Guid TimeEntryId,
    string Method = "pin",
    string? Notes = null);

/// <summary>
/// Request to adjust a time entry.
/// </summary>
public record AdjustTimeEntryRequest(
    DateTime? ClockInAt = null,
    DateTime? ClockOutAt = null,
    int? BreakMinutes = null,
    string? Reason = null);

/// <summary>
/// Request to add a break.
/// </summary>
public record AddBreakRequest(
    string Type = "unpaid",
    DateTime? StartAt = null,
    int? DurationMinutes = null);

/// <summary>
/// Request to end a break.
/// </summary>
public record EndBreakRequest(
    Guid BreakId);

/// <summary>
/// Current shift status for an employee.
/// </summary>
public class CurrentShiftStatusDto : HalResource
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public bool IsClockedIn { get; set; }
    public bool IsOnBreak { get; set; }
    public TimeEntryDto? CurrentTimeEntry { get; set; }
    public BreakDto? CurrentBreak { get; set; }
    public ShiftDto? ScheduledShift { get; set; }
}
