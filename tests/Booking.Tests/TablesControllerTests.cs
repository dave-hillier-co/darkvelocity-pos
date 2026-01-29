using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Booking.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Booking.Tests;

public class TablesControllerTests : IClassFixture<BookingApiFixture>
{
    private readonly BookingApiFixture _fixture;
    private readonly HttpClient _client;

    public TablesControllerTests(BookingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsTables()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/tables");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<HalCollectionResponse<TableDto>>();
        result.Should().NotBeNull();
        result!.Embedded.Items.Should().NotBeEmpty();
        result.Embedded.Items.Should().Contain(t => t.TableNumber == "1");
        result.Embedded.Items.Should().Contain(t => t.TableNumber == "2");
    }

    [Fact]
    public async Task GetById_ReturnsTable()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/tables/{_fixture.TestTableId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TableDto>();
        result.Should().NotBeNull();
        result!.TableNumber.Should().Be("1");
        result.Name.Should().Be("Window Table");
        result.MinCapacity.Should().Be(2);
        result.MaxCapacity.Should().Be(4);
    }

    [Fact]
    public async Task Create_CreatesTable()
    {
        // Arrange
        var request = new CreateTableRequest(
            FloorPlanId: _fixture.TestFloorPlanId,
            TableNumber: "3",
            Name: "Bar Table",
            MinCapacity: 1,
            MaxCapacity: 2,
            Shape: "round",
            PositionX: 10,
            PositionY: 5
        );

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/tables",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<TableDto>();
        result.Should().NotBeNull();
        result!.TableNumber.Should().Be("3");
        result.Name.Should().Be("Bar Table");
        result.Shape.Should().Be("round");
        result.Status.Should().Be("available");
    }

    [Fact]
    public async Task UpdateStatus_UpdatesTableStatus()
    {
        // Arrange
        var request = new UpdateTableStatusRequest(Status: "closed");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/tables/{_fixture.TestTable2Id}/status",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TableDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("closed");
    }

    [Fact]
    public async Task Create_WithDuplicateNumber_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTableRequest(
            FloorPlanId: _fixture.TestFloorPlanId,
            TableNumber: "1", // Already exists
            MinCapacity: 1,
            MaxCapacity: 2
        );

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/tables",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAll_FiltersbyFloorPlan()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/tables?floorPlanId={_fixture.TestFloorPlanId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<HalCollectionResponse<TableDto>>();
        result.Should().NotBeNull();
        result!.Embedded.Items.All(t => t.FloorPlanId == _fixture.TestFloorPlanId).Should().BeTrue();
    }

    [Fact]
    public async Task Update_UpdatesTable()
    {
        // Arrange
        var request = new UpdateTableRequest(
            Name: "Updated Window Table",
            MaxCapacity: 6
        );

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/tables/{_fixture.TestTableId}",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TableDto>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Window Table");
        result.MaxCapacity.Should().Be(6);
    }
}
