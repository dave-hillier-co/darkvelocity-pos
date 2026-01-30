using DarkVelocity.Customers.Api.Data;
using DarkVelocity.Customers.Api.Dtos;
using DarkVelocity.Customers.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Customers.Api.Controllers;

[ApiController]
[Route("api/rewards")]
public class RewardsController : ControllerBase
{
    private readonly CustomersDbContext _context;

    private Guid TenantId => Guid.Parse(Request.Headers["X-Tenant-Id"].FirstOrDefault()
        ?? "00000000-0000-0000-0000-000000000001");

    public RewardsController(CustomersDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<RewardSummaryDto>>> GetAll(
        [FromQuery] Guid? programId = null,
        [FromQuery] bool? activeOnly = true)
    {
        var query = _context.Rewards
            .Where(r => r.TenantId == TenantId);

        if (programId.HasValue)
        {
            query = query.Where(r => r.ProgramId == programId.Value);
        }

        if (activeOnly == true)
        {
            query = query.Where(r => r.IsActive);
        }

        var rewards = await query
            .OrderBy(r => r.PointsCost)
            .ToListAsync();

        var dtos = rewards.Select(r => new RewardSummaryDto
        {
            Id = r.Id,
            Name = r.Name,
            Type = r.Type,
            PointsCost = r.PointsCost,
            Value = r.Value,
            ImageUrl = r.ImageUrl,
            IsActive = r.IsActive
        }).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/rewards/{dto.Id}");
        }

        return Ok(HalCollection<RewardSummaryDto>.Create(
            dtos,
            "/api/rewards",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RewardDto>> GetById(Guid id)
    {
        var reward = await _context.Rewards
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId);

        if (reward == null)
            return NotFound();

        var dto = MapToDto(reward);
        dto.AddSelfLink($"/api/rewards/{reward.Id}");
        dto.AddLink("program", $"/api/loyalty-programs/{reward.ProgramId}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<RewardDto>> Create([FromBody] CreateRewardRequest request)
    {
        var program = await _context.LoyaltyPrograms
            .FirstOrDefaultAsync(p => p.Id == request.ProgramId && p.TenantId == TenantId);

        if (program == null)
            return NotFound(new { message = "Loyalty program not found" });

        var reward = new Reward
        {
            TenantId = TenantId,
            ProgramId = request.ProgramId,
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            PointsCost = request.PointsCost,
            Value = request.Value,
            MenuItemId = request.MenuItemId,
            DiscountPercentage = request.DiscountPercentage,
            MaxRedemptionsPerCustomer = request.MaxRedemptionsPerCustomer,
            TotalAvailable = request.TotalAvailable,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            TermsAndConditions = request.TermsAndConditions,
            ImageUrl = request.ImageUrl,
            IsActive = true
        };

        _context.Rewards.Add(reward);
        await _context.SaveChangesAsync();

        var dto = MapToDto(reward);
        dto.AddSelfLink($"/api/rewards/{reward.Id}");

        return CreatedAtAction(nameof(GetById), new { id = reward.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RewardDto>> Update(Guid id, [FromBody] UpdateRewardRequest request)
    {
        var reward = await _context.Rewards
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId);

        if (reward == null)
            return NotFound();

        if (request.Name != null) reward.Name = request.Name;
        if (request.Description != null) reward.Description = request.Description;
        if (request.PointsCost.HasValue) reward.PointsCost = request.PointsCost.Value;
        if (request.Value.HasValue) reward.Value = request.Value.Value;
        if (request.MenuItemId.HasValue) reward.MenuItemId = request.MenuItemId.Value;
        if (request.DiscountPercentage.HasValue) reward.DiscountPercentage = request.DiscountPercentage.Value;
        if (request.MaxRedemptionsPerCustomer.HasValue) reward.MaxRedemptionsPerCustomer = request.MaxRedemptionsPerCustomer.Value;
        if (request.TotalAvailable.HasValue) reward.TotalAvailable = request.TotalAvailable.Value;
        if (request.ValidUntil.HasValue) reward.ValidUntil = request.ValidUntil.Value;
        if (request.TermsAndConditions != null) reward.TermsAndConditions = request.TermsAndConditions;
        if (request.ImageUrl != null) reward.ImageUrl = request.ImageUrl;
        if (request.IsActive.HasValue) reward.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        var dto = MapToDto(reward);
        dto.AddSelfLink($"/api/rewards/{reward.Id}");

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var reward = await _context.Rewards
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId);

        if (reward == null)
            return NotFound();

        // Check if any customers have this reward
        var hasCustomerRewards = await _context.CustomerRewards
            .AnyAsync(cr => cr.RewardId == id && cr.Status == "available");

        if (hasCustomerRewards)
        {
            // Soft delete by deactivating
            reward.IsActive = false;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        _context.Rewards.Remove(reward);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // Customer rewards endpoints
    [HttpGet("{id:guid}/issue")]
    public async Task<ActionResult<CustomerRewardDto>> IssueToCustomer(
        Guid id,
        [FromQuery] Guid customerId)
    {
        var reward = await _context.Rewards
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId && r.IsActive);

        if (reward == null)
            return NotFound(new { message = "Reward not found or inactive" });

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound(new { message = "Customer not found" });

        // Check customer's loyalty membership
        var loyalty = await _context.CustomerLoyalties
            .FirstOrDefaultAsync(l => l.CustomerId == customerId && l.ProgramId == reward.ProgramId);

        if (loyalty == null)
            return BadRequest(new { message = "Customer is not enrolled in this reward's loyalty program" });

        // Check if customer has enough points
        if (loyalty.CurrentPoints < reward.PointsCost)
            return BadRequest(new { message = "Insufficient points" });

        // Check max redemptions per customer
        if (reward.MaxRedemptionsPerCustomer.HasValue)
        {
            var customerRedemptions = await _context.CustomerRewards
                .CountAsync(cr => cr.CustomerId == customerId && cr.RewardId == id);

            if (customerRedemptions >= reward.MaxRedemptionsPerCustomer.Value)
                return BadRequest(new { message = "Maximum redemptions reached for this reward" });
        }

        // Check total available
        if (reward.TotalAvailable.HasValue && reward.TotalRedeemed >= reward.TotalAvailable.Value)
            return BadRequest(new { message = "Reward is no longer available" });

        // Deduct points
        var transaction = new PointsTransaction
        {
            CustomerLoyaltyId = loyalty.Id,
            TransactionType = "redeem",
            Points = -reward.PointsCost,
            BalanceBefore = loyalty.CurrentPoints,
            BalanceAfter = loyalty.CurrentPoints - reward.PointsCost,
            Description = $"Redeemed for: {reward.Name}",
            ProcessedAt = DateTime.UtcNow
        };

        loyalty.CurrentPoints -= reward.PointsCost;
        loyalty.LastActivityAt = DateTime.UtcNow;

        // Create customer reward
        var customerReward = new CustomerReward
        {
            CustomerId = customerId,
            RewardId = id,
            Code = GenerateRewardCode(),
            Status = "available",
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = reward.ValidUntil
        };

        reward.TotalRedeemed++;

        _context.PointsTransactions.Add(transaction);
        _context.CustomerRewards.Add(customerReward);
        await _context.SaveChangesAsync();

        var dto = new CustomerRewardDto
        {
            Id = customerReward.Id,
            CustomerId = customerId,
            RewardId = id,
            RewardName = reward.Name,
            RewardType = reward.Type,
            Code = customerReward.Code,
            Status = customerReward.Status,
            IssuedAt = customerReward.IssuedAt,
            ExpiresAt = customerReward.ExpiresAt
        };

        dto.AddSelfLink($"/api/customers/{customerId}/rewards/{customerReward.Id}");

        return Ok(dto);
    }

    private static string GenerateRewardCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private static RewardDto MapToDto(Reward reward)
    {
        return new RewardDto
        {
            Id = reward.Id,
            TenantId = reward.TenantId,
            ProgramId = reward.ProgramId,
            Name = reward.Name,
            Description = reward.Description,
            Type = reward.Type,
            PointsCost = reward.PointsCost,
            Value = reward.Value,
            MenuItemId = reward.MenuItemId,
            DiscountPercentage = reward.DiscountPercentage,
            MaxRedemptionsPerCustomer = reward.MaxRedemptionsPerCustomer,
            TotalAvailable = reward.TotalAvailable,
            TotalRedeemed = reward.TotalRedeemed,
            ValidFrom = reward.ValidFrom,
            ValidUntil = reward.ValidUntil,
            TermsAndConditions = reward.TermsAndConditions,
            ImageUrl = reward.ImageUrl,
            IsActive = reward.IsActive,
            CreatedAt = reward.CreatedAt
        };
    }
}

[ApiController]
[Route("api/customers/{customerId:guid}/rewards")]
public class CustomerRewardsController : ControllerBase
{
    private readonly CustomersDbContext _context;

    private Guid TenantId => Guid.Parse(Request.Headers["X-Tenant-Id"].FirstOrDefault()
        ?? "00000000-0000-0000-0000-000000000001");

    public CustomerRewardsController(CustomersDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<CustomerRewardDto>>> GetAll(
        Guid customerId,
        [FromQuery] string? status = null)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var query = _context.CustomerRewards
            .Include(r => r.Reward)
            .Where(r => r.CustomerId == customerId);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(r => r.Status == status);
        }

        var rewards = await query
            .OrderByDescending(r => r.IssuedAt)
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
            $"/api/customers/{customerId}/rewards",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerRewardDto>> GetById(Guid customerId, Guid id)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var customerReward = await _context.CustomerRewards
            .Include(r => r.Reward)
            .FirstOrDefaultAsync(r => r.Id == id && r.CustomerId == customerId);

        if (customerReward == null)
            return NotFound();

        var dto = new CustomerRewardDto
        {
            Id = customerReward.Id,
            CustomerId = customerReward.CustomerId,
            RewardId = customerReward.RewardId,
            RewardName = customerReward.Reward?.Name ?? "",
            RewardType = customerReward.Reward?.Type ?? "",
            Code = customerReward.Code,
            Status = customerReward.Status,
            IssuedAt = customerReward.IssuedAt,
            ExpiresAt = customerReward.ExpiresAt,
            RedeemedAt = customerReward.RedeemedAt,
            RedeemedOrderId = customerReward.RedeemedOrderId,
            RedeemedLocationId = customerReward.RedeemedLocationId
        };

        dto.AddSelfLink($"/api/customers/{customerId}/rewards/{id}");
        dto.AddLink("reward", $"/api/rewards/{customerReward.RewardId}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/redeem")]
    public async Task<ActionResult<CustomerRewardDto>> Redeem(
        Guid customerId,
        Guid id,
        [FromBody] RedeemCustomerRewardRequest request)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var customerReward = await _context.CustomerRewards
            .Include(r => r.Reward)
            .FirstOrDefaultAsync(r => r.Id == id && r.CustomerId == customerId);

        if (customerReward == null)
            return NotFound();

        if (customerReward.Status != "available")
            return BadRequest(new { message = "Reward is not available for redemption" });

        if (customerReward.ExpiresAt.HasValue && customerReward.ExpiresAt < DateTime.UtcNow)
        {
            customerReward.Status = "expired";
            await _context.SaveChangesAsync();
            return BadRequest(new { message = "Reward has expired" });
        }

        customerReward.Status = "redeemed";
        customerReward.RedeemedAt = DateTime.UtcNow;
        customerReward.RedeemedOrderId = request.OrderId;
        customerReward.RedeemedLocationId = request.LocationId;

        await _context.SaveChangesAsync();

        var dto = new CustomerRewardDto
        {
            Id = customerReward.Id,
            CustomerId = customerReward.CustomerId,
            RewardId = customerReward.RewardId,
            RewardName = customerReward.Reward?.Name ?? "",
            RewardType = customerReward.Reward?.Type ?? "",
            Code = customerReward.Code,
            Status = customerReward.Status,
            IssuedAt = customerReward.IssuedAt,
            ExpiresAt = customerReward.ExpiresAt,
            RedeemedAt = customerReward.RedeemedAt,
            RedeemedOrderId = customerReward.RedeemedOrderId,
            RedeemedLocationId = customerReward.RedeemedLocationId
        };

        dto.AddSelfLink($"/api/customers/{customerId}/rewards/{id}");

        return Ok(dto);
    }
}
