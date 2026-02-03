using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Standalone LedgerGrain - Composition-based balance + transaction tracking
// ============================================================================

/// <summary>
/// A generic transaction record for the standalone LedgerGrain.
/// Uses metadata dictionary instead of domain-specific fields.
/// </summary>
[GenerateSerializer]
public record LedgerTransaction
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public decimal Amount { get; init; }
    [Id(2)] public decimal BalanceAfter { get; init; }
    [Id(3)] public string TransactionType { get; init; } = string.Empty;
    [Id(4)] public DateTime Timestamp { get; init; }
    [Id(5)] public string? Notes { get; init; }
    [Id(6)] public Dictionary<string, string> Metadata { get; init; } = new();
}

/// <summary>
/// State for the standalone LedgerGrain.
/// </summary>
[GenerateSerializer]
public sealed class LedgerState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public string OwnerType { get; set; } = string.Empty;
    [Id(2)] public string OwnerId { get; set; } = string.Empty;
    [Id(3)] public decimal Balance { get; set; }
    [Id(4)] public List<LedgerTransaction> Transactions { get; set; } = [];
    [Id(5)] public int Version { get; set; }
    [Id(6)] public DateTime CreatedAt { get; set; }
    [Id(7)] public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Result of a ledger operation.
/// </summary>
[GenerateSerializer]
public record LedgerResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] Guid TransactionId,
    [property: Id(2)] decimal Amount,
    [property: Id(3)] decimal BalanceBefore,
    [property: Id(4)] decimal BalanceAfter,
    [property: Id(5)] DateTime Timestamp,
    [property: Id(6)] string? Error = null)
{
    public static LedgerResult Failure(string error) => new(false, Guid.Empty, 0, 0, 0, DateTime.UtcNow, error);
}

/// <summary>
/// Interface for the standalone LedgerGrain.
/// Key format: org:{orgId}:ledger:{ownerType}:{ownerId}
///
/// Examples:
/// - org:abc:ledger:giftcard:123
/// - org:abc:ledger:cashdrawer:456
/// - org:abc:ledger:inventory:789
///
/// This grain is used via composition by other grains that need
/// balance + transaction tracking (GiftCardGrain, CashDrawerGrain, InventoryGrain).
/// </summary>
public interface ILedgerGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the current balance.
    /// </summary>
    Task<decimal> GetBalanceAsync();

    /// <summary>
    /// Credits (adds to) the balance.
    /// </summary>
    Task<LedgerResult> CreditAsync(
        decimal amount,
        string transactionType,
        string? notes = null,
        Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Debits (subtracts from) the balance.
    /// </summary>
    Task<LedgerResult> DebitAsync(
        decimal amount,
        string transactionType,
        string? notes = null,
        Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Adjusts the balance to a specific value.
    /// </summary>
    Task<LedgerResult> AdjustToAsync(
        decimal newBalance,
        string reason,
        Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Gets recent transactions, optionally limited.
    /// </summary>
    Task<IReadOnlyList<LedgerTransaction>> GetTransactionsAsync(int? limit = null);

    /// <summary>
    /// Checks if there is sufficient balance for a debit operation.
    /// </summary>
    Task<bool> HasSufficientBalanceAsync(decimal amount);

    /// <summary>
    /// Initializes the ledger if not already initialized.
    /// Called automatically on first operation, but can be called explicitly.
    /// </summary>
    Task InitializeAsync(Guid organizationId);
}

/// <summary>
/// Standalone LedgerGrain implementation.
/// Manages balance and transaction history for any owner grain.
/// </summary>
public class LedgerGrain : Grain, ILedgerGrain
{
    private readonly IPersistentState<LedgerState> _state;
    private const int MaxTransactionHistory = 100;

    public LedgerGrain(
        [PersistentState("ledger", "OrleansStorage")]
        IPersistentState<LedgerState> state)
    {
        _state = state;
    }

    public Task<decimal> GetBalanceAsync() => Task.FromResult(_state.State.Balance);

    public Task<bool> HasSufficientBalanceAsync(decimal amount)
        => Task.FromResult(_state.State.Balance >= amount);

    public async Task InitializeAsync(Guid organizationId)
    {
        if (_state.State.Version > 0)
            return; // Already initialized

        var key = this.GetPrimaryKeyString();
        var (orgId, ownerType, ownerId) = ParseLedgerKey(key);

        _state.State = new LedgerState
        {
            OrganizationId = organizationId,
            OwnerType = ownerType,
            OwnerId = ownerId,
            Balance = 0,
            Version = 1,
            CreatedAt = DateTime.UtcNow
        };

        await _state.WriteStateAsync();
    }

    public async Task<LedgerResult> CreditAsync(
        decimal amount,
        string transactionType,
        string? notes = null,
        Dictionary<string, string>? metadata = null)
    {
        if (amount < 0)
            return LedgerResult.Failure("Credit amount must be non-negative");

        return await ApplyTransactionAsync(amount, transactionType, notes, metadata);
    }

    public async Task<LedgerResult> DebitAsync(
        decimal amount,
        string transactionType,
        string? notes = null,
        Dictionary<string, string>? metadata = null)
    {
        if (amount < 0)
            return LedgerResult.Failure("Debit amount must be non-negative");

        if (amount > _state.State.Balance)
            return LedgerResult.Failure($"Insufficient balance. Available: {_state.State.Balance}, Requested: {amount}");

        return await ApplyTransactionAsync(-amount, transactionType, notes, metadata);
    }

    public async Task<LedgerResult> AdjustToAsync(
        decimal newBalance,
        string reason,
        Dictionary<string, string>? metadata = null)
    {
        if (newBalance < 0)
            return LedgerResult.Failure("Balance cannot be negative");

        var adjustment = newBalance - _state.State.Balance;
        return await ApplyTransactionAsync(adjustment, "adjustment", reason, metadata);
    }

    public Task<IReadOnlyList<LedgerTransaction>> GetTransactionsAsync(int? limit = null)
    {
        var transactions = _state.State.Transactions.AsEnumerable().Reverse();
        if (limit.HasValue)
            transactions = transactions.Take(limit.Value);
        return Task.FromResult<IReadOnlyList<LedgerTransaction>>(transactions.ToList());
    }

    private async Task<LedgerResult> ApplyTransactionAsync(
        decimal amount,
        string transactionType,
        string? notes,
        Dictionary<string, string>? metadata)
    {
        var previousBalance = _state.State.Balance;
        var newBalance = previousBalance + amount;
        var timestamp = DateTime.UtcNow;
        var transactionId = Guid.NewGuid();

        var transaction = new LedgerTransaction
        {
            Id = transactionId,
            Amount = amount,
            BalanceAfter = newBalance,
            TransactionType = transactionType,
            Timestamp = timestamp,
            Notes = notes,
            Metadata = metadata ?? new Dictionary<string, string>()
        };

        _state.State.Balance = newBalance;
        _state.State.Transactions.Add(transaction);
        TrimTransactionHistory();
        _state.State.Version++;
        _state.State.UpdatedAt = timestamp;

        await _state.WriteStateAsync();

        return new LedgerResult(
            true,
            transactionId,
            amount,
            previousBalance,
            newBalance,
            timestamp);
    }

    private void TrimTransactionHistory()
    {
        while (_state.State.Transactions.Count > MaxTransactionHistory)
        {
            _state.State.Transactions.RemoveAt(0);
        }
    }

    private static (Guid OrgId, string OwnerType, string OwnerId) ParseLedgerKey(string key)
    {
        // Format: org:{orgId}:ledger:{ownerType}:{ownerId}
        var parts = key.Split(':');
        if (parts.Length < 5 || parts[0] != "org" || parts[2] != "ledger")
            throw new ArgumentException($"Invalid ledger key format: {key}");

        // Handle compound owner IDs (e.g., inventory:{siteId}:{ingredientId})
        var ownerId = parts.Length > 5
            ? string.Join(":", parts.Skip(4))
            : parts[4];

        return (Guid.Parse(parts[1]), parts[3], ownerId);
    }
}

// ============================================================================
// Legacy LedgerGrain base class (for backwards compatibility during migration)
// ============================================================================

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
/// [LEGACY] Abstract base class for ledger behavior via inheritance.
///
/// NOTE: New code should use ILedgerGrain composition instead.
/// This base class is retained for backwards compatibility during migration.
/// </summary>
public abstract class LedgerGrainBase<TState, TTransaction> : Grain
    where TState : class, ILedgerState<TTransaction>, new()
    where TTransaction : class, ILedgerTransaction
{
    protected readonly IPersistentState<TState> State;
    private readonly int _maxTransactionHistory;

    protected LedgerGrainBase(IPersistentState<TState> state, int maxTransactionHistory = 100)
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
