using DarkVelocity.Customers.Api.Data;
using DarkVelocity.Customers.Api.Dtos;
using DarkVelocity.Customers.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Customers.Api.Controllers;

[ApiController]
[Route("api/customers/{customerId:guid}/addresses")]
public class CustomerAddressesController : ControllerBase
{
    private readonly CustomersDbContext _context;

    private Guid TenantId => Guid.Parse(Request.Headers["X-Tenant-Id"].FirstOrDefault()
        ?? "00000000-0000-0000-0000-000000000001");

    public CustomerAddressesController(CustomersDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<CustomerAddressDto>>> GetAll(Guid customerId)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var addresses = await _context.CustomerAddresses
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.Label)
            .ToListAsync();

        var dtos = addresses.Select(a => MapToDto(a, customerId)).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/customers/{customerId}/addresses/{dto.Id}");
        }

        return Ok(HalCollection<CustomerAddressDto>.Create(
            dtos,
            $"/api/customers/{customerId}/addresses",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerAddressDto>> GetById(Guid customerId, Guid id)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var address = await _context.CustomerAddresses
            .FirstOrDefaultAsync(a => a.Id == id && a.CustomerId == customerId);

        if (address == null)
            return NotFound();

        var dto = MapToDto(address, customerId);
        dto.AddSelfLink($"/api/customers/{customerId}/addresses/{address.Id}");
        dto.AddLink("customer", $"/api/customers/{customerId}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerAddressDto>> Create(Guid customerId, [FromBody] CreateAddressRequest request)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        // If this is the first address or marked as default, update other addresses
        if (request.IsDefault)
        {
            var existingAddresses = await _context.CustomerAddresses
                .Where(a => a.CustomerId == customerId && a.IsDefault)
                .ToListAsync();

            foreach (var existing in existingAddresses)
            {
                existing.IsDefault = false;
            }
        }

        var address = new CustomerAddress
        {
            CustomerId = customerId,
            Label = request.Label,
            Street = request.Street,
            Street2 = request.Street2,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Country = request.Country,
            IsDefault = request.IsDefault,
            DeliveryInstructions = request.DeliveryInstructions
        };

        // If this is the first address, make it default
        var hasAddresses = await _context.CustomerAddresses.AnyAsync(a => a.CustomerId == customerId);
        if (!hasAddresses)
        {
            address.IsDefault = true;
        }

        _context.CustomerAddresses.Add(address);
        await _context.SaveChangesAsync();

        var dto = MapToDto(address, customerId);
        dto.AddSelfLink($"/api/customers/{customerId}/addresses/{address.Id}");

        return CreatedAtAction(nameof(GetById), new { customerId, id = address.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerAddressDto>> Update(Guid customerId, Guid id, [FromBody] UpdateAddressRequest request)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var address = await _context.CustomerAddresses
            .FirstOrDefaultAsync(a => a.Id == id && a.CustomerId == customerId);

        if (address == null)
            return NotFound();

        if (request.Label != null) address.Label = request.Label;
        if (request.Street != null) address.Street = request.Street;
        if (request.Street2 != null) address.Street2 = request.Street2;
        if (request.City != null) address.City = request.City;
        if (request.State != null) address.State = request.State;
        if (request.PostalCode != null) address.PostalCode = request.PostalCode;
        if (request.Country != null) address.Country = request.Country;
        if (request.DeliveryInstructions != null) address.DeliveryInstructions = request.DeliveryInstructions;

        if (request.IsDefault == true && !address.IsDefault)
        {
            // Clear default from other addresses
            var existingDefaults = await _context.CustomerAddresses
                .Where(a => a.CustomerId == customerId && a.IsDefault && a.Id != id)
                .ToListAsync();

            foreach (var existing in existingDefaults)
            {
                existing.IsDefault = false;
            }

            address.IsDefault = true;
        }

        await _context.SaveChangesAsync();

        var dto = MapToDto(address, customerId);
        dto.AddSelfLink($"/api/customers/{customerId}/addresses/{address.Id}");

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid customerId, Guid id)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

        if (customer == null)
            return NotFound();

        var address = await _context.CustomerAddresses
            .FirstOrDefaultAsync(a => a.Id == id && a.CustomerId == customerId);

        if (address == null)
            return NotFound();

        var wasDefault = address.IsDefault;

        _context.CustomerAddresses.Remove(address);
        await _context.SaveChangesAsync();

        // If the deleted address was default, make the first remaining address default
        if (wasDefault)
        {
            var firstAddress = await _context.CustomerAddresses
                .Where(a => a.CustomerId == customerId)
                .FirstOrDefaultAsync();

            if (firstAddress != null)
            {
                firstAddress.IsDefault = true;
                await _context.SaveChangesAsync();
            }
        }

        return NoContent();
    }

    private static CustomerAddressDto MapToDto(CustomerAddress address, Guid customerId)
    {
        return new CustomerAddressDto
        {
            Id = address.Id,
            CustomerId = customerId,
            Label = address.Label,
            Street = address.Street,
            Street2 = address.Street2,
            City = address.City,
            State = address.State,
            PostalCode = address.PostalCode,
            Country = address.Country,
            IsDefault = address.IsDefault,
            DeliveryInstructions = address.DeliveryInstructions
        };
    }
}
