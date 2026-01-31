using DarkVelocity.Orleans.Abstractions;
using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Orleans.Grains.Subscribers;

/// <summary>
/// Implicit stream subscriber that handles customer spend events.
/// Updates customer loyalty projections based on spend data from orders.
/// This creates the "loyalty from accounting" pattern where loyalty status
/// is derived from actual spend rather than tracked separately.
/// </summary>
[ImplicitStreamSubscription(StreamConstants.CustomerSpendStreamNamespace)]
public class CustomerSpendSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<CustomerSpendSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;

    public CustomerSpendSubscriberGrain(ILogger<CustomerSpendSubscriberGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.CustomerSpendStreamNamespace, this.GetPrimaryKeyString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        _subscription = await stream.SubscribeAsync(this);

        _logger.LogInformation(
            "CustomerSpendSubscriber activated for organization {OrgId}",
            this.GetPrimaryKeyString());

        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        if (_subscription != null)
        {
            await _subscription.UnsubscribeAsync();
        }

        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task OnNextAsync(IStreamEvent item, StreamSequenceToken? token = null)
    {
        try
        {
            switch (item)
            {
                case CustomerSpendRecordedEvent spendEvent:
                    await HandleSpendRecordedAsync(spendEvent);
                    break;

                case CustomerSpendReversedEvent reversedEvent:
                    await HandleSpendReversedAsync(reversedEvent);
                    break;

                case LoyaltyPointsRedeemedEvent redeemedEvent:
                    await HandlePointsRedeemedAsync(redeemedEvent);
                    break;

                case CustomerTierChangedEvent tierChangedEvent:
                    await HandleTierChangedAsync(tierChangedEvent);
                    break;

                default:
                    _logger.LogDebug("Ignoring unhandled event type: {EventType}", item.GetType().Name);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling customer spend event {EventType}: {EventId}", item.GetType().Name, item.EventId);
        }
    }

    public Task OnCompletedAsync()
    {
        _logger.LogInformation("Customer spend event stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in customer spend event stream");
        return Task.CompletedTask;
    }

    private Task HandleSpendRecordedAsync(CustomerSpendRecordedEvent evt)
    {
        // This event is published BY the CustomerSpendProjectionGrain after recording spend
        // We can use it to trigger other downstream actions like:
        // - Sending loyalty notifications
        // - Triggering marketing automation
        // - Updating real-time dashboards

        _logger.LogInformation(
            "Customer {CustomerId} recorded spend of {NetSpend:C} at site {SiteId}",
            evt.CustomerId,
            evt.NetSpend,
            evt.SiteId);

        // Publish to alerts stream if we detect interesting patterns
        // For example: high-value customer, unusual spending patterns, etc.

        return Task.CompletedTask;
    }

    private Task HandleSpendReversedAsync(CustomerSpendReversedEvent evt)
    {
        _logger.LogInformation(
            "Customer {CustomerId} spend reversed: {Amount:C}. Reason: {Reason}",
            evt.CustomerId,
            evt.Amount,
            evt.Reason);

        // Could trigger notifications or alerts for refund patterns

        return Task.CompletedTask;
    }

    private async Task HandlePointsRedeemedAsync(LoyaltyPointsRedeemedEvent evt)
    {
        _logger.LogInformation(
            "Customer {CustomerId} redeemed {Points} points for {DiscountValue:C}",
            evt.CustomerId,
            evt.Points,
            evt.DiscountValue);

        // Record the loyalty redemption expense in accounting
        // Debit Loyalty Expense, Credit Loyalty Liability (or direct to discount)

        var journalEntryId = Guid.NewGuid();
        var performedBy = Guid.Empty;

        const string LoyaltyLiabilityAccountCode = "2400";
        const string LoyaltyExpenseAccountCode = "5800";

        // Debit Loyalty Expense
        var expenseGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, LoyaltyExpenseAccountCode)));

        await expenseGrain.PostDebitAsync(new PostDebitCommand(
            Amount: evt.DiscountValue,
            Description: $"Loyalty points redeemed - {evt.Points} points for order {evt.OrderId}",
            PerformedBy: performedBy,
            ReferenceType: "LoyaltyRedemption",
            ReferenceId: evt.OrderId,
            AccountingJournalEntryId: journalEntryId));

        // Credit Loyalty Liability (or contra-revenue)
        var liabilityGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, LoyaltyLiabilityAccountCode)));

        await liabilityGrain.PostCreditAsync(new PostCreditCommand(
            Amount: evt.DiscountValue,
            Description: $"Loyalty points redeemed - {evt.Points} points for order {evt.OrderId}",
            PerformedBy: performedBy,
            ReferenceType: "LoyaltyRedemption",
            ReferenceId: evt.OrderId,
            AccountingJournalEntryId: journalEntryId));

        _logger.LogInformation(
            "Created journal entry {JournalEntryId} for loyalty redemption",
            journalEntryId);
    }

    private Task HandleTierChangedAsync(CustomerTierChangedEvent evt)
    {
        _logger.LogInformation(
            "Customer {CustomerId} tier changed from {OldTier} to {NewTier} (Lifetime spend: {LifetimeSpend:C})",
            evt.CustomerId,
            evt.OldTier,
            evt.NewTier,
            evt.LifetimeSpend);

        // Could trigger:
        // - Congratulatory email/notification
        // - Marketing automation
        // - Staff alerts for VIP customers

        return Task.CompletedTask;
    }

    private static Guid GetAccountId(Guid orgId, string accountCode)
    {
        var input = $"{orgId}:{accountCode}";
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}
