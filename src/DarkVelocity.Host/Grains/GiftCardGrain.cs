using System.Security.Cryptography;
using System.Text;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Context for creating gift card transactions.
/// </summary>
public record GiftCardTransactionContext(
    GiftCardTransactionType Type,
    Guid? OrderId = null,
    Guid? PaymentId = null,
    Guid? SiteId = null,
    Guid PerformedBy = default);

public class GiftCardGrain : LedgerGrain<GiftCardState, GiftCardTransaction>, IGiftCardGrain
{
    private IAsyncStream<IStreamEvent>? _giftCardStream;

    public GiftCardGrain(
        [PersistentState("giftcard", "OrleansStorage")]
        IPersistentState<GiftCardState> state) : base(state)
    {
    }

    protected override bool IsInitialized => State.State.Id != Guid.Empty;

    protected override GiftCardTransaction CreateTransaction(
        decimal amount,
        decimal balanceAfter,
        string? notes,
        object? context)
    {
        var ctx = context as GiftCardTransactionContext
            ?? new GiftCardTransactionContext(GiftCardTransactionType.Adjustment);

        return new GiftCardTransaction
        {
            Id = Guid.NewGuid(),
            Type = ctx.Type,
            Amount = amount,
            BalanceAfter = balanceAfter,
            OrderId = ctx.OrderId,
            PaymentId = ctx.PaymentId,
            SiteId = ctx.SiteId,
            PerformedBy = ctx.PerformedBy,
            Timestamp = DateTime.UtcNow,
            Notes = notes
        };
    }

    protected override void OnBalanceChanged(decimal previousBalance, decimal newBalance)
    {
        // Update status based on balance
        if (newBalance == 0 && State.State.Status == GiftCardStatus.Active)
            State.State.Status = GiftCardStatus.Depleted;
        else if (newBalance > 0 && State.State.Status == GiftCardStatus.Depleted)
            State.State.Status = GiftCardStatus.Active;

        State.State.UpdatedAt = DateTime.UtcNow;
    }

    private IAsyncStream<IStreamEvent> GetGiftCardStream()
    {
        if (_giftCardStream == null && State.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.GiftCardStreamNamespace, State.State.OrganizationId.ToString());
            _giftCardStream = streamProvider.GetStream<IStreamEvent>(streamId);
        }
        return _giftCardStream!;
    }

    public async Task<GiftCardCreatedResult> CreateAsync(CreateGiftCardCommand command)
    {
        if (State.State.Id != Guid.Empty)
            throw new InvalidOperationException("Gift card already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, cardId) = GrainKeys.ParseOrgEntity(key);

        State.State = new GiftCardState
        {
            Id = cardId,
            OrganizationId = command.OrganizationId,
            CardNumber = command.CardNumber,
            Type = command.Type,
            Status = GiftCardStatus.Inactive,
            InitialValue = command.InitialValue,
            CurrentBalance = command.InitialValue,
            Currency = command.Currency,
            ExpiresAt = command.ExpiresAt,
            Pin = command.Pin != null ? HashPin(command.Pin) : null,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await State.WriteStateAsync();

        return new GiftCardCreatedResult(cardId, command.CardNumber, State.State.CreatedAt);
    }

    public Task<GiftCardState> GetStateAsync() => Task.FromResult(State.State);

    public async Task<GiftCardActivatedResult> ActivateAsync(ActivateGiftCardCommand command)
    {
        EnsureInitialized();

        if (State.State.Status != GiftCardStatus.Inactive)
            throw new InvalidOperationException($"Cannot activate gift card: {State.State.Status}");

        State.State.Status = GiftCardStatus.Active;
        State.State.ActivatedAt = DateTime.UtcNow;
        State.State.ActivatedBy = command.ActivatedBy;
        State.State.ActivationSiteId = command.SiteId;
        State.State.ActivationOrderId = command.OrderId;
        State.State.PurchaserCustomerId = command.PurchaserCustomerId;
        State.State.PurchaserName = command.PurchaserName;
        State.State.PurchaserEmail = command.PurchaserEmail;

        // Record activation transaction (no balance change)
        await RecordTransactionAsync(
            State.State.InitialValue,
            null,
            new GiftCardTransactionContext(
                GiftCardTransactionType.Activation,
                OrderId: command.OrderId,
                SiteId: command.SiteId,
                PerformedBy: command.ActivatedBy));

        // Publish gift card activated event
        if (GetGiftCardStream() != null)
        {
            await GetGiftCardStream().OnNextAsync(new GiftCardActivatedEvent(
                State.State.Id,
                command.SiteId,
                State.State.CardNumber,
                State.State.InitialValue,
                command.PurchaserCustomerId,
                command.OrderId)
            {
                OrganizationId = State.State.OrganizationId
            });
        }

        return new GiftCardActivatedResult(State.State.CurrentBalance, State.State.ActivatedAt.Value);
    }

    public async Task SetRecipientAsync(SetRecipientCommand command)
    {
        EnsureInitialized();

        State.State.RecipientCustomerId = command.CustomerId;
        State.State.RecipientName = command.Name;
        State.State.RecipientEmail = command.Email;
        State.State.RecipientPhone = command.Phone;
        State.State.PersonalMessage = command.PersonalMessage;
        State.State.UpdatedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();
    }

    public async Task<RedemptionResult> RedeemAsync(RedeemGiftCardCommand command)
    {
        EnsureInitialized();
        EnsureActive();
        EnsureNotExpired();

        var result = await DebitAsync(
            command.Amount,
            null,
            new GiftCardTransactionContext(
                GiftCardTransactionType.Redemption,
                OrderId: command.OrderId,
                PaymentId: command.PaymentId,
                SiteId: command.SiteId,
                PerformedBy: command.PerformedBy));

        State.State.TotalRedeemed += command.Amount;
        State.State.RedemptionCount++;
        State.State.LastUsedAt = DateTime.UtcNow;
        State.State.LastUsedSiteId = command.SiteId;

        // Publish gift card redeemed event
        if (GetGiftCardStream() != null)
        {
            await GetGiftCardStream().OnNextAsync(new GiftCardRedeemedEvent(
                State.State.Id,
                command.SiteId,
                State.State.CardNumber,
                command.Amount,
                State.State.CurrentBalance,
                command.OrderId,
                State.State.RecipientCustomerId)
            {
                OrganizationId = State.State.OrganizationId
            });
        }

        return new RedemptionResult(command.Amount, result.BalanceAfter);
    }

    public async Task<decimal> ReloadAsync(ReloadGiftCardCommand command)
    {
        EnsureInitialized();

        if (State.State.Status is GiftCardStatus.Cancelled or GiftCardStatus.Expired)
            throw new InvalidOperationException($"Cannot reload gift card: {State.State.Status}");

        var result = await CreditAsync(
            command.Amount,
            command.Notes,
            new GiftCardTransactionContext(
                GiftCardTransactionType.Reload,
                OrderId: command.OrderId,
                SiteId: command.SiteId,
                PerformedBy: command.PerformedBy));

        State.State.TotalReloaded += command.Amount;
        State.State.LastUsedAt = DateTime.UtcNow;
        State.State.LastUsedSiteId = command.SiteId;

        // Publish gift card reloaded event
        if (GetGiftCardStream() != null)
        {
            await GetGiftCardStream().OnNextAsync(new GiftCardReloadedEvent(
                State.State.Id,
                command.SiteId,
                State.State.CardNumber,
                command.Amount,
                result.BalanceAfter,
                command.OrderId)
            {
                OrganizationId = State.State.OrganizationId
            });
        }

        return result.BalanceAfter;
    }

    public async Task<decimal> RefundToCardAsync(RefundToGiftCardCommand command)
    {
        EnsureInitialized();

        if (State.State.Status is GiftCardStatus.Cancelled or GiftCardStatus.Expired)
            throw new InvalidOperationException($"Cannot refund to gift card: {State.State.Status}");

        var result = await CreditAsync(
            command.Amount,
            command.Notes,
            new GiftCardTransactionContext(
                GiftCardTransactionType.Refund,
                PaymentId: command.OriginalPaymentId,
                SiteId: command.SiteId,
                PerformedBy: command.PerformedBy));

        State.State.LastUsedAt = DateTime.UtcNow;
        State.State.LastUsedSiteId = command.SiteId;

        // Publish refund applied event
        if (_giftCardStream != null && command.OriginalOrderId != null)
        {
            await GetGiftCardStream().OnNextAsync(new GiftCardRefundAppliedEvent(
                State.State.Id,
                command.SiteId,
                State.State.CardNumber,
                command.Amount,
                result.BalanceAfter,
                command.OriginalOrderId.Value,
                command.Notes)
            {
                OrganizationId = State.State.OrganizationId
            });
        }

        return result.BalanceAfter;
    }

    public async Task<decimal> AdjustBalanceAsync(AdjustGiftCardCommand command)
    {
        EnsureInitialized();

        var result = command.Amount >= 0
            ? await CreditAsync(
                command.Amount,
                command.Reason,
                new GiftCardTransactionContext(
                    GiftCardTransactionType.Adjustment,
                    PerformedBy: command.AdjustedBy))
            : await DebitAsync(
                -command.Amount,
                command.Reason,
                new GiftCardTransactionContext(
                    GiftCardTransactionType.Adjustment,
                    PerformedBy: command.AdjustedBy));

        return result.BalanceAfter;
    }

    public Task<bool> ValidatePinAsync(string pin)
    {
        EnsureInitialized();

        if (State.State.Pin == null)
            return Task.FromResult(true); // No PIN required

        return Task.FromResult(State.State.Pin == HashPin(pin));
    }

    public async Task ExpireAsync()
    {
        EnsureInitialized();

        if (State.State.Status is GiftCardStatus.Cancelled or GiftCardStatus.Expired)
            throw new InvalidOperationException($"Cannot expire gift card: {State.State.Status}");

        var previousBalance = State.State.CurrentBalance;
        State.State.Status = GiftCardStatus.Expired;

        if (previousBalance > 0)
        {
            await DebitAsync(
                previousBalance,
                "Card expired",
                new GiftCardTransactionContext(GiftCardTransactionType.Expiration),
                allowNegative: false);
        }
        else
        {
            State.State.UpdatedAt = DateTime.UtcNow;
            State.State.Version++;
            await State.WriteStateAsync();
        }

        // Publish gift card expired event
        if (_giftCardStream != null && previousBalance > 0)
        {
            await GetGiftCardStream().OnNextAsync(new GiftCardExpiredEvent(
                State.State.Id,
                State.State.ActivationSiteId ?? Guid.Empty,
                State.State.CardNumber,
                previousBalance)
            {
                OrganizationId = State.State.OrganizationId
            });
        }
    }

    public async Task CancelAsync(string reason, Guid cancelledBy)
    {
        EnsureInitialized();

        if (State.State.Status == GiftCardStatus.Cancelled)
            throw new InvalidOperationException("Gift card already cancelled");

        var previousBalance = State.State.CurrentBalance;
        State.State.Status = GiftCardStatus.Cancelled;

        if (previousBalance > 0)
        {
            await DebitAsync(
                previousBalance,
                $"Cancelled: {reason}",
                new GiftCardTransactionContext(
                    GiftCardTransactionType.Void,
                    PerformedBy: cancelledBy),
                allowNegative: false);
        }
        else
        {
            State.State.UpdatedAt = DateTime.UtcNow;
            State.State.Version++;
            await State.WriteStateAsync();
        }
    }

    public async Task VoidTransactionAsync(Guid transactionId, string reason, Guid voidedBy)
    {
        EnsureInitialized();

        var originalTransaction = State.State.Transactions.FirstOrDefault(t => t.Id == transactionId)
            ?? throw new InvalidOperationException("Transaction not found");

        // Reverse the transaction
        var reversalAmount = -originalTransaction.Amount;

        if (reversalAmount > 0)
        {
            await CreditAsync(
                reversalAmount,
                $"Void of transaction {transactionId}: {reason}",
                new GiftCardTransactionContext(
                    GiftCardTransactionType.Void,
                    PerformedBy: voidedBy));
        }
        else
        {
            await DebitAsync(
                -reversalAmount,
                $"Void of transaction {transactionId}: {reason}",
                new GiftCardTransactionContext(
                    GiftCardTransactionType.Void,
                    PerformedBy: voidedBy));
        }
    }

    public Task<bool> ExistsAsync() => Task.FromResult(IsInitialized);

    public Task<GiftCardBalanceInfo> GetBalanceInfoAsync()
        => Task.FromResult(new GiftCardBalanceInfo(
            State.State.CurrentBalance,
            State.State.Status,
            State.State.ExpiresAt));

    public Task<bool> HasSufficientBalanceAsync(decimal amount)
    {
        if (State.State.Status != GiftCardStatus.Active)
            return Task.FromResult(false);

        if (State.State.ExpiresAt != null && State.State.ExpiresAt < DateTime.UtcNow)
            return Task.FromResult(false);

        return Task.FromResult(HasSufficientBalance(amount));
    }

    public Task<IReadOnlyList<GiftCardTransaction>> GetTransactionsAsync()
        => Task.FromResult<IReadOnlyList<GiftCardTransaction>>(State.State.Transactions);

    private void EnsureActive()
    {
        if (State.State.Status != GiftCardStatus.Active)
            throw new InvalidOperationException($"Gift card is not active: {State.State.Status}");
    }

    private void EnsureNotExpired()
    {
        if (State.State.ExpiresAt != null && State.State.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Gift card has expired");
    }

    private static string HashPin(string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
        return Convert.ToBase64String(bytes);
    }
}
