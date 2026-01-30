using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Labor.Api.Dtos;

/// <summary>
/// Employee availability details.
/// </summary>
public class AvailabilityDto : HalResource
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public int DayOfWeek { get; set; }
    public string DayOfWeekName { get; set; } = string.Empty;
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsPreferred { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Full availability schedule for an employee.
/// </summary>
public class EmployeeAvailabilityDto : HalResource
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public List<AvailabilityDto> Availabilities { get; set; } = new();
    public List<TimeOffRequestDto> UpcomingTimeOff { get; set; } = new();
}

/// <summary>
/// Request to set availability for a day.
/// </summary>
public record SetAvailabilityRequest(
    int DayOfWeek,
    bool IsAvailable,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    bool IsPreferred = false,
    DateOnly? EffectiveFrom = null,
    DateOnly? EffectiveTo = null,
    string? Notes = null);

/// <summary>
/// Request to update availability.
/// </summary>
public record UpdateAvailabilityRequest(
    bool? IsAvailable = null,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    bool? IsPreferred = null,
    DateOnly? EffectiveTo = null,
    string? Notes = null);

/// <summary>
/// Time off request details.
/// </summary>
public class TimeOffRequestDto : HalResource
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal TotalDays { get; set; }
    public bool IsPaid { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request to create a time off request.
/// </summary>
public record CreateTimeOffRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    string Type = "vacation",
    string? Reason = null);

/// <summary>
/// Request to respond to a time off request.
/// </summary>
public record RespondToTimeOffRequest(
    string? Notes = null);

/// <summary>
/// Time off balance for an employee.
/// </summary>
public class TimeOffBalanceDto : HalResource
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public int Year { get; set; }
    public List<TimeOffTypeBalanceDto> Balances { get; set; } = new();
}

public class TimeOffTypeBalanceDto
{
    public string Type { get; set; } = string.Empty;
    public decimal Accrued { get; set; }
    public decimal Used { get; set; }
    public decimal Pending { get; set; }
    public decimal Available { get; set; }
}
