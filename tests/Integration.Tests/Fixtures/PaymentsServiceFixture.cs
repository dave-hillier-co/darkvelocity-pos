using DarkVelocity.Payments.Api.Data;
using DarkVelocity.Payments.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Integration.Tests.Fixtures;

/// <summary>
/// Fixture for the Payments service with comprehensive test data for integration testing.
/// </summary>
public class PaymentsServiceFixture : WebApplicationFactory<DarkVelocity.Payments.Api.Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Shared test data IDs (coordinated across services)
    public Guid TestLocationId { get; set; }
    public Guid TestUserId { get; set; }
    public Guid TestOrderId { get; set; }

    // Service-specific test data
    public Guid CashPaymentMethodId { get; private set; }
    public Guid CardPaymentMethodId { get; private set; }
    public Guid VoucherPaymentMethodId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<PaymentsDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Initialize shared IDs if not set
        if (TestLocationId == Guid.Empty) TestLocationId = Guid.NewGuid();
        if (TestUserId == Guid.Empty) TestUserId = Guid.NewGuid();
        if (TestOrderId == Guid.Empty) TestOrderId = Guid.NewGuid();

        // Create payment methods
        var cashMethod = new PaymentMethod
        {
            LocationId = TestLocationId,
            Name = "Cash",
            MethodType = "cash",
            OpensDrawer = true,
            RequiresTip = false,
            DisplayOrder = 1,
            IsActive = true
        };
        db.PaymentMethods.Add(cashMethod);
        CashPaymentMethodId = cashMethod.Id;

        var cardMethod = new PaymentMethod
        {
            LocationId = TestLocationId,
            Name = "Credit Card",
            MethodType = "card",
            OpensDrawer = false,
            RequiresTip = true,
            RequiresExternalTerminal = true,
            DisplayOrder = 2,
            IsActive = true
        };
        db.PaymentMethods.Add(cardMethod);
        CardPaymentMethodId = cardMethod.Id;

        var voucherMethod = new PaymentMethod
        {
            LocationId = TestLocationId,
            Name = "Gift Voucher",
            MethodType = "voucher",
            OpensDrawer = false,
            RequiresTip = false,
            DisplayOrder = 3,
            IsActive = true
        };
        db.PaymentMethods.Add(voucherMethod);
        VoucherPaymentMethodId = voucherMethod.Id;

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

    public PaymentsDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    }
}
