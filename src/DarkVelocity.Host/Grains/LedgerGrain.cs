using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Represents a transaction in a ledger - the common behavior across
/// GiftCardTransaction, StockMovement, DrawerTransaction, etc.
/// </summary>
public interface ILedgerTransaction
{
    Guid Id { get; }
    decimal Amount { get; }
    DateTime Timestamp { get; }
    string? Notes { get; }
}

/// <summary>
/// Represents state that maintains a balance with transaction history.
/// Implemented by GiftCardState, CashDrawerState, etc.
/// </summary>
public interface ILedgerState<TTransaction> where TTransaction : ILedgerTransaction
{
    Guid OrganizationId { get; }
    decimal Balance { get; set; }
    List<TTransaction> Transactions { get; }
    int Version { get; set; }
}

/// <summary>
/// Result of a ledger operation (credit, debit, adjustment).
/// </summary>
public record LedgerOperationResult(
    Guid TransactionId,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    DateTime Timestamp);

/// <summary>
/// Provides core ledger behavior: balance tracking with transaction history.
///
/// This abstraction unifies the common pattern found in:
/// - GiftCardGrain (CurrentBalance + GiftCardTransactions)
/// - CashDrawerGrain (ExpectedBalance + DrawerTransactions)
/// - InventoryGrain (QuantityOnHand + StockMovements)
///
/// Subclasses implement domain-specific operations while delegating
/// balance management to these protected methods.
/// </summary>
public abstract class LedgerGrain<TState, TTransaction> : Grain
    where TState : class, ILedgerState<TTransaction>, new()
    where TTransaction : class, ILedgerTransaction
{
    protected readonly IPersistentState<TState> State;
    private readonly int _maxTransactionHistory;

    protected LedgerGrain(IPersistentState<TState> state, int maxTransactionHistory = 100)
    {
        State = state;
        _maxTransactionHistory = maxTransactionHistory;
    }

    /// <summary>
    /// Current balance of the ledger.
    /// </summary>
    protected decimal Balance => State.State.Balance;

    /// <summary>
    /// Check if this ledger has been initialized.
    /// </summary>
    protected abstract bool IsInitialized { get; }

    /// <summary>
    /// Creates a transaction record for a ledger operation.
    /// Subclasses implement this to create their specific transaction type.
    /// </summary>
    protected abstract TTransaction CreateTransaction(
        decimal amount,
        decimal balanceAfter,
        string? notes,
        object? context);

    /// <summary>
    /// Called after balance changes. Subclasses can override to update
    /// derived state (e.g., status based on balance thresholds).
    /// </summary>
    protected virtual void OnBalanceChanged(decimal previousBalance, decimal newBalance)
    {
    }

    /// <summary>
    /// Credits (adds to) the ledger balance.
    /// </summary>
    protected async Task<LedgerOperationResult> CreditAsync(
        decimal amount,
        string? notes = null,
        object? transactionContext = null)
    {
        if (amount < 0)
            throw new ArgumentException("Credit amount must be non-negative", nameof(amount));

        return await ApplyTransactionAsync(amount, notes, transactionContext);
    }

    /// <summary>
    /// Debits (subtracts from) the ledger balance.
    /// </summary>
    protected async Task<LedgerOperationResult> DebitAsync(
        decimal amount,
        string? notes = null,
        object? transactionContext = null,
        bool allowNegative = false)
    {
        if (amount < 0)
            throw new ArgumentException("Debit amount must be non-negative", nameof(amount));

        if (!allowNegative && amount > State.State.Balance)
            throw new InvalidOperationException(
                $"Insufficient balance. Available: {State.State.Balance}, Requested: {amount}");

        return await ApplyTransactionAsync(-amount, notes, transactionContext);
    }

    /// <summary>
    /// Adjusts the balance to a specific value.
    /// </summary>
    protected async Task<LedgerOperationResult> AdjustToAsync(
        decimal newBalance,
        string? notes = null,
        object? transactionContext = null)
    {
        if (newBalance < 0)
            throw new ArgumentException("Balance cannot be negative", nameof(newBalance));

        var adjustment = newBalance - State.State.Balance;
        return await ApplyTransactionAsync(adjustment, notes, transactionContext);
    }

    /// <summary>
    /// Core transaction application logic.
    /// </summary>
    private async Task<LedgerOperationResult> ApplyTransactionAsync(
        decimal amount,
        string? notes,
        object? context)
    {
        EnsureInitialized();

        var previousBalance = State.State.Balance;
        var newBalance = previousBalance + amount;
        var timestamp = DateTime.UtcNow;

        var transaction = CreateTransaction(amount, newBalance, notes, context);

        State.State.Balance = newBalance;
        State.State.Transactions.Add(transaction);
        TrimTransactionHistory();
        State.State.Version++;

        OnBalanceChanged(previousBalance, newBalance);

        await State.WriteStateAsync();

        return new LedgerOperationResult(
            transaction.Id,
            amount,
            previousBalance,
            newBalance,
            timestamp);
    }

    /// <summary>
    /// Records a transaction without affecting balance (for audit/history only).
    /// Useful for recording events like "card activated" that don't change balance.
    /// </summary>
    protected async Task<TTransaction> RecordTransactionAsync(
        decimal amount,
        string? notes = null,
        object? transactionContext = null)
    {
        EnsureInitialized();

        var transaction = CreateTransaction(amount, State.State.Balance, notes, transactionContext);

        State.State.Transactions.Add(transaction);
        TrimTransactionHistory();
        State.State.Version++;

        await State.WriteStateAsync();

        return transaction;
    }

    /// <summary>
    /// Gets recent transactions, optionally limited.
    /// </summary>
    protected IReadOnlyList<TTransaction> GetTransactions(int? limit = null)
    {
        var transactions = State.State.Transactions.AsEnumerable().Reverse();
        if (limit.HasValue)
            transactions = transactions.Take(limit.Value);
        return transactions.ToList();
    }

    /// <summary>
    /// Checks if there is sufficient balance for a debit operation.
    /// </summary>
    protected bool HasSufficientBalance(decimal amount) => State.State.Balance >= amount;

    /// <summary>
    /// Ensures the ledger has been initialized before operations.
    /// </summary>
    protected void EnsureInitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException($"{GetType().Name} has not been initialized");
    }

    /// <summary>
    /// Keeps transaction history bounded to prevent unbounded state growth.
    /// </summary>
    private void TrimTransactionHistory()
    {
        while (State.State.Transactions.Count > _maxTransactionHistory)
        {
            State.State.Transactions.RemoveAt(0);
        }
    }
}
