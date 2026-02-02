using DarkVelocity.Host.Events.JournaledEvents;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.EventSourcing;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Orleans grain for managing accounts with full journal entry tracking.
/// Implements double-entry accounting with complete audit trail using event sourcing.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class AccountGrain : JournaledGrain<AccountState, IAccountJournaledEvent>, IAccountGrain
{
    private const int MaxJournalEntries = 500;

    protected override void TransitionState(AccountState state, IAccountJournaledEvent @event)
    {
        switch (@event)
        {
            case AccountCreatedJournaledEvent e:
                state.Id = e.AccountId;
                state.OrganizationId = e.OrganizationId;
                state.AccountCode = e.AccountCode;
                state.Name = e.AccountName;
                state.AccountType = Enum.Parse<AccountType>(e.AccountType);
                state.ParentAccountId = e.ParentAccountCode != null ? Guid.Empty : null; // Note: actual parent lookup done in CreateAsync
                state.IsActive = e.IsActive;
                state.CreatedAt = e.OccurredAt;
                break;

            case AccountDebitedJournaledEvent e:
                var debitBalanceChange = IsDebitNormalBalance(state.AccountType)
                    ? e.Amount
                    : -e.Amount;
                state.Balance = e.NewBalance;
                state.TotalDebits += e.Amount;
                state.TotalEntryCount++;
                state.LastEntryAt = e.OccurredAt;
                AddJournalEntryToState(state, new AccountJournalEntry
                {
                    Id = e.EntryId,
                    Timestamp = e.OccurredAt,
                    EntryType = JournalEntryType.Debit,
                    Status = JournalEntryStatus.Posted,
                    Amount = e.Amount,
                    BalanceAfter = e.NewBalance,
                    Description = e.Description,
                    ReferenceType = e.ReferenceType,
                    ReferenceId = e.ReferenceId,
                    PerformedBy = e.PerformedBy
                });
                break;

            case AccountCreditedJournaledEvent e:
                state.Balance = e.NewBalance;
                state.TotalCredits += e.Amount;
                state.TotalEntryCount++;
                state.LastEntryAt = e.OccurredAt;
                AddJournalEntryToState(state, new AccountJournalEntry
                {
                    Id = e.EntryId,
                    Timestamp = e.OccurredAt,
                    EntryType = JournalEntryType.Credit,
                    Status = JournalEntryStatus.Posted,
                    Amount = e.Amount,
                    BalanceAfter = e.NewBalance,
                    Description = e.Description,
                    ReferenceType = e.ReferenceType,
                    ReferenceId = e.ReferenceId,
                    PerformedBy = e.PerformedBy
                });
                break;

            case AccountEntryReversedJournaledEvent e:
                state.Balance = e.NewBalance;
                state.TotalEntryCount++;
                state.LastEntryAt = e.OccurredAt;

                // Update original entry status
                var originalIndex = state.JournalEntries.FindIndex(je => je.Id == e.OriginalEntryId);
                if (originalIndex >= 0)
                {
                    state.JournalEntries[originalIndex] = state.JournalEntries[originalIndex] with
                    {
                        Status = JournalEntryStatus.Reversed,
                        ReversalEntryId = e.ReversalEntryId
                    };
                }

                AddJournalEntryToState(state, new AccountJournalEntry
                {
                    Id = e.ReversalEntryId,
                    Timestamp = e.OccurredAt,
                    EntryType = JournalEntryType.Reversal,
                    Status = JournalEntryStatus.Posted,
                    Amount = e.Amount,
                    BalanceAfter = e.NewBalance,
                    Description = $"Reversal: {e.Reason}",
                    PerformedBy = e.ReversedBy,
                    ReversedEntryId = e.OriginalEntryId
                });
                break;

            case AccountUpdatedJournaledEvent e:
                if (e.AccountName != null)
                    state.Name = e.AccountName;
                if (e.IsActive.HasValue)
                    state.IsActive = e.IsActive.Value;
                state.LastModifiedAt = e.OccurredAt;
                break;

            case AccountPeriodClosedJournaledEvent e:
                var periodSummary = new AccountPeriodSummary
                {
                    Year = e.Year,
                    Month = e.Month,
                    ClosingBalance = e.ClosingBalance
                };
                state.PeriodSummaries.Add(periodSummary);
                state.LastEntryAt = e.OccurredAt;

                // Move to next period
                if (e.Month == 12)
                {
                    state.CurrentPeriodYear = e.Year + 1;
                    state.CurrentPeriodMonth = 1;
                }
                else
                {
                    state.CurrentPeriodMonth = e.Month + 1;
                }
                break;
        }
    }

    private static void AddJournalEntryToState(AccountState state, AccountJournalEntry entry)
    {
        state.JournalEntries.Add(entry);

        // Keep only the most recent entries to prevent state bloat
        if (state.JournalEntries.Count > MaxJournalEntries)
        {
            var cutoff = DateTime.UtcNow.AddMonths(-12);
            var toRemove = state.JournalEntries
                .Where(e => e.Timestamp < cutoff)
                .OrderBy(e => e.Timestamp)
                .Take(state.JournalEntries.Count - MaxJournalEntries)
                .ToList();

            foreach (var old in toRemove)
            {
                state.JournalEntries.Remove(old);
            }
        }
    }

    #region Lifecycle

    public async Task<CreateAccountResult> CreateAsync(CreateAccountCommand command)
    {
        if (State.Id != Guid.Empty)
            throw new InvalidOperationException("Account already exists");

        if (string.IsNullOrWhiteSpace(command.AccountCode))
            throw new ArgumentException("Account code is required", nameof(command));

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Account name is required", nameof(command));

        var now = DateTime.UtcNow;

        RaiseEvent(new AccountCreatedJournaledEvent
        {
            AccountId = command.AccountId,
            OrganizationId = command.OrganizationId,
            AccountCode = command.AccountCode,
            AccountName = command.Name,
            AccountType = command.AccountType.ToString(),
            ParentAccountCode = null, // Simplified for event
            IsActive = true,
            OccurredAt = now
        });
        await ConfirmEvents();

        // Set additional properties not in the event
        State.SubType = command.SubType;
        State.Description = command.Description;
        State.ParentAccountId = command.ParentAccountId;
        State.IsSystemAccount = command.IsSystemAccount;
        State.TaxCode = command.TaxCode;
        State.ExternalReference = command.ExternalReference;
        State.Currency = command.Currency;
        State.CreatedBy = command.CreatedBy;
        State.CurrentPeriodYear = now.Year;
        State.CurrentPeriodMonth = now.Month;

        // Record opening balance if provided
        if (command.OpeningBalance != 0)
        {
            var entryId = Guid.NewGuid();
            var newBalance = command.OpeningBalance;

            // Add opening entry
            var entry = new AccountJournalEntry
            {
                Id = entryId,
                Timestamp = now,
                EntryType = JournalEntryType.Opening,
                Status = JournalEntryStatus.Posted,
                Amount = Math.Abs(command.OpeningBalance),
                BalanceAfter = command.OpeningBalance,
                Description = "Opening balance",
                PerformedBy = command.CreatedBy
            };

            State.Balance = command.OpeningBalance;
            State.JournalEntries.Add(entry);
            State.TotalEntryCount = 1;
            State.LastEntryAt = now;

            // Track as debit or credit based on sign and account type
            if (IsDebitNormalBalance(State.AccountType))
            {
                if (command.OpeningBalance > 0)
                    State.TotalDebits = command.OpeningBalance;
                else
                    State.TotalCredits = Math.Abs(command.OpeningBalance);
            }
            else
            {
                if (command.OpeningBalance > 0)
                    State.TotalCredits = command.OpeningBalance;
                else
                    State.TotalDebits = Math.Abs(command.OpeningBalance);
            }
        }

        return new CreateAccountResult(
            State.Id,
            State.AccountCode,
            State.Balance);
    }

    public Task<AccountState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(State.Id != Guid.Empty);
    }

    public async Task UpdateAsync(UpdateAccountCommand command)
    {
        EnsureExists();

        RaiseEvent(new AccountUpdatedJournaledEvent
        {
            AccountId = State.Id,
            AccountName = command.Name,
            IsActive = null,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Set additional properties not in the event
        if (command.Description != null)
            State.Description = command.Description;
        if (command.SubType != null)
            State.SubType = command.SubType;
        if (command.TaxCode != null)
            State.TaxCode = command.TaxCode;
        if (command.ExternalReference != null)
            State.ExternalReference = command.ExternalReference;
        if (command.ParentAccountId.HasValue)
            State.ParentAccountId = command.ParentAccountId;
        State.LastModifiedBy = command.UpdatedBy;
    }

    public async Task ActivateAsync(Guid activatedBy)
    {
        EnsureExists();

        if (State.IsActive)
            throw new InvalidOperationException("Account is already active");

        RaiseEvent(new AccountUpdatedJournaledEvent
        {
            AccountId = State.Id,
            IsActive = true,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        State.LastModifiedBy = activatedBy;
    }

    public async Task DeactivateAsync(Guid deactivatedBy)
    {
        EnsureExists();

        if (!State.IsActive)
            throw new InvalidOperationException("Account is already deactivated");

        if (State.IsSystemAccount)
            throw new InvalidOperationException("System accounts cannot be deactivated");

        RaiseEvent(new AccountUpdatedJournaledEvent
        {
            AccountId = State.Id,
            IsActive = false,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        State.LastModifiedBy = deactivatedBy;
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
        var balanceChange = IsDebitNormalBalance(State.AccountType)
            ? command.Amount
            : -command.Amount;

        var newBalance = State.Balance + balanceChange;

        RaiseEvent(new AccountDebitedJournaledEvent
        {
            AccountId = State.Id,
            EntryId = entryId,
            Amount = command.Amount,
            NewBalance = newBalance,
            Description = command.Description,
            ReferenceType = command.ReferenceType,
            ReferenceId = command.ReferenceId,
            PerformedBy = command.PerformedBy,
            OccurredAt = now
        });
        await ConfirmEvents();

        // Set additional properties on the journal entry (not stored in event)
        var lastEntry = State.JournalEntries.LastOrDefault();
        if (lastEntry != null && lastEntry.Id == entryId)
        {
            var index = State.JournalEntries.Count - 1;
            State.JournalEntries[index] = lastEntry with
            {
                ReferenceNumber = command.ReferenceNumber,
                AccountingJournalEntryId = command.AccountingJournalEntryId,
                CostCenterId = command.CostCenterId,
                Notes = command.Notes
            };
        }

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
        var balanceChange = IsDebitNormalBalance(State.AccountType)
            ? -command.Amount
            : command.Amount;

        var newBalance = State.Balance + balanceChange;

        RaiseEvent(new AccountCreditedJournaledEvent
        {
            AccountId = State.Id,
            EntryId = entryId,
            Amount = command.Amount,
            NewBalance = newBalance,
            Description = command.Description,
            ReferenceType = command.ReferenceType,
            ReferenceId = command.ReferenceId,
            PerformedBy = command.PerformedBy,
            OccurredAt = now
        });
        await ConfirmEvents();

        // Set additional properties on the journal entry (not stored in event)
        var lastEntry = State.JournalEntries.LastOrDefault();
        if (lastEntry != null && lastEntry.Id == entryId)
        {
            var index = State.JournalEntries.Count - 1;
            State.JournalEntries[index] = lastEntry with
            {
                ReferenceNumber = command.ReferenceNumber,
                AccountingJournalEntryId = command.AccountingJournalEntryId,
                CostCenterId = command.CostCenterId,
                Notes = command.Notes
            };
        }

        return new PostEntryResult(entryId, command.Amount, newBalance, JournalEntryType.Credit);
    }

    public async Task<PostEntryResult> AdjustBalanceAsync(AdjustBalanceCommand command)
    {
        EnsureExists();
        EnsureActive();

        var now = DateTime.UtcNow;
        var entryId = Guid.NewGuid();
        var adjustment = command.NewBalance - State.Balance;

        if (adjustment == 0)
            throw new InvalidOperationException("New balance is the same as current balance");

        // Use debit or credit event based on adjustment direction
        if ((adjustment > 0 && IsDebitNormalBalance(State.AccountType)) ||
            (adjustment < 0 && !IsDebitNormalBalance(State.AccountType)))
        {
            RaiseEvent(new AccountDebitedJournaledEvent
            {
                AccountId = State.Id,
                EntryId = entryId,
                Amount = Math.Abs(adjustment),
                NewBalance = command.NewBalance,
                Description = command.Reason,
                PerformedBy = command.AdjustedBy,
                OccurredAt = now
            });
        }
        else
        {
            RaiseEvent(new AccountCreditedJournaledEvent
            {
                AccountId = State.Id,
                EntryId = entryId,
                Amount = Math.Abs(adjustment),
                NewBalance = command.NewBalance,
                Description = command.Reason,
                PerformedBy = command.AdjustedBy,
                OccurredAt = now
            });
        }
        await ConfirmEvents();

        // Update the last entry to be an adjustment type
        var lastEntry = State.JournalEntries.LastOrDefault();
        if (lastEntry != null && lastEntry.Id == entryId)
        {
            var index = State.JournalEntries.Count - 1;
            State.JournalEntries[index] = lastEntry with
            {
                EntryType = JournalEntryType.Adjustment,
                ApprovedBy = command.ApprovedBy,
                Notes = command.Notes
            };
        }

        return new PostEntryResult(entryId, Math.Abs(adjustment), command.NewBalance, JournalEntryType.Adjustment);
    }

    public async Task<ReverseEntryResult> ReverseEntryAsync(ReverseEntryCommand command)
    {
        EnsureExists();
        EnsureActive();

        var originalEntry = State.JournalEntries.FirstOrDefault(e => e.Id == command.EntryId)
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
            balanceChange = IsDebitNormalBalance(State.AccountType)
                ? -originalEntry.Amount
                : originalEntry.Amount;
        }
        else if (originalEntry.EntryType == JournalEntryType.Credit)
        {
            balanceChange = IsDebitNormalBalance(State.AccountType)
                ? originalEntry.Amount
                : -originalEntry.Amount;
        }
        else
        {
            // For adjustments, we need to determine the original direction
            var previousEntry = State.JournalEntries
                .Where(e => e.Timestamp < originalEntry.Timestamp)
                .OrderByDescending(e => e.Timestamp)
                .FirstOrDefault();

            var previousBalance = previousEntry?.BalanceAfter ?? 0;
            var originalAdjustment = originalEntry.BalanceAfter - previousBalance;
            balanceChange = -originalAdjustment;
        }

        var newBalance = State.Balance + balanceChange;

        RaiseEvent(new AccountEntryReversedJournaledEvent
        {
            AccountId = State.Id,
            OriginalEntryId = command.EntryId,
            ReversalEntryId = reversalId,
            Amount = originalEntry.Amount,
            NewBalance = newBalance,
            Reason = command.Reason,
            ReversedBy = command.ReversedBy,
            OccurredAt = now
        });
        await ConfirmEvents();

        return new ReverseEntryResult(reversalId, command.EntryId, originalEntry.Amount, newBalance);
    }

    public async Task<AccountPeriodSummary> ClosePeriodAsync(ClosePeriodCommand command)
    {
        EnsureExists();

        // Check if period matches current period
        if (command.Year != State.CurrentPeriodYear ||
            command.Month != State.CurrentPeriodMonth)
        {
            throw new InvalidOperationException(
                $"Cannot close period {command.Year}-{command.Month}. Current period is {State.CurrentPeriodYear}-{State.CurrentPeriodMonth}");
        }

        // Check if period summary already exists
        if (State.PeriodSummaries.Any(s => s.Year == command.Year && s.Month == command.Month))
        {
            throw new InvalidOperationException($"Period {command.Year}-{command.Month} is already closed");
        }

        var now = DateTime.UtcNow;
        var periodStart = new DateTime(command.Year, command.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);

        // Get entries for this period
        var periodEntries = State.JournalEntries
            .Where(e => e.Timestamp >= periodStart && e.Timestamp < periodEnd && e.Status == JournalEntryStatus.Posted)
            .ToList();

        // Find opening balance (balance at start of period)
        var priorEntries = State.JournalEntries
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

        var closingBalance = command.ClosingBalance ?? State.Balance;

        // Entry count excludes Opening entries (not real activity)
        var activityEntries = periodEntries.Count(e => e.EntryType != JournalEntryType.Opening);

        RaiseEvent(new AccountPeriodClosedJournaledEvent
        {
            AccountId = State.Id,
            Year = command.Year,
            Month = command.Month,
            ClosingBalance = closingBalance,
            ClosedBy = command.ClosedBy,
            OccurredAt = now
        });
        await ConfirmEvents();

        // Update the period summary with full details
        var lastSummary = State.PeriodSummaries.LastOrDefault();
        if (lastSummary != null && lastSummary.Year == command.Year && lastSummary.Month == command.Month)
        {
            var index = State.PeriodSummaries.Count - 1;
            State.PeriodSummaries[index] = lastSummary with
            {
                OpeningBalance = openingBalance,
                TotalDebits = periodDebits,
                TotalCredits = periodCredits,
                EntryCount = activityEntries
            };
        }

        // Handle closing balance adjustment
        if (command.ClosingBalance.HasValue && command.ClosingBalance.Value != State.Balance)
        {
            State.Balance = command.ClosingBalance.Value;
        }

        return State.PeriodSummaries.Last(s => s.Year == command.Year && s.Month == command.Month);
    }

    #endregion

    #region Queries

    public Task<decimal> GetBalanceAsync()
    {
        return Task.FromResult(State.Balance);
    }

    public Task<AccountBalance> GetBalanceInfoAsync()
    {
        return Task.FromResult(new AccountBalance(
            State.Balance,
            State.TotalDebits,
            State.TotalCredits,
            State.LastEntryAt));
    }

    public Task<AccountSummary> GetSummaryAsync()
    {
        EnsureExists();

        return Task.FromResult(new AccountSummary(
            State.Id,
            State.AccountCode,
            State.Name,
            State.AccountType,
            State.Balance,
            State.TotalDebits,
            State.TotalCredits,
            State.TotalEntryCount,
            State.LastEntryAt,
            State.IsActive));
    }

    public Task<IReadOnlyList<AccountJournalEntry>> GetRecentEntriesAsync(int count = 50)
    {
        var entries = State.JournalEntries
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToList();

        return Task.FromResult<IReadOnlyList<AccountJournalEntry>>(entries);
    }

    public Task<IReadOnlyList<AccountJournalEntry>> GetEntriesInRangeAsync(DateTime from, DateTime to)
    {
        var entries = State.JournalEntries
            .Where(e => e.Timestamp >= from && e.Timestamp <= to)
            .OrderByDescending(e => e.Timestamp)
            .ToList();

        return Task.FromResult<IReadOnlyList<AccountJournalEntry>>(entries);
    }

    public Task<AccountJournalEntry?> GetEntryAsync(Guid entryId)
    {
        var entry = State.JournalEntries.FirstOrDefault(e => e.Id == entryId);
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<AccountJournalEntry>> GetEntriesByReferenceAsync(string referenceType, Guid referenceId)
    {
        var entries = State.JournalEntries
            .Where(e => e.ReferenceType == referenceType && e.ReferenceId == referenceId)
            .OrderByDescending(e => e.Timestamp)
            .ToList();

        return Task.FromResult<IReadOnlyList<AccountJournalEntry>>(entries);
    }

    public Task<IReadOnlyList<AccountPeriodSummary>> GetPeriodSummariesAsync(int? year = null)
    {
        var summaries = year.HasValue
            ? State.PeriodSummaries.Where(s => s.Year == year.Value).ToList()
            : State.PeriodSummaries.ToList();

        return Task.FromResult<IReadOnlyList<AccountPeriodSummary>>(summaries);
    }

    public Task<decimal> GetBalanceAtAsync(DateTime pointInTime)
    {
        // Find the most recent entry before or at the point in time
        var entry = State.JournalEntries
            .Where(e => e.Timestamp <= pointInTime)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefault();

        return Task.FromResult(entry?.BalanceAfter ?? 0);
    }

    #endregion

    #region Private Helpers

    private void EnsureExists()
    {
        if (State.Id == Guid.Empty)
            throw new InvalidOperationException("Account not found");
    }

    private void EnsureActive()
    {
        if (!State.IsActive)
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

    #endregion
}
