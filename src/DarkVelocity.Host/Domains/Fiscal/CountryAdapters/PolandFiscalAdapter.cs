using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DarkVelocity.Host.Events;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Poland JPK/KSeF Fiscal Adapter
// Implements Polish fiscal compliance requirements including:
// - JPK (Jednolity Plik Kontrolny) - Uniform Control File
// - KSeF (Krajowy System e-Faktur) - National e-Invoice System
// - VAT register format
// - Invoice verification
// ============================================================================

/// <summary>
/// Poland-specific configuration for JPK/KSeF compliance
/// </summary>
[GenerateSerializer]
public sealed record PolandFiscalConfiguration(
    [property: Id(0)] bool Enabled,
    [property: Id(1)] string Nip,
    [property: Id(2)] string Regon,
    [property: Id(3)] string CompanyName,
    [property: Id(4)] string CompanyAddress,
    [property: Id(5)] string TaxOfficeCode,
    [property: Id(6)] PolandFiscalEnvironment Environment,
    [property: Id(7)] string? KsefApiToken,
    [property: Id(8)] string? KsefCertificateThumbprint,
    [property: Id(9)] bool AutoSubmitInvoices,
    [property: Id(10)] bool EnableOnlineFiscalPrinter,
    [property: Id(11)] string? FiscalPrinterSerial);

/// <summary>
/// Poland fiscal environment (test or production)
/// </summary>
public enum PolandFiscalEnvironment
{
    Test,
    Production
}

/// <summary>
/// JPK document type
/// </summary>
public enum JpkDocumentType
{
    /// <summary>JPK_FA - Faktury VAT (VAT Invoices)</summary>
    JPK_FA,
    /// <summary>JPK_VAT - Ewidencja VAT (VAT Register)</summary>
    JPK_VAT,
    /// <summary>JPK_MAG - Magazyn (Inventory)</summary>
    JPK_MAG,
    /// <summary>JPK_PKPIR - Podatkowa Ksiega Przychodow i Rozchodow</summary>
    JPK_PKPIR
}

/// <summary>
/// Polish VAT rate types
/// </summary>
public enum PolandVatRate
{
    /// <summary>23% standard rate</summary>
    Rate23,
    /// <summary>8% reduced rate</summary>
    Rate8,
    /// <summary>5% reduced rate</summary>
    Rate5,
    /// <summary>0% zero rate (export, etc.)</summary>
    Rate0,
    /// <summary>Exempt from VAT</summary>
    Exempt,
    /// <summary>Not applicable</summary>
    NotApplicable
}

/// <summary>
/// KSeF invoice submission status
/// </summary>
public enum KsefSubmissionStatus
{
    Pending,
    Submitted,
    Accepted,
    Rejected,
    Failed
}

/// <summary>
/// State for Polish fiscal grain
/// </summary>
[GenerateSerializer]
public sealed class PolandFiscalState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public PolandFiscalConfiguration? Configuration { get; set; }
    [Id(3)] public long InvoiceSequence { get; set; }
    [Id(4)] public long FiscalReceiptSequence { get; set; }
    [Id(5)] public List<PolandVatEntry> VatRegister { get; set; } = [];
    [Id(6)] public List<KsefInvoiceRecord> PendingKsefInvoices { get; set; } = [];
    [Id(7)] public Dictionary<string, string> KsefReferenceMap { get; set; } = [];
    [Id(8)] public DateOnly CurrentPeriodStart { get; set; }
    [Id(9)] public DateOnly CurrentPeriodEnd { get; set; }
    [Id(10)] public string? KsefSessionToken { get; set; }
    [Id(11)] public DateTime? KsefSessionExpiresAt { get; set; }
    [Id(12)] public int Version { get; set; }
}

/// <summary>
/// Polish VAT register entry
/// </summary>
[GenerateSerializer]
public sealed record PolandVatEntry(
    [property: Id(0)] Guid EntryId,
    [property: Id(1)] DateTime Timestamp,
    [property: Id(2)] string DocumentNumber,
    [property: Id(3)] string DocumentType,
    [property: Id(4)] string? CounterpartyNip,
    [property: Id(5)] string? CounterpartyName,
    [property: Id(6)] decimal NetAmount,
    [property: Id(7)] decimal VatAmount,
    [property: Id(8)] decimal GrossAmount,
    [property: Id(9)] PolandVatRate VatRate,
    [property: Id(10)] string? KsefNumber,
    [property: Id(11)] KsefSubmissionStatus KsefStatus);

/// <summary>
/// KSeF invoice record for tracking submission
/// </summary>
[GenerateSerializer]
public sealed record KsefInvoiceRecord(
    [property: Id(0)] Guid InvoiceId,
    [property: Id(1)] string InvoiceNumber,
    [property: Id(2)] DateTime IssueDate,
    [property: Id(3)] decimal GrossAmount,
    [property: Id(4)] string? CounterpartyNip,
    [property: Id(5)] string InvoiceXml,
    [property: Id(6)] KsefSubmissionStatus Status,
    [property: Id(7)] string? KsefNumber,
    [property: Id(8)] string? ErrorMessage,
    [property: Id(9)] DateTime? SubmittedAt,
    [property: Id(10)] DateTime? AcceptedAt);

/// <summary>
/// Polish JPK/KSeF fiscal adapter implementation
/// </summary>
public class PolandFiscalAdapter : BaseFiscalCountryAdapter
{
    private readonly IPersistentState<PolandFiscalState> _state;
    private readonly HttpClient? _httpClient;
    private readonly TimeProvider _timeProvider;

    private static readonly Dictionary<PolandVatRate, decimal> VatRateValues = new()
    {
        [PolandVatRate.Rate23] = 0.23m,
        [PolandVatRate.Rate8] = 0.08m,
        [PolandVatRate.Rate5] = 0.05m,
        [PolandVatRate.Rate0] = 0m,
        [PolandVatRate.Exempt] = 0m,
        [PolandVatRate.NotApplicable] = 0m
    };

    public PolandFiscalAdapter(
        Guid orgId,
        Guid siteId,
        IGrainFactory grainFactory,
        IPersistentState<PolandFiscalState> state,
        ILogger<PolandFiscalAdapter> logger,
        HttpClient? httpClient = null,
        TimeProvider? timeProvider = null)
        : base(orgId, siteId, grainFactory, logger)
    {
        _state = state;
        _httpClient = httpClient;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public override FiscalCountry Country => FiscalCountry.Poland;
    public override string ComplianceStandard => "JPK/KSeF";

    public override async Task<FiscalResult> RecordTransactionAsync(
        FiscalTransactionData transaction, CancellationToken ct = default)
    {
        if (_state.State.Configuration == null || !_state.State.Configuration.Enabled)
        {
            return FailedResult("NOT_CONFIGURED", "Polish fiscal compliance not configured");
        }

        try
        {
            // Determine VAT rate from transaction
            var vatRate = DetermineVatRate(transaction);

            // Create VAT register entry
            var documentNumber = GenerateDocumentNumber(transaction.TransactionType);
            var vatEntry = new PolandVatEntry(
                EntryId: Guid.NewGuid(),
                Timestamp: transaction.Timestamp,
                DocumentNumber: documentNumber,
                DocumentType: transaction.TransactionType,
                CounterpartyNip: transaction.AdditionalData?.GetValueOrDefault("counterparty_nip"),
                CounterpartyName: transaction.AdditionalData?.GetValueOrDefault("counterparty_name"),
                NetAmount: transaction.NetAmounts.Values.Sum(),
                VatAmount: transaction.TaxAmounts.Values.Sum(),
                GrossAmount: transaction.GrossAmount,
                VatRate: vatRate,
                KsefNumber: null,
                KsefStatus: KsefSubmissionStatus.Pending);

            _state.State.VatRegister.Add(vatEntry);

            // If this is an invoice and KSeF is enabled, prepare for submission
            string? ksefNumber = null;
            if (IsInvoice(transaction.TransactionType) && _state.State.Configuration.AutoSubmitInvoices)
            {
                var invoiceXml = GenerateKsefInvoiceXml(transaction, documentNumber);
                var invoiceRecord = new KsefInvoiceRecord(
                    InvoiceId: transaction.TransactionId,
                    InvoiceNumber: documentNumber,
                    IssueDate: transaction.Timestamp,
                    GrossAmount: transaction.GrossAmount,
                    CounterpartyNip: transaction.AdditionalData?.GetValueOrDefault("counterparty_nip"),
                    InvoiceXml: invoiceXml,
                    Status: KsefSubmissionStatus.Pending,
                    KsefNumber: null,
                    ErrorMessage: null,
                    SubmittedAt: null,
                    AcceptedAt: null);

                _state.State.PendingKsefInvoices.Add(invoiceRecord);

                // Try to submit immediately if we have a session
                if (await EnsureKsefSessionAsync(ct))
                {
                    var submitResult = await SubmitToKsefAsync(invoiceRecord, ct);
                    if (submitResult.Success)
                    {
                        ksefNumber = submitResult.TransactionId;
                    }
                }
            }

            _state.State.Version++;
            await _state.WriteStateAsync();

            Logger.LogInformation(
                "Recorded Polish fiscal transaction {DocumentNumber} for {Amount:C}",
                documentNumber, transaction.GrossAmount);

            return SuccessResult(
                transactionId: documentNumber,
                metadata: new Dictionary<string, string>
                {
                    ["document_number"] = documentNumber,
                    ["vat_rate"] = vatRate.ToString(),
                    ["ksef_status"] = ksefNumber != null ? "submitted" : "pending",
                    ["ksef_number"] = ksefNumber ?? ""
                });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to record Polish fiscal transaction");
            return FailedResult("RECORD_FAILED", ex.Message);
        }
    }

    public override async Task<byte[]> GenerateAuditExportAsync(
        FiscalDateRange range, CancellationToken ct = default)
    {
        if (_state.State.Configuration == null)
        {
            throw new InvalidOperationException("Polish fiscal compliance not configured");
        }

        // Generate JPK_VAT XML
        var jpkXml = GenerateJpkVatXml(range);
        return Encoding.UTF8.GetBytes(jpkXml);
    }

    public override async Task<FiscalConfigValidationResult> ValidateConfigurationAsync(
        CancellationToken ct = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (_state.State.Configuration == null)
        {
            errors.Add("Polish fiscal configuration is not set");
            return new FiscalConfigValidationResult(false, errors, warnings, null);
        }

        var config = _state.State.Configuration;

        if (string.IsNullOrEmpty(config.Nip))
            errors.Add("NIP (Tax ID) is required");
        else if (!ValidateNip(config.Nip))
            errors.Add("NIP (Tax ID) is invalid");

        if (string.IsNullOrEmpty(config.Regon))
            warnings.Add("REGON is recommended");

        if (string.IsNullOrEmpty(config.CompanyName))
            errors.Add("Company name is required");

        if (config.AutoSubmitInvoices && string.IsNullOrEmpty(config.KsefApiToken))
            errors.Add("KSeF API token is required when auto-submit is enabled");

        if (config.EnableOnlineFiscalPrinter && string.IsNullOrEmpty(config.FiscalPrinterSerial))
            errors.Add("Fiscal printer serial is required when online printer is enabled");

        return new FiscalConfigValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors,
            Warnings: warnings,
            Metadata: new Dictionary<string, string>
            {
                ["nip"] = config.Nip ?? "",
                ["compliance_standard"] = "JPK/KSeF"
            });
    }

    public override Task<FiscalDeviceHealthStatus> GetHealthStatusAsync(
        CancellationToken ct = default)
    {
        var status = new FiscalDeviceHealthStatus(
            DeviceId: Guid.Empty,
            Status: _state.State.Configuration?.Enabled == true
                ? FiscalDeviceStatus.Active
                : FiscalDeviceStatus.Inactive,
            IsOnline: true,
            CertificateValid: !string.IsNullOrEmpty(_state.State.Configuration?.KsefApiToken),
            DaysUntilCertificateExpiry: null,
            LastSyncAt: null,
            LastTransactionAt: _state.State.VatRegister.LastOrDefault()?.Timestamp,
            TotalTransactions: _state.State.VatRegister.Count,
            LastError: null);

        return Task.FromResult(status);
    }

    public override async Task<FiscalResult> PerformDailyCloseAsync(
        DateTime businessDate, CancellationToken ct = default)
    {
        try
        {
            // Submit any pending KSeF invoices
            var pendingCount = 0;
            var submittedCount = 0;

            if (_state.State.Configuration?.AutoSubmitInvoices == true)
            {
                foreach (var invoice in _state.State.PendingKsefInvoices
                    .Where(i => i.Status == KsefSubmissionStatus.Pending))
                {
                    pendingCount++;
                    if (await EnsureKsefSessionAsync(ct))
                    {
                        var result = await SubmitToKsefAsync(invoice, ct);
                        if (result.Success)
                        {
                            submittedCount++;
                        }
                    }
                }
            }

            _state.State.Version++;
            await _state.WriteStateAsync();

            Logger.LogInformation(
                "Polish daily close completed for {BusinessDate}, submitted {Submitted}/{Pending} KSeF invoices",
                businessDate.ToString("yyyy-MM-dd"), submittedCount, pendingCount);

            return SuccessResult(
                metadata: new Dictionary<string, string>
                {
                    ["business_date"] = businessDate.ToString("yyyy-MM-dd"),
                    ["pending_ksef_invoices"] = pendingCount.ToString(),
                    ["submitted_ksef_invoices"] = submittedCount.ToString()
                });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to perform Polish daily close");
            return FailedResult("DAILY_CLOSE_FAILED", ex.Message);
        }
    }

    public override bool SupportsFeature(FiscalFeature feature)
    {
        return feature switch
        {
            FiscalFeature.VatRegisterExport => true,
            FiscalFeature.InvoiceVerification => true,
            FiscalFeature.BatchSubmission => true,
            FiscalFeature.ElectronicJournal => true,
            _ => false
        };
    }

    // ========================================================================
    // Poland-specific public methods
    // ========================================================================

    /// <summary>
    /// Configure Polish JPK/KSeF compliance
    /// </summary>
    public async Task ConfigureAsync(PolandFiscalConfiguration config)
    {
        _state.State.OrgId = OrgId;
        _state.State.SiteId = SiteId;
        _state.State.Configuration = config;

        // Set initial period
        var now = _timeProvider.GetUtcNow().DateTime;
        _state.State.CurrentPeriodStart = new DateOnly(now.Year, now.Month, 1);
        _state.State.CurrentPeriodEnd = _state.State.CurrentPeriodStart.AddMonths(1).AddDays(-1);

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// Generate JPK document for specified type and date range
    /// </summary>
    public async Task<byte[]> GenerateJpkDocumentAsync(
        JpkDocumentType documentType,
        FiscalDateRange range,
        CancellationToken ct = default)
    {
        return documentType switch
        {
            JpkDocumentType.JPK_VAT => Encoding.UTF8.GetBytes(GenerateJpkVatXml(range)),
            JpkDocumentType.JPK_FA => Encoding.UTF8.GetBytes(GenerateJpkFaXml(range)),
            _ => throw new NotSupportedException($"Document type {documentType} is not supported")
        };
    }

    /// <summary>
    /// Verify an invoice in KSeF
    /// </summary>
    public async Task<KsefVerificationResult> VerifyKsefInvoiceAsync(
        string ksefNumber,
        CancellationToken ct = default)
    {
        if (_httpClient == null || _state.State.Configuration == null)
        {
            return new KsefVerificationResult(false, null, "Not configured");
        }

        try
        {
            var baseUrl = GetKsefApiBaseUrl();
            var response = await _httpClient.GetAsync(
                $"{baseUrl}/invoices/{ksefNumber}",
                ct);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                return new KsefVerificationResult(true, content, null);
            }

            return new KsefVerificationResult(false, null, $"KSeF returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return new KsefVerificationResult(false, null, ex.Message);
        }
    }

    /// <summary>
    /// Get VAT register entries for a period
    /// </summary>
    public List<PolandVatEntry> GetVatRegister(DateOnly? start = null, DateOnly? end = null)
    {
        var query = _state.State.VatRegister.AsEnumerable();

        if (start.HasValue)
        {
            query = query.Where(e => DateOnly.FromDateTime(e.Timestamp) >= start.Value);
        }

        if (end.HasValue)
        {
            query = query.Where(e => DateOnly.FromDateTime(e.Timestamp) <= end.Value);
        }

        return query.ToList();
    }

    // ========================================================================
    // Private helper methods
    // ========================================================================

    private string GenerateDocumentNumber(string transactionType)
    {
        var prefix = transactionType switch
        {
            "Invoice" => "FV",
            "Receipt" => "PAR",
            "CorrectionInvoice" => "FK",
            _ => "DOC"
        };

        if (transactionType == "Invoice" || transactionType == "CorrectionInvoice")
        {
            _state.State.InvoiceSequence++;
            return $"{prefix}/{_state.State.InvoiceSequence}/{_timeProvider.GetUtcNow().DateTime:yyyy}";
        }
        else
        {
            _state.State.FiscalReceiptSequence++;
            return $"{prefix}/{_state.State.FiscalReceiptSequence}/{_timeProvider.GetUtcNow().DateTime:yyyy}";
        }
    }

    private static PolandVatRate DetermineVatRate(FiscalTransactionData transaction)
    {
        // Determine primary VAT rate from transaction
        var primaryRate = transaction.TaxAmounts.Keys.FirstOrDefault();

        return primaryRate?.ToUpperInvariant() switch
        {
            "23" or "23%" or "STANDARD" => PolandVatRate.Rate23,
            "8" or "8%" or "REDUCED" => PolandVatRate.Rate8,
            "5" or "5%" or "REDUCED2" => PolandVatRate.Rate5,
            "0" or "0%" or "ZERO" => PolandVatRate.Rate0,
            "ZW" or "EXEMPT" => PolandVatRate.Exempt,
            _ => PolandVatRate.Rate23
        };
    }

    private static bool IsInvoice(string transactionType)
    {
        return transactionType is "Invoice" or "CorrectionInvoice";
    }

    private static bool ValidateNip(string nip)
    {
        // Polish NIP validation (10-digit number with checksum)
        var digits = nip.Replace("-", "").Replace(" ", "");
        if (digits.Length != 10 || !digits.All(char.IsDigit))
            return false;

        var weights = new[] { 6, 5, 7, 2, 3, 4, 5, 6, 7 };
        var sum = digits.Take(9).Select((c, i) => (c - '0') * weights[i]).Sum();
        var checkDigit = sum % 11;

        return checkDigit == (digits[9] - '0');
    }

    private string GenerateJpkVatXml(FiscalDateRange range)
    {
        var config = _state.State.Configuration!;
        var entries = _state.State.VatRegister
            .Where(e => e.Timestamp >= range.StartDate && e.Timestamp <= range.EndDate)
            .ToList();

        var ns = XNamespace.Get("http://crd.gov.pl/wzor/2022/02/17/11148/");
        var nsTns = XNamespace.Get("http://crd.gov.pl/xml/schematy/dziedzinowe/mf/2022/01/05/eD/DefinicjeTypy/");

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "JPK",
                new XAttribute(XNamespace.Xmlns + "tns", nsTns),
                new XElement(ns + "Naglowek",
                    new XElement(ns + "KodFormularza",
                        new XAttribute("kodSystemowy", "JPK_VAT (3)"),
                        new XAttribute("wersjaSchemy", "1-0"),
                        "JPK_VAT"),
                    new XElement(ns + "WariantFormularza", "3"),
                    new XElement(ns + "CelZlozenia", "1"),
                    new XElement(ns + "DataWytworzeniaJPK", _timeProvider.GetUtcNow().DateTime.ToString("yyyy-MM-ddTHH:mm:ss")),
                    new XElement(ns + "DataOd", range.StartDate.ToString("yyyy-MM-dd")),
                    new XElement(ns + "DataDo", range.EndDate.ToString("yyyy-MM-dd")),
                    new XElement(ns + "NazwaSystemu", "DarkVelocity POS")),
                new XElement(ns + "Podmiot1",
                    new XElement(ns + "NIP", config.Nip),
                    new XElement(ns + "PelnaNazwa", config.CompanyName)),
                entries.Select(e => new XElement(ns + "SprzedazWiersz",
                    new XElement(ns + "LpSprzedazy", entries.IndexOf(e) + 1),
                    new XElement(ns + "NrKontrahenta", e.CounterpartyNip ?? "BRAK"),
                    new XElement(ns + "NazwaKontrahenta", e.CounterpartyName ?? "BRAK"),
                    new XElement(ns + "DowodSprzedazy", e.DocumentNumber),
                    new XElement(ns + "DataWystawienia", e.Timestamp.ToString("yyyy-MM-dd")),
                    new XElement(ns + "K_19", e.NetAmount.ToString("F2")),
                    new XElement(ns + "K_20", e.VatAmount.ToString("F2")))),
                new XElement(ns + "SprzedazCtrl",
                    new XElement(ns + "LiczbaWierszySprzedazy", entries.Count),
                    new XElement(ns + "PodatekNalezny", entries.Sum(e => e.VatAmount).ToString("F2")))));

        return doc.ToString();
    }

    private string GenerateJpkFaXml(FiscalDateRange range)
    {
        var config = _state.State.Configuration!;
        var invoices = _state.State.PendingKsefInvoices
            .Where(i => i.IssueDate >= range.StartDate && i.IssueDate <= range.EndDate)
            .ToList();

        // Simplified JPK_FA structure
        var ns = XNamespace.Get("http://jpk.mf.gov.pl/wzor/2019/09/27/09271/");

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "JPK",
                new XElement(ns + "Naglowek",
                    new XElement(ns + "KodFormularza", "JPK_FA"),
                    new XElement(ns + "WariantFormularza", "4"),
                    new XElement(ns + "DataWytworzeniaJPK", _timeProvider.GetUtcNow().DateTime.ToString("yyyy-MM-ddTHH:mm:ss")),
                    new XElement(ns + "DataOd", range.StartDate.ToString("yyyy-MM-dd")),
                    new XElement(ns + "DataDo", range.EndDate.ToString("yyyy-MM-dd"))),
                new XElement(ns + "Podmiot1",
                    new XElement(ns + "IdentyfikatorPodmiotu",
                        new XElement(ns + "NIP", config.Nip),
                        new XElement(ns + "PelnaNazwa", config.CompanyName))),
                invoices.Select((inv, idx) => new XElement(ns + "Faktura",
                    new XElement(ns + "P_1", inv.IssueDate.ToString("yyyy-MM-dd")),
                    new XElement(ns + "P_2A", inv.InvoiceNumber),
                    new XElement(ns + "P_15", inv.GrossAmount.ToString("F2")),
                    new XElement(ns + "KodWaluty", "PLN"),
                    new XElement(ns + "RodzajFaktury", "VAT")))));

        return doc.ToString();
    }

    private string GenerateKsefInvoiceXml(FiscalTransactionData transaction, string invoiceNumber)
    {
        var config = _state.State.Configuration!;

        // Generate FA(2) schema compliant XML for KSeF
        var ns = XNamespace.Get("http://crd.gov.pl/wzor/2023/06/29/12648/");

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "Faktura",
                new XElement(ns + "Naglowek",
                    new XElement(ns + "KodFormularza", "FA"),
                    new XElement(ns + "WariantFormularza", "2"),
                    new XElement(ns + "DataWytworzeniaFa", transaction.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss")),
                    new XElement(ns + "SystemInfo", "DarkVelocity POS")),
                new XElement(ns + "Podmiot1",
                    new XElement(ns + "DaneIdentyfikacyjne",
                        new XElement(ns + "NIP", config.Nip)),
                    new XElement(ns + "Adres",
                        new XElement(ns + "KodKraju", "PL"),
                        new XElement(ns + "AdresL1", config.CompanyAddress))),
                new XElement(ns + "Fa",
                    new XElement(ns + "KodWaluty", "PLN"),
                    new XElement(ns + "P_1", transaction.Timestamp.ToString("yyyy-MM-dd")),
                    new XElement(ns + "P_2", invoiceNumber),
                    new XElement(ns + "P_15", transaction.GrossAmount.ToString("F2")),
                    new XElement(ns + "Adnotacje",
                        new XElement(ns + "P_16", "2"),
                        new XElement(ns + "P_17", "2"),
                        new XElement(ns + "P_18", "2"),
                        new XElement(ns + "P_18A", "2")))));

        return doc.ToString();
    }

    private async Task<bool> EnsureKsefSessionAsync(CancellationToken ct)
    {
        if (_httpClient == null || _state.State.Configuration == null)
            return false;

        // Check if existing session is valid
        if (_state.State.KsefSessionToken != null &&
            _state.State.KsefSessionExpiresAt > _timeProvider.GetUtcNow().DateTime.AddMinutes(5))
        {
            return true;
        }

        try
        {
            var baseUrl = GetKsefApiBaseUrl();
            var response = await _httpClient.PostAsJsonAsync(
                $"{baseUrl}/auth/token",
                new { apiToken = _state.State.Configuration.KsefApiToken },
                ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<KsefAuthResponse>(ct);
                if (result != null)
                {
                    _state.State.KsefSessionToken = result.SessionToken;
                    _state.State.KsefSessionExpiresAt = _timeProvider.GetUtcNow().DateTime.AddHours(1);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to authenticate with KSeF");
        }

        return false;
    }

    private async Task<FiscalResult> SubmitToKsefAsync(KsefInvoiceRecord invoice, CancellationToken ct)
    {
        if (_httpClient == null || _state.State.KsefSessionToken == null)
        {
            return FailedResult("NO_SESSION", "No active KSeF session");
        }

        try
        {
            var baseUrl = GetKsefApiBaseUrl();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/invoices")
            {
                Content = new StringContent(invoice.InvoiceXml, Encoding.UTF8, "application/xml")
            };
            request.Headers.Add("Authorization", $"Bearer {_state.State.KsefSessionToken}");

            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<KsefSubmitResponse>(ct);

                // Update invoice record
                var idx = _state.State.PendingKsefInvoices.FindIndex(i => i.InvoiceId == invoice.InvoiceId);
                if (idx >= 0)
                {
                    _state.State.PendingKsefInvoices[idx] = invoice with
                    {
                        Status = KsefSubmissionStatus.Accepted,
                        KsefNumber = result?.KsefNumber,
                        SubmittedAt = _timeProvider.GetUtcNow().DateTime,
                        AcceptedAt = _timeProvider.GetUtcNow().DateTime
                    };

                    if (result?.KsefNumber != null)
                    {
                        _state.State.KsefReferenceMap[invoice.InvoiceNumber] = result.KsefNumber;
                    }
                }

                return SuccessResult(transactionId: result?.KsefNumber);
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            return FailedResult("KSEF_ERROR", error);
        }
        catch (Exception ex)
        {
            return FailedResult("SUBMIT_FAILED", ex.Message);
        }
    }

    private string GetKsefApiBaseUrl()
    {
        return _state.State.Configuration?.Environment == PolandFiscalEnvironment.Production
            ? "https://ksef.mf.gov.pl/api"
            : "https://ksef-test.mf.gov.pl/api";
    }
}

// ============================================================================
// Poland KSeF DTOs
// ============================================================================

internal sealed record KsefAuthResponse(string SessionToken);

internal sealed record KsefSubmitResponse(string KsefNumber, string Status);

/// <summary>
/// Result of KSeF invoice verification
/// </summary>
[GenerateSerializer]
public sealed record KsefVerificationResult(
    [property: Id(0)] bool Found,
    [property: Id(1)] string? InvoiceData,
    [property: Id(2)] string? ErrorMessage);

// ============================================================================
// Poland Fiscal Events
// ============================================================================

/// <summary>
/// Event when a Polish VAT register entry is recorded
/// </summary>
public sealed record PolishVatEntryRecorded(
    Guid TenantId,
    Guid SiteId,
    string DocumentNumber,
    string DocumentType,
    decimal GrossAmount,
    decimal VatAmount,
    string VatRate,
    DateTime RecordedAt
) : IntegrationEvent
{
    public override string EventType => "fiscal.poland.vat_entry_recorded";
}

/// <summary>
/// Event when a Polish invoice is submitted to KSeF
/// </summary>
public sealed record PolishKsefInvoiceSubmitted(
    Guid TenantId,
    Guid SiteId,
    string InvoiceNumber,
    string KsefNumber,
    decimal GrossAmount,
    DateTime SubmittedAt
) : IntegrationEvent
{
    public override string EventType => "fiscal.poland.ksef_invoice_submitted";
}

/// <summary>
/// Event when JPK document is generated
/// </summary>
public sealed record PolishJpkDocumentGenerated(
    Guid TenantId,
    Guid SiteId,
    string DocumentType,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    int RecordCount,
    long FileSizeBytes,
    DateTime GeneratedAt
) : IntegrationEvent
{
    public override string EventType => "fiscal.poland.jpk_document_generated";
}
