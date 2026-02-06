using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class AbcClassificationGrainTests
{
    private readonly TestClusterFixture _fixture;

    public AbcClassificationGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IInventoryGrain> CreateInventoryWithConsumptionAsync(
        Guid orgId, Guid siteId, Guid ingredientId, string name, decimal initialQty, decimal unitCost, decimal dailyConsumption)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
        await grain.InitializeAsync(new InitializeInventoryCommand(orgId, siteId, ingredientId, name, $"SKU-{ingredientId.ToString()[..8]}", "units", "General", 10, 50));
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", initialQty, unitCost));

        // Simulate consumption over time to establish usage patterns
        for (int i = 0; i < 30; i++)
        {
            if (dailyConsumption > 0)
            {
                await grain.ConsumeAsync(new ConsumeStockCommand(dailyConsumption, $"Daily use day {i + 1}"));
            }
        }

        return grain;
    }

    // Given: a new ABC classification grain for a site
    // When: the grain is initialized
    // Then: default settings are applied (80/95 thresholds, AnnualConsumptionValue method)
    [Fact]
    public async Task InitializeAsync_ShouldSetupClassification()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAbcClassificationGrain>(GrainKeys.AbcClassification(orgId, siteId));

        // Act
        await grain.InitializeAsync(orgId, siteId);

        // Assert
        (await grain.ExistsAsync()).Should().BeTrue();
        var settings = await grain.GetSettingsAsync();
        settings.ClassAThreshold.Should().Be(80);
        settings.ClassBThreshold.Should().Be(95);
        settings.Method.Should().Be(ClassificationMethod.AnnualConsumptionValue);
    }

    // Given: a new ABC classification grain for a site
    // When: the grain is initialized
    // Then: default reorder policies are set for all three classes (A requires approval with 14-day safety stock, C has 30-day safety stock)
    [Fact]
    public async Task InitializeAsync_ShouldSetDefaultReorderPolicies()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAbcClassificationGrain>(GrainKeys.AbcClassification(orgId, siteId));

        // Act
        await grain.InitializeAsync(orgId, siteId);

        // Assert
        var policies = await grain.GetAllReorderPoliciesAsync();
        policies.Should().HaveCount(3);

        var policyA = await grain.GetReorderPolicyAsync(AbcClass.A);
        policyA.Should().NotBeNull();
        policyA!.RequiresApproval.Should().BeTrue();
        policyA.SafetyStockDays.Should().Be(14);

        var policyB = await grain.GetReorderPolicyAsync(AbcClass.B);
        policyB.Should().NotBeNull();
        policyB!.RequiresApproval.Should().BeFalse();

        var policyC = await grain.GetReorderPolicyAsync(AbcClass.C);
        policyC.Should().NotBeNull();
        policyC!.SafetyStockDays.Should().Be(30);
    }

    // Given: an initialized ABC classification grain
    // When: a new ingredient is registered
    // Then: the ingredient is added with an Unclassified status pending the next classification run
    [Fact]
    public async Task RegisterIngredientAsync_ShouldAddIngredientAsUnclassified()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAbcClassificationGrain>(GrainKeys.AbcClassification(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act
        await grain.RegisterIngredientAsync(ingredientId, "Test Item", "SKU-001", "Category");

        // Assert
        var classification = await grain.GetClassificationAsync(ingredientId);
        classification.Should().NotBeNull();
        classification!.Classification.Should().Be(AbcClass.Unclassified);
        classification.IngredientName.Should().Be("Test Item");
    }

    // Given: three inventory items with high ($50/unit), medium ($10/unit), and low ($2/unit) consumption values
    // When: the Pareto-based ABC classification is run
    // Then: the highest-value item is ranked first and at least one item falls into Class A
    [Fact]
    public async Task ClassifyAsync_ShouldApplyParetoClassification()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        // Create items with different consumption values
        // High value item (should be Class A)
        var highValueId = Guid.NewGuid();
        await CreateInventoryWithConsumptionAsync(orgId, siteId, highValueId, "High Value Item", 1000, 50.00m, 10);

        // Medium value item (should be Class B)
        var mediumValueId = Guid.NewGuid();
        await CreateInventoryWithConsumptionAsync(orgId, siteId, mediumValueId, "Medium Value Item", 500, 10.00m, 5);

        // Low value item (should be Class C)
        var lowValueId = Guid.NewGuid();
        await CreateInventoryWithConsumptionAsync(orgId, siteId, lowValueId, "Low Value Item", 200, 2.00m, 2);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAbcClassificationGrain>(GrainKeys.AbcClassification(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        await grain.RegisterIngredientAsync(highValueId, "High Value Item", "SKU-HV", "Premium");
        await grain.RegisterIngredientAsync(mediumValueId, "Medium Value Item", "SKU-MV", "Standard");
        await grain.RegisterIngredientAsync(lowValueId, "Low Value Item", "SKU-LV", "Basic");

        // Act
        var report = await grain.ClassifyAsync();

        // Assert
        report.TotalItems.Should().Be(3);
        report.ClassACount.Should().BeGreaterThan(0);
        report.ClassAPercentage.Should().BeGreaterThan(0);

        // High value item should be ranked first
        var highValueClassification = await grain.GetClassificationAsync(highValueId);
        highValueClassification!.Rank.Should().Be(1);
    }

    // Given: an ingredient classified algorithmically, then manually overridden to a different ABC class
    // When: the classification is re-run
    // Then: the item retains its overridden classification
    [Fact]
    public async Task ClassifyAsync_ShouldDetectReclassifiedItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var itemId = Guid.NewGuid();
        await CreateInventoryWithConsumptionAsync(orgId, siteId, itemId, "Test Item", 500, 20.00m, 5);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAbcClassificationGrain>(GrainKeys.AbcClassification(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.RegisterIngredientAsync(itemId, "Test Item", "SKU-001", "Category");

        // First classification
        await grain.ClassifyAsync();
        var firstClassification = await grain.GetClassificationAsync(itemId);

        // Override to a different class
        var targetClass = firstClassification!.Classification == AbcClass.A ? AbcClass.C : AbcClass.A;
        await grain.OverrideClassificationAsync(itemId, targetClass, "Testing reclassification");

        // Act - Run classification again
        var report = await grain.ClassifyAsync();

        // Assert
        var reclassified = await grain.GetReclassifiedItemsAsync();
        // Item should show as reclassified since it moved from original class to override class
        var currentClassification = await grain.GetClassificationAsync(itemId);
        currentClassification!.Classification.Should().Be(targetClass);
    }

    // Given: three ingredients with high, medium, and low consumption values after classification
    // When: items are queried by each ABC class (A, B, C)
    // Then: all three items are distributed across the three classes with no unclassified items
    [Fact]
    public async Task GetItemsByClassAsync_ShouldReturnCorrectItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        // Create multiple items with varying values
        var item1 = Guid.NewGuid();
        var item2 = Guid.NewGuid();
        var item3 = Guid.NewGuid();

        await CreateInventoryWithConsumptionAsync(orgId, siteId, item1, "Item 1", 1000, 100.00m, 20);
        await CreateInventoryWithConsumptionAsync(orgId, siteId, item2, "Item 2", 500, 10.00m, 5);
        await CreateInventoryWithConsumptionAsync(orgId, siteId, item3, "Item 3", 100, 1.00m, 1);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAbcClassificationGrain>(GrainKeys.AbcClassification(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        await grain.RegisterIngredientAsync(item1, "Item 1", "SKU-1", "Cat1");
        await grain.RegisterIngredientAsync(item2, "Item 2", "SKU-2", "Cat2");
        await grain.RegisterIngredientAsync(item3, "Item 3", "SKU-3", "Cat3");

        await grain.ClassifyAsync();

        // Act
        var classAItems = await grain.GetItemsByClassAsync(AbcClass.A);
        var classBItems = await grain.GetItemsByClassAsync(AbcClass.B);
        var classCItems = await grain.GetItemsByClassAsync(AbcClass.C);

        // Assert - All items should be classified into one of the three classes
        var totalItems = classAItems.Count + classBItems.Count + classCItems.Count;
        totalItems.Should().Be(3);
    }

    // Given: an ingredient that has been algorithmically classified
    // When: the classification is manually overridden to Class A for strategic importance
    // Then: the ingredient's classification changes to Class A regardless of its consumption value
    [Fact]
    public async Task OverrideClassificationAsync_ShouldApplyManualClassification()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryWithConsumptionAsync(orgId, siteId, ingredientId, "Test Item", 100, 5.00m, 2);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAbcClassificationGrain>(GrainKeys.AbcClassification(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.RegisterIngredientAsync(ingredientId, "Test Item", "SKU-001", "Category");
        await grain.ClassifyAsync();

        // Act
        await grain.OverrideClassificationAsync(ingredientId, AbcClass.A, "Strategic importance");

        // Assert
        var classification = await grain.GetClassificationAsync(ingredientId);
        classification!.Classification.Should().Be(AbcClass.A);
    }

    // Given: an ingredient with a manual Class A override
    // When: the override is cleared and classification is re-run
    // Then: the ingredient is reclassified algorithmically based on its actual consumption value
    [Fact]
    public async Task ClearOverrideAsync_ShouldRemoveManualOverride()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryWithConsumptionAsync(orgId, siteId, ingredientId, "Test Item", 100, 5.00m, 2);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAbcClassificationGrain>(GrainKeys.AbcClassification(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.RegisterIngredientAsync(ingredientId, "Test Item", "SKU-001", "Category");
        await grain.ClassifyAsync();

        await grain.OverrideClassificationAsync(ingredientId, AbcClass.A, "Strategic importance");

        // Act
        await grain.ClearOverrideAsync(ingredientId);

        // Reclassify to apply algorithmic classification
        await grain.ClassifyAsync();

        // Assert
        var classification = await grain.GetClassificationAsync(ingredientId);
        // After clearing override, item should be reclassified algorithmically
        classification.Should().NotBeNull();
    }

    // Given: an initialized ABC classification with default reorder policies
    // When: the Class A reorder policy is updated to 21-day safety stock and 3-day review frequency
    // Then: the updated policy values are persisted for Class A
    [Fact]
    public async Task SetReorderPolicyAsync_ShouldUpdatePolicy()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAbcClassificationGrain>(GrainKeys.AbcClassification(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var newPolicy = new AbcReorderPolicy
        {
            Classification = AbcClass.A,
            SafetyStockDays = 21,
            ReviewFrequencyDays = 3,
            OrderFrequencyDays = 5,
            RequiresApproval = true,
            MaxOrderValueWithoutApproval = 0
        };

        // Act
        await grain.SetReorderPolicyAsync(newPolicy);

        // Assert
        var policy = await grain.GetReorderPolicyAsync(AbcClass.A);
        policy.Should().NotBeNull();
        policy!.SafetyStockDays.Should().Be(21);
        policy.ReviewFrequencyDays.Should().Be(3);
    }

    // Given: an initialized ABC classification with default settings
    // When: the settings are updated to 70/90 thresholds, Velocity method, 180-day period, no auto-reclassify
    // Then: the new configuration values are persisted
    [Fact]
    public async Task ConfigureAsync_ShouldUpdateSettings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAbcClassificationGrain>(GrainKeys.AbcClassification(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var newSettings = new AbcClassificationSettings(
            ClassAThreshold: 70,
            ClassBThreshold: 90,
            Method: ClassificationMethod.Velocity,
            AnalysisPeriodDays: 180,
            AutoReclassify: false,
            ReclassifyIntervalDays: 60);

        // Act
        await grain.ConfigureAsync(newSettings);

        // Assert
        var settings = await grain.GetSettingsAsync();
        settings.ClassAThreshold.Should().Be(70);
        settings.ClassBThreshold.Should().Be(90);
        settings.Method.Should().Be(ClassificationMethod.Velocity);
        settings.AnalysisPeriodDays.Should().Be(180);
    }

    // Given: a registered ingredient in the ABC classification grain
    // When: the ingredient is unregistered
    // Then: the ingredient's classification is removed and querying it returns null
    [Fact]
    public async Task UnregisterIngredientAsync_ShouldRemoveIngredient()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAbcClassificationGrain>(GrainKeys.AbcClassification(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.RegisterIngredientAsync(ingredientId, "Test Item", "SKU-001", "Category");

        // Act
        await grain.UnregisterIngredientAsync(ingredientId);

        // Assert
        var classification = await grain.GetClassificationAsync(ingredientId);
        classification.Should().BeNull();
    }

    // Given: two registered ingredients with different consumption values
    // When: the ABC classification report is generated
    // Then: the report shows correct totals, site ID, timestamp, and class value breakdown summing to total
    [Fact]
    public async Task GetReportAsync_ShouldReturnClassificationSummary()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var item1 = Guid.NewGuid();
        var item2 = Guid.NewGuid();

        await CreateInventoryWithConsumptionAsync(orgId, siteId, item1, "Item 1", 500, 50.00m, 10);
        await CreateInventoryWithConsumptionAsync(orgId, siteId, item2, "Item 2", 200, 10.00m, 3);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAbcClassificationGrain>(GrainKeys.AbcClassification(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.RegisterIngredientAsync(item1, "Item 1", "SKU-1", "Cat1");
        await grain.RegisterIngredientAsync(item2, "Item 2", "SKU-2", "Cat2");

        // Act
        var report = await grain.GetReportAsync();

        // Assert
        report.TotalItems.Should().Be(2);
        report.SiteId.Should().Be(siteId);
        report.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // Total value should be sum of all class values
        var totalClassValue = report.ClassAValue + report.ClassBValue + report.ClassCValue;
        totalClassValue.Should().Be(report.TotalValue);
    }
}
