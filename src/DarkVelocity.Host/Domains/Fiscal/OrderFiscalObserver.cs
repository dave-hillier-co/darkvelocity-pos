using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Domains.Fiscal;

/// <summary>
/// Configuration for order-fiscal integration.
/// </summary>
[GenerateSerializer]
public sealed class OrderFiscalIntegrationConfig
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public bool Enabled { get; set; }
    [Id(3)] public Guid DefaultDeviceId { get; set; }
    [Id(4)] public bool AutoSign { get; set; }
    [Id(5)] public FiscalProcessType DefaultProcessType { get; set; } = FiscalProcessType.Kassenbeleg;
    [Id(6)] public int Version { get; set; }
}

/// <summary>
/// State for the order-fiscal integration grain.
/// </summary>
[GenerateSerializer]
public sealed class OrderFiscalIntegrationState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public OrderFiscalIntegrationConfig Config { get; set; } = new();
    [Id(3)] public Dictionary<Guid, Guid> OrderToTransactionMap { get; set; } = [];
    [Id(4)] public StreamSubscriptionHandle<IStreamEvent>? SubscriptionHandle { get; set; }
    [Id(5)] public int Version { get; set; }
}

/// <summary>
/// Command to configure order-fiscal integration.
/// </summary>
[GenerateSerializer]
public record ConfigureOrderFiscalIntegrationCommand(
    [property: Id(0)] bool Enabled,
    [property: Id(1)] Guid DefaultDeviceId,
    [property: Id(2)] bool AutoSign,
    [property: Id(3)] FiscalProcessType DefaultProcessType);

/// <summary>
/// Interface for order-fiscal integration grain.
/// Listens to order events and creates fiscal transactions automatically.
/// Key: "{orgId}:{siteId}:orderfiscal"
/// </summary>
public interface IOrderFiscalIntegrationGrain : IGrainWithStringKey
{
    /// <summary>
    /// Configure the integration.
    /// </summary>
    Task<OrderFiscalIntegrationConfig> ConfigureAsync(ConfigureOrderFiscalIntegrationCommand command);

    /// <summary>
    /// Get current configuration.
    /// </summary>
    Task<OrderFiscalIntegrationConfig> GetConfigAsync();

    /// <summary>
    /// Start listening for order events.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stop listening for order events.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Manually create a fiscal transaction for an order.
    /// </summary>
    Task<FiscalTransactionSnapshot> CreateTransactionForOrderAsync(
        Guid orderId,
        Guid? deviceId = null);

    /// <summary>
    /// Get the fiscal transaction ID for an order.
    /// </summary>
    Task<Guid?> GetTransactionIdForOrderAsync(Guid orderId);
}

/// <summary>
/// Grain that integrates orders with fiscal transactions.
/// Subscribes to order stream and creates fiscal transactions when orders are paid/closed.
/// </summary>
public class OrderFiscalIntegrationGrain : Grain, IOrderFiscalIntegrationGrain, IAsyncObserver<IStreamEvent>
{
    private readonly IPersistentState<OrderFiscalIntegrationState> _state;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<OrderFiscalIntegrationGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscriptionHandle;

    public OrderFiscalIntegrationGrain(
        [PersistentState("orderFiscalIntegration", "OrleansStorage")]
        IPersistentState<OrderFiscalIntegrationState> state,
        IGrainFactory grainFactory,
        ILogger<OrderFiscalIntegrationGrain> logger)
    {
        _state = state;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.OrgId == Guid.Empty)
        {
            var key = this.GetPrimaryKeyString();
            var parts = key.Split(':');
            _state.State.OrgId = Guid.Parse(parts[0]);
            _state.State.SiteId = Guid.Parse(parts[1]);
        }

        // Resume subscription if previously active
        if (_state.State.Config.Enabled)
        {
            await SubscribeToOrderStreamAsync();
        }

        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        if (_subscriptionHandle != null)
        {
            await _subscriptionHandle.UnsubscribeAsync();
            _subscriptionHandle = null;
        }

        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task<OrderFiscalIntegrationConfig> ConfigureAsync(ConfigureOrderFiscalIntegrationCommand command)
    {
        _state.State.Config = new OrderFiscalIntegrationConfig
        {
            OrgId = _state.State.OrgId,
            SiteId = _state.State.SiteId,
            Enabled = command.Enabled,
            DefaultDeviceId = command.DefaultDeviceId,
            AutoSign = command.AutoSign,
            DefaultProcessType = command.DefaultProcessType,
            Version = _state.State.Config.Version + 1
        };

        _state.State.Version++;
        await _state.WriteStateAsync();

        if (command.Enabled)
        {
            await SubscribeToOrderStreamAsync();
        }
        else
        {
            await UnsubscribeFromOrderStreamAsync();
        }

        return _state.State.Config;
    }

    public Task<OrderFiscalIntegrationConfig> GetConfigAsync()
    {
        return Task.FromResult(_state.State.Config);
    }

    public async Task StartAsync()
    {
        if (!_state.State.Config.Enabled)
        {
            _state.State.Config.Enabled = true;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }

        await SubscribeToOrderStreamAsync();
    }

    public async Task StopAsync()
    {
        await UnsubscribeFromOrderStreamAsync();

        _state.State.Config.Enabled = false;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task<FiscalTransactionSnapshot> CreateTransactionForOrderAsync(
        Guid orderId,
        Guid? deviceId = null)
    {
        var effectiveDeviceId = deviceId ?? _state.State.Config.DefaultDeviceId;
        if (effectiveDeviceId == Guid.Empty)
        {
            throw new InvalidOperationException("No fiscal device configured");
        }

        // Get order details
        var orderKey = GrainKeys.Order(_state.State.OrgId, _state.State.SiteId, orderId);
        var orderGrain = _grainFactory.GetGrain<IOrderGrain>(orderKey);
        var order = await orderGrain.GetStateAsync();

        // Check if transaction already exists
        if (_state.State.OrderToTransactionMap.TryGetValue(orderId, out var existingTxId))
        {
            var existingTxGrain = _grainFactory.GetGrain<IFiscalTransactionGrain>(
                GrainKeys.FiscalTransaction(_state.State.OrgId, _state.State.SiteId, existingTxId));
            return await existingTxGrain.GetSnapshotAsync();
        }

        // Create fiscal transaction
        var transactionId = Guid.NewGuid();
        var txGrain = _grainFactory.GetGrain<IFiscalTransactionGrain>(
            GrainKeys.FiscalTransaction(_state.State.OrgId, _state.State.SiteId, transactionId));

        var netAmounts = CalculateNetAmounts(order);
        var taxAmounts = CalculateTaxAmounts(order);
        var paymentTypes = CalculatePaymentTypes(order);

        var command = new CreateFiscalTransactionCommand(
            FiscalDeviceId: effectiveDeviceId,
            LocationId: _state.State.SiteId,
            TransactionType: FiscalTransactionType.Receipt,
            ProcessType: _state.State.Config.DefaultProcessType,
            SourceType: "Order",
            SourceId: orderId,
            GrossAmount: order.GrandTotal,
            NetAmounts: netAmounts,
            TaxAmounts: taxAmounts,
            PaymentTypes: paymentTypes);

        var snapshot = await txGrain.CreateAsync(command);

        // Register transaction
        var registryGrain = _grainFactory.GetGrain<IFiscalTransactionRegistryGrain>(
            GrainKeys.FiscalTransactionRegistry(_state.State.OrgId, _state.State.SiteId));
        await registryGrain.RegisterTransactionAsync(
            transactionId,
            effectiveDeviceId,
            DateOnly.FromDateTime(DateTime.UtcNow));

        // Track mapping
        _state.State.OrderToTransactionMap[orderId] = transactionId;
        _state.State.Version++;
        await _state.WriteStateAsync();

        // Log to fiscal journal
        await LogToJournalAsync(
            FiscalEventType.TransactionCreated,
            $"Created fiscal transaction {transactionId} for order {orderId}",
            effectiveDeviceId,
            transactionId);

        // Auto-sign if enabled
        if (_state.State.Config.AutoSign)
        {
            await SignTransactionAsync(snapshot);
        }

        return snapshot;
    }

    public Task<Guid?> GetTransactionIdForOrderAsync(Guid orderId)
    {
        _state.State.OrderToTransactionMap.TryGetValue(orderId, out var transactionId);
        return Task.FromResult(transactionId == Guid.Empty ? null : (Guid?)transactionId);
    }

    // ========================================================================
    // Stream Observer Implementation
    // ========================================================================

    public async Task OnNextAsync(IStreamEvent item, StreamSequenceToken? token = null)
    {
        try
        {
            switch (item)
            {
                case OrderCompletedEvent completed:
                    await HandleOrderCompletedAsync(completed);
                    break;

                case OrderVoidedEvent voided:
                    await HandleOrderVoidedAsync(voided);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order event for fiscal integration");
            await LogToJournalAsync(
                FiscalEventType.Error,
                $"Error processing event: {ex.Message}",
                null,
                null,
                FiscalEventSeverity.Error);
        }
    }

    public Task OnCompletedAsync()
    {
        _logger.LogInformation("Order stream completed for fiscal integration");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in order stream for fiscal integration");
        return Task.CompletedTask;
    }

    // ========================================================================
    // Private Methods
    // ========================================================================

    private async Task SubscribeToOrderStreamAsync()
    {
        if (_subscriptionHandle != null)
            return;

        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.OrderStreamNamespace, _state.State.OrgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        _subscriptionHandle = await stream.SubscribeAsync(this);
        _logger.LogInformation(
            "Subscribed to order stream for org {OrgId}, site {SiteId}",
            _state.State.OrgId, _state.State.SiteId);
    }

    private async Task UnsubscribeFromOrderStreamAsync()
    {
        if (_subscriptionHandle == null)
            return;

        await _subscriptionHandle.UnsubscribeAsync();
        _subscriptionHandle = null;
        _logger.LogInformation(
            "Unsubscribed from order stream for org {OrgId}, site {SiteId}",
            _state.State.OrgId, _state.State.SiteId);
    }

    private async Task HandleOrderCompletedAsync(OrderCompletedEvent evt)
    {
        if (!_state.State.Config.Enabled)
            return;

        // Check if this order is for our site
        if (evt.SiteId != _state.State.SiteId)
            return;

        // Check if transaction already exists
        if (_state.State.OrderToTransactionMap.ContainsKey(evt.OrderId))
            return;

        try
        {
            await CreateTransactionForOrderAsync(evt.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create fiscal transaction for completed order {OrderId}", evt.OrderId);
        }
    }

    private async Task HandleOrderVoidedAsync(OrderVoidedEvent evt)
    {
        if (!_state.State.Config.Enabled)
            return;

        // Check if this order is for our site
        if (evt.SiteId != _state.State.SiteId)
            return;

        // Check if original transaction exists
        if (!_state.State.OrderToTransactionMap.TryGetValue(evt.OrderId, out var originalTxId))
            return;

        try
        {
            // Create void transaction
            var voidTxId = Guid.NewGuid();
            var voidTxGrain = _grainFactory.GetGrain<IFiscalTransactionGrain>(
                GrainKeys.FiscalTransaction(_state.State.OrgId, _state.State.SiteId, voidTxId));

            // Get original transaction for amounts
            var originalTxGrain = _grainFactory.GetGrain<IFiscalTransactionGrain>(
                GrainKeys.FiscalTransaction(_state.State.OrgId, _state.State.SiteId, originalTxId));
            var originalTx = await originalTxGrain.GetSnapshotAsync();

            var command = new CreateFiscalTransactionCommand(
                FiscalDeviceId: originalTx.FiscalDeviceId,
                LocationId: _state.State.SiteId,
                TransactionType: FiscalTransactionType.Void,
                ProcessType: FiscalProcessType.AVSonstiger,
                SourceType: "Order",
                SourceId: evt.OrderId,
                GrossAmount: -originalTx.GrossAmount,
                NetAmounts: originalTx.NetAmounts.ToDictionary(kv => kv.Key, kv => -kv.Value),
                TaxAmounts: originalTx.TaxAmounts.ToDictionary(kv => kv.Key, kv => -kv.Value),
                PaymentTypes: originalTx.PaymentTypes.ToDictionary(kv => kv.Key, kv => -kv.Value));

            await voidTxGrain.CreateAsync(command);

            // Register void transaction
            var registryGrain = _grainFactory.GetGrain<IFiscalTransactionRegistryGrain>(
                GrainKeys.FiscalTransactionRegistry(_state.State.OrgId, _state.State.SiteId));
            await registryGrain.RegisterTransactionAsync(
                voidTxId,
                originalTx.FiscalDeviceId,
                DateOnly.FromDateTime(DateTime.UtcNow));

            // Log to journal
            await LogToJournalAsync(
                FiscalEventType.TransactionVoided,
                $"Created void transaction {voidTxId} for original transaction {originalTxId} (order {evt.OrderId}, reason: {evt.Reason})",
                originalTx.FiscalDeviceId,
                voidTxId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create void fiscal transaction for order {OrderId}", evt.OrderId);
        }
    }

    private async Task SignTransactionAsync(FiscalTransactionSnapshot transaction)
    {
        // Get TSE grain for signing
        var tseKey = $"{_state.State.OrgId}:tse:{transaction.FiscalDeviceId}";
        var tseGrain = _grainFactory.GetGrain<ITseGrain>(tseKey);

        try
        {
            // Start TSE transaction
            var startResult = await tseGrain.StartTransactionAsync(new StartTseTransactionCommand(
                LocationId: _state.State.SiteId,
                ProcessType: transaction.ProcessType.ToString(),
                ProcessData: $"ORDER:{transaction.SourceId}",
                ClientId: null));

            if (!startResult.Success)
            {
                _logger.LogWarning("Failed to start TSE transaction: {Error}", startResult.ErrorMessage);
                return;
            }

            // Finish TSE transaction
            var finishResult = await tseGrain.FinishTransactionAsync(new FinishTseTransactionCommand(
                TransactionNumber: startResult.TransactionNumber,
                ProcessType: transaction.ProcessType.ToString(),
                ProcessData: $"ORDER:{transaction.SourceId};GROSS:{transaction.GrossAmount}"));

            if (finishResult.Success)
            {
                // Update fiscal transaction with signature
                var txGrain = _grainFactory.GetGrain<IFiscalTransactionGrain>(
                    GrainKeys.FiscalTransaction(_state.State.OrgId, _state.State.SiteId, transaction.FiscalTransactionId));

                await txGrain.SignAsync(new SignTransactionCommand(
                    Signature: finishResult.Signature,
                    SignatureCounter: finishResult.SignatureCounter,
                    CertificateSerial: finishResult.CertificateSerial,
                    QrCodeData: finishResult.QrCodeData,
                    TseResponseRaw: null));

                await LogToJournalAsync(
                    FiscalEventType.TransactionSigned,
                    $"Signed transaction {transaction.FiscalTransactionId}",
                    transaction.FiscalDeviceId,
                    transaction.FiscalTransactionId);
            }
            else
            {
                _logger.LogWarning("Failed to finish TSE transaction: {Error}", finishResult.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign fiscal transaction {TransactionId}", transaction.FiscalTransactionId);
        }
    }

    private async Task LogToJournalAsync(
        FiscalEventType eventType,
        string details,
        Guid? deviceId,
        Guid? transactionId,
        FiscalEventSeverity severity = FiscalEventSeverity.Info)
    {
        var journalGrain = _grainFactory.GetGrain<IFiscalJournalGrain>(
            GrainKeys.FiscalJournal(_state.State.OrgId, _state.State.SiteId, DateOnly.FromDateTime(DateTime.UtcNow)));

        await journalGrain.LogEventAsync(new LogFiscalEventCommand(
            LocationId: _state.State.SiteId,
            EventType: eventType,
            DeviceId: deviceId,
            TransactionId: transactionId,
            ExportId: null,
            Details: details,
            IpAddress: null,
            UserId: null,
            Severity: severity));
    }

    private static Dictionary<string, decimal> CalculateNetAmounts(OrderState order)
    {
        var netAmounts = new Dictionary<string, decimal>();

        foreach (var line in order.Lines.Where(l => l.Status != OrderLineStatus.Voided))
        {
            var vatKey = line.TaxRate >= 15 ? "NORMAL" : line.TaxRate >= 5 ? "REDUCED" : "NULL";
            var netAmount = line.LineTotal - line.TaxAmount;

            if (netAmounts.TryGetValue(vatKey, out var existing))
                netAmounts[vatKey] = existing + netAmount;
            else
                netAmounts[vatKey] = netAmount;
        }

        return netAmounts;
    }

    private static Dictionary<string, decimal> CalculateTaxAmounts(OrderState order)
    {
        var taxAmounts = new Dictionary<string, decimal>();

        foreach (var line in order.Lines.Where(l => l.Status != OrderLineStatus.Voided))
        {
            var vatKey = line.TaxRate >= 15 ? "NORMAL" : line.TaxRate >= 5 ? "REDUCED" : "NULL";

            if (taxAmounts.TryGetValue(vatKey, out var existing))
                taxAmounts[vatKey] = existing + line.TaxAmount;
            else
                taxAmounts[vatKey] = line.TaxAmount;
        }

        return taxAmounts;
    }

    private static Dictionary<string, decimal> CalculatePaymentTypes(OrderState order)
    {
        var paymentTypes = new Dictionary<string, decimal>();

        foreach (var payment in order.Payments)
        {
            var paymentKey = payment.Method.ToUpperInvariant() switch
            {
                "CASH" => "CASH",
                "CARD" => "CARD",
                "CREDIT" => "CARD",
                "DEBIT" => "CARD",
                _ => "OTHER"
            };

            if (paymentTypes.TryGetValue(paymentKey, out var existing))
                paymentTypes[paymentKey] = existing + payment.Amount;
            else
                paymentTypes[paymentKey] = payment.Amount;
        }

        return paymentTypes;
    }
}

/// <summary>
/// Extension to GrainKeys for order-fiscal integration.
/// </summary>
public static class OrderFiscalGrainKeys
{
    /// <summary>
    /// Creates a key for the order-fiscal integration grain.
    /// </summary>
    public static string OrderFiscalIntegration(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:orderfiscal";
}
