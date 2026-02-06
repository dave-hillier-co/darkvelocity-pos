using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;
using Orleans.TestingHost;
using Xunit;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class CostAlertIndexGrainTests
{
    private readonly TestCluster _cluster;

    public CostAlertIndexGrainTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    private ICostAlertIndexGrain GetIndexGrain(Guid orgId)
    {
        var key = GrainKeys.Index(orgId, "costalerts", "all");
        return _cluster.GrainFactory.GetGrain<ICostAlertIndexGrain>(key);
    }

    private static CostAlertSummary CreateAlertSummary(
        Guid? alertId = null,
        CostAlertType alertType = CostAlertType.IngredientPriceIncrease,
        string? recipeName = null,
        string? ingredientName = "Test Ingredient",
        bool isAcknowledged = false,
        decimal changePercent = 15m)
    {
        return new CostAlertSummary(
            alertId ?? Guid.NewGuid(),
            alertType,
            recipeName,
            ingredientName,
            null,
            changePercent,
            isAcknowledged,
            CostAlertAction.None,
            DateTime.UtcNow,
            null);
    }

    // Given: An empty cost alert index for an organization
    // When: A new ingredient price alert for "Salmon" is registered
    // Then: The alert is retrievable by ID and contains the correct ingredient name
    [Fact]
    public async Task RegisterAsync_ShouldAddAlertToIndex()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var index = GetIndexGrain(orgId);

        var summary = CreateAlertSummary(alertId, ingredientName: "Salmon");

        // Act
        await index.RegisterAsync(alertId, summary);

        // Assert
        var result = await index.GetByIdAsync(alertId);
        result.Should().NotBeNull();
        result!.IngredientName.Should().Be("Salmon");
    }

    // Given: A cost alert registered with a 10% price change
    // When: The same alert ID is re-registered with a 25% price change
    // Then: The alert is updated in-place and reflects the new 25% change percentage
    [Fact]
    public async Task RegisterAsync_ShouldUpdateExistingAlert()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var index = GetIndexGrain(orgId);

        var initialSummary = CreateAlertSummary(alertId, changePercent: 10m);
        await index.RegisterAsync(alertId, initialSummary);

        // Act
        var updatedSummary = CreateAlertSummary(alertId, changePercent: 25m);
        await index.RegisterAsync(alertId, updatedSummary);

        // Assert
        var result = await index.GetByIdAsync(alertId);
        result.Should().NotBeNull();
        result!.ChangePercent.Should().Be(25m);
    }

    // Given: An unacknowledged cost alert in the index
    // When: The alert is acknowledged with a "PriceAdjusted" action
    // Then: The alert status changes to acknowledged with the action recorded and a timestamp set
    [Fact]
    public async Task UpdateStatusAsync_ShouldUpdateAlertStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var index = GetIndexGrain(orgId);

        var summary = CreateAlertSummary(alertId, isAcknowledged: false);
        await index.RegisterAsync(alertId, summary);

        // Act
        var acknowledgedAt = DateTime.UtcNow;
        await index.UpdateStatusAsync(alertId, true, CostAlertAction.PriceAdjusted, acknowledgedAt);

        // Assert
        var result = await index.GetByIdAsync(alertId);
        result.Should().NotBeNull();
        result!.IsAcknowledged.Should().BeTrue();
        result.ActionTaken.Should().Be(CostAlertAction.PriceAdjusted);
        result.AcknowledgedAt.Should().NotBeNull();
    }

    // Given: A cost alert registered in the index
    // When: The alert is removed by its ID
    // Then: The alert is no longer retrievable and returns null on lookup
    [Fact]
    public async Task RemoveAsync_ShouldRemoveAlertFromIndex()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var index = GetIndexGrain(orgId);

        var summary = CreateAlertSummary(alertId);
        await index.RegisterAsync(alertId, summary);

        // Act
        await index.RemoveAsync(alertId);

        // Assert
        var result = await index.GetByIdAsync(alertId);
        result.Should().BeNull();
    }

    // Given: One active (unacknowledged) and one acknowledged cost alert in the index
    // When: Querying the index filtered by Active status
    // Then: Only the unacknowledged alert is returned
    [Fact]
    public async Task QueryAsync_ShouldFilterByStatus_Active()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var index = GetIndexGrain(orgId);

        var activeAlert = CreateAlertSummary(isAcknowledged: false);
        var acknowledgedAlert = CreateAlertSummary(isAcknowledged: true);

        await index.RegisterAsync(activeAlert.AlertId, activeAlert);
        await index.RegisterAsync(acknowledgedAlert.AlertId, acknowledgedAlert);

        // Act
        var result = await index.QueryAsync(new CostAlertQuery(
            Status: CostAlertStatus.Active,
            AlertType: null,
            FromDate: null,
            ToDate: null));

        // Assert
        result.Alerts.Should().HaveCount(1);
        result.Alerts[0].IsAcknowledged.Should().BeFalse();
    }

    // Given: One active and one acknowledged cost alert in the index
    // When: Querying the index filtered by Acknowledged status
    // Then: Only the acknowledged alert is returned
    [Fact]
    public async Task QueryAsync_ShouldFilterByStatus_Acknowledged()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var index = GetIndexGrain(orgId);

        var activeAlert = CreateAlertSummary(isAcknowledged: false);
        var acknowledgedAlert = CreateAlertSummary(isAcknowledged: true);

        await index.RegisterAsync(activeAlert.AlertId, activeAlert);
        await index.RegisterAsync(acknowledgedAlert.AlertId, acknowledgedAlert);

        // Act
        var result = await index.QueryAsync(new CostAlertQuery(
            Status: CostAlertStatus.Acknowledged,
            AlertType: null,
            FromDate: null,
            ToDate: null));

        // Assert
        result.Alerts.Should().HaveCount(1);
        result.Alerts[0].IsAcknowledged.Should().BeTrue();
    }

    // Given: Two cost alerts -- one for ingredient price increase and one for margin below threshold
    // When: Querying the index filtered by IngredientPriceIncrease alert type
    // Then: Only the ingredient price increase alert is returned
    [Fact]
    public async Task QueryAsync_ShouldFilterByAlertType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var index = GetIndexGrain(orgId);

        var priceIncreaseAlert = CreateAlertSummary(alertType: CostAlertType.IngredientPriceIncrease);
        var marginAlert = CreateAlertSummary(alertType: CostAlertType.MarginBelowThreshold);

        await index.RegisterAsync(priceIncreaseAlert.AlertId, priceIncreaseAlert);
        await index.RegisterAsync(marginAlert.AlertId, marginAlert);

        // Act
        var result = await index.QueryAsync(new CostAlertQuery(
            Status: null,
            AlertType: CostAlertType.IngredientPriceIncrease,
            FromDate: null,
            ToDate: null));

        // Assert
        result.Alerts.Should().HaveCount(1);
        result.Alerts[0].AlertType.Should().Be(CostAlertType.IngredientPriceIncrease);
    }

    // Given: 10 cost alerts registered in the index
    // When: Querying with Skip=3 and Take=4 for pagination
    // Then: 4 alerts are returned in the page and TotalCount reflects all 10 alerts
    [Fact]
    public async Task QueryAsync_ShouldReturnPaginatedResults()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var index = GetIndexGrain(orgId);

        // Create 10 alerts
        for (int i = 0; i < 10; i++)
        {
            var alert = CreateAlertSummary();
            await index.RegisterAsync(alert.AlertId, alert);
        }

        // Act
        var result = await index.QueryAsync(new CostAlertQuery(
            Status: null,
            AlertType: null,
            FromDate: null,
            ToDate: null,
            Skip: 3,
            Take: 4));

        // Assert
        result.Alerts.Should().HaveCount(4);
        result.TotalCount.Should().Be(10);
    }

    // Given: 3 active and 2 acknowledged cost alerts in the index
    // When: Querying with Status=All to retrieve all alerts
    // Then: TotalCount is 5, ActiveCount is 3, and AcknowledgedCount is 2
    [Fact]
    public async Task QueryAsync_ShouldReturnCounts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var index = GetIndexGrain(orgId);

        // Create 3 active and 2 acknowledged alerts
        for (int i = 0; i < 3; i++)
        {
            var alert = CreateAlertSummary(isAcknowledged: false);
            await index.RegisterAsync(alert.AlertId, alert);
        }
        for (int i = 0; i < 2; i++)
        {
            var alert = CreateAlertSummary(isAcknowledged: true);
            await index.RegisterAsync(alert.AlertId, alert);
        }

        // Act
        var result = await index.QueryAsync(new CostAlertQuery(
            Status: CostAlertStatus.All,
            AlertType: null,
            FromDate: null,
            ToDate: null));

        // Assert
        result.TotalCount.Should().Be(5);
        result.ActiveCount.Should().Be(3);
        result.AcknowledgedCount.Should().Be(2);
    }

    // Given: 2 active (unacknowledged) alerts and 1 acknowledged alert in the index
    // When: The active alert count is requested
    // Then: The count returns 2, excluding the acknowledged alert
    [Fact]
    public async Task GetActiveCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var index = GetIndexGrain(orgId);

        var activeAlert1 = CreateAlertSummary(isAcknowledged: false);
        var activeAlert2 = CreateAlertSummary(isAcknowledged: false);
        var acknowledgedAlert = CreateAlertSummary(isAcknowledged: true);

        await index.RegisterAsync(activeAlert1.AlertId, activeAlert1);
        await index.RegisterAsync(activeAlert2.AlertId, activeAlert2);
        await index.RegisterAsync(acknowledgedAlert.AlertId, acknowledgedAlert);

        // Act
        var count = await index.GetActiveCountAsync();

        // Assert
        count.Should().Be(2);
    }

    // Given: Two cost alerts registered in the index with known IDs
    // When: All alert IDs are retrieved from the index
    // Then: Both alert IDs are present in the returned collection
    [Fact]
    public async Task GetAllAlertIdsAsync_ShouldReturnAllIds()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var index = GetIndexGrain(orgId);

        var alertId1 = Guid.NewGuid();
        var alertId2 = Guid.NewGuid();

        await index.RegisterAsync(alertId1, CreateAlertSummary(alertId1));
        await index.RegisterAsync(alertId2, CreateAlertSummary(alertId2));

        // Act
        var ids = await index.GetAllAlertIdsAsync();

        // Assert
        ids.Should().HaveCount(2);
        ids.Should().Contain(alertId1);
        ids.Should().Contain(alertId2);
    }

    // Given: 5 active cost alerts registered in the index
    // When: The index is cleared
    // Then: All alerts are removed and the alert ID list is empty
    [Fact]
    public async Task ClearAsync_ShouldRemoveAllAlerts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var index = GetIndexGrain(orgId);

        for (int i = 0; i < 5; i++)
        {
            var alert = CreateAlertSummary();
            await index.RegisterAsync(alert.AlertId, alert);
        }

        var countBefore = await index.GetActiveCountAsync();
        countBefore.Should().Be(5);

        // Act
        await index.ClearAsync();

        // Assert
        var ids = await index.GetAllAlertIdsAsync();
        ids.Should().BeEmpty();
    }

    // Given: An empty cost alert index with no registered alerts
    // When: Looking up an alert by a non-existent ID
    // Then: The result is null indicating the alert does not exist
    [Fact]
    public async Task GetByIdAsync_NonExistentAlert_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var index = GetIndexGrain(orgId);

        // Act
        var result = await index.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    // Given: An empty cost alert index with no registered alerts
    // When: Attempting to acknowledge a non-existent alert ID
    // Then: The operation completes silently without throwing an error
    [Fact]
    public async Task UpdateStatusAsync_NonExistentAlert_ShouldBeNoOp()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var index = GetIndexGrain(orgId);

        // Act & Assert - should not throw
        await index.UpdateStatusAsync(Guid.NewGuid(), true, CostAlertAction.Accepted, DateTime.UtcNow);
    }
}
