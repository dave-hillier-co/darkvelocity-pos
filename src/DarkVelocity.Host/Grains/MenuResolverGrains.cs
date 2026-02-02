using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Menu Content Resolver Grain Implementation
// ============================================================================

/// <summary>
/// Grain for resolving effective menu content for a site.
/// Combines org-level documents with site-level overrides.
/// </summary>
public class MenuContentResolverGrain : Grain, IMenuContentResolverGrain
{
    private readonly IGrainFactory _grainFactory;
    private EffectiveMenuState? _cachedMenu;
    private DateTimeOffset _cacheExpiry = DateTimeOffset.MinValue;

    public MenuContentResolverGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async Task<EffectiveMenuState> ResolveAsync(MenuResolveContext context)
    {
        // Check cache
        if (_cachedMenu != null && _cacheExpiry > DateTimeOffset.UtcNow &&
            _cachedMenu.Channel == context.Channel && _cachedMenu.Locale == context.Locale)
        {
            return _cachedMenu;
        }

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var siteId = Guid.Parse(parts[1]);

        // Get registry for listing
        var registryGrain = _grainFactory.GetGrain<IMenuRegistryGrain>(GrainKeys.MenuRegistry(orgId));
        var categoryList = await registryGrain.GetCategoriesAsync(includeArchived: false);
        var itemList = await registryGrain.GetItemsAsync(includeArchived: false);

        // Get site overrides
        var overridesGrain = _grainFactory.GetGrain<ISiteMenuOverridesGrain>(GrainKeys.SiteMenuOverrides(orgId, siteId));
        var overrides = await overridesGrain.GetSnapshotAsync();

        var resolvedCategories = new List<ResolvedMenuCategory>();
        var resolvedItems = new List<ResolvedMenuItem>();

        // Resolve categories
        foreach (var categorySummary in categoryList.OrderBy(c => c.DisplayOrder))
        {
            // Check if hidden
            if (overrides.HiddenCategoryIds.Contains(categorySummary.DocumentId) && !context.IncludeHidden)
                continue;

            // Check availability windows
            if (!IsAvailableNow(categorySummary.DocumentId, overrides.AvailabilityWindows, context.AsOf, isCategory: true))
                continue;

            var categoryGrain = _grainFactory.GetGrain<IMenuCategoryDocumentGrain>(
                GrainKeys.MenuCategoryDocument(orgId, categorySummary.DocumentId));

            var categoryVersion = context.IncludeDraft
                ? await categoryGrain.GetDraftAsync() ?? await categoryGrain.GetPublishedAsync()
                : await categoryGrain.GetPublishedAsync();

            if (categoryVersion == null)
                continue;

            var localizedName = GetLocalizedName(categoryVersion.Translations, context.Locale, categoryVersion.Name);

            resolvedCategories.Add(new ResolvedMenuCategory
            {
                DocumentId = categorySummary.DocumentId,
                Version = categoryVersion.VersionNumber,
                Name = localizedName,
                Description = categoryVersion.Description,
                Color = categoryVersion.Color,
                IconUrl = categoryVersion.IconUrl,
                DisplayOrder = categoryVersion.DisplayOrder,
                ItemCount = categoryVersion.ItemDocumentIds.Count
            });
        }

        // Resolve items
        foreach (var itemSummary in itemList.OrderBy(i => i.CategoryId).ThenBy(i => i.Name))
        {
            // Check if hidden
            if (overrides.HiddenItemIds.Contains(itemSummary.DocumentId) && !context.IncludeHidden)
                continue;

            // Check if snoozed
            var isSnoozed = overrides.SnoozedItems.ContainsKey(itemSummary.DocumentId);
            DateTimeOffset? snoozedUntil = null;
            if (isSnoozed)
            {
                snoozedUntil = overrides.SnoozedItems[itemSummary.DocumentId];
                // Check if snooze expired
                if (snoozedUntil.HasValue && snoozedUntil.Value <= context.AsOf)
                {
                    isSnoozed = false;
                    snoozedUntil = null;
                }

                if (isSnoozed && !context.IncludeSnoozed)
                    continue;
            }

            // Check availability windows
            if (!IsAvailableNow(itemSummary.DocumentId, overrides.AvailabilityWindows, context.AsOf, isCategory: false))
                continue;

            var itemGrain = _grainFactory.GetGrain<IMenuItemDocumentGrain>(
                GrainKeys.MenuItemDocument(orgId, itemSummary.DocumentId));

            var itemVersion = context.IncludeDraft
                ? await itemGrain.GetDraftAsync() ?? await itemGrain.GetPublishedAsync()
                : await itemGrain.GetPublishedAsync();

            if (itemVersion == null)
                continue;

            // Apply price override if exists
            var price = itemVersion.Price;
            var priceOverride = overrides.PriceOverrides.FirstOrDefault(p =>
                p.ItemDocumentId == itemSummary.DocumentId &&
                (!p.EffectiveFrom.HasValue || p.EffectiveFrom.Value <= context.AsOf) &&
                (!p.EffectiveUntil.HasValue || p.EffectiveUntil.Value > context.AsOf));
            if (priceOverride != null)
            {
                price = priceOverride.Price;
            }

            var localizedName = GetLocalizedName(itemVersion.Translations, context.Locale, itemVersion.Name);
            var localizedDescription = GetLocalizedDescription(itemVersion.Translations, context.Locale, itemVersion.Description);

            // Get category name
            string? categoryName = null;
            if (itemVersion.CategoryId.HasValue)
            {
                var category = resolvedCategories.FirstOrDefault(c => c.DocumentId == itemVersion.CategoryId.Value.ToString());
                categoryName = category?.Name;
            }

            // Resolve modifiers
            var resolvedModifiers = new List<ResolvedModifierBlock>();
            foreach (var blockId in itemVersion.ModifierBlockIds)
            {
                var blockGrain = _grainFactory.GetGrain<IModifierBlockGrain>(GrainKeys.ModifierBlock(orgId, blockId));
                var blockVersion = await blockGrain.GetPublishedAsync();
                if (blockVersion != null)
                {
                    resolvedModifiers.Add(new ResolvedModifierBlock
                    {
                        BlockId = blockId,
                        Name = blockVersion.Name,
                        SelectionRule = blockVersion.SelectionRule,
                        MinSelections = blockVersion.MinSelections,
                        MaxSelections = blockVersion.MaxSelections,
                        IsRequired = blockVersion.IsRequired,
                        Options = blockVersion.Options
                            .Where(o => o.IsActive)
                            .OrderBy(o => o.DisplayOrder)
                            .Select(o => new ResolvedModifierOption
                            {
                                OptionId = o.OptionId,
                                Name = o.Name,
                                PriceAdjustment = o.PriceAdjustment,
                                IsDefault = o.IsDefault,
                                DisplayOrder = o.DisplayOrder
                            }).ToList()
                    });
                }
            }

            // Resolve tags
            var resolvedTags = new List<ResolvedContentTag>();
            foreach (var tagId in itemVersion.TagIds)
            {
                var tagGrain = _grainFactory.GetGrain<IContentTagGrain>(GrainKeys.ContentTag(orgId, tagId));
                if (await tagGrain.ExistsAsync())
                {
                    var tag = await tagGrain.GetSnapshotAsync();
                    if (tag.IsActive)
                    {
                        resolvedTags.Add(new ResolvedContentTag
                        {
                            TagId = tagId,
                            Name = tag.Name,
                            Category = tag.Category,
                            IconUrl = tag.IconUrl,
                            BadgeColor = tag.BadgeColor
                        });
                    }
                }
            }

            resolvedItems.Add(new ResolvedMenuItem
            {
                DocumentId = itemSummary.DocumentId,
                Version = itemVersion.VersionNumber,
                Name = localizedName,
                Description = localizedDescription,
                KitchenName = GetLocalizedKitchenName(itemVersion.Translations, context.Locale),
                Price = price,
                ImageUrl = itemVersion.ImageUrl,
                CategoryId = itemVersion.CategoryId?.ToString(),
                CategoryName = categoryName,
                Modifiers = resolvedModifiers,
                Tags = resolvedTags,
                IsSnoozed = isSnoozed,
                SnoozedUntil = snoozedUntil,
                IsAvailable = !isSnoozed,
                Sku = itemVersion.Sku,
                DisplayOrder = 0 // Could be enhanced with display order from category
            });
        }

        var etag = ComputeETag(resolvedCategories, resolvedItems);

        var result = new EffectiveMenuState
        {
            OrgId = orgId,
            SiteId = siteId,
            ResolvedAt = DateTimeOffset.UtcNow,
            Channel = context.Channel,
            Locale = context.Locale,
            Categories = resolvedCategories,
            Items = resolvedItems,
            ETag = etag,
            CacheUntil = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        // Cache result
        _cachedMenu = result;
        _cacheExpiry = result.CacheUntil ?? DateTimeOffset.UtcNow.AddMinutes(5);

        return result;
    }

    public async Task<EffectiveMenuState> PreviewAsync(MenuResolveContext context, MenuPreviewOptions options)
    {
        var previewContext = context with
        {
            IncludeDraft = options.ShowDraft,
            IncludeHidden = options.ShowHidden,
            IncludeSnoozed = options.ShowSnoozed
        };

        return await ResolveAsync(previewContext);
    }

    public async Task<ResolvedMenuItem?> ResolveItemAsync(string itemDocumentId, MenuResolveContext context)
    {
        var menu = await ResolveAsync(context);
        return menu.Items.FirstOrDefault(i => i.DocumentId == itemDocumentId);
    }

    public Task InvalidateCacheAsync()
    {
        _cachedMenu = null;
        _cacheExpiry = DateTimeOffset.MinValue;
        return Task.CompletedTask;
    }

    public async Task<bool> WouldBeActiveAsync(string documentId, int version, DateTimeOffset when)
    {
        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        var itemGrain = _grainFactory.GetGrain<IMenuItemDocumentGrain>(
            GrainKeys.MenuItemDocument(orgId, documentId));

        var previewVersion = await itemGrain.PreviewAtAsync(when);
        return previewVersion?.VersionNumber == version;
    }

    private static bool IsAvailableNow(string documentId, IReadOnlyList<AvailabilityWindow> windows, DateTimeOffset asOf, bool isCategory)
    {
        // If no windows define this item/category, it's always available
        var relevantWindows = windows.Where(w =>
            (isCategory && w.CategoryDocumentIds.Contains(documentId)) ||
            (!isCategory && w.ItemDocumentIds.Contains(documentId))).ToList();

        if (relevantWindows.Count == 0)
            return true;

        // Check if current time falls within any window
        var currentTime = TimeOnly.FromDateTime(asOf.DateTime);
        var currentDay = asOf.DayOfWeek;

        return relevantWindows.Any(w =>
            w.DaysOfWeek.Contains(currentDay) &&
            currentTime >= w.StartTime &&
            currentTime <= w.EndTime);
    }

    private static string GetLocalizedName(IReadOnlyDictionary<string, LocalizedStrings> translations, string locale, string defaultName)
    {
        if (translations.TryGetValue(locale, out var strings))
            return strings.Name;
        if (translations.TryGetValue("en-US", out strings))
            return strings.Name;
        return defaultName;
    }

    private static string? GetLocalizedDescription(IReadOnlyDictionary<string, LocalizedStrings> translations, string locale, string? defaultDescription)
    {
        if (translations.TryGetValue(locale, out var strings))
            return strings.Description;
        if (translations.TryGetValue("en-US", out strings))
            return strings.Description;
        return defaultDescription;
    }

    private static string? GetLocalizedKitchenName(IReadOnlyDictionary<string, LocalizedStrings> translations, string locale)
    {
        if (translations.TryGetValue(locale, out var strings))
            return strings.KitchenName;
        if (translations.TryGetValue("en-US", out strings))
            return strings.KitchenName;
        return null;
    }

    private static string ComputeETag(IReadOnlyList<ResolvedMenuCategory> categories, IReadOnlyList<ResolvedMenuItem> items)
    {
        var combined = string.Join("|",
            categories.Select(c => $"{c.DocumentId}:{c.Version}"),
            items.Select(i => $"{i.DocumentId}:{i.Version}:{i.Price}:{i.IsSnoozed}"));
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(combined));
        return Convert.ToBase64String(hash)[..16];
    }
}

// ============================================================================
// Menu Registry Grain Implementation
// ============================================================================

/// <summary>
/// Maintains a registry of all menu documents for efficient listing.
/// </summary>
[GenerateSerializer]
public sealed class MenuRegistryState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public bool IsCreated { get; set; }
    [Id(2)] public List<MenuItemDocumentSummary> Items { get; set; } = [];
    [Id(3)] public List<MenuCategoryDocumentSummary> Categories { get; set; } = [];
    [Id(4)] public List<string> ModifierBlockIds { get; set; } = [];
    [Id(5)] public Dictionary<string, (string Name, TagCategory Category)> Tags { get; set; } = [];
}

/// <summary>
/// Grain for maintaining a registry of menu documents.
/// </summary>
public class MenuRegistryGrain : Grain, IMenuRegistryGrain
{
    private readonly IPersistentState<MenuRegistryState> _state;

    public MenuRegistryGrain(
        [PersistentState("menuRegistry", "OrleansStorage")]
        IPersistentState<MenuRegistryState> state)
    {
        _state = state;
    }

    public async Task RegisterItemAsync(string documentId, string name, decimal price, string? categoryId)
    {
        await EnsureInitializedAsync();

        var existing = _state.State.Items.FirstOrDefault(i => i.DocumentId == documentId);
        if (existing != null)
        {
            _state.State.Items.Remove(existing);
        }

        _state.State.Items.Add(new MenuItemDocumentSummary(
            DocumentId: documentId,
            Name: name,
            Price: price,
            CategoryId: categoryId,
            HasDraft: false,
            IsArchived: false,
            PublishedVersion: 1,
            LastModified: DateTimeOffset.UtcNow));

        await _state.WriteStateAsync();
    }

    public async Task UpdateItemAsync(string documentId, string name, decimal price, string? categoryId, bool hasDraft, bool isArchived)
    {
        await EnsureInitializedAsync();

        var existing = _state.State.Items.FirstOrDefault(i => i.DocumentId == documentId);
        var publishedVersion = existing?.PublishedVersion ?? 1;

        if (existing != null)
        {
            _state.State.Items.Remove(existing);
        }

        _state.State.Items.Add(new MenuItemDocumentSummary(
            DocumentId: documentId,
            Name: name,
            Price: price,
            CategoryId: categoryId,
            HasDraft: hasDraft,
            IsArchived: isArchived,
            PublishedVersion: publishedVersion,
            LastModified: DateTimeOffset.UtcNow));

        await _state.WriteStateAsync();
    }

    public async Task UnregisterItemAsync(string documentId)
    {
        await EnsureInitializedAsync();

        _state.State.Items.RemoveAll(i => i.DocumentId == documentId);
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<MenuItemDocumentSummary>> GetItemsAsync(string? categoryId = null, bool includeArchived = false)
    {
        if (!_state.State.IsCreated)
            return Task.FromResult<IReadOnlyList<MenuItemDocumentSummary>>([]);

        var items = _state.State.Items.AsEnumerable();

        if (!includeArchived)
            items = items.Where(i => !i.IsArchived);

        if (categoryId != null)
            items = items.Where(i => i.CategoryId == categoryId);

        return Task.FromResult<IReadOnlyList<MenuItemDocumentSummary>>(items.ToList());
    }

    public async Task RegisterCategoryAsync(string documentId, string name, int displayOrder, string? color)
    {
        await EnsureInitializedAsync();

        var existing = _state.State.Categories.FirstOrDefault(c => c.DocumentId == documentId);
        if (existing != null)
        {
            _state.State.Categories.Remove(existing);
        }

        _state.State.Categories.Add(new MenuCategoryDocumentSummary(
            DocumentId: documentId,
            Name: name,
            DisplayOrder: displayOrder,
            Color: color,
            HasDraft: false,
            IsArchived: false,
            ItemCount: 0,
            LastModified: DateTimeOffset.UtcNow));

        await _state.WriteStateAsync();
    }

    public async Task UpdateCategoryAsync(string documentId, string name, int displayOrder, string? color, bool hasDraft, bool isArchived, int itemCount)
    {
        await EnsureInitializedAsync();

        var existing = _state.State.Categories.FirstOrDefault(c => c.DocumentId == documentId);
        if (existing != null)
        {
            _state.State.Categories.Remove(existing);
        }

        _state.State.Categories.Add(new MenuCategoryDocumentSummary(
            DocumentId: documentId,
            Name: name,
            DisplayOrder: displayOrder,
            Color: color,
            HasDraft: hasDraft,
            IsArchived: isArchived,
            ItemCount: itemCount,
            LastModified: DateTimeOffset.UtcNow));

        await _state.WriteStateAsync();
    }

    public async Task UnregisterCategoryAsync(string documentId)
    {
        await EnsureInitializedAsync();

        _state.State.Categories.RemoveAll(c => c.DocumentId == documentId);
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<MenuCategoryDocumentSummary>> GetCategoriesAsync(bool includeArchived = false)
    {
        if (!_state.State.IsCreated)
            return Task.FromResult<IReadOnlyList<MenuCategoryDocumentSummary>>([]);

        var categories = _state.State.Categories.AsEnumerable();

        if (!includeArchived)
            categories = categories.Where(c => !c.IsArchived);

        return Task.FromResult<IReadOnlyList<MenuCategoryDocumentSummary>>(
            categories.OrderBy(c => c.DisplayOrder).ToList());
    }

    public async Task RegisterModifierBlockAsync(string blockId, string name)
    {
        await EnsureInitializedAsync();

        if (!_state.State.ModifierBlockIds.Contains(blockId))
        {
            _state.State.ModifierBlockIds.Add(blockId);
            await _state.WriteStateAsync();
        }
    }

    public async Task UnregisterModifierBlockAsync(string blockId)
    {
        await EnsureInitializedAsync();

        _state.State.ModifierBlockIds.Remove(blockId);
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<string>> GetModifierBlockIdsAsync()
    {
        if (!_state.State.IsCreated)
            return Task.FromResult<IReadOnlyList<string>>([]);

        return Task.FromResult<IReadOnlyList<string>>(_state.State.ModifierBlockIds);
    }

    public async Task RegisterTagAsync(string tagId, string name, TagCategory category)
    {
        await EnsureInitializedAsync();

        _state.State.Tags[tagId] = (name, category);
        await _state.WriteStateAsync();
    }

    public async Task UnregisterTagAsync(string tagId)
    {
        await EnsureInitializedAsync();

        _state.State.Tags.Remove(tagId);
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<string>> GetTagIdsAsync(TagCategory? category = null)
    {
        if (!_state.State.IsCreated)
            return Task.FromResult<IReadOnlyList<string>>([]);

        var tags = _state.State.Tags.AsEnumerable();

        if (category.HasValue)
            tags = tags.Where(t => t.Value.Category == category.Value);

        return Task.FromResult<IReadOnlyList<string>>(tags.Select(t => t.Key).ToList());
    }

    private async Task EnsureInitializedAsync()
    {
        if (_state.State.IsCreated)
            return;

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0].Replace("menuregistry", ""));

        _state.State = new MenuRegistryState
        {
            OrgId = orgId,
            IsCreated = true
        };

        await _state.WriteStateAsync();
    }
}
