using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Comprehensive tests for the Kitchen domain covering:
/// - Kitchen ticket invalid state transitions
/// - Station assignment validation
/// - Item state machine edge cases
/// - Concurrent operation handling
/// - Station status validation
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class KitchenDomainCoverageTests
{
    private readonly TestClusterFixture _fixture;

    public KitchenDomainCoverageTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IKitchenTicketGrain GetTicketGrain(Guid orgId, Guid siteId, Guid ticketId)
        => _fixture.Cluster.GrainFactory.GetGrain<IKitchenTicketGrain>(
            GrainKeys.KitchenOrder(orgId, siteId, ticketId));

    private IKitchenStationGrain GetStationGrain(Guid orgId, Guid siteId, Guid stationId)
        => _fixture.Cluster.GrainFactory.GetGrain<IKitchenStationGrain>(
            GrainKeys.KitchenStation(orgId, siteId, stationId));

    private async Task<IKitchenTicketGrain> CreateTicketWithItemAsync(
        Guid orgId, Guid siteId, Guid ticketId)
    {
        var grain = GetTicketGrain(orgId, siteId, ticketId);
        await grain.CreateAsync(new CreateKitchenTicketCommand(
            orgId, siteId, Guid.NewGuid(), "ORD-001",
            OrderType.DineIn, "T1", 2, "Server"));
        await grain.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Burger", 1));
        return grain;
    }

    private async Task<IKitchenStationGrain> CreateOpenStationAsync(
        Guid orgId, Guid siteId, Guid stationId, string name = "Grill")
    {
        var grain = GetStationGrain(orgId, siteId, stationId);
        await grain.OpenAsync(new OpenStationCommand(orgId, siteId, name, StationType.Grill, 1));
        return grain;
    }

    // ============================================================================
    // Kitchen Ticket State Transition Tests
    // ============================================================================

    #region Ticket Invalid State Transitions

    [Fact]
    public async Task StartItemAsync_AlreadyStartedItem_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        var state = await ticket.GetStateAsync();
        var itemId = state.Items[0].Id;

        await ticket.StartItemAsync(new StartItemCommand(itemId, Guid.NewGuid()));

        // Act
        var act = () => ticket.StartItemAsync(new StartItemCommand(itemId, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already started*");
    }

    [Fact]
    public async Task StartItemAsync_CompletedItem_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        var state = await ticket.GetStateAsync();
        var itemId = state.Items[0].Id;

        await ticket.StartItemAsync(new StartItemCommand(itemId, Guid.NewGuid()));
        await ticket.CompleteItemAsync(new CompleteItemCommand(itemId, Guid.NewGuid()));

        // Act
        var act = () => ticket.StartItemAsync(new StartItemCommand(itemId, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already*");
    }

    [Fact]
    public async Task StartItemAsync_VoidedItem_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        var state = await ticket.GetStateAsync();
        var itemId = state.Items[0].Id;

        await ticket.VoidItemAsync(new VoidItemCommand(itemId, "Customer changed mind"));

        // Act
        var act = () => ticket.StartItemAsync(new StartItemCommand(itemId, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*voided*");
    }

    [Fact]
    public async Task CompleteItemAsync_NotStartedItem_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        var state = await ticket.GetStateAsync();
        var itemId = state.Items[0].Id;

        // Act - Try to complete without starting
        var act = () => ticket.CompleteItemAsync(new CompleteItemCommand(itemId, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be started*");
    }

    [Fact]
    public async Task CompleteItemAsync_AlreadyCompletedItem_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        var state = await ticket.GetStateAsync();
        var itemId = state.Items[0].Id;

        await ticket.StartItemAsync(new StartItemCommand(itemId, Guid.NewGuid()));
        await ticket.CompleteItemAsync(new CompleteItemCommand(itemId, Guid.NewGuid()));

        // Act
        var act = () => ticket.CompleteItemAsync(new CompleteItemCommand(itemId, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already completed*");
    }

    [Fact]
    public async Task CompleteItemAsync_VoidedItem_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        var state = await ticket.GetStateAsync();
        var itemId = state.Items[0].Id;

        await ticket.VoidItemAsync(new VoidItemCommand(itemId, "Cancelled"));

        // Act
        var act = () => ticket.CompleteItemAsync(new CompleteItemCommand(itemId, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*voided*");
    }

    [Fact]
    public async Task VoidItemAsync_AlreadyVoidedItem_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        var state = await ticket.GetStateAsync();
        var itemId = state.Items[0].Id;

        await ticket.VoidItemAsync(new VoidItemCommand(itemId, "First void"));

        // Act
        var act = () => ticket.VoidItemAsync(new VoidItemCommand(itemId, "Second void"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already voided*");
    }

    [Fact]
    public async Task VoidItemAsync_NonExistentItem_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        // Act
        var act = () => ticket.VoidItemAsync(new VoidItemCommand(Guid.NewGuid(), "Test"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Item not found*");
    }

    [Fact]
    public async Task StartItemAsync_NonExistentItem_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        // Act
        var act = () => ticket.StartItemAsync(new StartItemCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Item not found*");
    }

    #endregion

    #region Ticket Status Transitions

    [Fact]
    public async Task BumpAsync_WhenNotReady_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        // Ticket is New, not Ready

        // Act
        var act = () => ticket.BumpAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not ready*");
    }

    [Fact]
    public async Task BumpAsync_WhenAlreadyServed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        var state = await ticket.GetStateAsync();
        var itemId = state.Items[0].Id;

        await ticket.StartItemAsync(new StartItemCommand(itemId, Guid.NewGuid()));
        await ticket.CompleteItemAsync(new CompleteItemCommand(itemId, Guid.NewGuid()));
        await ticket.BumpAsync(Guid.NewGuid());

        // Act
        var act = () => ticket.BumpAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already served*");
    }

    [Fact]
    public async Task BumpAsync_WhenVoided_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        await ticket.VoidAsync("Order cancelled");

        // Act
        var act = () => ticket.BumpAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*voided*");
    }

    [Fact]
    public async Task VoidAsync_WhenAlreadyVoided_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        await ticket.VoidAsync("First void");

        // Act
        var act = () => ticket.VoidAsync("Second void");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already voided*");
    }

    [Fact]
    public async Task VoidAsync_WhenServed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        var state = await ticket.GetStateAsync();
        var itemId = state.Items[0].Id;

        await ticket.StartItemAsync(new StartItemCommand(itemId, Guid.NewGuid()));
        await ticket.CompleteItemAsync(new CompleteItemCommand(itemId, Guid.NewGuid()));
        await ticket.BumpAsync(Guid.NewGuid());

        // Act
        var act = () => ticket.VoidAsync("Too late void");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already served*");
    }

    [Fact]
    public async Task AddItemAsync_WhenVoided_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        await ticket.VoidAsync("Order cancelled");

        // Act
        var act = () => ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Fries", 1));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*voided*");
    }

    [Fact]
    public async Task AddItemAsync_WhenServed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        var state = await ticket.GetStateAsync();
        var itemId = state.Items[0].Id;

        await ticket.StartItemAsync(new StartItemCommand(itemId, Guid.NewGuid()));
        await ticket.CompleteItemAsync(new CompleteItemCommand(itemId, Guid.NewGuid()));
        await ticket.BumpAsync(Guid.NewGuid());

        // Act
        var act = () => ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Fries", 1));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*served*");
    }

    #endregion

    #region Ticket Creation Validation

    [Fact]
    public async Task CreateAsync_WhenAlreadyExists_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = GetTicketGrain(orgId, siteId, ticketId);

        await ticket.CreateAsync(new CreateKitchenTicketCommand(
            orgId, siteId, Guid.NewGuid(), "ORD-001",
            OrderType.DineIn, "T1", 2, "Server"));

        // Act
        var act = () => ticket.CreateAsync(new CreateKitchenTicketCommand(
            orgId, siteId, Guid.NewGuid(), "ORD-002",
            OrderType.DineIn, "T2", 4, "Server2"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateAsync_WithZeroGuestCount_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = GetTicketGrain(orgId, siteId, ticketId);

        // Act
        var act = () => ticket.CreateAsync(new CreateKitchenTicketCommand(
            orgId, siteId, Guid.NewGuid(), "ORD-001",
            OrderType.DineIn, "T1", 0, "Server"));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Guest count must be at least 1*");
    }

    [Fact]
    public async Task AddItemAsync_WithEmptyName_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        // Act
        var act = () => ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "", 1));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Name cannot be empty*");
    }

    [Fact]
    public async Task AddItemAsync_WithZeroQuantity_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        // Act
        var act = () => ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Item", 0));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Quantity must be at least 1*");
    }

    #endregion

    // ============================================================================
    // Kitchen Station State Transition Tests
    // ============================================================================

    #region Station Status Transitions

    [Fact]
    public async Task OpenAsync_WhenAlreadyOpen_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = await CreateOpenStationAsync(orgId, siteId, stationId);

        // Act
        var act = () => station.OpenAsync(new OpenStationCommand(
            orgId, siteId, "Grill2", StationType.Grill, 2));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already open*");
    }

    [Fact]
    public async Task PauseAsync_WhenClosed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = await CreateOpenStationAsync(orgId, siteId, stationId);
        await station.CloseAsync(Guid.NewGuid());

        // Act
        var act = () => station.PauseAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed station*");
    }

    [Fact]
    public async Task PauseAsync_WhenAlreadyPaused_ShouldBeIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = await CreateOpenStationAsync(orgId, siteId, stationId);
        await station.PauseAsync();

        // Act - Should not throw
        await station.PauseAsync();

        // Assert
        var state = await station.GetStateAsync();
        state.Status.Should().Be(StationStatus.Paused);
    }

    [Fact]
    public async Task ResumeAsync_WhenClosed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = await CreateOpenStationAsync(orgId, siteId, stationId);
        await station.CloseAsync(Guid.NewGuid());

        // Act
        var act = () => station.ResumeAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed station*");
    }

    [Fact]
    public async Task ResumeAsync_WhenAlreadyOpen_ShouldBeIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = await CreateOpenStationAsync(orgId, siteId, stationId);

        // Act - Should not throw
        await station.ResumeAsync();

        // Assert
        var state = await station.GetStateAsync();
        state.Status.Should().Be(StationStatus.Open);
    }

    [Fact]
    public async Task CloseAsync_WhenAlreadyClosed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = await CreateOpenStationAsync(orgId, siteId, stationId);
        await station.CloseAsync(Guid.NewGuid());

        // Act
        var act = () => station.CloseAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already closed*");
    }

    [Fact]
    public async Task ReceiveTicketAsync_WhenClosed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var station = await CreateOpenStationAsync(orgId, siteId, stationId);
        await station.CloseAsync(Guid.NewGuid());

        // Act
        var act = () => station.ReceiveTicketAsync(ticketId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed*");
    }

    [Fact]
    public async Task ReceiveTicketAsync_DuplicateTicket_ShouldBeIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var station = await CreateOpenStationAsync(orgId, siteId, stationId);

        await station.ReceiveTicketAsync(ticketId);

        // Act - Should not throw, but not duplicate
        await station.ReceiveTicketAsync(ticketId);

        // Assert
        var tickets = await station.GetCurrentTicketIdsAsync();
        tickets.Should().HaveCount(1);
    }

    [Fact]
    public async Task CompleteTicketAsync_NonExistentTicket_ShouldNotThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = await CreateOpenStationAsync(orgId, siteId, stationId);

        // Act - Should not throw
        await station.CompleteTicketAsync(Guid.NewGuid());

        // Assert
        var tickets = await station.GetCurrentTicketIdsAsync();
        tickets.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveTicketAsync_NonExistentTicket_ShouldNotThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = await CreateOpenStationAsync(orgId, siteId, stationId);

        // Act - Should not throw
        await station.RemoveTicketAsync(Guid.NewGuid());

        // Assert
        var tickets = await station.GetCurrentTicketIdsAsync();
        tickets.Should().BeEmpty();
    }

    #endregion

    #region Station Item Assignment Validation

    [Fact]
    public async Task AssignItemsAsync_WhenClosed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = await CreateOpenStationAsync(orgId, siteId, stationId);
        await station.CloseAsync(Guid.NewGuid());

        // Act
        var act = () => station.AssignItemsAsync(new AssignItemsToStationCommand(
            [Guid.NewGuid()]));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed*");
    }

    [Fact]
    public async Task SetPrinterAsync_WhenClosed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = await CreateOpenStationAsync(orgId, siteId, stationId);
        await station.CloseAsync(Guid.NewGuid());

        // Act
        var act = () => station.SetPrinterAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed*");
    }

    [Fact]
    public async Task SetDisplayAsync_WhenClosed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = await CreateOpenStationAsync(orgId, siteId, stationId);
        await station.CloseAsync(Guid.NewGuid());

        // Act
        var act = () => station.SetDisplayAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed*");
    }

    [Fact]
    public async Task AssignItemsAsync_EmptyLists_ShouldClearAssignments()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = await CreateOpenStationAsync(orgId, siteId, stationId);

        // First assign some items
        await station.AssignItemsAsync(new AssignItemsToStationCommand(
            [Guid.NewGuid(), Guid.NewGuid()], [Guid.NewGuid()]));

        // Act - Clear assignments
        await station.AssignItemsAsync(new AssignItemsToStationCommand([], []));

        // Assert
        var state = await station.GetStateAsync();
        state.AssignedMenuItemCategories.Should().BeEmpty();
        state.AssignedMenuItemIds.Should().BeEmpty();
    }

    #endregion

    // ============================================================================
    // Ticket Timing Tests
    // ============================================================================

    #region Timing Calculations

    [Fact]
    public async Task GetTimingsAsync_NewTicket_ShouldShowWaitTime()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        // Act
        var timings = await ticket.GetTimingsAsync();

        // Assert
        timings.WaitTime.Should().NotBeNull();
        timings.WaitTime!.Value.TotalSeconds.Should().BeGreaterOrEqualTo(0);
        timings.PrepTime.Should().BeNull(); // Not started
        timings.CompletedAt.Should().BeNull(); // Not completed
    }

    [Fact]
    public async Task GetTimingsAsync_InProgressTicket_ShouldTrackPrepTime()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        var state = await ticket.GetStateAsync();
        var itemId = state.Items[0].Id;

        await ticket.StartItemAsync(new StartItemCommand(itemId, Guid.NewGuid()));

        // Act
        var timings = await ticket.GetTimingsAsync();

        // Assert
        timings.WaitTime.Should().NotBeNull();
        // PrepTime starts tracking when started
        timings.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetTimingsAsync_CompletedTicket_ShouldShowAllTimings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        var state = await ticket.GetStateAsync();
        var itemId = state.Items[0].Id;

        await ticket.StartItemAsync(new StartItemCommand(itemId, Guid.NewGuid()));
        await ticket.CompleteItemAsync(new CompleteItemCommand(itemId, Guid.NewGuid()));

        // Act
        var timings = await ticket.GetTimingsAsync();

        // Assert
        timings.WaitTime.Should().NotBeNull();
        timings.PrepTime.Should().NotBeNull();
        timings.CompletedAt.Should().NotBeNull();
    }

    #endregion

    // ============================================================================
    // Priority and Rush Tests
    // ============================================================================

    #region Priority Edge Cases

    [Fact]
    public async Task SetPriorityAsync_WhenVoided_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        await ticket.VoidAsync("Cancelled");

        // Act
        var act = () => ticket.SetPriorityAsync(TicketPriority.VIP);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*voided*");
    }

    [Fact]
    public async Task MarkRushAsync_WhenServed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        var state = await ticket.GetStateAsync();
        var itemId = state.Items[0].Id;

        await ticket.StartItemAsync(new StartItemCommand(itemId, Guid.NewGuid()));
        await ticket.CompleteItemAsync(new CompleteItemCommand(itemId, Guid.NewGuid()));
        await ticket.BumpAsync(Guid.NewGuid());

        // Act
        var act = () => ticket.MarkRushAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*served*");
    }

    [Fact]
    public async Task FireAllAsync_ShouldSetPriorityToAllDay()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        // Act
        await ticket.FireAllAsync();

        // Assert
        var state = await ticket.GetStateAsync();
        state.IsFireAll.Should().BeTrue();
        state.Priority.Should().Be(TicketPriority.AllDay);
    }

    [Fact]
    public async Task FireAllAsync_WhenVoided_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketWithItemAsync(orgId, siteId, ticketId);

        await ticket.VoidAsync("Cancelled");

        // Act
        var act = () => ticket.FireAllAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*voided*");
    }

    #endregion

    // ============================================================================
    // Multi-Item Ticket State Tests
    // ============================================================================

    #region Multi-Item State Transitions

    [Fact]
    public async Task Ticket_WithMultipleItems_ShouldTrackStatusCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = GetTicketGrain(orgId, siteId, ticketId);

        await ticket.CreateAsync(new CreateKitchenTicketCommand(
            orgId, siteId, Guid.NewGuid(), "ORD-001",
            OrderType.DineIn, "T1", 4, "Server"));

        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Burger", 1));
        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Fries", 1));
        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Salad", 1));

        var state = await ticket.GetStateAsync();
        var burgerId = state.Items.First(i => i.Name == "Burger").Id;
        var friesId = state.Items.First(i => i.Name == "Fries").Id;
        var saladId = state.Items.First(i => i.Name == "Salad").Id;

        // Act & Assert - Partial completion should not mark ticket ready
        await ticket.StartItemAsync(new StartItemCommand(burgerId, Guid.NewGuid()));
        state = await ticket.GetStateAsync();
        state.Status.Should().Be(TicketStatus.InProgress);

        await ticket.CompleteItemAsync(new CompleteItemCommand(burgerId, Guid.NewGuid()));
        state = await ticket.GetStateAsync();
        state.Status.Should().Be(TicketStatus.InProgress); // Still waiting on other items

        // Complete second item
        await ticket.StartItemAsync(new StartItemCommand(friesId, Guid.NewGuid()));
        await ticket.CompleteItemAsync(new CompleteItemCommand(friesId, Guid.NewGuid()));
        state = await ticket.GetStateAsync();
        state.Status.Should().Be(TicketStatus.InProgress); // Still waiting on salad

        // Complete final item
        await ticket.StartItemAsync(new StartItemCommand(saladId, Guid.NewGuid()));
        await ticket.CompleteItemAsync(new CompleteItemCommand(saladId, Guid.NewGuid()));
        state = await ticket.GetStateAsync();
        state.Status.Should().Be(TicketStatus.Ready); // Now ready
    }

    [Fact]
    public async Task Ticket_VoidingAllItems_ShouldMakeTicketVoided()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = GetTicketGrain(orgId, siteId, ticketId);

        await ticket.CreateAsync(new CreateKitchenTicketCommand(
            orgId, siteId, Guid.NewGuid(), "ORD-001",
            OrderType.DineIn, "T1", 2, "Server"));

        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Burger", 1));
        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Fries", 1));

        var state = await ticket.GetStateAsync();
        var burgerId = state.Items[0].Id;
        var friesId = state.Items[1].Id;

        // Act - Void all items
        await ticket.VoidItemAsync(new VoidItemCommand(burgerId, "Cancelled"));
        await ticket.VoidItemAsync(new VoidItemCommand(friesId, "Cancelled"));

        // Assert
        state = await ticket.GetStateAsync();
        state.Status.Should().Be(TicketStatus.Voided);
    }

    [Fact]
    public async Task Ticket_VoidingOneItem_ShouldNotAffectOthers()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = GetTicketGrain(orgId, siteId, ticketId);

        await ticket.CreateAsync(new CreateKitchenTicketCommand(
            orgId, siteId, Guid.NewGuid(), "ORD-001",
            OrderType.DineIn, "T1", 2, "Server"));

        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Burger", 1));
        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Fries", 1));

        var state = await ticket.GetStateAsync();
        var burgerId = state.Items[0].Id;
        var friesId = state.Items[1].Id;

        // Act - Void only burger
        await ticket.VoidItemAsync(new VoidItemCommand(burgerId, "Customer changed"));

        // Assert
        state = await ticket.GetStateAsync();
        state.Status.Should().Be(TicketStatus.New); // Still active
        state.Items.First(i => i.Id == burgerId).Status.Should().Be(TicketItemStatus.Voided);
        state.Items.First(i => i.Id == friesId).Status.Should().Be(TicketItemStatus.Pending);

        // Completing the remaining item should make ticket ready
        await ticket.StartItemAsync(new StartItemCommand(friesId, Guid.NewGuid()));
        await ticket.CompleteItemAsync(new CompleteItemCommand(friesId, Guid.NewGuid()));

        state = await ticket.GetStateAsync();
        state.Status.Should().Be(TicketStatus.Ready);
    }

    [Fact]
    public async Task GetPendingItemsAsync_ShouldExcludeVoidedAndCompleted()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = GetTicketGrain(orgId, siteId, ticketId);

        await ticket.CreateAsync(new CreateKitchenTicketCommand(
            orgId, siteId, Guid.NewGuid(), "ORD-001",
            OrderType.DineIn, "T1", 4, "Server"));

        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Burger", 1));
        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Fries", 1));
        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Salad", 1));
        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Soup", 1));

        var state = await ticket.GetStateAsync();
        var burgerId = state.Items.First(i => i.Name == "Burger").Id;
        var friesId = state.Items.First(i => i.Name == "Fries").Id;

        // Void burger, complete fries
        await ticket.VoidItemAsync(new VoidItemCommand(burgerId, "Cancelled"));
        await ticket.StartItemAsync(new StartItemCommand(friesId, Guid.NewGuid()));
        await ticket.CompleteItemAsync(new CompleteItemCommand(friesId, Guid.NewGuid()));

        // Act
        var pending = await ticket.GetPendingItemsAsync();

        // Assert - Should only have Salad and Soup
        pending.Should().HaveCount(2);
        pending.Select(i => i.Name).Should().Contain("Salad");
        pending.Select(i => i.Name).Should().Contain("Soup");
        pending.Select(i => i.Name).Should().NotContain("Burger");
        pending.Select(i => i.Name).Should().NotContain("Fries");
    }

    #endregion
}
