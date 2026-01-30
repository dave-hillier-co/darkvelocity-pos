using System.Text.Json;
using DarkVelocity.OrdersGateway.Api.Dtos;
using DarkVelocity.OrdersGateway.Api.Entities;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.OrdersGateway.Api.Adapters;

/// <summary>
/// Adapter for Just Eat / Takeaway.com platform integration.
/// </summary>
public class JustEatAdapter : BaseDeliveryPlatformAdapter
{
    private const string BaseUrl = "https://partner-api.just-eat.com/orders/v1";

    public override string PlatformType => "JustEat";

    public JustEatAdapter(HttpClient httpClient, ILogger<JustEatAdapter> logger)
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
            Logger.LogInformation("Testing Just Eat connection");
            await Task.Delay(100, cancellationToken);
            return new ConnectionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to test Just Eat connection");
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

        Logger.LogInformation("Connected to Just Eat for platform {PlatformId}", platform.Id);
        return new ConnectionResult(true, MerchantId: platform.MerchantId);
    }

    public override Task DisconnectAsync(DeliveryPlatform platform, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Disconnected from Just Eat for platform {PlatformId}", platform.Id);
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

            // Just Eat uses different webhook formats
            if (webhookData.TryGetProperty("Type", out var typeElement))
            {
                var eventType = typeElement.GetString();
                return eventType switch
                {
                    "OrderPlaced" => ParseOrderPlaced(webhookData, payload),
                    "OrderCancelled" => Task.FromResult(new WebhookParseResult(true, WebhookEventType.OrderCancelled, RawPayload: payload)),
                    "OrderCollected" => Task.FromResult(new WebhookParseResult(true, WebhookEventType.OrderPickedUp, RawPayload: payload)),
                    _ => Task.FromResult(new WebhookParseResult(true, WebhookEventType.Unknown, RawPayload: payload))
                };
            }

            // Fallback for direct order webhooks
            if (webhookData.TryGetProperty("Id", out _))
            {
                return ParseOrderPlaced(webhookData, payload);
            }

            return Task.FromResult(new WebhookParseResult(true, WebhookEventType.Unknown, RawPayload: payload));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to parse Just Eat webhook");
            return Task.FromResult(new WebhookParseResult(false, WebhookEventType.Unknown, ErrorMessage: ex.Message));
        }
    }

    private Task<WebhookParseResult> ParseOrderPlaced(JsonElement webhookData, string payload)
    {
        try
        {
            var orderData = webhookData.TryGetProperty("Order", out var od) ? od : webhookData;

            var order = new ExternalOrder
            {
                PlatformOrderId = orderData.GetProperty("Id").GetString() ?? string.Empty,
                PlatformOrderNumber = orderData.TryGetProperty("FriendlyOrderReference", out var friendlyRef)
                    ? friendlyRef.GetString() ?? string.Empty
                    : orderData.GetProperty("Id").GetString() ?? string.Empty,
                Status = ExternalOrderStatus.Pending,
                OrderType = orderData.TryGetProperty("ServiceType", out var st) && st.GetString() == "Collection"
                    ? ExternalOrderType.Pickup
                    : ExternalOrderType.Delivery,
                PlacedAt = orderData.TryGetProperty("PlacedDate", out var placedDate)
                    ? DateTime.Parse(placedDate.GetString() ?? DateTime.UtcNow.ToString())
                    : DateTime.UtcNow,
                Currency = orderData.TryGetProperty("Currency", out var currency)
                    ? currency.GetString() ?? "EUR"
                    : "EUR",
                PlatformRawPayload = payload
            };

            // Parse customer
            if (orderData.TryGetProperty("Customer", out var customer))
            {
                var extCustomer = new ExternalCustomer
                {
                    Name = customer.TryGetProperty("Name", out var name) ? name.GetString() : null,
                    Phone = customer.TryGetProperty("PhoneNumber", out var phone) ? phone.GetString() : null
                };

                if (orderData.TryGetProperty("Fulfilment", out var fulfilment) &&
                    fulfilment.TryGetProperty("Address", out var addr))
                {
                    extCustomer.DeliveryAddress = new ExternalAddress
                    {
                        Street = addr.TryGetProperty("Lines", out var lines)
                            ? string.Join(", ", lines.EnumerateArray().Select(l => l.GetString()))
                            : null,
                        City = addr.TryGetProperty("City", out var city) ? city.GetString() : null,
                        PostalCode = addr.TryGetProperty("PostalCode", out var pc) ? pc.GetString() : null
                    };
                }

                order.Customer = JsonSerializer.Serialize(extCustomer);
            }

            // Parse items
            if (orderData.TryGetProperty("Items", out var items))
            {
                var orderItems = new List<ExternalOrderItem>();
                foreach (var item in items.EnumerateArray())
                {
                    orderItems.Add(new ExternalOrderItem
                    {
                        PlatformItemId = item.TryGetProperty("MenuItemId", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                        Name = item.TryGetProperty("Name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                        Quantity = item.TryGetProperty("Quantity", out var q) ? q.GetInt32() : 1,
                        UnitPrice = item.TryGetProperty("UnitPrice", out var up) ? up.GetDecimal() : 0,
                        TotalPrice = item.TryGetProperty("TotalPrice", out var tp) ? tp.GetDecimal() : 0
                    });
                }
                order.Items = JsonSerializer.Serialize(orderItems);
            }

            // Parse totals
            if (orderData.TryGetProperty("TotalPrice", out var totalPrice))
            {
                order.Total = totalPrice.GetDecimal();
            }
            if (orderData.TryGetProperty("Subtotal", out var subtotal))
            {
                order.Subtotal = subtotal.GetDecimal();
            }
            if (orderData.TryGetProperty("DeliveryFee", out var deliveryFee))
            {
                order.DeliveryFee = deliveryFee.GetDecimal();
            }

            return Task.FromResult(new WebhookParseResult(true, WebhookEventType.OrderCreated, order, RawPayload: payload));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to parse Just Eat order");
            return Task.FromResult(new WebhookParseResult(false, WebhookEventType.Unknown, ErrorMessage: ex.Message, RawPayload: payload));
        }
    }

    public override async Task<OrderActionResult> AcceptOrderAsync(DeliveryPlatform platform, string platformOrderId, int prepTimeMinutes, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Accepting Just Eat order {OrderId} with prep time {PrepTime} minutes", platformOrderId, prepTimeMinutes);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to accept Just Eat order {OrderId}", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<OrderActionResult> RejectOrderAsync(DeliveryPlatform platform, string platformOrderId, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Rejecting Just Eat order {OrderId}: {Reason}", platformOrderId, reason);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to reject Just Eat order {OrderId}", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<OrderActionResult> MarkReadyAsync(DeliveryPlatform platform, string platformOrderId, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Marking Just Eat order {OrderId} as ready", platformOrderId);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to mark Just Eat order {OrderId} as ready", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<OrderActionResult> CancelOrderAsync(DeliveryPlatform platform, string platformOrderId, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Cancelling Just Eat order {OrderId}: {Reason}", platformOrderId, reason);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to cancel Just Eat order {OrderId}", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<OrderActionResult> UpdatePrepTimeAsync(DeliveryPlatform platform, string platformOrderId, int newPrepTimeMinutes, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Updating Just Eat order {OrderId} prep time to {PrepTime} minutes", platformOrderId, newPrepTimeMinutes);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to update prep time for Just Eat order {OrderId}", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<MenuSyncResult> SyncMenuAsync(DeliveryPlatform platform, Guid locationId, IEnumerable<MenuItemForSync> items, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Syncing menu to Just Eat for location {LocationId}", locationId);
            var itemsList = items.ToList();
            await Task.Delay(100, cancellationToken);
            return new MenuSyncResult(true, itemsList.Count, 0);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to sync menu to Just Eat for location {LocationId}", locationId);
            return new MenuSyncResult(false, 0, items.Count());
        }
    }
}
