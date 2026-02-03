using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.IdentityModel.Tokens;
using Orleans.Runtime;
using Orleans.Streams;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace DarkVelocity.Host.Grains;

public class DeviceAuthGrain : Grain, IDeviceAuthGrain
{
    private readonly IPersistentState<DeviceAuthState> _state;
    private readonly IGrainFactory _grainFactory;
    private readonly IConfiguration _configuration;

    private const int DeviceCodeExpirationMinutes = 15;
    private const int PollingIntervalSeconds = 5;
    private const int MaxPollCount = 180; // 15 minutes at 5 second intervals
    private const string VerificationBaseUri = "https://app.darkvelocity.io/device";

    public DeviceAuthGrain(
        [PersistentState("deviceauth", "OrleansStorage")]
        IPersistentState<DeviceAuthState> state,
        IGrainFactory grainFactory,
        IConfiguration configuration)
    {
        _state = state;
        _grainFactory = grainFactory;
        _configuration = configuration;
    }

    public async Task<DeviceCodeResponse> InitiateAsync(DeviceCodeRequest request)
    {
        if (_state.State.Initialized)
            throw new InvalidOperationException("Device auth flow already initiated for this code");

        var userCode = this.GetPrimaryKeyString();
        var deviceCode = GenerateDeviceCode();

        _state.State = new DeviceAuthState
        {
            UserCode = userCode,
            DeviceCode = deviceCode,
            ClientId = request.ClientId,
            Scope = request.Scope,
            DeviceFingerprint = request.DeviceFingerprint,
            IpAddress = request.IpAddress,
            Status = DeviceAuthStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(DeviceCodeExpirationMinutes),
            PollCount = 0,
            Initialized = true
        };

        await _state.WriteStateAsync();

        // Schedule expiration
        RegisterTimer(
            ExpireIfPendingAsync,
            null,
            TimeSpan.FromMinutes(DeviceCodeExpirationMinutes),
            TimeSpan.FromMilliseconds(-1)); // One-shot timer

        return new DeviceCodeResponse(
            deviceCode,
            FormatUserCode(userCode),
            VerificationBaseUri,
            $"{VerificationBaseUri}?code={userCode}",
            DeviceCodeExpirationMinutes * 60,
            PollingIntervalSeconds);
    }

    public Task<DeviceAuthStatus> GetStatusAsync()
    {
        if (!_state.State.Initialized)
            return Task.FromResult(DeviceAuthStatus.Expired);

        // Check expiration
        if (DateTime.UtcNow > _state.State.ExpiresAt && _state.State.Status == DeviceAuthStatus.Pending)
        {
            _state.State.Status = DeviceAuthStatus.Expired;
        }

        return Task.FromResult(_state.State.Status);
    }

    public async Task<DeviceTokenResponse?> GetTokenAsync(string deviceCode)
    {
        if (!_state.State.Initialized)
            return null;

        // Verify device code matches
        if (_state.State.DeviceCode != deviceCode)
            return null;

        // Increment poll count (brute force protection)
        _state.State.PollCount++;
        if (_state.State.PollCount > MaxPollCount)
        {
            _state.State.Status = DeviceAuthStatus.Expired;
            await _state.WriteStateAsync();
            return null;
        }

        // Check if authorized
        if (_state.State.Status != DeviceAuthStatus.Authorized)
        {
            await _state.WriteStateAsync();
            return null;
        }

        // Generate tokens for the device
        var deviceId = _state.State.DeviceId!.Value;
        var orgId = _state.State.OrganizationId!.Value;
        var siteId = _state.State.SiteId!.Value;

        var accessToken = GenerateDeviceAccessToken(deviceId, orgId, siteId);
        var refreshToken = GenerateRefreshToken();

        // Register the device
        var deviceGrain = _grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, deviceId));
        await deviceGrain.RegisterAsync(new RegisterDeviceCommand(
            orgId,
            siteId,
            _state.State.DeviceName!,
            _state.State.AppType!.Value,
            _state.State.DeviceFingerprint,
            _state.State.AuthorizedBy!.Value));

        return new DeviceTokenResponse(
            accessToken,
            refreshToken,
            deviceId,
            orgId,
            siteId,
            3600 * 24 * 90); // 90 days for device token
    }

    public async Task AuthorizeAsync(AuthorizeDeviceCommand command)
    {
        if (!_state.State.Initialized)
            throw new InvalidOperationException("Device auth flow not initiated");

        if (_state.State.Status != DeviceAuthStatus.Pending)
            throw new InvalidOperationException($"Cannot authorize: current status is {_state.State.Status}");

        if (DateTime.UtcNow > _state.State.ExpiresAt)
        {
            _state.State.Status = DeviceAuthStatus.Expired;
            await _state.WriteStateAsync();
            throw new InvalidOperationException("Device code has expired");
        }

        _state.State.Status = DeviceAuthStatus.Authorized;
        _state.State.AuthorizedBy = command.AuthorizedBy;
        _state.State.OrganizationId = command.OrganizationId;
        _state.State.SiteId = command.SiteId;
        _state.State.DeviceName = command.DeviceName;
        _state.State.AppType = command.AppType;
        _state.State.DeviceId = Guid.NewGuid();

        await _state.WriteStateAsync();
    }

    public async Task DenyAsync(string reason)
    {
        if (!_state.State.Initialized)
            throw new InvalidOperationException("Device auth flow not initiated");

        if (_state.State.Status != DeviceAuthStatus.Pending)
            throw new InvalidOperationException($"Cannot deny: current status is {_state.State.Status}");

        _state.State.Status = DeviceAuthStatus.Denied;
        _state.State.DenialReason = reason;

        await _state.WriteStateAsync();
    }

    public Task<bool> IsExpiredAsync()
    {
        if (!_state.State.Initialized)
            return Task.FromResult(true);

        return Task.FromResult(DateTime.UtcNow > _state.State.ExpiresAt);
    }

    private async Task ExpireIfPendingAsync(object? state)
    {
        if (_state.State.Status == DeviceAuthStatus.Pending)
        {
            _state.State.Status = DeviceAuthStatus.Expired;
            await _state.WriteStateAsync();
        }
    }

    private static string GenerateDeviceCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string FormatUserCode(string code)
    {
        // Format as XXXX-XXXX for display
        if (code.Length >= 8)
            return $"{code[..4]}-{code.Substring(4, 4)}".ToUpperInvariant();
        return code.ToUpperInvariant();
    }

    private string GenerateDeviceAccessToken(Guid deviceId, Guid orgId, Guid siteId)
    {
        var key = _configuration["Auth:Jwt:Key"] ?? "dev-signing-key-min-32-characters!!";
        var issuer = _configuration["Auth:Jwt:Issuer"] ?? "https://api.darkvelocity.io";
        var audience = _configuration["Auth:Jwt:Audience"] ?? "darkvelocity-apps";

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("device_id", deviceId.ToString()),
            new Claim("org_id", orgId.ToString()),
            new Claim("site_id", siteId.ToString()),
            new Claim("token_type", "device"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(90),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}

public class DeviceGrain : Grain, IDeviceGrain
{
    private readonly IPersistentState<DeviceState> _state;
    private IAsyncStream<IStreamEvent>? _deviceStream;

    public DeviceGrain(
        [PersistentState("device", "OrleansStorage")]
        IPersistentState<DeviceState> state)
    {
        _state = state;
    }

    private IAsyncStream<IStreamEvent> GetDeviceStream()
    {
        if (_deviceStream == null && _state.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.DeviceStreamNamespace, _state.State.OrganizationId.ToString());
            _deviceStream = streamProvider.GetStream<IStreamEvent>(streamId);
        }
        return _deviceStream!;
    }

    public async Task<DeviceSnapshot> RegisterAsync(RegisterDeviceCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Device already registered");

        var key = this.GetPrimaryKeyString();
        var (_, _, deviceId) = GrainKeys.ParseOrgEntity(key);

        _state.State = new DeviceState
        {
            Id = deviceId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            Name = command.Name,
            Type = command.Type,
            Status = DeviceStatus.Authorized,
            Fingerprint = command.Fingerprint,
            AuthorizedBy = command.AuthorizedBy,
            AuthorizedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();

        return CreateSnapshot();
    }

    public Task<DeviceSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(CreateSnapshot());
    }

    public async Task SuspendAsync(string reason)
    {
        EnsureExists();

        _state.State.Status = DeviceStatus.Suspended;
        _state.State.SuspensionReason = reason;
        _state.State.CurrentUserId = null;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RevokeAsync(string reason)
    {
        EnsureExists();

        _state.State.Status = DeviceStatus.Revoked;
        _state.State.RevocationReason = reason;
        _state.State.CurrentUserId = null;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task ReactivateAsync()
    {
        EnsureExists();

        if (_state.State.Status == DeviceStatus.Revoked)
            throw new InvalidOperationException("Cannot reactivate a revoked device");

        _state.State.Status = DeviceStatus.Authorized;
        _state.State.SuspensionReason = null;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task SetCurrentUserAsync(Guid? userId)
    {
        EnsureExists();

        _state.State.CurrentUserId = userId;
        await _state.WriteStateAsync();
    }

    public async Task RecordHeartbeatAsync(string? appVersion)
    {
        EnsureExists();

        _state.State.LastSeenAt = DateTime.UtcNow;
        if (appVersion != null)
            _state.State.LastAppVersion = appVersion;

        await _state.WriteStateAsync();
    }

    public Task<bool> IsAuthorizedAsync()
    {
        return Task.FromResult(
            _state.State.Id != Guid.Empty &&
            _state.State.Status == DeviceStatus.Authorized);
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.Id != Guid.Empty);
    }

    private DeviceSnapshot CreateSnapshot() => new(
        _state.State.Id,
        _state.State.OrganizationId,
        _state.State.SiteId,
        _state.State.Name,
        _state.State.Type,
        _state.State.Status,
        _state.State.AuthorizedAt,
        _state.State.LastSeenAt,
        _state.State.CurrentUserId);

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Device does not exist");
    }
}

public class SessionGrain : Grain, ISessionGrain
{
    private readonly IPersistentState<SessionState> _state;
    private readonly IConfiguration _configuration;

    private const int AccessTokenMinutes = 60;
    private const int RefreshTokenDays = 30;

    public SessionGrain(
        [PersistentState("session", "OrleansStorage")]
        IPersistentState<SessionState> state,
        IConfiguration configuration)
    {
        _state = state;
        _configuration = configuration;
    }

    public async Task<SessionTokens> CreateAsync(CreateSessionCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Session already exists");

        var sessionId = Guid.NewGuid();
        var refreshToken = GenerateRefreshToken();
        var accessTokenExpires = DateTime.UtcNow.AddMinutes(AccessTokenMinutes);
        var refreshTokenExpires = DateTime.UtcNow.AddDays(RefreshTokenDays);

        _state.State = new SessionState
        {
            Id = sessionId,
            UserId = command.UserId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            DeviceId = command.DeviceId,
            AuthMethod = command.AuthMethod,
            IpAddress = command.IpAddress,
            UserAgent = command.UserAgent,
            RefreshTokenHash = HashToken(refreshToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = refreshTokenExpires,
            LastActivityAt = DateTime.UtcNow,
            RefreshCount = 0
        };

        await _state.WriteStateAsync();

        var accessToken = GenerateAccessToken(command.UserId, command.OrganizationId, command.SiteId, command.DeviceId);

        return new SessionTokens(accessToken, refreshToken, accessTokenExpires, refreshTokenExpires);
    }

    public async Task<RefreshResult> RefreshAsync(string refreshToken)
    {
        if (_state.State.Id == Guid.Empty)
            return new RefreshResult(false, Error: "Session not found");

        if (_state.State.IsRevoked)
            return new RefreshResult(false, Error: "Session revoked");

        if (DateTime.UtcNow > _state.State.ExpiresAt)
            return new RefreshResult(false, Error: "Session expired");

        var tokenHash = HashToken(refreshToken);
        if (tokenHash != _state.State.RefreshTokenHash)
            return new RefreshResult(false, Error: "Invalid refresh token");

        // Rotate refresh token
        var newRefreshToken = GenerateRefreshToken();
        var accessTokenExpires = DateTime.UtcNow.AddMinutes(AccessTokenMinutes);
        var refreshTokenExpires = DateTime.UtcNow.AddDays(RefreshTokenDays);

        _state.State.RefreshTokenHash = HashToken(newRefreshToken);
        _state.State.ExpiresAt = refreshTokenExpires;
        _state.State.LastActivityAt = DateTime.UtcNow;
        _state.State.RefreshCount++;

        await _state.WriteStateAsync();

        var accessToken = GenerateAccessToken(
            _state.State.UserId,
            _state.State.OrganizationId,
            _state.State.SiteId,
            _state.State.DeviceId);

        return new RefreshResult(
            true,
            new SessionTokens(accessToken, newRefreshToken, accessTokenExpires, refreshTokenExpires));
    }

    public async Task RevokeAsync()
    {
        if (_state.State.Id == Guid.Empty)
            return;

        _state.State.IsRevoked = true;
        await _state.WriteStateAsync();
    }

    public Task<bool> IsValidAsync()
    {
        return Task.FromResult(
            _state.State.Id != Guid.Empty &&
            !_state.State.IsRevoked &&
            DateTime.UtcNow <= _state.State.ExpiresAt);
    }

    public Task<SessionState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task RecordActivityAsync()
    {
        if (_state.State.Id == Guid.Empty)
            return;

        _state.State.LastActivityAt = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    private string GenerateAccessToken(Guid userId, Guid orgId, Guid? siteId, Guid? deviceId)
    {
        var key = _configuration["Auth:Jwt:Key"] ?? "dev-signing-key-min-32-characters!!";
        var issuer = _configuration["Auth:Jwt:Issuer"] ?? "https://api.darkvelocity.io";
        var audience = _configuration["Auth:Jwt:Audience"] ?? "darkvelocity-apps";

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("org_id", orgId.ToString()),
            new("token_type", "user"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (siteId.HasValue)
            claims.Add(new Claim("site_id", siteId.Value.ToString()));

        if (deviceId.HasValue)
            claims.Add(new Claim("device_id", deviceId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}

public class UserLookupGrain : Grain, IUserLookupGrain
{
    private readonly IPersistentState<UserLookupState> _state;
    private readonly IGrainFactory _grainFactory;

    public UserLookupGrain(
        [PersistentState("userlookup", "OrleansStorage")]
        IPersistentState<UserLookupState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public async Task RegisterPinAsync(Guid userId, string pinHash)
    {
        _state.State.PinToUserMap[pinHash] = userId;
        await _state.WriteStateAsync();
    }

    public async Task UnregisterPinAsync(Guid userId)
    {
        var pinHash = _state.State.PinToUserMap
            .FirstOrDefault(kv => kv.Value == userId).Key;

        if (pinHash != null)
        {
            _state.State.PinToUserMap.Remove(pinHash);
            await _state.WriteStateAsync();
        }
    }

    public async Task<UserLookupResult?> FindByPinHashAsync(string pinHash, Guid? siteId = null)
    {
        if (!_state.State.PinToUserMap.TryGetValue(pinHash, out var userId))
            return null;

        // Get org from grain key
        var key = this.GetPrimaryKeyString();
        var orgId = GrainKeys.ExtractOrgId(key);

        // Verify user exists and has site access if siteId provided
        var userGrain = _grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
        var userState = await userGrain.GetStateAsync();

        if (userState.Id == Guid.Empty)
            return null;

        if (userState.Status != UserStatus.Active)
            return null;

        if (siteId.HasValue && !userState.SiteAccess.Contains(siteId.Value))
            return null;

        return new UserLookupResult(userId, userState.DisplayName, orgId);
    }

    public async Task<IReadOnlyList<PinUserSummary>> GetUsersForSiteAsync(Guid siteId)
    {
        var key = this.GetPrimaryKeyString();
        var orgId = GrainKeys.ExtractOrgId(key);

        var results = new List<PinUserSummary>();

        // Get all users who have PINs registered
        foreach (var (_, userId) in _state.State.PinToUserMap)
        {
            var userGrain = _grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            var userState = await userGrain.GetStateAsync();

            // Skip if user doesn't exist, is inactive, or doesn't have site access
            if (userState.Id == Guid.Empty)
                continue;

            if (userState.Status != UserStatus.Active)
                continue;

            if (!userState.SiteAccess.Contains(siteId))
                continue;

            results.Add(new PinUserSummary(
                userId,
                userState.DisplayName,
                userState.FirstName,
                userState.LastName,
                null));
        }

        return results;
    }
}

[GenerateSerializer]
public sealed class UserLookupState
{
    [Id(0)] public Dictionary<string, Guid> PinToUserMap { get; set; } = new();
}
