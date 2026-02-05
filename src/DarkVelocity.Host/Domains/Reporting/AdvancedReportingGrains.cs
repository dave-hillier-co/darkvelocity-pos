using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Daypart Analysis Grain
// ============================================================================

/// <summary>
/// Grain for daypart analysis at site level.
/// Aggregates sales and labor by hour and daypart for performance analysis.
/// </summary>
public class DaypartAnalysisGrain : Grain, IDaypartAnalysisGrain
{
    private readonly IPersistentState<DaypartAnalysisState> _state;

    public DaypartAnalysisGrain(
        [PersistentState("daypartAnalysis", "OrleansStorage")]
        IPersistentState<DaypartAnalysisState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(DateTime businessDate, Guid siteId)
    {
        if (_state.State.SiteId != Guid.Empty)
            return; // Already initialized

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State = new DaypartAnalysisState
        {
            OrgId = orgId,
            SiteId = siteId,
            BusinessDate = businessDate,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task RecordHourlySaleAsync(RecordHourlySaleCommand command)
    {
        EnsureInitialized();

        if (!_state.State.HourlyData.TryGetValue(command.Hour, out var hourlyData))
        {
            hourlyData = new HourlyData { Hour = command.Hour };
            _state.State.HourlyData[command.Hour] = hourlyData;
        }

        hourlyData.NetSales += command.NetSales;
        hourlyData.TransactionCount += command.TransactionCount;
        hourlyData.GuestCount += command.GuestCount;
        hourlyData.TheoreticalCOGS += command.TheoreticalCOGS;

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RecordHourlyLaborAsync(RecordHourlyLaborCommand command)
    {
        EnsureInitialized();

        if (!_state.State.HourlyData.TryGetValue(command.Hour, out var hourlyData))
        {
            hourlyData = new HourlyData { Hour = command.Hour };
            _state.State.HourlyData[command.Hour] = hourlyData;
        }

        hourlyData.LaborHours += command.LaborHours;
        hourlyData.LaborCost += command.LaborCost;

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<DaypartAnalysisSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();

        var hourlyPerformances = _state.State.HourlyData.Values
            .OrderBy(h => h.Hour)
            .Select(h => new HourlyPerformance(
                Hour: h.Hour,
                NetSales: h.NetSales,
                TransactionCount: h.TransactionCount,
                GuestCount: h.GuestCount,
                LaborHours: h.LaborHours,
                SalesPerLaborHour: h.LaborHours > 0 ? h.NetSales / h.LaborHours : 0))
            .ToList();

        var totalDailySales = _state.State.HourlyData.Values.Sum(h => h.NetSales);

        var daypartPerformances = _state.State.DaypartDefinitions
            .Select(def =>
            {
                var hoursInDaypart = GetHoursInDaypart(def);
                var daypartData = _state.State.HourlyData
                    .Where(kvp => hoursInDaypart.Contains(kvp.Key))
                    .Select(kvp => kvp.Value)
                    .ToList();

                var netSales = daypartData.Sum(h => h.NetSales);
                var transactions = daypartData.Sum(h => h.TransactionCount);
                var guests = daypartData.Sum(h => h.GuestCount);
                var laborCost = daypartData.Sum(h => h.LaborCost);
                var laborHours = daypartData.Sum(h => h.LaborHours);

                return new DaypartPerformance(
                    Daypart: def.Daypart,
                    NetSales: netSales,
                    PercentOfDailySales: totalDailySales > 0 ? netSales / totalDailySales * 100 : 0,
                    TransactionCount: transactions,
                    GuestCount: guests,
                    AverageTicket: transactions > 0 ? netSales / transactions : 0,
                    LaborCost: laborCost,
                    SalesPerLaborHour: laborHours > 0 ? netSales / laborHours : 0,
                    ComparisonVsLastWeek: 0); // Would need historical data
            })
            .ToList();

        var peakDaypart = daypartPerformances
            .OrderByDescending(d => d.NetSales)
            .FirstOrDefault()?.Daypart ?? DayPart.Lunch;

        var peakHour = hourlyPerformances
            .OrderByDescending(h => h.NetSales)
            .FirstOrDefault()?.Hour ?? 12;

        var snapshot = new DaypartAnalysisSnapshot(
            BusinessDate: _state.State.BusinessDate,
            SiteId: _state.State.SiteId,
            DaypartPerformances: daypartPerformances,
            HourlyPerformances: hourlyPerformances,
            PeakDaypart: peakDaypart,
            PeakHour: peakHour);

        return Task.FromResult(snapshot);
    }

    public async Task<DaypartPerformance> GetDaypartPerformanceAsync(DayPart daypart)
    {
        var snapshot = await GetSnapshotAsync();
        return snapshot.DaypartPerformances.FirstOrDefault(d => d.Daypart == daypart)
            ?? new DaypartPerformance(daypart, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    public Task<IReadOnlyList<HourlyPerformance>> GetHourlyPerformanceAsync()
    {
        EnsureInitialized();

        var performances = _state.State.HourlyData.Values
            .OrderBy(h => h.Hour)
            .Select(h => new HourlyPerformance(
                Hour: h.Hour,
                NetSales: h.NetSales,
                TransactionCount: h.TransactionCount,
                GuestCount: h.GuestCount,
                LaborHours: h.LaborHours,
                SalesPerLaborHour: h.LaborHours > 0 ? h.NetSales / h.LaborHours : 0))
            .ToList();

        return Task.FromResult<IReadOnlyList<HourlyPerformance>>(performances);
    }

    public async Task SetDaypartDefinitionsAsync(IReadOnlyList<DaypartDefinition> definitions)
    {
        EnsureInitialized();

        _state.State.DaypartDefinitions = definitions
            .Select(d => new DaypartDefinitionState
            {
                Daypart = d.Daypart,
                StartTime = d.StartTime,
                EndTime = d.EndTime,
                DisplayName = d.DisplayName
            })
            .ToList();

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task FinalizeAsync()
    {
        EnsureInitialized();
        _state.State.IsFinalized = true;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private HashSet<int> GetHoursInDaypart(DaypartDefinitionState def)
    {
        var hours = new HashSet<int>();
        var startHour = (int)def.StartTime.TotalHours;
        var endHour = (int)def.EndTime.TotalHours;

        if (startHour < endHour)
        {
            for (int h = startHour; h < endHour; h++)
                hours.Add(h);
        }
        else // Crosses midnight (e.g., Late Night 22:00 - 06:00)
        {
            for (int h = startHour; h < 24; h++)
                hours.Add(h);
            for (int h = 0; h < endHour; h++)
                hours.Add(h);
        }

        return hours;
    }

    private void EnsureInitialized()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Daypart analysis grain not initialized");
    }
}

// ============================================================================
// Labor Report Grain
// ============================================================================

/// <summary>
/// Grain for labor reporting at site level.
/// Tracks labor cost percentage, sales per labor hour, and overtime.
/// </summary>
public class LaborReportGrain : Grain, ILaborReportGrain
{
    private readonly IPersistentState<LaborReportState> _state;

    public LaborReportGrain(
        [PersistentState("laborReport", "OrleansStorage")]
        IPersistentState<LaborReportState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(DateTime periodStart, DateTime periodEnd, Guid siteId)
    {
        if (_state.State.SiteId != Guid.Empty)
            return; // Already initialized

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State = new LaborReportState
        {
            OrgId = orgId,
            SiteId = siteId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task RecordLaborEntryAsync(RecordLaborEntryCommand command)
    {
        EnsureInitialized();

        // Update employee entry
        if (!_state.State.LaborEntries.TryGetValue(command.EmployeeId, out var entry))
        {
            entry = new LaborEntryData
            {
                EmployeeId = command.EmployeeId,
                Department = command.Department
            };
            _state.State.LaborEntries[command.EmployeeId] = entry;
        }

        entry.RegularHours += command.RegularHours;
        entry.OvertimeHours += command.OvertimeHours;
        entry.RegularCost += command.RegularHours * command.RegularRate;
        entry.OvertimeCost += command.OvertimeHours * command.OvertimeRate;

        // Update department aggregation
        if (!_state.State.ByDepartment.TryGetValue(command.Department, out var deptData))
        {
            deptData = new DepartmentLaborData { Department = command.Department };
            _state.State.ByDepartment[command.Department] = deptData;
        }

        deptData.LaborHours += command.RegularHours + command.OvertimeHours;
        deptData.LaborCost += (command.RegularHours * command.RegularRate) + (command.OvertimeHours * command.OvertimeRate);
        deptData.OvertimeHours += command.OvertimeHours;
        deptData.Employees.Add(command.EmployeeId);

        // Update daypart aggregation if provided
        if (command.Daypart.HasValue)
        {
            if (!_state.State.ByDaypart.TryGetValue(command.Daypart.Value, out var daypartData))
            {
                daypartData = new DaypartLaborData { Daypart = command.Daypart.Value };
                _state.State.ByDaypart[command.Daypart.Value] = daypartData;
            }

            daypartData.LaborHours += command.RegularHours + command.OvertimeHours;
            daypartData.LaborCost += (command.RegularHours * command.RegularRate) + (command.OvertimeHours * command.OvertimeRate);
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RecordScheduledHoursAsync(decimal scheduledHours, decimal scheduledCost)
    {
        EnsureInitialized();

        _state.State.ScheduledHours = scheduledHours;
        _state.State.ScheduledCost = scheduledCost;

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RecordSalesAsync(decimal sales, decimal salesByDaypart)
    {
        EnsureInitialized();

        _state.State.TotalSales = sales;

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<LaborReportSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();

        var totalLaborCost = _state.State.LaborEntries.Values.Sum(e => e.RegularCost + e.OvertimeCost);
        var totalLaborHours = _state.State.LaborEntries.Values.Sum(e => e.RegularHours + e.OvertimeHours);
        var totalOvertimeHours = _state.State.LaborEntries.Values.Sum(e => e.OvertimeHours);
        var totalOvertimeCost = _state.State.LaborEntries.Values.Sum(e => e.OvertimeCost);

        var byDepartment = _state.State.ByDepartment.Values
            .Select(d => new DepartmentLaborMetrics(
                Department: d.Department,
                LaborCost: d.LaborCost,
                LaborHours: d.LaborHours,
                OvertimeHours: d.OvertimeHours,
                LaborCostPercent: _state.State.TotalSales > 0 ? d.LaborCost / _state.State.TotalSales * 100 : 0,
                EmployeeCount: d.Employees.Count))
            .ToList();

        var byDaypart = _state.State.ByDaypart.Values
            .Select(d => new DaypartLaborMetrics(
                Daypart: d.Daypart,
                LaborCost: d.LaborCost,
                LaborHours: d.LaborHours,
                Sales: d.Sales,
                SalesPerLaborHour: d.LaborHours > 0 ? d.Sales / d.LaborHours : 0,
                LaborCostPercent: d.Sales > 0 ? d.LaborCost / d.Sales * 100 : 0))
            .ToList();

        var scheduleVarianceHours = totalLaborHours - _state.State.ScheduledHours;

        var snapshot = new LaborReportSnapshot(
            PeriodStart: _state.State.PeriodStart,
            PeriodEnd: _state.State.PeriodEnd,
            SiteId: _state.State.SiteId,
            TotalLaborCost: totalLaborCost,
            TotalSales: _state.State.TotalSales,
            LaborCostPercent: _state.State.TotalSales > 0 ? totalLaborCost / _state.State.TotalSales * 100 : 0,
            TotalLaborHours: totalLaborHours,
            SalesPerLaborHour: totalLaborHours > 0 ? _state.State.TotalSales / totalLaborHours : 0,
            ScheduledHours: _state.State.ScheduledHours,
            ActualHours: totalLaborHours,
            ScheduleVarianceHours: scheduleVarianceHours,
            ScheduleVariancePercent: _state.State.ScheduledHours > 0 ? scheduleVarianceHours / _state.State.ScheduledHours * 100 : 0,
            OvertimeHours: totalOvertimeHours,
            OvertimeCost: totalOvertimeCost,
            OvertimePercent: totalLaborHours > 0 ? totalOvertimeHours / totalLaborHours * 100 : 0,
            ByDepartment: byDepartment,
            ByDaypart: byDaypart);

        return Task.FromResult(snapshot);
    }

    public Task<decimal> GetLaborCostPercentAsync()
    {
        EnsureInitialized();

        var totalLaborCost = _state.State.LaborEntries.Values.Sum(e => e.RegularCost + e.OvertimeCost);
        var laborCostPercent = _state.State.TotalSales > 0 ? totalLaborCost / _state.State.TotalSales * 100 : 0;

        return Task.FromResult(laborCostPercent);
    }

    public Task<decimal> GetSalesPerLaborHourAsync()
    {
        EnsureInitialized();

        var totalLaborHours = _state.State.LaborEntries.Values.Sum(e => e.RegularHours + e.OvertimeHours);
        var salesPerLaborHour = totalLaborHours > 0 ? _state.State.TotalSales / totalLaborHours : 0;

        return Task.FromResult(salesPerLaborHour);
    }

    public Task<IReadOnlyList<OvertimeAlert>> GetOvertimeAlertsAsync()
    {
        EnsureInitialized();

        var alerts = _state.State.LaborEntries.Values
            .Where(e => e.OvertimeHours > 0)
            .Select(e => new OvertimeAlert(
                EmployeeId: e.EmployeeId,
                EmployeeName: e.EmployeeName,
                OvertimeHours: e.OvertimeHours,
                OvertimeCost: e.OvertimeCost,
                WeeklyHoursTotal: e.RegularHours + e.OvertimeHours))
            .OrderByDescending(a => a.OvertimeHours)
            .ToList();

        return Task.FromResult<IReadOnlyList<OvertimeAlert>>(alerts);
    }

    public async Task FinalizeAsync()
    {
        EnsureInitialized();
        _state.State.IsFinalized = true;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private void EnsureInitialized()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Labor report grain not initialized");
    }
}

// ============================================================================
// Product Mix Grain
// ============================================================================

/// <summary>
/// Grain for product mix analysis at site level.
/// Tracks item sales velocity, category performance, and modifier popularity.
/// </summary>
public class ProductMixGrain : Grain, IProductMixGrain
{
    private readonly IPersistentState<ProductMixState> _state;

    public ProductMixGrain(
        [PersistentState("productMix", "OrleansStorage")]
        IPersistentState<ProductMixState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(DateTime businessDate, Guid siteId)
    {
        if (_state.State.SiteId != Guid.Empty)
            return; // Already initialized

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State = new ProductMixState
        {
            OrgId = orgId,
            SiteId = siteId,
            BusinessDate = businessDate,
            OperatingHours = 12, // Default 12 hours
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task RecordProductSaleAsync(RecordProductSaleCommand command)
    {
        EnsureInitialized();

        // Update product data
        if (!_state.State.Products.TryGetValue(command.ProductId, out var productData))
        {
            productData = new ProductSalesData
            {
                ProductId = command.ProductId,
                ProductName = command.ProductName,
                Category = command.Category
            };
            _state.State.Products[command.ProductId] = productData;
        }

        productData.QuantitySold += command.Quantity;
        productData.NetSales += command.NetSales;
        productData.COGS += command.COGS;

        // Update total sales
        _state.State.TotalSales += command.NetSales;

        // Update modifier data
        foreach (var modifier in command.Modifiers)
        {
            if (!_state.State.Modifiers.TryGetValue(modifier.ModifierId, out var modifierData))
            {
                modifierData = new ModifierSalesData
                {
                    ModifierId = modifier.ModifierId,
                    ModifierName = modifier.ModifierName
                };
                _state.State.Modifiers[modifier.ModifierId] = modifierData;
            }

            modifierData.TimesApplied++;
            modifierData.TotalRevenue += modifier.Price;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RecordVoidAsync(RecordVoidCommand command)
    {
        EnsureInitialized();

        _state.State.Voids.Add(new VoidEntry
        {
            ProductId = command.ProductId,
            Reason = command.Reason,
            Amount = command.Amount
        });

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RecordCompAsync(RecordCompCommand command)
    {
        EnsureInitialized();

        _state.State.Comps.Add(new CompEntry
        {
            ProductId = command.ProductId,
            Reason = command.Reason,
            Amount = command.Amount
        });

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetOperatingHoursAsync(decimal operatingHours)
    {
        EnsureInitialized();

        _state.State.OperatingHours = operatingHours;

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<ProductMixSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();

        var products = GetProductPerformances();
        var categories = GetCategoryPerformancesInternal();
        var modifiers = GetModifierPerformances();
        var voidCompAnalysis = GetVoidCompAnalysisInternal();

        var snapshot = new ProductMixSnapshot(
            PeriodStart: _state.State.BusinessDate,
            PeriodEnd: _state.State.BusinessDate,
            SiteId: _state.State.SiteId,
            Products: products,
            Categories: categories,
            Modifiers: modifiers,
            VoidCompAnalysis: voidCompAnalysis);

        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<ProductPerformance>> GetTopProductsAsync(int count, string sortBy)
    {
        EnsureInitialized();

        var products = GetProductPerformances();

        var sorted = sortBy.ToLowerInvariant() switch
        {
            "quantity" => products.OrderByDescending(p => p.QuantitySold),
            "profit" => products.OrderByDescending(p => p.GrossProfit),
            _ => products.OrderByDescending(p => p.NetSales)
        };

        return Task.FromResult<IReadOnlyList<ProductPerformance>>(sorted.Take(count).ToList());
    }

    public Task<IReadOnlyList<CategoryPerformance>> GetCategoryPerformanceAsync()
    {
        EnsureInitialized();
        return Task.FromResult<IReadOnlyList<CategoryPerformance>>(GetCategoryPerformancesInternal());
    }

    public Task<VoidCompAnalysis> GetVoidCompAnalysisAsync()
    {
        EnsureInitialized();
        return Task.FromResult(GetVoidCompAnalysisInternal());
    }

    public async Task FinalizeAsync()
    {
        EnsureInitialized();
        _state.State.IsFinalized = true;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private List<ProductPerformance> GetProductPerformances()
    {
        var totalSales = _state.State.TotalSales;
        var operatingHours = _state.State.OperatingHours > 0 ? _state.State.OperatingHours : 12;

        var products = _state.State.Products.Values
            .Select(p => new
            {
                Product = p,
                GrossProfit = p.NetSales - p.COGS,
                GrossProfitPercent = p.NetSales > 0 ? (p.NetSales - p.COGS) / p.NetSales * 100 : 0
            })
            .ToList();

        var rankedByQuantity = products.OrderByDescending(p => p.Product.QuantitySold).ToList();
        var rankedByRevenue = products.OrderByDescending(p => p.Product.NetSales).ToList();
        var rankedByProfit = products.OrderByDescending(p => p.GrossProfit).ToList();

        return products.Select(p => new ProductPerformance(
            ProductId: p.Product.ProductId,
            ProductName: p.Product.ProductName,
            Category: p.Product.Category,
            QuantitySold: p.Product.QuantitySold,
            NetSales: p.Product.NetSales,
            PercentOfSales: totalSales > 0 ? p.Product.NetSales / totalSales * 100 : 0,
            GrossProfit: p.GrossProfit,
            GrossProfitPercent: p.GrossProfitPercent,
            SalesVelocity: p.Product.QuantitySold / operatingHours,
            RankByQuantity: rankedByQuantity.FindIndex(x => x.Product.ProductId == p.Product.ProductId) + 1,
            RankByRevenue: rankedByRevenue.FindIndex(x => x.Product.ProductId == p.Product.ProductId) + 1,
            RankByProfit: rankedByProfit.FindIndex(x => x.Product.ProductId == p.Product.ProductId) + 1))
            .OrderByDescending(p => p.NetSales)
            .ToList();
    }

    private List<CategoryPerformance> GetCategoryPerformancesInternal()
    {
        var totalSales = _state.State.TotalSales;

        return _state.State.Products.Values
            .GroupBy(p => p.Category)
            .Select(g =>
            {
                var netSales = g.Sum(p => p.NetSales);
                var cogs = g.Sum(p => p.COGS);
                var grossProfit = netSales - cogs;
                var quantitySold = g.Sum(p => p.QuantitySold);

                return new CategoryPerformance(
                    Category: g.Key,
                    ItemCount: g.Count(),
                    QuantitySold: quantitySold,
                    NetSales: netSales,
                    PercentOfSales: totalSales > 0 ? netSales / totalSales * 100 : 0,
                    GrossProfit: grossProfit,
                    GrossProfitPercent: netSales > 0 ? grossProfit / netSales * 100 : 0,
                    AverageItemPrice: quantitySold > 0 ? netSales / quantitySold : 0);
            })
            .OrderByDescending(c => c.NetSales)
            .ToList();
    }

    private List<ModifierPerformance> GetModifierPerformances()
    {
        var totalItems = _state.State.Products.Values.Sum(p => p.QuantitySold);

        return _state.State.Modifiers.Values
            .Select(m => new ModifierPerformance(
                ModifierId: m.ModifierId,
                ModifierName: m.ModifierName,
                TimesApplied: m.TimesApplied,
                TotalRevenue: m.TotalRevenue,
                AveragePerApplication: m.TimesApplied > 0 ? m.TotalRevenue / m.TimesApplied : 0,
                AttachmentRate: totalItems > 0 ? (decimal)m.TimesApplied / totalItems * 100 : 0))
            .OrderByDescending(m => m.TimesApplied)
            .ToList();
    }

    private VoidCompAnalysis GetVoidCompAnalysisInternal()
    {
        var totalVoidAmount = _state.State.Voids.Sum(v => v.Amount);
        var totalCompAmount = _state.State.Comps.Sum(c => c.Amount);
        var grossSales = _state.State.TotalSales + totalVoidAmount + totalCompAmount;

        var voidsByReason = _state.State.Voids
            .GroupBy(v => v.Reason)
            .Select(g => new VoidReasonBreakdown(
                Reason: g.Key,
                Count: g.Count(),
                Amount: g.Sum(v => v.Amount)))
            .OrderByDescending(v => v.Amount)
            .ToList();

        var compsByReason = _state.State.Comps
            .GroupBy(c => c.Reason)
            .Select(g => new CompReasonBreakdown(
                Reason: g.Key,
                Count: g.Count(),
                Amount: g.Sum(c => c.Amount)))
            .OrderByDescending(c => c.Amount)
            .ToList();

        return new VoidCompAnalysis(
            TotalVoids: _state.State.Voids.Count,
            TotalVoidAmount: totalVoidAmount,
            VoidPercent: grossSales > 0 ? totalVoidAmount / grossSales * 100 : 0,
            TotalComps: _state.State.Comps.Count,
            TotalCompAmount: totalCompAmount,
            CompPercent: grossSales > 0 ? totalCompAmount / grossSales * 100 : 0,
            VoidsByReason: voidsByReason,
            CompsByReason: compsByReason);
    }

    private void EnsureInitialized()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Product mix grain not initialized");
    }
}

// ============================================================================
// Payment Reconciliation Grain
// ============================================================================

/// <summary>
/// Grain for payment reconciliation at site level.
/// Compares POS totals to processor settlements and flags discrepancies.
/// </summary>
public class PaymentReconciliationGrain : Grain, IPaymentReconciliationGrain
{
    private readonly IPersistentState<PaymentReconciliationState> _state;

    public PaymentReconciliationGrain(
        [PersistentState("paymentReconciliation", "OrleansStorage")]
        IPersistentState<PaymentReconciliationState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(DateTime businessDate, Guid siteId)
    {
        if (_state.State.SiteId != Guid.Empty)
            return; // Already initialized

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State = new PaymentReconciliationState
        {
            OrgId = orgId,
            SiteId = siteId,
            BusinessDate = businessDate,
            Status = PaymentReconciliationStatus.Pending,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task RecordPosPaymentAsync(RecordPosPaymentCommand command)
    {
        EnsureInitialized();

        if (!_state.State.PosPayments.TryGetValue(command.PaymentMethod, out var paymentData))
        {
            paymentData = new PosPaymentData
            {
                PaymentMethod = command.PaymentMethod,
                ProcessorName = command.ProcessorName
            };
            _state.State.PosPayments[command.PaymentMethod] = paymentData;
        }

        paymentData.Amount += command.Amount;
        paymentData.TransactionCount++;

        // Track cash expected
        if (command.PaymentMethod.Equals("Cash", StringComparison.OrdinalIgnoreCase))
        {
            _state.State.CashExpected += command.Amount;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RecordProcessorSettlementAsync(RecordProcessorSettlementCommand command)
    {
        EnsureInitialized();

        _state.State.ProcessorSettlements.Add(new ProcessorSettlementData
        {
            ProcessorName = command.ProcessorName,
            BatchId = command.BatchId,
            GrossAmount = command.GrossAmount,
            Fees = command.Fees,
            NetAmount = command.NetAmount,
            TransactionCount = command.TransactionCount,
            SettlementDate = command.SettlementDate,
            Status = PaymentReconciliationStatus.Pending
        });

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RecordCashCountAsync(RecordCashCountCommand command)
    {
        EnsureInitialized();

        _state.State.CashActual = command.CashCounted;
        _state.State.CashCountedBy = command.CountedBy;

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<PaymentReconciliationSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();

        var posTotalCash = _state.State.PosPayments
            .Where(p => p.Key.Equals("Cash", StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Value.Amount);

        var posTotalCard = _state.State.PosPayments
            .Where(p => p.Key.Contains("Card", StringComparison.OrdinalIgnoreCase) ||
                        p.Key.Equals("Credit", StringComparison.OrdinalIgnoreCase) ||
                        p.Key.Equals("Debit", StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Value.Amount);

        var posTotalOther = _state.State.PosPayments
            .Where(p => !p.Key.Equals("Cash", StringComparison.OrdinalIgnoreCase) &&
                        !p.Key.Contains("Card", StringComparison.OrdinalIgnoreCase) &&
                        !p.Key.Equals("Credit", StringComparison.OrdinalIgnoreCase) &&
                        !p.Key.Equals("Debit", StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Value.Amount);

        var processorTotalSettled = _state.State.ProcessorSettlements.Sum(s => s.GrossAmount);
        var processorFees = _state.State.ProcessorSettlements.Sum(s => s.Fees);
        var processorNetSettlement = _state.State.ProcessorSettlements.Sum(s => s.NetAmount);

        var processorSettlements = _state.State.ProcessorSettlements
            .Select(s => new ProcessorSettlement(
                ProcessorName: s.ProcessorName,
                BatchId: s.BatchId,
                GrossAmount: s.GrossAmount,
                Fees: s.Fees,
                NetAmount: s.NetAmount,
                TransactionCount: s.TransactionCount,
                SettlementDate: s.SettlementDate,
                Status: s.Status))
            .ToList();

        var exceptions = _state.State.Exceptions
            .Select(e => new ReconciliationException(
                ExceptionId: e.ExceptionId,
                ExceptionType: e.ExceptionType,
                Description: e.Description,
                Amount: e.Amount,
                TransactionReference: e.TransactionReference,
                Status: e.Status,
                Resolution: e.Resolution,
                ResolvedAt: e.ResolvedAt,
                ResolvedBy: e.ResolvedBy))
            .ToList();

        var snapshot = new PaymentReconciliationSnapshot(
            BusinessDate: _state.State.BusinessDate,
            SiteId: _state.State.SiteId,
            Status: _state.State.Status,
            PosTotalCash: posTotalCash,
            PosTotalCard: posTotalCard,
            PosTotalOther: posTotalOther,
            PosGrandTotal: posTotalCash + posTotalCard + posTotalOther,
            ProcessorTotalSettled: processorTotalSettled,
            ProcessorFees: processorFees,
            ProcessorNetSettlement: processorNetSettlement,
            CashExpected: _state.State.CashExpected,
            CashActual: _state.State.CashActual,
            CashVariance: _state.State.CashActual - _state.State.CashExpected,
            CardVariance: posTotalCard - processorTotalSettled,
            ProcessorSettlements: processorSettlements,
            Exceptions: exceptions,
            ReconciledAt: _state.State.ReconciledAt,
            ReconciledBy: _state.State.ReconciledBy);

        return Task.FromResult(snapshot);
    }

    public async Task ReconcileAsync()
    {
        EnsureInitialized();

        var cashVariance = _state.State.CashActual - _state.State.CashExpected;
        var cardTotal = _state.State.PosPayments
            .Where(p => p.Key.Contains("Card", StringComparison.OrdinalIgnoreCase) ||
                        p.Key.Equals("Credit", StringComparison.OrdinalIgnoreCase) ||
                        p.Key.Equals("Debit", StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Value.Amount);
        var processorTotal = _state.State.ProcessorSettlements.Sum(s => s.GrossAmount);
        var cardVariance = cardTotal - processorTotal;

        // Check for cash discrepancy
        if (Math.Abs(cashVariance) > 1.00m) // $1 tolerance
        {
            _state.State.Exceptions.Add(new ReconciliationExceptionData
            {
                ExceptionId = Guid.NewGuid(),
                ExceptionType = "CashVariance",
                Description = cashVariance > 0 ? "Cash over" : "Cash short",
                Amount = cashVariance,
                Status = PaymentReconciliationStatus.Exception
            });
        }

        // Check for card discrepancy
        if (Math.Abs(cardVariance) > 0.01m) // 1 cent tolerance
        {
            _state.State.Exceptions.Add(new ReconciliationExceptionData
            {
                ExceptionId = Guid.NewGuid(),
                ExceptionType = "CardVariance",
                Description = $"Card payment variance: POS ${cardTotal:F2}, Processor ${processorTotal:F2}",
                Amount = cardVariance,
                Status = PaymentReconciliationStatus.Exception
            });
        }

        // Update settlement statuses
        foreach (var settlement in _state.State.ProcessorSettlements)
        {
            var posForProcessor = _state.State.PosPayments.Values
                .Where(p => p.ProcessorName == settlement.ProcessorName)
                .Sum(p => p.Amount);

            settlement.Status = Math.Abs(posForProcessor - settlement.GrossAmount) < 0.01m
                ? PaymentReconciliationStatus.Matched
                : PaymentReconciliationStatus.Discrepancy;
        }

        // Determine overall status
        _state.State.Status = _state.State.Exceptions.Any(e => e.Status == PaymentReconciliationStatus.Exception)
            ? PaymentReconciliationStatus.Discrepancy
            : PaymentReconciliationStatus.Matched;

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task ResolveExceptionAsync(ResolveExceptionCommand command)
    {
        EnsureInitialized();

        var exception = _state.State.Exceptions.FirstOrDefault(e => e.ExceptionId == command.ExceptionId);
        if (exception != null)
        {
            exception.Resolution = command.Resolution;
            exception.ResolvedAt = DateTime.UtcNow;
            exception.ResolvedBy = command.ResolvedBy;
            exception.Status = PaymentReconciliationStatus.Resolved;
        }

        // Update overall status if all exceptions resolved
        if (_state.State.Exceptions.All(e => e.Status == PaymentReconciliationStatus.Resolved))
        {
            _state.State.Status = PaymentReconciliationStatus.Matched;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<ReconciliationException>> GetExceptionsAsync()
    {
        EnsureInitialized();

        var exceptions = _state.State.Exceptions
            .Select(e => new ReconciliationException(
                ExceptionId: e.ExceptionId,
                ExceptionType: e.ExceptionType,
                Description: e.Description,
                Amount: e.Amount,
                TransactionReference: e.TransactionReference,
                Status: e.Status,
                Resolution: e.Resolution,
                ResolvedAt: e.ResolvedAt,
                ResolvedBy: e.ResolvedBy))
            .ToList();

        return Task.FromResult<IReadOnlyList<ReconciliationException>>(exceptions);
    }

    public Task<decimal> GetTotalVarianceAsync()
    {
        EnsureInitialized();

        var cashVariance = _state.State.CashActual - _state.State.CashExpected;
        var cardTotal = _state.State.PosPayments
            .Where(p => p.Key.Contains("Card", StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Value.Amount);
        var processorTotal = _state.State.ProcessorSettlements.Sum(s => s.GrossAmount);
        var cardVariance = cardTotal - processorTotal;

        return Task.FromResult(cashVariance + cardVariance);
    }

    public async Task FinalizeAsync(Guid reconciledBy)
    {
        EnsureInitialized();

        _state.State.ReconciledAt = DateTime.UtcNow;
        _state.State.ReconciledBy = reconciledBy;

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private void EnsureInitialized()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Payment reconciliation grain not initialized");
    }
}
