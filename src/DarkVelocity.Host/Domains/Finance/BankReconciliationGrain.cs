using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Commands
// ============================================================================

/// <summary>
/// Command to start a new reconciliation.
/// </summary>
[GenerateSerializer]
public record StartReconciliationCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid ReconciliationId,
    [property: Id(2)] string BankAccountNumber,
    [property: Id(3)] string BankAccountName,
    [property: Id(4)] DateOnly StatementDate,
    [property: Id(5)] decimal StatementEndingBalance,
    [property: Id(6)] Guid StartedBy,
    [property: Id(7)] string? StatementReference = null,
    [property: Id(8)] DateOnly? StatementStartDate = null,
    [property: Id(9)] decimal? StatementStartingBalance = null);

/// <summary>
/// Command to import bank transactions.
/// </summary>
[GenerateSerializer]
public record ImportBankTransactionsCommand(
    [property: Id(0)] IReadOnlyList<BankTransactionImport> Transactions,
    [property: Id(1)] Guid ImportedBy,
    [property: Id(2)] string? ImportSource = null);

/// <summary>
/// A bank transaction to import.
/// </summary>
[GenerateSerializer]
public record BankTransactionImport(
    [property: Id(0)] string TransactionId,
    [property: Id(1)] DateOnly TransactionDate,
    [property: Id(2)] decimal Amount,
    [property: Id(3)] string Description,
    [property: Id(4)] string? CheckNumber = null,
    [property: Id(5)] string? ReferenceNumber = null,
    [property: Id(6)] string? TransactionType = null);

/// <summary>
/// Command to match a bank transaction to a journal entry.
/// </summary>
[GenerateSerializer]
public record MatchTransactionCommand(
    [property: Id(0)] string BankTransactionId,
    [property: Id(1)] Guid JournalEntryId,
    [property: Id(2)] Guid MatchedBy,
    [property: Id(3)] string? Notes = null);

/// <summary>
/// Command to unmatch a transaction.
/// </summary>
[GenerateSerializer]
public record UnmatchTransactionCommand(
    [property: Id(0)] string BankTransactionId,
    [property: Id(1)] Guid UnmatchedBy,
    [property: Id(2)] string? Reason = null);

/// <summary>
/// Command to mark a transaction as an adjustment.
/// </summary>
[GenerateSerializer]
public record MarkAsAdjustmentCommand(
    [property: Id(0)] string BankTransactionId,
    [property: Id(1)] string AdjustmentType,
    [property: Id(2)] string Description,
    [property: Id(3)] Guid MarkedBy);

/// <summary>
/// Command to complete the reconciliation.
/// </summary>
[GenerateSerializer]
public record CompleteReconciliationCommand(
    [property: Id(0)] Guid CompletedBy,
    [property: Id(1)] string? Notes = null,
    [property: Id(2)] bool ForceComplete = false);

/// <summary>
/// Command to void a reconciliation.
/// </summary>
[GenerateSerializer]
public record VoidReconciliationCommand(
    [property: Id(0)] Guid VoidedBy,
    [property: Id(1)] string Reason);

// ============================================================================
// Results
// ============================================================================

/// <summary>
/// Status of a bank reconciliation.
/// </summary>
public enum ReconciliationStatus
{
    /// <summary>Reconciliation in progress.</summary>
    InProgress,
    /// <summary>Reconciliation completed and balanced.</summary>
    Completed,
    /// <summary>Reconciliation completed with discrepancies noted.</summary>
    CompletedWithDiscrepancies,
    /// <summary>Reconciliation voided.</summary>
    Voided
}

/// <summary>
/// Status of a bank transaction in reconciliation.
/// </summary>
public enum BankTransactionStatus
{
    /// <summary>Transaction not yet matched.</summary>
    Unmatched,
    /// <summary>Transaction matched to a journal entry.</summary>
    Matched,
    /// <summary>Transaction marked as an adjustment.</summary>
    Adjustment,
    /// <summary>Transaction flagged as a discrepancy.</summary>
    Discrepancy
}

/// <summary>
/// A bank transaction in a reconciliation.
/// </summary>
[GenerateSerializer]
public record BankTransaction(
    [property: Id(0)] string TransactionId,
    [property: Id(1)] DateOnly TransactionDate,
    [property: Id(2)] decimal Amount,
    [property: Id(3)] string Description,
    [property: Id(4)] string? CheckNumber,
    [property: Id(5)] string? ReferenceNumber,
    [property: Id(6)] string? TransactionType,
    [property: Id(7)] BankTransactionStatus Status,
    [property: Id(8)] Guid? MatchedJournalEntryId,
    [property: Id(9)] string? MatchNotes,
    [property: Id(10)] string? AdjustmentType,
    [property: Id(11)] string? AdjustmentDescription);

/// <summary>
/// Summary of a reconciliation.
/// </summary>
[GenerateSerializer]
public record ReconciliationSummary(
    [property: Id(0)] Guid ReconciliationId,
    [property: Id(1)] Guid OrganizationId,
    [property: Id(2)] string BankAccountNumber,
    [property: Id(3)] string BankAccountName,
    [property: Id(4)] DateOnly StatementDate,
    [property: Id(5)] decimal StatementEndingBalance,
    [property: Id(6)] decimal BookBalance,
    [property: Id(7)] decimal ClearedBalance,
    [property: Id(8)] decimal Difference,
    [property: Id(9)] int TotalTransactions,
    [property: Id(10)] int MatchedTransactions,
    [property: Id(11)] int UnmatchedTransactions,
    [property: Id(12)] int Adjustments,
    [property: Id(13)] ReconciliationStatus Status,
    [property: Id(14)] bool IsBalanced,
    [property: Id(15)] DateTime CreatedAt,
    [property: Id(16)] DateTime? CompletedAt);

/// <summary>
/// Snapshot of a full reconciliation.
/// </summary>
[GenerateSerializer]
public record ReconciliationSnapshot(
    [property: Id(0)] ReconciliationSummary Summary,
    [property: Id(1)] IReadOnlyList<BankTransaction> Transactions,
    [property: Id(2)] IReadOnlyList<OutstandingItem> OutstandingDeposits,
    [property: Id(3)] IReadOnlyList<OutstandingItem> OutstandingChecks);

/// <summary>
/// An outstanding item (deposit or check) not yet cleared.
/// </summary>
[GenerateSerializer]
public record OutstandingItem(
    [property: Id(0)] Guid JournalEntryId,
    [property: Id(1)] DateOnly Date,
    [property: Id(2)] decimal Amount,
    [property: Id(3)] string Description,
    [property: Id(4)] string? ReferenceNumber);

// ============================================================================
// Events
// ============================================================================

/// <summary>
/// Base interface for reconciliation events.
/// </summary>
public interface IBankReconciliationEvent
{
    Guid ReconciliationId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record ReconciliationStarted : IBankReconciliationEvent
{
    [Id(0)] public Guid ReconciliationId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public string BankAccountNumber { get; init; } = "";
    [Id(3)] public string BankAccountName { get; init; } = "";
    [Id(4)] public DateOnly StatementDate { get; init; }
    [Id(5)] public decimal StatementEndingBalance { get; init; }
    [Id(6)] public Guid StartedBy { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BankTransactionsImported : IBankReconciliationEvent
{
    [Id(0)] public Guid ReconciliationId { get; init; }
    [Id(1)] public int TransactionCount { get; init; }
    [Id(2)] public Guid ImportedBy { get; init; }
    [Id(3)] public string? ImportSource { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record TransactionMatched : IBankReconciliationEvent
{
    [Id(0)] public Guid ReconciliationId { get; init; }
    [Id(1)] public string BankTransactionId { get; init; } = "";
    [Id(2)] public Guid JournalEntryId { get; init; }
    [Id(3)] public Guid MatchedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record TransactionUnmatched : IBankReconciliationEvent
{
    [Id(0)] public Guid ReconciliationId { get; init; }
    [Id(1)] public string BankTransactionId { get; init; } = "";
    [Id(2)] public Guid UnmatchedBy { get; init; }
    [Id(3)] public string? Reason { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ReconciliationCompleted : IBankReconciliationEvent
{
    [Id(0)] public Guid ReconciliationId { get; init; }
    [Id(1)] public bool IsBalanced { get; init; }
    [Id(2)] public decimal Difference { get; init; }
    [Id(3)] public Guid CompletedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ReconciliationVoided : IBankReconciliationEvent
{
    [Id(0)] public Guid ReconciliationId { get; init; }
    [Id(1)] public Guid VoidedBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

// ============================================================================
// State
// ============================================================================

/// <summary>
/// State for the Bank Reconciliation grain.
/// </summary>
[GenerateSerializer]
public sealed class BankReconciliationState
{
    [Id(0)] public Guid ReconciliationId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public string BankAccountNumber { get; set; } = "";
    [Id(3)] public string BankAccountName { get; set; } = "";
    [Id(4)] public DateOnly StatementDate { get; set; }
    [Id(5)] public DateOnly? StatementStartDate { get; set; }
    [Id(6)] public decimal StatementEndingBalance { get; set; }
    [Id(7)] public decimal? StatementStartingBalance { get; set; }
    [Id(8)] public string? StatementReference { get; set; }
    [Id(9)] public ReconciliationStatus Status { get; set; } = ReconciliationStatus.InProgress;
    [Id(10)] public Dictionary<string, BankTransactionState> Transactions { get; set; } = [];
    [Id(11)] public decimal BookBalance { get; set; }
    [Id(12)] public DateTime CreatedAt { get; set; }
    [Id(13)] public Guid CreatedBy { get; set; }
    [Id(14)] public DateTime? CompletedAt { get; set; }
    [Id(15)] public Guid? CompletedBy { get; set; }
    [Id(16)] public string? VoidReason { get; set; }
    [Id(17)] public string? Notes { get; set; }
}

/// <summary>
/// State for a bank transaction within reconciliation.
/// </summary>
[GenerateSerializer]
public sealed class BankTransactionState
{
    [Id(0)] public string TransactionId { get; set; } = "";
    [Id(1)] public DateOnly TransactionDate { get; set; }
    [Id(2)] public decimal Amount { get; set; }
    [Id(3)] public string Description { get; set; } = "";
    [Id(4)] public string? CheckNumber { get; set; }
    [Id(5)] public string? ReferenceNumber { get; set; }
    [Id(6)] public string? TransactionType { get; set; }
    [Id(7)] public BankTransactionStatus Status { get; set; } = BankTransactionStatus.Unmatched;
    [Id(8)] public Guid? MatchedJournalEntryId { get; set; }
    [Id(9)] public Guid? MatchedBy { get; set; }
    [Id(10)] public DateTime? MatchedAt { get; set; }
    [Id(11)] public string? MatchNotes { get; set; }
    [Id(12)] public string? AdjustmentType { get; set; }
    [Id(13)] public string? AdjustmentDescription { get; set; }
}

// ============================================================================
// Grain Interface
// ============================================================================

/// <summary>
/// Grain interface for bank reconciliation.
/// Key format: {orgId}:reconciliation:{reconciliationId}
/// </summary>
public interface IBankReconciliationGrain : IGrainWithStringKey
{
    /// <summary>
    /// Starts a new bank reconciliation.
    /// </summary>
    Task<ReconciliationSummary> StartAsync(StartReconciliationCommand command);

    /// <summary>
    /// Imports bank transactions from a statement.
    /// </summary>
    Task<ReconciliationSummary> ImportTransactionsAsync(ImportBankTransactionsCommand command);

    /// <summary>
    /// Matches a bank transaction to a journal entry.
    /// </summary>
    Task MatchTransactionAsync(MatchTransactionCommand command);

    /// <summary>
    /// Unmatches a previously matched transaction.
    /// </summary>
    Task UnmatchTransactionAsync(UnmatchTransactionCommand command);

    /// <summary>
    /// Marks a transaction as an adjustment (bank fee, interest, etc.).
    /// </summary>
    Task MarkAsAdjustmentAsync(MarkAsAdjustmentCommand command);

    /// <summary>
    /// Completes the reconciliation.
    /// </summary>
    Task<ReconciliationSummary> CompleteAsync(CompleteReconciliationCommand command);

    /// <summary>
    /// Voids the reconciliation.
    /// </summary>
    Task VoidAsync(VoidReconciliationCommand command);

    /// <summary>
    /// Gets the reconciliation summary.
    /// </summary>
    Task<ReconciliationSummary> GetSummaryAsync();

    /// <summary>
    /// Gets the full reconciliation snapshot.
    /// </summary>
    Task<ReconciliationSnapshot> GetSnapshotAsync();

    /// <summary>
    /// Gets unmatched transactions.
    /// </summary>
    Task<IReadOnlyList<BankTransaction>> GetUnmatchedTransactionsAsync();

    /// <summary>
    /// Suggests matches for a bank transaction.
    /// </summary>
    Task<IReadOnlyList<JournalEntryMatchSuggestion>> SuggestMatchesAsync(string bankTransactionId);

    /// <summary>
    /// Checks if the reconciliation exists.
    /// </summary>
    Task<bool> ExistsAsync();
}

/// <summary>
/// A suggested match for a bank transaction.
/// </summary>
[GenerateSerializer]
public record JournalEntryMatchSuggestion(
    [property: Id(0)] Guid JournalEntryId,
    [property: Id(1)] string EntryNumber,
    [property: Id(2)] DateOnly PostingDate,
    [property: Id(3)] decimal Amount,
    [property: Id(4)] string Description,
    [property: Id(5)] int ConfidenceScore,
    [property: Id(6)] string MatchReason);

// ============================================================================
// Grain Implementation
// ============================================================================

/// <summary>
/// Orleans grain for bank reconciliation.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class BankReconciliationGrain : JournaledGrain<BankReconciliationState, IBankReconciliationEvent>, IBankReconciliationGrain
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<BankReconciliationGrain> _logger;

    public BankReconciliationGrain(
        IGrainFactory grainFactory,
        ILogger<BankReconciliationGrain> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    protected override void TransitionState(BankReconciliationState state, IBankReconciliationEvent @event)
    {
        switch (@event)
        {
            case ReconciliationStarted e:
                state.ReconciliationId = e.ReconciliationId;
                state.OrganizationId = e.OrganizationId;
                state.BankAccountNumber = e.BankAccountNumber;
                state.BankAccountName = e.BankAccountName;
                state.StatementDate = e.StatementDate;
                state.StatementEndingBalance = e.StatementEndingBalance;
                state.Status = ReconciliationStatus.InProgress;
                state.CreatedAt = e.OccurredAt;
                state.CreatedBy = e.StartedBy;
                break;

            case BankTransactionsImported:
                // Transactions are added separately in the command handler
                break;

            case TransactionMatched e:
                if (state.Transactions.TryGetValue(e.BankTransactionId, out var txn))
                {
                    txn.Status = BankTransactionStatus.Matched;
                    txn.MatchedJournalEntryId = e.JournalEntryId;
                    txn.MatchedBy = e.MatchedBy;
                    txn.MatchedAt = e.OccurredAt;
                }
                break;

            case TransactionUnmatched e:
                if (state.Transactions.TryGetValue(e.BankTransactionId, out var unmatchTxn))
                {
                    unmatchTxn.Status = BankTransactionStatus.Unmatched;
                    unmatchTxn.MatchedJournalEntryId = null;
                    unmatchTxn.MatchedBy = null;
                    unmatchTxn.MatchedAt = null;
                    unmatchTxn.MatchNotes = null;
                }
                break;

            case ReconciliationCompleted e:
                state.Status = e.IsBalanced
                    ? ReconciliationStatus.Completed
                    : ReconciliationStatus.CompletedWithDiscrepancies;
                state.CompletedAt = e.OccurredAt;
                state.CompletedBy = e.CompletedBy;
                break;

            case ReconciliationVoided e:
                state.Status = ReconciliationStatus.Voided;
                state.VoidReason = e.Reason;
                break;
        }
    }

    public async Task<ReconciliationSummary> StartAsync(StartReconciliationCommand command)
    {
        if (State.ReconciliationId != Guid.Empty)
            throw new InvalidOperationException("Reconciliation already exists");

        var now = DateTime.UtcNow;

        RaiseEvent(new ReconciliationStarted
        {
            ReconciliationId = command.ReconciliationId,
            OrganizationId = command.OrganizationId,
            BankAccountNumber = command.BankAccountNumber,
            BankAccountName = command.BankAccountName,
            StatementDate = command.StatementDate,
            StatementEndingBalance = command.StatementEndingBalance,
            StartedBy = command.StartedBy,
            OccurredAt = now
        });

        State.StatementReference = command.StatementReference;
        State.StatementStartDate = command.StatementStartDate;
        State.StatementStartingBalance = command.StatementStartingBalance;

        await ConfirmEvents();

        _logger.LogInformation(
            "Bank reconciliation started for account {Account} as of {Date}",
            command.BankAccountNumber,
            command.StatementDate);

        return ToSummary();
    }

    public async Task<ReconciliationSummary> ImportTransactionsAsync(ImportBankTransactionsCommand command)
    {
        EnsureExists();
        EnsureInProgress();

        var now = DateTime.UtcNow;

        foreach (var txn in command.Transactions)
        {
            if (State.Transactions.ContainsKey(txn.TransactionId))
                continue; // Skip duplicates

            State.Transactions[txn.TransactionId] = new BankTransactionState
            {
                TransactionId = txn.TransactionId,
                TransactionDate = txn.TransactionDate,
                Amount = txn.Amount,
                Description = txn.Description,
                CheckNumber = txn.CheckNumber,
                ReferenceNumber = txn.ReferenceNumber,
                TransactionType = txn.TransactionType
            };
        }

        RaiseEvent(new BankTransactionsImported
        {
            ReconciliationId = State.ReconciliationId,
            TransactionCount = command.Transactions.Count,
            ImportedBy = command.ImportedBy,
            ImportSource = command.ImportSource,
            OccurredAt = now
        });

        await ConfirmEvents();

        _logger.LogInformation(
            "Imported {Count} bank transactions for reconciliation {ReconciliationId}",
            command.Transactions.Count,
            State.ReconciliationId);

        return ToSummary();
    }

    public async Task MatchTransactionAsync(MatchTransactionCommand command)
    {
        EnsureExists();
        EnsureInProgress();

        if (!State.Transactions.TryGetValue(command.BankTransactionId, out var txn))
            throw new InvalidOperationException($"Transaction {command.BankTransactionId} not found");

        if (txn.Status == BankTransactionStatus.Matched)
            throw new InvalidOperationException($"Transaction {command.BankTransactionId} is already matched");

        var now = DateTime.UtcNow;

        RaiseEvent(new TransactionMatched
        {
            ReconciliationId = State.ReconciliationId,
            BankTransactionId = command.BankTransactionId,
            JournalEntryId = command.JournalEntryId,
            MatchedBy = command.MatchedBy,
            OccurredAt = now
        });

        txn.MatchNotes = command.Notes;

        await ConfirmEvents();

        _logger.LogInformation(
            "Matched bank transaction {TransactionId} to journal entry {JournalEntryId}",
            command.BankTransactionId,
            command.JournalEntryId);
    }

    public async Task UnmatchTransactionAsync(UnmatchTransactionCommand command)
    {
        EnsureExists();
        EnsureInProgress();

        if (!State.Transactions.TryGetValue(command.BankTransactionId, out var txn))
            throw new InvalidOperationException($"Transaction {command.BankTransactionId} not found");

        if (txn.Status != BankTransactionStatus.Matched)
            throw new InvalidOperationException($"Transaction {command.BankTransactionId} is not matched");

        var now = DateTime.UtcNow;

        RaiseEvent(new TransactionUnmatched
        {
            ReconciliationId = State.ReconciliationId,
            BankTransactionId = command.BankTransactionId,
            UnmatchedBy = command.UnmatchedBy,
            Reason = command.Reason,
            OccurredAt = now
        });

        await ConfirmEvents();

        _logger.LogInformation(
            "Unmatched bank transaction {TransactionId}. Reason: {Reason}",
            command.BankTransactionId,
            command.Reason ?? "Not specified");
    }

    public async Task MarkAsAdjustmentAsync(MarkAsAdjustmentCommand command)
    {
        EnsureExists();
        EnsureInProgress();

        if (!State.Transactions.TryGetValue(command.BankTransactionId, out var txn))
            throw new InvalidOperationException($"Transaction {command.BankTransactionId} not found");

        txn.Status = BankTransactionStatus.Adjustment;
        txn.AdjustmentType = command.AdjustmentType;
        txn.AdjustmentDescription = command.Description;

        await ConfirmEvents();

        _logger.LogInformation(
            "Marked bank transaction {TransactionId} as adjustment: {Type}",
            command.BankTransactionId,
            command.AdjustmentType);
    }

    public async Task<ReconciliationSummary> CompleteAsync(CompleteReconciliationCommand command)
    {
        EnsureExists();
        EnsureInProgress();

        var summary = ToSummary();

        if (!summary.IsBalanced && !command.ForceComplete)
            throw new InvalidOperationException(
                $"Reconciliation is not balanced. Difference: {summary.Difference:C}. Use ForceComplete to complete anyway.");

        var now = DateTime.UtcNow;

        RaiseEvent(new ReconciliationCompleted
        {
            ReconciliationId = State.ReconciliationId,
            IsBalanced = summary.IsBalanced,
            Difference = summary.Difference,
            CompletedBy = command.CompletedBy,
            OccurredAt = now
        });

        State.Notes = command.Notes;

        await ConfirmEvents();

        _logger.LogInformation(
            "Bank reconciliation {ReconciliationId} completed. Balanced: {IsBalanced}",
            State.ReconciliationId,
            summary.IsBalanced);

        return ToSummary();
    }

    public async Task VoidAsync(VoidReconciliationCommand command)
    {
        EnsureExists();

        if (State.Status == ReconciliationStatus.Voided)
            throw new InvalidOperationException("Reconciliation already voided");

        var now = DateTime.UtcNow;

        RaiseEvent(new ReconciliationVoided
        {
            ReconciliationId = State.ReconciliationId,
            VoidedBy = command.VoidedBy,
            Reason = command.Reason,
            OccurredAt = now
        });

        await ConfirmEvents();

        _logger.LogInformation(
            "Bank reconciliation {ReconciliationId} voided. Reason: {Reason}",
            State.ReconciliationId,
            command.Reason);
    }

    public Task<ReconciliationSummary> GetSummaryAsync()
    {
        return Task.FromResult(ToSummary());
    }

    public Task<ReconciliationSnapshot> GetSnapshotAsync()
    {
        var transactions = State.Transactions.Values
            .Select(ToTransaction)
            .OrderBy(t => t.TransactionDate)
            .ToList();

        // Outstanding items would be calculated from uncleared journal entries
        var outstandingDeposits = new List<OutstandingItem>();
        var outstandingChecks = new List<OutstandingItem>();

        return Task.FromResult(new ReconciliationSnapshot(
            ToSummary(),
            transactions,
            outstandingDeposits,
            outstandingChecks));
    }

    public Task<IReadOnlyList<BankTransaction>> GetUnmatchedTransactionsAsync()
    {
        var unmatched = State.Transactions.Values
            .Where(t => t.Status == BankTransactionStatus.Unmatched)
            .Select(ToTransaction)
            .OrderBy(t => t.TransactionDate)
            .ToList();

        return Task.FromResult<IReadOnlyList<BankTransaction>>(unmatched);
    }

    public Task<IReadOnlyList<JournalEntryMatchSuggestion>> SuggestMatchesAsync(string bankTransactionId)
    {
        // In a real implementation, this would:
        // 1. Get the bank transaction details
        // 2. Query journal entries with similar amounts and dates
        // 3. Score and rank potential matches

        var suggestions = new List<JournalEntryMatchSuggestion>();

        // Placeholder - would query journal entries based on amount, date, description
        return Task.FromResult<IReadOnlyList<JournalEntryMatchSuggestion>>(suggestions);
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(State.ReconciliationId != Guid.Empty);
    }

    private void EnsureExists()
    {
        if (State.ReconciliationId == Guid.Empty)
            throw new InvalidOperationException("Reconciliation not found");
    }

    private void EnsureInProgress()
    {
        if (State.Status != ReconciliationStatus.InProgress)
            throw new InvalidOperationException($"Reconciliation is {State.Status}, not in progress");
    }

    private ReconciliationSummary ToSummary()
    {
        var transactions = State.Transactions.Values.ToList();
        var matchedTransactions = transactions.Count(t => t.Status == BankTransactionStatus.Matched);
        var unmatchedTransactions = transactions.Count(t => t.Status == BankTransactionStatus.Unmatched);
        var adjustments = transactions.Count(t => t.Status == BankTransactionStatus.Adjustment);

        var clearedBalance = transactions
            .Where(t => t.Status == BankTransactionStatus.Matched || t.Status == BankTransactionStatus.Adjustment)
            .Sum(t => t.Amount);

        var difference = State.StatementEndingBalance - (State.BookBalance + clearedBalance);
        var isBalanced = Math.Abs(difference) < 0.01m;

        return new ReconciliationSummary(
            State.ReconciliationId,
            State.OrganizationId,
            State.BankAccountNumber,
            State.BankAccountName,
            State.StatementDate,
            State.StatementEndingBalance,
            State.BookBalance,
            clearedBalance,
            difference,
            transactions.Count,
            matchedTransactions,
            unmatchedTransactions,
            adjustments,
            State.Status,
            isBalanced,
            State.CreatedAt,
            State.CompletedAt);
    }

    private static BankTransaction ToTransaction(BankTransactionState state)
    {
        return new BankTransaction(
            state.TransactionId,
            state.TransactionDate,
            state.Amount,
            state.Description,
            state.CheckNumber,
            state.ReferenceNumber,
            state.TransactionType,
            state.Status,
            state.MatchedJournalEntryId,
            state.MatchNotes,
            state.AdjustmentType,
            state.AdjustmentDescription);
    }
}
