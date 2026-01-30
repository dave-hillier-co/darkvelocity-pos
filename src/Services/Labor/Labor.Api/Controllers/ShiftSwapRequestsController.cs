using DarkVelocity.Labor.Api.Data;
using DarkVelocity.Labor.Api.Dtos;
using DarkVelocity.Labor.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Labor.Api.Controllers;

[ApiController]
[Route("api/shift-swap-requests")]
public class ShiftSwapRequestsController : ControllerBase
{
    private readonly LaborDbContext _context;

    public ShiftSwapRequestsController(LaborDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// List shift swap requests.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HalCollection<ShiftSwapRequestDto>>> GetAll(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] Guid? employeeId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var query = _context.ShiftSwapRequests
            .Include(r => r.RequestingEmployee)
            .Include(r => r.TargetEmployee)
            .Include(r => r.RequestingShift)
                .ThenInclude(s => s!.Role)
            .Include(r => r.TargetShift)
                .ThenInclude(s => s!.Role)
            .AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(r => r.TenantId == tenantId.Value);

        if (employeeId.HasValue)
            query = query.Where(r =>
                r.RequestingEmployeeId == employeeId.Value ||
                r.TargetEmployeeId == employeeId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);

        if (!string.IsNullOrEmpty(type))
            query = query.Where(r => r.Type == type);

        var total = await query.CountAsync();

        var requests = await query
            .OrderByDescending(r => r.RequestedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = requests.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/shift-swap-requests/{dto.Id}");
            AddActionLinks(dto);
        }

        return Ok(HalCollection<ShiftSwapRequestDto>.Create(dtos, "/api/shift-swap-requests", total));
    }

    /// <summary>
    /// Get shift swap request by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShiftSwapRequestDto>> GetById(Guid id)
    {
        var request = await _context.ShiftSwapRequests
            .Include(r => r.RequestingEmployee)
            .Include(r => r.TargetEmployee)
            .Include(r => r.RequestingShift)
                .ThenInclude(s => s!.Role)
            .Include(r => r.TargetShift)
                .ThenInclude(s => s!.Role)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
            return NotFound();

        var dto = MapToDto(request);
        dto.AddSelfLink($"/api/shift-swap-requests/{id}");
        AddActionLinks(dto);
        return Ok(dto);
    }

    /// <summary>
    /// Create a shift swap request.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ShiftSwapRequestDto>> Create(
        [FromQuery] Guid tenantId,
        [FromBody] CreateShiftSwapRequest request)
    {
        var shift = await _context.Shifts
            .Include(s => s.Schedule)
            .FirstOrDefaultAsync(s => s.Id == request.RequestingShiftId);

        if (shift == null)
            return BadRequest(new { message = "Shift not found" });

        var swapRequest = new ShiftSwapRequest
        {
            TenantId = tenantId,
            RequestingEmployeeId = shift.EmployeeId,
            RequestingShiftId = request.RequestingShiftId,
            TargetEmployeeId = request.TargetEmployeeId,
            TargetShiftId = request.TargetShiftId,
            Type = request.Type,
            Reason = request.Reason,
            ManagerApprovalRequired = true
        };

        _context.ShiftSwapRequests.Add(swapRequest);
        await _context.SaveChangesAsync();

        // Reload with navigation properties
        swapRequest = await _context.ShiftSwapRequests
            .Include(r => r.RequestingEmployee)
            .Include(r => r.TargetEmployee)
            .Include(r => r.RequestingShift)
                .ThenInclude(s => s!.Role)
            .FirstAsync(r => r.Id == swapRequest.Id);

        var dto = MapToDto(swapRequest);
        dto.AddSelfLink($"/api/shift-swap-requests/{swapRequest.Id}");
        AddActionLinks(dto);
        return CreatedAtAction(nameof(GetById), new { id = swapRequest.Id }, dto);
    }

    /// <summary>
    /// Approve a shift swap request.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ShiftSwapRequestDto>> Approve(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] RespondToSwapRequest request)
    {
        var swapRequest = await _context.ShiftSwapRequests
            .Include(r => r.RequestingEmployee)
            .Include(r => r.TargetEmployee)
            .Include(r => r.RequestingShift)
            .Include(r => r.TargetShift)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (swapRequest == null)
            return NotFound();

        if (swapRequest.Status != "pending")
            return BadRequest(new { message = "Request is not pending" });

        swapRequest.Status = "approved";
        swapRequest.RespondedAt = DateTime.UtcNow;
        swapRequest.ManagerApprovedByUserId = userId;
        if (request.Notes != null) swapRequest.Notes = request.Notes;

        // Execute the swap
        if (swapRequest.Type == "swap" && swapRequest.TargetShift != null)
        {
            // Swap employees between shifts
            var requestingShift = swapRequest.RequestingShift!;
            var targetShift = swapRequest.TargetShift;

            var tempEmployeeId = requestingShift.EmployeeId;
            requestingShift.EmployeeId = targetShift.EmployeeId;
            targetShift.EmployeeId = tempEmployeeId;

            requestingShift.SwapRequestId = id;
            targetShift.SwapRequestId = id;
        }
        else if (swapRequest.Type == "drop")
        {
            // Mark shift as needing coverage (set employee to null or special status)
            swapRequest.RequestingShift!.Status = "cancelled";
            swapRequest.RequestingShift.SwapRequestId = id;
        }
        else if (swapRequest.Type == "pickup" && swapRequest.TargetEmployeeId.HasValue)
        {
            // Assign shift to target employee
            swapRequest.RequestingShift!.EmployeeId = swapRequest.TargetEmployeeId.Value;
            swapRequest.RequestingShift.SwapRequestId = id;
        }

        await _context.SaveChangesAsync();

        // Reload
        swapRequest = await _context.ShiftSwapRequests
            .Include(r => r.RequestingEmployee)
            .Include(r => r.TargetEmployee)
            .Include(r => r.RequestingShift)
                .ThenInclude(s => s!.Role)
            .FirstAsync(r => r.Id == id);

        var dto = MapToDto(swapRequest);
        dto.AddSelfLink($"/api/shift-swap-requests/{id}");
        return Ok(dto);
    }

    /// <summary>
    /// Reject a shift swap request.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ShiftSwapRequestDto>> Reject(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] RespondToSwapRequest request)
    {
        var swapRequest = await _context.ShiftSwapRequests
            .Include(r => r.RequestingEmployee)
            .Include(r => r.TargetEmployee)
            .Include(r => r.RequestingShift)
                .ThenInclude(s => s!.Role)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (swapRequest == null)
            return NotFound();

        if (swapRequest.Status != "pending")
            return BadRequest(new { message = "Request is not pending" });

        swapRequest.Status = "rejected";
        swapRequest.RespondedAt = DateTime.UtcNow;
        swapRequest.ManagerApprovedByUserId = userId;
        if (request.Notes != null) swapRequest.Notes = request.Notes;

        await _context.SaveChangesAsync();

        var dto = MapToDto(swapRequest);
        dto.AddSelfLink($"/api/shift-swap-requests/{id}");
        return Ok(dto);
    }

    private static ShiftSwapRequestDto MapToDto(ShiftSwapRequest request)
    {
        return new ShiftSwapRequestDto
        {
            Id = request.Id,
            TenantId = request.TenantId,
            RequestingEmployeeId = request.RequestingEmployeeId,
            RequestingEmployeeName = request.RequestingEmployee != null
                ? $"{request.RequestingEmployee.FirstName} {request.RequestingEmployee.LastName}"
                : string.Empty,
            RequestingShiftId = request.RequestingShiftId,
            RequestingShift = request.RequestingShift != null ? new ShiftSummaryDto
            {
                Id = request.RequestingShift.Id,
                EmployeeId = request.RequestingShift.EmployeeId,
                RoleId = request.RequestingShift.RoleId,
                RoleName = request.RequestingShift.Role?.Name ?? string.Empty,
                RoleColor = request.RequestingShift.Role?.Color ?? "#3B82F6",
                Date = request.RequestingShift.Date,
                StartTime = request.RequestingShift.StartTime,
                EndTime = request.RequestingShift.EndTime,
                ScheduledHours = request.RequestingShift.ScheduledHours,
                Status = request.RequestingShift.Status
            } : null,
            TargetEmployeeId = request.TargetEmployeeId,
            TargetEmployeeName = request.TargetEmployee != null
                ? $"{request.TargetEmployee.FirstName} {request.TargetEmployee.LastName}"
                : null,
            TargetShiftId = request.TargetShiftId,
            TargetShift = request.TargetShift != null ? new ShiftSummaryDto
            {
                Id = request.TargetShift.Id,
                EmployeeId = request.TargetShift.EmployeeId,
                RoleId = request.TargetShift.RoleId,
                RoleName = request.TargetShift.Role?.Name ?? string.Empty,
                RoleColor = request.TargetShift.Role?.Color ?? "#3B82F6",
                Date = request.TargetShift.Date,
                StartTime = request.TargetShift.StartTime,
                EndTime = request.TargetShift.EndTime,
                ScheduledHours = request.TargetShift.ScheduledHours,
                Status = request.TargetShift.Status
            } : null,
            Type = request.Type,
            Status = request.Status,
            RequestedAt = request.RequestedAt,
            RespondedAt = request.RespondedAt,
            ManagerApprovalRequired = request.ManagerApprovalRequired,
            ManagerApprovedByUserId = request.ManagerApprovedByUserId,
            Reason = request.Reason,
            Notes = request.Notes
        };
    }

    private static void AddActionLinks(ShiftSwapRequestDto dto)
    {
        if (dto.Status == "pending")
        {
            dto.AddLink("approve", $"/api/shift-swap-requests/{dto.Id}/approve");
            dto.AddLink("reject", $"/api/shift-swap-requests/{dto.Id}/reject");
        }
    }
}
