using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Labor.Api.Dtos;

/// <summary>
/// Full schedule details response.
/// </summary>
public class ScheduleDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate => WeekStartDate.AddDays(6);
    public string Status { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public decimal TotalScheduledHours { get; set; }
    public decimal TotalLaborCost { get; set; }
    public string? Notes { get; set; }
    public int ShiftCount { get; set; }
    public int EmployeeCount { get; set; }
    public List<ShiftDto> Shifts { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Summary schedule for list views.
/// </summary>
public class ScheduleSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate => WeekStartDate.AddDays(6);
    public string Status { get; set; } = string.Empty;
    public decimal TotalScheduledHours { get; set; }
    public decimal TotalLaborCost { get; set; }
    public int ShiftCount { get; set; }
    public int EmployeeCount { get; set; }
}

/// <summary>
/// Request to create a new schedule.
/// </summary>
public record CreateScheduleRequest(
    DateOnly WeekStartDate,
    string? Notes = null);

/// <summary>
/// Request to copy a schedule to a new week.
/// </summary>
public record CopyScheduleRequest(
    DateOnly TargetWeekStartDate);

/// <summary>
/// Labor forecast for a schedule.
/// </summary>
public class LaborForecastDto : HalResource
{
    public Guid ScheduleId { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public decimal TotalScheduledHours { get; set; }
    public decimal TotalLaborCost { get; set; }
    public List<DailyForecastDto> DailyBreakdown { get; set; } = new();
    public List<RoleForecastDto> RoleBreakdown { get; set; } = new();
}

public class DailyForecastDto
{
    public DateOnly Date { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public decimal ScheduledHours { get; set; }
    public decimal LaborCost { get; set; }
    public int ShiftCount { get; set; }
}

public class RoleForecastDto
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public decimal ScheduledHours { get; set; }
    public decimal LaborCost { get; set; }
    public int ShiftCount { get; set; }
}

/// <summary>
/// Coverage analysis for a schedule.
/// </summary>
public class CoverageAnalysisDto : HalResource
{
    public Guid ScheduleId { get; set; }
    public List<DailyCoverageDto> DailyCoverage { get; set; } = new();
    public List<CoverageGapDto> Gaps { get; set; } = new();
}

public class DailyCoverageDto
{
    public DateOnly Date { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public List<HourlyCoverageDto> HourlyCoverage { get; set; } = new();
}

public class HourlyCoverageDto
{
    public TimeOnly Hour { get; set; }
    public int EmployeeCount { get; set; }
    public Dictionary<string, int> ByRole { get; set; } = new();
}

public class CoverageGapDto
{
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}
