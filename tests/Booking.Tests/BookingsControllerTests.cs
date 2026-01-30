using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Booking.Api.Dtos;
using DarkVelocity.Shared.Contracts.Events;
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

    // Event Publishing Tests

    [Fact]
    public async Task Create_PublishesBookingCreatedEvent()
    {
        _fixture.ClearEventLog();

        var request = new CreateBookingRequest(
            GuestName: "Event Test Guest",
            PartySize: 4,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
            StartTime: new TimeOnly(19, 0),
            TableId: _fixture.TestTableId,
            Source: "phone"
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>();

        var events = _fixture.GetEventBus().GetEventLog();
        var bookingCreatedEvent = events.OfType<BookingCreated>().FirstOrDefault(e => e.BookingId == booking!.Id);

        bookingCreatedEvent.Should().NotBeNull();
        bookingCreatedEvent!.LocationId.Should().Be(_fixture.TestLocationId);
        bookingCreatedEvent.GuestName.Should().Be("Event Test Guest");
        bookingCreatedEvent.PartySize.Should().Be(4);
        bookingCreatedEvent.TableId.Should().Be(_fixture.TestTableId);
        bookingCreatedEvent.Source.Should().Be("phone");
    }

    [Fact]
    public async Task Create_WithWebSource_PublishesBothCreatedAndConfirmedEvents()
    {
        _fixture.ClearEventLog();

        var request = new CreateBookingRequest(
            GuestName: "Auto Confirm Guest",
            PartySize: 2,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(21)),
            StartTime: new TimeOnly(12, 30),
            Source: "web"
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>();

        var events = _fixture.GetEventBus().GetEventLog();

        var createdEvent = events.OfType<BookingCreated>().FirstOrDefault(e => e.BookingId == booking!.Id);
        createdEvent.Should().NotBeNull();

        var confirmedEvent = events.OfType<BookingConfirmed>().FirstOrDefault(e => e.BookingId == booking!.Id);
        confirmedEvent.Should().NotBeNull();
        confirmedEvent!.ConfirmationMethod.Should().Be("auto");
    }

    [Fact]
    public async Task Confirm_PublishesBookingConfirmedEvent()
    {
        // Create a pending booking
        var createRequest = new CreateBookingRequest(
            GuestName: "Confirm Event Test",
            PartySize: 2,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(22)),
            StartTime: new TimeOnly(19, 0),
            Source: "phone"
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings",
            createRequest);
        var booking = await createResponse.Content.ReadFromJsonAsync<BookingDto>();

        _fixture.ClearEventLog();

        var confirmRequest = new ConfirmBookingRequest(ConfirmationMethod: "email");
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking!.Id}/confirm",
            confirmRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = _fixture.GetEventBus().GetEventLog();
        var confirmedEvent = events.OfType<BookingConfirmed>().FirstOrDefault(e => e.BookingId == booking.Id);

        confirmedEvent.Should().NotBeNull();
        confirmedEvent!.BookingReference.Should().Be(booking.BookingReference);
        confirmedEvent.ConfirmationMethod.Should().Be("email");
    }

    [Fact]
    public async Task Seat_PublishesGuestSeatedEvent()
    {
        // Create and confirm booking
        var createRequest = new CreateBookingRequest(
            GuestName: "Seat Event Test",
            PartySize: 3,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(23)),
            StartTime: new TimeOnly(19, 0),
            TableId: _fixture.TestTableId,
            Source: "phone"
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings",
            createRequest);
        var booking = await createResponse.Content.ReadFromJsonAsync<BookingDto>();

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking!.Id}/confirm",
            new ConfirmBookingRequest());

        _fixture.ClearEventLog();

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking.Id}/seat",
            new SeatBookingRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = _fixture.GetEventBus().GetEventLog();
        var seatedEvent = events.OfType<GuestSeated>().FirstOrDefault(e => e.BookingId == booking.Id);

        seatedEvent.Should().NotBeNull();
        seatedEvent!.LocationId.Should().Be(_fixture.TestLocationId);
        seatedEvent.TableId.Should().Be(_fixture.TestTableId);
        seatedEvent.PartySize.Should().Be(3);
    }

    [Fact]
    public async Task Complete_PublishesGuestDepartedEvent()
    {
        // Create, confirm, and seat booking
        var createRequest = new CreateBookingRequest(
            GuestName: "Complete Event Test",
            PartySize: 2,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(24)),
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

        _fixture.ClearEventLog();

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking.Id}/complete",
            new CompleteBookingRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = _fixture.GetEventBus().GetEventLog();
        var departedEvent = events.OfType<GuestDeparted>().FirstOrDefault(e => e.BookingId == booking.Id);

        departedEvent.Should().NotBeNull();
        departedEvent!.BookingReference.Should().Be(booking.BookingReference);
        departedEvent.LocationId.Should().Be(_fixture.TestLocationId);
    }

    [Fact]
    public async Task Cancel_PublishesBookingCancelledEvent()
    {
        // Create booking
        var createRequest = new CreateBookingRequest(
            GuestName: "Cancel Event Test",
            PartySize: 2,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(25)),
            StartTime: new TimeOnly(19, 0),
            Source: "phone"
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings",
            createRequest);
        var booking = await createResponse.Content.ReadFromJsonAsync<BookingDto>();

        _fixture.ClearEventLog();

        var cancelRequest = new CancelBookingRequest(Reason: "Guest changed plans");
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking!.Id}/cancel",
            cancelRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = _fixture.GetEventBus().GetEventLog();
        var cancelledEvent = events.OfType<BookingCancelled>().FirstOrDefault(e => e.BookingId == booking.Id);

        cancelledEvent.Should().NotBeNull();
        cancelledEvent!.BookingReference.Should().Be(booking.BookingReference);
        cancelledEvent.Reason.Should().Be("Guest changed plans");
    }

    [Fact]
    public async Task MarkNoShow_PublishesBookingNoShowEvent()
    {
        // Create and confirm booking
        var createRequest = new CreateBookingRequest(
            GuestName: "NoShow Event Test",
            PartySize: 2,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(26)),
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

        _fixture.ClearEventLog();

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking.Id}/no-show",
            new MarkNoShowRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = _fixture.GetEventBus().GetEventLog();
        var noShowEvent = events.OfType<BookingNoShow>().FirstOrDefault(e => e.BookingId == booking.Id);

        noShowEvent.Should().NotBeNull();
        noShowEvent!.BookingReference.Should().Be(booking.BookingReference);
        noShowEvent.LocationId.Should().Be(_fixture.TestLocationId);
    }

    [Fact]
    public async Task LinkOrder_PublishesBookingLinkedToOrderEvent()
    {
        // Create, confirm, and seat booking
        var createRequest = new CreateBookingRequest(
            GuestName: "Link Order Event Test",
            PartySize: 2,
            BookingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(27)),
            StartTime: new TimeOnly(19, 0),
            TableId: _fixture.TestTableId,
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

        _fixture.ClearEventLog();

        var orderId = Guid.NewGuid();
        var linkRequest = new LinkOrderRequest(OrderId: orderId);
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/bookings/{booking.Id}/link-order",
            linkRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = _fixture.GetEventBus().GetEventLog();
        var linkedEvent = events.OfType<BookingLinkedToOrder>().FirstOrDefault(e => e.BookingId == booking.Id);

        linkedEvent.Should().NotBeNull();
        linkedEvent!.BookingReference.Should().Be(booking.BookingReference);
        linkedEvent.OrderId.Should().Be(orderId);
    }
}
