using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DarkVelocity.Host.Auth;

public class ApiKeyAuthOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public string HeaderName { get; set; } = "Authorization";
}

public class ApiKeyAuthHandler : AuthenticationHandler<ApiKeyAuthOptions>
{
    private readonly IGrainFactory _grainFactory;

    public ApiKeyAuthHandler(
        IOptionsMonitor<ApiKeyAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IGrainFactory grainFactory)
        : base(options, logger, encoder)
    {
        _grainFactory = grainFactory;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for Authorization header
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var authHeader))
        {
            return AuthenticateResult.NoResult();
        }

        var authValue = authHeader.ToString();

        // Support both "Bearer sk_xxx" and just "sk_xxx" formats
        string? apiKey = null;

        if (authValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = authValue[7..].Trim();
        }
        else if (authValue.StartsWith("sk_", StringComparison.OrdinalIgnoreCase) ||
                 authValue.StartsWith("pk_", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = authValue.Trim();
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            return AuthenticateResult.Fail("Invalid authorization header format");
        }

        // Parse API key format to get basic info
        var keyInfo = ParseApiKey(apiKey);
        if (keyInfo == null)
        {
            return AuthenticateResult.Fail("Invalid API key format");
        }

        // Get client IP for validation and logging
        var ipAddress = Context.Connection.RemoteIpAddress?.ToString();

        // Validate the API key against stored keys
        var validationResult = await ValidateApiKeyAsync(apiKey, ipAddress);

        if (!validationResult.IsValid)
        {
            Logger.LogWarning(
                "API key authentication failed: {Error}, KeyPrefix: {KeyPrefix}, IP: {IpAddress}",
                validationResult.Error,
                apiKey[..Math.Min(apiKey.Length, 20)] + "...",
                ipAddress);

            return AuthenticateResult.Fail(validationResult.Error ?? "Invalid API key");
        }

        // Build claims from validation result
        var claims = BuildClaims(validationResult, apiKey);

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        // Record usage asynchronously (fire and forget)
        _ = RecordUsageAsync(validationResult.KeyId!.Value, validationResult.OrganizationId!.Value, ipAddress);

        return AuthenticateResult.Success(ticket);
    }

    private async Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey, string? ipAddress)
    {
        try
        {
            // Hash the API key
            var keyHash = HashApiKey(apiKey);

            // Look up the key in the global lookup grain
            var lookupGrain = _grainFactory.GetGrain<IApiKeyLookupGrain>(GrainKeys.ApiKeyLookup());
            var lookupResult = await lookupGrain.LookupAsync(keyHash);

            if (lookupResult == null)
            {
                return new ApiKeyValidationResult(
                    false, "API key not found", null, null, null, null, false, null, null, null, null, 0);
            }

            var (organizationId, keyId) = lookupResult.Value;

            // Get the API key grain and validate
            var apiKeyGrain = _grainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(organizationId, keyId));
            return await apiKeyGrain.ValidateAsync(apiKey, ipAddress);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error validating API key");
            return new ApiKeyValidationResult(
                false, "Internal error during API key validation", null, null, null, null, false, null, null, null, null, 0);
        }
    }

    private static List<Claim> BuildClaims(ApiKeyValidationResult result, string apiKey)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId!.Value.ToString()),
            new("sub", result.UserId.Value.ToString()),
            new("org_id", result.OrganizationId!.Value.ToString()),
            new("api_key_id", result.KeyId!.Value.ToString()),
            new("api_key_prefix", apiKey[..Math.Min(apiKey.Length, 20)] + "..."),
            new("key_type", result.Type == ApiKeyType.Secret ? "secret" : "publishable"),
            new("test_mode", result.IsTestMode.ToString().ToLowerInvariant()),
            new("token_type", "api_key")
        };

        // Add roles from the API key
        if (result.Roles != null && result.Roles.Count > 0)
        {
            foreach (var role in result.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }
        else
        {
            // Default roles based on key type
            if (result.Type == ApiKeyType.Secret)
            {
                claims.Add(new Claim(ClaimTypes.Role, "api_full_access"));
            }
            else
            {
                claims.Add(new Claim(ClaimTypes.Role, "api_client_access"));
            }
        }

        // Add scopes as claims
        if (result.Scopes != null && result.Scopes.Count > 0)
        {
            foreach (var scope in result.Scopes)
            {
                foreach (var action in scope.Actions)
                {
                    claims.Add(new Claim("scope", $"{scope.Resource}:{action}"));
                }
            }
        }

        // Add custom claims
        if (result.CustomClaims != null)
        {
            foreach (var (key, value) in result.CustomClaims)
            {
                claims.Add(new Claim(key, value));
            }
        }

        // Add allowed site IDs as a claim
        if (result.AllowedSiteIds != null && result.AllowedSiteIds.Count > 0)
        {
            claims.Add(new Claim("allowed_sites", string.Join(",", result.AllowedSiteIds)));
        }

        // Add rate limit info
        if (result.RateLimitPerMinute > 0)
        {
            claims.Add(new Claim("rate_limit", result.RateLimitPerMinute.ToString()));
        }

        return claims;
    }

    private async Task RecordUsageAsync(Guid keyId, Guid organizationId, string? ipAddress)
    {
        try
        {
            var apiKeyGrain = _grainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(organizationId, keyId));
            await apiKeyGrain.RecordUsageAsync(ipAddress);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error recording API key usage for key {KeyId}", keyId);
        }
    }

    private static string HashApiKey(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToBase64String(bytes);
    }

    private static ApiKeyInfo? ParseApiKey(string apiKey)
    {
        // Format: {prefix}_{mode}_{keyIdPart}_{random}
        // Prefix: sk (secret) or pk (publishable)
        // Mode: test or live
        // KeyIdPart: First 12 chars of GUID without hyphens
        // Random: Random string for uniqueness

        var parts = apiKey.Split('_');
        if (parts.Length < 4)
        {
            return null;
        }

        var prefix = parts[0];
        var mode = parts[1];
        var keyIdPart = parts[2];

        var isPublishable = prefix.Equals("pk", StringComparison.OrdinalIgnoreCase);
        var isSecret = prefix.Equals("sk", StringComparison.OrdinalIgnoreCase);

        if (!isPublishable && !isSecret)
        {
            return null;
        }

        var isTestMode = mode.Equals("test", StringComparison.OrdinalIgnoreCase);
        var isLiveMode = mode.Equals("live", StringComparison.OrdinalIgnoreCase);

        if (!isTestMode && !isLiveMode)
        {
            return null;
        }

        // Validate key ID part format (should be alphanumeric, minimum length)
        if (string.IsNullOrEmpty(keyIdPart) || keyIdPart.Length < 8)
        {
            return null;
        }

        return new ApiKeyInfo(
            KeyIdPart: keyIdPart,
            IsPublishable: isPublishable,
            IsTestMode: isTestMode);
    }

    private record ApiKeyInfo(string KeyIdPart, bool IsPublishable, bool IsTestMode);
}

public static class ApiKeyAuthExtensions
{
    public static AuthenticationBuilder AddApiKeyAuth(
        this AuthenticationBuilder builder,
        Action<ApiKeyAuthOptions>? configureOptions = null)
    {
        return builder.AddScheme<ApiKeyAuthOptions, ApiKeyAuthHandler>(
            ApiKeyAuthOptions.DefaultScheme,
            configureOptions);
    }
}
