using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

[GenerateSerializer]
public sealed class PaymentMethodState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid AccountId { get; set; }
    [Id(2)] public PaymentMethodType Type { get; set; }
    [Id(3)] public string? CustomerId { get; set; }

    // Card details (sensitive data stored securely)
    [Id(4)] public string? CardBrand { get; set; }
    [Id(5)] public string? CardLast4 { get; set; }
    [Id(6)] public int? CardExpMonth { get; set; }
    [Id(7)] public int? CardExpYear { get; set; }
    [Id(8)] public string? CardFingerprint { get; set; }
    [Id(9)] public string? CardholderName { get; set; }
    [Id(10)] public string? CardFunding { get; set; }
    [Id(11)] public string? CardCountry { get; set; }

    // Bank account details
    [Id(12)] public string? BankAccountCountry { get; set; }
    [Id(13)] public string? BankAccountCurrency { get; set; }
    [Id(14)] public string? BankAccountLast4 { get; set; }
    [Id(15)] public string? BankAccountHolderName { get; set; }
    [Id(16)] public string? BankAccountHolderType { get; set; }
    [Id(17)] public string? BankName { get; set; }
    [Id(18)] public string? BankRoutingNumber { get; set; }
    [Id(19)] public string? BankAccountStatus { get; set; }

    // Billing details
    [Id(20)] public BillingDetails? BillingDetails { get; set; }

    // Processor token (encrypted reference for actual card data)
    [Id(21)] public string? ProcessorToken { get; set; }
    [Id(22)] public string? ProcessorName { get; set; }

    // Metadata
    [Id(23)] public Dictionary<string, string>? Metadata { get; set; }

    // Timestamps
    [Id(24)] public DateTime CreatedAt { get; set; }

    [Id(25)] public int Version { get; set; }
}
