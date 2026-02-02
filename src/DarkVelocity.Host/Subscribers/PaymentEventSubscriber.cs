using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains.Subscribers;

/// <summary>
/// Implicit stream subscriber that handles payment events.
/// Records payments on orders and creates accounting journal entries.
///
/// Accounting treatments:
/// - Payment Completed: Debit Cash/AR, Credit Sales Revenue (per method)
/// - Payment Refunded: Debit Refund Expense, Credit Cash/AR
/// - Payment Voided: Reverses original entries
/// </summary>
[ImplicitStreamSubscription(StreamConstants.PaymentStreamNamespace)]
public class PaymentEventSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<PaymentEventSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;

    // Standard account codes
    private const string CashAccountCode = "1000";
    private const string AccountsReceivableCode = "1200";
    private const string SalesRevenueAccountCode = "4000";
    private const string TipsPayableAccountCode = "2100";
    private const string RefundExpenseAccountCode = "5900";

    public PaymentEventSubscriberGrain(ILogger<PaymentEventSubscriberGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.PaymentStreamNamespace, this.GetPrimaryKeyString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        _subscription = await stream.SubscribeAsync(this);

        _logger.LogInformation(
            "PaymentEventSubscriber activated for organization {OrgId}",
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
                case PaymentCompletedEvent completedEvent:
                    await HandlePaymentCompletedAsync(completedEvent);
                    break;

                case PaymentRefundedEvent refundedEvent:
                    await HandlePaymentRefundedAsync(refundedEvent);
                    break;

                case PaymentVoidedEvent voidedEvent:
                    await HandlePaymentVoidedAsync(voidedEvent);
                    break;

                case PaymentInitiatedEvent initiatedEvent:
                    _logger.LogDebug(
                        "Payment {PaymentId} initiated for order {OrderId}",
                        initiatedEvent.PaymentId,
                        initiatedEvent.OrderId);
                    break;

                default:
                    _logger.LogDebug("Ignoring unhandled event type: {EventType}", item.GetType().Name);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling payment event {EventType}: {EventId}", item.GetType().Name, item.EventId);
        }
    }

    public Task OnCompletedAsync()
    {
        _logger.LogInformation("Payment event stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in payment event stream");
        return Task.CompletedTask;
    }

    private async Task HandlePaymentCompletedAsync(PaymentCompletedEvent evt)
    {
        _logger.LogInformation(
            "Payment {PaymentId} completed for {Amount:C} ({Method}) on order {OrderId}",
            evt.PaymentId,
            evt.TotalAmount,
            evt.Method,
            evt.OrderId);

        // Record payment on order grain
        var orderGrain = GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(evt.OrganizationId, evt.SiteId, evt.OrderId));

        await orderGrain.RecordPaymentAsync(
            evt.PaymentId,
            evt.Amount,
            evt.TipAmount,
            evt.Method);

        // For gift card payments, redeem from the gift card
        if (evt.Method == "GiftCard" && evt.GiftCardId.HasValue)
        {
            await RedeemFromGiftCardAsync(evt);
        }

        // Create accounting journal entry
        var journalEntryId = Guid.NewGuid();
        var performedBy = evt.CashierId;

        // Determine debit account based on payment method
        var debitAccountCode = evt.Method switch
        {
            "Cash" => CashAccountCode,
            "CreditCard" or "DebitCard" => AccountsReceivableCode,
            _ => CashAccountCode
        };

        // Debit Cash/AR (increase asset)
        var debitGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, debitAccountCode)));

        await debitGrain.PostDebitAsync(new PostDebitCommand(
            Amount: evt.TotalAmount,
            Description: $"Payment received - {evt.Method} for order {evt.OrderId}",
            PerformedBy: performedBy,
            ReferenceType: "Payment",
            ReferenceId: evt.PaymentId,
            AccountingJournalEntryId: journalEntryId));

        // Credit Sales Revenue (increase revenue) - excluding tips
        var salesGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, SalesRevenueAccountCode)));

        await salesGrain.PostCreditAsync(new PostCreditCommand(
            Amount: evt.Amount,
            Description: $"Payment received - {evt.Method} for order {evt.OrderId}",
            PerformedBy: performedBy,
            ReferenceType: "Payment",
            ReferenceId: evt.PaymentId,
            AccountingJournalEntryId: journalEntryId));

        // Credit Tips Payable if there's a tip
        if (evt.TipAmount > 0)
        {
            var tipsGrain = GrainFactory.GetGrain<IAccountGrain>(
                GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, TipsPayableAccountCode)));

            await tipsGrain.PostCreditAsync(new PostCreditCommand(
                Amount: evt.TipAmount,
                Description: $"Tip received for order {evt.OrderId}",
                PerformedBy: performedBy,
                ReferenceType: "PaymentTip",
                ReferenceId: evt.PaymentId,
                AccountingJournalEntryId: journalEntryId));
        }

        _logger.LogInformation(
            "Created journal entry {JournalEntryId} for payment completion",
            journalEntryId);
    }

    private async Task HandlePaymentRefundedAsync(PaymentRefundedEvent evt)
    {
        _logger.LogInformation(
            "Payment {PaymentId} refunded {RefundAmount:C}. Total refunded: {TotalRefunded:C}",
            evt.PaymentId,
            evt.RefundAmount,
            evt.TotalRefundedAmount);

        // For gift card payments, credit the refund back to the gift card
        if (evt.Method == "GiftCard" && evt.GiftCardId.HasValue)
        {
            await RefundToGiftCardAsync(evt);
        }

        var journalEntryId = Guid.NewGuid();

        // Debit Refund Expense (increase expense)
        var refundGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, RefundExpenseAccountCode)));

        await refundGrain.PostDebitAsync(new PostDebitCommand(
            Amount: evt.RefundAmount,
            Description: $"Payment refund - {evt.Method} for order {evt.OrderId}. Reason: {evt.Reason}",
            PerformedBy: evt.IssuedBy,
            ReferenceType: "PaymentRefund",
            ReferenceId: evt.RefundId,
            AccountingJournalEntryId: journalEntryId));

        // Credit Cash/AR (decrease asset)
        var creditAccountCode = evt.Method switch
        {
            "Cash" => CashAccountCode,
            "CreditCard" or "DebitCard" => AccountsReceivableCode,
            _ => CashAccountCode
        };

        var creditGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, creditAccountCode)));

        await creditGrain.PostCreditAsync(new PostCreditCommand(
            Amount: evt.RefundAmount,
            Description: $"Payment refund - {evt.Method} for order {evt.OrderId}",
            PerformedBy: evt.IssuedBy,
            ReferenceType: "PaymentRefund",
            ReferenceId: evt.RefundId,
            AccountingJournalEntryId: journalEntryId));

        _logger.LogInformation(
            "Created journal entry {JournalEntryId} for payment refund",
            journalEntryId);
    }

    private async Task HandlePaymentVoidedAsync(PaymentVoidedEvent evt)
    {
        _logger.LogInformation(
            "Payment {PaymentId} voided for {Amount:C}. Reason: {Reason}",
            evt.PaymentId,
            evt.VoidedAmount,
            evt.Reason);

        // For voided payments, we reverse the original entries
        var journalEntryId = Guid.NewGuid();

        // Credit Cash/AR (reverse the original debit)
        var creditAccountCode = evt.Method switch
        {
            "Cash" => CashAccountCode,
            "CreditCard" or "DebitCard" => AccountsReceivableCode,
            _ => CashAccountCode
        };

        var creditGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, creditAccountCode)));

        await creditGrain.PostCreditAsync(new PostCreditCommand(
            Amount: evt.VoidedAmount,
            Description: $"Payment voided - {evt.Method} for order {evt.OrderId}. Reason: {evt.Reason}",
            PerformedBy: evt.VoidedBy,
            ReferenceType: "PaymentVoid",
            ReferenceId: evt.PaymentId,
            AccountingJournalEntryId: journalEntryId));

        // Debit Sales Revenue (reverse the original credit)
        var salesGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, SalesRevenueAccountCode)));

        await salesGrain.PostDebitAsync(new PostDebitCommand(
            Amount: evt.VoidedAmount,
            Description: $"Payment voided - {evt.Method} for order {evt.OrderId}",
            PerformedBy: evt.VoidedBy,
            ReferenceType: "PaymentVoid",
            ReferenceId: evt.PaymentId,
            AccountingJournalEntryId: journalEntryId));

        _logger.LogInformation(
            "Created journal entry {JournalEntryId} for payment void reversal",
            journalEntryId);
    }

    /// <summary>
    /// Redeems the payment amount from the gift card.
    /// This decouples the payment domain from the gift card domain via events.
    /// </summary>
    private async Task RedeemFromGiftCardAsync(PaymentCompletedEvent evt)
    {
        _logger.LogInformation(
            "Redeeming {Amount:C} from gift card {GiftCardId} for payment {PaymentId}",
            evt.Amount,
            evt.GiftCardId,
            evt.PaymentId);

        var giftCardGrain = GrainFactory.GetGrain<IGiftCardGrain>(
            GrainKeys.GiftCard(evt.OrganizationId, evt.GiftCardId!.Value));

        try
        {
            var result = await giftCardGrain.RedeemAsync(new RedeemGiftCardCommand(
                Amount: evt.Amount,
                OrderId: evt.OrderId,
                PaymentId: evt.PaymentId,
                SiteId: evt.SiteId,
                PerformedBy: evt.CashierId));

            _logger.LogInformation(
                "Gift card {GiftCardId} redeemed {Amount:C}, remaining balance: {RemainingBalance:C}",
                evt.GiftCardId,
                result.AmountRedeemed,
                result.RemainingBalance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to redeem from gift card {GiftCardId} for payment {PaymentId}",
                evt.GiftCardId,
                evt.PaymentId);
            throw;
        }
    }

    /// <summary>
    /// Credits the refund amount back to the gift card.
    /// This decouples the payment domain from the gift card domain via events.
    /// </summary>
    private async Task RefundToGiftCardAsync(PaymentRefundedEvent evt)
    {
        _logger.LogInformation(
            "Crediting refund {Amount:C} to gift card {GiftCardId} for payment {PaymentId}",
            evt.RefundAmount,
            evt.GiftCardId,
            evt.PaymentId);

        var giftCardGrain = GrainFactory.GetGrain<IGiftCardGrain>(
            GrainKeys.GiftCard(evt.OrganizationId, evt.GiftCardId!.Value));

        try
        {
            var newBalance = await giftCardGrain.RefundToCardAsync(new RefundToGiftCardCommand(
                Amount: evt.RefundAmount,
                OriginalPaymentId: evt.PaymentId,
                SiteId: evt.SiteId,
                PerformedBy: evt.IssuedBy,
                OriginalOrderId: evt.OrderId,
                Notes: evt.Reason));

            _logger.LogInformation(
                "Gift card {GiftCardId} credited {Amount:C}, new balance: {NewBalance:C}",
                evt.GiftCardId,
                evt.RefundAmount,
                newBalance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to refund to gift card {GiftCardId} for payment {PaymentId}",
                evt.GiftCardId,
                evt.PaymentId);
            throw;
        }
    }

    private static Guid GetAccountId(Guid orgId, string accountCode)
    {
        var input = $"{orgId}:{accountCode}";
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}
