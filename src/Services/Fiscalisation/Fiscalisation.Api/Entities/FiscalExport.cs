using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Fiscalisation.Api.Entities;

/// <summary>
/// Represents a DSFinV-K export for German tax authority compliance.
/// Contains all required transaction data in the mandated format.
/// </summary>
public class FiscalExport : BaseEntity, ILocationScoped
{
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }

    /// <summary>
    /// Export type: Daily, Monthly, OnDemand, AuditRequest
    /// </summary>
    public string ExportType { get; set; } = "OnDemand";

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Status: Generating, Completed, Failed
    /// </summary>
    public string Status { get; set; } = "Generating";

    /// <summary>
    /// URL/path to the generated export file
    /// </summary>
    public string? FileUrl { get; set; }

    /// <summary>
    /// SHA256 hash of the export file for integrity verification
    /// </summary>
    public string? FileSha256 { get; set; }

    /// <summary>
    /// Total number of transactions included in the export
    /// </summary>
    public int TransactionCount { get; set; }

    public DateTime? GeneratedAt { get; set; }

    public Guid RequestedByUserId { get; set; }

    /// <summary>
    /// Reference number for tax authority audit requests
    /// </summary>
    public string? AuditReference { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long? FileSizeBytes { get; set; }
}
