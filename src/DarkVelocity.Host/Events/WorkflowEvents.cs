namespace DarkVelocity.Host.Events;

/// <summary>
/// Published when a workflow is initialized.
/// </summary>
[GenerateSerializer]
public sealed record WorkflowInitialized(
    [property: Id(0)] Guid OrgId,
    [property: Id(1)] string OwnerType,
    [property: Id(2)] Guid OwnerId,
    [property: Id(3)] string InitialStatus,
    [property: Id(4)] List<string> AllowedStatuses,
    [property: Id(5)] DateTime InitializedAt,
    [property: Id(6)] Guid? InitializedBy) : IntegrationEvent
{
    public override string EventType => "workflow.initialized";
}

/// <summary>
/// Published when a workflow status transition occurs.
/// Provides audit trail for all status changes.
/// </summary>
[GenerateSerializer]
public sealed record WorkflowTransitioned(
    [property: Id(0)] Guid OrgId,
    [property: Id(1)] string OwnerType,
    [property: Id(2)] Guid OwnerId,
    [property: Id(3)] Guid TransitionId,
    [property: Id(4)] string FromStatus,
    [property: Id(5)] string ToStatus,
    [property: Id(6)] Guid PerformedBy,
    [property: Id(7)] DateTime TransitionedAt,
    [property: Id(8)] string? Reason) : IntegrationEvent
{
    public override string EventType => "workflow.transitioned";
}
