namespace DarkVelocity.Host.State;

public enum SubscriptionPlan
{
    Free,
    Starter,
    Pro,
    Enterprise
}

public enum SubscriptionStatus
{
    Active,
    Trialing,
    PastDue,
    Cancelled,
    Suspended
}

public enum BillingCycle
{
    Monthly,
    Annual
}

/// <summary>
/// Plan features and limits.
/// </summary>
[GenerateSerializer]
public record PlanLimits
{
    [Id(0)] public int MaxSites { get; init; }
    [Id(1)] public int MaxUsers { get; init; }
    [Id(2)] public int MaxTransactionsPerMonth { get; init; }
    [Id(3)] public int MaxApiCallsPerMonth { get; init; }
    [Id(4)] public long MaxStorageBytes { get; init; }
    [Id(5)] public bool AdvancedReporting { get; init; }
    [Id(6)] public bool CustomBranding { get; init; }
    [Id(7)] public bool ApiAccess { get; init; }
    [Id(8)] public bool PrioritySupport { get; init; }
    [Id(9)] public bool SsoEnabled { get; init; }
    [Id(10)] public bool CustomIntegrations { get; init; }

    public static PlanLimits ForPlan(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Free => new PlanLimits
        {
            MaxSites = 1,
            MaxUsers = 3,
            MaxTransactionsPerMonth = 500,
            MaxApiCallsPerMonth = 1000,
            MaxStorageBytes = 1L * 1024 * 1024 * 1024, // 1 GB
            AdvancedReporting = false,
            CustomBranding = false,
            ApiAccess = false,
            PrioritySupport = false,
            SsoEnabled = false,
            CustomIntegrations = false
        },
        SubscriptionPlan.Starter => new PlanLimits
        {
            MaxSites = 3,
            MaxUsers = 10,
            MaxTransactionsPerMonth = 5000,
            MaxApiCallsPerMonth = 10000,
            MaxStorageBytes = 10L * 1024 * 1024 * 1024, // 10 GB
            AdvancedReporting = false,
            CustomBranding = false,
            ApiAccess = true,
            PrioritySupport = false,
            SsoEnabled = false,
            CustomIntegrations = false
        },
        SubscriptionPlan.Pro => new PlanLimits
        {
            MaxSites = 10,
            MaxUsers = 50,
            MaxTransactionsPerMonth = 50000,
            MaxApiCallsPerMonth = 100000,
            MaxStorageBytes = 100L * 1024 * 1024 * 1024, // 100 GB
            AdvancedReporting = true,
            CustomBranding = true,
            ApiAccess = true,
            PrioritySupport = true,
            SsoEnabled = false,
            CustomIntegrations = false
        },
        SubscriptionPlan.Enterprise => new PlanLimits
        {
            MaxSites = int.MaxValue,
            MaxUsers = int.MaxValue,
            MaxTransactionsPerMonth = int.MaxValue,
            MaxApiCallsPerMonth = int.MaxValue,
            MaxStorageBytes = long.MaxValue,
            AdvancedReporting = true,
            CustomBranding = true,
            ApiAccess = true,
            PrioritySupport = true,
            SsoEnabled = true,
            CustomIntegrations = true
        },
        _ => throw new ArgumentOutOfRangeException(nameof(plan))
    };
}

/// <summary>
/// Current usage metrics for the subscription.
/// </summary>
[GenerateSerializer]
public record UsageMetrics
{
    [Id(0)] public int CurrentSites { get; init; }
    [Id(1)] public int CurrentUsers { get; init; }
    [Id(2)] public int TransactionsThisMonth { get; init; }
    [Id(3)] public int ApiCallsThisMonth { get; init; }
    [Id(4)] public long StorageUsedBytes { get; init; }
    [Id(5)] public DateTime LastUpdated { get; init; }
    [Id(6)] public int PeriodMonth { get; init; }
    [Id(7)] public int PeriodYear { get; init; }
}

/// <summary>
/// Payment method reference (stored at processor, not locally).
/// </summary>
[GenerateSerializer]
public record PaymentMethodInfo
{
    [Id(0)] public string PaymentMethodId { get; init; } = string.Empty;
    [Id(1)] public string Type { get; init; } = string.Empty; // card, bank_account
    [Id(2)] public string Last4 { get; init; } = string.Empty;
    [Id(3)] public string? Brand { get; init; } // visa, mastercard
    [Id(4)] public int? ExpMonth { get; init; }
    [Id(5)] public int? ExpYear { get; init; }
    [Id(6)] public bool IsDefault { get; init; }
    [Id(7)] public DateTime AddedAt { get; init; }
}

/// <summary>
/// Invoice summary.
/// </summary>
[GenerateSerializer]
public record InvoiceSummary
{
    [Id(0)] public string InvoiceId { get; init; } = string.Empty;
    [Id(1)] public decimal Amount { get; init; }
    [Id(2)] public string Currency { get; init; } = "USD";
    [Id(3)] public DateTime PeriodStart { get; init; }
    [Id(4)] public DateTime PeriodEnd { get; init; }
    [Id(5)] public DateTime DueDate { get; init; }
    [Id(6)] public string Status { get; init; } = "pending"; // pending, paid, failed, voided
    [Id(7)] public DateTime? PaidAt { get; init; }
    [Id(8)] public string? HostedInvoiceUrl { get; init; }
}

/// <summary>
/// Subscription state for an organization.
/// </summary>
[GenerateSerializer]
public sealed class SubscriptionState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;
    [Id(2)] public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    [Id(3)] public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;

    // Stripe/processor references
    [Id(4)] public string? StripeCustomerId { get; set; }
    [Id(5)] public string? StripeSubscriptionId { get; set; }

    // Trial period
    [Id(6)] public DateTime? TrialStartedAt { get; set; }
    [Id(7)] public DateTime? TrialEndsAt { get; set; }

    // Billing dates
    [Id(8)] public DateTime? CurrentPeriodStart { get; set; }
    [Id(9)] public DateTime? CurrentPeriodEnd { get; set; }
    [Id(10)] public DateTime? CancelledAt { get; set; }
    [Id(11)] public DateTime? CancelAtPeriodEnd { get; set; }

    // Usage tracking
    [Id(12)] public UsageMetrics Usage { get; set; } = new();
    [Id(13)] public PlanLimits Limits { get; set; } = PlanLimits.ForPlan(SubscriptionPlan.Free);

    // Payment methods
    [Id(14)] public List<PaymentMethodInfo> PaymentMethods { get; set; } = [];
    [Id(15)] public string? DefaultPaymentMethodId { get; set; }

    // Recent invoices
    [Id(16)] public List<InvoiceSummary> RecentInvoices { get; set; } = [];

    // Metadata
    [Id(17)] public DateTime CreatedAt { get; set; }
    [Id(18)] public DateTime? UpdatedAt { get; set; }

    // Price information (cached from pricing configuration)
    [Id(19)] public decimal MonthlyPrice { get; set; }
    [Id(20)] public decimal AnnualPrice { get; set; }
    [Id(21)] public string Currency { get; set; } = "USD";
}
