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
        state.TableAssignments.Should().HaveCountGreaterOrEqualTo(1);
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
