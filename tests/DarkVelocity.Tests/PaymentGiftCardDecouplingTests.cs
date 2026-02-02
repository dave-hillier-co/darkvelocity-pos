using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using FluentAssertions;
using Orleans.Streams;
using Orleans.TestingHost;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests that verify the decoupled payment and gift card integration via events.
/// When a payment is completed using a gift card, the PaymentEventSubscriber
/// should automatically redeem from the gift card.
/// When a gift card payment is refunded, the subscriber should credit back to the card.
/// </summary>
[Collection(ClusterCollection.Name)]
public class PaymentGiftCardDecouplingTests
{
    private readonly TestCluster _cluster;

    public PaymentGiftCardDecouplingTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task PaymentCompleted_WithGiftCard_RedeemFromGiftCard()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var giftCardId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        // Create and activate a gift card with $100 balance
        var giftCardGrain = _cluster.GrainFactory.GetGrain<IGiftCardGrain>(
            GrainKeys.GiftCard(orgId, giftCardId));

        await giftCardGrain.CreateAsync(new CreateGiftCardCommand(
            OrganizationId: orgId,
            CardNumber: "GC-DECOUPLE-001",
            Type: GiftCardType.Physical,
            InitialValue: 100m,
            Currency: "USD",
            ExpiresAt: DateTime.UtcNow.AddYears(1)));

        await giftCardGrain.ActivateAsync(new ActivateGiftCardCommand(
            SiteId: siteId,
            OrderId: Guid.NewGuid(),
            ActivatedBy: cashierId));

        var initialState = await giftCardGrain.GetStateAsync();
        initialState.CurrentBalance.Should().Be(100m);

        // Create an order first
        var orderGrain = _cluster.GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(orgId, siteId, orderId));

        await orderGrain.OpenAsync(new OpenOrderCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            SalesPeriodId: Guid.NewGuid(),
            ServerId: cashierId,
            OrderType: "DineIn"));

        // Act - Initiate and complete a gift card payment
        var paymentGrain = _cluster.GrainFactory.GetGrain<IPaymentGrain>(
            GrainKeys.Payment(orgId, siteId, paymentId));

        await paymentGrain.InitiateAsync(new InitiatePaymentCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            OrderId: orderId,
            Method: PaymentMethod.GiftCard,
            Amount: 50m,
            CashierId: cashierId,
            CustomerId: null,
            DrawerId: null));

        await paymentGrain.CompleteGiftCardAsync(new ProcessGiftCardPaymentCommand(
            GiftCardId: giftCardId,
            CardNumber: "GC-DECOUPLE-001"));

        // Wait for event propagation through the subscriber
        await Task.Delay(1000);

        // Assert - Gift card should have been redeemed via the event-driven subscriber
        var finalState = await giftCardGrain.GetStateAsync();
        finalState.CurrentBalance.Should().Be(50m, "gift card should be redeemed via event-driven flow");
        finalState.TotalRedeemed.Should().Be(50m);
        finalState.RedemptionCount.Should().Be(1);

        // Verify the transaction was recorded
        var transactions = await giftCardGrain.GetTransactionsAsync();
        transactions.Should().Contain(t =>
            t.Type == GiftCardTransactionType.Redemption &&
            t.Amount == -50m &&
            t.PaymentId == paymentId);
    }

    [Fact]
    public async Task PaymentRefunded_WithGiftCard_CreditsBackToGiftCard()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var giftCardId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        // Create and activate a gift card with $100 balance
        var giftCardGrain = _cluster.GrainFactory.GetGrain<IGiftCardGrain>(
            GrainKeys.GiftCard(orgId, giftCardId));

        await giftCardGrain.CreateAsync(new CreateGiftCardCommand(
            OrganizationId: orgId,
            CardNumber: "GC-REFUND-001",
            Type: GiftCardType.Physical,
            InitialValue: 100m,
            Currency: "USD",
            ExpiresAt: DateTime.UtcNow.AddYears(1)));

        await giftCardGrain.ActivateAsync(new ActivateGiftCardCommand(
            SiteId: siteId,
            OrderId: Guid.NewGuid(),
            ActivatedBy: cashierId));

        // Create an order first
        var orderGrain = _cluster.GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(orgId, siteId, orderId));

        await orderGrain.OpenAsync(new OpenOrderCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            SalesPeriodId: Guid.NewGuid(),
            ServerId: cashierId,
            OrderType: "DineIn"));

        // Initiate and complete a gift card payment
        var paymentGrain = _cluster.GrainFactory.GetGrain<IPaymentGrain>(
            GrainKeys.Payment(orgId, siteId, paymentId));

        await paymentGrain.InitiateAsync(new InitiatePaymentCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            OrderId: orderId,
            Method: PaymentMethod.GiftCard,
            Amount: 75m,
            CashierId: cashierId,
            CustomerId: null,
            DrawerId: null));

        await paymentGrain.CompleteGiftCardAsync(new ProcessGiftCardPaymentCommand(
            GiftCardId: giftCardId,
            CardNumber: "GC-REFUND-001"));

        // Wait for redemption event to process
        await Task.Delay(1000);

        var stateAfterPayment = await giftCardGrain.GetStateAsync();
        stateAfterPayment.CurrentBalance.Should().Be(25m);

        // Act - Refund part of the payment
        await paymentGrain.RefundAsync(new RefundPaymentCommand(
            Amount: 30m,
            Reason: "Customer returned item",
            IssuedBy: cashierId));

        // Wait for refund event to process
        await Task.Delay(1000);

        // Assert - Gift card should have been credited back via the event-driven subscriber
        var finalState = await giftCardGrain.GetStateAsync();
        finalState.CurrentBalance.Should().Be(55m, "gift card should be credited with refund via event-driven flow");

        // Verify the refund transaction was recorded
        var transactions = await giftCardGrain.GetTransactionsAsync();
        transactions.Should().Contain(t =>
            t.Type == GiftCardTransactionType.Refund &&
            t.Amount == 30m);
    }

    [Fact]
    public async Task PaymentCompletedEvent_ContainsGiftCardId()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var giftCardId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        // Subscribe to payment stream to verify event contents
        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.PaymentStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var receivedEvents = new List<IStreamEvent>();
        var handle = await stream.SubscribeAsync((evt, _) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            // Create gift card
            var giftCardGrain = _cluster.GrainFactory.GetGrain<IGiftCardGrain>(
                GrainKeys.GiftCard(orgId, giftCardId));

            await giftCardGrain.CreateAsync(new CreateGiftCardCommand(
                OrganizationId: orgId,
                CardNumber: "GC-EVENT-001",
                Type: GiftCardType.Digital,
                InitialValue: 100m,
                Currency: "USD",
                ExpiresAt: DateTime.UtcNow.AddYears(1)));

            await giftCardGrain.ActivateAsync(new ActivateGiftCardCommand(
                SiteId: siteId,
                OrderId: Guid.NewGuid(),
                ActivatedBy: cashierId));

            // Create order
            var orderGrain = _cluster.GrainFactory.GetGrain<IOrderGrain>(
                GrainKeys.Order(orgId, siteId, orderId));

            await orderGrain.OpenAsync(new OpenOrderCommand(
                OrganizationId: orgId,
                SiteId: siteId,
                SalesPeriodId: Guid.NewGuid(),
                ServerId: cashierId,
                OrderType: "DineIn"));

            // Act - Complete gift card payment
            var paymentGrain = _cluster.GrainFactory.GetGrain<IPaymentGrain>(
                GrainKeys.Payment(orgId, siteId, paymentId));

            await paymentGrain.InitiateAsync(new InitiatePaymentCommand(
                OrganizationId: orgId,
                SiteId: siteId,
                OrderId: orderId,
                Method: PaymentMethod.GiftCard,
                Amount: 50m,
                CashierId: cashierId,
                CustomerId: null,
                DrawerId: null));

            await paymentGrain.CompleteGiftCardAsync(new ProcessGiftCardPaymentCommand(
                GiftCardId: giftCardId,
                CardNumber: "GC-EVENT-001"));

            await Task.Delay(500);

            // Assert - PaymentCompletedEvent should contain GiftCardId
            var completedEvent = receivedEvents.OfType<PaymentCompletedEvent>().FirstOrDefault();
            completedEvent.Should().NotBeNull();
            completedEvent!.GiftCardId.Should().Be(giftCardId);
            completedEvent.Method.Should().Be("GiftCard");
            completedEvent.PaymentId.Should().Be(paymentId);
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task PaymentRefundedEvent_ContainsGiftCardId()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var giftCardId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        // Subscribe to payment stream
        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.PaymentStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var receivedEvents = new List<IStreamEvent>();
        var handle = await stream.SubscribeAsync((evt, _) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            // Setup gift card and payment
            var giftCardGrain = _cluster.GrainFactory.GetGrain<IGiftCardGrain>(
                GrainKeys.GiftCard(orgId, giftCardId));

            await giftCardGrain.CreateAsync(new CreateGiftCardCommand(
                OrganizationId: orgId,
                CardNumber: "GC-REFUND-EVENT-001",
                Type: GiftCardType.Digital,
                InitialValue: 100m,
                Currency: "USD",
                ExpiresAt: DateTime.UtcNow.AddYears(1)));

            await giftCardGrain.ActivateAsync(new ActivateGiftCardCommand(
                SiteId: siteId,
                OrderId: Guid.NewGuid(),
                ActivatedBy: cashierId));

            var orderGrain = _cluster.GrainFactory.GetGrain<IOrderGrain>(
                GrainKeys.Order(orgId, siteId, orderId));

            await orderGrain.OpenAsync(new OpenOrderCommand(
                OrganizationId: orgId,
                SiteId: siteId,
                SalesPeriodId: Guid.NewGuid(),
                ServerId: cashierId,
                OrderType: "DineIn"));

            var paymentGrain = _cluster.GrainFactory.GetGrain<IPaymentGrain>(
                GrainKeys.Payment(orgId, siteId, paymentId));

            await paymentGrain.InitiateAsync(new InitiatePaymentCommand(
                OrganizationId: orgId,
                SiteId: siteId,
                OrderId: orderId,
                Method: PaymentMethod.GiftCard,
                Amount: 80m,
                CashierId: cashierId,
                CustomerId: null,
                DrawerId: null));

            await paymentGrain.CompleteGiftCardAsync(new ProcessGiftCardPaymentCommand(
                GiftCardId: giftCardId,
                CardNumber: "GC-REFUND-EVENT-001"));

            await Task.Delay(500);
            receivedEvents.Clear();

            // Act - Refund the payment
            await paymentGrain.RefundAsync(new RefundPaymentCommand(
                Amount: 25m,
                Reason: "Item not available",
                IssuedBy: cashierId));

            await Task.Delay(500);

            // Assert - PaymentRefundedEvent should contain GiftCardId
            var refundedEvent = receivedEvents.OfType<PaymentRefundedEvent>().FirstOrDefault();
            refundedEvent.Should().NotBeNull();
            refundedEvent!.GiftCardId.Should().Be(giftCardId);
            refundedEvent.Method.Should().Be("GiftCard");
            refundedEvent.RefundAmount.Should().Be(25m);
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task NonGiftCardPayment_DoesNotAffectGiftCard()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var giftCardId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        // Create a gift card to verify it's not affected
        var giftCardGrain = _cluster.GrainFactory.GetGrain<IGiftCardGrain>(
            GrainKeys.GiftCard(orgId, giftCardId));

        await giftCardGrain.CreateAsync(new CreateGiftCardCommand(
            OrganizationId: orgId,
            CardNumber: "GC-UNAFFECTED-001",
            Type: GiftCardType.Physical,
            InitialValue: 100m,
            Currency: "USD",
            ExpiresAt: DateTime.UtcNow.AddYears(1)));

        await giftCardGrain.ActivateAsync(new ActivateGiftCardCommand(
            SiteId: siteId,
            OrderId: Guid.NewGuid(),
            ActivatedBy: cashierId));

        // Create order
        var orderGrain = _cluster.GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(orgId, siteId, orderId));

        await orderGrain.OpenAsync(new OpenOrderCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            SalesPeriodId: Guid.NewGuid(),
            ServerId: cashierId,
            OrderType: "DineIn"));

        // Act - Complete a CASH payment (not gift card)
        var paymentGrain = _cluster.GrainFactory.GetGrain<IPaymentGrain>(
            GrainKeys.Payment(orgId, siteId, paymentId));

        await paymentGrain.InitiateAsync(new InitiatePaymentCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            OrderId: orderId,
            Method: PaymentMethod.Cash,
            Amount: 50m,
            CashierId: cashierId,
            CustomerId: null,
            DrawerId: null));

        await paymentGrain.CompleteCashAsync(new CompleteCashPaymentCommand(
            AmountTendered: 60m,
            TipAmount: 0m));

        await Task.Delay(500);

        // Assert - Gift card balance should be unchanged
        var giftCardState = await giftCardGrain.GetStateAsync();
        giftCardState.CurrentBalance.Should().Be(100m, "cash payment should not affect gift card");
        giftCardState.RedemptionCount.Should().Be(0);
    }
}
