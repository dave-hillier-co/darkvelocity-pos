using DarkVelocity.Procurement.Api.Data;
using DarkVelocity.Procurement.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Integration.Tests.Fixtures;

public class ProcurementServiceFixture : WebApplicationFactory<DarkVelocity.Procurement.Api.Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Shared test IDs (set by IntegrationTestFixture)
    public Guid TestLocationId { get; set; }
    public Guid TestUserId { get; set; }

    // Pre-created test data IDs
    public Guid TestSupplierId { get; private set; }
    public Guid SecondSupplierId { get; private set; }
    public Guid BeefIngredientId { get; private set; }
    public Guid CheeseIngredientId { get; private set; }
    public Guid TomatoesIngredientId { get; private set; }
    public Guid TestPurchaseOrderId { get; private set; }
    public Guid SubmittedPurchaseOrderId { get; private set; }
    public Guid TestDeliveryId { get; private set; }
    public Guid TestPurchaseOrderLineId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<ProcurementDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Generate ingredient IDs
        BeefIngredientId = Guid.NewGuid();
        CheeseIngredientId = Guid.NewGuid();
        TomatoesIngredientId = Guid.NewGuid();

        // Create primary supplier
        var supplier = new Supplier
        {
            Code = "SUP-001",
            Name = "Fresh Foods Ltd",
            ContactName = "John Smith",
            ContactEmail = "orders@freshfoods.com",
            ContactPhone = "020-1234-5678",
            Address = "123 Food Street, London",
            PaymentTermsDays = 30,
            LeadTimeDays = 3
        };
        db.Suppliers.Add(supplier);
        TestSupplierId = supplier.Id;

        // Create secondary supplier for comparison
        var supplier2 = new Supplier
        {
            Code = "SUP-002",
            Name = "Quality Meats Co",
            ContactName = "Jane Doe",
            ContactEmail = "orders@qualitymeats.com",
            PaymentTermsDays = 14,
            LeadTimeDays = 2
        };
        db.Suppliers.Add(supplier2);
        SecondSupplierId = supplier2.Id;

        // Create supplier ingredients for primary supplier
        var beefIngredient = new SupplierIngredient
        {
            SupplierId = supplier.Id,
            IngredientId = BeefIngredientId,
            SupplierProductCode = "FF-BEEF-001",
            SupplierProductName = "Premium Beef Mince",
            PackSize = 5m,
            PackUnit = "kg",
            LastKnownPrice = 25.00m,
            LastPriceUpdatedAt = DateTime.UtcNow,
            IsPreferred = true
        };
        db.SupplierIngredients.Add(beefIngredient);

        var cheeseIngredient = new SupplierIngredient
        {
            SupplierId = supplier.Id,
            IngredientId = CheeseIngredientId,
            SupplierProductCode = "FF-CHEESE-001",
            SupplierProductName = "Cheddar Cheese Block",
            PackSize = 2m,
            PackUnit = "kg",
            LastKnownPrice = 15.00m,
            LastPriceUpdatedAt = DateTime.UtcNow,
            IsPreferred = true
        };
        db.SupplierIngredients.Add(cheeseIngredient);

        var tomatoesIngredient = new SupplierIngredient
        {
            SupplierId = supplier.Id,
            IngredientId = TomatoesIngredientId,
            SupplierProductCode = "FF-TOM-001",
            SupplierProductName = "Fresh Tomatoes",
            PackSize = 10m,
            PackUnit = "kg",
            LastKnownPrice = 12.00m,
            LastPriceUpdatedAt = DateTime.UtcNow,
            IsPreferred = false
        };
        db.SupplierIngredients.Add(tomatoesIngredient);

        await db.SaveChangesAsync();

        // Create a draft purchase order
        var purchaseOrder = new PurchaseOrder
        {
            OrderNumber = "PO-20260126-0001",
            SupplierId = TestSupplierId,
            LocationId = TestLocationId,
            CreatedByUserId = TestUserId,
            Status = "draft",
            ExpectedDeliveryDate = DateTime.UtcNow.AddDays(3)
        };
        db.PurchaseOrders.Add(purchaseOrder);
        TestPurchaseOrderId = purchaseOrder.Id;

        // Add lines to draft PO
        var poLine1 = new PurchaseOrderLine
        {
            PurchaseOrderId = purchaseOrder.Id,
            IngredientId = BeefIngredientId,
            IngredientName = "Premium Beef Mince",
            QuantityOrdered = 10m,
            UnitPrice = 5.00m,
            LineTotal = 50m
        };
        db.PurchaseOrderLines.Add(poLine1);
        TestPurchaseOrderLineId = poLine1.Id;

        var poLine2 = new PurchaseOrderLine
        {
            PurchaseOrderId = purchaseOrder.Id,
            IngredientId = CheeseIngredientId,
            IngredientName = "Cheddar Cheese Block",
            QuantityOrdered = 5m,
            UnitPrice = 7.50m,
            LineTotal = 37.50m
        };
        db.PurchaseOrderLines.Add(poLine2);

        purchaseOrder.OrderTotal = 87.50m;

        await db.SaveChangesAsync();

        // Create a submitted purchase order (for receiving against)
        var submittedPO = new PurchaseOrder
        {
            OrderNumber = "PO-20260126-0002",
            SupplierId = TestSupplierId,
            LocationId = TestLocationId,
            CreatedByUserId = TestUserId,
            Status = "submitted",
            ExpectedDeliveryDate = DateTime.UtcNow.AddDays(1),
            SubmittedAt = DateTime.UtcNow.AddDays(-1),
            OrderTotal = 100m
        };
        db.PurchaseOrders.Add(submittedPO);
        SubmittedPurchaseOrderId = submittedPO.Id;

        var submittedPOLine = new PurchaseOrderLine
        {
            PurchaseOrderId = submittedPO.Id,
            IngredientId = BeefIngredientId,
            IngredientName = "Premium Beef Mince",
            QuantityOrdered = 20m,
            UnitPrice = 5.00m,
            LineTotal = 100m
        };
        db.PurchaseOrderLines.Add(submittedPOLine);

        await db.SaveChangesAsync();

        // Create a pending delivery
        var delivery = new Delivery
        {
            DeliveryNumber = "DEL-20260126-0001",
            SupplierId = TestSupplierId,
            LocationId = TestLocationId,
            ReceivedByUserId = TestUserId,
            Status = "pending",
            ReceivedAt = DateTime.UtcNow,
            SupplierInvoiceNumber = "INV-12345"
        };
        db.Deliveries.Add(delivery);
        TestDeliveryId = delivery.Id;

        // Add delivery line
        var deliveryLine = new DeliveryLine
        {
            DeliveryId = delivery.Id,
            IngredientId = TomatoesIngredientId,
            IngredientName = "Fresh Tomatoes",
            QuantityReceived = 8m,
            UnitCost = 1.50m,
            LineTotal = 12m,
            BatchNumber = "BATCH-001",
            ExpiryDate = DateTime.UtcNow.AddDays(14)
        };
        db.DeliveryLines.Add(deliveryLine);
        delivery.TotalValue = 12m;

        await db.SaveChangesAsync();
    }

    public new async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }

    public ProcurementDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
    }
}
