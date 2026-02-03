using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.Streams;
using Orleans.TestingHost;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class StreamEventTests
{
    private readonly TestCluster _cluster;

    public StreamEventTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task UserGrain_PublishesUserCreatedEvent_WhenCreated()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var receivedEvents = new List<IStreamEvent>();

        // Subscribe to user stream
        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.UserStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var subscription = await stream.SubscribeAsync((evt, token) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

            await grain.CreateAsync(new CreateUserCommand(
                OrganizationId: orgId,
                Email: "test@example.com",
                DisplayName: "Test User",
                FirstName: "Test",
                LastName: "User"));

            // Wait for event propagation
            await Task.Delay(500);

            Assert.Contains(receivedEvents, e => e is UserCreatedEvent);
            var createdEvent = receivedEvents.OfType<UserCreatedEvent>().First();
            Assert.Equal(userId, createdEvent.UserId);
            Assert.Equal("test@example.com", createdEvent.Email);
        }
        finally
        {
            await subscription.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task UserGrain_PublishesStatusChangedEvent_WhenDeactivated()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var receivedEvents = new List<IStreamEvent>();

        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.UserStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var subscription = await stream.SubscribeAsync((evt, token) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

            await grain.CreateAsync(new CreateUserCommand(
                orgId, "test2@example.com", "Test User 2"));

            await grain.DeactivateAsync();

            // Wait for event propagation
            await Task.Delay(500);

            Assert.Contains(receivedEvents, e => e is UserStatusChangedEvent);
            var statusEvent = receivedEvents.OfType<UserStatusChangedEvent>().First();
            Assert.Equal(UserStatus.Active, statusEvent.OldStatus);
            Assert.Equal(UserStatus.Inactive, statusEvent.NewStatus);
        }
        finally
        {
            await subscription.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task UserGrain_PublishesSiteAccessEvents_WhenAccessChanges()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var receivedEvents = new List<IStreamEvent>();

        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.UserStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var subscription = await stream.SubscribeAsync((evt, token) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

            await grain.CreateAsync(new CreateUserCommand(
                orgId, "test3@example.com", "Test User 3"));

            await grain.GrantSiteAccessAsync(siteId);
            await grain.RevokeSiteAccessAsync(siteId);

            // Wait for event propagation
            await Task.Delay(500);

            Assert.Contains(receivedEvents, e => e is UserSiteAccessGrantedEvent);
            Assert.Contains(receivedEvents, e => e is UserSiteAccessRevokedEvent);

            var grantEvent = receivedEvents.OfType<UserSiteAccessGrantedEvent>().First();
            Assert.Equal(siteId, grantEvent.SiteId);

            var revokeEvent = receivedEvents.OfType<UserSiteAccessRevokedEvent>().First();
            Assert.Equal(siteId, revokeEvent.SiteId);
        }
        finally
        {
            await subscription.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task EmployeeGrain_PublishesEmployeeCreatedEvent_WhenCreated()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var receivedEvents = new List<IStreamEvent>();

        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.EmployeeStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var subscription = await stream.SubscribeAsync((evt, token) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
                GrainKeys.Employee(orgId, employeeId));

            await grain.CreateAsync(new CreateEmployeeCommand(
                OrganizationId: orgId,
                UserId: userId,
                DefaultSiteId: siteId,
                EmployeeNumber: "EMP-STREAM-001",
                FirstName: "Stream",
                LastName: "Test",
                Email: "stream@example.com"));

            // Wait for event propagation
            await Task.Delay(500);

            Assert.Contains(receivedEvents, e => e is EmployeeCreatedEvent);
            var createdEvent = receivedEvents.OfType<EmployeeCreatedEvent>().First();
            Assert.Equal(employeeId, createdEvent.EmployeeId);
            Assert.Equal("Stream", createdEvent.FirstName);
        }
        finally
        {
            await subscription.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task EmployeeGrain_PublishesClockEvents_WhenClocking()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var receivedEvents = new List<IStreamEvent>();

        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.EmployeeStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var subscription = await stream.SubscribeAsync((evt, token) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
                GrainKeys.Employee(orgId, employeeId));

            await grain.CreateAsync(new CreateEmployeeCommand(
                orgId, userId, siteId, "EMP-CLOCK-001", "Clock", "Test", "clock@example.com"));

            await grain.ClockInAsync(new ClockInCommand(siteId));
            await grain.ClockOutAsync(new ClockOutCommand());

            // Wait for event propagation
            await Task.Delay(500);

            Assert.Contains(receivedEvents, e => e is EmployeeClockedInEvent);
            Assert.Contains(receivedEvents, e => e is EmployeeClockedOutEvent);
        }
        finally
        {
            await subscription.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task OrderGrain_PublishesOrderCompletedEvent_WhenClosed()
    {
        // This test verifies the event-driven pub/sub chain:
        // 1. OrderGrain publishes OrderCompletedEvent to order-events stream
        // 2. SalesEventSubscriber receives it and derives SaleRecordedEvent to sales-events stream

        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var receivedEvents = new List<IStreamEvent>();

        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var orderStreamId = StreamId.Create(StreamConstants.OrderStreamNamespace, orgId.ToString());
        var orderStream = streamProvider.GetStream<IStreamEvent>(orderStreamId);

        var salesStreamId = StreamId.Create(StreamConstants.SalesStreamNamespace, orgId.ToString());
        var salesStream = streamProvider.GetStream<IStreamEvent>(salesStreamId);

        var orderSub = await orderStream.SubscribeAsync((evt, token) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        var salesSub = await salesStream.SubscribeAsync((evt, token) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(
                GrainKeys.Order(orgId, siteId, orderId));

            await grain.CreateAsync(new CreateOrderCommand(
                OrganizationId: orgId,
                SiteId: siteId,
                CreatedBy: Guid.NewGuid(),
                Type: OrderType.DineIn));

            await grain.AddLineAsync(new AddLineCommand(
                MenuItemId: Guid.NewGuid(),
                Name: "Burger",
                Quantity: 1,
                UnitPrice: 12.99m));

            // Pay for the order
            await grain.RecordPaymentAsync(Guid.NewGuid(), 14.29m, 2.00m, "Card");

            // Close the order
            await grain.CloseAsync(Guid.NewGuid());

            // Wait for event propagation (longer delay for subscriber chain)
            await Task.Delay(1000);

            Assert.Contains(receivedEvents, e => e is OrderCreatedEvent);
            Assert.Contains(receivedEvents, e => e is OrderLineAddedEvent);
            Assert.Contains(receivedEvents, e => e is OrderCompletedEvent);
            // SaleRecordedEvent is now derived by SalesEventSubscriber from OrderCompletedEvent
            Assert.Contains(receivedEvents, e => e is SaleRecordedEvent);

            var completedEvent = receivedEvents.OfType<OrderCompletedEvent>().First();
            Assert.Equal(orderId, completedEvent.OrderId);
            Assert.Single(completedEvent.Lines);
            Assert.NotNull(completedEvent.BusinessDate); // New field for sales aggregation
        }
        finally
        {
            await orderSub.UnsubscribeAsync();
            await salesSub.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task OrderGrain_PublishesVoidEvents_WhenVoided()
    {
        // This test verifies the event-driven pub/sub chain:
        // 1. OrderGrain publishes OrderVoidedEvent to order-events stream
        // 2. SalesEventSubscriber receives it and derives VoidRecordedEvent to sales-events stream

        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var receivedEvents = new List<IStreamEvent>();

        var streamProvider = _cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var orderStreamId = StreamId.Create(StreamConstants.OrderStreamNamespace, orgId.ToString());
        var orderStream = streamProvider.GetStream<IStreamEvent>(orderStreamId);

        var salesStreamId = StreamId.Create(StreamConstants.SalesStreamNamespace, orgId.ToString());
        var salesStream = streamProvider.GetStream<IStreamEvent>(salesStreamId);

        var orderSub = await orderStream.SubscribeAsync((evt, token) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        var salesSub = await salesStream.SubscribeAsync((evt, token) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(
                GrainKeys.Order(orgId, siteId, orderId));

            await grain.CreateAsync(new CreateOrderCommand(
                OrganizationId: orgId,
                SiteId: siteId,
                CreatedBy: Guid.NewGuid(),
                Type: OrderType.DineIn));

            await grain.AddLineAsync(new AddLineCommand(
                MenuItemId: Guid.NewGuid(),
                Name: "Salad",
                Quantity: 1,
                UnitPrice: 9.99m));

            // Void the order
            var voidingUserId = Guid.NewGuid();
            await grain.VoidAsync(new VoidOrderCommand(voidingUserId, "Customer cancelled"));

            // Wait for event propagation (longer delay for subscriber chain)
            await Task.Delay(1000);

            Assert.Contains(receivedEvents, e => e is OrderVoidedEvent);
            // VoidRecordedEvent is now derived by SalesEventSubscriber from OrderVoidedEvent
            Assert.Contains(receivedEvents, e => e is VoidRecordedEvent);

            var voidEvent = receivedEvents.OfType<OrderVoidedEvent>().First();
            Assert.Equal("Customer cancelled", voidEvent.Reason);
            Assert.NotNull(voidEvent.BusinessDate); // New field for sales aggregation
        }
        finally
        {
            await orderSub.UnsubscribeAsync();
            await salesSub.UnsubscribeAsync();
        }
    }
}
