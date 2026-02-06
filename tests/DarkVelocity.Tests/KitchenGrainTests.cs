using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class KitchenTicketGrainTests
{
    private readonly TestClusterFixture _fixture;

    public KitchenTicketGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IKitchenTicketGrain> CreateTicketAsync(Guid orgId, Guid siteId, Guid ticketId, Guid orderId)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IKitchenTicketGrain>(GrainKeys.KitchenOrder(orgId, siteId, ticketId));
        await grain.CreateAsync(new CreateKitchenTicketCommand(
            orgId,
            siteId,
            orderId,
            "ORD-001",
            OrderType.DineIn,
            "T5",
            4,
            "John"));
        return grain;
    }

    // Given: No existing kitchen ticket for the given order
    // When: A kitchen ticket is created for a VIP dine-in order at table T10 with allergy notes
    // Then: The ticket is created with correct order details, VIP priority, and a KOT number
    [Fact]
    public async Task CreateAsync_ShouldCreateTicket()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IKitchenTicketGrain>(GrainKeys.KitchenOrder(orgId, siteId, ticketId));

        var command = new CreateKitchenTicketCommand(
            orgId,
            siteId,
            orderId,
            "ORD-100",
            OrderType.DineIn,
            "T10",
            6,
            "Jane",
            "Allergy: nuts",
            TicketPriority.VIP);

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Id.Should().Be(ticketId);
        result.TicketNumber.Should().StartWith("KOT-");

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(TicketStatus.New);
        state.OrderId.Should().Be(orderId);
        state.TableNumber.Should().Be("T10");
        state.GuestCount.Should().Be(6);
        state.Priority.Should().Be(TicketPriority.VIP);
        state.Notes.Should().Be("Allergy: nuts");
    }

    // Given: An existing kitchen ticket for a dine-in order
    // When: A burger with modifiers and special instructions is added to the grill station
    // Then: The item appears on the ticket with correct details and station assignment
    [Fact]
    public async Task AddItemAsync_ShouldAddItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = await CreateTicketAsync(orgId, siteId, ticketId, orderId);

        var stationId = Guid.NewGuid();

        // Act
        await grain.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Burger",
            2,
            ["No onions", "Extra cheese"],
            "Well done",
            stationId,
            "Grill"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Items.Should().HaveCount(1);
        state.Items[0].Name.Should().Be("Burger");
        state.Items[0].Quantity.Should().Be(2);
        state.Items[0].Modifiers.Should().Contain("No onions");
        state.Items[0].SpecialInstructions.Should().Be("Well done");
        state.Items[0].Status.Should().Be(TicketItemStatus.Pending);
        state.AssignedStationIds.Should().Contain(stationId);
    }

    // Given: A kitchen ticket with a pending burger item
    // When: The cook starts preparing the burger
    // Then: The item status changes to preparing and the ticket becomes in-progress
    [Fact]
    public async Task StartItemAsync_ShouldStartItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var preparedBy = Guid.NewGuid();
        var grain = await CreateTicketAsync(orgId, siteId, ticketId, orderId);
        await grain.AddItemAsync(new AddTicketItemCommand(Guid.NewGuid(), Guid.NewGuid(), "Burger", 1));

        var state = await grain.GetStateAsync();
        var itemId = state.Items[0].Id;

        // Act
        await grain.StartItemAsync(new StartItemCommand(itemId, preparedBy));

        // Assert
        state = await grain.GetStateAsync();
        state.Items[0].Status.Should().Be(TicketItemStatus.Preparing);
        state.Items[0].StartedAt.Should().NotBeNull();
        state.Items[0].PreparedBy.Should().Be(preparedBy);
        state.Status.Should().Be(TicketStatus.InProgress);
    }

    // Given: A kitchen ticket with a burger being prepared
    // When: The cook completes the burger preparation
    // Then: The item status changes to ready and the single-item ticket becomes ready
    [Fact]
    public async Task CompleteItemAsync_ShouldCompleteItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var preparedBy = Guid.NewGuid();
        var grain = await CreateTicketAsync(orgId, siteId, ticketId, orderId);
        await grain.AddItemAsync(new AddTicketItemCommand(Guid.NewGuid(), Guid.NewGuid(), "Burger", 1));

        var state = await grain.GetStateAsync();
        var itemId = state.Items[0].Id;
        await grain.StartItemAsync(new StartItemCommand(itemId));

        // Act
        await grain.CompleteItemAsync(new CompleteItemCommand(itemId, preparedBy));

        // Assert
        state = await grain.GetStateAsync();
        state.Items[0].Status.Should().Be(TicketItemStatus.Ready);
        state.Items[0].CompletedAt.Should().NotBeNull();
        state.Status.Should().Be(TicketStatus.Ready); // Only item completed
    }

    // Given: A kitchen ticket with a burger and fries both being prepared
    // When: Both items are completed
    // Then: The ticket status changes to ready with completion time recorded
    [Fact]
    public async Task CompleteAllItems_ShouldMakeTicketReady()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var preparedBy = Guid.NewGuid();
        var grain = await CreateTicketAsync(orgId, siteId, ticketId, orderId);
        await grain.AddItemAsync(new AddTicketItemCommand(Guid.NewGuid(), Guid.NewGuid(), "Burger", 1));
        await grain.AddItemAsync(new AddTicketItemCommand(Guid.NewGuid(), Guid.NewGuid(), "Fries", 1));

        var state = await grain.GetStateAsync();
        var item1Id = state.Items[0].Id;
        var item2Id = state.Items[1].Id;

        await grain.StartItemAsync(new StartItemCommand(item1Id));
        await grain.StartItemAsync(new StartItemCommand(item2Id));

        // Act
        await grain.CompleteItemAsync(new CompleteItemCommand(item1Id, preparedBy));
        await grain.CompleteItemAsync(new CompleteItemCommand(item2Id, preparedBy));

        // Assert
        state = await grain.GetStateAsync();
        state.Status.Should().Be(TicketStatus.Ready);
        state.CompletedAt.Should().NotBeNull();
        state.PrepTime.Should().NotBeNull();
    }

    // Given: A kitchen ticket with a pending burger item
    // When: The item is voided because the customer changed their order
    // Then: The item status changes to voided
    [Fact]
    public async Task VoidItemAsync_ShouldVoidItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = await CreateTicketAsync(orgId, siteId, ticketId, orderId);
        await grain.AddItemAsync(new AddTicketItemCommand(Guid.NewGuid(), Guid.NewGuid(), "Burger", 1));

        var state = await grain.GetStateAsync();
        var itemId = state.Items[0].Id;

        // Act
        await grain.VoidItemAsync(new VoidItemCommand(itemId, "Customer changed order"));

        // Assert
        state = await grain.GetStateAsync();
        state.Items[0].Status.Should().Be(TicketItemStatus.Voided);
    }

    // Given: A kitchen ticket with all items completed and ready for service
    // When: The expo bumps the ticket to mark it as served
    // Then: The ticket status changes to served with bump timestamp recorded
    [Fact]
    public async Task BumpAsync_ShouldMarkServed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var bumpedBy = Guid.NewGuid();
        var grain = await CreateTicketAsync(orgId, siteId, ticketId, orderId);
        await grain.AddItemAsync(new AddTicketItemCommand(Guid.NewGuid(), Guid.NewGuid(), "Burger", 1));

        var state = await grain.GetStateAsync();
        var itemId = state.Items[0].Id;
        await grain.StartItemAsync(new StartItemCommand(itemId));
        await grain.CompleteItemAsync(new CompleteItemCommand(itemId, Guid.NewGuid()));

        // Act
        await grain.BumpAsync(bumpedBy);

        // Assert
        state = await grain.GetStateAsync();
        state.Status.Should().Be(TicketStatus.Served);
        state.BumpedAt.Should().NotBeNull();
        state.BumpedBy.Should().Be(bumpedBy);
    }

    // Given: An existing kitchen ticket
    // When: The entire ticket is voided due to order cancellation
    // Then: The ticket status changes to voided with the void reason noted
    [Fact]
    public async Task VoidAsync_ShouldVoidTicket()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = await CreateTicketAsync(orgId, siteId, ticketId, orderId);

        // Act
        await grain.VoidAsync("Order cancelled");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(TicketStatus.Voided);
        state.Notes.Should().Contain("VOID: Order cancelled");
    }

    // Given: An existing kitchen ticket with normal priority
    // When: The priority is escalated to rush
    // Then: The ticket priority updates to rush
    [Fact]
    public async Task SetPriorityAsync_ShouldUpdatePriority()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = await CreateTicketAsync(orgId, siteId, ticketId, orderId);

        // Act
        await grain.SetPriorityAsync(TicketPriority.Rush);

        // Assert
        var state = await grain.GetStateAsync();
        state.Priority.Should().Be(TicketPriority.Rush);
    }

    // Given: An existing kitchen ticket with normal priority
    // When: The ticket is marked as rush
    // Then: The ticket priority changes to rush
    [Fact]
    public async Task MarkRushAsync_ShouldSetRushPriority()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = await CreateTicketAsync(orgId, siteId, ticketId, orderId);

        // Act
        await grain.MarkRushAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Priority.Should().Be(TicketPriority.Rush);
    }

    // Given: An existing kitchen ticket
    // When: Fire-all is triggered to expedite all items
    // Then: The ticket is marked as fire-all with AllDay priority
    [Fact]
    public async Task FireAllAsync_ShouldSetFireAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = await CreateTicketAsync(orgId, siteId, ticketId, orderId);

        // Act
        await grain.FireAllAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.IsFireAll.Should().BeTrue();
        state.Priority.Should().Be(TicketPriority.AllDay);
    }

    // Given: A kitchen ticket with three items, one already completed
    // When: Pending items are queried
    // Then: Only the two items still awaiting preparation are returned
    [Fact]
    public async Task GetPendingItemsAsync_ShouldReturnPendingItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = await CreateTicketAsync(orgId, siteId, ticketId, orderId);
        await grain.AddItemAsync(new AddTicketItemCommand(Guid.NewGuid(), Guid.NewGuid(), "Burger", 1));
        await grain.AddItemAsync(new AddTicketItemCommand(Guid.NewGuid(), Guid.NewGuid(), "Fries", 1));
        await grain.AddItemAsync(new AddTicketItemCommand(Guid.NewGuid(), Guid.NewGuid(), "Salad", 1));

        var state = await grain.GetStateAsync();
        await grain.StartItemAsync(new StartItemCommand(state.Items[0].Id));
        await grain.CompleteItemAsync(new CompleteItemCommand(state.Items[0].Id, Guid.NewGuid()));

        // Act
        var pending = await grain.GetPendingItemsAsync();

        // Assert
        pending.Should().HaveCount(2);
        pending.Select(i => i.Name).Should().Contain("Fries");
        pending.Select(i => i.Name).Should().Contain("Salad");
    }

    // Given: A kitchen ticket with one item that has been started and completed
    // When: Ticket timings are queried
    // Then: Wait time, prep time, and completion timestamp are all recorded
    [Fact]
    public async Task GetTimingsAsync_ShouldReturnTimings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = await CreateTicketAsync(orgId, siteId, ticketId, orderId);
        await grain.AddItemAsync(new AddTicketItemCommand(Guid.NewGuid(), Guid.NewGuid(), "Burger", 1));

        var state = await grain.GetStateAsync();
        await grain.StartItemAsync(new StartItemCommand(state.Items[0].Id));
        await grain.CompleteItemAsync(new CompleteItemCommand(state.Items[0].Id, Guid.NewGuid()));

        // Act
        var timings = await grain.GetTimingsAsync();

        // Assert
        timings.WaitTime.Should().NotBeNull();
        timings.PrepTime.Should().NotBeNull();
        timings.CompletedAt.Should().NotBeNull();
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class KitchenStationGrainTests
{
    private readonly TestClusterFixture _fixture;

    public KitchenStationGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IKitchenStationGrain> CreateStationAsync(Guid orgId, Guid siteId, Guid stationId, string name = "Grill Station")
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IKitchenStationGrain>(GrainKeys.KitchenStation(orgId, siteId, stationId));
        await grain.OpenAsync(new OpenStationCommand(orgId, siteId, name, StationType.Grill, 1));
        return grain;
    }

    // Given: A new kitchen station grain
    // When: The station is opened as a grill station
    // Then: The station is active with correct name, type, and open status
    [Fact]
    public async Task OpenAsync_ShouldOpenStation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IKitchenStationGrain>(GrainKeys.KitchenStation(orgId, siteId, stationId));

        // Act
        await grain.OpenAsync(new OpenStationCommand(orgId, siteId, "Grill", StationType.Grill, 1));

        // Assert
        var state = await grain.GetStateAsync();
        state.Id.Should().Be(stationId);
        state.Name.Should().Be("Grill");
        state.Type.Should().Be(StationType.Grill);
        state.Status.Should().Be(StationStatus.Open);
    }

    // Given: An open kitchen station
    // When: Menu categories and specific menu items are assigned to the station
    // Then: The station tracks both category and item assignments for routing
    [Fact]
    public async Task AssignItemsAsync_ShouldAssignItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var grain = await CreateStationAsync(orgId, siteId, stationId);

        var categoryId = Guid.NewGuid();
        var menuItemId = Guid.NewGuid();

        // Act
        await grain.AssignItemsAsync(new AssignItemsToStationCommand([categoryId], [menuItemId]));

        // Assert
        var state = await grain.GetStateAsync();
        state.AssignedMenuItemCategories.Should().Contain(categoryId);
        state.AssignedMenuItemIds.Should().Contain(menuItemId);
    }

    // Given: An open kitchen station
    // When: A printer is assigned to the station
    // Then: The station records the printer ID for ticket printing
    [Fact]
    public async Task SetPrinterAsync_ShouldSetPrinter()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = await CreateStationAsync(orgId, siteId, stationId);

        // Act
        await grain.SetPrinterAsync(printerId);

        // Assert
        var state = await grain.GetStateAsync();
        state.PrinterId.Should().Be(printerId);
    }

    // Given: An open kitchen station
    // When: A kitchen display screen is assigned to the station
    // Then: The station records the display ID for KDS routing
    [Fact]
    public async Task SetDisplayAsync_ShouldSetDisplay()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var displayId = Guid.NewGuid();
        var grain = await CreateStationAsync(orgId, siteId, stationId);

        // Act
        await grain.SetDisplayAsync(displayId);

        // Assert
        var state = await grain.GetStateAsync();
        state.DisplayId.Should().Be(displayId);
    }

    // Given: An open kitchen station with no active tickets
    // When: A kitchen ticket is routed to the station
    // Then: The station tracks the ticket in its active queue
    [Fact]
    public async Task ReceiveTicketAsync_ShouldAddTicket()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var grain = await CreateStationAsync(orgId, siteId, stationId);

        // Act
        await grain.ReceiveTicketAsync(ticketId);

        // Assert
        var tickets = await grain.GetCurrentTicketIdsAsync();
        tickets.Should().Contain(ticketId);
    }

    // Given: An open kitchen station with one active ticket
    // When: The ticket is completed at the station
    // Then: The ticket is removed from the station's active queue
    [Fact]
    public async Task CompleteTicketAsync_ShouldRemoveTicket()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var grain = await CreateStationAsync(orgId, siteId, stationId);
        await grain.ReceiveTicketAsync(ticketId);

        // Act
        await grain.CompleteTicketAsync(ticketId);

        // Assert
        var tickets = await grain.GetCurrentTicketIdsAsync();
        tickets.Should().NotContain(ticketId);
    }

    // Given: An open kitchen station
    // When: The station is paused
    // Then: The station status changes to paused
    [Fact]
    public async Task PauseAsync_ShouldPauseStation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var grain = await CreateStationAsync(orgId, siteId, stationId);

        // Act
        await grain.PauseAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(StationStatus.Paused);
    }

    // Given: A paused kitchen station
    // When: The station is resumed
    // Then: The station status returns to open
    [Fact]
    public async Task ResumeAsync_ShouldResumeStation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var grain = await CreateStationAsync(orgId, siteId, stationId);
        await grain.PauseAsync();

        // Act
        await grain.ResumeAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(StationStatus.Open);
    }

    // Given: An open kitchen station with one active ticket
    // When: The station is closed at end of shift
    // Then: The station is closed with timestamp and active tickets are cleared
    [Fact]
    public async Task CloseAsync_ShouldCloseStation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var grain = await CreateStationAsync(orgId, siteId, stationId);
        await grain.ReceiveTicketAsync(Guid.NewGuid());

        // Act
        await grain.CloseAsync(Guid.NewGuid());

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(StationStatus.Closed);
        state.ClosedAt.Should().NotBeNull();
        state.CurrentTicketIds.Should().BeEmpty();
    }

    // Given: An open kitchen station
    // When: The station's open status is checked
    // Then: The check confirms the station is open
    [Fact]
    public async Task IsOpenAsync_WhenOpen_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var grain = await CreateStationAsync(orgId, siteId, stationId);

        // Act
        var isOpen = await grain.IsOpenAsync();

        // Assert
        isOpen.Should().BeTrue();
    }

    // Given: A closed kitchen station
    // When: The station's open status is checked
    // Then: The check confirms the station is not open
    [Fact]
    public async Task IsOpenAsync_WhenClosed_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var grain = await CreateStationAsync(orgId, siteId, stationId);
        await grain.CloseAsync(Guid.NewGuid());

        // Act
        var isOpen = await grain.IsOpenAsync();

        // Assert
        isOpen.Should().BeFalse();
    }
}
