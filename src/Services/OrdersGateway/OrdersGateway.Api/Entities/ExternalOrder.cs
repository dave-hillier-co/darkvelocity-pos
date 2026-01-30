using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.OrdersGateway.Api.Entities;

/// <summary>
/// Represents an order received from an external delivery platform.
/// </summary>
public class ExternalOrder : BaseEntity, ILocationScoped
{
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public Guid DeliveryPlatformId { get; set; }

    /// <summary>
    /// The order ID from the delivery platform.
    /// </summary>
    public string PlatformOrderId { get; set; } = string.Empty;

    /// <summary>
    /// The display order number from the platform (shown to customers/drivers).
    /// </summary>
    public string PlatformOrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Linked internal DarkVelocity order ID (after order is accepted and created).
    /// </summary>
    public Guid? InternalOrderId { get; set; }

    /// <summary>
    /// Current status of the external order.
    /// </summary>
    public ExternalOrderStatus Status { get; set; } = ExternalOrderStatus.Pending;

    /// <summary>
    /// Type of order: Delivery or Pickup.
    /// </summary>
    public ExternalOrderType OrderType { get; set; } = ExternalOrderType.Delivery;

    /// <summary>
    /// When the order was placed on the platform.
    /// </summary>
    public DateTime PlacedAt { get; set; }

    /// <summary>
    /// When the order was accepted by the merchant.
    /// </summary>
    public DateTime? AcceptedAt { get; set; }

    /// <summary>
    /// Estimated pickup time communicated to the platform.
    /// </summary>
    public DateTime? EstimatedPickupAt { get; set; }

    /// <summary>
    /// When the order was actually picked up by driver.
    /// </summary>
    public DateTime? ActualPickupAt { get; set; }

    /// <summary>
    /// Customer information as JSON (name, phone, delivery address).
    /// </summary>
    public string Customer { get; set; } = "{}";

    /// <summary>
    /// Original items from the platform as JSON.
    /// </summary>
    public string Items { get; set; } = "[]";

    /// <summary>
    /// Order subtotal (before fees and taxes).
    /// </summary>
    public decimal Subtotal { get; set; }

    /// <summary>
    /// Delivery fee charged to customer.
    /// </summary>
    public decimal DeliveryFee { get; set; }

    /// <summary>
    /// Service/platform fee.
    /// </summary>
    public decimal ServiceFee { get; set; }

    /// <summary>
    /// Tax amount.
    /// </summary>
    public decimal Tax { get; set; }

    /// <summary>
    /// Tip amount from customer.
    /// </summary>
    public decimal Tip { get; set; }

    /// <summary>
    /// Total order amount.
    /// </summary>
    public decimal Total { get; set; }

    /// <summary>
    /// Currency code (ISO 4217).
    /// </summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>
    /// Special instructions from the customer.
    /// </summary>
    public string? SpecialInstructions { get; set; }

    /// <summary>
    /// Full raw webhook payload for debugging.
    /// </summary>
    public string? PlatformRawPayload { get; set; }

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of retry attempts for failed processing.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Additional metadata as JSON.
    /// </summary>
    public string Metadata { get; set; } = "{}";

    // Navigation property
    public DeliveryPlatform DeliveryPlatform { get; set; } = null!;
}

public enum ExternalOrderStatus
{
    Pending,
    Accepted,
    Rejected,
    Preparing,
    Ready,
    PickedUp,
    Delivered,
    Cancelled,
    Failed
}

public enum ExternalOrderType
{
    Delivery,
    Pickup
}
