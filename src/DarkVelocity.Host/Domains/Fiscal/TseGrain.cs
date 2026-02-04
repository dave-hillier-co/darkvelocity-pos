using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// TSE Grain Interfaces
// ============================================================================

/// <summary>
/// Command to start a TSE transaction
/// </summary>
[GenerateSerializer]
public record StartTseTransactionCommand(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] string ProcessType,
    [property: Id(2)] string ProcessData,
    [property: Id(3)] string? ClientId);

/// <summary>
/// Command to update a TSE transaction
/// </summary>
[GenerateSerializer]
public record UpdateTseTransactionCommand(
    [property: Id(0)] long TransactionNumber,
    [property: Id(1)] string ProcessData);

/// <summary>
/// Command to finish a TSE transaction
/// </summary>
[GenerateSerializer]
public record FinishTseTransactionCommand(
    [property: Id(0)] long TransactionNumber,
    [property: Id(1)] string ProcessType,
    [property: Id(2)] string ProcessData);

/// <summary>
/// Command to configure external TSE mapping
/// </summary>
[GenerateSerializer]
public record ConfigureExternalTseMappingCommand(
    [property: Id(0)] bool Enabled,
    [property: Id(1)] Guid? ExternalDeviceId,
    [property: Id(2)] ExternalTseType ExternalTseType,
    [property: Id(3)] string? ApiEndpoint,
    [property: Id(4)] string? ClientId,
    [property: Id(5)] bool ForwardAllEvents,
    [property: Id(6)] bool RequireExternalSignature);

/// <summary>
/// Snapshot of TSE state
/// </summary>
[GenerateSerializer]
public record TseSnapshot(
    [property: Id(0)] Guid TseId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] long TransactionCounter,
    [property: Id(3)] long SignatureCounter,
    [property: Id(4)] string CertificateSerial,
    [property: Id(5)] string PublicKeyBase64,
    [property: Id(6)] bool IsInitialized,
    [property: Id(7)] ExternalTseMappingConfig? ExternalMapping,
    [property: Id(8)] DateTime? LastSelfTestAt,
    [property: Id(9)] bool LastSelfTestPassed);

/// <summary>
/// Grain for TSE operations.
/// Acts as an internal TSE that generates events which can be optionally forwarded to external TSEs.
/// Key: "{orgId}:tse:{tseId}"
/// </summary>
public interface ITseGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initialize the TSE
    /// </summary>
    Task<TseSnapshot> InitializeAsync(Guid locationId);

    /// <summary>
    /// Start a new TSE transaction
    /// </summary>
    Task<TseStartTransactionResult> StartTransactionAsync(StartTseTransactionCommand command);

    /// <summary>
    /// Update transaction data
    /// </summary>
    Task<bool> UpdateTransactionAsync(UpdateTseTransactionCommand command);

    /// <summary>
    /// Finish transaction and get signature
    /// </summary>
    Task<TseFinishTransactionResult> FinishTransactionAsync(FinishTseTransactionCommand command);

    /// <summary>
    /// Perform self-test
    /// </summary>
    Task<TseSelfTestResult> SelfTestAsync();

    /// <summary>
    /// Configure external TSE mapping
    /// </summary>
    Task<TseSnapshot> ConfigureExternalMappingAsync(ConfigureExternalTseMappingCommand command);

    /// <summary>
    /// Receive response from external TSE (for async integrations)
    /// </summary>
    Task ReceiveExternalResponseAsync(
        long transactionNumber,
        string externalTransactionId,
        string externalSignature,
        string externalCertificateSerial,
        long externalSignatureCounter,
        DateTime externalTimestamp,
        string rawResponse);

    /// <summary>
    /// Get current snapshot
    /// </summary>
    Task<TseSnapshot> GetSnapshotAsync();
}

// ============================================================================
// TSE State
// ============================================================================

[GenerateSerializer]
public sealed class TseState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid TseId { get; set; }
    [Id(2)] public Guid LocationId { get; set; }
    [Id(3)] public long TransactionCounter { get; set; }
    [Id(4)] public long SignatureCounter { get; set; }
    [Id(5)] public byte[] SigningKey { get; set; } = [];
    [Id(6)] public string CertificateSerial { get; set; } = string.Empty;
    [Id(7)] public string PublicKeyBase64 { get; set; } = string.Empty;
    [Id(8)] public bool IsInitialized { get; set; }
    [Id(9)] public ExternalTseMappingConfig? ExternalMapping { get; set; }
    [Id(10)] public DateTime? LastSelfTestAt { get; set; }
    [Id(11)] public bool LastSelfTestPassed { get; set; }
    [Id(12)] public Dictionary<long, TseTransactionContext> ActiveTransactions { get; set; } = new();
    [Id(13)] public int Version { get; set; }
}

// ============================================================================
// TSE Grain Implementation
// ============================================================================

/// <summary>
/// TSE Grain implementation.
/// Provides TSE-like behavior internally and can forward events to external TSEs.
/// </summary>
public class TseGrain : Grain, ITseGrain
{
    private readonly IPersistentState<TseState> _state;
    private readonly IGrainFactory _grainFactory;
    private ITseProvider? _provider;
    private IAsyncStream<IntegrationEvent>? _eventStream;

    public TseGrain(
        [PersistentState("tse", "OrleansStorage")]
        IPersistentState<TseState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.IsInitialized)
        {
            InitializeProvider();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<TseSnapshot> InitializeAsync(Guid locationId)
    {
        if (_state.State.IsInitialized)
            throw new InvalidOperationException("TSE already initialized");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var tseId = Guid.Parse(parts[2]);

        // Generate signing key
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var signingKey = new byte[32];
        rng.GetBytes(signingKey);

        // Generate certificate serial
        var certificateSerial = "DVTSE" + DateTime.UtcNow.Ticks.ToString("X");
        var publicKeyBase64 = Convert.ToBase64String(signingKey[..16]);

        _state.State = new TseState
        {
            OrgId = orgId,
            TseId = tseId,
            LocationId = locationId,
            TransactionCounter = 0,
            SignatureCounter = 0,
            SigningKey = signingKey,
            CertificateSerial = certificateSerial,
            PublicKeyBase64 = publicKeyBase64,
            IsInitialized = true,
            Version = 1
        };

        await _state.WriteStateAsync();
        InitializeProvider();

        return CreateSnapshot();
    }

    public async Task<TseStartTransactionResult> StartTransactionAsync(StartTseTransactionCommand command)
    {
        EnsureInitialized();

        var result = await _provider!.StartTransactionAsync(
            command.ProcessType,
            command.ProcessData,
            command.ClientId);

        if (result.Success)
        {
            // Store active transaction
            var context = new TseTransactionContext(
                TseTransactionId: Guid.NewGuid(),
                TransactionNumber: result.TransactionNumber,
                StartTime: result.StartTime,
                ProcessType: command.ProcessType,
                ProcessData: command.ProcessData,
                ClientId: command.ClientId);

            _state.State.ActiveTransactions[result.TransactionNumber] = context;
            _state.State.TransactionCounter = result.TransactionNumber;
            _state.State.Version++;
            await _state.WriteStateAsync();

            // Emit TSE event
            var evt = new TseTransactionStarted(
                TseTransactionId: context.TseTransactionId,
                DeviceId: _state.State.TseId,
                LocationId: command.LocationId,
                TenantId: _state.State.OrgId,
                TransactionNumber: result.TransactionNumber,
                ProcessType: command.ProcessType,
                ProcessData: command.ProcessData,
                StartTime: result.StartTime,
                ClientId: command.ClientId);

            await PublishEventAsync(evt);
        }

        return result;
    }

    public async Task<bool> UpdateTransactionAsync(UpdateTseTransactionCommand command)
    {
        EnsureInitialized();

        var success = await _provider!.UpdateTransactionAsync(
            command.TransactionNumber,
            command.ProcessData);

        if (success && _state.State.ActiveTransactions.TryGetValue(command.TransactionNumber, out var context))
        {
            _state.State.ActiveTransactions[command.TransactionNumber] = context with
            {
                ProcessData = command.ProcessData
            };
            _state.State.Version++;
            await _state.WriteStateAsync();

            // Emit TSE event
            var evt = new TseTransactionUpdated(
                TseTransactionId: context.TseTransactionId,
                DeviceId: _state.State.TseId,
                LocationId: _state.State.LocationId,
                TenantId: _state.State.OrgId,
                TransactionNumber: command.TransactionNumber,
                ProcessData: command.ProcessData,
                UpdateTime: DateTime.UtcNow);

            await PublishEventAsync(evt);
        }

        return success;
    }

    public async Task<TseFinishTransactionResult> FinishTransactionAsync(FinishTseTransactionCommand command)
    {
        EnsureInitialized();

        TseTransactionContext? context = null;
        _state.State.ActiveTransactions.TryGetValue(command.TransactionNumber, out context);

        var result = await _provider!.FinishTransactionAsync(
            command.TransactionNumber,
            command.ProcessType,
            command.ProcessData);

        if (result.Success)
        {
            _state.State.ActiveTransactions.Remove(command.TransactionNumber);
            _state.State.SignatureCounter = result.SignatureCounter;
            _state.State.Version++;
            await _state.WriteStateAsync();

            // Emit TSE event
            var evt = new TseTransactionFinished(
                TseTransactionId: context?.TseTransactionId ?? Guid.NewGuid(),
                DeviceId: _state.State.TseId,
                LocationId: _state.State.LocationId,
                TenantId: _state.State.OrgId,
                TransactionNumber: result.TransactionNumber,
                SignatureCounter: result.SignatureCounter,
                Signature: result.Signature,
                SignatureAlgorithm: result.SignatureAlgorithm,
                PublicKeyBase64: result.PublicKeyBase64,
                StartTime: result.StartTime,
                EndTime: result.EndTime,
                ProcessType: command.ProcessType,
                ProcessData: command.ProcessData,
                CertificateSerial: result.CertificateSerial,
                TimeFormat: "utcTime",
                QrCodeData: result.QrCodeData);

            await PublishEventAsync(evt);
        }
        else
        {
            // Emit failure event
            var evt = new TseTransactionFailed(
                TseTransactionId: context?.TseTransactionId ?? Guid.NewGuid(),
                DeviceId: _state.State.TseId,
                LocationId: _state.State.LocationId,
                TenantId: _state.State.OrgId,
                TransactionNumber: command.TransactionNumber,
                ErrorCode: "FINISH_FAILED",
                ErrorMessage: result.ErrorMessage ?? "Unknown error",
                FailedAt: DateTime.UtcNow);

            await PublishEventAsync(evt);
        }

        return result;
    }

    public async Task<TseSelfTestResult> SelfTestAsync()
    {
        EnsureInitialized();

        var result = await _provider!.SelfTestAsync();

        _state.State.LastSelfTestAt = result.PerformedAt;
        _state.State.LastSelfTestPassed = result.Passed;
        _state.State.Version++;
        await _state.WriteStateAsync();

        // Emit TSE event
        var evt = new TseSelfTestPerformed(
            DeviceId: _state.State.TseId,
            LocationId: _state.State.LocationId,
            TenantId: _state.State.OrgId,
            Passed: result.Passed,
            ErrorMessage: result.ErrorMessage,
            PerformedAt: result.PerformedAt);

        await PublishEventAsync(evt);

        return result;
    }

    public async Task<TseSnapshot> ConfigureExternalMappingAsync(ConfigureExternalTseMappingCommand command)
    {
        EnsureInitialized();

        _state.State.ExternalMapping = new ExternalTseMappingConfig(
            Enabled: command.Enabled,
            ExternalDeviceId: command.ExternalDeviceId ?? Guid.Empty,
            ExternalTseType: command.ExternalTseType,
            ApiEndpoint: command.ApiEndpoint,
            ClientId: command.ClientId,
            ForwardAllEvents: command.ForwardAllEvents,
            RequireExternalSignature: command.RequireExternalSignature);

        _state.State.Version++;
        await _state.WriteStateAsync();

        // Reinitialize provider with new configuration
        InitializeProvider();

        return CreateSnapshot();
    }

    public async Task ReceiveExternalResponseAsync(
        long transactionNumber,
        string externalTransactionId,
        string externalSignature,
        string externalCertificateSerial,
        long externalSignatureCounter,
        DateTime externalTimestamp,
        string rawResponse)
    {
        EnsureInitialized();

        TseTransactionContext? context = null;
        _state.State.ActiveTransactions.TryGetValue(transactionNumber, out context);

        // Emit event for external response received
        var evt = new ExternalTseResponseReceived(
            TseTransactionId: context?.TseTransactionId ?? Guid.NewGuid(),
            DeviceId: _state.State.TseId,
            LocationId: _state.State.LocationId,
            TenantId: _state.State.OrgId,
            ExternalTransactionId: externalTransactionId,
            ExternalSignature: externalSignature,
            ExternalCertificateSerial: externalCertificateSerial,
            ExternalSignatureCounter: externalSignatureCounter,
            ExternalTimestamp: externalTimestamp,
            RawResponse: rawResponse);

        await PublishEventAsync(evt);
    }

    public Task<TseSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    private void InitializeProvider()
    {
        _provider = TseProviderFactory.CreateProvider(
            _state.State.ExternalMapping,
            _state.State.SigningKey,
            _state.State.TransactionCounter,
            _state.State.SignatureCounter);
    }

    private TseSnapshot CreateSnapshot()
    {
        return new TseSnapshot(
            TseId: _state.State.TseId,
            LocationId: _state.State.LocationId,
            TransactionCounter: _state.State.TransactionCounter,
            SignatureCounter: _state.State.SignatureCounter,
            CertificateSerial: _state.State.CertificateSerial,
            PublicKeyBase64: _state.State.PublicKeyBase64,
            IsInitialized: _state.State.IsInitialized,
            ExternalMapping: _state.State.ExternalMapping,
            LastSelfTestAt: _state.State.LastSelfTestAt,
            LastSelfTestPassed: _state.State.LastSelfTestPassed);
    }

    private async Task PublishEventAsync(IntegrationEvent evt)
    {
        // Get or create stream for publishing events
        if (_eventStream == null)
        {
            var streamProvider = this.GetStreamProvider("Default");
            _eventStream = streamProvider.GetStream<IntegrationEvent>(
                StreamId.Create("fiscal-tse-events", _state.State.OrgId.ToString()));
        }

        try
        {
            await _eventStream.OnNextAsync(evt);
        }
        catch
        {
            // Stream may not be configured - events still work via grain state
        }
    }

    private void EnsureInitialized()
    {
        if (!_state.State.IsInitialized)
            throw new InvalidOperationException("TSE not initialized");
    }
}
