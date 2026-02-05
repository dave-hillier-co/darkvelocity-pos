using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Journaled grain for Subscription management with full event sourcing.
/// Handles subscription plans, billing, usage tracking, and limits.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class SubscriptionGrain : JournaledGrain<SubscriptionState, ISubscriptionEvent>, ISubscriptionGrain
{
    // Pricing configuration (in a real system, this would come from a configuration service)
    private static readonly Dictionary<SubscriptionPlan, (decimal Monthly, decimal Annual)> Pricing = new()
    {
        [SubscriptionPlan.Free] = (0m, 0m),
        [SubscriptionPlan.Starter] = (49m, 490m),
        [SubscriptionPlan.Pro] = (149m, 1490m),
        [SubscriptionPlan.Enterprise] = (499m, 4990m)
    };

    protected override void TransitionState(SubscriptionState state, ISubscriptionEvent @event)
    {
        switch (@event)
        {
            case SubscriptionCreated e:
                state.OrganizationId = e.OrganizationId;
                state.Plan = e.Plan;
                state.BillingCycle = e.BillingCycle;
                state.StripeCustomerId = e.StripeCustomerId;
                state.StripeSubscriptionId = e.StripeSubscriptionId;
                state.Status = SubscriptionStatus.Active;
                state.Limits = PlanLimits.ForPlan(e.Plan);
                state.CreatedAt = e.OccurredAt;
                UpdatePricing(state, e.Plan, e.BillingCycle);
                break;

            case TrialStarted e:
                state.Plan = e.Plan;
                state.Status = SubscriptionStatus.Trialing;
                state.TrialStartedAt = e.OccurredAt;
                state.TrialEndsAt = e.TrialEndsAt;
                state.Limits = PlanLimits.ForPlan(e.Plan);
                state.UpdatedAt = e.OccurredAt;
                UpdatePricing(state, e.Plan, state.BillingCycle);
                break;

            case TrialEnded e:
                state.Status = e.ConvertedToPaid ? SubscriptionStatus.Active : SubscriptionStatus.Cancelled;
                if (!e.ConvertedToPaid)
                {
                    state.Plan = SubscriptionPlan.Free;
                    state.Limits = PlanLimits.ForPlan(SubscriptionPlan.Free);
                    UpdatePricing(state, SubscriptionPlan.Free, state.BillingCycle);
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case PlanChanged e:
                state.Plan = e.NewPlan;
                state.Limits = PlanLimits.ForPlan(e.NewPlan);
                state.UpdatedAt = e.OccurredAt;
                UpdatePricing(state, e.NewPlan, state.BillingCycle);
                break;

            case BillingCycleChanged e:
                state.BillingCycle = e.NewCycle;
                state.UpdatedAt = e.OccurredAt;
                UpdatePricing(state, state.Plan, e.NewCycle);
                break;

            case SubscriptionCanceled e:
                if (e.Immediate)
                {
                    state.Status = SubscriptionStatus.Cancelled;
                    state.CancelledAt = e.OccurredAt;
                }
                else
                {
                    state.CancelAtPeriodEnd = e.CancelAt;
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case SubscriptionReactivated e:
                state.Status = SubscriptionStatus.Active;
                state.CancelledAt = null;
                state.CancelAtPeriodEnd = null;
                state.UpdatedAt = e.OccurredAt;
                break;

            case PaymentMethodAdded e:
                state.PaymentMethods.Add(e.PaymentMethod);
                if (e.PaymentMethod.IsDefault)
                    state.DefaultPaymentMethodId = e.PaymentMethod.PaymentMethodId;
                state.UpdatedAt = e.OccurredAt;
                break;

            case PaymentMethodRemoved e:
                state.PaymentMethods.RemoveAll(pm => pm.PaymentMethodId == e.PaymentMethodId);
                if (state.DefaultPaymentMethodId == e.PaymentMethodId)
                    state.DefaultPaymentMethodId = state.PaymentMethods.FirstOrDefault()?.PaymentMethodId;
                state.UpdatedAt = e.OccurredAt;
                break;

            case DefaultPaymentMethodChanged e:
                state.DefaultPaymentMethodId = e.PaymentMethodId;
                // Update IsDefault flags
                for (int i = 0; i < state.PaymentMethods.Count; i++)
                {
                    var pm = state.PaymentMethods[i];
                    state.PaymentMethods[i] = pm with { IsDefault = pm.PaymentMethodId == e.PaymentMethodId };
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case InvoiceGenerated e:
                state.RecentInvoices.Add(e.Invoice);
                // Keep only last 12 invoices
                if (state.RecentInvoices.Count > 12)
                    state.RecentInvoices.RemoveAt(0);
                state.UpdatedAt = e.OccurredAt;
                break;

            case InvoicePaid e:
                var invoiceToPay = state.RecentInvoices.FindIndex(i => i.InvoiceId == e.InvoiceId);
                if (invoiceToPay >= 0)
                {
                    state.RecentInvoices[invoiceToPay] = state.RecentInvoices[invoiceToPay] with
                    {
                        Status = "paid",
                        PaidAt = e.OccurredAt
                    };
                }
                if (state.Status == SubscriptionStatus.PastDue)
                    state.Status = SubscriptionStatus.Active;
                state.UpdatedAt = e.OccurredAt;
                break;

            case InvoicePaymentFailed e:
                var invoiceToFail = state.RecentInvoices.FindIndex(i => i.InvoiceId == e.InvoiceId);
                if (invoiceToFail >= 0)
                {
                    state.RecentInvoices[invoiceToFail] = state.RecentInvoices[invoiceToFail] with { Status = "failed" };
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case SubscriptionPastDue e:
                state.Status = SubscriptionStatus.PastDue;
                state.UpdatedAt = e.OccurredAt;
                break;

            case UsageUpdated e:
                state.Usage = e.Usage;
                state.UpdatedAt = e.OccurredAt;
                break;

            case StripeCustomerLinked e:
                state.StripeCustomerId = e.StripeCustomerId;
                state.UpdatedAt = e.OccurredAt;
                break;

            case BillingPeriodRenewed e:
                state.CurrentPeriodStart = e.PeriodStart;
                state.CurrentPeriodEnd = e.PeriodEnd;
                // Reset monthly usage on period renewal
                state.Usage = state.Usage with
                {
                    TransactionsThisMonth = 0,
                    ApiCallsThisMonth = 0,
                    PeriodMonth = e.PeriodStart.Month,
                    PeriodYear = e.PeriodStart.Year,
                    LastUpdated = e.OccurredAt
                };
                state.UpdatedAt = e.OccurredAt;
                break;
        }
    }

    private static void UpdatePricing(SubscriptionState state, SubscriptionPlan plan, BillingCycle cycle)
    {
        if (Pricing.TryGetValue(plan, out var prices))
        {
            state.MonthlyPrice = prices.Monthly;
            state.AnnualPrice = prices.Annual;
        }
    }

    public async Task<SubscriptionCreatedResult> CreateAsync(CreateSubscriptionCommand command)
    {
        if (State.OrganizationId != Guid.Empty)
            throw new InvalidOperationException("Subscription already exists");

        var orgId = Guid.Parse(this.GetPrimaryKeyString());
        var now = DateTime.UtcNow;

        RaiseEvent(new SubscriptionCreated
        {
            OrganizationId = orgId,
            Plan = command.Plan,
            BillingCycle = command.BillingCycle,
            StripeCustomerId = command.StripeCustomerId,
            OccurredAt = now
        });

        await ConfirmEvents();

        return new SubscriptionCreatedResult(orgId, command.Plan, State.CreatedAt);
    }

    public Task<SubscriptionState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(State.OrganizationId != Guid.Empty);
    }

    public async Task StartTrialAsync(StartTrialCommand command)
    {
        EnsureExists();

        if (State.TrialStartedAt.HasValue)
            throw new InvalidOperationException("Trial has already been used");

        if (State.Plan != SubscriptionPlan.Free)
            throw new InvalidOperationException("Cannot start trial on paid plan");

        var now = DateTime.UtcNow;

        RaiseEvent(new TrialStarted
        {
            OrganizationId = State.OrganizationId,
            Plan = command.Plan,
            TrialEndsAt = now.AddDays(command.TrialDays),
            OccurredAt = now
        });

        await ConfirmEvents();
    }

    public async Task EndTrialAsync(bool convertToPaid)
    {
        EnsureExists();

        if (State.Status != SubscriptionStatus.Trialing)
            throw new InvalidOperationException("Not currently in trial");

        RaiseEvent(new TrialEnded
        {
            OrganizationId = State.OrganizationId,
            ConvertedToPaid = convertToPaid,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task<PlanChangeResult> ChangePlanAsync(ChangePlanCommand command)
    {
        EnsureExists();

        if (State.Plan == command.NewPlan)
            throw new InvalidOperationException("Already on this plan");

        var oldPlan = State.Plan;
        var now = DateTime.UtcNow;
        var effectiveAt = command.Immediate ? now : State.CurrentPeriodEnd;

        // Calculate proration for immediate upgrades
        decimal? proratedAmount = null;
        if (command.Immediate && State.CurrentPeriodEnd.HasValue)
        {
            var daysRemaining = (State.CurrentPeriodEnd.Value - now).Days;
            if (daysRemaining > 0)
            {
                var newPrice = GetPriceForPlan(command.NewPlan, State.BillingCycle);
                var oldPrice = GetPriceForPlan(oldPlan, State.BillingCycle);
                var dailyDiff = (newPrice - oldPrice) / 30m; // Simplified daily rate
                proratedAmount = dailyDiff * daysRemaining;
            }
        }

        RaiseEvent(new PlanChanged
        {
            OrganizationId = State.OrganizationId,
            OldPlan = oldPlan,
            NewPlan = command.NewPlan,
            ProratedAmount = proratedAmount,
            Immediate = command.Immediate,
            EffectiveAt = effectiveAt,
            OccurredAt = now
        });

        if (command.NewBillingCycle.HasValue && command.NewBillingCycle != State.BillingCycle)
        {
            RaiseEvent(new BillingCycleChanged
            {
                OrganizationId = State.OrganizationId,
                OldCycle = State.BillingCycle,
                NewCycle = command.NewBillingCycle.Value,
                EffectiveAt = effectiveAt ?? now,
                OccurredAt = now
            });
        }

        await ConfirmEvents();

        return new PlanChangeResult(oldPlan, command.NewPlan, proratedAmount, effectiveAt);
    }

    public async Task ChangeBillingCycleAsync(BillingCycle newCycle)
    {
        EnsureExists();

        if (State.BillingCycle == newCycle)
            throw new InvalidOperationException("Already on this billing cycle");

        var now = DateTime.UtcNow;

        RaiseEvent(new BillingCycleChanged
        {
            OrganizationId = State.OrganizationId,
            OldCycle = State.BillingCycle,
            NewCycle = newCycle,
            EffectiveAt = State.CurrentPeriodEnd ?? now,
            OccurredAt = now
        });

        await ConfirmEvents();
    }

    public async Task AddPaymentMethodAsync(AddPaymentMethodCommand command)
    {
        EnsureExists();

        var paymentMethod = new PaymentMethodInfo
        {
            PaymentMethodId = command.PaymentMethodId,
            Type = command.Type,
            Last4 = command.Last4,
            Brand = command.Brand,
            ExpMonth = command.ExpMonth,
            ExpYear = command.ExpYear,
            IsDefault = command.SetAsDefault || State.PaymentMethods.Count == 0,
            AddedAt = DateTime.UtcNow
        };

        RaiseEvent(new PaymentMethodAdded
        {
            OrganizationId = State.OrganizationId,
            PaymentMethod = paymentMethod,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task RemovePaymentMethodAsync(string paymentMethodId)
    {
        EnsureExists();

        if (!State.PaymentMethods.Any(pm => pm.PaymentMethodId == paymentMethodId))
            throw new InvalidOperationException("Payment method not found");

        // Cannot remove the only payment method if on a paid plan
        if (State.Plan != SubscriptionPlan.Free && State.PaymentMethods.Count == 1)
            throw new InvalidOperationException("Cannot remove the only payment method on a paid plan");

        RaiseEvent(new PaymentMethodRemoved
        {
            OrganizationId = State.OrganizationId,
            PaymentMethodId = paymentMethodId,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task SetDefaultPaymentMethodAsync(string paymentMethodId)
    {
        EnsureExists();

        if (!State.PaymentMethods.Any(pm => pm.PaymentMethodId == paymentMethodId))
            throw new InvalidOperationException("Payment method not found");

        RaiseEvent(new DefaultPaymentMethodChanged
        {
            OrganizationId = State.OrganizationId,
            PaymentMethodId = paymentMethodId,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task LinkStripeCustomerAsync(string stripeCustomerId, string? stripeSubscriptionId = null)
    {
        EnsureExists();

        RaiseEvent(new StripeCustomerLinked
        {
            OrganizationId = State.OrganizationId,
            StripeCustomerId = stripeCustomerId,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task RecordInvoiceAsync(InvoiceSummary invoice)
    {
        EnsureExists();

        RaiseEvent(new InvoiceGenerated
        {
            OrganizationId = State.OrganizationId,
            Invoice = invoice,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task RecordInvoicePaidAsync(string invoiceId, decimal amount, string paymentMethodId)
    {
        EnsureExists();

        RaiseEvent(new InvoicePaid
        {
            OrganizationId = State.OrganizationId,
            InvoiceId = invoiceId,
            Amount = amount,
            PaymentMethodId = paymentMethodId,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task RecordInvoiceFailedAsync(string invoiceId, string failureReason, int attemptCount)
    {
        EnsureExists();

        RaiseEvent(new InvoicePaymentFailed
        {
            OrganizationId = State.OrganizationId,
            InvoiceId = invoiceId,
            FailureReason = failureReason,
            AttemptCount = attemptCount,
            OccurredAt = DateTime.UtcNow
        });

        // After 3 failed attempts, mark as past due
        if (attemptCount >= 3)
        {
            RaiseEvent(new SubscriptionPastDue
            {
                OrganizationId = State.OrganizationId,
                InvoiceId = invoiceId,
                AmountDue = State.RecentInvoices.FirstOrDefault(i => i.InvoiceId == invoiceId)?.Amount ?? 0,
                OccurredAt = DateTime.UtcNow
            });
        }

        await ConfirmEvents();
    }

    public async Task RenewBillingPeriodAsync(DateTime periodStart, DateTime periodEnd)
    {
        EnsureExists();

        RaiseEvent(new BillingPeriodRenewed
        {
            OrganizationId = State.OrganizationId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task CancelAsync(CancelSubscriptionCommand command)
    {
        EnsureExists();

        if (State.Status == SubscriptionStatus.Cancelled)
            throw new InvalidOperationException("Subscription is already cancelled");

        var now = DateTime.UtcNow;

        RaiseEvent(new SubscriptionCanceled
        {
            OrganizationId = State.OrganizationId,
            Immediate = command.Immediate,
            CancelAt = command.Immediate ? null : State.CurrentPeriodEnd,
            Reason = command.Reason,
            OccurredAt = now
        });

        await ConfirmEvents();
    }

    public async Task ReactivateAsync()
    {
        EnsureExists();

        if (State.Status != SubscriptionStatus.Cancelled && State.CancelAtPeriodEnd == null)
            throw new InvalidOperationException("Subscription is not cancelled or pending cancellation");

        RaiseEvent(new SubscriptionReactivated
        {
            OrganizationId = State.OrganizationId,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task RecordUsageAsync(RecordUsageCommand command)
    {
        EnsureExists();

        var now = DateTime.UtcNow;
        var currentMonth = now.Month;
        var currentYear = now.Year;

        // Reset counters if we're in a new month
        var resetMonthly = State.Usage.PeriodMonth != currentMonth || State.Usage.PeriodYear != currentYear;

        var newUsage = new UsageMetrics
        {
            CurrentSites = command.SitesCount ?? State.Usage.CurrentSites,
            CurrentUsers = command.UsersCount ?? State.Usage.CurrentUsers,
            TransactionsThisMonth = resetMonthly
                ? (command.TransactionsDelta ?? 0)
                : State.Usage.TransactionsThisMonth + (command.TransactionsDelta ?? 0),
            ApiCallsThisMonth = resetMonthly
                ? (command.ApiCallsDelta ?? 0)
                : State.Usage.ApiCallsThisMonth + (command.ApiCallsDelta ?? 0),
            StorageUsedBytes = State.Usage.StorageUsedBytes + (command.StorageDelta ?? 0),
            LastUpdated = now,
            PeriodMonth = currentMonth,
            PeriodYear = currentYear
        };

        RaiseEvent(new UsageUpdated
        {
            OrganizationId = State.OrganizationId,
            Usage = newUsage,
            OccurredAt = now
        });

        // Check for limit exceedance
        var limits = State.Limits;
        if (newUsage.CurrentSites > limits.MaxSites)
        {
            RaiseEvent(new UsageLimitExceeded
            {
                OrganizationId = State.OrganizationId,
                LimitType = "sites",
                CurrentValue = newUsage.CurrentSites,
                LimitValue = limits.MaxSites,
                OccurredAt = now
            });
        }
        if (newUsage.CurrentUsers > limits.MaxUsers)
        {
            RaiseEvent(new UsageLimitExceeded
            {
                OrganizationId = State.OrganizationId,
                LimitType = "users",
                CurrentValue = newUsage.CurrentUsers,
                LimitValue = limits.MaxUsers,
                OccurredAt = now
            });
        }
        if (newUsage.TransactionsThisMonth > limits.MaxTransactionsPerMonth)
        {
            RaiseEvent(new UsageLimitExceeded
            {
                OrganizationId = State.OrganizationId,
                LimitType = "transactions",
                CurrentValue = newUsage.TransactionsThisMonth,
                LimitValue = limits.MaxTransactionsPerMonth,
                OccurredAt = now
            });
        }

        await ConfirmEvents();
    }

    public async Task ResetMonthlyUsageAsync()
    {
        EnsureExists();

        var now = DateTime.UtcNow;

        RaiseEvent(new UsageUpdated
        {
            OrganizationId = State.OrganizationId,
            Usage = State.Usage with
            {
                TransactionsThisMonth = 0,
                ApiCallsThisMonth = 0,
                PeriodMonth = now.Month,
                PeriodYear = now.Year,
                LastUpdated = now
            },
            OccurredAt = now
        });

        await ConfirmEvents();
    }

    public Task<UsageMetrics> GetCurrentUsageAsync()
    {
        EnsureExists();
        return Task.FromResult(State.Usage);
    }

    public Task<IReadOnlyList<UsageLimitStatus>> CheckUsageLimitsAsync()
    {
        EnsureExists();

        var limits = State.Limits;
        var usage = State.Usage;

        var statuses = new List<UsageLimitStatus>
        {
            new("sites", usage.CurrentSites, limits.MaxSites,
                usage.CurrentSites > limits.MaxSites,
                limits.MaxSites > 0 ? (double)usage.CurrentSites / limits.MaxSites * 100 : 0),
            new("users", usage.CurrentUsers, limits.MaxUsers,
                usage.CurrentUsers > limits.MaxUsers,
                limits.MaxUsers > 0 ? (double)usage.CurrentUsers / limits.MaxUsers * 100 : 0),
            new("transactions", usage.TransactionsThisMonth, limits.MaxTransactionsPerMonth,
                usage.TransactionsThisMonth > limits.MaxTransactionsPerMonth,
                limits.MaxTransactionsPerMonth > 0 ? (double)usage.TransactionsThisMonth / limits.MaxTransactionsPerMonth * 100 : 0),
            new("api_calls", usage.ApiCallsThisMonth, limits.MaxApiCallsPerMonth,
                usage.ApiCallsThisMonth > limits.MaxApiCallsPerMonth,
                limits.MaxApiCallsPerMonth > 0 ? (double)usage.ApiCallsThisMonth / limits.MaxApiCallsPerMonth * 100 : 0),
            new("storage", (int)(usage.StorageUsedBytes / 1024 / 1024), (int)(limits.MaxStorageBytes / 1024 / 1024),
                usage.StorageUsedBytes > limits.MaxStorageBytes,
                limits.MaxStorageBytes > 0 ? (double)usage.StorageUsedBytes / limits.MaxStorageBytes * 100 : 0)
        };

        return Task.FromResult<IReadOnlyList<UsageLimitStatus>>(statuses);
    }

    public Task<bool> CanAddSiteAsync()
    {
        EnsureExists();
        return Task.FromResult(State.Usage.CurrentSites < State.Limits.MaxSites);
    }

    public Task<bool> CanAddUserAsync()
    {
        EnsureExists();
        return Task.FromResult(State.Usage.CurrentUsers < State.Limits.MaxUsers);
    }

    public Task<bool> HasFeatureAsync(string featureName)
    {
        EnsureExists();

        var hasFeature = featureName.ToLowerInvariant() switch
        {
            "advanced_reporting" => State.Limits.AdvancedReporting,
            "custom_branding" => State.Limits.CustomBranding,
            "api_access" => State.Limits.ApiAccess,
            "priority_support" => State.Limits.PrioritySupport,
            "sso" => State.Limits.SsoEnabled,
            "custom_integrations" => State.Limits.CustomIntegrations,
            _ => false
        };

        return Task.FromResult(hasFeature);
    }

    public Task<PlanLimits> GetCurrentLimitsAsync()
    {
        EnsureExists();
        return Task.FromResult(State.Limits);
    }

    public Task<decimal> GetCurrentPriceAsync()
    {
        EnsureExists();
        return Task.FromResult(State.BillingCycle == BillingCycle.Monthly ? State.MonthlyPrice : State.AnnualPrice);
    }

    private void EnsureExists()
    {
        if (State.OrganizationId == Guid.Empty)
            throw new InvalidOperationException("Subscription does not exist");
    }

    private static decimal GetPriceForPlan(SubscriptionPlan plan, BillingCycle cycle)
    {
        if (Pricing.TryGetValue(plan, out var prices))
        {
            return cycle == BillingCycle.Monthly ? prices.Monthly : prices.Annual;
        }
        return 0m;
    }
}
