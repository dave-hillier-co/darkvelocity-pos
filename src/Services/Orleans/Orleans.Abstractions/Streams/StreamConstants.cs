namespace DarkVelocity.Orleans.Abstractions.Streams;

/// <summary>
/// Constants for Orleans stream providers and namespaces.
/// </summary>
public static class StreamConstants
{
    /// <summary>
    /// Default stream provider name (memory-based for development, can be swapped for Azure Event Hubs, etc.).
    /// </summary>
    public const string DefaultStreamProvider = "DarkVelocityStreamProvider";

    /// <summary>
    /// Stream namespace for user-related events (used for User ↔ Employee sync).
    /// </summary>
    public const string UserStreamNamespace = "user-events";

    /// <summary>
    /// Stream namespace for employee-related events.
    /// </summary>
    public const string EmployeeStreamNamespace = "employee-events";

    /// <summary>
    /// Stream namespace for order-related events (order completion, voids, etc.).
    /// </summary>
    public const string OrderStreamNamespace = "order-events";

    /// <summary>
    /// Stream namespace for inventory-related events (consumption, stock changes).
    /// </summary>
    public const string InventoryStreamNamespace = "inventory-events";

    /// <summary>
    /// Stream namespace for sales aggregation events.
    /// </summary>
    public const string SalesStreamNamespace = "sales-events";

    /// <summary>
    /// Stream namespace for alert triggers.
    /// </summary>
    public const string AlertStreamNamespace = "alert-events";
}
