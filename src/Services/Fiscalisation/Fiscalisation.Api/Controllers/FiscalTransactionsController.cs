using System.Text.Json;
using DarkVelocity.Fiscalisation.Api.Data;
using DarkVelocity.Fiscalisation.Api.Dtos;
using DarkVelocity.Fiscalisation.Api.Entities;
using DarkVelocity.Fiscalisation.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Fiscalisation.Api.Controllers;

[ApiController]
[Route("api/fiscal-transactions")]
public class FiscalTransactionsController : ControllerBase
{
    private readonly FiscalisationDbContext _context;
    private readonly ITseAdapterFactory _tseAdapterFactory;
    private readonly IFiscalJournalService _journalService;

    public FiscalTransactionsController(
        FiscalisationDbContext context,
        ITseAdapterFactory tseAdapterFactory,
        IFiscalJournalService journalService)
    {
        _context = context;
        _tseAdapterFactory = tseAdapterFactory;
        _journalService = journalService;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<FiscalTransactionDto>>> GetAll(
        [FromQuery] Guid? locationId = null,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] Guid? fiscalDeviceId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int limit = 50)
    {
        var query = _context.FiscalTransactions.AsQueryable();

        if (locationId.HasValue)
            query = query.Where(t => t.LocationId == locationId.Value);

        if (tenantId.HasValue)
            query = query.Where(t => t.TenantId == tenantId.Value);

        if (fiscalDeviceId.HasValue)
            query = query.Where(t => t.FiscalDeviceId == fiscalDeviceId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        if (from.HasValue)
            query = query.Where(t => t.StartTime >= from.Value);

        if (to.HasValue)
            query = query.Where(t => t.EndTime <= to.Value);

        var transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync();

        var dtos = transactions.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/fiscal-transactions/{dto.Id}");
            dto.AddLink("qr", $"/api/fiscal-transactions/{dto.Id}/qr");
        }

        return Ok(HalCollection<FiscalTransactionDto>.Create(
            dtos,
            "/api/fiscal-transactions",
            dtos.Count));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FiscalTransactionDto>> GetById(Guid id)
    {
        var transaction = await _context.FiscalTransactions.FindAsync(id);

        if (transaction == null)
            return NotFound();

        var dto = MapToDto(transaction);
        dto.AddSelfLink($"/api/fiscal-transactions/{transaction.Id}");
        dto.AddLink("qr", $"/api/fiscal-transactions/{transaction.Id}/qr");
        dto.AddLink("device", $"/api/fiscal-devices/{transaction.FiscalDeviceId}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<FiscalTransactionDto>> Create([FromBody] CreateFiscalTransactionRequest request)
    {
        // Find an active fiscal device for this location
        var device = await _context.FiscalDevices
            .FirstOrDefaultAsync(d =>
                d.LocationId == request.LocationId &&
                d.TenantId == request.TenantId &&
                d.Status == "Active");

        if (device == null)
        {
            return BadRequest(new { message = "No active fiscal device found for this location" });
        }

        var adapter = _tseAdapterFactory.GetAdapter(device.DeviceType);

        // Build process data for the TSE
        var processData = BuildProcessData(request);

        // Start transaction
        var startResult = await adapter.StartTransactionAsync(
            device.ClientId ?? "",
            request.ProcessType);

        if (!startResult.Success)
        {
            await _journalService.LogAsync(
                request.TenantId,
                "TransactionStartFailed",
                new { DeviceId = device.Id, ErrorMessage = startResult.ErrorMessage },
                locationId: request.LocationId,
                deviceId: device.Id,
                severity: "Error",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return BadRequest(new { message = startResult.ErrorMessage });
        }

        // Finish and sign transaction
        var signResult = await adapter.FinishTransactionAsync(
            device.ClientId ?? "",
            request.ProcessType,
            processData);

        var transaction = new FiscalTransaction
        {
            FiscalDeviceId = device.Id,
            LocationId = request.LocationId,
            TenantId = request.TenantId,
            TransactionNumber = signResult.TransactionNumber,
            TransactionType = request.TransactionType,
            ProcessType = request.ProcessType,
            StartTime = signResult.StartTime,
            EndTime = signResult.EndTime,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            GrossAmount = request.GrossAmount,
            NetAmounts = JsonSerializer.Serialize(request.NetAmounts),
            TaxAmounts = JsonSerializer.Serialize(request.TaxAmounts),
            PaymentTypes = JsonSerializer.Serialize(request.PaymentTypes),
            Signature = signResult.Signature,
            SignatureCounter = signResult.SignatureCounter,
            CertificateSerial = signResult.CertificateSerial,
            QrCodeData = signResult.QrCodeData,
            TseResponseRaw = signResult.RawResponse,
            Status = signResult.Success ? "Signed" : "Failed",
            ErrorMessage = signResult.ErrorMessage
        };

        _context.FiscalTransactions.Add(transaction);

        // Update device counters
        device.TransactionCounter = signResult.TransactionNumber;
        device.SignatureCounter = signResult.SignatureCounter;
        device.LastSyncAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _journalService.LogAsync(
            request.TenantId,
            signResult.Success ? "TransactionSigned" : "TransactionSigningFailed",
            new
            {
                TransactionId = transaction.Id,
                DeviceId = device.Id,
                TransactionNumber = signResult.TransactionNumber,
                GrossAmount = request.GrossAmount,
                ErrorMessage = signResult.ErrorMessage
            },
            locationId: request.LocationId,
            deviceId: device.Id,
            transactionId: transaction.Id,
            severity: signResult.Success ? "Info" : "Error",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        if (!signResult.Success)
        {
            return BadRequest(new { message = signResult.ErrorMessage, transactionId = transaction.Id });
        }

        var dto = MapToDto(transaction);
        dto.AddSelfLink($"/api/fiscal-transactions/{transaction.Id}");

        return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, dto);
    }

    [HttpGet("{id:guid}/qr")]
    public async Task<ActionResult<FiscalTransactionQrDto>> GetQrCode(Guid id)
    {
        var transaction = await _context.FiscalTransactions.FindAsync(id);

        if (transaction == null)
            return NotFound();

        if (string.IsNullOrEmpty(transaction.QrCodeData))
            return BadRequest(new { message = "No QR code data available for this transaction" });

        var dto = new FiscalTransactionQrDto
        {
            TransactionId = transaction.Id,
            QrCodeData = transaction.QrCodeData,
            // In production, generate actual QR code image here
            QrCodeBase64Image = null
        };

        dto.AddSelfLink($"/api/fiscal-transactions/{transaction.Id}/qr");
        dto.AddLink("transaction", $"/api/fiscal-transactions/{transaction.Id}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/void")]
    public async Task<ActionResult<FiscalTransactionDto>> Void(Guid id, [FromBody] VoidFiscalTransactionRequest request)
    {
        var originalTransaction = await _context.FiscalTransactions
            .Include(t => t.FiscalDevice)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (originalTransaction == null)
            return NotFound();

        if (originalTransaction.TransactionType == "Void")
            return BadRequest(new { message = "This transaction is already a void transaction" });

        if (originalTransaction.FiscalDevice == null || originalTransaction.FiscalDevice.Status != "Active")
            return BadRequest(new { message = "Fiscal device is not active" });

        var device = originalTransaction.FiscalDevice;
        var adapter = _tseAdapterFactory.GetAdapter(device.DeviceType);

        // Create a void transaction that reverses the original
        var voidProcessData = $"VOID:{originalTransaction.Id}|{request.Reason}";

        var startResult = await adapter.StartTransactionAsync(
            device.ClientId ?? "",
            "Kassenbeleg");

        if (!startResult.Success)
        {
            return BadRequest(new { message = startResult.ErrorMessage });
        }

        var signResult = await adapter.FinishTransactionAsync(
            device.ClientId ?? "",
            "Kassenbeleg",
            voidProcessData);

        var voidTransaction = new FiscalTransaction
        {
            FiscalDeviceId = device.Id,
            LocationId = originalTransaction.LocationId,
            TenantId = originalTransaction.TenantId,
            TransactionNumber = signResult.TransactionNumber,
            TransactionType = "Void",
            ProcessType = "Kassenbeleg",
            StartTime = signResult.StartTime,
            EndTime = signResult.EndTime,
            SourceType = originalTransaction.SourceType,
            SourceId = originalTransaction.SourceId,
            GrossAmount = -originalTransaction.GrossAmount,
            NetAmounts = originalTransaction.NetAmounts,
            TaxAmounts = originalTransaction.TaxAmounts,
            PaymentTypes = originalTransaction.PaymentTypes,
            Signature = signResult.Signature,
            SignatureCounter = signResult.SignatureCounter,
            CertificateSerial = signResult.CertificateSerial,
            QrCodeData = signResult.QrCodeData,
            TseResponseRaw = signResult.RawResponse,
            Status = signResult.Success ? "Signed" : "Failed",
            ErrorMessage = signResult.ErrorMessage
        };

        _context.FiscalTransactions.Add(voidTransaction);

        device.TransactionCounter = signResult.TransactionNumber;
        device.SignatureCounter = signResult.SignatureCounter;
        device.LastSyncAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _journalService.LogAsync(
            voidTransaction.TenantId,
            "TransactionVoided",
            new
            {
                OriginalTransactionId = originalTransaction.Id,
                VoidTransactionId = voidTransaction.Id,
                Reason = request.Reason
            },
            locationId: voidTransaction.LocationId,
            deviceId: device.Id,
            transactionId: voidTransaction.Id,
            userId: request.UserId,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        var dto = MapToDto(voidTransaction);
        dto.AddSelfLink($"/api/fiscal-transactions/{voidTransaction.Id}");

        return CreatedAtAction(nameof(GetById), new { id = voidTransaction.Id }, dto);
    }

    [HttpGet("by-location/{locationId:guid}")]
    public async Task<ActionResult<HalCollection<FiscalTransactionDto>>> GetByLocation(
        Guid locationId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int limit = 50)
    {
        var query = _context.FiscalTransactions
            .Where(t => t.LocationId == locationId);

        if (from.HasValue)
            query = query.Where(t => t.StartTime >= from.Value);

        if (to.HasValue)
            query = query.Where(t => t.EndTime <= to.Value);

        var transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync();

        var dtos = transactions.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/fiscal-transactions/{dto.Id}");
        }

        return Ok(HalCollection<FiscalTransactionDto>.Create(
            dtos,
            $"/api/fiscal-transactions/by-location/{locationId}",
            dtos.Count));
    }

    private static string BuildProcessData(CreateFiscalTransactionRequest request)
    {
        // Build process data string for KassenSichV
        // Format: Amount per tax rate
        var parts = new List<string>();

        foreach (var (fiscalCode, amount) in request.NetAmounts)
        {
            parts.Add($"{fiscalCode}:{amount:F2}");
        }

        return string.Join(";", parts);
    }

    private static FiscalTransactionDto MapToDto(FiscalTransaction transaction)
    {
        return new FiscalTransactionDto
        {
            Id = transaction.Id,
            FiscalDeviceId = transaction.FiscalDeviceId,
            LocationId = transaction.LocationId,
            TenantId = transaction.TenantId,
            TransactionNumber = transaction.TransactionNumber,
            TransactionType = transaction.TransactionType,
            ProcessType = transaction.ProcessType,
            StartTime = transaction.StartTime,
            EndTime = transaction.EndTime,
            SourceType = transaction.SourceType,
            SourceId = transaction.SourceId,
            GrossAmount = transaction.GrossAmount,
            NetAmounts = transaction.NetAmounts,
            TaxAmounts = transaction.TaxAmounts,
            PaymentTypes = transaction.PaymentTypes,
            Signature = transaction.Signature,
            SignatureCounter = transaction.SignatureCounter,
            CertificateSerial = transaction.CertificateSerial,
            QrCodeData = transaction.QrCodeData,
            Status = transaction.Status,
            ErrorMessage = transaction.ErrorMessage,
            RetryCount = transaction.RetryCount,
            ExportedAt = transaction.ExportedAt,
            CreatedAt = transaction.CreatedAt
        };
    }
}
