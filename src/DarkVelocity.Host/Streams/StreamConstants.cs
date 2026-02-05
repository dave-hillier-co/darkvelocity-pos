namespace DarkVelocity.Host.Streams;

/// <summary>
/// Constants for Orleans stream providers and namespaces.
/// </summary>
public static class StreamConstants
{
    /// <summary>
    /// Default stream provider name. Uses in-memory streams for local development
    /// and Azure Event Hubs for production.
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

    /// <summary>
    /// Stream namespace for booking deposit events.
    /// </summary>
    public const string BookingStreamNamespace = "booking-events";

    /// <summary>
    /// Stream namespace for gift card events.
    /// </summary>
    public const string GiftCardStreamNamespace = "giftcard-events";

    /// <summary>
    /// Stream namespace for customer spend events (for loyalty projection).
    /// </summary>
    public const string CustomerSpendStreamNamespace = "customer-spend-events";

    /// <summary>
    /// Stream namespace for accounting/journal entry events.
    /// </summary>
    public const string AccountingStreamNamespace = "accounting-events";

    /// <summary>
    /// Stream namespace for payment-related events (completion, refunds, voids).
    /// </summary>
    public const string PaymentStreamNamespace = "payment-events";

    /// <summary>
    /// Stream namespace for customer lifecycle events.
    /// </summary>
    public const string CustomerStreamNamespace = "customer-events";

    /// <summary>
    /// Stream namespace for device and authentication events.
    /// </summary>
    public const string DeviceStreamNamespace = "device-events";

    /// <summary>
    /// Stream namespace for purchase document events (invoice/receipt confirmation).
    /// </summary>
    public const string PurchaseDocumentStreamNamespace = "purchase-document-events";

    /// <summary>
    /// Stream namespace for workflow status transition events.
    /// </summary>
    public const string WorkflowStreamNamespace = "workflow-events";

    /// <summary>
    /// Stream namespace for notification events.
    /// </summary>
    public const string NotificationStreamNamespace = "notification-events";

    /// <summary>
    /// Stream namespace for webhook delivery events.
    /// </summary>
    public const string WebhookStreamNamespace = "webhook-events";

    /// <summary>
    /// Stream namespace for scheduled job events.
    /// </summary>
    public const string ScheduledJobStreamNamespace = "scheduled-job-events";
}
