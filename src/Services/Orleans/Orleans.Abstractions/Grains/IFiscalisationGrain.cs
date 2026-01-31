namespace DarkVelocity.Orleans.Abstractions.Grains;

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

public record RegisterFiscalDeviceCommand(
    Guid LocationId,
    FiscalDeviceType DeviceType,
    string SerialNumber,
    string? PublicKey,
    DateTime? CertificateExpiryDate,
    string? ApiEndpoint,
    string? ApiCredentialsEncrypted,
    string? ClientId);

public record UpdateFiscalDeviceCommand(
    FiscalDeviceStatus? Status,
    string? PublicKey,
    DateTime? CertificateExpiryDate,
    string? ApiEndpoint,
    string? ApiCredentialsEncrypted);

public record FiscalDeviceSnapshot(
    Guid FiscalDeviceId,
    Guid LocationId,
    FiscalDeviceType DeviceType,
    string SerialNumber,
    string? PublicKey,
    DateTime? CertificateExpiryDate,
    FiscalDeviceStatus Status,
    string? ApiEndpoint,
    DateTime? LastSyncAt,
    long TransactionCounter,
    long SignatureCounter,
    string? ClientId);

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

public record CreateFiscalTransactionCommand(
    Guid FiscalDeviceId,
    Guid LocationId,
    FiscalTransactionType TransactionType,
    FiscalProcessType ProcessType,
    string SourceType,
    Guid SourceId,
    decimal GrossAmount,
    Dictionary<string, decimal> NetAmounts,
    Dictionary<string, decimal> TaxAmounts,
    Dictionary<string, decimal> PaymentTypes);

public record SignTransactionCommand(
    string Signature,
    long SignatureCounter,
    string CertificateSerial,
    string QrCodeData,
    string TseResponseRaw);

public record FiscalTransactionSnapshot(
    Guid FiscalTransactionId,
    Guid FiscalDeviceId,
    Guid LocationId,
    long TransactionNumber,
    FiscalTransactionType TransactionType,
    FiscalProcessType ProcessType,
    DateTime StartTime,
    DateTime? EndTime,
    string SourceType,
    Guid SourceId,
    decimal GrossAmount,
    IReadOnlyDictionary<string, decimal> NetAmounts,
    IReadOnlyDictionary<string, decimal> TaxAmounts,
    IReadOnlyDictionary<string, decimal> PaymentTypes,
    string? Signature,
    long? SignatureCounter,
    string? CertificateSerial,
    string? QrCodeData,
    FiscalTransactionStatus Status,
    string? ErrorMessage,
    int RetryCount,
    DateTime? ExportedAt);

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

public record LogFiscalEventCommand(
    Guid? LocationId,
    FiscalEventType EventType,
    Guid? DeviceId,
    Guid? TransactionId,
    Guid? ExportId,
    string Details,
    string? IpAddress,
    Guid? UserId,
    FiscalEventSeverity Severity);

public record FiscalJournalEntry(
    Guid EntryId,
    DateTime Timestamp,
    Guid? LocationId,
    FiscalEventType EventType,
    Guid? DeviceId,
    Guid? TransactionId,
    Guid? ExportId,
    string Details,
    string? IpAddress,
    Guid? UserId,
    FiscalEventSeverity Severity);

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

public record CreateTaxRateCommand(
    string CountryCode,
    decimal Rate,
    string FiscalCode,
    string Description,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo);

public record TaxRateSnapshot(
    Guid TaxRateId,
    string CountryCode,
    decimal Rate,
    string FiscalCode,
    string Description,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsActive);

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
