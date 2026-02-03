using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class BookingEndpoints
{
    public static WebApplication MapBookingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/bookings").WithTags("Bookings");

        group.MapPost("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] RequestBookingRequest request,
            IGrainFactory grainFactory) =>
        {
            var bookingId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, siteId, bookingId));
            var result = await grain.RequestAsync(new RequestBookingCommand(
                orgId, siteId, request.Guest, request.RequestedTime, request.PartySize, request.Duration, request.SpecialRequests, request.Occasion, request.Source, request.ExternalRef, request.CustomerId));

            return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}", Hal.Resource(new
            {
                id = result.Id,
                confirmationCode = result.ConfirmationCode,
                createdAt = result.CreatedAt
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}" },
                ["confirm"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/confirm" },
                ["cancel"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/cancel" }
            }));
        });

        group.MapGet("/{bookingId}", async (Guid orgId, Guid siteId, Guid bookingId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, siteId, bookingId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Booking not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}" },
                ["confirm"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/confirm" },
                ["cancel"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/cancel" },
                ["checkin"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/checkin" }
            }));
        });

        group.MapPost("/{bookingId}/confirm", async (
            Guid orgId, Guid siteId, Guid bookingId,
            [FromBody] ConfirmBookingRequest? request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, siteId, bookingId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Booking not found"));

            var result = await grain.ConfirmAsync(request?.ConfirmedTime);
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["booking"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}" }
            }));
        });

        group.MapPost("/{bookingId}/cancel", async (
            Guid orgId, Guid siteId, Guid bookingId,
            [FromBody] CancelBookingRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, siteId, bookingId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Booking not found"));

            await grain.CancelAsync(new CancelBookingCommand(request.Reason, request.CancelledBy));
            return Results.Ok(new { message = "Booking cancelled" });
        });

        group.MapPost("/{bookingId}/checkin", async (
            Guid orgId, Guid siteId, Guid bookingId,
            [FromBody] CheckInRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, siteId, bookingId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Booking not found"));

            var arrivedAt = await grain.RecordArrivalAsync(new RecordArrivalCommand(request.CheckedInBy));
            return Results.Ok(Hal.Resource(new { arrivedAt }, new Dictionary<string, object>
            {
                ["booking"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}" },
                ["seat"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/seat" }
            }));
        });

        group.MapPost("/{bookingId}/seat", async (
            Guid orgId, Guid siteId, Guid bookingId,
            [FromBody] SeatGuestRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, siteId, bookingId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Booking not found"));

            await grain.SeatGuestAsync(new SeatGuestCommand(request.TableId, request.TableNumber, request.SeatedBy));
            return Results.Ok(new { message = "Guest seated" });
        });

        return app;
    }
}
