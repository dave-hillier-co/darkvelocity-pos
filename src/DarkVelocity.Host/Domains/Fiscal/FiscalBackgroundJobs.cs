using DarkVelocity.Host.Events;
using Orleans.Runtime;
using Orleans.Timers;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Fiscal Background Jobs
// Scheduled operations for fiscal compliance including:
// - Daily Z-report generation
// - Certificate expiry monitoring
// - Archive generation
// - KSeF invoice batch submission
// ============================================================================

/// <summary>
/// State for the fiscal job scheduler grain
/// </summary>
[GenerateSerializer]
public sealed class FiscalJobSchedulerState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Dictionary<Guid, SiteFiscalJobConfig> SiteConfigs { get; set; } = [];
    [Id(2)] public Dictionary<string, DateTime> LastJobRuns { get; set; } = [];
    [Id(3)] public List<FiscalJobHistoryEntry> JobHistory { get; set; } = [];
    [Id(4)] public int Version { get; set; }
}

/// <summary>
/// Per-site fiscal job configuration
/// </summary>
[GenerateSerializer]
public sealed record SiteFiscalJobConfig(
    [property: Id(0)] Guid SiteId,
    [property: Id(1)] bool DailyCloseEnabled,
    [property: Id(2)] TimeOnly DailyCloseTime,
    [property: Id(3)] bool ArchiveEnabled,
    [property: Id(4)] TimeOnly ArchiveTime,
    [property: Id(5)] bool CertificateMonitoringEnabled,
    [property: Id(6)] int CertificateExpiryWarningDays,
    [property: Id(7)] string TimeZoneId);

/// <summary>
/// Fiscal job history entry
/// </summary>
[GenerateSerializer]
public sealed record FiscalJobHistoryEntry(
    [property: Id(0)] Guid JobId,
    [property: Id(1)] string JobType,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] DateTime StartedAt,
    [property: Id(4)] DateTime? CompletedAt,
    [property: Id(5)] bool Success,
    [property: Id(6)] string? ErrorMessage,
    [property: Id(7)] Dictionary<string, string>? Metadata);

/// <summary>
/// Interface for fiscal job scheduler grain.
/// Manages scheduled fiscal operations for all sites in an organization.
/// Key: "{orgId}:fiscaljobs"
/// </summary>
public interface IFiscalJobSchedulerGrain : IGrainWithStringKey
{
    /// <summary>
    /// Configure jobs for a site
    /// </summary>
    Task ConfigureSiteJobsAsync(SiteFiscalJobConfig config);

    /// <summary>
    /// Remove job configuration for a site
    /// </summary>
    Task RemoveSiteJobsAsync(Guid siteId);

    /// <summary>
    /// Get job configuration for all sites
    /// </summary>
    Task<IReadOnlyList<SiteFiscalJobConfig>> GetSiteConfigsAsync();

    /// <summary>
    /// Get job history
    /// </summary>
    Task<IReadOnlyList<FiscalJobHistoryEntry>> GetJobHistoryAsync(int limit = 100);

    /// <summary>
    /// Manually trigger daily close for a site
    /// </summary>
    Task<FiscalJobHistoryEntry> TriggerDailyCloseAsync(Guid siteId, DateTime businessDate);

    /// <summary>
    /// Manually trigger archive generation for a site
    /// </summary>
    Task<FiscalJobHistoryEntry> TriggerArchiveGenerationAsync(Guid siteId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Check certificate expiry for all sites
    /// </summary>
    Task<IReadOnlyList<CertificateExpiryWarning>> CheckCertificateExpiryAsync();
}

/// <summary>
/// Certificate expiry warning
/// </summary>
[GenerateSerializer]
public sealed record CertificateExpiryWarning(
    [property: Id(0)] Guid SiteId,
    [property: Id(1)] string DeviceSerial,
    [property: Id(2)] DateTime ExpiryDate,
    [property: Id(3)] int DaysUntilExpiry,
    [property: Id(4)] string Severity);

/// <summary>
/// Fiscal job scheduler grain implementation
/// </summary>
public sealed class FiscalJobSchedulerGrain : Grain, IFiscalJobSchedulerGrain, IRemindable
{
    private readonly IPersistentState<FiscalJobSchedulerState> _state;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<FiscalJobSchedulerGrain> _logger;
    private readonly TimeProvider _timeProvider;

    private const string DailyJobsReminder = "daily-fiscal-jobs";
    private const string HourlyJobsReminder = "hourly-fiscal-jobs";
    private const int MaxHistoryEntries = 1000;

    public FiscalJobSchedulerGrain(
        [PersistentState("fiscalJobScheduler", "OrleansStorage")]
        IPersistentState<FiscalJobSchedulerState> state,
        IGrainFactory grainFactory,
        ILogger<FiscalJobSchedulerGrain> logger,
        TimeProvider? timeProvider = null)
    {
        _state = state;
        _grainFactory = grainFactory;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');

        if (_state.State.OrgId == Guid.Empty)
        {
            _state.State.OrgId = Guid.Parse(parts[0]);
        }

        // Register reminders for scheduled jobs
        await this.RegisterOrUpdateReminder(
            DailyJobsReminder,
            TimeSpan.FromMinutes(1), // Initial delay
            TimeSpan.FromHours(1));  // Check every hour for daily jobs

        await this.RegisterOrUpdateReminder(
            HourlyJobsReminder,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15)); // Check every 15 minutes for certificate monitoring

        await base.OnActivateAsync(cancellationToken);
    }

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        _logger.LogDebug("Fiscal job reminder triggered: {ReminderName}", reminderName);

        try
        {
            switch (reminderName)
            {
                case DailyJobsReminder:
                    await ProcessDailyJobsAsync();
                    break;

                case HourlyJobsReminder:
                    await ProcessHourlyJobsAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing fiscal job reminder: {ReminderName}", reminderName);
        }
    }

    public async Task ConfigureSiteJobsAsync(SiteFiscalJobConfig config)
    {
        _state.State.SiteConfigs[config.SiteId] = config;
        _state.State.Version++;
        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Configured fiscal jobs for site {SiteId}: DailyClose={DailyCloseEnabled}, Archive={ArchiveEnabled}",
            config.SiteId, config.DailyCloseEnabled, config.ArchiveEnabled);
    }

    public async Task RemoveSiteJobsAsync(Guid siteId)
    {
        _state.State.SiteConfigs.Remove(siteId);
        _state.State.Version++;
        await _state.WriteStateAsync();

        _logger.LogInformation("Removed fiscal jobs for site {SiteId}", siteId);
    }

    public Task<IReadOnlyList<SiteFiscalJobConfig>> GetSiteConfigsAsync()
    {
        return Task.FromResult<IReadOnlyList<SiteFiscalJobConfig>>(
            _state.State.SiteConfigs.Values.ToList());
    }

    public Task<IReadOnlyList<FiscalJobHistoryEntry>> GetJobHistoryAsync(int limit = 100)
    {
        var history = _state.State.JobHistory
            .OrderByDescending(h => h.StartedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<FiscalJobHistoryEntry>>(history);
    }

    public async Task<FiscalJobHistoryEntry> TriggerDailyCloseAsync(Guid siteId, DateTime businessDate)
    {
        var jobId = Guid.NewGuid();
        var entry = new FiscalJobHistoryEntry(
            JobId: jobId,
            JobType: "DailyClose",
            SiteId: siteId,
            StartedAt: _timeProvider.GetUtcNow().DateTime,
            CompletedAt: null,
            Success: false,
            ErrorMessage: null,
            Metadata: new Dictionary<string, string>
            {
                ["business_date"] = businessDate.ToString("yyyy-MM-dd"),
                ["triggered_by"] = "manual"
            });

        try
        {
            var fiscalGrain = _grainFactory.GetGrain<IMultiCountryFiscalGrain>(
                GrainKeys.MultiCountryFiscal(_state.State.OrgId, siteId));

            var result = await fiscalGrain.PerformDailyCloseAsync(businessDate);

            entry = entry with
            {
                CompletedAt = _timeProvider.GetUtcNow().DateTime,
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                Metadata = entry.Metadata != null
                    ? new Dictionary<string, string>(entry.Metadata)
                    {
                        ["result_transaction_id"] = result.TransactionId ?? ""
                    }
                    : null
            };

            _logger.LogInformation(
                "Daily close completed for site {SiteId}, business date {BusinessDate}, success: {Success}",
                siteId, businessDate.ToString("yyyy-MM-dd"), result.Success);
        }
        catch (Exception ex)
        {
            entry = entry with
            {
                CompletedAt = _timeProvider.GetUtcNow().DateTime,
                Success = false,
                ErrorMessage = ex.Message
            };

            _logger.LogError(ex, "Daily close failed for site {SiteId}", siteId);
        }

        await RecordJobHistoryAsync(entry);
        return entry;
    }

    public async Task<FiscalJobHistoryEntry> TriggerArchiveGenerationAsync(
        Guid siteId, DateTime startDate, DateTime endDate)
    {
        var jobId = Guid.NewGuid();
        var entry = new FiscalJobHistoryEntry(
            JobId: jobId,
            JobType: "ArchiveGeneration",
            SiteId: siteId,
            StartedAt: _timeProvider.GetUtcNow().DateTime,
            CompletedAt: null,
            Success: false,
            ErrorMessage: null,
            Metadata: new Dictionary<string, string>
            {
                ["start_date"] = startDate.ToString("yyyy-MM-dd"),
                ["end_date"] = endDate.ToString("yyyy-MM-dd"),
                ["triggered_by"] = "manual"
            });

        try
        {
            var fiscalGrain = _grainFactory.GetGrain<IMultiCountryFiscalGrain>(
                GrainKeys.MultiCountryFiscal(_state.State.OrgId, siteId));

            var archive = await fiscalGrain.GenerateAuditExportAsync(
                new FiscalDateRange(startDate, endDate));

            entry = entry with
            {
                CompletedAt = _timeProvider.GetUtcNow().DateTime,
                Success = true,
                Metadata = entry.Metadata != null
                    ? new Dictionary<string, string>(entry.Metadata)
                    {
                        ["archive_size_bytes"] = archive.Length.ToString()
                    }
                    : null
            };

            _logger.LogInformation(
                "Archive generation completed for site {SiteId}, {StartDate} to {EndDate}, size: {Size} bytes",
                siteId, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"), archive.Length);
        }
        catch (Exception ex)
        {
            entry = entry with
            {
                CompletedAt = _timeProvider.GetUtcNow().DateTime,
                Success = false,
                ErrorMessage = ex.Message
            };

            _logger.LogError(ex, "Archive generation failed for site {SiteId}", siteId);
        }

        await RecordJobHistoryAsync(entry);
        return entry;
    }

    public async Task<IReadOnlyList<CertificateExpiryWarning>> CheckCertificateExpiryAsync()
    {
        var warnings = new List<CertificateExpiryWarning>();

        foreach (var (siteId, config) in _state.State.SiteConfigs)
        {
            if (!config.CertificateMonitoringEnabled)
                continue;

            try
            {
                var fiscalGrain = _grainFactory.GetGrain<IMultiCountryFiscalGrain>(
                    GrainKeys.MultiCountryFiscal(_state.State.OrgId, siteId));

                var health = await fiscalGrain.GetHealthStatusAsync();

                if (health.DaysUntilCertificateExpiry.HasValue)
                {
                    var days = health.DaysUntilCertificateExpiry.Value;
                    var severity = days switch
                    {
                        <= 7 => "Critical",
                        <= 30 => "Warning",
                        <= 60 => "Info",
                        _ => null
                    };

                    if (severity != null && days <= config.CertificateExpiryWarningDays)
                    {
                        warnings.Add(new CertificateExpiryWarning(
                            SiteId: siteId,
                            DeviceSerial: health.DeviceId.ToString(),
                            ExpiryDate: _timeProvider.GetUtcNow().DateTime.AddDays(days),
                            DaysUntilExpiry: days,
                            Severity: severity));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check certificate expiry for site {SiteId}", siteId);
            }
        }

        return warnings;
    }

    // ========================================================================
    // Private methods
    // ========================================================================

    private async Task ProcessDailyJobsAsync()
    {
        var now = _timeProvider.GetUtcNow().DateTime;

        foreach (var (siteId, config) in _state.State.SiteConfigs)
        {
            try
            {
                // Convert to site's timezone
                var tz = TimeZoneInfo.FindSystemTimeZoneById(config.TimeZoneId);
                var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, tz);
                var localTime = TimeOnly.FromDateTime(localNow);

                // Check if it's time for daily close
                if (config.DailyCloseEnabled && IsTimeForJob(localTime, config.DailyCloseTime))
                {
                    var lastRunKey = $"daily-close:{siteId}";
                    var businessDate = localNow.Date.AddDays(-1); // Close previous day

                    if (!WasJobRunToday(lastRunKey, businessDate))
                    {
                        _logger.LogInformation(
                            "Starting scheduled daily close for site {SiteId}",
                            siteId);

                        await TriggerDailyCloseAsync(siteId, businessDate);
                        await RecordLastRunAsync(lastRunKey, businessDate);
                    }
                }

                // Check if it's time for archive generation
                if (config.ArchiveEnabled && IsTimeForJob(localTime, config.ArchiveTime))
                {
                    var lastRunKey = $"archive:{siteId}";
                    var archiveDate = localNow.Date.AddDays(-1);

                    if (!WasJobRunToday(lastRunKey, archiveDate))
                    {
                        _logger.LogInformation(
                            "Starting scheduled archive generation for site {SiteId}",
                            siteId);

                        await TriggerArchiveGenerationAsync(siteId, archiveDate, archiveDate.AddDays(1).AddTicks(-1));
                        await RecordLastRunAsync(lastRunKey, archiveDate);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing daily jobs for site {SiteId}", siteId);
            }
        }
    }

    private async Task ProcessHourlyJobsAsync()
    {
        // Check certificate expiry
        var warnings = await CheckCertificateExpiryAsync();

        foreach (var warning in warnings.Where(w => w.Severity == "Critical"))
        {
            _logger.LogWarning(
                "CRITICAL: Certificate expiring in {Days} days for site {SiteId}",
                warning.DaysUntilExpiry, warning.SiteId);

            // In production, this would send alerts via notification system
        }
    }

    private bool IsTimeForJob(TimeOnly currentTime, TimeOnly scheduledTime)
    {
        // Allow a 30-minute window for job execution
        var diff = Math.Abs((currentTime.ToTimeSpan() - scheduledTime.ToTimeSpan()).TotalMinutes);
        return diff <= 30;
    }

    private bool WasJobRunToday(string jobKey, DateTime forDate)
    {
        if (_state.State.LastJobRuns.TryGetValue(jobKey, out var lastRun))
        {
            return lastRun.Date == forDate.Date;
        }
        return false;
    }

    private async Task RecordLastRunAsync(string jobKey, DateTime runDate)
    {
        _state.State.LastJobRuns[jobKey] = runDate;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private async Task RecordJobHistoryAsync(FiscalJobHistoryEntry entry)
    {
        _state.State.JobHistory.Add(entry);

        // Trim history to max entries
        if (_state.State.JobHistory.Count > MaxHistoryEntries)
        {
            _state.State.JobHistory = _state.State.JobHistory
                .OrderByDescending(h => h.StartedAt)
                .Take(MaxHistoryEntries)
                .ToList();
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }
}

// ============================================================================
// Z-Report Generation Grain
// ============================================================================

/// <summary>
/// State for Z-report generation grain
/// </summary>
[GenerateSerializer]
public sealed class ZReportState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public long ReportNumber { get; set; }
    [Id(3)] public List<ZReportEntry> Reports { get; set; } = [];
    [Id(4)] public int Version { get; set; }
}

/// <summary>
/// Z-report entry
/// </summary>
[GenerateSerializer]
public sealed record ZReportEntry(
    [property: Id(0)] long ReportNumber,
    [property: Id(1)] DateOnly BusinessDate,
    [property: Id(2)] DateTime GeneratedAt,
    [property: Id(3)] decimal GrossSales,
    [property: Id(4)] decimal NetSales,
    [property: Id(5)] decimal TotalTax,
    [property: Id(6)] int TransactionCount,
    [property: Id(7)] Dictionary<string, decimal> SalesByVatRate,
    [property: Id(8)] Dictionary<string, decimal> SalesByPaymentType,
    [property: Id(9)] int VoidCount,
    [property: Id(10)] decimal VoidTotal,
    [property: Id(11)] string? Signature,
    [property: Id(12)] string? DeviceSerial);

/// <summary>
/// Interface for Z-report grain.
/// Generates end-of-day Z-reports for fiscal compliance.
/// Key: "{orgId}:{siteId}:zreport"
/// </summary>
public interface IZReportGrain : IGrainWithStringKey
{
    /// <summary>
    /// Generate Z-report for a business date
    /// </summary>
    Task<ZReportEntry> GenerateReportAsync(DateOnly businessDate);

    /// <summary>
    /// Get report by number
    /// </summary>
    Task<ZReportEntry?> GetReportAsync(long reportNumber);

    /// <summary>
    /// Get reports for a date range
    /// </summary>
    Task<IReadOnlyList<ZReportEntry>> GetReportsAsync(DateOnly startDate, DateOnly endDate);

    /// <summary>
    /// Get the latest report
    /// </summary>
    Task<ZReportEntry?> GetLatestReportAsync();
}

/// <summary>
/// Z-report grain implementation
/// </summary>
public sealed class ZReportGrain : Grain, IZReportGrain
{
    private readonly IPersistentState<ZReportState> _state;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ZReportGrain> _logger;
    private readonly TimeProvider _timeProvider;

    public ZReportGrain(
        [PersistentState("zReport", "OrleansStorage")]
        IPersistentState<ZReportState> state,
        IGrainFactory grainFactory,
        ILogger<ZReportGrain> logger,
        TimeProvider? timeProvider = null)
    {
        _state = state;
        _grainFactory = grainFactory;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');

        if (_state.State.OrgId == Guid.Empty)
        {
            _state.State.OrgId = Guid.Parse(parts[0]);
            _state.State.SiteId = Guid.Parse(parts[1]);
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<ZReportEntry> GenerateReportAsync(DateOnly businessDate)
    {
        // Check if report already exists for this date
        var existingReport = _state.State.Reports.FirstOrDefault(r => r.BusinessDate == businessDate);
        if (existingReport != null)
        {
            throw new InvalidOperationException($"Z-report already exists for {businessDate}");
        }

        // Get fiscal data for the day
        var fiscalGrain = _grainFactory.GetGrain<IMultiCountryFiscalGrain>(
            GrainKeys.MultiCountryFiscal(_state.State.OrgId, _state.State.SiteId));

        // Get transactions for the day from fiscal registry
        var registryGrain = _grainFactory.GetGrain<IFiscalTransactionRegistryGrain>(
            GrainKeys.FiscalTransactionRegistry(_state.State.OrgId, _state.State.SiteId));

        var transactionIds = await registryGrain.GetTransactionIdsAsync(businessDate, businessDate, null);

        // Aggregate transaction data
        decimal grossSales = 0;
        decimal netSales = 0;
        decimal totalTax = 0;
        int transactionCount = 0;
        int voidCount = 0;
        decimal voidTotal = 0;
        var salesByVatRate = new Dictionary<string, decimal>();
        var salesByPaymentType = new Dictionary<string, decimal>();

        foreach (var txId in transactionIds)
        {
            try
            {
                var txGrain = _grainFactory.GetGrain<IFiscalTransactionGrain>(
                    GrainKeys.FiscalTransaction(_state.State.OrgId, _state.State.SiteId, txId));

                var tx = await txGrain.GetSnapshotAsync();

                if (tx.TransactionType == FiscalTransactionType.Void)
                {
                    voidCount++;
                    voidTotal += Math.Abs(tx.GrossAmount);
                }
                else
                {
                    grossSales += tx.GrossAmount;
                    transactionCount++;

                    // Aggregate by VAT rate
                    foreach (var (rate, amount) in tx.TaxAmounts)
                    {
                        totalTax += amount;
                        if (salesByVatRate.ContainsKey(rate))
                            salesByVatRate[rate] += tx.NetAmounts.GetValueOrDefault(rate, 0);
                        else
                            salesByVatRate[rate] = tx.NetAmounts.GetValueOrDefault(rate, 0);
                    }

                    // Aggregate by payment type
                    foreach (var (type, amount) in tx.PaymentTypes)
                    {
                        if (salesByPaymentType.ContainsKey(type))
                            salesByPaymentType[type] += amount;
                        else
                            salesByPaymentType[type] = amount;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process transaction {TxId} for Z-report", txId);
            }
        }

        netSales = grossSales - totalTax;

        // Increment report number
        _state.State.ReportNumber++;
        var reportNumber = _state.State.ReportNumber;

        // Get device info for signature
        var health = await fiscalGrain.GetHealthStatusAsync();

        var report = new ZReportEntry(
            ReportNumber: reportNumber,
            BusinessDate: businessDate,
            GeneratedAt: _timeProvider.GetUtcNow().DateTime,
            GrossSales: grossSales,
            NetSales: netSales,
            TotalTax: totalTax,
            TransactionCount: transactionCount,
            SalesByVatRate: salesByVatRate,
            SalesByPaymentType: salesByPaymentType,
            VoidCount: voidCount,
            VoidTotal: voidTotal,
            Signature: null, // Would be signed by TSE in production
            DeviceSerial: health.DeviceId.ToString());

        _state.State.Reports.Add(report);
        _state.State.Version++;
        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Generated Z-report #{ReportNumber} for {BusinessDate}: {TransactionCount} transactions, {GrossSales:C} gross",
            reportNumber, businessDate, transactionCount, grossSales);

        return report;
    }

    public Task<ZReportEntry?> GetReportAsync(long reportNumber)
    {
        var report = _state.State.Reports.FirstOrDefault(r => r.ReportNumber == reportNumber);
        return Task.FromResult(report);
    }

    public Task<IReadOnlyList<ZReportEntry>> GetReportsAsync(DateOnly startDate, DateOnly endDate)
    {
        var reports = _state.State.Reports
            .Where(r => r.BusinessDate >= startDate && r.BusinessDate <= endDate)
            .OrderBy(r => r.BusinessDate)
            .ToList();

        return Task.FromResult<IReadOnlyList<ZReportEntry>>(reports);
    }

    public Task<ZReportEntry?> GetLatestReportAsync()
    {
        var report = _state.State.Reports
            .OrderByDescending(r => r.ReportNumber)
            .FirstOrDefault();

        return Task.FromResult(report);
    }
}

