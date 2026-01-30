using DarkVelocity.Labor.Api.Data;
using DarkVelocity.Labor.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Labor.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/labor-analytics")]
public class LaborAnalyticsController : ControllerBase
{
    private readonly LaborDbContext _context;

    public LaborAnalyticsController(LaborDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get labor cost report.
    /// </summary>
    [HttpGet("costs")]
    public async Task<ActionResult<LaborCostReportDto>> GetLaborCosts(
        Guid locationId,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var timeEntries = await _context.TimeEntries
            .Include(t => t.Role)
            .Where(t =>
                t.LocationId == locationId &&
                DateOnly.FromDateTime(t.ClockInAt) >= start &&
                DateOnly.FromDateTime(t.ClockInAt) <= end &&
                t.ClockOutAt != null)
            .ToListAsync();

        var dailyBreakdown = timeEntries
            .GroupBy(t => DateOnly.FromDateTime(t.ClockInAt))
            .Select(g => new DailyLaborCostDto
            {
                Date = g.Key,
                DayOfWeek = g.Key.DayOfWeek.ToString(),
                LaborCost = g.Sum(t => t.GrossPay),
                RegularPay = g.Sum(t => t.RegularHours * t.HourlyRate),
                OvertimePay = g.Sum(t => t.OvertimeHours * t.HourlyRate * t.OvertimeRate),
                Tips = 0, // Would come from tip distributions
                Sales = 0, // Would come from Orders service
                LaborCostPercentage = 0 // Would be calculated with sales data
            })
            .OrderBy(d => d.Date)
            .ToList();

        var roleBreakdown = timeEntries
            .GroupBy(t => new { t.RoleId, t.Role?.Name, t.Role?.Department })
            .Select(g => new RoleLaborCostDto
            {
                RoleId = g.Key.RoleId,
                RoleName = g.Key.Name ?? string.Empty,
                Department = g.Key.Department ?? string.Empty,
                TotalHours = g.Sum(t => t.ActualHours),
                TotalCost = g.Sum(t => t.GrossPay),
                AverageHourlyRate = g.Sum(t => t.ActualHours) > 0
                    ? g.Sum(t => t.GrossPay) / g.Sum(t => t.ActualHours)
                    : 0
            })
            .ToList();

        var dto = new LaborCostReportDto
        {
            LocationId = locationId,
            StartDate = start,
            EndDate = end,
            TotalLaborCost = timeEntries.Sum(t => t.GrossPay),
            TotalRegularPay = timeEntries.Sum(t => t.RegularHours * t.HourlyRate),
            TotalOvertimePay = timeEntries.Sum(t => t.OvertimeHours * t.HourlyRate * t.OvertimeRate),
            TotalTips = 0,
            TotalSales = 0,
            LaborCostPercentage = 0,
            DailyBreakdown = dailyBreakdown,
            RoleBreakdown = roleBreakdown
        };

        dto.AddSelfLink($"/api/locations/{locationId}/labor-analytics/costs?startDate={start}&endDate={end}");

        return Ok(dto);
    }

    /// <summary>
    /// Get hours summary report.
    /// </summary>
    [HttpGet("hours")]
    public async Task<ActionResult<HoursSummaryReportDto>> GetHoursSummary(
        Guid locationId,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7);
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var timeEntries = await _context.TimeEntries
            .Include(t => t.Employee)
            .Where(t =>
                t.LocationId == locationId &&
                DateOnly.FromDateTime(t.ClockInAt) >= start &&
                DateOnly.FromDateTime(t.ClockInAt) <= end &&
                t.ClockOutAt != null)
            .ToListAsync();

        var shifts = await _context.Shifts
            .Include(s => s.Schedule)
            .Include(s => s.Employee)
            .Where(s =>
                s.Schedule!.LocationId == locationId &&
                s.Date >= start &&
                s.Date <= end)
            .ToListAsync();

        var employeeBreakdown = timeEntries
            .GroupBy(t => new { t.EmployeeId, t.Employee?.FirstName, t.Employee?.LastName })
            .Select(g =>
            {
                var employeeShifts = shifts.Where(s => s.EmployeeId == g.Key.EmployeeId);
                var scheduledHours = employeeShifts.Sum(s => s.ScheduledHours);
                var actualHours = g.Sum(t => t.ActualHours);

                return new EmployeeHoursSummaryDto
                {
                    EmployeeId = g.Key.EmployeeId,
                    EmployeeName = $"{g.Key.FirstName} {g.Key.LastName}",
                    ScheduledHours = scheduledHours,
                    ActualHours = actualHours,
                    RegularHours = g.Sum(t => t.RegularHours),
                    OvertimeHours = g.Sum(t => t.OvertimeHours),
                    Variance = actualHours - scheduledHours
                };
            })
            .ToList();

        var totalScheduled = shifts.Sum(s => s.ScheduledHours);
        var totalActual = timeEntries.Sum(t => t.ActualHours);

        var dto = new HoursSummaryReportDto
        {
            LocationId = locationId,
            StartDate = start,
            EndDate = end,
            TotalScheduledHours = totalScheduled,
            TotalActualHours = totalActual,
            TotalRegularHours = timeEntries.Sum(t => t.RegularHours),
            TotalOvertimeHours = timeEntries.Sum(t => t.OvertimeHours),
            ScheduleAdherence = totalScheduled > 0 ? (totalActual / totalScheduled) * 100 : 100,
            EmployeeBreakdown = employeeBreakdown
        };

        dto.AddSelfLink($"/api/locations/{locationId}/labor-analytics/hours?startDate={start}&endDate={end}");

        return Ok(dto);
    }

    /// <summary>
    /// Get overtime report.
    /// </summary>
    [HttpGet("overtime")]
    public async Task<ActionResult<OvertimeReportDto>> GetOvertime(
        Guid locationId,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7);
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var timeEntries = await _context.TimeEntries
            .Include(t => t.Employee)
            .Where(t =>
                t.LocationId == locationId &&
                DateOnly.FromDateTime(t.ClockInAt) >= start &&
                DateOnly.FromDateTime(t.ClockInAt) <= end &&
                t.ClockOutAt != null)
            .ToListAsync();

        var employeeBreakdown = timeEntries
            .GroupBy(t => new { t.EmployeeId, t.Employee?.FirstName, t.Employee?.LastName, t.Employee?.MaxHoursPerWeek })
            .Select(g =>
            {
                var totalHours = g.Sum(t => t.ActualHours);
                var regularHours = g.Sum(t => t.RegularHours);
                var overtimeHours = g.Sum(t => t.OvertimeHours);
                var overtimeCost = g.Sum(t => t.OvertimeHours * t.HourlyRate * t.OvertimeRate);

                return new EmployeeOvertimeDto
                {
                    EmployeeId = g.Key.EmployeeId,
                    EmployeeName = $"{g.Key.FirstName} {g.Key.LastName}",
                    TotalHours = totalHours,
                    RegularHours = regularHours,
                    OvertimeHours = overtimeHours,
                    OvertimeCost = overtimeCost,
                    OvertimePercentage = totalHours > 0 ? (overtimeHours / totalHours) * 100 : 0
                };
            })
            .Where(e => e.OvertimeHours > 0)
            .OrderByDescending(e => e.OvertimeHours)
            .ToList();

        // Generate alerts for employees approaching overtime
        var currentWeekStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(int)DateTime.UtcNow.DayOfWeek);
        var currentWeekEntries = await _context.TimeEntries
            .Include(t => t.Employee)
            .Where(t =>
                t.LocationId == locationId &&
                DateOnly.FromDateTime(t.ClockInAt) >= currentWeekStart &&
                t.ClockOutAt != null)
            .ToListAsync();

        var alerts = currentWeekEntries
            .GroupBy(t => new { t.EmployeeId, t.Employee?.FirstName, t.Employee?.LastName, t.Employee?.MaxHoursPerWeek })
            .Select(g =>
            {
                var currentHours = g.Sum(t => t.ActualHours);
                var threshold = g.Key.MaxHoursPerWeek ?? 40;
                var remaining = threshold - currentHours;

                return new OvertimeAlertDto
                {
                    EmployeeId = g.Key.EmployeeId,
                    EmployeeName = $"{g.Key.FirstName} {g.Key.LastName}",
                    CurrentHours = currentHours,
                    ThresholdHours = threshold,
                    RemainingHours = Math.Max(0, remaining),
                    Severity = remaining <= 0 ? "critical" : remaining <= 4 ? "warning" : "info"
                };
            })
            .Where(a => a.RemainingHours <= 8)
            .OrderBy(a => a.RemainingHours)
            .ToList();

        var totalOvertimeHours = timeEntries.Sum(t => t.OvertimeHours);
        var totalHours = timeEntries.Sum(t => t.ActualHours);

        var dto = new OvertimeReportDto
        {
            LocationId = locationId,
            StartDate = start,
            EndDate = end,
            TotalOvertimeHours = totalOvertimeHours,
            TotalOvertimeCost = timeEntries.Sum(t => t.OvertimeHours * t.HourlyRate * t.OvertimeRate),
            OvertimePercentage = totalHours > 0 ? (totalOvertimeHours / totalHours) * 100 : 0,
            EmployeeBreakdown = employeeBreakdown,
            Alerts = alerts
        };

        dto.AddSelfLink($"/api/locations/{locationId}/labor-analytics/overtime?startDate={start}&endDate={end}");

        return Ok(dto);
    }

    /// <summary>
    /// Get schedule adherence report.
    /// </summary>
    [HttpGet("schedule-adherence")]
    public async Task<ActionResult<ScheduleAdherenceReportDto>> GetScheduleAdherence(
        Guid locationId,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7);
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var shifts = await _context.Shifts
            .Include(s => s.Schedule)
            .Include(s => s.Employee)
            .Include(s => s.TimeEntries)
            .Where(s =>
                s.Schedule!.LocationId == locationId &&
                s.Date >= start &&
                s.Date <= end)
            .ToListAsync();

        var employeeBreakdown = shifts
            .GroupBy(s => new { s.EmployeeId, s.Employee?.FirstName, s.Employee?.LastName })
            .Select(g =>
            {
                var totalShifts = g.Count();
                var onTimeShifts = 0;
                var lateShifts = 0;
                var earlyShifts = 0;
                var noShowShifts = 0;
                var totalMinutesLate = 0;

                foreach (var shift in g)
                {
                    var timeEntry = shift.TimeEntries.FirstOrDefault();
                    if (timeEntry == null)
                    {
                        if (shift.Date < DateOnly.FromDateTime(DateTime.UtcNow))
                            noShowShifts++;
                        continue;
                    }

                    var scheduledStart = shift.Date.ToDateTime(shift.StartTime);
                    var actualStart = timeEntry.ClockInAt;
                    var minutesDiff = (actualStart - scheduledStart).TotalMinutes;

                    if (minutesDiff <= 5) // 5 minute grace period
                        onTimeShifts++;
                    else if (minutesDiff > 5)
                    {
                        lateShifts++;
                        totalMinutesLate += (int)minutesDiff;
                    }
                    else
                        earlyShifts++;
                }

                return new EmployeeAdherenceDto
                {
                    EmployeeId = g.Key.EmployeeId,
                    EmployeeName = $"{g.Key.FirstName} {g.Key.LastName}",
                    TotalShifts = totalShifts,
                    OnTimeShifts = onTimeShifts,
                    LateShifts = lateShifts,
                    EarlyShifts = earlyShifts,
                    NoShowShifts = noShowShifts,
                    AdherencePercentage = totalShifts > 0
                        ? ((decimal)onTimeShifts / totalShifts) * 100
                        : 100,
                    AverageMinutesLate = lateShifts > 0
                        ? (decimal)totalMinutesLate / lateShifts
                        : 0
                };
            })
            .ToList();

        var totalShiftsAll = shifts.Count;
        var onTimeAll = employeeBreakdown.Sum(e => e.OnTimeShifts);
        var lateAll = employeeBreakdown.Sum(e => e.LateShifts);
        var earlyAll = employeeBreakdown.Sum(e => e.EarlyShifts);
        var noShowAll = employeeBreakdown.Sum(e => e.NoShowShifts);

        var dto = new ScheduleAdherenceReportDto
        {
            LocationId = locationId,
            StartDate = start,
            EndDate = end,
            OverallAdherence = totalShiftsAll > 0 ? ((decimal)onTimeAll / totalShiftsAll) * 100 : 100,
            OnTimePercentage = totalShiftsAll > 0 ? ((decimal)onTimeAll / totalShiftsAll) * 100 : 0,
            LatePercentage = totalShiftsAll > 0 ? ((decimal)lateAll / totalShiftsAll) * 100 : 0,
            EarlyPercentage = totalShiftsAll > 0 ? ((decimal)earlyAll / totalShiftsAll) * 100 : 0,
            NoShowPercentage = totalShiftsAll > 0 ? ((decimal)noShowAll / totalShiftsAll) * 100 : 0,
            EmployeeBreakdown = employeeBreakdown
        };

        dto.AddSelfLink($"/api/locations/{locationId}/labor-analytics/schedule-adherence?startDate={start}&endDate={end}");

        return Ok(dto);
    }
}
