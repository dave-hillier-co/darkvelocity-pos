using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class CustomerAccountGrain : JournaledGrain<CustomerAccountState, ICustomerAccountEvent>, ICustomerAccountGrain
{
    private readonly IGrainFactory _grainFactory;

    public CustomerAccountGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    protected override void TransitionState(CustomerAccountState state, ICustomerAccountEvent @event)
    {
        switch (@event)
        {
            case CustomerAccountOpened e:
                state.CustomerId = e.CustomerId;
                state.OrganizationId = e.OrganizationId;
                state.CreditLimit = e.CreditLimit;
                state.PaymentTermsDays = e.PaymentTermsDays;
                state.OpenedBy = e.OpenedBy;
                state.OpenedAt = e.OccurredAt;
                state.Status = CustomerAccountStatus.Active;
                state.Balance = 0;
                break;

            case CustomerAccountCharged e:
                var chargeTransaction = new AccountTransaction
                {
                    TransactionId = e.TransactionId,
                    Type = AccountTransactionType.Charge,
                    Amount = e.Amount,
                    BalanceAfter = state.Balance + e.Amount,
                    Description = e.Description,
                    OrderId = e.OrderId,
                    ProcessedBy = e.ChargedBy,
                    TransactionDate = e.OccurredAt
                };
                state.Transactions.Add(chargeTransaction);
                state.Balance += e.Amount;
                state.TotalCharges += e.Amount;
                state.ChargeCount++;
                state.LastChargeAt = e.OccurredAt;
                break;

            case CustomerAccountPaymentApplied e:
                var paymentTransaction = new AccountTransaction
                {
                    TransactionId = e.TransactionId,
                    Type = AccountTransactionType.Payment,
                    Amount = e.Amount,
                    BalanceAfter = state.Balance - e.Amount,
                    Description = $"Payment via {e.Method}",
                    Reference = e.Reference,
                    PaymentMethod = e.Method,
                    ProcessedBy = e.AppliedBy,
                    TransactionDate = e.OccurredAt
                };
                state.Transactions.Add(paymentTransaction);
                state.Balance -= e.Amount;
                state.TotalPayments += e.Amount;
                state.PaymentCount++;
                state.LastPaymentAt = e.OccurredAt;
                break;

            case CustomerAccountCreditApplied e:
                var creditTransaction = new AccountTransaction
                {
                    TransactionId = e.TransactionId,
                    Type = AccountTransactionType.Credit,
                    Amount = e.Amount,
                    BalanceAfter = state.Balance - e.Amount,
                    Description = e.Reason,
                    ProcessedBy = e.AppliedBy,
                    TransactionDate = e.OccurredAt
                };
                state.Transactions.Add(creditTransaction);
                state.Balance -= e.Amount;
                break;

            case CustomerAccountCreditLimitChanged e:
                state.CreditLimit = e.NewLimit;
                break;

            case CustomerAccountSuspended e:
                state.Status = CustomerAccountStatus.Suspended;
                state.SuspendedBy = e.SuspendedBy;
                state.SuspendedAt = e.OccurredAt;
                state.SuspensionReason = e.Reason;
                break;

            case CustomerAccountReactivated:
                state.Status = CustomerAccountStatus.Active;
                state.SuspendedBy = null;
                state.SuspendedAt = null;
                state.SuspensionReason = null;
                break;

            case CustomerAccountClosed e:
                state.Status = CustomerAccountStatus.Closed;
                state.ClosedBy = e.ClosedBy;
                state.ClosedAt = e.OccurredAt;
                state.ClosureReason = e.Reason;
                break;

            case CustomerAccountStatementGenerated e:
                state.Statements.Add(new AccountStatement
                {
                    StatementId = e.StatementId,
                    PeriodStart = e.PeriodStart,
                    PeriodEnd = e.PeriodEnd,
                    OpeningBalance = e.OpeningBalance,
                    ClosingBalance = e.ClosingBalance,
                    TotalCharges = e.TotalCharges,
                    TotalPayments = e.TotalPayments,
                    GeneratedAt = e.OccurredAt
                });
                break;
        }
    }

    public async Task<AccountOpenedResult> OpenAsync(OpenCustomerAccountCommand command)
    {
        if (State.CustomerId != Guid.Empty)
            throw new InvalidOperationException("Account already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, customerId) = GrainKeys.ParseOrgEntity(key);
        var now = DateTime.UtcNow;

        RaiseEvent(new CustomerAccountOpened
        {
            CustomerId = customerId,
            OrganizationId = command.OrganizationId,
            CreditLimit = command.CreditLimit,
            PaymentTermsDays = command.PaymentTermsDays,
            OpenedBy = command.OpenedBy,
            OccurredAt = now
        });
        await ConfirmEvents();

        return new AccountOpenedResult(customerId, command.CreditLimit, now);
    }

    public Task<CustomerAccountState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    public async Task<ChargeResult> ChargeAsync(ChargeAccountCommand command)
    {
        EnsureExists();
        EnsureActive();

        if (command.Amount <= 0)
            throw new InvalidOperationException("Charge amount must be positive");

        if (State.WouldExceedCreditLimit(command.Amount))
            throw new InvalidOperationException($"Charge would exceed credit limit. Available: {State.AvailableCredit:C}");

        var transactionId = Guid.NewGuid();

        RaiseEvent(new CustomerAccountCharged
        {
            CustomerId = State.CustomerId,
            TransactionId = transactionId,
            OrderId = command.OrderId,
            Amount = command.Amount,
            Description = command.Description,
            ChargedBy = command.ChargedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        return new ChargeResult(transactionId, State.Balance, State.AvailableCredit);
    }

    public async Task<PaymentAppliedResult> ApplyPaymentAsync(ApplyPaymentCommand command)
    {
        EnsureExists();

        if (State.Status == CustomerAccountStatus.Closed)
            throw new InvalidOperationException("Cannot apply payment to closed account");

        if (command.Amount <= 0)
            throw new InvalidOperationException("Payment amount must be positive");

        var transactionId = Guid.NewGuid();

        RaiseEvent(new CustomerAccountPaymentApplied
        {
            CustomerId = State.CustomerId,
            TransactionId = transactionId,
            Amount = command.Amount,
            Method = command.Method,
            Reference = command.Reference,
            AppliedBy = command.AppliedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        return new PaymentAppliedResult(transactionId, State.Balance, command.Amount);
    }

    public async Task ApplyCreditAsync(ApplyCreditCommand command)
    {
        EnsureExists();

        if (State.Status == CustomerAccountStatus.Closed)
            throw new InvalidOperationException("Cannot apply credit to closed account");

        if (command.Amount <= 0)
            throw new InvalidOperationException("Credit amount must be positive");

        RaiseEvent(new CustomerAccountCreditApplied
        {
            CustomerId = State.CustomerId,
            TransactionId = Guid.NewGuid(),
            Amount = command.Amount,
            Reason = command.Reason,
            AppliedBy = command.AppliedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task ChangeCreditLimitAsync(decimal newLimit, string reason, Guid changedBy)
    {
        EnsureExists();

        if (newLimit < 0)
            throw new InvalidOperationException("Credit limit cannot be negative");

        if (newLimit < State.Balance)
            throw new InvalidOperationException($"New limit ({newLimit:C}) cannot be less than current balance ({State.Balance:C})");

        RaiseEvent(new CustomerAccountCreditLimitChanged
        {
            CustomerId = State.CustomerId,
            OldLimit = State.CreditLimit,
            NewLimit = newLimit,
            Reason = reason,
            ChangedBy = changedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task SuspendAsync(string reason, Guid suspendedBy)
    {
        EnsureExists();

        if (State.Status != CustomerAccountStatus.Active)
            throw new InvalidOperationException($"Cannot suspend account with status: {State.Status}");

        RaiseEvent(new CustomerAccountSuspended
        {
            CustomerId = State.CustomerId,
            Reason = reason,
            SuspendedBy = suspendedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task ReactivateAsync(Guid reactivatedBy)
    {
        EnsureExists();

        if (State.Status != CustomerAccountStatus.Suspended)
            throw new InvalidOperationException($"Cannot reactivate account with status: {State.Status}");

        RaiseEvent(new CustomerAccountReactivated
        {
            CustomerId = State.CustomerId,
            ReactivatedBy = reactivatedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task CloseAsync(string reason, Guid closedBy)
    {
        EnsureExists();

        if (State.Status == CustomerAccountStatus.Closed)
            throw new InvalidOperationException("Account is already closed");

        if (State.Balance > 0)
            throw new InvalidOperationException($"Cannot close account with outstanding balance: {State.Balance:C}");

        RaiseEvent(new CustomerAccountClosed
        {
            CustomerId = State.CustomerId,
            Reason = reason,
            ClosedBy = closedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task<AccountStatement> GenerateStatementAsync(GenerateStatementCommand command)
    {
        EnsureExists();

        var periodStart = command.PeriodStart.ToDateTime(TimeOnly.MinValue);
        var periodEnd = command.PeriodEnd.ToDateTime(TimeOnly.MaxValue);

        var periodTransactions = State.Transactions
            .Where(t => t.TransactionDate >= periodStart && t.TransactionDate <= periodEnd)
            .OrderBy(t => t.TransactionDate)
            .ToList();

        // Calculate opening balance (balance before period start)
        var transactionsBeforePeriod = State.Transactions
            .Where(t => t.TransactionDate < periodStart)
            .ToList();

        var openingBalance = transactionsBeforePeriod.Any()
            ? transactionsBeforePeriod.Last().BalanceAfter
            : 0;

        var totalCharges = periodTransactions
            .Where(t => t.Type == AccountTransactionType.Charge)
            .Sum(t => t.Amount);

        var totalPayments = periodTransactions
            .Where(t => t.Type == AccountTransactionType.Payment || t.Type == AccountTransactionType.Credit)
            .Sum(t => t.Amount);

        var closingBalance = periodTransactions.Any()
            ? periodTransactions.Last().BalanceAfter
            : openingBalance;

        var statementId = Guid.NewGuid();

        RaiseEvent(new CustomerAccountStatementGenerated
        {
            CustomerId = State.CustomerId,
            StatementId = statementId,
            PeriodStart = command.PeriodStart,
            PeriodEnd = command.PeriodEnd,
            OpeningBalance = openingBalance,
            ClosingBalance = closingBalance,
            TotalCharges = totalCharges,
            TotalPayments = totalPayments,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        return new AccountStatement
        {
            StatementId = statementId,
            PeriodStart = command.PeriodStart,
            PeriodEnd = command.PeriodEnd,
            OpeningBalance = openingBalance,
            ClosingBalance = closingBalance,
            TotalCharges = totalCharges,
            TotalPayments = totalPayments,
            TransactionCount = periodTransactions.Count,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public Task<List<AccountTransaction>> GetTransactionsAsync(int limit = 50)
    {
        EnsureExists();
        var transactions = State.Transactions
            .OrderByDescending(t => t.TransactionDate)
            .Take(limit)
            .ToList();
        return Task.FromResult(transactions);
    }

    public Task<CustomerAccountSummary> GetSummaryAsync()
    {
        EnsureExists();
        return Task.FromResult(new CustomerAccountSummary
        {
            CustomerId = State.CustomerId,
            Status = State.Status,
            Balance = State.Balance,
            CreditLimit = State.CreditLimit,
            AvailableCredit = State.AvailableCredit,
            PaymentTermsDays = State.PaymentTermsDays,
            TotalCharges = State.TotalCharges,
            TotalPayments = State.TotalPayments,
            LastChargeAt = State.LastChargeAt,
            LastPaymentAt = State.LastPaymentAt,
            AccountAgeDays = State.AccountAgeDays
        });
    }

    public Task<decimal> GetBalanceAsync()
    {
        EnsureExists();
        return Task.FromResult(State.Balance);
    }

    public Task<decimal> GetAvailableCreditAsync()
    {
        EnsureExists();
        return Task.FromResult(State.AvailableCredit);
    }

    public Task<bool> CanChargeAsync(decimal amount)
    {
        if (State.CustomerId == Guid.Empty)
            return Task.FromResult(false);

        if (State.Status != CustomerAccountStatus.Active)
            return Task.FromResult(false);

        return Task.FromResult(!State.WouldExceedCreditLimit(amount));
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.CustomerId != Guid.Empty);
    public Task<CustomerAccountStatus> GetStatusAsync() => Task.FromResult(State.Status);

    private void EnsureExists()
    {
        if (State.CustomerId == Guid.Empty)
            throw new InvalidOperationException("Account does not exist");
    }

    private void EnsureActive()
    {
        if (State.Status != CustomerAccountStatus.Active)
            throw new InvalidOperationException($"Account is not active. Status: {State.Status}");
    }
}
