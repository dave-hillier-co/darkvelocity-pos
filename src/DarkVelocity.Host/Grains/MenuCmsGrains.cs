using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Menu Item Document Grain Implementation (Event Sourced)
// ============================================================================

/// <summary>
/// Event-sourced grain for menu item document management with versioning and workflow.
/// All state changes are recorded as events and can be replayed for full history.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class MenuItemDocumentGrain : JournaledGrain<MenuItemDocumentState, IMenuItemDocumentEvent>, IMenuItemDocumentGrain
{
    /// <summary>
    /// Applies domain events to mutate state. This is the core of event sourcing.
    /// </summary>
    protected override void TransitionState(MenuItemDocumentState state, IMenuItemDocumentEvent @event)
    {
        switch (@event)
        {
            case MenuItemDocumentInitialized e:
                state.OrgId = e.OrgId;
                state.DocumentId = e.DocumentId;
                state.IsCreated = true;
                state.CurrentVersion = 1;
                state.PublishedVersion = e.PublishImmediately ? 1 : null;
                state.DraftVersion = e.PublishImmediately ? null : 1;
                state.CreatedAt = e.OccurredAt;
                state.Versions.Add(new MenuItemVersionState
                {
                    VersionNumber = 1,
                    CreatedAt = e.OccurredAt,
                    CreatedBy = e.CreatedBy,
                    ChangeNote = "Initial creation",
                    Content = new LocalizedContent
                    {
                        DefaultLocale = e.Locale,
                        Translations = new Dictionary<string, LocalizedStrings>
                        {
                            [e.Locale] = new LocalizedStrings { Name = e.Name, Description = e.Description }
                        }
                    },
                    Pricing = new PricingInfo { BasePrice = e.Price },
                    Media = !string.IsNullOrEmpty(e.ImageUrl) ? new MediaInfo { PrimaryImageUrl = e.ImageUrl } : null,
                    CategoryId = e.CategoryId,
                    AccountingGroupId = e.AccountingGroupId,
                    RecipeId = e.RecipeId,
                    Sku = e.Sku,
                    TrackInventory = e.TrackInventory
                });
                break;

            case MenuItemDraftVersionCreated e:
                var baseVersion = state.DraftVersion.HasValue
                    ? state.Versions.First(v => v.VersionNumber == state.DraftVersion.Value)
                    : state.Versions.First(v => v.VersionNumber == state.PublishedVersion);
                var newVersion = new MenuItemVersionState
                {
                    VersionNumber = e.VersionNumber,
                    CreatedAt = e.OccurredAt,
                    CreatedBy = e.CreatedBy,
                    ChangeNote = e.ChangeNote,
                    Content = CloneContent(baseVersion.Content),
                    Pricing = new PricingInfo
                    {
                        BasePrice = e.Price ?? baseVersion.Pricing.BasePrice,
                        CostPrice = baseVersion.Pricing.CostPrice,
                        Currency = baseVersion.Pricing.Currency
                    },
                    Media = baseVersion.Media != null ? new MediaInfo
                    {
                        PrimaryImageUrl = e.ImageUrl ?? baseVersion.Media.PrimaryImageUrl,
                        ThumbnailUrl = baseVersion.Media.ThumbnailUrl,
                        AdditionalImageUrls = [.. baseVersion.Media.AdditionalImageUrls]
                    } : e.ImageUrl != null ? new MediaInfo { PrimaryImageUrl = e.ImageUrl } : null,
                    CategoryId = e.CategoryId ?? baseVersion.CategoryId,
                    AccountingGroupId = e.AccountingGroupId ?? baseVersion.AccountingGroupId,
                    RecipeId = e.RecipeId ?? baseVersion.RecipeId,
                    ModifierBlockIds = e.ModifierBlockIds ?? [.. baseVersion.ModifierBlockIds],
                    TagIds = e.TagIds ?? [.. baseVersion.TagIds],
                    Sku = e.Sku ?? baseVersion.Sku,
                    TrackInventory = e.TrackInventory ?? baseVersion.TrackInventory,
                    DisplayOrder = baseVersion.DisplayOrder
                };
                if (e.Name != null)
                    newVersion.Content.Translations[newVersion.Content.DefaultLocale].Name = e.Name;
                if (e.Description != null)
                    newVersion.Content.Translations[newVersion.Content.DefaultLocale].Description = e.Description;
                state.Versions.Add(newVersion);
                state.CurrentVersion = e.VersionNumber;
                state.DraftVersion = e.VersionNumber;
                break;

            case MenuItemDraftWasPublished e:
                state.PublishedVersion = e.PublishedVersion;
                state.DraftVersion = null;
                break;

            case MenuItemDraftDiscarded e:
                state.Versions.RemoveAll(v => v.VersionNumber == e.DiscardedVersion);
                state.DraftVersion = null;
                state.CurrentVersion = state.Versions.Max(v => v.VersionNumber);
                break;

            case MenuItemRevertedToVersion e:
                var targetVersion = state.Versions.First(v => v.VersionNumber == e.ToVersion);
                var revertedVersion = new MenuItemVersionState
                {
                    VersionNumber = e.NewVersionNumber,
                    CreatedAt = e.OccurredAt,
                    CreatedBy = e.RevertedBy,
                    ChangeNote = e.Reason ?? $"Reverted to version {e.ToVersion}",
                    Content = CloneContent(targetVersion.Content),
                    Pricing = new PricingInfo
                    {
                        BasePrice = targetVersion.Pricing.BasePrice,
                        CostPrice = targetVersion.Pricing.CostPrice,
                        Currency = targetVersion.Pricing.Currency
                    },
                    Media = targetVersion.Media,
                    CategoryId = targetVersion.CategoryId,
                    AccountingGroupId = targetVersion.AccountingGroupId,
                    RecipeId = targetVersion.RecipeId,
                    ModifierBlockIds = [.. targetVersion.ModifierBlockIds],
                    TagIds = [.. targetVersion.TagIds],
                    Sku = targetVersion.Sku,
                    TrackInventory = targetVersion.TrackInventory,
                    DisplayOrder = targetVersion.DisplayOrder
                };
                state.Versions.Add(revertedVersion);
                state.CurrentVersion = e.NewVersionNumber;
                state.PublishedVersion = e.NewVersionNumber;
                state.DraftVersion = null;
                break;

            case MenuItemTranslationAdded e:
                var versionForTranslation = state.DraftVersion.HasValue
                    ? state.Versions.First(v => v.VersionNumber == state.DraftVersion.Value)
                    : state.Versions.First(v => v.VersionNumber == state.PublishedVersion);
                versionForTranslation.Content.Translations[e.Locale] = new LocalizedStrings
                {
                    Name = e.Name,
                    Description = e.Description,
                    KitchenName = e.KitchenName
                };
                break;

            case MenuItemTranslationRemoved e:
                var versionForRemoval = state.DraftVersion.HasValue
                    ? state.Versions.First(v => v.VersionNumber == state.DraftVersion.Value)
                    : state.Versions.First(v => v.VersionNumber == state.PublishedVersion);
                versionForRemoval.Content.Translations.Remove(e.Locale);
                break;

            case MenuItemChangeWasScheduled e:
                state.Schedules.Add(new ScheduledChange
                {
                    ScheduleId = e.ScheduleId,
                    VersionToActivate = e.VersionToActivate,
                    ActivateAt = e.ActivateAt,
                    DeactivateAt = e.DeactivateAt,
                    Name = e.Name,
                    IsActive = true
                });
                break;

            case MenuItemScheduleWasCancelled e:
                state.Schedules.RemoveAll(s => s.ScheduleId == e.ScheduleId);
                break;

            case MenuItemDocumentWasArchived e:
                state.IsArchived = true;
                state.ArchivedAt = e.OccurredAt;
                break;

            case MenuItemDocumentWasRestored e:
                state.IsArchived = false;
                state.ArchivedAt = null;
                break;
        }
    }

    public async Task<MenuItemDocumentSnapshot> CreateAsync(CreateMenuItemDocumentCommand command)
    {
        if (State.IsCreated)
            throw new InvalidOperationException("Menu item document already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var documentId = parts[2];

        RaiseEvent(new MenuItemDocumentInitialized(
            DocumentId: documentId,
            OrgId: orgId,
            OccurredAt: DateTimeOffset.UtcNow,
            Name: command.Name,
            Price: command.Price,
            Description: command.Description,
            CategoryId: command.CategoryId,
            AccountingGroupId: command.AccountingGroupId,
            RecipeId: command.RecipeId,
            ImageUrl: command.ImageUrl,
            Sku: command.Sku,
            TrackInventory: command.TrackInventory,
            Locale: command.Locale,
            CreatedBy: command.CreatedBy,
            PublishImmediately: command.PublishImmediately
        ));

        await ConfirmEvents();
        return GetSnapshot();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(State.IsCreated);
    }

    public async Task<MenuItemVersionSnapshot> CreateDraftAsync(CreateMenuItemDraftCommand command)
    {
        EnsureInitialized();

        var newVersionNumber = State.CurrentVersion + 1;

        RaiseEvent(new MenuItemDraftVersionCreated(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            VersionNumber: newVersionNumber,
            CreatedBy: command.CreatedBy,
            ChangeNote: command.ChangeNote,
            Name: command.Name,
            Price: command.Price,
            Description: command.Description,
            ImageUrl: command.ImageUrl,
            CategoryId: command.CategoryId,
            AccountingGroupId: command.AccountingGroupId,
            RecipeId: command.RecipeId,
            Sku: command.Sku,
            TrackInventory: command.TrackInventory,
            ModifierBlockIds: command.ModifierBlockIds?.ToList(),
            TagIds: command.TagIds?.ToList()
        ));

        await ConfirmEvents();
        var newVersion = State.Versions.First(v => v.VersionNumber == newVersionNumber);
        return ToVersionSnapshot(newVersion);
    }

    public Task<MenuItemVersionSnapshot?> GetVersionAsync(int version)
    {
        EnsureInitialized();
        var v = State.Versions.FirstOrDefault(x => x.VersionNumber == version);
        return Task.FromResult(v != null ? ToVersionSnapshot(v) : null);
    }

    public Task<MenuItemVersionSnapshot?> GetPublishedAsync()
    {
        EnsureInitialized();
        if (!State.PublishedVersion.HasValue)
            return Task.FromResult<MenuItemVersionSnapshot?>(null);

        var v = State.Versions.First(x => x.VersionNumber == State.PublishedVersion.Value);
        return Task.FromResult<MenuItemVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<MenuItemVersionSnapshot?> GetDraftAsync()
    {
        EnsureInitialized();
        if (!State.DraftVersion.HasValue)
            return Task.FromResult<MenuItemVersionSnapshot?>(null);

        var v = State.Versions.First(x => x.VersionNumber == State.DraftVersion.Value);
        return Task.FromResult<MenuItemVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<IReadOnlyList<MenuItemVersionSnapshot>> GetVersionHistoryAsync(int skip = 0, int take = 20)
    {
        EnsureInitialized();
        var versions = State.Versions
            .OrderByDescending(v => v.VersionNumber)
            .Skip(skip)
            .Take(take)
            .Select(ToVersionSnapshot)
            .ToList();
        return Task.FromResult<IReadOnlyList<MenuItemVersionSnapshot>>(versions);
    }

    public async Task PublishDraftAsync(Guid? publishedBy = null, string? note = null)
    {
        EnsureInitialized();

        if (!State.DraftVersion.HasValue)
            throw new InvalidOperationException("No draft to publish");

        RaiseEvent(new MenuItemDraftWasPublished(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            PublishedVersion: State.DraftVersion.Value,
            PreviousPublishedVersion: State.PublishedVersion,
            PublishedBy: publishedBy,
            Note: note
        ));

        await ConfirmEvents();
    }

    public async Task DiscardDraftAsync()
    {
        EnsureInitialized();

        if (!State.DraftVersion.HasValue)
            return;

        RaiseEvent(new MenuItemDraftDiscarded(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            DiscardedVersion: State.DraftVersion.Value
        ));

        await ConfirmEvents();
    }

    public async Task RevertToVersionAsync(int version, Guid? revertedBy = null, string? reason = null)
    {
        EnsureInitialized();

        if (!State.Versions.Any(v => v.VersionNumber == version))
            throw new InvalidOperationException($"Version {version} not found");

        var newVersionNumber = State.CurrentVersion + 1;

        RaiseEvent(new MenuItemRevertedToVersion(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            FromVersion: State.PublishedVersion ?? 0,
            ToVersion: version,
            NewVersionNumber: newVersionNumber,
            RevertedBy: revertedBy,
            Reason: reason
        ));

        await ConfirmEvents();
    }

    public async Task AddTranslationAsync(AddMenuItemTranslationCommand command)
    {
        EnsureInitialized();

        var targetVersion = State.DraftVersion.HasValue
            ? State.Versions.First(v => v.VersionNumber == State.DraftVersion.Value)
            : State.Versions.First(v => v.VersionNumber == State.PublishedVersion);

        RaiseEvent(new MenuItemTranslationAdded(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            Locale: command.Locale,
            Name: command.Name,
            Description: command.Description,
            KitchenName: command.KitchenName
        ));

        await ConfirmEvents();
    }

    public async Task RemoveTranslationAsync(string locale)
    {
        EnsureInitialized();

        var targetVersion = State.DraftVersion.HasValue
            ? State.Versions.First(v => v.VersionNumber == State.DraftVersion.Value)
            : State.Versions.First(v => v.VersionNumber == State.PublishedVersion);

        if (locale == targetVersion.Content.DefaultLocale)
            throw new InvalidOperationException("Cannot remove default locale translation");

        RaiseEvent(new MenuItemTranslationRemoved(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            Locale: locale
        ));

        await ConfirmEvents();
    }

    public async Task<ScheduledChange> ScheduleChangeAsync(int version, DateTimeOffset activateAt, DateTimeOffset? deactivateAt = null, string? name = null)
    {
        EnsureInitialized();

        if (!State.Versions.Any(v => v.VersionNumber == version))
            throw new InvalidOperationException($"Version {version} not found");

        var scheduleId = Guid.NewGuid().ToString();

        RaiseEvent(new MenuItemChangeWasScheduled(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            ScheduleId: scheduleId,
            VersionToActivate: version,
            ActivateAt: activateAt,
            DeactivateAt: deactivateAt,
            Name: name
        ));

        await ConfirmEvents();
        return State.Schedules.First(s => s.ScheduleId == scheduleId);
    }

    public async Task CancelScheduleAsync(string scheduleId)
    {
        EnsureInitialized();

        var schedule = State.Schedules.FirstOrDefault(s => s.ScheduleId == scheduleId);
        if (schedule != null)
        {
            RaiseEvent(new MenuItemScheduleWasCancelled(
                DocumentId: State.DocumentId,
                OccurredAt: DateTimeOffset.UtcNow,
                ScheduleId: scheduleId
            ));

            await ConfirmEvents();
        }
    }

    public Task<IReadOnlyList<ScheduledChange>> GetSchedulesAsync()
    {
        EnsureInitialized();
        return Task.FromResult<IReadOnlyList<ScheduledChange>>(State.Schedules.Where(s => s.IsActive).ToList());
    }

    public async Task ArchiveAsync(Guid? archivedBy = null, string? reason = null)
    {
        EnsureInitialized();

        RaiseEvent(new MenuItemDocumentWasArchived(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            ArchivedBy: archivedBy,
            Reason: reason
        ));

        await ConfirmEvents();
    }

    public async Task RestoreAsync(Guid? restoredBy = null)
    {
        EnsureInitialized();

        RaiseEvent(new MenuItemDocumentWasRestored(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            RestoredBy: restoredBy
        ));

        await ConfirmEvents();
    }

    public Task<MenuItemDocumentSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(GetSnapshot());
    }

    public Task<MenuItemVersionSnapshot?> PreviewAtAsync(DateTimeOffset when)
    {
        EnsureInitialized();

        // Check schedules for what would be active at that time
        var activeSchedule = State.Schedules
            .Where(s => s.IsActive && s.ActivateAt <= when)
            .Where(s => !s.DeactivateAt.HasValue || s.DeactivateAt.Value > when)
            .OrderByDescending(s => s.ActivateAt)
            .FirstOrDefault();

        if (activeSchedule != null)
        {
            var v = State.Versions.FirstOrDefault(x => x.VersionNumber == activeSchedule.VersionToActivate);
            return Task.FromResult(v != null ? ToVersionSnapshot(v) : null);
        }

        // Return published version
        if (State.PublishedVersion.HasValue)
        {
            var v = State.Versions.First(x => x.VersionNumber == State.PublishedVersion.Value);
            return Task.FromResult<MenuItemVersionSnapshot?>(ToVersionSnapshot(v));
        }

        return Task.FromResult<MenuItemVersionSnapshot?>(null);
    }

    /// <summary>
    /// Gets the full event history for this document.
    /// This is the key benefit of using JournaledGrain - full audit trail.
    /// </summary>
    public async Task<IReadOnlyList<IMenuItemDocumentEvent>> GetEventHistoryAsync(int fromVersion = 0, int maxCount = 100)
    {
        var events = await RetrieveConfirmedEvents(fromVersion, maxCount);
        return events.ToList();
    }

    private MenuItemDocumentSnapshot GetSnapshot()
    {
        MenuItemVersionSnapshot? published = null;
        MenuItemVersionSnapshot? draft = null;

        if (State.PublishedVersion.HasValue)
        {
            var v = State.Versions.First(x => x.VersionNumber == State.PublishedVersion.Value);
            published = ToVersionSnapshot(v);
        }

        if (State.DraftVersion.HasValue)
        {
            var v = State.Versions.First(x => x.VersionNumber == State.DraftVersion.Value);
            draft = ToVersionSnapshot(v);
        }

        return new MenuItemDocumentSnapshot(
            DocumentId: State.DocumentId,
            OrgId: State.OrgId,
            CurrentVersion: State.CurrentVersion,
            PublishedVersion: State.PublishedVersion,
            DraftVersion: State.DraftVersion,
            IsArchived: State.IsArchived,
            CreatedAt: State.CreatedAt,
            Published: published,
            Draft: draft,
            Schedules: State.Schedules.Where(s => s.IsActive).ToList(),
            TotalVersions: State.Versions.Count);
    }

    private static MenuItemVersionSnapshot ToVersionSnapshot(MenuItemVersionState v)
    {
        var defaultStrings = v.Content.GetStrings();
        return new MenuItemVersionSnapshot(
            VersionNumber: v.VersionNumber,
            CreatedAt: v.CreatedAt,
            CreatedBy: v.CreatedBy,
            ChangeNote: v.ChangeNote,
            Name: defaultStrings.Name,
            Description: defaultStrings.Description,
            Price: v.Pricing.BasePrice,
            ImageUrl: v.Media?.PrimaryImageUrl,
            CategoryId: v.CategoryId,
            AccountingGroupId: v.AccountingGroupId,
            RecipeId: v.RecipeId,
            Sku: v.Sku,
            TrackInventory: v.TrackInventory,
            ModifierBlockIds: v.ModifierBlockIds,
            TagIds: v.TagIds,
            Translations: v.Content.Translations.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value));
    }

    private static LocalizedContent CloneContent(LocalizedContent source)
    {
        return new LocalizedContent
        {
            DefaultLocale = source.DefaultLocale,
            Translations = source.Translations.ToDictionary(
                kvp => kvp.Key,
                kvp => new LocalizedStrings
                {
                    Name = kvp.Value.Name,
                    Description = kvp.Value.Description,
                    KitchenName = kvp.Value.KitchenName
                })
        };
    }

    private void EnsureInitialized()
    {
        if (!State.IsCreated)
            throw new InvalidOperationException("Menu item document not initialized");
    }
}

// ============================================================================
// Menu Category Document Grain Implementation (Event Sourced)
// ============================================================================

/// <summary>
/// Event-sourced grain for menu category document management with versioning and workflow.
/// All state changes are recorded as events and can be replayed for full history.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class MenuCategoryDocumentGrain : JournaledGrain<MenuCategoryDocumentState, IMenuCategoryDocumentEvent>, IMenuCategoryDocumentGrain
{
    /// <summary>
    /// Applies domain events to mutate state.
    /// </summary>
    protected override void TransitionState(MenuCategoryDocumentState state, IMenuCategoryDocumentEvent @event)
    {
        switch (@event)
        {
            case MenuCategoryDocumentInitialized e:
                state.OrgId = e.OrgId;
                state.DocumentId = e.DocumentId;
                state.IsCreated = true;
                state.CurrentVersion = 1;
                state.PublishedVersion = e.PublishImmediately ? 1 : null;
                state.DraftVersion = e.PublishImmediately ? null : 1;
                state.CreatedAt = e.OccurredAt;
                state.Versions.Add(new MenuCategoryVersionState
                {
                    VersionNumber = 1,
                    CreatedAt = e.OccurredAt,
                    CreatedBy = e.CreatedBy,
                    ChangeNote = "Initial creation",
                    Content = new LocalizedContent
                    {
                        DefaultLocale = e.Locale,
                        Translations = new Dictionary<string, LocalizedStrings>
                        {
                            [e.Locale] = new LocalizedStrings { Name = e.Name, Description = e.Description }
                        }
                    },
                    Color = e.Color,
                    IconUrl = e.IconUrl,
                    DisplayOrder = e.DisplayOrder
                });
                break;

            case MenuCategoryDraftVersionCreated e:
                var baseVersion = state.DraftVersion.HasValue
                    ? state.Versions.First(v => v.VersionNumber == state.DraftVersion.Value)
                    : state.Versions.First(v => v.VersionNumber == state.PublishedVersion);
                var newVersion = new MenuCategoryVersionState
                {
                    VersionNumber = e.VersionNumber,
                    CreatedAt = e.OccurredAt,
                    CreatedBy = e.CreatedBy,
                    ChangeNote = e.ChangeNote,
                    Content = CloneContent(baseVersion.Content),
                    Color = e.Color ?? baseVersion.Color,
                    IconUrl = e.IconUrl ?? baseVersion.IconUrl,
                    DisplayOrder = e.DisplayOrder ?? baseVersion.DisplayOrder,
                    ItemDocumentIds = e.ItemDocumentIds ?? [.. baseVersion.ItemDocumentIds]
                };
                if (e.Name != null)
                    newVersion.Content.Translations[newVersion.Content.DefaultLocale].Name = e.Name;
                if (e.Description != null)
                    newVersion.Content.Translations[newVersion.Content.DefaultLocale].Description = e.Description;
                state.Versions.Add(newVersion);
                state.CurrentVersion = e.VersionNumber;
                state.DraftVersion = e.VersionNumber;
                break;

            case MenuCategoryDraftWasPublished e:
                state.PublishedVersion = e.PublishedVersion;
                state.DraftVersion = null;
                break;

            case MenuCategoryDraftDiscarded e:
                state.Versions.RemoveAll(v => v.VersionNumber == e.DiscardedVersion);
                state.DraftVersion = null;
                state.CurrentVersion = state.Versions.Max(v => v.VersionNumber);
                break;

            case MenuCategoryRevertedToVersion e:
                var targetVersion = state.Versions.First(v => v.VersionNumber == e.ToVersion);
                var revertedVersion = new MenuCategoryVersionState
                {
                    VersionNumber = e.NewVersionNumber,
                    CreatedAt = e.OccurredAt,
                    CreatedBy = e.RevertedBy,
                    ChangeNote = e.Reason ?? $"Reverted to version {e.ToVersion}",
                    Content = CloneContent(targetVersion.Content),
                    Color = targetVersion.Color,
                    IconUrl = targetVersion.IconUrl,
                    DisplayOrder = targetVersion.DisplayOrder,
                    ItemDocumentIds = [.. targetVersion.ItemDocumentIds]
                };
                state.Versions.Add(revertedVersion);
                state.CurrentVersion = e.NewVersionNumber;
                state.PublishedVersion = e.NewVersionNumber;
                state.DraftVersion = null;
                break;

            case MenuCategoryItemAdded e:
                var versionForAdd = state.DraftVersion.HasValue
                    ? state.Versions.First(v => v.VersionNumber == state.DraftVersion.Value)
                    : state.Versions.First(v => v.VersionNumber == state.PublishedVersion);
                if (!versionForAdd.ItemDocumentIds.Contains(e.ItemDocumentId))
                    versionForAdd.ItemDocumentIds.Add(e.ItemDocumentId);
                break;

            case MenuCategoryItemRemoved e:
                var versionForRemove = state.DraftVersion.HasValue
                    ? state.Versions.First(v => v.VersionNumber == state.DraftVersion.Value)
                    : state.Versions.First(v => v.VersionNumber == state.PublishedVersion);
                versionForRemove.ItemDocumentIds.Remove(e.ItemDocumentId);
                break;

            case MenuCategoryItemsReordered e:
                var versionForReorder = state.DraftVersion.HasValue
                    ? state.Versions.First(v => v.VersionNumber == state.DraftVersion.Value)
                    : state.Versions.First(v => v.VersionNumber == state.PublishedVersion);
                versionForReorder.ItemDocumentIds = e.ItemDocumentIds;
                break;

            case MenuCategoryChangeWasScheduled e:
                state.Schedules.Add(new ScheduledChange
                {
                    ScheduleId = e.ScheduleId,
                    VersionToActivate = e.VersionToActivate,
                    ActivateAt = e.ActivateAt,
                    DeactivateAt = e.DeactivateAt,
                    Name = e.Name,
                    IsActive = true
                });
                break;

            case MenuCategoryScheduleWasCancelled e:
                state.Schedules.RemoveAll(s => s.ScheduleId == e.ScheduleId);
                break;

            case MenuCategoryDocumentWasArchived e:
                state.IsArchived = true;
                break;

            case MenuCategoryDocumentWasRestored e:
                state.IsArchived = false;
                break;
        }
    }

    public async Task<MenuCategoryDocumentSnapshot> CreateAsync(CreateMenuCategoryDocumentCommand command)
    {
        if (State.IsCreated)
            throw new InvalidOperationException("Menu category document already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var documentId = parts[2];

        RaiseEvent(new MenuCategoryDocumentInitialized(
            DocumentId: documentId,
            OrgId: orgId,
            OccurredAt: DateTimeOffset.UtcNow,
            Name: command.Name,
            DisplayOrder: command.DisplayOrder,
            Description: command.Description,
            Color: command.Color,
            IconUrl: command.IconUrl,
            Locale: command.Locale,
            CreatedBy: command.CreatedBy,
            PublishImmediately: command.PublishImmediately
        ));

        await ConfirmEvents();
        return GetSnapshot();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(State.IsCreated);
    }

    public async Task<MenuCategoryVersionSnapshot> CreateDraftAsync(CreateMenuCategoryDraftCommand command)
    {
        EnsureInitialized();
        var newVersionNumber = State.CurrentVersion + 1;

        RaiseEvent(new MenuCategoryDraftVersionCreated(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            VersionNumber: newVersionNumber,
            CreatedBy: command.CreatedBy,
            ChangeNote: command.ChangeNote,
            Name: command.Name,
            DisplayOrder: command.DisplayOrder,
            Description: command.Description,
            Color: command.Color,
            IconUrl: command.IconUrl,
            ItemDocumentIds: command.ItemDocumentIds?.ToList()
        ));

        await ConfirmEvents();
        var newVersion = State.Versions.First(v => v.VersionNumber == newVersionNumber);
        return ToVersionSnapshot(newVersion);
    }

    public Task<MenuCategoryVersionSnapshot?> GetVersionAsync(int version)
    {
        EnsureInitialized();
        var v = State.Versions.FirstOrDefault(x => x.VersionNumber == version);
        return Task.FromResult(v != null ? ToVersionSnapshot(v) : null);
    }

    public Task<MenuCategoryVersionSnapshot?> GetPublishedAsync()
    {
        EnsureInitialized();
        if (!State.PublishedVersion.HasValue)
            return Task.FromResult<MenuCategoryVersionSnapshot?>(null);

        var v = State.Versions.First(x => x.VersionNumber == State.PublishedVersion.Value);
        return Task.FromResult<MenuCategoryVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<MenuCategoryVersionSnapshot?> GetDraftAsync()
    {
        EnsureInitialized();
        if (!State.DraftVersion.HasValue)
            return Task.FromResult<MenuCategoryVersionSnapshot?>(null);

        var v = State.Versions.First(x => x.VersionNumber == State.DraftVersion.Value);
        return Task.FromResult<MenuCategoryVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<IReadOnlyList<MenuCategoryVersionSnapshot>> GetVersionHistoryAsync(int skip = 0, int take = 20)
    {
        EnsureInitialized();
        var versions = State.Versions
            .OrderByDescending(v => v.VersionNumber)
            .Skip(skip)
            .Take(take)
            .Select(ToVersionSnapshot)
            .ToList();
        return Task.FromResult<IReadOnlyList<MenuCategoryVersionSnapshot>>(versions);
    }

    public async Task PublishDraftAsync(Guid? publishedBy = null, string? note = null)
    {
        EnsureInitialized();

        if (!State.DraftVersion.HasValue)
            throw new InvalidOperationException("No draft to publish");

        RaiseEvent(new MenuCategoryDraftWasPublished(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            PublishedVersion: State.DraftVersion.Value,
            PreviousPublishedVersion: State.PublishedVersion,
            PublishedBy: publishedBy,
            Note: note
        ));

        await ConfirmEvents();
    }

    public async Task DiscardDraftAsync()
    {
        EnsureInitialized();

        if (!State.DraftVersion.HasValue)
            return;

        RaiseEvent(new MenuCategoryDraftDiscarded(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            DiscardedVersion: State.DraftVersion.Value
        ));

        await ConfirmEvents();
    }

    public async Task RevertToVersionAsync(int version, Guid? revertedBy = null, string? reason = null)
    {
        EnsureInitialized();

        if (!State.Versions.Any(v => v.VersionNumber == version))
            throw new InvalidOperationException($"Version {version} not found");

        var newVersionNumber = State.CurrentVersion + 1;

        RaiseEvent(new MenuCategoryRevertedToVersion(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            FromVersion: State.PublishedVersion ?? 0,
            ToVersion: version,
            NewVersionNumber: newVersionNumber,
            RevertedBy: revertedBy,
            Reason: reason
        ));

        await ConfirmEvents();
    }

    public async Task AddItemAsync(string itemDocumentId)
    {
        EnsureInitialized();

        RaiseEvent(new MenuCategoryItemAdded(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            ItemDocumentId: itemDocumentId
        ));

        await ConfirmEvents();
    }

    public async Task RemoveItemAsync(string itemDocumentId)
    {
        EnsureInitialized();

        RaiseEvent(new MenuCategoryItemRemoved(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            ItemDocumentId: itemDocumentId
        ));

        await ConfirmEvents();
    }

    public async Task ReorderItemsAsync(IReadOnlyList<string> itemDocumentIds)
    {
        EnsureInitialized();

        RaiseEvent(new MenuCategoryItemsReordered(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            ItemDocumentIds: itemDocumentIds.ToList()
        ));

        await ConfirmEvents();
    }

    public async Task<ScheduledChange> ScheduleChangeAsync(int version, DateTimeOffset activateAt, DateTimeOffset? deactivateAt = null, string? name = null)
    {
        EnsureInitialized();

        if (!State.Versions.Any(v => v.VersionNumber == version))
            throw new InvalidOperationException($"Version {version} not found");

        var scheduleId = Guid.NewGuid().ToString();

        RaiseEvent(new MenuCategoryChangeWasScheduled(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            ScheduleId: scheduleId,
            VersionToActivate: version,
            ActivateAt: activateAt,
            DeactivateAt: deactivateAt,
            Name: name
        ));

        await ConfirmEvents();
        return State.Schedules.First(s => s.ScheduleId == scheduleId);
    }

    public async Task CancelScheduleAsync(string scheduleId)
    {
        EnsureInitialized();

        var schedule = State.Schedules.FirstOrDefault(s => s.ScheduleId == scheduleId);
        if (schedule != null)
        {
            RaiseEvent(new MenuCategoryScheduleWasCancelled(
                DocumentId: State.DocumentId,
                OccurredAt: DateTimeOffset.UtcNow,
                ScheduleId: scheduleId
            ));
            await ConfirmEvents();
        }
    }

    public async Task ArchiveAsync(Guid? archivedBy = null, string? reason = null)
    {
        EnsureInitialized();

        RaiseEvent(new MenuCategoryDocumentWasArchived(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            ArchivedBy: archivedBy,
            Reason: reason
        ));

        await ConfirmEvents();
    }

    public async Task RestoreAsync(Guid? restoredBy = null)
    {
        EnsureInitialized();

        RaiseEvent(new MenuCategoryDocumentWasRestored(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            RestoredBy: restoredBy
        ));

        await ConfirmEvents();
    }

    public Task<MenuCategoryDocumentSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(GetSnapshot());
    }

    /// <summary>
    /// Gets the full event history for this category document.
    /// </summary>
    public async Task<IReadOnlyList<IMenuCategoryDocumentEvent>> GetEventHistoryAsync(int fromVersion = 0, int maxCount = 100)
    {
        var events = await RetrieveConfirmedEvents(fromVersion, maxCount);
        return events.ToList();
    }

    private MenuCategoryDocumentSnapshot GetSnapshot()
    {
        MenuCategoryVersionSnapshot? published = null;
        MenuCategoryVersionSnapshot? draft = null;

        if (State.PublishedVersion.HasValue)
        {
            var v = State.Versions.First(x => x.VersionNumber == State.PublishedVersion.Value);
            published = ToVersionSnapshot(v);
        }

        if (State.DraftVersion.HasValue)
        {
            var v = State.Versions.First(x => x.VersionNumber == State.DraftVersion.Value);
            draft = ToVersionSnapshot(v);
        }

        return new MenuCategoryDocumentSnapshot(
            DocumentId: State.DocumentId,
            OrgId: State.OrgId,
            CurrentVersion: State.CurrentVersion,
            PublishedVersion: State.PublishedVersion,
            DraftVersion: State.DraftVersion,
            IsArchived: State.IsArchived,
            CreatedAt: State.CreatedAt,
            Published: published,
            Draft: draft,
            Schedules: State.Schedules.Where(s => s.IsActive).ToList(),
            TotalVersions: State.Versions.Count);
    }

    private static MenuCategoryVersionSnapshot ToVersionSnapshot(MenuCategoryVersionState v)
    {
        var defaultStrings = v.Content.GetStrings();
        return new MenuCategoryVersionSnapshot(
            VersionNumber: v.VersionNumber,
            CreatedAt: v.CreatedAt,
            CreatedBy: v.CreatedBy,
            ChangeNote: v.ChangeNote,
            Name: defaultStrings.Name,
            Description: defaultStrings.Description,
            Color: v.Color,
            IconUrl: v.IconUrl,
            DisplayOrder: v.DisplayOrder,
            ItemDocumentIds: v.ItemDocumentIds,
            Translations: v.Content.Translations.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value));
    }

    private static LocalizedContent CloneContent(LocalizedContent source)
    {
        return new LocalizedContent
        {
            DefaultLocale = source.DefaultLocale,
            Translations = source.Translations.ToDictionary(
                kvp => kvp.Key,
                kvp => new LocalizedStrings
                {
                    Name = kvp.Value.Name,
                    Description = kvp.Value.Description,
                    KitchenName = kvp.Value.KitchenName
                })
        };
    }

    private void EnsureInitialized()
    {
        if (!State.IsCreated)
            throw new InvalidOperationException("Menu category document not initialized");
    }
}

// ============================================================================
// Modifier Block Grain Implementation (Event Sourced)
// ============================================================================

/// <summary>
/// Event-sourced grain for reusable modifier block management.
/// All state changes are recorded as events and can be replayed for full history.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class ModifierBlockGrain : JournaledGrain<ModifierBlockState, IModifierBlockEvent>, IModifierBlockGrain
{
    /// <summary>
    /// Applies domain events to mutate state.
    /// </summary>
    protected override void TransitionState(ModifierBlockState state, IModifierBlockEvent @event)
    {
        switch (@event)
        {
            case ModifierBlockInitialized e:
                state.OrgId = e.OrgId;
                state.BlockId = e.BlockId;
                state.IsCreated = true;
                state.CurrentVersion = 1;
                state.PublishedVersion = e.PublishImmediately ? 1 : null;
                state.DraftVersion = e.PublishImmediately ? null : 1;
                state.CreatedAt = e.OccurredAt;
                var options = e.Options?.Select((o, i) => new ModifierOptionState
                {
                    OptionId = o.OptionId,
                    Content = new LocalizedContent
                    {
                        Translations = new Dictionary<string, LocalizedStrings>
                        {
                            ["en-US"] = new LocalizedStrings { Name = o.Name }
                        }
                    },
                    PriceAdjustment = o.PriceAdjustment,
                    IsDefault = o.IsDefault,
                    DisplayOrder = o.DisplayOrder == 0 ? i : o.DisplayOrder,
                    IsActive = true,
                    ServingSize = o.ServingSize,
                    ServingUnit = o.ServingUnit,
                    InventoryItemId = o.InventoryItemId
                }).ToList() ?? [];
                state.Versions.Add(new ModifierBlockVersionState
                {
                    VersionNumber = 1,
                    CreatedAt = e.OccurredAt,
                    CreatedBy = e.CreatedBy,
                    ChangeNote = "Initial creation",
                    Content = new LocalizedContent
                    {
                        Translations = new Dictionary<string, LocalizedStrings>
                        {
                            ["en-US"] = new LocalizedStrings { Name = e.Name }
                        }
                    },
                    SelectionRule = e.SelectionRule,
                    MinSelections = e.MinSelections,
                    MaxSelections = e.MaxSelections,
                    IsRequired = e.IsRequired,
                    Options = options
                });
                break;

            case ModifierBlockDraftVersionCreated e:
                var baseVersion = state.DraftVersion.HasValue
                    ? state.Versions.First(v => v.VersionNumber == state.DraftVersion.Value)
                    : state.Versions.First(v => v.VersionNumber == state.PublishedVersion);
                var draftOptions = e.Options?.Select((o, i) => new ModifierOptionState
                {
                    OptionId = o.OptionId,
                    Content = new LocalizedContent
                    {
                        Translations = new Dictionary<string, LocalizedStrings>
                        {
                            ["en-US"] = new LocalizedStrings { Name = o.Name }
                        }
                    },
                    PriceAdjustment = o.PriceAdjustment,
                    IsDefault = o.IsDefault,
                    DisplayOrder = o.DisplayOrder == 0 ? i : o.DisplayOrder,
                    IsActive = true,
                    ServingSize = o.ServingSize,
                    ServingUnit = o.ServingUnit,
                    InventoryItemId = o.InventoryItemId
                }).ToList() ?? baseVersion.Options.Select(o => new ModifierOptionState
                {
                    OptionId = o.OptionId,
                    Content = o.Content,
                    PriceAdjustment = o.PriceAdjustment,
                    IsDefault = o.IsDefault,
                    DisplayOrder = o.DisplayOrder,
                    IsActive = o.IsActive,
                    ServingSize = o.ServingSize,
                    ServingUnit = o.ServingUnit,
                    InventoryItemId = o.InventoryItemId
                }).ToList();
                state.Versions.Add(new ModifierBlockVersionState
                {
                    VersionNumber = e.VersionNumber,
                    CreatedAt = e.OccurredAt,
                    CreatedBy = e.CreatedBy,
                    ChangeNote = e.ChangeNote,
                    Content = new LocalizedContent
                    {
                        Translations = new Dictionary<string, LocalizedStrings>
                        {
                            ["en-US"] = new LocalizedStrings { Name = e.Name ?? baseVersion.Content.GetStrings().Name }
                        }
                    },
                    SelectionRule = e.SelectionRule ?? baseVersion.SelectionRule,
                    MinSelections = e.MinSelections ?? baseVersion.MinSelections,
                    MaxSelections = e.MaxSelections ?? baseVersion.MaxSelections,
                    IsRequired = e.IsRequired ?? baseVersion.IsRequired,
                    Options = draftOptions
                });
                state.CurrentVersion = e.VersionNumber;
                state.DraftVersion = e.VersionNumber;
                break;

            case ModifierBlockDraftWasPublished e:
                state.PublishedVersion = e.PublishedVersion;
                state.DraftVersion = null;
                break;

            case ModifierBlockDraftDiscarded e:
                state.Versions.RemoveAll(v => v.VersionNumber == e.DiscardedVersion);
                state.DraftVersion = null;
                state.CurrentVersion = state.Versions.Max(v => v.VersionNumber);
                break;

            case ModifierBlockUsageRegistered e:
                if (!state.UsedByItemIds.Contains(e.ItemDocumentId))
                    state.UsedByItemIds.Add(e.ItemDocumentId);
                break;

            case ModifierBlockUsageUnregistered e:
                state.UsedByItemIds.Remove(e.ItemDocumentId);
                break;

            case ModifierBlockWasArchived e:
                state.IsArchived = true;
                break;

            case ModifierBlockWasRestored e:
                state.IsArchived = false;
                break;
        }
    }

    public async Task<ModifierBlockSnapshot> CreateAsync(CreateModifierBlockCommand command)
    {
        if (State.IsCreated)
            throw new InvalidOperationException("Modifier block already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var blockId = parts[2];

        var optionData = command.Options?.Select((o, i) => new ModifierOptionData(
            OptionId: Guid.NewGuid().ToString(),
            Name: o.Name,
            PriceAdjustment: o.PriceAdjustment,
            IsDefault: o.IsDefault,
            DisplayOrder: o.DisplayOrder == 0 ? i : o.DisplayOrder,
            ServingSize: o.ServingSize,
            ServingUnit: o.ServingUnit,
            InventoryItemId: o.InventoryItemId
        )).ToList();

        RaiseEvent(new ModifierBlockInitialized(
            BlockId: blockId,
            OrgId: orgId,
            OccurredAt: DateTimeOffset.UtcNow,
            Name: command.Name,
            SelectionRule: command.SelectionRule,
            MinSelections: command.MinSelections,
            MaxSelections: command.MaxSelections,
            IsRequired: command.IsRequired,
            Options: optionData,
            CreatedBy: command.CreatedBy,
            PublishImmediately: command.PublishImmediately
        ));

        await ConfirmEvents();
        return GetSnapshot();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(State.IsCreated);
    }

    public async Task<ModifierBlockVersionSnapshot> CreateDraftAsync(CreateModifierBlockDraftCommand command)
    {
        EnsureInitialized();
        var newVersionNumber = State.CurrentVersion + 1;

        var optionData = command.Options?.Select((o, i) => new ModifierOptionData(
            OptionId: Guid.NewGuid().ToString(),
            Name: o.Name,
            PriceAdjustment: o.PriceAdjustment,
            IsDefault: o.IsDefault,
            DisplayOrder: o.DisplayOrder == 0 ? i : o.DisplayOrder,
            ServingSize: o.ServingSize,
            ServingUnit: o.ServingUnit,
            InventoryItemId: o.InventoryItemId
        )).ToList();

        RaiseEvent(new ModifierBlockDraftVersionCreated(
            BlockId: State.BlockId,
            OccurredAt: DateTimeOffset.UtcNow,
            VersionNumber: newVersionNumber,
            CreatedBy: command.CreatedBy,
            ChangeNote: command.ChangeNote,
            Name: command.Name,
            SelectionRule: command.SelectionRule,
            MinSelections: command.MinSelections,
            MaxSelections: command.MaxSelections,
            IsRequired: command.IsRequired,
            Options: optionData
        ));

        await ConfirmEvents();
        var newVersion = State.Versions.First(v => v.VersionNumber == newVersionNumber);
        return ToVersionSnapshot(newVersion);
    }

    public Task<ModifierBlockVersionSnapshot?> GetVersionAsync(int version)
    {
        EnsureInitialized();
        var v = State.Versions.FirstOrDefault(x => x.VersionNumber == version);
        return Task.FromResult(v != null ? ToVersionSnapshot(v) : null);
    }

    public Task<ModifierBlockVersionSnapshot?> GetPublishedAsync()
    {
        EnsureInitialized();
        if (!State.PublishedVersion.HasValue)
            return Task.FromResult<ModifierBlockVersionSnapshot?>(null);

        var v = State.Versions.First(x => x.VersionNumber == State.PublishedVersion.Value);
        return Task.FromResult<ModifierBlockVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<ModifierBlockVersionSnapshot?> GetDraftAsync()
    {
        EnsureInitialized();
        if (!State.DraftVersion.HasValue)
            return Task.FromResult<ModifierBlockVersionSnapshot?>(null);

        var v = State.Versions.First(x => x.VersionNumber == State.DraftVersion.Value);
        return Task.FromResult<ModifierBlockVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public async Task PublishDraftAsync(Guid? publishedBy = null, string? note = null)
    {
        EnsureInitialized();

        if (!State.DraftVersion.HasValue)
            throw new InvalidOperationException("No draft to publish");

        RaiseEvent(new ModifierBlockDraftWasPublished(
            BlockId: State.BlockId,
            OccurredAt: DateTimeOffset.UtcNow,
            PublishedVersion: State.DraftVersion.Value,
            PreviousPublishedVersion: State.PublishedVersion,
            PublishedBy: publishedBy,
            Note: note
        ));

        await ConfirmEvents();
    }

    public async Task DiscardDraftAsync()
    {
        EnsureInitialized();

        if (!State.DraftVersion.HasValue)
            return;

        RaiseEvent(new ModifierBlockDraftDiscarded(
            BlockId: State.BlockId,
            OccurredAt: DateTimeOffset.UtcNow,
            DiscardedVersion: State.DraftVersion.Value
        ));

        await ConfirmEvents();
    }

    public async Task RegisterUsageAsync(string itemDocumentId)
    {
        EnsureInitialized();

        RaiseEvent(new ModifierBlockUsageRegistered(
            BlockId: State.BlockId,
            OccurredAt: DateTimeOffset.UtcNow,
            ItemDocumentId: itemDocumentId
        ));

        await ConfirmEvents();
    }

    public async Task UnregisterUsageAsync(string itemDocumentId)
    {
        EnsureInitialized();

        RaiseEvent(new ModifierBlockUsageUnregistered(
            BlockId: State.BlockId,
            OccurredAt: DateTimeOffset.UtcNow,
            ItemDocumentId: itemDocumentId
        ));

        await ConfirmEvents();
    }

    public Task<IReadOnlyList<string>> GetUsageAsync()
    {
        EnsureInitialized();
        return Task.FromResult<IReadOnlyList<string>>(State.UsedByItemIds);
    }

    public async Task ArchiveAsync(Guid? archivedBy = null, string? reason = null)
    {
        EnsureInitialized();

        if (State.UsedByItemIds.Count > 0)
            throw new InvalidOperationException($"Cannot archive modifier block that is used by {State.UsedByItemIds.Count} items");

        RaiseEvent(new ModifierBlockWasArchived(
            BlockId: State.BlockId,
            OccurredAt: DateTimeOffset.UtcNow,
            ArchivedBy: archivedBy,
            Reason: reason
        ));

        await ConfirmEvents();
    }

    public async Task RestoreAsync(Guid? restoredBy = null)
    {
        EnsureInitialized();

        RaiseEvent(new ModifierBlockWasRestored(
            BlockId: State.BlockId,
            OccurredAt: DateTimeOffset.UtcNow,
            RestoredBy: restoredBy
        ));

        await ConfirmEvents();
    }

    public Task<ModifierBlockSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(GetSnapshot());
    }

    /// <summary>
    /// Gets the full event history for this modifier block.
    /// </summary>
    public async Task<IReadOnlyList<IModifierBlockEvent>> GetEventHistoryAsync(int fromVersion = 0, int maxCount = 100)
    {
        var events = await RetrieveConfirmedEvents(fromVersion, maxCount);
        return events.ToList();
    }

    private ModifierBlockSnapshot GetSnapshot()
    {
        ModifierBlockVersionSnapshot? published = null;
        ModifierBlockVersionSnapshot? draft = null;

        if (State.PublishedVersion.HasValue)
        {
            var v = State.Versions.First(x => x.VersionNumber == State.PublishedVersion.Value);
            published = ToVersionSnapshot(v);
        }

        if (State.DraftVersion.HasValue)
        {
            var v = State.Versions.First(x => x.VersionNumber == State.DraftVersion.Value);
            draft = ToVersionSnapshot(v);
        }

        return new ModifierBlockSnapshot(
            BlockId: State.BlockId,
            OrgId: State.OrgId,
            CurrentVersion: State.CurrentVersion,
            PublishedVersion: State.PublishedVersion,
            DraftVersion: State.DraftVersion,
            IsArchived: State.IsArchived,
            CreatedAt: State.CreatedAt,
            Published: published,
            Draft: draft,
            TotalVersions: State.Versions.Count,
            UsedByItemIds: State.UsedByItemIds);
    }

    private static ModifierBlockVersionSnapshot ToVersionSnapshot(ModifierBlockVersionState v)
    {
        return new ModifierBlockVersionSnapshot(
            VersionNumber: v.VersionNumber,
            CreatedAt: v.CreatedAt,
            CreatedBy: v.CreatedBy,
            ChangeNote: v.ChangeNote,
            Name: v.Content.GetStrings().Name,
            SelectionRule: v.SelectionRule,
            MinSelections: v.MinSelections,
            MaxSelections: v.MaxSelections,
            IsRequired: v.IsRequired,
            Options: v.Options.Select(o => new ModifierOptionSnapshot(
                OptionId: o.OptionId,
                Name: o.Content.GetStrings().Name,
                PriceAdjustment: o.PriceAdjustment,
                IsDefault: o.IsDefault,
                DisplayOrder: o.DisplayOrder,
                IsActive: o.IsActive,
                ServingSize: o.ServingSize,
                ServingUnit: o.ServingUnit,
                InventoryItemId: o.InventoryItemId
            )).ToList());
    }

    private void EnsureInitialized()
    {
        if (!State.IsCreated)
            throw new InvalidOperationException("Modifier block not initialized");
    }
}

// ============================================================================
// Content Tag Grain Implementation
// ============================================================================

/// <summary>
/// Grain for content tag management (allergens, dietary info, promotions).
/// </summary>
public class ContentTagGrain : Grain, IContentTagGrain
{
    private readonly IPersistentState<ContentTagState> _state;

    public ContentTagGrain(
        [PersistentState("contentTag", "OrleansStorage")]
        IPersistentState<ContentTagState> state)
    {
        _state = state;
    }

    public async Task<ContentTagSnapshot> CreateAsync(CreateContentTagCommand command)
    {
        if (_state.State.IsCreated)
            throw new InvalidOperationException("Content tag already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var tagId = parts[2];

        _state.State = new ContentTagState
        {
            OrgId = orgId,
            TagId = tagId,
            IsCreated = true,
            Category = command.Category,
            Content = new LocalizedContent
            {
                Translations = new Dictionary<string, LocalizedStrings>
                {
                    ["en-US"] = new LocalizedStrings { Name = command.Name }
                }
            },
            IconUrl = command.IconUrl,
            BadgeColor = command.BadgeColor,
            DisplayOrder = command.DisplayOrder,
            IsActive = true,
            ExternalTagId = command.ExternalTagId,
            ExternalPlatform = command.ExternalPlatform
        };

        await _state.WriteStateAsync();
        return ToSnapshot();
    }

    public async Task<ContentTagSnapshot> UpdateAsync(UpdateContentTagCommand command)
    {
        EnsureInitialized();

        if (command.Name != null)
        {
            _state.State.Content.Translations["en-US"].Name = command.Name;
        }
        if (command.IconUrl != null)
        {
            _state.State.IconUrl = command.IconUrl;
        }
        if (command.BadgeColor != null)
        {
            _state.State.BadgeColor = command.BadgeColor;
        }
        if (command.DisplayOrder.HasValue)
        {
            _state.State.DisplayOrder = command.DisplayOrder.Value;
        }
        if (command.IsActive.HasValue)
        {
            _state.State.IsActive = command.IsActive.Value;
        }

        await _state.WriteStateAsync();
        return ToSnapshot();
    }

    public Task<ContentTagSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(ToSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.IsCreated);
    }

    public async Task DeactivateAsync()
    {
        EnsureInitialized();
        _state.State.IsActive = false;
        await _state.WriteStateAsync();
    }

    public async Task ReactivateAsync()
    {
        EnsureInitialized();
        _state.State.IsActive = true;
        await _state.WriteStateAsync();
    }

    private ContentTagSnapshot ToSnapshot()
    {
        return new ContentTagSnapshot(
            TagId: _state.State.TagId,
            OrgId: _state.State.OrgId,
            Name: _state.State.Content.GetStrings().Name,
            Category: _state.State.Category,
            IconUrl: _state.State.IconUrl,
            BadgeColor: _state.State.BadgeColor,
            DisplayOrder: _state.State.DisplayOrder,
            IsActive: _state.State.IsActive,
            ExternalTagId: _state.State.ExternalTagId,
            ExternalPlatform: _state.State.ExternalPlatform);
    }

    private void EnsureInitialized()
    {
        if (!_state.State.IsCreated)
            throw new InvalidOperationException("Content tag not initialized");
    }
}

// ============================================================================
// Site Menu Overrides Grain Implementation
// ============================================================================

/// <summary>
/// Grain for site-level menu overrides.
/// Handles price overrides, visibility, availability windows, and snoozing.
/// </summary>
public class SiteMenuOverridesGrain : Grain, ISiteMenuOverridesGrain
{
    private readonly IPersistentState<SiteMenuOverridesState> _state;

    public SiteMenuOverridesGrain(
        [PersistentState("siteMenuOverrides", "OrleansStorage")]
        IPersistentState<SiteMenuOverridesState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync()
    {
        if (_state.State.IsCreated)
            return;

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var siteId = Guid.Parse(parts[1]);

        _state.State = new SiteMenuOverridesState
        {
            OrgId = orgId,
            SiteId = siteId,
            IsCreated = true,
            AuditLog =
            [
                new AuditEntry { Action = "Initialized" }
            ]
        };

        await _state.WriteStateAsync();
    }

    public async Task SetPriceOverrideAsync(SetSitePriceOverrideCommand command)
    {
        await EnsureInitializedAsync();

        var existing = _state.State.PriceOverrides.FirstOrDefault(p => p.ItemDocumentId == command.ItemDocumentId);
        if (existing != null)
        {
            _state.State.PriceOverrides.Remove(existing);
        }

        _state.State.PriceOverrides.Add(new SitePriceOverride
        {
            ItemDocumentId = command.ItemDocumentId,
            Price = command.Price,
            EffectiveFrom = command.EffectiveFrom,
            EffectiveUntil = command.EffectiveUntil,
            Reason = command.Reason
        });

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "PriceOverrideSet",
            UserId = command.SetBy,
            Note = $"Set price override for {command.ItemDocumentId} to {command.Price}"
        });

        await _state.WriteStateAsync();
    }

    public async Task RemovePriceOverrideAsync(string itemDocumentId, Guid? removedBy = null)
    {
        await EnsureInitializedAsync();

        _state.State.PriceOverrides.RemoveAll(p => p.ItemDocumentId == itemDocumentId);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "PriceOverrideRemoved",
            UserId = removedBy,
            Note = $"Removed price override for {itemDocumentId}"
        });

        await _state.WriteStateAsync();
    }

    public Task<decimal?> GetPriceOverrideAsync(string itemDocumentId)
    {
        if (!_state.State.IsCreated)
            return Task.FromResult<decimal?>(null);

        var now = DateTimeOffset.UtcNow;
        var @override = _state.State.PriceOverrides.FirstOrDefault(p =>
            p.ItemDocumentId == itemDocumentId &&
            (!p.EffectiveFrom.HasValue || p.EffectiveFrom.Value <= now) &&
            (!p.EffectiveUntil.HasValue || p.EffectiveUntil.Value > now));

        return Task.FromResult(@override?.Price);
    }

    public async Task HideItemAsync(string itemDocumentId, Guid? hiddenBy = null)
    {
        await EnsureInitializedAsync();

        _state.State.HiddenItemIds.Add(itemDocumentId);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "ItemHidden",
            UserId = hiddenBy,
            Note = $"Hidden item {itemDocumentId}"
        });

        await _state.WriteStateAsync();
    }

    public async Task UnhideItemAsync(string itemDocumentId, Guid? unhiddenBy = null)
    {
        await EnsureInitializedAsync();

        _state.State.HiddenItemIds.Remove(itemDocumentId);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "ItemUnhidden",
            UserId = unhiddenBy,
            Note = $"Unhidden item {itemDocumentId}"
        });

        await _state.WriteStateAsync();
    }

    public async Task HideCategoryAsync(string categoryDocumentId, Guid? hiddenBy = null)
    {
        await EnsureInitializedAsync();

        _state.State.HiddenCategoryIds.Add(categoryDocumentId);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "CategoryHidden",
            UserId = hiddenBy,
            Note = $"Hidden category {categoryDocumentId}"
        });

        await _state.WriteStateAsync();
    }

    public async Task UnhideCategoryAsync(string categoryDocumentId, Guid? unhiddenBy = null)
    {
        await EnsureInitializedAsync();

        _state.State.HiddenCategoryIds.Remove(categoryDocumentId);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "CategoryUnhidden",
            UserId = unhiddenBy,
            Note = $"Unhidden category {categoryDocumentId}"
        });

        await _state.WriteStateAsync();
    }

    public async Task AddLocalItemAsync(string itemDocumentId)
    {
        await EnsureInitializedAsync();

        if (!_state.State.LocalItemIds.Contains(itemDocumentId))
        {
            _state.State.LocalItemIds.Add(itemDocumentId);
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveLocalItemAsync(string itemDocumentId)
    {
        await EnsureInitializedAsync();

        _state.State.LocalItemIds.Remove(itemDocumentId);
        await _state.WriteStateAsync();
    }

    public async Task AddLocalCategoryAsync(string categoryDocumentId)
    {
        await EnsureInitializedAsync();

        if (!_state.State.LocalCategoryIds.Contains(categoryDocumentId))
        {
            _state.State.LocalCategoryIds.Add(categoryDocumentId);
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveLocalCategoryAsync(string categoryDocumentId)
    {
        await EnsureInitializedAsync();

        _state.State.LocalCategoryIds.Remove(categoryDocumentId);
        await _state.WriteStateAsync();
    }

    public async Task<AvailabilityWindow> AddAvailabilityWindowAsync(AddAvailabilityWindowCommand command)
    {
        await EnsureInitializedAsync();

        var window = new AvailabilityWindow
        {
            WindowId = Guid.NewGuid().ToString(),
            Name = command.Name,
            StartTime = command.StartTime,
            EndTime = command.EndTime,
            DaysOfWeek = command.DaysOfWeek.ToList(),
            ItemDocumentIds = command.ItemDocumentIds?.ToList() ?? [],
            CategoryDocumentIds = command.CategoryDocumentIds?.ToList() ?? [],
            IsActive = true
        };

        _state.State.AvailabilityWindows.Add(window);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "AvailabilityWindowAdded",
            Note = $"Added availability window '{command.Name}'"
        });

        await _state.WriteStateAsync();
        return window;
    }

    public async Task UpdateAvailabilityWindowAsync(string windowId, AddAvailabilityWindowCommand command)
    {
        await EnsureInitializedAsync();

        var window = _state.State.AvailabilityWindows.FirstOrDefault(w => w.WindowId == windowId)
            ?? throw new InvalidOperationException($"Availability window {windowId} not found");

        window.Name = command.Name;
        window.StartTime = command.StartTime;
        window.EndTime = command.EndTime;
        window.DaysOfWeek = command.DaysOfWeek.ToList();
        window.ItemDocumentIds = command.ItemDocumentIds?.ToList() ?? window.ItemDocumentIds;
        window.CategoryDocumentIds = command.CategoryDocumentIds?.ToList() ?? window.CategoryDocumentIds;

        await _state.WriteStateAsync();
    }

    public async Task RemoveAvailabilityWindowAsync(string windowId, Guid? removedBy = null)
    {
        await EnsureInitializedAsync();

        _state.State.AvailabilityWindows.RemoveAll(w => w.WindowId == windowId);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "AvailabilityWindowRemoved",
            UserId = removedBy,
            Note = $"Removed availability window {windowId}"
        });

        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<AvailabilityWindow>> GetAvailabilityWindowsAsync()
    {
        if (!_state.State.IsCreated)
            return Task.FromResult<IReadOnlyList<AvailabilityWindow>>([]);

        return Task.FromResult<IReadOnlyList<AvailabilityWindow>>(
            _state.State.AvailabilityWindows.Where(w => w.IsActive).ToList());
    }

    public async Task SnoozeItemAsync(string itemDocumentId, DateTimeOffset? until = null, Guid? snoozedBy = null, string? reason = null)
    {
        await EnsureInitializedAsync();

        _state.State.SnoozedItems[itemDocumentId] = until;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "ItemSnoozed",
            UserId = snoozedBy,
            Note = reason ?? $"Snoozed item {itemDocumentId}" + (until.HasValue ? $" until {until}" : " indefinitely")
        });

        await _state.WriteStateAsync();
    }

    public async Task UnsnoozeItemAsync(string itemDocumentId, Guid? unsnoozedBy = null)
    {
        await EnsureInitializedAsync();

        _state.State.SnoozedItems.Remove(itemDocumentId);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "ItemUnsnoozed",
            UserId = unsnoozedBy,
            Note = $"Unsnoozed item {itemDocumentId}"
        });

        await _state.WriteStateAsync();
    }

    public Task<bool> IsItemSnoozedAsync(string itemDocumentId)
    {
        if (!_state.State.IsCreated)
            return Task.FromResult(false);

        if (!_state.State.SnoozedItems.TryGetValue(itemDocumentId, out var until))
            return Task.FromResult(false);

        // If no expiry, it's indefinitely snoozed
        if (!until.HasValue)
            return Task.FromResult(true);

        // Check if snooze has expired
        return Task.FromResult(until.Value > DateTimeOffset.UtcNow);
    }

    public Task<SiteMenuOverridesSnapshot> GetSnapshotAsync()
    {
        if (!_state.State.IsCreated)
        {
            var key = this.GetPrimaryKeyString();
            var parts = key.Split(':');
            return Task.FromResult(new SiteMenuOverridesSnapshot(
                OrgId: Guid.Parse(parts[0]),
                SiteId: Guid.Parse(parts[1]),
                PriceOverrides: [],
                HiddenItemIds: [],
                HiddenCategoryIds: [],
                LocalItemIds: [],
                LocalCategoryIds: [],
                AvailabilityWindows: [],
                SnoozedItems: new Dictionary<string, DateTimeOffset?>()));
        }

        // Clean up expired snoozes
        var now = DateTimeOffset.UtcNow;
        var activeSnoozed = _state.State.SnoozedItems
            .Where(kvp => !kvp.Value.HasValue || kvp.Value.Value > now)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return Task.FromResult(new SiteMenuOverridesSnapshot(
            OrgId: _state.State.OrgId,
            SiteId: _state.State.SiteId,
            PriceOverrides: _state.State.PriceOverrides,
            HiddenItemIds: _state.State.HiddenItemIds.ToList(),
            HiddenCategoryIds: _state.State.HiddenCategoryIds.ToList(),
            LocalItemIds: _state.State.LocalItemIds,
            LocalCategoryIds: _state.State.LocalCategoryIds,
            AvailabilityWindows: _state.State.AvailabilityWindows.Where(w => w.IsActive).ToList(),
            SnoozedItems: activeSnoozed));
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_state.State.IsCreated)
            await InitializeAsync();
    }
}
