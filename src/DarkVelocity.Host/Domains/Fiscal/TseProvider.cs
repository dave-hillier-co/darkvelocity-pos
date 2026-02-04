using System.Security.Cryptography;
using System.Text;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// TSE Provider Interface
// Abstraction for internal TSE-like behavior that can be mapped to external TSEs
// ============================================================================

/// <summary>
/// Result of starting a TSE transaction
/// </summary>
[GenerateSerializer]
public record TseStartTransactionResult(
    [property: Id(0)] long TransactionNumber,
    [property: Id(1)] DateTime StartTime,
    [property: Id(2)] string? ClientId,
    [property: Id(3)] bool Success,
    [property: Id(4)] string? ErrorMessage);

/// <summary>
/// Result of finishing a TSE transaction
/// </summary>
[GenerateSerializer]
public record TseFinishTransactionResult(
    [property: Id(0)] long TransactionNumber,
    [property: Id(1)] long SignatureCounter,
    [property: Id(2)] string Signature,
    [property: Id(3)] string SignatureAlgorithm,
    [property: Id(4)] string PublicKeyBase64,
    [property: Id(5)] DateTime StartTime,
    [property: Id(6)] DateTime EndTime,
    [property: Id(7)] string CertificateSerial,
    [property: Id(8)] string QrCodeData,
    [property: Id(9)] bool Success,
    [property: Id(10)] string? ErrorMessage);

/// <summary>
/// Result of TSE self-test
/// </summary>
[GenerateSerializer]
public record TseSelfTestResult(
    [property: Id(0)] bool Passed,
    [property: Id(1)] string? ErrorMessage,
    [property: Id(2)] DateTime PerformedAt);

/// <summary>
/// TSE transaction context for tracking in-flight transactions
/// </summary>
[GenerateSerializer]
public record TseTransactionContext(
    [property: Id(0)] Guid TseTransactionId,
    [property: Id(1)] long TransactionNumber,
    [property: Id(2)] DateTime StartTime,
    [property: Id(3)] string ProcessType,
    [property: Id(4)] string ProcessData,
    [property: Id(5)] string? ClientId);

/// <summary>
/// Interface for TSE providers. Implementations can be internal (software-based)
/// or external (hardware/cloud TSE devices).
/// </summary>
public interface ITseProvider
{
    /// <summary>
    /// Start a new TSE transaction
    /// </summary>
    Task<TseStartTransactionResult> StartTransactionAsync(
        string processType,
        string processData,
        string? clientId);

    /// <summary>
    /// Update transaction data (add line items, payments, etc.)
    /// </summary>
    Task<bool> UpdateTransactionAsync(
        long transactionNumber,
        string processData);

    /// <summary>
    /// Finish transaction and generate signature
    /// </summary>
    Task<TseFinishTransactionResult> FinishTransactionAsync(
        long transactionNumber,
        string processType,
        string processData);

    /// <summary>
    /// Perform TSE self-test
    /// </summary>
    Task<TseSelfTestResult> SelfTestAsync();

    /// <summary>
    /// Get the TSE certificate serial number
    /// </summary>
    Task<string> GetCertificateSerialAsync();

    /// <summary>
    /// Get the TSE public key in base64 format
    /// </summary>
    Task<string> GetPublicKeyBase64Async();

    /// <summary>
    /// Check if this is an internal (software) TSE or external
    /// </summary>
    bool IsInternal { get; }

    /// <summary>
    /// Provider type identifier
    /// </summary>
    string ProviderType { get; }
}

// ============================================================================
// Internal TSE Provider
// Software-based TSE that generates signatures locally
// ============================================================================

/// <summary>
/// Internal TSE provider that generates TSE-like signatures using HMAC-SHA256.
/// This can be used standalone or as a source of events to forward to an external TSE.
/// </summary>
public class InternalTseProvider : ITseProvider
{
    private readonly byte[] _signingKey;
    private readonly string _certificateSerial;
    private readonly string _publicKeyBase64;
    private long _transactionCounter;
    private long _signatureCounter;
    private readonly Dictionary<long, TseTransactionContext> _activeTransactions = new();

    public InternalTseProvider(
        byte[]? signingKey = null,
        string? certificateSerial = null,
        long initialTransactionCounter = 0,
        long initialSignatureCounter = 0)
    {
        // Use provided key or generate a deterministic one for testing
        _signingKey = signingKey ?? GenerateDefaultKey();
        _certificateSerial = certificateSerial ?? GenerateSerialNumber();
        _publicKeyBase64 = Convert.ToBase64String(_signingKey[..32]); // Use first 32 bytes as "public key"
        _transactionCounter = initialTransactionCounter;
        _signatureCounter = initialSignatureCounter;
    }

    public bool IsInternal => true;
    public string ProviderType => "InternalTse";

    public Task<TseStartTransactionResult> StartTransactionAsync(
        string processType,
        string processData,
        string? clientId)
    {
        var transactionNumber = Interlocked.Increment(ref _transactionCounter);
        var startTime = DateTime.UtcNow;

        var context = new TseTransactionContext(
            TseTransactionId: Guid.NewGuid(),
            TransactionNumber: transactionNumber,
            StartTime: startTime,
            ProcessType: processType,
            ProcessData: processData,
            ClientId: clientId);

        _activeTransactions[transactionNumber] = context;

        return Task.FromResult(new TseStartTransactionResult(
            TransactionNumber: transactionNumber,
            StartTime: startTime,
            ClientId: clientId,
            Success: true,
            ErrorMessage: null));
    }

    public Task<bool> UpdateTransactionAsync(long transactionNumber, string processData)
    {
        if (!_activeTransactions.TryGetValue(transactionNumber, out var context))
            return Task.FromResult(false);

        // Update the process data
        _activeTransactions[transactionNumber] = context with { ProcessData = processData };
        return Task.FromResult(true);
    }

    public Task<TseFinishTransactionResult> FinishTransactionAsync(
        long transactionNumber,
        string processType,
        string processData)
    {
        if (!_activeTransactions.TryGetValue(transactionNumber, out var context))
        {
            return Task.FromResult(new TseFinishTransactionResult(
                TransactionNumber: transactionNumber,
                SignatureCounter: 0,
                Signature: string.Empty,
                SignatureAlgorithm: string.Empty,
                PublicKeyBase64: string.Empty,
                StartTime: DateTime.MinValue,
                EndTime: DateTime.MinValue,
                CertificateSerial: string.Empty,
                QrCodeData: string.Empty,
                Success: false,
                ErrorMessage: $"Transaction {transactionNumber} not found"));
        }

        var signatureCounter = Interlocked.Increment(ref _signatureCounter);
        var endTime = DateTime.UtcNow;

        // Generate signature using HMAC-SHA256
        var signatureData = BuildSignatureData(
            transactionNumber,
            context.StartTime,
            endTime,
            processType,
            processData,
            signatureCounter);

        var signature = GenerateSignature(signatureData);

        // Generate QR code data per KassenSichV format
        var qrCodeData = BuildQrCodeData(
            transactionNumber,
            context.StartTime,
            endTime,
            processType,
            processData,
            signatureCounter,
            signature);

        _activeTransactions.Remove(transactionNumber);

        return Task.FromResult(new TseFinishTransactionResult(
            TransactionNumber: transactionNumber,
            SignatureCounter: signatureCounter,
            Signature: signature,
            SignatureAlgorithm: "HMAC-SHA256",
            PublicKeyBase64: _publicKeyBase64,
            StartTime: context.StartTime,
            EndTime: endTime,
            CertificateSerial: _certificateSerial,
            QrCodeData: qrCodeData,
            Success: true,
            ErrorMessage: null));
    }

    public Task<TseSelfTestResult> SelfTestAsync()
    {
        // Perform basic self-test: verify signing works
        try
        {
            var testData = "SELF_TEST_" + DateTime.UtcNow.Ticks;
            var signature = GenerateSignature(testData);
            var passed = !string.IsNullOrEmpty(signature);

            return Task.FromResult(new TseSelfTestResult(
                Passed: passed,
                ErrorMessage: passed ? null : "Signature generation failed",
                PerformedAt: DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TseSelfTestResult(
                Passed: false,
                ErrorMessage: ex.Message,
                PerformedAt: DateTime.UtcNow));
        }
    }

    public Task<string> GetCertificateSerialAsync() => Task.FromResult(_certificateSerial);

    public Task<string> GetPublicKeyBase64Async() => Task.FromResult(_publicKeyBase64);

    public long GetTransactionCounter() => _transactionCounter;
    public long GetSignatureCounter() => _signatureCounter;

    private string GenerateSignature(string data)
    {
        using var hmac = new HMACSHA256(_signingKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    private static string BuildSignatureData(
        long transactionNumber,
        DateTime startTime,
        DateTime endTime,
        string processType,
        string processData,
        long signatureCounter)
    {
        return string.Join(";",
            transactionNumber.ToString(),
            FormatTime(startTime),
            FormatTime(endTime),
            processType,
            processData,
            signatureCounter.ToString());
    }

    private string BuildQrCodeData(
        long transactionNumber,
        DateTime startTime,
        DateTime endTime,
        string processType,
        string processData,
        long signatureCounter,
        string signature)
    {
        // KassenSichV QR code format (simplified):
        // V0;TseSerial;SignAlgo;TimeFormat;TransNo;StartTime;EndTime;ProcessType;ProcessData;SigCounter;Signature
        return string.Join(";",
            "V0",
            _certificateSerial,
            "HMAC-SHA256",
            "utcTime",
            transactionNumber.ToString(),
            FormatTime(startTime),
            FormatTime(endTime),
            processType,
            processData,
            signatureCounter.ToString(),
            signature);
    }

    private static string FormatTime(DateTime time) =>
        time.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    private static byte[] GenerateDefaultKey()
    {
        // Generate a deterministic key for development/testing
        // In production, this would come from secure configuration
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes("DarkVelocity-Internal-TSE-Key-v1"));
    }

    private static string GenerateSerialNumber()
    {
        // Generate a pseudo-random serial number
        return "DVTSE" + DateTime.UtcNow.Ticks.ToString("X");
    }
}

// ============================================================================
// External TSE Mapping Configuration
// ============================================================================

/// <summary>
/// Configuration for mapping internal TSE events to an external TSE
/// </summary>
[GenerateSerializer]
public record ExternalTseMappingConfig(
    [property: Id(0)] bool Enabled,
    [property: Id(1)] Guid ExternalDeviceId,
    [property: Id(2)] ExternalTseType ExternalTseType,
    [property: Id(3)] string? ApiEndpoint,
    [property: Id(4)] string? ClientId,
    [property: Id(5)] bool ForwardAllEvents,
    [property: Id(6)] bool RequireExternalSignature);

/// <summary>
/// Types of external TSE devices
/// </summary>
public enum ExternalTseType
{
    None,
    SwissbitCloud,
    SwissbitUsb,
    FiskalyCloud,
    Epson,
    Diebold,
    Custom
}

/// <summary>
/// Factory for creating TSE providers based on configuration
/// </summary>
public static class TseProviderFactory
{
    public static ITseProvider CreateProvider(
        ExternalTseMappingConfig? config,
        byte[]? signingKey = null,
        long initialTransactionCounter = 0,
        long initialSignatureCounter = 0)
    {
        // If no external mapping or not enabled, use internal provider
        if (config == null || !config.Enabled || config.ExternalTseType == ExternalTseType.None)
        {
            return new InternalTseProvider(
                signingKey,
                initialTransactionCounter: initialTransactionCounter,
                initialSignatureCounter: initialSignatureCounter);
        }

        // External TSE providers would be implemented here
        // For now, fall back to internal provider
        // In a full implementation, this would return SwissbitTseProvider, FiskalyTseProvider, etc.
        return new InternalTseProvider(
            signingKey,
            initialTransactionCounter: initialTransactionCounter,
            initialSignatureCounter: initialSignatureCounter);
    }
}
