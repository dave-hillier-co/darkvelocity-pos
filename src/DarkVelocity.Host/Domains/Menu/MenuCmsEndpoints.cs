using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class MenuCmsEndpoints
{
    public static WebApplication MapMenuCmsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/menu/cms").WithTags("Menu CMS");

        // ========================================================================
        // Menu Item Document Endpoints
        // ========================================================================

        group.MapPost("/items", async (
            Guid orgId,
            [FromBody] CreateMenuItemDocumentRequest request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var documentId = Guid.NewGuid().ToString();
            var grain = grainFactory.GetGrain<IMenuItemDocumentGrain>(GrainKeys.MenuItemDocument(orgId, documentId));

            var command = new CreateMenuItemDocumentCommand(
                Name: request.Name,
                Price: request.Price,
                Description: request.Description,
                CategoryId: request.CategoryId,
                AccountingGroupId: request.AccountingGroupId,
                RecipeId: request.RecipeId,
                ImageUrl: request.ImageUrl,
                Sku: request.Sku,
                TrackInventory: request.TrackInventory,
                Locale: request.Locale,
                CreatedBy: userId,
                PublishImmediately: request.PublishImmediately);

            var result = await grain.CreateAsync(command);

            // Register in registry
            var registryGrain = grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
            await registryGrain.RegisterItemAsync(documentId, request.Name, request.Price, request.CategoryId?.ToString());

            return Results.Created($"/api/orgs/{orgId}/menu/cms/items/{documentId}",
                Hal.Resource(ToResponse(result), new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/menu/cms/items/{documentId}" },
                    ["draft"] = new { href = $"/api/orgs/{orgId}/menu/cms/items/{documentId}/draft" },
                    ["publish"] = new { href = $"/api/orgs/{orgId}/menu/cms/items/{documentId}/publish" },
                    ["versions"] = new { href = $"/api/orgs/{orgId}/menu/cms/items/{documentId}/versions" }
                }));
        });

        group.MapGet("/items", async (
            Guid orgId,
            [FromQuery] string? categoryId,
            [FromQuery] bool includeArchived,
            IGrainFactory grainFactory) =>
        {
            var registryGrain = grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
            var items = await registryGrain.GetItemsAsync(categoryId, includeArchived);

            var response = items.Select(i => new MenuItemSummaryResponse(
                DocumentId: i.DocumentId,
                Name: i.Name,
                Price: i.Price,
                CategoryId: i.CategoryId,
                HasDraft: i.HasDraft,
                IsArchived: i.IsArchived,
                PublishedVersion: i.PublishedVersion,
                LastModified: i.LastModified)).ToList();

            return Results.Ok(Hal.Resource(new { items = response }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/cms/items" }
            }));
        });

        group.MapGet("/items/{documentId}", async (
            Guid orgId,
            string documentId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuItemDocumentGrain>(GrainKeys.MenuItemDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(ToResponse(snapshot), new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/cms/items/{documentId}" },
                ["draft"] = new { href = $"/api/orgs/{orgId}/menu/cms/items/{documentId}/draft" },
                ["publish"] = new { href = $"/api/orgs/{orgId}/menu/cms/items/{documentId}/publish" },
                ["versions"] = new { href = $"/api/orgs/{orgId}/menu/cms/items/{documentId}/versions" }
            }));
        });

        group.MapPost("/items/{documentId}/draft", async (
            Guid orgId,
            string documentId,
            [FromBody] CreateMenuItemDraftRequest request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuItemDocumentGrain>(GrainKeys.MenuItemDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            var command = new CreateMenuItemDraftCommand(
                Name: request.Name,
                Price: request.Price,
                Description: request.Description,
                CategoryId: request.CategoryId,
                AccountingGroupId: request.AccountingGroupId,
                RecipeId: request.RecipeId,
                ImageUrl: request.ImageUrl,
                Sku: request.Sku,
                TrackInventory: request.TrackInventory,
                ModifierBlockIds: request.ModifierBlockIds,
                TagIds: request.TagIds,
                ChangeNote: request.ChangeNote,
                CreatedBy: userId);

            var result = await grain.CreateDraftAsync(command);

            // Update registry
            var registryGrain = grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
            var snapshot = await grain.GetSnapshotAsync();
            var published = snapshot.Published;
            await registryGrain.UpdateItemAsync(
                documentId,
                published?.Name ?? result.Name,
                published?.Price ?? result.Price,
                published?.CategoryId?.ToString() ?? result.CategoryId?.ToString(),
                hasDraft: true,
                isArchived: snapshot.IsArchived);

            return Results.Ok(Hal.Resource(ToVersionResponse(result), new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/cms/items/{documentId}/draft" },
                ["publish"] = new { href = $"/api/orgs/{orgId}/menu/cms/items/{documentId}/publish" }
            }));
        });

        group.MapGet("/items/{documentId}/draft", async (
            Guid orgId,
            string documentId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuItemDocumentGrain>(GrainKeys.MenuItemDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            var draft = await grain.GetDraftAsync();
            if (draft == null)
                return Results.NotFound(new { message = "No draft exists" });

            return Results.Ok(Hal.Resource(ToVersionResponse(draft), new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/cms/items/{documentId}/draft" }
            }));
        });

        group.MapDelete("/items/{documentId}/draft", async (
            Guid orgId,
            string documentId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuItemDocumentGrain>(GrainKeys.MenuItemDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.DiscardDraftAsync();

            // Update registry
            var registryGrain = grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
            var snapshot = await grain.GetSnapshotAsync();
            var published = snapshot.Published;
            if (published != null)
            {
                await registryGrain.UpdateItemAsync(
                    documentId, published.Name, published.Price, published.CategoryId?.ToString(),
                    hasDraft: false, isArchived: snapshot.IsArchived);
            }

            return Results.NoContent();
        });

        group.MapPost("/items/{documentId}/publish", async (
            Guid orgId,
            string documentId,
            [FromBody] PublishDraftRequest? request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuItemDocumentGrain>(GrainKeys.MenuItemDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.PublishDraftAsync(userId, request?.Note);

            var snapshot = await grain.GetSnapshotAsync();

            // Update registry
            var registryGrain = grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
            var published = snapshot.Published!;
            await registryGrain.UpdateItemAsync(
                documentId, published.Name, published.Price, published.CategoryId?.ToString(),
                hasDraft: false, isArchived: snapshot.IsArchived);

            return Results.Ok(Hal.Resource(ToResponse(snapshot), new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/cms/items/{documentId}" }
            }));
        });

        group.MapGet("/items/{documentId}/versions", async (
            Guid orgId,
            string documentId,
            [FromQuery] int skip,
            [FromQuery] int take,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuItemDocumentGrain>(GrainKeys.MenuItemDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            take = take <= 0 ? 20 : Math.Min(take, 100);
            var versions = await grain.GetVersionHistoryAsync(skip, take);

            return Results.Ok(Hal.Resource(new
            {
                versions = versions.Select(ToVersionResponse).ToList()
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/cms/items/{documentId}/versions?skip={skip}&take={take}" }
            }));
        });

        group.MapPost("/items/{documentId}/revert", async (
            Guid orgId,
            string documentId,
            [FromBody] RevertVersionRequest request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuItemDocumentGrain>(GrainKeys.MenuItemDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.RevertToVersionAsync(request.Version, userId, request.Reason);
            var snapshot = await grain.GetSnapshotAsync();

            // Update registry
            var registryGrain = grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
            var published = snapshot.Published!;
            await registryGrain.UpdateItemAsync(
                documentId, published.Name, published.Price, published.CategoryId?.ToString(),
                hasDraft: false, isArchived: snapshot.IsArchived);

            return Results.Ok(Hal.Resource(ToResponse(snapshot), new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/cms/items/{documentId}" }
            }));
        });

        group.MapPost("/items/{documentId}/translations", async (
            Guid orgId,
            string documentId,
            [FromBody] AddTranslationRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuItemDocumentGrain>(GrainKeys.MenuItemDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.AddTranslationAsync(new AddMenuItemTranslationCommand(
                request.Locale, request.Name, request.Description, request.KitchenName));

            return Results.NoContent();
        });

        group.MapPost("/items/{documentId}/schedule", async (
            Guid orgId,
            string documentId,
            [FromBody] ScheduleChangeRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuItemDocumentGrain>(GrainKeys.MenuItemDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            var schedule = await grain.ScheduleChangeAsync(request.Version, request.ActivateAt, request.DeactivateAt, request.Name);

            return Results.Created($"/api/orgs/{orgId}/menu/cms/items/{documentId}/schedule/{schedule.ScheduleId}",
                ToScheduleResponse(schedule));
        });

        group.MapDelete("/items/{documentId}/schedule/{scheduleId}", async (
            Guid orgId,
            string documentId,
            string scheduleId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuItemDocumentGrain>(GrainKeys.MenuItemDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.CancelScheduleAsync(scheduleId);
            return Results.NoContent();
        });

        group.MapPost("/items/{documentId}/archive", async (
            Guid orgId,
            string documentId,
            [FromBody] ArchiveDocumentRequest? request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuItemDocumentGrain>(GrainKeys.MenuItemDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.ArchiveAsync(userId, request?.Reason);

            // Update registry
            var registryGrain = grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
            var snapshot = await grain.GetSnapshotAsync();
            var published = snapshot.Published;
            if (published != null)
            {
                await registryGrain.UpdateItemAsync(
                    documentId, published.Name, published.Price, published.CategoryId?.ToString(),
                    hasDraft: snapshot.DraftVersion.HasValue, isArchived: true);
            }

            return Results.NoContent();
        });

        group.MapPost("/items/{documentId}/restore", async (
            Guid orgId,
            string documentId,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuItemDocumentGrain>(GrainKeys.MenuItemDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.RestoreAsync(userId);

            // Update registry
            var registryGrain = grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
            var snapshot = await grain.GetSnapshotAsync();
            var published = snapshot.Published;
            if (published != null)
            {
                await registryGrain.UpdateItemAsync(
                    documentId, published.Name, published.Price, published.CategoryId?.ToString(),
                    hasDraft: snapshot.DraftVersion.HasValue, isArchived: false);
            }

            return Results.NoContent();
        });

        // ========================================================================
        // Menu Category Document Endpoints
        // ========================================================================

        group.MapPost("/categories", async (
            Guid orgId,
            [FromBody] CreateMenuCategoryDocumentRequest request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var documentId = Guid.NewGuid().ToString();
            var grain = grainFactory.GetGrain<IMenuCategoryDocumentGrain>(GrainKeys.MenuCategoryDocument(orgId, documentId));

            var command = new CreateMenuCategoryDocumentCommand(
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
            var registryGrain = grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
            await registryGrain.RegisterCategoryAsync(documentId, request.Name, request.DisplayOrder, request.Color);

            return Results.Created($"/api/orgs/{orgId}/menu/cms/categories/{documentId}",
                Hal.Resource(ToCategoryResponse(result), new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/menu/cms/categories/{documentId}" }
                }));
        });

        group.MapGet("/categories", async (
            Guid orgId,
            [FromQuery] bool includeArchived,
            IGrainFactory grainFactory) =>
        {
            var registryGrain = grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
            var categories = await registryGrain.GetCategoriesAsync(includeArchived);

            var response = categories.Select(c => new MenuCategorySummaryResponse(
                DocumentId: c.DocumentId,
                Name: c.Name,
                DisplayOrder: c.DisplayOrder,
                Color: c.Color,
                HasDraft: c.HasDraft,
                IsArchived: c.IsArchived,
                ItemCount: c.ItemCount,
                LastModified: c.LastModified)).ToList();

            return Results.Ok(Hal.Resource(new { categories = response }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/cms/categories" }
            }));
        });

        group.MapGet("/categories/{documentId}", async (
            Guid orgId,
            string documentId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuCategoryDocumentGrain>(GrainKeys.MenuCategoryDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(ToCategoryResponse(snapshot), new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/cms/categories/{documentId}" }
            }));
        });

        group.MapPost("/categories/{documentId}/draft", async (
            Guid orgId,
            string documentId,
            [FromBody] CreateMenuCategoryDraftRequest request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuCategoryDocumentGrain>(GrainKeys.MenuCategoryDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            var command = new CreateMenuCategoryDraftCommand(
                Name: request.Name,
                DisplayOrder: request.DisplayOrder,
                Description: request.Description,
                Color: request.Color,
                IconUrl: request.IconUrl,
                ItemDocumentIds: request.ItemDocumentIds,
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
            var grain = grainFactory.GetGrain<IMenuCategoryDocumentGrain>(GrainKeys.MenuCategoryDocument(orgId, documentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.PublishDraftAsync(userId, request?.Note);
            var snapshot = await grain.GetSnapshotAsync();

            // Update registry
            var registryGrain = grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
            var published = snapshot.Published!;
            await registryGrain.UpdateCategoryAsync(
                documentId, published.Name, published.DisplayOrder, published.Color,
                hasDraft: false, isArchived: snapshot.IsArchived, itemCount: published.ItemDocumentIds.Count);

            return Results.Ok(ToCategoryResponse(snapshot));
        });

        // ========================================================================
        // Modifier Block Endpoints
        // ========================================================================

        group.MapPost("/modifier-blocks", async (
            Guid orgId,
            [FromBody] CreateModifierBlockRequest request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var blockId = Guid.NewGuid().ToString();
            var grain = grainFactory.GetGrain<IModifierBlockGrain>(GrainKeys.ModifierBlock(orgId, blockId));

            var options = request.Options?.Select(o => new CreateModifierOptionCommand(
                o.Name, o.PriceAdjustment, o.IsDefault, o.DisplayOrder,
                o.ServingSize, o.ServingUnit, o.InventoryItemId)).ToList();

            var command = new CreateModifierBlockCommand(
                Name: request.Name,
                SelectionRule: request.SelectionRule,
                MinSelections: request.MinSelections,
                MaxSelections: request.MaxSelections,
                IsRequired: request.IsRequired,
                Options: options,
                CreatedBy: userId,
                PublishImmediately: request.PublishImmediately);

            var result = await grain.CreateAsync(command);

            // Register in registry
            var registryGrain = grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
            await registryGrain.RegisterModifierBlockAsync(blockId, request.Name);

            return Results.Created($"/api/orgs/{orgId}/menu/cms/modifier-blocks/{blockId}",
                ToModifierBlockResponse(result));
        });

        group.MapGet("/modifier-blocks", async (
            Guid orgId,
            IGrainFactory grainFactory) =>
        {
            var registryGrain = grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
            var blockIds = await registryGrain.GetModifierBlockIdsAsync();

            var blocks = new List<ModifierBlockResponse>();
            foreach (var blockId in blockIds)
            {
                var grain = grainFactory.GetGrain<IModifierBlockGrain>(GrainKeys.ModifierBlock(orgId, blockId));
                if (await grain.ExistsAsync())
                {
                    var snapshot = await grain.GetSnapshotAsync();
                    if (!snapshot.IsArchived)
                        blocks.Add(ToModifierBlockResponse(snapshot));
                }
            }

            return Results.Ok(Hal.Resource(new { modifierBlocks = blocks }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/cms/modifier-blocks" }
            }));
        });

        group.MapGet("/modifier-blocks/{blockId}", async (
            Guid orgId,
            string blockId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IModifierBlockGrain>(GrainKeys.ModifierBlock(orgId, blockId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(ToModifierBlockResponse(snapshot));
        });

        group.MapPost("/modifier-blocks/{blockId}/publish", async (
            Guid orgId,
            string blockId,
            [FromBody] PublishDraftRequest? request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IModifierBlockGrain>(GrainKeys.ModifierBlock(orgId, blockId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.PublishDraftAsync(userId, request?.Note);
            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(ToModifierBlockResponse(snapshot));
        });

        // ========================================================================
        // Content Tag Endpoints
        // ========================================================================

        group.MapPost("/tags", async (
            Guid orgId,
            [FromBody] CreateContentTagRequest request,
            IGrainFactory grainFactory) =>
        {
            var tagId = Guid.NewGuid().ToString();
            var grain = grainFactory.GetGrain<IContentTagGrain>(GrainKeys.ContentTag(orgId, tagId));

            var command = new CreateContentTagCommand(
                Name: request.Name,
                Category: request.Category,
                IconUrl: request.IconUrl,
                BadgeColor: request.BadgeColor,
                DisplayOrder: request.DisplayOrder,
                ExternalTagId: request.ExternalTagId,
                ExternalPlatform: request.ExternalPlatform);

            var result = await grain.CreateAsync(command);

            // Register in registry
            var registryGrain = grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
            await registryGrain.RegisterTagAsync(tagId, request.Name, request.Category);

            return Results.Created($"/api/orgs/{orgId}/menu/cms/tags/{tagId}", ToContentTagResponse(result));
        });

        group.MapGet("/tags", async (
            Guid orgId,
            [FromQuery] TagCategory? category,
            IGrainFactory grainFactory) =>
        {
            var registryGrain = grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
            var tagIds = await registryGrain.GetTagIdsAsync(category);

            var tags = new List<ContentTagResponse>();
            foreach (var tagId in tagIds)
            {
                var grain = grainFactory.GetGrain<IContentTagGrain>(GrainKeys.ContentTag(orgId, tagId));
                if (await grain.ExistsAsync())
                {
                    var snapshot = await grain.GetSnapshotAsync();
                    if (snapshot.IsActive)
                        tags.Add(ToContentTagResponse(snapshot));
                }
            }

            return Results.Ok(Hal.Resource(new { tags }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/cms/tags" }
            }));
        });

        group.MapPatch("/tags/{tagId}", async (
            Guid orgId,
            string tagId,
            [FromBody] UpdateContentTagRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IContentTagGrain>(GrainKeys.ContentTag(orgId, tagId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            var result = await grain.UpdateAsync(new UpdateContentTagCommand(
                Name: request.Name,
                IconUrl: request.IconUrl,
                BadgeColor: request.BadgeColor,
                DisplayOrder: request.DisplayOrder,
                IsActive: request.IsActive));

            return Results.Ok(ToContentTagResponse(result));
        });

        // ========================================================================
        // Site Menu Overrides Endpoints
        // ========================================================================

        var siteGroup = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/menu").WithTags("Site Menu");

        siteGroup.MapGet("/overrides", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISiteMenuOverridesGrain>(GrainKeys.SiteMenuOverrides(orgId, siteId));
            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(ToSiteOverridesResponse(snapshot));
        });

        siteGroup.MapPost("/overrides/price", async (
            Guid orgId,
            Guid siteId,
            [FromBody] SetSitePriceOverrideRequest request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISiteMenuOverridesGrain>(GrainKeys.SiteMenuOverrides(orgId, siteId));

            await grain.SetPriceOverrideAsync(new SetSitePriceOverrideCommand(
                ItemDocumentId: request.ItemDocumentId,
                Price: request.Price,
                EffectiveFrom: request.EffectiveFrom,
                EffectiveUntil: request.EffectiveUntil,
                Reason: request.Reason,
                SetBy: userId));

            // Invalidate cache
            var resolverGrain = grainFactory.GetGrain<IMenuContentResolverGrain>(GrainKeys.MenuContentResolver(orgId, siteId));
            await resolverGrain.InvalidateCacheAsync();

            return Results.NoContent();
        });

        siteGroup.MapDelete("/overrides/price/{itemDocumentId}", async (
            Guid orgId,
            Guid siteId,
            string itemDocumentId,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISiteMenuOverridesGrain>(GrainKeys.SiteMenuOverrides(orgId, siteId));
            await grain.RemovePriceOverrideAsync(itemDocumentId, userId);

            var resolverGrain = grainFactory.GetGrain<IMenuContentResolverGrain>(GrainKeys.MenuContentResolver(orgId, siteId));
            await resolverGrain.InvalidateCacheAsync();

            return Results.NoContent();
        });

        siteGroup.MapPost("/overrides/visibility", async (
            Guid orgId,
            Guid siteId,
            [FromBody] SetVisibilityRequest request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISiteMenuOverridesGrain>(GrainKeys.SiteMenuOverrides(orgId, siteId));

            if (request.IsHidden)
                await grain.HideItemAsync(request.DocumentId, userId);
            else
                await grain.UnhideItemAsync(request.DocumentId, userId);

            var resolverGrain = grainFactory.GetGrain<IMenuContentResolverGrain>(GrainKeys.MenuContentResolver(orgId, siteId));
            await resolverGrain.InvalidateCacheAsync();

            return Results.NoContent();
        });

        siteGroup.MapPost("/overrides/snooze", async (
            Guid orgId,
            Guid siteId,
            [FromBody] SnoozeItemRequest request,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISiteMenuOverridesGrain>(GrainKeys.SiteMenuOverrides(orgId, siteId));
            await grain.SnoozeItemAsync(request.ItemDocumentId, request.Until, userId, request.Reason);

            var resolverGrain = grainFactory.GetGrain<IMenuContentResolverGrain>(GrainKeys.MenuContentResolver(orgId, siteId));
            await resolverGrain.InvalidateCacheAsync();

            return Results.NoContent();
        });

        siteGroup.MapDelete("/overrides/snooze/{itemDocumentId}", async (
            Guid orgId,
            Guid siteId,
            string itemDocumentId,
            [FromHeader(Name = "X-User-Id")] Guid? userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISiteMenuOverridesGrain>(GrainKeys.SiteMenuOverrides(orgId, siteId));
            await grain.UnsnoozeItemAsync(itemDocumentId, userId);

            var resolverGrain = grainFactory.GetGrain<IMenuContentResolverGrain>(GrainKeys.MenuContentResolver(orgId, siteId));
            await resolverGrain.InvalidateCacheAsync();

            return Results.NoContent();
        });

        siteGroup.MapPost("/overrides/availability-windows", async (
            Guid orgId,
            Guid siteId,
            [FromBody] AddAvailabilityWindowRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISiteMenuOverridesGrain>(GrainKeys.SiteMenuOverrides(orgId, siteId));

            var window = await grain.AddAvailabilityWindowAsync(new AddAvailabilityWindowCommand(
                Name: request.Name,
                StartTime: request.StartTime,
                EndTime: request.EndTime,
                DaysOfWeek: request.DaysOfWeek.ToList(),
                ItemDocumentIds: request.ItemDocumentIds?.ToList(),
                CategoryDocumentIds: request.CategoryDocumentIds?.ToList()));

            var resolverGrain = grainFactory.GetGrain<IMenuContentResolverGrain>(GrainKeys.MenuContentResolver(orgId, siteId));
            await resolverGrain.InvalidateCacheAsync();

            return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/menu/overrides/availability-windows/{window.WindowId}",
                ToAvailabilityWindowResponse(window));
        });

        // ========================================================================
        // Effective Menu Resolution Endpoints
        // ========================================================================

        siteGroup.MapGet("/effective", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] string channel,
            [FromQuery] string locale,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuContentResolverGrain>(GrainKeys.MenuContentResolver(orgId, siteId));

            var context = new MenuResolveContext
            {
                OrgId = orgId,
                SiteId = siteId,
                Channel = string.IsNullOrEmpty(channel) ? "pos" : channel,
                Locale = string.IsNullOrEmpty(locale) ? "en-US" : locale,
                AsOf = DateTimeOffset.UtcNow
            };

            var menu = await grain.ResolveAsync(context);

            return Results.Ok(Hal.Resource(ToEffectiveMenuResponse(menu), new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/menu/effective?channel={context.Channel}&locale={context.Locale}" }
            }));
        });

        siteGroup.MapPost("/preview", async (
            Guid orgId,
            Guid siteId,
            [FromBody] ResolveMenuRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuContentResolverGrain>(GrainKeys.MenuContentResolver(orgId, siteId));

            var context = new MenuResolveContext
            {
                OrgId = orgId,
                SiteId = siteId,
                Channel = request.Channel,
                Locale = request.Locale,
                AsOf = request.AsOf ?? DateTimeOffset.UtcNow,
                IncludeDraft = request.IncludeDraft,
                IncludeHidden = request.IncludeHidden,
                IncludeSnoozed = request.IncludeSnoozed
            };

            var options = new MenuPreviewOptions(
                ShowDraft: request.IncludeDraft,
                ShowHidden: request.IncludeHidden,
                ShowSnoozed: request.IncludeSnoozed);

            var menu = await grain.PreviewAsync(context, options);

            return Results.Ok(ToEffectiveMenuResponse(menu));
        });

        siteGroup.MapPost("/invalidate-cache", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuContentResolverGrain>(GrainKeys.MenuContentResolver(orgId, siteId));
            await grain.InvalidateCacheAsync();
            return Results.NoContent();
        });

        return app;
    }

    // ========================================================================
    // Response Mapping Helpers
    // ========================================================================

    private static MenuItemDocumentResponse ToResponse(MenuItemDocumentSnapshot snapshot) => new(
        DocumentId: snapshot.DocumentId,
        OrgId: snapshot.OrgId,
        CurrentVersion: snapshot.CurrentVersion,
        PublishedVersion: snapshot.PublishedVersion,
        DraftVersion: snapshot.DraftVersion,
        IsArchived: snapshot.IsArchived,
        CreatedAt: snapshot.CreatedAt,
        Published: snapshot.Published != null ? ToVersionResponse(snapshot.Published) : null,
        Draft: snapshot.Draft != null ? ToVersionResponse(snapshot.Draft) : null,
        Schedules: snapshot.Schedules.Select(ToScheduleResponse).ToList(),
        TotalVersions: snapshot.TotalVersions);

    private static MenuItemVersionResponse ToVersionResponse(MenuItemVersionSnapshot v) => new(
        VersionNumber: v.VersionNumber,
        CreatedAt: v.CreatedAt,
        CreatedBy: v.CreatedBy,
        ChangeNote: v.ChangeNote,
        Name: v.Name,
        Description: v.Description,
        Price: v.Price,
        ImageUrl: v.ImageUrl,
        CategoryId: v.CategoryId,
        AccountingGroupId: v.AccountingGroupId,
        RecipeId: v.RecipeId,
        Sku: v.Sku,
        TrackInventory: v.TrackInventory,
        ModifierBlockIds: v.ModifierBlockIds,
        TagIds: v.TagIds);

    private static ScheduledChangeResponse ToScheduleResponse(ScheduledChange s) => new(
        ScheduleId: s.ScheduleId,
        Version: s.VersionToActivate,
        ActivateAt: s.ActivateAt,
        DeactivateAt: s.DeactivateAt,
        Name: s.Name,
        IsActive: s.IsActive);

    private static MenuCategoryDocumentResponse ToCategoryResponse(MenuCategoryDocumentSnapshot snapshot) => new(
        DocumentId: snapshot.DocumentId,
        OrgId: snapshot.OrgId,
        CurrentVersion: snapshot.CurrentVersion,
        PublishedVersion: snapshot.PublishedVersion,
        DraftVersion: snapshot.DraftVersion,
        IsArchived: snapshot.IsArchived,
        CreatedAt: snapshot.CreatedAt,
        Published: snapshot.Published != null ? ToCategoryVersionResponse(snapshot.Published) : null,
        Draft: snapshot.Draft != null ? ToCategoryVersionResponse(snapshot.Draft) : null,
        Schedules: snapshot.Schedules.Select(ToScheduleResponse).ToList(),
        TotalVersions: snapshot.TotalVersions);

    private static MenuCategoryVersionResponse ToCategoryVersionResponse(MenuCategoryVersionSnapshot v) => new(
        VersionNumber: v.VersionNumber,
        CreatedAt: v.CreatedAt,
        CreatedBy: v.CreatedBy,
        ChangeNote: v.ChangeNote,
        Name: v.Name,
        Description: v.Description,
        Color: v.Color,
        IconUrl: v.IconUrl,
        DisplayOrder: v.DisplayOrder,
        ItemDocumentIds: v.ItemDocumentIds);

    private static ModifierBlockResponse ToModifierBlockResponse(ModifierBlockSnapshot snapshot) => new(
        BlockId: snapshot.BlockId,
        OrgId: snapshot.OrgId,
        CurrentVersion: snapshot.CurrentVersion,
        PublishedVersion: snapshot.PublishedVersion,
        DraftVersion: snapshot.DraftVersion,
        IsArchived: snapshot.IsArchived,
        CreatedAt: snapshot.CreatedAt,
        Published: snapshot.Published != null ? ToModifierBlockVersionResponse(snapshot.Published) : null,
        Draft: snapshot.Draft != null ? ToModifierBlockVersionResponse(snapshot.Draft) : null,
        TotalVersions: snapshot.TotalVersions,
        UsedByItemIds: snapshot.UsedByItemIds);

    private static ModifierBlockVersionResponse ToModifierBlockVersionResponse(ModifierBlockVersionSnapshot v) => new(
        VersionNumber: v.VersionNumber,
        CreatedAt: v.CreatedAt,
        CreatedBy: v.CreatedBy,
        ChangeNote: v.ChangeNote,
        Name: v.Name,
        SelectionRule: v.SelectionRule,
        MinSelections: v.MinSelections,
        MaxSelections: v.MaxSelections,
        IsRequired: v.IsRequired,
        Options: v.Options.Select(o => new ModifierOptionResponse(
            OptionId: o.OptionId,
            Name: o.Name,
            PriceAdjustment: o.PriceAdjustment,
            IsDefault: o.IsDefault,
            DisplayOrder: o.DisplayOrder,
            IsActive: o.IsActive)).ToList());

    private static ContentTagResponse ToContentTagResponse(ContentTagSnapshot snapshot) => new(
        TagId: snapshot.TagId,
        OrgId: snapshot.OrgId,
        Name: snapshot.Name,
        Category: snapshot.Category,
        IconUrl: snapshot.IconUrl,
        BadgeColor: snapshot.BadgeColor,
        DisplayOrder: snapshot.DisplayOrder,
        IsActive: snapshot.IsActive,
        ExternalTagId: snapshot.ExternalTagId,
        ExternalPlatform: snapshot.ExternalPlatform);

    private static SiteMenuOverridesResponse ToSiteOverridesResponse(SiteMenuOverridesSnapshot snapshot) => new(
        OrgId: snapshot.OrgId,
        SiteId: snapshot.SiteId,
        PriceOverrides: snapshot.PriceOverrides.Select(p => new SitePriceOverrideResponse(
            ItemDocumentId: p.ItemDocumentId,
            Price: p.Price,
            EffectiveFrom: p.EffectiveFrom,
            EffectiveUntil: p.EffectiveUntil,
            Reason: p.Reason)).ToList(),
        HiddenItemIds: snapshot.HiddenItemIds,
        HiddenCategoryIds: snapshot.HiddenCategoryIds,
        LocalItemIds: snapshot.LocalItemIds,
        LocalCategoryIds: snapshot.LocalCategoryIds,
        AvailabilityWindows: snapshot.AvailabilityWindows.Select(ToAvailabilityWindowResponse).ToList(),
        SnoozedItems: snapshot.SnoozedItems);

    private static AvailabilityWindowResponse ToAvailabilityWindowResponse(AvailabilityWindow w) => new(
        WindowId: w.WindowId,
        Name: w.Name,
        StartTime: w.StartTime,
        EndTime: w.EndTime,
        DaysOfWeek: w.DaysOfWeek,
        ItemDocumentIds: w.ItemDocumentIds,
        CategoryDocumentIds: w.CategoryDocumentIds,
        IsActive: w.IsActive);

    private static EffectiveMenuResponse ToEffectiveMenuResponse(EffectiveMenuState menu) => new(
        OrgId: menu.OrgId,
        SiteId: menu.SiteId,
        ResolvedAt: menu.ResolvedAt,
        Channel: menu.Channel,
        Locale: menu.Locale,
        Categories: menu.Categories.Select(c => new ResolvedMenuCategoryResponse(
            DocumentId: c.DocumentId,
            Version: c.Version,
            Name: c.Name,
            Description: c.Description,
            Color: c.Color,
            IconUrl: c.IconUrl,
            DisplayOrder: c.DisplayOrder,
            ItemCount: c.ItemCount)).ToList(),
        Items: menu.Items.Select(i => new ResolvedMenuItemResponse(
            DocumentId: i.DocumentId,
            Version: i.Version,
            Name: i.Name,
            Description: i.Description,
            KitchenName: i.KitchenName,
            Price: i.Price,
            ImageUrl: i.ImageUrl,
            CategoryId: i.CategoryId,
            CategoryName: i.CategoryName,
            Modifiers: i.Modifiers.Select(m => new ResolvedModifierBlockResponse(
                BlockId: m.BlockId,
                Name: m.Name,
                SelectionRule: m.SelectionRule,
                MinSelections: m.MinSelections,
                MaxSelections: m.MaxSelections,
                IsRequired: m.IsRequired,
                Options: m.Options.Select(o => new ResolvedModifierOptionResponse(
                    OptionId: o.OptionId,
                    Name: o.Name,
                    PriceAdjustment: o.PriceAdjustment,
                    IsDefault: o.IsDefault,
                    DisplayOrder: o.DisplayOrder)).ToList())).ToList(),
            Tags: i.Tags.Select(t => new ResolvedContentTagResponse(
                TagId: t.TagId,
                Name: t.Name,
                Category: t.Category,
                IconUrl: t.IconUrl,
                BadgeColor: t.BadgeColor)).ToList(),
            IsSnoozed: i.IsSnoozed,
            SnoozedUntil: i.SnoozedUntil,
            IsAvailable: i.IsAvailable,
            Sku: i.Sku,
            DisplayOrder: i.DisplayOrder)).ToList(),
        ETag: menu.ETag);
}
