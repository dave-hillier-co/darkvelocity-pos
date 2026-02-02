using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for performance boundaries and large collection handling.
/// These tests verify the system behaves correctly with larger data volumes.
/// </summary>
[Collection(ClusterCollection.Name)]
public class PerformanceBoundaryTests
{
    private readonly TestClusterFixture _fixture;

    public PerformanceBoundaryTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    #region Large Order Tests

    [Fact]
    public async Task Order_ManyLines_ShouldHandleCorrectly()
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
            GuestCount: 20));

        // Act - Add 100 line items (large party with many items)
        for (int i = 0; i < 100; i++)
        {
            await grain.AddLineAsync(new AddLineCommand(
                MenuItemId: Guid.NewGuid(),
                Name: $"Item {i + 1}",
                Quantity: 1,
                UnitPrice: 10.00m + (i % 10)));
        }

        // Assert
        var state = await grain.GetStateAsync();
        state.Lines.Should().HaveCount(100);
        state.GrandTotal.Should().BeGreaterThan(0);

        var lines = await grain.GetLinesAsync();
        lines.Should().HaveCount(100);
    }

    [Fact]
    public async Task Order_ManyModifiersPerLine_ShouldCalculateCorrectly()
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

        // Act - Add item with many modifiers
        var modifiers = Enumerable.Range(1, 20).Select(i =>
            new OrderLineModifier(
                Id: Guid.NewGuid(),
                Name: $"Modifier {i}",
                Price: 0.50m)).ToList();

        var result = await grain.AddLineAsync(new AddLineCommand(
            MenuItemId: Guid.NewGuid(),
            Name: "Customized Item",
            Quantity: 1,
            UnitPrice: 15.00m,
            Modifiers: modifiers));

        // Assert - Base price + 20 modifiers at $0.50 each = $25
        result.LineTotal.Should().Be(25.00m);
    }

    [Fact]
    public async Task Order_ManyPayments_ShouldTrackAll()
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
            GuestCount: 10));

        // Add items totaling $500
        for (int i = 0; i < 50; i++)
        {
            await grain.AddLineAsync(new AddLineCommand(
                MenuItemId: Guid.NewGuid(),
                Name: $"Item {i}",
                Quantity: 1,
                UnitPrice: 10.00m));
        }

        await grain.SendAsync(Guid.NewGuid());

        // Act - 10 people each pay $50
        for (int i = 0; i < 10; i++)
        {
            await grain.RecordPaymentAsync(
                Guid.NewGuid(),
                50.00m,
                5.00m,  // Tip
                "Cash");
        }

        // Assert
        var state = await grain.GetStateAsync();
        state.Payments.Should().HaveCount(10);
        state.TotalPaid.Should().Be(500.00m);
        state.TotalTips.Should().Be(50.00m);
        state.BalanceDue.Should().Be(0m);
    }

    #endregion

    #region Large Inventory Tests

    [Fact]
    public async Task Inventory_ManyBatches_ShouldTrackAllWithFIFO()
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
            ProductName: "Multi-Batch Item",
            Unit: "ea",
            ReorderPoint: 50,
            ReorderQty: 100,
            TrackingMethod: InventoryTrackingMethod.Batch,
            CostingMethod: CostingMethod.FIFO));

        // Act - Receive 30 batches of varying quantities
        for (int i = 0; i < 30; i++)
        {
            await grain.ReceiveBatchAsync(new ReceiveBatchCommand(
                BatchNumber: $"BATCH-{i:D3}",
                Quantity: 10 + i,
                UnitCost: 1.00m + (i * 0.05m),
                ReceivedAt: DateTime.UtcNow.AddDays(-30 + i),
                ExpiresAt: DateTime.UtcNow.AddDays(60 + i)));
        }

        // Assert - Total should be sum of 10+11+12+...+39 = 30*10 + sum(0..29) = 300 + 435 = 735
        var levelInfo = await grain.GetLevelInfoAsync();
        levelInfo.OnHand.Should().Be(735);

        var batches = await grain.GetActiveBatchesAsync();
        batches.Should().HaveCount(30);
    }

    [Fact]
    public async Task Inventory_ConsumeThroughMultipleBatches_ShouldUseFIFO()
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
            ProductName: "FIFO Test Item",
            Unit: "ea",
            ReorderPoint: 10,
            ReorderQty: 50,
            TrackingMethod: InventoryTrackingMethod.Batch,
            CostingMethod: CostingMethod.FIFO));

        // Add 5 batches of 20 each = 100 total
        for (int i = 0; i < 5; i++)
        {
            await grain.ReceiveBatchAsync(new ReceiveBatchCommand(
                BatchNumber: $"BATCH-{i}",
                Quantity: 20,
                UnitCost: 1.00m + (i * 0.10m),
                ReceivedAt: DateTime.UtcNow.AddDays(-5 + i),
                ExpiresAt: DateTime.UtcNow.AddDays(30)));
        }

        // Act - Consume 75 units (should exhaust first 3 batches + 15 from 4th)
        await grain.ConsumeAsync(new ConsumeInventoryCommand(
            Quantity: 75,
            OrderId: Guid.NewGuid(),
            Reason: "Large order"));

        // Assert - Should have 25 units remaining across batches 4 and 5
        var levelInfo = await grain.GetLevelInfoAsync();
        levelInfo.OnHand.Should().Be(25);

        var batches = await grain.GetActiveBatchesAsync();
        batches.Where(b => b.RemainingQuantity > 0).Should().HaveCountGreaterThanOrEqualTo(2);
    }

    #endregion

    #region Large Customer History Tests

    [Fact]
    public async Task Customer_ManyVisits_ShouldTrackHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerGrain>(
            GrainKeys.Customer(orgId, customerId));

        await grain.CreateAsync(new CreateCustomerCommand(
            OrganizationId: orgId,
            Email: "frequent@customer.com",
            FirstName: "Frequent",
            LastName: "Visitor"));

        // Act - Record 50 visits over time
        for (int i = 0; i < 50; i++)
        {
            await grain.RecordVisitAsync(new RecordVisitCommand(
                VisitId: Guid.NewGuid(),
                SiteId: Guid.NewGuid(),
                VisitedAt: DateTime.UtcNow.AddDays(-50 + i),
                SpendAmount: 25.00m + (i % 20),
                OrderCount: 1,
                ItemCount: 3));
        }

        // Assert
        var history = await grain.GetVisitHistoryAsync();
        history.Should().HaveCountGreaterThanOrEqualTo(1);

        var state = await grain.GetStateAsync();
        state.TotalVisits.Should().Be(50);
    }

    [Fact]
    public async Task Customer_ManyTags_ShouldHandleAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerGrain>(
            GrainKeys.Customer(orgId, customerId));

        await grain.CreateAsync(new CreateCustomerCommand(
            OrganizationId: orgId,
            Email: "tagged@customer.com",
            FirstName: "Tagged",
            LastName: "User"));

        // Act - Add 20 different tags
        var tags = new[]
        {
            "VIP", "Regular", "Birthday Club", "Wine Club", "Early Bird",
            "Late Night", "Weekend Regular", "Weekday Regular", "Group Host",
            "Corporate", "Event Planner", "Reviewer", "Influencer", "Local",
            "Tourist", "Catering", "Takeout Preferred", "Dine-In Only",
            "Allergen Alert", "Special Requests"
        };

        foreach (var tag in tags)
        {
            await grain.AddTagAsync(tag);
        }

        // Assert
        var state = await grain.GetStateAsync();
        state.Tags.Should().HaveCount(20);
    }

    [Fact]
    public async Task CustomerSpend_HighVolumeSpending_ShouldAccumulateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));

        await grain.InitializeAsync(orgId, customerId);

        // Act - Record 100 transactions
        for (int i = 0; i < 100; i++)
        {
            await grain.RecordSpendAsync(new RecordSpendCommand(
                OrderId: Guid.NewGuid(),
                SiteId: Guid.NewGuid(),
                NetSpend: 50m,
                GrossSpend: 54m,
                DiscountAmount: 0m,
                ItemCount: 3,
                TransactionDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-100 + i))));
        }

        // Assert - 100 transactions at $50 each = $5000
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.CumulativeSpend.Should().Be(5000m);
        snapshot.AvailablePoints.Should().Be(5000); // 1 point per $1
        snapshot.TotalTransactions.Should().Be(100);

        // Should be Gold tier ($5000 > $2000 threshold)
        snapshot.CurrentTier.Should().Be("Gold");
    }

    #endregion

    #region Large Kitchen Tests

    [Fact]
    public async Task KitchenTicket_ManyItems_ShouldTrackAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticket = _fixture.Cluster.GrainFactory.GetGrain<IKitchenTicketGrain>(
            GrainKeys.KitchenOrder(orgId, siteId, ticketId));

        await ticket.CreateAsync(new CreateKitchenTicketCommand(
            orgId, siteId, Guid.NewGuid(), "ORD-LARGE",
            OrderType.DineIn, "T1", 15, "Server"));

        // Act - Add 50 items across different stations
        var stations = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        for (int i = 0; i < 50; i++)
        {
            await ticket.AddItemAsync(new AddTicketItemCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                $"Item {i + 1}",
                (i % 3) + 1,  // Quantities 1-3
                null,
                null,
                stations[i % 3],
                $"Station {i % 3}"));
        }

        // Assert
        var state = await ticket.GetStateAsync();
        state.Items.Should().HaveCount(50);
        state.AssignedStationIds.Should().HaveCount(3);
    }

    [Fact]
    public async Task KitchenStation_ManyActiveTickets_ShouldTrackAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = _fixture.Cluster.GrainFactory.GetGrain<IKitchenStationGrain>(
            GrainKeys.KitchenStation(orgId, siteId, stationId));

        await station.OpenAsync(new OpenStationCommand(
            orgId, siteId, "Busy Station", StationType.Grill, 1));

        // Create 30 tickets
        var ticketIds = new List<Guid>();
        for (int i = 0; i < 30; i++)
        {
            var ticketId = Guid.NewGuid();
            ticketIds.Add(ticketId);
            var ticket = _fixture.Cluster.GrainFactory.GetGrain<IKitchenTicketGrain>(
                GrainKeys.KitchenOrder(orgId, siteId, ticketId));
            await ticket.CreateAsync(new CreateKitchenTicketCommand(
                orgId, siteId, Guid.NewGuid(), $"ORD-{i:D3}",
                OrderType.DineIn, $"T{i}", 2, "Server"));
        }

        // Act - Add all tickets to station
        foreach (var ticketId in ticketIds)
        {
            await station.ReceiveTicketAsync(ticketId);
        }

        // Assert
        var currentTickets = await station.GetCurrentTicketIdsAsync();
        currentTickets.Should().HaveCount(30);
    }

    #endregion

    #region Large Menu Tests

    [Fact]
    public async Task MenuItem_ManyVariations_ShouldHandleAll()
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
            Name: "Multi-Variation Item",
            Description: "Item with many sizes",
            Price: 10.00m,
            ImageUrl: null,
            Sku: "MVI-001",
            TrackInventory: false));

        // Act - Add 15 variations
        for (int i = 0; i < 15; i++)
        {
            await grain.AddVariationAsync(new CreateMenuItemVariationCommand(
                Name: $"Size {i + 1}",
                PricingType: PricingType.Fixed,
                Price: 10.00m + (i * 2.00m),
                Sku: $"MVI-001-{i + 1}",
                DisplayOrder: i + 1));
        }

        // Assert
        var variations = await grain.GetVariationsAsync();
        variations.Should().HaveCount(15);
        variations.First().Price.Should().Be(10.00m);
        variations.Last().Price.Should().Be(38.00m);
    }

    [Fact]
    public async Task MenuItem_ManyModifierGroups_ShouldHandleAll()
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
            Name: "Highly Customizable Item",
            Description: null,
            Price: 15.00m,
            ImageUrl: null,
            Sku: null,
            TrackInventory: false));

        // Act - Add 10 modifier groups, each with 5 options
        for (int g = 0; g < 10; g++)
        {
            var options = Enumerable.Range(1, 5).Select(o =>
                new MenuItemModifierOption(
                    Guid.NewGuid(),
                    $"Option {g + 1}-{o}",
                    o * 0.25m,
                    o == 1)).ToList();

            await grain.AddModifierAsync(new MenuItemModifier(
                ModifierId: Guid.NewGuid(),
                Name: $"Modifier Group {g + 1}",
                PriceAdjustment: 0,
                IsRequired: g < 3,  // First 3 required
                MinSelections: g < 3 ? 1 : 0,
                MaxSelections: g < 3 ? 1 : 3,
                Options: options));
        }

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Modifiers.Should().HaveCount(10);
        snapshot.Modifiers.SelectMany(m => m.Options).Should().HaveCount(50);
    }

    [Fact]
    public async Task MenuDefinition_ManyScreensAndButtons_ShouldHandleAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var key = $"{orgId}:menudef:{menuId}";
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuDefinitionGrain>(key);

        await grain.CreateAsync(new CreateMenuDefinitionCommand(
            LocationId: Guid.NewGuid(),
            Name: "Large POS Menu",
            Description: "Menu with many screens",
            IsDefault: true));

        // Act - Add 10 screens
        for (int s = 0; s < 10; s++)
        {
            var screenId = Guid.NewGuid();
            var buttons = Enumerable.Range(0, 24).Select(b =>
                new MenuButtonDefinition(
                    Guid.NewGuid(),
                    Guid.NewGuid(),  // MenuItemId
                    null,
                    b / 6,  // Row
                    b % 6,  // Column
                    $"Item {s * 24 + b + 1}",
                    $"#{s:X2}{b:X2}FF",
                    "Item")).ToList();

            await grain.AddScreenAsync(new MenuScreenDefinition(
                screenId,
                $"Screen {s + 1}",
                s + 1,
                null,
                4,
                6,
                buttons));
        }

        // Assert - 10 screens with 24 buttons each = 240 buttons
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Screens.Should().HaveCount(10);
        snapshot.Screens.SelectMany(s => s.Buttons).Should().HaveCount(240);
    }

    #endregion

    #region Large Booking Tests

    [Fact]
    public async Task BookingSettings_ManyBlockedDates_ShouldHandleAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));

        await grain.InitializeAsync(new InitializeBookingSettingsCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            DefaultSlotDuration: TimeSpan.FromMinutes(90),
            MaxAdvanceBookingDays: 60,
            MinAdvanceBookingMinutes: 60,
            MaxPartySize: 20));

        // Act - Block 30 dates (holidays, private events, etc.)
        for (int i = 0; i < 30; i++)
        {
            await grain.BlockDateAsync(new BlockDateCommand(
                Date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(i * 2)),
                Reason: $"Blocked reason {i + 1}",
                BlockedBy: Guid.NewGuid()));
        }

        // Assert
        var state = await grain.GetStateAsync();
        state.BlockedDates.Should().HaveCount(30);
    }

    #endregion

    #region Large Procurement Tests

    [Fact]
    public async Task PurchaseOrder_ManyLines_ShouldCalculateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPurchaseOrderGrain>(
            GrainKeys.PurchaseOrder(orgId, poId));

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            OrgId: orgId,
            SiteId: siteId,
            SupplierId: supplierId,
            SupplierName: "Large Order Supplier",
            ExpectedDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            Notes: "Large order test"));

        // Act - Add 50 line items
        decimal expectedTotal = 0;
        for (int i = 0; i < 50; i++)
        {
            var qty = (i % 10) + 1;
            var price = 5.00m + (i % 20);
            expectedTotal += qty * price;

            await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
                IngredientId: Guid.NewGuid(),
                IngredientName: $"Ingredient {i + 1}",
                Quantity: qty,
                Unit: "ea",
                UnitPrice: price));
        }

        // Assert
        var state = await grain.GetStateAsync();
        state.Lines.Should().HaveCount(50);

        var total = await grain.GetTotalAsync();
        total.Should().Be(expectedTotal);
    }

    [Fact]
    public async Task Supplier_ManyIngredients_ShouldCatalogAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISupplierGrain>(
            GrainKeys.Supplier(orgId, supplierId));

        await grain.CreateAsync(new CreateSupplierCommand(
            OrganizationId: orgId,
            Name: "Large Catalog Supplier",
            ContactName: "Contact",
            Email: "supplier@test.com",
            Phone: "555-0000",
            Address: "123 Main St"));

        // Act - Add 100 ingredients to supplier catalog
        for (int i = 0; i < 100; i++)
        {
            await grain.AddIngredientAsync(new AddSupplierIngredientCommand(
                IngredientId: Guid.NewGuid(),
                IngredientName: $"Ingredient {i + 1}",
                SupplierSku: $"SKU-{i + 1:D4}",
                UnitPrice: 1.00m + (i * 0.10m),
                MinOrderQuantity: (i % 5) + 1,
                Unit: "ea",
                LeadTimeDays: (i % 7) + 1));
        }

        // Assert
        var state = await grain.GetStateAsync();
        state.Ingredients.Should().HaveCount(100);
    }

    #endregion

    #region Bulk Operation Tests

    [Fact]
    public async Task MenuEngineering_BulkRecordSales_ShouldHandleLargeVolume()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMenuEngineeringGrain>(
            GrainKeys.MenuEngineering(orgId, siteId));

        await grain.InitializeAsync(new InitializeMenuEngineeringCommand(
            OrgId: orgId,
            SiteId: siteId,
            SiteName: "Bulk Test Site",
            TargetMarginPercent: 70m));

        // Act - Record sales for 50 different products
        var commands = Enumerable.Range(1, 50).Select(i =>
            new RecordItemSalesCommand(
                ProductId: Guid.NewGuid(),
                ProductName: $"Product {i}",
                Category: $"Category {(i % 5) + 1}",
                SellingPrice: 10.00m + i,
                TheoreticalCost: 3.00m + (i * 0.1m),
                UnitsSold: i * 10,
                TotalRevenue: (10.00m + i) * (i * 10))).ToList();

        await grain.BulkRecordSalesAsync(commands);

        // Assert
        var items = await grain.GetItemAnalysisAsync();
        items.Should().HaveCount(50);

        var categories = await grain.GetCategoryAnalysisAsync();
        categories.Should().HaveCount(5);
    }

    #endregion
}
