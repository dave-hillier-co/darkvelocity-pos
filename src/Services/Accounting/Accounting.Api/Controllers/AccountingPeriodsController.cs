using DarkVelocity.Accounting.Api.Data;
using DarkVelocity.Accounting.Api.Dtos;
using DarkVelocity.Accounting.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Accounting.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/accounting-periods")]
public class AccountingPeriodsController : ControllerBase
{
    private readonly AccountingDbContext _context;

    // TODO: In multi-tenant implementation, inject ITenantContext to get TenantId
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public AccountingPeriodsController(AccountingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<AccountingPeriodDto>>> GetAll(
        Guid locationId,
        [FromQuery] PeriodType? periodType = null,
        [FromQuery] PeriodStatus? status = null,
        [FromQuery] int limit = 50)
    {
        var query = _context.AccountingPeriods
            .Where(p => p.LocationId == locationId && p.TenantId == DefaultTenantId);

        if (periodType.HasValue)
        {
            query = query.Where(p => p.PeriodType == periodType.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var periods = await query
            .OrderByDescending(p => p.StartDate)
            .Take(limit)
            .ToListAsync();

        // Get entry counts
        var periodIds = periods.Select(p => p.Id).ToList();
        var entryCounts = await _context.JournalEntries
            .Where(e => e.AccountingPeriodId.HasValue && periodIds.Contains(e.AccountingPeriodId.Value))
            .GroupBy(e => e.AccountingPeriodId)
            .Select(g => new { PeriodId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PeriodId!.Value, x => x.Count);

        var dtos = periods.Select(p => MapToDto(p, entryCounts.GetValueOrDefault(p.Id, 0))).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/locations/{locationId}/accounting-periods/{dto.Id}");
        }

        return Ok(HalCollection<AccountingPeriodDto>.Create(
            dtos,
            $"/api/locations/{locationId}/accounting-periods",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountingPeriodDto>> GetById(Guid locationId, Guid id)
    {
        var period = await _context.AccountingPeriods
            .FirstOrDefaultAsync(p => p.Id == id && p.LocationId == locationId && p.TenantId == DefaultTenantId);

        if (period == null)
            return NotFound();

        var entryCount = await _context.JournalEntries
            .CountAsync(e => e.AccountingPeriodId == id);

        var dto = MapToDto(period, entryCount);
        dto.AddSelfLink($"/api/locations/{locationId}/accounting-periods/{period.Id}");
        dto.AddLink("journal-entries", $"/api/locations/{locationId}/journal-entries?periodId={period.Id}");

        return Ok(dto);
    }

    [HttpGet("current")]
    public async Task<ActionResult<AccountingPeriodDto>> GetCurrent(Guid locationId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var period = await _context.AccountingPeriods
            .FirstOrDefaultAsync(p =>
                p.LocationId == locationId &&
                p.TenantId == DefaultTenantId &&
                p.Status == PeriodStatus.Open &&
                p.StartDate <= today &&
                p.EndDate >= today);

        if (period == null)
            return NotFound(new { message = "No open accounting period found for today" });

        var entryCount = await _context.JournalEntries
            .CountAsync(e => e.AccountingPeriodId == period.Id);

        var dto = MapToDto(period, entryCount);
        dto.AddSelfLink($"/api/locations/{locationId}/accounting-periods/{period.Id}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<AccountingPeriodDto>> Create(
        Guid locationId,
        [FromBody] CreateAccountingPeriodRequest request)
    {
        // Validate dates
        if (request.EndDate < request.StartDate)
        {
            return BadRequest(new { message = "End date must be after start date" });
        }

        // Check for overlapping periods of the same type
        var overlappingPeriod = await _context.AccountingPeriods
            .FirstOrDefaultAsync(p =>
                p.LocationId == locationId &&
                p.TenantId == DefaultTenantId &&
                p.PeriodType == request.PeriodType &&
                p.StartDate <= request.EndDate &&
                p.EndDate >= request.StartDate);

        if (overlappingPeriod != null)
        {
            return BadRequest(new { message = "An overlapping accounting period already exists" });
        }

        var period = new AccountingPeriod
        {
            TenantId = DefaultTenantId,
            LocationId = locationId,
            PeriodType = request.PeriodType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = PeriodStatus.Open,
            Notes = request.Notes
        };

        _context.AccountingPeriods.Add(period);
        await _context.SaveChangesAsync();

        var dto = MapToDto(period, 0);
        dto.AddSelfLink($"/api/locations/{locationId}/accounting-periods/{period.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, id = period.Id }, dto);
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<AccountingPeriodDto>> Close(
        Guid locationId,
        Guid id,
        [FromBody] CloseAccountingPeriodRequest request)
    {
        var period = await _context.AccountingPeriods
            .FirstOrDefaultAsync(p => p.Id == id && p.LocationId == locationId && p.TenantId == DefaultTenantId);

        if (period == null)
            return NotFound();

        if (period.Status == PeriodStatus.Closed || period.Status == PeriodStatus.Locked)
        {
            return BadRequest(new { message = "Period is already closed or locked" });
        }

        period.Status = PeriodStatus.Closed;
        period.ClosedAt = DateTime.UtcNow;
        // period.ClosedByUserId = Get from auth context

        if (!string.IsNullOrEmpty(request.Notes))
        {
            period.Notes = request.Notes;
        }

        await _context.SaveChangesAsync();

        var entryCount = await _context.JournalEntries
            .CountAsync(e => e.AccountingPeriodId == period.Id);

        var dto = MapToDto(period, entryCount);
        dto.AddSelfLink($"/api/locations/{locationId}/accounting-periods/{period.Id}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/lock")]
    public async Task<ActionResult<AccountingPeriodDto>> Lock(
        Guid locationId,
        Guid id,
        [FromBody] LockAccountingPeriodRequest request)
    {
        var period = await _context.AccountingPeriods
            .FirstOrDefaultAsync(p => p.Id == id && p.LocationId == locationId && p.TenantId == DefaultTenantId);

        if (period == null)
            return NotFound();

        if (period.Status == PeriodStatus.Locked)
        {
            return BadRequest(new { message = "Period is already locked" });
        }

        if (period.Status == PeriodStatus.Open)
        {
            return BadRequest(new { message = "Period must be closed before it can be locked" });
        }

        period.Status = PeriodStatus.Locked;

        if (!string.IsNullOrEmpty(request.Notes))
        {
            period.Notes = request.Notes;
        }

        await _context.SaveChangesAsync();

        var entryCount = await _context.JournalEntries
            .CountAsync(e => e.AccountingPeriodId == period.Id);

        var dto = MapToDto(period, entryCount);
        dto.AddSelfLink($"/api/locations/{locationId}/accounting-periods/{period.Id}");

        return Ok(dto);
    }

    private static AccountingPeriodDto MapToDto(AccountingPeriod period, int entryCount)
    {
        return new AccountingPeriodDto
        {
            Id = period.Id,
            TenantId = period.TenantId,
            LocationId = period.LocationId,
            PeriodType = period.PeriodType,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
            Status = period.Status,
            ClosedAt = period.ClosedAt,
            ClosedByUserId = period.ClosedByUserId,
            Notes = period.Notes,
            CreatedAt = period.CreatedAt,
            UpdatedAt = period.UpdatedAt,
            JournalEntryCount = entryCount
        };
    }
}
