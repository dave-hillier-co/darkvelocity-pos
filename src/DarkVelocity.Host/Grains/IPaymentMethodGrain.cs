namespace DarkVelocity.Host.Grains;

public enum PaymentMethodType
{
    Card,
    CardPresent,
    BankAccount,
    Wallet
}

[GenerateSerializer]
public record CreatePaymentMethodCommand(
    [property: Id(0)] Guid AccountId,
    [property: Id(1)] PaymentMethodType Type,
    [property: Id(2)] CardDetails? Card = null,
    [property: Id(3)] BankAccountDetails? BankAccount = null,
    [property: Id(4)] BillingDetails? BillingDetails = null,
    [property: Id(5)] Dictionary<string, string>? Metadata = null);

[GenerateSerializer]
public record CardDetails(
    [property: Id(0)] string Number,
    [property: Id(1)] int ExpMonth,
    [property: Id(2)] int ExpYear,
    [property: Id(3)] string? Cvc = null,
    [property: Id(4)] string? CardholderName = null);

[GenerateSerializer]
public record BankAccountDetails(
    [property: Id(0)] string Country,
    [property: Id(1)] string Currency,
    [property: Id(2)] string AccountHolderName,
    [property: Id(3)] string AccountHolderType,
    [property: Id(4)] string? RoutingNumber = null,
    [property: Id(5)] string? AccountNumber = null,
    [property: Id(6)] string? Iban = null);

[GenerateSerializer]
public record BillingDetails(
    [property: Id(0)] string? Name = null,
    [property: Id(1)] string? Email = null,
    [property: Id(2)] string? Phone = null,
    [property: Id(3)] PaymentMethodAddress? Address = null);

[GenerateSerializer]
public record PaymentMethodAddress(
    [property: Id(0)] string? Line1 = null,
    [property: Id(1)] string? Line2 = null,
    [property: Id(2)] string? City = null,
    [property: Id(3)] string? State = null,
    [property: Id(4)] string? PostalCode = null,
    [property: Id(5)] string? Country = null);

[GenerateSerializer]
public record CardSnapshot(
    [property: Id(0)] string Brand,
    [property: Id(1)] string Last4,
    [property: Id(2)] int ExpMonth,
    [property: Id(3)] int ExpYear,
    [property: Id(4)] string Fingerprint,
    [property: Id(5)] string? CardholderName = null,
    [property: Id(6)] string Funding = "credit",
    [property: Id(7)] string? Country = null);

[GenerateSerializer]
public record BankAccountSnapshot(
    [property: Id(0)] string Country,
    [property: Id(1)] string Currency,
    [property: Id(2)] string Last4,
    [property: Id(3)] string AccountHolderName,
    [property: Id(4)] string AccountHolderType,
    [property: Id(5)] string? BankName = null,
    [property: Id(6)] string? RoutingNumber = null,
    [property: Id(7)] string Status = "new");

[GenerateSerializer]
public record PaymentMethodSnapshot(
    [property: Id(0)] Guid Id,
    [property: Id(1)] Guid AccountId,
    [property: Id(2)] PaymentMethodType Type,
    [property: Id(3)] string? CustomerId,
    [property: Id(4)] CardSnapshot? Card,
    [property: Id(5)] BankAccountSnapshot? BankAccount,
    [property: Id(6)] BillingDetails? BillingDetails,
    [property: Id(7)] Dictionary<string, string>? Metadata,
    [property: Id(8)] DateTime CreatedAt,
    [property: Id(9)] bool Livemode);

/// <summary>
/// Grain for tokenized payment method storage.
/// Key: "{accountId}:pm:{paymentMethodId}"
/// </summary>
public interface IPaymentMethodGrain : IGrainWithStringKey
{
    // Creation
    Task<PaymentMethodSnapshot> CreateAsync(CreatePaymentMethodCommand command);
    Task<PaymentMethodSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();

    // Processor token access
    Task<string> GetProcessorTokenAsync();

    // Customer attachment
    Task<PaymentMethodSnapshot> AttachToCustomerAsync(string customerId);
    Task<PaymentMethodSnapshot> DetachFromCustomerAsync();

    // Update
    Task<PaymentMethodSnapshot> UpdateAsync(
        BillingDetails? billingDetails = null,
        int? expMonth = null,
        int? expYear = null,
        Dictionary<string, string>? metadata = null);
}
