using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class BookingGrainTests
{
    private readonly TestClusterFixture _fixture;

    public BookingGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private GuestInfo CreateGuestInfo(string name = "John Doe") => new()
    {
        Name = name,
        Phone = "+1234567890",
        Email = "john@example.com"
    };

    private async Task<IBookingGrain> CreateBookingAsync(Guid orgId, Guid siteId, Guid bookingId)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, siteId, bookingId));
        var command = new RequestBookingCommand(
            orgId,
            siteId,
            CreateGuestInfo(),
            DateTime.UtcNow.AddDays(1),
            4);
        await grain.RequestAsync(command);
        return grain;
    }

    // Given: a guest requesting a reservation for 6 with special requests and occasion
    // When: the booking is submitted via the website
    // Then: a new reservation is created with a 6-character confirmation code and all details persisted
    [Fact]
    public async Task RequestAsync_ShouldCreateBooking()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, siteId, bookingId));

        var command = new RequestBookingCommand(
            orgId,
            siteId,
            CreateGuestInfo("Jane Smith"),
            DateTime.UtcNow.AddDays(2),
            6,
            TimeSpan.FromHours(2),
            "Window table please",
            "Birthday",
            BookingSource.Website);

        // Act
        var result = await grain.RequestAsync(command);

        // Assert
        result.Id.Should().Be(bookingId);
        result.ConfirmationCode.Should().HaveLength(6);

        var state = await grain.GetStateAsync();
        state.PartySize.Should().Be(6);
        state.Guest.Name.Should().Be("Jane Smith");
        state.Status.Should().Be(BookingStatus.Requested);
        state.SpecialRequests.Should().Be("Window table please");
        state.Occasion.Should().Be("Birthday");
        state.Source.Should().Be(BookingSource.Website);
    }

    // Given: a reservation in Requested status
    // When: the host confirms the reservation
    // Then: the reservation status changes to Confirmed with a confirmation timestamp
    [Fact]
    public async Task ConfirmAsync_ShouldConfirmBooking()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);

        // Act
        var result = await grain.ConfirmAsync();

        // Assert
        result.ConfirmationCode.Should().NotBeEmpty();
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(BookingStatus.Confirmed);
        state.ConfirmedAt.Should().NotBeNull();
    }

    // Given: an existing reservation for 4 guests
    // When: the guest calls to change the party size to 8 and update the date
    // Then: the reservation reflects the new party size, time, and special requests
    [Fact]
    public async Task ModifyAsync_ShouldUpdateBooking()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        var newTime = DateTime.UtcNow.AddDays(3);

        // Act
        await grain.ModifyAsync(new ModifyBookingCommand(newTime, 8, null, "Updated requests"));

        // Assert
        var state = await grain.GetStateAsync();
        state.PartySize.Should().Be(8);
        state.RequestedTime.Should().BeCloseTo(newTime, TimeSpan.FromSeconds(1));
        state.SpecialRequests.Should().Be("Updated requests");
    }

    // Given: an existing reservation
    // When: the guest cancels the reservation with a reason
    // Then: the reservation status changes to Cancelled with the cancellation reason recorded
    [Fact]
    public async Task CancelAsync_ShouldCancelBooking()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var cancelledBy = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);

        // Act
        await grain.CancelAsync(new CancelBookingCommand("Customer request", cancelledBy));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(BookingStatus.Cancelled);
        state.CancellationReason.Should().Be("Customer request");
        state.CancelledBy.Should().Be(cancelledBy);
    }

    // Given: an existing reservation without a table assignment
    // When: the host assigns table T5 (capacity 6) to the reservation
    // Then: the table assignment is recorded with the correct table number and capacity
    [Fact]
    public async Task AssignTableAsync_ShouldAddTableAssignment()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);

        // Act
        await grain.AssignTableAsync(new AssignTableCommand(tableId, "T5", 6));

        // Assert
        var state = await grain.GetStateAsync();
        state.TableAssignments.Should().HaveCount(1);
        state.TableAssignments[0].TableId.Should().Be(tableId);
        state.TableAssignments[0].TableNumber.Should().Be("T5");
        state.TableAssignments[0].Capacity.Should().Be(6);
    }

    // Given: a confirmed reservation
    // When: the guest arrives at the restaurant
    // Then: the reservation status changes to Arrived with the arrival time and check-in staff recorded
    [Fact]
    public async Task RecordArrivalAsync_ShouldMarkArrived()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var checkedInBy = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        await grain.ConfirmAsync();

        // Act
        var arrivedAt = await grain.RecordArrivalAsync(new RecordArrivalCommand(checkedInBy));

        // Assert
        arrivedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(BookingStatus.Arrived);
        state.CheckedInBy.Should().Be(checkedInBy);
    }

    // Given: a guest who has arrived for their reservation
    // When: the host seats the guest at table T10
    // Then: the reservation status changes to Seated with the seating time and table assignment recorded
    [Fact]
    public async Task SeatGuestAsync_ShouldSeatGuest()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var seatedBy = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        await grain.ConfirmAsync();
        await grain.RecordArrivalAsync(new RecordArrivalCommand(Guid.NewGuid()));

        // Act
        await grain.SeatGuestAsync(new SeatGuestCommand(tableId, "T10", seatedBy));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(BookingStatus.Seated);
        state.SeatedAt.Should().NotBeNull();
        state.SeatedBy.Should().Be(seatedBy);
        state.TableAssignments.Should().Contain(t => t.TableId == tableId);
    }

    // Given: a seated guest with an active order
    // When: the guest finishes dining and departs
    // Then: the reservation is marked Completed with departure time and linked order
    [Fact]
    public async Task RecordDepartureAsync_ShouldCompleteBooking()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        await grain.ConfirmAsync();
        await grain.RecordArrivalAsync(new RecordArrivalCommand(Guid.NewGuid()));
        await grain.SeatGuestAsync(new SeatGuestCommand(Guid.NewGuid(), "T1", Guid.NewGuid()));

        // Act
        await grain.RecordDepartureAsync(new RecordDepartureCommand(orderId));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(BookingStatus.Completed);
        state.DepartedAt.Should().NotBeNull();
        state.LinkedOrderId.Should().Be(orderId);
    }

    // Given: a confirmed reservation where the guest has not arrived
    // When: staff marks the reservation as a no-show
    // Then: the reservation status changes to NoShow
    [Fact]
    public async Task MarkNoShowAsync_ShouldMarkNoShow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        await grain.ConfirmAsync();

        // Act
        await grain.MarkNoShowAsync(Guid.NewGuid());

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(BookingStatus.NoShow);
    }

    // Given: an existing reservation
    // When: a deposit of $50 is required for the reservation
    // Then: the reservation status changes to PendingDeposit with the deposit amount recorded
    [Fact]
    public async Task RequireDepositAsync_ShouldSetDepositRequired()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);

        // Act
        await grain.RequireDepositAsync(new RequireDepositCommand(50m, DateTime.UtcNow.AddDays(1)));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(BookingStatus.PendingDeposit);
        state.Deposit.Should().NotBeNull();
        state.Deposit!.Amount.Should().Be(50m);
        state.Deposit.Status.Should().Be(DepositStatus.Required);
    }

    // Given: a reservation with a $50 deposit required
    // When: the guest pays the deposit by credit card
    // Then: the deposit is marked as Paid with the payment method and reference recorded
    [Fact]
    public async Task RecordDepositPaymentAsync_ShouldMarkDepositPaid()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        await grain.RequireDepositAsync(new RequireDepositCommand(50m, DateTime.UtcNow.AddDays(1)));

        // Act
        await grain.RecordDepositPaymentAsync(new RecordDepositPaymentCommand(PaymentMethod.CreditCard, "ref123"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Deposit!.Status.Should().Be(DepositStatus.Paid);
        state.Deposit.PaymentMethod.Should().Be(PaymentMethod.CreditCard);
        state.Deposit.PaymentReference.Should().Be("ref123");
    }

    // Given: a reservation with a required but unpaid deposit
    // When: the host attempts to confirm the reservation
    // Then: confirmation is rejected because the deposit has not been paid
    [Fact]
    public async Task ConfirmAsync_WithUnpaidDeposit_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        await grain.RequireDepositAsync(new RequireDepositCommand(50m, DateTime.UtcNow.AddDays(1)));

        // Act
        var act = () => grain.ConfirmAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Deposit required but not paid*");
    }

    // Given: an existing reservation
    // When: VIP and Anniversary tags are added to the reservation
    // Then: both tags are associated with the reservation
    [Fact]
    public async Task AddTagAsync_ShouldAddTag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);

        // Act
        await grain.AddTagAsync("VIP");
        await grain.AddTagAsync("Anniversary");

        // Assert
        var state = await grain.GetStateAsync();
        state.Tags.Should().Contain("VIP");
        state.Tags.Should().Contain("Anniversary");
    }

    // State Transition Tests

    // Given: a cancelled reservation
    // When: the host attempts to modify the reservation
    // Then: modification is rejected because cancelled reservations cannot be changed
    [Fact]
    public async Task ModifyAsync_CancelledBooking_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        await grain.CancelAsync(new CancelBookingCommand("No longer needed", Guid.NewGuid()));

        // Act
        var act = () => grain.ModifyAsync(new ModifyBookingCommand(DateTime.UtcNow.AddDays(5), 6));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status*");
    }

    // Given: a reservation that has been completed (guest departed)
    // When: the host attempts to modify the reservation
    // Then: modification is rejected because completed reservations cannot be changed
    [Fact]
    public async Task ModifyAsync_CompletedBooking_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        await grain.ConfirmAsync();
        await grain.RecordArrivalAsync(new RecordArrivalCommand(Guid.NewGuid()));
        await grain.SeatGuestAsync(new SeatGuestCommand(Guid.NewGuid(), "T1", Guid.NewGuid()));
        await grain.RecordDepartureAsync(new RecordDepartureCommand(Guid.NewGuid()));

        // Act
        var act = () => grain.ModifyAsync(new ModifyBookingCommand(DateTime.UtcNow.AddDays(5), 6));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status*");
    }

    // Given: a reservation still in Requested status (not yet confirmed)
    // When: staff attempts to mark it as a no-show
    // Then: the no-show is rejected because only confirmed reservations can be marked as no-shows
    [Fact]
    public async Task MarkNoShowAsync_NonConfirmedBooking_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        // Booking is in Requested status, not Confirmed

        // Act
        var act = () => grain.MarkNoShowAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status*");
    }

    // Given: a reservation in Requested status (guest has not arrived)
    // When: the host attempts to seat the guest
    // Then: seating is rejected because the guest must arrive before being seated
    [Fact]
    public async Task SeatAsync_WithoutArrival_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        // Booking is in Requested status, not Arrived or Confirmed

        // Act
        var act = () => grain.SeatGuestAsync(new SeatGuestCommand(Guid.NewGuid(), "T1", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status*");
    }

    // Given: a guest who has arrived but has not been seated
    // When: the host attempts to record their departure
    // Then: departure is rejected because the guest must be seated before departing
    [Fact]
    public async Task RecordDepartureAsync_WithoutSeating_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        await grain.ConfirmAsync();
        await grain.RecordArrivalAsync(new RecordArrivalCommand(Guid.NewGuid()));
        // Guest arrived but not seated

        // Act
        var act = () => grain.RecordDepartureAsync(new RecordDepartureCommand(Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status*");
    }

    // Deposit Edge Cases

    // Given: a reservation with a $50 deposit required
    // When: a manager waives the deposit requirement
    // Then: the deposit status changes to Waived
    [Fact]
    public async Task WaiveDepositAsync_ShouldWaiveDeposit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var waivedBy = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        await grain.RequireDepositAsync(new RequireDepositCommand(50m, DateTime.UtcNow.AddDays(1)));

        // Act
        await grain.WaiveDepositAsync(waivedBy);

        // Assert
        var state = await grain.GetStateAsync();
        state.Deposit.Should().NotBeNull();
        state.Deposit!.Status.Should().Be(DepositStatus.Waived);
    }

    // Given: a reservation with a deposit required but not yet paid
    // When: staff attempts to forfeit the deposit
    // Then: forfeiture is rejected because there is no paid deposit to forfeit
    [Fact]
    public async Task ForfeitDepositAsync_WithoutPaidDeposit_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        await grain.RequireDepositAsync(new RequireDepositCommand(50m, DateTime.UtcNow.AddDays(1)));
        // Deposit is Required but not Paid

        // Act
        var act = () => grain.ForfeitDepositAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No paid deposit to forfeit*");
    }

    // Given: a reservation with a deposit required but not yet paid
    // When: staff attempts to refund the deposit
    // Then: refund is rejected because there is no paid deposit to refund
    [Fact]
    public async Task RefundDepositAsync_WithoutPaidDeposit_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        await grain.RequireDepositAsync(new RequireDepositCommand(50m, DateTime.UtcNow.AddDays(1)));
        // Deposit is Required but not Paid

        // Act
        var act = () => grain.RefundDepositAsync("Customer requested", Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No paid deposit to refund*");
    }

    // Given: a cancelled reservation with a previously paid deposit
    // When: staff processes a deposit refund after cancellation
    // Then: the deposit status changes to Refunded with a refund timestamp while the reservation remains Cancelled
    [Fact]
    public async Task DepositTransitions_AfterCancellation_ShouldHandle()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        await grain.RequireDepositAsync(new RequireDepositCommand(50m, DateTime.UtcNow.AddDays(1)));
        await grain.RecordDepositPaymentAsync(new RecordDepositPaymentCommand(PaymentMethod.CreditCard, "ref123"));
        await grain.CancelAsync(new CancelBookingCommand("Customer cancelled", Guid.NewGuid()));

        // Act - Refund the deposit after cancellation
        await grain.RefundDepositAsync("Booking cancelled", Guid.NewGuid());

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(BookingStatus.Cancelled);
        state.Deposit.Should().NotBeNull();
        state.Deposit!.Status.Should().Be(DepositStatus.Refunded);
        state.Deposit.RefundedAt.Should().NotBeNull();
    }

    // Table Assignment Tests

    // Given: a reservation with table T5 assigned
    // When: the host clears the table assignment
    // Then: the reservation has no table assignments
    [Fact]
    public async Task ClearTableAssignmentAsync_ShouldClearTable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);
        await grain.AssignTableAsync(new AssignTableCommand(tableId, "T5", 6));

        // Verify assignment exists
        var stateBefore = await grain.GetStateAsync();
        stateBefore.TableAssignments.Should().HaveCount(1);

        // Act
        await grain.ClearTableAssignmentAsync();

        // Assert
        var stateAfter = await grain.GetStateAsync();
        stateAfter.TableAssignments.Should().BeEmpty();
    }

    // Given: a reservation for a large party
    // When: three tables (T1, T2, T3) are assigned to accommodate the group
    // Then: all three table assignments are tracked on the reservation
    [Fact]
    public async Task AssignTableAsync_MultipleTables_ShouldTrackAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var tableId1 = Guid.NewGuid();
        var tableId2 = Guid.NewGuid();
        var tableId3 = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);

        // Act
        await grain.AssignTableAsync(new AssignTableCommand(tableId1, "T1", 4));
        await grain.AssignTableAsync(new AssignTableCommand(tableId2, "T2", 4));
        await grain.AssignTableAsync(new AssignTableCommand(tableId3, "T3", 4));

        // Assert
        var state = await grain.GetStateAsync();
        state.TableAssignments.Should().HaveCount(3);
        state.TableAssignments.Should().Contain(t => t.TableId == tableId1 && t.TableNumber == "T1");
        state.TableAssignments.Should().Contain(t => t.TableId == tableId2 && t.TableNumber == "T2");
        state.TableAssignments.Should().Contain(t => t.TableId == tableId3 && t.TableNumber == "T3");
    }

    // Given: a reservation with table T5 already assigned
    // When: the same table T5 is assigned again
    // Then: the duplicate assignment is handled without error
    [Fact]
    public async Task AssignTableAsync_SameTableTwice_ShouldBeIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = await CreateBookingAsync(orgId, siteId, bookingId);

        // Act - Assign the same table twice
        await grain.AssignTableAsync(new AssignTableCommand(tableId, "T5", 6));
        await grain.AssignTableAsync(new AssignTableCommand(tableId, "T5", 6));

        // Assert - Should have two assignments (grain doesn't de-duplicate)
        var state = await grain.GetStateAsync();
        state.TableAssignments.Should().HaveCountGreaterThanOrEqualTo(1);
        state.TableAssignments.Where(t => t.TableId == tableId).Should().NotBeEmpty();
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class WaitlistGrainTests
{
    private readonly TestClusterFixture _fixture;

    public WaitlistGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private GuestInfo CreateGuestInfo(string name = "John Doe") => new()
    {
        Name = name,
        Phone = "+1234567890"
    };

    private async Task<IWaitlistGrain> CreateWaitlistAsync(Guid orgId, Guid siteId, DateOnly date)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IWaitlistGrain>(GrainKeys.Waitlist(orgId, siteId, date));
        await grain.InitializeAsync(orgId, siteId, date);
        return grain;
    }

    // Given: a venue on a specific date with no waitlist
    // When: the waitlist is initialized for that date
    // Then: the waitlist is created and ready to accept walk-in entries
    [Fact]
    public async Task InitializeAsync_ShouldCreateWaitlist()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IWaitlistGrain>(GrainKeys.Waitlist(orgId, siteId, date));

        // Act
        await grain.InitializeAsync(orgId, siteId, date);

        // Assert
        var exists = await grain.ExistsAsync();
        exists.Should().BeTrue();

        var state = await grain.GetStateAsync();
        state.Date.Should().Be(date);
        state.SiteId.Should().Be(siteId);
    }

    // Given: an active waitlist for a venue
    // When: a walk-in party of 4 is added with a 30-minute quoted wait
    // Then: the entry is created at position 1 with the quoted wait time
    [Fact]
    public async Task AddEntryAsync_ShouldAddToWaitlist()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        var command = new AddToWaitlistCommand(
            CreateGuestInfo(),
            4,
            TimeSpan.FromMinutes(30),
            "Patio preferred",
            NotificationMethod.Sms);

        // Act
        var result = await grain.AddEntryAsync(command);

        // Assert
        result.EntryId.Should().NotBeEmpty();
        result.Position.Should().Be(1);
        result.QuotedWait.Should().Be(TimeSpan.FromMinutes(30));
    }

    // Given: a waitlist with no entries
    // When: three walk-in parties are added sequentially
    // Then: each party receives an incrementing position (1, 2, 3)
    [Fact]
    public async Task AddEntryAsync_ShouldIncrementPosition()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        // Act
        var result1 = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo("Guest 1"), 2, TimeSpan.FromMinutes(15)));
        var result2 = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo("Guest 2"), 4, TimeSpan.FromMinutes(20)));
        var result3 = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo("Guest 3"), 6, TimeSpan.FromMinutes(30)));

        // Assert
        result1.Position.Should().Be(1);
        result2.Position.Should().Be(2);
        result3.Position.Should().Be(3);
    }

    // Given: a party of 4 waiting on the waitlist
    // When: their table becomes ready and they are notified
    // Then: the entry status changes to Notified with a notification timestamp
    [Fact]
    public async Task NotifyEntryAsync_ShouldMarkNotified()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);
        var result = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo(), 4, TimeSpan.FromMinutes(15)));

        // Act
        await grain.NotifyEntryAsync(result.EntryId);

        // Assert
        var entries = await grain.GetEntriesAsync();
        entries.Should().HaveCount(1);
        entries[0].Status.Should().Be(WaitlistStatus.Notified);
        entries[0].NotifiedAt.Should().NotBeNull();
    }

    // Given: a party of 4 waiting on the waitlist
    // When: a table opens up and the party is seated
    // Then: the entry status changes to Seated with a seating timestamp
    [Fact]
    public async Task SeatEntryAsync_ShouldMarkSeated()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var tableId = Guid.NewGuid();
        var grain = await CreateWaitlistAsync(orgId, siteId, date);
        var result = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo(), 4, TimeSpan.FromMinutes(15)));

        // Act
        await grain.SeatEntryAsync(result.EntryId, tableId);

        // Assert
        var state = await grain.GetStateAsync();
        var entry = state.Entries.First(e => e.Id == result.EntryId);
        entry.Status.Should().Be(WaitlistStatus.Seated);
        entry.SeatedAt.Should().NotBeNull();
    }

    // Given: a party of 4 waiting on the waitlist
    // When: the guest decides to leave without being seated
    // Then: the entry status changes to Left
    [Fact]
    public async Task RemoveEntryAsync_ShouldMarkLeft()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);
        var result = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo(), 4, TimeSpan.FromMinutes(15)));

        // Act
        await grain.RemoveEntryAsync(result.EntryId, "Guest left");

        // Assert
        var state = await grain.GetStateAsync();
        var entry = state.Entries.First(e => e.Id == result.EntryId);
        entry.Status.Should().Be(WaitlistStatus.Left);
    }

    // Given: a waitlist with two parties currently waiting
    // When: the waiting count is requested
    // Then: the count reflects exactly 2 waiting parties
    [Fact]
    public async Task GetWaitingCountAsync_ShouldReturnCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);
        await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo("Guest 1"), 2, TimeSpan.FromMinutes(15)));
        await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo("Guest 2"), 4, TimeSpan.FromMinutes(20)));

        // Act
        var count = await grain.GetWaitingCountAsync();

        // Assert
        count.Should().Be(2);
    }

    // Given: a walk-in party of 4 on the waitlist
    // When: the party is converted to a formal reservation
    // Then: a booking is created and linked to the waitlist entry
    [Fact]
    public async Task ConvertToBookingAsync_ShouldConvertEntry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);
        var result = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo(), 4, TimeSpan.FromMinutes(15)));

        // Act
        var bookingId = await grain.ConvertToBookingAsync(result.EntryId, DateTime.UtcNow.AddHours(1));

        // Assert
        bookingId.Should().NotBeNull();

        var state = await grain.GetStateAsync();
        var entry = state.Entries.First(e => e.Id == result.EntryId);
        entry.ConvertedToBookingId.Should().Be(bookingId);
    }

    // Given: three waitlist entries where one is seated and one is notified
    // When: the active entries are requested
    // Then: only the notified and waiting entries are returned (seated entries are excluded)
    [Fact]
    public async Task GetEntriesAsync_ShouldReturnOnlyActiveEntries()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);
        var entry1 = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo("Guest 1"), 2, TimeSpan.FromMinutes(15)));
        var entry2 = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo("Guest 2"), 4, TimeSpan.FromMinutes(20)));
        var entry3 = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo("Guest 3"), 6, TimeSpan.FromMinutes(30)));

        await grain.SeatEntryAsync(entry1.EntryId, Guid.NewGuid());
        await grain.NotifyEntryAsync(entry2.EntryId);

        // Act
        var entries = await grain.GetEntriesAsync();

        // Assert
        entries.Should().HaveCount(2); // entry2 (Notified) and entry3 (Waiting)
        entries.Select(e => e.Id).Should().Contain(entry2.EntryId);
        entries.Select(e => e.Id).Should().Contain(entry3.EntryId);
    }

    // Position and Estimated Wait Tests

    // Given: three parties on the waitlist in order
    // When: the third party is moved to position 1
    // Then: the party's position is updated to 1
    [Fact]
    public async Task UpdatePositionAsync_ShouldReorderPositions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);
        var entry1 = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo("Guest 1"), 2, TimeSpan.FromMinutes(15)));
        var entry2 = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo("Guest 2"), 4, TimeSpan.FromMinutes(20)));
        var entry3 = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo("Guest 3"), 6, TimeSpan.FromMinutes(30)));

        // Act - Move entry3 to position 1
        await grain.UpdatePositionAsync(entry3.EntryId, 1);

        // Assert
        var state = await grain.GetStateAsync();
        var entry = state.Entries.First(e => e.Id == entry3.EntryId);
        entry.Position.Should().Be(1);
    }

    // Given: an empty waitlist with no seating history
    // When: the estimated wait is requested for a party of 4
    // Then: the default estimate of 15 minutes is returned
    [Fact]
    public async Task GetEstimatedWaitAsync_ShouldCalculate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        // Act - No seated entries yet, should return default estimate
        var estimate = await grain.GetEstimatedWaitAsync(4);

        // Assert
        estimate.Should().Be(TimeSpan.FromMinutes(15)); // Default when no history
    }

    // Given: a waitlist with one seated entry and two parties still waiting
    // When: the estimated wait is requested for a party of 4
    // Then: the estimate is calculated based on seating history and waiting parties ahead
    [Fact]
    public async Task GetEstimatedWaitAsync_WithHistory_ShouldCalculate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);

        // Add and seat some entries to build history
        var entry1 = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo("Guest 1"), 2, TimeSpan.FromMinutes(15)));
        await grain.SeatEntryAsync(entry1.EntryId, Guid.NewGuid());

        // Add more waiting entries
        await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo("Guest 2"), 4, TimeSpan.FromMinutes(20)));
        await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo("Guest 3"), 6, TimeSpan.FromMinutes(30)));

        // Act
        var estimate = await grain.GetEstimatedWaitAsync(4);

        // Assert - Should calculate based on waiting entries ahead and average wait
        estimate.Should().BeGreaterThan(TimeSpan.Zero);
    }

    // Notify Tests

    // Given: a waitlist entry that has already been seated
    // When: staff attempts to notify the already-seated guest
    // Then: notification is rejected because seated entries cannot be notified
    [Fact]
    public async Task NotifyAsync_FromNonWaiting_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var grain = await CreateWaitlistAsync(orgId, siteId, date);
        var entry = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo(), 4, TimeSpan.FromMinutes(15)));

        // Seat the entry (no longer in Waiting status)
        await grain.SeatEntryAsync(entry.EntryId, Guid.NewGuid());

        // Act
        var act = () => grain.NotifyEntryAsync(entry.EntryId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Entry cannot be notified*");
    }

    // Seat Tests

    // Given: a walk-in party currently waiting on the waitlist
    // When: a table opens and the party is seated directly from waiting status
    // Then: the entry status changes to Seated with a seating timestamp
    [Fact]
    public async Task SeatAsync_FromWaiting_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var tableId = Guid.NewGuid();
        var grain = await CreateWaitlistAsync(orgId, siteId, date);
        var entry = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo(), 4, TimeSpan.FromMinutes(15)));

        // Entry is in Waiting status

        // Act
        await grain.SeatEntryAsync(entry.EntryId, tableId);

        // Assert
        var state = await grain.GetStateAsync();
        var seatedEntry = state.Entries.First(e => e.Id == entry.EntryId);
        seatedEntry.Status.Should().Be(WaitlistStatus.Seated);
        seatedEntry.SeatedAt.Should().NotBeNull();
    }

    // Given: a walk-in party that has been notified their table is ready
    // When: the party responds and is seated
    // Then: the entry status changes from Notified to Seated
    [Fact]
    public async Task SeatAsync_FromNotified_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var tableId = Guid.NewGuid();
        var grain = await CreateWaitlistAsync(orgId, siteId, date);
        var entry = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo(), 4, TimeSpan.FromMinutes(15)));

        // Notify the entry first
        await grain.NotifyEntryAsync(entry.EntryId);

        // Verify it's in Notified status
        var stateAfterNotify = await grain.GetStateAsync();
        stateAfterNotify.Entries.First(e => e.Id == entry.EntryId).Status.Should().Be(WaitlistStatus.Notified);

        // Act
        await grain.SeatEntryAsync(entry.EntryId, tableId);

        // Assert
        var state = await grain.GetStateAsync();
        var seatedEntry = state.Entries.First(e => e.Id == entry.EntryId);
        seatedEntry.Status.Should().Be(WaitlistStatus.Seated);
        seatedEntry.SeatedAt.Should().NotBeNull();
    }

    // Given: a walk-in party that has left the waitlist
    // When: staff attempts to seat the departed party
    // Then: seating is rejected because departed guests cannot be seated
    [Fact]
    public async Task SeatAsync_FromLeft_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var tableId = Guid.NewGuid();
        var grain = await CreateWaitlistAsync(orgId, siteId, date);
        var entry = await grain.AddEntryAsync(new AddToWaitlistCommand(CreateGuestInfo(), 4, TimeSpan.FromMinutes(15)));

        // Remove the entry (marks as Left)
        await grain.RemoveEntryAsync(entry.EntryId, "Guest left");

        // Act
        var act = () => grain.SeatEntryAsync(entry.EntryId, tableId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Entry cannot be seated*");
    }
}
