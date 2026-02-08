using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class OrderingEndpoints
{
    public static WebApplication MapOrderingEndpoints(this WebApplication app)
    {
        MapAdminEndpoints(app);
        MapPublicEndpoints(app);
        return app;
    }

    // ============================================================================
    // Admin Endpoints - Manage ordering links (QR codes, kiosk URLs)
    // ============================================================================

    private static void MapAdminEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/ordering-links")
            .WithTags("Ordering Links");

        // Create a new ordering link
        group.MapPost("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] CreateOrderingLinkRequest request,
            IGrainFactory grainFactory) =>
        {
            var linkId = Guid.NewGuid();
            var linkType = Enum.Parse<OrderingLinkType>(request.Type, ignoreCase: true);

            var grain = grainFactory.GetGrain<IOrderingLinkGrain>(GrainKeys.OrderingLink(orgId, linkId));
            var result = await grain.CreateAsync(new CreateOrderingLinkCommand(
                OrganizationId: orgId,
                SiteId: siteId,
                Type: linkType,
                Name: request.Name,
                TableId: request.TableId,
                TableNumber: request.TableNumber));

            // Register in the site's link registry
            var registry = grainFactory.GetGrain<IOrderingLinkRegistryGrain>(
                GrainKeys.OrderingLinkRegistry(orgId, siteId));
            await registry.RegisterLinkAsync(new OrderingLinkSummary(
                LinkId: result.LinkId,
                Name: result.Name,
                Type: result.Type,
                ShortCode: result.ShortCode,
                IsActive: result.IsActive,
                TableId: result.TableId,
                TableNumber: result.TableNumber));

            var basePath = $"/api/orgs/{orgId}/sites/{siteId}/ordering-links/{linkId}";
            return Results.Created(basePath, Hal.Resource(new
            {
                id = result.LinkId,
                result.OrganizationId,
                result.SiteId,
                type = result.Type.ToString(),
                result.Name,
                result.ShortCode,
                result.IsActive,
                result.TableId,
                result.TableNumber,
                result.CreatedAt,
                orderingUrl = $"/order/{result.ShortCode}"
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = basePath },
                ["ordering-page"] = new { href = $"/order/{result.ShortCode}" },
                ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" }
            }));
        });

        // List ordering links for a site
        group.MapGet("/", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] bool includeInactive,
            IGrainFactory grainFactory) =>
        {
            var registry = grainFactory.GetGrain<IOrderingLinkRegistryGrain>(
                GrainKeys.OrderingLinkRegistry(orgId, siteId));
            var links = await registry.GetLinksAsync(includeInactive);

            var items = links.Select(l => (object)Hal.Resource(new
            {
                id = l.LinkId,
                type = l.Type.ToString(),
                l.Name,
                l.ShortCode,
                l.IsActive,
                l.TableId,
                l.TableNumber,
                orderingUrl = $"/order/{l.ShortCode}"
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/ordering-links/{l.LinkId}" },
                ["ordering-page"] = new { href = $"/order/{l.ShortCode}" }
            }));

            var selfHref = $"/api/orgs/{orgId}/sites/{siteId}/ordering-links";
            return Results.Ok(Hal.Collection(selfHref, items, links.Count));
        });

        // Get a specific ordering link
        group.MapGet("/{linkId}", async (
            Guid orgId,
            Guid siteId,
            Guid linkId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderingLinkGrain>(GrainKeys.OrderingLink(orgId, linkId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Ordering link not found"));

            var snapshot = await grain.GetSnapshotAsync();
            var basePath = $"/api/orgs/{orgId}/sites/{siteId}/ordering-links/{linkId}";
            return Results.Ok(Hal.Resource(new
            {
                id = snapshot.LinkId,
                snapshot.OrganizationId,
                snapshot.SiteId,
                type = snapshot.Type.ToString(),
                snapshot.Name,
                snapshot.ShortCode,
                snapshot.IsActive,
                snapshot.TableId,
                snapshot.TableNumber,
                snapshot.CreatedAt,
                orderingUrl = $"/order/{snapshot.ShortCode}"
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = basePath },
                ["ordering-page"] = new { href = $"/order/{snapshot.ShortCode}" },
                ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" }
            }));
        });

        // Update an ordering link
        group.MapPatch("/{linkId}", async (
            Guid orgId,
            Guid siteId,
            Guid linkId,
            [FromBody] UpdateOrderingLinkRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderingLinkGrain>(GrainKeys.OrderingLink(orgId, linkId));
            var result = await grain.UpdateAsync(new UpdateOrderingLinkCommand(
                Name: request.Name,
                TableId: request.TableId,
                TableNumber: request.TableNumber));

            // Update registry
            var registry = grainFactory.GetGrain<IOrderingLinkRegistryGrain>(
                GrainKeys.OrderingLinkRegistry(orgId, siteId));
            await registry.UpdateLinkAsync(linkId, request.Name, null);

            var basePath = $"/api/orgs/{orgId}/sites/{siteId}/ordering-links/{linkId}";
            return Results.Ok(Hal.Resource(new
            {
                id = result.LinkId,
                type = result.Type.ToString(),
                result.Name,
                result.ShortCode,
                result.IsActive,
                result.TableId,
                result.TableNumber
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = basePath }
            }));
        });

        // Deactivate an ordering link
        group.MapPost("/{linkId}/deactivate", async (
            Guid orgId,
            Guid siteId,
            Guid linkId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderingLinkGrain>(GrainKeys.OrderingLink(orgId, linkId));
            await grain.DeactivateAsync();

            var registry = grainFactory.GetGrain<IOrderingLinkRegistryGrain>(
                GrainKeys.OrderingLinkRegistry(orgId, siteId));
            await registry.UpdateLinkAsync(linkId, null, false);

            return Results.NoContent();
        });

        // Activate an ordering link
        group.MapPost("/{linkId}/activate", async (
            Guid orgId,
            Guid siteId,
            Guid linkId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrderingLinkGrain>(GrainKeys.OrderingLink(orgId, linkId));
            await grain.ActivateAsync();

            var registry = grainFactory.GetGrain<IOrderingLinkRegistryGrain>(
                GrainKeys.OrderingLinkRegistry(orgId, siteId));
            await registry.UpdateLinkAsync(linkId, null, true);

            return Results.NoContent();
        });
    }

    // ============================================================================
    // Public Endpoints - Guest ordering (no authentication required)
    // ============================================================================

    private static void MapPublicEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/public/ordering")
            .WithTags("Public Ordering")
            .AllowAnonymous();

        // Resolve an ordering link by short code and get the menu
        group.MapGet("/{linkCode}", async (
            string linkCode,
            IGrainFactory grainFactory) =>
        {
            // We need to find which site this link belongs to.
            // The link code is globally unique, but we need the registry.
            // For public access, we use a global lookup grain.
            var lookupGrain = grainFactory.GetGrain<IOrderingLinkGrain>(linkCode);

            // Actually, we need a different approach - let's resolve via the link grain directly.
            // Short codes are derived from the linkId, so we need a lookup mechanism.
            // For now, this endpoint expects the caller to know the orgId/siteId from the link URL.
            // A better approach: encode orgId+siteId into the link URL path.

            return Results.NotFound(Hal.Error("not_found", "Use /api/public/ordering/{orgId}/{siteId}/{linkCode} instead"));
        });

        // Get menu for an ordering link (public, no auth)
        group.MapGet("/{orgId}/{siteId}/{linkCode}", async (
            Guid orgId,
            Guid siteId,
            string linkCode,
            IGrainFactory grainFactory) =>
        {
            // Find the link by short code
            var registry = grainFactory.GetGrain<IOrderingLinkRegistryGrain>(
                GrainKeys.OrderingLinkRegistry(orgId, siteId));
            var linkSummary = await registry.FindByShortCodeAsync(linkCode);

            if (linkSummary == null || !linkSummary.IsActive)
                return Results.NotFound(Hal.Error("not_found", "Ordering link not found or inactive"));

            // Get the full menu using the existing menu category and item grains
            var categories = new List<object>();
            var items = new List<object>();

            // Get categories from the menu registry
            var menuRegistry = grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
            var categoryList = await menuRegistry.GetCategoriesAsync(includeArchived: false);
            var itemList = await menuRegistry.GetItemsAsync(includeArchived: false);

            foreach (var cat in categoryList.OrderBy(c => c.DisplayOrder))
            {
                categories.Add(new
                {
                    id = cat.DocumentId,
                    cat.Name,
                    cat.DisplayOrder,
                    cat.Color,
                    cat.ItemCount
                });
            }

            foreach (var item in itemList.OrderBy(i => i.Name))
            {
                items.Add(new
                {
                    id = item.DocumentId,
                    item.Name,
                    item.Price,
                    categoryId = item.CategoryId
                });
            }

            return Results.Ok(Hal.Resource(new
            {
                linkId = linkSummary.LinkId,
                siteName = siteId.ToString(),
                type = linkSummary.Type.ToString(),
                linkSummary.TableId,
                linkSummary.TableNumber,
                categories,
                items
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/public/ordering/{orgId}/{siteId}/{linkCode}" },
                ["start-session"] = new { href = $"/api/public/ordering/{orgId}/{siteId}/{linkCode}/sessions" }
            }));
        });

        // Start a guest ordering session
        group.MapPost("/{orgId}/{siteId}/{linkCode}/sessions", async (
            Guid orgId,
            Guid siteId,
            string linkCode,
            IGrainFactory grainFactory) =>
        {
            // Validate the link
            var registry = grainFactory.GetGrain<IOrderingLinkRegistryGrain>(
                GrainKeys.OrderingLinkRegistry(orgId, siteId));
            var linkSummary = await registry.FindByShortCodeAsync(linkCode);

            if (linkSummary == null || !linkSummary.IsActive)
                return Results.NotFound(Hal.Error("not_found", "Ordering link not found or inactive"));

            // Create a new guest session
            var sessionId = Guid.NewGuid();
            var sessionGrain = grainFactory.GetGrain<IGuestSessionGrain>(
                GrainKeys.GuestSession(orgId, siteId, sessionId));

            var result = await sessionGrain.StartAsync(new StartGuestSessionCommand(
                OrganizationId: orgId,
                SiteId: siteId,
                LinkId: linkSummary.LinkId,
                OrderingType: linkSummary.Type,
                TableId: linkSummary.TableId,
                TableNumber: linkSummary.TableNumber));

            var sessionPath = $"/api/public/ordering/sessions/{sessionId}";
            return Results.Created(sessionPath, Hal.Resource(new
            {
                id = result.SessionId,
                status = result.Status.ToString(),
                type = result.OrderingType.ToString(),
                result.TableNumber,
                cartItems = result.CartItems,
                result.CartTotal
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = sessionPath },
                ["cart"] = new { href = $"{sessionPath}/cart" },
                ["submit"] = new { href = $"{sessionPath}/submit" }
            }));
        });

        // Get session state (cart)
        group.MapGet("/sessions/{sessionId}", async (
            Guid sessionId,
            [FromHeader(Name = "X-Org-Id")] Guid orgId,
            [FromHeader(Name = "X-Site-Id")] Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IGuestSessionGrain>(
                GrainKeys.GuestSession(orgId, siteId, sessionId));
            var snapshot = await grain.GetSnapshotAsync();

            var sessionPath = $"/api/public/ordering/sessions/{sessionId}";
            return Results.Ok(Hal.Resource(new
            {
                id = snapshot.SessionId,
                status = snapshot.Status.ToString(),
                type = snapshot.OrderingType.ToString(),
                snapshot.TableNumber,
                cartItems = snapshot.CartItems.Select(i => new
                {
                    i.CartItemId,
                    i.MenuItemId,
                    i.Name,
                    i.Quantity,
                    i.UnitPrice,
                    i.Notes,
                    modifiers = i.Modifiers?.Select(m => new { m.ModifierId, m.Name, m.PriceAdjustment }),
                    lineTotal = (i.UnitPrice + (i.Modifiers?.Sum(m => m.PriceAdjustment) ?? 0)) * i.Quantity
                }),
                snapshot.CartTotal,
                snapshot.OrderId,
                snapshot.OrderNumber,
                snapshot.GuestName
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = sessionPath },
                ["cart"] = new { href = $"{sessionPath}/cart" },
                ["submit"] = new { href = $"{sessionPath}/submit" }
            }));
        });

        // Add item to cart
        group.MapPost("/sessions/{sessionId}/cart", async (
            Guid sessionId,
            [FromHeader(Name = "X-Org-Id")] Guid orgId,
            [FromHeader(Name = "X-Site-Id")] Guid siteId,
            [FromBody] AddToCartRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IGuestSessionGrain>(
                GrainKeys.GuestSession(orgId, siteId, sessionId));

            var modifiers = request.Modifiers?.Select(m => new GuestCartModifier(
                ModifierId: m.ModifierId,
                Name: m.Name,
                PriceAdjustment: m.PriceAdjustment
            )).ToList();

            var result = await grain.AddToCartAsync(new AddToCartCommand(
                MenuItemId: request.MenuItemId,
                Name: request.Name,
                Quantity: request.Quantity,
                UnitPrice: request.UnitPrice,
                Notes: request.Notes,
                Modifiers: modifiers));

            return Results.Ok(new
            {
                id = result.SessionId,
                status = result.Status.ToString(),
                cartItems = result.CartItems,
                result.CartTotal
            });
        });

        // Update cart item
        group.MapPatch("/sessions/{sessionId}/cart/{cartItemId}", async (
            Guid sessionId,
            Guid cartItemId,
            [FromHeader(Name = "X-Org-Id")] Guid orgId,
            [FromHeader(Name = "X-Site-Id")] Guid siteId,
            [FromBody] UpdateCartItemRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IGuestSessionGrain>(
                GrainKeys.GuestSession(orgId, siteId, sessionId));

            var result = await grain.UpdateCartItemAsync(new UpdateCartItemCommand(
                CartItemId: cartItemId,
                Quantity: request.Quantity,
                Notes: request.Notes));

            return Results.Ok(new
            {
                id = result.SessionId,
                cartItems = result.CartItems,
                result.CartTotal
            });
        });

        // Remove item from cart
        group.MapDelete("/sessions/{sessionId}/cart/{cartItemId}", async (
            Guid sessionId,
            Guid cartItemId,
            [FromHeader(Name = "X-Org-Id")] Guid orgId,
            [FromHeader(Name = "X-Site-Id")] Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IGuestSessionGrain>(
                GrainKeys.GuestSession(orgId, siteId, sessionId));

            var result = await grain.RemoveFromCartAsync(cartItemId);

            return Results.Ok(new
            {
                id = result.SessionId,
                cartItems = result.CartItems,
                result.CartTotal
            });
        });

        // Submit order
        group.MapPost("/sessions/{sessionId}/submit", async (
            Guid sessionId,
            [FromHeader(Name = "X-Org-Id")] Guid orgId,
            [FromHeader(Name = "X-Site-Id")] Guid siteId,
            [FromBody] SubmitOrderRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IGuestSessionGrain>(
                GrainKeys.GuestSession(orgId, siteId, sessionId));

            var result = await grain.SubmitOrderAsync(new SubmitGuestOrderCommand(
                GuestName: request.GuestName,
                GuestPhone: request.GuestPhone));

            return Results.Ok(Hal.Resource(new
            {
                orderId = result.OrderId,
                result.OrderNumber,
                result.SubmittedAt,
                status = "Submitted"
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/public/ordering/sessions/{sessionId}" },
                ["order-status"] = new { href = $"/api/public/ordering/sessions/{sessionId}/status" }
            }));
        });

        // Get order status
        group.MapGet("/sessions/{sessionId}/status", async (
            Guid sessionId,
            [FromHeader(Name = "X-Org-Id")] Guid orgId,
            [FromHeader(Name = "X-Site-Id")] Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var sessionGrain = grainFactory.GetGrain<IGuestSessionGrain>(
                GrainKeys.GuestSession(orgId, siteId, sessionId));
            var snapshot = await sessionGrain.GetSnapshotAsync();

            string? orderStatus = null;
            if (snapshot.OrderId.HasValue)
            {
                var orderGrain = grainFactory.GetGrain<IOrderGrain>(
                    GrainKeys.Order(orgId, siteId, snapshot.OrderId.Value));
                var status = await orderGrain.GetStatusAsync();
                orderStatus = status.ToString();
            }

            return Results.Ok(new
            {
                sessionId = snapshot.SessionId,
                sessionStatus = snapshot.Status.ToString(),
                snapshot.OrderId,
                snapshot.OrderNumber,
                orderStatus,
                snapshot.CartTotal,
                snapshot.GuestName,
                snapshot.TableNumber
            });
        });
    }
}
