using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain representing an expense record.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class ExpenseGrain : JournaledGrain<ExpenseState, IExpenseEvent>, IExpenseGrain
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ExpenseGrain> _logger;
    private Lazy<IAsyncStream<DomainEvent>>? _eventStream;

    public ExpenseGrain(
        IGrainFactory grainFactory,
        ILogger<ExpenseGrain> logger)
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
                StreamConstants.PurchaseDocumentStreamNamespace,
                State.OrganizationId.ToString());
        });

        return base.OnActivateAsync(cancellationToken);
    }

    protected override void TransitionState(ExpenseState state, IExpenseEvent @event)
    {
        switch (@event)
        {
            case ExpenseCreated e:
                state.ExpenseId = e.ExpenseId;
                state.OrganizationId = e.OrganizationId;
                state.SiteId = e.SiteId;
                state.Description = e.Description;
                state.Amount = e.Amount;
                state.Category = Enum.TryParse<ExpenseCategory>(e.Category, out var cat) ? cat : ExpenseCategory.Other;
                state.VendorId = e.VendorId;
                state.VendorName = e.VendorName;
                state.ExpenseDate = e.ExpenseDate;
                state.DocumentUrl = e.ReceiptUrl;
                state.Status = ExpenseStatus.Pending;
                state.CreatedAt = e.OccurredAt;
                state.CreatedBy = e.SubmittedBy;
                break;

            case ExpenseUpdated e:
                if (e.Description != null) state.Description = e.Description;
                if (e.Amount.HasValue) state.Amount = e.Amount.Value;
                if (e.Category != null && Enum.TryParse<ExpenseCategory>(e.Category, out var updCat))
                    state.Category = updCat;
                if (e.ExpenseDate.HasValue) state.ExpenseDate = e.ExpenseDate.Value;
                if (e.ReceiptUrl != null) state.DocumentUrl = e.ReceiptUrl;
                state.UpdatedAt = e.OccurredAt;
                state.UpdatedBy = e.UpdatedBy;
                break;

            case ExpenseApproved e:
                state.Status = ExpenseStatus.Approved;
                state.ApprovedBy = e.ApprovedBy;
                state.ApprovedAt = e.OccurredAt;
                if (e.Notes != null)
                    state.Notes = (state.Notes ?? "") + $"\nApproval note: {e.Notes}";
                break;

            case ExpenseRejected e:
                state.Status = ExpenseStatus.Rejected;
                state.Notes = (state.Notes ?? "") + $"\nRejection reason: {e.Reason}";
                break;

            case ExpensePaid e:
                state.Status = ExpenseStatus.Paid;
                if (e.ReferenceNumber != null) state.ReferenceNumber = e.ReferenceNumber;
                if (e.PaymentMethod != null && Enum.TryParse<PaymentMethod>(e.PaymentMethod, out var pm))
                    state.PaymentMethod = pm;
                break;

            case ExpenseVoided e:
                state.Status = ExpenseStatus.Voided;
                state.Notes = (state.Notes ?? "") + $"\nVoided: {e.Reason}";
                break;

            case ExpenseCancelled e:
                state.Status = ExpenseStatus.Voided;
                state.Notes = (state.Notes ?? "") + $"\nCancelled: {e.Reason}";
                break;

            case ExpenseReceiptAttached e:
                state.DocumentUrl = e.ReceiptUrl;
                state.DocumentFilename = e.FileName;
                break;

            case ExpenseRecurrenceSet e:
                state.IsRecurring = true;
                state.RecurrencePattern = new RecurrencePattern
                {
                    Frequency = Enum.TryParse<RecurrenceFrequency>(e.Frequency, out var freq) ? freq : RecurrenceFrequency.Monthly,
                    Interval = e.Interval
                };
                break;
        }
    }

    public async Task<ExpenseSnapshot> RecordAsync(RecordExpenseCommand command)
    {
        if (State.ExpenseId != Guid.Empty)
            throw new InvalidOperationException("Expense already exists");

        RaiseEvent(new ExpenseCreated
        {
            ExpenseId = command.ExpenseId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            Description = command.Description,
            Amount = command.Amount,
            Category = command.Category.ToString(),
            VendorId = command.VendorId,
            VendorName = command.VendorName,
            ExpenseDate = command.ExpenseDate,
            ReceiptUrl = null,
            SubmittedBy = command.RecordedBy,
            OccurredAt = DateTime.UtcNow
        });

        // Apply additional fields that aren't in the journaled event
        State.Currency = command.Currency;
        State.CustomCategory = command.CustomCategory;
        State.PaymentMethod = command.PaymentMethod;
        State.ReferenceNumber = command.ReferenceNumber;
        State.TaxAmount = command.TaxAmount;
        State.IsTaxDeductible = command.IsTaxDeductible;
        State.Notes = command.Notes;
        State.Tags = command.Tags?.ToList() ?? [];

        await ConfirmEvents();

        // Register with index
        await RegisterWithIndexAsync();

        var evt = new ExpenseRecorded
        {
            ExpenseId = command.ExpenseId,
            SiteId = command.SiteId,
            Category = command.Category.ToString(),
            CustomCategory = command.CustomCategory,
            Description = command.Description,
            Amount = command.Amount,
            Currency = command.Currency,
            ExpenseDate = command.ExpenseDate,
            VendorName = command.VendorName,
            PaymentMethod = command.PaymentMethod?.ToString(),
            RecordedBy = command.RecordedBy,
            OrganizationId = command.OrganizationId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Expense recorded: {ExpenseId} - {Description} (${Amount})",
            command.ExpenseId,
            command.Description,
            command.Amount);

        return ToSnapshot();
    }

    public async Task<ExpenseSnapshot> UpdateAsync(UpdateExpenseCommand command)
    {
        EnsureExists();
        EnsureModifiable();

        RaiseEvent(new ExpenseUpdated
        {
            ExpenseId = State.ExpenseId,
            Description = command.Description,
            Amount = command.Amount,
            Category = command.Category?.ToString(),
            ExpenseDate = command.ExpenseDate,
            ReceiptUrl = null,
            UpdatedBy = command.UpdatedBy,
            OccurredAt = DateTime.UtcNow
        });

        // Apply additional fields
        if (command.CustomCategory != null) State.CustomCategory = command.CustomCategory;
        if (command.VendorId.HasValue) State.VendorId = command.VendorId;
        if (command.VendorName != null) State.VendorName = command.VendorName;
        if (command.PaymentMethod.HasValue) State.PaymentMethod = command.PaymentMethod;
        if (command.ReferenceNumber != null) State.ReferenceNumber = command.ReferenceNumber;
        if (command.TaxAmount.HasValue) State.TaxAmount = command.TaxAmount;
        if (command.IsTaxDeductible.HasValue) State.IsTaxDeductible = command.IsTaxDeductible.Value;
        if (command.Notes != null) State.Notes = command.Notes;
        if (command.Tags != null) State.Tags = command.Tags.ToList();

        await ConfirmEvents();
        await UpdateIndexAsync();

        var evt = new ExpenseUpdatedDomainEvent
        {
            ExpenseId = State.ExpenseId,
            UpdatedBy = command.UpdatedBy,
            Description = command.Description,
            Amount = command.Amount,
            Category = command.Category?.ToString(),
            ExpenseDate = command.ExpenseDate,
            OrganizationId = State.OrganizationId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Expense updated: {ExpenseId} by {UserId}",
            State.ExpenseId,
            command.UpdatedBy);

        return ToSnapshot();
    }

    public async Task<ExpenseSnapshot> ApproveAsync(ApproveExpenseCommand command)
    {
        EnsureExists();

        if (State.Status != ExpenseStatus.Pending)
            throw new InvalidOperationException($"Cannot approve expense in status {State.Status}");

        RaiseEvent(new ExpenseApproved
        {
            ExpenseId = State.ExpenseId,
            ApprovedBy = command.ApprovedBy,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
        await UpdateIndexAsync();

        var evt = new ExpenseApprovedDomainEvent
        {
            ExpenseId = State.ExpenseId,
            ApprovedBy = command.ApprovedBy,
            Notes = command.Notes,
            OrganizationId = State.OrganizationId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Expense approved: {ExpenseId} by {UserId}",
            State.ExpenseId,
            command.ApprovedBy);

        return ToSnapshot();
    }

    public async Task<ExpenseSnapshot> RejectAsync(RejectExpenseCommand command)
    {
        EnsureExists();

        if (State.Status != ExpenseStatus.Pending)
            throw new InvalidOperationException($"Cannot reject expense in status {State.Status}");

        RaiseEvent(new ExpenseRejected
        {
            ExpenseId = State.ExpenseId,
            RejectedBy = command.RejectedBy,
            Reason = command.Reason,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
        await UpdateIndexAsync();

        var evt = new ExpenseRejectedDomainEvent
        {
            ExpenseId = State.ExpenseId,
            RejectedBy = command.RejectedBy,
            Reason = command.Reason,
            OrganizationId = State.OrganizationId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Expense rejected: {ExpenseId} by {UserId}, reason: {Reason}",
            State.ExpenseId,
            command.RejectedBy,
            command.Reason);

        return ToSnapshot();
    }

    public async Task<ExpenseSnapshot> MarkPaidAsync(MarkExpensePaidCommand command)
    {
        EnsureExists();

        if (State.Status != ExpenseStatus.Approved && State.Status != ExpenseStatus.Pending)
            throw new InvalidOperationException($"Cannot mark expense as paid in status {State.Status}");

        RaiseEvent(new ExpensePaid
        {
            ExpenseId = State.ExpenseId,
            PaidBy = command.PaidBy,
            PaymentDate = command.PaymentDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            ReferenceNumber = command.ReferenceNumber,
            PaymentMethod = command.PaymentMethod?.ToString(),
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
        await UpdateIndexAsync();

        var evt = new ExpensePaidDomainEvent
        {
            ExpenseId = State.ExpenseId,
            PaidBy = command.PaidBy,
            PaymentDate = command.PaymentDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            ReferenceNumber = command.ReferenceNumber,
            PaymentMethod = command.PaymentMethod?.ToString(),
            OrganizationId = State.OrganizationId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Expense marked paid: {ExpenseId} by {UserId}",
            State.ExpenseId,
            command.PaidBy);

        return ToSnapshot();
    }

    public async Task VoidAsync(VoidExpenseCommand command)
    {
        EnsureExists();

        if (State.Status == ExpenseStatus.Voided)
            throw new InvalidOperationException("Expense already voided");

        RaiseEvent(new ExpenseVoided
        {
            ExpenseId = State.ExpenseId,
            VoidedBy = command.VoidedBy,
            Reason = command.Reason,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Remove from index
        var indexGrain = _grainFactory.GetGrain<IExpenseIndexGrain>(
            GrainKeys.Site(State.OrganizationId, State.SiteId));
        await indexGrain.RemoveExpenseAsync(State.ExpenseId);

        var evt = new ExpenseVoidedDomainEvent
        {
            ExpenseId = State.ExpenseId,
            VoidedBy = command.VoidedBy,
            Reason = command.Reason,
            OrganizationId = State.OrganizationId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Expense voided: {ExpenseId} by {UserId}, reason: {Reason}",
            State.ExpenseId,
            command.VoidedBy,
            command.Reason);
    }

    public async Task<ExpenseSnapshot> AttachDocumentAsync(AttachDocumentCommand command)
    {
        EnsureExists();

        RaiseEvent(new ExpenseReceiptAttached
        {
            ExpenseId = State.ExpenseId,
            ReceiptUrl = command.DocumentUrl,
            FileName = command.Filename,
            OccurredAt = DateTime.UtcNow
        });

        State.UpdatedAt = DateTime.UtcNow;
        State.UpdatedBy = command.AttachedBy;

        await ConfirmEvents();
        await UpdateIndexAsync();

        var evt = new ExpenseDocumentAttached
        {
            ExpenseId = State.ExpenseId,
            DocumentUrl = command.DocumentUrl,
            Filename = command.Filename,
            AttachedBy = command.AttachedBy,
            OrganizationId = State.OrganizationId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Document attached to expense: {ExpenseId}, file: {Filename}",
            State.ExpenseId,
            command.Filename);

        return ToSnapshot();
    }

    public async Task<ExpenseSnapshot> SetRecurrenceAsync(SetRecurrenceCommand command)
    {
        EnsureExists();

        RaiseEvent(new ExpenseRecurrenceSet
        {
            ExpenseId = State.ExpenseId,
            Frequency = command.Pattern.Frequency.ToString(),
            Interval = command.Pattern.Interval,
            SetBy = command.SetBy,
            OccurredAt = DateTime.UtcNow
        });

        // Apply the full pattern
        State.RecurrencePattern = command.Pattern;

        await ConfirmEvents();

        var evt = new RecurringExpenseCreated
        {
            ExpenseId = State.ExpenseId,
            Pattern = command.Pattern,
            CreatedBy = command.SetBy,
            OrganizationId = State.OrganizationId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Recurrence set for expense: {ExpenseId}, frequency: {Frequency}",
            State.ExpenseId,
            command.Pattern.Frequency);

        return ToSnapshot();
    }

    public Task<ExpenseSnapshot> GetSnapshotAsync()
    {
        return Task.FromResult(ToSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(State.ExpenseId != Guid.Empty);
    }

    private void EnsureExists()
    {
        if (State.ExpenseId == Guid.Empty)
            throw new InvalidOperationException("Expense not initialized");
    }

    private void EnsureModifiable()
    {
        if (State.Status == ExpenseStatus.Voided)
            throw new InvalidOperationException("Cannot modify voided expense");
        if (State.Status == ExpenseStatus.Paid)
            throw new InvalidOperationException("Cannot modify paid expense");
    }

    private ExpenseSnapshot ToSnapshot()
    {
        return new ExpenseSnapshot(
            State.ExpenseId,
            State.OrganizationId,
            State.SiteId,
            State.Category,
            State.CustomCategory,
            State.Description,
            State.Amount,
            State.Currency,
            State.ExpenseDate,
            State.VendorId,
            State.VendorName,
            State.PaymentMethod,
            State.ReferenceNumber,
            State.DocumentUrl,
            State.DocumentFilename,
            State.IsRecurring,
            State.TaxAmount,
            State.IsTaxDeductible,
            State.Notes,
            State.Tags,
            State.Status,
            State.ApprovedBy,
            State.ApprovedAt,
            State.CreatedAt,
            State.CreatedBy,
            Version);
    }

    private ExpenseSummary ToSummary()
    {
        return new ExpenseSummary(
            State.ExpenseId,
            State.Category,
            State.Description,
            State.Amount,
            State.Currency,
            State.ExpenseDate,
            State.VendorName,
            State.Status,
            !string.IsNullOrEmpty(State.DocumentUrl));
    }

    private async Task RegisterWithIndexAsync()
    {
        var indexGrain = _grainFactory.GetGrain<IExpenseIndexGrain>(
            GrainKeys.Site(State.OrganizationId, State.SiteId));
        await indexGrain.RegisterExpenseAsync(ToSummary());
    }

    private async Task UpdateIndexAsync()
    {
        var indexGrain = _grainFactory.GetGrain<IExpenseIndexGrain>(
            GrainKeys.Site(State.OrganizationId, State.SiteId));
        await indexGrain.UpdateExpenseAsync(ToSummary());
    }

    private async Task PublishEventAsync(DomainEvent evt)
    {
        if (_eventStream != null && State.OrganizationId != Guid.Empty)
        {
            await _eventStream.Value.OnNextAsync(evt);
        }
    }
}

/// <summary>
/// Grain for indexing and querying expenses at site level.
/// </summary>
public class ExpenseIndexGrain : Grain, IExpenseIndexGrain
{
    private readonly IPersistentState<ExpenseIndexState> _state;
    private readonly ILogger<ExpenseIndexGrain> _logger;

    public ExpenseIndexGrain(
        [PersistentState("expense-index", "purchases")]
        IPersistentState<ExpenseIndexState> state,
        ILogger<ExpenseIndexGrain> logger)
    {
        _state = state;
        _logger = logger;
    }

    public async Task RegisterExpenseAsync(ExpenseSummary expense)
    {
        _state.State.Expenses[expense.ExpenseId] = expense;
        await _state.WriteStateAsync();
    }

    public async Task UpdateExpenseAsync(ExpenseSummary expense)
    {
        if (_state.State.Expenses.ContainsKey(expense.ExpenseId))
        {
            _state.State.Expenses[expense.ExpenseId] = expense;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveExpenseAsync(Guid expenseId)
    {
        if (_state.State.Expenses.Remove(expenseId))
        {
            await _state.WriteStateAsync();
        }
    }

    public Task<ExpenseQueryResult> QueryAsync(ExpenseQuery query)
    {
        var expenses = _state.State.Expenses.Values.AsEnumerable();

        // Apply filters
        if (query.FromDate.HasValue)
            expenses = expenses.Where(e => e.ExpenseDate >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            expenses = expenses.Where(e => e.ExpenseDate <= query.ToDate.Value);
        if (query.Category.HasValue)
            expenses = expenses.Where(e => e.Category == query.Category.Value);
        if (query.Status.HasValue)
            expenses = expenses.Where(e => e.Status == query.Status.Value);
        if (!string.IsNullOrEmpty(query.VendorName))
            expenses = expenses.Where(e => e.VendorName?.Contains(query.VendorName, StringComparison.OrdinalIgnoreCase) == true);
        if (query.MinAmount.HasValue)
            expenses = expenses.Where(e => e.Amount >= query.MinAmount.Value);
        if (query.MaxAmount.HasValue)
            expenses = expenses.Where(e => e.Amount <= query.MaxAmount.Value);

        var filtered = expenses.ToList();
        var totalCount = filtered.Count;
        var totalAmount = filtered.Sum(e => e.Amount);

        var paged = filtered
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.Amount)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToList();

        return Task.FromResult(new ExpenseQueryResult(paged, totalCount, totalAmount));
    }

    public Task<IReadOnlyList<ExpenseCategoryTotal>> GetCategoryTotalsAsync(
        DateOnly fromDate,
        DateOnly toDate)
    {
        var totals = _state.State.Expenses.Values
            .Where(e => e.ExpenseDate >= fromDate && e.ExpenseDate <= toDate)
            .Where(e => e.Status != ExpenseStatus.Voided && e.Status != ExpenseStatus.Rejected)
            .GroupBy(e => e.Category)
            .Select(g => new ExpenseCategoryTotal(
                g.Key,
                null,
                g.Count(),
                g.Sum(e => e.Amount)))
            .OrderByDescending(t => t.TotalAmount)
            .ToList();

        return Task.FromResult<IReadOnlyList<ExpenseCategoryTotal>>(totals);
    }

    public Task<decimal> GetTotalAsync(DateOnly fromDate, DateOnly toDate)
    {
        var total = _state.State.Expenses.Values
            .Where(e => e.ExpenseDate >= fromDate && e.ExpenseDate <= toDate)
            .Where(e => e.Status != ExpenseStatus.Voided && e.Status != ExpenseStatus.Rejected)
            .Sum(e => e.Amount);

        return Task.FromResult(total);
    }
}

/// <summary>
/// State for expense index.
/// </summary>
[GenerateSerializer]
public sealed class ExpenseIndexState
{
    [Id(0)] public Dictionary<Guid, ExpenseSummary> Expenses { get; set; } = [];
}
