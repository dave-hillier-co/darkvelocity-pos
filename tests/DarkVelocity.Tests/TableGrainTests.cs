using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TableGrainTests
{
    private readonly TestClusterFixture _fixture;

    public TableGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ITableGrain> CreateTableAsync(Guid orgId, Guid siteId, Guid tableId, string number = "T1")
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ITableGrain>(GrainKeys.Table(orgId, siteId, tableId));
        await grain.CreateAsync(new CreateTableCommand(orgId, siteId, number));
        return grain;
    }

    // Given: no tables exist in the venue
    // When: a new corner booth table T5 is created with capacity 2-6 and rectangle shape
    // Then: the table is created with all specified properties and Available status
    [Fact]
    public async Task CreateAsync_ShouldCreateTable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ITableGrain>(GrainKeys.Table(orgId, siteId, tableId));

        var command = new CreateTableCommand(
            orgId,
            siteId,
            "T5",
            MinCapacity: 2,
            MaxCapacity: 6,
            Name: "Corner Booth",
            Shape: TableShape.Rectangle);

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Id.Should().Be(tableId);
        result.Number.Should().Be("T5");

        var state = await grain.GetStateAsync();
        state.Number.Should().Be("T5");
        state.Name.Should().Be("Corner Booth");
        state.MinCapacity.Should().Be(2);
        state.MaxCapacity.Should().Be(6);
        state.Shape.Should().Be(TableShape.Rectangle);
        state.Status.Should().Be(TableStatus.Available);
    }

    // Given: an existing table
    // When: the table number, name, capacity, and shape are updated
    // Then: all updated properties are persisted correctly
    [Fact]
    public async Task UpdateAsync_ShouldUpdateTable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);

        // Act
        await grain.UpdateAsync(new UpdateTableCommand(
            Number: "T10",
            Name: "Window Seat",
            MaxCapacity: 8,
            Shape: TableShape.Round));

        // Assert
        var state = await grain.GetStateAsync();
        state.Number.Should().Be("T10");
        state.Name.Should().Be("Window Seat");
        state.MaxCapacity.Should().Be(8);
        state.Shape.Should().Be(TableShape.Round);
    }

    // Given: an available table
    // When: the Smith Party of 4 is seated with a linked booking and order
    // Then: the table status changes to Occupied with all occupancy details recorded
    [Fact]
    public async Task SeatAsync_ShouldOccupyTable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);

        // Act
        await grain.SeatAsync(new SeatTableCommand(bookingId, orderId, "Smith Party", 4, serverId));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(TableStatus.Occupied);
        state.CurrentOccupancy.Should().NotBeNull();
        state.CurrentOccupancy!.BookingId.Should().Be(bookingId);
        state.CurrentOccupancy.OrderId.Should().Be(orderId);
        state.CurrentOccupancy.GuestName.Should().Be("Smith Party");
        state.CurrentOccupancy.GuestCount.Should().Be(4);
        state.CurrentOccupancy.ServerId.Should().Be(serverId);
    }

    // Given: an occupied table with a walk-in guest
    // When: the guest departs and the table is cleared
    // Then: the table status changes to Dirty and the occupancy is removed
    [Fact]
    public async Task ClearAsync_ShouldMarkTableDirty()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);
        await grain.SeatAsync(new SeatTableCommand(null, null, "Walk-in", 2));

        // Act
        await grain.ClearAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(TableStatus.Dirty);
        state.CurrentOccupancy.Should().BeNull();
    }

    // Given: a dirty table that needs bussing
    // When: staff marks the table as clean
    // Then: the table status changes back to Available
    [Fact]
    public async Task MarkCleanAsync_ShouldMakeTableAvailable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);
        await grain.SeatAsync(new SeatTableCommand(null, null, "Walk-in", 2));
        await grain.ClearAsync();

        // Act
        await grain.MarkCleanAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(TableStatus.Available);
    }

    // Given: an available table
    // When: the table is blocked for a VIP reservation
    // Then: the table status changes to Blocked
    [Fact]
    public async Task BlockAsync_ShouldBlockTable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);

        // Act
        await grain.BlockAsync("Reserved for VIP");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(TableStatus.Blocked);
    }

    // Given: a blocked table
    // When: the block is released
    // Then: the table status changes back to Available
    [Fact]
    public async Task UnblockAsync_ShouldUnblockTable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);
        await grain.BlockAsync();

        // Act
        await grain.UnblockAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(TableStatus.Available);
    }

    // Given: two separate tables
    // When: the tables are combined to accommodate a larger party
    // Then: the first table tracks the combination with the second table
    [Fact]
    public async Task CombineWithAsync_ShouldCombineTables()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId1 = Guid.NewGuid();
        var tableId2 = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId1);

        // Act
        await grain.CombineWithAsync(tableId2);

        // Assert
        var state = await grain.GetStateAsync();
        state.CombinedWith.Should().Contain(tableId2);
    }

    // Given: two tables that have been combined
    // When: the tables are uncombined
    // Then: the combination is cleared and the tables are independent again
    [Fact]
    public async Task UncombineAsync_ShouldSeparateTables()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId1 = Guid.NewGuid();
        var tableId2 = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId1);
        await grain.CombineWithAsync(tableId2);

        // Act
        await grain.UncombineAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.CombinedWith.Should().BeEmpty();
    }

    // Given: an existing table
    // When: "window" and "quiet" tags are added
    // Then: both tags are associated with the table
    [Fact]
    public async Task AddTagAsync_ShouldAddTag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);

        // Act
        await grain.AddTagAsync("window");
        await grain.AddTagAsync("quiet");

        // Assert
        var state = await grain.GetStateAsync();
        state.Tags.Should().Contain("window");
        state.Tags.Should().Contain("quiet");
    }

    // Given: a table tagged with "window" and "quiet"
    // When: the "window" tag is removed
    // Then: only the "quiet" tag remains
    [Fact]
    public async Task RemoveTagAsync_ShouldRemoveTag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);
        await grain.AddTagAsync("window");
        await grain.AddTagAsync("quiet");

        // Act
        await grain.RemoveTagAsync("window");

        // Assert
        var state = await grain.GetStateAsync();
        state.Tags.Should().NotContain("window");
        state.Tags.Should().Contain("quiet");
    }

    // Given: an existing table
    // When: the table's floor plan position is set with coordinates and rotation
    // Then: the position is persisted with the correct X, Y, and rotation values
    [Fact]
    public async Task SetPositionAsync_ShouldUpdatePosition()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);
        var position = new TablePosition { X = 100, Y = 200, Width = 60, Height = 60, Rotation = 45 };

        // Act
        await grain.SetPositionAsync(position);

        // Assert
        var state = await grain.GetStateAsync();
        state.Position.Should().NotBeNull();
        state.Position!.X.Should().Be(100);
        state.Position.Y.Should().Be(200);
        state.Position.Rotation.Should().Be(45);
    }

    // Given: an existing table not assigned to any floor plan
    // When: the table is assigned to a floor plan
    // Then: the table records the floor plan assignment
    [Fact]
    public async Task SetFloorPlanAsync_ShouldAssignFloorPlan()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);

        // Act
        await grain.SetFloorPlanAsync(floorPlanId);

        // Assert
        var state = await grain.GetStateAsync();
        state.FloorPlanId.Should().Be(floorPlanId);
    }

    // Given: a table that transitions through Available, Occupied, Dirty, and back to Available
    // When: availability is checked at each stage
    // Then: only Available status reports as available; Occupied and Dirty report as unavailable
    [Fact]
    public async Task IsAvailableAsync_ShouldReturnCorrectStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);

        // Act & Assert
        (await grain.IsAvailableAsync()).Should().BeTrue();

        await grain.SeatAsync(new SeatTableCommand(null, null, "Guest", 2));
        (await grain.IsAvailableAsync()).Should().BeFalse();

        await grain.ClearAsync();
        (await grain.IsAvailableAsync()).Should().BeFalse();

        await grain.MarkCleanAsync();
        (await grain.IsAvailableAsync()).Should().BeTrue();
    }

    // Given: an existing table
    // When: the table is deleted
    // Then: the table no longer exists in the system
    [Fact]
    public async Task DeleteAsync_ShouldRemoveTable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);

        // Act
        await grain.DeleteAsync();

        // Assert
        var exists = await grain.ExistsAsync();
        exists.Should().BeFalse();
    }

    // Given: a table currently occupied by a guest
    // When: staff attempts to seat another guest at the same table
    // Then: seating is rejected because the table is already occupied
    [Fact]
    public async Task SeatAsync_WhenTableOccupied_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);
        await grain.SeatAsync(new SeatTableCommand(null, null, "First Guest", 2));

        // Act
        var act = () => grain.SeatAsync(new SeatTableCommand(null, null, "Second Guest", 4));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot seat at table with status*");
    }

    // Status Transition Tests

    // Given: a table blocked for a private event
    // When: staff attempts to seat a guest at the blocked table
    // Then: seating is rejected because the table is blocked
    [Fact]
    public async Task SeatAsync_BlockedTable_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);
        await grain.BlockAsync("Reserved for private event");

        // Act
        var act = () => grain.SeatAsync(new SeatTableCommand(null, null, "Guest", 2));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot seat at table with status*");
    }

    // Given: a table in Dirty status awaiting bussing
    // When: staff attempts to seat a new guest at the dirty table
    // Then: seating is rejected because the table has not been cleaned
    [Fact]
    public async Task SeatAsync_DirtyTable_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);
        await grain.SeatAsync(new SeatTableCommand(null, null, "First Guest", 2));
        await grain.ClearAsync(); // Marks table as dirty

        // Act
        var act = () => grain.SeatAsync(new SeatTableCommand(null, null, "New Guest", 4));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot seat at table with status*");
    }

    // Given: a table in Available status (not dirty)
    // When: staff attempts to mark the table as clean
    // Then: the action is rejected because the table is not dirty
    [Fact]
    public async Task MarkCleanAsync_NotDirty_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);
        // Table is in Available status, not Dirty

        // Act
        var act = () => grain.MarkCleanAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Table is not dirty*");
    }

    // Given: a table in Available status (not blocked)
    // When: staff attempts to unblock the table
    // Then: the action is rejected because the table is not blocked
    [Fact]
    public async Task UnblockAsync_NotBlocked_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);
        // Table is in Available status, not Blocked

        // Act
        var act = () => grain.UnblockAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Table is not blocked*");
    }

    // Combination Tests

    // Given: a table marked as non-combinable
    // When: staff attempts to combine it with another table
    // Then: the combination is rejected because the table is not combinable
    [Fact]
    public async Task CombineAsync_NonCombinable_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var otherTableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);

        // Set table as non-combinable
        await grain.UpdateAsync(new UpdateTableCommand(IsCombinable: false));

        // Act
        var act = () => grain.CombineWithAsync(otherTableId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Table is not combinable*");
    }

    // Given: a table already combined with another table
    // When: the same combination is requested again
    // Then: the operation is idempotent with only one combination tracked
    [Fact]
    public async Task CombineAsync_AlreadyCombined_ShouldHandle()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var otherTableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);
        await grain.CombineWithAsync(otherTableId);

        // Act - Combine again with the same table
        await grain.CombineWithAsync(otherTableId);

        // Assert - Should be idempotent
        var state = await grain.GetStateAsync();
        state.CombinedWith.Should().HaveCount(1);
        state.CombinedWith.Should().Contain(otherTableId);
    }

    // Given: a table not combined with any other table
    // When: uncombine is called
    // Then: the operation completes without error (idempotent)
    [Fact]
    public async Task UncombineAsync_NotCombined_ShouldBeIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);
        // Table is not combined with anything

        // Act - Should not throw
        await grain.UncombineAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.CombinedWith.Should().BeEmpty();
    }

    // Occupancy Tests

    // Given: an available table
    // When: a VIP guest party of 6 is seated with booking, order, and server details
    // Then: all occupancy fields including seating timestamp are stored correctly
    [Fact]
    public async Task SeatAsync_WithAllOptionalFields_ShouldStoreAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);

        // Act
        await grain.SeatAsync(new SeatTableCommand(
            BookingId: bookingId,
            OrderId: orderId,
            GuestName: "VIP Guest",
            GuestCount: 6,
            ServerId: serverId));

        // Assert
        var state = await grain.GetStateAsync();
        state.CurrentOccupancy.Should().NotBeNull();
        state.CurrentOccupancy!.BookingId.Should().Be(bookingId);
        state.CurrentOccupancy.OrderId.Should().Be(orderId);
        state.CurrentOccupancy.GuestName.Should().Be("VIP Guest");
        state.CurrentOccupancy.GuestCount.Should().Be(6);
        state.CurrentOccupancy.ServerId.Should().Be(serverId);
        state.CurrentOccupancy.SeatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: a table with the Smith Party of 4 seated
    // When: the current occupancy is queried
    // Then: the occupancy details match the seated party
    [Fact]
    public async Task GetOccupancyAsync_AfterSeating_ShouldReturnCorrect()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);
        await grain.SeatAsync(new SeatTableCommand(bookingId, null, "Smith Party", 4));

        // Act
        var occupancy = await grain.GetOccupancyAsync();

        // Assert
        occupancy.Should().NotBeNull();
        occupancy!.BookingId.Should().Be(bookingId);
        occupancy.GuestName.Should().Be("Smith Party");
        occupancy.GuestCount.Should().Be(4);
    }

    // Given: an occupied table with a guest
    // When: the table is cleared after the guest departs
    // Then: the occupancy is removed and the table status changes to Dirty
    [Fact]
    public async Task ClearAsync_ShouldClearOccupancy()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateTableAsync(orgId, siteId, tableId);
        await grain.SeatAsync(new SeatTableCommand(Guid.NewGuid(), Guid.NewGuid(), "Guest", 3, Guid.NewGuid()));

        // Verify occupancy exists
        var occupancyBefore = await grain.GetOccupancyAsync();
        occupancyBefore.Should().NotBeNull();

        // Act
        await grain.ClearAsync();

        // Assert
        var occupancyAfter = await grain.GetOccupancyAsync();
        occupancyAfter.Should().BeNull();

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(TableStatus.Dirty);
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class FloorPlanGrainTests
{
    private readonly TestClusterFixture _fixture;

    public FloorPlanGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IFloorPlanGrain> CreateFloorPlanAsync(Guid orgId, Guid siteId, Guid floorPlanId, string name = "Main Floor")
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IFloorPlanGrain>(GrainKeys.FloorPlan(orgId, siteId, floorPlanId));
        await grain.CreateAsync(new CreateFloorPlanCommand(orgId, siteId, name));
        return grain;
    }

    // Given: a venue with no floor plans
    // When: a new Patio floor plan is created as the default with specific dimensions
    // Then: the floor plan is created with correct name, dimensions, default flag, and active status
    [Fact]
    public async Task CreateAsync_ShouldCreateFloorPlan()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IFloorPlanGrain>(GrainKeys.FloorPlan(orgId, siteId, floorPlanId));

        var command = new CreateFloorPlanCommand(
            orgId,
            siteId,
            "Patio",
            IsDefault: true,
            Width: 1000,
            Height: 800);

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Id.Should().Be(floorPlanId);
        result.Name.Should().Be("Patio");

        var state = await grain.GetStateAsync();
        state.Name.Should().Be("Patio");
        state.IsDefault.Should().BeTrue();
        state.Width.Should().Be(1000);
        state.Height.Should().Be(800);
        state.IsActive.Should().BeTrue();
    }

    // Given: an existing floor plan
    // When: the name, dimensions, and background image are updated
    // Then: the updated properties are persisted and unchanged properties are preserved
    [Fact]
    public async Task UpdateAsync_ShouldUpdateFloorPlan()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = await CreateFloorPlanAsync(orgId, siteId, floorPlanId);

        // Act
        await grain.UpdateAsync(new UpdateFloorPlanCommand(
            Name: "Rooftop",
            Width: 1200,
            Height: 900,
            BackgroundImageUrl: "https://example.com/floor.png"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Name.Should().Be("Rooftop");
        state.Width.Should().Be(1200);
        state.Height.Should().Be(900);
        state.BackgroundImageUrl.Should().Be("https://example.com/floor.png");
    }

    // Given: an empty floor plan
    // When: two tables are added to the floor plan
    // Then: both table IDs are tracked on the floor plan
    [Fact]
    public async Task AddTableAsync_ShouldAddTableToFloorPlan()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var tableId1 = Guid.NewGuid();
        var tableId2 = Guid.NewGuid();
        var grain = await CreateFloorPlanAsync(orgId, siteId, floorPlanId);

        // Act
        await grain.AddTableAsync(tableId1);
        await grain.AddTableAsync(tableId2);

        // Assert
        var tableIds = await grain.GetTableIdsAsync();
        tableIds.Should().Contain(tableId1);
        tableIds.Should().Contain(tableId2);
    }

    // Given: a floor plan with two tables
    // When: one table is removed from the floor plan
    // Then: only the remaining table is tracked on the floor plan
    [Fact]
    public async Task RemoveTableAsync_ShouldRemoveTableFromFloorPlan()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var tableId1 = Guid.NewGuid();
        var tableId2 = Guid.NewGuid();
        var grain = await CreateFloorPlanAsync(orgId, siteId, floorPlanId);
        await grain.AddTableAsync(tableId1);
        await grain.AddTableAsync(tableId2);

        // Act
        await grain.RemoveTableAsync(tableId1);

        // Assert
        var tableIds = await grain.GetTableIdsAsync();
        tableIds.Should().NotContain(tableId1);
        tableIds.Should().Contain(tableId2);
    }

    // Given: an empty floor plan
    // When: "Bar Area" and "Dining Room" sections are added with color codes
    // Then: both sections are tracked with their names and colors
    [Fact]
    public async Task AddSectionAsync_ShouldAddSection()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = await CreateFloorPlanAsync(orgId, siteId, floorPlanId);

        // Act
        await grain.AddSectionAsync("Bar Area", "#FF5733");
        await grain.AddSectionAsync("Dining Room", "#33FF57");

        // Assert
        var state = await grain.GetStateAsync();
        state.Sections.Should().HaveCount(2);
        state.Sections[0].Name.Should().Be("Bar Area");
        state.Sections[0].Color.Should().Be("#FF5733");
        state.Sections[1].Name.Should().Be("Dining Room");
    }

    // Given: a floor plan with a "Bar Area" section
    // When: the "Bar Area" section is removed
    // Then: the floor plan has no sections
    [Fact]
    public async Task RemoveSectionAsync_ShouldRemoveSection()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = await CreateFloorPlanAsync(orgId, siteId, floorPlanId);
        await grain.AddSectionAsync("Bar Area", "#FF5733");
        var state = await grain.GetStateAsync();
        var sectionId = state.Sections[0].Id;

        // Act
        await grain.RemoveSectionAsync(sectionId);

        // Assert
        state = await grain.GetStateAsync();
        state.Sections.Should().BeEmpty();
    }

    // Given: an active floor plan
    // When: the floor plan is deactivated
    // Then: the floor plan is no longer active
    [Fact]
    public async Task DeactivateAsync_ShouldDeactivateFloorPlan()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = await CreateFloorPlanAsync(orgId, siteId, floorPlanId);

        // Act
        await grain.DeactivateAsync();

        // Assert
        (await grain.IsActiveAsync()).Should().BeFalse();
    }

    // Given: a deactivated floor plan
    // When: the floor plan is reactivated
    // Then: the floor plan becomes active again
    [Fact]
    public async Task ActivateAsync_ShouldActivateFloorPlan()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = await CreateFloorPlanAsync(orgId, siteId, floorPlanId);
        await grain.DeactivateAsync();

        // Act
        await grain.ActivateAsync();

        // Assert
        (await grain.IsActiveAsync()).Should().BeTrue();
    }

    // Given: a non-default floor plan
    // When: the floor plan is set as the default
    // Then: the floor plan is marked as the default floor plan
    [Fact]
    public async Task SetDefaultAsync_ShouldMarkAsDefault()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = await CreateFloorPlanAsync(orgId, siteId, floorPlanId);

        // Act
        await grain.SetDefaultAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.IsDefault.Should().BeTrue();
    }
}
