using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// POS Device Grain
// ============================================================================

/// <summary>
/// Grain for POS device management.
/// Manages POS terminals, tablets, and mobile devices.
/// </summary>
public class PosDeviceGrain : Grain, IPosDeviceGrain
{
    private readonly IPersistentState<PosDeviceState> _state;

    public PosDeviceGrain(
        [PersistentState("posDevice", "OrleansStorage")]
        IPersistentState<PosDeviceState> state)
    {
        _state = state;
    }

    public async Task<PosDeviceSnapshot> RegisterAsync(RegisterPosDeviceCommand command)
    {
        if (_state.State.PosDeviceId != Guid.Empty)
            throw new InvalidOperationException("POS device already registered");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var deviceId = Guid.Parse(parts[2]);

        _state.State = new PosDeviceState
        {
            OrgId = orgId,
            PosDeviceId = deviceId,
            LocationId = command.LocationId,
            Name = command.Name,
            DeviceId = command.DeviceId,
            DeviceType = command.DeviceType,
            Model = command.Model,
            OsVersion = command.OsVersion,
            AppVersion = command.AppVersion,
            IsActive = true,
            IsOnline = true,
            RegisteredAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<PosDeviceSnapshot> UpdateAsync(UpdatePosDeviceCommand command)
    {
        EnsureInitialized();

        if (command.Name != null) _state.State.Name = command.Name;
        if (command.Model != null) _state.State.Model = command.Model;
        if (command.OsVersion != null) _state.State.OsVersion = command.OsVersion;
        if (command.AppVersion != null) _state.State.AppVersion = command.AppVersion;
        if (command.DefaultPrinterId.HasValue) _state.State.DefaultPrinterId = command.DefaultPrinterId.Value;
        if (command.DefaultCashDrawerId.HasValue) _state.State.DefaultCashDrawerId = command.DefaultCashDrawerId.Value;
        if (command.AutoPrintReceipts.HasValue) _state.State.AutoPrintReceipts = command.AutoPrintReceipts.Value;
        if (command.OpenDrawerOnCash.HasValue) _state.State.OpenDrawerOnCash = command.OpenDrawerOnCash.Value;
        if (command.IsActive.HasValue) _state.State.IsActive = command.IsActive.Value;

        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task DeactivateAsync()
    {
        EnsureInitialized();
        _state.State.IsActive = false;
        _state.State.IsOnline = false;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<PosDeviceSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public async Task RecordHeartbeatAsync(string? appVersion, string? osVersion)
    {
        EnsureInitialized();

        _state.State.IsOnline = true;
        _state.State.LastSeenAt = DateTime.UtcNow;

        if (appVersion != null) _state.State.AppVersion = appVersion;
        if (osVersion != null) _state.State.OsVersion = osVersion;

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetOfflineAsync()
    {
        EnsureInitialized();
        _state.State.IsOnline = false;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<bool> IsOnlineAsync()
    {
        EnsureInitialized();
        return Task.FromResult(_state.State.IsOnline);
    }

    private PosDeviceSnapshot CreateSnapshot()
    {
        return new PosDeviceSnapshot(
            PosDeviceId: _state.State.PosDeviceId,
            LocationId: _state.State.LocationId,
            Name: _state.State.Name,
            DeviceId: _state.State.DeviceId,
            DeviceType: _state.State.DeviceType,
            Model: _state.State.Model,
            OsVersion: _state.State.OsVersion,
            AppVersion: _state.State.AppVersion,
            DefaultPrinterId: _state.State.DefaultPrinterId,
            DefaultCashDrawerId: _state.State.DefaultCashDrawerId,
            AutoPrintReceipts: _state.State.AutoPrintReceipts,
            OpenDrawerOnCash: _state.State.OpenDrawerOnCash,
            IsActive: _state.State.IsActive,
            IsOnline: _state.State.IsOnline,
            LastSeenAt: _state.State.LastSeenAt,
            RegisteredAt: _state.State.RegisteredAt);
    }

    private void EnsureInitialized()
    {
        if (_state.State.PosDeviceId == Guid.Empty)
            throw new InvalidOperationException("POS device grain not initialized");
    }
}

// ============================================================================
// Printer Grain
// ============================================================================

/// <summary>
/// Grain for printer management.
/// Manages receipt, kitchen, and label printers.
/// </summary>
public class PrinterGrain : Grain, IPrinterGrain
{
    private readonly IPersistentState<PrinterState> _state;

    public PrinterGrain(
        [PersistentState("printer", "OrleansStorage")]
        IPersistentState<PrinterState> state)
    {
        _state = state;
    }

    public async Task<PrinterSnapshot> RegisterAsync(RegisterPrinterCommand command)
    {
        if (_state.State.PrinterId != Guid.Empty)
            throw new InvalidOperationException("Printer already registered");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var printerId = Guid.Parse(parts[2]);

        _state.State = new PrinterState
        {
            OrgId = orgId,
            PrinterId = printerId,
            LocationId = command.LocationId,
            Name = command.Name,
            PrinterType = command.PrinterType,
            ConnectionType = command.ConnectionType,
            IpAddress = command.IpAddress,
            Port = command.Port,
            MacAddress = command.MacAddress,
            UsbVendorId = command.UsbVendorId,
            UsbProductId = command.UsbProductId,
            PaperWidth = command.PaperWidth,
            IsDefault = command.IsDefault,
            IsActive = true,
            IsOnline = false,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<PrinterSnapshot> UpdateAsync(UpdatePrinterCommand command)
    {
        EnsureInitialized();

        if (command.Name != null) _state.State.Name = command.Name;
        if (command.IpAddress != null) _state.State.IpAddress = command.IpAddress;
        if (command.Port.HasValue) _state.State.Port = command.Port.Value;
        if (command.MacAddress != null) _state.State.MacAddress = command.MacAddress;
        if (command.PaperWidth.HasValue) _state.State.PaperWidth = command.PaperWidth.Value;
        if (command.IsDefault.HasValue) _state.State.IsDefault = command.IsDefault.Value;
        if (command.IsActive.HasValue) _state.State.IsActive = command.IsActive.Value;
        if (command.CharacterSet != null) _state.State.CharacterSet = command.CharacterSet;
        if (command.SupportsCut.HasValue) _state.State.SupportsCut = command.SupportsCut.Value;
        if (command.SupportsCashDrawer.HasValue) _state.State.SupportsCashDrawer = command.SupportsCashDrawer.Value;

        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task DeactivateAsync()
    {
        EnsureInitialized();
        _state.State.IsActive = false;
        _state.State.IsOnline = false;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<PrinterSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public async Task RecordPrintAsync()
    {
        EnsureInitialized();
        _state.State.LastPrintAt = DateTime.UtcNow;
        _state.State.IsOnline = true;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetOnlineAsync(bool isOnline)
    {
        EnsureInitialized();
        _state.State.IsOnline = isOnline;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<bool> IsOnlineAsync()
    {
        EnsureInitialized();
        return Task.FromResult(_state.State.IsOnline);
    }

    private PrinterSnapshot CreateSnapshot()
    {
        return new PrinterSnapshot(
            PrinterId: _state.State.PrinterId,
            LocationId: _state.State.LocationId,
            Name: _state.State.Name,
            PrinterType: _state.State.PrinterType,
            ConnectionType: _state.State.ConnectionType,
            IpAddress: _state.State.IpAddress,
            Port: _state.State.Port,
            MacAddress: _state.State.MacAddress,
            UsbVendorId: _state.State.UsbVendorId,
            UsbProductId: _state.State.UsbProductId,
            PaperWidth: _state.State.PaperWidth,
            IsDefault: _state.State.IsDefault,
            IsActive: _state.State.IsActive,
            CharacterSet: _state.State.CharacterSet,
            SupportsCut: _state.State.SupportsCut,
            SupportsCashDrawer: _state.State.SupportsCashDrawer,
            LastPrintAt: _state.State.LastPrintAt,
            IsOnline: _state.State.IsOnline);
    }

    private void EnsureInitialized()
    {
        if (_state.State.PrinterId == Guid.Empty)
            throw new InvalidOperationException("Printer grain not initialized");
    }
}

// ============================================================================
// Cash Drawer Hardware Grain
// ============================================================================

/// <summary>
/// Grain for cash drawer hardware management.
/// Manages physical cash drawer configurations.
/// </summary>
public class CashDrawerHardwareGrain : Grain, ICashDrawerHardwareGrain
{
    private readonly IPersistentState<CashDrawerHardwareState> _state;

    public CashDrawerHardwareGrain(
        [PersistentState("cashDrawerHardware", "OrleansStorage")]
        IPersistentState<CashDrawerHardwareState> state)
    {
        _state = state;
    }

    public async Task<CashDrawerHardwareSnapshot> RegisterAsync(RegisterCashDrawerCommand command)
    {
        if (_state.State.CashDrawerId != Guid.Empty)
            throw new InvalidOperationException("Cash drawer already registered");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var drawerId = Guid.Parse(parts[2]);

        _state.State = new CashDrawerHardwareState
        {
            OrgId = orgId,
            CashDrawerId = drawerId,
            LocationId = command.LocationId,
            Name = command.Name,
            PrinterId = command.PrinterId,
            ConnectionType = command.ConnectionType,
            IpAddress = command.IpAddress,
            Port = command.Port,
            IsActive = true,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<CashDrawerHardwareSnapshot> UpdateAsync(UpdateCashDrawerCommand command)
    {
        EnsureInitialized();

        if (command.Name != null) _state.State.Name = command.Name;
        if (command.PrinterId.HasValue) _state.State.PrinterId = command.PrinterId.Value;
        if (command.IpAddress != null) _state.State.IpAddress = command.IpAddress;
        if (command.Port.HasValue) _state.State.Port = command.Port.Value;
        if (command.IsActive.HasValue) _state.State.IsActive = command.IsActive.Value;
        if (command.KickPulsePin.HasValue) _state.State.KickPulsePin = command.KickPulsePin.Value;
        if (command.KickPulseOnTime.HasValue) _state.State.KickPulseOnTime = command.KickPulseOnTime.Value;
        if (command.KickPulseOffTime.HasValue) _state.State.KickPulseOffTime = command.KickPulseOffTime.Value;

        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task DeactivateAsync()
    {
        EnsureInitialized();
        _state.State.IsActive = false;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<CashDrawerHardwareSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public async Task RecordOpenAsync()
    {
        EnsureInitialized();
        _state.State.LastOpenedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<string> GetKickCommandAsync()
    {
        EnsureInitialized();

        // ESC/POS drawer kick command
        var pin = _state.State.KickPulsePin;
        var onTime = _state.State.KickPulseOnTime / 2; // Convert to ESC/POS units
        var offTime = _state.State.KickPulseOffTime / 2;

        // ESC p m t1 t2 (0x1B 0x70)
        return Task.FromResult($"\\x1B\\x70\\x{pin:X2}\\x{onTime:X2}\\x{offTime:X2}");
    }

    private CashDrawerHardwareSnapshot CreateSnapshot()
    {
        return new CashDrawerHardwareSnapshot(
            CashDrawerId: _state.State.CashDrawerId,
            LocationId: _state.State.LocationId,
            Name: _state.State.Name,
            PrinterId: _state.State.PrinterId,
            ConnectionType: _state.State.ConnectionType,
            IpAddress: _state.State.IpAddress,
            Port: _state.State.Port,
            IsActive: _state.State.IsActive,
            KickPulsePin: _state.State.KickPulsePin,
            KickPulseOnTime: _state.State.KickPulseOnTime,
            KickPulseOffTime: _state.State.KickPulseOffTime,
            LastOpenedAt: _state.State.LastOpenedAt);
    }

    private void EnsureInitialized()
    {
        if (_state.State.CashDrawerId == Guid.Empty)
            throw new InvalidOperationException("Cash drawer hardware grain not initialized");
    }
}

// ============================================================================
// Device Status Grain
// ============================================================================

/// <summary>
/// Grain for device status aggregation at location level.
/// Provides real-time device status for monitoring.
/// </summary>
public class DeviceStatusGrain : Grain, IDeviceStatusGrain
{
    private readonly IPersistentState<DeviceStatusState> _state;

    public DeviceStatusGrain(
        [PersistentState("deviceStatus", "OrleansStorage")]
        IPersistentState<DeviceStatusState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(Guid locationId)
    {
        if (_state.State.LocationId != Guid.Empty)
            return;

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State = new DeviceStatusState
        {
            OrgId = orgId,
            LocationId = locationId,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task RegisterDeviceAsync(string deviceType, Guid deviceId, string deviceName)
    {
        EnsureInitialized();

        var existing = _state.State.Devices.FirstOrDefault(d => d.DeviceId == deviceId);
        if (existing == null)
        {
            _state.State.Devices.Add(new RegisteredDeviceState
            {
                DeviceType = deviceType,
                DeviceId = deviceId,
                DeviceName = deviceName,
                IsOnline = false
            });

            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task UnregisterDeviceAsync(string deviceType, Guid deviceId)
    {
        EnsureInitialized();

        _state.State.Devices.RemoveAll(d => d.DeviceId == deviceId);
        _state.State.Alerts.RemoveAll(a => a.DeviceId == deviceId);

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task UpdateDeviceStatusAsync(string deviceType, Guid deviceId, bool isOnline)
    {
        EnsureInitialized();

        var device = _state.State.Devices.FirstOrDefault(d => d.DeviceId == deviceId);
        if (device != null)
        {
            device.IsOnline = isOnline;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task AddAlertAsync(DeviceAlert alert)
    {
        EnsureInitialized();

        _state.State.Alerts.Add(new DeviceAlertState
        {
            DeviceId = alert.DeviceId,
            DeviceType = alert.DeviceType,
            DeviceName = alert.DeviceName,
            AlertType = alert.AlertType,
            Message = alert.Message,
            Timestamp = alert.Timestamp
        });

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task ClearAlertAsync(Guid deviceId)
    {
        EnsureInitialized();

        _state.State.Alerts.RemoveAll(a => a.DeviceId == deviceId);
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<DeviceStatusSummary> GetSummaryAsync()
    {
        EnsureInitialized();

        var posDevices = _state.State.Devices.Where(d => d.DeviceType == "POS").ToList();
        var printers = _state.State.Devices.Where(d => d.DeviceType == "Printer").ToList();
        var cashDrawers = _state.State.Devices.Where(d => d.DeviceType == "CashDrawer").ToList();

        return Task.FromResult(new DeviceStatusSummary(
            TotalPosDevices: posDevices.Count,
            OnlinePosDevices: posDevices.Count(d => d.IsOnline),
            TotalPrinters: printers.Count,
            OnlinePrinters: printers.Count(d => d.IsOnline),
            TotalCashDrawers: cashDrawers.Count,
            Alerts: _state.State.Alerts.Select(a => new DeviceAlert(
                DeviceId: a.DeviceId,
                DeviceType: a.DeviceType,
                DeviceName: a.DeviceName,
                AlertType: a.AlertType,
                Message: a.Message,
                Timestamp: a.Timestamp)).ToList()));
    }

    private void EnsureInitialized()
    {
        if (_state.State.LocationId == Guid.Empty)
            throw new InvalidOperationException("Device status grain not initialized");
    }
}
