using DarkVelocity.Labor.Api.Data;
using DarkVelocity.Labor.Api.Dtos;
using DarkVelocity.Labor.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Labor.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/tip-pool-rules")]
public class TipPoolRulesController : ControllerBase
{
    private readonly LaborDbContext _context;

    public TipPoolRulesController(LaborDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// List tip pool rules for a location.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HalCollection<TipPoolRuleDto>>> GetAll(
        Guid locationId,
        [FromQuery] bool? isActive = null)
    {
        var query = _context.TipPoolRules
            .Include(r => r.Role)
            .Where(r => r.LocationId == locationId);

        if (isActive.HasValue)
            query = query.Where(r => r.IsActive == isActive.Value);

        var rules = await query.ToListAsync();

        var dtos = rules.Select(r => new TipPoolRuleDto
        {
            Id = r.Id,
            TenantId = r.TenantId,
            LocationId = r.LocationId,
            RoleId = r.RoleId,
            RoleName = r.Role?.Name ?? string.Empty,
            PoolSharePercentage = r.PoolSharePercentage,
            DistributionWeight = r.DistributionWeight,
            MinimumHoursToQualify = r.MinimumHoursToQualify,
            IsActive = r.IsActive
        }).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/locations/{locationId}/tip-pool-rules/{dto.Id}");
            dto.AddLink("role", $"/api/roles/{dto.RoleId}");
        }

        return Ok(HalCollection<TipPoolRuleDto>.Create(dtos, $"/api/locations/{locationId}/tip-pool-rules", dtos.Count));
    }

    /// <summary>
    /// Create a tip pool rule.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TipPoolRuleDto>> Create(
        Guid locationId,
        [FromQuery] Guid tenantId,
        [FromBody] CreateTipPoolRuleRequest request)
    {
        // Check if rule already exists for this role
        var existing = await _context.TipPoolRules
            .AnyAsync(r => r.LocationId == locationId && r.RoleId == request.RoleId);

        if (existing)
            return BadRequest(new { message = "Rule already exists for this role" });

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId);
        if (role == null)
            return BadRequest(new { message = "Role not found" });

        var rule = new TipPoolRule
        {
            TenantId = tenantId,
            LocationId = locationId,
            RoleId = request.RoleId,
            PoolSharePercentage = request.PoolSharePercentage,
            DistributionWeight = request.DistributionWeight,
            MinimumHoursToQualify = request.MinimumHoursToQualify
        };

        _context.TipPoolRules.Add(rule);
        await _context.SaveChangesAsync();

        var dto = new TipPoolRuleDto
        {
            Id = rule.Id,
            TenantId = rule.TenantId,
            LocationId = rule.LocationId,
            RoleId = rule.RoleId,
            RoleName = role.Name,
            PoolSharePercentage = rule.PoolSharePercentage,
            DistributionWeight = rule.DistributionWeight,
            MinimumHoursToQualify = rule.MinimumHoursToQualify,
            IsActive = rule.IsActive
        };

        dto.AddSelfLink($"/api/locations/{locationId}/tip-pool-rules/{rule.Id}");
        dto.AddLink("role", $"/api/roles/{rule.RoleId}");

        return CreatedAtAction(nameof(GetAll), new { locationId }, dto);
    }

    /// <summary>
    /// Update a tip pool rule.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TipPoolRuleDto>> Update(
        Guid locationId,
        Guid id,
        [FromBody] UpdateTipPoolRuleRequest request)
    {
        var rule = await _context.TipPoolRules
            .Include(r => r.Role)
            .FirstOrDefaultAsync(r => r.Id == id && r.LocationId == locationId);

        if (rule == null)
            return NotFound();

        if (request.PoolSharePercentage.HasValue) rule.PoolSharePercentage = request.PoolSharePercentage.Value;
        if (request.DistributionWeight.HasValue) rule.DistributionWeight = request.DistributionWeight.Value;
        if (request.MinimumHoursToQualify.HasValue) rule.MinimumHoursToQualify = request.MinimumHoursToQualify;
        if (request.IsActive.HasValue) rule.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        var dto = new TipPoolRuleDto
        {
            Id = rule.Id,
            TenantId = rule.TenantId,
            LocationId = rule.LocationId,
            RoleId = rule.RoleId,
            RoleName = rule.Role?.Name ?? string.Empty,
            PoolSharePercentage = rule.PoolSharePercentage,
            DistributionWeight = rule.DistributionWeight,
            MinimumHoursToQualify = rule.MinimumHoursToQualify,
            IsActive = rule.IsActive
        };

        dto.AddSelfLink($"/api/locations/{locationId}/tip-pool-rules/{rule.Id}");
        dto.AddLink("role", $"/api/roles/{rule.RoleId}");

        return Ok(dto);
    }

    /// <summary>
    /// Delete a tip pool rule.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid id)
    {
        var rule = await _context.TipPoolRules
            .FirstOrDefaultAsync(r => r.Id == id && r.LocationId == locationId);

        if (rule == null)
            return NotFound();

        _context.TipPoolRules.Remove(rule);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
