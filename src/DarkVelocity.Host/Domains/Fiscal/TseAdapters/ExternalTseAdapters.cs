using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// External TSE Adapter Interfaces
// Hardware and cloud TSE device integrations for German KassenSichV compliance
// ============================================================================

/// <summary>
/// Configuration for external TSE adapters
/// </summary>
[GenerateSerializer]
public sealed record ExternalTseConfiguration(
    [property: Id(0)] ExternalTseType TseType,
    [property: Id(1)] string? ApiEndpoint,
    [property: Id(2)] string? ApiKey,
    [property: Id(3)] string? ApiSecret,
    [property: Id(4)] string? TssId,
    [property: Id(5)] string? ClientId,
    [property: Id(6)] string? DeviceSerial,
    [property: Id(7)] string? CertificatePath,
    [property: Id(8)] int TimeoutMs,
    [property: Id(9)] int RetryAttempts);

/// <summary>
/// Extended interface for external TSE adapters with additional capabilities
/// </summary>
public interface IExternalTseAdapter : ITseProvider
{
    /// <summary>
    /// Initialize connection to external TSE
    /// </summary>
    Task<bool> ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Disconnect from external TSE
    /// </summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Check if connected to TSE
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Get TSE device information
    /// </summary>
    Task<TseDeviceInfo> GetDeviceInfoAsync(CancellationToken ct = default);

    /// <summary>
    /// Export audit data from TSE
    /// </summary>
    Task<byte[]> ExportAuditDataAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);

    /// <summary>
    /// Register a client with the TSE
    /// </summary>
    Task<bool> RegisterClientAsync(string clientId, CancellationToken ct = default);

    /// <summary>
    /// Deregister a client from the TSE
    /// </summary>
    Task<bool> DeregisterClientAsync(string clientId, CancellationToken ct = default);
}

/// <summary>
/// TSE device information
/// </summary>
[GenerateSerializer]
public sealed record TseDeviceInfo(
    [property: Id(0)] string SerialNumber,
    [property: Id(1)] string FirmwareVersion,
    [property: Id(2)] string CertificateSerial,
    [property: Id(3)] string PublicKey,
    [property: Id(4)] DateTime? CertificateExpiryDate,
    [property: Id(5)] long TransactionCounter,
    [property: Id(6)] long SignatureCounter,
    [property: Id(7)] string State,
    [property: Id(8)] int? RemainingSignatures);

// ============================================================================
// Swissbit Cloud TSE Adapter
// ============================================================================

/// <summary>
/// Adapter for Swissbit cloud-based TSE
/// </summary>
public sealed class SwissbitCloudTseAdapter : IExternalTseAdapter
{
    private readonly ExternalTseConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SwissbitCloudTseAdapter> _logger;
    private string? _accessToken;
    private DateTime? _tokenExpiresAt;
    private long _transactionCounter;
    private long _signatureCounter;
    private readonly Dictionary<long, SwissbitTransactionContext> _activeTransactions = [];

    public SwissbitCloudTseAdapter(
        ExternalTseConfiguration config,
        HttpClient httpClient,
        ILogger<SwissbitCloudTseAdapter> logger)
    {
        _config = config;
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool IsInternal => false;
    public string ProviderType => "SwissbitCloud";
    public bool IsConnected => _accessToken != null && _tokenExpiresAt > DateTime.UtcNow;

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_config.ApiEndpoint}/auth/token",
                new { apiKey = _config.ApiKey, apiSecret = _config.ApiSecret },
                ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SwissbitAuthResponse>(ct);
                _accessToken = result?.AccessToken;
                _tokenExpiresAt = DateTime.UtcNow.AddSeconds(result?.ExpiresIn ?? 3600);
                _logger.LogInformation("Connected to Swissbit Cloud TSE");
                return true;
            }

            _logger.LogWarning("Failed to connect to Swissbit Cloud TSE: {Status}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to Swissbit Cloud TSE");
            return false;
        }
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _accessToken = null;
        _tokenExpiresAt = null;
        return Task.CompletedTask;
    }

    public async Task<TseStartTransactionResult> StartTransactionAsync(
        string processType, string processData, string? clientId)
    {
        if (!IsConnected)
        {
            return new TseStartTransactionResult(0, DateTime.MinValue, clientId, false, "Not connected");
        }

        try
        {
            var txId = Guid.NewGuid().ToString("N");
            var request = new HttpRequestMessage(HttpMethod.Put,
                $"{_config.ApiEndpoint}/tss/{_config.TssId}/tx/{txId}?tx_revision=1")
            {
                Content = JsonContent.Create(new { state = "ACTIVE", client_id = clientId ?? _config.ClientId })
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SwissbitTransactionResponse>();
                var transactionNumber = result?.Number ?? ++_transactionCounter;

                _activeTransactions[transactionNumber] = new SwissbitTransactionContext(
                    txId, transactionNumber, DateTime.UtcNow, processType, processData, clientId);

                return new TseStartTransactionResult(transactionNumber, DateTime.UtcNow, clientId, true, null);
            }

            var error = await response.Content.ReadAsStringAsync();
            return new TseStartTransactionResult(0, DateTime.MinValue, clientId, false, error);
        }
        catch (Exception ex)
        {
            return new TseStartTransactionResult(0, DateTime.MinValue, clientId, false, ex.Message);
        }
    }

    public async Task<bool> UpdateTransactionAsync(long transactionNumber, string processData)
    {
        if (!_activeTransactions.TryGetValue(transactionNumber, out var context))
            return false;

        _activeTransactions[transactionNumber] = context with { ProcessData = processData };
        return true;
    }

    public async Task<TseFinishTransactionResult> FinishTransactionAsync(
        long transactionNumber, string processType, string processData)
    {
        if (!IsConnected || !_activeTransactions.TryGetValue(transactionNumber, out var context))
        {
            return new TseFinishTransactionResult(
                transactionNumber, 0, string.Empty, string.Empty, string.Empty,
                DateTime.MinValue, DateTime.MinValue, string.Empty, string.Empty,
                false, "Transaction not found or not connected");
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Put,
                $"{_config.ApiEndpoint}/tss/{_config.TssId}/tx/{context.ExternalTxId}?tx_revision=2")
            {
                Content = JsonContent.Create(new
                {
                    state = "FINISHED",
                    client_id = context.ClientId ?? _config.ClientId,
                    schema = new
                    {
                        standard_v1 = new
                        {
                            receipt = new
                            {
                                receipt_type = "RECEIPT",
                                amounts_per_vat_rate = new[] { new { vat_rate = "NORMAL", amount = "0.00" } },
                                amounts_per_payment_type = new[] { new { payment_type = "CASH", amount = "0.00" } }
                            }
                        }
                    }
                })
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SwissbitTransactionResponse>();
                _activeTransactions.Remove(transactionNumber);

                var signatureCounter = result?.Signature?.Counter ?? ++_signatureCounter;

                return new TseFinishTransactionResult(
                    TransactionNumber: transactionNumber,
                    SignatureCounter: signatureCounter,
                    Signature: result?.Signature?.Value ?? string.Empty,
                    SignatureAlgorithm: result?.Signature?.Algorithm ?? "ecdsa-plain-SHA256",
                    PublicKeyBase64: result?.Signature?.PublicKey ?? string.Empty,
                    StartTime: context.StartTime,
                    EndTime: DateTime.UtcNow,
                    CertificateSerial: _config.DeviceSerial ?? string.Empty,
                    QrCodeData: result?.QrCodeData ?? string.Empty,
                    Success: true,
                    ErrorMessage: null);
            }

            var error = await response.Content.ReadAsStringAsync();
            return new TseFinishTransactionResult(
                transactionNumber, 0, string.Empty, string.Empty, string.Empty,
                context.StartTime, DateTime.UtcNow, string.Empty, string.Empty,
                false, error);
        }
        catch (Exception ex)
        {
            return new TseFinishTransactionResult(
                transactionNumber, 0, string.Empty, string.Empty, string.Empty,
                context.StartTime, DateTime.UtcNow, string.Empty, string.Empty,
                false, ex.Message);
        }
    }

    public async Task<TseSelfTestResult> SelfTestAsync()
    {
        if (!IsConnected)
        {
            return new TseSelfTestResult(false, "Not connected", DateTime.UtcNow);
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_config.ApiEndpoint}/tss/{_config.TssId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);

            return new TseSelfTestResult(
                response.IsSuccessStatusCode,
                response.IsSuccessStatusCode ? null : $"Status: {response.StatusCode}",
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            return new TseSelfTestResult(false, ex.Message, DateTime.UtcNow);
        }
    }

    public Task<string> GetCertificateSerialAsync() => Task.FromResult(_config.DeviceSerial ?? string.Empty);

    public Task<string> GetPublicKeyBase64Async() => Task.FromResult(string.Empty);

    public async Task<TseDeviceInfo> GetDeviceInfoAsync(CancellationToken ct = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to TSE");
        }

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_config.ApiEndpoint}/tss/{_config.TssId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SwissbitTssInfo>(ct);

        return new TseDeviceInfo(
            SerialNumber: result?.SerialNumber ?? string.Empty,
            FirmwareVersion: "Cloud",
            CertificateSerial: result?.Certificate ?? string.Empty,
            PublicKey: result?.PublicKey ?? string.Empty,
            CertificateExpiryDate: null,
            TransactionCounter: _transactionCounter,
            SignatureCounter: _signatureCounter,
            State: result?.State ?? "UNKNOWN",
            RemainingSignatures: null);
    }

    public async Task<byte[]> ExportAuditDataAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to TSE");
        }

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{_config.ApiEndpoint}/tss/{_config.TssId}/export")
        {
            Content = JsonContent.Create(new
            {
                start_date = startDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                end_date = endDate.ToString("yyyy-MM-ddTHH:mm:ssZ")
            })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<bool> RegisterClientAsync(string clientId, CancellationToken ct = default)
    {
        if (!IsConnected) return false;

        var request = new HttpRequestMessage(HttpMethod.Put,
            $"{_config.ApiEndpoint}/tss/{_config.TssId}/client/{clientId}")
        {
            Content = JsonContent.Create(new { serial_number = clientId })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeregisterClientAsync(string clientId, CancellationToken ct = default)
    {
        if (!IsConnected) return false;

        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{_config.ApiEndpoint}/tss/{_config.TssId}/client/{clientId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    private sealed record SwissbitTransactionContext(
        string ExternalTxId,
        long TransactionNumber,
        DateTime StartTime,
        string ProcessType,
        string ProcessData,
        string? ClientId);
}

// ============================================================================
// Fiskaly Cloud TSE Adapter
// ============================================================================

/// <summary>
/// Adapter for Fiskaly cloud-based TSE (wraps existing Fiskaly integration)
/// </summary>
public sealed class FiskalyCloudTseAdapter : IExternalTseAdapter
{
    private readonly ExternalTseConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<FiskalyCloudTseAdapter> _logger;
    private string? _accessToken;
    private DateTime? _tokenExpiresAt;
    private long _transactionCounter;
    private long _signatureCounter;
    private readonly Dictionary<long, FiskalyTransactionContext> _activeTransactions = [];

    public FiskalyCloudTseAdapter(
        ExternalTseConfiguration config,
        HttpClient httpClient,
        ILogger<FiskalyCloudTseAdapter> logger)
    {
        _config = config;
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool IsInternal => false;
    public string ProviderType => "FiskalyCloud";
    public bool IsConnected => _accessToken != null && _tokenExpiresAt > DateTime.UtcNow;

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_config.ApiEndpoint}/auth",
                new { api_key = _config.ApiKey, api_secret = _config.ApiSecret },
                ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FiskalyAuthResponse>(ct);
                _accessToken = result?.AccessToken;
                _tokenExpiresAt = DateTime.UtcNow.AddSeconds(result?.ExpiresIn ?? 3600);
                _logger.LogInformation("Connected to Fiskaly Cloud TSE");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to Fiskaly Cloud TSE");
            return false;
        }
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _accessToken = null;
        _tokenExpiresAt = null;
        return Task.CompletedTask;
    }

    public async Task<TseStartTransactionResult> StartTransactionAsync(
        string processType, string processData, string? clientId)
    {
        if (!IsConnected)
        {
            return new TseStartTransactionResult(0, DateTime.MinValue, clientId, false, "Not connected");
        }

        try
        {
            var txId = Guid.NewGuid().ToString("N");
            var request = new HttpRequestMessage(HttpMethod.Put,
                $"{_config.ApiEndpoint}/tss/{_config.TssId}/tx/{txId}?tx_revision=1")
            {
                Content = JsonContent.Create(new { state = "ACTIVE", client_id = clientId ?? _config.ClientId })
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FiskalyTxResponse>();
                var transactionNumber = result?.Number ?? ++_transactionCounter;

                _activeTransactions[transactionNumber] = new FiskalyTransactionContext(
                    txId, transactionNumber, DateTime.UtcNow, processType, processData, clientId);

                return new TseStartTransactionResult(transactionNumber, DateTime.UtcNow, clientId, true, null);
            }

            var error = await response.Content.ReadAsStringAsync();
            return new TseStartTransactionResult(0, DateTime.MinValue, clientId, false, error);
        }
        catch (Exception ex)
        {
            return new TseStartTransactionResult(0, DateTime.MinValue, clientId, false, ex.Message);
        }
    }

    public Task<bool> UpdateTransactionAsync(long transactionNumber, string processData)
    {
        if (!_activeTransactions.TryGetValue(transactionNumber, out var context))
            return Task.FromResult(false);

        _activeTransactions[transactionNumber] = context with { ProcessData = processData };
        return Task.FromResult(true);
    }

    public async Task<TseFinishTransactionResult> FinishTransactionAsync(
        long transactionNumber, string processType, string processData)
    {
        if (!IsConnected || !_activeTransactions.TryGetValue(transactionNumber, out var context))
        {
            return new TseFinishTransactionResult(
                transactionNumber, 0, string.Empty, string.Empty, string.Empty,
                DateTime.MinValue, DateTime.MinValue, string.Empty, string.Empty,
                false, "Transaction not found or not connected");
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Put,
                $"{_config.ApiEndpoint}/tss/{_config.TssId}/tx/{context.ExternalTxId}?tx_revision=2")
            {
                Content = JsonContent.Create(new
                {
                    state = "FINISHED",
                    client_id = context.ClientId ?? _config.ClientId,
                    schema = new
                    {
                        standard_v1 = new
                        {
                            receipt = new
                            {
                                receipt_type = "RECEIPT",
                                amounts_per_vat_rate = new[] { new { vat_rate = "NORMAL", amount = "0.00" } },
                                amounts_per_payment_type = new[] { new { payment_type = "CASH", amount = "0.00" } }
                            }
                        }
                    }
                })
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FiskalyTxResponse>();
                _activeTransactions.Remove(transactionNumber);

                var signatureCounter = result?.Signature?.Counter ?? ++_signatureCounter;

                return new TseFinishTransactionResult(
                    TransactionNumber: transactionNumber,
                    SignatureCounter: signatureCounter,
                    Signature: result?.Signature?.Value ?? string.Empty,
                    SignatureAlgorithm: result?.Signature?.Algorithm ?? "ecdsa-plain-SHA256",
                    PublicKeyBase64: result?.Signature?.PublicKey ?? string.Empty,
                    StartTime: context.StartTime,
                    EndTime: DateTime.UtcNow,
                    CertificateSerial: _config.DeviceSerial ?? string.Empty,
                    QrCodeData: result?.QrCodeData ?? string.Empty,
                    Success: true,
                    ErrorMessage: null);
            }

            var error = await response.Content.ReadAsStringAsync();
            return new TseFinishTransactionResult(
                transactionNumber, 0, string.Empty, string.Empty, string.Empty,
                context.StartTime, DateTime.UtcNow, string.Empty, string.Empty,
                false, error);
        }
        catch (Exception ex)
        {
            return new TseFinishTransactionResult(
                transactionNumber, 0, string.Empty, string.Empty, string.Empty,
                context.StartTime, DateTime.UtcNow, string.Empty, string.Empty,
                false, ex.Message);
        }
    }

    public async Task<TseSelfTestResult> SelfTestAsync()
    {
        if (!IsConnected)
        {
            return new TseSelfTestResult(false, "Not connected", DateTime.UtcNow);
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_config.ApiEndpoint}/tss/{_config.TssId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);

            return new TseSelfTestResult(
                response.IsSuccessStatusCode,
                response.IsSuccessStatusCode ? null : $"Status: {response.StatusCode}",
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            return new TseSelfTestResult(false, ex.Message, DateTime.UtcNow);
        }
    }

    public Task<string> GetCertificateSerialAsync() => Task.FromResult(_config.DeviceSerial ?? string.Empty);

    public Task<string> GetPublicKeyBase64Async() => Task.FromResult(string.Empty);

    public async Task<TseDeviceInfo> GetDeviceInfoAsync(CancellationToken ct = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to TSE");
        }

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_config.ApiEndpoint}/tss/{_config.TssId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FiskalyTssInfo>(ct);

        return new TseDeviceInfo(
            SerialNumber: result?.SerialNumber ?? string.Empty,
            FirmwareVersion: "Cloud",
            CertificateSerial: result?.Certificate ?? string.Empty,
            PublicKey: result?.PublicKey ?? string.Empty,
            CertificateExpiryDate: null,
            TransactionCounter: _transactionCounter,
            SignatureCounter: _signatureCounter,
            State: result?.State ?? "UNKNOWN",
            RemainingSignatures: null);
    }

    public async Task<byte[]> ExportAuditDataAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to TSE");
        }

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{_config.ApiEndpoint}/tss/{_config.TssId}/export")
        {
            Content = JsonContent.Create(new
            {
                start_date = startDate.ToUniversalTime().Ticks / 10000,
                end_date = endDate.ToUniversalTime().Ticks / 10000
            })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<bool> RegisterClientAsync(string clientId, CancellationToken ct = default)
    {
        if (!IsConnected) return false;

        var request = new HttpRequestMessage(HttpMethod.Put,
            $"{_config.ApiEndpoint}/tss/{_config.TssId}/client/{clientId}")
        {
            Content = JsonContent.Create(new { serial_number = clientId })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeregisterClientAsync(string clientId, CancellationToken ct = default)
    {
        if (!IsConnected) return false;

        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{_config.ApiEndpoint}/tss/{_config.TssId}/client/{clientId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    private sealed record FiskalyTransactionContext(
        string ExternalTxId,
        long TransactionNumber,
        DateTime StartTime,
        string ProcessType,
        string ProcessData,
        string? ClientId);

    private sealed record FiskalyAuthResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record FiskalyTxResponse(
        [property: JsonPropertyName("_id")] string Id,
        [property: JsonPropertyName("number")] long Number,
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("signature")] FiskalySignatureInfo? Signature,
        [property: JsonPropertyName("qr_code_data")] string? QrCodeData);

    private sealed record FiskalySignatureInfo(
        [property: JsonPropertyName("value")] string Value,
        [property: JsonPropertyName("counter")] long Counter,
        [property: JsonPropertyName("algorithm")] string Algorithm,
        [property: JsonPropertyName("public_key")] string? PublicKey);

    private sealed record FiskalyTssInfo(
        [property: JsonPropertyName("_id")] string Id,
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("serial_number")] string? SerialNumber,
        [property: JsonPropertyName("certificate")] string? Certificate,
        [property: JsonPropertyName("public_key")] string? PublicKey);
}

// ============================================================================
// Diebold Nixdorf TSE Adapter
// ============================================================================

/// <summary>
/// Adapter for Diebold Nixdorf hardware TSE devices
/// </summary>
public sealed class DieboldNixdorfTseAdapter : IExternalTseAdapter
{
    private readonly ExternalTseConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DieboldNixdorfTseAdapter> _logger;
    private bool _isConnected;
    private string? _sessionId;
    private long _transactionCounter;
    private long _signatureCounter;
    private string _certificateSerial = string.Empty;
    private string _publicKey = string.Empty;
    private readonly Dictionary<long, DieboldTransactionContext> _activeTransactions = [];

    public DieboldNixdorfTseAdapter(
        ExternalTseConfiguration config,
        HttpClient httpClient,
        ILogger<DieboldNixdorfTseAdapter> logger)
    {
        _config = config;
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool IsInternal => false;
    public string ProviderType => "DieboldNixdorf";
    public bool IsConnected => _isConnected && _sessionId != null;

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            // Diebold Nixdorf uses a local REST API on the TSE device
            var response = await _httpClient.PostAsJsonAsync(
                $"{_config.ApiEndpoint}/api/v1/session/start",
                new { clientId = _config.ClientId },
                ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<DieboldSessionResponse>(ct);
                _sessionId = result?.SessionId;
                _isConnected = true;

                // Get device info
                var infoResponse = await _httpClient.GetAsync(
                    $"{_config.ApiEndpoint}/api/v1/tse/info",
                    ct);

                if (infoResponse.IsSuccessStatusCode)
                {
                    var info = await infoResponse.Content.ReadFromJsonAsync<DieboldTseInfo>(ct);
                    _certificateSerial = info?.SerialNumber ?? string.Empty;
                    _publicKey = info?.PublicKey ?? string.Empty;
                    _transactionCounter = info?.TransactionCounter ?? 0;
                    _signatureCounter = info?.SignatureCounter ?? 0;
                }

                _logger.LogInformation("Connected to Diebold Nixdorf TSE: {Serial}", _certificateSerial);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to Diebold Nixdorf TSE");
            return false;
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_sessionId != null)
        {
            try
            {
                await _httpClient.PostAsJsonAsync(
                    $"{_config.ApiEndpoint}/api/v1/session/end",
                    new { sessionId = _sessionId },
                    ct);
            }
            catch
            {
                // Ignore disconnect errors
            }
        }

        _sessionId = null;
        _isConnected = false;
    }

    public async Task<TseStartTransactionResult> StartTransactionAsync(
        string processType, string processData, string? clientId)
    {
        if (!IsConnected)
        {
            return new TseStartTransactionResult(0, DateTime.MinValue, clientId, false, "Not connected");
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_config.ApiEndpoint}/api/v1/transaction/start",
                new
                {
                    sessionId = _sessionId,
                    clientId = clientId ?? _config.ClientId,
                    processType,
                    processData
                });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<DieboldTransactionStartResponse>();
                var transactionNumber = result?.TransactionNumber ?? ++_transactionCounter;

                _activeTransactions[transactionNumber] = new DieboldTransactionContext(
                    transactionNumber, DateTime.UtcNow, processType, processData, clientId);

                return new TseStartTransactionResult(transactionNumber, DateTime.UtcNow, clientId, true, null);
            }

            var error = await response.Content.ReadAsStringAsync();
            return new TseStartTransactionResult(0, DateTime.MinValue, clientId, false, error);
        }
        catch (Exception ex)
        {
            return new TseStartTransactionResult(0, DateTime.MinValue, clientId, false, ex.Message);
        }
    }

    public Task<bool> UpdateTransactionAsync(long transactionNumber, string processData)
    {
        if (!_activeTransactions.TryGetValue(transactionNumber, out var context))
            return Task.FromResult(false);

        _activeTransactions[transactionNumber] = context with { ProcessData = processData };
        return Task.FromResult(true);
    }

    public async Task<TseFinishTransactionResult> FinishTransactionAsync(
        long transactionNumber, string processType, string processData)
    {
        if (!IsConnected || !_activeTransactions.TryGetValue(transactionNumber, out var context))
        {
            return new TseFinishTransactionResult(
                transactionNumber, 0, string.Empty, string.Empty, string.Empty,
                DateTime.MinValue, DateTime.MinValue, string.Empty, string.Empty,
                false, "Transaction not found or not connected");
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_config.ApiEndpoint}/api/v1/transaction/finish",
                new
                {
                    sessionId = _sessionId,
                    transactionNumber,
                    processType,
                    processData
                });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<DieboldTransactionFinishResponse>();
                _activeTransactions.Remove(transactionNumber);

                _signatureCounter = result?.SignatureCounter ?? _signatureCounter + 1;

                return new TseFinishTransactionResult(
                    TransactionNumber: transactionNumber,
                    SignatureCounter: _signatureCounter,
                    Signature: result?.Signature ?? string.Empty,
                    SignatureAlgorithm: "ecdsa-plain-SHA256",
                    PublicKeyBase64: _publicKey,
                    StartTime: context.StartTime,
                    EndTime: DateTime.UtcNow,
                    CertificateSerial: _certificateSerial,
                    QrCodeData: result?.QrCodeData ?? string.Empty,
                    Success: true,
                    ErrorMessage: null);
            }

            var error = await response.Content.ReadAsStringAsync();
            return new TseFinishTransactionResult(
                transactionNumber, 0, string.Empty, string.Empty, string.Empty,
                context.StartTime, DateTime.UtcNow, string.Empty, string.Empty,
                false, error);
        }
        catch (Exception ex)
        {
            return new TseFinishTransactionResult(
                transactionNumber, 0, string.Empty, string.Empty, string.Empty,
                context.StartTime, DateTime.UtcNow, string.Empty, string.Empty,
                false, ex.Message);
        }
    }

    public async Task<TseSelfTestResult> SelfTestAsync()
    {
        if (!IsConnected)
        {
            return new TseSelfTestResult(false, "Not connected", DateTime.UtcNow);
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_config.ApiEndpoint}/api/v1/tse/selftest",
                new { sessionId = _sessionId });

            var result = await response.Content.ReadFromJsonAsync<DieboldSelfTestResponse>();

            return new TseSelfTestResult(
                result?.Passed ?? false,
                result?.ErrorMessage,
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            return new TseSelfTestResult(false, ex.Message, DateTime.UtcNow);
        }
    }

    public Task<string> GetCertificateSerialAsync() => Task.FromResult(_certificateSerial);

    public Task<string> GetPublicKeyBase64Async() => Task.FromResult(_publicKey);

    public async Task<TseDeviceInfo> GetDeviceInfoAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(
            $"{_config.ApiEndpoint}/api/v1/tse/info",
            ct);

        response.EnsureSuccessStatusCode();

        var info = await response.Content.ReadFromJsonAsync<DieboldTseInfo>(ct);

        return new TseDeviceInfo(
            SerialNumber: info?.SerialNumber ?? string.Empty,
            FirmwareVersion: info?.FirmwareVersion ?? string.Empty,
            CertificateSerial: info?.SerialNumber ?? string.Empty,
            PublicKey: info?.PublicKey ?? string.Empty,
            CertificateExpiryDate: info?.CertificateExpiry,
            TransactionCounter: info?.TransactionCounter ?? 0,
            SignatureCounter: info?.SignatureCounter ?? 0,
            State: info?.State ?? "UNKNOWN",
            RemainingSignatures: info?.RemainingSignatures);
    }

    public async Task<byte[]> ExportAuditDataAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"{_config.ApiEndpoint}/api/v1/tse/export",
            new
            {
                sessionId = _sessionId,
                startDate = startDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                endDate = endDate.ToString("yyyy-MM-ddTHH:mm:ssZ")
            },
            ct);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<bool> RegisterClientAsync(string clientId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"{_config.ApiEndpoint}/api/v1/client/register",
            new { sessionId = _sessionId, clientId },
            ct);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeregisterClientAsync(string clientId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"{_config.ApiEndpoint}/api/v1/client/deregister",
            new { sessionId = _sessionId, clientId },
            ct);

        return response.IsSuccessStatusCode;
    }

    private sealed record DieboldTransactionContext(
        long TransactionNumber,
        DateTime StartTime,
        string ProcessType,
        string ProcessData,
        string? ClientId);

    private sealed record DieboldSessionResponse(string? SessionId);

    private sealed record DieboldTseInfo(
        string? SerialNumber,
        string? FirmwareVersion,
        string? PublicKey,
        string? State,
        DateTime? CertificateExpiry,
        long TransactionCounter,
        long SignatureCounter,
        int? RemainingSignatures);

    private sealed record DieboldTransactionStartResponse(long TransactionNumber);

    private sealed record DieboldTransactionFinishResponse(
        string Signature,
        long SignatureCounter,
        string QrCodeData);

    private sealed record DieboldSelfTestResponse(bool Passed, string? ErrorMessage);
}

// ============================================================================
// Swissbit USB TSE Adapter (Hardware)
// ============================================================================

/// <summary>
/// Adapter for Swissbit USB hardware TSE devices
/// Communicates with local TSE driver/daemon
/// </summary>
public sealed class SwissbitUsbTseAdapter : IExternalTseAdapter
{
    private readonly ExternalTseConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SwissbitUsbTseAdapter> _logger;
    private bool _isConnected;
    private string? _sessionId;
    private long _transactionCounter;
    private long _signatureCounter;
    private string _certificateSerial = string.Empty;
    private string _publicKey = string.Empty;
    private readonly Dictionary<long, SwissbitUsbTransactionContext> _activeTransactions = [];

    public SwissbitUsbTseAdapter(
        ExternalTseConfiguration config,
        HttpClient httpClient,
        ILogger<SwissbitUsbTseAdapter> logger)
    {
        _config = config;
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool IsInternal => false;
    public string ProviderType => "SwissbitUSB";
    public bool IsConnected => _isConnected;

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            // Swissbit USB TSE typically uses a local daemon running on the POS device
            // Default endpoint is usually http://localhost:8080 or similar
            var endpoint = _config.ApiEndpoint ?? "http://localhost:8080";

            var response = await _httpClient.PostAsJsonAsync(
                $"{endpoint}/api/v1/tse/initialize",
                new { clientId = _config.ClientId, adminPin = _config.ApiKey },
                ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SwissbitUsbInitResponse>(ct);
                _certificateSerial = result?.SerialNumber ?? string.Empty;
                _publicKey = result?.PublicKey ?? string.Empty;
                _transactionCounter = result?.TransactionCounter ?? 0;
                _signatureCounter = result?.SignatureCounter ?? 0;
                _isConnected = true;

                _logger.LogInformation("Connected to Swissbit USB TSE: {Serial}", _certificateSerial);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to Swissbit USB TSE");
            return false;
        }
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _isConnected = false;
        _sessionId = null;
        return Task.CompletedTask;
    }

    public async Task<TseStartTransactionResult> StartTransactionAsync(
        string processType, string processData, string? clientId)
    {
        if (!IsConnected)
        {
            return new TseStartTransactionResult(0, DateTime.MinValue, clientId, false, "Not connected");
        }

        try
        {
            var endpoint = _config.ApiEndpoint ?? "http://localhost:8080";
            var response = await _httpClient.PostAsJsonAsync(
                $"{endpoint}/api/v1/transaction/start",
                new
                {
                    clientId = clientId ?? _config.ClientId,
                    processType,
                    processData
                });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SwissbitUsbTxStartResponse>();
                var transactionNumber = result?.TransactionNumber ?? ++_transactionCounter;

                _activeTransactions[transactionNumber] = new SwissbitUsbTransactionContext(
                    transactionNumber, DateTime.UtcNow, processType, processData, clientId);

                return new TseStartTransactionResult(transactionNumber, DateTime.UtcNow, clientId, true, null);
            }

            var error = await response.Content.ReadAsStringAsync();
            return new TseStartTransactionResult(0, DateTime.MinValue, clientId, false, error);
        }
        catch (Exception ex)
        {
            return new TseStartTransactionResult(0, DateTime.MinValue, clientId, false, ex.Message);
        }
    }

    public Task<bool> UpdateTransactionAsync(long transactionNumber, string processData)
    {
        if (!_activeTransactions.TryGetValue(transactionNumber, out var context))
            return Task.FromResult(false);

        _activeTransactions[transactionNumber] = context with { ProcessData = processData };
        return Task.FromResult(true);
    }

    public async Task<TseFinishTransactionResult> FinishTransactionAsync(
        long transactionNumber, string processType, string processData)
    {
        if (!IsConnected || !_activeTransactions.TryGetValue(transactionNumber, out var context))
        {
            return new TseFinishTransactionResult(
                transactionNumber, 0, string.Empty, string.Empty, string.Empty,
                DateTime.MinValue, DateTime.MinValue, string.Empty, string.Empty,
                false, "Transaction not found or not connected");
        }

        try
        {
            var endpoint = _config.ApiEndpoint ?? "http://localhost:8080";
            var response = await _httpClient.PostAsJsonAsync(
                $"{endpoint}/api/v1/transaction/finish",
                new
                {
                    transactionNumber,
                    processType,
                    processData
                });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SwissbitUsbTxFinishResponse>();
                _activeTransactions.Remove(transactionNumber);

                _signatureCounter = result?.SignatureCounter ?? _signatureCounter + 1;

                return new TseFinishTransactionResult(
                    TransactionNumber: transactionNumber,
                    SignatureCounter: _signatureCounter,
                    Signature: result?.Signature ?? string.Empty,
                    SignatureAlgorithm: "ecdsa-plain-SHA256",
                    PublicKeyBase64: _publicKey,
                    StartTime: context.StartTime,
                    EndTime: DateTime.UtcNow,
                    CertificateSerial: _certificateSerial,
                    QrCodeData: result?.QrCodeData ?? string.Empty,
                    Success: true,
                    ErrorMessage: null);
            }

            var error = await response.Content.ReadAsStringAsync();
            return new TseFinishTransactionResult(
                transactionNumber, 0, string.Empty, string.Empty, string.Empty,
                context.StartTime, DateTime.UtcNow, string.Empty, string.Empty,
                false, error);
        }
        catch (Exception ex)
        {
            return new TseFinishTransactionResult(
                transactionNumber, 0, string.Empty, string.Empty, string.Empty,
                context.StartTime, DateTime.UtcNow, string.Empty, string.Empty,
                false, ex.Message);
        }
    }

    public async Task<TseSelfTestResult> SelfTestAsync()
    {
        if (!IsConnected)
        {
            return new TseSelfTestResult(false, "Not connected", DateTime.UtcNow);
        }

        try
        {
            var endpoint = _config.ApiEndpoint ?? "http://localhost:8080";
            var response = await _httpClient.PostAsync($"{endpoint}/api/v1/tse/selftest", null);

            return new TseSelfTestResult(
                response.IsSuccessStatusCode,
                response.IsSuccessStatusCode ? null : $"Status: {response.StatusCode}",
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            return new TseSelfTestResult(false, ex.Message, DateTime.UtcNow);
        }
    }

    public Task<string> GetCertificateSerialAsync() => Task.FromResult(_certificateSerial);

    public Task<string> GetPublicKeyBase64Async() => Task.FromResult(_publicKey);

    public async Task<TseDeviceInfo> GetDeviceInfoAsync(CancellationToken ct = default)
    {
        var endpoint = _config.ApiEndpoint ?? "http://localhost:8080";
        var response = await _httpClient.GetAsync($"{endpoint}/api/v1/tse/info", ct);
        response.EnsureSuccessStatusCode();

        var info = await response.Content.ReadFromJsonAsync<SwissbitUsbTseInfo>(ct);

        return new TseDeviceInfo(
            SerialNumber: info?.SerialNumber ?? string.Empty,
            FirmwareVersion: info?.FirmwareVersion ?? string.Empty,
            CertificateSerial: info?.SerialNumber ?? string.Empty,
            PublicKey: info?.PublicKey ?? string.Empty,
            CertificateExpiryDate: info?.CertificateExpiry,
            TransactionCounter: info?.TransactionCounter ?? 0,
            SignatureCounter: info?.SignatureCounter ?? 0,
            State: info?.State ?? "UNKNOWN",
            RemainingSignatures: info?.RemainingSignatures);
    }

    public async Task<byte[]> ExportAuditDataAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        var endpoint = _config.ApiEndpoint ?? "http://localhost:8080";
        var response = await _httpClient.PostAsJsonAsync(
            $"{endpoint}/api/v1/tse/export",
            new
            {
                startDate = startDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                endDate = endDate.ToString("yyyy-MM-ddTHH:mm:ssZ")
            },
            ct);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<bool> RegisterClientAsync(string clientId, CancellationToken ct = default)
    {
        var endpoint = _config.ApiEndpoint ?? "http://localhost:8080";
        var response = await _httpClient.PostAsJsonAsync(
            $"{endpoint}/api/v1/client/register",
            new { clientId },
            ct);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeregisterClientAsync(string clientId, CancellationToken ct = default)
    {
        var endpoint = _config.ApiEndpoint ?? "http://localhost:8080";
        var response = await _httpClient.PostAsJsonAsync(
            $"{endpoint}/api/v1/client/deregister",
            new { clientId },
            ct);

        return response.IsSuccessStatusCode;
    }

    private sealed record SwissbitUsbTransactionContext(
        long TransactionNumber,
        DateTime StartTime,
        string ProcessType,
        string ProcessData,
        string? ClientId);

    private sealed record SwissbitUsbInitResponse(
        string? SerialNumber,
        string? PublicKey,
        long TransactionCounter,
        long SignatureCounter);

    private sealed record SwissbitUsbTseInfo(
        string? SerialNumber,
        string? FirmwareVersion,
        string? PublicKey,
        string? State,
        DateTime? CertificateExpiry,
        long TransactionCounter,
        long SignatureCounter,
        int? RemainingSignatures);

    private sealed record SwissbitUsbTxStartResponse(long TransactionNumber);

    private sealed record SwissbitUsbTxFinishResponse(
        string Signature,
        long SignatureCounter,
        string QrCodeData);
}

// ============================================================================
// Swissbit DTOs (shared)
// ============================================================================

internal sealed record SwissbitAuthResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

internal sealed record SwissbitTransactionResponse(
    [property: JsonPropertyName("_id")] string Id,
    [property: JsonPropertyName("number")] long Number,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("signature")] SwissbitSignatureInfo? Signature,
    [property: JsonPropertyName("qr_code_data")] string? QrCodeData);

internal sealed record SwissbitSignatureInfo(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("counter")] long Counter,
    [property: JsonPropertyName("algorithm")] string Algorithm,
    [property: JsonPropertyName("public_key")] string? PublicKey);

internal sealed record SwissbitTssInfo(
    [property: JsonPropertyName("_id")] string Id,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("serial_number")] string? SerialNumber,
    [property: JsonPropertyName("certificate")] string? Certificate,
    [property: JsonPropertyName("public_key")] string? PublicKey);

// ============================================================================
// TSE Adapter Factory
// ============================================================================

/// <summary>
/// Factory for creating external TSE adapters
/// </summary>
public sealed class ExternalTseAdapterFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    public ExternalTseAdapterFactory(
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Create an external TSE adapter based on configuration
    /// </summary>
    public IExternalTseAdapter CreateAdapter(ExternalTseConfiguration config)
    {
        var httpClient = _httpClientFactory.CreateClient("TseAdapter");
        httpClient.Timeout = TimeSpan.FromMilliseconds(config.TimeoutMs > 0 ? config.TimeoutMs : 30000);

        return config.TseType switch
        {
            ExternalTseType.SwissbitCloud => new SwissbitCloudTseAdapter(
                config, httpClient, _loggerFactory.CreateLogger<SwissbitCloudTseAdapter>()),

            ExternalTseType.SwissbitUsb => new SwissbitUsbTseAdapter(
                config, httpClient, _loggerFactory.CreateLogger<SwissbitUsbTseAdapter>()),

            ExternalTseType.FiskalyCloud => new FiskalyCloudTseAdapter(
                config, httpClient, _loggerFactory.CreateLogger<FiskalyCloudTseAdapter>()),

            ExternalTseType.Diebold => new DieboldNixdorfTseAdapter(
                config, httpClient, _loggerFactory.CreateLogger<DieboldNixdorfTseAdapter>()),

            _ => throw new NotSupportedException($"TSE type {config.TseType} is not supported")
        };
    }
}
