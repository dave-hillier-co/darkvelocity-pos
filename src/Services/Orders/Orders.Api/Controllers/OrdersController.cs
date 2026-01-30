using DarkVelocity.Orders.Api.Data;
using DarkVelocity.Orders.Api.Dtos;
using DarkVelocity.Orders.Api.Entities;
using DarkVelocity.Orders.Api.Services;
using DarkVelocity.Shared.Contracts.Events;
using DarkVelocity.Shared.Contracts.Hal;
using DarkVelocity.Shared.Infrastructure.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Orders.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/orders")]
public class OrdersController : ControllerBase
{
    private readonly OrdersDbContext _context;
    private readonly IOrderNumberGenerator _orderNumberGenerator;
    private readonly IEventBus _eventBus;

    public OrdersController(OrdersDbContext context, IOrderNumberGenerator orderNumberGenerator, IEventBus eventBus)
    {
        _context = context;
        _orderNumberGenerator = orderNumberGenerator;
        _eventBus = eventBus;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<OrderDto>>> GetAll(
        Guid locationId,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50)
    {
        var query = _context.Orders
            .Include(o => o.Lines)
            .Where(o => o.LocationId == locationId);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(o => o.Status == status);
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Take(limit)
            .ToListAsync();

        var dtos = orders.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/locations/{locationId}/orders/{dto.Id}");
        }

        return Ok(HalCollection<OrderDto>.Create(
            dtos,
            $"/api/locations/{locationId}/orders",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid locationId, Guid id)
    {
        var order = await _context.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id && o.LocationId == locationId);

        if (order == null)
            return NotFound();

        var dto = MapToDto(order);
        dto.AddSelfLink($"/api/locations/{locationId}/orders/{order.Id}");
        dto.AddLink("lines", $"/api/locations/{locationId}/orders/{order.Id}/lines");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create(Guid locationId, [FromBody] CreateOrderRequest request)
    {
        var currentPeriod = await _context.SalesPeriods
            .FirstOrDefaultAsync(p => p.LocationId == locationId && p.Status == "open");

        var orderNumber = await _orderNumberGenerator.GenerateAsync(locationId);

        var order = new Order
        {
            LocationId = locationId,
            SalesPeriodId = currentPeriod?.Id,
            UserId = request.UserId,
            OrderNumber = orderNumber,
            OrderType = request.OrderType,
            Status = "open",
            TableId = request.TableId,
            GuestCount = request.GuestCount,
            CustomerName = request.CustomerName
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        await _eventBus.PublishAsync(new OrderCreated(
            OrderId: order.Id,
            LocationId: order.LocationId,
            UserId: order.UserId,
            OrderNumber: order.OrderNumber,
            OrderType: order.OrderType,
            TableId: order.TableId
        ));

        var dto = MapToDto(order);
        dto.AddSelfLink($"/api/locations/{locationId}/orders/{order.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, id = order.Id }, dto);
    }

    [HttpPost("{id:guid}/lines")]
    public async Task<ActionResult<OrderLineDto>> AddLine(Guid locationId, Guid id, [FromBody] AddOrderLineRequest request)
    {
        var order = await _context.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id && o.LocationId == locationId);

        if (order == null)
            return NotFound();

        if (order.Status != "open")
            return BadRequest(new { message = "Cannot add lines to a non-open order" });

        var lineTotal = (request.Quantity * request.UnitPrice);

        var line = new OrderLine
        {
            OrderId = id,
            MenuItemId = request.MenuItemId,
            ItemName = request.ItemName,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            TaxRate = request.TaxRate,
            LineTotal = lineTotal,
            Notes = request.Notes,
            CourseNumber = request.CourseNumber,
            SeatNumber = request.SeatNumber
        };

        _context.OrderLines.Add(line);
        await _context.SaveChangesAsync();

        await _eventBus.PublishAsync(new OrderLineAdded(
            OrderId: id,
            LineId: line.Id,
            MenuItemId: line.MenuItemId,
            ItemName: line.ItemName,
            Quantity: line.Quantity,
            UnitPrice: line.UnitPrice,
            LineTotal: line.LineTotal
        ));

        // Reload order with lines for recalculation
        await _context.Entry(order).Collection(o => o.Lines).LoadAsync();
        RecalculateTotals(order);
        await _context.SaveChangesAsync();

        var dto = new OrderLineDto
        {
            Id = line.Id,
            OrderId = line.OrderId,
            MenuItemId = line.MenuItemId,
            ItemName = line.ItemName,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            DiscountAmount = line.DiscountAmount,
            TaxRate = line.TaxRate,
            LineTotal = line.LineTotal,
            Notes = line.Notes,
            CourseNumber = line.CourseNumber,
            SeatNumber = line.SeatNumber,
            IsVoided = line.IsVoided
        };

        return Created($"/api/locations/{locationId}/orders/{id}/lines/{line.Id}", dto);
    }

    [HttpPatch("{orderId:guid}/lines/{lineId:guid}")]
    public async Task<ActionResult<OrderLineDto>> UpdateLine(
        Guid locationId,
        Guid orderId,
        Guid lineId,
        [FromBody] UpdateOrderLineRequest request)
    {
        var order = await _context.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.LocationId == locationId);

        if (order == null)
            return NotFound();

        var line = order.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line == null)
            return NotFound();

        if (order.Status != "open")
            return BadRequest(new { message = "Cannot modify lines on a non-open order" });

        if (request.Quantity.HasValue)
        {
            line.Quantity = request.Quantity.Value;
        }

        if (request.DiscountAmount.HasValue)
        {
            line.DiscountAmount = request.DiscountAmount.Value;
            line.DiscountReason = request.DiscountReason;
        }

        if (request.Notes != null)
        {
            line.Notes = request.Notes;
        }

        line.LineTotal = (line.Quantity * line.UnitPrice) - line.DiscountAmount;
        RecalculateTotals(order);
        await _context.SaveChangesAsync();

        var dto = new OrderLineDto
        {
            Id = line.Id,
            OrderId = line.OrderId,
            MenuItemId = line.MenuItemId,
            ItemName = line.ItemName,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            DiscountAmount = line.DiscountAmount,
            DiscountReason = line.DiscountReason,
            TaxRate = line.TaxRate,
            LineTotal = line.LineTotal,
            Notes = line.Notes,
            CourseNumber = line.CourseNumber,
            SeatNumber = line.SeatNumber,
            IsVoided = line.IsVoided
        };

        return Ok(dto);
    }

    [HttpDelete("{orderId:guid}/lines/{lineId:guid}")]
    public async Task<IActionResult> RemoveLine(Guid locationId, Guid orderId, Guid lineId)
    {
        var order = await _context.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.LocationId == locationId);

        if (order == null)
            return NotFound();

        var line = order.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line == null)
            return NotFound();

        if (order.Status != "open")
            return BadRequest(new { message = "Cannot remove lines from a non-open order" });

        order.Lines.Remove(line);
        _context.OrderLines.Remove(line);
        RecalculateTotals(order);
        await _context.SaveChangesAsync();

        await _eventBus.PublishAsync(new OrderLineRemoved(
            OrderId: orderId,
            LineId: lineId
        ));

        return NoContent();
    }

    [HttpPost("{id:guid}/send")]
    public async Task<ActionResult<OrderDto>> Send(Guid locationId, Guid id)
    {
        var order = await _context.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id && o.LocationId == locationId);

        if (order == null)
            return NotFound();

        if (order.Status != "open")
            return BadRequest(new { message = "Order is not open" });

        if (!order.Lines.Any())
            return BadRequest(new { message = "Cannot send an empty order" });

        order.Status = "sent";
        order.SentAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = MapToDto(order);
        dto.AddSelfLink($"/api/locations/{locationId}/orders/{order.Id}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<OrderDto>> Complete(Guid locationId, Guid id)
    {
        var order = await _context.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id && o.LocationId == locationId);

        if (order == null)
            return NotFound();

        if (order.Status == "completed")
            return BadRequest(new { message = "Order is already completed" });

        if (order.Status == "voided")
            return BadRequest(new { message = "Cannot complete a voided order" });

        order.Status = "completed";
        order.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var lines = order.Lines.Where(l => !l.IsVoided).Select(l => new OrderLineSnapshot(
            LineId: l.Id,
            MenuItemId: l.MenuItemId,
            ItemName: l.ItemName,
            Quantity: l.Quantity,
            UnitPrice: l.UnitPrice,
            LineTotal: l.LineTotal
        )).ToList();

        await _eventBus.PublishAsync(new OrderCompleted(
            OrderId: order.Id,
            LocationId: order.LocationId,
            OrderNumber: order.OrderNumber,
            GrandTotal: order.GrandTotal,
            Lines: lines
        ));

        var dto = MapToDto(order);
        dto.AddSelfLink($"/api/locations/{locationId}/orders/{order.Id}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/void")]
    public async Task<ActionResult<OrderDto>> Void(Guid locationId, Guid id, [FromBody] VoidOrderRequest request)
    {
        var order = await _context.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id && o.LocationId == locationId);

        if (order == null)
            return NotFound();

        if (order.Status == "voided")
            return BadRequest(new { message = "Order is already voided" });

        order.Status = "voided";
        order.VoidedAt = DateTime.UtcNow;
        order.VoidedByUserId = request.UserId;
        order.VoidReason = request.Reason;
        await _context.SaveChangesAsync();

        await _eventBus.PublishAsync(new OrderVoided(
            OrderId: order.Id,
            UserId: request.UserId,
            Reason: request.Reason
        ));

        var dto = MapToDto(order);
        dto.AddSelfLink($"/api/locations/{locationId}/orders/{order.Id}");

        return Ok(dto);
    }

    private void RecalculateTotals(Order order)
    {
        var activeLines = order.Lines.Where(l => !l.IsVoided).ToList();

        order.Subtotal = activeLines.Sum(l => l.Quantity * l.UnitPrice);
        order.DiscountTotal = activeLines.Sum(l => l.DiscountAmount);
        order.TaxTotal = activeLines.Sum(l => l.LineTotal * l.TaxRate);
        order.GrandTotal = activeLines.Sum(l => l.LineTotal) + order.TaxTotal;
    }

    private static OrderDto MapToDto(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            LocationId = order.LocationId,
            SalesPeriodId = order.SalesPeriodId,
            UserId = order.UserId,
            OrderNumber = order.OrderNumber,
            OrderType = order.OrderType,
            Status = order.Status,
            TableId = order.TableId,
            GuestCount = order.GuestCount,
            CustomerName = order.CustomerName,
            Subtotal = order.Subtotal,
            DiscountTotal = order.DiscountTotal,
            TaxTotal = order.TaxTotal,
            GrandTotal = order.GrandTotal,
            SentAt = order.SentAt,
            CompletedAt = order.CompletedAt,
            CreatedAt = order.CreatedAt,
            Lines = order.Lines.Select(l => new OrderLineDto
            {
                Id = l.Id,
                OrderId = l.OrderId,
                MenuItemId = l.MenuItemId,
                ItemName = l.ItemName,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                DiscountAmount = l.DiscountAmount,
                DiscountReason = l.DiscountReason,
                TaxRate = l.TaxRate,
                LineTotal = l.LineTotal,
                Notes = l.Notes,
                CourseNumber = l.CourseNumber,
                SeatNumber = l.SeatNumber,
                IsVoided = l.IsVoided
            }).ToList()
        };
    }
}
