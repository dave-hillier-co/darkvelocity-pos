using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace DarkVelocity.Tests.Api;

[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public class CustomerApiTests
{
    private readonly HttpClient _client;

    public CustomerApiTests(ApiTestFixture fixture)
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

    // Given: an organization that manages customer profiles
    // When: a new customer is registered with contact details
    // Then: the customer profile is created with a display name and HAL links to loyalty
    [Fact]
    public async Task CreateCustomer_ReturnsCreatedWithHalResponse()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var request = new
        {
            firstName = "John",
            lastName = "Doe",
            email = "john.doe@example.com",
            phone = "+1-555-123-4567",
            source = 0 // Direct
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/customers", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().NotBeEmpty();
        json.RootElement.GetProperty("displayName").GetString().Should().NotBeNullOrEmpty();
        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain("/customers/");
        json.RootElement.GetProperty("_links").GetProperty("loyalty").GetProperty("href").GetString()
            .Should().EndWith("/loyalty");
    }

    // Given: a customer who has been registered in the organization
    // When: the customer profile is retrieved by identifier
    // Then: the customer details are returned with HAL links to loyalty and rewards
    [Fact]
    public async Task GetCustomer_WhenExists_ReturnsOkWithHalResponse()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var createRequest = new { firstName = "Jane", lastName = "Smith", email = "jane@example.com" };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/customers", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var customerId = createJson.RootElement.GetProperty("id").GetGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/customers/{customerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().Be(customerId);
        json.RootElement.GetProperty("_links").GetProperty("loyalty").GetProperty("href").GetString()
            .Should().Contain("/loyalty");
        json.RootElement.GetProperty("_links").GetProperty("rewards").GetProperty("href").GetString()
            .Should().Contain("/rewards");
    }

    // Given: an organization with no matching customer
    // When: a non-existent customer identifier is looked up
    // Then: a not-found error is returned
    [Fact]
    public async Task GetCustomer_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var nonExistentCustomerId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/customers/{nonExistentCustomerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("error").GetString().Should().Be("not_found");
    }

    // Given: an existing customer profile
    // When: the customer's name and phone number are updated
    // Then: the updated profile is returned with a self HAL link
    [Fact]
    public async Task UpdateCustomer_ReturnsOkWithUpdatedData()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var createRequest = new { firstName = "Bob", lastName = "Wilson", email = "bob@example.com" };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/customers", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var customerId = createJson.RootElement.GetProperty("id").GetGuid();

        var updateRequest = new { firstName = "Robert", phone = "+1-555-999-8888" };

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/orgs/{orgId}/customers/{customerId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain($"/customers/{customerId}");
    }

    // Given: a registered customer who is not yet in a loyalty program
    // When: the customer is enrolled in a loyalty program with a member number and tier
    // Then: the enrollment is confirmed
    [Fact]
    public async Task EnrollInLoyalty_ReturnsOk()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var createRequest = new { firstName = "Loyal", lastName = "Customer", email = "loyal@example.com" };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/customers", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var customerId = createJson.RootElement.GetProperty("id").GetGuid();

        var enrollRequest = new
        {
            programId = Guid.NewGuid(),
            memberNumber = "LYL001",
            initialTierId = Guid.NewGuid(),
            tierName = "Bronze"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/customers/{customerId}/loyalty/enroll", enrollRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("message").GetString().Should().Be("Enrolled in loyalty program");
    }

    // Given: a customer enrolled in a loyalty program
    // When: the customer earns loyalty points from a purchase
    // Then: the points are awarded and a HAL link back to the customer is returned
    [Fact]
    public async Task EarnPoints_ReturnsOkWithHalResponse()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var createRequest = new { firstName = "Points", lastName = "Earner", email = "points@example.com" };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/customers", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var customerId = createJson.RootElement.GetProperty("id").GetGuid();

        // Enroll first
        var enrollRequest = new { programId = Guid.NewGuid(), memberNumber = "LYL002", initialTierId = Guid.NewGuid(), tierName = "Silver" };
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/customers/{customerId}/loyalty/enroll", enrollRequest);

        var earnRequest = new { points = 100, reason = "Purchase", spendAmount = 50.00m };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/customers/{customerId}/loyalty/earn", earnRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("customer").GetProperty("href").GetString()
            .Should().Contain($"/customers/{customerId}");
    }

    // Given: a loyalty member with a sufficient points balance
    // When: the customer redeems points against an order
    // Then: the points are deducted and a HAL link back to the customer is returned
    [Fact]
    public async Task RedeemPoints_ReturnsOkWithHalResponse()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var createRequest = new { firstName = "Redeemer", lastName = "Customer", email = "redeem@example.com" };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/customers", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var customerId = createJson.RootElement.GetProperty("id").GetGuid();

        // Enroll and earn points
        var enrollRequest = new { programId = Guid.NewGuid(), memberNumber = "LYL003", initialTierId = Guid.NewGuid(), tierName = "Gold" };
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/customers/{customerId}/loyalty/enroll", enrollRequest);

        var earnRequest = new { points = 500, reason = "Purchase" };
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/customers/{customerId}/loyalty/earn", earnRequest);

        var redeemRequest = new { points = 100, orderId = Guid.NewGuid(), reason = "Discount redemption" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/customers/{customerId}/loyalty/redeem", redeemRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("customer").GetProperty("href").GetString()
            .Should().Contain($"/customers/{customerId}");
    }

    // Given: a registered customer
    // When: the customer's rewards are retrieved
    // Then: a HAL collection of rewards is returned with a count
    [Fact]
    public async Task GetRewards_ReturnsHalCollection()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var createRequest = new { firstName = "Rewards", lastName = "Customer", email = "rewards@example.com" };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/customers", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var customerId = createJson.RootElement.GetProperty("id").GetGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/customers/{customerId}/rewards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain("/rewards");
        json.RootElement.GetProperty("_embedded").GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        json.RootElement.GetProperty("count").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }
}
