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
}
