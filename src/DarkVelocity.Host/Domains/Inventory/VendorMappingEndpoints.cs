using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class VendorMappingEndpoints
{
    public static WebApplication MapVendorMappingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/vendors/{vendorId}/mappings")
            .WithTags("VendorMappings");

        // Initialize a vendor mapping record
        group.MapPost("/", async (
            Guid orgId,
            string vendorId,
            [FromBody] InitializeVendorMappingRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IVendorItemMappingGrain>(
                GrainKeys.VendorItemMapping(orgId, vendorId));

            if (await grain.ExistsAsync())
                return Results.Conflict(Hal.Error("already_exists", "Vendor mapping already initialized"));

            var snapshot = await grain.InitializeAsync(new InitializeVendorMappingCommand(
                orgId,
                vendorId,
                request.VendorName ?? vendorId,
                request.VendorType ?? VendorType.Unknown));

            return Results.Created(
                $"/api/orgs/{orgId}/vendors/{vendorId}/mappings",
                Hal.Resource(snapshot, BuildVendorLinks(orgId, vendorId)));
        });

        // Get vendor mapping summary
        group.MapGet("/", async (
            Guid orgId,
            string vendorId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IVendorItemMappingGrain>(
                GrainKeys.VendorItemMapping(orgId, vendorId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Vendor mapping not found"));

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildVendorLinks(orgId, vendorId)));
        });

        // List all mappings for a vendor
        group.MapGet("/items", async (
            Guid orgId,
            string vendorId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IVendorItemMappingGrain>(
                GrainKeys.VendorItemMapping(orgId, vendorId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Vendor mapping not found"));

            var mappings = await grain.GetAllMappingsAsync();
            return Results.Ok(Hal.Collection(
                $"/api/orgs/{orgId}/vendors/{vendorId}/mappings/items",
                mappings.Cast<object>(),
                mappings.Count));
        });

        // Get mapping for a specific vendor description
        group.MapGet("/lookup", async (
            Guid orgId,
            string vendorId,
            [FromQuery] string description,
            [FromQuery] string? productCode,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IVendorItemMappingGrain>(
                GrainKeys.VendorItemMapping(orgId, vendorId));

            var result = await grain.GetMappingAsync(description, productCode);

            if (!result.Found)
                return Results.NotFound(Hal.Error("not_found", "No mapping found for this description"));

            return Results.Ok(new
            {
                found = true,
                matchType = result.MatchType.ToString(),
                mapping = result.Mapping
            });
        });

        // Get suggestions for a vendor description
        group.MapGet("/suggest", async (
            Guid orgId,
            string vendorId,
            [FromQuery] string description,
            [FromQuery] int? maxSuggestions,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IVendorItemMappingGrain>(
                GrainKeys.VendorItemMapping(orgId, vendorId));

            var suggestions = await grain.GetSuggestionsAsync(
                description,
                null, // No candidate ingredients for now
                maxSuggestions ?? 5);

            return Results.Ok(new
            {
                description,
                suggestions
            });
        });

        // Set or update a mapping
        group.MapPut("/items/{description}", async (
            Guid orgId,
            string vendorId,
            string description,
            [FromBody] SetMappingRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IVendorItemMappingGrain>(
                GrainKeys.VendorItemMapping(orgId, vendorId));

            // Initialize if needed
            if (!await grain.ExistsAsync())
            {
                await grain.InitializeAsync(new InitializeVendorMappingCommand(
                    orgId,
                    vendorId,
                    vendorId,
                    VendorType.Unknown));
            }

            var mapping = await grain.SetMappingAsync(new SetMappingCommand(
                Uri.UnescapeDataString(description),
                request.IngredientId,
                request.IngredientName,
                request.IngredientSku,
                request.SetBy,
                request.VendorProductCode,
                request.ExpectedUnitPrice,
                request.Unit));

            return Results.Ok(mapping);
        });

        // Delete a mapping
        group.MapDelete("/items/{description}", async (
            Guid orgId,
            string vendorId,
            string description,
            [FromQuery] Guid deletedBy,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IVendorItemMappingGrain>(
                GrainKeys.VendorItemMapping(orgId, vendorId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Vendor mapping not found"));

            await grain.DeleteMappingAsync(new DeleteMappingCommand(
                Uri.UnescapeDataString(description),
                deletedBy));

            return Results.NoContent();
        });

        // Bulk import mappings
        group.MapPost("/import", async (
            Guid orgId,
            string vendorId,
            [FromBody] BulkImportMappingsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IVendorItemMappingGrain>(
                GrainKeys.VendorItemMapping(orgId, vendorId));

            // Initialize if needed
            if (!await grain.ExistsAsync())
            {
                await grain.InitializeAsync(new InitializeVendorMappingCommand(
                    orgId,
                    vendorId,
                    request.VendorName ?? vendorId,
                    request.VendorType ?? VendorType.Unknown));
            }

            var imported = 0;
            var errors = new List<string>();

            foreach (var mapping in request.Mappings)
            {
                try
                {
                    await grain.SetMappingAsync(new SetMappingCommand(
                        mapping.VendorDescription,
                        mapping.IngredientId,
                        mapping.IngredientName,
                        mapping.IngredientSku,
                        request.ImportedBy,
                        mapping.VendorProductCode,
                        mapping.ExpectedUnitPrice,
                        mapping.Unit));
                    imported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{mapping.VendorDescription}: {ex.Message}");
                }
            }

            return Results.Ok(new
            {
                imported,
                total = request.Mappings.Count,
                errors = errors.Count > 0 ? errors : null
            });
        });

        return app;
    }

    private static Dictionary<string, object> BuildVendorLinks(Guid orgId, string vendorId)
    {
        var basePath = $"/api/orgs/{orgId}/vendors/{vendorId}/mappings";
        return new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["items"] = new { href = $"{basePath}/items" },
            ["lookup"] = new { href = $"{basePath}/lookup{{?description,productCode}}", templated = true },
            ["suggest"] = new { href = $"{basePath}/suggest{{?description,maxSuggestions}}", templated = true },
            ["import"] = new { href = $"{basePath}/import" }
        };
    }
}
