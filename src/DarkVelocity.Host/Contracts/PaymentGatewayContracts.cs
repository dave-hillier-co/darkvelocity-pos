namespace DarkVelocity.Host.Contracts;

// ============================================================================
// Payment Gateway API Request DTOs (Stripe-compatible)
// ============================================================================

public record CreatePaymentIntentApiRequest(
    long Amount,
    string? Currency = null,
    IReadOnlyList<string>? PaymentMethodTypes = null,
    string? Description = null,
    string? StatementDescriptor = null,
    string? CaptureMethod = null,
    string? Customer = null,
    string? PaymentMethod = null,
    Dictionary<string, string>? Metadata = null);

public record UpdatePaymentIntentApiRequest(
    long? Amount = null,
    string? Description = null,
    string? Customer = null,
    Dictionary<string, string>? Metadata = null);

public record ConfirmPaymentIntentApiRequest(
    string? PaymentMethod = null,
    string? ReturnUrl = null,
    bool? OffSession = null);

public record CapturePaymentIntentApiRequest(long? AmountToCapture = null);

public record CancelPaymentIntentApiRequest(string? CancellationReason = null);

// ============================================================================
// Payment Method API Request DTOs
// ============================================================================

public record CreatePaymentMethodApiRequest(
    string Type,
    CardApiRequest? Card = null,
    BankAccountApiRequest? UsBankAccount = null,
    BillingDetailsApiRequest? BillingDetails = null,
    Dictionary<string, string>? Metadata = null);

public record CardApiRequest(
    string Number,
    int ExpMonth,
    int ExpYear,
    string? Cvc = null);

public record BankAccountApiRequest(
    string AccountHolderName,
    string AccountHolderType,
    string AccountNumber,
    string RoutingNumber);

public record BillingDetailsApiRequest(
    string? Name = null,
    string? Email = null,
    string? Phone = null,
    AddressApiRequest? Address = null);

public record AddressApiRequest(
    string? Line1 = null,
    string? Line2 = null,
    string? City = null,
    string? State = null,
    string? PostalCode = null,
    string? Country = null);

public record UpdatePaymentMethodApiRequest(
    BillingDetailsApiRequest? BillingDetails = null,
    CardUpdateApiRequest? Card = null,
    Dictionary<string, string>? Metadata = null);

public record CardUpdateApiRequest(
    int? ExpMonth = null,
    int? ExpYear = null);

public record AttachPaymentMethodApiRequest(
    string Customer);

public record HandleNextActionApiRequest(
    string ActionData);
