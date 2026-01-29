using DarkVelocity.Booking.Api.Data;
using DarkVelocity.Booking.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Booking.Tests;

public class BookingApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;
    public HttpClient Client { get; private set; } = null!;

    // Shared test data
    public Guid TestLocationId { get; private set; }
    public Guid TestFloorPlanId { get; private set; }
    public Guid TestTableId { get; private set; }
    public Guid TestTable2Id { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureServices(services =>
        {
            // Remove existing db context registration
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<BookingDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Use in-memory SQLite
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<BookingDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await db.Database.EnsureCreatedAsync();

        // Seed test data
        TestLocationId = Guid.NewGuid();

        var floorPlan = new FloorPlan
        {
            LocationId = TestLocationId,
            Name = "Main Dining",
            Description = "Main dining room",
            GridWidth = 20,
            GridHeight = 15,
            IsActive = true,
            DefaultTurnTimeMinutes = 90
        };
        db.FloorPlans.Add(floorPlan);
        TestFloorPlanId = floorPlan.Id;

        var table1 = new Table
        {
            LocationId = TestLocationId,
            FloorPlanId = floorPlan.Id,
            TableNumber = "1",
            Name = "Window Table",
            MinCapacity = 2,
            MaxCapacity = 4,
            Shape = "rectangle",
            PositionX = 1,
            PositionY = 1,
            Width = 2,
            Height = 1,
            Status = "available",
            IsActive = true,
            IsCombinationAllowed = true,
            AssignmentPriority = 10
        };
        db.Tables.Add(table1);
        TestTableId = table1.Id;

        var table2 = new Table
        {
            LocationId = TestLocationId,
            FloorPlanId = floorPlan.Id,
            TableNumber = "2",
            MinCapacity = 2,
            MaxCapacity = 6,
            Shape = "round",
            PositionX = 5,
            PositionY = 1,
            Width = 2,
            Height = 2,
            Status = "available",
            IsActive = true,
            IsCombinationAllowed = true,
            AssignmentPriority = 20
        };
        db.Tables.Add(table2);
        TestTable2Id = table2.Id;

        // Add time slots for the week
        for (int day = 0; day < 7; day++)
        {
            db.TimeSlots.Add(new TimeSlot
            {
                LocationId = TestLocationId,
                DayOfWeek = day,
                Name = "Lunch",
                StartTime = new TimeOnly(12, 0),
                EndTime = new TimeOnly(15, 0),
                IntervalMinutes = 15,
                TurnTimeMinutes = 90,
                IsActive = true
            });

            db.TimeSlots.Add(new TimeSlot
            {
                LocationId = TestLocationId,
                DayOfWeek = day,
                Name = "Dinner",
                StartTime = new TimeOnly(18, 0),
                EndTime = new TimeOnly(22, 0),
                IntervalMinutes = 15,
                TurnTimeMinutes = 90,
                IsActive = true
            });
        }

        // Add booking settings
        db.BookingSettings.Add(new BookingSettings
        {
            LocationId = TestLocationId,
            BookingWindowDays = 30,
            MinAdvanceHours = 2,
            DefaultDurationMinutes = 90,
            MaxOnlinePartySize = 8,
            MaxPartySize = 20,
            TableTurnBufferMinutes = 15,
            OnlineBookingEnabled = true,
            AutoConfirmOnlineBookings = true,
            WaitlistEnabled = true,
            Timezone = "Europe/London"
        });

        await db.SaveChangesAsync();
    }

    public new async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_connection != null)
            await _connection.DisposeAsync();
    }
}
