using DarkVelocity.Orders.Api.Data;
using DarkVelocity.Orders.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Integration.Tests.Fixtures;

/// <summary>
/// Fixture for the Orders service with comprehensive test data for integration testing.
/// </summary>
public class OrdersServiceFixture : WebApplicationFactory<DarkVelocity.Orders.Api.Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Shared test data IDs (coordinated across services)
    public Guid TestLocationId { get; set; }
    public Guid TestUserId { get; set; }
    public Guid TestMenuItemId { get; set; }
    public Guid TestMenuItemId2 { get; set; }

    // Service-specific test data
    public Guid TestSalesPeriodId { get; private set; }
    public Guid TestOrderId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<OrdersDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Initialize shared IDs if not set
        if (TestLocationId == Guid.Empty) TestLocationId = Guid.NewGuid();
        if (TestUserId == Guid.Empty) TestUserId = Guid.NewGuid();
        if (TestMenuItemId == Guid.Empty) TestMenuItemId = Guid.NewGuid();
        if (TestMenuItemId2 == Guid.Empty) TestMenuItemId2 = Guid.NewGuid();

        // Create open sales period for testing
        var salesPeriod = new SalesPeriod
        {
            LocationId = TestLocationId,
            OpenedByUserId = TestUserId,
            OpenedAt = DateTime.UtcNow,
            OpeningCashAmount = 200.00m,
            Status = "open"
        };
        db.SalesPeriods.Add(salesPeriod);
        TestSalesPeriodId = salesPeriod.Id;

        await db.SaveChangesAsync();

        // Create test order with lines
        var order = new Order
        {
            LocationId = TestLocationId,
            SalesPeriodId = TestSalesPeriodId,
            UserId = TestUserId,
            OrderNumber = $"{DateTime.UtcNow:yyyyMMdd}-0001",
            OrderType = "direct_sale",
            Status = "open",
            CustomerName = "Integration Test Customer"
        };
        db.Orders.Add(order);
        TestOrderId = order.Id;

        await db.SaveChangesAsync();

        // Add order lines
        var line1 = new OrderLine
        {
            OrderId = TestOrderId,
            MenuItemId = TestMenuItemId,
            ItemName = "Classic Burger",
            Quantity = 2,
            UnitPrice = 12.50m,
            TaxRate = 0.20m
        };
        line1.LineTotal = line1.Quantity * line1.UnitPrice;
        db.OrderLines.Add(line1);

        var line2 = new OrderLine
        {
            OrderId = TestOrderId,
            MenuItemId = TestMenuItemId2,
            ItemName = "French Fries",
            Quantity = 2,
            UnitPrice = 4.50m,
            TaxRate = 0.20m
        };
        line2.LineTotal = line2.Quantity * line2.UnitPrice;
        db.OrderLines.Add(line2);

        await db.SaveChangesAsync();

        // Update order totals
        order.Subtotal = line1.LineTotal + line2.LineTotal;
        order.TaxTotal = order.Subtotal * 0.20m;
        order.GrandTotal = order.Subtotal + order.TaxTotal;

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

    public OrdersDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    }
}
