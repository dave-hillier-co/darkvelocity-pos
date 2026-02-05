using System.Security.Cryptography;
using System.Text;
using DarkVelocity.Host.Grains;
using Orleans.Runtime;

namespace DarkVelocity.Host.PaymentProcessors;

/// <summary>
/// Adyen payment processor grain.
/// Handles payment creation, capture, refund, and terminal operations via Adyen.
/// Key format: "{orgId}:adyen:{paymentIntentId}"
/// </summary>
public class AdyenProcessorGrain : Grain, IAdyenProcessorGrain
{
    private readonly IPersistentState<AdyenProcessorState> _state;
    private readonly IAdyenClient _adyenClient;
    private readonly IGrainFactory _grainFactory;

    public AdyenProcessorGrain(
        [PersistentState("adyenProcessor", "OrleansStorage")]
        IPersistentState<AdyenProcessorState> state,
        IAdyenClient adyenClient,
        IGrainFactory grainFactory)
    {
        _state = state;
        _adyenClient = adyenClient;
        _grainFactory = grainFactory;
    }

    public async Task<ProcessorAuthResult> AuthorizeAsync(ProcessorAuthRequest request)
    {
        var (orgId, _, paymentIntentId) = GrainKeys.ParseOrgEntity(this.GetPrimaryKeyString());

        // Check circuit breaker
        var circuitKey = $"adyen:{orgId}";
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
            _state.State.Currency = request.Currency.ToUpperInvariant();
            _state.State.CaptureDelayed = !request.CaptureAutomatically;
            _state.State.Reference = $"dv_{paymentIntentId:N}";
            _state.State.Metadata = request.Metadata;
            _state.State.CreatedAt = DateTime.UtcNow;
        }

        _state.State.LastAttemptAt = DateTime.UtcNow;
        _state.State.RetryCount++;

        // Generate or retrieve idempotency key
        var idempotencyKey = GetOrCreateIdempotencyKey("authorize");

        try
        {
            var adyenRequest = new AdyenPaymentRequest(
                Amount: request.Amount,
                Currency: request.Currency.ToUpperInvariant(),
                PaymentMethod: request.PaymentMethodToken,
                Reference: _state.State.Reference!,
                ShopperReference: _state.State.ShopperReference,
                MerchantAccount: _state.State.MerchantAccount,
                ReturnUrl: null,
                CaptureDelayed: _state.State.CaptureDelayed,
                Metadata: request.Metadata);

            var result = await _adyenClient.CreatePaymentAsync(adyenRequest, idempotencyKey);

            if (result.Success)
            {
                _state.State.PspReference = result.PspReference;
                _state.State.ResultCode = result.ResultCode;
                _state.State.AuthCode = result.AuthCode;

                if (result.Action != null)
                {
                    // 3DS or other action required
                    _state.State.Status = "requires_action";
                    _state.State.ActionType = result.Action.Type;
                    _state.State.ActionUrl = result.Action.Url;
                    _state.State.PaymentData = result.Action.PaymentData;
                    AddEvent("action_required", result.PspReference);

                    PaymentProcessorRetryHelper.RecordSuccess(circuitKey);
                    await _state.WriteStateAsync();

                    return new ProcessorAuthResult(
                        Success: false,
                        TransactionId: result.PspReference,
                        AuthorizationCode: null,
                        DeclineCode: null,
                        DeclineMessage: null,
                        RequiredAction: new NextAction(
                            result.Action.Type,
                            result.Action.Url,
                            result.Action.Data ?? new Dictionary<string, string>()),
                        NetworkTransactionId: null);
                }

                // Check result code
                if (result.ResultCode == "Authorised")
                {
                    _state.State.AuthorizedAmount = request.Amount;

                    if (request.CaptureAutomatically)
                    {
                        _state.State.Status = "captured";
                        _state.State.CapturedAmount = request.Amount;
                        _state.State.CapturedAt = DateTime.UtcNow;
                        AddEvent("captured", result.PspReference);
                    }
                    else
                    {
                        _state.State.Status = "authorized";
                        _state.State.AuthorizedAt = DateTime.UtcNow;
                        AddEvent("authorized", result.PspReference);
                    }

                    _state.State.LastErrorCode = null;
                    _state.State.LastErrorMessage = null;

                    PaymentProcessorRetryHelper.RecordSuccess(circuitKey);
                    await _state.WriteStateAsync();

                    return new ProcessorAuthResult(
                        Success: true,
                        TransactionId: result.PspReference,
                        AuthorizationCode: result.AuthCode,
                        DeclineCode: null,
                        DeclineMessage: null,
                        RequiredAction: null,
                        NetworkTransactionId: result.PspReference);
                }
                else if (result.ResultCode == "Pending" || result.ResultCode == "Received")
                {
                    _state.State.Status = "pending";
                    AddEvent("pending", result.PspReference);

                    PaymentProcessorRetryHelper.RecordSuccess(circuitKey);
                    await _state.WriteStateAsync();

                    return new ProcessorAuthResult(
                        Success: false,
                        TransactionId: result.PspReference,
                        AuthorizationCode: null,
                        DeclineCode: "pending",
                        DeclineMessage: "Payment is being processed. Please wait for confirmation.",
                        RequiredAction: null,
                        NetworkTransactionId: null);
                }
                else
                {
                    // Refused or other non-success
                    _state.State.Status = "failed";
                    _state.State.LastErrorCode = result.RefusalReasonCode;
                    _state.State.LastErrorMessage = result.RefusalReason;
                    AddEvent("authorization_refused", result.RefusalReasonCode);

                    PaymentProcessorRetryHelper.RecordSuccess(circuitKey); // API call succeeded even if payment failed
                    await _state.WriteStateAsync();

                    return new ProcessorAuthResult(
                        Success: false,
                        TransactionId: result.PspReference,
                        AuthorizationCode: null,
                        DeclineCode: result.RefusalReasonCode,
                        DeclineMessage: result.RefusalReason,
                        RequiredAction: null,
                        NetworkTransactionId: null);
                }
            }
            else
            {
                // API call failed
                _state.State.Status = "failed";
                _state.State.LastErrorCode = result.RefusalReasonCode;
                _state.State.LastErrorMessage = result.RefusalReason;
                AddEvent("authorization_failed", result.RefusalReasonCode);

                var shouldRetry = PaymentProcessorRetryHelper.ShouldRetry(
                    _state.State.RetryCount,
                    result.RefusalReasonCode);

                if (shouldRetry && PaymentProcessorRetryHelper.IsRetryableError(result.RefusalReasonCode))
                {
                    _state.State.NextRetryAt = PaymentProcessorRetryHelper.GetNextRetryTime(_state.State.RetryCount);
                    PaymentProcessorRetryHelper.RecordFailure(circuitKey);
                }

                await _state.WriteStateAsync();

                return new ProcessorAuthResult(
                    Success: false,
                    TransactionId: result.PspReference,
                    AuthorizationCode: null,
                    DeclineCode: result.RefusalReasonCode,
                    DeclineMessage: result.RefusalReason,
                    RequiredAction: null,
                    NetworkTransactionId: null);
            }
        }
        catch (Exception ex)
        {
            _state.State.LastErrorCode = "processing_error";
            _state.State.LastErrorMessage = ex.Message;
            AddEvent("authorization_error", ex.Message);

            PaymentProcessorRetryHelper.RecordFailure(circuitKey);

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
        if (_state.State.PspReference != transactionId)
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
        var circuitKey = $"adyen:{_state.State.OrgId}";

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
            var result = await _adyenClient.CapturePaymentAsync(
                transactionId,
                captureAmount,
                _state.State.Currency,
                idempotencyKey);

            if (result.Success)
            {
                _state.State.CapturedAmount = captureAmount;
                _state.State.Status = "captured";
                _state.State.CapturedAt = DateTime.UtcNow;
                AddEvent("captured", result.PspReference);

                PaymentProcessorRetryHelper.RecordSuccess(circuitKey);
                await _state.WriteStateAsync();

                return new ProcessorCaptureResult(
                    Success: true,
                    CaptureId: result.PspReference ?? $"cap_{Guid.NewGuid():N}",
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
        if (_state.State.PspReference != transactionId)
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
        var circuitKey = $"adyen:{_state.State.OrgId}";

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
            var result = await _adyenClient.RefundPaymentAsync(
                transactionId,
                amount,
                _state.State.Currency,
                reason,
                idempotencyKey);

            if (result.Success)
            {
                _state.State.RefundedAmount += amount;

                if (_state.State.RefundedAmount >= _state.State.CapturedAmount)
                {
                    _state.State.Status = "refunded";
                }

                AddEvent("refunded", result.PspReference);
                PaymentProcessorRetryHelper.RecordSuccess(circuitKey);
                await _state.WriteStateAsync();

                return new ProcessorRefundResult(
                    Success: true,
                    RefundId: result.PspReference,
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
        if (_state.State.PspReference != transactionId)
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
        var circuitKey = $"adyen:{_state.State.OrgId}";

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
            var result = await _adyenClient.CancelPaymentAsync(transactionId, idempotencyKey);

            if (result.Success)
            {
                _state.State.Status = "voided";
                _state.State.AuthorizedAmount = 0;
                _state.State.CanceledAt = DateTime.UtcNow;
                AddEvent("voided", result.PspReference);

                PaymentProcessorRetryHelper.RecordSuccess(circuitKey);
                await _state.WriteStateAsync();

                return new ProcessorVoidResult(
                    Success: true,
                    VoidId: result.PspReference ?? $"void_{Guid.NewGuid():N}",
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

        // Handle notification types
        switch (eventType)
        {
            case "AUTHORISATION" when _state.State.Status == "pending" || _state.State.Status == "requires_action":
                // Delayed authorization completed
                _state.State.Status = _state.State.CaptureDelayed ? "authorized" : "captured";
                _state.State.AuthorizedAmount = _state.State.Amount;

                if (!_state.State.CaptureDelayed)
                {
                    _state.State.CapturedAmount = _state.State.Amount;
                    _state.State.CapturedAt = DateTime.UtcNow;
                }
                else
                {
                    _state.State.AuthorizedAt = DateTime.UtcNow;
                }

                await NotifyPaymentIntentGrainAsync();
                break;

            case "CAPTURE":
                _state.State.Status = "captured";
                _state.State.CapturedAt = DateTime.UtcNow;
                break;

            case "REFUND":
                // Handled separately via notification pspReference
                break;

            case "CANCELLATION":
                _state.State.Status = "voided";
                _state.State.CanceledAt = DateTime.UtcNow;
                break;

            case "CHARGEBACK":
                _state.State.Status = "disputed";
                break;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<ProcessorPaymentState> GetStateAsync()
    {
        return Task.FromResult(new ProcessorPaymentState(
            ProcessorName: "adyen",
            PaymentIntentId: _state.State.PaymentIntentId,
            TransactionId: _state.State.PspReference,
            AuthorizationCode: _state.State.AuthCode,
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
                e.PspReference,
                e.Data)).ToList()));
    }

    // ========================================================================
    // Adyen-Specific Operations
    // ========================================================================

    public async Task<ProcessorAuthResult> AuthorizeWithSplitAsync(
        ProcessorAuthRequest request,
        List<AdyenSplitItem> splits)
    {
        // Store split configuration
        _state.State.Splits = splits.Select(s => new AdyenSplitRecord(
            s.Account,
            s.Amount,
            s.Type,
            s.Reference)).ToList();

        var (orgId, _, paymentIntentId) = GrainKeys.ParseOrgEntity(this.GetPrimaryKeyString());
        var idempotencyKey = GetOrCreateIdempotencyKey("authorize_split");
        var circuitKey = $"adyen:{orgId}";

        if (PaymentProcessorRetryHelper.IsCircuitOpen(circuitKey))
        {
            return CreateFailureResult("circuit_open", "Payment processor temporarily unavailable");
        }

        // Initialize state
        if (_state.State.PaymentIntentId == Guid.Empty)
        {
            _state.State.OrgId = orgId;
            _state.State.PaymentIntentId = paymentIntentId;
            _state.State.Amount = request.Amount;
            _state.State.Currency = request.Currency.ToUpperInvariant();
            _state.State.CaptureDelayed = !request.CaptureAutomatically;
            _state.State.Reference = $"dv_{paymentIntentId:N}";
            _state.State.Metadata = request.Metadata;
            _state.State.CreatedAt = DateTime.UtcNow;
        }

        _state.State.LastAttemptAt = DateTime.UtcNow;
        _state.State.RetryCount++;

        try
        {
            var adyenRequest = new AdyenPaymentRequest(
                Amount: request.Amount,
                Currency: request.Currency.ToUpperInvariant(),
                PaymentMethod: request.PaymentMethodToken,
                Reference: _state.State.Reference!,
                ShopperReference: _state.State.ShopperReference,
                MerchantAccount: _state.State.MerchantAccount,
                ReturnUrl: null,
                CaptureDelayed: _state.State.CaptureDelayed,
                Metadata: request.Metadata);

            var adyenSplits = splits.Select(s => new AdyenSplitAmount(
                s.Account,
                s.Amount,
                s.Type,
                s.Reference)).ToList();

            var result = await _adyenClient.CreateSplitPaymentAsync(adyenRequest, adyenSplits, idempotencyKey);

            // Process result similar to AuthorizeAsync
            if (result.Success && result.ResultCode == "Authorised")
            {
                _state.State.PspReference = result.PspReference;
                _state.State.ResultCode = result.ResultCode;
                _state.State.AuthCode = result.AuthCode;
                _state.State.AuthorizedAmount = request.Amount;

                if (request.CaptureAutomatically)
                {
                    _state.State.Status = "captured";
                    _state.State.CapturedAmount = request.Amount;
                    _state.State.CapturedAt = DateTime.UtcNow;
                }
                else
                {
                    _state.State.Status = "authorized";
                    _state.State.AuthorizedAt = DateTime.UtcNow;
                }

                AddEvent("split_authorized", result.PspReference);
                PaymentProcessorRetryHelper.RecordSuccess(circuitKey);
                await _state.WriteStateAsync();

                return new ProcessorAuthResult(
                    Success: true,
                    TransactionId: result.PspReference,
                    AuthorizationCode: result.AuthCode,
                    DeclineCode: null,
                    DeclineMessage: null,
                    RequiredAction: null,
                    NetworkTransactionId: result.PspReference);
            }
            else
            {
                _state.State.Status = "failed";
                _state.State.LastErrorCode = result.RefusalReasonCode;
                _state.State.LastErrorMessage = result.RefusalReason;
                AddEvent("split_authorization_failed", result.RefusalReasonCode);

                await _state.WriteStateAsync();

                return CreateFailureResult(
                    result.RefusalReasonCode ?? "unknown",
                    result.RefusalReason ?? "Split payment failed");
            }
        }
        catch (Exception ex)
        {
            _state.State.LastErrorCode = "processing_error";
            _state.State.LastErrorMessage = ex.Message;
            AddEvent("split_authorization_error", ex.Message);

            PaymentProcessorRetryHelper.RecordFailure(circuitKey);
            await _state.WriteStateAsync();

            return CreateFailureResult("processing_error", ex.Message);
        }
    }

    public async Task HandleAdyenNotificationAsync(string notificationType, string pspReference, string payload)
    {
        AddEvent($"notification_{notificationType}", pspReference);

        // Update state based on notification
        _state.State.PspReference ??= pspReference;

        await HandleWebhookAsync(notificationType, payload);
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
        _state.State.Events.Add(new AdyenProcessorEventRecord(
            DateTime.UtcNow,
            eventType,
            _state.State.PspReference,
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

    private async Task NotifyPaymentIntentGrainAsync()
    {
        try
        {
            var key = this.GetPrimaryKeyString();
            var parts = key.Split(':');
            var accountId = Guid.Parse(parts[0]);
            var piKey = $"{accountId}:pi:{_state.State.PaymentIntentId}";
            var piGrain = _grainFactory.GetGrain<IPaymentIntentGrain>(piKey);

            if (_state.State.CaptureDelayed)
            {
                await piGrain.RecordAuthorizationAsync(
                    _state.State.PspReference!,
                    _state.State.AuthCode ?? "");
            }
            else
            {
                await piGrain.RecordCaptureAsync(
                    _state.State.PspReference!,
                    _state.State.CapturedAmount);
            }
        }
        catch
        {
            // Log but don't fail the notification processing
        }
    }
}

// ============================================================================
// Stub Implementation for Development
// ============================================================================

/// <summary>
/// Stub Adyen client for development and testing.
/// In production, this would be replaced with actual Adyen SDK calls.
/// </summary>
public class StubAdyenClient : IAdyenClient
{
    public Task<AdyenPaymentResult> CreatePaymentAsync(
        AdyenPaymentRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Adyen SDK call
        // throw new NotImplementedException("Adyen SDK integration pending");

        var pspRef = $"{RandomNumberGenerator.GetInt32(1000000000, int.MaxValue)}";
        return Task.FromResult(new AdyenPaymentResult(
            Success: true,
            PspReference: pspRef,
            ResultCode: "Authorised",
            AuthCode: RandomNumberGenerator.GetInt32(100000, 999999).ToString(),
            Action: null,
            RefusalReason: null,
            RefusalReasonCode: null,
            AdditionalData: null));
    }

    public Task<AdyenCaptureResult> CapturePaymentAsync(
        string pspReference,
        long amount,
        string currency,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Adyen SDK call
        return Task.FromResult(new AdyenCaptureResult(
            Success: true,
            PspReference: $"{RandomNumberGenerator.GetInt32(1000000000, int.MaxValue)}",
            Status: "[capture-received]",
            ErrorCode: null,
            ErrorMessage: null));
    }

    public Task<AdyenRefundResult> RefundPaymentAsync(
        string pspReference,
        long amount,
        string currency,
        string? reason = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Adyen SDK call
        return Task.FromResult(new AdyenRefundResult(
            Success: true,
            PspReference: $"{RandomNumberGenerator.GetInt32(1000000000, int.MaxValue)}",
            Status: "[refund-received]",
            ErrorCode: null,
            ErrorMessage: null));
    }

    public Task<AdyenCancelResult> CancelPaymentAsync(
        string pspReference,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Adyen SDK call
        return Task.FromResult(new AdyenCancelResult(
            Success: true,
            PspReference: $"{RandomNumberGenerator.GetInt32(1000000000, int.MaxValue)}",
            Status: "[cancel-received]",
            ErrorCode: null,
            ErrorMessage: null));
    }

    public Task<AdyenPaymentResult> CreateSplitPaymentAsync(
        AdyenPaymentRequest request,
        List<AdyenSplitAmount> splits,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Adyen SDK call with splits
        var pspRef = $"{RandomNumberGenerator.GetInt32(1000000000, int.MaxValue)}";
        return Task.FromResult(new AdyenPaymentResult(
            Success: true,
            PspReference: pspRef,
            ResultCode: "Authorised",
            AuthCode: RandomNumberGenerator.GetInt32(100000, 999999).ToString(),
            Action: null,
            RefusalReason: null,
            RefusalReasonCode: null,
            AdditionalData: new Dictionary<string, string> { ["splits"] = "applied" }));
    }

    public Task<AdyenTerminalResult> RegisterTerminalAsync(
        AdyenTerminalRegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Adyen SDK call
        return Task.FromResult(new AdyenTerminalResult(
            Success: true,
            TerminalId: request.TerminalId,
            Status: "boarded",
            ErrorCode: null,
            ErrorMessage: null));
    }

    public Task<AdyenTerminalPaymentResult> ProcessTerminalPaymentAsync(
        string terminalId,
        AdyenTerminalPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Adyen SDK call
        return Task.FromResult(new AdyenTerminalPaymentResult(
            Success: true,
            PspReference: $"{RandomNumberGenerator.GetInt32(1000000000, int.MaxValue)}",
            ResultCode: "Authorised",
            AuthCode: RandomNumberGenerator.GetInt32(100000, 999999).ToString(),
            ErrorCode: null,
            ErrorMessage: null));
    }

    public bool VerifyHmacSignature(string payload, string hmacSignature, string hmacKey)
    {
        // TODO: Implement actual Adyen HMAC verification
        if (string.IsNullOrEmpty(hmacSignature) || string.IsNullOrEmpty(hmacKey))
            return false;

        using var hmac = new HMACSHA256(Convert.FromBase64String(hmacKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedSignature = Convert.ToBase64String(hash);

        return hmacSignature == computedSignature;
    }
}
