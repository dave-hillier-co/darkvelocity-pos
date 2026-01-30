using DarkVelocity.OrdersGateway.Api.Adapters;
using DarkVelocity.OrdersGateway.Api.Data;
using DarkVelocity.OrdersGateway.Api.Entities;
using DarkVelocity.OrdersGateway.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.OrdersGateway.Api.Controllers;

/// <summary>
/// Controller for receiving webhooks from delivery platforms.
/// </summary>
[ApiController]
[Route("webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly OrdersGatewayDbContext _context;
    private readonly IDeliveryPlatformAdapterFactory _adapterFactory;
    private readonly IExternalOrderService _orderService;
    private readonly IAutoAcceptEngine _autoAcceptEngine;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        OrdersGatewayDbContext context,
        IDeliveryPlatformAdapterFactory adapterFactory,
        IExternalOrderService orderService,
        IAutoAcceptEngine autoAcceptEngine,
        ILogger<WebhooksController> logger)
    {
        _context = context;
        _adapterFactory = adapterFactory;
        _orderService = orderService;
        _autoAcceptEngine = autoAcceptEngine;
        _logger = logger;
    }

    /// <summary>
    /// Receive webhooks from Uber Eats.
    /// </summary>
    [HttpPost("ubereats")]
    public Task<IActionResult> UberEats([FromQuery] string? storeId = null)
        => ProcessWebhook("UberEats", storeId);

    /// <summary>
    /// Receive webhooks from DoorDash.
    /// </summary>
    [HttpPost("doordash")]
    public Task<IActionResult> DoorDash([FromQuery] string? storeId = null)
        => ProcessWebhook("DoorDash", storeId);

    /// <summary>
    /// Receive webhooks from Deliveroo.
    /// </summary>
    [HttpPost("deliveroo")]
    public Task<IActionResult> Deliveroo([FromQuery] string? storeId = null)
        => ProcessWebhook("Deliveroo", storeId);

    /// <summary>
    /// Receive webhooks from Just Eat.
    /// </summary>
    [HttpPost("justeat")]
    public Task<IActionResult> JustEat([FromQuery] string? storeId = null)
        => ProcessWebhook("JustEat", storeId);

    /// <summary>
    /// Generic webhook endpoint for custom platforms.
    /// </summary>
    [HttpPost("generic/{platformId:guid}")]
    public async Task<IActionResult> Generic(Guid platformId, [FromQuery] string? storeId = null)
    {
        var platform = await _context.DeliveryPlatforms.FindAsync(platformId);
        if (platform == null)
        {
            return NotFound("Platform not found");
        }

        return await ProcessWebhookForPlatform(platform, storeId);
    }

    /// <summary>
    /// Webhook verification endpoint (used by some platforms).
    /// </summary>
    [HttpGet("{platform}/verify")]
    public IActionResult Verify(string platform, [FromQuery] string? challenge = null)
    {
        if (!string.IsNullOrEmpty(challenge))
        {
            return Ok(challenge);
        }

        return Ok();
    }

    private async Task<IActionResult> ProcessWebhook(string platformType, string? storeId)
    {
        // Find the platform by type
        var query = _context.DeliveryPlatforms
            .Where(p => p.PlatformType == platformType && p.Status == PlatformStatus.Active);

        // If storeId is provided, try to match via PlatformLocation
        if (!string.IsNullOrEmpty(storeId))
        {
            var platformLocation = await _context.PlatformLocations
                .Include(pl => pl.DeliveryPlatform)
                .FirstOrDefaultAsync(pl => pl.PlatformStoreId == storeId && pl.DeliveryPlatform.PlatformType == platformType);

            if (platformLocation != null)
            {
                return await ProcessWebhookForPlatform(platformLocation.DeliveryPlatform, storeId);
            }
        }

        // Fall back to first active platform of this type
        var platform = await query.FirstOrDefaultAsync();
        if (platform == null)
        {
            _logger.LogWarning("No active {PlatformType} platform found for webhook", platformType);
            return NotFound($"No active {platformType} platform found");
        }

        return await ProcessWebhookForPlatform(platform, storeId);
    }

    private async Task<IActionResult> ProcessWebhookForPlatform(DeliveryPlatform platform, string? storeId)
    {
        // Read the raw payload
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();

        // Get signature from headers (different platforms use different header names)
        var signature = Request.Headers["X-Uber-Signature"].FirstOrDefault()
            ?? Request.Headers["X-DoorDash-Signature"].FirstOrDefault()
            ?? Request.Headers["X-Deliveroo-Signature"].FirstOrDefault()
            ?? Request.Headers["X-JustEat-Signature"].FirstOrDefault()
            ?? Request.Headers["X-Webhook-Signature"].FirstOrDefault();

        _logger.LogDebug("Received webhook for {PlatformType}: {Payload}", platform.PlatformType, payload);

        var adapter = _adapterFactory.GetAdapter(platform.PlatformType);
        if (adapter == null)
        {
            _logger.LogError("No adapter found for {PlatformType}", platform.PlatformType);
            return StatusCode(500, "Adapter not available");
        }

        // Parse the webhook
        var result = await adapter.ParseWebhookAsync(payload, signature, platform);

        if (!result.Success)
        {
            _logger.LogWarning("Failed to parse webhook: {Error}", result.ErrorMessage);
            return BadRequest(result.ErrorMessage);
        }

        // Determine location from storeId
        Guid locationId = Guid.Empty;
        if (!string.IsNullOrEmpty(storeId))
        {
            var platformLocation = await _context.PlatformLocations
                .FirstOrDefaultAsync(pl => pl.DeliveryPlatformId == platform.Id && pl.PlatformStoreId == storeId);

            if (platformLocation != null)
            {
                locationId = platformLocation.LocationId;
            }
        }

        // Handle different event types
        switch (result.EventType)
        {
            case WebhookEventType.OrderCreated:
                if (result.Order != null)
                {
                    await HandleOrderCreated(platform, locationId, result.Order);
                }
                break;

            case WebhookEventType.OrderCancelled:
                if (result.Order != null)
                {
                    await HandleOrderCancelled(platform, result.Order.PlatformOrderId);
                }
                break;

            case WebhookEventType.OrderPickedUp:
                if (result.Order != null)
                {
                    await HandleOrderPickedUp(platform, result.Order.PlatformOrderId);
                }
                break;

            case WebhookEventType.OrderDelivered:
                if (result.Order != null)
                {
                    await HandleOrderDelivered(platform, result.Order.PlatformOrderId);
                }
                break;

            default:
                _logger.LogDebug("Unhandled webhook event type: {EventType}", result.EventType);
                break;
        }

        return Ok();
    }

    private async Task HandleOrderCreated(DeliveryPlatform platform, Guid locationId, ExternalOrder order)
    {
        try
        {
            // Create the order in the database
            var createdOrder = await _orderService.CreateFromWebhookAsync(platform, locationId, order);

            // Try auto-accept if enabled
            await _autoAcceptEngine.TryAutoAcceptAsync(createdOrder, platform);

            _logger.LogInformation("Processed order {PlatformOrderId} from {PlatformType}",
                order.PlatformOrderId, platform.PlatformType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process order {PlatformOrderId} from {PlatformType}",
                order.PlatformOrderId, platform.PlatformType);
            throw;
        }
    }

    private async Task HandleOrderCancelled(DeliveryPlatform platform, string platformOrderId)
    {
        var order = await _orderService.GetByPlatformOrderIdAsync(platform.Id, platformOrderId);
        if (order != null)
        {
            await _orderService.UpdateStatusAsync(order.Id, ExternalOrderStatus.Cancelled);
            _logger.LogInformation("Order {PlatformOrderId} cancelled by platform", platformOrderId);
        }
    }

    private async Task HandleOrderPickedUp(DeliveryPlatform platform, string platformOrderId)
    {
        var order = await _orderService.GetByPlatformOrderIdAsync(platform.Id, platformOrderId);
        if (order != null)
        {
            await _orderService.UpdateStatusAsync(order.Id, ExternalOrderStatus.PickedUp);
            _logger.LogInformation("Order {PlatformOrderId} picked up", platformOrderId);
        }
    }

    private async Task HandleOrderDelivered(DeliveryPlatform platform, string platformOrderId)
    {
        var order = await _orderService.GetByPlatformOrderIdAsync(platform.Id, platformOrderId);
        if (order != null)
        {
            await _orderService.UpdateStatusAsync(order.Id, ExternalOrderStatus.Delivered);
            _logger.LogInformation("Order {PlatformOrderId} delivered", platformOrderId);
        }
    }
}
