using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace DarkVelocity.Tests.Api;

[Collection(ApiCollection.Name)]
public class HealthApiTests
{
    private readonly HttpClient _client;

    public HealthApiTests(ApiTestFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOkWithHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("status").GetString().Should().Be("healthy");
    }
}
