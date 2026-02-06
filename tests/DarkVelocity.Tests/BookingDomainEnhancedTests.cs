using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for the enhanced Booking Calendar grain functionality.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class BookingCalendarEnhancedTests
{
    private readonly TestClusterFixture _fixture;

    public BookingCalendarEnhancedTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IBookingCalendarGrain> CreateCalendarAsync(Guid orgId, Guid siteId, DateOnly date)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingCalendarGrain>(
            GrainKeys.BookingCalendar(orgId, siteId, date));
        await grain.InitializeAsync(orgId, siteId, date);
        return grain;
    }

    // Given: an initialized booking calendar with no reservations for the day
    // When: the day view is requested
    // Then: the view should show zero bookings, zero covers, and all time slots empty
    [Fact]
    public async Task GetDayViewAsync_EmptyCalendar_ShouldReturnEmptySlots()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateCalendarAsync(orgId, siteId, date);

        // Act
        var dayView = await grain.GetDayViewAsync();

        // Assert
        dayView.Date.Should().Be(date);
        dayView.TotalBookings.Should().Be(0);
        dayView.TotalCovers.Should().Be(0);
        dayView.Slots.Should().NotBeEmpty();
        dayView.Slots.All(s => s.BookingCount == 0).Should().BeTrue();
    }

    // Given: a booking calendar with reservations at noon (4 covers), 12:30 PM (2 covers), and 7 PM (6 covers)
    // When: the day view is requested with 1-hour slot intervals
    // Then: the lunch slot should group 2 bookings with 6 covers, and the dinner slot should have 1 booking with 6 covers
    [Fact]
    public async Task GetDayViewAsync_WithBookings_ShouldGroupBySlot()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateCalendarAsync(orgId, siteId, date);

        // Add bookings at different times
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "CONF1", new TimeOnly(12, 0), 4, "Guest 1", BookingStatus.Confirmed));
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "CONF2", new TimeOnly(12, 30), 2, "Guest 2", BookingStatus.Confirmed));
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "CONF3", new TimeOnly(19, 0), 6, "Guest 3", BookingStatus.Confirmed));

        // Act
        var dayView = await grain.GetDayViewAsync(TimeSpan.FromHours(1));

        // Assert
        dayView.TotalBookings.Should().Be(3);
        dayView.TotalCovers.Should().Be(12);

        // The 12:00-13:00 slot should have 2 bookings
        var lunchSlot = dayView.Slots.FirstOrDefault(s => s.StartTime == new TimeOnly(12, 0));
        lunchSlot.Should().NotBeNull();
        lunchSlot!.BookingCount.Should().Be(2);
        lunchSlot.CoverCount.Should().Be(6);

        // The 19:00-20:00 slot should have 1 booking
        var dinnerSlot = dayView.Slots.FirstOrDefault(s => s.StartTime == new TimeOnly(19, 0));
        dinnerSlot.Should().NotBeNull();
        dinnerSlot!.BookingCount.Should().Be(1);
        dinnerSlot.CoverCount.Should().Be(6);
    }

    // Given: three bookings allocated to two tables (T1 has 2 bookings, T2 has 1 booking)
    // When: table allocations are retrieved for the day
    // Then: allocations should be grouped by table with correct booking counts per table
    [Fact]
    public async Task GetTableAllocationsAsync_ShouldGroupBookingsByTable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var tableId1 = Guid.NewGuid();
        var tableId2 = Guid.NewGuid();
        var grain = await CreateCalendarAsync(orgId, siteId, date);

        var bookingId1 = Guid.NewGuid();
        var bookingId2 = Guid.NewGuid();
        var bookingId3 = Guid.NewGuid();

        await grain.AddBookingAsync(new AddBookingToCalendarCommand(
            bookingId1, "CONF1", new TimeOnly(12, 0), 4, "Guest 1", BookingStatus.Confirmed));
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(
            bookingId2, "CONF2", new TimeOnly(14, 0), 2, "Guest 2", BookingStatus.Confirmed));
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(
            bookingId3, "CONF3", new TimeOnly(19, 0), 6, "Guest 3", BookingStatus.Confirmed));

        await grain.SetTableAllocationAsync(bookingId1, tableId1, "T1");
        await grain.SetTableAllocationAsync(bookingId2, tableId1, "T1");
        await grain.SetTableAllocationAsync(bookingId3, tableId2, "T2");

        // Act
        var allocations = await grain.GetTableAllocationsAsync();

        // Assert
        allocations.Should().HaveCount(2);

        var table1Allocation = allocations.FirstOrDefault(a => a.TableId == tableId1);
        table1Allocation.Should().NotBeNull();
        table1Allocation!.Bookings.Should().HaveCount(2);

        var table2Allocation = allocations.FirstOrDefault(a => a.TableId == tableId2);
        table2Allocation.Should().NotBeNull();
        table2Allocation!.Bookings.Should().HaveCount(1);
    }
}

/// <summary>
/// Tests for the Table Assignment Optimizer grain.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TableAssignmentOptimizerTests
{
    private readonly TestClusterFixture _fixture;

    public TableAssignmentOptimizerTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ITableAssignmentOptimizerGrain> CreateOptimizerAsync(Guid orgId, Guid siteId)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ITableAssignmentOptimizerGrain>(
            GrainKeys.TableAssignmentOptimizer(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        return grain;
    }

    // Given: a table assignment optimizer with no tables registered
    // When: a recommendation is requested for a party of 4
    // Then: the result should indicate failure with no recommendations and a suitable message
    [Fact]
    public async Task GetRecommendationsAsync_NoTables_ShouldReturnNoRecommendations()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateOptimizerAsync(orgId, siteId);

        var request = new TableAssignmentRequest(
            Guid.NewGuid(), 4, DateTime.UtcNow, TimeSpan.FromMinutes(90));

        // Act
        var result = await grain.GetRecommendationsAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Recommendations.Should().BeEmpty();
        result.Message.Should().Be("No suitable tables available");
    }

    // Given: a table assignment optimizer with one 4-top table registered
    // When: a recommendation is requested for a party of 4
    // Then: the optimizer should recommend that table with its number
    [Fact]
    public async Task GetRecommendationsAsync_WithSuitableTable_ShouldReturnRecommendation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateOptimizerAsync(orgId, siteId);

        await grain.RegisterTableAsync(tableId, "T1", 2, 4, false);

        var request = new TableAssignmentRequest(
            Guid.NewGuid(), 4, DateTime.UtcNow, TimeSpan.FromMinutes(90));

        // Act
        var result = await grain.GetRecommendationsAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Recommendations.Should().HaveCount(1);
        result.Recommendations[0].TableId.Should().Be(tableId);
        result.Recommendations[0].TableNumber.Should().Be("T1");
    }

    // Given: a table assignment optimizer with a 2-top, 4-top, and 8-top table
    // When: a recommendation is requested for a party of 4
    // Then: the optimizer should rank the 4-top as the top recommendation (perfect capacity match)
    [Fact]
    public async Task GetRecommendationsAsync_ShouldPreferPerfectCapacityMatch()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var smallTableId = Guid.NewGuid();
        var perfectTableId = Guid.NewGuid();
        var largeTableId = Guid.NewGuid();
        var grain = await CreateOptimizerAsync(orgId, siteId);

        await grain.RegisterTableAsync(smallTableId, "T1", 2, 2, false);
        await grain.RegisterTableAsync(perfectTableId, "T2", 2, 4, false);
        await grain.RegisterTableAsync(largeTableId, "T3", 4, 8, false);

        var request = new TableAssignmentRequest(
            Guid.NewGuid(), 4, DateTime.UtcNow, TimeSpan.FromMinutes(90));

        // Act
        var result = await grain.GetRecommendationsAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Recommendations.Should().HaveCountGreaterThanOrEqualTo(1);
        result.Recommendations[0].TableId.Should().Be(perfectTableId); // Perfect match scores highest
    }

    // Given: a table assignment optimizer with a normal 4-top and a VIP-tagged 4-top
    // When: a recommendation is requested for a VIP party of 4
    // Then: the optimizer should prefer the VIP-tagged table over the regular one
    [Fact]
    public async Task GetRecommendationsAsync_WithVipPreference_ShouldPreferVipTable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var normalTableId = Guid.NewGuid();
        var vipTableId = Guid.NewGuid();
        var grain = await CreateOptimizerAsync(orgId, siteId);

        await grain.RegisterTableAsync(normalTableId, "T1", 2, 4, false);
        await grain.RegisterTableAsync(vipTableId, "VIP1", 2, 4, false, ["VIP"]);

        var request = new TableAssignmentRequest(
            Guid.NewGuid(), 4, DateTime.UtcNow, TimeSpan.FromMinutes(90), IsVip: true);

        // Act
        var result = await grain.GetRecommendationsAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Recommendations.Should().HaveCountGreaterThanOrEqualTo(1);
        result.Recommendations[0].TableId.Should().Be(vipTableId);
    }

    // Given: a table assignment optimizer with one registered table assigned to a server section
    // When: the table is recorded as occupied by a party
    // Then: the table should no longer be recommended for new parties
    [Fact]
    public async Task RecordTableUsageAsync_ShouldMarkTableOccupied()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var grain = await CreateOptimizerAsync(orgId, siteId);

        await grain.RegisterTableAsync(tableId, "T1", 2, 4, false);
        await grain.UpdateServerSectionAsync(new UpdateServerSectionCommand(
            serverId, "Server 1", [tableId], 30));

        // Act
        await grain.RecordTableUsageAsync(tableId, serverId, 4);

        // Assert - Table should not be recommended since it's occupied
        var request = new TableAssignmentRequest(
            Guid.NewGuid(), 4, DateTime.UtcNow, TimeSpan.FromMinutes(90));
        var result = await grain.GetRecommendationsAsync(request);

        result.Success.Should().BeFalse();
    }

    // Given: an occupied table that has been cleared after the guests departed
    // When: the table usage is cleared in the optimizer
    // Then: the table should be available for recommendation again
    [Fact]
    public async Task ClearTableUsageAsync_ShouldMakeTableAvailable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var grain = await CreateOptimizerAsync(orgId, siteId);

        await grain.RegisterTableAsync(tableId, "T1", 2, 4, false);
        await grain.UpdateServerSectionAsync(new UpdateServerSectionCommand(
            serverId, "Server 1", [tableId], 30));
        await grain.RecordTableUsageAsync(tableId, serverId, 4);

        // Act
        await grain.ClearTableUsageAsync(tableId);

        // Assert - Table should now be recommended
        var request = new TableAssignmentRequest(
            Guid.NewGuid(), 4, DateTime.UtcNow, TimeSpan.FromMinutes(90));
        var result = await grain.GetRecommendationsAsync(request);

        result.Success.Should().BeTrue();
        result.Recommendations[0].TableId.Should().Be(tableId);
    }

    // Given: a server section with 2 tables (max 20 covers) where 1 table is occupied with 4 guests
    // When: server workloads are retrieved
    // Then: the workload should show 4 current covers, 20% load, 1 occupied out of 2 total tables
    [Fact]
    public async Task GetServerWorkloadsAsync_ShouldReturnWorkloadStats()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId1 = Guid.NewGuid();
        var tableId2 = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var grain = await CreateOptimizerAsync(orgId, siteId);

        await grain.RegisterTableAsync(tableId1, "T1", 2, 4, false);
        await grain.RegisterTableAsync(tableId2, "T2", 2, 4, false);
        await grain.UpdateServerSectionAsync(new UpdateServerSectionCommand(
            serverId, "Server 1", [tableId1, tableId2], 20));

        await grain.RecordTableUsageAsync(tableId1, serverId, 4);

        // Act
        var workloads = await grain.GetServerWorkloadsAsync();

        // Assert
        workloads.Should().HaveCount(1);
        workloads[0].ServerId.Should().Be(serverId);
        workloads[0].ServerName.Should().Be("Server 1");
        workloads[0].CurrentCovers.Should().Be(4);
        workloads[0].MaxCovers.Should().Be(20);
        workloads[0].TableCount.Should().Be(2);
        workloads[0].OccupiedTableCount.Should().Be(1);
        workloads[0].LoadPercentage.Should().Be(20);
    }
}

/// <summary>
/// Tests for the Turn Time Analytics grain.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TurnTimeAnalyticsTests
{
    private readonly TestClusterFixture _fixture;

    public TurnTimeAnalyticsTests(TestClusterFixture fixture)
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

    // Given: a turn time analytics grain with no recorded turn times
    // When: overall statistics are requested
    // Then: the stats should show zero samples and a default 90-minute average turn time
    [Fact]
    public async Task GetOverallStatsAsync_NoRecords_ShouldReturnDefaults()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAnalyticsAsync(orgId, siteId);

        // Act
        var stats = await grain.GetOverallStatsAsync();

        // Assert
        stats.SampleCount.Should().Be(0);
        stats.AverageTurnTime.Should().Be(TimeSpan.FromMinutes(90));
    }

    // Given: a turn time analytics grain with no prior data
    // When: three table turn times are recorded for different party sizes
    // Then: the overall stats should show 3 samples with a positive average turn time
    [Fact]
    public async Task RecordTurnTimeAsync_ShouldRecordAndCalculateStats()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAnalyticsAsync(orgId, siteId);

        var now = DateTime.UtcNow;

        // Record several turn times
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), Guid.NewGuid(), 2, now.AddHours(-2), now.AddMinutes(-30)));
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), Guid.NewGuid(), 4, now.AddHours(-3), now.AddHours(-1)));
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), Guid.NewGuid(), 2, now.AddHours(-1.5), now.AddMinutes(-15)));

        // Act
        var stats = await grain.GetOverallStatsAsync();

        // Assert
        stats.SampleCount.Should().Be(3);
        stats.AverageTurnTime.TotalMinutes.Should().BeGreaterThan(0);
    }

    // Given: recorded turn times for parties of 2 (60 min each), 4 (90 min), and 6 (120 min)
    // When: statistics are retrieved grouped by party size
    // Then: three groups should be returned with correct sample counts per party size
    [Fact]
    public async Task GetStatsByPartySizeAsync_ShouldGroupByPartySize()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAnalyticsAsync(orgId, siteId);

        var now = DateTime.UtcNow;

        // Record turn times for different party sizes
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), null, 2, now.AddHours(-2), now.AddHours(-1))); // 60 min
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), null, 2, now.AddHours(-3), now.AddHours(-2))); // 60 min
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), null, 4, now.AddHours(-2.5), now.AddHours(-1))); // 90 min
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), null, 6, now.AddHours(-3), now.AddHours(-1))); // 120 min

        // Act
        var statsByPartySize = await grain.GetStatsByPartySizeAsync();

        // Assert
        statsByPartySize.Should().HaveCount(3);
        statsByPartySize.Should().Contain(s => s.PartySize == 2 && s.Stats.SampleCount == 2);
        statsByPartySize.Should().Contain(s => s.PartySize == 4 && s.Stats.SampleCount == 1);
        statsByPartySize.Should().Contain(s => s.PartySize == 6 && s.Stats.SampleCount == 1);
    }

    // Given: five recorded Saturday evening turn times of 90 minutes each for parties of 4
    // When: an estimated turn time is requested for a party of 4 on Saturday at 7 PM
    // Then: the estimate should be approximately 90 minutes based on historical data
    [Fact]
    public async Task GetEstimatedTurnTimeAsync_ShouldEstimateBasedOnHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateAnalyticsAsync(orgId, siteId);

        var saturday = DateTime.UtcNow;
        while (saturday.DayOfWeek != DayOfWeek.Saturday)
            saturday = saturday.AddDays(1);

        // Record Saturday evening turn times
        for (int i = 0; i < 5; i++)
        {
            var seatedAt = saturday.Date.AddHours(19).AddMinutes(i * 30);
            var departedAt = seatedAt.AddMinutes(90);
            await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
                Guid.NewGuid(), null, 4, seatedAt, departedAt));
        }

        // Act
        var estimate = await grain.GetEstimatedTurnTimeAsync(4, DayOfWeek.Saturday, new TimeOnly(19, 0));

        // Assert
        estimate.TotalMinutes.Should().BeInRange(80, 100); // Should be around 90 minutes
    }

    // Given: a turn time analytics grain tracking active seatings
    // When: a party of 4 is registered as seated at table T1
    // Then: the active seatings should contain the booking with correct table and party size
    [Fact]
    public async Task RegisterSeatingAsync_ShouldTrackActiveSeating()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateAnalyticsAsync(orgId, siteId);

        // Act
        await grain.RegisterSeatingAsync(bookingId, tableId, "T1", 4, DateTime.UtcNow);
        var activeSeatings = await grain.GetActiveSeatingsAsync();

        // Assert
        activeSeatings.Should().HaveCount(1);
        activeSeatings[0].BookingId.Should().Be(bookingId);
        activeSeatings[0].TableId.Should().Be(tableId);
        activeSeatings[0].PartySize.Should().Be(4);
    }

    // Given: a table seating that started 3 hours ago (well past typical turn time)
    // When: long-running tables are queried with a 30-minute overdue threshold
    // Then: the table should be flagged as overdue with a positive overdue duration
    [Fact]
    public async Task GetLongRunningTablesAsync_ShouldIdentifyOverdueTables()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateAnalyticsAsync(orgId, siteId);

        // Register a seating that started 3 hours ago
        await grain.RegisterSeatingAsync(bookingId, null, "T1", 4, DateTime.UtcNow.AddHours(-3));

        // Act
        var longRunning = await grain.GetLongRunningTablesAsync(TimeSpan.FromMinutes(30));

        // Assert
        longRunning.Should().HaveCount(1);
        longRunning[0].BookingId.Should().Be(bookingId);
        longRunning[0].OverdueBy.TotalMinutes.Should().BeGreaterThan(0);
    }
}

/// <summary>
/// Tests for the No-Show Detection grain.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class NoShowDetectionTests
{
    private readonly TestClusterFixture _fixture;

    public NoShowDetectionTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<INoShowDetectionGrain> CreateNoShowDetectionAsync(Guid orgId, Guid siteId)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INoShowDetectionGrain>(
            GrainKeys.NoShowDetection(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        return grain;
    }

    // Given: a no-show detection grain for a site
    // When: a booking scheduled for 2 hours from now is registered for no-show monitoring
    // Then: the booking should appear in the pending checks with the correct booking time
    [Fact]
    public async Task RegisterBookingAsync_ShouldAddToPendingChecks()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateNoShowDetectionAsync(orgId, siteId);

        var bookingTime = DateTime.UtcNow.AddHours(2);

        // Act
        await grain.RegisterBookingAsync(new RegisterNoShowCheckCommand(
            bookingId, bookingTime, "John Doe"));

        // Assert
        var pending = await grain.GetPendingChecksAsync();
        pending.Should().HaveCount(1);
        pending[0].BookingId.Should().Be(bookingId);
        pending[0].BookingTime.Should().Be(bookingTime);
    }

    // Given: a booking registered for no-show monitoring
    // When: the booking is unregistered (e.g., guest arrived)
    // Then: the pending checks list should be empty
    [Fact]
    public async Task UnregisterBookingAsync_ShouldRemoveFromPendingChecks()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateNoShowDetectionAsync(orgId, siteId);

        await grain.RegisterBookingAsync(new RegisterNoShowCheckCommand(
            bookingId, DateTime.UtcNow.AddHours(2), "John Doe"));

        // Act
        await grain.UnregisterBookingAsync(bookingId);

        // Assert
        var pending = await grain.GetPendingChecksAsync();
        pending.Should().BeEmpty();
    }

    // Given: a freshly initialized no-show detection grain
    // When: the default settings are retrieved
    // Then: settings should include a 15-minute grace period with auto-mark, notify, and deposit forfeit enabled
    [Fact]
    public async Task GetSettingsAsync_ShouldReturnDefaultSettings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateNoShowDetectionAsync(orgId, siteId);

        // Act
        var settings = await grain.GetSettingsAsync();

        // Assert
        settings.GracePeriod.Should().Be(TimeSpan.FromMinutes(15));
        settings.AutoMarkNoShow.Should().BeTrue();
        settings.NotifyOnNoShow.Should().BeTrue();
        settings.ForfeitDepositOnNoShow.Should().BeTrue();
    }

    // Given: a no-show detection grain with default settings
    // When: the grace period is updated to 30 minutes and auto-mark is disabled
    // Then: the updated settings should reflect the new grace period and disabled auto-mark
    [Fact]
    public async Task UpdateSettingsAsync_ShouldUpdateSettings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateNoShowDetectionAsync(orgId, siteId);

        // Act
        await grain.UpdateSettingsAsync(new UpdateNoShowSettingsCommand(
            GracePeriod: TimeSpan.FromMinutes(30),
            AutoMarkNoShow: false));

        var settings = await grain.GetSettingsAsync();

        // Assert
        settings.GracePeriod.Should().Be(TimeSpan.FromMinutes(30));
        settings.AutoMarkNoShow.Should().BeFalse();
    }
}

/// <summary>
/// Tests for the Enhanced Waitlist grain.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class EnhancedWaitlistTests
{
    private readonly TestClusterFixture _fixture;

    public EnhancedWaitlistTests(TestClusterFixture fixture)
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

    // Given: an empty waitlist for the day
    // When: a party of 4 is added to the waitlist
    // Then: the entry should be created at position 1 with a wait time estimate
    [Fact]
    public async Task AddEntryAsync_ShouldAddWithEstimate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        // Act
        var result = await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(
            CreateGuestInfo(), 4));

        // Assert
        result.EntryId.Should().NotBeEmpty();
        result.Position.Should().Be(1);
        result.Estimate.Should().NotBeNull();
        result.Estimate.Position.Should().Be(1);
    }

    // Given: a waitlist with two parties already waiting (party of 2 and party of 4)
    // When: a wait estimate is requested for a new party of 4
    // Then: the estimate should show position 3, 2 parties ahead, with a positive estimated wait
    [Fact]
    public async Task GetWaitEstimateAsync_ShouldCalculateEstimate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        // Add some entries
        await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(CreateGuestInfo("Guest 1"), 2));
        await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(CreateGuestInfo("Guest 2"), 4));

        // Act
        var estimate = await grain.GetWaitEstimateAsync(4);

        // Assert
        estimate.Position.Should().Be(3);
        estimate.PartiesAhead.Should().Be(2);
        estimate.EstimatedWait.TotalMinutes.Should().BeGreaterThan(0);
    }

    // Given: a waitlist with 4 parties already waiting
    // When: a returning customer with 10 prior visits is added to the waitlist
    // Then: the returning customer should receive a priority boost and be placed ahead of some regular guests
    [Fact]
    public async Task AddEntryAsync_ReturningCustomer_ShouldGetPriorityBoost()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        // Add some regular entries first
        await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(CreateGuestInfo("Guest 1"), 2));
        await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(CreateGuestInfo("Guest 2"), 4));
        await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(CreateGuestInfo("Guest 3"), 2));
        await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(CreateGuestInfo("Guest 4"), 4));

        // Act - Add a returning customer
        var result = await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(
            CreateGuestInfo("VIP Guest"),
            4,
            IsReturningCustomer: true,
            CustomerVisitCount: 10));

        // Assert - Should be bumped up in position
        result.Position.Should().BeLessThan(5);
    }

    // Given: a guest on the waitlist with a party of 4
    // When: the guest is seated at table T1
    // Then: the entry should be marked as seated with the table assignment and timestamp recorded
    [Fact]
    public async Task SeatEntryAsync_ShouldMarkSeatedAndRecordWaitTime()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var tableId = Guid.NewGuid();
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        var result = await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(
            CreateGuestInfo(), 4));

        // Act
        var promotion = await grain.SeatEntryAsync(result.EntryId, tableId, "T1");

        // Assert
        promotion.EntryId.Should().Be(result.EntryId);
        promotion.TableId.Should().Be(tableId);
        promotion.TableNumber.Should().Be("T1");
        promotion.PromotedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify entry status
        var entry = await grain.GetEntryAsync(result.EntryId);
        entry.Should().NotBeNull();
        entry!.Status.Should().Be(WaitlistStatus.Seated);
    }

    // Given: a waitlist with parties of 6, 2, and 4 guests
    // When: a 4-top table becomes available and the next suitable entry is sought
    // Then: a party that fits the table capacity (between 2 and 4 guests) should be returned
    [Fact]
    public async Task FindNextSuitableEntryAsync_ShouldMatchTableCapacity()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(CreateGuestInfo("Guest 1"), 6)); // Too large for 4-top
        await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(CreateGuestInfo("Guest 2"), 2)); // Good fit
        await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(CreateGuestInfo("Guest 3"), 4)); // Perfect fit

        // Act
        var suitable = await grain.FindNextSuitableEntryAsync(4);

        // Assert
        suitable.Should().NotBeNull();
        suitable!.PartySize.Should().BeLessThanOrEqualTo(4);
        suitable.PartySize.Should().BeGreaterThanOrEqualTo(2); // Capacity - 2
    }

    // Given: a waitlist with three guests in positions 1, 2, and 3
    // When: the third guest is moved to position 1
    // Then: the queue should be reordered with the moved guest at the front
    [Fact]
    public async Task UpdatePositionAsync_ShouldReorderQueue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        var entry1 = await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(CreateGuestInfo("Guest 1"), 2));
        var entry2 = await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(CreateGuestInfo("Guest 2"), 4));
        var entry3 = await grain.AddEntryAsync(new AddToEnhancedWaitlistCommand(CreateGuestInfo("Guest 3"), 2));

        // Act - Move entry3 to position 1
        await grain.UpdatePositionAsync(entry3.EntryId, 1);

        // Assert
        var entries = await grain.GetEntriesAsync();
        var updatedEntry3 = entries.FirstOrDefault(e => e.Id == entry3.EntryId);
        updatedEntry3.Should().NotBeNull();
        updatedEntry3!.Position.Should().Be(1);
    }
}

/// <summary>
/// Tests for the Booking Notification Scheduler grain.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class BookingNotificationSchedulerTests
{
    private readonly TestClusterFixture _fixture;

    public BookingNotificationSchedulerTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IBookingNotificationSchedulerGrain> CreateSchedulerAsync(Guid orgId, Guid siteId)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingNotificationSchedulerGrain>(
            GrainKeys.BookingNotificationScheduler(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        return grain;
    }

    // Given: a booking notification scheduler for a site
    // When: notifications are scheduled for a booking 2 days in the future
    // Then: at least 2 reminder notifications (24-hour and 2-hour) should be queued
    [Fact]
    public async Task ScheduleBookingNotificationsAsync_ShouldScheduleReminders()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateSchedulerAsync(orgId, siteId);

        var bookingTime = DateTime.UtcNow.AddDays(2);

        // Act
        await grain.ScheduleBookingNotificationsAsync(
            bookingId,
            "John Doe",
            "john@example.com",
            "email",
            bookingTime,
            "CONF123",
            4,
            "Test Restaurant");

        // Assert
        var pending = await grain.GetPendingNotificationsAsync(bookingId);
        pending.Should().HaveCountGreaterThanOrEqualTo(2); // 24h and 2h reminders
    }

    // Given: a booking with scheduled notification reminders
    // When: the booking notifications are cancelled
    // Then: no pending notifications should remain for that booking
    [Fact]
    public async Task CancelBookingNotificationsAsync_ShouldRemoveAllPending()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateSchedulerAsync(orgId, siteId);

        await grain.ScheduleBookingNotificationsAsync(
            bookingId,
            "John Doe",
            "john@example.com",
            "email",
            DateTime.UtcNow.AddDays(2),
            "CONF123",
            4);

        // Act
        await grain.CancelBookingNotificationsAsync(bookingId);

        // Assert
        var pending = await grain.GetPendingNotificationsAsync(bookingId);
        pending.Should().BeEmpty();
    }

    // Given: a freshly initialized booking notification scheduler
    // When: the default notification settings are retrieved
    // Then: confirmation, 24-hour reminder, 2-hour reminder, and follow-up notifications should all be enabled
    [Fact]
    public async Task GetSettingsAsync_ShouldReturnDefaultSettings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateSchedulerAsync(orgId, siteId);

        // Act
        var settings = await grain.GetSettingsAsync();

        // Assert
        settings.SendConfirmation.Should().BeTrue();
        settings.Send24hReminder.Should().BeTrue();
        settings.Send2hReminder.Should().BeTrue();
        settings.SendFollowUp.Should().BeTrue();
    }

    // Given: a booking notification scheduler with default settings
    // When: the 24-hour reminder is disabled and the default channel is changed to SMS
    // Then: the updated settings should reflect the disabled reminder and SMS channel
    [Fact]
    public async Task UpdateSettingsAsync_ShouldUpdateSettings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateSchedulerAsync(orgId, siteId);

        // Act
        await grain.UpdateSettingsAsync(new UpdateBookingNotificationSettingsCommand(
            Send24hReminder: false,
            DefaultChannel: "sms"));

        var settings = await grain.GetSettingsAsync();

        // Assert
        settings.Send24hReminder.Should().BeFalse();
        settings.DefaultChannel.Should().Be("sms");
    }
}
