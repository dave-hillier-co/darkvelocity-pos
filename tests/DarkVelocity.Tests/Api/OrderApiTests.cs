using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace DarkVelocity.Tests.Api;

[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public class OrderApiTests
{
    private readonly HttpClient _client;

    public OrderApiTests(ApiTestFixture fixture)
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
    public async Task CreateOrder_ReturnsCreatedWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var request = new
        {
            createdBy = Guid.NewGuid(),
            type = 0, // DineIn
            tableNumber = "T01",
            guestCount = 2
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().NotBeEmpty();
        json.RootElement.GetProperty("orderNumber").GetString().Should().NotBeNullOrEmpty();
        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain("/orders/");
        json.RootElement.GetProperty("_links").GetProperty("lines").GetProperty("href").GetString()
            .Should().EndWith("/lines");
    }

    [Fact]
    public async Task GetOrder_WhenExists_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createRequest = new { createdBy = Guid.NewGuid(), type = 0, guestCount = 1 };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var orderId = createJson.RootElement.GetProperty("id").GetGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().Be(orderId);
        json.RootElement.GetProperty("_links").GetProperty("send").GetProperty("href").GetString()
            .Should().EndWith("/send");
        json.RootElement.GetProperty("_links").GetProperty("close").GetProperty("href").GetString()
            .Should().EndWith("/close");
    }

    [Fact]
    public async Task GetOrder_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var nonExistentOrderId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/sites/{siteId}/orders/{nonExistentOrderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("error").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task AddLineToOrder_ReturnsCreatedWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createOrderRequest = new { createdBy = Guid.NewGuid(), type = 0, guestCount = 1 };
        var createOrderResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders", createOrderRequest);
        var createOrderContent = await createOrderResponse.Content.ReadAsStringAsync();
        var createOrderJson = JsonDocument.Parse(createOrderContent);
        var orderId = createOrderJson.RootElement.GetProperty("id").GetGuid();

        var lineRequest = new
        {
            menuItemId = Guid.NewGuid(),
            name = "Burger",
            quantity = 2,
            unitPrice = 12.99m,
            notes = "No onions"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines", lineRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("lineId").GetGuid().Should().NotBeEmpty();
        json.RootElement.GetProperty("_links").GetProperty("order").GetProperty("href").GetString()
            .Should().Contain($"/orders/{orderId}");
    }

    [Fact]
    public async Task GetOrderLines_ReturnsHalCollection()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createOrderRequest = new { createdBy = Guid.NewGuid(), type = 0, guestCount = 1 };
        var createOrderResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders", createOrderRequest);
        var createOrderContent = await createOrderResponse.Content.ReadAsStringAsync();
        var createOrderJson = JsonDocument.Parse(createOrderContent);
        var orderId = createOrderJson.RootElement.GetProperty("id").GetGuid();

        // Add two lines
        var line1 = new { menuItemId = Guid.NewGuid(), name = "Burger", quantity = 1, unitPrice = 12.99m };
        var line2 = new { menuItemId = Guid.NewGuid(), name = "Fries", quantity = 1, unitPrice = 4.99m };

        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines", line1);
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines", line2);

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("count").GetInt32().Should().Be(2);
        json.RootElement.GetProperty("_embedded").GetProperty("items").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task RemoveLineFromOrder_ReturnsNoContent()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createOrderRequest = new { createdBy = Guid.NewGuid(), type = 0, guestCount = 1 };
        var createOrderResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders", createOrderRequest);
        var createOrderContent = await createOrderResponse.Content.ReadAsStringAsync();
        var createOrderJson = JsonDocument.Parse(createOrderContent);
        var orderId = createOrderJson.RootElement.GetProperty("id").GetGuid();

        var lineRequest = new { menuItemId = Guid.NewGuid(), name = "Burger", quantity = 1, unitPrice = 12.99m };
        var lineResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines", lineRequest);
        var lineContent = await lineResponse.Content.ReadAsStringAsync();
        var lineJson = JsonDocument.Parse(lineContent);
        var lineId = lineJson.RootElement.GetProperty("lineId").GetGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines/{lineId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SendOrder_ReturnsOkWithStatus()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createOrderRequest = new { createdBy = Guid.NewGuid(), type = 0, guestCount = 1 };
        var createOrderResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders", createOrderRequest);
        var createOrderContent = await createOrderResponse.Content.ReadAsStringAsync();
        var createOrderJson = JsonDocument.Parse(createOrderContent);
        var orderId = createOrderJson.RootElement.GetProperty("id").GetGuid();

        // Add a line item before sending (orders need at least one item to send)
        var lineRequest = new { menuItemId = Guid.NewGuid(), name = "Test Item", quantity = 1, unitPrice = 10.00m };
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines", lineRequest);

        var sendRequest = new { sentBy = Guid.NewGuid() };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/send", sendRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("status").ValueKind.Should().NotBe(JsonValueKind.Undefined);
        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain($"/orders/{orderId}");
    }

    [Fact]
    public async Task CloseOrder_ReturnsOk()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createOrderRequest = new { createdBy = Guid.NewGuid(), type = 0, guestCount = 1 };
        var createOrderResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders", createOrderRequest);
        var createOrderContent = await createOrderResponse.Content.ReadAsStringAsync();
        var createOrderJson = JsonDocument.Parse(createOrderContent);
        var orderId = createOrderJson.RootElement.GetProperty("id").GetGuid();

        var closeRequest = new { closedBy = Guid.NewGuid() };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/close", closeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("message").GetString().Should().Be("Order closed");
    }

    [Fact]
    public async Task VoidOrder_ReturnsOk()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createOrderRequest = new { createdBy = Guid.NewGuid(), type = 0, guestCount = 1 };
        var createOrderResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders", createOrderRequest);
        var createOrderContent = await createOrderResponse.Content.ReadAsStringAsync();
        var createOrderJson = JsonDocument.Parse(createOrderContent);
        var orderId = createOrderJson.RootElement.GetProperty("id").GetGuid();

        var voidRequest = new { voidedBy = Guid.NewGuid(), reason = "Customer changed mind" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/void", voidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("message").GetString().Should().Be("Order voided");
    }

    [Fact]
    public async Task ApplyDiscount_ReturnsOkWithTotals()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createOrderRequest = new { createdBy = Guid.NewGuid(), type = 0, guestCount = 1 };
        var createOrderResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders", createOrderRequest);
        var createOrderContent = await createOrderResponse.Content.ReadAsStringAsync();
        var createOrderJson = JsonDocument.Parse(createOrderContent);
        var orderId = createOrderJson.RootElement.GetProperty("id").GetGuid();

        // Add a line first
        var lineRequest = new { menuItemId = Guid.NewGuid(), name = "Burger", quantity = 1, unitPrice = 20.00m };
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines", lineRequest);

        var discountRequest = new
        {
            name = "Happy Hour",
            type = 0, // Percentage
            value = 10m,
            appliedBy = Guid.NewGuid()
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/discounts", discountRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("order").GetProperty("href").GetString()
            .Should().Contain($"/orders/{orderId}");
    }

    [Fact]
    public async Task GetOrderTotals_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createOrderRequest = new { createdBy = Guid.NewGuid(), type = 0, guestCount = 1 };
        var createOrderResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders", createOrderRequest);
        var createOrderContent = await createOrderResponse.Content.ReadAsStringAsync();
        var createOrderJson = JsonDocument.Parse(createOrderContent);
        var orderId = createOrderJson.RootElement.GetProperty("id").GetGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/totals");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("order").GetProperty("href").GetString()
            .Should().Contain($"/orders/{orderId}");
    }
}
