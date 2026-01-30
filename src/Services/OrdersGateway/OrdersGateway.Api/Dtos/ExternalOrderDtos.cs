using DarkVelocity.OrdersGateway.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.OrdersGateway.Api.Dtos;

/// <summary>
/// Response DTO for an external order.
/// </summary>
public class ExternalOrderDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public Guid DeliveryPlatformId { get; set; }
    public string PlatformType { get; set; } = string.Empty;
    public string PlatformOrderId { get; set; } = string.Empty;
    public string PlatformOrderNumber { get; set; } = string.Empty;
    public Guid? InternalOrderId { get; set; }
    public ExternalOrderStatus Status { get; set; }
    public ExternalOrderType OrderType { get; set; }
    public DateTime PlacedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? EstimatedPickupAt { get; set; }
    public DateTime? ActualPickupAt { get; set; }
    public ExternalCustomer? Customer { get; set; }
    public List<ExternalOrderItem> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal Tax { get; set; }
    public decimal Tip { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? SpecialInstructions { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request to accept an external order.
/// </summary>
public record AcceptOrderRequest(
    int PrepTimeMinutes = 20);

/// <summary>
/// Request to reject an external order.
/// </summary>
public record RejectOrderRequest(
    string Reason,
    RejectReason ReasonCode = RejectReason.Other);

/// <summary>
/// Standard rejection reasons.
/// </summary>
public enum RejectReason
{
    StoreClosed,
    TooFarAway,
    ItemUnavailable,
    TooBusy,
    ClosingSoon,
    TechnicalIssue,
    Other
}

/// <summary>
/// Request to cancel an external order.
/// </summary>
public record CancelOrderRequest(
    string Reason,
    CancelledBy CancelledBy = CancelledBy.Merchant);

/// <summary>
/// Who cancelled the order.
/// </summary>
public enum CancelledBy
{
    Merchant,
    Platform,
    Customer
}

/// <summary>
/// Request to adjust preparation time.
/// </summary>
public record AdjustPrepTimeRequest(
    int NewPrepTimeMinutes);

/// <summary>
/// Response for order tracking information.
/// </summary>
public class OrderTrackingDto : HalResource
{
    public Guid ExternalOrderId { get; set; }
    public string PlatformOrderId { get; set; } = string.Empty;
    public ExternalOrderStatus Status { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public double? DriverLatitude { get; set; }
    public double? DriverLongitude { get; set; }
    public DateTime? EstimatedDeliveryAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}
