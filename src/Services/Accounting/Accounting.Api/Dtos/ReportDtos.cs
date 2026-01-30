using DarkVelocity.Accounting.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Accounting.Api.Dtos;

// Trial Balance

public class TrialBalanceReportDto : HalResource
{
    public DateOnly AsOfDate { get; set; }
    public Guid? LocationId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public bool IsBalanced => TotalDebits == TotalCredits;
    public List<TrialBalanceLineDto> Lines { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class TrialBalanceLineDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public decimal DebitBalance { get; set; }
    public decimal CreditBalance { get; set; }
}

// Profit & Loss (Income Statement)

public class ProfitLossReportDto : HalResource
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Guid? LocationId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetIncome { get; set; }
    public List<ProfitLossSectionDto> RevenueSections { get; set; } = new();
    public List<ProfitLossSectionDto> ExpenseSections { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class ProfitLossSectionDto
{
    public string SectionName { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public List<ProfitLossLineDto> Lines { get; set; } = new();
}

public class ProfitLossLineDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

// Balance Sheet

public class BalanceSheetReportDto : HalResource
{
    public DateOnly AsOfDate { get; set; }
    public Guid? LocationId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal TotalEquity { get; set; }
    public bool IsBalanced => TotalAssets == (TotalLiabilities + TotalEquity);
    public List<BalanceSheetSectionDto> AssetSections { get; set; } = new();
    public List<BalanceSheetSectionDto> LiabilitySections { get; set; } = new();
    public List<BalanceSheetSectionDto> EquitySections { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class BalanceSheetSectionDto
{
    public string SectionName { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public List<BalanceSheetLineDto> Lines { get; set; } = new();
}

public class BalanceSheetLineDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}

// VAT Summary

public class VatSummaryReportDto : HalResource
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Guid? LocationId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal TotalTaxableAmount { get; set; }
    public decimal TotalVatAmount { get; set; }
    public List<VatSummaryLineDto> Lines { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class VatSummaryLineDto
{
    public string TaxCode { get; set; } = string.Empty;
    public decimal TaxRate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal VatAmount { get; set; }
}

// Tax Liability

public class TaxLiabilityReportDto : HalResource
{
    public string Period { get; set; } = string.Empty;
    public Guid? LocationId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal TotalLiability { get; set; }
    public List<TaxLiabilityLineDto> Lines { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class TaxLiabilityLineDto
{
    public string TaxCode { get; set; } = string.Empty;
    public decimal TaxRate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public TaxLiabilityStatus Status { get; set; }
}

// Gift Card Liability

public class GiftCardLiabilityReportDto : HalResource
{
    public DateOnly AsOfDate { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int TotalOutstandingCards { get; set; }
    public decimal TotalLiability { get; set; }
    public List<GiftCardLiabilityByProgramDto> ByProgram { get; set; } = new();
    public List<GiftCardLiabilityByAgeDto> ByAge { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class GiftCardLiabilityByProgramDto
{
    public string ProgramName { get; set; } = string.Empty;
    public int CardCount { get; set; }
    public decimal Liability { get; set; }
}

public class GiftCardLiabilityByAgeDto
{
    public string AgeBucket { get; set; } = string.Empty;
    public int CardCount { get; set; }
    public decimal Liability { get; set; }
}

// Daily Summary

public class DailySummaryReportDto : HalResource
{
    public DateOnly Date { get; set; }
    public Guid LocationId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public decimal TotalPayments { get; set; }
    public decimal TotalRefunds { get; set; }
    public decimal TotalTax { get; set; }
    public decimal NetRevenue { get; set; }
    public int OrderCount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public List<DailySummaryPaymentMethodDto> PaymentMethods { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class DailySummaryPaymentMethodDto
{
    public string PaymentMethod { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal Amount { get; set; }
}
