namespace DarkVelocity.Host.PaymentProcessors;

// ============================================================================
// Stripe Processor Events
// ============================================================================

[GenerateSerializer]
public abstract record StripeProcessorEvent;

[GenerateSerializer]
public sealed record StripePaymentIntentCreatedEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string StripePaymentIntentId,
    [property: Id(2)] long Amount,
    [property: Id(3)] string Currency,
    [property: Id(4)] string Status,
    [property: Id(5)] string? ClientSecret,
    [property: Id(6)] string? IdempotencyKey,
    [property: Id(7)] DateTime CreatedAt) : StripeProcessorEvent;

[GenerateSerializer]
public sealed record StripePaymentAuthorizedEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string StripePaymentIntentId,
    [property: Id(2)] string? ChargeId,
    [property: Id(3)] long Amount,
    [property: Id(4)] DateTime AuthorizedAt) : StripeProcessorEvent;

[GenerateSerializer]
public sealed record StripePaymentCapturedEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string StripePaymentIntentId,
    [property: Id(2)] long CapturedAmount,
    [property: Id(3)] string? IdempotencyKey,
    [property: Id(4)] DateTime CapturedAt) : StripeProcessorEvent;

[GenerateSerializer]
public sealed record StripePaymentRefundedEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string StripeRefundId,
    [property: Id(2)] long RefundedAmount,
    [property: Id(3)] string? Reason,
    [property: Id(4)] string? IdempotencyKey,
    [property: Id(5)] DateTime RefundedAt) : StripeProcessorEvent;

[GenerateSerializer]
public sealed record StripePaymentCanceledEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string StripePaymentIntentId,
    [property: Id(2)] string? CancellationReason,
    [property: Id(3)] DateTime CanceledAt) : StripeProcessorEvent;

[GenerateSerializer]
public sealed record StripePaymentFailedEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string? StripePaymentIntentId,
    [property: Id(2)] string ErrorCode,
    [property: Id(3)] string ErrorMessage,
    [property: Id(4)] string? DeclineCode,
    [property: Id(5)] int AttemptNumber,
    [property: Id(6)] DateTime FailedAt) : StripeProcessorEvent;

[GenerateSerializer]
public sealed record StripeTerminalReaderPairedEvent(
    [property: Id(0)] Guid TerminalId,
    [property: Id(1)] string StripeReaderId,
    [property: Id(2)] string Label,
    [property: Id(3)] string? DeviceType,
    [property: Id(4)] string? SerialNumber,
    [property: Id(5)] DateTime PairedAt) : StripeProcessorEvent;

[GenerateSerializer]
public sealed record StripeProcessorErrorEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string Operation,
    [property: Id(2)] string ErrorCode,
    [property: Id(3)] string ErrorMessage,
    [property: Id(4)] int AttemptNumber,
    [property: Id(5)] bool WillRetry,
    [property: Id(6)] DateTime OccurredAt) : StripeProcessorEvent;

[GenerateSerializer]
public sealed record StripeWebhookReceivedEvent(
    [property: Id(0)] string EventId,
    [property: Id(1)] string EventType,
    [property: Id(2)] string? RelatedPaymentIntentId,
    [property: Id(3)] DateTime ReceivedAt) : StripeProcessorEvent;

[GenerateSerializer]
public sealed record Stripe3dsRequiredEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string StripePaymentIntentId,
    [property: Id(2)] string? RedirectUrl,
    [property: Id(3)] DateTime RequiredAt) : StripeProcessorEvent;

[GenerateSerializer]
public sealed record Stripe3dsCompletedEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string StripePaymentIntentId,
    [property: Id(2)] bool Successful,
    [property: Id(3)] DateTime CompletedAt) : StripeProcessorEvent;

// ============================================================================
// Adyen Processor Events
// ============================================================================

[GenerateSerializer]
public abstract record AdyenProcessorEvent;

[GenerateSerializer]
public sealed record AdyenPaymentCreatedEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string PspReference,
    [property: Id(2)] long Amount,
    [property: Id(3)] string Currency,
    [property: Id(4)] string ResultCode,
    [property: Id(5)] string? AuthCode,
    [property: Id(6)] string? IdempotencyKey,
    [property: Id(7)] DateTime CreatedAt) : AdyenProcessorEvent;

[GenerateSerializer]
public sealed record AdyenPaymentAuthorizedEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string PspReference,
    [property: Id(2)] long Amount,
    [property: Id(3)] string? AuthCode,
    [property: Id(4)] DateTime AuthorizedAt) : AdyenProcessorEvent;

[GenerateSerializer]
public sealed record AdyenPaymentCapturedEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string PspReference,
    [property: Id(2)] string? CapturePspReference,
    [property: Id(3)] long CapturedAmount,
    [property: Id(4)] string? IdempotencyKey,
    [property: Id(5)] DateTime CapturedAt) : AdyenProcessorEvent;

[GenerateSerializer]
public sealed record AdyenPaymentRefundedEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string OriginalPspReference,
    [property: Id(2)] string? RefundPspReference,
    [property: Id(3)] long RefundedAmount,
    [property: Id(4)] string? Reason,
    [property: Id(5)] string? IdempotencyKey,
    [property: Id(6)] DateTime RefundedAt) : AdyenProcessorEvent;

[GenerateSerializer]
public sealed record AdyenPaymentCanceledEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string PspReference,
    [property: Id(2)] DateTime CanceledAt) : AdyenProcessorEvent;

[GenerateSerializer]
public sealed record AdyenPaymentFailedEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string? PspReference,
    [property: Id(2)] string RefusalReason,
    [property: Id(3)] string? RefusalReasonCode,
    [property: Id(4)] int AttemptNumber,
    [property: Id(5)] DateTime FailedAt) : AdyenProcessorEvent;

[GenerateSerializer]
public sealed record AdyenTerminalReaderPairedEvent(
    [property: Id(0)] Guid TerminalId,
    [property: Id(1)] string AdyenTerminalId,
    [property: Id(2)] string StoreId,
    [property: Id(3)] string MerchantAccount,
    [property: Id(4)] DateTime PairedAt) : AdyenProcessorEvent;

[GenerateSerializer]
public sealed record AdyenProcessorErrorEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string Operation,
    [property: Id(2)] string ErrorCode,
    [property: Id(3)] string ErrorMessage,
    [property: Id(4)] int AttemptNumber,
    [property: Id(5)] bool WillRetry,
    [property: Id(6)] DateTime OccurredAt) : AdyenProcessorEvent;

[GenerateSerializer]
public sealed record AdyenNotificationReceivedEvent(
    [property: Id(0)] string NotificationType,
    [property: Id(1)] string PspReference,
    [property: Id(2)] string? OriginalReference,
    [property: Id(3)] DateTime ReceivedAt) : AdyenProcessorEvent;

[GenerateSerializer]
public sealed record Adyen3dsRequiredEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string? PspReference,
    [property: Id(2)] string ActionType,
    [property: Id(3)] string? ActionUrl,
    [property: Id(4)] DateTime RequiredAt) : AdyenProcessorEvent;

[GenerateSerializer]
public sealed record Adyen3dsCompletedEvent(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] string PspReference,
    [property: Id(2)] bool Successful,
    [property: Id(3)] DateTime CompletedAt) : AdyenProcessorEvent;

// ============================================================================
// Idempotency Key Events
// ============================================================================

[GenerateSerializer]
public abstract record IdempotencyKeyEvent;

[GenerateSerializer]
public sealed record IdempotencyKeyGeneratedEvent(
    [property: Id(0)] string Key,
    [property: Id(1)] string Operation,
    [property: Id(2)] Guid RelatedEntityId,
    [property: Id(3)] DateTime GeneratedAt,
    [property: Id(4)] DateTime ExpiresAt) : IdempotencyKeyEvent;

[GenerateSerializer]
public sealed record IdempotencyKeyUsedEvent(
    [property: Id(0)] string Key,
    [property: Id(1)] bool Successful,
    [property: Id(2)] string? ResultHash,
    [property: Id(3)] DateTime UsedAt) : IdempotencyKeyEvent;

[GenerateSerializer]
public sealed record IdempotencyKeyExpiredEvent(
    [property: Id(0)] string Key,
    [property: Id(1)] DateTime ExpiredAt) : IdempotencyKeyEvent;
