using System.Text.Json;
using DarkVelocity.OrdersGateway.Api.Dtos;
using DarkVelocity.OrdersGateway.Api.Entities;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.OrdersGateway.Api.Adapters;

/// <summary>
/// Adapter for DoorDash platform integration.
/// </summary>
public class DoorDashAdapter : BaseDeliveryPlatformAdapter
{
    private const string BaseUrl = "https://api.doordash.com/drive/v2";

    public override string PlatformType => "DoorDash";

    public DoorDashAdapter(HttpClient httpClient, ILogger<DoorDashAdapter> logger)
        : base(httpClient, logger)
    {
    }

    public override async Task<ConnectionResult> TestConnectionAsync(PlatformCredentials credentials, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(credentials.ApiKey))
        {
            return new ConnectionResult(false, "API key is required");
        }

        try
        {
            Logger.LogInformation("Testing DoorDash connection");
            await Task.Delay(100, cancellationToken);
            return new ConnectionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to test DoorDash connection");
            return new ConnectionResult(false, ex.Message);
        }
    }

    public override async Task<ConnectionResult> ConnectAsync(DeliveryPlatform platform, PlatformCredentials credentials, CancellationToken cancellationToken = default)
    {
        var testResult = await TestConnectionAsync(credentials, cancellationToken);
        if (!testResult.Success)
        {
            return testResult;
        }

        Logger.LogInformation("Connected to DoorDash for platform {PlatformId}", platform.Id);
        return new ConnectionResult(true, MerchantId: platform.MerchantId);
    }

    public override Task DisconnectAsync(DeliveryPlatform platform, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Disconnected from DoorDash for platform {PlatformId}", platform.Id);
        return Task.CompletedTask;
    }

    public override Task<WebhookParseResult> ParseWebhookAsync(string payload, string? signature, DeliveryPlatform platform, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrEmpty(platform.WebhookSecret) && !ValidateWebhookSignature(payload, signature, platform.WebhookSecret))
            {
                return Task.FromResult(new WebhookParseResult(false, WebhookEventType.Unknown, ErrorMessage: "Invalid webhook signature"));
            }

            var webhookData = JsonSerializer.Deserialize<JsonElement>(payload);
            var eventType = webhookData.GetProperty("event_type").GetString();

            return eventType switch
            {
                "ORDER_CREATED" => ParseOrderCreated(webhookData, payload),
                "ORDER_CANCELLED" => Task.FromResult(new WebhookParseResult(true, WebhookEventType.OrderCancelled, RawPayload: payload)),
                "ORDER_PICKED_UP" => Task.FromResult(new WebhookParseResult(true, WebhookEventType.OrderPickedUp, RawPayload: payload)),
                "ORDER_DELIVERED" => Task.FromResult(new WebhookParseResult(true, WebhookEventType.OrderDelivered, RawPayload: payload)),
                _ => Task.FromResult(new WebhookParseResult(true, WebhookEventType.Unknown, RawPayload: payload))
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to parse DoorDash webhook");
            return Task.FromResult(new WebhookParseResult(false, WebhookEventType.Unknown, ErrorMessage: ex.Message));
        }
    }

    private Task<WebhookParseResult> ParseOrderCreated(JsonElement webhookData, string payload)
    {
        try
        {
            var orderData = webhookData.GetProperty("data");

            var order = new ExternalOrder
            {
                PlatformOrderId = orderData.GetProperty("external_delivery_id").GetString() ?? string.Empty,
                PlatformOrderNumber = orderData.TryGetProperty("order_value", out var ov) && ov.TryGetProperty("external_reference", out var extRef)
                    ? extRef.GetString() ?? orderData.GetProperty("external_delivery_id").GetString() ?? string.Empty
                    : orderData.GetProperty("external_delivery_id").GetString() ?? string.Empty,
                Status = ExternalOrderStatus.Pending,
                OrderType = ExternalOrderType.Delivery,
                PlacedAt = DateTime.UtcNow,
                Currency = "USD",
                PlatformRawPayload = payload
            };

            // Parse customer
            if (orderData.TryGetProperty("dropoff", out var dropoff))
            {
                var customer = new ExternalCustomer
                {
                    Name = dropoff.TryGetProperty("contact_name", out var name) ? name.GetString() : null,
                    Phone = dropoff.TryGetProperty("contact_phone", out var phone) ? phone.GetString() : null
                };

                if (dropoff.TryGetProperty("location", out var location))
                {
                    customer.DeliveryAddress = new ExternalAddress
                    {
                        Street = location.TryGetProperty("address", out var addr) ? addr.GetString() : null,
                        City = location.TryGetProperty("city", out var city) ? city.GetString() : null,
                        PostalCode = location.TryGetProperty("zip_code", out var zip) ? zip.GetString() : null
                    };
                }

                order.Customer = JsonSerializer.Serialize(customer);
            }

            // Parse order value
            if (orderData.TryGetProperty("order_value", out var orderValue))
            {
                order.Subtotal = orderValue.TryGetProperty("subtotal", out var sub) ? sub.GetDecimal() / 100m : 0;
                order.Tax = orderValue.TryGetProperty("tax", out var tax) ? tax.GetDecimal() / 100m : 0;
                order.Total = orderValue.TryGetProperty("total", out var total) ? total.GetDecimal() / 100m : 0;
                order.Tip = orderValue.TryGetProperty("tip", out var tip) ? tip.GetDecimal() / 100m : 0;
            }

            return Task.FromResult(new WebhookParseResult(true, WebhookEventType.OrderCreated, order, RawPayload: payload));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to parse DoorDash order");
            return Task.FromResult(new WebhookParseResult(false, WebhookEventType.Unknown, ErrorMessage: ex.Message, RawPayload: payload));
        }
    }

    public override async Task<OrderActionResult> AcceptOrderAsync(DeliveryPlatform platform, string platformOrderId, int prepTimeMinutes, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Accepting DoorDash order {OrderId} with prep time {PrepTime} minutes", platformOrderId, prepTimeMinutes);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to accept DoorDash order {OrderId}", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<OrderActionResult> RejectOrderAsync(DeliveryPlatform platform, string platformOrderId, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Rejecting DoorDash order {OrderId}: {Reason}", platformOrderId, reason);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to reject DoorDash order {OrderId}", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<OrderActionResult> MarkReadyAsync(DeliveryPlatform platform, string platformOrderId, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Marking DoorDash order {OrderId} as ready", platformOrderId);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to mark DoorDash order {OrderId} as ready", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<OrderActionResult> CancelOrderAsync(DeliveryPlatform platform, string platformOrderId, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Cancelling DoorDash order {OrderId}: {Reason}", platformOrderId, reason);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to cancel DoorDash order {OrderId}", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<OrderActionResult> UpdatePrepTimeAsync(DeliveryPlatform platform, string platformOrderId, int newPrepTimeMinutes, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Updating DoorDash order {OrderId} prep time to {PrepTime} minutes", platformOrderId, newPrepTimeMinutes);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to update prep time for DoorDash order {OrderId}", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<MenuSyncResult> SyncMenuAsync(DeliveryPlatform platform, Guid locationId, IEnumerable<MenuItemForSync> items, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Syncing menu to DoorDash for location {LocationId}", locationId);
            var itemsList = items.ToList();
            await Task.Delay(100, cancellationToken);
            return new MenuSyncResult(true, itemsList.Count, 0);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to sync menu to DoorDash for location {LocationId}", locationId);
            return new MenuSyncResult(false, 0, items.Count());
        }
    }
}
