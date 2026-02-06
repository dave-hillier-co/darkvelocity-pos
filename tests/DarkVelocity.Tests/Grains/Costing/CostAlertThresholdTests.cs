using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;
using Orleans.TestingHost;
using Xunit;

namespace DarkVelocity.Tests.Grains.Costing;

/// <summary>
/// Tests for cost alert threshold trigger behaviors and alert scenarios.
/// Focuses on various threshold conditions that should/should not generate alerts.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class CostAlertThresholdTests
{
    private readonly TestCluster _cluster;

    public CostAlertThresholdTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    // ============================================================================
    // Ingredient Price Change Threshold Tests
    // ============================================================================

    // Given: costing settings configured with a price change alert threshold
    // When: an ingredient price change percentage is evaluated against the threshold
    // Then: an alert is triggered only when the absolute price change exceeds the threshold
    [Theory]
    [InlineData(100.00, 105.00, 5.0, false)]   // 5% increase, threshold 10% - no alert
    [InlineData(100.00, 110.00, 10.0, false)]  // 10% increase, threshold 10% - at threshold
    [InlineData(100.00, 115.00, 10.0, true)]   // 15% increase, threshold 10% - alert
    [InlineData(100.00, 125.00, 10.0, true)]   // 25% increase, threshold 10% - alert
    [InlineData(100.00, 90.00, 10.0, false)]   // 10% decrease, threshold 10% - no alert (below)
    [InlineData(100.00, 85.00, 10.0, true)]    // 15% decrease, threshold 10% - alert (magnitude)
    public async Task CostingSettings_PriceChangeThreshold_ShouldEvaluateCorrectly(
        decimal previousPrice,
        decimal currentPrice,
        decimal threshold,
        bool expectAlert)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var settingsGrain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        await settingsGrain.InitializeAsync(locationId);
        await settingsGrain.UpdateAsync(new UpdateCostingSettingsCommand(
            TargetFoodCostPercent: null,
            TargetBeverageCostPercent: null,
            MinimumMarginPercent: null,
            WarningMarginPercent: null,
            PriceChangeAlertThreshold: threshold,
            CostIncreaseAlertThreshold: null,
            AutoRecalculateCosts: null,
            AutoCreateSnapshots: null,
            SnapshotFrequencyDays: null));

        // Act
        var changePercent = previousPrice > 0
            ? Math.Abs((currentPrice - previousPrice) / previousPrice * 100)
            : 0;
        var shouldAlert = await settingsGrain.ShouldAlertOnPriceChangeAsync(changePercent);

        // Assert
        shouldAlert.Should().Be(expectAlert);
    }

    // Given: costing settings configured with a cost increase alert threshold
    // When: a recipe cost change percentage is evaluated against the threshold
    // Then: an alert is triggered only for cost increases that exceed the threshold
    [Theory]
    [InlineData(10.00, 10.50, 5.0, false)]    // 5% increase, threshold 5% - at threshold
    [InlineData(10.00, 10.60, 5.0, true)]     // 6% increase, threshold 5% - alert
    [InlineData(10.00, 9.50, 5.0, false)]     // 5% decrease - no cost increase
    public async Task CostingSettings_CostIncreaseThreshold_ShouldEvaluateCorrectly(
        decimal previousCost,
        decimal currentCost,
        decimal threshold,
        bool expectAlert)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var settingsGrain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        await settingsGrain.InitializeAsync(locationId);
        await settingsGrain.UpdateAsync(new UpdateCostingSettingsCommand(
            TargetFoodCostPercent: null,
            TargetBeverageCostPercent: null,
            MinimumMarginPercent: null,
            WarningMarginPercent: null,
            PriceChangeAlertThreshold: null,
            CostIncreaseAlertThreshold: threshold,
            AutoRecalculateCosts: null,
            AutoCreateSnapshots: null,
            SnapshotFrequencyDays: null));

        // Act
        var changePercent = previousCost > 0
            ? (currentCost - previousCost) / previousCost * 100
            : 0;
        var shouldAlert = await settingsGrain.ShouldAlertOnCostIncreaseAsync(changePercent);

        // Assert
        shouldAlert.Should().Be(expectAlert);
    }

    // ============================================================================
    // Margin Threshold Tests
    // ============================================================================

    // Given: costing settings with a configured minimum margin percentage
    // When: an actual margin percentage is checked against the minimum
    // Then: a below-minimum flag is raised only when the margin is strictly below the threshold
    [Theory]
    [InlineData(55.0, 50.0, false)]  // Margin 55%, min 50% - OK
    [InlineData(50.0, 50.0, false)]  // Margin at minimum - OK
    [InlineData(49.0, 50.0, true)]   // Margin below minimum - alert
    [InlineData(45.0, 50.0, true)]   // Margin well below minimum - alert
    [InlineData(30.0, 50.0, true)]   // Very low margin - alert
    public async Task CostingSettings_MinimumMarginThreshold_ShouldEvaluateCorrectly(
        decimal actualMargin,
        decimal minimumMargin,
        bool expectBelowMinimum)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var settingsGrain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        await settingsGrain.InitializeAsync(locationId);
        await settingsGrain.UpdateAsync(new UpdateCostingSettingsCommand(
            TargetFoodCostPercent: null,
            TargetBeverageCostPercent: null,
            MinimumMarginPercent: minimumMargin,
            WarningMarginPercent: null,
            PriceChangeAlertThreshold: null,
            CostIncreaseAlertThreshold: null,
            AutoRecalculateCosts: null,
            AutoCreateSnapshots: null,
            SnapshotFrequencyDays: null));

        // Act
        var isBelowMinimum = await settingsGrain.IsMarginBelowMinimumAsync(actualMargin);

        // Assert
        isBelowMinimum.Should().Be(expectBelowMinimum);
    }

    // Given: costing settings with a configured warning margin percentage
    // When: an actual margin percentage is checked against the warning level
    // Then: a below-warning flag is raised only when the margin is strictly below the warning threshold
    [Theory]
    [InlineData(65.0, 60.0, false)]  // Margin 65%, warning 60% - OK
    [InlineData(60.0, 60.0, false)]  // Margin at warning - OK
    [InlineData(59.0, 60.0, true)]   // Margin below warning - alert
    [InlineData(55.0, 60.0, true)]   // Margin well below warning - alert
    public async Task CostingSettings_WarningMarginThreshold_ShouldEvaluateCorrectly(
        decimal actualMargin,
        decimal warningMargin,
        bool expectBelowWarning)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var settingsGrain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        await settingsGrain.InitializeAsync(locationId);
        await settingsGrain.UpdateAsync(new UpdateCostingSettingsCommand(
            TargetFoodCostPercent: null,
            TargetBeverageCostPercent: null,
            MinimumMarginPercent: null,
            WarningMarginPercent: warningMargin,
            PriceChangeAlertThreshold: null,
            CostIncreaseAlertThreshold: null,
            AutoRecalculateCosts: null,
            AutoCreateSnapshots: null,
            SnapshotFrequencyDays: null));

        // Act
        var isBelowWarning = await settingsGrain.IsMarginBelowWarningAsync(actualMargin);

        // Assert
        isBelowWarning.Should().Be(expectBelowWarning);
    }

    // ============================================================================
    // Alert Creation for Different Alert Types
    // ============================================================================

    // Given: a salmon fillet ingredient with a 20% price increase affecting 10 recipes
    // When: an ingredient price increase cost alert is created
    // Then: the alert calculates the correct change percentage and captures the affected recipe count
    [Fact]
    public async Task CostAlert_IngredientPriceIncrease_ShouldCalculateImpactCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostAlertGrain>(
            GrainKeys.CostAlert(orgId, alertId));

        // Act - 20% price increase affecting 10 recipes
        var snapshot = await grain.CreateAsync(new CreateCostAlertCommand(
            AlertType: CostAlertType.IngredientPriceIncrease,
            RecipeId: null,
            RecipeName: null,
            IngredientId: ingredientId,
            IngredientName: "Salmon Fillet",
            MenuItemId: null,
            MenuItemName: null,
            PreviousValue: 25.00m,
            CurrentValue: 30.00m,
            ThresholdValue: 10m,
            ImpactDescription: "Significant price increase from supplier",
            AffectedRecipeCount: 10));

        // Assert
        snapshot.AlertType.Should().Be(CostAlertType.IngredientPriceIncrease);
        snapshot.ChangePercent.Should().Be(20m); // (30-25)/25 * 100
        snapshot.AffectedRecipeCount.Should().Be(10);
        snapshot.IngredientName.Should().Be("Salmon Fillet");
        snapshot.IsAcknowledged.Should().BeFalse();
    }

    // Given: a grilled salmon menu item whose margin dropped from 60% to 45% below the 50% threshold
    // When: a margin-below-threshold cost alert is created
    // Then: the alert captures previous margin, current margin, threshold, and linked recipe/menu item IDs
    [Fact]
    public async Task CostAlert_MarginBelowThreshold_ShouldCaptureMarginData()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var menuItemId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostAlertGrain>(
            GrainKeys.CostAlert(orgId, alertId));

        // Act - margin dropped from 60% to 45%
        var snapshot = await grain.CreateAsync(new CreateCostAlertCommand(
            AlertType: CostAlertType.MarginBelowThreshold,
            RecipeId: recipeId,
            RecipeName: "Grilled Salmon",
            IngredientId: null,
            IngredientName: null,
            MenuItemId: menuItemId,
            MenuItemName: "Grilled Salmon Entree",
            PreviousValue: 60m, // Previous margin %
            CurrentValue: 45m,  // Current margin %
            ThresholdValue: 50m, // Minimum margin threshold
            ImpactDescription: "Margin below minimum threshold due to ingredient cost increases",
            AffectedRecipeCount: 1));

        // Assert
        snapshot.AlertType.Should().Be(CostAlertType.MarginBelowThreshold);
        snapshot.PreviousValue.Should().Be(60m);
        snapshot.CurrentValue.Should().Be(45m);
        snapshot.ThresholdValue.Should().Be(50m);
        snapshot.ChangePercent.Should().Be(-25m); // (45-60)/60 * 100
        snapshot.RecipeId.Should().Be(recipeId);
        snapshot.MenuItemId.Should().Be(menuItemId);
    }

    // Given: a Caesar salad recipe whose cost increased from $5.00 to $6.50
    // When: a recipe cost increase alert is created
    // Then: the alert calculates a 30% cost increase and identifies the affected recipe
    [Fact]
    public async Task CostAlert_RecipeCostIncrease_ShouldTrackCostChange()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostAlertGrain>(
            GrainKeys.CostAlert(orgId, alertId));

        // Act - recipe cost increased from $5 to $6.50
        var snapshot = await grain.CreateAsync(new CreateCostAlertCommand(
            AlertType: CostAlertType.RecipeCostIncrease,
            RecipeId: recipeId,
            RecipeName: "Caesar Salad",
            IngredientId: null,
            IngredientName: null,
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Caesar Salad",
            PreviousValue: 5.00m,
            CurrentValue: 6.50m,
            ThresholdValue: 5m, // 5% threshold
            ImpactDescription: "Cost increased due to lettuce price surge",
            AffectedRecipeCount: 1));

        // Assert
        snapshot.AlertType.Should().Be(CostAlertType.RecipeCostIncrease);
        snapshot.ChangePercent.Should().Be(30m); // (6.50-5)/5 * 100
        snapshot.RecipeName.Should().Be("Caesar Salad");
    }

    // Given: olive oil with a 25% seasonal price decrease affecting 25 recipes
    // When: an ingredient price decrease alert is created
    // Then: the alert captures the negative change percentage and the opportunity to improve margins
    [Fact]
    public async Task CostAlert_IngredientPriceDecrease_ShouldIdentifyOpportunity()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostAlertGrain>(
            GrainKeys.CostAlert(orgId, alertId));

        // Act - significant price decrease (opportunity to improve margins)
        var snapshot = await grain.CreateAsync(new CreateCostAlertCommand(
            AlertType: CostAlertType.IngredientPriceDecrease,
            RecipeId: null,
            RecipeName: null,
            IngredientId: ingredientId,
            IngredientName: "Olive Oil",
            MenuItemId: null,
            MenuItemName: null,
            PreviousValue: 40.00m,
            CurrentValue: 30.00m,
            ThresholdValue: 10m,
            ImpactDescription: "Seasonal price decrease - opportunity to improve margins",
            AffectedRecipeCount: 25));

        // Assert
        snapshot.AlertType.Should().Be(CostAlertType.IngredientPriceDecrease);
        snapshot.ChangePercent.Should().Be(-25m); // (30-40)/40 * 100
        snapshot.AffectedRecipeCount.Should().Be(25);
    }

    // ============================================================================
    // Alert Workflow Tests
    // ============================================================================

    // Given: a cost alert created for a beef tenderloin price increase
    // When: a manager acknowledges the alert with a price-adjusted action and notes
    // Then: the alert transitions from unacknowledged to acknowledged with the user ID and action recorded
    [Fact]
    public async Task CostAlert_FullWorkflow_FromCreationToAcknowledgment()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostAlertGrain>(
            GrainKeys.CostAlert(orgId, alertId));

        // Step 1: Create alert
        await grain.CreateAsync(new CreateCostAlertCommand(
            AlertType: CostAlertType.IngredientPriceIncrease,
            RecipeId: null,
            RecipeName: null,
            IngredientId: Guid.NewGuid(),
            IngredientName: "Beef Tenderloin",
            MenuItemId: null,
            MenuItemName: null,
            PreviousValue: 35.00m,
            CurrentValue: 45.00m,
            ThresholdValue: 10m,
            ImpactDescription: "Major supplier price increase",
            AffectedRecipeCount: 8));

        // Verify initial state
        var initialExists = await grain.ExistsAsync();
        var initialAcknowledged = await grain.IsAcknowledgedAsync();
        initialExists.Should().BeTrue();
        initialAcknowledged.Should().BeFalse();

        // Step 2: Acknowledge with action
        var acknowledgedSnapshot = await grain.AcknowledgeAsync(new AcknowledgeCostAlertCommand(
            AcknowledgedByUserId: userId,
            Notes: "Menu prices have been adjusted to maintain margins",
            ActionTaken: CostAlertAction.PriceAdjusted));

        // Verify final state
        var finalAcknowledged = await grain.IsAcknowledgedAsync();
        finalAcknowledged.Should().BeTrue();
        acknowledgedSnapshot.AcknowledgedByUserId.Should().Be(userId);
        acknowledgedSnapshot.ActionTaken.Should().Be(CostAlertAction.PriceAdjusted);
        acknowledgedSnapshot.AcknowledgedAt.Should().NotBeNull();
    }

    // Given: a recipe cost increase alert awaiting acknowledgment
    // When: the alert is acknowledged with a specific action type
    // Then: the recorded action matches the chosen response and the alert is marked acknowledged
    [Theory]
    [InlineData(CostAlertAction.PriceAdjusted)]
    [InlineData(CostAlertAction.MenuUpdated)]
    [InlineData(CostAlertAction.Accepted)]
    [InlineData(CostAlertAction.Ignored)]
    public async Task CostAlert_DifferentActions_ShouldBeRecordedCorrectly(CostAlertAction action)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostAlertGrain>(
            GrainKeys.CostAlert(orgId, alertId));

        await grain.CreateAsync(new CreateCostAlertCommand(
            AlertType: CostAlertType.RecipeCostIncrease,
            RecipeId: Guid.NewGuid(),
            RecipeName: "Test Recipe",
            IngredientId: null,
            IngredientName: null,
            MenuItemId: null,
            MenuItemName: null,
            PreviousValue: 10.00m,
            CurrentValue: 12.00m,
            ThresholdValue: 5m,
            ImpactDescription: null,
            AffectedRecipeCount: 1));

        // Act
        var snapshot = await grain.AcknowledgeAsync(new AcknowledgeCostAlertCommand(
            AcknowledgedByUserId: Guid.NewGuid(),
            Notes: $"Action taken: {action}",
            ActionTaken: action));

        // Assert
        snapshot.ActionTaken.Should().Be(action);
        snapshot.IsAcknowledged.Should().BeTrue();
    }

    // ============================================================================
    // Edge Cases
    // ============================================================================

    // Given: a newly added ingredient with no previous price (zero previous value)
    // When: a cost alert is created with zero as the previous value
    // Then: the alert is created without error, recording both the zero baseline and current price
    [Fact]
    public async Task CostAlert_ZeroPreviousValue_ShouldHandleGracefully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostAlertGrain>(
            GrainKeys.CostAlert(orgId, alertId));

        // Act - new ingredient (previous value was 0)
        var snapshot = await grain.CreateAsync(new CreateCostAlertCommand(
            AlertType: CostAlertType.IngredientPriceIncrease,
            RecipeId: null,
            RecipeName: null,
            IngredientId: Guid.NewGuid(),
            IngredientName: "New Ingredient",
            MenuItemId: null,
            MenuItemName: null,
            PreviousValue: 0m,
            CurrentValue: 10.00m,
            ThresholdValue: 10m,
            ImpactDescription: "New ingredient added",
            AffectedRecipeCount: 1));

        // Assert - should not throw, change percent may be handled specially
        snapshot.Should().NotBeNull();
        snapshot.PreviousValue.Should().Be(0m);
        snapshot.CurrentValue.Should().Be(10.00m);
    }

    // Given: an ingredient whose price has not actually changed
    // When: a cost alert is created with identical previous and current values
    // Then: the calculated change percentage is zero
    [Fact]
    public async Task CostAlert_SameValue_ShouldShowZeroPercentChange()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostAlertGrain>(
            GrainKeys.CostAlert(orgId, alertId));

        // Act - no actual change (edge case)
        var snapshot = await grain.CreateAsync(new CreateCostAlertCommand(
            AlertType: CostAlertType.IngredientPriceIncrease,
            RecipeId: null,
            RecipeName: null,
            IngredientId: Guid.NewGuid(),
            IngredientName: "Stable Ingredient",
            MenuItemId: null,
            MenuItemName: null,
            PreviousValue: 10.00m,
            CurrentValue: 10.00m,
            ThresholdValue: 10m,
            ImpactDescription: null,
            AffectedRecipeCount: 1));

        // Assert
        snapshot.ChangePercent.Should().Be(0m);
    }

    // Given: costing settings with a minimum margin of 50% and warning margin of 60%
    // When: margins at 65%, 55%, and 45% are evaluated against both thresholds
    // Then: the correct combination of below-minimum and below-warning flags is returned for each level
    [Fact]
    public async Task CostingSettings_MultipleThresholdsInteraction()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        await grain.InitializeAsync(locationId);

        // Set up hierarchical thresholds: minimum 50%, warning 60%
        await grain.UpdateAsync(new UpdateCostingSettingsCommand(
            TargetFoodCostPercent: null,
            TargetBeverageCostPercent: null,
            MinimumMarginPercent: 50m,
            WarningMarginPercent: 60m,
            PriceChangeAlertThreshold: null,
            CostIncreaseAlertThreshold: null,
            AutoRecalculateCosts: null,
            AutoCreateSnapshots: null,
            SnapshotFrequencyDays: null));

        // Act & Assert - test margin at different levels

        // 65% margin - above both thresholds
        (await grain.IsMarginBelowMinimumAsync(65m)).Should().BeFalse();
        (await grain.IsMarginBelowWarningAsync(65m)).Should().BeFalse();

        // 55% margin - below warning but above minimum
        (await grain.IsMarginBelowMinimumAsync(55m)).Should().BeFalse();
        (await grain.IsMarginBelowWarningAsync(55m)).Should().BeTrue();

        // 45% margin - below both thresholds
        (await grain.IsMarginBelowMinimumAsync(45m)).Should().BeTrue();
        (await grain.IsMarginBelowWarningAsync(45m)).Should().BeTrue();
    }

    // ============================================================================
    // Alert Index Integration Tests
    // ============================================================================

    // Given: a cost alert index with three active and two acknowledged alerts registered
    // When: querying for active alerts, acknowledged alerts, and total count
    // Then: the index returns correct counts and correctly partitions alerts by status
    [Fact]
    public async Task CostAlertIndex_RegisterMultipleAlerts_ShouldTrackAllCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var indexKey = GrainKeys.Index(orgId, "costalerts", "all");
        var indexGrain = _cluster.GrainFactory.GetGrain<ICostAlertIndexGrain>(indexKey);

        var activeAlerts = new List<CostAlertSummary>();
        var acknowledgedAlerts = new List<CostAlertSummary>();

        // Create 3 active alerts
        for (int i = 0; i < 3; i++)
        {
            var summary = new CostAlertSummary(
                Guid.NewGuid(),
                CostAlertType.IngredientPriceIncrease,
                null,
                $"Ingredient {i}",
                null,
                10m + i * 5,
                false,
                CostAlertAction.None,
                DateTime.UtcNow.AddHours(-i),
                null);
            activeAlerts.Add(summary);
            await indexGrain.RegisterAsync(summary.AlertId, summary);
        }

        // Create 2 acknowledged alerts
        for (int i = 0; i < 2; i++)
        {
            var summary = new CostAlertSummary(
                Guid.NewGuid(),
                CostAlertType.RecipeCostIncrease,
                $"Recipe {i}",
                null,
                null,
                5m + i * 2,
                true,
                CostAlertAction.PriceAdjusted,
                DateTime.UtcNow.AddDays(-i - 1),
                DateTime.UtcNow.AddHours(-i));
            acknowledgedAlerts.Add(summary);
            await indexGrain.RegisterAsync(summary.AlertId, summary);
        }

        // Act
        var activeCount = await indexGrain.GetActiveCountAsync();
        var allIds = await indexGrain.GetAllAlertIdsAsync();

        var activeQuery = await indexGrain.QueryAsync(new CostAlertQuery(
            Status: CostAlertStatus.Active,
            AlertType: null,
            FromDate: null,
            ToDate: null));

        var acknowledgedQuery = await indexGrain.QueryAsync(new CostAlertQuery(
            Status: CostAlertStatus.Acknowledged,
            AlertType: null,
            FromDate: null,
            ToDate: null));

        // Assert
        activeCount.Should().Be(3);
        allIds.Should().HaveCount(5);
        activeQuery.Alerts.Should().HaveCount(3);
        activeQuery.ActiveCount.Should().Be(3);
        acknowledgedQuery.Alerts.Should().HaveCount(2);
        acknowledgedQuery.AcknowledgedCount.Should().Be(2);
    }

    // Given: a cost alert index containing alerts of different types (price increase, margin, recipe cost)
    // When: querying by a specific alert type
    // Then: only alerts matching the requested type are returned
    [Fact]
    public async Task CostAlertIndex_QueryByAlertType_ShouldFilterCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var indexKey = GrainKeys.Index(orgId, "costalerts", "all");
        var indexGrain = _cluster.GrainFactory.GetGrain<ICostAlertIndexGrain>(indexKey);

        // Register alerts of different types
        var priceIncreaseAlert = new CostAlertSummary(
            Guid.NewGuid(), CostAlertType.IngredientPriceIncrease, null, "Ingredient A", null, 15m, false, CostAlertAction.None, DateTime.UtcNow, null);
        var marginAlert = new CostAlertSummary(
            Guid.NewGuid(), CostAlertType.MarginBelowThreshold, "Recipe A", null, null, -10m, false, CostAlertAction.None, DateTime.UtcNow, null);
        var recipeCostAlert = new CostAlertSummary(
            Guid.NewGuid(), CostAlertType.RecipeCostIncrease, "Recipe B", null, null, 20m, false, CostAlertAction.None, DateTime.UtcNow, null);

        await indexGrain.RegisterAsync(priceIncreaseAlert.AlertId, priceIncreaseAlert);
        await indexGrain.RegisterAsync(marginAlert.AlertId, marginAlert);
        await indexGrain.RegisterAsync(recipeCostAlert.AlertId, recipeCostAlert);

        // Act
        var priceIncreaseQuery = await indexGrain.QueryAsync(new CostAlertQuery(
            Status: null,
            AlertType: CostAlertType.IngredientPriceIncrease,
            FromDate: null,
            ToDate: null));

        var marginQuery = await indexGrain.QueryAsync(new CostAlertQuery(
            Status: null,
            AlertType: CostAlertType.MarginBelowThreshold,
            FromDate: null,
            ToDate: null));

        // Assert
        priceIncreaseQuery.Alerts.Should().HaveCount(1);
        priceIncreaseQuery.Alerts[0].AlertType.Should().Be(CostAlertType.IngredientPriceIncrease);

        marginQuery.Alerts.Should().HaveCount(1);
        marginQuery.Alerts[0].AlertType.Should().Be(CostAlertType.MarginBelowThreshold);
    }
}
