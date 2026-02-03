namespace DarkVelocity.Host.Grains;

// ============================================================================
// POS Device Grain
// ============================================================================

public enum PosDeviceType
{
    Tablet,
    Terminal,
    Mobile
}

[GenerateSerializer]
public record RegisterPosDeviceCommand(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] string Name,
    [property: Id(2)] string DeviceId,
    [property: Id(3)] PosDeviceType DeviceType,
    [property: Id(4)] string? Model,
    [property: Id(5)] string? OsVersion,
    [property: Id(6)] string? AppVersion);

[GenerateSerializer]
public record UpdatePosDeviceCommand(
    [property: Id(0)] string? Name,
    [property: Id(1)] string? Model,
    [property: Id(2)] string? OsVersion,
    [property: Id(3)] string? AppVersion,
    [property: Id(4)] Guid? DefaultPrinterId,
    [property: Id(5)] Guid? DefaultCashDrawerId,
    [property: Id(6)] bool? AutoPrintReceipts,
    [property: Id(7)] bool? OpenDrawerOnCash,
    [property: Id(8)] bool? IsActive);

[GenerateSerializer]
public record PosDeviceSnapshot(
    [property: Id(0)] Guid PosDeviceId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] string Name,
    [property: Id(3)] string DeviceId,
    [property: Id(4)] PosDeviceType DeviceType,
    [property: Id(5)] string? Model,
    [property: Id(6)] string? OsVersion,
    [property: Id(7)] string? AppVersion,
    [property: Id(8)] Guid? DefaultPrinterId,
    [property: Id(9)] Guid? DefaultCashDrawerId,
    [property: Id(10)] bool AutoPrintReceipts,
    [property: Id(11)] bool OpenDrawerOnCash,
    [property: Id(12)] bool IsActive,
    [property: Id(13)] bool IsOnline,
    [property: Id(14)] DateTime? LastSeenAt,
    [property: Id(15)] DateTime? RegisteredAt);

/// <summary>
/// Grain for POS device management.
/// Key: "{orgId}:posdevice:{deviceId}"
/// </summary>
public interface IPosDeviceGrain : IGrainWithStringKey
{
    Task<PosDeviceSnapshot> RegisterAsync(RegisterPosDeviceCommand command);
    Task<PosDeviceSnapshot> UpdateAsync(UpdatePosDeviceCommand command);
    Task DeactivateAsync();
    Task<PosDeviceSnapshot> GetSnapshotAsync();
    Task RecordHeartbeatAsync(string? appVersion, string? osVersion);
    Task SetOfflineAsync();
    Task<bool> IsOnlineAsync();
}

// ============================================================================
// Printer Grain
// ============================================================================

public enum PrinterType
{
    Receipt,
    Kitchen,
    Label
}

public enum PrinterConnectionType
{
    Usb,
    Network,
    Bluetooth
}

[GenerateSerializer]
public record RegisterPrinterCommand(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] string Name,
    [property: Id(2)] PrinterType PrinterType,
    [property: Id(3)] PrinterConnectionType ConnectionType,
    [property: Id(4)] string? IpAddress,
    [property: Id(5)] int? Port,
    [property: Id(6)] string? MacAddress,
    [property: Id(7)] string? UsbVendorId,
    [property: Id(8)] string? UsbProductId,
    [property: Id(9)] int PaperWidth,
    [property: Id(10)] bool IsDefault);

[GenerateSerializer]
public record UpdatePrinterCommand(
    [property: Id(0)] string? Name,
    [property: Id(1)] string? IpAddress,
    [property: Id(2)] int? Port,
    [property: Id(3)] string? MacAddress,
    [property: Id(4)] int? PaperWidth,
    [property: Id(5)] bool? IsDefault,
    [property: Id(6)] bool? IsActive,
    [property: Id(7)] string? CharacterSet,
    [property: Id(8)] bool? SupportsCut,
    [property: Id(9)] bool? SupportsCashDrawer);

[GenerateSerializer]
public record PrinterSnapshot(
    [property: Id(0)] Guid PrinterId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] string Name,
    [property: Id(3)] PrinterType PrinterType,
    [property: Id(4)] PrinterConnectionType ConnectionType,
    [property: Id(5)] string? IpAddress,
    [property: Id(6)] int? Port,
    [property: Id(7)] string? MacAddress,
    [property: Id(8)] string? UsbVendorId,
    [property: Id(9)] string? UsbProductId,
    [property: Id(10)] int PaperWidth,
    [property: Id(11)] bool IsDefault,
    [property: Id(12)] bool IsActive,
    [property: Id(13)] string CharacterSet,
    [property: Id(14)] bool SupportsCut,
    [property: Id(15)] bool SupportsCashDrawer,
    [property: Id(16)] DateTime? LastPrintAt,
    [property: Id(17)] bool IsOnline);

/// <summary>
/// Grain for printer management.
/// Key: "{orgId}:printer:{printerId}"
/// </summary>
public interface IPrinterGrain : IGrainWithStringKey
{
    Task<PrinterSnapshot> RegisterAsync(RegisterPrinterCommand command);
    Task<PrinterSnapshot> UpdateAsync(UpdatePrinterCommand command);
    Task DeactivateAsync();
    Task<PrinterSnapshot> GetSnapshotAsync();
    Task RecordPrintAsync();
    Task SetOnlineAsync(bool isOnline);
    Task<bool> IsOnlineAsync();
}

// ============================================================================
// Cash Drawer Grain (Extended)
// ============================================================================

public enum CashDrawerConnectionType
{
    Printer,
    Usb,
    Network
}

[GenerateSerializer]
public record RegisterCashDrawerCommand(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] string Name,
    [property: Id(2)] Guid? PrinterId,
    [property: Id(3)] CashDrawerConnectionType ConnectionType,
    [property: Id(4)] string? IpAddress,
    [property: Id(5)] int? Port);

[GenerateSerializer]
public record UpdateCashDrawerCommand(
    [property: Id(0)] string? Name,
    [property: Id(1)] Guid? PrinterId,
    [property: Id(2)] string? IpAddress,
    [property: Id(3)] int? Port,
    [property: Id(4)] bool? IsActive,
    [property: Id(5)] int? KickPulsePin,
    [property: Id(6)] int? KickPulseOnTime,
    [property: Id(7)] int? KickPulseOffTime);

[GenerateSerializer]
public record CashDrawerHardwareSnapshot(
    [property: Id(0)] Guid CashDrawerId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] string Name,
    [property: Id(3)] Guid? PrinterId,
    [property: Id(4)] CashDrawerConnectionType ConnectionType,
    [property: Id(5)] string? IpAddress,
    [property: Id(6)] int? Port,
    [property: Id(7)] bool IsActive,
    [property: Id(8)] int KickPulsePin,
    [property: Id(9)] int KickPulseOnTime,
    [property: Id(10)] int KickPulseOffTime,
    [property: Id(11)] DateTime? LastOpenedAt);

/// <summary>
/// Grain for cash drawer hardware management.
/// Key: "{orgId}:cashdrawerhw:{drawerId}"
/// </summary>
public interface ICashDrawerHardwareGrain : IGrainWithStringKey
{
    Task<CashDrawerHardwareSnapshot> RegisterAsync(RegisterCashDrawerCommand command);
    Task<CashDrawerHardwareSnapshot> UpdateAsync(UpdateCashDrawerCommand command);
    Task DeactivateAsync();
    Task<CashDrawerHardwareSnapshot> GetSnapshotAsync();
    Task RecordOpenAsync();
    Task<string> GetKickCommandAsync();
}

// ============================================================================
// Device Status Grain (Aggregate)
// ============================================================================

[GenerateSerializer]
public record DeviceStatusSummary(
    [property: Id(0)] int TotalPosDevices,
    [property: Id(1)] int OnlinePosDevices,
    [property: Id(2)] int TotalPrinters,
    [property: Id(3)] int OnlinePrinters,
    [property: Id(4)] int TotalCashDrawers,
    [property: Id(5)] IReadOnlyList<DeviceAlert> Alerts);

[GenerateSerializer]
public record DeviceAlert(
    [property: Id(0)] Guid DeviceId,
    [property: Id(1)] string DeviceType,
    [property: Id(2)] string DeviceName,
    [property: Id(3)] string AlertType,
    [property: Id(4)] string Message,
    [property: Id(5)] DateTime Timestamp);

/// <summary>
/// Grain for device status aggregation at location level.
/// Key: "{orgId}:{locationId}:devicestatus"
/// </summary>
public interface IDeviceStatusGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid locationId);
    Task RegisterDeviceAsync(string deviceType, Guid deviceId, string deviceName);
    Task UnregisterDeviceAsync(string deviceType, Guid deviceId);
    Task UpdateDeviceStatusAsync(string deviceType, Guid deviceId, bool isOnline);
    Task AddAlertAsync(DeviceAlert alert);
    Task ClearAlertAsync(Guid deviceId);
    Task<DeviceStatusSummary> GetSummaryAsync();
}
