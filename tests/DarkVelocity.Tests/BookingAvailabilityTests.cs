using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class BookingSettingsGrainTests
{
    private readonly TestClusterFixture _fixture;

    public BookingSettingsGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IBookingSettingsGrain> CreateBookingSettingsAsync(Guid orgId, Guid siteId)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(GrainKeys.BookingSettings(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        return grain;
    }

    // Given: a venue with no booking settings configured
    // When: booking settings are initialized for the venue
    // Then: default settings are applied (11am open, 10pm close, max party size 8)
    [Fact]
    public async Task InitializeAsync_ShouldCreateSettings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(GrainKeys.BookingSettings(orgId, siteId));

        // Act
        await grain.InitializeAsync(orgId, siteId);

        // Assert
        var exists = await grain.ExistsAsync();
        exists.Should().BeTrue();

        var state = await grain.GetStateAsync();
        state.OrganizationId.Should().Be(orgId);
        state.SiteId.Should().Be(siteId);
        state.DefaultOpenTime.Should().Be(new TimeOnly(11, 0));
        state.DefaultCloseTime.Should().Be(new TimeOnly(22, 0));
        state.MaxPartySizeOnline.Should().Be(8);
    }

    // Given: a venue with default booking settings
    // When: the manager updates operating hours, party size limits, slot intervals, and deposit requirements
    // Then: all updated settings are persisted correctly
    [Fact]
    public async Task UpdateAsync_ShouldUpdateSettings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateBookingSettingsAsync(orgId, siteId);

        // Act
        await grain.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: new TimeOnly(10, 0),
            DefaultCloseTime: new TimeOnly(23, 0),
            MaxPartySizeOnline: 10,
            MaxBookingsPerSlot: 15,
            SlotInterval: TimeSpan.FromMinutes(30),
            RequireDeposit: true,
            DepositAmount: 25m));

        // Assert
        var state = await grain.GetStateAsync();
        state.DefaultOpenTime.Should().Be(new TimeOnly(10, 0));
        state.DefaultCloseTime.Should().Be(new TimeOnly(23, 0));
        state.MaxPartySizeOnline.Should().Be(10);
        state.MaxBookingsPerSlot.Should().Be(15);
        state.SlotInterval.Should().Be(TimeSpan.FromMinutes(30));
        state.RequireDeposit.Should().BeTrue();
        state.DepositAmount.Should().Be(25m);
    }

    // Given: a venue open from 11am to 10pm with default settings
    // When: availability is requested for a party of 4 on a future date
    // Then: all time slots within operating hours are returned as available
    [Fact]
    public async Task GetAvailabilityAsync_ShouldReturnSlots()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateBookingSettingsAsync(orgId, siteId);
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // Act
        var slots = await grain.GetAvailabilityAsync(new GetAvailabilityQuery(date, 4));

        // Assert
        slots.Should().NotBeEmpty();
        slots.All(s => s.Time >= new TimeOnly(11, 0)).Should().BeTrue();
        slots.All(s => s.Time < new TimeOnly(22, 0)).Should().BeTrue();
        slots.All(s => s.IsAvailable).Should().BeTrue(); // No blocked dates
    }

    // Given: a venue with a maximum online party size of 8
    // When: availability is requested for a party of 12
    // Then: all time slots are returned as unavailable because the party exceeds the maximum
    [Fact]
    public async Task GetAvailabilityAsync_WithLargeParty_ShouldReturnUnavailable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateBookingSettingsAsync(orgId, siteId);
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // Act - Party size exceeds max online (8)
        var slots = await grain.GetAvailabilityAsync(new GetAvailabilityQuery(date, 12));

        // Assert
        slots.All(s => !s.IsAvailable).Should().BeTrue();
    }

    // Given: a venue open from 11am to 10pm
    // When: slot availability is checked at noon, 6pm, 10am (before open), and 11pm (after close)
    // Then: slots during operating hours are available; slots outside are unavailable
    [Fact]
    public async Task IsSlotAvailableAsync_ShouldCheckAvailability()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateBookingSettingsAsync(orgId, siteId);
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // Act & Assert
        (await grain.IsSlotAvailableAsync(date, new TimeOnly(12, 0), 4)).Should().BeTrue();
        (await grain.IsSlotAvailableAsync(date, new TimeOnly(18, 0), 4)).Should().BeTrue();
        (await grain.IsSlotAvailableAsync(date, new TimeOnly(10, 0), 4)).Should().BeFalse(); // Before open
        (await grain.IsSlotAvailableAsync(date, new TimeOnly(23, 0), 4)).Should().BeFalse(); // After close
        (await grain.IsSlotAvailableAsync(date, new TimeOnly(12, 0), 12)).Should().BeFalse(); // Too large
    }

    // Given: a venue accepting reservations
    // When: a specific date is blocked (e.g., private event or holiday)
    // Then: the date is marked as blocked and no reservations can be made for that date
    [Fact]
    public async Task BlockDateAsync_ShouldBlockDate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateBookingSettingsAsync(orgId, siteId);
        var blockedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7));

        // Act
        await grain.BlockDateAsync(blockedDate);

        // Assert
        (await grain.IsDateBlockedAsync(blockedDate)).Should().BeTrue();
        (await grain.IsSlotAvailableAsync(blockedDate, new TimeOnly(12, 0), 4)).Should().BeFalse();
    }

    // Given: a venue with a previously blocked date
    // When: the blocked date is unblocked
    // Then: the date becomes available for reservations again
    [Fact]
    public async Task UnblockDateAsync_ShouldUnblockDate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateBookingSettingsAsync(orgId, siteId);
        var blockedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
        await grain.BlockDateAsync(blockedDate);

        // Act
        await grain.UnblockDateAsync(blockedDate);

        // Assert
        (await grain.IsDateBlockedAsync(blockedDate)).Should().BeFalse();
        (await grain.IsSlotAvailableAsync(blockedDate, new TimeOnly(12, 0), 4)).Should().BeTrue();
    }

    // Given: a venue with a blocked date
    // When: availability is requested for the blocked date
    // Then: all time slots on that date are returned as unavailable
    [Fact]
    public async Task GetAvailabilityAsync_WithBlockedDate_ShouldReturnUnavailable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateBookingSettingsAsync(orgId, siteId);
        var blockedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        await grain.BlockDateAsync(blockedDate);

        // Act
        var slots = await grain.GetAvailabilityAsync(new GetAvailabilityQuery(blockedDate, 4));

        // Assert
        slots.All(s => !s.IsAvailable).Should().BeTrue();
    }

    // Given: a venue configured with 30-minute slot intervals from 6pm to 10pm
    // When: availability is requested for a party of 2
    // Then: exactly 8 time slots are generated at 30-minute intervals
    [Fact]
    public async Task GetAvailabilityAsync_WithCustomSlotInterval_ShouldGenerateCorrectSlots()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateBookingSettingsAsync(orgId, siteId);
        await grain.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: new TimeOnly(18, 0),
            DefaultCloseTime: new TimeOnly(22, 0),
            SlotInterval: TimeSpan.FromMinutes(30)));
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // Act
        var slots = await grain.GetAvailabilityAsync(new GetAvailabilityQuery(date, 2));

        // Assert
        // 18:00, 18:30, 19:00, 19:30, 20:00, 20:30, 21:00, 21:30 = 8 slots
        slots.Should().HaveCount(8);
        slots[0].Time.Should().Be(new TimeOnly(18, 0));
        slots[1].Time.Should().Be(new TimeOnly(18, 30));
        slots[7].Time.Should().Be(new TimeOnly(21, 30));
    }

    // Settings Validation Tests

    // Given: a venue with booking settings
    // When: the maximum bookings per slot is set to 5
    // Then: each available time slot reports a capacity of 5 bookings
    [Fact]
    public async Task MaxBookingsPerSlot_ShouldEnforce()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateBookingSettingsAsync(orgId, siteId);

        // Act
        await grain.UpdateAsync(new UpdateBookingSettingsCommand(MaxBookingsPerSlot: 5));

        // Assert
        var state = await grain.GetStateAsync();
        state.MaxBookingsPerSlot.Should().Be(5);

        // Verify availability reflects the max bookings per slot
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var slots = await grain.GetAvailabilityAsync(new GetAvailabilityQuery(date, 2));
        slots.Should().NotBeEmpty();
        slots.All(s => s.AvailableCapacity == 5).Should().BeTrue();
    }

    // Given: a venue with default booking settings
    // When: the advance booking window is set to 14 days
    // Then: the 14-day advance booking limit is persisted
    [Fact]
    public async Task AdvanceBookingDays_ShouldValidate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateBookingSettingsAsync(orgId, siteId);

        // Act
        await grain.UpdateAsync(new UpdateBookingSettingsCommand(AdvanceBookingDays: 14));

        // Assert
        var state = await grain.GetStateAsync();
        state.AdvanceBookingDays.Should().Be(14);
    }

    // Given: a venue requiring deposits for large parties
    // When: deposit requirement is enabled with a $50 amount
    // Then: the deposit settings are saved with the default party size threshold of 6
    [Fact]
    public async Task DepositPartySizeThreshold_ShouldApply()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateBookingSettingsAsync(orgId, siteId);

        // Act
        await grain.UpdateAsync(new UpdateBookingSettingsCommand(
            RequireDeposit: true,
            DepositAmount: 50m));

        // Assert
        var state = await grain.GetStateAsync();
        state.RequireDeposit.Should().BeTrue();
        state.DepositAmount.Should().Be(50m);
        state.DepositPartySizeThreshold.Should().Be(6); // Default value
    }

    // Given: a venue with default booking settings
    // When: the cancellation deadline is checked
    // Then: the default cancellation deadline is 24 hours before the reservation
    [Fact]
    public async Task CancellationDeadline_ShouldEnforce()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateBookingSettingsAsync(orgId, siteId);

        // Assert - Check default cancellation deadline
        var state = await grain.GetStateAsync();
        state.CancellationDeadline.Should().Be(TimeSpan.FromHours(24));
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class BookingCalendarGrainTests
{
    private readonly TestClusterFixture _fixture;

    public BookingCalendarGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IBookingCalendarGrain> CreateCalendarAsync(Guid orgId, Guid siteId, DateOnly date)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingCalendarGrain>(GrainKeys.BookingCalendar(orgId, siteId, date));
        await grain.InitializeAsync(orgId, siteId, date);
        return grain;
    }

    // Given: a venue on a specific date with no calendar
    // When: the booking calendar is initialized for that date
    // Then: the calendar is created with the correct date and no bookings
    [Fact]
    public async Task InitializeAsync_ShouldCreateCalendar()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingCalendarGrain>(GrainKeys.BookingCalendar(orgId, siteId, date));

        // Act
        await grain.InitializeAsync(orgId, siteId, date);

        // Assert
        var exists = await grain.ExistsAsync();
        exists.Should().BeTrue();

        var state = await grain.GetStateAsync();
        state.Date.Should().Be(date);
        state.SiteId.Should().Be(siteId);
        state.Bookings.Should().BeEmpty();
    }

    // Given: an empty booking calendar
    // When: a confirmed reservation for the Smith Party (4 guests at 7pm) is added
    // Then: the booking appears on the calendar with the correct confirmation code, time, and party details
    [Fact]
    public async Task AddBookingAsync_ShouldAddBooking()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var bookingId = Guid.NewGuid();
        var grain = await CreateCalendarAsync(orgId, siteId, date);

        var command = new AddBookingToCalendarCommand(
            bookingId,
            "ABC123",
            new TimeOnly(19, 0),
            4,
            "Smith Party",
            BookingStatus.Confirmed);

        // Act
        await grain.AddBookingAsync(command);

        // Assert
        var bookings = await grain.GetBookingsAsync();
        bookings.Should().HaveCount(1);
        bookings[0].BookingId.Should().Be(bookingId);
        bookings[0].ConfirmationCode.Should().Be("ABC123");
        bookings[0].Time.Should().Be(new TimeOnly(19, 0));
        bookings[0].PartySize.Should().Be(4);
        bookings[0].GuestName.Should().Be("Smith Party");
        bookings[0].Status.Should().Be(BookingStatus.Confirmed);
    }

    // Given: a booking calendar with no reservations
    // When: three confirmed reservations totaling 12 covers are added
    // Then: the total cover count for the day is 12
    [Fact]
    public async Task AddBookingAsync_ShouldUpdateTotalCovers()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateCalendarAsync(orgId, siteId, date);

        // Act
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(Guid.NewGuid(), "A1", new TimeOnly(18, 0), 4, "Guest 1", BookingStatus.Confirmed));
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(Guid.NewGuid(), "A2", new TimeOnly(19, 0), 6, "Guest 2", BookingStatus.Confirmed));
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(Guid.NewGuid(), "A3", new TimeOnly(20, 0), 2, "Guest 3", BookingStatus.Confirmed));

        // Assert
        var coverCount = await grain.GetCoverCountAsync();
        coverCount.Should().Be(12); // 4 + 6 + 2
    }

    // Given: a confirmed reservation for Smith at 7pm on the calendar
    // When: the reservation status is updated to Seated at table T5
    // Then: the calendar entry reflects the Seated status and table assignment
    [Fact]
    public async Task UpdateBookingAsync_ShouldUpdateBooking()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var bookingId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateCalendarAsync(orgId, siteId, date);
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(bookingId, "ABC123", new TimeOnly(19, 0), 4, "Smith", BookingStatus.Confirmed));

        // Act
        await grain.UpdateBookingAsync(new UpdateBookingInCalendarCommand(
            bookingId,
            Status: BookingStatus.Seated,
            TableId: tableId,
            TableNumber: "T5"));

        // Assert
        var bookings = await grain.GetBookingsAsync();
        bookings[0].Status.Should().Be(BookingStatus.Seated);
        bookings[0].TableId.Should().Be(tableId);
        bookings[0].TableNumber.Should().Be("T5");
    }

    // Given: a calendar with one confirmed reservation
    // When: the reservation is removed from the calendar
    // Then: the calendar is empty and the cover count returns to zero
    [Fact]
    public async Task RemoveBookingAsync_ShouldRemoveBooking()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var bookingId = Guid.NewGuid();
        var grain = await CreateCalendarAsync(orgId, siteId, date);
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(bookingId, "ABC123", new TimeOnly(19, 0), 4, "Smith", BookingStatus.Confirmed));

        // Act
        await grain.RemoveBookingAsync(bookingId);

        // Assert
        var bookings = await grain.GetBookingsAsync();
        bookings.Should().BeEmpty();

        var coverCount = await grain.GetCoverCountAsync();
        coverCount.Should().Be(0);
    }

    // Given: a calendar with two confirmed and one seated reservation
    // When: bookings are filtered by Confirmed and Seated status
    // Then: the confirmed filter returns 2 bookings and the seated filter returns 1 booking
    [Fact]
    public async Task GetBookingsAsync_WithStatusFilter_ShouldFilterBookings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateCalendarAsync(orgId, siteId, date);

        await grain.AddBookingAsync(new AddBookingToCalendarCommand(Guid.NewGuid(), "A1", new TimeOnly(18, 0), 4, "Guest 1", BookingStatus.Confirmed));
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(Guid.NewGuid(), "A2", new TimeOnly(19, 0), 6, "Guest 2", BookingStatus.Seated));
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(Guid.NewGuid(), "A3", new TimeOnly(20, 0), 2, "Guest 3", BookingStatus.Confirmed));

        // Act
        var confirmedBookings = await grain.GetBookingsAsync(BookingStatus.Confirmed);
        var seatedBookings = await grain.GetBookingsAsync(BookingStatus.Seated);

        // Assert
        confirmedBookings.Should().HaveCount(2);
        seatedBookings.Should().HaveCount(1);
        seatedBookings[0].GuestName.Should().Be("Guest 2");
    }

    // Given: a calendar with lunch bookings at noon and 1pm, and dinner bookings at 6pm and 7pm
    // When: bookings are queried for the lunch window (11am-3pm) and dinner window (5pm-9pm)
    // Then: each time range returns exactly 2 bookings
    [Fact]
    public async Task GetBookingsByTimeRangeAsync_ShouldFilterByTime()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateCalendarAsync(orgId, siteId, date);

        await grain.AddBookingAsync(new AddBookingToCalendarCommand(Guid.NewGuid(), "A1", new TimeOnly(12, 0), 2, "Lunch 1", BookingStatus.Confirmed));
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(Guid.NewGuid(), "A2", new TimeOnly(13, 0), 4, "Lunch 2", BookingStatus.Confirmed));
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(Guid.NewGuid(), "A3", new TimeOnly(18, 0), 4, "Dinner 1", BookingStatus.Confirmed));
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(Guid.NewGuid(), "A4", new TimeOnly(19, 0), 6, "Dinner 2", BookingStatus.Confirmed));

        // Act
        var lunchBookings = await grain.GetBookingsByTimeRangeAsync(new TimeOnly(11, 0), new TimeOnly(15, 0));
        var dinnerBookings = await grain.GetBookingsByTimeRangeAsync(new TimeOnly(17, 0), new TimeOnly(21, 0));

        // Assert
        lunchBookings.Should().HaveCount(2);
        dinnerBookings.Should().HaveCount(2);
    }

    // Given: a calendar with one confirmed, one seated, and one cancelled reservation
    // When: booking counts are requested for each status and overall
    // Then: each status count returns 1, and the total count returns 3
    [Fact]
    public async Task GetBookingCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateCalendarAsync(orgId, siteId, date);

        await grain.AddBookingAsync(new AddBookingToCalendarCommand(Guid.NewGuid(), "A1", new TimeOnly(18, 0), 4, "Guest 1", BookingStatus.Confirmed));
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(Guid.NewGuid(), "A2", new TimeOnly(19, 0), 6, "Guest 2", BookingStatus.Seated));
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(Guid.NewGuid(), "A3", new TimeOnly(20, 0), 2, "Guest 3", BookingStatus.Cancelled));

        // Act & Assert
        (await grain.GetBookingCountAsync()).Should().Be(3);
        (await grain.GetBookingCountAsync(BookingStatus.Confirmed)).Should().Be(1);
        (await grain.GetBookingCountAsync(BookingStatus.Seated)).Should().Be(1);
        (await grain.GetBookingCountAsync(BookingStatus.Cancelled)).Should().Be(1);
    }

    // Given: three reservations added in non-chronological order (7pm, 8pm, 6pm)
    // When: all bookings are retrieved from the calendar
    // Then: the bookings are returned sorted by time (6pm, 7pm, 8pm)
    [Fact]
    public async Task GetBookingsAsync_ShouldReturnSortedByTime()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateCalendarAsync(orgId, siteId, date);

        // Add in random order
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(Guid.NewGuid(), "A2", new TimeOnly(19, 0), 4, "Guest 2", BookingStatus.Confirmed));
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(Guid.NewGuid(), "A3", new TimeOnly(20, 0), 4, "Guest 3", BookingStatus.Confirmed));
        await grain.AddBookingAsync(new AddBookingToCalendarCommand(Guid.NewGuid(), "A1", new TimeOnly(18, 0), 4, "Guest 1", BookingStatus.Confirmed));

        // Act
        var bookings = await grain.GetBookingsAsync();

        // Assert
        bookings[0].Time.Should().Be(new TimeOnly(18, 0));
        bookings[1].Time.Should().Be(new TimeOnly(19, 0));
        bookings[2].Time.Should().Be(new TimeOnly(20, 0));
    }
}
