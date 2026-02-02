namespace DarkVelocity.Host.Grains;

public enum PaymentIntentStatus
{
    RequiresPaymentMethod,
    RequiresConfirmation,
    RequiresAction,
    Processing,
    RequiresCapture,
    Succeeded,
    Canceled
}

public enum CaptureMethod
{
    Automatic,
    Manual
}

[GenerateSerializer]
public record CreatePaymentIntentCommand(
    [property: Id(0)] Guid AccountId,
    [property: Id(1)] long Amount,
    [property: Id(2)] string Currency,
    [property: Id(3)] IReadOnlyList<string>? PaymentMethodTypes = null,
    [property: Id(4)] string? Description = null,
    [property: Id(5)] string? StatementDescriptor = null,
    [property: Id(6)] CaptureMethod CaptureMethod = CaptureMethod.Automatic,
    [property: Id(7)] string? CustomerId = null,
    [property: Id(8)] string? PaymentMethodId = null,
    [property: Id(9)] Dictionary<string, string>? Metadata = null);

[GenerateSerializer]
public record ConfirmPaymentIntentCommand(
    [property: Id(0)] string? PaymentMethodId = null,
    [property: Id(1)] string? ReturnUrl = null,
    [property: Id(2)] bool OffSession = false);

[GenerateSerializer]
public record UpdatePaymentIntentCommand(
    [property: Id(0)] long? Amount = null,
    [property: Id(1)] string? Description = null,
    [property: Id(2)] string? CustomerId = null,
    [property: Id(3)] Dictionary<string, string>? Metadata = null);

[GenerateSerializer]
public record PaymentIntentSnapshot(
    [property: Id(0)] Guid Id,
    [property: Id(1)] Guid AccountId,
    [property: Id(2)] long Amount,
    [property: Id(3)] long AmountCapturable,
    [property: Id(4)] long AmountReceived,
    [property: Id(5)] string Currency,
    [property: Id(6)] PaymentIntentStatus Status,
    [property: Id(7)] string? PaymentMethodId,
    [property: Id(8)] string? CustomerId,
    [property: Id(9)] string? Description,
    [property: Id(10)] string? StatementDescriptor,
    [property: Id(11)] CaptureMethod CaptureMethod,
    [property: Id(12)] string ClientSecret,
    [property: Id(13)] string? LastPaymentError,
    [property: Id(14)] string? ProcessorTransactionId,
    [property: Id(15)] NextAction? NextAction,
    [property: Id(16)] Dictionary<string, string>? Metadata,
    [property: Id(17)] DateTime CreatedAt,
    [property: Id(18)] DateTime? CanceledAt,
    [property: Id(19)] DateTime? SucceededAt);

[GenerateSerializer]
public record NextAction(
    [property: Id(0)] string Type,
    [property: Id(1)] string? RedirectUrl = null,
    [property: Id(2)] Dictionary<string, string>? Data = null);

/// <summary>
/// Grain for PaymentIntent lifecycle management.
/// Key: "{accountId}:pi:{paymentIntentId}"
/// </summary>
public interface IPaymentIntentGrain : IGrainWithStringKey
{
    // Creation
    Task<PaymentIntentSnapshot> CreateAsync(CreatePaymentIntentCommand command);
    Task<PaymentIntentSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();

    // Lifecycle
    Task<PaymentIntentSnapshot> ConfirmAsync(ConfirmPaymentIntentCommand command);
    Task<PaymentIntentSnapshot> CaptureAsync(long? amountToCapture = null);
    Task<PaymentIntentSnapshot> CancelAsync(string? cancellationReason = null);

    // Updates
    Task<PaymentIntentSnapshot> UpdateAsync(UpdatePaymentIntentCommand command);

    // Payment method
    Task<PaymentIntentSnapshot> AttachPaymentMethodAsync(string paymentMethodId);

    // 3DS / Action handling
    Task<PaymentIntentSnapshot> HandleNextActionAsync(string actionData);

    // Internal - called by processor callbacks
    Task RecordAuthorizationAsync(string processorTxnId, string authCode);
    Task RecordDeclineAsync(string declineCode, string declineMessage);
    Task RecordCaptureAsync(string processorTxnId, long capturedAmount);

    // Status
    Task<PaymentIntentStatus> GetStatusAsync();
}
