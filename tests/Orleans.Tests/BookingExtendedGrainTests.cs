using DarkVelocity.Orleans.Abstractions;
using DarkVelocity.Orleans.Abstractions.Grains;
using FluentAssertions;
using Orleans.TestingHost;
using Xunit;

namespace DarkVelocity.Orleans.Tests;

[Collection(TestClusterCollection.Name)]
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
            FloorPlanId: Guid.NewGuid(),
            TableNumber: "T1",
            Name: "Table 1",
            MinCapacity: 2,
            MaxCapacity: 4,
            Shape: "rectangle",
            PositionX: 100,
            PositionY: 200,
            Width: 100,
            Height: 80,
            Rotation: 0,
            IsCombinationAllowed: true,
            AssignmentPriority: 1,
            Notes: "Near window");

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.TableId.Should().Be(tableId);
        snapshot.TableNumber.Should().Be("T1");
        snapshot.MinCapacity.Should().Be(2);
        snapshot.MaxCapacity.Should().Be(4);
        snapshot.Status.Should().Be(TableStatus.Available);
        snapshot.IsActive.Should().BeTrue();
    }

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
            FloorPlanId: Guid.NewGuid(),
            TableNumber: "T2",
            Name: null,
            MinCapacity: 2,
            MaxCapacity: 2,
            Shape: "circle",
            PositionX: 0,
            PositionY: 0,
            Width: 60,
            Height: 60,
            Rotation: 0,
            IsCombinationAllowed: false,
            AssignmentPriority: 2,
            Notes: null));

        // Act
        await grain.SetStatusAsync(TableStatus.Occupied);
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.Status.Should().Be(TableStatus.Occupied);
    }

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
            FloorPlanId: Guid.NewGuid(),
            TableNumber: "T3",
            Name: null,
            MinCapacity: 2,
            MaxCapacity: 4,
            Shape: null,
            PositionX: 0,
            PositionY: 0,
            Width: 100,
            Height: 100,
            Rotation: 0,
            IsCombinationAllowed: true,
            AssignmentPriority: 3,
            Notes: null));

        // Act
        var availableInitially = await grain.IsAvailableAsync();
        await grain.SetStatusAsync(TableStatus.Occupied);
        var availableAfterOccupied = await grain.IsAvailableAsync();

        // Assert
        availableInitially.Should().BeTrue();
        availableAfterOccupied.Should().BeFalse();
    }

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
            FloorPlanId: Guid.NewGuid(),
            TableNumber: "T4",
            Name: null,
            MinCapacity: 2,
            MaxCapacity: 6,
            Shape: null,
            PositionX: 50,
            PositionY: 50,
            Width: 100,
            Height: 100,
            Rotation: 0,
            IsCombinationAllowed: true,
            AssignmentPriority: 1,
            Notes: null));

        // Act
        await grain.UpdatePositionAsync(200, 300, 45);
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.PositionX.Should().Be(200);
        snapshot.PositionY.Should().Be(300);
        snapshot.Rotation.Should().Be(45);
    }

    // ============================================================================
    // Floor Plan Grain Tests
    // ============================================================================

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
            Name: "Main Dining Room",
            Description: "First floor dining area",
            GridWidth: 1200,
            GridHeight: 800,
            BackgroundImageUrl: null,
            SortOrder: 1,
            DefaultTurnTimeMinutes: 90);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.FloorPlanId.Should().Be(floorPlanId);
        snapshot.Name.Should().Be("Main Dining Room");
        snapshot.GridWidth.Should().Be(1200);
        snapshot.GridHeight.Should().Be(800);
        snapshot.IsActive.Should().BeTrue();
        snapshot.TableCount.Should().Be(0);
    }

    [Fact]
    public async Task FloorPlanGrain_IncrementTableCount_TracksTableCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IFloorPlanGrain>(
            GrainKeys.FloorPlan(orgId, siteId, floorPlanId));

        await grain.CreateAsync(new CreateFloorPlanCommand(
            Name: "Test Floor",
            Description: null,
            GridWidth: 1000,
            GridHeight: 600,
            BackgroundImageUrl: null,
            SortOrder: 1,
            DefaultTurnTimeMinutes: 60));

        // Act
        await grain.IncrementTableCountAsync();
        await grain.IncrementTableCountAsync();
        await grain.IncrementTableCountAsync();

        var count = await grain.GetTableCountAsync();

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task FloorPlanGrain_DecrementTableCount_DecreasesCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var floorPlanId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IFloorPlanGrain>(
            GrainKeys.FloorPlan(orgId, siteId, floorPlanId));

        await grain.CreateAsync(new CreateFloorPlanCommand(
            Name: "Test Floor",
            Description: null,
            GridWidth: 1000,
            GridHeight: 600,
            BackgroundImageUrl: null,
            SortOrder: 1,
            DefaultTurnTimeMinutes: 60));

        await grain.IncrementTableCountAsync();
        await grain.IncrementTableCountAsync();

        // Act
        await grain.DecrementTableCountAsync();
        var count = await grain.GetTableCountAsync();

        // Assert
        count.Should().Be(1);
    }

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
            Name: "Original Name",
            Description: null,
            GridWidth: 1000,
            GridHeight: 600,
            BackgroundImageUrl: null,
            SortOrder: 1,
            DefaultTurnTimeMinutes: 60));

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateFloorPlanCommand(
            Name: "Updated Name",
            Description: "Now with description",
            GridWidth: 1200,
            GridHeight: null,
            BackgroundImageUrl: "https://example.com/image.png",
            SortOrder: 2,
            IsActive: null,
            DefaultTurnTimeMinutes: 90));

        // Assert
        snapshot.Name.Should().Be("Updated Name");
        snapshot.Description.Should().Be("Now with description");
        snapshot.GridWidth.Should().Be(1200);
        snapshot.GridHeight.Should().Be(600); // Unchanged
        snapshot.DefaultTurnTimeMinutes.Should().Be(90);
    }

    // ============================================================================
    // Booking Settings Grain Tests
    // ============================================================================

    [Fact]
    public async Task BookingSettingsGrain_Initialize_SetsDefaultValues()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));

        // Act
        await grain.InitializeAsync(siteId);
        var settings = await grain.GetSettingsAsync();

        // Assert
        settings.LocationId.Should().Be(siteId);
        settings.DefaultBookingDurationMinutes.Should().Be(90);
        settings.MinAdvanceBookingMinutes.Should().Be(30);
        settings.MaxAdvanceBookingDays.Should().Be(60);
        settings.AllowOnlineBookings.Should().BeTrue();
        settings.AllowWaitlist.Should().BeTrue();
    }

    [Fact]
    public async Task BookingSettingsGrain_Update_UpdatesSettings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));

        await grain.InitializeAsync(siteId);

        // Act
        var settings = await grain.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultBookingDurationMinutes: 120,
            MinAdvanceBookingMinutes: 60,
            MaxAdvanceBookingDays: 90,
            AllowOnlineBookings: null,
            RequireDeposit: true,
            DepositAmount: 25.00m,
            DepositPercentage: null,
            CancellationDeadlineMinutes: null,
            CancellationFeeAmount: null,
            CancellationFeePercentage: null,
            AllowWaitlist: false,
            MaxWaitlistSize: null,
            FirstServiceStart: null,
            FirstServiceEnd: null,
            SecondServiceStart: null,
            SecondServiceEnd: null,
            TurnTimeMinutes: null,
            BufferTimeMinutes: null,
            ConfirmationMessageTemplate: null,
            ReminderMessageTemplate: null));

        // Assert
        settings.DefaultBookingDurationMinutes.Should().Be(120);
        settings.RequireDeposit.Should().BeTrue();
        settings.DepositAmount.Should().Be(25.00m);
        settings.AllowWaitlist.Should().BeFalse();
    }

    [Fact]
    public async Task BookingSettingsGrain_CalculateDeposit_ReturnsFixedAmount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));

        await grain.InitializeAsync(siteId);
        await grain.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultBookingDurationMinutes: null,
            MinAdvanceBookingMinutes: null,
            MaxAdvanceBookingDays: null,
            AllowOnlineBookings: null,
            RequireDeposit: true,
            DepositAmount: 50.00m,
            DepositPercentage: null,
            CancellationDeadlineMinutes: null,
            CancellationFeeAmount: null,
            CancellationFeePercentage: null,
            AllowWaitlist: null,
            MaxWaitlistSize: null,
            FirstServiceStart: null,
            FirstServiceEnd: null,
            SecondServiceStart: null,
            SecondServiceEnd: null,
            TurnTimeMinutes: null,
            BufferTimeMinutes: null,
            ConfirmationMessageTemplate: null,
            ReminderMessageTemplate: null));

        // Act
        var deposit = await grain.CalculateDepositAsync(200.00m);

        // Assert
        deposit.Should().Be(50.00m);
    }

    [Fact]
    public async Task BookingSettingsGrain_CalculateDeposit_ReturnsPercentage()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));

        await grain.InitializeAsync(siteId);
        await grain.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultBookingDurationMinutes: null,
            MinAdvanceBookingMinutes: null,
            MaxAdvanceBookingDays: null,
            AllowOnlineBookings: null,
            RequireDeposit: true,
            DepositAmount: 0,
            DepositPercentage: 25m,
            CancellationDeadlineMinutes: null,
            CancellationFeeAmount: null,
            CancellationFeePercentage: null,
            AllowWaitlist: null,
            MaxWaitlistSize: null,
            FirstServiceStart: null,
            FirstServiceEnd: null,
            SecondServiceStart: null,
            SecondServiceEnd: null,
            TurnTimeMinutes: null,
            BufferTimeMinutes: null,
            ConfirmationMessageTemplate: null,
            ReminderMessageTemplate: null));

        // Act
        var deposit = await grain.CalculateDepositAsync(200.00m);

        // Assert
        deposit.Should().Be(50.00m); // 25% of 200
    }

    [Fact]
    public async Task BookingSettingsGrain_IsWithinBookingWindow_ValidatesCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));

        await grain.InitializeAsync(siteId);

        // Act
        var tooSoon = await grain.IsWithinBookingWindowAsync(DateTime.UtcNow.AddMinutes(15));
        var validTime = await grain.IsWithinBookingWindowAsync(DateTime.UtcNow.AddDays(7));
        var tooFar = await grain.IsWithinBookingWindowAsync(DateTime.UtcNow.AddDays(90));

        // Assert (default: min 30 min, max 60 days)
        tooSoon.Should().BeFalse();
        validTime.Should().BeTrue();
        tooFar.Should().BeFalse();
    }
}
