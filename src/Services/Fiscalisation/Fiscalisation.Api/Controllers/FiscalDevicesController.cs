using DarkVelocity.Fiscalisation.Api.Data;
using DarkVelocity.Fiscalisation.Api.Dtos;
using DarkVelocity.Fiscalisation.Api.Entities;
using DarkVelocity.Fiscalisation.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Fiscalisation.Api.Controllers;

[ApiController]
[Route("api/fiscal-devices")]
public class FiscalDevicesController : ControllerBase
{
    private readonly FiscalisationDbContext _context;
    private readonly ITseAdapterFactory _tseAdapterFactory;
    private readonly IFiscalJournalService _journalService;

    public FiscalDevicesController(
        FiscalisationDbContext context,
        ITseAdapterFactory tseAdapterFactory,
        IFiscalJournalService journalService)
    {
        _context = context;
        _tseAdapterFactory = tseAdapterFactory;
        _journalService = journalService;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<FiscalDeviceDto>>> GetAll(
        [FromQuery] Guid? locationId = null,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50)
    {
        var query = _context.FiscalDevices.AsQueryable();

        if (locationId.HasValue)
            query = query.Where(d => d.LocationId == locationId.Value);

        if (tenantId.HasValue)
            query = query.Where(d => d.TenantId == tenantId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(d => d.Status == status);

        var devices = await query
            .OrderByDescending(d => d.CreatedAt)
            .Take(limit)
            .ToListAsync();

        var dtos = devices.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/fiscal-devices/{dto.Id}");
            dto.AddLink("status", $"/api/fiscal-devices/{dto.Id}/status");
            dto.AddLink("transactions", $"/api/fiscal-devices/{dto.Id}/transactions");
        }

        return Ok(HalCollection<FiscalDeviceDto>.Create(
            dtos,
            "/api/fiscal-devices",
            dtos.Count));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FiscalDeviceDto>> GetById(Guid id)
    {
        var device = await _context.FiscalDevices.FindAsync(id);

        if (device == null)
            return NotFound();

        var dto = MapToDto(device);
        dto.AddSelfLink($"/api/fiscal-devices/{device.Id}");
        dto.AddLink("status", $"/api/fiscal-devices/{device.Id}/status");
        dto.AddLink("initialize", $"/api/fiscal-devices/{device.Id}/initialize");
        dto.AddLink("self-test", $"/api/fiscal-devices/{device.Id}/self-test");
        dto.AddLink("transactions", $"/api/fiscal-devices/{device.Id}/transactions");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<FiscalDeviceDto>> Register([FromBody] RegisterFiscalDeviceRequest request)
    {
        // Validate device type
        var supportedTypes = _tseAdapterFactory.GetSupportedDeviceTypes();
        if (!supportedTypes.Contains(request.DeviceType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = $"Unsupported device type: {request.DeviceType}. Supported types: {string.Join(", ", supportedTypes)}" });
        }

        // Check for existing device with same serial number
        var existingDevice = await _context.FiscalDevices
            .FirstOrDefaultAsync(d => d.SerialNumber == request.SerialNumber);

        if (existingDevice != null)
        {
            return Conflict(new { message = $"A device with serial number {request.SerialNumber} already exists" });
        }

        var device = new FiscalDevice
        {
            TenantId = request.TenantId,
            LocationId = request.LocationId,
            DeviceType = request.DeviceType,
            SerialNumber = request.SerialNumber,
            ApiEndpoint = request.ApiEndpoint,
            ApiCredentialsEncrypted = request.ApiCredentials, // Should be encrypted in production
            ClientId = request.ClientId,
            Status = "Inactive"
        };

        _context.FiscalDevices.Add(device);
        await _context.SaveChangesAsync();

        await _journalService.LogAsync(
            request.TenantId,
            "DeviceRegistered",
            new { DeviceId = device.Id, SerialNumber = request.SerialNumber, DeviceType = request.DeviceType },
            locationId: request.LocationId,
            deviceId: device.Id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        var dto = MapToDto(device);
        dto.AddSelfLink($"/api/fiscal-devices/{device.Id}");

        return CreatedAtAction(nameof(GetById), new { id = device.Id }, dto);
    }

    [HttpPost("{id:guid}/initialize")]
    public async Task<ActionResult<FiscalDeviceDto>> Initialize(Guid id, [FromBody] InitializeFiscalDeviceRequest request)
    {
        var device = await _context.FiscalDevices.FindAsync(id);

        if (device == null)
            return NotFound();

        if (device.Status == "Active")
            return BadRequest(new { message = "Device is already initialized and active" });

        var adapter = _tseAdapterFactory.GetAdapter(device.DeviceType);

        var result = await adapter.InitializeAsync(
            device.SerialNumber,
            device.ApiEndpoint,
            device.ApiCredentialsEncrypted,
            request.AdminPin);

        if (!result.Success)
        {
            device.Status = "Failed";
            await _context.SaveChangesAsync();

            await _journalService.LogAsync(
                device.TenantId,
                "DeviceInitializationFailed",
                new { DeviceId = id, ErrorMessage = result.ErrorMessage },
                locationId: device.LocationId,
                deviceId: id,
                severity: "Error",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return BadRequest(new { message = result.ErrorMessage });
        }

        device.Status = "Active";
        device.PublicKey = result.PublicKey;
        device.CertificateExpiryDate = result.CertificateExpiryDate;
        device.ClientId = result.ClientId ?? device.ClientId;
        device.LastSyncAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _journalService.LogAsync(
            device.TenantId,
            "DeviceInitialized",
            new { DeviceId = id, ClientId = device.ClientId },
            locationId: device.LocationId,
            deviceId: id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        var dto = MapToDto(device);
        dto.AddSelfLink($"/api/fiscal-devices/{device.Id}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/self-test")]
    public async Task<ActionResult<FiscalDeviceStatusDto>> SelfTest(Guid id)
    {
        var device = await _context.FiscalDevices.FindAsync(id);

        if (device == null)
            return NotFound();

        var adapter = _tseAdapterFactory.GetAdapter(device.DeviceType);
        var result = await adapter.SelfTestAsync();

        if (!result.Success)
        {
            await _journalService.LogAsync(
                device.TenantId,
                "SelfTestFailed",
                new { DeviceId = id, ErrorMessage = result.ErrorMessage },
                locationId: device.LocationId,
                deviceId: id,
                severity: "Warning",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return BadRequest(new { message = result.ErrorMessage });
        }

        device.Status = result.Status;
        device.LastSyncAt = result.LastSyncAt;
        device.TransactionCounter = result.TransactionCounter;
        device.SignatureCounter = result.SignatureCounter;

        await _context.SaveChangesAsync();

        await _journalService.LogAsync(
            device.TenantId,
            "SelfTestPerformed",
            new { DeviceId = id, Status = result.Status },
            locationId: device.LocationId,
            deviceId: id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        var statusDto = new FiscalDeviceStatusDto
        {
            Id = device.Id,
            Status = device.Status,
            SerialNumber = device.SerialNumber,
            LastSyncAt = device.LastSyncAt,
            TransactionCounter = device.TransactionCounter,
            SignatureCounter = device.SignatureCounter,
            IsCertificateValid = device.CertificateExpiryDate > DateTime.UtcNow,
            DaysUntilCertificateExpiry = device.CertificateExpiryDate.HasValue
                ? (int)(device.CertificateExpiryDate.Value - DateTime.UtcNow).TotalDays
                : null
        };

        statusDto.AddSelfLink($"/api/fiscal-devices/{device.Id}/status");

        return Ok(statusDto);
    }

    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult<FiscalDeviceStatusDto>> GetStatus(Guid id)
    {
        var device = await _context.FiscalDevices.FindAsync(id);

        if (device == null)
            return NotFound();

        var statusDto = new FiscalDeviceStatusDto
        {
            Id = device.Id,
            Status = device.Status,
            SerialNumber = device.SerialNumber,
            LastSyncAt = device.LastSyncAt,
            TransactionCounter = device.TransactionCounter,
            SignatureCounter = device.SignatureCounter,
            IsCertificateValid = device.CertificateExpiryDate > DateTime.UtcNow,
            DaysUntilCertificateExpiry = device.CertificateExpiryDate.HasValue
                ? (int)(device.CertificateExpiryDate.Value - DateTime.UtcNow).TotalDays
                : null
        };

        statusDto.AddSelfLink($"/api/fiscal-devices/{device.Id}/status");
        statusDto.AddLink("self-test", $"/api/fiscal-devices/{device.Id}/self-test");

        return Ok(statusDto);
    }

    [HttpPost("{id:guid}/decommission")]
    public async Task<ActionResult<FiscalDeviceDto>> Decommission(Guid id, [FromBody] DecommissionFiscalDeviceRequest request)
    {
        var device = await _context.FiscalDevices.FindAsync(id);

        if (device == null)
            return NotFound();

        if (device.Status == "Inactive")
            return BadRequest(new { message = "Device is already inactive" });

        var adapter = _tseAdapterFactory.GetAdapter(device.DeviceType);
        var result = await adapter.DecommissionAsync(device.ClientId ?? "");

        if (!result.Success)
        {
            await _journalService.LogAsync(
                device.TenantId,
                "DecommissionFailed",
                new { DeviceId = id, ErrorMessage = result.ErrorMessage },
                locationId: device.LocationId,
                deviceId: id,
                userId: request.UserId,
                severity: "Error",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return BadRequest(new { message = result.ErrorMessage });
        }

        device.Status = "Inactive";
        await _context.SaveChangesAsync();

        await _journalService.LogAsync(
            device.TenantId,
            "DeviceDecommissioned",
            new { DeviceId = id, Reason = request.Reason },
            locationId: device.LocationId,
            deviceId: id,
            userId: request.UserId,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        var dto = MapToDto(device);
        dto.AddSelfLink($"/api/fiscal-devices/{device.Id}");

        return Ok(dto);
    }

    private static FiscalDeviceDto MapToDto(FiscalDevice device)
    {
        return new FiscalDeviceDto
        {
            Id = device.Id,
            TenantId = device.TenantId,
            LocationId = device.LocationId,
            DeviceType = device.DeviceType,
            SerialNumber = device.SerialNumber,
            PublicKey = device.PublicKey,
            CertificateExpiryDate = device.CertificateExpiryDate,
            Status = device.Status,
            ApiEndpoint = device.ApiEndpoint,
            LastSyncAt = device.LastSyncAt,
            TransactionCounter = device.TransactionCounter,
            SignatureCounter = device.SignatureCounter,
            ClientId = device.ClientId,
            CreatedAt = device.CreatedAt
        };
    }
}
