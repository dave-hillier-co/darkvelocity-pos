namespace DarkVelocity.Host.PaymentProcessors;

// ============================================================================
// Stripe Processor State
// ============================================================================

[GenerateSerializer]
public sealed class StripeProcessorState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid PaymentIntentId { get; set; }
    [Id(2)] public string? StripePaymentIntentId { get; set; }
    [Id(3)] public string? StripeChargeId { get; set; }
    [Id(4)] public string? ClientSecret { get; set; }
    [Id(5)] public string Status { get; set; } = "pending";
    [Id(6)] public long Amount { get; set; }
    [Id(7)] public string Currency { get; set; } = "usd";
    [Id(8)] public long AuthorizedAmount { get; set; }
    [Id(9)] public long CapturedAmount { get; set; }
    [Id(10)] public long RefundedAmount { get; set; }
    [Id(11)] public bool CaptureAutomatically { get; set; }
    [Id(12)] public string? StatementDescriptor { get; set; }
    [Id(13)] public string? CustomerId { get; set; }
    [Id(14)] public string? PaymentMethodId { get; set; }
    [Id(15)] public Dictionary<string, string>? Metadata { get; set; }

    // Stripe Connect
    [Id(16)] public string? ConnectedAccountId { get; set; }
    [Id(17)] public long? ApplicationFee { get; set; }

    // 3DS
    [Id(18)] public string? NextActionType { get; set; }
    [Id(19)] public string? NextActionRedirectUrl { get; set; }

    // Retry tracking
    [Id(20)] public int RetryCount { get; set; }
    [Id(21)] public DateTime? LastAttemptAt { get; set; }
    [Id(22)] public DateTime? NextRetryAt { get; set; }
    [Id(23)] public string? LastErrorCode { get; set; }
    [Id(24)] public string? LastErrorMessage { get; set; }

    // Idempotency
    [Id(25)] public Dictionary<string, string> IdempotencyKeys { get; set; } = [];

    // Timestamps
    [Id(26)] public DateTime CreatedAt { get; set; }
    [Id(27)] public DateTime? AuthorizedAt { get; set; }
    [Id(28)] public DateTime? CapturedAt { get; set; }
    [Id(29)] public DateTime? CanceledAt { get; set; }

    // Event history
    [Id(30)] public List<StripeProcessorEventRecord> Events { get; set; } = [];

    [Id(31)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed record StripeProcessorEventRecord(
    [property: Id(0)] DateTime Timestamp,
    [property: Id(1)] string EventType,
    [property: Id(2)] string? ExternalEventId,
    [property: Id(3)] string? Data);

// ============================================================================
// Adyen Processor State
// ============================================================================

[GenerateSerializer]
public sealed class AdyenProcessorState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid PaymentIntentId { get; set; }
    [Id(2)] public string? PspReference { get; set; }
    [Id(3)] public string? AuthCode { get; set; }
    [Id(4)] public string Status { get; set; } = "pending";
    [Id(5)] public string? ResultCode { get; set; }
    [Id(6)] public long Amount { get; set; }
    [Id(7)] public string Currency { get; set; } = "USD";
    [Id(8)] public long AuthorizedAmount { get; set; }
    [Id(9)] public long CapturedAmount { get; set; }
    [Id(10)] public long RefundedAmount { get; set; }
    [Id(11)] public bool CaptureDelayed { get; set; }
    [Id(12)] public string MerchantAccount { get; set; } = string.Empty;
    [Id(13)] public string? ShopperReference { get; set; }
    [Id(14)] public string? Reference { get; set; }
    [Id(15)] public Dictionary<string, string>? Metadata { get; set; }

    // Split payments
    [Id(16)] public List<AdyenSplitRecord>? Splits { get; set; }

    // 3DS / Action
    [Id(17)] public string? ActionType { get; set; }
    [Id(18)] public string? ActionUrl { get; set; }
    [Id(19)] public string? PaymentData { get; set; }

    // Retry tracking
    [Id(20)] public int RetryCount { get; set; }
    [Id(21)] public DateTime? LastAttemptAt { get; set; }
    [Id(22)] public DateTime? NextRetryAt { get; set; }
    [Id(23)] public string? LastErrorCode { get; set; }
    [Id(24)] public string? LastErrorMessage { get; set; }

    // Idempotency
    [Id(25)] public Dictionary<string, string> IdempotencyKeys { get; set; } = [];

    // Timestamps
    [Id(26)] public DateTime CreatedAt { get; set; }
    [Id(27)] public DateTime? AuthorizedAt { get; set; }
    [Id(28)] public DateTime? CapturedAt { get; set; }
    [Id(29)] public DateTime? CanceledAt { get; set; }

    // Event history
    [Id(30)] public List<AdyenProcessorEventRecord> Events { get; set; } = [];

    [Id(31)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed record AdyenSplitRecord(
    [property: Id(0)] string Account,
    [property: Id(1)] long Amount,
    [property: Id(2)] string Type,
    [property: Id(3)] string? Reference);

[GenerateSerializer]
public sealed record AdyenProcessorEventRecord(
    [property: Id(0)] DateTime Timestamp,
    [property: Id(1)] string EventType,
    [property: Id(2)] string? PspReference,
    [property: Id(3)] string? Data);

// ============================================================================
// Idempotency Key State
// ============================================================================

[GenerateSerializer]
public sealed class IdempotencyKeyState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Dictionary<string, IdempotencyKeyRecord> Keys { get; set; } = [];
    [Id(2)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed record IdempotencyKeyRecord(
    [property: Id(0)] string Key,
    [property: Id(1)] string Operation,
    [property: Id(2)] Guid RelatedEntityId,
    [property: Id(3)] DateTime GeneratedAt,
    [property: Id(4)] DateTime ExpiresAt,
    [property: Id(5)] bool Used,
    [property: Id(6)] DateTime? UsedAt,
    [property: Id(7)] bool? Successful,
    [property: Id(8)] string? ResultHash);
