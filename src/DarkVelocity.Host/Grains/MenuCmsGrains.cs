using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Menu Item Document Grain Implementation
// ============================================================================

/// <summary>
/// Grain for menu item document management with versioning and workflow.
/// Provides CMS-like functionality: draft/publish, versioning, scheduling, localization.
/// </summary>
public class MenuItemDocumentGrain : Grain, IMenuItemDocumentGrain
{
    private readonly IPersistentState<MenuItemDocumentState> _state;

    public MenuItemDocumentGrain(
        [PersistentState("menuItemDocument", "OrleansStorage")]
        IPersistentState<MenuItemDocumentState> state)
    {
        _state = state;
    }

    public async Task<MenuItemDocumentSnapshot> CreateAsync(CreateMenuItemDocumentCommand command)
    {
        if (_state.State.IsCreated)
            throw new InvalidOperationException("Menu item document already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var documentId = parts[2];

        var version = new MenuItemVersionState
        {
            VersionNumber = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = command.CreatedBy,
            ChangeNote = "Initial creation",
            Content = new LocalizedContent
            {
                DefaultLocale = command.Locale,
                Translations = new Dictionary<string, LocalizedStrings>
                {
                    [command.Locale] = new LocalizedStrings
                    {
                        Name = command.Name,
                        Description = command.Description
                    }
                }
            },
            Pricing = new PricingInfo
            {
                BasePrice = command.Price
            },
            Media = !string.IsNullOrEmpty(command.ImageUrl)
                ? new MediaInfo { PrimaryImageUrl = command.ImageUrl }
                : null,
            CategoryId = command.CategoryId,
            AccountingGroupId = command.AccountingGroupId,
            RecipeId = command.RecipeId,
            Sku = command.Sku,
            TrackInventory = command.TrackInventory
        };

        _state.State = new MenuItemDocumentState
        {
            OrgId = orgId,
            DocumentId = documentId,
            IsCreated = true,
            CurrentVersion = 1,
            PublishedVersion = command.PublishImmediately ? 1 : null,
            DraftVersion = command.PublishImmediately ? null : 1,
            Versions = [version],
            CreatedAt = DateTimeOffset.UtcNow,
            AuditLog =
            [
                new AuditEntry
                {
                    Action = "Created",
                    UserId = command.CreatedBy,
                    VersionNumber = 1
                }
            ]
        };

        if (command.PublishImmediately)
        {
            _state.State.AuditLog.Add(new AuditEntry
            {
                Action = "Published",
                UserId = command.CreatedBy,
                VersionNumber = 1
            });
        }

        await _state.WriteStateAsync();
        return GetSnapshot();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.IsCreated);
    }

    public async Task<MenuItemVersionSnapshot> CreateDraftAsync(CreateMenuItemDraftCommand command)
    {
        EnsureInitialized();

        // Get base version (either current draft or published)
        var baseVersion = _state.State.DraftVersion.HasValue
            ? _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value)
            : _state.State.Versions.First(v => v.VersionNumber == _state.State.PublishedVersion);

        var newVersionNumber = _state.State.CurrentVersion + 1;

        var newVersion = new MenuItemVersionState
        {
            VersionNumber = newVersionNumber,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = command.CreatedBy,
            ChangeNote = command.ChangeNote,
            Content = CloneContent(baseVersion.Content),
            Pricing = new PricingInfo
            {
                BasePrice = command.Price ?? baseVersion.Pricing.BasePrice,
                CostPrice = baseVersion.Pricing.CostPrice,
                Currency = baseVersion.Pricing.Currency
            },
            Media = baseVersion.Media != null ? new MediaInfo
            {
                PrimaryImageUrl = command.ImageUrl ?? baseVersion.Media.PrimaryImageUrl,
                ThumbnailUrl = baseVersion.Media.ThumbnailUrl,
                AdditionalImageUrls = [.. baseVersion.Media.AdditionalImageUrls]
            } : command.ImageUrl != null ? new MediaInfo { PrimaryImageUrl = command.ImageUrl } : null,
            CategoryId = command.CategoryId ?? baseVersion.CategoryId,
            AccountingGroupId = command.AccountingGroupId ?? baseVersion.AccountingGroupId,
            RecipeId = command.RecipeId ?? baseVersion.RecipeId,
            ModifierBlockIds = command.ModifierBlockIds?.ToList() ?? [.. baseVersion.ModifierBlockIds],
            TagIds = command.TagIds?.ToList() ?? [.. baseVersion.TagIds],
            Sku = command.Sku ?? baseVersion.Sku,
            TrackInventory = command.TrackInventory ?? baseVersion.TrackInventory,
            DisplayOrder = baseVersion.DisplayOrder
        };

        // Update name/description if provided
        if (command.Name != null)
        {
            var defaultLocale = newVersion.Content.DefaultLocale;
            newVersion.Content.Translations[defaultLocale].Name = command.Name;
        }
        if (command.Description != null)
        {
            var defaultLocale = newVersion.Content.DefaultLocale;
            newVersion.Content.Translations[defaultLocale].Description = command.Description;
        }

        _state.State.Versions.Add(newVersion);
        _state.State.CurrentVersion = newVersionNumber;
        _state.State.DraftVersion = newVersionNumber;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "DraftCreated",
            UserId = command.CreatedBy,
            Note = command.ChangeNote,
            VersionNumber = newVersionNumber
        });

        await _state.WriteStateAsync();
        return ToVersionSnapshot(newVersion);
    }

    public Task<MenuItemVersionSnapshot?> GetVersionAsync(int version)
    {
        EnsureInitialized();
        var v = _state.State.Versions.FirstOrDefault(x => x.VersionNumber == version);
        return Task.FromResult(v != null ? ToVersionSnapshot(v) : null);
    }

    public Task<MenuItemVersionSnapshot?> GetPublishedAsync()
    {
        EnsureInitialized();
        if (!_state.State.PublishedVersion.HasValue)
            return Task.FromResult<MenuItemVersionSnapshot?>(null);

        var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.PublishedVersion.Value);
        return Task.FromResult<MenuItemVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<MenuItemVersionSnapshot?> GetDraftAsync()
    {
        EnsureInitialized();
        if (!_state.State.DraftVersion.HasValue)
            return Task.FromResult<MenuItemVersionSnapshot?>(null);

        var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.DraftVersion.Value);
        return Task.FromResult<MenuItemVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<IReadOnlyList<MenuItemVersionSnapshot>> GetVersionHistoryAsync(int skip = 0, int take = 20)
    {
        EnsureInitialized();
        var versions = _state.State.Versions
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

        if (!_state.State.DraftVersion.HasValue)
            throw new InvalidOperationException("No draft to publish");

        var previousPublished = _state.State.PublishedVersion;
        _state.State.PublishedVersion = _state.State.DraftVersion;
        _state.State.DraftVersion = null;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Published",
            UserId = publishedBy,
            Note = note ?? $"Published version {_state.State.PublishedVersion} (previously {previousPublished})",
            VersionNumber = _state.State.PublishedVersion
        });

        await _state.WriteStateAsync();
    }

    public async Task DiscardDraftAsync()
    {
        EnsureInitialized();

        if (!_state.State.DraftVersion.HasValue)
            return;

        var draftVersion = _state.State.DraftVersion.Value;
        _state.State.Versions.RemoveAll(v => v.VersionNumber == draftVersion);
        _state.State.DraftVersion = null;

        // Recalculate current version
        _state.State.CurrentVersion = _state.State.Versions.Max(v => v.VersionNumber);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "DraftDiscarded",
            VersionNumber = draftVersion
        });

        await _state.WriteStateAsync();
    }

    public async Task RevertToVersionAsync(int version, Guid? revertedBy = null, string? reason = null)
    {
        EnsureInitialized();

        var targetVersion = _state.State.Versions.FirstOrDefault(v => v.VersionNumber == version)
            ?? throw new InvalidOperationException($"Version {version} not found");

        var newVersionNumber = _state.State.CurrentVersion + 1;

        // Create a new version that's a copy of the target version
        var revertedVersion = new MenuItemVersionState
        {
            VersionNumber = newVersionNumber,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = revertedBy,
            ChangeNote = reason ?? $"Reverted from version {_state.State.PublishedVersion} to version {version}",
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

        _state.State.Versions.Add(revertedVersion);
        _state.State.CurrentVersion = newVersionNumber;
        _state.State.PublishedVersion = newVersionNumber;
        _state.State.DraftVersion = null;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Reverted",
            UserId = revertedBy,
            Note = reason ?? $"Reverted to version {version}",
            VersionNumber = newVersionNumber
        });

        await _state.WriteStateAsync();
    }

    public async Task AddTranslationAsync(AddMenuItemTranslationCommand command)
    {
        EnsureInitialized();

        // Add to draft if exists, otherwise to published
        var targetVersion = _state.State.DraftVersion.HasValue
            ? _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value)
            : _state.State.Versions.First(v => v.VersionNumber == _state.State.PublishedVersion);

        targetVersion.Content.Translations[command.Locale] = new LocalizedStrings
        {
            Name = command.Name,
            Description = command.Description,
            KitchenName = command.KitchenName
        };

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = $"TranslationAdded:{command.Locale}",
            VersionNumber = targetVersion.VersionNumber
        });

        await _state.WriteStateAsync();
    }

    public async Task RemoveTranslationAsync(string locale)
    {
        EnsureInitialized();

        var targetVersion = _state.State.DraftVersion.HasValue
            ? _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value)
            : _state.State.Versions.First(v => v.VersionNumber == _state.State.PublishedVersion);

        if (locale == targetVersion.Content.DefaultLocale)
            throw new InvalidOperationException("Cannot remove default locale translation");

        targetVersion.Content.Translations.Remove(locale);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = $"TranslationRemoved:{locale}",
            VersionNumber = targetVersion.VersionNumber
        });

        await _state.WriteStateAsync();
    }

    public async Task<ScheduledChange> ScheduleChangeAsync(int version, DateTimeOffset activateAt, DateTimeOffset? deactivateAt = null, string? name = null)
    {
        EnsureInitialized();

        if (!_state.State.Versions.Any(v => v.VersionNumber == version))
            throw new InvalidOperationException($"Version {version} not found");

        var schedule = new ScheduledChange
        {
            ScheduleId = Guid.NewGuid().ToString(),
            VersionToActivate = version,
            ActivateAt = activateAt,
            DeactivateAt = deactivateAt,
            Name = name,
            IsActive = true
        };

        _state.State.Schedules.Add(schedule);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "ScheduleCreated",
            Note = $"Scheduled version {version} for {activateAt}",
            VersionNumber = version
        });

        await _state.WriteStateAsync();
        return schedule;
    }

    public async Task CancelScheduleAsync(string scheduleId)
    {
        EnsureInitialized();

        var schedule = _state.State.Schedules.FirstOrDefault(s => s.ScheduleId == scheduleId);
        if (schedule != null)
        {
            _state.State.Schedules.Remove(schedule);

            _state.State.AuditLog.Add(new AuditEntry
            {
                Action = "ScheduleCancelled",
                Note = $"Cancelled schedule {scheduleId}"
            });

            await _state.WriteStateAsync();
        }
    }

    public Task<IReadOnlyList<ScheduledChange>> GetSchedulesAsync()
    {
        EnsureInitialized();
        return Task.FromResult<IReadOnlyList<ScheduledChange>>(_state.State.Schedules.Where(s => s.IsActive).ToList());
    }

    public async Task ArchiveAsync(Guid? archivedBy = null, string? reason = null)
    {
        EnsureInitialized();

        _state.State.IsArchived = true;
        _state.State.ArchivedAt = DateTimeOffset.UtcNow;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Archived",
            UserId = archivedBy,
            Note = reason
        });

        await _state.WriteStateAsync();
    }

    public async Task RestoreAsync(Guid? restoredBy = null)
    {
        EnsureInitialized();

        _state.State.IsArchived = false;
        _state.State.ArchivedAt = null;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Restored",
            UserId = restoredBy
        });

        await _state.WriteStateAsync();
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
        var activeSchedule = _state.State.Schedules
            .Where(s => s.IsActive && s.ActivateAt <= when)
            .Where(s => !s.DeactivateAt.HasValue || s.DeactivateAt.Value > when)
            .OrderByDescending(s => s.ActivateAt)
            .FirstOrDefault();

        if (activeSchedule != null)
        {
            var v = _state.State.Versions.FirstOrDefault(x => x.VersionNumber == activeSchedule.VersionToActivate);
            return Task.FromResult(v != null ? ToVersionSnapshot(v) : null);
        }

        // Return published version
        if (_state.State.PublishedVersion.HasValue)
        {
            var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.PublishedVersion.Value);
            return Task.FromResult<MenuItemVersionSnapshot?>(ToVersionSnapshot(v));
        }

        return Task.FromResult<MenuItemVersionSnapshot?>(null);
    }

    private MenuItemDocumentSnapshot GetSnapshot()
    {
        MenuItemVersionSnapshot? published = null;
        MenuItemVersionSnapshot? draft = null;

        if (_state.State.PublishedVersion.HasValue)
        {
            var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.PublishedVersion.Value);
            published = ToVersionSnapshot(v);
        }

        if (_state.State.DraftVersion.HasValue)
        {
            var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.DraftVersion.Value);
            draft = ToVersionSnapshot(v);
        }

        return new MenuItemDocumentSnapshot(
            DocumentId: _state.State.DocumentId,
            OrgId: _state.State.OrgId,
            CurrentVersion: _state.State.CurrentVersion,
            PublishedVersion: _state.State.PublishedVersion,
            DraftVersion: _state.State.DraftVersion,
            IsArchived: _state.State.IsArchived,
            CreatedAt: _state.State.CreatedAt,
            Published: published,
            Draft: draft,
            Schedules: _state.State.Schedules.Where(s => s.IsActive).ToList(),
            TotalVersions: _state.State.Versions.Count);
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
        if (!_state.State.IsCreated)
            throw new InvalidOperationException("Menu item document not initialized");
    }
}

// ============================================================================
// Menu Category Document Grain Implementation
// ============================================================================

/// <summary>
/// Grain for menu category document management with versioning and workflow.
/// </summary>
public class MenuCategoryDocumentGrain : Grain, IMenuCategoryDocumentGrain
{
    private readonly IPersistentState<MenuCategoryDocumentState> _state;

    public MenuCategoryDocumentGrain(
        [PersistentState("menuCategoryDocument", "OrleansStorage")]
        IPersistentState<MenuCategoryDocumentState> state)
    {
        _state = state;
    }

    public async Task<MenuCategoryDocumentSnapshot> CreateAsync(CreateMenuCategoryDocumentCommand command)
    {
        if (_state.State.IsCreated)
            throw new InvalidOperationException("Menu category document already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var documentId = parts[2];

        var version = new MenuCategoryVersionState
        {
            VersionNumber = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = command.CreatedBy,
            ChangeNote = "Initial creation",
            Content = new LocalizedContent
            {
                DefaultLocale = command.Locale,
                Translations = new Dictionary<string, LocalizedStrings>
                {
                    [command.Locale] = new LocalizedStrings
                    {
                        Name = command.Name,
                        Description = command.Description
                    }
                }
            },
            Color = command.Color,
            IconUrl = command.IconUrl,
            DisplayOrder = command.DisplayOrder
        };

        _state.State = new MenuCategoryDocumentState
        {
            OrgId = orgId,
            DocumentId = documentId,
            IsCreated = true,
            CurrentVersion = 1,
            PublishedVersion = command.PublishImmediately ? 1 : null,
            DraftVersion = command.PublishImmediately ? null : 1,
            Versions = [version],
            CreatedAt = DateTimeOffset.UtcNow,
            AuditLog =
            [
                new AuditEntry
                {
                    Action = "Created",
                    UserId = command.CreatedBy,
                    VersionNumber = 1
                }
            ]
        };

        if (command.PublishImmediately)
        {
            _state.State.AuditLog.Add(new AuditEntry
            {
                Action = "Published",
                UserId = command.CreatedBy,
                VersionNumber = 1
            });
        }

        await _state.WriteStateAsync();
        return GetSnapshot();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.IsCreated);
    }

    public async Task<MenuCategoryVersionSnapshot> CreateDraftAsync(CreateMenuCategoryDraftCommand command)
    {
        EnsureInitialized();

        var baseVersion = _state.State.DraftVersion.HasValue
            ? _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value)
            : _state.State.Versions.First(v => v.VersionNumber == _state.State.PublishedVersion);

        var newVersionNumber = _state.State.CurrentVersion + 1;

        var newVersion = new MenuCategoryVersionState
        {
            VersionNumber = newVersionNumber,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = command.CreatedBy,
            ChangeNote = command.ChangeNote,
            Content = CloneContent(baseVersion.Content),
            Color = command.Color ?? baseVersion.Color,
            IconUrl = command.IconUrl ?? baseVersion.IconUrl,
            DisplayOrder = command.DisplayOrder ?? baseVersion.DisplayOrder,
            ItemDocumentIds = command.ItemDocumentIds?.ToList() ?? [.. baseVersion.ItemDocumentIds]
        };

        if (command.Name != null)
        {
            var defaultLocale = newVersion.Content.DefaultLocale;
            newVersion.Content.Translations[defaultLocale].Name = command.Name;
        }
        if (command.Description != null)
        {
            var defaultLocale = newVersion.Content.DefaultLocale;
            newVersion.Content.Translations[defaultLocale].Description = command.Description;
        }

        _state.State.Versions.Add(newVersion);
        _state.State.CurrentVersion = newVersionNumber;
        _state.State.DraftVersion = newVersionNumber;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "DraftCreated",
            UserId = command.CreatedBy,
            Note = command.ChangeNote,
            VersionNumber = newVersionNumber
        });

        await _state.WriteStateAsync();
        return ToVersionSnapshot(newVersion);
    }

    public Task<MenuCategoryVersionSnapshot?> GetVersionAsync(int version)
    {
        EnsureInitialized();
        var v = _state.State.Versions.FirstOrDefault(x => x.VersionNumber == version);
        return Task.FromResult(v != null ? ToVersionSnapshot(v) : null);
    }

    public Task<MenuCategoryVersionSnapshot?> GetPublishedAsync()
    {
        EnsureInitialized();
        if (!_state.State.PublishedVersion.HasValue)
            return Task.FromResult<MenuCategoryVersionSnapshot?>(null);

        var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.PublishedVersion.Value);
        return Task.FromResult<MenuCategoryVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<MenuCategoryVersionSnapshot?> GetDraftAsync()
    {
        EnsureInitialized();
        if (!_state.State.DraftVersion.HasValue)
            return Task.FromResult<MenuCategoryVersionSnapshot?>(null);

        var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.DraftVersion.Value);
        return Task.FromResult<MenuCategoryVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<IReadOnlyList<MenuCategoryVersionSnapshot>> GetVersionHistoryAsync(int skip = 0, int take = 20)
    {
        EnsureInitialized();
        var versions = _state.State.Versions
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

        if (!_state.State.DraftVersion.HasValue)
            throw new InvalidOperationException("No draft to publish");

        var previousPublished = _state.State.PublishedVersion;
        _state.State.PublishedVersion = _state.State.DraftVersion;
        _state.State.DraftVersion = null;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Published",
            UserId = publishedBy,
            Note = note ?? $"Published version {_state.State.PublishedVersion} (previously {previousPublished})",
            VersionNumber = _state.State.PublishedVersion
        });

        await _state.WriteStateAsync();
    }

    public async Task DiscardDraftAsync()
    {
        EnsureInitialized();

        if (!_state.State.DraftVersion.HasValue)
            return;

        var draftVersion = _state.State.DraftVersion.Value;
        _state.State.Versions.RemoveAll(v => v.VersionNumber == draftVersion);
        _state.State.DraftVersion = null;
        _state.State.CurrentVersion = _state.State.Versions.Max(v => v.VersionNumber);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "DraftDiscarded",
            VersionNumber = draftVersion
        });

        await _state.WriteStateAsync();
    }

    public async Task RevertToVersionAsync(int version, Guid? revertedBy = null, string? reason = null)
    {
        EnsureInitialized();

        var targetVersion = _state.State.Versions.FirstOrDefault(v => v.VersionNumber == version)
            ?? throw new InvalidOperationException($"Version {version} not found");

        var newVersionNumber = _state.State.CurrentVersion + 1;

        var revertedVersion = new MenuCategoryVersionState
        {
            VersionNumber = newVersionNumber,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = revertedBy,
            ChangeNote = reason ?? $"Reverted from version {_state.State.PublishedVersion} to version {version}",
            Content = CloneContent(targetVersion.Content),
            Color = targetVersion.Color,
            IconUrl = targetVersion.IconUrl,
            DisplayOrder = targetVersion.DisplayOrder,
            ItemDocumentIds = [.. targetVersion.ItemDocumentIds]
        };

        _state.State.Versions.Add(revertedVersion);
        _state.State.CurrentVersion = newVersionNumber;
        _state.State.PublishedVersion = newVersionNumber;
        _state.State.DraftVersion = null;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Reverted",
            UserId = revertedBy,
            Note = reason ?? $"Reverted to version {version}",
            VersionNumber = newVersionNumber
        });

        await _state.WriteStateAsync();
    }

    public async Task AddItemAsync(string itemDocumentId)
    {
        EnsureInitialized();

        var targetVersion = _state.State.DraftVersion.HasValue
            ? _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value)
            : _state.State.Versions.First(v => v.VersionNumber == _state.State.PublishedVersion);

        if (!targetVersion.ItemDocumentIds.Contains(itemDocumentId))
        {
            targetVersion.ItemDocumentIds.Add(itemDocumentId);
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveItemAsync(string itemDocumentId)
    {
        EnsureInitialized();

        var targetVersion = _state.State.DraftVersion.HasValue
            ? _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value)
            : _state.State.Versions.First(v => v.VersionNumber == _state.State.PublishedVersion);

        targetVersion.ItemDocumentIds.Remove(itemDocumentId);
        await _state.WriteStateAsync();
    }

    public async Task ReorderItemsAsync(IReadOnlyList<string> itemDocumentIds)
    {
        EnsureInitialized();

        var targetVersion = _state.State.DraftVersion.HasValue
            ? _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value)
            : _state.State.Versions.First(v => v.VersionNumber == _state.State.PublishedVersion);

        targetVersion.ItemDocumentIds = itemDocumentIds.ToList();
        await _state.WriteStateAsync();
    }

    public async Task<ScheduledChange> ScheduleChangeAsync(int version, DateTimeOffset activateAt, DateTimeOffset? deactivateAt = null, string? name = null)
    {
        EnsureInitialized();

        if (!_state.State.Versions.Any(v => v.VersionNumber == version))
            throw new InvalidOperationException($"Version {version} not found");

        var schedule = new ScheduledChange
        {
            ScheduleId = Guid.NewGuid().ToString(),
            VersionToActivate = version,
            ActivateAt = activateAt,
            DeactivateAt = deactivateAt,
            Name = name,
            IsActive = true
        };

        _state.State.Schedules.Add(schedule);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "ScheduleCreated",
            Note = $"Scheduled version {version} for {activateAt}",
            VersionNumber = version
        });

        await _state.WriteStateAsync();
        return schedule;
    }

    public async Task CancelScheduleAsync(string scheduleId)
    {
        EnsureInitialized();

        var schedule = _state.State.Schedules.FirstOrDefault(s => s.ScheduleId == scheduleId);
        if (schedule != null)
        {
            _state.State.Schedules.Remove(schedule);
            _state.State.AuditLog.Add(new AuditEntry
            {
                Action = "ScheduleCancelled",
                Note = $"Cancelled schedule {scheduleId}"
            });
            await _state.WriteStateAsync();
        }
    }

    public async Task ArchiveAsync(Guid? archivedBy = null, string? reason = null)
    {
        EnsureInitialized();

        _state.State.IsArchived = true;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Archived",
            UserId = archivedBy,
            Note = reason
        });

        await _state.WriteStateAsync();
    }

    public async Task RestoreAsync(Guid? restoredBy = null)
    {
        EnsureInitialized();

        _state.State.IsArchived = false;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Restored",
            UserId = restoredBy
        });

        await _state.WriteStateAsync();
    }

    public Task<MenuCategoryDocumentSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(GetSnapshot());
    }

    private MenuCategoryDocumentSnapshot GetSnapshot()
    {
        MenuCategoryVersionSnapshot? published = null;
        MenuCategoryVersionSnapshot? draft = null;

        if (_state.State.PublishedVersion.HasValue)
        {
            var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.PublishedVersion.Value);
            published = ToVersionSnapshot(v);
        }

        if (_state.State.DraftVersion.HasValue)
        {
            var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.DraftVersion.Value);
            draft = ToVersionSnapshot(v);
        }

        return new MenuCategoryDocumentSnapshot(
            DocumentId: _state.State.DocumentId,
            OrgId: _state.State.OrgId,
            CurrentVersion: _state.State.CurrentVersion,
            PublishedVersion: _state.State.PublishedVersion,
            DraftVersion: _state.State.DraftVersion,
            IsArchived: _state.State.IsArchived,
            CreatedAt: _state.State.CreatedAt,
            Published: published,
            Draft: draft,
            Schedules: _state.State.Schedules.Where(s => s.IsActive).ToList(),
            TotalVersions: _state.State.Versions.Count);
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
        if (!_state.State.IsCreated)
            throw new InvalidOperationException("Menu category document not initialized");
    }
}

// ============================================================================
// Modifier Block Grain Implementation
// ============================================================================

/// <summary>
/// Grain for reusable modifier block management.
/// </summary>
public class ModifierBlockGrain : Grain, IModifierBlockGrain
{
    private readonly IPersistentState<ModifierBlockState> _state;

    public ModifierBlockGrain(
        [PersistentState("modifierBlock", "OrleansStorage")]
        IPersistentState<ModifierBlockState> state)
    {
        _state = state;
    }

    public async Task<ModifierBlockSnapshot> CreateAsync(CreateModifierBlockCommand command)
    {
        if (_state.State.IsCreated)
            throw new InvalidOperationException("Modifier block already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var blockId = parts[2];

        var options = command.Options?.Select((o, i) => new ModifierOptionState
        {
            OptionId = Guid.NewGuid().ToString(),
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

        var version = new ModifierBlockVersionState
        {
            VersionNumber = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = command.CreatedBy,
            ChangeNote = "Initial creation",
            Content = new LocalizedContent
            {
                Translations = new Dictionary<string, LocalizedStrings>
                {
                    ["en-US"] = new LocalizedStrings { Name = command.Name }
                }
            },
            SelectionRule = command.SelectionRule,
            MinSelections = command.MinSelections,
            MaxSelections = command.MaxSelections,
            IsRequired = command.IsRequired,
            Options = options
        };

        _state.State = new ModifierBlockState
        {
            OrgId = orgId,
            BlockId = blockId,
            IsCreated = true,
            CurrentVersion = 1,
            PublishedVersion = command.PublishImmediately ? 1 : null,
            DraftVersion = command.PublishImmediately ? null : 1,
            Versions = [version],
            CreatedAt = DateTimeOffset.UtcNow,
            AuditLog =
            [
                new AuditEntry
                {
                    Action = "Created",
                    UserId = command.CreatedBy,
                    VersionNumber = 1
                }
            ]
        };

        await _state.WriteStateAsync();
        return GetSnapshot();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.IsCreated);
    }

    public async Task<ModifierBlockVersionSnapshot> CreateDraftAsync(CreateModifierBlockDraftCommand command)
    {
        EnsureInitialized();

        var baseVersion = _state.State.DraftVersion.HasValue
            ? _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value)
            : _state.State.Versions.First(v => v.VersionNumber == _state.State.PublishedVersion);

        var newVersionNumber = _state.State.CurrentVersion + 1;

        var options = command.Options?.Select((o, i) => new ModifierOptionState
        {
            OptionId = Guid.NewGuid().ToString(),
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

        var newVersion = new ModifierBlockVersionState
        {
            VersionNumber = newVersionNumber,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = command.CreatedBy,
            ChangeNote = command.ChangeNote,
            Content = new LocalizedContent
            {
                Translations = new Dictionary<string, LocalizedStrings>
                {
                    ["en-US"] = new LocalizedStrings
                    {
                        Name = command.Name ?? baseVersion.Content.GetStrings().Name
                    }
                }
            },
            SelectionRule = command.SelectionRule ?? baseVersion.SelectionRule,
            MinSelections = command.MinSelections ?? baseVersion.MinSelections,
            MaxSelections = command.MaxSelections ?? baseVersion.MaxSelections,
            IsRequired = command.IsRequired ?? baseVersion.IsRequired,
            Options = options
        };

        _state.State.Versions.Add(newVersion);
        _state.State.CurrentVersion = newVersionNumber;
        _state.State.DraftVersion = newVersionNumber;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "DraftCreated",
            UserId = command.CreatedBy,
            Note = command.ChangeNote,
            VersionNumber = newVersionNumber
        });

        await _state.WriteStateAsync();
        return ToVersionSnapshot(newVersion);
    }

    public Task<ModifierBlockVersionSnapshot?> GetVersionAsync(int version)
    {
        EnsureInitialized();
        var v = _state.State.Versions.FirstOrDefault(x => x.VersionNumber == version);
        return Task.FromResult(v != null ? ToVersionSnapshot(v) : null);
    }

    public Task<ModifierBlockVersionSnapshot?> GetPublishedAsync()
    {
        EnsureInitialized();
        if (!_state.State.PublishedVersion.HasValue)
            return Task.FromResult<ModifierBlockVersionSnapshot?>(null);

        var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.PublishedVersion.Value);
        return Task.FromResult<ModifierBlockVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<ModifierBlockVersionSnapshot?> GetDraftAsync()
    {
        EnsureInitialized();
        if (!_state.State.DraftVersion.HasValue)
            return Task.FromResult<ModifierBlockVersionSnapshot?>(null);

        var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.DraftVersion.Value);
        return Task.FromResult<ModifierBlockVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public async Task PublishDraftAsync(Guid? publishedBy = null, string? note = null)
    {
        EnsureInitialized();

        if (!_state.State.DraftVersion.HasValue)
            throw new InvalidOperationException("No draft to publish");

        _state.State.PublishedVersion = _state.State.DraftVersion;
        _state.State.DraftVersion = null;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Published",
            UserId = publishedBy,
            Note = note,
            VersionNumber = _state.State.PublishedVersion
        });

        await _state.WriteStateAsync();
    }

    public async Task DiscardDraftAsync()
    {
        EnsureInitialized();

        if (!_state.State.DraftVersion.HasValue)
            return;

        var draftVersion = _state.State.DraftVersion.Value;
        _state.State.Versions.RemoveAll(v => v.VersionNumber == draftVersion);
        _state.State.DraftVersion = null;
        _state.State.CurrentVersion = _state.State.Versions.Max(v => v.VersionNumber);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "DraftDiscarded",
            VersionNumber = draftVersion
        });

        await _state.WriteStateAsync();
    }

    public async Task RegisterUsageAsync(string itemDocumentId)
    {
        EnsureInitialized();

        if (!_state.State.UsedByItemIds.Contains(itemDocumentId))
        {
            _state.State.UsedByItemIds.Add(itemDocumentId);
            await _state.WriteStateAsync();
        }
    }

    public async Task UnregisterUsageAsync(string itemDocumentId)
    {
        EnsureInitialized();
        _state.State.UsedByItemIds.Remove(itemDocumentId);
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<string>> GetUsageAsync()
    {
        EnsureInitialized();
        return Task.FromResult<IReadOnlyList<string>>(_state.State.UsedByItemIds);
    }

    public async Task ArchiveAsync(Guid? archivedBy = null, string? reason = null)
    {
        EnsureInitialized();

        if (_state.State.UsedByItemIds.Count > 0)
            throw new InvalidOperationException($"Cannot archive modifier block that is used by {_state.State.UsedByItemIds.Count} items");

        _state.State.IsArchived = true;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Archived",
            UserId = archivedBy,
            Note = reason
        });

        await _state.WriteStateAsync();
    }

    public async Task RestoreAsync(Guid? restoredBy = null)
    {
        EnsureInitialized();

        _state.State.IsArchived = false;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Restored",
            UserId = restoredBy
        });

        await _state.WriteStateAsync();
    }

    public Task<ModifierBlockSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(GetSnapshot());
    }

    private ModifierBlockSnapshot GetSnapshot()
    {
        ModifierBlockVersionSnapshot? published = null;
        ModifierBlockVersionSnapshot? draft = null;

        if (_state.State.PublishedVersion.HasValue)
        {
            var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.PublishedVersion.Value);
            published = ToVersionSnapshot(v);
        }

        if (_state.State.DraftVersion.HasValue)
        {
            var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.DraftVersion.Value);
            draft = ToVersionSnapshot(v);
        }

        return new ModifierBlockSnapshot(
            BlockId: _state.State.BlockId,
            OrgId: _state.State.OrgId,
            CurrentVersion: _state.State.CurrentVersion,
            PublishedVersion: _state.State.PublishedVersion,
            DraftVersion: _state.State.DraftVersion,
            IsArchived: _state.State.IsArchived,
            CreatedAt: _state.State.CreatedAt,
            Published: published,
            Draft: draft,
            TotalVersions: _state.State.Versions.Count,
            UsedByItemIds: _state.State.UsedByItemIds);
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
        if (!_state.State.IsCreated)
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
