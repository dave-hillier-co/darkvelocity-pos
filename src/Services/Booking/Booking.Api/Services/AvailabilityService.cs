using DarkVelocity.Booking.Api.Data;
using DarkVelocity.Booking.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Booking.Api.Services;

public interface IAvailabilityService
{
    Task<List<AvailableSlot>> GetAvailableSlotsAsync(
        Guid locationId,
        DateOnly date,
        int partySize,
        Guid? floorPlanId = null);

    Task<List<Table>> GetAvailableTablesAsync(
        Guid locationId,
        DateOnly date,
        TimeOnly startTime,
        int durationMinutes,
        int partySize,
        Guid? floorPlanId = null);

    Task<bool> IsTableAvailableAsync(
        Guid tableId,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        Guid? excludeBookingId = null);

    Task<DepositPolicy?> GetApplicableDepositPolicyAsync(
        Guid locationId,
        int partySize,
        DateOnly date,
        TimeOnly time);

    Task<decimal> CalculateDepositAmountAsync(
        DepositPolicy policy,
        int partySize);
}

public class AvailabilityService : IAvailabilityService
{
    private readonly BookingDbContext _context;

    public AvailabilityService(BookingDbContext context)
    {
        _context = context;
    }

    public async Task<List<AvailableSlot>> GetAvailableSlotsAsync(
        Guid locationId,
        DateOnly date,
        int partySize,
        Guid? floorPlanId = null)
    {
        var dayOfWeek = (int)date.DayOfWeek;

        // Check for date override
        var dateOverride = await _context.DateOverrides
            .FirstOrDefaultAsync(d => d.LocationId == locationId && d.Date == date);

        if (dateOverride?.OverrideType == "closed")
        {
            return new List<AvailableSlot>();
        }

        // Get time slots for this day
        var timeSlotsQuery = _context.TimeSlots
            .Where(t => t.LocationId == locationId &&
                       t.DayOfWeek == dayOfWeek &&
                       t.IsActive);

        if (floorPlanId.HasValue)
        {
            timeSlotsQuery = timeSlotsQuery.Where(t =>
                t.FloorPlanId == null || t.FloorPlanId == floorPlanId.Value);
        }

        var timeSlots = await timeSlotsQuery.OrderBy(t => t.StartTime).ToListAsync();

        if (!timeSlots.Any())
        {
            return new List<AvailableSlot>();
        }

        // Get settings
        var settings = await _context.BookingSettings
            .FirstOrDefaultAsync(s => s.LocationId == locationId);

        var defaultDuration = settings?.DefaultDurationMinutes ?? 90;
        var bufferMinutes = settings?.TableTurnBufferMinutes ?? 15;

        // Get existing bookings for this date
        var existingBookings = await _context.Bookings
            .Where(b => b.LocationId == locationId &&
                       b.BookingDate == date &&
                       b.Status != "cancelled" &&
                       b.Status != "no_show")
            .ToListAsync();

        // Get available tables
        var tablesQuery = _context.Tables
            .Where(t => t.LocationId == locationId &&
                       t.IsActive &&
                       t.MaxCapacity >= partySize &&
                       t.MinCapacity <= partySize);

        if (floorPlanId.HasValue)
        {
            tablesQuery = tablesQuery.Where(t => t.FloorPlanId == floorPlanId.Value);
        }

        var tables = await tablesQuery.ToListAsync();

        // Also check table combinations for larger parties
        var combinations = await _context.TableCombinations
            .Include(c => c.Tables)
            .ThenInclude(ct => ct.Table)
            .Where(c => c.LocationId == locationId &&
                       c.IsActive &&
                       c.CombinedCapacity >= partySize &&
                       c.MinPartySize <= partySize)
            .ToListAsync();

        var availableSlots = new List<AvailableSlot>();

        foreach (var slot in timeSlots)
        {
            var currentTime = slot.StartTime;

            while (currentTime.Add(TimeSpan.FromMinutes(defaultDuration)) <= slot.EndTime)
            {
                var slotEndTime = currentTime.Add(TimeSpan.FromMinutes(defaultDuration));

                // Check if any table is available
                var hasAvailableTable = tables.Any(t =>
                    !existingBookings.Any(b =>
                        b.TableId == t.Id &&
                        TimesOverlap(currentTime, slotEndTime, b.StartTime, b.EndTime, bufferMinutes)));

                // Check combinations too
                if (!hasAvailableTable)
                {
                    hasAvailableTable = combinations.Any(c =>
                        !existingBookings.Any(b =>
                            b.TableCombinationId == c.Id &&
                            TimesOverlap(currentTime, slotEndTime, b.StartTime, b.EndTime, bufferMinutes)));
                }

                if (hasAvailableTable)
                {
                    // Count available slots
                    var availableTableCount = tables.Count(t =>
                        !existingBookings.Any(b =>
                            b.TableId == t.Id &&
                            TimesOverlap(currentTime, slotEndTime, b.StartTime, b.EndTime, bufferMinutes)));

                    availableSlots.Add(new AvailableSlot
                    {
                        Time = currentTime,
                        AvailableTableCount = availableTableCount,
                        SlotName = slot.Name
                    });
                }

                currentTime = currentTime.Add(TimeSpan.FromMinutes(slot.IntervalMinutes));
            }
        }

        return availableSlots;
    }

    public async Task<List<Table>> GetAvailableTablesAsync(
        Guid locationId,
        DateOnly date,
        TimeOnly startTime,
        int durationMinutes,
        int partySize,
        Guid? floorPlanId = null)
    {
        var endTime = startTime.Add(TimeSpan.FromMinutes(durationMinutes));

        var settings = await _context.BookingSettings
            .FirstOrDefaultAsync(s => s.LocationId == locationId);

        var bufferMinutes = settings?.TableTurnBufferMinutes ?? 15;

        // Get existing bookings
        var existingBookings = await _context.Bookings
            .Where(b => b.LocationId == locationId &&
                       b.BookingDate == date &&
                       b.Status != "cancelled" &&
                       b.Status != "no_show")
            .ToListAsync();

        // Get suitable tables
        var tablesQuery = _context.Tables
            .Include(t => t.FloorPlan)
            .Where(t => t.LocationId == locationId &&
                       t.IsActive &&
                       t.MaxCapacity >= partySize &&
                       t.MinCapacity <= partySize);

        if (floorPlanId.HasValue)
        {
            tablesQuery = tablesQuery.Where(t => t.FloorPlanId == floorPlanId.Value);
        }

        var tables = await tablesQuery
            .OrderBy(t => t.AssignmentPriority)
            .ThenBy(t => t.MaxCapacity) // Prefer smaller tables that fit
            .ToListAsync();

        return tables.Where(t =>
            !existingBookings.Any(b =>
                b.TableId == t.Id &&
                TimesOverlap(startTime, endTime, b.StartTime, b.EndTime, bufferMinutes)))
            .ToList();
    }

    public async Task<bool> IsTableAvailableAsync(
        Guid tableId,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        Guid? excludeBookingId = null)
    {
        var table = await _context.Tables.FindAsync(tableId);
        if (table == null || !table.IsActive)
            return false;

        var settings = await _context.BookingSettings
            .FirstOrDefaultAsync(s => s.LocationId == table.LocationId);

        var bufferMinutes = settings?.TableTurnBufferMinutes ?? 15;

        var conflictingBooking = await _context.Bookings
            .Where(b => b.TableId == tableId &&
                       b.BookingDate == date &&
                       b.Status != "cancelled" &&
                       b.Status != "no_show" &&
                       (excludeBookingId == null || b.Id != excludeBookingId))
            .FirstOrDefaultAsync(b =>
                TimesOverlap(startTime, endTime, b.StartTime, b.EndTime, bufferMinutes));

        return conflictingBooking == null;
    }

    public async Task<DepositPolicy?> GetApplicableDepositPolicyAsync(
        Guid locationId,
        int partySize,
        DateOnly date,
        TimeOnly time)
    {
        var dayName = date.DayOfWeek.ToString();

        var policies = await _context.DepositPolicies
            .Where(p => p.LocationId == locationId &&
                       p.IsActive &&
                       p.MinPartySize <= partySize &&
                       (p.MaxPartySize == null || p.MaxPartySize >= partySize))
            .OrderByDescending(p => p.Priority)
            .ToListAsync();

        foreach (var policy in policies)
        {
            // Check day restriction
            if (!string.IsNullOrEmpty(policy.ApplicableDays))
            {
                var days = policy.ApplicableDays.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (!days.Contains(dayName, StringComparer.OrdinalIgnoreCase))
                    continue;
            }

            // Check time restriction
            if (policy.ApplicableFromTime.HasValue && time < policy.ApplicableFromTime.Value)
                continue;

            if (policy.ApplicableToTime.HasValue && time > policy.ApplicableToTime.Value)
                continue;

            return policy;
        }

        return null;
    }

    public Task<decimal> CalculateDepositAmountAsync(DepositPolicy policy, int partySize)
    {
        decimal amount = policy.DepositType switch
        {
            "per_person" => (policy.AmountPerPerson ?? 0) * partySize,
            "flat_rate" => policy.FlatAmount ?? 0,
            "percentage" => 0, // Would need minimum spend info
            _ => 0
        };

        // Apply min/max constraints
        if (policy.MinimumAmount.HasValue && amount < policy.MinimumAmount.Value)
            amount = policy.MinimumAmount.Value;

        if (policy.MaximumAmount.HasValue && amount > policy.MaximumAmount.Value)
            amount = policy.MaximumAmount.Value;

        return Task.FromResult(amount);
    }

    private static bool TimesOverlap(
        TimeOnly start1, TimeOnly end1,
        TimeOnly start2, TimeOnly end2,
        int bufferMinutes)
    {
        // Add buffer to the existing booking
        var bufferedStart2 = start2.Add(TimeSpan.FromMinutes(-bufferMinutes));
        var bufferedEnd2 = end2.Add(TimeSpan.FromMinutes(bufferMinutes));

        return start1 < bufferedEnd2 && end1 > bufferedStart2;
    }
}

public class AvailableSlot
{
    public TimeOnly Time { get; set; }
    public int AvailableTableCount { get; set; }
    public string? SlotName { get; set; }
}
