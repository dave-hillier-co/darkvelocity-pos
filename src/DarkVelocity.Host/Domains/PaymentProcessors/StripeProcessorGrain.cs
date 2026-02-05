using System.Security.Cryptography;
using System.Text;
using DarkVelocity.Host.Grains;
using Orleans.Runtime;

namespace DarkVelocity.Host.PaymentProcessors;

/// <summary>
/// Stripe payment processor grain.
/// Handles payment creation, capture, refund, and terminal operations via Stripe.
/// Key format: "{orgId}:stripe:{paymentIntentId}"
/// </summary>
public class StripeProcessorGrain : Grain, IStripeProcessorGrain
{
    private readonly IPersistentState<StripeProcessorState> _state;
    private readonly IStripeClient _stripeClient;
    private readonly IGrainFactory _grainFactory;

    public StripeProcessorGrain(
        [PersistentState("stripeProcessor", "OrleansStorage")]
        IPersistentState<StripeProcessorState> state,
        IStripeClient stripeClient,
        IGrainFactory grainFactory)
    {
        _state = state;
        _stripeClient = stripeClient;
        _grainFactory = grainFactory;
    }

    public async Task<ProcessorAuthResult> AuthorizeAsync(ProcessorAuthRequest request)
    {
        var (orgId, _, paymentIntentId) = GrainKeys.ParseOrgEntity(this.GetPrimaryKeyString());

        // Check circuit breaker
        var circuitKey = $"stripe:{orgId}";
        if (PaymentProcessorRetryHelper.IsCircuitOpen(circuitKey))
        {
            AddEvent("authorization_circuit_open", null);
            return CreateFailureResult("circuit_open", "Payment processor temporarily unavailable. Please try again later.");
        }

        // Initialize state if first attempt
        if (_state.State.PaymentIntentId == Guid.Empty)
        {
            _state.State.OrgId = orgId;
            _state.State.PaymentIntentId = paymentIntentId;
            _state.State.Amount = request.Amount;
            _state.State.Currency = request.Currency;
            _state.State.CaptureAutomatically = request.CaptureAutomatically;
            _state.State.StatementDescriptor = request.StatementDescriptor;
            _state.State.PaymentMethodId = request.PaymentMethodToken;
            _state.State.Metadata = request.Metadata;
            _state.State.CreatedAt = DateTime.UtcNow;
        }

        _state.State.LastAttemptAt = DateTime.UtcNow;
        _state.State.RetryCount++;

        // Generate or retrieve idempotency key
        var idempotencyKey = GetOrCreateIdempotencyKey("authorize");

        try
        {
            var stripeRequest = new StripePaymentIntentCreateRequest(
                Amount: request.Amount,
                Currency: request.Currency,
                PaymentMethodId: request.PaymentMethodToken,
                AutomaticCapture: request.CaptureAutomatically,
                StatementDescriptor: request.StatementDescriptor,
                CustomerId: null,
                Metadata: request.Metadata,
                ConnectedAccountId: _state.State.ConnectedAccountId,
                ApplicationFee: _state.State.ApplicationFee);

            var result = await _stripeClient.CreatePaymentIntentAsync(stripeRequest, idempotencyKey);

            if (result.Success)
            {
                _state.State.StripePaymentIntentId = result.PaymentIntentId;
                _state.State.ClientSecret = result.ClientSecret;

                if (result.NextAction != null)
                {
                    // 3DS required
                    _state.State.Status = "requires_action";
                    _state.State.NextActionType = result.NextAction.Type;
                    _state.State.NextActionRedirectUrl = result.NextAction.RedirectUrl;
                    AddEvent("3ds_required", result.PaymentIntentId);

                    PaymentProcessorRetryHelper.RecordSuccess(circuitKey);
                    await _state.WriteStateAsync();

                    return new ProcessorAuthResult(
                        Success: false,
                        TransactionId: result.PaymentIntentId,
                        AuthorizationCode: null,
                        DeclineCode: null,
                        DeclineMessage: null,
                        RequiredAction: new NextAction(
                            result.NextAction.Type,
                            result.NextAction.RedirectUrl,
                            new Dictionary<string, string> { ["payment_intent"] = result.PaymentIntentId! }),
                        NetworkTransactionId: null);
                }

                // Authorization succeeded
                _state.State.StripeChargeId = result.ChargeId;
                _state.State.AuthorizedAmount = result.Amount;

                if (request.CaptureAutomatically)
                {
                    _state.State.Status = "captured";
                    _state.State.CapturedAmount = result.Amount;
                    _state.State.CapturedAt = DateTime.UtcNow;
                    AddEvent("captured", result.PaymentIntentId);
                }
                else
                {
                    _state.State.Status = "authorized";
                    _state.State.AuthorizedAt = DateTime.UtcNow;
                    AddEvent("authorized", result.PaymentIntentId);
                }

                _state.State.LastErrorCode = null;
                _state.State.LastErrorMessage = null;

                PaymentProcessorRetryHelper.RecordSuccess(circuitKey);
                await _state.WriteStateAsync();

                return new ProcessorAuthResult(
                    Success: true,
                    TransactionId: result.PaymentIntentId,
                    AuthorizationCode: GenerateAuthCode(),
                    DeclineCode: null,
                    DeclineMessage: null,
                    RequiredAction: null,
                    NetworkTransactionId: result.ChargeId);
            }
            else
            {
                // Authorization failed
                _state.State.Status = "failed";
                _state.State.LastErrorCode = result.ErrorCode ?? result.DeclineCode;
                _state.State.LastErrorMessage = result.ErrorMessage;
                AddEvent("authorization_failed", result.ErrorCode);

                // Determine if we should retry
                var shouldRetry = PaymentProcessorRetryHelper.ShouldRetry(
                    _state.State.RetryCount,
                    result.ErrorCode);

                if (shouldRetry && PaymentProcessorRetryHelper.IsRetryableError(result.ErrorCode))
                {
                    _state.State.NextRetryAt = PaymentProcessorRetryHelper.GetNextRetryTime(_state.State.RetryCount);
                    PaymentProcessorRetryHelper.RecordFailure(circuitKey);
                }
                else if (!PaymentProcessorRetryHelper.IsTerminalError(result.ErrorCode))
                {
                    PaymentProcessorRetryHelper.RecordFailure(circuitKey);
                }

                await _state.WriteStateAsync();

                return new ProcessorAuthResult(
                    Success: false,
                    TransactionId: result.PaymentIntentId,
                    AuthorizationCode: null,
                    DeclineCode: result.DeclineCode ?? result.ErrorCode,
                    DeclineMessage: result.ErrorMessage,
                    RequiredAction: null,
                    NetworkTransactionId: null);
            }
        }
        catch (Exception ex)
        {
            _state.State.LastErrorCode = "processing_error";
            _state.State.LastErrorMessage = ex.Message;
            AddEvent("authorization_error", ex.Message);

            // Record failure for circuit breaker
            PaymentProcessorRetryHelper.RecordFailure(circuitKey);

            // Determine if we should retry
            if (PaymentProcessorRetryHelper.ShouldRetry(_state.State.RetryCount, "processing_error"))
            {
                _state.State.NextRetryAt = PaymentProcessorRetryHelper.GetNextRetryTime(_state.State.RetryCount);
            }

            await _state.WriteStateAsync();

            return CreateFailureResult("processing_error", ex.Message);
        }
    }

    public async Task<ProcessorCaptureResult> CaptureAsync(string transactionId, long? amount = null)
    {
        if (_state.State.StripePaymentIntentId != transactionId)
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
                ErrorMessage: "Capture amount exceeds authorized amount");
        }

        var idempotencyKey = GetOrCreateIdempotencyKey($"capture_{captureAmount}");
        var circuitKey = $"stripe:{_state.State.OrgId}";

        if (PaymentProcessorRetryHelper.IsCircuitOpen(circuitKey))
        {
            return new ProcessorCaptureResult(
                Success: false,
                CaptureId: null,
                CapturedAmount: 0,
                ErrorCode: "circuit_open",
                ErrorMessage: "Payment processor temporarily unavailable");
        }

        try
        {
            var result = await _stripeClient.CapturePaymentIntentAsync(
                transactionId,
                captureAmount,
                idempotencyKey);

            if (result.Success)
            {
                _state.State.CapturedAmount = captureAmount;
                _state.State.Status = "captured";
                _state.State.CapturedAt = DateTime.UtcNow;
                AddEvent("captured", result.PaymentIntentId);

                PaymentProcessorRetryHelper.RecordSuccess(circuitKey);
                await _state.WriteStateAsync();

                return new ProcessorCaptureResult(
                    Success: true,
                    CaptureId: result.ChargeId ?? $"cap_{Guid.NewGuid():N}",
                    CapturedAmount: captureAmount,
                    ErrorCode: null,
                    ErrorMessage: null);
            }
            else
            {
                PaymentProcessorRetryHelper.RecordFailure(circuitKey);
                AddEvent("capture_failed", result.ErrorCode);
                await _state.WriteStateAsync();

                return new ProcessorCaptureResult(
                    Success: false,
                    CaptureId: null,
                    CapturedAmount: 0,
                    ErrorCode: result.ErrorCode,
                    ErrorMessage: result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            PaymentProcessorRetryHelper.RecordFailure(circuitKey);
            AddEvent("capture_error", ex.Message);
            await _state.WriteStateAsync();

            return new ProcessorCaptureResult(
                Success: false,
                CaptureId: null,
                CapturedAmount: 0,
                ErrorCode: "processing_error",
                ErrorMessage: ex.Message);
        }
    }

    public async Task<ProcessorRefundResult> RefundAsync(string transactionId, long amount, string? reason = null)
    {
        if (_state.State.StripePaymentIntentId != transactionId)
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
                ErrorMessage: "Refund amount exceeds available balance");
        }

        var idempotencyKey = GetOrCreateIdempotencyKey($"refund_{amount}_{DateTime.UtcNow.Ticks}");
        var circuitKey = $"stripe:{_state.State.OrgId}";

        if (PaymentProcessorRetryHelper.IsCircuitOpen(circuitKey))
        {
            return new ProcessorRefundResult(
                Success: false,
                RefundId: null,
                RefundedAmount: 0,
                ErrorCode: "circuit_open",
                ErrorMessage: "Payment processor temporarily unavailable");
        }

        try
        {
            var result = await _stripeClient.CreateRefundAsync(
                transactionId,
                amount,
                reason,
                idempotencyKey);

            if (result.Success)
            {
                _state.State.RefundedAmount += amount;

                if (_state.State.RefundedAmount >= _state.State.CapturedAmount)
                {
                    _state.State.Status = "refunded";
                }

                AddEvent("refunded", result.RefundId);
                PaymentProcessorRetryHelper.RecordSuccess(circuitKey);
                await _state.WriteStateAsync();

                return new ProcessorRefundResult(
                    Success: true,
                    RefundId: result.RefundId,
                    RefundedAmount: amount,
                    ErrorCode: null,
                    ErrorMessage: null);
            }
            else
            {
                PaymentProcessorRetryHelper.RecordFailure(circuitKey);
                AddEvent("refund_failed", result.ErrorCode);
                await _state.WriteStateAsync();

                return new ProcessorRefundResult(
                    Success: false,
                    RefundId: null,
                    RefundedAmount: 0,
                    ErrorCode: result.ErrorCode,
                    ErrorMessage: result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            PaymentProcessorRetryHelper.RecordFailure(circuitKey);
            AddEvent("refund_error", ex.Message);
            await _state.WriteStateAsync();

            return new ProcessorRefundResult(
                Success: false,
                RefundId: null,
                RefundedAmount: 0,
                ErrorCode: "processing_error",
                ErrorMessage: ex.Message);
        }
    }

    public async Task<ProcessorVoidResult> VoidAsync(string transactionId, string? reason = null)
    {
        if (_state.State.StripePaymentIntentId != transactionId)
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

        var idempotencyKey = GetOrCreateIdempotencyKey("void");
        var circuitKey = $"stripe:{_state.State.OrgId}";

        if (PaymentProcessorRetryHelper.IsCircuitOpen(circuitKey))
        {
            return new ProcessorVoidResult(
                Success: false,
                VoidId: null,
                ErrorCode: "circuit_open",
                ErrorMessage: "Payment processor temporarily unavailable");
        }

        try
        {
            var result = await _stripeClient.CancelPaymentIntentAsync(
                transactionId,
                reason,
                idempotencyKey);

            if (result.Success)
            {
                _state.State.Status = "voided";
                _state.State.AuthorizedAmount = 0;
                _state.State.CanceledAt = DateTime.UtcNow;
                AddEvent("voided", result.PaymentIntentId);

                PaymentProcessorRetryHelper.RecordSuccess(circuitKey);
                await _state.WriteStateAsync();

                return new ProcessorVoidResult(
                    Success: true,
                    VoidId: $"void_{Guid.NewGuid():N}",
                    ErrorCode: null,
                    ErrorMessage: null);
            }
            else
            {
                PaymentProcessorRetryHelper.RecordFailure(circuitKey);
                AddEvent("void_failed", result.ErrorCode);
                await _state.WriteStateAsync();

                return new ProcessorVoidResult(
                    Success: false,
                    VoidId: null,
                    ErrorCode: result.ErrorCode,
                    ErrorMessage: result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            PaymentProcessorRetryHelper.RecordFailure(circuitKey);
            AddEvent("void_error", ex.Message);
            await _state.WriteStateAsync();

            return new ProcessorVoidResult(
                Success: false,
                VoidId: null,
                ErrorCode: "processing_error",
                ErrorMessage: ex.Message);
        }
    }

    public async Task HandleWebhookAsync(string eventType, string payload)
    {
        AddEvent(eventType, payload);

        // Handle 3DS completion
        if (eventType == "payment_intent.succeeded" && _state.State.Status == "requires_action")
        {
            _state.State.Status = _state.State.CaptureAutomatically ? "captured" : "authorized";
            _state.State.NextActionType = null;
            _state.State.NextActionRedirectUrl = null;

            if (_state.State.CaptureAutomatically)
            {
                _state.State.CapturedAmount = _state.State.AuthorizedAmount;
                _state.State.CapturedAt = DateTime.UtcNow;
            }
            else
            {
                _state.State.AuthorizedAt = DateTime.UtcNow;
            }

            // Notify the PaymentIntent grain
            await NotifyPaymentIntentGrainAsync();
        }
        else if (eventType == "payment_intent.payment_failed")
        {
            _state.State.Status = "failed";
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<ProcessorPaymentState> GetStateAsync()
    {
        return Task.FromResult(new ProcessorPaymentState(
            ProcessorName: "stripe",
            PaymentIntentId: _state.State.PaymentIntentId,
            TransactionId: _state.State.StripePaymentIntentId,
            AuthorizationCode: null, // Stripe doesn't provide auth codes in the same way
            Status: _state.State.Status,
            AuthorizedAmount: _state.State.AuthorizedAmount,
            CapturedAmount: _state.State.CapturedAmount,
            RefundedAmount: _state.State.RefundedAmount,
            RetryCount: _state.State.RetryCount,
            LastAttemptAt: _state.State.LastAttemptAt,
            LastError: _state.State.LastErrorMessage,
            Events: _state.State.Events.Select(e => new ProcessorEvent(
                e.Timestamp,
                e.EventType,
                e.ExternalEventId,
                e.Data)).ToList()));
    }

    // ========================================================================
    // Stripe-Specific Operations
    // ========================================================================

    public async Task<string> CreateSetupIntentAsync(string customerId)
    {
        var idempotencyKey = GetOrCreateIdempotencyKey($"setup_{customerId}");

        try
        {
            var result = await _stripeClient.CreateSetupIntentAsync(customerId, idempotencyKey);

            if (result.Success)
            {
                return result.ClientSecret!;
            }

            throw new InvalidOperationException($"Failed to create SetupIntent: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            AddEvent("setup_intent_error", ex.Message);
            await _state.WriteStateAsync();
            throw;
        }
    }

    public async Task HandleStripeWebhookAsync(string eventType, string stripeEventId, string payload)
    {
        AddEvent(eventType, $"event_id:{stripeEventId}");
        await HandleWebhookAsync(eventType, payload);
    }

    public async Task<ProcessorAuthResult> AuthorizeOnBehalfOfAsync(
        ProcessorAuthRequest request,
        string connectedAccountId,
        long? applicationFee = null)
    {
        // Store Connect-specific details
        _state.State.ConnectedAccountId = connectedAccountId;
        _state.State.ApplicationFee = applicationFee;

        // Delegate to standard authorize
        return await AuthorizeAsync(request);
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private string GetOrCreateIdempotencyKey(string operation)
    {
        var keyName = $"{operation}_{_state.State.RetryCount}";

        if (!_state.State.IdempotencyKeys.TryGetValue(keyName, out var key))
        {
            key = $"idem_{_state.State.PaymentIntentId:N}_{operation}_{Guid.NewGuid():N}";
            _state.State.IdempotencyKeys[keyName] = key;
        }

        return key;
    }

    private void AddEvent(string eventType, string? data)
    {
        _state.State.Events.Add(new StripeProcessorEventRecord(
            DateTime.UtcNow,
            eventType,
            $"evt_{Guid.NewGuid():N}",
            data));
    }

    private static ProcessorAuthResult CreateFailureResult(string errorCode, string errorMessage)
    {
        return new ProcessorAuthResult(
            Success: false,
            TransactionId: null,
            AuthorizationCode: null,
            DeclineCode: errorCode,
            DeclineMessage: errorMessage,
            RequiredAction: null,
            NetworkTransactionId: null);
    }

    private static string GenerateAuthCode()
    {
        return RandomNumberGenerator.GetInt32(100000, 999999).ToString();
    }

    private async Task NotifyPaymentIntentGrainAsync()
    {
        try
        {
            var key = this.GetPrimaryKeyString();
            var parts = key.Split(':');
            var accountId = Guid.Parse(parts[0]);
            var piKey = $"{accountId}:pi:{_state.State.PaymentIntentId}";
            var piGrain = _grainFactory.GetGrain<IPaymentIntentGrain>(piKey);

            if (_state.State.CaptureAutomatically)
            {
                await piGrain.RecordCaptureAsync(
                    _state.State.StripePaymentIntentId!,
                    _state.State.CapturedAmount);
            }
            else
            {
                await piGrain.RecordAuthorizationAsync(
                    _state.State.StripePaymentIntentId!,
                    GenerateAuthCode());
            }
        }
        catch
        {
            // Log but don't fail the webhook processing
        }
    }
}

// ============================================================================
// Stub Implementation for Development
// ============================================================================

/// <summary>
/// Stub Stripe client for development and testing.
/// In production, this would be replaced with actual Stripe SDK calls.
/// </summary>
public class StubStripeClient : IStripeClient
{
    public Task<StripePaymentIntentResult> CreatePaymentIntentAsync(
        StripePaymentIntentCreateRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Stripe SDK call
        // throw new NotImplementedException("Stripe SDK integration pending");

        // For development, simulate success
        var piId = $"pi_{Guid.NewGuid():N}";
        return Task.FromResult(new StripePaymentIntentResult(
            Success: true,
            PaymentIntentId: piId,
            Status: request.AutomaticCapture ? "succeeded" : "requires_capture",
            ClientSecret: $"{piId}_secret_{Guid.NewGuid():N}",
            Amount: request.Amount,
            ChargeId: $"ch_{Guid.NewGuid():N}",
            NextAction: null,
            ErrorCode: null,
            ErrorMessage: null,
            DeclineCode: null));
    }

    public Task<StripePaymentIntentResult> ConfirmPaymentIntentAsync(
        string paymentIntentId,
        string? paymentMethodId = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Stripe SDK call
        return Task.FromResult(new StripePaymentIntentResult(
            Success: true,
            PaymentIntentId: paymentIntentId,
            Status: "succeeded",
            ClientSecret: null,
            Amount: 0,
            ChargeId: $"ch_{Guid.NewGuid():N}",
            NextAction: null,
            ErrorCode: null,
            ErrorMessage: null,
            DeclineCode: null));
    }

    public Task<StripePaymentIntentResult> CapturePaymentIntentAsync(
        string paymentIntentId,
        long? amountToCapture = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Stripe SDK call
        return Task.FromResult(new StripePaymentIntentResult(
            Success: true,
            PaymentIntentId: paymentIntentId,
            Status: "succeeded",
            ClientSecret: null,
            Amount: amountToCapture ?? 0,
            ChargeId: $"ch_{Guid.NewGuid():N}",
            NextAction: null,
            ErrorCode: null,
            ErrorMessage: null,
            DeclineCode: null));
    }

    public Task<StripePaymentIntentResult> CancelPaymentIntentAsync(
        string paymentIntentId,
        string? cancellationReason = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Stripe SDK call
        return Task.FromResult(new StripePaymentIntentResult(
            Success: true,
            PaymentIntentId: paymentIntentId,
            Status: "canceled",
            ClientSecret: null,
            Amount: 0,
            ChargeId: null,
            NextAction: null,
            ErrorCode: null,
            ErrorMessage: null,
            DeclineCode: null));
    }

    public Task<StripeRefundResult> CreateRefundAsync(
        string paymentIntentId,
        long? amount = null,
        string? reason = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Stripe SDK call
        return Task.FromResult(new StripeRefundResult(
            Success: true,
            RefundId: $"re_{Guid.NewGuid():N}",
            Status: "succeeded",
            Amount: amount ?? 0,
            ErrorCode: null,
            ErrorMessage: null));
    }

    public Task<StripeSetupIntentResult> CreateSetupIntentAsync(
        string customerId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Stripe SDK call
        var siId = $"seti_{Guid.NewGuid():N}";
        return Task.FromResult(new StripeSetupIntentResult(
            Success: true,
            SetupIntentId: siId,
            ClientSecret: $"{siId}_secret_{Guid.NewGuid():N}",
            Status: "requires_payment_method",
            ErrorCode: null,
            ErrorMessage: null));
    }

    public Task<StripeTerminalReaderResult> CreateTerminalReaderAsync(
        StripeTerminalReaderCreateRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Stripe SDK call
        return Task.FromResult(new StripeTerminalReaderResult(
            Success: true,
            ReaderId: $"tmr_{Guid.NewGuid():N}",
            Label: request.Label,
            Status: "online",
            DeviceType: "stripe_m2",
            SerialNumber: $"SN{Guid.NewGuid():N}"[..16],
            ErrorCode: null,
            ErrorMessage: null));
    }

    public Task<StripeTerminalConnectionTokenResult> CreateConnectionTokenAsync(
        string? locationId = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Stripe SDK call
        return Task.FromResult(new StripeTerminalConnectionTokenResult(
            Success: true,
            Secret: $"pst_test_secret_{Guid.NewGuid():N}",
            ErrorCode: null,
            ErrorMessage: null));
    }

    public Task<StripeTerminalReaderResult> ProcessTerminalPaymentIntentAsync(
        string readerId,
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Stripe SDK call
        return Task.FromResult(new StripeTerminalReaderResult(
            Success: true,
            ReaderId: readerId,
            Label: null,
            Status: "online",
            DeviceType: null,
            SerialNumber: null,
            ErrorCode: null,
            ErrorMessage: null));
    }

    public bool VerifyWebhookSignature(string payload, string signature, string secret)
    {
        // TODO: Implement actual Stripe webhook signature verification
        // In production, use Stripe.EventUtility.VerifySignature
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
            return false;

        // Simple HMAC verification stub
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedSignature = Convert.ToHexString(hash).ToLowerInvariant();

        return signature.Contains(computedSignature);
    }
}
