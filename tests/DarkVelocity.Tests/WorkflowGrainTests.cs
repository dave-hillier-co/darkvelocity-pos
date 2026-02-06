using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class WorkflowGrainTests
{
    private readonly TestClusterFixture _fixture;

    public WorkflowGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IWorkflowGrain GetGrain(Guid orgId, string ownerType, Guid ownerId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IWorkflowGrain>(
            GrainKeys.Workflow(orgId, ownerType, ownerId));
    }

    private static List<string> DefaultAllowedStatuses =>
        new() { "Draft", "Pending", "Approved", "Rejected", "Closed" };

    // ============================================================================
    // InitializeAsync Tests
    // ============================================================================

    // Given: A new expense workflow with Draft as the initial status and a defined set of allowed statuses
    // When: The workflow is initialized
    // Then: The workflow state reflects the organization, owner, initial status, and version 1
    [Fact]
    public async Task InitializeAsync_ValidParameters_ShouldInitializeWorkflow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        // Act
        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Assert
        var state = await grain.GetStateAsync();
        state.OrganizationId.Should().Be(orgId);
        state.OwnerType.Should().Be("expense");
        state.OwnerId.Should().Be(ownerId);
        state.CurrentStatus.Should().Be("Draft");
        state.AllowedStatuses.Should().BeEquivalentTo(DefaultAllowedStatuses);
        state.IsInitialized.Should().BeTrue();
        state.Version.Should().Be(1);
        state.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        state.Transitions.Should().BeEmpty();
        state.LastTransitionAt.Should().BeNull();
    }

    // Given: A workflow that has already been initialized
    // When: Initialization is attempted again
    // Then: An error is raised indicating the workflow is already initialized
    [Fact]
    public async Task InitializeAsync_AlreadyInitialized_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Act
        var act = () => grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Workflow already initialized");
    }

    // Given: A new workflow grain
    // When: Initialization is attempted with an initial status not in the allowed statuses list
    // Then: An error is raised indicating the initial status must be in the allowed list
    [Fact]
    public async Task InitializeAsync_InitialStatusNotInAllowedList_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        // Act
        var act = () => grain.InitializeAsync("InvalidStatus", DefaultAllowedStatuses);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*must be in the allowed statuses list*");
    }

    // Given: A new workflow grain
    // When: Initialization is attempted with an empty allowed statuses list
    // Then: An error is raised indicating allowed statuses cannot be empty
    [Fact]
    public async Task InitializeAsync_EmptyAllowedStatuses_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        // Act
        var act = () => grain.InitializeAsync("Draft", new List<string>());

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    // Given: A new workflow grain
    // When: Initialization is attempted with null allowed statuses
    // Then: An error is raised indicating allowed statuses cannot be empty
    [Fact]
    public async Task InitializeAsync_NullAllowedStatuses_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        // Act
        var act = () => grain.InitializeAsync("Draft", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    // Given: A new workflow grain
    // When: Initialization is attempted with an empty string as the initial status
    // Then: An error is raised indicating the initial status is required
    [Fact]
    public async Task InitializeAsync_EmptyInitialStatus_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        // Act
        var act = () => grain.InitializeAsync("", DefaultAllowedStatuses);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Initial status is required*");
    }

    // Given: A new workflow grain
    // When: Initialization is attempted with whitespace as the initial status
    // Then: An error is raised indicating the initial status is required
    [Fact]
    public async Task InitializeAsync_WhitespaceInitialStatus_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        // Act
        var act = () => grain.InitializeAsync("   ", DefaultAllowedStatuses);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Initial status is required*");
    }

    // ============================================================================
    // TransitionAsync Tests
    // ============================================================================

    // Given: An expense workflow initialized in Draft status
    // When: The workflow is transitioned to Pending with a reason
    // Then: The transition succeeds and the current status is Pending with a recorded transition ID
    [Fact]
    public async Task TransitionAsync_ValidTransition_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Act
        var result = await grain.TransitionAsync("Pending", performedBy, "Submitted for review");

        // Assert
        result.Success.Should().BeTrue();
        result.PreviousStatus.Should().Be("Draft");
        result.CurrentStatus.Should().Be("Pending");
        result.TransitionId.Should().NotBeNull();
        result.TransitionedAt.Should().NotBeNull();
        result.ErrorMessage.Should().BeNull();

        var status = await grain.GetStatusAsync();
        status.Should().Be("Pending");
    }

    // Given: An expense workflow currently in Draft status
    // When: A transition to the same Draft status is attempted
    // Then: The transition fails with an "Already in status" message and no transition is recorded
    [Fact]
    public async Task TransitionAsync_SameStatus_ShouldReturnFailed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Act
        var result = await grain.TransitionAsync("Draft", performedBy, null);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Already in status");
        result.PreviousStatus.Should().Be("Draft");
        result.CurrentStatus.Should().Be("Draft");
        result.TransitionId.Should().BeNull();
        result.TransitionedAt.Should().BeNull();
    }

    // Given: An expense workflow initialized in Draft status with a defined set of allowed statuses
    // When: A transition to an invalid status not in the allowed list is attempted
    // Then: The transition fails and the workflow remains in Draft
    [Fact]
    public async Task TransitionAsync_StatusNotInAllowedList_ShouldReturnFailed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Act
        var result = await grain.TransitionAsync("InvalidStatus", performedBy, null);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not in the allowed statuses list");
        result.PreviousStatus.Should().Be("Draft");
        result.CurrentStatus.Should().Be("Draft");
    }

    // Given: A workflow grain that has not been initialized
    // When: A status transition is attempted
    // Then: An error is raised indicating the workflow is not initialized
    [Fact]
    public async Task TransitionAsync_NotInitialized_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        // Act
        var act = () => grain.TransitionAsync("Pending", performedBy, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    // Given: An expense workflow in Draft status
    // When: The workflow is transitioned to Approved with a performer and reason
    // Then: The transition history captures the performer, reason, timestamps, and status change
    [Fact]
    public async Task TransitionAsync_CapturesMetadata()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();
        var reason = "Budget approved by finance team";
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);
        var beforeTransition = DateTime.UtcNow;

        // Act
        var result = await grain.TransitionAsync("Approved", performedBy, reason);

        // Assert
        var history = await grain.GetHistoryAsync();
        history.Should().HaveCount(1);

        var transition = history[0];
        transition.Id.Should().NotBeEmpty();
        transition.FromStatus.Should().Be("Draft");
        transition.ToStatus.Should().Be("Approved");
        transition.PerformedBy.Should().Be(performedBy);
        transition.Reason.Should().Be(reason);
        transition.PerformedAt.Should().BeOnOrAfter(beforeTransition);
        transition.PerformedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: An expense workflow initialized in Draft status
    // When: A transition with an empty target status is attempted
    // Then: The transition fails with a message indicating the status is required
    [Fact]
    public async Task TransitionAsync_EmptyNewStatus_ShouldReturnFailed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Act
        var result = await grain.TransitionAsync("", performedBy, null);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("required");
    }

    // Given: An expense workflow initialized in Draft status
    // When: The workflow is transitioned to Pending without providing a reason
    // Then: The transition succeeds and the history records a null reason
    [Fact]
    public async Task TransitionAsync_NullReason_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Act
        var result = await grain.TransitionAsync("Pending", performedBy, null);

        // Assert
        result.Success.Should().BeTrue();

        var history = await grain.GetHistoryAsync();
        history[0].Reason.Should().BeNull();
    }

    // ============================================================================
    // GetStatusAsync Tests
    // ============================================================================

    // Given: An expense workflow initialized in Draft status
    // When: The current status is queried
    // Then: The status returned is Draft
    [Fact]
    public async Task GetStatusAsync_ReturnsCurrentStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Act
        var status = await grain.GetStatusAsync();

        // Assert
        status.Should().Be("Draft");
    }

    // Given: A workflow grain that has not been initialized
    // When: The current status is queried
    // Then: An error is raised indicating the workflow is not initialized
    [Fact]
    public async Task GetStatusAsync_NotInitialized_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        // Act
        var act = () => grain.GetStatusAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    // Given: An expense workflow that has been transitioned from Draft to Approved
    // When: The current status is queried
    // Then: The status returned is Approved
    [Fact]
    public async Task GetStatusAsync_AfterTransition_ReturnsUpdatedStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);
        await grain.TransitionAsync("Approved", performedBy, null);

        // Act
        var status = await grain.GetStatusAsync();

        // Assert
        status.Should().Be("Approved");
    }

    // ============================================================================
    // GetHistoryAsync Tests
    // ============================================================================

    // Given: An expense workflow that has undergone three transitions (Draft to Pending to Approved to Closed)
    // When: The transition history is retrieved
    // Then: All three transitions are returned in chronological order with correct from/to statuses and reasons
    [Fact]
    public async Task GetHistoryAsync_ReturnsAllTransitionsInOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);
        await grain.TransitionAsync("Pending", performedBy, "First transition");
        await grain.TransitionAsync("Approved", performedBy, "Second transition");
        await grain.TransitionAsync("Closed", performedBy, "Third transition");

        // Act
        var history = await grain.GetHistoryAsync();

        // Assert
        history.Should().HaveCount(3);
        history[0].FromStatus.Should().Be("Draft");
        history[0].ToStatus.Should().Be("Pending");
        history[0].Reason.Should().Be("First transition");

        history[1].FromStatus.Should().Be("Pending");
        history[1].ToStatus.Should().Be("Approved");
        history[1].Reason.Should().Be("Second transition");

        history[2].FromStatus.Should().Be("Approved");
        history[2].ToStatus.Should().Be("Closed");
        history[2].Reason.Should().Be("Third transition");
    }

    // Given: A workflow grain that has not been initialized
    // When: The transition history is requested
    // Then: An error is raised indicating the workflow is not initialized
    [Fact]
    public async Task GetHistoryAsync_NotInitialized_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        // Act
        var act = () => grain.GetHistoryAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    // Given: A workflow initialized in Draft status with no transitions performed
    // When: The transition history is requested
    // Then: An empty list is returned
    [Fact]
    public async Task GetHistoryAsync_NoTransitions_ReturnsEmptyList()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Act
        var history = await grain.GetHistoryAsync();

        // Assert
        history.Should().BeEmpty();
    }

    // ============================================================================
    // CanTransitionToAsync Tests
    // ============================================================================

    // Given: An expense workflow initialized in Draft with Pending as an allowed status
    // When: The workflow checks if it can transition to Pending
    // Then: The check returns true
    [Fact]
    public async Task CanTransitionToAsync_ValidTarget_ReturnsTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Act
        var canTransition = await grain.CanTransitionToAsync("Pending");

        // Assert
        canTransition.Should().BeTrue();
    }

    // Given: An expense workflow initialized in Draft status
    // When: The workflow checks if it can transition to an invalid status not in the allowed list
    // Then: The check returns false
    [Fact]
    public async Task CanTransitionToAsync_InvalidTarget_ReturnsFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Act
        var canTransition = await grain.CanTransitionToAsync("InvalidStatus");

        // Assert
        canTransition.Should().BeFalse();
    }

    // Given: An expense workflow currently in Draft status
    // When: The workflow checks if it can transition to Draft (the same status)
    // Then: The check returns false
    [Fact]
    public async Task CanTransitionToAsync_SameStatus_ReturnsFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Act
        var canTransition = await grain.CanTransitionToAsync("Draft");

        // Assert
        canTransition.Should().BeFalse();
    }

    // Given: A workflow grain that has not been initialized
    // When: The workflow checks if it can transition to Pending
    // Then: The check returns false instead of throwing
    [Fact]
    public async Task CanTransitionToAsync_NotInitialized_ReturnsFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        // Act - Note: CanTransitionToAsync returns false instead of throwing for uninitialized
        var canTransition = await grain.CanTransitionToAsync("Pending");

        // Assert
        canTransition.Should().BeFalse();
    }

    // Given: An expense workflow initialized in Draft status
    // When: The workflow checks if it can transition to an empty string target
    // Then: The check returns false
    [Fact]
    public async Task CanTransitionToAsync_EmptyTarget_ReturnsFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Act
        var canTransition = await grain.CanTransitionToAsync("");

        // Assert
        canTransition.Should().BeFalse();
    }

    // Given: An expense workflow initialized in Draft status
    // When: The workflow checks if it can transition to a whitespace-only target
    // Then: The check returns false
    [Fact]
    public async Task CanTransitionToAsync_WhitespaceTarget_ReturnsFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Act
        var canTransition = await grain.CanTransitionToAsync("   ");

        // Assert
        canTransition.Should().BeFalse();
    }

    // ============================================================================
    // GetStateAsync Tests
    // ============================================================================

    // Given: A booking workflow initialized in Pending and transitioned to Approved
    // When: The full workflow state is retrieved
    // Then: The state includes the organization, owner type, current status, one transition, and version 2
    [Fact]
    public async Task GetStateAsync_ReturnsCompleteState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, "booking", ownerId);

        await grain.InitializeAsync("Pending", DefaultAllowedStatuses);
        await grain.TransitionAsync("Approved", performedBy, "Confirmed by manager");

        // Act
        var state = await grain.GetStateAsync();

        // Assert
        state.OrganizationId.Should().Be(orgId);
        state.OwnerType.Should().Be("booking");
        state.OwnerId.Should().Be(ownerId);
        state.CurrentStatus.Should().Be("Approved");
        state.AllowedStatuses.Should().BeEquivalentTo(DefaultAllowedStatuses);
        state.Transitions.Should().HaveCount(1);
        state.IsInitialized.Should().BeTrue();
        state.Version.Should().Be(2);
        state.LastTransitionAt.Should().NotBeNull();
    }

    // Given: A workflow grain that has not been initialized
    // When: The full workflow state is requested
    // Then: An error is raised indicating the workflow is not initialized
    [Fact]
    public async Task GetStateAsync_NotInitialized_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        // Act
        var act = () => grain.GetStateAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    // ============================================================================
    // Version Tracking Tests
    // ============================================================================

    // Given: An expense workflow initialized in Draft status (version 1)
    // When: Two successive transitions are performed (Draft to Pending, Pending to Approved)
    // Then: The version increments to 2 after the first transition and 3 after the second
    [Fact]
    public async Task Version_IncrementsOnTransitions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        // Act & Assert
        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);
        var stateAfterInit = await grain.GetStateAsync();
        stateAfterInit.Version.Should().Be(1);

        await grain.TransitionAsync("Pending", performedBy, null);
        var stateAfterFirst = await grain.GetStateAsync();
        stateAfterFirst.Version.Should().Be(2);

        await grain.TransitionAsync("Approved", performedBy, null);
        var stateAfterSecond = await grain.GetStateAsync();
        stateAfterSecond.Version.Should().Be(3);
    }

    // Given: An expense workflow initialized in Draft status
    // When: An invalid transition to a disallowed status is attempted
    // Then: The version remains unchanged from the pre-transition value
    [Fact]
    public async Task Version_DoesNotIncrementOnFailedTransition()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);
        var initialState = await grain.GetStateAsync();
        var initialVersion = initialState.Version;

        // Act - attempt invalid transition
        await grain.TransitionAsync("InvalidStatus", performedBy, null);

        // Assert
        var stateAfterFailed = await grain.GetStateAsync();
        stateAfterFailed.Version.Should().Be(initialVersion);
    }

    // ============================================================================
    // LastTransitionAt Timestamp Tests
    // ============================================================================

    // Given: An expense workflow initialized in Draft with no prior transitions
    // When: The workflow is transitioned to Pending
    // Then: The last transition timestamp is set to approximately the current time
    [Fact]
    public async Task LastTransitionAt_UpdatesOnTransition()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);
        var stateBeforeTransition = await grain.GetStateAsync();
        stateBeforeTransition.LastTransitionAt.Should().BeNull();

        var beforeTransition = DateTime.UtcNow;

        // Act
        await grain.TransitionAsync("Pending", performedBy, null);

        // Assert
        var stateAfterTransition = await grain.GetStateAsync();
        stateAfterTransition.LastTransitionAt.Should().NotBeNull();
        stateAfterTransition.LastTransitionAt.Should().BeOnOrAfter(beforeTransition);
        stateAfterTransition.LastTransitionAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: An expense workflow that has already been transitioned once (Draft to Pending)
    // When: A second transition to Approved is performed
    // Then: The last transition timestamp advances beyond the first transition time
    [Fact]
    public async Task LastTransitionAt_UpdatesOnEachTransition()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);
        await grain.TransitionAsync("Pending", performedBy, null);

        var stateAfterFirst = await grain.GetStateAsync();
        var firstTransitionTime = stateAfterFirst.LastTransitionAt;

        // Small delay to ensure different timestamps
        await Task.Delay(10);

        // Act
        await grain.TransitionAsync("Approved", performedBy, null);

        // Assert
        var stateAfterSecond = await grain.GetStateAsync();
        stateAfterSecond.LastTransitionAt.Should().BeOnOrAfter(firstTransitionTime!.Value);
    }

    // ============================================================================
    // Multiple Consecutive Transitions Tests
    // ============================================================================

    // Given: A purchase document workflow initialized in Draft status with multiple staff members
    // When: Six transitions simulate a full approval lifecycle (submit, reject, correct, resubmit, approve, close)
    // Then: The complete transition path is recorded with correct performers, and the final status is Closed at version 7
    [Fact]
    public async Task MultipleTransitions_BuildsCompleteHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();
        var grain = GetGrain(orgId, "purchasedocument", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Act - simulate a typical approval workflow
        await grain.TransitionAsync("Pending", user1, "Submitted for review");
        await grain.TransitionAsync("Rejected", user2, "Missing receipts");
        await grain.TransitionAsync("Draft", user1, "Correcting submission");
        await grain.TransitionAsync("Pending", user1, "Resubmitted with receipts");
        await grain.TransitionAsync("Approved", user3, "All requirements met");
        await grain.TransitionAsync("Closed", user3, "Filed");

        // Assert
        var history = await grain.GetHistoryAsync();
        history.Should().HaveCount(6);

        // Verify the workflow path
        var statuses = history.Select(t => (t.FromStatus, t.ToStatus)).ToList();
        statuses[0].Should().Be(("Draft", "Pending"));
        statuses[1].Should().Be(("Pending", "Rejected"));
        statuses[2].Should().Be(("Rejected", "Draft"));
        statuses[3].Should().Be(("Draft", "Pending"));
        statuses[4].Should().Be(("Pending", "Approved"));
        statuses[5].Should().Be(("Approved", "Closed"));

        // Verify performer tracking
        history[0].PerformedBy.Should().Be(user1);
        history[1].PerformedBy.Should().Be(user2);
        history[4].PerformedBy.Should().Be(user3);

        // Verify final state
        var status = await grain.GetStatusAsync();
        status.Should().Be("Closed");

        var state = await grain.GetStateAsync();
        state.Version.Should().Be(7); // 1 init + 6 transitions
    }

    // ============================================================================
    // Different Owner Types Tests
    // ============================================================================

    // Given: Three workflow grains for different owner types (expense, booking, purchase document) within the same organization
    // When: Each workflow is initialized and transitioned independently
    // Then: Each grain maintains its own status and transition history without cross-contamination
    [Fact]
    public async Task WorkflowGrain_DifferentOwnerTypes_MaintainSeparateState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();

        var expenseGrain = GetGrain(orgId, "expense", ownerId);
        var bookingGrain = GetGrain(orgId, "booking", ownerId);
        var purchaseGrain = GetGrain(orgId, "purchasedocument", ownerId);

        // Act
        await expenseGrain.InitializeAsync("Draft", DefaultAllowedStatuses);
        await bookingGrain.InitializeAsync("Pending", DefaultAllowedStatuses);
        await purchaseGrain.InitializeAsync("Draft", DefaultAllowedStatuses);

        await expenseGrain.TransitionAsync("Approved", performedBy, null);
        await bookingGrain.TransitionAsync("Rejected", performedBy, null);

        // Assert - each grain maintains independent state
        (await expenseGrain.GetStatusAsync()).Should().Be("Approved");
        (await bookingGrain.GetStatusAsync()).Should().Be("Rejected");
        (await purchaseGrain.GetStatusAsync()).Should().Be("Draft");

        var expenseState = await expenseGrain.GetStateAsync();
        expenseState.OwnerType.Should().Be("expense");
        expenseState.Transitions.Should().HaveCount(1);

        var bookingState = await bookingGrain.GetStateAsync();
        bookingState.OwnerType.Should().Be("booking");
        bookingState.Transitions.Should().HaveCount(1);

        var purchaseState = await purchaseGrain.GetStateAsync();
        purchaseState.OwnerType.Should().Be("purchasedocument");
        purchaseState.Transitions.Should().BeEmpty();
    }

    // ============================================================================
    // Transition ID Uniqueness Tests
    // ============================================================================

    // Given: An expense workflow initialized in Draft status
    // When: Three successive transitions are performed (Draft to Pending to Approved to Closed)
    // Then: Each transition is assigned a unique, non-empty ID
    [Fact]
    public async Task TransitionIds_AreUnique()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, "expense", ownerId);

        await grain.InitializeAsync("Draft", DefaultAllowedStatuses);

        // Act
        var result1 = await grain.TransitionAsync("Pending", performedBy, null);
        var result2 = await grain.TransitionAsync("Approved", performedBy, null);
        var result3 = await grain.TransitionAsync("Closed", performedBy, null);

        // Assert
        var ids = new[] { result1.TransitionId, result2.TransitionId, result3.TransitionId };
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().NotContain(Guid.Empty);
    }

    // ============================================================================
    // Custom Status Values Tests
    // ============================================================================

    // Given: A workflow with custom status values including "New", "In Progress", "Under Review", "On Hold", "Completed", and "Cancelled"
    // When: The workflow progresses through five transitions ending in Completed
    // Then: All custom statuses are accepted, the final status is Completed, and all five transitions are recorded
    [Fact]
    public async Task WorkflowGrain_CustomStatusValues_WorksCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, "custom", ownerId);

        var customStatuses = new List<string>
        {
            "New",
            "In Progress",
            "Under Review",
            "On Hold",
            "Completed",
            "Cancelled"
        };

        // Act
        await grain.InitializeAsync("New", customStatuses);
        await grain.TransitionAsync("In Progress", performedBy, "Started work");
        await grain.TransitionAsync("On Hold", performedBy, "Waiting for input");
        await grain.TransitionAsync("In Progress", performedBy, "Resumed");
        await grain.TransitionAsync("Under Review", performedBy, "Ready for review");
        await grain.TransitionAsync("Completed", performedBy, "Approved");

        // Assert
        var status = await grain.GetStatusAsync();
        status.Should().Be("Completed");

        var state = await grain.GetStateAsync();
        state.AllowedStatuses.Should().BeEquivalentTo(customStatuses);
        state.Transitions.Should().HaveCount(5);
    }
}
