using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Grains;

public record InitiatePaymentCommand(
    Guid OrganizationId,
    Guid SiteId,
    Guid OrderId,
    PaymentMethod Method,
    decimal Amount,
    Guid CashierId,
    Guid? CustomerId = null,
    Guid? DrawerId = null);

public record CompleteCashPaymentCommand(
    decimal AmountTendered,
    decimal TipAmount = 0);

public record ProcessCardPaymentCommand(
    string GatewayReference,
    string AuthorizationCode,
    CardInfo CardInfo,
    string GatewayName,
    decimal TipAmount = 0);

public record ProcessGiftCardPaymentCommand(
    Guid GiftCardId,
    string CardNumber);

public record VoidPaymentCommand(Guid VoidedBy, string Reason);

public record RefundPaymentCommand(
    decimal Amount,
    string Reason,
    Guid IssuedBy);

public record AdjustTipCommand(decimal NewTipAmount, Guid AdjustedBy);

public record PaymentInitiatedResult(Guid Id, DateTime CreatedAt);
public record PaymentCompletedResult(decimal TotalAmount, decimal? ChangeGiven);
public record RefundResult(Guid RefundId, decimal RefundedAmount, decimal RemainingBalance);

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

    Task<bool> ExistsAsync();
    Task<PaymentStatus> GetStatusAsync();
}

public record OpenDrawerCommand(
    Guid OrganizationId,
    Guid SiteId,
    Guid UserId,
    decimal OpeningFloat);

public record RecordCashInCommand(Guid PaymentId, decimal Amount);
public record RecordCashOutCommand(decimal Amount, string Reason, Guid? ApprovedBy = null);
public record CashDropCommand(decimal Amount, string? Notes = null);
public record CountDrawerCommand(decimal CountedAmount, Guid CountedBy);
public record CloseDrawerCommand(decimal ActualBalance, Guid ClosedBy);

public record DrawerOpenedResult(Guid Id, DateTime OpenedAt);
public record DrawerClosedResult(decimal ExpectedBalance, decimal ActualBalance, decimal Variance);

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
