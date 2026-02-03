using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class OrderEndpoints
{
    public static WebApplication MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/orders").WithTags("Orders");

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
        });

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
        });

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
        });

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
        });

        group.MapDelete("/{orderId}/lines/{lineId}", async (
            Guid orgId, Guid siteId, Guid orderId, Guid lineId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            await grain.RemoveLineAsync(lineId);
            return Results.NoContent();
        });

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
        });

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
        });

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
        });

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
        });

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
        });

        return app;
    }
}
