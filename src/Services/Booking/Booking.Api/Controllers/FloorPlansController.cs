using DarkVelocity.Booking.Api.Data;
using DarkVelocity.Booking.Api.Dtos;
using DarkVelocity.Booking.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Booking.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/floor-plans")]
public class FloorPlansController : ControllerBase
{
    private readonly BookingDbContext _context;

    public FloorPlansController(BookingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<FloorPlanDto>>> GetAll(
        Guid locationId,
        [FromQuery] bool? isActive = null,
        [FromQuery] int limit = 50)
    {
        var query = _context.FloorPlans
            .Include(f => f.Tables)
            .Where(f => f.LocationId == locationId);

        if (isActive.HasValue)
        {
            query = query.Where(f => f.IsActive == isActive.Value);
        }

        var floorPlans = await query
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .Take(limit)
            .ToListAsync();

        var dtos = floorPlans.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            AddLinks(dto, locationId);
        }

        return Ok(HalCollection<FloorPlanDto>.Create(
            dtos,
            $"/api/locations/{locationId}/floor-plans",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FloorPlanDto>> GetById(Guid locationId, Guid id)
    {
        var floorPlan = await _context.FloorPlans
            .Include(f => f.Tables)
            .FirstOrDefaultAsync(f => f.Id == id && f.LocationId == locationId);

        if (floorPlan == null)
            return NotFound();

        var dto = MapToDto(floorPlan);
        AddLinks(dto, locationId);
        AddDetailedLinks(dto, locationId);

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<FloorPlanDto>> Create(
        Guid locationId,
        [FromBody] CreateFloorPlanRequest request)
    {
        var floorPlan = new FloorPlan
        {
            LocationId = locationId,
            Name = request.Name,
            Description = request.Description,
            GridWidth = request.GridWidth,
            GridHeight = request.GridHeight,
            BackgroundImageUrl = request.BackgroundImageUrl,
            SortOrder = request.SortOrder,
            DefaultTurnTimeMinutes = request.DefaultTurnTimeMinutes,
            IsActive = true
        };

        _context.FloorPlans.Add(floorPlan);
        await _context.SaveChangesAsync();

        var dto = MapToDto(floorPlan);
        AddLinks(dto, locationId);

        return CreatedAtAction(nameof(GetById), new { locationId, id = floorPlan.Id }, dto);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<FloorPlanDto>> Update(
        Guid locationId,
        Guid id,
        [FromBody] UpdateFloorPlanRequest request)
    {
        var floorPlan = await _context.FloorPlans
            .Include(f => f.Tables)
            .FirstOrDefaultAsync(f => f.Id == id && f.LocationId == locationId);

        if (floorPlan == null)
            return NotFound();

        if (request.Name != null) floorPlan.Name = request.Name;
        if (request.Description != null) floorPlan.Description = request.Description;
        if (request.GridWidth.HasValue) floorPlan.GridWidth = request.GridWidth.Value;
        if (request.GridHeight.HasValue) floorPlan.GridHeight = request.GridHeight.Value;
        if (request.BackgroundImageUrl != null) floorPlan.BackgroundImageUrl = request.BackgroundImageUrl;
        if (request.SortOrder.HasValue) floorPlan.SortOrder = request.SortOrder.Value;
        if (request.IsActive.HasValue) floorPlan.IsActive = request.IsActive.Value;
        if (request.DefaultTurnTimeMinutes.HasValue) floorPlan.DefaultTurnTimeMinutes = request.DefaultTurnTimeMinutes.Value;

        await _context.SaveChangesAsync();

        var dto = MapToDto(floorPlan);
        AddLinks(dto, locationId);

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid id)
    {
        var floorPlan = await _context.FloorPlans
            .FirstOrDefaultAsync(f => f.Id == id && f.LocationId == locationId);

        if (floorPlan == null)
            return NotFound();

        // Check if there are active bookings
        var hasActiveBookings = await _context.Bookings
            .AnyAsync(b => b.Table != null &&
                          b.Table.FloorPlanId == id &&
                          (b.Status == "pending" || b.Status == "confirmed"));

        if (hasActiveBookings)
            return BadRequest(new { message = "Cannot delete floor plan with active bookings" });

        _context.FloorPlans.Remove(floorPlan);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static FloorPlanDto MapToDto(FloorPlan floorPlan)
    {
        return new FloorPlanDto
        {
            Id = floorPlan.Id,
            LocationId = floorPlan.LocationId,
            Name = floorPlan.Name,
            Description = floorPlan.Description,
            GridWidth = floorPlan.GridWidth,
            GridHeight = floorPlan.GridHeight,
            BackgroundImageUrl = floorPlan.BackgroundImageUrl,
            SortOrder = floorPlan.SortOrder,
            IsActive = floorPlan.IsActive,
            DefaultTurnTimeMinutes = floorPlan.DefaultTurnTimeMinutes,
            CreatedAt = floorPlan.CreatedAt,
            TableCount = floorPlan.Tables.Count,
            Tables = floorPlan.Tables.Select(t => new TableDto
            {
                Id = t.Id,
                LocationId = t.LocationId,
                FloorPlanId = t.FloorPlanId,
                TableNumber = t.TableNumber,
                Name = t.Name,
                MinCapacity = t.MinCapacity,
                MaxCapacity = t.MaxCapacity,
                Shape = t.Shape,
                PositionX = t.PositionX,
                PositionY = t.PositionY,
                Width = t.Width,
                Height = t.Height,
                Rotation = t.Rotation,
                Status = t.Status,
                IsCombinationAllowed = t.IsCombinationAllowed,
                IsActive = t.IsActive,
                AssignmentPriority = t.AssignmentPriority,
                Notes = t.Notes,
                CreatedAt = t.CreatedAt
            }).ToList()
        };
    }

    private static void AddLinks(FloorPlanDto dto, Guid locationId)
    {
        dto.AddSelfLink($"/api/locations/{locationId}/floor-plans/{dto.Id}");
        dto.AddLink("tables", $"/api/locations/{locationId}/tables?floorPlanId={dto.Id}");
    }

    private static void AddDetailedLinks(FloorPlanDto dto, Guid locationId)
    {
        dto.AddLink("combinations", $"/api/locations/{locationId}/table-combinations?floorPlanId={dto.Id}");
    }
}
