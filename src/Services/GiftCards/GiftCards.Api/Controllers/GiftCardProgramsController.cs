using DarkVelocity.GiftCards.Api.Data;
using DarkVelocity.GiftCards.Api.Dtos;
using DarkVelocity.GiftCards.Api.Entities;
using DarkVelocity.GiftCards.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.GiftCards.Api.Controllers;

[ApiController]
[Route("api/giftcard-programs")]
public class GiftCardProgramsController : ControllerBase
{
    private readonly GiftCardsDbContext _context;

    public GiftCardProgramsController(GiftCardsDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// List gift card programs
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HalCollection<GiftCardProgramSummaryDto>>> GetAll(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var query = _context.GiftCardPrograms.AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(p => p.TenantId == tenantId.Value);

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        var total = await query.CountAsync();

        var programs = await query
            .OrderBy(p => p.Name)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        // Get statistics for each program
        var programIds = programs.Select(p => p.Id).ToList();
        var stats = await _context.GiftCards
            .Where(g => programIds.Contains(g.ProgramId))
            .GroupBy(g => g.ProgramId)
            .Select(g => new
            {
                ProgramId = g.Key,
                ActiveCount = g.Count(c => c.Status == "active"),
                TotalBalance = g.Where(c => c.Status == "active").Sum(c => c.CurrentBalance)
            })
            .ToListAsync();

        var statsDict = stats.ToDictionary(s => s.ProgramId);

        var dtos = programs.Select(p =>
        {
            statsDict.TryGetValue(p.Id, out var s);
            return new GiftCardProgramSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                IsActive = p.IsActive,
                CurrencyCode = p.CurrencyCode,
                ActiveCardsCount = s?.ActiveCount ?? 0,
                TotalOutstandingBalance = s?.TotalBalance ?? 0
            };
        }).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/giftcard-programs/{dto.Id}");
            dto.AddLink("cards", $"/api/giftcards?programId={dto.Id}");
            dto.AddLink("designs", $"/api/giftcard-programs/{dto.Id}/designs");
        }

        return Ok(HalCollection<GiftCardProgramSummaryDto>.Create(dtos, "/api/giftcard-programs", total));
    }

    /// <summary>
    /// Get program by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GiftCardProgramDto>> GetById(Guid id)
    {
        var program = await _context.GiftCardPrograms
            .Include(p => p.Designs.Where(d => d.IsActive).OrderBy(d => d.SortOrder))
            .FirstOrDefaultAsync(p => p.Id == id);

        if (program == null)
            return NotFound();

        // Get statistics
        var stats = await _context.GiftCards
            .Where(g => g.ProgramId == id)
            .GroupBy(g => 1)
            .Select(g => new
            {
                TotalIssued = g.Count(),
                ActiveCount = g.Count(c => c.Status == "active"),
                TotalBalance = g.Where(c => c.Status == "active").Sum(c => c.CurrentBalance)
            })
            .FirstOrDefaultAsync();

        var dto = new GiftCardProgramDto
        {
            Id = program.Id,
            TenantId = program.TenantId,
            Name = program.Name,
            Description = program.Description,
            CardNumberPrefix = program.CardNumberPrefix,
            DefaultExpiryMonths = program.DefaultExpiryMonths,
            MinimumLoadAmount = program.MinimumLoadAmount,
            MaximumLoadAmount = program.MaximumLoadAmount,
            MaximumBalance = program.MaximumBalance,
            AllowReload = program.AllowReload,
            AllowPartialRedemption = program.AllowPartialRedemption,
            RequirePin = program.RequirePin,
            IsActive = program.IsActive,
            CurrencyCode = program.CurrencyCode,
            CreatedAt = program.CreatedAt,
            UpdatedAt = program.UpdatedAt,
            TotalCardsIssued = stats?.TotalIssued ?? 0,
            ActiveCardsCount = stats?.ActiveCount ?? 0,
            TotalOutstandingBalance = stats?.TotalBalance ?? 0,
            Designs = program.Designs.Select(d => new GiftCardDesignDto
            {
                Id = d.Id,
                ProgramId = d.ProgramId,
                Name = d.Name,
                Description = d.Description,
                ImageUrl = d.ImageUrl,
                ThumbnailUrl = d.ThumbnailUrl,
                IsDefault = d.IsDefault,
                IsActive = d.IsActive,
                SortOrder = d.SortOrder,
                CreatedAt = d.CreatedAt
            }).ToList()
        };

        AddLinks(dto);

        return Ok(dto);
    }

    /// <summary>
    /// Get cards for a program
    /// </summary>
    [HttpGet("{id:guid}/cards")]
    public async Task<ActionResult<HalCollection<GiftCardSummaryDto>>> GetCards(
        Guid id,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var program = await _context.GiftCardPrograms.FindAsync(id);
        if (program == null)
            return NotFound();

        var query = _context.GiftCards
            .Where(g => g.ProgramId == id);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(g => g.Status == status);

        var total = await query.CountAsync();

        var cards = await query
            .OrderByDescending(g => g.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var dtos = cards.Select(g => new GiftCardSummaryDto
        {
            Id = g.Id,
            MaskedCardNumber = CardNumberHelper.MaskCardNumber(g.CardNumber),
            CardType = g.CardType,
            CurrentBalance = g.CurrentBalance,
            CurrencyCode = g.CurrencyCode,
            Status = g.Status,
            ExpiryDate = g.ExpiryDate,
            IsExpired = g.ExpiryDate.HasValue && g.ExpiryDate.Value < now,
            LastUsedAt = g.LastUsedAt,
            RecipientName = g.RecipientName,
            ProgramName = program.Name
        }).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/giftcards/{dto.Id}");
        }

        return Ok(HalCollection<GiftCardSummaryDto>.Create(
            dtos,
            $"/api/giftcard-programs/{id}/cards",
            total
        ));
    }

    /// <summary>
    /// Create a new program
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<GiftCardProgramDto>> Create(
        [FromQuery] Guid tenantId,
        [FromBody] CreateGiftCardProgramRequest request)
    {
        // Validate uniqueness of name within tenant
        var exists = await _context.GiftCardPrograms
            .AnyAsync(p => p.TenantId == tenantId && p.Name == request.Name);

        if (exists)
            return BadRequest(new { message = "A program with this name already exists" });

        // Validate amounts
        if (request.MinimumLoadAmount < 0)
            return BadRequest(new { message = "Minimum load amount cannot be negative" });

        if (request.MaximumLoadAmount < request.MinimumLoadAmount)
            return BadRequest(new { message = "Maximum load amount must be greater than or equal to minimum" });

        if (request.MaximumBalance < request.MaximumLoadAmount)
            return BadRequest(new { message = "Maximum balance must be greater than or equal to maximum load amount" });

        var program = new GiftCardProgram
        {
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            CardNumberPrefix = request.CardNumberPrefix.ToUpperInvariant(),
            DefaultExpiryMonths = request.DefaultExpiryMonths,
            MinimumLoadAmount = request.MinimumLoadAmount,
            MaximumLoadAmount = request.MaximumLoadAmount,
            MaximumBalance = request.MaximumBalance,
            AllowReload = request.AllowReload,
            AllowPartialRedemption = request.AllowPartialRedemption,
            RequirePin = request.RequirePin,
            CurrencyCode = request.CurrencyCode.ToUpperInvariant(),
            IsActive = true
        };

        _context.GiftCardPrograms.Add(program);
        await _context.SaveChangesAsync();

        var dto = new GiftCardProgramDto
        {
            Id = program.Id,
            TenantId = program.TenantId,
            Name = program.Name,
            Description = program.Description,
            CardNumberPrefix = program.CardNumberPrefix,
            DefaultExpiryMonths = program.DefaultExpiryMonths,
            MinimumLoadAmount = program.MinimumLoadAmount,
            MaximumLoadAmount = program.MaximumLoadAmount,
            MaximumBalance = program.MaximumBalance,
            AllowReload = program.AllowReload,
            AllowPartialRedemption = program.AllowPartialRedemption,
            RequirePin = program.RequirePin,
            IsActive = program.IsActive,
            CurrencyCode = program.CurrencyCode,
            CreatedAt = program.CreatedAt,
            TotalCardsIssued = 0,
            ActiveCardsCount = 0,
            TotalOutstandingBalance = 0
        };

        AddLinks(dto);

        return CreatedAtAction(nameof(GetById), new { id = program.Id }, dto);
    }

    /// <summary>
    /// Update a program
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GiftCardProgramDto>> Update(
        Guid id,
        [FromBody] UpdateGiftCardProgramRequest request)
    {
        var program = await _context.GiftCardPrograms
            .Include(p => p.Designs.Where(d => d.IsActive).OrderBy(d => d.SortOrder))
            .FirstOrDefaultAsync(p => p.Id == id);

        if (program == null)
            return NotFound();

        if (request.Name != null)
        {
            // Validate uniqueness
            var exists = await _context.GiftCardPrograms
                .AnyAsync(p => p.TenantId == program.TenantId && p.Name == request.Name && p.Id != id);
            if (exists)
                return BadRequest(new { message = "A program with this name already exists" });

            program.Name = request.Name;
        }

        if (request.Description != null)
            program.Description = request.Description;

        if (request.DefaultExpiryMonths.HasValue)
            program.DefaultExpiryMonths = request.DefaultExpiryMonths;

        if (request.MinimumLoadAmount.HasValue)
            program.MinimumLoadAmount = request.MinimumLoadAmount.Value;

        if (request.MaximumLoadAmount.HasValue)
            program.MaximumLoadAmount = request.MaximumLoadAmount.Value;

        if (request.MaximumBalance.HasValue)
            program.MaximumBalance = request.MaximumBalance.Value;

        if (request.AllowReload.HasValue)
            program.AllowReload = request.AllowReload.Value;

        if (request.AllowPartialRedemption.HasValue)
            program.AllowPartialRedemption = request.AllowPartialRedemption.Value;

        if (request.RequirePin.HasValue)
            program.RequirePin = request.RequirePin.Value;

        if (request.IsActive.HasValue)
            program.IsActive = request.IsActive.Value;

        // Validate amounts
        if (program.MaximumLoadAmount < program.MinimumLoadAmount)
            return BadRequest(new { message = "Maximum load amount must be greater than or equal to minimum" });

        if (program.MaximumBalance < program.MaximumLoadAmount)
            return BadRequest(new { message = "Maximum balance must be greater than or equal to maximum load amount" });

        await _context.SaveChangesAsync();

        // Get statistics
        var stats = await _context.GiftCards
            .Where(g => g.ProgramId == id)
            .GroupBy(g => 1)
            .Select(g => new
            {
                TotalIssued = g.Count(),
                ActiveCount = g.Count(c => c.Status == "active"),
                TotalBalance = g.Where(c => c.Status == "active").Sum(c => c.CurrentBalance)
            })
            .FirstOrDefaultAsync();

        var dto = new GiftCardProgramDto
        {
            Id = program.Id,
            TenantId = program.TenantId,
            Name = program.Name,
            Description = program.Description,
            CardNumberPrefix = program.CardNumberPrefix,
            DefaultExpiryMonths = program.DefaultExpiryMonths,
            MinimumLoadAmount = program.MinimumLoadAmount,
            MaximumLoadAmount = program.MaximumLoadAmount,
            MaximumBalance = program.MaximumBalance,
            AllowReload = program.AllowReload,
            AllowPartialRedemption = program.AllowPartialRedemption,
            RequirePin = program.RequirePin,
            IsActive = program.IsActive,
            CurrencyCode = program.CurrencyCode,
            CreatedAt = program.CreatedAt,
            UpdatedAt = program.UpdatedAt,
            TotalCardsIssued = stats?.TotalIssued ?? 0,
            ActiveCardsCount = stats?.ActiveCount ?? 0,
            TotalOutstandingBalance = stats?.TotalBalance ?? 0,
            Designs = program.Designs.Select(d => new GiftCardDesignDto
            {
                Id = d.Id,
                ProgramId = d.ProgramId,
                Name = d.Name,
                Description = d.Description,
                ImageUrl = d.ImageUrl,
                ThumbnailUrl = d.ThumbnailUrl,
                IsDefault = d.IsDefault,
                IsActive = d.IsActive,
                SortOrder = d.SortOrder,
                CreatedAt = d.CreatedAt
            }).ToList()
        };

        AddLinks(dto);

        return Ok(dto);
    }

    // ============================================
    // Designs
    // ============================================

    /// <summary>
    /// List designs for a program
    /// </summary>
    [HttpGet("{programId:guid}/designs")]
    public async Task<ActionResult<HalCollection<GiftCardDesignDto>>> GetDesigns(
        Guid programId,
        [FromQuery] bool? isActive = null)
    {
        var program = await _context.GiftCardPrograms.FindAsync(programId);
        if (program == null)
            return NotFound();

        var query = _context.GiftCardDesigns
            .Where(d => d.ProgramId == programId);

        if (isActive.HasValue)
            query = query.Where(d => d.IsActive == isActive.Value);

        var designs = await query
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name)
            .ToListAsync();

        var dtos = designs.Select(d => new GiftCardDesignDto
        {
            Id = d.Id,
            ProgramId = d.ProgramId,
            Name = d.Name,
            Description = d.Description,
            ImageUrl = d.ImageUrl,
            ThumbnailUrl = d.ThumbnailUrl,
            IsDefault = d.IsDefault,
            IsActive = d.IsActive,
            SortOrder = d.SortOrder,
            CreatedAt = d.CreatedAt
        }).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/giftcard-programs/{programId}/designs/{dto.Id}");
            dto.AddLink("program", $"/api/giftcard-programs/{programId}");
        }

        return Ok(HalCollection<GiftCardDesignDto>.Create(
            dtos,
            $"/api/giftcard-programs/{programId}/designs",
            dtos.Count
        ));
    }

    /// <summary>
    /// Create a design for a program
    /// </summary>
    [HttpPost("{programId:guid}/designs")]
    public async Task<ActionResult<GiftCardDesignDto>> CreateDesign(
        Guid programId,
        [FromBody] CreateGiftCardDesignRequest request)
    {
        var program = await _context.GiftCardPrograms.FindAsync(programId);
        if (program == null)
            return NotFound();

        // Validate uniqueness
        var exists = await _context.GiftCardDesigns
            .AnyAsync(d => d.ProgramId == programId && d.Name == request.Name);
        if (exists)
            return BadRequest(new { message = "A design with this name already exists for this program" });

        var design = new GiftCardDesign
        {
            ProgramId = programId,
            Name = request.Name,
            Description = request.Description,
            ImageUrl = request.ImageUrl,
            ThumbnailUrl = request.ThumbnailUrl,
            IsDefault = request.IsDefault,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        // If this is marked as default, unset other defaults
        if (design.IsDefault)
        {
            var existingDefaults = await _context.GiftCardDesigns
                .Where(d => d.ProgramId == programId && d.IsDefault)
                .ToListAsync();

            foreach (var existing in existingDefaults)
            {
                existing.IsDefault = false;
            }
        }

        _context.GiftCardDesigns.Add(design);
        await _context.SaveChangesAsync();

        var dto = new GiftCardDesignDto
        {
            Id = design.Id,
            ProgramId = design.ProgramId,
            Name = design.Name,
            Description = design.Description,
            ImageUrl = design.ImageUrl,
            ThumbnailUrl = design.ThumbnailUrl,
            IsDefault = design.IsDefault,
            IsActive = design.IsActive,
            SortOrder = design.SortOrder,
            CreatedAt = design.CreatedAt
        };

        dto.AddSelfLink($"/api/giftcard-programs/{programId}/designs/{dto.Id}");
        dto.AddLink("program", $"/api/giftcard-programs/{programId}");

        return CreatedAtAction(nameof(GetDesigns), new { programId }, dto);
    }

    /// <summary>
    /// Update a design
    /// </summary>
    [HttpPut("{programId:guid}/designs/{designId:guid}")]
    public async Task<ActionResult<GiftCardDesignDto>> UpdateDesign(
        Guid programId,
        Guid designId,
        [FromBody] UpdateGiftCardDesignRequest request)
    {
        var design = await _context.GiftCardDesigns
            .FirstOrDefaultAsync(d => d.Id == designId && d.ProgramId == programId);

        if (design == null)
            return NotFound();

        if (request.Name != null)
        {
            // Validate uniqueness
            var exists = await _context.GiftCardDesigns
                .AnyAsync(d => d.ProgramId == programId && d.Name == request.Name && d.Id != designId);
            if (exists)
                return BadRequest(new { message = "A design with this name already exists for this program" });

            design.Name = request.Name;
        }

        if (request.Description != null)
            design.Description = request.Description;

        if (request.ImageUrl != null)
            design.ImageUrl = request.ImageUrl;

        if (request.ThumbnailUrl != null)
            design.ThumbnailUrl = request.ThumbnailUrl;

        if (request.IsActive.HasValue)
            design.IsActive = request.IsActive.Value;

        if (request.SortOrder.HasValue)
            design.SortOrder = request.SortOrder.Value;

        if (request.IsDefault == true && !design.IsDefault)
        {
            // Unset other defaults
            var existingDefaults = await _context.GiftCardDesigns
                .Where(d => d.ProgramId == programId && d.IsDefault && d.Id != designId)
                .ToListAsync();

            foreach (var existing in existingDefaults)
            {
                existing.IsDefault = false;
            }

            design.IsDefault = true;
        }
        else if (request.IsDefault == false)
        {
            design.IsDefault = false;
        }

        await _context.SaveChangesAsync();

        var dto = new GiftCardDesignDto
        {
            Id = design.Id,
            ProgramId = design.ProgramId,
            Name = design.Name,
            Description = design.Description,
            ImageUrl = design.ImageUrl,
            ThumbnailUrl = design.ThumbnailUrl,
            IsDefault = design.IsDefault,
            IsActive = design.IsActive,
            SortOrder = design.SortOrder,
            CreatedAt = design.CreatedAt
        };

        dto.AddSelfLink($"/api/giftcard-programs/{programId}/designs/{dto.Id}");
        dto.AddLink("program", $"/api/giftcard-programs/{programId}");

        return Ok(dto);
    }

    // ============================================
    // Helper Methods
    // ============================================

    private static void AddLinks(GiftCardProgramDto dto)
    {
        dto.AddSelfLink($"/api/giftcard-programs/{dto.Id}");
        dto.AddLink("cards", $"/api/giftcards?programId={dto.Id}");
        dto.AddLink("designs", $"/api/giftcard-programs/{dto.Id}/designs");
    }
}
