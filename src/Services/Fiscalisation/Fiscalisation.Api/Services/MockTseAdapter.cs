using System.Security.Cryptography;
using System.Text;

namespace DarkVelocity.Fiscalisation.Api.Services;

/// <summary>
/// Mock TSE adapter for development and testing.
/// Simulates TSE behavior without requiring actual hardware or cloud service.
/// </summary>
public class MockTseAdapter : ITseAdapter
{
    private long _transactionCounter;
    private long _signatureCounter;
    private readonly string _mockSerialNumber;
    private readonly string _mockCertificateSerial;
    private DateTime? _transactionStartTime;

    public MockTseAdapter()
    {
        _mockSerialNumber = $"MOCK-TSE-{Guid.NewGuid():N}".Substring(0, 20).ToUpper();
        _mockCertificateSerial = $"MOCK-CERT-{Guid.NewGuid():N}".Substring(0, 16).ToUpper();
        _transactionCounter = 0;
        _signatureCounter = 0;
    }

    public string DeviceType => "MockTSE";

    public Task<TseInitializationResult> InitializeAsync(
        string serialNumber,
        string? apiEndpoint,
        string? apiCredentials,
        string? adminPin,
        CancellationToken cancellationToken = default)
    {
        // Simulate initialization delay
        var publicKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var result = new TseInitializationResult(
            Success: true,
            PublicKey: publicKey,
            CertificateSerial: _mockCertificateSerial,
            CertificateExpiryDate: DateTime.UtcNow.AddYears(5),
            ClientId: $"CLIENT-{Guid.NewGuid():N}".Substring(0, 12).ToUpper(),
            ErrorMessage: null);

        return Task.FromResult(result);
    }

    public Task<(bool Success, DateTime StartTime, string? ErrorMessage)> StartTransactionAsync(
        string clientId,
        string processType,
        CancellationToken cancellationToken = default)
    {
        _transactionStartTime = DateTime.UtcNow;
        return Task.FromResult((true, _transactionStartTime.Value, (string?)null));
    }

    public Task<TseSigningResult> FinishTransactionAsync(
        string clientId,
        string processType,
        string processData,
        CancellationToken cancellationToken = default)
    {
        var startTime = _transactionStartTime ?? DateTime.UtcNow.AddMilliseconds(-100);
        var endTime = DateTime.UtcNow;

        _transactionCounter++;
        _signatureCounter++;

        // Generate mock signature
        var signatureData = $"{_mockSerialNumber}|{_signatureCounter}|{startTime:O}|{endTime:O}|{processType}|{processData}";
        var signature = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(signatureData)));

        // Generate KassenSichV QR code data format
        var qrCodeData = GenerateQrCodeData(
            _mockSerialNumber,
            _signatureCounter,
            startTime,
            endTime,
            processType,
            processData,
            signature);

        var result = new TseSigningResult(
            Success: true,
            TransactionNumber: _transactionCounter,
            SignatureCounter: _signatureCounter,
            Signature: signature,
            CertificateSerial: _mockCertificateSerial,
            StartTime: startTime,
            EndTime: endTime,
            QrCodeData: qrCodeData,
            RawResponse: $"{{\"transactionNumber\":{_transactionCounter},\"signatureCounter\":{_signatureCounter}}}",
            ErrorMessage: null);

        _transactionStartTime = null;
        return Task.FromResult(result);
    }

    public Task<TseSelfTestResult> SelfTestAsync(CancellationToken cancellationToken = default)
    {
        var result = new TseSelfTestResult(
            Success: true,
            Status: "Active",
            LastSyncAt: DateTime.UtcNow,
            TransactionCounter: _transactionCounter,
            SignatureCounter: _signatureCounter,
            ErrorMessage: null);

        return Task.FromResult(result);
    }

    public Task<(bool Success, string? ErrorMessage)> DecommissionAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult((true, (string?)null));
    }

    /// <summary>
    /// Generate QR code data in KassenSichV format:
    /// V0;[TSE-Seriennummer];[Signaturzähler];[Start-Zeit];[Ende-Zeit];
    /// [Prozess-Typ];[Prozess-Daten];[Brutto-Umsätze];[Signatur-Base64]
    /// </summary>
    private static string GenerateQrCodeData(
        string serialNumber,
        long signatureCounter,
        DateTime startTime,
        DateTime endTime,
        string processType,
        string processData,
        string signature)
    {
        var startTimeStr = startTime.ToString("yyyy-MM-ddTHH:mm:ss");
        var endTimeStr = endTime.ToString("yyyy-MM-ddTHH:mm:ss");

        return $"V0;{serialNumber};{signatureCounter};{startTimeStr};{endTimeStr};{processType};{processData};{signature}";
    }
}
