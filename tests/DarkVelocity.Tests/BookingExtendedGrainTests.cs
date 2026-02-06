using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;
using Orleans.TestingHost;
using Xunit;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class BookingExtendedGrainTests
{
    private readonly TestCluster _cluster;

    public BookingExtendedGrainTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    // ============================================================================
    // Table Grain Tests
    // ============================================================================

    // Given: no tables exist in the venue
    // When: a new rectangular table T1 is created with capacity 2-4
    // Then: the table is created with the correct number, capacity, and Available status
    [Fact]
    public async Task TableGrain_Create_CreatesTableSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITableGrain>(
            GrainKeys.Table(orgId, siteId, tableId));

        var command = new CreateTableCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Number: "T1",
            MinCapacity: 2,
            MaxCapacity: 4,
            Name: "Table 1",
            Shape: TableShape.Rectangle,
            FloorPlanId: Guid.NewGuid());

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Id.Should().Be(tableId);
        result.Number.Should().Be("T1");

        var state = await grain.GetStateAsync();
        state.MinCapacity.Should().Be(2);
        state.MaxCapacity.Should().Be(4);
        state.Status.Should().Be(TableStatus.Available);
    }

    // Given: an available round 2-top table
    // When: the table status is set to Occupied
    // Then: the table status changes to Occupied
    [Fact]
    public async Task TableGrain_SetStatus_ChangesStatusCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITableGrain>(
            GrainKeys.Table(orgId, siteId, tableId));

        await grain.CreateAsync(new CreateTableCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Number: "T2",
            MinCapacity: 2,
            MaxCapacity: 2,
            Name: null,
            Shape: TableShape.Round,
            FloorPlanId: Guid.NewGuid()));

        // Act
        await grain.SetStatusAsync(TableStatus.Occupied);
        var state = await grain.GetStateAsync();

        // Assert
        state.Status.Should().Be(TableStatus.Occupied);
    }

    // Given: a newly created available 4-top table
    // When: the table status changes to Occupied
    // Then: availability check returns true before occupation and false after
    [Fact]
    public async Task TableGrain_IsAvailable_ReturnsFalseWhenOccupied()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITableGrain>(
            GrainKeys.Table(orgId, siteId, tableId));

        await grain.CreateAsync(new CreateTableCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Number: "T3",
            MinCapacity: 2,
            MaxCapacity: 4,
            Name: null,
            Shape: TableShape.Square,
            FloorPlanId: Guid.NewGuid()));

        // Act
        var availableInitially = await grain.IsAvailableAsync();
        await grain.SetStatusAsync(TableStatus.Occupied);
        var availableAfterOccupied = await grain.IsAvailableAsync();

        // Assert
        availableInitially.Should().BeTrue();
        availableAfterOccupied.Should().BeFalse();
    }

    // Given: an existing 6-top table on the floor plan
    // When: the table's position is updated to coordinates (200, 300) with 45-degree rotation
    // Then: the table position reflects the new coordinates and rotation
    [Fact]
    public async Task TableGrain_UpdatePosition_UpdatesCoordinates()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITableGrain>(
            GrainKeys.Table(orgId, siteId, tableId));

        await grain.CreateAsync(new CreateTableCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Number: "T4",
            MinCapacity: 2,
            MaxCapacity: 6,
            Name: null,
            Shape: TableShape.Square,
            FloorPlanId: Guid.NewGuid()));

        // Act
        await grain.SetPositionAsync(new TablePosition { X = 200, Y = 300, Width = 100, Height = 100, Rotation = 45 });
        var state = await grain.GetStateAsync();

        // Assert
        state.Position!.X.Should().Be(200);
        state.Position.Y.Should().Be(300);
        state.Position.Rotation.Should().Be(45);
    }

    // ============================================================================
    // Floor Plan Grain Tests
    // ============================================================================

    // Given: a venue with no floor plans
    // When: a new "Main Dining Room" floor plan is created with specific dimensions
    // Then: the floor plan is created with correct name, dimensions, active status, and no tables
    [Fact]
    public async Task FloorPlanGrain_Create_CreatesFloorPlanSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IFloorPlanGrain>(
            GrainKeys.FloorPlan(orgId, siteId, floorPlanId));

        var command = new CreateFloorPlanCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Name: "Main Dining Room",
            IsDefault: false,
            Width: 1200,
            Height: 800);

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Id.Should().Be(floorPlanId);
        result.Name.Should().Be("Main Dining Room");

        var state = await grain.GetStateAsync();
        state.Width.Should().Be(1200);
        state.Height.Should().Be(800);
        state.IsActive.Should().BeTrue();
        state.TableIds.Should().BeEmpty();
    }

    // Given: an empty floor plan
    // When: three tables are added to the floor plan
    // Then: all three table IDs are tracked on the floor plan
    [Fact]
    public async Task FloorPlanGrain_AddTable_TracksTableIds()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IFloorPlanGrain>(
            GrainKeys.FloorPlan(orgId, siteId, floorPlanId));

        await grain.CreateAsync(new CreateFloorPlanCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Name: "Test Floor",
            IsDefault: false,
            Width: 1000,
            Height: 600));

        var tableId1 = Guid.NewGuid();
        var tableId2 = Guid.NewGuid();
        var tableId3 = Guid.NewGuid();

        // Act
        await grain.AddTableAsync(tableId1);
        await grain.AddTableAsync(tableId2);
        await grain.AddTableAsync(tableId3);

        var tableIds = await grain.GetTableIdsAsync();

        // Assert
        tableIds.Should().HaveCount(3);
        tableIds.Should().Contain(tableId1);
        tableIds.Should().Contain(tableId2);
        tableIds.Should().Contain(tableId3);
    }

    // Given: a floor plan with two tables
    // When: one table is removed
    // Then: the table count decreases and only the remaining table is tracked
    [Fact]
    public async Task FloorPlanGrain_RemoveTable_DecreasesCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IFloorPlanGrain>(
            GrainKeys.FloorPlan(orgId, siteId, floorPlanId));

        await grain.CreateAsync(new CreateFloorPlanCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Name: "Test Floor",
            IsDefault: false,
            Width: 1000,
            Height: 600));

        var tableId1 = Guid.NewGuid();
        var tableId2 = Guid.NewGuid();

        await grain.AddTableAsync(tableId1);
        await grain.AddTableAsync(tableId2);

        // Act
        await grain.RemoveTableAsync(tableId1);
        var tableIds = await grain.GetTableIdsAsync();

        // Assert
        tableIds.Should().HaveCount(1);
        tableIds.Should().NotContain(tableId1);
        tableIds.Should().Contain(tableId2);
    }

    // Given: a floor plan with original name and dimensions
    // When: the name, width, and background image are updated
    // Then: updated properties are changed and unchanged properties are preserved
    [Fact]
    public async Task FloorPlanGrain_Update_UpdatesProperties()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IFloorPlanGrain>(
            GrainKeys.FloorPlan(orgId, siteId, floorPlanId));

        await grain.CreateAsync(new CreateFloorPlanCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Name: "Original Name",
            IsDefault: false,
            Width: 1000,
            Height: 600));

        // Act
        await grain.UpdateAsync(new UpdateFloorPlanCommand(
            Name: "Updated Name",
            Width: 1200,
            Height: null,
            BackgroundImageUrl: "https://example.com/image.png",
            IsActive: null));

        var state = await grain.GetStateAsync();

        // Assert
        state.Name.Should().Be("Updated Name");
        state.BackgroundImageUrl.Should().Be("https://example.com/image.png");
        state.Width.Should().Be(1200);
        state.Height.Should().Be(600); // Unchanged
    }

    // ============================================================================
    // Booking Settings Grain Tests
    // ============================================================================

    // Given: a venue with no booking settings
    // When: booking settings are initialized
    // Then: default values are applied (90-minute duration, 30-day advance, max party 8)
    [Fact]
    public async Task BookingSettingsGrain_Initialize_SetsDefaultValues()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));

        // Act
        await grain.InitializeAsync(orgId, siteId);
        var settings = await grain.GetStateAsync();

        // Assert
        settings.SiteId.Should().Be(siteId);
        settings.OrganizationId.Should().Be(orgId);
        settings.DefaultDuration.Should().Be(TimeSpan.FromMinutes(90));
        settings.AdvanceBookingDays.Should().Be(30);
        settings.MaxPartySizeOnline.Should().Be(8);
    }

    // Given: a venue with default booking settings
    // When: the duration, party size, advance days, and deposit settings are updated
    // Then: all updated booking settings are persisted correctly
    [Fact]
    public async Task BookingSettingsGrain_Update_UpdatesSettings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));

        await grain.InitializeAsync(orgId, siteId);

        // Act
        await grain.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: null,
            DefaultCloseTime: null,
            DefaultDuration: TimeSpan.FromMinutes(120),
            SlotInterval: null,
            MaxPartySizeOnline: 10,
            MaxBookingsPerSlot: null,
            AdvanceBookingDays: 45,
            RequireDeposit: true,
            DepositAmount: 25.00m));

        var settings = await grain.GetStateAsync();

        // Assert
        settings.DefaultDuration.Should().Be(TimeSpan.FromMinutes(120));
        settings.RequireDeposit.Should().BeTrue();
        settings.DepositAmount.Should().Be(25.00m);
        settings.MaxPartySizeOnline.Should().Be(10);
        settings.AdvanceBookingDays.Should().Be(45);
    }

    // Given: a venue with booking settings initialized
    // When: a $50 deposit requirement is enabled
    // Then: the deposit requirement and amount are persisted
    [Fact]
    public async Task BookingSettingsGrain_Update_SetsDepositAmount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));

        await grain.InitializeAsync(orgId, siteId);
        await grain.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: null,
            DefaultCloseTime: null,
            DefaultDuration: null,
            SlotInterval: null,
            MaxPartySizeOnline: null,
            MaxBookingsPerSlot: null,
            AdvanceBookingDays: null,
            RequireDeposit: true,
            DepositAmount: 50.00m));

        // Act
        var settings = await grain.GetStateAsync();

        // Assert
        settings.RequireDeposit.Should().BeTrue();
        settings.DepositAmount.Should().Be(50.00m);
    }

    // Given: a venue with booking settings
    // When: a specific date is blocked for reservations
    // Then: the date is blocked and adjacent dates remain unblocked
    [Fact]
    public async Task BookingSettingsGrain_BlockDate_BlocksDate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));

        await grain.InitializeAsync(orgId, siteId);
        var dateToBlock = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

        // Act
        await grain.BlockDateAsync(dateToBlock);
        var isBlocked = await grain.IsDateBlockedAsync(dateToBlock);
        var isNotBlocked = await grain.IsDateBlockedAsync(dateToBlock.AddDays(1));

        // Assert
        isBlocked.Should().BeTrue();
        isNotBlocked.Should().BeFalse();
    }

    // Given: a venue open from 11am to 10pm
    // When: a 6pm slot availability is checked for a party of 4
    // Then: the slot is available because it falls within operating hours and party size limits
    [Fact]
    public async Task BookingSettingsGrain_IsSlotAvailable_ValidatesCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));

        await grain.InitializeAsync(orgId, siteId);

        var validDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var validTime = new TimeOnly(18, 0); // 6 PM, within default operating hours (11am-10pm)

        // Act
        var isAvailable = await grain.IsSlotAvailableAsync(validDate, validTime, partySize: 4);

        // Assert
        isAvailable.Should().BeTrue();
    }
}
