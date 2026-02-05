using DarkVelocity.Host.Authorization;
using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
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
            var links = BuildOrderLinks(orgId, siteId, orderId, state);
            return Results.Ok(Hal.Resource(state, links));
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

            // Convert BundleComponentRequest to OrderLineBundleComponent
            var bundleComponents = request.BundleComponents?.Select(c => new OrderLineBundleComponent
            {
                SlotId = c.SlotId,
                SlotName = c.SlotName,
                ItemDocumentId = c.ItemDocumentId,
                ItemName = c.ItemName,
                Quantity = c.Quantity,
                PriceAdjustment = c.PriceAdjustment,
                Modifiers = c.Modifiers ?? []
            }).ToList();

            var result = await grain.AddLineAsync(new AddLineCommand(
                request.MenuItemId, request.Name, request.Quantity, request.UnitPrice, request.Notes, request.Modifiers, request.TaxRate,
                request.IsBundle, bundleComponents));

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
            var state = await grain.GetStateAsync();
            var links = BuildOrderLinks(orgId, siteId, orderId, state);
            return Results.Ok(Hal.Resource(new { status = state.Status }, links));
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
            var state = await grain.GetStateAsync();
            var links = BuildOrderLinks(orgId, siteId, orderId, state);
            return Results.Ok(Hal.Resource(new { status = state.Status }, links));
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

        // Hold/Fire Workflow Endpoints

        // Hold specific items
        group.MapPost("/{orderId}/hold", async (
            Guid orgId, Guid siteId, Guid orderId,
            [FromBody] HoldItemsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            await grain.HoldItemsAsync(new HoldItemsCommand(request.LineIds, request.HeldBy, request.Reason));
            var summary = await grain.GetHoldSummaryAsync();

            return Results.Ok(Hal.Resource(new HoldSummaryResponse(
                summary.TotalHeldCount,
                summary.HeldByCourseCounts,
                summary.HeldLineIds), new Dictionary<string, object>
            {
                ["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
            }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.Operate, isSiteScoped: true));

        // Release items from hold
        group.MapPost("/{orderId}/release", async (
            Guid orgId, Guid siteId, Guid orderId,
            [FromBody] ReleaseItemsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            await grain.ReleaseItemsAsync(new ReleaseItemsCommand(request.LineIds, request.ReleasedBy));
            var summary = await grain.GetHoldSummaryAsync();

            return Results.Ok(Hal.Resource(new HoldSummaryResponse(
                summary.TotalHeldCount,
                summary.HeldByCourseCounts,
                summary.HeldLineIds), new Dictionary<string, object>
            {
                ["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
            }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.Operate, isSiteScoped: true));

        // Set course number for items
        group.MapPost("/{orderId}/set-course", async (
            Guid orgId, Guid siteId, Guid orderId,
            [FromBody] SetItemCourseRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            await grain.SetItemCourseAsync(new SetItemCourseCommand(request.LineIds, request.CourseNumber, request.SetBy));
            var courses = await grain.GetCourseSummaryAsync();

            return Results.Ok(Hal.Resource(new CourseSummaryResponse(courses), new Dictionary<string, object>
            {
                ["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
            }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.Operate, isSiteScoped: true));

        // Fire specific items to kitchen
        group.MapPost("/{orderId}/fire", async (
            Guid orgId, Guid siteId, Guid orderId,
            [FromBody] FireItemsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            var result = await grain.FireItemsAsync(new FireItemsCommand(request.LineIds, request.FiredBy));

            return Results.Ok(Hal.Resource(new FireResultResponse(
                result.FiredCount,
                result.FiredLineIds,
                result.FiredAt), new Dictionary<string, object>
            {
                ["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
            }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.Operate, isSiteScoped: true));

        // Fire all items in a specific course
        group.MapPost("/{orderId}/fire-course", async (
            Guid orgId, Guid siteId, Guid orderId,
            [FromBody] FireCourseRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            var result = await grain.FireCourseAsync(new FireCourseCommand(request.CourseNumber, request.FiredBy));

            return Results.Ok(Hal.Resource(new FireResultResponse(
                result.FiredCount,
                result.FiredLineIds,
                result.FiredAt), new Dictionary<string, object>
            {
                ["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
            }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.Operate, isSiteScoped: true));

        // Fire all pending items at once
        group.MapPost("/{orderId}/fire-all", async (
            Guid orgId, Guid siteId, Guid orderId,
            [FromBody] FireAllRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            var result = await grain.FireAllAsync(request.FiredBy);

            return Results.Ok(Hal.Resource(new FireResultResponse(
                result.FiredCount,
                result.FiredLineIds,
                result.FiredAt), new Dictionary<string, object>
            {
                ["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
            }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.Operate, isSiteScoped: true));

        // Get hold summary
        group.MapGet("/{orderId}/hold-summary", async (
            Guid orgId, Guid siteId, Guid orderId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            var summary = await grain.GetHoldSummaryAsync();

            return Results.Ok(Hal.Resource(new HoldSummaryResponse(
                summary.TotalHeldCount,
                summary.HeldByCourseCounts,
                summary.HeldLineIds), new Dictionary<string, object>
            {
                ["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
            }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.View, isSiteScoped: true));

        // Get held items
        group.MapGet("/{orderId}/held-items", async (
            Guid orgId, Guid siteId, Guid orderId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            var heldItems = await grain.GetHeldItemsAsync();
            var items = heldItems.Select(l => Hal.Resource(l, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines/{l.Id}" }
            })).ToList();

            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/held-items", items, items.Count));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.View, isSiteScoped: true));

        // Get course summary
        group.MapGet("/{orderId}/courses", async (
            Guid orgId, Guid siteId, Guid orderId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Order not found"));

            var courses = await grain.GetCourseSummaryAsync();

            return Results.Ok(Hal.Resource(new CourseSummaryResponse(courses), new Dictionary<string, object>
            {
                ["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
            }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Site, Permissions.View, isSiteScoped: true));

        return app;
    }

    /// <summary>
    /// Builds HATEOAS links for an order resource, including conditional cross-domain links
    /// and action links based on order state.
    /// </summary>
    private static Dictionary<string, object> BuildOrderLinks(Guid orgId, Guid siteId, Guid orderId, OrderState state)
    {
        var links = new Dictionary<string, object>
        {
            // Core resource links
            ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" },
            ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
            ["lines"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines" },
            ["payments"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/payments" },
            ["totals"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/totals" },
            ["hold-summary"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/hold-summary" },
            ["courses"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/courses" }
        };

        // Conditional cross-domain links based on associated resources
        if (state.CustomerId.HasValue)
        {
            links["customer"] = new { href = $"/api/orgs/{orgId}/customers/{state.CustomerId}" };
        }

        if (state.TableId.HasValue)
        {
            links["table"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables/{state.TableId}" };
        }

        if (state.BookingId.HasValue)
        {
            links["booking"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{state.BookingId}" };
        }

        // Kitchen tickets link - available when order has been sent
        if (state.Status != OrderStatus.Open)
        {
            links["kitchen-tickets"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/kitchen-tickets" };
        }

        // Action links based on order state
        switch (state.Status)
        {
            case OrderStatus.Open:
                links["send"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/send" };
                links["void"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/void" };
                links["cancel"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/cancel" };
                links["split:by-items"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/split/by-items" };
                links["split:by-people"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/split/by-people{{?count}}", templated = true };
                break;

            case OrderStatus.Sent:
            case OrderStatus.PartiallyPaid:
                links["close"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/close" };
                links["void"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/void" };
                links["split:by-items"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/split/by-items" };
                links["split:by-people"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/split/by-people{{?count}}", templated = true };
                break;

            case OrderStatus.Paid:
                links["close"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/close" };
                break;

            // Closed and Voided orders have no action links
        }

        return links;
    }
}
