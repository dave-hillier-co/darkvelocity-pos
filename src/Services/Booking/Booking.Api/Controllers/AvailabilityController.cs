using DarkVelocity.Booking.Api.Data;
using DarkVelocity.Booking.Api.Dtos;
using DarkVelocity.Booking.Api.Entities;
using DarkVelocity.Booking.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Booking.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/availability")]
public class AvailabilityController : ControllerBase
{
    private readonly BookingDbContext _context;
    private readonly IAvailabilityService _availabilityService;

    public AvailabilityController(
        BookingDbContext context,
        IAvailabilityService availabilityService)
    {
        _context = context;
        _availabilityService = availabilityService;
    }

    [HttpGet]
    public async Task<ActionResult<AvailabilityResponseDto>> CheckAvailability(
        Guid locationId,
        [FromQuery] DateOnly date,
        [FromQuery] int partySize,
        [FromQuery] Guid? floorPlanId = null)
    {
        var slots = await _availabilityService.GetAvailableSlotsAsync(
            locationId, date, partySize, floorPlanId);

        var response = new AvailabilityResponseDto
        {
            Date = date,
            PartySize = partySize,
            FloorPlanId = floorPlanId,
            Slots = slots.Select(s => new AvailableSlotDto
            {
                Time = s.Time,
                AvailableTableCount = s.AvailableTableCount,
                SlotName = s.SlotName
            }).ToList()
        };

        response.AddSelfLink($"/api/locations/{locationId}/availability?date={date:yyyy-MM-dd}&partySize={partySize}");

        return Ok(response);
    }

    [HttpGet("tables")]
    public async Task<ActionResult<HalCollection<AvailableTableDto>>> GetAvailableTables(
        Guid locationId,
        [FromQuery] DateOnly date,
        [FromQuery] TimeOnly startTime,
        [FromQuery] int durationMinutes,
        [FromQuery] int partySize,
        [FromQuery] Guid? floorPlanId = null)
    {
        var tables = await _availabilityService.GetAvailableTablesAsync(
            locationId, date, startTime, durationMinutes, partySize, floorPlanId);

        var dtos = tables.Select(t => new AvailableTableDto
        {
            Id = t.Id,
            TableNumber = t.TableNumber,
            Name = t.Name,
            MinCapacity = t.MinCapacity,
            MaxCapacity = t.MaxCapacity,
            FloorPlanName = t.FloorPlan?.Name,
            Shape = t.Shape
        }).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/locations/{locationId}/tables/{dto.Id}");
        }

        return Ok(HalCollection<AvailableTableDto>.Create(
            dtos,
            $"/api/locations/{locationId}/availability/tables",
            dtos.Count
        ));
    }

    [HttpGet("deposit-requirement")]
    public async Task<ActionResult<DepositRequirementDto>> GetDepositRequirement(
        Guid locationId,
        [FromQuery] int partySize,
        [FromQuery] DateOnly date,
        [FromQuery] TimeOnly time)
    {
        var policy = await _availabilityService.GetApplicableDepositPolicyAsync(
            locationId, partySize, date, time);

        if (policy == null)
        {
            return Ok(new DepositRequirementDto
            {
                DepositRequired = false
            });
        }

        var amount = await _availabilityService.CalculateDepositAmountAsync(policy, partySize);

        var response = new DepositRequirementDto
        {
            DepositRequired = true,
            Amount = amount,
            CurrencyCode = policy.CurrencyCode,
            PolicyName = policy.Name,
            RefundableUntilHours = policy.RefundableUntilHours,
            RefundPercentage = policy.RefundPercentage,
            ForfeitsOnNoShow = policy.ForfeitsOnNoShow
        };

        response.AddSelfLink($"/api/locations/{locationId}/availability/deposit-requirement?partySize={partySize}&date={date:yyyy-MM-dd}&time={time:HH:mm}");

        return Ok(response);
    }

    [HttpGet("calendar")]
    public async Task<ActionResult<object>> GetCalendarAvailability(
        Guid locationId,
        [FromQuery] int partySize,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly? toDate = null)
    {
        var endDate = toDate ?? fromDate.AddDays(30);

        var results = new List<object>();
        var currentDate = fromDate;

        while (currentDate <= endDate)
        {
            var slots = await _availabilityService.GetAvailableSlotsAsync(
                locationId, currentDate, partySize);

            results.Add(new
            {
                Date = currentDate,
                HasAvailability = slots.Any(),
                SlotCount = slots.Count,
                FirstAvailableTime = slots.FirstOrDefault()?.Time,
                LastAvailableTime = slots.LastOrDefault()?.Time
            });

            currentDate = currentDate.AddDays(1);
        }

        return Ok(new
        {
            PartySize = partySize,
            FromDate = fromDate,
            ToDate = endDate,
            Dates = results
        });
    }
}

[ApiController]
[Route("api/locations/{locationId:guid}/time-slots")]
public class TimeSlotsController : ControllerBase
{
    private readonly BookingDbContext _context;

    public TimeSlotsController(BookingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<TimeSlotDto>>> GetAll(
        Guid locationId,
        [FromQuery] int? dayOfWeek = null,
        [FromQuery] bool? isActive = null)
    {
        var query = _context.TimeSlots
            .Where(t => t.LocationId == locationId);

        if (dayOfWeek.HasValue)
            query = query.Where(t => t.DayOfWeek == dayOfWeek.Value);

        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        var slots = await query
            .OrderBy(t => t.DayOfWeek)
            .ThenBy(t => t.StartTime)
            .ToListAsync();

        var dtos = slots.Select(s => MapToDto(s, locationId)).ToList();

        return Ok(HalCollection<TimeSlotDto>.Create(
            dtos,
            $"/api/locations/{locationId}/time-slots",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TimeSlotDto>> GetById(Guid locationId, Guid id)
    {
        var slot = await _context.TimeSlots
            .FirstOrDefaultAsync(t => t.Id == id && t.LocationId == locationId);

        if (slot == null)
            return NotFound();

        return Ok(MapToDto(slot, locationId));
    }

    [HttpPost]
    public async Task<ActionResult<TimeSlotDto>> Create(
        Guid locationId,
        [FromBody] CreateTimeSlotRequest request)
    {
        if (request.DayOfWeek < 0 || request.DayOfWeek > 6)
            return BadRequest(new { message = "Day of week must be between 0 (Sunday) and 6 (Saturday)" });

        var slot = new TimeSlot
        {
            LocationId = locationId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IntervalMinutes = request.IntervalMinutes,
            MaxBookings = request.MaxBookings,
            MaxCovers = request.MaxCovers,
            TurnTimeMinutes = request.TurnTimeMinutes,
            Name = request.Name,
            IsActive = true,
            FloorPlanId = request.FloorPlanId,
            Priority = request.Priority
        };

        _context.TimeSlots.Add(slot);
        await _context.SaveChangesAsync();

        var dto = MapToDto(slot, locationId);

        return CreatedAtAction(nameof(GetById), new { locationId, id = slot.Id }, dto);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<TimeSlotDto>> Update(
        Guid locationId,
        Guid id,
        [FromBody] UpdateTimeSlotRequest request)
    {
        var slot = await _context.TimeSlots
            .FirstOrDefaultAsync(t => t.Id == id && t.LocationId == locationId);

        if (slot == null)
            return NotFound();

        if (request.StartTime.HasValue) slot.StartTime = request.StartTime.Value;
        if (request.EndTime.HasValue) slot.EndTime = request.EndTime.Value;
        if (request.IntervalMinutes.HasValue) slot.IntervalMinutes = request.IntervalMinutes.Value;
        if (request.MaxBookings.HasValue) slot.MaxBookings = request.MaxBookings;
        if (request.MaxCovers.HasValue) slot.MaxCovers = request.MaxCovers;
        if (request.TurnTimeMinutes.HasValue) slot.TurnTimeMinutes = request.TurnTimeMinutes.Value;
        if (request.Name != null) slot.Name = request.Name;
        if (request.IsActive.HasValue) slot.IsActive = request.IsActive.Value;
        if (request.FloorPlanId.HasValue) slot.FloorPlanId = request.FloorPlanId;
        if (request.Priority.HasValue) slot.Priority = request.Priority.Value;

        await _context.SaveChangesAsync();

        return Ok(MapToDto(slot, locationId));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid id)
    {
        var slot = await _context.TimeSlots
            .FirstOrDefaultAsync(t => t.Id == id && t.LocationId == locationId);

        if (slot == null)
            return NotFound();

        _context.TimeSlots.Remove(slot);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static TimeSlotDto MapToDto(TimeSlot slot, Guid locationId)
    {
        var dayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        var dto = new TimeSlotDto
        {
            Id = slot.Id,
            LocationId = slot.LocationId,
            DayOfWeek = slot.DayOfWeek,
            DayName = dayNames[slot.DayOfWeek],
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            IntervalMinutes = slot.IntervalMinutes,
            MaxBookings = slot.MaxBookings,
            MaxCovers = slot.MaxCovers,
            TurnTimeMinutes = slot.TurnTimeMinutes,
            Name = slot.Name,
            IsActive = slot.IsActive,
            FloorPlanId = slot.FloorPlanId,
            Priority = slot.Priority,
            CreatedAt = slot.CreatedAt
        };

        dto.AddSelfLink($"/api/locations/{locationId}/time-slots/{slot.Id}");

        return dto;
    }
}

[ApiController]
[Route("api/locations/{locationId:guid}/date-overrides")]
public class DateOverridesController : ControllerBase
{
    private readonly BookingDbContext _context;

    public DateOverridesController(BookingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<DateOverrideDto>>> GetAll(
        Guid locationId,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null)
    {
        var query = _context.DateOverrides
            .Where(d => d.LocationId == locationId);

        if (fromDate.HasValue)
            query = query.Where(d => d.Date >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(d => d.Date <= toDate.Value);

        var overrides = await query
            .OrderBy(d => d.Date)
            .ToListAsync();

        var dtos = overrides.Select(d => MapToDto(d, locationId)).ToList();

        return Ok(HalCollection<DateOverrideDto>.Create(
            dtos,
            $"/api/locations/{locationId}/date-overrides",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DateOverrideDto>> GetById(Guid locationId, Guid id)
    {
        var dateOverride = await _context.DateOverrides
            .FirstOrDefaultAsync(d => d.Id == id && d.LocationId == locationId);

        if (dateOverride == null)
            return NotFound();

        return Ok(MapToDto(dateOverride, locationId));
    }

    [HttpPost]
    public async Task<ActionResult<DateOverrideDto>> Create(
        Guid locationId,
        [FromBody] CreateDateOverrideRequest request)
    {
        // Check for existing override on this date
        var exists = await _context.DateOverrides
            .AnyAsync(d => d.LocationId == locationId && d.Date == request.Date);

        if (exists)
            return BadRequest(new { message = "An override already exists for this date" });

        var dateOverride = new DateOverride
        {
            LocationId = locationId,
            Date = request.Date,
            OverrideType = request.OverrideType,
            Name = request.Name,
            Description = request.Description,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            MaxBookings = request.MaxBookings,
            MaxCovers = request.MaxCovers,
            DisableOnlineBooking = request.DisableOnlineBooking,
            Notes = request.Notes
        };

        _context.DateOverrides.Add(dateOverride);
        await _context.SaveChangesAsync();

        var dto = MapToDto(dateOverride, locationId);

        return CreatedAtAction(nameof(GetById), new { locationId, id = dateOverride.Id }, dto);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<DateOverrideDto>> Update(
        Guid locationId,
        Guid id,
        [FromBody] UpdateDateOverrideRequest request)
    {
        var dateOverride = await _context.DateOverrides
            .FirstOrDefaultAsync(d => d.Id == id && d.LocationId == locationId);

        if (dateOverride == null)
            return NotFound();

        if (request.OverrideType != null) dateOverride.OverrideType = request.OverrideType;
        if (request.Name != null) dateOverride.Name = request.Name;
        if (request.Description != null) dateOverride.Description = request.Description;
        if (request.StartTime.HasValue) dateOverride.StartTime = request.StartTime;
        if (request.EndTime.HasValue) dateOverride.EndTime = request.EndTime;
        if (request.MaxBookings.HasValue) dateOverride.MaxBookings = request.MaxBookings;
        if (request.MaxCovers.HasValue) dateOverride.MaxCovers = request.MaxCovers;
        if (request.DisableOnlineBooking.HasValue) dateOverride.DisableOnlineBooking = request.DisableOnlineBooking.Value;
        if (request.Notes != null) dateOverride.Notes = request.Notes;

        await _context.SaveChangesAsync();

        return Ok(MapToDto(dateOverride, locationId));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid id)
    {
        var dateOverride = await _context.DateOverrides
            .FirstOrDefaultAsync(d => d.Id == id && d.LocationId == locationId);

        if (dateOverride == null)
            return NotFound();

        _context.DateOverrides.Remove(dateOverride);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static DateOverrideDto MapToDto(DateOverride dateOverride, Guid locationId)
    {
        var dto = new DateOverrideDto
        {
            Id = dateOverride.Id,
            LocationId = dateOverride.LocationId,
            Date = dateOverride.Date,
            OverrideType = dateOverride.OverrideType,
            Name = dateOverride.Name,
            Description = dateOverride.Description,
            StartTime = dateOverride.StartTime,
            EndTime = dateOverride.EndTime,
            MaxBookings = dateOverride.MaxBookings,
            MaxCovers = dateOverride.MaxCovers,
            DisableOnlineBooking = dateOverride.DisableOnlineBooking,
            Notes = dateOverride.Notes,
            CreatedAt = dateOverride.CreatedAt
        };

        dto.AddSelfLink($"/api/locations/{locationId}/date-overrides/{dateOverride.Id}");

        return dto;
    }
}
