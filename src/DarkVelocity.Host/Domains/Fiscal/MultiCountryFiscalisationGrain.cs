using DarkVelocity.Host.Events;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Multi-Country Fiscalisation Grain
// Routes fiscal operations to country-specific adapters
// ============================================================================

/// <summary>
/// Site-level fiscal configuration supporting multiple countries
/// </summary>
[GenerateSerializer]
public sealed record SiteFiscalConfiguration(
    [property: Id(0)] Guid SiteId,
    [property: Id(1)] FiscalCountry Country,
    [property: Id(2)] bool Enabled,
    [property: Id(3)] string? TseDeviceId,
    [property: Id(4)] ExternalTseType? TseType,
    [property: Id(5)] Dictionary<string, string> CountrySpecificConfig);

/// <summary>
/// Command to configure site fiscalization
/// </summary>
[GenerateSerializer]
public sealed record ConfigureSiteFiscalCommand(
    [property: Id(0)] FiscalCountry Country,
    [property: Id(1)] bool Enabled,
    [property: Id(2)] string? TseDeviceId,
    [property: Id(3)] ExternalTseType? TseType,
    [property: Id(4)] Dictionary<string, string>? CountrySpecificConfig);

/// <summary>
/// Multi-country fiscalization snapshot
/// </summary>
[GenerateSerializer]
public sealed record MultiCountryFiscalSnapshot(
    [property: Id(0)] Guid OrgId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] FiscalCountry Country,
    [property: Id(3)] bool Enabled,
    [property: Id(4)] string ComplianceStandard,
    [property: Id(5)] string? TseDeviceId,
    [property: Id(6)] ExternalTseType? TseType,
    [property: Id(7)] long TransactionCount,
    [property: Id(8)] DateTime? LastTransactionAt,
    [property: Id(9)] string? LastError,
    [property: Id(10)] Dictionary<string, string> Status);

/// <summary>
/// State for multi-country fiscalization grain
/// </summary>
[GenerateSerializer]
public sealed class MultiCountryFiscalState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public SiteFiscalConfiguration? Configuration { get; set; }
    [Id(3)] public long TransactionCount { get; set; }
    [Id(4)] public DateTime? LastTransactionAt { get; set; }
    [Id(5)] public string? LastError { get; set; }
    [Id(6)] public int Version { get; set; }
}

/// <summary>
/// Interface for multi-country fiscalization grain.
/// Provides a unified interface for fiscal operations across all supported countries.
/// Key: "{orgId}:{siteId}:fiscal"
/// </summary>
public interface IMultiCountryFiscalGrain : IGrainWithStringKey
{
    /// <summary>
    /// Configure fiscalization for this site
    /// </summary>
    Task<MultiCountryFiscalSnapshot> ConfigureAsync(ConfigureSiteFiscalCommand command);

    /// <summary>
    /// Get current configuration and status
    /// </summary>
    Task<MultiCountryFiscalSnapshot> GetSnapshotAsync();

    /// <summary>
    /// Record a fiscal transaction
    /// </summary>
    Task<FiscalResult> RecordTransactionAsync(FiscalTransactionData transaction);

    /// <summary>
    /// Generate audit export in country-specific format
    /// </summary>
    Task<byte[]> GenerateAuditExportAsync(FiscalDateRange range);

    /// <summary>
    /// Validate current configuration
    /// </summary>
    Task<FiscalConfigValidationResult> ValidateConfigurationAsync();

    /// <summary>
    /// Get device/service health status
    /// </summary>
    Task<FiscalDeviceHealthStatus> GetHealthStatusAsync();

    /// <summary>
    /// Perform daily closing operations
    /// </summary>
    Task<FiscalResult> PerformDailyCloseAsync(DateTime businessDate);

    /// <summary>
    /// Get supported features for the configured country
    /// </summary>
    Task<IReadOnlyList<FiscalFeature>> GetSupportedFeaturesAsync();
}

/// <summary>
/// Multi-country fiscalization grain implementation.
/// Routes fiscal operations to the appropriate country adapter.
/// </summary>
public sealed class MultiCountryFiscalGrain : Grain, IMultiCountryFiscalGrain
{
    private readonly IPersistentState<MultiCountryFiscalState> _state;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<MultiCountryFiscalGrain> _logger;
    private IFiscalCountryAdapter? _countryAdapter;
    private IAsyncStream<IntegrationEvent>? _eventStream;

    public MultiCountryFiscalGrain(
        [PersistentState("multiCountryFiscal", "OrleansStorage")]
        IPersistentState<MultiCountryFiscalState> state,
        IGrainFactory grainFactory,
        ILogger<MultiCountryFiscalGrain> logger)
    {
        _state = state;
        _grainFactory = grainFactory;
        _logger = logger;
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

        // Initialize country adapter if configured
        if (_state.State.Configuration != null && _state.State.Configuration.Enabled)
        {
            InitializeCountryAdapter();
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<MultiCountryFiscalSnapshot> ConfigureAsync(ConfigureSiteFiscalCommand command)
    {
        _state.State.Configuration = new SiteFiscalConfiguration(
            SiteId: _state.State.SiteId,
            Country: command.Country,
            Enabled: command.Enabled,
            TseDeviceId: command.TseDeviceId,
            TseType: command.TseType,
            CountrySpecificConfig: command.CountrySpecificConfig ?? new Dictionary<string, string>());

        _state.State.Version++;
        await _state.WriteStateAsync();

        // Initialize the appropriate country adapter
        if (command.Enabled)
        {
            await InitializeCountryAdapterAsync();
        }
        else
        {
            _countryAdapter = null;
        }

        _logger.LogInformation(
            "Configured {Country} fiscalization for site {SiteId}, enabled: {Enabled}",
            command.Country, _state.State.SiteId, command.Enabled);

        // Publish configuration event
        await PublishEventAsync(new FiscalConfigurationChanged(
            TenantId: _state.State.OrgId,
            SiteId: _state.State.SiteId,
            Country: command.Country.ToString(),
            Enabled: command.Enabled,
            ComplianceStandard: GetComplianceStandard(command.Country),
            ChangedAt: DateTime.UtcNow));

        return CreateSnapshot();
    }

    public Task<MultiCountryFiscalSnapshot> GetSnapshotAsync()
    {
        return Task.FromResult(CreateSnapshot());
    }

    public async Task<FiscalResult> RecordTransactionAsync(FiscalTransactionData transaction)
    {
        if (_countryAdapter == null || _state.State.Configuration?.Enabled != true)
        {
            return new FiscalResult(
                Success: false,
                TransactionId: null,
                Signature: null,
                SignatureCounter: null,
                CertificateSerial: null,
                QrCodeData: null,
                ErrorCode: "NOT_CONFIGURED",
                ErrorMessage: "Fiscalization is not configured or disabled",
                Metadata: null);
        }

        try
        {
            var result = await _countryAdapter.RecordTransactionAsync(transaction);

            if (result.Success)
            {
                _state.State.TransactionCount++;
                _state.State.LastTransactionAt = DateTime.UtcNow;
                _state.State.LastError = null;
            }
            else
            {
                _state.State.LastError = result.ErrorMessage;
            }

            _state.State.Version++;
            await _state.WriteStateAsync();

            // Publish event
            await PublishEventAsync(new FiscalTransactionRecorded(
                TenantId: _state.State.OrgId,
                SiteId: _state.State.SiteId,
                TransactionId: transaction.TransactionId,
                Country: _state.State.Configuration.Country.ToString(),
                Success: result.Success,
                Signature: result.Signature,
                SignatureCounter: result.SignatureCounter,
                ErrorMessage: result.ErrorMessage,
                RecordedAt: DateTime.UtcNow));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record fiscal transaction for {Country}", _state.State.Configuration.Country);
            _state.State.LastError = ex.Message;
            await _state.WriteStateAsync();

            return new FiscalResult(
                Success: false,
                TransactionId: null,
                Signature: null,
                SignatureCounter: null,
                CertificateSerial: null,
                QrCodeData: null,
                ErrorCode: "RECORD_FAILED",
                ErrorMessage: ex.Message,
                Metadata: null);
        }
    }

    public async Task<byte[]> GenerateAuditExportAsync(FiscalDateRange range)
    {
        if (_countryAdapter == null)
        {
            throw new InvalidOperationException("Fiscalization is not configured");
        }

        var export = await _countryAdapter.GenerateAuditExportAsync(range);

        // Publish event
        await PublishEventAsync(new FiscalAuditExportGenerated(
            TenantId: _state.State.OrgId,
            SiteId: _state.State.SiteId,
            Country: _state.State.Configuration!.Country.ToString(),
            StartDate: range.StartDate,
            EndDate: range.EndDate,
            FileSizeBytes: export.Length,
            GeneratedAt: DateTime.UtcNow));

        return export;
    }

    public async Task<FiscalConfigValidationResult> ValidateConfigurationAsync()
    {
        if (_countryAdapter == null)
        {
            return new FiscalConfigValidationResult(
                IsValid: false,
                Errors: new List<string> { "Fiscalization is not configured" },
                Warnings: new List<string>(),
                Metadata: null);
        }

        return await _countryAdapter.ValidateConfigurationAsync();
    }

    public async Task<FiscalDeviceHealthStatus> GetHealthStatusAsync()
    {
        if (_countryAdapter == null)
        {
            return new FiscalDeviceHealthStatus(
                DeviceId: Guid.Empty,
                Status: FiscalDeviceStatus.Inactive,
                IsOnline: false,
                CertificateValid: false,
                DaysUntilCertificateExpiry: null,
                LastSyncAt: null,
                LastTransactionAt: null,
                TotalTransactions: 0,
                LastError: "Not configured");
        }

        return await _countryAdapter.GetHealthStatusAsync();
    }

    public async Task<FiscalResult> PerformDailyCloseAsync(DateTime businessDate)
    {
        if (_countryAdapter == null)
        {
            return new FiscalResult(
                Success: false,
                TransactionId: null,
                Signature: null,
                SignatureCounter: null,
                CertificateSerial: null,
                QrCodeData: null,
                ErrorCode: "NOT_CONFIGURED",
                ErrorMessage: "Fiscalization is not configured",
                Metadata: null);
        }

        var result = await _countryAdapter.PerformDailyCloseAsync(businessDate);

        if (result.Success)
        {
            // Publish event
            await PublishEventAsync(new FiscalDailyClosePerformed(
                TenantId: _state.State.OrgId,
                SiteId: _state.State.SiteId,
                Country: _state.State.Configuration!.Country.ToString(),
                BusinessDate: businessDate,
                TransactionCount: _state.State.TransactionCount,
                PerformedAt: DateTime.UtcNow));
        }

        return result;
    }

    public Task<IReadOnlyList<FiscalFeature>> GetSupportedFeaturesAsync()
    {
        if (_countryAdapter == null)
        {
            return Task.FromResult<IReadOnlyList<FiscalFeature>>(Array.Empty<FiscalFeature>());
        }

        var features = Enum.GetValues<FiscalFeature>()
            .Where(f => _countryAdapter.SupportsFeature(f))
            .ToList();

        return Task.FromResult<IReadOnlyList<FiscalFeature>>(features);
    }

    // ========================================================================
    // Private methods
    // ========================================================================

    private void InitializeCountryAdapter()
    {
        // Create country-specific adapter based on configuration
        // This is called on activation and uses existing state
        _logger.LogDebug(
            "Initializing {Country} adapter for site {SiteId}",
            _state.State.Configuration?.Country, _state.State.SiteId);
    }

    private async Task InitializeCountryAdapterAsync()
    {
        if (_state.State.Configuration == null)
            return;

        var country = _state.State.Configuration.Country;
        var config = _state.State.Configuration.CountrySpecificConfig;

        _logger.LogInformation(
            "Initializing {Country} fiscal adapter for site {SiteId}",
            country, _state.State.SiteId);

        // Country-specific adapter initialization would happen here
        // In a full implementation, this would create the appropriate adapter
        // based on the country and configure it with country-specific settings
    }

    private MultiCountryFiscalSnapshot CreateSnapshot()
    {
        var country = _state.State.Configuration?.Country ?? FiscalCountry.Germany;
        var status = new Dictionary<string, string>
        {
            ["adapter_initialized"] = (_countryAdapter != null).ToString(),
            ["transaction_count"] = _state.State.TransactionCount.ToString()
        };

        if (_state.State.LastError != null)
        {
            status["last_error"] = _state.State.LastError;
        }

        return new MultiCountryFiscalSnapshot(
            OrgId: _state.State.OrgId,
            SiteId: _state.State.SiteId,
            Country: country,
            Enabled: _state.State.Configuration?.Enabled ?? false,
            ComplianceStandard: GetComplianceStandard(country),
            TseDeviceId: _state.State.Configuration?.TseDeviceId,
            TseType: _state.State.Configuration?.TseType,
            TransactionCount: _state.State.TransactionCount,
            LastTransactionAt: _state.State.LastTransactionAt,
            LastError: _state.State.LastError,
            Status: status);
    }

    private static string GetComplianceStandard(FiscalCountry country)
    {
        return country switch
        {
            FiscalCountry.Germany => "KassenSichV",
            FiscalCountry.Austria => "RKSV",
            FiscalCountry.Italy => "RT",
            FiscalCountry.France => "NF 525",
            FiscalCountry.Poland => "JPK/KSeF",
            _ => "Unknown"
        };
    }

    private async Task PublishEventAsync(IntegrationEvent evt)
    {
        if (_eventStream == null)
        {
            try
            {
                var streamProvider = this.GetStreamProvider("Default");
                _eventStream = streamProvider.GetStream<IntegrationEvent>(
                    StreamId.Create("fiscal-events", _state.State.OrgId.ToString()));
            }
            catch
            {
                return;
            }
        }

        try
        {
            await _eventStream.OnNextAsync(evt);
        }
        catch
        {
            // Stream may not be configured
        }
    }
}

// ============================================================================
// Multi-Country Fiscal Events
// ============================================================================

/// <summary>
/// Event when fiscal configuration changes for a site
/// </summary>
public sealed record FiscalConfigurationChanged(
    Guid TenantId,
    Guid SiteId,
    string Country,
    bool Enabled,
    string ComplianceStandard,
    DateTime ChangedAt
) : IntegrationEvent
{
    public override string EventType => "fiscal.configuration.changed";
}

/// <summary>
/// Event when a fiscal transaction is recorded
/// </summary>
public sealed record FiscalTransactionRecorded(
    Guid TenantId,
    Guid SiteId,
    Guid TransactionId,
    string Country,
    bool Success,
    string? Signature,
    long? SignatureCounter,
    string? ErrorMessage,
    DateTime RecordedAt
) : IntegrationEvent
{
    public override string EventType => "fiscal.transaction.recorded";
}

/// <summary>
/// Event when audit export is generated
/// </summary>
public sealed record FiscalAuditExportGenerated(
    Guid TenantId,
    Guid SiteId,
    string Country,
    DateTime StartDate,
    DateTime EndDate,
    long FileSizeBytes,
    DateTime GeneratedAt
) : IntegrationEvent
{
    public override string EventType => "fiscal.audit_export.generated";
}

/// <summary>
/// Event when daily close is performed
/// </summary>
public sealed record FiscalDailyClosePerformed(
    Guid TenantId,
    Guid SiteId,
    string Country,
    DateTime BusinessDate,
    long TransactionCount,
    DateTime PerformedAt
) : IntegrationEvent
{
    public override string EventType => "fiscal.daily_close.performed";
}

// ============================================================================
// Country Adapter Factory Implementation
// ============================================================================

/// <summary>
/// Implementation of fiscal country adapter factory
/// </summary>
public sealed class FiscalCountryAdapterFactory : IFiscalCountryAdapterFactory
{
    private readonly IGrainFactory _grainFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;

    private static readonly HashSet<FiscalCountry> SupportedCountries = new()
    {
        FiscalCountry.Germany,
        FiscalCountry.Austria,
        FiscalCountry.Italy,
        FiscalCountry.France,
        FiscalCountry.Poland
    };

    public FiscalCountryAdapterFactory(
        IGrainFactory grainFactory,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory)
    {
        _grainFactory = grainFactory;
        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
    }

    public IFiscalCountryAdapter CreateAdapter(FiscalCountry country, Guid orgId, Guid siteId)
    {
        // In a full implementation, this would create the appropriate adapter
        // based on the country and inject required dependencies
        throw new NotImplementedException(
            $"Adapter creation for {country} should be done through grain activation");
    }

    public bool IsCountrySupported(FiscalCountry country)
    {
        return SupportedCountries.Contains(country);
    }

    public IReadOnlyList<FiscalCountry> GetSupportedCountries()
    {
        return SupportedCountries.ToList();
    }
}

// ============================================================================
// Grain Keys Extension
// ============================================================================

public static partial class GrainKeys
{
    /// <summary>
    /// Creates a key for the multi-country fiscal grain
    /// </summary>
    public static string MultiCountryFiscal(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:fiscal";

    /// <summary>
    /// Creates a key for the French fiscal grain
    /// </summary>
    public static string FrenchFiscal(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:fiscal:france";

    /// <summary>
    /// Creates a key for the Polish fiscal grain
    /// </summary>
    public static string PolishFiscal(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:fiscal:poland";
}
