using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class InventoryTransferGrainTests
{
    private readonly TestClusterFixture _fixture;

    public InventoryTransferGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IInventoryGrain> CreateInventoryAsync(Guid orgId, Guid siteId, Guid ingredientId, string name = "Test Ingredient")
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
        await grain.InitializeAsync(new InitializeInventoryCommand(orgId, siteId, ingredientId, name, $"SKU-{ingredientId.ToString()[..8]}", "units", "General", 10, 50));
        return grain;
    }

    // Given: A new inter-site inventory transfer with one line item for ground beef
    // When: The transfer is requested with source site, destination site, and 25 lb of ground beef
    // Then: The transfer is created with status Requested and the line item records the requested quantity
    [Fact]
    public async Task RequestAsync_ShouldCreateTransfer()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryTransferGrain>(GrainKeys.InventoryTransfer(orgId, transferId));

        // Act
        await grain.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId,
            "TRN-001", userId,
            Lines: [new TransferLineRequest(ingredientId, "Ground Beef", 25, "lb")]));

        // Assert
        var state = await grain.GetStateAsync();
        state.TransferNumber.Should().Be("TRN-001");
        state.Status.Should().Be(TransferStatus.Requested);
        state.SourceSiteId.Should().Be(sourceSiteId);
        state.DestinationSiteId.Should().Be(destSiteId);
        state.Lines.Should().HaveCount(1);
        state.Lines[0].RequestedQuantity.Should().Be(25);
    }

    // Given: A pending inventory transfer between two sites
    // When: A manager approves the transfer
    // Then: The transfer status transitions to Approved with the approver recorded
    [Fact]
    public async Task ApproveAsync_ShouldTransitionToApproved()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryTransferGrain>(GrainKeys.InventoryTransfer(orgId, transferId));
        await grain.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId,
            "TRN-001", userId,
            Lines: [new TransferLineRequest(ingredientId, "Ground Beef", 25, "lb")]));

        // Act
        await grain.ApproveAsync(new ApproveTransferCommand(approverId, "Approved for transfer"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(TransferStatus.Approved);
        state.ApprovedBy.Should().Be(approverId);
    }

    // Given: A pending inventory transfer between two sites
    // When: The transfer is rejected due to insufficient stock at the source site
    // Then: The transfer status transitions to Rejected with the rejection reason recorded
    [Fact]
    public async Task RejectAsync_ShouldTransitionToRejected()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryTransferGrain>(GrainKeys.InventoryTransfer(orgId, transferId));
        await grain.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId,
            "TRN-001", userId,
            Lines: [new TransferLineRequest(ingredientId, "Ground Beef", 25, "lb")]));

        // Act
        await grain.RejectAsync(new RejectTransferCommand(userId, "Insufficient stock at source"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(TransferStatus.Rejected);
        state.RejectionReason.Should().Be("Insufficient stock at source");
    }

    // Given: An approved transfer of 25 lb ground beef from a source site with 100 units on hand
    // When: The transfer is shipped with a tracking number
    // Then: The transfer status transitions to Shipped and source inventory is deducted to 75 units
    [Fact]
    public async Task ShipAsync_ShouldDeductFromSourceInventory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        // Create inventory at source
        await CreateInventoryAsync(orgId, sourceSiteId, ingredientId, "Ground Beef");
        var sourceInventory = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, sourceSiteId, ingredientId));
        await sourceInventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryTransferGrain>(GrainKeys.InventoryTransfer(orgId, transferId));
        await grain.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId,
            "TRN-001", userId,
            Lines: [new TransferLineRequest(ingredientId, "Ground Beef", 25, "lb")]));
        await grain.ApproveAsync(new ApproveTransferCommand(userId));

        // Act
        await grain.ShipAsync(new ShipTransferCommand(userId, TrackingNumber: "TRACK123"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(TransferStatus.Shipped);
        state.TrackingNumber.Should().Be("TRACK123");
        state.TotalShippedValue.Should().Be(125); // 25 * 5.00

        // Verify source inventory was deducted
        var sourceLevel = await sourceInventory.GetLevelInfoAsync();
        sourceLevel.QuantityOnHand.Should().Be(75); // 100 - 25
    }

    // Given: A shipped transfer of 25 lb ground beef with destination inventory initialized
    // When: The transfer receipt is finalized at the destination site
    // Then: The transfer status transitions to Received and destination inventory is credited with 25 units
    [Fact]
    public async Task FinalizeReceiptAsync_ShouldCreditDestinationInventory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        // Create inventory at source
        await CreateInventoryAsync(orgId, sourceSiteId, ingredientId, "Ground Beef");
        var sourceInventory = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, sourceSiteId, ingredientId));
        await sourceInventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        // Create inventory at destination
        await CreateInventoryAsync(orgId, destSiteId, ingredientId, "Ground Beef");
        var destInventory = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, destSiteId, ingredientId));

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryTransferGrain>(GrainKeys.InventoryTransfer(orgId, transferId));
        await grain.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId,
            "TRN-001", userId,
            Lines: [new TransferLineRequest(ingredientId, "Ground Beef", 25, "lb")]));
        await grain.ApproveAsync(new ApproveTransferCommand(userId));
        await grain.ShipAsync(new ShipTransferCommand(userId));

        // Act
        await grain.FinalizeReceiptAsync(new FinalizeTransferReceiptCommand(userId));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(TransferStatus.Received);

        // Verify destination inventory was credited
        var destLevel = await destInventory.GetLevelInfoAsync();
        destLevel.QuantityOnHand.Should().Be(25);
    }

    // Given: A shipped transfer of 25 lb ground beef between two sites
    // When: The destination receives only 23 units due to transit damage
    // Then: A negative variance of -2 units is recorded for the transfer line
    [Fact]
    public async Task ReceiveItemAsync_WithVariance_ShouldTrackDifference()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        // Create inventory at source
        await CreateInventoryAsync(orgId, sourceSiteId, ingredientId, "Ground Beef");
        var sourceInventory = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, sourceSiteId, ingredientId));
        await sourceInventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        // Create inventory at destination
        await CreateInventoryAsync(orgId, destSiteId, ingredientId, "Ground Beef");

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryTransferGrain>(GrainKeys.InventoryTransfer(orgId, transferId));
        await grain.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId,
            "TRN-001", userId,
            Lines: [new TransferLineRequest(ingredientId, "Ground Beef", 25, "lb")]));
        await grain.ApproveAsync(new ApproveTransferCommand(userId));
        await grain.ShipAsync(new ShipTransferCommand(userId));

        // Act - receive 23 instead of 25 (2 units short)
        await grain.ReceiveItemAsync(new ReceiveTransferItemCommand(ingredientId, 23, userId, Condition: "Good", Notes: "2 units damaged"));

        // Assert
        var variances = await grain.GetVariancesAsync();
        variances.Should().HaveCount(1);
        variances[0].ShippedQuantity.Should().Be(25);
        variances[0].ReceivedQuantity.Should().Be(23);
        variances[0].Variance.Should().Be(-2);
    }

    // Given: A shipped transfer that deducted 25 units from source inventory (100 down to 75)
    // When: The transfer is cancelled with stock return to source enabled
    // Then: The transfer status transitions to Cancelled and source inventory is restored to 100 units
    [Fact]
    public async Task CancelAsync_AfterShipped_ShouldReturnStock()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sourceSiteId = Guid.NewGuid();
        var destSiteId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        // Create inventory at source
        await CreateInventoryAsync(orgId, sourceSiteId, ingredientId, "Ground Beef");
        var sourceInventory = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, sourceSiteId, ingredientId));
        await sourceInventory.ReceiveBatchAsync(new ReceiveBatchCommand("BATCH001", 100, 5.00m));

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryTransferGrain>(GrainKeys.InventoryTransfer(orgId, transferId));
        await grain.RequestAsync(new RequestTransferCommand(
            orgId, sourceSiteId, destSiteId,
            "TRN-001", userId,
            Lines: [new TransferLineRequest(ingredientId, "Ground Beef", 25, "lb")]));
        await grain.ApproveAsync(new ApproveTransferCommand(userId));
        await grain.ShipAsync(new ShipTransferCommand(userId));

        // Verify stock was deducted
        var levelAfterShip = await sourceInventory.GetLevelInfoAsync();
        levelAfterShip.QuantityOnHand.Should().Be(75);

        // Act
        await grain.CancelAsync(new CancelTransferCommand(userId, "Transfer cancelled", ReturnStockToSource: true));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(TransferStatus.Cancelled);
        state.StockReturnedToSource.Should().BeTrue();

        // Verify stock was returned
        var levelAfterCancel = await sourceInventory.GetLevelInfoAsync();
        levelAfterCancel.QuantityOnHand.Should().Be(100); // Back to original
    }
}
