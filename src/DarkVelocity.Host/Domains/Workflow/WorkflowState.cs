using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

/// <summary>
/// Persistent state for workflow status management and transition audit trail.
/// </summary>
[GenerateSerializer]
public sealed class WorkflowState
{
    /// <summary>
    /// Organization ID for multi-tenancy.
    /// </summary>
    [Id(0)] public Guid OrganizationId { get; set; }

    /// <summary>
    /// Type of the owning entity (e.g., "expense", "booking", "purchasedocument").
    /// </summary>
    [Id(1)] public string OwnerType { get; set; } = string.Empty;

    /// <summary>
    /// ID of the owning entity.
    /// </summary>
    [Id(2)] public Guid OwnerId { get; set; }

    /// <summary>
    /// Current workflow status. Uses string for flexibility - domain grains define their own status values.
    /// </summary>
    [Id(3)] public string CurrentStatus { get; set; } = string.Empty;

    /// <summary>
    /// List of all allowed statuses for this workflow instance.
    /// </summary>
    [Id(4)] public List<string> AllowedStatuses { get; set; } = [];

    /// <summary>
    /// Complete history of all status transitions with audit information.
    /// </summary>
    [Id(5)] public List<WorkflowTransition> Transitions { get; set; } = [];

    /// <summary>
    /// Whether this workflow has been initialized.
    /// </summary>
    [Id(6)] public bool IsInitialized { get; set; }

    /// <summary>
    /// Version for optimistic concurrency.
    /// </summary>
    [Id(7)] public int Version { get; set; }

    /// <summary>
    /// When the workflow was initialized.
    /// </summary>
    [Id(8)] public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the last transition occurred.
    /// </summary>
    [Id(9)] public DateTime? LastTransitionAt { get; set; }
}
