namespace DarkVelocity.Host.Grains;

// ============================================================================
// Fiscal API Request/Response Contracts
// DTOs for multi-country fiscal compliance API endpoints
// ============================================================================

/// <summary>
/// Request to configure site fiscal settings
/// </summary>
public sealed record ConfigureSiteFiscalRequest(
    string Country,
    bool Enabled,
    string? TseDeviceId,
    string? TseType,
    Dictionary<string, string>? CountrySpecificConfig);

/// <summary>
/// Request to generate fiscal export
/// </summary>
public sealed record GenerateFiscalExportRequest(
    DateTime StartDate,
    DateTime EndDate,
    string? Format);

/// <summary>
/// Request to generate Z-report
/// </summary>
public sealed record GenerateZReportRequest(
    DateOnly BusinessDate);

/// <summary>
/// Request to trigger daily close
/// </summary>
public sealed record TriggerDailyCloseRequest(
    DateTime BusinessDate);

/// <summary>
/// Request to configure fiscal jobs for a site
/// </summary>
public sealed record ConfigureFiscalJobsRequest(
    bool DailyCloseEnabled,
    TimeOnly DailyCloseTime,
    bool ArchiveEnabled,
    TimeOnly ArchiveTime,
    bool CertificateMonitoringEnabled,
    int CertificateExpiryWarningDays,
    string TimeZoneId);

/// <summary>
/// Request to generate Polish JPK export
/// </summary>
public sealed record GenerateJpkExportRequest(
    DateTime StartDate,
    DateTime EndDate,
    string? DocumentType);

/// <summary>
/// Response for supported countries
/// </summary>
public sealed record SupportedCountriesResponse(
    IReadOnlyList<string> Countries);

/// <summary>
/// Response for fiscal compliance status
/// </summary>
public sealed record FiscalComplianceStatusResponse(
    Guid SiteId,
    string Country,
    string ComplianceStandard,
    bool Enabled,
    bool Configured,
    bool Healthy,
    string? LastError,
    DateTime? LastTransactionAt,
    long TransactionCount);

/// <summary>
/// Response for Z-report list
/// </summary>
public sealed record ZReportListResponse(
    IReadOnlyList<ZReportEntry> Reports,
    int Total);

/// <summary>
/// Response for fiscal job history
/// </summary>
public sealed record FiscalJobHistoryResponse(
    IReadOnlyList<FiscalJobHistoryEntry> History,
    int Total);

/// <summary>
/// Response for certificate expiry warnings
/// </summary>
public sealed record CertificateExpiryWarningsResponse(
    IReadOnlyList<CertificateExpiryWarning> Warnings,
    int Total);
