using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Recipe Document Grain Implementation (Event Sourced)
// ============================================================================

/// <summary>
/// Event-sourced grain for recipe document management with versioning and workflow.
/// All state changes are recorded as events and can be replayed for full history.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class RecipeDocumentGrain : JournaledGrain<RecipeDocumentState, IRecipeDocumentEvent>, IRecipeDocumentGrain
{
    /// <summary>
    /// Applies domain events to mutate state. This is the core of event sourcing.
    /// </summary>
    protected override void TransitionState(RecipeDocumentState state, IRecipeDocumentEvent @event)
    {
        switch (@event)
        {
            case RecipeDocumentInitialized e:
                state.OrgId = e.OrgId;
                state.DocumentId = e.DocumentId;
                state.IsCreated = true;
                state.CurrentVersion = 1;
                state.PublishedVersion = e.PublishImmediately ? 1 : null;
                state.DraftVersion = e.PublishImmediately ? null : 1;
                state.CreatedAt = e.OccurredAt;
                var ingredients = e.Ingredients?.Select((i, idx) => new RecipeIngredientState
                {
                    IngredientId = i.IngredientId,
                    IngredientName = i.IngredientName,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    WastePercentage = i.WastePercentage,
                    UnitCost = i.UnitCost,
                    PrepInstructions = i.PrepInstructions,
                    IsOptional = i.IsOptional,
                    DisplayOrder = i.DisplayOrder == 0 ? idx : i.DisplayOrder,
                    SubstitutionIds = i.SubstitutionIds?.ToList() ?? []
                }).ToList() ?? [];
                state.Versions.Add(new RecipeVersionState
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
                    Media = !string.IsNullOrEmpty(e.ImageUrl) ? new MediaInfo { PrimaryImageUrl = e.ImageUrl } : null,
                    PortionYield = e.PortionYield,
                    YieldUnit = e.YieldUnit,
                    Ingredients = ingredients,
                    AllergenTags = e.AllergenTags?.ToList() ?? [],
                    DietaryTags = e.DietaryTags?.ToList() ?? [],
                    PrepInstructions = e.PrepInstructions,
                    PrepTimeMinutes = e.PrepTimeMinutes,
                    CookTimeMinutes = e.CookTimeMinutes,
                    CategoryId = e.CategoryId
                });
                break;

            case RecipeDraftVersionCreated e:
                var baseVersion = state.DraftVersion.HasValue
                    ? state.Versions.First(v => v.VersionNumber == state.DraftVersion.Value)
                    : state.Versions.First(v => v.VersionNumber == state.PublishedVersion);
                var draftIngredients = e.Ingredients?.Select((i, idx) => new RecipeIngredientState
                {
                    IngredientId = i.IngredientId,
                    IngredientName = i.IngredientName,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    WastePercentage = i.WastePercentage,
                    UnitCost = i.UnitCost,
                    PrepInstructions = i.PrepInstructions,
                    IsOptional = i.IsOptional,
                    DisplayOrder = i.DisplayOrder == 0 ? idx : i.DisplayOrder,
                    SubstitutionIds = i.SubstitutionIds?.ToList() ?? []
                }).ToList() ?? baseVersion.Ingredients.Select(i => new RecipeIngredientState
                {
                    IngredientId = i.IngredientId,
                    IngredientName = i.IngredientName,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    WastePercentage = i.WastePercentage,
                    UnitCost = i.UnitCost,
                    PrepInstructions = i.PrepInstructions,
                    IsOptional = i.IsOptional,
                    DisplayOrder = i.DisplayOrder,
                    SubstitutionIds = [.. i.SubstitutionIds]
                }).ToList();
                var newVersion = new RecipeVersionState
                {
                    VersionNumber = e.VersionNumber,
                    CreatedAt = e.OccurredAt,
                    CreatedBy = e.CreatedBy,
                    ChangeNote = e.ChangeNote,
                    Content = CloneContent(baseVersion.Content),
                    Media = baseVersion.Media != null ? new MediaInfo
                    {
                        PrimaryImageUrl = e.ImageUrl ?? baseVersion.Media.PrimaryImageUrl,
                        ThumbnailUrl = baseVersion.Media.ThumbnailUrl,
                        AdditionalImageUrls = [.. baseVersion.Media.AdditionalImageUrls]
                    } : e.ImageUrl != null ? new MediaInfo { PrimaryImageUrl = e.ImageUrl } : null,
                    PortionYield = e.PortionYield ?? baseVersion.PortionYield,
                    YieldUnit = e.YieldUnit ?? baseVersion.YieldUnit,
                    Ingredients = draftIngredients,
                    AllergenTags = e.AllergenTags?.ToList() ?? [.. baseVersion.AllergenTags],
                    DietaryTags = e.DietaryTags?.ToList() ?? [.. baseVersion.DietaryTags],
                    PrepInstructions = e.PrepInstructions ?? baseVersion.PrepInstructions,
                    PrepTimeMinutes = e.PrepTimeMinutes ?? baseVersion.PrepTimeMinutes,
                    CookTimeMinutes = e.CookTimeMinutes ?? baseVersion.CookTimeMinutes,
                    CategoryId = e.CategoryId ?? baseVersion.CategoryId
                };
                if (e.Name != null)
                    newVersion.Content.Translations[newVersion.Content.DefaultLocale].Name = e.Name;
                if (e.Description != null)
                    newVersion.Content.Translations[newVersion.Content.DefaultLocale].Description = e.Description;
                state.Versions.Add(newVersion);
                state.CurrentVersion = e.VersionNumber;
                state.DraftVersion = e.VersionNumber;
                break;

            case RecipeDraftWasPublished e:
                state.PublishedVersion = e.PublishedVersion;
                state.DraftVersion = null;
                break;

            case RecipeDraftWasDiscarded e:
                state.Versions.RemoveAll(v => v.VersionNumber == e.DiscardedVersion);
                state.DraftVersion = null;
                state.CurrentVersion = state.Versions.Max(v => v.VersionNumber);
                break;

            case RecipeRevertedToVersion e:
                var targetVersion = state.Versions.First(v => v.VersionNumber == e.ToVersion);
                state.Versions.Add(new RecipeVersionState
                {
                    VersionNumber = e.NewVersionNumber,
                    CreatedAt = e.OccurredAt,
                    CreatedBy = e.RevertedBy,
                    ChangeNote = e.Reason ?? $"Reverted to version {e.ToVersion}",
                    Content = CloneContent(targetVersion.Content),
                    Media = targetVersion.Media,
                    PortionYield = targetVersion.PortionYield,
                    YieldUnit = targetVersion.YieldUnit,
                    Ingredients = targetVersion.Ingredients.Select(i => new RecipeIngredientState
                    {
                        IngredientId = i.IngredientId,
                        IngredientName = i.IngredientName,
                        Quantity = i.Quantity,
                        Unit = i.Unit,
                        WastePercentage = i.WastePercentage,
                        UnitCost = i.UnitCost,
                        PrepInstructions = i.PrepInstructions,
                        IsOptional = i.IsOptional,
                        DisplayOrder = i.DisplayOrder,
                        SubstitutionIds = [.. i.SubstitutionIds]
                    }).ToList(),
                    AllergenTags = [.. targetVersion.AllergenTags],
                    DietaryTags = [.. targetVersion.DietaryTags],
                    PrepInstructions = targetVersion.PrepInstructions,
                    PrepTimeMinutes = targetVersion.PrepTimeMinutes,
                    CookTimeMinutes = targetVersion.CookTimeMinutes,
                    CategoryId = targetVersion.CategoryId
                });
                state.CurrentVersion = e.NewVersionNumber;
                state.PublishedVersion = e.NewVersionNumber;
                state.DraftVersion = null;
                break;

            case RecipeTranslationAdded e:
                var versionForTranslation = state.DraftVersion.HasValue
                    ? state.Versions.First(v => v.VersionNumber == state.DraftVersion.Value)
                    : state.Versions.First(v => v.VersionNumber == state.PublishedVersion);
                versionForTranslation.Content.Translations[e.Locale] = new LocalizedStrings
                {
                    Name = e.Name,
                    Description = e.Description
                };
                break;

            case RecipeTranslationRemoved e:
                var versionForRemoval = state.DraftVersion.HasValue
                    ? state.Versions.First(v => v.VersionNumber == state.DraftVersion.Value)
                    : state.Versions.First(v => v.VersionNumber == state.PublishedVersion);
                versionForRemoval.Content.Translations.Remove(e.Locale);
                break;

            case RecipeChangeWasScheduled e:
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

            case RecipeScheduleWasCancelled e:
                state.Schedules.RemoveAll(s => s.ScheduleId == e.ScheduleId);
                break;

            case RecipeDocumentWasArchived e:
                state.IsArchived = true;
                break;

            case RecipeDocumentWasRestored e:
                state.IsArchived = false;
                break;

            case RecipeLinkedToMenu e:
                if (!state.LinkedMenuItemIds.Contains(e.MenuItemDocumentId))
                    state.LinkedMenuItemIds.Add(e.MenuItemDocumentId);
                break;

            case RecipeUnlinkedFromMenu e:
                state.LinkedMenuItemIds.Remove(e.MenuItemDocumentId);
                break;

            case RecipeCostWasRecalculated e:
                if (e.UpdatedIngredientPrices != null)
                {
                    var versionToUpdate = state.Versions.FirstOrDefault(v => v.VersionNumber == e.VersionNumber);
                    if (versionToUpdate != null)
                    {
                        foreach (var ingredient in versionToUpdate.Ingredients)
                        {
                            if (e.UpdatedIngredientPrices.TryGetValue(ingredient.IngredientId, out var newPrice))
                            {
                                ingredient.UnitCost = newPrice;
                            }
                        }
                    }
                }
                break;
        }
    }

    public async Task<RecipeDocumentSnapshot> CreateAsync(CreateRecipeDocumentCommand command)
    {
        if (State.IsCreated)
            throw new InvalidOperationException("Recipe document already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var documentId = parts[2];

        var ingredientData = command.Ingredients?.Select((i, idx) => new RecipeIngredientData(
            IngredientId: i.IngredientId,
            IngredientName: i.IngredientName,
            Quantity: i.Quantity,
            Unit: i.Unit,
            WastePercentage: i.WastePercentage,
            UnitCost: i.UnitCost,
            PrepInstructions: i.PrepInstructions,
            IsOptional: i.IsOptional,
            DisplayOrder: i.DisplayOrder == 0 ? idx : i.DisplayOrder,
            SubstitutionIds: i.SubstitutionIds?.ToList()
        )).ToList();

        RaiseEvent(new RecipeDocumentInitialized(
            DocumentId: documentId,
            OrgId: orgId,
            OccurredAt: DateTimeOffset.UtcNow,
            Name: command.Name,
            Description: command.Description,
            PortionYield: command.PortionYield,
            YieldUnit: command.YieldUnit,
            Ingredients: ingredientData,
            AllergenTags: command.AllergenTags?.ToList(),
            DietaryTags: command.DietaryTags?.ToList(),
            PrepInstructions: command.PrepInstructions,
            PrepTimeMinutes: command.PrepTimeMinutes,
            CookTimeMinutes: command.CookTimeMinutes,
            ImageUrl: command.ImageUrl,
            CategoryId: command.CategoryId,
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

    public async Task<RecipeVersionSnapshot> CreateDraftAsync(CreateRecipeDraftCommand command)
    {
        EnsureInitialized();

        var newVersionNumber = State.CurrentVersion + 1;

        var ingredientData = command.Ingredients?.Select((i, idx) => new RecipeIngredientData(
            IngredientId: i.IngredientId,
            IngredientName: i.IngredientName,
            Quantity: i.Quantity,
            Unit: i.Unit,
            WastePercentage: i.WastePercentage,
            UnitCost: i.UnitCost,
            PrepInstructions: i.PrepInstructions,
            IsOptional: i.IsOptional,
            DisplayOrder: i.DisplayOrder == 0 ? idx : i.DisplayOrder,
            SubstitutionIds: i.SubstitutionIds?.ToList()
        )).ToList();

        RaiseEvent(new RecipeDraftVersionCreated(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            VersionNumber: newVersionNumber,
            Name: command.Name,
            Description: command.Description,
            PortionYield: command.PortionYield,
            YieldUnit: command.YieldUnit,
            Ingredients: ingredientData,
            AllergenTags: command.AllergenTags?.ToList(),
            DietaryTags: command.DietaryTags?.ToList(),
            PrepInstructions: command.PrepInstructions,
            PrepTimeMinutes: command.PrepTimeMinutes,
            CookTimeMinutes: command.CookTimeMinutes,
            ImageUrl: command.ImageUrl,
            CategoryId: command.CategoryId,
            ChangeNote: command.ChangeNote,
            CreatedBy: command.CreatedBy
        ));

        await ConfirmEvents();

        var newVersion = State.Versions.First(v => v.VersionNumber == newVersionNumber);
        return ToVersionSnapshot(newVersion);
    }

    public Task<RecipeVersionSnapshot?> GetVersionAsync(int version)
    {
        EnsureInitialized();
        var v = State.Versions.FirstOrDefault(x => x.VersionNumber == version);
        return Task.FromResult(v != null ? ToVersionSnapshot(v) : null);
    }

    public Task<RecipeVersionSnapshot?> GetPublishedAsync()
    {
        EnsureInitialized();
        if (!State.PublishedVersion.HasValue)
            return Task.FromResult<RecipeVersionSnapshot?>(null);

        var v = State.Versions.First(x => x.VersionNumber == State.PublishedVersion.Value);
        return Task.FromResult<RecipeVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<RecipeVersionSnapshot?> GetDraftAsync()
    {
        EnsureInitialized();
        if (!State.DraftVersion.HasValue)
            return Task.FromResult<RecipeVersionSnapshot?>(null);

        var v = State.Versions.First(x => x.VersionNumber == State.DraftVersion.Value);
        return Task.FromResult<RecipeVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<IReadOnlyList<RecipeVersionSnapshot>> GetVersionHistoryAsync(int skip = 0, int take = 20)
    {
        EnsureInitialized();
        var versions = State.Versions
            .OrderByDescending(v => v.VersionNumber)
            .Skip(skip)
            .Take(take)
            .Select(ToVersionSnapshot)
            .ToList();
        return Task.FromResult<IReadOnlyList<RecipeVersionSnapshot>>(versions);
    }

    public async Task PublishDraftAsync(Guid? publishedBy = null, string? note = null)
    {
        EnsureInitialized();

        if (!State.DraftVersion.HasValue)
            throw new InvalidOperationException("No draft to publish");

        RaiseEvent(new RecipeDraftWasPublished(
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

        RaiseEvent(new RecipeDraftWasDiscarded(
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

        RaiseEvent(new RecipeRevertedToVersion(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            FromVersion: State.CurrentVersion,
            ToVersion: version,
            NewVersionNumber: newVersionNumber,
            RevertedBy: revertedBy,
            Reason: reason
        ));

        await ConfirmEvents();
    }

    public async Task AddTranslationAsync(AddRecipeTranslationCommand command)
    {
        EnsureInitialized();

        RaiseEvent(new RecipeTranslationAdded(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            Locale: command.Locale,
            Name: command.Name,
            Description: command.Description
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

        RaiseEvent(new RecipeTranslationRemoved(
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

        RaiseEvent(new RecipeChangeWasScheduled(
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

        if (State.Schedules.Any(s => s.ScheduleId == scheduleId))
        {
            RaiseEvent(new RecipeScheduleWasCancelled(
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

        RaiseEvent(new RecipeDocumentWasArchived(
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

        RaiseEvent(new RecipeDocumentWasRestored(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            RestoredBy: restoredBy
        ));

        await ConfirmEvents();
    }

    public async Task RecalculateCostAsync(IReadOnlyDictionary<Guid, decimal>? ingredientPrices = null)
    {
        EnsureInitialized();

        if (ingredientPrices == null || ingredientPrices.Count == 0)
            return;

        // Get the version to update (published or draft)
        var versionNumber = State.PublishedVersion ?? State.DraftVersion;
        if (!versionNumber.HasValue)
            return;

        var version = State.Versions.FirstOrDefault(v => v.VersionNumber == versionNumber.Value);
        if (version == null)
            return;

        var previousCost = version.TheoreticalCost;

        // Create a dictionary of prices that actually apply to this recipe's ingredients
        var applicablePrices = new Dictionary<Guid, decimal>();
        foreach (var ingredient in version.Ingredients)
        {
            if (ingredientPrices.TryGetValue(ingredient.IngredientId, out var newPrice))
            {
                applicablePrices[ingredient.IngredientId] = newPrice;
            }
        }

        if (applicablePrices.Count == 0)
            return;

        // Calculate what the new cost will be
        var newCost = version.Ingredients.Sum(i =>
        {
            var unitCost = applicablePrices.TryGetValue(i.IngredientId, out var newPrice) ? newPrice : i.UnitCost;
            var effectiveQty = i.WastePercentage > 0 ? i.Quantity / (1 - i.WastePercentage / 100) : i.Quantity;
            return effectiveQty * unitCost;
        });

        RaiseEvent(new RecipeCostWasRecalculated(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            VersionNumber: versionNumber.Value,
            PreviousCost: previousCost,
            NewCost: newCost,
            UpdatedIngredientPrices: applicablePrices
        ));

        await ConfirmEvents();
    }

    public async Task LinkMenuItemAsync(string menuItemDocumentId)
    {
        EnsureInitialized();

        if (!State.LinkedMenuItemIds.Contains(menuItemDocumentId))
        {
            RaiseEvent(new RecipeLinkedToMenu(
                DocumentId: State.DocumentId,
                OccurredAt: DateTimeOffset.UtcNow,
                MenuItemDocumentId: menuItemDocumentId
            ));

            await ConfirmEvents();
        }
    }

    public async Task UnlinkMenuItemAsync(string menuItemDocumentId)
    {
        EnsureInitialized();

        if (State.LinkedMenuItemIds.Contains(menuItemDocumentId))
        {
            RaiseEvent(new RecipeUnlinkedFromMenu(
                DocumentId: State.DocumentId,
                OccurredAt: DateTimeOffset.UtcNow,
                MenuItemDocumentId: menuItemDocumentId
            ));

            await ConfirmEvents();
        }
    }

    public Task<IReadOnlyList<string>> GetLinkedMenuItemsAsync()
    {
        EnsureInitialized();
        return Task.FromResult<IReadOnlyList<string>>(State.LinkedMenuItemIds);
    }

    public Task<RecipeDocumentSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(GetSnapshot());
    }

    public async Task<IReadOnlyList<IRecipeDocumentEvent>> GetEventHistoryAsync(int fromVersion = 0, int maxCount = 100)
    {
        var events = await RetrieveConfirmedEvents(fromVersion, maxCount);
        return events.ToList();
    }

    public Task<RecipeVersionSnapshot?> PreviewAtAsync(DateTimeOffset when)
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
            return Task.FromResult<RecipeVersionSnapshot?>(ToVersionSnapshot(v));
        }

        return Task.FromResult<RecipeVersionSnapshot?>(null);
    }

    private RecipeDocumentSnapshot GetSnapshot()
    {
        RecipeVersionSnapshot? published = null;
        RecipeVersionSnapshot? draft = null;

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

        return new RecipeDocumentSnapshot(
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
            TotalVersions: State.Versions.Count,
            LinkedMenuItemIds: State.LinkedMenuItemIds);
    }

    private static RecipeVersionSnapshot ToVersionSnapshot(RecipeVersionState v)
    {
        var defaultStrings = v.Content.GetStrings();
        return new RecipeVersionSnapshot(
            VersionNumber: v.VersionNumber,
            CreatedAt: v.CreatedAt,
            CreatedBy: v.CreatedBy,
            ChangeNote: v.ChangeNote,
            Name: defaultStrings.Name,
            Description: defaultStrings.Description,
            PortionYield: v.PortionYield,
            YieldUnit: v.YieldUnit,
            Ingredients: v.Ingredients.Select(i => new RecipeIngredientSnapshot(
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
                SubstitutionIds: i.SubstitutionIds
            )).ToList(),
            AllergenTags: v.AllergenTags,
            DietaryTags: v.DietaryTags,
            PrepInstructions: v.PrepInstructions,
            PrepTimeMinutes: v.PrepTimeMinutes,
            CookTimeMinutes: v.CookTimeMinutes,
            ImageUrl: v.Media?.PrimaryImageUrl,
            CategoryId: v.CategoryId,
            TheoreticalCost: v.TheoreticalCost,
            CostPerPortion: v.CostPerPortion,
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
            throw new InvalidOperationException("Recipe document not initialized");
    }
}

// ============================================================================
// Recipe Category Document Grain Implementation (Event Sourced)
// ============================================================================

/// <summary>
/// Event-sourced grain for recipe category document management with versioning and workflow.
/// All state changes are recorded as events and can be replayed for full history.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class RecipeCategoryDocumentGrain : JournaledGrain<RecipeCategoryDocumentState, IRecipeCategoryDocumentEvent>, IRecipeCategoryDocumentGrain
{
    /// <summary>
    /// Applies domain events to mutate state. This is the core of event sourcing.
    /// </summary>
    protected override void TransitionState(RecipeCategoryDocumentState state, IRecipeCategoryDocumentEvent @event)
    {
        switch (@event)
        {
            case RecipeCategoryDocumentInitialized e:
                state.OrgId = e.OrgId;
                state.DocumentId = e.DocumentId;
                state.IsCreated = true;
                state.CurrentVersion = 1;
                state.PublishedVersion = e.PublishImmediately ? 1 : null;
                state.DraftVersion = e.PublishImmediately ? null : 1;
                state.CreatedAt = e.OccurredAt;
                state.Versions.Add(new RecipeCategoryVersionState
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

            case RecipeCategoryDraftVersionCreated e:
                var baseVer = state.DraftVersion.HasValue
                    ? state.Versions.First(v => v.VersionNumber == state.DraftVersion.Value)
                    : state.Versions.First(v => v.VersionNumber == state.PublishedVersion);
                var newVer = new RecipeCategoryVersionState
                {
                    VersionNumber = e.VersionNumber,
                    CreatedAt = e.OccurredAt,
                    CreatedBy = e.CreatedBy,
                    ChangeNote = e.ChangeNote,
                    Content = CloneContent(baseVer.Content),
                    Color = e.Color ?? baseVer.Color,
                    IconUrl = e.IconUrl ?? baseVer.IconUrl,
                    DisplayOrder = e.DisplayOrder ?? baseVer.DisplayOrder,
                    RecipeDocumentIds = e.RecipeDocumentIds?.ToList() ?? [.. baseVer.RecipeDocumentIds]
                };
                if (e.Name != null)
                    newVer.Content.Translations[newVer.Content.DefaultLocale].Name = e.Name;
                if (e.Description != null)
                    newVer.Content.Translations[newVer.Content.DefaultLocale].Description = e.Description;
                state.Versions.Add(newVer);
                state.CurrentVersion = e.VersionNumber;
                state.DraftVersion = e.VersionNumber;
                break;

            case RecipeCategoryDraftWasPublished e:
                state.PublishedVersion = e.PublishedVersion;
                state.DraftVersion = null;
                break;

            case RecipeCategoryDraftWasDiscarded e:
                state.Versions.RemoveAll(v => v.VersionNumber == e.DiscardedVersion);
                state.DraftVersion = null;
                state.CurrentVersion = state.Versions.Max(v => v.VersionNumber);
                break;

            case RecipeCategoryRevertedToVersion e:
                var targetVer = state.Versions.First(v => v.VersionNumber == e.ToVersion);
                state.Versions.Add(new RecipeCategoryVersionState
                {
                    VersionNumber = e.NewVersionNumber,
                    CreatedAt = e.OccurredAt,
                    CreatedBy = e.RevertedBy,
                    ChangeNote = e.Reason ?? $"Reverted to version {e.ToVersion}",
                    Content = CloneContent(targetVer.Content),
                    Color = targetVer.Color,
                    IconUrl = targetVer.IconUrl,
                    DisplayOrder = targetVer.DisplayOrder,
                    RecipeDocumentIds = [.. targetVer.RecipeDocumentIds]
                });
                state.CurrentVersion = e.NewVersionNumber;
                state.PublishedVersion = e.NewVersionNumber;
                state.DraftVersion = null;
                break;

            case RecipeCategoryRecipeAdded e:
                var verForAdd = state.DraftVersion.HasValue
                    ? state.Versions.First(v => v.VersionNumber == state.DraftVersion.Value)
                    : state.Versions.First(v => v.VersionNumber == state.PublishedVersion);
                if (!verForAdd.RecipeDocumentIds.Contains(e.RecipeDocumentId))
                    verForAdd.RecipeDocumentIds.Add(e.RecipeDocumentId);
                break;

            case RecipeCategoryRecipeRemoved e:
                var verForRemove = state.DraftVersion.HasValue
                    ? state.Versions.First(v => v.VersionNumber == state.DraftVersion.Value)
                    : state.Versions.First(v => v.VersionNumber == state.PublishedVersion);
                verForRemove.RecipeDocumentIds.Remove(e.RecipeDocumentId);
                break;

            case RecipeCategoryRecipesReordered e:
                var verForReorder = state.DraftVersion.HasValue
                    ? state.Versions.First(v => v.VersionNumber == state.DraftVersion.Value)
                    : state.Versions.First(v => v.VersionNumber == state.PublishedVersion);
                verForReorder.RecipeDocumentIds = e.RecipeDocumentIds.ToList();
                break;

            case RecipeCategoryChangeWasScheduled e:
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

            case RecipeCategoryScheduleWasCancelled e:
                state.Schedules.RemoveAll(s => s.ScheduleId == e.ScheduleId);
                break;

            case RecipeCategoryDocumentWasArchived e:
                state.IsArchived = true;
                break;

            case RecipeCategoryDocumentWasRestored e:
                state.IsArchived = false;
                break;
        }
    }

    public async Task<RecipeCategoryDocumentSnapshot> CreateAsync(CreateRecipeCategoryDocumentCommand command)
    {
        if (State.IsCreated)
            throw new InvalidOperationException("Recipe category document already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var documentId = parts[2];

        RaiseEvent(new RecipeCategoryDocumentInitialized(
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

    public async Task<RecipeCategoryVersionSnapshot> CreateDraftAsync(CreateRecipeCategoryDraftCommand command)
    {
        EnsureInitialized();

        var newVersionNumber = State.CurrentVersion + 1;

        RaiseEvent(new RecipeCategoryDraftVersionCreated(
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
            RecipeDocumentIds: command.RecipeDocumentIds?.ToList()
        ));

        await ConfirmEvents();

        var newVersion = State.Versions.First(v => v.VersionNumber == newVersionNumber);
        return ToVersionSnapshot(newVersion);
    }

    public Task<RecipeCategoryVersionSnapshot?> GetVersionAsync(int version)
    {
        EnsureInitialized();
        var v = State.Versions.FirstOrDefault(x => x.VersionNumber == version);
        return Task.FromResult(v != null ? ToVersionSnapshot(v) : null);
    }

    public Task<RecipeCategoryVersionSnapshot?> GetPublishedAsync()
    {
        EnsureInitialized();
        if (!State.PublishedVersion.HasValue)
            return Task.FromResult<RecipeCategoryVersionSnapshot?>(null);

        var v = State.Versions.First(x => x.VersionNumber == State.PublishedVersion.Value);
        return Task.FromResult<RecipeCategoryVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<RecipeCategoryVersionSnapshot?> GetDraftAsync()
    {
        EnsureInitialized();
        if (!State.DraftVersion.HasValue)
            return Task.FromResult<RecipeCategoryVersionSnapshot?>(null);

        var v = State.Versions.First(x => x.VersionNumber == State.DraftVersion.Value);
        return Task.FromResult<RecipeCategoryVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<IReadOnlyList<RecipeCategoryVersionSnapshot>> GetVersionHistoryAsync(int skip = 0, int take = 20)
    {
        EnsureInitialized();
        var versions = State.Versions
            .OrderByDescending(v => v.VersionNumber)
            .Skip(skip)
            .Take(take)
            .Select(ToVersionSnapshot)
            .ToList();
        return Task.FromResult<IReadOnlyList<RecipeCategoryVersionSnapshot>>(versions);
    }

    public async Task PublishDraftAsync(Guid? publishedBy = null, string? note = null)
    {
        EnsureInitialized();

        if (!State.DraftVersion.HasValue)
            throw new InvalidOperationException("No draft to publish");

        RaiseEvent(new RecipeCategoryDraftWasPublished(
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

        RaiseEvent(new RecipeCategoryDraftWasDiscarded(
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

        RaiseEvent(new RecipeCategoryRevertedToVersion(
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

    public async Task AddRecipeAsync(string recipeDocumentId)
    {
        EnsureInitialized();

        var targetVersion = State.DraftVersion.HasValue
            ? State.Versions.First(v => v.VersionNumber == State.DraftVersion.Value)
            : State.Versions.First(v => v.VersionNumber == State.PublishedVersion);

        if (!targetVersion.RecipeDocumentIds.Contains(recipeDocumentId))
        {
            RaiseEvent(new RecipeCategoryRecipeAdded(
                DocumentId: State.DocumentId,
                OccurredAt: DateTimeOffset.UtcNow,
                RecipeDocumentId: recipeDocumentId
            ));

            await ConfirmEvents();
        }
    }

    public async Task RemoveRecipeAsync(string recipeDocumentId)
    {
        EnsureInitialized();

        RaiseEvent(new RecipeCategoryRecipeRemoved(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            RecipeDocumentId: recipeDocumentId
        ));

        await ConfirmEvents();
    }

    public async Task ReorderRecipesAsync(IReadOnlyList<string> recipeDocumentIds)
    {
        EnsureInitialized();

        RaiseEvent(new RecipeCategoryRecipesReordered(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            RecipeDocumentIds: recipeDocumentIds.ToList()
        ));

        await ConfirmEvents();
    }

    public async Task<ScheduledChange> ScheduleChangeAsync(int version, DateTimeOffset activateAt, DateTimeOffset? deactivateAt = null, string? name = null)
    {
        EnsureInitialized();

        if (!State.Versions.Any(v => v.VersionNumber == version))
            throw new InvalidOperationException($"Version {version} not found");

        var scheduleId = Guid.NewGuid().ToString();

        RaiseEvent(new RecipeCategoryChangeWasScheduled(
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

        if (State.Schedules.Any(s => s.ScheduleId == scheduleId))
        {
            RaiseEvent(new RecipeCategoryScheduleWasCancelled(
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

        RaiseEvent(new RecipeCategoryDocumentWasArchived(
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

        RaiseEvent(new RecipeCategoryDocumentWasRestored(
            DocumentId: State.DocumentId,
            OccurredAt: DateTimeOffset.UtcNow,
            RestoredBy: restoredBy
        ));

        await ConfirmEvents();
    }

    public Task<RecipeCategoryDocumentSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(GetSnapshot());
    }

    public async Task<IReadOnlyList<IRecipeCategoryDocumentEvent>> GetEventHistoryAsync(int fromVersion = 0, int maxCount = 100)
    {
        var events = await RetrieveConfirmedEvents(fromVersion, maxCount);
        return events.ToList();
    }

    private RecipeCategoryDocumentSnapshot GetSnapshot()
    {
        RecipeCategoryVersionSnapshot? published = null;
        RecipeCategoryVersionSnapshot? draft = null;

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

        return new RecipeCategoryDocumentSnapshot(
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

    private static RecipeCategoryVersionSnapshot ToVersionSnapshot(RecipeCategoryVersionState v)
    {
        var defaultStrings = v.Content.GetStrings();
        return new RecipeCategoryVersionSnapshot(
            VersionNumber: v.VersionNumber,
            CreatedAt: v.CreatedAt,
            CreatedBy: v.CreatedBy,
            ChangeNote: v.ChangeNote,
            Name: defaultStrings.Name,
            Description: defaultStrings.Description,
            Color: v.Color,
            IconUrl: v.IconUrl,
            DisplayOrder: v.DisplayOrder,
            RecipeDocumentIds: v.RecipeDocumentIds,
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
            throw new InvalidOperationException("Recipe category document not initialized");
    }
}

// ============================================================================
// Recipe Registry Grain Implementation
// ============================================================================

/// <summary>
/// Grain for maintaining a registry of recipe documents.
/// </summary>
public class RecipeRegistryGrain : Grain, IRecipeRegistryGrain
{
    private readonly IPersistentState<RecipeRegistryState> _state;

    private RecipeRegistryState State => _state.State;

    public RecipeRegistryGrain(
        [PersistentState("recipeRegistry", "OrleansStorage")]
        IPersistentState<RecipeRegistryState> state)
    {
        _state = state;
    }

    public async Task RegisterRecipeAsync(string documentId, string name, decimal costPerPortion, string? categoryId, int linkedMenuItemCount = 0)
    {
        await EnsureInitializedAsync();

        State.Recipes[documentId] = new RecipeRegistryEntry
        {
            DocumentId = documentId,
            Name = name,
            CostPerPortion = costPerPortion,
            CategoryId = categoryId,
            HasDraft = false,
            IsArchived = false,
            PublishedVersion = 1,
            LastModified = DateTimeOffset.UtcNow,
            LinkedMenuItemCount = linkedMenuItemCount
        };

        await _state.WriteStateAsync();
    }

    public async Task UpdateRecipeAsync(string documentId, string name, decimal costPerPortion, string? categoryId, bool hasDraft, bool isArchived, int linkedMenuItemCount)
    {
        await EnsureInitializedAsync();

        if (State.Recipes.TryGetValue(documentId, out var entry))
        {
            entry.Name = name;
            entry.CostPerPortion = costPerPortion;
            entry.CategoryId = categoryId;
            entry.HasDraft = hasDraft;
            entry.IsArchived = isArchived;
            entry.LastModified = DateTimeOffset.UtcNow;
            entry.LinkedMenuItemCount = linkedMenuItemCount;
            await _state.WriteStateAsync();
        }
    }

    public async Task UnregisterRecipeAsync(string documentId)
    {
        await EnsureInitializedAsync();
        State.Recipes.Remove(documentId);
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<RecipeDocumentSummary>> GetRecipesAsync(string? categoryId = null, bool includeArchived = false)
    {
        var recipes = State.Recipes.Values
            .Where(r => includeArchived || !r.IsArchived)
            .Where(r => categoryId == null || r.CategoryId == categoryId)
            .OrderBy(r => r.Name)
            .Select(r => new RecipeDocumentSummary(
                DocumentId: r.DocumentId,
                Name: r.Name,
                CostPerPortion: r.CostPerPortion,
                CategoryId: r.CategoryId,
                HasDraft: r.HasDraft,
                IsArchived: r.IsArchived,
                PublishedVersion: r.PublishedVersion,
                LastModified: r.LastModified,
                LinkedMenuItemCount: r.LinkedMenuItemCount))
            .ToList();

        return Task.FromResult<IReadOnlyList<RecipeDocumentSummary>>(recipes);
    }

    public async Task RegisterCategoryAsync(string documentId, string name, int displayOrder, string? color)
    {
        await EnsureInitializedAsync();

        State.Categories[documentId] = new RecipeCategoryRegistryEntry
        {
            DocumentId = documentId,
            Name = name,
            DisplayOrder = displayOrder,
            Color = color,
            HasDraft = false,
            IsArchived = false,
            RecipeCount = 0,
            LastModified = DateTimeOffset.UtcNow
        };

        await _state.WriteStateAsync();
    }

    public async Task UpdateCategoryAsync(string documentId, string name, int displayOrder, string? color, bool hasDraft, bool isArchived, int recipeCount)
    {
        await EnsureInitializedAsync();

        if (State.Categories.TryGetValue(documentId, out var entry))
        {
            entry.Name = name;
            entry.DisplayOrder = displayOrder;
            entry.Color = color;
            entry.HasDraft = hasDraft;
            entry.IsArchived = isArchived;
            entry.RecipeCount = recipeCount;
            entry.LastModified = DateTimeOffset.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task UnregisterCategoryAsync(string documentId)
    {
        await EnsureInitializedAsync();
        State.Categories.Remove(documentId);
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<RecipeCategoryDocumentSummary>> GetCategoriesAsync(bool includeArchived = false)
    {
        var categories = State.Categories.Values
            .Where(c => includeArchived || !c.IsArchived)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new RecipeCategoryDocumentSummary(
                DocumentId: c.DocumentId,
                Name: c.Name,
                DisplayOrder: c.DisplayOrder,
                Color: c.Color,
                HasDraft: c.HasDraft,
                IsArchived: c.IsArchived,
                RecipeCount: c.RecipeCount,
                LastModified: c.LastModified))
            .ToList();

        return Task.FromResult<IReadOnlyList<RecipeCategoryDocumentSummary>>(categories);
    }

    public Task<IReadOnlyList<RecipeDocumentSummary>> SearchRecipesAsync(string query, int take = 20)
    {
        var lowerQuery = query.ToLowerInvariant();
        var recipes = State.Recipes.Values
            .Where(r => !r.IsArchived)
            .Where(r => r.Name.ToLowerInvariant().Contains(lowerQuery))
            .Take(take)
            .Select(r => new RecipeDocumentSummary(
                DocumentId: r.DocumentId,
                Name: r.Name,
                CostPerPortion: r.CostPerPortion,
                CategoryId: r.CategoryId,
                HasDraft: r.HasDraft,
                IsArchived: r.IsArchived,
                PublishedVersion: r.PublishedVersion,
                LastModified: r.LastModified,
                LinkedMenuItemCount: r.LinkedMenuItemCount))
            .ToList();

        return Task.FromResult<IReadOnlyList<RecipeDocumentSummary>>(recipes);
    }

    private async Task EnsureInitializedAsync()
    {
        if (!State.IsCreated)
        {
            var key = this.GetPrimaryKeyString();
            var parts = key.Split(':');
            State.OrgId = Guid.Parse(parts[0]);
            State.IsCreated = true;
            await _state.WriteStateAsync();
        }
    }
}
