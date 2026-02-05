using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DarkVelocity.Host.Events;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// France NF 525 Fiscal Adapter
// Implements French fiscal compliance requirements including:
// - Cumulative totals (Grand Total Perpetuel)
// - Sequential numbering
// - JET (Journal Electronique Technique) format
// - Daily archive generation
// - Inalterability of data
// ============================================================================

/// <summary>
/// France-specific configuration for NF 525 compliance
/// </summary>
[GenerateSerializer]
public sealed record FranceFiscalConfiguration(
    [property: Id(0)] bool Enabled,
    [property: Id(1)] string SoftwareVersion,
    [property: Id(2)] string SoftwareName,
    [property: Id(3)] string CertificationNumber,
    [property: Id(4)] string SirenNumber,
    [property: Id(5)] string NicNumber,
    [property: Id(6)] string CompanyName,
    [property: Id(7)] string CompanyAddress,
    [property: Id(8)] string? SigningKeyEncrypted,
    [property: Id(9)] bool AutoArchive,
    [property: Id(10)] TimeOnly DailyArchiveTime);

/// <summary>
/// French cumulative totals state - must be immutable and auditable
/// </summary>
[GenerateSerializer]
public sealed class FrenchCumulativeTotals
{
    [Id(0)] public decimal GrandTotalPerpetuel { get; set; }
    [Id(1)] public decimal GrandTotalPerpetuelTTC { get; set; }
    [Id(2)] public long SequenceNumber { get; set; }
    [Id(3)] public Dictionary<string, decimal> TotalsByVatRate { get; set; } = [];
    [Id(4)] public Dictionary<string, decimal> TotalsByPaymentType { get; set; } = [];
    [Id(5)] public int TransactionCount { get; set; }
    [Id(6)] public int VoidCount { get; set; }
    [Id(7)] public decimal VoidTotal { get; set; }
    [Id(8)] public DateTime? LastTransactionAt { get; set; }
    [Id(9)] public string? LastSignature { get; set; }
}

/// <summary>
/// JET entry for French electronic journal
/// </summary>
[GenerateSerializer]
public sealed record JetEntry(
    [property: Id(0)] long SequenceNumber,
    [property: Id(1)] DateTime Timestamp,
    [property: Id(2)] string EventType,
    [property: Id(3)] string EventData,
    [property: Id(4)] string Signature,
    [property: Id(5)] string PreviousSignature,
    [property: Id(6)] decimal? Amount,
    [property: Id(7)] string? OperatorId,
    [property: Id(8)] string Hash);

/// <summary>
/// State for French fiscal grain
/// </summary>
[GenerateSerializer]
public sealed class FrenchFiscalState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public FranceFiscalConfiguration? Configuration { get; set; }
    [Id(3)] public FrenchCumulativeTotals DailyTotals { get; set; } = new();
    [Id(4)] public FrenchCumulativeTotals PeriodTotals { get; set; } = new();
    [Id(5)] public DateOnly CurrentBusinessDate { get; set; }
    [Id(6)] public List<JetEntry> TodayJournal { get; set; } = [];
    [Id(7)] public byte[] SigningKey { get; set; } = [];
    [Id(8)] public string CertificateSerial { get; set; } = string.Empty;
    [Id(9)] public DateTime? LastArchiveAt { get; set; }
    [Id(10)] public int Version { get; set; }
}

/// <summary>
/// French NF 525 fiscal adapter implementation
/// </summary>
public class FranceFiscalAdapter : BaseFiscalCountryAdapter
{
    private readonly IPersistentState<FrenchFiscalState> _state;
    private readonly TimeProvider _timeProvider;

    public FranceFiscalAdapter(
        Guid orgId,
        Guid siteId,
        IGrainFactory grainFactory,
        IPersistentState<FrenchFiscalState> state,
        ILogger<FranceFiscalAdapter> logger,
        TimeProvider? timeProvider = null)
        : base(orgId, siteId, grainFactory, logger)
    {
        _state = state;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public override FiscalCountry Country => FiscalCountry.France;
    public override string ComplianceStandard => "NF 525";

    public override async Task<FiscalResult> RecordTransactionAsync(
        FiscalTransactionData transaction, CancellationToken ct = default)
    {
        if (_state.State.Configuration == null || !_state.State.Configuration.Enabled)
        {
            return FailedResult("NOT_CONFIGURED", "French fiscal compliance not configured");
        }

        try
        {
            // Check if business day has changed
            var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
            if (today != _state.State.CurrentBusinessDate)
            {
                await PerformDayRolloverAsync(today);
            }

            // Increment sequence number
            _state.State.DailyTotals.SequenceNumber++;
            _state.State.PeriodTotals.SequenceNumber++;
            var sequenceNumber = _state.State.PeriodTotals.SequenceNumber;

            // Update cumulative totals
            UpdateCumulativeTotals(transaction);

            // Generate signature chain
            var previousSignature = _state.State.DailyTotals.LastSignature ?? "INITIAL";
            var signature = GenerateSignature(transaction, sequenceNumber, previousSignature);
            _state.State.DailyTotals.LastSignature = signature;

            // Create JET entry
            var jetEntry = CreateJetEntry(transaction, sequenceNumber, signature, previousSignature);
            _state.State.TodayJournal.Add(jetEntry);

            _state.State.DailyTotals.LastTransactionAt = _timeProvider.GetUtcNow().DateTime;
            _state.State.DailyTotals.TransactionCount++;
            _state.State.Version++;

            await _state.WriteStateAsync();

            Logger.LogInformation(
                "Recorded French fiscal transaction {SequenceNumber} for {Amount:C}",
                sequenceNumber, transaction.GrossAmount);

            return SuccessResult(
                transactionId: sequenceNumber.ToString(),
                signature: signature,
                signatureCounter: sequenceNumber,
                certificateSerial: _state.State.CertificateSerial,
                qrCodeData: GenerateFrenchQrCode(transaction, sequenceNumber, signature),
                metadata: new Dictionary<string, string>
                {
                    ["grand_total_perpetuel"] = _state.State.PeriodTotals.GrandTotalPerpetuel.ToString("F2"),
                    ["daily_total"] = _state.State.DailyTotals.GrandTotalPerpetuel.ToString("F2"),
                    ["transaction_count"] = _state.State.DailyTotals.TransactionCount.ToString()
                });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to record French fiscal transaction");
            return FailedResult("RECORD_FAILED", ex.Message);
        }
    }

    public override async Task<byte[]> GenerateAuditExportAsync(
        FiscalDateRange range, CancellationToken ct = default)
    {
        if (_state.State.Configuration == null)
        {
            throw new InvalidOperationException("French fiscal compliance not configured");
        }

        // Generate JET (Journal Electronique Technique) archive
        var archive = new FrenchJetArchive
        {
            Header = new FrenchJetHeader
            {
                SoftwareName = _state.State.Configuration.SoftwareName,
                SoftwareVersion = _state.State.Configuration.SoftwareVersion,
                CertificationNumber = _state.State.Configuration.CertificationNumber,
                SirenNumber = _state.State.Configuration.SirenNumber,
                NicNumber = _state.State.Configuration.NicNumber,
                CompanyName = _state.State.Configuration.CompanyName,
                CompanyAddress = _state.State.Configuration.CompanyAddress,
                ExportStartDate = range.StartDate,
                ExportEndDate = range.EndDate,
                GeneratedAt = _timeProvider.GetUtcNow().DateTime
            },
            Entries = _state.State.TodayJournal
                .Where(e => e.Timestamp >= range.StartDate && e.Timestamp <= range.EndDate)
                .ToList(),
            Footer = new FrenchJetFooter
            {
                TotalTransactions = _state.State.DailyTotals.TransactionCount,
                GrandTotalPerpetuel = _state.State.PeriodTotals.GrandTotalPerpetuel,
                FirstSequenceNumber = _state.State.TodayJournal.FirstOrDefault()?.SequenceNumber ?? 0,
                LastSequenceNumber = _state.State.TodayJournal.LastOrDefault()?.SequenceNumber ?? 0
            }
        };

        // Serialize to JSON (JET format is typically JSON or XML)
        var json = JsonSerializer.Serialize(archive, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return Encoding.UTF8.GetBytes(json);
    }

    public override async Task<FiscalConfigValidationResult> ValidateConfigurationAsync(
        CancellationToken ct = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (_state.State.Configuration == null)
        {
            errors.Add("French fiscal configuration is not set");
            return new FiscalConfigValidationResult(false, errors, warnings, null);
        }

        var config = _state.State.Configuration;

        if (string.IsNullOrEmpty(config.CertificationNumber))
            errors.Add("NF 525 certification number is required");

        if (string.IsNullOrEmpty(config.SirenNumber))
            errors.Add("SIREN number is required");

        if (string.IsNullOrEmpty(config.SoftwareName))
            errors.Add("Software name is required");

        if (string.IsNullOrEmpty(config.SoftwareVersion))
            errors.Add("Software version is required");

        if (_state.State.SigningKey.Length == 0)
            errors.Add("Signing key is not configured");

        if (!config.AutoArchive)
            warnings.Add("Automatic daily archive is disabled - manual archive generation required");

        return new FiscalConfigValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors,
            Warnings: warnings,
            Metadata: new Dictionary<string, string>
            {
                ["certification_number"] = config.CertificationNumber ?? "",
                ["siren"] = config.SirenNumber ?? "",
                ["compliance_standard"] = "NF 525"
            });
    }

    public override Task<FiscalDeviceHealthStatus> GetHealthStatusAsync(
        CancellationToken ct = default)
    {
        var status = new FiscalDeviceHealthStatus(
            DeviceId: Guid.Empty, // Software-based, no device ID
            Status: _state.State.Configuration?.Enabled == true
                ? FiscalDeviceStatus.Active
                : FiscalDeviceStatus.Inactive,
            IsOnline: true,
            CertificateValid: _state.State.SigningKey.Length > 0,
            DaysUntilCertificateExpiry: null,
            LastSyncAt: _state.State.LastArchiveAt,
            LastTransactionAt: _state.State.DailyTotals.LastTransactionAt,
            TotalTransactions: _state.State.PeriodTotals.TransactionCount,
            LastError: null);

        return Task.FromResult(status);
    }

    public override async Task<FiscalResult> PerformDailyCloseAsync(
        DateTime businessDate, CancellationToken ct = default)
    {
        try
        {
            // Generate daily Z-report equivalent
            var archiveData = await GenerateDailyArchiveAsync(businessDate, ct);

            // Reset daily totals
            _state.State.DailyTotals = new FrenchCumulativeTotals
            {
                SequenceNumber = 0
            };
            _state.State.TodayJournal.Clear();
            _state.State.LastArchiveAt = _timeProvider.GetUtcNow().DateTime;
            _state.State.Version++;

            await _state.WriteStateAsync();

            Logger.LogInformation(
                "French daily close completed for {BusinessDate}",
                businessDate.ToString("yyyy-MM-dd"));

            return SuccessResult(
                metadata: new Dictionary<string, string>
                {
                    ["business_date"] = businessDate.ToString("yyyy-MM-dd"),
                    ["archive_size"] = archiveData.Length.ToString(),
                    ["transactions_archived"] = _state.State.DailyTotals.TransactionCount.ToString()
                });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to perform French daily close");
            return FailedResult("DAILY_CLOSE_FAILED", ex.Message);
        }
    }

    public override bool SupportsFeature(FiscalFeature feature)
    {
        return feature switch
        {
            FiscalFeature.CumulativeTotals => true,
            FiscalFeature.ElectronicJournal => true,
            FiscalFeature.CertificateSigning => true,
            FiscalFeature.QrCodeGeneration => true,
            FiscalFeature.RealTimeSigning => true,
            _ => false
        };
    }

    // ========================================================================
    // French-specific public methods
    // ========================================================================

    /// <summary>
    /// Configure French NF 525 compliance
    /// </summary>
    public async Task ConfigureAsync(FranceFiscalConfiguration config)
    {
        _state.State.OrgId = OrgId;
        _state.State.SiteId = SiteId;
        _state.State.Configuration = config;
        _state.State.CurrentBusinessDate = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);

        // Generate or restore signing key
        if (!string.IsNullOrEmpty(config.SigningKeyEncrypted))
        {
            // Decrypt existing key
            _state.State.SigningKey = Convert.FromBase64String(config.SigningKeyEncrypted);
        }
        else
        {
            // Generate new signing key
            using var rng = RandomNumberGenerator.Create();
            _state.State.SigningKey = new byte[32];
            rng.GetBytes(_state.State.SigningKey);
        }

        _state.State.CertificateSerial = $"FR-NF525-{OrgId:N}".Substring(0, 24).ToUpper();
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    /// <summary>
    /// Get current cumulative totals (Grand Total Perpetuel)
    /// </summary>
    public FrenchCumulativeTotals GetCumulativeTotals()
    {
        return _state.State.PeriodTotals;
    }

    /// <summary>
    /// Get today's totals
    /// </summary>
    public FrenchCumulativeTotals GetDailyTotals()
    {
        return _state.State.DailyTotals;
    }

    // ========================================================================
    // Private helper methods
    // ========================================================================

    private void UpdateCumulativeTotals(FiscalTransactionData transaction)
    {
        var isVoid = transaction.TransactionType == "Void" || transaction.TransactionType == "Cancellation";

        // Update daily totals
        _state.State.DailyTotals.GrandTotalPerpetuel += transaction.GrossAmount;
        _state.State.DailyTotals.GrandTotalPerpetuelTTC += transaction.GrossAmount;

        // Update period totals
        _state.State.PeriodTotals.GrandTotalPerpetuel += transaction.GrossAmount;
        _state.State.PeriodTotals.GrandTotalPerpetuelTTC += transaction.GrossAmount;
        _state.State.PeriodTotals.TransactionCount++;

        if (isVoid)
        {
            _state.State.DailyTotals.VoidCount++;
            _state.State.DailyTotals.VoidTotal += Math.Abs(transaction.GrossAmount);
            _state.State.PeriodTotals.VoidCount++;
            _state.State.PeriodTotals.VoidTotal += Math.Abs(transaction.GrossAmount);
        }

        // Update VAT totals
        foreach (var (rate, amount) in transaction.TaxAmounts)
        {
            if (_state.State.DailyTotals.TotalsByVatRate.ContainsKey(rate))
                _state.State.DailyTotals.TotalsByVatRate[rate] += amount;
            else
                _state.State.DailyTotals.TotalsByVatRate[rate] = amount;

            if (_state.State.PeriodTotals.TotalsByVatRate.ContainsKey(rate))
                _state.State.PeriodTotals.TotalsByVatRate[rate] += amount;
            else
                _state.State.PeriodTotals.TotalsByVatRate[rate] = amount;
        }

        // Update payment type totals
        foreach (var (type, amount) in transaction.PaymentTypes)
        {
            if (_state.State.DailyTotals.TotalsByPaymentType.ContainsKey(type))
                _state.State.DailyTotals.TotalsByPaymentType[type] += amount;
            else
                _state.State.DailyTotals.TotalsByPaymentType[type] = amount;

            if (_state.State.PeriodTotals.TotalsByPaymentType.ContainsKey(type))
                _state.State.PeriodTotals.TotalsByPaymentType[type] += amount;
            else
                _state.State.PeriodTotals.TotalsByPaymentType[type] = amount;
        }
    }

    private string GenerateSignature(
        FiscalTransactionData transaction,
        long sequenceNumber,
        string previousSignature)
    {
        // Build data to sign (NF 525 chained signature)
        var dataToSign = string.Join("|",
            sequenceNumber.ToString(),
            transaction.Timestamp.ToString("O"),
            transaction.GrossAmount.ToString("F2"),
            _state.State.PeriodTotals.GrandTotalPerpetuel.ToString("F2"),
            previousSignature);

        using var hmac = new HMACSHA256(_state.State.SigningKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
        return Convert.ToBase64String(hash);
    }

    private JetEntry CreateJetEntry(
        FiscalTransactionData transaction,
        long sequenceNumber,
        string signature,
        string previousSignature)
    {
        var eventData = JsonSerializer.Serialize(new
        {
            transaction.GrossAmount,
            transaction.TaxAmounts,
            transaction.PaymentTypes,
            transaction.SourceType,
            transaction.SourceId,
            GrandTotalPerpetuel = _state.State.PeriodTotals.GrandTotalPerpetuel
        });

        // Calculate hash for integrity
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(eventData + signature));
        var hash = Convert.ToBase64String(hashBytes);

        return new JetEntry(
            SequenceNumber: sequenceNumber,
            Timestamp: transaction.Timestamp,
            EventType: transaction.TransactionType,
            EventData: eventData,
            Signature: signature,
            PreviousSignature: previousSignature,
            Amount: transaction.GrossAmount,
            OperatorId: transaction.OperatorId,
            Hash: hash);
    }

    private string GenerateFrenchQrCode(
        FiscalTransactionData transaction,
        long sequenceNumber,
        string signature)
    {
        // French QR code format for receipts
        return string.Join(";",
            "FR",
            _state.State.Configuration?.SirenNumber ?? "",
            _state.State.CertificateSerial,
            sequenceNumber.ToString(),
            transaction.Timestamp.ToString("yyyyMMddHHmmss"),
            transaction.GrossAmount.ToString("F2"),
            _state.State.PeriodTotals.GrandTotalPerpetuel.ToString("F2"),
            signature[..Math.Min(16, signature.Length)]);
    }

    private async Task PerformDayRolloverAsync(DateOnly newDate)
    {
        Logger.LogInformation(
            "French fiscal day rollover from {OldDate} to {NewDate}",
            _state.State.CurrentBusinessDate, newDate);

        // Archive previous day if needed
        if (_state.State.Configuration?.AutoArchive == true && _state.State.TodayJournal.Count > 0)
        {
            await GenerateDailyArchiveAsync(
                _state.State.CurrentBusinessDate.ToDateTime(TimeOnly.MinValue),
                CancellationToken.None);
        }

        // Reset daily totals but keep period totals
        _state.State.DailyTotals = new FrenchCumulativeTotals
        {
            SequenceNumber = 0
        };
        _state.State.TodayJournal.Clear();
        _state.State.CurrentBusinessDate = newDate;
    }

    private async Task<byte[]> GenerateDailyArchiveAsync(DateTime businessDate, CancellationToken ct)
    {
        var range = new FiscalDateRange(
            businessDate.Date,
            businessDate.Date.AddDays(1).AddTicks(-1));

        return await GenerateAuditExportAsync(range, ct);
    }
}

// ============================================================================
// French JET Archive Format
// ============================================================================

/// <summary>
/// French JET (Journal Electronique Technique) archive structure
/// </summary>
public sealed class FrenchJetArchive
{
    public FrenchJetHeader Header { get; set; } = new();
    public List<JetEntry> Entries { get; set; } = [];
    public FrenchJetFooter Footer { get; set; } = new();
}

/// <summary>
/// JET archive header
/// </summary>
public sealed class FrenchJetHeader
{
    public string SoftwareName { get; set; } = string.Empty;
    public string SoftwareVersion { get; set; } = string.Empty;
    public string CertificationNumber { get; set; } = string.Empty;
    public string SirenNumber { get; set; } = string.Empty;
    public string NicNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyAddress { get; set; } = string.Empty;
    public DateTime ExportStartDate { get; set; }
    public DateTime ExportEndDate { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// JET archive footer
/// </summary>
public sealed class FrenchJetFooter
{
    public int TotalTransactions { get; set; }
    public decimal GrandTotalPerpetuel { get; set; }
    public long FirstSequenceNumber { get; set; }
    public long LastSequenceNumber { get; set; }
}

// ============================================================================
// French Fiscal Events
// ============================================================================

/// <summary>
/// Event when a French JET entry is recorded
/// </summary>
public sealed record FrenchJournalEntryRecorded(
    Guid TenantId,
    Guid SiteId,
    long SequenceNumber,
    string TransactionType,
    decimal Amount,
    decimal GrandTotalPerpetuel,
    string Signature,
    DateTime RecordedAt
) : IntegrationEvent
{
    public override string EventType => "fiscal.france.journal_entry_recorded";
}

/// <summary>
/// Event when a French daily archive is generated
/// </summary>
public sealed record FrenchDailyArchiveGenerated(
    Guid TenantId,
    Guid SiteId,
    DateOnly BusinessDate,
    int TransactionCount,
    decimal DailyTotal,
    decimal GrandTotalPerpetuel,
    long ArchiveSizeBytes,
    DateTime GeneratedAt
) : IntegrationEvent
{
    public override string EventType => "fiscal.france.daily_archive_generated";
}
