using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// State for OAuth identity to user ID mapping.
/// Key format: "{provider}:{externalId}" -> UserId
/// </summary>
[GenerateSerializer]
public sealed class OAuthLookupState
{
    [Id(0)] public Dictionary<string, Guid> ExternalIdToUserId { get; set; } = [];
}

/// <summary>
/// Lookup grain for mapping OAuth external identities to internal user IDs.
/// One instance per organization.
/// Key format: "{orgId}:oauthlookup"
/// </summary>
public interface IOAuthLookupGrain : IGrainWithStringKey
{
    /// <summary>
    /// Registers an external OAuth identity to a user ID.
    /// </summary>
    Task RegisterExternalIdAsync(string provider, string externalId, Guid userId);

    /// <summary>
    /// Finds a user ID by external OAuth identity.
    /// </summary>
    Task<Guid?> FindByExternalIdAsync(string provider, string externalId);

    /// <summary>
    /// Unregisters an external OAuth identity.
    /// </summary>
    Task UnregisterExternalIdAsync(string provider, string externalId);

    /// <summary>
    /// Gets all external IDs for a given user.
    /// </summary>
    Task<Dictionary<string, string>> GetExternalIdsForUserAsync(Guid userId);
}

public class OAuthLookupGrain : Grain, IOAuthLookupGrain
{
    private readonly IPersistentState<OAuthLookupState> _state;

    public OAuthLookupGrain(
        [PersistentState("oauthlookup", "OrleansStorage")]
        IPersistentState<OAuthLookupState> state)
    {
        _state = state;
    }

    private static string MakeKey(string provider, string externalId) =>
        $"{provider.ToLowerInvariant()}:{externalId}";

    public async Task RegisterExternalIdAsync(string provider, string externalId, Guid userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        var key = MakeKey(provider, externalId);

        if (_state.State.ExternalIdToUserId.TryGetValue(key, out var existingUserId))
        {
            if (existingUserId != userId)
            {
                throw new InvalidOperationException(
                    $"External identity {provider}:{externalId} is already linked to a different user");
            }
            return; // Already registered to the same user
        }

        _state.State.ExternalIdToUserId[key] = userId;
        await _state.WriteStateAsync();
    }

    public Task<Guid?> FindByExternalIdAsync(string provider, string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        var key = MakeKey(provider, externalId);
        if (_state.State.ExternalIdToUserId.TryGetValue(key, out var userId))
        {
            return Task.FromResult<Guid?>(userId);
        }
        return Task.FromResult<Guid?>(null);
    }

    public async Task UnregisterExternalIdAsync(string provider, string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        var key = MakeKey(provider, externalId);
        if (_state.State.ExternalIdToUserId.Remove(key))
        {
            await _state.WriteStateAsync();
        }
    }

    public Task<Dictionary<string, string>> GetExternalIdsForUserAsync(Guid userId)
    {
        var result = new Dictionary<string, string>();
        foreach (var kvp in _state.State.ExternalIdToUserId.Where(x => x.Value == userId))
        {
            var parts = kvp.Key.Split(':', 2);
            if (parts.Length == 2)
            {
                result[parts[0]] = parts[1]; // provider -> externalId
            }
        }
        return Task.FromResult(result);
    }
}
