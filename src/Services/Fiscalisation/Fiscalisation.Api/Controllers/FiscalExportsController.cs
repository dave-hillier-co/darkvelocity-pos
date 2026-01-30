using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DarkVelocity.Fiscalisation.Api.Data;
using DarkVelocity.Fiscalisation.Api.Dtos;
using DarkVelocity.Fiscalisation.Api.Entities;
using DarkVelocity.Fiscalisation.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Fiscalisation.Api.Controllers;

[ApiController]
[Route("api/fiscal-exports")]
public class FiscalExportsController : ControllerBase
{
    private readonly FiscalisationDbContext _context;
    private readonly IFiscalJournalService _journalService;

    public FiscalExportsController(
        FiscalisationDbContext context,
        IFiscalJournalService journalService)
    {
        _context = context;
        _journalService = journalService;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<FiscalExportDto>>> GetAll(
        [FromQuery] Guid? locationId = null,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50)
    {
        var query = _context.FiscalExports.AsQueryable();

        if (locationId.HasValue)
            query = query.Where(e => e.LocationId == locationId.Value);

        if (tenantId.HasValue)
            query = query.Where(e => e.TenantId == tenantId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(e => e.Status == status);

        var exports = await query
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync();

        var dtos = exports.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/fiscal-exports/{dto.Id}");
            if (dto.Status == "Completed" && !string.IsNullOrEmpty(dto.FileUrl))
            {
                dto.AddLink("download", $"/api/fiscal-exports/{dto.Id}/download");
            }
        }

        return Ok(HalCollection<FiscalExportDto>.Create(
            dtos,
            "/api/fiscal-exports",
            dtos.Count));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FiscalExportDto>> GetById(Guid id)
    {
        var export = await _context.FiscalExports.FindAsync(id);

        if (export == null)
            return NotFound();

        var dto = MapToDto(export);
        dto.AddSelfLink($"/api/fiscal-exports/{export.Id}");

        if (export.Status == "Completed" && !string.IsNullOrEmpty(export.FileUrl))
        {
            dto.AddLink("download", $"/api/fiscal-exports/{export.Id}/download");
        }

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<FiscalExportDto>> Create([FromBody] CreateFiscalExportRequest request)
    {
        // Validate date range
        if (request.EndDate < request.StartDate)
        {
            return BadRequest(new { message = "End date must be after start date" });
        }

        // Check for existing export with same parameters
        var existingExport = await _context.FiscalExports
            .FirstOrDefaultAsync(e =>
                e.LocationId == request.LocationId &&
                e.TenantId == request.TenantId &&
                e.StartDate == request.StartDate &&
                e.EndDate == request.EndDate &&
                e.Status == "Completed");

        if (existingExport != null)
        {
            return Conflict(new
            {
                message = "An export with the same parameters already exists",
                existingExportId = existingExport.Id
            });
        }

        var export = new FiscalExport
        {
            TenantId = request.TenantId,
            LocationId = request.LocationId,
            ExportType = request.ExportType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = "Generating",
            RequestedByUserId = request.RequestedByUserId,
            AuditReference = request.AuditReference
        };

        _context.FiscalExports.Add(export);
        await _context.SaveChangesAsync();

        await _journalService.LogAsync(
            request.TenantId,
            "ExportRequested",
            new { ExportId = export.Id, StartDate = request.StartDate, EndDate = request.EndDate },
            locationId: request.LocationId,
            exportId: export.Id,
            userId: request.RequestedByUserId,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        // Generate the export (in production, this would be done async via a background job)
        try
        {
            await GenerateExportAsync(export);
        }
        catch (Exception ex)
        {
            export.Status = "Failed";
            await _context.SaveChangesAsync();

            await _journalService.LogAsync(
                request.TenantId,
                "ExportFailed",
                new { ExportId = export.Id, Error = ex.Message },
                locationId: request.LocationId,
                exportId: export.Id,
                severity: "Error",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return StatusCode(500, new { message = "Export generation failed", error = ex.Message });
        }

        var dto = MapToDto(export);
        dto.AddSelfLink($"/api/fiscal-exports/{export.Id}");

        return CreatedAtAction(nameof(GetById), new { id = export.Id }, dto);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var export = await _context.FiscalExports.FindAsync(id);

        if (export == null)
            return NotFound();

        if (export.Status != "Completed")
            return BadRequest(new { message = "Export is not yet completed" });

        if (string.IsNullOrEmpty(export.FileUrl))
            return NotFound(new { message = "Export file not found" });

        // In production, this would serve the actual file from storage
        // For now, generate a placeholder DSFinV-K structure
        var transactions = await _context.FiscalTransactions
            .Where(t =>
                t.LocationId == export.LocationId &&
                t.TenantId == export.TenantId &&
                t.StartTime >= export.StartDate &&
                t.EndTime <= export.EndDate &&
                t.Status == "Signed")
            .ToListAsync();

        var content = GenerateDsFinVKContent(export, transactions);
        var bytes = Encoding.UTF8.GetBytes(content);

        await _journalService.LogAsync(
            export.TenantId,
            "ExportDownloaded",
            new { ExportId = export.Id },
            locationId: export.LocationId,
            exportId: export.Id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return File(bytes, "application/zip", $"dsfinvk_{export.StartDate:yyyyMMdd}_{export.EndDate:yyyyMMdd}.zip");
    }

    private async Task GenerateExportAsync(FiscalExport export)
    {
        var transactions = await _context.FiscalTransactions
            .Where(t =>
                t.LocationId == export.LocationId &&
                t.TenantId == export.TenantId &&
                t.StartTime >= export.StartDate &&
                t.EndTime <= export.EndDate &&
                t.Status == "Signed")
            .ToListAsync();

        export.TransactionCount = transactions.Count;

        // Generate export content
        var content = GenerateDsFinVKContent(export, transactions);
        var contentBytes = Encoding.UTF8.GetBytes(content);

        // Calculate hash
        export.FileSha256 = Convert.ToHexString(SHA256.HashData(contentBytes)).ToLower();
        export.FileSizeBytes = contentBytes.Length;

        // In production, upload to storage and set FileUrl
        export.FileUrl = $"/exports/{export.TenantId}/{export.Id}.zip";
        export.Status = "Completed";
        export.GeneratedAt = DateTime.UtcNow;

        // Mark transactions as exported
        foreach (var transaction in transactions)
        {
            transaction.ExportedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        await _journalService.LogAsync(
            export.TenantId,
            "ExportGenerated",
            new { ExportId = export.Id, TransactionCount = export.TransactionCount },
            locationId: export.LocationId,
            exportId: export.Id,
            ipAddress: null);
    }

    private static string GenerateDsFinVKContent(FiscalExport export, List<FiscalTransaction> transactions)
    {
        // Generate DSFinV-K format (simplified representation)
        // In production, this would generate proper CSV files in the required structure
        var sb = new StringBuilder();

        sb.AppendLine("# DSFinV-K Export");
        sb.AppendLine($"# Generated: {DateTime.UtcNow:O}");
        sb.AppendLine($"# Location: {export.LocationId}");
        sb.AppendLine($"# Period: {export.StartDate:yyyy-MM-dd} to {export.EndDate:yyyy-MM-dd}");
        sb.AppendLine($"# Transactions: {transactions.Count}");
        sb.AppendLine();

        // Transactions header
        sb.AppendLine("# transactions.csv");
        sb.AppendLine("Z_KASSE_ID,Z_ERESSION,Z_NR,BON_ID,BON_NR,BON_TYP,BON_START,BON_ENDE,BRUTTO,NETTO,UST");

        foreach (var tx in transactions)
        {
            sb.AppendLine($"{tx.LocationId},{tx.FiscalDeviceId},{tx.TransactionNumber},{tx.Id},{tx.TransactionNumber},{tx.TransactionType},{tx.StartTime:O},{tx.EndTime:O},{tx.GrossAmount:F2},{tx.NetAmounts},{tx.TaxAmounts}");
        }

        sb.AppendLine();

        // TSE data header
        sb.AppendLine("# transactions_tse.csv");
        sb.AppendLine("Z_KASSE_ID,BON_ID,TSE_SERIAL,TSE_SIG_NR,TSE_SIGNATURE,TSE_START,TSE_ENDE");

        foreach (var tx in transactions)
        {
            sb.AppendLine($"{tx.LocationId},{tx.Id},{tx.CertificateSerial},{tx.SignatureCounter},{tx.Signature},{tx.StartTime:O},{tx.EndTime:O}");
        }

        return sb.ToString();
    }

    private static FiscalExportDto MapToDto(FiscalExport export)
    {
        return new FiscalExportDto
        {
            Id = export.Id,
            TenantId = export.TenantId,
            LocationId = export.LocationId,
            ExportType = export.ExportType,
            StartDate = export.StartDate,
            EndDate = export.EndDate,
            Status = export.Status,
            FileUrl = export.FileUrl,
            FileSha256 = export.FileSha256,
            TransactionCount = export.TransactionCount,
            GeneratedAt = export.GeneratedAt,
            RequestedByUserId = export.RequestedByUserId,
            AuditReference = export.AuditReference,
            FileSizeBytes = export.FileSizeBytes,
            CreatedAt = export.CreatedAt
        };
    }
}
