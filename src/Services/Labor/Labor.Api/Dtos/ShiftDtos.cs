using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Labor.Api.Dtos;

/// <summary>
/// Full shift details response.
/// </summary>
public class ShiftDto : HalResource
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string RoleColor { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int BreakMinutes { get; set; }
    public decimal ScheduledHours { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal LaborCost { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsOvertime { get; set; }
    public Guid? SwapRequestId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Summary shift for list views.
/// </summary>
public class ShiftSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string RoleColor { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public decimal ScheduledHours { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Request to create a new shift.
/// </summary>
public record CreateShiftRequest(
    Guid EmployeeId,
    Guid RoleId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int BreakMinutes = 0,
    string? Notes = null);

/// <summary>
/// Request to update a shift.
/// </summary>
public record UpdateShiftRequest(
    Guid? EmployeeId = null,
    Guid? RoleId = null,
    DateOnly? Date = null,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    int? BreakMinutes = null,
    string? Notes = null);

/// <summary>
/// Shift swap request details.
/// </summary>
public class ShiftSwapRequestDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RequestingEmployeeId { get; set; }
    public string RequestingEmployeeName { get; set; } = string.Empty;
    public Guid RequestingShiftId { get; set; }
    public ShiftSummaryDto? RequestingShift { get; set; }
    public Guid? TargetEmployeeId { get; set; }
    public string? TargetEmployeeName { get; set; }
    public Guid? TargetShiftId { get; set; }
    public ShiftSummaryDto? TargetShift { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public bool ManagerApprovalRequired { get; set; }
    public Guid? ManagerApprovedByUserId { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request to create a shift swap request.
/// </summary>
public record CreateShiftSwapRequest(
    Guid RequestingShiftId,
    string Type,
    Guid? TargetEmployeeId = null,
    Guid? TargetShiftId = null,
    string? Reason = null);

/// <summary>
/// Request to respond to a shift swap.
/// </summary>
public record RespondToSwapRequest(
    string? Notes = null);

/// <summary>
/// Request for auto-generating shifts.
/// </summary>
public record AutoGenerateShiftsRequest(
    Dictionary<string, int>? MinimumCoverageByRole = null,
    TimeOnly? DefaultStartTime = null,
    TimeOnly? DefaultEndTime = null,
    bool RespectAvailability = true,
    bool RespectTimeOff = true);
