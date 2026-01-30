namespace DarkVelocity.Fiscalisation.Api.Services;

/// <summary>
/// Result of a TSE initialization operation
/// </summary>
public record TseInitializationResult(
    bool Success,
    string? PublicKey,
    string? CertificateSerial,
    DateTime? CertificateExpiryDate,
    string? ClientId,
    string? ErrorMessage);

/// <summary>
/// Result of a transaction signing operation
/// </summary>
public record TseSigningResult(
    bool Success,
    long TransactionNumber,
    long SignatureCounter,
    string? Signature,
    string? CertificateSerial,
    DateTime StartTime,
    DateTime EndTime,
    string? QrCodeData,
    string? RawResponse,
    string? ErrorMessage);

/// <summary>
/// Result of a TSE self-test operation
/// </summary>
public record TseSelfTestResult(
    bool Success,
    string Status,
    DateTime? LastSyncAt,
    long TransactionCounter,
    long SignatureCounter,
    string? ErrorMessage);

/// <summary>
/// Interface for TSE (Technical Security Equipment) adapters.
/// Implementations handle communication with different TSE providers
/// (Swissbit, Fiskaly, Epson, etc.)
/// </summary>
public interface ITseAdapter
{
    /// <summary>
    /// The TSE device type this adapter handles
    /// </summary>
    string DeviceType { get; }

    /// <summary>
    /// Initialize the TSE device and register a client
    /// </summary>
    Task<TseInitializationResult> InitializeAsync(
        string serialNumber,
        string? apiEndpoint,
        string? apiCredentials,
        string? adminPin,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Start a new transaction
    /// </summary>
    Task<(bool Success, DateTime StartTime, string? ErrorMessage)> StartTransactionAsync(
        string clientId,
        string processType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finish and sign a transaction
    /// </summary>
    Task<TseSigningResult> FinishTransactionAsync(
        string clientId,
        string processType,
        string processData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform a self-test to verify TSE health
    /// </summary>
    Task<TseSelfTestResult> SelfTestAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deregister the client from the TSE
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> DecommissionAsync(
        string clientId,
        CancellationToken cancellationToken = default);
}
