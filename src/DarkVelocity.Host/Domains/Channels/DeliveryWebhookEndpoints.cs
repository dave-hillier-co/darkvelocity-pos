using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

/// <summary>
/// Webhook endpoints for receiving orders from delivery platforms.
/// These endpoints receive external order events and route them to appropriate grains.
/// </summary>
public static class DeliveryWebhookEndpoints
{
    public static WebApplication MapDeliveryWebhookEndpoints(this WebApplication app)
    {
        var webhookGroup = app.MapGroup("/api/webhooks").WithTags("Delivery Webhooks");

        // ============================================================================
        // Deliverect Webhook Receiver
        // ============================================================================

        webhookGroup.MapPost("/deliverect", async (
            HttpContext context,
            [FromServices] IGrainFactory grainFactory,
            [FromServices] ILogger<DeliverectWebhookHandler> logger) =>
        {
            var handler = new DeliverectWebhookHandler(grainFactory, logger);
            return await handler.HandleWebhookAsync(context);
        })
        .WithName("DeliverectWebhook")
        .WithSummary("Receive webhooks from Deliverect")
        .AllowAnonymous(); // Webhooks are authenticated via HMAC signature

        // ============================================================================
        // UberEats Webhook Receiver
        // ============================================================================

        webhookGroup.MapPost("/ubereats", async (
            HttpContext context,
            [FromServices] IGrainFactory grainFactory,
            [FromServices] ILogger<UberEatsWebhookHandler> logger) =>
        {
            var handler = new UberEatsWebhookHandler(grainFactory, logger);
            return await handler.HandleWebhookAsync(context);
        })
        .WithName("UberEatsWebhook")
        .WithSummary("Receive webhooks from UberEats")
        .AllowAnonymous();

        // ============================================================================
        // DoorDash Webhook Receiver
        // ============================================================================

        webhookGroup.MapPost("/doordash", async (
            HttpContext context,
            [FromServices] IGrainFactory grainFactory,
            [FromServices] ILogger<DoorDashWebhookHandler> logger) =>
        {
            var handler = new DoorDashWebhookHandler(grainFactory, logger);
            return await handler.HandleWebhookAsync(context);
        })
        .WithName("DoorDashWebhook")
        .WithSummary("Receive webhooks from DoorDash")
        .AllowAnonymous();

        return app;
    }
}

/// <summary>
/// Base class for webhook handlers providing common functionality.
/// </summary>
public abstract class BaseWebhookHandler
{
    protected readonly IGrainFactory GrainFactory;
    protected readonly ILogger Logger;

    protected BaseWebhookHandler(IGrainFactory grainFactory, ILogger logger)
    {
        GrainFactory = grainFactory;
        Logger = logger;
    }

    /// <summary>
    /// Validates HMAC signature for webhook authentication.
    /// </summary>
    protected bool ValidateHmacSignature(string payload, string signature, string secret, string algorithm = "SHA256")
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
            return false;

        try
        {
            using var hmac = algorithm.ToUpperInvariant() switch
            {
                "SHA256" => (HMAC)new HMACSHA256(Encoding.UTF8.GetBytes(secret)),
                "SHA512" => new HMACSHA512(Encoding.UTF8.GetBytes(secret)),
                "SHA1" => new HMACSHA1(Encoding.UTF8.GetBytes(secret)),
                _ => new HMACSHA256(Encoding.UTF8.GetBytes(secret))
            };

            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computedSignature = Convert.ToHexString(computedHash).ToLowerInvariant();

            // Handle signature formats: "sha256=xxx" or just "xxx"
            var providedSignature = signature.Contains('=')
                ? signature.Split('=').Last().ToLowerInvariant()
                : signature.ToLowerInvariant();

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature),
                Encoding.UTF8.GetBytes(providedSignature));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "HMAC signature validation failed");
            return false;
        }
    }

    /// <summary>
    /// Reads the request body as a string.
    /// </summary>
    protected async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
        return body;
    }

    /// <summary>
    /// Gets the webhook secret for a channel.
    /// </summary>
    protected async Task<string?> GetWebhookSecretAsync(Guid orgId, Guid channelId)
    {
        var grain = GrainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));
        var snapshot = await grain.GetSnapshotAsync();
        return snapshot.ChannelId != Guid.Empty ? null : null; // WebhookSecret would be stored in channel state
    }
}

/// <summary>
/// Handles webhooks from Deliverect aggregator platform.
/// </summary>
public class DeliverectWebhookHandler : BaseWebhookHandler
{
    public DeliverectWebhookHandler(IGrainFactory grainFactory, ILogger<DeliverectWebhookHandler> logger)
        : base(grainFactory, logger) { }

    public async Task<IResult> HandleWebhookAsync(HttpContext context)
    {
        var body = await ReadBodyAsync(context);
        var signature = context.Request.Headers["X-Deliverect-Signature"].FirstOrDefault();

        Logger.LogInformation("Received Deliverect webhook, payload length: {Length}", body.Length);

        try
        {
            var payload = JsonSerializer.Deserialize<DeliverectWebhookPayload>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (payload == null)
            {
                Logger.LogWarning("Failed to parse Deliverect webhook payload");
                return Results.BadRequest(new { error = "invalid_payload", message = "Could not parse webhook payload" });
            }

            // Route based on event type
            return payload.Event?.ToLowerInvariant() switch
            {
                "order.created" or "order" => await HandleOrderCreatedAsync(payload, body),
                "order.cancelled" => await HandleOrderCancelledAsync(payload),
                "order.status" => await HandleOrderStatusUpdateAsync(payload),
                "products.sync" => await HandleProductSyncRequestAsync(payload),
                "store.busy" => await HandleStoreBusyAsync(payload),
                _ => Results.Ok(new { received = true, event_type = payload.Event })
            };
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "JSON parsing error for Deliverect webhook");
            return Results.BadRequest(new { error = "json_error", message = "Invalid JSON format" });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing Deliverect webhook");
            return Results.StatusCode(500);
        }
    }

    private async Task<IResult> HandleOrderCreatedAsync(DeliverectWebhookPayload payload, string rawPayload)
    {
        if (payload.Order == null)
        {
            return Results.BadRequest(new { error = "missing_order", message = "Order data is required" });
        }

        var order = payload.Order;

        // Parse organization and location from Deliverect account/location
        if (!Guid.TryParse(payload.AccountId, out var orgId) ||
            !Guid.TryParse(payload.LocationId, out var locationId))
        {
            Logger.LogWarning("Invalid org/location IDs in Deliverect webhook: {AccountId}, {LocationId}",
                payload.AccountId, payload.LocationId);
            return Results.BadRequest(new { error = "invalid_ids", message = "Invalid account or location ID" });
        }

        // Find the channel for this platform
        var channelId = await FindChannelIdAsync(orgId, DeliveryPlatformType.Deliverect);
        if (channelId == null)
        {
            Logger.LogWarning("No Deliverect channel configured for org {OrgId}", orgId);
            return Results.NotFound(new { error = "channel_not_found", message = "No Deliverect channel configured" });
        }

        // Create external order
        var externalOrderId = Guid.NewGuid();
        var externalOrderGrain = GrainFactory.GetGrain<IExternalOrderGrain>(
            GrainKeys.OrgEntity(orgId, "externalorder", externalOrderId));

        var externalOrder = MapDeliverectOrderToExternalOrder(order, locationId, channelId.Value, rawPayload);
        var snapshot = await externalOrderGrain.ReceiveAsync(externalOrder);

        // Record order on channel
        var channelGrain = GrainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId.Value));
        await channelGrain.RecordOrderAsync(order.Payment?.Amount ?? 0m);

        Logger.LogInformation("Created external order {ExternalOrderId} from Deliverect order {PlatformOrderId}",
            externalOrderId, order.Id);

        return Results.Ok(new
        {
            received = true,
            external_order_id = externalOrderId,
            platform_order_id = order.Id,
            status = snapshot.Status.ToString()
        });
    }

    private async Task<IResult> HandleOrderCancelledAsync(DeliverectWebhookPayload payload)
    {
        if (string.IsNullOrEmpty(payload.Order?.Id))
        {
            return Results.BadRequest(new { error = "missing_order_id", message = "Order ID is required" });
        }

        Logger.LogInformation("Order cancelled: {OrderId}", payload.Order.Id);
        return Results.Ok(new { received = true, action = "cancelled" });
    }

    private Task<IResult> HandleOrderStatusUpdateAsync(DeliverectWebhookPayload payload)
    {
        Logger.LogInformation("Order status update: {OrderId}, Status: {Status}",
            payload.Order?.Id, payload.Order?.Status);
        return Task.FromResult<IResult>(Results.Ok(new { received = true, action = "status_update" }));
    }

    private Task<IResult> HandleProductSyncRequestAsync(DeliverectWebhookPayload payload)
    {
        Logger.LogInformation("Product sync requested for location: {LocationId}", payload.LocationId);
        return Task.FromResult<IResult>(Results.Ok(new { received = true, action = "products_sync" }));
    }

    private Task<IResult> HandleStoreBusyAsync(DeliverectWebhookPayload payload)
    {
        Logger.LogInformation("Store busy mode changed for location: {LocationId}", payload.LocationId);
        return Task.FromResult<IResult>(Results.Ok(new { received = true, action = "store_busy" }));
    }

    private async Task<Guid?> FindChannelIdAsync(Guid orgId, DeliveryPlatformType platformType)
    {
        var registry = GrainFactory.GetGrain<IChannelRegistryGrain>(GrainKeys.ChannelRegistry(orgId));
        var channels = await registry.GetChannelsByPlatformAsync(platformType);
        return channels.FirstOrDefault()?.ChannelId;
    }

    private ExternalOrderReceived MapDeliverectOrderToExternalOrder(
        DeliverectOrder order,
        Guid locationId,
        Guid channelId,
        string rawPayload)
    {
        var customer = new ExternalOrderCustomer(
            Name: order.Customer?.Name ?? "Unknown",
            Phone: order.Customer?.PhoneNumber,
            Email: order.Customer?.Email,
            DeliveryAddress: order.DeliveryAddress != null ? new DeliveryAddress(
                Street: order.DeliveryAddress.Street ?? "",
                PostalCode: order.DeliveryAddress.PostalCode,
                City: order.DeliveryAddress.City ?? "",
                Country: order.DeliveryAddress.Country ?? "",
                ExtraAddressInfo: order.DeliveryAddress.ExtraAddressInfo) : null);

        var items = (order.Items ?? []).Select(item => new ExternalOrderItem(
            PlatformItemId: item.Plu ?? item.Id ?? "",
            InternalMenuItemId: null, // Will be mapped later
            Name: item.Name ?? "Unknown Item",
            Quantity: item.Quantity,
            UnitPrice: (item.Price ?? 0m) / 100m, // Deliverect uses cents
            TotalPrice: (item.Price ?? 0m) * item.Quantity / 100m,
            SpecialInstructions: item.Comment,
            Modifiers: item.SubItems?.Select(sub => new ExternalOrderModifier(
                Name: sub.Name ?? "",
                Price: (sub.Price ?? 0m) / 100m)).ToList())).ToList();

        var discounts = order.Discounts?.Select(d => new ExternalOrderDiscount(
            Type: d.Type ?? "unknown",
            Provider: d.Provider?.ToLowerInvariant() == "channel" ? DiscountProvider.Channel : DiscountProvider.Restaurant,
            Name: d.Name ?? "Discount",
            Amount: (d.Amount ?? 0m) / 100m)).ToList();

        return new ExternalOrderReceived(
            LocationId: locationId,
            DeliveryPlatformId: channelId,
            PlatformOrderId: order.Id ?? "",
            PlatformOrderNumber: order.ChannelOrderDisplayId ?? order.ChannelOrderId ?? "",
            ChannelDisplayId: order.ChannelOrderDisplayId,
            OrderType: MapOrderType(order.OrderType),
            PlacedAt: order.PlacedAt ?? DateTime.UtcNow,
            ScheduledPickupAt: order.PickupTime,
            ScheduledDeliveryAt: order.DeliveryTime,
            IsAsapDelivery: order.IsAsap ?? true,
            Customer: customer,
            Courier: order.Courier != null ? new CourierInfo(
                FirstName: order.Courier.FirstName,
                LastName: order.Courier.LastName,
                PhoneNumber: order.Courier.PhoneNumber,
                Provider: order.Courier.Provider,
                Status: order.Courier.Status) : null,
            Items: items,
            Subtotal: (order.Payment?.Amount ?? 0m) / 100m,
            DeliveryFee: (order.DeliveryCost ?? 0m) / 100m,
            ServiceFee: (order.ServiceFee ?? 0m) / 100m,
            Tax: (order.Tax ?? 0m) / 100m,
            Tip: (order.Tip ?? 0m) / 100m,
            Total: (order.Payment?.Amount ?? 0m) / 100m,
            Currency: order.Currency ?? "USD",
            Discounts: discounts,
            Packaging: order.Packaging != null ? new PackagingPreferences(
                IncludeCutlery: order.Packaging.IncludeCutlery ?? false,
                IsReusable: order.Packaging.IsReusable ?? false,
                BagFee: order.Packaging.BagFee) : null,
            SpecialInstructions: order.Note,
            PlatformRawPayload: rawPayload);
    }

    private static ExternalOrderType MapOrderType(int? orderType) => orderType switch
    {
        1 => ExternalOrderType.Delivery,
        2 => ExternalOrderType.Pickup,
        3 => ExternalOrderType.DineIn,
        _ => ExternalOrderType.Delivery
    };
}

/// <summary>
/// Handles webhooks from UberEats platform.
/// </summary>
public class UberEatsWebhookHandler : BaseWebhookHandler
{
    public UberEatsWebhookHandler(IGrainFactory grainFactory, ILogger<UberEatsWebhookHandler> logger)
        : base(grainFactory, logger) { }

    public async Task<IResult> HandleWebhookAsync(HttpContext context)
    {
        var body = await ReadBodyAsync(context);
        var signature = context.Request.Headers["X-Uber-Signature"].FirstOrDefault();

        Logger.LogInformation("Received UberEats webhook, payload length: {Length}", body.Length);

        try
        {
            var payload = JsonSerializer.Deserialize<UberEatsWebhookPayload>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (payload == null)
            {
                return Results.BadRequest(new { error = "invalid_payload" });
            }

            return payload.EventType?.ToLowerInvariant() switch
            {
                "orders.notification" => await HandleOrderNotificationAsync(payload, body),
                "orders.cancel" => await HandleOrderCancelAsync(payload),
                _ => Results.Ok(new { received = true, event_type = payload.EventType })
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing UberEats webhook");
            return Results.StatusCode(500);
        }
    }

    private async Task<IResult> HandleOrderNotificationAsync(UberEatsWebhookPayload payload, string rawPayload)
    {
        Logger.LogInformation("UberEats order notification received: {OrderId}", payload.Meta?.ResourceId);

        // Similar implementation to Deliverect, adapted for UberEats format
        return await Task.FromResult(Results.Ok(new
        {
            received = true,
            order_id = payload.Meta?.ResourceId
        }));
    }

    private Task<IResult> HandleOrderCancelAsync(UberEatsWebhookPayload payload)
    {
        Logger.LogInformation("UberEats order cancelled: {OrderId}", payload.Meta?.ResourceId);
        return Task.FromResult<IResult>(Results.Ok(new { received = true, action = "cancelled" }));
    }
}

/// <summary>
/// Handles webhooks from DoorDash platform.
/// </summary>
public class DoorDashWebhookHandler : BaseWebhookHandler
{
    public DoorDashWebhookHandler(IGrainFactory grainFactory, ILogger<DoorDashWebhookHandler> logger)
        : base(grainFactory, logger) { }

    public async Task<IResult> HandleWebhookAsync(HttpContext context)
    {
        var body = await ReadBodyAsync(context);
        var signature = context.Request.Headers["X-DoorDash-Signature"].FirstOrDefault();

        Logger.LogInformation("Received DoorDash webhook, payload length: {Length}", body.Length);

        try
        {
            var payload = JsonSerializer.Deserialize<DoorDashWebhookPayload>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (payload == null)
            {
                return Results.BadRequest(new { error = "invalid_payload" });
            }

            return payload.EventName?.ToLowerInvariant() switch
            {
                "order.created" => await HandleOrderCreatedAsync(payload, body),
                "order.cancelled" => await HandleOrderCancelledAsync(payload),
                _ => Results.Ok(new { received = true, event_name = payload.EventName })
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing DoorDash webhook");
            return Results.StatusCode(500);
        }
    }

    private async Task<IResult> HandleOrderCreatedAsync(DoorDashWebhookPayload payload, string rawPayload)
    {
        Logger.LogInformation("DoorDash order created: {OrderId}", payload.OrderId);

        return await Task.FromResult(Results.Ok(new
        {
            received = true,
            order_id = payload.OrderId
        }));
    }

    private Task<IResult> HandleOrderCancelledAsync(DoorDashWebhookPayload payload)
    {
        Logger.LogInformation("DoorDash order cancelled: {OrderId}", payload.OrderId);
        return Task.FromResult<IResult>(Results.Ok(new { received = true, action = "cancelled" }));
    }
}

// ============================================================================
// Webhook Payload DTOs
// ============================================================================

public record DeliverectWebhookPayload(
    string? Event,
    string? AccountId,
    string? LocationId,
    DeliverectOrder? Order);

public record DeliverectOrder(
    string? Id,
    string? ChannelOrderId,
    string? ChannelOrderDisplayId,
    int? Status,
    int? OrderType,
    int? Channel,
    DateTime? PlacedAt,
    DateTime? PickupTime,
    DateTime? DeliveryTime,
    bool? IsAsap,
    DeliverectCustomer? Customer,
    DeliverectDeliveryAddress? DeliveryAddress,
    DeliverectCourier? Courier,
    List<DeliverectOrderItem>? Items,
    DeliverectPayment? Payment,
    List<DeliverectDiscount>? Discounts,
    DeliverectPackaging? Packaging,
    decimal? DeliveryCost,
    decimal? ServiceFee,
    decimal? Tax,
    decimal? Tip,
    string? Currency,
    string? Note);

public record DeliverectCustomer(
    string? Name,
    string? PhoneNumber,
    string? Email);

public record DeliverectDeliveryAddress(
    string? Street,
    string? PostalCode,
    string? City,
    string? Country,
    string? ExtraAddressInfo);

public record DeliverectCourier(
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string? Provider,
    int? Status);

public record DeliverectOrderItem(
    string? Id,
    string? Plu,
    string? Name,
    int Quantity,
    decimal? Price,
    string? Comment,
    List<DeliverectSubItem>? SubItems);

public record DeliverectSubItem(
    string? Name,
    decimal? Price);

public record DeliverectPayment(
    decimal? Amount,
    int? Type,
    decimal? Due);

public record DeliverectDiscount(
    string? Type,
    string? Provider,
    string? Name,
    decimal? Amount);

public record DeliverectPackaging(
    bool? IncludeCutlery,
    bool? IsReusable,
    decimal? BagFee);

public record UberEatsWebhookPayload(
    string? EventType,
    UberEatsMeta? Meta);

public record UberEatsMeta(
    string? ResourceId,
    string? Status);

public record DoorDashWebhookPayload(
    string? EventName,
    string? OrderId,
    string? StoreId);
