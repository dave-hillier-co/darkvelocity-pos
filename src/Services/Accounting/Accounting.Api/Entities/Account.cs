using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Accounting.Api.Entities;

/// <summary>
/// Represents an account in the Chart of Accounts.
/// </summary>
public class Account : BaseEntity
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// Unique account code (e.g., "1000", "4100", "5200")
    /// </summary>
    public required string AccountCode { get; set; }

    /// <summary>
    /// Display name for the account
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Main type of account
    /// </summary>
    public AccountType AccountType { get; set; }

    /// <summary>
    /// Sub-classification (e.g., "Cash", "Receivables", "Sales")
    /// </summary>
    public string? SubType { get; set; }

    /// <summary>
    /// Parent account for hierarchical structure
    /// </summary>
    public Guid? ParentAccountId { get; set; }
    public Account? ParentAccount { get; set; }

    /// <summary>
    /// Child accounts in the hierarchy
    /// </summary>
    public ICollection<Account> ChildAccounts { get; set; } = new List<Account>();

    /// <summary>
    /// System accounts are auto-created and cannot be deleted
    /// </summary>
    public bool IsSystemAccount { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Default tax code for entries to this account
    /// </summary>
    public string? TaxCode { get; set; }

    /// <summary>
    /// External reference for ERP mapping (e.g., DATEV account number)
    /// </summary>
    public string? ExternalReference { get; set; }

    /// <summary>
    /// Description of the account's purpose
    /// </summary>
    public string? Description { get; set; }
}

public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Revenue,
    Expense
}
