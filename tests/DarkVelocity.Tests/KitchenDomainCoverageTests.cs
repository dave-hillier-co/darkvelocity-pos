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

    // Given: A kitchen ticket with an item already being prepared
    // When: The same item is started again
    // Then: The start is rejected because the item is already started
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

    // Given: A kitchen ticket with an item that has already been completed
    // When: The completed item is started again
    // Then: The start is rejected because the item is already finished
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

    // Given: A kitchen ticket with a voided item
    // When: The voided item is started
    // Then: The start is rejected because voided items cannot be prepared
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

    // Given: A kitchen ticket with a pending item that has not been started
    // When: The item is completed without being started first
    // Then: The completion is rejected because items must be started before completing
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

    // Given: A kitchen ticket with an item that has already been completed
    // When: The item is completed again
    // Then: The completion is rejected because the item is already completed
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

    // Given: A kitchen ticket with a voided item
    // When: The voided item is completed
    // Then: The completion is rejected because voided items cannot be completed
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

    // Given: A kitchen ticket with an already-voided item
    // When: The item is voided again
    // Then: The void is rejected because the item is already voided
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

    // Given: A kitchen ticket with one item
    // When: A void is attempted on a non-existent item ID
    // Then: The void is rejected because the item was not found
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

    // Given: A kitchen ticket with one item
    // When: A start is attempted on a non-existent item ID
    // Then: The start is rejected because the item was not found
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

    // Given: A new kitchen ticket that has not been completed
    // When: The ticket is bumped before items are ready
    // Then: The bump is rejected because the ticket is not ready for service
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

    // Given: A kitchen ticket that has already been bumped and served
    // When: The ticket is bumped again
    // Then: The bump is rejected because the ticket is already served
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

    // Given: A voided kitchen ticket
    // When: The ticket is bumped
    // Then: The bump is rejected because voided tickets cannot be served
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

    // Given: An already-voided kitchen ticket
    // When: The ticket is voided again
    // Then: The void is rejected because the ticket is already voided
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

    // Given: A kitchen ticket that has been served to the guest
    // When: The ticket is voided after service
    // Then: The void is rejected because served tickets cannot be voided
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

    // Given: A voided kitchen ticket
    // When: A new item is added to the voided ticket
    // Then: The addition is rejected because voided tickets cannot accept new items
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

    // Given: A kitchen ticket that has been served
    // When: A new item is added to the served ticket
    // Then: The addition is rejected because served tickets cannot accept new items
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

    // Given: An existing kitchen ticket
    // When: A second ticket is created with the same grain identity
    // Then: The creation is rejected because the ticket already exists
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

    // Given: No existing kitchen ticket
    // When: A ticket is created with a guest count of zero
    // Then: The creation is rejected because guest count must be at least 1
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

    // Given: An existing kitchen ticket
    // When: An item is added with an empty name
    // Then: The addition is rejected because item name is required
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

    // Given: An existing kitchen ticket
    // When: An item is added with zero quantity
    // Then: The addition is rejected because quantity must be at least 1
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

    // Given: An already-open kitchen station
    // When: The station is opened again
    // Then: The open is rejected because the station is already open
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

    // Given: A closed kitchen station
    // When: The station is paused
    // Then: The pause is rejected because a closed station cannot be paused
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

    // Given: An already-paused kitchen station
    // When: The station is paused again
    // Then: The operation is idempotent and the station remains paused
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

    // Given: A closed kitchen station
    // When: The station is resumed
    // Then: The resume is rejected because a closed station cannot be resumed
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

    // Given: An already-open kitchen station
    // When: The station is resumed
    // Then: The operation is idempotent and the station remains open
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

    // Given: An already-closed kitchen station
    // When: The station is closed again
    // Then: The close is rejected because the station is already closed
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

    // Given: A closed kitchen station
    // When: A ticket is routed to the closed station
    // Then: The routing is rejected because closed stations cannot receive tickets
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

    // Given: An open kitchen station that already has a specific ticket
    // When: The same ticket is routed to the station again
    // Then: The operation is idempotent and the ticket is not duplicated
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

    // Given: An open kitchen station with no active tickets
    // When: A non-existent ticket is completed
    // Then: The operation succeeds silently with no effect
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

    // Given: An open kitchen station with no active tickets
    // When: A non-existent ticket is removed
    // Then: The operation succeeds silently with no effect
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

    // Given: A closed kitchen station
    // When: Menu items are assigned to the closed station
    // Then: The assignment is rejected because closed stations cannot be configured
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

    // Given: A closed kitchen station
    // When: A printer is assigned to the closed station
    // Then: The assignment is rejected because closed stations cannot be configured
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

    // Given: A closed kitchen station
    // When: A display screen is assigned to the closed station
    // Then: The assignment is rejected because closed stations cannot be configured
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

    // Given: A kitchen station with existing category and item assignments
    // When: Empty assignment lists are submitted
    // Then: All previous assignments are cleared
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

    // Given: A newly created kitchen ticket with no items started
    // When: Ticket timings are queried
    // Then: Wait time is tracked but prep time and completion are not yet available
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

    // Given: A kitchen ticket with one item being prepared
    // When: Ticket timings are queried during preparation
    // Then: Wait time is recorded and completion has not yet occurred
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

    // Given: A kitchen ticket with all items started and completed
    // When: Ticket timings are queried after completion
    // Then: Wait time, prep time, and completion timestamp are all recorded
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

    // Given: A voided kitchen ticket
    // When: The priority is changed to VIP
    // Then: The change is rejected because voided tickets cannot be reprioritized
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

    // Given: A kitchen ticket that has been served
    // When: The ticket is marked as rush
    // Then: The rush is rejected because served tickets cannot be expedited
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

    // Given: An active kitchen ticket with normal priority
    // When: Fire-all is triggered to expedite all items simultaneously
    // Then: The ticket is flagged as fire-all with AllDay priority
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

    // Given: A voided kitchen ticket
    // When: Fire-all is triggered
    // Then: The fire-all is rejected because voided tickets cannot be expedited
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

    // Given: A kitchen ticket with burger, fries, and salad items
    // When: Items are completed one by one in sequence
    // Then: Ticket stays in-progress until all items are ready, then becomes ready
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

    // Given: A kitchen ticket with a burger and fries
    // When: All items on the ticket are voided
    // Then: The entire ticket status changes to voided
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

    // Given: A kitchen ticket with a burger and fries
    // When: Only the burger is voided and the fries are completed
    // Then: The voided item does not block the ticket from becoming ready
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

    // Given: A ticket with four items: one voided, one completed, and two still pending
    // When: Pending items are queried
    // Then: Only the two pending items (salad and soup) are returned
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
