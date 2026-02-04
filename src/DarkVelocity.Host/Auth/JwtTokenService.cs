using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DarkVelocity.Host.Auth;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "https://api.darkvelocity.app";
    public string Audience { get; set; } = "darkvelocity-apps";
    public string SecretKey { get; set; } = "change-this-in-production-min-32-chars!!";
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
    public int RefreshTokenLifetimeDays { get; set; } = 30;
    public int DeviceCodeLifetimeMinutes { get; set; } = 15;
    public int DeviceCodePollingIntervalSeconds { get; set; } = 5;
}

public sealed class JwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenService(JwtSettings settings)
    {
        _settings = settings;
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
    }

    public (string AccessToken, DateTime Expires) GenerateAccessToken(
        Guid userId,
        string displayName,
        Guid organizationId,
        Guid? siteId = null,
        Guid? deviceId = null,
        IEnumerable<string>? roles = null,
        Guid? sessionId = null)
    {
        var expires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Name, displayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("org_id", organizationId.ToString()),
        };

        if (siteId.HasValue)
            claims.Add(new Claim("site_id", siteId.Value.ToString()));

        if (deviceId.HasValue)
            claims.Add(new Claim("device_id", deviceId.Value.ToString()));

        if (sessionId.HasValue)
            claims.Add(new Claim("session_id", sessionId.Value.ToString()));

        if (roles != null)
        {
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public string GenerateDeviceToken(
        Guid deviceId,
        Guid organizationId,
        Guid siteId,
        string deviceName)
    {
        var expires = DateTime.UtcNow.AddDays(90); // Device tokens are long-lived

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, deviceId.ToString()),
            new(JwtRegisteredClaimNames.Name, deviceName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("org_id", organizationId.ToString()),
            new("site_id", siteId.ToString()),
            new("device_id", deviceId.ToString()),
            new("token_type", "device"),
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _signingKey,
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }

    public TokenValidationParameters GetValidationParameters() => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = _signingKey,
        ValidateIssuer = true,
        ValidIssuer = _settings.Issuer,
        ValidateAudience = true,
        ValidAudience = _settings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(5)
    };
}
