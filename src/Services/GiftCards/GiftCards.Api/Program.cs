using DarkVelocity.GiftCards.Api.Data;
using DarkVelocity.GiftCards.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database - only register if not in Test environment (tests configure their own provider)
if (!builder.Environment.IsEnvironment("Test"))
{
    builder.Services.AddDbContext<GiftCardsDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=giftcards_db;Username=darkvelocity;Password=darkvelocity_dev";
        options.UseNpgsql(connectionString);
    });
}

// Services
builder.Services.AddScoped<ICardNumberGenerator, CardNumberGenerator>();

builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<GiftCardsDbContext>();

var app = builder.Build();

// Migrate database on startup in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GiftCardsDbContext>();
    db.Database.EnsureCreated();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Make Program accessible for WebApplicationFactory
public partial class Program { }
