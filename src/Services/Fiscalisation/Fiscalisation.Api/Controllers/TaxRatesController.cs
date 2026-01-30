using DarkVelocity.Fiscalisation.Api.Data;
using DarkVelocity.Fiscalisation.Api.Dtos;
using DarkVelocity.Fiscalisation.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Fiscalisation.Api.Controllers;

[ApiController]
[Route("api/tax-rates")]
public class TaxRatesController : ControllerBase
{
    private readonly FiscalisationDbContext _context;

    public TaxRatesController(FiscalisationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<TaxRateDto>>> GetAll(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? countryCode = null,
        [FromQuery] bool? activeOnly = true,
        [FromQuery] int limit = 50)
    {
        var query = _context.TaxRates.AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(r => r.TenantId == tenantId.Value);

        if (!string.IsNullOrEmpty(countryCode))
            query = query.Where(r => r.CountryCode == countryCode.ToUpper());

        if (activeOnly == true)
            query = query.Where(r => r.IsActive);

        var rates = await query
            .OrderBy(r => r.CountryCode)
            .ThenBy(r => r.FiscalCode)
            .Take(limit)
            .ToListAsync();

        var dtos = rates.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/tax-rates/{dto.Id}");
        }

        return Ok(HalCollection<TaxRateDto>.Create(
            dtos,
            "/api/tax-rates",
            dtos.Count));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaxRateDto>> GetById(Guid id)
    {
        var rate = await _context.TaxRates.FindAsync(id);

        if (rate == null)
            return NotFound();

        var dto = MapToDto(rate);
        dto.AddSelfLink($"/api/tax-rates/{rate.Id}");

        return Ok(dto);
    }

    [HttpGet("by-country/{countryCode}")]
    public async Task<ActionResult<HalCollection<TaxRateDto>>> GetByCountry(
        string countryCode,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] DateTime? asOfDate = null)
    {
        var effectiveDate = asOfDate ?? DateTime.UtcNow;

        var query = _context.TaxRates
            .Where(r =>
                r.CountryCode == countryCode.ToUpper() &&
                r.IsActive &&
                r.EffectiveFrom <= effectiveDate &&
                (r.EffectiveTo == null || r.EffectiveTo >= effectiveDate));

        if (tenantId.HasValue)
            query = query.Where(r => r.TenantId == tenantId.Value);

        var rates = await query
            .OrderBy(r => r.FiscalCode)
            .ToListAsync();

        var dtos = rates.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/tax-rates/{dto.Id}");
        }

        return Ok(HalCollection<TaxRateDto>.Create(
            dtos,
            $"/api/tax-rates/by-country/{countryCode}",
            dtos.Count));
    }

    [HttpPost]
    public async Task<ActionResult<TaxRateDto>> Create([FromBody] CreateTaxRateRequest request)
    {
        // Check for duplicate fiscal code
        var existingRate = await _context.TaxRates
            .FirstOrDefaultAsync(r =>
                r.TenantId == request.TenantId &&
                r.FiscalCode == request.FiscalCode);

        if (existingRate != null)
        {
            return Conflict(new
            {
                message = $"A tax rate with fiscal code '{request.FiscalCode}' already exists for this tenant"
            });
        }

        var rate = new TaxRate
        {
            TenantId = request.TenantId,
            CountryCode = request.CountryCode.ToUpper(),
            Rate = request.Rate,
            FiscalCode = request.FiscalCode,
            Description = request.Description,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            IsActive = true
        };

        _context.TaxRates.Add(rate);
        await _context.SaveChangesAsync();

        var dto = MapToDto(rate);
        dto.AddSelfLink($"/api/tax-rates/{rate.Id}");

        return CreatedAtAction(nameof(GetById), new { id = rate.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TaxRateDto>> Update(Guid id, [FromBody] UpdateTaxRateRequest request)
    {
        var rate = await _context.TaxRates.FindAsync(id);

        if (rate == null)
            return NotFound();

        if (request.Description != null)
            rate.Description = request.Description;

        if (request.EffectiveTo.HasValue)
            rate.EffectiveTo = request.EffectiveTo.Value;

        if (request.IsActive.HasValue)
            rate.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        var dto = MapToDto(rate);
        dto.AddSelfLink($"/api/tax-rates/{rate.Id}");

        return Ok(dto);
    }

    [HttpPost("seed-german-rates")]
    public async Task<ActionResult<HalCollection<TaxRateDto>>> SeedGermanRates([FromQuery] Guid tenantId)
    {
        // Check if rates already exist
        var existingRates = await _context.TaxRates
            .Where(r => r.TenantId == tenantId && r.CountryCode == "DE")
            .AnyAsync();

        if (existingRates)
        {
            return Conflict(new { message = "German tax rates already exist for this tenant" });
        }

        // Standard German tax rates as of 2024
        var germanRates = new List<TaxRate>
        {
            new()
            {
                TenantId = tenantId,
                CountryCode = "DE",
                Rate = 0.19m,
                FiscalCode = "A",
                Description = "Standard VAT rate (19%)",
                EffectiveFrom = new DateTime(2007, 1, 1),
                IsActive = true
            },
            new()
            {
                TenantId = tenantId,
                CountryCode = "DE",
                Rate = 0.07m,
                FiscalCode = "B",
                Description = "Reduced VAT rate (7%)",
                EffectiveFrom = new DateTime(1983, 7, 1),
                IsActive = true
            },
            new()
            {
                TenantId = tenantId,
                CountryCode = "DE",
                Rate = 0.107m,
                FiscalCode = "C",
                Description = "Agricultural flat-rate (10.7%)",
                EffectiveFrom = new DateTime(2022, 1, 1),
                IsActive = true
            },
            new()
            {
                TenantId = tenantId,
                CountryCode = "DE",
                Rate = 0.055m,
                FiscalCode = "D",
                Description = "Agricultural flat-rate reduced (5.5%)",
                EffectiveFrom = new DateTime(2022, 1, 1),
                IsActive = true
            },
            new()
            {
                TenantId = tenantId,
                CountryCode = "DE",
                Rate = 0.00m,
                FiscalCode = "E",
                Description = "Zero rate / Exempt",
                EffectiveFrom = new DateTime(1968, 1, 1),
                IsActive = true
            }
        };

        _context.TaxRates.AddRange(germanRates);
        await _context.SaveChangesAsync();

        var dtos = germanRates.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/tax-rates/{dto.Id}");
        }

        return Ok(HalCollection<TaxRateDto>.Create(
            dtos,
            "/api/tax-rates/seed-german-rates",
            dtos.Count));
    }

    private static TaxRateDto MapToDto(TaxRate rate)
    {
        return new TaxRateDto
        {
            Id = rate.Id,
            TenantId = rate.TenantId,
            CountryCode = rate.CountryCode,
            Rate = rate.Rate,
            FiscalCode = rate.FiscalCode,
            Description = rate.Description,
            EffectiveFrom = rate.EffectiveFrom,
            EffectiveTo = rate.EffectiveTo,
            IsActive = rate.IsActive,
            CreatedAt = rate.CreatedAt
        };
    }
}
