using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Partial delivery handling tests covering:
/// - Multi-delivery scenarios for single PO
/// - Partial receives with discrepancies
/// - Delivery-to-inventory integration
/// - Complex delivery workflows
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PartialDeliveryTests
{
    private readonly TestClusterFixture _fixture;

    public PartialDeliveryTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IPurchaseOrderGrain GetPurchaseOrderGrain(Guid orgId, Guid poId)
    {
        var key = $"{orgId}:purchaseorder:{poId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IPurchaseOrderGrain>(key);
    }

    private IDeliveryGrain GetDeliveryGrain(Guid orgId, Guid deliveryId)
    {
        var key = $"{orgId}:delivery:{deliveryId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IDeliveryGrain>(key);
    }

    // ============================================================================
    // Partial Receive on PO Tests
    // ============================================================================

    // Given: A submitted purchase order with a single line of 100 units of ground beef
    // When: The line is received in three deliveries (30, 40, 30 units)
    // Then: The PO transitions through PartiallyReceived to Received as quantities accumulate to 100
    [Fact]
    public async Task PurchaseOrder_PartialReceive_SingleItem_ShouldTrackProgress()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetPurchaseOrderGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(5), "Weekly order"));

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId, Guid.NewGuid(), "Ground Beef", 100, 5.00m, null));

        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act - receive in parts
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 30));

        // Assert
        var snapshot1 = await grain.GetSnapshotAsync();
        snapshot1.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
        snapshot1.Lines[0].QuantityReceived.Should().Be(30);
        (await grain.IsFullyReceivedAsync()).Should().BeFalse();

        // Continue receiving
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 40));

        var snapshot2 = await grain.GetSnapshotAsync();
        snapshot2.Lines[0].QuantityReceived.Should().Be(70);
        snapshot2.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);

        // Final receive
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 30));

        var snapshot3 = await grain.GetSnapshotAsync();
        snapshot3.Lines[0].QuantityReceived.Should().Be(100);
        snapshot3.Status.Should().Be(PurchaseOrderStatus.Received);
        (await grain.IsFullyReceivedAsync()).Should().BeTrue();
    }

    // Given: A submitted purchase order with three lines (chicken 50, beef 30, pork 20)
    // When: Chicken is fully received, beef is partially received, and pork is not yet received
    // Then: Each line tracks its received quantity independently and the PO remains PartiallyReceived
    [Fact]
    public async Task PurchaseOrder_PartialReceive_MultipleItems_ShouldTrackSeparately()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId1 = Guid.NewGuid();
        var lineId2 = Guid.NewGuid();
        var lineId3 = Guid.NewGuid();
        var grain = GetPurchaseOrderGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(lineId1, Guid.NewGuid(), "Chicken", 50, 4.00m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(lineId2, Guid.NewGuid(), "Beef", 30, 8.00m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(lineId3, Guid.NewGuid(), "Pork", 20, 6.00m, null));

        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act - receive items in different patterns
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId1, 50)); // Complete
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId2, 15)); // Partial
        // lineId3 not received yet

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);

        snapshot.Lines.First(l => l.LineId == lineId1).QuantityReceived.Should().Be(50);
        snapshot.Lines.First(l => l.LineId == lineId2).QuantityReceived.Should().Be(15);
        snapshot.Lines.First(l => l.LineId == lineId3).QuantityReceived.Should().Be(0);
    }

    // Given: A submitted purchase order for 1,000 small parts
    // When: The line is received across 10 deliveries of 100 units each
    // Then: The accumulated received quantity reaches 1,000 and the PO status transitions to Received
    [Fact]
    public async Task PurchaseOrder_ReceiveInManySmallDeliveries_ShouldAccumulate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetPurchaseOrderGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(7), null));

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId, Guid.NewGuid(), "Small Parts", 1000, 0.50m, null));

        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act - receive in 10 deliveries
        for (int i = 0; i < 10; i++)
        {
            await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 100));
        }

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines[0].QuantityReceived.Should().Be(1000);
        snapshot.Status.Should().Be(PurchaseOrderStatus.Received);
    }

    // ============================================================================
    // Delivery With Discrepancies Tests
    // ============================================================================

    // Given: A delivery with a line for tomatoes where 80 units were received
    // When: A short delivery discrepancy is recorded (expected 100, actual 80)
    // Then: The delivery is flagged with a ShortDelivery discrepancy showing expected vs actual quantities
    [Fact]
    public async Task Delivery_ShortWithDiscrepancy_ShouldRecordBoth()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetDeliveryGrain(orgId, deliveryId);

        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-001", null));

        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId, Guid.NewGuid(), "Tomatoes",
            null, 80, 2.00m, null, null, null));

        // Act - record short delivery discrepancy
        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId, DiscrepancyType.ShortDelivery,
            100, 80, "Ordered 100, received 80"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.HasDiscrepancies.Should().BeTrue();
        snapshot.Discrepancies.Should().HaveCount(1);

        var discrepancy = snapshot.Discrepancies[0];
        discrepancy.Type.Should().Be(DiscrepancyType.ShortDelivery);
        discrepancy.ExpectedQuantity.Should().Be(100);
        discrepancy.ActualQuantity.Should().Be(80);
    }

    // Given: A delivery with three lines (milk, eggs, butter)
    // When: Discrepancies are recorded: short delivery on milk, damaged goods on eggs, incorrect price on butter
    // Then: All three discrepancy types are tracked on the delivery
    [Fact]
    public async Task Delivery_MultipleDiscrepancyTypes_ShouldTrackAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var lineId1 = Guid.NewGuid();
        var lineId2 = Guid.NewGuid();
        var lineId3 = Guid.NewGuid();
        var grain = GetDeliveryGrain(orgId, deliveryId);

        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), null, Guid.NewGuid(),
            Guid.NewGuid(), "INV-002", null));

        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId1, Guid.NewGuid(), "Milk", null, 20, 3.00m, null, null, null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId2, Guid.NewGuid(), "Eggs", null, 10, 4.00m, null, null, null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId3, Guid.NewGuid(), "Butter", null, 15, 5.00m, null, null, null));

        // Act - record different discrepancy types
        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId1, DiscrepancyType.ShortDelivery, 24, 20, "4 short"));

        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId2, DiscrepancyType.DamagedGoods, 12, 10, "2 cracked"));

        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId3, DiscrepancyType.IncorrectPrice, 15, 15, "Price was $6 not $5"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.HasDiscrepancies.Should().BeTrue();
        snapshot.Discrepancies.Should().HaveCount(3);

        snapshot.Discrepancies.Select(d => d.Type).Should().Contain(
            DiscrepancyType.ShortDelivery,
            DiscrepancyType.DamagedGoods,
            DiscrepancyType.IncorrectPrice);
    }

    // Given: A delivery with a line for 120 potatoes received against an expected 100
    // When: An over-delivery discrepancy is recorded
    // Then: The discrepancy shows OverDelivery type with expected 100 and actual 120
    [Fact]
    public async Task Delivery_OverDelivery_ShouldBeTrackedAsDiscrepancy()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetDeliveryGrain(orgId, deliveryId);

        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-003", null));

        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId, Guid.NewGuid(), "Potatoes",
            Guid.NewGuid(), 120, 0.50m, null, null, null));

        // Act - record over-delivery
        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId, DiscrepancyType.OverDelivery,
            100, 120, "Extra 20 units delivered"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Discrepancies[0].Type.Should().Be(DiscrepancyType.OverDelivery);
        snapshot.Discrepancies[0].ActualQuantity.Should().Be(120);
        snapshot.Discrepancies[0].ExpectedQuantity.Should().Be(100);
    }

    // ============================================================================
    // Accept/Reject Flow Tests
    // ============================================================================

    // Given: A delivery with a recorded short delivery discrepancy on cheese (50 expected, 45 received)
    // When: The delivery is accepted despite the discrepancy
    // Then: The delivery status transitions to Accepted while preserving the discrepancy record
    [Fact]
    public async Task Delivery_AcceptWithDiscrepancies_ShouldMaintainDiscrepancyRecords()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetDeliveryGrain(orgId, deliveryId);

        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-004", null));

        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId, Guid.NewGuid(), "Cheese", null, 45, 8.00m, null, null, null));

        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId, DiscrepancyType.ShortDelivery, 50, 45, "5 blocks short"));

        // Act - accept despite discrepancy
        await grain.AcceptAsync(new AcceptDeliveryCommand(Guid.NewGuid()));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(DeliveryStatus.Accepted);
        snapshot.HasDiscrepancies.Should().BeTrue();
        snapshot.Discrepancies.Should().HaveCount(1);
    }

    // Given: A delivery of fresh fish with all product expired and a quality issue discrepancy recorded
    // When: The entire delivery is rejected
    // Then: The delivery status transitions to Rejected with the rejection reason noting expired product
    [Fact]
    public async Task Delivery_RejectDueToQualityIssue_ShouldMarkAsRejected()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetDeliveryGrain(orgId, deliveryId);

        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-005", null));

        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId, Guid.NewGuid(), "Fresh Fish", null, 25, 15.00m,
            "BATCH-FISH-001", DateTime.UtcNow.AddDays(-2), null)); // Already expired

        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId, DiscrepancyType.QualityIssue, 25, 0, "All product expired"));

        // Act
        await grain.RejectAsync(new RejectDeliveryCommand(
            "Entire delivery rejected due to expired product",
            Guid.NewGuid()));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(DeliveryStatus.Rejected);
        snapshot.RejectionReason.Should().Contain("expired");
    }

    // ============================================================================
    // Direct Delivery (No PO) Tests
    // ============================================================================

    // Given: A walk-in vendor delivering goods without an existing purchase order
    // When: A direct delivery is created with no PO reference
    // Then: The delivery is created in Pending status with no purchase order association
    [Fact]
    public async Task Delivery_WithoutPO_ShouldCreateDirectDelivery()
    {
        // Arrange - "walk-in" vendor or emergency order
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetDeliveryGrain(orgId, deliveryId);

        // Act
        var result = await grain.CreateAsync(new CreateDeliveryCommand(
            SupplierId: supplierId,
            PurchaseOrderId: null, // No PO
            LocationId: locationId,
            ReceivedByUserId: Guid.NewGuid(),
            SupplierInvoiceNumber: "WALKIN-001",
            Notes: "Emergency order - farmer's market vendor"));

        // Assert
        result.PurchaseOrderId.Should().BeNull();
        result.SupplierInvoiceNumber.Should().Be("WALKIN-001");
        result.Status.Should().Be(DeliveryStatus.Pending);
    }

    // Given: A direct delivery from a farmer's market vendor with no purchase order
    // When: Three items are added (organic tomatoes, fresh herbs, local honey)
    // Then: All three lines are recorded with a total delivery value of $173
    [Fact]
    public async Task Delivery_DirectDelivery_CanAddMultipleItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var grain = GetDeliveryGrain(orgId, deliveryId);

        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), null, Guid.NewGuid(),
            Guid.NewGuid(), "FM-001", "Farmer's market"));

        // Act - add various items from direct delivery
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Organic Tomatoes",
            null, 30, 3.50m, null, DateTime.UtcNow.AddDays(5), null));

        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Fresh Herbs",
            null, 10, 2.00m, null, DateTime.UtcNow.AddDays(3), null));

        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Local Honey",
            null, 6, 8.00m, "HONEY-2024-001", DateTime.UtcNow.AddYears(1), null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines.Should().HaveCount(3);
        snapshot.TotalValue.Should().Be(105m + 20m + 48m); // 173
    }

    // ============================================================================
    // Batch and Expiry Tracking Tests
    // ============================================================================

    // Given: A delivery receiving perishable goods (cream and yogurt) with batch tracking
    // When: Lines are added with batch numbers and expiry dates
    // Then: Each line records its batch number and expiry date for traceability
    [Fact]
    public async Task Delivery_WithBatchInfo_ShouldTrackBatchNumbers()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var grain = GetDeliveryGrain(orgId, deliveryId);

        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-006", null));

        var expiryDate1 = DateTime.UtcNow.AddDays(14);
        var expiryDate2 = DateTime.UtcNow.AddDays(7);

        // Act
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Cream",
            null, 24, 3.50m, "CREAM-2024-001", expiryDate1, "Store in cooler"));

        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Yogurt",
            null, 48, 1.25m, "YOGURT-2024-002", expiryDate2, null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines.Should().HaveCount(2);

        snapshot.Lines[0].BatchNumber.Should().Be("CREAM-2024-001");
        snapshot.Lines[0].ExpiryDate.Should().BeCloseTo(expiryDate1, TimeSpan.FromSeconds(1));

        snapshot.Lines[1].BatchNumber.Should().Be("YOGURT-2024-002");
        snapshot.Lines[1].ExpiryDate.Should().BeCloseTo(expiryDate2, TimeSpan.FromSeconds(1));
    }

    // Given: A delivery containing perishable milk and non-perishable napkins
    // When: Lines are added with batch info for milk but none for napkins
    // Then: Milk has batch number and expiry date while napkins have neither
    [Fact]
    public async Task Delivery_MixedBatchedAndUnbatched_ShouldHandle()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var grain = GetDeliveryGrain(orgId, deliveryId);

        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), null, Guid.NewGuid(),
            Guid.NewGuid(), "INV-007", null));

        // Act
        // Batched item
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Milk",
            null, 24, 3.00m, "MILK-BATCH-001", DateTime.UtcNow.AddDays(10), null));

        // Unbatched item (no expiry)
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Napkins",
            null, 500, 0.02m, null, null, null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines.Should().HaveCount(2);

        snapshot.Lines[0].BatchNumber.Should().Be("MILK-BATCH-001");
        snapshot.Lines[0].ExpiryDate.Should().NotBeNull();

        snapshot.Lines[1].BatchNumber.Should().BeNullOrEmpty();
        snapshot.Lines[1].ExpiryDate.Should().BeNull();
    }

    // ============================================================================
    // Total Calculation Tests
    // ============================================================================

    // Given: A delivery with three lines (Item A at $50, Item B at $50, Item C at $100)
    // When: The delivery snapshot is retrieved
    // Then: The total delivery value is calculated as $200
    [Fact]
    public async Task Delivery_TotalValue_ShouldSumAllLines()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var grain = GetDeliveryGrain(orgId, deliveryId);

        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), null, Guid.NewGuid(),
            Guid.NewGuid(), "INV-008", null));

        // Act
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Item A", null, 10, 5.00m, null, null, null)); // 50
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Item B", null, 20, 2.50m, null, null, null)); // 50
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Item C", null, 5, 20.00m, null, null, null)); // 100

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalValue.Should().Be(200m);
    }

    // Given: A delivery with a single line of 3 units at $125.50 each
    // When: The delivery snapshot is retrieved
    // Then: The line total is calculated as $376.50
    [Fact]
    public async Task Delivery_LineTotal_ShouldCalculateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var grain = GetDeliveryGrain(orgId, deliveryId);

        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), null, Guid.NewGuid(),
            Guid.NewGuid(), "INV-009", null));

        // Act
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Expensive Item",
            null, 3, 125.50m, null, null, null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines[0].QuantityReceived.Should().Be(3);
        snapshot.Lines[0].UnitCost.Should().Be(125.50m);
        snapshot.Lines[0].LineTotal.Should().Be(376.50m);
    }
}
