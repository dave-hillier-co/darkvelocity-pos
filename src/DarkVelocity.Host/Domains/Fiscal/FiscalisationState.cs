using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

// ============================================================================
// Fiscal Device State
// ============================================================================

[GenerateSerializer]
public sealed class FiscalDeviceState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid FiscalDeviceId { get; set; }
    [Id(2)] public Guid LocationId { get; set; }
    [Id(3)] public FiscalDeviceType DeviceType { get; set; }
    [Id(4)] public string SerialNumber { get; set; } = string.Empty;
    [Id(5)] public string? PublicKey { get; set; }
    [Id(6)] public DateTime? CertificateExpiryDate { get; set; }
    [Id(7)] public FiscalDeviceStatus Status { get; set; }
    [Id(8)] public string? ApiEndpoint { get; set; }
    [Id(9)] public string? ApiCredentialsEncrypted { get; set; }
    [Id(10)] public DateTime? LastSyncAt { get; set; }
    [Id(11)] public long TransactionCounter { get; set; }
    [Id(12)] public long SignatureCounter { get; set; }
    [Id(13)] public string? ClientId { get; set; }
    [Id(14)] public int Version { get; set; }

    // Lifecycle management fields
    [Id(15)] public string? TaxAuthorityRegistrationId { get; set; }
    [Id(16)] public DateTime? ActivatedAt { get; set; }
    [Id(17)] public Guid? ActivatedBy { get; set; }
    [Id(18)] public string? DeactivationReason { get; set; }
    [Id(19)] public DateTime? DeactivatedAt { get; set; }
    [Id(20)] public Guid? DeactivatedBy { get; set; }
    [Id(21)] public DateTime? LastTransactionAt { get; set; }
    [Id(22)] public string? LastError { get; set; }
    [Id(23)] public DateTime? LastSelfTestAt { get; set; }
    [Id(24)] public bool LastSelfTestPassed { get; set; }
}

// ============================================================================
// Fiscal Device Registry State
// ============================================================================

[GenerateSerializer]
public sealed class FiscalDeviceRegistryState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public Dictionary<Guid, string> Devices { get; set; } = [];
    [Id(3)] public int Version { get; set; }
}

// ============================================================================
// Fiscal Transaction Registry State
// ============================================================================

[GenerateSerializer]
public sealed class FiscalTransactionRegistryState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public List<FiscalTransactionRegistryEntry> Transactions { get; set; } = [];
    [Id(3)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class FiscalTransactionRegistryEntry
{
    [Id(0)] public Guid TransactionId { get; set; }
    [Id(1)] public Guid DeviceId { get; set; }
    [Id(2)] public DateOnly Date { get; set; }
    [Id(3)] public DateTime CreatedAt { get; set; }
}

// ============================================================================
// Fiscal Transaction State
// ============================================================================

[GenerateSerializer]
public sealed class FiscalTransactionState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid FiscalTransactionId { get; set; }
    [Id(2)] public Guid FiscalDeviceId { get; set; }
    [Id(3)] public Guid LocationId { get; set; }
    [Id(4)] public long TransactionNumber { get; set; }
    [Id(5)] public FiscalTransactionType TransactionType { get; set; }
    [Id(6)] public FiscalProcessType ProcessType { get; set; }
    [Id(7)] public DateTime StartTime { get; set; }
    [Id(8)] public DateTime? EndTime { get; set; }
    [Id(9)] public string SourceType { get; set; } = string.Empty;
    [Id(10)] public Guid SourceId { get; set; }
    [Id(11)] public decimal GrossAmount { get; set; }
    [Id(12)] public Dictionary<string, decimal> NetAmounts { get; set; } = [];
    [Id(13)] public Dictionary<string, decimal> TaxAmounts { get; set; } = [];
    [Id(14)] public Dictionary<string, decimal> PaymentTypes { get; set; } = [];
    [Id(15)] public string? Signature { get; set; }
    [Id(16)] public long? SignatureCounter { get; set; }
    [Id(17)] public string? CertificateSerial { get; set; }
    [Id(18)] public string? QrCodeData { get; set; }
    [Id(19)] public string? TseResponseRaw { get; set; }
    [Id(20)] public FiscalTransactionStatus Status { get; set; }
    [Id(21)] public string? ErrorMessage { get; set; }
    [Id(22)] public int RetryCount { get; set; }
    [Id(23)] public DateTime? ExportedAt { get; set; }
    [Id(24)] public int Version { get; set; }
}

// ============================================================================
// Fiscal Journal State
// ============================================================================

[GenerateSerializer]
public sealed class FiscalJournalState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public DateTime Date { get; set; }
    [Id(2)] public List<FiscalJournalEntryState> Entries { get; set; } = [];
    [Id(3)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class FiscalJournalEntryState
{
    [Id(0)] public Guid EntryId { get; set; }
    [Id(1)] public DateTime Timestamp { get; set; }
    [Id(2)] public Guid? LocationId { get; set; }
    [Id(3)] public FiscalEventType EventType { get; set; }
    [Id(4)] public Guid? DeviceId { get; set; }
    [Id(5)] public Guid? TransactionId { get; set; }
    [Id(6)] public Guid? ExportId { get; set; }
    [Id(7)] public string Details { get; set; } = string.Empty;
    [Id(8)] public string? IpAddress { get; set; }
    [Id(9)] public Guid? UserId { get; set; }
    [Id(10)] public FiscalEventSeverity Severity { get; set; }
}

// ============================================================================
// Tax Rate State
// ============================================================================

[GenerateSerializer]
public sealed class TaxRateState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid TaxRateId { get; set; }
    [Id(2)] public string CountryCode { get; set; } = string.Empty;
    [Id(3)] public decimal Rate { get; set; }
    [Id(4)] public string FiscalCode { get; set; } = string.Empty;
    [Id(5)] public string Description { get; set; } = string.Empty;
    [Id(6)] public DateTime EffectiveFrom { get; set; }
    [Id(7)] public DateTime? EffectiveTo { get; set; }
    [Id(8)] public bool IsActive { get; set; } = true;
    [Id(9)] public int Version { get; set; }
}
