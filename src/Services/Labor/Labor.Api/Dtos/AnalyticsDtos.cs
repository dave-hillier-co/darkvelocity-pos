using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Labor.Api.Dtos;

/// <summary>
/// Labor cost report.
/// </summary>
public class LaborCostReportDto : HalResource
{
    public Guid LocationId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal TotalLaborCost { get; set; }
    public decimal TotalRegularPay { get; set; }
    public decimal TotalOvertimePay { get; set; }
    public decimal TotalTips { get; set; }
    public decimal TotalSales { get; set; }
    public decimal LaborCostPercentage { get; set; }
    public List<DailyLaborCostDto> DailyBreakdown { get; set; } = new();
    public List<RoleLaborCostDto> RoleBreakdown { get; set; } = new();
}

public class DailyLaborCostDto
{
    public DateOnly Date { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public decimal LaborCost { get; set; }
    public decimal RegularPay { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal Tips { get; set; }
    public decimal Sales { get; set; }
    public decimal LaborCostPercentage { get; set; }
}

public class RoleLaborCostDto
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal TotalHours { get; set; }
    public decimal TotalCost { get; set; }
    public decimal AverageHourlyRate { get; set; }
}

/// <summary>
/// Hours summary report.
/// </summary>
public class HoursSummaryReportDto : HalResource
{
    public Guid LocationId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal TotalScheduledHours { get; set; }
    public decimal TotalActualHours { get; set; }
    public decimal TotalRegularHours { get; set; }
    public decimal TotalOvertimeHours { get; set; }
    public decimal ScheduleAdherence { get; set; }
    public List<EmployeeHoursSummaryDto> EmployeeBreakdown { get; set; } = new();
}

public class EmployeeHoursSummaryDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public decimal ScheduledHours { get; set; }
    public decimal ActualHours { get; set; }
    public decimal RegularHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal Variance { get; set; }
}

/// <summary>
/// Overtime report.
/// </summary>
public class OvertimeReportDto : HalResource
{
    public Guid LocationId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal TotalOvertimeHours { get; set; }
    public decimal TotalOvertimeCost { get; set; }
    public decimal OvertimePercentage { get; set; }
    public List<EmployeeOvertimeDto> EmployeeBreakdown { get; set; } = new();
    public List<OvertimeAlertDto> Alerts { get; set; } = new();
}

public class EmployeeOvertimeDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public decimal TotalHours { get; set; }
    public decimal RegularHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal OvertimeCost { get; set; }
    public decimal OvertimePercentage { get; set; }
}

public class OvertimeAlertDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public decimal CurrentHours { get; set; }
    public decimal ThresholdHours { get; set; }
    public decimal RemainingHours { get; set; }
    public string Severity { get; set; } = string.Empty;
}

/// <summary>
/// Labor vs sales report.
/// </summary>
public class LaborVsSalesReportDto : HalResource
{
    public Guid LocationId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalLaborCost { get; set; }
    public decimal LaborCostPercentage { get; set; }
    public decimal TargetLaborPercentage { get; set; }
    public decimal Variance { get; set; }
    public List<DailyLaborVsSalesDto> DailyBreakdown { get; set; } = new();
}

public class DailyLaborVsSalesDto
{
    public DateOnly Date { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public decimal Sales { get; set; }
    public decimal LaborCost { get; set; }
    public decimal LaborPercentage { get; set; }
    public decimal TargetPercentage { get; set; }
    public decimal Variance { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Schedule adherence report.
/// </summary>
public class ScheduleAdherenceReportDto : HalResource
{
    public Guid LocationId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal OverallAdherence { get; set; }
    public decimal OnTimePercentage { get; set; }
    public decimal LatePercentage { get; set; }
    public decimal EarlyPercentage { get; set; }
    public decimal NoShowPercentage { get; set; }
    public List<EmployeeAdherenceDto> EmployeeBreakdown { get; set; } = new();
}

public class EmployeeAdherenceDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public int TotalShifts { get; set; }
    public int OnTimeShifts { get; set; }
    public int LateShifts { get; set; }
    public int EarlyShifts { get; set; }
    public int NoShowShifts { get; set; }
    public decimal AdherencePercentage { get; set; }
    public decimal AverageMinutesLate { get; set; }
}
