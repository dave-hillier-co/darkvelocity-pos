namespace DarkVelocity.Host.Events;

/// <summary>
/// Published when a transaction is successfully signed by a TSE device
/// </summary>
public sealed record TransactionSigned(
    Guid TransactionId,
    Guid DeviceId,
    Guid LocationId,
    Guid TenantId,
    long TransactionNumber,
    long SignatureCounter,
    string? Signature,
    decimal GrossAmount,
    DateTime SignedAt
) : IntegrationEvent
{
    public override string EventType => "fiscalisation.transaction.signed";
}

/// <summary>
/// Published when transaction signing fails
/// </summary>
public sealed record TransactionSigningFailed(
    Guid TransactionId,
    Guid DeviceId,
    Guid LocationId,
    Guid TenantId,
    string? ErrorCode,
    string? ErrorMessage,
    bool WillRetry
) : IntegrationEvent
{
    public override string EventType => "fiscalisation.transaction.signing_failed";
}

/// <summary>
/// Published when a fiscal device status changes
/// </summary>
public sealed record FiscalDeviceHealthChanged(
    Guid DeviceId,
    Guid LocationId,
    Guid TenantId,
    string OldStatus,
    string NewStatus,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "fiscalisation.device.health_changed";
}

/// <summary>
/// Published when a DSFinV-K export is generated
/// </summary>
public sealed record ExportGenerated(
    Guid ExportId,
    Guid LocationId,
    Guid TenantId,
    DateTime StartDate,
    DateTime EndDate,
    int TransactionCount,
    string? FileUrl
) : IntegrationEvent
{
    public override string EventType => "fiscalisation.export.generated";
}

/// <summary>
/// Published when a new fiscal device is registered
/// </summary>
public sealed record FiscalDeviceRegistered(
    Guid DeviceId,
    Guid LocationId,
    Guid TenantId,
    string DeviceType,
    string SerialNumber
) : IntegrationEvent
{
    public override string EventType => "fiscalisation.device.registered";
}

/// <summary>
/// Published when a fiscal device is decommissioned
/// </summary>
public sealed record FiscalDeviceDecommissioned(
    Guid DeviceId,
    Guid LocationId,
    Guid TenantId,
    string Reason
) : IntegrationEvent
{
    public override string EventType => "fiscalisation.device.decommissioned";
}
