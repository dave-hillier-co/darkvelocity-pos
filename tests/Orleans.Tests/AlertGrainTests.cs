using DarkVelocity.Orleans.Abstractions.Grains;
using FluentAssertions;

namespace DarkVelocity.Orleans.Tests;

[Collection(ClusterCollection.Name)]
public class AlertGrainTests
{
    private readonly TestClusterFixture _fixture;

    public AlertGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private string GetGrainKey(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:alerts";

    [Fact]
    public async Task InitializeAsync_ShouldInitializeWithDefaultRules()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));

        // Act
        await grain.InitializeAsync(orgId, siteId);

        // Assert
        var rules = await grain.GetRulesAsync();
        rules.Should().NotBeEmpty();
        rules.Should().Contain(r => r.Type == AlertType.LowStock);
        rules.Should().Contain(r => r.Type == AlertType.OutOfStock);
        rules.Should().Contain(r => r.Type == AlertType.ExpiryRisk);
    }

    [Fact]
    public async Task CreateAlertAsync_ShouldCreateAlert()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act
        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Low Stock: Ground Beef",
            Message: "Ground Beef is below reorder point",
            EntityId: Guid.NewGuid(),
            EntityType: "Ingredient"));

        // Assert
        alert.Should().NotBeNull();
        alert.AlertId.Should().NotBeEmpty();
        alert.Type.Should().Be(AlertType.LowStock);
        alert.Severity.Should().Be(AlertSeverity.Medium);
        alert.Status.Should().Be(AlertStatus.Active);
    }

    [Fact]
    public async Task GetActiveAlertsAsync_ShouldReturnActiveAlerts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Low Stock: Ground Beef",
            Message: "Ground Beef is below reorder point"));

        await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.ExpiryRisk,
            Severity: AlertSeverity.High,
            Title: "Expiry Risk: Milk",
            Message: "Milk expires in 2 days"));

        // Act
        var activeAlerts = await grain.GetActiveAlertsAsync();

        // Assert
        activeAlerts.Should().HaveCount(2);
        activeAlerts.Should().BeInDescendingOrder(a => a.Severity);
    }

    [Fact]
    public async Task AcknowledgeAsync_ShouldUpdateAlertStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Low Stock: Ground Beef",
            Message: "Ground Beef is below reorder point"));

        // Act
        await grain.AcknowledgeAsync(new AcknowledgeAlertCommand(alert.AlertId, userId));

        // Assert
        var updatedAlert = await grain.GetAlertAsync(alert.AlertId);
        updatedAlert.Should().NotBeNull();
        updatedAlert!.Status.Should().Be(AlertStatus.Acknowledged);
        updatedAlert.AcknowledgedBy.Should().Be(userId);
        updatedAlert.AcknowledgedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldUpdateAlertStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Low Stock: Ground Beef",
            Message: "Ground Beef is below reorder point"));

        // Act
        await grain.ResolveAsync(new ResolveAlertCommand(alert.AlertId, userId, "Ordered more stock"));

        // Assert
        var updatedAlert = await grain.GetAlertAsync(alert.AlertId);
        updatedAlert.Should().NotBeNull();
        updatedAlert!.Status.Should().Be(AlertStatus.Resolved);
        updatedAlert.ResolvedBy.Should().Be(userId);
        updatedAlert.ResolutionNotes.Should().Be("Ordered more stock");
    }

    [Fact]
    public async Task SnoozeAsync_ShouldUpdateAlertStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Low Stock: Ground Beef",
            Message: "Ground Beef is below reorder point"));

        // Act
        await grain.SnoozeAsync(new SnoozeAlertCommand(alert.AlertId, TimeSpan.FromHours(4), userId));

        // Assert
        var updatedAlert = await grain.GetAlertAsync(alert.AlertId);
        updatedAlert.Should().NotBeNull();
        updatedAlert!.Status.Should().Be(AlertStatus.Snoozed);
        updatedAlert.SnoozedUntil.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAlertCountsByTypeAsync_ShouldReturnCorrectCounts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Low Stock 1",
            Message: "Low stock"));

        await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Low Stock 2",
            Message: "Low stock"));

        await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.ExpiryRisk,
            Severity: AlertSeverity.High,
            Title: "Expiry Risk",
            Message: "Expiring soon"));

        // Act
        var counts = await grain.GetAlertCountsByTypeAsync();

        // Assert
        counts[AlertType.LowStock].Should().Be(2);
        counts[AlertType.ExpiryRisk].Should().Be(1);
    }

    [Fact]
    public async Task DismissAsync_ShouldRemoveFromActive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Low Stock",
            Message: "Low stock"));

        // Act
        await grain.DismissAsync(new DismissAlertCommand(alert.AlertId, userId, "False positive"));

        // Assert
        var activeAlerts = await grain.GetActiveAlertsAsync();
        activeAlerts.Should().NotContain(a => a.AlertId == alert.AlertId);
    }

    [Fact]
    public async Task UpdateRuleAsync_ShouldUpdateExistingRule()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var rules = await grain.GetRulesAsync();
        var lowStockRule = rules.First(r => r.Type == AlertType.LowStock);

        // Act
        var updatedRule = lowStockRule with
        {
            IsEnabled = false,
            DefaultSeverity = AlertSeverity.Low
        };
        await grain.UpdateRuleAsync(updatedRule);

        // Assert
        var newRules = await grain.GetRulesAsync();
        var updated = newRules.First(r => r.RuleId == lowStockRule.RuleId);
        updated.IsEnabled.Should().BeFalse();
        updated.DefaultSeverity.Should().Be(AlertSeverity.Low);
    }
}
