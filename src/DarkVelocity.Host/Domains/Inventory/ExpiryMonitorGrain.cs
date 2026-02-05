using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

public class ExpiryMonitorGrain : Grain, IExpiryMonitorGrain
{
    private readonly IPersistentState<ExpiryMonitorState> _state;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ExpiryMonitorGrain> _logger;
    private Lazy<IAsyncStream<IStreamEvent>>? _alertStream;

    public ExpiryMonitorGrain(
        [PersistentState("expiryMonitor", "OrleansStorage")] IPersistentState<ExpiryMonitorState> state,
        IGrainFactory grainFactory,
        ILogger<ExpiryMonitorGrain> logger)
    {
        _state = state;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.OrganizationId != Guid.Empty)
        {
            InitializeLazyFields();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    private void InitializeLazyFields()
    {
        var orgId = _state.State.OrganizationId;

        _alertStream = new Lazy<IAsyncStream<IStreamEvent>>(() =>
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.AlertStreamNamespace, orgId.ToString());
            return streamProvider.GetStream<IStreamEvent>(streamId);
        });
    }

    private IAsyncStream<IStreamEvent> AlertStream => _alertStream!.Value;

    public async Task InitializeAsync(Guid organizationId, Guid siteId)
    {
        if (_state.State.OrganizationId != Guid.Empty)
            throw new InvalidOperationException("Expiry monitor already initialized");

        _state.State.OrganizationId = organizationId;
        _state.State.SiteId = siteId;
        _state.State.Settings = new ExpiryMonitorSettings();

        await _state.WriteStateAsync();
        InitializeLazyFields();

        _logger.LogInformation(
            "Expiry monitor initialized for site {SiteId}",
            siteId);
    }

    public async Task ConfigureAsync(ExpiryMonitorSettings settings)
    {
        EnsureExists();

        _state.State.Settings = settings;
        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Expiry monitor configured: Warning={Warning}d, Urgent={Urgent}d, Critical={Critical}d",
            settings.WarningDays,
            settings.UrgentDays,
            settings.CriticalDays);
    }

    public Task<ExpiryMonitorSettings> GetSettingsAsync()
    {
        return Task.FromResult(_state.State.Settings);
    }

    public async Task RegisterIngredientAsync(Guid ingredientId, string ingredientName, string sku, string category)
    {
        EnsureExists();

        _state.State.MonitoredIngredients[ingredientId] = new MonitoredIngredient
        {
            IngredientId = ingredientId,
            IngredientName = ingredientName,
            Sku = sku,
            Category = category,
            RegisteredAt = DateTime.UtcNow
        };

        await _state.WriteStateAsync();
    }

    public async Task UnregisterIngredientAsync(Guid ingredientId)
    {
        EnsureExists();

        _state.State.MonitoredIngredients.Remove(ingredientId);
        await _state.WriteStateAsync();
    }

    public async Task<ExpiryReport> ScanForExpiringItemsAsync()
    {
        EnsureExists();

        var now = DateTime.UtcNow;
        var settings = _state.State.Settings;
        var expiringItems = new List<ExpiringItem>();

        foreach (var ingredient in _state.State.MonitoredIngredients.Values)
        {
            var inventoryGrain = _grainFactory.GetGrain<IInventoryGrain>(
                GrainKeys.Inventory(_state.State.OrganizationId, _state.State.SiteId, ingredient.IngredientId));

            if (!await inventoryGrain.ExistsAsync())
                continue;

            var batches = await inventoryGrain.GetActiveBatchesAsync();
            var state = await inventoryGrain.GetStateAsync();

            foreach (var batch in batches.Where(b => b.ExpiryDate.HasValue && b.Quantity > 0))
            {
                var daysUntilExpiry = (int)(batch.ExpiryDate!.Value - now).TotalDays;
                var urgency = CalculateUrgency(daysUntilExpiry, settings);

                if (urgency != ExpiryUrgency.Normal)
                {
                    expiringItems.Add(new ExpiringItem
                    {
                        IngredientId = ingredient.IngredientId,
                        IngredientName = ingredient.IngredientName,
                        Sku = ingredient.Sku,
                        Category = ingredient.Category,
                        BatchId = batch.Id,
                        BatchNumber = batch.BatchNumber,
                        ExpiryDate = batch.ExpiryDate!.Value,
                        DaysUntilExpiry = daysUntilExpiry,
                        Quantity = batch.Quantity,
                        Unit = state.Unit,
                        UnitCost = batch.UnitCost,
                        ValueAtRisk = batch.Quantity * batch.UnitCost,
                        Urgency = urgency,
                        Location = batch.Location
                    });
                }
            }
        }

        var expiredItems = expiringItems.Where(i => i.Urgency == ExpiryUrgency.Expired).ToList();
        var criticalItems = expiringItems.Where(i => i.Urgency == ExpiryUrgency.Critical).ToList();
        var urgentItems = expiringItems.Where(i => i.Urgency == ExpiryUrgency.Urgent).ToList();
        var warningItems = expiringItems.Where(i => i.Urgency == ExpiryUrgency.Warning).ToList();

        var valueByCategory = expiringItems
            .GroupBy(i => i.Category)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.ValueAtRisk));

        var report = new ExpiryReport
        {
            GeneratedAt = now,
            SiteId = _state.State.SiteId,
            TotalItemsMonitored = _state.State.MonitoredIngredients.Count,
            ExpiredCount = expiredItems.Count,
            CriticalCount = criticalItems.Count,
            UrgentCount = urgentItems.Count,
            WarningCount = warningItems.Count,
            TotalExpiredValue = expiredItems.Sum(i => i.ValueAtRisk),
            TotalAtRiskValue = expiringItems.Sum(i => i.ValueAtRisk),
            ExpiredItems = expiredItems,
            CriticalItems = criticalItems,
            UrgentItems = urgentItems,
            WarningItems = warningItems,
            ValueAtRiskByCategory = valueByCategory
        };

        // Update cached report
        _state.State.LastScanAt = now;
        _state.State.CachedReport = new ExpiryReportCache
        {
            GeneratedAt = now,
            TotalItemsMonitored = report.TotalItemsMonitored,
            ExpiredCount = report.ExpiredCount,
            CriticalCount = report.CriticalCount,
            UrgentCount = report.UrgentCount,
            WarningCount = report.WarningCount,
            TotalExpiredValue = report.TotalExpiredValue,
            TotalAtRiskValue = report.TotalAtRiskValue
        };
        await _state.WriteStateAsync();

        // Send alerts if configured
        if (settings.SendAlerts)
        {
            await SendAlertsAsync(expiredItems, criticalItems);
        }

        _logger.LogInformation(
            "Expiry scan complete: {Expired} expired, {Critical} critical, {Urgent} urgent, {Warning} warning. Value at risk: {Value:C}",
            expiredItems.Count,
            criticalItems.Count,
            urgentItems.Count,
            warningItems.Count,
            report.TotalAtRiskValue);

        return report;
    }

    private async Task SendAlertsAsync(List<ExpiringItem> expiredItems, List<ExpiringItem> criticalItems)
    {
        foreach (var item in expiredItems.Concat(criticalItems).Take(10)) // Limit alerts
        {
            await AlertStream.OnNextAsync(new ExpiryAlertEvent(
                item.IngredientId,
                _state.State.SiteId,
                item.IngredientName,
                item.ExpiryDate,
                item.DaysUntilExpiry,
                item.Quantity,
                item.ValueAtRisk)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }
    }

    public async Task<IReadOnlyList<ExpiringItem>> GetExpiringItemsAsync(int daysAhead = 30)
    {
        EnsureExists();

        var report = await ScanForExpiringItemsAsync();
        return report.ExpiredItems
            .Concat(report.CriticalItems)
            .Concat(report.UrgentItems)
            .Concat(report.WarningItems)
            .Where(i => i.DaysUntilExpiry <= daysAhead)
            .OrderBy(i => i.DaysUntilExpiry)
            .ToList();
    }

    public async Task<IReadOnlyList<ExpiringItem>> GetExpiredItemsAsync()
    {
        EnsureExists();

        var report = await ScanForExpiringItemsAsync();
        return report.ExpiredItems;
    }

    public async Task<IReadOnlyList<ExpiringItem>> GetCriticalItemsAsync()
    {
        EnsureExists();

        var report = await ScanForExpiringItemsAsync();
        return report.CriticalItems;
    }

    public async Task<IReadOnlyList<ExpiredBatchWriteOff>> WriteOffExpiredBatchesAsync(Guid performedBy)
    {
        EnsureExists();

        var writeOffs = new List<ExpiredBatchWriteOff>();
        var now = DateTime.UtcNow;

        foreach (var ingredient in _state.State.MonitoredIngredients.Values)
        {
            var inventoryGrain = _grainFactory.GetGrain<IInventoryGrain>(
                GrainKeys.Inventory(_state.State.OrganizationId, _state.State.SiteId, ingredient.IngredientId));

            if (!await inventoryGrain.ExistsAsync())
                continue;

            var batches = await inventoryGrain.GetActiveBatchesAsync();
            var expiredBatches = batches
                .Where(b => b.ExpiryDate.HasValue && b.ExpiryDate.Value < now && b.Quantity > 0)
                .ToList();

            if (expiredBatches.Count > 0)
            {
                await inventoryGrain.WriteOffExpiredBatchesAsync(performedBy);

                foreach (var batch in expiredBatches)
                {
                    writeOffs.Add(new ExpiredBatchWriteOff
                    {
                        IngredientId = ingredient.IngredientId,
                        IngredientName = ingredient.IngredientName,
                        BatchId = batch.Id,
                        BatchNumber = batch.BatchNumber,
                        Quantity = batch.Quantity,
                        UnitCost = batch.UnitCost,
                        TotalCost = batch.Quantity * batch.UnitCost,
                        ExpiryDate = batch.ExpiryDate!.Value,
                        WrittenOffAt = now
                    });
                }
            }
        }

        _logger.LogInformation(
            "Wrote off {Count} expired batches totaling {Value:C}",
            writeOffs.Count,
            writeOffs.Sum(w => w.TotalCost));

        return writeOffs;
    }

    public async Task<ExpiryReport> GetReportAsync()
    {
        return await ScanForExpiringItemsAsync();
    }

    public async Task<Dictionary<ExpiryUrgency, decimal>> GetValueAtRiskByUrgencyAsync()
    {
        EnsureExists();

        var report = await ScanForExpiringItemsAsync();
        return new Dictionary<ExpiryUrgency, decimal>
        {
            [ExpiryUrgency.Expired] = report.ExpiredItems.Sum(i => i.ValueAtRisk),
            [ExpiryUrgency.Critical] = report.CriticalItems.Sum(i => i.ValueAtRisk),
            [ExpiryUrgency.Urgent] = report.UrgentItems.Sum(i => i.ValueAtRisk),
            [ExpiryUrgency.Warning] = report.WarningItems.Sum(i => i.ValueAtRisk)
        };
    }

    public async Task<Dictionary<string, decimal>> GetValueAtRiskByCategoryAsync()
    {
        EnsureExists();

        var report = await ScanForExpiringItemsAsync();
        return report.ValueAtRiskByCategory;
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.OrganizationId != Guid.Empty);

    private void EnsureExists()
    {
        if (_state.State.OrganizationId == Guid.Empty)
            throw new InvalidOperationException("Expiry monitor not initialized");
    }

    private static ExpiryUrgency CalculateUrgency(int daysUntilExpiry, ExpiryMonitorSettings settings)
    {
        if (daysUntilExpiry < 0)
            return ExpiryUrgency.Expired;
        if (daysUntilExpiry <= settings.CriticalDays)
            return ExpiryUrgency.Critical;
        if (daysUntilExpiry <= settings.UrgentDays)
            return ExpiryUrgency.Urgent;
        if (daysUntilExpiry <= settings.WarningDays)
            return ExpiryUrgency.Warning;
        return ExpiryUrgency.Normal;
    }
}
