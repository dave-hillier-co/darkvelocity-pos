using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class AvailabilityEndpoints
{
    public static WebApplication MapAvailabilityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/availability").WithTags("Availability");

        group.MapGet("/", async (
            Guid orgId, Guid siteId,
            [FromQuery] DateOnly date,
            [FromQuery] int partySize,
            [FromQuery] TimeOnly? preferredTime,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBookingSettingsGrain>(GrainKeys.BookingSettings(orgId, siteId));
            if (!await grain.ExistsAsync())
                await grain.InitializeAsync(orgId, siteId);

            var slots = await grain.GetAvailabilityAsync(new GetAvailabilityQuery(date, partySize, preferredTime));
            var items = slots.Select(s => new
            {
                time = s.Time.ToString("HH:mm"),
                isAvailable = s.IsAvailable,
                availableCapacity = s.AvailableCapacity
            }).ToList();

            return Results.Ok(new
            {
                date = date.ToString("yyyy-MM-dd"),
                partySize,
                _links = new { self = new { href = $"/api/orgs/{orgId}/sites/{siteId}/availability?date={date:yyyy-MM-dd}&partySize={partySize}" } },
                slots = items
            });
        });

        group.MapGet("/check", async (
            Guid orgId, Guid siteId,
            [FromQuery] DateOnly date,
            [FromQuery] TimeOnly time,
            [FromQuery] int partySize,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBookingSettingsGrain>(GrainKeys.BookingSettings(orgId, siteId));
            if (!await grain.ExistsAsync())
                await grain.InitializeAsync(orgId, siteId);

            var isAvailable = await grain.IsSlotAvailableAsync(date, time, partySize);
            return Results.Ok(new { date = date.ToString("yyyy-MM-dd"), time = time.ToString("HH:mm"), partySize, isAvailable });
        });

        // Booking settings endpoints
        app.MapGet("/api/orgs/{orgId}/sites/{siteId}/booking-settings", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBookingSettingsGrain>(GrainKeys.BookingSettings(orgId, siteId));
            if (!await grain.ExistsAsync())
                await grain.InitializeAsync(orgId, siteId);

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/booking-settings" },
                ["availability"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/availability" }
            }));
        }).WithTags("Availability");

        app.MapPatch("/api/orgs/{orgId}/sites/{siteId}/booking-settings", async (
            Guid orgId, Guid siteId,
            [FromBody] UpdateBookingSettingsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBookingSettingsGrain>(GrainKeys.BookingSettings(orgId, siteId));
            if (!await grain.ExistsAsync())
                await grain.InitializeAsync(orgId, siteId);

            await grain.UpdateAsync(new UpdateBookingSettingsCommand(
                request.DefaultOpenTime, request.DefaultCloseTime, request.DefaultDuration,
                request.SlotInterval, request.MaxPartySizeOnline, request.MaxBookingsPerSlot,
                request.AdvanceBookingDays, request.RequireDeposit, request.DepositAmount));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/booking-settings" }
            }));
        }).WithTags("Availability");

        // Booking calendar - list bookings by date
        app.MapGet("/api/orgs/{orgId}/sites/{siteId}/bookings", async (
            Guid orgId, Guid siteId,
            [FromQuery] DateOnly? date,
            [FromQuery] string? status,
            IGrainFactory grainFactory) =>
        {
            var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var grain = grainFactory.GetGrain<IBookingCalendarGrain>(GrainKeys.BookingCalendar(orgId, siteId, targetDate));

            if (!await grain.ExistsAsync())
                return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/bookings?date={targetDate:yyyy-MM-dd}", new List<object>(), 0));

            BookingStatus? statusFilter = status != null ? Enum.Parse<BookingStatus>(status, true) : null;
            var bookings = await grain.GetBookingsAsync(statusFilter);
            var items = bookings.Select(b => Hal.Resource(b, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{b.BookingId}" }
            })).ToList();

            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/bookings?date={targetDate:yyyy-MM-dd}", items, items.Count));
        }).WithTags("Bookings");

        return app;
    }
}
