using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Commands
// ============================================================================

/// <summary>
/// Command to initialize the chart of accounts.
/// </summary>
[GenerateSerializer]
public record InitializeChartOfAccountsCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid InitializedBy,
    [property: Id(2)] bool CreateDefaultAccounts = true,
    [property: Id(3)] string Currency = "USD");

/// <summary>
/// Command to add an account to the chart.
/// </summary>
[GenerateSerializer]
public record AddAccountToChartCommand(
    [property: Id(0)] string AccountNumber,
    [property: Id(1)] string Name,
    [property: Id(2)] AccountType AccountType,
    [property: Id(3)] NormalBalance NormalBalance,
    [property: Id(4)] Guid AddedBy,
    [property: Id(5)] string? ParentAccountNumber = null,
    [property: Id(6)] string? Description = null,
    [property: Id(7)] bool IsSystemAccount = false,
    [property: Id(8)] string? TaxCode = null);

/// <summary>
/// Command to update an account in the chart.
/// </summary>
[GenerateSerializer]
public record UpdateChartAccountCommand(
    [property: Id(0)] string AccountNumber,
    [property: Id(1)] Guid UpdatedBy,
    [property: Id(2)] string? Name = null,
    [property: Id(3)] string? Description = null,
    [property: Id(4)] string? ParentAccountNumber = null,
    [property: Id(5)] string? TaxCode = null);

/// <summary>
/// Command to deactivate an account in the chart.
/// </summary>
[GenerateSerializer]
public record DeactivateChartAccountCommand(
    [property: Id(0)] string AccountNumber,
    [property: Id(1)] Guid DeactivatedBy,
    [property: Id(2)] string? Reason = null);

/// <summary>
/// Command to reactivate an account in the chart.
/// </summary>
[GenerateSerializer]
public record ReactivateChartAccountCommand(
    [property: Id(0)] string AccountNumber,
    [property: Id(1)] Guid ReactivatedBy);

// ============================================================================
// Results
// ============================================================================

/// <summary>
/// Account entry in the chart of accounts.
/// </summary>
[GenerateSerializer]
public record ChartAccount(
    [property: Id(0)] string AccountNumber,
    [property: Id(1)] string Name,
    [property: Id(2)] AccountType AccountType,
    [property: Id(3)] NormalBalance NormalBalance,
    [property: Id(4)] bool IsActive,
    [property: Id(5)] bool IsSystemAccount,
    [property: Id(6)] string? ParentAccountNumber,
    [property: Id(7)] string? Description,
    [property: Id(8)] string? TaxCode,
    [property: Id(9)] int Level,
    [property: Id(10)] Guid? LinkedAccountGrainId);

/// <summary>
/// Hierarchical view of accounts.
/// </summary>
[GenerateSerializer]
public record ChartAccountHierarchy(
    [property: Id(0)] ChartAccount Account,
    [property: Id(1)] IReadOnlyList<ChartAccountHierarchy> Children);

/// <summary>
/// Summary of the chart of accounts.
/// </summary>
[GenerateSerializer]
public record ChartOfAccountsSummary(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] int TotalAccounts,
    [property: Id(2)] int ActiveAccounts,
    [property: Id(3)] int AssetAccounts,
    [property: Id(4)] int LiabilityAccounts,
    [property: Id(5)] int EquityAccounts,
    [property: Id(6)] int RevenueAccounts,
    [property: Id(7)] int ExpenseAccounts,
    [property: Id(8)] string Currency,
    [property: Id(9)] DateTime LastModifiedAt);

// ============================================================================
// State
// ============================================================================

/// <summary>
/// Normal balance direction for an account.
/// </summary>
public enum NormalBalance
{
    /// <summary>Account increases with debits (Assets, Expenses).</summary>
    Debit,
    /// <summary>Account increases with credits (Liabilities, Equity, Revenue).</summary>
    Credit
}

/// <summary>
/// State for the Chart of Accounts grain.
/// </summary>
[GenerateSerializer]
public sealed class ChartOfAccountsState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public bool IsInitialized { get; set; }
    [Id(2)] public string Currency { get; set; } = "USD";
    [Id(3)] public Dictionary<string, ChartAccountEntry> Accounts { get; set; } = [];
    [Id(4)] public DateTime CreatedAt { get; set; }
    [Id(5)] public Guid CreatedBy { get; set; }
    [Id(6)] public DateTime LastModifiedAt { get; set; }
    [Id(7)] public Guid? LastModifiedBy { get; set; }
    [Id(8)] public int Version { get; set; }
}

/// <summary>
/// Internal representation of a chart account entry.
/// </summary>
[GenerateSerializer]
public sealed record ChartAccountEntry
{
    [Id(0)] public required string AccountNumber { get; init; }
    [Id(1)] public required string Name { get; set; }
    [Id(2)] public required AccountType AccountType { get; init; }
    [Id(3)] public required NormalBalance NormalBalance { get; init; }
    [Id(4)] public bool IsActive { get; set; } = true;
    [Id(5)] public bool IsSystemAccount { get; init; }
    [Id(6)] public string? ParentAccountNumber { get; set; }
    [Id(7)] public string? Description { get; set; }
    [Id(8)] public string? TaxCode { get; set; }
    [Id(9)] public Guid? LinkedAccountGrainId { get; set; }
    [Id(10)] public DateTime CreatedAt { get; init; }
    [Id(11)] public DateTime? DeactivatedAt { get; set; }
}

// ============================================================================
// Grain Interface
// ============================================================================

/// <summary>
/// Grain interface for managing the Chart of Accounts.
/// Provides hierarchical account structure with account numbers and types.
/// Key format: {orgId}:chartofaccounts
/// </summary>
public interface IChartOfAccountsGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initializes the chart of accounts for an organization.
    /// </summary>
    Task InitializeAsync(InitializeChartOfAccountsCommand command);

    /// <summary>
    /// Checks if the chart has been initialized.
    /// </summary>
    Task<bool> IsInitializedAsync();

    /// <summary>
    /// Adds an account to the chart.
    /// </summary>
    Task<ChartAccount> AddAccountAsync(AddAccountToChartCommand command);

    /// <summary>
    /// Updates an account in the chart.
    /// </summary>
    Task<ChartAccount> UpdateAccountAsync(UpdateChartAccountCommand command);

    /// <summary>
    /// Deactivates an account (soft delete).
    /// </summary>
    Task DeactivateAccountAsync(DeactivateChartAccountCommand command);

    /// <summary>
    /// Reactivates a previously deactivated account.
    /// </summary>
    Task<ChartAccount> ReactivateAccountAsync(ReactivateChartAccountCommand command);

    /// <summary>
    /// Gets an account by account number.
    /// </summary>
    Task<ChartAccount?> GetAccountAsync(string accountNumber);

    /// <summary>
    /// Gets all accounts as a flat list.
    /// </summary>
    Task<IReadOnlyList<ChartAccount>> GetAllAccountsAsync(bool includeInactive = false);

    /// <summary>
    /// Gets accounts by type.
    /// </summary>
    Task<IReadOnlyList<ChartAccount>> GetAccountsByTypeAsync(AccountType accountType);

    /// <summary>
    /// Gets the hierarchical view of accounts.
    /// </summary>
    Task<IReadOnlyList<ChartAccountHierarchy>> GetHierarchyAsync();

    /// <summary>
    /// Gets children of a specific account.
    /// </summary>
    Task<IReadOnlyList<ChartAccount>> GetChildAccountsAsync(string parentAccountNumber);

    /// <summary>
    /// Gets a summary of the chart.
    /// </summary>
    Task<ChartOfAccountsSummary> GetSummaryAsync();

    /// <summary>
    /// Links an account number to an AccountGrain.
    /// </summary>
    Task LinkAccountGrainAsync(string accountNumber, Guid accountGrainId);

    /// <summary>
    /// Validates that an account number exists and is active.
    /// </summary>
    Task<bool> ValidateAccountAsync(string accountNumber);
}

// ============================================================================
// Grain Implementation
// ============================================================================

/// <summary>
/// Orleans grain for managing the Chart of Accounts.
/// </summary>
public class ChartOfAccountsGrain : Grain, IChartOfAccountsGrain
{
    private readonly IPersistentState<ChartOfAccountsState> _state;
    private readonly ILogger<ChartOfAccountsGrain> _logger;

    public ChartOfAccountsGrain(
        [PersistentState("chart-of-accounts", "OrleansStorage")]
        IPersistentState<ChartOfAccountsState> state,
        ILogger<ChartOfAccountsGrain> logger)
    {
        _state = state;
        _logger = logger;
    }

    public async Task InitializeAsync(InitializeChartOfAccountsCommand command)
    {
        if (_state.State.IsInitialized)
            throw new InvalidOperationException("Chart of accounts already initialized");

        var now = DateTime.UtcNow;
        _state.State.OrganizationId = command.OrganizationId;
        _state.State.Currency = command.Currency;
        _state.State.IsInitialized = true;
        _state.State.CreatedAt = now;
        _state.State.CreatedBy = command.InitializedBy;
        _state.State.LastModifiedAt = now;
        _state.State.Version = 1;

        if (command.CreateDefaultAccounts)
        {
            CreateDefaultAccounts(now);
        }

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Chart of accounts initialized for organization {OrgId} with {Count} default accounts",
            command.OrganizationId,
            _state.State.Accounts.Count);
    }

    private void CreateDefaultAccounts(DateTime now)
    {
        // Assets (1xxx)
        AddDefaultAccount("1000", "Assets", AccountType.Asset, NormalBalance.Debit, null, now, isSystem: true);
        AddDefaultAccount("1100", "Cash and Cash Equivalents", AccountType.Asset, NormalBalance.Debit, "1000", now);
        AddDefaultAccount("1110", "Cash on Hand", AccountType.Asset, NormalBalance.Debit, "1100", now);
        AddDefaultAccount("1120", "Cash in Bank", AccountType.Asset, NormalBalance.Debit, "1100", now);
        AddDefaultAccount("1130", "Petty Cash", AccountType.Asset, NormalBalance.Debit, "1100", now);
        AddDefaultAccount("1200", "Accounts Receivable", AccountType.Asset, NormalBalance.Debit, "1000", now);
        AddDefaultAccount("1300", "Inventory", AccountType.Asset, NormalBalance.Debit, "1000", now);
        AddDefaultAccount("1310", "Food Inventory", AccountType.Asset, NormalBalance.Debit, "1300", now);
        AddDefaultAccount("1320", "Beverage Inventory", AccountType.Asset, NormalBalance.Debit, "1300", now);
        AddDefaultAccount("1330", "Supplies Inventory", AccountType.Asset, NormalBalance.Debit, "1300", now);
        AddDefaultAccount("1500", "Fixed Assets", AccountType.Asset, NormalBalance.Debit, "1000", now);
        AddDefaultAccount("1510", "Equipment", AccountType.Asset, NormalBalance.Debit, "1500", now);
        AddDefaultAccount("1520", "Furniture and Fixtures", AccountType.Asset, NormalBalance.Debit, "1500", now);
        AddDefaultAccount("1590", "Accumulated Depreciation", AccountType.Asset, NormalBalance.Credit, "1500", now);

        // Liabilities (2xxx)
        AddDefaultAccount("2000", "Liabilities", AccountType.Liability, NormalBalance.Credit, null, now, isSystem: true);
        AddDefaultAccount("2100", "Accounts Payable", AccountType.Liability, NormalBalance.Credit, "2000", now);
        AddDefaultAccount("2200", "Accrued Expenses", AccountType.Liability, NormalBalance.Credit, "2000", now);
        AddDefaultAccount("2210", "Wages Payable", AccountType.Liability, NormalBalance.Credit, "2200", now);
        AddDefaultAccount("2220", "Tips Payable", AccountType.Liability, NormalBalance.Credit, "2200", now);
        AddDefaultAccount("2300", "Sales Tax Payable", AccountType.Liability, NormalBalance.Credit, "2000", now);
        AddDefaultAccount("2400", "Gift Card Liability", AccountType.Liability, NormalBalance.Credit, "2000", now);
        AddDefaultAccount("2500", "Unearned Revenue", AccountType.Liability, NormalBalance.Credit, "2000", now);
        AddDefaultAccount("2600", "Long-Term Debt", AccountType.Liability, NormalBalance.Credit, "2000", now);

        // Equity (3xxx)
        AddDefaultAccount("3000", "Equity", AccountType.Equity, NormalBalance.Credit, null, now, isSystem: true);
        AddDefaultAccount("3100", "Owner's Capital", AccountType.Equity, NormalBalance.Credit, "3000", now);
        AddDefaultAccount("3200", "Owner's Drawings", AccountType.Equity, NormalBalance.Debit, "3000", now);
        AddDefaultAccount("3300", "Retained Earnings", AccountType.Equity, NormalBalance.Credit, "3000", now);
        AddDefaultAccount("3400", "Current Year Earnings", AccountType.Equity, NormalBalance.Credit, "3000", now);

        // Revenue (4xxx)
        AddDefaultAccount("4000", "Revenue", AccountType.Revenue, NormalBalance.Credit, null, now, isSystem: true);
        AddDefaultAccount("4100", "Food Sales", AccountType.Revenue, NormalBalance.Credit, "4000", now);
        AddDefaultAccount("4200", "Beverage Sales", AccountType.Revenue, NormalBalance.Credit, "4000", now);
        AddDefaultAccount("4210", "Alcoholic Beverage Sales", AccountType.Revenue, NormalBalance.Credit, "4200", now);
        AddDefaultAccount("4220", "Non-Alcoholic Beverage Sales", AccountType.Revenue, NormalBalance.Credit, "4200", now);
        AddDefaultAccount("4300", "Service Revenue", AccountType.Revenue, NormalBalance.Credit, "4000", now);
        AddDefaultAccount("4310", "Catering Revenue", AccountType.Revenue, NormalBalance.Credit, "4300", now);
        AddDefaultAccount("4320", "Delivery Fees", AccountType.Revenue, NormalBalance.Credit, "4300", now);
        AddDefaultAccount("4400", "Gift Card Sales", AccountType.Revenue, NormalBalance.Credit, "4000", now);
        AddDefaultAccount("4500", "Other Revenue", AccountType.Revenue, NormalBalance.Credit, "4000", now);
        AddDefaultAccount("4900", "Sales Discounts", AccountType.Revenue, NormalBalance.Debit, "4000", now);
        AddDefaultAccount("4910", "Sales Returns", AccountType.Revenue, NormalBalance.Debit, "4000", now);

        // Expenses (5xxx - COGS, 6xxx - Operating)
        AddDefaultAccount("5000", "Cost of Goods Sold", AccountType.Expense, NormalBalance.Debit, null, now, isSystem: true);
        AddDefaultAccount("5100", "Food Cost", AccountType.Expense, NormalBalance.Debit, "5000", now);
        AddDefaultAccount("5200", "Beverage Cost", AccountType.Expense, NormalBalance.Debit, "5000", now);
        AddDefaultAccount("5300", "Packaging Cost", AccountType.Expense, NormalBalance.Debit, "5000", now);

        AddDefaultAccount("6000", "Operating Expenses", AccountType.Expense, NormalBalance.Debit, null, now, isSystem: true);
        AddDefaultAccount("6100", "Payroll Expenses", AccountType.Expense, NormalBalance.Debit, "6000", now);
        AddDefaultAccount("6110", "Wages and Salaries", AccountType.Expense, NormalBalance.Debit, "6100", now);
        AddDefaultAccount("6120", "Payroll Taxes", AccountType.Expense, NormalBalance.Debit, "6100", now);
        AddDefaultAccount("6130", "Employee Benefits", AccountType.Expense, NormalBalance.Debit, "6100", now);
        AddDefaultAccount("6200", "Rent Expense", AccountType.Expense, NormalBalance.Debit, "6000", now);
        AddDefaultAccount("6300", "Utilities", AccountType.Expense, NormalBalance.Debit, "6000", now);
        AddDefaultAccount("6400", "Insurance", AccountType.Expense, NormalBalance.Debit, "6000", now);
        AddDefaultAccount("6500", "Marketing and Advertising", AccountType.Expense, NormalBalance.Debit, "6000", now);
        AddDefaultAccount("6600", "Supplies Expense", AccountType.Expense, NormalBalance.Debit, "6000", now);
        AddDefaultAccount("6700", "Repairs and Maintenance", AccountType.Expense, NormalBalance.Debit, "6000", now);
        AddDefaultAccount("6800", "Professional Fees", AccountType.Expense, NormalBalance.Debit, "6000", now);
        AddDefaultAccount("6900", "Depreciation Expense", AccountType.Expense, NormalBalance.Debit, "6000", now);
        AddDefaultAccount("6950", "Bank and Credit Card Fees", AccountType.Expense, NormalBalance.Debit, "6000", now);
        AddDefaultAccount("6990", "Miscellaneous Expense", AccountType.Expense, NormalBalance.Debit, "6000", now);
    }

    private void AddDefaultAccount(
        string accountNumber,
        string name,
        AccountType accountType,
        NormalBalance normalBalance,
        string? parentAccountNumber,
        DateTime now,
        bool isSystem = false)
    {
        _state.State.Accounts[accountNumber] = new ChartAccountEntry
        {
            AccountNumber = accountNumber,
            Name = name,
            AccountType = accountType,
            NormalBalance = normalBalance,
            ParentAccountNumber = parentAccountNumber,
            IsSystemAccount = isSystem,
            CreatedAt = now
        };
    }

    public Task<bool> IsInitializedAsync()
    {
        return Task.FromResult(_state.State.IsInitialized);
    }

    public async Task<ChartAccount> AddAccountAsync(AddAccountToChartCommand command)
    {
        EnsureInitialized();

        if (_state.State.Accounts.ContainsKey(command.AccountNumber))
            throw new InvalidOperationException($"Account {command.AccountNumber} already exists");

        if (command.ParentAccountNumber != null &&
            !_state.State.Accounts.ContainsKey(command.ParentAccountNumber))
            throw new InvalidOperationException($"Parent account {command.ParentAccountNumber} not found");

        var now = DateTime.UtcNow;
        var entry = new ChartAccountEntry
        {
            AccountNumber = command.AccountNumber,
            Name = command.Name,
            AccountType = command.AccountType,
            NormalBalance = command.NormalBalance,
            ParentAccountNumber = command.ParentAccountNumber,
            Description = command.Description,
            IsSystemAccount = command.IsSystemAccount,
            TaxCode = command.TaxCode,
            CreatedAt = now
        };

        _state.State.Accounts[command.AccountNumber] = entry;
        _state.State.LastModifiedAt = now;
        _state.State.LastModifiedBy = command.AddedBy;
        _state.State.Version++;

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Account {AccountNumber} ({Name}) added to chart of accounts",
            command.AccountNumber,
            command.Name);

        return ToChartAccount(entry);
    }

    public async Task<ChartAccount> UpdateAccountAsync(UpdateChartAccountCommand command)
    {
        EnsureInitialized();

        if (!_state.State.Accounts.TryGetValue(command.AccountNumber, out var entry))
            throw new InvalidOperationException($"Account {command.AccountNumber} not found");

        if (command.ParentAccountNumber != null &&
            !_state.State.Accounts.ContainsKey(command.ParentAccountNumber))
            throw new InvalidOperationException($"Parent account {command.ParentAccountNumber} not found");

        // Prevent circular references
        if (command.ParentAccountNumber == command.AccountNumber)
            throw new InvalidOperationException("Account cannot be its own parent");

        if (command.Name != null) entry.Name = command.Name;
        if (command.Description != null) entry.Description = command.Description;
        if (command.ParentAccountNumber != null) entry.ParentAccountNumber = command.ParentAccountNumber;
        if (command.TaxCode != null) entry.TaxCode = command.TaxCode;

        _state.State.LastModifiedAt = DateTime.UtcNow;
        _state.State.LastModifiedBy = command.UpdatedBy;
        _state.State.Version++;

        await _state.WriteStateAsync();

        _logger.LogInformation("Account {AccountNumber} updated", command.AccountNumber);

        return ToChartAccount(entry);
    }

    public async Task DeactivateAccountAsync(DeactivateChartAccountCommand command)
    {
        EnsureInitialized();

        if (!_state.State.Accounts.TryGetValue(command.AccountNumber, out var entry))
            throw new InvalidOperationException($"Account {command.AccountNumber} not found");

        if (!entry.IsActive)
            throw new InvalidOperationException($"Account {command.AccountNumber} is already inactive");

        if (entry.IsSystemAccount)
            throw new InvalidOperationException("System accounts cannot be deactivated");

        // Check for active children
        var hasActiveChildren = _state.State.Accounts.Values
            .Any(a => a.ParentAccountNumber == command.AccountNumber && a.IsActive);

        if (hasActiveChildren)
            throw new InvalidOperationException("Cannot deactivate account with active child accounts");

        entry.IsActive = false;
        entry.DeactivatedAt = DateTime.UtcNow;
        _state.State.LastModifiedAt = DateTime.UtcNow;
        _state.State.LastModifiedBy = command.DeactivatedBy;
        _state.State.Version++;

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Account {AccountNumber} deactivated. Reason: {Reason}",
            command.AccountNumber,
            command.Reason ?? "Not specified");
    }

    public async Task<ChartAccount> ReactivateAccountAsync(ReactivateChartAccountCommand command)
    {
        EnsureInitialized();

        if (!_state.State.Accounts.TryGetValue(command.AccountNumber, out var entry))
            throw new InvalidOperationException($"Account {command.AccountNumber} not found");

        if (entry.IsActive)
            throw new InvalidOperationException($"Account {command.AccountNumber} is already active");

        entry.IsActive = true;
        entry.DeactivatedAt = null;
        _state.State.LastModifiedAt = DateTime.UtcNow;
        _state.State.LastModifiedBy = command.ReactivatedBy;
        _state.State.Version++;

        await _state.WriteStateAsync();

        _logger.LogInformation("Account {AccountNumber} reactivated", command.AccountNumber);

        return ToChartAccount(entry);
    }

    public Task<ChartAccount?> GetAccountAsync(string accountNumber)
    {
        if (!_state.State.Accounts.TryGetValue(accountNumber, out var entry))
            return Task.FromResult<ChartAccount?>(null);

        return Task.FromResult<ChartAccount?>(ToChartAccount(entry));
    }

    public Task<IReadOnlyList<ChartAccount>> GetAllAccountsAsync(bool includeInactive = false)
    {
        var accounts = _state.State.Accounts.Values
            .Where(a => includeInactive || a.IsActive)
            .OrderBy(a => a.AccountNumber)
            .Select(ToChartAccount)
            .ToList();

        return Task.FromResult<IReadOnlyList<ChartAccount>>(accounts);
    }

    public Task<IReadOnlyList<ChartAccount>> GetAccountsByTypeAsync(AccountType accountType)
    {
        var accounts = _state.State.Accounts.Values
            .Where(a => a.AccountType == accountType && a.IsActive)
            .OrderBy(a => a.AccountNumber)
            .Select(ToChartAccount)
            .ToList();

        return Task.FromResult<IReadOnlyList<ChartAccount>>(accounts);
    }

    public Task<IReadOnlyList<ChartAccountHierarchy>> GetHierarchyAsync()
    {
        var rootAccounts = _state.State.Accounts.Values
            .Where(a => a.ParentAccountNumber == null && a.IsActive)
            .OrderBy(a => a.AccountNumber)
            .Select(BuildHierarchy)
            .ToList();

        return Task.FromResult<IReadOnlyList<ChartAccountHierarchy>>(rootAccounts);
    }

    private ChartAccountHierarchy BuildHierarchy(ChartAccountEntry entry)
    {
        var children = _state.State.Accounts.Values
            .Where(a => a.ParentAccountNumber == entry.AccountNumber && a.IsActive)
            .OrderBy(a => a.AccountNumber)
            .Select(BuildHierarchy)
            .ToList();

        return new ChartAccountHierarchy(ToChartAccount(entry), children);
    }

    public Task<IReadOnlyList<ChartAccount>> GetChildAccountsAsync(string parentAccountNumber)
    {
        var children = _state.State.Accounts.Values
            .Where(a => a.ParentAccountNumber == parentAccountNumber && a.IsActive)
            .OrderBy(a => a.AccountNumber)
            .Select(ToChartAccount)
            .ToList();

        return Task.FromResult<IReadOnlyList<ChartAccount>>(children);
    }

    public Task<ChartOfAccountsSummary> GetSummaryAsync()
    {
        var accounts = _state.State.Accounts.Values;
        var activeAccounts = accounts.Where(a => a.IsActive).ToList();

        return Task.FromResult(new ChartOfAccountsSummary(
            _state.State.OrganizationId,
            accounts.Count,
            activeAccounts.Count,
            activeAccounts.Count(a => a.AccountType == AccountType.Asset),
            activeAccounts.Count(a => a.AccountType == AccountType.Liability),
            activeAccounts.Count(a => a.AccountType == AccountType.Equity),
            activeAccounts.Count(a => a.AccountType == AccountType.Revenue),
            activeAccounts.Count(a => a.AccountType == AccountType.Expense),
            _state.State.Currency,
            _state.State.LastModifiedAt));
    }

    public async Task LinkAccountGrainAsync(string accountNumber, Guid accountGrainId)
    {
        EnsureInitialized();

        if (!_state.State.Accounts.TryGetValue(accountNumber, out var entry))
            throw new InvalidOperationException($"Account {accountNumber} not found");

        entry.LinkedAccountGrainId = accountGrainId;
        _state.State.LastModifiedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<bool> ValidateAccountAsync(string accountNumber)
    {
        var isValid = _state.State.Accounts.TryGetValue(accountNumber, out var entry) && entry.IsActive;
        return Task.FromResult(isValid);
    }

    private void EnsureInitialized()
    {
        if (!_state.State.IsInitialized)
            throw new InvalidOperationException("Chart of accounts not initialized");
    }

    private ChartAccount ToChartAccount(ChartAccountEntry entry)
    {
        var level = CalculateLevel(entry.AccountNumber);
        return new ChartAccount(
            entry.AccountNumber,
            entry.Name,
            entry.AccountType,
            entry.NormalBalance,
            entry.IsActive,
            entry.IsSystemAccount,
            entry.ParentAccountNumber,
            entry.Description,
            entry.TaxCode,
            level,
            entry.LinkedAccountGrainId);
    }

    private int CalculateLevel(string accountNumber)
    {
        var level = 0;
        if (_state.State.Accounts.TryGetValue(accountNumber, out var entry))
        {
            var current = entry;
            while (current.ParentAccountNumber != null)
            {
                level++;
                if (!_state.State.Accounts.TryGetValue(current.ParentAccountNumber, out current))
                    break;
            }
        }
        return level;
    }
}
