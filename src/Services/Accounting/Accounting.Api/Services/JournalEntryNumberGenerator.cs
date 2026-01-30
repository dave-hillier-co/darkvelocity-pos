using DarkVelocity.Accounting.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Accounting.Api.Services;

public interface IJournalEntryNumberGenerator
{
    Task<string> GenerateAsync(Guid tenantId, Guid locationId);
}

public class JournalEntryNumberGenerator : IJournalEntryNumberGenerator
{
    private readonly AccountingDbContext _context;

    public JournalEntryNumberGenerator(AccountingDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateAsync(Guid tenantId, Guid locationId)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"JE-{year}-";

        var lastEntry = await _context.JournalEntries
            .Where(e => e.TenantId == tenantId &&
                        e.LocationId == locationId &&
                        e.EntryNumber.StartsWith(prefix))
            .OrderByDescending(e => e.EntryNumber)
            .FirstOrDefaultAsync();

        int nextNumber = 1;
        if (lastEntry != null)
        {
            var lastNumberStr = lastEntry.EntryNumber.Substring(prefix.Length);
            if (int.TryParse(lastNumberStr, out int lastNumber))
            {
                nextNumber = lastNumber + 1;
            }
        }

        return $"{prefix}{nextNumber:D5}";
    }
}
