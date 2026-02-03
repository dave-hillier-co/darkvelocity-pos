using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record CreateGiftCardCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] string CardNumber,
    [property: Id(2)] GiftCardType Type,
    [property: Id(3)] decimal InitialValue,
    [property: Id(4)] string Currency = "USD",
    [property: Id(5)] DateTime? ExpiresAt = null,
    [property: Id(6)] string? Pin = null);

[GenerateSerializer]
public record ActivateGiftCardCommand(
    [property: Id(0)] Guid ActivatedBy,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid? OrderId = null,
    [property: Id(3)] Guid? PurchaserCustomerId = null,
    [property: Id(4)] string? PurchaserName = null,
    [property: Id(5)] string? PurchaserEmail = null);

[GenerateSerializer]
public record SetRecipientCommand(
    [property: Id(0)] Guid? CustomerId = null,
    [property: Id(1)] string? Name = null,
    [property: Id(2)] string? Email = null,
    [property: Id(3)] string? Phone = null,
    [property: Id(4)] string? PersonalMessage = null);

[GenerateSerializer]
public record RedeemGiftCardCommand(
    [property: Id(0)] decimal Amount,
    [property: Id(1)] Guid OrderId,
    [property: Id(2)] Guid PaymentId,
    [property: Id(3)] Guid SiteId,
    [property: Id(4)] Guid PerformedBy);

[GenerateSerializer]
public record ReloadGiftCardCommand(
    [property: Id(0)] decimal Amount,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid PerformedBy,
    [property: Id(3)] Guid? OrderId = null,
    [property: Id(4)] string? Notes = null);

[GenerateSerializer]
public record RefundToGiftCardCommand(
    [property: Id(0)] decimal Amount,
    [property: Id(1)] Guid OriginalPaymentId,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] Guid PerformedBy,
    [property: Id(4)] Guid? OriginalOrderId = null,
    [property: Id(5)] string? Notes = null);

[GenerateSerializer]
public record AdjustGiftCardCommand(
    [property: Id(0)] decimal Amount, // positive or negative
    [property: Id(1)] string Reason,
    [property: Id(2)] Guid AdjustedBy);

[GenerateSerializer]
public record GiftCardCreatedResult([property: Id(0)] Guid Id, [property: Id(1)] string CardNumber, [property: Id(2)] DateTime CreatedAt);
[GenerateSerializer]
public record GiftCardActivatedResult([property: Id(0)] decimal Balance, [property: Id(1)] DateTime ActivatedAt);
[GenerateSerializer]
public record RedemptionResult([property: Id(0)] decimal AmountRedeemed, [property: Id(1)] decimal RemainingBalance);
[GenerateSerializer]
public record GiftCardBalanceInfo([property: Id(0)] decimal CurrentBalance, [property: Id(1)] GiftCardStatus Status, [property: Id(2)] DateTime? ExpiresAt);

public interface IGiftCardGrain : IGrainWithStringKey
{
    Task<GiftCardCreatedResult> CreateAsync(CreateGiftCardCommand command);
    Task<GiftCardState> GetStateAsync();

    Task<GiftCardActivatedResult> ActivateAsync(ActivateGiftCardCommand command);
    Task SetRecipientAsync(SetRecipientCommand command);

    Task<RedemptionResult> RedeemAsync(RedeemGiftCardCommand command);
    Task<decimal> ReloadAsync(ReloadGiftCardCommand command);
    Task<decimal> RefundToCardAsync(RefundToGiftCardCommand command);
    Task<decimal> AdjustBalanceAsync(AdjustGiftCardCommand command);

    Task<bool> ValidatePinAsync(string pin);
    Task ExpireAsync();
    Task CancelAsync(string reason, Guid cancelledBy);
    Task VoidTransactionAsync(Guid transactionId, string reason, Guid voidedBy);

    // Queries
    Task<bool> ExistsAsync();
    Task<GiftCardBalanceInfo> GetBalanceInfoAsync();
    Task<bool> HasSufficientBalanceAsync(decimal amount);
    Task<IReadOnlyList<GiftCardTransaction>> GetTransactionsAsync();
}
