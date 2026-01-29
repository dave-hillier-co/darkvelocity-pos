using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Integration.Tests.Fixtures;
using DarkVelocity.Inventory.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Integration.Tests;

/// <summary>
/// Integration tests for Inventory and Stock Management workflows.
///
/// Business Scenarios Covered:
/// - Stock batch management (receiving, tracking)
/// - FIFO stock consumption
/// - Stock level queries
/// - Low stock detection
/// - Waste recording
/// - Recipe-based stock consumption
/// </summary>
public class InventoryIntegrationTests : IClassFixture<InventoryServiceFixture>
{
    private readonly InventoryServiceFixture _fixture;
    private readonly HttpClient _client;

    public InventoryIntegrationTests(InventoryServiceFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    #region Ingredient Management

    [Fact]
    public async Task CreateIngredient_WithValidData_CreatesIngredient()
    {
        // Arrange
        var code = $"ING-{Guid.NewGuid():N}".Substring(0, 20);
        var request = new CreateIngredientRequest(
            Code: code,
            Name: "Test Ingredient",
            UnitOfMeasure: "kg",
            Category: "proteins",
            StorageType: "chilled",
            ReorderLevel: 10m,
            ReorderQuantity: 25m);

        // Act
        var response = await _client.PostAsJsonAsync("/api/ingredients", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var ingredient = await response.Content.ReadFromJsonAsync<IngredientDto>();
        ingredient.Should().NotBeNull();
        ingredient!.Code.Should().Be(code);
        ingredient.Name.Should().Be("Test Ingredient");
        ingredient.UnitOfMeasure.Should().Be("kg");
        ingredient.Category.Should().Be("proteins");
        ingredient.StorageType.Should().Be("chilled");
        ingredient.ReorderLevel.Should().Be(10m);
        ingredient.ReorderQuantity.Should().Be(25m);
        ingredient.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetIngredients_ReturnsAllIngredients()
    {
        // Act
        var response = await _client.GetAsync("/api/ingredients");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<IngredientDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetIngredientById_ReturnsIngredient()
    {
        // Act
        var response = await _client.GetAsync($"/api/ingredients/{_fixture.BeefIngredientId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var ingredient = await response.Content.ReadFromJsonAsync<IngredientDto>();
        ingredient.Should().NotBeNull();
        ingredient!.Id.Should().Be(_fixture.BeefIngredientId);
        ingredient.Code.Should().Be("BEEF-PATTY");
    }

    [Fact]
    public async Task UpdateIngredient_UpdatesFields()
    {
        // Arrange - Create a new ingredient to update
        var createRequest = new CreateIngredientRequest(
            Code: $"UPDATE-{Guid.NewGuid():N}".Substring(0, 20),
            Name: "Original Name",
            UnitOfMeasure: "unit");

        var createResponse = await _client.PostAsJsonAsync("/api/ingredients", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<IngredientDto>();

        var updateRequest = new UpdateIngredientRequest(
            Name: "Updated Name",
            ReorderLevel: 15m);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/ingredients/{created!.Id}",
            updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<IngredientDto>();
        updated!.Name.Should().Be("Updated Name");
        updated.ReorderLevel.Should().Be(15m);
    }

    #endregion

    #region Stock Batch Management

    [Fact]
    public async Task CreateStockBatch_CreatesActiveBatch()
    {
        // Arrange - Create a new ingredient first
        var ingredientRequest = new CreateIngredientRequest(
            Code: $"BATCH-TEST-{Guid.NewGuid():N}".Substring(0, 20),
            Name: "Batch Test Ingredient",
            UnitOfMeasure: "kg");
        var ingredientResponse = await _client.PostAsJsonAsync("/api/ingredients", ingredientRequest);
        var ingredient = await ingredientResponse.Content.ReadFromJsonAsync<IngredientDto>();

        var request = new CreateStockBatchRequest(
            IngredientId: ingredient!.Id,
            Quantity: 25m,
            UnitCost: 5.50m,
            BatchNumber: "BATCH-001",
            ExpiryDate: DateTime.UtcNow.AddDays(30));

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/batches",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var batch = await response.Content.ReadFromJsonAsync<StockBatchDto>();
        batch.Should().NotBeNull();
        batch!.InitialQuantity.Should().Be(25m);
        batch.RemainingQuantity.Should().Be(25m);
        batch.UnitCost.Should().Be(5.50m);
        batch.BatchNumber.Should().Be("BATCH-001");
        batch.Status.Should().Be("active");
    }

    [Fact]
    public async Task CreateStockBatch_InvalidIngredient_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateStockBatchRequest(
            IngredientId: Guid.NewGuid(), // Non-existent
            Quantity: 10m,
            UnitCost: 5.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/batches",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBatches_ReturnsActiveBatchesForIngredient()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/{_fixture.BeefIngredientId}/batches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<StockBatchDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
        collection.Embedded.Items.Should().OnlyContain(b => b.IngredientId == _fixture.BeefIngredientId);
    }

    [Fact]
    public async Task GetBatches_ReturnsInFifoOrder()
    {
        // Act - Get batches for beef (which has two batches with different costs)
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/{_fixture.BeefIngredientId}/batches");

        // Assert
        var collection = await response.Content.ReadFromJsonAsync<HalCollection<StockBatchDto>>();
        var batches = collection!.Embedded.Items.ToList();

        // First batch should be older (cheaper at $2.00)
        batches.Should().HaveCountGreaterOrEqualTo(2);
        batches[0].UnitCost.Should().Be(2.00m);
        batches[1].UnitCost.Should().Be(2.50m);
    }

    #endregion

    #region Stock Levels

    [Fact]
    public async Task GetStockLevels_ReturnsAggregatedStockForLocation()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<StockLevelDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetStockLevels_CalculatesTotalStockFromBatches()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock");

        // Assert
        var collection = await response.Content.ReadFromJsonAsync<HalCollection<StockLevelDto>>();
        var beefStock = collection!.Embedded.Items.FirstOrDefault(s => s.IngredientCode == "BEEF-PATTY");

        beefStock.Should().NotBeNull();
        beefStock!.TotalStock.Should().Be(100m); // 50 + 50 from two batches
        beefStock.ActiveBatchCount.Should().Be(2);
    }

    [Fact]
    public async Task GetLowStock_ReturnsOnlyLowStockItems()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/low-stock");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<StockLevelDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().OnlyContain(s => s.IsLowStock);
    }

    #endregion

    #region FIFO Stock Consumption

    [Fact]
    public async Task ConsumeStock_ConsumesFromOldestBatchFirst_FIFO()
    {
        // Arrange - Create a new ingredient with two batches
        var ingredientRequest = new CreateIngredientRequest(
            Code: $"FIFO-{Guid.NewGuid():N}".Substring(0, 20),
            Name: "FIFO Test Ingredient",
            UnitOfMeasure: "unit");
        var ingredientResponse = await _client.PostAsJsonAsync("/api/ingredients", ingredientRequest);
        var ingredient = await ingredientResponse.Content.ReadFromJsonAsync<IngredientDto>();

        // Create first batch (older, cheaper at $4.00)
        var batch1Request = new CreateStockBatchRequest(
            IngredientId: ingredient!.Id,
            Quantity: 10m,
            UnitCost: 4.00m);
        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/batches",
            batch1Request);

        // Wait a tiny bit to ensure different timestamps
        await Task.Delay(50);

        // Create second batch (newer, more expensive at $6.00)
        var batch2Request = new CreateStockBatchRequest(
            IngredientId: ingredient.Id,
            Quantity: 10m,
            UnitCost: 6.00m);
        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/batches",
            batch2Request);

        // Consume 5 units - should come from first batch at $4.00
        var consumeRequest = new ConsumeStockRequest(
            IngredientId: ingredient.Id,
            Quantity: 5m,
            ConsumptionType: "sale");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/consume",
            consumeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ConsumptionResultDto>();
        result!.TotalQuantityConsumed.Should().Be(5m);
        result.TotalCost.Should().Be(20.00m); // 5 * $4.00
        result.BatchConsumptions.Should().HaveCount(1);
        result.BatchConsumptions[0].UnitCost.Should().Be(4.00m);
    }

    [Fact]
    public async Task ConsumeStock_SpansMultipleBatches_FIFO()
    {
        // Arrange
        var ingredientRequest = new CreateIngredientRequest(
            Code: $"FIFO-SPAN-{Guid.NewGuid():N}".Substring(0, 20),
            Name: "FIFO Span Test",
            UnitOfMeasure: "unit");
        var ingredientResponse = await _client.PostAsJsonAsync("/api/ingredients", ingredientRequest);
        var ingredient = await ingredientResponse.Content.ReadFromJsonAsync<IngredientDto>();

        // Create first batch with 3 units at $4.00
        var batch1Request = new CreateStockBatchRequest(
            IngredientId: ingredient!.Id,
            Quantity: 3m,
            UnitCost: 4.00m);
        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/batches",
            batch1Request);

        await Task.Delay(50);

        // Create second batch with 5 units at $6.00
        var batch2Request = new CreateStockBatchRequest(
            IngredientId: ingredient.Id,
            Quantity: 5m,
            UnitCost: 6.00m);
        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/batches",
            batch2Request);

        // Consume 5 units - should take 3 from first batch ($4.00) and 2 from second ($6.00)
        var consumeRequest = new ConsumeStockRequest(
            IngredientId: ingredient.Id,
            Quantity: 5m,
            ConsumptionType: "sale");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/consume",
            consumeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ConsumptionResultDto>();
        result!.TotalQuantityConsumed.Should().Be(5m);
        result.TotalCost.Should().Be(24.00m); // (3 * $4.00) + (2 * $6.00) = $12 + $12
        result.BatchConsumptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConsumeStock_UpdatesRemainingQuantity()
    {
        // Arrange
        var ingredientRequest = new CreateIngredientRequest(
            Code: $"CONSUME-{Guid.NewGuid():N}".Substring(0, 20),
            Name: "Consume Test",
            UnitOfMeasure: "unit");
        var ingredientResponse = await _client.PostAsJsonAsync("/api/ingredients", ingredientRequest);
        var ingredient = await ingredientResponse.Content.ReadFromJsonAsync<IngredientDto>();

        var batchRequest = new CreateStockBatchRequest(
            IngredientId: ingredient!.Id,
            Quantity: 20m,
            UnitCost: 5.00m);
        var batchResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/batches",
            batchRequest);
        var batch = await batchResponse.Content.ReadFromJsonAsync<StockBatchDto>();

        // Consume 8 units
        var consumeRequest = new ConsumeStockRequest(
            IngredientId: ingredient.Id,
            Quantity: 8m,
            ConsumptionType: "sale");

        // Act
        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/consume",
            consumeRequest);

        // Assert - Check remaining quantity
        var batchesResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/{ingredient.Id}/batches");
        var batches = await batchesResponse.Content.ReadFromJsonAsync<HalCollection<StockBatchDto>>();
        var updatedBatch = batches!.Embedded.Items.First(b => b.Id == batch!.Id);

        updatedBatch.RemainingQuantity.Should().Be(12m); // 20 - 8
    }

    [Fact]
    public async Task ConsumeStock_InsufficientStock_ReturnsWarning()
    {
        // Arrange
        var ingredientRequest = new CreateIngredientRequest(
            Code: $"INSUFF-{Guid.NewGuid():N}".Substring(0, 20),
            Name: "Insufficient Test",
            UnitOfMeasure: "unit");
        var ingredientResponse = await _client.PostAsJsonAsync("/api/ingredients", ingredientRequest);
        var ingredient = await ingredientResponse.Content.ReadFromJsonAsync<IngredientDto>();

        // Create batch with only 2 units
        var batchRequest = new CreateStockBatchRequest(
            IngredientId: ingredient!.Id,
            Quantity: 2m,
            UnitCost: 5.00m);
        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/batches",
            batchRequest);

        // Try to consume 5 units (more than available)
        var consumeRequest = new ConsumeStockRequest(
            IngredientId: ingredient.Id,
            Quantity: 5m,
            ConsumptionType: "sale");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/consume",
            consumeRequest);

        // Assert - Should still succeed but with warning
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseText = await response.Content.ReadAsStringAsync();
        responseText.Should().Contain("warning");
    }

    [Fact]
    public async Task ConsumeStock_TracksOrderId()
    {
        // Arrange
        var ingredientRequest = new CreateIngredientRequest(
            Code: $"ORDER-{Guid.NewGuid():N}".Substring(0, 20),
            Name: "Order Track Test",
            UnitOfMeasure: "unit");
        var ingredientResponse = await _client.PostAsJsonAsync("/api/ingredients", ingredientRequest);
        var ingredient = await ingredientResponse.Content.ReadFromJsonAsync<IngredientDto>();

        var batchRequest = new CreateStockBatchRequest(
            IngredientId: ingredient!.Id,
            Quantity: 10m,
            UnitCost: 5.00m);
        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/batches",
            batchRequest);

        var orderId = Guid.NewGuid();
        var consumeRequest = new ConsumeStockRequest(
            IngredientId: ingredient.Id,
            Quantity: 2m,
            OrderId: orderId,
            ConsumptionType: "sale");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/consume",
            consumeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Waste Recording

    [Fact]
    public async Task RecordWaste_RecordsWasteAndReducesStock()
    {
        // Arrange
        var ingredientRequest = new CreateIngredientRequest(
            Code: $"WASTE-{Guid.NewGuid():N}".Substring(0, 20),
            Name: "Waste Test Ingredient",
            UnitOfMeasure: "unit");
        var ingredientResponse = await _client.PostAsJsonAsync("/api/ingredients", ingredientRequest);
        var ingredient = await ingredientResponse.Content.ReadFromJsonAsync<IngredientDto>();

        var batchRequest = new CreateStockBatchRequest(
            IngredientId: ingredient!.Id,
            Quantity: 20m,
            UnitCost: 5.00m);
        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/batches",
            batchRequest);

        var wasteRequest = new RecordWasteRequest(
            IngredientId: ingredient.Id,
            Quantity: 3m,
            Reason: "spoilage",
            RecordedByUserId: Guid.NewGuid(),
            Notes: "Found expired in back of cooler");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/waste",
            wasteRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var wasteRecord = await response.Content.ReadFromJsonAsync<WasteRecordDto>();
        wasteRecord.Should().NotBeNull();
        wasteRecord!.Quantity.Should().Be(3m);
        wasteRecord.Reason.Should().Be("spoilage");
        wasteRecord.Notes.Should().Be("Found expired in back of cooler");
        wasteRecord.EstimatedCost.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetWasteRecords_ReturnsWasteForLocation()
    {
        // Arrange - Create waste record first
        var ingredientRequest = new CreateIngredientRequest(
            Code: $"WASTELIST-{Guid.NewGuid():N}".Substring(0, 20),
            Name: "Waste List Test",
            UnitOfMeasure: "unit");
        var ingredientResponse = await _client.PostAsJsonAsync("/api/ingredients", ingredientRequest);
        var ingredient = await ingredientResponse.Content.ReadFromJsonAsync<IngredientDto>();

        var batchRequest = new CreateStockBatchRequest(
            IngredientId: ingredient!.Id,
            Quantity: 10m,
            UnitCost: 5.00m);
        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/batches",
            batchRequest);

        var wasteRequest = new RecordWasteRequest(
            IngredientId: ingredient.Id,
            Quantity: 2m,
            Reason: "damaged",
            RecordedByUserId: Guid.NewGuid());

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/waste",
            wasteRequest);

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/waste");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<WasteRecordDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    #endregion

    #region Recipe Management

    [Fact]
    public async Task CreateRecipe_WithIngredients_CreatesRecipe()
    {
        // Arrange
        var recipeRequest = new CreateRecipeRequest(
            Code: $"RCP-{Guid.NewGuid():N}".Substring(0, 20),
            Name: "Test Recipe",
            MenuItemId: Guid.NewGuid(),
            PortionYield: 4,
            Instructions: "Mix ingredients and cook");

        // Act
        var response = await _client.PostAsJsonAsync("/api/recipes", recipeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        recipe.Should().NotBeNull();
        recipe!.Name.Should().Be("Test Recipe");
        recipe.PortionYield.Should().Be(4);
        recipe.Instructions.Should().Be("Mix ingredients and cook");
        recipe.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task AddIngredientToRecipe_AddsIngredient()
    {
        // Arrange - Create a recipe first
        var recipeRequest = new CreateRecipeRequest(
            Code: $"RCP-ING-{Guid.NewGuid():N}".Substring(0, 20),
            Name: "Recipe With Ingredients");
        var recipeResponse = await _client.PostAsJsonAsync("/api/recipes", recipeRequest);
        var recipe = await recipeResponse.Content.ReadFromJsonAsync<RecipeDto>();

        var addIngredientRequest = new AddRecipeIngredientRequest(
            IngredientId: _fixture.BeefIngredientId,
            Quantity: 2m,
            WastePercentage: 5m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/recipes/{recipe!.Id}/ingredients",
            addIngredientRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var recipeIngredient = await response.Content.ReadFromJsonAsync<RecipeIngredientDto>();
        recipeIngredient.Should().NotBeNull();
        recipeIngredient!.IngredientId.Should().Be(_fixture.BeefIngredientId);
        recipeIngredient.Quantity.Should().Be(2m);
        recipeIngredient.WastePercentage.Should().Be(5m);
        recipeIngredient.EffectiveQuantity.Should().Be(2.1m); // 2 * 1.05
    }

    [Fact]
    public async Task GetRecipeById_ReturnsRecipeWithIngredients()
    {
        // Act
        var response = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        recipe.Should().NotBeNull();
        recipe!.Id.Should().Be(_fixture.BurgerRecipeId);
        recipe.Name.Should().Be("Classic Cheeseburger");
        recipe.Ingredients.Should().NotBeEmpty();
    }

    #endregion

    #region Location Isolation

    [Fact]
    public async Task StockBatches_IsolatedByLocation()
    {
        // Arrange
        var location1 = Guid.NewGuid();
        var location2 = Guid.NewGuid();

        var ingredientRequest = new CreateIngredientRequest(
            Code: $"ISOLATE-{Guid.NewGuid():N}".Substring(0, 20),
            Name: "Isolation Test",
            UnitOfMeasure: "unit");
        var ingredientResponse = await _client.PostAsJsonAsync("/api/ingredients", ingredientRequest);
        var ingredient = await ingredientResponse.Content.ReadFromJsonAsync<IngredientDto>();

        // Create batch in location 1
        var batch1Request = new CreateStockBatchRequest(
            IngredientId: ingredient!.Id,
            Quantity: 10m,
            UnitCost: 5.00m);
        await _client.PostAsJsonAsync($"/api/locations/{location1}/stock/batches", batch1Request);

        // Create batch in location 2
        var batch2Request = new CreateStockBatchRequest(
            IngredientId: ingredient.Id,
            Quantity: 20m,
            UnitCost: 6.00m);
        await _client.PostAsJsonAsync($"/api/locations/{location2}/stock/batches", batch2Request);

        // Act - Get batches for location 1
        var response = await _client.GetAsync(
            $"/api/locations/{location1}/stock/{ingredient.Id}/batches");

        // Assert
        var collection = await response.Content.ReadFromJsonAsync<HalCollection<StockBatchDto>>();
        collection!.Embedded.Items.Should().HaveCount(1);
        collection.Embedded.Items.First().RemainingQuantity.Should().Be(10m);
    }

    #endregion
}
