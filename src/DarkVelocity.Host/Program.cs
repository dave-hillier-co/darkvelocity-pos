using DarkVelocity.Host.Endpoints;
using DarkVelocity.Host.Extensions;
using DarkVelocity.Host.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Orleans Silo
builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();
    siloBuilder.AddMemoryGrainStorageAsDefault();
    siloBuilder.AddMemoryGrainStorage("PersistentStorage");
    siloBuilder.AddMemoryGrainStorage("OrleansStorage");

    // Log consistency provider for JournaledGrain event sourcing
    siloBuilder.AddLogStorageBasedLogConsistencyProvider("LogStorage");
    siloBuilder.AddMemoryGrainStorage("LogStorage");

    siloBuilder.AddMemoryStreams("StreamProvider");
    siloBuilder.UseDashboard(options =>
    {
        options.Port = 8888;
        options.HostSelf = true;
    });
});

// Configure services
builder.Services
    .AddSwaggerDocumentation()
    .AddJwtAuthentication(builder.Configuration)
    .AddCorsPolicy()
    .AddSearchServices(builder.Configuration)
    .AddPaymentGatewayServices()
    .AddSingleton<IDocumentIntelligenceService, StubDocumentIntelligenceService>()
    .AddSingleton<IEmailIngestionService, StubEmailIngestionService>()
    .AddSingleton<IFuzzyMatchingService, FuzzyMatchingService>();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DarkVelocity POS API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
   .WithTags("Health");

// Map all API endpoints
app.MapOAuthEndpoints()
   .MapAuthEndpoints()
   .MapDeviceEndpoints()
   .MapOrganizationEndpoints()
   .MapSiteEndpoints()
   .MapOrderEndpoints()
   .MapPaymentEndpoints()
   .MapMenuEndpoints()
   .MapMenuCmsEndpoints()
   .MapCustomerEndpoints()
   .MapInventoryEndpoints()
   .MapBookingEndpoints()
   .MapEmployeeEndpoints()
   .MapSearchEndpoints()
   .MapTableEndpoints()
   .MapFloorPlanEndpoints()
   .MapWaitlistEndpoints()
   .MapAvailabilityEndpoints()
   .MapWebhookEndpoints()
   .MapPaymentGatewayEndpoints()
   .MapChannelEndpoints()
   .MapBatchEndpoints()
   .MapPurchaseDocumentEndpoints()
   .MapEmailIngestionEndpoints()
   .MapVendorMappingEndpoints()
   .MapExpenseEndpoints();

app.Run();

// Expose Program class for WebApplicationFactory integration testing
public partial class Program { }
