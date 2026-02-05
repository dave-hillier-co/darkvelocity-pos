using DarkVelocity.Host.Endpoints;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class CostingEndpoints
{
    public static WebApplication MapCostingEndpoints(this WebApplication app)
    {
        MapCostAlertEndpoints(app);
        MapCostingSettingsEndpoints(app);
        MapAccountingGroupEndpoints(app);
        MapMenuEngineeringEndpoints(app);
        MapProfitabilityEndpoints(app);

        return app;
    }

    // ============================================================================
    // Cost Alert Endpoints
    // ============================================================================

    private static void MapCostAlertEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/cost-alerts")
            .WithTags("Costing");

        // List all cost alerts
        group.MapGet("/", async (
            Guid orgId,
            [FromQuery] CostAlertStatus? status,
            [FromQuery] CostAlertType? alertType,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? skip,
            [FromQuery] int? take,
            IGrainFactory grainFactory) =>
        {
            var indexGrain = grainFactory.GetGrain<ICostAlertIndexGrain>(
                GrainKeys.Index(orgId, "costalerts", "all"));

            var query = new CostAlertQuery(
                status,
                alertType,
                from,
                to,
                skip ?? 0,
                take ?? 50);

            var result = await indexGrain.QueryAsync(query);

            return Results.Ok(new
            {
                _links = new
                {
                    self = new { href = $"/api/orgs/{orgId}/cost-alerts" }
                },
                totalCount = result.TotalCount,
                activeCount = result.ActiveCount,
                acknowledgedCount = result.AcknowledgedCount,
                alerts = result.Alerts.Select(a => BuildAlertResource(orgId, a))
            });
        }).WithName("ListCostAlerts")
          .WithDescription("List all cost alerts with optional filtering");

        // Get specific alert
        group.MapGet("/{alertId}", async (
            Guid orgId,
            Guid alertId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICostAlertGrain>(
                GrainKeys.CostAlert(orgId, alertId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Cost alert not found"));

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildAlertLinks(orgId, alertId)));
        }).WithName("GetCostAlert")
          .WithDescription("Get a specific cost alert by ID");

        // Create cost alert
        group.MapPost("/", async (
            Guid orgId,
            [FromBody] CreateCostAlertRequest request,
            IGrainFactory grainFactory) =>
        {
            var alertId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<ICostAlertGrain>(
                GrainKeys.CostAlert(orgId, alertId));

            var snapshot = await grain.CreateAsync(new CreateCostAlertCommand(
                request.AlertType,
                request.RecipeId,
                request.RecipeName,
                request.IngredientId,
                request.IngredientName,
                request.MenuItemId,
                request.MenuItemName,
                request.PreviousValue,
                request.CurrentValue,
                request.ThresholdValue,
                request.ImpactDescription,
                request.AffectedRecipeCount));

            // Register in index
            var indexGrain = grainFactory.GetGrain<ICostAlertIndexGrain>(
                GrainKeys.Index(orgId, "costalerts", "all"));

            await indexGrain.RegisterAsync(alertId, new CostAlertSummary(
                alertId,
                snapshot.AlertType,
                snapshot.RecipeName,
                snapshot.IngredientName,
                snapshot.MenuItemName,
                snapshot.ChangePercent,
                snapshot.IsAcknowledged,
                snapshot.ActionTaken,
                snapshot.CreatedAt,
                snapshot.AcknowledgedAt));

            return Results.Created(
                $"/api/orgs/{orgId}/cost-alerts/{alertId}",
                Hal.Resource(snapshot, BuildAlertLinks(orgId, alertId)));
        }).WithName("CreateCostAlert")
          .WithDescription("Create a new cost alert");

        // Acknowledge alert
        group.MapPost("/{alertId}/acknowledge", async (
            Guid orgId,
            Guid alertId,
            [FromBody] AcknowledgeCostAlertRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICostAlertGrain>(
                GrainKeys.CostAlert(orgId, alertId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Cost alert not found"));

            var snapshot = await grain.AcknowledgeAsync(new AcknowledgeCostAlertCommand(
                request.AcknowledgedByUserId,
                request.Notes,
                request.ActionTaken));

            // Update index
            var indexGrain = grainFactory.GetGrain<ICostAlertIndexGrain>(
                GrainKeys.Index(orgId, "costalerts", "all"));

            await indexGrain.UpdateStatusAsync(
                alertId,
                snapshot.IsAcknowledged,
                snapshot.ActionTaken,
                snapshot.AcknowledgedAt);

            return Results.Ok(Hal.Resource(snapshot, BuildAlertLinks(orgId, alertId)));
        }).WithName("AcknowledgeCostAlert")
          .WithDescription("Acknowledge a cost alert and record action taken");

        // Get active alert count
        group.MapGet("/count/active", async (
            Guid orgId,
            IGrainFactory grainFactory) =>
        {
            var indexGrain = grainFactory.GetGrain<ICostAlertIndexGrain>(
                GrainKeys.Index(orgId, "costalerts", "all"));

            var count = await indexGrain.GetActiveCountAsync();

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/cost-alerts/count/active" } },
                count
            });
        }).WithName("GetActiveCostAlertCount")
          .WithDescription("Get count of active (unacknowledged) cost alerts");
    }

    // ============================================================================
    // Costing Settings Endpoints
    // ============================================================================

    private static void MapCostingSettingsEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/costing-settings")
            .WithTags("Costing");

        // Get costing settings
        group.MapGet("/", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICostingSettingsGrain>(
                GrainKeys.CostingSettings(orgId, siteId));

            if (!await grain.ExistsAsync())
            {
                // Initialize with defaults
                await grain.InitializeAsync(siteId);
            }

            var settings = await grain.GetSettingsAsync();

            return Results.Ok(Hal.Resource(settings, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/costing-settings" }
            }));
        }).WithName("GetCostingSettings")
          .WithDescription("Get costing settings for a site");

        // Update costing settings
        group.MapPut("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] UpdateCostingSettingsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICostingSettingsGrain>(
                GrainKeys.CostingSettings(orgId, siteId));

            if (!await grain.ExistsAsync())
            {
                await grain.InitializeAsync(siteId);
            }

            var settings = await grain.UpdateAsync(new UpdateCostingSettingsCommand(
                request.TargetFoodCostPercent,
                request.TargetBeverageCostPercent,
                request.MinimumMarginPercent,
                request.WarningMarginPercent,
                request.PriceChangeAlertThreshold,
                request.CostIncreaseAlertThreshold,
                request.AutoRecalculateCosts,
                request.AutoCreateSnapshots,
                request.SnapshotFrequencyDays));

            return Results.Ok(Hal.Resource(settings, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/costing-settings" }
            }));
        }).WithName("UpdateCostingSettings")
          .WithDescription("Update costing settings for a site");
    }

    // ============================================================================
    // Accounting Group Endpoints (Placeholder for future expansion)
    // ============================================================================

    private static void MapAccountingGroupEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/accounting-groups")
            .WithTags("Costing");

        // List accounting groups
        group.MapGet("/", async (Guid orgId, IGrainFactory grainFactory) =>
        {
            // For now, return a static list of common accounting groups
            // A future AccountingGroupGrain could be implemented for customization
            var groups = new[]
            {
                new AccountingGroupResponse(Guid.NewGuid(), "Food", "Food cost of goods sold", 30m),
                new AccountingGroupResponse(Guid.NewGuid(), "Beverage", "Beverage cost of goods sold", 25m),
                new AccountingGroupResponse(Guid.NewGuid(), "Alcohol", "Alcohol cost of goods sold", 20m),
                new AccountingGroupResponse(Guid.NewGuid(), "Non-Alcoholic", "Non-alcoholic beverage costs", 15m),
                new AccountingGroupResponse(Guid.NewGuid(), "Supplies", "Disposable supplies and packaging", 5m)
            };

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/accounting-groups" } },
                groups
            });
        }).WithName("ListAccountingGroups")
          .WithDescription("List accounting groups for cost categorization");

        // Create accounting group (placeholder)
        group.MapPost("/", (Guid orgId, [FromBody] CreateAccountingGroupRequest request) =>
        {
            // TODO: Implement AccountingGroupGrain when needed
            var groupId = Guid.NewGuid();
            return Results.Created(
                $"/api/orgs/{orgId}/accounting-groups/{groupId}",
                new AccountingGroupResponse(groupId, request.Name, request.Description, request.TargetCostPercent));
        }).WithName("CreateAccountingGroup")
          .WithDescription("Create a new accounting group");

        // Update accounting group (placeholder)
        group.MapPut("/{groupId}", (
            Guid orgId,
            Guid groupId,
            [FromBody] UpdateAccountingGroupRequest request) =>
        {
            // TODO: Implement AccountingGroupGrain when needed
            return Results.Ok(new AccountingGroupResponse(
                groupId,
                request.Name ?? "Updated Group",
                request.Description,
                request.TargetCostPercent ?? 30m));
        }).WithName("UpdateAccountingGroup")
          .WithDescription("Update an accounting group");
    }

    // ============================================================================
    // Menu Engineering Endpoints
    // ============================================================================

    private static void MapMenuEngineeringEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/menu-engineering")
            .WithTags("Costing");

        // Get menu engineering analysis
        group.MapGet("/", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuEngineeringGrain>(
                GrainKeys.MenuEngineering(orgId, siteId));

            var periodStart = fromDate ?? DateTime.UtcNow.AddMonths(-1);
            var periodEnd = toDate ?? DateTime.UtcNow;

            var report = await grain.AnalyzeAsync(new AnalyzeMenuCommand(
                periodStart,
                periodEnd));

            return Results.Ok(Hal.Resource(report, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/menu-engineering" },
                ["items"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/menu-engineering/items" },
                ["categories"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/menu-engineering/categories" },
                ["suggestions"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/menu-engineering/price-suggestions" }
            }));
        }).WithName("GetMenuEngineeringAnalysis")
          .WithDescription("Get menu engineering analysis for a site");

        // Get item analysis
        group.MapGet("/items", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] string? category,
            [FromQuery] MenuClass? classification,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuEngineeringGrain>(
                GrainKeys.MenuEngineering(orgId, siteId));

            IReadOnlyList<MenuItemAnalysis> items;
            if (classification.HasValue)
            {
                items = await grain.GetItemsByClassAsync(classification.Value);
            }
            else
            {
                items = await grain.GetItemAnalysisAsync(category);
            }

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/sites/{siteId}/menu-engineering/items" } },
                items
            });
        }).WithName("GetMenuEngineeringItems")
          .WithDescription("Get menu item analysis with optional filtering");

        // Get specific item analysis
        group.MapGet("/items/{itemId}", async (
            Guid orgId,
            Guid siteId,
            Guid itemId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuEngineeringGrain>(
                GrainKeys.MenuEngineering(orgId, siteId));

            var item = await grain.GetItemAsync(itemId);
            if (item == null)
                return Results.NotFound(Hal.Error("not_found", "Item not found in menu engineering data"));

            return Results.Ok(Hal.Resource(item, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/menu-engineering/items/{itemId}" },
                ["collection"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/menu-engineering/items" }
            }));
        }).WithName("GetMenuEngineeringItem")
          .WithDescription("Get analysis for a specific menu item");

        // Get category analysis
        group.MapGet("/categories", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuEngineeringGrain>(
                GrainKeys.MenuEngineering(orgId, siteId));

            var categories = await grain.GetCategoryAnalysisAsync();

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/sites/{siteId}/menu-engineering/categories" } },
                categories
            });
        }).WithName("GetMenuEngineeringCategories")
          .WithDescription("Get category-level menu engineering analysis");

        // Get classification counts
        group.MapGet("/classifications", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuEngineeringGrain>(
                GrainKeys.MenuEngineering(orgId, siteId));

            var counts = await grain.GetClassificationCountsAsync();

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/sites/{siteId}/menu-engineering/classifications" } },
                classifications = counts.Select(kvp => new { classification = kvp.Key.ToString(), count = kvp.Value })
            });
        }).WithName("GetMenuEngineeringClassifications")
          .WithDescription("Get counts of items by menu engineering classification");

        // Get price suggestions
        group.MapGet("/price-suggestions", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] decimal? targetMargin,
            [FromQuery] decimal? maxPriceChange,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuEngineeringGrain>(
                GrainKeys.MenuEngineering(orgId, siteId));

            var suggestions = await grain.GetPriceSuggestionsAsync(
                targetMargin ?? 70m,
                maxPriceChange ?? 15m);

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/sites/{siteId}/menu-engineering/price-suggestions" } },
                targetMarginPercent = targetMargin ?? 70m,
                maxPriceChangePercent = maxPriceChange ?? 15m,
                suggestions
            });
        }).WithName("GetPriceSuggestions")
          .WithDescription("Get price optimization suggestions based on target margin");

        // Set target margin
        group.MapPut("/target-margin", async (
            Guid orgId,
            Guid siteId,
            [FromBody] SetTargetMarginRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuEngineeringGrain>(
                GrainKeys.MenuEngineering(orgId, siteId));

            await grain.SetTargetMarginAsync(request.TargetMarginPercent);

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/sites/{siteId}/menu-engineering/target-margin" } },
                targetMarginPercent = request.TargetMarginPercent
            });
        }).WithName("SetMenuEngineeringTargetMargin")
          .WithDescription("Set the target margin percentage for menu engineering analysis");

        // Record item sales (for feeding data to menu engineering)
        group.MapPost("/sales", async (
            Guid orgId,
            Guid siteId,
            [FromBody] RecordItemSalesRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuEngineeringGrain>(
                GrainKeys.MenuEngineering(orgId, siteId));

            await grain.RecordItemSalesAsync(new RecordItemSalesCommand(
                request.ProductId,
                request.ProductName,
                request.Category,
                request.SellingPrice,
                request.TheoreticalCost,
                request.UnitsSold,
                request.TotalRevenue,
                request.RecipeId,
                request.RecipeVersionId));

            return Results.Accepted();
        }).WithName("RecordItemSales")
          .WithDescription("Record item sales data for menu engineering analysis");
    }

    // ============================================================================
    // Profitability Dashboard Endpoints
    // ============================================================================

    private static void MapProfitabilityEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/profitability")
            .WithTags("Costing");

        // Get profitability dashboard
        group.MapGet("/", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IProfitabilityDashboardGrain>(
                GrainKeys.ProfitabilityDashboard(orgId, siteId));

            var range = new DateRange(
                fromDate ?? DateTime.UtcNow.AddMonths(-1),
                toDate ?? DateTime.UtcNow);

            var dashboard = await grain.GetDashboardAsync(range);

            return Results.Ok(Hal.Resource(dashboard, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/profitability" },
                ["categories"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/profitability/categories" },
                ["trends"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/profitability/trends" },
                ["variance"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/profitability/variance" }
            }));
        }).WithName("GetProfitabilityDashboard")
          .WithDescription("Get profitability dashboard with cost analysis");

        // Get category breakdown
        group.MapGet("/categories", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IProfitabilityDashboardGrain>(
                GrainKeys.ProfitabilityDashboard(orgId, siteId));

            var breakdown = await grain.GetCategoryBreakdownAsync();

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/sites/{siteId}/profitability/categories" } },
                categories = breakdown
            });
        }).WithName("GetProfitabilityCategoryBreakdown")
          .WithDescription("Get cost breakdown by category");

        // Get cost trends
        group.MapGet("/trends", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IProfitabilityDashboardGrain>(
                GrainKeys.ProfitabilityDashboard(orgId, siteId));

            var range = new DateRange(
                fromDate ?? DateTime.UtcNow.AddMonths(-3),
                toDate ?? DateTime.UtcNow);

            var trends = await grain.GetCostTrendsAsync(range);

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/sites/{siteId}/profitability/trends" } },
                periodStart = range.StartDate,
                periodEnd = range.EndDate,
                trends
            });
        }).WithName("GetCostTrends")
          .WithDescription("Get cost trends over time");

        // Get variance analysis
        group.MapGet("/variance", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] int? count,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IProfitabilityDashboardGrain>(
                GrainKeys.ProfitabilityDashboard(orgId, siteId));

            var varianceItems = await grain.GetTopVarianceItemsAsync(count ?? 10);

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/sites/{siteId}/profitability/variance" } },
                items = varianceItems
            });
        }).WithName("GetCostVariance")
          .WithDescription("Get items with highest theoretical vs actual cost variance");

        // Get item profitability
        group.MapGet("/items/{itemId}", async (
            Guid orgId,
            Guid siteId,
            Guid itemId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IProfitabilityDashboardGrain>(
                GrainKeys.ProfitabilityDashboard(orgId, siteId));

            var item = await grain.GetItemProfitabilityAsync(itemId);
            if (item == null)
                return Results.NotFound(Hal.Error("not_found", "Item not found in profitability data"));

            return Results.Ok(Hal.Resource(item, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/profitability/items/{itemId}" }
            }));
        }).WithName("GetItemProfitability")
          .WithDescription("Get profitability analysis for a specific item");

        // Get top/bottom margin items
        group.MapGet("/items/top-margin", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] int? count,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IProfitabilityDashboardGrain>(
                GrainKeys.ProfitabilityDashboard(orgId, siteId));

            var items = await grain.GetTopMarginItemsAsync(count ?? 10);

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/sites/{siteId}/profitability/items/top-margin" } },
                items
            });
        }).WithName("GetTopMarginItems")
          .WithDescription("Get items with highest contribution margin");

        group.MapGet("/items/bottom-margin", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] int? count,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IProfitabilityDashboardGrain>(
                GrainKeys.ProfitabilityDashboard(orgId, siteId));

            var items = await grain.GetBottomMarginItemsAsync(count ?? 10);

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/sites/{siteId}/profitability/items/bottom-margin" } },
                items
            });
        }).WithName("GetBottomMarginItems")
          .WithDescription("Get items with lowest contribution margin");
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

    private static Dictionary<string, object> BuildAlertLinks(Guid orgId, Guid alertId)
    {
        var basePath = $"/api/orgs/{orgId}/cost-alerts/{alertId}";
        return new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["acknowledge"] = new { href = $"{basePath}/acknowledge" },
            ["collection"] = new { href = $"/api/orgs/{orgId}/cost-alerts" }
        };
    }

    private static object BuildAlertResource(Guid orgId, CostAlertSummary alert)
    {
        return new
        {
            _links = BuildAlertLinks(orgId, alert.AlertId),
            alertId = alert.AlertId,
            alertType = alert.AlertType.ToString(),
            recipeName = alert.RecipeName,
            ingredientName = alert.IngredientName,
            menuItemName = alert.MenuItemName,
            changePercent = alert.ChangePercent,
            isAcknowledged = alert.IsAcknowledged,
            actionTaken = alert.ActionTaken.ToString(),
            createdAt = alert.CreatedAt,
            acknowledgedAt = alert.AcknowledgedAt
        };
    }
}

// ============================================================================
// Request/Response DTOs
// ============================================================================

public record CreateCostAlertRequest(
    CostAlertType AlertType,
    Guid? RecipeId,
    string? RecipeName,
    Guid? IngredientId,
    string? IngredientName,
    Guid? MenuItemId,
    string? MenuItemName,
    decimal PreviousValue,
    decimal CurrentValue,
    decimal? ThresholdValue,
    string? ImpactDescription,
    int AffectedRecipeCount);

public record AcknowledgeCostAlertRequest(
    Guid AcknowledgedByUserId,
    string? Notes,
    CostAlertAction ActionTaken);

public record UpdateCostingSettingsRequest(
    decimal? TargetFoodCostPercent,
    decimal? TargetBeverageCostPercent,
    decimal? MinimumMarginPercent,
    decimal? WarningMarginPercent,
    decimal? PriceChangeAlertThreshold,
    decimal? CostIncreaseAlertThreshold,
    bool? AutoRecalculateCosts,
    bool? AutoCreateSnapshots,
    int? SnapshotFrequencyDays);

public record CreateAccountingGroupRequest(
    string Name,
    string? Description,
    decimal TargetCostPercent);

public record UpdateAccountingGroupRequest(
    string? Name,
    string? Description,
    decimal? TargetCostPercent);

public record AccountingGroupResponse(
    Guid GroupId,
    string Name,
    string? Description,
    decimal TargetCostPercent);

public record SetTargetMarginRequest(
    decimal TargetMarginPercent);

public record RecordItemSalesRequest(
    Guid ProductId,
    string ProductName,
    string Category,
    decimal SellingPrice,
    decimal TheoreticalCost,
    int UnitsSold,
    decimal TotalRevenue,
    Guid? RecipeId = null,
    Guid? RecipeVersionId = null);
