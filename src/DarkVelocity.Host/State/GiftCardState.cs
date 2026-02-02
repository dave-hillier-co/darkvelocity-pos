using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

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

[GenerateSerializer]
public record GiftCardTransaction : ILedgerTransaction
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public GiftCardTransactionType Type { get; init; }
    [Id(2)] public decimal Amount { get; init; }
    [Id(3)] public decimal BalanceAfter { get; init; }
    [Id(4)] public Guid? OrderId { get; init; }
    [Id(5)] public Guid? PaymentId { get; init; }
    [Id(6)] public Guid? SiteId { get; init; }
    [Id(7)] public Guid PerformedBy { get; init; }
    [Id(8)] public DateTime Timestamp { get; init; }
    [Id(9)] public string? Notes { get; init; }
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
public sealed class GiftCardState : ILedgerState<GiftCardTransaction>
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

    // ILedgerState.Balance maps to CurrentBalance
    decimal ILedgerState<GiftCardTransaction>.Balance
    {
        get => CurrentBalance;
        set => CurrentBalance = value;
    }

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
