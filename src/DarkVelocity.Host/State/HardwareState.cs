using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

// ============================================================================
// POS Device State
// ============================================================================

[GenerateSerializer]
public sealed class PosDeviceState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid PosDeviceId { get; set; }
    [Id(2)] public Guid LocationId { get; set; }
    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public string DeviceId { get; set; } = string.Empty;
    [Id(5)] public PosDeviceType DeviceType { get; set; }
    [Id(6)] public string? Model { get; set; }
    [Id(7)] public string? OsVersion { get; set; }
    [Id(8)] public string? AppVersion { get; set; }
    [Id(9)] public Guid? DefaultPrinterId { get; set; }
    [Id(10)] public Guid? DefaultCashDrawerId { get; set; }
    [Id(11)] public bool AutoPrintReceipts { get; set; } = true;
    [Id(12)] public bool OpenDrawerOnCash { get; set; } = true;
    [Id(13)] public bool IsActive { get; set; } = true;
    [Id(14)] public bool IsOnline { get; set; }
    [Id(15)] public DateTime? LastSeenAt { get; set; }
    [Id(16)] public DateTime? RegisteredAt { get; set; }
    [Id(17)] public int Version { get; set; }
}

// ============================================================================
// Printer State
// ============================================================================

[GenerateSerializer]
public sealed class PrinterState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid PrinterId { get; set; }
    [Id(2)] public Guid LocationId { get; set; }
    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public PrinterType PrinterType { get; set; }
    [Id(5)] public PrinterConnectionType ConnectionType { get; set; }
    [Id(6)] public string? IpAddress { get; set; }
    [Id(7)] public int? Port { get; set; }
    [Id(8)] public string? MacAddress { get; set; }
    [Id(9)] public string? UsbVendorId { get; set; }
    [Id(10)] public string? UsbProductId { get; set; }
    [Id(11)] public int PaperWidth { get; set; } = 80;
    [Id(12)] public bool IsDefault { get; set; }
    [Id(13)] public bool IsActive { get; set; } = true;
    [Id(14)] public string CharacterSet { get; set; } = "CP437";
    [Id(15)] public bool SupportsCut { get; set; } = true;
    [Id(16)] public bool SupportsCashDrawer { get; set; } = true;
    [Id(17)] public DateTime? LastPrintAt { get; set; }
    [Id(18)] public bool IsOnline { get; set; }
    [Id(19)] public int Version { get; set; }
}

// ============================================================================
// Cash Drawer Hardware State
// ============================================================================

[GenerateSerializer]
public sealed class CashDrawerHardwareState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid CashDrawerId { get; set; }
    [Id(2)] public Guid LocationId { get; set; }
    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public Guid? PrinterId { get; set; }
    [Id(5)] public CashDrawerConnectionType ConnectionType { get; set; }
    [Id(6)] public string? IpAddress { get; set; }
    [Id(7)] public int? Port { get; set; }
    [Id(8)] public bool IsActive { get; set; } = true;
    [Id(9)] public int KickPulsePin { get; set; } = 0;
    [Id(10)] public int KickPulseOnTime { get; set; } = 100;
    [Id(11)] public int KickPulseOffTime { get; set; } = 100;
    [Id(12)] public DateTime? LastOpenedAt { get; set; }
    [Id(13)] public int Version { get; set; }
}

// ============================================================================
// Device Status State
// ============================================================================

[GenerateSerializer]
public sealed class DeviceStatusState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid LocationId { get; set; }
    [Id(2)] public List<RegisteredDeviceState> Devices { get; set; } = [];
    [Id(3)] public List<DeviceAlertState> Alerts { get; set; } = [];
    [Id(4)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class RegisteredDeviceState
{
    [Id(0)] public string DeviceType { get; set; } = string.Empty;
    [Id(1)] public Guid DeviceId { get; set; }
    [Id(2)] public string DeviceName { get; set; } = string.Empty;
    [Id(3)] public bool IsOnline { get; set; }
}

[GenerateSerializer]
public sealed class DeviceAlertState
{
    [Id(0)] public Guid DeviceId { get; set; }
    [Id(1)] public string DeviceType { get; set; } = string.Empty;
    [Id(2)] public string DeviceName { get; set; } = string.Empty;
    [Id(3)] public string AlertType { get; set; } = string.Empty;
    [Id(4)] public string Message { get; set; } = string.Empty;
    [Id(5)] public DateTime Timestamp { get; set; }
}
