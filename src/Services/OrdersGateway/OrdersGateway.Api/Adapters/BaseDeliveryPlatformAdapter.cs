using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DarkVelocity.OrdersGateway.Api.Dtos;
using DarkVelocity.OrdersGateway.Api.Entities;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.OrdersGateway.Api.Adapters;

/// <summary>
/// Base class for delivery platform adapters with common functionality.
/// </summary>
public abstract class BaseDeliveryPlatformAdapter : IDeliveryPlatformAdapter
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;
    protected readonly JsonSerializerOptions JsonOptions;

    public abstract string PlatformType { get; }

    protected BaseDeliveryPlatformAdapter(HttpClient httpClient, ILogger logger)
    {
        HttpClient = httpClient;
        Logger = logger;
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };
    }

    #region Abstract Methods (must be implemented by specific adapters)

    public abstract Task<ConnectionResult> TestConnectionAsync(PlatformCredentials credentials, CancellationToken cancellationToken = default);
    public abstract Task<ConnectionResult> ConnectAsync(DeliveryPlatform platform, PlatformCredentials credentials, CancellationToken cancellationToken = default);
    public abstract Task DisconnectAsync(DeliveryPlatform platform, CancellationToken cancellationToken = default);
    public abstract Task<WebhookParseResult> ParseWebhookAsync(string payload, string? signature, DeliveryPlatform platform, CancellationToken cancellationToken = default);
    public abstract Task<OrderActionResult> AcceptOrderAsync(DeliveryPlatform platform, string platformOrderId, int prepTimeMinutes, CancellationToken cancellationToken = default);
    public abstract Task<OrderActionResult> RejectOrderAsync(DeliveryPlatform platform, string platformOrderId, string reason, CancellationToken cancellationToken = default);
    public abstract Task<OrderActionResult> MarkReadyAsync(DeliveryPlatform platform, string platformOrderId, CancellationToken cancellationToken = default);
    public abstract Task<OrderActionResult> CancelOrderAsync(DeliveryPlatform platform, string platformOrderId, string reason, CancellationToken cancellationToken = default);
    public abstract Task<OrderActionResult> UpdatePrepTimeAsync(DeliveryPlatform platform, string platformOrderId, int newPrepTimeMinutes, CancellationToken cancellationToken = default);
    public abstract Task<MenuSyncResult> SyncMenuAsync(DeliveryPlatform platform, Guid locationId, IEnumerable<MenuItemForSync> items, CancellationToken cancellationToken = default);

    #endregion

    #region Default Implementations (can be overridden)

    public virtual Task<OrderTrackingDto?> GetTrackingAsync(DeliveryPlatform platform, string platformOrderId, CancellationToken cancellationToken = default)
    {
        // Default: tracking not supported
        return Task.FromResult<OrderTrackingDto?>(null);
    }

    public virtual Task<bool> UpdateItemAvailabilityAsync(DeliveryPlatform platform, string platformItemId, bool isAvailable, CancellationToken cancellationToken = default)
    {
        Logger.LogWarning("UpdateItemAvailability not implemented for {PlatformType}", PlatformType);
        return Task.FromResult(false);
    }

    public virtual Task<bool> UpdateItemPriceAsync(DeliveryPlatform platform, string platformItemId, decimal price, CancellationToken cancellationToken = default)
    {
        Logger.LogWarning("UpdateItemPrice not implemented for {PlatformType}", PlatformType);
        return Task.FromResult(false);
    }

    public virtual Task<bool> SetStoreStatusAsync(DeliveryPlatform platform, string platformStoreId, bool isOpen, CancellationToken cancellationToken = default)
    {
        Logger.LogWarning("SetStoreStatus not implemented for {PlatformType}", PlatformType);
        return Task.FromResult(false);
    }

    public virtual Task<bool> SetBusyModeAsync(DeliveryPlatform platform, string platformStoreId, bool isBusy, int? additionalPrepMinutes, CancellationToken cancellationToken = default)
    {
        Logger.LogWarning("SetBusyMode not implemented for {PlatformType}", PlatformType);
        return Task.FromResult(false);
    }

    #endregion

    #region Protected Helper Methods

    protected async Task<T?> SendRequestAsync<T>(
        HttpMethod method,
        string url,
        object? body = null,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, url);

        if (body != null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        if (headers != null)
        {
            foreach (var (key, value) in headers)
            {
                request.Headers.TryAddWithoutValidation(key, value);
            }
        }

        try
        {
            using var response = await HttpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError("Platform API error: {StatusCode} - {Content}", response.StatusCode, content);
                return default;
            }

            if (string.IsNullOrEmpty(content))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(content, JsonOptions);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error calling platform API: {Url}", url);
            throw;
        }
    }

    protected void SetBearerToken(string token)
    {
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    protected bool ValidateWebhookSignature(string payload, string? signature, string secret, string algorithm = "sha256")
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
        {
            return false;
        }

        using var hmac = algorithm.ToLowerInvariant() switch
        {
            "sha256" => new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret)),
            "sha1" => new System.Security.Cryptography.HMACSHA1(Encoding.UTF8.GetBytes(secret)),
            _ => throw new ArgumentException($"Unsupported algorithm: {algorithm}")
        };

        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expectedSignature = Convert.ToHexString(hash).ToLowerInvariant();

        // Handle signatures with prefixes like "sha256="
        var actualSignature = signature.Contains('=')
            ? signature.Split('=').Last().ToLowerInvariant()
            : signature.ToLowerInvariant();

        return string.Equals(expectedSignature, actualSignature, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
