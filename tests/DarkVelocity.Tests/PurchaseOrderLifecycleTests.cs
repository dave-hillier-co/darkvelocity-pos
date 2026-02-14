using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Purchase order lifecycle tests covering:
/// - Complete workflow from draft to received
/// - State transition validations
/// - Edge cases in order modifications
/// - Under/over delivery scenarios
/// - Supplier metrics integration
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PurchaseOrderLifecycleTests
{
    private readonly TestClusterFixture _fixture;

    public PurchaseOrderLifecycleTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IPurchaseOrderGrain GetPOGrain(Guid orgId, Guid poId)
    {
        var key = $"{orgId}:purchaseorder:{poId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IPurchaseOrderGrain>(key);
    }

    private ISupplierGrain GetSupplierGrain(Guid orgId, Guid supplierId)
    {
        var key = $"{orgId}:supplier:{supplierId}";
        return _fixture.Cluster.GrainFactory.GetGrain<ISupplierGrain>(key);
    }

    // ============================================================================
    // Complete Workflow Tests
    // ============================================================================

    // Given: a draft purchase order with produce line items
    // When: the order is submitted and all lines are fully received in sequence
    // Then: the PO transitions through Draft, Submitted, PartiallyReceived, and Received statuses
    [Fact]
    public async Task CompleteWorkflow_DraftToReceived_ShouldTransitionCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetPOGrain(orgId, poId);

        // Draft
        var createResult = await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), userId,
            DateTime.UtcNow.AddDays(3), "Weekly produce order"));

        createResult.Status.Should().Be(PurchaseOrderStatus.Draft);
        createResult.OrderNumber.Should().StartWith("PO-");

        // Add lines
        var line1 = Guid.NewGuid();
        var line2 = Guid.NewGuid();
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(line1, Guid.NewGuid(), "SKU", "Tomatoes", 50, 2.00m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(line2, Guid.NewGuid(), "SKU", "Onions", 30, 1.00m, null));

        var draftSnapshot = await grain.GetSnapshotAsync();
        draftSnapshot.OrderTotal.Should().Be(130m); // 100 + 30

        // Submit
        var submitResult = await grain.SubmitAsync(new SubmitPurchaseOrderCommand(userId));
        submitResult.Status.Should().Be(PurchaseOrderStatus.Submitted);
        submitResult.SubmittedAt.Should().NotBeNull();

        // Receive line 1 fully
        await grain.ReceiveLineAsync(new ReceiveLineCommand(line1, 50));

        var partialSnapshot = await grain.GetSnapshotAsync();
        partialSnapshot.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);

        // Receive line 2 fully
        await grain.ReceiveLineAsync(new ReceiveLineCommand(line2, 30));

        var finalSnapshot = await grain.GetSnapshotAsync();
        finalSnapshot.Status.Should().Be(PurchaseOrderStatus.Received);
        finalSnapshot.ReceivedAt.Should().NotBeNull();
        (await grain.IsFullyReceivedAsync()).Should().BeTrue();
    }

    // Given: a submitted purchase order with line items
    // When: the order is cancelled due to supplier being out of stock
    // Then: the PO moves to Cancelled status with the cancellation reason and timestamp recorded
    [Fact]
    public async Task CompleteWorkflow_DraftToCancelled_ShouldTransitionCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetPOGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), userId,
            DateTime.UtcNow.AddDays(3), null));

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "SKU", "Item", 10, 5.00m, null));

        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(userId));

        // Act
        var cancelResult = await grain.CancelAsync(new CancelPurchaseOrderCommand(
            "Supplier out of stock", userId));

        // Assert
        cancelResult.Status.Should().Be(PurchaseOrderStatus.Cancelled);
        cancelResult.CancelledAt.Should().NotBeNull();
        cancelResult.CancellationReason.Should().Be("Supplier out of stock");
    }

    // ============================================================================
    // Draft Modification Tests
    // ============================================================================

    // Given: a draft purchase order with one line item
    // When: the line quantity, unit price, and notes are updated
    // Then: the line reflects the new values and the order total is recalculated
    [Fact]
    public async Task Draft_CanModifyLines_BeforeSubmission()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetPOGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId, Guid.NewGuid(), "SKU", "Test Item", 10, 5.00m, "Initial note"));

        // Act - update
        await grain.UpdateLineAsync(new UpdatePurchaseOrderLineCommand(
            lineId, 20, 6.00m, "Updated note"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines[0].QuantityOrdered.Should().Be(20);
        snapshot.Lines[0].UnitPrice.Should().Be(6.00m);
        snapshot.Lines[0].Notes.Should().Be("Updated note");
        snapshot.OrderTotal.Should().Be(120m); // 20 * 6
    }

    // Given: a draft purchase order with two line items
    // When: one line item is removed
    // Then: only the remaining line exists and the order total reflects the removal
    [Fact]
    public async Task Draft_CanRemoveLines_BeforeSubmission()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId1 = Guid.NewGuid();
        var lineId2 = Guid.NewGuid();
        var grain = GetPOGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(lineId1, Guid.NewGuid(), "SKU", "Item A", 10, 5.00m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(lineId2, Guid.NewGuid(), "SKU", "Item B", 20, 3.00m, null));

        // Act
        await grain.RemoveLineAsync(lineId1);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines.Should().HaveCount(1);
        snapshot.Lines[0].ProductName.Should().Be("Item B");
        snapshot.OrderTotal.Should().Be(60m);
    }

    // Given: a draft purchase order with no line items
    // When: multiple lines are added and removed in sequence
    // Then: the final line count and order total reflect all add/remove operations correctly
    [Fact]
    public async Task Draft_CanAddRemoveAddLines_MultipleOperations()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var grain = GetPOGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        // Act - multiple add/remove operations
        var line1 = Guid.NewGuid();
        var line2 = Guid.NewGuid();
        var line3 = Guid.NewGuid();

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(line1, Guid.NewGuid(), "SKU", "Item 1", 10, 1.00m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(line2, Guid.NewGuid(), "SKU", "Item 2", 20, 2.00m, null));
        await grain.RemoveLineAsync(line1);
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(line3, Guid.NewGuid(), "SKU", "Item 3", 30, 3.00m, null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines.Should().HaveCount(2);
        snapshot.OrderTotal.Should().Be(40 + 90); // 130
    }

    // ============================================================================
    // Under-Delivery Tests
    // ============================================================================

    // Given: a submitted purchase order for 100 units of an ingredient
    // When: only 80 units are received from the supplier
    // Then: the PO remains in PartiallyReceived status with the shortfall recorded
    [Fact]
    public async Task Receive_UnderDelivery_ShouldAcceptPartial()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetPOGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId, Guid.NewGuid(), "SKU", "Test Item", 100, 5.00m, null));

        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act - receive less than ordered
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 80));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines[0].QuantityOrdered.Should().Be(100);
        snapshot.Lines[0].QuantityReceived.Should().Be(80);

        // Per negative stock philosophy, we accept what we get
        // Status stays PartiallyReceived until full quantity received
        snapshot.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
    }

    // Given: a partially received purchase order with 60 of 100 units delivered
    // When: the remaining 40 units are received in a follow-up delivery
    // Then: the PO moves to Received status with the full ordered quantity
    [Fact]
    public async Task Receive_UnderDelivery_CanContinueReceiving()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetPOGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId, Guid.NewGuid(), "SKU", "Test Item", 100, 5.00m, null));

        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // First partial receive
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 60));

        // Act - continue receiving
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 40));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines[0].QuantityReceived.Should().Be(100);
        snapshot.Status.Should().Be(PurchaseOrderStatus.Received);
    }

    // ============================================================================
    // Over-Delivery Tests (Per Negative Stock Philosophy)
    // ============================================================================

    // Given: a submitted purchase order for 50 units of an ingredient
    // When: the supplier delivers 60 units (over-delivery)
    // Then: the PO accepts the excess quantity and moves to Received status
    [Fact]
    public async Task Receive_OverDelivery_ShouldAcceptExtraQuantity()
    {
        // Arrange - Per CLAUDE.md: "Negative stock is the default"
        // Receiving more than ordered is tracked (opposite of shortage)
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetPOGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId, Guid.NewGuid(), "SKU", "Bonus Items", 50, 5.00m, null));

        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act - receive more than ordered
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 60));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines[0].QuantityOrdered.Should().Be(50);
        snapshot.Lines[0].QuantityReceived.Should().Be(60);
        snapshot.Status.Should().Be(PurchaseOrderStatus.Received);
    }

    // Given: a submitted purchase order for 30 units of an ingredient
    // When: three separate deliveries totaling 45 units are received
    // Then: the received quantity accumulates beyond the ordered amount across all receipts
    [Fact]
    public async Task Receive_OverDelivery_MultipleReceipts_ShouldAccumulate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetPOGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId, Guid.NewGuid(), "SKU", "Test Item", 30, 5.00m, null));

        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act - receive in parts, exceeding ordered amount
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 20));
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 15));
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 10));

        // Assert - 45 received for 30 ordered
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines[0].QuantityOrdered.Should().Be(30);
        snapshot.Lines[0].QuantityReceived.Should().Be(45);
    }

    // ============================================================================
    // State Validation Tests
    // ============================================================================

    // Given: a purchase order that has already been submitted to the supplier
    // When: adding a new line item is attempted
    // Then: the operation is rejected because the PO is no longer in draft status
    [Fact]
    public async Task SubmittedPO_CannotAddLines()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var grain = GetPOGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "SKU", "Initial", 10, 5.00m, null));

        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act & Assert
        var act = () => grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "SKU", "Late Add", 5, 3.00m, null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*submitted*");
    }

    // Given: a purchase order that has been cancelled
    // When: receiving a delivery against the cancelled PO is attempted
    // Then: the operation is rejected because goods cannot be received against a cancelled order
    [Fact]
    public async Task CancelledPO_CannotReceive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetPOGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId, Guid.NewGuid(), "SKU", "Item", 10, 5.00m, null));

        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));
        await grain.CancelAsync(new CancelPurchaseOrderCommand("Cancelled", Guid.NewGuid()));

        // Act & Assert
        var act = () => grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 10));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot receive*");
    }

    // Given: a purchase order that has been fully received
    // When: cancellation is attempted after all goods have arrived
    // Then: the operation is rejected because a received PO cannot be cancelled
    [Fact]
    public async Task ReceivedPO_CannotCancel()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetPOGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId, Guid.NewGuid(), "SKU", "Item", 10, 5.00m, null));

        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 10));

        // Verify fully received
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PurchaseOrderStatus.Received);

        // Act & Assert
        var act = () => grain.CancelAsync(new CancelPurchaseOrderCommand("Want to cancel", Guid.NewGuid()));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot cancel*");
    }

    // ============================================================================
    // Order Number and Metadata Tests
    // ============================================================================

    // Given: three new purchase orders for the same organization
    // When: all three orders are created
    // Then: each receives a unique PO number with the standard "PO-" prefix
    [Fact]
    public async Task CreatePO_ShouldGenerateUniqueOrderNumber()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var po1 = Guid.NewGuid();
        var po2 = Guid.NewGuid();
        var po3 = Guid.NewGuid();

        var grain1 = GetPOGrain(orgId, po1);
        var grain2 = GetPOGrain(orgId, po2);
        var grain3 = GetPOGrain(orgId, po3);

        // Act
        var result1 = await grain1.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(3), null));
        var result2 = await grain2.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(3), null));
        var result3 = await grain3.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(3), null));

        // Assert
        result1.OrderNumber.Should().StartWith("PO-");
        result2.OrderNumber.Should().StartWith("PO-");
        result3.OrderNumber.Should().StartWith("PO-");

        // Order numbers should be unique
        var orderNumbers = new[] { result1.OrderNumber, result2.OrderNumber, result3.OrderNumber };
        orderNumbers.Distinct().Count().Should().Be(3);
    }

    // Given: a new purchase order
    // When: created with a scheduled delivery date and procurement notes
    // Then: the order records the expected delivery schedule and notes
    [Fact]
    public async Task CreatePO_ShouldTrackExpectedDeliveryDate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var expectedDate = DateTime.UtcNow.AddDays(5).Date;
        var grain = GetPOGrain(orgId, poId);

        // Act
        var result = await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            expectedDate, "Scheduled delivery"));

        // Assert
        result.ExpectedDeliveryDate.Date.Should().Be(expectedDate);
        result.Notes.Should().Be("Scheduled delivery");
    }

    // ============================================================================
    // Total Calculation Tests
    // ============================================================================

    // Given: a draft purchase order with three line items at varying quantities and prices
    // When: the order total is requested
    // Then: the total equals the sum of all line totals
    [Fact]
    public async Task GetTotal_ShouldReturnCorrectSum()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var grain = GetPOGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "SKU", "Item A", 10, 5.00m, null)); // 50
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "SKU", "Item B", 20, 2.50m, null)); // 50
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "SKU", "Item C", 5, 20.00m, null)); // 100

        // Act
        var total = await grain.GetTotalAsync();

        // Assert
        total.Should().Be(200m);
    }

    // Given: a draft purchase order with one line of 7 units at a fractional unit price
    // When: the line total is calculated
    // Then: the total is the precise product of quantity and unit price
    [Fact]
    public async Task LineTotal_ShouldCalculateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var grain = GetPOGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "SKU", "Precision Item", 7, 13.57m, null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines[0].LineTotal.Should().Be(94.99m); // 7 * 13.57
    }

    // ============================================================================
    // Multi-Line Receive Tests
    // ============================================================================

    // Given: a submitted purchase order with three line items
    // When: one line is fully received, one partially received, and one not received at all
    // Then: each line tracks its received quantity independently and the PO remains PartiallyReceived
    [Fact]
    public async Task MultiLineReceive_MixedProgress_ShouldTrackSeparately()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var line1 = Guid.NewGuid();
        var line2 = Guid.NewGuid();
        var line3 = Guid.NewGuid();
        var grain = GetPOGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(line1, Guid.NewGuid(), "SKU", "Fast Delivery", 100, 1.00m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(line2, Guid.NewGuid(), "SKU", "Slow Delivery", 50, 2.00m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(line3, Guid.NewGuid(), "SKU", "No Delivery", 25, 4.00m, null));

        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act - receive at different rates
        await grain.ReceiveLineAsync(new ReceiveLineCommand(line1, 100)); // Complete
        await grain.ReceiveLineAsync(new ReceiveLineCommand(line2, 25)); // Partial
        // line3 not received

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);

        var fastLine = snapshot.Lines.First(l => l.LineId == line1);
        fastLine.QuantityReceived.Should().Be(100);

        var slowLine = snapshot.Lines.First(l => l.LineId == line2);
        slowLine.QuantityReceived.Should().Be(25);

        var noLine = snapshot.Lines.First(l => l.LineId == line3);
        noLine.QuantityReceived.Should().Be(0);
    }

    // Given: a submitted purchase order with five line items at varying quantities
    // When: all five lines are received in full
    // Then: the PO moves to Received status and is marked as fully received
    [Fact]
    public async Task MultiLineReceive_AllComplete_ShouldMarkReceived()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lines = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var grain = GetPOGrain(orgId, poId);

        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        for (int i = 0; i < lines.Count; i++)
        {
            await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
                lines[i], Guid.NewGuid(), "SKU", $"Item {i}", 10 + i, 1.00m + i * 0.50m, null));
        }

        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act - receive all lines
        for (int i = 0; i < lines.Count; i++)
        {
            await grain.ReceiveLineAsync(new ReceiveLineCommand(lines[i], 10 + i));
        }

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PurchaseOrderStatus.Received);
        (await grain.IsFullyReceivedAsync()).Should().BeTrue();
    }
}
