using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TurnTimeAnalyticsGrainTests
{
    private readonly TestClusterFixture _fixture;

    public TurnTimeAnalyticsGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ITurnTimeAnalyticsGrain> CreateAnalyticsAsync()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ITurnTimeAnalyticsGrain>(
            GrainKeys.TurnTimeAnalytics(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        return grain;
    }

    // Given: a turn time analytics grain with no records
    // When: overall stats are queried
    // Then: default stats are returned with zero sample count
    [Fact]
    public async Task GetOverallStats_NoRecords_ReturnsDefaults()
    {
        var grain = await CreateAnalyticsAsync();

        var stats = await grain.GetOverallStatsAsync();

        stats.SampleCount.Should().Be(0);
    }

    // Given: a turn time analytics grain
    // When: a 60-minute turn time is recorded for a party of 4
    // Then: overall stats reflect the single record
    [Fact]
    public async Task RecordTurnTime_SingleRecord_StatsReflectIt()
    {
        var grain = await CreateAnalyticsAsync();
        var seatedAt = DateTime.UtcNow.AddHours(-1);
        var departedAt = DateTime.UtcNow;

        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            BookingId: Guid.NewGuid(),
            TableId: Guid.NewGuid(),
            PartySize: 4,
            SeatedAt: seatedAt,
            DepartedAt: departedAt,
            CheckTotal: 120m));

        var stats = await grain.GetOverallStatsAsync();
        stats.SampleCount.Should().Be(1);
        stats.AverageTurnTime.TotalMinutes.Should().BeApproximately(60, 1);
    }

    // Given: a turn time analytics grain with turn times for different party sizes
    // When: stats by party size are queried
    // Then: separate stats are returned for each party size
    [Fact]
    public async Task GetStatsByPartySize_MultiplePartySizes_GroupsCorrectly()
    {
        var grain = await CreateAnalyticsAsync();
        var baseTime = DateTime.UtcNow;

        // Party of 2: 45 min
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), Guid.NewGuid(), PartySize: 2,
            SeatedAt: baseTime.AddMinutes(-45), DepartedAt: baseTime));

        // Party of 6: 90 min
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), Guid.NewGuid(), PartySize: 6,
            SeatedAt: baseTime.AddMinutes(-90), DepartedAt: baseTime));

        var statsByPartySize = await grain.GetStatsByPartySizeAsync();
        statsByPartySize.Should().HaveCount(2);

        var party2Stats = statsByPartySize.FirstOrDefault(s => s.PartySize == 2);
        var party6Stats = statsByPartySize.FirstOrDefault(s => s.PartySize == 6);

        party2Stats.Should().NotBeNull();
        party6Stats.Should().NotBeNull();
        party2Stats!.Stats.AverageTurnTime.TotalMinutes.Should().BeApproximately(45, 1);
        party6Stats!.Stats.AverageTurnTime.TotalMinutes.Should().BeApproximately(90, 1);
    }

    // Given: turn time records on different days of the week
    // When: stats by day are queried
    // Then: stats are grouped by day of week
    [Fact]
    public async Task GetStatsByDay_MultipleDays_GroupsByDayOfWeek()
    {
        var grain = await CreateAnalyticsAsync();

        // Find next Monday and Friday
        var now = DateTime.UtcNow;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
        var monday = now.AddDays(daysUntilMonday == 0 ? 7 : daysUntilMonday).Date.AddHours(12);
        var friday = monday.AddDays(4);

        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), Guid.NewGuid(), PartySize: 4,
            SeatedAt: monday.AddMinutes(-60), DepartedAt: monday));

        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), Guid.NewGuid(), PartySize: 4,
            SeatedAt: friday.AddMinutes(-90), DepartedAt: friday));

        var statsByDay = await grain.GetStatsByDayAsync();
        statsByDay.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    // Given: a turn time analytics grain
    // When: a very short duration (< 5 min) is recorded
    // Then: it is ignored (not added to stats)
    [Fact]
    public async Task RecordTurnTime_VeryShortDuration_IsIgnored()
    {
        var grain = await CreateAnalyticsAsync();
        var now = DateTime.UtcNow;

        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), Guid.NewGuid(), PartySize: 2,
            SeatedAt: now.AddMinutes(-3), DepartedAt: now));

        var stats = await grain.GetOverallStatsAsync();
        stats.SampleCount.Should().Be(0, "durations under 5 minutes are filtered out");
    }

    // Given: a turn time analytics grain
    // When: a seating is registered
    // Then: it appears in active seatings
    [Fact]
    public async Task RegisterSeating_AppearsInActiveSeatings()
    {
        var grain = await CreateAnalyticsAsync();
        var bookingId = Guid.NewGuid();
        var tableId = Guid.NewGuid();

        await grain.RegisterSeatingAsync(bookingId, tableId, "T5", 4, DateTime.UtcNow);

        var activeSeatings = await grain.GetActiveSeatingsAsync();
        activeSeatings.Should().ContainSingle(s => s.BookingId == bookingId);
    }

    // Given: an active seating
    // When: the seating is unregistered
    // Then: it no longer appears in active seatings
    [Fact]
    public async Task UnregisterSeating_RemovedFromActiveSeatings()
    {
        var grain = await CreateAnalyticsAsync();
        var bookingId = Guid.NewGuid();

        await grain.RegisterSeatingAsync(bookingId, Guid.NewGuid(), "T5", 4, DateTime.UtcNow);
        await grain.UnregisterSeatingAsync(bookingId);

        var activeSeatings = await grain.GetActiveSeatingsAsync();
        activeSeatings.Should().NotContain(s => s.BookingId == bookingId);
    }

    // Given: a turn time analytics grain with a seating that exceeds the threshold
    // When: long-running tables are queried
    // Then: the overdue seating is flagged
    [Fact]
    public async Task GetLongRunningTables_OverdueSeating_IsFlagged()
    {
        var grain = await CreateAnalyticsAsync();
        var bookingId = Guid.NewGuid();

        // Seat 2 hours ago
        await grain.RegisterSeatingAsync(bookingId, Guid.NewGuid(), "T3", 4,
            DateTime.UtcNow.AddHours(-2));

        // Threshold of 90 minutes
        var alerts = await grain.GetLongRunningTablesAsync(TimeSpan.FromMinutes(90));
        alerts.Should().ContainSingle(a => a.BookingId == bookingId);
        alerts[0].OverdueBy.TotalMinutes.Should().BeGreaterThan(0);
    }

    // Given: a turn time analytics grain with recent records
    // When: recent records are queried with a limit
    // Then: only the specified number of records are returned
    [Fact]
    public async Task GetRecentRecords_RespectsLimit()
    {
        var grain = await CreateAnalyticsAsync();
        var baseTime = DateTime.UtcNow;

        // Record 5 turn times
        for (int i = 0; i < 5; i++)
        {
            await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
                Guid.NewGuid(), Guid.NewGuid(), PartySize: 4,
                SeatedAt: baseTime.AddMinutes(-(60 + i * 10)),
                DepartedAt: baseTime.AddMinutes(-i * 10)));
        }

        var records = await grain.GetRecentRecordsAsync(limit: 3);
        records.Should().HaveCount(3);
    }

    // Given: a turn time analytics grain with a recorded turn time
    // When: RecordTurnTimeAsync is called
    // Then: the active seating for that booking is also removed
    [Fact]
    public async Task RecordTurnTime_RemovesActiveSeating()
    {
        var grain = await CreateAnalyticsAsync();
        var bookingId = Guid.NewGuid();
        var seatedAt = DateTime.UtcNow.AddHours(-1);

        await grain.RegisterSeatingAsync(bookingId, Guid.NewGuid(), "T1", 4, seatedAt);

        // Verify active
        var activeBefore = await grain.GetActiveSeatingsAsync();
        activeBefore.Should().ContainSingle(s => s.BookingId == bookingId);

        // Record departure
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            bookingId, Guid.NewGuid(), PartySize: 4,
            SeatedAt: seatedAt, DepartedAt: DateTime.UtcNow));

        // Verify removed
        var activeAfter = await grain.GetActiveSeatingsAsync();
        activeAfter.Should().NotContain(s => s.BookingId == bookingId);
    }

    // Given: a turn time analytics grain with multiple records
    // When: overall stats are queried
    // Then: average and median are calculated correctly
    [Fact]
    public async Task GetOverallStats_MultipleRecords_CalculatesAverageAndMedian()
    {
        var grain = await CreateAnalyticsAsync();
        var baseTime = DateTime.UtcNow;

        // 30 min, 60 min, 90 min
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), Guid.NewGuid(), PartySize: 2,
            SeatedAt: baseTime.AddMinutes(-30), DepartedAt: baseTime));
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), Guid.NewGuid(), PartySize: 4,
            SeatedAt: baseTime.AddMinutes(-60), DepartedAt: baseTime));
        await grain.RecordTurnTimeAsync(new RecordTurnTimeCommand(
            Guid.NewGuid(), Guid.NewGuid(), PartySize: 6,
            SeatedAt: baseTime.AddMinutes(-90), DepartedAt: baseTime));

        var stats = await grain.GetOverallStatsAsync();
        stats.SampleCount.Should().Be(3);
        stats.AverageTurnTime.TotalMinutes.Should().BeApproximately(60, 1);
        stats.MedianTurnTime.TotalMinutes.Should().BeApproximately(60, 1);
        stats.MinTurnTime.TotalMinutes.Should().BeApproximately(30, 1);
        stats.MaxTurnTime.TotalMinutes.Should().BeApproximately(90, 1);
    }
}
