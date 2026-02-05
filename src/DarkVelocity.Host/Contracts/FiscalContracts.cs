using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.Contracts;

// ============================================================================
// Fiscal Device Contracts
// ============================================================================

/// <summary>
/// Request to register a new fiscal device.
/// </summary>
public record RegisterFiscalDeviceRequest(
    /// <summary>Device type (SwissbitCloud, SwissbitUsb, FiskalyCloud, Epson, Diebold).</summary>
    string DeviceType,

    /// <summary>Device serial number.</summary>
    string SerialNumber,

    /// <summary>Device public key (for TSE devices).</summary>
    string? PublicKey = null,

    /// <summary>Certificate expiry date.</summary>
    DateTime? CertificateExpiryDate = null,

    /// <summary>API endpoint for cloud-based devices.</summary>
    string? ApiEndpoint = null,

    /// <summary>Encrypted API credentials.</summary>
    string? ApiCredentialsEncrypted = null,

    /// <summary>Client identifier for the TSE.</summary>
    string? ClientId = null);

/// <summary>
/// Request to update a fiscal device.
/// </summary>
public record UpdateFiscalDeviceRequest(
    /// <summary>Updated device status.</summary>
    string? Status = null,

    /// <summary>Updated public key.</summary>
    string? PublicKey = null,

    /// <summary>Updated certificate expiry date.</summary>
    DateTime? CertificateExpiryDate = null,

    /// <summary>Updated API endpoint.</summary>
    string? ApiEndpoint = null,

    /// <summary>Updated encrypted API credentials.</summary>
    string? ApiCredentialsEncrypted = null);

/// <summary>
/// Response for fiscal device operations.
/// </summary>
public record FiscalDeviceResponse(
    Guid Id,
    Guid SiteId,
    string DeviceType,
    string SerialNumber,
    string? PublicKey,
    DateTime? CertificateExpiryDate,
    string Status,
    string? ApiEndpoint,
    DateTime? LastSyncAt,
    long TransactionCounter,
    long SignatureCounter,
    string? ClientId);

/// <summary>
/// List of fiscal devices.
/// </summary>
public record FiscalDeviceListResponse(
    List<FiscalDeviceResponse> Items,
    int Total);

/// <summary>
/// Request to activate a fiscal device.
/// </summary>
public record ActivateFiscalDeviceRequest(
    /// <summary>Tax authority registration ID (if applicable).</summary>
    string? TaxAuthorityRegistrationId = null,

    /// <summary>Operator performing the activation.</summary>
    Guid? OperatorId = null);

/// <summary>
/// Request to deactivate a fiscal device.
/// </summary>
public record DeactivateFiscalDeviceRequest(
    /// <summary>Reason for deactivation.</summary>
    string Reason,

    /// <summary>Operator performing the deactivation.</summary>
    Guid? OperatorId = null);

// ============================================================================
// Fiscal Transaction Contracts
// ============================================================================

/// <summary>
/// Request to create a fiscal transaction.
/// </summary>
public record CreateFiscalTransactionRequest(
    /// <summary>Fiscal device ID to use for signing.</summary>
    Guid FiscalDeviceId,

    /// <summary>Transaction type (Receipt, TrainingReceipt, Void, Cancellation).</summary>
    string TransactionType,

    /// <summary>Process type (Kassenbeleg, AVTransfer, AVBestellung, AVSonstiger).</summary>
    string ProcessType,

    /// <summary>Source type (e.g., "Order", "Payment").</summary>
    string SourceType,

    /// <summary>Source entity ID.</summary>
    Guid SourceId,

    /// <summary>Gross amount.</summary>
    decimal GrossAmount,

    /// <summary>Net amounts by VAT rate.</summary>
    Dictionary<string, decimal> NetAmounts,

    /// <summary>Tax amounts by VAT rate.</summary>
    Dictionary<string, decimal> TaxAmounts,

    /// <summary>Amounts by payment type.</summary>
    Dictionary<string, decimal> PaymentTypes);

/// <summary>
/// Response for fiscal transaction operations.
/// </summary>
public record FiscalTransactionResponse(
    Guid Id,
    Guid FiscalDeviceId,
    Guid SiteId,
    long TransactionNumber,
    string TransactionType,
    string ProcessType,
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
    string Status,
    string? ErrorMessage,
    int RetryCount,
    DateTime? ExportedAt);

/// <summary>
/// List of fiscal transactions.
/// </summary>
public record FiscalTransactionListResponse(
    List<FiscalTransactionResponse> Items,
    int Total,
    int Page,
    int PageSize);

// ============================================================================
// Fiscal Journal Contracts
// ============================================================================

/// <summary>
/// Response for fiscal journal entry.
/// </summary>
public record FiscalJournalEntryResponse(
    Guid EntryId,
    DateTime Timestamp,
    Guid? SiteId,
    string EventType,
    Guid? DeviceId,
    Guid? TransactionId,
    Guid? ExportId,
    string Details,
    string? IpAddress,
    Guid? UserId,
    string Severity);

/// <summary>
/// List of fiscal journal entries.
/// </summary>
public record FiscalJournalResponse(
    List<FiscalJournalEntryResponse> Entries,
    int Total,
    DateOnly Date);

// ============================================================================
// DSFinV-K Export Contracts
// ============================================================================

/// <summary>
/// Request to generate a DSFinV-K export.
/// </summary>
public record GenerateDSFinVKExportRequest(
    /// <summary>Start date for the export (inclusive).</summary>
    DateOnly StartDate,

    /// <summary>End date for the export (inclusive).</summary>
    DateOnly EndDate,

    /// <summary>Optional description for the export.</summary>
    string? Description = null,

    /// <summary>Include only specific device IDs (null = all).</summary>
    List<Guid>? DeviceIds = null);

/// <summary>
/// Response for DSFinV-K export generation.
/// </summary>
public record DSFinVKExportResponse(
    Guid ExportId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    int TransactionCount,
    string? DownloadUrl,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? ErrorMessage);

/// <summary>
/// List of DSFinV-K exports.
/// </summary>
public record DSFinVKExportListResponse(
    List<DSFinVKExportResponse> Items,
    int Total);

// ============================================================================
// Device Health Contracts
// ============================================================================

/// <summary>
/// Response for device health check.
/// </summary>
public record FiscalDeviceHealthResponse(
    Guid DeviceId,
    string Status,
    bool IsOnline,
    bool CertificateValid,
    int? DaysUntilCertificateExpiry,
    DateTime? LastSyncAt,
    DateTime? LastTransactionAt,
    long TotalTransactions,
    string? LastError);

/// <summary>
/// Result of device self-test.
/// </summary>
public record FiscalDeviceSelfTestResponse(
    Guid DeviceId,
    bool Passed,
    string? ErrorMessage,
    DateTime PerformedAt);
