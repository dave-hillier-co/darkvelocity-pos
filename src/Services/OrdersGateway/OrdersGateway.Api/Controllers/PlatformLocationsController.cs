using System.Text.Json;
using DarkVelocity.OrdersGateway.Api.Data;
using DarkVelocity.OrdersGateway.Api.Dtos;
using DarkVelocity.OrdersGateway.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.OrdersGateway.Api.Controllers;

/// <summary>
/// Controller for managing platform-location mappings.
/// </summary>
[ApiController]
[Route("api/delivery-platforms/{platformId:guid}/locations")]
public class PlatformLocationsController : ControllerBase
{
    private readonly OrdersGatewayDbContext _context;

    public PlatformLocationsController(OrdersGatewayDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// List locations mapped to a platform.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HalCollection<PlatformLocationDto>>> GetAll(Guid platformId)
    {
        var platform = await _context.DeliveryPlatforms.FindAsync(platformId);
        if (platform == null)
        {
            return NotFound("Platform not found");
        }

        var locations = await _context.PlatformLocations
            .Where(pl => pl.DeliveryPlatformId == platformId)
            .ToListAsync();

        var dtos = locations.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/platform-locations/{dto.Id}");
            dto.AddLink("platform", $"/api/delivery-platforms/{platformId}");
        }

        return Ok(HalCollection<PlatformLocationDto>.Create(dtos, $"/api/delivery-platforms/{platformId}/locations", locations.Count));
    }

    /// <summary>
    /// Map a location to a platform.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PlatformLocationDto>> MapLocation(Guid platformId, [FromBody] MapLocationRequest request)
    {
        var platform = await _context.DeliveryPlatforms.FindAsync(platformId);
        if (platform == null)
        {
            return NotFound("Platform not found");
        }

        // Check if already mapped
        var existing = await _context.PlatformLocations
            .FirstOrDefaultAsync(pl => pl.DeliveryPlatformId == platformId && pl.LocationId == request.LocationId);

        if (existing != null)
        {
            return Conflict("Location is already mapped to this platform");
        }

        var platformLocation = new PlatformLocation
        {
            DeliveryPlatformId = platformId,
            LocationId = request.LocationId,
            PlatformStoreId = request.PlatformStoreId,
            MenuMappingId = request.MenuMappingId,
            OperatingHoursOverride = request.OperatingHoursOverride != null
                ? JsonSerializer.Serialize(request.OperatingHoursOverride)
                : null,
            IsActive = true
        };

        _context.PlatformLocations.Add(platformLocation);
        await _context.SaveChangesAsync();

        var dto = MapToDto(platformLocation);
        dto.AddSelfLink($"/api/platform-locations/{platformLocation.Id}");

        return CreatedAtAction(nameof(Get), new { id = platformLocation.Id }, dto);
    }

    /// <summary>
    /// Get a specific platform-location mapping.
    /// </summary>
    [HttpGet("~/api/platform-locations/{id:guid}")]
    public async Task<ActionResult<PlatformLocationDto>> Get(Guid id)
    {
        var platformLocation = await _context.PlatformLocations.FindAsync(id);
        if (platformLocation == null)
        {
            return NotFound();
        }

        var dto = MapToDto(platformLocation);
        dto.AddSelfLink($"/api/platform-locations/{id}");
        dto.AddLink("platform", $"/api/delivery-platforms/{platformLocation.DeliveryPlatformId}");

        return Ok(dto);
    }

    /// <summary>
    /// Update a platform-location mapping.
    /// </summary>
    [HttpPut("~/api/platform-locations/{id:guid}")]
    public async Task<ActionResult<PlatformLocationDto>> Update(Guid id, [FromBody] UpdatePlatformLocationRequest request)
    {
        var platformLocation = await _context.PlatformLocations.FindAsync(id);
        if (platformLocation == null)
        {
            return NotFound();
        }

        if (request.PlatformStoreId != null)
        {
            platformLocation.PlatformStoreId = request.PlatformStoreId;
        }

        if (request.IsActive.HasValue)
        {
            platformLocation.IsActive = request.IsActive.Value;
        }

        if (request.MenuMappingId.HasValue)
        {
            platformLocation.MenuMappingId = request.MenuMappingId;
        }

        if (request.OperatingHoursOverride != null)
        {
            platformLocation.OperatingHoursOverride = JsonSerializer.Serialize(request.OperatingHoursOverride);
        }

        await _context.SaveChangesAsync();

        var dto = MapToDto(platformLocation);
        dto.AddSelfLink($"/api/platform-locations/{id}");

        return Ok(dto);
    }

    /// <summary>
    /// Remove a platform-location mapping.
    /// </summary>
    [HttpDelete("~/api/platform-locations/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var platformLocation = await _context.PlatformLocations.FindAsync(id);
        if (platformLocation == null)
        {
            return NotFound();
        }

        _context.PlatformLocations.Remove(platformLocation);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static PlatformLocationDto MapToDto(PlatformLocation pl)
    {
        return new PlatformLocationDto
        {
            Id = pl.Id,
            DeliveryPlatformId = pl.DeliveryPlatformId,
            LocationId = pl.LocationId,
            PlatformStoreId = pl.PlatformStoreId,
            IsActive = pl.IsActive,
            MenuMappingId = pl.MenuMappingId,
            OperatingHoursOverride = !string.IsNullOrEmpty(pl.OperatingHoursOverride)
                ? JsonSerializer.Deserialize<OperatingHoursOverride>(pl.OperatingHoursOverride)
                : null,
            CreatedAt = pl.CreatedAt,
            UpdatedAt = pl.UpdatedAt
        };
    }
}
