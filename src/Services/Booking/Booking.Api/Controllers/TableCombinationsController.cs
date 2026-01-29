using DarkVelocity.Booking.Api.Data;
using DarkVelocity.Booking.Api.Dtos;
using DarkVelocity.Booking.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Booking.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/table-combinations")]
public class TableCombinationsController : ControllerBase
{
    private readonly BookingDbContext _context;

    public TableCombinationsController(BookingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<TableCombinationDto>>> GetAll(
        Guid locationId,
        [FromQuery] Guid? floorPlanId = null,
        [FromQuery] Guid? tableId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int? minCapacity = null,
        [FromQuery] int limit = 50)
    {
        var query = _context.TableCombinations
            .Include(c => c.FloorPlan)
            .Include(c => c.Tables)
            .ThenInclude(ct => ct.Table)
            .Where(c => c.LocationId == locationId);

        if (floorPlanId.HasValue)
            query = query.Where(c => c.FloorPlanId == floorPlanId.Value);

        if (tableId.HasValue)
            query = query.Where(c => c.Tables.Any(t => t.TableId == tableId.Value));

        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);

        if (minCapacity.HasValue)
            query = query.Where(c => c.CombinedCapacity >= minCapacity.Value);

        var combinations = await query
            .OrderBy(c => c.FloorPlanId)
            .ThenBy(c => c.Name)
            .Take(limit)
            .ToListAsync();

        var dtos = combinations.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            AddLinks(dto, locationId);
        }

        return Ok(HalCollection<TableCombinationDto>.Create(
            dtos,
            $"/api/locations/{locationId}/table-combinations",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TableCombinationDto>> GetById(Guid locationId, Guid id)
    {
        var combination = await _context.TableCombinations
            .Include(c => c.FloorPlan)
            .Include(c => c.Tables)
            .ThenInclude(ct => ct.Table)
            .FirstOrDefaultAsync(c => c.Id == id && c.LocationId == locationId);

        if (combination == null)
            return NotFound();

        var dto = MapToDto(combination);
        AddLinks(dto, locationId);
        AddDetailedLinks(dto, locationId);

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<TableCombinationDto>> Create(
        Guid locationId,
        [FromBody] CreateTableCombinationRequest request)
    {
        // Verify floor plan exists
        var floorPlan = await _context.FloorPlans
            .FirstOrDefaultAsync(f => f.Id == request.FloorPlanId && f.LocationId == locationId);

        if (floorPlan == null)
            return BadRequest(new { message = "Floor plan not found" });

        // Verify all tables exist and are on the same floor plan
        var tables = await _context.Tables
            .Where(t => request.TableIds.Contains(t.Id) && t.LocationId == locationId)
            .ToListAsync();

        if (tables.Count != request.TableIds.Count)
            return BadRequest(new { message = "One or more tables not found" });

        if (tables.Any(t => t.FloorPlanId != request.FloorPlanId))
            return BadRequest(new { message = "All tables must be on the same floor plan" });

        if (tables.Any(t => !t.IsCombinationAllowed))
            return BadRequest(new { message = "One or more tables do not allow combination" });

        var combination = new TableCombination
        {
            LocationId = locationId,
            FloorPlanId = request.FloorPlanId,
            Name = request.Name,
            CombinedCapacity = request.CombinedCapacity,
            MinPartySize = request.MinPartySize,
            IsActive = true,
            Notes = request.Notes
        };

        _context.TableCombinations.Add(combination);

        // Add tables to combination
        var position = 1;
        foreach (var tableId in request.TableIds)
        {
            _context.TableCombinationTables.Add(new TableCombinationTable
            {
                TableCombinationId = combination.Id,
                TableId = tableId,
                Position = position++
            });
        }

        await _context.SaveChangesAsync();

        // Reload with related data
        await _context.Entry(combination).Reference(c => c.FloorPlan).LoadAsync();
        await _context.Entry(combination).Collection(c => c.Tables).LoadAsync();
        foreach (var ct in combination.Tables)
        {
            await _context.Entry(ct).Reference(t => t.Table).LoadAsync();
        }

        var dto = MapToDto(combination);
        AddLinks(dto, locationId);

        return CreatedAtAction(nameof(GetById), new { locationId, id = combination.Id }, dto);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<TableCombinationDto>> Update(
        Guid locationId,
        Guid id,
        [FromBody] UpdateTableCombinationRequest request)
    {
        var combination = await _context.TableCombinations
            .Include(c => c.FloorPlan)
            .Include(c => c.Tables)
            .ThenInclude(ct => ct.Table)
            .FirstOrDefaultAsync(c => c.Id == id && c.LocationId == locationId);

        if (combination == null)
            return NotFound();

        if (request.Name != null) combination.Name = request.Name;
        if (request.CombinedCapacity.HasValue) combination.CombinedCapacity = request.CombinedCapacity.Value;
        if (request.MinPartySize.HasValue) combination.MinPartySize = request.MinPartySize.Value;
        if (request.IsActive.HasValue) combination.IsActive = request.IsActive.Value;
        if (request.Notes != null) combination.Notes = request.Notes;

        await _context.SaveChangesAsync();

        var dto = MapToDto(combination);
        AddLinks(dto, locationId);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/tables")]
    public async Task<ActionResult<TableCombinationDto>> AddTable(
        Guid locationId,
        Guid id,
        [FromBody] AddTableToCombinationRequest request)
    {
        var combination = await _context.TableCombinations
            .Include(c => c.FloorPlan)
            .Include(c => c.Tables)
            .ThenInclude(ct => ct.Table)
            .FirstOrDefaultAsync(c => c.Id == id && c.LocationId == locationId);

        if (combination == null)
            return NotFound();

        // Verify table exists
        var table = await _context.Tables
            .FirstOrDefaultAsync(t => t.Id == request.TableId && t.LocationId == locationId);

        if (table == null)
            return BadRequest(new { message = "Table not found" });

        if (table.FloorPlanId != combination.FloorPlanId)
            return BadRequest(new { message = "Table must be on the same floor plan" });

        if (!table.IsCombinationAllowed)
            return BadRequest(new { message = "Table does not allow combination" });

        // Check if already in combination
        if (combination.Tables.Any(t => t.TableId == request.TableId))
            return BadRequest(new { message = "Table is already in this combination" });

        _context.TableCombinationTables.Add(new TableCombinationTable
        {
            TableCombinationId = id,
            TableId = request.TableId,
            Position = request.Position
        });

        await _context.SaveChangesAsync();

        // Reload
        await _context.Entry(combination).Collection(c => c.Tables).LoadAsync();
        foreach (var ct in combination.Tables)
        {
            await _context.Entry(ct).Reference(t => t.Table).LoadAsync();
        }

        var dto = MapToDto(combination);
        AddLinks(dto, locationId);

        return Ok(dto);
    }

    [HttpDelete("{id:guid}/tables/{tableId:guid}")]
    public async Task<IActionResult> RemoveTable(Guid locationId, Guid id, Guid tableId)
    {
        var combinationTable = await _context.TableCombinationTables
            .Include(ct => ct.TableCombination)
            .FirstOrDefaultAsync(ct =>
                ct.TableCombinationId == id &&
                ct.TableId == tableId &&
                ct.TableCombination!.LocationId == locationId);

        if (combinationTable == null)
            return NotFound();

        _context.TableCombinationTables.Remove(combinationTable);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid id)
    {
        var combination = await _context.TableCombinations
            .FirstOrDefaultAsync(c => c.Id == id && c.LocationId == locationId);

        if (combination == null)
            return NotFound();

        // Check for active bookings
        var hasActiveBookings = await _context.Bookings
            .AnyAsync(b => b.TableCombinationId == id &&
                          (b.Status == "pending" || b.Status == "confirmed"));

        if (hasActiveBookings)
            return BadRequest(new { message = "Cannot delete combination with active bookings" });

        _context.TableCombinations.Remove(combination);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static TableCombinationDto MapToDto(TableCombination combination)
    {
        return new TableCombinationDto
        {
            Id = combination.Id,
            LocationId = combination.LocationId,
            FloorPlanId = combination.FloorPlanId,
            FloorPlanName = combination.FloorPlan?.Name,
            Name = combination.Name,
            CombinedCapacity = combination.CombinedCapacity,
            MinPartySize = combination.MinPartySize,
            IsActive = combination.IsActive,
            Notes = combination.Notes,
            CreatedAt = combination.CreatedAt,
            Tables = combination.Tables
                .OrderBy(t => t.Position)
                .Select(ct => new TableDto
                {
                    Id = ct.Table!.Id,
                    LocationId = ct.Table.LocationId,
                    FloorPlanId = ct.Table.FloorPlanId,
                    TableNumber = ct.Table.TableNumber,
                    Name = ct.Table.Name,
                    MinCapacity = ct.Table.MinCapacity,
                    MaxCapacity = ct.Table.MaxCapacity,
                    Shape = ct.Table.Shape,
                    PositionX = ct.Table.PositionX,
                    PositionY = ct.Table.PositionY,
                    Width = ct.Table.Width,
                    Height = ct.Table.Height,
                    Rotation = ct.Table.Rotation,
                    Status = ct.Table.Status,
                    IsCombinationAllowed = ct.Table.IsCombinationAllowed,
                    IsActive = ct.Table.IsActive,
                    AssignmentPriority = ct.Table.AssignmentPriority,
                    Notes = ct.Table.Notes,
                    CreatedAt = ct.Table.CreatedAt
                }).ToList()
        };
    }

    private static void AddLinks(TableCombinationDto dto, Guid locationId)
    {
        dto.AddSelfLink($"/api/locations/{locationId}/table-combinations/{dto.Id}");
        dto.AddLink("floorPlan", $"/api/locations/{locationId}/floor-plans/{dto.FloorPlanId}");
    }

    private static void AddDetailedLinks(TableCombinationDto dto, Guid locationId)
    {
        dto.AddLink("bookings", $"/api/locations/{locationId}/bookings?tableCombinationId={dto.Id}");
    }
}
