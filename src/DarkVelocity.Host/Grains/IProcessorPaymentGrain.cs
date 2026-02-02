namespace DarkVelocity.Host.Grains;

/// <summary>
/// Base interface for processor-specific payment handling.
/// Each processor grain tracks its own transaction state.
/// </summary>
public interface IProcessorPaymentGrain : IGrainWithStringKey
{
    Task<ProcessorAuthResult> AuthorizeAsync(ProcessorAuthRequest request);
    Task<ProcessorCaptureResult> CaptureAsync(string transactionId, long? amount = null);
    Task<ProcessorRefundResult> RefundAsync(string transactionId, long amount, string? reason = null);
    Task<ProcessorVoidResult> VoidAsync(string transactionId, string? reason = null);

    // Webhook reconciliation
    Task HandleWebhookAsync(string eventType, string payload);

    // State
    Task<ProcessorPaymentState> GetStateAsync();
}

[GenerateSerializer]
public record ProcessorAuthRequest(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] long Amount,
    [property: Id(2)] string Currency,
    [property: Id(3)] string PaymentMethodToken,
    [property: Id(4)] bool CaptureAutomatically,
    [property: Id(5)] string? StatementDescriptor,
    [property: Id(6)] Dictionary<string, string>? Metadata);

[GenerateSerializer]
public record ProcessorAuthResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? TransactionId,
    [property: Id(2)] string? AuthorizationCode,
    [property: Id(3)] string? DeclineCode,
    [property: Id(4)] string? DeclineMessage,
    [property: Id(5)] NextAction? RequiredAction,
    [property: Id(6)] string? NetworkTransactionId);

[GenerateSerializer]
public record ProcessorCaptureResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? CaptureId,
    [property: Id(2)] long CapturedAmount,
    [property: Id(3)] string? ErrorCode,
    [property: Id(4)] string? ErrorMessage);

[GenerateSerializer]
public record ProcessorRefundResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? RefundId,
    [property: Id(2)] long RefundedAmount,
    [property: Id(3)] string? ErrorCode,
    [property: Id(4)] string? ErrorMessage);

[GenerateSerializer]
public record ProcessorVoidResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? VoidId,
    [property: Id(2)] string? ErrorCode,
    [property: Id(3)] string? ErrorMessage);

[GenerateSerializer]
public record ProcessorPaymentState(
    [property: Id(0)] string ProcessorName,
    [property: Id(1)] Guid PaymentIntentId,
    [property: Id(2)] string? TransactionId,
    [property: Id(3)] string? AuthorizationCode,
    [property: Id(4)] string Status,
    [property: Id(5)] long AuthorizedAmount,
    [property: Id(6)] long CapturedAmount,
    [property: Id(7)] long RefundedAmount,
    [property: Id(8)] int RetryCount,
    [property: Id(9)] DateTime? LastAttemptAt,
    [property: Id(10)] string? LastError,
    [property: Id(11)] List<ProcessorEvent> Events);

[GenerateSerializer]
public record ProcessorEvent(
    [property: Id(0)] DateTime Timestamp,
    [property: Id(1)] string EventType,
    [property: Id(2)] string? ExternalEventId,
    [property: Id(3)] string? Data);

/// <summary>
/// Mock processor for testing.
/// Key: "{accountId}:mock:{paymentIntentId}"
/// </summary>
public interface IMockProcessorGrain : IProcessorPaymentGrain
{
    // Test controls
    Task ConfigureNextResponseAsync(bool shouldSucceed, string? errorCode = null, int? delayMs = null);
    Task SimulateWebhookAsync(string eventType);
    Task SimulateDisputeAsync(long amount, string reason);
}

/// <summary>
/// Stripe-specific payment processor grain.
/// Key: "{accountId}:stripe:{paymentIntentId}"
/// </summary>
public interface IStripeProcessorGrain : IProcessorPaymentGrain
{
    // Stripe-specific operations
    Task<string> CreateSetupIntentAsync(string customerId);
    Task HandleStripeWebhookAsync(string eventType, string stripeEventId, string payload);

    // Stripe Connect specific
    Task<ProcessorAuthResult> AuthorizeOnBehalfOfAsync(
        ProcessorAuthRequest request,
        string connectedAccountId,
        long? applicationFee = null);
}

/// <summary>
/// Adyen-specific payment processor grain.
/// Key: "{accountId}:adyen:{paymentIntentId}"
/// </summary>
public interface IAdyenProcessorGrain : IProcessorPaymentGrain
{
    // Adyen-specific operations
    Task<ProcessorAuthResult> AuthorizeWithSplitAsync(
        ProcessorAuthRequest request,
        List<AdyenSplitItem> splits);

    Task HandleAdyenNotificationAsync(string notificationType, string pspReference, string payload);
}

[GenerateSerializer]
public record AdyenSplitItem(
    [property: Id(0)] string Account,
    [property: Id(1)] long Amount,
    [property: Id(2)] string Type,
    [property: Id(3)] string? Reference);
