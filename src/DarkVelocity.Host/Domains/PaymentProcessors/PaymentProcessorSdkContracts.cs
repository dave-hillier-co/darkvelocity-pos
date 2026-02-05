namespace DarkVelocity.Host.PaymentProcessors;

// ============================================================================
// Stripe SDK Abstraction
// ============================================================================

/// <summary>
/// Abstraction for Stripe SDK to enable testing.
/// Actual implementation would call Stripe.net SDK.
/// </summary>
public interface IStripeClient
{
    Task<StripePaymentIntentResult> CreatePaymentIntentAsync(
        StripePaymentIntentCreateRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<StripePaymentIntentResult> ConfirmPaymentIntentAsync(
        string paymentIntentId,
        string? paymentMethodId = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<StripePaymentIntentResult> CapturePaymentIntentAsync(
        string paymentIntentId,
        long? amountToCapture = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<StripePaymentIntentResult> CancelPaymentIntentAsync(
        string paymentIntentId,
        string? cancellationReason = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<StripeRefundResult> CreateRefundAsync(
        string paymentIntentId,
        long? amount = null,
        string? reason = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<StripeSetupIntentResult> CreateSetupIntentAsync(
        string customerId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    // Terminal operations
    Task<StripeTerminalReaderResult> CreateTerminalReaderAsync(
        StripeTerminalReaderCreateRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<StripeTerminalConnectionTokenResult> CreateConnectionTokenAsync(
        string? locationId = null,
        CancellationToken cancellationToken = default);

    Task<StripeTerminalReaderResult> ProcessTerminalPaymentIntentAsync(
        string readerId,
        string paymentIntentId,
        CancellationToken cancellationToken = default);

    // Webhook verification
    bool VerifyWebhookSignature(string payload, string signature, string secret);
}

[GenerateSerializer]
public record StripePaymentIntentCreateRequest(
    [property: Id(0)] long Amount,
    [property: Id(1)] string Currency,
    [property: Id(2)] string? PaymentMethodId,
    [property: Id(3)] bool AutomaticCapture,
    [property: Id(4)] string? StatementDescriptor,
    [property: Id(5)] string? CustomerId,
    [property: Id(6)] Dictionary<string, string>? Metadata,
    [property: Id(7)] string? ConnectedAccountId,
    [property: Id(8)] long? ApplicationFee);

[GenerateSerializer]
public record StripePaymentIntentResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? PaymentIntentId,
    [property: Id(2)] string? Status,
    [property: Id(3)] string? ClientSecret,
    [property: Id(4)] long Amount,
    [property: Id(5)] string? ChargeId,
    [property: Id(6)] StripeNextAction? NextAction,
    [property: Id(7)] string? ErrorCode,
    [property: Id(8)] string? ErrorMessage,
    [property: Id(9)] string? DeclineCode);

[GenerateSerializer]
public record StripeNextAction(
    [property: Id(0)] string Type,
    [property: Id(1)] string? RedirectUrl,
    [property: Id(2)] StripeUseStripeSdkAction? UseStripeSdk);

[GenerateSerializer]
public record StripeUseStripeSdkAction(
    [property: Id(0)] string? Type,
    [property: Id(1)] string? StripeJs);

[GenerateSerializer]
public record StripeRefundResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? RefundId,
    [property: Id(2)] string? Status,
    [property: Id(3)] long Amount,
    [property: Id(4)] string? ErrorCode,
    [property: Id(5)] string? ErrorMessage);

[GenerateSerializer]
public record StripeSetupIntentResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? SetupIntentId,
    [property: Id(2)] string? ClientSecret,
    [property: Id(3)] string? Status,
    [property: Id(4)] string? ErrorCode,
    [property: Id(5)] string? ErrorMessage);

[GenerateSerializer]
public record StripeTerminalReaderCreateRequest(
    [property: Id(0)] string RegistrationCode,
    [property: Id(1)] string Label,
    [property: Id(2)] string? LocationId,
    [property: Id(3)] Dictionary<string, string>? Metadata);

[GenerateSerializer]
public record StripeTerminalReaderResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? ReaderId,
    [property: Id(2)] string? Label,
    [property: Id(3)] string? Status,
    [property: Id(4)] string? DeviceType,
    [property: Id(5)] string? SerialNumber,
    [property: Id(6)] string? ErrorCode,
    [property: Id(7)] string? ErrorMessage);

[GenerateSerializer]
public record StripeTerminalConnectionTokenResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? Secret,
    [property: Id(2)] string? ErrorCode,
    [property: Id(3)] string? ErrorMessage);

// ============================================================================
// Adyen SDK Abstraction
// ============================================================================

/// <summary>
/// Abstraction for Adyen SDK to enable testing.
/// Actual implementation would call Adyen .NET API library.
/// </summary>
public interface IAdyenClient
{
    Task<AdyenPaymentResult> CreatePaymentAsync(
        AdyenPaymentRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<AdyenCaptureResult> CapturePaymentAsync(
        string pspReference,
        long amount,
        string currency,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<AdyenRefundResult> RefundPaymentAsync(
        string pspReference,
        long amount,
        string currency,
        string? reason = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<AdyenCancelResult> CancelPaymentAsync(
        string pspReference,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<AdyenPaymentResult> CreateSplitPaymentAsync(
        AdyenPaymentRequest request,
        List<AdyenSplitAmount> splits,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    // Terminal operations
    Task<AdyenTerminalResult> RegisterTerminalAsync(
        AdyenTerminalRegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<AdyenTerminalPaymentResult> ProcessTerminalPaymentAsync(
        string terminalId,
        AdyenTerminalPaymentRequest request,
        CancellationToken cancellationToken = default);

    // Webhook verification
    bool VerifyHmacSignature(string payload, string hmacSignature, string hmacKey);
}

[GenerateSerializer]
public record AdyenPaymentRequest(
    [property: Id(0)] long Amount,
    [property: Id(1)] string Currency,
    [property: Id(2)] string PaymentMethod,
    [property: Id(3)] string Reference,
    [property: Id(4)] string? ShopperReference,
    [property: Id(5)] string MerchantAccount,
    [property: Id(6)] string? ReturnUrl,
    [property: Id(7)] bool CaptureDelayed,
    [property: Id(8)] Dictionary<string, string>? Metadata);

[GenerateSerializer]
public record AdyenPaymentResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? PspReference,
    [property: Id(2)] string? ResultCode,
    [property: Id(3)] string? AuthCode,
    [property: Id(4)] AdyenAction? Action,
    [property: Id(5)] string? RefusalReason,
    [property: Id(6)] string? RefusalReasonCode,
    [property: Id(7)] Dictionary<string, string>? AdditionalData);

[GenerateSerializer]
public record AdyenAction(
    [property: Id(0)] string Type,
    [property: Id(1)] string? Url,
    [property: Id(2)] string? PaymentData,
    [property: Id(3)] Dictionary<string, string>? Data);

[GenerateSerializer]
public record AdyenCaptureResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? PspReference,
    [property: Id(2)] string? Status,
    [property: Id(3)] string? ErrorCode,
    [property: Id(4)] string? ErrorMessage);

[GenerateSerializer]
public record AdyenRefundResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? PspReference,
    [property: Id(2)] string? Status,
    [property: Id(3)] string? ErrorCode,
    [property: Id(4)] string? ErrorMessage);

[GenerateSerializer]
public record AdyenCancelResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? PspReference,
    [property: Id(2)] string? Status,
    [property: Id(3)] string? ErrorCode,
    [property: Id(4)] string? ErrorMessage);

[GenerateSerializer]
public record AdyenSplitAmount(
    [property: Id(0)] string Account,
    [property: Id(1)] long Amount,
    [property: Id(2)] string Type,
    [property: Id(3)] string? Reference);

[GenerateSerializer]
public record AdyenTerminalRegisterRequest(
    [property: Id(0)] string TerminalId,
    [property: Id(1)] string StoreId,
    [property: Id(2)] string MerchantAccount);

[GenerateSerializer]
public record AdyenTerminalResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? TerminalId,
    [property: Id(2)] string? Status,
    [property: Id(3)] string? ErrorCode,
    [property: Id(4)] string? ErrorMessage);

[GenerateSerializer]
public record AdyenTerminalPaymentRequest(
    [property: Id(0)] long Amount,
    [property: Id(1)] string Currency,
    [property: Id(2)] string Reference,
    [property: Id(3)] string MerchantAccount);

[GenerateSerializer]
public record AdyenTerminalPaymentResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? PspReference,
    [property: Id(2)] string? ResultCode,
    [property: Id(3)] string? AuthCode,
    [property: Id(4)] string? ErrorCode,
    [property: Id(5)] string? ErrorMessage);
