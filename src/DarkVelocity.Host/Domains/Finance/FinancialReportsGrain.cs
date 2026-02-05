using DarkVelocity.Host.State;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Report Types
// ============================================================================

/// <summary>
/// A line item in a financial report.
/// </summary>
[GenerateSerializer]
public record ReportLineItem(
    [property: Id(0)] string AccountNumber,
    [property: Id(1)] string AccountName,
    [property: Id(2)] int Level,
    [property: Id(3)] decimal Amount,
    [property: Id(4)] decimal? ComparisonAmount,
    [property: Id(5)] decimal? Variance,
    [property: Id(6)] decimal? VariancePercent,
    [property: Id(7)] bool IsSubtotal,
    [property: Id(8)] bool IsTotal);

/// <summary>
/// Section in a financial report.
/// </summary>
[GenerateSerializer]
public record ReportSection(
    [property: Id(0)] string Title,
    [property: Id(1)] IReadOnlyList<ReportLineItem> Lines,
    [property: Id(2)] decimal SectionTotal,
    [property: Id(3)] decimal? ComparisonTotal);

/// <summary>
/// Trial Balance report.
/// </summary>
[GenerateSerializer]
public record TrialBalanceReport(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] DateOnly AsOfDate,
    [property: Id(2)] IReadOnlyList<TrialBalanceLine> Lines,
    [property: Id(3)] decimal TotalDebits,
    [property: Id(4)] decimal TotalCredits,
    [property: Id(5)] bool IsBalanced,
    [property: Id(6)] DateTime GeneratedAt);

/// <summary>
/// Line in a trial balance.
/// </summary>
[GenerateSerializer]
public record TrialBalanceLine(
    [property: Id(0)] string AccountNumber,
    [property: Id(1)] string AccountName,
    [property: Id(2)] AccountType AccountType,
    [property: Id(3)] decimal DebitBalance,
    [property: Id(4)] decimal CreditBalance);

/// <summary>
/// Income Statement (Profit & Loss) report.
/// </summary>
[GenerateSerializer]
public record IncomeStatementReport(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] DateOnly FromDate,
    [property: Id(2)] DateOnly ToDate,
    [property: Id(3)] ReportSection Revenue,
    [property: Id(4)] ReportSection CostOfGoodsSold,
    [property: Id(5)] decimal GrossProfit,
    [property: Id(6)] decimal GrossProfitMargin,
    [property: Id(7)] ReportSection OperatingExpenses,
    [property: Id(8)] decimal OperatingIncome,
    [property: Id(9)] ReportSection OtherIncomeExpenses,
    [property: Id(10)] decimal NetIncome,
    [property: Id(11)] decimal NetProfitMargin,
    [property: Id(12)] IncomeStatementReport? ComparisonPeriod,
    [property: Id(13)] DateTime GeneratedAt);

/// <summary>
/// Balance Sheet report.
/// </summary>
[GenerateSerializer]
public record BalanceSheetReport(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] DateOnly AsOfDate,
    [property: Id(2)] ReportSection Assets,
    [property: Id(3)] ReportSection Liabilities,
    [property: Id(4)] ReportSection Equity,
    [property: Id(5)] decimal TotalAssets,
    [property: Id(6)] decimal TotalLiabilities,
    [property: Id(7)] decimal TotalEquity,
    [property: Id(8)] bool IsBalanced,
    [property: Id(9)] BalanceSheetReport? ComparisonPeriod,
    [property: Id(10)] DateTime GeneratedAt);

/// <summary>
/// Cash Flow Statement report.
/// </summary>
[GenerateSerializer]
public record CashFlowStatementReport(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] DateOnly FromDate,
    [property: Id(2)] DateOnly ToDate,
    [property: Id(3)] decimal BeginningCashBalance,
    [property: Id(4)] ReportSection OperatingActivities,
    [property: Id(5)] decimal NetCashFromOperating,
    [property: Id(6)] ReportSection InvestingActivities,
    [property: Id(7)] decimal NetCashFromInvesting,
    [property: Id(8)] ReportSection FinancingActivities,
    [property: Id(9)] decimal NetCashFromFinancing,
    [property: Id(10)] decimal NetChangeInCash,
    [property: Id(11)] decimal EndingCashBalance,
    [property: Id(12)] DateTime GeneratedAt);

// ============================================================================
// Commands
// ============================================================================

/// <summary>
/// Request for a trial balance report.
/// </summary>
[GenerateSerializer]
public record TrialBalanceRequest(
    [property: Id(0)] DateOnly AsOfDate,
    [property: Id(1)] bool IncludeInactiveAccounts = false);

/// <summary>
/// Request for an income statement.
/// </summary>
[GenerateSerializer]
public record IncomeStatementRequest(
    [property: Id(0)] DateOnly FromDate,
    [property: Id(1)] DateOnly ToDate,
    [property: Id(2)] bool IncludeComparison = false,
    [property: Id(3)] DateOnly? ComparisonFromDate = null,
    [property: Id(4)] DateOnly? ComparisonToDate = null);

/// <summary>
/// Request for a balance sheet.
/// </summary>
[GenerateSerializer]
public record BalanceSheetRequest(
    [property: Id(0)] DateOnly AsOfDate,
    [property: Id(1)] bool IncludeComparison = false,
    [property: Id(2)] DateOnly? ComparisonAsOfDate = null);

/// <summary>
/// Request for a cash flow statement.
/// </summary>
[GenerateSerializer]
public record CashFlowStatementRequest(
    [property: Id(0)] DateOnly FromDate,
    [property: Id(1)] DateOnly ToDate);

// ============================================================================
// Grain Interface
// ============================================================================

/// <summary>
/// Grain interface for generating financial reports.
/// Key format: {orgId}:financialreports
/// </summary>
public interface IFinancialReportsGrain : IGrainWithStringKey
{
    /// <summary>
    /// Generates a trial balance report.
    /// </summary>
    Task<TrialBalanceReport> GenerateTrialBalanceAsync(TrialBalanceRequest request);

    /// <summary>
    /// Generates an income statement (Profit & Loss).
    /// </summary>
    Task<IncomeStatementReport> GenerateIncomeStatementAsync(IncomeStatementRequest request);

    /// <summary>
    /// Generates a balance sheet.
    /// </summary>
    Task<BalanceSheetReport> GenerateBalanceSheetAsync(BalanceSheetRequest request);

    /// <summary>
    /// Generates a cash flow statement.
    /// </summary>
    Task<CashFlowStatementReport> GenerateCashFlowStatementAsync(CashFlowStatementRequest request);
}

// ============================================================================
// Grain Implementation
// ============================================================================

/// <summary>
/// Orleans grain for generating financial reports.
/// This grain is stateless and generates reports on-demand by querying other grains.
/// </summary>
[StatelessWorker]
public class FinancialReportsGrain : Grain, IFinancialReportsGrain
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<FinancialReportsGrain> _logger;
    private Guid _organizationId;

    public FinancialReportsGrain(
        IGrainFactory grainFactory,
        ILogger<FinancialReportsGrain> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        if (parts.Length >= 1)
        {
            _organizationId = Guid.Parse(parts[0]);
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<TrialBalanceReport> GenerateTrialBalanceAsync(TrialBalanceRequest request)
    {
        _logger.LogInformation(
            "Generating trial balance for org {OrgId} as of {Date}",
            _organizationId,
            request.AsOfDate);

        var chartGrain = _grainFactory.GetGrain<IChartOfAccountsGrain>(
            $"{_organizationId}:chartofaccounts");

        var accounts = await chartGrain.GetAllAccountsAsync(request.IncludeInactiveAccounts);

        var lines = new List<TrialBalanceLine>();
        decimal totalDebits = 0;
        decimal totalCredits = 0;

        foreach (var account in accounts.OrderBy(a => a.AccountNumber))
        {
            // In a real implementation, you would get the balance from each AccountGrain
            // For now, we'll create placeholder entries
            var balance = 0m; // Would be: await GetAccountBalanceAsync(account.AccountNumber, request.AsOfDate);

            decimal debitBalance = 0;
            decimal creditBalance = 0;

            // Accounts with debit normal balance show positive balances in debit column
            if (account.NormalBalance == NormalBalance.Debit)
            {
                if (balance >= 0)
                    debitBalance = balance;
                else
                    creditBalance = Math.Abs(balance);
            }
            else
            {
                if (balance >= 0)
                    creditBalance = balance;
                else
                    debitBalance = Math.Abs(balance);
            }

            if (debitBalance != 0 || creditBalance != 0 || request.IncludeInactiveAccounts)
            {
                lines.Add(new TrialBalanceLine(
                    account.AccountNumber,
                    account.Name,
                    account.AccountType,
                    debitBalance,
                    creditBalance));

                totalDebits += debitBalance;
                totalCredits += creditBalance;
            }
        }

        var isBalanced = Math.Abs(totalDebits - totalCredits) < 0.01m;

        return new TrialBalanceReport(
            _organizationId,
            request.AsOfDate,
            lines,
            totalDebits,
            totalCredits,
            isBalanced,
            DateTime.UtcNow);
    }

    public async Task<IncomeStatementReport> GenerateIncomeStatementAsync(IncomeStatementRequest request)
    {
        _logger.LogInformation(
            "Generating income statement for org {OrgId} from {FromDate} to {ToDate}",
            _organizationId,
            request.FromDate,
            request.ToDate);

        var chartGrain = _grainFactory.GetGrain<IChartOfAccountsGrain>(
            $"{_organizationId}:chartofaccounts");

        var accounts = await chartGrain.GetAllAccountsAsync();

        // Group accounts by type
        var revenueAccounts = accounts.Where(a => a.AccountType == AccountType.Revenue).ToList();
        var cogsAccounts = accounts.Where(a =>
            a.AccountType == AccountType.Expense &&
            a.AccountNumber.StartsWith("5")).ToList(); // COGS accounts typically start with 5
        var expenseAccounts = accounts.Where(a =>
            a.AccountType == AccountType.Expense &&
            !a.AccountNumber.StartsWith("5")).ToList();

        // Build sections (placeholder balances)
        var revenueSection = BuildReportSection("Revenue", revenueAccounts);
        var cogsSection = BuildReportSection("Cost of Goods Sold", cogsAccounts);
        var expenseSection = BuildReportSection("Operating Expenses", expenseAccounts);
        var otherSection = new ReportSection("Other Income/Expenses", [], 0, null);

        var grossProfit = revenueSection.SectionTotal - cogsSection.SectionTotal;
        var operatingIncome = grossProfit - expenseSection.SectionTotal;
        var netIncome = operatingIncome + otherSection.SectionTotal;

        var totalRevenue = revenueSection.SectionTotal;
        var grossProfitMargin = totalRevenue != 0 ? (grossProfit / totalRevenue) * 100 : 0;
        var netProfitMargin = totalRevenue != 0 ? (netIncome / totalRevenue) * 100 : 0;

        // Generate comparison if requested
        IncomeStatementReport? comparison = null;
        if (request.IncludeComparison && request.ComparisonFromDate.HasValue && request.ComparisonToDate.HasValue)
        {
            comparison = await GenerateIncomeStatementAsync(new IncomeStatementRequest(
                request.ComparisonFromDate.Value,
                request.ComparisonToDate.Value,
                false));
        }

        return new IncomeStatementReport(
            _organizationId,
            request.FromDate,
            request.ToDate,
            revenueSection,
            cogsSection,
            grossProfit,
            grossProfitMargin,
            expenseSection,
            operatingIncome,
            otherSection,
            netIncome,
            netProfitMargin,
            comparison,
            DateTime.UtcNow);
    }

    public async Task<BalanceSheetReport> GenerateBalanceSheetAsync(BalanceSheetRequest request)
    {
        _logger.LogInformation(
            "Generating balance sheet for org {OrgId} as of {Date}",
            _organizationId,
            request.AsOfDate);

        var chartGrain = _grainFactory.GetGrain<IChartOfAccountsGrain>(
            $"{_organizationId}:chartofaccounts");

        var accounts = await chartGrain.GetAllAccountsAsync();

        var assetAccounts = accounts.Where(a => a.AccountType == AccountType.Asset).ToList();
        var liabilityAccounts = accounts.Where(a => a.AccountType == AccountType.Liability).ToList();
        var equityAccounts = accounts.Where(a => a.AccountType == AccountType.Equity).ToList();

        var assetSection = BuildReportSection("Assets", assetAccounts);
        var liabilitySection = BuildReportSection("Liabilities", liabilityAccounts);
        var equitySection = BuildReportSection("Equity", equityAccounts);

        var totalAssets = assetSection.SectionTotal;
        var totalLiabilities = liabilitySection.SectionTotal;
        var totalEquity = equitySection.SectionTotal;

        var isBalanced = Math.Abs(totalAssets - (totalLiabilities + totalEquity)) < 0.01m;

        // Generate comparison if requested
        BalanceSheetReport? comparison = null;
        if (request.IncludeComparison && request.ComparisonAsOfDate.HasValue)
        {
            comparison = await GenerateBalanceSheetAsync(new BalanceSheetRequest(
                request.ComparisonAsOfDate.Value,
                false));
        }

        return new BalanceSheetReport(
            _organizationId,
            request.AsOfDate,
            assetSection,
            liabilitySection,
            equitySection,
            totalAssets,
            totalLiabilities,
            totalEquity,
            isBalanced,
            comparison,
            DateTime.UtcNow);
    }

    public Task<CashFlowStatementReport> GenerateCashFlowStatementAsync(CashFlowStatementRequest request)
    {
        _logger.LogInformation(
            "Generating cash flow statement for org {OrgId} from {FromDate} to {ToDate}",
            _organizationId,
            request.FromDate,
            request.ToDate);

        // Cash flow statement is more complex and requires analyzing changes in accounts
        // This is a simplified implementation

        var operatingSection = new ReportSection(
            "Cash Flows from Operating Activities",
            new List<ReportLineItem>
            {
                new("", "Net Income", 0, 0, null, null, null, false, false),
                new("", "Adjustments for non-cash items:", 0, 0, null, null, null, false, false),
                new("", "  Depreciation", 1, 0, null, null, null, false, false),
                new("", "Changes in working capital:", 0, 0, null, null, null, false, false),
                new("", "  Accounts Receivable", 1, 0, null, null, null, false, false),
                new("", "  Inventory", 1, 0, null, null, null, false, false),
                new("", "  Accounts Payable", 1, 0, null, null, null, false, false)
            },
            0,
            null);

        var investingSection = new ReportSection(
            "Cash Flows from Investing Activities",
            new List<ReportLineItem>
            {
                new("", "Purchase of Equipment", 0, 0, null, null, null, false, false),
                new("", "Sale of Assets", 0, 0, null, null, null, false, false)
            },
            0,
            null);

        var financingSection = new ReportSection(
            "Cash Flows from Financing Activities",
            new List<ReportLineItem>
            {
                new("", "Loan Proceeds", 0, 0, null, null, null, false, false),
                new("", "Loan Payments", 0, 0, null, null, null, false, false),
                new("", "Owner Distributions", 0, 0, null, null, null, false, false)
            },
            0,
            null);

        return Task.FromResult(new CashFlowStatementReport(
            _organizationId,
            request.FromDate,
            request.ToDate,
            0, // Beginning cash balance
            operatingSection,
            0, // Net cash from operating
            investingSection,
            0, // Net cash from investing
            financingSection,
            0, // Net cash from financing
            0, // Net change in cash
            0, // Ending cash balance
            DateTime.UtcNow));
    }

    private static ReportSection BuildReportSection(string title, List<ChartAccount> accounts)
    {
        var lines = new List<ReportLineItem>();
        decimal sectionTotal = 0;

        // Group accounts by parent for hierarchical display
        var rootAccounts = accounts.Where(a => a.ParentAccountNumber == null).ToList();

        foreach (var account in accounts.OrderBy(a => a.AccountNumber))
        {
            // Placeholder: In real implementation, get actual balances
            var amount = 0m;
            sectionTotal += amount;

            lines.Add(new ReportLineItem(
                account.AccountNumber,
                account.Name,
                account.Level,
                amount,
                null,
                null,
                null,
                false,
                false));
        }

        // Add total line
        lines.Add(new ReportLineItem(
            "",
            $"Total {title}",
            0,
            sectionTotal,
            null,
            null,
            null,
            false,
            true));

        return new ReportSection(title, lines, sectionTotal, null);
    }
}
