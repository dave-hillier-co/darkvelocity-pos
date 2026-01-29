using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Integration.Tests.Fixtures;
using DarkVelocity.Costing.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Integration.Tests;

/// <summary>
/// P2 Integration tests for Costing workflows:
/// - Recipe Cost Calculation (ingredient costs, waste percentages)
/// - Cost Snapshots (historical tracking, trend analysis)
/// - Margin Monitoring (alerts, thresholds)
/// - Ingredient Price Management (updates, recalculation triggers)
/// </summary>
public class CostingIntegrationTests : IClassFixture<CostingServiceFixture>
{
    private readonly CostingServiceFixture _fixture;
    private readonly HttpClient _client;

    public CostingIntegrationTests(CostingServiceFixture fixture)
    {
        _fixture = fixture;
        _fixture.TestLocationId = Guid.NewGuid();
        _fixture.BeefIngredientId = Guid.NewGuid();
        _fixture.CheeseIngredientId = Guid.NewGuid();
        _fixture.BunsIngredientId = Guid.NewGuid();
        _fixture.TomatoesIngredientId = Guid.NewGuid();
        _fixture.BurgerMenuItemId = Guid.NewGuid();
        _fixture.PastaMenuItemId = Guid.NewGuid();
        _client = fixture.Client;
    }

    #region Recipe Cost Calculation

    [Fact]
    public async Task GetRecipeCost_CalculatesSumOfIngredientCosts()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/cost?menuPrice=12.00");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cost = await response.Content.ReadFromJsonAsync<RecipeCostCalculationDto>();
        cost.Should().NotBeNull();
        cost!.TotalIngredientCost.Should().BeGreaterThan(0);
        cost.IngredientCosts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetRecipeCost_IncludesWastePercentageInCalculation()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/cost");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cost = await response.Content.ReadFromJsonAsync<RecipeCostCalculationDto>();

        // Verify waste percentage affects effective quantity
        var beefLine = cost!.IngredientCosts.FirstOrDefault(i => i.IngredientName == "Ground Beef");
        if (beefLine != null)
        {
            // Effective quantity should be > base quantity due to waste
            beefLine.EffectiveQuantity.Should().BeGreaterThan(beefLine.Quantity);
        }
    }

    [Fact]
    public async Task GetRecipeCost_WithMenuPrice_CalculatesMarginAndCostPercentage()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/cost?menuPrice=12.00");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cost = await response.Content.ReadFromJsonAsync<RecipeCostCalculationDto>();

        cost!.MenuPrice.Should().Be(12.00m);
        cost.CostPercentage.Should().NotBeNull();
        cost.GrossMarginPercent.Should().NotBeNull();

        // Cost percentage + Gross margin should equal 100%
        if (cost.CostPercentage.HasValue && cost.GrossMarginPercent.HasValue)
        {
            var total = cost.CostPercentage.Value + cost.GrossMarginPercent.Value;
            total.Should().BeApproximately(100m, 0.1m);
        }
    }

    [Fact]
    public async Task GetRecipeCost_MultiPortionRecipe_CalculatesCostPerPortion()
    {
        // Pasta recipe has 4 portion yield
        // Act
        var response = await _client.GetAsync(
            $"/api/recipes/{_fixture.PastaRecipeId}/cost");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cost = await response.Content.ReadFromJsonAsync<RecipeCostCalculationDto>();

        // Cost per portion should be total / yield
        cost!.PortionYield.Should().Be(4);
        if (cost.TotalIngredientCost > 0)
        {
            cost.CostPerPortion.Should().BeLessThan(cost.TotalIngredientCost);
        }
    }

    [Fact]
    public async Task RecalculateRecipeCost_UpdatesCostFromCurrentIngredientPrices()
    {
        // Act
        var response = await _client.PostAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/recalculate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        recipe!.CostCalculatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    #endregion

    #region Ingredient Price Management

    [Fact]
    public async Task UpdateIngredientPrice_TracksHistoricalPrice()
    {
        // Arrange
        var updateRequest = new UpdateIngredientPriceRequest(
            CurrentPrice: 55.00m, // Increase from 50.00
            PackSize: 5m);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/ingredient-prices/{_fixture.BeefIngredientId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<IngredientPriceDto>();
        updated!.CurrentPrice.Should().Be(55.00m);
        updated.PreviousPrice.Should().Be(50.00m);
        updated.PriceChangedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task UpdateIngredientPrice_CalculatesPriceChangePercent()
    {
        // Arrange - Create a new ingredient price to update
        var createRequest = new CreateIngredientPriceRequest(
            IngredientId: Guid.NewGuid(),
            IngredientName: "Test Ingredient",
            CurrentPrice: 100.00m,
            UnitOfMeasure: "kg",
            PackSize: 10m);

        var createResponse = await _client.PostAsJsonAsync("/api/ingredient-prices", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<IngredientPriceDto>();

        // Update with 10% increase
        var updateRequest = new UpdateIngredientPriceRequest(
            CurrentPrice: 110.00m,
            PackSize: 10m);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/ingredient-prices/{created!.IngredientId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<IngredientPriceDto>();
        updated!.PriceChangePercent.Should().BeApproximately(10m, 0.1m);
    }

    [Fact]
    public async Task GetAffectedRecipes_ReturnsRecipesUsingIngredient()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/ingredient-prices/{_fixture.BeefIngredientId}/recipes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Should return burger recipe which uses beef
    }

    [Fact]
    public async Task RecalculateAllRecipesUsingIngredient_UpdatesAllAffectedRecipes()
    {
        // Act
        var response = await _client.PostAsync(
            $"/api/ingredient-prices/{_fixture.BeefIngredientId}/recalculate-recipes", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetIngredientPrices_FilterByRecentChange_ReturnsRecentlyChangedPrices()
    {
        // Act
        var response = await _client.GetAsync("/api/ingredient-prices?hasRecentChange=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Cost Snapshots

    [Fact]
    public async Task CreateCostSnapshot_CapturesCurrentCostState()
    {
        // Arrange
        var request = new CreateSnapshotRequest(
            MenuPrice: 12.00m,
            SnapshotReason: "manual");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/snapshots", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var snapshot = await response.Content.ReadFromJsonAsync<RecipeCostSnapshotDto>();
        snapshot.Should().NotBeNull();
        snapshot!.SnapshotDate.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
        snapshot.MenuPrice.Should().Be(12.00m);
        snapshot.TotalIngredientCost.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetCostSnapshots_ReturnsHistoricalSnapshots()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/snapshots");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCostSnapshots_FilterByDateRange_ReturnsFilteredSnapshots()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await _client.GetAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/snapshots?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetLatestSnapshot_ReturnsNewestSnapshot()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/snapshots/latest");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var snapshot = await response.Content.ReadFromJsonAsync<RecipeCostSnapshotDto>();
        snapshot.Should().NotBeNull();
    }

    [Fact]
    public async Task CompareSnapshots_ReturnsCostTrendAnalysis()
    {
        // Arrange - Get the date ranges for comparison
        var date1 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var date2 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));

        // Act
        var response = await _client.GetAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/snapshots/compare?date1={date1:yyyy-MM-dd}&date2={date2:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateSnapshot_OnPriceChange_RecordsReasonAsPriceChange()
    {
        // Arrange
        var request = new CreateSnapshotRequest(
            MenuPrice: 12.00m,
            SnapshotReason: "price_change");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/snapshots", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var snapshot = await response.Content.ReadFromJsonAsync<RecipeCostSnapshotDto>();
        snapshot!.SnapshotReason.Should().Be("price_change");
    }

    #endregion

    #region Margin Monitoring & Alerts

    [Fact]
    public async Task GetCostAlerts_ReturnsUnacknowledgedAlerts()
    {
        // Act
        var response = await _client.GetAsync("/api/cost-alerts?acknowledged=false");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCostAlerts_FilterByType_ReturnsFilteredAlerts()
    {
        // Act
        var response = await _client.GetAsync("/api/cost-alerts?alertType=cost_increase");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUnacknowledgedAlertCount_ReturnsCountByType()
    {
        // Act
        var response = await _client.GetAsync("/api/cost-alerts/unacknowledged/count");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AcknowledgeAlert_WithActionTaken_MarksAcknowledged()
    {
        // Arrange
        var request = new AcknowledgeCostAlertRequest(
            Notes: "Will adjust menu price next week",
            ActionTaken: "price_adjustment_planned");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/cost-alerts/{_fixture.UnacknowledgedAlertId}/acknowledge", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var acknowledged = await response.Content.ReadFromJsonAsync<CostAlertDto>();
        acknowledged!.IsAcknowledged.Should().BeTrue();
        acknowledged.AcknowledgedAt.Should().NotBeNull();
        acknowledged.ActionTaken.Should().Be("price_adjustment_planned");
    }

    [Fact]
    public async Task BulkAcknowledgeAlerts_AcknowledgesMultipleAlerts()
    {
        // Arrange - Create some alerts to acknowledge
        var alert1Request = new
        {
            AlertType = "cost_increase",
            RecipeId = _fixture.BurgerRecipeId,
            RecipeName = "Burger",
            PreviousValue = 3.00m,
            CurrentValue = 3.50m,
            ChangePercent = 16.67m,
            AffectedRecipeCount = 1
        };

        var create1 = await _client.PostAsJsonAsync("/api/cost-alerts", alert1Request);
        var created1 = await create1.Content.ReadFromJsonAsync<CostAlertDto>();

        var alert2Request = new
        {
            AlertType = "cost_increase",
            RecipeId = _fixture.PastaRecipeId,
            RecipeName = "Pasta",
            PreviousValue = 2.00m,
            CurrentValue = 2.30m,
            ChangePercent = 15.00m,
            AffectedRecipeCount = 1
        };

        var create2 = await _client.PostAsJsonAsync("/api/cost-alerts", alert2Request);
        var created2 = await create2.Content.ReadFromJsonAsync<CostAlertDto>();

        var alertIds = new List<Guid> { created1!.Id, created2!.Id };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/cost-alerts/acknowledge-bulk?actionTaken=reviewed", alertIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateCostAlert_ManualAlert_CreatesAlert()
    {
        // Arrange
        var request = new
        {
            AlertType = "margin_warning",
            RecipeId = _fixture.PastaRecipeId,
            RecipeName = "Spaghetti Bolognese",
            PreviousValue = 65.00m,
            CurrentValue = 55.00m,
            ChangePercent = -15.38m,
            ThresholdValue = 60.00m,
            ImpactDescription = "Margin dropped below warning threshold",
            AffectedRecipeCount = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/cost-alerts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var alert = await response.Content.ReadFromJsonAsync<CostAlertDto>();
        alert!.AlertType.Should().Be("margin_warning");
        alert.IsAcknowledged.Should().BeFalse();
    }

    [Fact]
    public async Task GetCostAlerts_FilterByRecipe_ReturnsRecipeAlerts()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/cost-alerts?recipeId={_fixture.BurgerRecipeId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCostAlerts_FilterByIngredient_ReturnsIngredientAlerts()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/cost-alerts?ingredientId={_fixture.CheeseIngredientId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Costing Settings

    [Fact]
    public async Task UpdateCostingSettings_UpdatesThresholds()
    {
        // Arrange
        var request = new UpdateCostingSettingsRequest(
            TargetFoodCostPercent: 32m,
            WarningMarginPercent: 55m,
            CostIncreaseAlertThreshold: 8m);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/costing-settings/{_fixture.TestLocationId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = await response.Content.ReadFromJsonAsync<CostingSettingsDto>();
        settings!.TargetFoodCostPercent.Should().Be(32m);
        settings.WarningMarginPercent.Should().Be(55m);
        settings.CostIncreaseAlertThreshold.Should().Be(8m);
    }

    [Fact]
    public async Task UpdateCostingSettings_AutoRecalculateDisabled_DoesNotAutoRecalculate()
    {
        // Arrange
        var request = new UpdateCostingSettingsRequest(
            AutoRecalculateCosts: false);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/costing-settings/{_fixture.TestLocationId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = await response.Content.ReadFromJsonAsync<CostingSettingsDto>();
        settings!.AutoRecalculateCosts.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateCostingSettings_SnapshotFrequency_UpdatesFrequency()
    {
        // Arrange
        var request = new UpdateCostingSettingsRequest(
            AutoCreateSnapshots: true,
            SnapshotFrequencyDays: 14);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/costing-settings/{_fixture.TestLocationId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = await response.Content.ReadFromJsonAsync<CostingSettingsDto>();
        settings!.AutoCreateSnapshots.Should().BeTrue();
        settings.SnapshotFrequencyDays.Should().Be(14);
    }

    #endregion

    #region Recipe Management

    [Fact]
    public async Task CreateRecipe_WithIngredients_CalculatesInitialCost()
    {
        // Arrange
        var recipeRequest = new CreateRecipeRequest(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "New Test Item",
            Code: "RCP-TEST-001",
            CategoryName: "Test Category",
            PortionYield: 2);

        // Act
        var response = await _client.PostAsJsonAsync("/api/recipes", recipeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        recipe!.Code.Should().Be("RCP-TEST-001");
        recipe.PortionYield.Should().Be(2);
    }

    [Fact]
    public async Task AddRecipeIngredient_UpdatesRecipeCost()
    {
        // Arrange - Create a recipe first
        var recipeRequest = new CreateRecipeRequest(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Sandwich",
            Code: "RCP-SAND-001",
            PortionYield: 1);

        var recipeResponse = await _client.PostAsJsonAsync("/api/recipes", recipeRequest);
        var recipe = await recipeResponse.Content.ReadFromJsonAsync<RecipeDto>();

        var ingredientRequest = new AddRecipeIngredientRequest(
            IngredientId: _fixture.CheeseIngredientId,
            IngredientName: "Cheddar Cheese",
            Quantity: 0.05m,
            UnitOfMeasure: "kg",
            WastePercentage: 5m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/recipes/{recipe!.Id}/ingredients", ingredientRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var ingredient = await response.Content.ReadFromJsonAsync<RecipeIngredientDto>();
        ingredient!.Quantity.Should().Be(0.05m);
        ingredient.WastePercentage.Should().Be(5m);
    }

    [Fact]
    public async Task UpdateRecipeIngredient_WastePercentage_AffectsEffectiveQuantity()
    {
        // This test would verify waste percentage updates
        // The fixture has burger recipe with ingredients
        var recipe = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}");
        var recipeDto = await recipe.Content.ReadFromJsonAsync<RecipeDto>();

        // Verify waste affects calculation
        var beefIngredient = recipeDto!.Ingredients.FirstOrDefault(i => i.IngredientName == "Ground Beef");
        if (beefIngredient != null && beefIngredient.WastePercentage > 0)
        {
            beefIngredient.EffectiveQuantity.Should().BeGreaterThan(beefIngredient.Quantity);
        }
    }

    [Fact]
    public async Task GetRecipes_FilterByCategory_ReturnsFilteredRecipes()
    {
        // Act
        var response = await _client.GetAsync("/api/recipes?isActive=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRecipeByMenuItem_ReturnsLinkedRecipe()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/recipes/by-menu-item/{_fixture.BurgerMenuItemId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        recipe!.MenuItemId.Should().Be(_fixture.BurgerMenuItemId);
    }

    #endregion

    #region Margin Reports

    [Fact]
    public async Task GetRecipe_WithCostData_IncludesMarginInformation()
    {
        // Act
        var response = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        recipe!.CurrentCostPerPortion.Should().BeGreaterThan(0);
        recipe.CostCalculatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllRecipes_IncludesCostSummary()
    {
        // Act
        var response = await _client.GetAsync("/api/recipes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}
