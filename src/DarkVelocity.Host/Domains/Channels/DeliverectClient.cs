using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DarkVelocity.Host.Adapters;

// ============================================================================
// Deliverect Client Interface
// ============================================================================

/// <summary>
/// HTTP client interface for Deliverect API interactions.
/// </summary>
public interface IDeliverectClient
{
    /// <summary>
    /// Updates the status of an order on Deliverect.
    /// </summary>
    Task<DeliverectApiResponse> UpdateOrderStatusAsync(DeliverectOrderStatusUpdate request);

    /// <summary>
    /// Syncs products/menu to Deliverect.
    /// </summary>
    Task<DeliverectApiResponse> SyncProductsAsync(DeliverectProductSync request);

    /// <summary>
    /// Snoozes or unsnoozes a product on Deliverect.
    /// </summary>
    Task<DeliverectApiResponse> SnoozeProductAsync(DeliverectSnoozeRequest request);

    /// <summary>
    /// Updates store status (open/closed/busy) on Deliverect.
    /// </summary>
    Task<DeliverectApiResponse> UpdateStoreStatusAsync(DeliverectStoreStatusUpdate request);

    /// <summary>
    /// Gets the current access token for API calls.
    /// </summary>
    Task<string?> GetAccessTokenAsync();
}

// ============================================================================
// Deliverect API DTOs
// ============================================================================

/// <summary>
/// Generic response from Deliverect API.
/// </summary>
public record DeliverectApiResponse(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    string? RawResponse);

/// <summary>
/// Request to update order status on Deliverect.
/// </summary>
public record DeliverectOrderStatusUpdate
{
    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = "";

    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("pickupTime")]
    public DateTime? PickupTime { get; init; }

    [JsonPropertyName("cancelReason")]
    public string? CancelReason { get; init; }

    [JsonPropertyName("preparationTime")]
    public int? PreparationTime { get; init; }
}

/// <summary>
/// Request to sync products to Deliverect.
/// </summary>
public record DeliverectProductSync
{
    [JsonPropertyName("locationId")]
    public string LocationId { get; init; } = "";

    [JsonPropertyName("products")]
    public List<DeliverectProduct> Products { get; init; } = [];

    [JsonPropertyName("categories")]
    public List<DeliverectCategory> Categories { get; init; } = [];
}

/// <summary>
/// Product definition for Deliverect.
/// </summary>
public record DeliverectProduct
{
    [JsonPropertyName("productType")]
    public int ProductType { get; init; } = 1;

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("plu")]
    public string Plu { get; init; } = "";

    [JsonPropertyName("price")]
    public int Price { get; init; } // In cents

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; init; }

    [JsonPropertyName("max")]
    public int Max { get; init; } = 999;

    [JsonPropertyName("snoozed")]
    public bool Snoozed { get; init; }

    [JsonPropertyName("subProducts")]
    public List<string> SubProducts { get; init; } = [];

    [JsonPropertyName("productTags")]
    public List<int> ProductTags { get; init; } = [];

    [JsonPropertyName("deliveryTax")]
    public decimal? DeliveryTax { get; init; }

    [JsonPropertyName("takeawayTax")]
    public decimal? TakeawayTax { get; init; }

    [JsonPropertyName("eatInTax")]
    public decimal? EatInTax { get; init; }
}

/// <summary>
/// Category definition for Deliverect.
/// </summary>
public record DeliverectCategory
{
    [JsonPropertyName("categoryId")]
    public string CategoryId { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
}

/// <summary>
/// Request to snooze/unsnooze a product.
/// </summary>
public record DeliverectSnoozeRequest
{
    [JsonPropertyName("locationId")]
    public string LocationId { get; init; } = "";

    [JsonPropertyName("plu")]
    public string Plu { get; init; } = "";

    [JsonPropertyName("snoozed")]
    public bool Snoozed { get; init; }

    [JsonPropertyName("snoozedUntil")]
    public DateTime? SnoozedUntil { get; init; }
}

/// <summary>
/// Request to update store status.
/// </summary>
public record DeliverectStoreStatusUpdate
{
    [JsonPropertyName("locationId")]
    public string LocationId { get; init; } = "";

    [JsonPropertyName("isOpen")]
    public bool IsOpen { get; init; }

    [JsonPropertyName("busyMode")]
    public bool BusyMode { get; init; }

    [JsonPropertyName("additionalPrepTime")]
    public int? AdditionalPrepTime { get; init; }
}

// ============================================================================
// Deliverect Client Configuration
// ============================================================================

/// <summary>
/// Configuration options for Deliverect client.
/// </summary>
public class DeliverectClientOptions
{
    public const string SectionName = "Deliverect";

    /// <summary>
    /// Base URL for Deliverect API (production).
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.deliverect.com";

    /// <summary>
    /// Base URL for Deliverect staging API.
    /// </summary>
    public string StagingUrl { get; set; } = "https://api.staging.deliverect.com";

    /// <summary>
    /// OAuth2 client ID for M2M authentication.
    /// </summary>
    public string ClientId { get; set; } = "";

    /// <summary>
    /// OAuth2 client secret for M2M authentication.
    /// </summary>
    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// Webhook secret for HMAC validation.
    /// </summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to use staging environment.
    /// </summary>
    public bool UseStaging { get; set; } = false;

    /// <summary>
    /// Account ID for Deliverect.
    /// </summary>
    public string AccountId { get; set; } = "";

    /// <summary>
    /// Gets the effective base URL based on environment.
    /// </summary>
    public string EffectiveBaseUrl => UseStaging ? StagingUrl : BaseUrl;
}

// ============================================================================
// Deliverect Client Implementation
// ============================================================================

/// <summary>
/// HTTP client implementation for Deliverect API.
/// Handles authentication, retries, and error handling.
/// </summary>
public class DeliverectClient : IDeliverectClient
{
    private readonly HttpClient _httpClient;
    private readonly DeliverectClientOptions _options;
    private readonly ILogger<DeliverectClient> _logger;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public DeliverectClient(
        HttpClient httpClient,
        IOptions<DeliverectClientOptions> options,
        ILogger<DeliverectClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.EffectiveBaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<DeliverectApiResponse> UpdateOrderStatusAsync(DeliverectOrderStatusUpdate request)
    {
        return await ExecuteWithAuthAsync(async () =>
        {
            var endpoint = $"/orders/{request.OrderId}/status";
            var content = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            _logger.LogInformation("Updating order status on Deliverect: {OrderId} -> {Status}",
                request.OrderId, request.Status);

            var response = await _httpClient.PostAsync(endpoint, content);
            return await ProcessResponseAsync(response);
        });
    }

    public async Task<DeliverectApiResponse> SyncProductsAsync(DeliverectProductSync request)
    {
        return await ExecuteWithAuthAsync(async () =>
        {
            var endpoint = $"/products/{_options.AccountId}";
            var content = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            _logger.LogInformation("Syncing {Count} products to Deliverect for location {LocationId}",
                request.Products.Count, request.LocationId);

            var response = await _httpClient.PostAsync(endpoint, content);
            return await ProcessResponseAsync(response);
        });
    }

    public async Task<DeliverectApiResponse> SnoozeProductAsync(DeliverectSnoozeRequest request)
    {
        return await ExecuteWithAuthAsync(async () =>
        {
            var endpoint = $"/products/{_options.AccountId}/snooze";
            var content = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            _logger.LogInformation("Snoozing product on Deliverect: {Plu} at {LocationId}, snoozed: {Snoozed}",
                request.Plu, request.LocationId, request.Snoozed);

            var response = await _httpClient.PostAsync(endpoint, content);
            return await ProcessResponseAsync(response);
        });
    }

    public async Task<DeliverectApiResponse> UpdateStoreStatusAsync(DeliverectStoreStatusUpdate request)
    {
        return await ExecuteWithAuthAsync(async () =>
        {
            var endpoint = $"/locations/{request.LocationId}/status";
            var content = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            _logger.LogInformation("Updating store status on Deliverect: {LocationId}, open: {IsOpen}, busy: {BusyMode}",
                request.LocationId, request.IsOpen, request.BusyMode);

            var response = await _httpClient.PatchAsync(endpoint, content);
            return await ProcessResponseAsync(response);
        });
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        await _tokenLock.WaitAsync();
        try
        {
            if (_accessToken != null && DateTime.UtcNow < _tokenExpiry)
            {
                return _accessToken;
            }

            var tokenEndpoint = "/oauth/token";
            var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = "genericPOS"
            });

            _logger.LogDebug("Requesting new access token from Deliverect");

            var response = await _httpClient.PostAsync(tokenEndpoint, tokenRequest);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get Deliverect access token: {StatusCode} - {Body}",
                    response.StatusCode, errorBody);
                return null;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<DeliverectTokenResponse>();
            if (tokenResponse == null)
            {
                _logger.LogError("Failed to parse Deliverect token response");
                return null;
            }

            _accessToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60); // Refresh 1 minute early

            _logger.LogDebug("Obtained new Deliverect access token, expires at {Expiry}", _tokenExpiry);

            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<DeliverectApiResponse> ExecuteWithAuthAsync(Func<Task<DeliverectApiResponse>> action)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return new DeliverectApiResponse(false, "auth_failed", "Failed to obtain access token", null);
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            return await action();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to Deliverect failed");
            return new DeliverectApiResponse(false, "http_error", ex.Message, null);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Request to Deliverect timed out");
            return new DeliverectApiResponse(false, "timeout", "Request timed out", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Deliverect API");
            return new DeliverectApiResponse(false, "unexpected_error", ex.Message, null);
        }
    }

    private async Task<DeliverectApiResponse> ProcessResponseAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Deliverect API call successful: {StatusCode}", response.StatusCode);
            return new DeliverectApiResponse(true, null, null, body);
        }

        _logger.LogWarning("Deliverect API call failed: {StatusCode} - {Body}", response.StatusCode, body);

        try
        {
            var errorResponse = JsonSerializer.Deserialize<DeliverectErrorResponse>(body, JsonOptions);
            return new DeliverectApiResponse(
                false,
                errorResponse?.Code ?? response.StatusCode.ToString(),
                errorResponse?.Message ?? body,
                body);
        }
        catch
        {
            return new DeliverectApiResponse(
                false,
                response.StatusCode.ToString(),
                body,
                body);
        }
    }

    private record DeliverectTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string TokenType);

    private record DeliverectErrorResponse(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("message")] string? Message);
}

// ============================================================================
// Service Registration Extension
// ============================================================================

/// <summary>
/// Extension methods for registering Deliverect services.
/// </summary>
public static class DeliverectServiceExtensions
{
    /// <summary>
    /// Adds Deliverect HTTP client and adapter services to the service collection.
    /// </summary>
    public static IServiceCollection AddDeliverectServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure options
        services.Configure<DeliverectClientOptions>(configuration.GetSection(DeliverectClientOptions.SectionName));

        // Register HTTP client with resilience
        services.AddHttpClient<IDeliverectClient, DeliverectClient>()
            .ConfigureHttpClient((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<DeliverectClientOptions>>().Value;
                client.BaseAddress = new Uri(options.EffectiveBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .AddStandardResilienceHandler(); // Adds retry, circuit breaker, timeout

        // Register adapter
        services.AddTransient<DeliverectAdapter>();
        services.AddSingleton<IPlatformAdapterFactory, PlatformAdapterFactory>();

        return services;
    }
}
