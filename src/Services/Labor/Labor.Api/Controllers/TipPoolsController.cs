using DarkVelocity.Labor.Api.Data;
using DarkVelocity.Labor.Api.Dtos;
using DarkVelocity.Labor.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Labor.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/tip-pools")]
public class TipPoolsController : ControllerBase
{
    private readonly LaborDbContext _context;

    public TipPoolsController(LaborDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// List tip pools for a location.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HalCollection<TipPoolSummaryDto>>> GetAll(
        Guid locationId,
        [FromQuery] string? status = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] int limit = 30,
        [FromQuery] int offset = 0)
    {
        var query = _context.TipPools
            .Include(t => t.Distributions)
            .Where(t => t.LocationId == locationId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        if (fromDate.HasValue)
            query = query.Where(t => t.Date >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.Date <= toDate.Value);

        var total = await query.CountAsync();

        var pools = await query
            .OrderByDescending(t => t.Date)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = pools.Select(p => new TipPoolSummaryDto
        {
            Id = p.Id,
            LocationId = p.LocationId,
            Date = p.Date,
            TotalTips = p.TotalTips,
            DistributionMethod = p.DistributionMethod,
            Status = p.Status,
            DistributionCount = p.Distributions.Count
        }).ToList();

        foreach (var dto in dtos)
            dto.AddSelfLink($"/api/locations/{locationId}/tip-pools/{dto.Id}");

        return Ok(HalCollection<TipPoolSummaryDto>.Create(dtos, $"/api/locations/{locationId}/tip-pools", total));
    }

    /// <summary>
    /// Get tip pool by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TipPoolDto>> GetById(Guid locationId, Guid id)
    {
        var pool = await _context.TipPools
            .Include(t => t.Distributions)
                .ThenInclude(d => d.Employee)
            .Include(t => t.Distributions)
                .ThenInclude(d => d.Role)
            .FirstOrDefaultAsync(t => t.Id == id && t.LocationId == locationId);

        if (pool == null)
            return NotFound();

        var dto = MapToDto(pool, locationId);
        AddLinks(dto, locationId);
        AddActionLinks(dto, locationId, pool);
        return Ok(dto);
    }

    /// <summary>
    /// Create a tip pool.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TipPoolDto>> Create(
        Guid locationId,
        [FromQuery] Guid tenantId,
        [FromBody] CreateTipPoolRequest request)
    {
        // Check if pool already exists for this date
        var existing = await _context.TipPools
            .AnyAsync(t => t.LocationId == locationId && t.Date == request.Date);

        if (existing)
            return BadRequest(new { message = "Tip pool already exists for this date" });

        var pool = new TipPool
        {
            TenantId = tenantId,
            LocationId = locationId,
            Date = request.Date,
            TotalTips = request.TotalTips,
            DistributionMethod = request.DistributionMethod,
            SalesPeriodId = request.SalesPeriodId,
            Notes = request.Notes,
            Status = "pending"
        };

        _context.TipPools.Add(pool);
        await _context.SaveChangesAsync();

        var dto = MapToDto(pool, locationId);
        AddLinks(dto, locationId);
        AddActionLinks(dto, locationId, pool);
        return CreatedAtAction(nameof(GetById), new { locationId, id = pool.Id }, dto);
    }

    /// <summary>
    /// Update a tip pool.
    /// </summary>
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<TipPoolDto>> Update(
        Guid locationId,
        Guid id,
        [FromBody] UpdateTipPoolRequest request)
    {
        var pool = await _context.TipPools
            .Include(t => t.Distributions)
            .FirstOrDefaultAsync(t => t.Id == id && t.LocationId == locationId);

        if (pool == null)
            return NotFound();

        if (pool.Status == "locked")
            return BadRequest(new { message = "Cannot modify locked tip pool" });

        if (request.TotalTips.HasValue) pool.TotalTips = request.TotalTips.Value;
        if (request.DistributionMethod != null) pool.DistributionMethod = request.DistributionMethod;
        if (request.Notes != null) pool.Notes = request.Notes;

        await _context.SaveChangesAsync();

        var dto = MapToDto(pool, locationId);
        AddLinks(dto, locationId);
        AddActionLinks(dto, locationId, pool);
        return Ok(dto);
    }

    /// <summary>
    /// Calculate tip distribution for a pool.
    /// </summary>
    [HttpPost("{id:guid}/calculate")]
    public async Task<ActionResult<TipPoolDto>> Calculate(Guid locationId, Guid id)
    {
        var pool = await _context.TipPools
            .Include(t => t.Distributions)
            .FirstOrDefaultAsync(t => t.Id == id && t.LocationId == locationId);

        if (pool == null)
            return NotFound();

        if (pool.Status != "pending")
            return BadRequest(new { message = "Pool must be pending to calculate" });

        // Get time entries for this date and location
        var timeEntries = await _context.TimeEntries
            .Include(t => t.Employee)
            .Include(t => t.Role)
            .Where(t =>
                t.LocationId == locationId &&
                DateOnly.FromDateTime(t.ClockInAt) == pool.Date &&
                t.ClockOutAt != null &&
                t.Status != "disputed")
            .ToListAsync();

        // Get tip pool rules
        var rules = await _context.TipPoolRules
            .Where(r => r.LocationId == locationId && r.IsActive)
            .ToDictionaryAsync(r => r.RoleId);

        // Clear existing distributions
        _context.TipDistributions.RemoveRange(pool.Distributions);

        // Calculate based on distribution method
        var distributions = new List<TipDistribution>();
        var totalHours = timeEntries.Sum(t => t.ActualHours);

        foreach (var entry in timeEntries)
        {
            var weight = rules.TryGetValue(entry.RoleId, out var rule)
                ? rule.DistributionWeight
                : 1.0m;

            var tipShare = pool.DistributionMethod switch
            {
                "equal" => pool.TotalTips / timeEntries.Count,
                "hours" => totalHours > 0 ? pool.TotalTips * (entry.ActualHours / totalHours) : 0,
                "points" => pool.TotalTips * (entry.ActualHours * weight) /
                           timeEntries.Sum(e => e.ActualHours * (rules.TryGetValue(e.RoleId, out var r) ? r.DistributionWeight : 1.0m)),
                _ => 0
            };

            var existingDist = distributions.FirstOrDefault(d => d.EmployeeId == entry.EmployeeId);
            if (existingDist != null)
            {
                existingDist.HoursWorked += entry.ActualHours;
                existingDist.TipShare += tipShare;
            }
            else
            {
                distributions.Add(new TipDistribution
                {
                    TipPoolId = id,
                    EmployeeId = entry.EmployeeId,
                    RoleId = entry.RoleId,
                    HoursWorked = entry.ActualHours,
                    TipShare = tipShare,
                    TipPercentage = pool.TotalTips > 0 ? (tipShare / pool.TotalTips) * 100 : 0,
                    Status = "calculated"
                });
            }
        }

        // Recalculate percentages
        foreach (var dist in distributions)
        {
            dist.TipPercentage = pool.TotalTips > 0 ? (dist.TipShare / pool.TotalTips) * 100 : 0;
        }

        _context.TipDistributions.AddRange(distributions);
        pool.Status = "calculated";
        pool.CalculatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Reload with navigation properties
        pool = await _context.TipPools
            .Include(t => t.Distributions)
                .ThenInclude(d => d.Employee)
            .Include(t => t.Distributions)
                .ThenInclude(d => d.Role)
            .FirstAsync(t => t.Id == id);

        var dto = MapToDto(pool, locationId);
        AddLinks(dto, locationId);
        AddActionLinks(dto, locationId, pool);
        return Ok(dto);
    }

    /// <summary>
    /// Distribute (finalize) a tip pool.
    /// </summary>
    [HttpPost("{id:guid}/distribute")]
    public async Task<ActionResult<TipPoolDto>> Distribute(
        Guid locationId,
        Guid id,
        [FromQuery] Guid userId)
    {
        var pool = await _context.TipPools
            .Include(t => t.Distributions)
                .ThenInclude(d => d.Employee)
            .Include(t => t.Distributions)
                .ThenInclude(d => d.Role)
            .FirstOrDefaultAsync(t => t.Id == id && t.LocationId == locationId);

        if (pool == null)
            return NotFound();

        if (pool.Status != "calculated")
            return BadRequest(new { message = "Pool must be calculated before distribution" });

        pool.Status = "distributed";
        pool.DistributedAt = DateTime.UtcNow;
        pool.DistributedByUserId = userId;

        foreach (var dist in pool.Distributions)
        {
            dist.Status = "approved";
        }

        await _context.SaveChangesAsync();

        var dto = MapToDto(pool, locationId);
        AddLinks(dto, locationId);
        return Ok(dto);
    }

    /// <summary>
    /// Get distributions for a tip pool.
    /// </summary>
    [HttpGet("{id:guid}/distributions")]
    public async Task<ActionResult<HalCollection<TipDistributionDto>>> GetDistributions(
        Guid locationId,
        Guid id)
    {
        var pool = await _context.TipPools
            .FirstOrDefaultAsync(t => t.Id == id && t.LocationId == locationId);

        if (pool == null)
            return NotFound();

        var distributions = await _context.TipDistributions
            .Include(d => d.Employee)
            .Include(d => d.Role)
            .Where(d => d.TipPoolId == id)
            .OrderByDescending(d => d.TipShare)
            .ToListAsync();

        var dtos = distributions.Select(d => new TipDistributionDto
        {
            Id = d.Id,
            TipPoolId = d.TipPoolId,
            EmployeeId = d.EmployeeId,
            EmployeeName = d.Employee != null ? $"{d.Employee.FirstName} {d.Employee.LastName}" : string.Empty,
            RoleId = d.RoleId,
            RoleName = d.Role?.Name ?? string.Empty,
            HoursWorked = d.HoursWorked,
            PointsEarned = d.PointsEarned,
            TipShare = d.TipShare,
            TipPercentage = d.TipPercentage,
            DeclaredTips = d.DeclaredTips,
            Status = d.Status,
            PaidAt = d.PaidAt
        }).ToList();

        foreach (var dto in dtos)
            dto.AddSelfLink($"/api/locations/{locationId}/tip-pools/{id}/distributions/{dto.Id}");

        return Ok(HalCollection<TipDistributionDto>.Create(dtos, $"/api/locations/{locationId}/tip-pools/{id}/distributions", dtos.Count));
    }

    private static TipPoolDto MapToDto(TipPool pool, Guid locationId)
    {
        return new TipPoolDto
        {
            Id = pool.Id,
            TenantId = pool.TenantId,
            LocationId = pool.LocationId,
            Date = pool.Date,
            SalesPeriodId = pool.SalesPeriodId,
            TotalTips = pool.TotalTips,
            DistributionMethod = pool.DistributionMethod,
            Status = pool.Status,
            CalculatedAt = pool.CalculatedAt,
            DistributedAt = pool.DistributedAt,
            DistributedByUserId = pool.DistributedByUserId,
            Notes = pool.Notes,
            DistributionCount = pool.Distributions.Count,
            Distributions = pool.Distributions.Select(d => new TipDistributionDto
            {
                Id = d.Id,
                TipPoolId = d.TipPoolId,
                EmployeeId = d.EmployeeId,
                EmployeeName = d.Employee != null ? $"{d.Employee.FirstName} {d.Employee.LastName}" : string.Empty,
                RoleId = d.RoleId,
                RoleName = d.Role?.Name ?? string.Empty,
                HoursWorked = d.HoursWorked,
                PointsEarned = d.PointsEarned,
                TipShare = d.TipShare,
                TipPercentage = d.TipPercentage,
                DeclaredTips = d.DeclaredTips,
                Status = d.Status,
                PaidAt = d.PaidAt
            }).ToList(),
            CreatedAt = pool.CreatedAt,
            UpdatedAt = pool.UpdatedAt
        };
    }

    private static void AddLinks(TipPoolDto dto, Guid locationId)
    {
        dto.AddSelfLink($"/api/locations/{locationId}/tip-pools/{dto.Id}");
        dto.AddLink("distributions", $"/api/locations/{locationId}/tip-pools/{dto.Id}/distributions");
    }

    private static void AddActionLinks(TipPoolDto dto, Guid locationId, TipPool pool)
    {
        var baseUrl = $"/api/locations/{locationId}/tip-pools/{dto.Id}";

        switch (pool.Status)
        {
            case "pending":
                dto.AddLink("calculate", $"{baseUrl}/calculate");
                break;
            case "calculated":
                dto.AddLink("distribute", $"{baseUrl}/distribute");
                dto.AddLink("recalculate", $"{baseUrl}/calculate");
                break;
        }
    }
}
