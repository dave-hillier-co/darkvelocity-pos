using System.Text.Json;
using DarkVelocity.OrdersGateway.Api.Data;
using DarkVelocity.OrdersGateway.Api.Dtos;
using DarkVelocity.OrdersGateway.Api.Entities;
using DarkVelocity.OrdersGateway.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.OrdersGateway.Api.Controllers;

/// <summary>
/// Controller for menu synchronization with delivery platforms.
/// </summary>
[ApiController]
public class MenuSyncController : ControllerBase
{
    private readonly OrdersGatewayDbContext _context;
    private readonly IMenuSyncService _menuSyncService;

    public MenuSyncController(
        OrdersGatewayDbContext context,
        IMenuSyncService menuSyncService)
    {
        _context = context;
        _menuSyncService = menuSyncService;
    }

    /// <summary>
    /// Trigger a menu sync to a platform.
    /// </summary>
    [HttpPost("api/delivery-platforms/{platformId:guid}/menu-sync")]
    public async Task<ActionResult<MenuSyncDto>> TriggerSync(Guid platformId, [FromBody] TriggerMenuSyncRequest request)
    {
        var platform = await _context.DeliveryPlatforms.FindAsync(platformId);
        if (platform == null)
        {
            return NotFound("Platform not found");
        }

        // If no location specified, get the first active location
        var locationId = request.LocationId;
        if (!locationId.HasValue)
        {
            var platformLocation = await _context.PlatformLocations
                .Where(pl => pl.DeliveryPlatformId == platformId && pl.IsActive)
                .FirstOrDefaultAsync();

            if (platformLocation == null)
            {
                return BadRequest("No active location found for this platform");
            }

            locationId = platformLocation.LocationId;
        }

        try
        {
            var sync = await _menuSyncService.TriggerSyncAsync(platformId, locationId.Value, request.FullSync);
            var dto = MapToDto(sync);
            dto.AddSelfLink($"/api/menu-syncs/{sync.Id}");

            return Accepted(dto);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get menu sync status.
    /// </summary>
    [HttpGet("api/menu-syncs/{id:guid}")]
    public async Task<ActionResult<MenuSyncDto>> GetSync(Guid id)
    {
        var sync = await _menuSyncService.GetSyncStatusAsync(id);
        if (sync == null)
        {
            return NotFound();
        }

        var dto = MapToDto(sync);
        dto.AddSelfLink($"/api/menu-syncs/{id}");
        dto.AddLink("platform", $"/api/delivery-platforms/{sync.DeliveryPlatformId}");

        return Ok(dto);
    }

    /// <summary>
    /// Get menu sync history for a platform.
    /// </summary>
    [HttpGet("api/delivery-platforms/{platformId:guid}/menu-syncs")]
    public async Task<ActionResult<HalCollection<MenuSyncDto>>> GetSyncHistory(Guid platformId, [FromQuery] int limit = 20)
    {
        var platform = await _context.DeliveryPlatforms.FindAsync(platformId);
        if (platform == null)
        {
            return NotFound("Platform not found");
        }

        var syncs = await _menuSyncService.GetSyncHistoryAsync(platformId, limit);
        var dtos = syncs.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/menu-syncs/{dto.Id}");
        }

        return Ok(HalCollection<MenuSyncDto>.Create(dtos, $"/api/delivery-platforms/{platformId}/menu-syncs", syncs.Count));
    }

    /// <summary>
    /// List all menu item mappings.
    /// </summary>
    [HttpGet("api/menu-mappings")]
    public async Task<ActionResult<HalCollection<MenuItemMappingDto>>> GetMappings(
        [FromQuery] Guid? platformId = null,
        [FromQuery] int limit = 100)
    {
        var query = _context.MenuItemMappings.AsQueryable();

        if (platformId.HasValue)
        {
            query = query.Where(m => m.DeliveryPlatformId == platformId.Value);
        }

        var mappings = await query.Take(limit).ToListAsync();
        var dtos = mappings.Select(MapMappingToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/menu-mappings/{dto.Id}");
        }

        return Ok(HalCollection<MenuItemMappingDto>.Create(dtos, "/api/menu-mappings", mappings.Count));
    }

    /// <summary>
    /// Update a menu item mapping.
    /// </summary>
    [HttpPut("api/menu-mappings/{id:guid}")]
    public async Task<ActionResult<MenuItemMappingDto>> UpdateMapping(Guid id, [FromBody] UpdateMenuItemMappingRequest request)
    {
        var mapping = await _context.MenuItemMappings.FindAsync(id);
        if (mapping == null)
        {
            return NotFound();
        }

        if (request.PlatformCategoryId != null)
        {
            mapping.PlatformCategoryId = request.PlatformCategoryId;
        }

        if (request.PriceOverride.HasValue)
        {
            mapping.PriceOverride = request.PriceOverride;
        }

        if (request.IsAvailable.HasValue)
        {
            mapping.IsAvailable = request.IsAvailable.Value;
        }

        if (request.ModifierMappings != null)
        {
            mapping.ModifierMappings = JsonSerializer.Serialize(request.ModifierMappings);
        }

        await _context.SaveChangesAsync();

        var dto = MapMappingToDto(mapping);
        dto.AddSelfLink($"/api/menu-mappings/{id}");

        return Ok(dto);
    }

    /// <summary>
    /// Bulk update menu item mappings.
    /// </summary>
    [HttpPost("api/menu-mappings/bulk")]
    public async Task<IActionResult> BulkUpdateMappings([FromBody] BulkUpdateMappingsRequest request)
    {
        foreach (var update in request.Updates)
        {
            var mapping = await _context.MenuItemMappings.FindAsync(update.MappingId);
            if (mapping == null)
            {
                continue;
            }

            if (update.PriceOverride.HasValue)
            {
                mapping.PriceOverride = update.PriceOverride;
            }

            if (update.IsAvailable.HasValue)
            {
                mapping.IsAvailable = update.IsAvailable.Value;
            }
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static MenuSyncDto MapToDto(MenuSync sync)
    {
        return new MenuSyncDto
        {
            Id = sync.Id,
            TenantId = sync.TenantId,
            DeliveryPlatformId = sync.DeliveryPlatformId,
            LocationId = sync.LocationId,
            Status = sync.Status,
            ItemsTotal = sync.ItemsTotal,
            ItemsSynced = sync.ItemsSynced,
            ItemsFailed = sync.ItemsFailed,
            StartedAt = sync.StartedAt,
            CompletedAt = sync.CompletedAt,
            Errors = !string.IsNullOrEmpty(sync.ErrorLog)
                ? JsonSerializer.Deserialize<List<MenuSyncError>>(sync.ErrorLog)
                : null,
            TriggeredBy = sync.TriggeredBy,
            CreatedAt = sync.CreatedAt
        };
    }

    private static MenuItemMappingDto MapMappingToDto(MenuItemMapping mapping)
    {
        return new MenuItemMappingDto
        {
            Id = mapping.Id,
            TenantId = mapping.TenantId,
            DeliveryPlatformId = mapping.DeliveryPlatformId,
            InternalMenuItemId = mapping.InternalMenuItemId,
            PlatformItemId = mapping.PlatformItemId,
            PlatformCategoryId = mapping.PlatformCategoryId,
            PriceOverride = mapping.PriceOverride,
            IsAvailable = mapping.IsAvailable,
            ModifierMappings = !string.IsNullOrEmpty(mapping.ModifierMappings)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(mapping.ModifierMappings) ?? new()
                : new(),
            LastSyncedAt = mapping.LastSyncedAt,
            CreatedAt = mapping.CreatedAt,
            UpdatedAt = mapping.UpdatedAt
        };
    }
}
