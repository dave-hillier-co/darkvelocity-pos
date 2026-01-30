using System.Text.Json;
using DarkVelocity.OrdersGateway.Api.Adapters;
using DarkVelocity.OrdersGateway.Api.Data;
using DarkVelocity.OrdersGateway.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.OrdersGateway.Api.Services;

/// <summary>
/// Engine for automatically accepting orders based on platform settings.
/// </summary>
public interface IAutoAcceptEngine
{
    Task<bool> TryAutoAcceptAsync(ExternalOrder order, DeliveryPlatform platform, CancellationToken cancellationToken = default);
}

public class AutoAcceptEngine : IAutoAcceptEngine
{
    private readonly OrdersGatewayDbContext _context;
    private readonly IExternalOrderService _orderService;
    private readonly IDeliveryPlatformAdapterFactory _adapterFactory;
    private readonly ILogger<AutoAcceptEngine> _logger;

    public AutoAcceptEngine(
        OrdersGatewayDbContext context,
        IExternalOrderService orderService,
        IDeliveryPlatformAdapterFactory adapterFactory,
        ILogger<AutoAcceptEngine> logger)
    {
        _context = context;
        _orderService = orderService;
        _adapterFactory = adapterFactory;
        _logger = logger;
    }

    public async Task<bool> TryAutoAcceptAsync(ExternalOrder order, DeliveryPlatform platform, CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse platform settings
            var settings = JsonSerializer.Deserialize<PlatformSettings>(platform.Settings) ?? new PlatformSettings();

            if (!settings.AutoAcceptOrders)
            {
                _logger.LogDebug("Auto-accept disabled for platform {PlatformId}", platform.Id);
                return false;
            }

            // Check location availability
            var platformLocation = await _context.PlatformLocations
                .FirstOrDefaultAsync(pl => pl.DeliveryPlatformId == platform.Id && pl.LocationId == order.LocationId, cancellationToken);

            if (platformLocation == null || !platformLocation.IsActive)
            {
                _logger.LogWarning("Location {LocationId} not active for platform {PlatformId}", order.LocationId, platform.Id);
                return false;
            }

            // TODO: Check if location is open (would require calling Location service)
            // TODO: Check if all menu items are available (would require calling Menu service)

            // Determine prep time
            var prepTimeMinutes = settings.IsBusyMode
                ? settings.BusyModePrepTime
                : settings.DefaultPrepTime;

            // Accept the order
            var result = await _orderService.AcceptOrderAsync(order.Id, prepTimeMinutes, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Auto-accepted order {OrderId} with prep time {PrepTime} minutes", order.Id, prepTimeMinutes);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to auto-accept order {OrderId}: {Error}", order.Id, result.ErrorMessage);

                // Increment retry count
                order.RetryCount++;
                order.ErrorMessage = result.ErrorMessage;

                if (order.RetryCount >= settings.MaxAutoAcceptRetries)
                {
                    order.Status = ExternalOrderStatus.Failed;
                    _logger.LogError("Order {OrderId} exceeded max auto-accept retries", order.Id);
                }

                await _context.SaveChangesAsync(cancellationToken);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in auto-accept for order {OrderId}", order.Id);

            order.RetryCount++;
            order.ErrorMessage = ex.Message;
            await _context.SaveChangesAsync(cancellationToken);

            return false;
        }
    }
}
