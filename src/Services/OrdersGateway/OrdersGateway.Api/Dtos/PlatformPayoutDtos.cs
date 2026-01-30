using DarkVelocity.OrdersGateway.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.OrdersGateway.Api.Dtos;

/// <summary>
/// Response DTO for a platform payout.
/// </summary>
public class PlatformPayoutDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DeliveryPlatformId { get; set; }
    public string PlatformType { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string PayoutReference { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal Commissions { get; set; }
    public decimal Fees { get; set; }
    public decimal Adjustments { get; set; }
    public decimal NetAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public PayoutStatus Status { get; set; }
    public DateTime ReceivedAt { get; set; }
    public int OrderCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request to reconcile a payout.
/// </summary>
public record ReconcilePayoutRequest(
    string? Notes = null);

/// <summary>
/// Request to dispute a payout.
/// </summary>
public record DisputePayoutRequest(
    string Reason,
    decimal? ExpectedAmount = null);
