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
