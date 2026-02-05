using System.Globalization;
using System.Text;
using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.Services;

/// <summary>
/// Report types supported for export.
/// </summary>
public enum ReportType
{
    DailySales,
    ProductMix,
    LaborReport,
    PaymentReconciliation,
    DaypartAnalysis,
    Inventory,
    Consumption,
    Waste
}

/// <summary>
/// Export format options.
/// </summary>
public enum ExportFormat
{
    Csv,
    Excel
}

/// <summary>
/// Export request parameters.
/// </summary>
public record ExportRequest(
    Guid OrgId,
    Guid SiteId,
    ReportType ReportType,
    ExportFormat Format,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IncludeHeaders = true);

/// <summary>
/// Export result with file content and metadata.
/// </summary>
public record ExportResult(
    byte[] Content,
    string ContentType,
    string FileName);

/// <summary>
/// Scheduled export configuration.
/// </summary>
public record ScheduledExport(
    Guid ExportId,
    Guid OrgId,
    Guid SiteId,
    ReportType ReportType,
    ExportFormat Format,
    string CronExpression,
    string RecipientEmail,
    bool IsActive);

/// <summary>
/// Service for exporting reports to CSV and Excel formats.
/// </summary>
public class ReportExportService : IReportExportService
{
    private readonly IGrainFactory _grainFactory;

    public ReportExportService(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    /// <summary>
    /// Exports a report based on the request parameters.
    /// </summary>
    public async Task<ExportResult> ExportAsync(ExportRequest request)
    {
        var data = await GetReportDataAsync(request);

        return request.Format switch
        {
            ExportFormat.Csv => ExportToCsv(data, request),
            ExportFormat.Excel => ExportToExcel(data, request),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Format))
        };
    }

    /// <summary>
    /// Exports a daily sales report.
    /// </summary>
    public async Task<ExportResult> ExportDailySalesAsync(
        Guid orgId, Guid siteId, DateOnly startDate, DateOnly endDate, ExportFormat format)
    {
        var request = new ExportRequest(orgId, siteId, ReportType.DailySales, format, startDate, endDate);
        return await ExportAsync(request);
    }

    /// <summary>
    /// Exports a product mix report.
    /// </summary>
    public async Task<ExportResult> ExportProductMixAsync(
        Guid orgId, Guid siteId, DateOnly date, ExportFormat format)
    {
        var request = new ExportRequest(orgId, siteId, ReportType.ProductMix, format, date, date);
        return await ExportAsync(request);
    }

    /// <summary>
    /// Exports a labor report.
    /// </summary>
    public async Task<ExportResult> ExportLaborReportAsync(
        Guid orgId, Guid siteId, DateOnly startDate, DateOnly endDate, ExportFormat format)
    {
        var request = new ExportRequest(orgId, siteId, ReportType.LaborReport, format, startDate, endDate);
        return await ExportAsync(request);
    }

    /// <summary>
    /// Exports a payment reconciliation report.
    /// </summary>
    public async Task<ExportResult> ExportPaymentReconciliationAsync(
        Guid orgId, Guid siteId, DateOnly date, ExportFormat format)
    {
        var request = new ExportRequest(orgId, siteId, ReportType.PaymentReconciliation, format, date, date);
        return await ExportAsync(request);
    }

    private async Task<ReportData> GetReportDataAsync(ExportRequest request)
    {
        return request.ReportType switch
        {
            ReportType.DailySales => await GetDailySalesDataAsync(request),
            ReportType.ProductMix => await GetProductMixDataAsync(request),
            ReportType.LaborReport => await GetLaborReportDataAsync(request),
            ReportType.PaymentReconciliation => await GetPaymentReconciliationDataAsync(request),
            ReportType.DaypartAnalysis => await GetDaypartAnalysisDataAsync(request),
            ReportType.Inventory => await GetInventoryDataAsync(request),
            ReportType.Consumption => await GetConsumptionDataAsync(request),
            ReportType.Waste => await GetWasteDataAsync(request),
            _ => throw new ArgumentOutOfRangeException(nameof(request.ReportType))
        };
    }

    private async Task<ReportData> GetDailySalesDataAsync(ExportRequest request)
    {
        var headers = new[] { "Date", "Gross Sales", "Discounts", "Net Sales", "COGS", "Gross Profit", "GP%", "Transactions", "Guests", "Avg Ticket" };
        var rows = new List<string[]>();

        for (var date = request.StartDate; date <= request.EndDate; date = date.AddDays(1))
        {
            var key = GrainKeys.DailySales(request.OrgId, request.SiteId, date);
            var grain = _grainFactory.GetGrain<IDailySalesGrain>(key);

            try
            {
                var snapshot = await grain.GetSnapshotAsync();
                rows.Add(new[]
                {
                    date.ToString("yyyy-MM-dd"),
                    snapshot.GrossSales.ToString("F2", CultureInfo.InvariantCulture),
                    (snapshot.GrossSales - snapshot.NetSales).ToString("F2", CultureInfo.InvariantCulture),
                    snapshot.NetSales.ToString("F2", CultureInfo.InvariantCulture),
                    snapshot.ActualCOGS.ToString("F2", CultureInfo.InvariantCulture),
                    snapshot.GrossProfit.ToString("F2", CultureInfo.InvariantCulture),
                    snapshot.GrossProfitPercent.ToString("F1", CultureInfo.InvariantCulture),
                    snapshot.TransactionCount.ToString(),
                    snapshot.GuestCount.ToString(),
                    snapshot.AverageTicket.ToString("F2", CultureInfo.InvariantCulture)
                });
            }
            catch
            {
                // Skip dates with no data
            }
        }

        return new ReportData("Daily Sales Report", headers, rows);
    }

    private async Task<ReportData> GetProductMixDataAsync(ExportRequest request)
    {
        var headers = new[] { "Product", "Category", "Qty Sold", "Net Sales", "% of Sales", "Gross Profit", "GP%", "Velocity/Hr" };
        var rows = new List<string[]>();

        var key = $"{request.OrgId}:{request.SiteId}:productmix:{request.StartDate:yyyy-MM-dd}";
        var grain = _grainFactory.GetGrain<IProductMixGrain>(key);

        try
        {
            var snapshot = await grain.GetSnapshotAsync();
            foreach (var product in snapshot.Products.OrderByDescending(p => p.NetSales))
            {
                rows.Add(new[]
                {
                    product.ProductName,
                    product.Category,
                    product.QuantitySold.ToString(),
                    product.NetSales.ToString("F2", CultureInfo.InvariantCulture),
                    product.PercentOfSales.ToString("F1", CultureInfo.InvariantCulture),
                    product.GrossProfit.ToString("F2", CultureInfo.InvariantCulture),
                    product.GrossProfitPercent.ToString("F1", CultureInfo.InvariantCulture),
                    product.SalesVelocity.ToString("F2", CultureInfo.InvariantCulture)
                });
            }
        }
        catch
        {
            // Return empty if no data
        }

        return new ReportData($"Product Mix Report - {request.StartDate:yyyy-MM-dd}", headers, rows);
    }

    private async Task<ReportData> GetLaborReportDataAsync(ExportRequest request)
    {
        var headers = new[] { "Metric", "Value" };
        var rows = new List<string[]>();

        var key = $"{request.OrgId}:{request.SiteId}:labor:{request.StartDate:yyyy-MM-dd}";
        var grain = _grainFactory.GetGrain<ILaborReportGrain>(key);

        try
        {
            var snapshot = await grain.GetSnapshotAsync();

            rows.Add(new[] { "Period Start", snapshot.PeriodStart.ToString("yyyy-MM-dd") });
            rows.Add(new[] { "Period End", snapshot.PeriodEnd.ToString("yyyy-MM-dd") });
            rows.Add(new[] { "Total Sales", snapshot.TotalSales.ToString("F2", CultureInfo.InvariantCulture) });
            rows.Add(new[] { "Total Labor Cost", snapshot.TotalLaborCost.ToString("F2", CultureInfo.InvariantCulture) });
            rows.Add(new[] { "Labor Cost %", snapshot.LaborCostPercent.ToString("F1", CultureInfo.InvariantCulture) + "%" });
            rows.Add(new[] { "Total Labor Hours", snapshot.TotalLaborHours.ToString("F1", CultureInfo.InvariantCulture) });
            rows.Add(new[] { "Sales Per Labor Hour", snapshot.SalesPerLaborHour.ToString("F2", CultureInfo.InvariantCulture) });
            rows.Add(new[] { "Scheduled Hours", snapshot.ScheduledHours.ToString("F1", CultureInfo.InvariantCulture) });
            rows.Add(new[] { "Actual Hours", snapshot.ActualHours.ToString("F1", CultureInfo.InvariantCulture) });
            rows.Add(new[] { "Schedule Variance", snapshot.ScheduleVarianceHours.ToString("F1", CultureInfo.InvariantCulture) });
            rows.Add(new[] { "Overtime Hours", snapshot.OvertimeHours.ToString("F1", CultureInfo.InvariantCulture) });
            rows.Add(new[] { "Overtime Cost", snapshot.OvertimeCost.ToString("F2", CultureInfo.InvariantCulture) });
            rows.Add(new[] { "Overtime %", snapshot.OvertimePercent.ToString("F1", CultureInfo.InvariantCulture) + "%" });
        }
        catch
        {
            // Return empty if no data
        }

        return new ReportData($"Labor Report - {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}", headers, rows);
    }

    private async Task<ReportData> GetPaymentReconciliationDataAsync(ExportRequest request)
    {
        var headers = new[] { "Payment Method", "POS Total", "Processor Total", "Variance" };
        var rows = new List<string[]>();

        var key = $"{request.OrgId}:{request.SiteId}:reconciliation:{request.StartDate:yyyy-MM-dd}";
        var grain = _grainFactory.GetGrain<IPaymentReconciliationGrain>(key);

        try
        {
            var snapshot = await grain.GetSnapshotAsync();

            rows.Add(new[] { "Cash", snapshot.PosTotalCash.ToString("F2", CultureInfo.InvariantCulture), snapshot.CashActual.ToString("F2", CultureInfo.InvariantCulture), snapshot.CashVariance.ToString("F2", CultureInfo.InvariantCulture) });
            rows.Add(new[] { "Card", snapshot.PosTotalCard.ToString("F2", CultureInfo.InvariantCulture), snapshot.ProcessorTotalSettled.ToString("F2", CultureInfo.InvariantCulture), snapshot.CardVariance.ToString("F2", CultureInfo.InvariantCulture) });
            rows.Add(new[] { "Other", snapshot.PosTotalOther.ToString("F2", CultureInfo.InvariantCulture), "-", "-" });
            rows.Add(new[] { "TOTAL", snapshot.PosGrandTotal.ToString("F2", CultureInfo.InvariantCulture), "-", (snapshot.CashVariance + snapshot.CardVariance).ToString("F2", CultureInfo.InvariantCulture) });

            if (snapshot.Exceptions.Any())
            {
                rows.Add(new[] { "", "", "", "" });
                rows.Add(new[] { "EXCEPTIONS", "", "", "" });
                foreach (var ex in snapshot.Exceptions)
                {
                    rows.Add(new[] { ex.ExceptionType, ex.Description, ex.Amount.ToString("F2", CultureInfo.InvariantCulture), ex.Status.ToString() });
                }
            }
        }
        catch
        {
            // Return empty if no data
        }

        return new ReportData($"Payment Reconciliation - {request.StartDate:yyyy-MM-dd}", headers, rows);
    }

    private async Task<ReportData> GetDaypartAnalysisDataAsync(ExportRequest request)
    {
        var headers = new[] { "Daypart", "Net Sales", "% of Total", "Transactions", "Guests", "Avg Ticket", "Labor Cost", "SPLH" };
        var rows = new List<string[]>();

        var key = $"{request.OrgId}:{request.SiteId}:daypart:{request.StartDate:yyyy-MM-dd}";
        var grain = _grainFactory.GetGrain<IDaypartAnalysisGrain>(key);

        try
        {
            var snapshot = await grain.GetSnapshotAsync();
            foreach (var dp in snapshot.DaypartPerformances)
            {
                rows.Add(new[]
                {
                    dp.Daypart.ToString(),
                    dp.NetSales.ToString("F2", CultureInfo.InvariantCulture),
                    dp.PercentOfDailySales.ToString("F1", CultureInfo.InvariantCulture),
                    dp.TransactionCount.ToString(),
                    dp.GuestCount.ToString(),
                    dp.AverageTicket.ToString("F2", CultureInfo.InvariantCulture),
                    dp.LaborCost.ToString("F2", CultureInfo.InvariantCulture),
                    dp.SalesPerLaborHour.ToString("F2", CultureInfo.InvariantCulture)
                });
            }
        }
        catch
        {
            // Return empty if no data
        }

        return new ReportData($"Daypart Analysis - {request.StartDate:yyyy-MM-dd}", headers, rows);
    }

    private async Task<ReportData> GetInventoryDataAsync(ExportRequest request)
    {
        var headers = new[] { "SKU", "Ingredient", "Category", "On Hand", "Unit", "Value", "Low Stock", "Expiring Soon" };
        var rows = new List<string[]>();

        var key = GrainKeys.DailyInventorySnapshot(request.OrgId, request.SiteId, request.StartDate);
        var grain = _grainFactory.GetGrain<IDailyInventorySnapshotGrain>(key);

        try
        {
            var snapshot = await grain.GetSnapshotAsync();
            foreach (var item in snapshot.Ingredients.OrderBy(i => i.Category).ThenBy(i => i.IngredientName))
            {
                rows.Add(new[]
                {
                    item.Sku,
                    item.IngredientName,
                    item.Category,
                    item.OnHandQuantity.ToString("F2", CultureInfo.InvariantCulture),
                    item.Unit,
                    item.TotalValue.ToString("F2", CultureInfo.InvariantCulture),
                    item.IsLowStock ? "Yes" : "No",
                    item.IsExpiringSoon ? "Yes" : "No"
                });
            }
        }
        catch
        {
            // Return empty if no data
        }

        return new ReportData($"Inventory Snapshot - {request.StartDate:yyyy-MM-dd}", headers, rows);
    }

    private async Task<ReportData> GetConsumptionDataAsync(ExportRequest request)
    {
        var headers = new[] { "Ingredient", "Category", "Theoretical Qty", "Actual Qty", "Variance Qty", "Theoretical Cost", "Actual Cost", "Variance Cost", "Variance %" };
        var rows = new List<string[]>();

        var key = GrainKeys.DailyConsumption(request.OrgId, request.SiteId, request.StartDate);
        var grain = _grainFactory.GetGrain<IDailyConsumptionGrain>(key);

        try
        {
            var variances = await grain.GetVarianceBreakdownAsync();
            foreach (var v in variances.OrderByDescending(v => Math.Abs(v.CostVariance)))
            {
                var variancePercent = v.TheoreticalCost > 0 ? v.CostVariance / v.TheoreticalCost * 100 : 0;
                rows.Add(new[]
                {
                    v.IngredientName,
                    v.Category,
                    v.TheoreticalUsage.ToString("F2", CultureInfo.InvariantCulture),
                    v.ActualUsage.ToString("F2", CultureInfo.InvariantCulture),
                    v.UsageVariance.ToString("F2", CultureInfo.InvariantCulture),
                    v.TheoreticalCost.ToString("F2", CultureInfo.InvariantCulture),
                    v.ActualCost.ToString("F2", CultureInfo.InvariantCulture),
                    v.CostVariance.ToString("F2", CultureInfo.InvariantCulture),
                    variancePercent.ToString("F1", CultureInfo.InvariantCulture)
                });
            }
        }
        catch
        {
            // Return empty if no data
        }

        return new ReportData($"Consumption Report - {request.StartDate:yyyy-MM-dd}", headers, rows);
    }

    private async Task<ReportData> GetWasteDataAsync(ExportRequest request)
    {
        var headers = new[] { "Ingredient", "Category", "Quantity", "Unit", "Reason", "Cost" };
        var rows = new List<string[]>();

        var key = GrainKeys.DailyWaste(request.OrgId, request.SiteId, request.StartDate);
        var grain = _grainFactory.GetGrain<IDailyWasteGrain>(key);

        try
        {
            var facts = await grain.GetFactsAsync();
            foreach (var f in facts.OrderByDescending(f => f.CostBasis))
            {
                rows.Add(new[]
                {
                    f.IngredientName,
                    f.Category,
                    f.Quantity.ToString("F2", CultureInfo.InvariantCulture),
                    f.Unit,
                    f.Reason.ToString(),
                    f.CostBasis.ToString("F2", CultureInfo.InvariantCulture)
                });
            }
        }
        catch
        {
            // Return empty if no data
        }

        return new ReportData($"Waste Report - {request.StartDate:yyyy-MM-dd}", headers, rows);
    }

    private ExportResult ExportToCsv(ReportData data, ExportRequest request)
    {
        var sb = new StringBuilder();

        // Add title
        sb.AppendLine(data.Title);
        sb.AppendLine();

        // Add headers
        if (request.IncludeHeaders)
        {
            sb.AppendLine(string.Join(",", data.Headers.Select(EscapeCsvField)));
        }

        // Add rows
        foreach (var row in data.Rows)
        {
            sb.AppendLine(string.Join(",", row.Select(EscapeCsvField)));
        }

        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"{request.ReportType}_{request.SiteId}_{request.StartDate:yyyyMMdd}";
        if (request.StartDate != request.EndDate)
        {
            fileName += $"_to_{request.EndDate:yyyyMMdd}";
        }
        fileName += ".csv";

        return new ExportResult(content, "text/csv", fileName);
    }

    private ExportResult ExportToExcel(ReportData data, ExportRequest request)
    {
        // Create a simple Excel XML format (SpreadsheetML)
        // This is a lightweight approach that doesn't require external dependencies
        var sb = new StringBuilder();

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
        sb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
        sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");

        sb.AppendLine("<Styles>");
        sb.AppendLine("<Style ss:ID=\"Header\">");
        sb.AppendLine("<Font ss:Bold=\"1\"/>");
        sb.AppendLine("<Interior ss:Color=\"#CCCCCC\" ss:Pattern=\"Solid\"/>");
        sb.AppendLine("</Style>");
        sb.AppendLine("<Style ss:ID=\"Title\">");
        sb.AppendLine("<Font ss:Bold=\"1\" ss:Size=\"14\"/>");
        sb.AppendLine("</Style>");
        sb.AppendLine("<Style ss:ID=\"Number\">");
        sb.AppendLine("<NumberFormat ss:Format=\"#,##0.00\"/>");
        sb.AppendLine("</Style>");
        sb.AppendLine("</Styles>");

        sb.AppendLine($"<Worksheet ss:Name=\"{EscapeXml(request.ReportType.ToString())}\">");
        sb.AppendLine("<Table>");

        // Title row
        sb.AppendLine("<Row>");
        sb.AppendLine($"<Cell ss:StyleID=\"Title\"><Data ss:Type=\"String\">{EscapeXml(data.Title)}</Data></Cell>");
        sb.AppendLine("</Row>");
        sb.AppendLine("<Row></Row>"); // Empty row

        // Header row
        if (request.IncludeHeaders)
        {
            sb.AppendLine("<Row>");
            foreach (var header in data.Headers)
            {
                sb.AppendLine($"<Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{EscapeXml(header)}</Data></Cell>");
            }
            sb.AppendLine("</Row>");
        }

        // Data rows
        foreach (var row in data.Rows)
        {
            sb.AppendLine("<Row>");
            foreach (var cell in row)
            {
                var dataType = decimal.TryParse(cell, NumberStyles.Any, CultureInfo.InvariantCulture, out _) ? "Number" : "String";
                sb.AppendLine($"<Cell><Data ss:Type=\"{dataType}\">{EscapeXml(cell)}</Data></Cell>");
            }
            sb.AppendLine("</Row>");
        }

        sb.AppendLine("</Table>");
        sb.AppendLine("</Worksheet>");
        sb.AppendLine("</Workbook>");

        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"{request.ReportType}_{request.SiteId}_{request.StartDate:yyyyMMdd}";
        if (request.StartDate != request.EndDate)
        {
            fileName += $"_to_{request.EndDate:yyyyMMdd}";
        }
        fileName += ".xlsx";

        return new ExportResult(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return "";

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    private static string EscapeXml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private record ReportData(string Title, string[] Headers, List<string[]> Rows);
}

/// <summary>
/// Interface for report export service.
/// </summary>
public interface IReportExportService
{
    Task<ExportResult> ExportAsync(ExportRequest request);
    Task<ExportResult> ExportDailySalesAsync(Guid orgId, Guid siteId, DateOnly startDate, DateOnly endDate, ExportFormat format);
    Task<ExportResult> ExportProductMixAsync(Guid orgId, Guid siteId, DateOnly date, ExportFormat format);
    Task<ExportResult> ExportLaborReportAsync(Guid orgId, Guid siteId, DateOnly startDate, DateOnly endDate, ExportFormat format);
    Task<ExportResult> ExportPaymentReconciliationAsync(Guid orgId, Guid siteId, DateOnly date, ExportFormat format);
}
