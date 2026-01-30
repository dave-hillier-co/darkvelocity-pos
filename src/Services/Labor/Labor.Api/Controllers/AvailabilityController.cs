using DarkVelocity.Labor.Api.Data;
using DarkVelocity.Labor.Api.Dtos;
using DarkVelocity.Labor.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Labor.Api.Controllers;

[ApiController]
[Route("api/employees/{employeeId:guid}/availability")]
public class AvailabilityController : ControllerBase
{
    private readonly LaborDbContext _context;
    private static readonly string[] DayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

    public AvailabilityController(LaborDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get availability for an employee.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<EmployeeAvailabilityDto>> GetAvailability(Guid employeeId)
    {
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee == null)
            return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var availabilities = await _context.Availabilities
            .Where(a =>
                a.EmployeeId == employeeId &&
                a.EffectiveFrom <= today &&
                (a.EffectiveTo == null || a.EffectiveTo >= today))
            .OrderBy(a => a.DayOfWeek)
            .ToListAsync();

        var upcomingTimeOff = await _context.TimeOffRequests
            .Where(t =>
                t.EmployeeId == employeeId &&
                t.Status == "approved" &&
                t.EndDate >= today)
            .OrderBy(t => t.StartDate)
            .Take(5)
            .ToListAsync();

        var dto = new EmployeeAvailabilityDto
        {
            EmployeeId = employeeId,
            EmployeeName = $"{employee.FirstName} {employee.LastName}",
            Availabilities = availabilities.Select(a => new AvailabilityDto
            {
                Id = a.Id,
                EmployeeId = a.EmployeeId,
                DayOfWeek = a.DayOfWeek,
                DayOfWeekName = DayNames[a.DayOfWeek],
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                IsAvailable = a.IsAvailable,
                IsPreferred = a.IsPreferred,
                EffectiveFrom = a.EffectiveFrom,
                EffectiveTo = a.EffectiveTo,
                Notes = a.Notes
            }).ToList(),
            UpcomingTimeOff = upcomingTimeOff.Select(t => new TimeOffRequestDto
            {
                Id = t.Id,
                EmployeeId = t.EmployeeId,
                Type = t.Type,
                StartDate = t.StartDate,
                EndDate = t.EndDate,
                TotalDays = t.TotalDays,
                IsPaid = t.IsPaid,
                Status = t.Status
            }).ToList()
        };

        dto.AddSelfLink($"/api/employees/{employeeId}/availability");
        dto.AddLink("employee", $"/api/employees/{employeeId}");
        dto.AddLink("time-off", $"/api/employees/{employeeId}/time-off");

        return Ok(dto);
    }

    /// <summary>
    /// Set availability for a specific day.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AvailabilityDto>> SetAvailability(
        Guid employeeId,
        [FromBody] SetAvailabilityRequest request)
    {
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee == null)
            return NotFound();

        if (request.DayOfWeek < 0 || request.DayOfWeek > 6)
            return BadRequest(new { message = "Day of week must be 0-6" });

        var effectiveFrom = request.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // Check for existing availability for this day
        var existing = await _context.Availabilities
            .FirstOrDefaultAsync(a =>
                a.EmployeeId == employeeId &&
                a.DayOfWeek == request.DayOfWeek &&
                a.EffectiveFrom <= effectiveFrom &&
                (a.EffectiveTo == null || a.EffectiveTo >= effectiveFrom));

        if (existing != null)
        {
            // End existing availability the day before
            existing.EffectiveTo = effectiveFrom.AddDays(-1);
        }

        var availability = new Availability
        {
            EmployeeId = employeeId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsAvailable = request.IsAvailable,
            IsPreferred = request.IsPreferred,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Notes = request.Notes
        };

        _context.Availabilities.Add(availability);
        await _context.SaveChangesAsync();

        var dto = new AvailabilityDto
        {
            Id = availability.Id,
            EmployeeId = availability.EmployeeId,
            DayOfWeek = availability.DayOfWeek,
            DayOfWeekName = DayNames[availability.DayOfWeek],
            StartTime = availability.StartTime,
            EndTime = availability.EndTime,
            IsAvailable = availability.IsAvailable,
            IsPreferred = availability.IsPreferred,
            EffectiveFrom = availability.EffectiveFrom,
            EffectiveTo = availability.EffectiveTo,
            Notes = availability.Notes
        };

        dto.AddSelfLink($"/api/employees/{employeeId}/availability/{availability.Id}");

        return CreatedAtAction(nameof(GetAvailability), new { employeeId }, dto);
    }

    /// <summary>
    /// Update availability.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AvailabilityDto>> UpdateAvailability(
        Guid employeeId,
        Guid id,
        [FromBody] UpdateAvailabilityRequest request)
    {
        var availability = await _context.Availabilities
            .FirstOrDefaultAsync(a => a.Id == id && a.EmployeeId == employeeId);

        if (availability == null)
            return NotFound();

        if (request.IsAvailable.HasValue) availability.IsAvailable = request.IsAvailable.Value;
        if (request.StartTime.HasValue) availability.StartTime = request.StartTime;
        if (request.EndTime.HasValue) availability.EndTime = request.EndTime;
        if (request.IsPreferred.HasValue) availability.IsPreferred = request.IsPreferred.Value;
        if (request.EffectiveTo.HasValue) availability.EffectiveTo = request.EffectiveTo;
        if (request.Notes != null) availability.Notes = request.Notes;

        await _context.SaveChangesAsync();

        var dto = new AvailabilityDto
        {
            Id = availability.Id,
            EmployeeId = availability.EmployeeId,
            DayOfWeek = availability.DayOfWeek,
            DayOfWeekName = DayNames[availability.DayOfWeek],
            StartTime = availability.StartTime,
            EndTime = availability.EndTime,
            IsAvailable = availability.IsAvailable,
            IsPreferred = availability.IsPreferred,
            EffectiveFrom = availability.EffectiveFrom,
            EffectiveTo = availability.EffectiveTo,
            Notes = availability.Notes
        };

        dto.AddSelfLink($"/api/employees/{employeeId}/availability/{availability.Id}");

        return Ok(dto);
    }

    /// <summary>
    /// Delete availability.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAvailability(Guid employeeId, Guid id)
    {
        var availability = await _context.Availabilities
            .FirstOrDefaultAsync(a => a.Id == id && a.EmployeeId == employeeId);

        if (availability == null)
            return NotFound();

        _context.Availabilities.Remove(availability);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Set full week availability in one call.
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<EmployeeAvailabilityDto>> SetWeekAvailability(
        Guid employeeId,
        [FromBody] List<SetAvailabilityRequest> requests)
    {
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee == null)
            return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // End all existing availabilities
        var existingAvailabilities = await _context.Availabilities
            .Where(a =>
                a.EmployeeId == employeeId &&
                (a.EffectiveTo == null || a.EffectiveTo >= today))
            .ToListAsync();

        foreach (var existing in existingAvailabilities)
        {
            existing.EffectiveTo = today.AddDays(-1);
        }

        // Create new availabilities
        foreach (var request in requests)
        {
            if (request.DayOfWeek < 0 || request.DayOfWeek > 6)
                continue;

            var availability = new Availability
            {
                EmployeeId = employeeId,
                DayOfWeek = request.DayOfWeek,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                IsAvailable = request.IsAvailable,
                IsPreferred = request.IsPreferred,
                EffectiveFrom = request.EffectiveFrom ?? today,
                EffectiveTo = request.EffectiveTo,
                Notes = request.Notes
            };

            _context.Availabilities.Add(availability);
        }

        await _context.SaveChangesAsync();

        // Return updated availability
        return await GetAvailability(employeeId);
    }
}
