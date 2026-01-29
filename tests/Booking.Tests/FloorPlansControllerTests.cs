using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Booking.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Booking.Tests;

public class FloorPlansControllerTests : IClassFixture<BookingApiFixture>
{
    private readonly BookingApiFixture _fixture;
    private readonly HttpClient _client;

    public FloorPlansControllerTests(BookingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsFloorPlans()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/floor-plans");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<HalCollectionResponse<FloorPlanDto>>();
        result.Should().NotBeNull();
        result!.Embedded.Items.Should().NotBeEmpty();
        result.Embedded.Items.Should().Contain(fp => fp.Name == "Main Dining");
    }

    [Fact]
    public async Task GetById_ReturnsFloorPlan()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/floor-plans/{_fixture.TestFloorPlanId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<FloorPlanDto>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("Main Dining");
        result.Tables.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_CreatesFloorPlan()
    {
        // Arrange
        var request = new CreateFloorPlanRequest(
            Name: "Patio",
            Description: "Outdoor seating",
            GridWidth: 15,
            GridHeight: 10
        );

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/floor-plans",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<FloorPlanDto>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("Patio");
        result.Description.Should().Be("Outdoor seating");
        result.GridWidth.Should().Be(15);
        result.GridHeight.Should().Be(10);
    }

    [Fact]
    public async Task Update_UpdatesFloorPlan()
    {
        // Arrange
        var request = new UpdateFloorPlanRequest(
            Description: "Updated description"
        );

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/floor-plans/{_fixture.TestFloorPlanId}",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<FloorPlanDto>();
        result.Should().NotBeNull();
        result!.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task GetById_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/floor-plans/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// Helper class for deserializing HAL collections
public class HalCollectionResponse<T>
{
    public HalEmbeddedItems<T> Embedded { get; set; } = new();
    public int Count { get; set; }
    public int? Total { get; set; }
}

public class HalEmbeddedItems<T>
{
    public List<T> Items { get; set; } = new();
}
