using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record CreateSubscriptionCommand(
    [property: Id(0)] SubscriptionPlan Plan = SubscriptionPlan.Free,
    [property: Id(1)] BillingCycle BillingCycle = BillingCycle.Monthly,
    [property: Id(2)] string? StripeCustomerId = null);

[GenerateSerializer]
public record StartTrialCommand(
    [property: Id(0)] SubscriptionPlan Plan,
    [property: Id(1)] int TrialDays = 14);

[GenerateSerializer]
public record ChangePlanCommand(
    [property: Id(0)] SubscriptionPlan NewPlan,
    [property: Id(1)] bool Immediate = true,
    [property: Id(2)] BillingCycle? NewBillingCycle = null);

[GenerateSerializer]
public record AddPaymentMethodCommand(
    [property: Id(0)] string PaymentMethodId,
    [property: Id(1)] string Type,
    [property: Id(2)] string Last4,
    [property: Id(3)] string? Brand = null,
    [property: Id(4)] int? ExpMonth = null,
    [property: Id(5)] int? ExpYear = null,
    [property: Id(6)] bool SetAsDefault = true);

[GenerateSerializer]
public record RecordUsageCommand(
    [property: Id(0)] int? SitesCount = null,
    [property: Id(1)] int? UsersCount = null,
    [property: Id(2)] int? TransactionsDelta = null,
    [property: Id(3)] int? ApiCallsDelta = null,
    [property: Id(4)] long? StorageDelta = null);

[GenerateSerializer]
public record CancelSubscriptionCommand(
    [property: Id(0)] bool Immediate = false,
    [property: Id(1)] string? Reason = null);

[GenerateSerializer]
public record SubscriptionCreatedResult(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] SubscriptionPlan Plan,
    [property: Id(2)] DateTime CreatedAt);

[GenerateSerializer]
public record PlanChangeResult(
    [property: Id(0)] SubscriptionPlan OldPlan,
    [property: Id(1)] SubscriptionPlan NewPlan,
    [property: Id(2)] decimal? ProratedAmount,
    [property: Id(3)] DateTime? EffectiveAt);

[GenerateSerializer]
public record UsageLimitStatus(
    [property: Id(0)] string LimitType,
    [property: Id(1)] int CurrentValue,
    [property: Id(2)] int LimitValue,
    [property: Id(3)] bool Exceeded,
    [property: Id(4)] double PercentageUsed);

public interface ISubscriptionGrain : IGrainWithStringKey
{
    // Subscription lifecycle
    Task<SubscriptionCreatedResult> CreateAsync(CreateSubscriptionCommand command);
    Task<SubscriptionState> GetStateAsync();
    Task<bool> ExistsAsync();

    // Trial management
    Task StartTrialAsync(StartTrialCommand command);
    Task EndTrialAsync(bool convertToPaid);

    // Plan management
    Task<PlanChangeResult> ChangePlanAsync(ChangePlanCommand command);
    Task ChangeBillingCycleAsync(BillingCycle newCycle);

    // Payment methods
    Task AddPaymentMethodAsync(AddPaymentMethodCommand command);
    Task RemovePaymentMethodAsync(string paymentMethodId);
    Task SetDefaultPaymentMethodAsync(string paymentMethodId);

    // Billing
    Task LinkStripeCustomerAsync(string stripeCustomerId, string? stripeSubscriptionId = null);
    Task RecordInvoiceAsync(InvoiceSummary invoice);
    Task RecordInvoicePaidAsync(string invoiceId, decimal amount, string paymentMethodId);
    Task RecordInvoiceFailedAsync(string invoiceId, string failureReason, int attemptCount);
    Task RenewBillingPeriodAsync(DateTime periodStart, DateTime periodEnd);

    // Cancellation
    Task CancelAsync(CancelSubscriptionCommand command);
    Task ReactivateAsync();

    // Usage tracking
    Task RecordUsageAsync(RecordUsageCommand command);
    Task ResetMonthlyUsageAsync();
    Task<UsageMetrics> GetCurrentUsageAsync();
    Task<IReadOnlyList<UsageLimitStatus>> CheckUsageLimitsAsync();
    Task<bool> CanAddSiteAsync();
    Task<bool> CanAddUserAsync();
    Task<bool> HasFeatureAsync(string featureName);

    // Plan information
    Task<PlanLimits> GetCurrentLimitsAsync();
    Task<decimal> GetCurrentPriceAsync();
}
