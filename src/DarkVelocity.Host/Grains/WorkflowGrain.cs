using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Manages status transitions and approval audit trail for domain entities.
/// Domain grains (ExpenseGrain, BookingGrain, PurchaseDocumentGrain) compose with this grain
/// for consistent status management while keeping business rules in their own domain.
/// </summary>
public class WorkflowGrain : Grain, IWorkflowGrain
{
    private readonly IPersistentState<WorkflowState> _state;
    private IAsyncStream<IStreamEvent>? _workflowStream;

    public WorkflowGrain(
        [PersistentState("workflow", "OrleansStorage")]
        IPersistentState<WorkflowState> state)
    {
        _state = state;
    }

    private IAsyncStream<IStreamEvent> GetWorkflowStream()
    {
        if (_workflowStream == null && _state.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.WorkflowStreamNamespace, _state.State.OrganizationId.ToString());
            _workflowStream = streamProvider.GetStream<IStreamEvent>(streamId);
        }
        return _workflowStream!;
    }

    public async Task InitializeAsync(string initialStatus, List<string> allowedStatuses)
    {
        if (_state.State.IsInitialized)
            throw new InvalidOperationException("Workflow already initialized");

        if (string.IsNullOrWhiteSpace(initialStatus))
            throw new ArgumentException("Initial status is required", nameof(initialStatus));

        if (allowedStatuses == null || allowedStatuses.Count == 0)
            throw new ArgumentException("Allowed statuses list cannot be empty", nameof(allowedStatuses));

        if (!allowedStatuses.Contains(initialStatus))
            throw new ArgumentException($"Initial status '{initialStatus}' must be in the allowed statuses list", nameof(initialStatus));

        var key = this.GetPrimaryKeyString();
        var (orgId, ownerType, ownerId) = GrainKeys.ParseWorkflow(key);

        _state.State = new WorkflowState
        {
            OrganizationId = orgId,
            OwnerType = ownerType,
            OwnerId = ownerId,
            CurrentStatus = initialStatus,
            AllowedStatuses = allowedStatuses,
            Transitions = [],
            IsInitialized = true,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public Task<string> GetStatusAsync()
    {
        EnsureInitialized();
        return Task.FromResult(_state.State.CurrentStatus);
    }

    public async Task<WorkflowTransitionResult> TransitionAsync(string newStatus, Guid performedBy, string? reason)
    {
        EnsureInitialized();

        var currentStatus = _state.State.CurrentStatus;

        if (string.IsNullOrWhiteSpace(newStatus))
            return WorkflowTransitionResult.Failed(currentStatus, "New status is required");

        if (!_state.State.AllowedStatuses.Contains(newStatus))
            return WorkflowTransitionResult.Failed(currentStatus, $"Status '{newStatus}' is not in the allowed statuses list");

        if (currentStatus == newStatus)
            return WorkflowTransitionResult.Failed(currentStatus, $"Already in status '{newStatus}'");

        var transitionId = Guid.NewGuid();
        var transitionedAt = DateTime.UtcNow;

        var transition = new WorkflowTransition(
            transitionId,
            currentStatus,
            newStatus,
            performedBy,
            transitionedAt,
            reason);

        _state.State.Transitions.Add(transition);
        _state.State.CurrentStatus = newStatus;
        _state.State.LastTransitionAt = transitionedAt;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish workflow transitioned event
        if (GetWorkflowStream() != null)
        {
            await GetWorkflowStream().OnNextAsync(new WorkflowTransitionedStreamEvent(
                transitionId,
                _state.State.OwnerType,
                _state.State.OwnerId,
                currentStatus,
                newStatus,
                performedBy,
                transitionedAt,
                reason)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }

        return WorkflowTransitionResult.Succeeded(currentStatus, newStatus, transitionId, transitionedAt);
    }

    public Task<IReadOnlyList<WorkflowTransition>> GetHistoryAsync()
    {
        EnsureInitialized();
        return Task.FromResult<IReadOnlyList<WorkflowTransition>>(_state.State.Transitions);
    }

    public Task<bool> CanTransitionToAsync(string targetStatus)
    {
        if (!_state.State.IsInitialized)
            return Task.FromResult(false);

        if (string.IsNullOrWhiteSpace(targetStatus))
            return Task.FromResult(false);

        if (!_state.State.AllowedStatuses.Contains(targetStatus))
            return Task.FromResult(false);

        if (_state.State.CurrentStatus == targetStatus)
            return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public Task<WorkflowState> GetStateAsync()
    {
        EnsureInitialized();
        return Task.FromResult(_state.State);
    }

    private void EnsureInitialized()
    {
        if (!_state.State.IsInitialized)
            throw new InvalidOperationException("Workflow not initialized. Call InitializeAsync first.");
    }
}

/// <summary>
/// Stream event for workflow transitions.
/// </summary>
[GenerateSerializer]
public sealed record WorkflowTransitionedStreamEvent(
    [property: Id(0)] Guid TransitionId,
    [property: Id(1)] string OwnerType,
    [property: Id(2)] Guid OwnerId,
    [property: Id(3)] string FromStatus,
    [property: Id(4)] string ToStatus,
    [property: Id(5)] Guid PerformedBy,
    [property: Id(6)] DateTime TransitionedAt,
    [property: Id(7)] string? Reason) : StreamEvent;

/// <summary>
/// Stream event for workflow initialization.
/// </summary>
[GenerateSerializer]
public sealed record WorkflowInitializedStreamEvent(
    [property: Id(0)] string OwnerType,
    [property: Id(1)] Guid OwnerId,
    [property: Id(2)] string InitialStatus,
    [property: Id(3)] List<string> AllowedStatuses) : StreamEvent;
