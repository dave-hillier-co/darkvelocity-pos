using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class RecipeCmsEndpoints
{
    public static WebApplication MapRecipeCmsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/recipes/cms").WithTags("Recipe CMS");

        // ========================================================================
        // Recipe Document Endpoints
        // ========================================================================

        group.MapPost("/recipes", async (
            Guid orgId,
            [FromBody] CreateRecipeDocumentRequest request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var documentId = Guid.NewGuid().ToString();
            var grain = grainFactory.GetGrain<IRecipeDocumentGrain>(GrainKeys.RecipeDocument(orgId, documentId));

            var ingredients = request.Ingredients?.Select(i => new CreateRecipeIngredientCommand(
                IngredientId: i.IngredientId,
                IngredientName: i.IngredientName,
                Quantity: i.Quantity,
                Unit: i.Unit,
                WastePercentage: i.WastePercentage,
                UnitCost: i.UnitCost,
                PrepInstructions: i.PrepInstructions,
                IsOptional: i.IsOptional,
                DisplayOrder: i.DisplayOrder,
                SubstitutionIds: i.SubstitutionIds)).ToList();

            var command = new CreateRecipeDocumentCommand(
                Name: request.Name,
                Description: request.Description,
                PortionYield: request.PortionYield,
                YieldUnit: request.YieldUnit,
                Ingredients: ingredients,
                AllergenTags: request.AllergenTags,
                DietaryTags: request.DietaryTags,
                PrepInstructions: request.PrepInstructions,
                PrepTimeMinutes: request.PrepTimeMinutes,
                CookTimeMinutes: request.CookTimeMinutes,
                ImageUrl: request.ImageUrl,
                CategoryId: request.CategoryId,
                Locale: request.Locale,
                CreatedBy: userId,
                PublishImmediately: request.PublishImmediately);

            var result = await grain.CreateAsync(command);

            // Register in registry
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            await registryGrain.RegisterRecipeAsync(documentId, request.Name, result.Published?.CostPerPortion ?? result.Draft?.CostPerPortion ?? 0, request.CategoryId?.ToString());

            return Results.Created($"/api/orgs/{orgId}/recipes/cms/recipes/{documentId}",
                Hal.Resource(ToResponse(result), new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/recipes/cms/recipes/{documentId}" },
                    ["draft"] = new { href = $"/api/orgs/{orgId}/recipes/cms/recipes/{documentId}/draft" },
                    ["publish"] = new { href = $"/api/orgs/{orgId}/recipes/cms/recipes/{documentId}/publish" },
                    ["versions"] = new { href = $"/api/orgs/{orgId}/recipes/cms/recipes/{documentId}/versions" }
                }));
        });

        group.MapGet("/recipes", async (
            Guid orgId,
            [FromQuery] string? categoryId,
            [FromQuery] bool includeArchived,
            IGrainFactory grainFactory) =>
        {
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            var recipes = await registryGrain.GetRecipesAsync(categoryId, includeArchived);

            var response = recipes.Select(r => new RecipeSummaryResponse(
                DocumentId: r.DocumentId,
                Name: r.Name,
                CostPerPortion: r.CostPerPortion,
                CategoryId: r.CategoryId,
                HasDraft: r.HasDraft,
                IsArchived: r.IsArchived,
                PublishedVersion: r.PublishedVersion,
                LastModified: r.LastModified,
                LinkedMenuItemCount: r.LinkedMenuItemCount)).ToList();

            return Results.Ok(Hal.Resource(new { recipes = response }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/recipes/cms/recipes" }
            }));
        });

        group.MapGet("/recipes/{documentId}", async (
            Guid orgId,
            string documentId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeDocumentGrain>(GrainKeys.RecipeDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(ToResponse(snapshot), new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/recipes/cms/recipes/{documentId}" },
                ["draft"] = new { href = $"/api/orgs/{orgId}/recipes/cms/recipes/{documentId}/draft" },
                ["publish"] = new { href = $"/api/orgs/{orgId}/recipes/cms/recipes/{documentId}/publish" },
                ["versions"] = new { href = $"/api/orgs/{orgId}/recipes/cms/recipes/{documentId}/versions" }
            }));
        });

        group.MapPost("/recipes/{documentId}/draft", async (
            Guid orgId,
            string documentId,
            [FromBody] CreateRecipeDraftRequest request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeDocumentGrain>(GrainKeys.RecipeDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            var ingredients = request.Ingredients?.Select(i => new CreateRecipeIngredientCommand(
                IngredientId: i.IngredientId,
                IngredientName: i.IngredientName,
                Quantity: i.Quantity,
                Unit: i.Unit,
                WastePercentage: i.WastePercentage,
                UnitCost: i.UnitCost,
                PrepInstructions: i.PrepInstructions,
                IsOptional: i.IsOptional,
                DisplayOrder: i.DisplayOrder,
                SubstitutionIds: i.SubstitutionIds)).ToList();

            var command = new CreateRecipeDraftCommand(
                Name: request.Name,
                Description: request.Description,
                PortionYield: request.PortionYield,
                YieldUnit: request.YieldUnit,
                Ingredients: ingredients,
                AllergenTags: request.AllergenTags,
                DietaryTags: request.DietaryTags,
                PrepInstructions: request.PrepInstructions,
                PrepTimeMinutes: request.PrepTimeMinutes,
                CookTimeMinutes: request.CookTimeMinutes,
                ImageUrl: request.ImageUrl,
                CategoryId: request.CategoryId,
                ChangeNote: request.ChangeNote,
                CreatedBy: userId);

            var result = await grain.CreateDraftAsync(command);

            // Update registry
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            var snapshot = await grain.GetSnapshotAsync();
            var published = snapshot.Published;
            await registryGrain.UpdateRecipeAsync(
                documentId,
                published?.Name ?? result.Name,
                published?.CostPerPortion ?? result.CostPerPortion,
                published?.CategoryId?.ToString() ?? result.CategoryId?.ToString(),
                hasDraft: true,
                isArchived: snapshot.IsArchived,
                linkedMenuItemCount: snapshot.LinkedMenuItemIds.Count);

            return Results.Ok(Hal.Resource(ToVersionResponse(result), new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/recipes/cms/recipes/{documentId}/draft" },
                ["publish"] = new { href = $"/api/orgs/{orgId}/recipes/cms/recipes/{documentId}/publish" }
            }));
        });

        group.MapGet("/recipes/{documentId}/draft", async (
            Guid orgId,
            string documentId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeDocumentGrain>(GrainKeys.RecipeDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            var draft = await grain.GetDraftAsync();
            if (draft == null)
                return Results.NotFound(new { message = "No draft exists" });

            return Results.Ok(Hal.Resource(ToVersionResponse(draft), new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/recipes/cms/recipes/{documentId}/draft" }
            }));
        });

        group.MapDelete("/recipes/{documentId}/draft", async (
            Guid orgId,
            string documentId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeDocumentGrain>(GrainKeys.RecipeDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.DiscardDraftAsync();

            // Update registry
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            var snapshot = await grain.GetSnapshotAsync();
            var published = snapshot.Published;
            if (published != null)
            {
                await registryGrain.UpdateRecipeAsync(
                    documentId, published.Name, published.CostPerPortion, published.CategoryId?.ToString(),
                    hasDraft: false, isArchived: snapshot.IsArchived, linkedMenuItemCount: snapshot.LinkedMenuItemIds.Count);
            }

            return Results.NoContent();
        });

        group.MapPost("/recipes/{documentId}/publish", async (
            Guid orgId,
            string documentId,
            [FromBody] PublishDraftRequest? request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeDocumentGrain>(GrainKeys.RecipeDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.PublishDraftAsync(userId, request?.Note);

            var snapshot = await grain.GetSnapshotAsync();

            // Update registry
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            var published = snapshot.Published!;
            await registryGrain.UpdateRecipeAsync(
                documentId, published.Name, published.CostPerPortion, published.CategoryId?.ToString(),
                hasDraft: false, isArchived: snapshot.IsArchived, linkedMenuItemCount: snapshot.LinkedMenuItemIds.Count);

            return Results.Ok(Hal.Resource(ToResponse(snapshot), new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/recipes/cms/recipes/{documentId}" }
            }));
        });

        group.MapGet("/recipes/{documentId}/versions", async (
            Guid orgId,
            string documentId,
            [FromQuery] int skip,
            [FromQuery] int take,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeDocumentGrain>(GrainKeys.RecipeDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            take = take <= 0 ? 20 : Math.Min(take, 100);
            var versions = await grain.GetVersionHistoryAsync(skip, take);

            return Results.Ok(Hal.Resource(new
            {
                versions = versions.Select(ToVersionResponse).ToList()
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/recipes/cms/recipes/{documentId}/versions?skip={skip}&take={take}" }
            }));
        });

        group.MapPost("/recipes/{documentId}/revert", async (
            Guid orgId,
            string documentId,
            [FromBody] RevertVersionRequest request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeDocumentGrain>(GrainKeys.RecipeDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.RevertToVersionAsync(request.Version, userId, request.Reason);
            var snapshot = await grain.GetSnapshotAsync();

            // Update registry
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            var published = snapshot.Published!;
            await registryGrain.UpdateRecipeAsync(
                documentId, published.Name, published.CostPerPortion, published.CategoryId?.ToString(),
                hasDraft: false, isArchived: snapshot.IsArchived, linkedMenuItemCount: snapshot.LinkedMenuItemIds.Count);

            return Results.Ok(Hal.Resource(ToResponse(snapshot), new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/recipes/cms/recipes/{documentId}" }
            }));
        });

        group.MapPost("/recipes/{documentId}/translations", async (
            Guid orgId,
            string documentId,
            [FromBody] AddRecipeTranslationRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeDocumentGrain>(GrainKeys.RecipeDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.AddTranslationAsync(new AddRecipeTranslationCommand(
                request.Locale, request.Name, request.Description, request.PrepInstructions));

            return Results.NoContent();
        });

        group.MapPost("/recipes/{documentId}/schedule", async (
            Guid orgId,
            string documentId,
            [FromBody] ScheduleChangeRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeDocumentGrain>(GrainKeys.RecipeDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            var schedule = await grain.ScheduleChangeAsync(request.Version, request.ActivateAt, request.DeactivateAt, request.Name);

            return Results.Created($"/api/orgs/{orgId}/recipes/cms/recipes/{documentId}/schedule/{schedule.ScheduleId}",
                new ScheduledChangeResponse(
                    ScheduleId: schedule.ScheduleId,
                    Version: schedule.VersionToActivate,
                    ActivateAt: schedule.ActivateAt,
                    DeactivateAt: schedule.DeactivateAt,
                    Name: schedule.Name,
                    IsActive: schedule.IsActive));
        });

        group.MapDelete("/recipes/{documentId}/schedule/{scheduleId}", async (
            Guid orgId,
            string documentId,
            string scheduleId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeDocumentGrain>(GrainKeys.RecipeDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.CancelScheduleAsync(scheduleId);
            return Results.NoContent();
        });

        group.MapPost("/recipes/{documentId}/archive", async (
            Guid orgId,
            string documentId,
            [FromBody] ArchiveDocumentRequest? request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeDocumentGrain>(GrainKeys.RecipeDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.ArchiveAsync(userId, request?.Reason);

            // Update registry
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            var snapshot = await grain.GetSnapshotAsync();
            var published = snapshot.Published;
            if (published != null)
            {
                await registryGrain.UpdateRecipeAsync(
                    documentId, published.Name, published.CostPerPortion, published.CategoryId?.ToString(),
                    hasDraft: snapshot.DraftVersion.HasValue, isArchived: true, linkedMenuItemCount: snapshot.LinkedMenuItemIds.Count);
            }

            return Results.NoContent();
        });

        group.MapPost("/recipes/{documentId}/restore", async (
            Guid orgId,
            string documentId,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeDocumentGrain>(GrainKeys.RecipeDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.RestoreAsync(userId);

            // Update registry
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            var snapshot = await grain.GetSnapshotAsync();
            var published = snapshot.Published;
            if (published != null)
            {
                await registryGrain.UpdateRecipeAsync(
                    documentId, published.Name, published.CostPerPortion, published.CategoryId?.ToString(),
                    hasDraft: snapshot.DraftVersion.HasValue, isArchived: false, linkedMenuItemCount: snapshot.LinkedMenuItemIds.Count);
            }

            return Results.NoContent();
        });

        group.MapPost("/recipes/{documentId}/recalculate-cost", async (
            Guid orgId,
            string documentId,
            [FromBody] RecalculateCostRequest? request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeDocumentGrain>(GrainKeys.RecipeDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.RecalculateCostAsync(request?.IngredientPrices);
            var snapshot = await grain.GetSnapshotAsync();

            // Update registry
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            var published = snapshot.Published;
            if (published != null)
            {
                await registryGrain.UpdateRecipeAsync(
                    documentId, published.Name, published.CostPerPortion, published.CategoryId?.ToString(),
                    hasDraft: snapshot.DraftVersion.HasValue, isArchived: snapshot.IsArchived, linkedMenuItemCount: snapshot.LinkedMenuItemIds.Count);
            }

            return Results.Ok(ToResponse(snapshot));
        });

        group.MapPost("/recipes/{documentId}/link-menu-item", async (
            Guid orgId,
            string documentId,
            [FromBody] LinkRecipeToMenuItemRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeDocumentGrain>(GrainKeys.RecipeDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.LinkMenuItemAsync(request.MenuItemDocumentId);

            // Update registry
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            var snapshot = await grain.GetSnapshotAsync();
            var published = snapshot.Published;
            if (published != null)
            {
                await registryGrain.UpdateRecipeAsync(
                    documentId, published.Name, published.CostPerPortion, published.CategoryId?.ToString(),
                    hasDraft: snapshot.DraftVersion.HasValue, isArchived: snapshot.IsArchived, linkedMenuItemCount: snapshot.LinkedMenuItemIds.Count);
            }

            return Results.NoContent();
        });

        group.MapDelete("/recipes/{documentId}/link-menu-item/{menuItemDocumentId}", async (
            Guid orgId,
            string documentId,
            string menuItemDocumentId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeDocumentGrain>(GrainKeys.RecipeDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.UnlinkMenuItemAsync(menuItemDocumentId);

            // Update registry
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            var snapshot = await grain.GetSnapshotAsync();
            var published = snapshot.Published;
            if (published != null)
            {
                await registryGrain.UpdateRecipeAsync(
                    documentId, published.Name, published.CostPerPortion, published.CategoryId?.ToString(),
                    hasDraft: snapshot.DraftVersion.HasValue, isArchived: snapshot.IsArchived, linkedMenuItemCount: snapshot.LinkedMenuItemIds.Count);
            }

            return Results.NoContent();
        });

        group.MapGet("/recipes/search", async (
            Guid orgId,
            [FromQuery] string query,
            [FromQuery] int take,
            IGrainFactory grainFactory) =>
        {
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            take = take <= 0 ? 20 : Math.Min(take, 100);
            var recipes = await registryGrain.SearchRecipesAsync(query, take);

            var response = recipes.Select(r => new RecipeSummaryResponse(
                DocumentId: r.DocumentId,
                Name: r.Name,
                CostPerPortion: r.CostPerPortion,
                CategoryId: r.CategoryId,
                HasDraft: r.HasDraft,
                IsArchived: r.IsArchived,
                PublishedVersion: r.PublishedVersion,
                LastModified: r.LastModified,
                LinkedMenuItemCount: r.LinkedMenuItemCount)).ToList();

            return Results.Ok(Hal.Resource(new { recipes = response }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/recipes/cms/recipes/search?query={query}&take={take}" }
            }));
        });

        // ========================================================================
        // Recipe Category Document Endpoints
        // ========================================================================

        group.MapPost("/categories", async (
            Guid orgId,
            [FromBody] CreateRecipeCategoryDocumentRequest request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var documentId = Guid.NewGuid().ToString();
            var grain = grainFactory.GetGrain<IRecipeCategoryDocumentGrain>(GrainKeys.RecipeCategoryDocument(orgId, documentId));

            var command = new CreateRecipeCategoryDocumentCommand(
                Name: request.Name,
                DisplayOrder: request.DisplayOrder,
                Description: request.Description,
                Color: request.Color,
                IconUrl: request.IconUrl,
                Locale: request.Locale,
                CreatedBy: userId,
                PublishImmediately: request.PublishImmediately);

            var result = await grain.CreateAsync(command);

            // Register in registry
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            await registryGrain.RegisterCategoryAsync(documentId, request.Name, request.DisplayOrder, request.Color);

            return Results.Created($"/api/orgs/{orgId}/recipes/cms/categories/{documentId}",
                Hal.Resource(ToCategoryResponse(result), new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/recipes/cms/categories/{documentId}" }
                }));
        });

        group.MapGet("/categories", async (
            Guid orgId,
            [FromQuery] bool includeArchived,
            IGrainFactory grainFactory) =>
        {
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            var categories = await registryGrain.GetCategoriesAsync(includeArchived);

            var response = categories.Select(c => new RecipeCategorySummaryResponse(
                DocumentId: c.DocumentId,
                Name: c.Name,
                DisplayOrder: c.DisplayOrder,
                Color: c.Color,
                HasDraft: c.HasDraft,
                IsArchived: c.IsArchived,
                RecipeCount: c.RecipeCount,
                LastModified: c.LastModified)).ToList();

            return Results.Ok(Hal.Resource(new { categories = response }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/recipes/cms/categories" }
            }));
        });

        group.MapGet("/categories/{documentId}", async (
            Guid orgId,
            string documentId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeCategoryDocumentGrain>(GrainKeys.RecipeCategoryDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(ToCategoryResponse(snapshot), new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/recipes/cms/categories/{documentId}" }
            }));
        });

        group.MapPost("/categories/{documentId}/draft", async (
            Guid orgId,
            string documentId,
            [FromBody] CreateRecipeCategoryDraftRequest request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeCategoryDocumentGrain>(GrainKeys.RecipeCategoryDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            var command = new CreateRecipeCategoryDraftCommand(
                Name: request.Name,
                DisplayOrder: request.DisplayOrder,
                Description: request.Description,
                Color: request.Color,
                IconUrl: request.IconUrl,
                RecipeDocumentIds: request.RecipeDocumentIds,
                ChangeNote: request.ChangeNote,
                CreatedBy: userId);

            var result = await grain.CreateDraftAsync(command);
            return Results.Ok(ToCategoryVersionResponse(result));
        });

        group.MapPost("/categories/{documentId}/publish", async (
            Guid orgId,
            string documentId,
            [FromBody] PublishDraftRequest? request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeCategoryDocumentGrain>(GrainKeys.RecipeCategoryDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.PublishDraftAsync(userId, request?.Note);
            var snapshot = await grain.GetSnapshotAsync();

            // Update registry
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            var published = snapshot.Published!;
            await registryGrain.UpdateCategoryAsync(
                documentId, published.Name, published.DisplayOrder, published.Color,
                hasDraft: false, isArchived: snapshot.IsArchived, recipeCount: published.RecipeDocumentIds.Count);

            return Results.Ok(ToCategoryResponse(snapshot));
        });

        group.MapPost("/categories/{documentId}/archive", async (
            Guid orgId,
            string documentId,
            [FromBody] ArchiveDocumentRequest? request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeCategoryDocumentGrain>(GrainKeys.RecipeCategoryDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.ArchiveAsync(userId, request?.Reason);

            // Update registry
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            var snapshot = await grain.GetSnapshotAsync();
            var published = snapshot.Published;
            if (published != null)
            {
                await registryGrain.UpdateCategoryAsync(
                    documentId, published.Name, published.DisplayOrder, published.Color,
                    hasDraft: snapshot.DraftVersion.HasValue, isArchived: true, recipeCount: published.RecipeDocumentIds.Count);
            }

            return Results.NoContent();
        });

        group.MapPost("/categories/{documentId}/restore", async (
            Guid orgId,
            string documentId,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRecipeCategoryDocumentGrain>(GrainKeys.RecipeCategoryDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.RestoreAsync(userId);

            // Update registry
            var registryGrain = grainFactory.GetGrain<IRecipeRegistryGrain>(GrainKeys.RecipeRegistry(orgId));
            var snapshot = await grain.GetSnapshotAsync();
            var published = snapshot.Published;
            if (published != null)
            {
                await registryGrain.UpdateCategoryAsync(
                    documentId, published.Name, published.DisplayOrder, published.Color,
                    hasDraft: snapshot.DraftVersion.HasValue, isArchived: false, recipeCount: published.RecipeDocumentIds.Count);
            }

            return Results.NoContent();
        });

        return app;
    }

    // ========================================================================
    // Response Mapping Helpers
    // ========================================================================

    private static RecipeDocumentResponse ToResponse(RecipeDocumentSnapshot snapshot) => new(
        DocumentId: snapshot.DocumentId,
        OrgId: snapshot.OrgId,
        CurrentVersion: snapshot.CurrentVersion,
        PublishedVersion: snapshot.PublishedVersion,
        DraftVersion: snapshot.DraftVersion,
        IsArchived: snapshot.IsArchived,
        CreatedAt: snapshot.CreatedAt,
        Published: snapshot.Published != null ? ToVersionResponse(snapshot.Published) : null,
        Draft: snapshot.Draft != null ? ToVersionResponse(snapshot.Draft) : null,
        Schedules: snapshot.Schedules.Select(s => new ScheduledChangeResponse(
            ScheduleId: s.ScheduleId,
            Version: s.VersionToActivate,
            ActivateAt: s.ActivateAt,
            DeactivateAt: s.DeactivateAt,
            Name: s.Name,
            IsActive: s.IsActive)).ToList(),
        TotalVersions: snapshot.TotalVersions,
        LinkedMenuItemIds: snapshot.LinkedMenuItemIds);

    private static RecipeVersionResponse ToVersionResponse(RecipeVersionSnapshot v) => new(
        VersionNumber: v.VersionNumber,
        CreatedAt: v.CreatedAt,
        CreatedBy: v.CreatedBy,
        ChangeNote: v.ChangeNote,
        Name: v.Name,
        Description: v.Description,
        PortionYield: v.PortionYield,
        YieldUnit: v.YieldUnit,
        Ingredients: v.Ingredients.Select(i => new RecipeIngredientResponse(
            IngredientId: i.IngredientId,
            IngredientName: i.IngredientName,
            Quantity: i.Quantity,
            Unit: i.Unit,
            WastePercentage: i.WastePercentage,
            EffectiveQuantity: i.EffectiveQuantity,
            UnitCost: i.UnitCost,
            LineCost: i.LineCost,
            PrepInstructions: i.PrepInstructions,
            IsOptional: i.IsOptional,
            DisplayOrder: i.DisplayOrder,
            SubstitutionIds: i.SubstitutionIds)).ToList(),
        AllergenTags: v.AllergenTags,
        DietaryTags: v.DietaryTags,
        PrepInstructions: v.PrepInstructions,
        PrepTimeMinutes: v.PrepTimeMinutes,
        CookTimeMinutes: v.CookTimeMinutes,
        ImageUrl: v.ImageUrl,
        CategoryId: v.CategoryId,
        TheoreticalCost: v.TheoreticalCost,
        CostPerPortion: v.CostPerPortion);

    private static RecipeCategoryDocumentResponse ToCategoryResponse(RecipeCategoryDocumentSnapshot snapshot) => new(
        DocumentId: snapshot.DocumentId,
        OrgId: snapshot.OrgId,
        CurrentVersion: snapshot.CurrentVersion,
        PublishedVersion: snapshot.PublishedVersion,
        DraftVersion: snapshot.DraftVersion,
        IsArchived: snapshot.IsArchived,
        CreatedAt: snapshot.CreatedAt,
        Published: snapshot.Published != null ? ToCategoryVersionResponse(snapshot.Published) : null,
        Draft: snapshot.Draft != null ? ToCategoryVersionResponse(snapshot.Draft) : null,
        Schedules: snapshot.Schedules.Select(s => new ScheduledChangeResponse(
            ScheduleId: s.ScheduleId,
            Version: s.VersionToActivate,
            ActivateAt: s.ActivateAt,
            DeactivateAt: s.DeactivateAt,
            Name: s.Name,
            IsActive: s.IsActive)).ToList(),
        TotalVersions: snapshot.TotalVersions);

    private static RecipeCategoryVersionResponse ToCategoryVersionResponse(RecipeCategoryVersionSnapshot v) => new(
        VersionNumber: v.VersionNumber,
        CreatedAt: v.CreatedAt,
        CreatedBy: v.CreatedBy,
        ChangeNote: v.ChangeNote,
        Name: v.Name,
        Description: v.Description,
        Color: v.Color,
        IconUrl: v.IconUrl,
        DisplayOrder: v.DisplayOrder,
        RecipeDocumentIds: v.RecipeDocumentIds);
}
