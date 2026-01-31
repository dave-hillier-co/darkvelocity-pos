using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.State;
using Orleans.Runtime;

namespace DarkVelocity.Orleans.Grains;

// ============================================================================
// Fiscal Device Grain
// ============================================================================

/// <summary>
/// Grain for fiscal device management.
/// Manages TSE devices for German KassenSichV compliance.
/// </summary>
public class FiscalDeviceGrain : Grain, IFiscalDeviceGrain
{
    private readonly IPersistentState<FiscalDeviceState> _state;

    public FiscalDeviceGrain(
        [PersistentState("fiscalDevice", "OrleansStorage")]
        IPersistentState<FiscalDeviceState> state)
    {
        _state = state;
    }

    public async Task<FiscalDeviceSnapshot> RegisterAsync(RegisterFiscalDeviceCommand command)
    {
        if (_state.State.FiscalDeviceId != Guid.Empty)
            throw new InvalidOperationException("Fiscal device already registered");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var deviceId = Guid.Parse(parts[2]);

        _state.State = new FiscalDeviceState
        {
            OrgId = orgId,
            FiscalDeviceId = deviceId,
            LocationId = command.LocationId,
            DeviceType = command.DeviceType,
            SerialNumber = command.SerialNumber,
            PublicKey = command.PublicKey,
            CertificateExpiryDate = command.CertificateExpiryDate,
            Status = FiscalDeviceStatus.Active,
            ApiEndpoint = command.ApiEndpoint,
            ApiCredentialsEncrypted = command.ApiCredentialsEncrypted,
            ClientId = command.ClientId,
            TransactionCounter = 0,
            SignatureCounter = 0,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<FiscalDeviceSnapshot> UpdateAsync(UpdateFiscalDeviceCommand command)
    {
        EnsureInitialized();

        if (command.Status.HasValue) _state.State.Status = command.Status.Value;
        if (command.PublicKey != null) _state.State.PublicKey = command.PublicKey;
        if (command.CertificateExpiryDate.HasValue) _state.State.CertificateExpiryDate = command.CertificateExpiryDate.Value;
        if (command.ApiEndpoint != null) _state.State.ApiEndpoint = command.ApiEndpoint;
        if (command.ApiCredentialsEncrypted != null) _state.State.ApiCredentialsEncrypted = command.ApiCredentialsEncrypted;

        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task DeactivateAsync()
    {
        EnsureInitialized();

        _state.State.Status = FiscalDeviceStatus.Inactive;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<FiscalDeviceSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public async Task<long> GetNextTransactionCounterAsync()
    {
        EnsureInitialized();

        _state.State.TransactionCounter++;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return _state.State.TransactionCounter;
    }

    public async Task<long> GetNextSignatureCounterAsync()
    {
        EnsureInitialized();

        _state.State.SignatureCounter++;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return _state.State.SignatureCounter;
    }

    public async Task RecordSyncAsync()
    {
        EnsureInitialized();

        _state.State.LastSyncAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<bool> IsCertificateExpiringAsync(int daysThreshold = 30)
    {
        EnsureInitialized();

        if (!_state.State.CertificateExpiryDate.HasValue)
            return Task.FromResult(false);

        var daysUntilExpiry = (_state.State.CertificateExpiryDate.Value - DateTime.UtcNow).TotalDays;
        return Task.FromResult(daysUntilExpiry <= daysThreshold);
    }

    private FiscalDeviceSnapshot CreateSnapshot()
    {
        return new FiscalDeviceSnapshot(
            FiscalDeviceId: _state.State.FiscalDeviceId,
            LocationId: _state.State.LocationId,
            DeviceType: _state.State.DeviceType,
            SerialNumber: _state.State.SerialNumber,
            PublicKey: _state.State.PublicKey,
            CertificateExpiryDate: _state.State.CertificateExpiryDate,
            Status: _state.State.Status,
            ApiEndpoint: _state.State.ApiEndpoint,
            LastSyncAt: _state.State.LastSyncAt,
            TransactionCounter: _state.State.TransactionCounter,
            SignatureCounter: _state.State.SignatureCounter,
            ClientId: _state.State.ClientId);
    }

    private void EnsureInitialized()
    {
        if (_state.State.FiscalDeviceId == Guid.Empty)
            throw new InvalidOperationException("Fiscal device grain not initialized");
    }
}

// ============================================================================
// Fiscal Transaction Grain
// ============================================================================

/// <summary>
/// Grain for fiscal transaction management.
/// Manages individual fiscal transactions with TSE signing.
/// </summary>
public class FiscalTransactionGrain : Grain, IFiscalTransactionGrain
{
    private readonly IPersistentState<FiscalTransactionState> _state;
    private readonly IGrainFactory _grainFactory;

    public FiscalTransactionGrain(
        [PersistentState("fiscalTransaction", "OrleansStorage")]
        IPersistentState<FiscalTransactionState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public async Task<FiscalTransactionSnapshot> CreateAsync(CreateFiscalTransactionCommand command)
    {
        if (_state.State.FiscalTransactionId != Guid.Empty)
            throw new InvalidOperationException("Fiscal transaction already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var transactionId = Guid.Parse(parts[2]);

        // Get next transaction number from device
        var deviceKey = $"{orgId}:fiscaldevice:{command.FiscalDeviceId}";
        var deviceGrain = _grainFactory.GetGrain<IFiscalDeviceGrain>(deviceKey);
        var transactionNumber = await deviceGrain.GetNextTransactionCounterAsync();

        _state.State = new FiscalTransactionState
        {
            OrgId = orgId,
            FiscalTransactionId = transactionId,
            FiscalDeviceId = command.FiscalDeviceId,
            LocationId = command.LocationId,
            TransactionNumber = transactionNumber,
            TransactionType = command.TransactionType,
            ProcessType = command.ProcessType,
            StartTime = DateTime.UtcNow,
            SourceType = command.SourceType,
            SourceId = command.SourceId,
            GrossAmount = command.GrossAmount,
            NetAmounts = new Dictionary<string, decimal>(command.NetAmounts),
            TaxAmounts = new Dictionary<string, decimal>(command.TaxAmounts),
            PaymentTypes = new Dictionary<string, decimal>(command.PaymentTypes),
            Status = FiscalTransactionStatus.Pending,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<FiscalTransactionSnapshot> SignAsync(SignTransactionCommand command)
    {
        EnsureInitialized();

        if (_state.State.Status == FiscalTransactionStatus.Signed)
            throw new InvalidOperationException("Transaction already signed");

        _state.State.Signature = command.Signature;
        _state.State.SignatureCounter = command.SignatureCounter;
        _state.State.CertificateSerial = command.CertificateSerial;
        _state.State.QrCodeData = command.QrCodeData;
        _state.State.TseResponseRaw = command.TseResponseRaw;
        _state.State.EndTime = DateTime.UtcNow;
        _state.State.Status = FiscalTransactionStatus.Signed;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task MarkFailedAsync(string errorMessage)
    {
        EnsureInitialized();

        _state.State.Status = FiscalTransactionStatus.Failed;
        _state.State.ErrorMessage = errorMessage;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task IncrementRetryAsync()
    {
        EnsureInitialized();

        _state.State.RetryCount++;
        _state.State.Status = FiscalTransactionStatus.Retrying;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task MarkExportedAsync()
    {
        EnsureInitialized();

        _state.State.ExportedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<FiscalTransactionSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<string> GetQrCodeDataAsync()
    {
        EnsureInitialized();
        return Task.FromResult(_state.State.QrCodeData ?? string.Empty);
    }

    private FiscalTransactionSnapshot CreateSnapshot()
    {
        return new FiscalTransactionSnapshot(
            FiscalTransactionId: _state.State.FiscalTransactionId,
            FiscalDeviceId: _state.State.FiscalDeviceId,
            LocationId: _state.State.LocationId,
            TransactionNumber: _state.State.TransactionNumber,
            TransactionType: _state.State.TransactionType,
            ProcessType: _state.State.ProcessType,
            StartTime: _state.State.StartTime,
            EndTime: _state.State.EndTime,
            SourceType: _state.State.SourceType,
            SourceId: _state.State.SourceId,
            GrossAmount: _state.State.GrossAmount,
            NetAmounts: _state.State.NetAmounts,
            TaxAmounts: _state.State.TaxAmounts,
            PaymentTypes: _state.State.PaymentTypes,
            Signature: _state.State.Signature,
            SignatureCounter: _state.State.SignatureCounter,
            CertificateSerial: _state.State.CertificateSerial,
            QrCodeData: _state.State.QrCodeData,
            Status: _state.State.Status,
            ErrorMessage: _state.State.ErrorMessage,
            RetryCount: _state.State.RetryCount,
            ExportedAt: _state.State.ExportedAt);
    }

    private void EnsureInitialized()
    {
        if (_state.State.FiscalTransactionId == Guid.Empty)
            throw new InvalidOperationException("Fiscal transaction grain not initialized");
    }
}

// ============================================================================
// Fiscal Journal Grain
// ============================================================================

/// <summary>
/// Grain for fiscal journal (immutable audit log).
/// Provides append-only logging for fiscal compliance.
/// </summary>
public class FiscalJournalGrain : Grain, IFiscalJournalGrain
{
    private readonly IPersistentState<FiscalJournalState> _state;

    public FiscalJournalGrain(
        [PersistentState("fiscalJournal", "OrleansStorage")]
        IPersistentState<FiscalJournalState> state)
    {
        _state = state;
    }

    public async Task LogEventAsync(LogFiscalEventCommand command)
    {
        if (_state.State.OrgId == Guid.Empty)
        {
            var key = this.GetPrimaryKeyString();
            var parts = key.Split(':');
            _state.State.OrgId = Guid.Parse(parts[0]);
            _state.State.Date = DateTime.UtcNow.Date;
        }

        _state.State.Entries.Add(new FiscalJournalEntryState
        {
            EntryId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            LocationId = command.LocationId,
            EventType = command.EventType,
            DeviceId = command.DeviceId,
            TransactionId = command.TransactionId,
            ExportId = command.ExportId,
            Details = command.Details,
            IpAddress = command.IpAddress,
            UserId = command.UserId,
            Severity = command.Severity
        });

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<FiscalJournalEntry>> GetEntriesAsync()
    {
        var entries = _state.State.Entries
            .Select(CreateEntry)
            .ToList();

        return Task.FromResult<IReadOnlyList<FiscalJournalEntry>>(entries);
    }

    public Task<IReadOnlyList<FiscalJournalEntry>> GetEntriesByDeviceAsync(Guid deviceId)
    {
        var entries = _state.State.Entries
            .Where(e => e.DeviceId == deviceId)
            .Select(CreateEntry)
            .ToList();

        return Task.FromResult<IReadOnlyList<FiscalJournalEntry>>(entries);
    }

    public Task<IReadOnlyList<FiscalJournalEntry>> GetErrorsAsync()
    {
        var entries = _state.State.Entries
            .Where(e => e.Severity == FiscalEventSeverity.Error)
            .Select(CreateEntry)
            .ToList();

        return Task.FromResult<IReadOnlyList<FiscalJournalEntry>>(entries);
    }

    public Task<int> GetEntryCountAsync()
    {
        return Task.FromResult(_state.State.Entries.Count);
    }

    private static FiscalJournalEntry CreateEntry(FiscalJournalEntryState e)
    {
        return new FiscalJournalEntry(
            EntryId: e.EntryId,
            Timestamp: e.Timestamp,
            LocationId: e.LocationId,
            EventType: e.EventType,
            DeviceId: e.DeviceId,
            TransactionId: e.TransactionId,
            ExportId: e.ExportId,
            Details: e.Details,
            IpAddress: e.IpAddress,
            UserId: e.UserId,
            Severity: e.Severity);
    }
}

// ============================================================================
// Tax Rate Grain
// ============================================================================

/// <summary>
/// Grain for tax rate management.
/// Manages tax rates by country and fiscal code.
/// </summary>
public class TaxRateGrain : Grain, ITaxRateGrain
{
    private readonly IPersistentState<TaxRateState> _state;

    public TaxRateGrain(
        [PersistentState("taxRate", "OrleansStorage")]
        IPersistentState<TaxRateState> state)
    {
        _state = state;
    }

    public async Task<TaxRateSnapshot> CreateAsync(CreateTaxRateCommand command)
    {
        if (_state.State.TaxRateId != Guid.Empty)
            throw new InvalidOperationException("Tax rate already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State = new TaxRateState
        {
            OrgId = orgId,
            TaxRateId = Guid.NewGuid(),
            CountryCode = command.CountryCode,
            Rate = command.Rate,
            FiscalCode = command.FiscalCode,
            Description = command.Description,
            EffectiveFrom = command.EffectiveFrom,
            EffectiveTo = command.EffectiveTo,
            IsActive = true,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task DeactivateAsync(DateTime effectiveTo)
    {
        EnsureInitialized();

        _state.State.EffectiveTo = effectiveTo;
        _state.State.IsActive = false;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<TaxRateSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<decimal> GetCurrentRateAsync()
    {
        EnsureInitialized();
        return Task.FromResult(_state.State.Rate);
    }

    public Task<bool> IsActiveOnDateAsync(DateTime date)
    {
        EnsureInitialized();

        var isActive = date >= _state.State.EffectiveFrom &&
                      (!_state.State.EffectiveTo.HasValue || date <= _state.State.EffectiveTo.Value);

        return Task.FromResult(isActive);
    }

    private TaxRateSnapshot CreateSnapshot()
    {
        return new TaxRateSnapshot(
            TaxRateId: _state.State.TaxRateId,
            CountryCode: _state.State.CountryCode,
            Rate: _state.State.Rate,
            FiscalCode: _state.State.FiscalCode,
            Description: _state.State.Description,
            EffectiveFrom: _state.State.EffectiveFrom,
            EffectiveTo: _state.State.EffectiveTo,
            IsActive: _state.State.IsActive);
    }

    private void EnsureInitialized()
    {
        if (_state.State.TaxRateId == Guid.Empty)
            throw new InvalidOperationException("Tax rate grain not initialized");
    }
}
