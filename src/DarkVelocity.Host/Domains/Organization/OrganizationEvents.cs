using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for organization domain events.
/// </summary>
public interface IOrganizationEvent
{
    DateTime OccurredAt { get; }
}

/// <summary>
/// Organization was created.
/// </summary>
[GenerateSerializer]
public sealed record OrganizationCreated : IOrganizationEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public string Name { get; init; } = string.Empty;
    [Id(2)] public string Slug { get; init; } = string.Empty;
    [Id(3)] public OrganizationSettings Settings { get; init; } = new();
    [Id(4)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Organization name or settings were updated.
/// </summary>
[GenerateSerializer]
public sealed record OrganizationUpdated : IOrganizationEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public string? Name { get; init; }
    [Id(2)] public OrganizationSettings? Settings { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Organization was suspended.
/// </summary>
[GenerateSerializer]
public sealed record OrganizationSuspended : IOrganizationEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public string Reason { get; init; } = string.Empty;
    [Id(2)] public Guid? SuspendedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Organization was reactivated from suspension.
/// </summary>
[GenerateSerializer]
public sealed record OrganizationReactivated : IOrganizationEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public Guid? ReactivatedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Site was added to organization.
/// </summary>
[GenerateSerializer]
public sealed record SiteAddedToOrganization : IOrganizationEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Site was removed from organization.
/// </summary>
[GenerateSerializer]
public sealed record SiteRemovedFromOrganization : IOrganizationEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Organization branding was updated.
/// </summary>
[GenerateSerializer]
public sealed record OrganizationBrandingUpdated : IOrganizationEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public OrganizationBranding Branding { get; init; } = new();
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Organization feature flag was updated.
/// </summary>
[GenerateSerializer]
public sealed record OrganizationFeatureFlagSet : IOrganizationEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public string FeatureName { get; init; } = string.Empty;
    [Id(2)] public bool Enabled { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Organization custom domain was configured.
/// </summary>
[GenerateSerializer]
public sealed record OrganizationCustomDomainConfigured : IOrganizationEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public string Domain { get; init; } = string.Empty;
    [Id(2)] public bool Verified { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Organization cancellation was initiated.
/// </summary>
[GenerateSerializer]
public sealed record OrganizationCancellationInitiated : IOrganizationEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public DateTime EffectiveDate { get; init; }
    [Id(2)] public string? Reason { get; init; }
    [Id(3)] public bool Immediate { get; init; }
    [Id(4)] public Guid? InitiatedBy { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Organization was cancelled and marked for deletion.
/// </summary>
[GenerateSerializer]
public sealed record OrganizationCancelled : IOrganizationEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public DateTime DataRetentionEndDate { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Organization was reactivated from cancellation.
/// </summary>
[GenerateSerializer]
public sealed record OrganizationReactivatedFromCancellation : IOrganizationEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public Guid? ReactivatedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Organization slug was changed.
/// </summary>
[GenerateSerializer]
public sealed record OrganizationSlugChanged : IOrganizationEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public string OldSlug { get; init; } = string.Empty;
    [Id(2)] public string NewSlug { get; init; } = string.Empty;
    [Id(3)] public DateTime OccurredAt { get; init; }
}
