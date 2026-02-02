namespace DarkVelocity.Host.Grains;

/// <summary>
/// Represents a summary entry in an index with its entity identifier.
/// </summary>
[GenerateSerializer]
public record IndexEntry<TSummary>(
    [property: Id(0)] Guid EntityId,
    [property: Id(1)] TSummary Summary,
    [property: Id(2)] DateTime RegisteredAt)
    where TSummary : notnull;

/// <summary>
/// Result of a query operation on an index.
/// </summary>
[GenerateSerializer]
public record IndexQueryResult<TSummary>(
    [property: Id(0)] IReadOnlyList<TSummary> Items,
    [property: Id(1)] int TotalCount)
    where TSummary : notnull;

/// <summary>
/// Provides in-memory indexing for queryable summaries.
///
/// This grain maintains a collection of entity summaries that can be queried
/// without activating individual entity grains. Useful for:
/// - Listing orders for a site
/// - Finding expenses in a date range
/// - Querying bookings by status
///
/// Key format: org:{orgId}:index:{indexType}:{scope}
/// Examples:
/// - org:abc:index:orders:site-123
/// - org:abc:index:expenses:2024-01
/// - org:abc:index:bookings:site-456
///
/// Usage pattern:
/// 1. Entity grain (e.g., OrderGrain) publishes summary on state changes
/// 2. API/other grains query the index for listings
/// 3. Index maintains bounded history with automatic cleanup
///
/// Note: QueryAsync with Func predicate is designed for grain-to-grain calls.
/// For external API calls, use GetAllAsync() or GetRecentAsync() and filter
/// in the calling code.
/// </summary>
/// <typeparam name="TSummary">The summary type stored in the index. Must have [GenerateSerializer].</typeparam>
public interface IIndexGrain<TSummary> : IGrainWithStringKey
    where TSummary : notnull
{
    /// <summary>
    /// Registers a new entity summary in the index.
    /// If the entity already exists, updates it instead.
    /// </summary>
    Task RegisterAsync(Guid entityId, TSummary summary);

    /// <summary>
    /// Updates an existing entity summary in the index.
    /// If the entity doesn't exist, registers it as new.
    /// </summary>
    Task UpdateAsync(Guid entityId, TSummary summary);

    /// <summary>
    /// Removes an entity from the index.
    /// No-op if the entity doesn't exist.
    /// </summary>
    Task RemoveAsync(Guid entityId);

    /// <summary>
    /// Queries the index with a predicate filter.
    ///
    /// Note: This method is designed for grain-to-grain calls within the silo.
    /// The predicate function cannot be serialized across process boundaries.
    /// For external API calls, use GetAllAsync() and filter in the caller.
    /// </summary>
    /// <param name="predicate">Filter function applied to each summary.</param>
    /// <param name="limit">Maximum number of results to return. Null for unlimited.</param>
    /// <returns>Matching summaries in reverse chronological order (newest first).</returns>
    Task<IReadOnlyList<TSummary>> QueryAsync(Func<TSummary, bool> predicate, int? limit = null);

    /// <summary>
    /// Gets the most recent entries from the index.
    /// </summary>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <returns>Recent summaries in reverse chronological order (newest first).</returns>
    Task<IReadOnlyList<TSummary>> GetRecentAsync(int limit = 50);

    /// <summary>
    /// Gets all entries from the index.
    /// Use sparingly - consider GetRecentAsync for large indexes.
    /// </summary>
    /// <returns>All entries in reverse chronological order (newest first).</returns>
    Task<IReadOnlyList<IndexEntry<TSummary>>> GetAllAsync();

    /// <summary>
    /// Gets a specific entry by entity ID.
    /// </summary>
    /// <param name="entityId">The entity identifier.</param>
    /// <returns>The summary if found, null otherwise.</returns>
    Task<TSummary?> GetByIdAsync(Guid entityId);

    /// <summary>
    /// Gets the count of entries in the index.
    /// </summary>
    Task<int> GetCountAsync();

    /// <summary>
    /// Checks if an entity exists in the index.
    /// </summary>
    Task<bool> ExistsAsync(Guid entityId);

    /// <summary>
    /// Clears all entries from the index.
    /// Use with caution - primarily for testing/maintenance.
    /// </summary>
    Task ClearAsync();
}
