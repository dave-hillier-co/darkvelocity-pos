using System.Text.Json;
using DarkVelocity.OrdersGateway.Api.Dtos;
using DarkVelocity.OrdersGateway.Api.Entities;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.OrdersGateway.Api.Adapters;

/// <summary>
/// Adapter for Uber Eats platform integration.
/// </summary>
public class UberEatsAdapter : BaseDeliveryPlatformAdapter
{
    private const string BaseUrl = "https://api.uber.com/v1/eats";

    public override string PlatformType => "UberEats";

    public UberEatsAdapter(HttpClient httpClient, ILogger<UberEatsAdapter> logger)
        : base(httpClient, logger)
    {
    }

    public override async Task<ConnectionResult> TestConnectionAsync(PlatformCredentials credentials, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(credentials.ClientId) || string.IsNullOrEmpty(credentials.ClientSecret))
        {
            return new ConnectionResult(false, "ClientId and ClientSecret are required");
        }

        try
        {
            // In production, this would authenticate with OAuth2
            // For now, we simulate a successful test
            Logger.LogInformation("Testing Uber Eats connection");
            await Task.Delay(100, cancellationToken);

            return new ConnectionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to test Uber Eats connection");
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

        Logger.LogInformation("Connected to Uber Eats for platform {PlatformId}", platform.Id);
        return new ConnectionResult(true, MerchantId: credentials.ClientId);
    }

    public override Task DisconnectAsync(DeliveryPlatform platform, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Disconnected from Uber Eats for platform {PlatformId}", platform.Id);
        return Task.CompletedTask;
    }

    public override Task<WebhookParseResult> ParseWebhookAsync(string payload, string? signature, DeliveryPlatform platform, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate signature
            if (!string.IsNullOrEmpty(platform.WebhookSecret) && !ValidateWebhookSignature(payload, signature, platform.WebhookSecret))
            {
                return Task.FromResult(new WebhookParseResult(false, WebhookEventType.Unknown, ErrorMessage: "Invalid webhook signature"));
            }

            var webhookData = JsonSerializer.Deserialize<JsonElement>(payload);
            var eventType = webhookData.GetProperty("event_type").GetString();

            return eventType switch
            {
                "orders.notification" => ParseOrderNotification(webhookData, payload),
                "orders.cancel" => Task.FromResult(new WebhookParseResult(true, WebhookEventType.OrderCancelled, RawPayload: payload)),
                _ => Task.FromResult(new WebhookParseResult(true, WebhookEventType.Unknown, RawPayload: payload))
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to parse Uber Eats webhook");
            return Task.FromResult(new WebhookParseResult(false, WebhookEventType.Unknown, ErrorMessage: ex.Message));
        }
    }

    private Task<WebhookParseResult> ParseOrderNotification(JsonElement webhookData, string payload)
    {
        try
        {
            var orderData = webhookData.GetProperty("meta").GetProperty("resource");

            var order = new ExternalOrder
            {
                PlatformOrderId = orderData.GetProperty("id").GetString() ?? string.Empty,
                PlatformOrderNumber = orderData.GetProperty("display_id").GetString() ?? string.Empty,
                Status = ExternalOrderStatus.Pending,
                OrderType = orderData.TryGetProperty("delivery_info", out _) ? ExternalOrderType.Delivery : ExternalOrderType.Pickup,
                PlacedAt = DateTime.UtcNow,
                Currency = orderData.TryGetProperty("payment", out var payment)
                    ? payment.GetProperty("currency_code").GetString() ?? "USD"
                    : "USD",
                PlatformRawPayload = payload
            };

            // Parse customer info
            if (orderData.TryGetProperty("eater", out var eater))
            {
                var customer = new ExternalCustomer
                {
                    Name = $"{eater.GetProperty("first_name").GetString()} {eater.GetProperty("last_name").GetString()}".Trim(),
                    Phone = eater.TryGetProperty("phone", out var phone) ? phone.GetString() : null
                };

                if (orderData.TryGetProperty("delivery_info", out var deliveryInfo))
                {
                    customer.DeliveryAddress = new ExternalAddress
                    {
                        Street = deliveryInfo.TryGetProperty("location", out var location)
                            ? location.GetProperty("address").GetString()
                            : null
                    };
                }

                order.Customer = JsonSerializer.Serialize(customer);
            }

            // Parse items
            if (orderData.TryGetProperty("cart", out var cart) && cart.TryGetProperty("items", out var items))
            {
                var orderItems = new List<ExternalOrderItem>();
                foreach (var item in items.EnumerateArray())
                {
                    orderItems.Add(new ExternalOrderItem
                    {
                        PlatformItemId = item.GetProperty("id").GetString() ?? string.Empty,
                        Name = item.GetProperty("title").GetString() ?? string.Empty,
                        Quantity = item.GetProperty("quantity").GetInt32(),
                        UnitPrice = item.GetProperty("price").GetProperty("unit_price").GetProperty("amount").GetDecimal() / 100m,
                        TotalPrice = item.GetProperty("price").GetProperty("total_price").GetProperty("amount").GetDecimal() / 100m
                    });
                }
                order.Items = JsonSerializer.Serialize(orderItems);

                // Calculate totals
                if (cart.TryGetProperty("total", out var total))
                {
                    order.Total = total.GetProperty("amount").GetDecimal() / 100m;
                }
                if (cart.TryGetProperty("sub_total", out var subtotal))
                {
                    order.Subtotal = subtotal.GetProperty("amount").GetDecimal() / 100m;
                }
            }

            return Task.FromResult(new WebhookParseResult(true, WebhookEventType.OrderCreated, order, RawPayload: payload));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to parse Uber Eats order notification");
            return Task.FromResult(new WebhookParseResult(false, WebhookEventType.Unknown, ErrorMessage: ex.Message, RawPayload: payload));
        }
    }

    public override async Task<OrderActionResult> AcceptOrderAsync(DeliveryPlatform platform, string platformOrderId, int prepTimeMinutes, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Accepting Uber Eats order {OrderId} with prep time {PrepTime} minutes", platformOrderId, prepTimeMinutes);

            // In production, this would call the Uber Eats API
            // POST /v1/eats/orders/{order_id}/accept
            await Task.Delay(50, cancellationToken);

            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to accept Uber Eats order {OrderId}", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<OrderActionResult> RejectOrderAsync(DeliveryPlatform platform, string platformOrderId, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Rejecting Uber Eats order {OrderId}: {Reason}", platformOrderId, reason);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to reject Uber Eats order {OrderId}", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<OrderActionResult> MarkReadyAsync(DeliveryPlatform platform, string platformOrderId, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Marking Uber Eats order {OrderId} as ready", platformOrderId);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to mark Uber Eats order {OrderId} as ready", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<OrderActionResult> CancelOrderAsync(DeliveryPlatform platform, string platformOrderId, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Cancelling Uber Eats order {OrderId}: {Reason}", platformOrderId, reason);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to cancel Uber Eats order {OrderId}", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<OrderActionResult> UpdatePrepTimeAsync(DeliveryPlatform platform, string platformOrderId, int newPrepTimeMinutes, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Updating Uber Eats order {OrderId} prep time to {PrepTime} minutes", platformOrderId, newPrepTimeMinutes);
            await Task.Delay(50, cancellationToken);
            return new OrderActionResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to update prep time for Uber Eats order {OrderId}", platformOrderId);
            return new OrderActionResult(false, ex.Message);
        }
    }

    public override async Task<MenuSyncResult> SyncMenuAsync(DeliveryPlatform platform, Guid locationId, IEnumerable<MenuItemForSync> items, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Syncing menu to Uber Eats for location {LocationId}", locationId);
            var itemsList = items.ToList();

            // In production, this would call the Uber Eats Menu API
            await Task.Delay(100, cancellationToken);

            return new MenuSyncResult(true, itemsList.Count, 0);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to sync menu to Uber Eats for location {LocationId}", locationId);
            return new MenuSyncResult(false, 0, items.Count(), new List<MenuSyncError>
            {
                new() { ErrorMessage = ex.Message }
            });
        }
    }

    public override async Task<bool> SetStoreStatusAsync(DeliveryPlatform platform, string platformStoreId, bool isOpen, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Setting Uber Eats store {StoreId} status to {Status}", platformStoreId, isOpen ? "open" : "closed");
            await Task.Delay(50, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to set store status for Uber Eats store {StoreId}", platformStoreId);
            return false;
        }
    }
}
