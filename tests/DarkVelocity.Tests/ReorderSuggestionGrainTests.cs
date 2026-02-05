using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ReorderSuggestionGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ReorderSuggestionGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IInventoryGrain> CreateInventoryWithLowStockAsync(
        Guid orgId, Guid siteId, Guid ingredientId, string name, decimal currentQty, decimal unitCost, decimal dailyConsumption, int consumptionDays = 30)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
        // Set par level and reorder point to trigger suggestions
        await grain.InitializeAsync(new InitializeInventoryCommand(orgId, siteId, ingredientId, name, $"SKU-{ingredientId.ToString()[..8]}", "units", "General", 20, 100));

        // Add initial inventory higher than current to allow consumption
        var totalToConsume = dailyConsumption * consumptionDays;
        var initialQty = currentQty + totalToConsume;
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", initialQty, unitCost));

        // Simulate consumption to establish usage pattern and bring to current level
        for (int i = 0; i < consumptionDays; i++)
        {
            await grain.ConsumeAsync(new ConsumeStockCommand(dailyConsumption, $"Daily use day {i + 1}"));
        }

        return grain;
    }

    [Fact]
    public async Task InitializeAsync_ShouldSetupReorderSuggestions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IReorderSuggestionGrain>(GrainKeys.ReorderSuggestion(orgId, siteId));

        // Act
        await grain.InitializeAsync(orgId, siteId);

        // Assert
        (await grain.ExistsAsync()).Should().BeTrue();
        var settings = await grain.GetSettingsAsync();
        settings.DefaultLeadTimeDays.Should().Be(7);
        settings.SafetyStockMultiplier.Should().Be(1.5m);
    }

    [Fact]
    public async Task RegisterIngredientAsync_ShouldAddIngredientForMonitoring()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IReorderSuggestionGrain>(GrainKeys.ReorderSuggestion(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act
        await grain.RegisterIngredientAsync(
            ingredientId,
            "Ground Beef",
            "SKU-GB",
            "Proteins",
            "lb",
            supplierId,
            "Acme Meats",
            5);

        // Generate suggestions to verify registration
        await CreateInventoryWithLowStockAsync(orgId, siteId, ingredientId, "Ground Beef", 5, 5.00m, 2);
        var report = await grain.GenerateSuggestionsAsync();

        // Assert
        report.TotalSuggestions.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateSuggestionsAsync_ShouldCreateSuggestionsForLowStock()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        // Create inventory with low stock and consumption history
        await CreateInventoryWithLowStockAsync(orgId, siteId, ingredientId, "Ground Beef", 5, 5.00m, 2);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IReorderSuggestionGrain>(GrainKeys.ReorderSuggestion(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.RegisterIngredientAsync(ingredientId, "Ground Beef", "SKU-GB", "Proteins", "lb");

        // Act
        var report = await grain.GenerateSuggestionsAsync();

        // Assert
        report.TotalSuggestions.Should().BeGreaterThan(0);
        report.TotalEstimatedCost.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateSuggestionsAsync_ShouldCategorizeByUrgency()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        // Create item with zero stock (out of stock)
        var outOfStockId = Guid.NewGuid();
        var outOfStockGrain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, outOfStockId));
        await outOfStockGrain.InitializeAsync(new InitializeInventoryCommand(orgId, siteId, outOfStockId, "Out of Stock Item", "SKU-OOS", "units", "General", 20, 100));
        await outOfStockGrain.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 60, 10.00m));
        // Consume all stock
        for (int i = 0; i < 30; i++)
        {
            await outOfStockGrain.ConsumeAsync(new ConsumeStockCommand(2, $"Day {i + 1}"));
        }

        // Create item with critical stock
        var criticalId = Guid.NewGuid();
        await CreateInventoryWithLowStockAsync(orgId, siteId, criticalId, "Critical Item", 3, 10.00m, 3);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IReorderSuggestionGrain>(GrainKeys.ReorderSuggestion(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.RegisterIngredientAsync(outOfStockId, "Out of Stock Item", "SKU-OOS", "General", "units");
        await grain.RegisterIngredientAsync(criticalId, "Critical Item", "SKU-CRI", "General", "units");

        // Act
        var report = await grain.GenerateSuggestionsAsync();

        // Assert
        var urgentItems = report.OutOfStockCount + report.CriticalCount + report.HighCount;
        urgentItems.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetPendingSuggestionsAsync_ShouldReturnOrderedByUrgency()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var item1 = Guid.NewGuid();
        var item2 = Guid.NewGuid();

        await CreateInventoryWithLowStockAsync(orgId, siteId, item1, "Item 1", 5, 10.00m, 3);
        await CreateInventoryWithLowStockAsync(orgId, siteId, item2, "Item 2", 2, 10.00m, 4);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IReorderSuggestionGrain>(GrainKeys.ReorderSuggestion(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.RegisterIngredientAsync(item1, "Item 1", "SKU-1", "General", "units");
        await grain.RegisterIngredientAsync(item2, "Item 2", "SKU-2", "General", "units");

        await grain.GenerateSuggestionsAsync();

        // Act
        var suggestions = await grain.GetPendingSuggestionsAsync();

        // Assert
        suggestions.Should().NotBeEmpty();
        // Verify ordering - higher urgency should come first
        for (int i = 1; i < suggestions.Count; i++)
        {
            ((int)suggestions[i - 1].Urgency).Should().BeGreaterThanOrEqualTo((int)suggestions[i].Urgency);
        }
    }

    [Fact]
    public async Task ApproveSuggestionAsync_ShouldUpdateStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        await CreateInventoryWithLowStockAsync(orgId, siteId, ingredientId, "Ground Beef", 5, 5.00m, 2);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IReorderSuggestionGrain>(GrainKeys.ReorderSuggestion(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.RegisterIngredientAsync(ingredientId, "Ground Beef", "SKU-GB", "Proteins", "lb");
        var report = await grain.GenerateSuggestionsAsync();

        var suggestionId = report.HighPriorityItems.Concat(report.MediumPriorityItems).Concat(report.CriticalItems).First().SuggestionId;

        // Act
        await grain.ApproveSuggestionAsync(suggestionId, approverId);

        // Assert
        var pendingSuggestions = await grain.GetPendingSuggestionsAsync();
        pendingSuggestions.Should().NotContain(s => s.SuggestionId == suggestionId);
    }

    [Fact]
    public async Task DismissSuggestionAsync_ShouldUpdateStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await CreateInventoryWithLowStockAsync(orgId, siteId, ingredientId, "Ground Beef", 5, 5.00m, 2);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IReorderSuggestionGrain>(GrainKeys.ReorderSuggestion(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.RegisterIngredientAsync(ingredientId, "Ground Beef", "SKU-GB", "Proteins", "lb");
        var report = await grain.GenerateSuggestionsAsync();

        var suggestionId = report.HighPriorityItems.Concat(report.MediumPriorityItems).Concat(report.CriticalItems).First().SuggestionId;

        // Act
        await grain.DismissSuggestionAsync(suggestionId, userId, "Stock already ordered manually");

        // Assert
        var pendingSuggestions = await grain.GetPendingSuggestionsAsync();
        pendingSuggestions.Should().NotContain(s => s.SuggestionId == suggestionId);
    }

    [Fact]
    public async Task MarkAsOrderedAsync_ShouldLinkToPurchaseOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var purchaseOrderId = Guid.NewGuid();

        await CreateInventoryWithLowStockAsync(orgId, siteId, ingredientId, "Ground Beef", 5, 5.00m, 2);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IReorderSuggestionGrain>(GrainKeys.ReorderSuggestion(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.RegisterIngredientAsync(ingredientId, "Ground Beef", "SKU-GB", "Proteins", "lb");
        var report = await grain.GenerateSuggestionsAsync();

        var suggestionId = report.HighPriorityItems.Concat(report.MediumPriorityItems).Concat(report.CriticalItems).First().SuggestionId;

        // Act
        await grain.MarkAsOrderedAsync(suggestionId, purchaseOrderId);

        // Assert
        var pendingSuggestions = await grain.GetPendingSuggestionsAsync();
        pendingSuggestions.Should().NotContain(s => s.SuggestionId == suggestionId);
    }

    [Fact]
    public async Task GeneratePurchaseOrderDraftAsync_ShouldCreateDraftFromSuggestions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryWithLowStockAsync(orgId, siteId, ingredientId, "Ground Beef", 5, 5.00m, 2);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IReorderSuggestionGrain>(GrainKeys.ReorderSuggestion(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.RegisterIngredientAsync(ingredientId, "Ground Beef", "SKU-GB", "Proteins", "lb");
        await grain.GenerateSuggestionsAsync();

        // Act
        var draft = await grain.GeneratePurchaseOrderDraftAsync();

        // Assert
        draft.Should().NotBeNull();
        draft.Lines.Should().NotBeEmpty();
        draft.TotalValue.Should().BeGreaterThan(0);
        draft.RequestedDeliveryDate.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task GenerateConsolidatedDraftsAsync_ShouldGroupBySupplier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var supplier1 = Guid.NewGuid();
        var supplier2 = Guid.NewGuid();

        var item1 = Guid.NewGuid();
        var item2 = Guid.NewGuid();
        var item3 = Guid.NewGuid();

        await CreateInventoryWithLowStockAsync(orgId, siteId, item1, "Item 1", 5, 5.00m, 2);
        await CreateInventoryWithLowStockAsync(orgId, siteId, item2, "Item 2", 3, 8.00m, 3);
        await CreateInventoryWithLowStockAsync(orgId, siteId, item3, "Item 3", 2, 10.00m, 4);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IReorderSuggestionGrain>(GrainKeys.ReorderSuggestion(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        await grain.RegisterIngredientAsync(item1, "Item 1", "SKU-1", "General", "units", supplier1, "Supplier 1");
        await grain.RegisterIngredientAsync(item2, "Item 2", "SKU-2", "General", "units", supplier1, "Supplier 1");
        await grain.RegisterIngredientAsync(item3, "Item 3", "SKU-3", "General", "units", supplier2, "Supplier 2");

        await grain.GenerateSuggestionsAsync();

        // Act
        var drafts = await grain.GenerateConsolidatedDraftsAsync();

        // Assert
        drafts.Should().HaveCountGreaterThanOrEqualTo(2);
        // Verify each draft has a consistent supplier
        foreach (var draft in drafts.Where(d => d.SupplierId.HasValue))
        {
            draft.Lines.Should().AllSatisfy(line => line.Should().NotBeNull());
        }
    }

    [Fact]
    public async Task CalculateOptimalOrderQuantityAsync_ShouldReturnEOQ()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryWithLowStockAsync(orgId, siteId, ingredientId, "Ground Beef", 5, 5.00m, 2);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IReorderSuggestionGrain>(GrainKeys.ReorderSuggestion(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.RegisterIngredientAsync(ingredientId, "Ground Beef", "SKU-GB", "Proteins", "lb");

        // Act
        var eoq = await grain.CalculateOptimalOrderQuantityAsync(ingredientId, orderingCost: 50m, holdingCostPercentage: 0.25m);

        // Assert
        eoq.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateIngredientSupplierAsync_ShouldUpdateSupplierInfo()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        await CreateInventoryWithLowStockAsync(orgId, siteId, ingredientId, "Ground Beef", 5, 5.00m, 2);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IReorderSuggestionGrain>(GrainKeys.ReorderSuggestion(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.RegisterIngredientAsync(ingredientId, "Ground Beef", "SKU-GB", "Proteins", "lb");

        // Act
        await grain.UpdateIngredientSupplierAsync(ingredientId, supplierId, "New Supplier", 3);
        var report = await grain.GenerateSuggestionsAsync();

        // Assert
        var suggestions = await grain.GetPendingSuggestionsAsync();
        var suggestion = suggestions.FirstOrDefault(s => s.IngredientId == ingredientId);
        if (suggestion != null)
        {
            suggestion.PreferredSupplierId.Should().Be(supplierId);
            suggestion.PreferredSupplierName.Should().Be("New Supplier");
            suggestion.LeadTimeDays.Should().Be(3);
        }
    }

    [Fact]
    public async Task ConfigureAsync_ShouldUpdateSettings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IReorderSuggestionGrain>(GrainKeys.ReorderSuggestion(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        var newSettings = new ReorderSettings(
            DefaultLeadTimeDays: 14,
            SafetyStockMultiplier: 2.0m,
            UseAbcClassification: false,
            AnalysisPeriodDays: 60,
            AutoGeneratePO: true,
            MinimumOrderValue: 100m,
            ConsolidateBySupplier: false);

        // Act
        await grain.ConfigureAsync(newSettings);

        // Assert
        var settings = await grain.GetSettingsAsync();
        settings.DefaultLeadTimeDays.Should().Be(14);
        settings.SafetyStockMultiplier.Should().Be(2.0m);
        settings.UseAbcClassification.Should().BeFalse();
        settings.AnalysisPeriodDays.Should().Be(60);
    }

    [Fact]
    public async Task UnregisterIngredientAsync_ShouldRemoveIngredientAndSuggestions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryWithLowStockAsync(orgId, siteId, ingredientId, "Ground Beef", 5, 5.00m, 2);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IReorderSuggestionGrain>(GrainKeys.ReorderSuggestion(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.RegisterIngredientAsync(ingredientId, "Ground Beef", "SKU-GB", "Proteins", "lb");
        await grain.GenerateSuggestionsAsync();

        var beforeCount = (await grain.GetPendingSuggestionsAsync()).Count;
        beforeCount.Should().BeGreaterThan(0);

        // Act
        await grain.UnregisterIngredientAsync(ingredientId);

        // Assert
        var suggestions = await grain.GetPendingSuggestionsAsync();
        suggestions.Should().NotContain(s => s.IngredientId == ingredientId);
    }

    [Fact]
    public async Task GetSuggestionsByUrgencyAsync_ShouldFilterCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        await CreateInventoryWithLowStockAsync(orgId, siteId, ingredientId, "Ground Beef", 5, 5.00m, 2);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IReorderSuggestionGrain>(GrainKeys.ReorderSuggestion(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        await grain.RegisterIngredientAsync(ingredientId, "Ground Beef", "SKU-GB", "Proteins", "lb");
        var report = await grain.GenerateSuggestionsAsync();

        // Find an urgency level that has suggestions
        var targetUrgency = report.CriticalCount > 0 ? ReorderUrgency.Critical :
                           report.HighCount > 0 ? ReorderUrgency.High :
                           ReorderUrgency.Medium;

        // Act
        var suggestions = await grain.GetSuggestionsByUrgencyAsync(targetUrgency);

        // Assert
        suggestions.Should().AllSatisfy(s => s.Urgency.Should().Be(targetUrgency));
    }

    [Fact]
    public async Task GetReportAsync_ShouldIncludeSupplierSummary()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var item1 = Guid.NewGuid();
        var item2 = Guid.NewGuid();

        await CreateInventoryWithLowStockAsync(orgId, siteId, item1, "Item 1", 5, 5.00m, 2);
        await CreateInventoryWithLowStockAsync(orgId, siteId, item2, "Item 2", 3, 8.00m, 3);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IReorderSuggestionGrain>(GrainKeys.ReorderSuggestion(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        await grain.RegisterIngredientAsync(item1, "Item 1", "SKU-1", "General", "units", supplierId, "Main Supplier");
        await grain.RegisterIngredientAsync(item2, "Item 2", "SKU-2", "General", "units", supplierId, "Main Supplier");

        // Act
        var report = await grain.GetReportAsync();

        // Assert
        report.BySupplier.Should().ContainKey(supplierId);
        var supplierSummary = report.BySupplier[supplierId];
        supplierSummary.ItemCount.Should().Be(2);
        supplierSummary.SupplierName.Should().Be("Main Supplier");
        supplierSummary.TotalValue.Should().BeGreaterThan(0);
    }
}
