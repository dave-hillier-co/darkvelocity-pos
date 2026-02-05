using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

/// <summary>
/// Persistent state for the cost alert index grain.
/// </summary>
[GenerateSerializer]
public sealed class CostAlertIndexState
{
    /// <summary>
    /// The organization this index belongs to.
    /// </summary>
    [Id(0)]
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The indexed alerts, keyed by alert ID.
    /// </summary>
    [Id(1)]
    public Dictionary<Guid, CostAlertSummary> Alerts { get; set; } = [];

    /// <summary>
    /// State version for optimistic concurrency.
    /// </summary>
    [Id(2)]
    public int Version { get; set; }

    /// <summary>
    /// When this index was created.
    /// </summary>
    [Id(3)]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this index was last modified.
    /// </summary>
    [Id(4)]
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Maximum number of alerts to keep in the index.
    /// Oldest acknowledged alerts are removed first when this limit is exceeded.
    /// Default: 1000.
    /// </summary>
    [Id(5)]
    public int MaxEntries { get; set; } = 1000;
}
