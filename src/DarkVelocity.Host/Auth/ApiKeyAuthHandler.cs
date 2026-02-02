using System.Security.Claims;
using System.Text.Encodings.Web;
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

        // Parse API key format: sk_{mode}_{accountId}_{random}
        // e.g., sk_test_abc123def456_xyz789
        // or pk_live_abc123def456_xyz789
        var keyInfo = ParseApiKey(apiKey);
        if (keyInfo == null)
        {
            return AuthenticateResult.Fail("Invalid API key format");
        }

        // For now, validate format only. In production, you'd validate against stored keys.
        // TODO: Validate against IAccountGrain.ValidateApiKeyAsync()

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, keyInfo.AccountId),
            new("account_id", keyInfo.AccountId),
            new("key_type", keyInfo.IsPublishable ? "publishable" : "secret"),
            new("test_mode", keyInfo.IsTestMode.ToString().ToLowerInvariant()),
            new("api_key_id", apiKey[..Math.Min(apiKey.Length, 20)] + "...")
        };

        // Secret keys have full access
        if (!keyInfo.IsPublishable)
        {
            claims.Add(new Claim(ClaimTypes.Role, "api_full_access"));
        }
        else
        {
            // Publishable keys have limited access
            claims.Add(new Claim(ClaimTypes.Role, "api_client_access"));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private static ApiKeyInfo? ParseApiKey(string apiKey)
    {
        // Format: {prefix}_{mode}_{accountId}_{random}
        // Prefix: sk (secret) or pk (publishable)
        // Mode: test or live
        // AccountId: GUID without hyphens (32 chars)
        // Random: Random string for uniqueness

        var parts = apiKey.Split('_');
        if (parts.Length < 4)
        {
            return null;
        }

        var prefix = parts[0];
        var mode = parts[1];
        var accountId = parts[2];

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

        // Validate account ID format (should be parseable as GUID or be alphanumeric)
        if (string.IsNullOrEmpty(accountId) || accountId.Length < 8)
        {
            return null;
        }

        return new ApiKeyInfo(
            AccountId: accountId,
            IsPublishable: isPublishable,
            IsTestMode: isTestMode);
    }

    private record ApiKeyInfo(string AccountId, bool IsPublishable, bool IsTestMode);
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
