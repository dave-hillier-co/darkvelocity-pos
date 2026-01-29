using DarkVelocity.Booking.Api.Data;
using DarkVelocity.Booking.Api.Dtos;
using DarkVelocity.Booking.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Booking.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/tables")]
public class TablesController : ControllerBase
{
    private readonly BookingDbContext _context;

    public TablesController(BookingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<TableDto>>> GetAll(
        Guid locationId,
        [FromQuery] Guid? floorPlanId = null,
        [FromQuery] string? status = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int? minCapacity = null,
        [FromQuery] int limit = 100)
    {
        var query = _context.Tables
            .Include(t => t.FloorPlan)
            .Where(t => t.LocationId == locationId);

        if (floorPlanId.HasValue)
            query = query.Where(t => t.FloorPlanId == floorPlanId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        if (minCapacity.HasValue)
            query = query.Where(t => t.MaxCapacity >= minCapacity.Value);

        var tables = await query
            .OrderBy(t => t.FloorPlanId)
            .ThenBy(t => t.AssignmentPriority)
            .ThenBy(t => t.TableNumber)
            .Take(limit)
            .ToListAsync();

        var dtos = tables.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            AddLinks(dto, locationId);
        }

        return Ok(HalCollection<TableDto>.Create(
            dtos,
            $"/api/locations/{locationId}/tables",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TableDto>> GetById(Guid locationId, Guid id)
    {
        var table = await _context.Tables
            .Include(t => t.FloorPlan)
            .FirstOrDefaultAsync(t => t.Id == id && t.LocationId == locationId);

        if (table == null)
            return NotFound();

        var dto = MapToDto(table);
        AddLinks(dto, locationId);
        AddDetailedLinks(dto, locationId);

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<TableDto>> Create(
        Guid locationId,
        [FromBody] CreateTableRequest request)
    {
        // Verify floor plan exists
        var floorPlan = await _context.FloorPlans
            .FirstOrDefaultAsync(f => f.Id == request.FloorPlanId && f.LocationId == locationId);

        if (floorPlan == null)
            return BadRequest(new { message = "Floor plan not found" });

        // Check for duplicate table number
        var exists = await _context.Tables
            .AnyAsync(t => t.LocationId == locationId && t.TableNumber == request.TableNumber);

        if (exists)
            return BadRequest(new { message = "Table number already exists" });

        var table = new Table
        {
            LocationId = locationId,
            FloorPlanId = request.FloorPlanId,
            TableNumber = request.TableNumber,
            Name = request.Name,
            MinCapacity = request.MinCapacity,
            MaxCapacity = request.MaxCapacity,
            Shape = request.Shape,
            PositionX = request.PositionX,
            PositionY = request.PositionY,
            Width = request.Width,
            Height = request.Height,
            Rotation = request.Rotation,
            Status = "available",
            IsCombinationAllowed = request.IsCombinationAllowed,
            IsActive = true,
            AssignmentPriority = request.AssignmentPriority,
            Notes = request.Notes
        };

        _context.Tables.Add(table);
        await _context.SaveChangesAsync();

        // Reload with floor plan
        await _context.Entry(table).Reference(t => t.FloorPlan).LoadAsync();

        var dto = MapToDto(table);
        AddLinks(dto, locationId);

        return CreatedAtAction(nameof(GetById), new { locationId, id = table.Id }, dto);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<TableDto>> Update(
        Guid locationId,
        Guid id,
        [FromBody] UpdateTableRequest request)
    {
        var table = await _context.Tables
            .Include(t => t.FloorPlan)
            .FirstOrDefaultAsync(t => t.Id == id && t.LocationId == locationId);

        if (table == null)
            return NotFound();

        // Check for duplicate table number if changing
        if (request.TableNumber != null && request.TableNumber != table.TableNumber)
        {
            var exists = await _context.Tables
                .AnyAsync(t => t.LocationId == locationId &&
                              t.TableNumber == request.TableNumber &&
                              t.Id != id);

            if (exists)
                return BadRequest(new { message = "Table number already exists" });

            table.TableNumber = request.TableNumber;
        }

        if (request.Name != null) table.Name = request.Name;
        if (request.MinCapacity.HasValue) table.MinCapacity = request.MinCapacity.Value;
        if (request.MaxCapacity.HasValue) table.MaxCapacity = request.MaxCapacity.Value;
        if (request.Shape != null) table.Shape = request.Shape;
        if (request.PositionX.HasValue) table.PositionX = request.PositionX.Value;
        if (request.PositionY.HasValue) table.PositionY = request.PositionY.Value;
        if (request.Width.HasValue) table.Width = request.Width.Value;
        if (request.Height.HasValue) table.Height = request.Height.Value;
        if (request.Rotation.HasValue) table.Rotation = request.Rotation.Value;
        if (request.Status != null) table.Status = request.Status;
        if (request.IsCombinationAllowed.HasValue) table.IsCombinationAllowed = request.IsCombinationAllowed.Value;
        if (request.IsActive.HasValue) table.IsActive = request.IsActive.Value;
        if (request.AssignmentPriority.HasValue) table.AssignmentPriority = request.AssignmentPriority.Value;
        if (request.Notes != null) table.Notes = request.Notes;

        await _context.SaveChangesAsync();

        var dto = MapToDto(table);
        AddLinks(dto, locationId);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<TableDto>> UpdateStatus(
        Guid locationId,
        Guid id,
        [FromBody] UpdateTableStatusRequest request)
    {
        var table = await _context.Tables
            .Include(t => t.FloorPlan)
            .FirstOrDefaultAsync(t => t.Id == id && t.LocationId == locationId);

        if (table == null)
            return NotFound();

        var validStatuses = new[] { "available", "occupied", "reserved", "closed" };
        if (!validStatuses.Contains(request.Status))
            return BadRequest(new { message = $"Invalid status. Must be one of: {string.Join(", ", validStatuses)}" });

        table.Status = request.Status;
        await _context.SaveChangesAsync();

        var dto = MapToDto(table);
        AddLinks(dto, locationId);

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid id)
    {
        var table = await _context.Tables
            .FirstOrDefaultAsync(t => t.Id == id && t.LocationId == locationId);

        if (table == null)
            return NotFound();

        // Check for active bookings
        var hasActiveBookings = await _context.Bookings
            .AnyAsync(b => b.TableId == id &&
                          (b.Status == "pending" || b.Status == "confirmed"));

        if (hasActiveBookings)
            return BadRequest(new { message = "Cannot delete table with active bookings" });

        _context.Tables.Remove(table);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("batch-update-positions")]
    public async Task<IActionResult> BatchUpdatePositions(
        Guid locationId,
        [FromBody] List<TablePositionUpdate> updates)
    {
        var tableIds = updates.Select(u => u.TableId).ToList();

        var tables = await _context.Tables
            .Where(t => t.LocationId == locationId && tableIds.Contains(t.Id))
            .ToListAsync();

        foreach (var table in tables)
        {
            var update = updates.First(u => u.TableId == table.Id);
            table.PositionX = update.PositionX;
            table.PositionY = update.PositionY;
            if (update.Rotation.HasValue)
                table.Rotation = update.Rotation.Value;
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static TableDto MapToDto(Table table)
    {
        return new TableDto
        {
            Id = table.Id,
            LocationId = table.LocationId,
            FloorPlanId = table.FloorPlanId,
            FloorPlanName = table.FloorPlan?.Name,
            TableNumber = table.TableNumber,
            Name = table.Name,
            MinCapacity = table.MinCapacity,
            MaxCapacity = table.MaxCapacity,
            Shape = table.Shape,
            PositionX = table.PositionX,
            PositionY = table.PositionY,
            Width = table.Width,
            Height = table.Height,
            Rotation = table.Rotation,
            Status = table.Status,
            IsCombinationAllowed = table.IsCombinationAllowed,
            IsActive = table.IsActive,
            AssignmentPriority = table.AssignmentPriority,
            Notes = table.Notes,
            CreatedAt = table.CreatedAt
        };
    }

    private static void AddLinks(TableDto dto, Guid locationId)
    {
        dto.AddSelfLink($"/api/locations/{locationId}/tables/{dto.Id}");
        dto.AddLink("floorPlan", $"/api/locations/{locationId}/floor-plans/{dto.FloorPlanId}");
    }

    private static void AddDetailedLinks(TableDto dto, Guid locationId)
    {
        dto.AddLink("bookings", $"/api/locations/{locationId}/bookings?tableId={dto.Id}");
        dto.AddLink("combinations", $"/api/locations/{locationId}/table-combinations?tableId={dto.Id}");
    }
}

public record TablePositionUpdate(
    Guid TableId,
    int PositionX,
    int PositionY,
    int? Rotation = null);
