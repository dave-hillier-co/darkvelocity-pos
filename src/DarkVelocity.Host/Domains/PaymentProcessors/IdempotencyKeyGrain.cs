using System.Security.Cryptography;
using DarkVelocity.Host.Grains;
using Orleans.Runtime;

namespace DarkVelocity.Host.PaymentProcessors;

/// <summary>
/// Grain interface for idempotency key management.
/// Key format: "{orgId}:idempotency"
/// </summary>
public interface IIdempotencyKeyGrain : IGrainWithStringKey
{
    /// <summary>
    /// Generates a new idempotency key for an operation.
    /// </summary>
    Task<string> GenerateKeyAsync(string operation, Guid relatedEntityId, TimeSpan? ttl = null);

    /// <summary>
    /// Checks if a key exists and has not been used.
    /// Returns (exists, alreadyUsed, previousResult).
    /// </summary>
    Task<IdempotencyCheckResult> CheckKeyAsync(string key);

    /// <summary>
    /// Marks a key as used with the operation result.
    /// </summary>
    Task MarkKeyUsedAsync(string key, bool successful, string? resultHash = null);

    /// <summary>
    /// Gets the status of a specific key.
    /// </summary>
    Task<IdempotencyKeyStatus?> GetKeyStatusAsync(string key);

    /// <summary>
    /// Removes expired keys (cleanup operation).
    /// </summary>
    Task<int> CleanupExpiredKeysAsync();

    /// <summary>
    /// Validates that an operation can proceed with the given key.
    /// Returns false if the key was already used successfully.
    /// </summary>
    Task<bool> TryAcquireAsync(string key, string operation, Guid relatedEntityId, TimeSpan? ttl = null);
}

[GenerateSerializer]
public record IdempotencyCheckResult(
    [property: Id(0)] bool Exists,
    [property: Id(1)] bool AlreadyUsed,
    [property: Id(2)] bool? PreviousSuccess,
    [property: Id(3)] string? PreviousResultHash);

[GenerateSerializer]
public record IdempotencyKeyStatus(
    [property: Id(0)] string Key,
    [property: Id(1)] string Operation,
    [property: Id(2)] Guid RelatedEntityId,
    [property: Id(3)] DateTime GeneratedAt,
    [property: Id(4)] DateTime ExpiresAt,
    [property: Id(5)] bool Used,
    [property: Id(6)] DateTime? UsedAt,
    [property: Id(7)] bool? Successful,
    [property: Id(8)] string? ResultHash);

/// <summary>
/// Manages idempotency keys to prevent duplicate operations.
/// Ensures that retried operations don't execute twice.
/// </summary>
public class IdempotencyKeyGrain : Grain, IIdempotencyKeyGrain
{
    private readonly IPersistentState<IdempotencyKeyState> _state;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);
    private const int MaxKeys = 10000;

    public IdempotencyKeyGrain(
        [PersistentState("idempotencyKeys", "OrleansStorage")]
        IPersistentState<IdempotencyKeyState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        // Parse org ID from key
        var keyParts = this.GetPrimaryKeyString().Split(':');
        if (keyParts.Length >= 1 && Guid.TryParse(keyParts[0], out var orgId))
        {
            _state.State.OrgId = orgId;
        }

        // Register timer for periodic cleanup
        this.RegisterGrainTimer(
            CleanupTimerCallback,
            new object(),
            TimeSpan.FromMinutes(15),
            TimeSpan.FromHours(1));
    }

    public async Task<string> GenerateKeyAsync(string operation, Guid relatedEntityId, TimeSpan? ttl = null)
    {
        // Cleanup if we have too many keys
        if (_state.State.Keys.Count >= MaxKeys)
        {
            await CleanupExpiredKeysAsync();

            // If still too many, remove oldest
            if (_state.State.Keys.Count >= MaxKeys)
            {
                var oldestKeys = _state.State.Keys
                    .OrderBy(k => k.Value.GeneratedAt)
                    .Take(MaxKeys / 10)
                    .Select(k => k.Key)
                    .ToList();

                foreach (var oldKey in oldestKeys)
                {
                    _state.State.Keys.Remove(oldKey);
                }
            }
        }

        var key = GenerateUniqueKey(operation);
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(ttl ?? DefaultTtl);

        var record = new IdempotencyKeyRecord(
            Key: key,
            Operation: operation,
            RelatedEntityId: relatedEntityId,
            GeneratedAt: now,
            ExpiresAt: expiresAt,
            Used: false,
            UsedAt: null,
            Successful: null,
            ResultHash: null);

        _state.State.Keys[key] = record;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return key;
    }

    public Task<IdempotencyCheckResult> CheckKeyAsync(string key)
    {
        if (!_state.State.Keys.TryGetValue(key, out var record))
        {
            return Task.FromResult(new IdempotencyCheckResult(
                Exists: false,
                AlreadyUsed: false,
                PreviousSuccess: null,
                PreviousResultHash: null));
        }

        // Check if expired
        if (record.ExpiresAt < DateTime.UtcNow)
        {
            return Task.FromResult(new IdempotencyCheckResult(
                Exists: false,
                AlreadyUsed: false,
                PreviousSuccess: null,
                PreviousResultHash: null));
        }

        return Task.FromResult(new IdempotencyCheckResult(
            Exists: true,
            AlreadyUsed: record.Used,
            PreviousSuccess: record.Successful,
            PreviousResultHash: record.ResultHash));
    }

    public async Task MarkKeyUsedAsync(string key, bool successful, string? resultHash = null)
    {
        if (!_state.State.Keys.TryGetValue(key, out var record))
        {
            // Key doesn't exist, create it as already used
            record = new IdempotencyKeyRecord(
                Key: key,
                Operation: "unknown",
                RelatedEntityId: Guid.Empty,
                GeneratedAt: DateTime.UtcNow,
                ExpiresAt: DateTime.UtcNow.Add(DefaultTtl),
                Used: true,
                UsedAt: DateTime.UtcNow,
                Successful: successful,
                ResultHash: resultHash);

            _state.State.Keys[key] = record;
        }
        else
        {
            // Update existing record
            _state.State.Keys[key] = record with
            {
                Used = true,
                UsedAt = DateTime.UtcNow,
                Successful = successful,
                ResultHash = resultHash
            };
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<IdempotencyKeyStatus?> GetKeyStatusAsync(string key)
    {
        if (!_state.State.Keys.TryGetValue(key, out var record))
        {
            return Task.FromResult<IdempotencyKeyStatus?>(null);
        }

        return Task.FromResult<IdempotencyKeyStatus?>(new IdempotencyKeyStatus(
            record.Key,
            record.Operation,
            record.RelatedEntityId,
            record.GeneratedAt,
            record.ExpiresAt,
            record.Used,
            record.UsedAt,
            record.Successful,
            record.ResultHash));
    }

    public async Task<int> CleanupExpiredKeysAsync()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _state.State.Keys
            .Where(kvp => kvp.Value.ExpiresAt < now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _state.State.Keys.Remove(key);
        }

        if (expiredKeys.Count > 0)
        {
            _state.State.Version++;
            await _state.WriteStateAsync();
        }

        return expiredKeys.Count;
    }

    public async Task<bool> TryAcquireAsync(string key, string operation, Guid relatedEntityId, TimeSpan? ttl = null)
    {
        var checkResult = await CheckKeyAsync(key);

        // If key exists and was used successfully, don't allow re-execution
        if (checkResult.Exists && checkResult.AlreadyUsed && checkResult.PreviousSuccess == true)
        {
            return false;
        }

        // If key doesn't exist, create it
        if (!checkResult.Exists)
        {
            var now = DateTime.UtcNow;
            var expiresAt = now.Add(ttl ?? DefaultTtl);

            var record = new IdempotencyKeyRecord(
                Key: key,
                Operation: operation,
                RelatedEntityId: relatedEntityId,
                GeneratedAt: now,
                ExpiresAt: expiresAt,
                Used: false,
                UsedAt: null,
                Successful: null,
                ResultHash: null);

            _state.State.Keys[key] = record;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }

        return true;
    }

    private static string GenerateUniqueKey(string operation)
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        var uniquePart = Convert.ToHexString(bytes).ToLowerInvariant();
        return $"idem_{operation}_{uniquePart}";
    }

    private async Task CleanupTimerCallback(object state)
    {
        await CleanupExpiredKeysAsync();
    }
}

/// <summary>
/// Extension methods for idempotency key operations.
/// </summary>
public static class IdempotencyKeyExtensions
{
    /// <summary>
    /// Computes a hash of the result for comparison in idempotent operations.
    /// </summary>
    public static string ComputeResultHash<T>(T result)
    {
        if (result == null) return "null";

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    /// <summary>
    /// Creates a grain key for the idempotency key grain.
    /// </summary>
    public static string IdempotencyKeyGrainKey(Guid orgId) => $"{orgId}:idempotency";
}
