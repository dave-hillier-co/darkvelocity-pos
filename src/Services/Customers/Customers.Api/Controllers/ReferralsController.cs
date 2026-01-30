using DarkVelocity.Customers.Api.Data;
using DarkVelocity.Customers.Api.Dtos;
using DarkVelocity.Customers.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Customers.Api.Controllers;

[ApiController]
[Route("api/referrals")]
public class ReferralsController : ControllerBase
{
    private readonly CustomersDbContext _context;

    private Guid TenantId => Guid.Parse(Request.Headers["X-Tenant-Id"].FirstOrDefault()
        ?? "00000000-0000-0000-0000-000000000001");

    public ReferralsController(CustomersDbContext context)
    {
        _context = context;
    }

    [HttpPost("validate")]
    public async Task<ActionResult<ValidateReferralResponse>> Validate([FromBody] ValidateReferralRequest request)
    {
        var referral = await _context.Referrals
            .Include(r => r.ReferrerCustomer)
            .FirstOrDefaultAsync(r => r.ReferralCode == request.Code && r.TenantId == TenantId);

        if (referral == null)
        {
            return Ok(new ValidateReferralResponse
            {
                IsValid = false,
                Message = "Invalid referral code"
            });
        }

        if (referral.Status != "pending")
        {
            return Ok(new ValidateReferralResponse
            {
                IsValid = false,
                Message = "Referral code has already been used"
            });
        }

        return Ok(new ValidateReferralResponse
        {
            IsValid = true,
            ReferrerName = referral.ReferrerCustomer?.FullName,
            RefereeBonus = referral.RefereeBonus,
            Message = "Valid referral code"
        });
    }

    [HttpGet("analytics")]
    public async Task<ActionResult<object>> GetAnalytics()
    {
        var totalReferrals = await _context.Referrals
            .CountAsync(r => r.TenantId == TenantId);

        var completedReferrals = await _context.Referrals
            .CountAsync(r => r.TenantId == TenantId && r.Status == "completed");

        var pendingReferrals = await _context.Referrals
            .CountAsync(r => r.TenantId == TenantId && r.Status == "pending");

        var topReferrers = await _context.Referrals
            .Where(r => r.TenantId == TenantId && r.Status == "completed")
            .GroupBy(r => r.ReferrerCustomerId)
            .Select(g => new { CustomerId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var customerIds = topReferrers.Select(t => t.CustomerId).ToList();
        var customers = await _context.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.FullName);

        return Ok(new
        {
            _links = new { self = new { href = "/api/referrals/analytics" } },
            totalReferrals,
            completedReferrals,
            pendingReferrals,
            conversionRate = totalReferrals > 0 ? (decimal)completedReferrals / totalReferrals * 100 : 0,
            topReferrers = topReferrers.Select(t => new
            {
                customerId = t.CustomerId,
                customerName = customers.GetValueOrDefault(t.CustomerId, "Unknown"),
                referralCount = t.Count
            })
        });
    }
}

[ApiController]
[Route("api/customers/{customerId:guid}")]
public class CustomerReferralsController : ControllerBase
{
    private readonly CustomersDbContext _context;

    private Guid TenantId => Guid.Parse(Request.Headers["X-Tenant-Id"].FirstOrDefault()
        ?? "00000000-0000-0000-0000-000000000001");

    public CustomerReferralsController(CustomersDbContext context)
    {
        _context = context;
    }

    [HttpGet("referral-code")]
    public async Task<ActionResult<ReferralCodeDto>> GetReferralCode(Guid customerId)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        // Get or create referral code
        var referral = await _context.Referrals
            .FirstOrDefaultAsync(r => r.ReferrerCustomerId == customerId && r.Status == "pending");

        if (referral == null)
        {
            // Get referral bonus from active loyalty program
            var program = await _context.LoyaltyPrograms
                .FirstOrDefaultAsync(p => p.TenantId == TenantId && p.Status == "active");

            var referrerBonus = program?.ReferralBonus ?? 100;
            var refereeBonus = program?.WelcomeBonus ?? 50;

            referral = new Referral
            {
                TenantId = TenantId,
                ReferrerCustomerId = customerId,
                ReferralCode = GenerateReferralCode(),
                Status = "pending",
                ReferrerBonus = referrerBonus,
                RefereeBonus = refereeBonus
            };

            _context.Referrals.Add(referral);
            await _context.SaveChangesAsync();
        }

        var dto = new ReferralCodeDto
        {
            Code = referral.ReferralCode,
            ReferrerBonus = referral.ReferrerBonus,
            RefereeBonus = referral.RefereeBonus
        };

        dto.AddSelfLink($"/api/customers/{customerId}/referral-code");

        return Ok(dto);
    }

    [HttpGet("referrals")]
    public async Task<ActionResult<HalCollection<ReferralDto>>> GetReferrals(
        Guid customerId,
        [FromQuery] string? status = null)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var query = _context.Referrals
            .Include(r => r.ReferredCustomer)
            .Where(r => r.ReferrerCustomerId == customerId);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(r => r.Status == status);
        }

        var referrals = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var dtos = referrals.Select(r => new ReferralDto
        {
            Id = r.Id,
            TenantId = r.TenantId,
            ReferrerCustomerId = r.ReferrerCustomerId,
            ReferrerName = customer.FullName,
            ReferredCustomerId = r.ReferredCustomerId,
            ReferredName = r.ReferredCustomer?.FullName,
            ReferralCode = r.ReferralCode,
            Status = r.Status,
            ReferrerBonus = r.ReferrerBonus,
            RefereeBonus = r.RefereeBonus,
            CreatedAt = r.CreatedAt,
            CompletedAt = r.CompletedAt
        }).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/customers/{customerId}/referrals/{dto.Id}");
        }

        return Ok(HalCollection<ReferralDto>.Create(
            dtos,
            $"/api/customers/{customerId}/referrals",
            dtos.Count
        ));
    }

    [HttpPost("apply-referral")]
    public async Task<ActionResult<ReferralDto>> ApplyReferral(Guid customerId, [FromBody] ValidateReferralRequest request)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var referral = await _context.Referrals
            .Include(r => r.ReferrerCustomer)
            .FirstOrDefaultAsync(r => r.ReferralCode == request.Code && r.TenantId == TenantId);

        if (referral == null)
            return BadRequest(new { message = "Invalid referral code" });

        if (referral.Status != "pending")
            return BadRequest(new { message = "Referral code has already been used" });

        if (referral.ReferrerCustomerId == customerId)
            return BadRequest(new { message = "Cannot use your own referral code" });

        // Mark referral as completed
        referral.ReferredCustomerId = customerId;
        referral.Status = "completed";
        referral.CompletedAt = DateTime.UtcNow;

        // Award bonus points to both parties
        var program = await _context.LoyaltyPrograms
            .FirstOrDefaultAsync(p => p.TenantId == TenantId && p.Status == "active");

        if (program != null)
        {
            // Award referee bonus
            var refereeLoyalty = await _context.CustomerLoyalties
                .FirstOrDefaultAsync(l => l.CustomerId == customerId && l.ProgramId == program.Id);

            if (refereeLoyalty != null)
            {
                var refereeTransaction = new PointsTransaction
                {
                    CustomerLoyaltyId = refereeLoyalty.Id,
                    TransactionType = "referral",
                    Points = referral.RefereeBonus,
                    BalanceBefore = refereeLoyalty.CurrentPoints,
                    BalanceAfter = refereeLoyalty.CurrentPoints + referral.RefereeBonus,
                    Description = $"Referral bonus from {referral.ReferrerCustomer?.FullName}",
                    ProcessedAt = DateTime.UtcNow
                };

                refereeLoyalty.CurrentPoints += referral.RefereeBonus;
                refereeLoyalty.LifetimePoints += referral.RefereeBonus;
                refereeLoyalty.LastActivityAt = DateTime.UtcNow;

                _context.PointsTransactions.Add(refereeTransaction);
            }

            // Award referrer bonus
            var referrerLoyalty = await _context.CustomerLoyalties
                .FirstOrDefaultAsync(l => l.CustomerId == referral.ReferrerCustomerId && l.ProgramId == program.Id);

            if (referrerLoyalty != null)
            {
                var referrerTransaction = new PointsTransaction
                {
                    CustomerLoyaltyId = referrerLoyalty.Id,
                    TransactionType = "referral",
                    Points = referral.ReferrerBonus,
                    BalanceBefore = referrerLoyalty.CurrentPoints,
                    BalanceAfter = referrerLoyalty.CurrentPoints + referral.ReferrerBonus,
                    Description = $"Referral bonus for referring {customer.FullName}",
                    ProcessedAt = DateTime.UtcNow
                };

                referrerLoyalty.CurrentPoints += referral.ReferrerBonus;
                referrerLoyalty.LifetimePoints += referral.ReferrerBonus;
                referrerLoyalty.LastActivityAt = DateTime.UtcNow;

                _context.PointsTransactions.Add(referrerTransaction);
            }
        }

        await _context.SaveChangesAsync();

        var dto = new ReferralDto
        {
            Id = referral.Id,
            TenantId = referral.TenantId,
            ReferrerCustomerId = referral.ReferrerCustomerId,
            ReferrerName = referral.ReferrerCustomer?.FullName,
            ReferredCustomerId = referral.ReferredCustomerId,
            ReferredName = customer.FullName,
            ReferralCode = referral.ReferralCode,
            Status = referral.Status,
            ReferrerBonus = referral.ReferrerBonus,
            RefereeBonus = referral.RefereeBonus,
            CreatedAt = referral.CreatedAt,
            CompletedAt = referral.CompletedAt
        };

        dto.AddSelfLink($"/api/customers/{customerId}/referrals/{referral.Id}");

        return Ok(dto);
    }

    private static string GenerateReferralCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return "REF-" + new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
