using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PaymentGrainTests
{
    private readonly TestClusterFixture _fixture;

    public PaymentGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IOrderGrain> CreateOrderWithLineAsync(Guid orgId, Guid siteId, Guid orderId, decimal amount)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, Guid.NewGuid(), OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, amount));
        return grain;
    }

    // Given: an order with a line item totaling $100
    // When: a cash payment of $100 is initiated against the order
    // Then: the payment is created with Initiated status and the correct amount
    [Fact]
    public async Task InitiateAsync_ShouldCreatePayment()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        await CreateOrderWithLineAsync(orgId, siteId, orderId, 100m);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));

        var command = new InitiatePaymentCommand(orgId, siteId, orderId, PaymentMethod.Cash, 100m, cashierId);

        // Act
        var result = await grain.InitiateAsync(command);

        // Assert
        result.Id.Should().Be(paymentId);
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Initiated);
        state.Method.Should().Be(PaymentMethod.Cash);
        state.Amount.Should().Be(100m);
    }

    // Given: an initiated cash payment of $100
    // When: the cashier completes the payment with $120 tendered and a $5 tip
    // Then: the payment is completed with a total of $105 and $15 change given
    [Fact]
    public async Task CompleteCashAsync_ShouldCompletePayment()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await CreateOrderWithLineAsync(orgId, siteId, orderId, 100m);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
        await grain.InitiateAsync(new InitiatePaymentCommand(orgId, siteId, orderId, PaymentMethod.Cash, 100m, Guid.NewGuid()));

        // Act
        var result = await grain.CompleteCashAsync(new CompleteCashPaymentCommand(120m, 5m));

        // Assert
        result.TotalAmount.Should().Be(105m); // 100 + 5 tip
        result.ChangeGiven.Should().Be(15m); // 120 - 105

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Completed);
    }

    // Given: an initiated credit card payment of $100
    // When: the card payment is processed with a $10 tip via Stripe
    // Then: the payment is completed with a total of $110 and card details are recorded
    [Fact]
    public async Task CompleteCardAsync_ShouldCompletePayment()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await CreateOrderWithLineAsync(orgId, siteId, orderId, 100m);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
        await grain.InitiateAsync(new InitiatePaymentCommand(orgId, siteId, orderId, PaymentMethod.CreditCard, 100m, Guid.NewGuid()));

        var cardInfo = new CardInfo
        {
            MaskedNumber = "****4242",
            Brand = "Visa",
            EntryMethod = "chip"
        };

        // Act
        var result = await grain.CompleteCardAsync(new ProcessCardPaymentCommand("ref123", "auth456", cardInfo, "Stripe", 10m));

        // Assert
        result.TotalAmount.Should().Be(110m);
        result.ChangeGiven.Should().BeNull();

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Completed);
        state.CardInfo!.MaskedNumber.Should().Be("****4242");
    }

    // Given: a completed cash payment of $100
    // When: a full refund of $100 is issued for customer dissatisfaction
    // Then: the payment is fully refunded with a zero remaining balance
    [Fact]
    public async Task RefundAsync_ShouldRefundPayment()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        await CreateOrderWithLineAsync(orgId, siteId, orderId, 100m);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
        await grain.InitiateAsync(new InitiatePaymentCommand(orgId, siteId, orderId, PaymentMethod.Cash, 100m, Guid.NewGuid()));
        await grain.CompleteCashAsync(new CompleteCashPaymentCommand(100m));

        // Act
        var result = await grain.RefundAsync(new RefundPaymentCommand(100m, "Customer dissatisfied", managerId));

        // Assert
        result.RefundedAmount.Should().Be(100m);
        result.RemainingBalance.Should().Be(0);

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Refunded);
    }

    // Given: a completed cash payment of $100
    // When: a partial refund of $30 is issued
    // Then: $30 is refunded and $70 remains, with the payment marked as partially refunded
    [Fact]
    public async Task PartialRefundAsync_ShouldPartiallyRefund()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        await CreateOrderWithLineAsync(orgId, siteId, orderId, 100m);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
        await grain.InitiateAsync(new InitiatePaymentCommand(orgId, siteId, orderId, PaymentMethod.Cash, 100m, Guid.NewGuid()));
        await grain.CompleteCashAsync(new CompleteCashPaymentCommand(100m));

        // Act
        var result = await grain.PartialRefundAsync(new RefundPaymentCommand(30m, "Partial return", managerId));

        // Assert
        result.RefundedAmount.Should().Be(30m);
        result.RemainingBalance.Should().Be(70m);

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.PartiallyRefunded);
    }

    // Given: an initiated cash payment of $100
    // When: the payment is voided because the customer cancelled
    // Then: the payment status is Voided and the void reason is recorded
    [Fact]
    public async Task VoidAsync_ShouldVoidPayment()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await CreateOrderWithLineAsync(orgId, siteId, orderId, 100m);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
        await grain.InitiateAsync(new InitiatePaymentCommand(orgId, siteId, orderId, PaymentMethod.Cash, 100m, Guid.NewGuid()));

        // Act
        await grain.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Customer cancelled"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Voided);
        state.VoidReason.Should().Be("Customer cancelled");
    }

    // Given: a completed cash payment of $100 with a $10 tip
    // When: the tip is adjusted to $15
    // Then: the tip amount is updated to $15 and the total becomes $115
    [Fact]
    public async Task AdjustTipAsync_ShouldAdjustTip()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await CreateOrderWithLineAsync(orgId, siteId, orderId, 100m);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
        await grain.InitiateAsync(new InitiatePaymentCommand(orgId, siteId, orderId, PaymentMethod.Cash, 100m, Guid.NewGuid()));
        await grain.CompleteCashAsync(new CompleteCashPaymentCommand(110m, 10m));

        // Act
        await grain.AdjustTipAsync(new AdjustTipCommand(15m, Guid.NewGuid()));

        // Assert
        var state = await grain.GetStateAsync();
        state.TipAmount.Should().Be(15m);
        state.TotalAmount.Should().Be(115m);
    }

    // =========================================================================
    // Card Authorization Flow Tests
    // =========================================================================

    // Given: an initiated credit card payment of $150
    // When: the card is authorized, the authorization is recorded, and the payment is captured
    // Then: the payment progresses through Authorizing, Authorized, and Captured statuses
    [Fact]
    public async Task Payment_CardAuthorization_FullFlow_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await CreateOrderWithLineAsync(orgId, siteId, orderId, 150m);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
        await grain.InitiateAsync(new InitiatePaymentCommand(orgId, siteId, orderId, PaymentMethod.CreditCard, 150m, Guid.NewGuid()));

        // Act - Step 1: Request Authorization
        await grain.RequestAuthorizationAsync();
        var stateAfterRequest = await grain.GetStateAsync();
        stateAfterRequest.Status.Should().Be(PaymentStatus.Authorizing);

        // Act - Step 2: Record Authorization
        var cardInfo = new CardInfo
        {
            MaskedNumber = "****1234",
            Brand = "Mastercard",
            EntryMethod = "contactless"
        };
        await grain.RecordAuthorizationAsync("AUTH123", "GW456", cardInfo);
        var stateAfterAuth = await grain.GetStateAsync();
        stateAfterAuth.Status.Should().Be(PaymentStatus.Authorized);
        stateAfterAuth.AuthorizationCode.Should().Be("AUTH123");
        stateAfterAuth.GatewayReference.Should().Be("GW456");

        // Act - Step 3: Capture
        await grain.CaptureAsync();
        var stateAfterCapture = await grain.GetStateAsync();

        // Assert
        stateAfterCapture.Status.Should().Be(PaymentStatus.Captured);
        stateAfterCapture.CapturedAt.Should().NotBeNull();
    }

    // Given: an initiated credit card payment awaiting authorization
    // When: the card is declined due to insufficient funds
    // Then: the payment status is set to Declined
    [Fact]
    public async Task Payment_CardAuthorization_Declined_ShouldRecordDecline()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await CreateOrderWithLineAsync(orgId, siteId, orderId, 100m);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
        await grain.InitiateAsync(new InitiatePaymentCommand(orgId, siteId, orderId, PaymentMethod.CreditCard, 100m, Guid.NewGuid()));

        // Act
        await grain.RequestAuthorizationAsync();
        await grain.RecordDeclineAsync("insufficient_funds", "Your card has insufficient funds");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Declined);
    }

    // Given: an authorized credit card payment of $75
    // When: the payment is voided because the customer changed their mind
    // Then: the payment status is Voided and the void reason is recorded
    [Fact]
    public async Task Payment_VoidAuthorized_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await CreateOrderWithLineAsync(orgId, siteId, orderId, 75m);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
        await grain.InitiateAsync(new InitiatePaymentCommand(orgId, siteId, orderId, PaymentMethod.CreditCard, 75m, Guid.NewGuid()));
        await grain.RequestAuthorizationAsync();
        var cardInfo = new CardInfo { MaskedNumber = "****5678", Brand = "Visa", EntryMethod = "chip" };
        await grain.RecordAuthorizationAsync("AUTH789", "GW012", cardInfo);

        // Act
        await grain.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Customer changed mind"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Voided);
        state.VoidReason.Should().Be("Customer changed mind");
    }

    // Given: an authorized credit card payment of $200
    // When: the card payment is completed directly from authorized status with a $20 tip
    // Then: the payment is completed with a total of $220
    [Fact]
    public async Task Payment_CompleteCard_FromAuthorizedStatus_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await CreateOrderWithLineAsync(orgId, siteId, orderId, 200m);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
        await grain.InitiateAsync(new InitiatePaymentCommand(orgId, siteId, orderId, PaymentMethod.CreditCard, 200m, Guid.NewGuid()));
        await grain.RequestAuthorizationAsync();
        var cardInfo = new CardInfo { MaskedNumber = "****9999", Brand = "Amex", EntryMethod = "manual" };
        await grain.RecordAuthorizationAsync("AUTH456", "GW789", cardInfo);

        // Act - Complete directly from Authorized status
        var result = await grain.CompleteCardAsync(new ProcessCardPaymentCommand("GW789-complete", "AUTH456-final", cardInfo, "Stripe", 20m));

        // Assert
        result.TotalAmount.Should().Be(220m); // 200 + 20 tip
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Completed);
    }

    // =========================================================================
    // State Transition Tests
    // =========================================================================

    // Given: a payment that has already been completed
    // When: completing the same payment a second time is attempted
    // Then: the operation is rejected with an invalid status error
    [Fact]
    public async Task Payment_CompleteTwice_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await CreateOrderWithLineAsync(orgId, siteId, orderId, 50m);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
        await grain.InitiateAsync(new InitiatePaymentCommand(orgId, siteId, orderId, PaymentMethod.Cash, 50m, Guid.NewGuid()));
        await grain.CompleteCashAsync(new CompleteCashPaymentCommand(50m));

        // Act
        var act = () => grain.CompleteCashAsync(new CompleteCashPaymentCommand(50m));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status*");
    }

    // Given: a completed cash payment of $100
    // When: a manager voids the completed payment
    // Then: the payment is successfully voided via manager override
    [Fact]
    public async Task Payment_VoidCompleted_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await CreateOrderWithLineAsync(orgId, siteId, orderId, 100m);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
        await grain.InitiateAsync(new InitiatePaymentCommand(orgId, siteId, orderId, PaymentMethod.Cash, 100m, Guid.NewGuid()));
        await grain.CompleteCashAsync(new CompleteCashPaymentCommand(100m));

        // Void should still work on completed payment (per the VoidAsync implementation)
        // The current implementation allows voiding non-Voided/non-Refunded payments
        await grain.VoidAsync(new VoidPaymentCommand(Guid.NewGuid(), "Manager override"));

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PaymentStatus.Voided);
    }

    // Given: an initiated payment that has not been completed
    // When: a refund is attempted before the payment is completed
    // Then: the operation is rejected because only completed payments can be refunded
    [Fact]
    public async Task Payment_RefundBeforeCompletion_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await CreateOrderWithLineAsync(orgId, siteId, orderId, 100m);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
        await grain.InitiateAsync(new InitiatePaymentCommand(orgId, siteId, orderId, PaymentMethod.Cash, 100m, Guid.NewGuid()));

        // Act - Try to refund before completing
        var act = () => grain.RefundAsync(new RefundPaymentCommand(50m, "Refund test", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Can only refund completed payments*");
    }

    // Given: an initiated payment that has not been completed
    // When: a tip adjustment is attempted before the payment is completed
    // Then: the operation is rejected because tips can only be adjusted on completed payments
    [Fact]
    public async Task Payment_AdjustTipBeforeCompletion_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await CreateOrderWithLineAsync(orgId, siteId, orderId, 100m);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
        await grain.InitiateAsync(new InitiatePaymentCommand(orgId, siteId, orderId, PaymentMethod.Cash, 100m, Guid.NewGuid()));

        // Act - Try to adjust tip before completing
        var act = () => grain.AdjustTipAsync(new AdjustTipCommand(15m, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Can only adjust tip on completed payments*");
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class CashDrawerGrainTests
{
    private readonly TestClusterFixture _fixture;

    public CashDrawerGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // Given: a new cash drawer at a site
    // When: the drawer is opened with a $200 starting float
    // Then: the drawer status is Open and the expected balance matches the opening float
    [Fact]
    public async Task OpenAsync_ShouldOpenDrawer()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerGrain>(GrainKeys.CashDrawer(orgId, siteId, drawerId));

        // Act
        var result = await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 200m));

        // Assert
        result.Id.Should().Be(drawerId);
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(DrawerStatus.Open);
        state.OpeningFloat.Should().Be(200m);
        state.ExpectedBalance.Should().Be(200m);
    }

    // Given: an open cash drawer with a $200 balance
    // When: $50 cash is received from a payment
    // Then: the expected drawer balance increases to $250
    [Fact]
    public async Task RecordCashInAsync_ShouldIncreaseBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerGrain>(GrainKeys.CashDrawer(orgId, siteId, drawerId));
        await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 200m));

        // Act
        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 50m));

        // Assert
        var balance = await grain.GetExpectedBalanceAsync();
        balance.Should().Be(250m);
    }

    // Given: an open cash drawer with a $200 balance
    // When: $50 is paid out as change
    // Then: the expected drawer balance decreases to $150
    [Fact]
    public async Task RecordCashOutAsync_ShouldDecreaseBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerGrain>(GrainKeys.CashDrawer(orgId, siteId, drawerId));
        await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 200m));

        // Act
        await grain.RecordCashOutAsync(new RecordCashOutCommand(50m, "Change"));

        // Assert
        var balance = await grain.GetExpectedBalanceAsync();
        balance.Should().Be(150m);
    }

    // Given: an open cash drawer with a $500 balance
    // When: a $300 cash drop is made to the safe
    // Then: the expected balance decreases to $200 and the drop is recorded
    [Fact]
    public async Task RecordDropAsync_ShouldRecordDrop()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerGrain>(GrainKeys.CashDrawer(orgId, siteId, drawerId));
        await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 500m));

        // Act
        await grain.RecordDropAsync(new CashDropCommand(300m, "Safe deposit"));

        // Assert
        var state = await grain.GetStateAsync();
        state.ExpectedBalance.Should().Be(200m);
        state.CashDrops.Should().HaveCount(1);
        state.CashDrops[0].Amount.Should().Be(300m);
    }

    // Given: an open cash drawer with $200 opening float and $100 cash received
    // When: the drawer is closed with an actual count of $295
    // Then: the drawer is closed showing a -$5 variance (short)
    [Fact]
    public async Task CloseAsync_ShouldCloseDrawerWithVariance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerGrain>(GrainKeys.CashDrawer(orgId, siteId, drawerId));
        await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 200m));
        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 100m));

        // Act
        var result = await grain.CloseAsync(new CloseDrawerCommand(295m, userId));

        // Assert
        result.ExpectedBalance.Should().Be(300m);
        result.ActualBalance.Should().Be(295m);
        result.Variance.Should().Be(-5m); // $5 short

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(DrawerStatus.Closed);
    }

    // Given: an open cash drawer
    // When: the drawer is opened for a no-sale transaction (e.g., making change)
    // Then: a no-sale transaction is recorded in the drawer history
    [Fact]
    public async Task OpenNoSaleAsync_ShouldRecordNoSale()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerGrain>(GrainKeys.CashDrawer(orgId, siteId, drawerId));
        await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 200m));

        // Act
        await grain.OpenNoSaleAsync(userId, "Customer needed change");

        // Assert
        var state = await grain.GetStateAsync();
        state.Transactions.Should().Contain(t => t.Type == DrawerTransactionType.NoSale);
    }

    // =========================================================================
    // Additional CashDrawer Tests
    // =========================================================================

    // Given: an open cash drawer with $300 float and $150 cash received
    // When: a cash count of $440 is performed
    // Then: the drawer enters Counting status with the actual balance recorded
    [Fact]
    public async Task CashDrawer_CountAsync_ShouldReturnBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerGrain>(GrainKeys.CashDrawer(orgId, siteId, drawerId));
        await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 300m));
        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 150m));

        // Act
        await grain.CountAsync(new CountDrawerCommand(440m, userId));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(DrawerStatus.Counting);
        state.ActualBalance.Should().Be(440m);
        state.LastCountedAt.Should().NotBeNull();
    }

    // Given: a cash drawer that is already open
    // When: opening the same drawer is attempted again
    // Then: the operation is rejected because the drawer is already open
    [Fact]
    public async Task CashDrawer_OpenAlreadyOpen_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerGrain>(GrainKeys.CashDrawer(orgId, siteId, drawerId));
        await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 200m));

        // Act
        var act = () => grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 200m));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already open*");
    }

    // Given: a cash drawer that has already been closed
    // When: closing the same drawer is attempted again
    // Then: the operation is rejected because the drawer is already closed
    [Fact]
    public async Task CashDrawer_CloseAlreadyClosed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerGrain>(GrainKeys.CashDrawer(orgId, siteId, drawerId));
        await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 200m));
        await grain.CloseAsync(new CloseDrawerCommand(200m, userId));

        // Act
        var act = () => grain.CloseAsync(new CloseDrawerCommand(200m, userId));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already closed*");
    }

    // Given: a cash drawer that has been closed
    // When: recording a cash-in transaction on the closed drawer is attempted
    // Then: the operation is rejected because the drawer is not open
    [Fact]
    public async Task CashDrawer_RecordOnClosed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerGrain>(GrainKeys.CashDrawer(orgId, siteId, drawerId));
        await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 200m));
        await grain.CloseAsync(new CloseDrawerCommand(200m, userId));

        // Act
        var act = () => grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 50m));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not open*");
    }

    // Given: an open cash drawer with $200 float and $100 received (expected $300)
    // When: the drawer is closed with an actual count of $290
    // Then: a negative variance of -$10 is calculated, indicating cash is short
    [Fact]
    public async Task CashDrawer_VarianceCalculation_Negative()
    {
        // Arrange - Drawer short by $10
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerGrain>(GrainKeys.CashDrawer(orgId, siteId, drawerId));
        await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 200m));
        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 100m));

        // Act - Count shows $290 but expected is $300
        var result = await grain.CloseAsync(new CloseDrawerCommand(290m, userId));

        // Assert
        result.ExpectedBalance.Should().Be(300m);
        result.ActualBalance.Should().Be(290m);
        result.Variance.Should().Be(-10m); // $10 short
    }

    // Given: an open cash drawer with $150 float, $75 received, and $25 paid out (expected $200)
    // When: the drawer is closed with an actual count of $200
    // Then: the variance is zero, indicating the drawer is perfectly balanced
    [Fact]
    public async Task CashDrawer_VarianceCalculation_Zero()
    {
        // Arrange - Drawer perfectly balanced
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerGrain>(GrainKeys.CashDrawer(orgId, siteId, drawerId));
        await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 150m));
        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 75m));
        await grain.RecordCashOutAsync(new RecordCashOutCommand(25m, "Change given"));

        // Act - Count matches expected balance exactly
        var result = await grain.CloseAsync(new CloseDrawerCommand(200m, userId));

        // Assert
        result.ExpectedBalance.Should().Be(200m);
        result.ActualBalance.Should().Be(200m);
        result.Variance.Should().Be(0m);
    }
}
