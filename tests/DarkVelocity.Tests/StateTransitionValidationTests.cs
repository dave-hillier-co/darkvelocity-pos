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
[Trait("Category", "Integration")]
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

    // Given: A booking that has been confirmed and the guest has arrived
    // When: Attempting to confirm the booking again
    // Then: The confirmation is rejected because an arrived booking cannot be re-confirmed
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

    // Given: A booking where the guest has been seated at a table
    // When: Attempting to confirm the already-seated booking
    // Then: The confirmation is rejected because a seated booking cannot be re-confirmed
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

    // Given: A booking that has been completed (guest departed)
    // When: Attempting to confirm the completed booking
    // Then: The confirmation is rejected because completed bookings are immutable
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

    // Given: A booking that has been cancelled
    // When: Attempting to confirm the cancelled booking
    // Then: The confirmation is rejected because cancelled bookings cannot be confirmed
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

    // Given: A requested booking with a required deposit that has not been paid
    // When: Attempting to confirm the booking without deposit payment
    // Then: The confirmation is rejected because the deposit has not been received
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

    // Given: A booking where the guest has already arrived
    // When: Attempting to modify the party size after arrival
    // Then: The modification is rejected because arrived bookings cannot be modified
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

    // Given: A booking where the guest has been seated
    // When: Attempting to modify the party size while guests are seated
    // Then: The modification is rejected because seated bookings cannot be modified
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

    // Given: A completed booking (guest has departed)
    // When: Attempting to modify the party size after completion
    // Then: The modification is rejected because completed bookings are immutable
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

    // Given: A cancelled booking
    // When: Attempting to modify the party size on the cancelled booking
    // Then: The modification is rejected because cancelled bookings cannot be modified
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

    // Given: A completed booking where the guest has departed
    // When: Attempting to cancel the completed booking
    // Then: The cancellation is rejected because completed bookings cannot be cancelled
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

    // Given: An already cancelled booking
    // When: Attempting to cancel the booking a second time
    // Then: The duplicate cancellation is rejected
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

    // Given: A booking still in Requested status (not yet confirmed)
    // When: Attempting to record guest arrival before confirmation
    // Then: The arrival is rejected because only confirmed bookings can receive arrivals
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

    // Given: A booking where guests are already seated
    // When: Attempting to record arrival again after seating
    // Then: The arrival is rejected because the guest has already been seated
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

    // Given: A cancelled booking
    // When: Attempting to record guest arrival on a cancelled booking
    // Then: The arrival is rejected because cancelled bookings cannot receive arrivals
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

    // Given: A booking still in Requested status (not yet confirmed or arrived)
    // When: Attempting to seat the guest directly from Requested
    // Then: The seating is rejected because guests must arrive before being seated
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

    // Given: A cancelled booking
    // When: Attempting to seat guests from a cancelled booking
    // Then: The seating is rejected because cancelled bookings cannot proceed
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

    // Given: A booking marked as a no-show
    // When: Attempting to seat guests from a no-show booking
    // Then: The seating is rejected because no-show bookings cannot be seated
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

    // Given: A confirmed booking where guests have not yet been seated
    // When: Attempting to record departure before seating
    // Then: The departure is rejected because guests must be seated before departing
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

    // Given: A booking where the guest has arrived but has not been seated
    // When: Attempting to record departure without seating
    // Then: The departure is rejected because guests must be seated before departing
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

    // Given: A booking still in Requested status
    // When: Attempting to record departure from an unconfirmed booking
    // Then: The departure is rejected because the booking has not progressed through the lifecycle
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

    // Given: A booking still in Requested status (not yet confirmed)
    // When: Attempting to mark the booking as a no-show
    // Then: The no-show is rejected because only confirmed bookings can be marked as no-show
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

    // Given: A booking where the guest is currently seated
    // When: Attempting to mark a seated booking as a no-show
    // Then: The no-show is rejected because the guest is present and seated
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

    // Given: An already confirmed booking
    // When: Attempting to require a deposit after the booking has been confirmed
    // Then: The deposit requirement is rejected because deposits must be set before confirmation
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

    // Given: A requested booking with no deposit requirement set
    // When: Attempting to record a deposit payment
    // Then: The payment is rejected because no deposit was required for this booking
    [Fact]
    public async Task Booking_RecordDepositPayment_WithoutDeposit_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateRequestedBookingAsync(orgId, siteId, bookingId);

        // Act
        var act = () => grain.RecordDepositPaymentAsync(new RecordDepositPaymentCommand(PaymentMethod.CreditCard, "ref123"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No deposit required*");
    }

    #endregion

    // ============================================================================
    // Order State Transition Validation Tests
    // ============================================================================

    #region Order - Line Operations on Closed/Voided Orders

    // Given: A closed order that has been fully settled
    // When: Attempting to add a new line item to the closed order
    // Then: The line add is rejected because closed orders cannot accept new items
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

    // Given: A voided order
    // When: Attempting to add a new line item to the voided order
    // Then: The line add is rejected because voided orders cannot accept new items
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

    // Given: A closed order with an existing line item
    // When: Attempting to update the line item quantity on the closed order
    // Then: The update is rejected because closed orders are immutable
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

    // Given: A closed order with an existing line item
    // When: Attempting to void a line item on the closed order
    // Then: The void is rejected because closed orders are immutable
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

    // Given: A voided order with an existing line item
    // When: Attempting to remove a line item from the voided order
    // Then: The removal is rejected because voided orders are immutable
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

    // Given: An open order with no line items added
    // When: Attempting to send the empty order to the kitchen
    // Then: The send is rejected because there are no pending items to prepare
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

    // Given: A closed order
    // When: Attempting to send the closed order to the kitchen
    // Then: The send is rejected because closed orders cannot be sent
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

    // Given: A sent order with unpaid items and an outstanding balance
    // When: Attempting to close the order before full payment
    // Then: The close is rejected because there is an outstanding balance
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

    // Given: An already voided order
    // When: Attempting to void the order a second time
    // Then: The duplicate void is rejected
    [Fact]
    public async Task Order_Void_WhenAlreadyVoided_ShouldThrow()
    {
        // Arrange
        var grain = await CreateVoidedOrderAsync();

        // Act
        var act = () => grain.VoidAsync(new VoidOrderCommand(Guid.NewGuid(), "Test"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    // Given: A closed and settled order
    // When: Attempting to void the closed order
    // Then: The void is rejected because closed orders cannot be voided
    [Fact]
    public async Task Order_Void_WhenClosed_ShouldThrow()
    {
        // Arrange
        var grain = await CreateClosedOrderAsync();

        // Act
        var act = () => grain.VoidAsync(new VoidOrderCommand(Guid.NewGuid(), "Test"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    #endregion

    #region Order - Reopen Operations

    // Given: An order that is currently open
    // When: Attempting to reopen an already-open order
    // Then: The reopen is rejected because only closed or voided orders can be reopened
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

    // Given: An order that has been sent to the kitchen
    // When: Attempting to reopen a sent order
    // Then: The reopen is rejected because only closed or voided orders can be reopened
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

    // Given: A closed and settled order
    // When: Attempting to apply a 10% discount to the closed order
    // Then: The discount is rejected because closed orders cannot accept discounts
    [Fact]
    public async Task Order_ApplyDiscount_WhenClosed_ShouldThrow()
    {
        // Arrange
        var grain = await CreateClosedOrderAsync();

        // Act
        var act = () => grain.ApplyDiscountAsync(new ApplyDiscountCommand(
            "Test discount", DiscountType.Percentage, 10m, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed or voided*");
    }

    #endregion

    // ============================================================================
    // Payment State Transition Validation Tests
    // ============================================================================

    #region Payment - Authorization Flow

    // Given: A completed cash payment
    // When: Attempting to request card authorization on an already completed payment
    // Then: The authorization request is rejected because the payment must be in Initiated status
    [Fact]
    public async Task Payment_RequestAuthorization_WhenCompleted_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCompletedCashPaymentAsync();

        // Act
        var act = () => grain.RequestAuthorizationAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*Initiated*");
    }

    // Given: A payment in Initiated status (authorization not yet requested)
    // When: Attempting to record an authorization result without requesting it first
    // Then: The authorization is rejected because the payment must be in Authorizing status
    [Fact]
    public async Task Payment_RecordAuthorization_WhenNotAuthorizing_ShouldThrow()
    {
        // Arrange
        var grain = await CreateInitiatedPaymentAsync();

        // Act
        var act = () => grain.RecordAuthorizationAsync(
            "auth123", "ref456", new CardInfo { MaskedNumber = "****1234", Brand = "Visa" });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*Authorizing*");
    }

    // Given: A payment in Initiated status (not yet authorized)
    // When: Attempting to capture a payment that has not been authorized
    // Then: The capture is rejected because the payment must be in Authorized status
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

    // Given: An already completed cash payment
    // When: Attempting to complete the cash payment a second time
    // Then: The completion is rejected because the payment must be in Initiated status
    [Fact]
    public async Task Payment_CompleteCash_WhenCompleted_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCompletedCashPaymentAsync();

        // Act
        var act = () => grain.CompleteCashAsync(new CompleteCashPaymentCommand(50m, 5m));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*Initiated*");
    }

    // Given: A voided payment
    // When: Attempting to complete a card payment on the voided payment
    // Then: The card completion is rejected because voided payments cannot be completed
    [Fact]
    public async Task Payment_CompleteCard_WhenVoided_ShouldThrow()
    {
        // Arrange
        var grain = await CreateVoidedPaymentAsync();

        // Act
        var act = () => grain.CompleteCardAsync(new ProcessCardPaymentCommand(
            "ref123", "auth456", new CardInfo { MaskedNumber = "****1234", Brand = "Visa", ExpiryMonth = "12", ExpiryYear = "2025" }, "Stripe", 0));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status for card completion*");
    }

    #endregion

    #region Payment - Refund Flow

    // Given: A payment in Initiated status (not yet completed)
    // When: Attempting to refund the payment before it has been completed
    // Then: The refund is rejected because only completed payments can be refunded
    [Fact]
    public async Task Payment_Refund_WhenNotCompleted_ShouldThrow()
    {
        // Arrange
        var grain = await CreateInitiatedPaymentAsync();

        // Act
        var act = () => grain.RefundAsync(new RefundPaymentCommand(10m, "Test refund", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only refund completed payments*");
    }

    // Given: A completed cash payment
    // When: Attempting to refund more than the original payment amount
    // Then: The refund is rejected because the refund exceeds the available balance
    [Fact]
    public async Task Payment_Refund_ExceedsBalance_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCompletedCashPaymentAsync();
        var state = await grain.GetStateAsync();

        // Act - try to refund more than payment amount
        var act = () => grain.RefundAsync(new RefundPaymentCommand(
            state.Amount + 100m, "Test refund", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds available balance*");
    }

    // Given: A completed cash payment that has already been fully refunded
    // When: Attempting to issue another refund on the fully refunded payment
    // Then: The refund is rejected because the payment is no longer in Completed status
    [Fact]
    public async Task Payment_Refund_WhenAlreadyFullyRefunded_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCompletedCashPaymentAsync();
        var state = await grain.GetStateAsync();

        // First refund - full amount
        await grain.RefundAsync(new RefundPaymentCommand(state.Amount, "Full refund", Guid.NewGuid()));

        // Act - try to refund again
        var act = () => grain.RefundAsync(new RefundPaymentCommand(10m, "Another refund", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only refund completed payments*");
    }

    #endregion

    #region Payment - Void Flow

    // Given: An already voided payment
    // When: Attempting to void the payment a second time
    // Then: The duplicate void is rejected
    [Fact]
    public async Task Payment_Void_WhenAlreadyVoided_ShouldThrow()
    {
        // Arrange
        var grain = await CreateVoidedPaymentAsync();

        // Act
        var act = () => grain.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Test"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot void payment with status*");
    }

    // Given: A fully refunded payment
    // When: Attempting to void the refunded payment
    // Then: The void is rejected because fully refunded payments cannot be voided
    [Fact]
    public async Task Payment_Void_WhenFullyRefunded_ShouldThrow()
    {
        // Arrange
        var grain = await CreateCompletedCashPaymentAsync();
        var state = await grain.GetStateAsync();
        await grain.RefundAsync(new RefundPaymentCommand(state.Amount, "Full refund", Guid.NewGuid()));

        // Act
        var act = () => grain.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Test"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot void payment with status*");
    }

    #endregion

    #region Payment - Tip Adjustment

    // Given: A payment in Initiated status (not yet completed)
    // When: Attempting to adjust the tip before the payment is completed
    // Then: The tip adjustment is rejected because only completed payments can have tips adjusted
    [Fact]
    public async Task Payment_AdjustTip_WhenNotCompleted_ShouldThrow()
    {
        // Arrange
        var grain = await CreateInitiatedPaymentAsync();

        // Act
        var act = () => grain.AdjustTipAsync(new AdjustTipCommand(5m, Guid.NewGuid()));

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
            orgId, siteId, Guid.NewGuid(), PaymentMethod.Cash, 25.00m, Guid.NewGuid()));

        return grain;
    }

    private async Task<IPaymentGrain> CreateCompletedCashPaymentAsync()
    {
        var grain = await CreateInitiatedPaymentAsync();
        await grain.CompleteCashAsync(new CompleteCashPaymentCommand(30.00m, 0));
        return grain;
    }

    private async Task<IPaymentGrain> CreateVoidedPaymentAsync()
    {
        var grain = await CreateInitiatedPaymentAsync();
        await grain.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Test void"));
        return grain;
    }
}
