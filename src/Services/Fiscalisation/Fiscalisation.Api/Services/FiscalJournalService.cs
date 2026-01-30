using System.Text.Json;
using DarkVelocity.Fiscalisation.Api.Data;
using DarkVelocity.Fiscalisation.Api.Entities;

namespace DarkVelocity.Fiscalisation.Api.Services;

/// <summary>
/// Service for creating audit log entries in the fiscal journal.
/// </summary>
public interface IFiscalJournalService
{
    Task LogAsync(
        Guid tenantId,
        string eventType,
        object details,
        Guid? locationId = null,
        Guid? deviceId = null,
        Guid? transactionId = null,
        Guid? exportId = null,
        Guid? userId = null,
        string? ipAddress = null,
        string severity = "Info",
        CancellationToken cancellationToken = default);
}

public class FiscalJournalService : IFiscalJournalService
{
    private readonly FiscalisationDbContext _context;

    public FiscalJournalService(FiscalisationDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(
        Guid tenantId,
        string eventType,
        object details,
        Guid? locationId = null,
        Guid? deviceId = null,
        Guid? transactionId = null,
        Guid? exportId = null,
        Guid? userId = null,
        string? ipAddress = null,
        string severity = "Info",
        CancellationToken cancellationToken = default)
    {
        var journalEntry = new FiscalJournal
        {
            TenantId = tenantId,
            LocationId = locationId,
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            DeviceId = deviceId,
            TransactionId = transactionId,
            ExportId = exportId,
            Details = JsonSerializer.Serialize(details),
            IpAddress = ipAddress,
            UserId = userId,
            Severity = severity
        };

        _context.FiscalJournals.Add(journalEntry);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
