using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace DarkVelocity.Tests.Api;

[Collection(ApiCollection.Name)]
public class PaymentApiTests
{
    private readonly HttpClient _client;

    public PaymentApiTests(ApiTestFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    private async Task<(Guid OrgId, Guid SiteId, Guid OrderId)> CreateOrgSiteAndOrderAsync()
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

        // Create order
        var orderRequest = new { createdBy = Guid.NewGuid(), type = 0, guestCount = 1 };
        var orderResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders", orderRequest);
        var orderContent = await orderResponse.Content.ReadAsStringAsync();
        var orderJson = JsonDocument.Parse(orderContent);
        var orderId = orderJson.RootElement.GetProperty("id").GetGuid();

        // Add a line to the order
        var lineRequest = new { menuItemId = Guid.NewGuid(), name = "Burger", quantity = 1, unitPrice = 25.00m };
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines", lineRequest);

        return (orgId, siteId, orderId);
    }

    [Fact]
    public async Task InitiatePayment_ReturnsCreatedWithHalResponse()
    {
        // Arrange
        var (orgId, siteId, orderId) = await CreateOrgSiteAndOrderAsync();
        var request = new
        {
            orderId = orderId,
            method = 0, // Cash
            amount = 25.00m,
            cashierId = Guid.NewGuid()
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/payments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().NotBeEmpty();
        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain("/payments/");
        json.RootElement.GetProperty("_links").GetProperty("complete-cash").GetProperty("href").GetString()
            .Should().EndWith("/complete-cash");
        json.RootElement.GetProperty("_links").GetProperty("complete-card").GetProperty("href").GetString()
            .Should().EndWith("/complete-card");
    }

    [Fact]
    public async Task GetPayment_WhenExists_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId, orderId) = await CreateOrgSiteAndOrderAsync();
        var createRequest = new { orderId = orderId, method = 0, amount = 25.00m, cashierId = Guid.NewGuid() };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/payments", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var paymentId = createJson.RootElement.GetProperty("id").GetGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().Be(paymentId);
        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain($"/payments/{paymentId}");
    }

    [Fact]
    public async Task GetPayment_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        var (orgId, siteId, _) = await CreateOrgSiteAndOrderAsync();
        var nonExistentPaymentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/sites/{siteId}/payments/{nonExistentPaymentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("error").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task CompleteCashPayment_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId, orderId) = await CreateOrgSiteAndOrderAsync();
        var createRequest = new { orderId = orderId, method = 0, amount = 25.00m, cashierId = Guid.NewGuid() };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/payments", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var paymentId = createJson.RootElement.GetProperty("id").GetGuid();

        var completeRequest = new { amountTendered = 30.00m, tipAmount = 5.00m };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}/complete-cash", completeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("payment").GetProperty("href").GetString()
            .Should().Contain($"/payments/{paymentId}");
    }

    [Fact]
    public async Task CompleteCardPayment_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId, orderId) = await CreateOrgSiteAndOrderAsync();
        var createRequest = new { orderId = orderId, method = 1, amount = 25.00m, cashierId = Guid.NewGuid() };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/payments", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var paymentId = createJson.RootElement.GetProperty("id").GetGuid();

        var completeRequest = new
        {
            gatewayReference = "ref_123456",
            authorizationCode = "AUTH123",
            cardInfo = new { lastFour = "1234", brand = "Visa", expiryMonth = 12, expiryYear = 2025 },
            gatewayName = "Stripe",
            tipAmount = 5.00m
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}/complete-card", completeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("payment").GetProperty("href").GetString()
            .Should().Contain($"/payments/{paymentId}");
    }

    [Fact]
    public async Task VoidPayment_ReturnsOk()
    {
        // Arrange
        var (orgId, siteId, orderId) = await CreateOrgSiteAndOrderAsync();
        var createRequest = new { orderId = orderId, method = 0, amount = 25.00m, cashierId = Guid.NewGuid() };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/payments", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var paymentId = createJson.RootElement.GetProperty("id").GetGuid();

        var voidRequest = new { voidedBy = Guid.NewGuid(), reason = "Customer request" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}/void", voidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("message").GetString().Should().Be("Payment voided");
    }

    [Fact]
    public async Task RefundPayment_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId, orderId) = await CreateOrgSiteAndOrderAsync();
        var createRequest = new { orderId = orderId, method = 0, amount = 25.00m, cashierId = Guid.NewGuid() };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/payments", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var paymentId = createJson.RootElement.GetProperty("id").GetGuid();

        // Complete payment first
        var completeRequest = new { amountTendered = 30.00m };
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}/complete-cash", completeRequest);

        var refundRequest = new { amount = 10.00m, reason = "Partial refund", issuedBy = Guid.NewGuid() };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}/refund", refundRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("payment").GetProperty("href").GetString()
            .Should().Contain($"/payments/{paymentId}");
    }
}
