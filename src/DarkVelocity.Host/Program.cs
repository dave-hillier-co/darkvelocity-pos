using Azure.Data.Tables;
using DarkVelocity.Host.Adapters;
using DarkVelocity.Host.Authorization;
using DarkVelocity.Host.Endpoints;
using DarkVelocity.Host.Extensions;
using DarkVelocity.Host.PaymentProcessors;
using DarkVelocity.Host.Services;
using DarkVelocity.Host.Streams;
using Orleans.Dashboard;

var builder = WebApplication.CreateBuilder(args);

// Azure Storage connection - uses Azurite for local dev, Azure Table Storage in production
var azureStorageConnectionString = builder.Configuration.GetConnectionString("AzureStorage")
    ?? "UseDevelopmentStorage=true";

// Configure Orleans Silo
builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();
    siloBuilder.AddMemoryGrainStorageAsDefault();
    siloBuilder.AddMemoryGrainStorage("PersistentStorage");
    siloBuilder.AddMemoryGrainStorage("OrleansStorage");

    // Azure Table Storage for JournaledGrain event sourcing
    siloBuilder.AddLogStorageBasedLogConsistencyProvider("LogStorage");
    siloBuilder.AddAzureTableGrainStorage("LogStorage", options =>
    {
        options.TableServiceClient = new TableServiceClient(azureStorageConnectionString);
    });

    // PubSubStore required for stream pub/sub
    siloBuilder.AddMemoryGrainStorage("PubSubStore");

    // Memory streaming provider for development
    // TODO: Replace with Azure Event Hubs or similar for production pub/sub
    siloBuilder.AddMemoryStreams(StreamConstants.DefaultStreamProvider);

    // Orleans Dashboard for real-time cluster monitoring
    siloBuilder.AddDashboard();
});

// Configure services
builder.Services
    .AddSwaggerDocumentation()
    .AddJwtAuthentication(builder.Configuration)
    .AddSpiceDbAuthorization(builder.Configuration)
    .AddCorsPolicy()
    .AddSearchServices(builder.Configuration)
    .AddPaymentGatewayServices()
    .AddDeliverectServices(builder.Configuration)
    .AddMemoryCache()
    .AddApiKeySeeder()
    .AddSingleton<IDocumentIntelligenceService, StubDocumentIntelligenceService>()
    .AddSingleton<IEmailIngestionService, StubEmailIngestionService>()
    .AddSingleton<IFuzzyMatchingService, FuzzyMatchingService>()
    // Notification services
    .AddSingleton<IEmailService, StubEmailService>()
    .AddSingleton<ISmsService, StubSmsService>()
    .AddSingleton<IPushService, StubPushService>()
    .AddSingleton<ISlackService, StubSlackService>()
    // Webhook delivery service
    .AddHttpClient<IWebhookDeliveryService, WebhookDeliveryService>();

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

// Orleans Dashboard at /dashboard
app.MapOrleansDashboard(routePrefix: "/dashboard");

// Map all API endpoints
app.MapOAuthEndpoints()
   .MapAuthEndpoints()
   .MapApiKeyEndpoints()
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
   .MapRoleEndpoints()
   .MapScheduleEndpoints()
   .MapTimeEntryEndpoints()
   .MapEmployeeAvailabilityEndpoints()
   .MapTimeOffEndpoints()
   .MapShiftSwapEndpoints()
   .MapTipPoolEndpoints()
   .MapPayrollEndpoints()
   .MapUserEndpoints()
   .MapSearchEndpoints()
   .MapTableEndpoints()
   .MapFloorPlanEndpoints()
   .MapWaitlistEndpoints()
   .MapAvailabilityEndpoints()
   .MapWebhookEndpoints()
   .MapPaymentGatewayEndpoints()
   .MapPaymentProcessorEndpoints()
   .MapChannelEndpoints()
   .MapDeliveryWebhookEndpoints()
   .MapExternalOrderEndpoints()
   .MapBatchEndpoints()
   .MapPurchaseDocumentEndpoints()
   .MapEmailIngestionEndpoints()
   .MapVendorMappingEndpoints()
   .MapExpenseEndpoints()
   .MapReportingEndpoints()
   .MapCostingEndpoints()
   .MapSystemEndpoints();

app.Run();

// Expose Program class for WebApplicationFactory integration testing
public partial class Program { }
