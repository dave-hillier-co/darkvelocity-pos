using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

[GenerateSerializer]
public sealed class MockProcessorState
{
    [Id(0)] public Guid PaymentIntentId { get; set; }
    [Id(1)] public string? TransactionId { get; set; }
    [Id(2)] public string? AuthorizationCode { get; set; }
    [Id(3)] public string Status { get; set; } = "pending";
    [Id(4)] public long AuthorizedAmount { get; set; }
    [Id(5)] public long CapturedAmount { get; set; }
    [Id(6)] public long RefundedAmount { get; set; }
    [Id(7)] public int RetryCount { get; set; }
    [Id(8)] public DateTime? LastAttemptAt { get; set; }
    [Id(9)] public string? LastError { get; set; }
    [Id(10)] public List<ProcessorEvent> Events { get; set; } = [];

    // Test configuration
    [Id(11)] public bool? NextResponseShouldSucceed { get; set; }
    [Id(12)] public string? NextErrorCode { get; set; }
    [Id(13)] public int? NextDelayMs { get; set; }

    [Id(14)] public int Version { get; set; }
}
