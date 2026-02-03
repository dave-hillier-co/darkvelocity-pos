using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Provides in-memory indexing for queryable summaries.
///
/// This grain maintains a bounded collection of entity summaries that can be
/// queried without activating individual entity grains.
///
/// Key format: org:{orgId}:index:{indexType}:{scope}
///
/// Usage from entity grains:
/// <code>
/// // In OrderGrain after state change:
/// var indexKey = GrainKeys.Index(orgId, "orders", siteId.ToString());
/// var index = GrainFactory.GetGrain&lt;IIndexGrain&lt;OrderSummary&gt;&gt;(indexKey);
/// await index.RegisterAsync(orderId, new OrderSummary(...));
/// </code>
///
/// Querying:
/// <code>
/// var recentOrders = await index.GetRecentAsync(20);
/// var allEntries = await index.GetAllAsync();
/// var openOrders = allEntries.Where(e => e.Summary.Status == "Open").ToList();
/// </code>
/// </summary>
/// <typeparam name="TSummary">The summary type stored in the index. Must have [GenerateSerializer].</typeparam>
public class IndexGrain<TSummary> : Grain, IIndexGrain<TSummary>
    where TSummary : notnull
{
    private readonly IPersistentState<IndexState<TSummary>> _state;

    public IndexGrain(
        [PersistentState("index", "OrleansStorage")]
        IPersistentState<IndexState<TSummary>> state)
    {
        _state = state;
    }

    /// <inheritdoc />
    public async Task RegisterAsync(Guid entityId, TSummary summary)
    {
        var now = DateTime.UtcNow;

        if (_state.State.CreatedAt == default)
        {
            InitializeState();
        }

        var entry = new IndexEntry<TSummary>(entityId, summary, now);
        _state.State.Entries[entityId] = entry;
        _state.State.ModifiedAt = now;
        _state.State.Version++;

        TrimIfNeeded();

        await _state.WriteStateAsync();
    }

    /// <inheritdoc />
    public Task UpdateAsync(Guid entityId, TSummary summary)
    {
        // Update is semantically the same as Register - upsert behavior
        return RegisterAsync(entityId, summary);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(Guid entityId)
    {
        if (_state.State.Entries.Remove(entityId))
        {
            _state.State.ModifiedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TSummary>> GetRecentAsync(int limit = 50)
    {
        var recent = _state.State.Entries.Values
            .OrderByDescending(e => e.RegisteredAt)
            .Take(limit)
            .Select(e => e.Summary)
            .ToList();

        return Task.FromResult<IReadOnlyList<TSummary>>(recent);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IndexEntry<TSummary>>> GetAllAsync()
    {
        var all = _state.State.Entries.Values
            .OrderByDescending(e => e.RegisteredAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<IndexEntry<TSummary>>>(all);
    }

    /// <inheritdoc />
    public Task<TSummary?> GetByIdAsync(Guid entityId)
    {
        if (_state.State.Entries.TryGetValue(entityId, out var entry))
        {
            return Task.FromResult<TSummary?>(entry.Summary);
        }
        return Task.FromResult<TSummary?>(default);
    }

    /// <inheritdoc />
    public Task<int> GetCountAsync()
    {
        return Task.FromResult(_state.State.Entries.Count);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid entityId)
    {
        return Task.FromResult(_state.State.Entries.ContainsKey(entityId));
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        _state.State.Entries.Clear();
        _state.State.ModifiedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// Initializes state with metadata from the grain key.
    /// </summary>
    private void InitializeState()
    {
        var now = DateTime.UtcNow;
        _state.State.CreatedAt = now;
        _state.State.ModifiedAt = now;

        // Parse key: org:{orgId}:index:{indexType}:{scope}
        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');

        if (parts.Length >= 5 && parts[0] == "org" && parts[2] == "index")
        {
            if (Guid.TryParse(parts[1], out var orgId))
            {
                _state.State.OrganizationId = orgId;
            }
            _state.State.IndexType = parts[3];
            _state.State.Scope = string.Join(":", parts[4..]);
        }
    }

    /// <summary>
    /// Removes oldest entries if the index exceeds the maximum size.
    /// </summary>
    private void TrimIfNeeded()
    {
        var maxEntries = _state.State.MaxEntries;
        if (_state.State.Entries.Count <= maxEntries)
        {
            return;
        }

        // Remove oldest entries
        var entriesToRemove = _state.State.Entries
            .OrderBy(kvp => kvp.Value.RegisteredAt)
            .Take(_state.State.Entries.Count - maxEntries)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in entriesToRemove)
        {
            _state.State.Entries.Remove(key);
        }
    }
}
