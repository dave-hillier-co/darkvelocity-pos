using DarkVelocity.Orders.Api.Data;
using DarkVelocity.Orders.Api.Entities;
using DarkVelocity.Shared.Infrastructure.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Orders.Tests;

public class OrdersApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Test data IDs
    public Guid TestLocationId { get; private set; }
    public Guid TestUserId { get; private set; }
    public Guid TestSalesPeriodId { get; private set; }
    public Guid TestOrderId { get; private set; }
    public Guid TestMenuItemId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Create SQLite in-memory connection (keep open for test lifetime)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add SQLite DbContext
            services.AddDbContext<OrdersDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient();

        // Seed test data
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Create test IDs (would normally come from other services)
        TestLocationId = Guid.NewGuid();
        TestUserId = Guid.NewGuid();
        TestMenuItemId = Guid.NewGuid();

        // Create test sales period
        var salesPeriod = new SalesPeriod
        {
            LocationId = TestLocationId,
            OpenedByUserId = TestUserId,
            OpenedAt = DateTime.UtcNow,
            OpeningCashAmount = 100.00m,
            Status = "open"
        };
        db.SalesPeriods.Add(salesPeriod);
        TestSalesPeriodId = salesPeriod.Id;

        await db.SaveChangesAsync();

        // Create test order
        var order = new Order
        {
            LocationId = TestLocationId,
            SalesPeriodId = TestSalesPeriodId,
            UserId = TestUserId,
            OrderNumber = "20260126-0001",
            OrderType = "direct_sale",
            Status = "open"
        };
        db.Orders.Add(order);
        TestOrderId = order.Id;

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

    public InMemoryEventBus GetEventBus()
    {
        var scope = Services.CreateScope();
        return (InMemoryEventBus)scope.ServiceProvider.GetRequiredService<IEventBus>();
    }

    public void ClearEventLog()
    {
        GetEventBus().ClearEventLog();
    }
}
