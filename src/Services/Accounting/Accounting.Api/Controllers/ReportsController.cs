using DarkVelocity.Accounting.Api.Data;
using DarkVelocity.Accounting.Api.Dtos;
using DarkVelocity.Accounting.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Accounting.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly AccountingDbContext _context;

    // TODO: In multi-tenant implementation, inject ITenantContext to get TenantId
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public ReportsController(AccountingDbContext context)
    {
        _context = context;
    }

    [HttpGet("trial-balance")]
    public async Task<ActionResult<TrialBalanceReportDto>> GetTrialBalance(
        [FromQuery] DateOnly? asOfDate = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] string currency = "EUR")
    {
        var date = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _context.JournalEntries
            .Include(e => e.Lines)
            .Where(e =>
                e.TenantId == DefaultTenantId &&
                e.Status == JournalEntryStatus.Posted &&
                e.EntryDate <= date);

        if (locationId.HasValue)
        {
            query = query.Where(e => e.LocationId == locationId.Value);
        }

        var entries = await query.ToListAsync();
        var allLines = entries.SelectMany(e => e.Lines).ToList();

        var accounts = await _context.Accounts
            .Where(a => a.TenantId == DefaultTenantId && a.IsActive)
            .ToDictionaryAsync(a => a.AccountCode, a => a);

        var balancesByAccount = allLines
            .GroupBy(l => l.AccountCode)
            .Select(g => new
            {
                AccountCode = g.Key,
                TotalDebit = g.Sum(l => l.DebitAmount),
                TotalCredit = g.Sum(l => l.CreditAmount)
            })
            .ToList();

        var lines = balancesByAccount
            .Where(b => accounts.ContainsKey(b.AccountCode))
            .Select(b =>
            {
                var account = accounts[b.AccountCode];
                var netBalance = b.TotalDebit - b.TotalCredit;

                // For Asset and Expense accounts, debit balance is positive
                // For Liability, Equity, and Revenue accounts, credit balance is positive
                var isDebitNormal = account.AccountType == AccountType.Asset ||
                                    account.AccountType == AccountType.Expense;

                return new TrialBalanceLineDto
                {
                    AccountCode = b.AccountCode,
                    AccountName = account.Name,
                    AccountType = account.AccountType,
                    DebitBalance = netBalance > 0 ? netBalance : 0,
                    CreditBalance = netBalance < 0 ? Math.Abs(netBalance) : 0
                };
            })
            .OrderBy(l => l.AccountCode)
            .ToList();

        var report = new TrialBalanceReportDto
        {
            AsOfDate = date,
            LocationId = locationId,
            Currency = currency,
            TotalDebits = lines.Sum(l => l.DebitBalance),
            TotalCredits = lines.Sum(l => l.CreditBalance),
            Lines = lines,
            GeneratedAt = DateTime.UtcNow
        };

        report.AddSelfLink("/api/reports/trial-balance");

        return Ok(report);
    }

    [HttpGet("profit-loss")]
    public async Task<ActionResult<ProfitLossReportDto>> GetProfitLoss(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        [FromQuery] Guid? locationId = null,
        [FromQuery] string currency = "EUR")
    {
        var query = _context.JournalEntries
            .Include(e => e.Lines)
            .Where(e =>
                e.TenantId == DefaultTenantId &&
                e.Status == JournalEntryStatus.Posted &&
                e.EntryDate >= startDate &&
                e.EntryDate <= endDate);

        if (locationId.HasValue)
        {
            query = query.Where(e => e.LocationId == locationId.Value);
        }

        var entries = await query.ToListAsync();
        var allLines = entries.SelectMany(e => e.Lines).ToList();

        var accounts = await _context.Accounts
            .Where(a => a.TenantId == DefaultTenantId &&
                        (a.AccountType == AccountType.Revenue || a.AccountType == AccountType.Expense))
            .ToDictionaryAsync(a => a.AccountCode, a => a);

        var balancesByAccount = allLines
            .Where(l => accounts.ContainsKey(l.AccountCode))
            .GroupBy(l => l.AccountCode)
            .Select(g => new
            {
                AccountCode = g.Key,
                NetAmount = g.Sum(l => l.CreditAmount - l.DebitAmount)
            })
            .ToList();

        var revenueLines = balancesByAccount
            .Where(b => accounts[b.AccountCode].AccountType == AccountType.Revenue)
            .Select(b => new ProfitLossLineDto
            {
                AccountCode = b.AccountCode,
                AccountName = accounts[b.AccountCode].Name,
                Amount = b.NetAmount
            })
            .OrderBy(l => l.AccountCode)
            .ToList();

        var expenseLines = balancesByAccount
            .Where(b => accounts[b.AccountCode].AccountType == AccountType.Expense)
            .Select(b => new ProfitLossLineDto
            {
                AccountCode = b.AccountCode,
                AccountName = accounts[b.AccountCode].Name,
                Amount = Math.Abs(b.NetAmount)
            })
            .OrderBy(l => l.AccountCode)
            .ToList();

        var totalRevenue = revenueLines.Sum(l => l.Amount);
        var totalExpenses = expenseLines.Sum(l => l.Amount);

        var report = new ProfitLossReportDto
        {
            StartDate = startDate,
            EndDate = endDate,
            LocationId = locationId,
            Currency = currency,
            TotalRevenue = totalRevenue,
            TotalExpenses = totalExpenses,
            NetIncome = totalRevenue - totalExpenses,
            RevenueSections = new List<ProfitLossSectionDto>
            {
                new()
                {
                    SectionName = "Revenue",
                    Total = totalRevenue,
                    Lines = revenueLines
                }
            },
            ExpenseSections = new List<ProfitLossSectionDto>
            {
                new()
                {
                    SectionName = "Operating Expenses",
                    Total = totalExpenses,
                    Lines = expenseLines
                }
            },
            GeneratedAt = DateTime.UtcNow
        };

        report.AddSelfLink("/api/reports/profit-loss");

        return Ok(report);
    }

    [HttpGet("balance-sheet")]
    public async Task<ActionResult<BalanceSheetReportDto>> GetBalanceSheet(
        [FromQuery] DateOnly? asOfDate = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] string currency = "EUR")
    {
        var date = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _context.JournalEntries
            .Include(e => e.Lines)
            .Where(e =>
                e.TenantId == DefaultTenantId &&
                e.Status == JournalEntryStatus.Posted &&
                e.EntryDate <= date);

        if (locationId.HasValue)
        {
            query = query.Where(e => e.LocationId == locationId.Value);
        }

        var entries = await query.ToListAsync();
        var allLines = entries.SelectMany(e => e.Lines).ToList();

        var accounts = await _context.Accounts
            .Where(a => a.TenantId == DefaultTenantId)
            .ToDictionaryAsync(a => a.AccountCode, a => a);

        var balancesByAccount = allLines
            .Where(l => accounts.ContainsKey(l.AccountCode))
            .GroupBy(l => l.AccountCode)
            .Select(g => new
            {
                AccountCode = g.Key,
                NetBalance = g.Sum(l => l.DebitAmount - l.CreditAmount)
            })
            .ToList();

        var assetLines = balancesByAccount
            .Where(b => accounts[b.AccountCode].AccountType == AccountType.Asset)
            .Select(b => new BalanceSheetLineDto
            {
                AccountCode = b.AccountCode,
                AccountName = accounts[b.AccountCode].Name,
                Balance = b.NetBalance
            })
            .OrderBy(l => l.AccountCode)
            .ToList();

        var liabilityLines = balancesByAccount
            .Where(b => accounts[b.AccountCode].AccountType == AccountType.Liability)
            .Select(b => new BalanceSheetLineDto
            {
                AccountCode = b.AccountCode,
                AccountName = accounts[b.AccountCode].Name,
                Balance = Math.Abs(b.NetBalance)
            })
            .OrderBy(l => l.AccountCode)
            .ToList();

        var equityLines = balancesByAccount
            .Where(b => accounts[b.AccountCode].AccountType == AccountType.Equity)
            .Select(b => new BalanceSheetLineDto
            {
                AccountCode = b.AccountCode,
                AccountName = accounts[b.AccountCode].Name,
                Balance = Math.Abs(b.NetBalance)
            })
            .OrderBy(l => l.AccountCode)
            .ToList();

        var totalAssets = assetLines.Sum(l => l.Balance);
        var totalLiabilities = liabilityLines.Sum(l => l.Balance);
        var totalEquity = equityLines.Sum(l => l.Balance);

        var report = new BalanceSheetReportDto
        {
            AsOfDate = date,
            LocationId = locationId,
            Currency = currency,
            TotalAssets = totalAssets,
            TotalLiabilities = totalLiabilities,
            TotalEquity = totalEquity,
            AssetSections = new List<BalanceSheetSectionDto>
            {
                new()
                {
                    SectionName = "Assets",
                    Total = totalAssets,
                    Lines = assetLines
                }
            },
            LiabilitySections = new List<BalanceSheetSectionDto>
            {
                new()
                {
                    SectionName = "Liabilities",
                    Total = totalLiabilities,
                    Lines = liabilityLines
                }
            },
            EquitySections = new List<BalanceSheetSectionDto>
            {
                new()
                {
                    SectionName = "Equity",
                    Total = totalEquity,
                    Lines = equityLines
                }
            },
            GeneratedAt = DateTime.UtcNow
        };

        report.AddSelfLink("/api/reports/balance-sheet");

        return Ok(report);
    }

    [HttpGet("vat-summary")]
    public async Task<ActionResult<VatSummaryReportDto>> GetVatSummary(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        [FromQuery] Guid? locationId = null,
        [FromQuery] string currency = "EUR")
    {
        var query = _context.JournalEntries
            .Include(e => e.Lines)
            .Where(e =>
                e.TenantId == DefaultTenantId &&
                e.Status == JournalEntryStatus.Posted &&
                e.EntryDate >= startDate &&
                e.EntryDate <= endDate);

        if (locationId.HasValue)
        {
            query = query.Where(e => e.LocationId == locationId.Value);
        }

        var entries = await query.ToListAsync();
        var allLines = entries
            .SelectMany(e => e.Lines)
            .Where(l => l.TaxCode != null && l.TaxAmount.HasValue)
            .ToList();

        var vatByCode = allLines
            .GroupBy(l => l.TaxCode!)
            .Select(g => new VatSummaryLineDto
            {
                TaxCode = g.Key,
                TaxRate = GetTaxRateFromCode(g.Key),
                TaxableAmount = g.Sum(l => l.CreditAmount > 0 ? l.CreditAmount : l.DebitAmount),
                VatAmount = g.Sum(l => l.TaxAmount ?? 0)
            })
            .OrderBy(l => l.TaxCode)
            .ToList();

        var report = new VatSummaryReportDto
        {
            StartDate = startDate,
            EndDate = endDate,
            LocationId = locationId,
            Currency = currency,
            TotalTaxableAmount = vatByCode.Sum(l => l.TaxableAmount),
            TotalVatAmount = vatByCode.Sum(l => l.VatAmount),
            Lines = vatByCode,
            GeneratedAt = DateTime.UtcNow
        };

        report.AddSelfLink("/api/reports/vat-summary");

        return Ok(report);
    }

    [HttpGet("tax-liability")]
    public async Task<ActionResult<TaxLiabilityReportDto>> GetTaxLiability(
        [FromQuery] string period,
        [FromQuery] Guid? locationId = null,
        [FromQuery] string currency = "EUR")
    {
        var taxLiabilities = await _context.TaxLiabilities
            .Where(t =>
                t.TenantId == DefaultTenantId &&
                t.Period == period &&
                (locationId == null || t.LocationId == locationId))
            .ToListAsync();

        var lines = taxLiabilities.Select(t => new TaxLiabilityLineDto
        {
            TaxCode = t.TaxCode,
            TaxRate = t.TaxRate,
            TaxableAmount = t.TaxableAmount,
            TaxAmount = t.TaxAmount,
            Status = t.Status
        }).ToList();

        var report = new TaxLiabilityReportDto
        {
            Period = period,
            LocationId = locationId,
            Currency = currency,
            TotalLiability = lines.Sum(l => l.TaxAmount),
            Lines = lines,
            GeneratedAt = DateTime.UtcNow
        };

        report.AddSelfLink("/api/reports/tax-liability");

        return Ok(report);
    }

    [HttpGet("gift-card-liability")]
    public async Task<ActionResult<GiftCardLiabilityReportDto>> GetGiftCardLiability(
        [FromQuery] DateOnly? asOfDate = null,
        [FromQuery] string currency = "EUR")
    {
        var date = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var liability = await _context.GiftCardLiabilities
            .Where(g => g.TenantId == DefaultTenantId && g.AsOfDate == date)
            .FirstOrDefaultAsync();

        if (liability == null)
        {
            // Return empty report
            var emptyReport = new GiftCardLiabilityReportDto
            {
                AsOfDate = date,
                Currency = currency,
                TotalOutstandingCards = 0,
                TotalLiability = 0,
                ByProgram = new List<GiftCardLiabilityByProgramDto>(),
                ByAge = new List<GiftCardLiabilityByAgeDto>(),
                GeneratedAt = DateTime.UtcNow
            };
            emptyReport.AddSelfLink("/api/reports/gift-card-liability");
            return Ok(emptyReport);
        }

        var report = new GiftCardLiabilityReportDto
        {
            AsOfDate = date,
            Currency = liability.Currency,
            TotalOutstandingCards = liability.TotalOutstandingCards,
            TotalLiability = liability.TotalLiability,
            ByProgram = new List<GiftCardLiabilityByProgramDto>(), // Parse from JSON if needed
            ByAge = new List<GiftCardLiabilityByAgeDto>(), // Parse from JSON if needed
            GeneratedAt = DateTime.UtcNow
        };

        report.AddSelfLink("/api/reports/gift-card-liability");

        return Ok(report);
    }

    [HttpGet("daily-summary")]
    public async Task<ActionResult<DailySummaryReportDto>> GetDailySummary(
        [FromQuery] Guid locationId,
        [FromQuery] DateOnly? date = null,
        [FromQuery] string currency = "EUR")
    {
        var reportDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var entries = await _context.JournalEntries
            .Include(e => e.Lines)
            .Where(e =>
                e.TenantId == DefaultTenantId &&
                e.LocationId == locationId &&
                e.Status == JournalEntryStatus.Posted &&
                e.EntryDate == reportDate)
            .ToListAsync();

        var salesEntries = entries.Where(e => e.SourceType == JournalEntrySourceType.Order).ToList();
        var paymentEntries = entries.Where(e => e.SourceType == JournalEntrySourceType.Payment).ToList();

        var accounts = await _context.Accounts
            .Where(a => a.TenantId == DefaultTenantId && a.AccountType == AccountType.Revenue)
            .Select(a => a.AccountCode)
            .ToListAsync();

        var totalSales = salesEntries
            .SelectMany(e => e.Lines)
            .Where(l => accounts.Contains(l.AccountCode))
            .Sum(l => l.CreditAmount);

        var totalTax = salesEntries
            .SelectMany(e => e.Lines)
            .Where(l => l.TaxAmount.HasValue)
            .Sum(l => l.TaxAmount ?? 0);

        var report = new DailySummaryReportDto
        {
            Date = reportDate,
            LocationId = locationId,
            Currency = currency,
            TotalSales = totalSales,
            TotalPayments = paymentEntries.Sum(e => e.TotalDebit),
            TotalRefunds = 0, // Calculate from refund entries if tracked
            TotalTax = totalTax,
            NetRevenue = totalSales - totalTax,
            OrderCount = salesEntries.Count,
            AverageOrderValue = salesEntries.Count > 0 ? totalSales / salesEntries.Count : 0,
            PaymentMethods = new List<DailySummaryPaymentMethodDto>(), // Aggregate from payment data
            GeneratedAt = DateTime.UtcNow
        };

        report.AddSelfLink($"/api/reports/daily-summary?locationId={locationId}&date={reportDate}");

        return Ok(report);
    }

    private static decimal GetTaxRateFromCode(string taxCode)
    {
        // Common German tax rates
        return taxCode.ToUpperInvariant() switch
        {
            "A" => 0.19m, // 19% standard rate
            "B" => 0.07m, // 7% reduced rate
            "C" => 0.00m, // 0% exempt
            _ => 0.00m
        };
    }
}
