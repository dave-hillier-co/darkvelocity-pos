using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record InitiatePaymentCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid OrderId,
    [property: Id(3)] PaymentMethod Method,
    [property: Id(4)] decimal Amount,
    [property: Id(5)] Guid CashierId,
    [property: Id(6)] Guid? CustomerId = null,
    [property: Id(7)] Guid? DrawerId = null);

[GenerateSerializer]
public record CompleteCashPaymentCommand(
    [property: Id(0)] decimal AmountTendered,
    [property: Id(1)] decimal TipAmount = 0);

[GenerateSerializer]
public record ProcessCardPaymentCommand(
    [property: Id(0)] string GatewayReference,
    [property: Id(1)] string AuthorizationCode,
    [property: Id(2)] CardInfo CardInfo,
    [property: Id(3)] string GatewayName,
    [property: Id(4)] decimal TipAmount = 0);

[GenerateSerializer]
public record ProcessGiftCardPaymentCommand(
    [property: Id(0)] Guid GiftCardId,
    [property: Id(1)] string CardNumber);

[GenerateSerializer]
public record VoidPaymentCommand([property: Id(0)] Guid VoidedBy, [property: Id(1)] string Reason);

[GenerateSerializer]
public record RefundPaymentCommand(
    [property: Id(0)] decimal Amount,
    [property: Id(1)] string Reason,
    [property: Id(2)] Guid IssuedBy);

[GenerateSerializer]
public record AdjustTipCommand([property: Id(0)] decimal NewTipAmount, [property: Id(1)] Guid AdjustedBy);

[GenerateSerializer]
public record PaymentInitiatedResult([property: Id(0)] Guid Id, [property: Id(1)] DateTime CreatedAt);
[GenerateSerializer]
public record PaymentCompletedResult([property: Id(0)] decimal TotalAmount, [property: Id(1)] decimal? ChangeGiven);
[GenerateSerializer]
public record RefundResult([property: Id(0)] Guid RefundId, [property: Id(1)] decimal RefundedAmount, [property: Id(2)] decimal RemainingBalance);

public interface IPaymentGrain : IGrainWithStringKey
{
    Task<PaymentInitiatedResult> InitiateAsync(InitiatePaymentCommand command);
    Task<PaymentState> GetStateAsync();

    // Payment completion by method
    Task<PaymentCompletedResult> CompleteCashAsync(CompleteCashPaymentCommand command);
    Task<PaymentCompletedResult> CompleteCardAsync(ProcessCardPaymentCommand command);
    Task<PaymentCompletedResult> CompleteGiftCardAsync(ProcessGiftCardPaymentCommand command);

    // Card authorization flow
    Task RequestAuthorizationAsync();
    Task RecordAuthorizationAsync(string authCode, string gatewayRef, CardInfo cardInfo);
    Task RecordDeclineAsync(string declineCode, string reason);
    Task CaptureAsync();

    // Modifications
    Task<RefundResult> RefundAsync(RefundPaymentCommand command);
    Task<RefundResult> PartialRefundAsync(RefundPaymentCommand command);
    Task VoidAsync(VoidPaymentCommand command);
    Task AdjustTipAsync(AdjustTipCommand command);

    // Batch management
    Task AssignToBatchAsync(Guid batchId);

    // Retry management
    Task ScheduleRetryAsync(string failureReason, int? maxRetries = null);
    Task RecordRetryAttemptAsync(bool success, string? errorCode = null, string? errorMessage = null);
    Task<bool> ShouldRetryAsync();
    Task<RetryInfo> GetRetryInfoAsync();

    Task<bool> ExistsAsync();
    Task<PaymentStatus> GetStatusAsync();
}

[GenerateSerializer]
public record RetryInfo(
    [property: Id(0)] int RetryCount,
    [property: Id(1)] int MaxRetries,
    [property: Id(2)] DateTime? NextRetryAt,
    [property: Id(3)] bool RetryExhausted,
    [property: Id(4)] string? LastErrorCode,
    [property: Id(5)] string? LastErrorMessage);

[GenerateSerializer]
public record OpenDrawerCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid UserId,
    [property: Id(3)] decimal OpeningFloat);

[GenerateSerializer]
public record RecordCashInCommand([property: Id(0)] Guid PaymentId, [property: Id(1)] decimal Amount);
[GenerateSerializer]
public record RecordCashOutCommand([property: Id(0)] decimal Amount, [property: Id(1)] string Reason, [property: Id(2)] Guid? ApprovedBy = null);
[GenerateSerializer]
public record CashDropCommand([property: Id(0)] decimal Amount, [property: Id(1)] string? Notes = null);
[GenerateSerializer]
public record CountDrawerCommand([property: Id(0)] decimal CountedAmount, [property: Id(1)] Guid CountedBy);
[GenerateSerializer]
public record CloseDrawerCommand([property: Id(0)] decimal ActualBalance, [property: Id(1)] Guid ClosedBy);

[GenerateSerializer]
public record DrawerOpenedResult([property: Id(0)] Guid Id, [property: Id(1)] DateTime OpenedAt);
[GenerateSerializer]
public record DrawerClosedResult([property: Id(0)] decimal ExpectedBalance, [property: Id(1)] decimal ActualBalance, [property: Id(2)] decimal Variance);

public interface ICashDrawerGrain : IGrainWithStringKey
{
    Task<DrawerOpenedResult> OpenAsync(OpenDrawerCommand command);
    Task<CashDrawerState> GetStateAsync();

    Task RecordCashInAsync(RecordCashInCommand command);
    Task RecordCashOutAsync(RecordCashOutCommand command);
    Task RecordDropAsync(CashDropCommand command);
    Task OpenNoSaleAsync(Guid userId, string? reason = null);

    Task CountAsync(CountDrawerCommand command);
    Task<DrawerClosedResult> CloseAsync(CloseDrawerCommand command);

    Task<bool> IsOpenAsync();
    Task<decimal> GetExpectedBalanceAsync();
    Task<DrawerStatus> GetStatusAsync();
    Task<bool> ExistsAsync();
}
