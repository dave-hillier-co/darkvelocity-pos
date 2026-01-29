using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Booking.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Booking.Tests;

public class WaitlistControllerTests : IClassFixture<BookingApiFixture>
{
    private readonly BookingApiFixture _fixture;
    private readonly HttpClient _client;

    public WaitlistControllerTests(BookingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task Create_CreatesWaitlistEntry()
    {
        // Arrange
        var request = new CreateWaitlistEntryRequest(
            GuestName: "Walk-in Guest",
            PartySize: 2,
            RequestedDate: DateOnly.FromDateTime(DateTime.UtcNow),
            PreferredTime: new TimeOnly(19, 0),
            GuestPhone: "07700900001",
            Source: "walk_in"
        );

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/waitlist",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<WaitlistEntryDto>();
        result.Should().NotBeNull();
        result!.GuestName.Should().Be("Walk-in Guest");
        result.PartySize.Should().Be(2);
        result.Status.Should().Be("waiting");
        result.QueuePosition.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task OfferTable_SendsOffer()
    {
        // Arrange - Create waitlist entry
        var createRequest = new CreateWaitlistEntryRequest(
            GuestName: "Offer Test",
            PartySize: 2,
            RequestedDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Source: "walk_in"
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/waitlist",
            createRequest);
        var entry = await createResponse.Content.ReadFromJsonAsync<WaitlistEntryDto>();

        // Act
        var offerRequest = new OfferTableRequest(
            TableId: _fixture.TestTableId,
            OfferExpiryMinutes: 10
        );
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/waitlist/{entry!.Id}/offer",
            offerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<WaitlistEntryDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("notified");
        result.OfferedTableId.Should().Be(_fixture.TestTableId);
        result.OfferExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ConfirmOffer_ConfirmsEntry()
    {
        // Arrange - Create and offer
        var createRequest = new CreateWaitlistEntryRequest(
            GuestName: "Confirm Test",
            PartySize: 2,
            RequestedDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Source: "walk_in"
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/waitlist",
            createRequest);
        var entry = await createResponse.Content.ReadFromJsonAsync<WaitlistEntryDto>();

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/waitlist/{entry!.Id}/offer",
            new OfferTableRequest(TableId: _fixture.TestTable2Id));

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/waitlist/{entry.Id}/confirm",
            new {});

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<WaitlistEntryDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("confirmed");
        result.ConfirmedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ConvertToBooking_CreatesBooking()
    {
        // Arrange - Create, offer, and confirm
        var createRequest = new CreateWaitlistEntryRequest(
            GuestName: "Convert Test",
            PartySize: 2,
            RequestedDate: DateOnly.FromDateTime(DateTime.UtcNow),
            PreferredTime: new TimeOnly(20, 0),
            GuestPhone: "07700900002",
            Source: "walk_in"
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/waitlist",
            createRequest);
        var entry = await createResponse.Content.ReadFromJsonAsync<WaitlistEntryDto>();

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/waitlist/{entry!.Id}/offer",
            new OfferTableRequest(TableId: _fixture.TestTableId));

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/waitlist/{entry.Id}/confirm",
            new {});

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/waitlist/{entry.Id}/convert",
            new ConvertToBookingRequest(DurationMinutes: 90));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BookingDto>();
        result.Should().NotBeNull();
        result!.GuestName.Should().Be("Convert Test");
        result.Status.Should().Be("confirmed");
        result.Source.Should().Be("walk_in");
    }

    [Fact]
    public async Task Cancel_CancelsEntry()
    {
        // Arrange
        var createRequest = new CreateWaitlistEntryRequest(
            GuestName: "Cancel Test",
            PartySize: 2,
            RequestedDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Source: "walk_in"
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/waitlist",
            createRequest);
        var entry = await createResponse.Content.ReadFromJsonAsync<WaitlistEntryDto>();

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/waitlist/{entry!.Id}/cancel",
            new CancelWaitlistEntryRequest(Reason: "Guest left"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<WaitlistEntryDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("cancelled");
    }

    [Fact]
    public async Task GetSummary_ReturnsSummary()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/waitlist/summary");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<WaitlistSummaryDto>();
        result.Should().NotBeNull();
    }
}
