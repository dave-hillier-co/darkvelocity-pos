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
