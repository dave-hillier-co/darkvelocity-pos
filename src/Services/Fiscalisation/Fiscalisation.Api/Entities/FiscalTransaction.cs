using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Fiscalisation.Api.Entities;

/// <summary>
/// Represents a fiscalized transaction signed by a TSE device.
/// Contains all required data for KassenSichV compliance.
/// </summary>
public class FiscalTransaction : BaseEntity, ILocationScoped
{
    public Guid FiscalDeviceId { get; set; }
    public Guid LocationId { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>
    /// Sequential transaction number assigned by the TSE
    /// </summary>
    public long TransactionNumber { get; set; }

    /// <summary>
    /// Type: Receipt, TrainingReceipt, Void, Cancellation
    /// </summary>
    public string TransactionType { get; set; } = "Receipt";

    /// <summary>
    /// KassenSichV process type: Kassenbeleg, AVTransfer, AVBestellung, AVSonstiger
    /// </summary>
    public string ProcessType { get; set; } = "Kassenbeleg";

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Source type: Order, Payment, GiftCard, CashDrawer
    /// </summary>
    public string SourceType { get; set; } = "Order";
    public Guid SourceId { get; set; }

    public decimal GrossAmount { get; set; }

    /// <summary>
    /// Net amounts per tax rate as JSON: { "A": 10.00, "B": 5.00 }
    /// </summary>
    public string NetAmounts { get; set; } = "{}";

    /// <summary>
    /// Tax amounts per tax rate as JSON: { "A": 1.90, "B": 0.35 }
    /// </summary>
    public string TaxAmounts { get; set; } = "{}";

    /// <summary>
    /// Payment breakdown as JSON: { "Cash": 10.00, "Card": 5.00 }
    /// </summary>
    public string PaymentTypes { get; set; } = "{}";

    /// <summary>
    /// Digital signature from TSE
    /// </summary>
    public string? Signature { get; set; }

    /// <summary>
    /// Signature counter at time of signing
    /// </summary>
    public long SignatureCounter { get; set; }

    /// <summary>
    /// TSE certificate serial number
    /// </summary>
    public string? CertificateSerial { get; set; }

    /// <summary>
    /// QR code data for receipt (KassenSichV format)
    /// </summary>
    public string? QrCodeData { get; set; }

    /// <summary>
    /// Full raw response from TSE for audit purposes
    /// </summary>
    public string? TseResponseRaw { get; set; }

    /// <summary>
    /// Status: Pending, Signed, Failed, Retrying
    /// </summary>
    public string Status { get; set; } = "Pending";

    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }

    /// <summary>
    /// When this transaction was included in a DSFinV-K export
    /// </summary>
    public DateTime? ExportedAt { get; set; }

    public FiscalDevice? FiscalDevice { get; set; }
}
