using DarkVelocity.Host.Authorization;
using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class OrderEndpoints
{
    public static WebApplication MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/orders")
            .WithTags("Orders")
            .RequireSpiceDbAuthorization();

        group.MapPost("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] CreateOrderRequest request,
            IGrainFactory grainFactory) =>
        {
            var orderId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            var result = await grain.CreateAsync(new CreateOrderCommand(
                orgId, siteId, request.CreatedBy, request.Type, request.TableId, request.TableNumber, request.CustomerId, request.GuestCount));

            return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}", Hal.Resource(new
            {
                id = result.Id,
                orderNumber = result.OrderNumber,
                createdAt = result.CreatedAt
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" },
                ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
                ["lines"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines" }
            }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.Operate, isSiteScoped: true));

        group.MapGet("/{orderId}", async (Guid orgId, Guid siteId, Guid orderId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" },
                ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
                ["lines"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines" },
                ["send"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/send" },
                ["close"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/close" }
            }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.View, isSiteScoped: true));

        group.MapPost("/{orderId}/lines", async (
            Guid orgId,
            Guid siteId,
            Guid orderId,
            [FromBody] AddLineRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            var result = await grain.AddLineAsync(new AddLineCommand(
                request.MenuItemId, request.Name, request.Quantity, request.UnitPrice, request.Notes, request.Modifiers));

            return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines/{result.LineId}",
                Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines/{result.LineId}" },
                    ["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
                }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.Operate, isSiteScoped: true));

        group.MapGet("/{orderId}/lines", async (Guid orgId, Guid siteId, Guid orderId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            var lines = await grain.GetLinesAsync();
            var items = lines.Select(l => Hal.Resource(l, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines/{l.Id}" }
            })).ToList();

            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines", items, items.Count));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.View, isSiteScoped: true));

        group.MapDelete("/{orderId}/lines/{lineId}", async (
            Guid orgId, Guid siteId, Guid orderId, Guid lineId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            await grain.RemoveLineAsync(lineId);
            return Results.NoContent();
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.Operate, isSiteScoped: true));

        group.MapPost("/{orderId}/send", async (
            Guid orgId, Guid siteId, Guid orderId,
            [FromBody] SendOrderRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            await grain.SendAsync(request.SentBy);
            var state = await grain.GetStateAsync();

            return Results.Ok(Hal.Resource(new { status = state.Status, sentAt = state.SentAt }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
            }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.Operate, isSiteScoped: true));

        group.MapPost("/{orderId}/close", async (
            Guid orgId, Guid siteId, Guid orderId,
            [FromBody] CloseOrderRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            await grain.CloseAsync(request.ClosedBy);
            return Results.Ok(new { message = "Order closed" });
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.Operate, isSiteScoped: true));

        // Void requires manager-level permission
        group.MapPost("/{orderId}/void", async (
            Guid orgId, Guid siteId, Guid orderId,
            [FromBody] VoidOrderRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            await grain.VoidAsync(new VoidOrderCommand(request.VoidedBy, request.Reason));
            return Results.Ok(new { message = "Order voided" });
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.Manage, isSiteScoped: true));

        // Discounts require supervisor-level permission
        group.MapPost("/{orderId}/discounts", async (
            Guid orgId, Guid siteId, Guid orderId,
            [FromBody] ApplyDiscountRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            await grain.ApplyDiscountAsync(new ApplyDiscountCommand(
                request.Name, request.Type, request.Value, request.AppliedBy, request.DiscountId, request.Reason, request.ApprovedBy));
            var totals = await grain.GetTotalsAsync();

            return Results.Ok(Hal.Resource(totals, new Dictionary<string, object>
            {
                ["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
            }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.Supervise, isSiteScoped: true));

        group.MapGet("/{orderId}/totals", async (Guid orgId, Guid siteId, Guid orderId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            var totals = await grain.GetTotalsAsync();
            return Results.Ok(Hal.Resource(totals, new Dictionary<string, object>
            {
                ["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
            }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.View, isSiteScoped: true));

        // Bill Splitting Endpoints

        // Split order by moving specific items to a new order
        group.MapPost("/{orderId}/split/by-items", async (
            Guid orgId, Guid siteId, Guid orderId,
            [FromBody] SplitByItemsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            var result = await grain.SplitByItemsAsync(new SplitByItemsCommand(
                request.LineIds, request.SplitBy, request.GuestCount));

            var response = new SplitByItemsResponse(
                result.NewOrderId,
                result.NewOrderNumber,
                result.LinesMoved,
                result.NewOrderTotal,
                result.RemainingOrderTotal);

            return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/orders/{result.NewOrderId}",
                Hal.Resource(response, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" },
                    ["newOrder"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{result.NewOrderId}" }
                }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.Operate, isSiteScoped: true));

        // Calculate split payment by number of people (equal split)
        group.MapGet("/{orderId}/split/by-people", async (
            Guid orgId, Guid siteId, Guid orderId,
            [FromQuery] int count,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            var result = await grain.CalculateSplitByPeopleAsync(count);

            var response = new SplitPaymentResponse(
                result.TotalAmount,
                result.BalanceDue,
                result.Shares.Select(s => new SplitShareResponse(
                    s.ShareNumber, s.Amount, s.Tax, s.Total, s.Label)).ToList(),
                result.IsValid);

            return Results.Ok(Hal.Resource(response, new Dictionary<string, object>
            {
                ["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
            }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.View, isSiteScoped: true));

        // Calculate split payment by custom amounts
        group.MapPost("/{orderId}/split/by-amounts", async (
            Guid orgId, Guid siteId, Guid orderId,
            [FromBody] SplitByAmountsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            var result = await grain.CalculateSplitByAmountsAsync(request.Amounts);

            var response = new SplitPaymentResponse(
                result.TotalAmount,
                result.BalanceDue,
                result.Shares.Select(s => new SplitShareResponse(
                    s.ShareNumber, s.Amount, s.Tax, s.Total, s.Label)).ToList(),
                result.IsValid);

            return Results.Ok(Hal.Resource(response, new Dictionary<string, object>
            {
                ["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
            }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.View, isSiteScoped: true));

        return app;
    }
}
