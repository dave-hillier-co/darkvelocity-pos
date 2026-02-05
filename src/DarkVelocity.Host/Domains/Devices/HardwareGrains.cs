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

    // Health monitoring implementation

    public async Task RecordHeartbeatAsync(Guid deviceId, UpdateDeviceHealthCommand? healthUpdate = null)
    {
        EnsureInitialized();

        var device = _state.State.Devices.FirstOrDefault(d => d.DeviceId == deviceId);
        if (device == null)
            return;

        var now = DateTime.UtcNow;
        var wasOnline = device.IsOnline;

        device.LastSeenAt = now;
        device.IsOnline = true;

        // Track uptime
        if (!wasOnline && device.LastOnlineStatusChange.HasValue)
        {
            device.LastOnlineStatusChange = now;
        }
        else if (device.FirstSeenAt == null)
        {
            device.FirstSeenAt = now;
            device.LastOnlineStatusChange = now;
        }

        // Update health metrics if provided
        if (healthUpdate != null)
        {
            if (healthUpdate.SignalStrength.HasValue)
                device.SignalStrength = healthUpdate.SignalStrength;
            if (healthUpdate.LatencyMs.HasValue)
                device.LatencyMs = healthUpdate.LatencyMs;
            if (healthUpdate.PrinterStatus.HasValue)
                device.PrinterStatus = healthUpdate.PrinterStatus;
            if (healthUpdate.PaperLevel.HasValue)
                device.PaperLevel = healthUpdate.PaperLevel;
            if (healthUpdate.PendingPrintJobs.HasValue)
                device.PendingPrintJobs = healthUpdate.PendingPrintJobs;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<DeviceHealthMetrics?> GetDeviceHealthAsync(Guid deviceId)
    {
        EnsureInitialized();

        var device = _state.State.Devices.FirstOrDefault(d => d.DeviceId == deviceId);
        if (device == null)
            return Task.FromResult<DeviceHealthMetrics?>(null);

        return Task.FromResult<DeviceHealthMetrics?>(CreateHealthMetrics(device));
    }

    public Task<DeviceHealthSummary> GetHealthSummaryAsync()
    {
        EnsureInitialized();

        var devices = _state.State.Devices;
        var onlineCount = devices.Count(d => d.IsOnline);
        var offlineCount = devices.Count(d => !d.IsOnline);
        var alertDeviceIds = _state.State.Alerts.Select(a => a.DeviceId).Distinct().ToHashSet();
        var devicesWithAlerts = devices.Count(d => alertDeviceIds.Contains(d.DeviceId));

        var overallQuality = CalculateOverallConnectionQuality(devices);

        var metrics = devices.Select(CreateHealthMetrics).ToList();
        var alerts = _state.State.Alerts.Select(a => new DeviceAlert(
            DeviceId: a.DeviceId,
            DeviceType: a.DeviceType,
            DeviceName: a.DeviceName,
            AlertType: a.AlertType,
            Message: a.Message,
            Timestamp: a.Timestamp)).ToList();

        return Task.FromResult(new DeviceHealthSummary(
            LocationId: _state.State.LocationId,
            TotalDevices: devices.Count,
            OnlineDevices: onlineCount,
            OfflineDevices: offlineCount,
            DevicesWithAlerts: devicesWithAlerts,
            OverallConnectionQuality: overallQuality,
            LastHealthCheck: DateTime.UtcNow,
            DeviceMetrics: metrics,
            ActiveAlerts: alerts));
    }

    public async Task<IReadOnlyList<DeviceHealthMetrics>> PerformHealthCheckAsync(TimeSpan offlineThreshold)
    {
        EnsureInitialized();

        var now = DateTime.UtcNow;
        var staleDevices = new List<DeviceHealthMetrics>();

        foreach (var device in _state.State.Devices)
        {
            // Check if device hasn't been seen within threshold
            if (device.IsOnline &&
                device.LastSeenAt.HasValue &&
                (now - device.LastSeenAt.Value) > offlineThreshold)
            {
                // Mark as offline
                device.IsOnline = false;

                // Track disconnect
                device.DisconnectTimestamps.Add(now);

                // Keep only last 24h of disconnects
                var cutoff = now.AddHours(-24);
                device.DisconnectTimestamps.RemoveAll(t => t < cutoff);

                // Update uptime tracking
                if (device.LastOnlineStatusChange.HasValue)
                {
                    device.TotalOnlineTime += (now - device.LastOnlineStatusChange.Value);
                }
                device.LastOnlineStatusChange = now;

                // Create alert
                _state.State.Alerts.Add(new DeviceAlertState
                {
                    DeviceId = device.DeviceId,
                    DeviceType = device.DeviceType,
                    DeviceName = device.DeviceName,
                    AlertType = "Offline",
                    Message = $"Device has not been seen for {offlineThreshold.TotalMinutes:F0} minutes",
                    Timestamp = now
                });

                staleDevices.Add(CreateHealthMetrics(device));
            }
        }

        if (staleDevices.Any())
        {
            _state.State.Version++;
            await _state.WriteStateAsync();
        }

        return staleDevices;
    }

    public async Task UpdatePrinterHealthAsync(Guid printerId, PrinterHealthStatus status, int? paperLevel = null)
    {
        EnsureInitialized();

        var device = _state.State.Devices.FirstOrDefault(d => d.DeviceId == printerId);
        if (device == null || device.DeviceType != "Printer")
            return;

        device.PrinterStatus = status;
        if (paperLevel.HasValue)
            device.PaperLevel = paperLevel;

        // Create alerts for problematic statuses
        if (status == PrinterHealthStatus.PaperOut)
        {
            // Remove existing paper alerts
            _state.State.Alerts.RemoveAll(a => a.DeviceId == printerId && a.AlertType.StartsWith("Paper"));

            _state.State.Alerts.Add(new DeviceAlertState
            {
                DeviceId = printerId,
                DeviceType = "Printer",
                DeviceName = device.DeviceName,
                AlertType = "PaperOut",
                Message = "Printer is out of paper",
                Timestamp = DateTime.UtcNow
            });
        }
        else if (status == PrinterHealthStatus.PaperLow)
        {
            // Remove existing paper alerts
            _state.State.Alerts.RemoveAll(a => a.DeviceId == printerId && a.AlertType.StartsWith("Paper"));

            _state.State.Alerts.Add(new DeviceAlertState
            {
                DeviceId = printerId,
                DeviceType = "Printer",
                DeviceName = device.DeviceName,
                AlertType = "PaperLow",
                Message = $"Printer paper is low ({paperLevel}%)",
                Timestamp = DateTime.UtcNow
            });
        }
        else if (status == PrinterHealthStatus.Error)
        {
            _state.State.Alerts.Add(new DeviceAlertState
            {
                DeviceId = printerId,
                DeviceType = "Printer",
                DeviceName = device.DeviceName,
                AlertType = "PrinterError",
                Message = "Printer has encountered an error",
                Timestamp = DateTime.UtcNow
            });
        }
        else if (status == PrinterHealthStatus.Ready)
        {
            // Clear paper and error alerts when ready
            _state.State.Alerts.RemoveAll(a => a.DeviceId == printerId &&
                (a.AlertType.StartsWith("Paper") || a.AlertType == "PrinterError"));
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private DeviceHealthMetrics CreateHealthMetrics(RegisteredDeviceState device)
    {
        var now = DateTime.UtcNow;

        // Calculate disconnect count in last 24h
        var cutoff = now.AddHours(-24);
        var disconnectCount24h = device.DisconnectTimestamps.Count(t => t >= cutoff);

        // Calculate uptime percentage
        double? uptimePercentage = null;
        if (device.FirstSeenAt.HasValue)
        {
            var totalTime = now - device.FirstSeenAt.Value;
            if (totalTime.TotalSeconds > 0)
            {
                var onlineTime = device.TotalOnlineTime;
                if (device.IsOnline && device.LastOnlineStatusChange.HasValue)
                {
                    onlineTime += (now - device.LastOnlineStatusChange.Value);
                }
                uptimePercentage = (onlineTime.TotalSeconds / totalTime.TotalSeconds) * 100;
            }
        }

        // Determine connection quality based on metrics
        var connectionQuality = CalculateConnectionQuality(device, disconnectCount24h);

        return new DeviceHealthMetrics(
            DeviceId: device.DeviceId,
            DeviceType: device.DeviceType,
            DeviceName: device.DeviceName,
            IsOnline: device.IsOnline,
            LastSeenAt: device.LastSeenAt,
            ConnectionQuality: connectionQuality,
            SignalStrength: device.SignalStrength,
            LatencyMs: device.LatencyMs,
            UptimePercentage: uptimePercentage,
            DisconnectCount24h: disconnectCount24h,
            PrinterStatus: device.PrinterStatus,
            PaperLevel: device.PaperLevel,
            PendingPrintJobs: device.PendingPrintJobs,
            FailedPrintJobs24h: device.FailedPrintJobs24h);
    }

    private static ConnectionQuality CalculateConnectionQuality(RegisteredDeviceState device, int disconnectCount24h)
    {
        if (!device.IsOnline)
            return ConnectionQuality.Disconnected;

        // Score based on multiple factors
        var score = 100;

        // Signal strength impact
        if (device.SignalStrength.HasValue)
        {
            if (device.SignalStrength < -70) score -= 30;
            else if (device.SignalStrength < -60) score -= 15;
            else if (device.SignalStrength < -50) score -= 5;
        }

        // Latency impact
        if (device.LatencyMs.HasValue)
        {
            if (device.LatencyMs > 500) score -= 30;
            else if (device.LatencyMs > 200) score -= 15;
            else if (device.LatencyMs > 100) score -= 5;
        }

        // Disconnect frequency impact
        if (disconnectCount24h > 10) score -= 30;
        else if (disconnectCount24h > 5) score -= 15;
        else if (disconnectCount24h > 2) score -= 5;

        return score switch
        {
            >= 85 => ConnectionQuality.Excellent,
            >= 70 => ConnectionQuality.Good,
            >= 50 => ConnectionQuality.Fair,
            _ => ConnectionQuality.Poor
        };
    }

    private static ConnectionQuality CalculateOverallConnectionQuality(List<RegisteredDeviceState> devices)
    {
        if (!devices.Any())
            return ConnectionQuality.Excellent;

        var onlineCount = devices.Count(d => d.IsOnline);
        var onlinePercentage = (double)onlineCount / devices.Count * 100;

        return onlinePercentage switch
        {
            >= 95 => ConnectionQuality.Excellent,
            >= 80 => ConnectionQuality.Good,
            >= 60 => ConnectionQuality.Fair,
            >= 30 => ConnectionQuality.Poor,
            _ => ConnectionQuality.Disconnected
        };
    }

    private void EnsureInitialized()
    {
        if (_state.State.LocationId == Guid.Empty)
            throw new InvalidOperationException("Device status grain not initialized");
    }
}
