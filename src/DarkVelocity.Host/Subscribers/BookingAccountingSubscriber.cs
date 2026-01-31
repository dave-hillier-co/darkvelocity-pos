using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains.Subscribers;

/// <summary>
/// Implicit stream subscriber that handles booking events for accounting.
/// Creates journal entries for deposit lifecycle events.
///
/// Accounting treatments:
/// - Deposit Paid: Debit Cash, Credit Customer Deposits Liability
/// - Deposit Applied: Debit Customer Deposits Liability, Credit Sales Revenue
/// - Deposit Refunded: Debit Customer Deposits Liability, Credit Cash
/// - Deposit Forfeited: Debit Customer Deposits Liability, Credit Other Income
/// </summary>
[ImplicitStreamSubscription(StreamConstants.BookingStreamNamespace)]
public class BookingAccountingSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<BookingAccountingSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;

    // Standard account codes - these would be configured per organization
    private const string CashAccountCode = "1000";
    private const string CustomerDepositsAccountCode = "2200";
    private const string SalesRevenueAccountCode = "4000";
    private const string OtherIncomeAccountCode = "4900";

    public BookingAccountingSubscriberGrain(ILogger<BookingAccountingSubscriberGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.BookingStreamNamespace, this.GetPrimaryKeyString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        _subscription = await stream.SubscribeAsync(this);

        _logger.LogInformation(
            "BookingAccountingSubscriber activated for organization {OrgId}",
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
                case BookingDepositPaidEvent paidEvent:
                    await HandleDepositPaidAsync(paidEvent);
                    break;

                case BookingDepositAppliedToOrderEvent appliedEvent:
                    await HandleDepositAppliedAsync(appliedEvent);
                    break;

                case BookingDepositRefundedEvent refundedEvent:
                    await HandleDepositRefundedAsync(refundedEvent);
                    break;

                case BookingDepositForfeitedEvent forfeitedEvent:
                    await HandleDepositForfeitedAsync(forfeitedEvent);
                    break;

                default:
                    _logger.LogDebug("Ignoring unhandled event type: {EventType}", item.GetType().Name);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling booking event {EventType}: {EventId}", item.GetType().Name, item.EventId);
        }
    }

    public Task OnCompletedAsync()
    {
        _logger.LogInformation("Booking event stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in booking event stream");
        return Task.CompletedTask;
    }

    private async Task HandleDepositPaidAsync(BookingDepositPaidEvent evt)
    {
        _logger.LogInformation(
            "Booking deposit paid for {BookingId}: {Amount:C}",
            evt.BookingId,
            evt.AmountPaid);

        var journalEntryId = Guid.NewGuid();
        var performedBy = Guid.Empty; // System action

        // Debit Cash (increase asset)
        var cashGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, CashAccountCode)));

        await cashGrain.PostDebitAsync(new PostDebitCommand(
            Amount: evt.AmountPaid,
            Description: $"Booking deposit received - {evt.BookingId}",
            PerformedBy: performedBy,
            ReferenceNumber: evt.PaymentReference,
            ReferenceType: "BookingDeposit",
            ReferenceId: evt.BookingId,
            AccountingJournalEntryId: journalEntryId));

        // Credit Customer Deposits Liability (increase liability)
        var depositsGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, CustomerDepositsAccountCode)));

        await depositsGrain.PostCreditAsync(new PostCreditCommand(
            Amount: evt.AmountPaid,
            Description: $"Booking deposit received - {evt.BookingId}",
            PerformedBy: performedBy,
            ReferenceNumber: evt.PaymentReference,
            ReferenceType: "BookingDeposit",
            ReferenceId: evt.BookingId,
            AccountingJournalEntryId: journalEntryId));

        _logger.LogInformation(
            "Created journal entry {JournalEntryId} for booking deposit payment",
            journalEntryId);
    }

    private async Task HandleDepositAppliedAsync(BookingDepositAppliedToOrderEvent evt)
    {
        _logger.LogInformation(
            "Booking deposit applied for {BookingId}: {Amount:C} to order {OrderId}",
            evt.BookingId,
            evt.AmountApplied,
            evt.OrderId);

        var journalEntryId = Guid.NewGuid();
        var performedBy = Guid.Empty;

        // Debit Customer Deposits Liability (decrease liability)
        var depositsGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, CustomerDepositsAccountCode)));

        await depositsGrain.PostDebitAsync(new PostDebitCommand(
            Amount: evt.AmountApplied,
            Description: $"Deposit applied to order {evt.OrderId}",
            PerformedBy: performedBy,
            ReferenceType: "OrderPayment",
            ReferenceId: evt.OrderId,
            AccountingJournalEntryId: journalEntryId));

        // Credit Sales Revenue (increase revenue)
        var salesGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, SalesRevenueAccountCode)));

        await salesGrain.PostCreditAsync(new PostCreditCommand(
            Amount: evt.AmountApplied,
            Description: $"Deposit applied to order {evt.OrderId}",
            PerformedBy: performedBy,
            ReferenceType: "OrderPayment",
            ReferenceId: evt.OrderId,
            AccountingJournalEntryId: journalEntryId));

        _logger.LogInformation(
            "Created journal entry {JournalEntryId} for deposit application",
            journalEntryId);
    }

    private async Task HandleDepositRefundedAsync(BookingDepositRefundedEvent evt)
    {
        _logger.LogInformation(
            "Booking deposit refunded for {BookingId}: {Amount:C}",
            evt.BookingId,
            evt.AmountRefunded);

        var journalEntryId = Guid.NewGuid();
        var performedBy = Guid.Empty;

        // Debit Customer Deposits Liability (decrease liability)
        var depositsGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, CustomerDepositsAccountCode)));

        await depositsGrain.PostDebitAsync(new PostDebitCommand(
            Amount: evt.AmountRefunded,
            Description: $"Booking deposit refunded - {evt.BookingId}. Reason: {evt.Reason}",
            PerformedBy: performedBy,
            ReferenceType: "BookingRefund",
            ReferenceId: evt.BookingId,
            AccountingJournalEntryId: journalEntryId));

        // Credit Cash (decrease asset)
        var cashGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, CashAccountCode)));

        await cashGrain.PostCreditAsync(new PostCreditCommand(
            Amount: evt.AmountRefunded,
            Description: $"Booking deposit refunded - {evt.BookingId}. Reason: {evt.Reason}",
            PerformedBy: performedBy,
            ReferenceType: "BookingRefund",
            ReferenceId: evt.BookingId,
            AccountingJournalEntryId: journalEntryId));

        _logger.LogInformation(
            "Created journal entry {JournalEntryId} for deposit refund",
            journalEntryId);
    }

    private async Task HandleDepositForfeitedAsync(BookingDepositForfeitedEvent evt)
    {
        _logger.LogInformation(
            "Booking deposit forfeited for {BookingId}: {Amount:C}. Reason: {Reason}",
            evt.BookingId,
            evt.AmountForfeited,
            evt.ForfeitureReason);

        var journalEntryId = Guid.NewGuid();
        var performedBy = Guid.Empty;

        // Debit Customer Deposits Liability (decrease liability)
        var depositsGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, CustomerDepositsAccountCode)));

        await depositsGrain.PostDebitAsync(new PostDebitCommand(
            Amount: evt.AmountForfeited,
            Description: $"Booking deposit forfeited - {evt.BookingId}. Reason: {evt.ForfeitureReason}",
            PerformedBy: performedBy,
            ReferenceType: "BookingForfeiture",
            ReferenceId: evt.BookingId,
            AccountingJournalEntryId: journalEntryId));

        // Credit Other Income (forfeited deposits are income)
        var incomeGrain = GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, OtherIncomeAccountCode)));

        await incomeGrain.PostCreditAsync(new PostCreditCommand(
            Amount: evt.AmountForfeited,
            Description: $"Forfeited booking deposit - {evt.BookingId}",
            PerformedBy: performedBy,
            ReferenceType: "BookingForfeiture",
            ReferenceId: evt.BookingId,
            AccountingJournalEntryId: journalEntryId));

        _logger.LogInformation(
            "Created journal entry {JournalEntryId} for deposit forfeiture",
            journalEntryId);
    }

    /// <summary>
    /// Gets the account ID for a given account code.
    /// In production, this would look up from a chart of accounts service.
    /// For now, we generate a deterministic GUID from the org + code.
    /// </summary>
    private static Guid GetAccountId(Guid orgId, string accountCode)
    {
        // Generate deterministic GUID from org ID and account code
        var input = $"{orgId}:{accountCode}";
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}
