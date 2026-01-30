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
/// Controller for managing external orders from delivery platforms.
/// </summary>
[ApiController]
[Route("api/external-orders")]
public class ExternalOrdersController : ControllerBase
{
    private readonly OrdersGatewayDbContext _context;
    private readonly IExternalOrderService _orderService;

    public ExternalOrdersController(
        OrdersGatewayDbContext context,
        IExternalOrderService orderService)
    {
        _context = context;
        _orderService = orderService;
    }

    /// <summary>
    /// List external orders with filtering.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HalCollection<ExternalOrderDto>>> GetAll(
        [FromQuery] Guid? locationId = null,
        [FromQuery] Guid? platformId = null,
        [FromQuery] ExternalOrderStatus? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var query = _context.ExternalOrders
            .Include(o => o.DeliveryPlatform)
            .AsQueryable();

        if (locationId.HasValue)
        {
            query = query.Where(o => o.LocationId == locationId.Value);
        }

        if (platformId.HasValue)
        {
            query = query.Where(o => o.DeliveryPlatformId == platformId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(o => o.PlacedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(o => o.PlacedAt <= toDate.Value);
        }

        var total = await query.CountAsync();

        var orders = await query
            .OrderByDescending(o => o.PlacedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = orders.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            AddOrderLinks(dto);
        }

        return Ok(HalCollection<ExternalOrderDto>.Create(dtos, "/api/external-orders", total));
    }

    /// <summary>
    /// Get a specific external order.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExternalOrderDto>> Get(Guid id)
    {
        var order = await _orderService.GetByIdAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        var dto = MapToDto(order);
        AddOrderLinks(dto);

        return Ok(dto);
    }

    /// <summary>
    /// Get external orders by location.
    /// </summary>
    [HttpGet("~/api/locations/{locationId:guid}/external-orders")]
    public async Task<ActionResult<HalCollection<ExternalOrderDto>>> GetByLocation(
        Guid locationId,
        [FromQuery] ExternalOrderStatus? status = null,
        [FromQuery] int limit = 50)
    {
        var orders = await _orderService.GetByLocationAsync(locationId, status, limit);
        var dtos = orders.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            AddOrderLinks(dto);
        }

        return Ok(HalCollection<ExternalOrderDto>.Create(dtos, $"/api/locations/{locationId}/external-orders", orders.Count));
    }

    /// <summary>
    /// Accept an external order.
    /// </summary>
    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, [FromBody] AcceptOrderRequest request)
    {
        var result = await _orderService.AcceptOrderAsync(id, request.PrepTimeMinutes);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return NoContent();
    }

    /// <summary>
    /// Reject an external order.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectOrderRequest request)
    {
        var result = await _orderService.RejectOrderAsync(id, request.Reason);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return NoContent();
    }

    /// <summary>
    /// Mark an external order as ready for pickup.
    /// </summary>
    [HttpPost("{id:guid}/ready")]
    public async Task<IActionResult> MarkReady(Guid id)
    {
        var result = await _orderService.MarkReadyAsync(id);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return NoContent();
    }

    /// <summary>
    /// Cancel an external order.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelOrderRequest request)
    {
        var result = await _orderService.CancelOrderAsync(id, request.Reason, request.CancelledBy);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return NoContent();
    }

    /// <summary>
    /// Adjust preparation time for an order.
    /// </summary>
    [HttpPost("{id:guid}/adjust-time")]
    public async Task<IActionResult> AdjustPrepTime(Guid id, [FromBody] AdjustPrepTimeRequest request)
    {
        var result = await _orderService.AdjustPrepTimeAsync(id, request.NewPrepTimeMinutes);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return NoContent();
    }

    /// <summary>
    /// Get delivery tracking for an order.
    /// </summary>
    [HttpGet("{id:guid}/tracking")]
    public async Task<ActionResult<OrderTrackingDto>> GetTracking(Guid id)
    {
        var order = await _orderService.GetByIdAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        // TODO: Implement actual tracking by calling the adapter
        var tracking = new OrderTrackingDto
        {
            ExternalOrderId = order.Id,
            PlatformOrderId = order.PlatformOrderId,
            Status = order.Status,
            LastUpdatedAt = order.UpdatedAt
        };

        tracking.AddSelfLink($"/api/external-orders/{id}/tracking");

        return Ok(tracking);
    }

    private static ExternalOrderDto MapToDto(ExternalOrder order)
    {
        return new ExternalOrderDto
        {
            Id = order.Id,
            TenantId = order.TenantId,
            LocationId = order.LocationId,
            DeliveryPlatformId = order.DeliveryPlatformId,
            PlatformType = order.DeliveryPlatform?.PlatformType ?? string.Empty,
            PlatformOrderId = order.PlatformOrderId,
            PlatformOrderNumber = order.PlatformOrderNumber,
            InternalOrderId = order.InternalOrderId,
            Status = order.Status,
            OrderType = order.OrderType,
            PlacedAt = order.PlacedAt,
            AcceptedAt = order.AcceptedAt,
            EstimatedPickupAt = order.EstimatedPickupAt,
            ActualPickupAt = order.ActualPickupAt,
            Customer = JsonSerializer.Deserialize<ExternalCustomer>(order.Customer),
            Items = JsonSerializer.Deserialize<List<ExternalOrderItem>>(order.Items) ?? new List<ExternalOrderItem>(),
            Subtotal = order.Subtotal,
            DeliveryFee = order.DeliveryFee,
            ServiceFee = order.ServiceFee,
            Tax = order.Tax,
            Tip = order.Tip,
            Total = order.Total,
            Currency = order.Currency,
            SpecialInstructions = order.SpecialInstructions,
            ErrorMessage = order.ErrorMessage,
            RetryCount = order.RetryCount,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt
        };
    }

    private static void AddOrderLinks(ExternalOrderDto dto)
    {
        dto.AddSelfLink($"/api/external-orders/{dto.Id}");
        dto.AddLink("platform", $"/api/delivery-platforms/{dto.DeliveryPlatformId}");
        dto.AddLink("tracking", $"/api/external-orders/{dto.Id}/tracking");

        if (dto.Status == ExternalOrderStatus.Pending)
        {
            dto.AddLink("accept", $"/api/external-orders/{dto.Id}/accept");
            dto.AddLink("reject", $"/api/external-orders/{dto.Id}/reject");
        }

        if (dto.Status == ExternalOrderStatus.Accepted || dto.Status == ExternalOrderStatus.Preparing)
        {
            dto.AddLink("ready", $"/api/external-orders/{dto.Id}/ready");
            dto.AddLink("adjust-time", $"/api/external-orders/{dto.Id}/adjust-time");
            dto.AddLink("cancel", $"/api/external-orders/{dto.Id}/cancel");
        }

        if (dto.InternalOrderId.HasValue)
        {
            dto.AddLink("internal-order", $"/api/orders/{dto.InternalOrderId}");
        }
    }
}
