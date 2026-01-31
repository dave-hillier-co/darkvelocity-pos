namespace DarkVelocity.Orleans.Abstractions.Grains;

// ============================================================================
// POS Device Grain
// ============================================================================

public enum PosDeviceType
{
    Tablet,
    Terminal,
    Mobile
}

public record RegisterPosDeviceCommand(
    Guid LocationId,
    string Name,
    string DeviceId,
    PosDeviceType DeviceType,
    string? Model,
    string? OsVersion,
    string? AppVersion);

public record UpdatePosDeviceCommand(
    string? Name,
    string? Model,
    string? OsVersion,
    string? AppVersion,
    Guid? DefaultPrinterId,
    Guid? DefaultCashDrawerId,
    bool? AutoPrintReceipts,
    bool? OpenDrawerOnCash,
    bool? IsActive);

public record PosDeviceSnapshot(
    Guid PosDeviceId,
    Guid LocationId,
    string Name,
    string DeviceId,
    PosDeviceType DeviceType,
    string? Model,
    string? OsVersion,
    string? AppVersion,
    Guid? DefaultPrinterId,
    Guid? DefaultCashDrawerId,
    bool AutoPrintReceipts,
    bool OpenDrawerOnCash,
    bool IsActive,
    bool IsOnline,
    DateTime? LastSeenAt,
    DateTime? RegisteredAt);

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

public record RegisterPrinterCommand(
    Guid LocationId,
    string Name,
    PrinterType PrinterType,
    PrinterConnectionType ConnectionType,
    string? IpAddress,
    int? Port,
    string? MacAddress,
    string? UsbVendorId,
    string? UsbProductId,
    int PaperWidth,
    bool IsDefault);

public record UpdatePrinterCommand(
    string? Name,
    string? IpAddress,
    int? Port,
    string? MacAddress,
    int? PaperWidth,
    bool? IsDefault,
    bool? IsActive,
    string? CharacterSet,
    bool? SupportsCut,
    bool? SupportsCashDrawer);

public record PrinterSnapshot(
    Guid PrinterId,
    Guid LocationId,
    string Name,
    PrinterType PrinterType,
    PrinterConnectionType ConnectionType,
    string? IpAddress,
    int? Port,
    string? MacAddress,
    string? UsbVendorId,
    string? UsbProductId,
    int PaperWidth,
    bool IsDefault,
    bool IsActive,
    string CharacterSet,
    bool SupportsCut,
    bool SupportsCashDrawer,
    DateTime? LastPrintAt,
    bool IsOnline);

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

public record RegisterCashDrawerCommand(
    Guid LocationId,
    string Name,
    Guid? PrinterId,
    CashDrawerConnectionType ConnectionType,
    string? IpAddress,
    int? Port);

public record UpdateCashDrawerCommand(
    string? Name,
    Guid? PrinterId,
    string? IpAddress,
    int? Port,
    bool? IsActive,
    int? KickPulsePin,
    int? KickPulseOnTime,
    int? KickPulseOffTime);

public record CashDrawerHardwareSnapshot(
    Guid CashDrawerId,
    Guid LocationId,
    string Name,
    Guid? PrinterId,
    CashDrawerConnectionType ConnectionType,
    string? IpAddress,
    int? Port,
    bool IsActive,
    int KickPulsePin,
    int KickPulseOnTime,
    int KickPulseOffTime,
    DateTime? LastOpenedAt);

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

public record DeviceStatusSummary(
    int TotalPosDevices,
    int OnlinePosDevices,
    int TotalPrinters,
    int OnlinePrinters,
    int TotalCashDrawers,
    IReadOnlyList<DeviceAlert> Alerts);

public record DeviceAlert(
    Guid DeviceId,
    string DeviceType,
    string DeviceName,
    string AlertType,
    string Message,
    DateTime Timestamp);

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
