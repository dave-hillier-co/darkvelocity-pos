using DarkVelocity.Customers.Api.Data;
using DarkVelocity.Customers.Api.Dtos;
using DarkVelocity.Customers.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Customers.Api.Controllers;

[ApiController]
[Route("api/loyalty-programs")]
public class LoyaltyProgramsController : ControllerBase
{
    private readonly CustomersDbContext _context;

    private Guid TenantId => Guid.Parse(Request.Headers["X-Tenant-Id"].FirstOrDefault()
        ?? "00000000-0000-0000-0000-000000000001");

    public LoyaltyProgramsController(CustomersDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<LoyaltyProgramSummaryDto>>> GetAll(
        [FromQuery] string? status = null)
    {
        var query = _context.LoyaltyPrograms
            .Where(p => p.TenantId == TenantId);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(p => p.Status == status);
        }

        var programs = await query
            .OrderBy(p => p.Name)
            .ToListAsync();

        var memberCounts = await _context.CustomerLoyalties
            .Where(l => programs.Select(p => p.Id).Contains(l.ProgramId))
            .GroupBy(l => l.ProgramId)
            .Select(g => new { ProgramId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProgramId, x => x.Count);

        var dtos = programs.Select(p => new LoyaltyProgramSummaryDto
        {
            Id = p.Id,
            Name = p.Name,
            Type = p.Type,
            Status = p.Status,
            MemberCount = memberCounts.GetValueOrDefault(p.Id, 0)
        }).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/loyalty-programs/{dto.Id}");
        }

        return Ok(HalCollection<LoyaltyProgramSummaryDto>.Create(
            dtos,
            "/api/loyalty-programs",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LoyaltyProgramDto>> GetById(Guid id)
    {
        var program = await _context.LoyaltyPrograms
            .Include(p => p.Tiers.OrderBy(t => t.SortOrder))
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId);

        if (program == null)
            return NotFound();

        var dto = MapToDto(program);
        dto.AddSelfLink($"/api/loyalty-programs/{program.Id}");
        dto.AddLink("tiers", $"/api/loyalty-programs/{program.Id}/tiers");
        dto.AddLink("rewards", $"/api/loyalty-programs/{program.Id}/rewards");
        dto.AddLink("analytics", $"/api/loyalty-programs/{program.Id}/analytics");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<LoyaltyProgramDto>> Create([FromBody] CreateLoyaltyProgramRequest request)
    {
        var program = new LoyaltyProgram
        {
            TenantId = TenantId,
            Name = request.Name,
            Type = request.Type,
            Status = "active",
            PointsPerCurrencyUnit = request.PointsPerCurrencyUnit,
            PointsValueInCurrency = request.PointsValueInCurrency,
            MinimumRedemption = request.MinimumRedemption,
            PointsExpireAfterDays = request.PointsExpireAfterDays,
            WelcomeBonus = request.WelcomeBonus,
            BirthdayBonus = request.BirthdayBonus,
            ReferralBonus = request.ReferralBonus,
            TermsAndConditions = request.TermsAndConditions,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        _context.LoyaltyPrograms.Add(program);
        await _context.SaveChangesAsync();

        var dto = MapToDto(program);
        dto.AddSelfLink($"/api/loyalty-programs/{program.Id}");

        return CreatedAtAction(nameof(GetById), new { id = program.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LoyaltyProgramDto>> Update(Guid id, [FromBody] UpdateLoyaltyProgramRequest request)
    {
        var program = await _context.LoyaltyPrograms
            .Include(p => p.Tiers.OrderBy(t => t.SortOrder))
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId);

        if (program == null)
            return NotFound();

        if (request.Name != null) program.Name = request.Name;
        if (request.Status != null) program.Status = request.Status;
        if (request.PointsPerCurrencyUnit.HasValue) program.PointsPerCurrencyUnit = request.PointsPerCurrencyUnit.Value;
        if (request.PointsValueInCurrency.HasValue) program.PointsValueInCurrency = request.PointsValueInCurrency.Value;
        if (request.MinimumRedemption.HasValue) program.MinimumRedemption = request.MinimumRedemption.Value;
        if (request.PointsExpireAfterDays.HasValue) program.PointsExpireAfterDays = request.PointsExpireAfterDays.Value;
        if (request.WelcomeBonus.HasValue) program.WelcomeBonus = request.WelcomeBonus.Value;
        if (request.BirthdayBonus.HasValue) program.BirthdayBonus = request.BirthdayBonus.Value;
        if (request.ReferralBonus.HasValue) program.ReferralBonus = request.ReferralBonus.Value;
        if (request.TermsAndConditions != null) program.TermsAndConditions = request.TermsAndConditions;
        if (request.EndDate.HasValue) program.EndDate = request.EndDate.Value;

        await _context.SaveChangesAsync();

        var dto = MapToDto(program);
        dto.AddSelfLink($"/api/loyalty-programs/{program.Id}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/pause")]
    public async Task<ActionResult<LoyaltyProgramDto>> Pause(Guid id)
    {
        var program = await _context.LoyaltyPrograms
            .Include(p => p.Tiers.OrderBy(t => t.SortOrder))
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId);

        if (program == null)
            return NotFound();

        if (program.Status != "active")
            return BadRequest(new { message = "Only active programs can be paused" });

        program.Status = "paused";
        await _context.SaveChangesAsync();

        var dto = MapToDto(program);
        dto.AddSelfLink($"/api/loyalty-programs/{program.Id}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<ActionResult<LoyaltyProgramDto>> Resume(Guid id)
    {
        var program = await _context.LoyaltyPrograms
            .Include(p => p.Tiers.OrderBy(t => t.SortOrder))
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId);

        if (program == null)
            return NotFound();

        if (program.Status != "paused")
            return BadRequest(new { message = "Only paused programs can be resumed" });

        program.Status = "active";
        await _context.SaveChangesAsync();

        var dto = MapToDto(program);
        dto.AddSelfLink($"/api/loyalty-programs/{program.Id}");

        return Ok(dto);
    }

    // Tiers endpoints
    [HttpGet("{programId:guid}/tiers")]
    public async Task<ActionResult<HalCollection<LoyaltyTierDto>>> GetTiers(Guid programId)
    {
        var program = await _context.LoyaltyPrograms
            .FirstOrDefaultAsync(p => p.Id == programId && p.TenantId == TenantId);

        if (program == null)
            return NotFound();

        var tiers = await _context.LoyaltyTiers
            .Where(t => t.ProgramId == programId)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        var dtos = tiers.Select(t => MapTierToDto(t)).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/loyalty-programs/{programId}/tiers/{dto.Id}");
        }

        return Ok(HalCollection<LoyaltyTierDto>.Create(
            dtos,
            $"/api/loyalty-programs/{programId}/tiers",
            dtos.Count
        ));
    }

    [HttpPost("{programId:guid}/tiers")]
    public async Task<ActionResult<LoyaltyTierDto>> CreateTier(Guid programId, [FromBody] CreateLoyaltyTierRequest request)
    {
        var program = await _context.LoyaltyPrograms
            .FirstOrDefaultAsync(p => p.Id == programId && p.TenantId == TenantId);

        if (program == null)
            return NotFound();

        var tier = new LoyaltyTier
        {
            ProgramId = programId,
            Name = request.Name,
            MinimumPoints = request.MinimumPoints,
            PointsMultiplier = request.PointsMultiplier,
            FreeDelivery = request.FreeDelivery,
            PriorityBooking = request.PriorityBooking,
            ExclusiveOffers = request.ExclusiveOffers,
            BirthdayReward = request.BirthdayReward,
            Color = request.Color,
            IconUrl = request.IconUrl,
            SortOrder = request.SortOrder
        };

        _context.LoyaltyTiers.Add(tier);
        await _context.SaveChangesAsync();

        var dto = MapTierToDto(tier);
        dto.AddSelfLink($"/api/loyalty-programs/{programId}/tiers/{tier.Id}");

        return CreatedAtAction(nameof(GetTiers), new { programId }, dto);
    }

    [HttpPut("{programId:guid}/tiers/{tierId:guid}")]
    public async Task<ActionResult<LoyaltyTierDto>> UpdateTier(
        Guid programId,
        Guid tierId,
        [FromBody] UpdateLoyaltyTierRequest request)
    {
        var program = await _context.LoyaltyPrograms
            .FirstOrDefaultAsync(p => p.Id == programId && p.TenantId == TenantId);

        if (program == null)
            return NotFound();

        var tier = await _context.LoyaltyTiers
            .FirstOrDefaultAsync(t => t.Id == tierId && t.ProgramId == programId);

        if (tier == null)
            return NotFound();

        if (request.Name != null) tier.Name = request.Name;
        if (request.MinimumPoints.HasValue) tier.MinimumPoints = request.MinimumPoints.Value;
        if (request.PointsMultiplier.HasValue) tier.PointsMultiplier = request.PointsMultiplier.Value;
        if (request.FreeDelivery.HasValue) tier.FreeDelivery = request.FreeDelivery.Value;
        if (request.PriorityBooking.HasValue) tier.PriorityBooking = request.PriorityBooking.Value;
        if (request.ExclusiveOffers.HasValue) tier.ExclusiveOffers = request.ExclusiveOffers.Value;
        if (request.BirthdayReward != null) tier.BirthdayReward = request.BirthdayReward;
        if (request.Color != null) tier.Color = request.Color;
        if (request.IconUrl != null) tier.IconUrl = request.IconUrl;
        if (request.SortOrder.HasValue) tier.SortOrder = request.SortOrder.Value;

        await _context.SaveChangesAsync();

        var dto = MapTierToDto(tier);
        dto.AddSelfLink($"/api/loyalty-programs/{programId}/tiers/{tier.Id}");

        return Ok(dto);
    }

    [HttpDelete("{programId:guid}/tiers/{tierId:guid}")]
    public async Task<IActionResult> DeleteTier(Guid programId, Guid tierId)
    {
        var program = await _context.LoyaltyPrograms
            .FirstOrDefaultAsync(p => p.Id == programId && p.TenantId == TenantId);

        if (program == null)
            return NotFound();

        var tier = await _context.LoyaltyTiers
            .FirstOrDefaultAsync(t => t.Id == tierId && t.ProgramId == programId);

        if (tier == null)
            return NotFound();

        // Check if any members are on this tier
        var membersOnTier = await _context.CustomerLoyalties
            .AnyAsync(l => l.CurrentTierId == tierId);

        if (membersOnTier)
            return BadRequest(new { message = "Cannot delete tier with active members" });

        _context.LoyaltyTiers.Remove(tier);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id:guid}/analytics")]
    public async Task<ActionResult<object>> GetAnalytics(Guid id)
    {
        var program = await _context.LoyaltyPrograms
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId);

        if (program == null)
            return NotFound();

        var memberCount = await _context.CustomerLoyalties
            .CountAsync(l => l.ProgramId == id);

        var totalPointsIssued = await _context.PointsTransactions
            .Where(t => t.CustomerLoyalty!.ProgramId == id && t.Points > 0)
            .SumAsync(t => t.Points);

        var totalPointsRedeemed = await _context.PointsTransactions
            .Where(t => t.CustomerLoyalty!.ProgramId == id && t.Points < 0)
            .SumAsync(t => Math.Abs(t.Points));

        var tierDistribution = await _context.CustomerLoyalties
            .Where(l => l.ProgramId == id && l.CurrentTierId != null)
            .GroupBy(l => l.CurrentTierId)
            .Select(g => new { TierId = g.Key, Count = g.Count() })
            .ToListAsync();

        var tiers = await _context.LoyaltyTiers
            .Where(t => t.ProgramId == id)
            .ToDictionaryAsync(t => t.Id, t => t.Name);

        return Ok(new
        {
            _links = new { self = new { href = $"/api/loyalty-programs/{id}/analytics" } },
            programId = id,
            memberCount,
            totalPointsIssued,
            totalPointsRedeemed,
            outstandingPoints = totalPointsIssued - totalPointsRedeemed,
            tierDistribution = tierDistribution.Select(td => new
            {
                tierId = td.TierId,
                tierName = td.TierId.HasValue && tiers.ContainsKey(td.TierId.Value) ? tiers[td.TierId.Value] : "Unknown",
                memberCount = td.Count
            })
        });
    }

    private static LoyaltyProgramDto MapToDto(LoyaltyProgram program)
    {
        return new LoyaltyProgramDto
        {
            Id = program.Id,
            TenantId = program.TenantId,
            Name = program.Name,
            Type = program.Type,
            Status = program.Status,
            PointsPerCurrencyUnit = program.PointsPerCurrencyUnit,
            PointsValueInCurrency = program.PointsValueInCurrency,
            MinimumRedemption = program.MinimumRedemption,
            PointsExpireAfterDays = program.PointsExpireAfterDays,
            WelcomeBonus = program.WelcomeBonus,
            BirthdayBonus = program.BirthdayBonus,
            ReferralBonus = program.ReferralBonus,
            TermsAndConditions = program.TermsAndConditions,
            StartDate = program.StartDate,
            EndDate = program.EndDate,
            CreatedAt = program.CreatedAt,
            Tiers = program.Tiers.Select(MapTierToDto).ToList()
        };
    }

    private static LoyaltyTierDto MapTierToDto(LoyaltyTier tier)
    {
        return new LoyaltyTierDto
        {
            Id = tier.Id,
            ProgramId = tier.ProgramId,
            Name = tier.Name,
            MinimumPoints = tier.MinimumPoints,
            PointsMultiplier = tier.PointsMultiplier,
            FreeDelivery = tier.FreeDelivery,
            PriorityBooking = tier.PriorityBooking,
            ExclusiveOffers = tier.ExclusiveOffers,
            BirthdayReward = tier.BirthdayReward,
            Color = tier.Color,
            IconUrl = tier.IconUrl,
            SortOrder = tier.SortOrder
        };
    }
}
