using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record OpenCustomerAccountCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] decimal CreditLimit,
    [property: Id(2)] int PaymentTermsDays,
    [property: Id(3)] Guid OpenedBy);

[GenerateSerializer]
public record ChargeAccountCommand(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] decimal Amount,
    [property: Id(2)] string Description,
    [property: Id(3)] Guid ChargedBy);

[GenerateSerializer]
public record ApplyPaymentCommand(
    [property: Id(0)] decimal Amount,
    [property: Id(1)] PaymentMethod Method,
    [property: Id(2)] string? Reference,
    [property: Id(3)] Guid AppliedBy);

[GenerateSerializer]
public record ApplyCreditCommand(
    [property: Id(0)] decimal Amount,
    [property: Id(1)] string Reason,
    [property: Id(2)] Guid AppliedBy);

[GenerateSerializer]
public record GenerateStatementCommand(
    [property: Id(0)] DateOnly PeriodStart,
    [property: Id(1)] DateOnly PeriodEnd);

[GenerateSerializer]
public record AccountOpenedResult(
    [property: Id(0)] Guid CustomerId,
    [property: Id(1)] decimal CreditLimit,
    [property: Id(2)] DateTime OpenedAt);

[GenerateSerializer]
public record ChargeResult(
    [property: Id(0)] Guid TransactionId,
    [property: Id(1)] decimal NewBalance,
    [property: Id(2)] decimal AvailableCredit);

[GenerateSerializer]
public record PaymentAppliedResult(
    [property: Id(0)] Guid TransactionId,
    [property: Id(1)] decimal NewBalance,
    [property: Id(2)] decimal PaymentAmount);

[GenerateSerializer]
public record CustomerAccountSummary
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public CustomerAccountStatus Status { get; init; }
    [Id(2)] public decimal Balance { get; init; }
    [Id(3)] public decimal CreditLimit { get; init; }
    [Id(4)] public decimal AvailableCredit { get; init; }
    [Id(5)] public int PaymentTermsDays { get; init; }
    [Id(6)] public decimal TotalCharges { get; init; }
    [Id(7)] public decimal TotalPayments { get; init; }
    [Id(8)] public DateTime? LastChargeAt { get; init; }
    [Id(9)] public DateTime? LastPaymentAt { get; init; }
    [Id(10)] public int AccountAgeDays { get; init; }
}

public interface ICustomerAccountGrain : IGrainWithStringKey
{
    /// <summary>
    /// Opens a new customer house account.
    /// </summary>
    Task<AccountOpenedResult> OpenAsync(OpenCustomerAccountCommand command);

    /// <summary>
    /// Gets the current state of the account.
    /// </summary>
    Task<CustomerAccountState> GetStateAsync();

    /// <summary>
    /// Charges an order to the account.
    /// </summary>
    Task<ChargeResult> ChargeAsync(ChargeAccountCommand command);

    /// <summary>
    /// Applies a payment to reduce the balance.
    /// </summary>
    Task<PaymentAppliedResult> ApplyPaymentAsync(ApplyPaymentCommand command);

    /// <summary>
    /// Applies a credit (adjustment) to the account.
    /// </summary>
    Task ApplyCreditAsync(ApplyCreditCommand command);

    /// <summary>
    /// Changes the credit limit.
    /// </summary>
    Task ChangeCreditLimitAsync(decimal newLimit, string reason, Guid changedBy);

    /// <summary>
    /// Suspends the account (no new charges allowed).
    /// </summary>
    Task SuspendAsync(string reason, Guid suspendedBy);

    /// <summary>
    /// Reactivates a suspended account.
    /// </summary>
    Task ReactivateAsync(Guid reactivatedBy);

    /// <summary>
    /// Closes the account.
    /// </summary>
    Task CloseAsync(string reason, Guid closedBy);

    /// <summary>
    /// Generates an account statement for a period.
    /// </summary>
    Task<AccountStatement> GenerateStatementAsync(GenerateStatementCommand command);

    /// <summary>
    /// Gets recent transactions.
    /// </summary>
    Task<List<AccountTransaction>> GetTransactionsAsync(int limit = 50);

    /// <summary>
    /// Gets account summary.
    /// </summary>
    Task<CustomerAccountSummary> GetSummaryAsync();

    /// <summary>
    /// Gets current balance.
    /// </summary>
    Task<decimal> GetBalanceAsync();

    /// <summary>
    /// Gets available credit.
    /// </summary>
    Task<decimal> GetAvailableCreditAsync();

    /// <summary>
    /// Checks if account can be charged the given amount.
    /// </summary>
    Task<bool> CanChargeAsync(decimal amount);

    /// <summary>
    /// Checks if account exists.
    /// </summary>
    Task<bool> ExistsAsync();

    /// <summary>
    /// Gets account status.
    /// </summary>
    Task<CustomerAccountStatus> GetStatusAsync();
}
