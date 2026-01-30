using DarkVelocity.OrdersGateway.Api.Data;
using DarkVelocity.OrdersGateway.Api.Dtos;
using DarkVelocity.OrdersGateway.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.OrdersGateway.Api.Controllers;

/// <summary>
/// Controller for delivery platform analytics.
/// </summary>
[ApiController]
[Route("api/delivery-analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly OrdersGatewayDbContext _context;

    public AnalyticsController(OrdersGatewayDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get summary analytics across all platforms.
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<DeliveryAnalyticsSummaryDto>> GetSummary(
        [FromQuery] Guid? locationId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null)
    {
        var from = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var to = toDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _context.ExternalOrders
            .Where(o => DateOnly.FromDateTime(o.PlacedAt) >= from && DateOnly.FromDateTime(o.PlacedAt) <= to);

        if (locationId.HasValue)
        {
            query = query.Where(o => o.LocationId == locationId.Value);
        }

        var orders = await query.ToListAsync();

        var totalOrders = orders.Count;
        var acceptedOrders = orders.Count(o => o.Status == ExternalOrderStatus.Accepted ||
                                                o.Status == ExternalOrderStatus.Preparing ||
                                                o.Status == ExternalOrderStatus.Ready ||
                                                o.Status == ExternalOrderStatus.PickedUp ||
                                                o.Status == ExternalOrderStatus.Delivered);
        var rejectedOrders = orders.Count(o => o.Status == ExternalOrderStatus.Rejected);
        var cancelledOrders = orders.Count(o => o.Status == ExternalOrderStatus.Cancelled);
        var totalRevenue = orders.Where(o => o.Status != ExternalOrderStatus.Rejected &&
                                              o.Status != ExternalOrderStatus.Cancelled &&
                                              o.Status != ExternalOrderStatus.Failed)
                                 .Sum(o => o.Total);

        var avgPrepTime = orders.Where(o => o.AcceptedAt.HasValue && o.EstimatedPickupAt.HasValue)
                                .Select(o => (o.EstimatedPickupAt!.Value - o.AcceptedAt!.Value).TotalMinutes)
                                .DefaultIfEmpty(0)
                                .Average();

        var dto = new DeliveryAnalyticsSummaryDto
        {
            PeriodStart = from,
            PeriodEnd = to,
            TotalOrders = totalOrders,
            AcceptedOrders = acceptedOrders,
            RejectedOrders = rejectedOrders,
            CancelledOrders = cancelledOrders,
            TotalRevenue = totalRevenue,
            AverageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0,
            AveragePrepTimeMinutes = (int)avgPrepTime,
            AcceptanceRate = totalOrders > 0 ? (decimal)acceptedOrders / totalOrders * 100 : 0,
            CancellationRate = totalOrders > 0 ? (decimal)cancelledOrders / totalOrders * 100 : 0,
            Currency = orders.FirstOrDefault()?.Currency ?? "EUR"
        };

        dto.AddSelfLink("/api/delivery-analytics/summary");

        return Ok(dto);
    }

    /// <summary>
    /// Get analytics broken down by platform.
    /// </summary>
    [HttpGet("by-platform")]
    public async Task<ActionResult<HalCollection<PlatformAnalyticsDto>>> GetByPlatform(
        [FromQuery] Guid? locationId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null)
    {
        var from = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var to = toDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _context.ExternalOrders
            .Include(o => o.DeliveryPlatform)
            .Where(o => DateOnly.FromDateTime(o.PlacedAt) >= from && DateOnly.FromDateTime(o.PlacedAt) <= to);

        if (locationId.HasValue)
        {
            query = query.Where(o => o.LocationId == locationId.Value);
        }

        var ordersByPlatform = await query
            .GroupBy(o => new { o.DeliveryPlatformId, o.DeliveryPlatform.PlatformType, o.DeliveryPlatform.Name })
            .Select(g => new
            {
                g.Key.DeliveryPlatformId,
                g.Key.PlatformType,
                g.Key.Name,
                Orders = g.ToList()
            })
            .ToListAsync();

        var dtos = ordersByPlatform.Select(g =>
        {
            var totalOrders = g.Orders.Count;
            var acceptedOrders = g.Orders.Count(o => o.Status == ExternalOrderStatus.Accepted ||
                                                      o.Status == ExternalOrderStatus.Preparing ||
                                                      o.Status == ExternalOrderStatus.Ready ||
                                                      o.Status == ExternalOrderStatus.PickedUp ||
                                                      o.Status == ExternalOrderStatus.Delivered);
            var rejectedOrders = g.Orders.Count(o => o.Status == ExternalOrderStatus.Rejected);
            var cancelledOrders = g.Orders.Count(o => o.Status == ExternalOrderStatus.Cancelled);
            var completedOrders = g.Orders.Where(o => o.Status != ExternalOrderStatus.Rejected &&
                                                       o.Status != ExternalOrderStatus.Cancelled &&
                                                       o.Status != ExternalOrderStatus.Failed);
            var totalRevenue = completedOrders.Sum(o => o.Total);

            return new PlatformAnalyticsDto
            {
                DeliveryPlatformId = g.DeliveryPlatformId,
                PlatformType = g.PlatformType,
                PlatformName = g.Name,
                PeriodStart = from,
                PeriodEnd = to,
                TotalOrders = totalOrders,
                AcceptedOrders = acceptedOrders,
                RejectedOrders = rejectedOrders,
                CancelledOrders = cancelledOrders,
                TotalRevenue = totalRevenue,
                AverageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0,
                AveragePrepTimeMinutes = 0, // Would need more data to calculate
                AcceptanceRate = totalOrders > 0 ? (decimal)acceptedOrders / totalOrders * 100 : 0,
                TotalCommissions = 0, // Would come from payouts
                NetRevenue = totalRevenue, // Simplified
                Currency = g.Orders.FirstOrDefault()?.Currency ?? "EUR"
            };
        }).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/delivery-analytics/by-platform?platformId={dto.DeliveryPlatformId}");
            dto.AddLink("platform", $"/api/delivery-platforms/{dto.DeliveryPlatformId}");
        }

        return Ok(HalCollection<PlatformAnalyticsDto>.Create(dtos, "/api/delivery-analytics/by-platform", dtos.Count));
    }

    /// <summary>
    /// Get revenue analytics by platform.
    /// </summary>
    [HttpGet("revenue")]
    public async Task<ActionResult<RevenueByPlatformDto>> GetRevenue(
        [FromQuery] Guid? locationId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null)
    {
        var from = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var to = toDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _context.ExternalOrders
            .Include(o => o.DeliveryPlatform)
            .Where(o => DateOnly.FromDateTime(o.PlacedAt) >= from && DateOnly.FromDateTime(o.PlacedAt) <= to)
            .Where(o => o.Status != ExternalOrderStatus.Rejected &&
                        o.Status != ExternalOrderStatus.Cancelled &&
                        o.Status != ExternalOrderStatus.Failed);

        if (locationId.HasValue)
        {
            query = query.Where(o => o.LocationId == locationId.Value);
        }

        var ordersByPlatform = await query
            .GroupBy(o => new { o.DeliveryPlatform.PlatformType, o.DeliveryPlatform.Name })
            .Select(g => new
            {
                g.Key.PlatformType,
                g.Key.Name,
                GrossRevenue = g.Sum(o => o.Total),
                OrderCount = g.Count()
            })
            .ToListAsync();

        var totalGross = ordersByPlatform.Sum(p => p.GrossRevenue);

        var platforms = ordersByPlatform.Select(p => new PlatformRevenueItem
        {
            PlatformType = p.PlatformType,
            PlatformName = p.Name,
            GrossRevenue = p.GrossRevenue,
            Commissions = 0, // Would need payout data
            NetRevenue = p.GrossRevenue, // Simplified
            OrderCount = p.OrderCount,
            PercentageOfTotal = totalGross > 0 ? p.GrossRevenue / totalGross * 100 : 0
        }).ToList();

        var dto = new RevenueByPlatformDto
        {
            PeriodStart = from,
            PeriodEnd = to,
            Platforms = platforms,
            TotalGrossRevenue = totalGross,
            TotalCommissions = 0,
            TotalNetRevenue = totalGross,
            Currency = "EUR"
        };

        dto.AddSelfLink("/api/delivery-analytics/revenue");

        return Ok(dto);
    }

    /// <summary>
    /// Get preparation time metrics.
    /// </summary>
    [HttpGet("prep-times")]
    public async Task<ActionResult<PrepTimeMetricsDto>> GetPrepTimes(
        [FromQuery] Guid? locationId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null)
    {
        var from = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var to = toDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _context.ExternalOrders
            .Include(o => o.DeliveryPlatform)
            .Where(o => DateOnly.FromDateTime(o.PlacedAt) >= from && DateOnly.FromDateTime(o.PlacedAt) <= to)
            .Where(o => o.AcceptedAt.HasValue && o.EstimatedPickupAt.HasValue);

        if (locationId.HasValue)
        {
            query = query.Where(o => o.LocationId == locationId.Value);
        }

        var orders = await query.ToListAsync();

        var prepTimes = orders.Select(o => (int)(o.EstimatedPickupAt!.Value - o.AcceptedAt!.Value).TotalMinutes).ToList();
        var sortedPrepTimes = prepTimes.OrderBy(t => t).ToList();

        var targetPrepTime = 30; // configurable
        var ordersWithinTarget = prepTimes.Count(t => t <= targetPrepTime);

        var dto = new PrepTimeMetricsDto
        {
            PeriodStart = from,
            PeriodEnd = to,
            AveragePrepTimeMinutes = prepTimes.Any() ? (int)prepTimes.Average() : 0,
            MedianPrepTimeMinutes = sortedPrepTimes.Any() ? sortedPrepTimes[sortedPrepTimes.Count / 2] : 0,
            MinPrepTimeMinutes = prepTimes.Any() ? prepTimes.Min() : 0,
            MaxPrepTimeMinutes = prepTimes.Any() ? prepTimes.Max() : 0,
            OrdersWithinTarget = ordersWithinTarget,
            TotalOrders = orders.Count,
            OnTimePercentage = orders.Count > 0 ? (decimal)ordersWithinTarget / orders.Count * 100 : 0,
            ByPlatform = orders.GroupBy(o => o.DeliveryPlatform.PlatformType)
                              .Select(g => new PrepTimeByPlatform
                              {
                                  PlatformType = g.Key,
                                  AveragePrepTimeMinutes = (int)g.Select(o => (o.EstimatedPickupAt!.Value - o.AcceptedAt!.Value).TotalMinutes).Average(),
                                  OrderCount = g.Count()
                              }).ToList(),
            ByHour = orders.GroupBy(o => o.PlacedAt.Hour)
                          .Select(g => new PrepTimeByHour
                          {
                              Hour = g.Key,
                              AveragePrepTimeMinutes = (int)g.Select(o => (o.EstimatedPickupAt!.Value - o.AcceptedAt!.Value).TotalMinutes).Average(),
                              OrderCount = g.Count()
                          })
                          .OrderBy(h => h.Hour)
                          .ToList()
        };

        dto.AddSelfLink("/api/delivery-analytics/prep-times");

        return Ok(dto);
    }
}
