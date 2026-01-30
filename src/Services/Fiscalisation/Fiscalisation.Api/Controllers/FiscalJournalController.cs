using DarkVelocity.Fiscalisation.Api.Data;
using DarkVelocity.Fiscalisation.Api.Dtos;
using DarkVelocity.Fiscalisation.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Fiscalisation.Api.Controllers;

[ApiController]
[Route("api/fiscal-journal")]
public class FiscalJournalController : ControllerBase
{
    private readonly FiscalisationDbContext _context;

    public FiscalJournalController(FiscalisationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<FiscalJournalDto>>> GetAll(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] string? eventType = null,
        [FromQuery] string? severity = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] Guid? deviceId = null,
        [FromQuery] Guid? transactionId = null,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0)
    {
        var query = _context.FiscalJournals.AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(j => j.TenantId == tenantId.Value);

        if (locationId.HasValue)
            query = query.Where(j => j.LocationId == locationId.Value);

        if (!string.IsNullOrEmpty(eventType))
            query = query.Where(j => j.EventType == eventType);

        if (!string.IsNullOrEmpty(severity))
            query = query.Where(j => j.Severity == severity);

        if (from.HasValue)
            query = query.Where(j => j.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(j => j.Timestamp <= to.Value);

        if (deviceId.HasValue)
            query = query.Where(j => j.DeviceId == deviceId.Value);

        if (transactionId.HasValue)
            query = query.Where(j => j.TransactionId == transactionId.Value);

        var totalCount = await query.CountAsync();

        var entries = await query
            .OrderByDescending(j => j.Timestamp)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = entries.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/fiscal-journal/{dto.Id}");
        }

        var collection = HalCollection<FiscalJournalDto>.Create(
            dtos,
            "/api/fiscal-journal",
            totalCount);

        // Add pagination links
        if (offset > 0)
        {
            collection.AddLink("prev", $"/api/fiscal-journal?offset={Math.Max(0, offset - limit)}&limit={limit}");
        }
        if (offset + limit < totalCount)
        {
            collection.AddLink("next", $"/api/fiscal-journal?offset={offset + limit}&limit={limit}");
        }

        return Ok(collection);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FiscalJournalDto>> GetById(Guid id)
    {
        var entry = await _context.FiscalJournals.FindAsync(id);

        if (entry == null)
            return NotFound();

        var dto = MapToDto(entry);
        dto.AddSelfLink($"/api/fiscal-journal/{entry.Id}");

        if (entry.DeviceId.HasValue)
            dto.AddLink("device", $"/api/fiscal-devices/{entry.DeviceId}");

        if (entry.TransactionId.HasValue)
            dto.AddLink("transaction", $"/api/fiscal-transactions/{entry.TransactionId}");

        if (entry.ExportId.HasValue)
            dto.AddLink("export", $"/api/fiscal-exports/{entry.ExportId}");

        return Ok(dto);
    }

    [HttpGet("event-types")]
    public ActionResult<IEnumerable<string>> GetEventTypes()
    {
        var eventTypes = new[]
        {
            "DeviceRegistered",
            "DeviceInitialized",
            "DeviceInitializationFailed",
            "DeviceDecommissioned",
            "DecommissionFailed",
            "DeviceStatusChanged",
            "SelfTestPerformed",
            "SelfTestFailed",
            "TransactionSigned",
            "TransactionSigningFailed",
            "TransactionStartFailed",
            "TransactionVoided",
            "ExportRequested",
            "ExportGenerated",
            "ExportFailed",
            "ExportDownloaded"
        };

        return Ok(eventTypes);
    }

    [HttpGet("summary")]
    public async Task<ActionResult> GetSummary(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query = _context.FiscalJournals.AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(j => j.TenantId == tenantId.Value);

        if (locationId.HasValue)
            query = query.Where(j => j.LocationId == locationId.Value);

        if (from.HasValue)
            query = query.Where(j => j.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(j => j.Timestamp <= to.Value);

        var summary = await query
            .GroupBy(j => new { j.EventType, j.Severity })
            .Select(g => new
            {
                EventType = g.Key.EventType,
                Severity = g.Key.Severity,
                Count = g.Count()
            })
            .ToListAsync();

        var totalByEventType = summary
            .GroupBy(s => s.EventType)
            .Select(g => new { EventType = g.Key, Count = g.Sum(x => x.Count) })
            .OrderByDescending(x => x.Count)
            .ToList();

        var totalBySeverity = summary
            .GroupBy(s => s.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Sum(x => x.Count) })
            .ToList();

        return Ok(new
        {
            ByEventType = totalByEventType,
            BySeverity = totalBySeverity,
            TotalEntries = summary.Sum(s => s.Count)
        });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] Guid tenantId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string format = "json")
    {
        var query = _context.FiscalJournals
            .Where(j => j.TenantId == tenantId);

        if (from.HasValue)
            query = query.Where(j => j.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(j => j.Timestamp <= to.Value);

        var entries = await query
            .OrderBy(j => j.Timestamp)
            .ToListAsync();

        var dtos = entries.Select(MapToDto).ToList();

        if (format.ToLower() == "csv")
        {
            var csv = GenerateCsvExport(dtos);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"fiscal_journal_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        return Ok(dtos);
    }

    private static string GenerateCsvExport(List<FiscalJournalDto> entries)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Id,TenantId,LocationId,Timestamp,EventType,Severity,DeviceId,TransactionId,ExportId,UserId,IpAddress,Details");

        foreach (var entry in entries)
        {
            sb.AppendLine($"{entry.Id},{entry.TenantId},{entry.LocationId},{entry.Timestamp:O},{entry.EventType},{entry.Severity},{entry.DeviceId},{entry.TransactionId},{entry.ExportId},{entry.UserId},{entry.IpAddress},\"{entry.Details.Replace("\"", "\"\"")}\"");
        }

        return sb.ToString();
    }

    private static FiscalJournalDto MapToDto(FiscalJournal entry)
    {
        return new FiscalJournalDto
        {
            Id = entry.Id,
            TenantId = entry.TenantId,
            LocationId = entry.LocationId,
            Timestamp = entry.Timestamp,
            EventType = entry.EventType,
            DeviceId = entry.DeviceId,
            TransactionId = entry.TransactionId,
            ExportId = entry.ExportId,
            Details = entry.Details,
            IpAddress = entry.IpAddress,
            UserId = entry.UserId,
            Severity = entry.Severity
        };
    }
}
