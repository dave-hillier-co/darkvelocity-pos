using System.Globalization;
using System.Text;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Services;

// ============================================================================
// Tax Calculation Types
// ============================================================================

[GenerateSerializer]
public record TaxConfiguration(
    [property: Id(0)] string JurisdictionCode,
    [property: Id(1)] decimal FederalTaxRate,
    [property: Id(2)] decimal StateTaxRate,
    [property: Id(3)] decimal LocalTaxRate,
    [property: Id(4)] decimal SocialSecurityRate,
    [property: Id(5)] decimal MedicareRate,
    [property: Id(6)] decimal SocialSecurityWageLimit,
    [property: Id(7)] decimal AdditionalMedicareThreshold,
    [property: Id(8)] decimal AdditionalMedicareRate);

[GenerateSerializer]
public record TaxWithholding(
    [property: Id(0)] decimal FederalWithholding,
    [property: Id(1)] decimal StateWithholding,
    [property: Id(2)] decimal LocalWithholding,
    [property: Id(3)] decimal SocialSecurityWithholding,
    [property: Id(4)] decimal MedicareWithholding,
    [property: Id(5)] decimal TotalWithholding);

[GenerateSerializer]
public record EmployeeTaxSummary(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] string EmployeeName,
    [property: Id(2)] decimal GrossWages,
    [property: Id(3)] TaxWithholding CurrentPeriod,
    [property: Id(4)] TaxWithholding YearToDate);

// ============================================================================
// Payroll Export Types
// ============================================================================

public enum PayrollExportFormat
{
    Csv,
    Adp,
    Gusto,
    Paychex,
    QuickBooks
}

[GenerateSerializer]
public record PayrollExportRequest(
    [property: Id(0)] Guid OrgId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] DateOnly PeriodStart,
    [property: Id(3)] DateOnly PeriodEnd,
    [property: Id(4)] PayrollExportFormat Format,
    [property: Id(5)] bool IncludeTaxWithholdings = true,
    [property: Id(6)] bool IncludeTips = true,
    [property: Id(7)] bool IncludeDeductions = true);

[GenerateSerializer]
public record PayrollExportEntry(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] string EmployeeNumber,
    [property: Id(2)] string FirstName,
    [property: Id(3)] string LastName,
    [property: Id(4)] string Email,
    [property: Id(5)] decimal RegularHours,
    [property: Id(6)] decimal OvertimeHours,
    [property: Id(7)] decimal DoubleOvertimeHours,
    [property: Id(8)] decimal HourlyRate,
    [property: Id(9)] decimal OvertimeRate,
    [property: Id(10)] decimal DoubleOvertimeRate,
    [property: Id(11)] decimal RegularPay,
    [property: Id(12)] decimal OvertimePay,
    [property: Id(13)] decimal DoubleOvertimePay,
    [property: Id(14)] decimal TipsReceived,
    [property: Id(15)] decimal GrossPay,
    [property: Id(16)] TaxWithholding? TaxWithholding,
    [property: Id(17)] decimal Deductions,
    [property: Id(18)] decimal NetPay,
    [property: Id(19)] string Department,
    [property: Id(20)] string JobTitle);

[GenerateSerializer]
public record PayrollExportResult(
    [property: Id(0)] Guid ExportId,
    [property: Id(1)] DateOnly PeriodStart,
    [property: Id(2)] DateOnly PeriodEnd,
    [property: Id(3)] PayrollExportFormat Format,
    [property: Id(4)] int EmployeeCount,
    [property: Id(5)] decimal TotalRegularHours,
    [property: Id(6)] decimal TotalOvertimeHours,
    [property: Id(7)] decimal TotalGrossPay,
    [property: Id(8)] decimal TotalNetPay,
    [property: Id(9)] decimal TotalTaxWithholdings,
    [property: Id(10)] string FileContent,
    [property: Id(11)] string FileName,
    [property: Id(12)] string ContentType);

[GenerateSerializer]
public record PayrollPreview(
    [property: Id(0)] DateOnly PeriodStart,
    [property: Id(1)] DateOnly PeriodEnd,
    [property: Id(2)] int EmployeeCount,
    [property: Id(3)] decimal TotalRegularHours,
    [property: Id(4)] decimal TotalOvertimeHours,
    [property: Id(5)] decimal TotalGrossPay,
    [property: Id(6)] decimal TotalTaxWithholdings,
    [property: Id(7)] decimal TotalDeductions,
    [property: Id(8)] decimal TotalNetPay,
    [property: Id(9)] IReadOnlyList<PayrollExportEntry> Entries);

// ============================================================================
// Tax Calculation Service
// ============================================================================

public interface ITaxCalculationService
{
    TaxWithholding CalculateWithholding(decimal grossPay, TaxConfiguration config, decimal ytdGrossPay = 0);
    TaxConfiguration GetTaxConfiguration(string jurisdictionCode);
    EmployeeTaxSummary CalculateEmployeeTaxSummary(
        Guid employeeId,
        string employeeName,
        decimal grossPay,
        decimal ytdGrossPay,
        string jurisdictionCode);
}

public class TaxCalculationService : ITaxCalculationService
{
    private static readonly Dictionary<string, TaxConfiguration> TaxConfigurations = new()
    {
        ["US-FEDERAL"] = new TaxConfiguration(
            JurisdictionCode: "US-FEDERAL",
            FederalTaxRate: 0.22m, // 22% supplemental rate
            StateTaxRate: 0m,
            LocalTaxRate: 0m,
            SocialSecurityRate: 0.062m, // 6.2%
            MedicareRate: 0.0145m, // 1.45%
            SocialSecurityWageLimit: 168600m, // 2024 limit
            AdditionalMedicareThreshold: 200000m,
            AdditionalMedicareRate: 0.009m), // 0.9% additional
        ["US-CA"] = new TaxConfiguration(
            JurisdictionCode: "US-CA",
            FederalTaxRate: 0.22m,
            StateTaxRate: 0.0725m, // ~7.25% average
            LocalTaxRate: 0m,
            SocialSecurityRate: 0.062m,
            MedicareRate: 0.0145m,
            SocialSecurityWageLimit: 168600m,
            AdditionalMedicareThreshold: 200000m,
            AdditionalMedicareRate: 0.009m),
        ["US-NY"] = new TaxConfiguration(
            JurisdictionCode: "US-NY",
            FederalTaxRate: 0.22m,
            StateTaxRate: 0.0685m, // ~6.85%
            LocalTaxRate: 0.03876m, // NYC
            SocialSecurityRate: 0.062m,
            MedicareRate: 0.0145m,
            SocialSecurityWageLimit: 168600m,
            AdditionalMedicareThreshold: 200000m,
            AdditionalMedicareRate: 0.009m),
        ["US-TX"] = new TaxConfiguration(
            JurisdictionCode: "US-TX",
            FederalTaxRate: 0.22m,
            StateTaxRate: 0m, // No state income tax
            LocalTaxRate: 0m,
            SocialSecurityRate: 0.062m,
            MedicareRate: 0.0145m,
            SocialSecurityWageLimit: 168600m,
            AdditionalMedicareThreshold: 200000m,
            AdditionalMedicareRate: 0.009m),
        ["US-FL"] = new TaxConfiguration(
            JurisdictionCode: "US-FL",
            FederalTaxRate: 0.22m,
            StateTaxRate: 0m, // No state income tax
            LocalTaxRate: 0m,
            SocialSecurityRate: 0.062m,
            MedicareRate: 0.0145m,
            SocialSecurityWageLimit: 168600m,
            AdditionalMedicareThreshold: 200000m,
            AdditionalMedicareRate: 0.009m),
        ["UK"] = new TaxConfiguration(
            JurisdictionCode: "UK",
            FederalTaxRate: 0.20m, // Basic rate
            StateTaxRate: 0m,
            LocalTaxRate: 0m,
            SocialSecurityRate: 0.12m, // Employee NI
            MedicareRate: 0m,
            SocialSecurityWageLimit: 50270m, // Upper earnings limit
            AdditionalMedicareThreshold: 0m,
            AdditionalMedicareRate: 0.02m) // NI above UEL
    };

    public TaxConfiguration GetTaxConfiguration(string jurisdictionCode)
    {
        if (TaxConfigurations.TryGetValue(jurisdictionCode, out var config))
            return config;

        // Default to US Federal
        return TaxConfigurations["US-FEDERAL"];
    }

    public TaxWithholding CalculateWithholding(decimal grossPay, TaxConfiguration config, decimal ytdGrossPay = 0)
    {
        var federalWithholding = grossPay * config.FederalTaxRate;
        var stateWithholding = grossPay * config.StateTaxRate;
        var localWithholding = grossPay * config.LocalTaxRate;

        // Social Security - capped at wage limit
        decimal ssWithholding = 0;
        var ytdAfterPayment = ytdGrossPay + grossPay;
        if (ytdGrossPay < config.SocialSecurityWageLimit)
        {
            var taxableForSS = Math.Min(grossPay, config.SocialSecurityWageLimit - ytdGrossPay);
            ssWithholding = taxableForSS * config.SocialSecurityRate;
        }

        // Medicare - no wage limit, but additional rate above threshold
        var medicareWithholding = grossPay * config.MedicareRate;
        if (config.AdditionalMedicareThreshold > 0 && ytdAfterPayment > config.AdditionalMedicareThreshold)
        {
            var amountOverThreshold = ytdAfterPayment - Math.Max(ytdGrossPay, config.AdditionalMedicareThreshold);
            medicareWithholding += amountOverThreshold * config.AdditionalMedicareRate;
        }

        var totalWithholding = federalWithholding + stateWithholding + localWithholding +
                               ssWithholding + medicareWithholding;

        return new TaxWithholding(
            Math.Round(federalWithholding, 2),
            Math.Round(stateWithholding, 2),
            Math.Round(localWithholding, 2),
            Math.Round(ssWithholding, 2),
            Math.Round(medicareWithholding, 2),
            Math.Round(totalWithholding, 2));
    }

    public EmployeeTaxSummary CalculateEmployeeTaxSummary(
        Guid employeeId,
        string employeeName,
        decimal grossPay,
        decimal ytdGrossPay,
        string jurisdictionCode)
    {
        var config = GetTaxConfiguration(jurisdictionCode);
        var currentWithholding = CalculateWithholding(grossPay, config, ytdGrossPay);

        // Calculate YTD (simplified - would need actual YTD tracking)
        var ytdWithholding = CalculateWithholding(ytdGrossPay + grossPay, config);

        return new EmployeeTaxSummary(
            employeeId,
            employeeName,
            grossPay,
            currentWithholding,
            ytdWithholding);
    }
}

// ============================================================================
// Payroll Export Service
// ============================================================================

public interface IPayrollExportService
{
    Task<PayrollPreview> GeneratePreviewAsync(
        IReadOnlyList<PayrollExportEntry> entries,
        DateOnly periodStart,
        DateOnly periodEnd);

    Task<PayrollExportResult> ExportAsync(
        IReadOnlyList<PayrollExportEntry> entries,
        PayrollExportRequest request);

    string GenerateCsv(IReadOnlyList<PayrollExportEntry> entries, bool includeTaxes);
    string GenerateAdpFormat(IReadOnlyList<PayrollExportEntry> entries);
    string GenerateGustoFormat(IReadOnlyList<PayrollExportEntry> entries);
}

public class PayrollExportService : IPayrollExportService
{
    private readonly ITaxCalculationService _taxService;

    public PayrollExportService(ITaxCalculationService taxService)
    {
        _taxService = taxService;
    }

    public Task<PayrollPreview> GeneratePreviewAsync(
        IReadOnlyList<PayrollExportEntry> entries,
        DateOnly periodStart,
        DateOnly periodEnd)
    {
        var preview = new PayrollPreview(
            periodStart,
            periodEnd,
            entries.Count,
            entries.Sum(e => e.RegularHours),
            entries.Sum(e => e.OvertimeHours + e.DoubleOvertimeHours),
            entries.Sum(e => e.GrossPay),
            entries.Sum(e => e.TaxWithholding?.TotalWithholding ?? 0),
            entries.Sum(e => e.Deductions),
            entries.Sum(e => e.NetPay),
            entries);

        return Task.FromResult(preview);
    }

    public Task<PayrollExportResult> ExportAsync(
        IReadOnlyList<PayrollExportEntry> entries,
        PayrollExportRequest request)
    {
        string content;
        string fileName;
        string contentType;

        switch (request.Format)
        {
            case PayrollExportFormat.Csv:
                content = GenerateCsv(entries, request.IncludeTaxWithholdings);
                fileName = $"payroll_{request.PeriodStart:yyyy-MM-dd}_{request.PeriodEnd:yyyy-MM-dd}.csv";
                contentType = "text/csv";
                break;

            case PayrollExportFormat.Adp:
                content = GenerateAdpFormat(entries);
                fileName = $"payroll_adp_{request.PeriodStart:yyyy-MM-dd}_{request.PeriodEnd:yyyy-MM-dd}.csv";
                contentType = "text/csv";
                break;

            case PayrollExportFormat.Gusto:
                content = GenerateGustoFormat(entries);
                fileName = $"payroll_gusto_{request.PeriodStart:yyyy-MM-dd}_{request.PeriodEnd:yyyy-MM-dd}.csv";
                contentType = "text/csv";
                break;

            case PayrollExportFormat.Paychex:
                content = GeneratePaychexFormat(entries);
                fileName = $"payroll_paychex_{request.PeriodStart:yyyy-MM-dd}_{request.PeriodEnd:yyyy-MM-dd}.csv";
                contentType = "text/csv";
                break;

            case PayrollExportFormat.QuickBooks:
                content = GenerateQuickBooksFormat(entries);
                fileName = $"payroll_quickbooks_{request.PeriodStart:yyyy-MM-dd}_{request.PeriodEnd:yyyy-MM-dd}.iif";
                contentType = "text/plain";
                break;

            default:
                throw new ArgumentException($"Unsupported export format: {request.Format}");
        }

        var result = new PayrollExportResult(
            Guid.NewGuid(),
            request.PeriodStart,
            request.PeriodEnd,
            request.Format,
            entries.Count,
            entries.Sum(e => e.RegularHours),
            entries.Sum(e => e.OvertimeHours + e.DoubleOvertimeHours),
            entries.Sum(e => e.GrossPay),
            entries.Sum(e => e.NetPay),
            entries.Sum(e => e.TaxWithholding?.TotalWithholding ?? 0),
            content,
            fileName,
            contentType);

        return Task.FromResult(result);
    }

    public string GenerateCsv(IReadOnlyList<PayrollExportEntry> entries, bool includeTaxes)
    {
        var sb = new StringBuilder();

        // Header
        var headers = new List<string>
        {
            "Employee ID", "Employee Number", "First Name", "Last Name", "Email",
            "Department", "Job Title",
            "Regular Hours", "Overtime Hours", "Double OT Hours",
            "Hourly Rate", "OT Rate", "Double OT Rate",
            "Regular Pay", "Overtime Pay", "Double OT Pay",
            "Tips", "Gross Pay"
        };

        if (includeTaxes)
        {
            headers.AddRange(new[]
            {
                "Federal Tax", "State Tax", "Local Tax",
                "Social Security", "Medicare", "Total Tax"
            });
        }

        headers.AddRange(new[] { "Deductions", "Net Pay" });

        sb.AppendLine(string.Join(",", headers));

        // Data rows
        foreach (var entry in entries)
        {
            var values = new List<string>
            {
                entry.EmployeeId.ToString(),
                CsvEscape(entry.EmployeeNumber),
                CsvEscape(entry.FirstName),
                CsvEscape(entry.LastName),
                CsvEscape(entry.Email),
                CsvEscape(entry.Department),
                CsvEscape(entry.JobTitle),
                entry.RegularHours.ToString("F2", CultureInfo.InvariantCulture),
                entry.OvertimeHours.ToString("F2", CultureInfo.InvariantCulture),
                entry.DoubleOvertimeHours.ToString("F2", CultureInfo.InvariantCulture),
                entry.HourlyRate.ToString("F2", CultureInfo.InvariantCulture),
                entry.OvertimeRate.ToString("F2", CultureInfo.InvariantCulture),
                entry.DoubleOvertimeRate.ToString("F2", CultureInfo.InvariantCulture),
                entry.RegularPay.ToString("F2", CultureInfo.InvariantCulture),
                entry.OvertimePay.ToString("F2", CultureInfo.InvariantCulture),
                entry.DoubleOvertimePay.ToString("F2", CultureInfo.InvariantCulture),
                entry.TipsReceived.ToString("F2", CultureInfo.InvariantCulture),
                entry.GrossPay.ToString("F2", CultureInfo.InvariantCulture)
            };

            if (includeTaxes && entry.TaxWithholding != null)
            {
                values.AddRange(new[]
                {
                    entry.TaxWithholding.FederalWithholding.ToString("F2", CultureInfo.InvariantCulture),
                    entry.TaxWithholding.StateWithholding.ToString("F2", CultureInfo.InvariantCulture),
                    entry.TaxWithholding.LocalWithholding.ToString("F2", CultureInfo.InvariantCulture),
                    entry.TaxWithholding.SocialSecurityWithholding.ToString("F2", CultureInfo.InvariantCulture),
                    entry.TaxWithholding.MedicareWithholding.ToString("F2", CultureInfo.InvariantCulture),
                    entry.TaxWithholding.TotalWithholding.ToString("F2", CultureInfo.InvariantCulture)
                });
            }
            else if (includeTaxes)
            {
                values.AddRange(new[] { "0.00", "0.00", "0.00", "0.00", "0.00", "0.00" });
            }

            values.AddRange(new[]
            {
                entry.Deductions.ToString("F2", CultureInfo.InvariantCulture),
                entry.NetPay.ToString("F2", CultureInfo.InvariantCulture)
            });

            sb.AppendLine(string.Join(",", values));
        }

        return sb.ToString();
    }

    public string GenerateAdpFormat(IReadOnlyList<PayrollExportEntry> entries)
    {
        // ADP format: Fixed-width or specific CSV format
        var sb = new StringBuilder();

        // ADP header
        sb.AppendLine("H,ADP_PAYROLL_IMPORT,1.0");
        sb.AppendLine("C,Employee ID,Hours Code,Hours,Rate,Earnings Code,Amount");

        foreach (var entry in entries)
        {
            // Regular hours
            if (entry.RegularHours > 0)
            {
                sb.AppendLine($"D,{entry.EmployeeNumber},REG,{entry.RegularHours:F2},{entry.HourlyRate:F2},,");
            }

            // Overtime hours
            if (entry.OvertimeHours > 0)
            {
                sb.AppendLine($"D,{entry.EmployeeNumber},OT,{entry.OvertimeHours:F2},{entry.OvertimeRate:F2},,");
            }

            // Double overtime hours
            if (entry.DoubleOvertimeHours > 0)
            {
                sb.AppendLine($"D,{entry.EmployeeNumber},DOT,{entry.DoubleOvertimeHours:F2},{entry.DoubleOvertimeRate:F2},,");
            }

            // Tips
            if (entry.TipsReceived > 0)
            {
                sb.AppendLine($"D,{entry.EmployeeNumber},,,TIPS,{entry.TipsReceived:F2}");
            }
        }

        sb.AppendLine($"T,{entries.Count}");

        return sb.ToString();
    }

    public string GenerateGustoFormat(IReadOnlyList<PayrollExportEntry> entries)
    {
        // Gusto CSV import format
        var sb = new StringBuilder();

        sb.AppendLine("employee_id,employee_email,regular_hours,overtime_hours,double_overtime_hours,cash_tips,credit_card_tips,bonus,commission,reimbursement");

        foreach (var entry in entries)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                entry.EmployeeNumber,
                CsvEscape(entry.Email),
                entry.RegularHours.ToString("F2", CultureInfo.InvariantCulture),
                entry.OvertimeHours.ToString("F2", CultureInfo.InvariantCulture),
                entry.DoubleOvertimeHours.ToString("F2", CultureInfo.InvariantCulture),
                "0.00", // cash tips - would need to split
                entry.TipsReceived.ToString("F2", CultureInfo.InvariantCulture), // credit card tips
                "0.00", // bonus
                "0.00", // commission
                "0.00"  // reimbursement
            }));
        }

        return sb.ToString();
    }

    private string GeneratePaychexFormat(IReadOnlyList<PayrollExportEntry> entries)
    {
        // Paychex CSV import format
        var sb = new StringBuilder();

        sb.AppendLine("Employee Number,Last Name,First Name,Pay Type,Hours,Rate,Amount");

        foreach (var entry in entries)
        {
            if (entry.RegularHours > 0)
            {
                sb.AppendLine($"{entry.EmployeeNumber},{CsvEscape(entry.LastName)},{CsvEscape(entry.FirstName)},R,{entry.RegularHours:F2},{entry.HourlyRate:F2},{entry.RegularPay:F2}");
            }

            if (entry.OvertimeHours > 0)
            {
                sb.AppendLine($"{entry.EmployeeNumber},{CsvEscape(entry.LastName)},{CsvEscape(entry.FirstName)},O,{entry.OvertimeHours:F2},{entry.OvertimeRate:F2},{entry.OvertimePay:F2}");
            }

            if (entry.TipsReceived > 0)
            {
                sb.AppendLine($"{entry.EmployeeNumber},{CsvEscape(entry.LastName)},{CsvEscape(entry.FirstName)},T,,,{entry.TipsReceived:F2}");
            }
        }

        return sb.ToString();
    }

    private string GenerateQuickBooksFormat(IReadOnlyList<PayrollExportEntry> entries)
    {
        // QuickBooks IIF format
        var sb = new StringBuilder();

        sb.AppendLine("!TIMEACT\tDATE\tJOB\tEMP\tITEM\tDURATION\tNOTE");

        var today = DateTime.Now.ToString("M/d/yyyy");

        foreach (var entry in entries)
        {
            var name = $"{entry.LastName}, {entry.FirstName}";

            if (entry.RegularHours > 0)
            {
                sb.AppendLine($"TIMEACT\t{today}\t\t{name}\tRegular Pay\t{entry.RegularHours:F2}\t");
            }

            if (entry.OvertimeHours > 0)
            {
                sb.AppendLine($"TIMEACT\t{today}\t\t{name}\tOvertime Pay\t{entry.OvertimeHours:F2}\t");
            }
        }

        return sb.ToString();
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

// ============================================================================
// Payroll Export Grain (for grain-based access)
// ============================================================================

[GenerateSerializer]
public record GeneratePayrollExportCommand(
    [property: Id(0)] Guid SiteId,
    [property: Id(1)] DateOnly PeriodStart,
    [property: Id(2)] DateOnly PeriodEnd,
    [property: Id(3)] PayrollExportFormat Format,
    [property: Id(4)] string JurisdictionCode = "US-FEDERAL");

/// <summary>
/// Grain for payroll export management.
/// Key: "org:{orgId}:payrollexport"
/// </summary>
public interface IPayrollExportGrain : IGrainWithStringKey
{
    /// <summary>
    /// Generates a payroll export preview.
    /// </summary>
    Task<PayrollPreview> GeneratePreviewAsync(GeneratePayrollExportCommand command);

    /// <summary>
    /// Exports payroll data to the specified format.
    /// </summary>
    Task<PayrollExportResult> ExportAsync(GeneratePayrollExportCommand command);

    /// <summary>
    /// Gets the tax configuration for a jurisdiction.
    /// </summary>
    Task<TaxConfiguration> GetTaxConfigurationAsync(string jurisdictionCode);

    /// <summary>
    /// Calculates tax withholding for an employee.
    /// </summary>
    Task<EmployeeTaxSummary> CalculateEmployeeTaxesAsync(
        Guid employeeId,
        string employeeName,
        decimal grossPay,
        decimal ytdGrossPay,
        string jurisdictionCode);
}
