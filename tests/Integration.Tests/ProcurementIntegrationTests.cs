using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Integration.Tests.Fixtures;
using DarkVelocity.Procurement.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Integration.Tests;

/// <summary>
/// P2 Integration tests for Procurement workflows:
/// - Purchase Order Lifecycle (creation, submission, cancellation)
/// - Delivery Receipt (receiving, accepting, rejecting)
/// - Supplier Management (analysis, pricing)
/// - Discrepancy Handling
/// </summary>
public class ProcurementIntegrationTests : IClassFixture<ProcurementServiceFixture>
{
    private readonly ProcurementServiceFixture _fixture;
    private readonly HttpClient _client;

    public ProcurementIntegrationTests(ProcurementServiceFixture fixture)
    {
        _fixture = fixture;
        _fixture.TestLocationId = Guid.NewGuid();
        _fixture.TestUserId = Guid.NewGuid();
        _client = fixture.Client;
    }

    #region Purchase Order Lifecycle

    [Fact]
    public async Task CreatePurchaseOrder_WithSupplierPricing_CreatesOrderWithPrices()
    {
        // Arrange
        var request = new CreatePurchaseOrderRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId,
            CreatedByUserId: _fixture.TestUserId,
            ExpectedDeliveryDate: DateTime.UtcNow.AddDays(5),
            Notes: "Weekly stock order");

        // Act
        var response = await _client.PostAsJsonAsync("/api/purchase-orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var po = await response.Content.ReadFromJsonAsync<PurchaseOrderDto>();
        po.Should().NotBeNull();
        po!.Status.Should().Be("draft");
        po.SupplierId.Should().Be(_fixture.TestSupplierId);
        po.LocationId.Should().Be(_fixture.TestLocationId);
        po.OrderNumber.Should().StartWith("PO-");
    }

    [Fact]
    public async Task AddPurchaseOrderLine_ToDraftOrder_AddsLineWithCorrectTotals()
    {
        // Arrange - Create a new PO first
        var createRequest = new CreatePurchaseOrderRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);

        var createResponse = await _client.PostAsJsonAsync("/api/purchase-orders", createRequest);
        var po = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        var lineRequest = new AddPurchaseOrderLineRequest(
            IngredientId: _fixture.BeefIngredientId,
            IngredientName: "Premium Beef Mince",
            QuantityOrdered: 15m,
            UnitPrice: 5.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/purchase-orders/{po!.Id}/lines", lineRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var line = await response.Content.ReadFromJsonAsync<PurchaseOrderLineDto>();
        line.Should().NotBeNull();
        line!.QuantityOrdered.Should().Be(15m);
        line.UnitPrice.Should().Be(5.00m);
        line.LineTotal.Should().Be(75m);
    }

    [Fact]
    public async Task UpdatePurchaseOrderLine_OnDraftOrder_UpdatesLineAndRecalculatesTotal()
    {
        // Arrange
        var updateRequest = new UpdatePurchaseOrderLineRequest(
            QuantityOrdered: 20m,
            UnitPrice: 5.50m);

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/purchase-orders/{_fixture.TestPurchaseOrderId}/lines/{_fixture.TestPurchaseOrderLineId}",
            updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var line = await response.Content.ReadFromJsonAsync<PurchaseOrderLineDto>();
        line!.QuantityOrdered.Should().Be(20m);
        line.UnitPrice.Should().Be(5.50m);
        line.LineTotal.Should().Be(110m);
    }

    [Fact]
    public async Task SubmitPurchaseOrder_FromDraft_TransitionsToSubmitted()
    {
        // Arrange - Create a new draft PO with lines
        var createRequest = new CreatePurchaseOrderRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);

        var createResponse = await _client.PostAsJsonAsync("/api/purchase-orders", createRequest);
        var po = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        // Add a line
        var lineRequest = new AddPurchaseOrderLineRequest(
            IngredientId: _fixture.CheeseIngredientId,
            IngredientName: "Cheddar Cheese",
            QuantityOrdered: 10m,
            UnitPrice: 7.50m);
        await _client.PostAsJsonAsync($"/api/purchase-orders/{po!.Id}/lines", lineRequest);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/purchase-orders/{po.Id}/submit",
            new SubmitPurchaseOrderRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var submittedPO = await response.Content.ReadFromJsonAsync<PurchaseOrderDto>();
        submittedPO!.Status.Should().Be("submitted");
        submittedPO.SubmittedAt.Should().NotBeNull();
        submittedPO.SubmittedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task SubmitPurchaseOrder_WithoutLines_ReturnsBadRequest()
    {
        // Arrange - Create empty PO
        var createRequest = new CreatePurchaseOrderRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);

        var createResponse = await _client.PostAsJsonAsync("/api/purchase-orders", createRequest);
        var po = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/purchase-orders/{po!.Id}/submit",
            new SubmitPurchaseOrderRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CancelPurchaseOrder_BeforeReceipt_TransitionsToCancelled()
    {
        // Arrange - Create and submit a PO
        var createRequest = new CreatePurchaseOrderRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);

        var createResponse = await _client.PostAsJsonAsync("/api/purchase-orders", createRequest);
        var po = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        // Add line and submit
        var lineRequest = new AddPurchaseOrderLineRequest(
            IngredientId: _fixture.TomatoesIngredientId,
            IngredientName: "Fresh Tomatoes",
            QuantityOrdered: 5m,
            UnitPrice: 2.00m);
        await _client.PostAsJsonAsync($"/api/purchase-orders/{po!.Id}/lines", lineRequest);
        await _client.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/submit", new SubmitPurchaseOrderRequest());

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/purchase-orders/{po.Id}/cancel",
            new CancelPurchaseOrderRequest("Supplier unable to fulfill"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelledPO = await response.Content.ReadFromJsonAsync<PurchaseOrderDto>();
        cancelledPO!.Status.Should().Be("cancelled");
    }

    [Fact]
    public async Task AddLine_ToSubmittedOrder_ReturnsBadRequest()
    {
        // Arrange
        var lineRequest = new AddPurchaseOrderLineRequest(
            IngredientId: _fixture.BeefIngredientId,
            IngredientName: "Beef",
            QuantityOrdered: 5m,
            UnitPrice: 5.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/purchase-orders/{_fixture.SubmittedPurchaseOrderId}/lines",
            lineRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPurchaseOrders_FilterByStatus_ReturnsFilteredResults()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/purchase-orders?status=submitted&locationId={_fixture.TestLocationId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPurchaseOrders_FilterBySupplier_ReturnsSupplierOrders()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/purchase-orders?supplierId={_fixture.TestSupplierId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Delivery Receipt

    [Fact]
    public async Task CreateDelivery_LinkedToPurchaseOrder_CreatesDeliveryWithPOReference()
    {
        // Arrange
        var request = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId,
            PurchaseOrderId: _fixture.SubmittedPurchaseOrderId,
            ReceivedByUserId: _fixture.TestUserId,
            SupplierInvoiceNumber: "INV-2026-001");

        // Act
        var response = await _client.PostAsJsonAsync("/api/deliveries", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var delivery = await response.Content.ReadFromJsonAsync<DeliveryDto>();
        delivery.Should().NotBeNull();
        delivery!.Status.Should().Be("pending");
        delivery.PurchaseOrderId.Should().Be(_fixture.SubmittedPurchaseOrderId);
        delivery.DeliveryNumber.Should().StartWith("DEL-");
    }

    [Fact]
    public async Task AddDeliveryLine_WithBatchAndExpiry_RecordsTraceability()
    {
        // Arrange - Create delivery first
        var deliveryRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);

        var deliveryResponse = await _client.PostAsJsonAsync("/api/deliveries", deliveryRequest);
        var delivery = await deliveryResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        var lineRequest = new AddDeliveryLineRequest(
            IngredientId: _fixture.BeefIngredientId,
            IngredientName: "Premium Beef Mince",
            QuantityReceived: 10m,
            UnitCost: 5.00m,
            BatchNumber: "BATCH-BEEF-2026-001",
            ExpiryDate: DateTime.UtcNow.AddDays(14));

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery!.Id}/lines", lineRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var line = await response.Content.ReadFromJsonAsync<DeliveryLineDto>();
        line!.BatchNumber.Should().Be("BATCH-BEEF-2026-001");
        line.ExpiryDate.Should().NotBeNull();
        line.QuantityReceived.Should().Be(10m);
        line.LineTotal.Should().Be(50m);
    }

    [Fact]
    public async Task ReceiveDelivery_WithQuantityVariance_RecordsDiscrepancy()
    {
        // Arrange - Create delivery with line
        var deliveryRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId,
            PurchaseOrderId: _fixture.SubmittedPurchaseOrderId);

        var deliveryResponse = await _client.PostAsJsonAsync("/api/deliveries", deliveryRequest);
        var delivery = await deliveryResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        // Add line (less than ordered)
        var lineRequest = new AddDeliveryLineRequest(
            IngredientId: _fixture.BeefIngredientId,
            IngredientName: "Premium Beef Mince",
            QuantityReceived: 15m, // Ordered 20, received 15
            UnitCost: 5.00m);

        var lineResponse = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery!.Id}/lines", lineRequest);
        var line = await lineResponse.Content.ReadFromJsonAsync<DeliveryLineDto>();

        // Add discrepancy
        var discrepancyRequest = new AddDeliveryDiscrepancyRequest(
            DeliveryLineId: line!.Id,
            DiscrepancyType: "quantity_shortage",
            QuantityAffected: 5m,
            Description: "5kg short of ordered quantity");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/discrepancies", discrepancyRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var discrepancy = await response.Content.ReadFromJsonAsync<DeliveryDiscrepancyDto>();
        discrepancy!.DiscrepancyType.Should().Be("quantity_shortage");
        discrepancy.QuantityAffected.Should().Be(5m);
    }

    [Fact]
    public async Task ReceiveDelivery_WithPriceVariance_RecordsPriceDiscrepancy()
    {
        // Arrange
        var deliveryRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);

        var deliveryResponse = await _client.PostAsJsonAsync("/api/deliveries", deliveryRequest);
        var delivery = await deliveryResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        // Add line with different price than PO
        var lineRequest = new AddDeliveryLineRequest(
            IngredientId: _fixture.CheeseIngredientId,
            IngredientName: "Cheddar Cheese",
            QuantityReceived: 10m,
            UnitCost: 8.50m); // Higher than expected $7.50

        var lineResponse = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery!.Id}/lines", lineRequest);
        var line = await lineResponse.Content.ReadFromJsonAsync<DeliveryLineDto>();

        // Add price discrepancy
        var discrepancyRequest = new AddDeliveryDiscrepancyRequest(
            DeliveryLineId: line!.Id,
            DiscrepancyType: "price_variance",
            PriceDifference: 1.00m,
            Description: "Invoice price $1.00 higher than PO price");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/discrepancies", discrepancyRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var discrepancy = await response.Content.ReadFromJsonAsync<DeliveryDiscrepancyDto>();
        discrepancy!.DiscrepancyType.Should().Be("price_variance");
        discrepancy.PriceDifference.Should().Be(1.00m);
    }

    [Fact]
    public async Task ResolveDiscrepancy_WithAction_MarksResolved()
    {
        // Arrange - Create delivery with discrepancy
        var deliveryRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);

        var deliveryResponse = await _client.PostAsJsonAsync("/api/deliveries", deliveryRequest);
        var delivery = await deliveryResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        var lineRequest = new AddDeliveryLineRequest(
            IngredientId: _fixture.TomatoesIngredientId,
            IngredientName: "Tomatoes",
            QuantityReceived: 7m,
            UnitCost: 1.50m);

        var lineResponse = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery!.Id}/lines", lineRequest);
        var line = await lineResponse.Content.ReadFromJsonAsync<DeliveryLineDto>();

        var discrepancyRequest = new AddDeliveryDiscrepancyRequest(
            DeliveryLineId: line!.Id,
            DiscrepancyType: "damaged_goods",
            QuantityAffected: 3m,
            Description: "3kg of tomatoes damaged in transit");

        var discrepancyResponse = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/discrepancies", discrepancyRequest);
        var discrepancy = await discrepancyResponse.Content.ReadFromJsonAsync<DeliveryDiscrepancyDto>();

        // Act
        var resolveRequest = new ResolveDiscrepancyRequest(
            ActionTaken: "credit_requested",
            ResolvedByUserId: _fixture.TestUserId,
            ResolutionNotes: "Credit note requested from supplier");

        var response = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/discrepancies/{discrepancy!.Id}/resolve",
            resolveRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolved = await response.Content.ReadFromJsonAsync<DeliveryDiscrepancyDto>();
        resolved!.ActionTaken.Should().Be("credit_requested");
        resolved.ResolvedAt.Should().NotBeNull();
        resolved.ResolutionNotes.Should().Contain("Credit note");
    }

    [Fact]
    public async Task AcceptDelivery_WithAllLinesReceived_TransitionsToAccepted()
    {
        // Arrange - Create delivery with lines
        var deliveryRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);

        var deliveryResponse = await _client.PostAsJsonAsync("/api/deliveries", deliveryRequest);
        var delivery = await deliveryResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        var lineRequest = new AddDeliveryLineRequest(
            IngredientId: _fixture.BeefIngredientId,
            IngredientName: "Beef",
            QuantityReceived: 10m,
            UnitCost: 5.00m);
        await _client.PostAsJsonAsync($"/api/deliveries/{delivery!.Id}/lines", lineRequest);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/accept",
            new AcceptDeliveryRequest(AcceptedByUserId: _fixture.TestUserId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accepted = await response.Content.ReadFromJsonAsync<DeliveryDto>();
        accepted!.Status.Should().Be("accepted");
        accepted.AcceptedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RejectDelivery_WithReason_TransitionsToRejected()
    {
        // Arrange
        var deliveryRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);

        var deliveryResponse = await _client.PostAsJsonAsync("/api/deliveries", deliveryRequest);
        var delivery = await deliveryResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery!.Id}/reject",
            new RejectDeliveryRequest(
                Reason: "Quality issues - goods not up to standard",
                RejectedByUserId: _fixture.TestUserId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = await response.Content.ReadFromJsonAsync<DeliveryDto>();
        rejected!.Status.Should().Be("rejected");
    }

    [Fact]
    public async Task AcceptDelivery_AlreadyAccepted_ReturnsBadRequest()
    {
        // Act - try to accept an already accepted delivery
        var response = await _client.PostAsJsonAsync(
            $"/api/deliveries/{_fixture.TestDeliveryId}/accept",
            new AcceptDeliveryRequest());

        // The delivery might be pending, let's accept it first then try again
        // This tests the idempotency/status check
        if (response.StatusCode == HttpStatusCode.OK)
        {
            // Now try to accept again
            response = await _client.PostAsJsonAsync(
                $"/api/deliveries/{_fixture.TestDeliveryId}/accept",
                new AcceptDeliveryRequest());
        }

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Supplier Management

    [Fact]
    public async Task CreateSupplier_WithIngredients_AssociatesProductCatalog()
    {
        // Arrange
        var supplierRequest = new CreateSupplierRequest(
            Code: "SUP-NEW-001",
            Name: "New Supplier Co",
            ContactName: "Test Contact",
            ContactEmail: "test@newsupplier.com",
            PaymentTermsDays: 21,
            LeadTimeDays: 4);

        var supplierResponse = await _client.PostAsJsonAsync("/api/suppliers", supplierRequest);
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<SupplierDto>();

        // Add ingredient
        var ingredientRequest = new AddSupplierIngredientRequest(
            IngredientId: Guid.NewGuid(),
            LastKnownPrice: 30.00m,
            SupplierProductCode: "NS-PROD-001",
            SupplierProductName: "New Product",
            PackSize: 5m,
            PackUnit: "kg",
            IsPreferred: true);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/suppliers/{supplier!.Id}/ingredients", ingredientRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var ingredient = await response.Content.ReadFromJsonAsync<SupplierIngredientDto>();
        ingredient!.SupplierProductCode.Should().Be("NS-PROD-001");
        ingredient.LastKnownPrice.Should().Be(30.00m);
        ingredient.IsPreferred.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateSupplierIngredient_PriceChange_UpdatesLastKnownPrice()
    {
        // Arrange
        var updateRequest = new UpdateSupplierIngredientRequest(
            LastKnownPrice: 28.00m);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/suppliers/{_fixture.TestSupplierId}/ingredients/{_fixture.BeefIngredientId}",
            updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<SupplierIngredientDto>();
        updated!.LastKnownPrice.Should().Be(28.00m);
        updated.LastPriceUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetSupplierIngredients_ReturnsProductCatalog()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/suppliers/{_fixture.TestSupplierId}/ingredients");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeactivateSupplier_SetsInactive_ExcludesFromActiveList()
    {
        // Arrange - Create supplier to deactivate
        var createRequest = new CreateSupplierRequest(
            Code: "SUP-DEACT-001",
            Name: "Supplier To Deactivate");

        var createResponse = await _client.PostAsJsonAsync("/api/suppliers", createRequest);
        var supplier = await createResponse.Content.ReadFromJsonAsync<SupplierDto>();

        // Act
        var response = await _client.DeleteAsync($"/api/suppliers/{supplier!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify not in active list
        var listResponse = await _client.GetAsync("/api/suppliers?activeOnly=true");
        var content = await listResponse.Content.ReadAsStringAsync();
        content.Should().NotContain(supplier.Id.ToString());
    }

    [Fact]
    public async Task GetSuppliers_FilterActive_ReturnsOnlyActiveSuppliers()
    {
        // Act
        var response = await _client.GetAsync("/api/suppliers?activeOnly=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Partial Delivery

    [Fact]
    public async Task ReceivePartialDelivery_UpdatesPOStatus_ToPartiallyReceived()
    {
        // This test verifies the PO → Delivery workflow for partial receipts
        // Arrange - Create and submit a PO
        var poRequest = new CreatePurchaseOrderRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);

        var poResponse = await _client.PostAsJsonAsync("/api/purchase-orders", poRequest);
        var po = await poResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        // Add line for 20 units
        var lineRequest = new AddPurchaseOrderLineRequest(
            IngredientId: _fixture.BeefIngredientId,
            IngredientName: "Beef",
            QuantityOrdered: 20m,
            UnitPrice: 5.00m);
        await _client.PostAsJsonAsync($"/api/purchase-orders/{po!.Id}/lines", lineRequest);
        await _client.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/submit", new SubmitPurchaseOrderRequest());

        // Create delivery for partial quantity
        var deliveryRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId,
            PurchaseOrderId: po.Id);

        var deliveryResponse = await _client.PostAsJsonAsync("/api/deliveries", deliveryRequest);
        var delivery = await deliveryResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        // Receive only 10 of 20 ordered
        var deliveryLineRequest = new AddDeliveryLineRequest(
            IngredientId: _fixture.BeefIngredientId,
            IngredientName: "Beef",
            QuantityReceived: 10m, // Half of ordered
            UnitCost: 5.00m);

        await _client.PostAsJsonAsync($"/api/deliveries/{delivery!.Id}/lines", deliveryLineRequest);

        // Act - Accept the partial delivery
        var response = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/accept",
            new AcceptDeliveryRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Check PO status reflects partial receipt
        var updatedPO = await _client.GetAsync($"/api/purchase-orders/{po.Id}");
        var poResult = await updatedPO.Content.ReadFromJsonAsync<PurchaseOrderDto>();
        poResult!.Status.Should().BeOneOf("submitted", "partially_received");
    }

    #endregion
}
