using System.Security.Cryptography;
using System.Text;
using DarkVelocity.Orleans.Abstractions;
using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.State;
using Orleans.Runtime;

namespace DarkVelocity.Orleans.Grains;

public class GiftCardGrain : Grain, IGiftCardGrain
{
    private readonly IPersistentState<GiftCardState> _state;

    public GiftCardGrain(
        [PersistentState("giftcard", "OrleansStorage")]
        IPersistentState<GiftCardState> state)
    {
        _state = state;
    }

    public async Task<GiftCardCreatedResult> CreateAsync(CreateGiftCardCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Gift card already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, cardId) = GrainKeys.ParseOrgEntity(key);

        _state.State = new GiftCardState
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

        await _state.WriteStateAsync();

        return new GiftCardCreatedResult(cardId, command.CardNumber, _state.State.CreatedAt);
    }

    public Task<GiftCardState> GetStateAsync() => Task.FromResult(_state.State);

    public async Task<GiftCardActivatedResult> ActivateAsync(ActivateGiftCardCommand command)
    {
        EnsureExists();

        if (_state.State.Status != GiftCardStatus.Inactive)
            throw new InvalidOperationException($"Cannot activate gift card: {_state.State.Status}");

        _state.State.Status = GiftCardStatus.Active;
        _state.State.ActivatedAt = DateTime.UtcNow;
        _state.State.ActivatedBy = command.ActivatedBy;
        _state.State.ActivationSiteId = command.SiteId;
        _state.State.ActivationOrderId = command.OrderId;
        _state.State.PurchaserCustomerId = command.PurchaserCustomerId;
        _state.State.PurchaserName = command.PurchaserName;
        _state.State.PurchaserEmail = command.PurchaserEmail;

        var transaction = new GiftCardTransaction
        {
            Id = Guid.NewGuid(),
            Type = GiftCardTransactionType.Activation,
            Amount = _state.State.InitialValue,
            BalanceAfter = _state.State.CurrentBalance,
            SiteId = command.SiteId,
            OrderId = command.OrderId,
            PerformedBy = command.ActivatedBy,
            Timestamp = DateTime.UtcNow
        };
        _state.State.Transactions.Add(transaction);
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new GiftCardActivatedResult(_state.State.CurrentBalance, _state.State.ActivatedAt.Value);
    }

    public async Task SetRecipientAsync(SetRecipientCommand command)
    {
        EnsureExists();

        _state.State.RecipientCustomerId = command.CustomerId;
        _state.State.RecipientName = command.Name;
        _state.State.RecipientEmail = command.Email;
        _state.State.RecipientPhone = command.Phone;
        _state.State.PersonalMessage = command.PersonalMessage;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task<RedemptionResult> RedeemAsync(RedeemGiftCardCommand command)
    {
        EnsureExists();
        EnsureActive();
        EnsureNotExpired();

        if (command.Amount > _state.State.CurrentBalance)
            throw new InvalidOperationException("Insufficient balance");

        _state.State.CurrentBalance -= command.Amount;
        _state.State.TotalRedeemed += command.Amount;
        _state.State.RedemptionCount++;
        _state.State.LastUsedAt = DateTime.UtcNow;
        _state.State.LastUsedSiteId = command.SiteId;

        if (_state.State.CurrentBalance == 0)
            _state.State.Status = GiftCardStatus.Depleted;

        var transaction = new GiftCardTransaction
        {
            Id = Guid.NewGuid(),
            Type = GiftCardTransactionType.Redemption,
            Amount = -command.Amount,
            BalanceAfter = _state.State.CurrentBalance,
            OrderId = command.OrderId,
            PaymentId = command.PaymentId,
            SiteId = command.SiteId,
            PerformedBy = command.PerformedBy,
            Timestamp = DateTime.UtcNow
        };
        _state.State.Transactions.Add(transaction);
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new RedemptionResult(command.Amount, _state.State.CurrentBalance);
    }

    public async Task<decimal> ReloadAsync(ReloadGiftCardCommand command)
    {
        EnsureExists();

        if (_state.State.Status is GiftCardStatus.Cancelled or GiftCardStatus.Expired)
            throw new InvalidOperationException($"Cannot reload gift card: {_state.State.Status}");

        if (_state.State.Status == GiftCardStatus.Depleted)
            _state.State.Status = GiftCardStatus.Active;

        _state.State.CurrentBalance += command.Amount;
        _state.State.TotalReloaded += command.Amount;
        _state.State.LastUsedAt = DateTime.UtcNow;
        _state.State.LastUsedSiteId = command.SiteId;

        var transaction = new GiftCardTransaction
        {
            Id = Guid.NewGuid(),
            Type = GiftCardTransactionType.Reload,
            Amount = command.Amount,
            BalanceAfter = _state.State.CurrentBalance,
            OrderId = command.OrderId,
            SiteId = command.SiteId,
            PerformedBy = command.PerformedBy,
            Timestamp = DateTime.UtcNow,
            Notes = command.Notes
        };
        _state.State.Transactions.Add(transaction);
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return _state.State.CurrentBalance;
    }

    public async Task<decimal> RefundToCardAsync(RefundToGiftCardCommand command)
    {
        EnsureExists();

        if (_state.State.Status is GiftCardStatus.Cancelled or GiftCardStatus.Expired)
            throw new InvalidOperationException($"Cannot refund to gift card: {_state.State.Status}");

        if (_state.State.Status == GiftCardStatus.Depleted)
            _state.State.Status = GiftCardStatus.Active;

        _state.State.CurrentBalance += command.Amount;
        _state.State.LastUsedAt = DateTime.UtcNow;
        _state.State.LastUsedSiteId = command.SiteId;

        var transaction = new GiftCardTransaction
        {
            Id = Guid.NewGuid(),
            Type = GiftCardTransactionType.Refund,
            Amount = command.Amount,
            BalanceAfter = _state.State.CurrentBalance,
            PaymentId = command.OriginalPaymentId,
            SiteId = command.SiteId,
            PerformedBy = command.PerformedBy,
            Timestamp = DateTime.UtcNow,
            Notes = command.Notes
        };
        _state.State.Transactions.Add(transaction);
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return _state.State.CurrentBalance;
    }

    public async Task<decimal> AdjustBalanceAsync(AdjustGiftCardCommand command)
    {
        EnsureExists();

        var newBalance = _state.State.CurrentBalance + command.Amount;
        if (newBalance < 0)
            throw new InvalidOperationException("Adjustment would result in negative balance");

        _state.State.CurrentBalance = newBalance;

        if (newBalance == 0)
            _state.State.Status = GiftCardStatus.Depleted;
        else if (_state.State.Status == GiftCardStatus.Depleted)
            _state.State.Status = GiftCardStatus.Active;

        var transaction = new GiftCardTransaction
        {
            Id = Guid.NewGuid(),
            Type = GiftCardTransactionType.Adjustment,
            Amount = command.Amount,
            BalanceAfter = _state.State.CurrentBalance,
            PerformedBy = command.AdjustedBy,
            Timestamp = DateTime.UtcNow,
            Notes = command.Reason
        };
        _state.State.Transactions.Add(transaction);
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return _state.State.CurrentBalance;
    }

    public Task<bool> ValidatePinAsync(string pin)
    {
        EnsureExists();

        if (_state.State.Pin == null)
            return Task.FromResult(true); // No PIN required

        return Task.FromResult(_state.State.Pin == HashPin(pin));
    }

    public async Task ExpireAsync()
    {
        EnsureExists();

        if (_state.State.Status is GiftCardStatus.Cancelled or GiftCardStatus.Expired)
            throw new InvalidOperationException($"Cannot expire gift card: {_state.State.Status}");

        var previousBalance = _state.State.CurrentBalance;
        _state.State.Status = GiftCardStatus.Expired;

        if (previousBalance > 0)
        {
            var transaction = new GiftCardTransaction
            {
                Id = Guid.NewGuid(),
                Type = GiftCardTransactionType.Expiration,
                Amount = -previousBalance,
                BalanceAfter = 0,
                PerformedBy = Guid.Empty,
                Timestamp = DateTime.UtcNow,
                Notes = "Card expired"
            };
            _state.State.Transactions.Add(transaction);
            _state.State.CurrentBalance = 0;
        }

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(string reason, Guid cancelledBy)
    {
        EnsureExists();

        if (_state.State.Status == GiftCardStatus.Cancelled)
            throw new InvalidOperationException("Gift card already cancelled");

        var previousBalance = _state.State.CurrentBalance;
        _state.State.Status = GiftCardStatus.Cancelled;

        if (previousBalance > 0)
        {
            var transaction = new GiftCardTransaction
            {
                Id = Guid.NewGuid(),
                Type = GiftCardTransactionType.Void,
                Amount = -previousBalance,
                BalanceAfter = 0,
                PerformedBy = cancelledBy,
                Timestamp = DateTime.UtcNow,
                Notes = $"Cancelled: {reason}"
            };
            _state.State.Transactions.Add(transaction);
            _state.State.CurrentBalance = 0;
        }

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task VoidTransactionAsync(Guid transactionId, string reason, Guid voidedBy)
    {
        EnsureExists();

        var index = _state.State.Transactions.FindIndex(t => t.Id == transactionId);
        if (index < 0)
            throw new InvalidOperationException("Transaction not found");

        var originalTransaction = _state.State.Transactions[index];

        // Reverse the transaction
        var reversalAmount = -originalTransaction.Amount;
        var newBalance = _state.State.CurrentBalance + reversalAmount;

        if (newBalance < 0)
            throw new InvalidOperationException("Void would result in negative balance");

        _state.State.CurrentBalance = newBalance;

        var voidTransaction = new GiftCardTransaction
        {
            Id = Guid.NewGuid(),
            Type = GiftCardTransactionType.Void,
            Amount = reversalAmount,
            BalanceAfter = _state.State.CurrentBalance,
            PerformedBy = voidedBy,
            Timestamp = DateTime.UtcNow,
            Notes = $"Void of transaction {transactionId}: {reason}"
        };
        _state.State.Transactions.Add(voidTransaction);

        if (_state.State.CurrentBalance == 0)
            _state.State.Status = GiftCardStatus.Depleted;
        else if (_state.State.Status == GiftCardStatus.Depleted)
            _state.State.Status = GiftCardStatus.Active;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.Id != Guid.Empty);

    public Task<GiftCardBalanceInfo> GetBalanceInfoAsync()
        => Task.FromResult(new GiftCardBalanceInfo(
            _state.State.CurrentBalance,
            _state.State.Status,
            _state.State.ExpiresAt));

    public Task<bool> HasSufficientBalanceAsync(decimal amount)
    {
        if (_state.State.Status != GiftCardStatus.Active)
            return Task.FromResult(false);

        if (_state.State.ExpiresAt != null && _state.State.ExpiresAt < DateTime.UtcNow)
            return Task.FromResult(false);

        return Task.FromResult(_state.State.CurrentBalance >= amount);
    }

    public Task<IReadOnlyList<GiftCardTransaction>> GetTransactionsAsync()
        => Task.FromResult<IReadOnlyList<GiftCardTransaction>>(_state.State.Transactions);

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Gift card does not exist");
    }

    private void EnsureActive()
    {
        if (_state.State.Status != GiftCardStatus.Active)
            throw new InvalidOperationException($"Gift card is not active: {_state.State.Status}");
    }

    private void EnsureNotExpired()
    {
        if (_state.State.ExpiresAt != null && _state.State.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Gift card has expired");
    }

    private static string HashPin(string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
        return Convert.ToBase64String(bytes);
    }
}
