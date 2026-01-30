using DarkVelocity.OrdersGateway.Api.Adapters;
using DarkVelocity.OrdersGateway.Api.Data;
using DarkVelocity.OrdersGateway.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
if (!builder.Environment.IsEnvironment("Test"))
{
    builder.Services.AddDbContext<OrdersGatewayDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=ordersgateway_db;Username=darkvelocity;Password=darkvelocity_dev";
        options.UseNpgsql(connectionString);
    });
}

// Register delivery platform adapters
builder.Services.AddDeliveryPlatformAdapters();

// Register services
builder.Services.AddScoped<IExternalOrderService, ExternalOrderService>();
builder.Services.AddScoped<IAutoAcceptEngine, AutoAcceptEngine>();
builder.Services.AddScoped<IMenuSyncService, MenuSyncService>();

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Orders Gateway API",
        Version = "v1",
        Description = "Unified integration hub for third-party delivery platforms (Uber Eats, DoorDash, Deliveroo, Just Eat, etc.)"
    });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrdersGatewayDbContext>();

var app = builder.Build();

// Auto-migrate in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OrdersGatewayDbContext>();
    db.Database.EnsureCreated();
}

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
