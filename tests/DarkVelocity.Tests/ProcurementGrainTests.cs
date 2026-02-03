using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class SupplierGrainTests
{
    private readonly TestClusterFixture _fixture;

    public SupplierGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private ISupplierGrain GetGrain(Guid orgId, Guid supplierId)
    {
        var key = $"{orgId}:supplier:{supplierId}";
        return _fixture.Cluster.GrainFactory.GetGrain<ISupplierGrain>(key);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateSupplier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);

        // Act
        var result = await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP-001",
            Name: "Fresh Produce Co",
            ContactName: "John Smith",
            ContactEmail: "john@freshproduce.com",
            ContactPhone: "+1-555-0100",
            Address: "123 Farm Road, Countryside",
            PaymentTermsDays: 30,
            LeadTimeDays: 2,
            Notes: "Preferred produce supplier"));

        // Assert
        result.SupplierId.Should().Be(supplierId);
        result.Code.Should().Be("SUP-001");
        result.Name.Should().Be("Fresh Produce Co");
        result.ContactName.Should().Be("John Smith");
        result.ContactEmail.Should().Be("john@freshproduce.com");
        result.PaymentTermsDays.Should().Be(30);
        result.LeadTimeDays.Should().Be(2);
        result.IsActive.Should().BeTrue();
        result.Ingredients.Should().BeEmpty();
        result.TotalPurchasesYtd.Should().Be(0);
        result.OnTimeDeliveryPercent.Should().Be(100);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateSupplier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);
        await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP-002",
            Name: "Meat Suppliers Ltd",
            ContactName: "Jane Doe",
            ContactEmail: "jane@meatsuppliers.com",
            ContactPhone: "+1-555-0200",
            Address: "456 Industrial Ave",
            PaymentTermsDays: 14,
            LeadTimeDays: 1,
            Notes: null));

        // Act
        var result = await grain.UpdateAsync(new UpdateSupplierCommand(
            Name: "Premium Meat Suppliers",
            ContactName: "Jane Wilson",
            ContactEmail: null,
            ContactPhone: null,
            Address: null,
            PaymentTermsDays: 21,
            LeadTimeDays: null,
            Notes: "Updated contact name",
            IsActive: null));

        // Assert
        result.Name.Should().Be("Premium Meat Suppliers");
        result.ContactName.Should().Be("Jane Wilson");
        result.PaymentTermsDays.Should().Be(21);
        result.LeadTimeDays.Should().Be(1); // Unchanged
    }

    [Fact]
    public async Task AddIngredientAsync_ShouldAddIngredientToCatalog()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);
        await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP-003",
            Name: "Dairy Farm",
            ContactName: "Bob",
            ContactEmail: "bob@dairy.com",
            ContactPhone: "555-0300",
            Address: "789 Dairy Lane",
            PaymentTermsDays: 7,
            LeadTimeDays: 1,
            Notes: null));

        // Act
        await grain.AddIngredientAsync(new SupplierIngredient(
            IngredientId: ingredientId,
            IngredientName: "Whole Milk",
            Sku: "MILK-001",
            SupplierSku: "WM-GAL-01",
            UnitPrice: 3.50m,
            Unit: "gallon",
            MinOrderQuantity: 10,
            LeadTimeDays: 1));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Ingredients.Should().HaveCount(1);
        snapshot.Ingredients[0].IngredientName.Should().Be("Whole Milk");
        snapshot.Ingredients[0].UnitPrice.Should().Be(3.50m);
        snapshot.Ingredients[0].SupplierSku.Should().Be("WM-GAL-01");
    }

    [Fact]
    public async Task AddIngredientAsync_MultipleIngredients_ShouldAddAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);
        await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP-004",
            Name: "Bakery Supplies",
            ContactName: "Alice",
            ContactEmail: "alice@bakery.com",
            ContactPhone: "555-0400",
            Address: "321 Baker St",
            PaymentTermsDays: 14,
            LeadTimeDays: 3,
            Notes: null));

        // Act
        await grain.AddIngredientAsync(new SupplierIngredient(
            Guid.NewGuid(), "All-Purpose Flour", "FLR-001", "APF-50LB", 25.00m, "bag", 5, 3));
        await grain.AddIngredientAsync(new SupplierIngredient(
            Guid.NewGuid(), "Sugar", "SGR-001", "GS-25LB", 18.00m, "bag", 5, 3));
        await grain.AddIngredientAsync(new SupplierIngredient(
            Guid.NewGuid(), "Butter", "BTR-001", "UB-36CT", 85.00m, "case", 2, 2));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Ingredients.Should().HaveCount(3);
    }

    [Fact]
    public async Task AddIngredientAsync_ExistingIngredient_ShouldUpdateDetails()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);
        await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP-005",
            Name: "Seafood Direct",
            ContactName: "Mike",
            ContactEmail: "mike@seafood.com",
            ContactPhone: "555-0500",
            Address: "Harbor Rd",
            PaymentTermsDays: 7,
            LeadTimeDays: 1,
            Notes: null));

        await grain.AddIngredientAsync(new SupplierIngredient(
            ingredientId, "Salmon Fillet", "SAL-001", "SF-5LB", 45.00m, "lb", 10, 1));

        // Act
        await grain.AddIngredientAsync(new SupplierIngredient(
            ingredientId, "Salmon Fillet", "SAL-001", "SF-5LB-FRESH", 48.00m, "lb", 5, 1));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Ingredients.Should().HaveCount(1);
        snapshot.Ingredients[0].UnitPrice.Should().Be(48.00m);
        snapshot.Ingredients[0].SupplierSku.Should().Be("SF-5LB-FRESH");
        snapshot.Ingredients[0].MinOrderQuantity.Should().Be(5);
    }

    [Fact]
    public async Task RemoveIngredientAsync_ShouldRemoveFromCatalog()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var ingredientId1 = Guid.NewGuid();
        var ingredientId2 = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);
        await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP-006",
            Name: "Beverage Dist",
            ContactName: "Tom",
            ContactEmail: "tom@bev.com",
            ContactPhone: "555-0600",
            Address: "100 Drink Ave",
            PaymentTermsDays: 30,
            LeadTimeDays: 5,
            Notes: null));

        await grain.AddIngredientAsync(new SupplierIngredient(
            ingredientId1, "Cola Syrup", "COL-001", "CS-5GAL", 75.00m, "container", 2, 5));
        await grain.AddIngredientAsync(new SupplierIngredient(
            ingredientId2, "Orange Juice", "OJ-001", "OJ-CASE", 35.00m, "case", 5, 3));

        // Act
        await grain.RemoveIngredientAsync(ingredientId1);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Ingredients.Should().HaveCount(1);
        snapshot.Ingredients[0].IngredientName.Should().Be("Orange Juice");
    }

    [Fact]
    public async Task UpdateIngredientPriceAsync_ShouldUpdatePrice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);
        await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP-007",
            Name: "Coffee Roasters",
            ContactName: "Eva",
            ContactEmail: "eva@coffee.com",
            ContactPhone: "555-0700",
            Address: "99 Bean Blvd",
            PaymentTermsDays: 14,
            LeadTimeDays: 7,
            Notes: null));

        await grain.AddIngredientAsync(new SupplierIngredient(
            ingredientId, "Espresso Beans", "ESP-001", "EB-5LB", 65.00m, "bag", 3, 7));

        // Act
        await grain.UpdateIngredientPriceAsync(ingredientId, 72.50m);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Ingredients[0].UnitPrice.Should().Be(72.50m);
    }

    [Fact]
    public async Task GetIngredientPriceAsync_ShouldReturnPrice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);
        await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP-008",
            Name: "Spice Traders",
            ContactName: "Raj",
            ContactEmail: "raj@spice.com",
            ContactPhone: "555-0800",
            Address: "Spice Market",
            PaymentTermsDays: 30,
            LeadTimeDays: 14,
            Notes: null));

        await grain.AddIngredientAsync(new SupplierIngredient(
            ingredientId, "Saffron", "SAF-001", "SF-1OZ", 125.00m, "oz", 1, 14));

        // Act
        var price = await grain.GetIngredientPriceAsync(ingredientId);

        // Assert
        price.Should().Be(125.00m);
    }

    [Fact]
    public async Task RecordPurchaseAsync_ShouldUpdateMetrics()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);
        await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP-009",
            Name: "General Foods",
            ContactName: "Pat",
            ContactEmail: "pat@general.com",
            ContactPhone: "555-0900",
            Address: "1 Food Way",
            PaymentTermsDays: 30,
            LeadTimeDays: 3,
            Notes: null));

        // Act
        await grain.RecordPurchaseAsync(1500.00m, true);
        await grain.RecordPurchaseAsync(2000.00m, true);
        await grain.RecordPurchaseAsync(1000.00m, false);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalPurchasesYtd.Should().Be(4500.00m);
        snapshot.OnTimeDeliveryPercent.Should().Be(66); // 2 out of 3
    }

    [Fact]
    public async Task UpdateAsync_Deactivate_ShouldDeactivateSupplier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);
        await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP-010",
            Name: "Old Vendor",
            ContactName: "Nobody",
            ContactEmail: "none@old.com",
            ContactPhone: "555-0000",
            Address: "Gone",
            PaymentTermsDays: 30,
            LeadTimeDays: 30,
            Notes: null));

        // Act
        var result = await grain.UpdateAsync(new UpdateSupplierCommand(
            Name: null, ContactName: null, ContactEmail: null,
            ContactPhone: null, Address: null, PaymentTermsDays: null,
            LeadTimeDays: null, Notes: "No longer in business",
            IsActive: false));

        // Assert
        result.IsActive.Should().BeFalse();
        result.Notes.Should().Be("No longer in business");
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PurchaseOrderGrainTests
{
    private readonly TestClusterFixture _fixture;

    public PurchaseOrderGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IPurchaseOrderGrain GetGrain(Guid orgId, Guid poId)
    {
        var key = $"{orgId}:purchaseorder:{poId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IPurchaseOrderGrain>(key);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateDraftPurchaseOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);

        // Act
        var result = await grain.CreateAsync(new CreatePurchaseOrderCommand(
            SupplierId: supplierId,
            LocationId: locationId,
            CreatedByUserId: userId,
            ExpectedDeliveryDate: DateTime.UtcNow.AddDays(3),
            Notes: "Weekly produce order"));

        // Assert
        result.PurchaseOrderId.Should().Be(poId);
        result.SupplierId.Should().Be(supplierId);
        result.Status.Should().Be(PurchaseOrderStatus.Draft);
        result.OrderNumber.Should().StartWith("PO-");
        result.Lines.Should().BeEmpty();
        result.OrderTotal.Should().Be(0);
    }

    [Fact]
    public async Task AddLineAsync_ShouldAddLineToDraft()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);
        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        // Act
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            LineId: Guid.NewGuid(),
            IngredientId: Guid.NewGuid(),
            IngredientName: "Tomatoes",
            QuantityOrdered: 50,
            UnitPrice: 2.50m,
            Notes: "Roma preferred"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines.Should().HaveCount(1);
        snapshot.Lines[0].IngredientName.Should().Be("Tomatoes");
        snapshot.Lines[0].QuantityOrdered.Should().Be(50);
        snapshot.Lines[0].UnitPrice.Should().Be(2.50m);
        snapshot.Lines[0].LineTotal.Should().Be(125.00m);
        snapshot.OrderTotal.Should().Be(125.00m);
    }

    [Fact]
    public async Task AddLineAsync_MultipleLines_ShouldCalculateTotal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);
        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(5), null));

        // Act
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Chicken Breast", 100, 4.50m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Ground Beef", 75, 5.00m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Pork Chops", 50, 3.75m, null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines.Should().HaveCount(3);
        snapshot.OrderTotal.Should().Be(450.00m + 375.00m + 187.50m);
    }

    [Fact]
    public async Task UpdateLineAsync_ShouldUpdateLine()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);
        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId, Guid.NewGuid(), "Lettuce", 30, 1.50m, null));

        // Act
        await grain.UpdateLineAsync(new UpdatePurchaseOrderLineCommand(
            LineId: lineId,
            QuantityOrdered: 50,
            UnitPrice: 1.75m,
            Notes: "Increased quantity"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines[0].QuantityOrdered.Should().Be(50);
        snapshot.Lines[0].UnitPrice.Should().Be(1.75m);
        snapshot.Lines[0].LineTotal.Should().Be(87.50m);
        snapshot.OrderTotal.Should().Be(87.50m);
    }

    [Fact]
    public async Task RemoveLineAsync_ShouldRemoveLine()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId1 = Guid.NewGuid();
        var lineId2 = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);
        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId1, Guid.NewGuid(), "Onions", 25, 1.00m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId2, Guid.NewGuid(), "Garlic", 10, 3.00m, null));

        // Act
        await grain.RemoveLineAsync(lineId1);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines.Should().HaveCount(1);
        snapshot.Lines[0].IngredientName.Should().Be("Garlic");
        snapshot.OrderTotal.Should().Be(30.00m);
    }

    [Fact]
    public async Task SubmitAsync_ShouldChangeStatusToSubmitted()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);
        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Carrots", 40, 1.25m, null));

        // Act
        var result = await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Assert
        result.Status.Should().Be(PurchaseOrderStatus.Submitted);
        result.SubmittedAt.Should().NotBeNull();
        result.SubmittedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ReceiveLineAsync_PartialReceive_ShouldUpdateStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId1 = Guid.NewGuid();
        var lineId2 = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);
        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId1, Guid.NewGuid(), "Potatoes", 100, 0.50m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId2, Guid.NewGuid(), "Celery", 20, 2.00m, null));
        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId1, 100));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
        snapshot.Lines.First(l => l.LineId == lineId1).QuantityReceived.Should().Be(100);
        snapshot.Lines.First(l => l.LineId == lineId2).QuantityReceived.Should().Be(0);
    }

    [Fact]
    public async Task ReceiveLineAsync_FullReceive_ShouldMarkAsReceived()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId1 = Guid.NewGuid();
        var lineId2 = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);
        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId1, Guid.NewGuid(), "Apples", 50, 1.00m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId2, Guid.NewGuid(), "Oranges", 50, 1.25m, null));
        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId1, 50));
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId2, 50));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PurchaseOrderStatus.Received);
        snapshot.ReceivedAt.Should().NotBeNull();
        var isFullyReceived = await grain.IsFullyReceivedAsync();
        isFullyReceived.Should().BeTrue();
    }

    [Fact]
    public async Task ReceiveLineAsync_IncrementalReceive_ShouldAccumulate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);
        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId, Guid.NewGuid(), "Rice", 100, 1.50m, null));
        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 40));
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 35));
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 25));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines[0].QuantityReceived.Should().Be(100);
        snapshot.Status.Should().Be(PurchaseOrderStatus.Received);
    }

    [Fact]
    public async Task CancelAsync_ShouldCancelOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);
        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Mushrooms", 20, 5.00m, null));
        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act
        var result = await grain.CancelAsync(new CancelPurchaseOrderCommand(
            Reason: "Supplier out of stock",
            CancelledByUserId: Guid.NewGuid()));

        // Assert
        result.Status.Should().Be(PurchaseOrderStatus.Cancelled);
        result.CancelledAt.Should().NotBeNull();
        result.CancellationReason.Should().Be("Supplier out of stock");
    }

    [Fact]
    public async Task GetTotalAsync_ShouldReturnOrderTotal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);
        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Item A", 10, 5.00m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Item B", 20, 2.50m, null));

        // Act
        var total = await grain.GetTotalAsync();

        // Assert
        total.Should().Be(100.00m); // 50 + 50
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DeliveryGrainTests
{
    private readonly TestClusterFixture _fixture;

    public DeliveryGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDeliveryGrain GetGrain(Guid orgId, Guid deliveryId)
    {
        var key = $"{orgId}:delivery:{deliveryId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IDeliveryGrain>(key);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateDelivery()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);

        // Act
        var result = await grain.CreateAsync(new CreateDeliveryCommand(
            SupplierId: supplierId,
            PurchaseOrderId: poId,
            LocationId: locationId,
            ReceivedByUserId: userId,
            SupplierInvoiceNumber: "INV-2024-001",
            Notes: "Morning delivery"));

        // Assert
        result.DeliveryId.Should().Be(deliveryId);
        result.SupplierId.Should().Be(supplierId);
        result.PurchaseOrderId.Should().Be(poId);
        result.Status.Should().Be(DeliveryStatus.Pending);
        result.DeliveryNumber.Should().StartWith("DEL-");
        result.SupplierInvoiceNumber.Should().Be("INV-2024-001");
        result.Lines.Should().BeEmpty();
        result.Discrepancies.Should().BeEmpty();
        result.HasDiscrepancies.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithoutPurchaseOrder_ShouldAllowDirectDelivery()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);

        // Act
        var result = await grain.CreateAsync(new CreateDeliveryCommand(
            SupplierId: Guid.NewGuid(),
            PurchaseOrderId: null,
            LocationId: Guid.NewGuid(),
            ReceivedByUserId: Guid.NewGuid(),
            SupplierInvoiceNumber: "WALK-IN-001",
            Notes: "Walk-in vendor"));

        // Assert
        result.PurchaseOrderId.Should().BeNull();
        result.Status.Should().Be(DeliveryStatus.Pending);
    }

    [Fact]
    public async Task AddLineAsync_ShouldAddDeliveryLine()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-001", null));

        // Act
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            LineId: Guid.NewGuid(),
            IngredientId: Guid.NewGuid(),
            IngredientName: "Fresh Basil",
            PurchaseOrderLineId: Guid.NewGuid(),
            QuantityReceived: 5,
            UnitCost: 3.00m,
            BatchNumber: "BATCH-001",
            ExpiryDate: DateTime.UtcNow.AddDays(7),
            Notes: null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines.Should().HaveCount(1);
        snapshot.Lines[0].IngredientName.Should().Be("Fresh Basil");
        snapshot.Lines[0].QuantityReceived.Should().Be(5);
        snapshot.Lines[0].UnitCost.Should().Be(3.00m);
        snapshot.Lines[0].LineTotal.Should().Be(15.00m);
        snapshot.Lines[0].BatchNumber.Should().Be("BATCH-001");
        snapshot.TotalValue.Should().Be(15.00m);
    }

    [Fact]
    public async Task AddLineAsync_MultipleLines_ShouldAccumulateTotal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-002", null));

        // Act
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Tomatoes",
            null, 50, 2.00m, null, null, null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Peppers",
            null, 30, 3.00m, null, null, null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Onions",
            null, 40, 1.00m, null, null, null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines.Should().HaveCount(3);
        snapshot.TotalValue.Should().Be(100.00m + 90.00m + 40.00m);
    }

    [Fact]
    public async Task RecordDiscrepancyAsync_ShortDelivery_ShouldRecordDiscrepancy()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-003", null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId, Guid.NewGuid(), "Chicken Wings",
            Guid.NewGuid(), 80, 4.00m, null, null, null));

        // Act
        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            DiscrepancyId: Guid.NewGuid(),
            LineId: lineId,
            Type: DiscrepancyType.ShortDelivery,
            ExpectedQuantity: 100,
            ActualQuantity: 80,
            Notes: "20 units short"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.HasDiscrepancies.Should().BeTrue();
        snapshot.Discrepancies.Should().HaveCount(1);
        snapshot.Discrepancies[0].Type.Should().Be(DiscrepancyType.ShortDelivery);
        snapshot.Discrepancies[0].ExpectedQuantity.Should().Be(100);
        snapshot.Discrepancies[0].ActualQuantity.Should().Be(80);
    }

    [Fact]
    public async Task RecordDiscrepancyAsync_DamagedGoods_ShouldRecordDiscrepancy()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-004", null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId, Guid.NewGuid(), "Glass Bottles",
            null, 48, 1.50m, null, null, null));

        // Act
        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            DiscrepancyId: Guid.NewGuid(),
            LineId: lineId,
            Type: DiscrepancyType.DamagedGoods,
            ExpectedQuantity: 48,
            ActualQuantity: 42,
            Notes: "6 bottles broken in transit"));

        // Assert
        var hasDiscrepancies = await grain.HasDiscrepanciesAsync();
        hasDiscrepancies.Should().BeTrue();
    }

    [Fact]
    public async Task RecordDiscrepancyAsync_MultipleDiscrepancies_ShouldRecordAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var lineId1 = Guid.NewGuid();
        var lineId2 = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-005", null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId1, Guid.NewGuid(), "Milk", null, 20, 3.00m, null, null, null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId2, Guid.NewGuid(), "Eggs", null, 10, 4.00m, null, null, null));

        // Act
        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId1, DiscrepancyType.ShortDelivery, 24, 20, "4 short"));
        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId2, DiscrepancyType.QualityIssue, 12, 10, "2 cracked"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Discrepancies.Should().HaveCount(2);
    }

    [Fact]
    public async Task AcceptAsync_ShouldAcceptDelivery()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-006", null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Bread",
            null, 50, 2.50m, "BATCH-B001", DateTime.UtcNow.AddDays(3), null));

        // Act
        var result = await grain.AcceptAsync(new AcceptDeliveryCommand(Guid.NewGuid()));

        // Assert
        result.Status.Should().Be(DeliveryStatus.Accepted);
        result.AcceptedAt.Should().NotBeNull();
        result.AcceptedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AcceptAsync_WithDiscrepancies_ShouldStillAccept()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-007", null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId, Guid.NewGuid(), "Cheese", null, 45, 8.00m, null, null, null));
        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId, DiscrepancyType.ShortDelivery, 50, 45, "5 short"));

        // Act
        var result = await grain.AcceptAsync(new AcceptDeliveryCommand(Guid.NewGuid()));

        // Assert
        result.Status.Should().Be(DeliveryStatus.Accepted);
        result.HasDiscrepancies.Should().BeTrue();
    }

    [Fact]
    public async Task RejectAsync_ShouldRejectDelivery()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-008", null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Fish",
            null, 30, 12.00m, null, DateTime.UtcNow.AddDays(-1), null));

        // Act
        var result = await grain.RejectAsync(new RejectDeliveryCommand(
            Reason: "Product expired on arrival",
            RejectedByUserId: Guid.NewGuid()));

        // Assert
        result.Status.Should().Be(DeliveryStatus.Rejected);
        result.RejectedAt.Should().NotBeNull();
        result.RejectionReason.Should().Be("Product expired on arrival");
    }

    [Fact]
    public async Task AddLineAsync_WithBatchAndExpiry_ShouldTrackBatchInfo()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var expiryDate = DateTime.UtcNow.AddDays(14);
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), null, Guid.NewGuid(),
            Guid.NewGuid(), "INV-009", null));

        // Act
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            LineId: Guid.NewGuid(),
            IngredientId: Guid.NewGuid(),
            IngredientName: "Cream Cheese",
            PurchaseOrderLineId: null,
            QuantityReceived: 24,
            UnitCost: 4.50m,
            BatchNumber: "CC-2024-0115",
            ExpiryDate: expiryDate,
            Notes: "Refrigerate immediately"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines[0].BatchNumber.Should().Be("CC-2024-0115");
        snapshot.Lines[0].ExpiryDate.Should().BeCloseTo(expiryDate, TimeSpan.FromSeconds(1));
    }
}
