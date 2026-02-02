using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using FluentAssertions;
using Orleans.Streams;
using Orleans.TestingHost;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for audit trail and event sourcing verification.
/// Ensures that state changes emit proper events with correct metadata.
/// </summary>
[Collection(ClusterCollection.Name)]
public class AuditTrailVerificationTests
{
    private readonly TestCluster _cluster;

    public AuditTrailVerificationTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    #region Gift Card Event Audit Tests

    [Fact]
    public async Task GiftCard_Lifecycle_ShouldEmitAllEvents()
    {
        // Arrange
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

            // Act - Full lifecycle
            await grain.CreateAsync(new CreateGiftCardCommand(
                OrganizationId: orgId,
                CardNumber: "GC-AUDIT-001",
                Type: GiftCardType.Physical,
                InitialValue: 100m,
                Currency: "USD",
                ExpiresAt: DateTime.UtcNow.AddYears(1)));

            await grain.ActivateAsync(new ActivateGiftCardCommand(
                SiteId: siteId,
                OrderId: Guid.NewGuid(),
                ActivatedBy: Guid.NewGuid()));

            await grain.RedeemAsync(new RedeemGiftCardCommand(
                SiteId: siteId,
                Amount: 30m,
                OrderId: Guid.NewGuid(),
                PaymentId: Guid.NewGuid(),
                PerformedBy: Guid.NewGuid()));

            await grain.ExpireAsync();

            await Task.Delay(500);

            // Assert - All lifecycle events emitted
            receivedEvents.OfType<GiftCardActivatedEvent>().Should().HaveCount(1);
            receivedEvents.OfType<GiftCardRedeemedEvent>().Should().HaveCount(1);
            receivedEvents.OfType<GiftCardExpiredEvent>().Should().HaveCount(1);

            // Verify event metadata
            var activatedEvent = receivedEvents.OfType<GiftCardActivatedEvent>().First();
            activatedEvent.CardId.Should().Be(cardId);
            activatedEvent.OrganizationId.Should().Be(orgId);
            activatedEvent.Amount.Should().Be(100m);
            activatedEvent.CardNumber.Should().Be("GC-AUDIT-001");
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task GiftCard_RedeemEvent_ShouldHaveCorrectBalanceTracking()
    {
        // Arrange
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
                CardNumber: "GC-BALANCE",
                Type: GiftCardType.Digital,
                InitialValue: 75m,
                Currency: "USD",
                ExpiresAt: DateTime.UtcNow.AddYears(1)));

            await grain.ActivateAsync(new ActivateGiftCardCommand(
                SiteId: siteId,
                OrderId: Guid.NewGuid(),
                ActivatedBy: Guid.NewGuid()));

            // Act - Multiple redemptions
            await grain.RedeemAsync(new RedeemGiftCardCommand(
                SiteId: siteId,
                Amount: 25m,
                OrderId: Guid.NewGuid(),
                PaymentId: Guid.NewGuid(),
                PerformedBy: Guid.NewGuid()));

            await grain.RedeemAsync(new RedeemGiftCardCommand(
                SiteId: siteId,
                Amount: 30m,
                OrderId: Guid.NewGuid(),
                PaymentId: Guid.NewGuid(),
                PerformedBy: Guid.NewGuid()));

            await Task.Delay(500);

            // Assert - Each redemption has correct remaining balance
            var redeemEvents = receivedEvents.OfType<GiftCardRedeemedEvent>().ToList();
            redeemEvents.Should().HaveCount(2);

            redeemEvents[0].Amount.Should().Be(25m);
            redeemEvents[0].RemainingBalance.Should().Be(50m);

            redeemEvents[1].Amount.Should().Be(30m);
            redeemEvents[1].RemainingBalance.Should().Be(20m);
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    #endregion

    #region Customer Spend Event Audit Tests

    [Fact]
    public async Task CustomerSpend_RecordSpend_ShouldEmitSpendAndPointsEvents()
    {
        // Arrange
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

            // Act
            await grain.RecordSpendAsync(new RecordSpendCommand(
                OrderId: orderId,
                SiteId: siteId,
                NetSpend: 100m,
                GrossSpend: 108m,
                DiscountAmount: 5m,
                ItemCount: 4,
                TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

            await Task.Delay(500);

            // Assert - Both spend recorded and points earned events
            var spendEvent = receivedEvents.OfType<CustomerSpendRecordedEvent>().FirstOrDefault();
            spendEvent.Should().NotBeNull();
            spendEvent!.CustomerId.Should().Be(customerId);
            spendEvent.NetSpend.Should().Be(100m);
            spendEvent.OrderId.Should().Be(orderId);

            var pointsEvent = receivedEvents.OfType<LoyaltyPointsEarnedEvent>().FirstOrDefault();
            pointsEvent.Should().NotBeNull();
            pointsEvent!.CustomerId.Should().Be(customerId);
            pointsEvent.PointsEarned.Should().Be(100); // 1 point per $1
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task CustomerSpend_TierPromotion_ShouldEmitTierChangedEvent()
    {
        // Arrange
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

            // Act - Spend enough to cross Silver threshold (500)
            await grain.RecordSpendAsync(new RecordSpendCommand(
                OrderId: Guid.NewGuid(),
                SiteId: siteId,
                NetSpend: 550m,
                GrossSpend: 594m,
                DiscountAmount: 0m,
                ItemCount: 20,
                TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

            await Task.Delay(500);

            // Assert
            var tierEvent = receivedEvents.OfType<CustomerTierChangedEvent>().FirstOrDefault();
            tierEvent.Should().NotBeNull();
            tierEvent!.CustomerId.Should().Be(customerId);
            tierEvent.OldTier.Should().Be("Bronze");
            tierEvent.NewTier.Should().Be("Silver");
            tierEvent.CumulativeSpend.Should().Be(550m);
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task CustomerSpend_PointsRedemption_ShouldEmitRedemptionEvent()
    {
        // Arrange
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

            // Earn points first
            await grain.RecordSpendAsync(new RecordSpendCommand(
                OrderId: Guid.NewGuid(),
                SiteId: siteId,
                NetSpend: 200m,
                GrossSpend: 216m,
                DiscountAmount: 0m,
                ItemCount: 10,
                TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

            receivedEvents.Clear();

            // Act - Redeem points
            await grain.RedeemPointsAsync(new RedeemSpendPointsCommand(
                Points: 50,
                OrderId: orderId,
                RewardType: "Discount"));

            await Task.Delay(500);

            // Assert
            var redemptionEvent = receivedEvents.OfType<LoyaltyPointsRedeemedEvent>().FirstOrDefault();
            redemptionEvent.Should().NotBeNull();
            redemptionEvent!.CustomerId.Should().Be(customerId);
            redemptionEvent.PointsRedeemed.Should().Be(50);
            redemptionEvent.DiscountValue.Should().Be(0.50m); // $0.01 per point
            redemptionEvent.RemainingPoints.Should().Be(150);
            redemptionEvent.OrderId.Should().Be(orderId);
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task CustomerSpend_ReverseSpend_ShouldEmitReversalEvent()
    {
        // Arrange
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
                DiscountAmount: 0m,
                ItemCount: 6,
                TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

            receivedEvents.Clear();

            // Act - Reverse the spend (refund)
            await grain.ReverseSpendAsync(new ReverseSpendCommand(
                OrderId: orderId,
                Amount: 150m,
                Reason: "Order refunded"));

            await Task.Delay(500);

            // Assert
            var reversalEvent = receivedEvents.OfType<CustomerSpendReversedEvent>().FirstOrDefault();
            reversalEvent.Should().NotBeNull();
            reversalEvent!.CustomerId.Should().Be(customerId);
            reversalEvent.ReversedAmount.Should().Be(150m);
            reversalEvent.Reason.Should().Be("Order refunded");
            reversalEvent.OrderId.Should().Be(orderId);
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    #endregion

    #region User Event Audit Tests

    [Fact]
    public async Task User_Creation_ShouldEmitCreatedEvent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.UserStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var receivedEvents = new List<IStreamEvent>();
        var handle = await stream.SubscribeAsync((evt, _) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(
                GrainKeys.User(orgId, userId));

            // Act
            await grain.CreateAsync(new CreateUserCommand(
                OrganizationId: orgId,
                Email: "audit@test.com",
                FirstName: "Audit",
                LastName: "Test",
                UserType: UserType.Staff));

            await Task.Delay(500);

            // Assert
            var createdEvent = receivedEvents.OfType<UserCreatedEvent>().FirstOrDefault();
            createdEvent.Should().NotBeNull();
            createdEvent!.UserId.Should().Be(userId);
            createdEvent.Email.Should().Be("audit@test.com");
            createdEvent.OrganizationId.Should().Be(orgId);
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task User_StatusChange_ShouldEmitStatusChangedEvent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.UserStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var receivedEvents = new List<IStreamEvent>();
        var handle = await stream.SubscribeAsync((evt, _) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(
                GrainKeys.User(orgId, userId));

            await grain.CreateAsync(new CreateUserCommand(
                OrganizationId: orgId,
                Email: "status@test.com",
                FirstName: "Status",
                LastName: "Test",
                UserType: UserType.Staff));

            receivedEvents.Clear();

            // Act
            await grain.ChangeStatusAsync(UserStatus.Suspended);

            await Task.Delay(500);

            // Assert
            var statusEvent = receivedEvents.OfType<UserStatusChangedEvent>().FirstOrDefault();
            statusEvent.Should().NotBeNull();
            statusEvent!.UserId.Should().Be(userId);
            statusEvent.NewStatus.Should().Be(UserStatus.Suspended.ToString());
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    #endregion

    #region Event Metadata Tests

    [Fact]
    public async Task Events_ShouldHaveTimestamps()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

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
            var beforeOperation = DateTime.UtcNow;

            var grain = _cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
                GrainKeys.CustomerSpendProjection(orgId, customerId));

            await grain.InitializeAsync(orgId, customerId);

            await grain.RecordSpendAsync(new RecordSpendCommand(
                OrderId: Guid.NewGuid(),
                SiteId: Guid.NewGuid(),
                NetSpend: 50m,
                GrossSpend: 54m,
                DiscountAmount: 0m,
                ItemCount: 2,
                TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));

            var afterOperation = DateTime.UtcNow;

            await Task.Delay(500);

            // Assert - Events have timestamps within expected range
            var spendEvent = receivedEvents.OfType<CustomerSpendRecordedEvent>().FirstOrDefault();
            spendEvent.Should().NotBeNull();
            spendEvent!.OccurredAt.Should().BeOnOrAfter(beforeOperation);
            spendEvent.OccurredAt.Should().BeOnOrBefore(afterOperation.AddSeconds(1));
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task Events_ShouldHaveOrganizationContext()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

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
                CardNumber: "GC-ORG-TEST",
                Type: GiftCardType.Physical,
                InitialValue: 50m,
                Currency: "USD",
                ExpiresAt: DateTime.UtcNow.AddYears(1)));

            await grain.ActivateAsync(new ActivateGiftCardCommand(
                SiteId: Guid.NewGuid(),
                OrderId: Guid.NewGuid(),
                ActivatedBy: Guid.NewGuid()));

            await Task.Delay(500);

            // Assert - Event has organization ID
            var activatedEvent = receivedEvents.OfType<GiftCardActivatedEvent>().FirstOrDefault();
            activatedEvent.Should().NotBeNull();
            activatedEvent!.OrganizationId.Should().Be(orgId);
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    #endregion

    #region Multi-Event Sequence Tests

    [Fact]
    public async Task Events_ShouldBeOrderedChronologically()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

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

            // Act - Multiple operations in sequence
            for (int i = 0; i < 5; i++)
            {
                await grain.RecordSpendAsync(new RecordSpendCommand(
                    OrderId: Guid.NewGuid(),
                    SiteId: Guid.NewGuid(),
                    NetSpend: 20m,
                    GrossSpend: 21.60m,
                    DiscountAmount: 0m,
                    ItemCount: 1,
                    TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow)));
            }

            await Task.Delay(500);

            // Assert - Events arrived in order
            var spendEvents = receivedEvents.OfType<CustomerSpendRecordedEvent>().ToList();
            spendEvents.Should().HaveCount(5);

            for (int i = 1; i < spendEvents.Count; i++)
            {
                spendEvents[i].OccurredAt.Should().BeOnOrAfter(spendEvents[i - 1].OccurredAt);
            }
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task GiftCard_MultipleRedemptions_ShouldTrackSequence()
    {
        // Arrange
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
                CardNumber: "GC-SEQUENCE",
                Type: GiftCardType.Physical,
                InitialValue: 100m,
                Currency: "USD",
                ExpiresAt: DateTime.UtcNow.AddYears(1)));

            await grain.ActivateAsync(new ActivateGiftCardCommand(
                SiteId: siteId,
                OrderId: Guid.NewGuid(),
                ActivatedBy: Guid.NewGuid()));

            // Act - Multiple redemptions
            var redemptions = new[] { 10m, 20m, 30m, 15m };
            foreach (var amount in redemptions)
            {
                await grain.RedeemAsync(new RedeemGiftCardCommand(
                    SiteId: siteId,
                    Amount: amount,
                    OrderId: Guid.NewGuid(),
                    PaymentId: Guid.NewGuid(),
                    PerformedBy: Guid.NewGuid()));
            }

            await Task.Delay(500);

            // Assert - Balances tracked correctly in sequence
            var redeemEvents = receivedEvents.OfType<GiftCardRedeemedEvent>().ToList();
            redeemEvents.Should().HaveCount(4);

            redeemEvents[0].RemainingBalance.Should().Be(90m);  // 100 - 10
            redeemEvents[1].RemainingBalance.Should().Be(70m);  // 90 - 20
            redeemEvents[2].RemainingBalance.Should().Be(40m);  // 70 - 30
            redeemEvents[3].RemainingBalance.Should().Be(25m);  // 40 - 15
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    #endregion
}
