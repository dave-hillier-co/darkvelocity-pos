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
