namespace DarkVelocity.Host.State;

public enum SettlementBatchStatus
{
    Open,
    Closed,
    Settling,
    Settled,
    Failed
}

[GenerateSerializer]
public record BatchPaymentEntry
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public decimal Amount { get; init; }
    [Id(2)] public PaymentMethod Method { get; init; }
    [Id(3)] public string? GatewayReference { get; init; }
    [Id(4)] public DateTime AddedAt { get; init; }
}

[GenerateSerializer]
public record PaymentMethodTotal
{
    [Id(0)] public PaymentMethod Method { get; init; }
    [Id(1)] public decimal TotalAmount { get; init; }
    [Id(2)] public int Count { get; init; }
}

[GenerateSerializer]
public sealed class SettlementBatchState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public DateOnly BusinessDate { get; set; }
    [Id(4)] public string BatchNumber { get; set; } = string.Empty;
    [Id(5)] public SettlementBatchStatus Status { get; set; } = SettlementBatchStatus.Open;

    [Id(6)] public List<BatchPaymentEntry> Payments { get; set; } = [];
    [Id(7)] public decimal TotalAmount { get; set; }
    [Id(8)] public int PaymentCount { get; set; }

    // Settlement details
    [Id(9)] public string? SettlementReference { get; set; }
    [Id(10)] public decimal SettledAmount { get; set; }
    [Id(11)] public decimal ProcessingFees { get; set; }
    [Id(12)] public decimal NetAmount { get; set; }

    // Error tracking
    [Id(13)] public string? LastErrorCode { get; set; }
    [Id(14)] public string? LastErrorMessage { get; set; }
    [Id(15)] public int SettlementAttempts { get; set; }

    // Audit
    [Id(16)] public Guid OpenedBy { get; set; }
    [Id(17)] public Guid? ClosedBy { get; set; }
    [Id(18)] public DateTime CreatedAt { get; set; }
    [Id(19)] public DateTime? ClosedAt { get; set; }
    [Id(20)] public DateTime? SettledAt { get; set; }

    /// <summary>
    /// Calculates totals grouped by payment method.
    /// </summary>
    public List<PaymentMethodTotal> GetTotalsByMethod()
    {
        return Payments
            .GroupBy(p => p.Method)
            .Select(g => new PaymentMethodTotal
            {
                Method = g.Key,
                TotalAmount = g.Sum(p => p.Amount),
                Count = g.Count()
            })
            .ToList();
    }
}
