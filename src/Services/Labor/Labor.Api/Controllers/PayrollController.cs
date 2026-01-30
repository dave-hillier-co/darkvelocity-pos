using DarkVelocity.Labor.Api.Data;
using DarkVelocity.Labor.Api.Dtos;
using DarkVelocity.Labor.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Labor.Api.Controllers;

[ApiController]
[Route("api/payroll-periods")]
public class PayrollController : ControllerBase
{
    private readonly LaborDbContext _context;

    public PayrollController(LaborDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// List payroll periods.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HalCollection<PayrollPeriodSummaryDto>>> GetAll(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0)
    {
        var query = _context.PayrollPeriods
            .Include(p => p.Entries)
            .AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(p => p.TenantId == tenantId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.Status == status);

        var total = await query.CountAsync();

        var periods = await query
            .OrderByDescending(p => p.PeriodStart)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = periods.Select(p => new PayrollPeriodSummaryDto
        {
            Id = p.Id,
            PeriodStart = p.PeriodStart,
            PeriodEnd = p.PeriodEnd,
            Status = p.Status,
            TotalRegularHours = p.TotalRegularHours,
            TotalOvertimeHours = p.TotalOvertimeHours,
            TotalGrossPay = p.TotalGrossPay,
            EntryCount = p.Entries.Count
        }).ToList();

        foreach (var dto in dtos)
            dto.AddSelfLink($"/api/payroll-periods/{dto.Id}");

        return Ok(HalCollection<PayrollPeriodSummaryDto>.Create(dtos, "/api/payroll-periods", total));
    }

    /// <summary>
    /// Get payroll period by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PayrollPeriodDto>> GetById(Guid id)
    {
        var period = await _context.PayrollPeriods
            .Include(p => p.Entries)
                .ThenInclude(e => e.Employee)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (period == null)
            return NotFound();

        var dto = MapToDto(period);
        AddLinks(dto);
        AddActionLinks(dto, period);
        return Ok(dto);
    }

    /// <summary>
    /// Create a payroll period.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PayrollPeriodDto>> Create(
        [FromQuery] Guid tenantId,
        [FromBody] CreatePayrollPeriodRequest request)
    {
        // Check for overlapping periods
        var overlapping = await _context.PayrollPeriods
            .AnyAsync(p =>
                p.TenantId == tenantId &&
                p.PeriodStart <= request.PeriodEnd &&
                p.PeriodEnd >= request.PeriodStart);

        if (overlapping)
            return BadRequest(new { message = "Overlapping payroll period exists" });

        var period = new PayrollPeriod
        {
            TenantId = tenantId,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            Status = "open"
        };

        _context.PayrollPeriods.Add(period);
        await _context.SaveChangesAsync();

        var dto = MapToDto(period);
        AddLinks(dto);
        AddActionLinks(dto, period);
        return CreatedAtAction(nameof(GetById), new { id = period.Id }, dto);
    }

    /// <summary>
    /// Process payroll for a period.
    /// </summary>
    [HttpPost("{id:guid}/process")]
    public async Task<ActionResult<PayrollPeriodDto>> Process(Guid id)
    {
        var period = await _context.PayrollPeriods
            .Include(p => p.Entries)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (period == null)
            return NotFound();

        if (period.Status != "open")
            return BadRequest(new { message = "Period must be open to process" });

        // Get all completed time entries for this period
        var timeEntries = await _context.TimeEntries
            .Include(t => t.Employee)
            .Where(t =>
                t.TenantId == period.TenantId &&
                DateOnly.FromDateTime(t.ClockInAt) >= period.PeriodStart &&
                DateOnly.FromDateTime(t.ClockInAt) <= period.PeriodEnd &&
                t.ClockOutAt != null &&
                t.Status != "disputed")
            .ToListAsync();

        // Get tip distributions for this period
        var tipDistributions = await _context.TipDistributions
            .Include(d => d.TipPool)
            .Where(d =>
                d.TipPool!.TenantId == period.TenantId &&
                d.TipPool.Date >= period.PeriodStart &&
                d.TipPool.Date <= period.PeriodEnd &&
                d.Status == "approved")
            .ToListAsync();

        // Clear existing entries
        _context.PayrollEntries.RemoveRange(period.Entries);

        // Group time entries by employee
        var entriesByEmployee = timeEntries
            .GroupBy(t => t.EmployeeId)
            .ToList();

        foreach (var group in entriesByEmployee)
        {
            var employeeId = group.Key;
            var employee = group.First().Employee!;

            var regularHours = group.Sum(t => t.RegularHours);
            var overtimeHours = group.Sum(t => t.OvertimeHours);

            // Use actual hourly rate from time entries
            var avgHourlyRate = group.Average(t => t.HourlyRate);
            var avgOvertimeRate = group.Average(t => t.OvertimeRate);

            var regularPay = group.Sum(t => t.RegularHours * t.HourlyRate);
            var overtimePay = group.Sum(t => t.OvertimeHours * t.HourlyRate * t.OvertimeRate);

            var tips = tipDistributions
                .Where(d => d.EmployeeId == employeeId)
                .Sum(d => d.TipShare);

            var entry = new PayrollEntry
            {
                PayrollPeriodId = id,
                EmployeeId = employeeId,
                RegularHours = regularHours,
                OvertimeHours = overtimeHours,
                RegularPay = regularPay,
                OvertimePay = overtimePay,
                TipIncome = tips,
                GrossPay = regularPay + overtimePay + tips,
                Status = "pending"
            };

            _context.PayrollEntries.Add(entry);
        }

        // Update period totals
        period.TotalRegularHours = entriesByEmployee.Sum(g => g.Sum(t => t.RegularHours));
        period.TotalOvertimeHours = entriesByEmployee.Sum(g => g.Sum(t => t.OvertimeHours));
        period.TotalTips = tipDistributions.Sum(d => d.TipShare);
        period.TotalGrossPay = period.Entries.Sum(e => e.GrossPay);
        period.Status = "processing";
        period.ProcessedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Reload
        period = await _context.PayrollPeriods
            .Include(p => p.Entries)
                .ThenInclude(e => e.Employee)
            .FirstAsync(p => p.Id == id);

        // Recalculate totals from entries
        period.TotalGrossPay = period.Entries.Sum(e => e.GrossPay);
        await _context.SaveChangesAsync();

        var dto = MapToDto(period);
        AddLinks(dto);
        AddActionLinks(dto, period);
        return Ok(dto);
    }

    /// <summary>
    /// Approve payroll for a period.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<PayrollPeriodDto>> Approve(
        Guid id,
        [FromQuery] Guid userId)
    {
        var period = await _context.PayrollPeriods
            .Include(p => p.Entries)
                .ThenInclude(e => e.Employee)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (period == null)
            return NotFound();

        if (period.Status != "processing")
            return BadRequest(new { message = "Period must be processed before approval" });

        period.Status = "approved";
        period.ApprovedByUserId = userId;

        foreach (var entry in period.Entries)
        {
            entry.Status = "approved";
        }

        await _context.SaveChangesAsync();

        var dto = MapToDto(period);
        AddLinks(dto);
        AddActionLinks(dto, period);
        return Ok(dto);
    }

    /// <summary>
    /// Export payroll to external provider format.
    /// </summary>
    [HttpPost("{id:guid}/export")]
    public async Task<ActionResult<object>> Export(
        Guid id,
        [FromBody] ExportPayrollRequest request)
    {
        var period = await _context.PayrollPeriods
            .Include(p => p.Entries)
                .ThenInclude(e => e.Employee)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (period == null)
            return NotFound();

        if (period.Status != "approved")
            return BadRequest(new { message = "Period must be approved before export" });

        // Generate export data based on format
        var exportData = request.Format.ToLower() switch
        {
            "adp" => GenerateAdpExport(period),
            "gusto" => GenerateGustoExport(period),
            "datev" => GenerateDatevExport(period),
            _ => GenerateGenericExport(period)
        };

        period.Status = "exported";
        period.ExportedAt = DateTime.UtcNow;
        period.ExportFormat = request.Format;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            periodId = id,
            format = request.Format,
            exportedAt = period.ExportedAt,
            data = exportData
        });
    }

    /// <summary>
    /// Get entries for a payroll period.
    /// </summary>
    [HttpGet("{id:guid}/entries")]
    public async Task<ActionResult<HalCollection<PayrollEntryDto>>> GetEntries(Guid id)
    {
        var period = await _context.PayrollPeriods
            .FirstOrDefaultAsync(p => p.Id == id);

        if (period == null)
            return NotFound();

        var entries = await _context.PayrollEntries
            .Include(e => e.Employee)
            .Where(e => e.PayrollPeriodId == id)
            .OrderBy(e => e.Employee!.LastName)
            .ThenBy(e => e.Employee!.FirstName)
            .ToListAsync();

        var dtos = entries.Select(e => new PayrollEntryDto
        {
            Id = e.Id,
            PayrollPeriodId = e.PayrollPeriodId,
            EmployeeId = e.EmployeeId,
            EmployeeName = e.Employee != null ? $"{e.Employee.FirstName} {e.Employee.LastName}" : string.Empty,
            EmployeeNumber = e.Employee?.EmployeeNumber ?? string.Empty,
            RegularHours = e.RegularHours,
            OvertimeHours = e.OvertimeHours,
            RegularPay = e.RegularPay,
            OvertimePay = e.OvertimePay,
            TipIncome = e.TipIncome,
            GrossPay = e.GrossPay,
            Adjustments = e.Adjustments,
            AdjustmentNotes = e.AdjustmentNotes,
            Status = e.Status
        }).ToList();

        foreach (var dto in dtos)
            dto.AddSelfLink($"/api/payroll-periods/{id}/entries/{dto.Id}");

        return Ok(HalCollection<PayrollEntryDto>.Create(dtos, $"/api/payroll-periods/{id}/entries", dtos.Count));
    }

    /// <summary>
    /// Adjust a payroll entry.
    /// </summary>
    [HttpPut("{periodId:guid}/entries/{entryId:guid}")]
    public async Task<ActionResult<PayrollEntryDto>> AdjustEntry(
        Guid periodId,
        Guid entryId,
        [FromBody] AdjustPayrollEntryRequest request)
    {
        var entry = await _context.PayrollEntries
            .Include(e => e.Employee)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.PayrollPeriodId == periodId);

        if (entry == null)
            return NotFound();

        entry.Adjustments = request.Adjustments;
        entry.AdjustmentNotes = request.AdjustmentNotes;
        entry.GrossPay = entry.RegularPay + entry.OvertimePay + entry.TipIncome + entry.Adjustments;

        // Update period totals
        var period = await _context.PayrollPeriods
            .Include(p => p.Entries)
            .FirstAsync(p => p.Id == periodId);
        period.TotalGrossPay = period.Entries.Sum(e => e.GrossPay);

        await _context.SaveChangesAsync();

        var dto = new PayrollEntryDto
        {
            Id = entry.Id,
            PayrollPeriodId = entry.PayrollPeriodId,
            EmployeeId = entry.EmployeeId,
            EmployeeName = entry.Employee != null ? $"{entry.Employee.FirstName} {entry.Employee.LastName}" : string.Empty,
            EmployeeNumber = entry.Employee?.EmployeeNumber ?? string.Empty,
            RegularHours = entry.RegularHours,
            OvertimeHours = entry.OvertimeHours,
            RegularPay = entry.RegularPay,
            OvertimePay = entry.OvertimePay,
            TipIncome = entry.TipIncome,
            GrossPay = entry.GrossPay,
            Adjustments = entry.Adjustments,
            AdjustmentNotes = entry.AdjustmentNotes,
            Status = entry.Status
        };

        dto.AddSelfLink($"/api/payroll-periods/{periodId}/entries/{entry.Id}");

        return Ok(dto);
    }

    private static object GenerateAdpExport(PayrollPeriod period)
    {
        return period.Entries.Select(e => new
        {
            EmployeeId = e.Employee?.EmployeeNumber,
            RegularHours = e.RegularHours,
            OvertimeHours = e.OvertimeHours,
            GrossEarnings = e.GrossPay
        }).ToList();
    }

    private static object GenerateGustoExport(PayrollPeriod period)
    {
        return period.Entries.Select(e => new
        {
            employee_id = e.Employee?.EmployeeNumber,
            regular_hours = e.RegularHours,
            overtime_hours = e.OvertimeHours,
            regular_pay = e.RegularPay,
            overtime_pay = e.OvertimePay,
            tips = e.TipIncome
        }).ToList();
    }

    private static object GenerateDatevExport(PayrollPeriod period)
    {
        // DATEV LODAS format
        return period.Entries.Select(e => new
        {
            Personalnummer = e.Employee?.EmployeeNumber,
            Lohnart_1 = "1000", // Regular wage
            Betrag_1 = e.RegularPay,
            Lohnart_2 = "1100", // Overtime
            Betrag_2 = e.OvertimePay,
            Lohnart_3 = "1200", // Tips
            Betrag_3 = e.TipIncome
        }).ToList();
    }

    private static object GenerateGenericExport(PayrollPeriod period)
    {
        return period.Entries.Select(e => new
        {
            employeeId = e.EmployeeId,
            employeeNumber = e.Employee?.EmployeeNumber,
            employeeName = e.Employee != null ? $"{e.Employee.FirstName} {e.Employee.LastName}" : string.Empty,
            regularHours = e.RegularHours,
            overtimeHours = e.OvertimeHours,
            regularPay = e.RegularPay,
            overtimePay = e.OvertimePay,
            tipIncome = e.TipIncome,
            adjustments = e.Adjustments,
            grossPay = e.GrossPay
        }).ToList();
    }

    private static PayrollPeriodDto MapToDto(PayrollPeriod period)
    {
        return new PayrollPeriodDto
        {
            Id = period.Id,
            TenantId = period.TenantId,
            PeriodStart = period.PeriodStart,
            PeriodEnd = period.PeriodEnd,
            Status = period.Status,
            TotalRegularHours = period.TotalRegularHours,
            TotalOvertimeHours = period.TotalOvertimeHours,
            TotalGrossPay = period.TotalGrossPay,
            TotalTips = period.TotalTips,
            ProcessedAt = period.ProcessedAt,
            ApprovedByUserId = period.ApprovedByUserId,
            ExportedAt = period.ExportedAt,
            ExportFormat = period.ExportFormat,
            EntryCount = period.Entries.Count,
            Entries = period.Entries.Select(e => new PayrollEntryDto
            {
                Id = e.Id,
                PayrollPeriodId = e.PayrollPeriodId,
                EmployeeId = e.EmployeeId,
                EmployeeName = e.Employee != null ? $"{e.Employee.FirstName} {e.Employee.LastName}" : string.Empty,
                EmployeeNumber = e.Employee?.EmployeeNumber ?? string.Empty,
                RegularHours = e.RegularHours,
                OvertimeHours = e.OvertimeHours,
                RegularPay = e.RegularPay,
                OvertimePay = e.OvertimePay,
                TipIncome = e.TipIncome,
                GrossPay = e.GrossPay,
                Adjustments = e.Adjustments,
                AdjustmentNotes = e.AdjustmentNotes,
                Status = e.Status
            }).ToList(),
            CreatedAt = period.CreatedAt,
            UpdatedAt = period.UpdatedAt
        };
    }

    private static void AddLinks(PayrollPeriodDto dto)
    {
        dto.AddSelfLink($"/api/payroll-periods/{dto.Id}");
        dto.AddLink("entries", $"/api/payroll-periods/{dto.Id}/entries");
    }

    private static void AddActionLinks(PayrollPeriodDto dto, PayrollPeriod period)
    {
        var baseUrl = $"/api/payroll-periods/{dto.Id}";

        switch (period.Status)
        {
            case "open":
                dto.AddLink("process", $"{baseUrl}/process");
                break;
            case "processing":
                dto.AddLink("approve", $"{baseUrl}/approve");
                dto.AddLink("reprocess", $"{baseUrl}/process");
                break;
            case "approved":
                dto.AddLink("export", $"{baseUrl}/export");
                break;
        }
    }
}
