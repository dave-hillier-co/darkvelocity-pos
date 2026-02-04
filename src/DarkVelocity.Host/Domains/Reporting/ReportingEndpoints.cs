using DarkVelocity.Host.Authorization;
using DarkVelocity.Host.Costing;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class ReportingEndpoints
{
    public static WebApplication MapReportingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/reports")
            .WithTags("Reporting")
            .RequireSpiceDbAuthorization();

        // ============================================================================
        // Dashboard
        // ============================================================================

        group.MapGet("/dashboard", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISiteDashboardGrain>(GrainKeys.SiteDashboard(orgId, siteId));
            var metrics = await grain.GetMetricsAsync();

            return Results.Ok(Hal.Resource(metrics, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/dashboard" },
                ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
                ["todaySales"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/sales/today" },
                ["currentInventory"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/inventory/today" }
            }));
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        group.MapPost("/dashboard/refresh", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISiteDashboardGrain>(GrainKeys.SiteDashboard(orgId, siteId));
            await grain.RefreshAsync();

            return Results.Ok(new { message = "Dashboard refreshed" });
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        // ============================================================================
        // Daily Sales
        // ============================================================================

        group.MapGet("/sales/today", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var grain = grainFactory.GetGrain<IDailySalesGrain>(GrainKeys.DailySales(orgId, siteId, today));

            try
            {
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/sales/today" },
                    ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
                    ["metrics"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/sales/{today:yyyy-MM-dd}/metrics" },
                    ["facts"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/sales/{today:yyyy-MM-dd}/facts" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "No sales data for today"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        group.MapGet("/sales/{date}", async (
            Guid orgId,
            Guid siteId,
            DateOnly date,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDailySalesGrain>(GrainKeys.DailySales(orgId, siteId, date));

            try
            {
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/sales/{date:yyyy-MM-dd}" },
                    ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
                    ["metrics"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/sales/{date:yyyy-MM-dd}/metrics" },
                    ["facts"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/sales/{date:yyyy-MM-dd}/facts" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No sales data for {date:yyyy-MM-dd}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        group.MapGet("/sales/{date}/metrics", async (
            Guid orgId,
            Guid siteId,
            DateOnly date,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDailySalesGrain>(GrainKeys.DailySales(orgId, siteId, date));

            try
            {
                var metrics = await grain.GetMetricsAsync();
                return Results.Ok(Hal.Resource(metrics, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/sales/{date:yyyy-MM-dd}/metrics" },
                    ["snapshot"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/sales/{date:yyyy-MM-dd}" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No sales data for {date:yyyy-MM-dd}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        group.MapGet("/sales/{date}/gross-profit", async (
            Guid orgId,
            Guid siteId,
            DateOnly date,
            [FromQuery] CostingMethod? method,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDailySalesGrain>(GrainKeys.DailySales(orgId, siteId, date));
            var costingMethod = method ?? CostingMethod.WAC;

            try
            {
                var metrics = await grain.GetGrossProfitMetricsAsync(costingMethod);
                return Results.Ok(Hal.Resource(metrics, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/sales/{date:yyyy-MM-dd}/gross-profit?method={costingMethod}" },
                    ["snapshot"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/sales/{date:yyyy-MM-dd}" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No sales data for {date:yyyy-MM-dd}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        group.MapGet("/sales/{date}/facts", async (
            Guid orgId,
            Guid siteId,
            DateOnly date,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDailySalesGrain>(GrainKeys.DailySales(orgId, siteId, date));

            try
            {
                var facts = await grain.GetFactsAsync();
                var items = facts.Select(f => Hal.Resource(f, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/sales/{date:yyyy-MM-dd}/facts/{f.FactId}" }
                })).ToList();

                return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/reports/sales/{date:yyyy-MM-dd}/facts", items, items.Count));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No sales data for {date:yyyy-MM-dd}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        // ============================================================================
        // Daily Inventory Snapshots
        // ============================================================================

        group.MapGet("/inventory/today", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var grain = grainFactory.GetGrain<IDailyInventorySnapshotGrain>(GrainKeys.DailyInventorySnapshot(orgId, siteId, today));

            try
            {
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/inventory/today" },
                    ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
                    ["health"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/inventory/{today:yyyy-MM-dd}/health" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "No inventory snapshot for today"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        group.MapGet("/inventory/{date}", async (
            Guid orgId,
            Guid siteId,
            DateOnly date,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDailyInventorySnapshotGrain>(GrainKeys.DailyInventorySnapshot(orgId, siteId, date));

            try
            {
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/inventory/{date:yyyy-MM-dd}" },
                    ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
                    ["health"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/inventory/{date:yyyy-MM-dd}/health" },
                    ["facts"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/inventory/{date:yyyy-MM-dd}/facts" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No inventory snapshot for {date:yyyy-MM-dd}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        group.MapGet("/inventory/{date}/health", async (
            Guid orgId,
            Guid siteId,
            DateOnly date,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDailyInventorySnapshotGrain>(GrainKeys.DailyInventorySnapshot(orgId, siteId, date));

            try
            {
                var metrics = await grain.GetHealthMetricsAsync();
                return Results.Ok(Hal.Resource(metrics, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/inventory/{date:yyyy-MM-dd}/health" },
                    ["snapshot"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/inventory/{date:yyyy-MM-dd}" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No inventory snapshot for {date:yyyy-MM-dd}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        group.MapGet("/inventory/{date}/facts", async (
            Guid orgId,
            Guid siteId,
            DateOnly date,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDailyInventorySnapshotGrain>(GrainKeys.DailyInventorySnapshot(orgId, siteId, date));

            try
            {
                var facts = await grain.GetFactsAsync();
                var items = facts.Select(f => Hal.Resource(f, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/inventory/{date:yyyy-MM-dd}/facts/{f.FactId}" }
                })).ToList();

                return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/reports/inventory/{date:yyyy-MM-dd}/facts", items, items.Count));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No inventory snapshot for {date:yyyy-MM-dd}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        // ============================================================================
        // Daily Consumption
        // ============================================================================

        group.MapGet("/consumption/today", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var grain = grainFactory.GetGrain<IDailyConsumptionGrain>(GrainKeys.DailyConsumption(orgId, siteId, today));

            try
            {
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/consumption/today" },
                    ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
                    ["variances"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/consumption/{today:yyyy-MM-dd}/variances" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "No consumption data for today"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        group.MapGet("/consumption/{date}", async (
            Guid orgId,
            Guid siteId,
            DateOnly date,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDailyConsumptionGrain>(GrainKeys.DailyConsumption(orgId, siteId, date));

            try
            {
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/consumption/{date:yyyy-MM-dd}" },
                    ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
                    ["variances"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/consumption/{date:yyyy-MM-dd}/variances" },
                    ["facts"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/consumption/{date:yyyy-MM-dd}/facts" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No consumption data for {date:yyyy-MM-dd}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        group.MapGet("/consumption/{date}/variances", async (
            Guid orgId,
            Guid siteId,
            DateOnly date,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDailyConsumptionGrain>(GrainKeys.DailyConsumption(orgId, siteId, date));

            try
            {
                var variances = await grain.GetVarianceBreakdownAsync();
                var items = variances.Select(v => Hal.Resource(v, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/consumption/{date:yyyy-MM-dd}/variances/{v.IngredientId}" }
                })).ToList();

                return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/reports/consumption/{date:yyyy-MM-dd}/variances", items, items.Count));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No consumption data for {date:yyyy-MM-dd}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        group.MapGet("/consumption/{date}/facts", async (
            Guid orgId,
            Guid siteId,
            DateOnly date,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDailyConsumptionGrain>(GrainKeys.DailyConsumption(orgId, siteId, date));

            try
            {
                var facts = await grain.GetFactsAsync();
                var items = facts.Select(f => Hal.Resource(f, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/consumption/{date:yyyy-MM-dd}/facts/{f.FactId}" }
                })).ToList();

                return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/reports/consumption/{date:yyyy-MM-dd}/facts", items, items.Count));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No consumption data for {date:yyyy-MM-dd}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        // ============================================================================
        // Daily Waste
        // ============================================================================

        group.MapGet("/waste/today", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var grain = grainFactory.GetGrain<IDailyWasteGrain>(GrainKeys.DailyWaste(orgId, siteId, today));

            try
            {
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/waste/today" },
                    ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
                    ["facts"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/waste/{today:yyyy-MM-dd}/facts" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "No waste data for today"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        group.MapGet("/waste/{date}", async (
            Guid orgId,
            Guid siteId,
            DateOnly date,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDailyWasteGrain>(GrainKeys.DailyWaste(orgId, siteId, date));

            try
            {
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/waste/{date:yyyy-MM-dd}" },
                    ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
                    ["facts"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/waste/{date:yyyy-MM-dd}/facts" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No waste data for {date:yyyy-MM-dd}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        group.MapGet("/waste/{date}/facts", async (
            Guid orgId,
            Guid siteId,
            DateOnly date,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDailyWasteGrain>(GrainKeys.DailyWaste(orgId, siteId, date));

            try
            {
                var facts = await grain.GetFactsAsync();
                var items = facts.Select(f => Hal.Resource(f, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/waste/{date:yyyy-MM-dd}/facts/{f.FactId}" }
                })).ToList();

                return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/reports/waste/{date:yyyy-MM-dd}/facts", items, items.Count));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No waste data for {date:yyyy-MM-dd}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        // ============================================================================
        // Period Aggregations
        // ============================================================================

        group.MapGet("/periods/{periodType}/{year}/{periodNumber}", async (
            Guid orgId,
            Guid siteId,
            PeriodType periodType,
            int year,
            int periodNumber,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPeriodAggregationGrain>(
                GrainKeys.PeriodAggregation(orgId, siteId, periodType, year, periodNumber));

            try
            {
                var summary = await grain.GetSummaryAsync();
                return Results.Ok(Hal.Resource(summary, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/periods/{periodType}/{year}/{periodNumber}" },
                    ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
                    ["salesMetrics"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/periods/{periodType}/{year}/{periodNumber}/sales" },
                    ["grossProfit"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/periods/{periodType}/{year}/{periodNumber}/gross-profit" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No data for {periodType} period {periodNumber} of {year}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        group.MapGet("/periods/{periodType}/{year}/{periodNumber}/sales", async (
            Guid orgId,
            Guid siteId,
            PeriodType periodType,
            int year,
            int periodNumber,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPeriodAggregationGrain>(
                GrainKeys.PeriodAggregation(orgId, siteId, periodType, year, periodNumber));

            try
            {
                var metrics = await grain.GetSalesMetricsAsync();
                return Results.Ok(Hal.Resource(metrics, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/periods/{periodType}/{year}/{periodNumber}/sales" },
                    ["summary"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/periods/{periodType}/{year}/{periodNumber}" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No data for {periodType} period {periodNumber} of {year}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        group.MapGet("/periods/{periodType}/{year}/{periodNumber}/gross-profit", async (
            Guid orgId,
            Guid siteId,
            PeriodType periodType,
            int year,
            int periodNumber,
            [FromQuery] CostingMethod? method,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPeriodAggregationGrain>(
                GrainKeys.PeriodAggregation(orgId, siteId, periodType, year, periodNumber));
            var costingMethod = method ?? CostingMethod.WAC;

            try
            {
                var metrics = await grain.GetGrossProfitMetricsAsync(costingMethod);
                return Results.Ok(Hal.Resource(metrics, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/periods/{periodType}/{year}/{periodNumber}/gross-profit?method={costingMethod}" },
                    ["summary"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/periods/{periodType}/{year}/{periodNumber}" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No data for {periodType} period {periodNumber} of {year}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        // ============================================================================
        // Convenience Period Endpoints
        // ============================================================================

        group.MapGet("/weekly/{year}/{weekNumber}", async (
            Guid orgId,
            Guid siteId,
            int year,
            int weekNumber,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPeriodAggregationGrain>(
                GrainKeys.PeriodAggregation(orgId, siteId, PeriodType.Weekly, year, weekNumber));

            try
            {
                var summary = await grain.GetSummaryAsync();
                return Results.Ok(Hal.Resource(summary, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/weekly/{year}/{weekNumber}" },
                    ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No data for week {weekNumber} of {year}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        group.MapGet("/monthly/{year}/{month}", async (
            Guid orgId,
            Guid siteId,
            int year,
            int month,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPeriodAggregationGrain>(
                GrainKeys.PeriodAggregation(orgId, siteId, PeriodType.Monthly, year, month));

            try
            {
                var summary = await grain.GetSummaryAsync();
                return Results.Ok(Hal.Resource(summary, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reports/monthly/{year}/{month}" },
                    ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", $"No data for month {month} of {year}"));
            }
        }).WithMetadata(new RequirePermissionAttribute(ResourceTypes.Reporting, Permissions.View, isSiteScoped: true));

        return app;
    }
}
