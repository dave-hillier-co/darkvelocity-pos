using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Mock payment processor for testing.
/// Simulates various payment scenarios based on test card numbers.
/// </summary>
public class MockProcessorGrain : Grain, IMockProcessorGrain
{
    private readonly IPersistentState<MockProcessorState> _state;
    private readonly IGrainFactory _grainFactory;

    // Test card numbers (following Stripe's test card conventions)
    private const string CardSuccess = "4242424242424242";
    private const string CardDecline = "4000000000000002";
    private const string CardInsufficientFunds = "4000000000009995";
    private const string CardExpired = "4000000000000069";
    private const string CardCvcFail = "4000000000000127";
    private const string Card3dsRequired = "4000002500003155";
    private const string CardProcessingError = "4000000000000119";

    public MockProcessorGrain(
        [PersistentState("mockProcessor", "OrleansStorage")]
        IPersistentState<MockProcessorState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public async Task<ProcessorAuthResult> AuthorizeAsync(ProcessorAuthRequest request)
    {
        _state.State.PaymentIntentId = request.PaymentIntentId;
        _state.State.LastAttemptAt = DateTime.UtcNow;
        _state.State.RetryCount++;

        // Check for configured test response
        if (_state.State.NextResponseShouldSucceed.HasValue)
        {
            var configuredSuccess = _state.State.NextResponseShouldSucceed.Value;
            var delay = _state.State.NextDelayMs;

            // Reset configuration
            _state.State.NextResponseShouldSucceed = null;
            _state.State.NextErrorCode = null;
            _state.State.NextDelayMs = null;

            if (delay.HasValue)
                await Task.Delay(delay.Value);

            if (!configuredSuccess)
            {
                var errorCode = _state.State.NextErrorCode ?? "card_declined";
                AddEvent("authorization_failed", errorCode);
                await _state.WriteStateAsync();

                return new ProcessorAuthResult(
                    Success: false,
                    TransactionId: null,
                    AuthorizationCode: null,
                    DeclineCode: errorCode,
                    DeclineMessage: GetDeclineMessage(errorCode),
                    RequiredAction: null,
                    NetworkTransactionId: null);
            }
        }

        // Simulate based on payment method token (card number)
        var paymentMethodToken = request.PaymentMethodToken;
        var cardNumber = ExtractCardNumber(paymentMethodToken);

        // Handle test card scenarios
        var result = cardNumber switch
        {
            CardDecline => CreateDeclineResult("card_declined", "Your card was declined."),
            CardInsufficientFunds => CreateDeclineResult("insufficient_funds", "Your card has insufficient funds."),
            CardExpired => CreateDeclineResult("expired_card", "Your card has expired."),
            CardCvcFail => CreateDeclineResult("incorrect_cvc", "Your card's security code is incorrect."),
            CardProcessingError => CreateDeclineResult("processing_error", "An error occurred while processing your card."),
            Card3dsRequired => Create3dsRequiredResult(request),
            _ => CreateSuccessResult(request)
        };

        if (result.Success)
        {
            _state.State.TransactionId = result.TransactionId;
            _state.State.AuthorizationCode = result.AuthorizationCode;
            _state.State.AuthorizedAmount = request.Amount;
            _state.State.Status = request.CaptureAutomatically ? "captured" : "authorized";

            if (request.CaptureAutomatically)
            {
                _state.State.CapturedAmount = request.Amount;
            }

            AddEvent("authorization_succeeded", result.TransactionId);
        }
        else if (result.RequiredAction != null)
        {
            _state.State.Status = "requires_action";
            AddEvent("3ds_required", null);
        }
        else
        {
            _state.State.Status = "failed";
            _state.State.LastError = result.DeclineMessage;
            AddEvent("authorization_failed", result.DeclineCode);
        }

        _state.State.Version++;
        await _state.WriteStateAsync();

        return result;
    }

    public async Task<ProcessorCaptureResult> CaptureAsync(string transactionId, long? amount = null)
    {
        if (_state.State.TransactionId != transactionId)
        {
            return new ProcessorCaptureResult(
                Success: false,
                CaptureId: null,
                CapturedAmount: 0,
                ErrorCode: "invalid_transaction",
                ErrorMessage: "Transaction not found");
        }

        if (_state.State.Status != "authorized")
        {
            return new ProcessorCaptureResult(
                Success: false,
                CaptureId: null,
                CapturedAmount: 0,
                ErrorCode: "invalid_state",
                ErrorMessage: $"Cannot capture transaction in {_state.State.Status} state");
        }

        var captureAmount = amount ?? _state.State.AuthorizedAmount;
        if (captureAmount > _state.State.AuthorizedAmount)
        {
            return new ProcessorCaptureResult(
                Success: false,
                CaptureId: null,
                CapturedAmount: 0,
                ErrorCode: "amount_too_large",
                ErrorMessage: $"Capture amount exceeds authorized amount");
        }

        var captureId = $"cap_{Guid.NewGuid():N}";
        _state.State.CapturedAmount = captureAmount;
        _state.State.Status = "captured";
        AddEvent("captured", captureId);

        _state.State.Version++;
        await _state.WriteStateAsync();

        return new ProcessorCaptureResult(
            Success: true,
            CaptureId: captureId,
            CapturedAmount: captureAmount,
            ErrorCode: null,
            ErrorMessage: null);
    }

    public async Task<ProcessorRefundResult> RefundAsync(string transactionId, long amount, string? reason = null)
    {
        if (_state.State.TransactionId != transactionId)
        {
            return new ProcessorRefundResult(
                Success: false,
                RefundId: null,
                RefundedAmount: 0,
                ErrorCode: "invalid_transaction",
                ErrorMessage: "Transaction not found");
        }

        if (_state.State.Status != "captured")
        {
            return new ProcessorRefundResult(
                Success: false,
                RefundId: null,
                RefundedAmount: 0,
                ErrorCode: "invalid_state",
                ErrorMessage: $"Cannot refund transaction in {_state.State.Status} state");
        }

        var availableForRefund = _state.State.CapturedAmount - _state.State.RefundedAmount;
        if (amount > availableForRefund)
        {
            return new ProcessorRefundResult(
                Success: false,
                RefundId: null,
                RefundedAmount: 0,
                ErrorCode: "amount_too_large",
                ErrorMessage: $"Refund amount exceeds available balance");
        }

        var refundId = $"re_{Guid.NewGuid():N}";
        _state.State.RefundedAmount += amount;

        if (_state.State.RefundedAmount >= _state.State.CapturedAmount)
        {
            _state.State.Status = "refunded";
        }

        AddEvent("refunded", refundId);

        _state.State.Version++;
        await _state.WriteStateAsync();

        return new ProcessorRefundResult(
            Success: true,
            RefundId: refundId,
            RefundedAmount: amount,
            ErrorCode: null,
            ErrorMessage: null);
    }

    public async Task<ProcessorVoidResult> VoidAsync(string transactionId, string? reason = null)
    {
        if (_state.State.TransactionId != transactionId)
        {
            return new ProcessorVoidResult(
                Success: false,
                VoidId: null,
                ErrorCode: "invalid_transaction",
                ErrorMessage: "Transaction not found");
        }

        if (_state.State.Status != "authorized")
        {
            return new ProcessorVoidResult(
                Success: false,
                VoidId: null,
                ErrorCode: "invalid_state",
                ErrorMessage: $"Cannot void transaction in {_state.State.Status} state");
        }

        var voidId = $"void_{Guid.NewGuid():N}";
        _state.State.Status = "voided";
        _state.State.AuthorizedAmount = 0;
        AddEvent("voided", voidId);

        _state.State.Version++;
        await _state.WriteStateAsync();

        return new ProcessorVoidResult(
            Success: true,
            VoidId: voidId,
            ErrorCode: null,
            ErrorMessage: null);
    }

    public async Task HandleWebhookAsync(string eventType, string payload)
    {
        AddEvent(eventType, payload);

        // Handle 3DS completion
        if (eventType == "next_action_completed" && _state.State.Status == "requires_action")
        {
            // Simulate successful 3DS completion
            var transactionId = $"txn_{Guid.NewGuid():N}";
            var authCode = GenerateAuthCode();

            _state.State.TransactionId = transactionId;
            _state.State.AuthorizationCode = authCode;
            _state.State.Status = "authorized";
            AddEvent("3ds_completed", transactionId);

            // Notify the PaymentIntent grain
            var key = this.GetPrimaryKeyString();
            var parts = key.Split(':');
            var accountId = Guid.Parse(parts[0]);
            var piKey = $"{accountId}:pi:{_state.State.PaymentIntentId}";
            var piGrain = _grainFactory.GetGrain<IPaymentIntentGrain>(piKey);
            await piGrain.RecordAuthorizationAsync(transactionId, authCode);
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<ProcessorPaymentState> GetStateAsync()
    {
        return Task.FromResult(new ProcessorPaymentState(
            ProcessorName: "mock",
            PaymentIntentId: _state.State.PaymentIntentId,
            TransactionId: _state.State.TransactionId,
            AuthorizationCode: _state.State.AuthorizationCode,
            Status: _state.State.Status,
            AuthorizedAmount: _state.State.AuthorizedAmount,
            CapturedAmount: _state.State.CapturedAmount,
            RefundedAmount: _state.State.RefundedAmount,
            RetryCount: _state.State.RetryCount,
            LastAttemptAt: _state.State.LastAttemptAt,
            LastError: _state.State.LastError,
            Events: _state.State.Events));
    }

    public async Task ConfigureNextResponseAsync(bool shouldSucceed, string? errorCode = null, int? delayMs = null)
    {
        _state.State.NextResponseShouldSucceed = shouldSucceed;
        _state.State.NextErrorCode = errorCode;
        _state.State.NextDelayMs = delayMs;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SimulateWebhookAsync(string eventType)
    {
        await HandleWebhookAsync(eventType, "{}");
    }

    public async Task SimulateDisputeAsync(long amount, string reason)
    {
        AddEvent("dispute_created", $"{{\"amount\":{amount},\"reason\":\"{reason}\"}}");
        _state.State.Status = "disputed";
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private void AddEvent(string eventType, string? data)
    {
        _state.State.Events.Add(new ProcessorEvent(
            DateTime.UtcNow,
            eventType,
            $"evt_{Guid.NewGuid():N}",
            data));
    }

    private static string ExtractCardNumber(string paymentMethodToken)
    {
        // Payment method tokens encode card info, extract for simulation
        // Format: "pm_card_{last4}" or just the card number for testing
        if (paymentMethodToken.StartsWith("pm_card_"))
        {
            var last4 = paymentMethodToken[8..];
            return last4 switch
            {
                "4242" => CardSuccess,
                "0002" => CardDecline,
                "9995" => CardInsufficientFunds,
                "0069" => CardExpired,
                "0127" => CardCvcFail,
                "3155" => Card3dsRequired,
                "0119" => CardProcessingError,
                _ => CardSuccess
            };
        }

        return paymentMethodToken;
    }

    private static ProcessorAuthResult CreateSuccessResult(ProcessorAuthRequest request)
    {
        var transactionId = $"txn_{Guid.NewGuid():N}";
        var authCode = GenerateAuthCode();
        var networkTxnId = $"ntxn_{Guid.NewGuid():N}";

        return new ProcessorAuthResult(
            Success: true,
            TransactionId: transactionId,
            AuthorizationCode: authCode,
            DeclineCode: null,
            DeclineMessage: null,
            RequiredAction: null,
            NetworkTransactionId: networkTxnId);
    }

    private static ProcessorAuthResult CreateDeclineResult(string declineCode, string declineMessage)
    {
        return new ProcessorAuthResult(
            Success: false,
            TransactionId: null,
            AuthorizationCode: null,
            DeclineCode: declineCode,
            DeclineMessage: declineMessage,
            RequiredAction: null,
            NetworkTransactionId: null);
    }

    private static ProcessorAuthResult Create3dsRequiredResult(ProcessorAuthRequest request)
    {
        return new ProcessorAuthResult(
            Success: false,
            TransactionId: null,
            AuthorizationCode: null,
            DeclineCode: null,
            DeclineMessage: null,
            RequiredAction: new NextAction(
                Type: "redirect_to_url",
                RedirectUrl: $"https://mock-3ds.example.com/authenticate?pi={request.PaymentIntentId}",
                Data: new Dictionary<string, string>
                {
                    ["payment_intent_id"] = request.PaymentIntentId.ToString()
                }),
            NetworkTransactionId: null);
    }

    private static string GenerateAuthCode()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }

    private static string GetDeclineMessage(string declineCode)
    {
        return declineCode switch
        {
            "card_declined" => "Your card was declined.",
            "insufficient_funds" => "Your card has insufficient funds.",
            "expired_card" => "Your card has expired.",
            "incorrect_cvc" => "Your card's security code is incorrect.",
            "processing_error" => "An error occurred while processing your card.",
            _ => "Your card was declined."
        };
    }
}
