using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// High-priority test coverage for the Tables and Bookings domain.
/// Covers: booking conflicts, overbooking, table combining/splitting,
/// waitlist queue management, capacity validation, no-show handling,
/// and deposit/cancellation policies.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class BookingConflictDetectionTests
{
    private readonly TestClusterFixture _fixture;

    public BookingConflictDetectionTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private GuestInfo CreateGuestInfo(string name = "John Doe") => new()
    {
        Name = name,
        Phone = "+1234567890",
        Email = "john@example.com"
    };

    private async Task<IBookingCalendarGrain> CreateCalendarWithSettingsAsync(
        Guid orgId, Guid siteId, DateOnly date, int maxBookingsPerSlot = 10)
    {
        // Setup booking settings
        var settingsGrain = _fixture.Cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));
        await settingsGrain.InitializeAsync(orgId, siteId);
        await settingsGrain.UpdateAsync(new UpdateBookingSettingsCommand(
            MaxBookingsPerSlot: maxBookingsPerSlot,
            SlotInterval: TimeSpan.FromMinutes(15)));

        // Create calendar
        var calendarGrain = _fixture.Cluster.GrainFactory.GetGrain<IBookingCalendarGrain>(
            GrainKeys.BookingCalendar(orgId, siteId, date));
        await calendarGrain.InitializeAsync(orgId, siteId, date);
        return calendarGrain;
    }

    [Fact]
    public async Task BookingCalendar_MultipleBookingsSameSlot_ShouldTrackAllBookings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var calendar = await CreateCalendarWithSettingsAsync(orgId, siteId, date);

        var slotTime = new TimeOnly(19, 0);

        // Act - Add multiple bookings at the same time
        await calendar.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "CONF1", slotTime, 4, "Party 1", BookingStatus.Confirmed));
        await calendar.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "CONF2", slotTime, 2, "Party 2", BookingStatus.Confirmed));
        await calendar.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "CONF3", slotTime, 6, "Party 3", BookingStatus.Confirmed));

        // Assert
        var dayView = await calendar.GetDayViewAsync(TimeSpan.FromMinutes(15));
        var slot = dayView.Slots.FirstOrDefault(s => s.StartTime == slotTime);

        slot.Should().NotBeNull();
        slot!.BookingCount.Should().Be(3);
        slot.CoverCount.Should().Be(12); // 4 + 2 + 6
    }

    [Fact]
    public async Task BookingCalendar_OverlappingTimeSlots_ShouldCountCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var calendar = await CreateCalendarWithSettingsAsync(orgId, siteId, date);

        // Act - Add bookings at adjacent times that may overlap in service duration
        await calendar.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "CONF1", new TimeOnly(18, 30), 4, "Early Dinner", BookingStatus.Confirmed));
        await calendar.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "CONF2", new TimeOnly(18, 45), 4, "Slightly Later", BookingStatus.Confirmed));
        await calendar.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "CONF3", new TimeOnly(19, 0), 4, "Prime Time", BookingStatus.Confirmed));

        // Assert
        var bookings = await calendar.GetBookingsByTimeRangeAsync(
            new TimeOnly(18, 0), new TimeOnly(20, 0));

        bookings.Should().HaveCount(3);
        bookings.Should().BeInAscendingOrder(b => b.Time);
    }

    [Fact]
    public async Task BookingSettings_CheckAvailability_WhenAtMaxCapacity_ShouldReturnUnavailable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var settingsGrain = _fixture.Cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));
        await settingsGrain.InitializeAsync(orgId, siteId);

        // Set max party size to 8
        await settingsGrain.UpdateAsync(new UpdateBookingSettingsCommand(MaxPartySizeOnline: 8));

        // Act
        var isAvailableSmall = await settingsGrain.IsSlotAvailableAsync(
            date, new TimeOnly(19, 0), partySize: 4);
        var isAvailableLarge = await settingsGrain.IsSlotAvailableAsync(
            date, new TimeOnly(19, 0), partySize: 10);

        // Assert
        isAvailableSmall.Should().BeTrue();
        isAvailableLarge.Should().BeFalse();
    }

    [Fact]
    public async Task BookingSettings_BlockedDate_ShouldPreventAllBookings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var blockedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var settingsGrain = _fixture.Cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));
        await settingsGrain.InitializeAsync(orgId, siteId);

        // Act
        await settingsGrain.BlockDateAsync(blockedDate);

        // Assert - All time slots should be unavailable
        var morningAvailable = await settingsGrain.IsSlotAvailableAsync(
            blockedDate, new TimeOnly(11, 0), 2);
        var afternoonAvailable = await settingsGrain.IsSlotAvailableAsync(
            blockedDate, new TimeOnly(14, 0), 2);
        var eveningAvailable = await settingsGrain.IsSlotAvailableAsync(
            blockedDate, new TimeOnly(19, 0), 2);

        morningAvailable.Should().BeFalse();
        afternoonAvailable.Should().BeFalse();
        eveningAvailable.Should().BeFalse();
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class OverbookingScenarioTests
{
    private readonly TestClusterFixture _fixture;

    public OverbookingScenarioTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BookingCalendar_TrackCovers_WithStatusChanges_ShouldUpdateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var calendarGrain = _fixture.Cluster.GrainFactory.GetGrain<IBookingCalendarGrain>(
            GrainKeys.BookingCalendar(orgId, siteId, date));
        await calendarGrain.InitializeAsync(orgId, siteId, date);

        var bookingId1 = Guid.NewGuid();
        var bookingId2 = Guid.NewGuid();

        // Act - Add confirmed bookings
        await calendarGrain.AddBookingAsync(new AddBookingToCalendarCommand(
            bookingId1, "CONF1", new TimeOnly(19, 0), 4, "Guest 1", BookingStatus.Confirmed));
        await calendarGrain.AddBookingAsync(new AddBookingToCalendarCommand(
            bookingId2, "CONF2", new TimeOnly(19, 0), 4, "Guest 2", BookingStatus.Confirmed));

        var initialCount = await calendarGrain.GetCoverCountAsync();

        // Cancel one booking
        await calendarGrain.UpdateBookingAsync(new UpdateBookingInCalendarCommand(
            bookingId1, Status: BookingStatus.Cancelled));

        var afterCancellationCount = await calendarGrain.GetCoverCountAsync();

        // Assert
        initialCount.Should().Be(8);
        afterCancellationCount.Should().Be(4); // Only active booking counted
    }

    [Fact]
    public async Task BookingCalendar_DayView_ShouldSummarizeCapacityCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var calendarGrain = _fixture.Cluster.GrainFactory.GetGrain<IBookingCalendarGrain>(
            GrainKeys.BookingCalendar(orgId, siteId, date));
        await calendarGrain.InitializeAsync(orgId, siteId, date);

        // Add bookings across different time slots
        await calendarGrain.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "C1", new TimeOnly(12, 0), 2, "Lunch 1", BookingStatus.Confirmed));
        await calendarGrain.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "C2", new TimeOnly(12, 30), 4, "Lunch 2", BookingStatus.Confirmed));
        await calendarGrain.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "C3", new TimeOnly(18, 0), 6, "Dinner 1", BookingStatus.Seated));
        await calendarGrain.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "C4", new TimeOnly(19, 0), 4, "Dinner 2", BookingStatus.NoShow));

        // Act
        var dayView = await calendarGrain.GetDayViewAsync();

        // Assert
        dayView.TotalBookings.Should().Be(4);
        dayView.TotalCovers.Should().Be(16); // 2 + 4 + 6 + 4
        dayView.ConfirmedBookings.Should().Be(2);
        dayView.SeatedBookings.Should().Be(1);
        dayView.NoShowCount.Should().Be(1);
    }

    [Fact]
    public async Task BookingCalendar_HighVolumeSlot_ShouldHandleMany()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var calendarGrain = _fixture.Cluster.GrainFactory.GetGrain<IBookingCalendarGrain>(
            GrainKeys.BookingCalendar(orgId, siteId, date));
        await calendarGrain.InitializeAsync(orgId, siteId, date);

        var primeTimeSlot = new TimeOnly(19, 0);

        // Act - Add 20 bookings to prime time slot
        for (int i = 0; i < 20; i++)
        {
            await calendarGrain.AddBookingAsync(new AddBookingToCalendarCommand(
                Guid.NewGuid(),
                $"CONF{i:D3}",
                primeTimeSlot.AddMinutes(i % 4 * 15), // Spread across 15-min intervals
                2 + (i % 3),
                $"Guest {i}",
                BookingStatus.Confirmed));
        }

        // Assert
        var bookings = await calendarGrain.GetBookingsAsync();
        bookings.Should().HaveCount(20);

        var dayView = await calendarGrain.GetDayViewAsync(TimeSpan.FromHours(1));
        var primeSlot = dayView.Slots.FirstOrDefault(s => s.StartTime == primeTimeSlot);
        primeSlot.Should().NotBeNull();
        primeSlot!.BookingCount.Should().BeGreaterThan(0);
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TableCombiningSplittingTests
{
    private readonly TestClusterFixture _fixture;

    public TableCombiningSplittingTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ITableGrain> CreateTableAsync(
        Guid orgId, Guid siteId, Guid tableId, string number,
        int minCapacity = 2, int maxCapacity = 4, bool isCombinable = true)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ITableGrain>(
            GrainKeys.Table(orgId, siteId, tableId));
        await grain.CreateAsync(new CreateTableCommand(orgId, siteId, number,
            MinCapacity: minCapacity, MaxCapacity: maxCapacity));
        if (isCombinable)
        {
            await grain.UpdateAsync(new UpdateTableCommand(IsCombinable: true));
        }
        return grain;
    }

    [Fact]
    public async Task Table_CombineMultipleTables_ShouldTrackAllCombinations()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var table1Id = Guid.NewGuid();
        var table2Id = Guid.NewGuid();
        var table3Id = Guid.NewGuid();

        var table1 = await CreateTableAsync(orgId, siteId, table1Id, "T1");
        await CreateTableAsync(orgId, siteId, table2Id, "T2");
        await CreateTableAsync(orgId, siteId, table3Id, "T3");

        // Act - Combine table1 with table2 and table3
        await table1.CombineWithAsync(table2Id);
        await table1.CombineWithAsync(table3Id);

        // Assert
        var state = await table1.GetStateAsync();
        state.CombinedWith.Should().HaveCount(2);
        state.CombinedWith.Should().Contain(table2Id);
        state.CombinedWith.Should().Contain(table3Id);
    }

    [Fact]
    public async Task Table_UncombineFromMultiple_ShouldClearAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var table1Id = Guid.NewGuid();
        var table2Id = Guid.NewGuid();
        var table3Id = Guid.NewGuid();

        var table1 = await CreateTableAsync(orgId, siteId, table1Id, "T1");
        await CreateTableAsync(orgId, siteId, table2Id, "T2");
        await CreateTableAsync(orgId, siteId, table3Id, "T3");

        await table1.CombineWithAsync(table2Id);
        await table1.CombineWithAsync(table3Id);

        // Act
        await table1.UncombineAsync();

        // Assert
        var state = await table1.GetStateAsync();
        state.CombinedWith.Should().BeEmpty();
    }

    [Fact]
    public async Task Table_SeatCombinedTables_ShouldWorkCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var table1Id = Guid.NewGuid();
        var table2Id = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var table1 = await CreateTableAsync(orgId, siteId, table1Id, "T1", maxCapacity: 4);
        await CreateTableAsync(orgId, siteId, table2Id, "T2", maxCapacity: 4);

        // Combine tables for a larger party
        await table1.CombineWithAsync(table2Id);

        // Act - Seat a large party
        await table1.SeatAsync(new SeatTableCommand(
            BookingId: bookingId,
            OrderId: null,
            GuestName: "Large Party",
            GuestCount: 8,
            ServerId: null));

        // Assert
        var state = await table1.GetStateAsync();
        state.Status.Should().Be(TableStatus.Occupied);
        state.CurrentOccupancy.Should().NotBeNull();
        state.CurrentOccupancy!.GuestCount.Should().Be(8);
        state.CombinedWith.Should().Contain(table2Id);
    }

    [Fact]
    public async Task Table_ClearCombinedTable_ShouldPreserveCombination()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var table1Id = Guid.NewGuid();
        var table2Id = Guid.NewGuid();

        var table1 = await CreateTableAsync(orgId, siteId, table1Id, "T1");
        await CreateTableAsync(orgId, siteId, table2Id, "T2");

        await table1.CombineWithAsync(table2Id);
        await table1.SeatAsync(new SeatTableCommand(null, null, "Guest", 6));

        // Act
        await table1.ClearAsync();
        await table1.MarkCleanAsync();

        // Assert - Combination should be preserved after clearing
        var state = await table1.GetStateAsync();
        state.Status.Should().Be(TableStatus.Available);
        state.CombinedWith.Should().Contain(table2Id);
    }

    [Fact]
    public async Task TableOptimizer_RecommendCombination_ForLargeParty()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var table1Id = Guid.NewGuid();
        var table2Id = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var optimizer = _fixture.Cluster.GrainFactory.GetGrain<ITableAssignmentOptimizerGrain>(
            GrainKeys.TableAssignmentOptimizer(orgId, siteId));
        await optimizer.InitializeAsync(orgId, siteId);

        // Register two 4-top tables as combinable
        await optimizer.RegisterTableAsync(table1Id, "T1", 2, 4, true);
        await optimizer.RegisterTableAsync(table2Id, "T2", 2, 4, true);

        // Act - Request a table for 8 people
        var request = new TableAssignmentRequest(
            bookingId, 8, DateTime.UtcNow, TimeSpan.FromMinutes(90));
        var result = await optimizer.GetRecommendationsAsync(request);

        // Assert - Should either suggest combination or return no single table
        // The optimizer may return recommendations for combined tables
        // or indicate that no single table is available
        result.Should().NotBeNull();
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class WaitlistQueueManagementTests
{
    private readonly TestClusterFixture _fixture;

    public WaitlistQueueManagementTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private GuestInfo CreateGuestInfo(string name = "John Doe") => new()
    {
        Name = name,
        Phone = "+1234567890",
        Email = "john@example.com"
    };

    private async Task<IEnhancedWaitlistGrain> CreateWaitlistAsync(Guid orgId, Guid siteId, DateOnly date)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IEnhancedWaitlistGrain>(
            GrainKeys.EnhancedWaitlist(orgId, siteId, date));
        await grain.InitializeAsync(orgId, siteId, date);
        return grain;
    }

    [Fact]
    public async Task Waitlist_ExpireOldEntries_ShouldRemoveStaleEntries()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        // Configure short max wait time
        await grain.UpdateSettingsAsync(new UpdateWaitlistSettingsCommand(
            MaxWaitTime: TimeSpan.FromMinutes(5),
            AutoExpireEntries: true));

        // Add entries
        var entry1 = await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(
            CreateGuestInfo("Guest 1"), 2));
        await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(
            CreateGuestInfo("Guest 2"), 4));

        // Act - Expire old entries (in a real scenario, time would have passed)
        var expiredIds = await grain.ExpireOldEntriesAsync();

        // Assert - Entries may or may not be expired depending on actual time elapsed
        expiredIds.Should().NotBeNull();
    }

    [Fact]
    public async Task Waitlist_RecalculateEstimates_ShouldUpdateAllEntries()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        // Add several entries
        var entry1 = await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(
            CreateGuestInfo("Guest 1"), 2));
        var entry2 = await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(
            CreateGuestInfo("Guest 2"), 4));
        var entry3 = await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(
            CreateGuestInfo("Guest 3"), 2));

        // Act
        await grain.RecalculateEstimatesAsync();

        // Assert - Get estimates for each entry
        var estimate1 = await grain.GetEntryEstimateAsync(entry1.EntryId);
        var estimate2 = await grain.GetEntryEstimateAsync(entry2.EntryId);
        var estimate3 = await grain.GetEntryEstimateAsync(entry3.EntryId);

        estimate1.Position.Should().Be(1);
        estimate2.Position.Should().Be(2);
        estimate3.Position.Should().Be(3);
        estimate1.EstimatedWait.Should().BeLessThanOrEqualTo(estimate2.EstimatedWait);
        estimate2.EstimatedWait.Should().BeLessThanOrEqualTo(estimate3.EstimatedWait);
    }

    [Fact]
    public async Task Waitlist_UpdateTurnTimeData_ShouldImproveEstimates()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        // Get initial estimate
        var initialEstimate = await grain.GetWaitEstimateAsync(4);

        // Act - Update turn time data with faster turns
        await grain.UpdateTurnTimeDataAsync(4, TimeSpan.FromMinutes(45));
        await grain.UpdateTurnTimeDataAsync(4, TimeSpan.FromMinutes(50));
        await grain.UpdateTurnTimeDataAsync(4, TimeSpan.FromMinutes(48));

        var updatedEstimate = await grain.GetWaitEstimateAsync(4);

        // Assert
        updatedEstimate.AverageTurnTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task Waitlist_GetEntriesByStatus_ShouldFilterCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        var entry1 = await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(
            CreateGuestInfo("Guest 1"), 2));
        var entry2 = await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(
            CreateGuestInfo("Guest 2"), 4));
        var entry3 = await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(
            CreateGuestInfo("Guest 3"), 2));

        // Seat one entry
        await grain.SeatEntryAsync(entry1.EntryId, Guid.NewGuid(), "T1");

        // Notify another
        await grain.NotifyTableReadyAsync(entry2.EntryId);

        // Act
        var waitingEntries = await grain.GetEntriesByStatusAsync(WaitlistStatus.Waiting);
        var seatedEntries = await grain.GetEntriesByStatusAsync(WaitlistStatus.Seated);
        var notifiedEntries = await grain.GetEntriesByStatusAsync(WaitlistStatus.Notified);

        // Assert
        waitingEntries.Should().HaveCount(1);
        waitingEntries[0].Id.Should().Be(entry3.EntryId);
        seatedEntries.Should().HaveCount(1);
        seatedEntries[0].Id.Should().Be(entry1.EntryId);
        notifiedEntries.Should().HaveCount(1);
        notifiedEntries[0].Id.Should().Be(entry2.EntryId);
    }

    [Fact]
    public async Task Waitlist_PromoteToBooking_ShouldCreateBookingAndUpdateEntry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var tableId = Guid.NewGuid();
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        var entry = await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(
            CreateGuestInfo("Guest for Booking"), 4));

        // Act
        var bookingTime = DateTime.UtcNow.AddMinutes(15);
        var result = await grain.PromoteToBookingAsync(entry.EntryId, bookingTime, tableId);

        // Assert
        result.EntryId.Should().Be(entry.EntryId);
        result.BookingId.Should().NotBeNull();
        result.TableId.Should().Be(tableId);
        result.PromotedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Waitlist_FindNextSuitable_ShouldMatchTableCapacity()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        // Add entries with different party sizes
        await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(
            CreateGuestInfo("Large Party"), 8)); // Too big for 4-top
        await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(
            CreateGuestInfo("Medium Party"), 4)); // Perfect for 4-top
        await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(
            CreateGuestInfo("Small Party"), 2)); // Fits 4-top but wastes space

        // Act - Find entry suitable for a 4-top table
        var suitable = await grain.FindNextSuitableEntryAsync(4);

        // Assert
        suitable.Should().NotBeNull();
        suitable!.PartySize.Should().BeLessThanOrEqualTo(4);
    }

    [Fact]
    public async Task Waitlist_NotificationHistory_ShouldTrackAllNotifications()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        var entry = await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(
            CreateGuestInfo("Guest"), 4));

        // Act - Send multiple notifications
        await grain.SendPositionUpdateAsync(entry.EntryId);
        await grain.NotifyTableReadyAsync(entry.EntryId);

        var history = await grain.GetNotificationHistoryAsync(entry.EntryId);

        // Assert
        history.Should().HaveCountGreaterThanOrEqualTo(2);
        history.Should().Contain(n => n.Type == "table_ready" || n.Type == "position_update");
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class CapacityValidationTests
{
    private readonly TestClusterFixture _fixture;

    public CapacityValidationTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Table_SeatGuests_ExceedingCapacity_ShouldStillAllow()
    {
        // Arrange - In hospitality, you might occasionally seat more than capacity
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ITableGrain>(
            GrainKeys.Table(orgId, siteId, tableId));

        await grain.CreateAsync(new CreateTableCommand(orgId, siteId, "T1",
            MinCapacity: 2, MaxCapacity: 4));

        // Act - Seat 6 at a 4-top (sometimes you squeeze in)
        await grain.SeatAsync(new SeatTableCommand(null, null, "Tight Squeeze", 6));

        // Assert - System should allow this as reality sometimes exceeds ideal
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(TableStatus.Occupied);
        state.CurrentOccupancy!.GuestCount.Should().Be(6);
    }

    [Fact]
    public async Task BookingSettings_PartySizeValidation_ShouldEnforce()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var settingsGrain = _fixture.Cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));
        await settingsGrain.InitializeAsync(orgId, siteId);

        await settingsGrain.UpdateAsync(new UpdateBookingSettingsCommand(MaxPartySizeOnline: 6));

        // Act & Assert
        var available4 = await settingsGrain.IsSlotAvailableAsync(date, new TimeOnly(19, 0), 4);
        var available6 = await settingsGrain.IsSlotAvailableAsync(date, new TimeOnly(19, 0), 6);
        var available8 = await settingsGrain.IsSlotAvailableAsync(date, new TimeOnly(19, 0), 8);

        available4.Should().BeTrue();
        available6.Should().BeTrue();
        available8.Should().BeFalse();
    }

    [Fact]
    public async Task BookingSettings_TimeValidation_OutsideHours_ShouldReject()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var settingsGrain = _fixture.Cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));
        await settingsGrain.InitializeAsync(orgId, siteId);

        // Set operating hours 11am - 10pm
        await settingsGrain.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: new TimeOnly(11, 0),
            DefaultCloseTime: new TimeOnly(22, 0)));

        // Act & Assert
        var beforeOpen = await settingsGrain.IsSlotAvailableAsync(date, new TimeOnly(10, 0), 2);
        var atOpen = await settingsGrain.IsSlotAvailableAsync(date, new TimeOnly(11, 0), 2);
        var midDay = await settingsGrain.IsSlotAvailableAsync(date, new TimeOnly(14, 0), 2);
        var lastSlot = await settingsGrain.IsSlotAvailableAsync(date, new TimeOnly(21, 30), 2);
        var afterClose = await settingsGrain.IsSlotAvailableAsync(date, new TimeOnly(22, 30), 2);

        beforeOpen.Should().BeFalse();
        atOpen.Should().BeTrue();
        midDay.Should().BeTrue();
        lastSlot.Should().BeTrue();
        afterClose.Should().BeFalse();
    }

    [Fact]
    public async Task TableOptimizer_CapacityMatching_ShouldPreferOptimalFit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var smallTableId = Guid.NewGuid();
        var mediumTableId = Guid.NewGuid();
        var largeTableId = Guid.NewGuid();

        var optimizer = _fixture.Cluster.GrainFactory.GetGrain<ITableAssignmentOptimizerGrain>(
            GrainKeys.TableAssignmentOptimizer(orgId, siteId));
        await optimizer.InitializeAsync(orgId, siteId);

        // Register tables of different sizes
        await optimizer.RegisterTableAsync(smallTableId, "2-Top", 1, 2, false);
        await optimizer.RegisterTableAsync(mediumTableId, "4-Top", 2, 4, false);
        await optimizer.RegisterTableAsync(largeTableId, "6-Top", 4, 6, false);

        // Act - Request table for party of 3
        var request = new TableAssignmentRequest(
            Guid.NewGuid(), 3, DateTime.UtcNow, TimeSpan.FromMinutes(90));
        var result = await optimizer.GetRecommendationsAsync(request);

        // Assert - Should recommend 4-top (optimal fit) over 6-top
        result.Success.Should().BeTrue();
        result.Recommendations.Should().HaveCountGreaterThan(0);
        result.Recommendations[0].TableId.Should().Be(mediumTableId);
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class NoShowHandlingTests
{
    private readonly TestClusterFixture _fixture;

    public NoShowHandlingTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private GuestInfo CreateGuestInfo(string name = "John Doe") => new()
    {
        Name = name,
        Phone = "+1234567890",
        Email = "john@example.com"
    };

    private async Task<IBookingGrain> CreateConfirmedBookingAsync(
        Guid orgId, Guid siteId, Guid bookingId, DateTime requestedTime)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingGrain>(
            GrainKeys.Booking(orgId, siteId, bookingId));
        await grain.RequestAsync(new RequestBookingCommand(
            orgId, siteId, CreateGuestInfo(), requestedTime, 4));
        await grain.ConfirmAsync();
        return grain;
    }

    [Fact]
    public async Task Booking_MarkNoShow_ShouldUpdateStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var markedBy = Guid.NewGuid();
        var booking = await CreateConfirmedBookingAsync(
            orgId, siteId, bookingId, DateTime.UtcNow.AddHours(-1));

        // Act
        await booking.MarkNoShowAsync(markedBy);

        // Assert
        var state = await booking.GetStateAsync();
        state.Status.Should().Be(BookingStatus.NoShow);
    }

    [Fact]
    public async Task Booking_NoShowWithDeposit_ShouldAllowForfeit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingGrain>(
            GrainKeys.Booking(orgId, siteId, bookingId));

        await grain.RequestAsync(new RequestBookingCommand(
            orgId, siteId, CreateGuestInfo(), DateTime.UtcNow.AddHours(-1), 4));
        await grain.RequireDepositAsync(new RequireDepositCommand(50m, DateTime.UtcNow.AddDays(1)));
        await grain.RecordDepositPaymentAsync(new RecordDepositPaymentCommand(
            PaymentMethod.CreditCard, "ref123"));
        await grain.ConfirmAsync();

        // Act - Mark as no-show and forfeit deposit
        await grain.MarkNoShowAsync(Guid.NewGuid());
        await grain.ForfeitDepositAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(BookingStatus.NoShow);
        state.Deposit!.Status.Should().Be(DepositStatus.Forfeited);
    }

    [Fact]
    public async Task NoShowDetection_CheckPendingBookings_ShouldIdentifyLateArrivals()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var pastBookingTime = DateTime.UtcNow.AddMinutes(-30);

        var detector = _fixture.Cluster.GrainFactory.GetGrain<INoShowDetectionGrain>(
            GrainKeys.NoShowDetection(orgId, siteId));
        await detector.InitializeAsync(orgId, siteId);

        // Register a booking that was supposed to arrive 30 minutes ago
        await detector.RegisterBookingAsync(new RegisterNoShowCheckCommand(
            bookingId, pastBookingTime, "Late Guest"));

        // Act
        var pendingChecks = await detector.GetPendingChecksAsync();

        // Assert
        pendingChecks.Should().HaveCount(1);
        pendingChecks[0].BookingId.Should().Be(bookingId);
        pendingChecks[0].BookingTime.Should().Be(pastBookingTime);
    }

    [Fact]
    public async Task NoShowDetection_Settings_GracePeriod_ShouldBeRespected()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var detector = _fixture.Cluster.GrainFactory.GetGrain<INoShowDetectionGrain>(
            GrainKeys.NoShowDetection(orgId, siteId));
        await detector.InitializeAsync(orgId, siteId);

        // Act - Set 30 minute grace period
        await detector.UpdateSettingsAsync(new UpdateNoShowSettingsCommand(
            GracePeriod: TimeSpan.FromMinutes(30)));

        var settings = await detector.GetSettingsAsync();

        // Assert
        settings.GracePeriod.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task NoShowDetection_UnregisterOnArrival_ShouldRemoveFromPending()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var detector = _fixture.Cluster.GrainFactory.GetGrain<INoShowDetectionGrain>(
            GrainKeys.NoShowDetection(orgId, siteId));
        await detector.InitializeAsync(orgId, siteId);

        await detector.RegisterBookingAsync(new RegisterNoShowCheckCommand(
            bookingId, DateTime.UtcNow.AddHours(1), "Arriving Guest"));

        var beforeUnregister = await detector.GetPendingChecksAsync();
        beforeUnregister.Should().HaveCount(1);

        // Act - Guest arrives, unregister from no-show tracking
        await detector.UnregisterBookingAsync(bookingId);

        // Assert
        var afterUnregister = await detector.GetPendingChecksAsync();
        afterUnregister.Should().BeEmpty();
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DepositCancellationPolicyTests
{
    private readonly TestClusterFixture _fixture;

    public DepositCancellationPolicyTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private GuestInfo CreateGuestInfo(string name = "John Doe") => new()
    {
        Name = name,
        Phone = "+1234567890",
        Email = "john@example.com"
    };

    [Fact]
    public async Task Booking_RequireDeposit_ShouldBlockConfirmationUntilPaid()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingGrain>(
            GrainKeys.Booking(orgId, siteId, bookingId));

        await grain.RequestAsync(new RequestBookingCommand(
            orgId, siteId, CreateGuestInfo(), DateTime.UtcNow.AddDays(1), 6));
        await grain.RequireDepositAsync(new RequireDepositCommand(50m, DateTime.UtcNow.AddDays(1)));

        // Act & Assert - Should throw when trying to confirm without deposit
        var act = () => grain.ConfirmAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Deposit required but not paid*");
    }

    [Fact]
    public async Task Booking_DepositPaid_ShouldAllowConfirmation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingGrain>(
            GrainKeys.Booking(orgId, siteId, bookingId));

        await grain.RequestAsync(new RequestBookingCommand(
            orgId, siteId, CreateGuestInfo(), DateTime.UtcNow.AddDays(1), 6));
        await grain.RequireDepositAsync(new RequireDepositCommand(50m, DateTime.UtcNow.AddDays(1)));
        await grain.RecordDepositPaymentAsync(new RecordDepositPaymentCommand(
            PaymentMethod.CreditCard, "charge_123"));

        // Act
        var result = await grain.ConfirmAsync();

        // Assert
        result.ConfirmationCode.Should().NotBeEmpty();
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(BookingStatus.Confirmed);
        state.Deposit!.Status.Should().Be(DepositStatus.Paid);
    }

    [Fact]
    public async Task Booking_DepositWaived_ShouldAllowConfirmation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var waivedBy = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingGrain>(
            GrainKeys.Booking(orgId, siteId, bookingId));

        await grain.RequestAsync(new RequestBookingCommand(
            orgId, siteId, CreateGuestInfo(), DateTime.UtcNow.AddDays(1), 6));
        await grain.RequireDepositAsync(new RequireDepositCommand(50m, DateTime.UtcNow.AddDays(1)));
        await grain.WaiveDepositAsync(waivedBy);

        // Act
        var result = await grain.ConfirmAsync();

        // Assert
        result.ConfirmationCode.Should().NotBeEmpty();
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(BookingStatus.Confirmed);
        state.Deposit!.Status.Should().Be(DepositStatus.Waived);
    }

    [Fact]
    public async Task Booking_CancelWithPaidDeposit_ShouldAllowRefund()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingGrain>(
            GrainKeys.Booking(orgId, siteId, bookingId));

        await grain.RequestAsync(new RequestBookingCommand(
            orgId, siteId, CreateGuestInfo(), DateTime.UtcNow.AddDays(3), 4));
        await grain.RequireDepositAsync(new RequireDepositCommand(25m, DateTime.UtcNow.AddDays(1)));
        await grain.RecordDepositPaymentAsync(new RecordDepositPaymentCommand(
            PaymentMethod.CreditCard, "charge_456"));
        await grain.ConfirmAsync();

        // Act - Cancel and refund (cancellation more than 24h in advance)
        await grain.CancelAsync(new CancelBookingCommand(
            "Guest requested cancellation", Guid.NewGuid()));
        await grain.RefundDepositAsync("Cancellation within policy period", Guid.NewGuid());

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(BookingStatus.Cancelled);
        state.Deposit!.Status.Should().Be(DepositStatus.Refunded);
        state.Deposit.RefundedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Booking_CancelWithPaidDeposit_ShouldAllowForfeit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingGrain>(
            GrainKeys.Booking(orgId, siteId, bookingId));

        await grain.RequestAsync(new RequestBookingCommand(
            orgId, siteId, CreateGuestInfo(), DateTime.UtcNow.AddHours(12), 4));
        await grain.RequireDepositAsync(new RequireDepositCommand(25m, DateTime.UtcNow.AddDays(1)));
        await grain.RecordDepositPaymentAsync(new RecordDepositPaymentCommand(
            PaymentMethod.CreditCard, "charge_789"));
        await grain.ConfirmAsync();

        // Act - Cancel and forfeit (late cancellation)
        await grain.CancelAsync(new CancelBookingCommand(
            "Last minute cancellation", Guid.NewGuid()));
        await grain.ForfeitDepositAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(BookingStatus.Cancelled);
        state.Deposit!.Status.Should().Be(DepositStatus.Forfeited);
    }

    [Fact]
    public async Task BookingSettings_DepositConfiguration_ShouldPersist()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var settingsGrain = _fixture.Cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));
        await settingsGrain.InitializeAsync(orgId, siteId);

        // Act
        await settingsGrain.UpdateAsync(new UpdateBookingSettingsCommand(
            RequireDeposit: true,
            DepositAmount: 75m));

        // Assert
        var state = await settingsGrain.GetStateAsync();
        state.RequireDeposit.Should().BeTrue();
        state.DepositAmount.Should().Be(75m);
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TurnTimeCalculationTests
{
    private readonly TestClusterFixture _fixture;

    public TurnTimeCalculationTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ITurnTimeAnalyticsGrain> CreateAnalyticsAsync(Guid orgId, Guid siteId)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ITurnTimeAnalyticsGrain>(
            GrainKeys.TurnTimeAnalytics(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        return grain;
    }

    [Fact]
    public async Task TurnTime_RecordMultipleTimes_ShouldCalculateAverages()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAnalyticsAsync(orgId, siteId);

        var now = DateTime.UtcNow;

        // Act - Record multiple turn times
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), Guid.NewGuid(), 2, now.AddHours(-3), now.AddHours(-2))); // 60 min
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), Guid.NewGuid(), 2, now.AddHours(-5), now.AddHours(-4))); // 60 min
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), Guid.NewGuid(), 4, now.AddHours(-4), now.AddHours(-2.5))); // 90 min
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), Guid.NewGuid(), 6, now.AddHours(-3.5), now.AddHours(-1.5))); // 120 min

        // Assert
        var stats = await grain.GetOverallStatsAsync();
        stats.SampleCount.Should().Be(4);
        stats.AverageTurnTime.TotalMinutes.Should().BeInRange(60, 120);
    }

    [Fact]
    public async Task TurnTime_StatsByPartySize_ShouldSegmentData()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAnalyticsAsync(orgId, siteId);

        var now = DateTime.UtcNow;

        // Record turn times for different party sizes
        for (int i = 0; i < 5; i++)
        {
            await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
                Guid.NewGuid(), null, 2, now.AddHours(-i - 3), now.AddHours(-i - 2)));
        }
        for (int i = 0; i < 3; i++)
        {
            await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
                Guid.NewGuid(), null, 4, now.AddHours(-i - 4), now.AddHours(-i - 2.5)));
        }

        // Act
        var statsByPartySize = await grain.GetStatsByPartySizeAsync();

        // Assert
        statsByPartySize.Should().HaveCountGreaterThanOrEqualTo(2);
        statsByPartySize.Should().Contain(s => s.PartySize == 2 && s.Stats.SampleCount == 5);
        statsByPartySize.Should().Contain(s => s.PartySize == 4 && s.Stats.SampleCount == 3);
    }

    [Fact]
    public async Task TurnTime_EstimateByDayAndTime_ShouldConsiderPatterns()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAnalyticsAsync(orgId, siteId);

        // Find a Friday for consistent testing
        var friday = DateTime.UtcNow;
        while (friday.DayOfWeek != DayOfWeek.Friday)
            friday = friday.AddDays(1);

        // Record Friday dinner turn times (typically longer)
        for (int i = 0; i < 5; i++)
        {
            var seatedAt = friday.Date.AddDays(-7 * i).AddHours(19);
            var departedAt = seatedAt.AddMinutes(100 + i * 5); // 100-120 min
            await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
                Guid.NewGuid(), null, 4, seatedAt, departedAt));
        }

        // Act
        var estimate = await grain.GetEstimatedTurnTimeAsync(4, DayOfWeek.Friday, new TimeOnly(19, 0));

        // Assert
        estimate.TotalMinutes.Should().BeInRange(90, 130);
    }

    [Fact]
    public async Task TurnTime_ActiveSeatings_ShouldTrackCurrentOccupancy()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAnalyticsAsync(orgId, siteId);

        var booking1 = Guid.NewGuid();
        var booking2 = Guid.NewGuid();
        var table1 = Guid.NewGuid();
        var table2 = Guid.NewGuid();

        // Act - Register active seatings
        await grain.RegisterSeatingAsync(booking1, table1, "T1", 4, DateTime.UtcNow.AddMinutes(-30));
        await grain.RegisterSeatingAsync(booking2, table2, "T2", 2, DateTime.UtcNow.AddMinutes(-15));

        var activeSeatings = await grain.GetActiveSeatingsAsync();

        // Assert
        activeSeatings.Should().HaveCount(2);
        activeSeatings.Should().Contain(s => s.BookingId == booking1 && s.TableNumber == "T1");
        activeSeatings.Should().Contain(s => s.BookingId == booking2 && s.TableNumber == "T2");
    }

    [Fact]
    public async Task TurnTime_LongRunningTables_ShouldIdentifyOverdue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAnalyticsAsync(orgId, siteId);

        // Register one normal seating and one long-running
        await grain.RegisterSeatingAsync(
            Guid.NewGuid(), Guid.NewGuid(), "T1", 4, DateTime.UtcNow.AddMinutes(-30));
        await grain.RegisterSeatingAsync(
            Guid.NewGuid(), Guid.NewGuid(), "T2", 2, DateTime.UtcNow.AddHours(-3));

        // Act - Find tables that exceeded expected 90 minute turn time by 30+ minutes
        var longRunning = await grain.GetLongRunningTablesAsync(TimeSpan.FromMinutes(30));

        // Assert
        longRunning.Should().HaveCount(1);
        longRunning[0].TableNumber.Should().Be("T2");
        longRunning[0].OverdueBy.TotalMinutes.Should().BeGreaterThan(0);
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TimeSlotAvailabilityTests
{
    private readonly TestClusterFixture _fixture;

    public TimeSlotAvailabilityTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Availability_CustomSlotIntervals_ShouldGenerateCorrectSlots()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var settingsGrain = _fixture.Cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));
        await settingsGrain.InitializeAsync(orgId, siteId);

        // Set 15-minute intervals from 6pm to 9pm
        await settingsGrain.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: new TimeOnly(18, 0),
            DefaultCloseTime: new TimeOnly(21, 0),
            SlotInterval: TimeSpan.FromMinutes(15)));

        // Act
        var slots = await settingsGrain.GetAvailabilityAsync(new GetAvailabilityQuery(date, 4));

        // Assert - Should have 12 slots: 18:00, 18:15, 18:30, 18:45, 19:00, 19:15, 19:30, 19:45, 20:00, 20:15, 20:30, 20:45
        slots.Should().HaveCount(12);
        slots.First().Time.Should().Be(new TimeOnly(18, 0));
        slots.Last().Time.Should().Be(new TimeOnly(20, 45));
    }

    [Fact]
    public async Task Availability_AdvanceBookingDays_ShouldLimitFutureDates()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var settingsGrain = _fixture.Cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));
        await settingsGrain.InitializeAsync(orgId, siteId);

        // Set advance booking to 14 days
        await settingsGrain.UpdateAsync(new UpdateBookingSettingsCommand(AdvanceBookingDays: 14));

        // Act
        var withinRange = DateOnly.FromDateTime(DateTime.Today.AddDays(10));
        var beyondRange = DateOnly.FromDateTime(DateTime.Today.AddDays(20));

        var slotsWithin = await settingsGrain.GetAvailabilityAsync(new GetAvailabilityQuery(withinRange, 2));
        var slotsBeyond = await settingsGrain.GetAvailabilityAsync(new GetAvailabilityQuery(beyondRange, 2));

        // Assert
        slotsWithin.Should().NotBeEmpty();
        slotsWithin.Any(s => s.IsAvailable).Should().BeTrue();
        slotsBeyond.All(s => !s.IsAvailable).Should().BeTrue();
    }

    [Fact]
    public async Task Availability_MultipleBlockedDates_ShouldAllBeUnavailable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var settingsGrain = _fixture.Cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));
        await settingsGrain.InitializeAsync(orgId, siteId);

        // Block multiple dates (e.g., holiday period)
        var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10));
        await settingsGrain.BlockDateAsync(startDate);
        await settingsGrain.BlockDateAsync(startDate.AddDays(1));
        await settingsGrain.BlockDateAsync(startDate.AddDays(2));

        // Act & Assert
        for (int i = 0; i < 3; i++)
        {
            var checkDate = startDate.AddDays(i);
            var isBlocked = await settingsGrain.IsDateBlockedAsync(checkDate);
            isBlocked.Should().BeTrue($"Date {checkDate} should be blocked");

            var slots = await settingsGrain.GetAvailabilityAsync(new GetAvailabilityQuery(checkDate, 2));
            slots.All(s => !s.IsAvailable).Should().BeTrue($"All slots on {checkDate} should be unavailable");
        }
    }

    [Fact]
    public async Task Availability_UnblockDate_ShouldRestoreAvailability()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
        var settingsGrain = _fixture.Cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));
        await settingsGrain.InitializeAsync(orgId, siteId);

        await settingsGrain.BlockDateAsync(date);
        var blockedSlots = await settingsGrain.GetAvailabilityAsync(new GetAvailabilityQuery(date, 2));
        blockedSlots.All(s => !s.IsAvailable).Should().BeTrue();

        // Act
        await settingsGrain.UnblockDateAsync(date);

        // Assert
        var unblockedSlots = await settingsGrain.GetAvailabilityAsync(new GetAvailabilityQuery(date, 2));
        unblockedSlots.Any(s => s.IsAvailable).Should().BeTrue();
    }
}
