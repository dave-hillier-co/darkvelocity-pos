namespace DarkVelocity.Host.State;

public enum OrganizationStatus
{
    Active,
    Suspended,
    PendingCancellation,
    Cancelled,
    Deleted
}

[GenerateSerializer]
public record OrganizationSettings
{
    [Id(0)] public string DefaultCurrency { get; init; } = "USD";
    [Id(1)] public string DefaultTimezone { get; init; } = "America/New_York";
    [Id(2)] public string DefaultLocale { get; init; } = "en-US";
    [Id(3)] public bool RequirePinForVoids { get; init; } = true;
    [Id(4)] public bool RequireManagerApprovalForDiscounts { get; init; } = true;
    [Id(5)] public int DataRetentionDays { get; init; } = 365 * 7; // 7 years
    [Id(6)] public bool AutoClockOutEnabled { get; init; } = false;
    [Id(7)] public int AutoClockOutAfterHours { get; init; } = 12;
}

/// <summary>
/// Organization branding settings for white-labeling.
/// </summary>
[GenerateSerializer]
public record OrganizationBranding
{
    [Id(0)] public string? LogoUrl { get; init; }
    [Id(1)] public string? FaviconUrl { get; init; }
    [Id(2)] public string PrimaryColor { get; init; } = "#1a73e8";
    [Id(3)] public string SecondaryColor { get; init; } = "#4285f4";
    [Id(4)] public string? AccentColor { get; init; }
    [Id(5)] public string? BackgroundColor { get; init; }
    [Id(6)] public string? TextColor { get; init; }
    [Id(7)] public string? CustomCss { get; init; }
}

/// <summary>
/// Custom domain configuration for organization.
/// </summary>
[GenerateSerializer]
public record CustomDomainConfig
{
    [Id(0)] public string Domain { get; init; } = string.Empty;
    [Id(1)] public bool Verified { get; init; }
    [Id(2)] public DateTime? VerifiedAt { get; init; }
    [Id(3)] public string? VerificationToken { get; init; }
    [Id(4)] public DateTime? LastVerificationAttempt { get; init; }
}

/// <summary>
/// Cancellation details for organization.
/// </summary>
[GenerateSerializer]
public record CancellationDetails
{
    [Id(0)] public DateTime InitiatedAt { get; init; }
    [Id(1)] public DateTime EffectiveDate { get; init; }
    [Id(2)] public DateTime DataRetentionEndDate { get; init; }
    [Id(3)] public string? Reason { get; init; }
    [Id(4)] public Guid? InitiatedBy { get; init; }
    [Id(5)] public bool Immediate { get; init; }
}

[GenerateSerializer]
public record BillingInfo
{
    [Id(0)] public string? StripeCustomerId { get; init; }
    [Id(1)] public string? SubscriptionId { get; init; }
    [Id(2)] public string PlanId { get; init; } = "free";
    [Id(3)] public DateTime? TrialEndsAt { get; init; }
}

[GenerateSerializer]
public sealed class OrganizationState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public string Slug { get; set; } = string.Empty;
    [Id(3)] public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;
    [Id(4)] public OrganizationSettings Settings { get; set; } = new();
    [Id(5)] public BillingInfo? Billing { get; set; }
    [Id(6)] public DateTime CreatedAt { get; set; }
    [Id(7)] public DateTime? UpdatedAt { get; set; }
    [Id(8)] public List<Guid> SiteIds { get; set; } = [];

    // Extended fields for Organization domain improvements
    [Id(10)] public OrganizationBranding Branding { get; set; } = new();
    [Id(11)] public CustomDomainConfig? CustomDomain { get; set; }
    [Id(12)] public Dictionary<string, bool> FeatureFlags { get; set; } = [];
    [Id(13)] public CancellationDetails? Cancellation { get; set; }
    [Id(14)] public string? SuspensionReason { get; set; }
    [Id(15)] public List<string> SlugHistory { get; set; } = [];
}
