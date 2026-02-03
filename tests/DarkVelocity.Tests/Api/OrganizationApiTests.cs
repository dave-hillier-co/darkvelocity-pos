using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace DarkVelocity.Tests.Api;

[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public class OrganizationApiTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public OrganizationApiTests(ApiTestFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task CreateOrganization_ReturnsCreatedWithHalResponse()
    {
        // Arrange
        var request = new { name = "Test Organization", slug = "test-org" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orgs", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().NotBeEmpty();
        json.RootElement.GetProperty("slug").GetString().Should().Be("test-org");
        json.RootElement.GetProperty("name").GetString().Should().Be("Test Organization");
        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().StartWith("/api/orgs/");
        json.RootElement.GetProperty("_links").GetProperty("sites").GetProperty("href").GetString()
            .Should().EndWith("/sites");
    }

    [Fact]
    public async Task CreateOrganization_WithSettings_ReturnsCreatedWithSettings()
    {
        // Arrange
        var request = new
        {
            name = "Settings Org",
            slug = "settings-org",
            settings = new { defaultCurrency = "EUR", defaultTimezone = "Europe/Paris" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orgs", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetOrganization_WhenExists_ReturnsOkWithHalResponse()
    {
        // Arrange
        var createRequest = new { name = "Get Test Org", slug = "get-test-org" };
        var createResponse = await _client.PostAsJsonAsync("/api/orgs", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var orgId = createJson.RootElement.GetProperty("id").GetGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().Be(orgId);
        json.RootElement.GetProperty("name").GetString().Should().Be("Get Test Org");
        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Be($"/api/orgs/{orgId}");
    }

    [Fact]
    public async Task GetOrganization_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("error").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task UpdateOrganization_WhenExists_ReturnsOkWithUpdatedData()
    {
        // Arrange
        var createRequest = new { name = "Update Test Org", slug = "update-test-org" };
        var createResponse = await _client.PostAsJsonAsync("/api/orgs", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var orgId = createJson.RootElement.GetProperty("id").GetGuid();

        var updateRequest = new { name = "Updated Org Name" };

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/orgs/{orgId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("name").GetString().Should().Be("Updated Org Name");
    }

    [Fact]
    public async Task UpdateOrganization_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateRequest = new { name = "Updated Name" };

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/orgs/{nonExistentId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SuspendOrganization_WhenExists_ReturnsOk()
    {
        // Arrange
        var createRequest = new { name = "Suspend Test Org", slug = "suspend-test-org" };
        var createResponse = await _client.PostAsJsonAsync("/api/orgs", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var orgId = createJson.RootElement.GetProperty("id").GetGuid();

        var suspendRequest = new { reason = "Non-payment" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/suspend", suspendRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("message").GetString().Should().Be("Organization suspended");
    }

    [Fact]
    public async Task SuspendOrganization_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var suspendRequest = new { reason = "Test reason" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{nonExistentId}/suspend", suspendRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
