using DarkVelocity.Accounting.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Accounting.Api.Dtos;

// Response DTOs

public class AccountDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public string? SubType { get; set; }
    public Guid? ParentAccountId { get; set; }
    public bool IsSystemAccount { get; set; }
    public bool IsActive { get; set; }
    public string? TaxCode { get; set; }
    public string? ExternalReference { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AccountTreeDto : HalResource
{
    public Guid Id { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public bool IsActive { get; set; }
    public List<AccountTreeDto> Children { get; set; } = new();
}

// Request DTOs

public record CreateAccountRequest(
    string AccountCode,
    string Name,
    AccountType AccountType,
    string? SubType = null,
    Guid? ParentAccountId = null,
    string? TaxCode = null,
    string? ExternalReference = null,
    string? Description = null);

public record UpdateAccountRequest(
    string Name,
    string? SubType = null,
    Guid? ParentAccountId = null,
    bool? IsActive = null,
    string? TaxCode = null,
    string? ExternalReference = null,
    string? Description = null);

public record ImportAccountsRequest(
    string TemplateType,
    List<CreateAccountRequest> Accounts);
