using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace DarkVelocity.Tests.Api;

[Collection(ApiCollection.Name)]
public class SiteApiTests
{
    private readonly HttpClient _client;

    public SiteApiTests(ApiTestFixture fixture)
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
    public async Task CreateSite_ReturnsCreatedWithHalResponse()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var request = new
        {
            name = "Test Site",
            code = "TS01",
            address = new
            {
                street = "123 Main St",
                city = "New York",
                state = "NY",
                postalCode = "10001",
                country = "USA"
            },
            timezone = "America/New_York",
            currency = "USD"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().NotBeEmpty();
        json.RootElement.GetProperty("code").GetString().Should().Be("TS01");
        json.RootElement.GetProperty("name").GetString().Should().Be("Test Site");
        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain("/sites/");
        json.RootElement.GetProperty("_links").GetProperty("organization").GetProperty("href").GetString()
            .Should().Be($"/api/orgs/{orgId}");
    }

    [Fact]
    public async Task GetSite_WhenExists_ReturnsOkWithHalResponse()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var createRequest = new
        {
            name = "Get Test Site",
            code = "GTS01",
            address = new { street = "456 Oak Ave", city = "Boston", state = "MA", postalCode = "02101", country = "USA" }
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var siteId = createJson.RootElement.GetProperty("id").GetGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/sites/{siteId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().Be(siteId);
        json.RootElement.GetProperty("name").GetString().Should().Be("Get Test Site");
        json.RootElement.GetProperty("_links").GetProperty("orders").GetProperty("href").GetString()
            .Should().Contain("/orders");
    }

    [Fact]
    public async Task GetSite_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var nonExistentSiteId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/sites/{nonExistentSiteId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("error").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task ListSites_WhenOrganizationExists_ReturnsHalCollection()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();

        // Create two sites
        var site1Request = new
        {
            name = "Site One",
            code = "S01",
            address = new { street = "1 First St", city = "Chicago", state = "IL", postalCode = "60601", country = "USA" }
        };
        var site2Request = new
        {
            name = "Site Two",
            code = "S02",
            address = new { street = "2 Second St", city = "Chicago", state = "IL", postalCode = "60602", country = "USA" }
        };

        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites", site1Request);
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites", site2Request);

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/sites");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("count").GetInt32().Should().Be(2);
        json.RootElement.GetProperty("_embedded").GetProperty("items").GetArrayLength().Should().Be(2);
        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain("/sites");
    }

    [Fact]
    public async Task ListSites_WhenOrganizationNotExists_ReturnsNotFound()
    {
        // Arrange
        var nonExistentOrgId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{nonExistentOrgId}/sites");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateSite_WhenExists_ReturnsOkWithUpdatedData()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var createRequest = new
        {
            name = "Update Test Site",
            code = "UTS01",
            address = new { street = "789 Pine Rd", city = "Seattle", state = "WA", postalCode = "98101", country = "USA" }
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var siteId = createJson.RootElement.GetProperty("id").GetGuid();

        var updateRequest = new { name = "Updated Site Name" };

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("name").GetString().Should().Be("Updated Site Name");
    }

    [Fact]
    public async Task OpenSite_WhenExists_ReturnsOk()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var createRequest = new
        {
            name = "Open Test Site",
            code = "OTS01",
            address = new { street = "321 Elm St", city = "Denver", state = "CO", postalCode = "80201", country = "USA" }
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var siteId = createJson.RootElement.GetProperty("id").GetGuid();

        // Act
        var response = await _client.PostAsync($"/api/orgs/{orgId}/sites/{siteId}/open", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("message").GetString().Should().Be("Site opened");
    }

    [Fact]
    public async Task CloseSite_WhenExists_ReturnsOk()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var createRequest = new
        {
            name = "Close Test Site",
            code = "CTS01",
            address = new { street = "654 Maple Ave", city = "Austin", state = "TX", postalCode = "73301", country = "USA" }
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var siteId = createJson.RootElement.GetProperty("id").GetGuid();

        // Open site first
        await _client.PostAsync($"/api/orgs/{orgId}/sites/{siteId}/open", null);

        // Act
        var response = await _client.PostAsync($"/api/orgs/{orgId}/sites/{siteId}/close", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("message").GetString().Should().Be("Site closed");
    }
}
