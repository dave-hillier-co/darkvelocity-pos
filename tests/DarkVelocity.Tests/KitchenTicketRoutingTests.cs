using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for kitchen ticket routing scenarios including multi-station orders,
/// station assignment, and station failure handling.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class KitchenTicketRoutingTests
{
    private readonly TestClusterFixture _fixture;

    public KitchenTicketRoutingTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IKitchenTicketGrain GetTicketGrain(Guid orgId, Guid siteId, Guid ticketId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IKitchenTicketGrain>(
            GrainKeys.KitchenOrder(orgId, siteId, ticketId));
    }

    private IKitchenStationGrain GetStationGrain(Guid orgId, Guid siteId, Guid stationId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IKitchenStationGrain>(
            GrainKeys.KitchenStation(orgId, siteId, stationId));
    }

    private async Task<IKitchenTicketGrain> CreateTicketAsync(
        Guid orgId, Guid siteId, Guid ticketId,
        string tableNumber = "T1",
        TicketPriority priority = TicketPriority.Normal)
    {
        var grain = GetTicketGrain(orgId, siteId, ticketId);
        await grain.CreateAsync(new CreateKitchenTicketCommand(
            orgId,
            siteId,
            Guid.NewGuid(),
            $"ORD-{ticketId.ToString()[..4]}",
            OrderType.DineIn,
            tableNumber,
            2,
            "Server",
            null,
            priority));
        return grain;
    }

    private async Task<IKitchenStationGrain> CreateStationAsync(
        Guid orgId, Guid siteId, Guid stationId,
        string name, StationType type, int displayOrder = 0)
    {
        var grain = GetStationGrain(orgId, siteId, stationId);
        await grain.OpenAsync(new OpenStationCommand(orgId, siteId, name, type, displayOrder));
        return grain;
    }

    #region Multi-Station Routing Tests

    [Fact]
    public async Task AddItemAsync_WithDifferentStations_ShouldTrackAllStations()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var grillStationId = Guid.NewGuid();
        var fryStationId = Guid.NewGuid();
        var saladStationId = Guid.NewGuid();

        var ticket = await CreateTicketAsync(orgId, siteId, ticketId);

        // Act - Add items for different stations
        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Burger", 1,
            null, null, grillStationId, "Grill"));

        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "French Fries", 1,
            null, null, fryStationId, "Fry"));

        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Caesar Salad", 1,
            null, null, saladStationId, "Salad"));

        // Assert
        var state = await ticket.GetStateAsync();
        state.Items.Should().HaveCount(3);
        state.AssignedStationIds.Should().HaveCount(3);
        state.AssignedStationIds.Should().Contain(grillStationId);
        state.AssignedStationIds.Should().Contain(fryStationId);
        state.AssignedStationIds.Should().Contain(saladStationId);
    }

    [Fact]
    public async Task AddItemAsync_SameStationMultipleTimes_ShouldNotDuplicateStation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var grillStationId = Guid.NewGuid();

        var ticket = await CreateTicketAsync(orgId, siteId, ticketId);

        // Act - Add multiple items for same station
        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Burger", 1,
            null, null, grillStationId, "Grill"));

        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Steak", 1,
            null, null, grillStationId, "Grill"));

        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Grilled Chicken", 1,
            null, null, grillStationId, "Grill"));

        // Assert
        var state = await ticket.GetStateAsync();
        state.Items.Should().HaveCount(3);
        state.AssignedStationIds.Should().HaveCount(1);
        state.AssignedStationIds.Should().Contain(grillStationId);
    }

    [Fact]
    public async Task AddItemAsync_NoStationAssigned_ShouldStillAddItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        var ticket = await CreateTicketAsync(orgId, siteId, ticketId);

        // Act - Add item without station
        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Drink", 1,
            null, null, null, null));

        // Assert
        var state = await ticket.GetStateAsync();
        state.Items.Should().HaveCount(1);
        state.Items[0].StationId.Should().BeNull();
    }

    #endregion

    #region Station Receiving Tests

    [Fact]
    public async Task ReceiveTicketAsync_ShouldAddTicketToStation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var stationId = Guid.NewGuid();

        await CreateTicketAsync(orgId, siteId, ticketId);
        var station = await CreateStationAsync(orgId, siteId, stationId, "Grill", StationType.Grill);

        // Act
        await station.ReceiveTicketAsync(ticketId);

        // Assert
        var tickets = await station.GetCurrentTicketIdsAsync();
        tickets.Should().Contain(ticketId);
    }

    [Fact]
    public async Task ReceiveTicketAsync_MultipleTickets_ShouldTrackAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticket1Id = Guid.NewGuid();
        var ticket2Id = Guid.NewGuid();
        var ticket3Id = Guid.NewGuid();
        var stationId = Guid.NewGuid();

        await CreateTicketAsync(orgId, siteId, ticket1Id);
        await CreateTicketAsync(orgId, siteId, ticket2Id);
        await CreateTicketAsync(orgId, siteId, ticket3Id);
        var station = await CreateStationAsync(orgId, siteId, stationId, "Grill", StationType.Grill);

        // Act
        await station.ReceiveTicketAsync(ticket1Id);
        await station.ReceiveTicketAsync(ticket2Id);
        await station.ReceiveTicketAsync(ticket3Id);

        // Assert
        var tickets = await station.GetCurrentTicketIdsAsync();
        tickets.Should().HaveCount(3);
        tickets.Should().Contain(ticket1Id);
        tickets.Should().Contain(ticket2Id);
        tickets.Should().Contain(ticket3Id);
    }

    [Fact]
    public async Task CompleteTicketAsync_ShouldRemoveFromStation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var stationId = Guid.NewGuid();

        await CreateTicketAsync(orgId, siteId, ticketId);
        var station = await CreateStationAsync(orgId, siteId, stationId, "Grill", StationType.Grill);
        await station.ReceiveTicketAsync(ticketId);

        // Act
        await station.CompleteTicketAsync(ticketId);

        // Assert
        var tickets = await station.GetCurrentTicketIdsAsync();
        tickets.Should().NotContain(ticketId);
    }

    [Fact]
    public async Task RemoveTicketAsync_ShouldRemoveWithoutCompletion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var stationId = Guid.NewGuid();

        await CreateTicketAsync(orgId, siteId, ticketId);
        var station = await CreateStationAsync(orgId, siteId, stationId, "Grill", StationType.Grill);
        await station.ReceiveTicketAsync(ticketId);

        // Act
        await station.RemoveTicketAsync(ticketId);

        // Assert
        var tickets = await station.GetCurrentTicketIdsAsync();
        tickets.Should().NotContain(ticketId);
    }

    #endregion

    #region Station Pause/Resume Tests

    [Fact]
    public async Task PauseAsync_ShouldPauseStation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = await CreateStationAsync(orgId, siteId, stationId, "Grill", StationType.Grill);

        // Act
        await station.PauseAsync();

        // Assert
        var state = await station.GetStateAsync();
        state.Status.Should().Be(StationStatus.Paused);
    }

    [Fact]
    public async Task PauseAsync_WithActiveTickets_ShouldKeepTickets()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var stationId = Guid.NewGuid();

        await CreateTicketAsync(orgId, siteId, ticketId);
        var station = await CreateStationAsync(orgId, siteId, stationId, "Grill", StationType.Grill);
        await station.ReceiveTicketAsync(ticketId);

        // Act
        await station.PauseAsync();

        // Assert
        var state = await station.GetStateAsync();
        state.Status.Should().Be(StationStatus.Paused);
        state.CurrentTicketIds.Should().Contain(ticketId);
    }

    [Fact]
    public async Task ResumeAsync_ShouldResumeStation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = await CreateStationAsync(orgId, siteId, stationId, "Grill", StationType.Grill);
        await station.PauseAsync();

        // Act
        await station.ResumeAsync();

        // Assert
        var state = await station.GetStateAsync();
        state.Status.Should().Be(StationStatus.Open);
    }

    [Fact]
    public async Task ResumeAsync_AfterPause_ShouldRetainTickets()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var stationId = Guid.NewGuid();

        await CreateTicketAsync(orgId, siteId, ticketId);
        var station = await CreateStationAsync(orgId, siteId, stationId, "Grill", StationType.Grill);
        await station.ReceiveTicketAsync(ticketId);
        await station.PauseAsync();

        // Act
        await station.ResumeAsync();

        // Assert
        var tickets = await station.GetCurrentTicketIdsAsync();
        tickets.Should().Contain(ticketId);
    }

    #endregion

    #region Station Close Tests

    [Fact]
    public async Task CloseAsync_ShouldCloseStation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var closedBy = Guid.NewGuid();
        var station = await CreateStationAsync(orgId, siteId, stationId, "Grill", StationType.Grill);

        // Act
        await station.CloseAsync(closedBy);

        // Assert
        var state = await station.GetStateAsync();
        state.Status.Should().Be(StationStatus.Closed);
        state.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CloseAsync_WithActiveTickets_ShouldClearTickets()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var stationId = Guid.NewGuid();

        await CreateTicketAsync(orgId, siteId, ticketId);
        var station = await CreateStationAsync(orgId, siteId, stationId, "Grill", StationType.Grill);
        await station.ReceiveTicketAsync(ticketId);

        // Act
        await station.CloseAsync(Guid.NewGuid());

        // Assert
        var state = await station.GetStateAsync();
        state.CurrentTicketIds.Should().BeEmpty();
    }

    [Fact]
    public async Task IsOpenAsync_WhenPaused_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = await CreateStationAsync(orgId, siteId, stationId, "Grill", StationType.Grill);
        await station.PauseAsync();

        // Act
        var isOpen = await station.IsOpenAsync();

        // Assert
        isOpen.Should().BeFalse();
    }

    #endregion

    #region Item Assignment Tests

    [Fact]
    public async Task AssignItemsAsync_Categories_ShouldAssignCategories()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var category1 = Guid.NewGuid();
        var category2 = Guid.NewGuid();
        var station = await CreateStationAsync(orgId, siteId, stationId, "Grill", StationType.Grill);

        // Act
        await station.AssignItemsAsync(new AssignItemsToStationCommand(
            MenuItemCategories: [category1, category2]));

        // Assert
        var state = await station.GetStateAsync();
        state.AssignedMenuItemCategories.Should().HaveCount(2);
        state.AssignedMenuItemCategories.Should().Contain(category1);
        state.AssignedMenuItemCategories.Should().Contain(category2);
    }

    [Fact]
    public async Task AssignItemsAsync_SpecificItems_ShouldAssignItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var item1 = Guid.NewGuid();
        var item2 = Guid.NewGuid();
        var station = await CreateStationAsync(orgId, siteId, stationId, "Specialty", StationType.Prep);

        // Act
        await station.AssignItemsAsync(new AssignItemsToStationCommand(
            MenuItemIds: [item1, item2]));

        // Assert
        var state = await station.GetStateAsync();
        state.AssignedMenuItemIds.Should().HaveCount(2);
        state.AssignedMenuItemIds.Should().Contain(item1);
        state.AssignedMenuItemIds.Should().Contain(item2);
    }

    [Fact]
    public async Task AssignItemsAsync_CategoriesAndItems_ShouldAssignBoth()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var category = Guid.NewGuid();
        var item = Guid.NewGuid();
        var station = await CreateStationAsync(orgId, siteId, stationId, "Mixed", StationType.Salad);

        // Act
        await station.AssignItemsAsync(new AssignItemsToStationCommand(
            MenuItemCategories: [category],
            MenuItemIds: [item]));

        // Assert
        var state = await station.GetStateAsync();
        state.AssignedMenuItemCategories.Should().Contain(category);
        state.AssignedMenuItemIds.Should().Contain(item);
    }

    #endregion

    #region Priority and Rush Tests

    [Fact]
    public async Task SetPriorityAsync_ShouldUpdateTicketPriority()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketAsync(orgId, siteId, ticketId, priority: TicketPriority.Normal);

        // Act
        await ticket.SetPriorityAsync(TicketPriority.VIP);

        // Assert
        var state = await ticket.GetStateAsync();
        state.Priority.Should().Be(TicketPriority.VIP);
    }

    [Fact]
    public async Task MarkRushAsync_ShouldSetRushPriority()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketAsync(orgId, siteId, ticketId);

        // Act
        await ticket.MarkRushAsync();

        // Assert
        var state = await ticket.GetStateAsync();
        state.Priority.Should().Be(TicketPriority.Rush);
    }

    [Fact]
    public async Task MarkVipAsync_ShouldSetVipPriority()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketAsync(orgId, siteId, ticketId);

        // Act
        await ticket.MarkVipAsync();

        // Assert
        var state = await ticket.GetStateAsync();
        state.Priority.Should().Be(TicketPriority.VIP);
    }

    [Fact]
    public async Task FireAllAsync_ShouldSetFireAllAndAllDayPriority()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketAsync(orgId, siteId, ticketId);

        // Act
        await ticket.FireAllAsync();

        // Assert
        var state = await ticket.GetStateAsync();
        state.IsFireAll.Should().BeTrue();
        state.Priority.Should().Be(TicketPriority.AllDay);
    }

    #endregion

    #region Course Management Tests

    [Fact]
    public async Task AddItemAsync_WithCourseNumber_ShouldTrackCourse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketAsync(orgId, siteId, ticketId);

        // Act
        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Soup", 1,
            null, null, null, null, CourseNumber: 1));

        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Steak", 1,
            null, null, null, null, CourseNumber: 2));

        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Dessert", 1,
            null, null, null, null, CourseNumber: 3));

        // Assert
        var state = await ticket.GetStateAsync();
        state.Items.Should().HaveCount(3);
        state.Items[0].CourseNumber.Should().Be(1);
        state.Items[1].CourseNumber.Should().Be(2);
        state.Items[2].CourseNumber.Should().Be(3);
    }

    [Fact]
    public async Task CreateAsync_WithCourseNumber_ShouldSetDefaultCourse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var grain = GetTicketGrain(orgId, siteId, ticketId);

        // Act
        await grain.CreateAsync(new CreateKitchenTicketCommand(
            orgId, siteId, Guid.NewGuid(), "ORD-001",
            OrderType.DineIn, "T1", 4, "Server",
            null, TicketPriority.Normal, CourseNumber: 2));

        // Assert
        var state = await grain.GetStateAsync();
        state.CourseNumber.Should().Be(2);
    }

    #endregion

    #region Ticket Void Tests

    [Fact]
    public async Task VoidAsync_ShouldVoidTicket()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketAsync(orgId, siteId, ticketId);

        // Act
        await ticket.VoidAsync("Order cancelled by customer");

        // Assert
        var state = await ticket.GetStateAsync();
        state.Status.Should().Be(TicketStatus.Voided);
    }

    [Fact]
    public async Task VoidItemAsync_SingleItem_ShouldVoidItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketAsync(orgId, siteId, ticketId);
        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Burger", 1));
        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Fries", 1));

        var state = await ticket.GetStateAsync();
        var itemToVoid = state.Items[0].Id;

        // Act
        await ticket.VoidItemAsync(new VoidItemCommand(itemToVoid, "Customer changed order"));

        // Assert
        state = await ticket.GetStateAsync();
        state.Items.First(i => i.Id == itemToVoid).Status.Should().Be(TicketItemStatus.Voided);
        state.Items.First(i => i.Name == "Fries").Status.Should().Be(TicketItemStatus.Pending);
    }

    [Fact]
    public async Task VoidAsync_AllItems_ShouldVoidAllItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = await CreateTicketAsync(orgId, siteId, ticketId);
        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Burger", 1));
        await ticket.AddItemAsync(new AddTicketItemCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Fries", 1));

        // Act
        await ticket.VoidAsync("Table left without ordering");

        // Assert
        var state = await ticket.GetStateAsync();
        state.Status.Should().Be(TicketStatus.Voided);
    }

    #endregion

    #region Printer and Display Tests

    [Fact]
    public async Task SetPrinterAsync_ShouldAssignPrinter()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var station = await CreateStationAsync(orgId, siteId, stationId, "Grill", StationType.Grill);

        // Act
        await station.SetPrinterAsync(printerId);

        // Assert
        var state = await station.GetStateAsync();
        state.PrinterId.Should().Be(printerId);
    }

    [Fact]
    public async Task SetDisplayAsync_ShouldAssignDisplay()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var displayId = Guid.NewGuid();
        var station = await CreateStationAsync(orgId, siteId, stationId, "Grill", StationType.Grill);

        // Act
        await station.SetDisplayAsync(displayId);

        // Assert
        var state = await station.GetStateAsync();
        state.DisplayId.Should().Be(displayId);
    }

    [Fact]
    public async Task SetPrinterAsync_ThenSetDisplay_ShouldHaveBoth()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var displayId = Guid.NewGuid();
        var station = await CreateStationAsync(orgId, siteId, stationId, "Grill", StationType.Grill);

        // Act
        await station.SetPrinterAsync(printerId);
        await station.SetDisplayAsync(displayId);

        // Assert
        var state = await station.GetStateAsync();
        state.PrinterId.Should().Be(printerId);
        state.DisplayId.Should().Be(displayId);
    }

    #endregion

    #region Station Type Tests

    [Fact]
    public async Task CreateStation_DifferentTypes_ShouldSetCorrectType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        // Act
        var grillStation = await CreateStationAsync(orgId, siteId, Guid.NewGuid(), "Grill", StationType.Grill);
        var coldStation = await CreateStationAsync(orgId, siteId, Guid.NewGuid(), "Salad", StationType.Salad);
        var prepStation = await CreateStationAsync(orgId, siteId, Guid.NewGuid(), "Prep", StationType.Prep);
        var expoStation = await CreateStationAsync(orgId, siteId, Guid.NewGuid(), "Expo", StationType.Expo);

        // Assert
        (await grillStation.GetStateAsync()).Type.Should().Be(StationType.Grill);
        (await coldStation.GetStateAsync()).Type.Should().Be(StationType.Salad);
        (await prepStation.GetStateAsync()).Type.Should().Be(StationType.Prep);
        (await expoStation.GetStateAsync()).Type.Should().Be(StationType.Expo);
    }

    #endregion
}
