namespace DarkVelocity.Host.State;

public enum CustomerAccountStatus
{
    Active,
    Suspended,
    Closed
}

public enum AccountTransactionType
{
    Charge,
    Payment,
    Credit,
    Refund
}

[GenerateSerializer]
public record AccountTransaction
{
    [Id(0)] public Guid TransactionId { get; init; }
    [Id(1)] public AccountTransactionType Type { get; init; }
    [Id(2)] public decimal Amount { get; init; }
    [Id(3)] public decimal BalanceAfter { get; init; }
    [Id(4)] public string Description { get; init; } = "";
    [Id(5)] public Guid? OrderId { get; init; }
    [Id(6)] public string? Reference { get; init; }
    [Id(7)] public PaymentMethod? PaymentMethod { get; init; }
    [Id(8)] public Guid ProcessedBy { get; init; }
    [Id(9)] public DateTime TransactionDate { get; init; }
}

[GenerateSerializer]
public record AccountStatement
{
    [Id(0)] public Guid StatementId { get; init; }
    [Id(1)] public DateOnly PeriodStart { get; init; }
    [Id(2)] public DateOnly PeriodEnd { get; init; }
    [Id(3)] public decimal OpeningBalance { get; init; }
    [Id(4)] public decimal ClosingBalance { get; init; }
    [Id(5)] public decimal TotalCharges { get; init; }
    [Id(6)] public decimal TotalPayments { get; init; }
    [Id(7)] public int TransactionCount { get; init; }
    [Id(8)] public DateTime GeneratedAt { get; init; }
}

[GenerateSerializer]
public sealed class CustomerAccountState
{
    [Id(0)] public Guid CustomerId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public CustomerAccountStatus Status { get; set; } = CustomerAccountStatus.Active;

    // Balance and limits
    [Id(3)] public decimal Balance { get; set; } // Positive = customer owes, Negative = credit
    [Id(4)] public decimal CreditLimit { get; set; }
    [Id(5)] public int PaymentTermsDays { get; set; } = 30;

    // Transaction history
    [Id(6)] public List<AccountTransaction> Transactions { get; set; } = [];
    [Id(7)] public List<AccountStatement> Statements { get; set; } = [];

    // Statistics
    [Id(8)] public decimal TotalCharges { get; set; }
    [Id(9)] public decimal TotalPayments { get; set; }
    [Id(10)] public int ChargeCount { get; set; }
    [Id(11)] public int PaymentCount { get; set; }

    // Timestamps
    [Id(12)] public DateTime OpenedAt { get; set; }
    [Id(13)] public DateTime? SuspendedAt { get; set; }
    [Id(14)] public DateTime? ClosedAt { get; set; }
    [Id(15)] public DateTime? LastChargeAt { get; set; }
    [Id(16)] public DateTime? LastPaymentAt { get; set; }

    // Audit
    [Id(17)] public Guid OpenedBy { get; set; }
    [Id(18)] public Guid? SuspendedBy { get; set; }
    [Id(19)] public Guid? ClosedBy { get; set; }
    [Id(20)] public string? SuspensionReason { get; set; }
    [Id(21)] public string? ClosureReason { get; set; }

    /// <summary>
    /// Available credit remaining on the account.
    /// </summary>
    public decimal AvailableCredit => Math.Max(0, CreditLimit - Balance);

    /// <summary>
    /// Checks if a charge of the given amount would exceed the credit limit.
    /// </summary>
    public bool WouldExceedCreditLimit(decimal amount) => (Balance + amount) > CreditLimit;

    /// <summary>
    /// Gets oldest unpaid charge date for aging.
    /// </summary>
    public DateTime? OldestUnpaidChargeDate
    {
        get
        {
            if (Balance <= 0) return null;
            return Transactions
                .Where(t => t.Type == AccountTransactionType.Charge)
                .OrderBy(t => t.TransactionDate)
                .FirstOrDefault()?.TransactionDate;
        }
    }

    /// <summary>
    /// Gets account age in days.
    /// </summary>
    public int AccountAgeDays => (DateTime.UtcNow - OpenedAt).Days;
}
