using System.Text.Json;
using DarkVelocity.OrdersGateway.Api.Dtos;
using DarkVelocity.OrdersGateway.Api.Entities;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.OrdersGateway.Api.Adapters;

/// <summary>
/// Adapter for Deliveroo platform integration.
/// </summary>
public class DeliverooAdapter : BaseDeliveryPlatformAdapter
{
    private const string BaseUrl = "https://api.deliveroo.com/orderapi/v1";

    public override string PlatformType => "Deliveroo";

    public DeliverooAdapter(HttpClient httpClient, ILogger<DeliverooAdapter> logger)
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
            Logger.LogInformation("Testing Deliveroo connection");
            await Task.Delay(100, cancellationToken);
            return new ConnectionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to test Deliveroo connection");
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

        Logger.LogInformation("Connected to Deliveroo for platform {PlatformId}", platform.Id);
        return new ConnectionResult(true, MerchantId: platform.MerchantId);
    }

    public override Task DisconnectAsync(DeliveryPlatform platform, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Disconnected from Deliveroo for platform {PlatformId}", platform.Id);
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
            var eventType = webhookData.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

            return eventType switch
            {
                "order.created" => ParseOrderCreated(webhookData, payload),
                "order.cancelled" => Task.FromResult(new WebhookParseResult(true, WebhookEventType.OrderCancelled, RawPayload: payload)),
                "order.picked_up" => Task.FromResult(new WebhookParseResult(true, WebhookEventType.OrderPickedUp, RawPayload: payload)),
                _ => Task.FromResult(new WebhookParseResult(true, WebhookEventType.Unknown, RawPayload: payload))
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to parse Deliveroo webhook");
            return Task.FromResult(new WebhookParseResult(false, WebhookEventType.Unknown, ErrorMessage: ex.Message));
        }
    }

    private Task<WebhookParseResult> ParseOrderCreated(JsonElement webhookData, string payload)
    {
        try
        {
            var orderData = webhookData.GetProperty("order");

            var order = new ExternalOrder
            {
                PlatformOrderId = orderData.GetProperty("id").GetString() ?? string.Empty,
                PlatformOrderNumber = orderData.TryGetProperty("display_id", out var displayId)
                    ? displayId.GetString() ?? string.Empty
                    : orderData.GetProperty("id").GetString() ?? string.Empty,
                Status = ExternalOrderStatus.Pending,
                OrderType = orderData.TryGetProperty("fulfillment_type", out var ft) && ft.GetString() == "pickup"
                    ? ExternalOrderType.Pickup
                    : ExternalOrderType.Delivery,
                PlacedAt = orderData.TryGetProperty("placed_at", out var placedAt)
                    ? DateTime.Parse(placedAt.GetString() ?? DateTime.UtcNow.ToString())
                    : DateTime.UtcNow,
                Currency = orderData.TryGetProperty("currency", out var currency) ? currency.GetString() ?? "GBP" : "GBP",
                PlatformRawPayload = payload
            };

            // Parse customer
            if (orderData.TryGetProperty("customer", out var customer))
            {
                var extCustomer = new ExternalCustomer
                {
                    Name = customer.TryGetProperty("name", out var name) ? name.GetString() : null,
                    Phone = customer.TryGetProperty("phone", out var phone) ? phone.GetString() : null
                };

                if (orderData.TryGetProperty("delivery_address", out var addr))
                {
                    extCustomer.DeliveryAddress = new ExternalAddress
                    {
                        Street = addr.TryGetProperty("address_line_1", out var a1) ? a1.GetString() : null,
                        City = addr.TryGetProperty("city", out var city) ? city.GetString() : null,
                        PostalCode = addr.TryGetProperty("postcode", out var pc) ? pc.GetString() : null,
                        Country = "GB"
                    };
                }

                order.Customer = JsonSerializer.Serialize(extCustomer);
            }

            // Parse items
            if (orderData.TryGetProperty("items", out var items))
            {
                var orderItems = new List<ExternalOrderItem>();
                foreach (var item in items.EnumerateArray())
                {
                    orderItems.Add(new ExternalOrderItem
                    {
                        PlatformItemId = item.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                        Name = item.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                        Quantity = item.TryGetProperty("quantity", out var q) ? q.GetInt32() : 1,
                        UnitPrice = item.TryGetProperty("unit_price", out var up) ? up.GetDecimal() / 100m : 0,
                        TotalPrice = item.TryGetProperty("total_price", out var tp) ? tp.GetDecimal() / 100m : 0
                    });
                }
                order.Items = JsonSerializer.Serialize(orderItems);
            }

            // Parse totals
            if (orderData.TryGetProperty("total", out var total))
            {
                order.Total = total.TryGetProperty("amount", out var amt) ? amt.GetDecimal() / 100m : 0;
            }
            if (orderData.TryGetProperty("subtotal", out var subtotal))
            {
                order.Subtotal = subtotal.TryGetProperty("amount", out var amt) ? amt.GetDecimal() / 100m : 0;
            }

            return Task.FromResult(new WebhookParseResult(true, WebhookEventType.OrderCreated, order, RawPayload: payload));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to parse Deliveroo order");
            return Task.FromResult(new WebhookParseResult(false, WebhookEventType.Unknown, ErrorMessage: ex.Message, RawPayload: payload));
        }
    }

    public override async Task<OrderActionResult> AcceptOrderAsync(DeliveryPlatform platform, string platformOrderId, int prepTimeMinutes, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Accepting Deliveroo order {OrderId} with prep time {PrepTime} minutes", platformOrderId, prepTimeMinutes);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to accept Deliveroo order {OrderId}", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<OrderActionResult> RejectOrderAsync(DeliveryPlatform platform, string platformOrderId, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Rejecting Deliveroo order {OrderId}: {Reason}", platformOrderId, reason);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to reject Deliveroo order {OrderId}", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<OrderActionResult> MarkReadyAsync(DeliveryPlatform platform, string platformOrderId, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Marking Deliveroo order {OrderId} as ready", platformOrderId);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to mark Deliveroo order {OrderId} as ready", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<OrderActionResult> CancelOrderAsync(DeliveryPlatform platform, string platformOrderId, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Cancelling Deliveroo order {OrderId}: {Reason}", platformOrderId, reason);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to cancel Deliveroo order {OrderId}", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<OrderActionResult> UpdatePrepTimeAsync(DeliveryPlatform platform, string platformOrderId, int newPrepTimeMinutes, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Updating Deliveroo order {OrderId} prep time to {PrepTime} minutes", platformOrderId, newPrepTimeMinutes);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to update prep time for Deliveroo order {OrderId}", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<MenuSyncResult> SyncMenuAsync(DeliveryPlatform platform, Guid locationId, IEnumerable<MenuItemForSync> items, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Syncing menu to Deliveroo for location {LocationId}", locationId);
            var itemsList = items.ToList();
            await Task.Delay(100, cancellationToken);
            return new MenuSyncResult(true, itemsList.Count, 0);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to sync menu to Deliveroo for location {LocationId}", locationId);
            return new MenuSyncResult(false, 0, items.Count());
        }
    }
}
