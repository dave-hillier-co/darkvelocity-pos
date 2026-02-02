using System.Security.Cryptography;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

public class PaymentIntentGrain : Grain, IPaymentIntentGrain
{
    private readonly IPersistentState<PaymentIntentState> _state;
    private readonly IGrainFactory _grainFactory;
    private Lazy<IAsyncStream<IIntegrationEvent>>? _eventStream;

    public PaymentIntentGrain(
        [PersistentState("paymentIntent", "OrleansStorage")]
        IPersistentState<PaymentIntentState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.AccountId != Guid.Empty)
        {
            InitializeStream();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    private void InitializeStream()
    {
        var accountId = _state.State.AccountId;
        _eventStream = new Lazy<IAsyncStream<IIntegrationEvent>>(() =>
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create("PaymentIntents", accountId.ToString());
            return streamProvider.GetStream<IIntegrationEvent>(streamId);
        });
    }

    private IAsyncStream<IIntegrationEvent>? EventStream => _eventStream?.Value;

    public async Task<PaymentIntentSnapshot> CreateAsync(CreatePaymentIntentCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("PaymentIntent already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var accountId = Guid.Parse(parts[0]);
        var paymentIntentId = Guid.Parse(parts[2]); // accountId:pi:paymentIntentId

        _state.State = new PaymentIntentState
        {
            Id = paymentIntentId,
            AccountId = accountId,
            Amount = command.Amount,
            Currency = command.Currency.ToLowerInvariant(),
            Description = command.Description,
            StatementDescriptor = command.StatementDescriptor,
            CaptureMethod = command.CaptureMethod,
            CustomerId = command.CustomerId,
            PaymentMethodId = command.PaymentMethodId,
            PaymentMethodTypes = command.PaymentMethodTypes ?? ["card"],
            Metadata = command.Metadata,
            ClientSecret = GenerateClientSecret(paymentIntentId),
            Status = command.PaymentMethodId != null
                ? PaymentIntentStatus.RequiresConfirmation
                : PaymentIntentStatus.RequiresPaymentMethod,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        InitializeStream();

        var snapshot = GetSnapshot();

        // Publish event
        await PublishEventAsync(new PaymentIntentCreated(
            snapshot.Id,
            snapshot.AccountId,
            snapshot.Amount,
            snapshot.Currency,
            snapshot.Status.ToString()));

        return snapshot;
    }

    public Task<PaymentIntentSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(GetSnapshot());
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.Id != Guid.Empty);

    public async Task<PaymentIntentSnapshot> ConfirmAsync(ConfirmPaymentIntentCommand command)
    {
        EnsureExists();
        EnsureCanTransition(PaymentIntentStatus.RequiresConfirmation, PaymentIntentStatus.RequiresPaymentMethod);

        // If payment method provided, attach it
        if (!string.IsNullOrEmpty(command.PaymentMethodId))
        {
            _state.State.PaymentMethodId = command.PaymentMethodId;
        }

        if (string.IsNullOrEmpty(_state.State.PaymentMethodId))
            throw new InvalidOperationException("PaymentIntent requires a payment method to confirm");

        _state.State.Status = PaymentIntentStatus.Processing;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Process the payment via the processor grain
        try
        {
            var processorGrain = GetProcessorGrain();
            var authResult = await processorGrain.AuthorizeAsync(new ProcessorAuthRequest(
                _state.State.Id,
                _state.State.Amount,
                _state.State.Currency,
                _state.State.PaymentMethodId!,
                _state.State.CaptureMethod == CaptureMethod.Automatic,
                _state.State.StatementDescriptor,
                _state.State.Metadata));

            if (authResult.RequiredAction != null)
            {
                _state.State.Status = PaymentIntentStatus.RequiresAction;
                _state.State.NextAction = authResult.RequiredAction;
            }
            else if (authResult.Success)
            {
                _state.State.ProcessorTransactionId = authResult.TransactionId;
                _state.State.ProcessorAuthorizationCode = authResult.AuthorizationCode;

                if (_state.State.CaptureMethod == CaptureMethod.Automatic)
                {
                    _state.State.Status = PaymentIntentStatus.Succeeded;
                    _state.State.AmountReceived = _state.State.Amount;
                    _state.State.SucceededAt = DateTime.UtcNow;

                    await PublishEventAsync(new PaymentIntentSucceeded(
                        _state.State.Id,
                        _state.State.AccountId,
                        _state.State.Amount,
                        _state.State.Currency));
                }
                else
                {
                    _state.State.Status = PaymentIntentStatus.RequiresCapture;
                    _state.State.AmountCapturable = _state.State.Amount;
                }
            }
            else
            {
                _state.State.Status = PaymentIntentStatus.RequiresPaymentMethod;
                _state.State.LastPaymentError = $"{authResult.DeclineCode}: {authResult.DeclineMessage}";

                await PublishEventAsync(new PaymentIntentFailed(
                    _state.State.Id,
                    _state.State.AccountId,
                    authResult.DeclineCode ?? "unknown",
                    authResult.DeclineMessage ?? "Payment failed"));
            }
        }
        catch (Exception ex)
        {
            _state.State.Status = PaymentIntentStatus.RequiresPaymentMethod;
            _state.State.LastPaymentError = ex.Message;

            await PublishEventAsync(new PaymentIntentFailed(
                _state.State.Id,
                _state.State.AccountId,
                "processing_error",
                ex.Message));
        }

        _state.State.Version++;
        await _state.WriteStateAsync();

        return GetSnapshot();
    }

    public async Task<PaymentIntentSnapshot> CaptureAsync(long? amountToCapture = null)
    {
        EnsureExists();
        EnsureCanTransition(PaymentIntentStatus.RequiresCapture);

        var captureAmount = amountToCapture ?? _state.State.AmountCapturable;

        if (captureAmount > _state.State.AmountCapturable)
            throw new InvalidOperationException($"Cannot capture {captureAmount}, only {_state.State.AmountCapturable} is capturable");

        try
        {
            var processorGrain = GetProcessorGrain();
            var captureResult = await processorGrain.CaptureAsync(
                _state.State.ProcessorTransactionId!,
                captureAmount);

            if (captureResult.Success)
            {
                _state.State.AmountReceived = captureResult.CapturedAmount;
                _state.State.AmountCapturable = 0;
                _state.State.Status = PaymentIntentStatus.Succeeded;
                _state.State.SucceededAt = DateTime.UtcNow;

                await PublishEventAsync(new PaymentIntentSucceeded(
                    _state.State.Id,
                    _state.State.AccountId,
                    captureResult.CapturedAmount,
                    _state.State.Currency));
            }
            else
            {
                _state.State.LastPaymentError = $"{captureResult.ErrorCode}: {captureResult.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            _state.State.LastPaymentError = ex.Message;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();

        return GetSnapshot();
    }

    public async Task<PaymentIntentSnapshot> CancelAsync(string? cancellationReason = null)
    {
        EnsureExists();

        if (_state.State.Status == PaymentIntentStatus.Succeeded)
            throw new InvalidOperationException("Cannot cancel a succeeded PaymentIntent");

        if (_state.State.Status == PaymentIntentStatus.Canceled)
            throw new InvalidOperationException("PaymentIntent is already canceled");

        // If there's a processor transaction, void it
        if (!string.IsNullOrEmpty(_state.State.ProcessorTransactionId))
        {
            var processorGrain = GetProcessorGrain();
            await processorGrain.VoidAsync(_state.State.ProcessorTransactionId, cancellationReason);
        }

        _state.State.Status = PaymentIntentStatus.Canceled;
        _state.State.CanceledAt = DateTime.UtcNow;
        _state.State.CancellationReason = cancellationReason;
        _state.State.AmountCapturable = 0;
        _state.State.Version++;

        await _state.WriteStateAsync();

        await PublishEventAsync(new PaymentIntentCanceled(
            _state.State.Id,
            _state.State.AccountId,
            cancellationReason));

        return GetSnapshot();
    }

    public async Task<PaymentIntentSnapshot> UpdateAsync(UpdatePaymentIntentCommand command)
    {
        EnsureExists();

        if (_state.State.Status is PaymentIntentStatus.Succeeded or PaymentIntentStatus.Canceled)
            throw new InvalidOperationException("Cannot update a succeeded or canceled PaymentIntent");

        if (command.Amount.HasValue)
        {
            _state.State.Amount = command.Amount.Value;
        }

        if (command.Description != null)
        {
            _state.State.Description = command.Description;
        }

        if (command.CustomerId != null)
        {
            _state.State.CustomerId = command.CustomerId;
        }

        if (command.Metadata != null)
        {
            _state.State.Metadata = command.Metadata;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();

        return GetSnapshot();
    }

    public async Task<PaymentIntentSnapshot> AttachPaymentMethodAsync(string paymentMethodId)
    {
        EnsureExists();

        if (_state.State.Status is PaymentIntentStatus.Succeeded or PaymentIntentStatus.Canceled)
            throw new InvalidOperationException("Cannot attach payment method to a succeeded or canceled PaymentIntent");

        _state.State.PaymentMethodId = paymentMethodId;

        if (_state.State.Status == PaymentIntentStatus.RequiresPaymentMethod)
        {
            _state.State.Status = PaymentIntentStatus.RequiresConfirmation;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();

        return GetSnapshot();
    }

    public async Task<PaymentIntentSnapshot> HandleNextActionAsync(string actionData)
    {
        EnsureExists();
        EnsureCanTransition(PaymentIntentStatus.RequiresAction);

        // Process the action result via the processor
        var processorGrain = GetProcessorGrain();
        await processorGrain.HandleWebhookAsync("next_action_completed", actionData);

        _state.State.NextAction = null;
        _state.State.Status = PaymentIntentStatus.Processing;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return GetSnapshot();
    }

    public async Task RecordAuthorizationAsync(string processorTxnId, string authCode)
    {
        EnsureExists();

        _state.State.ProcessorTransactionId = processorTxnId;
        _state.State.ProcessorAuthorizationCode = authCode;

        if (_state.State.CaptureMethod == CaptureMethod.Automatic)
        {
            _state.State.Status = PaymentIntentStatus.Succeeded;
            _state.State.AmountReceived = _state.State.Amount;
            _state.State.SucceededAt = DateTime.UtcNow;

            await PublishEventAsync(new PaymentIntentSucceeded(
                _state.State.Id,
                _state.State.AccountId,
                _state.State.Amount,
                _state.State.Currency));
        }
        else
        {
            _state.State.Status = PaymentIntentStatus.RequiresCapture;
            _state.State.AmountCapturable = _state.State.Amount;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RecordDeclineAsync(string declineCode, string declineMessage)
    {
        EnsureExists();

        _state.State.Status = PaymentIntentStatus.RequiresPaymentMethod;
        _state.State.LastPaymentError = $"{declineCode}: {declineMessage}";
        _state.State.Version++;

        await _state.WriteStateAsync();

        await PublishEventAsync(new PaymentIntentFailed(
            _state.State.Id,
            _state.State.AccountId,
            declineCode,
            declineMessage));
    }

    public async Task RecordCaptureAsync(string processorTxnId, long capturedAmount)
    {
        EnsureExists();

        _state.State.ProcessorTransactionId = processorTxnId;
        _state.State.AmountReceived = capturedAmount;
        _state.State.AmountCapturable = 0;
        _state.State.Status = PaymentIntentStatus.Succeeded;
        _state.State.SucceededAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        await PublishEventAsync(new PaymentIntentSucceeded(
            _state.State.Id,
            _state.State.AccountId,
            capturedAmount,
            _state.State.Currency));
    }

    public Task<PaymentIntentStatus> GetStatusAsync()
    {
        return Task.FromResult(_state.State.Status);
    }

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("PaymentIntent does not exist");
    }

    private void EnsureCanTransition(params PaymentIntentStatus[] allowedStatuses)
    {
        if (!allowedStatuses.Contains(_state.State.Status))
        {
            var allowed = string.Join(", ", allowedStatuses);
            throw new InvalidOperationException(
                $"Cannot perform this operation when status is {_state.State.Status}. Allowed: {allowed}");
        }
    }

    private PaymentIntentSnapshot GetSnapshot() => new(
        _state.State.Id,
        _state.State.AccountId,
        _state.State.Amount,
        _state.State.AmountCapturable,
        _state.State.AmountReceived,
        _state.State.Currency,
        _state.State.Status,
        _state.State.PaymentMethodId,
        _state.State.CustomerId,
        _state.State.Description,
        _state.State.StatementDescriptor,
        _state.State.CaptureMethod,
        _state.State.ClientSecret,
        _state.State.LastPaymentError,
        _state.State.ProcessorTransactionId,
        _state.State.NextAction,
        _state.State.Metadata,
        _state.State.CreatedAt,
        _state.State.CanceledAt,
        _state.State.SucceededAt);

    private static string GenerateClientSecret(Guid paymentIntentId)
    {
        var randomBytes = new byte[24];
        RandomNumberGenerator.Fill(randomBytes);
        var secret = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")[..24];
        return $"pi_{paymentIntentId:N}_secret_{secret}";
    }

    private IProcessorPaymentGrain GetProcessorGrain()
    {
        // Default to mock processor for now
        var processorName = _state.State.ProcessorName ?? "mock";
        var key = $"{_state.State.AccountId}:{processorName}:{_state.State.Id}";
        return _grainFactory.GetGrain<IMockProcessorGrain>(key);
    }

    private async Task PublishEventAsync(IIntegrationEvent @event)
    {
        if (EventStream == null)
            return;

        try
        {
            await EventStream.OnNextAsync(@event);
        }
        catch
        {
            // Log but don't fail the operation
        }
    }
}
