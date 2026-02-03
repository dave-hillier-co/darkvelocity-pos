namespace DarkVelocity.Host.Grains;

// ============================================================================
// Fiscal Device Grain
// ============================================================================

public enum FiscalDeviceType
{
    SwissbitCloud,
    SwissbitUsb,
    FiskalyCloud,
    Epson,
    Diebold
}

public enum FiscalDeviceStatus
{
    Active,
    Inactive,
    Failed,
    CertificateExpiring
}

[GenerateSerializer]
public record RegisterFiscalDeviceCommand(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] FiscalDeviceType DeviceType,
    [property: Id(2)] string SerialNumber,
    [property: Id(3)] string? PublicKey,
    [property: Id(4)] DateTime? CertificateExpiryDate,
    [property: Id(5)] string? ApiEndpoint,
    [property: Id(6)] string? ApiCredentialsEncrypted,
    [property: Id(7)] string? ClientId);

[GenerateSerializer]
public record UpdateFiscalDeviceCommand(
    [property: Id(0)] FiscalDeviceStatus? Status,
    [property: Id(1)] string? PublicKey,
    [property: Id(2)] DateTime? CertificateExpiryDate,
    [property: Id(3)] string? ApiEndpoint,
    [property: Id(4)] string? ApiCredentialsEncrypted);

[GenerateSerializer]
public record FiscalDeviceSnapshot(
    [property: Id(0)] Guid FiscalDeviceId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] FiscalDeviceType DeviceType,
    [property: Id(3)] string SerialNumber,
    [property: Id(4)] string? PublicKey,
    [property: Id(5)] DateTime? CertificateExpiryDate,
    [property: Id(6)] FiscalDeviceStatus Status,
    [property: Id(7)] string? ApiEndpoint,
    [property: Id(8)] DateTime? LastSyncAt,
    [property: Id(9)] long TransactionCounter,
    [property: Id(10)] long SignatureCounter,
    [property: Id(11)] string? ClientId);

/// <summary>
/// Grain for fiscal device management.
/// Key: "{orgId}:fiscaldevice:{deviceId}"
/// </summary>
public interface IFiscalDeviceGrain : IGrainWithStringKey
{
    Task<FiscalDeviceSnapshot> RegisterAsync(RegisterFiscalDeviceCommand command);
    Task<FiscalDeviceSnapshot> UpdateAsync(UpdateFiscalDeviceCommand command);
    Task DeactivateAsync();
    Task<FiscalDeviceSnapshot> GetSnapshotAsync();
    Task<long> GetNextTransactionCounterAsync();
    Task<long> GetNextSignatureCounterAsync();
    Task RecordSyncAsync();
    Task<bool> IsCertificateExpiringAsync(int daysThreshold = 30);
}

// ============================================================================
// Fiscal Transaction Grain
// ============================================================================

public enum FiscalTransactionType
{
    Receipt,
    TrainingReceipt,
    Void,
    Cancellation
}

public enum FiscalProcessType
{
    Kassenbeleg,
    AVTransfer,
    AVBestellung,
    AVSonstiger
}

public enum FiscalTransactionStatus
{
    Pending,
    Signed,
    Failed,
    Retrying
}

[GenerateSerializer]
public record CreateFiscalTransactionCommand(
    [property: Id(0)] Guid FiscalDeviceId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] FiscalTransactionType TransactionType,
    [property: Id(3)] FiscalProcessType ProcessType,
    [property: Id(4)] string SourceType,
    [property: Id(5)] Guid SourceId,
    [property: Id(6)] decimal GrossAmount,
    [property: Id(7)] Dictionary<string, decimal> NetAmounts,
    [property: Id(8)] Dictionary<string, decimal> TaxAmounts,
    [property: Id(9)] Dictionary<string, decimal> PaymentTypes);

[GenerateSerializer]
public record SignTransactionCommand(
    [property: Id(0)] string Signature,
    [property: Id(1)] long SignatureCounter,
    [property: Id(2)] string CertificateSerial,
    [property: Id(3)] string QrCodeData,
    [property: Id(4)] string TseResponseRaw);

[GenerateSerializer]
public record FiscalTransactionSnapshot(
    [property: Id(0)] Guid FiscalTransactionId,
    [property: Id(1)] Guid FiscalDeviceId,
    [property: Id(2)] Guid LocationId,
    [property: Id(3)] long TransactionNumber,
    [property: Id(4)] FiscalTransactionType TransactionType,
    [property: Id(5)] FiscalProcessType ProcessType,
    [property: Id(6)] DateTime StartTime,
    [property: Id(7)] DateTime? EndTime,
    [property: Id(8)] string SourceType,
    [property: Id(9)] Guid SourceId,
    [property: Id(10)] decimal GrossAmount,
    [property: Id(11)] IReadOnlyDictionary<string, decimal> NetAmounts,
    [property: Id(12)] IReadOnlyDictionary<string, decimal> TaxAmounts,
    [property: Id(13)] IReadOnlyDictionary<string, decimal> PaymentTypes,
    [property: Id(14)] string? Signature,
    [property: Id(15)] long? SignatureCounter,
    [property: Id(16)] string? CertificateSerial,
    [property: Id(17)] string? QrCodeData,
    [property: Id(18)] FiscalTransactionStatus Status,
    [property: Id(19)] string? ErrorMessage,
    [property: Id(20)] int RetryCount,
    [property: Id(21)] DateTime? ExportedAt);

/// <summary>
/// Grain for fiscal transaction management.
/// Key: "{orgId}:fiscaltransaction:{transactionId}"
/// </summary>
public interface IFiscalTransactionGrain : IGrainWithStringKey
{
    Task<FiscalTransactionSnapshot> CreateAsync(CreateFiscalTransactionCommand command);
    Task<FiscalTransactionSnapshot> SignAsync(SignTransactionCommand command);
    Task MarkFailedAsync(string errorMessage);
    Task IncrementRetryAsync();
    Task MarkExportedAsync();
    Task<FiscalTransactionSnapshot> GetSnapshotAsync();
    Task<string> GetQrCodeDataAsync();
}

// ============================================================================
// Fiscal Journal Grain (Immutable Audit Log)
// ============================================================================

public enum FiscalEventType
{
    TransactionSigned,
    DeviceRegistered,
    DeviceDecommissioned,
    ExportGenerated,
    Error,
    DeviceStatusChanged,
    SelfTestPerformed
}

public enum FiscalEventSeverity
{
    Info,
    Warning,
    Error
}

[GenerateSerializer]
public record LogFiscalEventCommand(
    [property: Id(0)] Guid? LocationId,
    [property: Id(1)] FiscalEventType EventType,
    [property: Id(2)] Guid? DeviceId,
    [property: Id(3)] Guid? TransactionId,
    [property: Id(4)] Guid? ExportId,
    [property: Id(5)] string Details,
    [property: Id(6)] string? IpAddress,
    [property: Id(7)] Guid? UserId,
    [property: Id(8)] FiscalEventSeverity Severity);

[GenerateSerializer]
public record FiscalJournalEntry(
    [property: Id(0)] Guid EntryId,
    [property: Id(1)] DateTime Timestamp,
    [property: Id(2)] Guid? LocationId,
    [property: Id(3)] FiscalEventType EventType,
    [property: Id(4)] Guid? DeviceId,
    [property: Id(5)] Guid? TransactionId,
    [property: Id(6)] Guid? ExportId,
    [property: Id(7)] string Details,
    [property: Id(8)] string? IpAddress,
    [property: Id(9)] Guid? UserId,
    [property: Id(10)] FiscalEventSeverity Severity);

/// <summary>
/// Grain for fiscal journal (immutable audit log).
/// Key: "{orgId}:fiscaljournal:{date:yyyy-MM-dd}"
/// </summary>
public interface IFiscalJournalGrain : IGrainWithStringKey
{
    Task LogEventAsync(LogFiscalEventCommand command);
    Task<IReadOnlyList<FiscalJournalEntry>> GetEntriesAsync();
    Task<IReadOnlyList<FiscalJournalEntry>> GetEntriesByDeviceAsync(Guid deviceId);
    Task<IReadOnlyList<FiscalJournalEntry>> GetErrorsAsync();
    Task<int> GetEntryCountAsync();
}

// ============================================================================
// Tax Rate Grain
// ============================================================================

[GenerateSerializer]
public record CreateTaxRateCommand(
    [property: Id(0)] string CountryCode,
    [property: Id(1)] decimal Rate,
    [property: Id(2)] string FiscalCode,
    [property: Id(3)] string Description,
    [property: Id(4)] DateTime EffectiveFrom,
    [property: Id(5)] DateTime? EffectiveTo);

[GenerateSerializer]
public record TaxRateSnapshot(
    [property: Id(0)] Guid TaxRateId,
    [property: Id(1)] string CountryCode,
    [property: Id(2)] decimal Rate,
    [property: Id(3)] string FiscalCode,
    [property: Id(4)] string Description,
    [property: Id(5)] DateTime EffectiveFrom,
    [property: Id(6)] DateTime? EffectiveTo,
    [property: Id(7)] bool IsActive);

/// <summary>
/// Grain for tax rate management.
/// Key: "{orgId}:taxrate:{countryCode}:{fiscalCode}"
/// </summary>
public interface ITaxRateGrain : IGrainWithStringKey
{
    Task<TaxRateSnapshot> CreateAsync(CreateTaxRateCommand command);
    Task DeactivateAsync(DateTime effectiveTo);
    Task<TaxRateSnapshot> GetSnapshotAsync();
    Task<decimal> GetCurrentRateAsync();
    Task<bool> IsActiveOnDateAsync(DateTime date);
}
