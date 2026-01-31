using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Configure Orleans Silo
builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();
    siloBuilder.AddMemoryGrainStorageAsDefault();
    siloBuilder.AddMemoryGrainStorage("PersistentStorage");
    siloBuilder.AddMemoryStreams("StreamProvider");
    siloBuilder.UseDashboard(options =>
    {
        options.Port = 8888;
        options.HostSelf = true;
    });
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",  // POS dev
                "http://localhost:5174"   // Back Office dev
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// ============================================================================
// Auth / User API Endpoints
// NOTE: Requires IUserGrain.GetSnapshotAsync, AuthenticateAsync, and CreateUserCommand updates
// ============================================================================

#if false // Auth API - requires grain interface updates
var authGroup = app.MapGroup("/api").WithTags("Auth");

authGroup.MapPost("/users", async (
    [FromBody] CreateUserRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(request.OrgId, request.UserId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/users/{request.UserId}", result);
});

authGroup.MapGet("/users/{orgId}/{userId}", async (
    Guid orgId,
    Guid userId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
    var result = await grain.GetSnapshotAsync();
    return Results.Ok(result);
});

authGroup.MapPost("/users/{orgId}/{userId}/login", async (
    Guid orgId,
    Guid userId,
    [FromBody] LoginRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
    var result = await grain.AuthenticateAsync(request.Password);
    return result != null ? Results.Ok(result) : Results.Unauthorized();
});
#endif

// ============================================================================
// Location API Endpoints
// NOTE: Requires GetSnapshotAsync, CreateOrganizationCommand, CreateSiteCommand updates
// ============================================================================

#if false // Location API - requires grain interface updates
var locationsGroup = app.MapGroup("/api/locations").WithTags("Locations");

locationsGroup.MapPost("/organizations", async (
    [FromBody] CreateOrganizationRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(request.OrgId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/locations/organizations/{request.OrgId}", result);
});

locationsGroup.MapGet("/organizations/{orgId}", async (
    Guid orgId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
    var result = await grain.GetSnapshotAsync();
    return Results.Ok(result);
});

locationsGroup.MapPost("/sites", async (
    [FromBody] CreateSiteRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(request.OrgId, request.SiteId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/locations/sites/{request.SiteId}", result);
});

locationsGroup.MapGet("/sites/{orgId}/{siteId}", async (
    Guid orgId,
    Guid siteId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));
    var result = await grain.GetSnapshotAsync();
    return Results.Ok(result);
});
#endif

// ============================================================================
// Menu API Endpoints
// NOTE: Requires ICategoryGrain, IModifierGrain, UpdatePriceAsync, CreateMenuItemCommand updates
// ============================================================================

#if false // Menu API - requires grain interface updates
var menuGroup = app.MapGroup("/api/menu").WithTags("Menu");

menuGroup.MapPost("/items", async (
    [FromBody] CreateMenuItemRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IMenuItemGrain>(GrainKeys.MenuItem(request.OrgId, request.MenuItemId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/menu/items/{request.MenuItemId}", result);
});

menuGroup.MapGet("/items/{orgId}/{menuItemId}", async (
    Guid orgId,
    Guid menuItemId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IMenuItemGrain>(GrainKeys.MenuItem(orgId, menuItemId));
    var result = await grain.GetSnapshotAsync();
    return Results.Ok(result);
});

menuGroup.MapPut("/items/{orgId}/{menuItemId}/price", async (
    Guid orgId,
    Guid menuItemId,
    [FromBody] UpdatePriceRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IMenuItemGrain>(GrainKeys.MenuItem(orgId, menuItemId));
    var result = await grain.UpdatePriceAsync(request.Price);
    return Results.Ok(result);
});

menuGroup.MapPost("/categories", async (
    [FromBody] CreateCategoryRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ICategoryGrain>(GrainKeys.Category(request.OrgId, request.CategoryId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/menu/categories/{request.CategoryId}", result);
});

menuGroup.MapPost("/modifiers", async (
    [FromBody] CreateModifierRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IModifierGrain>(GrainKeys.Modifier(request.OrgId, request.ModifierId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/menu/modifiers/{request.ModifierId}", result);
});
#endif

// ============================================================================
// Orders API Endpoints
// NOTE: Requires GrainKeys.Order 3-arg, IOrderGrain methods (GetSnapshotAsync, AddItemAsync, etc.)
// ============================================================================

#if false // Orders API - requires grain interface updates
var ordersGroup = app.MapGroup("/api/orders").WithTags("Orders");

ordersGroup.MapPost("/", async (
    [FromBody] CreateOrderRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(request.OrgId, request.OrderId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/orders/{request.OrderId}", result);
});

ordersGroup.MapGet("/{orgId}/{orderId}", async (
    Guid orgId,
    Guid orderId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, orderId));
    var result = await grain.GetSnapshotAsync();
    return Results.Ok(result);
});

ordersGroup.MapPost("/{orgId}/{orderId}/items", async (
    Guid orgId,
    Guid orderId,
    [FromBody] AddOrderItemRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, orderId));
    var result = await grain.AddItemAsync(request.ToCommand());
    return Results.Ok(result);
});

ordersGroup.MapPost("/{orgId}/{orderId}/submit", async (
    Guid orgId,
    Guid orderId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, orderId));
    var result = await grain.SubmitAsync();
    return Results.Ok(result);
});

ordersGroup.MapPost("/{orgId}/{orderId}/complete", async (
    Guid orgId,
    Guid orderId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, orderId));
    var result = await grain.CompleteAsync();
    return Results.Ok(result);
});

ordersGroup.MapPost("/{orgId}/{orderId}/cancel", async (
    Guid orgId,
    Guid orderId,
    [FromBody] CancelOrderRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, orderId));
    var result = await grain.CancelAsync(request.Reason);
    return Results.Ok(result);
});
#endif

// ============================================================================
// Payments API Endpoints
// NOTE: These endpoints require grain interface updates - commented out for now
// ============================================================================

#if false // Payments API - requires IPaymentGrain updates (CreateAsync, GetSnapshotAsync, ProcessAsync don't exist)
var paymentsGroup = app.MapGroup("/api/payments").WithTags("Payments");

paymentsGroup.MapPost("/", async (
    [FromBody] CreatePaymentRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(request.OrgId, request.PaymentId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/payments/{request.PaymentId}", result);
});

paymentsGroup.MapGet("/{orgId}/{paymentId}", async (
    Guid orgId,
    Guid paymentId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, paymentId));
    var result = await grain.GetSnapshotAsync();
    return Results.Ok(result);
});

paymentsGroup.MapPost("/{orgId}/{paymentId}/process", async (
    Guid orgId,
    Guid paymentId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, paymentId));
    var result = await grain.ProcessAsync();
    return Results.Ok(result);
});

paymentsGroup.MapPost("/refunds", async (
    [FromBody] CreateRefundApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IRefundGrain>(GrainKeys.Refund(request.OrgId, request.RefundId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/payments/refunds/{request.RefundId}", result);
});
#endif

// ============================================================================
// Inventory API Endpoints
// NOTE: IInventoryItemGrain and IInventoryCountGrain don't exist - use IInventoryGrain instead
// ============================================================================

#if false // Inventory API - requires IInventoryItemGrain and IInventoryCountGrain (don't exist)
var inventoryGroup = app.MapGroup("/api/inventory").WithTags("Inventory");

inventoryGroup.MapPost("/items", async (
    [FromBody] CreateInventoryItemRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IInventoryItemGrain>(GrainKeys.InventoryItem(request.OrgId, request.ItemId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/inventory/items/{request.ItemId}", result);
});

inventoryGroup.MapGet("/items/{orgId}/{itemId}", async (
    Guid orgId,
    Guid itemId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IInventoryItemGrain>(GrainKeys.InventoryItem(orgId, itemId));
    var result = await grain.GetSnapshotAsync();
    return Results.Ok(result);
});

inventoryGroup.MapPost("/items/{orgId}/{itemId}/adjust", async (
    Guid orgId,
    Guid itemId,
    [FromBody] AdjustInventoryRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IInventoryItemGrain>(GrainKeys.InventoryItem(orgId, itemId));
    var result = await grain.AdjustQuantityAsync(request.Adjustment, request.Reason);
    return Results.Ok(result);
});

inventoryGroup.MapPost("/counts", async (
    [FromBody] CreateInventoryCountRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IInventoryCountGrain>(GrainKeys.InventoryCount(request.OrgId, request.CountId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/inventory/counts/{request.CountId}", result);
});
#endif

// ============================================================================
// Hardware API Endpoints
// NOTE: Requires IDeviceGrain (doesn't exist)
// ============================================================================

#if false // Hardware API - requires IDeviceGrain
var hardwareGroup = app.MapGroup("/api/hardware").WithTags("Hardware");

hardwareGroup.MapPost("/devices", async (
    [FromBody] RegisterDeviceRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(request.OrgId, request.DeviceId));
    var result = await grain.RegisterAsync(request.ToCommand());
    return Results.Created($"/api/hardware/devices/{request.DeviceId}", result);
});

hardwareGroup.MapGet("/devices/{orgId}/{deviceId}", async (
    Guid orgId,
    Guid deviceId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, deviceId));
    var result = await grain.GetSnapshotAsync();
    return Results.Ok(result);
});

hardwareGroup.MapPost("/devices/{orgId}/{deviceId}/heartbeat", async (
    Guid orgId,
    Guid deviceId,
    [FromBody] HeartbeatRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, deviceId));
    await grain.HeartbeatAsync(request.IpAddress, request.SoftwareVersion);
    return Results.Ok();
});

hardwareGroup.MapPost("/terminals", async (
    [FromBody] RegisterTerminalApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ITerminalGrain>(GrainKeys.Terminal(request.OrgId, request.TerminalId));
    var result = await grain.RegisterAsync(request.ToCommand());
    return Results.Created($"/api/hardware/terminals/{request.TerminalId}", result);
});
#endif

// ============================================================================
// Procurement API Endpoints
// NOTE: Requires SubmitAsync command, CreatePurchaseOrderCommand updates
// ============================================================================

#if false // Procurement API - requires grain interface updates
var procurementGroup = app.MapGroup("/api/procurement").WithTags("Procurement");

procurementGroup.MapPost("/orders", async (
    [FromBody] CreatePurchaseOrderRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IPurchaseOrderGrain>(GrainKeys.PurchaseOrder(request.OrgId, request.OrderId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/procurement/orders/{request.OrderId}", result);
});

procurementGroup.MapGet("/orders/{orgId}/{orderId}", async (
    Guid orgId,
    Guid orderId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IPurchaseOrderGrain>(GrainKeys.PurchaseOrder(orgId, orderId));
    var result = await grain.GetSnapshotAsync();
    return Results.Ok(result);
});

procurementGroup.MapPost("/orders/{orgId}/{orderId}/submit", async (
    Guid orgId,
    Guid orderId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IPurchaseOrderGrain>(GrainKeys.PurchaseOrder(orgId, orderId));
    var result = await grain.SubmitAsync();
    return Results.Ok(result);
});

procurementGroup.MapPost("/suppliers", async (
    [FromBody] CreateSupplierRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ISupplierGrain>(GrainKeys.Supplier(request.OrgId, request.SupplierId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/procurement/suppliers/{request.SupplierId}", result);
});
#endif

// ============================================================================
// Costing API Endpoints
// NOTE: May work partially, leaving enabled for verification
// ============================================================================

#if false // Costing API - requires verification of grain interfaces
var costingGroup = app.MapGroup("/api/costing").WithTags("Costing");

costingGroup.MapPost("/recipes", async (
    [FromBody] CreateRecipeApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(request.OrgId, request.RecipeId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/costing/recipes/{request.RecipeId}", result);
});

costingGroup.MapGet("/recipes/{orgId}/{recipeId}", async (
    Guid orgId,
    Guid recipeId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));
    var result = await grain.GetSnapshotAsync();
    return Results.Ok(result);
});

costingGroup.MapPost("/recipes/{orgId}/{recipeId}/calculate", async (
    Guid orgId,
    Guid recipeId,
    [FromBody] CalculateCostRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IRecipeGrain>(GrainKeys.Recipe(orgId, recipeId));
    var result = await grain.CalculateCostAsync(request.MenuPrice);
    return Results.Ok(result);
});

costingGroup.MapPost("/ingredients", async (
    [FromBody] CreateIngredientPriceApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IIngredientPriceGrain>(GrainKeys.IngredientPrice(request.OrgId, request.IngredientId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/costing/ingredients/{request.IngredientId}", result);
});

costingGroup.MapPost("/alerts", async (
    [FromBody] CreateCostAlertApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ICostAlertGrain>(GrainKeys.CostAlert(request.OrgId, request.AlertId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/costing/alerts/{request.AlertId}", result);
});
#endif

// ============================================================================
// Reporting API Endpoints
// NOTE: Requires IDailySalesReportGrain (doesn't exist)
// ============================================================================

#if false // Reporting API - requires IDailySalesReportGrain
var reportsGroup = app.MapGroup("/api/reports").WithTags("Reports");

reportsGroup.MapGet("/sales/{orgId}/{siteId}/{date}", async (
    Guid orgId,
    Guid siteId,
    DateOnly date,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IDailySalesReportGrain>(GrainKeys.DailySalesReport(orgId, siteId, date));
    var result = await grain.GetReportAsync();
    return Results.Ok(result);
});

reportsGroup.MapPost("/sales/{orgId}/{siteId}/{date}/generate", async (
    Guid orgId,
    Guid siteId,
    DateOnly date,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IDailySalesReportGrain>(GrainKeys.DailySalesReport(orgId, siteId, date));
    await grain.GenerateAsync();
    return Results.Ok();
});
#endif

// ============================================================================
// Payment Gateway API Endpoints
// NOTE: May work partially, needs verification
// ============================================================================

#if false // Payment Gateway API - requires verification of grain interfaces
var gatewayGroup = app.MapGroup("/api/gateway").WithTags("PaymentGateway");

gatewayGroup.MapPost("/merchants", async (
    [FromBody] CreateMerchantApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IMerchantGrain>(GrainKeys.Merchant(request.OrgId, request.MerchantId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/gateway/merchants/{request.MerchantId}", result);
});

gatewayGroup.MapGet("/merchants/{orgId}/{merchantId}", async (
    Guid orgId,
    Guid merchantId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IMerchantGrain>(GrainKeys.Merchant(orgId, merchantId));
    var result = await grain.GetSnapshotAsync();
    return Results.Ok(result);
});

gatewayGroup.MapPost("/webhooks", async (
    [FromBody] CreateWebhookApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IWebhookEndpointGrain>(GrainKeys.Webhook(request.OrgId, request.EndpointId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/gateway/webhooks/{request.EndpointId}", result);
});
#endif

// ============================================================================
// Booking API Endpoints
// NOTE: Requires GrainKeys.Booking 3-arg, CreateAsync, GetSnapshotAsync, CancelAsync command
// ============================================================================

#if false // Booking API - requires grain interface updates
var bookingGroup = app.MapGroup("/api/booking").WithTags("Booking");

bookingGroup.MapPost("/reservations", async (
    [FromBody] CreateBookingApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(request.OrgId, request.BookingId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/booking/reservations/{request.BookingId}", result);
});

bookingGroup.MapGet("/reservations/{orgId}/{bookingId}", async (
    Guid orgId,
    Guid bookingId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, bookingId));
    var result = await grain.GetSnapshotAsync();
    return Results.Ok(result);
});

bookingGroup.MapPost("/reservations/{orgId}/{bookingId}/confirm", async (
    Guid orgId,
    Guid bookingId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, bookingId));
    var result = await grain.ConfirmAsync();
    return Results.Ok(result);
});

bookingGroup.MapPost("/reservations/{orgId}/{bookingId}/cancel", async (
    Guid orgId,
    Guid bookingId,
    [FromBody] CancelBookingRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, bookingId));
    var result = await grain.CancelAsync(request.Reason);
    return Results.Ok(result);
});

bookingGroup.MapPost("/tables", async (
    [FromBody] CreateTableApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ITableGrain>(GrainKeys.Table(request.OrgId, request.TableId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/booking/tables/{request.TableId}", result);
});

bookingGroup.MapGet("/tables/{orgId}/{tableId}", async (
    Guid orgId,
    Guid tableId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ITableGrain>(GrainKeys.Table(orgId, tableId));
    var result = await grain.GetSnapshotAsync();
    return Results.Ok(result);
});
#endif

// ============================================================================
// Labor API Endpoints
// NOTE: Requires GetSnapshotAsync, ClockInAsync, ClockOutAsync updates, IShiftGrain
// ============================================================================

#if false // Labor API - requires grain interface updates
var laborGroup = app.MapGroup("/api/labor").WithTags("Labor");

laborGroup.MapPost("/employees", async (
    [FromBody] CreateEmployeeApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(request.OrgId, request.EmployeeId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/labor/employees/{request.EmployeeId}", result);
});

laborGroup.MapGet("/employees/{orgId}/{employeeId}", async (
    Guid orgId,
    Guid employeeId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
    var result = await grain.GetSnapshotAsync();
    return Results.Ok(result);
});

laborGroup.MapPost("/employees/{orgId}/{employeeId}/clock-in", async (
    Guid orgId,
    Guid employeeId,
    [FromBody] ClockInRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
    var result = await grain.ClockInAsync(request.SiteId, request.RoleId);
    return Results.Ok(result);
});

laborGroup.MapPost("/employees/{orgId}/{employeeId}/clock-out", async (
    Guid orgId,
    Guid employeeId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
    var result = await grain.ClockOutAsync();
    return Results.Ok(result);
});

laborGroup.MapPost("/shifts", async (
    [FromBody] CreateShiftApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IShiftGrain>(GrainKeys.Shift(request.OrgId, request.ShiftId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/labor/shifts/{request.ShiftId}", result);
});

laborGroup.MapPost("/time-off", async (
    [FromBody] CreateTimeOffApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ITimeOffGrain>(GrainKeys.TimeOffRequest(request.OrgId, request.RequestId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/labor/time-off/{request.RequestId}", result);
});
#endif

// ============================================================================
// Customers API Endpoints
// NOTE: Requires GetSnapshotAsync, CreateCustomerCommand, CreateLoyaltyProgramCommand updates
// ============================================================================

#if false // Customers API - requires grain interface updates
var customersGroup = app.MapGroup("/api/customers").WithTags("Customers");

customersGroup.MapPost("/", async (
    [FromBody] CreateCustomerApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(request.OrgId, request.CustomerId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/customers/{request.CustomerId}", result);
});

customersGroup.MapGet("/{orgId}/{customerId}", async (
    Guid orgId,
    Guid customerId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
    var result = await grain.GetSnapshotAsync();
    return Results.Ok(result);
});

customersGroup.MapPost("/loyalty", async (
    [FromBody] CreateLoyaltyProgramApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ILoyaltyProgramGrain>(GrainKeys.LoyaltyProgram(request.OrgId, request.ProgramId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/customers/loyalty/{request.ProgramId}", result);
});
#endif

// ============================================================================
// Gift Cards API Endpoints
// NOTE: Requires GetSnapshotAsync, RedeemAsync signature, CreateGiftCardCommand updates
// ============================================================================

#if false // Gift Cards API - requires grain interface updates
var giftCardsGroup = app.MapGroup("/api/giftcards").WithTags("GiftCards");

giftCardsGroup.MapPost("/", async (
    [FromBody] CreateGiftCardApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(request.OrgId, request.GiftCardId));
    var result = await grain.CreateAsync(request.ToCommand());
    return Results.Created($"/api/giftcards/{request.GiftCardId}", result);
});

giftCardsGroup.MapGet("/{orgId}/{giftCardId}", async (
    Guid orgId,
    Guid giftCardId,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, giftCardId));
    var result = await grain.GetSnapshotAsync();
    return Results.Ok(result);
});

giftCardsGroup.MapPost("/{orgId}/{giftCardId}/redeem", async (
    Guid orgId,
    Guid giftCardId,
    [FromBody] RedeemGiftCardRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, giftCardId));
    var result = await grain.RedeemAsync(request.Amount, request.OrderId, request.TransactionRef);
    return Results.Ok(result);
});
#endif

app.Run();

// ============================================================================
// Request DTOs
// NOTE: All DTOs below are disabled pending grain interface alignment
// ============================================================================

#if false // Request DTOs - disabled pending grain interface updates
public record CreateUserRequest(Guid OrgId, Guid UserId, string Email, string Password, string FirstName, string LastName, string? Role)
{
    public CreateUserCommand ToCommand() => new(Email, Password, FirstName, LastName, Role ?? "user");
}

public record LoginRequest(string Password);

public record CreateOrganizationRequest(Guid OrgId, string Name, string Country, string Currency, string? Timezone)
{
    public CreateOrganizationCommand ToCommand() => new(Name, Country, Currency, Timezone);
}

public record CreateSiteRequest(Guid OrgId, Guid SiteId, string Name, string Type, string? AddressLine1, string? City, string? State, string? PostalCode, string? Country, string? Timezone)
{
    public CreateSiteCommand ToCommand() => new(Name, Type, AddressLine1, City, State, PostalCode, Country, Timezone);
}

public record CreateMenuItemRequest(Guid OrgId, Guid MenuItemId, string Name, string? Description, decimal Price, Guid CategoryId, string? Sku)
{
    public CreateMenuItemCommand ToCommand() => new(Name, Description, Price, CategoryId, Sku);
}

public record UpdatePriceRequest(decimal Price);

public record CreateCategoryRequest(Guid OrgId, Guid CategoryId, string Name, string? Description, int SortOrder)
{
    public CreateCategoryCommand ToCommand() => new(Name, Description, SortOrder);
}

public record CreateModifierRequest(Guid OrgId, Guid ModifierId, string Name, string? Type, bool Required, int? MinSelections, int? MaxSelections)
{
    public CreateModifierCommand ToCommand() => new(Name, Type, Required, MinSelections, MaxSelections);
}

public record CreateOrderRequest(Guid OrgId, Guid OrderId, Guid SiteId, Guid? CustomerId, string OrderType, Guid? TableId)
{
    public CreateOrderCommand ToCommand() => new(SiteId, CustomerId, OrderType, TableId);
}

public record AddOrderItemRequest(Guid MenuItemId, string Name, int Quantity, decimal UnitPrice, List<OrderItemModifier>? Modifiers)
{
    public AddOrderItemCommand ToCommand() => new(MenuItemId, Name, Quantity, UnitPrice, Modifiers);
}

public record CancelOrderRequest(string? Reason);

public record CreatePaymentRequest(Guid OrgId, Guid PaymentId, Guid OrderId, decimal Amount, string Currency, string Method, string? Reference)
{
    public CreatePaymentCommand ToCommand() => new(OrderId, Amount, Currency, Method, Reference);
}

public record CreateRefundApiRequest(Guid OrgId, Guid RefundId, Guid PaymentIntentId, long Amount, string Currency, string? Reason)
{
    public CreateRefundCommand ToCommand() => new(PaymentIntentId, Amount, Currency, Reason, null);
}

public record CreateInventoryItemRequest(Guid OrgId, Guid ItemId, string Name, string Sku, string? Category, string UnitOfMeasure, decimal CurrentQuantity, decimal? ReorderPoint, decimal? ReorderQuantity)
{
    public CreateInventoryItemCommand ToCommand() => new(Name, Sku, Category, UnitOfMeasure, CurrentQuantity, ReorderPoint, ReorderQuantity);
}

public record AdjustInventoryRequest(decimal Adjustment, string? Reason);

public record CreateInventoryCountRequest(Guid OrgId, Guid CountId, Guid SiteId, string? Type, List<Guid>? CategoryIds)
{
    public CreateInventoryCountCommand ToCommand() => new(SiteId, Type, CategoryIds);
}

public record RegisterDeviceRequest(Guid OrgId, Guid DeviceId, string Name, string DeviceType, string? SerialNumber, Guid SiteId)
{
    public RegisterDeviceCommand ToCommand() => new(Name, DeviceType, SerialNumber, SiteId);
}

public record HeartbeatRequest(string? IpAddress, string? SoftwareVersion);

public record RegisterTerminalApiRequest(Guid OrgId, Guid TerminalId, Guid LocationId, string Label, string? DeviceType, string? SerialNumber)
{
    public RegisterTerminalCommand ToCommand() => new(LocationId, Label, DeviceType, SerialNumber, null);
}

public record CreatePurchaseOrderRequest(Guid OrgId, Guid OrderId, Guid SupplierId, string SupplierName, Guid SiteId, DateOnly? ExpectedDeliveryDate)
{
    public CreatePurchaseOrderCommand ToCommand() => new(SupplierId, SupplierName, SiteId, ExpectedDeliveryDate);
}

public record CreateSupplierRequest(Guid OrgId, Guid SupplierId, string Name, string? ContactName, string? Email, string? Phone, string? AddressLine1, string? City, string? State, string? PostalCode, string? Country)
{
    public CreateSupplierCommand ToCommand() => new(Name, ContactName, Email, Phone, AddressLine1, City, State, PostalCode, Country);
}

public record CreateRecipeApiRequest(Guid OrgId, Guid RecipeId, Guid MenuItemId, string MenuItemName, string Code, Guid? CategoryId, string? CategoryName, string? Description, int PortionYield, string? PrepInstructions)
{
    public CreateRecipeCommand ToCommand() => new(MenuItemId, MenuItemName, Code, CategoryId, CategoryName, Description, PortionYield, PrepInstructions);
}

public record CalculateCostRequest(decimal MenuPrice);

public record CreateIngredientPriceApiRequest(Guid OrgId, Guid IngredientId, string IngredientName, decimal CurrentPrice, string UnitOfMeasure, decimal PackSize, Guid? PreferredSupplierId, string? PreferredSupplierName)
{
    public CreateIngredientPriceCommand ToCommand() => new(IngredientId, IngredientName, CurrentPrice, UnitOfMeasure, PackSize, PreferredSupplierId, PreferredSupplierName);
}

public record CreateCostAlertApiRequest(Guid OrgId, Guid AlertId, CostAlertType AlertType, Guid? RecipeId, string? RecipeName, Guid? IngredientId, string? IngredientName, Guid? MenuItemId, string? MenuItemName, decimal PreviousValue, decimal CurrentValue, decimal ThresholdValue, string? ImpactDescription, int AffectedRecipeCount)
{
    public CreateCostAlertCommand ToCommand() => new(AlertType, RecipeId, RecipeName, IngredientId, IngredientName, MenuItemId, MenuItemName, PreviousValue, CurrentValue, ThresholdValue, ImpactDescription, AffectedRecipeCount);
}

public record CreateMerchantApiRequest(Guid OrgId, Guid MerchantId, string Name, string Email, string BusinessName, string? BusinessType, string Country, string DefaultCurrency, string? StatementDescriptor, string? AddressLine1, string? AddressLine2, string? City, string? State, string? PostalCode)
{
    public CreateMerchantCommand ToCommand() => new(Name, Email, BusinessName, BusinessType, Country, DefaultCurrency, StatementDescriptor, AddressLine1, AddressLine2, City, State, PostalCode, null);
}

public record CreateWebhookApiRequest(Guid OrgId, Guid EndpointId, string Url, string? Description, string[] EnabledEvents, string? Secret)
{
    public CreateWebhookEndpointCommand ToCommand() => new(Url, Description, EnabledEvents, Secret);
}

public record CreateBookingApiRequest(Guid OrgId, Guid BookingId, Guid SiteId, string CustomerName, string? CustomerPhone, string? CustomerEmail, DateTime BookingTime, int PartySize, string? SpecialRequests)
{
    public CreateBookingCommand ToCommand() => new(SiteId, CustomerName, CustomerPhone, CustomerEmail, BookingTime, PartySize, SpecialRequests);
}

public record CancelBookingRequest(string? Reason);

public record CreateTableApiRequest(Guid OrgId, Guid TableId, Guid SiteId, string Name, int Capacity, string? Section, int? PositionX, int? PositionY)
{
    public CreateTableCommand ToCommand() => new(SiteId, Name, Capacity, Section, PositionX, PositionY);
}

public record CreateEmployeeApiRequest(Guid OrgId, Guid EmployeeId, string FirstName, string LastName, string Email, string? Phone, Guid PrimaryRoleId, decimal HourlyRate, DateOnly StartDate)
{
    public CreateEmployeeCommand ToCommand() => new(FirstName, LastName, Email, Phone, PrimaryRoleId, HourlyRate, StartDate);
}

public record ClockInRequest(Guid SiteId, Guid? RoleId);

public record CreateShiftApiRequest(Guid OrgId, Guid ShiftId, Guid EmployeeId, Guid SiteId, Guid RoleId, DateTime StartTime, DateTime EndTime, string? Notes)
{
    public CreateShiftCommand ToCommand() => new(EmployeeId, SiteId, RoleId, StartTime, EndTime, Notes);
}

public record CreateTimeOffApiRequest(Guid OrgId, Guid RequestId, Guid EmployeeId, TimeOffType Type, DateOnly StartDate, DateOnly EndDate, string? Reason)
{
    public CreateTimeOffCommand ToCommand() => new(EmployeeId, Type, StartDate, EndDate, Reason);
}

public record CreateCustomerApiRequest(Guid OrgId, Guid CustomerId, string FirstName, string LastName, string? Email, string? Phone)
{
    public CreateCustomerCommand ToCommand() => new(FirstName, LastName, Email, Phone);
}

public record CreateLoyaltyProgramApiRequest(Guid OrgId, Guid ProgramId, string Name, string? Description, decimal PointsPerDollar, decimal PointsToReward, decimal RewardValue)
{
    public CreateLoyaltyProgramCommand ToCommand() => new(Name, Description, PointsPerDollar, PointsToReward, RewardValue);
}

public record CreateGiftCardApiRequest(Guid OrgId, Guid GiftCardId, string Code, decimal InitialBalance, string Currency, DateOnly? ExpirationDate)
{
    public CreateGiftCardCommand ToCommand() => new(Code, InitialBalance, Currency, ExpirationDate);
}

public record RedeemGiftCardRequest(decimal Amount, Guid OrderId, string TransactionRef);

// ============================================================================
// Command Aliases for API-to-Grain mapping
// These records bridge the simplified API request commands to the actual grain commands
// ============================================================================

/// <summary>Alias for CreateMenuCategoryCommand with simplified parameters</summary>
public record CreateCategoryCommand(string Name, string? Description, int SortOrder);

/// <summary>Command for creating modifiers (simplified)</summary>
public record CreateModifierCommand(string Name, string? Type, bool Required, int? MinSelections, int? MaxSelections);

/// <summary>Alias for OrderLineModifier for API requests</summary>
public record OrderItemModifier(Guid ModifierId, string Name, decimal Price, int Quantity);

/// <summary>Alias for AddLineCommand with simplified parameters</summary>
public record AddOrderItemCommand(
    Guid MenuItemId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    List<OrderItemModifier>? Modifiers);

/// <summary>Alias for InitiatePaymentCommand with simplified parameters</summary>
public record CreatePaymentCommand(Guid OrderId, decimal Amount, string Currency, string Method, string? Reference);

/// <summary>Alias for InitializeInventoryCommand with simplified parameters</summary>
public record CreateInventoryItemCommand(
    string Name,
    string Sku,
    string? Category,
    string UnitOfMeasure,
    decimal CurrentQuantity,
    decimal? ReorderPoint,
    decimal? ReorderQuantity);

/// <summary>Command for creating inventory counts</summary>
public record CreateInventoryCountCommand(Guid SiteId, string? Type, List<Guid>? CategoryIds);

/// <summary>Alias for RegisterPosDeviceCommand with simplified parameters</summary>
public record RegisterDeviceCommand(string Name, string DeviceType, string? SerialNumber, Guid SiteId);

/// <summary>Alias for RequestBookingCommand with simplified parameters</summary>
public record CreateBookingCommand(
    Guid SiteId,
    string CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    DateTime BookingTime,
    int PartySize,
    string? SpecialRequests);

/// <summary>Alias for AddShiftCommand with simplified parameters</summary>
public record CreateShiftCommand(
    Guid EmployeeId,
    Guid SiteId,
    Guid RoleId,
    DateTime StartTime,
    DateTime EndTime,
    string? Notes);
#endif
