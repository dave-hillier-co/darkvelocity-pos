using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace DarkVelocity.Tests.Api;

[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public class EmployeeApiTests
{
    private readonly HttpClient _client;

    public EmployeeApiTests(ApiTestFixture fixture)
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

    // Given: an organization with a site
    // When: a new employee is hired with an employee number and assigned to the site
    // Then: the employee record is created with a HAL link to clock in
    [Fact]
    public async Task CreateEmployee_ReturnsCreatedWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var request = new
        {
            userId = Guid.NewGuid(),
            defaultSiteId = siteId,
            employeeNumber = "EMP001",
            firstName = "John",
            lastName = "Doe",
            email = "john.doe@company.com",
            employmentType = 0, // FullTime
            hireDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/employees", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().NotBeEmpty();
        json.RootElement.GetProperty("employeeNumber").GetString().Should().Be("EMP001");
        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain("/employees/");
        json.RootElement.GetProperty("_links").GetProperty("clock-in").GetProperty("href").GetString()
            .Should().EndWith("/clock-in");
    }

    // Given: an employee who has been added to the organization
    // When: the employee record is retrieved by identifier
    // Then: the employee details are returned with HAL links for clock-in and clock-out
    [Fact]
    public async Task GetEmployee_WhenExists_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createRequest = new
        {
            userId = Guid.NewGuid(),
            defaultSiteId = siteId,
            employeeNumber = "EMP002",
            firstName = "Jane",
            lastName = "Smith",
            email = "jane.smith@company.com"
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/employees", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var employeeId = createJson.RootElement.GetProperty("id").GetGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/employees/{employeeId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().Be(employeeId);
        json.RootElement.GetProperty("_links").GetProperty("clock-in").GetProperty("href").GetString()
            .Should().EndWith("/clock-in");
        json.RootElement.GetProperty("_links").GetProperty("clock-out").GetProperty("href").GetString()
            .Should().EndWith("/clock-out");
    }

    // Given: an organization with no matching employee
    // When: a non-existent employee identifier is looked up
    // Then: a not-found error is returned
    [Fact]
    public async Task GetEmployee_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        var (orgId, _) = await CreateOrgAndSiteAsync();
        var nonExistentEmployeeId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/employees/{nonExistentEmployeeId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("error").GetString().Should().Be("not_found");
    }

    // Given: an existing employee record
    // When: the employee's name and hourly rate are updated
    // Then: the updated record is returned with a self HAL link
    [Fact]
    public async Task UpdateEmployee_ReturnsOkWithUpdatedData()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createRequest = new
        {
            userId = Guid.NewGuid(),
            defaultSiteId = siteId,
            employeeNumber = "EMP003",
            firstName = "Bob",
            lastName = "Wilson",
            email = "bob.wilson@company.com"
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/employees", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var employeeId = createJson.RootElement.GetProperty("id").GetGuid();

        var updateRequest = new { firstName = "Robert", hourlyRate = 25.00m };

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/orgs/{orgId}/employees/{employeeId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain($"/employees/{employeeId}");
    }

    // Given: an employee who is not currently clocked in
    // When: the employee clocks in at their assigned site
    // Then: the time entry begins and a HAL link to clock out is provided
    [Fact]
    public async Task ClockIn_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createRequest = new
        {
            userId = Guid.NewGuid(),
            defaultSiteId = siteId,
            employeeNumber = "EMP004",
            firstName = "Alice",
            lastName = "Brown",
            email = "alice.brown@company.com"
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/employees", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var employeeId = createJson.RootElement.GetProperty("id").GetGuid();

        var clockInRequest = new { siteId = siteId };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/employees/{employeeId}/clock-in", clockInRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("employee").GetProperty("href").GetString()
            .Should().Contain($"/employees/{employeeId}");
        json.RootElement.GetProperty("_links").GetProperty("clock-out").GetProperty("href").GetString()
            .Should().EndWith("/clock-out");
    }

    // Given: an employee who is currently clocked in
    // When: the employee clocks out at end of shift
    // Then: the time entry is completed and a HAL link to the employee is returned
    [Fact]
    public async Task ClockOut_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createRequest = new
        {
            userId = Guid.NewGuid(),
            defaultSiteId = siteId,
            employeeNumber = "EMP005",
            firstName = "Charlie",
            lastName = "Davis",
            email = "charlie.davis@company.com"
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/employees", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var employeeId = createJson.RootElement.GetProperty("id").GetGuid();

        // Clock in first
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/employees/{employeeId}/clock-in", new { siteId = siteId });

        var clockOutRequest = new { notes = "End of shift" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/employees/{employeeId}/clock-out", clockOutRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("employee").GetProperty("href").GetString()
            .Should().Contain($"/employees/{employeeId}");
    }

    // Given: an employee without a specific role assignment
    // When: the employee is assigned a role with department and hourly rate override
    // Then: the role assignment is confirmed
    [Fact]
    public async Task AssignRole_ReturnsOk()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createRequest = new
        {
            userId = Guid.NewGuid(),
            defaultSiteId = siteId,
            employeeNumber = "EMP006",
            firstName = "Diana",
            lastName = "Evans",
            email = "diana.evans@company.com"
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/employees", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var employeeId = createJson.RootElement.GetProperty("id").GetGuid();

        var roleRequest = new
        {
            roleId = Guid.NewGuid(),
            roleName = "Shift Manager",
            department = "Operations",
            isPrimary = true,
            hourlyRateOverride = 22.00m
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/employees/{employeeId}/roles", roleRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("message").GetString().Should().Be("Role assigned");
    }

    // Given: an employee who has been assigned a role
    // When: the role is removed from the employee
    // Then: the role is unassigned successfully
    [Fact]
    public async Task RemoveRole_ReturnsNoContent()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createRequest = new
        {
            userId = Guid.NewGuid(),
            defaultSiteId = siteId,
            employeeNumber = "EMP007",
            firstName = "Edward",
            lastName = "Foster",
            email = "edward.foster@company.com"
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/employees", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var employeeId = createJson.RootElement.GetProperty("id").GetGuid();

        // Assign role first
        var roleId = Guid.NewGuid();
        var roleRequest = new { roleId = roleId, roleName = "Cashier", department = "Front of House" };
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/employees/{employeeId}/roles", roleRequest);

        // Act
        var response = await _client.DeleteAsync($"/api/orgs/{orgId}/employees/{employeeId}/roles/{roleId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
