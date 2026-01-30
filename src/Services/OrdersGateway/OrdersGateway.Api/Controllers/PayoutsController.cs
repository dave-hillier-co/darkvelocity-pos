using System.Text.Json;
using DarkVelocity.OrdersGateway.Api.Data;
using DarkVelocity.OrdersGateway.Api.Dtos;
using DarkVelocity.OrdersGateway.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.OrdersGateway.Api.Controllers;

/// <summary>
/// Controller for platform payout reconciliation.
/// </summary>
[ApiController]
[Route("api/platform-payouts")]
public class PayoutsController : ControllerBase
{
    private readonly OrdersGatewayDbContext _context;

    public PayoutsController(OrdersGatewayDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// List platform payouts.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HalCollection<PlatformPayoutDto>>> GetAll(
        [FromQuery] Guid? platformId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] PayoutStatus? status = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] int limit = 50)
    {
        var query = _context.PlatformPayouts
            .Include(p => p.DeliveryPlatform)
            .AsQueryable();

        if (platformId.HasValue)
        {
            query = query.Where(p => p.DeliveryPlatformId == platformId.Value);
        }

        if (locationId.HasValue)
        {
            query = query.Where(p => p.LocationId == locationId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(p => p.PeriodEnd >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(p => p.PeriodStart <= toDate.Value);
        }

        var payouts = await query
            .OrderByDescending(p => p.ReceivedAt)
            .Take(limit)
            .ToListAsync();

        var dtos = payouts.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/platform-payouts/{dto.Id}");
        }

        return Ok(HalCollection<PlatformPayoutDto>.Create(dtos, "/api/platform-payouts", payouts.Count));
    }

    /// <summary>
    /// Get pending payouts for reconciliation.
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<HalCollection<PlatformPayoutDto>>> GetPending([FromQuery] Guid? locationId = null)
    {
        var query = _context.PlatformPayouts
            .Include(p => p.DeliveryPlatform)
            .Where(p => p.Status == PayoutStatus.Pending);

        if (locationId.HasValue)
        {
            query = query.Where(p => p.LocationId == locationId.Value);
        }

        var payouts = await query
            .OrderBy(p => p.ReceivedAt)
            .ToListAsync();

        var dtos = payouts.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/platform-payouts/{dto.Id}");
            dto.AddLink("reconcile", $"/api/platform-payouts/{dto.Id}/reconcile");
        }

        return Ok(HalCollection<PlatformPayoutDto>.Create(dtos, "/api/platform-payouts/pending", payouts.Count));
    }

    /// <summary>
    /// Get a specific payout.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlatformPayoutDto>> Get(Guid id)
    {
        var payout = await _context.PlatformPayouts
            .Include(p => p.DeliveryPlatform)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payout == null)
        {
            return NotFound();
        }

        var dto = MapToDto(payout);
        dto.AddSelfLink($"/api/platform-payouts/{id}");
        dto.AddLink("platform", $"/api/delivery-platforms/{payout.DeliveryPlatformId}");

        if (payout.Status == PayoutStatus.Pending)
        {
            dto.AddLink("reconcile", $"/api/platform-payouts/{id}/reconcile");
        }

        return Ok(dto);
    }

    /// <summary>
    /// Mark a payout as reconciled.
    /// </summary>
    [HttpPost("{id:guid}/reconcile")]
    public async Task<IActionResult> Reconcile(Guid id, [FromBody] ReconcilePayoutRequest request)
    {
        var payout = await _context.PlatformPayouts.FindAsync(id);
        if (payout == null)
        {
            return NotFound();
        }

        if (payout.Status != PayoutStatus.Pending)
        {
            return BadRequest("Payout has already been processed");
        }

        payout.Status = PayoutStatus.Reconciled;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Mark a payout as disputed.
    /// </summary>
    [HttpPost("{id:guid}/dispute")]
    public async Task<IActionResult> Dispute(Guid id, [FromBody] DisputePayoutRequest request)
    {
        var payout = await _context.PlatformPayouts.FindAsync(id);
        if (payout == null)
        {
            return NotFound();
        }

        payout.Status = PayoutStatus.Disputed;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static PlatformPayoutDto MapToDto(PlatformPayout payout)
    {
        var orderIds = !string.IsNullOrEmpty(payout.OrderIds)
            ? JsonSerializer.Deserialize<List<Guid>>(payout.OrderIds) ?? new List<Guid>()
            : new List<Guid>();

        return new PlatformPayoutDto
        {
            Id = payout.Id,
            TenantId = payout.TenantId,
            DeliveryPlatformId = payout.DeliveryPlatformId,
            PlatformType = payout.DeliveryPlatform?.PlatformType ?? string.Empty,
            LocationId = payout.LocationId,
            PayoutReference = payout.PayoutReference,
            PeriodStart = payout.PeriodStart,
            PeriodEnd = payout.PeriodEnd,
            GrossAmount = payout.GrossAmount,
            Commissions = payout.Commissions,
            Fees = payout.Fees,
            Adjustments = payout.Adjustments,
            NetAmount = payout.NetAmount,
            Currency = payout.Currency,
            Status = payout.Status,
            ReceivedAt = payout.ReceivedAt,
            OrderCount = orderIds.Count,
            CreatedAt = payout.CreatedAt,
            UpdatedAt = payout.UpdatedAt
        };
    }
}
