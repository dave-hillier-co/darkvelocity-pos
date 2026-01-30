using DarkVelocity.Customers.Api.Data;
using DarkVelocity.Customers.Api.Dtos;
using DarkVelocity.Customers.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Customers.Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly CustomersDbContext _context;

    // In a real implementation, this would come from the authenticated user's JWT claims
    private Guid TenantId => Guid.Parse(Request.Headers["X-Tenant-Id"].FirstOrDefault()
        ?? "00000000-0000-0000-0000-000000000001");

    public CustomersController(CustomersDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<CustomerSummaryDto>>> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] string? tag = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var query = _context.Customers
            .Where(c => c.TenantId == TenantId);

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(c =>
                c.Email.ToLower().Contains(searchLower) ||
                c.FirstName.ToLower().Contains(searchLower) ||
                c.LastName.ToLower().Contains(searchLower) ||
                (c.Phone != null && c.Phone.Contains(search)));
        }

        if (!string.IsNullOrEmpty(tag))
        {
            query = query.Where(c => c.Tags.Contains(tag));
        }

        var total = await query.CountAsync();

        var customers = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = customers.Select(c => new CustomerSummaryDto
        {
            Id = c.Id,
            Email = c.Email,
            Phone = c.Phone,
            FullName = c.FullName,
            Tags = c.Tags,
            TotalVisits = c.TotalVisits,
            TotalSpend = c.TotalSpend,
            LastVisitAt = c.LastVisitAt
        }).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/customers/{dto.Id}");
        }

        return Ok(HalCollection<CustomerSummaryDto>.Create(
            dtos,
            "/api/customers",
            total
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> GetById(Guid id)
    {
        var customer = await _context.Customers
            .Include(c => c.Addresses)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var dto = MapToDto(customer);
        dto.AddSelfLink($"/api/customers/{customer.Id}");
        dto.AddLink("addresses", $"/api/customers/{customer.Id}/addresses");
        dto.AddLink("loyalty", $"/api/customers/{customer.Id}/loyalty");
        dto.AddLink("orders", $"/api/customers/{customer.Id}/orders");
        dto.AddLink("rewards", $"/api/customers/{customer.Id}/rewards");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create([FromBody] CreateCustomerRequest request)
    {
        // Check if customer with same email already exists
        var existing = await _context.Customers
            .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Email == request.Email);

        if (existing != null)
            return Conflict(new { message = "A customer with this email already exists" });

        var customer = new Customer
        {
            TenantId = TenantId,
            Email = request.Email,
            Phone = request.Phone,
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            PreferredLanguage = request.PreferredLanguage,
            MarketingOptIn = request.MarketingOptIn,
            SmsOptIn = request.SmsOptIn,
            Tags = request.Tags ?? new List<string>(),
            Notes = request.Notes,
            Source = request.Source,
            DefaultLocationId = request.DefaultLocationId,
            ExternalId = request.ExternalId
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        var dto = MapToDto(customer);
        dto.AddSelfLink($"/api/customers/{customer.Id}");

        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> Update(Guid id, [FromBody] UpdateCustomerRequest request)
    {
        var customer = await _context.Customers
            .Include(c => c.Addresses)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        if (request.Email != null)
        {
            // Check if email is being changed to one that already exists
            var existing = await _context.Customers
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Email == request.Email && c.Id != id);

            if (existing != null)
                return Conflict(new { message = "A customer with this email already exists" });

            customer.Email = request.Email;
        }

        if (request.Phone != null) customer.Phone = request.Phone;
        if (request.FirstName != null) customer.FirstName = request.FirstName;
        if (request.LastName != null) customer.LastName = request.LastName;
        if (request.DateOfBirth.HasValue) customer.DateOfBirth = request.DateOfBirth.Value;
        if (request.Gender != null) customer.Gender = request.Gender;
        if (request.PreferredLanguage != null) customer.PreferredLanguage = request.PreferredLanguage;
        if (request.MarketingOptIn.HasValue) customer.MarketingOptIn = request.MarketingOptIn.Value;
        if (request.SmsOptIn.HasValue) customer.SmsOptIn = request.SmsOptIn.Value;
        if (request.Tags != null) customer.Tags = request.Tags;
        if (request.Notes != null) customer.Notes = request.Notes;
        if (request.DefaultLocationId.HasValue) customer.DefaultLocationId = request.DefaultLocationId.Value;

        await _context.SaveChangesAsync();

        var dto = MapToDto(customer);
        dto.AddSelfLink($"/api/customers/{customer.Id}");

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        // Soft delete
        customer.IsDeleted = true;
        customer.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("lookup")]
    public async Task<ActionResult<CustomerDto>> Lookup([FromBody] CustomerLookupRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) && string.IsNullOrEmpty(request.Phone))
            return BadRequest(new { message = "Either email or phone is required" });

        var query = _context.Customers
            .Include(c => c.Addresses)
            .Where(c => c.TenantId == TenantId);

        if (!string.IsNullOrEmpty(request.Email))
        {
            query = query.Where(c => c.Email == request.Email);
        }
        else if (!string.IsNullOrEmpty(request.Phone))
        {
            query = query.Where(c => c.Phone == request.Phone);
        }

        var customer = await query.FirstOrDefaultAsync();

        if (customer == null)
            return NotFound();

        var dto = MapToDto(customer);
        dto.AddSelfLink($"/api/customers/{customer.Id}");

        return Ok(dto);
    }

    [HttpPost("merge")]
    public async Task<ActionResult<CustomerDto>> Merge([FromBody] MergeCustomersRequest request)
    {
        var primary = await _context.Customers
            .Include(c => c.Addresses)
            .Include(c => c.LoyaltyMemberships)
            .Include(c => c.Rewards)
            .FirstOrDefaultAsync(c => c.Id == request.PrimaryCustomerId && c.TenantId == TenantId);

        var secondary = await _context.Customers
            .Include(c => c.Addresses)
            .Include(c => c.LoyaltyMemberships)
            .Include(c => c.Rewards)
            .FirstOrDefaultAsync(c => c.Id == request.SecondaryCustomerId && c.TenantId == TenantId);

        if (primary == null || secondary == null)
            return NotFound();

        // Merge stats
        primary.TotalVisits += secondary.TotalVisits;
        primary.TotalSpend += secondary.TotalSpend;
        primary.AverageOrderValue = primary.TotalVisits > 0
            ? primary.TotalSpend / primary.TotalVisits
            : 0;

        if (secondary.LastVisitAt > primary.LastVisitAt)
            primary.LastVisitAt = secondary.LastVisitAt;

        // Merge tags
        foreach (var tag in secondary.Tags)
        {
            if (!primary.Tags.Contains(tag))
                primary.Tags.Add(tag);
        }

        // Move addresses
        foreach (var address in secondary.Addresses.ToList())
        {
            address.CustomerId = primary.Id;
            address.IsDefault = false;
        }

        // Soft delete secondary
        secondary.IsDeleted = true;
        secondary.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(primary);
        dto.AddSelfLink($"/api/customers/{primary.Id}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/tags")]
    public async Task<ActionResult<CustomerDto>> UpdateTags(Guid id, [FromBody] List<string> tags)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        customer.Tags = tags;
        await _context.SaveChangesAsync();

        var dto = MapToDto(customer);
        dto.AddSelfLink($"/api/customers/{customer.Id}");

        return Ok(dto);
    }

    [HttpGet("{id:guid}/orders")]
    public async Task<ActionResult<object>> GetOrders(Guid id, [FromQuery] int limit = 50)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        // In a real implementation, this would call the Orders service
        // For now, return a placeholder response
        return Ok(new
        {
            _links = new { self = new { href = $"/api/customers/{id}/orders" } },
            message = "Order history would be fetched from Orders service",
            customerId = id
        });
    }

    [HttpGet("{id:guid}/visits")]
    public async Task<ActionResult<object>> GetVisits(Guid id, [FromQuery] int limit = 50)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        // In a real implementation, this would aggregate visits across locations
        return Ok(new
        {
            _links = new { self = new { href = $"/api/customers/{id}/visits" } },
            totalVisits = customer.TotalVisits,
            lastVisitAt = customer.LastVisitAt,
            customerId = id
        });
    }

    private static CustomerDto MapToDto(Customer customer)
    {
        return new CustomerDto
        {
            Id = customer.Id,
            TenantId = customer.TenantId,
            ExternalId = customer.ExternalId,
            Email = customer.Email,
            Phone = customer.Phone,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            FullName = customer.FullName,
            DateOfBirth = customer.DateOfBirth,
            Gender = customer.Gender,
            PreferredLanguage = customer.PreferredLanguage,
            MarketingOptIn = customer.MarketingOptIn,
            SmsOptIn = customer.SmsOptIn,
            Tags = customer.Tags,
            Notes = customer.Notes,
            Source = customer.Source,
            DefaultLocationId = customer.DefaultLocationId,
            LastVisitAt = customer.LastVisitAt,
            TotalVisits = customer.TotalVisits,
            TotalSpend = customer.TotalSpend,
            AverageOrderValue = customer.AverageOrderValue,
            CreatedAt = customer.CreatedAt,
            Addresses = customer.Addresses.Select(a => new CustomerAddressDto
            {
                Id = a.Id,
                CustomerId = a.CustomerId,
                Label = a.Label,
                Street = a.Street,
                Street2 = a.Street2,
                City = a.City,
                State = a.State,
                PostalCode = a.PostalCode,
                Country = a.Country,
                IsDefault = a.IsDefault,
                DeliveryInstructions = a.DeliveryInstructions
            }).ToList()
        };
    }
}
