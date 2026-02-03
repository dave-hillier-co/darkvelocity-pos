using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Search;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class SearchEndpoints
{
    public static WebApplication MapSearchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/search").WithTags("Search");

        group.MapGet("/orders", async (
            Guid orgId,
            [FromServices] ISearchService searchService,
            [FromQuery] string? q = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] string? status = null,
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null,
            [FromQuery] decimal? minTotal = null,
            [FromQuery] decimal? maxTotal = null,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 20,
            [FromQuery] string sortBy = "CreatedAt",
            [FromQuery] bool descending = true) =>
        {
            var query = new OrderSearchQuery
            {
                Text = q,
                SiteId = siteId,
                Status = status,
                FromDate = fromDate,
                ToDate = toDate,
                MinTotal = minTotal,
                MaxTotal = maxTotal,
                Skip = skip,
                Take = Math.Min(take, 100),
                SortBy = sortBy,
                Descending = descending
            };

            var result = await searchService.SearchOrdersAsync(orgId, query);

            return Results.Ok(Hal.Collection(
                result.Items,
                new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/search/orders" }
                },
                new Dictionary<string, object>
                {
                    ["totalCount"] = result.TotalCount,
                    ["skip"] = result.Skip,
                    ["take"] = result.Take,
                    ["hasMore"] = result.HasMore
                }));
        });

        group.MapGet("/customers", async (
            Guid orgId,
            [FromServices] ISearchService searchService,
            [FromQuery] string? q = null,
            [FromQuery] string? status = null,
            [FromQuery] string? loyaltyTier = null,
            [FromQuery] string? segment = null,
            [FromQuery] decimal? minSpend = null,
            [FromQuery] decimal? maxSpend = null,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 20,
            [FromQuery] string sortBy = "DisplayName",
            [FromQuery] bool descending = false) =>
        {
            var query = new CustomerSearchQuery
            {
                Text = q,
                Status = status,
                LoyaltyTier = loyaltyTier,
                Segment = segment,
                MinSpend = minSpend,
                MaxSpend = maxSpend,
                Skip = skip,
                Take = Math.Min(take, 100),
                SortBy = sortBy,
                Descending = descending
            };

            var result = await searchService.SearchCustomersAsync(orgId, query);

            return Results.Ok(Hal.Collection(
                result.Items,
                new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/search/customers" }
                },
                new Dictionary<string, object>
                {
                    ["totalCount"] = result.TotalCount,
                    ["skip"] = result.Skip,
                    ["take"] = result.Take,
                    ["hasMore"] = result.HasMore
                }));
        });

        group.MapGet("/payments", async (
            Guid orgId,
            [FromServices] ISearchService searchService,
            [FromQuery] string? q = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? orderId = null,
            [FromQuery] string? method = null,
            [FromQuery] string? status = null,
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null,
            [FromQuery] decimal? minAmount = null,
            [FromQuery] decimal? maxAmount = null,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 20,
            [FromQuery] string sortBy = "CreatedAt",
            [FromQuery] bool descending = true) =>
        {
            var query = new PaymentSearchQuery
            {
                Text = q,
                SiteId = siteId,
                OrderId = orderId,
                Method = method,
                Status = status,
                FromDate = fromDate,
                ToDate = toDate,
                MinAmount = minAmount,
                MaxAmount = maxAmount,
                Skip = skip,
                Take = Math.Min(take, 100),
                SortBy = sortBy,
                Descending = descending
            };

            var result = await searchService.SearchPaymentsAsync(orgId, query);

            return Results.Ok(Hal.Collection(
                result.Items,
                new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/search/payments" }
                },
                new Dictionary<string, object>
                {
                    ["totalCount"] = result.TotalCount,
                    ["skip"] = result.Skip,
                    ["take"] = result.Take,
                    ["hasMore"] = result.HasMore
                }));
        });

        group.MapGet("/projection/status", async (
            Guid orgId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISearchProjectionGrain>($"org:{orgId}");
            var status = await grain.GetStatusAsync();
            return Results.Ok(status);
        });

        group.MapPost("/projection/activate", async (
            Guid orgId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISearchProjectionGrain>($"org:{orgId}");
            await grain.ActivateProjectionAsync();
            var status = await grain.GetStatusAsync();
            return Results.Ok(status);
        });

        return app;
    }
}
