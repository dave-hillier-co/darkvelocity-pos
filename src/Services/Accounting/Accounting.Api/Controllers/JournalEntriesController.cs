using DarkVelocity.Accounting.Api.Data;
using DarkVelocity.Accounting.Api.Dtos;
using DarkVelocity.Accounting.Api.Entities;
using DarkVelocity.Accounting.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Accounting.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/journal-entries")]
public class JournalEntriesController : ControllerBase
{
    private readonly AccountingDbContext _context;
    private readonly IJournalEntryNumberGenerator _entryNumberGenerator;

    // TODO: In multi-tenant implementation, inject ITenantContext to get TenantId
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public JournalEntriesController(
        AccountingDbContext context,
        IJournalEntryNumberGenerator entryNumberGenerator)
    {
        _context = context;
        _entryNumberGenerator = entryNumberGenerator;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<JournalEntryDto>>> GetAll(
        Guid locationId,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        [FromQuery] JournalEntrySourceType? sourceType = null,
        [FromQuery] JournalEntryStatus? status = null,
        [FromQuery] int limit = 50)
    {
        var query = _context.JournalEntries
            .Include(e => e.Lines)
            .Where(e => e.LocationId == locationId && e.TenantId == DefaultTenantId);

        if (startDate.HasValue)
        {
            query = query.Where(e => e.EntryDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(e => e.EntryDate <= endDate.Value);
        }

        if (sourceType.HasValue)
        {
            query = query.Where(e => e.SourceType == sourceType.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        var entries = await query
            .OrderByDescending(e => e.PostedAt)
            .Take(limit)
            .ToListAsync();

        var dtos = entries.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/locations/{locationId}/journal-entries/{dto.Id}");
        }

        return Ok(HalCollection<JournalEntryDto>.Create(
            dtos,
            $"/api/locations/{locationId}/journal-entries",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JournalEntryDto>> GetById(Guid locationId, Guid id)
    {
        var entry = await _context.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == id && e.LocationId == locationId && e.TenantId == DefaultTenantId);

        if (entry == null)
            return NotFound();

        var dto = MapToDto(entry);
        dto.AddSelfLink($"/api/locations/{locationId}/journal-entries/{entry.Id}");
        dto.AddLink("lines", $"/api/locations/{locationId}/journal-entries/{entry.Id}/lines");

        if (entry.ReversedByEntryId.HasValue)
        {
            dto.AddLink("reversedBy", $"/api/locations/{locationId}/journal-entries/{entry.ReversedByEntryId}");
        }

        if (entry.ReversesEntryId.HasValue)
        {
            dto.AddLink("reverses", $"/api/locations/{locationId}/journal-entries/{entry.ReversesEntryId}");
        }

        return Ok(dto);
    }

    [HttpGet("by-source/{sourceType}/{sourceId:guid}")]
    public async Task<ActionResult<JournalEntryDto>> GetBySource(
        Guid locationId,
        JournalEntrySourceType sourceType,
        Guid sourceId)
    {
        var entry = await _context.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e =>
                e.LocationId == locationId &&
                e.TenantId == DefaultTenantId &&
                e.SourceType == sourceType &&
                e.SourceId == sourceId);

        if (entry == null)
            return NotFound();

        var dto = MapToDto(entry);
        dto.AddSelfLink($"/api/locations/{locationId}/journal-entries/{entry.Id}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<JournalEntryDto>> Create(
        Guid locationId,
        [FromBody] CreateJournalEntryRequest request)
    {
        // Validate that debits equal credits
        var totalDebits = request.Lines.Sum(l => l.DebitAmount);
        var totalCredits = request.Lines.Sum(l => l.CreditAmount);

        if (totalDebits != totalCredits)
        {
            return BadRequest(new { message = "Total debits must equal total credits" });
        }

        if (totalDebits == 0)
        {
            return BadRequest(new { message = "Journal entry must have non-zero amounts" });
        }

        // Validate account codes exist
        var accountCodes = request.Lines.Select(l => l.AccountCode).Distinct().ToList();
        var accounts = await _context.Accounts
            .Where(a => a.TenantId == DefaultTenantId && accountCodes.Contains(a.AccountCode))
            .ToDictionaryAsync(a => a.AccountCode, a => a);

        foreach (var accountCode in accountCodes)
        {
            if (!accounts.ContainsKey(accountCode))
            {
                return BadRequest(new { message = $"Account code '{accountCode}' not found" });
            }
        }

        var entryNumber = await _entryNumberGenerator.GenerateAsync(DefaultTenantId, locationId);

        var entry = new JournalEntry
        {
            TenantId = DefaultTenantId,
            LocationId = locationId,
            EntryNumber = entryNumber,
            EntryDate = request.EntryDate,
            PostedAt = DateTime.UtcNow,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            Description = request.Description,
            TotalDebit = totalDebits,
            TotalCredit = totalCredits,
            Currency = request.Currency,
            Status = JournalEntryStatus.Posted,
            FiscalTransactionId = request.FiscalTransactionId
        };

        int lineNumber = 1;
        foreach (var lineRequest in request.Lines)
        {
            var account = accounts[lineRequest.AccountCode];
            var line = new JournalEntryLine
            {
                JournalEntryId = entry.Id,
                AccountCode = lineRequest.AccountCode,
                AccountName = account.Name,
                DebitAmount = lineRequest.DebitAmount,
                CreditAmount = lineRequest.CreditAmount,
                TaxCode = lineRequest.TaxCode,
                TaxAmount = lineRequest.TaxAmount,
                CostCenterId = lineRequest.CostCenterId,
                Description = lineRequest.Description,
                LineNumber = lineNumber++
            };
            entry.Lines.Add(line);
        }

        _context.JournalEntries.Add(entry);
        await _context.SaveChangesAsync();

        var dto = MapToDto(entry);
        dto.AddSelfLink($"/api/locations/{locationId}/journal-entries/{entry.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, id = entry.Id }, dto);
    }

    [HttpPost("{id:guid}/reverse")]
    public async Task<ActionResult<JournalEntryDto>> Reverse(
        Guid locationId,
        Guid id,
        [FromBody] ReverseJournalEntryRequest request)
    {
        var originalEntry = await _context.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == id && e.LocationId == locationId && e.TenantId == DefaultTenantId);

        if (originalEntry == null)
            return NotFound();

        if (originalEntry.Status == JournalEntryStatus.Reversed)
            return BadRequest(new { message = "Journal entry is already reversed" });

        if (originalEntry.ReversedByEntryId.HasValue)
            return BadRequest(new { message = "Journal entry has already been reversed" });

        var entryNumber = await _entryNumberGenerator.GenerateAsync(DefaultTenantId, locationId);

        var reversalEntry = new JournalEntry
        {
            TenantId = DefaultTenantId,
            LocationId = locationId,
            EntryNumber = entryNumber,
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PostedAt = DateTime.UtcNow,
            SourceType = originalEntry.SourceType,
            SourceId = originalEntry.SourceId,
            Description = $"Reversal of {originalEntry.EntryNumber}: {request.Reason}",
            TotalDebit = originalEntry.TotalCredit, // Swap debits and credits
            TotalCredit = originalEntry.TotalDebit,
            Currency = originalEntry.Currency,
            Status = JournalEntryStatus.Posted,
            ReversesEntryId = originalEntry.Id
        };

        int lineNumber = 1;
        foreach (var originalLine in originalEntry.Lines.OrderBy(l => l.LineNumber))
        {
            var line = new JournalEntryLine
            {
                JournalEntryId = reversalEntry.Id,
                AccountCode = originalLine.AccountCode,
                AccountName = originalLine.AccountName,
                DebitAmount = originalLine.CreditAmount, // Swap
                CreditAmount = originalLine.DebitAmount, // Swap
                TaxCode = originalLine.TaxCode,
                TaxAmount = originalLine.TaxAmount.HasValue ? -originalLine.TaxAmount.Value : null,
                CostCenterId = originalLine.CostCenterId,
                Description = $"Reversal: {originalLine.Description}",
                LineNumber = lineNumber++
            };
            reversalEntry.Lines.Add(line);
        }

        originalEntry.Status = JournalEntryStatus.Reversed;
        originalEntry.ReversedByEntryId = reversalEntry.Id;

        _context.JournalEntries.Add(reversalEntry);
        await _context.SaveChangesAsync();

        var dto = MapToDto(reversalEntry);
        dto.AddSelfLink($"/api/locations/{locationId}/journal-entries/{reversalEntry.Id}");
        dto.AddLink("reverses", $"/api/locations/{locationId}/journal-entries/{originalEntry.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, id = reversalEntry.Id }, dto);
    }

    private static JournalEntryDto MapToDto(JournalEntry entry)
    {
        return new JournalEntryDto
        {
            Id = entry.Id,
            TenantId = entry.TenantId,
            LocationId = entry.LocationId,
            EntryNumber = entry.EntryNumber,
            EntryDate = entry.EntryDate,
            PostedAt = entry.PostedAt,
            SourceType = entry.SourceType,
            SourceId = entry.SourceId,
            Description = entry.Description,
            TotalDebit = entry.TotalDebit,
            TotalCredit = entry.TotalCredit,
            Currency = entry.Currency,
            Status = entry.Status,
            ReversedByEntryId = entry.ReversedByEntryId,
            ReversesEntryId = entry.ReversesEntryId,
            FiscalTransactionId = entry.FiscalTransactionId,
            AccountingPeriodId = entry.AccountingPeriodId,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt,
            Lines = entry.Lines.OrderBy(l => l.LineNumber).Select(l => new JournalEntryLineDto
            {
                Id = l.Id,
                JournalEntryId = l.JournalEntryId,
                AccountCode = l.AccountCode,
                AccountName = l.AccountName,
                DebitAmount = l.DebitAmount,
                CreditAmount = l.CreditAmount,
                TaxCode = l.TaxCode,
                TaxAmount = l.TaxAmount,
                CostCenterId = l.CostCenterId,
                Description = l.Description,
                LineNumber = l.LineNumber
            }).ToList()
        };
    }
}
