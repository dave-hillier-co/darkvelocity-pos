using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using FluentAssertions;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
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

    #region Error Handling Tests

    [Fact]
    public async Task CreateAlertAsync_WhenNotInitialized_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        // Note: Not calling InitializeAsync

        // Act & Assert
        var act = async () => await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Test",
            Message: "Test message"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public async Task AcknowledgeAsync_WhenAlertNotFound_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act & Assert
        var act = async () => await grain.AcknowledgeAsync(
            new AcknowledgeAlertCommand(Guid.NewGuid(), Guid.NewGuid()));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task ResolveAsync_WhenAlertNotFound_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act & Assert
        var act = async () => await grain.ResolveAsync(
            new ResolveAlertCommand(Guid.NewGuid(), Guid.NewGuid(), "Notes"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task SnoozeAsync_WhenAlertNotFound_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act & Assert
        var act = async () => await grain.SnoozeAsync(
            new SnoozeAlertCommand(Guid.NewGuid(), TimeSpan.FromHours(1), Guid.NewGuid()));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DismissAsync_WhenAlertNotFound_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act & Assert
        var act = async () => await grain.DismissAsync(
            new DismissAlertCommand(Guid.NewGuid(), Guid.NewGuid(), "Reason"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task InitializeAsync_WhenAlreadyInitialized_ShouldBeIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Create an alert to verify state is preserved
        await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Test Alert",
            Message: "Test message"));

        // Act - Initialize again
        await grain.InitializeAsync(orgId, siteId);

        // Assert - Alert should still exist (state preserved)
        var alerts = await grain.GetActiveAlertsAsync();
        alerts.Should().HaveCount(1);
        alerts[0].Title.Should().Be("Test Alert");
    }

    #endregion

    #region Snooze Behavior Tests

    [Fact]
    public async Task GetActiveAlertsAsync_ShouldIncludeExpiredSnoozedAlerts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Snoozed Alert",
            Message: "This alert will have expired snooze"));

        // Snooze for a very short duration (already expired or almost expired)
        await grain.SnoozeAsync(new SnoozeAlertCommand(alert.AlertId, TimeSpan.FromMilliseconds(1), Guid.NewGuid()));

        // Wait for snooze to expire
        await Task.Delay(100);

        // Act
        var activeAlerts = await grain.GetActiveAlertsAsync();

        // Assert - Should include the alert since snooze expired
        activeAlerts.Should().Contain(a => a.AlertId == alert.AlertId);
    }

    [Fact]
    public async Task GetActiveAlertsAsync_ShouldExcludeCurrentlySnoozedAlerts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Snoozed Alert",
            Message: "This alert is currently snoozed"));

        // Snooze for a long duration
        await grain.SnoozeAsync(new SnoozeAlertCommand(alert.AlertId, TimeSpan.FromHours(4), Guid.NewGuid()));

        // Act
        var activeAlerts = await grain.GetActiveAlertsAsync();

        // Assert - Should NOT include the snoozed alert
        activeAlerts.Should().NotContain(a => a.AlertId == alert.AlertId);
    }

    [Fact]
    public async Task SnoozeAsync_ShouldCalculateSnoozedUntilCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Test Alert",
            Message: "Test"));

        var beforeSnooze = DateTime.UtcNow;
        var snoozeDuration = TimeSpan.FromHours(2);

        // Act
        await grain.SnoozeAsync(new SnoozeAlertCommand(alert.AlertId, snoozeDuration, Guid.NewGuid()));

        // Assert
        var snoozedAlert = await grain.GetAlertAsync(alert.AlertId);
        snoozedAlert.Should().NotBeNull();
        snoozedAlert!.SnoozedUntil.Should().NotBeNull();
        snoozedAlert.SnoozedUntil!.Value.Should().BeCloseTo(
            beforeSnooze.Add(snoozeDuration), TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Active Count Tests

    [Fact]
    public async Task GetActiveAlertCountAsync_ShouldIncludeAcknowledgedAlerts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var alert1 = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Active Alert",
            Message: "Active"));

        var alert2 = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.OutOfStock,
            Severity: AlertSeverity.High,
            Title: "Acknowledged Alert",
            Message: "Will be acknowledged"));

        await grain.AcknowledgeAsync(new AcknowledgeAlertCommand(alert2.AlertId, Guid.NewGuid()));

        // Act
        var count = await grain.GetActiveAlertCountAsync();

        // Assert - Both Active and Acknowledged should be counted
        count.Should().Be(2);
    }

    [Fact]
    public async Task GetActiveAlertCountAsync_ShouldExcludeResolvedAlerts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var alert1 = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Active Alert",
            Message: "Active"));

        var alert2 = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.OutOfStock,
            Severity: AlertSeverity.High,
            Title: "Resolved Alert",
            Message: "Will be resolved"));

        await grain.ResolveAsync(new ResolveAlertCommand(alert2.AlertId, Guid.NewGuid(), "Fixed"));

        // Act
        var count = await grain.GetActiveAlertCountAsync();

        // Assert - Only Active should be counted, not Resolved
        count.Should().Be(1);
    }

    [Fact]
    public async Task GetActiveAlertCountAsync_ShouldExcludeDismissedAlerts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var alert1 = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Active Alert",
            Message: "Active"));

        var alert2 = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.OutOfStock,
            Severity: AlertSeverity.High,
            Title: "Dismissed Alert",
            Message: "Will be dismissed"));

        await grain.DismissAsync(new DismissAlertCommand(alert2.AlertId, Guid.NewGuid(), "False positive"));

        // Act
        var count = await grain.GetActiveAlertCountAsync();

        // Assert - Only Active should be counted, not Dismissed
        count.Should().Be(1);
    }

    [Fact]
    public async Task GetActiveAlertCountAsync_ShouldHandleExpiredSnoozes()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var alert1 = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Active Alert",
            Message: "Active"));

        var alert2 = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.OutOfStock,
            Severity: AlertSeverity.High,
            Title: "Snoozed Alert",
            Message: "Currently snoozed"));

        await grain.SnoozeAsync(new SnoozeAlertCommand(alert2.AlertId, TimeSpan.FromHours(4), Guid.NewGuid()));

        // Act
        var count = await grain.GetActiveAlertCountAsync();

        // Assert - Snoozed alerts are not in Active/Acknowledged status, so only 1
        count.Should().Be(1);
    }

    #endregion

    #region Filtering Tests

    [Fact]
    public async Task GetAlertsAsync_WithStatusFilter_ShouldReturnMatching()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var activeAlert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Active Alert",
            Message: "Active"));

        var resolvedAlert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.OutOfStock,
            Severity: AlertSeverity.High,
            Title: "Resolved Alert",
            Message: "Will be resolved"));

        await grain.ResolveAsync(new ResolveAlertCommand(resolvedAlert.AlertId, Guid.NewGuid(), "Fixed"));

        // Act
        var activeAlerts = await grain.GetAlertsAsync(status: AlertStatus.Active);
        var resolvedAlerts = await grain.GetAlertsAsync(status: AlertStatus.Resolved);

        // Assert
        activeAlerts.Should().HaveCount(1);
        activeAlerts[0].AlertId.Should().Be(activeAlert.AlertId);

        resolvedAlerts.Should().HaveCount(1);
        resolvedAlerts[0].AlertId.Should().Be(resolvedAlert.AlertId);
    }

    [Fact]
    public async Task GetAlertsAsync_WithTypeFilter_ShouldReturnMatching()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Low Stock Alert",
            Message: "Low stock"));

        await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.OutOfStock,
            Severity: AlertSeverity.High,
            Title: "Out of Stock Alert",
            Message: "Out of stock"));

        await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Low,
            Title: "Another Low Stock",
            Message: "Also low stock"));

        // Act
        var lowStockAlerts = await grain.GetAlertsAsync(type: AlertType.LowStock);
        var outOfStockAlerts = await grain.GetAlertsAsync(type: AlertType.OutOfStock);

        // Assert
        lowStockAlerts.Should().HaveCount(2);
        lowStockAlerts.Should().OnlyContain(a => a.Type == AlertType.LowStock);

        outOfStockAlerts.Should().HaveCount(1);
        outOfStockAlerts[0].Type.Should().Be(AlertType.OutOfStock);
    }

    [Fact]
    public async Task GetAlertsAsync_WithStatusAndTypeFilter_ShouldReturnMatching()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var lowStockActive = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Low Stock Active",
            Message: "Low stock active"));

        var lowStockResolved = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Low Stock Resolved",
            Message: "Low stock resolved"));

        var outOfStockActive = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.OutOfStock,
            Severity: AlertSeverity.High,
            Title: "Out of Stock Active",
            Message: "Out of stock active"));

        await grain.ResolveAsync(new ResolveAlertCommand(lowStockResolved.AlertId, Guid.NewGuid(), "Fixed"));

        // Act
        var activeLowStockAlerts = await grain.GetAlertsAsync(
            status: AlertStatus.Active,
            type: AlertType.LowStock);

        // Assert
        activeLowStockAlerts.Should().HaveCount(1);
        activeLowStockAlerts[0].AlertId.Should().Be(lowStockActive.AlertId);
    }

    [Fact]
    public async Task GetAlertsAsync_WithLimit_ShouldRespectLimit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Create 5 alerts
        for (int i = 0; i < 5; i++)
        {
            await grain.CreateAlertAsync(new CreateAlertCommand(
                Type: AlertType.LowStock,
                Severity: AlertSeverity.Medium,
                Title: $"Alert {i}",
                Message: $"Message {i}"));
        }

        // Act
        var limitedAlerts = await grain.GetAlertsAsync(limit: 3);

        // Assert
        limitedAlerts.Should().HaveCount(3);
    }

    #endregion

    #region Metadata & Entity Tests

    [Fact]
    public async Task CreateAlertAsync_WithMetadata_ShouldPersistMetadata()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var metadata = new Dictionary<string, string>
        {
            { "ingredientName", "Ground Beef" },
            { "currentQuantity", "5" },
            { "reorderPoint", "20" }
        };

        // Act
        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Low Stock: Ground Beef",
            Message: "Stock is below reorder point",
            Metadata: metadata));

        // Assert
        var retrievedAlert = await grain.GetAlertAsync(alert.AlertId);
        retrievedAlert.Should().NotBeNull();
        retrievedAlert!.Metadata.Should().NotBeNull();
        retrievedAlert.Metadata!["ingredientName"].Should().Be("Ground Beef");
        retrievedAlert.Metadata["currentQuantity"].Should().Be("5");
        retrievedAlert.Metadata["reorderPoint"].Should().Be("20");
    }

    [Fact]
    public async Task CreateAlertAsync_WithEntityLink_ShouldPersistEntityInfo()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act
        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Low Stock: Ground Beef",
            Message: "Stock is below reorder point",
            EntityId: ingredientId,
            EntityType: "Ingredient"));

        // Assert
        var retrievedAlert = await grain.GetAlertAsync(alert.AlertId);
        retrievedAlert.Should().NotBeNull();
        retrievedAlert!.EntityId.Should().Be(ingredientId);
        retrievedAlert.EntityType.Should().Be("Ingredient");
    }

    #endregion

    #region Stream Event Tests

    [Fact]
    public async Task CreateAlertAsync_ShouldPublishAlertTriggeredEvent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var receivedEvents = new List<IStreamEvent>();

        var streamProvider = _fixture.Cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.AlertStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var subscription = await stream.SubscribeAsync((evt, token) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
            await grain.InitializeAsync(orgId, siteId);

            // Act
            var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
                Type: AlertType.LowStock,
                Severity: AlertSeverity.High,
                Title: "Low Stock Alert",
                Message: "Ground Beef is low"));

            // Wait for event propagation
            await Task.Delay(500);

            // Assert
            receivedEvents.Should().ContainSingle(e => e is AlertTriggeredEvent);
            var triggeredEvent = receivedEvents.OfType<AlertTriggeredEvent>().First();
            triggeredEvent.AlertId.Should().Be(alert.AlertId);
            triggeredEvent.SiteId.Should().Be(siteId);
            triggeredEvent.AlertType.Should().Be(AlertType.LowStock.ToString());
            triggeredEvent.Severity.Should().Be(AlertSeverity.High.ToString());
            triggeredEvent.Title.Should().Be("Low Stock Alert");
        }
        finally
        {
            await subscription.UnsubscribeAsync();
        }
    }

    #endregion

    #region Rules Tests

    [Fact]
    public async Task InitializeAsync_ShouldLoadAllSevenDefaultRules()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));

        // Act
        await grain.InitializeAsync(orgId, siteId);
        var rules = await grain.GetRulesAsync();

        // Assert
        rules.Should().HaveCount(7);
        rules.Should().Contain(r => r.Type == AlertType.GPDropped);
        rules.Should().Contain(r => r.Type == AlertType.HighVariance);
        rules.Should().Contain(r => r.Type == AlertType.LowStock);
        rules.Should().Contain(r => r.Type == AlertType.OutOfStock);
        rules.Should().Contain(r => r.Type == AlertType.ExpiryRisk);
        rules.Should().Contain(r => r.Type == AlertType.SupplierPriceSpike);
        rules.Should().Contain(r => r.Type == AlertType.NegativeStock);
    }

    [Fact]
    public async Task UpdateRuleAsync_WithNewRule_ShouldAddRule()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var newRule = new AlertRule
        {
            RuleId = Guid.NewGuid(),
            Type = AlertType.HighWaste,
            Name = "High Waste Alert",
            Description = "Waste exceeds threshold",
            IsEnabled = true,
            DefaultSeverity = AlertSeverity.High,
            Condition = new AlertRuleCondition
            {
                Metric = "WastePercent",
                Operator = ComparisonOperator.GreaterThan,
                Threshold = 5.0m
            },
            Actions = new List<AlertAction> { new AlertAction { ActionType = AlertActionType.CreateAlert } },
            CooldownPeriod = TimeSpan.FromHours(12)
        };

        // Act
        await grain.UpdateRuleAsync(newRule);
        var rules = await grain.GetRulesAsync();

        // Assert
        rules.Should().HaveCount(8); // 7 default + 1 new
        rules.Should().Contain(r => r.RuleId == newRule.RuleId);
        var addedRule = rules.First(r => r.RuleId == newRule.RuleId);
        addedRule.Name.Should().Be("High Waste Alert");
        addedRule.Type.Should().Be(AlertType.HighWaste);
    }

    [Fact]
    public async Task UpdateRuleAsync_EnableDisable_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var rules = await grain.GetRulesAsync();
        var lowStockRule = rules.First(r => r.Type == AlertType.LowStock);
        lowStockRule.IsEnabled.Should().BeTrue();

        // Act - Disable the rule
        var disabledRule = lowStockRule with { IsEnabled = false };
        await grain.UpdateRuleAsync(disabledRule);

        // Assert - Rule is disabled
        var updatedRules = await grain.GetRulesAsync();
        var updated = updatedRules.First(r => r.RuleId == lowStockRule.RuleId);
        updated.IsEnabled.Should().BeFalse();

        // Act - Re-enable the rule
        var enabledRule = updated with { IsEnabled = true };
        await grain.UpdateRuleAsync(enabledRule);

        // Assert - Rule is enabled again
        var finalRules = await grain.GetRulesAsync();
        var final = finalRules.First(r => r.RuleId == lowStockRule.RuleId);
        final.IsEnabled.Should().BeTrue();
    }

    #endregion

    #region Severity Tests

    [Fact]
    public async Task CreateAlertAsync_LowSeverity_ShouldCreate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act
        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.AgedStock,
            Severity: AlertSeverity.Low,
            Title: "Aged Stock Notice",
            Message: "Some stock is getting old"));

        // Assert
        alert.Should().NotBeNull();
        alert.Severity.Should().Be(AlertSeverity.Low);
    }

    [Fact]
    public async Task CreateAlertAsync_CriticalSeverity_ShouldCreate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act
        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.NegativeStock,
            Severity: AlertSeverity.Critical,
            Title: "Critical: Negative Stock",
            Message: "Stock quantity is negative"));

        // Assert
        alert.Should().NotBeNull();
        alert.Severity.Should().Be(AlertSeverity.Critical);
    }

    [Fact]
    public async Task GetActiveAlertsAsync_ShouldOrderBySeverityThenTime()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Create alerts with different severities (in reverse order)
        var lowAlert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.AgedStock,
            Severity: AlertSeverity.Low,
            Title: "Low Severity Alert",
            Message: "Low"));

        await Task.Delay(10); // Small delay to ensure different timestamps

        var mediumAlert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Medium Severity Alert",
            Message: "Medium"));

        await Task.Delay(10);

        var criticalAlert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.NegativeStock,
            Severity: AlertSeverity.Critical,
            Title: "Critical Severity Alert",
            Message: "Critical"));

        await Task.Delay(10);

        var highAlert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.OutOfStock,
            Severity: AlertSeverity.High,
            Title: "High Severity Alert",
            Message: "High"));

        // Act
        var activeAlerts = await grain.GetActiveAlertsAsync();

        // Assert - Should be ordered by severity descending (Critical > High > Medium > Low)
        activeAlerts.Should().HaveCount(4);
        activeAlerts[0].Severity.Should().Be(AlertSeverity.Critical);
        activeAlerts[1].Severity.Should().Be(AlertSeverity.High);
        activeAlerts[2].Severity.Should().Be(AlertSeverity.Medium);
        activeAlerts[3].Severity.Should().Be(AlertSeverity.Low);
    }

    #endregion

    #region Alert Types Tests

    [Fact]
    public async Task CreateAlertAsync_OutOfStock_ShouldCreate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act
        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.OutOfStock,
            Severity: AlertSeverity.High,
            Title: "Out of Stock: Chicken Breast",
            Message: "Chicken Breast is out of stock",
            EntityId: Guid.NewGuid(),
            EntityType: "Ingredient"));

        // Assert
        alert.Should().NotBeNull();
        alert.Type.Should().Be(AlertType.OutOfStock);
    }

    [Fact]
    public async Task CreateAlertAsync_NegativeStock_ShouldCreate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act
        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.NegativeStock,
            Severity: AlertSeverity.Critical,
            Title: "Negative Stock: Tomatoes",
            Message: "Tomatoes quantity is -5 units",
            EntityId: Guid.NewGuid(),
            EntityType: "Ingredient",
            Metadata: new Dictionary<string, string> { { "quantity", "-5" } }));

        // Assert
        alert.Should().NotBeNull();
        alert.Type.Should().Be(AlertType.NegativeStock);
    }

    [Fact]
    public async Task CreateAlertAsync_GPDropped_ShouldCreate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act
        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.GPDropped,
            Severity: AlertSeverity.High,
            Title: "GP% Dropped",
            Message: "Gross profit dropped by 5% vs last week",
            Metadata: new Dictionary<string, string>
            {
                { "currentGP", "62" },
                { "previousGP", "67" },
                { "change", "-5" }
            }));

        // Assert
        alert.Should().NotBeNull();
        alert.Type.Should().Be(AlertType.GPDropped);
    }

    [Fact]
    public async Task CreateAlertAsync_HighVariance_ShouldCreate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act
        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.HighVariance,
            Severity: AlertSeverity.Medium,
            Title: "High Cost Variance",
            Message: "Actual vs theoretical cost variance is 18%",
            Metadata: new Dictionary<string, string>
            {
                { "actualCost", "5900" },
                { "theoreticalCost", "5000" },
                { "variancePercent", "18" }
            }));

        // Assert
        alert.Should().NotBeNull();
        alert.Type.Should().Be(AlertType.HighVariance);
    }

    [Fact]
    public async Task CreateAlertAsync_SupplierPriceSpike_ShouldCreate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act
        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.SupplierPriceSpike,
            Severity: AlertSeverity.Medium,
            Title: "Price Spike: Beef from ABC Meats",
            Message: "Beef price increased 15% from last invoice",
            EntityId: supplierId,
            EntityType: "Supplier",
            Metadata: new Dictionary<string, string>
            {
                { "itemName", "Beef" },
                { "previousPrice", "8.50" },
                { "newPrice", "9.78" },
                { "changePercent", "15" }
            }));

        // Assert
        alert.Should().NotBeNull();
        alert.Type.Should().Be(AlertType.SupplierPriceSpike);
        alert.EntityType.Should().Be("Supplier");
    }

    #endregion

    #region State Transition Tests

    [Fact]
    public async Task AcknowledgeAsync_AlreadyAcknowledged_ShouldUpdate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Test Alert",
            Message: "Test"));

        // First acknowledgment
        await grain.AcknowledgeAsync(new AcknowledgeAlertCommand(alert.AlertId, firstUser));
        var firstAck = await grain.GetAlertAsync(alert.AlertId);
        var firstAckTime = firstAck!.AcknowledgedAt;

        await Task.Delay(50); // Small delay to ensure different timestamp

        // Act - Second acknowledgment by different user
        await grain.AcknowledgeAsync(new AcknowledgeAlertCommand(alert.AlertId, secondUser));

        // Assert
        var updatedAlert = await grain.GetAlertAsync(alert.AlertId);
        updatedAlert.Should().NotBeNull();
        updatedAlert!.Status.Should().Be(AlertStatus.Acknowledged);
        updatedAlert.AcknowledgedBy.Should().Be(secondUser);
        updatedAlert.AcknowledgedAt.Should().BeAfter(firstAckTime!.Value);
    }

    [Fact]
    public async Task ResolveAsync_AfterAcknowledge_ShouldMaintainHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var acknowledger = Guid.NewGuid();
        var resolver = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: AlertType.LowStock,
            Severity: AlertSeverity.Medium,
            Title: "Test Alert",
            Message: "Test"));

        // Acknowledge first
        await grain.AcknowledgeAsync(new AcknowledgeAlertCommand(alert.AlertId, acknowledger));

        // Act - Then resolve
        await grain.ResolveAsync(new ResolveAlertCommand(alert.AlertId, resolver, "Ordered more stock"));

        // Assert - Both acknowledge and resolve info should be present
        var resolvedAlert = await grain.GetAlertAsync(alert.AlertId);
        resolvedAlert.Should().NotBeNull();
        resolvedAlert!.Status.Should().Be(AlertStatus.Resolved);
        resolvedAlert.AcknowledgedBy.Should().Be(acknowledger);
        resolvedAlert.AcknowledgedAt.Should().NotBeNull();
        resolvedAlert.ResolvedBy.Should().Be(resolver);
        resolvedAlert.ResolvedAt.Should().NotBeNull();
        resolvedAlert.ResolutionNotes.Should().Be("Ordered more stock");
    }

    [Fact]
    public async Task GetAlertAsync_WhenNotFound_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act
        var alert = await grain.GetAlertAsync(Guid.NewGuid());

        // Assert
        alert.Should().BeNull();
    }

    #endregion
}
