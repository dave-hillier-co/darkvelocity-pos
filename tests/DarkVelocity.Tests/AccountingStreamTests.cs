using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.Streams;
using Orleans.TestingHost;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class AccountingStreamTests
{
    private readonly TestCluster _cluster;

    public AccountingStreamTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task GiftCardGrain_ActivateAsync_PublishesEvent()
    {
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        // Subscribe to the gift card stream
        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.GiftCardStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var receivedEvents = new List<IStreamEvent>();
        var handle = await stream.SubscribeAsync((evt, _) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            // Create and activate a gift card
            var grain = _cluster.GrainFactory.GetGrain<IGiftCardGrain>(
                GrainKeys.GiftCard(orgId, cardId));

            await grain.CreateAsync(new CreateGiftCardCommand(
                OrganizationId: orgId,
                CardNumber: "GC-12345",
                Type: GiftCardType.Physical,
                InitialValue: 50m,
                Currency: "USD",
                ExpiresAt: DateTime.UtcNow.AddYears(1)));

            await grain.ActivateAsync(new ActivateGiftCardCommand(
                SiteId: siteId,
                OrderId: orderId,
                ActivatedBy: Guid.NewGuid(),
                PurchaserCustomerId: customerId,
                PurchaserName: "John Doe",
                PurchaserEmail: "john@example.com"));

            // Wait for event propagation
            await Task.Delay(500);

            // Verify event was published
            var activatedEvent = receivedEvents.OfType<GiftCardActivatedEvent>().FirstOrDefault();
            Assert.NotNull(activatedEvent);
            Assert.Equal(cardId, activatedEvent.CardId);
            Assert.Equal(50m, activatedEvent.Amount);
            Assert.Equal("GC-12345", activatedEvent.CardNumber);
            Assert.Equal(orgId, activatedEvent.OrganizationId);
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task GiftCardGrain_RedeemAsync_PublishesEvent()
    {
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.GiftCardStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var receivedEvents = new List<IStreamEvent>();
        var handle = await stream.SubscribeAsync((evt, _) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _cluster.GrainFactory.GetGrain<IGiftCardGrain>(
                GrainKeys.GiftCard(orgId, cardId));

            await grain.CreateAsync(new CreateGiftCardCommand(
                OrganizationId: orgId,
                CardNumber: "GC-67890",
                Type: GiftCardType.Digital,
                InitialValue: 100m,
                Currency: "USD",
                ExpiresAt: DateTime.UtcNow.AddYears(1)));

            await grain.ActivateAsync(new ActivateGiftCardCommand(
                SiteId: siteId,
                OrderId: Guid.NewGuid(),
                ActivatedBy: Guid.NewGuid()));

            // Redeem part of the card
            var redemptionOrderId = Guid.NewGuid();
            await grain.RedeemAsync(new RedeemGiftCardCommand(
                SiteId: siteId,
                Amount: 25m,
                OrderId: redemptionOrderId,
                PaymentId: paymentId,
                PerformedBy: Guid.NewGuid()));

            await Task.Delay(500);

            var redeemedEvent = receivedEvents.OfType<GiftCardRedeemedEvent>().FirstOrDefault();
            Assert.NotNull(redeemedEvent);
            Assert.Equal(cardId, redeemedEvent.CardId);
            Assert.Equal(25m, redeemedEvent.Amount);
            Assert.Equal(75m, redeemedEvent.RemainingBalance);
            Assert.Equal(redemptionOrderId, redeemedEvent.OrderId);
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task GiftCardGrain_ExpireAsync_PublishesEventWithBreakage()
    {
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.GiftCardStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var receivedEvents = new List<IStreamEvent>();
        var handle = await stream.SubscribeAsync((evt, _) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _cluster.GrainFactory.GetGrain<IGiftCardGrain>(
                GrainKeys.GiftCard(orgId, cardId));

            await grain.CreateAsync(new CreateGiftCardCommand(
                OrganizationId: orgId,
                CardNumber: "GC-EXPIRE",
                Type: GiftCardType.Physical,
                InitialValue: 75m,
                Currency: "USD",
                ExpiresAt: DateTime.UtcNow.AddYears(1)));

            await grain.ActivateAsync(new ActivateGiftCardCommand(
                SiteId: siteId,
                OrderId: Guid.NewGuid(),
                ActivatedBy: Guid.NewGuid()));

            // Expire the card with remaining balance
            await grain.ExpireAsync();

            await Task.Delay(500);

            var expiredEvent = receivedEvents.OfType<GiftCardExpiredEvent>().FirstOrDefault();
            Assert.NotNull(expiredEvent);
            Assert.Equal(cardId, expiredEvent.CardId);
            Assert.Equal(75m, expiredEvent.ExpiredBalance); // Breakage amount
            Assert.Equal("GC-EXPIRE", expiredEvent.CardNumber);
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task CustomerSpendProjection_RecordSpendAsync_PublishesSpendEvent()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.CustomerSpendStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var receivedEvents = new List<IStreamEvent>();
        var handle = await stream.SubscribeAsync((evt, _) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
                GrainKeys.CustomerSpendProjection(orgId, customerId));

            await grain.InitializeAsync(orgId, customerId);

            await grain.RecordSpendAsync(new RecordSpendCommand(
                OrderId: orderId,
                SiteId: siteId,
                NetSpend: 150m,
                GrossSpend: 162m,
                DiscountAmount: 10m,
                TaxAmount: 12m,
                ItemCount: 5,
                TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

            await Task.Delay(500);

            // Should have both spend recorded and points earned events
            var spendEvent = receivedEvents.OfType<CustomerSpendRecordedEvent>().FirstOrDefault();
            Assert.NotNull(spendEvent);
            Assert.Equal(customerId, spendEvent.CustomerId);
            Assert.Equal(150m, spendEvent.NetSpend);
            Assert.Equal(orderId, spendEvent.OrderId);

            var pointsEvent = receivedEvents.OfType<LoyaltyPointsEarnedEvent>().FirstOrDefault();
            Assert.NotNull(pointsEvent);
            Assert.Equal(customerId, pointsEvent.CustomerId);
            Assert.Equal(150, pointsEvent.PointsEarned);
            Assert.Equal("Bronze", pointsEvent.CurrentTier);
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task CustomerSpendProjection_TierChange_PublishesEvent()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.CustomerSpendStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var receivedEvents = new List<IStreamEvent>();
        var handle = await stream.SubscribeAsync((evt, _) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
                GrainKeys.CustomerSpendProjection(orgId, customerId));

            await grain.InitializeAsync(orgId, customerId);

            // Record spend that crosses Silver threshold (500)
            await grain.RecordSpendAsync(new RecordSpendCommand(
                OrderId: Guid.NewGuid(),
                SiteId: siteId,
                NetSpend: 600m,
                GrossSpend: 648m,
                DiscountAmount: 0m,
                TaxAmount: 48m,
                ItemCount: 15,
                TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

            await Task.Delay(500);

            var tierChangedEvent = receivedEvents.OfType<CustomerTierChangedEvent>().FirstOrDefault();
            Assert.NotNull(tierChangedEvent);
            Assert.Equal(customerId, tierChangedEvent.CustomerId);
            Assert.Equal("Bronze", tierChangedEvent.OldTier);
            Assert.Equal("Silver", tierChangedEvent.NewTier);
            Assert.Equal(600m, tierChangedEvent.CumulativeSpend);
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task CustomerSpendProjection_RedeemPoints_PublishesEvent()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.CustomerSpendStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var receivedEvents = new List<IStreamEvent>();
        var handle = await stream.SubscribeAsync((evt, _) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
                GrainKeys.CustomerSpendProjection(orgId, customerId));

            await grain.InitializeAsync(orgId, customerId);

            // Earn points
            await grain.RecordSpendAsync(new RecordSpendCommand(
                OrderId: Guid.NewGuid(),
                SiteId: siteId,
                NetSpend: 300m,
                GrossSpend: 324m,
                DiscountAmount: 0m,
                TaxAmount: 24m,
                ItemCount: 8,
                TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

            receivedEvents.Clear();

            // Redeem points
            await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(
                Points: 100,
                OrderId: orderId,
                RewardType: "Discount"));

            await Task.Delay(500);

            var redeemedEvent = receivedEvents.OfType<LoyaltyPointsRedeemedEvent>().FirstOrDefault();
            Assert.NotNull(redeemedEvent);
            Assert.Equal(customerId, redeemedEvent.CustomerId);
            Assert.Equal(100, redeemedEvent.PointsRedeemed);
            Assert.Equal(1.00m, redeemedEvent.DiscountValue);
            Assert.Equal(200, redeemedEvent.RemainingPoints);
            Assert.Equal(orderId, redeemedEvent.OrderId);
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task CustomerSpendProjection_ReverseSpend_PublishesEvent()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.CustomerSpendStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var receivedEvents = new List<IStreamEvent>();
        var handle = await stream.SubscribeAsync((evt, _) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
                GrainKeys.CustomerSpendProjection(orgId, customerId));

            await grain.InitializeAsync(orgId, customerId);

            await grain.RecordSpendAsync(new RecordSpendCommand(
                OrderId: orderId,
                SiteId: siteId,
                NetSpend: 200m,
                GrossSpend: 216m,
                DiscountAmount: 0m,
                TaxAmount: 16m,
                ItemCount: 5,
                TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

            receivedEvents.Clear();

            await grain.ReverseSpendAsync(new ReverseSpendCommand(
                OrderId: orderId,
                Amount: 200m,
                Reason: "Order refund"));

            await Task.Delay(500);

            var reversedEvent = receivedEvents.OfType<CustomerSpendReversedEvent>().FirstOrDefault();
            Assert.NotNull(reversedEvent);
            Assert.Equal(customerId, reversedEvent.CustomerId);
            Assert.Equal(200m, reversedEvent.ReversedAmount);
            Assert.Equal("Order refund", reversedEvent.Reason);
            Assert.Equal(orderId, reversedEvent.OrderId);
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }
}
