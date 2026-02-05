using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.Runtime;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace DarkVelocity.Host.Services;

/// <summary>
/// Interface for DSFinV-K export service.
/// Generates German tax audit data exports per DSFinV-K specification.
/// </summary>
public interface IDSFinVKExportService
{
    /// <summary>
    /// Generate a DSFinV-K export for a date range.
    /// </summary>
    Task<DSFinVKExportResponse> GenerateExportAsync(
        Guid orgId,
        Guid siteId,
        Guid exportId,
        DateOnly startDate,
        DateOnly endDate,
        string? description,
        List<Guid>? deviceIds,
        Guid? requestedBy);

    /// <summary>
    /// Get status of an export.
    /// </summary>
    Task<DSFinVKExportResponse?> GetExportStatusAsync(Guid orgId, Guid siteId, Guid exportId);

    /// <summary>
    /// Download a completed export.
    /// </summary>
    Task<(Stream? Stream, string FileName)> DownloadExportAsync(Guid orgId, Guid siteId, Guid exportId);

    /// <summary>
    /// List all exports for a site.
    /// </summary>
    Task<List<DSFinVKExportResponse>> ListExportsAsync(Guid orgId, Guid siteId);
}

/// <summary>
/// DSFinV-K export state.
/// </summary>
[GenerateSerializer]
public sealed class DSFinVKExportState
{
    [Id(0)] public Guid ExportId { get; set; }
    [Id(1)] public Guid OrgId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public DateOnly StartDate { get; set; }
    [Id(4)] public DateOnly EndDate { get; set; }
    [Id(5)] public string? Description { get; set; }
    [Id(6)] public DSFinVKExportStatus Status { get; set; }
    [Id(7)] public int TransactionCount { get; set; }
    [Id(8)] public string? FilePath { get; set; }
    [Id(9)] public string? DownloadUrl { get; set; }
    [Id(10)] public DateTime CreatedAt { get; set; }
    [Id(11)] public DateTime? CompletedAt { get; set; }
    [Id(12)] public string? ErrorMessage { get; set; }
    [Id(13)] public Guid? RequestedBy { get; set; }
    [Id(14)] public int Version { get; set; }
}

public enum DSFinVKExportStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

/// <summary>
/// DSFinV-K export grain interface.
/// </summary>
public interface IDSFinVKExportGrain : IGrainWithStringKey
{
    Task<DSFinVKExportState> CreateAsync(
        DateOnly startDate,
        DateOnly endDate,
        string? description,
        Guid? requestedBy);

    Task<DSFinVKExportState> GetStateAsync();

    Task SetProcessingAsync();
    Task SetCompletedAsync(int transactionCount, string filePath, string downloadUrl);
    Task SetFailedAsync(string errorMessage);
}

/// <summary>
/// DSFinV-K export grain implementation.
/// </summary>
public class DSFinVKExportGrain : Grain, IDSFinVKExportGrain
{
    private readonly IPersistentState<DSFinVKExportState> _state;

    public DSFinVKExportGrain(
        [PersistentState("dsfinvkExport", "OrleansStorage")]
        IPersistentState<DSFinVKExportState> state)
    {
        _state = state;
    }

    public async Task<DSFinVKExportState> CreateAsync(
        DateOnly startDate,
        DateOnly endDate,
        string? description,
        Guid? requestedBy)
    {
        if (_state.State.ExportId != Guid.Empty)
            throw new InvalidOperationException("Export already created");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');

        _state.State = new DSFinVKExportState
        {
            ExportId = Guid.Parse(parts[3]),
            OrgId = Guid.Parse(parts[0]),
            SiteId = Guid.Parse(parts[1]),
            StartDate = startDate,
            EndDate = endDate,
            Description = description,
            Status = DSFinVKExportStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            RequestedBy = requestedBy,
            Version = 1
        };

        await _state.WriteStateAsync();
        return _state.State;
    }

    public Task<DSFinVKExportState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task SetProcessingAsync()
    {
        _state.State.Status = DSFinVKExportStatus.Processing;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetCompletedAsync(int transactionCount, string filePath, string downloadUrl)
    {
        _state.State.Status = DSFinVKExportStatus.Completed;
        _state.State.TransactionCount = transactionCount;
        _state.State.FilePath = filePath;
        _state.State.DownloadUrl = downloadUrl;
        _state.State.CompletedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetFailedAsync(string errorMessage)
    {
        _state.State.Status = DSFinVKExportStatus.Failed;
        _state.State.ErrorMessage = errorMessage;
        _state.State.CompletedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }
}

/// <summary>
/// DSFinV-K export registry grain interface.
/// </summary>
public interface IDSFinVKExportRegistryGrain : IGrainWithStringKey
{
    Task RegisterExportAsync(Guid exportId);
    Task<IReadOnlyList<Guid>> GetExportIdsAsync();
}

/// <summary>
/// DSFinV-K export registry grain state.
/// </summary>
[GenerateSerializer]
public sealed class DSFinVKExportRegistryState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public List<Guid> ExportIds { get; set; } = [];
    [Id(3)] public int Version { get; set; }
}

/// <summary>
/// DSFinV-K export registry grain implementation.
/// </summary>
public class DSFinVKExportRegistryGrain : Grain, IDSFinVKExportRegistryGrain
{
    private readonly IPersistentState<DSFinVKExportRegistryState> _state;

    public DSFinVKExportRegistryGrain(
        [PersistentState("dsfinvkRegistry", "OrleansStorage")]
        IPersistentState<DSFinVKExportRegistryState> state)
    {
        _state = state;
    }

    public async Task RegisterExportAsync(Guid exportId)
    {
        _state.State.ExportIds.Insert(0, exportId);
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<Guid>> GetExportIdsAsync()
    {
        return Task.FromResult<IReadOnlyList<Guid>>(_state.State.ExportIds);
    }
}

/// <summary>
/// Implementation of DSFinV-K export service.
/// Generates German tax audit data exports per DSFinV-K specification.
/// </summary>
public class DSFinVKExportService : IDSFinVKExportService
{
    private readonly IGrainFactory _grainFactory;
    private readonly string _exportBasePath;
    private readonly ILogger<DSFinVKExportService> _logger;

    public DSFinVKExportService(
        IGrainFactory grainFactory,
        IConfiguration configuration,
        ILogger<DSFinVKExportService> logger)
    {
        _grainFactory = grainFactory;
        _exportBasePath = configuration["DSFinVK:ExportPath"] ?? Path.Combine(Path.GetTempPath(), "dsfinvk-exports");
        _logger = logger;

        // Ensure export directory exists
        Directory.CreateDirectory(_exportBasePath);
    }

    public async Task<DSFinVKExportResponse> GenerateExportAsync(
        Guid orgId,
        Guid siteId,
        Guid exportId,
        DateOnly startDate,
        DateOnly endDate,
        string? description,
        List<Guid>? deviceIds,
        Guid? requestedBy)
    {
        // Create export grain
        var exportGrain = _grainFactory.GetGrain<IDSFinVKExportGrain>(
            GrainKeys.DSFinVKExport(orgId, siteId, exportId));

        var state = await exportGrain.CreateAsync(startDate, endDate, description, requestedBy);

        // Register export
        var registryGrain = _grainFactory.GetGrain<IDSFinVKExportRegistryGrain>(
            GrainKeys.DSFinVKExportRegistry(orgId, siteId));
        await registryGrain.RegisterExportAsync(exportId);

        // Start background processing
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessExportAsync(orgId, siteId, exportId, startDate, endDate, deviceIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process DSFinV-K export {ExportId}", exportId);
                await exportGrain.SetFailedAsync(ex.Message);
            }
        });

        return ToResponse(state);
    }

    private async Task ProcessExportAsync(
        Guid orgId,
        Guid siteId,
        Guid exportId,
        DateOnly startDate,
        DateOnly endDate,
        List<Guid>? deviceIds)
    {
        var exportGrain = _grainFactory.GetGrain<IDSFinVKExportGrain>(
            GrainKeys.DSFinVKExport(orgId, siteId, exportId));

        await exportGrain.SetProcessingAsync();

        // Collect all transaction data
        var transactionRegistryGrain = _grainFactory.GetGrain<IFiscalTransactionRegistryGrain>(
            GrainKeys.FiscalTransactionRegistry(orgId, siteId));

        var transactionIds = await transactionRegistryGrain.GetTransactionIdsAsync(
            startDate, endDate, deviceIds?.FirstOrDefault());

        var transactions = new List<FiscalTransactionSnapshot>();
        foreach (var txId in transactionIds)
        {
            var txGrain = _grainFactory.GetGrain<IFiscalTransactionGrain>(
                GrainKeys.FiscalTransaction(orgId, siteId, txId));
            try
            {
                var snapshot = await txGrain.GetSnapshotAsync();
                transactions.Add(snapshot);
            }
            catch
            {
                // Skip invalid transactions
            }
        }

        // Generate DSFinV-K files
        var exportDir = Path.Combine(_exportBasePath, $"{exportId}");
        Directory.CreateDirectory(exportDir);

        // Generate each required file
        await GenerateTransactionsCsvAsync(exportDir, transactions);
        await GeneratePaymentsCsvAsync(exportDir, transactions);
        await GenerateLinesCsvAsync(exportDir, transactions);
        await GenerateVatCsvAsync(exportDir, transactions);
        await GenerateCashpointClosingCsvAsync(exportDir, orgId, siteId, startDate, endDate, transactions);
        await GenerateLocationCsvAsync(exportDir, orgId, siteId);
        await GenerateIndexXmlAsync(exportDir, orgId, siteId, startDate, endDate, transactions.Count);

        // Create ZIP archive
        var zipPath = Path.Combine(_exportBasePath, $"dsfinvk_{siteId}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.zip");
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        ZipFile.CreateFromDirectory(exportDir, zipPath);

        // Cleanup temp directory
        Directory.Delete(exportDir, true);

        // Update export state
        var downloadUrl = $"/api/orgs/{orgId}/sites/{siteId}/fiscal/dsfinvk-export/{exportId}/download";
        await exportGrain.SetCompletedAsync(transactions.Count, zipPath, downloadUrl);
    }

    private static async Task GenerateTransactionsCsvAsync(string dir, List<FiscalTransactionSnapshot> transactions)
    {
        var sb = new StringBuilder();
        // DSFinV-K transactions.csv header
        sb.AppendLine("Z_KASSE_ID;Z_ERESSION;Z_NR;BON_ID;BON_NR;BON_TYP;BON_NAME;BEDIENER_ID;BEDIENER_NAME;UMS_BRUTTO;STORNIERUNG;TIMESTAMP_START;TIMESTAMP_END;TSE_ID;TSE_SERIAL;TSE_TANR;TSE_SIG_COUNTER;TSE_SIGNATURE;TSE_START;TSE_END;TSE_PROCESS_TYPE;TSE_PROCESS_DATA");

        foreach (var tx in transactions)
        {
            sb.AppendLine(string.Join(";", new[]
            {
                tx.FiscalDeviceId.ToString(),
                "1",
                tx.TransactionNumber.ToString(),
                tx.FiscalTransactionId.ToString(),
                tx.TransactionNumber.ToString(),
                MapTransactionType(tx.TransactionType),
                tx.TransactionType.ToString(),
                "",
                "",
                FormatDecimal(tx.GrossAmount),
                tx.TransactionType == FiscalTransactionType.Void ? "1" : "0",
                FormatDateTime(tx.StartTime),
                FormatDateTime(tx.EndTime),
                tx.FiscalDeviceId.ToString(),
                tx.CertificateSerial ?? "",
                tx.TransactionNumber.ToString(),
                tx.SignatureCounter?.ToString() ?? "",
                tx.Signature ?? "",
                FormatDateTime(tx.StartTime),
                FormatDateTime(tx.EndTime),
                tx.ProcessType.ToString(),
                ""
            }));
        }

        await File.WriteAllTextAsync(Path.Combine(dir, "transactions.csv"), sb.ToString(), Encoding.UTF8);
    }

    private static async Task GeneratePaymentsCsvAsync(string dir, List<FiscalTransactionSnapshot> transactions)
    {
        var sb = new StringBuilder();
        // DSFinV-K payment.csv header
        sb.AppendLine("Z_KASSE_ID;Z_ERRESSION;Z_NR;BON_ID;ZAHLART_TYP;ZAHLART_NAME;ZAHLWAEHRUNG_CODE;ZAHLWAEHRUNG_BETRAG");

        foreach (var tx in transactions)
        {
            foreach (var payment in tx.PaymentTypes)
            {
                sb.AppendLine(string.Join(";", new[]
                {
                    tx.FiscalDeviceId.ToString(),
                    "1",
                    tx.TransactionNumber.ToString(),
                    tx.FiscalTransactionId.ToString(),
                    MapPaymentType(payment.Key),
                    payment.Key,
                    "EUR",
                    FormatDecimal(payment.Value)
                }));
            }
        }

        await File.WriteAllTextAsync(Path.Combine(dir, "payment.csv"), sb.ToString(), Encoding.UTF8);
    }

    private static async Task GenerateLinesCsvAsync(string dir, List<FiscalTransactionSnapshot> transactions)
    {
        var sb = new StringBuilder();
        // DSFinV-K lines.csv header
        sb.AppendLine("Z_KASSE_ID;Z_ERRESSION;Z_NR;BON_ID;POS_ZEILE;GUTSCHEIN_NR;ARTIKELTEXT;MENGE;FAKTOR;INHAUS;P_STORNO;STK_BR;UST_SCHLUESSEL;UST_PROZENT;WARENGR_ID;WARENGR_NAME");

        var lineNumber = 1;
        foreach (var tx in transactions)
        {
            // Since we don't have line items in the transaction, create a summary line
            foreach (var net in tx.NetAmounts)
            {
                var taxRate = net.Key switch
                {
                    "NORMAL" => "19.00",
                    "REDUCED" => "7.00",
                    "NULL" => "0.00",
                    _ => "19.00"
                };

                sb.AppendLine(string.Join(";", new[]
                {
                    tx.FiscalDeviceId.ToString(),
                    "1",
                    tx.TransactionNumber.ToString(),
                    tx.FiscalTransactionId.ToString(),
                    lineNumber.ToString(),
                    "",
                    $"Transaction {tx.TransactionNumber}",
                    "1",
                    "1.00",
                    "1",
                    tx.TransactionType == FiscalTransactionType.Void ? "1" : "0",
                    FormatDecimal(net.Value + (tx.TaxAmounts.GetValueOrDefault(net.Key, 0))),
                    MapVatKey(net.Key),
                    taxRate,
                    "1",
                    "General"
                }));
                lineNumber++;
            }
        }

        await File.WriteAllTextAsync(Path.Combine(dir, "lines.csv"), sb.ToString(), Encoding.UTF8);
    }

    private static async Task GenerateVatCsvAsync(string dir, List<FiscalTransactionSnapshot> transactions)
    {
        var sb = new StringBuilder();
        // DSFinV-K vat.csv header
        sb.AppendLine("Z_KASSE_ID;Z_ERRESSION;Z_NR;BON_ID;UST_SCHLUESSEL;UST_PROZENT;UST_BRUTTO;UST_NETTO;UST_UST");

        foreach (var tx in transactions)
        {
            foreach (var net in tx.NetAmounts)
            {
                var taxAmount = tx.TaxAmounts.GetValueOrDefault(net.Key, 0);
                var grossAmount = net.Value + taxAmount;
                var taxRate = net.Key switch
                {
                    "NORMAL" => "19.00",
                    "REDUCED" => "7.00",
                    "NULL" => "0.00",
                    _ => "19.00"
                };

                sb.AppendLine(string.Join(";", new[]
                {
                    tx.FiscalDeviceId.ToString(),
                    "1",
                    tx.TransactionNumber.ToString(),
                    tx.FiscalTransactionId.ToString(),
                    MapVatKey(net.Key),
                    taxRate,
                    FormatDecimal(grossAmount),
                    FormatDecimal(net.Value),
                    FormatDecimal(taxAmount)
                }));
            }
        }

        await File.WriteAllTextAsync(Path.Combine(dir, "vat.csv"), sb.ToString(), Encoding.UTF8);
    }

    private static async Task GenerateCashpointClosingCsvAsync(
        string dir,
        Guid orgId,
        Guid siteId,
        DateOnly startDate,
        DateOnly endDate,
        List<FiscalTransactionSnapshot> transactions)
    {
        var sb = new StringBuilder();
        // DSFinV-K cashpointclosing.csv header (Z-report data)
        sb.AppendLine("Z_KASSE_ID;Z_ERRESSION;Z_NR;Z_NAME;Z_ZEITPUNKT_BEGINN;Z_ZEITPUNKT_ENDE;Z_ANZAHL_BON;Z_UMSATZ_BRUTTO;Z_STORNO_BETRAG");

        var deviceGroups = transactions.GroupBy(t => t.FiscalDeviceId);
        foreach (var group in deviceGroups)
        {
            var zNr = 1;
            var totalGross = group.Sum(t => t.GrossAmount);
            var voidAmount = group.Where(t => t.TransactionType == FiscalTransactionType.Void)
                                  .Sum(t => t.GrossAmount);

            sb.AppendLine(string.Join(";", new[]
            {
                group.Key.ToString(),
                "1",
                zNr.ToString(),
                $"Z-Report {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                startDate.ToString("yyyy-MM-dd") + "T00:00:00",
                endDate.ToString("yyyy-MM-dd") + "T23:59:59",
                group.Count().ToString(),
                FormatDecimal(totalGross),
                FormatDecimal(voidAmount)
            }));
        }

        await File.WriteAllTextAsync(Path.Combine(dir, "cashpointclosing.csv"), sb.ToString(), Encoding.UTF8);
    }

    private static async Task GenerateLocationCsvAsync(string dir, Guid orgId, Guid siteId)
    {
        var sb = new StringBuilder();
        // DSFinV-K location.csv header
        sb.AppendLine("LOC_NAME;LOC_STRASSE;LOC_PLZ;LOC_ORT;LOC_LAND;LOC_USTID");

        sb.AppendLine(string.Join(";", new[]
        {
            $"Site {siteId}",
            "",
            "",
            "",
            "DE",
            ""
        }));

        await File.WriteAllTextAsync(Path.Combine(dir, "location.csv"), sb.ToString(), Encoding.UTF8);
    }

    private static async Task GenerateIndexXmlAsync(
        string dir,
        Guid orgId,
        Guid siteId,
        DateOnly startDate,
        DateOnly endDate,
        int transactionCount)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("DataExport",
                new XAttribute("Version", "2.0"),
                new XElement("ExportCreationDate", DateTime.UtcNow.ToString("O")),
                new XElement("DataSupplier",
                    new XElement("Name", "DarkVelocity POS"),
                    new XElement("Location", siteId.ToString()),
                    new XElement("TaxNumber", "")),
                new XElement("Period",
                    new XElement("StartDate", startDate.ToString("yyyy-MM-dd")),
                    new XElement("EndDate", endDate.ToString("yyyy-MM-dd"))),
                new XElement("Statistics",
                    new XElement("TotalTransactions", transactionCount)),
                new XElement("Files",
                    new XElement("File", new XAttribute("Name", "transactions.csv"), new XAttribute("Type", "Transactions")),
                    new XElement("File", new XAttribute("Name", "payment.csv"), new XAttribute("Type", "Payments")),
                    new XElement("File", new XAttribute("Name", "lines.csv"), new XAttribute("Type", "Lines")),
                    new XElement("File", new XAttribute("Name", "vat.csv"), new XAttribute("Type", "VAT")),
                    new XElement("File", new XAttribute("Name", "cashpointclosing.csv"), new XAttribute("Type", "CashpointClosing")),
                    new XElement("File", new XAttribute("Name", "location.csv"), new XAttribute("Type", "Location")))));

        await using var writer = new StreamWriter(Path.Combine(dir, "index.xml"), false, Encoding.UTF8);
        await writer.WriteAsync(doc.ToString());
    }

    public async Task<DSFinVKExportResponse?> GetExportStatusAsync(Guid orgId, Guid siteId, Guid exportId)
    {
        var exportGrain = _grainFactory.GetGrain<IDSFinVKExportGrain>(
            GrainKeys.DSFinVKExport(orgId, siteId, exportId));

        var state = await exportGrain.GetStateAsync();
        if (state.ExportId == Guid.Empty)
            return null;

        return ToResponse(state);
    }

    public async Task<(Stream? Stream, string FileName)> DownloadExportAsync(Guid orgId, Guid siteId, Guid exportId)
    {
        var exportGrain = _grainFactory.GetGrain<IDSFinVKExportGrain>(
            GrainKeys.DSFinVKExport(orgId, siteId, exportId));

        var state = await exportGrain.GetStateAsync();
        if (state.ExportId == Guid.Empty || state.Status != DSFinVKExportStatus.Completed || string.IsNullOrEmpty(state.FilePath))
            return (null, "");

        if (!File.Exists(state.FilePath))
            return (null, "");

        var stream = new FileStream(state.FilePath, FileMode.Open, FileAccess.Read);
        var fileName = Path.GetFileName(state.FilePath);

        return (stream, fileName);
    }

    public async Task<List<DSFinVKExportResponse>> ListExportsAsync(Guid orgId, Guid siteId)
    {
        var registryGrain = _grainFactory.GetGrain<IDSFinVKExportRegistryGrain>(
            GrainKeys.DSFinVKExportRegistry(orgId, siteId));

        var exportIds = await registryGrain.GetExportIdsAsync();
        var exports = new List<DSFinVKExportResponse>();

        foreach (var exportId in exportIds.Take(50)) // Limit to last 50
        {
            var exportGrain = _grainFactory.GetGrain<IDSFinVKExportGrain>(
                GrainKeys.DSFinVKExport(orgId, siteId, exportId));

            var state = await exportGrain.GetStateAsync();
            if (state.ExportId != Guid.Empty)
            {
                exports.Add(ToResponse(state));
            }
        }

        return exports;
    }

    private static DSFinVKExportResponse ToResponse(DSFinVKExportState state)
    {
        return new DSFinVKExportResponse(
            ExportId: state.ExportId,
            StartDate: state.StartDate,
            EndDate: state.EndDate,
            Status: state.Status.ToString(),
            TransactionCount: state.TransactionCount,
            DownloadUrl: state.Status == DSFinVKExportStatus.Completed ? state.DownloadUrl : null,
            CreatedAt: state.CreatedAt,
            CompletedAt: state.CompletedAt,
            ErrorMessage: state.ErrorMessage);
    }

    private static string FormatDecimal(decimal value)
    {
        return value.ToString("F2", CultureInfo.InvariantCulture);
    }

    private static string FormatDateTime(DateTime? dt)
    {
        return dt?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "";
    }

    private static string MapTransactionType(FiscalTransactionType type)
    {
        return type switch
        {
            FiscalTransactionType.Receipt => "Beleg",
            FiscalTransactionType.TrainingReceipt => "Training",
            FiscalTransactionType.Void => "Storno",
            FiscalTransactionType.Cancellation => "Storno",
            _ => "Beleg"
        };
    }

    private static string MapPaymentType(string paymentType)
    {
        return paymentType.ToUpperInvariant() switch
        {
            "CASH" => "Bar",
            "CARD" => "Unbar",
            "CREDIT" => "Unbar",
            "DEBIT" => "Unbar",
            _ => "Unbar"
        };
    }

    private static string MapVatKey(string vatRate)
    {
        return vatRate.ToUpperInvariant() switch
        {
            "NORMAL" => "1",
            "REDUCED" => "2",
            "NULL" => "5",
            "ZERO" => "5",
            _ => "1"
        };
    }
}
