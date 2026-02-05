namespace DarkVelocity.Host.State;

public enum PaymentMethod
{
    Cash,
    CreditCard,
    DebitCard,
    GiftCard,
    LoyaltyPoints,
    HouseAccount,
    Check,
    BankTransfer,
    ACH,
    Wire,
    PettyCash,
    Other
}

public enum PaymentStatus
{
    Created,
    Initiated,
    Authorizing,
    Authorized,
    Captured,
    Completed,
    Declined,
    Voided,
    Refunded,
    PartiallyRefunded
}

[GenerateSerializer]
public record CardInfo
{
    [Id(0)] public string MaskedNumber { get; init; } = string.Empty; // e.g., "****4242"
    [Id(1)] public string Brand { get; init; } = string.Empty; // Visa, Mastercard, etc.
    [Id(2)] public string? ExpiryMonth { get; init; }
    [Id(3)] public string? ExpiryYear { get; init; }
    [Id(4)] public string EntryMethod { get; init; } = string.Empty; // chip, swipe, contactless, keyed
    [Id(5)] public string? CardholderName { get; init; }
}

[GenerateSerializer]
public record RefundInfo
{
    [Id(0)] public Guid RefundId { get; init; }
    [Id(1)] public decimal Amount { get; init; }
    [Id(2)] public string Reason { get; init; } = string.Empty;
    [Id(3)] public Guid IssuedBy { get; init; }
    [Id(4)] public DateTime IssuedAt { get; init; }
    [Id(5)] public string? GatewayReference { get; init; }
}

[GenerateSerializer]
public sealed class PaymentState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public Guid OrderId { get; set; }
    [Id(4)] public PaymentMethod Method { get; set; }
    [Id(5)] public PaymentStatus Status { get; set; } = PaymentStatus.Created;
    [Id(6)] public decimal Amount { get; set; }
    [Id(7)] public decimal TipAmount { get; set; }
    [Id(8)] public decimal TotalAmount { get; set; }
    [Id(9)] public string Currency { get; set; } = "USD";

    [Id(10)] public Guid CashierId { get; set; }
    [Id(11)] public Guid? CustomerId { get; set; }
    [Id(12)] public Guid? DrawerId { get; set; }

    // Card payment details
    [Id(13)] public string? GatewayReference { get; set; }
    [Id(14)] public string? AuthorizationCode { get; set; }
    [Id(15)] public CardInfo? CardInfo { get; set; }
    [Id(16)] public string? GatewayName { get; set; }
    [Id(17)] public string? AvsResult { get; set; }
    [Id(18)] public string? CvvResult { get; set; }

    // Cash payment details
    [Id(19)] public decimal? AmountTendered { get; set; }
    [Id(20)] public decimal? ChangeGiven { get; set; }

    // Gift card details
    [Id(21)] public Guid? GiftCardId { get; set; }
    [Id(22)] public string? GiftCardNumber { get; set; }

    // Loyalty redemption
    [Id(23)] public int? LoyaltyPointsRedeemed { get; set; }

    // Refunds
    [Id(24)] public List<RefundInfo> Refunds { get; set; } = [];
    [Id(25)] public decimal RefundedAmount { get; set; }

    // Batch tracking
    [Id(26)] public Guid? BatchId { get; set; }

    // Timestamps
    [Id(27)] public DateTime CreatedAt { get; set; }
    [Id(28)] public DateTime? AuthorizedAt { get; set; }
    [Id(29)] public DateTime? CapturedAt { get; set; }
    [Id(30)] public DateTime? CompletedAt { get; set; }
    [Id(31)] public DateTime? VoidedAt { get; set; }
    [Id(32)] public Guid? VoidedBy { get; set; }
    [Id(33)] public string? VoidReason { get; set; }

    // Retry tracking
    [Id(34)] public int RetryCount { get; set; }
    [Id(35)] public int MaxRetries { get; set; } = 3;
    [Id(36)] public DateTime? NextRetryAt { get; set; }
    [Id(37)] public string? LastErrorCode { get; set; }
    [Id(38)] public string? LastErrorMessage { get; set; }
    [Id(39)] public bool RetryExhausted { get; set; }
    [Id(40)] public List<PaymentRetryAttempt> RetryHistory { get; set; } = [];
}

[GenerateSerializer]
public record PaymentRetryAttempt
{
    [Id(0)] public int AttemptNumber { get; init; }
    [Id(1)] public bool Success { get; init; }
    [Id(2)] public string? ErrorCode { get; init; }
    [Id(3)] public string? ErrorMessage { get; init; }
    [Id(4)] public DateTime AttemptedAt { get; init; }
}

public enum DrawerStatus
{
    Closed,
    Open,
    Counting,
    Reconciling
}

[GenerateSerializer]
public record CashDrop
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public decimal Amount { get; init; }
    [Id(2)] public Guid DroppedBy { get; init; }
    [Id(3)] public DateTime DroppedAt { get; init; }
    [Id(4)] public string? Notes { get; init; }
}

[GenerateSerializer]
public record DrawerTransaction
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public DrawerTransactionType Type { get; init; }
    [Id(2)] public decimal Amount { get; init; }
    [Id(3)] public Guid? PaymentId { get; init; }
    [Id(4)] public string? Description { get; init; }
    [Id(5)] public DateTime Timestamp { get; init; }
}

public enum DrawerTransactionType
{
    OpeningFloat,
    CashSale,
    CashRefund,
    CashPayout,
    NoSale,
    Drop,
    Adjustment
}

[GenerateSerializer]
public sealed class CashDrawerState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public Guid? DeviceId { get; set; }
    [Id(4)] public string Name { get; set; } = string.Empty;
    [Id(5)] public DrawerStatus Status { get; set; } = DrawerStatus.Closed;
    [Id(6)] public Guid? CurrentUserId { get; set; }
    [Id(7)] public DateTime? OpenedAt { get; set; }
    [Id(8)] public decimal OpeningFloat { get; set; }
    [Id(9)] public decimal CashIn { get; set; }
    [Id(10)] public decimal CashOut { get; set; }
    [Id(11)] public decimal ExpectedBalance { get; set; }
    [Id(12)] public decimal? ActualBalance { get; set; }
    [Id(13)] public List<CashDrop> CashDrops { get; set; } = [];
    [Id(14)] public List<DrawerTransaction> Transactions { get; set; } = [];
    [Id(15)] public DateTime? LastCountedAt { get; set; }
    [Id(16)] public int Version { get; set; }
}
