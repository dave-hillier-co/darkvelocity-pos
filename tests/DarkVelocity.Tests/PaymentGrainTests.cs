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
}
