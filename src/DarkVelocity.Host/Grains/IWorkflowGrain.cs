namespace DarkVelocity.Host.Grains;

/// <summary>
/// Manages status transitions and approval audit trail for domain entities.
/// Key format: org:{orgId}:workflow:{ownerType}:{ownerId}
/// Domain grains (ExpenseGrain, BookingGrain, etc.) compose with this grain
/// for consistent status management while keeping business rules in their domain.
/// </summary>
public interface IWorkflowGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initialize the workflow with an initial status and allowed statuses.
    /// </summary>
    Task InitializeAsync(string initialStatus, List<string> allowedStatuses);

    /// <summary>
    /// Get the current workflow status.
    /// </summary>
    Task<string> GetStatusAsync();

    /// <summary>
    /// Transition to a new status with audit trail.
    /// Domain grains should validate transitions before calling this method.
    /// </summary>
    Task<WorkflowTransitionResult> TransitionAsync(string newStatus, Guid performedBy, string? reason);

    /// <summary>
    /// Get the full transition history for audit purposes.
    /// </summary>
    Task<IReadOnlyList<WorkflowTransition>> GetHistoryAsync();

    /// <summary>
    /// Check if a transition to the target status is valid.
    /// </summary>
    Task<bool> CanTransitionToAsync(string targetStatus);

    /// <summary>
    /// Get the complete workflow state.
    /// </summary>
    Task<WorkflowState> GetStateAsync();
}

[GenerateSerializer]
public record WorkflowTransition(
    [property: Id(0)] Guid Id,
    [property: Id(1)] string FromStatus,
    [property: Id(2)] string ToStatus,
    [property: Id(3)] Guid PerformedBy,
    [property: Id(4)] DateTime PerformedAt,
    [property: Id(5)] string? Reason);

[GenerateSerializer]
public record WorkflowTransitionResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? ErrorMessage,
    [property: Id(2)] string PreviousStatus,
    [property: Id(3)] string CurrentStatus,
    [property: Id(4)] Guid? TransitionId,
    [property: Id(5)] DateTime? TransitionedAt)
{
    public static WorkflowTransitionResult Succeeded(
        string previousStatus,
        string newStatus,
        Guid transitionId,
        DateTime transitionedAt) =>
        new(true, null, previousStatus, newStatus, transitionId, transitionedAt);

    public static WorkflowTransitionResult Failed(string currentStatus, string errorMessage) =>
        new(false, errorMessage, currentStatus, currentStatus, null, null);
}
