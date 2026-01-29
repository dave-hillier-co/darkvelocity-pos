using DarkVelocity.Reporting.Api.Data;
using DarkVelocity.Reporting.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Integration.Tests.Fixtures;

/// <summary>
/// Fixture for the Reporting service with comprehensive test data for integration testing.
/// </summary>
public class ReportingServiceFixture : WebApplicationFactory<DarkVelocity.Reporting.Api.Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Shared test data IDs (coordinated across services)
    public Guid TestLocationId { get; set; }
    public Guid TestMenuItemId { get; set; }
    public Guid TestMenuItemId2 { get; set; }

    // Service-specific test data
    public Guid TestCategoryId { get; private set; }
    public Guid TestSupplierId { get; private set; }
    public Guid TestThresholdId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<ReportingDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Initialize shared IDs if not set
        if (TestLocationId == Guid.Empty) TestLocationId = Guid.NewGuid();
        if (TestMenuItemId == Guid.Empty) TestMenuItemId = Guid.NewGuid();
        if (TestMenuItemId2 == Guid.Empty) TestMenuItemId2 = Guid.NewGuid();

        TestCategoryId = Guid.NewGuid();
        TestSupplierId = Guid.NewGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);

        // Create daily sales summaries for trending analysis
        for (var i = 0; i < 7; i++)
        {
            var date = today.AddDays(-i);
            var baseRevenue = 1500 - (i * 50); // Increasing trend towards today

            db.DailySalesSummaries.Add(new DailySalesSummary
            {
                LocationId = TestLocationId,
                Date = date,
                GrossRevenue = baseRevenue,
                DiscountTotal = baseRevenue * 0.03m, // 3% discounts
                NetRevenue = baseRevenue * 0.97m,
                TaxCollected = baseRevenue * 0.10m, // 10% tax
                TotalCOGS = baseRevenue * 0.35m, // 35% COGS
                GrossProfit = baseRevenue * 0.62m, // 62% margin
                GrossMarginPercent = 62.00m,
                OrderCount = 40 + (7 - i) * 2,
                ItemsSold = 100 + (7 - i) * 5,
                AverageOrderValue = baseRevenue / (40 + (7 - i) * 2),
                TipsCollected = baseRevenue * 0.05m, // 5% tips
                CashTotal = baseRevenue * 0.40m, // 40% cash
                CardTotal = baseRevenue * 0.55m, // 55% card
                OtherPaymentTotal = baseRevenue * 0.05m, // 5% other
                RefundCount = i < 3 ? 1 : 0,
                RefundTotal = i < 3 ? 25m : 0m
            });
        }

        await db.SaveChangesAsync();

        // Create item sales summaries
        db.ItemSalesSummaries.Add(new ItemSalesSummary
        {
            LocationId = TestLocationId,
            Date = today,
            MenuItemId = TestMenuItemId,
            MenuItemName = "Classic Burger",
            CategoryId = TestCategoryId,
            CategoryName = "Mains",
            QuantitySold = 45,
            GrossRevenue = 562.50m, // 45 * 12.50
            DiscountTotal = 12.50m,
            NetRevenue = 550.00m,
            TotalCOGS = 180.00m, // $4 per burger
            GrossProfit = 370.00m,
            GrossMarginPercent = 67.27m,
            AverageCostPerUnit = 4.00m,
            ProfitPerUnit = 8.22m
        });

        db.ItemSalesSummaries.Add(new ItemSalesSummary
        {
            LocationId = TestLocationId,
            Date = today,
            MenuItemId = TestMenuItemId2,
            MenuItemName = "French Fries",
            CategoryId = TestCategoryId,
            CategoryName = "Sides",
            QuantitySold = 60,
            GrossRevenue = 270.00m, // 60 * 4.50
            DiscountTotal = 5.00m,
            NetRevenue = 265.00m,
            TotalCOGS = 60.00m, // $1 per fries
            GrossProfit = 205.00m,
            GrossMarginPercent = 77.36m,
            AverageCostPerUnit = 1.00m,
            ProfitPerUnit = 3.42m
        });

        await db.SaveChangesAsync();

        // Create category sales summaries
        db.CategorySalesSummaries.Add(new CategorySalesSummary
        {
            LocationId = TestLocationId,
            Date = today,
            CategoryId = TestCategoryId,
            CategoryName = "Mains",
            ItemsSold = 45,
            GrossRevenue = 562.50m,
            DiscountTotal = 12.50m,
            NetRevenue = 550.00m,
            TotalCOGS = 180.00m,
            GrossProfit = 370.00m,
            GrossMarginPercent = 67.27m,
            RevenuePercentOfTotal = 67.57m
        });

        await db.SaveChangesAsync();

        // Create margin thresholds
        var overallThreshold = new MarginThreshold
        {
            LocationId = TestLocationId,
            ThresholdType = "overall",
            MinimumMarginPercent = 50.00m,
            WarningMarginPercent = 60.00m
        };
        db.MarginThresholds.Add(overallThreshold);
        TestThresholdId = overallThreshold.Id;

        db.MarginThresholds.Add(new MarginThreshold
        {
            LocationId = TestLocationId,
            ThresholdType = "category",
            CategoryId = TestCategoryId,
            CategoryName = "Mains",
            MinimumMarginPercent = 55.00m,
            WarningMarginPercent = 65.00m
        });

        await db.SaveChangesAsync();

        // Create supplier spend summary
        db.SupplierSpendSummaries.Add(new SupplierSpendSummary
        {
            LocationId = TestLocationId,
            PeriodStart = today.AddDays(-7),
            PeriodEnd = today,
            SupplierId = TestSupplierId,
            SupplierName = "Fresh Foods Ltd",
            TotalSpend = 3500.00m,
            DeliveryCount = 4,
            AverageDeliveryValue = 875.00m,
            OnTimeDeliveries = 3,
            LateDeliveries = 1,
            OnTimePercentage = 75.00m,
            DiscrepancyCount = 1,
            DiscrepancyValue = 75.00m,
            DiscrepancyRate = 25.00m,
            UniqueProductsOrdered = 20
        });

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

    public ReportingDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
    }
}
