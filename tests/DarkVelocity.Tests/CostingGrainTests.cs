using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;
using Orleans.TestingHost;
using Xunit;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class CostingGrainTests
{
    private readonly TestCluster _cluster;

    public CostingGrainTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    // ============================================================================
    // Recipe Grain Tests
    // ============================================================================

    [Fact]
    public async Task RecipeGrain_Create_CreatesRecipeSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        var command = new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Grilled Salmon",
            Code: "GS001",
            CategoryId: Guid.NewGuid(),
            CategoryName: "Main Course",
            Description: "Fresh Atlantic salmon",
            PortionYield: 1,
            PrepInstructions: "Grill at 400F for 12 minutes");

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.RecipeId.Should().Be(recipeId);
        snapshot.MenuItemName.Should().Be("Grilled Salmon");
        snapshot.Code.Should().Be("GS001");
        snapshot.PortionYield.Should().Be(1);
        snapshot.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RecipeGrain_AddIngredient_CalculatesCostCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Recipe",
            Code: "TR001",
            CategoryId: null,
            CategoryName: null,
            Description: null,
            PortionYield: 2,
            PrepInstructions: null));

        // Act
        await grain.AddIngredientAsync(new RecipeIngredientCommand(
            IngredientId: Guid.NewGuid(),
            IngredientName: "Salmon Fillet",
            Quantity: 200,
            UnitOfMeasure: "grams",
            WastePercentage: 10,
            CurrentUnitCost: 0.05m));

        var calculation = await grain.CalculateCostAsync(25.99m);

        // Assert
        calculation.PortionYield.Should().Be(2);
        calculation.TotalIngredientCost.Should().Be(11m); // 200 * 1.1 * 0.05
        calculation.CostPerPortion.Should().Be(5.5m);
        calculation.MenuPrice.Should().Be(25.99m);
        calculation.CostPercentage.Should().BeApproximately(21.16m, 0.1m);
    }

    [Fact]
    public async Task RecipeGrain_CreateCostSnapshot_TracksHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Recipe",
            Code: "TR002",
            CategoryId: null,
            CategoryName: null,
            Description: null,
            PortionYield: 1,
            PrepInstructions: null));

        // Act
        await grain.CreateCostSnapshotAsync(15.99m, "Weekly snapshot");
        await grain.CreateCostSnapshotAsync(16.99m, "Price increased");

        var history = await grain.GetCostHistoryAsync(10);

        // Assert
        history.Should().HaveCount(2);
        history[0].MenuPrice.Should().Be(16.99m);
    }

    // ============================================================================
    // Ingredient Price Grain Tests
    // ============================================================================

    [Fact]
    public async Task IngredientPriceGrain_Create_CreatesSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientPriceGrain>(
            GrainKeys.IngredientPrice(orgId, ingredientId));

        var command = new CreateIngredientPriceCommand(
            IngredientId: ingredientId,
            IngredientName: "Atlantic Salmon",
            CurrentPrice: 25.00m,
            UnitOfMeasure: "kg",
            PackSize: 1,
            PreferredSupplierId: Guid.NewGuid(),
            PreferredSupplierName: "Fish Co.");

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.IngredientName.Should().Be("Atlantic Salmon");
        snapshot.CurrentPrice.Should().Be(25.00m);
        snapshot.PricePerUnit.Should().Be(25.00m);
        snapshot.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task IngredientPriceGrain_UpdatePrice_TracksPriceHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientPriceGrain>(
            GrainKeys.IngredientPrice(orgId, ingredientId));

        await grain.CreateAsync(new CreateIngredientPriceCommand(
            IngredientId: ingredientId,
            IngredientName: "Test Ingredient",
            CurrentPrice: 10.00m,
            UnitOfMeasure: "kg",
            PackSize: 1,
            PreferredSupplierId: null,
            PreferredSupplierName: null));

        // Act
        await grain.UpdatePriceAsync(12.00m, "Supplier price increase");
        var snapshot = await grain.GetSnapshotAsync();
        var history = await grain.GetPriceHistoryAsync(10);

        // Assert
        snapshot.CurrentPrice.Should().Be(12.00m);
        snapshot.PreviousPrice.Should().Be(10.00m);
        snapshot.PriceChangePercent.Should().Be(20m);
        history.Should().HaveCount(2);
    }

    // ============================================================================
    // Cost Alert Grain Tests
    // ============================================================================

    [Fact]
    public async Task CostAlertGrain_Create_CreatesAlertSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostAlertGrain>(
            GrainKeys.CostAlert(orgId, alertId));

        var command = new CreateCostAlertCommand(
            AlertType: CostAlertType.IngredientPriceIncrease,
            RecipeId: null,
            RecipeName: null,
            IngredientId: Guid.NewGuid(),
            IngredientName: "Salmon",
            MenuItemId: null,
            MenuItemName: null,
            PreviousValue: 20.00m,
            CurrentValue: 25.00m,
            ThresholdValue: 10m,
            ImpactDescription: "Affects 5 recipes",
            AffectedRecipeCount: 5);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.AlertType.Should().Be(CostAlertType.IngredientPriceIncrease);
        snapshot.ChangePercent.Should().Be(25m);
        snapshot.IsAcknowledged.Should().BeFalse();
    }

    [Fact]
    public async Task CostAlertGrain_Acknowledge_UpdatesStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostAlertGrain>(
            GrainKeys.CostAlert(orgId, alertId));

        await grain.CreateAsync(new CreateCostAlertCommand(
            AlertType: CostAlertType.MarginBelowThreshold,
            RecipeId: Guid.NewGuid(),
            RecipeName: "Test Recipe",
            IngredientId: null,
            IngredientName: null,
            MenuItemId: null,
            MenuItemName: null,
            PreviousValue: 50m,
            CurrentValue: 40m,
            ThresholdValue: 45m,
            ImpactDescription: null,
            AffectedRecipeCount: 1));

        // Act
        var ackCommand = new AcknowledgeCostAlertCommand(
            AcknowledgedByUserId: Guid.NewGuid(),
            Notes: "Menu price will be adjusted",
            ActionTaken: CostAlertAction.MenuUpdated);

        var snapshot = await grain.AcknowledgeAsync(ackCommand);

        // Assert
        snapshot.IsAcknowledged.Should().BeTrue();
        snapshot.ActionTaken.Should().Be(CostAlertAction.MenuUpdated);
        snapshot.Notes.Should().Be("Menu price will be adjusted");
    }

    // ============================================================================
    // Costing Settings Grain Tests
    // ============================================================================

    [Fact]
    public async Task CostingSettingsGrain_Initialize_SetsDefaultValues()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        // Act
        await grain.InitializeAsync(locationId);
        var settings = await grain.GetSettingsAsync();

        // Assert
        settings.LocationId.Should().Be(locationId);
        settings.TargetFoodCostPercent.Should().Be(30m);
        settings.TargetBeverageCostPercent.Should().Be(25m);
        settings.MinimumMarginPercent.Should().Be(50m);
        settings.AutoRecalculateCosts.Should().BeTrue();
    }

    [Fact]
    public async Task CostingSettingsGrain_ShouldAlertOnPriceChange_ReturnsCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        await grain.InitializeAsync(locationId);

        // Act
        var shouldAlert15 = await grain.ShouldAlertOnPriceChangeAsync(15m);
        var shouldAlert5 = await grain.ShouldAlertOnPriceChangeAsync(5m);

        // Assert - default threshold is 10%
        shouldAlert15.Should().BeTrue();
        shouldAlert5.Should().BeFalse();
    }

    // ============================================================================
    // Additional Recipe Grain Tests
    // ============================================================================

    [Fact]
    public async Task RecipeGrain_UpdateAsync_ShouldUpdateRecipeMetadata()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Original Name",
            Code: "ORIG001",
            CategoryId: null,
            CategoryName: null,
            Description: "Original description",
            PortionYield: 2,
            PrepInstructions: "Original instructions"));

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateRecipeCommand(
            MenuItemName: "Updated Name",
            Code: "UPD001",
            CategoryId: Guid.NewGuid(),
            CategoryName: "New Category",
            Description: "Updated description",
            PortionYield: 4,
            PrepInstructions: "Updated instructions",
            IsActive: null));

        // Assert
        snapshot.MenuItemName.Should().Be("Updated Name");
        snapshot.Code.Should().Be("UPD001");
        snapshot.CategoryName.Should().Be("New Category");
        snapshot.Description.Should().Be("Updated description");
        snapshot.PortionYield.Should().Be(4);
        snapshot.PrepInstructions.Should().Be("Updated instructions");
    }

    [Fact]
    public async Task RecipeGrain_DeleteAsync_ShouldSetInactiveFlag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Recipe",
            Code: "TR001",
            CategoryId: null,
            CategoryName: null,
            Description: null,
            PortionYield: 1,
            PrepInstructions: null));

        // Act
        await grain.DeleteAsync();
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task RecipeGrain_UpdateIngredientAsync_ShouldUpdateQuantityAndCost()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Recipe",
            Code: "TR001",
            CategoryId: null,
            CategoryName: null,
            Description: null,
            PortionYield: 1,
            PrepInstructions: null));

        await grain.AddIngredientAsync(new RecipeIngredientCommand(
            IngredientId: ingredientId,
            IngredientName: "Salmon Fillet",
            Quantity: 100,
            UnitOfMeasure: "grams",
            WastePercentage: 10,
            CurrentUnitCost: 0.05m));

        // Act
        await grain.UpdateIngredientAsync(ingredientId, new RecipeIngredientCommand(
            IngredientId: ingredientId,
            IngredientName: "Salmon Fillet Premium",
            Quantity: 200,
            UnitOfMeasure: "grams",
            WastePercentage: 5,
            CurrentUnitCost: 0.08m));

        var ingredients = await grain.GetIngredientsAsync();

        // Assert
        ingredients.Should().HaveCount(1);
        ingredients[0].IngredientName.Should().Be("Salmon Fillet Premium");
        ingredients[0].Quantity.Should().Be(200);
        ingredients[0].WastePercentage.Should().Be(5);
        ingredients[0].CurrentUnitCost.Should().Be(0.08m);
        // Effective quantity: 200 * 1.05 = 210, Line cost: 210 * 0.08 = 16.8
        ingredients[0].CurrentLineCost.Should().Be(16.8m);
    }

    [Fact]
    public async Task RecipeGrain_UpdateIngredientAsync_NonExistent_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Recipe",
            Code: "TR001",
            CategoryId: null,
            CategoryName: null,
            Description: null,
            PortionYield: 1,
            PrepInstructions: null));

        // Act
        var act = () => grain.UpdateIngredientAsync(Guid.NewGuid(), new RecipeIngredientCommand(
            IngredientId: Guid.NewGuid(),
            IngredientName: "Non-existent",
            Quantity: 100,
            UnitOfMeasure: "grams",
            WastePercentage: 0,
            CurrentUnitCost: 1.00m));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Ingredient not found");
    }

    [Fact]
    public async Task RecipeGrain_RemoveIngredientAsync_ShouldRemoveIngredient()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Recipe",
            Code: "TR001",
            CategoryId: null,
            CategoryName: null,
            Description: null,
            PortionYield: 1,
            PrepInstructions: null));

        await grain.AddIngredientAsync(new RecipeIngredientCommand(
            IngredientId: ingredientId,
            IngredientName: "Salmon",
            Quantity: 100,
            UnitOfMeasure: "grams",
            WastePercentage: 0,
            CurrentUnitCost: 0.05m));

        // Act
        await grain.RemoveIngredientAsync(ingredientId);
        var ingredients = await grain.GetIngredientsAsync();

        // Assert
        ingredients.Should().BeEmpty();
    }

    [Fact]
    public async Task RecipeGrain_RemoveIngredientAsync_NonExistent_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Recipe",
            Code: "TR001",
            CategoryId: null,
            CategoryName: null,
            Description: null,
            PortionYield: 1,
            PrepInstructions: null));

        // Act
        var act = () => grain.RemoveIngredientAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Ingredient not found");
    }

    [Fact]
    public async Task RecipeGrain_GetIngredientsAsync_ShouldReturnCostBreakdown()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Recipe",
            Code: "TR001",
            CategoryId: null,
            CategoryName: null,
            Description: null,
            PortionYield: 1,
            PrepInstructions: null));

        // Add two ingredients with different costs
        await grain.AddIngredientAsync(new RecipeIngredientCommand(
            IngredientId: Guid.NewGuid(),
            IngredientName: "Expensive Ingredient",
            Quantity: 100,
            UnitOfMeasure: "grams",
            WastePercentage: 0,
            CurrentUnitCost: 0.10m)); // Cost: 10

        await grain.AddIngredientAsync(new RecipeIngredientCommand(
            IngredientId: Guid.NewGuid(),
            IngredientName: "Cheap Ingredient",
            Quantity: 100,
            UnitOfMeasure: "grams",
            WastePercentage: 0,
            CurrentUnitCost: 0.05m)); // Cost: 5

        // Act
        var ingredients = await grain.GetIngredientsAsync();

        // Assert
        ingredients.Should().HaveCount(2);
        // Total cost = 15, Expensive = 10/15 = 66.67%, Cheap = 5/15 = 33.33%
        var expensiveIngredient = ingredients.First(i => i.IngredientName == "Expensive Ingredient");
        var cheapIngredient = ingredients.First(i => i.IngredientName == "Cheap Ingredient");

        expensiveIngredient.CostPercentOfTotal.Should().BeApproximately(66.67m, 0.1m);
        cheapIngredient.CostPercentOfTotal.Should().BeApproximately(33.33m, 0.1m);
    }

    [Fact]
    public async Task RecipeGrain_RecalculateFromPricesAsync_ShouldUpdateCosts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var ingredientId1 = Guid.NewGuid();
        var ingredientId2 = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Recipe",
            Code: "TR001",
            CategoryId: null,
            CategoryName: null,
            Description: null,
            PortionYield: 1,
            PrepInstructions: null));

        await grain.AddIngredientAsync(new RecipeIngredientCommand(
            IngredientId: ingredientId1,
            IngredientName: "Ingredient 1",
            Quantity: 100,
            UnitOfMeasure: "grams",
            WastePercentage: 0,
            CurrentUnitCost: 0.05m));

        await grain.AddIngredientAsync(new RecipeIngredientCommand(
            IngredientId: ingredientId2,
            IngredientName: "Ingredient 2",
            Quantity: 100,
            UnitOfMeasure: "grams",
            WastePercentage: 0,
            CurrentUnitCost: 0.10m));

        // Act
        var newPrices = new Dictionary<Guid, decimal>
        {
            { ingredientId1, 0.08m },
            { ingredientId2, 0.12m }
        };
        var snapshot = await grain.RecalculateFromPricesAsync(newPrices);

        // Assert
        // Ingredient 1: 100 * 0.08 = 8, Ingredient 2: 100 * 0.12 = 12, Total = 20
        snapshot.CurrentCostPerPortion.Should().Be(20m);
    }

    [Fact]
    public async Task RecipeGrain_RecalculateFromPricesAsync_PartialPrices_ShouldUpdateMatching()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var ingredientId1 = Guid.NewGuid();
        var ingredientId2 = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Recipe",
            Code: "TR001",
            CategoryId: null,
            CategoryName: null,
            Description: null,
            PortionYield: 1,
            PrepInstructions: null));

        await grain.AddIngredientAsync(new RecipeIngredientCommand(
            IngredientId: ingredientId1,
            IngredientName: "Ingredient 1",
            Quantity: 100,
            UnitOfMeasure: "grams",
            WastePercentage: 0,
            CurrentUnitCost: 0.05m)); // Cost: 5

        await grain.AddIngredientAsync(new RecipeIngredientCommand(
            IngredientId: ingredientId2,
            IngredientName: "Ingredient 2",
            Quantity: 100,
            UnitOfMeasure: "grams",
            WastePercentage: 0,
            CurrentUnitCost: 0.10m)); // Cost: 10

        // Act - only update ingredient 1 price
        var newPrices = new Dictionary<Guid, decimal>
        {
            { ingredientId1, 0.08m } // New cost: 8
        };
        var snapshot = await grain.RecalculateFromPricesAsync(newPrices);

        // Assert
        // Ingredient 1: 100 * 0.08 = 8, Ingredient 2: unchanged 100 * 0.10 = 10, Total = 18
        snapshot.CurrentCostPerPortion.Should().Be(18m);
    }

    [Fact]
    public async Task RecipeGrain_CreateAsync_ZeroPortionYield_ShouldDefaultToOne()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        // Act
        var snapshot = await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Recipe",
            Code: "TR001",
            CategoryId: null,
            CategoryName: null,
            Description: null,
            PortionYield: 0, // Zero should default to 1
            PrepInstructions: null));

        // Assert
        snapshot.PortionYield.Should().Be(1);
    }

    [Fact]
    public async Task RecipeGrain_CalculateCostAsync_ZeroMenuPrice_ShouldHandleNullMargin()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Recipe",
            Code: "TR001",
            CategoryId: null,
            CategoryName: null,
            Description: null,
            PortionYield: 1,
            PrepInstructions: null));

        await grain.AddIngredientAsync(new RecipeIngredientCommand(
            IngredientId: Guid.NewGuid(),
            IngredientName: "Ingredient",
            Quantity: 100,
            UnitOfMeasure: "grams",
            WastePercentage: 0,
            CurrentUnitCost: 0.10m));

        // Act
        var calculation = await grain.CalculateCostAsync(0m);

        // Assert
        calculation.MenuPrice.Should().Be(0m);
        calculation.CostPercentage.Should().BeNull();
        calculation.GrossMarginPercent.Should().BeNull();
    }

    [Fact]
    public async Task RecipeGrain_CalculateCostAsync_NoIngredients_ShouldReturnZero()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Recipe",
            Code: "TR001",
            CategoryId: null,
            CategoryName: null,
            Description: null,
            PortionYield: 1,
            PrepInstructions: null));

        // Act
        var calculation = await grain.CalculateCostAsync(10m);

        // Assert
        calculation.TotalIngredientCost.Should().Be(0m);
        calculation.CostPerPortion.Should().Be(0m);
        calculation.CostPercentage.Should().Be(0m);
        calculation.GrossMarginPercent.Should().Be(100m);
    }

    [Fact]
    public async Task RecipeGrain_CreateCostSnapshotAsync_Over52_ShouldTrim()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Recipe",
            Code: "TR001",
            CategoryId: null,
            CategoryName: null,
            Description: null,
            PortionYield: 1,
            PrepInstructions: null));

        // Act - create 55 snapshots
        for (int i = 0; i < 55; i++)
        {
            await grain.CreateCostSnapshotAsync(10.00m + i, $"Snapshot {i}");
        }

        var history = await grain.GetCostHistoryAsync(100);

        // Assert - should be trimmed to max 52
        history.Should().HaveCount(52);
    }

    [Fact]
    public async Task RecipeGrain_GetSnapshotAsync_ShouldReturnState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var menuItemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: menuItemId,
            MenuItemName: "Test Recipe",
            Code: "TR001",
            CategoryId: categoryId,
            CategoryName: "Main Course",
            Description: "A test recipe",
            PortionYield: 2,
            PrepInstructions: "Cook it well"));

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.RecipeId.Should().Be(recipeId);
        snapshot.MenuItemId.Should().Be(menuItemId);
        snapshot.MenuItemName.Should().Be("Test Recipe");
        snapshot.Code.Should().Be("TR001");
        snapshot.CategoryId.Should().Be(categoryId);
        snapshot.CategoryName.Should().Be("Main Course");
        snapshot.Description.Should().Be("A test recipe");
        snapshot.PortionYield.Should().Be(2);
        snapshot.PrepInstructions.Should().Be("Cook it well");
        snapshot.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RecipeGrain_ExistsAsync_ShouldReturnCorrectStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));

        // Act - before creation
        var existsBefore = await grain.ExistsAsync();

        await grain.CreateAsync(new CreateRecipeCommand(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Recipe",
            Code: "TR001",
            CategoryId: null,
            CategoryName: null,
            Description: null,
            PortionYield: 1,
            PrepInstructions: null));

        // Act - after creation
        var existsAfter = await grain.ExistsAsync();

        // Assert
        existsBefore.Should().BeFalse();
        existsAfter.Should().BeTrue();
    }

    // ============================================================================
    // Additional Ingredient Price Grain Tests
    // ============================================================================

    [Fact]
    public async Task IngredientPriceGrain_UpdateAsync_ShouldUpdatePriceAndPackSize()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientPriceGrain>(
            GrainKeys.IngredientPrice(orgId, ingredientId));

        await grain.CreateAsync(new CreateIngredientPriceCommand(
            IngredientId: ingredientId,
            IngredientName: "Test Ingredient",
            CurrentPrice: 10.00m,
            UnitOfMeasure: "kg",
            PackSize: 1,
            PreferredSupplierId: null,
            PreferredSupplierName: null));

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateIngredientPriceCommand(
            CurrentPrice: 20.00m,
            PackSize: 5,
            PreferredSupplierId: Guid.NewGuid(),
            PreferredSupplierName: "New Supplier",
            IsActive: null));

        // Assert
        snapshot.CurrentPrice.Should().Be(20.00m);
        snapshot.PackSize.Should().Be(5);
        snapshot.PricePerUnit.Should().Be(4.00m); // 20 / 5
        snapshot.PreferredSupplierName.Should().Be("New Supplier");
    }

    [Fact]
    public async Task IngredientPriceGrain_DeleteAsync_ShouldSetInactiveFlag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientPriceGrain>(
            GrainKeys.IngredientPrice(orgId, ingredientId));

        await grain.CreateAsync(new CreateIngredientPriceCommand(
            IngredientId: ingredientId,
            IngredientName: "Test Ingredient",
            CurrentPrice: 10.00m,
            UnitOfMeasure: "kg",
            PackSize: 1,
            PreferredSupplierId: null,
            PreferredSupplierName: null));

        // Act
        await grain.DeleteAsync();
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task IngredientPriceGrain_GetPricePerUnitAsync_ShouldCalculateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientPriceGrain>(
            GrainKeys.IngredientPrice(orgId, ingredientId));

        await grain.CreateAsync(new CreateIngredientPriceCommand(
            IngredientId: ingredientId,
            IngredientName: "Test Ingredient",
            CurrentPrice: 24.00m,
            UnitOfMeasure: "kg",
            PackSize: 8,
            PreferredSupplierId: null,
            PreferredSupplierName: null));

        // Act
        var pricePerUnit = await grain.GetPricePerUnitAsync();

        // Assert
        pricePerUnit.Should().Be(3.00m); // 24 / 8
    }

    [Fact]
    public async Task IngredientPriceGrain_UpdatePriceAsync_Decrease_ShouldShowNegativeChangePercent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientPriceGrain>(
            GrainKeys.IngredientPrice(orgId, ingredientId));

        await grain.CreateAsync(new CreateIngredientPriceCommand(
            IngredientId: ingredientId,
            IngredientName: "Test Ingredient",
            CurrentPrice: 10.00m,
            UnitOfMeasure: "kg",
            PackSize: 1,
            PreferredSupplierId: null,
            PreferredSupplierName: null));

        // Act
        var snapshot = await grain.UpdatePriceAsync(8.00m, "Price decrease");

        // Assert
        snapshot.CurrentPrice.Should().Be(8.00m);
        snapshot.PreviousPrice.Should().Be(10.00m);
        snapshot.PriceChangePercent.Should().Be(-20m); // (8 - 10) / 10 * 100
    }

    [Fact]
    public async Task IngredientPriceGrain_UpdatePriceAsync_NoChange_ShouldShowZeroPercent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientPriceGrain>(
            GrainKeys.IngredientPrice(orgId, ingredientId));

        await grain.CreateAsync(new CreateIngredientPriceCommand(
            IngredientId: ingredientId,
            IngredientName: "Test Ingredient",
            CurrentPrice: 10.00m,
            UnitOfMeasure: "kg",
            PackSize: 1,
            PreferredSupplierId: null,
            PreferredSupplierName: null));

        // Act
        var snapshot = await grain.UpdatePriceAsync(10.00m, "No change");

        // Assert
        snapshot.CurrentPrice.Should().Be(10.00m);
        snapshot.PriceChangePercent.Should().Be(0m);
    }

    [Fact]
    public async Task IngredientPriceGrain_CreateAsync_PackSizeGreaterThanOne_ShouldCalculatePricePerUnit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientPriceGrain>(
            GrainKeys.IngredientPrice(orgId, ingredientId));

        // Act
        var snapshot = await grain.CreateAsync(new CreateIngredientPriceCommand(
            IngredientId: ingredientId,
            IngredientName: "Bulk Ingredient",
            CurrentPrice: 50.00m,
            UnitOfMeasure: "kg",
            PackSize: 10,
            PreferredSupplierId: null,
            PreferredSupplierName: null));

        // Assert
        snapshot.PackSize.Should().Be(10);
        snapshot.PricePerUnit.Should().Be(5.00m); // 50 / 10
    }

    [Fact]
    public async Task IngredientPriceGrain_CreateAsync_PackSizeZero_ShouldDefaultToOne()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientPriceGrain>(
            GrainKeys.IngredientPrice(orgId, ingredientId));

        // Act
        var snapshot = await grain.CreateAsync(new CreateIngredientPriceCommand(
            IngredientId: ingredientId,
            IngredientName: "Test Ingredient",
            CurrentPrice: 10.00m,
            UnitOfMeasure: "kg",
            PackSize: 0, // Zero should default to 1
            PreferredSupplierId: null,
            PreferredSupplierName: null));

        // Assert
        snapshot.PackSize.Should().Be(1);
        snapshot.PricePerUnit.Should().Be(10.00m);
    }

    [Fact]
    public async Task IngredientPriceGrain_PriceHistory_Over100_ShouldTrim()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientPriceGrain>(
            GrainKeys.IngredientPrice(orgId, ingredientId));

        await grain.CreateAsync(new CreateIngredientPriceCommand(
            IngredientId: ingredientId,
            IngredientName: "Test Ingredient",
            CurrentPrice: 10.00m,
            UnitOfMeasure: "kg",
            PackSize: 1,
            PreferredSupplierId: null,
            PreferredSupplierName: null));

        // Act - update price 105 times (plus initial = 106 entries)
        for (int i = 1; i <= 105; i++)
        {
            await grain.UpdatePriceAsync(10.00m + i, $"Update {i}");
        }

        var history = await grain.GetPriceHistoryAsync(200);

        // Assert - should be trimmed to max 100
        history.Should().HaveCount(100);
    }

    [Fact]
    public async Task IngredientPriceGrain_ExistsAsync_ShouldReturnCorrectStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IIngredientPriceGrain>(
            GrainKeys.IngredientPrice(orgId, ingredientId));

        // Act - before creation
        var existsBefore = await grain.ExistsAsync();

        await grain.CreateAsync(new CreateIngredientPriceCommand(
            IngredientId: ingredientId,
            IngredientName: "Test Ingredient",
            CurrentPrice: 10.00m,
            UnitOfMeasure: "kg",
            PackSize: 1,
            PreferredSupplierId: null,
            PreferredSupplierName: null));

        // Act - after creation
        var existsAfter = await grain.ExistsAsync();

        // Assert
        existsBefore.Should().BeFalse();
        existsAfter.Should().BeTrue();
    }

    // ============================================================================
    // Additional Cost Alert Grain Tests
    // ============================================================================

    [Fact]
    public async Task CostAlertGrain_CreateAsync_RecipeCostIncrease_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostAlertGrain>(
            GrainKeys.CostAlert(orgId, alertId));

        // Act
        var snapshot = await grain.CreateAsync(new CreateCostAlertCommand(
            AlertType: CostAlertType.RecipeCostIncrease,
            RecipeId: recipeId,
            RecipeName: "Grilled Salmon",
            IngredientId: null,
            IngredientName: null,
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Grilled Salmon Entree",
            PreviousValue: 5.00m,
            CurrentValue: 6.50m,
            ThresholdValue: 5m,
            ImpactDescription: "Recipe cost increased due to ingredient price changes",
            AffectedRecipeCount: 1));

        // Assert
        snapshot.AlertType.Should().Be(CostAlertType.RecipeCostIncrease);
        snapshot.RecipeId.Should().Be(recipeId);
        snapshot.RecipeName.Should().Be("Grilled Salmon");
        snapshot.ChangePercent.Should().Be(30m); // (6.50 - 5.00) / 5.00 * 100
        snapshot.IsAcknowledged.Should().BeFalse();
    }

    [Fact]
    public async Task CostAlertGrain_CreateAsync_IngredientPriceDecrease_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostAlertGrain>(
            GrainKeys.CostAlert(orgId, alertId));

        // Act
        var snapshot = await grain.CreateAsync(new CreateCostAlertCommand(
            AlertType: CostAlertType.IngredientPriceDecrease,
            RecipeId: null,
            RecipeName: null,
            IngredientId: ingredientId,
            IngredientName: "Atlantic Salmon",
            MenuItemId: null,
            MenuItemName: null,
            PreviousValue: 30.00m,
            CurrentValue: 22.00m,
            ThresholdValue: 10m,
            ImpactDescription: "Opportunity to increase margins",
            AffectedRecipeCount: 5));

        // Assert
        snapshot.AlertType.Should().Be(CostAlertType.IngredientPriceDecrease);
        snapshot.IngredientId.Should().Be(ingredientId);
        snapshot.IngredientName.Should().Be("Atlantic Salmon");
        snapshot.ChangePercent.Should().BeApproximately(-26.67m, 0.1m); // (22 - 30) / 30 * 100
        snapshot.AffectedRecipeCount.Should().Be(5);
    }

    [Fact]
    public async Task CostAlertGrain_AcknowledgeAsync_PriceAdjusted_ShouldRecord()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var userId = Guid.NewGuid();
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
            PreviousValue: 5.00m,
            CurrentValue: 6.00m,
            ThresholdValue: 5m,
            ImpactDescription: null,
            AffectedRecipeCount: 1));

        // Act
        var snapshot = await grain.AcknowledgeAsync(new AcknowledgeCostAlertCommand(
            AcknowledgedByUserId: userId,
            Notes: "Adjusted menu prices accordingly",
            ActionTaken: CostAlertAction.PriceAdjusted));

        // Assert
        snapshot.IsAcknowledged.Should().BeTrue();
        snapshot.AcknowledgedByUserId.Should().Be(userId);
        snapshot.ActionTaken.Should().Be(CostAlertAction.PriceAdjusted);
        snapshot.Notes.Should().Be("Adjusted menu prices accordingly");
        snapshot.AcknowledgedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CostAlertGrain_AcknowledgeAsync_Accepted_ShouldRecord()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostAlertGrain>(
            GrainKeys.CostAlert(orgId, alertId));

        await grain.CreateAsync(new CreateCostAlertCommand(
            AlertType: CostAlertType.MarginBelowThreshold,
            RecipeId: Guid.NewGuid(),
            RecipeName: "Test Recipe",
            IngredientId: null,
            IngredientName: null,
            MenuItemId: null,
            MenuItemName: null,
            PreviousValue: 60m,
            CurrentValue: 45m,
            ThresholdValue: 50m,
            ImpactDescription: null,
            AffectedRecipeCount: 1));

        // Act
        var snapshot = await grain.AcknowledgeAsync(new AcknowledgeCostAlertCommand(
            AcknowledgedByUserId: userId,
            Notes: "This is acceptable for the current menu strategy",
            ActionTaken: CostAlertAction.Accepted));

        // Assert
        snapshot.IsAcknowledged.Should().BeTrue();
        snapshot.ActionTaken.Should().Be(CostAlertAction.Accepted);
    }

    [Fact]
    public async Task CostAlertGrain_AcknowledgeAsync_Ignored_ShouldRecord()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostAlertGrain>(
            GrainKeys.CostAlert(orgId, alertId));

        await grain.CreateAsync(new CreateCostAlertCommand(
            AlertType: CostAlertType.IngredientPriceIncrease,
            RecipeId: null,
            RecipeName: null,
            IngredientId: Guid.NewGuid(),
            IngredientName: "Salt",
            MenuItemId: null,
            MenuItemName: null,
            PreviousValue: 1.00m,
            CurrentValue: 1.15m,
            ThresholdValue: 10m,
            ImpactDescription: "Minor impact",
            AffectedRecipeCount: 50));

        // Act
        var snapshot = await grain.AcknowledgeAsync(new AcknowledgeCostAlertCommand(
            AcknowledgedByUserId: userId,
            Notes: "Minimal impact, ignoring",
            ActionTaken: CostAlertAction.Ignored));

        // Assert
        snapshot.IsAcknowledged.Should().BeTrue();
        snapshot.ActionTaken.Should().Be(CostAlertAction.Ignored);
    }

    [Fact]
    public async Task CostAlertGrain_AcknowledgeAsync_AlreadyAcknowledged_ShouldThrow()
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
            PreviousValue: 5.00m,
            CurrentValue: 6.00m,
            ThresholdValue: 5m,
            ImpactDescription: null,
            AffectedRecipeCount: 1));

        await grain.AcknowledgeAsync(new AcknowledgeCostAlertCommand(
            AcknowledgedByUserId: Guid.NewGuid(),
            Notes: "First acknowledgment",
            ActionTaken: CostAlertAction.Accepted));

        // Act
        var act = () => grain.AcknowledgeAsync(new AcknowledgeCostAlertCommand(
            AcknowledgedByUserId: Guid.NewGuid(),
            Notes: "Second acknowledgment",
            ActionTaken: CostAlertAction.Ignored));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Alert has already been acknowledged");
    }

    [Fact]
    public async Task CostAlertGrain_IsAcknowledgedAsync_ShouldReturnCorrectStatus()
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
            PreviousValue: 5.00m,
            CurrentValue: 6.00m,
            ThresholdValue: 5m,
            ImpactDescription: null,
            AffectedRecipeCount: 1));

        // Act - before acknowledgment
        var acknowledgedBefore = await grain.IsAcknowledgedAsync();

        await grain.AcknowledgeAsync(new AcknowledgeCostAlertCommand(
            AcknowledgedByUserId: Guid.NewGuid(),
            Notes: null,
            ActionTaken: CostAlertAction.Accepted));

        // Act - after acknowledgment
        var acknowledgedAfter = await grain.IsAcknowledgedAsync();

        // Assert
        acknowledgedBefore.Should().BeFalse();
        acknowledgedAfter.Should().BeTrue();
    }

    [Fact]
    public async Task CostAlertGrain_GetSnapshotAsync_ShouldReturnState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var menuItemId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostAlertGrain>(
            GrainKeys.CostAlert(orgId, alertId));

        await grain.CreateAsync(new CreateCostAlertCommand(
            AlertType: CostAlertType.IngredientPriceIncrease,
            RecipeId: recipeId,
            RecipeName: "Test Recipe",
            IngredientId: ingredientId,
            IngredientName: "Test Ingredient",
            MenuItemId: menuItemId,
            MenuItemName: "Test Menu Item",
            PreviousValue: 10.00m,
            CurrentValue: 15.00m,
            ThresholdValue: 10m,
            ImpactDescription: "Major cost impact",
            AffectedRecipeCount: 3));

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.AlertId.Should().Be(alertId);
        snapshot.AlertType.Should().Be(CostAlertType.IngredientPriceIncrease);
        snapshot.RecipeId.Should().Be(recipeId);
        snapshot.RecipeName.Should().Be("Test Recipe");
        snapshot.IngredientId.Should().Be(ingredientId);
        snapshot.IngredientName.Should().Be("Test Ingredient");
        snapshot.MenuItemId.Should().Be(menuItemId);
        snapshot.MenuItemName.Should().Be("Test Menu Item");
        snapshot.PreviousValue.Should().Be(10.00m);
        snapshot.CurrentValue.Should().Be(15.00m);
        snapshot.ThresholdValue.Should().Be(10m);
        snapshot.ImpactDescription.Should().Be("Major cost impact");
        snapshot.AffectedRecipeCount.Should().Be(3);
    }

    [Fact]
    public async Task CostAlertGrain_ExistsAsync_ShouldReturnCorrectStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostAlertGrain>(
            GrainKeys.CostAlert(orgId, alertId));

        // Act - before creation
        var existsBefore = await grain.ExistsAsync();

        await grain.CreateAsync(new CreateCostAlertCommand(
            AlertType: CostAlertType.RecipeCostIncrease,
            RecipeId: Guid.NewGuid(),
            RecipeName: "Test Recipe",
            IngredientId: null,
            IngredientName: null,
            MenuItemId: null,
            MenuItemName: null,
            PreviousValue: 5.00m,
            CurrentValue: 6.00m,
            ThresholdValue: 5m,
            ImpactDescription: null,
            AffectedRecipeCount: 1));

        // Act - after creation
        var existsAfter = await grain.ExistsAsync();

        // Assert
        existsBefore.Should().BeFalse();
        existsAfter.Should().BeTrue();
    }

    // ============================================================================
    // Additional Costing Settings Grain Tests
    // ============================================================================

    [Fact]
    public async Task CostingSettingsGrain_UpdateAsync_ShouldUpdateSettings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        await grain.InitializeAsync(locationId);

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateCostingSettingsCommand(
            TargetFoodCostPercent: 28m,
            TargetBeverageCostPercent: 22m,
            MinimumMarginPercent: 55m,
            WarningMarginPercent: 65m,
            PriceChangeAlertThreshold: 15m,
            CostIncreaseAlertThreshold: 8m,
            AutoRecalculateCosts: false,
            AutoCreateSnapshots: false,
            SnapshotFrequencyDays: 14));

        // Assert
        snapshot.TargetFoodCostPercent.Should().Be(28m);
        snapshot.TargetBeverageCostPercent.Should().Be(22m);
        snapshot.MinimumMarginPercent.Should().Be(55m);
        snapshot.WarningMarginPercent.Should().Be(65m);
        snapshot.PriceChangeAlertThreshold.Should().Be(15m);
        snapshot.CostIncreaseAlertThreshold.Should().Be(8m);
        snapshot.AutoRecalculateCosts.Should().BeFalse();
        snapshot.AutoCreateSnapshots.Should().BeFalse();
        snapshot.SnapshotFrequencyDays.Should().Be(14);
    }

    [Fact]
    public async Task CostingSettingsGrain_UpdateAsync_PartialUpdate_ShouldPreserveOthers()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        await grain.InitializeAsync(locationId);

        // Act - only update food cost target
        var snapshot = await grain.UpdateAsync(new UpdateCostingSettingsCommand(
            TargetFoodCostPercent: 35m,
            TargetBeverageCostPercent: null,
            MinimumMarginPercent: null,
            WarningMarginPercent: null,
            PriceChangeAlertThreshold: null,
            CostIncreaseAlertThreshold: null,
            AutoRecalculateCosts: null,
            AutoCreateSnapshots: null,
            SnapshotFrequencyDays: null));

        // Assert - food cost updated, others preserved at defaults
        snapshot.TargetFoodCostPercent.Should().Be(35m);
        snapshot.TargetBeverageCostPercent.Should().Be(25m); // Default
        snapshot.MinimumMarginPercent.Should().Be(50m); // Default
        snapshot.WarningMarginPercent.Should().Be(60m); // Default
        snapshot.AutoRecalculateCosts.Should().BeTrue(); // Default
    }

    [Fact]
    public async Task CostingSettingsGrain_ShouldAlertOnCostIncreaseAsync_BelowThreshold_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        await grain.InitializeAsync(locationId);
        // Default threshold is 5%

        // Act
        var shouldAlert = await grain.ShouldAlertOnCostIncreaseAsync(3m);

        // Assert
        shouldAlert.Should().BeFalse();
    }

    [Fact]
    public async Task CostingSettingsGrain_ShouldAlertOnCostIncreaseAsync_AboveThreshold_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        await grain.InitializeAsync(locationId);
        // Default threshold is 5%

        // Act
        var shouldAlert = await grain.ShouldAlertOnCostIncreaseAsync(7m);

        // Assert
        shouldAlert.Should().BeTrue();
    }

    [Fact]
    public async Task CostingSettingsGrain_IsMarginBelowMinimumAsync_AtThreshold_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        await grain.InitializeAsync(locationId);
        // Default minimum margin is 50%

        // Act
        var isBelowMinimum = await grain.IsMarginBelowMinimumAsync(50m);

        // Assert
        isBelowMinimum.Should().BeFalse();
    }

    [Fact]
    public async Task CostingSettingsGrain_IsMarginBelowMinimumAsync_BelowThreshold_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        await grain.InitializeAsync(locationId);
        // Default minimum margin is 50%

        // Act
        var isBelowMinimum = await grain.IsMarginBelowMinimumAsync(45m);

        // Assert
        isBelowMinimum.Should().BeTrue();
    }

    [Fact]
    public async Task CostingSettingsGrain_IsMarginBelowWarningAsync_AtThreshold_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        await grain.InitializeAsync(locationId);
        // Default warning margin is 60%

        // Act
        var isBelowWarning = await grain.IsMarginBelowWarningAsync(60m);

        // Assert
        isBelowWarning.Should().BeFalse();
    }

    [Fact]
    public async Task CostingSettingsGrain_IsMarginBelowWarningAsync_BelowThreshold_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        await grain.InitializeAsync(locationId);
        // Default warning margin is 60%

        // Act
        var isBelowWarning = await grain.IsMarginBelowWarningAsync(55m);

        // Assert
        isBelowWarning.Should().BeTrue();
    }

    [Fact]
    public async Task CostingSettingsGrain_InitializeAsync_AlreadyInitialized_ShouldBeIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        await grain.InitializeAsync(locationId);

        // Update settings
        await grain.UpdateAsync(new UpdateCostingSettingsCommand(
            TargetFoodCostPercent: 35m,
            TargetBeverageCostPercent: null,
            MinimumMarginPercent: null,
            WarningMarginPercent: null,
            PriceChangeAlertThreshold: null,
            CostIncreaseAlertThreshold: null,
            AutoRecalculateCosts: null,
            AutoCreateSnapshots: null,
            SnapshotFrequencyDays: null));

        // Act - try to initialize again
        await grain.InitializeAsync(locationId);
        var settings = await grain.GetSettingsAsync();

        // Assert - settings should remain updated, not reset to defaults
        settings.TargetFoodCostPercent.Should().Be(35m);
    }

    [Fact]
    public async Task CostingSettingsGrain_ExistsAsync_ShouldReturnCorrectStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ICostingSettingsGrain>(
            GrainKeys.CostingSettings(orgId, locationId));

        // Act - before initialization
        var existsBefore = await grain.ExistsAsync();

        await grain.InitializeAsync(locationId);

        // Act - after initialization
        var existsAfter = await grain.ExistsAsync();

        // Assert
        existsBefore.Should().BeFalse();
        existsAfter.Should().BeTrue();
    }
}
