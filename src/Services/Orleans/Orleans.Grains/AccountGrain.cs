using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.State;
using Orleans.Runtime;

namespace DarkVelocity.Orleans.Grains;

/// <summary>
/// Orleans grain for managing accounts with full journal entry tracking.
/// Implements double-entry accounting with complete audit trail.
/// </summary>
public class AccountGrain : Grain, IAccountGrain
{
    private readonly IPersistentState<AccountState> _state;
    private const int MaxJournalEntries = 500;

    public AccountGrain(
        [PersistentState("account", "OrleansStorage")]
        IPersistentState<AccountState> state)
    {
        _state = state;
    }

    #region Lifecycle

    public async Task<CreateAccountResult> CreateAsync(CreateAccountCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Account already exists");

        if (string.IsNullOrWhiteSpace(command.AccountCode))
            throw new ArgumentException("Account code is required", nameof(command));

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Account name is required", nameof(command));

        var now = DateTime.UtcNow;

        _state.State = new AccountState
        {
            Id = command.AccountId,
            OrganizationId = command.OrganizationId,
            AccountCode = command.AccountCode,
            Name = command.Name,
            AccountType = command.AccountType,
            SubType = command.SubType,
            Description = command.Description,
            ParentAccountId = command.ParentAccountId,
            IsSystemAccount = command.IsSystemAccount,
            IsActive = true,
            TaxCode = command.TaxCode,
            ExternalReference = command.ExternalReference,
            Currency = command.Currency,
            Balance = 0,
            CreatedAt = now,
            CreatedBy = command.CreatedBy,
            CurrentPeriodYear = now.Year,
            CurrentPeriodMonth = now.Month,
            Version = 1
        };

        // Record opening balance if provided
        if (command.OpeningBalance != 0)
        {
            var entry = new AccountJournalEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = now,
                EntryType = JournalEntryType.Opening,
                Status = JournalEntryStatus.Posted,
                Amount = Math.Abs(command.OpeningBalance),
                BalanceAfter = command.OpeningBalance,
                Description = "Opening balance",
                PerformedBy = command.CreatedBy
            };

            _state.State.Balance = command.OpeningBalance;
            _state.State.JournalEntries.Add(entry);
            _state.State.TotalEntryCount = 1;
            _state.State.LastEntryAt = now;

            // Track as debit or credit based on sign and account type
            if (IsDebitNormalBalance(_state.State.AccountType))
            {
                if (command.OpeningBalance > 0)
                    _state.State.TotalDebits = command.OpeningBalance;
                else
                    _state.State.TotalCredits = Math.Abs(command.OpeningBalance);
            }
            else
            {
                if (command.OpeningBalance > 0)
                    _state.State.TotalCredits = command.OpeningBalance;
                else
                    _state.State.TotalDebits = Math.Abs(command.OpeningBalance);
            }
        }

        await _state.WriteStateAsync();

        return new CreateAccountResult(
            _state.State.Id,
            _state.State.AccountCode,
            _state.State.Balance);
    }

    public Task<AccountState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.Id != Guid.Empty);
    }

    public async Task UpdateAsync(UpdateAccountCommand command)
    {
        EnsureExists();

        if (command.Name != null)
            _state.State.Name = command.Name;
        if (command.Description != null)
            _state.State.Description = command.Description;
        if (command.SubType != null)
            _state.State.SubType = command.SubType;
        if (command.TaxCode != null)
            _state.State.TaxCode = command.TaxCode;
        if (command.ExternalReference != null)
            _state.State.ExternalReference = command.ExternalReference;
        if (command.ParentAccountId.HasValue)
            _state.State.ParentAccountId = command.ParentAccountId;

        _state.State.LastModifiedAt = DateTime.UtcNow;
        _state.State.LastModifiedBy = command.UpdatedBy;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task ActivateAsync(Guid activatedBy)
    {
        EnsureExists();

        if (_state.State.IsActive)
            throw new InvalidOperationException("Account is already active");

        _state.State.IsActive = true;
        _state.State.LastModifiedAt = DateTime.UtcNow;
        _state.State.LastModifiedBy = activatedBy;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync(Guid deactivatedBy)
    {
        EnsureExists();

        if (!_state.State.IsActive)
            throw new InvalidOperationException("Account is already deactivated");

        if (_state.State.IsSystemAccount)
            throw new InvalidOperationException("System accounts cannot be deactivated");

        _state.State.IsActive = false;
        _state.State.LastModifiedAt = DateTime.UtcNow;
        _state.State.LastModifiedBy = deactivatedBy;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    #endregion

    #region Journal Entries

    public async Task<PostEntryResult> PostDebitAsync(PostDebitCommand command)
    {
        EnsureExists();
        EnsureActive();

        if (command.Amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(command));

        var now = DateTime.UtcNow;
        var entryId = Guid.NewGuid();

        // Debit increases balance for Asset/Expense, decreases for Liability/Equity/Revenue
        var balanceChange = IsDebitNormalBalance(_state.State.AccountType)
            ? command.Amount
            : -command.Amount;

        var newBalance = _state.State.Balance + balanceChange;

        var entry = new AccountJournalEntry
        {
            Id = entryId,
            Timestamp = now,
            EntryType = JournalEntryType.Debit,
            Status = JournalEntryStatus.Posted,
            Amount = command.Amount,
            BalanceAfter = newBalance,
            Description = command.Description,
            ReferenceNumber = command.ReferenceNumber,
            ReferenceType = command.ReferenceType,
            ReferenceId = command.ReferenceId,
            AccountingJournalEntryId = command.AccountingJournalEntryId,
            PerformedBy = command.PerformedBy,
            CostCenterId = command.CostCenterId,
            Notes = command.Notes
        };

        _state.State.Balance = newBalance;
        _state.State.TotalDebits += command.Amount;
        _state.State.TotalEntryCount++;
        _state.State.LastEntryAt = now;
        AddJournalEntry(entry);
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new PostEntryResult(entryId, command.Amount, newBalance, JournalEntryType.Debit);
    }

    public async Task<PostEntryResult> PostCreditAsync(PostCreditCommand command)
    {
        EnsureExists();
        EnsureActive();

        if (command.Amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(command));

        var now = DateTime.UtcNow;
        var entryId = Guid.NewGuid();

        // Credit decreases balance for Asset/Expense, increases for Liability/Equity/Revenue
        var balanceChange = IsDebitNormalBalance(_state.State.AccountType)
            ? -command.Amount
            : command.Amount;

        var newBalance = _state.State.Balance + balanceChange;

        var entry = new AccountJournalEntry
        {
            Id = entryId,
            Timestamp = now,
            EntryType = JournalEntryType.Credit,
            Status = JournalEntryStatus.Posted,
            Amount = command.Amount,
            BalanceAfter = newBalance,
            Description = command.Description,
            ReferenceNumber = command.ReferenceNumber,
            ReferenceType = command.ReferenceType,
            ReferenceId = command.ReferenceId,
            AccountingJournalEntryId = command.AccountingJournalEntryId,
            PerformedBy = command.PerformedBy,
            CostCenterId = command.CostCenterId,
            Notes = command.Notes
        };

        _state.State.Balance = newBalance;
        _state.State.TotalCredits += command.Amount;
        _state.State.TotalEntryCount++;
        _state.State.LastEntryAt = now;
        AddJournalEntry(entry);
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new PostEntryResult(entryId, command.Amount, newBalance, JournalEntryType.Credit);
    }

    public async Task<PostEntryResult> AdjustBalanceAsync(AdjustBalanceCommand command)
    {
        EnsureExists();
        EnsureActive();

        var now = DateTime.UtcNow;
        var entryId = Guid.NewGuid();
        var adjustment = command.NewBalance - _state.State.Balance;

        if (adjustment == 0)
            throw new InvalidOperationException("New balance is the same as current balance");

        var entry = new AccountJournalEntry
        {
            Id = entryId,
            Timestamp = now,
            EntryType = JournalEntryType.Adjustment,
            Status = JournalEntryStatus.Posted,
            Amount = Math.Abs(adjustment),
            BalanceAfter = command.NewBalance,
            Description = command.Reason,
            PerformedBy = command.AdjustedBy,
            ApprovedBy = command.ApprovedBy,
            Notes = command.Notes
        };

        // Track as debit or credit based on adjustment direction and account type
        if (adjustment > 0)
        {
            if (IsDebitNormalBalance(_state.State.AccountType))
                _state.State.TotalDebits += Math.Abs(adjustment);
            else
                _state.State.TotalCredits += Math.Abs(adjustment);
        }
        else
        {
            if (IsDebitNormalBalance(_state.State.AccountType))
                _state.State.TotalCredits += Math.Abs(adjustment);
            else
                _state.State.TotalDebits += Math.Abs(adjustment);
        }

        _state.State.Balance = command.NewBalance;
        _state.State.TotalEntryCount++;
        _state.State.LastEntryAt = now;
        AddJournalEntry(entry);
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new PostEntryResult(entryId, Math.Abs(adjustment), command.NewBalance, JournalEntryType.Adjustment);
    }

    public async Task<ReverseEntryResult> ReverseEntryAsync(ReverseEntryCommand command)
    {
        EnsureExists();
        EnsureActive();

        var originalEntry = _state.State.JournalEntries.FirstOrDefault(e => e.Id == command.EntryId)
            ?? throw new InvalidOperationException("Entry not found");

        if (originalEntry.Status == JournalEntryStatus.Reversed)
            throw new InvalidOperationException("Entry has already been reversed");

        if (originalEntry.EntryType == JournalEntryType.Reversal)
            throw new InvalidOperationException("Cannot reverse a reversal entry");

        var now = DateTime.UtcNow;
        var reversalId = Guid.NewGuid();

        // Calculate reversal amount (opposite of original effect)
        decimal balanceChange;
        if (originalEntry.EntryType == JournalEntryType.Debit)
        {
            balanceChange = IsDebitNormalBalance(_state.State.AccountType)
                ? -originalEntry.Amount
                : originalEntry.Amount;
        }
        else if (originalEntry.EntryType == JournalEntryType.Credit)
        {
            balanceChange = IsDebitNormalBalance(_state.State.AccountType)
                ? originalEntry.Amount
                : -originalEntry.Amount;
        }
        else
        {
            // For adjustments, we need to determine the original direction
            var previousEntry = _state.State.JournalEntries
                .Where(e => e.Timestamp < originalEntry.Timestamp)
                .OrderByDescending(e => e.Timestamp)
                .FirstOrDefault();

            var previousBalance = previousEntry?.BalanceAfter ?? 0;
            var originalAdjustment = originalEntry.BalanceAfter - previousBalance;
            balanceChange = -originalAdjustment;
        }

        var newBalance = _state.State.Balance + balanceChange;

        var reversalEntry = new AccountJournalEntry
        {
            Id = reversalId,
            Timestamp = now,
            EntryType = JournalEntryType.Reversal,
            Status = JournalEntryStatus.Posted,
            Amount = originalEntry.Amount,
            BalanceAfter = newBalance,
            Description = $"Reversal: {command.Reason}",
            ReferenceNumber = originalEntry.ReferenceNumber,
            ReferenceType = originalEntry.ReferenceType,
            ReferenceId = originalEntry.ReferenceId,
            PerformedBy = command.ReversedBy,
            ReversedEntryId = command.EntryId
        };

        // Update original entry status
        var originalIndex = _state.State.JournalEntries.FindIndex(e => e.Id == command.EntryId);
        _state.State.JournalEntries[originalIndex] = originalEntry with
        {
            Status = JournalEntryStatus.Reversed,
            ReversalEntryId = reversalId
        };

        _state.State.Balance = newBalance;
        _state.State.TotalEntryCount++;
        _state.State.LastEntryAt = now;
        AddJournalEntry(reversalEntry);
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new ReverseEntryResult(reversalId, command.EntryId, originalEntry.Amount, newBalance);
    }

    public async Task<AccountPeriodSummary> ClosePeriodAsync(ClosePeriodCommand command)
    {
        EnsureExists();

        // Check if period matches current period
        if (command.Year != _state.State.CurrentPeriodYear ||
            command.Month != _state.State.CurrentPeriodMonth)
        {
            throw new InvalidOperationException(
                $"Cannot close period {command.Year}-{command.Month}. Current period is {_state.State.CurrentPeriodYear}-{_state.State.CurrentPeriodMonth}");
        }

        // Check if period summary already exists
        if (_state.State.PeriodSummaries.Any(s => s.Year == command.Year && s.Month == command.Month))
        {
            throw new InvalidOperationException($"Period {command.Year}-{command.Month} is already closed");
        }

        var now = DateTime.UtcNow;
        var periodStart = new DateTime(command.Year, command.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);

        // Get entries for this period
        var periodEntries = _state.State.JournalEntries
            .Where(e => e.Timestamp >= periodStart && e.Timestamp < periodEnd && e.Status == JournalEntryStatus.Posted)
            .ToList();

        // Find opening balance (balance at start of period)
        var priorEntries = _state.State.JournalEntries
            .Where(e => e.Timestamp < periodStart)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefault();

        var openingBalance = priorEntries?.BalanceAfter ?? 0;

        var periodDebits = periodEntries
            .Where(e => e.EntryType == JournalEntryType.Debit)
            .Sum(e => e.Amount);

        var periodCredits = periodEntries
            .Where(e => e.EntryType == JournalEntryType.Credit)
            .Sum(e => e.Amount);

        var closingBalance = command.ClosingBalance ?? _state.State.Balance;

        var summary = new AccountPeriodSummary
        {
            Year = command.Year,
            Month = command.Month,
            OpeningBalance = openingBalance,
            TotalDebits = periodDebits,
            TotalCredits = periodCredits,
            ClosingBalance = closingBalance,
            EntryCount = periodEntries.Count
        };

        _state.State.PeriodSummaries.Add(summary);

        // Record period close entry if there's any adjustment
        if (command.ClosingBalance.HasValue && command.ClosingBalance.Value != _state.State.Balance)
        {
            var adjustmentEntry = new AccountJournalEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = now,
                EntryType = JournalEntryType.PeriodClose,
                Status = JournalEntryStatus.Posted,
                Amount = Math.Abs(command.ClosingBalance.Value - _state.State.Balance),
                BalanceAfter = command.ClosingBalance.Value,
                Description = $"Period close adjustment for {command.Year}-{command.Month:D2}",
                PerformedBy = command.ClosedBy
            };

            _state.State.Balance = command.ClosingBalance.Value;
            _state.State.TotalEntryCount++;
            _state.State.LastEntryAt = now;
            AddJournalEntry(adjustmentEntry);
        }

        // Move to next period
        if (command.Month == 12)
        {
            _state.State.CurrentPeriodYear = command.Year + 1;
            _state.State.CurrentPeriodMonth = 1;
        }
        else
        {
            _state.State.CurrentPeriodMonth = command.Month + 1;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();

        return summary;
    }

    #endregion

    #region Queries

    public Task<decimal> GetBalanceAsync()
    {
        return Task.FromResult(_state.State.Balance);
    }

    public Task<AccountBalance> GetBalanceInfoAsync()
    {
        return Task.FromResult(new AccountBalance(
            _state.State.Balance,
            _state.State.TotalDebits,
            _state.State.TotalCredits,
            _state.State.LastEntryAt));
    }

    public Task<AccountSummary> GetSummaryAsync()
    {
        EnsureExists();

        return Task.FromResult(new AccountSummary(
            _state.State.Id,
            _state.State.AccountCode,
            _state.State.Name,
            _state.State.AccountType,
            _state.State.Balance,
            _state.State.TotalDebits,
            _state.State.TotalCredits,
            _state.State.TotalEntryCount,
            _state.State.LastEntryAt,
            _state.State.IsActive));
    }

    public Task<IReadOnlyList<AccountJournalEntry>> GetRecentEntriesAsync(int count = 50)
    {
        var entries = _state.State.JournalEntries
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToList();

        return Task.FromResult<IReadOnlyList<AccountJournalEntry>>(entries);
    }

    public Task<IReadOnlyList<AccountJournalEntry>> GetEntriesInRangeAsync(DateTime from, DateTime to)
    {
        var entries = _state.State.JournalEntries
            .Where(e => e.Timestamp >= from && e.Timestamp <= to)
            .OrderByDescending(e => e.Timestamp)
            .ToList();

        return Task.FromResult<IReadOnlyList<AccountJournalEntry>>(entries);
    }

    public Task<AccountJournalEntry?> GetEntryAsync(Guid entryId)
    {
        var entry = _state.State.JournalEntries.FirstOrDefault(e => e.Id == entryId);
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<AccountJournalEntry>> GetEntriesByReferenceAsync(string referenceType, Guid referenceId)
    {
        var entries = _state.State.JournalEntries
            .Where(e => e.ReferenceType == referenceType && e.ReferenceId == referenceId)
            .OrderByDescending(e => e.Timestamp)
            .ToList();

        return Task.FromResult<IReadOnlyList<AccountJournalEntry>>(entries);
    }

    public Task<IReadOnlyList<AccountPeriodSummary>> GetPeriodSummariesAsync(int? year = null)
    {
        var summaries = year.HasValue
            ? _state.State.PeriodSummaries.Where(s => s.Year == year.Value).ToList()
            : _state.State.PeriodSummaries.ToList();

        return Task.FromResult<IReadOnlyList<AccountPeriodSummary>>(summaries);
    }

    public Task<decimal> GetBalanceAtAsync(DateTime pointInTime)
    {
        // Find the most recent entry before or at the point in time
        var entry = _state.State.JournalEntries
            .Where(e => e.Timestamp <= pointInTime)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefault();

        return Task.FromResult(entry?.BalanceAfter ?? 0);
    }

    #endregion

    #region Private Helpers

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Account not found");
    }

    private void EnsureActive()
    {
        if (!_state.State.IsActive)
            throw new InvalidOperationException("Account is not active");
    }

    /// <summary>
    /// Determines if account type has a normal debit balance.
    /// Assets and Expenses have normal debit balances.
    /// Liabilities, Equity, and Revenue have normal credit balances.
    /// </summary>
    private static bool IsDebitNormalBalance(AccountType accountType)
    {
        return accountType == AccountType.Asset || accountType == AccountType.Expense;
    }

    private void AddJournalEntry(AccountJournalEntry entry)
    {
        _state.State.JournalEntries.Add(entry);

        // Keep only the most recent entries to prevent state bloat
        if (_state.State.JournalEntries.Count > MaxJournalEntries)
        {
            // Keep posted/reversed entries that aren't too old
            var cutoff = DateTime.UtcNow.AddMonths(-12);
            var toRemove = _state.State.JournalEntries
                .Where(e => e.Timestamp < cutoff)
                .OrderBy(e => e.Timestamp)
                .Take(_state.State.JournalEntries.Count - MaxJournalEntries)
                .ToList();

            foreach (var old in toRemove)
            {
                _state.State.JournalEntries.Remove(old);
            }
        }
    }

    #endregion
}
