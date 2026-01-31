using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains.Subscribers;

/// <summary>
/// Implicit stream subscriber that handles gift card events for accounting.
/// Creates journal entries for gift card lifecycle events.
///
/// Accounting treatments:
/// - Activated (sold): Debit Cash, Credit Gift Card Liability
/// - Redeemed: Debit Gift Card Liability, Credit Sales Revenue
/// - Reloaded: Debit Cash, Credit Gift Card Liability
/// - Refund Applied: Debit Refund Expense, Credit Gift Card Liability
/// - Expired: Debit Gift Card Liability, Credit Breakage Income
/// </summary>
[ImplicitStreamSubscription(StreamConstants.GiftCardStreamNamespace)]
public class GiftCardAccountingSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<GiftCardAccountingSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;

    // Standard account codes
    private const string CashAccountCode = "1000";
    private const string GiftCardLiabilityAccountCode = "2300";
    private const string SalesRevenueAccountCode = "4000";
    private const string RefundExpenseAccountCode = "5900";
    private const string BreakageIncomeAccountCode = "4950";

    public GiftCardAccountingSubscriberGrain(ILogger<GiftCardAccountingSubscriberGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.GiftCardStreamNamespace, this.GetPrimaryKeyString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        _subscription = await stream.SubscribeAsync(this);

        _logger.LogInformation(
            "GiftCardAccountingSubscriber activated for organization {OrgId}",
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
                case GiftCardActivatedEvent activatedEvent:
                    await HandleGiftCardActivatedAsync(activatedEvent);
                    break;

                case GiftCardRedeemedEvent redeemedEvent:
                    await HandleGiftCardRedeemedAsync(redeemedEvent);
                    break;

                case GiftCardReloadedEvent reloadedEvent:
                    await HandleGiftCardReloadedAsync(reloadedEvent);
                    break;

                case GiftCardRefundAppliedEvent refundEvent:
                    await HandleGiftCardRefundAppliedAsync(refundEvent);
                    break;

                case GiftCardExpiredEvent expiredEvent:
                    await HandleGiftCardExpiredAsync(expiredEvent);
                    break;

                default:
                    _logger.LogDebug("Ignoring unhandled event type: {EventType}", item.GetType().Name);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling gift card event {EventType}: {EventId}", item.GetType().Name, item.EventId);
        }
    }

    public Task OnCompletedAsync()
    {
        _logger.LogInformation("Gift card event stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in gift card event stream");
        return Task.CompletedTask;
    }

    private async Task HandleGiftCardActivatedAsync(GiftCardActivatedEvent evt)
    {
        _logger.LogInformation(
            "Gift card {CardNumber} activated for {Amount:C}",
            evt.CardNumber,
            evt.Amount);

        var journalEntryId = Guid.NewGuid();
        var performedBy = Guid.Empty;

        // Debit Cash (increase asset)
        var cashGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, CashAccountCode)));

        await cashGrain.PostDebitAsync(new PostDebitCommand(
            Amount: evt.Amount,
            Description: $"Gift card sold - {evt.CardNumber}",
            PerformedBy: performedBy,
            ReferenceType: "GiftCardSale",
            ReferenceId: evt.CardId,
            AccountingJournalEntryId: journalEntryId));

        // Credit Gift Card Liability (increase liability)
        var liabilityGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, GiftCardLiabilityAccountCode)));

        await liabilityGrain.PostCreditAsync(new PostCreditCommand(
            Amount: evt.Amount,
            Description: $"Gift card sold - {evt.CardNumber}",
            PerformedBy: performedBy,
            ReferenceType: "GiftCardSale",
            ReferenceId: evt.CardId,
            AccountingJournalEntryId: journalEntryId));

        _logger.LogInformation(
            "Created journal entry {JournalEntryId} for gift card activation",
            journalEntryId);
    }

    private async Task HandleGiftCardRedeemedAsync(GiftCardRedeemedEvent evt)
    {
        _logger.LogInformation(
            "Gift card {CardNumber} redeemed for {Amount:C}",
            evt.CardNumber,
            evt.Amount);

        var journalEntryId = Guid.NewGuid();
        var performedBy = Guid.Empty;

        // Debit Gift Card Liability (decrease liability)
        var liabilityGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, GiftCardLiabilityAccountCode)));

        await liabilityGrain.PostDebitAsync(new PostDebitCommand(
            Amount: evt.Amount,
            Description: $"Gift card redeemed - {evt.CardNumber} for order {evt.OrderId}",
            PerformedBy: performedBy,
            ReferenceType: "GiftCardRedemption",
            ReferenceId: evt.OrderId,
            AccountingJournalEntryId: journalEntryId));

        // Credit Sales Revenue (increase revenue)
        var salesGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, SalesRevenueAccountCode)));

        await salesGrain.PostCreditAsync(new PostCreditCommand(
            Amount: evt.Amount,
            Description: $"Gift card redeemed - {evt.CardNumber} for order {evt.OrderId}",
            PerformedBy: performedBy,
            ReferenceType: "GiftCardRedemption",
            ReferenceId: evt.OrderId,
            AccountingJournalEntryId: journalEntryId));

        _logger.LogInformation(
            "Created journal entry {JournalEntryId} for gift card redemption",
            journalEntryId);
    }

    private async Task HandleGiftCardReloadedAsync(GiftCardReloadedEvent evt)
    {
        _logger.LogInformation(
            "Gift card {CardNumber} reloaded with {Amount:C}",
            evt.CardNumber,
            evt.Amount);

        var journalEntryId = Guid.NewGuid();
        var performedBy = Guid.Empty;

        // Debit Cash (increase asset)
        var cashGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, CashAccountCode)));

        await cashGrain.PostDebitAsync(new PostDebitCommand(
            Amount: evt.Amount,
            Description: $"Gift card reloaded - {evt.CardNumber}",
            PerformedBy: performedBy,
            ReferenceType: "GiftCardReload",
            ReferenceId: evt.CardId,
            AccountingJournalEntryId: journalEntryId));

        // Credit Gift Card Liability (increase liability)
        var liabilityGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, GiftCardLiabilityAccountCode)));

        await liabilityGrain.PostCreditAsync(new PostCreditCommand(
            Amount: evt.Amount,
            Description: $"Gift card reloaded - {evt.CardNumber}",
            PerformedBy: performedBy,
            ReferenceType: "GiftCardReload",
            ReferenceId: evt.CardId,
            AccountingJournalEntryId: journalEntryId));

        _logger.LogInformation(
            "Created journal entry {JournalEntryId} for gift card reload",
            journalEntryId);
    }

    private async Task HandleGiftCardRefundAppliedAsync(GiftCardRefundAppliedEvent evt)
    {
        _logger.LogInformation(
            "Gift card refund applied to {CardNumber}: {Amount:C}",
            evt.CardNumber,
            evt.Amount);

        var journalEntryId = Guid.NewGuid();
        var performedBy = Guid.Empty;

        // Debit Refund Expense (increase expense)
        var refundGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, RefundExpenseAccountCode)));

        await refundGrain.PostDebitAsync(new PostDebitCommand(
            Amount: evt.Amount,
            Description: $"Refund to gift card - {evt.CardNumber} from order {evt.OriginalOrderId}",
            PerformedBy: performedBy,
            ReferenceType: "GiftCardRefund",
            ReferenceId: evt.OriginalOrderId,
            AccountingJournalEntryId: journalEntryId));

        // Credit Gift Card Liability (increase liability)
        var liabilityGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, GiftCardLiabilityAccountCode)));

        await liabilityGrain.PostCreditAsync(new PostCreditCommand(
            Amount: evt.Amount,
            Description: $"Refund to gift card - {evt.CardNumber} from order {evt.OriginalOrderId}",
            PerformedBy: performedBy,
            ReferenceType: "GiftCardRefund",
            ReferenceId: evt.OriginalOrderId,
            AccountingJournalEntryId: journalEntryId));

        _logger.LogInformation(
            "Created journal entry {JournalEntryId} for gift card refund",
            journalEntryId);
    }

    private async Task HandleGiftCardExpiredAsync(GiftCardExpiredEvent evt)
    {
        _logger.LogInformation(
            "Gift card {CardNumber} expired with remaining balance {Amount:C}",
            evt.CardNumber,
            evt.ExpiredBalance);

        if (evt.ExpiredBalance <= 0)
            return; // No accounting entry needed for zero balance

        var journalEntryId = Guid.NewGuid();
        var performedBy = Guid.Empty;

        // Debit Gift Card Liability (decrease liability)
        var liabilityGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, GiftCardLiabilityAccountCode)));

        await liabilityGrain.PostDebitAsync(new PostDebitCommand(
            Amount: evt.ExpiredBalance,
            Description: $"Gift card expired - {evt.CardNumber}",
            PerformedBy: performedBy,
            ReferenceType: "GiftCardExpiration",
            ReferenceId: evt.CardId,
            AccountingJournalEntryId: journalEntryId));

        // Credit Breakage Income (increase income)
        var breakageGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, BreakageIncomeAccountCode)));

        await breakageGrain.PostCreditAsync(new PostCreditCommand(
            Amount: evt.ExpiredBalance,
            Description: $"Gift card breakage income - {evt.CardNumber}",
            PerformedBy: performedBy,
            ReferenceType: "GiftCardExpiration",
            ReferenceId: evt.CardId,
            AccountingJournalEntryId: journalEntryId));

        _logger.LogInformation(
            "Created journal entry {JournalEntryId} for gift card expiration (breakage)",
            journalEntryId);
    }

    private static Guid GetAccountId(Guid orgId, string accountCode)
    {
        var input = $"{orgId}:{accountCode}";
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}
