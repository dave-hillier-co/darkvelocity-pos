using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Commands
// ============================================================================

/// <summary>
/// Command to create a journal entry.
/// </summary>
[GenerateSerializer]
public record CreateJournalEntryCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid JournalEntryId,
    [property: Id(2)] DateOnly PostingDate,
    [property: Id(3)] IReadOnlyList<JournalEntryLineCommand> Lines,
    [property: Id(4)] Guid CreatedBy,
    [property: Id(5)] string? Memo = null,
    [property: Id(6)] DateOnly? EffectiveDate = null,
    [property: Id(7)] string? ReferenceNumber = null,
    [property: Id(8)] string? ReferenceType = null,
    [property: Id(9)] Guid? ReferenceId = null,
    [property: Id(10)] bool AutoPost = false,
    [property: Id(11)] bool IsReversing = false,
    [property: Id(12)] DateOnly? ReversalDate = null);

/// <summary>
/// A line in a journal entry command.
/// </summary>
[GenerateSerializer]
public record JournalEntryLineCommand(
    [property: Id(0)] string AccountNumber,
    [property: Id(1)] decimal DebitAmount,
    [property: Id(2)] decimal CreditAmount,
    [property: Id(3)] string? Description = null,
    [property: Id(4)] Guid? CostCenterId = null,
    [property: Id(5)] string? TaxCode = null);

/// <summary>
/// Command to post a journal entry.
/// </summary>
[GenerateSerializer]
public record PostJournalEntryCommand(
    [property: Id(0)] Guid PostedBy,
    [property: Id(1)] string? Notes = null);

/// <summary>
/// Command to approve a journal entry.
/// </summary>
[GenerateSerializer]
public record ApproveJournalEntryCommand(
    [property: Id(0)] Guid ApprovedBy,
    [property: Id(1)] string? Notes = null);

/// <summary>
/// Command to reject a journal entry.
/// </summary>
[GenerateSerializer]
public record RejectJournalEntryCommand(
    [property: Id(0)] Guid RejectedBy,
    [property: Id(1)] string Reason);

/// <summary>
/// Command to reverse a journal entry.
/// </summary>
[GenerateSerializer]
public record ReverseJournalEntryCommand(
    [property: Id(0)] Guid ReversedBy,
    [property: Id(1)] DateOnly ReversalDate,
    [property: Id(2)] string? Reason = null);

/// <summary>
/// Command to void a journal entry.
/// </summary>
[GenerateSerializer]
public record VoidJournalEntryCommand(
    [property: Id(0)] Guid VoidedBy,
    [property: Id(1)] string Reason);

// ============================================================================
// Results
// ============================================================================

/// <summary>
/// Status of a journal entry.
/// </summary>
public enum JournalEntryEntryStatus
{
    /// <summary>Entry created but not yet posted.</summary>
    Draft,
    /// <summary>Entry pending approval.</summary>
    PendingApproval,
    /// <summary>Entry approved, ready to post.</summary>
    Approved,
    /// <summary>Entry posted to the ledger.</summary>
    Posted,
    /// <summary>Entry rejected.</summary>
    Rejected,
    /// <summary>Entry voided.</summary>
    Voided,
    /// <summary>Entry reversed.</summary>
    Reversed
}

/// <summary>
/// A line in a journal entry.
/// </summary>
[GenerateSerializer]
public record JournalEntryLine(
    [property: Id(0)] int LineNumber,
    [property: Id(1)] string AccountNumber,
    [property: Id(2)] string? AccountName,
    [property: Id(3)] decimal DebitAmount,
    [property: Id(4)] decimal CreditAmount,
    [property: Id(5)] string? Description,
    [property: Id(6)] Guid? CostCenterId,
    [property: Id(7)] string? TaxCode);

/// <summary>
/// Snapshot of a journal entry.
/// </summary>
[GenerateSerializer]
public record JournalEntrySnapshot(
    [property: Id(0)] Guid JournalEntryId,
    [property: Id(1)] Guid OrganizationId,
    [property: Id(2)] string EntryNumber,
    [property: Id(3)] DateOnly PostingDate,
    [property: Id(4)] DateOnly EffectiveDate,
    [property: Id(5)] string? Memo,
    [property: Id(6)] IReadOnlyList<JournalEntryLine> Lines,
    [property: Id(7)] decimal TotalDebits,
    [property: Id(8)] decimal TotalCredits,
    [property: Id(9)] JournalEntryEntryStatus Status,
    [property: Id(10)] string? ReferenceNumber,
    [property: Id(11)] string? ReferenceType,
    [property: Id(12)] Guid? ReferenceId,
    [property: Id(13)] bool IsReversing,
    [property: Id(14)] Guid? ReversalEntryId,
    [property: Id(15)] Guid? ReversedFromEntryId,
    [property: Id(16)] DateTime CreatedAt,
    [property: Id(17)] Guid CreatedBy,
    [property: Id(18)] DateTime? PostedAt,
    [property: Id(19)] Guid? PostedBy,
    [property: Id(20)] DateTime? ApprovedAt,
    [property: Id(21)] Guid? ApprovedBy);

// ============================================================================
// Events
// ============================================================================

/// <summary>
/// Base interface for journal entry events.
/// </summary>
public interface IJournalEntryEvent
{
    Guid JournalEntryId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record JournalEntryCreated : IJournalEntryEvent
{
    [Id(0)] public Guid JournalEntryId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public string EntryNumber { get; init; } = "";
    [Id(3)] public DateOnly PostingDate { get; init; }
    [Id(4)] public DateOnly EffectiveDate { get; init; }
    [Id(5)] public string? Memo { get; init; }
    [Id(6)] public decimal TotalDebits { get; init; }
    [Id(7)] public decimal TotalCredits { get; init; }
    [Id(8)] public string? ReferenceNumber { get; init; }
    [Id(9)] public string? ReferenceType { get; init; }
    [Id(10)] public Guid? ReferenceId { get; init; }
    [Id(11)] public bool IsReversing { get; init; }
    [Id(12)] public Guid CreatedBy { get; init; }
    [Id(13)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record JournalEntryPosted : IJournalEntryEvent
{
    [Id(0)] public Guid JournalEntryId { get; init; }
    [Id(1)] public Guid PostedBy { get; init; }
    [Id(2)] public string? Notes { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record JournalEntryApproved : IJournalEntryEvent
{
    [Id(0)] public Guid JournalEntryId { get; init; }
    [Id(1)] public Guid ApprovedBy { get; init; }
    [Id(2)] public string? Notes { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record JournalEntryRejected : IJournalEntryEvent
{
    [Id(0)] public Guid JournalEntryId { get; init; }
    [Id(1)] public Guid RejectedBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record JournalEntryReversed : IJournalEntryEvent
{
    [Id(0)] public Guid JournalEntryId { get; init; }
    [Id(1)] public Guid ReversalEntryId { get; init; }
    [Id(2)] public Guid ReversedBy { get; init; }
    [Id(3)] public string? Reason { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record JournalEntryVoided : IJournalEntryEvent
{
    [Id(0)] public Guid JournalEntryId { get; init; }
    [Id(1)] public Guid VoidedBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

// ============================================================================
// State
// ============================================================================

/// <summary>
/// State for the Journal Entry grain.
/// </summary>
[GenerateSerializer]
public sealed class JournalEntryState
{
    [Id(0)] public Guid JournalEntryId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public string EntryNumber { get; set; } = "";
    [Id(3)] public DateOnly PostingDate { get; set; }
    [Id(4)] public DateOnly EffectiveDate { get; set; }
    [Id(5)] public string? Memo { get; set; }
    [Id(6)] public List<JournalEntryLineState> Lines { get; set; } = [];
    [Id(7)] public decimal TotalDebits { get; set; }
    [Id(8)] public decimal TotalCredits { get; set; }
    [Id(9)] public JournalEntryEntryStatus Status { get; set; } = JournalEntryEntryStatus.Draft;
    [Id(10)] public string? ReferenceNumber { get; set; }
    [Id(11)] public string? ReferenceType { get; set; }
    [Id(12)] public Guid? ReferenceId { get; set; }
    [Id(13)] public bool IsReversing { get; set; }
    [Id(14)] public DateOnly? ReversalDate { get; set; }
    [Id(15)] public Guid? ReversalEntryId { get; set; }
    [Id(16)] public Guid? ReversedFromEntryId { get; set; }
    [Id(17)] public DateTime CreatedAt { get; set; }
    [Id(18)] public Guid CreatedBy { get; set; }
    [Id(19)] public DateTime? PostedAt { get; set; }
    [Id(20)] public Guid? PostedBy { get; set; }
    [Id(21)] public DateTime? ApprovedAt { get; set; }
    [Id(22)] public Guid? ApprovedBy { get; set; }
    [Id(23)] public string? RejectionReason { get; set; }
    [Id(24)] public string? VoidReason { get; set; }
}

/// <summary>
/// State for a journal entry line.
/// </summary>
[GenerateSerializer]
public sealed class JournalEntryLineState
{
    [Id(0)] public int LineNumber { get; set; }
    [Id(1)] public string AccountNumber { get; set; } = "";
    [Id(2)] public string? AccountName { get; set; }
    [Id(3)] public decimal DebitAmount { get; set; }
    [Id(4)] public decimal CreditAmount { get; set; }
    [Id(5)] public string? Description { get; set; }
    [Id(6)] public Guid? CostCenterId { get; set; }
    [Id(7)] public string? TaxCode { get; set; }
}

// ============================================================================
// Grain Interface
// ============================================================================

/// <summary>
/// Grain interface for managing journal entries.
/// Implements double-entry bookkeeping with approval workflow.
/// Key format: {orgId}:journalentry:{entryId}
/// </summary>
public interface IJournalEntryGrain : IGrainWithStringKey
{
    /// <summary>
    /// Creates a new journal entry.
    /// </summary>
    Task<JournalEntrySnapshot> CreateAsync(CreateJournalEntryCommand command);

    /// <summary>
    /// Posts the journal entry to the ledger.
    /// </summary>
    Task<JournalEntrySnapshot> PostAsync(PostJournalEntryCommand command);

    /// <summary>
    /// Approves the journal entry (for entries requiring approval).
    /// </summary>
    Task<JournalEntrySnapshot> ApproveAsync(ApproveJournalEntryCommand command);

    /// <summary>
    /// Rejects the journal entry.
    /// </summary>
    Task<JournalEntrySnapshot> RejectAsync(RejectJournalEntryCommand command);

    /// <summary>
    /// Reverses the journal entry, creating a new reversing entry.
    /// </summary>
    Task<JournalEntrySnapshot> ReverseAsync(ReverseJournalEntryCommand command);

    /// <summary>
    /// Voids the journal entry (only for unposted entries).
    /// </summary>
    Task VoidAsync(VoidJournalEntryCommand command);

    /// <summary>
    /// Gets the current snapshot.
    /// </summary>
    Task<JournalEntrySnapshot> GetSnapshotAsync();

    /// <summary>
    /// Checks if the journal entry exists.
    /// </summary>
    Task<bool> ExistsAsync();

    /// <summary>
    /// Gets the journal entry status.
    /// </summary>
    Task<JournalEntryEntryStatus> GetStatusAsync();
}

// ============================================================================
// Grain Implementation
// ============================================================================

/// <summary>
/// Orleans grain for managing journal entries with double-entry bookkeeping.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class JournalEntryGrain : JournaledGrain<JournalEntryState, IJournalEntryEvent>, IJournalEntryGrain
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<JournalEntryGrain> _logger;
    private Lazy<IAsyncStream<DomainEvent>>? _eventStream;

    public JournalEntryGrain(
        IGrainFactory grainFactory,
        ILogger<JournalEntryGrain> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _eventStream = new Lazy<IAsyncStream<DomainEvent>>(() =>
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            return streamProvider.GetStream<DomainEvent>(
                StreamConstants.AccountingStreamNamespace,
                State.OrganizationId.ToString());
        });

        return base.OnActivateAsync(cancellationToken);
    }

    protected override void TransitionState(JournalEntryState state, IJournalEntryEvent @event)
    {
        switch (@event)
        {
            case JournalEntryCreated e:
                state.JournalEntryId = e.JournalEntryId;
                state.OrganizationId = e.OrganizationId;
                state.EntryNumber = e.EntryNumber;
                state.PostingDate = e.PostingDate;
                state.EffectiveDate = e.EffectiveDate;
                state.Memo = e.Memo;
                state.TotalDebits = e.TotalDebits;
                state.TotalCredits = e.TotalCredits;
                state.ReferenceNumber = e.ReferenceNumber;
                state.ReferenceType = e.ReferenceType;
                state.ReferenceId = e.ReferenceId;
                state.IsReversing = e.IsReversing;
                state.Status = JournalEntryEntryStatus.Draft;
                state.CreatedAt = e.OccurredAt;
                state.CreatedBy = e.CreatedBy;
                break;

            case JournalEntryPosted e:
                state.Status = JournalEntryEntryStatus.Posted;
                state.PostedAt = e.OccurredAt;
                state.PostedBy = e.PostedBy;
                break;

            case JournalEntryApproved e:
                state.Status = JournalEntryEntryStatus.Approved;
                state.ApprovedAt = e.OccurredAt;
                state.ApprovedBy = e.ApprovedBy;
                break;

            case JournalEntryRejected e:
                state.Status = JournalEntryEntryStatus.Rejected;
                state.RejectionReason = e.Reason;
                break;

            case JournalEntryReversed e:
                state.Status = JournalEntryEntryStatus.Reversed;
                state.ReversalEntryId = e.ReversalEntryId;
                break;

            case JournalEntryVoided e:
                state.Status = JournalEntryEntryStatus.Voided;
                state.VoidReason = e.Reason;
                break;
        }
    }

    public async Task<JournalEntrySnapshot> CreateAsync(CreateJournalEntryCommand command)
    {
        if (State.JournalEntryId != Guid.Empty)
            throw new InvalidOperationException("Journal entry already exists");

        // Validate double-entry bookkeeping rule
        var totalDebits = command.Lines.Sum(l => l.DebitAmount);
        var totalCredits = command.Lines.Sum(l => l.CreditAmount);

        if (totalDebits != totalCredits)
            throw new InvalidOperationException(
                $"Debits ({totalDebits:C}) must equal Credits ({totalCredits:C})");

        if (totalDebits == 0)
            throw new InvalidOperationException("Journal entry must have non-zero amounts");

        // Validate each line has either debit or credit (not both or neither)
        foreach (var line in command.Lines)
        {
            if (line.DebitAmount > 0 && line.CreditAmount > 0)
                throw new InvalidOperationException(
                    $"Line for account {line.AccountNumber} cannot have both debit and credit amounts");

            if (line.DebitAmount == 0 && line.CreditAmount == 0)
                throw new InvalidOperationException(
                    $"Line for account {line.AccountNumber} must have either debit or credit amount");

            if (line.DebitAmount < 0 || line.CreditAmount < 0)
                throw new InvalidOperationException("Amounts cannot be negative");
        }

        // Validate account numbers exist
        var chartGrain = _grainFactory.GetGrain<IChartOfAccountsGrain>(
            $"{command.OrganizationId}:chartofaccounts");

        foreach (var line in command.Lines)
        {
            var isValid = await chartGrain.ValidateAccountAsync(line.AccountNumber);
            if (!isValid)
                throw new InvalidOperationException($"Account {line.AccountNumber} not found or inactive");
        }

        var now = DateTime.UtcNow;
        var entryNumber = GenerateEntryNumber(command.PostingDate);
        var effectiveDate = command.EffectiveDate ?? command.PostingDate;

        RaiseEvent(new JournalEntryCreated
        {
            JournalEntryId = command.JournalEntryId,
            OrganizationId = command.OrganizationId,
            EntryNumber = entryNumber,
            PostingDate = command.PostingDate,
            EffectiveDate = effectiveDate,
            Memo = command.Memo,
            TotalDebits = totalDebits,
            TotalCredits = totalCredits,
            ReferenceNumber = command.ReferenceNumber,
            ReferenceType = command.ReferenceType,
            ReferenceId = command.ReferenceId,
            IsReversing = command.IsReversing,
            CreatedBy = command.CreatedBy,
            OccurredAt = now
        });

        // Store lines (not in event for simplicity)
        var lineNumber = 1;
        foreach (var line in command.Lines)
        {
            var account = await chartGrain.GetAccountAsync(line.AccountNumber);
            State.Lines.Add(new JournalEntryLineState
            {
                LineNumber = lineNumber++,
                AccountNumber = line.AccountNumber,
                AccountName = account?.Name,
                DebitAmount = line.DebitAmount,
                CreditAmount = line.CreditAmount,
                Description = line.Description,
                CostCenterId = line.CostCenterId,
                TaxCode = line.TaxCode
            });
        }

        if (command.IsReversing && command.ReversalDate.HasValue)
        {
            State.ReversalDate = command.ReversalDate;
        }

        await ConfirmEvents();

        _logger.LogInformation(
            "Journal entry {EntryNumber} created with {LineCount} lines, Total: {Total:C}",
            entryNumber,
            command.Lines.Count,
            totalDebits);

        // Auto-post if requested
        if (command.AutoPost)
        {
            return await PostAsync(new PostJournalEntryCommand(command.CreatedBy));
        }

        return ToSnapshot();
    }

    public async Task<JournalEntrySnapshot> PostAsync(PostJournalEntryCommand command)
    {
        EnsureExists();

        if (State.Status == JournalEntryEntryStatus.Posted)
            throw new InvalidOperationException("Journal entry already posted");

        if (State.Status == JournalEntryEntryStatus.Voided)
            throw new InvalidOperationException("Cannot post voided journal entry");

        if (State.Status == JournalEntryEntryStatus.Reversed)
            throw new InvalidOperationException("Cannot post reversed journal entry");

        if (State.Status == JournalEntryEntryStatus.Rejected)
            throw new InvalidOperationException("Cannot post rejected journal entry");

        // Check period is open
        var periodGrain = _grainFactory.GetGrain<IAccountingPeriodGrain>(
            $"{State.OrganizationId}:accountingperiod:{State.PostingDate.Year}");

        var canPost = await periodGrain.CanPostToDateAsync(State.PostingDate);
        if (!canPost)
            throw new InvalidOperationException(
                $"Cannot post to date {State.PostingDate}. Period may be closed.");

        var now = DateTime.UtcNow;

        // Post to individual account grains
        foreach (var line in State.Lines)
        {
            var accountGrain = _grainFactory.GetGrain<IAccountGrain>(
                GrainKeys.Account(State.OrganizationId, Guid.NewGuid())); // Note: Would need account ID lookup in real impl

            // This is simplified - in a real implementation, you'd look up the AccountGrain
            // by account number through the ChartOfAccounts grain
        }

        RaiseEvent(new JournalEntryPosted
        {
            JournalEntryId = State.JournalEntryId,
            PostedBy = command.PostedBy,
            Notes = command.Notes,
            OccurredAt = now
        });

        await ConfirmEvents();

        _logger.LogInformation(
            "Journal entry {EntryNumber} posted by {UserId}",
            State.EntryNumber,
            command.PostedBy);

        // Publish domain event
        await PublishEventAsync(new JournalEntryPostedDomainEvent
        {
            JournalEntryId = State.JournalEntryId,
            OrganizationId = State.OrganizationId,
            EntryNumber = State.EntryNumber,
            PostingDate = State.PostingDate,
            TotalAmount = State.TotalDebits,
            PostedBy = command.PostedBy,
            OccurredAt = now
        });

        return ToSnapshot();
    }

    public async Task<JournalEntrySnapshot> ApproveAsync(ApproveJournalEntryCommand command)
    {
        EnsureExists();

        if (State.Status != JournalEntryEntryStatus.PendingApproval &&
            State.Status != JournalEntryEntryStatus.Draft)
            throw new InvalidOperationException($"Cannot approve entry in status {State.Status}");

        var now = DateTime.UtcNow;

        RaiseEvent(new JournalEntryApproved
        {
            JournalEntryId = State.JournalEntryId,
            ApprovedBy = command.ApprovedBy,
            Notes = command.Notes,
            OccurredAt = now
        });

        await ConfirmEvents();

        _logger.LogInformation(
            "Journal entry {EntryNumber} approved by {UserId}",
            State.EntryNumber,
            command.ApprovedBy);

        return ToSnapshot();
    }

    public async Task<JournalEntrySnapshot> RejectAsync(RejectJournalEntryCommand command)
    {
        EnsureExists();

        if (State.Status == JournalEntryEntryStatus.Posted)
            throw new InvalidOperationException("Cannot reject posted entry. Use reverse instead.");

        if (State.Status == JournalEntryEntryStatus.Voided)
            throw new InvalidOperationException("Cannot reject voided entry");

        var now = DateTime.UtcNow;

        RaiseEvent(new JournalEntryRejected
        {
            JournalEntryId = State.JournalEntryId,
            RejectedBy = command.RejectedBy,
            Reason = command.Reason,
            OccurredAt = now
        });

        await ConfirmEvents();

        _logger.LogInformation(
            "Journal entry {EntryNumber} rejected by {UserId}. Reason: {Reason}",
            State.EntryNumber,
            command.RejectedBy,
            command.Reason);

        return ToSnapshot();
    }

    public async Task<JournalEntrySnapshot> ReverseAsync(ReverseJournalEntryCommand command)
    {
        EnsureExists();

        if (State.Status != JournalEntryEntryStatus.Posted)
            throw new InvalidOperationException("Only posted entries can be reversed");

        if (State.ReversalEntryId.HasValue)
            throw new InvalidOperationException("Entry has already been reversed");

        var now = DateTime.UtcNow;

        // Create reversal entry
        var reversalEntryId = Guid.NewGuid();
        var reversalLines = State.Lines.Select(l => new JournalEntryLineCommand(
            l.AccountNumber,
            l.CreditAmount,  // Swap debit/credit
            l.DebitAmount,
            $"Reversal of: {l.Description}",
            l.CostCenterId,
            l.TaxCode)).ToList();

        var reversalGrain = _grainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(State.OrganizationId, reversalEntryId));

        await reversalGrain.CreateAsync(new CreateJournalEntryCommand(
            State.OrganizationId,
            reversalEntryId,
            command.ReversalDate,
            reversalLines,
            command.ReversedBy,
            $"Reversal of {State.EntryNumber}: {command.Reason}",
            command.ReversalDate,
            State.ReferenceNumber,
            "Reversal",
            State.JournalEntryId,
            AutoPost: true,
            IsReversing: true));

        RaiseEvent(new JournalEntryReversed
        {
            JournalEntryId = State.JournalEntryId,
            ReversalEntryId = reversalEntryId,
            ReversedBy = command.ReversedBy,
            Reason = command.Reason,
            OccurredAt = now
        });

        await ConfirmEvents();

        _logger.LogInformation(
            "Journal entry {EntryNumber} reversed. Reversal entry: {ReversalId}",
            State.EntryNumber,
            reversalEntryId);

        return ToSnapshot();
    }

    public async Task VoidAsync(VoidJournalEntryCommand command)
    {
        EnsureExists();

        if (State.Status == JournalEntryEntryStatus.Posted)
            throw new InvalidOperationException("Cannot void posted entry. Use reverse instead.");

        if (State.Status == JournalEntryEntryStatus.Voided)
            throw new InvalidOperationException("Entry already voided");

        var now = DateTime.UtcNow;

        RaiseEvent(new JournalEntryVoided
        {
            JournalEntryId = State.JournalEntryId,
            VoidedBy = command.VoidedBy,
            Reason = command.Reason,
            OccurredAt = now
        });

        await ConfirmEvents();

        _logger.LogInformation(
            "Journal entry {EntryNumber} voided by {UserId}. Reason: {Reason}",
            State.EntryNumber,
            command.VoidedBy,
            command.Reason);
    }

    public Task<JournalEntrySnapshot> GetSnapshotAsync()
    {
        return Task.FromResult(ToSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(State.JournalEntryId != Guid.Empty);
    }

    public Task<JournalEntryEntryStatus> GetStatusAsync()
    {
        return Task.FromResult(State.Status);
    }

    private void EnsureExists()
    {
        if (State.JournalEntryId == Guid.Empty)
            throw new InvalidOperationException("Journal entry not found");
    }

    private static string GenerateEntryNumber(DateOnly postingDate)
    {
        return $"JE-{postingDate:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
    }

    private JournalEntrySnapshot ToSnapshot()
    {
        var lines = State.Lines.Select(l => new JournalEntryLine(
            l.LineNumber,
            l.AccountNumber,
            l.AccountName,
            l.DebitAmount,
            l.CreditAmount,
            l.Description,
            l.CostCenterId,
            l.TaxCode)).ToList();

        return new JournalEntrySnapshot(
            State.JournalEntryId,
            State.OrganizationId,
            State.EntryNumber,
            State.PostingDate,
            State.EffectiveDate,
            State.Memo,
            lines,
            State.TotalDebits,
            State.TotalCredits,
            State.Status,
            State.ReferenceNumber,
            State.ReferenceType,
            State.ReferenceId,
            State.IsReversing,
            State.ReversalEntryId,
            State.ReversedFromEntryId,
            State.CreatedAt,
            State.CreatedBy,
            State.PostedAt,
            State.PostedBy,
            State.ApprovedAt,
            State.ApprovedBy);
    }

    private async Task PublishEventAsync(DomainEvent evt)
    {
        if (_eventStream != null && State.OrganizationId != Guid.Empty)
        {
            await _eventStream.Value.OnNextAsync(evt);
        }
    }
}

// ============================================================================
// Domain Events (for Kafka publishing)
// ============================================================================

[GenerateSerializer]
public sealed record JournalEntryPostedDomainEvent : DomainEvent
{
    public override string EventType => "accounting.journal_entry.posted";
    public override string AggregateType => "JournalEntry";
    public override Guid AggregateId => JournalEntryId;

    [Id(100)] public required Guid JournalEntryId { get; init; }
    [Id(101)] public required Guid OrganizationId { get; init; }
    [Id(102)] public required string EntryNumber { get; init; }
    [Id(103)] public required DateOnly PostingDate { get; init; }
    [Id(104)] public required decimal TotalAmount { get; init; }
    [Id(105)] public required Guid PostedBy { get; init; }
}
