using System.Text.Json;
using DarkVelocity.OrdersGateway.Api.Adapters;
using DarkVelocity.OrdersGateway.Api.Data;
using DarkVelocity.OrdersGateway.Api.Dtos;
using DarkVelocity.OrdersGateway.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.OrdersGateway.Api.Controllers;

/// <summary>
/// Controller for managing delivery platform integrations.
/// </summary>
[ApiController]
[Route("api/delivery-platforms")]
public class DeliveryPlatformsController : ControllerBase
{
    private readonly OrdersGatewayDbContext _context;
    private readonly IDeliveryPlatformAdapterFactory _adapterFactory;

    public DeliveryPlatformsController(
        OrdersGatewayDbContext context,
        IDeliveryPlatformAdapterFactory adapterFactory)
    {
        _context = context;
        _adapterFactory = adapterFactory;
    }

    /// <summary>
    /// List all connected delivery platforms.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HalCollection<DeliveryPlatformDto>>> GetAll(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] PlatformStatus? status = null)
    {
        var query = _context.DeliveryPlatforms.AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(p => p.TenantId == tenantId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var platforms = await query
            .OrderBy(p => p.Name)
            .ToListAsync();

        var dtos = platforms.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/delivery-platforms/{dto.Id}");
            dto.AddLink("locations", $"/api/delivery-platforms/{dto.Id}/locations");
            dto.AddLink("menu-syncs", $"/api/delivery-platforms/{dto.Id}/menu-syncs");
        }

        return Ok(HalCollection<DeliveryPlatformDto>.Create(dtos, "/api/delivery-platforms", platforms.Count));
    }

    /// <summary>
    /// Get a specific delivery platform.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DeliveryPlatformDto>> Get(Guid id)
    {
        var platform = await _context.DeliveryPlatforms.FindAsync(id);
        if (platform == null)
        {
            return NotFound();
        }

        var dto = MapToDto(platform);
        dto.AddSelfLink($"/api/delivery-platforms/{id}");
        dto.AddLink("locations", $"/api/delivery-platforms/{id}/locations");
        dto.AddLink("menu-syncs", $"/api/delivery-platforms/{id}/menu-syncs");
        dto.AddLink("status", $"/api/delivery-platforms/{id}/status");

        return Ok(dto);
    }

    /// <summary>
    /// Connect a new delivery platform.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<DeliveryPlatformDto>> Connect([FromBody] ConnectPlatformRequest request)
    {
        // Validate platform type is supported
        var supportedPlatforms = _adapterFactory.GetSupportedPlatforms();
        if (!supportedPlatforms.Contains(request.PlatformType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest($"Platform type '{request.PlatformType}' is not supported. Supported: {string.Join(", ", supportedPlatforms)}");
        }

        var adapter = _adapterFactory.GetAdapter(request.PlatformType);
        if (adapter == null)
        {
            return BadRequest($"No adapter available for platform type '{request.PlatformType}'");
        }

        // Test connection if credentials provided
        if (request.Credentials != null)
        {
            var testResult = await adapter.TestConnectionAsync(request.Credentials);
            if (!testResult.Success)
            {
                return BadRequest($"Connection test failed: {testResult.ErrorMessage}");
            }
        }

        var platform = new DeliveryPlatform
        {
            TenantId = Guid.NewGuid(), // TODO: Get from auth context
            PlatformType = request.PlatformType,
            Name = request.Name,
            MerchantId = request.MerchantId,
            Status = PlatformStatus.Active,
            Settings = JsonSerializer.Serialize(request.Settings ?? new PlatformSettings()),
            ConnectedAt = DateTime.UtcNow,
            WebhookSecret = request.Credentials?.WebhookSecret
        };

        // Encrypt and store credentials
        if (request.Credentials != null)
        {
            platform.ApiCredentialsEncrypted = JsonSerializer.Serialize(request.Credentials);
        }

        _context.DeliveryPlatforms.Add(platform);
        await _context.SaveChangesAsync();

        var dto = MapToDto(platform);
        dto.AddSelfLink($"/api/delivery-platforms/{platform.Id}");

        return CreatedAtAction(nameof(Get), new { id = platform.Id }, dto);
    }

    /// <summary>
    /// Update platform settings.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DeliveryPlatformDto>> Update(Guid id, [FromBody] UpdatePlatformRequest request)
    {
        var platform = await _context.DeliveryPlatforms.FindAsync(id);
        if (platform == null)
        {
            return NotFound();
        }

        if (request.Name != null)
        {
            platform.Name = request.Name;
        }

        if (request.Settings != null)
        {
            platform.Settings = JsonSerializer.Serialize(request.Settings);
        }

        await _context.SaveChangesAsync();

        var dto = MapToDto(platform);
        dto.AddSelfLink($"/api/delivery-platforms/{id}");

        return Ok(dto);
    }

    /// <summary>
    /// Pause receiving orders from a platform.
    /// </summary>
    [HttpPost("{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid id)
    {
        var platform = await _context.DeliveryPlatforms.FindAsync(id);
        if (platform == null)
        {
            return NotFound();
        }

        platform.Status = PlatformStatus.Paused;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Resume receiving orders from a platform.
    /// </summary>
    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id)
    {
        var platform = await _context.DeliveryPlatforms.FindAsync(id);
        if (platform == null)
        {
            return NotFound();
        }

        platform.Status = PlatformStatus.Active;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Disconnect from a platform.
    /// </summary>
    [HttpPost("{id:guid}/disconnect")]
    public async Task<IActionResult> Disconnect(Guid id)
    {
        var platform = await _context.DeliveryPlatforms.FindAsync(id);
        if (platform == null)
        {
            return NotFound();
        }

        var adapter = _adapterFactory.GetAdapter(platform.PlatformType);
        if (adapter != null)
        {
            await adapter.DisconnectAsync(platform);
        }

        platform.Status = PlatformStatus.Disconnected;
        platform.ApiCredentialsEncrypted = null;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Get platform connection status and health.
    /// </summary>
    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult<PlatformStatusDto>> GetStatus(Guid id)
    {
        var platform = await _context.DeliveryPlatforms.FindAsync(id);
        if (platform == null)
        {
            return NotFound();
        }

        var dto = new PlatformStatusDto
        {
            Id = platform.Id,
            PlatformType = platform.PlatformType,
            Status = platform.Status,
            IsConnected = platform.Status == PlatformStatus.Active,
            IsReceivingOrders = platform.Status == PlatformStatus.Active && platform.LastOrderAt.HasValue,
            LastOrderAt = platform.LastOrderAt,
            LastSyncAt = platform.LastSyncAt,
            CheckedAt = DateTime.UtcNow
        };

        dto.AddSelfLink($"/api/delivery-platforms/{id}/status");

        return Ok(dto);
    }

    /// <summary>
    /// Test platform connection.
    /// </summary>
    [HttpPost("{id:guid}/test")]
    public async Task<ActionResult<ConnectionResult>> TestConnection(Guid id)
    {
        var platform = await _context.DeliveryPlatforms.FindAsync(id);
        if (platform == null)
        {
            return NotFound();
        }

        var adapter = _adapterFactory.GetAdapter(platform.PlatformType);
        if (adapter == null)
        {
            return BadRequest($"No adapter available for platform type '{platform.PlatformType}'");
        }

        // Parse stored credentials
        var credentials = string.IsNullOrEmpty(platform.ApiCredentialsEncrypted)
            ? new PlatformCredentials()
            : JsonSerializer.Deserialize<PlatformCredentials>(platform.ApiCredentialsEncrypted) ?? new PlatformCredentials();

        var result = await adapter.TestConnectionAsync(credentials);

        if (!result.Success)
        {
            platform.Status = PlatformStatus.Error;
            await _context.SaveChangesAsync();
        }

        return Ok(result);
    }

    private static DeliveryPlatformDto MapToDto(DeliveryPlatform platform)
    {
        return new DeliveryPlatformDto
        {
            Id = platform.Id,
            TenantId = platform.TenantId,
            PlatformType = platform.PlatformType,
            Name = platform.Name,
            Status = platform.Status,
            MerchantId = platform.MerchantId,
            Settings = JsonSerializer.Deserialize<PlatformSettings>(platform.Settings) ?? new PlatformSettings(),
            ConnectedAt = platform.ConnectedAt,
            LastSyncAt = platform.LastSyncAt,
            LastOrderAt = platform.LastOrderAt,
            CreatedAt = platform.CreatedAt,
            UpdatedAt = platform.UpdatedAt
        };
    }
}
