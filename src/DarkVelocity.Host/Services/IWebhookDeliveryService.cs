using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.Host.Services;

/// <summary>
/// Service for delivering webhooks to subscriber endpoints.
/// </summary>
public interface IWebhookDeliveryService
{
    /// <summary>
    /// Delivers a webhook payload to the specified URL.
    /// </summary>
    /// <param name="url">The endpoint URL to deliver to.</param>
    /// <param name="payload">The JSON payload to send.</param>
    /// <param name="secret">Optional secret for HMAC signature.</param>
    /// <param name="headers">Optional additional headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The delivery result.</returns>
    Task<WebhookDeliveryResult> DeliverAsync(
        string url,
        object payload,
        string? secret = null,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an HMAC-SHA256 signature for the payload.
    /// </summary>
    string GenerateSignature(string payload, string secret);

    /// <summary>
    /// Verifies an HMAC-SHA256 signature.
    /// </summary>
    bool VerifySignature(string payload, string signature, string secret);
}

/// <summary>
/// Result of a webhook delivery attempt.
/// </summary>
[GenerateSerializer]
public record WebhookDeliveryResult
{
    /// <summary>
    /// Whether the delivery was successful (2xx response).
    /// </summary>
    [Id(0)] public required bool Success { get; init; }

    /// <summary>
    /// HTTP status code from the response.
    /// </summary>
    [Id(1)] public int? StatusCode { get; init; }

    /// <summary>
    /// Error message if the delivery failed.
    /// </summary>
    [Id(2)] public string? ErrorMessage { get; init; }

    /// <summary>
    /// Response body if available.
    /// </summary>
    [Id(3)] public string? ResponseBody { get; init; }

    /// <summary>
    /// Response time in milliseconds.
    /// </summary>
    [Id(4)] public int ResponseTimeMs { get; init; }

    /// <summary>
    /// Response headers from the endpoint.
    /// </summary>
    [Id(5)] public Dictionary<string, string>? ResponseHeaders { get; init; }

    /// <summary>
    /// Whether this failure should be retried.
    /// </summary>
    [Id(6)] public bool ShouldRetry { get; init; }

    public static WebhookDeliveryResult Succeeded(int statusCode, string? responseBody, int responseTimeMs, Dictionary<string, string>? responseHeaders = null)
        => new()
        {
            Success = true,
            StatusCode = statusCode,
            ResponseBody = responseBody,
            ResponseTimeMs = responseTimeMs,
            ResponseHeaders = responseHeaders,
            ShouldRetry = false
        };

    public static WebhookDeliveryResult Failed(string errorMessage, int? statusCode = null, string? responseBody = null, int responseTimeMs = 0, bool shouldRetry = true)
        => new()
        {
            Success = false,
            StatusCode = statusCode,
            ErrorMessage = errorMessage,
            ResponseBody = responseBody,
            ResponseTimeMs = responseTimeMs,
            ShouldRetry = shouldRetry
        };
}

/// <summary>
/// Default implementation of webhook delivery service.
/// </summary>
public sealed class WebhookDeliveryService : IWebhookDeliveryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebhookDeliveryService> _logger;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public WebhookDeliveryService(HttpClient httpClient, ILogger<WebhookDeliveryService> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = DefaultTimeout;
        _logger = logger;
    }

    public async Task<WebhookDeliveryResult> DeliverAsync(
        string url,
        object payload,
        string? secret = null,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // Add custom headers
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            // Add signature header if secret is provided
            if (!string.IsNullOrEmpty(secret))
            {
                var signature = GenerateSignature(jsonPayload, secret);
                request.Headers.TryAddWithoutValidation("X-Webhook-Signature", signature);
                request.Headers.TryAddWithoutValidation("X-Signature-256", $"sha256={signature}");
            }

            // Add standard webhook headers
            request.Headers.TryAddWithoutValidation("X-Webhook-Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            request.Headers.TryAddWithoutValidation("User-Agent", "DarkVelocity-Webhook/1.0");

            _logger.LogDebug(
                "Delivering webhook to {Url}, payload size: {Size} bytes",
                url, jsonPayload.Length);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            var responseTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseHeaders = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Webhook delivered successfully to {Url}. Status: {StatusCode}, Response time: {ResponseTimeMs}ms",
                    url, (int)response.StatusCode, responseTimeMs);

                return WebhookDeliveryResult.Succeeded(
                    (int)response.StatusCode,
                    responseBody,
                    responseTimeMs,
                    responseHeaders);
            }
            else
            {
                var shouldRetry = ShouldRetryStatusCode(response.StatusCode);

                _logger.LogWarning(
                    "Webhook delivery failed to {Url}. Status: {StatusCode}, Response: {Response}, ShouldRetry: {ShouldRetry}",
                    url, (int)response.StatusCode, TruncateResponse(responseBody), shouldRetry);

                return WebhookDeliveryResult.Failed(
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    (int)response.StatusCode,
                    responseBody,
                    responseTimeMs,
                    shouldRetry);
            }
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || !cancellationToken.IsCancellationRequested)
        {
            var responseTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogWarning("Webhook delivery timed out to {Url} after {ResponseTimeMs}ms", url, responseTimeMs);
            return WebhookDeliveryResult.Failed("Request timed out", responseTimeMs: responseTimeMs, shouldRetry: true);
        }
        catch (HttpRequestException ex)
        {
            var responseTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogWarning(ex, "Webhook delivery failed to {Url}: {Message}", url, ex.Message);
            return WebhookDeliveryResult.Failed(ex.Message, responseTimeMs: responseTimeMs, shouldRetry: true);
        }
        catch (Exception ex)
        {
            var responseTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "Unexpected error delivering webhook to {Url}", url);
            return WebhookDeliveryResult.Failed(ex.Message, responseTimeMs: responseTimeMs, shouldRetry: false);
        }
    }

    public string GenerateSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool VerifySignature(string payload, string signature, string secret)
    {
        var expectedSignature = GenerateSignature(payload, secret);

        // Handle both raw signature and "sha256=" prefixed format
        var actualSignature = signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? signature[7..]
            : signature;

        return string.Equals(expectedSignature, actualSignature, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldRetryStatusCode(HttpStatusCode statusCode)
    {
        // Retry on server errors and rate limiting
        return statusCode switch
        {
            HttpStatusCode.TooManyRequests => true,
            HttpStatusCode.InternalServerError => true,
            HttpStatusCode.BadGateway => true,
            HttpStatusCode.ServiceUnavailable => true,
            HttpStatusCode.GatewayTimeout => true,
            _ => false
        };
    }

    private static string TruncateResponse(string response, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(response)) return "(empty)";
        return response.Length <= maxLength ? response : response[..maxLength] + "...";
    }
}

/// <summary>
/// Stub webhook delivery service for development/testing.
/// </summary>
public sealed class StubWebhookDeliveryService : IWebhookDeliveryService
{
    private readonly ILogger<StubWebhookDeliveryService> _logger;

    public StubWebhookDeliveryService(ILogger<StubWebhookDeliveryService> logger)
    {
        _logger = logger;
    }

    public Task<WebhookDeliveryResult> DeliverAsync(
        string url,
        object payload,
        string? secret = null,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _logger.LogInformation(
            "[STUB WEBHOOK] URL: {Url}, Payload size: {Size}, HasSecret: {HasSecret}",
            url, jsonPayload.Length, !string.IsNullOrEmpty(secret));

        return Task.FromResult(WebhookDeliveryResult.Succeeded(
            statusCode: 200,
            responseBody: """{"status": "ok", "stub": true}""",
            responseTimeMs: 50));
    }

    public string GenerateSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool VerifySignature(string payload, string signature, string secret)
    {
        var expectedSignature = GenerateSignature(payload, secret);
        var actualSignature = signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? signature[7..]
            : signature;
        return string.Equals(expectedSignature, actualSignature, StringComparison.OrdinalIgnoreCase);
    }
}
