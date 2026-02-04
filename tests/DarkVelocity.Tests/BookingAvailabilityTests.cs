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
