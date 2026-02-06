using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class AlertRuleEvaluationTests
{
    private readonly TestClusterFixture _fixture;

    public AlertRuleEvaluationTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private string GetGrainKey(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:alerts";

    #region Low Stock Rule Tests

    // Given: an ingredient with 5 units on hand and a reorder point of 20
    // When: alert rules are evaluated against the inventory metrics
    // Then: a LowStock alert is triggered for the ingredient with the actual value in metadata
    [Fact]
    public async Task EvaluateRulesAsync_LowStock_WhenBelowReorderPoint_ShouldTrigger()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var metrics = new MetricsSnapshot
        {
            EntityId = ingredientId,
            EntityType = "Ingredient",
            EntityName = "Ground Beef",
            Metrics = new Dictionary<string, decimal>
            {
                { "QuantityOnHand", 5 },
                { "ReorderPoint", 20 }
            }
        };

        // Act
        var triggeredAlerts = await grain.EvaluateRulesAsync(metrics);

        // Assert
        triggeredAlerts.Should().ContainSingle(a => a.Type == AlertType.LowStock);
        var alert = triggeredAlerts.First(a => a.Type == AlertType.LowStock);
        alert.Title.Should().Contain("Ground Beef");
        alert.EntityId.Should().Be(ingredientId);
        alert.Metadata.Should().ContainKey("actualValue");
    }

    // Given: an ingredient with 50 units on hand and a reorder point of 20
    // When: alert rules are evaluated against the inventory metrics
    // Then: no LowStock alert is triggered because stock is above the reorder point
    [Fact]
    public async Task EvaluateRulesAsync_LowStock_WhenAboveReorderPoint_ShouldNotTrigger()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var metrics = new MetricsSnapshot
        {
            EntityId = Guid.NewGuid(),
            EntityType = "Ingredient",
            EntityName = "Ground Beef",
            Metrics = new Dictionary<string, decimal>
            {
                { "QuantityOnHand", 50 },
                { "ReorderPoint", 20 }
            }
        };

        // Act
        var triggeredAlerts = await grain.EvaluateRulesAsync(metrics);

        // Assert
        triggeredAlerts.Should().NotContain(a => a.Type == AlertType.LowStock);
    }

    #endregion

    #region Out of Stock Rule Tests

    // Given: an ingredient with zero units on hand
    // When: alert rules are evaluated against the inventory metrics
    // Then: an OutOfStock alert is triggered
    [Fact]
    public async Task EvaluateRulesAsync_OutOfStock_WhenZero_ShouldTrigger()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var metrics = new MetricsSnapshot
        {
            EntityId = Guid.NewGuid(),
            EntityType = "Ingredient",
            EntityName = "Chicken Breast",
            Metrics = new Dictionary<string, decimal>
            {
                { "QuantityOnHand", 0 }
            }
        };

        // Act
        var triggeredAlerts = await grain.EvaluateRulesAsync(metrics);

        // Assert
        triggeredAlerts.Should().ContainSingle(a => a.Type == AlertType.OutOfStock);
    }

    #endregion

    #region Negative Stock Rule Tests

    // Given: an ingredient with a negative quantity of -5 units (inventory discrepancy)
    // When: alert rules are evaluated against the inventory metrics
    // Then: a NegativeStock alert is triggered at Critical severity
    [Fact]
    public async Task EvaluateRulesAsync_NegativeStock_WhenNegative_ShouldTrigger()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var metrics = new MetricsSnapshot
        {
            EntityId = Guid.NewGuid(),
            EntityType = "Ingredient",
            EntityName = "Tomatoes",
            Metrics = new Dictionary<string, decimal>
            {
                { "QuantityOnHand", -5 }
            }
        };

        // Act
        var triggeredAlerts = await grain.EvaluateRulesAsync(metrics);

        // Assert
        triggeredAlerts.Should().ContainSingle(a => a.Type == AlertType.NegativeStock);
        var alert = triggeredAlerts.First(a => a.Type == AlertType.NegativeStock);
        alert.Severity.Should().Be(AlertSeverity.Critical);
    }

    #endregion

    #region Expiry Risk Rule Tests

    // Given: a perishable ingredient expiring in 2 days (within the default threshold)
    // When: alert rules are evaluated against the expiry metrics
    // Then: an ExpiryRisk alert is triggered with the days-until-expiry in the message
    [Fact]
    public async Task EvaluateRulesAsync_ExpiryRisk_WhenWithinThreshold_ShouldTrigger()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var metrics = new MetricsSnapshot
        {
            EntityId = Guid.NewGuid(),
            EntityType = "Ingredient",
            EntityName = "Milk",
            Metrics = new Dictionary<string, decimal>
            {
                { "DaysUntilExpiry", 2 }
            }
        };

        // Act
        var triggeredAlerts = await grain.EvaluateRulesAsync(metrics);

        // Assert
        triggeredAlerts.Should().ContainSingle(a => a.Type == AlertType.ExpiryRisk);
        var alert = triggeredAlerts.First(a => a.Type == AlertType.ExpiryRisk);
        alert.Message.Should().Contain("2");
    }

    // Given: a shelf-stable ingredient expiring in 90 days (well beyond the threshold)
    // When: alert rules are evaluated against the expiry metrics
    // Then: no ExpiryRisk alert is triggered
    [Fact]
    public async Task EvaluateRulesAsync_ExpiryRisk_WhenFarFromExpiry_ShouldNotTrigger()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var metrics = new MetricsSnapshot
        {
            EntityId = Guid.NewGuid(),
            EntityType = "Ingredient",
            EntityName = "Canned Goods",
            Metrics = new Dictionary<string, decimal>
            {
                { "DaysUntilExpiry", 90 }
            }
        };

        // Act
        var triggeredAlerts = await grain.EvaluateRulesAsync(metrics);

        // Assert
        triggeredAlerts.Should().NotContain(a => a.Type == AlertType.ExpiryRisk);
    }

    #endregion

    #region GP Dropped Rule Tests

    // Given: a site with gross profit at 58% this week vs 65% last week (7-point drop)
    // When: alert rules are evaluated against the profitability metrics
    // Then: a GPDropped alert is triggered due to the significant margin decline
    [Fact]
    public async Task EvaluateRulesAsync_GPDropped_WhenSignificantDrop_ShouldTrigger()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var metrics = new MetricsSnapshot
        {
            EntityType = "Site",
            EntityName = "Main Restaurant",
            Metrics = new Dictionary<string, decimal>
            {
                { "GrossProfitPercent", 58 },
                { "GrossProfitPercentLastWeek", 65 }
            }
        };

        // Act
        var triggeredAlerts = await grain.EvaluateRulesAsync(metrics);

        // Assert
        triggeredAlerts.Should().ContainSingle(a => a.Type == AlertType.GPDropped);
    }

    #endregion

    #region High Variance Rule Tests

    // Given: a site with an 18% COGS variance (actual vs theoretical cost)
    // When: alert rules are evaluated against the costing metrics
    // Then: a HighVariance alert is triggered because the variance exceeds the threshold
    [Fact]
    public async Task EvaluateRulesAsync_HighVariance_WhenExceedsThreshold_ShouldTrigger()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var metrics = new MetricsSnapshot
        {
            EntityType = "Site",
            EntityName = "Main Restaurant",
            Metrics = new Dictionary<string, decimal>
            {
                { "COGSVariancePercent", 18 }
            }
        };

        // Act
        var triggeredAlerts = await grain.EvaluateRulesAsync(metrics);

        // Assert
        triggeredAlerts.Should().ContainSingle(a => a.Type == AlertType.HighVariance);
    }

    #endregion

    #region Supplier Price Spike Rule Tests

    // Given: a supplier whose item price increased by 15%
    // When: alert rules are evaluated against the supplier pricing metrics
    // Then: a SupplierPriceSpike alert is triggered
    [Fact]
    public async Task EvaluateRulesAsync_SupplierPriceSpike_WhenSignificantIncrease_ShouldTrigger()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var metrics = new MetricsSnapshot
        {
            EntityId = Guid.NewGuid(),
            EntityType = "Supplier",
            EntityName = "ABC Meats",
            Metrics = new Dictionary<string, decimal>
            {
                { "PriceChangePercent", 15 }
            }
        };

        // Act
        var triggeredAlerts = await grain.EvaluateRulesAsync(metrics);

        // Assert
        triggeredAlerts.Should().ContainSingle(a => a.Type == AlertType.SupplierPriceSpike);
    }

    #endregion

    #region Cooldown Period Tests

    // Given: a LowStock alert that was already triggered for an ingredient
    // When: the same metrics are evaluated again immediately
    // Then: the alert does not re-trigger due to the cooldown period
    [Fact]
    public async Task EvaluateRulesAsync_WithCooldown_ShouldNotTriggerDuringCooldown()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var metrics = new MetricsSnapshot
        {
            EntityId = Guid.NewGuid(),
            EntityType = "Ingredient",
            EntityName = "Ground Beef",
            Metrics = new Dictionary<string, decimal>
            {
                { "QuantityOnHand", 5 },
                { "ReorderPoint", 20 }
            }
        };

        // First evaluation - should trigger
        var firstAlerts = await grain.EvaluateRulesAsync(metrics);
        firstAlerts.Should().ContainSingle(a => a.Type == AlertType.LowStock);

        // Second evaluation immediately after - should NOT trigger due to cooldown
        var secondAlerts = await grain.EvaluateRulesAsync(metrics);
        secondAlerts.Should().NotContain(a => a.Type == AlertType.LowStock);
    }

    #endregion

    #region Disabled Rule Tests

    // Given: the LowStock alert rule has been disabled for the site
    // When: inventory metrics below the reorder point are evaluated
    // Then: no LowStock alert is triggered because the rule is disabled
    [Fact]
    public async Task EvaluateRulesAsync_DisabledRule_ShouldNotTrigger()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Disable the LowStock rule
        var rules = await grain.GetRulesAsync();
        var lowStockRule = rules.First(r => r.Type == AlertType.LowStock);
        await grain.UpdateRuleAsync(lowStockRule with { IsEnabled = false });

        var metrics = new MetricsSnapshot
        {
            EntityId = Guid.NewGuid(),
            EntityType = "Ingredient",
            EntityName = "Ground Beef",
            Metrics = new Dictionary<string, decimal>
            {
                { "QuantityOnHand", 5 },
                { "ReorderPoint", 20 }
            }
        };

        // Act
        var triggeredAlerts = await grain.EvaluateRulesAsync(metrics);

        // Assert
        triggeredAlerts.Should().NotContain(a => a.Type == AlertType.LowStock);
    }

    #endregion

    #region Multiple Rules Tests

    // Given: an ingredient with a quantity of -2 units (both out of stock and negative)
    // When: alert rules are evaluated against the inventory metrics
    // Then: both OutOfStock and NegativeStock alerts are triggered simultaneously
    [Fact]
    public async Task EvaluateRulesAsync_MultipleConditions_ShouldTriggerAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Metrics that should trigger both OutOfStock and NegativeStock
        var metrics = new MetricsSnapshot
        {
            EntityId = Guid.NewGuid(),
            EntityType = "Ingredient",
            EntityName = "Problematic Item",
            Metrics = new Dictionary<string, decimal>
            {
                { "QuantityOnHand", -2 } // Both out of stock and negative
            }
        };

        // Act
        var triggeredAlerts = await grain.EvaluateRulesAsync(metrics);

        // Assert
        triggeredAlerts.Should().Contain(a => a.Type == AlertType.OutOfStock);
        triggeredAlerts.Should().Contain(a => a.Type == AlertType.NegativeStock);
    }

    #endregion

    #region Context Tests

    // Given: an out-of-stock ingredient with additional context (last order ID and consumption time)
    // When: alert rules are evaluated with context metadata
    // Then: the triggered alert's metadata includes the context information from the metrics snapshot
    [Fact]
    public async Task EvaluateRulesAsync_WithContext_ShouldIncludeInMetadata()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAlertGrain>(GetGrainKey(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var metrics = new MetricsSnapshot
        {
            EntityId = Guid.NewGuid(),
            EntityType = "Ingredient",
            EntityName = "Ground Beef",
            Metrics = new Dictionary<string, decimal>
            {
                { "QuantityOnHand", 0 }
            },
            Context = new Dictionary<string, string>
            {
                { "lastOrderId", "12345" },
                { "lastConsumptionTime", "2024-01-15T10:30:00Z" }
            }
        };

        // Act
        var triggeredAlerts = await grain.EvaluateRulesAsync(metrics);

        // Assert
        triggeredAlerts.Should().NotBeEmpty();
        var alert = triggeredAlerts[0];
        alert.Metadata.Should().ContainKey("lastOrderId");
        alert.Metadata!["lastOrderId"].Should().Be("12345");
    }

    #endregion
}
