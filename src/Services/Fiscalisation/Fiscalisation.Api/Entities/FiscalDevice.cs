using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Fiscalisation.Api.Entities;

/// <summary>
/// Represents a Technical Security Equipment (TSE) device for fiscal compliance.
/// Supports multiple TSE providers like Swissbit, Fiskaly, Epson, etc.
/// </summary>
public class FiscalDevice : BaseEntity, ILocationScoped
{
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }

    /// <summary>
    /// Type of TSE device: SwissbitCloud, SwissbitUSB, FiskalyCloud, Epson, Diebold
    /// </summary>
    public string DeviceType { get; set; } = string.Empty;

    public string SerialNumber { get; set; } = string.Empty;
    public string? PublicKey { get; set; }
    public DateTime? CertificateExpiryDate { get; set; }

    /// <summary>
    /// Status: Active, Inactive, Failed, CertificateExpiring
    /// </summary>
    public string Status { get; set; } = "Inactive";

    /// <summary>
    /// API endpoint for cloud TSE providers
    /// </summary>
    public string? ApiEndpoint { get; set; }

    /// <summary>
    /// Encrypted API credentials for cloud TSE
    /// </summary>
    public string? ApiCredentialsEncrypted { get; set; }

    public DateTime? LastSyncAt { get; set; }

    /// <summary>
    /// Sequential transaction counter maintained by the TSE
    /// </summary>
    public long TransactionCounter { get; set; }

    /// <summary>
    /// Sequential signature counter maintained by the TSE
    /// </summary>
    public long SignatureCounter { get; set; }

    /// <summary>
    /// Client ID registered with the TSE (required for KassenSichV)
    /// </summary>
    public string? ClientId { get; set; }

    public ICollection<FiscalTransaction> Transactions { get; set; } = new List<FiscalTransaction>();
}
