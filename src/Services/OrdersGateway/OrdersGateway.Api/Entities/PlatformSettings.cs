namespace DarkVelocity.OrdersGateway.Api.Entities;

/// <summary>
/// Settings for a delivery platform connection.
/// Serialized to JSON in DeliveryPlatform.Settings.
/// </summary>
public class PlatformSettings
{
    /// <summary>
    /// Whether to automatically accept incoming orders.
    /// </summary>
    public bool AutoAcceptOrders { get; set; } = false;

    /// <summary>
    /// Default preparation time in minutes.
    /// </summary>
    public int DefaultPrepTime { get; set; } = 20;

    /// <summary>
    /// Preparation time to use when in busy mode.
    /// </summary>
    public int BusyModePrepTime { get; set; } = 35;

    /// <summary>
    /// Whether to automatically sync menu changes to the platform.
    /// </summary>
    public bool AutoSyncMenu { get; set; } = false;

    /// <summary>
    /// Maximum number of auto-accept retries on failure.
    /// </summary>
    public int MaxAutoAcceptRetries { get; set; } = 3;

    /// <summary>
    /// Whether the store is currently in busy mode (longer prep times).
    /// </summary>
    public bool IsBusyMode { get; set; } = false;
}

/// <summary>
/// Customer information from an external order.
/// Serialized to JSON in ExternalOrder.Customer.
/// </summary>
public class ExternalCustomer
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public ExternalAddress? DeliveryAddress { get; set; }
}

/// <summary>
/// Delivery address for an external order.
/// </summary>
public class ExternalAddress
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? Instructions { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

/// <summary>
/// An item from an external order.
/// </summary>
public class ExternalOrderItem
{
    public string PlatformItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Notes { get; set; }
    public List<ExternalOrderModifier> Modifiers { get; set; } = new();
}

/// <summary>
/// A modifier for an external order item.
/// </summary>
public class ExternalOrderModifier
{
    public string PlatformModifierId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal Price { get; set; }
}
