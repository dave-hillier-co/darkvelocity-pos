using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain representing an expense record.
/// </summary>
public class ExpenseGrain : Grain, IExpenseGrain
{
    private readonly IPersistentState<ExpenseState> _state;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ExpenseGrain> _logger;
    private IAsyncStream<DomainEvent>? _eventStream;

    public ExpenseGrain(
        [PersistentState("expense", "purchases")]
        IPersistentState<ExpenseState> state,
        IGrainFactory grainFactory,
        ILogger<ExpenseGrain> logger)
    {
        _state = state;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            _eventStream = streamProvider.GetStream<DomainEvent>(
                StreamConstants.PurchaseDocumentStreamNamespace,
                _state.State.OrganizationId.ToString());
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<ExpenseSnapshot> RecordAsync(RecordExpenseCommand command)
    {
        if (_state.State.ExpenseId != Guid.Empty)
            throw new InvalidOperationException("Expense already exists");

        _state.State = new ExpenseState
        {
            ExpenseId = command.ExpenseId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            Category = command.Category,
            CustomCategory = command.CustomCategory,
            Description = command.Description,
            Amount = command.Amount,
            Currency = command.Currency,
            ExpenseDate = command.ExpenseDate,
            VendorId = command.VendorId,
            VendorName = command.VendorName,
            PaymentMethod = command.PaymentMethod,
            ReferenceNumber = command.ReferenceNumber,
            TaxAmount = command.TaxAmount,
            IsTaxDeductible = command.IsTaxDeductible,
            Notes = command.Notes,
            Tags = command.Tags?.ToList() ?? [],
            Status = ExpenseStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = command.RecordedBy,
            Version = 1
        };

        await _state.WriteStateAsync();

        // Initialize event stream
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        _eventStream = streamProvider.GetStream<DomainEvent>(
            StreamConstants.PurchaseDocumentStreamNamespace,
            _state.State.OrganizationId.ToString());

        // Register with index
        await RegisterWithIndexAsync();

        var evt = new ExpenseRecorded
        {
            ExpenseId = command.ExpenseId,
            SiteId = command.SiteId,
            Category = command.Category,
            CustomCategory = command.CustomCategory,
            Description = command.Description,
            Amount = command.Amount,
            Currency = command.Currency,
            ExpenseDate = command.ExpenseDate,
            VendorName = command.VendorName,
            PaymentMethod = command.PaymentMethod,
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

        if (command.Category.HasValue)
            _state.State.Category = command.Category.Value;
        if (command.CustomCategory != null)
            _state.State.CustomCategory = command.CustomCategory;
        if (command.Description != null)
            _state.State.Description = command.Description;
        if (command.Amount.HasValue)
            _state.State.Amount = command.Amount.Value;
        if (command.ExpenseDate.HasValue)
            _state.State.ExpenseDate = command.ExpenseDate.Value;
        if (command.VendorId.HasValue)
            _state.State.VendorId = command.VendorId;
        if (command.VendorName != null)
            _state.State.VendorName = command.VendorName;
        if (command.PaymentMethod.HasValue)
            _state.State.PaymentMethod = command.PaymentMethod;
        if (command.ReferenceNumber != null)
            _state.State.ReferenceNumber = command.ReferenceNumber;
        if (command.TaxAmount.HasValue)
            _state.State.TaxAmount = command.TaxAmount;
        if (command.IsTaxDeductible.HasValue)
            _state.State.IsTaxDeductible = command.IsTaxDeductible.Value;
        if (command.Notes != null)
            _state.State.Notes = command.Notes;
        if (command.Tags != null)
            _state.State.Tags = command.Tags.ToList();

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.UpdatedBy = command.UpdatedBy;
        _state.State.Version++;

        await _state.WriteStateAsync();
        await UpdateIndexAsync();

        var evt = new ExpenseUpdated
        {
            ExpenseId = _state.State.ExpenseId,
            UpdatedBy = command.UpdatedBy,
            Description = command.Description,
            Amount = command.Amount,
            Category = command.Category,
            ExpenseDate = command.ExpenseDate,
            OrganizationId = _state.State.OrganizationId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Expense updated: {ExpenseId} by {UserId}",
            _state.State.ExpenseId,
            command.UpdatedBy);

        return ToSnapshot();
    }

    public async Task<ExpenseSnapshot> ApproveAsync(ApproveExpenseCommand command)
    {
        EnsureExists();

        if (_state.State.Status != ExpenseStatus.Pending)
            throw new InvalidOperationException($"Cannot approve expense in status {_state.State.Status}");

        _state.State.Status = ExpenseStatus.Approved;
        _state.State.ApprovedBy = command.ApprovedBy;
        _state.State.ApprovedAt = DateTime.UtcNow;
        if (command.Notes != null)
            _state.State.Notes = (_state.State.Notes ?? "") + $"\nApproval note: {command.Notes}";
        _state.State.Version++;

        await _state.WriteStateAsync();
        await UpdateIndexAsync();

        var evt = new ExpenseApproved
        {
            ExpenseId = _state.State.ExpenseId,
            ApprovedBy = command.ApprovedBy,
            Notes = command.Notes,
            OrganizationId = _state.State.OrganizationId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Expense approved: {ExpenseId} by {UserId}",
            _state.State.ExpenseId,
            command.ApprovedBy);

        return ToSnapshot();
    }

    public async Task<ExpenseSnapshot> RejectAsync(RejectExpenseCommand command)
    {
        EnsureExists();

        if (_state.State.Status != ExpenseStatus.Pending)
            throw new InvalidOperationException($"Cannot reject expense in status {_state.State.Status}");

        _state.State.Status = ExpenseStatus.Rejected;
        _state.State.Notes = (_state.State.Notes ?? "") + $"\nRejection reason: {command.Reason}";
        _state.State.Version++;

        await _state.WriteStateAsync();
        await UpdateIndexAsync();

        var evt = new ExpenseRejected
        {
            ExpenseId = _state.State.ExpenseId,
            RejectedBy = command.RejectedBy,
            Reason = command.Reason,
            OrganizationId = _state.State.OrganizationId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Expense rejected: {ExpenseId} by {UserId}, reason: {Reason}",
            _state.State.ExpenseId,
            command.RejectedBy,
            command.Reason);

        return ToSnapshot();
    }

    public async Task<ExpenseSnapshot> MarkPaidAsync(MarkExpensePaidCommand command)
    {
        EnsureExists();

        if (_state.State.Status != ExpenseStatus.Approved && _state.State.Status != ExpenseStatus.Pending)
            throw new InvalidOperationException($"Cannot mark expense as paid in status {_state.State.Status}");

        _state.State.Status = ExpenseStatus.Paid;
        if (command.ReferenceNumber != null)
            _state.State.ReferenceNumber = command.ReferenceNumber;
        if (command.PaymentMethod.HasValue)
            _state.State.PaymentMethod = command.PaymentMethod;
        _state.State.Version++;

        await _state.WriteStateAsync();
        await UpdateIndexAsync();

        var evt = new ExpensePaid
        {
            ExpenseId = _state.State.ExpenseId,
            PaidBy = command.PaidBy,
            PaymentDate = command.PaymentDate,
            ReferenceNumber = command.ReferenceNumber,
            PaymentMethod = command.PaymentMethod,
            OrganizationId = _state.State.OrganizationId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Expense marked paid: {ExpenseId} by {UserId}",
            _state.State.ExpenseId,
            command.PaidBy);

        return ToSnapshot();
    }

    public async Task VoidAsync(VoidExpenseCommand command)
    {
        EnsureExists();

        if (_state.State.Status == ExpenseStatus.Voided)
            throw new InvalidOperationException("Expense already voided");

        _state.State.Status = ExpenseStatus.Voided;
        _state.State.Notes = (_state.State.Notes ?? "") + $"\nVoided: {command.Reason}";
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Remove from index
        var indexGrain = _grainFactory.GetGrain<IExpenseIndexGrain>(
            GrainKeys.Site(_state.State.OrganizationId, _state.State.SiteId));
        await indexGrain.RemoveExpenseAsync(_state.State.ExpenseId);

        var evt = new ExpenseVoided
        {
            ExpenseId = _state.State.ExpenseId,
            VoidedBy = command.VoidedBy,
            Reason = command.Reason,
            OrganizationId = _state.State.OrganizationId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Expense voided: {ExpenseId} by {UserId}, reason: {Reason}",
            _state.State.ExpenseId,
            command.VoidedBy,
            command.Reason);
    }

    public async Task<ExpenseSnapshot> AttachDocumentAsync(AttachDocumentCommand command)
    {
        EnsureExists();

        _state.State.DocumentUrl = command.DocumentUrl;
        _state.State.DocumentFilename = command.Filename;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.UpdatedBy = command.AttachedBy;
        _state.State.Version++;

        await _state.WriteStateAsync();
        await UpdateIndexAsync();

        var evt = new ExpenseDocumentAttached
        {
            ExpenseId = _state.State.ExpenseId,
            DocumentUrl = command.DocumentUrl,
            Filename = command.Filename,
            AttachedBy = command.AttachedBy,
            OrganizationId = _state.State.OrganizationId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Document attached to expense: {ExpenseId}, file: {Filename}",
            _state.State.ExpenseId,
            command.Filename);

        return ToSnapshot();
    }

    public async Task<ExpenseSnapshot> SetRecurrenceAsync(SetRecurrenceCommand command)
    {
        EnsureExists();

        _state.State.IsRecurring = true;
        _state.State.RecurrencePattern = command.Pattern;
        _state.State.Version++;

        await _state.WriteStateAsync();

        var evt = new RecurringExpenseCreated
        {
            ExpenseId = _state.State.ExpenseId,
            Pattern = command.Pattern,
            CreatedBy = command.SetBy,
            OrganizationId = _state.State.OrganizationId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Recurrence set for expense: {ExpenseId}, frequency: {Frequency}",
            _state.State.ExpenseId,
            command.Pattern.Frequency);

        return ToSnapshot();
    }

    public Task<ExpenseSnapshot> GetSnapshotAsync()
    {
        return Task.FromResult(ToSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.ExpenseId != Guid.Empty);
    }

    private void EnsureExists()
    {
        if (_state.State.ExpenseId == Guid.Empty)
            throw new InvalidOperationException("Expense not initialized");
    }

    private void EnsureModifiable()
    {
        if (_state.State.Status == ExpenseStatus.Voided)
            throw new InvalidOperationException("Cannot modify voided expense");
        if (_state.State.Status == ExpenseStatus.Paid)
            throw new InvalidOperationException("Cannot modify paid expense");
    }

    private ExpenseSnapshot ToSnapshot()
    {
        return new ExpenseSnapshot(
            _state.State.ExpenseId,
            _state.State.OrganizationId,
            _state.State.SiteId,
            _state.State.Category,
            _state.State.CustomCategory,
            _state.State.Description,
            _state.State.Amount,
            _state.State.Currency,
            _state.State.ExpenseDate,
            _state.State.VendorId,
            _state.State.VendorName,
            _state.State.PaymentMethod,
            _state.State.ReferenceNumber,
            _state.State.DocumentUrl,
            _state.State.DocumentFilename,
            _state.State.IsRecurring,
            _state.State.TaxAmount,
            _state.State.IsTaxDeductible,
            _state.State.Notes,
            _state.State.Tags,
            _state.State.Status,
            _state.State.ApprovedBy,
            _state.State.ApprovedAt,
            _state.State.CreatedAt,
            _state.State.CreatedBy,
            _state.State.Version);
    }

    private ExpenseSummary ToSummary()
    {
        return new ExpenseSummary(
            _state.State.ExpenseId,
            _state.State.Category,
            _state.State.Description,
            _state.State.Amount,
            _state.State.Currency,
            _state.State.ExpenseDate,
            _state.State.VendorName,
            _state.State.Status,
            !string.IsNullOrEmpty(_state.State.DocumentUrl));
    }

    private async Task RegisterWithIndexAsync()
    {
        var indexGrain = _grainFactory.GetGrain<IExpenseIndexGrain>(
            GrainKeys.Site(_state.State.OrganizationId, _state.State.SiteId));
        await indexGrain.RegisterExpenseAsync(ToSummary());
    }

    private async Task UpdateIndexAsync()
    {
        var indexGrain = _grainFactory.GetGrain<IExpenseIndexGrain>(
            GrainKeys.Site(_state.State.OrganizationId, _state.State.SiteId));
        await indexGrain.UpdateExpenseAsync(ToSummary());
    }

    private async Task PublishEventAsync(DomainEvent evt)
    {
        if (_eventStream != null)
        {
            await _eventStream.OnNextAsync(evt);
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
