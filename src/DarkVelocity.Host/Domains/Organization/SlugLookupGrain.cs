using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Slug lookup result containing organization information.
/// </summary>
[GenerateSerializer]
public record SlugLookupResult(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] DateTime ReservedAt);

/// <summary>
/// Slug change history entry.
/// </summary>
[GenerateSerializer]
public record SlugHistoryEntry(
    [property: Id(0)] string OldSlug,
    [property: Id(1)] string NewSlug,
    [property: Id(2)] DateTime ChangedAt,
    [property: Id(3)] Guid OrganizationId);

/// <summary>
/// Interface for global slug lookup grain.
/// Ensures unique slugs for organizations across the system.
/// </summary>
public interface ISlugLookupGrain : IGrainWithStringKey
{
    /// <summary>
    /// Checks if a slug is available for use.
    /// </summary>
    Task<bool> IsSlugAvailableAsync(string slug);

    /// <summary>
    /// Reserves a slug for an organization.
    /// </summary>
    Task<bool> ReserveSlugAsync(string slug, Guid organizationId);

    /// <summary>
    /// Releases a slug reservation.
    /// </summary>
    Task ReleaseSlugAsync(string slug, Guid organizationId);

    /// <summary>
    /// Looks up the organization ID for a given slug.
    /// </summary>
    Task<SlugLookupResult?> GetOrganizationBySlugAsync(string slug);

    /// <summary>
    /// Changes a slug from old to new for an organization.
    /// Returns true if successful, false if new slug is taken.
    /// </summary>
    Task<bool> ChangeSlugAsync(string oldSlug, string newSlug, Guid organizationId);

    /// <summary>
    /// Gets the slug change history.
    /// </summary>
    Task<IReadOnlyList<SlugHistoryEntry>> GetSlugHistoryAsync();

    /// <summary>
    /// Checks if a slug is a reserved system slug.
    /// </summary>
    Task<bool> IsSystemSlugAsync(string slug);
}

/// <summary>
/// State for slug lookup grain.
/// </summary>
[GenerateSerializer]
public sealed class SlugLookupState
{
    [Id(0)] public Dictionary<string, SlugLookupResult> Slugs { get; set; } = [];
    [Id(1)] public List<SlugHistoryEntry> History { get; set; } = [];
}

/// <summary>
/// Global grain for slug lookup and reservation.
/// Single instance (singleton) ensures consistent slug uniqueness.
/// </summary>
public class SlugLookupGrain : Grain, ISlugLookupGrain
{
    private readonly IPersistentState<SlugLookupState> _state;

    // Reserved system slugs that cannot be used by organizations
    private static readonly HashSet<string> SystemSlugs =
    [
        "admin",
        "api",
        "app",
        "auth",
        "billing",
        "blog",
        "cdn",
        "dashboard",
        "docs",
        "help",
        "home",
        "login",
        "logout",
        "oauth",
        "portal",
        "register",
        "settings",
        "signup",
        "static",
        "status",
        "support",
        "system",
        "terms",
        "privacy",
        "www",
        "root",
        "null",
        "undefined",
        "test",
        "demo"
    ];

    public SlugLookupGrain(
        [PersistentState("sluglookup", "OrleansStorage")]
        IPersistentState<SlugLookupState> state)
    {
        _state = state;
    }

    public Task<bool> IsSlugAvailableAsync(string slug)
    {
        var normalizedSlug = NormalizeSlug(slug);

        if (IsSystemSlug(normalizedSlug))
            return Task.FromResult(false);

        var isAvailable = !_state.State.Slugs.ContainsKey(normalizedSlug);
        return Task.FromResult(isAvailable);
    }

    public async Task<bool> ReserveSlugAsync(string slug, Guid organizationId)
    {
        var normalizedSlug = NormalizeSlug(slug);

        if (IsSystemSlug(normalizedSlug))
            return false;

        if (_state.State.Slugs.ContainsKey(normalizedSlug))
            return false;

        _state.State.Slugs[normalizedSlug] = new SlugLookupResult(organizationId, DateTime.UtcNow);
        await _state.WriteStateAsync();

        return true;
    }

    public async Task ReleaseSlugAsync(string slug, Guid organizationId)
    {
        var normalizedSlug = NormalizeSlug(slug);

        if (_state.State.Slugs.TryGetValue(normalizedSlug, out var entry))
        {
            if (entry.OrganizationId == organizationId)
            {
                _state.State.Slugs.Remove(normalizedSlug);
                await _state.WriteStateAsync();
            }
        }
    }

    public Task<SlugLookupResult?> GetOrganizationBySlugAsync(string slug)
    {
        var normalizedSlug = NormalizeSlug(slug);

        if (_state.State.Slugs.TryGetValue(normalizedSlug, out var result))
            return Task.FromResult<SlugLookupResult?>(result);

        return Task.FromResult<SlugLookupResult?>(null);
    }

    public async Task<bool> ChangeSlugAsync(string oldSlug, string newSlug, Guid organizationId)
    {
        var normalizedOldSlug = NormalizeSlug(oldSlug);
        var normalizedNewSlug = NormalizeSlug(newSlug);

        // Verify the old slug belongs to this organization
        if (!_state.State.Slugs.TryGetValue(normalizedOldSlug, out var oldEntry) ||
            oldEntry.OrganizationId != organizationId)
        {
            return false;
        }

        // Check if new slug is available
        if (IsSystemSlug(normalizedNewSlug) || _state.State.Slugs.ContainsKey(normalizedNewSlug))
            return false;

        var now = DateTime.UtcNow;

        // Record history
        _state.State.History.Add(new SlugHistoryEntry(
            normalizedOldSlug,
            normalizedNewSlug,
            now,
            organizationId));

        // Keep only last 1000 history entries
        if (_state.State.History.Count > 1000)
            _state.State.History.RemoveRange(0, _state.State.History.Count - 1000);

        // Remove old slug and add new one
        _state.State.Slugs.Remove(normalizedOldSlug);
        _state.State.Slugs[normalizedNewSlug] = new SlugLookupResult(organizationId, now);

        await _state.WriteStateAsync();

        return true;
    }

    public Task<IReadOnlyList<SlugHistoryEntry>> GetSlugHistoryAsync()
    {
        return Task.FromResult<IReadOnlyList<SlugHistoryEntry>>(_state.State.History);
    }

    public Task<bool> IsSystemSlugAsync(string slug)
    {
        return Task.FromResult(IsSystemSlug(NormalizeSlug(slug)));
    }

    private static string NormalizeSlug(string slug)
    {
        return slug.ToLowerInvariant().Trim();
    }

    private static bool IsSystemSlug(string normalizedSlug)
    {
        return SystemSlugs.Contains(normalizedSlug);
    }
}
