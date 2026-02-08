using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Endpoints;

public static class FloorPlanLiveEndpoints
{
    public static WebApplication MapFloorPlanLiveEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/floor-plans").WithTags("FloorPlans");

        // Composite live floor plan view - the "host stand" screen
        group.MapGet("/{floorPlanId}/live", async (
            Guid orgId, Guid siteId, Guid floorPlanId,
            IGrainFactory grainFactory) =>
        {
            var floorPlanGrain = grainFactory.GetGrain<IFloorPlanGrain>(
                GrainKeys.FloorPlan(orgId, siteId, floorPlanId));
            if (!await floorPlanGrain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Floor plan not found"));

            var floorPlanState = await floorPlanGrain.GetStateAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var now = TimeOnly.FromDateTime(DateTime.UtcNow);

            // Fan out to get all table states in parallel
            var tableTasks = floorPlanState.TableIds.Select(async tableId =>
            {
                var tableGrain = grainFactory.GetGrain<ITableGrain>(
                    GrainKeys.Table(orgId, siteId, tableId));
                if (!await tableGrain.ExistsAsync())
                    return null;
                return await tableGrain.GetStateAsync();
            });
            var tableStates = (await Task.WhenAll(tableTasks))
                .Where(t => t != null)
                .ToList();

            // Get today's calendar for upcoming bookings
            var calendarGrain = grainFactory.GetGrain<IBookingCalendarGrain>(
                GrainKeys.BookingCalendar(orgId, siteId, today));

            IReadOnlyList<BookingReference> upcomingArrivals = [];
            IReadOnlyList<TableAllocation> tableAllocations = [];

            if (await calendarGrain.ExistsAsync())
            {
                // Get bookings arriving in next 30 minutes
                var windowEnd = now.Add(TimeSpan.FromMinutes(30));
                var upcomingBookings = await calendarGrain.GetBookingsByTimeRangeAsync(now, windowEnd);
                upcomingArrivals = upcomingBookings
                    .Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Arrived)
                    .ToList();

                // Get table allocations for today
                tableAllocations = await calendarGrain.GetTableAllocationsAsync();
            }

            // Get waitlist entries for today
            var waitlistGrain = grainFactory.GetGrain<IWaitlistGrain>(
                GrainKeys.Waitlist(orgId, siteId, today));
            IReadOnlyList<WaitlistEntry> waitlistEntries = [];
            if (await waitlistGrain.ExistsAsync())
            {
                waitlistEntries = await waitlistGrain.GetEntriesAsync();
            }

            // Build per-table views
            var tableViews = tableStates.Select(table =>
            {
                // Find the next booking for this table from today's allocations
                var tableAllocation = tableAllocations.FirstOrDefault(a => a.TableId == table!.Id);
                BookingReference? nextBooking = null;
                if (tableAllocation != null)
                {
                    nextBooking = tableAllocation.Bookings
                        .Where(b => b.Time >= now && b.Status == BookingStatus.Confirmed)
                        .OrderBy(b => b.Time)
                        .FirstOrDefault();
                }

                return new
                {
                    id = table!.Id,
                    number = table.Number,
                    name = table.Name,
                    status = table.Status.ToString().ToLowerInvariant(),
                    minCapacity = table.MinCapacity,
                    maxCapacity = table.MaxCapacity,
                    shape = table.Shape.ToString().ToLowerInvariant(),
                    position = table.Position,
                    sectionId = table.SectionId,
                    isCombinable = table.IsCombinable,
                    combinedWith = table.CombinedWith,
                    currentOccupancy = table.CurrentOccupancy != null ? new
                    {
                        bookingId = table.CurrentOccupancy.BookingId,
                        orderId = table.CurrentOccupancy.OrderId,
                        guestName = table.CurrentOccupancy.GuestName,
                        guestCount = table.CurrentOccupancy.GuestCount,
                        seatedAt = table.CurrentOccupancy.SeatedAt,
                        serverId = table.CurrentOccupancy.ServerId,
                        elapsedMinutes = (int)(DateTime.UtcNow - table.CurrentOccupancy.SeatedAt).TotalMinutes
                    } : (object?)null,
                    nextBooking = nextBooking != null ? new
                    {
                        bookingId = nextBooking.BookingId,
                        time = nextBooking.Time.ToString("HH:mm"),
                        guestName = nextBooking.GuestName,
                        partySize = nextBooking.PartySize,
                        confirmationCode = nextBooking.ConfirmationCode
                    } : (object?)null
                };
            }).ToList();

            // Build section summaries
            var sectionSummaries = floorPlanState.Sections.Select(section =>
            {
                var sectionTables = tableStates
                    .Where(t => t!.SectionId == section.Id)
                    .ToList();
                return new
                {
                    id = section.Id,
                    name = section.Name,
                    color = section.Color,
                    tableCount = sectionTables.Count,
                    occupiedCount = sectionTables.Count(t => t!.Status == TableStatus.Occupied),
                    availableCount = sectionTables.Count(t => t!.Status == TableStatus.Available),
                    totalCapacity = sectionTables.Sum(t => t!.MaxCapacity),
                    currentCovers = sectionTables
                        .Where(t => t!.CurrentOccupancy != null)
                        .Sum(t => t!.CurrentOccupancy!.GuestCount)
                };
            }).ToList();

            // Build summary stats
            var summary = new
            {
                totalTables = tableStates.Count,
                availableTables = tableStates.Count(t => t!.Status == TableStatus.Available),
                occupiedTables = tableStates.Count(t => t!.Status == TableStatus.Occupied),
                reservedTables = tableStates.Count(t => t!.Status == TableStatus.Reserved),
                dirtyTables = tableStates.Count(t => t!.Status == TableStatus.Dirty),
                blockedTables = tableStates.Count(t => t!.Status == TableStatus.Blocked),
                totalCapacity = tableStates.Sum(t => t!.MaxCapacity),
                currentCovers = tableStates
                    .Where(t => t!.CurrentOccupancy != null)
                    .Sum(t => t!.CurrentOccupancy!.GuestCount),
                upcomingArrivalCount = upcomingArrivals.Count,
                waitlistCount = waitlistEntries.Count
            };

            var response = new
            {
                floorPlanId,
                name = floorPlanState.Name,
                width = floorPlanState.Width,
                height = floorPlanState.Height,
                backgroundImageUrl = floorPlanState.BackgroundImageUrl,
                asOf = DateTime.UtcNow,
                summary,
                tables = tableViews,
                sections = sectionSummaries,
                upcomingArrivals = upcomingArrivals.Select(b => new
                {
                    bookingId = b.BookingId,
                    time = b.Time.ToString("HH:mm"),
                    guestName = b.GuestName,
                    partySize = b.PartySize,
                    status = b.Status.ToString().ToLowerInvariant(),
                    confirmationCode = b.ConfirmationCode,
                    tableId = b.TableId,
                    tableNumber = b.TableNumber
                }),
                waitlist = waitlistEntries.Select(w => new
                {
                    id = w.Id,
                    guestName = w.Guest.Name,
                    partySize = w.PartySize,
                    quotedWait = w.QuotedWait,
                    checkedInAt = w.CheckedInAt,
                    status = w.Status.ToString().ToLowerInvariant(),
                    waitingMinutes = (int)(DateTime.UtcNow - w.CheckedInAt).TotalMinutes
                })
            };

            var basePath = $"/api/orgs/{orgId}/sites/{siteId}/floor-plans/{floorPlanId}";
            var links = new Dictionary<string, object>
            {
                ["self"] = new { href = $"{basePath}/live" },
                ["floor-plan"] = new { href = basePath },
                ["tables"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables" },
                ["bookings"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings" },
                ["waitlist"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/waitlist" }
            };

            return Results.Ok(Hal.Resource(response, links));
        });

        return app;
    }
}
