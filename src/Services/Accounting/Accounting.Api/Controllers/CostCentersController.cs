using DarkVelocity.Accounting.Api.Data;
using DarkVelocity.Accounting.Api.Dtos;
using DarkVelocity.Accounting.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Accounting.Api.Controllers;

[ApiController]
[Route("api/cost-centers")]
public class CostCentersController : ControllerBase
{
    private readonly AccountingDbContext _context;

    // TODO: In multi-tenant implementation, inject ITenantContext to get TenantId
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public CostCentersController(AccountingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<CostCenterDto>>> GetAll(
        [FromQuery] Guid? locationId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int limit = 100)
    {
        var query = _context.CostCenters
            .Where(c => c.TenantId == DefaultTenantId);

        if (locationId.HasValue)
        {
            query = query.Where(c => c.LocationId == locationId.Value || c.LocationId == null);
        }

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        var costCenters = await query
            .OrderBy(c => c.Code)
            .Take(limit)
            .ToListAsync();

        var dtos = costCenters.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/cost-centers/{dto.Id}");
        }

        return Ok(HalCollection<CostCenterDto>.Create(
            dtos,
            "/api/cost-centers",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CostCenterDto>> GetById(Guid id)
    {
        var costCenter = await _context.CostCenters
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == DefaultTenantId);

        if (costCenter == null)
            return NotFound();

        var dto = MapToDto(costCenter);
        dto.AddSelfLink($"/api/cost-centers/{costCenter.Id}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<CostCenterDto>> Create([FromBody] CreateCostCenterRequest request)
    {
        // Validate unique code
        var existingCostCenter = await _context.CostCenters
            .FirstOrDefaultAsync(c => c.TenantId == DefaultTenantId && c.Code == request.Code);

        if (existingCostCenter != null)
            return BadRequest(new { message = "Cost center code already exists" });

        var costCenter = new CostCenter
        {
            TenantId = DefaultTenantId,
            Code = request.Code,
            Name = request.Name,
            LocationId = request.LocationId,
            Description = request.Description,
            IsActive = true
        };

        _context.CostCenters.Add(costCenter);
        await _context.SaveChangesAsync();

        var dto = MapToDto(costCenter);
        dto.AddSelfLink($"/api/cost-centers/{costCenter.Id}");

        return CreatedAtAction(nameof(GetById), new { id = costCenter.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CostCenterDto>> Update(Guid id, [FromBody] UpdateCostCenterRequest request)
    {
        var costCenter = await _context.CostCenters
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == DefaultTenantId);

        if (costCenter == null)
            return NotFound();

        if (!string.IsNullOrEmpty(request.Name))
        {
            costCenter.Name = request.Name;
        }

        if (request.LocationId.HasValue)
        {
            costCenter.LocationId = request.LocationId;
        }

        if (request.IsActive.HasValue)
        {
            costCenter.IsActive = request.IsActive.Value;
        }

        if (request.Description != null)
        {
            costCenter.Description = request.Description;
        }

        await _context.SaveChangesAsync();

        var dto = MapToDto(costCenter);
        dto.AddSelfLink($"/api/cost-centers/{costCenter.Id}");

        return Ok(dto);
    }

    private static CostCenterDto MapToDto(CostCenter costCenter)
    {
        return new CostCenterDto
        {
            Id = costCenter.Id,
            TenantId = costCenter.TenantId,
            Code = costCenter.Code,
            Name = costCenter.Name,
            LocationId = costCenter.LocationId,
            IsActive = costCenter.IsActive,
            Description = costCenter.Description,
            CreatedAt = costCenter.CreatedAt,
            UpdatedAt = costCenter.UpdatedAt
        };
    }
}
