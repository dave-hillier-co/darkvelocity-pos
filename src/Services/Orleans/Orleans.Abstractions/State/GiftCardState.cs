namespace DarkVelocity.Orleans.Abstractions.State;

public enum GiftCardStatus
{
    Inactive,
    Active,
    Depleted,
    Expired,
    Cancelled
}

public enum GiftCardType
{
    Physical,
    Digital,
    Promotional
}

public record GiftCardTransaction
{
    public Guid Id { get; init; }
    public GiftCardTransactionType Type { get; init; }
    public decimal Amount { get; init; }
    public decimal BalanceAfter { get; init; }
    public Guid? OrderId { get; init; }
    public Guid? PaymentId { get; init; }
    public Guid? SiteId { get; init; }
    public Guid PerformedBy { get; init; }
    public DateTime Timestamp { get; init; }
    public string? Notes { get; init; }
}

public enum GiftCardTransactionType
{
    Activation,
    Reload,
    Redemption,
    Adjustment,
    Expiration,
    Refund,
    Transfer,
    Void
}

[GenerateSerializer]
public sealed class GiftCardState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public string CardNumber { get; set; } = string.Empty;
    [Id(3)] public string? Pin { get; set; }
    [Id(4)] public GiftCardType Type { get; set; }
    [Id(5)] public GiftCardStatus Status { get; set; } = GiftCardStatus.Inactive;

    [Id(6)] public decimal InitialValue { get; set; }
    [Id(7)] public decimal CurrentBalance { get; set; }
    [Id(8)] public string Currency { get; set; } = "USD";

    [Id(9)] public DateTime? ActivatedAt { get; set; }
    [Id(10)] public DateTime? ExpiresAt { get; set; }
    [Id(11)] public Guid? ActivatedBy { get; set; }
    [Id(12)] public Guid? ActivationSiteId { get; set; }
    [Id(13)] public Guid? ActivationOrderId { get; set; }

    [Id(14)] public Guid? RecipientCustomerId { get; set; }
    [Id(15)] public string? RecipientName { get; set; }
    [Id(16)] public string? RecipientEmail { get; set; }
    [Id(17)] public string? RecipientPhone { get; set; }
    [Id(18)] public string? PersonalMessage { get; set; }

    [Id(19)] public Guid? PurchaserCustomerId { get; set; }
    [Id(20)] public string? PurchaserName { get; set; }
    [Id(21)] public string? PurchaserEmail { get; set; }

    [Id(22)] public List<GiftCardTransaction> Transactions { get; set; } = [];
    [Id(23)] public decimal TotalRedeemed { get; set; }
    [Id(24)] public decimal TotalReloaded { get; set; }
    [Id(25)] public int RedemptionCount { get; set; }

    [Id(26)] public DateTime CreatedAt { get; set; }
    [Id(27)] public DateTime? UpdatedAt { get; set; }
    [Id(28)] public DateTime? LastUsedAt { get; set; }
    [Id(29)] public Guid? LastUsedSiteId { get; set; }

    [Id(30)] public int Version { get; set; }
}
