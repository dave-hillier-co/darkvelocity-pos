using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

[GenerateSerializer]
public sealed class PaymentIntentState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid AccountId { get; set; }
    [Id(2)] public long Amount { get; set; }
    [Id(3)] public long AmountCapturable { get; set; }
    [Id(4)] public long AmountReceived { get; set; }
    [Id(5)] public string Currency { get; set; } = "usd";
    [Id(6)] public PaymentIntentStatus Status { get; set; } = PaymentIntentStatus.RequiresPaymentMethod;
    [Id(7)] public string? PaymentMethodId { get; set; }
    [Id(8)] public string? CustomerId { get; set; }
    [Id(9)] public string? Description { get; set; }
    [Id(10)] public string? StatementDescriptor { get; set; }
    [Id(11)] public CaptureMethod CaptureMethod { get; set; } = CaptureMethod.Automatic;
    [Id(12)] public string ClientSecret { get; set; } = string.Empty;
    [Id(13)] public string? LastPaymentError { get; set; }
    [Id(14)] public string? ProcessorTransactionId { get; set; }
    [Id(15)] public string? ProcessorAuthorizationCode { get; set; }
    [Id(16)] public NextAction? NextAction { get; set; }
    [Id(17)] public Dictionary<string, string>? Metadata { get; set; }
    [Id(18)] public IReadOnlyList<string> PaymentMethodTypes { get; set; } = ["card"];
    [Id(19)] public DateTime CreatedAt { get; set; }
    [Id(20)] public DateTime? CanceledAt { get; set; }
    [Id(21)] public DateTime? SucceededAt { get; set; }
    [Id(22)] public string? CancellationReason { get; set; }
    [Id(23)] public string? ProcessorName { get; set; }

    // Idempotency cache
    [Id(24)] public Dictionary<string, IdempotencyEntry> IdempotencyCache { get; set; } = [];

    [Id(25)] public int Version { get; set; }
}

[GenerateSerializer]
public record IdempotencyEntry(
    [property: Id(0)] DateTime CreatedAt,
    [property: Id(1)] PaymentIntentSnapshot Snapshot);
