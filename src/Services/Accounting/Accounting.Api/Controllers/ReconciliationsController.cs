using DarkVelocity.Accounting.Api.Data;
using DarkVelocity.Accounting.Api.Dtos;
using DarkVelocity.Accounting.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Accounting.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/reconciliations")]
public class ReconciliationsController : ControllerBase
{
    private readonly AccountingDbContext _context;

    // TODO: In multi-tenant implementation, inject ITenantContext to get TenantId
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public ReconciliationsController(AccountingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<ReconciliationDto>>> GetAll(
        Guid locationId,
        [FromQuery] ReconciliationType? reconciliationType = null,
        [FromQuery] ReconciliationStatus? status = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        [FromQuery] int limit = 50)
    {
        var query = _context.Reconciliations
            .Where(r => r.LocationId == locationId && r.TenantId == DefaultTenantId);

        if (reconciliationType.HasValue)
        {
            query = query.Where(r => r.ReconciliationType == reconciliationType.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(r => r.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(r => r.Date <= endDate.Value);
        }

        var reconciliations = await query
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToListAsync();

        var dtos = reconciliations.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/locations/{locationId}/reconciliations/{dto.Id}");
        }

        return Ok(HalCollection<ReconciliationDto>.Create(
            dtos,
            $"/api/locations/{locationId}/reconciliations",
            dtos.Count
        ));
    }

    [HttpGet("pending")]
    public async Task<ActionResult<HalCollection<ReconciliationDto>>> GetPending(Guid locationId)
    {
        var reconciliations = await _context.Reconciliations
            .Where(r =>
                r.LocationId == locationId &&
                r.TenantId == DefaultTenantId &&
                (r.Status == ReconciliationStatus.Pending || r.Status == ReconciliationStatus.Variance))
            .OrderByDescending(r => r.Date)
            .ToListAsync();

        var dtos = reconciliations.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/locations/{locationId}/reconciliations/{dto.Id}");
        }

        return Ok(HalCollection<ReconciliationDto>.Create(
            dtos,
            $"/api/locations/{locationId}/reconciliations/pending",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReconciliationDto>> GetById(Guid locationId, Guid id)
    {
        var reconciliation = await _context.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == id && r.LocationId == locationId && r.TenantId == DefaultTenantId);

        if (reconciliation == null)
            return NotFound();

        var dto = MapToDto(reconciliation);
        dto.AddSelfLink($"/api/locations/{locationId}/reconciliations/{reconciliation.Id}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<ReconciliationDto>> Create(
        Guid locationId,
        [FromBody] CreateReconciliationRequest request)
    {
        var variance = request.ActualAmount - request.ExpectedAmount;

        var reconciliation = new Reconciliation
        {
            TenantId = DefaultTenantId,
            LocationId = locationId,
            ReconciliationType = request.ReconciliationType,
            Date = request.Date,
            ExpectedAmount = request.ExpectedAmount,
            ActualAmount = request.ActualAmount,
            Variance = variance,
            Status = variance == 0 ? ReconciliationStatus.Matched : ReconciliationStatus.Variance,
            Currency = request.Currency,
            ExternalReference = request.ExternalReference
        };

        _context.Reconciliations.Add(reconciliation);
        await _context.SaveChangesAsync();

        var dto = MapToDto(reconciliation);
        dto.AddSelfLink($"/api/locations/{locationId}/reconciliations/{reconciliation.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, id = reconciliation.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReconciliationDto>> Update(
        Guid locationId,
        Guid id,
        [FromBody] UpdateReconciliationRequest request)
    {
        var reconciliation = await _context.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == id && r.LocationId == locationId && r.TenantId == DefaultTenantId);

        if (reconciliation == null)
            return NotFound();

        if (reconciliation.Status == ReconciliationStatus.Resolved)
        {
            return BadRequest(new { message = "Cannot modify a resolved reconciliation" });
        }

        if (request.ActualAmount.HasValue)
        {
            reconciliation.ActualAmount = request.ActualAmount.Value;
            reconciliation.Variance = reconciliation.ActualAmount - reconciliation.ExpectedAmount;
            reconciliation.Status = reconciliation.Variance == 0
                ? ReconciliationStatus.Matched
                : ReconciliationStatus.Variance;
        }

        if (request.ExternalReference != null)
        {
            reconciliation.ExternalReference = request.ExternalReference;
        }

        await _context.SaveChangesAsync();

        var dto = MapToDto(reconciliation);
        dto.AddSelfLink($"/api/locations/{locationId}/reconciliations/{reconciliation.Id}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult<ReconciliationDto>> Resolve(
        Guid locationId,
        Guid id,
        [FromBody] ResolveReconciliationRequest request)
    {
        var reconciliation = await _context.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == id && r.LocationId == locationId && r.TenantId == DefaultTenantId);

        if (reconciliation == null)
            return NotFound();

        if (reconciliation.Status == ReconciliationStatus.Resolved)
        {
            return BadRequest(new { message = "Reconciliation is already resolved" });
        }

        reconciliation.Status = ReconciliationStatus.Resolved;
        reconciliation.ResolvedAt = DateTime.UtcNow;
        // reconciliation.ResolvedByUserId = Get from auth context
        reconciliation.ResolutionNotes = request.ResolutionNotes;

        await _context.SaveChangesAsync();

        var dto = MapToDto(reconciliation);
        dto.AddSelfLink($"/api/locations/{locationId}/reconciliations/{reconciliation.Id}");

        return Ok(dto);
    }

    private static ReconciliationDto MapToDto(Reconciliation reconciliation)
    {
        return new ReconciliationDto
        {
            Id = reconciliation.Id,
            TenantId = reconciliation.TenantId,
            LocationId = reconciliation.LocationId,
            ReconciliationType = reconciliation.ReconciliationType,
            Date = reconciliation.Date,
            ExpectedAmount = reconciliation.ExpectedAmount,
            ActualAmount = reconciliation.ActualAmount,
            Variance = reconciliation.Variance,
            Status = reconciliation.Status,
            ResolvedAt = reconciliation.ResolvedAt,
            ResolvedByUserId = reconciliation.ResolvedByUserId,
            ResolutionNotes = reconciliation.ResolutionNotes,
            Currency = reconciliation.Currency,
            ExternalReference = reconciliation.ExternalReference,
            CreatedAt = reconciliation.CreatedAt,
            UpdatedAt = reconciliation.UpdatedAt
        };
    }
}
