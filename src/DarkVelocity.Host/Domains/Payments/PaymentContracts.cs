using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

public record InitiatePaymentRequest(
    Guid OrderId,
    PaymentMethod Method,
    decimal Amount,
    Guid CashierId,
    Guid? CustomerId = null,
    Guid? DrawerId = null);

public record CompleteCashRequest(decimal AmountTendered, decimal TipAmount = 0);

public record CompleteCardRequest(
    string GatewayReference,
    string AuthorizationCode,
    CardInfo CardInfo,
    string GatewayName,
    decimal TipAmount = 0);

public record VoidPaymentRequest(Guid VoidedBy, string Reason);
public record RefundPaymentRequest(decimal Amount, string Reason, Guid IssuedBy);
