using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace DarkVelocity.Tests.Api;

[Collection(ApiCollection.Name)]
public class BookingApiTests
{
    private readonly HttpClient _client;

    public BookingApiTests(ApiTestFixture fixture)
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
    public async Task RequestBooking_ReturnsCreatedWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var request = new
        {
            guest = new
            {
                name = "John Smith",
                email = "john.smith@example.com",
                phone = "+1-555-123-4567"
            },
            requestedTime = DateTime.UtcNow.AddDays(1).AddHours(18), // Tomorrow at 6 PM
            partySize = 4,
            duration = "01:30:00", // 1.5 hours
            specialRequests = "Window seat please",
            occasion = "Birthday",
            source = 0 // Direct
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().NotBeEmpty();
        json.RootElement.GetProperty("confirmationCode").GetString().Should().NotBeNullOrEmpty();
        json.RootElement.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()
            .Should().Contain("/bookings/");
        json.RootElement.GetProperty("_links").GetProperty("confirm").GetProperty("href").GetString()
            .Should().EndWith("/confirm");
        json.RootElement.GetProperty("_links").GetProperty("cancel").GetProperty("href").GetString()
            .Should().EndWith("/cancel");
    }

    [Fact]
    public async Task GetBooking_WhenExists_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createRequest = new
        {
            guest = new { name = "Jane Doe", email = "jane@example.com", phone = "+1-555-999-8888" },
            requestedTime = DateTime.UtcNow.AddDays(2).AddHours(19),
            partySize = 2
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/bookings", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var bookingId = createJson.RootElement.GetProperty("id").GetGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetGuid().Should().Be(bookingId);
        json.RootElement.GetProperty("_links").GetProperty("confirm").GetProperty("href").GetString()
            .Should().EndWith("/confirm");
        json.RootElement.GetProperty("_links").GetProperty("checkin").GetProperty("href").GetString()
            .Should().EndWith("/checkin");
    }

    [Fact]
    public async Task GetBooking_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var nonExistentBookingId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/orgs/{orgId}/sites/{siteId}/bookings/{nonExistentBookingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("error").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task ConfirmBooking_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createRequest = new
        {
            guest = new { name = "Bob Wilson", email = "bob@example.com", phone = "+1-555-111-2222" },
            requestedTime = DateTime.UtcNow.AddDays(3).AddHours(20),
            partySize = 6
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/bookings", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var bookingId = createJson.RootElement.GetProperty("id").GetGuid();

        var confirmRequest = new { confirmedTime = DateTime.UtcNow.AddDays(3).AddHours(20) };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/confirm", confirmRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("booking").GetProperty("href").GetString()
            .Should().Contain($"/bookings/{bookingId}");
    }

    [Fact]
    public async Task CancelBooking_ReturnsOk()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createRequest = new
        {
            guest = new { name = "Cancel Test", email = "cancel@example.com", phone = "+1-555-333-4444" },
            requestedTime = DateTime.UtcNow.AddDays(4).AddHours(18),
            partySize = 3
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/bookings", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var bookingId = createJson.RootElement.GetProperty("id").GetGuid();

        var cancelRequest = new { reason = "Customer request", cancelledBy = Guid.NewGuid() };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/cancel", cancelRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("message").GetString().Should().Be("Booking cancelled");
    }

    [Fact]
    public async Task CheckInGuest_ReturnsOkWithHalResponse()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createRequest = new
        {
            guest = new { name = "Checkin Guest", email = "checkin@example.com", phone = "+1-555-555-6666" },
            requestedTime = DateTime.UtcNow.AddMinutes(30),
            partySize = 2
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/bookings", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var bookingId = createJson.RootElement.GetProperty("id").GetGuid();

        // Confirm first
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/confirm", new { });

        var checkinRequest = new { checkedInBy = Guid.NewGuid() };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/checkin", checkinRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("_links").GetProperty("booking").GetProperty("href").GetString()
            .Should().Contain($"/bookings/{bookingId}");
        json.RootElement.GetProperty("_links").GetProperty("seat").GetProperty("href").GetString()
            .Should().EndWith("/seat");
    }

    [Fact]
    public async Task SeatGuest_ReturnsOk()
    {
        // Arrange
        var (orgId, siteId) = await CreateOrgAndSiteAsync();
        var createRequest = new
        {
            guest = new { name = "Seat Guest", email = "seat@example.com", phone = "+1-555-777-8888" },
            requestedTime = DateTime.UtcNow.AddMinutes(15),
            partySize = 4
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/bookings", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var bookingId = createJson.RootElement.GetProperty("id").GetGuid();

        // Confirm and check in
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/confirm", new { });
        await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/checkin", new { checkedInBy = Guid.NewGuid() });

        var seatRequest = new { tableId = Guid.NewGuid(), tableNumber = "T05", seatedBy = Guid.NewGuid() };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/seat", seatRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("message").GetString().Should().Be("Guest seated");
    }
}
