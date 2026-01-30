using DarkVelocity.Labor.Api.Data;
using DarkVelocity.Shared.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database - only register if not in Test environment
if (!builder.Environment.IsEnvironment("Test"))
{
    builder.Services.AddDbContext<LaborDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=labor_db;Username=darkvelocity;Password=darkvelocity_dev";
        options.UseNpgsql(connectionString);
    });
}

// Event Bus
builder.Services.AddInMemoryEventBus();
builder.Services.AddEventHandlersFromAssembly(typeof(Program).Assembly);

// Controllers
builder.Services.AddControllers();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<LaborDbContext>();

var app = builder.Build();

// Database migration on startup in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LaborDbContext>();
    db.Database.EnsureCreated();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Make Program accessible for WebApplicationFactory (tests)
public partial class Program { }
