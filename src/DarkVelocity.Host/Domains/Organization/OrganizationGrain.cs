using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.EventSourcing;
using Orleans.Providers;
using System.Security.Cryptography;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Journaled grain for Organization management with full event sourcing.
/// All state changes are recorded as events and can be replayed for audit trail.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class OrganizationGrain : JournaledGrain<OrganizationState, IOrganizationEvent>, IOrganizationGrain
{
    /// <summary>
    /// Applies an event to the grain state. This is the core of event sourcing.
    /// </summary>
    protected override void TransitionState(OrganizationState state, IOrganizationEvent @event)
    {
        switch (@event)
        {
            case OrganizationCreated e:
                state.Id = e.OrganizationId;
                state.Name = e.Name;
                state.Slug = e.Slug;
                state.Settings = e.Settings;
                state.Status = OrganizationStatus.Active;
                state.CreatedAt = e.OccurredAt;
                break;

            case OrganizationUpdated e:
                if (e.Name != null)
                    state.Name = e.Name;
                if (e.Settings != null)
                    state.Settings = e.Settings;
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrganizationSuspended e:
                state.Status = OrganizationStatus.Suspended;
                state.SuspensionReason = e.Reason;
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrganizationReactivated e:
                state.Status = OrganizationStatus.Active;
                state.SuspensionReason = null;
                state.UpdatedAt = e.OccurredAt;
                break;

            case SiteAddedToOrganization e:
                if (!state.SiteIds.Contains(e.SiteId))
                    state.SiteIds.Add(e.SiteId);
                state.UpdatedAt = e.OccurredAt;
                break;

            case SiteRemovedFromOrganization e:
                state.SiteIds.Remove(e.SiteId);
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrganizationBrandingUpdated e:
                state.Branding = e.Branding;
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrganizationFeatureFlagSet e:
                state.FeatureFlags[e.FeatureName] = e.Enabled;
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrganizationCustomDomainConfigured e:
                state.CustomDomain = new CustomDomainConfig
                {
                    Domain = e.Domain,
                    Verified = e.Verified,
                    VerifiedAt = e.Verified ? e.OccurredAt : null,
                    VerificationToken = e.Verified ? null : GenerateVerificationToken(),
                    LastVerificationAttempt = e.Verified ? e.OccurredAt : null
                };
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrganizationCancellationInitiated e:
                state.Status = OrganizationStatus.PendingCancellation;
                state.Cancellation = new CancellationDetails
                {
                    InitiatedAt = e.OccurredAt,
                    EffectiveDate = e.EffectiveDate,
                    DataRetentionEndDate = e.EffectiveDate.AddDays(state.Settings.DataRetentionDays),
                    Reason = e.Reason,
                    InitiatedBy = e.InitiatedBy,
                    Immediate = e.Immediate
                };
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrganizationCancelled e:
                state.Status = OrganizationStatus.Cancelled;
                if (state.Cancellation != null)
                {
                    state.Cancellation = state.Cancellation with
                    {
                        DataRetentionEndDate = e.DataRetentionEndDate
                    };
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrganizationReactivatedFromCancellation e:
                state.Status = OrganizationStatus.Active;
                state.Cancellation = null;
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrganizationSlugChanged e:
                state.SlugHistory.Add(e.OldSlug);
                state.Slug = e.NewSlug;
                state.UpdatedAt = e.OccurredAt;
                break;
        }
    }

    public async Task<OrganizationCreatedResult> CreateAsync(CreateOrganizationCommand command)
    {
        if (State.Id != Guid.Empty)
            throw new InvalidOperationException("Organization already exists");

        var orgId = Guid.Parse(this.GetPrimaryKeyString());
        var now = DateTime.UtcNow;

        RaiseEvent(new OrganizationCreated
        {
            OrganizationId = orgId,
            Name = command.Name,
            Slug = command.Slug,
            Settings = command.Settings ?? new OrganizationSettings(),
            OccurredAt = now
        });

        await ConfirmEvents();

        return new OrganizationCreatedResult(orgId, command.Slug, State.CreatedAt);
    }

    public async Task<OrganizationUpdatedResult> UpdateAsync(UpdateOrganizationCommand command)
    {
        EnsureExists();

        RaiseEvent(new OrganizationUpdated
        {
            OrganizationId = State.Id,
            Name = command.Name,
            Settings = command.Settings,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();

        return new OrganizationUpdatedResult(Version, State.UpdatedAt!.Value);
    }

    public Task<OrganizationState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    public async Task SuspendAsync(string reason)
    {
        EnsureExists();

        if (State.Status == OrganizationStatus.Suspended)
            throw new InvalidOperationException("Organization is already suspended");

        RaiseEvent(new OrganizationSuspended
        {
            OrganizationId = State.Id,
            Reason = reason,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task ReactivateAsync()
    {
        EnsureExists();

        if (State.Status != OrganizationStatus.Suspended)
            throw new InvalidOperationException("Organization is not suspended");

        RaiseEvent(new OrganizationReactivated
        {
            OrganizationId = State.Id,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task<Guid> AddSiteAsync(Guid siteId)
    {
        EnsureExists();

        if (!State.SiteIds.Contains(siteId))
        {
            RaiseEvent(new SiteAddedToOrganization
            {
                OrganizationId = State.Id,
                SiteId = siteId,
                OccurredAt = DateTime.UtcNow
            });

            await ConfirmEvents();
        }

        return siteId;
    }

    public async Task RemoveSiteAsync(Guid siteId)
    {
        EnsureExists();

        if (State.SiteIds.Contains(siteId))
        {
            RaiseEvent(new SiteRemovedFromOrganization
            {
                OrganizationId = State.Id,
                SiteId = siteId,
                OccurredAt = DateTime.UtcNow
            });

            await ConfirmEvents();
        }
    }

    public Task<IReadOnlyList<Guid>> GetSiteIdsAsync()
    {
        return Task.FromResult<IReadOnlyList<Guid>>(State.SiteIds);
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(State.Id != Guid.Empty);
    }

    public async Task UpdateBrandingAsync(UpdateBrandingCommand command)
    {
        EnsureExists();

        var branding = new OrganizationBranding
        {
            LogoUrl = command.LogoUrl ?? State.Branding.LogoUrl,
            FaviconUrl = command.FaviconUrl ?? State.Branding.FaviconUrl,
            PrimaryColor = command.PrimaryColor ?? State.Branding.PrimaryColor,
            SecondaryColor = command.SecondaryColor ?? State.Branding.SecondaryColor,
            AccentColor = command.AccentColor ?? State.Branding.AccentColor,
            CustomCss = command.CustomCss ?? State.Branding.CustomCss
        };

        RaiseEvent(new OrganizationBrandingUpdated
        {
            OrganizationId = State.Id,
            Branding = branding,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task ConfigureCustomDomainAsync(string domain)
    {
        EnsureExists();

        if (string.IsNullOrWhiteSpace(domain))
            throw new ArgumentException("Domain cannot be empty", nameof(domain));

        RaiseEvent(new OrganizationCustomDomainConfigured
        {
            OrganizationId = State.Id,
            Domain = domain.ToLowerInvariant(),
            Verified = false,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task VerifyCustomDomainAsync()
    {
        EnsureExists();

        if (State.CustomDomain == null)
            throw new InvalidOperationException("No custom domain configured");

        if (State.CustomDomain.Verified)
            throw new InvalidOperationException("Custom domain is already verified");

        // In a real implementation, this would check DNS records
        // For now, we'll mark it as verified
        RaiseEvent(new OrganizationCustomDomainConfigured
        {
            OrganizationId = State.Id,
            Domain = State.CustomDomain.Domain,
            Verified = true,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task SetFeatureFlagAsync(string featureName, bool enabled)
    {
        EnsureExists();

        if (string.IsNullOrWhiteSpace(featureName))
            throw new ArgumentException("Feature name cannot be empty", nameof(featureName));

        RaiseEvent(new OrganizationFeatureFlagSet
        {
            OrganizationId = State.Id,
            FeatureName = featureName,
            Enabled = enabled,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public Task<bool> GetFeatureFlagAsync(string featureName)
    {
        EnsureExists();

        return Task.FromResult(State.FeatureFlags.TryGetValue(featureName, out var enabled) && enabled);
    }

    public async Task<CancellationResult> InitiateCancellationAsync(InitiateCancellationCommand command)
    {
        EnsureExists();

        if (State.Status == OrganizationStatus.Cancelled)
            throw new InvalidOperationException("Organization is already cancelled");

        if (State.Status == OrganizationStatus.PendingCancellation)
            throw new InvalidOperationException("Cancellation already in progress");

        var now = DateTime.UtcNow;
        var effectiveDate = command.Immediate
            ? now
            : GetBillingPeriodEndDate();
        var dataRetentionEndDate = effectiveDate.AddDays(State.Settings.DataRetentionDays);

        RaiseEvent(new OrganizationCancellationInitiated
        {
            OrganizationId = State.Id,
            EffectiveDate = effectiveDate,
            Reason = command.Reason,
            Immediate = command.Immediate,
            InitiatedBy = command.InitiatedBy,
            OccurredAt = now
        });

        await ConfirmEvents();

        // If immediate, also mark as cancelled
        if (command.Immediate)
        {
            RaiseEvent(new OrganizationCancelled
            {
                OrganizationId = State.Id,
                DataRetentionEndDate = dataRetentionEndDate,
                OccurredAt = now
            });

            await ConfirmEvents();
        }

        return new CancellationResult(effectiveDate, dataRetentionEndDate);
    }

    public async Task ReactivateFromCancellationAsync(Guid? reactivatedBy = null)
    {
        EnsureExists();

        if (State.Status != OrganizationStatus.PendingCancellation && State.Status != OrganizationStatus.Cancelled)
            throw new InvalidOperationException("Organization is not in cancellation state");

        // Check if within grace period
        if (State.Status == OrganizationStatus.Cancelled && State.Cancellation != null)
        {
            if (DateTime.UtcNow > State.Cancellation.DataRetentionEndDate)
                throw new InvalidOperationException("Grace period has expired, reactivation is not possible");
        }

        RaiseEvent(new OrganizationReactivatedFromCancellation
        {
            OrganizationId = State.Id,
            ReactivatedBy = reactivatedBy,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task ChangeSlugAsync(ChangeSlugCommand command)
    {
        EnsureExists();

        if (string.IsNullOrWhiteSpace(command.NewSlug))
            throw new ArgumentException("New slug cannot be empty", nameof(command));

        if (command.NewSlug == State.Slug)
            throw new ArgumentException("New slug must be different from current slug", nameof(command));

        // In a real implementation, we'd check with SlugLookupGrain for uniqueness
        RaiseEvent(new OrganizationSlugChanged
        {
            OrganizationId = State.Id,
            OldSlug = State.Slug,
            NewSlug = command.NewSlug.ToLowerInvariant(),
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public Task<int> GetVersionAsync()
    {
        return Task.FromResult(Version);
    }

    private void EnsureExists()
    {
        if (State.Id == Guid.Empty)
            throw new InvalidOperationException("Organization does not exist");
    }

    private static string GenerateVerificationToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private DateTime GetBillingPeriodEndDate()
    {
        // Returns end of current billing period (end of current month)
        var now = DateTime.UtcNow;
        var lastDayOfMonth = DateTime.DaysInMonth(now.Year, now.Month);
        return new DateTime(now.Year, now.Month, lastDayOfMonth, 23, 59, 59, DateTimeKind.Utc);
    }
}
