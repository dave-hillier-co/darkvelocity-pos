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

    // Given: no supplier record exists
    // When: a new supplier is registered with contact details, payment terms, and lead time
    // Then: the supplier is created with all provided details and defaults to active with no catalog items
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
        result.Catalog.Should().BeEmpty();
        result.TotalPurchasesYtd.Should().Be(0);
        result.OnTimeDeliveryPercent.Should().Be(100);
    }

    // Given: an existing meat supplier with 14-day payment terms
    // When: the supplier name, contact, and payment terms are updated
    // Then: only the changed fields are updated while unchanged fields retain their original values
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

    // Given: a dairy farm supplier with no SKUs in their catalog
    // When: whole milk is added to the supplier's catalog with pricing and supplier product code
    // Then: the supplier catalog contains the milk entry with correct price and supplier product code
    [Fact]
    public async Task AddSkuAsync_ShouldAddSkuToCatalog()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
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
        await grain.AddSkuAsync(new SupplierCatalogItem(
            SkuId: skuId,
            SkuCode: "MILK-001",
            ProductName: "Whole Milk",
            SupplierProductCode: "WM-GAL-01",
            UnitPrice: 3.50m,
            Unit: "gallon",
            MinOrderQuantity: 10,
            LeadTimeDays: 1));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Catalog.Should().HaveCount(1);
        snapshot.Catalog[0].ProductName.Should().Be("Whole Milk");
        snapshot.Catalog[0].UnitPrice.Should().Be(3.50m);
        snapshot.Catalog[0].SupplierProductCode.Should().Be("WM-GAL-01");
    }

    // Given: a bakery supplies vendor with an empty catalog
    // When: flour, sugar, and butter are each added to the catalog
    // Then: the supplier catalog contains all three SKUs
    [Fact]
    public async Task AddSkuAsync_MultipleSkus_ShouldAddAll()
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
        await grain.AddSkuAsync(new SupplierCatalogItem(
            Guid.NewGuid(), "FLR-001", "All-Purpose Flour", "APF-50LB", 25.00m, "bag", 5, 3));
        await grain.AddSkuAsync(new SupplierCatalogItem(
            Guid.NewGuid(), "SGR-001", "Sugar", "GS-25LB", 18.00m, "bag", 5, 3));
        await grain.AddSkuAsync(new SupplierCatalogItem(
            Guid.NewGuid(), "BTR-001", "Butter", "UB-36CT", 85.00m, "case", 2, 2));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Catalog.Should().HaveCount(3);
    }

    // Given: a seafood supplier with salmon fillet already in their catalog at $45/lb
    // When: the same salmon fillet SKU is re-added with a new price and supplier product code
    // Then: the catalog still has one entry but reflects the updated price, supplier product code, and minimum order
    [Fact]
    public async Task AddSkuAsync_ExistingSku_ShouldUpdateDetails()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
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

        await grain.AddSkuAsync(new SupplierCatalogItem(
            skuId, "SAL-001", "Salmon Fillet", "SF-5LB", 45.00m, "lb", 10, 1));

        // Act
        await grain.AddSkuAsync(new SupplierCatalogItem(
            skuId, "SAL-001", "Salmon Fillet", "SF-5LB-FRESH", 48.00m, "lb", 5, 1));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Catalog.Should().HaveCount(1);
        snapshot.Catalog[0].UnitPrice.Should().Be(48.00m);
        snapshot.Catalog[0].SupplierProductCode.Should().Be("SF-5LB-FRESH");
        snapshot.Catalog[0].MinOrderQuantity.Should().Be(5);
    }

    // Given: a beverage distributor with cola syrup and orange juice in their catalog
    // When: cola syrup is removed from the supplier catalog
    // Then: only orange juice remains in the catalog
    [Fact]
    public async Task RemoveSkuAsync_ShouldRemoveFromCatalog()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var skuId1 = Guid.NewGuid();
        var skuId2 = Guid.NewGuid();
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

        await grain.AddSkuAsync(new SupplierCatalogItem(
            skuId1, "COL-001", "Cola Syrup", "CS-5GAL", 75.00m, "container", 2, 5));
        await grain.AddSkuAsync(new SupplierCatalogItem(
            skuId2, "OJ-001", "Orange Juice", "OJ-CASE", 35.00m, "case", 5, 3));

        // Act
        await grain.RemoveSkuAsync(skuId1);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Catalog.Should().HaveCount(1);
        snapshot.Catalog[0].ProductName.Should().Be("Orange Juice");
    }

    // Given: a coffee roaster with espresso beans listed at $65/bag
    // When: the espresso bean price is updated to $72.50/bag
    // Then: the catalog reflects the new price
    [Fact]
    public async Task UpdateSkuPriceAsync_ShouldUpdatePrice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
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

        await grain.AddSkuAsync(new SupplierCatalogItem(
            skuId, "ESP-001", "Espresso Beans", "EB-5LB", 65.00m, "bag", 3, 7));

        // Act
        await grain.UpdateSkuPriceAsync(skuId, 72.50m);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Catalog[0].UnitPrice.Should().Be(72.50m);
    }

    // Given: a spice trader with saffron listed at $125/oz
    // When: the price for saffron is queried
    // Then: the returned price is $125.00
    [Fact]
    public async Task GetSkuPriceAsync_ShouldReturnPrice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
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

        await grain.AddSkuAsync(new SupplierCatalogItem(
            skuId, "SAF-001", "Saffron", "SF-1OZ", 125.00m, "oz", 1, 14));

        // Act
        var price = await grain.GetSkuPriceAsync(skuId);

        // Assert
        price.Should().Be(125.00m);
    }

    // Given: a supplier with no purchase history
    // When: three purchases are recorded -- two on-time and one late
    // Then: year-to-date spend totals $4,500 and on-time delivery rate is 66%
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

    // Given: an active supplier
    // When: the supplier is updated with IsActive set to false and a note that they are no longer in business
    // Then: the supplier is marked as inactive with the closing note recorded
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

    // Given: a supplier with no SKUs in their catalog
    // When: a price update is attempted for a non-existent SKU
    // Then: the operation fails because the SKU is not found
    [Fact]
    public async Task UpdateSkuPriceAsync_NonExistent_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var nonExistentSkuId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);
        await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP-011",
            Name: "Test Supplier",
            ContactName: "Test Contact",
            ContactEmail: "test@supplier.com",
            ContactPhone: "555-0011",
            Address: "123 Test St",
            PaymentTermsDays: 30,
            LeadTimeDays: 5,
            Notes: null));

        // Act
        var act = () => grain.UpdateSkuPriceAsync(nonExistentSkuId, 25.00m);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // Given: a supplier with one SKU in their catalog
    // When: the price is queried for a different SKU not in the catalog
    // Then: the operation fails because the SKU is not found
    [Fact]
    public async Task GetSkuPriceAsync_NonExistent_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var nonExistentSkuId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);
        await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP-012",
            Name: "Test Supplier",
            ContactName: "Test Contact",
            ContactEmail: "test@supplier.com",
            ContactPhone: "555-0012",
            Address: "456 Test Ave",
            PaymentTermsDays: 14,
            LeadTimeDays: 3,
            Notes: null));
        // Add one SKU to ensure the supplier has catalog items
        await grain.AddSkuAsync(new SupplierCatalogItem(
            Guid.NewGuid(), "ING-001", "Some Item", "SI-001", 10.00m, "kg", 5, 3));

        // Act
        var act = () => grain.GetSkuPriceAsync(nonExistentSkuId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // Given: a supplier that has never been registered (no CreateAsync called)
    // When: any operation (snapshot, update, add SKU, record purchase) is attempted
    // Then: each operation fails because the supplier is not initialized
    [Fact]
    public async Task Operations_OnUninitialized_ShouldThrow()
    {
        // Arrange - create grain but don't call CreateAsync
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);

        // Act & Assert - GetSnapshotAsync
        var actGetSnapshot = () => grain.GetSnapshotAsync();
        await actGetSnapshot.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");

        // Act & Assert - UpdateAsync
        var actUpdate = () => grain.UpdateAsync(new UpdateSupplierCommand(
            "New Name", null, null, null, null, null, null, null, null));
        await actUpdate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");

        // Act & Assert - AddSkuAsync
        var actAddSku = () => grain.AddSkuAsync(new SupplierCatalogItem(
            Guid.NewGuid(), "SKU-001", "Test", "SSSKU-001", 10.00m, "unit", 1, 1));
        await actAddSku.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");

        // Act & Assert - RecordPurchaseAsync
        var actRecordPurchase = () => grain.RecordPurchaseAsync(100.00m, true);
        await actRecordPurchase.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    // Given: a supplier that has already been registered
    // When: a second registration is attempted for the same supplier
    // Then: the operation fails because the supplier already exists
    [Fact]
    public async Task CreateAsync_AlreadyCreated_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);
        await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP-013",
            Name: "First Supplier",
            ContactName: "First Contact",
            ContactEmail: "first@supplier.com",
            ContactPhone: "555-0013",
            Address: "First Address",
            PaymentTermsDays: 30,
            LeadTimeDays: 5,
            Notes: null));

        // Act
        var act = () => grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP-013-DUP",
            Name: "Second Supplier",
            ContactName: "Second Contact",
            ContactEmail: "second@supplier.com",
            ContactPhone: "555-0014",
            Address: "Second Address",
            PaymentTermsDays: 14,
            LeadTimeDays: 3,
            Notes: null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    // Given: a supplier with no delivery history
    // When: four purchases are recorded and all deliveries arrive late
    // Then: year-to-date spend totals $2,550 and on-time delivery rate is 0%
    [Fact]
    public async Task RecordPurchaseAsync_AllLate_ShouldCalculateZeroPercent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);
        await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP-014",
            Name: "Late Delivery Supplier",
            ContactName: "Slow Joe",
            ContactEmail: "slow@supplier.com",
            ContactPhone: "555-0015",
            Address: "Somewhere Far",
            PaymentTermsDays: 30,
            LeadTimeDays: 7,
            Notes: null));

        // Act - record all late deliveries
        await grain.RecordPurchaseAsync(500.00m, false);
        await grain.RecordPurchaseAsync(750.00m, false);
        await grain.RecordPurchaseAsync(300.00m, false);
        await grain.RecordPurchaseAsync(1000.00m, false);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalPurchasesYtd.Should().Be(2550.00m);
        snapshot.OnTimeDeliveryPercent.Should().Be(0);
    }

    // Given: a supplier with no delivery history
    // When: five purchases are recorded and all deliveries arrive on time
    // Then: year-to-date spend totals $2,000 and on-time delivery rate is 100%
    [Fact]
    public async Task RecordPurchaseAsync_AllOnTime_ShouldCalculate100Percent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var grain = GetGrain(orgId, supplierId);
        await grain.CreateAsync(new CreateSupplierCommand(
            Code: "SUP-015",
            Name: "Reliable Supplier",
            ContactName: "Punctual Pat",
            ContactEmail: "ontime@supplier.com",
            ContactPhone: "555-0016",
            Address: "Just Around Corner",
            PaymentTermsDays: 30,
            LeadTimeDays: 1,
            Notes: null));

        // Act - record all on-time deliveries
        await grain.RecordPurchaseAsync(200.00m, true);
        await grain.RecordPurchaseAsync(350.00m, true);
        await grain.RecordPurchaseAsync(500.00m, true);
        await grain.RecordPurchaseAsync(150.00m, true);
        await grain.RecordPurchaseAsync(800.00m, true);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalPurchasesYtd.Should().Be(2000.00m);
        snapshot.OnTimeDeliveryPercent.Should().Be(100);
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

    // Given: no purchase order exists
    // When: a new purchase order is created for a supplier with an expected delivery date
    // Then: the order is in draft status with a generated PO number, no lines, and zero total
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

    // Given: a draft purchase order with no line items
    // When: 50 units of tomatoes at $2.50 each are added as a line item
    // Then: the order has one line totaling $125 and the order total reflects this
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
            SkuId: Guid.NewGuid(),
            SkuCode: "TOM-001",
            ProductName: "Tomatoes",
            QuantityOrdered: 50,
            UnitPrice: 2.50m,
            Notes: "Roma preferred"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines.Should().HaveCount(1);
        snapshot.Lines[0].ProductName.Should().Be("Tomatoes");
        snapshot.Lines[0].QuantityOrdered.Should().Be(50);
        snapshot.Lines[0].UnitPrice.Should().Be(2.50m);
        snapshot.Lines[0].LineTotal.Should().Be(125.00m);
        snapshot.OrderTotal.Should().Be(125.00m);
    }

    // Given: a draft purchase order with no line items
    // When: chicken breast, ground beef, and pork chops are added as separate lines
    // Then: the order has three lines and the total reflects the sum of all line totals
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
            Guid.NewGuid(), Guid.NewGuid(), "CHK-001", "Chicken Breast", 100, 4.50m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "GBF-001", "Ground Beef", 75, 5.00m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "PRC-001", "Pork Chops", 50, 3.75m, null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines.Should().HaveCount(3);
        snapshot.OrderTotal.Should().Be(450.00m + 375.00m + 187.50m);
    }

    // Given: a draft purchase order with 30 units of lettuce at $1.50 each
    // When: the lettuce line is updated to 50 units at $1.75 each
    // Then: the line reflects the new quantity, price, and recalculated total of $87.50
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
            lineId, Guid.NewGuid(), "LET-001", "Lettuce", 30, 1.50m, null));

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

    // Given: a draft purchase order with onions and garlic as line items
    // When: the onions line is removed
    // Then: only garlic remains and the order total reflects only the garlic line
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
            lineId1, Guid.NewGuid(), "ONI-001", "Onions", 25, 1.00m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId2, Guid.NewGuid(), "GAR-001", "Garlic", 10, 3.00m, null));

        // Act
        await grain.RemoveLineAsync(lineId1);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines.Should().HaveCount(1);
        snapshot.Lines[0].ProductName.Should().Be("Garlic");
        snapshot.OrderTotal.Should().Be(30.00m);
    }

    // Given: a draft purchase order with a line item for carrots
    // When: the purchase order is submitted to the supplier
    // Then: the order status changes to submitted with a recorded submission timestamp
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
            Guid.NewGuid(), Guid.NewGuid(), "CAR-001", "Carrots", 40, 1.25m, null));

        // Act
        var result = await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Assert
        result.Status.Should().Be(PurchaseOrderStatus.Submitted);
        result.SubmittedAt.Should().NotBeNull();
        result.SubmittedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: a submitted purchase order with lines for 100 potatoes and 20 celery
    // When: all 100 potatoes are received but no celery has arrived yet
    // Then: the order is partially received with potatoes fully received and celery at zero
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
            lineId1, Guid.NewGuid(), "POT-001", "Potatoes", 100, 0.50m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId2, Guid.NewGuid(), "CEL-001", "Celery", 20, 2.00m, null));
        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId1, 100));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
        snapshot.Lines.First(l => l.LineId == lineId1).QuantityReceived.Should().Be(100);
        snapshot.Lines.First(l => l.LineId == lineId2).QuantityReceived.Should().Be(0);
    }

    // Given: a submitted purchase order with lines for 50 apples and 50 oranges
    // When: both lines are received in full
    // Then: the order status changes to received with a timestamp and is marked fully received
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
            lineId1, Guid.NewGuid(), "APL-001", "Apples", 50, 1.00m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            lineId2, Guid.NewGuid(), "ORG-001", "Oranges", 50, 1.25m, null));
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

    // Given: a submitted purchase order for 100 units of rice
    // When: rice is received in three separate deliveries of 40, 35, and 25 units
    // Then: the total received accumulates to 100 and the order is marked as received
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
            lineId, Guid.NewGuid(), "RIC-001", "Rice", 100, 1.50m, null));
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

    // Given: a submitted purchase order for mushrooms
    // When: the order is cancelled because the supplier is out of stock
    // Then: the order status is cancelled with a timestamp and the cancellation reason recorded
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
            Guid.NewGuid(), Guid.NewGuid(), "MSH-001", "Mushrooms", 20, 5.00m, null));
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

    // Given: a draft purchase order with two line items totaling $100
    // When: the order total is queried
    // Then: the returned total is $100.00
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
            Guid.NewGuid(), Guid.NewGuid(), "ITA-001", "Item A", 10, 5.00m, null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "ITB-001", "Item B", 20, 2.50m, null));

        // Act
        var total = await grain.GetTotalAsync();

        // Assert
        total.Should().Be(100.00m); // 50 + 50
    }

    // Given: a draft purchase order with no line items
    // When: the empty order is submitted
    // Then: the operation fails because a purchase order cannot be submitted without lines
    [Fact]
    public async Task SubmitAsync_EmptyPO_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);
        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));

        // Act
        var act = () => grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*empty*");
    }

    // Given: a purchase order that has already been submitted to the supplier
    // When: a second submission is attempted
    // Then: the operation fails because the order is not in draft status
    [Fact]
    public async Task SubmitAsync_AlreadySubmitted_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);
        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "TST-001", "Test Item", 10, 5.00m, null));
        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act
        var act = () => grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not in draft*");
    }

    // Given: a purchase order that has been submitted to the supplier
    // When: a new line item is added to the submitted order
    // Then: the operation fails because lines cannot be added after submission
    [Fact]
    public async Task AddLineAsync_SubmittedPO_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);
        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "INI-001", "Initial Item", 10, 5.00m, null));
        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act
        var act = () => grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "NEW-001", "New Item", 5, 2.00m, null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*submitted*");
    }

    // Given: a purchase order that has been submitted to the supplier
    // When: an existing line item quantity is updated on the submitted order
    // Then: the operation fails because lines cannot be modified after submission
    [Fact]
    public async Task UpdateLineAsync_SubmittedPO_ShouldThrow()
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
            lineId, Guid.NewGuid(), "TST-001", "Test Item", 10, 5.00m, null));
        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act
        var act = () => grain.UpdateLineAsync(new UpdatePurchaseOrderLineCommand(
            lineId, 20, null, null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*submitted*");
    }

    // Given: a purchase order that has been submitted to the supplier
    // When: a line item is removed from the submitted order
    // Then: the operation fails because lines cannot be removed after submission
    [Fact]
    public async Task RemoveLineAsync_SubmittedPO_ShouldThrow()
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
            lineId, Guid.NewGuid(), "TST-001", "Test Item", 10, 5.00m, null));
        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act
        var act = () => grain.RemoveLineAsync(lineId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*submitted*");
    }

    // Given: a purchase order still in draft status (not yet submitted)
    // When: an attempt is made to receive goods against the draft order
    // Then: the operation fails because goods cannot be received on an unsubmitted order
    [Fact]
    public async Task ReceiveLineAsync_DraftStatus_ShouldThrow()
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
            lineId, Guid.NewGuid(), "TST-001", "Test Item", 10, 5.00m, null));

        // Act
        var act = () => grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 10));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot receive*");
    }

    // Given: a purchase order that was submitted and then cancelled
    // When: an attempt is made to receive goods against the cancelled order
    // Then: the operation fails because goods cannot be received on a cancelled order
    [Fact]
    public async Task ReceiveLineAsync_CancelledStatus_ShouldThrow()
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
            lineId, Guid.NewGuid(), "TST-001", "Test Item", 10, 5.00m, null));
        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));
        await grain.CancelAsync(new CancelPurchaseOrderCommand("Changed plans", Guid.NewGuid()));

        // Act
        var act = () => grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 10));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot receive*");
    }

    // Given: a purchase order that has been fully received
    // When: an attempt is made to cancel the received order
    // Then: the operation fails because a fully received order cannot be cancelled
    [Fact]
    public async Task CancelAsync_ReceivedPO_ShouldThrow()
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
            lineId, Guid.NewGuid(), "TST-001", "Test Item", 10, 5.00m, null));
        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 10));

        // Assert PO is fully received
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PurchaseOrderStatus.Received);

        // Act
        var act = () => grain.CancelAsync(new CancelPurchaseOrderCommand("Want to cancel", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot cancel*");
    }

    // Given: a purchase order that has already been cancelled
    // When: a second cancellation is attempted
    // Then: the operation fails because the order is already cancelled
    [Fact]
    public async Task CancelAsync_AlreadyCancelled_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);
        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "TST-001", "Test Item", 10, 5.00m, null));
        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));
        await grain.CancelAsync(new CancelPurchaseOrderCommand("First cancellation", Guid.NewGuid()));

        // Act
        var act = () => grain.CancelAsync(new CancelPurchaseOrderCommand("Second cancellation", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot cancel*");
    }

    // Given: a draft purchase order with one line item
    // When: an update is attempted on a line ID that does not exist on the order
    // Then: the operation fails because the line is not found
    [Fact]
    public async Task UpdateLineAsync_NonExistentLine_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var nonExistentLineId = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);
        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "TST-001", "Test Item", 10, 5.00m, null));

        // Act
        var act = () => grain.UpdateLineAsync(new UpdatePurchaseOrderLineCommand(
            nonExistentLineId, 20, null, null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // Given: a submitted purchase order with one line item
    // When: goods are received against a line ID that does not exist on the order
    // Then: the operation fails because the line is not found
    [Fact]
    public async Task ReceiveLineAsync_NonExistentLine_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var nonExistentLineId = Guid.NewGuid();
        var grain = GetGrain(orgId, poId);
        await grain.CreateAsync(new CreatePurchaseOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(3), null));
        await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "TST-001", "Test Item", 10, 5.00m, null));
        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act
        var act = () => grain.ReceiveLineAsync(new ReceiveLineCommand(nonExistentLineId, 10));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // Given: a submitted purchase order for 10 units of an item
    // When: 15 units are received (over-delivery)
    // Then: the over-delivery is accepted and tracked, reflecting the negative stock philosophy
    [Fact]
    public async Task ReceiveLineAsync_OverDelivery_ShouldHandle()
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
            lineId, Guid.NewGuid(), "TST-001", "Test Item", 10, 5.00m, null));
        await grain.SubmitAsync(new SubmitPurchaseOrderCommand(Guid.NewGuid()));

        // Act - receive more than ordered
        await grain.ReceiveLineAsync(new ReceiveLineCommand(lineId, 15));

        // Assert - over-delivery should be tracked (negative stock philosophy)
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines[0].QuantityReceived.Should().Be(15);
        snapshot.Lines[0].QuantityOrdered.Should().Be(10);
        snapshot.Status.Should().Be(PurchaseOrderStatus.Received);
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

    // Given: no delivery record exists
    // When: a delivery is created linked to a supplier, purchase order, and location
    // Then: the delivery is in pending status with a generated delivery number and no lines or discrepancies
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

    // Given: no delivery record exists and no prior purchase order was placed
    // When: a walk-in delivery is created without a linked purchase order
    // Then: the delivery is created in pending status with no purchase order reference
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

    // Given: a pending delivery with no line items
    // When: 5 units of fresh basil at $3.00 each are received with a batch number
    // Then: the delivery has one line totaling $15.00 with batch tracking information
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
            SkuId: Guid.NewGuid(),
            SkuCode: "BAS-001",
            ProductName: "Fresh Basil",
            PurchaseOrderLineId: Guid.NewGuid(),
            QuantityReceived: 5,
            UnitCost: 3.00m,
            BatchNumber: "BATCH-001",
            ExpiryDate: DateTime.UtcNow.AddDays(7),
            Notes: null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines.Should().HaveCount(1);
        snapshot.Lines[0].ProductName.Should().Be("Fresh Basil");
        snapshot.Lines[0].QuantityReceived.Should().Be(5);
        snapshot.Lines[0].UnitCost.Should().Be(3.00m);
        snapshot.Lines[0].LineTotal.Should().Be(15.00m);
        snapshot.Lines[0].BatchNumber.Should().Be("BATCH-001");
        snapshot.TotalValue.Should().Be(15.00m);
    }

    // Given: a pending delivery with no line items
    // When: tomatoes, peppers, and onions are each received as separate lines
    // Then: the delivery has three lines and the total value sums all line totals
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
            Guid.NewGuid(), Guid.NewGuid(), "TOM-001", "Tomatoes",
            null, 50, 2.00m, null, null, null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "PEP-001", "Peppers",
            null, 30, 3.00m, null, null, null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "ONI-001", "Onions",
            null, 40, 1.00m, null, null, null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines.Should().HaveCount(3);
        snapshot.TotalValue.Should().Be(100.00m + 90.00m + 40.00m);
    }

    // Given: a delivery with 80 chicken wings received against an expected 100
    // When: a short delivery discrepancy of 20 units is recorded
    // Then: the delivery is flagged with a short delivery discrepancy showing expected vs actual quantities
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
            lineId, Guid.NewGuid(), "CKW-001", "Chicken Wings",
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

    // Given: a delivery with 48 glass bottles received, 6 of which are broken
    // When: a damaged goods discrepancy is recorded
    // Then: the delivery is flagged as having discrepancies
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
            lineId, Guid.NewGuid(), "GLB-001", "Glass Bottles",
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

    // Given: a delivery with milk (4 short) and eggs (2 cracked) both having issues
    // When: a short delivery discrepancy is recorded for milk and a quality issue for eggs
    // Then: the delivery has two separate discrepancies recorded
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
            lineId1, Guid.NewGuid(), "MLK-001", "Milk", null, 20, 3.00m, null, null, null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId2, Guid.NewGuid(), "EGG-001", "Eggs", null, 10, 4.00m, null, null, null));

        // Act
        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId1, DiscrepancyType.ShortDelivery, 24, 20, "4 short"));
        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId2, DiscrepancyType.QualityIssue, 12, 10, "2 cracked"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Discrepancies.Should().HaveCount(2);
    }

    // Given: a pending delivery with bread items and batch tracking
    // When: the delivery is accepted by a staff member
    // Then: the delivery status changes to accepted with a recorded acceptance timestamp
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
            Guid.NewGuid(), Guid.NewGuid(), "BRD-001", "Bread",
            null, 50, 2.50m, "BATCH-B001", DateTime.UtcNow.AddDays(3), null));

        // Act
        var result = await grain.AcceptAsync(new AcceptDeliveryCommand(Guid.NewGuid()));

        // Assert
        result.Status.Should().Be(DeliveryStatus.Accepted);
        result.AcceptedAt.Should().NotBeNull();
        result.AcceptedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: a pending delivery of cheese with a recorded short delivery discrepancy of 5 units
    // When: the delivery is accepted despite the discrepancy
    // Then: the delivery is accepted but still flagged as having discrepancies
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
            lineId, Guid.NewGuid(), "CHS-001", "Cheese", null, 45, 8.00m, null, null, null));
        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId, DiscrepancyType.ShortDelivery, 50, 45, "5 short"));

        // Act
        var result = await grain.AcceptAsync(new AcceptDeliveryCommand(Guid.NewGuid()));

        // Assert
        result.Status.Should().Be(DeliveryStatus.Accepted);
        result.HasDiscrepancies.Should().BeTrue();
    }

    // Given: a pending delivery of fish with an expiry date that has already passed
    // When: the delivery is rejected because the product expired on arrival
    // Then: the delivery status changes to rejected with a timestamp and the rejection reason
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
            Guid.NewGuid(), Guid.NewGuid(), "FSH-001", "Fish",
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
            SkuId: Guid.NewGuid(),
            SkuCode: "CCH-001",
            ProductName: "Cream Cheese",
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

    [Fact]
    public async Task AddLineAsync_AcceptedDelivery_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-010", null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "INI-001", "Initial Item",
            null, 10, 5.00m, null, null, null));
        await grain.AcceptAsync(new AcceptDeliveryCommand(Guid.NewGuid()));

        // Act
        var act = () => grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "NEW-001", "New Item",
            null, 5, 2.00m, null, null, null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*processed*");
    }

    [Fact]
    public async Task AddLineAsync_RejectedDelivery_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-011", null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "INI-001", "Initial Item",
            null, 10, 5.00m, null, null, null));
        await grain.RejectAsync(new RejectDeliveryCommand("Wrong order", Guid.NewGuid()));

        // Act
        var act = () => grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "NEW-001", "New Item",
            null, 5, 2.00m, null, null, null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*processed*");
    }

    [Fact]
    public async Task RecordDiscrepancyAsync_AcceptedDelivery_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-012", null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId, Guid.NewGuid(), "TST-001", "Test Item",
            null, 10, 5.00m, null, null, null));
        await grain.AcceptAsync(new AcceptDeliveryCommand(Guid.NewGuid()));

        // Act
        var act = () => grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId, DiscrepancyType.ShortDelivery, 15, 10, "Late discrepancy"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*processed*");
    }

    [Fact]
    public async Task RecordDiscrepancyAsync_OverDelivery_ShouldRecord()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-013", null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId, Guid.NewGuid(), "POT-001", "Potatoes",
            Guid.NewGuid(), 120, 0.50m, null, null, null));

        // Act
        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId, DiscrepancyType.OverDelivery, 100, 120, "20 extra units"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.HasDiscrepancies.Should().BeTrue();
        snapshot.Discrepancies.Should().HaveCount(1);
        snapshot.Discrepancies[0].Type.Should().Be(DiscrepancyType.OverDelivery);
        snapshot.Discrepancies[0].ExpectedQuantity.Should().Be(100);
        snapshot.Discrepancies[0].ActualQuantity.Should().Be(120);
    }

    [Fact]
    public async Task RecordDiscrepancyAsync_WrongItem_ShouldRecord()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-014", null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId, Guid.NewGuid(), "CKT-001", "Chicken Thighs",
            Guid.NewGuid(), 25, 4.00m, null, null, null));

        // Act
        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId, DiscrepancyType.WrongItem, 25, 25, "Received drumsticks instead of thighs"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.HasDiscrepancies.Should().BeTrue();
        snapshot.Discrepancies[0].Type.Should().Be(DiscrepancyType.WrongItem);
        snapshot.Discrepancies[0].Notes.Should().Contain("drumsticks");
    }

    [Fact]
    public async Task RecordDiscrepancyAsync_QualityIssue_ShouldRecord()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-015", null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId, Guid.NewGuid(), "BAN-001", "Bananas",
            null, 50, 0.30m, "BATCH-BAN-001", DateTime.UtcNow.AddDays(5), null));

        // Act
        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId, DiscrepancyType.QualityIssue, 50, 35, "15 bananas overripe"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.HasDiscrepancies.Should().BeTrue();
        snapshot.Discrepancies[0].Type.Should().Be(DiscrepancyType.QualityIssue);
        snapshot.Discrepancies[0].ExpectedQuantity.Should().Be(50);
        snapshot.Discrepancies[0].ActualQuantity.Should().Be(35);
    }

    [Fact]
    public async Task RecordDiscrepancyAsync_IncorrectPrice_ShouldRecord()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-016", null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            lineId, Guid.NewGuid(), "OLV-001", "Olive Oil",
            Guid.NewGuid(), 12, 15.00m, null, null, null));

        // Act - note: using quantity fields to represent price discrepancy context
        await grain.RecordDiscrepancyAsync(new RecordDiscrepancyCommand(
            Guid.NewGuid(), lineId, DiscrepancyType.IncorrectPrice, 12, 12, "Invoiced at $18/unit instead of agreed $15"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.HasDiscrepancies.Should().BeTrue();
        snapshot.Discrepancies[0].Type.Should().Be(DiscrepancyType.IncorrectPrice);
        snapshot.Discrepancies[0].Notes.Should().Contain("$18");
    }

    [Fact]
    public async Task AcceptAsync_AlreadyAccepted_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-017", null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "TST-001", "Test Item",
            null, 10, 5.00m, null, null, null));
        await grain.AcceptAsync(new AcceptDeliveryCommand(Guid.NewGuid()));

        // Act
        var act = () => grain.AcceptAsync(new AcceptDeliveryCommand(Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not pending*");
    }

    [Fact]
    public async Task RejectAsync_AlreadyRejected_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-018", null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "TST-001", "Test Item",
            null, 10, 5.00m, null, null, null));
        await grain.RejectAsync(new RejectDeliveryCommand("Quality issues", Guid.NewGuid()));

        // Act
        var act = () => grain.RejectAsync(new RejectDeliveryCommand("More issues", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not pending*");
    }

    [Fact]
    public async Task RejectAsync_Accepted_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var grain = GetGrain(orgId, deliveryId);
        await grain.CreateAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "INV-019", null));
        await grain.AddLineAsync(new AddDeliveryLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), "TST-001", "Test Item",
            null, 10, 5.00m, null, null, null));
        await grain.AcceptAsync(new AcceptDeliveryCommand(Guid.NewGuid()));

        // Act
        var act = () => grain.RejectAsync(new RejectDeliveryCommand("Changed mind", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not pending*");
    }
}
