using DarkVelocity.Accounting.Api.Data;
using DarkVelocity.Accounting.Api.Dtos;
using DarkVelocity.Accounting.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DarkVelocity.Accounting.Api.Controllers;

[ApiController]
[Route("api/exports")]
public class ExportsController : ControllerBase
{
    private readonly AccountingDbContext _context;

    // TODO: In multi-tenant implementation, inject ITenantContext to get TenantId
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public ExportsController(AccountingDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Export journal entries to DATEV format (German accounting standard)
    /// </summary>
    [HttpPost("datev")]
    public async Task<IActionResult> ExportDatev([FromBody] ExportDatevRequest request)
    {
        var entries = await GetJournalEntriesForExport(
            request.StartDate,
            request.EndDate,
            request.LocationId);

        if (!entries.Any())
        {
            return NotFound(new { message = "No journal entries found for the specified period" });
        }

        var csv = GenerateDatevCsv(entries, request.IncludeHeaders);

        var fileName = $"DATEV_Export_{request.StartDate:yyyyMMdd}_{request.EndDate:yyyyMMdd}.csv";

        return File(
            Encoding.UTF8.GetBytes(csv),
            "text/csv",
            fileName);
    }

    /// <summary>
    /// Export journal entries to generic CSV format
    /// </summary>
    [HttpPost("csv")]
    public async Task<IActionResult> ExportCsv([FromBody] ExportCsvRequest request)
    {
        var query = _context.JournalEntries
            .Include(e => e.Lines)
            .Where(e =>
                e.TenantId == DefaultTenantId &&
                e.Status == JournalEntryStatus.Posted &&
                e.EntryDate >= request.StartDate &&
                e.EntryDate <= request.EndDate);

        if (request.LocationId.HasValue)
        {
            query = query.Where(e => e.LocationId == request.LocationId.Value);
        }

        var entries = await query
            .OrderBy(e => e.EntryDate)
            .ThenBy(e => e.EntryNumber)
            .ToListAsync();

        if (request.AccountCodes?.Any() == true)
        {
            // Filter entries to only include those with matching account codes
            entries = entries
                .Where(e => e.Lines.Any(l => request.AccountCodes.Contains(l.AccountCode)))
                .ToList();
        }

        if (!entries.Any())
        {
            return NotFound(new { message = "No journal entries found for the specified criteria" });
        }

        var csv = GenerateGenericCsv(entries, request.IncludeHeaders);

        var fileName = $"JournalEntries_{request.StartDate:yyyyMMdd}_{request.EndDate:yyyyMMdd}.csv";

        return File(
            Encoding.UTF8.GetBytes(csv),
            "text/csv",
            fileName);
    }

    /// <summary>
    /// Export journal entries to Sage format
    /// </summary>
    [HttpPost("sage")]
    public async Task<IActionResult> ExportSage([FromBody] ExportSageRequest request)
    {
        var entries = await GetJournalEntriesForExport(
            request.StartDate,
            request.EndDate,
            request.LocationId);

        if (!entries.Any())
        {
            return NotFound(new { message = "No journal entries found for the specified period" });
        }

        var csv = GenerateSageCsv(entries);

        var fileName = $"Sage_Export_{request.StartDate:yyyyMMdd}_{request.EndDate:yyyyMMdd}.csv";

        return File(
            Encoding.UTF8.GetBytes(csv),
            "text/csv",
            fileName);
    }

    /// <summary>
    /// Export journal entries to Xero format
    /// </summary>
    [HttpPost("xero")]
    public async Task<IActionResult> ExportXero([FromBody] ExportXeroRequest request)
    {
        var entries = await GetJournalEntriesForExport(
            request.StartDate,
            request.EndDate,
            request.LocationId);

        if (!entries.Any())
        {
            return NotFound(new { message = "No journal entries found for the specified period" });
        }

        var csv = GenerateXeroCsv(entries);

        var fileName = $"Xero_Export_{request.StartDate:yyyyMMdd}_{request.EndDate:yyyyMMdd}.csv";

        return File(
            Encoding.UTF8.GetBytes(csv),
            "text/csv",
            fileName);
    }

    /// <summary>
    /// Export journal entries to QuickBooks format
    /// </summary>
    [HttpPost("quickbooks")]
    public async Task<IActionResult> ExportQuickBooks([FromBody] ExportQuickBooksRequest request)
    {
        var entries = await GetJournalEntriesForExport(
            request.StartDate,
            request.EndDate,
            request.LocationId);

        if (!entries.Any())
        {
            return NotFound(new { message = "No journal entries found for the specified period" });
        }

        var csv = GenerateQuickBooksCsv(entries);

        var fileName = $"QuickBooks_Export_{request.StartDate:yyyyMMdd}_{request.EndDate:yyyyMMdd}.csv";

        return File(
            Encoding.UTF8.GetBytes(csv),
            "text/csv",
            fileName);
    }

    private async Task<List<JournalEntry>> GetJournalEntriesForExport(
        DateOnly startDate,
        DateOnly endDate,
        Guid? locationId)
    {
        var query = _context.JournalEntries
            .Include(e => e.Lines)
            .Where(e =>
                e.TenantId == DefaultTenantId &&
                e.Status == JournalEntryStatus.Posted &&
                e.EntryDate >= startDate &&
                e.EntryDate <= endDate);

        if (locationId.HasValue)
        {
            query = query.Where(e => e.LocationId == locationId.Value);
        }

        return await query
            .OrderBy(e => e.EntryDate)
            .ThenBy(e => e.EntryNumber)
            .ToListAsync();
    }

    private static string GenerateDatevCsv(List<JournalEntry> entries, bool includeHeaders)
    {
        var sb = new StringBuilder();

        // DATEV format header
        if (includeHeaders)
        {
            sb.AppendLine("Umsatz (ohne Soll/Haben-Kz);Soll/Haben-Kennzeichen;WKZ Umsatz;Kurs;Basis-Umsatz;WKZ Basis-Umsatz;Konto;Gegenkonto (ohne BU-Schlüssel);BU-Schlüssel;Belegdatum;Belegfeld 1;Belegfeld 2;Skonto;Buchungstext;Postensperre;Diverse Adressnummer;Geschäftspartnerbank;Sachverhalt;Zinssperre;Beleglink;Beleginfo - Art 1;Beleginfo - Inhalt 1;KOST1 - Kostenstelle;KOST2 - Kostenstelle;KOST-Menge;EU-Land u. UStID;EU-Steuersatz;Abw. Versteuerungsart;Sachverhalt L+L;Funktionsergänzung L+L;BU 49 Hauptfunktionstyp;BU 49 Hauptfunktionsnummer;BU 49 Funktionsergänzung;Zusatzinformation - Art 1;Zusatzinformation - Inhalt 1;Stück;Gewicht;Zahlweise;Forderungsart;Veranlagungsjahr;Zugeordnete Fälligkeit;Skontotyp;Auftragsnummer;Buchungstyp;USt-Schlüssel (Anzahlungen);EU-Land (Anzahlungen);Sachverhalt L+L (Anzahlungen);EU-Steuersatz (Anzahlungen);Erlöskonto (Anzahlungen);Herkunft-Kz;Buchungs GUID;KOST-Datum;SEPA-Mandatsreferenz;Skontosperre;Gesellschaftername;Beteiligtennummer;Identifikationsnummer;Zeichnernummer;Postensperre bis;Bezeichnung SoBil-Sachverhalt;Kennzeichen SoBil-Buchung;Festschreibung;Leistungsdatum;Datum Zuord.Steuerperiode");
        }

        foreach (var entry in entries)
        {
            foreach (var line in entry.Lines.OrderBy(l => l.LineNumber))
            {
                var amount = line.DebitAmount > 0 ? line.DebitAmount : line.CreditAmount;
                var sollHaben = line.DebitAmount > 0 ? "S" : "H";

                sb.Append($"{amount:0.00};");
                sb.Append($"{sollHaben};");
                sb.Append($"{entry.Currency};");
                sb.Append(";"); // Kurs
                sb.Append(";"); // Basis-Umsatz
                sb.Append(";"); // WKZ Basis-Umsatz
                sb.Append($"{line.AccountCode};");
                sb.Append(";"); // Gegenkonto
                sb.Append(";"); // BU-Schlüssel
                sb.Append($"{entry.EntryDate:ddMMyyyy};");
                sb.Append($"{entry.EntryNumber};");
                sb.Append(";"); // Belegfeld 2
                sb.Append(";"); // Skonto
                sb.Append($"\"{EscapeCsv(entry.Description)}\";");
                // Fill remaining fields with defaults
                sb.AppendLine(";;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;");
            }
        }

        return sb.ToString();
    }

    private static string GenerateGenericCsv(List<JournalEntry> entries, bool includeHeaders)
    {
        var sb = new StringBuilder();

        if (includeHeaders)
        {
            sb.AppendLine("Entry Number,Entry Date,Posted At,Description,Source Type,Account Code,Account Name,Debit,Credit,Tax Code,Tax Amount,Cost Center,Currency");
        }

        foreach (var entry in entries)
        {
            foreach (var line in entry.Lines.OrderBy(l => l.LineNumber))
            {
                sb.Append($"{EscapeCsv(entry.EntryNumber)},");
                sb.Append($"{entry.EntryDate:yyyy-MM-dd},");
                sb.Append($"{entry.PostedAt:yyyy-MM-ddTHH:mm:ssZ},");
                sb.Append($"\"{EscapeCsv(entry.Description)}\",");
                sb.Append($"{entry.SourceType},");
                sb.Append($"{line.AccountCode},");
                sb.Append($"\"{EscapeCsv(line.AccountName)}\",");
                sb.Append($"{line.DebitAmount:0.00},");
                sb.Append($"{line.CreditAmount:0.00},");
                sb.Append($"{line.TaxCode ?? ""},");
                sb.Append($"{line.TaxAmount?.ToString("0.00") ?? ""},");
                sb.Append($"{line.CostCenterId?.ToString() ?? ""},");
                sb.AppendLine($"{entry.Currency}");
            }
        }

        return sb.ToString();
    }

    private static string GenerateSageCsv(List<JournalEntry> entries)
    {
        var sb = new StringBuilder();

        // Sage format header
        sb.AppendLine("Type,Account Ref,Nominal A/C Ref,Dept,Date,Details,Net Amount,Tax Code,Tax Amount");

        foreach (var entry in entries)
        {
            foreach (var line in entry.Lines.OrderBy(l => l.LineNumber))
            {
                var netAmount = line.DebitAmount > 0 ? line.DebitAmount : -line.CreditAmount;

                sb.Append("JC,"); // Journal Credit/Debit
                sb.Append($","); // Account Ref
                sb.Append($"{line.AccountCode},");
                sb.Append(","); // Dept
                sb.Append($"{entry.EntryDate:dd/MM/yyyy},");
                sb.Append($"\"{EscapeCsv(entry.Description)}\",");
                sb.Append($"{netAmount:0.00},");
                sb.Append($"{line.TaxCode ?? "T9"},");
                sb.AppendLine($"{line.TaxAmount?.ToString("0.00") ?? "0.00"}");
            }
        }

        return sb.ToString();
    }

    private static string GenerateXeroCsv(List<JournalEntry> entries)
    {
        var sb = new StringBuilder();

        // Xero Manual Journal format
        sb.AppendLine("*Narration,*Date,*AccountCode,*Description,*TaxType,*TaxAmount,*Debit,*Credit");

        foreach (var entry in entries)
        {
            foreach (var line in entry.Lines.OrderBy(l => l.LineNumber))
            {
                sb.Append($"\"{EscapeCsv(entry.Description)}\",");
                sb.Append($"{entry.EntryDate:yyyy-MM-dd},");
                sb.Append($"{line.AccountCode},");
                sb.Append($"\"{EscapeCsv(line.Description ?? entry.Description)}\",");
                sb.Append($"{MapTaxCodeToXero(line.TaxCode)},");
                sb.Append($"{line.TaxAmount?.ToString("0.00") ?? ""},");
                sb.Append($"{(line.DebitAmount > 0 ? line.DebitAmount.ToString("0.00") : "")},");
                sb.AppendLine($"{(line.CreditAmount > 0 ? line.CreditAmount.ToString("0.00") : "")}");
            }
        }

        return sb.ToString();
    }

    private static string GenerateQuickBooksCsv(List<JournalEntry> entries)
    {
        var sb = new StringBuilder();

        // QuickBooks IIF format header
        sb.AppendLine("!TRNS\tTRNSTYPE\tDATE\tACCNT\tNAME\tAMOUNT\tMEMO");
        sb.AppendLine("!SPL\tTRNSTYPE\tDATE\tACCNT\tNAME\tAMOUNT\tMEMO");
        sb.AppendLine("!ENDTRNS");

        foreach (var entry in entries)
        {
            var lines = entry.Lines.OrderBy(l => l.LineNumber).ToList();
            var isFirstLine = true;

            foreach (var line in lines)
            {
                var amount = line.DebitAmount > 0 ? line.DebitAmount : -line.CreditAmount;
                var rowType = isFirstLine ? "TRNS" : "SPL";

                sb.Append($"{rowType}\t");
                sb.Append("GENERAL JOURNAL\t");
                sb.Append($"{entry.EntryDate:MM/dd/yyyy}\t");
                sb.Append($"{line.AccountCode}\t");
                sb.Append("\t"); // Name
                sb.Append($"{amount:0.00}\t");
                sb.AppendLine($"{EscapeCsv(entry.Description)}");

                isFirstLine = false;
            }

            sb.AppendLine("ENDTRNS");
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Replace("\"", "\"\"");
    }

    private static string MapTaxCodeToXero(string? taxCode)
    {
        if (string.IsNullOrEmpty(taxCode))
            return "NONE";

        return taxCode.ToUpperInvariant() switch
        {
            "A" => "OUTPUT2", // Standard rate
            "B" => "OUTPUT", // Reduced rate
            "C" => "EXEMPTOUTPUT", // Exempt
            _ => "NONE"
        };
    }
}
