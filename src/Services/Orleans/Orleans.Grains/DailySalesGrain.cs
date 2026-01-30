using DarkVelocity.Orleans.Abstractions.Costing;
using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.Projections;
using DarkVelocity.Orleans.Abstractions.State;
using DarkVelocity.Shared.Contracts.Events;
using Orleans.Runtime;

namespace DarkVelocity.Orleans.Grains;

/// <summary>
/// Grain for daily sales aggregation at site level.
/// Aggregates sales data for reporting and GP calculations.
/// </summary>
public class DailySalesGrain : Grain, IDailySalesGrain
{
    private readonly IPersistentState<DailySalesState> _state;

    public DailySalesGrain(
        [PersistentState("dailySales", "OrleansStorage")]
        IPersistentState<DailySalesState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(DailySalesAggregationCommand command)
    {
        if (_state.State.SiteId != Guid.Empty)
            return; // Already initialized

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State = new DailySalesState
        {
            OrgId = orgId,
            SiteId = command.SiteId,
            SiteName = command.SiteName,
            BusinessDate = command.BusinessDate,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task RecordSaleAsync(RecordSaleCommand command)
    {
        EnsureInitialized();

        // Update aggregated totals
        _state.State.GrossSales += command.GrossSales;
        _state.State.Discounts += command.Discounts;
        _state.State.Voids += command.Voids;
        _state.State.Comps += command.Comps;
        _state.State.Tax += command.Tax;
        _state.State.NetSales += command.NetSales;
        _state.State.TheoreticalCOGS += command.TheoreticalCOGS;
        _state.State.ActualCOGS += command.ActualCOGS ?? 0;
        _state.State.TransactionCount++;
        _state.State.GuestCount += command.GuestCount;

        // Update channel breakdown
        if (!_state.State.SalesByChannel.TryGetValue(command.Channel, out var channelTotal))
            channelTotal = 0;
        _state.State.SalesByChannel[command.Channel] = channelTotal + command.NetSales;

        // Update category breakdown
        if (!_state.State.SalesByCategory.TryGetValue(command.Category, out var categoryTotal))
            categoryTotal = 0;
        _state.State.SalesByCategory[command.Category] = categoryTotal + command.NetSales;

        // Create fact record
        var fact = new SalesFact
        {
            FactId = Guid.NewGuid(),
            Date = _state.State.BusinessDate,
            OrgId = _state.State.OrgId,
            SiteId = _state.State.SiteId,
            SiteName = _state.State.SiteName,
            Channel = command.Channel,
            ProductId = command.ProductId,
            ProductName = command.ProductName,
            Category = command.Category,
            Quantity = command.Quantity,
            TransactionCount = 1,
            GrossSales = command.GrossSales,
            Discounts = command.Discounts,
            Voids = command.Voids,
            Comps = command.Comps,
            Tax = command.Tax,
            NetSales = command.NetSales,
            TheoreticalCOGS = command.TheoreticalCOGS,
            ActualCOGS = command.ActualCOGS,
            COGSVariance = command.ActualCOGS.HasValue ? command.ActualCOGS.Value - command.TheoreticalCOGS : null,
            WeekNumber = GetIso8601WeekNumber(_state.State.BusinessDate),
            PeriodNumber = GetFourWeekPeriod(_state.State.BusinessDate),
            FiscalYear = _state.State.BusinessDate.Year
        };

        _state.State.Facts.Add(fact);
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<DailySalesSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();

        var grossProfit = _state.State.NetSales - _state.State.ActualCOGS;
        var grossProfitPercent = _state.State.NetSales > 0
            ? grossProfit / _state.State.NetSales * 100
            : 0;

        var snapshot = new DailySalesSnapshot(
            Date: _state.State.BusinessDate,
            SiteId: _state.State.SiteId,
            SiteName: _state.State.SiteName,
            GrossSales: _state.State.GrossSales,
            NetSales: _state.State.NetSales,
            TheoreticalCOGS: _state.State.TheoreticalCOGS,
            ActualCOGS: _state.State.ActualCOGS,
            GrossProfit: grossProfit,
            GrossProfitPercent: grossProfitPercent,
            TransactionCount: _state.State.TransactionCount,
            GuestCount: _state.State.GuestCount,
            AverageTicket: _state.State.TransactionCount > 0
                ? _state.State.NetSales / _state.State.TransactionCount
                : 0,
            SalesByChannel: _state.State.SalesByChannel,
            SalesByCategory: _state.State.SalesByCategory);

        return Task.FromResult(snapshot);
    }

    public Task<SalesMetrics> GetMetricsAsync()
    {
        EnsureInitialized();

        var metrics = new SalesMetrics
        {
            OrgId = _state.State.OrgId,
            SiteId = _state.State.SiteId,
            PeriodStart = _state.State.BusinessDate,
            PeriodEnd = _state.State.BusinessDate,
            PeriodType = "Daily",
            GrossSales = _state.State.GrossSales,
            Discounts = _state.State.Discounts,
            Voids = _state.State.Voids,
            Comps = _state.State.Comps,
            Tax = _state.State.Tax,
            NetSales = _state.State.NetSales,
            TransactionCount = _state.State.TransactionCount,
            CoversServed = _state.State.GuestCount
        };

        return Task.FromResult(metrics);
    }

    public Task<GrossProfitMetrics> GetGrossProfitMetricsAsync(CostingMethod method)
    {
        EnsureInitialized();

        var metrics = new GrossProfitMetrics
        {
            OrgId = _state.State.OrgId,
            SiteId = _state.State.SiteId,
            PeriodStart = _state.State.BusinessDate,
            PeriodEnd = _state.State.BusinessDate,
            PeriodType = "Daily",
            NetSales = _state.State.NetSales,
            ActualCOGS = _state.State.ActualCOGS,
            TheoreticalCOGS = _state.State.TheoreticalCOGS,
            CostingMethod = method
        };

        return Task.FromResult(metrics);
    }

    public Task<IReadOnlyList<SalesFact>> GetFactsAsync()
    {
        EnsureInitialized();
        return Task.FromResult<IReadOnlyList<SalesFact>>(_state.State.Facts);
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
            throw new InvalidOperationException("Daily sales grain not initialized");
    }

    private static int GetIso8601WeekNumber(DateTime date)
    {
        var day = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(date);
        if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            date = date.AddDays(3);
        return System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
            date,
            System.Globalization.CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday);
    }

    private static int GetFourWeekPeriod(DateTime date)
    {
        var dayOfYear = date.DayOfYear;
        return (dayOfYear - 1) / 28 + 1; // 1-13
    }
}
