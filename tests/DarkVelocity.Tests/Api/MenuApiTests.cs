using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace DarkVelocity.Tests.Api;

[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public class MenuApiTests
{
    private readonly HttpClient _client;

    public MenuApiTests(ApiTestFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    private async Task<Guid> CreateOrganizationAsync()
    {
        var request = new { name = $"Test Org {Guid.NewGuid()}", slug = $"test-org-{Guid.NewGuid()}" };
        var response = await _client.PostAsJsonAsync("/api/orgs", request);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task CreateMenuCategory_ReturnsCreatedWithHalResponse()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var request = new
        {
            locationId = Guid.NewGuid(),
            name = "Appetizers",
            description = "Starters and small plates",
            displayOrder = 1,
            color = "#FF5733"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/menu/categories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().NotBeEmpty();
        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain("/categories/");
        json.RootElement.GetProperty("_links").GetProperty("items").GetProperty("href").GetString()
            .Should().EndWith("/items");
    }

    [Fact]
    public async Task GetMenuCategory_WhenExists_ReturnsOkWithHalResponse()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var createRequest = new
        {
            locationId = Guid.NewGuid(),
            name = "Main Courses",
            description = "Entrees",
            displayOrder = 2,
            color = "#33FF57"
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/menu/categories", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var categoryId = createJson.RootElement.GetProperty("id").GetGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/menu/categories/{categoryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().Be(categoryId);
        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain($"/categories/{categoryId}");
    }

    [Fact]
    public async Task CreateMenuItem_ReturnsCreatedWithHalResponse()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();

        // Create category first
        var categoryRequest = new
        {
            locationId = Guid.NewGuid(),
            name = "Burgers",
            description = "Handcrafted burgers",
            displayOrder = 1,
            color = "#FF0000"
        };
        var categoryResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/menu/categories", categoryRequest);
        var categoryContent = await categoryResponse.Content.ReadAsStringAsync();
        var categoryJson = JsonDocument.Parse(categoryContent);
        var categoryId = categoryJson.RootElement.GetProperty("id").GetGuid();

        var itemRequest = new
        {
            locationId = Guid.NewGuid(),
            categoryId = categoryId,
            name = "Classic Burger",
            price = 12.99m,
            description = "A classic beef patty with lettuce, tomato, and cheese",
            sku = "BRG001"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/menu/items", itemRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().NotBeEmpty();
        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain("/items/");
        json.RootElement.GetProperty("_links").GetProperty("category").GetProperty("href").GetString()
            .Should().Contain($"/categories/{categoryId}");
    }

    [Fact]
    public async Task GetMenuItem_WhenExists_ReturnsOkWithHalResponse()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();

        // Create category
        var categoryRequest = new { locationId = Guid.NewGuid(), name = "Salads", description = "Fresh salads", displayOrder = 1, color = "#00FF00" };
        var categoryResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/menu/categories", categoryRequest);
        var categoryContent = await categoryResponse.Content.ReadAsStringAsync();
        var categoryJson = JsonDocument.Parse(categoryContent);
        var categoryId = categoryJson.RootElement.GetProperty("id").GetGuid();

        // Create menu item
        var itemRequest = new { locationId = Guid.NewGuid(), categoryId = categoryId, name = "Caesar Salad", price = 9.99m };
        var itemResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/menu/items", itemRequest);
        var itemContent = await itemResponse.Content.ReadAsStringAsync();
        var itemJson = JsonDocument.Parse(itemContent);
        var itemId = itemJson.RootElement.GetProperty("id").GetGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/menu/items/{itemId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().Be(itemId);
        json.RootElement.GetProperty("name").GetString().Should().Be("Caesar Salad");
        json.RootElement.GetProperty("_links").GetProperty("category").GetProperty("href").GetString()
            .Should().Contain($"/categories/{categoryId}");
    }

    [Fact]
    public async Task UpdateMenuItem_ReturnsOkWithUpdatedData()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();

        // Create category
        var categoryRequest = new { locationId = Guid.NewGuid(), name = "Drinks", description = "Beverages", displayOrder = 1, color = "#0000FF" };
        var categoryResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/menu/categories", categoryRequest);
        var categoryContent = await categoryResponse.Content.ReadAsStringAsync();
        var categoryJson = JsonDocument.Parse(categoryContent);
        var categoryId = categoryJson.RootElement.GetProperty("id").GetGuid();

        // Create menu item
        var itemRequest = new { locationId = Guid.NewGuid(), categoryId = categoryId, name = "Soda", price = 2.99m };
        var itemResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/menu/items", itemRequest);
        var itemContent = await itemResponse.Content.ReadAsStringAsync();
        var itemJson = JsonDocument.Parse(itemContent);
        var itemId = itemJson.RootElement.GetProperty("id").GetGuid();

        var updateRequest = new { name = "Large Soda", price = 3.49m };

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/orgs/{orgId}/menu/items/{itemId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain($"/items/{itemId}");
    }
}
