using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Fiscalisation.Api.Dtos;

// ============================================
// Fiscal Device DTOs
// ============================================

public class FiscalDeviceDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string? PublicKey { get; set; }
    public DateTime? CertificateExpiryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ApiEndpoint { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public long TransactionCounter { get; set; }
    public long SignatureCounter { get; set; }
    public string? ClientId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FiscalDeviceStatusDto : HalResource
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public DateTime? LastSyncAt { get; set; }
    public long TransactionCounter { get; set; }
    public long SignatureCounter { get; set; }
    public bool IsCertificateValid { get; set; }
    public int? DaysUntilCertificateExpiry { get; set; }
}

public record RegisterFiscalDeviceRequest(
    Guid TenantId,
    Guid LocationId,
    string DeviceType,
    string SerialNumber,
    string? ApiEndpoint = null,
    string? ApiCredentials = null,
    string? ClientId = null);

public record InitializeFiscalDeviceRequest(
    string? AdminPin = null);

public record DecommissionFiscalDeviceRequest(
    Guid UserId,
    string Reason);

// ============================================
// Fiscal Transaction DTOs
// ============================================

public class FiscalTransactionDto : HalResource
{
    public Guid Id { get; set; }
    public Guid FiscalDeviceId { get; set; }
    public Guid LocationId { get; set; }
    public Guid TenantId { get; set; }
    public long TransactionNumber { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string ProcessType { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public decimal GrossAmount { get; set; }
    public string NetAmounts { get; set; } = "{}";
    public string TaxAmounts { get; set; } = "{}";
    public string PaymentTypes { get; set; } = "{}";
    public string? Signature { get; set; }
    public long SignatureCounter { get; set; }
    public string? CertificateSerial { get; set; }
    public string? QrCodeData { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? ExportedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FiscalTransactionQrDto : HalResource
{
    public Guid TransactionId { get; set; }
    public string? QrCodeData { get; set; }
    public string? QrCodeBase64Image { get; set; }
}

public record CreateFiscalTransactionRequest(
    Guid LocationId,
    Guid TenantId,
    string TransactionType,
    string ProcessType,
    string SourceType,
    Guid SourceId,
    decimal GrossAmount,
    Dictionary<string, decimal> NetAmounts,
    Dictionary<string, decimal> TaxAmounts,
    Dictionary<string, decimal> PaymentTypes);

public record VoidFiscalTransactionRequest(
    Guid UserId,
    string Reason);

// ============================================
// Fiscal Export DTOs
// ============================================

public class FiscalExportDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public string ExportType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? FileSha256 { get; set; }
    public int TransactionCount { get; set; }
    public DateTime? GeneratedAt { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string? AuditReference { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record CreateFiscalExportRequest(
    Guid TenantId,
    Guid LocationId,
    Guid RequestedByUserId,
    DateTime StartDate,
    DateTime EndDate,
    string ExportType = "OnDemand",
    string? AuditReference = null);

// ============================================
// Fiscal Journal DTOs
// ============================================

public class FiscalJournalDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? LocationId { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid? DeviceId { get; set; }
    public Guid? TransactionId { get; set; }
    public Guid? ExportId { get; set; }
    public string Details { get; set; } = "{}";
    public string? IpAddress { get; set; }
    public Guid? UserId { get; set; }
    public string Severity { get; set; } = string.Empty;
}

// ============================================
// Tax Rate DTOs
// ============================================

public class TaxRateDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public string FiscalCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record CreateTaxRateRequest(
    Guid TenantId,
    string CountryCode,
    decimal Rate,
    string FiscalCode,
    string Description,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo = null);

public record UpdateTaxRateRequest(
    string? Description = null,
    DateTime? EffectiveTo = null,
    bool? IsActive = null);
