using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for concurrent operations and race condition handling.
/// Orleans grains are single-writer, single-threaded by design, so these tests
/// verify that concurrent requests are properly serialized and state remains consistent.
/// </summary>
[Collection(ClusterCollection.Name)]
public class ConcurrentOperationTests
{
    private readonly TestClusterFixture _fixture;

    public ConcurrentOperationTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    #region Order Concurrent Tests

    [Fact]
    public async Task Order_ConcurrentLineAdds_ShouldSerializeCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(orgId, siteId, orderId));

        await grain.CreateAsync(new CreateOrderCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            CreatedBy: Guid.NewGuid(),
            Type: OrderType.DineIn,
            GuestCount: 4));

        // Act - Add 10 lines concurrently
        var tasks = Enumerable.Range(1, 10).Select(i =>
            grain.AddLineAsync(new AddLineCommand(
                MenuItemId: Guid.NewGuid(),
                Name: $"Item {i}",
                Quantity: 1,
                UnitPrice: 10.00m * i)));

        var results = await Task.WhenAll(tasks);

        // Assert - All lines should be added with unique IDs
        var state = await grain.GetStateAsync();
        state.Lines.Should().HaveCount(10);
        results.Select(r => r.LineId).Distinct().Should().HaveCount(10);
    }

    [Fact]
    public async Task Order_ConcurrentPayments_ShouldTrackCorrectBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(orgId, siteId, orderId));

        await grain.CreateAsync(new CreateOrderCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            CreatedBy: Guid.NewGuid(),
            Type: OrderType.DineIn,
            GuestCount: 2));

        // Add items totaling $100
        for (int i = 0; i < 10; i++)
        {
            await grain.AddLineAsync(new AddLineCommand(
                MenuItemId: Guid.NewGuid(),
                Name: $"Item {i}",
                Quantity: 1,
                UnitPrice: 10.00m));
        }

        await grain.SendAsync(Guid.NewGuid());

        // Act - Make 4 concurrent partial payments of $25 each
        var tasks = Enumerable.Range(1, 4).Select(i =>
            grain.RecordPaymentAsync(
                Guid.NewGuid(),
                25.00m,
                0m,
                "Cash"));

        await Task.WhenAll(tasks);

        // Assert - All payments recorded, balance should be $0
        var state = await grain.GetStateAsync();
        state.Payments.Should().HaveCount(4);
        state.BalanceDue.Should().Be(0m);
        state.TotalPaid.Should().Be(100m);
    }

    [Fact]
    public async Task Order_ConcurrentUpdateAndVoid_ShouldHandleCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(orgId, siteId, orderId));

        await grain.CreateAsync(new CreateOrderCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            CreatedBy: Guid.NewGuid(),
            Type: OrderType.DineIn,
            GuestCount: 2));

        var line = await grain.AddLineAsync(new AddLineCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Burger",
            Quantity: 1,
            UnitPrice: 15.00m));

        // Act - Try to update and void concurrently
        // One will succeed, one should fail due to state transition
        var updateTask = grain.UpdateLineAsync(new UpdateLineCommand(
            LineId: line.LineId,
            Quantity: 2));

        var voidTask = grain.VoidAsync(new VoidOrderCommand(
            VoidedBy: Guid.NewGuid(),
            Reason: "Customer left"));

        // Wait for both to complete (one may throw)
        await Task.WhenAll(
            updateTask.ContinueWith(t => t.IsCompletedSuccessfully),
            voidTask.ContinueWith(t => t.IsCompletedSuccessfully));

        // Assert - State should be consistent (either updated or voided, not both in invalid state)
        var state = await grain.GetStateAsync();
        // Either voided with original quantity, or not voided with updated quantity
        if (state.Status == OrderStatus.Voided)
        {
            state.VoidedAt.Should().NotBeNull();
        }
        else
        {
            state.Lines.First().Quantity.Should().Be(2);
        }
    }

    #endregion

    #region Inventory Concurrent Tests

    [Fact]
    public async Task Inventory_ConcurrentConsumptions_ShouldMaintainCorrectLevel()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, inventoryId));

        await grain.InitializeAsync(new InitializeInventoryCommand(
            OrgId: orgId,
            SiteId: siteId,
            ProductName: "Burger Patties",
            Unit: "ea",
            ReorderPoint: 10,
            ReorderQty: 50,
            TrackingMethod: InventoryTrackingMethod.Batch,
            CostingMethod: CostingMethod.FIFO));

        // Receive 100 units
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand(
            BatchNumber: "BATCH-001",
            Quantity: 100,
            UnitCost: 2.00m,
            ReceivedAt: DateTime.UtcNow,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            SupplierId: Guid.NewGuid()));

        // Act - 10 concurrent consumptions of 5 units each
        var tasks = Enumerable.Range(1, 10).Select(i =>
            grain.ConsumeAsync(new ConsumeInventoryCommand(
                Quantity: 5,
                OrderId: Guid.NewGuid(),
                Reason: $"Order {i}")));

        await Task.WhenAll(tasks);

        // Assert - Should have consumed 50 units, 50 remaining
        var levelInfo = await grain.GetLevelInfoAsync();
        levelInfo.OnHand.Should().Be(50);
    }

    [Fact]
    public async Task Inventory_ConcurrentReceiveAndConsume_ShouldTrackBatchesCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(orgId, siteId, inventoryId));

        await grain.InitializeAsync(new InitializeInventoryCommand(
            OrgId: orgId,
            SiteId: siteId,
            ProductName: "Wine Bottles",
            Unit: "btl",
            ReorderPoint: 5,
            ReorderQty: 24,
            TrackingMethod: InventoryTrackingMethod.Batch,
            CostingMethod: CostingMethod.FIFO));

        // Start with initial batch
        await grain.ReceiveBatchAsync(new ReceiveBatchCommand(
            BatchNumber: "BATCH-INITIAL",
            Quantity: 20,
            UnitCost: 15.00m,
            ReceivedAt: DateTime.UtcNow,
            ExpiresAt: DateTime.UtcNow.AddYears(2)));

        // Act - Mix of receives and consumes concurrently
        var receiveTasks = Enumerable.Range(1, 3).Select(i =>
            grain.ReceiveBatchAsync(new ReceiveBatchCommand(
                BatchNumber: $"BATCH-{i}",
                Quantity: 12,
                UnitCost: 15.00m + i,
                ReceivedAt: DateTime.UtcNow,
                ExpiresAt: DateTime.UtcNow.AddYears(2))));

        var consumeTasks = Enumerable.Range(1, 5).Select(i =>
            grain.ConsumeAsync(new ConsumeInventoryCommand(
                Quantity: 3,
                OrderId: Guid.NewGuid(),
                Reason: $"Order {i}")));

        await Task.WhenAll(receiveTasks.Concat(consumeTasks));

        // Assert - Should have correct total
        // Initial 20 + (3 receives * 12) - (5 consumes * 3) = 20 + 36 - 15 = 41
        var levelInfo = await grain.GetLevelInfoAsync();
        levelInfo.OnHand.Should().Be(41);
    }

    #endregion

    #region Customer Concurrent Tests

    [Fact]
    public async Task Customer_ConcurrentPointsEarning_ShouldAccumulateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // Act - 10 concurrent spend recordings
        var tasks = Enumerable.Range(1, 10).Select(i =>
            grain.RecordSpendAsync(new RecordSpendCommand(
                OrderId: Guid.NewGuid(),
                SiteId: Guid.NewGuid(),
                NetSpend: 50m,
                GrossSpend: 54m,
                DiscountAmount: 0m,
                ItemCount: 2,
                TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow))));

        await Task.WhenAll(tasks);

        // Assert - Should have 500 cumulative spend, 500 points (1 point per $1)
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.CumulativeSpend.Should().Be(500m);
        snapshot.AvailablePoints.Should().Be(500);
    }

    [Fact]
    public async Task Customer_ConcurrentTagOperations_ShouldNotDuplicateTags()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerGrain>(
            GrainKeys.Customer(orgId, customerId));

        await grain.CreateAsync(new CreateCustomerCommand(
            OrganizationId: orgId,
            Email: "test@example.com",
            FirstName: "John",
            LastName: "Doe"));

        // Act - Try to add the same tag concurrently
        var tasks = Enumerable.Range(1, 5).Select(_ =>
            grain.AddTagAsync("VIP"));

        await Task.WhenAll(tasks);

        // Assert - Should only have one VIP tag
        var state = await grain.GetStateAsync();
        state.Tags.Count(t => t == "VIP").Should().Be(1);
    }

    #endregion

    #region Booking Concurrent Tests

    [Fact]
    public async Task Booking_ConcurrentModifications_ShouldSerialize()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingGrain>(
            GrainKeys.Booking(orgId, siteId, bookingId));

        await grain.RequestAsync(new RequestBookingCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            CustomerId: Guid.NewGuid(),
            PartySize: 4,
            RequestedDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            RequestedTime: new TimeOnly(19, 0),
            CustomerName: "John Doe",
            CustomerPhone: "555-1234",
            CustomerEmail: "john@example.com"));

        await grain.ConfirmAsync(new ConfirmBookingCommand(
            ConfirmedBy: Guid.NewGuid(),
            ConfirmedDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            ConfirmedTime: new TimeOnly(19, 0),
            Duration: TimeSpan.FromHours(2)));

        // Act - Concurrent modifications
        var tasks = Enumerable.Range(2, 5).Select(i =>
            grain.ModifyAsync(new ModifyBookingCommand(
                ModifiedBy: Guid.NewGuid(),
                NewPartySize: i)));

        await Task.WhenAll(tasks);

        // Assert - Should have final consistent state
        var state = await grain.GetStateAsync();
        state.PartySize.Should().BeInRange(2, 6);
    }

    #endregion

    #region Kitchen Concurrent Tests

    [Fact]
    public async Task KitchenStation_ConcurrentTicketOperations_ShouldTrackAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = _fixture.Cluster.GrainFactory.GetGrain<IKitchenStationGrain>(
            GrainKeys.KitchenStation(orgId, siteId, stationId));

        await station.OpenAsync(new OpenStationCommand(
            orgId, siteId, "Grill", StationType.Grill, 0));

        // Create 10 tickets
        var ticketIds = new List<Guid>();
        for (int i = 0; i < 10; i++)
        {
            var ticketId = Guid.NewGuid();
            ticketIds.Add(ticketId);
            var ticket = _fixture.Cluster.GrainFactory.GetGrain<IKitchenTicketGrain>(
                GrainKeys.KitchenOrder(orgId, siteId, ticketId));
            await ticket.CreateAsync(new CreateKitchenTicketCommand(
                orgId, siteId, Guid.NewGuid(), $"ORD-{i:D3}",
                OrderType.DineIn, $"T{i}", 2, "Server"));
        }

        // Act - Add all tickets to station concurrently
        var addTasks = ticketIds.Select(id => station.ReceiveTicketAsync(id));
        await Task.WhenAll(addTasks);

        // Assert - All tickets should be tracked
        var currentTickets = await station.GetCurrentTicketIdsAsync();
        currentTickets.Should().HaveCount(10);
    }

    [Fact]
    public async Task KitchenTicket_ConcurrentItemAdds_ShouldAddAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = _fixture.Cluster.GrainFactory.GetGrain<IKitchenTicketGrain>(
            GrainKeys.KitchenOrder(orgId, siteId, ticketId));

        await ticket.CreateAsync(new CreateKitchenTicketCommand(
            orgId, siteId, Guid.NewGuid(), "ORD-001",
            OrderType.DineIn, "T1", 4, "Server"));

        // Act - Add 10 items concurrently
        var tasks = Enumerable.Range(1, 10).Select(i =>
            ticket.AddItemAsync(new AddTicketItemCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                $"Item {i}",
                1)));

        await Task.WhenAll(tasks);

        // Assert - All items should be added
        var state = await ticket.GetStateAsync();
        state.Items.Should().HaveCount(10);
    }

    #endregion

    #region Menu Concurrent Tests

    [Fact]
    public async Task MenuItem_ConcurrentModifierOperations_ShouldHandleCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var key = $"{orgId}:menuitem:{itemId}";
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuItemGrain>(key);

        await grain.CreateAsync(new CreateMenuItemCommand(
            LocationId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: null,
            RecipeId: null,
            Name: "Coffee",
            Description: null,
            Price: 4.00m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        // Act - Add multiple modifiers concurrently
        var tasks = Enumerable.Range(1, 5).Select(i =>
            grain.AddModifierAsync(new MenuItemModifier(
                ModifierId: Guid.NewGuid(),
                Name: $"Modifier {i}",
                PriceAdjustment: 0.50m * i,
                IsRequired: false,
                MinSelections: 0,
                MaxSelections: 1,
                Options: new List<MenuItemModifierOption>
                {
                    new(Guid.NewGuid(), $"Option {i}A", 0m, true),
                    new(Guid.NewGuid(), $"Option {i}B", 0.25m, false)
                })));

        await Task.WhenAll(tasks);

        // Assert - All modifiers should be added
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Modifiers.Should().HaveCount(5);
    }

    [Fact]
    public async Task MenuCategory_ConcurrentItemCountUpdates_ShouldBeAccurate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var key = $"{orgId}:menucategory:{categoryId}";
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuCategoryGrain>(key);

        await grain.CreateAsync(new CreateMenuCategoryCommand(
            LocationId: Guid.NewGuid(),
            Name: "Appetizers",
            Description: null,
            DisplayOrder: 1,
            Color: null));

        // Act - Increment item count concurrently
        var incrementTasks = Enumerable.Range(1, 20)
            .Select(_ => grain.IncrementItemCountAsync());
        await Task.WhenAll(incrementTasks);

        // Decrement some concurrently
        var decrementTasks = Enumerable.Range(1, 5)
            .Select(_ => grain.DecrementItemCountAsync());
        await Task.WhenAll(decrementTasks);

        // Assert - Should have 15 items (20 - 5)
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemCount.Should().Be(15);
    }

    #endregion

    #region Payment Concurrent Tests

    [Fact]
    public async Task Payment_ConcurrentInitiations_ShouldCreateSeparatePayments()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        // Create order first
        var orderGrain = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(orgId, siteId, orderId));
        await orderGrain.CreateAsync(new CreateOrderCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            CreatedBy: Guid.NewGuid(),
            Type: OrderType.DineIn,
            GuestCount: 2));
        await orderGrain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Item", 1, 100.00m));
        await orderGrain.SendAsync(Guid.NewGuid());

        // Act - Create multiple payments concurrently
        var paymentIds = Enumerable.Range(1, 4).Select(_ => Guid.NewGuid()).ToList();
        var tasks = paymentIds.Select(paymentId =>
        {
            var paymentGrain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(
                GrainKeys.Payment(orgId, siteId, paymentId));
            return paymentGrain.InitiateAsync(new InitiatePaymentCommand(
                OrderId: orderId,
                Method: "Cash",
                Amount: 25.00m,
                Currency: "USD"));
        });

        await Task.WhenAll(tasks);

        // Assert - All payments should be created
        foreach (var paymentId in paymentIds)
        {
            var paymentGrain = _fixture.Cluster.GrainFactory.GetGrain<IPaymentGrain>(
                GrainKeys.Payment(orgId, siteId, paymentId));
            var state = await paymentGrain.GetStateAsync();
            state.Amount.Should().Be(25.00m);
        }
    }

    #endregion

    #region Multi-Grain Concurrent Tests

    [Fact]
    public async Task MultiGrain_ConcurrentOrdersToSameTable_ShouldAllSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tableId = Guid.NewGuid();

        // Act - Create multiple orders for the same table concurrently
        var orderIds = Enumerable.Range(1, 5).Select(_ => Guid.NewGuid()).ToList();
        var tasks = orderIds.Select(orderId =>
        {
            var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(
                GrainKeys.Order(orgId, siteId, orderId));
            return grain.CreateAsync(new CreateOrderCommand(
                OrganizationId: orgId,
                SiteId: siteId,
                CreatedBy: Guid.NewGuid(),
                Type: OrderType.DineIn,
                GuestCount: 2,
                TableId: tableId));
        });

        await Task.WhenAll(tasks);

        // Assert - All orders should be created
        foreach (var orderId in orderIds)
        {
            var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(
                GrainKeys.Order(orgId, siteId, orderId));
            var exists = await grain.ExistsAsync();
            exists.Should().BeTrue();
        }
    }

    [Fact]
    public async Task MultiGrain_RapidFireOperations_ShouldMaintainConsistency()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(orgId, siteId, orderId));

        await grain.CreateAsync(new CreateOrderCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            CreatedBy: Guid.NewGuid(),
            Type: OrderType.DineIn,
            GuestCount: 2));

        // Act - Rapid sequence of operations
        var addTasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            addTasks.Add(grain.AddLineAsync(new AddLineCommand(
                MenuItemId: Guid.NewGuid(),
                Name: $"Item {i}",
                Quantity: 1,
                UnitPrice: 5.00m)));
        }

        await Task.WhenAll(addTasks);

        // Assert
        var state = await grain.GetStateAsync();
        state.Lines.Should().HaveCount(50);
        state.GrandTotal.Should().Be(250.00m); // 50 items * $5
    }

    #endregion
}
