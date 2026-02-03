using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.Runtime;
using Orleans.Streams;
using System.Security.Cryptography;
using System.Text;

namespace DarkVelocity.Host.Grains;

public class ApiKeyGrain : Grain, IApiKeyGrain
{
    private readonly IPersistentState<UserApiKeyState> _state;
    private IAsyncStream<IStreamEvent>? _stream;

    public ApiKeyGrain(
        [PersistentState("apikey", "OrleansStorage")]
        IPersistentState<UserApiKeyState> state)
    {
        _state = state;
    }

    private IAsyncStream<IStreamEvent> GetStream()
    {
        if (_stream == null && _state.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.UserStreamNamespace, _state.State.OrganizationId.ToString());
            _stream = streamProvider.GetStream<IStreamEvent>(streamId);
        }
        return _stream!;
    }

    public async Task<ApiKeyCreatedResult> CreateAsync(CreateApiKeyCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("API key already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, keyId) = GrainKeys.ParseOrgEntity(key);

        // Generate the actual API key
        var (apiKey, keyHash) = GenerateApiKey(command.Type, command.IsTestMode, keyId);
        var keyPrefix = apiKey[..Math.Min(apiKey.Length, 20)] + "...";

        _state.State = new UserApiKeyState
        {
            Id = keyId,
            OrganizationId = command.OrganizationId,
            UserId = command.UserId,
            Name = command.Name,
            Description = command.Description,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            Type = command.Type,
            IsTestMode = command.IsTestMode,
            Status = ApiKeyStatus.Active,
            Scopes = command.Scopes ?? [],
            CustomClaims = command.CustomClaims ?? [],
            Roles = command.Roles ?? [],
            AllowedSiteIds = command.AllowedSiteIds ?? [],
            AllowedIpRanges = command.AllowedIpRanges ?? [],
            RateLimitPerMinute = command.RateLimitPerMinute ?? 0,
            ExpiresAt = command.ExpiresAt,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();

        // Register with the user's API key registry
        var registryGrain = GrainFactory.GetGrain<IApiKeyRegistryGrain>(
            GrainKeys.ApiKeyRegistry(command.OrganizationId, command.UserId));
        await registryGrain.RegisterKeyAsync(keyId, keyHash);

        // Register with global lookup
        var lookupGrain = GrainFactory.GetGrain<IApiKeyLookupGrain>(GrainKeys.ApiKeyLookup());
        await lookupGrain.RegisterAsync(keyHash, command.OrganizationId, keyId);

        // Publish event
        if (GetStream() != null)
        {
            await GetStream().OnNextAsync(new ApiKeyCreatedEvent(
                keyId,
                command.UserId,
                command.Name,
                command.Type,
                command.IsTestMode,
                keyPrefix)
            {
                OrganizationId = command.OrganizationId
            });
        }

        return new ApiKeyCreatedResult(keyId, apiKey, keyPrefix, _state.State.CreatedAt);
    }

    public Task<UserApiKeyState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task UpdateAsync(UpdateApiKeyCommand command)
    {
        EnsureExists();
        EnsureNotRevoked();

        if (command.Name != null)
            _state.State.Name = command.Name;

        if (command.Description != null)
            _state.State.Description = command.Description;

        if (command.Scopes != null)
            _state.State.Scopes = command.Scopes;

        if (command.CustomClaims != null)
            _state.State.CustomClaims = command.CustomClaims;

        if (command.Roles != null)
            _state.State.Roles = command.Roles;

        if (command.AllowedSiteIds != null)
            _state.State.AllowedSiteIds = command.AllowedSiteIds;

        if (command.AllowedIpRanges != null)
            _state.State.AllowedIpRanges = command.AllowedIpRanges;

        if (command.RateLimitPerMinute.HasValue)
            _state.State.RateLimitPerMinute = command.RateLimitPerMinute.Value;

        if (command.ExpiresAt.HasValue)
            _state.State.ExpiresAt = command.ExpiresAt.Value;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish event
        if (GetStream() != null)
        {
            await GetStream().OnNextAsync(new ApiKeyUpdatedEvent(
                _state.State.Id,
                _state.State.UserId,
                _state.State.Name)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }
    }

    public async Task RevokeAsync(Guid revokedBy, string? reason)
    {
        EnsureExists();

        if (_state.State.Status == ApiKeyStatus.Revoked)
            return; // Already revoked

        _state.State.Status = ApiKeyStatus.Revoked;
        _state.State.RevocationReason = reason;
        _state.State.RevokedAt = DateTime.UtcNow;
        _state.State.RevokedBy = revokedBy;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Unregister from registry
        var registryGrain = GrainFactory.GetGrain<IApiKeyRegistryGrain>(
            GrainKeys.ApiKeyRegistry(_state.State.OrganizationId, _state.State.UserId));
        await registryGrain.UnregisterKeyAsync(_state.State.Id, _state.State.KeyHash);

        // Unregister from global lookup
        var lookupGrain = GrainFactory.GetGrain<IApiKeyLookupGrain>(GrainKeys.ApiKeyLookup());
        await lookupGrain.UnregisterAsync(_state.State.KeyHash);

        // Publish event
        if (GetStream() != null)
        {
            await GetStream().OnNextAsync(new ApiKeyRevokedEvent(
                _state.State.Id,
                _state.State.UserId,
                revokedBy,
                reason)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }
    }

    public Task<ApiKeyValidationResult> ValidateAsync(string apiKey, string? ipAddress)
    {
        if (_state.State.Id == Guid.Empty)
        {
            return Task.FromResult(new ApiKeyValidationResult(
                false, "API key not found", null, null, null, null, false, null, null, null, null, 0));
        }

        // Check if revoked
        if (_state.State.Status == ApiKeyStatus.Revoked)
        {
            return Task.FromResult(new ApiKeyValidationResult(
                false, "API key has been revoked", null, null, null, null, false, null, null, null, null, 0));
        }

        // Check if expired
        if (_state.State.ExpiresAt.HasValue && _state.State.ExpiresAt.Value < DateTime.UtcNow)
        {
            return Task.FromResult(new ApiKeyValidationResult(
                false, "API key has expired", null, null, null, null, false, null, null, null, null, 0));
        }

        // Verify the key hash
        var providedHash = HashApiKey(apiKey);
        if (providedHash != _state.State.KeyHash)
        {
            return Task.FromResult(new ApiKeyValidationResult(
                false, "Invalid API key", null, null, null, null, false, null, null, null, null, 0));
        }

        // Check IP restrictions if configured
        if (_state.State.AllowedIpRanges.Count > 0 && !string.IsNullOrEmpty(ipAddress))
        {
            if (!IsIpAllowed(ipAddress, _state.State.AllowedIpRanges))
            {
                return Task.FromResult(new ApiKeyValidationResult(
                    false, "IP address not allowed", null, null, null, null, false, null, null, null, null, 0));
            }
        }

        return Task.FromResult(new ApiKeyValidationResult(
            true,
            null,
            _state.State.Id,
            _state.State.UserId,
            _state.State.OrganizationId,
            _state.State.Type,
            _state.State.IsTestMode,
            _state.State.Scopes,
            _state.State.CustomClaims,
            _state.State.Roles,
            _state.State.AllowedSiteIds,
            _state.State.RateLimitPerMinute));
    }

    public async Task RecordUsageAsync(string? ipAddress)
    {
        EnsureExists();

        _state.State.LastUsedAt = DateTime.UtcNow;
        _state.State.LastUsedFromIp = ipAddress;
        _state.State.UsageCount++;

        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.Id != Guid.Empty);
    }

    public Task<ApiKeySummary> GetSummaryAsync()
    {
        EnsureExists();

        var scopeNames = _state.State.Scopes.Select(s => s.Resource).ToList();

        return Task.FromResult(new ApiKeySummary(
            _state.State.Id,
            _state.State.Name,
            _state.State.Description,
            _state.State.KeyPrefix,
            _state.State.Type,
            _state.State.IsTestMode,
            _state.State.Status,
            scopeNames,
            _state.State.CreatedAt,
            _state.State.ExpiresAt,
            _state.State.LastUsedAt));
    }

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("API key does not exist");
    }

    private void EnsureNotRevoked()
    {
        if (_state.State.Status == ApiKeyStatus.Revoked)
            throw new InvalidOperationException("API key has been revoked");
    }

    private static (string ApiKey, string KeyHash) GenerateApiKey(ApiKeyType type, bool isTestMode, Guid keyId)
    {
        // Format: {prefix}_{mode}_{keyId}_{random}
        var prefix = type == ApiKeyType.Secret ? "sk" : "pk";
        var mode = isTestMode ? "test" : "live";
        var keyIdPart = keyId.ToString("N")[..12]; // First 12 chars of GUID without hyphens
        var randomPart = GenerateRandomString(24);

        var apiKey = $"{prefix}_{mode}_{keyIdPart}_{randomPart}";
        var keyHash = HashApiKey(apiKey);

        return (apiKey, keyHash);
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }
        return new string(result);
    }

    private static string HashApiKey(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToBase64String(bytes);
    }

    private static bool IsIpAllowed(string ipAddress, List<string> allowedRanges)
    {
        // Simple implementation - check exact matches and basic CIDR
        foreach (var range in allowedRanges)
        {
            if (range == ipAddress)
                return true;

            // Basic CIDR support (e.g., "192.168.1.0/24")
            if (range.Contains('/'))
            {
                if (IsIpInCidr(ipAddress, range))
                    return true;
            }
        }
        return false;
    }

    private static bool IsIpInCidr(string ipAddress, string cidr)
    {
        try
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var prefixLength))
                return false;

            var networkAddress = System.Net.IPAddress.Parse(parts[0]);
            var checkAddress = System.Net.IPAddress.Parse(ipAddress);

            var networkBytes = networkAddress.GetAddressBytes();
            var checkBytes = checkAddress.GetAddressBytes();

            if (networkBytes.Length != checkBytes.Length)
                return false;

            var mask = prefixLength;
            for (int i = 0; i < networkBytes.Length && mask > 0; i++)
            {
                var byteMask = (byte)(0xFF << Math.Max(0, 8 - mask));
                if ((networkBytes[i] & byteMask) != (checkBytes[i] & byteMask))
                    return false;
                mask -= 8;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public class ApiKeyRegistryGrain : Grain, IApiKeyRegistryGrain
{
    private readonly IPersistentState<UserApiKeyRegistryState> _state;

    public ApiKeyRegistryGrain(
        [PersistentState("apikeyregistry", "OrleansStorage")]
        IPersistentState<UserApiKeyRegistryState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(Guid organizationId, Guid userId)
    {
        if (_state.State.UserId != Guid.Empty)
            return; // Already initialized

        _state.State = new UserApiKeyRegistryState
        {
            OrganizationId = organizationId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        await _state.WriteStateAsync();
    }

    public async Task RegisterKeyAsync(Guid keyId, string keyHash)
    {
        if (!_state.State.ApiKeyIds.Contains(keyId))
        {
            _state.State.ApiKeyIds.Add(keyId);
            _state.State.KeyHashIndex[keyHash] = keyId;
            _state.State.UpdatedAt = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task UnregisterKeyAsync(Guid keyId, string keyHash)
    {
        if (_state.State.ApiKeyIds.Remove(keyId))
        {
            _state.State.KeyHashIndex.Remove(keyHash);
            _state.State.UpdatedAt = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<IReadOnlyList<Guid>> GetKeyIdsAsync()
    {
        return Task.FromResult<IReadOnlyList<Guid>>(_state.State.ApiKeyIds);
    }

    public Task<Guid?> FindKeyIdByHashAsync(string keyHash)
    {
        if (_state.State.KeyHashIndex.TryGetValue(keyHash, out var keyId))
            return Task.FromResult<Guid?>(keyId);
        return Task.FromResult<Guid?>(null);
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.UserId != Guid.Empty);
    }
}

/// <summary>
/// State for the global API key lookup grain.
/// </summary>
[GenerateSerializer]
public sealed class ApiKeyLookupState
{
    [Id(0)] public Dictionary<string, (Guid OrganizationId, Guid KeyId)> KeyHashIndex { get; set; } = [];
}

public class ApiKeyLookupGrain : Grain, IApiKeyLookupGrain
{
    private readonly IPersistentState<ApiKeyLookupState> _state;

    public ApiKeyLookupGrain(
        [PersistentState("apikeylookup", "OrleansStorage")]
        IPersistentState<ApiKeyLookupState> state)
    {
        _state = state;
    }

    public async Task RegisterAsync(string keyHash, Guid organizationId, Guid keyId)
    {
        _state.State.KeyHashIndex[keyHash] = (organizationId, keyId);
        await _state.WriteStateAsync();
    }

    public async Task UnregisterAsync(string keyHash)
    {
        if (_state.State.KeyHashIndex.Remove(keyHash))
        {
            await _state.WriteStateAsync();
        }
    }

    public Task<(Guid OrganizationId, Guid KeyId)?> LookupAsync(string keyHash)
    {
        if (_state.State.KeyHashIndex.TryGetValue(keyHash, out var result))
            return Task.FromResult<(Guid, Guid)?>(result);
        return Task.FromResult<(Guid, Guid)?>(null);
    }
}

// Event definitions for API key operations
[GenerateSerializer]
public sealed record ApiKeyCreatedEvent(
    [property: Id(0)] Guid KeyId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] string Name,
    [property: Id(3)] ApiKeyType Type,
    [property: Id(4)] bool IsTestMode,
    [property: Id(5)] string KeyPrefix) : StreamEvent;

[GenerateSerializer]
public sealed record ApiKeyUpdatedEvent(
    [property: Id(0)] Guid KeyId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] string Name) : StreamEvent;

[GenerateSerializer]
public sealed record ApiKeyRevokedEvent(
    [property: Id(0)] Guid KeyId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] Guid RevokedBy,
    [property: Id(3)] string? Reason) : StreamEvent;
