using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Fiscalisation.Api.Entities;

/// <summary>
/// Immutable audit log for all fiscal operations.
/// Required for KassenSichV compliance and audit trail.
/// </summary>
public class FiscalJournal : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid? LocationId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Event type: TransactionSigned, DeviceRegistered, DeviceDecommissioned,
    /// ExportGenerated, Error, DeviceStatusChanged, SelfTestPerformed
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    public Guid? DeviceId { get; set; }
    public Guid? TransactionId { get; set; }
    public Guid? ExportId { get; set; }

    /// <summary>
    /// JSON details of the event
    /// </summary>
    public string Details { get; set; } = "{}";

    /// <summary>
    /// IP address of the request origin
    /// </summary>
    public string? IpAddress { get; set; }

    public Guid? UserId { get; set; }

    /// <summary>
    /// Severity level: Info, Warning, Error
    /// </summary>
    public string Severity { get; set; } = "Info";
}
