using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

/// <summary>
/// Persistent state for an index grain.
/// Stores entity summaries with their identifiers and timestamps.
/// </summary>
/// <typeparam name="TSummary">The summary type stored in the index.</typeparam>
[GenerateSerializer]
public sealed class IndexState<TSummary> where TSummary : notnull
{
    /// <summary>
    /// The organization this index belongs to.
    /// </summary>
    [Id(0)]
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The type of index (e.g., "orders", "expenses", "bookings").
    /// </summary>
    [Id(1)]
    public string IndexType { get; set; } = string.Empty;

    /// <summary>
    /// The scope of the index (e.g., site ID, month).
    /// </summary>
    [Id(2)]
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// The indexed entries, keyed by entity ID.
    /// </summary>
    [Id(3)]
    public Dictionary<Guid, IndexEntry<TSummary>> Entries { get; set; } = [];

    /// <summary>
    /// State version for optimistic concurrency.
    /// </summary>
    [Id(4)]
    public int Version { get; set; }

    /// <summary>
    /// When this index was created.
    /// </summary>
    [Id(5)]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this index was last modified.
    /// </summary>
    [Id(6)]
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Maximum number of entries to keep in the index.
    /// Oldest entries are removed when this limit is exceeded.
    /// Default: 10000.
    /// </summary>
    [Id(7)]
    public int MaxEntries { get; set; } = 10000;
}
