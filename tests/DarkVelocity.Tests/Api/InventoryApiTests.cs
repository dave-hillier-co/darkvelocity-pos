using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace DarkVelocity.Tests.Api;

[Collection(ApiCollection.Name)]
public class InventoryApiTests
{
    private readonly HttpClient _client;

    public InventoryApiTests(ApiTestFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    private async Task<(Guid OrgId, Guid SiteId)> CreateOrgAndSiteAsync()
    {
        // Create organization
        var orgRequest = new { name = $"Test Org {Guid.NewGuid()}", slug = $"test-org-{Guid.NewGuid()}" };
        var orgResponse = await _client.PostAsJsonAsync("/api/orgs", orgRequest);
        var orgContent = await orgResponse.Content.ReadAsStringAsync();
        var orgJson = JsonDocument.Parse(orgContent);
        var orgId = orgJson.RootElement.GetProperty("id").GetGuid();

        // Create site
        var siteRequest = new
        {
            name = "Test Site",
            code = $"TS{Guid.NewGuid().ToString()[..4]}",
            address = new { street = "123 Main St", city = "New York", state = "NY", postalCode = "10001", country = "USA" }
        };
        var siteResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites", siteRequest);
        var siteContent = await siteResponse.Content.ReadAsStringAsync();
        var siteJson = JsonDocument.Parse(siteContent);
        var siteId = siteJson.RootElement.GetProperty("id").GetGuid();

        return (orgId, siteId);
    }

    [Fact]
    public async Task InitializeInventory_ReturnsCreatedWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var ingredientId = Guid.NewGuid();
        var request = new
        {
            ingredientId = ingredientId,
            ingredientName = "Flour",
            sku = "FLR001",
            unit = "kg",
            category = "Dry Goods",
            reorderPoint = 10m,
            parLevel = 50m
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/inventory", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("ingredientId").GetGuid().Should().Be(ingredientId);
        json.RootElement.GetProperty("ingredientName").GetString().Should().Be("Flour");
        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain($"/inventory/{ingredientId}");
        json.RootElement.GetProperty("_links").GetProperty("receive").GetProperty("href").GetString()
            .Should().EndWith("/receive");
    }

    [Fact]
    public async Task GetInventory_WhenExists_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var ingredientId = Guid.NewGuid();
        var initRequest = new { ingredientId = ingredientId, ingredientName = "Sugar", sku = "SGR001", unit = "kg", category = "Dry Goods" };
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/inventory", initRequest);

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("receive").GetProperty("href").GetString()
            .Should().EndWith("/receive");
        json.RootElement.GetProperty("_links").GetProperty("consume").GetProperty("href").GetString()
            .Should().EndWith("/consume");
        json.RootElement.GetProperty("_links").GetProperty("adjust").GetProperty("href").GetString()
            .Should().EndWith("/adjust");
    }

    [Fact]
    public async Task GetInventory_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var nonExistentIngredientId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/sites/{siteId}/inventory/{nonExistentIngredientId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("error").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task ReceiveBatch_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var ingredientId = Guid.NewGuid();
        var initRequest = new { ingredientId = ingredientId, ingredientName = "Salt", sku = "SLT001", unit = "kg", category = "Dry Goods" };
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/inventory", initRequest);

        var receiveRequest = new
        {
            batchNumber = "BATCH001",
            quantity = 25m,
            unitCost = 2.50m,
            expiryDate = DateTime.UtcNow.AddMonths(12),
            supplierId = Guid.NewGuid(),
            location = "Shelf A1",
            notes = "Weekly delivery",
            receivedBy = Guid.NewGuid()
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}/receive", receiveRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("inventory").GetProperty("href").GetString()
            .Should().Contain($"/inventory/{ingredientId}");
    }

    [Fact]
    public async Task ConsumeStock_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var ingredientId = Guid.NewGuid();
        var initRequest = new { ingredientId = ingredientId, ingredientName = "Pepper", sku = "PPR001", unit = "kg", category = "Spices" };
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/inventory", initRequest);

        // Receive some stock first
        var receiveRequest = new { batchNumber = "BATCH002", quantity = 10m, unitCost = 5.00m };
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}/receive", receiveRequest);

        var consumeRequest = new { quantity = 2m, reason = "Order preparation", orderId = Guid.NewGuid(), performedBy = Guid.NewGuid() };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}/consume", consumeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("inventory").GetProperty("href").GetString()
            .Should().Contain($"/inventory/{ingredientId}");
    }

    [Fact]
    public async Task AdjustQuantity_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var ingredientId = Guid.NewGuid();
        var initRequest = new { ingredientId = ingredientId, ingredientName = "Oil", sku = "OIL001", unit = "L", category = "Liquids" };
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/inventory", initRequest);

        var adjustRequest = new { newQuantity = 100m, reason = "Physical count correction", adjustedBy = Guid.NewGuid() };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}/adjust", adjustRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("inventory").GetProperty("href").GetString()
            .Should().Contain($"/inventory/{ingredientId}");
    }

    [Fact]
    public async Task GetLevel_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var ingredientId = Guid.NewGuid();
        var initRequest = new { ingredientId = ingredientId, ingredientName = "Butter", sku = "BTR001", unit = "kg", category = "Dairy" };
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/inventory", initRequest);

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}/level");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("inventory").GetProperty("href").GetString()
            .Should().Contain($"/inventory/{ingredientId}");
    }
}
