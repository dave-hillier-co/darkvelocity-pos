namespace DarkVelocity.Orleans.Abstractions.State;

public enum OrganizationStatus
{
    Active,
    Suspended,
    Cancelled
}

public record OrganizationSettings
{
    public string DefaultCurrency { get; init; } = "USD";
    public string DefaultTimezone { get; init; } = "America/New_York";
    public string DefaultLocale { get; init; } = "en-US";
    public bool RequirePinForVoids { get; init; } = true;
    public bool RequireManagerApprovalForDiscounts { get; init; } = true;
    public int DataRetentionDays { get; init; } = 365 * 7; // 7 years
}

public record BillingInfo
{
    public string? StripeCustomerId { get; init; }
    public string? SubscriptionId { get; init; }
    public string PlanId { get; init; } = "free";
    public DateTime? TrialEndsAt { get; init; }
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
    [Id(9)] public int Version { get; set; }
}
