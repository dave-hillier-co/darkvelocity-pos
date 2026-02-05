using System.Net.Http.Json;
using System.Text.Json;
using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.Adapters;

// ============================================================================
// Platform Adapter Interface
// ============================================================================

/// <summary>
/// Interface for delivery platform adapters.
/// Provides a unified API for interacting with different delivery platforms.
/// </summary>
public interface IDeliveryPlatformAdapter
{
    /// <summary>
    /// The platform type this adapter handles.
    /// </summary>
    DeliveryPlatformType PlatformType { get; }

    /// <summary>
    /// Parses a webhook payload into an ExternalOrderReceived event.
    /// </summary>
    Task<ExternalOrderReceived> ParseOrderAsync(string webhookPayload, Guid locationId, Guid channelId);

    /// <summary>
    /// Accepts an order on the platform.
    /// </summary>
    Task<PlatformResponse> AcceptOrderAsync(string externalOrderId, DateTime? estimatedPickupTime = null);

    /// <summary>
    /// Rejects an order on the platform.
    /// </summary>
    Task<PlatformResponse> RejectOrderAsync(string externalOrderId, string reason);

    /// <summary>
    /// Updates the order status on the platform.
    /// </summary>
    Task<PlatformResponse> UpdateStatusAsync(string externalOrderId, ExternalOrderStatus status);

    /// <summary>
    /// Pushes a menu to the platform.
    /// </summary>
    Task<MenuPushResult> PushMenuAsync(MenuSnapshot menu, string externalStoreId);

    /// <summary>
    /// Updates item availability (snooze/unsnooze) on the platform.
    /// </summary>
    Task<PlatformResponse> UpdateItemAvailabilityAsync(string externalStoreId, string externalItemId, bool isAvailable);

    /// <summary>
    /// Updates store open/closed status on the platform.
    /// </summary>
    Task<PlatformResponse> UpdateStoreStatusAsync(string externalStoreId, bool isOpen, int? additionalPrepMinutes = null);

    /// <summary>
    /// Validates a webhook signature.
    /// </summary>
    bool ValidateWebhookSignature(string payload, string signature, string secret);
}

/// <summary>
/// Response from a platform API call.
/// </summary>
public record PlatformResponse(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    string? RawResponse);

/// <summary>
/// Result of pushing a menu to a platform.
/// </summary>
public record MenuPushResult(
    bool Success,
    int ItemsPushed,
    int ItemsFailed,
    List<string> Errors);

/// <summary>
/// Menu snapshot for pushing to external platforms.
/// </summary>
public record MenuSnapshot(
    Guid MenuId,
    string Name,
    List<MenuCategorySnapshot> Categories,
    string Currency);

/// <summary>
/// Category snapshot for menu push.
/// </summary>
public record MenuCategorySnapshot(
    string Id,
    string Name,
    int SortOrder,
    List<MenuItemSnapshot> Items);

/// <summary>
/// Item snapshot for menu push.
/// </summary>
public record MenuItemSnapshot(
    Guid ItemId,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    bool IsAvailable,
    List<ModifierGroupSnapshot> ModifierGroups,
    List<string> Tags);

/// <summary>
/// Modifier group snapshot for menu push.
/// </summary>
public record ModifierGroupSnapshot(
    string Id,
    string Name,
    int MinSelections,
    int MaxSelections,
    List<ModifierSnapshot> Modifiers);

/// <summary>
/// Modifier snapshot for menu push.
/// </summary>
public record ModifierSnapshot(
    string Id,
    string Name,
    decimal Price);

// ============================================================================
// Deliverect Adapter Implementation
// ============================================================================

/// <summary>
/// Adapter for Deliverect aggregator platform.
/// Implements the Deliverect POS API for order management and menu sync.
/// </summary>
public class DeliverectAdapter : IDeliveryPlatformAdapter
{
    private readonly IDeliverectClient _client;
    private readonly ILogger<DeliverectAdapter> _logger;

    public DeliveryPlatformType PlatformType => DeliveryPlatformType.Deliverect;

    public DeliverectAdapter(IDeliverectClient client, ILogger<DeliverectAdapter> logger)
    {
        _client = client;
        _logger = logger;
    }

    public Task<ExternalOrderReceived> ParseOrderAsync(string webhookPayload, Guid locationId, Guid channelId)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var payload = JsonSerializer.Deserialize<DeliverectOrderPayload>(webhookPayload, options)
            ?? throw new InvalidOperationException("Failed to parse Deliverect order payload");

        var order = payload.Order ?? throw new InvalidOperationException("Order data is missing from payload");

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
            InternalMenuItemId: null,
            Name: item.Name ?? "Unknown Item",
            Quantity: item.Quantity,
            UnitPrice: (item.Price ?? 0m) / 100m,
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

        var result = new ExternalOrderReceived(
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
            PlatformRawPayload: webhookPayload);

        return Task.FromResult(result);
    }

    public async Task<PlatformResponse> AcceptOrderAsync(string externalOrderId, DateTime? estimatedPickupTime = null)
    {
        try
        {
            var request = new DeliverectOrderStatusUpdate
            {
                OrderId = externalOrderId,
                Status = 20, // Deliverect status code for Accepted
                PickupTime = estimatedPickupTime
            };

            var response = await _client.UpdateOrderStatusAsync(request);
            return new PlatformResponse(response.Success, response.ErrorCode, response.ErrorMessage, response.RawResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to accept order {OrderId} on Deliverect", externalOrderId);
            return new PlatformResponse(false, "exception", ex.Message, null);
        }
    }

    public async Task<PlatformResponse> RejectOrderAsync(string externalOrderId, string reason)
    {
        try
        {
            var request = new DeliverectOrderStatusUpdate
            {
                OrderId = externalOrderId,
                Status = 120, // Deliverect status code for Failed/Rejected
                CancelReason = reason
            };

            var response = await _client.UpdateOrderStatusAsync(request);
            return new PlatformResponse(response.Success, response.ErrorCode, response.ErrorMessage, response.RawResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject order {OrderId} on Deliverect", externalOrderId);
            return new PlatformResponse(false, "exception", ex.Message, null);
        }
    }

    public async Task<PlatformResponse> UpdateStatusAsync(string externalOrderId, ExternalOrderStatus status)
    {
        try
        {
            var deliverectStatus = MapToDeliverectStatus(status);

            var request = new DeliverectOrderStatusUpdate
            {
                OrderId = externalOrderId,
                Status = deliverectStatus
            };

            var response = await _client.UpdateOrderStatusAsync(request);
            return new PlatformResponse(response.Success, response.ErrorCode, response.ErrorMessage, response.RawResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update status for order {OrderId} on Deliverect", externalOrderId);
            return new PlatformResponse(false, "exception", ex.Message, null);
        }
    }

    public async Task<MenuPushResult> PushMenuAsync(MenuSnapshot menu, string externalStoreId)
    {
        try
        {
            var deliverectProducts = new DeliverectProductSync
            {
                LocationId = externalStoreId,
                Products = menu.Categories.SelectMany(cat => cat.Items.Select(item => new DeliverectProduct
                {
                    ProductType = 1, // Regular product
                    Name = item.Name,
                    Plu = item.ItemId.ToString(),
                    Price = (int)(item.Price * 100), // Convert to cents
                    Description = item.Description,
                    ImageUrl = item.ImageUrl,
                    Max = 999,
                    Snoozed = !item.IsAvailable,
                    SubProducts = item.ModifierGroups.SelectMany(g => g.Modifiers.Select(m => m.Id)).ToList(),
                    ProductTags = item.Tags.Select(t => int.TryParse(t, out var id) ? id : 0).Where(t => t > 0).ToList()
                })).ToList(),
                Categories = menu.Categories.Select(cat => new DeliverectCategory
                {
                    CategoryId = cat.Id,
                    Name = cat.Name
                }).ToList()
            };

            var response = await _client.SyncProductsAsync(deliverectProducts);

            return new MenuPushResult(
                Success: response.Success,
                ItemsPushed: deliverectProducts.Products.Count,
                ItemsFailed: 0,
                Errors: response.Success ? [] : [response.ErrorMessage ?? "Unknown error"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push menu to Deliverect for store {StoreId}", externalStoreId);
            return new MenuPushResult(false, 0, 0, [ex.Message]);
        }
    }

    public async Task<PlatformResponse> UpdateItemAvailabilityAsync(string externalStoreId, string externalItemId, bool isAvailable)
    {
        try
        {
            var request = new DeliverectSnoozeRequest
            {
                LocationId = externalStoreId,
                Plu = externalItemId,
                Snoozed = !isAvailable
            };

            var response = await _client.SnoozeProductAsync(request);
            return new PlatformResponse(response.Success, response.ErrorCode, response.ErrorMessage, response.RawResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update item availability on Deliverect: {ItemId}", externalItemId);
            return new PlatformResponse(false, "exception", ex.Message, null);
        }
    }

    public async Task<PlatformResponse> UpdateStoreStatusAsync(string externalStoreId, bool isOpen, int? additionalPrepMinutes = null)
    {
        try
        {
            var request = new DeliverectStoreStatusUpdate
            {
                LocationId = externalStoreId,
                IsOpen = isOpen,
                BusyMode = additionalPrepMinutes.HasValue,
                AdditionalPrepTime = additionalPrepMinutes
            };

            var response = await _client.UpdateStoreStatusAsync(request);
            return new PlatformResponse(response.Success, response.ErrorCode, response.ErrorMessage, response.RawResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update store status on Deliverect: {StoreId}", externalStoreId);
            return new PlatformResponse(false, "exception", ex.Message, null);
        }
    }

    public bool ValidateWebhookSignature(string payload, string signature, string secret)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
            return false;

        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        var computedSignature = Convert.ToHexString(computedHash).ToLowerInvariant();

        var providedSignature = signature.Contains('=')
            ? signature.Split('=').Last().ToLowerInvariant()
            : signature.ToLowerInvariant();

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(computedSignature),
            System.Text.Encoding.UTF8.GetBytes(providedSignature));
    }

    private static ExternalOrderType MapOrderType(int? orderType) => orderType switch
    {
        1 => ExternalOrderType.Delivery,
        2 => ExternalOrderType.Pickup,
        3 => ExternalOrderType.DineIn,
        _ => ExternalOrderType.Delivery
    };

    private static int MapToDeliverectStatus(ExternalOrderStatus status) => status switch
    {
        ExternalOrderStatus.Pending => 10,
        ExternalOrderStatus.Accepted => 20,
        ExternalOrderStatus.Preparing => 30,
        ExternalOrderStatus.Ready => 40,
        ExternalOrderStatus.PickedUp => 50,
        ExternalOrderStatus.Delivered => 60,
        ExternalOrderStatus.Cancelled => 110,
        ExternalOrderStatus.Failed => 120,
        ExternalOrderStatus.Rejected => 120,
        _ => 10
    };
}

// ============================================================================
// Deliverect DTOs for Adapter
// ============================================================================

internal record DeliverectOrderPayload(DeliverectOrderData? Order);

internal record DeliverectOrderData(
    string? Id,
    string? ChannelOrderId,
    string? ChannelOrderDisplayId,
    int? Status,
    int? OrderType,
    DateTime? PlacedAt,
    DateTime? PickupTime,
    DateTime? DeliveryTime,
    bool? IsAsap,
    DeliverectCustomerData? Customer,
    DeliverectAddressData? DeliveryAddress,
    DeliverectCourierData? Courier,
    List<DeliverectItemData>? Items,
    DeliverectPaymentData? Payment,
    List<DeliverectDiscountData>? Discounts,
    DeliverectPackagingData? Packaging,
    decimal? DeliveryCost,
    decimal? ServiceFee,
    decimal? Tax,
    decimal? Tip,
    string? Currency,
    string? Note);

internal record DeliverectCustomerData(string? Name, string? PhoneNumber, string? Email);
internal record DeliverectAddressData(string? Street, string? PostalCode, string? City, string? Country, string? ExtraAddressInfo);
internal record DeliverectCourierData(string? FirstName, string? LastName, string? PhoneNumber, string? Provider, int? Status);
internal record DeliverectItemData(string? Id, string? Plu, string? Name, int Quantity, decimal? Price, string? Comment, List<DeliverectSubItemData>? SubItems);
internal record DeliverectSubItemData(string? Name, decimal? Price);
internal record DeliverectPaymentData(decimal? Amount, int? Type);
internal record DeliverectDiscountData(string? Type, string? Provider, string? Name, decimal? Amount);
internal record DeliverectPackagingData(bool? IncludeCutlery, bool? IsReusable, decimal? BagFee);

// ============================================================================
// Platform Adapter Factory
// ============================================================================

/// <summary>
/// Factory for creating platform adapters based on platform type.
/// </summary>
public interface IPlatformAdapterFactory
{
    IDeliveryPlatformAdapter GetAdapter(DeliveryPlatformType platformType);
}

/// <summary>
/// Default implementation of platform adapter factory.
/// </summary>
public class PlatformAdapterFactory : IPlatformAdapterFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PlatformAdapterFactory> _logger;

    public PlatformAdapterFactory(IServiceProvider serviceProvider, ILogger<PlatformAdapterFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public IDeliveryPlatformAdapter GetAdapter(DeliveryPlatformType platformType)
    {
        return platformType switch
        {
            DeliveryPlatformType.Deliverect => _serviceProvider.GetRequiredService<DeliverectAdapter>(),
            DeliveryPlatformType.UberEats => throw new NotImplementedException("UberEats adapter not yet implemented"),
            DeliveryPlatformType.DoorDash => throw new NotImplementedException("DoorDash adapter not yet implemented"),
            DeliveryPlatformType.Deliveroo => throw new NotImplementedException("Deliveroo adapter not yet implemented"),
            DeliveryPlatformType.JustEat => throw new NotImplementedException("JustEat adapter not yet implemented"),
            _ => throw new ArgumentException($"No adapter available for platform type: {platformType}")
        };
    }
}
