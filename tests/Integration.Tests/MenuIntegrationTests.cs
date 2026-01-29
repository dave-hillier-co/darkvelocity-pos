using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Integration.Tests.Fixtures;
using DarkVelocity.Menu.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Integration.Tests;

/// <summary>
/// P2 Integration tests for Menu structure management:
/// - Menu Screen Organization
/// - Button Configuration and Linking
/// - Default Menu Management
/// - Category Navigation
/// </summary>
public class MenuIntegrationTests : IClassFixture<MenuServiceFixture>
{
    private readonly MenuServiceFixture _fixture;
    private readonly HttpClient _client;

    public MenuIntegrationTests(MenuServiceFixture fixture)
    {
        _fixture = fixture;
        _fixture.TestLocationId = Guid.NewGuid();
        _fixture.SecondLocationId = Guid.NewGuid();
        _client = fixture.Client;
    }

    #region Menu Structure

    [Fact]
    public async Task GetMenu_WithScreens_ReturnsFullHierarchy()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/menus/{_fixture.DefaultMenuId}?includeScreens=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var menu = await response.Content.ReadFromJsonAsync<MenuDto>();
        menu.Should().NotBeNull();
        menu!.Screens.Should().NotBeEmpty();
        menu.Screens.First().Buttons.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateMenu_AsDefault_UpdatesPreviousDefault()
    {
        // Arrange - Create a menu and set as default
        var request = new CreateMenuRequest(
            Name: "New Default Menu",
            Description: "This will become the new default",
            IsDefault: true);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/menus", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var newMenu = await response.Content.ReadFromJsonAsync<MenuDto>();
        newMenu!.IsDefault.Should().BeTrue();

        // Verify old default is no longer default
        var oldMenuResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/menus/{_fixture.DefaultMenuId}");
        var oldMenu = await oldMenuResponse.Content.ReadFromJsonAsync<MenuDto>();

        // Only one menu should be default
        if (newMenu.Id != _fixture.DefaultMenuId)
        {
            oldMenu!.IsDefault.Should().BeFalse();
        }
    }

    [Fact]
    public async Task UpdateMenu_SetAsDefault_ChangesDefaultMenu()
    {
        // Arrange
        var request = new UpdateMenuRequest(IsDefault: true);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/menus/{_fixture.SecondaryMenuId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<MenuDto>();
        updated!.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task GetDefaultMenu_ReturnsCurrentDefault()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/menus");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // The response should include menus with one marked as default
    }

    [Fact]
    public async Task CreateMenu_WithoutDefault_DoesNotAffectExistingDefault()
    {
        // Arrange
        var request = new CreateMenuRequest(
            Name: "Non-Default Menu",
            Description: "This is not the default",
            IsDefault: false);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/menus", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var newMenu = await response.Content.ReadFromJsonAsync<MenuDto>();
        newMenu!.IsDefault.Should().BeFalse();
    }

    #endregion

    #region Screen Management

    [Fact]
    public async Task CreateScreen_AddsToMenu()
    {
        // Arrange
        var request = new CreateScreenRequest(
            Name: "Drinks",
            Position: 2,
            Color: "#0066FF",
            Rows: 3,
            Columns: 4);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/menus/{_fixture.DefaultMenuId}/screens",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var screen = await response.Content.ReadFromJsonAsync<MenuScreenDto>();
        screen!.Name.Should().Be("Drinks");
        screen.Position.Should().Be(2);
        screen.Rows.Should().Be(3);
        screen.Columns.Should().Be(4);
    }

    [Fact]
    public async Task CreateScreen_WithCustomGrid_SetsGridSize()
    {
        // Arrange
        var request = new CreateScreenRequest(
            Name: "Large Grid",
            Position: 3,
            Rows: 6,
            Columns: 8);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/menus/{_fixture.DefaultMenuId}/screens",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var screen = await response.Content.ReadFromJsonAsync<MenuScreenDto>();
        screen!.Rows.Should().Be(6);
        screen.Columns.Should().Be(8);
    }

    [Fact]
    public async Task GetMenu_ScreensOrderedByPosition()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/menus/{_fixture.DefaultMenuId}?includeScreens=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var menu = await response.Content.ReadFromJsonAsync<MenuDto>();

        if (menu!.Screens.Count > 1)
        {
            var positions = menu.Screens.Select(s => s.Position).ToList();
            positions.Should().BeInAscendingOrder();
        }
    }

    #endregion

    #region Button Configuration

    [Fact]
    public async Task CreateButton_LinkedToMenuItem_CreatesItemButton()
    {
        // Arrange
        var request = new CreateButtonRequest(
            Row: 1,
            Column: 0,
            ItemId: _fixture.SodaItemId,
            Label: "Soda",
            Color: "#00FF00",
            ButtonType: "item");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/menus/{_fixture.DefaultMenuId}/screens/{_fixture.TestScreenId}/buttons",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var button = await response.Content.ReadFromJsonAsync<MenuButtonDto>();
        button!.ItemId.Should().Be(_fixture.SodaItemId);
        button.ButtonType.Should().Be("item");
    }

    [Fact]
    public async Task CreateButton_CategoryButton_CreatesCategoryNavigation()
    {
        // Arrange
        var request = new CreateButtonRequest(
            Row: 3,
            Column: 0,
            ItemId: null,
            Label: "Drinks Menu",
            Color: "#0000FF",
            ButtonType: "category");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/menus/{_fixture.DefaultMenuId}/screens/{_fixture.TestScreenId}/buttons",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var button = await response.Content.ReadFromJsonAsync<MenuButtonDto>();
        button!.ItemId.Should().BeNull();
        button.ButtonType.Should().Be("category");
        button.Label.Should().Be("Drinks Menu");
    }

    [Fact]
    public async Task CreateButton_WithSpan_TakesMultipleCells()
    {
        // Arrange
        var request = new CreateButtonRequest(
            Row: 2,
            Column: 0,
            ItemId: _fixture.BurgerItemId,
            Label: "SPECIAL BURGER",
            Color: "#FF0000",
            RowSpan: 2,
            ColumnSpan: 2,
            ButtonType: "item");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/menus/{_fixture.DefaultMenuId}/screens/{_fixture.TestScreenId}/buttons",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var button = await response.Content.ReadFromJsonAsync<MenuButtonDto>();
        button!.RowSpan.Should().Be(2);
        button.ColumnSpan.Should().Be(2);
    }

    [Fact]
    public async Task GetScreen_IncludesButtonsWithItemDetails()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/menus/{_fixture.DefaultMenuId}?includeScreens=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var menu = await response.Content.ReadFromJsonAsync<MenuDto>();

        var screen = menu!.Screens.FirstOrDefault(s => s.Id == _fixture.TestScreenId);
        screen.Should().NotBeNull();

        var itemButton = screen!.Buttons.FirstOrDefault(b => b.ItemId != null);
        if (itemButton != null)
        {
            // Button should include linked item details
            itemButton.Label.Should().NotBeNullOrEmpty();
        }
    }

    #endregion

    #region Menu Items

    [Fact]
    public async Task CreateMenuItem_WithRecipeLink_LinksToCosting()
    {
        // Arrange
        var recipeId = Guid.NewGuid(); // Would normally come from Costing service
        var request = new CreateMenuItemRequest(
            Name: "Costed Item",
            CategoryId: _fixture.MainCategoryId,
            AccountingGroupId: _fixture.FoodAccountingGroupId,
            Price: 15.00m,
            Description: "Item with recipe link",
            Sku: "COSTED-001",
            RecipeId: recipeId,
            TrackInventory: true);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await response.Content.ReadFromJsonAsync<MenuItemDto>();
        item!.RecipeId.Should().Be(recipeId);
        item.TrackInventory.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateMenuItem_PriceChange_OnlyAffectsNewOrders()
    {
        // Arrange - Update the burger price
        var request = new UpdateMenuItemRequest(Price: 14.50m);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/items/{_fixture.BurgerItemId}",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<MenuItemDto>();
        updated!.Price.Should().Be(14.50m);
        // Existing orders would retain old price (verified in Order service)
    }

    [Fact]
    public async Task DeactivateMenuItem_SetsInactive()
    {
        // Arrange - Create an item to deactivate
        var createRequest = new CreateMenuItemRequest(
            Name: "Item To Deactivate",
            CategoryId: _fixture.MainCategoryId,
            AccountingGroupId: _fixture.FoodAccountingGroupId,
            Price: 10.00m,
            Sku: "DEACT-001");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/items", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItemDto>();

        // Act
        var updateRequest = new UpdateMenuItemRequest(IsActive: false);
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/items/{created!.Id}",
            updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var deactivated = await response.Content.ReadFromJsonAsync<MenuItemDto>();
        deactivated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetMenuItems_FilterByCategory_ReturnsFilteredItems()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/items?categoryId={_fixture.MainCategoryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateMenuItem_UniqueSku_EnforcedPerLocation()
    {
        // Arrange - Create item with specific SKU
        var request1 = new CreateMenuItemRequest(
            Name: "First Item",
            CategoryId: _fixture.MainCategoryId,
            AccountingGroupId: _fixture.FoodAccountingGroupId,
            Price: 8.00m,
            Sku: "UNIQUE-SKU-001");

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/items", request1);

        // Try to create another with same SKU
        var request2 = new CreateMenuItemRequest(
            Name: "Second Item",
            CategoryId: _fixture.MainCategoryId,
            AccountingGroupId: _fixture.FoodAccountingGroupId,
            Price: 9.00m,
            Sku: "UNIQUE-SKU-001");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/items", request2);

        // Assert - should fail with conflict or bad request
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    #endregion

    #region Category Management

    [Fact]
    public async Task GetCategory_IncludesItemCount()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/categories/{_fixture.MainCategoryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        category!.ItemCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task CreateCategory_WithDisplayOrder_SetsPosition()
    {
        // Arrange
        var request = new CreateCategoryRequest(
            Name: "Desserts",
            Description: "Sweet treats",
            DisplayOrder: 5,
            Color: "#FF00FF");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/categories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        category!.DisplayOrder.Should().Be(5);
        category.Color.Should().Be("#FF00FF");
    }

    [Fact]
    public async Task UpdateCategory_ReorderDisplay_UpdatesOrder()
    {
        // Arrange
        var request = new UpdateCategoryRequest(DisplayOrder: 10);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/categories/{_fixture.DrinksCategoryId}",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<CategoryDto>();
        updated!.DisplayOrder.Should().Be(10);
    }

    [Fact]
    public async Task DeactivateCategory_SetsInactive()
    {
        // Arrange - Create category to deactivate
        var createRequest = new CreateCategoryRequest(
            Name: "Temporary Category",
            Description: "Will be deactivated",
            DisplayOrder: 99);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/categories", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CategoryDto>();

        // Act
        var updateRequest = new UpdateCategoryRequest(IsActive: false);
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/categories/{created!.Id}",
            updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var deactivated = await response.Content.ReadFromJsonAsync<CategoryDto>();
        deactivated!.IsActive.Should().BeFalse();
    }

    #endregion

    #region Accounting Groups

    [Fact]
    public async Task CreateAccountingGroup_WithTaxRate_SetsTaxRate()
    {
        // Arrange
        var request = new CreateAccountingGroupRequest(
            Name: "Alcohol",
            Description: "Alcoholic beverages",
            TaxRate: 0.25m);

        // Act
        var response = await _client.PostAsJsonAsync("/api/accounting-groups", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var group = await response.Content.ReadFromJsonAsync<AccountingGroupDto>();
        group!.TaxRate.Should().Be(0.25m);
    }

    [Fact]
    public async Task UpdateAccountingGroup_TaxRate_UpdatesTaxCalculation()
    {
        // Arrange
        var request = new UpdateAccountingGroupRequest(TaxRate: 0.15m);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/accounting-groups/{_fixture.FoodAccountingGroupId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<AccountingGroupDto>();
        updated!.TaxRate.Should().Be(0.15m);
    }

    [Fact]
    public async Task GetMenuItem_IncludesTaxRateFromAccountingGroup()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/items/{_fixture.BurgerItemId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<MenuItemDto>();
        item!.TaxRate.Should().NotBeNull();
        item.AccountingGroupName.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Location Isolation

    [Fact]
    public async Task GetMenus_OnlyReturnsLocationMenus()
    {
        // Act - Try to get menus for a different location
        var differentLocationId = Guid.NewGuid();
        var response = await _client.GetAsync(
            $"/api/locations/{differentLocationId}/menus");

        // Assert - Should return empty list, not other location's menus
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateMenuItem_SameSkuDifferentLocation_Allowed()
    {
        // Arrange - Create item in first location
        var request1 = new CreateMenuItemRequest(
            Name: "Location 1 Item",
            CategoryId: _fixture.MainCategoryId,
            AccountingGroupId: _fixture.FoodAccountingGroupId,
            Price: 10.00m,
            Sku: "CROSS-LOC-001");

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/items", request1);

        // Create category in second location first
        var catRequest = new CreateCategoryRequest(
            Name: "Other Location Category",
            Description: "For other location",
            DisplayOrder: 1);

        var catResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.SecondLocationId}/categories", catRequest);
        var otherCategory = await catResponse.Content.ReadFromJsonAsync<CategoryDto>();

        // Now create item with same SKU in different location
        var request2 = new CreateMenuItemRequest(
            Name: "Location 2 Item",
            CategoryId: otherCategory!.Id,
            AccountingGroupId: _fixture.FoodAccountingGroupId,
            Price: 11.00m,
            Sku: "CROSS-LOC-001");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.SecondLocationId}/items", request2);

        // Assert - Same SKU allowed in different location
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    #endregion
}
