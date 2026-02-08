using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TableAssignmentOptimizerGrainTests
{
    private readonly TestClusterFixture _fixture;

    public TableAssignmentOptimizerGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ITableAssignmentOptimizerGrain> CreateOptimizerAsync()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ITableAssignmentOptimizerGrain>(
            GrainKeys.TableAssignmentOptimizer(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        return grain;
    }

    // Given: an optimizer with a 4-top and an 8-top table registered
    // When: a party of 4 requests recommendations
    // Then: the 4-top scores higher than the 8-top (perfect capacity match)
    [Fact]
    public async Task GetRecommendations_PerfectCapacityMatch_ScoresHighest()
    {
        var grain = await CreateOptimizerAsync();
        var table4Id = Guid.NewGuid();
        var table8Id = Guid.NewGuid();

        await grain.RegisterTableAsync(table4Id, "T1", 2, 4, isCombinable: true);
        await grain.RegisterTableAsync(table8Id, "T2", 4, 8, isCombinable: true);

        var result = await grain.GetRecommendationsAsync(new TableAssignmentRequest(
            BookingId: Guid.NewGuid(),
            PartySize: 4,
            BookingTime: DateTime.UtcNow.AddHours(1),
            Duration: TimeSpan.FromMinutes(90)));

        result.Success.Should().BeTrue();
        result.Recommendations.Should().HaveCountGreaterOrEqualTo(1);

        var topRecommendation = result.Recommendations.OrderByDescending(r => r.Score).First();
        topRecommendation.TableId.Should().Be(table4Id, "the 4-top is a perfect match for a party of 4");
    }

    // Given: an optimizer with a 2-top, 4-top, and 8-top registered
    // When: a party of 3 requests recommendations
    // Then: the 4-top scores higher than the 8-top (less wasted capacity)
    [Fact]
    public async Task GetRecommendations_SmallerOverCapacity_ScoresHigherThanLarger()
    {
        var grain = await CreateOptimizerAsync();
        var table2Id = Guid.NewGuid();
        var table4Id = Guid.NewGuid();
        var table8Id = Guid.NewGuid();

        await grain.RegisterTableAsync(table2Id, "T1", 1, 2, isCombinable: true);
        await grain.RegisterTableAsync(table4Id, "T2", 2, 4, isCombinable: true);
        await grain.RegisterTableAsync(table8Id, "T3", 4, 8, isCombinable: true);

        var result = await grain.GetRecommendationsAsync(new TableAssignmentRequest(
            BookingId: Guid.NewGuid(),
            PartySize: 3,
            BookingTime: DateTime.UtcNow.AddHours(1),
            Duration: TimeSpan.FromMinutes(90)));

        result.Success.Should().BeTrue();
        var recommendations = result.Recommendations.OrderByDescending(r => r.Score).ToList();

        // The 4-top should rank above the 8-top
        var table4Rec = recommendations.FirstOrDefault(r => r.TableId == table4Id);
        var table8Rec = recommendations.FirstOrDefault(r => r.TableId == table8Id);

        table4Rec.Should().NotBeNull();
        table8Rec.Should().NotBeNull();
        table4Rec!.Score.Should().BeGreaterThan(table8Rec!.Score);
    }

    // Given: an optimizer with no tables registered
    // When: a party of 4 requests recommendations
    // Then: the result indicates no recommendations available
    [Fact]
    public async Task GetRecommendations_NoTables_ReturnsNoRecommendations()
    {
        var grain = await CreateOptimizerAsync();

        var result = await grain.GetRecommendationsAsync(new TableAssignmentRequest(
            BookingId: Guid.NewGuid(),
            PartySize: 4,
            BookingTime: DateTime.UtcNow.AddHours(1),
            Duration: TimeSpan.FromMinutes(90)));

        result.Recommendations.Should().BeEmpty();
    }

    // Given: an optimizer with tables registered
    // When: a table is registered then unregistered
    // Then: the unregistered table no longer appears in recommendations
    [Fact]
    public async Task UnregisterTable_RemovesFromRecommendations()
    {
        var grain = await CreateOptimizerAsync();
        var tableId = Guid.NewGuid();

        await grain.RegisterTableAsync(tableId, "T1", 2, 4, isCombinable: true);

        // Verify it appears
        var result = await grain.GetRecommendationsAsync(new TableAssignmentRequest(
            BookingId: Guid.NewGuid(),
            PartySize: 3,
            BookingTime: DateTime.UtcNow.AddHours(1),
            Duration: TimeSpan.FromMinutes(90)));
        result.Recommendations.Should().Contain(r => r.TableId == tableId);

        // Unregister
        await grain.UnregisterTableAsync(tableId);

        // Verify gone
        result = await grain.GetRecommendationsAsync(new TableAssignmentRequest(
            BookingId: Guid.NewGuid(),
            PartySize: 3,
            BookingTime: DateTime.UtcNow.AddHours(1),
            Duration: TimeSpan.FromMinutes(90)));
        result.Recommendations.Should().NotContain(r => r.TableId == tableId);
    }

    // Given: an optimizer with one table registered
    // When: AutoAssign is called
    // Then: the top recommendation is returned
    [Fact]
    public async Task AutoAssign_ReturnsTopRecommendation()
    {
        var grain = await CreateOptimizerAsync();
        var tableId = Guid.NewGuid();

        await grain.RegisterTableAsync(tableId, "T1", 2, 4, isCombinable: true);

        var recommendation = await grain.AutoAssignAsync(new TableAssignmentRequest(
            BookingId: Guid.NewGuid(),
            PartySize: 3,
            BookingTime: DateTime.UtcNow.AddHours(1),
            Duration: TimeSpan.FromMinutes(90)));

        recommendation.Should().NotBeNull();
        recommendation!.TableId.Should().Be(tableId);
    }

    // Given: an optimizer with tables and an occupied table
    // When: recommendations are requested
    // Then: occupied tables are not recommended
    [Fact]
    public async Task GetRecommendations_OccupiedTables_NotRecommended()
    {
        var grain = await CreateOptimizerAsync();
        var occupiedTableId = Guid.NewGuid();
        var availableTableId = Guid.NewGuid();

        await grain.RegisterTableAsync(occupiedTableId, "T1", 2, 4, isCombinable: true);
        await grain.RegisterTableAsync(availableTableId, "T2", 2, 4, isCombinable: true);

        // Mark one as in use
        await grain.RecordTableUsageAsync(occupiedTableId, Guid.NewGuid(), 3);

        var result = await grain.GetRecommendationsAsync(new TableAssignmentRequest(
            BookingId: Guid.NewGuid(),
            PartySize: 3,
            BookingTime: DateTime.UtcNow.AddHours(1),
            Duration: TimeSpan.FromMinutes(90)));

        result.Recommendations.Should().NotContain(r => r.TableId == occupiedTableId);
        result.Recommendations.Should().Contain(r => r.TableId == availableTableId);
    }

    // Given: an optimizer with tables cleared after usage
    // When: recommendations are requested
    // Then: the cleared table is available again
    [Fact]
    public async Task ClearTableUsage_MakesTableAvailableAgain()
    {
        var grain = await CreateOptimizerAsync();
        var tableId = Guid.NewGuid();

        await grain.RegisterTableAsync(tableId, "T1", 2, 4, isCombinable: true);
        await grain.RecordTableUsageAsync(tableId, Guid.NewGuid(), 3);

        // Table should not appear while occupied
        var result = await grain.GetRecommendationsAsync(new TableAssignmentRequest(
            BookingId: Guid.NewGuid(),
            PartySize: 3,
            BookingTime: DateTime.UtcNow.AddHours(1),
            Duration: TimeSpan.FromMinutes(90)));
        result.Recommendations.Should().NotContain(r => r.TableId == tableId);

        // Clear and check again
        await grain.ClearTableUsageAsync(tableId);

        result = await grain.GetRecommendationsAsync(new TableAssignmentRequest(
            BookingId: Guid.NewGuid(),
            PartySize: 3,
            BookingTime: DateTime.UtcNow.AddHours(1),
            Duration: TimeSpan.FromMinutes(90)));
        result.Recommendations.Should().Contain(r => r.TableId == tableId);
    }

    // Given: an optimizer with server sections configured
    // When: server workloads are queried
    // Then: workloads reflect recorded table usage
    [Fact]
    public async Task ServerWorkloads_ReflectsTableUsage()
    {
        var grain = await CreateOptimizerAsync();
        var serverId = Guid.NewGuid();
        var table1Id = Guid.NewGuid();
        var table2Id = Guid.NewGuid();

        await grain.RegisterTableAsync(table1Id, "T1", 2, 4, isCombinable: true);
        await grain.RegisterTableAsync(table2Id, "T2", 2, 4, isCombinable: true);

        await grain.UpdateServerSectionAsync(new UpdateServerSectionCommand(
            serverId, "Alice", [table1Id, table2Id], MaxCovers: 20));

        // Record usage on one table
        await grain.RecordTableUsageAsync(table1Id, serverId, 3);

        var workloads = await grain.GetServerWorkloadsAsync();
        workloads.Should().ContainSingle();
        workloads[0].ServerId.Should().Be(serverId);
        workloads[0].CurrentCovers.Should().Be(3);
        workloads[0].OccupiedTableCount.Should().Be(1);
    }

    // Given: an optimizer with server sections
    // When: server sections are queried
    // Then: the configured sections are returned
    [Fact]
    public async Task GetServerSections_ReturnsConfiguredSections()
    {
        var grain = await CreateOptimizerAsync();
        var serverId = Guid.NewGuid();
        var tableId = Guid.NewGuid();

        await grain.RegisterTableAsync(tableId, "T1", 2, 4, isCombinable: true);
        await grain.UpdateServerSectionAsync(new UpdateServerSectionCommand(
            serverId, "Bob", [tableId], MaxCovers: 15));

        var sections = await grain.GetServerSectionsAsync();
        sections.Should().ContainSingle();
        sections[0].ServerId.Should().Be(serverId);
        sections[0].ServerName.Should().Be("Bob");
        sections[0].TableIds.Should().Contain(tableId);
    }
}
