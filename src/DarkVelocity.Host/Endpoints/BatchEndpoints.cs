using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class BatchEndpoints
{
    public static WebApplication MapBatchEndpoints(this WebApplication app)
    {
        app.MapMenuBatchEndpoints();
        app.MapInventoryBatchEndpoints();
        app.MapOrderBatchEndpoints();
        app.MapCustomerBatchEndpoints();
        app.MapPaymentBatchEndpoints();

        return app;
    }

    // =========================================================================
    // Menu Batch Endpoints
    // =========================================================================

    private static WebApplication MapMenuBatchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/menu/batch").WithTags("Menu Batch");

        group.MapPost("/upsert", async (
            Guid orgId,
            [FromBody] BatchUpsertMenuItemsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuBatchGrain>(GrainKeys.MenuBatch(orgId));
            var results = await grain.BatchUpsertAsync(request.IdempotencyKey, request.Items);
            return Results.Ok(Hal.Resource(new { results, count = results.Count }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/batch/upsert" }
            }));
        });

        group.MapPost("/retrieve", async (
            Guid orgId,
            [FromBody] BatchRetrieveRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuBatchGrain>(GrainKeys.MenuBatch(orgId));
            var result = await grain.BatchRetrieveAsync(request.Ids);
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/batch/retrieve" }
            }));
        });

        group.MapPost("/delete", async (
            Guid orgId,
            [FromBody] BatchDeleteRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuBatchGrain>(GrainKeys.MenuBatch(orgId));
            var result = await grain.BatchDeleteAsync(request.Ids);
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/batch/delete" }
            }));
        });

        group.MapPost("/search", async (
            Guid orgId,
            [FromBody] MenuSearchFilter filter,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuBatchGrain>(GrainKeys.MenuBatch(orgId));
            var result = await grain.SearchAsync(filter);
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/batch/search" }
            }));
        });

        group.MapGet("/list", async (
            Guid orgId,
            int? limit,
            string? cursor,
            bool? includeInactive,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuBatchGrain>(GrainKeys.MenuBatch(orgId));
            var result = await grain.ListAsync(limit ?? 100, cursor, includeInactive ?? false);
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/batch/list" }
            }));
        });

        return app;
    }

    // =========================================================================
    // Inventory Batch Endpoints
    // =========================================================================

    private static WebApplication MapInventoryBatchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/inventory/batch").WithTags("Inventory Batch");

        group.MapPost("/change", async (
            Guid orgId,
            Guid siteId,
            [FromBody] BatchInventoryChangeRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryBatchGrain>(GrainKeys.InventoryBatch(orgId, siteId));
            var results = await grain.BatchChangeAsync(request.IdempotencyKey, request.Changes);
            return Results.Ok(Hal.Resource(new { results, count = results.Count }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/batch/change" }
            }));
        });

        group.MapPost("/counts", async (
            Guid orgId,
            Guid siteId,
            [FromBody] BatchInventoryCountsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryBatchGrain>(GrainKeys.InventoryBatch(orgId, siteId));
            var results = await grain.BatchRetrieveCountsAsync(request.Items);
            return Results.Ok(Hal.Resource(new { items = results, count = results.Count }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/batch/counts" }
            }));
        });

        group.MapPost("/physical-count", async (
            Guid orgId,
            Guid siteId,
            [FromBody] BatchPhysicalCountRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryBatchGrain>(GrainKeys.InventoryBatch(orgId, siteId));
            var counts = request.Counts.Select(c => (c.InventoryItemId, c.CountedQuantity, c.CountedBy)).ToList();
            var results = await grain.BatchPhysicalCountAsync(request.IdempotencyKey, counts);
            return Results.Ok(Hal.Resource(new { results, count = results.Count }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/batch/physical-count" }
            }));
        });

        group.MapPost("/transfer", async (
            Guid orgId,
            Guid siteId,
            [FromBody] BatchTransferRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryBatchGrain>(GrainKeys.InventoryBatch(orgId, siteId));
            var result = await grain.BatchTransferAsync(request.IdempotencyKey, request.Transfers);
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/batch/transfer" }
            }));
        });

        group.MapPost("/search", async (
            Guid orgId,
            Guid siteId,
            [FromBody] InventorySearchFilter filter,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryBatchGrain>(GrainKeys.InventoryBatch(orgId, siteId));
            var result = await grain.SearchAsync(filter);
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/batch/search" }
            }));
        });

        group.MapGet("/low-stock", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryBatchGrain>(GrainKeys.InventoryBatch(orgId, siteId));
            var items = await grain.GetBelowReorderPointAsync(siteId);
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/inventory/batch/low-stock", items.Cast<object>(), items.Count));
        });

        group.MapGet("/expiring", async (Guid orgId, Guid siteId, int? days, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryBatchGrain>(GrainKeys.InventoryBatch(orgId, siteId));
            var items = await grain.GetExpiringSoonAsync(siteId, days ?? 7);
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/inventory/batch/expiring", items.Cast<object>(), items.Count));
        });

        return app;
    }

    // =========================================================================
    // Order Batch Endpoints
    // =========================================================================

    private static WebApplication MapOrderBatchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/orders/batch").WithTags("Orders Batch");

        group.MapPost("/search", async (
            Guid orgId,
            Guid siteId,
            [FromBody] OrderSearchFilter filter,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderBatchGrain>(GrainKeys.OrderBatch(orgId, siteId));
            var result = await grain.SearchAsync(filter);
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/batch/search" }
            }));
        });

        group.MapPost("/retrieve", async (
            Guid orgId,
            Guid siteId,
            [FromBody] BatchRetrieveRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderBatchGrain>(GrainKeys.OrderBatch(orgId, siteId));
            var result = await grain.BatchRetrieveAsync(request.Ids);
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/batch/retrieve" }
            }));
        });

        group.MapPost("/clone", async (
            Guid orgId,
            Guid siteId,
            [FromBody] CloneOrderApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderBatchGrain>(GrainKeys.OrderBatch(orgId, siteId));
            var result = await grain.CloneAsync(new CloneOrderRequest(
                request.SourceOrderId, request.CreatedBy, request.NewType, request.NewTableId, request.NewTableNumber,
                request.IncludeDiscounts, request.IncludeServiceCharges));
            return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/orders/{result.NewOrderId}", Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{result.NewOrderId}" },
                ["source"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{request.SourceOrderId}" }
            }));
        });

        group.MapPost("/calculate", async (
            Guid orgId,
            Guid siteId,
            [FromBody] CalculateOrderRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderBatchGrain>(GrainKeys.OrderBatch(orgId, siteId));
            var serviceCharges = request.ServiceCharges?.Select(s => (s.Name, s.Rate, s.IsTaxable)).ToList();
            var totals = await grain.CalculateAsync(request.Lines, request.Discounts, serviceCharges, request.TaxRate);
            return Results.Ok(Hal.Resource(totals, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/batch/calculate" }
            }));
        });

        group.MapGet("/open", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderBatchGrain>(GrainKeys.OrderBatch(orgId, siteId));
            var orders = await grain.GetOpenOrdersAsync(siteId);
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/orders/batch/open", orders.Cast<object>(), orders.Count));
        });

        group.MapGet("/table/{tableId}", async (Guid orgId, Guid siteId, Guid tableId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderBatchGrain>(GrainKeys.OrderBatch(orgId, siteId));
            var orders = await grain.GetOrdersByTableAsync(siteId, tableId);
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/orders/batch/table/{tableId}", orders.Cast<object>(), orders.Count));
        });

        return app;
    }

    // =========================================================================
    // Customer Batch Endpoints
    // =========================================================================

    private static WebApplication MapCustomerBatchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/customers/batch").WithTags("Customers Batch");

        group.MapPost("/search", async (
            Guid orgId,
            [FromBody] CustomerSearchFilter filter,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerBatchGrain>(GrainKeys.CustomerBatch(orgId));
            var result = await grain.SearchAsync(filter);
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/customers/batch/search" }
            }));
        });

        group.MapPost("/retrieve", async (
            Guid orgId,
            [FromBody] BatchRetrieveRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerBatchGrain>(GrainKeys.CustomerBatch(orgId));
            var result = await grain.BatchRetrieveAsync(request.Ids);
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/customers/batch/retrieve" }
            }));
        });

        group.MapPost("/create", async (
            Guid orgId,
            [FromBody] BatchCreateCustomersRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerBatchGrain>(GrainKeys.CustomerBatch(orgId));
            var results = await grain.BatchCreateAsync(request.IdempotencyKey, request.Customers);
            return Results.Ok(Hal.Resource(new { results, count = results.Count }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/customers/batch/create" }
            }));
        });

        group.MapPost("/groups", async (
            Guid orgId,
            [FromBody] CreateCustomerGroupRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerBatchGrain>(GrainKeys.CustomerBatch(orgId));
            var group = await grain.CreateGroupAsync(request.Name, request.Description);
            return Results.Created($"/api/orgs/{orgId}/customers/groups/{group.GroupId}", Hal.Resource(group, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/customers/groups/{group.GroupId}" },
                ["members"] = new { href = $"/api/orgs/{orgId}/customers/groups/{group.GroupId}/members" }
            }));
        });

        group.MapGet("/groups", async (Guid orgId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerBatchGrain>(GrainKeys.CustomerBatch(orgId));
            var groups = await grain.GetGroupsAsync();
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/customers/batch/groups", groups.Cast<object>(), groups.Count));
        });

        group.MapPost("/groups/{groupId}/members", async (
            Guid orgId,
            Guid groupId,
            [FromBody] AddToGroupRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerBatchGrain>(GrainKeys.CustomerBatch(orgId));
            var result = await grain.AddToGroupAsync(groupId, request.CustomerIds);
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["group"] = new { href = $"/api/orgs/{orgId}/customers/groups/{groupId}" }
            }));
        });

        group.MapGet("/groups/{groupId}/members", async (
            Guid orgId,
            Guid groupId,
            int? limit,
            string? cursor,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerBatchGrain>(GrainKeys.CustomerBatch(orgId));
            var result = await grain.GetGroupMembersAsync(groupId, limit ?? 50, cursor);
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/customers/groups/{groupId}/members" },
                ["group"] = new { href = $"/api/orgs/{orgId}/customers/groups/{groupId}" }
            }));
        });

        group.MapGet("/lookup/email/{email}", async (Guid orgId, string email, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerBatchGrain>(GrainKeys.CustomerBatch(orgId));
            var customer = await grain.LookupByEmailAsync(email);
            if (customer == null)
                return Results.NotFound(Hal.Error("not_found", "Customer not found"));
            return Results.Ok(Hal.Resource(customer, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/customers/{customer.CustomerId}" }
            }));
        });

        group.MapGet("/lookup/phone/{phone}", async (Guid orgId, string phone, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerBatchGrain>(GrainKeys.CustomerBatch(orgId));
            var customer = await grain.LookupByPhoneAsync(phone);
            if (customer == null)
                return Results.NotFound(Hal.Error("not_found", "Customer not found"));
            return Results.Ok(Hal.Resource(customer, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/customers/{customer.CustomerId}" }
            }));
        });

        group.MapGet("/top", async (Guid orgId, int? limit, Guid? siteId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerBatchGrain>(GrainKeys.CustomerBatch(orgId));
            var customers = await grain.GetTopCustomersAsync(limit ?? 10, null, siteId);
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/customers/batch/top", customers.Cast<object>(), customers.Count));
        });

        return app;
    }

    // =========================================================================
    // Payment Batch Endpoints
    // =========================================================================

    private static WebApplication MapPaymentBatchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/payments/batch").WithTags("Payments Batch");

        group.MapPost("/search", async (
            Guid orgId,
            Guid siteId,
            [FromBody] PaymentSearchFilter filter,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPaymentBatchGrain>(GrainKeys.PaymentBatch(orgId, siteId));
            var result = await grain.SearchAsync(filter);
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payments/batch/search" }
            }));
        });

        group.MapGet("/order/{orderId}", async (Guid orgId, Guid siteId, Guid orderId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPaymentBatchGrain>(GrainKeys.PaymentBatch(orgId, siteId));
            var payments = await grain.GetByOrderAsync(orderId);
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/payments/batch/order/{orderId}", payments.Cast<object>(), payments.Count));
        });

        group.MapGet("/date-range", async (
            Guid orgId,
            Guid siteId,
            DateTime startDate,
            DateTime endDate,
            State.PaymentMethod? method,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPaymentBatchGrain>(GrainKeys.PaymentBatch(orgId, siteId));
            var result = await grain.GetByDateRangeAsync(siteId, startDate, endDate, method);
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payments/batch/date-range" }
            }));
        });

        group.MapGet("/refunds", async (
            Guid orgId,
            Guid siteId,
            DateTime startDate,
            DateTime endDate,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPaymentBatchGrain>(GrainKeys.PaymentBatch(orgId, siteId));
            var refunds = await grain.GetRefundsAsync(siteId, startDate, endDate);
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/payments/batch/refunds", refunds.Cast<object>(), refunds.Count));
        });

        return app;
    }
}
