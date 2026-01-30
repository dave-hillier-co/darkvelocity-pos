using System.Text.Json;
using DarkVelocity.OrdersGateway.Api.Adapters;
using DarkVelocity.OrdersGateway.Api.Data;
using DarkVelocity.OrdersGateway.Api.Dtos;
using DarkVelocity.OrdersGateway.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.OrdersGateway.Api.Services;

/// <summary>
/// Service for managing external orders from delivery platforms.
/// </summary>
public interface IExternalOrderService
{
    Task<ExternalOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ExternalOrder?> GetByPlatformOrderIdAsync(Guid platformId, string platformOrderId, CancellationToken cancellationToken = default);
    Task<List<ExternalOrder>> GetByLocationAsync(Guid locationId, ExternalOrderStatus? status = null, int limit = 50, CancellationToken cancellationToken = default);
    Task<ExternalOrder> CreateFromWebhookAsync(DeliveryPlatform platform, Guid locationId, ExternalOrder order, CancellationToken cancellationToken = default);
    Task<OrderActionResult> AcceptOrderAsync(Guid orderId, int prepTimeMinutes, CancellationToken cancellationToken = default);
    Task<OrderActionResult> RejectOrderAsync(Guid orderId, string reason, CancellationToken cancellationToken = default);
    Task<OrderActionResult> MarkReadyAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<OrderActionResult> CancelOrderAsync(Guid orderId, string reason, CancelledBy cancelledBy, CancellationToken cancellationToken = default);
    Task<OrderActionResult> AdjustPrepTimeAsync(Guid orderId, int newPrepTimeMinutes, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid orderId, ExternalOrderStatus status, CancellationToken cancellationToken = default);
}

public class ExternalOrderService : IExternalOrderService
{
    private readonly OrdersGatewayDbContext _context;
    private readonly IDeliveryPlatformAdapterFactory _adapterFactory;
    private readonly ILogger<ExternalOrderService> _logger;

    public ExternalOrderService(
        OrdersGatewayDbContext context,
        IDeliveryPlatformAdapterFactory adapterFactory,
        ILogger<ExternalOrderService> logger)
    {
        _context = context;
        _adapterFactory = adapterFactory;
        _logger = logger;
    }

    public async Task<ExternalOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ExternalOrders
            .Include(o => o.DeliveryPlatform)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<ExternalOrder?> GetByPlatformOrderIdAsync(Guid platformId, string platformOrderId, CancellationToken cancellationToken = default)
    {
        return await _context.ExternalOrders
            .Include(o => o.DeliveryPlatform)
            .FirstOrDefaultAsync(o => o.DeliveryPlatformId == platformId && o.PlatformOrderId == platformOrderId, cancellationToken);
    }

    public async Task<List<ExternalOrder>> GetByLocationAsync(Guid locationId, ExternalOrderStatus? status = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        var query = _context.ExternalOrders
            .Include(o => o.DeliveryPlatform)
            .Where(o => o.LocationId == locationId);

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        return await query
            .OrderByDescending(o => o.PlacedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<ExternalOrder> CreateFromWebhookAsync(DeliveryPlatform platform, Guid locationId, ExternalOrder order, CancellationToken cancellationToken = default)
    {
        // Check for duplicate
        var existing = await GetByPlatformOrderIdAsync(platform.Id, order.PlatformOrderId, cancellationToken);
        if (existing != null)
        {
            _logger.LogWarning("Duplicate order received: {PlatformOrderId} from {PlatformType}", order.PlatformOrderId, platform.PlatformType);
            return existing;
        }

        order.TenantId = platform.TenantId;
        order.LocationId = locationId;
        order.DeliveryPlatformId = platform.Id;

        _context.ExternalOrders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        // Update platform's last order time
        platform.LastOrderAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created external order {OrderId} from {PlatformType}", order.Id, platform.PlatformType);

        return order;
    }

    public async Task<OrderActionResult> AcceptOrderAsync(Guid orderId, int prepTimeMinutes, CancellationToken cancellationToken = default)
    {
        var order = await GetByIdAsync(orderId, cancellationToken);
        if (order == null)
        {
            return new OrderActionResult(false, "Order not found");
        }

        if (order.Status != ExternalOrderStatus.Pending)
        {
            return new OrderActionResult(false, $"Cannot accept order in {order.Status} status");
        }

        var adapter = _adapterFactory.GetAdapter(order.DeliveryPlatform.PlatformType);
        if (adapter == null)
        {
            return new OrderActionResult(false, $"No adapter found for {order.DeliveryPlatform.PlatformType}");
        }

        var result = await adapter.AcceptOrderAsync(order.DeliveryPlatform, order.PlatformOrderId, prepTimeMinutes, cancellationToken);

        if (result.Success)
        {
            order.Status = ExternalOrderStatus.Accepted;
            order.AcceptedAt = DateTime.UtcNow;
            order.EstimatedPickupAt = DateTime.UtcNow.AddMinutes(prepTimeMinutes);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Accepted order {OrderId} with prep time {PrepTime} minutes", orderId, prepTimeMinutes);
        }

        return result;
    }

    public async Task<OrderActionResult> RejectOrderAsync(Guid orderId, string reason, CancellationToken cancellationToken = default)
    {
        var order = await GetByIdAsync(orderId, cancellationToken);
        if (order == null)
        {
            return new OrderActionResult(false, "Order not found");
        }

        if (order.Status != ExternalOrderStatus.Pending)
        {
            return new OrderActionResult(false, $"Cannot reject order in {order.Status} status");
        }

        var adapter = _adapterFactory.GetAdapter(order.DeliveryPlatform.PlatformType);
        if (adapter == null)
        {
            return new OrderActionResult(false, $"No adapter found for {order.DeliveryPlatform.PlatformType}");
        }

        var result = await adapter.RejectOrderAsync(order.DeliveryPlatform, order.PlatformOrderId, reason, cancellationToken);

        if (result.Success)
        {
            order.Status = ExternalOrderStatus.Rejected;
            order.ErrorMessage = reason;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Rejected order {OrderId}: {Reason}", orderId, reason);
        }

        return result;
    }

    public async Task<OrderActionResult> MarkReadyAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetByIdAsync(orderId, cancellationToken);
        if (order == null)
        {
            return new OrderActionResult(false, "Order not found");
        }

        if (order.Status != ExternalOrderStatus.Accepted && order.Status != ExternalOrderStatus.Preparing)
        {
            return new OrderActionResult(false, $"Cannot mark order as ready in {order.Status} status");
        }

        var adapter = _adapterFactory.GetAdapter(order.DeliveryPlatform.PlatformType);
        if (adapter == null)
        {
            return new OrderActionResult(false, $"No adapter found for {order.DeliveryPlatform.PlatformType}");
        }

        var result = await adapter.MarkReadyAsync(order.DeliveryPlatform, order.PlatformOrderId, cancellationToken);

        if (result.Success)
        {
            order.Status = ExternalOrderStatus.Ready;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Marked order {OrderId} as ready", orderId);
        }

        return result;
    }

    public async Task<OrderActionResult> CancelOrderAsync(Guid orderId, string reason, CancelledBy cancelledBy, CancellationToken cancellationToken = default)
    {
        var order = await GetByIdAsync(orderId, cancellationToken);
        if (order == null)
        {
            return new OrderActionResult(false, "Order not found");
        }

        if (order.Status == ExternalOrderStatus.PickedUp || order.Status == ExternalOrderStatus.Delivered || order.Status == ExternalOrderStatus.Cancelled)
        {
            return new OrderActionResult(false, $"Cannot cancel order in {order.Status} status");
        }

        var adapter = _adapterFactory.GetAdapter(order.DeliveryPlatform.PlatformType);
        if (adapter == null)
        {
            return new OrderActionResult(false, $"No adapter found for {order.DeliveryPlatform.PlatformType}");
        }

        var result = await adapter.CancelOrderAsync(order.DeliveryPlatform, order.PlatformOrderId, reason, cancellationToken);

        if (result.Success)
        {
            order.Status = ExternalOrderStatus.Cancelled;
            order.ErrorMessage = $"{cancelledBy}: {reason}";
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Cancelled order {OrderId} by {CancelledBy}: {Reason}", orderId, cancelledBy, reason);
        }

        return result;
    }

    public async Task<OrderActionResult> AdjustPrepTimeAsync(Guid orderId, int newPrepTimeMinutes, CancellationToken cancellationToken = default)
    {
        var order = await GetByIdAsync(orderId, cancellationToken);
        if (order == null)
        {
            return new OrderActionResult(false, "Order not found");
        }

        if (order.Status != ExternalOrderStatus.Accepted && order.Status != ExternalOrderStatus.Preparing)
        {
            return new OrderActionResult(false, $"Cannot adjust prep time for order in {order.Status} status");
        }

        var adapter = _adapterFactory.GetAdapter(order.DeliveryPlatform.PlatformType);
        if (adapter == null)
        {
            return new OrderActionResult(false, $"No adapter found for {order.DeliveryPlatform.PlatformType}");
        }

        var result = await adapter.UpdatePrepTimeAsync(order.DeliveryPlatform, order.PlatformOrderId, newPrepTimeMinutes, cancellationToken);

        if (result.Success)
        {
            order.EstimatedPickupAt = DateTime.UtcNow.AddMinutes(newPrepTimeMinutes);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Adjusted prep time for order {OrderId} to {PrepTime} minutes", orderId, newPrepTimeMinutes);
        }

        return result;
    }

    public async Task UpdateStatusAsync(Guid orderId, ExternalOrderStatus status, CancellationToken cancellationToken = default)
    {
        var order = await _context.ExternalOrders.FindAsync(new object[] { orderId }, cancellationToken);
        if (order != null)
        {
            order.Status = status;

            if (status == ExternalOrderStatus.PickedUp)
            {
                order.ActualPickupAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Updated order {OrderId} status to {Status}", orderId, status);
        }
    }
}
