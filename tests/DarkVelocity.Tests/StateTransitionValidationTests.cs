using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for validating state transition guards across grains.
/// Ensures invalid state transitions are properly rejected.
/// </summary>
[Collection(ClusterCollection.Name)]
public class StateTransitionValidationTests
{
    private readonly TestClusterFixture _fixture;

    public StateTransitionValidationTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // ============================================================================
    // Booking State Transition Validation Tests
    // ============================================================================

    #region Booking - Confirm Transitions

    [Fact]
    public async Task Booking_Confirm_FromArrived_ShouldThrow()
    {
        // Arrange
        var grain = await CreateConfirmedAndArrivedBookingAsync();

        // Act
        var act = () => grain.ConfirmAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task Booking_Confirm_FromSeated_ShouldThrow()
    {
        // Arrange
        var grain = await CreateSeatedBookingAsync();

        // Act
        var act = () => grain.ConfirmAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task Booking_Confirm_FromCompleted_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCompletedBookingAsync();

        // Act
        var act = () => grain.ConfirmAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task Booking_Confirm_FromCancelled_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCancelledBookingAsync();

        // Act
        var act = () => grain.ConfirmAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task Booking_Confirm_WithRequiredDeposit_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateRequestedBookingAsync(orgId, siteId, bookingId);

        await grain.RequireDepositAsync(new RequireDepositCommand(100m, DateTime.UtcNow.AddDays(1)));

        // Act
        var act = () => grain.ConfirmAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Deposit required but not paid*");
    }

    #endregion

    #region Booking - Modify Transitions

    [Fact]
    public async Task Booking_Modify_FromArrived_ShouldThrow()
    {
        // Arrange
        var grain = await CreateConfirmedAndArrivedBookingAsync();

        // Act
        var act = () => grain.ModifyAsync(new ModifyBookingCommand(NewPartySize: 6));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task Booking_Modify_FromSeated_ShouldThrow()
    {
        // Arrange
        var grain = await CreateSeatedBookingAsync();

        // Act
        var act = () => grain.ModifyAsync(new ModifyBookingCommand(NewPartySize: 6));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task Booking_Modify_FromCompleted_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCompletedBookingAsync();

        // Act
        var act = () => grain.ModifyAsync(new ModifyBookingCommand(NewPartySize: 6));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task Booking_Modify_FromCancelled_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCancelledBookingAsync();

        // Act
        var act = () => grain.ModifyAsync(new ModifyBookingCommand(NewPartySize: 6));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    #endregion

    #region Booking - Cancel Transitions

    [Fact]
    public async Task Booking_Cancel_FromCompleted_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCompletedBookingAsync();

        // Act
        var act = () => grain.CancelAsync(new CancelBookingCommand("Test", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot cancel completed booking*");
    }

    [Fact]
    public async Task Booking_Cancel_WhenAlreadyCancelled_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCancelledBookingAsync();

        // Act
        var act = () => grain.CancelAsync(new CancelBookingCommand("Test again", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already cancelled*");
    }

    #endregion

    #region Booking - Arrival Transitions

    [Fact]
    public async Task Booking_RecordArrival_FromRequested_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateRequestedBookingAsync(orgId, siteId, bookingId);

        // Act
        var act = () => grain.RecordArrivalAsync(new RecordArrivalCommand(Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task Booking_RecordArrival_FromSeated_ShouldThrow()
    {
        // Arrange
        var grain = await CreateSeatedBookingAsync();

        // Act
        var act = () => grain.RecordArrivalAsync(new RecordArrivalCommand(Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task Booking_RecordArrival_FromCancelled_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCancelledBookingAsync();

        // Act
        var act = () => grain.RecordArrivalAsync(new RecordArrivalCommand(Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    #endregion

    #region Booking - Seating Transitions

    [Fact]
    public async Task Booking_SeatGuest_FromRequested_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateRequestedBookingAsync(orgId, siteId, bookingId);

        // Act
        var act = () => grain.SeatGuestAsync(new SeatGuestCommand(Guid.NewGuid(), "T1", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task Booking_SeatGuest_FromCancelled_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCancelledBookingAsync();

        // Act
        var act = () => grain.SeatGuestAsync(new SeatGuestCommand(Guid.NewGuid(), "T1", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task Booking_SeatGuest_FromNoShow_ShouldThrow()
    {
        // Arrange
        var grain = await CreateNoShowBookingAsync();

        // Act
        var act = () => grain.SeatGuestAsync(new SeatGuestCommand(Guid.NewGuid(), "T1", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    #endregion

    #region Booking - Departure Transitions

    [Fact]
    public async Task Booking_RecordDeparture_FromConfirmed_ShouldThrow()
    {
        // Arrange
        var grain = await CreateConfirmedBookingAsync();

        // Act
        var act = () => grain.RecordDepartureAsync(new RecordDepartureCommand(null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task Booking_RecordDeparture_FromArrived_ShouldThrow()
    {
        // Arrange
        var grain = await CreateConfirmedAndArrivedBookingAsync();

        // Act
        var act = () => grain.RecordDepartureAsync(new RecordDepartureCommand(null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task Booking_RecordDeparture_FromRequested_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateRequestedBookingAsync(orgId, siteId, bookingId);

        // Act
        var act = () => grain.RecordDepartureAsync(new RecordDepartureCommand(null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    #endregion

    #region Booking - No Show Transitions

    [Fact]
    public async Task Booking_MarkNoShow_FromRequested_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateRequestedBookingAsync(orgId, siteId, bookingId);

        // Act
        var act = () => grain.MarkNoShowAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task Booking_MarkNoShow_FromSeated_ShouldThrow()
    {
        // Arrange
        var grain = await CreateSeatedBookingAsync();

        // Act
        var act = () => grain.MarkNoShowAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    #endregion

    #region Booking - Deposit Transitions

    [Fact]
    public async Task Booking_RequireDeposit_FromConfirmed_ShouldThrow()
    {
        // Arrange
        var grain = await CreateConfirmedBookingAsync();

        // Act
        var act = () => grain.RequireDepositAsync(new RequireDepositCommand(100m, DateTime.UtcNow.AddDays(1)));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task Booking_RecordDepositPayment_WithoutDeposit_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateRequestedBookingAsync(orgId, siteId, bookingId);

        // Act
        var act = () => grain.RecordDepositPaymentAsync(new RecordDepositPaymentCommand(Guid.NewGuid(), "ref123"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No deposit required*");
    }

    #endregion

    // ============================================================================
    // Order State Transition Validation Tests
    // ============================================================================

    #region Order - Line Operations on Closed/Voided Orders

    [Fact]
    public async Task Order_AddLine_WhenClosed_ShouldThrow()
    {
        // Arrange
        var grain = await CreateClosedOrderAsync();

        // Act
        var act = () => grain.AddLineAsync(new AddLineCommand(
            Guid.NewGuid(), "Burger", 1, 10.00m));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    [Fact]
    public async Task Order_AddLine_WhenVoided_ShouldThrow()
    {
        // Arrange
        var grain = await CreateVoidedOrderAsync();

        // Act
        var act = () => grain.AddLineAsync(new AddLineCommand(
            Guid.NewGuid(), "Burger", 1, 10.00m));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    [Fact]
    public async Task Order_UpdateLine_WhenClosed_ShouldThrow()
    {
        // Arrange
        var (grain, lineId) = await CreateClosedOrderWithLineAsync();

        // Act
        var act = () => grain.UpdateLineAsync(new UpdateLineCommand(lineId, Quantity: 5));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    [Fact]
    public async Task Order_VoidLine_WhenClosed_ShouldThrow()
    {
        // Arrange
        var (grain, lineId) = await CreateClosedOrderWithLineAsync();

        // Act
        var act = () => grain.VoidLineAsync(new VoidLineCommand(lineId, Guid.NewGuid(), "Test"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    [Fact]
    public async Task Order_RemoveLine_WhenVoided_ShouldThrow()
    {
        // Arrange
        var (grain, lineId) = await CreateVoidedOrderWithLineAsync();

        // Act
        var act = () => grain.RemoveLineAsync(lineId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    #endregion

    #region Order - Send Operations

    [Fact]
    public async Task Order_Send_WithoutItems_ShouldThrow()
    {
        // Arrange
        var grain = await CreateOpenOrderAsync();

        // Act
        var act = () => grain.SendAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No pending items*");
    }

    [Fact]
    public async Task Order_Send_WhenClosed_ShouldThrow()
    {
        // Arrange
        var grain = await CreateClosedOrderAsync();

        // Act
        var act = () => grain.SendAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    #endregion

    #region Order - Close Operations

    [Fact]
    public async Task Order_Close_WithOutstandingBalance_ShouldThrow()
    {
        // Arrange
        var grain = await CreateSentOrderWithBalanceAsync();

        // Act
        var act = () => grain.CloseAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*outstanding balance*");
    }

    #endregion

    #region Order - Void Operations

    [Fact]
    public async Task Order_Void_WhenAlreadyVoided_ShouldThrow()
    {
        // Arrange
        var grain = await CreateVoidedOrderAsync();

        // Act
        var act = () => grain.VoidAsync(Guid.NewGuid(), "Test");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    [Fact]
    public async Task Order_Void_WhenClosed_ShouldThrow()
    {
        // Arrange
        var grain = await CreateClosedOrderAsync();

        // Act
        var act = () => grain.VoidAsync(Guid.NewGuid(), "Test");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    #endregion

    #region Order - Reopen Operations

    [Fact]
    public async Task Order_Reopen_WhenOpen_ShouldThrow()
    {
        // Arrange
        var grain = await CreateOpenOrderAsync();

        // Act
        var act = () => grain.ReopenAsync(Guid.NewGuid(), "Test");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only reopen closed or voided*");
    }

    [Fact]
    public async Task Order_Reopen_WhenSent_ShouldThrow()
    {
        // Arrange
        var grain = await CreateSentOrderAsync();

        // Act
        var act = () => grain.ReopenAsync(Guid.NewGuid(), "Test");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only reopen closed or voided*");
    }

    #endregion

    #region Order - Discount Operations

    [Fact]
    public async Task Order_ApplyDiscount_WhenClosed_ShouldThrow()
    {
        // Arrange
        var grain = await CreateClosedOrderAsync();

        // Act
        var act = () => grain.ApplyDiscountAsync(new ApplyDiscountCommand(
            DiscountType.Percentage, 10m, "Test discount", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    #endregion

    // ============================================================================
    // Payment State Transition Validation Tests
    // ============================================================================

    #region Payment - Authorization Flow

    [Fact]
    public async Task Payment_RequestAuthorization_WhenCompleted_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCompletedCashPaymentAsync();

        // Act
        var act = () => grain.RequestAuthorizationAsync(new RequestAuthorizationCommand(
            "4111111111111111", "12/25", "123"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*Initiated*");
    }

    [Fact]
    public async Task Payment_RecordAuthorization_WhenNotAuthorizing_ShouldThrow()
    {
        // Arrange
        var grain = await CreateInitiatedPaymentAsync();

        // Act
        var act = () => grain.RecordAuthorizationAsync(new RecordAuthorizationCommand(
            "auth123", "ref456", "fingerprint"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*Authorizing*");
    }

    [Fact]
    public async Task Payment_Capture_WhenNotAuthorized_ShouldThrow()
    {
        // Arrange
        var grain = await CreateInitiatedPaymentAsync();

        // Act
        var act = () => grain.CaptureAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*Authorized*");
    }

    #endregion

    #region Payment - Completion Flow

    [Fact]
    public async Task Payment_CompleteCash_WhenCompleted_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCompletedCashPaymentAsync();

        // Act
        var act = () => grain.CompleteCashAsync(new CompleteCashCommand(50m, 5m));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*Initiated*");
    }

    [Fact]
    public async Task Payment_CompleteCard_WhenVoided_ShouldThrow()
    {
        // Arrange
        var grain = await CreateVoidedPaymentAsync();

        // Act
        var act = () => grain.CompleteCardAsync(new CompleteCardCommand(
            "ref123", "auth456", new CardInfo("1234", "Visa", 12, 2025), "Stripe", 0));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status for card completion*");
    }

    #endregion

    #region Payment - Refund Flow

    [Fact]
    public async Task Payment_Refund_WhenNotCompleted_ShouldThrow()
    {
        // Arrange
        var grain = await CreateInitiatedPaymentAsync();

        // Act
        var act = () => grain.RefundAsync(new RefundCommand(10m, "Test refund", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only refund completed payments*");
    }

    [Fact]
    public async Task Payment_Refund_ExceedsBalance_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCompletedCashPaymentAsync();
        var state = await grain.GetStateAsync();

        // Act - try to refund more than payment amount
        var act = () => grain.RefundAsync(new RefundCommand(
            state.Amount + 100m, "Test refund", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds available balance*");
    }

    [Fact]
    public async Task Payment_Refund_WhenAlreadyFullyRefunded_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCompletedCashPaymentAsync();
        var state = await grain.GetStateAsync();

        // First refund - full amount
        await grain.RefundAsync(new RefundCommand(state.Amount, "Full refund", Guid.NewGuid()));

        // Act - try to refund again
        var act = () => grain.RefundAsync(new RefundCommand(10m, "Another refund", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only refund completed payments*");
    }

    #endregion

    #region Payment - Void Flow

    [Fact]
    public async Task Payment_Void_WhenAlreadyVoided_ShouldThrow()
    {
        // Arrange
        var grain = await CreateVoidedPaymentAsync();

        // Act
        var act = () => grain.VoidAsync(Guid.NewGuid(), "Test");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot void payment with status*");
    }

    [Fact]
    public async Task Payment_Void_WhenFullyRefunded_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCompletedCashPaymentAsync();
        var state = await grain.GetStateAsync();
        await grain.RefundAsync(new RefundCommand(state.Amount, "Full refund", Guid.NewGuid()));

        // Act
        var act = () => grain.VoidAsync(Guid.NewGuid(), "Test");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot void payment with status*");
    }

    #endregion

    #region Payment - Tip Adjustment

    [Fact]
    public async Task Payment_AdjustTip_WhenNotCompleted_ShouldThrow()
    {
        // Arrange
        var grain = await CreateInitiatedPaymentAsync();

        // Act
        var act = () => grain.AdjustTipAsync(5m);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only adjust tip on completed payments*");
    }

    #endregion

    // ============================================================================
    // Helper Methods - Booking
    // ============================================================================

    private GuestInfo CreateGuestInfo() => new()
    {
        Name = "Test Guest",
        Phone = "+1234567890",
        Email = "test@example.com"
    };

    private async Task<IBookingGrain> CreateRequestedBookingAsync(Guid orgId, Guid siteId, Guid bookingId)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingGrain>(
            GrainKeys.Booking(orgId, siteId, bookingId));

        await grain.RequestAsync(new RequestBookingCommand(
            orgId, siteId, CreateGuestInfo(), DateTime.UtcNow.AddDays(1), 4));

        return grain;
    }

    private async Task<IBookingGrain> CreateConfirmedBookingAsync()
    {
        var grain = await CreateRequestedBookingAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await grain.ConfirmAsync();
        return grain;
    }

    private async Task<IBookingGrain> CreateConfirmedAndArrivedBookingAsync()
    {
        var grain = await CreateConfirmedBookingAsync();
        await grain.RecordArrivalAsync(new RecordArrivalCommand(Guid.NewGuid()));
        return grain;
    }

    private async Task<IBookingGrain> CreateSeatedBookingAsync()
    {
        var grain = await CreateConfirmedAndArrivedBookingAsync();
        await grain.SeatGuestAsync(new SeatGuestCommand(Guid.NewGuid(), "T1", Guid.NewGuid()));
        return grain;
    }

    private async Task<IBookingGrain> CreateCompletedBookingAsync()
    {
        var grain = await CreateSeatedBookingAsync();
        await grain.RecordDepartureAsync(new RecordDepartureCommand(null));
        return grain;
    }

    private async Task<IBookingGrain> CreateCancelledBookingAsync()
    {
        var grain = await CreateRequestedBookingAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await grain.CancelAsync(new CancelBookingCommand("Test cancellation", Guid.NewGuid()));
        return grain;
    }

    private async Task<IBookingGrain> CreateNoShowBookingAsync()
    {
        var grain = await CreateConfirmedBookingAsync();
        await grain.MarkNoShowAsync();
        return grain;
    }

    // ============================================================================
    // Helper Methods - Order
    // ============================================================================

    private async Task<IOrderGrain> CreateOpenOrderAsync()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(orgId, siteId, orderId));

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, Guid.NewGuid(), OrderType.DineIn, GuestCount: 2));
        return grain;
    }

    private async Task<IOrderGrain> CreateSentOrderAsync()
    {
        var grain = await CreateOpenOrderAsync();
        await grain.AddLineAsync(new AddLineCommand(
            Guid.NewGuid(), "Burger", 1, 10.00m));
        await grain.SendAsync(Guid.NewGuid());
        return grain;
    }

    private async Task<IOrderGrain> CreateSentOrderWithBalanceAsync()
    {
        return await CreateSentOrderAsync();
    }

    private async Task<IOrderGrain> CreateClosedOrderAsync()
    {
        var grain = await CreateSentOrderAsync();
        var state = await grain.GetStateAsync();

        // Record payment to clear balance
        await grain.RecordPaymentAsync(Guid.NewGuid(), state.GrandTotal, 0, "Cash");

        await grain.CloseAsync(Guid.NewGuid());
        return grain;
    }

    private async Task<(IOrderGrain, Guid)> CreateClosedOrderWithLineAsync()
    {
        var grain = await CreateOpenOrderAsync();
        var menuItemId = Guid.NewGuid();

        var addResult = await grain.AddLineAsync(new AddLineCommand(
            menuItemId, "Burger", 1, 10.00m));
        await grain.SendAsync(Guid.NewGuid());

        var state = await grain.GetStateAsync();
        await grain.RecordPaymentAsync(Guid.NewGuid(), state.GrandTotal, 0, "Cash");
        await grain.CloseAsync(Guid.NewGuid());

        return (grain, addResult.LineId);
    }

    private async Task<IOrderGrain> CreateVoidedOrderAsync()
    {
        var grain = await CreateOpenOrderAsync();
        await grain.VoidAsync(new VoidOrderCommand(Guid.NewGuid(), "Test void"));
        return grain;
    }

    private async Task<(IOrderGrain, Guid)> CreateVoidedOrderWithLineAsync()
    {
        var grain = await CreateOpenOrderAsync();

        var addResult = await grain.AddLineAsync(new AddLineCommand(
            Guid.NewGuid(), "Burger", 1, 10.00m));
        await grain.VoidAsync(new VoidOrderCommand(Guid.NewGuid(), "Test void"));

        return (grain, addResult.LineId);
    }

    // ============================================================================
    // Helper Methods - Payment
    // ============================================================================

    private async Task<IPaymentGrain> CreateInitiatedPaymentAsync()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(
            GrainKeys.Payment(orgId, siteId, paymentId));

        await grain.InitiateAsync(new InitiatePaymentCommand(
            Guid.NewGuid(), PaymentMethod.Cash, 25.00m, Guid.NewGuid()));

        return grain;
    }

    private async Task<IPaymentGrain> CreateCompletedCashPaymentAsync()
    {
        var grain = await CreateInitiatedPaymentAsync();
        await grain.CompleteCashAsync(new CompleteCashCommand(30.00m, 0));
        return grain;
    }

    private async Task<IPaymentGrain> CreateVoidedPaymentAsync()
    {
        var grain = await CreateInitiatedPaymentAsync();
        await grain.VoidAsync(Guid.NewGuid(), "Test void");
        return grain;
    }
}
