using DarkVelocity.Labor.Api.Data;
using DarkVelocity.Labor.Api.Dtos;
using DarkVelocity.Labor.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Labor.Api.Controllers;

[ApiController]
[Route("api/time-entries")]
public class TimeEntriesController : ControllerBase
{
    private readonly LaborDbContext _context;

    public TimeEntriesController(LaborDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// List time entries with filtering.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HalCollection<TimeEntrySummaryDto>>> GetAll(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] Guid? employeeId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var query = _context.TimeEntries
            .Include(t => t.Employee)
            .Include(t => t.Role)
            .AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(t => t.TenantId == tenantId.Value);

        if (locationId.HasValue)
            query = query.Where(t => t.LocationId == locationId.Value);

        if (employeeId.HasValue)
            query = query.Where(t => t.EmployeeId == employeeId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        if (fromDate.HasValue)
            query = query.Where(t => DateOnly.FromDateTime(t.ClockInAt) >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => DateOnly.FromDateTime(t.ClockInAt) <= toDate.Value);

        var total = await query.CountAsync();

        var entries = await query
            .OrderByDescending(t => t.ClockInAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = entries.Select(MapToSummaryDto).ToList();

        foreach (var dto in dtos)
            dto.AddSelfLink($"/api/time-entries/{dto.Id}");

        return Ok(HalCollection<TimeEntrySummaryDto>.Create(dtos, "/api/time-entries", total));
    }

    /// <summary>
    /// Get time entry by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TimeEntryDto>> GetById(Guid id)
    {
        var entry = await _context.TimeEntries
            .Include(t => t.Employee)
            .Include(t => t.Role)
            .Include(t => t.Breaks)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (entry == null)
            return NotFound();

        var dto = MapToDto(entry);
        AddLinks(dto);
        AddActionLinks(dto, entry);
        return Ok(dto);
    }

    /// <summary>
    /// Clock in an employee.
    /// </summary>
    [HttpPost("clock-in")]
    public async Task<ActionResult<TimeEntryDto>> ClockIn(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid locationId,
        [FromBody] ClockInRequest request)
    {
        // Validate employee
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId);
        if (employee == null)
            return BadRequest(new { message = "Employee not found" });

        // Check if already clocked in
        var activeEntry = await _context.TimeEntries
            .FirstOrDefaultAsync(t =>
                t.EmployeeId == request.EmployeeId &&
                t.ClockOutAt == null);

        if (activeEntry != null)
            return BadRequest(new { message = "Employee is already clocked in" });

        // Validate role
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId);
        if (role == null)
            return BadRequest(new { message = "Role not found" });

        // Get hourly rate
        var employeeRole = await _context.EmployeeRoles
            .FirstOrDefaultAsync(er => er.EmployeeId == request.EmployeeId && er.RoleId == request.RoleId);
        var hourlyRate = employeeRole?.HourlyRateOverride ?? role.DefaultHourlyRate ?? employee.HourlyRate ?? 0;

        var entry = new TimeEntry
        {
            TenantId = tenantId,
            EmployeeId = request.EmployeeId,
            LocationId = locationId,
            RoleId = request.RoleId,
            ShiftId = request.ShiftId,
            ClockInAt = DateTime.UtcNow,
            ClockInMethod = request.Method,
            HourlyRate = hourlyRate,
            OvertimeRate = employee.OvertimeRate,
            Status = "active",
            Notes = request.Notes
        };

        _context.TimeEntries.Add(entry);
        await _context.SaveChangesAsync();

        entry.Employee = employee;
        entry.Role = role;

        var dto = MapToDto(entry);
        AddLinks(dto);
        AddActionLinks(dto, entry);
        return CreatedAtAction(nameof(GetById), new { id = entry.Id }, dto);
    }

    /// <summary>
    /// Clock out an employee.
    /// </summary>
    [HttpPost("clock-out")]
    public async Task<ActionResult<TimeEntryDto>> ClockOut([FromBody] ClockOutRequest request)
    {
        var entry = await _context.TimeEntries
            .Include(t => t.Employee)
            .Include(t => t.Role)
            .Include(t => t.Breaks)
            .FirstOrDefaultAsync(t => t.Id == request.TimeEntryId);

        if (entry == null)
            return NotFound();

        if (entry.ClockOutAt != null)
            return BadRequest(new { message = "Already clocked out" });

        // End any active breaks
        var activeBreak = entry.Breaks.FirstOrDefault(b => b.EndAt == null);
        if (activeBreak != null)
        {
            activeBreak.EndAt = DateTime.UtcNow;
            activeBreak.DurationMinutes = (int)(activeBreak.EndAt.Value - activeBreak.StartAt).TotalMinutes;
        }

        entry.ClockOutAt = DateTime.UtcNow;
        entry.ClockOutMethod = request.Method;
        entry.Status = "completed";
        if (request.Notes != null) entry.Notes = request.Notes;

        // Calculate hours and pay
        CalculateTimeEntryPay(entry);

        await _context.SaveChangesAsync();

        var dto = MapToDto(entry);
        AddLinks(dto);
        return Ok(dto);
    }

    /// <summary>
    /// Adjust a time entry.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TimeEntryDto>> Adjust(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] AdjustTimeEntryRequest request)
    {
        var entry = await _context.TimeEntries
            .Include(t => t.Employee)
            .Include(t => t.Role)
            .Include(t => t.Breaks)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (entry == null)
            return NotFound();

        if (request.ClockInAt.HasValue) entry.ClockInAt = request.ClockInAt.Value;
        if (request.ClockOutAt.HasValue) entry.ClockOutAt = request.ClockOutAt.Value;
        if (request.BreakMinutes.HasValue) entry.BreakMinutes = request.BreakMinutes.Value;

        entry.AdjustedByUserId = userId;
        entry.AdjustmentReason = request.Reason;
        entry.Status = "adjusted";

        // Recalculate pay if clocked out
        if (entry.ClockOutAt != null)
            CalculateTimeEntryPay(entry);

        await _context.SaveChangesAsync();

        var dto = MapToDto(entry);
        AddLinks(dto);
        return Ok(dto);
    }

    /// <summary>
    /// Approve a time entry.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<TimeEntryDto>> Approve(
        Guid id,
        [FromQuery] Guid userId)
    {
        var entry = await _context.TimeEntries
            .Include(t => t.Employee)
            .Include(t => t.Role)
            .Include(t => t.Breaks)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (entry == null)
            return NotFound();

        if (entry.ClockOutAt == null)
            return BadRequest(new { message = "Cannot approve an active time entry" });

        entry.ApprovedByUserId = userId;
        entry.ApprovedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(entry);
        AddLinks(dto);
        return Ok(dto);
    }

    /// <summary>
    /// Add a break to a time entry.
    /// </summary>
    [HttpPost("{id:guid}/breaks")]
    public async Task<ActionResult<BreakDto>> AddBreak(
        Guid id,
        [FromBody] AddBreakRequest request)
    {
        var entry = await _context.TimeEntries
            .Include(t => t.Breaks)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (entry == null)
            return NotFound();

        if (entry.ClockOutAt != null)
            return BadRequest(new { message = "Cannot add break to completed time entry" });

        // Check if already on break
        var activeBreak = entry.Breaks.FirstOrDefault(b => b.EndAt == null);
        if (activeBreak != null)
            return BadRequest(new { message = "Already on break" });

        var breakEntry = new Entities.Break
        {
            TimeEntryId = id,
            StartAt = request.StartAt ?? DateTime.UtcNow,
            Type = request.Type
        };

        if (request.DurationMinutes.HasValue)
        {
            breakEntry.EndAt = breakEntry.StartAt.AddMinutes(request.DurationMinutes.Value);
            breakEntry.DurationMinutes = request.DurationMinutes.Value;
            breakEntry.AutoDeducted = true;
        }

        _context.Breaks.Add(breakEntry);

        // Update total break minutes
        if (breakEntry.EndAt != null)
            entry.BreakMinutes += breakEntry.DurationMinutes;

        await _context.SaveChangesAsync();

        var dto = new BreakDto
        {
            Id = breakEntry.Id,
            TimeEntryId = id,
            StartAt = breakEntry.StartAt,
            EndAt = breakEntry.EndAt,
            Type = breakEntry.Type,
            DurationMinutes = breakEntry.DurationMinutes,
            AutoDeducted = breakEntry.AutoDeducted
        };

        dto.AddSelfLink($"/api/time-entries/{id}/breaks/{breakEntry.Id}");

        return CreatedAtAction(nameof(GetById), new { id }, dto);
    }

    /// <summary>
    /// End an active break.
    /// </summary>
    [HttpPost("{id:guid}/breaks/end")]
    public async Task<ActionResult<BreakDto>> EndBreak(
        Guid id,
        [FromBody] EndBreakRequest request)
    {
        var entry = await _context.TimeEntries
            .Include(t => t.Breaks)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (entry == null)
            return NotFound();

        var breakEntry = entry.Breaks.FirstOrDefault(b => b.Id == request.BreakId);
        if (breakEntry == null)
            return BadRequest(new { message = "Break not found" });

        if (breakEntry.EndAt != null)
            return BadRequest(new { message = "Break already ended" });

        breakEntry.EndAt = DateTime.UtcNow;
        breakEntry.DurationMinutes = (int)(breakEntry.EndAt.Value - breakEntry.StartAt).TotalMinutes;

        // Update total break minutes
        entry.BreakMinutes += breakEntry.DurationMinutes;

        await _context.SaveChangesAsync();

        var dto = new BreakDto
        {
            Id = breakEntry.Id,
            TimeEntryId = id,
            StartAt = breakEntry.StartAt,
            EndAt = breakEntry.EndAt,
            Type = breakEntry.Type,
            DurationMinutes = breakEntry.DurationMinutes,
            AutoDeducted = breakEntry.AutoDeducted
        };

        dto.AddSelfLink($"/api/time-entries/{id}/breaks/{breakEntry.Id}");

        return Ok(dto);
    }

    /// <summary>
    /// Get current shift status for an employee.
    /// </summary>
    [HttpGet("current/{employeeId:guid}")]
    public async Task<ActionResult<CurrentShiftStatusDto>> GetCurrentShiftStatus(Guid employeeId)
    {
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee == null)
            return NotFound();

        var activeEntry = await _context.TimeEntries
            .Include(t => t.Role)
            .Include(t => t.Breaks)
            .FirstOrDefaultAsync(t => t.EmployeeId == employeeId && t.ClockOutAt == null);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var scheduledShift = await _context.Shifts
            .Include(s => s.Role)
            .Where(s => s.EmployeeId == employeeId && s.Date == today)
            .OrderBy(s => s.StartTime)
            .FirstOrDefaultAsync();

        var dto = new CurrentShiftStatusDto
        {
            EmployeeId = employeeId,
            EmployeeName = $"{employee.FirstName} {employee.LastName}",
            IsClockedIn = activeEntry != null,
            IsOnBreak = activeEntry?.Breaks.Any(b => b.EndAt == null) ?? false,
            CurrentTimeEntry = activeEntry != null ? MapToDto(activeEntry) : null,
            CurrentBreak = activeEntry?.Breaks.FirstOrDefault(b => b.EndAt == null) is { } b
                ? new BreakDto
                {
                    Id = b.Id,
                    TimeEntryId = activeEntry.Id,
                    StartAt = b.StartAt,
                    Type = b.Type
                }
                : null
        };

        dto.AddSelfLink($"/api/time-entries/current/{employeeId}");

        if (!dto.IsClockedIn)
            dto.AddLink("clock-in", "/api/time-entries/clock-in");
        else
        {
            dto.AddLink("clock-out", "/api/time-entries/clock-out");
            if (!dto.IsOnBreak)
                dto.AddLink("start-break", $"/api/time-entries/{activeEntry!.Id}/breaks");
            else
                dto.AddLink("end-break", $"/api/time-entries/{activeEntry!.Id}/breaks/end");
        }

        return Ok(dto);
    }

    private static void CalculateTimeEntryPay(TimeEntry entry)
    {
        if (entry.ClockOutAt == null) return;

        var totalMinutes = (entry.ClockOutAt.Value - entry.ClockInAt).TotalMinutes;
        var breakMinutes = entry.BreakMinutes + entry.Breaks.Sum(b => b.DurationMinutes);
        var workMinutes = totalMinutes - breakMinutes;

        entry.ActualHours = (decimal)(workMinutes / 60);
        entry.BreakMinutes = breakMinutes;

        // Calculate regular and overtime (simple weekly threshold)
        // In real implementation, this would check weekly totals
        var maxRegularHours = 8m; // Daily threshold for simplicity
        entry.RegularHours = Math.Min(entry.ActualHours, maxRegularHours);
        entry.OvertimeHours = Math.Max(0, entry.ActualHours - maxRegularHours);

        var regularPay = entry.RegularHours * entry.HourlyRate;
        var overtimePay = entry.OvertimeHours * entry.HourlyRate * entry.OvertimeRate;
        entry.GrossPay = regularPay + overtimePay;
    }

    private static TimeEntryDto MapToDto(TimeEntry entry)
    {
        return new TimeEntryDto
        {
            Id = entry.Id,
            TenantId = entry.TenantId,
            EmployeeId = entry.EmployeeId,
            EmployeeName = entry.Employee != null
                ? $"{entry.Employee.FirstName} {entry.Employee.LastName}"
                : string.Empty,
            LocationId = entry.LocationId,
            ShiftId = entry.ShiftId,
            RoleId = entry.RoleId,
            RoleName = entry.Role?.Name ?? string.Empty,
            ClockInAt = entry.ClockInAt,
            ClockOutAt = entry.ClockOutAt,
            ClockInMethod = entry.ClockInMethod,
            ClockOutMethod = entry.ClockOutMethod,
            BreakMinutes = entry.BreakMinutes,
            ActualHours = entry.ActualHours,
            RegularHours = entry.RegularHours,
            OvertimeHours = entry.OvertimeHours,
            HourlyRate = entry.HourlyRate,
            OvertimeRate = entry.OvertimeRate,
            GrossPay = entry.GrossPay,
            Status = entry.Status,
            AdjustedByUserId = entry.AdjustedByUserId,
            AdjustmentReason = entry.AdjustmentReason,
            ApprovedByUserId = entry.ApprovedByUserId,
            ApprovedAt = entry.ApprovedAt,
            Notes = entry.Notes,
            Breaks = entry.Breaks.Select(b => new BreakDto
            {
                Id = b.Id,
                TimeEntryId = entry.Id,
                StartAt = b.StartAt,
                EndAt = b.EndAt,
                Type = b.Type,
                DurationMinutes = b.DurationMinutes,
                AutoDeducted = b.AutoDeducted
            }).ToList(),
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt
        };
    }

    private static TimeEntrySummaryDto MapToSummaryDto(TimeEntry entry)
    {
        return new TimeEntrySummaryDto
        {
            Id = entry.Id,
            EmployeeId = entry.EmployeeId,
            EmployeeName = entry.Employee != null
                ? $"{entry.Employee.FirstName} {entry.Employee.LastName}"
                : string.Empty,
            RoleId = entry.RoleId,
            RoleName = entry.Role?.Name ?? string.Empty,
            ClockInAt = entry.ClockInAt,
            ClockOutAt = entry.ClockOutAt,
            ActualHours = entry.ActualHours,
            GrossPay = entry.GrossPay,
            Status = entry.Status
        };
    }

    private static void AddLinks(TimeEntryDto dto)
    {
        dto.AddSelfLink($"/api/time-entries/{dto.Id}");
        dto.AddLink("employee", $"/api/employees/{dto.EmployeeId}");
        dto.AddLink("role", $"/api/roles/{dto.RoleId}");
    }

    private static void AddActionLinks(TimeEntryDto dto, TimeEntry entry)
    {
        if (entry.ClockOutAt == null)
        {
            dto.AddLink("clock-out", "/api/time-entries/clock-out");
            if (!entry.Breaks.Any(b => b.EndAt == null))
                dto.AddLink("start-break", $"/api/time-entries/{dto.Id}/breaks");
            else
                dto.AddLink("end-break", $"/api/time-entries/{dto.Id}/breaks/end");
        }
        else if (entry.ApprovedAt == null)
        {
            dto.AddLink("approve", $"/api/time-entries/{dto.Id}/approve");
        }
    }
}
