using DarkVelocity.Labor.Api.Data;
using DarkVelocity.Labor.Api.Dtos;
using DarkVelocity.Labor.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Labor.Api.Controllers;

[ApiController]
[Route("api/time-off-requests")]
public class TimeOffController : ControllerBase
{
    private readonly LaborDbContext _context;

    public TimeOffController(LaborDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// List time off requests.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HalCollection<TimeOffRequestDto>>> GetAll(
        [FromQuery] Guid? employeeId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var query = _context.TimeOffRequests
            .Include(t => t.Employee)
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(t => t.EmployeeId == employeeId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        if (!string.IsNullOrEmpty(type))
            query = query.Where(t => t.Type == type);

        if (fromDate.HasValue)
            query = query.Where(t => t.EndDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.StartDate <= toDate.Value);

        var total = await query.CountAsync();

        var requests = await query
            .OrderByDescending(t => t.RequestedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = requests.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/time-off-requests/{dto.Id}");
            AddActionLinks(dto);
        }

        return Ok(HalCollection<TimeOffRequestDto>.Create(dtos, "/api/time-off-requests", total));
    }

    /// <summary>
    /// Get time off request by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TimeOffRequestDto>> GetById(Guid id)
    {
        var request = await _context.TimeOffRequests
            .Include(t => t.Employee)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (request == null)
            return NotFound();

        var dto = MapToDto(request);
        dto.AddSelfLink($"/api/time-off-requests/{id}");
        dto.AddLink("employee", $"/api/employees/{dto.EmployeeId}");
        AddActionLinks(dto);
        return Ok(dto);
    }

    /// <summary>
    /// Submit a time off request.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TimeOffRequestDto>> Create(
        [FromQuery] Guid employeeId,
        [FromBody] CreateTimeOffRequest request)
    {
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee == null)
            return BadRequest(new { message = "Employee not found" });

        // Validate dates
        if (request.EndDate < request.StartDate)
            return BadRequest(new { message = "End date must be after start date" });

        // Check for overlapping requests
        var overlapping = await _context.TimeOffRequests
            .AnyAsync(t =>
                t.EmployeeId == employeeId &&
                t.Status != "rejected" &&
                t.Status != "cancelled" &&
                t.StartDate <= request.EndDate &&
                t.EndDate >= request.StartDate);

        if (overlapping)
            return BadRequest(new { message = "Overlapping time off request exists" });

        var totalDays = (request.EndDate.DayNumber - request.StartDate.DayNumber) + 1;

        var timeOff = new TimeOffRequest
        {
            EmployeeId = employeeId,
            Type = request.Type,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TotalDays = totalDays,
            IsPaid = request.Type != "unpaid",
            Reason = request.Reason,
            Status = "pending"
        };

        _context.TimeOffRequests.Add(timeOff);
        await _context.SaveChangesAsync();

        timeOff.Employee = employee;

        var dto = MapToDto(timeOff);
        dto.AddSelfLink($"/api/time-off-requests/{timeOff.Id}");
        dto.AddLink("employee", $"/api/employees/{employeeId}");
        AddActionLinks(dto);
        return CreatedAtAction(nameof(GetById), new { id = timeOff.Id }, dto);
    }

    /// <summary>
    /// Approve a time off request.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<TimeOffRequestDto>> Approve(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] RespondToTimeOffRequest request)
    {
        var timeOff = await _context.TimeOffRequests
            .Include(t => t.Employee)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (timeOff == null)
            return NotFound();

        if (timeOff.Status != "pending")
            return BadRequest(new { message = "Request is not pending" });

        timeOff.Status = "approved";
        timeOff.ReviewedByUserId = userId;
        timeOff.ReviewedAt = DateTime.UtcNow;
        if (request.Notes != null) timeOff.Notes = request.Notes;

        await _context.SaveChangesAsync();

        var dto = MapToDto(timeOff);
        dto.AddSelfLink($"/api/time-off-requests/{id}");
        dto.AddLink("employee", $"/api/employees/{timeOff.EmployeeId}");
        return Ok(dto);
    }

    /// <summary>
    /// Reject a time off request.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<TimeOffRequestDto>> Reject(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] RespondToTimeOffRequest request)
    {
        var timeOff = await _context.TimeOffRequests
            .Include(t => t.Employee)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (timeOff == null)
            return NotFound();

        if (timeOff.Status != "pending")
            return BadRequest(new { message = "Request is not pending" });

        timeOff.Status = "rejected";
        timeOff.ReviewedByUserId = userId;
        timeOff.ReviewedAt = DateTime.UtcNow;
        if (request.Notes != null) timeOff.Notes = request.Notes;

        await _context.SaveChangesAsync();

        var dto = MapToDto(timeOff);
        dto.AddSelfLink($"/api/time-off-requests/{id}");
        dto.AddLink("employee", $"/api/employees/{timeOff.EmployeeId}");
        return Ok(dto);
    }

    /// <summary>
    /// Cancel a time off request.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<TimeOffRequestDto>> Cancel(Guid id)
    {
        var timeOff = await _context.TimeOffRequests
            .Include(t => t.Employee)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (timeOff == null)
            return NotFound();

        if (timeOff.Status == "rejected" || timeOff.Status == "cancelled")
            return BadRequest(new { message = "Cannot cancel this request" });

        timeOff.Status = "cancelled";

        await _context.SaveChangesAsync();

        var dto = MapToDto(timeOff);
        dto.AddSelfLink($"/api/time-off-requests/{id}");
        dto.AddLink("employee", $"/api/employees/{timeOff.EmployeeId}");
        return Ok(dto);
    }

    /// <summary>
    /// Get time off requests for an employee.
    /// </summary>
    [HttpGet("/api/employees/{employeeId:guid}/time-off")]
    public async Task<ActionResult<HalCollection<TimeOffRequestDto>>> GetByEmployee(
        Guid employeeId,
        [FromQuery] string? status = null,
        [FromQuery] int year = 0)
    {
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee == null)
            return NotFound();

        if (year == 0) year = DateTime.UtcNow.Year;

        var query = _context.TimeOffRequests
            .Include(t => t.Employee)
            .Where(t => t.EmployeeId == employeeId && t.StartDate.Year == year);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        var requests = await query
            .OrderByDescending(t => t.StartDate)
            .ToListAsync();

        var dtos = requests.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/time-off-requests/{dto.Id}");
            AddActionLinks(dto);
        }

        return Ok(HalCollection<TimeOffRequestDto>.Create(dtos, $"/api/employees/{employeeId}/time-off", dtos.Count));
    }

    /// <summary>
    /// Get time off balance for an employee.
    /// </summary>
    [HttpGet("/api/employees/{employeeId:guid}/time-off-balance")]
    public async Task<ActionResult<TimeOffBalanceDto>> GetBalance(
        Guid employeeId,
        [FromQuery] int year = 0)
    {
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee == null)
            return NotFound();

        if (year == 0) year = DateTime.UtcNow.Year;

        var requests = await _context.TimeOffRequests
            .Where(t =>
                t.EmployeeId == employeeId &&
                t.StartDate.Year == year)
            .ToListAsync();

        var balances = new List<TimeOffTypeBalanceDto>();
        var types = new[] { "vacation", "sick", "personal" };

        foreach (var type in types)
        {
            var typeRequests = requests.Where(r => r.Type == type).ToList();
            var used = typeRequests.Where(r => r.Status == "approved").Sum(r => r.TotalDays);
            var pending = typeRequests.Where(r => r.Status == "pending").Sum(r => r.TotalDays);

            // Default accrual (in real implementation, this would come from employee policy)
            var accrued = type switch
            {
                "vacation" => 20m, // 20 days vacation
                "sick" => 10m,     // 10 sick days
                "personal" => 3m,  // 3 personal days
                _ => 0m
            };

            balances.Add(new TimeOffTypeBalanceDto
            {
                Type = type,
                Accrued = accrued,
                Used = used,
                Pending = pending,
                Available = accrued - used - pending
            });
        }

        var dto = new TimeOffBalanceDto
        {
            EmployeeId = employeeId,
            EmployeeName = $"{employee.FirstName} {employee.LastName}",
            Year = year,
            Balances = balances
        };

        dto.AddSelfLink($"/api/employees/{employeeId}/time-off-balance?year={year}");
        dto.AddLink("time-off", $"/api/employees/{employeeId}/time-off");

        return Ok(dto);
    }

    private static TimeOffRequestDto MapToDto(TimeOffRequest request)
    {
        return new TimeOffRequestDto
        {
            Id = request.Id,
            EmployeeId = request.EmployeeId,
            EmployeeName = request.Employee != null
                ? $"{request.Employee.FirstName} {request.Employee.LastName}"
                : string.Empty,
            Type = request.Type,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TotalDays = request.TotalDays,
            IsPaid = request.IsPaid,
            Status = request.Status,
            RequestedAt = request.RequestedAt,
            ReviewedByUserId = request.ReviewedByUserId,
            ReviewedAt = request.ReviewedAt,
            Reason = request.Reason,
            Notes = request.Notes
        };
    }

    private static void AddActionLinks(TimeOffRequestDto dto)
    {
        if (dto.Status == "pending")
        {
            dto.AddLink("approve", $"/api/time-off-requests/{dto.Id}/approve");
            dto.AddLink("reject", $"/api/time-off-requests/{dto.Id}/reject");
            dto.AddLink("cancel", $"/api/time-off-requests/{dto.Id}/cancel");
        }
        else if (dto.Status == "approved" && dto.StartDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            dto.AddLink("cancel", $"/api/time-off-requests/{dto.Id}/cancel");
        }
    }
}
