using DarkVelocity.Accounting.Api.Data;
using DarkVelocity.Accounting.Api.Dtos;
using DarkVelocity.Accounting.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Accounting.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly AccountingDbContext _context;

    // TODO: In multi-tenant implementation, inject ITenantContext to get TenantId
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public AccountsController(AccountingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<AccountDto>>> GetAll(
        [FromQuery] AccountType? accountType = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int limit = 100)
    {
        var query = _context.Accounts
            .Where(a => a.TenantId == DefaultTenantId);

        if (accountType.HasValue)
        {
            query = query.Where(a => a.AccountType == accountType.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(a => a.IsActive == isActive.Value);
        }

        var accounts = await query
            .OrderBy(a => a.AccountCode)
            .Take(limit)
            .ToListAsync();

        var dtos = accounts.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/accounts/{dto.Id}");
        }

        return Ok(HalCollection<AccountDto>.Create(
            dtos,
            "/api/accounts",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountDto>> GetById(Guid id)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == DefaultTenantId);

        if (account == null)
            return NotFound();

        var dto = MapToDto(account);
        dto.AddSelfLink($"/api/accounts/{account.Id}");

        if (account.ParentAccountId.HasValue)
        {
            dto.AddLink("parent", $"/api/accounts/{account.ParentAccountId}");
        }

        return Ok(dto);
    }

    [HttpGet("tree")]
    public async Task<ActionResult<HalCollection<AccountTreeDto>>> GetTree()
    {
        var accounts = await _context.Accounts
            .Where(a => a.TenantId == DefaultTenantId && a.IsActive)
            .OrderBy(a => a.AccountCode)
            .ToListAsync();

        var rootAccounts = accounts.Where(a => a.ParentAccountId == null).ToList();
        var dtos = rootAccounts.Select(a => BuildTreeDto(a, accounts)).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/accounts/{dto.Id}");
        }

        return Ok(HalCollection<AccountTreeDto>.Create(
            dtos,
            "/api/accounts/tree",
            dtos.Count
        ));
    }

    [HttpPost]
    public async Task<ActionResult<AccountDto>> Create([FromBody] CreateAccountRequest request)
    {
        // Validate unique account code
        var existingAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.TenantId == DefaultTenantId && a.AccountCode == request.AccountCode);

        if (existingAccount != null)
            return BadRequest(new { message = "Account code already exists" });

        // Validate parent account if specified
        if (request.ParentAccountId.HasValue)
        {
            var parentExists = await _context.Accounts
                .AnyAsync(a => a.Id == request.ParentAccountId.Value && a.TenantId == DefaultTenantId);

            if (!parentExists)
                return BadRequest(new { message = "Parent account not found" });
        }

        var account = new Account
        {
            TenantId = DefaultTenantId,
            AccountCode = request.AccountCode,
            Name = request.Name,
            AccountType = request.AccountType,
            SubType = request.SubType,
            ParentAccountId = request.ParentAccountId,
            TaxCode = request.TaxCode,
            ExternalReference = request.ExternalReference,
            Description = request.Description,
            IsSystemAccount = false,
            IsActive = true
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        var dto = MapToDto(account);
        dto.AddSelfLink($"/api/accounts/{account.Id}");

        return CreatedAtAction(nameof(GetById), new { id = account.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AccountDto>> Update(Guid id, [FromBody] UpdateAccountRequest request)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == DefaultTenantId);

        if (account == null)
            return NotFound();

        if (account.IsSystemAccount)
            return BadRequest(new { message = "Cannot modify system accounts" });

        // Validate parent account if specified
        if (request.ParentAccountId.HasValue)
        {
            if (request.ParentAccountId.Value == id)
                return BadRequest(new { message = "Account cannot be its own parent" });

            var parentExists = await _context.Accounts
                .AnyAsync(a => a.Id == request.ParentAccountId.Value && a.TenantId == DefaultTenantId);

            if (!parentExists)
                return BadRequest(new { message = "Parent account not found" });
        }

        account.Name = request.Name;
        account.SubType = request.SubType;
        account.ParentAccountId = request.ParentAccountId;
        account.TaxCode = request.TaxCode;
        account.ExternalReference = request.ExternalReference;
        account.Description = request.Description;

        if (request.IsActive.HasValue)
        {
            account.IsActive = request.IsActive.Value;
        }

        await _context.SaveChangesAsync();

        var dto = MapToDto(account);
        dto.AddSelfLink($"/api/accounts/{account.Id}");

        return Ok(dto);
    }

    [HttpPost("import")]
    public async Task<ActionResult<HalCollection<AccountDto>>> Import([FromBody] ImportAccountsRequest request)
    {
        var createdAccounts = new List<Account>();

        foreach (var accountRequest in request.Accounts)
        {
            // Skip if account code already exists
            var existingAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.TenantId == DefaultTenantId && a.AccountCode == accountRequest.AccountCode);

            if (existingAccount != null)
                continue;

            var account = new Account
            {
                TenantId = DefaultTenantId,
                AccountCode = accountRequest.AccountCode,
                Name = accountRequest.Name,
                AccountType = accountRequest.AccountType,
                SubType = accountRequest.SubType,
                ParentAccountId = accountRequest.ParentAccountId,
                TaxCode = accountRequest.TaxCode,
                ExternalReference = accountRequest.ExternalReference,
                Description = accountRequest.Description,
                IsSystemAccount = false,
                IsActive = true
            };

            _context.Accounts.Add(account);
            createdAccounts.Add(account);
        }

        await _context.SaveChangesAsync();

        var dtos = createdAccounts.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/accounts/{dto.Id}");
        }

        return Ok(HalCollection<AccountDto>.Create(
            dtos,
            "/api/accounts/import",
            dtos.Count
        ));
    }

    private static AccountDto MapToDto(Account account)
    {
        return new AccountDto
        {
            Id = account.Id,
            TenantId = account.TenantId,
            AccountCode = account.AccountCode,
            Name = account.Name,
            AccountType = account.AccountType,
            SubType = account.SubType,
            ParentAccountId = account.ParentAccountId,
            IsSystemAccount = account.IsSystemAccount,
            IsActive = account.IsActive,
            TaxCode = account.TaxCode,
            ExternalReference = account.ExternalReference,
            Description = account.Description,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        };
    }

    private AccountTreeDto BuildTreeDto(Account account, List<Account> allAccounts)
    {
        var dto = new AccountTreeDto
        {
            Id = account.Id,
            AccountCode = account.AccountCode,
            Name = account.Name,
            AccountType = account.AccountType,
            IsActive = account.IsActive,
            Children = allAccounts
                .Where(a => a.ParentAccountId == account.Id)
                .Select(a => BuildTreeDto(a, allAccounts))
                .ToList()
        };

        return dto;
    }
}
