using DarkVelocity.Customers.Api.Data;
using DarkVelocity.Customers.Api.Dtos;
using DarkVelocity.Customers.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Customers.Api.Controllers;

[ApiController]
[Route("api/customers/{customerId:guid}/loyalty")]
public class CustomerLoyaltyController : ControllerBase
{
    private readonly CustomersDbContext _context;

    private Guid TenantId => Guid.Parse(Request.Headers["X-Tenant-Id"].FirstOrDefault()
        ?? "00000000-0000-0000-0000-000000000001");

    public CustomerLoyaltyController(CustomersDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<CustomerLoyaltyDto>>> GetAll(Guid customerId)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var loyalties = await _context.CustomerLoyalties
            .Include(l => l.Program)
            .Include(l => l.CurrentTier)
            .Where(l => l.CustomerId == customerId)
            .ToListAsync();

        var dtos = loyalties.Select(l => MapToDto(l, customerId)).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/customers/{customerId}/loyalty/{dto.ProgramId}");
        }

        return Ok(HalCollection<CustomerLoyaltyDto>.Create(
            dtos,
            $"/api/customers/{customerId}/loyalty",
            dtos.Count
        ));
    }

    [HttpGet("{programId:guid}")]
    public async Task<ActionResult<CustomerLoyaltyDto>> GetByProgram(Guid customerId, Guid programId)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var loyalty = await _context.CustomerLoyalties
            .Include(l => l.Program)
            .Include(l => l.CurrentTier)
            .FirstOrDefaultAsync(l => l.CustomerId == customerId && l.ProgramId == programId);

        if (loyalty == null)
            return NotFound();

        var dto = MapToDto(loyalty, customerId);
        dto.AddSelfLink($"/api/customers/{customerId}/loyalty/{programId}");
        dto.AddLink("transactions", $"/api/customers/{customerId}/loyalty/{programId}/transactions");
        dto.AddLink("rewards", $"/api/customers/{customerId}/loyalty/rewards");

        return Ok(dto);
    }

    [HttpPost("enroll")]
    public async Task<ActionResult<CustomerLoyaltyDto>> Enroll(Guid customerId, [FromBody] EnrollCustomerRequest request)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var program = await _context.LoyaltyPrograms
            .Include(p => p.Tiers.OrderBy(t => t.MinimumPoints))
            .FirstOrDefaultAsync(p => p.Id == request.ProgramId && p.TenantId == TenantId);

        if (program == null)
            return NotFound(new { message = "Loyalty program not found" });

        if (program.Status != "active")
            return BadRequest(new { message = "Cannot enroll in an inactive program" });

        // Check if already enrolled
        var existing = await _context.CustomerLoyalties
            .FirstOrDefaultAsync(l => l.CustomerId == customerId && l.ProgramId == request.ProgramId);

        if (existing != null)
            return Conflict(new { message = "Customer is already enrolled in this program" });

        var baseTier = program.Tiers.OrderBy(t => t.MinimumPoints).FirstOrDefault();

        var loyalty = new CustomerLoyalty
        {
            CustomerId = customerId,
            ProgramId = request.ProgramId,
            CurrentPoints = 0,
            LifetimePoints = 0,
            CurrentTierId = baseTier?.Id,
            EnrolledAt = DateTime.UtcNow
        };

        _context.CustomerLoyalties.Add(loyalty);

        // Award welcome bonus if configured
        if (program.WelcomeBonus > 0)
        {
            loyalty.CurrentPoints = program.WelcomeBonus.Value;
            loyalty.LifetimePoints = program.WelcomeBonus.Value;

            var welcomeTransaction = new PointsTransaction
            {
                CustomerLoyaltyId = loyalty.Id,
                TransactionType = "bonus",
                Points = program.WelcomeBonus.Value,
                BalanceBefore = 0,
                BalanceAfter = program.WelcomeBonus.Value,
                Description = "Welcome bonus",
                ProcessedAt = DateTime.UtcNow,
                ExpiresAt = program.PointsExpireAfterDays.HasValue
                    ? DateTime.UtcNow.AddDays(program.PointsExpireAfterDays.Value)
                    : null
            };

            _context.PointsTransactions.Add(welcomeTransaction);
        }

        await _context.SaveChangesAsync();

        // Reload to get navigation properties
        await _context.Entry(loyalty).Reference(l => l.Program).LoadAsync();
        await _context.Entry(loyalty).Reference(l => l.CurrentTier).LoadAsync();

        var dto = MapToDto(loyalty, customerId);
        dto.AddSelfLink($"/api/customers/{customerId}/loyalty/{request.ProgramId}");

        return CreatedAtAction(nameof(GetByProgram), new { customerId, programId = request.ProgramId }, dto);
    }

    [HttpGet("points")]
    public async Task<ActionResult<object>> GetPointsBalance(Guid customerId)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var loyalties = await _context.CustomerLoyalties
            .Include(l => l.Program)
            .Where(l => l.CustomerId == customerId)
            .Select(l => new
            {
                programId = l.ProgramId,
                programName = l.Program!.Name,
                currentPoints = l.CurrentPoints,
                lifetimePoints = l.LifetimePoints
            })
            .ToListAsync();

        return Ok(new
        {
            _links = new { self = new { href = $"/api/customers/{customerId}/loyalty/points" } },
            customerId,
            balances = loyalties
        });
    }

    [HttpGet("{programId:guid}/transactions")]
    public async Task<ActionResult<HalCollection<PointsTransactionDto>>> GetTransactions(
        Guid customerId,
        Guid programId,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var loyalty = await _context.CustomerLoyalties
            .FirstOrDefaultAsync(l => l.CustomerId == customerId && l.ProgramId == programId);

        if (loyalty == null)
            return NotFound();

        var total = await _context.PointsTransactions
            .CountAsync(t => t.CustomerLoyaltyId == loyalty.Id);

        var transactions = await _context.PointsTransactions
            .Where(t => t.CustomerLoyaltyId == loyalty.Id)
            .OrderByDescending(t => t.ProcessedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = transactions.Select(t => new PointsTransactionDto
        {
            Id = t.Id,
            CustomerLoyaltyId = t.CustomerLoyaltyId,
            TransactionType = t.TransactionType,
            Points = t.Points,
            BalanceBefore = t.BalanceBefore,
            BalanceAfter = t.BalanceAfter,
            OrderId = t.OrderId,
            LocationId = t.LocationId,
            Description = t.Description,
            ExpiresAt = t.ExpiresAt,
            ProcessedAt = t.ProcessedAt
        }).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/customers/{customerId}/loyalty/{programId}/transactions/{dto.Id}");
        }

        return Ok(HalCollection<PointsTransactionDto>.Create(
            dtos,
            $"/api/customers/{customerId}/loyalty/{programId}/transactions",
            total
        ));
    }

    [HttpPost("earn")]
    public async Task<ActionResult<CustomerLoyaltyDto>> EarnPoints(Guid customerId, [FromBody] EarnPointsRequest request)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        // Get the active program for this tenant
        var loyalty = await _context.CustomerLoyalties
            .Include(l => l.Program)
            .ThenInclude(p => p!.Tiers.OrderBy(t => t.MinimumPoints))
            .Include(l => l.CurrentTier)
            .FirstOrDefaultAsync(l => l.CustomerId == customerId && l.Program!.Status == "active");

        if (loyalty == null)
            return BadRequest(new { message = "Customer is not enrolled in any active loyalty program" });

        var program = loyalty.Program!;
        var multiplier = loyalty.CurrentTier?.PointsMultiplier ?? 1.0m;
        var pointsToEarn = (int)(request.Points * multiplier);

        var transaction = new PointsTransaction
        {
            CustomerLoyaltyId = loyalty.Id,
            TransactionType = "earn",
            Points = pointsToEarn,
            BalanceBefore = loyalty.CurrentPoints,
            BalanceAfter = loyalty.CurrentPoints + pointsToEarn,
            OrderId = request.OrderId,
            LocationId = request.LocationId,
            Description = request.Description,
            ProcessedAt = DateTime.UtcNow,
            ExpiresAt = program.PointsExpireAfterDays.HasValue
                ? DateTime.UtcNow.AddDays(program.PointsExpireAfterDays.Value)
                : null
        };

        loyalty.CurrentPoints += pointsToEarn;
        loyalty.LifetimePoints += pointsToEarn;
        loyalty.TierQualifyingPoints += pointsToEarn;
        loyalty.LastActivityAt = DateTime.UtcNow;

        // Check for tier upgrade
        var newTier = program.Tiers
            .Where(t => t.MinimumPoints <= loyalty.LifetimePoints)
            .OrderByDescending(t => t.MinimumPoints)
            .FirstOrDefault();

        if (newTier != null && newTier.Id != loyalty.CurrentTierId)
        {
            loyalty.CurrentTierId = newTier.Id;
        }

        _context.PointsTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Reload tier
        await _context.Entry(loyalty).Reference(l => l.CurrentTier).LoadAsync();

        var dto = MapToDto(loyalty, customerId);
        dto.AddSelfLink($"/api/customers/{customerId}/loyalty/{loyalty.ProgramId}");

        return Ok(dto);
    }

    [HttpPost("redeem")]
    public async Task<ActionResult<CustomerLoyaltyDto>> RedeemPoints(Guid customerId, [FromBody] RedeemPointsRequest request)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var loyalty = await _context.CustomerLoyalties
            .Include(l => l.Program)
            .Include(l => l.CurrentTier)
            .FirstOrDefaultAsync(l => l.CustomerId == customerId && l.Program!.Status == "active");

        if (loyalty == null)
            return BadRequest(new { message = "Customer is not enrolled in any active loyalty program" });

        if (loyalty.CurrentPoints < request.Points)
            return BadRequest(new { message = "Insufficient points" });

        var program = loyalty.Program!;
        if (request.Points < program.MinimumRedemption)
            return BadRequest(new { message = $"Minimum redemption is {program.MinimumRedemption} points" });

        var transaction = new PointsTransaction
        {
            CustomerLoyaltyId = loyalty.Id,
            TransactionType = "redeem",
            Points = -request.Points,
            BalanceBefore = loyalty.CurrentPoints,
            BalanceAfter = loyalty.CurrentPoints - request.Points,
            Description = request.Description,
            ProcessedAt = DateTime.UtcNow
        };

        loyalty.CurrentPoints -= request.Points;
        loyalty.LastActivityAt = DateTime.UtcNow;

        _context.PointsTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        var dto = MapToDto(loyalty, customerId);
        dto.AddSelfLink($"/api/customers/{customerId}/loyalty/{loyalty.ProgramId}");

        return Ok(dto);
    }

    [HttpPost("adjust")]
    public async Task<ActionResult<CustomerLoyaltyDto>> AdjustPoints(Guid customerId, [FromBody] AdjustPointsRequest request)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var loyalty = await _context.CustomerLoyalties
            .Include(l => l.Program)
            .ThenInclude(p => p!.Tiers.OrderBy(t => t.MinimumPoints))
            .Include(l => l.CurrentTier)
            .FirstOrDefaultAsync(l => l.CustomerId == customerId && l.Program!.Status == "active");

        if (loyalty == null)
            return BadRequest(new { message = "Customer is not enrolled in any active loyalty program" });

        var newBalance = loyalty.CurrentPoints + request.Points;
        if (newBalance < 0)
            return BadRequest(new { message = "Adjustment would result in negative balance" });

        var transaction = new PointsTransaction
        {
            CustomerLoyaltyId = loyalty.Id,
            TransactionType = "adjust",
            Points = request.Points,
            BalanceBefore = loyalty.CurrentPoints,
            BalanceAfter = newBalance,
            Description = request.Reason,
            ProcessedAt = DateTime.UtcNow
        };

        loyalty.CurrentPoints = newBalance;
        if (request.Points > 0)
        {
            loyalty.LifetimePoints += request.Points;
            loyalty.TierQualifyingPoints += request.Points;
        }
        loyalty.LastActivityAt = DateTime.UtcNow;

        // Check for tier upgrade
        var program = loyalty.Program!;
        var newTier = program.Tiers
            .Where(t => t.MinimumPoints <= loyalty.LifetimePoints)
            .OrderByDescending(t => t.MinimumPoints)
            .FirstOrDefault();

        if (newTier != null && newTier.Id != loyalty.CurrentTierId)
        {
            loyalty.CurrentTierId = newTier.Id;
        }

        _context.PointsTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Reload tier
        await _context.Entry(loyalty).Reference(l => l.CurrentTier).LoadAsync();

        var dto = MapToDto(loyalty, customerId);
        dto.AddSelfLink($"/api/customers/{customerId}/loyalty/{loyalty.ProgramId}");

        return Ok(dto);
    }

    [HttpGet("rewards")]
    public async Task<ActionResult<HalCollection<CustomerRewardDto>>> GetRewards(Guid customerId)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var rewards = await _context.CustomerRewards
            .Include(r => r.Reward)
            .Where(r => r.CustomerId == customerId && r.Status == "available")
            .ToListAsync();

        var dtos = rewards.Select(r => new CustomerRewardDto
        {
            Id = r.Id,
            CustomerId = r.CustomerId,
            RewardId = r.RewardId,
            RewardName = r.Reward?.Name ?? "",
            RewardType = r.Reward?.Type ?? "",
            Code = r.Code,
            Status = r.Status,
            IssuedAt = r.IssuedAt,
            ExpiresAt = r.ExpiresAt,
            RedeemedAt = r.RedeemedAt,
            RedeemedOrderId = r.RedeemedOrderId,
            RedeemedLocationId = r.RedeemedLocationId
        }).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/customers/{customerId}/rewards/{dto.Id}");
        }

        return Ok(HalCollection<CustomerRewardDto>.Create(
            dtos,
            $"/api/customers/{customerId}/loyalty/rewards",
            dtos.Count
        ));
    }

    private static CustomerLoyaltyDto MapToDto(CustomerLoyalty loyalty, Guid customerId)
    {
        return new CustomerLoyaltyDto
        {
            Id = loyalty.Id,
            CustomerId = customerId,
            ProgramId = loyalty.ProgramId,
            ProgramName = loyalty.Program?.Name ?? "",
            CurrentPoints = loyalty.CurrentPoints,
            LifetimePoints = loyalty.LifetimePoints,
            CurrentTierId = loyalty.CurrentTierId,
            CurrentTierName = loyalty.CurrentTier?.Name,
            TierQualifyingPoints = loyalty.TierQualifyingPoints,
            TierExpiresAt = loyalty.TierExpiresAt,
            EnrolledAt = loyalty.EnrolledAt,
            LastActivityAt = loyalty.LastActivityAt
        };
    }
}
