using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Grains;

public record CreateGiftCardCommand(
    Guid OrganizationId,
    string CardNumber,
    GiftCardType Type,
    decimal InitialValue,
    string Currency = "USD",
    DateTime? ExpiresAt = null,
    string? Pin = null);

public record ActivateGiftCardCommand(
    Guid ActivatedBy,
    Guid SiteId,
    Guid? OrderId = null,
    Guid? PurchaserCustomerId = null,
    string? PurchaserName = null,
    string? PurchaserEmail = null);

public record SetRecipientCommand(
    Guid? CustomerId = null,
    string? Name = null,
    string? Email = null,
    string? Phone = null,
    string? PersonalMessage = null);

public record RedeemGiftCardCommand(
    decimal Amount,
    Guid OrderId,
    Guid PaymentId,
    Guid SiteId,
    Guid PerformedBy);

public record ReloadGiftCardCommand(
    decimal Amount,
    Guid SiteId,
    Guid PerformedBy,
    Guid? OrderId = null,
    string? Notes = null);

public record RefundToGiftCardCommand(
    decimal Amount,
    Guid OriginalPaymentId,
    Guid SiteId,
    Guid PerformedBy,
    string? Notes = null);

public record AdjustGiftCardCommand(
    decimal Amount, // positive or negative
    string Reason,
    Guid AdjustedBy);

public record GiftCardCreatedResult(Guid Id, string CardNumber, DateTime CreatedAt);
public record GiftCardActivatedResult(decimal Balance, DateTime ActivatedAt);
public record RedemptionResult(decimal AmountRedeemed, decimal RemainingBalance);
public record GiftCardBalanceInfo(decimal CurrentBalance, GiftCardStatus Status, DateTime? ExpiresAt);

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
