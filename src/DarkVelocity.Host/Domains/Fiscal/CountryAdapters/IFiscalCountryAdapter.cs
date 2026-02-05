namespace DarkVelocity.Host.Grains;

// ============================================================================
// Fiscal Country Adapter Interface
// Abstraction for country-specific fiscal compliance implementations
// ============================================================================

/// <summary>
/// Supported fiscal countries
/// </summary>
public enum FiscalCountry
{
    /// <summary>Germany - KassenSichV compliance with TSE</summary>
    Germany,
    /// <summary>Austria - RKSV (Registrierkassensicherheitsverordnung)</summary>
    Austria,
    /// <summary>Italy - RT (Registratore Telematico)</summary>
    Italy,
    /// <summary>France - NF 525 certification</summary>
    France,
    /// <summary>Poland - JPK/KSeF compliance</summary>
    Poland
}

/// <summary>
/// Result of a fiscal operation
/// </summary>
[GenerateSerializer]
public record FiscalResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] string? TransactionId,
    [property: Id(2)] string? Signature,
    [property: Id(3)] long? SignatureCounter,
    [property: Id(4)] string? CertificateSerial,
    [property: Id(5)] string? QrCodeData,
    [property: Id(6)] string? ErrorCode,
    [property: Id(7)] string? ErrorMessage,
    [property: Id(8)] Dictionary<string, string>? Metadata);

/// <summary>
/// Fiscal transaction data for country adapters
/// </summary>
[GenerateSerializer]
public record FiscalTransactionData(
    [property: Id(0)] Guid TransactionId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] DateTime Timestamp,
    [property: Id(3)] string TransactionType,
    [property: Id(4)] decimal GrossAmount,
    [property: Id(5)] Dictionary<string, decimal> NetAmounts,
    [property: Id(6)] Dictionary<string, decimal> TaxAmounts,
    [property: Id(7)] Dictionary<string, decimal> PaymentTypes,
    [property: Id(8)] string? SourceType,
    [property: Id(9)] Guid? SourceId,
    [property: Id(10)] string? OperatorId,
    [property: Id(11)] Dictionary<string, string>? AdditionalData);

/// <summary>
/// Date range for audit exports
/// </summary>
[GenerateSerializer]
public record FiscalDateRange(
    [property: Id(0)] DateTime StartDate,
    [property: Id(1)] DateTime EndDate);

/// <summary>
/// Configuration validation result
/// </summary>
[GenerateSerializer]
public record FiscalConfigValidationResult(
    [property: Id(0)] bool IsValid,
    [property: Id(1)] List<string> Errors,
    [property: Id(2)] List<string> Warnings,
    [property: Id(3)] Dictionary<string, string>? Metadata);

/// <summary>
/// Interface for country-specific fiscal compliance adapters.
/// Each country has unique requirements for transaction recording, signing, and audit exports.
/// </summary>
public interface IFiscalCountryAdapter
{
    /// <summary>
    /// The country this adapter handles
    /// </summary>
    FiscalCountry Country { get; }

    /// <summary>
    /// Human-readable name for the compliance standard
    /// </summary>
    string ComplianceStandard { get; }

    /// <summary>
    /// Record a fiscal transaction according to country-specific requirements
    /// </summary>
    Task<FiscalResult> RecordTransactionAsync(FiscalTransactionData transaction, CancellationToken ct = default);

    /// <summary>
    /// Generate an audit export file in the country-specific format
    /// </summary>
    Task<byte[]> GenerateAuditExportAsync(FiscalDateRange range, CancellationToken ct = default);

    /// <summary>
    /// Validate the current configuration for this country
    /// </summary>
    Task<FiscalConfigValidationResult> ValidateConfigurationAsync(CancellationToken ct = default);

    /// <summary>
    /// Get the current status of the fiscal device/service
    /// </summary>
    Task<FiscalDeviceHealthStatus> GetHealthStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Perform any required daily closing operations (Z-report, etc.)
    /// </summary>
    Task<FiscalResult> PerformDailyCloseAsync(DateTime businessDate, CancellationToken ct = default);

    /// <summary>
    /// Check if the adapter supports a specific feature
    /// </summary>
    bool SupportsFeature(FiscalFeature feature);
}

/// <summary>
/// Features that may or may not be supported by country adapters
/// </summary>
public enum FiscalFeature
{
    /// <summary>Hardware TSE device support</summary>
    HardwareTse,
    /// <summary>Cloud-based TSE support</summary>
    CloudTse,
    /// <summary>Real-time transaction signing</summary>
    RealTimeSigning,
    /// <summary>Batch transaction submission</summary>
    BatchSubmission,
    /// <summary>Cumulative totals (grand total perpetuel)</summary>
    CumulativeTotals,
    /// <summary>Electronic journal generation</summary>
    ElectronicJournal,
    /// <summary>Invoice verification</summary>
    InvoiceVerification,
    /// <summary>VAT register export</summary>
    VatRegisterExport,
    /// <summary>QR code generation</summary>
    QrCodeGeneration,
    /// <summary>Certificate-based signing</summary>
    CertificateSigning
}

/// <summary>
/// Base class for country fiscal adapters with common functionality
/// </summary>
public abstract class BaseFiscalCountryAdapter : IFiscalCountryAdapter
{
    protected readonly Guid OrgId;
    protected readonly Guid SiteId;
    protected readonly IGrainFactory GrainFactory;
    protected readonly ILogger Logger;

    protected BaseFiscalCountryAdapter(
        Guid orgId,
        Guid siteId,
        IGrainFactory grainFactory,
        ILogger logger)
    {
        OrgId = orgId;
        SiteId = siteId;
        GrainFactory = grainFactory;
        Logger = logger;
    }

    public abstract FiscalCountry Country { get; }
    public abstract string ComplianceStandard { get; }

    public abstract Task<FiscalResult> RecordTransactionAsync(
        FiscalTransactionData transaction, CancellationToken ct = default);

    public abstract Task<byte[]> GenerateAuditExportAsync(
        FiscalDateRange range, CancellationToken ct = default);

    public abstract Task<FiscalConfigValidationResult> ValidateConfigurationAsync(
        CancellationToken ct = default);

    public abstract Task<FiscalDeviceHealthStatus> GetHealthStatusAsync(
        CancellationToken ct = default);

    public virtual Task<FiscalResult> PerformDailyCloseAsync(
        DateTime businessDate, CancellationToken ct = default)
    {
        // Default implementation - override in country-specific adapters
        return Task.FromResult(new FiscalResult(
            Success: true,
            TransactionId: null,
            Signature: null,
            SignatureCounter: null,
            CertificateSerial: null,
            QrCodeData: null,
            ErrorCode: null,
            ErrorMessage: null,
            Metadata: new Dictionary<string, string> { ["business_date"] = businessDate.ToString("yyyy-MM-dd") }));
    }

    public abstract bool SupportsFeature(FiscalFeature feature);

    /// <summary>
    /// Helper to create a failed result
    /// </summary>
    protected static FiscalResult FailedResult(string errorCode, string errorMessage)
    {
        return new FiscalResult(
            Success: false,
            TransactionId: null,
            Signature: null,
            SignatureCounter: null,
            CertificateSerial: null,
            QrCodeData: null,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage,
            Metadata: null);
    }

    /// <summary>
    /// Helper to create a successful result
    /// </summary>
    protected static FiscalResult SuccessResult(
        string? transactionId = null,
        string? signature = null,
        long? signatureCounter = null,
        string? certificateSerial = null,
        string? qrCodeData = null,
        Dictionary<string, string>? metadata = null)
    {
        return new FiscalResult(
            Success: true,
            TransactionId: transactionId,
            Signature: signature,
            SignatureCounter: signatureCounter,
            CertificateSerial: certificateSerial,
            QrCodeData: qrCodeData,
            ErrorCode: null,
            ErrorMessage: null,
            Metadata: metadata);
    }
}

/// <summary>
/// Factory for creating country-specific fiscal adapters
/// </summary>
public interface IFiscalCountryAdapterFactory
{
    /// <summary>
    /// Create an adapter for the specified country
    /// </summary>
    IFiscalCountryAdapter CreateAdapter(FiscalCountry country, Guid orgId, Guid siteId);

    /// <summary>
    /// Check if an adapter is available for the specified country
    /// </summary>
    bool IsCountrySupported(FiscalCountry country);

    /// <summary>
    /// Get all supported countries
    /// </summary>
    IReadOnlyList<FiscalCountry> GetSupportedCountries();
}
