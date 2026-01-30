using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.OrdersGateway.Api.Dtos;

/// <summary>
/// Summary analytics across all delivery platforms.
/// </summary>
public class DeliveryAnalyticsSummaryDto : HalResource
{
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public int TotalOrders { get; set; }
    public int AcceptedOrders { get; set; }
    public int RejectedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int AveragePrepTimeMinutes { get; set; }
    public decimal AcceptanceRate { get; set; }
    public decimal CancellationRate { get; set; }
    public string Currency { get; set; } = "EUR";
}

/// <summary>
/// Analytics broken down by platform.
/// </summary>
public class PlatformAnalyticsDto : HalResource
{
    public Guid DeliveryPlatformId { get; set; }
    public string PlatformType { get; set; } = string.Empty;
    public string PlatformName { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public int TotalOrders { get; set; }
    public int AcceptedOrders { get; set; }
    public int RejectedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int AveragePrepTimeMinutes { get; set; }
    public decimal AcceptanceRate { get; set; }
    public decimal TotalCommissions { get; set; }
    public decimal NetRevenue { get; set; }
    public string Currency { get; set; } = "EUR";
}

/// <summary>
/// Revenue analytics by platform.
/// </summary>
public class RevenueByPlatformDto : HalResource
{
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public List<PlatformRevenueItem> Platforms { get; set; } = new();
    public decimal TotalGrossRevenue { get; set; }
    public decimal TotalCommissions { get; set; }
    public decimal TotalNetRevenue { get; set; }
    public string Currency { get; set; } = "EUR";
}

/// <summary>
/// Revenue data for a single platform.
/// </summary>
public class PlatformRevenueItem
{
    public string PlatformType { get; set; } = string.Empty;
    public string PlatformName { get; set; } = string.Empty;
    public decimal GrossRevenue { get; set; }
    public decimal Commissions { get; set; }
    public decimal NetRevenue { get; set; }
    public int OrderCount { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

/// <summary>
/// Preparation time metrics.
/// </summary>
public class PrepTimeMetricsDto : HalResource
{
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public int AveragePrepTimeMinutes { get; set; }
    public int MedianPrepTimeMinutes { get; set; }
    public int MinPrepTimeMinutes { get; set; }
    public int MaxPrepTimeMinutes { get; set; }
    public int OrdersWithinTarget { get; set; }
    public int TotalOrders { get; set; }
    public decimal OnTimePercentage { get; set; }
    public List<PrepTimeByPlatform> ByPlatform { get; set; } = new();
    public List<PrepTimeByHour> ByHour { get; set; } = new();
}

/// <summary>
/// Prep time data for a single platform.
/// </summary>
public class PrepTimeByPlatform
{
    public string PlatformType { get; set; } = string.Empty;
    public int AveragePrepTimeMinutes { get; set; }
    public int OrderCount { get; set; }
}

/// <summary>
/// Prep time data for a specific hour of day.
/// </summary>
public class PrepTimeByHour
{
    public int Hour { get; set; }
    public int AveragePrepTimeMinutes { get; set; }
    public int OrderCount { get; set; }
}
