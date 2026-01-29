using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Booking.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Booking.Tests;

public class BookingsControllerTests : IClassFixture<BookingApiFixture>
{
    private readonly BookingApiFixture _fixture;
    private readonly HttpClient _client;

    public BookingsControllerTests(BookingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task Create_CreatesBooking()
    {
        // Arrange
        var request = new CreateBookingRequest(
            GuestName: "John Smith",
            PartySize: 4,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            StartTime: new TimeOnly(19, 0),
            TableId: _fixture.TestTableId,
            GuestEmail: "john@example.com",
            GuestPhone: "07700900000"
        );

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<BookingDto>();
        result.Should().NotBeNull();
        result!.GuestName.Should().Be("John Smith");
        result.PartySize.Should().Be(4);
        result.BookingReference.Should().StartWith("BK-");
        result.Status.Should().Be("pending");
    }

    [Fact]
    public async Task Create_WithWebSource_AutoConfirms()
    {
        // Arrange
        var request = new CreateBookingRequest(
            GuestName: "Jane Doe",
            PartySize: 2,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            StartTime: new TimeOnly(12, 30),
            TableId: _fixture.TestTableId,
            GuestEmail: "jane@example.com",
            Source: "web"
        );

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<BookingDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("confirmed");
        result.IsConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task Confirm_ConfirmsBooking()
    {
        // Arrange - Create a pending booking
        var createRequest = new CreateBookingRequest(
            GuestName: "Confirm Test",
            PartySize: 2,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            StartTime: new TimeOnly(19, 30),
            Source: "phone"
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings",
            createRequest);
        var booking = await createResponse.Content.ReadFromJsonAsync<BookingDto>();

        // Act
        var confirmRequest = new ConfirmBookingRequest(ConfirmationMethod: "email");
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking!.Id}/confirm",
            confirmRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BookingDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("confirmed");
        result.IsConfirmed.Should().BeTrue();
        result.ConfirmationMethod.Should().Be("email");
    }

    [Fact]
    public async Task Seat_SeatsBooking()
    {
        // Arrange - Create and confirm booking
        var createRequest = new CreateBookingRequest(
            GuestName: "Seat Test",
            PartySize: 2,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)),
            StartTime: new TimeOnly(13, 0),
            TableId: _fixture.TestTable2Id,
            Source: "phone"
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings",
            createRequest);
        var booking = await createResponse.Content.ReadFromJsonAsync<BookingDto>();

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking!.Id}/confirm",
            new ConfirmBookingRequest());

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking.Id}/seat",
            new SeatBookingRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BookingDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("seated");
        result.SeatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Complete_CompletesBooking()
    {
        // Arrange - Create, confirm, and seat booking
        var createRequest = new CreateBookingRequest(
            GuestName: "Complete Test",
            PartySize: 2,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            StartTime: new TimeOnly(20, 0),
            Source: "phone"
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings",
            createRequest);
        var booking = await createResponse.Content.ReadFromJsonAsync<BookingDto>();

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking!.Id}/confirm",
            new ConfirmBookingRequest());

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking.Id}/seat",
            new SeatBookingRequest());

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking.Id}/complete",
            new CompleteBookingRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BookingDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("completed");
        result.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Cancel_CancelsBooking()
    {
        // Arrange - Create booking
        var createRequest = new CreateBookingRequest(
            GuestName: "Cancel Test",
            PartySize: 2,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(6)),
            StartTime: new TimeOnly(19, 0),
            Source: "phone"
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings",
            createRequest);
        var booking = await createResponse.Content.ReadFromJsonAsync<BookingDto>();

        // Act
        var cancelRequest = new CancelBookingRequest(Reason: "Guest cancelled");
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking!.Id}/cancel",
            cancelRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BookingDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("cancelled");
        result.CancellationReason.Should().Be("Guest cancelled");
    }

    [Fact]
    public async Task MarkNoShow_MarksAsNoShow()
    {
        // Arrange - Create and confirm booking
        var createRequest = new CreateBookingRequest(
            GuestName: "NoShow Test",
            PartySize: 2,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            StartTime: new TimeOnly(18, 0),
            Source: "phone"
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings",
            createRequest);
        var booking = await createResponse.Content.ReadFromJsonAsync<BookingDto>();

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking!.Id}/confirm",
            new ConfirmBookingRequest());

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking.Id}/no-show",
            new MarkNoShowRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BookingDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("no_show");
        result.MarkedNoShowAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByReference_ReturnsBooking()
    {
        // Arrange - Create booking
        var createRequest = new CreateBookingRequest(
            GuestName: "Reference Test",
            PartySize: 2,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8)),
            StartTime: new TimeOnly(19, 0)
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings",
            createRequest);
        var booking = await createResponse.Content.ReadFromJsonAsync<BookingDto>();

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/reference/{booking!.BookingReference}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BookingDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(booking.Id);
    }

    [Fact]
    public async Task Update_UpdatesBooking()
    {
        // Arrange - Create booking
        var createRequest = new CreateBookingRequest(
            GuestName: "Update Test",
            PartySize: 2,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(9)),
            StartTime: new TimeOnly(19, 0)
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings",
            createRequest);
        var booking = await createResponse.Content.ReadFromJsonAsync<BookingDto>();

        // Act
        var updateRequest = new UpdateBookingRequest(
            PartySize: 4,
            SpecialRequests: "Birthday celebration"
        );
        var response = await _client.PatchAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking!.Id}",
            updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BookingDto>();
        result.Should().NotBeNull();
        result!.PartySize.Should().Be(4);
        result.SpecialRequests.Should().Be("Birthday celebration");
    }

    [Fact]
    public async Task GetAll_FiltersbyDate()
    {
        // Arrange
        var testDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

        var createRequest = new CreateBookingRequest(
            GuestName: "Filter Test",
            PartySize: 2,
            BookingDate: testDate,
            StartTime: new TimeOnly(19, 0)
        );

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings",
            createRequest);

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings?date={testDate:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<HalCollectionResponse<BookingSummaryDto>>();
        result.Should().NotBeNull();
        result!.Embedded.Items.Should().Contain(b => b.GuestName == "Filter Test");
    }
}
