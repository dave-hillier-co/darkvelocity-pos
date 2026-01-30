using DarkVelocity.Labor.Api.Data;
using DarkVelocity.Labor.Api.Dtos;
using DarkVelocity.Labor.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Labor.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/schedules")]
public class SchedulesController : ControllerBase
{
    private readonly LaborDbContext _context;

    public SchedulesController(LaborDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// List schedules for a location.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HalCollection<ScheduleSummaryDto>>> GetAll(
        Guid locationId,
        [FromQuery] string? status = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0)
    {
        var query = _context.Schedules
            .Include(s => s.Shifts)
            .Where(s => s.LocationId == locationId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(s => s.Status == status);

        if (fromDate.HasValue)
            query = query.Where(s => s.WeekStartDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(s => s.WeekStartDate <= toDate.Value);

        var total = await query.CountAsync();

        var schedules = await query
            .OrderByDescending(s => s.WeekStartDate)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = schedules.Select(s => MapToSummaryDto(s, locationId)).ToList();

        foreach (var dto in dtos)
            dto.AddSelfLink($"/api/locations/{locationId}/schedules/{dto.Id}");

        return Ok(HalCollection<ScheduleSummaryDto>.Create(dtos, $"/api/locations/{locationId}/schedules", total));
    }

    /// <summary>
    /// Get schedule by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ScheduleDto>> GetById(Guid locationId, Guid id)
    {
        var schedule = await _context.Schedules
            .Include(s => s.Shifts)
                .ThenInclude(sh => sh.Employee)
            .Include(s => s.Shifts)
                .ThenInclude(sh => sh.Role)
            .FirstOrDefaultAsync(s => s.Id == id && s.LocationId == locationId);

        if (schedule == null)
            return NotFound();

        var dto = MapToDto(schedule, locationId);
        AddLinks(dto, locationId);
        AddActionLinks(dto, locationId, schedule);
        return Ok(dto);
    }

    /// <summary>
    /// Create a new schedule.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ScheduleDto>> Create(
        Guid locationId,
        [FromQuery] Guid tenantId,
        [FromBody] CreateScheduleRequest request)
    {
        // Ensure week start is Monday
        var weekStart = request.WeekStartDate;
        if (weekStart.DayOfWeek != DayOfWeek.Monday)
        {
            var daysUntilMonday = ((int)DayOfWeek.Monday - (int)weekStart.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7;
            weekStart = weekStart.AddDays(-(7 - daysUntilMonday));
        }

        // Check if schedule already exists for this week
        var existing = await _context.Schedules
            .AnyAsync(s => s.LocationId == locationId && s.WeekStartDate == weekStart);

        if (existing)
            return BadRequest(new { message = "Schedule already exists for this week" });

        var schedule = new Schedule
        {
            TenantId = tenantId,
            LocationId = locationId,
            WeekStartDate = weekStart,
            Notes = request.Notes,
            Status = "draft"
        };

        _context.Schedules.Add(schedule);
        await _context.SaveChangesAsync();

        var dto = MapToDto(schedule, locationId);
        AddLinks(dto, locationId);
        AddActionLinks(dto, locationId, schedule);
        return CreatedAtAction(nameof(GetById), new { locationId, id = schedule.Id }, dto);
    }

    /// <summary>
    /// Publish a schedule to employees.
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<ScheduleDto>> Publish(
        Guid locationId,
        Guid id,
        [FromQuery] Guid userId)
    {
        var schedule = await _context.Schedules
            .Include(s => s.Shifts)
                .ThenInclude(sh => sh.Employee)
            .Include(s => s.Shifts)
                .ThenInclude(sh => sh.Role)
            .FirstOrDefaultAsync(s => s.Id == id && s.LocationId == locationId);

        if (schedule == null)
            return NotFound();

        if (schedule.Status != "draft")
            return BadRequest(new { message = "Only draft schedules can be published" });

        schedule.Status = "published";
        schedule.PublishedAt = DateTime.UtcNow;
        schedule.PublishedByUserId = userId;

        await _context.SaveChangesAsync();

        var dto = MapToDto(schedule, locationId);
        AddLinks(dto, locationId);
        AddActionLinks(dto, locationId, schedule);
        return Ok(dto);
    }

    /// <summary>
    /// Copy a schedule to a new week.
    /// </summary>
    [HttpPost("{id:guid}/copy")]
    public async Task<ActionResult<ScheduleDto>> Copy(
        Guid locationId,
        Guid id,
        [FromBody] CopyScheduleRequest request)
    {
        var sourceSchedule = await _context.Schedules
            .Include(s => s.Shifts)
            .FirstOrDefaultAsync(s => s.Id == id && s.LocationId == locationId);

        if (sourceSchedule == null)
            return NotFound();

        // Check if target week already has a schedule
        var existing = await _context.Schedules
            .AnyAsync(s => s.LocationId == locationId && s.WeekStartDate == request.TargetWeekStartDate);

        if (existing)
            return BadRequest(new { message = "Schedule already exists for target week" });

        var daysDifference = request.TargetWeekStartDate.DayNumber - sourceSchedule.WeekStartDate.DayNumber;

        var newSchedule = new Schedule
        {
            TenantId = sourceSchedule.TenantId,
            LocationId = locationId,
            WeekStartDate = request.TargetWeekStartDate,
            Status = "draft"
        };

        _context.Schedules.Add(newSchedule);

        // Copy all shifts
        foreach (var shift in sourceSchedule.Shifts)
        {
            var newShift = new Shift
            {
                ScheduleId = newSchedule.Id,
                EmployeeId = shift.EmployeeId,
                RoleId = shift.RoleId,
                Date = shift.Date.AddDays(daysDifference),
                StartTime = shift.StartTime,
                EndTime = shift.EndTime,
                BreakMinutes = shift.BreakMinutes,
                ScheduledHours = shift.ScheduledHours,
                HourlyRate = shift.HourlyRate,
                LaborCost = shift.LaborCost,
                Status = "scheduled"
            };
            _context.Shifts.Add(newShift);
        }

        await _context.SaveChangesAsync();

        // Reload with navigation properties
        newSchedule = await _context.Schedules
            .Include(s => s.Shifts)
                .ThenInclude(sh => sh.Employee)
            .Include(s => s.Shifts)
                .ThenInclude(sh => sh.Role)
            .FirstAsync(s => s.Id == newSchedule.Id);

        // Recalculate totals
        UpdateScheduleTotals(newSchedule);
        await _context.SaveChangesAsync();

        var dto = MapToDto(newSchedule, locationId);
        AddLinks(dto, locationId);
        AddActionLinks(dto, locationId, newSchedule);
        return CreatedAtAction(nameof(GetById), new { locationId, id = newSchedule.Id }, dto);
    }

    /// <summary>
    /// Get labor forecast for a schedule.
    /// </summary>
    [HttpGet("{id:guid}/labor-forecast")]
    public async Task<ActionResult<LaborForecastDto>> GetLaborForecast(Guid locationId, Guid id)
    {
        var schedule = await _context.Schedules
            .Include(s => s.Shifts)
                .ThenInclude(sh => sh.Role)
            .FirstOrDefaultAsync(s => s.Id == id && s.LocationId == locationId);

        if (schedule == null)
            return NotFound();

        var dailyBreakdown = schedule.Shifts
            .GroupBy(s => s.Date)
            .Select(g => new DailyForecastDto
            {
                Date = g.Key,
                DayOfWeek = g.Key.DayOfWeek.ToString(),
                ScheduledHours = g.Sum(s => s.ScheduledHours),
                LaborCost = g.Sum(s => s.LaborCost),
                ShiftCount = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        var roleBreakdown = schedule.Shifts
            .GroupBy(s => new { s.RoleId, s.Role?.Name })
            .Select(g => new RoleForecastDto
            {
                RoleId = g.Key.RoleId,
                RoleName = g.Key.Name ?? string.Empty,
                ScheduledHours = g.Sum(s => s.ScheduledHours),
                LaborCost = g.Sum(s => s.LaborCost),
                ShiftCount = g.Count()
            })
            .ToList();

        var dto = new LaborForecastDto
        {
            ScheduleId = id,
            WeekStartDate = schedule.WeekStartDate,
            TotalScheduledHours = schedule.TotalScheduledHours,
            TotalLaborCost = schedule.TotalLaborCost,
            DailyBreakdown = dailyBreakdown,
            RoleBreakdown = roleBreakdown
        };

        dto.AddSelfLink($"/api/locations/{locationId}/schedules/{id}/labor-forecast");
        dto.AddLink("schedule", $"/api/locations/{locationId}/schedules/{id}");

        return Ok(dto);
    }

    /// <summary>
    /// Get shifts for a schedule.
    /// </summary>
    [HttpGet("{id:guid}/shifts")]
    public async Task<ActionResult<HalCollection<ShiftDto>>> GetShifts(
        Guid locationId,
        Guid id,
        [FromQuery] DateOnly? date = null,
        [FromQuery] Guid? employeeId = null,
        [FromQuery] Guid? roleId = null)
    {
        var schedule = await _context.Schedules
            .FirstOrDefaultAsync(s => s.Id == id && s.LocationId == locationId);

        if (schedule == null)
            return NotFound();

        var query = _context.Shifts
            .Include(s => s.Employee)
            .Include(s => s.Role)
            .Where(s => s.ScheduleId == id);

        if (date.HasValue)
            query = query.Where(s => s.Date == date.Value);

        if (employeeId.HasValue)
            query = query.Where(s => s.EmployeeId == employeeId.Value);

        if (roleId.HasValue)
            query = query.Where(s => s.RoleId == roleId.Value);

        var shifts = await query
            .OrderBy(s => s.Date)
            .ThenBy(s => s.StartTime)
            .ToListAsync();

        var dtos = shifts.Select(s => MapShiftToDto(s, locationId, id)).ToList();

        foreach (var dto in dtos)
            dto.AddSelfLink($"/api/locations/{locationId}/schedules/{id}/shifts/{dto.Id}");

        return Ok(HalCollection<ShiftDto>.Create(dtos, $"/api/locations/{locationId}/schedules/{id}/shifts", dtos.Count));
    }

    /// <summary>
    /// Create a shift in a schedule.
    /// </summary>
    [HttpPost("{id:guid}/shifts")]
    public async Task<ActionResult<ShiftDto>> CreateShift(
        Guid locationId,
        Guid id,
        [FromBody] CreateShiftRequest request)
    {
        var schedule = await _context.Schedules
            .FirstOrDefaultAsync(s => s.Id == id && s.LocationId == locationId);

        if (schedule == null)
            return NotFound();

        if (schedule.Status == "locked")
            return BadRequest(new { message = "Cannot modify locked schedule" });

        // Validate employee exists
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId);
        if (employee == null)
            return BadRequest(new { message = "Employee not found" });

        // Validate role exists
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId);
        if (role == null)
            return BadRequest(new { message = "Role not found" });

        // Get effective hourly rate
        var employeeRole = await _context.EmployeeRoles
            .FirstOrDefaultAsync(er => er.EmployeeId == request.EmployeeId && er.RoleId == request.RoleId);
        var hourlyRate = employeeRole?.HourlyRateOverride ?? role.DefaultHourlyRate ?? employee.HourlyRate ?? 0;

        // Calculate hours
        var startDateTime = request.Date.ToDateTime(request.StartTime);
        var endDateTime = request.Date.ToDateTime(request.EndTime);
        if (request.EndTime < request.StartTime)
            endDateTime = endDateTime.AddDays(1); // Overnight shift

        var totalMinutes = (endDateTime - startDateTime).TotalMinutes - request.BreakMinutes;
        var scheduledHours = (decimal)(totalMinutes / 60);
        var laborCost = scheduledHours * hourlyRate;

        var shift = new Shift
        {
            ScheduleId = id,
            EmployeeId = request.EmployeeId,
            RoleId = request.RoleId,
            Date = request.Date,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            BreakMinutes = request.BreakMinutes,
            ScheduledHours = scheduledHours,
            HourlyRate = hourlyRate,
            LaborCost = laborCost,
            Notes = request.Notes,
            Status = "scheduled"
        };

        _context.Shifts.Add(shift);

        // Update schedule totals
        schedule.TotalScheduledHours += scheduledHours;
        schedule.TotalLaborCost += laborCost;

        await _context.SaveChangesAsync();

        shift.Employee = employee;
        shift.Role = role;

        var dto = MapShiftToDto(shift, locationId, id);
        dto.AddSelfLink($"/api/locations/{locationId}/schedules/{id}/shifts/{shift.Id}");
        return CreatedAtAction(nameof(GetShifts), new { locationId, id }, dto);
    }

    private static void UpdateScheduleTotals(Schedule schedule)
    {
        schedule.TotalScheduledHours = schedule.Shifts.Sum(s => s.ScheduledHours);
        schedule.TotalLaborCost = schedule.Shifts.Sum(s => s.LaborCost);
    }

    private static ScheduleDto MapToDto(Schedule schedule, Guid locationId)
    {
        return new ScheduleDto
        {
            Id = schedule.Id,
            TenantId = schedule.TenantId,
            LocationId = schedule.LocationId,
            WeekStartDate = schedule.WeekStartDate,
            Status = schedule.Status,
            PublishedAt = schedule.PublishedAt,
            PublishedByUserId = schedule.PublishedByUserId,
            TotalScheduledHours = schedule.TotalScheduledHours,
            TotalLaborCost = schedule.TotalLaborCost,
            Notes = schedule.Notes,
            ShiftCount = schedule.Shifts.Count,
            EmployeeCount = schedule.Shifts.Select(s => s.EmployeeId).Distinct().Count(),
            Shifts = schedule.Shifts.Select(s => MapShiftToDto(s, locationId, schedule.Id)).ToList(),
            CreatedAt = schedule.CreatedAt,
            UpdatedAt = schedule.UpdatedAt
        };
    }

    private static ScheduleSummaryDto MapToSummaryDto(Schedule schedule, Guid locationId)
    {
        return new ScheduleSummaryDto
        {
            Id = schedule.Id,
            LocationId = schedule.LocationId,
            WeekStartDate = schedule.WeekStartDate,
            Status = schedule.Status,
            TotalScheduledHours = schedule.TotalScheduledHours,
            TotalLaborCost = schedule.TotalLaborCost,
            ShiftCount = schedule.Shifts.Count,
            EmployeeCount = schedule.Shifts.Select(s => s.EmployeeId).Distinct().Count()
        };
    }

    private static ShiftDto MapShiftToDto(Shift shift, Guid locationId, Guid scheduleId)
    {
        return new ShiftDto
        {
            Id = shift.Id,
            ScheduleId = scheduleId,
            EmployeeId = shift.EmployeeId,
            EmployeeName = shift.Employee != null ? $"{shift.Employee.FirstName} {shift.Employee.LastName}" : string.Empty,
            RoleId = shift.RoleId,
            RoleName = shift.Role?.Name ?? string.Empty,
            RoleColor = shift.Role?.Color ?? "#3B82F6",
            Date = shift.Date,
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            BreakMinutes = shift.BreakMinutes,
            ScheduledHours = shift.ScheduledHours,
            HourlyRate = shift.HourlyRate,
            LaborCost = shift.LaborCost,
            Status = shift.Status,
            Notes = shift.Notes,
            IsOvertime = shift.IsOvertime,
            SwapRequestId = shift.SwapRequestId,
            CreatedAt = shift.CreatedAt,
            UpdatedAt = shift.UpdatedAt
        };
    }

    private static void AddLinks(ScheduleDto dto, Guid locationId)
    {
        dto.AddSelfLink($"/api/locations/{locationId}/schedules/{dto.Id}");
        dto.AddLink("shifts", $"/api/locations/{locationId}/schedules/{dto.Id}/shifts");
        dto.AddLink("labor-forecast", $"/api/locations/{locationId}/schedules/{dto.Id}/labor-forecast");
        dto.AddLink("coverage", $"/api/locations/{locationId}/schedules/{dto.Id}/coverage");
    }

    private static void AddActionLinks(ScheduleDto dto, Guid locationId, Schedule schedule)
    {
        var baseUrl = $"/api/locations/{locationId}/schedules/{dto.Id}";

        switch (schedule.Status)
        {
            case "draft":
                dto.AddLink("publish", $"{baseUrl}/publish");
                dto.AddLink("copy", $"{baseUrl}/copy");
                dto.AddLink("auto-generate", $"{baseUrl}/auto-generate");
                break;
            case "published":
                dto.AddLink("copy", $"{baseUrl}/copy");
                break;
        }
    }
}
