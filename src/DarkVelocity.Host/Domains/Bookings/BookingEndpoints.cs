using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
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

            var links = new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}" },
                ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
                ["confirm"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/confirm" },
                ["cancel"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/cancel" }
            };

            if (request.CustomerId.HasValue)
            {
                links["customer"] = new { href = $"/api/orgs/{orgId}/customers/{request.CustomerId}" };
            }

            return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}", Hal.Resource(new
            {
                id = result.Id,
                confirmationCode = result.ConfirmationCode,
                createdAt = result.CreatedAt
            }, links));
        });

        group.MapGet("/{bookingId}", async (Guid orgId, Guid siteId, Guid bookingId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, siteId, bookingId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Booking not found"));

            var state = await grain.GetStateAsync();
            var links = BuildBookingLinks(orgId, siteId, bookingId, state);
            return Results.Ok(Hal.Resource(state, links));
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
            var state = await grain.GetStateAsync();
            var links = BuildBookingLinks(orgId, siteId, bookingId, state);
            return Results.Ok(Hal.Resource(result, links));
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
            var state = await grain.GetStateAsync();
            var links = BuildBookingLinks(orgId, siteId, bookingId, state);
            return Results.Ok(Hal.Resource(new { message = "Booking cancelled" }, links));
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
            var state = await grain.GetStateAsync();
            var links = BuildBookingLinks(orgId, siteId, bookingId, state);
            return Results.Ok(Hal.Resource(new { arrivedAt }, links));
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
            var state = await grain.GetStateAsync();
            var links = BuildBookingLinks(orgId, siteId, bookingId, state);
            return Results.Ok(Hal.Resource(new { message = "Guest seated" }, links));
        });

        group.MapPost("/{bookingId}/complete", async (
            Guid orgId, Guid siteId, Guid bookingId,
            [FromBody] CompleteBookingRequest? request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, siteId, bookingId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Booking not found"));

            await grain.RecordDepartureAsync(new RecordDepartureCommand(request?.OrderId));
            var state = await grain.GetStateAsync();
            var links = BuildBookingLinks(orgId, siteId, bookingId, state);
            return Results.Ok(Hal.Resource(new { message = "Booking completed" }, links));
        });

        return app;
    }

    private static Dictionary<string, object> BuildBookingLinks(Guid orgId, Guid siteId, Guid bookingId, BookingState state)
    {
        var links = new Dictionary<string, object>
        {
            ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}" },
            ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
            ["availability"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/availability" },
            ["floor-plans"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/floor-plans" },
            ["waitlist"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/waitlist" }
        };

        // Cross-domain resource links
        if (state.CustomerId.HasValue)
        {
            links["customer"] = new { href = $"/api/orgs/{orgId}/customers/{state.CustomerId}" };
        }

        if (state.TableAssignments.Count > 0)
        {
            var tableLinks = state.TableAssignments.Select(t => new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables/{t.TableId}" }).ToArray();
            links["tables"] = tableLinks;
        }

        if (state.LinkedOrderId.HasValue)
        {
            links["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{state.LinkedOrderId}" };
        }

        // Action links based on booking status
        switch (state.Status)
        {
            case BookingStatus.Requested:
            case BookingStatus.PendingDeposit:
                links["confirm"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/confirm" };
                links["cancel"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/cancel" };
                break;

            case BookingStatus.Confirmed:
                links["checkin"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/checkin" };
                links["cancel"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/cancel" };
                break;

            case BookingStatus.Arrived:
                links["seat"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/seat" };
                links["cancel"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/cancel" };
                break;

            case BookingStatus.Seated:
                links["complete"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/complete" };
                break;

            // No action links for terminal states: Completed, NoShow, Cancelled
        }

        return links;
    }
}
